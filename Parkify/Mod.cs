using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Unity.Entities;

namespace Parkify
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger(nameof(Parkify)).SetShowsErrorsInUI(false);
        public static ParkifySetting setting { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info("[Parkify] MOD ONLOAD EXECUTED");
            log.Info("Parkify v0.1.0-LOCAL loaded | Build 20260817-PROTOTYPE");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"[Parkify] Executable asset location: {asset.path}");

            setting = new ParkifySetting(this);
            setting.RegisterInOptionsUI();

            AssetDatabase.global.LoadSettings(nameof(Parkify), setting, new ParkifySetting(this));

            updateSystem.UpdateAt<PortfolioSupportUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ParkifySystem>(SystemUpdatePhase.GameSimulation);
            log.Info("[Parkify] System registered: ParkifySystem (GameSimulation)");

            updateSystem.UpdateAt<ParkifyUISystem>(SystemUpdatePhase.UIUpdate);
            log.Info("[Parkify] System registered: ParkifyUISystem (UIUpdate)");
        }

        public void OnDispose()
        {
            log.Info("[Parkify] MOD ONDISPOSE EXECUTED");
            if (setting != null)
            {
                setting.UnregisterInOptionsUI();
                setting = null;
            }
        }
    }
}
