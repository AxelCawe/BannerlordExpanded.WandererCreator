using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace BannerlordExpanded.WandererCreator.GameStates
{
    public class CreatorGameManager : MBGameManager, IGameStateManagerListener
    {
        protected override void DoLoadingForGameManager(GameManagerLoadingSteps gameManagerLoadingStep, out GameManagerLoadingSteps nextStep)
        {
            nextStep = GameManagerLoadingSteps.None;
            switch (gameManagerLoadingStep)
            {
                case GameManagerLoadingSteps.PreInitializeZerothStep:
                    nextStep = GameManagerLoadingSteps.FirstInitializeFirstStep;
                    break;
                case GameManagerLoadingSteps.FirstInitializeFirstStep:
                    MBGameManager.LoadModuleData(false);
                    nextStep = GameManagerLoadingSteps.WaitSecondStep;
                    break;
                case GameManagerLoadingSteps.WaitSecondStep:
                    // In the example, StartNewGame() is called here. We skip it as we are already the active manager.
                    nextStep = GameManagerLoadingSteps.SecondInitializeThirdState;
                    break;
                case GameManagerLoadingSteps.SecondInitializeThirdState:
                    MBGlobals.InitializeReferences();
                    var campaign = new Campaign(CampaignGameMode.Campaign);
                    Game.CreateGame(campaign, this);
                    campaign.SetLoadingParameters(Campaign.GameLoadingType.NewCampaign);
                    Game.Current.DoLoading();
                    nextStep = GameManagerLoadingSteps.PostInitializeFourthState;
                    break;
                case GameManagerLoadingSteps.PostInitializeFourthState:
                    bool flag = true;
                    // Reflection to access internal SubModules list
                    var field = typeof(TaleWorlds.MountAndBlade.Module).GetField("_submodules", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        var subModules = field.GetValue(TaleWorlds.MountAndBlade.Module.CurrentModule) as System.Collections.IEnumerable;
                        if (subModules != null)
                        {
                            foreach (var item in subModules)
                            {
                                if (item is MBSubModuleBase subModule)
                                {
                                    flag = (flag && subModule.DoLoading(Game.Current));
                                }
                            }
                        }
                    }
                    nextStep = (flag ? GameManagerLoadingSteps.FinishLoadingFifthStep : GameManagerLoadingSteps.PostInitializeFourthState);
                    break;
                case GameManagerLoadingSteps.FinishLoadingFifthStep:
                    nextStep = (Game.Current.DoLoading() ? GameManagerLoadingSteps.None : GameManagerLoadingSteps.FinishLoadingFifthStep);
                    break;
            }
        }

        public override void OnAfterCampaignStart(Game game)
        {
            // Required implementation
        }

        public override void OnLoadFinished()
        {
            base.OnLoadFinished();
            FileLogger.Log("OnLoadFinished called");

            // Mark that Wanderer Creator is active (for Harmony patches)
            Patches.InventoryPatches.IsCreatorActive = true;

            // Explicitly disable any lingering loading screen
            try { TaleWorlds.Engine.Utilities.DisableGlobalLoadingWindow(); } catch { }

            // FORCE LOAD VITAL XMLs (If they were missed by customized loading)
            try
            {
                FileLogger.Log("Attempting to force load XMLs...");
                // Note: LoadXML typically searches in loaded module paths
                Game.Current.ObjectManager.LoadXML("Monsters");
                // Game.Current.ObjectManager.LoadXML("skeleton_scales"); // Optional
                FileLogger.Log("Force loading XMLs completed (trace).");
            }
            catch (Exception ex)
            {
                FileLogger.Log($"Warning: Failed to force load XMLs: {ex.Message}");
            }

            // Register Listener to handle FaceGen/BarberState Screen creation
            Game.Current.GameStateManager.RegisterListener(this);
            FileLogger.Log("Registered IGameStateManagerListener");

            // Initialize Hero.MainHero for inventory support (like TroopDesigner)
            try
            {
                // First, ensure Campaign.PlayerDefaultFaction is set (required for Clan.PlayerClan)
                // This is internal so we use reflection
                var playerFactionProp = typeof(Campaign).GetProperty("PlayerDefaultFaction",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (playerFactionProp != null && Campaign.Current != null)
                {
                    var existingClan = Campaign.Current.CampaignObjectManager.Find<Clan>("player_faction");
                    if (existingClan == null)
                    {
                        // Try any existing clan
                        foreach (var clan in Clan.All)
                        {
                            if (clan != null && !clan.IsEliminated)
                            {
                                existingClan = clan;
                                break;
                            }
                        }
                    }

                    if (existingClan != null)
                    {
                        playerFactionProp.SetValue(Campaign.Current, existingClan);
                        FileLogger.Log($"Set Campaign.PlayerDefaultFaction to: {existingClan.StringId}");
                    }
                    else
                    {
                        FileLogger.Log("Warning: No clan found for PlayerDefaultFaction");
                    }
                }
                else
                {
                    FileLogger.Log("Warning: Could not find PlayerDefaultFaction property");
                }

                // Use a vanilla character template instead of custom one
                var editorCharacter = CharacterObject.Find("villager_empire");
                if (editorCharacter != null && Hero.MainHero != null)
                {
                    // SetCharacterObject is private, use reflection
                    var setCharMethod = typeof(Hero).GetMethod("SetCharacterObject",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (setCharMethod != null)
                    {
                        setCharMethod.Invoke(Hero.MainHero, new object[] { editorCharacter });
                        FileLogger.Log($"Set Hero.MainHero to: {editorCharacter.StringId}");
                    }
                    else
                    {
                        FileLogger.Log("Warning: SetCharacterObject method not found");
                    }

                    // Set all skills to 300 (like TroopDesigner does)
                    try
                    {
                        var allSkillObjects = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<TaleWorlds.Core.SkillObject>();
                        foreach (var skill in allSkillObjects)
                        {
                            Hero.MainHero.SetSkillValue(skill, 300);
                        }
                        FileLogger.Log($"Set all skills to 300 on Hero.MainHero ({allSkillObjects.Count} skills)");
                    }
                    catch (Exception skillEx)
                    {
                        FileLogger.Log($"Warning: Could not set skills: {skillEx.Message}");
                    }
                }
                else
                {
                    FileLogger.Log($"Warning: Could not set MainHero. Character found: {editorCharacter != null}, MainHero exists: {Hero.MainHero != null}");
                }

                // Clear MainParty's item roster (like TroopDesigner)
                if (TaleWorlds.CampaignSystem.Party.MobileParty.MainParty != null)
                {
                    TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.ItemRoster.Clear();
                    FileLogger.Log("Cleared MainParty ItemRoster");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"Warning: Hero initialization failed: {ex.Message}");
            }

            // CRITICAL: Push CreatorState as base GameState so session doesn't end when BarberState pops
            // Using new directly instead of CreateState<T>() to ensure constructor is called
            Game.Current.GameStateManager.PushState(new CreatorState());
            FileLogger.Log("Pushed CreatorState (base GameState)");

            // Push our blank screen (visual layer)
            TaleWorlds.ScreenSystem.ScreenManager.PushScreen(new BannerlordExpanded.WandererCreator.UI.CreatorScreen());
            FileLogger.Log("Pushed CreatorScreen");

            // Start Controller
            var controller = BannerlordExpanded.WandererCreator.Controllers.EditorController.Instance;
            if (controller != null)
            {
                controller.Start();
                FileLogger.Log("Controller Started");
            }
        }

        public void OnPushState(GameState gameState, bool isTopGameState)
        {
            FileLogger.Log($"OnPushState called for: {gameState.GetType().FullName}, isTop: {isTopGameState}");
            // Note: BarberScreen is automatically created by the game via [GameStateScreen] attribute
            // No manual screen creation needed
        }

        public void OnCreateState(GameState gameState) { }

        public void OnPopState(GameState gameState)
        {
            FileLogger.Log($"OnPopState called for: {gameState.GetType().FullName}");

            if (gameState is BarberState barberState)
            {
                FileLogger.Log("BarberState popped. Returning to editor form...");

                // Try to get the edited BodyProperties
                // This may fail if the character state was cleared, but that's okay
                string bodyPropsString = "";
                try
                {
                    var character = barberState.Character;
                    if (character != null)
                    {
                        var bodyProps = character.GetBodyPropertiesMin(false);
                        bodyPropsString = bodyProps.ToString();
                        FileLogger.Log($"Got BodyProperties: {bodyPropsString}");
                    }
                    else
                    {
                        FileLogger.Log("Note: barberState.Character was null (this is normal if character wasn't modified)");
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Log($"Note: Could not get BodyProperties (this is normal): {ex.Message}");
                }

                // Notify controller with whatever we got (may be empty)
                var controller = BannerlordExpanded.WandererCreator.Controllers.EditorController.Instance;
                controller?.OnFaceGenComplete(bodyPropsString);

                // Clean up BarberScreen properly before returning to editor
                try
                {
                    // [Existing cleanup logic continues...]
                    var topScreen = TaleWorlds.ScreenSystem.ScreenManager.TopScreen;
                    FileLogger.Log($"Current TopScreen: {topScreen?.GetType().FullName ?? "null"}");

                    // Try to clean up the BarberScreen's 3D scene if it's still lingering
                    if (topScreen != null && topScreen.GetType().Name.Contains("BarberScreen"))
                    {
                        FileLogger.Log("Found BarberScreen, attempting to clean up its 3D scene...");
                        try
                        {
                            // Get the _facegenLayer field via reflection
                            var facegenLayerField = topScreen.GetType().GetField("_facegenLayer",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (facegenLayerField != null)
                            {
                                var facegenLayer = facegenLayerField.GetValue(topScreen);
                                if (facegenLayer != null)
                                {
                                    // Try to get SceneLayer and disable it
                                    var sceneLayerProp = facegenLayer.GetType().GetProperty("SceneLayer");
                                    if (sceneLayerProp != null)
                                    {
                                        var sceneLayer = sceneLayerProp.GetValue(facegenLayer);
                                        if (sceneLayer != null)
                                        {
                                            var sceneViewProp = sceneLayer.GetType().GetProperty("SceneView");
                                            if (sceneViewProp != null)
                                            {
                                                var sceneView = sceneViewProp.GetValue(sceneLayer);
                                                if (sceneView != null)
                                                {
                                                    var setEnableMethod = sceneView.GetType().GetMethod("SetEnable");
                                                    if (setEnableMethod != null)
                                                    {
                                                        setEnableMethod.Invoke(sceneView, new object[] { false });
                                                        FileLogger.Log("Disabled BarberScreen SceneView");
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // Call OnFinalize on facegenLayer
                                    var onFinalizeMethod = facegenLayer.GetType().GetMethod("OnFinalize");
                                    if (onFinalizeMethod != null)
                                    {
                                        onFinalizeMethod.Invoke(facegenLayer, null);
                                        FileLogger.Log("Called OnFinalize on facegenLayer");
                                    }
                                }
                            }
                        }
                        catch (Exception cleanupEx)
                        {
                            FileLogger.Log($"BarberScreen cleanup failed: {cleanupEx.Message}");
                        }
                    }

                    // Pop screens to get back to CreatorScreen
                    int poppedCount = 0;
                    while (TaleWorlds.ScreenSystem.ScreenManager.TopScreen != null &&
                           !(TaleWorlds.ScreenSystem.ScreenManager.TopScreen is BannerlordExpanded.WandererCreator.UI.CreatorScreen))
                    {
                        FileLogger.Log($"Popping: {TaleWorlds.ScreenSystem.ScreenManager.TopScreen.GetType().Name}");
                        TaleWorlds.ScreenSystem.ScreenManager.PopScreen();
                        poppedCount++;
                        if (poppedCount > 10) break;
                    }
                    FileLogger.Log($"Popped {poppedCount} screens");

                    // Ensure we have a CreatorScreen
                    if (TaleWorlds.ScreenSystem.ScreenManager.TopScreen == null ||
                        !(TaleWorlds.ScreenSystem.ScreenManager.TopScreen is BannerlordExpanded.WandererCreator.UI.CreatorScreen))
                    {
                        FileLogger.Log("Pushing fresh CreatorScreen...");
                        TaleWorlds.ScreenSystem.ScreenManager.PushScreen(new BannerlordExpanded.WandererCreator.UI.CreatorScreen());
                    }
                    FileLogger.Log("Done cleaning up");
                }
                catch (Exception ex)
                {
                    FileLogger.Log($"Error cleaning up screens: {ex.Message}");
                }
            }
            else if (gameState is InventoryState)
            {
                FileLogger.Log("InventoryState popped. Calculating changes and returning to editor...");
                var controller = BannerlordExpanded.WandererCreator.Controllers.EditorController.Instance;
                controller?.OnInventoryComplete();
            }
        }
        public void OnCleanStates() { }
        public void OnSavedState(GameState gameState) { }
        public void OnLoadedState(GameState gameState) { }
        public void OnSavedGameLoadFinished() { }
    }
}
