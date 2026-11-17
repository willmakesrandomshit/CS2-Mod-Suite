using Colossal.Logging;
using Game;
using Game.Modding;

namespace BuildGuard
{
    public class Mod : IMod
    {
        public static ILog Log = LogManager.GetLogger(nameof(BuildGuard)).SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("BuildGuard 0.1.0-alpha.1 loaded (read-only edit-preview summary; never modifies the city)");
            updateSystem.UpdateAt<BuildGuardSystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose() { Log.Info("BuildGuard disposing."); }
    }
}
