using Colossal.Logging;
using Game;
using Game.Modding;

namespace CitizenStories
{
    public class Mod : IMod
    {
        public static ILog Log = LogManager.GetLogger(nameof(CitizenStories)).SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("Citizen Stories 0.1.0-alpha.1 loaded (read-only; never modifies the simulation)");
            updateSystem.UpdateAt<CitizenStoriesUISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            Log.Info("Citizen Stories disposing.");
        }
    }
}
