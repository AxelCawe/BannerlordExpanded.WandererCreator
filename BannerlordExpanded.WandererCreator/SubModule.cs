using TaleWorlds.MountAndBlade;
using System;


namespace BannerlordExpanded.WandererCreator
{
    public class SubModule : MBSubModuleBase
    {
        private static System.Collections.Concurrent.ConcurrentQueue<Action> _mainThreadActions = new System.Collections.Concurrent.ConcurrentQueue<Action>();

        public static void EnqueueMainThreadAction(Action action)
        {
            _mainThreadActions.Enqueue(action);
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            while (_mainThreadActions.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    // Log error but don't crash game
                    FileLogger.Log($"Error executing Main Thread Action: {ex}");
                }
            }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // Initialize Harmony patches for inventory support
            try
            {
                var harmony = new HarmonyLib.Harmony("BannerlordExpanded.WandererCreator");
                harmony.PatchAll();
                FileLogger.Log("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                FileLogger.Log($"Failed to apply Harmony patches: {ex.Message}");
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
        }

        private static bool _buttonAdded = false;

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            // Reset the creator active flag (when returning to main menu)
            Patches.InventoryPatches.IsCreatorActive = false;

            // Guard against duplicate button registration
            if (_buttonAdded) return;
            _buttonAdded = true;

            TaleWorlds.MountAndBlade.Module.CurrentModule.AddInitialStateOption(new TaleWorlds.MountAndBlade.InitialStateOption(
                "WandererCreator",
                new TaleWorlds.Localization.TextObject("{=*}Wanderer Creator"),
                9999,
                () =>
                {
                    // Check Fullscreen Mode
                    var controller = new BannerlordExpanded.WandererCreator.Controllers.EditorController();
                    if (controller.CanStart())
                    {
                        // Start a new Game session to allow FaceGen/Inventory access
                        MBGameManager.StartNewGame(new BannerlordExpanded.WandererCreator.GameStates.CreatorGameManager());
                    }
                },
                () => (false, null)
            ));
        }
    }
}