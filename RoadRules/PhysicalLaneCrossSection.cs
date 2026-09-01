using System;
using System.Collections.Generic;
using System.Linq;

namespace RoadRules
{
    /// <summary>
    /// Pure cross-section geometry helpers shared by runtime lane discovery and its local
    /// regression harness. No ECS state or simulation behavior is changed here.
    /// </summary>
    internal static class PhysicalLaneCrossSection
    {
        internal static float GetMergeTolerance(float candidateWidth, float clusterWidth)
        {
            candidateWidth = candidateWidth > 0.1f ? candidateWidth : 3.5f;
            clusterWidth = clusterWidth > 0.1f ? clusterWidth : candidateWidth;
            return Math.Max(0.35f, Math.Min(0.90f, Math.Min(candidateWidth, clusterWidth) * 0.22f));
        }

        internal static bool SameCenterline(float candidateOffset, float clusterOffset, float candidateWidth, float clusterWidth)
        {
            return Math.Abs(candidateOffset - clusterOffset) <= GetMergeTolerance(candidateWidth, clusterWidth);
        }

        private readonly struct SyntheticCandidate
        {
            internal readonly float Offset;
            internal readonly float Width;
            internal readonly bool IsMaster;
            internal readonly bool IsFullLane;

            internal SyntheticCandidate(float offset, float width = 4f, bool isMaster = false, bool isFullLane = true)
            {
                Offset = offset;
                Width = width;
                IsMaster = isMaster;
                IsFullLane = isFullLane;
            }
        }

        private static int CountPhysicalLanes(params SyntheticCandidate[] candidates)
        {
            List<SyntheticCandidate> seeds = candidates.Where(x => !x.IsMaster && x.IsFullLane).ToList();
            if (seeds.Count == 0)
                seeds = candidates.Where(x => !x.IsMaster).ToList();
            if (seeds.Count == 0)
                seeds = candidates.ToList();

            var clusters = new List<List<SyntheticCandidate>>();
            foreach (SyntheticCandidate candidate in seeds)
            {
                bool merged = false;
                foreach (List<SyntheticCandidate> cluster in clusters)
                {
                    float averageOffset = cluster.Average(x => x.Offset);
                    float averageWidth = cluster.Average(x => x.Width);
                    if (!SameCenterline(candidate.Offset, averageOffset, candidate.Width, averageWidth))
                        continue;
                    cluster.Add(candidate);
                    merged = true;
                    break;
                }
                if (!merged)
                    clusters.Add(new List<SyntheticCandidate> { candidate });
            }
            return clusters.Count;
        }

        internal static string RunRegressionHarness()
        {
            var cases = new Dictionary<string, (int Expected, SyntheticCandidate[] Candidates)>
            {
                ["real one-lane carriageway"] = (1, new[] { new SyntheticCandidate(0f) }),
                ["exact runtime regression: master + two full highway lanes"] = (2, new[]
                {
                    new SyntheticCandidate(4f, isMaster: true, isFullLane: false),
                    new SyntheticCandidate(2f),
                    new SyntheticCandidate(6f)
                }),
                ["real three-lane carriageway"] = (3, new[]
                {
                    new SyntheticCandidate(-4f), new SyntheticCandidate(0f), new SyntheticCandidate(4f)
                }),
                ["real four-lane/highway carriageway"] = (4, new[]
                {
                    new SyntheticCandidate(-6f), new SyntheticCandidate(-2f),
                    new SyntheticCandidate(2f), new SyntheticCandidate(6f)
                }),
                ["duplicate routing representations"] = (2, new[]
                {
                    new SyntheticCandidate(-2.00f), new SyntheticCandidate(-2.10f),
                    new SyntheticCandidate(2.00f), new SyntheticCandidate(2.08f)
                }),
                ["ramp"] = (1, new[] { new SyntheticCandidate(1.75f) }),
                ["merge/diverge partial helper ignored"] = (2, new[]
                {
                    new SyntheticCandidate(-2f), new SyntheticCandidate(2f),
                    new SyntheticCandidate(5.5f, isFullLane: false)
                }),
                ["bridge"] = (2, new[] { new SyntheticCandidate(-2f), new SyntheticCandidate(2f) }),
                ["asymmetric forward carriageway"] = (3, new[]
                {
                    new SyntheticCandidate(1.5f), new SyntheticCandidate(5.5f), new SyntheticCandidate(9.5f)
                }),
                ["asymmetric reverse carriageway"] = (2, new[]
                {
                    new SyntheticCandidate(-1.5f), new SyntheticCandidate(-5.5f)
                })
            };

            var failures = new List<string>();
            foreach (var pair in cases)
            {
                int actual = CountPhysicalLanes(pair.Value.Candidates);
                if (actual != pair.Value.Expected)
                    failures.Add($"{pair.Key}: expected {pair.Value.Expected}, got {actual}");
            }
            return failures.Count == 0
                ? $"PASS {cases.Count}/{cases.Count}"
                : "FAIL " + string.Join("; ", failures);
        }
    }
}
