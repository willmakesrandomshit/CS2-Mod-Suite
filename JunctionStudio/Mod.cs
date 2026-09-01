using Colossal.Logging;
using Game;
using Game.Modding;
using Unity.Entities;

namespace JunctionStudio
{
    public sealed class Mod : IMod
    {
        public static readonly ILog Log = LogManager.GetLogger("JunctionStudio.Mod").SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("Junction Studio v1.1.4-beta.1 loaded | release candidate");
            updateSystem.UpdateAt<AreaPaintToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<JunctionStudioSystem>(SystemUpdatePhase.UIUpdate);

            // Register with SuiteBridge
            Colossal.UtilitySuite.Interop.SuiteBridge.Register("JunctionStudio", (entityIndex, pos) =>
            {
                var ui = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<JunctionStudioSystem>();
                if (ui != null)
                {
                    // Open Junction Studio
                }
            });

            Log.Info("Junction Studio v1.1.4-beta.1 loaded successfully.");
        }

        public void OnDispose()
        {
            Log.Info("Disposed Junction Studio");
            Colossal.UtilitySuite.Interop.SuiteBridge.Unregister("JunctionStudio");
        }
    }
}
