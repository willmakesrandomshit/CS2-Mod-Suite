using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Unity.Entities;

namespace SaveGuard
{
    public class Mod : IMod
    {
        public static ILog Log = LogManager.GetLogger(nameof(SaveGuard)).SetShowsErrorsInUI(false);
        public static SaveGuardSetting SettingInstance { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("SaveGuard v1.2.4-beta.1 loaded | release candidate");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Log.Info($"SaveGuard v1.2.4-beta.1 asset loaded from: {asset.path}");
            }

            // ModSetting registers its instance by mod ID in its constructor.
            // Construct defaults first so the UI resolves the live settings object.
            var defaults = new SaveGuardSetting(this);
            SettingInstance = new SaveGuardSetting(this);
            SettingInstance.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(SettingInstance));
            AssetDatabase.global.LoadSettings(nameof(SaveGuard), SettingInstance, defaults);

            updateSystem.UpdateAt<SaveGuardSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<SaveGuardUISystem>(SystemUpdatePhase.UIUpdate);

            // Register with SuiteBridge
            Colossal.UtilitySuite.Interop.SuiteBridge.Register("SaveGuard", (entityIndex, pos) =>
            {
                var ui = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<SaveGuardUISystem>();
                if (ui != null)
                {
                    // Open SaveGuard UI
                }
            });

            Log.Info("SaveGuard v1.2.4-beta.1 loaded successfully.");
        }

        public void OnDispose()
        {
            Log.Info("SaveGuard disposing.");
            Colossal.UtilitySuite.Interop.SuiteBridge.Unregister("SaveGuard");
            if (SettingInstance != null)
            {
                SettingInstance.UnregisterInOptionsUI();
                SettingInstance = null;
            }
        }
    }
}
