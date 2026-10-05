using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Unity.Entities;

namespace ContourPlus
{
    public class Mod : IMod
    {
        public static ILog Log = LogManager.GetLogger(nameof(ContourPlus)).SetShowsErrorsInUI(false);
        public static ContourPlusSetting SettingInstance { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("Contour Plus v1.2.5-beta.1 loaded | toolbar asset repair");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Log.Info($"Contour Plus v1.2.5-beta.1 asset loaded from: {asset.path}");
            }

            // ModSetting registers its instance by mod ID in its constructor.
            // Construct defaults first so the UI resolves the live settings object.
            var defaults = new ContourPlusSetting(this);
            SettingInstance = new ContourPlusSetting(this);
            SettingInstance.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(SettingInstance));
            AssetDatabase.global.LoadSettings(nameof(ContourPlus), SettingInstance, defaults);

            updateSystem.UpdateAt<PortfolioSupportUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<TopographySystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<ContourOverlaySystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<ContourPlusUISystem>(SystemUpdatePhase.UIUpdate);


            // Register with SuiteBridge
            Colossal.UtilitySuite.Interop.SuiteBridge.Register("ContourPlus", (entityIndex, pos) =>
            {
                var ui = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<ContourPlusUISystem>();
                if (ui != null)
                {
                    // Open Contour Plus UI
                }
            });

            Log.Info("Contour Plus v1.2.5-beta.1 loaded successfully.");
        }

        public void OnDispose()
        {
            Log.Info("Contour Plus disposing.");
            Colossal.UtilitySuite.Interop.SuiteBridge.Unregister("ContourPlus");
            if (SettingInstance != null)
            {
                SettingInstance.UnregisterInOptionsUI();
                SettingInstance = null;
            }
        }
    }
}
