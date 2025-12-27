using BannerlordExpanded.WandererCreator.VersionCompatibility;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

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
                    // Get submodules list using GameApiWrapper
                    if (GameApiWrapper.TryGetSubmodules(TaleWorlds.MountAndBlade.Module.CurrentModule, out var subModules) && subModules != null)
                    {
                        foreach (var item in subModules)
                        {
                            if (item is MBSubModuleBase subModule)
                            {
                                flag = (flag && subModule.DoLoading(Game.Current));
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
                if (Campaign.Current != null)
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
                        if (GameApiWrapper.TrySetPlayerDefaultFaction(Campaign.Current, existingClan))
                            FileLogger.Log($"Set Campaign.PlayerDefaultFaction to: {existingClan.StringId}");
                        else
                            FileLogger.Log("Warning: Failed to set PlayerDefaultFaction via GameApiWrapper");
                    }
                    else
                    {
                        FileLogger.Log("Warning: No clan found for PlayerDefaultFaction");
                    }
                }

                // Use a vanilla character template instead of custom one
                var editorCharacter = CharacterObject.Find("villager_empire");
                if (editorCharacter != null && Hero.MainHero != null)
                {
                    // SetCharacterObject is private, use GameApiWrapper
                    if (GameApiWrapper.TrySetCharacterObject(Hero.MainHero, editorCharacter))
                        FileLogger.Log($"Set Hero.MainHero to: {editorCharacter.StringId}");
                    else
                        FileLogger.Log("Warning: Failed to set CharacterObject via GameApiWrapper");

                    // CRITICAL: Set Game.Current.PlayerTroop so that IsPlayerCharacter returns true
                    // Without this, UpdatePlayerCharacterBodyProperties silently skips saving FaceGen edits!
                    Game.Current.PlayerTroop = Hero.MainHero.CharacterObject;
                    FileLogger.Log($"Set Game.Current.PlayerTroop to: {Hero.MainHero.CharacterObject?.StringId}");

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


                // Notify controller with whatever we got (may be empty)
                var controller = BannerlordExpanded.WandererCreator.Controllers.EditorController.Instance;
                controller?.OnFaceGenComplete();

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
                            // Get and cleanup the facegen layer using GameApiWrapper
                            if (GameApiWrapper.TryGetFaceGenLayer(topScreen, out var facegenLayer) && facegenLayer != null)
                            {
                                if (GameApiWrapper.TryCleanupFaceGenLayer(facegenLayer))
                                    FileLogger.Log("Cleaned up BarberScreen FaceGen layer");
                                else
                                    FileLogger.Log("Warning: FaceGen layer cleanup incomplete");
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
