using System;
using Colossal.Logging;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace NetworkStudio
{
    public partial class NetworkStudioToolSystem : ToolBaseSystem
    {
        public static readonly ILog log = Mod.log;
        public static NetworkStudioToolSystem Instance { get; private set; }

        public override string toolID => "NetworkStudioTool";

        private DefaultToolSystem m_DefaultTool;
        private NetworkStudioSystem m_NetworkStudioSystem;
        private NetworkStudioUISystem m_UISystem;

        public Entity HoveredEdge { get; private set; } = Entity.Null;
        public float3 CursorWorldPos { get; private set; } = float3.zero;

        public override PrefabBase GetPrefab() => null;
        public override bool TrySetPrefab(PrefabBase prefab) => false;

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            m_DefaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            m_NetworkStudioSystem = World.GetOrCreateSystemManaged<NetworkStudioSystem>();
            m_UISystem = World.GetOrCreateSystemManaged<NetworkStudioUISystem>();

            log.Info("[NetworkStudioToolSystem] OnCreate executed.");
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            applyAction.shouldBeEnabled = true;
            secondaryApplyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
            log.Info("[NetworkStudioToolSystem] Tool started.");
        }

        protected override void OnStopRunning()
        {
            applyAction.shouldBeEnabled = false;
            secondaryApplyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;
            HoveredEdge = Entity.Null;
            log.Info("[NetworkStudioToolSystem] Tool stopped.");
            base.OnStopRunning();
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            if (m_ToolRaycastSystem != null)
            {
                m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.Terrain;
                m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.PublicTransportRoad;
                m_ToolRaycastSystem.raycastFlags = RaycastFlags.SubElements | RaycastFlags.Markers;
                m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground;
            }
        }

        public void ActivateTool()
        {
            if (m_ToolSystem != null && m_ToolSystem.activeTool != this)
            {
                m_ToolSystem.activeTool = this;
            }
        }

        public void DeactivateTool()
        {
            if (m_ToolSystem != null && m_ToolSystem.activeTool == this)
            {
                m_ToolSystem.activeTool = m_DefaultTool;
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_NetworkStudioSystem != null && m_NetworkStudioSystem.SelectedEdge != Entity.Null)
            {
                if (!EntityManager.Exists(m_NetworkStudioSystem.SelectedEdge) || EntityManager.HasComponent<Deleted>(m_NetworkStudioSystem.SelectedEdge))
                {
                    m_NetworkStudioSystem.ClearSelection();
                }
            }

            RaycastHit hit;
            bool hitSomething = GetRaycastResult(out Entity hitEntity, out hit);
            CursorWorldPos = hitSomething ? hit.m_HitPosition : float3.zero;

            Entity targetEdge = Entity.Null;
            if (hitSomething && hitEntity != Entity.Null && EntityManager.Exists(hitEntity))
            {
                targetEdge = hitEntity;
                if (EntityManager.HasComponent<Owner>(hitEntity))
                {
                    var owner = EntityManager.GetComponentData<Owner>(hitEntity).m_Owner;
                    if (EntityManager.HasComponent<Edge>(owner))
                        targetEdge = owner;
                }
            }
            HoveredEdge = targetEdge;

            // Left Click (Select road)
            if (applyAction != null && applyAction.WasPressedThisFrame())
            {
                if (targetEdge != Entity.Null && EntityManager.HasComponent<Edge>(targetEdge))
                {
                    m_NetworkStudioSystem?.SelectEdge(targetEdge);
                }
                else
                {
                    m_NetworkStudioSystem?.ClearSelection();
                }
            }

            // Right Click (Step down / Cancel)
            if (secondaryApplyAction != null && secondaryApplyAction.WasPressedThisFrame())
            {
                if (m_NetworkStudioSystem != null && m_NetworkStudioSystem.SelectedEdge != Entity.Null)
                {
                    m_NetworkStudioSystem.ClearSelection();
                }
                else
                {
                    DeactivateTool();
                }
            }

            // Esc key
            if (cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                if (m_NetworkStudioSystem != null && m_NetworkStudioSystem.SelectedEdge != Entity.Null)
                {
                    m_NetworkStudioSystem.ClearSelection();
                }
                else
                {
                    DeactivateTool();
                }
            }

            return inputDeps;
        }
    }
}
