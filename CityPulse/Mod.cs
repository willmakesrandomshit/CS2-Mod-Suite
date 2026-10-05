using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Unity.Entities;

namespace CityPulse
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger(nameof(CityPulse)).SetShowsErrorsInUI(false);
        public static CityPulseSetting setting { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info("City Pulse v3.0.4-beta.1 loaded | release candidate");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"City Pulse v3.0.4-beta.1 asset location: {asset.path}");

            // ModSetting registers its instance by mod ID in its constructor.
            // Construct defaults first so the UI resolves the live settings object.
            var defaults = new CityPulseSetting(this);
            setting = new CityPulseSetting(this);
            setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(setting));
            AssetDatabase.global.LoadSettings(nameof(CityPulse), setting, defaults);

            updateSystem.UpdateAt<PortfolioSupportUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AnalyticsScheduler>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CityPulseUISystem>(SystemUpdatePhase.UIUpdate);

            // Register with SuiteBridge
            Colossal.UtilitySuite.Interop.SuiteBridge.Register("CityPulse", (entityIndex, pos) =>
            {
                var ui = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<CityPulseUISystem>();
                if (ui != null)
                {
                    // Open City Pulse UI
                }
            });

            log.Info("City Pulse v3.0.4-beta.1 loaded successfully.");
        }

        public void OnDispose()
        {
            log.Info("City Pulse disposing.");
            Colossal.UtilitySuite.Interop.SuiteBridge.Unregister("CityPulse");
            if (setting != null)
            {
                setting.UnregisterInOptionsUI();
                setting = null;
            }
        }
    }
}
