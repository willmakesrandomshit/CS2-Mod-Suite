using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Colossal.IO.AssetDatabase;
using Unity.Entities;

namespace TrafficStressTester
{
    public sealed class Mod : IMod
    {
        public static readonly ILog Log = LogManager
            .GetLogger($"{nameof(TrafficStressTester)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);
        public static TrafficStressSetting Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("Traffic Stress Lab v1.5.5-beta.1 BETA loaded");
            // ModSetting registers its instance by mod ID in its constructor.
            // Construct defaults first so the UI resolves the live settings object.
            var defaults = new TrafficStressSetting(this);
            Settings = new TrafficStressSetting(this);
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
            AssetDatabase.global.LoadSettings(nameof(TrafficStressTester), Settings, defaults);
            
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Log.Info($"Traffic Stress Lab asset location: {asset.path}");

            updateSystem.UpdateAt<TrafficStressSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<TrafficStressUISystem>(SystemUpdatePhase.UIUpdate);

            // Register with SuiteBridge
            Colossal.UtilitySuite.Interop.SuiteBridge.Register("TrafficStressLab", (entityIndex, pos) =>
            {
                var ui = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<TrafficStressUISystem>();
                if (ui != null)
                {
                    // Open Traffic Stress Lab
                }
            });

            Log.Info("Traffic Stress Lab v1.5.5-beta.1 BETA loaded successfully.");
        }

        public void OnDispose()
        {
            Log.Info("Traffic Stress Lab disposing.");
            Colossal.UtilitySuite.Interop.SuiteBridge.Unregister("TrafficStressLab");
            TrafficStressState.Reset();
            Settings?.UnregisterInOptionsUI();
            Settings = null;
        }
    }
}
