using System;
using System.Collections.Generic;
using Colossal.Logging;
using Game.Common;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;

namespace MarkingStudio
{
    public struct ProceduralOptions
    {
        public bool GenerateStopBars;
        public bool GenerateCrosswalks;
        public bool GenerateTurnGuides;
        public bool GenerateMedianIslands;
        public float StopBarOffsetMeters;
        public int TurnGuideStyle; // 1 = Dashed
    }

    /// <summary>
    /// Magic Marking 2.1 Procedural Junction Marking Generator.
    /// Analyzes junction geometry and emits standard, fully-editable Magic Marking
    /// lines and polygonal areas.
    /// </summary>
    public static class ProceduralJunctionGenerator
    {
        private static readonly ILog log = Mod.log;

        public static int GenerateJunctionMarkings(
            EntityManager em,
            Entity node,
            IReadOnlyList<MarkingEndpoint> endpoints,
            IReadOnlyList<MarkingCornerAnchor> corners,
            ProceduralOptions options)
        {
            if (node == Entity.Null || !em.Exists(node) || endpoints == null || endpoints.Count < 2)
            {
                return 0;
            }

            var preview = TopologyAwareAutoGenerator.AnalyzeAndGenerateCandidates(em, node, endpoints, corners);
            int countAdded = TopologyAwareAutoGenerator.ApplyGeneratedMarkings(em, node, preview);

            if (countAdded > 0)
            {
                em.AddComponent<Updated>(node);
                log.Info($"ProceduralJunctionGenerator: emitted {countAdded} topology-aware marking primitives on Node #{node.Index}.");
            }

            return countAdded;
        }
    }
}
