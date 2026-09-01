using System;
using UnityEngine;

namespace FastTrack
{
    /// <summary>
    /// Central runtime state for FastTrack's visual-safe feature set.
    ///
    /// VISUAL CONTRACT:
    /// FastTrack never writes LOD bias, shadow distance/cascades, pixel-light
    /// count, reflection, particle, decal, render-scale, culling or HDRP state.
    /// Only the temporary city-loading upload budget is changed, and it is
    /// restored as soon as loading completes.
    /// </summary>
    internal static class FastTrackRuntime
    {
        // ── vanilla capture ────────────────────────────────────────────────
        private static bool   s_Initialized;
        private static int    s_UploadTimeSlice;
        private static int    s_UploadBufferSize;
        private static bool   s_UploadPersistent;
        private static ThreadPriority s_LoadingPriority;
        private static bool   s_LoadingOptimizationFaulted;
        private static int    s_AppliedTimeSlice;
        private static int    s_AppliedBufferSize;

        // ── state ──────────────────────────────────────────────────────────
        public static bool   Enabled             { get; private set; } = true;
        public static bool   SafeMode            { get; private set; } = true;
        public static bool   LoadingBoostActive  { get; private set; }
        public static int    VisualMutationCount => 0;

        // performance signals (written by PerformanceProfilerSystem each second)
        public static float  AverageFps          { get; set; }
        public static float  LastLoadSeconds      { get; set; }
        public static float  MainThreadMs         { get; set; }
        public static float  RenderThreadMs       { get; set; }
        public static float  GpuFrameMs           { get; set; }
        public static long   GcBytesPerFrame      { get; set; }
        public static long   DrawCalls            { get; set; }
        public static long   SetPassCalls         { get; set; }
        public static bool   IsGpuBound           { get; set; }
        public static bool   IsCpuBound           { get; set; }
        public static string Bottleneck           { get; set; } = "Measuring";
        public static string Status               { get; set; } = "Starting";

        // camera state (written by CameraAwarenessSystem)
        public static CameraState CameraState     { get; set; } = CameraState.Overview;
        public static float       CameraAltitude  { get; set; }

        // Legacy telemetry retained for UI/binding compatibility. Visual
        // mutation systems were removed in v1.3.4 and these remain zero/false.
        public static int    ShadowThrottledCount { get; private set; }
        public static bool   DecalOptimizeActive  { get; private set; }

        // Contract telemetry: native values are never changed.
        public static float  LodScale             { get; private set; } = 1f;
        public static float  RenderScale          { get; private set; } = 1f;

        // ── initialize ────────────────────────────────────────────────────
        public static void Initialize()
        {
            if (s_Initialized) return;
            s_Initialized = true;
            Enabled       = true;
            SyncSettings(Mod.Settings);
            MaintainVisualContract();
        }

        // ── loading optimization ──────────────────────────────────────────
        /// <summary>
        /// Opt-in loading-budget experiment. No speedup is guaranteed. Capture
        /// immediately before writing, not at mod startup (other mods may tune it).
        /// </summary>
        public static void BeginLoadingBoost()
        {
            if (!Enabled || !s_Initialized || LoadingBoostActive) return;
            if (Mod.Settings?.ExperimentalLoadingBoost != true) return;
            if (Mod.Settings?.DebugLoadingGroup != true) return;
            if (s_LoadingOptimizationFaulted) return;

            try
            {
                s_UploadTimeSlice = QualitySettings.asyncUploadTimeSlice;
                s_UploadBufferSize = QualitySettings.asyncUploadBufferSize;
                s_UploadPersistent = QualitySettings.asyncUploadPersistentBuffer;
                s_LoadingPriority = Application.backgroundLoadingPriority;
                s_AppliedTimeSlice = Mathf.Max(s_UploadTimeSlice, 8);
                s_AppliedBufferSize = Mathf.Max(s_UploadBufferSize, 64);
                // Mark before the first write so a partial failure can restore.
                LoadingBoostActive = true;
                QualitySettings.asyncUploadTimeSlice        = s_AppliedTimeSlice;
                QualitySettings.asyncUploadBufferSize       = s_AppliedBufferSize;
                QualitySettings.asyncUploadPersistentBuffer = true;
                Application.backgroundLoadingPriority       = ThreadPriority.High;
            }
            catch (Exception ex)
            {
                s_LoadingOptimizationFaulted = true;
                Mod.Log.Error(ex, "Loading optimization faulted; disabled for this process and falling back to vanilla.");
                EndLoadingBoost();
            }
        }

        public static void EndLoadingBoost()
        {
            if (!s_Initialized || !LoadingBoostActive) return;
            // Do not retry every frame or overwrite a different value written by
            // another mod. Each field is restored independently after partial faults.
            LoadingBoostActive = false;
            RestoreLoadingValue(() => { if (QualitySettings.asyncUploadTimeSlice == s_AppliedTimeSlice) QualitySettings.asyncUploadTimeSlice = s_UploadTimeSlice; });
            RestoreLoadingValue(() => { if (QualitySettings.asyncUploadBufferSize == s_AppliedBufferSize) QualitySettings.asyncUploadBufferSize = s_UploadBufferSize; });
            RestoreLoadingValue(() => { if (QualitySettings.asyncUploadPersistentBuffer) QualitySettings.asyncUploadPersistentBuffer = s_UploadPersistent; });
            RestoreLoadingValue(() => { if (Application.backgroundLoadingPriority == ThreadPriority.High) Application.backgroundLoadingPriority = s_LoadingPriority; });
        }

        private static void RestoreLoadingValue(Action restore)
        {
            try { restore(); }
            catch (Exception ex)
            {
                s_LoadingOptimizationFaulted = true;
                Mod.Log.Error(ex, "Failed to restore a loading setting. Loading boost disabled; restart CS2 before using it again.");
            }
        }

        /// <summary>
        /// Publish and assert FastTrack's no-visual-mutation contract. This method
        /// intentionally performs no Unity/HDRP writes.
        /// </summary>
        public static void MaintainVisualContract()
        {
            LodScale = 1f;
            RenderScale = 1f;
            ShadowThrottledCount = 0;
            DecalOptimizeActive = false;
            Status = s_LoadingOptimizationFaulted ? "Loading boost faulted — restart CS2" : Enabled ? "Read-only visual monitoring" : "FastTrack paused";
        }

        // ── settings sync ─────────────────────────────────────────────────
        public static void SyncSettings(FastTrackSetting settings)
        {
            if (settings == null) return;
            Enabled = settings.Enabled;
            SafeMode = true;
            MaintainVisualContract();
            if (!Enabled || !settings.ExperimentalLoadingBoost || !settings.DebugLoadingGroup) EndLoadingBoost();
        }

        public static void Enable()
        {
            Enabled = true;
            if (Mod.Settings != null) { Mod.Settings.Enabled = true; Mod.Settings.ApplyAndSave(); }
            MaintainVisualContract();
        }

        /// <summary>
        /// User-facing pause. Persist the setting as well as restoring runtime
        /// state so the per-frame settings synchronizer cannot immediately
        /// re-enable FastTrack on the next frame.
        /// </summary>
        public static void Disable()
        {
            if (Mod.Settings != null)
            {
                Mod.Settings.Enabled = false;
                Mod.Settings.ApplyAndSave();
                return;
            }

            RestoreVanilla();
        }

        internal static void ResetTelemetry()
        {
            AverageFps = LastLoadSeconds = MainThreadMs = RenderThreadMs = GpuFrameMs = 0f;
            GcBytesPerFrame = DrawCalls = SetPassCalls = 0;
            IsGpuBound = IsCpuBound = false;
            Bottleneck = "Measuring";
        }

        public static void OnSettingsChanged()
        {
            SyncSettings(Mod.Settings);
        }

        public static void Dispose()
        {
            RestoreVanilla();
            s_Initialized = false;
            ResetTelemetry();
        }

        // ── restore ───────────────────────────────────────────────────────
        /// <summary>
        /// Full vanilla restore. Called on disable/unload.
        /// Only an active, opt-in loading-budget experiment needs restoration.
        /// </summary>
        public static void RestoreVanilla()
        {
            if (!s_Initialized) return;
            EndLoadingBoost();
            Enabled  = false;
            MaintainVisualContract();
        }
    }

    /// <summary>Camera altitude zone used to gate optimization aggressiveness.</summary>
    public enum CameraState { ZoomedIn, Overview, Aerial, UltraAerial }
}
