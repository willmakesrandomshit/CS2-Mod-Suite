using System;
using Game;
using Game.Rendering;
using Game.SceneFlow;
using Unity.Entities;
using UnityEngine;

namespace FastTrack
{
    /// <summary>
    /// Central runtime state for FastTrack's opt-in adaptive detail feature.
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

        // Adaptive detail writes only the game's own runtime LOD multiplier.
        // The native user setting is not modified or saved.
        private static RenderingSystem s_RenderingSystem;
        private static bool   s_LodBaselineCaptured;
        private static bool   s_LodWriteActive;
        private static bool   s_LodOptimizationFaulted;
        private static float  s_LodBaseline;
        private static float  s_LastAppliedLod;

        private const float k_StockLowLod = 0.25f;

        // ── state ──────────────────────────────────────────────────────────
        public static bool   Enabled             { get; private set; } = true;
        public static bool   LoadingBoostActive  { get; private set; }
        public static bool   AdaptiveLodActive   { get; private set; }

        // performance signals (written by PerformanceProfilerSystem each second)
        public static float  AverageFps          { get; set; }
        public static int    SampleSequence      { get; set; }
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
        public static bool        CameraMoving    { get; set; }

        // Legacy telemetry retained for UI/binding compatibility.
        public static int    ShadowThrottledCount { get; private set; }
        public static bool   DecalOptimizeActive  { get; private set; }

        // Runtime telemetry for the adaptive LOD multiplier.
        public static float  LodScale             { get; private set; } = 1f;
        public static float  RenderScale          { get; private set; } = 1f;

        // ── initialize ────────────────────────────────────────────────────
        public static void Initialize()
        {
            if (s_Initialized) return;
            s_Initialized = true;
            Enabled       = true;
            SyncSettings(Mod.Settings);
            RefreshStatus();
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

        /// <summary>Apply camera-aware LOD using the game's native rendering setting.</summary>
        public static void UpdateAdaptiveLod(CameraState cameraState)
        {
            ShadowThrottledCount = 0;
            DecalOptimizeActive = false;

            var settings = Mod.Settings;
            var manager = GameManager.instance;
            if (!Enabled || settings?.AdaptiveLodEnabled != true || manager == null || !manager.gameMode.IsGame() || manager.isGameLoading)
            {
                RestoreAdaptiveLod();
                RefreshStatus();
                return;
            }

            try
            {
                var currentRenderingSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<RenderingSystem>();
                if (currentRenderingSystem == null)
                {
                    RefreshStatus();
                    return;
                }

                if (!ReferenceEquals(currentRenderingSystem, s_RenderingSystem))
                {
                    RestoreAdaptiveLod();
                    s_RenderingSystem = currentRenderingSystem;
                    s_LodBaseline = currentRenderingSystem.levelOfDetail;
                    s_LodBaselineCaptured = true;
                    s_LodOptimizationFaulted = false;
                    Mod.Log.Info($"Adaptive LOD attached to game RenderingSystem; native LOD={s_LodBaseline:F3}.");
                }

                if (!s_LodBaselineCaptured || s_LodOptimizationFaulted) { RefreshStatus(); return; }

                float current = s_RenderingSystem.levelOfDetail;
                if (s_LodWriteActive && !Mathf.Approximately(current, s_LastAppliedLod))
                {
                    // The user, game, or another mod changed the native setting.
                    // Treat that new value as the baseline before adapting again.
                    s_LodBaseline = current;
                    s_LodWriteActive = false;
                    Mod.Log.Info($"Adaptive LOD rebased to external game value {s_LodBaseline:F3}.");
                }
                else if (!s_LodWriteActive)
                {
                    // Keep the baseline aligned with changes made while the
                    // optimization is inactive or at a full-detail view.
                    s_LodBaseline = current;
                }

                float reduction = Mathf.Clamp(settings.AdaptiveLodReductionPercent, 10, 40) / 100f;
                float altitudeStrength = cameraState == CameraState.UltraAerial ? 1f
                    : cameraState == CameraState.Aerial ? 0.5f
                    : 0f;
                float target = s_LodBaseline;
                if (altitudeStrength > 0f && s_LodBaseline > 0f)
                {
                    float adapted = s_LodBaseline * (1f - reduction * altitudeStrength);
                    float lodFloor = Mathf.Min(s_LodBaseline, k_StockLowLod);
                    target = Mathf.Max(lodFloor, adapted);
                }

                if (!Mathf.Approximately(current, target))
                {
                    s_RenderingSystem.levelOfDetail = target;
                    s_LastAppliedLod = target;
                    s_LodWriteActive = !Mathf.Approximately(target, s_LodBaseline);
                    Mod.Log.Info($"Adaptive LOD applied: camera={cameraState}, native={s_LodBaseline:F3}, runtime={target:F3}.");
                }
                else if (Mathf.Approximately(target, s_LodBaseline))
                {
                    s_LodWriteActive = false;
                }

                AdaptiveLodActive = s_LodWriteActive;
                LodScale = s_LodBaseline > 0f ? target / s_LodBaseline : 1f;
            }
            catch (Exception ex)
            {
                s_LodOptimizationFaulted = true;
                Mod.Log.Error(ex, "Adaptive LOD failed. FastTrack is restoring the native value and disabling this optimization for the current session.");
                RestoreAdaptiveLod();
            }

            RefreshStatus();
        }

        public static void RestoreAdaptiveLod()
        {
            if (s_LodWriteActive && s_RenderingSystem != null)
            {
                try
                {
                    // Restore only if our last value is still present. This avoids
                    // overwriting an intervening change by the game or another mod.
                    if (Mathf.Approximately(s_RenderingSystem.levelOfDetail, s_LastAppliedLod))
                    {
                        s_RenderingSystem.levelOfDetail = s_LodBaseline;
                        Mod.Log.Info($"Adaptive LOD restored native value {s_LodBaseline:F3}.");
                    }
                    else
                    {
                        Mod.Log.Info("Adaptive LOD left an external LOD change untouched during restore.");
                    }
                }
                catch (Exception ex)
                {
                    s_LodOptimizationFaulted = true;
                    Mod.Log.Error(ex, "Failed to restore native LOD. Reopen FastTrack after returning to the city to retry.");
                }
            }

            s_LodWriteActive = false;
            AdaptiveLodActive = false;
            LodScale = 1f;
        }

        private static void RefreshStatus()
        {
            Status = s_LoadingOptimizationFaulted
                ? "Loading boost faulted — restart CS2"
                : s_LodOptimizationFaulted
                    ? "Adaptive Detail failed — native setting restored"
                    : !Enabled
                        ? "FastTrack paused"
                        : AdaptiveLodActive
                            ? $"Adaptive detail active · LOD {LodScale:P0}"
                            : Mod.Settings?.AdaptiveLodEnabled == true
                                ? "Adaptive Detail on · full detail at this view"
                                : "Monitoring · Adaptive Detail off";
        }

        // ── settings sync ─────────────────────────────────────────────────
        public static void SyncSettings(FastTrackSetting settings)
        {
            if (settings == null) return;
            Enabled = settings.Enabled;
            if (!Enabled || !settings.AdaptiveLodEnabled) RestoreAdaptiveLod();
            RefreshStatus();
            if (!Enabled || !settings.ExperimentalLoadingBoost || !settings.DebugLoadingGroup) EndLoadingBoost();
        }

        public static void Enable()
        {
            Enabled = true;
            if (Mod.Settings != null) { Mod.Settings.Enabled = true; Mod.Settings.ApplyAndSave(); }
            RefreshStatus();
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
            AdaptiveLodActive = false;
            LodScale = 1f;
            RenderScale = 1f;
            Bottleneck = "Measuring";
        }

        public static void OnSettingsChanged()
        {
            SyncSettings(Mod.Settings);
        }

        public static void Dispose()
        {
            RestoreVanilla();
            s_RenderingSystem = null;
            s_LodBaselineCaptured = false;
            s_LodOptimizationFaulted = false;
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
            RestoreAdaptiveLod();
            Enabled  = false;
            RefreshStatus();
        }
    }

    /// <summary>Camera altitude zone used to gate optimization aggressiveness.</summary>
    public enum CameraState { ZoomedIn, Overview, Aerial, UltraAerial }
}
