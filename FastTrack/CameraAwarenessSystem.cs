using Game;
using Game.Rendering;
using Unity.Entities;
using UnityEngine;

namespace FastTrack
{
    /// <summary>
    /// Reads camera state and drives FastTrack's opt-in native LOD adjustment.
    /// </summary>
    public sealed partial class CameraAwarenessSystem : GameSystemBase
    {
        private const float k_ZoomedInAlt      =  80f;   // m — user inspecting detail
        private const float k_OverviewAlt      = 250f;   // m — normal city overview
        private const float k_AerialAlt        = 500f;   // m — aerial/district view
        // above k_AerialAlt = UltraAerial — maximum user-selected reduction
        private const float k_CameraMetricWorldUnits = 10000f;

        private const float k_MovingThreshold  =  15f;   // m/s — camera is panning fast
        private const float k_SmoothFactor     =  0.15f; // EMA smoothing

        private Vector3 m_LastCamPos;
        private float   m_SmoothedAlt;
        private float   m_SmoothedSpeed;
        private bool    m_Initialized;

        protected override void OnUpdate()
        {
            if (!FastTrackRuntime.Enabled) return;

            // Camera.main's world-space Y is not the gameplay camera's height
            // above the map. CS2 exposes its active controller and height
            // metric through CameraUpdateSystem; use those so zooming toward
            // elevated terrain still returns to full detail.
            var cameraSystem = World?.GetExistingSystemManaged<CameraUpdateSystem>();
            var cameraController = cameraSystem?.activeCameraController;
            if (cameraSystem == null || cameraController == null)
            {
                FastTrackRuntime.RestoreAdaptiveLod();
                m_Initialized = false;
                return;
            }

            Vector3 camPos = cameraController.position;
            float   dt     = UnityEngine.Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            // Speed of camera movement in world units per second
            float speed = m_Initialized
                ? Vector3.Distance(camPos, m_LastCamPos) / dt
                : 0f;
            m_LastCamPos = camPos;
            m_Initialized = true;

            // CameraHeightMetric is CS2's normalized 0..1 shadow-height metric,
            // based on a 10,000-world-unit range. Convert it to world units so
            // the thresholds above stay in meters. Smooth to avoid flicker.
            float rawAlt = cameraSystem.CameraHeightMetric * k_CameraMetricWorldUnits;
            m_SmoothedAlt   = Mathf.Lerp(m_SmoothedAlt,   rawAlt, k_SmoothFactor);
            m_SmoothedSpeed = Mathf.Lerp(m_SmoothedSpeed, speed,   k_SmoothFactor);

            FastTrackRuntime.CameraAltitude = m_SmoothedAlt;

            // Classify camera state
            // Moving fast → treat as Overview minimum (avoid aggressive quality drop during pan)
            bool moving = m_SmoothedSpeed > k_MovingThreshold;
            FastTrackRuntime.CameraMoving = moving;

            CameraState state;
            if (m_SmoothedAlt <= k_ZoomedInAlt)
                state = CameraState.ZoomedIn;
            else if (m_SmoothedAlt <= k_OverviewAlt || moving)
                state = CameraState.Overview;
            else if (m_SmoothedAlt <= k_AerialAlt)
                state = CameraState.Aerial;
            else
                state = CameraState.UltraAerial;

            FastTrackRuntime.CameraState = state;
            FastTrackRuntime.UpdateAdaptiveLod(state);
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            FastTrackRuntime.RestoreAdaptiveLod();
            m_Initialized = false;
        }

        protected override void OnDestroy()
        {
            FastTrackRuntime.RestoreAdaptiveLod();
            m_Initialized = false;
            base.OnDestroy();
        }
    }
}
