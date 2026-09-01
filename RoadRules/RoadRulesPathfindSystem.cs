using System;
using System.Collections.Generic;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Areas;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using NetCarLaneFlags = Game.Net.CarLaneFlags;
using NetCarLane = Game.Net.CarLane;
using PrefabRef = Game.Prefabs.PrefabRef;
using NetLaneData = Game.Prefabs.NetLaneData;
using CarLaneData = Game.Prefabs.CarLaneData;
using PathfindCarData = Game.Prefabs.PathfindCarData;

namespace RoadRules
{
    /// <summary>
    /// Authoritative CS2 Simulation & Pathfinding Enforcement System.
    /// Intercepts native CS2 ECS network and pathfinding graphs to ensure:
    /// 1. Closed physical lanes are completely removed from pathfinding (SubLane.m_PathMethods = 0, Blockage = 0..255, Speed = 0.001 m/s).
    /// 2. Transit / Public Only corridors set native CarLaneFlags.PublicOnly -> RuleFlags.ForbidPrivateTraffic.
    /// 3. Speed limits directly modify CarLane.m_SpeedLimit and path graph edge weights.
    /// 4. All sub-segment and slave routing entities in the PhysicalRoadLane receive synchronous updates.
    /// 5. Approaching and current vehicles are signaled with PathfindUpdated to recalculate routes around restrictions.
    /// </summary>
    [UpdateAfter(typeof(RoadRulesSystem))]
    [UpdateAfter(typeof(LaneDataSystem))]
    [UpdateAfter(typeof(LanesModifiedSystem))]
    public partial class RoadRulesPathfindSystem : GameSystemBase
    {
        private struct BaselineLaneState
        {
            public NetCarLane CarLane;
            public Entity ParentEdge;
            public bool HasParentSubLane;
            public PathMethod PathMethods;
        }

        public static ILog Log = LogManager.GetLogger(nameof(RoadRules)).SetShowsErrorsInUI(false);
        public static RoadRulesPathfindSystem Instance { get; private set; }

        private EntityQuery m_VehicleQuery;
        private EntityQuery m_ObservedVehicleQuery;
        private PathfindQueueSystem m_PathfindQueueSystem;
        private readonly Dictionary<Entity, BaselineLaneState> m_Baselines = new Dictionary<Entity, BaselineLaneState>();
        private readonly HashSet<Entity> m_PreExistingObservedVehicles = new HashSet<Entity>();
        private readonly HashSet<Entity> m_ObservedVehicles = new HashSet<Entity>();
        private PhysicalRoadLane m_ObservedPhysicalLane;
        private int m_ObservedCars;
        private int m_ObservedTrucks;
        private int m_ObservedBuses;
        private int m_ObservedTaxis;
        private int m_ObservedEmergency;
        private int m_ObservedServices;
        private int m_ObservedViolations;
        private int m_ObservationCursor;
        private int m_ReassertionTick;
        private int m_RebuildsTriggeredCount = 0;
        private int m_VehiclesReroutedCount = 0;
        private int m_ActiveEnforcedEntitiesCount = 0;
        private int m_ExistingPathsSignaledLastApply = 0;
        private bool m_NativeUpdateRequestedLastApply = false;

        public int RebuildsTriggeredCount => m_RebuildsTriggeredCount;
        public int VehiclesReroutedCount => m_VehiclesReroutedCount;
        public int ActiveEnforcedEntitiesCount => m_ActiveEnforcedEntitiesCount;
        public int ExistingPathsSignaledLastApply => m_ExistingPathsSignaledLastApply;
        public bool NativeUpdateRequestedLastApply => m_NativeUpdateRequestedLastApply;
        public int ObservedCars => m_ObservedCars;
        public int ObservedTrucks => m_ObservedTrucks;
        public int ObservedBuses => m_ObservedBuses;
        public int ObservedTaxis => m_ObservedTaxis;
        public int ObservedEmergency => m_ObservedEmergency;
        public int ObservedServices => m_ObservedServices;
        public int ObservedViolations => m_ObservedViolations;
        public string EnforcementSummary { get; private set; } = "VANILLA";

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            m_PathfindQueueSystem = World.GetOrCreateSystemManaged<PathfindQueueSystem>();
            m_VehicleQuery = GetEntityQuery(ComponentType.ReadOnly<Game.Vehicles.Car>(), ComponentType.ReadOnly<PathOwner>());
            m_ObservedVehicleQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Vehicles.Car>(),
                ComponentType.ReadOnly<Game.Vehicles.CarCurrentLane>());
            Log.Info("[RoadRules] RoadRulesPathfindSystem created — native path graph rule injection active.");
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            RestoreAllToVanilla("game preload", queueNativeRebuilds: false);
            base.OnGamePreload(purpose, mode);
            ResetObservationState(null);
        }

        protected override void OnUpdate()
        {
            // LaneDataSystem legitimately rebuilds lanes after network edits and can restore vanilla
            // component values. Reassert active in-memory rules at a low cadence, after both the lane
            // data and native path graph rebuild systems have run. Nothing is serialized into the save.
            if (++m_ReassertionTick < 32)
                return;

            m_ReassertionTick = 0;
            var rules = RoadRulesSystem.Instance?.GetAllRules();
            if (rules == null)
                return;

            foreach (var pair in rules)
            {
                var record = pair.Value;
                if (record?.PhysicalLanes == null)
                    continue;

                for (int i = 0; i < record.PhysicalLanes.Count; i++)
                {
                    PhysicalRoadLane lane = record.PhysicalLanes[i];
                    if (lane.Preset != LaneRulePreset.DefaultAll || lane.IsClosed || lane.LocalAccessOnly ||
                        lane.AllowedVehicles != VehicleAccessFlags.All || lane.CustomSpeedLimitKph > 0f)
                    {
                        EnforcePhysicalLane(lane, false);
                    }
                }
            }

            if (RoadRulesUISystem.Instance?.PanelOpen == true)
                ObserveSelectedLaneTraffic();
        }

        /// <summary>
        /// Enforces the rules of a physical road lane across EVERY underlying routing entity.
        /// </summary>
        public void EnforcePhysicalLane(PhysicalRoadLane physicalLane, bool invalidateExistingVehicles = true)
        {
            if (physicalLane == null || physicalLane.RoutingEntities == null || physicalLane.RoutingEntities.Count == 0)
                return;

            HashSet<Entity> affectedEdges = new HashSet<Entity>();
            if (invalidateExistingVehicles) m_NativeUpdateRequestedLastApply = false;

            for (int i = 0; i < physicalLane.RoutingEntities.Count; i++)
            {
                Entity entity = physicalLane.RoutingEntities[i];
                PhysicalRoadLane effectiveRule = RoadRulesSystem.Instance?.ResolveEffectiveRule(entity, physicalLane) ?? physicalLane;
                EnforceRule(entity, effectiveRule, affectedEdges, invalidateExistingVehicles);
                if (invalidateExistingVehicles) m_NativeUpdateRequestedLastApply = true;
            }

            // Only a user rule change performs the bounded one-shot scan. The periodic reassertion
            // never repeats a city-wide vehicle scan.
            if (!invalidateExistingVehicles) return;
            m_ExistingPathsSignaledLastApply = 0;
            var signaled = new HashSet<Entity>();
            InvalidateVehiclesOnEdges(affectedEdges, physicalLane, signaled);
            m_ExistingPathsSignaledLastApply = signaled.Count;
            m_VehiclesReroutedCount += signaled.Count;
            ResetObservationState(physicalLane);
        }

        /// <summary>
        /// Immediately applies the given rule's restrictions to a real CarLane entity and parent Edge SubLane buffer.
        /// </summary>
        public void EnforceRule(Entity laneEnt, PhysicalRoadLane rule, HashSet<Entity> affectedEdges, bool forceNativeGraphUpdate)
        {
            if (laneEnt == Entity.Null || !EntityManager.Exists(laneEnt) || !EntityManager.HasComponent<CarLane>(laneEnt))
            {
                return;
            }

            try
            {
                NetCarLane currentCarLane = EntityManager.GetComponentData<NetCarLane>(laneEnt);
                bool carLaneModified = false;
                bool edgeBufferModified = false;
                Entity parentEdge = Entity.Null;

                if (EntityManager.HasComponent<Owner>(laneEnt))
                {
                    parentEdge = EntityManager.GetComponentData<Owner>(laneEnt).m_Owner;
                    if (parentEdge != Entity.Null && EntityManager.Exists(parentEdge))
                    {
                        affectedEdges?.Add(parentEdge);
                    }
                }

                BaselineLaneState baseline = CaptureBaseline(laneEnt, parentEdge, currentCarLane);
                // Rebase only the fields Road Rules owns. Starting from the current component
                // preserves unrelated flags written by vanilla or another mod after our baseline
                // was captured, while still making every Road Rules transition reversible.
                NetCarLane carLane = currentCarLane;
                const NetCarLaneFlags controlledFlags = NetCarLaneFlags.Forbidden | NetCarLaneFlags.PublicOnly;
                carLane.m_Flags = (carLane.m_Flags & ~controlledFlags) | (baseline.CarLane.m_Flags & controlledFlags);
                carLane.m_BlockageStart = baseline.CarLane.m_BlockageStart;
                carLane.m_BlockageEnd = baseline.CarLane.m_BlockageEnd;
                carLane.m_SpeedLimit = baseline.CarLane.m_SpeedLimit;
                RoadRuleTranslation translation = RoadRuleTranslator.Translate(
                    rule.AllowedVehicles,
                    rule.IsClosed,
                    rule.LocalAccessOnly);
                if (!translation.IsSupported)
                {
                    Log.Warn($"[RoadRules] Unsupported rule rejected for routing entity #{laneEnt.Index}: {translation.Description}");
                    return;
                }
                EnforcementSummary = translation.Description;

                // 1. HARD CLOSURE
                if (translation.HardClosed)
                {
                    // A. Update CarLane Component
                    carLane.m_Flags |= NetCarLaneFlags.Forbidden;
                    carLane.m_BlockageStart = 0;
                    carLane.m_BlockageEnd = 255;
                    carLane.m_SpeedLimit = 0.001f;

                    // B. Modify Parent Edge SubLane Buffer PathMethods (Hard Disallow in PathfindExecutor.DisallowConnection)
                    if (parentEdge != Entity.Null && EntityManager.HasBuffer<SubLane>(parentEdge))
                    {
                        var subLaneBuffer = EntityManager.GetBuffer<SubLane>(parentEdge);
                        for (int s = 0; s < subLaneBuffer.Length; s++)
                        {
                            var subLane = subLaneBuffer[s];
                            if (subLane.m_SubLane == laneEnt)
                            {
                                PathMethod targetMethods = (PathMethod)0;
                                if (subLane.m_PathMethods != targetMethods)
                                {
                                    subLane.m_PathMethods = targetMethods; // Hard disallow for ALL path types
                                    subLaneBuffer[s] = subLane;
                                    edgeBufferModified = true;
                                }
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // REOPEN / RESTORE THE EXACT CAPTURED SUB-LANE BUFFER VALUE.
                    if (parentEdge != Entity.Null && EntityManager.HasBuffer<SubLane>(parentEdge))
                    {
                        var subLaneBuffer = EntityManager.GetBuffer<SubLane>(parentEdge);
                        for (int s = 0; s < subLaneBuffer.Length; s++)
                        {
                            var subLane = subLaneBuffer[s];
                            if (subLane.m_SubLane == laneEnt)
                            {
                                PathMethod restoredMethods = baseline.HasParentSubLane
                                    ? baseline.PathMethods
                                    : subLane.m_PathMethods;
                                if (subLane.m_PathMethods != restoredMethods)
                                {
                                    subLane.m_PathMethods = restoredMethods;
                                    subLaneBuffer[s] = subLane;
                                    edgeBufferModified = true;
                                }
                                break;
                            }
                        }
                    }

                    // 2. VEHICLE CLASS PERMISSIONS. PublicOnly is CS2's one real road-lane
                    // eligibility category. It includes buses, taxis, emergency and several
                    // service vehicles; independent six-class hard masks do not exist.
                    if (translation.SetPublicOnly)
                        carLane.m_Flags |= NetCarLaneFlags.PublicOnly;

                    // 3. SPEED LIMITS
                    float targetSpeedMps;
                    if (rule.CustomSpeedLimitKph > 0f)
                    {
                        targetSpeedMps = rule.CustomSpeedLimitKph / 3.6f;
                    }
                    else if (translation.Kind == RoadRuleEnforcementKind.LocalAccessBias)
                    {
                        // Local access penalty: lower speed rating discourages through-traffic A* choice while maintaining connectivity
                        targetSpeedMps = math.min(carLane.m_DefaultSpeedLimit > 0f ? carLane.m_DefaultSpeedLimit : (50f / 3.6f), 15f / 3.6f);
                    }
                    else
                    {
                        // Preserve the exact captured runtime value. This respects vanilla lanes
                        // and other speed-management mods instead of guessing from the prefab.
                        targetSpeedMps = baseline.CarLane.m_SpeedLimit;
                    }

                    if (math.abs(carLane.m_SpeedLimit - targetSpeedMps) > 0.05f)
                    {
                        carLane.m_SpeedLimit = targetSpeedMps;
                        carLaneModified = true;
                    }
                }

                // 4. INVALIDATE CS2 PATHFINDING GRAPH
                carLaneModified = !carLane.Equals(currentCarLane);
                if (carLaneModified)
                {
                    EntityManager.SetComponentData(laneEnt, carLane);
                    if (!EntityManager.HasComponent<PathfindUpdated>(laneEnt))
                        EntityManager.AddComponent<PathfindUpdated>(laneEnt);
                    if (!EntityManager.HasComponent<Updated>(laneEnt))
                        EntityManager.AddComponent<Updated>(laneEnt);

                    m_ActiveEnforcedEntitiesCount++;
                }

                if (edgeBufferModified && parentEdge != Entity.Null)
                {
                    if (!EntityManager.HasComponent<PathfindUpdated>(parentEdge))
                        EntityManager.AddComponent<PathfindUpdated>(parentEdge);
                    if (!EntityManager.HasComponent<Updated>(parentEdge))
                        EntityManager.AddComponent<Updated>(parentEdge);

                    m_RebuildsTriggeredCount++;
                }

                // CarLane flags alone are not sufficient for a truck-only ban. Inject the native
                // ForbidHeavyTraffic RuleFlags bit into the actual path graph specification. CS2's
                // own vehicle path parameters ignore this bit for light cars and obey it for heavy
                // vehicles, which is the same mechanism used by the vanilla Heavy Traffic Ban.
                if (forceNativeGraphUpdate || carLaneModified || edgeBufferModified)
                    QueueNativePathfindSpecification(laneEnt, parentEdge, carLane, rule, translation);

                // An unrestricted lane can still have a custom speed. Keep its original
                // capture until that speed override is reset, or the next tick captures
                // our own modified speed as "vanilla" and Reset cannot restore it.
                if (translation.Kind == RoadRuleEnforcementKind.Vanilla && rule.CustomSpeedLimitKph <= 0f)
                    m_Baselines.Remove(laneEnt);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[RoadRules] EnforceRule exception on Routing Entity #{laneEnt.Index}");
            }
        }

        private void QueueNativePathfindSpecification(Entity laneEnt, Entity parentEdge, NetCarLane carLane, PhysicalRoadLane rule, RoadRuleTranslation translation)
        {
            if (m_PathfindQueueSystem == null || !EntityManager.HasComponent<Lane>(laneEnt) ||
                !EntityManager.HasComponent<Curve>(laneEnt) || !EntityManager.HasComponent<PrefabRef>(laneEnt))
                return;

            PrefabRef prefabRef = EntityManager.GetComponentData<PrefabRef>(laneEnt);
            if (prefabRef.m_Prefab == Entity.Null ||
                !EntityManager.HasComponent<NetLaneData>(prefabRef.m_Prefab) ||
                !EntityManager.HasComponent<CarLaneData>(prefabRef.m_Prefab))
                return;

            NetLaneData netLaneData = EntityManager.GetComponentData<NetLaneData>(prefabRef.m_Prefab);
            if (netLaneData.m_PathfindPrefab == Entity.Null ||
                !EntityManager.HasComponent<PathfindCarData>(netLaneData.m_PathfindPrefab))
                return;

            Lane lane = EntityManager.GetComponentData<Lane>(laneEnt);
            Curve curve = EntityManager.GetComponentData<Curve>(laneEnt);
            CarLaneData carLaneData = EntityManager.GetComponentData<CarLaneData>(prefabRef.m_Prefab);
            PathfindCarData pathfindCarData = EntityManager.GetComponentData<PathfindCarData>(netLaneData.m_PathfindPrefab);
            MasterLane masterLane = EntityManager.HasComponent<MasterLane>(laneEnt)
                ? EntityManager.GetComponentData<MasterLane>(laneEnt)
                : default;

            RuleFlags nativeRules = GetVanillaDistrictRules(parentEdge, carLane, carLaneData);
            nativeRules |= translation.AddedNativeRules;

            PathSpecification specification = PathUtils.GetCarDriveSpecification(
                curve, carLane, masterLane, carLaneData, pathfindCarData, nativeRules, 0.01f);

            if (translation.HardClosed)
            {
                specification.m_Methods = (PathMethod)0;
                specification.m_Rules |= RuleFlags.HasBlockage;
                specification.m_BlockageStart = 0;
                specification.m_BlockageEnd = 255;
            }

            var action = new UpdateAction(1, Allocator.Persistent);
            action.m_UpdateData[0] = new UpdateActionData
            {
                m_Owner = laneEnt,
                m_StartNode = lane.m_StartNode,
                m_MiddleNode = lane.m_MiddleNode,
                m_EndNode = lane.m_EndNode,
                m_Specification = specification,
                m_Location = PathUtils.GetLocationSpecification(curve)
            };
            m_PathfindQueueSystem.Enqueue(action, Dependency);
            m_RebuildsTriggeredCount++;

            Log.Debug($"[RoadRules] Native graph queued lane=#{laneEnt.Index} rules={specification.m_Rules} methods={specification.m_Methods} mode={translation.Kind}");
        }

        private BaselineLaneState CaptureBaseline(Entity laneEnt, Entity parentEdge, NetCarLane currentCarLane)
        {
            if (m_Baselines.TryGetValue(laneEnt, out BaselineLaneState baseline))
                return baseline;

            baseline = new BaselineLaneState { CarLane = currentCarLane, ParentEdge = parentEdge };
            if (parentEdge != Entity.Null && EntityManager.Exists(parentEdge) && EntityManager.HasBuffer<SubLane>(parentEdge))
            {
                DynamicBuffer<SubLane> subLanes = EntityManager.GetBuffer<SubLane>(parentEdge);
                for (int i = 0; i < subLanes.Length; i++)
                {
                    if (subLanes[i].m_SubLane != laneEnt) continue;
                    baseline.HasParentSubLane = true;
                    baseline.PathMethods = subLanes[i].m_PathMethods;
                    break;
                }
            }
            m_Baselines[laneEnt] = baseline;
            return baseline;
        }

        public void RestoreAllToVanilla(string reason, bool queueNativeRebuilds)
        {
            if (m_Baselines.Count == 0)
                return;

            int restored = 0;
            var snapshots = new List<KeyValuePair<Entity, BaselineLaneState>>(m_Baselines);
            for (int i = 0; i < snapshots.Count; i++)
            {
                Entity laneEnt = snapshots[i].Key;
                BaselineLaneState baseline = snapshots[i].Value;
                if (laneEnt == Entity.Null || !EntityManager.Exists(laneEnt) ||
                    !EntityManager.HasComponent<NetCarLane>(laneEnt))
                    continue;

                EntityManager.SetComponentData(laneEnt, baseline.CarLane);
                if (!EntityManager.HasComponent<PathfindUpdated>(laneEnt))
                    EntityManager.AddComponent<PathfindUpdated>(laneEnt);
                if (!EntityManager.HasComponent<Updated>(laneEnt))
                    EntityManager.AddComponent<Updated>(laneEnt);

                Entity parentEdge = baseline.ParentEdge;
                if (baseline.HasParentSubLane && parentEdge != Entity.Null &&
                    EntityManager.Exists(parentEdge) && EntityManager.HasBuffer<SubLane>(parentEdge))
                {
                    DynamicBuffer<SubLane> subLanes = EntityManager.GetBuffer<SubLane>(parentEdge);
                    for (int s = 0; s < subLanes.Length; s++)
                    {
                        SubLane subLane = subLanes[s];
                        if (subLane.m_SubLane != laneEnt) continue;
                        subLane.m_PathMethods = baseline.PathMethods;
                        subLanes[s] = subLane;
                        break;
                    }
                    if (!EntityManager.HasComponent<PathfindUpdated>(parentEdge))
                        EntityManager.AddComponent<PathfindUpdated>(parentEdge);
                    if (!EntityManager.HasComponent<Updated>(parentEdge))
                        EntityManager.AddComponent<Updated>(parentEdge);
                }

                if (queueNativeRebuilds)
                {
                    var vanillaRule = PhysicalRoadLane.CreateDefault(
                        0,
                        new List<Entity> { laneEnt },
                        laneEnt,
                        laneEnt,
                        Entity.Null,
                        EntityManager.HasComponent<Curve>(laneEnt) ? EntityManager.GetComponentData<Curve>(laneEnt) : default,
                        0f,
                        0,
                        0,
                        false,
                        0,
                        "Restored");
                    QueueNativePathfindSpecification(
                        laneEnt,
                        parentEdge,
                        baseline.CarLane,
                        vanillaRule,
                        RoadRuleTranslator.Translate(VehicleAccessFlags.All, false, false));
                }

                restored++;
            }

            m_Baselines.Clear();
            ResetObservationState(null);
            EnforcementSummary = "VANILLA — restored captured lane state";
            Log.Info($"[RoadRules] Restored {restored} routing lane(s) to captured vanilla state during {reason}.");
        }

        private void ResetObservationState(PhysicalRoadLane lane)
        {
            m_ObservedPhysicalLane = lane;
            m_PreExistingObservedVehicles.Clear();
            m_ObservedVehicles.Clear();
            m_ObservedCars = 0;
            m_ObservedTrucks = 0;
            m_ObservedBuses = 0;
            m_ObservedTaxis = 0;
            m_ObservedEmergency = 0;
            m_ObservedServices = 0;
            m_ObservedViolations = 0;
            m_ObservationCursor = 0;

            if (lane?.RoutingEntities == null || lane.RoutingEntities.Count == 0 || m_ObservedVehicleQuery.IsEmptyIgnoreFilter)
                return;

            NativeArray<Entity> entities = default;
            NativeArray<Game.Vehicles.CarCurrentLane> currentLanes = default;
            try
            {
                entities = m_ObservedVehicleQuery.ToEntityArray(Allocator.Temp);
                currentLanes = m_ObservedVehicleQuery.ToComponentDataArray<Game.Vehicles.CarCurrentLane>(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    if (IsOnObservedLane(currentLanes[i], lane))
                        m_PreExistingObservedVehicles.Add(entities[i]);
                }
            }
            finally
            {
                if (currentLanes.IsCreated) currentLanes.Dispose();
                if (entities.IsCreated) entities.Dispose();
            }
        }

        /// <summary>
        /// Debug-only sampled observation. It runs at the existing 32-frame maintenance cadence
        /// and only while the Road Rules panel is open; normal gameplay has no vehicle scan.
        /// Counters represent unique vehicles first seen entering after the last user rule apply.
        /// </summary>
        private void ObserveSelectedLaneTraffic()
        {
            PhysicalRoadLane lane = m_ObservedPhysicalLane;
            if (lane?.RoutingEntities == null || lane.RoutingEntities.Count == 0 || m_ObservedVehicleQuery.IsEmptyIgnoreFilter)
                return;

            NativeArray<Entity> entities = m_ObservedVehicleQuery.ToEntityArray(Allocator.Temp);
            NativeArray<Game.Vehicles.CarCurrentLane> currentLanes =
                m_ObservedVehicleQuery.ToComponentDataArray<Game.Vehicles.CarCurrentLane>(Allocator.Temp);
            try
            {
                int sampleLimit = math.min(entities.Length, 4096);
                for (int sample = 0; sample < sampleLimit; sample++)
                {
                    int i = (m_ObservationCursor + sample) % entities.Length;
                    Entity vehicle = entities[i];
                    if (!IsOnObservedLane(currentLanes[i], lane)) continue;

                    if (m_PreExistingObservedVehicles.Contains(vehicle) || !m_ObservedVehicles.Add(vehicle))
                        continue;

                    VehicleAccessFlags category = ClassifyVehicle(vehicle);
                    IncrementObserved(category);

                    PhysicalRoadLane effective = RoadRulesSystem.Instance?.ResolveEffectiveRule(
                        currentLanes[i].m_Lane,
                        lane) ?? lane;
                    RoadRuleTranslation translation = RoadRuleTranslator.Translate(
                        effective.AllowedVehicles,
                        effective.IsClosed,
                        effective.LocalAccessOnly);
                    if (!IsAllowedByTranslation(category, translation))
                    {
                        m_ObservedViolations++;
                        Log.Warn($"[RoadRules] OBSERVED VIOLATION vehicle=#{vehicle.Index} class={category} lane=#{currentLanes[i].m_Lane.Index} mode={translation.Kind}");
                    }
                }
                if (entities.Length > 0)
                    m_ObservationCursor = (m_ObservationCursor + sampleLimit) % entities.Length;
            }
            finally
            {
                currentLanes.Dispose();
                entities.Dispose();
            }

        }

        private static bool IsOnObservedLane(Game.Vehicles.CarCurrentLane currentLane, PhysicalRoadLane lane)
        {
            return lane.RoutingEntities.Contains(currentLane.m_Lane) ||
                   (currentLane.m_ChangeLane != Entity.Null && lane.RoutingEntities.Contains(currentLane.m_ChangeLane));
        }

        private VehicleAccessFlags ClassifyVehicle(Entity vehicle)
        {
            if (EntityManager.HasComponent<Game.Vehicles.Ambulance>(vehicle) ||
                EntityManager.HasComponent<Game.Vehicles.FireEngine>(vehicle) ||
                EntityManager.HasComponent<Game.Vehicles.PoliceCar>(vehicle))
                return VehicleAccessFlags.Emergency;

            if (EntityManager.HasComponent<Game.Vehicles.Taxi>(vehicle))
                return VehicleAccessFlags.Taxis;

            if (EntityManager.HasComponent<Game.Vehicles.PublicTransport>(vehicle))
                return VehicleAccessFlags.Buses;

            if (EntityManager.HasComponent<Game.Vehicles.GarbageTruck>(vehicle) ||
                EntityManager.HasComponent<Game.Vehicles.MaintenanceVehicle>(vehicle) ||
                EntityManager.HasComponent<Game.Vehicles.PostVan>(vehicle) ||
                EntityManager.HasComponent<Game.Vehicles.Hearse>(vehicle))
                return VehicleAccessFlags.Service;

            if (EntityManager.HasComponent<Game.Vehicles.DeliveryTruck>(vehicle))
                return VehicleAccessFlags.Trucks;

            return VehicleAccessFlags.PrivateCars;
        }

        private void IncrementObserved(VehicleAccessFlags category)
        {
            switch (category)
            {
                case VehicleAccessFlags.Trucks: m_ObservedTrucks++; break;
                case VehicleAccessFlags.Buses: m_ObservedBuses++; break;
                case VehicleAccessFlags.Taxis: m_ObservedTaxis++; break;
                case VehicleAccessFlags.Emergency: m_ObservedEmergency++; break;
                case VehicleAccessFlags.Service: m_ObservedServices++; break;
                default: m_ObservedCars++; break;
            }
        }

        private static bool IsAllowedByTranslation(VehicleAccessFlags category, RoadRuleTranslation translation)
        {
            switch (translation.Kind)
            {
                case RoadRuleEnforcementKind.HardClosure:
                    return false;
                case RoadRuleEnforcementKind.PublicLaneGate:
                    return (RoadRuleTranslator.PublicLaneUsers & category) != 0;
                case RoadRuleEnforcementKind.HeavyTrafficAvoidance:
                    // This is a soft native route-cost preference, not a hard
                    // eligibility gate. A truck entering is not a violation.
                    return true;
                default:
                    return true;
            }
        }

        private RuleFlags GetVanillaDistrictRules(Entity parentEdge, NetCarLane carLane, CarLaneData carLaneData)
        {
            RuleFlags add = 0;
            RuleFlags remove = 0;
            if (parentEdge == Entity.Null || !EntityManager.HasComponent<BorderDistrict>(parentEdge))
                return 0;

            BorderDistrict border = EntityManager.GetComponentData<BorderDistrict>(parentEdge);
            AccumulateDistrictRules(border.m_Left, carLane, carLaneData, ref add, ref remove);
            AccumulateDistrictRules(border.m_Right, carLane, carLaneData, ref add, ref remove);
            return (RuleFlags)((byte)add & (byte)~remove);
        }

        private void AccumulateDistrictRules(Entity districtEntity, NetCarLane carLane, CarLaneData carLaneData,
            ref RuleFlags add, ref RuleFlags remove)
        {
            if (districtEntity == Entity.Null || !EntityManager.HasComponent<District>(districtEntity))
                return;

            District district = EntityManager.GetComponentData<District>(districtEntity);
            AccumulateOption(district, DistrictOption.ForbidCombustionEngines, RuleFlags.ForbidCombustionEngines, ref add, ref remove);
            AccumulateOption(district, DistrictOption.ForbidTransitTraffic, RuleFlags.ForbidTransitTraffic, ref add, ref remove);

            if ((carLaneData.m_RoadTypes & RoadTypes.Car) != RoadTypes.None && (carLane.m_Flags & NetCarLaneFlags.Highway) == 0)
                AccumulateOption(district, DistrictOption.ForbidHeavyTraffic, RuleFlags.ForbidHeavyTraffic, ref add, ref remove);
        }

        private static void AccumulateOption(District district, DistrictOption option, RuleFlags flag,
            ref RuleFlags add, ref RuleFlags remove)
        {
            if (AreaUtils.CheckOption(district, option)) add |= flag;
            else remove |= flag;
        }

        /// <summary>
        /// Invalidates existing vehicle routes that are navigating towards or across the restricted edge.
        /// </summary>
        private void InvalidateVehiclesOnEdges(HashSet<Entity> edgeEntities, PhysicalRoadLane rule, HashSet<Entity> signaled)
        {
            if (edgeEntities == null || edgeEntities.Count == 0) return;

            NativeArray<Entity> vehicleEntities = default;
            try
            {
                // Scan the vehicle population once for the entire physical lane. A physical
                // lane can have several routing representations/owners around a junction; the
                // previous per-edge loop repeated the same city-wide scan for every owner.
                vehicleEntities = m_VehicleQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < vehicleEntities.Length; i++)
                {
                    Entity veh = vehicleEntities[i];
                    if (!EntityManager.Exists(veh)) continue;

                    if (EntityManager.HasBuffer<PathElement>(veh))
                    {
                        var pathBuffer = EntityManager.GetBuffer<PathElement>(veh);
                        bool usesEdgeOrLane = false;

                        for (int p = 0; p < pathBuffer.Length; p++)
                        {
                            Entity targetEnt = pathBuffer[p].m_Target;
                            if (edgeEntities.Contains(targetEnt) || rule.RoutingEntities.Contains(targetEnt))
                            {
                                usesEdgeOrLane = true;
                                break;
                            }
                        }

                        if (usesEdgeOrLane)
                        {
                            if (!EntityManager.HasComponent<PathfindUpdated>(veh))
                            {
                                EntityManager.AddComponent<PathfindUpdated>(veh);
                            }
                            signaled?.Add(veh);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RoadRules] InvalidateVehiclesOnEdges failed");
            }
            finally
            {
                if (vehicleEntities.IsCreated)
                    vehicleEntities.Dispose();
            }
        }

        protected override void OnDestroy()
        {
            if (World != null && World.IsCreated)
                RestoreAllToVanilla("system teardown", queueNativeRebuilds: false);
            Instance = null;
            base.OnDestroy();
        }
    }
}
