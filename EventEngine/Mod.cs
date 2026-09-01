using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Unity.Entities;

namespace EventEngine
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger(nameof(EventEngine)).SetShowsErrorsInUI(false);
        public static EventEngineSetting setting { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info("[EventEngine] MOD ONLOAD EXECUTED");
            log.Info("Event Engine v0.2.1-beta.1 loaded | local release candidate");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"[EventEngine] Executable asset location: {asset.path}");

            // ModSetting registers its instance by mod ID in its constructor.
            // Construct defaults first so the UI resolves the live settings object.
            var defaults = new EventEngineSetting(this);
            setting = new EventEngineSetting(this);
            setting.RegisterInOptionsUI();

            AssetDatabase.global.LoadSettings(nameof(EventEngine), setting, defaults);

            updateSystem.UpdateAt<EventEngineSystem>(SystemUpdatePhase.GameSimulation);
            log.Info("[EventEngine] System registered: EventEngineSystem (GameSimulation)");

            updateSystem.UpdateAt<EventEngineVenueToolSystem>(SystemUpdatePhase.ToolUpdate);
            log.Info("[EventEngine] System registered: EventEngineVenueToolSystem (ToolUpdate)");

            updateSystem.UpdateAt<EventEngineUISystem>(SystemUpdatePhase.UIUpdate);
            log.Info("[EventEngine] System registered: EventEngineUISystem (UIUpdate)");
        }

        public void OnDispose()
        {
            log.Info("[EventEngine] MOD ONDISPOSE EXECUTED");
            if (setting != null)
            {
                setting.UnregisterInOptionsUI();
                setting = null;
            }
        }
    }
}
