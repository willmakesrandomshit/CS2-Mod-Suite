using System;
using Colossal.UI.Binding;
using Game.Common;
using Game.Objects;
using Game.Rendering;
using Game.UI;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace AutoDirector
{
    // Moves only the camera. Picks a moving car, follows/orbits it for a few seconds,
    // then cuts to another. Never writes to any entity, so it cannot affect the city or saves.
    public partial class AutoDirectorSystem : UISystemBase
    {
        private CameraUpdateSystem m_Camera;
        private EntityQuery m_CarQuery;
        private ValueBinding<bool> m_Active;
        private ValueBinding<string> m_ShotLabel;

        private bool m_Running;
        private Entity m_Target = Entity.Null;
        private ShotPlan m_Plan;
        private float m_Elapsed;
        private float m_BaseYaw, m_BaseZoom, m_BasePitch;
        private int m_ShotCounter;
        private int m_Seed = Environment.TickCount;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Camera = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
            m_CarQuery = GetEntityQuery(
                ComponentType.ReadOnly<Car>(), ComponentType.ReadOnly<Moving>(), ComponentType.ReadOnly<Game.Objects.Transform>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Game.Tools.Temp>());
            AddBinding(m_Active = new ValueBinding<bool>("autoDirector", "active", false));
            AddBinding(m_ShotLabel = new ValueBinding<string>("autoDirector", "shot", ""));
            AddBinding(new TriggerBinding("autoDirector", "toggle", Toggle));
        }

        private void Toggle()
        {
            m_Running = !m_Running;
            m_Target = Entity.Null;
            m_Active.Update(m_Running);
            if (!m_Running) m_ShotLabel.Update("");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (!m_Running) return;
            try
            {
                var controller = m_Camera?.activeCameraController;
                if (controller == null) return;

                m_Elapsed += UnityEngine.Time.unscaledDeltaTime;

                bool targetValid = m_Target != Entity.Null && EntityManager.Exists(m_Target) && EntityManager.HasComponent<Game.Objects.Transform>(m_Target);
                if (!targetValid || m_Elapsed >= m_Plan.Duration)
                {
                    if (!StartNextShot(controller)) return;
                }

                float3 pos = EntityManager.GetComponentData<Game.Objects.Transform>(m_Target).m_Position;
                controller.pivot = new Vector3(pos.x, pos.y, pos.z);

                Vector3 rot = controller.rotation;
                controller.rotation = new Vector3(m_BasePitch, DirectorLogic.YawAt(m_Plan, m_BaseYaw, m_Elapsed), rot.z);
                controller.zoom = DirectorLogic.ZoomAt(m_Plan, m_BaseZoom, m_Elapsed);
            }
            catch (Exception ex)
            {
                Mod.Log.Warn("Auto Director stopped after an error: " + ex.Message);
                m_Running = false;
                m_Active.Update(false);
                m_ShotLabel.Update("");
            }
        }

        private bool StartNextShot(IGameCameraController controller)
        {
            using (var cars = m_CarQuery.ToEntityArray(Allocator.Temp))
            {
                if (cars.Length == 0)
                {
                    m_ShotLabel.Update("Waiting for traffic...");
                    m_Elapsed = 0f;
                    m_Target = Entity.Null;
                    return false;
                }
                m_Seed = unchecked(m_Seed * 1664525 + 1013904223);
                m_Target = cars[(int)(((uint)m_Seed >> 4) % (uint)cars.Length)];
            }
            m_Plan = DirectorLogic.Plan(m_Seed + m_ShotCounter++, 6f, 12f);
            m_Elapsed = 0f;
            Vector3 rot = controller.rotation;
            m_BasePitch = rot.x;
            m_BaseYaw = rot.y;
            m_BaseZoom = Mathf.Max(5f, controller.zoom);
            m_ShotLabel.Update(m_Plan.Kind.ToString());
            return true;
        }
    }
}
