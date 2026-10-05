using Colossal.Logging;
using Game;
using Game.Modding;

namespace AccessStudio
{
    public sealed class Mod : IMod
    {
        internal static readonly ILog Log = LogManager.GetLogger(nameof(AccessStudio)).SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("[AccessStudio] v0.3.3-alpha.1 loading — local release candidate; Remote Service Point remains experimental");
            updateSystem.UpdateAt<PortfolioSupportUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AccessStudioProbeSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<AccessStudioPocSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<AccessStudioToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<AccessStudioUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AccessStudioOverlaySystem>(SystemUpdatePhase.Rendering);
            Log.Info("[AccessStudio] Road B fallback and experimental Road C POC registered. No Harmony patch or Access Studio save serialization is active.");
        }

        public void OnDispose()
        {
            Log.Info("[AccessStudio] Probe disposed.");
        }
    }
}
