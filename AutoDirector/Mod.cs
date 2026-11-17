using Colossal.Logging;
using Game;
using Game.Modding;

namespace AutoDirector
{
    public class Mod : IMod
    {
        public static ILog Log = LogManager.GetLogger(nameof(AutoDirector)).SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("Auto Director 0.1.0-alpha.1 loaded (moves the camera only; never modifies the simulation)");
            updateSystem.UpdateAt<AutoDirectorSystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose() { Log.Info("Auto Director disposing."); }
    }
}
