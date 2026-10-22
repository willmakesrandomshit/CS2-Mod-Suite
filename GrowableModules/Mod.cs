using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace GrowableModulesPrototype
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(GrowableModulesPrototype)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            updateSystem.UpdateAt<GrowableModulePrototypeSystem>(SystemUpdatePhase.PrefabUpdate);

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
        }
    }
}
