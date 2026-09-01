using System;
using System.Collections.Generic;
using System.Text;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Building = Game.Buildings.Building;
using ObjectTransform = Game.Objects.Transform;
using SpawnLocation = Game.Objects.SpawnLocation;

namespace AccessStudio
{
    /// <summary>
    /// Development-only, reversible service-access proof of concept.
    /// This deliberately does not persist an Access Studio override and does not reapply one
    /// after vanilla changes it. Runtime evidence must establish the correct behavior first.
    /// </summary>
    public sealed partial class AccessStudioPocSystem : GameSystemBase
    {
        private const float MaximumRoadDistance = 120f;
        private const float MaximumRemoteDistance = 50f;
        private const float RemoteReachDistance = 12f;
        private const int MonitorIntervalFrames = 30;

        private sealed class SpawnSnapshot
        {
            public Entity Entity;
            public SpawnLocation Value;
            public bool HadTransform;
            public ObjectTransform Transform;
        }

        private enum OverrideMode
        {
            RoadReassignment,
            RemoteServicePoint
        }

        private sealed class OverrideRecord
        {
            public Entity Building;
            public OverrideMode Mode;
            public Building OriginalBuilding;
            public bool HadBackSide;
            public BackSide OriginalBackSide;
            public readonly List<SpawnSnapshot> OriginalSpawnLocations = new List<SpawnSnapshot>();
            public Entity CustomRoad;
            public float CustomCurvePosition;
            public float3 CustomWorldPosition;
            public int AppliedFrame;
            public bool Reverted;
            public bool ObservedPathToRoadB;
            public bool ObservedTruckOnRoadB;
            public bool ObservedTruckOnRoadA;
            public bool ObservedTruckReachedPoint;
            public bool AddedRemoteRoadMembership;
            public readonly List<Entity> RemoteSpawnLocations = new List<Entity>();
            public bool HadGarbageRequest;
            public int InitialGarbage = -1;
            public int LastGarbage = -1;
            public string LastTelemetrySignature = string.Empty;
        }

        private enum PendingKind
        {
            None,
            Move,
            Remote,
            Reset
        }

        private struct PendingOperation
        {
            public PendingKind Kind;
            public Entity Building;
            public Entity Road;
            public float3 HitPosition;
        }

        private readonly Dictionary<Entity, OverrideRecord> m_Overrides = new Dictionary<Entity, OverrideRecord>();
        private EntityQuery m_GarbageRequestQuery;
        private EntityQuery m_GarbageVehicleQuery;
        private PrefabSystem m_PrefabSystem;
        private AccessStudioProbeSystem m_Probe;
        private PendingOperation m_Pending;
        private int m_Frame;

        public string State { get; private set; } = "NO OVERRIDE";
        public string Status { get; private set; } = "Select a building, then choose Move Service Access.";
        public string Telemetry { get; private set; } = "No service-access POC is active.";
        public bool HasActiveOverride { get; private set; }
        public Entity ActiveBuilding { get; private set; } = Entity.Null;
        public Entity OriginalRoad { get; private set; } = Entity.Null;
        public Entity CustomRoad { get; private set; } = Entity.Null;
        public bool HasRemoteOverride => ActiveBuilding != Entity.Null && m_Overrides.TryGetValue(ActiveBuilding, out var record) && record.Mode == OverrideMode.RemoteServicePoint;
        public float3 RemoteWorldPosition => HasRemoteOverride ? m_Overrides[ActiveBuilding].CustomWorldPosition : float3.zero;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_Probe = World.GetOrCreateSystemManaged<AccessStudioProbeSystem>();
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

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            // Overrides modify vanilla Building/SpawnLocation components directly. Restore
            // them before a world transition so a transient experiment cannot leak into the
            // next city or be left behind during a reload.
            RestoreAllOverrides("world preload");
            m_Pending = default;
            base.OnGamePreload(purpose, mode);
        }

        protected override void OnUpdate()
        {
            m_Frame++;
            if (m_Pending.Kind != PendingKind.None)
            {
                var operation = m_Pending;
                m_Pending = default;
                if (operation.Kind == PendingKind.Move)
                    ApplyMove(operation.Building, operation.Road, operation.HitPosition);
                else if (operation.Kind == PendingKind.Remote)
                    ApplyRemote(operation.Building, operation.Road, operation.HitPosition);
                else if (operation.Kind == PendingKind.Reset)
                    ApplyReset(operation.Building);
            }

            if (m_Frame % MonitorIntervalFrames != 0 || m_Overrides.Count == 0) return;

            var removed = new List<Entity>();
            foreach (var pair in m_Overrides)
            {
                if (!EntityManager.Exists(pair.Key) || EntityManager.HasComponent<Deleted>(pair.Key))
                {
                    Mod.Log.Warn($"[AccessStudio] POC building {FormatEntity(pair.Key)} was deleted. Dropping the transient override record.");
                    removed.Add(pair.Key);
                    continue;
                }
                MonitorOverride(pair.Value);
            }
            for (var i = 0; i < removed.Count; i++) m_Overrides.Remove(removed[i]);

            if (ActiveBuilding != Entity.Null && !m_Overrides.ContainsKey(ActiveBuilding))
                PublishNoOverride();
        }

        public bool QueueMove(Entity building, Entity road, float3 hitPosition, out string reason)
        {
            if (!ValidateMove(building, road, hitPosition, out reason))
            {
                State = "TARGET REJECTED";
                Status = reason;
                return false;
            }

            m_Pending = new PendingOperation
            {
                Kind = PendingKind.Move,
                Building = building,
                Road = road,
                HitPosition = hitPosition
            };
            State = "APPLY PENDING";
            Status = $"Queued Road B {FormatEntity(road)} for the next simulation update.";
            return true;
        }

        public bool QueueReset(Entity building)
        {
            if (building == Entity.Null || !m_Overrides.ContainsKey(building))
            {
                State = "NO OVERRIDE";
                Status = "The selected building has no transient POC override.";
                return false;
            }
            m_Pending = new PendingOperation { Kind = PendingKind.Reset, Building = building };
            State = "RESET PENDING";
            Status = "Vanilla restoration queued for the next simulation update.";
            return true;
        }

        public bool HasOverride(Entity building) => building != Entity.Null && m_Overrides.ContainsKey(building);

        public bool QueueRemote(Entity building, Entity road, float3 hitPosition, out string reason)
        {
            if (HasOverride(building))
            {
                reason = "Reset the current override before placing a remote service point.";
                State = "RESET REQUIRED";
                Status = reason;
                return false;
            }
            if (!ValidateMove(building, road, hitPosition, out reason, true))
            {
                State = "REMOTE TARGET REJECTED";
                Status = reason;
                return false;
            }
            m_Pending = new PendingOperation { Kind = PendingKind.Remote, Building = building, Road = road, HitPosition = hitPosition };
            State = "REMOTE APPLY PENDING";
            Status = $"Queued experimental Road C point on {FormatEntity(road)}.";
            return true;
        }

        private bool ValidateMove(Entity building, Entity road, float3 hitPosition, out string reason, bool remote = false)
        {
            if (building == Entity.Null || !EntityManager.Exists(building) ||
                !EntityManager.HasComponent<Building>(building) || EntityManager.HasComponent<Deleted>(building) ||
                EntityManager.HasComponent<Temp>(building))
            {
                reason = "Select a valid persistent building first.";
                return false;
            }
            if (road == Entity.Null || !EntityManager.Exists(road) ||
                !EntityManager.HasComponent<Game.Net.Edge>(road) || !EntityManager.HasComponent<Game.Net.Road>(road) ||
                !EntityManager.HasComponent<Game.Net.Curve>(road) || EntityManager.HasComponent<Deleted>(road) ||
                EntityManager.HasComponent<Temp>(road))
            {
                reason = "Target must be a persistent vehicle road edge.";
                return false;
            }
            if (!EntityManager.HasBuffer<Game.Net.SubLane>(road) || !HasCarLane(road))
            {
                reason = "Target road has no usable car lane.";
                return false;
            }

            var current = EntityManager.GetComponentData<Building>(building);
            if (current.m_RoadEdge == road)
            {
                reason = "Choose a different road from the building's current service road.";
                return false;
            }
            if (!EntityManager.HasBuffer<ConnectedBuilding>(road))
            {
                reason = "Target road lacks the vanilla ConnectedBuilding buffer.";
                return false;
            }
            if (!TryGetComponent(building, out ObjectTransform transform))
            {
                reason = "Selected building has no world transform.";
                return false;
            }
            var distance = math.distance(transform.m_Position, hitPosition);
            var limit = remote ? MaximumRemoteDistance : MaximumRoadDistance;
            if (distance > limit)
            {
                reason = $"Target is {distance:0.0} m away; the {(remote ? "remote POC" : "development")} limit is {limit:0} m.";
                return false;
            }

            reason = "Valid vehicle road.";
            return true;
        }

        private void ApplyMove(Entity buildingEntity, Entity roadEntity, float3 hitPosition)
        {
            try
            {
                if (!ValidateMove(buildingEntity, roadEntity, hitPosition, out var reason))
                {
                    State = "TARGET REJECTED";
                    Status = reason;
                    Mod.Log.Warn($"[AccessStudio] MOVE SERVICE ACCESS rejected at apply time: {reason}");
                    return;
                }

                if (!m_Overrides.TryGetValue(buildingEntity, out var record))
                {
                    var original = EntityManager.GetComponentData<Building>(buildingEntity);
                    record = new OverrideRecord
                    {
                        Building = buildingEntity,
                        Mode = OverrideMode.RoadReassignment,
                        OriginalBuilding = original,
                        HadBackSide = TryGetComponent(buildingEntity, out BackSide originalBackSide),
                        OriginalBackSide = originalBackSide,
                        InitialGarbage = TryGetComponent(buildingEntity, out GarbageProducer producer) ? producer.m_Garbage : -1,
                        LastGarbage = TryGetComponent(buildingEntity, out GarbageProducer producer2) ? producer2.m_Garbage : -1
                    };
                    CaptureSpawnLocations(record);
                    m_Overrides.Add(buildingEntity, record);
                }

                var building = EntityManager.GetComponentData<Building>(buildingEntity);
                var oldRoad = building.m_RoadEdge;
                var curve = EntityManager.GetComponentData<Game.Net.Curve>(roadEntity);
                MathUtils.Distance(curve.m_Bezier.xz, hitPosition.xz, out var curvePosition);
                curvePosition = math.saturate(curvePosition);

                UpdateConnectedBuildingMembership(oldRoad, roadEntity, buildingEntity);
                building.m_RoadEdge = roadEntity;
                building.m_CurvePosition = curvePosition;
                EntityManager.SetComponentData(buildingEntity, building);
                EmitRoadConnectionUpdated(buildingEntity, oldRoad, roadEntity);

                record.CustomRoad = roadEntity;
                record.CustomCurvePosition = curvePosition;
                record.CustomWorldPosition = MathUtils.Position(curve.m_Bezier, curvePosition);
                record.AppliedFrame = m_Frame;
                record.Reverted = false;
                record.ObservedPathToRoadB = false;
                record.ObservedTruckOnRoadB = false;
                record.ObservedTruckOnRoadA = false;
                record.HadGarbageRequest = false;
                record.LastTelemetrySignature = string.Empty;

                ActiveBuilding = buildingEntity;
                OriginalRoad = record.OriginalBuilding.m_RoadEdge;
                CustomRoad = roadEntity;
                HasActiveOverride = true;
                State = "CUSTOM — MONITORING";
                Status = $"Road A {FormatEntity(oldRoad)} → Road B {FormatEntity(roadEntity)} at t={curvePosition:0.000}. Waiting for vanilla regeneration and a newly routed service vehicle.";

                Mod.Log.Info(
                    $"[AccessStudio] MOVE SERVICE ACCESS APPLIED (development POC) | building={FormatEntity(buildingEntity)} | " +
                    $"roadA={FormatEntity(oldRoad)} | roadB={FormatEntity(roadEntity)} | curve={curvePosition:0.0000} | " +
                    $"world={FormatFloat3(record.CustomWorldPosition)} | originalSpawnLocations={record.OriginalSpawnLocations.Count}");
            }
            catch (Exception ex)
            {
                State = "APPLY FAILED";
                Status = ex.Message;
                Mod.Log.Error(ex, "[AccessStudio] MOVE SERVICE ACCESS failed; use Reset to Vanilla if an override record exists.");
            }
        }

        private void ApplyRemote(Entity buildingEntity, Entity roadEntity, float3 hitPosition)
        {
            try
            {
                if (!ValidateMove(buildingEntity, roadEntity, hitPosition, out var reason, true))
                {
                    State = "REMOTE TARGET REJECTED";
                    Status = reason;
                    return;
                }

                var building = EntityManager.GetComponentData<Building>(buildingEntity);
                if (!TryFindNearestCarLane(roadEntity, hitPosition, out var lane, out var lanePosition, out var laneWorld))
                {
                    State = "REMOTE TARGET REJECTED";
                    Status = "Road C has no persistent car lane with usable curve geometry.";
                    return;
                }

                var record = new OverrideRecord
                {
                    Building = buildingEntity,
                    Mode = OverrideMode.RemoteServicePoint,
                    OriginalBuilding = building,
                    HadBackSide = TryGetComponent(buildingEntity, out BackSide originalBackSide),
                    OriginalBackSide = originalBackSide,
                    CustomRoad = roadEntity,
                    CustomCurvePosition = lanePosition,
                    CustomWorldPosition = laneWorld,
                    AppliedFrame = m_Frame,
                    InitialGarbage = TryGetComponent(buildingEntity, out GarbageProducer producer) ? producer.m_Garbage : -1,
                    LastGarbage = TryGetComponent(buildingEntity, out GarbageProducer producer2) ? producer2.m_Garbage : -1
                };
                CaptureSpawnLocations(record);

                for (var i = 0; i < record.OriginalSpawnLocations.Count; i++)
                {
                    var snapshot = record.OriginalSpawnLocations[i];
                    if (!IsExternalVehicleSpawn(snapshot, building.m_RoadEdge)) continue;

                    var location = snapshot.Value;
                    location.m_ConnectedLane1 = lane;
                    location.m_ConnectedLane2 = Entity.Null;
                    location.m_CurvePosition1 = lanePosition;
                    location.m_CurvePosition2 = 0f;
                    EntityManager.SetComponentData(snapshot.Entity, location);

                    if (snapshot.HadTransform)
                    {
                        var moved = snapshot.Transform;
                        moved.m_Position = laneWorld;
                        EntityManager.SetComponentData(snapshot.Entity, moved);
                    }
                    EnsurePathfindUpdated(snapshot.Entity);
                    record.RemoteSpawnLocations.Add(snapshot.Entity);
                }

                if (record.RemoteSpawnLocations.Count == 0)
                {
                    RestoreSpawnLocations(record);
                    State = "REMOTE TARGET REJECTED";
                    Status = "No vehicle/service SpawnLocation connected to Road A was found. Nothing was changed.";
                    return;
                }

                record.AddedRemoteRoadMembership = AddConnectedBuildingMembership(roadEntity, buildingEntity);
                m_Overrides.Add(buildingEntity, record);
                ActiveBuilding = buildingEntity;
                OriginalRoad = building.m_RoadEdge;
                CustomRoad = roadEntity;
                HasActiveOverride = true;
                State = "EXPERIMENTAL REMOTE — MONITORING";
                Status = $"Road A remains {FormatEntity(building.m_RoadEdge)}. {record.RemoteSpawnLocations.Count} vehicle/service endpoint(s) now target Road C {FormatEntity(roadEntity)}.";
                Telemetry = BuildRemoteTelemetry(record, 0, 0, false);

                Mod.Log.Info(
                    $"[AccessStudio] REMOTE SERVICE POINT APPLIED (experimental/local only) | building={FormatEntity(buildingEntity)} | " +
                    $"frontageA={FormatEntity(building.m_RoadEdge)} | roadC={FormatEntity(roadEntity)} | lane={FormatEntity(lane)}@{lanePosition:0.0000} | " +
                    $"point={FormatFloat3(laneWorld)} | movedVehicleSpawns={record.RemoteSpawnLocations.Count} | addedRoadCMembership={record.AddedRemoteRoadMembership}");
            }
            catch (Exception ex)
            {
                State = "REMOTE APPLY FAILED";
                Status = ex.Message;
                Mod.Log.Error(ex, "[AccessStudio] PLACE REMOTE SERVICE POINT failed.");
            }
        }

        private void ApplyReset(Entity buildingEntity)
        {
            if (!m_Overrides.TryGetValue(buildingEntity, out var record))
            {
                PublishNoOverride();
                return;
            }

            try
            {
                if (!EntityManager.Exists(buildingEntity) || EntityManager.HasComponent<Deleted>(buildingEntity))
                {
                    m_Overrides.Remove(buildingEntity);
                    PublishNoOverride();
                    return;
                }

                var current = EntityManager.GetComponentData<Building>(buildingEntity);
                var currentRoad = current.m_RoadEdge;
                UpdateConnectedBuildingMembership(currentRoad, record.OriginalBuilding.m_RoadEdge, buildingEntity);
                EntityManager.SetComponentData(buildingEntity, record.OriginalBuilding);

                if (record.HadBackSide && EntityManager.HasComponent<BackSide>(buildingEntity))
                    EntityManager.SetComponentData(buildingEntity, record.OriginalBackSide);

                RestoreSpawnLocations(record);

                if (record.Mode == OverrideMode.RemoteServicePoint && record.AddedRemoteRoadMembership)
                    RemoveConnectedBuildingMembership(record.CustomRoad, buildingEntity);

                if (record.Mode == OverrideMode.RoadReassignment)
                    EmitRoadConnectionUpdated(buildingEntity, currentRoad, record.OriginalBuilding.m_RoadEdge);
                m_Overrides.Remove(buildingEntity);
                State = "VANILLA RESTORED";
                Status = record.Mode == OverrideMode.RemoteServicePoint
                    ? $"Removed the remote Road C endpoint from {FormatEntity(buildingEntity)}. Road A remained {FormatEntity(record.OriginalBuilding.m_RoadEdge)}."
                    : $"Restored {FormatEntity(buildingEntity)} to Road A {FormatEntity(record.OriginalBuilding.m_RoadEdge)} and released it to vanilla management.";
                Telemetry = "Transient override removed. Observe a newly routed service vehicle to verify vanilla behavior.";
                Mod.Log.Info(
                    $"[AccessStudio] RESET TO VANILLA APPLIED | building={FormatEntity(buildingEntity)} | " +
                    $"from={FormatEntity(currentRoad)} | restored={FormatEntity(record.OriginalBuilding.m_RoadEdge)} | " +
                    $"spawnLocationsRestored={record.OriginalSpawnLocations.Count}");
                ActiveBuilding = buildingEntity;
                OriginalRoad = record.OriginalBuilding.m_RoadEdge;
                CustomRoad = Entity.Null;
                HasActiveOverride = false;
                m_Probe?.RefreshSnapshot();
            }
            catch (Exception ex)
            {
                State = "RESET FAILED";
                Status = ex.Message;
                Mod.Log.Error(ex, "[AccessStudio] RESET TO VANILLA failed.");
            }
        }

        private void MonitorOverride(OverrideRecord record)
        {
            if (record.Mode == OverrideMode.RemoteServicePoint)
            {
                MonitorRemoteOverride(record);
                return;
            }

            var building = EntityManager.GetComponentData<Building>(record.Building);
            if (building.m_RoadEdge != record.CustomRoad || math.abs(building.m_CurvePosition - record.CustomCurvePosition) > 0.001f)
            {
                if (!record.Reverted)
                {
                    record.Reverted = true;
                    State = "VANILLA REVERSION OBSERVED";
                    Status = $"Vanilla changed the building to {FormatEntity(building.m_RoadEdge)} at t={building.m_CurvePosition:0.000}. The POC did not fight or reapply it.";
                    Mod.Log.Warn(
                        $"[AccessStudio] VANILLA REVERSION OBSERVED | building={FormatEntity(record.Building)} | " +
                        $"expectedRoadB={FormatEntity(record.CustomRoad)}@{record.CustomCurvePosition:0.0000} | " +
                        $"actual={FormatEntity(building.m_RoadEdge)}@{building.m_CurvePosition:0.0000} | framesAfterApply={m_Frame - record.AppliedFrame}");
                }
            }

            var requestEntities = new HashSet<Entity>();
            using (var entities = m_GarbageRequestQuery.ToEntityArray(Allocator.Temp))
            using (var requests = m_GarbageRequestQuery.ToComponentDataArray<GarbageCollectionRequest>(Allocator.Temp))
            {
                for (var i = 0; i < requests.Length; i++)
                    if (requests[i].m_Target == record.Building) requestEntities.Add(entities[i]);
            }
            if (requestEntities.Count > 0) record.HadGarbageRequest = true;

            var vehicleLines = new StringBuilder();
            var assigned = 0;
            using (var vehicleEntities = m_GarbageVehicleQuery.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < vehicleEntities.Length; i++)
                {
                    var vehicle = vehicleEntities[i];
                    var dispatches = EntityManager.GetBuffer<ServiceDispatch>(vehicle, true);
                    Entity matchingRequest = Entity.Null;
                    for (var j = 0; j < dispatches.Length; j++)
                    {
                        if (!requestEntities.Contains(dispatches[j].m_Request)) continue;
                        matchingRequest = dispatches[j].m_Request;
                        break;
                    }
                    if (matchingRequest == Entity.Null) continue;

                    assigned++;
                    var target = EntityManager.GetComponentData<Game.Common.Target>(vehicle);
                    var currentLane = Entity.Null;
                    var currentEdge = Entity.Null;
                    if (TryGetComponent(vehicle, out CarCurrentLane lane))
                    {
                        currentLane = lane.m_Lane;
                        currentEdge = ResolveOwningEdge(currentLane);
                        if (currentEdge == record.CustomRoad) record.ObservedTruckOnRoadB = true;
                        if (currentEdge == record.OriginalBuilding.m_RoadEdge) record.ObservedTruckOnRoadA = true;
                    }

                    var pathContainsB = false;
                    var pathContainsA = false;
                    if (EntityManager.HasBuffer<PathElement>(vehicle))
                    {
                        var path = EntityManager.GetBuffer<PathElement>(vehicle, true);
                        for (var j = 0; j < path.Length; j++)
                        {
                            var edge = ResolveOwningEdge(path[j].m_Target);
                            pathContainsB |= edge == record.CustomRoad;
                            pathContainsA |= edge == record.OriginalBuilding.m_RoadEdge;
                        }
                    }
                    record.ObservedPathToRoadB |= pathContainsB;

                    var prefabName = "unknown vehicle prefab";
                    if (TryGetComponent(vehicle, out PrefabRef prefabRef) &&
                        m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef.m_Prefab, out var prefab))
                        prefabName = prefab.name;

                    vehicleLines.AppendLine(
                        $"truck={FormatEntity(vehicle)} prefab={prefabName} request={FormatEntity(matchingRequest)} " +
                        $"target={FormatEntity(target.m_Target)} currentLane={FormatEntity(currentLane)} currentEdge={FormatEntity(currentEdge)} " +
                        $"pathHasA={pathContainsA} pathHasB={pathContainsB}");
                }
            }

            var garbage = -1;
            if (TryGetComponent(record.Building, out GarbageProducer producer)) garbage = producer.m_Garbage;
            var requestEnded = record.HadGarbageRequest && requestEntities.Count == 0;
            var signature =
                $"road={building.m_RoadEdge.Index}:{building.m_RoadEdge.Version};requests={requestEntities.Count};assigned={assigned};" +
                $"garbage={garbage};pathB={record.ObservedPathToRoadB};truckB={record.ObservedTruckOnRoadB};truckA={record.ObservedTruckOnRoadA};ended={requestEnded}";

            Telemetry =
                $"Building: {FormatEntity(record.Building)}\n" +
                $"Road A: {FormatEntity(record.OriginalBuilding.m_RoadEdge)}\n" +
                $"Road B: {FormatEntity(record.CustomRoad)} @ {record.CustomCurvePosition:0.000}\n" +
                $"Current building road: {FormatEntity(building.m_RoadEdge)} @ {building.m_CurvePosition:0.000}\n" +
                $"Garbage: {garbage} (at apply: {record.InitialGarbage})\n" +
                $"Requests: {requestEntities.Count} · assigned trucks: {assigned}\n" +
                $"Path contains Road B: {record.ObservedPathToRoadB}\n" +
                $"Truck physically on Road B: {record.ObservedTruckOnRoadB}\n" +
                $"Truck physically on Road A after override: {record.ObservedTruckOnRoadA}\n" +
                $"Request ended after Road B observation: {requestEnded && record.ObservedTruckOnRoadB}";

            if (signature != record.LastTelemetrySignature)
            {
                record.LastTelemetrySignature = signature;
                Mod.Log.Info(
                    $"[AccessStudio] SERVICE POC TELEMETRY | {signature}\n{vehicleLines.ToString().TrimEnd()}");
            }
            record.LastGarbage = garbage;

            if (!record.Reverted)
            {
                if (record.ObservedTruckOnRoadB)
                {
                    State = "ROAD B OBSERVED — COMPLETION PENDING";
                    Status = "A matching garbage truck was physically observed on Road B. Confirm arrival and garbage reduction before calling this a PASS.";
                }
                else if (record.ObservedPathToRoadB)
                {
                    State = "ROAD B IN PATH — MONITORING";
                    Status = "A matching truck path contains Road B, but physical approach and completed service are not proven yet.";
                }
            }

            if (m_Frame - record.AppliedFrame == MonitorIntervalFrames * 2)
                m_Probe?.RefreshSnapshot();
        }

        private void MonitorRemoteOverride(OverrideRecord record)
        {
            if (record.CustomRoad == Entity.Null || !EntityManager.Exists(record.CustomRoad) ||
                EntityManager.HasComponent<Deleted>(record.CustomRoad) || EntityManager.HasComponent<Temp>(record.CustomRoad))
            {
                State = "REMOTE TARGET ROAD REMOVED";
                Status = "Road C was removed. Access Studio queued an automatic safe return to vanilla.";
                Telemetry = BuildRemoteTelemetry(record, 0, 0, false) + "\nTarget road valid: False\nAutomatic reset: queued";
                m_Pending = new PendingOperation { Kind = PendingKind.Reset, Building = record.Building };
                Mod.Log.Warn($"[AccessStudio] REMOTE ROAD C REMOVED | building={FormatEntity(record.Building)} | roadC={FormatEntity(record.CustomRoad)} | automaticReset=True");
                return;
            }

            var building = EntityManager.GetComponentData<Building>(record.Building);
            var spawnIntact = true;
            for (var i = 0; i < record.RemoteSpawnLocations.Count; i++)
            {
                var entity = record.RemoteSpawnLocations[i];
                if (!TryGetComponent(entity, out SpawnLocation location) ||
                    ResolveOwningEdge(location.m_ConnectedLane1) != record.CustomRoad)
                {
                    spawnIntact = false;
                    break;
                }
            }
            if (!spawnIntact && !record.Reverted)
            {
                record.Reverted = true;
                State = "VANILLA REVERSION OBSERVED";
                Status = "Vanilla regenerated a remote SpawnLocation. The POC did not fight or reapply it; reset before another test.";
                Mod.Log.Warn($"[AccessStudio] REMOTE SPAWN REVERSION | building={FormatEntity(record.Building)} | roadC={FormatEntity(record.CustomRoad)} | harmonyUsed=False");
            }

            var requestEntities = new HashSet<Entity>();
            using (var entities = m_GarbageRequestQuery.ToEntityArray(Allocator.Temp))
            using (var requests = m_GarbageRequestQuery.ToComponentDataArray<GarbageCollectionRequest>(Allocator.Temp))
            {
                for (var i = 0; i < requests.Length; i++)
                    if (requests[i].m_Target == record.Building) requestEntities.Add(entities[i]);
            }
            if (requestEntities.Count > 0) record.HadGarbageRequest = true;

            var assigned = 0;
            using (var vehicles = m_GarbageVehicleQuery.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < vehicles.Length; i++)
                {
                    var vehicle = vehicles[i];
                    var dispatches = EntityManager.GetBuffer<ServiceDispatch>(vehicle, true);
                    var matches = false;
                    for (var j = 0; j < dispatches.Length; j++)
                        if (requestEntities.Contains(dispatches[j].m_Request)) { matches = true; break; }
                    if (!matches) continue;
                    assigned++;

                    if (TryGetComponent(vehicle, out CarCurrentLane currentLane) &&
                        ResolveOwningEdge(currentLane.m_Lane) == record.CustomRoad)
                        record.ObservedTruckOnRoadB = true;

                    if (TryGetComponent(vehicle, out ObjectTransform vehicleTransform) &&
                        math.distance(vehicleTransform.m_Position, record.CustomWorldPosition) <= RemoteReachDistance)
                        record.ObservedTruckReachedPoint = true;

                    if (EntityManager.HasBuffer<PathElement>(vehicle))
                    {
                        var path = EntityManager.GetBuffer<PathElement>(vehicle, true);
                        for (var j = 0; j < path.Length; j++)
                            if (ResolveOwningEdge(path[j].m_Target) == record.CustomRoad)
                            {
                                record.ObservedPathToRoadB = true;
                                break;
                            }
                    }
                }
            }

            var garbage = TryGetComponent(record.Building, out GarbageProducer producer) ? producer.m_Garbage : -1;
            record.LastGarbage = garbage;
            var requestEnded = record.HadGarbageRequest && requestEntities.Count == 0;
            var completed = requestEnded && record.ObservedTruckReachedPoint &&
                            record.InitialGarbage >= 0 && garbage >= 0 && garbage < record.InitialGarbage;
            Telemetry = BuildRemoteTelemetry(record, requestEntities.Count, assigned, completed) +
                        $"\nSpawn override intact: {spawnIntact}";

            var signature = $"roadA={building.m_RoadEdge.Index}:{building.m_RoadEdge.Version};roadC={record.CustomRoad.Index}:{record.CustomRoad.Version};" +
                            $"requests={requestEntities.Count};assigned={assigned};garbage={garbage};pathC={record.ObservedPathToRoadB};" +
                            $"truckC={record.ObservedTruckOnRoadB};reached={record.ObservedTruckReachedPoint};completed={completed};spawnIntact={spawnIntact}";
            if (signature != record.LastTelemetrySignature)
            {
                record.LastTelemetrySignature = signature;
                Mod.Log.Info($"[AccessStudio] REMOTE ROAD C TELEMETRY | {signature}");
            }

            if (!record.Reverted)
            {
                if (completed)
                {
                    State = "ROAD C HARD GATE — PASS";
                    Status = "A real garbage truck reached the manual Road C point and garbage decreased. Reset remains available.";
                }
                else if (record.ObservedTruckReachedPoint)
                {
                    State = "ROAD C REACHED — SERVICE PENDING";
                    Status = "A matching truck reached the manual point; waiting for the request to end and garbage to decrease.";
                }
                else if (record.ObservedTruckOnRoadB)
                {
                    State = "TRUCK ON ROAD C — MONITORING";
                    Status = "A matching garbage truck is physically on Road C; waiting for it to reach the manual point.";
                }
                else if (record.ObservedPathToRoadB)
                {
                    State = "ROAD C IN PATH — MONITORING";
                    Status = "A newly routed garbage truck path contains Road C; physical arrival is not proven yet.";
                }
            }
        }

        private string BuildRemoteTelemetry(OverrideRecord record, int requests, int assigned, bool completed)
        {
            return
                $"Building: {FormatEntity(record.Building)}\n" +
                $"Normal frontage Road A: {FormatEntity(record.OriginalBuilding.m_RoadEdge)}\n" +
                $"Remote service Road C: {FormatEntity(record.CustomRoad)} @ {record.CustomCurvePosition:0.000}\n" +
                $"Remote point: {FormatFloat3(record.CustomWorldPosition)}\n" +
                $"Vehicle/service endpoints moved: {record.RemoteSpawnLocations.Count}\n" +
                $"Garbage: {record.LastGarbage} (at apply: {record.InitialGarbage})\n" +
                $"Requests: {requests} · assigned trucks: {assigned}\n" +
                $"Path contains Road C: {record.ObservedPathToRoadB}\n" +
                $"Truck physically on Road C: {record.ObservedTruckOnRoadB}\n" +
                $"Truck reached remote point (≤{RemoteReachDistance:0} m): {record.ObservedTruckReachedPoint}\n" +
                $"Service completed with garbage decrease: {completed}\n" +
                $"Building frontage unchanged: {(EntityManager.Exists(record.Building) && EntityManager.GetComponentData<Building>(record.Building).m_RoadEdge == record.OriginalBuilding.m_RoadEdge)}";
        }

        private void CaptureSpawnLocations(OverrideRecord record)
        {
            if (!EntityManager.HasBuffer<SpawnLocationElement>(record.Building)) return;
            var elements = EntityManager.GetBuffer<SpawnLocationElement>(record.Building, true);
            for (var i = 0; i < elements.Length; i++)
            {
                var entity = elements[i].m_SpawnLocation;
                if (!TryGetComponent(entity, out SpawnLocation location)) continue;
                var hadTransform = TryGetComponent(entity, out ObjectTransform transform);
                record.OriginalSpawnLocations.Add(new SpawnSnapshot
                {
                    Entity = entity,
                    Value = location,
                    HadTransform = hadTransform,
                    Transform = transform
                });
            }
        }

        private bool IsExternalVehicleSpawn(SpawnSnapshot snapshot, Entity frontageRoad)
        {
            if (!TryGetComponent(snapshot.Entity, out PrefabRef prefabRef) ||
                !TryGetComponent(prefabRef.m_Prefab, out SpawnLocationData data)) return false;
            if (data.m_ActivityMask.m_Mask != 0 || (data.m_RoadTypes & RoadTypes.Car) == 0) return false;
            if (data.m_ConnectionType != RouteConnectionType.Road && data.m_ConnectionType != RouteConnectionType.Cargo)
                return false;
            return ResolveOwningEdge(snapshot.Value.m_ConnectedLane1) == frontageRoad ||
                   ResolveOwningEdge(snapshot.Value.m_ConnectedLane2) == frontageRoad ||
                   IsCarRoadConnectionLane(snapshot.Value.m_ConnectedLane1) ||
                   IsCarRoadConnectionLane(snapshot.Value.m_ConnectedLane2);
        }

        private bool IsCarRoadConnectionLane(Entity lane)
        {
            if (!TryGetComponent(lane, out Game.Net.ConnectionLane connection)) return false;
            return (connection.m_Flags & ConnectionLaneFlags.Road) != 0 &&
                   (connection.m_RoadTypes & RoadTypes.Car) != 0;
        }

        private bool TryFindNearestCarLane(Entity road, float3 hitPosition, out Entity laneEntity, out float lanePosition, out float3 worldPosition)
        {
            laneEntity = Entity.Null;
            lanePosition = 0f;
            worldPosition = default;
            var bestDistance = float.MaxValue;
            var lanes = EntityManager.GetBuffer<Game.Net.SubLane>(road, true);
            for (var i = 0; i < lanes.Length; i++)
            {
                var candidate = lanes[i].m_SubLane;
                if (!EntityManager.HasComponent<Game.Net.CarLane>(candidate) ||
                    !TryGetComponent(candidate, out Game.Net.Curve curve) ||
                    EntityManager.HasComponent<Deleted>(candidate) || EntityManager.HasComponent<Temp>(candidate)) continue;
                MathUtils.Distance(curve.m_Bezier.xz, hitPosition.xz, out var t);
                t = math.saturate(t);
                var point = MathUtils.Position(curve.m_Bezier, t);
                var distance = math.distancesq(point, hitPosition);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                laneEntity = candidate;
                lanePosition = t;
                worldPosition = point;
            }
            return laneEntity != Entity.Null;
        }

        private void RestoreSpawnLocations(OverrideRecord record)
        {
            for (var i = 0; i < record.OriginalSpawnLocations.Count; i++)
            {
                var snapshot = record.OriginalSpawnLocations[i];
                if (!EntityManager.Exists(snapshot.Entity)) continue;
                if (EntityManager.HasComponent<SpawnLocation>(snapshot.Entity))
                    EntityManager.SetComponentData(snapshot.Entity, snapshot.Value);
                if (snapshot.HadTransform && EntityManager.HasComponent<ObjectTransform>(snapshot.Entity))
                    EntityManager.SetComponentData(snapshot.Entity, snapshot.Transform);
                EnsurePathfindUpdated(snapshot.Entity);
            }
        }

        private void EnsurePathfindUpdated(Entity entity)
        {
            if (!EntityManager.Exists(entity) || EntityManager.HasComponent<PathfindUpdated>(entity)) return;
            EntityManager.AddComponent<PathfindUpdated>(entity);
        }

        private bool AddConnectedBuildingMembership(Entity road, Entity building)
        {
            if (road == Entity.Null || !EntityManager.Exists(road) || !EntityManager.HasBuffer<ConnectedBuilding>(road)) return false;
            var buffer = EntityManager.GetBuffer<ConnectedBuilding>(road);
            for (var i = 0; i < buffer.Length; i++)
                if (buffer[i].m_Building == building) return false;
            buffer.Add(new ConnectedBuilding(building));
            return true;
        }

        private void RemoveConnectedBuildingMembership(Entity road, Entity building)
        {
            if (road == Entity.Null || !EntityManager.Exists(road) || !EntityManager.HasBuffer<ConnectedBuilding>(road)) return;
            var buffer = EntityManager.GetBuffer<ConnectedBuilding>(road);
            for (var i = buffer.Length - 1; i >= 0; i--)
                if (buffer[i].m_Building == building) buffer.RemoveAt(i);
        }

        private void UpdateConnectedBuildingMembership(Entity oldRoad, Entity newRoad, Entity building)
        {
            if (oldRoad != Entity.Null && EntityManager.Exists(oldRoad) && EntityManager.HasBuffer<ConnectedBuilding>(oldRoad))
            {
                var oldBuffer = EntityManager.GetBuffer<ConnectedBuilding>(oldRoad);
                for (var i = oldBuffer.Length - 1; i >= 0; i--)
                    if (oldBuffer[i].m_Building == building) oldBuffer.RemoveAt(i);
            }
            if (newRoad != Entity.Null && EntityManager.Exists(newRoad) && EntityManager.HasBuffer<ConnectedBuilding>(newRoad))
            {
                var newBuffer = EntityManager.GetBuffer<ConnectedBuilding>(newRoad);
                for (var i = 0; i < newBuffer.Length; i++)
                    if (newBuffer[i].m_Building == building) return;
                newBuffer.Add(new ConnectedBuilding(building));
            }
        }

        private void EmitRoadConnectionUpdated(Entity building, Entity oldRoad, Entity newRoad)
        {
            var eventEntity = EntityManager.CreateEntity(typeof(Game.Common.Event), typeof(RoadConnectionUpdated));
            EntityManager.SetComponentData(eventEntity, new RoadConnectionUpdated
            {
                m_Building = building,
                m_Old = oldRoad,
                m_New = newRoad
            });
        }

        private bool HasCarLane(Entity road)
        {
            var lanes = EntityManager.GetBuffer<Game.Net.SubLane>(road, true);
            for (var i = 0; i < lanes.Length; i++)
                if (EntityManager.HasComponent<Game.Net.CarLane>(lanes[i].m_SubLane)) return true;
            return false;
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

        private void PublishNoOverride()
        {
            HasActiveOverride = false;
            ActiveBuilding = Entity.Null;
            OriginalRoad = Entity.Null;
            CustomRoad = Entity.Null;
            State = "NO OVERRIDE";
            Status = "Select a building, then choose Move Service Access.";
            Telemetry = "No service-access POC is active.";
        }

        private void RestoreAllOverrides(string reason)
        {
            if (m_Overrides.Count == 0) return;

            var buildings = new List<Entity>(m_Overrides.Keys);
            Mod.Log.Info($"[AccessStudio] Restoring {buildings.Count} transient override(s) during {reason}.");
            for (var i = 0; i < buildings.Count; i++)
            {
                try
                {
                    ApplyReset(buildings[i]);
                }
                catch (Exception ex)
                {
                    Mod.Log.Warn($"[AccessStudio] Could not restore {FormatEntity(buildings[i])} during {reason}: {ex.Message}");
                }
            }
            m_Overrides.Clear();
            PublishNoOverride();
        }

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

        private static string FormatEntity(Entity entity) => entity == Entity.Null ? "Null" : $"#{entity.Index}:{entity.Version}";
        private static string FormatFloat3(float3 value) => $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";

        protected override void OnDestroy()
        {
            try
            {
                RestoreAllOverrides("system teardown");
            }
            catch (Exception ex)
            {
                Mod.Log.Warn($"[AccessStudio] Teardown restoration was interrupted: {ex.Message}");
            }
            finally
            {
                m_Pending = default;
                m_Overrides.Clear();
                base.OnDestroy();
            }
        }
    }
}
