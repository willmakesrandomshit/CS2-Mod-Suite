using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Unity.Entities;

namespace CrashLens
{
    public sealed class Mod : IMod
    {
        public static readonly ILog Log = LogManager.GetLogger("CrashLens").SetShowsErrorsInUI(false);
        public static CrashLensSetting Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("CrashLens v1.1.4-beta.1 BETA loaded");
            // ModSetting registers its instance by mod ID in its constructor.
            // Construct defaults first so the UI resolves the live settings object.
            var defaults = new CrashLensSetting(this);
            Settings = new CrashLensSetting(this);
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
            AssetDatabase.global.LoadSettings(nameof(CrashLens), Settings, defaults);
            CrashLensRuntime.Initialize();
            updateSystem.UpdateAt<HealthMonitorSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<CrashLensUISystem>(SystemUpdatePhase.UIUpdate);

            // Register with SuiteBridge
            Colossal.UtilitySuite.Interop.SuiteBridge.Register("CrashLens", (entityIndex, pos) =>
            {
                var ui = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<CrashLensUISystem>();
                if (ui != null)
                {
                    // Open CrashLens UI
                }
            });

            Log.Info("CrashLens v1.1.4-beta.1 loaded successfully.");
        }

        public void OnDispose()
        {
            Log.Info("CrashLens disposing.");
            Colossal.UtilitySuite.Interop.SuiteBridge.Unregister("CrashLens");
            Settings?.UnregisterInOptionsUI();
            Settings = null;
            CrashLensRuntime.Dispose();
        }
    }
}
