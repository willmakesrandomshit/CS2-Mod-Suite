using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;

namespace TrafficStressTester
{
    /// <summary>
    /// Multiplies the game's own transient random-road-traffic requests. The vanilla
    /// dispatcher still creates every vehicle, path and AI controller.
    /// </summary>
    [UpdateBefore(typeof(RandomTrafficDispatchSystem))]
    public sealed partial class TrafficStressSystem : GameSystemBase
    {
        private EntityQuery m_RequestQuery;
        private EntityQuery m_CloneQuery;
        private EntityQuery m_OrphanQuery;
        private readonly List<RandomTrafficRequest> m_RequestTemplates = new List<RandomTrafficRequest>(128);
        private uint m_Batch;
        private uint m_Tick;
        private int m_TemplateCursor;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_RequestQuery = GetEntityQuery(
                ComponentType.ReadOnly<RandomTrafficRequest>(),
                ComponentType.ReadOnly<ServiceRequest>(),
                ComponentType.Exclude<TrafficStressClone>(),
                ComponentType.Exclude<Deleted>());
            m_CloneQuery = GetEntityQuery(
                ComponentType.ReadOnly<TrafficStressClone>(),
                ComponentType.Exclude<Deleted>());
            // v1.2.0 pulse requests were missing ServiceRequest/RequestGroup, so the
            // vanilla dispatcher could never consume them. Remove that old shape as
            // soon as a world is opened, even if the multiplier is currently 1x.
            m_OrphanQuery = GetEntityQuery(
                ComponentType.ReadOnly<TrafficStressClone>(),
                ComponentType.ReadOnly<RandomTrafficRequest>(),
                ComponentType.Exclude<ServiceRequest>(),
                ComponentType.Exclude<Deleted>());
        }

        protected override void OnUpdate()
        {
            if (TrafficStressState.ConsumeStopRequest())
            {
                CancelOutstandingRequests();
            }
            CleanupLegacyOrphans();

            int multiplier = TrafficStressState.Multiplier;
            if (multiplier <= 1)
                return;

            using EntityCommandBuffer buffer = new EntityCommandBuffer(Allocator.Temp);
            int generated = 0;
            int limit = Mod.Settings == null ? 96 : Mod.Settings.PerFrameRequestLimit;
            limit = System.Math.Max(1, System.Math.Min(512, limit));
            // Keep submitted work bounded if pathfinding is saturated. This preserves
            // strong traffic pressure without allowing an ever-growing request queue.
            // Gridlock deliberately allows a deeper queue so highways can saturate,
            // while retaining a hard ceiling when the dispatcher cannot keep up.
            int outstandingLimit = limit * (multiplier >= 25 ? 16 : 8);
            int outstanding = m_CloneQuery.CalculateEntityCount();
            limit = System.Math.Min(limit, System.Math.Max(0, outstandingLimit - outstanding));
            if (limit == 0)
            {
                return;
            }
            uint batch = ++m_Batch;
            if (!m_RequestQuery.IsEmptyIgnoreFilter)
            {
                // Learn valid request shapes from vanilla and multiply the live batch.
                using NativeArray<Entity> requests = m_RequestQuery.ToEntityArray(Allocator.Temp);
                using NativeArray<RandomTrafficRequest> data = m_RequestQuery.ToComponentDataArray<RandomTrafficRequest>(Allocator.Temp);
                for (int i = 0; i < requests.Length; i++)
                {
                    RememberTemplate(data[i]);
                    for (byte copy = 1; copy < multiplier && generated < limit; copy++)
                    {
                        Entity clone = buffer.Instantiate(requests[i]);
                        buffer.AddComponent(clone, new TrafficStressClone { Batch = batch });
                        generated++;
                    }
                }
            }

            // Vanilla random requests can be sparse. Sustain pressure between them so
            // higher settings visibly load highways rather than only amplifying rare bursts.
            int pulseInterval = GetPulseInterval();
            if (multiplier >= 25) pulseInterval = System.Math.Max(2, pulseInterval / 2);
            if (++m_Tick % pulseInterval == 0 && m_RequestTemplates.Count > 0)
            {
                int extra = multiplier - 1;
                for (int i = 0; i < extra && generated < limit; i++)
                {
                    if (!TryNextValidTemplate(out RandomTrafficRequest template)) break;
                    Entity request = buffer.CreateEntity();
                    // Match TrafficSpawnerAISystem's request archetype. RequestGroup is
                    // converted by ServiceRequestSystem into a scheduled UpdateFrame;
                    // ServiceRequest then owns retry, dispatch and destruction lifecycle.
                    buffer.AddComponent(request, new ServiceRequest(false));
                    buffer.AddComponent(request, template);
                    buffer.AddComponent(request, new RequestGroup(16u));
                    buffer.AddComponent(request, new TrafficStressClone { Batch = batch });
                    generated++;
                }
            }

            buffer.Playback(EntityManager);
            if (generated > 0) TrafficStressState.AddGenerated(generated);
        }

        private void CancelOutstandingRequests()
        {
            if (m_CloneQuery.IsEmptyIgnoreFilter) return;
            int count = m_CloneQuery.CalculateEntityCount();
            EntityManager.DestroyEntity(m_CloneQuery);
            Mod.Log.Info($"Stop / Reset cancelled {count} queued traffic stress request(s). Existing vehicles continue normally.");
        }

        private void CleanupLegacyOrphans()
        {
            if (m_OrphanQuery.IsEmptyIgnoreFilter)
                return;

            int count = m_OrphanQuery.CalculateEntityCount();
            EntityManager.DestroyEntity(m_OrphanQuery);
            Mod.Log.Info($"Removed {count} legacy unscheduled traffic stress request(s).");
        }

        private void RememberTemplate(RandomTrafficRequest request)
        {
            if (request.m_Target != Entity.Null && !EntityManager.Exists(request.m_Target))
                return;
            if (m_RequestTemplates.Count < 128) m_RequestTemplates.Add(request);
            else m_RequestTemplates[m_TemplateCursor++ % m_RequestTemplates.Count] = request;
        }

        private static int GetPulseInterval()
        {
            if (Mod.Settings == null) return 12;
            switch (Mod.Settings.TrafficRampSpeed) { case RampSpeed.Gentle: return 24; case RampSpeed.Extreme: return 4; default: return 10; }
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            m_RequestTemplates.Clear();
            m_Batch = 0;
            m_Tick = 0;
            m_TemplateCursor = 0;
            TrafficStressState.PrepareForNewCity(Mod.Settings == null || Mod.Settings.ResetWhenLeavingCity);
        }

        private bool TryNextValidTemplate(out RandomTrafficRequest result)
        {
            for (int i = 0; i < m_RequestTemplates.Count; i++)
            {
                RandomTrafficRequest request = m_RequestTemplates[m_TemplateCursor++ % m_RequestTemplates.Count];
                if (request.m_Target == Entity.Null || (EntityManager.Exists(request.m_Target) && !EntityManager.HasComponent<Deleted>(request.m_Target)))
                { result = request; return true; }
            }
            m_RequestTemplates.Clear();
            result = default;
            return false;
        }

        protected override void OnDestroy()
        {
            // Requests normally live for only a dispatcher tick. Remove any that remain
            // when a world is torn down so no transient stress state reaches a save.
            if (!m_CloneQuery.IsEmptyIgnoreFilter)
                EntityManager.DestroyEntity(m_CloneQuery);
            m_RequestTemplates.Clear();
            base.OnDestroy();
        }
    }

    internal struct TrafficStressClone : IComponentData
    {
        public uint Batch;
    }
}
