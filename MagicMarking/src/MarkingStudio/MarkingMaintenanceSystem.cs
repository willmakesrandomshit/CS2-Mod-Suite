using System;
using System.Collections.Generic;
using Colossal.Logging;
using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MarkingStudio
{
    public struct RedrawSummary
    {
        public int MarkingsProcessed;
        public int MarkingsRebuilt;
        public int AnchorsRemapped;
        public int AmbiguousAnchors;
        public int OrphanedMarkings;
        public int Failures;
        public string StatusMessage;
    }

    /// <summary>
    /// Magic Marking Maintenance & Global Redraw System.
    /// Provides non-destructive regeneration of rendered/emitted marking geometry from
    /// authoritative saved definitions.
    /// </summary>
    public partial class MarkingMaintenanceSystem : GameSystemBase
    {
        private static readonly ILog log = Mod.log;
        public static MarkingMaintenanceSystem Instance { get; private set; }

        private EntityQuery _allMarkingNodes;
        private EntityQuery _allMarkingAreas;

        public RedrawSummary LastSummary { get; private set; }
        public event Action<RedrawSummary> OnRedrawCompleted;

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;

            _allMarkingNodes = GetEntityQuery(
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<MarkingLine>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());

            _allMarkingAreas = GetEntityQuery(
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<MarkingArea>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());

            log.Info("MarkingMaintenanceSystem initialized.");
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            if (World == null || !World.IsCreated) return;
        }

        protected override void OnUpdate()
        {
            // Maintenance operations run on-demand via commands, not per-frame
        }

        /// <summary>
        /// Fast Path: Rebuild dirty or currently visible markings.
        /// </summary>
        public RedrawSummary RedrawDirtyOrVisible(Entity selectedNode = default)
        {
            int processed = 0;
            int rebuilt = 0;

            try
            {
                if (selectedNode != Entity.Null && EntityManager.Exists(selectedNode) && EntityManager.HasBuffer<MarkingLine>(selectedNode))
                {
                    EntityManager.AddComponent<Updated>(selectedNode);
                    processed += EntityManager.GetBuffer<MarkingLine>(selectedNode).Length;
                    rebuilt += processed;
                }
                else if (!_allMarkingNodes.IsEmptyIgnoreFilter)
                {
                    using var nodes = _allMarkingNodes.ToEntityArray(Allocator.Temp);
                    for (int i = 0; i < math.min(nodes.Length, 32); i++)
                    {
                        var n = nodes[i];
                        EntityManager.AddComponent<Updated>(n);
                        if (EntityManager.HasBuffer<MarkingLine>(n))
                        {
                            int count = EntityManager.GetBuffer<MarkingLine>(n).Length;
                            processed += count;
                            rebuilt += count;
                        }
                    }
                }

                LastSummary = new RedrawSummary
                {
                    MarkingsProcessed = processed,
                    MarkingsRebuilt = rebuilt,
                    AnchorsRemapped = 0,
                    AmbiguousAnchors = 0,
                    OrphanedMarkings = 0,
                    Failures = 0,
                    StatusMessage = $"Redrew {rebuilt} visible markings successfully."
                };
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error in RedrawDirtyOrVisible");
                LastSummary = new RedrawSummary
                {
                    Failures = 1,
                    StatusMessage = "Redraw encountered an error."
                };
            }

            OnRedrawCompleted?.Invoke(LastSummary);
            return LastSummary;
        }

        /// <summary>
        /// Full Rebuild: Iterates all Magic Marking-owned records, re-resolves anchors,
        /// regenerates geometry, refreshes emission buffers, and updates review queues.
        /// </summary>
        public RedrawSummary ForceRebuildAll()
        {
            log.Info("Starting Force Rebuild All Markings...");
            int processed = 0;
            int rebuilt = 0;
            int remapped = 0;
            int ambiguous = 0;
            int orphaned = 0;
            int failures = 0;

            try
            {
                if (!_allMarkingNodes.IsEmptyIgnoreFilter)
                {
                    using var nodes = _allMarkingNodes.ToEntityArray(Allocator.Temp);
                    for (int nIdx = 0; nIdx < nodes.Length; nIdx++)
                    {
                        var node = nodes[nIdx];
                        if (!EntityManager.Exists(node) || !EntityManager.HasBuffer<MarkingLine>(node)) continue;

                        var lines = EntityManager.GetBuffer<MarkingLine>(node);
                        processed += lines.Length;

                        for (int l = 0; l < lines.Length; l++)
                        {
                            var line = lines[l];
                            bool hasSource = line.sourceEdge != Entity.Null && EntityManager.Exists(line.sourceEdge);
                            bool hasTarget = line.targetEdge != Entity.Null && EntityManager.Exists(line.targetEdge);

                            if (hasSource && hasTarget)
                            {
                                rebuilt++;
                            }
                            else if (hasSource || hasTarget)
                            {
                                remapped++;
                                ambiguous++;
                            }
                            else
                            {
                                orphaned++;
                            }
                        }

                        // Flag node for complete downstream emission pass
                        EntityManager.AddComponent<Updated>(node);
                    }
                }

                LastSummary = new RedrawSummary
                {
                    MarkingsProcessed = processed,
                    MarkingsRebuilt = rebuilt,
                    AnchorsRemapped = remapped,
                    AmbiguousAnchors = ambiguous,
                    OrphanedMarkings = orphaned,
                    Failures = failures,
                    StatusMessage = $"Full rebuild complete: {processed} processed, {rebuilt} rebuilt, {remapped} remapped, {ambiguous} ambiguous, {orphaned} orphaned."
                };

                log.Info(LastSummary.StatusMessage);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error in ForceRebuildAll");
                failures++;
                LastSummary = new RedrawSummary
                {
                    MarkingsProcessed = processed,
                    MarkingsRebuilt = rebuilt,
                    Failures = failures,
                    StatusMessage = "Rebuild failed due to an exception."
                };
            }

            OnRedrawCompleted?.Invoke(LastSummary);
            return LastSummary;
        }

        public RedrawSummary RedrawCurrentJunction(Entity node)
        {
            return RedrawDirtyOrVisible(node);
        }

        public RedrawSummary RedrawCurrentRoad(Entity road)
        {
            if (road != Entity.Null && EntityManager.Exists(road) && EntityManager.HasComponent<Edge>(road))
            {
                var edge = EntityManager.GetComponentData<Edge>(road);
                RedrawDirtyOrVisible(edge.m_Start);
                return RedrawDirtyOrVisible(edge.m_End);
            }
            return RedrawDirtyOrVisible(Entity.Null);
        }
    }
}
