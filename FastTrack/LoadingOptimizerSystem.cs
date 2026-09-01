using System.Diagnostics;
using Colossal.Serialization.Entities;
using Game;

namespace FastTrack
{
    /// <summary>
    /// Measures the loading stage visible to mods. Optional upload-budget tuning
    /// is experimental, off by default, and does not promise a loading speedup.
    /// </summary>
    public sealed partial class LoadingOptimizerSystem : GameSystemBase
    {
        private readonly Stopwatch m_Stopwatch = new Stopwatch();

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            FastTrackRuntime.EndLoadingBoost();
            FastTrackRuntime.ResetTelemetry();
            m_Stopwatch.Reset();
            if (!mode.IsGame()) return;
            m_Stopwatch.Restart();
            if (Mod.Settings?.DebugLoadingGroup == true)
                FastTrackRuntime.BeginLoadingBoost();
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGameLoadingComplete(purpose, mode);
            if (m_Stopwatch.IsRunning)
            {
                m_Stopwatch.Stop();
                FastTrackRuntime.LastLoadSeconds = (float)m_Stopwatch.Elapsed.TotalSeconds;
            }
            FastTrackRuntime.EndLoadingBoost();

            if (Mod.Settings?.DiagnosticsLogging == true)
                Mod.Log.Info($"LoadingOptimizer: city loaded in {FastTrackRuntime.LastLoadSeconds:F1}s (experimentalBoost={Mod.Settings?.ExperimentalLoadingBoost == true})");
        }

        protected override void OnDestroy()
        {
            FastTrackRuntime.EndLoadingBoost();
            base.OnDestroy();
        }

        protected override void OnUpdate() { }
    }
}
