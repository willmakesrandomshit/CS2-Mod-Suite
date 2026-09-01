using Colossal.Mathematics;
using Game;
using Game.Objects;
using Game.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ObjectTransform = Game.Objects.Transform;
using UnityColor = UnityEngine.Color;

namespace AccessStudio
{
    /// <summary>Development visualization only; it does not create simulation connectivity.</summary>
    public sealed partial class AccessStudioOverlaySystem : GameSystemBase
    {
        private static readonly UnityColor PointColor = new UnityColor(0.20f, 0.85f, 1f, 0.95f);
        private static readonly UnityColor PointOutline = new UnityColor(0.02f, 0.15f, 0.20f, 1f);
        private static readonly UnityColor GuideColor = new UnityColor(0.20f, 0.85f, 1f, 0.55f);
        private OverlayRenderSystem m_Overlay;
        private AccessStudioPocSystem m_Poc;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Overlay = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_Poc = World.GetOrCreateSystemManaged<AccessStudioPocSystem>();
        }

        protected override void OnUpdate()
        {
            if (m_Poc == null || !m_Poc.HasRemoteOverride || m_Overlay == null) return;
            var point = m_Poc.RemoteWorldPosition + new float3(0f, 0.18f, 0f);
            var buffer = m_Overlay.GetBuffer(out var dependencies);
            dependencies.Complete();
            buffer.DrawCircle(
                outlineColor: PointOutline,
                fillColor: PointColor,
                outlineWidth: 0.22f,
                styleFlags: OverlayRenderSystem.StyleFlags.Projected,
                direction: new float2(0f, 1f),
                position: point,
                diameter: 4.5f);

            if (m_Poc.ActiveBuilding != Entity.Null && EntityManager.Exists(m_Poc.ActiveBuilding) &&
                EntityManager.HasComponent<ObjectTransform>(m_Poc.ActiveBuilding))
            {
                var buildingPoint = EntityManager.GetComponentData<ObjectTransform>(m_Poc.ActiveBuilding).m_Position + new float3(0f, 0.2f, 0f);
                buffer.DrawLine(GuideColor, new Line3.Segment(buildingPoint, point), 0.16f, true);
            }
        }
    }
}
