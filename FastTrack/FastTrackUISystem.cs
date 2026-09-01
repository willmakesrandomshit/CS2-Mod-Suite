using System;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.UI;

namespace FastTrack
{
    public sealed partial class FastTrackUISystem : UISystemBase
    {
        private const string Group = "fastTrack";

        // ── bindings ──────────────────────────────────────────────────────
        private ValueBinding<bool>   m_Enabled;
        private ValueBinding<float>  m_Fps;
        private ValueBinding<float>  m_LoadSeconds;
        private ValueBinding<float>  m_LodScale;
        private ValueBinding<float>  m_RenderScale;
        private ValueBinding<float>  m_MainThread;
        private ValueBinding<float>  m_RenderThread;
        private ValueBinding<float>  m_GpuMs;
        private ValueBinding<int>    m_DrawCalls;
        private ValueBinding<bool>   m_Open;
        private ValueBinding<bool>   m_ShowButton;
        private ValueBinding<string> m_Status;
        private ValueBinding<string> m_Bottleneck;
        private ValueBinding<string> m_Preset;
        private ValueBinding<string> m_CameraState;
        private ValueBinding<bool>   m_IsGpuBound;
        private ValueBinding<bool>   m_IsCpuBound;
        private ValueBinding<int>    m_ShadowThrottled;
        private ValueBinding<bool>   m_DecalOptimized;
        private ValueBinding<bool>   m_LoadBoostActive;
        private ValueBinding<bool>   m_LoadBoostEnabled;
        private ValueBinding<bool>   m_SafeMode;
        private ValueBinding<int>    m_VisualMutations;

        private bool m_IsOpen;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_Enabled          = Bind<bool>("enabled",            true);
            m_Fps              = Bind<float>("fps",               0f);
            m_LoadSeconds      = Bind<float>("loadSeconds",       0f);
            m_LodScale         = Bind<float>("lodScale",          1f);
            m_RenderScale      = Bind<float>("renderScale",       1f);
            m_MainThread       = Bind<float>("mainThreadMs",      0f);
            m_RenderThread     = Bind<float>("renderThreadMs",    0f);
            m_GpuMs            = Bind<float>("gpuMs",             0f);
            // Colossal UI bindings do not provide an Int64 writer. Throwing after
            // UISystemBase.OnCreate leaves its preload callback subscribed to a
            // destroyed system and breaks the next New Game transition.
            m_DrawCalls        = Bind<int>  ("drawCalls",          0);
            m_Open             = Bind<bool> ("open",              false);
            m_ShowButton       = Bind<bool> ("showButton",        true);
            m_Status           = Bind<string>("status",           "Starting");
            m_Bottleneck       = Bind<string>("bottleneck",       "Measuring");
            m_Preset           = Bind<string>("preset",           "Balanced");
            m_CameraState      = Bind<string>("cameraState",      "Overview");
            m_IsGpuBound       = Bind<bool> ("isGpuBound",       false);
            m_IsCpuBound       = Bind<bool> ("isCpuBound",       false);
            m_ShadowThrottled  = Bind<int>  ("shadowThrottled",  0);
            m_DecalOptimized   = Bind<bool> ("decalOptimized",   false);
            m_LoadBoostActive  = Bind<bool> ("loadBoostActive",  false);
            m_LoadBoostEnabled = Bind<bool> ("loadBoostEnabled", false);
            m_SafeMode         = Bind<bool> ("safeMode",          true);
            m_VisualMutations  = Bind<int>  ("visualMutations",      0);

            AddBinding(new TriggerBinding(Group, "enable",   Enable));
            AddBinding(new TriggerBinding(Group, "restore",  Restore));
            AddBinding(new TriggerBinding<bool>(Group, "setEnabled", SetEnabled));
            AddBinding(new TriggerBinding(Group, "toggle",   Toggle));
            AddBinding(new TriggerBinding(Group, "close",    Close));
            AddBinding(new TriggerBinding<string>(Group, "setPreset", SetPreset));
            Mod.Log.Info($"FASTTRACK_LIFECYCLE event=created system={nameof(FastTrackUISystem)} world={DescribeWorld()}");
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            Mod.Log.Info($"FASTTRACK_LIFECYCLE event=preload system={nameof(FastTrackUISystem)} world={DescribeWorld()} purpose={purpose} mode={mode}");
            base.OnGamePreload(purpose, mode);
            m_IsOpen = false;
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            Mod.Log.Info($"FASTTRACK_LIFECYCLE event=loading_complete system={nameof(FastTrackUISystem)} world={DescribeWorld()} purpose={purpose} mode={mode}");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            m_Enabled.Update(FastTrackRuntime.Enabled);
            m_Fps.Update(FastTrackRuntime.AverageFps);
            m_LoadSeconds.Update(FastTrackRuntime.LastLoadSeconds);
            m_LodScale.Update(FastTrackRuntime.LodScale);
            m_RenderScale.Update(FastTrackRuntime.RenderScale);
            m_MainThread.Update(FastTrackRuntime.MainThreadMs);
            m_RenderThread.Update(FastTrackRuntime.RenderThreadMs);
            m_GpuMs.Update(FastTrackRuntime.GpuFrameMs);
            m_DrawCalls.Update(ToBindingCount(FastTrackRuntime.DrawCalls));
            m_Open.Update(m_IsOpen);
            m_ShowButton.Update(Mod.Settings?.ShowToolbarButton ?? true);
            m_Status.Update(FastTrackRuntime.Status);
            m_Bottleneck.Update(FastTrackRuntime.Bottleneck);
            m_Preset.Update(Mod.Settings?.Preset.ToString() ?? "Balanced");
            m_CameraState.Update(FastTrackRuntime.CameraState.ToString());
            m_IsGpuBound.Update(FastTrackRuntime.IsGpuBound);
            m_IsCpuBound.Update(FastTrackRuntime.IsCpuBound);
            m_ShadowThrottled.Update(FastTrackRuntime.ShadowThrottledCount);
            m_DecalOptimized.Update(FastTrackRuntime.DecalOptimizeActive);
            m_LoadBoostActive.Update(FastTrackRuntime.LoadingBoostActive);
            m_LoadBoostEnabled.Update(FastTrackRuntime.Enabled && Mod.Settings?.ExperimentalLoadingBoost == true && Mod.Settings?.DebugLoadingGroup == true);
            m_SafeMode.Update(FastTrackRuntime.SafeMode);
            m_VisualMutations.Update(FastTrackRuntime.VisualMutationCount);
        }

        protected override void OnDestroy()
        {
            Mod.Log.Info($"FASTTRACK_LIFECYCLE event=destroying system={nameof(FastTrackUISystem)} world={DescribeWorld()}");
            base.OnDestroy();
        }

        private void Enable()   => FastTrackRuntime.Enable();
        private void Restore()  => FastTrackRuntime.Disable();
        private void SetEnabled(bool enabled)
        {
            if (enabled) Enable();
            else Restore();
        }
        private void Toggle()   => m_IsOpen = !m_IsOpen;
        public void Open()      => m_IsOpen = true;
        private void Close()    => m_IsOpen = false;

        private void SetPreset(string presetName)
        {
            if (Mod.Settings == null) return;
            if (System.Enum.TryParse<OptimizationPreset>(presetName, out var preset))
            {
                Mod.Settings.Preset = preset;
                Mod.Settings.ApplyAndSave();
            }
        }

        // Helper to reduce boilerplate
        private ValueBinding<T> Bind<T>(string key, T defaultVal)
        {
            var b = new ValueBinding<T>(Group, key, defaultVal, ValueWriters.Create<T>());
            AddBinding(b);
            return b;
        }

        private static int ToBindingCount(long value)
        {
            if (value <= 0L) return 0;
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private string DescribeWorld()
        {
            return World == null ? "<null>" : $"{World.Name}#{World.GetHashCode():X8}:created={World.IsCreated}";
        }
    }
}
