using Game;
using UnityEngine;

namespace FastTrack
{
    /// <summary>
    /// Reads camera state every frame and publishes altitude zone + movement info.
    /// The result is exposed only as read-only diagnostic telemetry.
    ///
    /// Does NOT modify any rendering state itself — purely observes.
    /// </summary>
    public sealed partial class CameraAwarenessSystem : GameSystemBase
    {
        private const float k_ZoomedInAlt      =  80f;   // m — user inspecting detail
        private const float k_OverviewAlt      = 250f;   // m — normal city overview
        private const float k_AerialAlt        = 500f;   // m — aerial/district view
        // above k_AerialAlt = UltraAerial — aggressive optimization safe

        private const float k_MovingThreshold  =  15f;   // m/s — camera is panning fast
        private const float k_SmoothFactor     =  0.15f; // EMA smoothing

        private Camera   m_MainCamera;
        private Vector3  m_LastCamPos;
        private float    m_SmoothedAlt;
        private float    m_SmoothedSpeed;
        private bool     m_Initialized;

        protected override void OnUpdate()
        {
            if (!FastTrackRuntime.Enabled) return;
            if (Mod.Settings?.DebugCameraObserverGroup != true) return;

            // Lazy-resolve main camera (it may not exist during loading)
            if (m_MainCamera == null || !m_MainCamera.isActiveAndEnabled)
            {
                m_MainCamera = Camera.main;
                if (m_MainCamera == null) return;
                m_LastCamPos   = m_MainCamera.transform.position;
                m_SmoothedAlt  = m_MainCamera.transform.position.y;
                m_Initialized  = false;
            }

            Vector3 camPos = m_MainCamera.transform.position;
            float   dt     = UnityEngine.Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            // Speed of camera movement in world units per second
            float speed = m_Initialized
                ? Vector3.Distance(camPos, m_LastCamPos) / dt
                : 0f;
            m_LastCamPos = camPos;
            m_Initialized = true;

            // Altitude: camera Y is a reasonable proxy in CS2's isometric camera model.
            // Using exponential moving average to avoid jitter.
            float rawAlt = camPos.y;
            m_SmoothedAlt   = Mathf.Lerp(m_SmoothedAlt,   rawAlt, k_SmoothFactor);
            m_SmoothedSpeed = Mathf.Lerp(m_SmoothedSpeed, speed,   k_SmoothFactor);

            FastTrackRuntime.CameraAltitude = m_SmoothedAlt;

            // Classify camera state
            // Moving fast → treat as Overview minimum (avoid aggressive quality drop during pan)
            bool moving = m_SmoothedSpeed > k_MovingThreshold;

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
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            m_MainCamera = null;
            m_Initialized = false;
        }

        protected override void OnDestroy()
        {
            m_MainCamera = null;
            base.OnDestroy();
        }
    }
}
