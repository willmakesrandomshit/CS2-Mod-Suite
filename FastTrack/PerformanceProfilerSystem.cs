using System;
using Game;
using Unity.Profiling;
using UnityEngine;

namespace FastTrack
{
    /// <summary>
    /// Samples available profiler counters once per second. Main-thread time
    /// includes waits and rendering work: it is not a simulation-only measurement.
    /// </summary>
    public sealed partial class PerformanceProfilerSystem : GameSystemBase
    {
        // Sample window for rolling average
        private const int k_SampleCount = 8;

        private ProfilerRecorder m_MainThread;
        private ProfilerRecorder m_RenderThread;
        private ProfilerRecorder m_GpuFrameTime;
        private ProfilerRecorder m_GcAlloc;
        private ProfilerRecorder m_DrawCalls;
        private ProfilerRecorder m_SetPassCalls;

        private readonly float[] m_MainThreadSamples  = new float[k_SampleCount];
        private readonly float[] m_RenderThreadSamples = new float[k_SampleCount];
        private readonly float[] m_GpuSamples          = new float[k_SampleCount];
        private int m_SampleIndex;
        private float m_Elapsed;
        private float m_LogElapsed;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_MainThread   = TryStart(ProfilerCategory.Internal, "Main Thread");
            m_RenderThread = TryStart(ProfilerCategory.Internal, "Render Thread");
            m_GpuFrameTime = TryStart(ProfilerCategory.Render, "GPU Frame Time");
            m_GcAlloc      = TryStart(ProfilerCategory.Memory, "GC Allocated In Frame");
            m_DrawCalls    = TryStart(ProfilerCategory.Render, "Draw Calls Count");
            m_SetPassCalls = TryStart(ProfilerCategory.Render, "SetPass Calls Count");
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            Array.Clear(m_MainThreadSamples, 0, k_SampleCount);
            Array.Clear(m_RenderThreadSamples, 0, k_SampleCount);
            Array.Clear(m_GpuSamples, 0, k_SampleCount);
            m_SampleIndex = 0;
            m_Elapsed = m_LogElapsed = 0f;
        }

        protected override void OnUpdate()
        {
            if (!FastTrackRuntime.Enabled || Mod.Settings?.DebugProfilerGroup != true)
            {
                m_Elapsed = 0f;
                FastTrackRuntime.Bottleneck = "Monitoring paused";
                return;
            }

            float delta = UnityEngine.Time.unscaledDeltaTime;
            if (delta <= 0f || delta > 1f) return;
            m_Elapsed    += delta;
            m_LogElapsed += delta;
            if (m_Elapsed < 1f) return;
            m_Elapsed = 0f;

            // Write into ring buffers
            int idx = m_SampleIndex % k_SampleCount;
            m_MainThreadSamples[idx]   = NsToMs(Read(m_MainThread));
            m_RenderThreadSamples[idx] = NsToMs(Read(m_RenderThread));
            m_GpuSamples[idx]          = NsToMs(Read(m_GpuFrameTime));
            m_SampleIndex = (m_SampleIndex + 1) % k_SampleCount;

            // Publish smoothed averages
            FastTrackRuntime.MainThreadMs   = Average(m_MainThreadSamples);
            FastTrackRuntime.RenderThreadMs = Average(m_RenderThreadSamples);
            FastTrackRuntime.GpuFrameMs     = Average(m_GpuSamples);
            FastTrackRuntime.GcBytesPerFrame = Read(m_GcAlloc);
            FastTrackRuntime.DrawCalls      = Read(m_DrawCalls);
            FastTrackRuntime.SetPassCalls   = Read(m_SetPassCalls);

            // Classify bottleneck
            float budgetMs     = 1000f / Mathf.Max(20f, Mod.Settings?.TargetFps ?? 30);
            bool  mainOverBudget  = FastTrackRuntime.MainThreadMs  > budgetMs * 1.25f;
            bool  gpuOverBudget   = FastTrackRuntime.GpuFrameMs    > budgetMs * 1.25f;
            bool  highDrawCalls   = FastTrackRuntime.DrawCalls      >= 1800;
            bool  highSetPass     = FastTrackRuntime.SetPassCalls   >= 400;

            if (gpuOverBudget)
                FastTrackRuntime.Bottleneck = "GPU frame over target (sampled)";
            else if (mainOverBudget)
                FastTrackRuntime.Bottleneck = "Main thread over target (includes waits)";
            else if (highDrawCalls || highSetPass)
                FastTrackRuntime.Bottleneck = "High draw workload (heuristic)";
            else if (FastTrackRuntime.MainThreadMs <= 0f || FastTrackRuntime.GpuFrameMs <= 0f)
                FastTrackRuntime.Bottleneck = "Incomplete timing counters";
            else
                FastTrackRuntime.Bottleneck = "No over-target timing sample";

            FastTrackRuntime.IsGpuBound = gpuOverBudget;
            FastTrackRuntime.IsCpuBound = mainOverBudget && !FastTrackRuntime.IsGpuBound;

            if (m_LogElapsed >= 60f)
            {
                m_LogElapsed = 0f;
                if (Mod.Settings?.DiagnosticsLogging == true)
                {
                    Mod.Log.Info(
                        $"PERF fps={FastTrackRuntime.AverageFps:F1} " +
                        $"main={FastTrackRuntime.MainThreadMs:F2}ms " +
                        $"gpu={FastTrackRuntime.GpuFrameMs:F2}ms " +
                        $"draws={FastTrackRuntime.DrawCalls} " +
                        $"setpass={FastTrackRuntime.SetPassCalls} " +
                        $"bottleneck={FastTrackRuntime.Bottleneck}");
                }
            }
        }

        private static float NsToMs(long ns) => ns > 0 ? ns / 1_000_000f : 0f;

        private static ProfilerRecorder TryStart(ProfilerCategory category, string counter)
        {
            try { return ProfilerRecorder.StartNew(category, counter, k_SampleCount); }
            catch (Exception ex)
            {
                Mod.Log.Warn($"Profiler counter unavailable: {counter}: {ex.Message}");
                return default;
            }
        }

        private static long Read(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue : 0;

        private static float Average(float[] samples)
        {
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i] <= 0f) continue;
                sum += samples[i];
                count++;
            }
            return count == 0 ? 0f : sum / count;
        }

        protected override void OnDestroy()
        {
            if (m_MainThread.Valid) m_MainThread.Dispose();
            if (m_RenderThread.Valid) m_RenderThread.Dispose();
            if (m_GpuFrameTime.Valid) m_GpuFrameTime.Dispose();
            if (m_GcAlloc.Valid) m_GcAlloc.Dispose();
            if (m_DrawCalls.Valid) m_DrawCalls.Dispose();
            if (m_SetPassCalls.Valid) m_SetPassCalls.Dispose();
            base.OnDestroy();
        }
    }
}
