using Game;
using UnityEngine;

namespace FastTrack
{
    /// <summary>
    /// Legacy-named frame-rate monitor. It measures FPS; Adaptive Detail is
    /// applied separately by CameraAwarenessSystem through the game LOD system.
    /// </summary>
    public sealed partial class RenderScaleController : GameSystemBase
    {
        private float m_Elapsed;

        protected override void OnUpdate()
        {
            FastTrackRuntime.SyncSettings(Mod.Settings);

            float delta = UnityEngine.Time.unscaledDeltaTime;
            if (delta <= 0f || delta > 1f) return;

            // Smooth FPS tracking
            float fps = delta > 0f ? 1f / delta : 0f;
            FastTrackRuntime.AverageFps = FastTrackRuntime.AverageFps <= 0f
                ? fps
                : Mathf.Lerp(FastTrackRuntime.AverageFps, fps, 0.05f);

            m_Elapsed += delta;
            if (m_Elapsed < 1f) return;
            m_Elapsed = 0f;
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            m_Elapsed = 0f;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}
