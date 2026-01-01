using TaleWorlds.ScreenSystem;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Core;

namespace BannerlordExpanded.WandererCreator.UI
{
    public class CreatorScreen : ScreenBase
    {
        public CreatorScreen()
        {
        }

        private GauntletLayer? _layer;
        private SceneLayer? _sceneLayer;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            // Create an empty SceneLayer to cover any lingering 3D content (like BarberScreen)
            try
            {
                var scene = Scene.CreateNewScene(true, false);
                scene.SetName("CreatorEmptyScene");
                _sceneLayer = new SceneLayer(true, true);
                _sceneLayer.SetScene(scene);
                AddLayer(_sceneLayer);
            }
            catch (System.Exception ex)
            {
                FileLogger.Log($"Failed to create SceneLayer: {ex.Message}");
            }

            // Add UI layer on top
            _layer = new GauntletLayer("GauntletLayer", 200);
            AddLayer(_layer);

            // Disable loading window if it's active
            try
            {
                if (LoadingWindow.IsLoadingWindowActive)
                {
                    LoadingWindow.DisableGlobalLoadingWindow();
                }
            }
            catch (System.Exception ex)
            {
                FileLogger.Log($"Warning: Failed to disable loading window: {ex.Message}");
                System.Windows.Forms.MessageBox.Show(
                    $"Failed to disable loading window. The mod may not work correctly.\n\nError: {ex.Message}",
                    "Wanderer Creator Warning",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            // When we return to this screen (e.g. from FaceGen), show the form again
            BannerlordExpanded.WandererCreator.Controllers.EditorController.Instance?.OnScreenActivated();
            // Ensure cursor is visible
            MouseManager.ShowCursor(true);
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();
        }

        protected override void OnFinalize()
        {
            base.OnFinalize();
            if (_sceneLayer != null)
            {
                try
                {
                    _sceneLayer.SceneView?.SetEnable(false);
                }
                catch (System.Exception ex)
                {
                    FileLogger.Log($"Warning: Failed to disable scene view: {ex.Message}");
                    System.Windows.Forms.MessageBox.Show(
                        $"Failed to disable scene view. The mod may not work correctly.\n\nError: {ex.Message}",
                        "Wanderer Creator Warning",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                }
            }
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);

            // Keep cursor visible (clicking background can hide it)
            MouseManager.ShowCursor(true);

            // Check if external editor signaled exit
            if (BannerlordExpanded.WandererCreator.Controllers.EditorController.Instance != null &&
                BannerlordExpanded.WandererCreator.Controllers.EditorController.Instance.ShouldExit)
            {
                // Ensure we only pop once
                if (Game.Current.GameStateManager.ActiveState is BannerlordExpanded.WandererCreator.GameStates.CreatorState)
                {
                    Game.Current.GameStateManager.PopState();
                }
            }
        }
    }
}
