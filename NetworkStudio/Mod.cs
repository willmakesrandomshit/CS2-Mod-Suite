using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Unity.Entities;

namespace NetworkStudio
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger(nameof(NetworkStudio)).SetShowsErrorsInUI(false);
        public static NetworkStudioSetting setting { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info("[NetworkStudio] MOD ONLOAD EXECUTED");
            log.Info("Network Studio v0.1.0-LOCAL loaded | Build 20260817-PROTOTYPE");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"[NetworkStudio] Executable asset location: {asset.path}");

            setting = new NetworkStudioSetting(this);
            setting.RegisterInOptionsUI();

            AssetDatabase.global.LoadSettings(nameof(NetworkStudio), setting, new NetworkStudioSetting(this));

            updateSystem.UpdateAt<PortfolioSupportUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<NetworkStudioSystem>(SystemUpdatePhase.GameSimulation);
            log.Info("[NetworkStudio] System registered: NetworkStudioSystem (GameSimulation)");

            updateSystem.UpdateAt<NetworkStudioToolSystem>(SystemUpdatePhase.ToolUpdate);
            log.Info("[NetworkStudio] System registered: NetworkStudioToolSystem (ToolUpdate)");

            updateSystem.UpdateAt<NetworkStudioUISystem>(SystemUpdatePhase.UIUpdate);
            log.Info("[NetworkStudio] System registered: NetworkStudioUISystem (UIUpdate)");
        }

        public void OnDispose()
        {
            log.Info("[NetworkStudio] MOD ONDISPOSE EXECUTED");
            if (setting != null)
            {
                setting.UnregisterInOptionsUI();
                setting = null;
            }
        }
    }
}
