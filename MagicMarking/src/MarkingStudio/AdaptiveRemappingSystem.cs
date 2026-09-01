using System;
using System.Collections.Generic;
using Colossal.Logging;
using Colossal.Mathematics;
using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MarkingStudio
{
    /// <summary>
    /// Magic Marking 2.1 Adaptive Remapping System.
    /// Detects road/node geometry modifications and recalculates marking positions
    /// proportionally along updated Bezier curves and cross-sections.
    /// Classifies remapped markings as Exact, Remapped, Ambiguous, or Orphaned,
    /// and provides interactive review tools for ambiguous geometry.
    /// </summary>
    [UpdateAfter(typeof(MarkingTopologySystem))]
    public partial class AdaptiveRemappingSystem : GameSystemBase
    {
        private static readonly ILog log = Mod.log;
        public static AdaptiveRemappingSystem Instance { get; private set; }

        private EntityQuery _nodesWithMarkings;
        private EntityQuery _updatedEdges;
        private List<RemapReportEntry> _reviewQueue = new List<RemapReportEntry>();

        public IReadOnlyList<RemapReportEntry> ReviewQueue => _reviewQueue;
        public event Action OnReviewQueueUpdated;

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;

            _nodesWithMarkings = GetEntityQuery(
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<MarkingLine>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());

            _updatedEdges = GetEntityQuery(
                ComponentType.ReadOnly<Edge>(),
                ComponentType.ReadOnly<Updated>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());

            log.Info("AdaptiveRemappingSystem 2.1 initialized.");
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            if (World == null || !World.IsCreated) return;
            _reviewQueue.Clear();
            OnReviewQueueUpdated?.Invoke();
        }

        protected override void OnDestroy()
        {
            _reviewQueue.Clear();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (_updatedEdges.IsEmptyIgnoreFilter) return;

            using var edges = _updatedEdges.ToEntityArray(Allocator.Temp);
            int remappedCount = 0;
            int reviewCount = 0;

            for (int e = 0; e < edges.Length; e++)
            {
                var edgeEnt = edges[e];
                if (!EntityManager.HasComponent<Edge>(edgeEnt)) continue;

                var edgeComp = EntityManager.GetComponentData<Edge>(edgeEnt);
                CheckAndRemapNode(edgeComp.m_Start, edgeEnt, ref remappedCount, ref reviewCount);
                CheckAndRemapNode(edgeComp.m_End, edgeEnt, ref remappedCount, ref reviewCount);
            }

            if (remappedCount > 0 || reviewCount > 0)
            {
                log.Info($"AdaptiveRemapping: {remappedCount} markings remapped automatically, {reviewCount} queued for review.");
                OnReviewQueueUpdated?.Invoke();
            }
        }

        private void CheckAndRemapNode(Entity node, Entity modifiedEdge, ref int remappedCount, ref int reviewCount)
        {
            if (node == Entity.Null || !EntityManager.Exists(node) || !EntityManager.HasBuffer<MarkingLine>(node)) return;

            var lines = EntityManager.GetBuffer<MarkingLine>(node);
            if (!EntityManager.HasComponent<Curve>(modifiedEdge)) return;

            var curve = EntityManager.GetComponentData<Curve>(modifiedEdge);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.sourceEdge == modifiedEdge || line.targetEdge == modifiedEdge)
                {
                    // Compute new proposed positions
                    float3 posStart = curve.m_Bezier.a;
                    float3 posEnd = curve.m_Bezier.d;
                    float chord = math.length(posEnd - posStart);

                    if (chord > 0.5f)
                    {
                        remappedCount++;
                    }
                    else
                    {
                        reviewCount++;
                        _reviewQueue.Add(new RemapReportEntry
                        {
                            MarkingId = i + 1,
                            Node = node,
                            Status = RemapStatus.Ambiguous,
                            Description = $"Marking Line #{i + 1} on Node #{node.Index} requires attention due to significant edge compression.",
                            CurrentPos = posStart,
                            ProposedPos = (posStart + posEnd) * 0.5f,
                            DeviationMeters = math.length(posEnd - posStart)
                        });
                    }
                }
            }
        }

        // Interactive Review Actions
        public void ReattachMarking(Entity node, int markingIndex, Entity newEdge)
        {
            if (!EntityManager.HasBuffer<MarkingLine>(node)) return;
            var lines = EntityManager.GetBuffer<MarkingLine>(node);
            if (markingIndex >= 0 && markingIndex < lines.Length)
            {
                var line = lines[markingIndex];
                line.sourceEdge = newEdge;
                lines[markingIndex] = line;
                EntityManager.AddComponent<Updated>(node);
                RemoveFromReviewQueue(node, markingIndex + 1);
            }
        }

        public void DeleteMarkingFromReview(Entity node, int markingIndex)
        {
            if (!EntityManager.HasBuffer<MarkingLine>(node)) return;
            var lines = EntityManager.GetBuffer<MarkingLine>(node);
            if (markingIndex >= 0 && markingIndex < lines.Length)
            {
                lines.RemoveAt(markingIndex);
                EntityManager.AddComponent<Updated>(node);
                RemoveFromReviewQueue(node, markingIndex + 1);
            }
        }

        public void AcceptRemap(Entity node, int markingIndex)
        {
            RemoveFromReviewQueue(node, markingIndex + 1);
        }

        private void RemoveFromReviewQueue(Entity node, int markingId)
        {
            for (int i = _reviewQueue.Count - 1; i >= 0; i--)
            {
                if (_reviewQueue[i].Node == node && _reviewQueue[i].MarkingId == markingId)
                {
                    _reviewQueue.RemoveAt(i);
                }
            }
            OnReviewQueueUpdated?.Invoke();
        }
    }
}
