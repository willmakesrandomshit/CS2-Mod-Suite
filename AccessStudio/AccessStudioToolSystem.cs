using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace AccessStudio
{
    public enum AccessStudioToolMode
    {
        SelectBuilding,
        SelectServiceRoad,
        SelectRemoteServicePoint
    }

    public sealed partial class AccessStudioToolSystem : ToolBaseSystem
    {
        private DefaultToolSystem m_DefaultTool;
        private AccessStudioProbeSystem m_Probe;
        private AccessStudioPocSystem m_Poc;

        public override string toolID => "AccessStudioProbeTool";
        public bool IsActive => m_ToolSystem != null && m_ToolSystem.activeTool == this;
        public Entity HoveredEntity { get; private set; } = Entity.Null;
        public float3 CursorPosition { get; private set; }
        public AccessStudioToolMode Mode { get; private set; } = AccessStudioToolMode.SelectBuilding;

        public override PrefabBase GetPrefab() => null;
        public override bool TrySetPrefab(PrefabBase prefab) => false;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_DefaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            m_Probe = World.GetOrCreateSystemManaged<AccessStudioProbeSystem>();
            m_Poc = World.GetOrCreateSystemManaged<AccessStudioPocSystem>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            applyAction.shouldBeEnabled = true;
            secondaryApplyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
            Mod.Log.Info($"[AccessStudio] Tool active in {Mode} mode.");
        }

        protected override void OnStopRunning()
        {
            applyAction.shouldBeEnabled = false;
            secondaryApplyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;
            HoveredEntity = Entity.Null;
            base.OnStopRunning();
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            ConfigureRaycast();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var hit = GetRaycastResult(out var rawEntity, out RaycastHit raycastHit);
            HoveredEntity = hit ? rawEntity : Entity.Null;
            CursorPosition = hit ? raycastHit.m_HitPosition : float3.zero;

            if (applyAction.WasPressedThisFrame())
            {
                if (Mode == AccessStudioToolMode.SelectServiceRoad || Mode == AccessStudioToolMode.SelectRemoteServicePoint)
                {
                    if (!hit)
                    {
                        Mod.Log.Info("[AccessStudio] Road B selection click did not hit a network entity.");
                    }
                    else
                    {
                        var road = ResolvePersistentRoadEdge(rawEntity, out var trace);
                        if (road == Entity.Null)
                        {
                            Mod.Log.Warn($"[AccessStudio] Road B selection rejected: {trace}");
                        }
                        else if ((Mode == AccessStudioToolMode.SelectRemoteServicePoint
                                  ? m_Poc.QueueRemote(m_Probe.SelectedBuilding, road, CursorPosition, out var reason)
                                  : m_Poc.QueueMove(m_Probe.SelectedBuilding, road, CursorPosition, out reason)))
                        {
                            Mod.Log.Info($"[AccessStudio] {(Mode == AccessStudioToolMode.SelectRemoteServicePoint ? "Road C remote point" : "Road B")} selected {FormatEntity(road)} | resolve={trace}");
                            Mode = AccessStudioToolMode.SelectBuilding;
                            ConfigureRaycast();
                        }
                        else
                        {
                            Mod.Log.Warn($"[AccessStudio] Road B selection rejected: {reason} | resolve={trace}");
                        }
                    }
                }
                else if (hit)
                {
                    m_Probe.SelectFromRaycast(rawEntity, CursorPosition);
                }
                else
                {
                    Mod.Log.Info("[AccessStudio] Probe click did not hit a static object.");
                }
            }

            if (secondaryApplyAction.WasPressedThisFrame() || cancelAction.WasPressedThisFrame())
            {
                if (Mode == AccessStudioToolMode.SelectServiceRoad || Mode == AccessStudioToolMode.SelectRemoteServicePoint)
                {
                    CancelServiceRoadSelection();
                }
                else if (m_Probe.SelectedBuilding != Entity.Null) m_Probe.ClearSelection();
                else ExitTool();
            }
            return inputDeps;
        }

        public void Activate()
        {
            Mode = AccessStudioToolMode.SelectBuilding;
            ConfigureRaycast();
            if (m_ToolSystem != null) m_ToolSystem.activeTool = this;
        }

        public bool BeginServiceRoadSelection()
        {
            if (m_Probe == null || m_Probe.SelectedBuilding == Entity.Null)
                return false;
            Mode = AccessStudioToolMode.SelectServiceRoad;
            ConfigureRaycast();
            if (m_ToolSystem != null) m_ToolSystem.activeTool = this;
            Mod.Log.Info($"[AccessStudio] MOVE SERVICE ACCESS armed for {FormatEntity(m_Probe.SelectedBuilding)}. Click a different vehicle road.");
            return true;
        }

        public bool BeginRemoteServicePointSelection()
        {
            if (m_Probe == null || m_Probe.SelectedBuilding == Entity.Null) return false;
            Mode = AccessStudioToolMode.SelectRemoteServicePoint;
            ConfigureRaycast();
            if (m_ToolSystem != null) m_ToolSystem.activeTool = this;
            Mod.Log.Info($"[AccessStudio] EXPERIMENTAL REMOTE SERVICE POINT armed for {FormatEntity(m_Probe.SelectedBuilding)}. Click a nearby car road (maximum 50 m). ");
            return true;
        }

        public void CancelServiceRoadSelection()
        {
            Mode = AccessStudioToolMode.SelectBuilding;
            ConfigureRaycast();
            Mod.Log.Info("[AccessStudio] Road B selection cancelled; returning to building selection.");
        }

        public void ExitTool()
        {
            Mode = AccessStudioToolMode.SelectBuilding;
            if (m_ToolSystem != null && m_DefaultTool != null) m_ToolSystem.activeTool = m_DefaultTool;
        }

        private void ConfigureRaycast()
        {
            if (m_ToolRaycastSystem == null) return;
            if (Mode == AccessStudioToolMode.SelectServiceRoad || Mode == AccessStudioToolMode.SelectRemoteServicePoint)
            {
                m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.Terrain;
                m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.PublicTransportRoad;
                m_ToolRaycastSystem.raycastFlags = RaycastFlags.SubElements | RaycastFlags.Markers;
                m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
            }
            else
            {
                m_ToolRaycastSystem.typeMask = TypeMask.StaticObjects;
                m_ToolRaycastSystem.raycastFlags = RaycastFlags.BuildingLots | RaycastFlags.SubElements;
                m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground;
            }
        }

        private Entity ResolvePersistentRoadEdge(Entity rawEntity, out string trace)
        {
            var current = rawEntity;
            trace = FormatEntity(rawEntity);
            for (var depth = 0; depth < 12; depth++)
            {
                if (current == Entity.Null || !EntityManager.Exists(current)) return Entity.Null;
                if (EntityManager.HasComponent<Temp>(current))
                {
                    var temp = EntityManager.GetComponentData<Temp>(current);
                    if (temp.m_Original == Entity.Null || !EntityManager.Exists(temp.m_Original)) return Entity.Null;
                    current = temp.m_Original;
                    trace += " -> Temp.Original " + FormatEntity(current);
                    continue;
                }
                if (EntityManager.HasComponent<Game.Net.Edge>(current) &&
                    EntityManager.HasComponent<Game.Net.Road>(current) &&
                    !EntityManager.HasComponent<Deleted>(current))
                    return current;
                if (!EntityManager.HasComponent<Owner>(current)) return Entity.Null;
                var owner = EntityManager.GetComponentData<Owner>(current).m_Owner;
                if (owner == Entity.Null || owner == current) return Entity.Null;
                current = owner;
                trace += " -> Owner " + FormatEntity(current);
            }
            return Entity.Null;
        }

        private static string FormatEntity(Entity entity) => entity == Entity.Null ? "Null" : $"#{entity.Index}:{entity.Version}";
    }
}
