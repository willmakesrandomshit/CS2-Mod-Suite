using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Vehicles;
using Game.Tools;
using TrafficSpawner = Game.Buildings.TrafficSpawner;
using Unity.Collections;
using Unity.Entities;

namespace TrafficStressTester
{
    /// <summary>
    /// Amplifies approved road dispatches immediately before the native spawner
    /// consumes them. Cloning RandomTrafficRequest is invalid: each destination
    /// TrafficSpawner owns exactly one request, so the dispatcher rejects clones.
    /// </summary>
    public sealed partial class TrafficStressSystem : GameSystemBase
    {
        private TrafficSpawnerAISystem m_SpawnerSystem;
        private EntityQuery m_SourceQuery;
        private EntityQuery m_LegacyCloneQuery;
        private EntityQuery m_RoadVehicleQuery;
        private int m_SourceCursor;
        private int m_LastMultiplier = 1;
        private int m_RampBatches;
        private bool m_LogFirstBatch;

        // Use the native cadence and offset; every inserted entry must be consumed
        // in this same simulation batch, before its request completes and is deleted.
        public override int GetUpdateInterval(SystemUpdatePhase phase) =>
            m_SpawnerSystem.GetUpdateInterval(phase);
        public override int GetUpdateOffset(SystemUpdatePhase phase) =>
            m_SpawnerSystem.GetUpdateOffset(phase);

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SpawnerSystem = World.GetOrCreateSystemManaged<TrafficSpawnerAISystem>();
            m_SourceQuery = GetEntityQuery(
                ComponentType.ReadOnly<TrafficSpawner>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Transform>(),
                ComponentType.ReadWrite<ServiceDispatch>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Deleted>());
            // Retain the old component type only to clean requests from earlier builds.
            m_LegacyCloneQuery = GetEntityQuery(ComponentType.ReadOnly<TrafficStressClone>(),
                ComponentType.Exclude<Deleted>());
            m_RoadVehicleQuery = GetEntityQuery(ComponentType.ReadOnly<CarCurrentLane>(),
                ComponentType.ReadOnly<Moving>(), ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());
        }

        protected override void OnUpdate()
        {
            CleanupLegacyRequests();
            if (TrafficStressState.ConsumeStopRequest())
                m_LastMultiplier = 1; // Restart the ramp even after Stop -> Start between ticks.
            TrafficStressState.SetRoadVehicles(m_RoadVehicleQuery.CalculateEntityCount());
            int multiplier = TrafficStressState.Multiplier;
            if (multiplier != m_LastMultiplier)
            {
                m_LastMultiplier = multiplier;
                m_RampBatches = 0;
                m_LogFirstBatch = multiplier > 1;
                Mod.Log.Info($"Traffic departure setting changed to {multiplier}x.");
            }
            if (multiplier <= 1 || !m_SpawnerSystem.Enabled)
            {
                TrafficStressState.PublishBatch(0, 0, false);
                return;
            }

            int maximum = Mod.Settings == null ? 96 : Mod.Settings.PerFrameRequestLimit;
            int ramp = Mod.Settings == null ? (int)RampSpeed.Fast : (int)Mod.Settings.TrafficRampSpeed;
            int budget = TrafficStressPolicy.BatchBudget(maximum, ramp, ++m_RampBatches);
            int generated = 0;
            int routes = 0;
            bool limited = false;
            using NativeArray<Entity> sources = m_SourceQuery.ToEntityArray(Allocator.Temp);
            for (int n = 0; n < sources.Length; n++)
            {
                Entity source = sources[(m_SourceCursor + n) % sources.Length];
                Entity prefab = EntityManager.GetComponentData<PrefabRef>(source).m_Prefab;
                if (!EntityManager.HasComponent<TrafficSpawnerData>(prefab) ||
                    (EntityManager.GetComponentData<TrafficSpawnerData>(prefab).m_RoadType & RoadTypes.Car) == 0)
                    continue;

                DynamicBuffer<ServiceDispatch> dispatches = EntityManager.GetBuffer<ServiceDispatch>(source);
                int originalCount = dispatches.Length;
                // Capture the original length so newly appended entries are never amplified.
                for (int i = 0; i < originalCount; i++)
                {
                    Entity request = dispatches[i].m_Request;
                    if (!IsApprovedRoadDispatch(source, request)) continue;
                    // A native buffer can already contain repeated entries (e.g. another mod).
                    bool duplicate = false;
                    for (int j = 0; j < i; j++)
                        if (dispatches[j].m_Request == request) { duplicate = true; break; }
                    if (duplicate) continue;
                    routes++;
                    int copies = TrafficStressPolicy.ExtraDepartures(multiplier, budget - generated);
                    limited |= copies < multiplier - 1;
                    for (int copy = 0; copy < copies; copy++)
                        dispatches.Add(new ServiceDispatch(request));
                    generated += copies;
                }
            }
            if (sources.Length > 0) m_SourceCursor = (m_SourceCursor + 1) % sources.Length;
            TrafficStressState.AddGenerated(generated);
            TrafficStressState.PublishBatch(routes, budget, limited);
            if (generated > 0 && m_LogFirstBatch)
            {
                Mod.Log.Info($"Queued {generated} extra departures from {routes} approved road route(s); batch cap {budget}. Native TrafficSpawnerAISystem creates the vehicles.");
                m_LogFirstBatch = false;
            }
        }

        private bool IsApprovedRoadDispatch(Entity source, Entity request)
        {
            if (!EntityManager.HasComponent<RandomTrafficRequest>(request) ||
                !EntityManager.HasComponent<ServiceRequest>(request) ||
                !EntityManager.HasComponent<Dispatched>(request) ||
                !EntityManager.HasComponent<PathInformation>(request) ||
                !EntityManager.HasBuffer<PathElement>(request) ||
                EntityManager.HasComponent<Deleted>(request)) return false;

            RandomTrafficRequest traffic = EntityManager.GetComponentData<RandomTrafficRequest>(request);
            PathInformation path = EntityManager.GetComponentData<PathInformation>(request);
            return (traffic.m_RoadType & RoadTypes.Car) != 0 && traffic.m_TrackType == TrackTypes.None &&
                EntityManager.HasComponent<TrafficSpawner>(traffic.m_Target) &&
                !EntityManager.HasComponent<Deleted>(traffic.m_Target) &&
                !EntityManager.HasComponent<Destroyed>(traffic.m_Target) &&
                EntityManager.GetComponentData<TrafficSpawner>(traffic.m_Target).m_TrafficRequest == request &&
                EntityManager.GetComponentData<Dispatched>(request).m_Handler == source &&
                path.m_Origin == source && EntityManager.HasComponent<PrefabRef>(path.m_Destination) &&
                !EntityManager.HasComponent<Deleted>(path.m_Destination) &&
                !EntityManager.HasComponent<Destroyed>(path.m_Destination) &&
                EntityManager.GetBuffer<PathElement>(request, true).Length > 0;
        }

        private void CleanupLegacyRequests()
        {
            if (m_LegacyCloneQuery.IsEmptyIgnoreFilter) return;
            int count = m_LegacyCloneQuery.CalculateEntityCount();
            EntityManager.DestroyEntity(m_LegacyCloneQuery);
            Mod.Log.Info($"Removed {count} legacy cloned traffic request(s).");
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            m_SourceCursor = 0;
            m_LastMultiplier = 1;
            m_RampBatches = 0;
            TrafficStressState.PrepareForNewCity(Mod.Settings == null || Mod.Settings.ResetWhenLeavingCity);
        }

        protected override void OnDestroy()
        {
            CleanupLegacyRequests();
            base.OnDestroy();
        }
    }

    internal struct TrafficStressClone : IComponentData
    {
        public uint Batch;
        public TrafficStressClone(uint batch) { Batch = batch; }
    }
}
