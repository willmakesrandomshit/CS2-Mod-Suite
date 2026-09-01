using Colossal.Logging;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Unity.Entities;

namespace FastTrack
{
    public sealed class Mod : IMod
    {
        public static readonly ILog Log = LogManager.GetLogger("FastTrack").SetShowsErrorsInUI(false);
        public static FastTrackSetting Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("FastTrack v1.3.5-beta.1 loaded | release candidate | vanilla visual contract");
            var defaults = new FastTrackSetting(this);
            Settings = new FastTrackSetting(this);
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
            AssetDatabase.global.LoadSettings(nameof(FastTrack), Settings, defaults);
            bool migrated = Settings.EnforceVisualContract();
            FastTrackRuntime.Initialize();
            if (migrated) Settings.ApplyAndSave();

            // Read-only camera classification used for diagnostics only.
            updateSystem.UpdateAt<CameraAwarenessSystem>(SystemUpdatePhase.Rendering);

            // Safe loading-only optimization. No renderer/LOD systems are modified.
            updateSystem.UpdateAt<LoadingOptimizerSystem>(SystemUpdatePhase.GameSimulation);

            // Read-only performance signal collection.
            updateSystem.UpdateAt<PerformanceProfilerSystem>(SystemUpdatePhase.Rendering);

            // Legacy-named read-only FPS monitor and visual-contract telemetry.
            updateSystem.UpdateAt<RenderScaleController>(SystemUpdatePhase.Rendering);

            // UI
            updateSystem.UpdateAt<FastTrackUISystem>(SystemUpdatePhase.UIUpdate);

            // Register with SuiteBridge
            Colossal.UtilitySuite.Interop.SuiteBridge.Register("FastTrack", (entityIndex, pos) =>
            {
                var ui = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<FastTrackUISystem>();
                if (ui != null)
                {
                    ui.Open();
                }
            });

            Log.Info("FastTrack v1.3.5-beta.1 loaded successfully; visual mutations=0.");
        }

        public void OnDispose()
        {
            Log.Info("FastTrack disposing.");
            Colossal.UtilitySuite.Interop.SuiteBridge.Unregister("FastTrack");
            Settings?.UnregisterInOptionsUI();
            Settings = null;
            FastTrackRuntime.Dispose();
        }
    }
}
