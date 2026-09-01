using System;
using System.Collections.Generic;
using System.Text;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Building = Game.Buildings.Building;
using ObjectTransform = Game.Objects.Transform;
using SpawnLocation = Game.Objects.SpawnLocation;

namespace AccessStudio
{
    public sealed partial class AccessStudioProbeSystem : GameSystemBase
    {
        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_GarbageRequestQuery;
        private EntityQuery m_GarbageVehicleQuery;

        public Entity SelectedBuilding { get; private set; } = Entity.Null;
        public string Status { get; private set; } = "Activate the probe and click a building.";
        public string DiagnosticText { get; private set; } = "No building selected.";
        public string LastResolveTrace { get; private set; } = "None";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_GarbageRequestQuery = GetEntityQuery(
                ComponentType.ReadOnly<GarbageCollectionRequest>(),
                ComponentType.Exclude<Deleted>());
            m_GarbageVehicleQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Vehicles.GarbageTruck>(),
                ComponentType.ReadOnly<Game.Common.Target>(),
                ComponentType.ReadOnly<ServiceDispatch>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());
        }

        protected override void OnUpdate()
        {
            if (SelectedBuilding != Entity.Null &&
                (!EntityManager.Exists(SelectedBuilding) || EntityManager.HasComponent<Deleted>(SelectedBuilding)))
            {
                Mod.Log.Warn("[AccessStudio] Selected building was deleted; clearing probe selection.");
                ClearSelection();
            }
        }

        public bool SelectFromRaycast(Entity rawEntity, float3 hitPosition)
        {
            var building = ResolvePersistentBuilding(rawEntity, out var trace);
            LastResolveTrace = trace;
            if (building == Entity.Null)
            {
                Status = "That object did not resolve to a persistent building.";
                DiagnosticText = $"Raw hit: {FormatEntity(rawEntity)}\nHit position: {hitPosition}\nResolve: {trace}";
                Mod.Log.Info($"[AccessStudio] Probe selection failed. {DiagnosticText.Replace("\n", " | ")}");
                return false;
            }

            SelectedBuilding = building;
            RefreshSnapshot(hitPosition);
            return true;
        }

        public void RefreshSnapshot(float3? hitPosition = null)
        {
            if (SelectedBuilding == Entity.Null || !EntityManager.Exists(SelectedBuilding))
            {
                ClearSelection();
                return;
            }

            var sb = new StringBuilder(4096);
            var buildingEntity = SelectedBuilding;
            var building = EntityManager.GetComponentData<Building>(buildingEntity);
            sb.AppendLine($"Building entity: {FormatEntity(buildingEntity)}");
            if (hitPosition.HasValue) sb.AppendLine($"Clicked at: {FormatFloat3(hitPosition.Value)}");
            sb.AppendLine($"Resolve trace: {LastResolveTrace}");

            if (TryGetComponent(buildingEntity, out PrefabRef prefabRef))
            {
                var prefabName = FormatEntity(prefabRef.m_Prefab);
                if (m_PrefabSystem != null && m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef.m_Prefab, out var prefab))
                    prefabName = prefab.name;
                sb.AppendLine($"Prefab: {prefabName} ({FormatEntity(prefabRef.m_Prefab)})");
            }

            if (TryGetComponent(buildingEntity, out ObjectTransform transform))
            {
                sb.AppendLine($"Position: {FormatFloat3(transform.m_Position)}");
                sb.AppendLine($"Rotation: {transform.m_Rotation.value}");
            }

            sb.AppendLine($"Front road edge: {FormatEntity(building.m_RoadEdge)}");
            sb.AppendLine($"Front curve position: {building.m_CurvePosition:0.0000}");
            AppendRoadDetails(sb, "Front road", building.m_RoadEdge);

            if (TryGetComponent(buildingEntity, out BackSide backSide))
            {
                sb.AppendLine($"BackSide road edge: {FormatEntity(backSide.m_RoadEdge)}");
                sb.AppendLine($"BackSide curve position: {backSide.m_CurvePosition:0.0000}");
                AppendRoadDetails(sb, "Back road", backSide.m_RoadEdge);
            }
            else
            {
                sb.AppendLine("BackSide: absent");
            }

            AppendOwner(sb, buildingEntity);
            AppendSpawnLocations(sb, buildingEntity);
            AppendConnectionLanes(sb, buildingEntity);
            AppendGarbageState(sb, buildingEntity);

            DiagnosticText = sb.ToString().TrimEnd();
            Status = "Read-only snapshot captured. Check AccessStudio.log for the same evidence.";
            Mod.Log.Info("[AccessStudio] SNAPSHOT BEGIN\n" + DiagnosticText + "\n[AccessStudio] SNAPSHOT END");
        }

        public void ClearSelection()
        {
            SelectedBuilding = Entity.Null;
            Status = "Activate the probe and click a building.";
            DiagnosticText = "No building selected.";
            LastResolveTrace = "None";
        }

        public Entity ResolvePersistentBuilding(Entity rawEntity, out string trace)
        {
            var steps = new List<string>();
            var current = rawEntity;
            for (var depth = 0; depth < 16; depth++)
            {
                if (current == Entity.Null || !EntityManager.Exists(current))
                {
                    steps.Add($"invalid at depth {depth}");
                    break;
                }

                steps.Add(FormatEntity(current));
                if (EntityManager.HasComponent<Temp>(current))
                {
                    var temp = EntityManager.GetComponentData<Temp>(current);
                    if (temp.m_Original != Entity.Null && EntityManager.Exists(temp.m_Original))
                    {
                        current = temp.m_Original;
                        steps.Add("Temp->Original");
                        continue;
                    }
                }

                if (EntityManager.HasComponent<Building>(current) && !EntityManager.HasComponent<Deleted>(current))
                {
                    trace = string.Join(" -> ", steps);
                    return current;
                }

                if (TryGetComponent(current, out Owner owner) &&
                    owner.m_Owner != Entity.Null && owner.m_Owner != current)
                {
                    current = owner.m_Owner;
                    steps.Add("Owner");
                    continue;
                }
                break;
            }

            trace = string.Join(" -> ", steps);
            return Entity.Null;
        }

        private void AppendRoadDetails(StringBuilder sb, string label, Entity road)
        {
            if (road == Entity.Null || !EntityManager.Exists(road))
            {
                sb.AppendLine($"{label}: unavailable");
                return;
            }
            if (TryGetComponent(road, out Curve curve))
                sb.AppendLine($"{label} curve: a={FormatFloat3(curve.m_Bezier.a)} d={FormatFloat3(curve.m_Bezier.d)} length={curve.m_Length:0.00}");
            if (EntityManager.HasComponent<Deleted>(road)) sb.AppendLine($"{label}: DELETED");
            if (EntityManager.HasBuffer<ConnectedBuilding>(road))
                sb.AppendLine($"{label} connected-building count: {EntityManager.GetBuffer<ConnectedBuilding>(road, true).Length}");
        }

        private void AppendOwner(StringBuilder sb, Entity entity)
        {
            if (TryGetComponent(entity, out Owner owner))
                sb.AppendLine($"Building owner: {FormatEntity(owner.m_Owner)}");
            else
                sb.AppendLine("Building owner: none");
        }

        private void AppendSpawnLocations(StringBuilder sb, Entity building)
        {
            var count = 0;
            if (EntityManager.HasBuffer<SpawnLocationElement>(building))
            {
                var elements = EntityManager.GetBuffer<SpawnLocationElement>(building, true);
                sb.AppendLine($"SpawnLocationElement count: {elements.Length}");
                for (var i = 0; i < elements.Length && i < 64; i++)
                {
                    var item = elements[i];
                    var spawn = item.m_SpawnLocation;
                    sb.Append($"  [{i}] type={item.m_Type} entity={FormatEntity(spawn)}");
                    if (spawn != Entity.Null && EntityManager.Exists(spawn) && TryGetComponent(spawn, out SpawnLocation location))
                    {
                        sb.Append($" lane1={FormatEntity(location.m_ConnectedLane1)}@{location.m_CurvePosition1:0.000}");
                        sb.Append($" lane2={FormatEntity(location.m_ConnectedLane2)}@{location.m_CurvePosition2:0.000}");
                        sb.Append($" group={location.m_GroupIndex} flags={location.m_Flags} restriction={FormatEntity(location.m_AccessRestriction)}");
                        sb.Append($" edge1={FormatEntity(ResolveOwningEdge(location.m_ConnectedLane1))} edge2={FormatEntity(ResolveOwningEdge(location.m_ConnectedLane2))}");
                        if (TryGetComponent(spawn, out Owner owner)) sb.Append($" owner={FormatEntity(owner.m_Owner)}");
                        if (TryGetComponent(spawn, out ObjectTransform spawnTransform))
                        {
                            sb.Append($" world={FormatFloat3(spawnTransform.m_Position)}");
                            if (TryGetComponent(building, out ObjectTransform buildingTransform))
                            {
                                var local = math.mul(math.inverse(buildingTransform.m_Rotation), spawnTransform.m_Position - buildingTransform.m_Position);
                                sb.Append($" local={FormatFloat3(local)}");
                            }
                        }
                        if (TryGetComponent(spawn, out PrefabRef spawnPrefab) && TryGetComponent(spawnPrefab.m_Prefab, out SpawnLocationData data))
                        {
                            var role = data.m_ActivityMask.m_Mask != 0 ? "activity/decorative" :
                                ((data.m_RoadTypes & RoadTypes.Car) != 0 &&
                                 (data.m_ConnectionType == RouteConnectionType.Road || data.m_ConnectionType == RouteConnectionType.Cargo))
                                    ? "vehicle/service-candidate" :
                                data.m_ConnectionType == RouteConnectionType.Pedestrian ? "pedestrian" : "other";
                            sb.Append($" connection={data.m_ConnectionType} roadTypes={data.m_RoadTypes} activity=0x{data.m_ActivityMask.m_Mask:X} role={role}");
                        }
                        count++;
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("SpawnLocationElement buffer: absent");
            }
            sb.AppendLine($"Resolved SpawnLocation components: {count}");
        }

        private Entity ResolveOwningEdge(Entity entity)
        {
            var current = entity;
            for (var depth = 0; depth < 12; depth++)
            {
                if (current == Entity.Null || !EntityManager.Exists(current)) return Entity.Null;
                if (EntityManager.HasComponent<Game.Net.Edge>(current)) return current;
                if (!TryGetComponent(current, out Owner owner) || owner.m_Owner == current) return Entity.Null;
                current = owner.m_Owner;
            }
            return Entity.Null;
        }

        private void AppendConnectionLanes(StringBuilder sb, Entity building)
        {
            var road = 0;
            var pedestrian = 0;
            var total = 0;
            if (EntityManager.HasBuffer<Game.Net.SubLane>(building))
            {
                var lanes = EntityManager.GetBuffer<Game.Net.SubLane>(building, true);
                sb.AppendLine($"Building SubLane count: {lanes.Length}");
                for (var i = 0; i < lanes.Length && i < 128; i++)
                {
                    var laneEntity = lanes[i].m_SubLane;
                    if (!TryGetComponent(laneEntity, out Game.Net.ConnectionLane lane)) continue;
                    total++;
                    if ((lane.m_Flags & ConnectionLaneFlags.Road) != 0) road++;
                    if ((lane.m_Flags & ConnectionLaneFlags.Pedestrian) != 0) pedestrian++;
                    sb.AppendLine($"  connection {FormatEntity(laneEntity)} flags={lane.m_Flags} roadTypes={lane.m_RoadTypes} trackTypes={lane.m_TrackTypes} restriction={FormatEntity(lane.m_AccessRestriction)}");
                }
            }
            else
            {
                sb.AppendLine("Building SubLane buffer: absent");
            }
            sb.AppendLine($"Connection lanes: total={total}, road={road}, pedestrian={pedestrian}");
        }

        private void AppendGarbageState(StringBuilder sb, Entity building)
        {
            var matchingRequests = new HashSet<Entity>();
            using (var requestEntities = m_GarbageRequestQuery.ToEntityArray(Allocator.Temp))
            using (var requests = m_GarbageRequestQuery.ToComponentDataArray<GarbageCollectionRequest>(Allocator.Temp))
            {
                for (var i = 0; i < requests.Length; i++)
                {
                    if (requests[i].m_Target != building) continue;
                    matchingRequests.Add(requestEntities[i]);
                    sb.AppendLine($"Garbage request: {FormatEntity(requestEntities[i])} target={FormatEntity(requests[i].m_Target)} priority={requests[i].m_Priority} flags={requests[i].m_Flags} dispatchIndex={requests[i].m_DispatchIndex}");
                }
            }
            sb.AppendLine($"Matching garbage requests: {matchingRequests.Count}");

            var assignedVehicles = 0;
            using (var vehicleEntities = m_GarbageVehicleQuery.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < vehicleEntities.Length; i++)
                {
                    var vehicle = vehicleEntities[i];
                    var dispatches = EntityManager.GetBuffer<ServiceDispatch>(vehicle, true);
                    for (var j = 0; j < dispatches.Length; j++)
                    {
                        if (!matchingRequests.Contains(dispatches[j].m_Request)) continue;
                        assignedVehicles++;
                        var target = EntityManager.GetComponentData<Game.Common.Target>(vehicle);
                        var pathSummary = "no PathOwner";
                        if (TryGetComponent(vehicle, out PathOwner pathOwner))
                            pathSummary = $"pathFlags={pathOwner.m_State} elementIndex={pathOwner.m_ElementIndex}";
                        sb.AppendLine($"Assigned garbage truck: {FormatEntity(vehicle)} request={FormatEntity(dispatches[j].m_Request)} target={FormatEntity(target.m_Target)} {pathSummary}");
                    }
                }
            }
            sb.AppendLine($"Assigned garbage trucks: {assignedVehicles}");
        }

        private static string FormatEntity(Entity entity) => entity == Entity.Null ? "Null" : $"#{entity.Index}:{entity.Version}";
        private static string FormatFloat3(float3 value) => $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";

        private bool TryGetComponent<T>(Entity entity, out T component)
            where T : unmanaged, IComponentData
        {
            if (entity != Entity.Null && EntityManager.Exists(entity) && EntityManager.HasComponent<T>(entity))
            {
                component = EntityManager.GetComponentData<T>(entity);
                return true;
            }

            component = default;
            return false;
        }
    }
}
