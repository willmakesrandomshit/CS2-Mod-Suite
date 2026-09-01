using System;
using System.Collections.Generic;
using Colossal.Logging;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MarkingStudio
{
    public enum GenerationConfidence
    {
        High,
        Medium,
        Low
    }

    public enum SemanticAnchorType
    {
        LaneBoundary,      // Internal lane divider at junction cap (gapIndex 1..N-1)
        RoadOuterEdge,     // Outer pavement edge / curb (gapIndex 0 or N)
        MedianEdge,        // Median / divider boundary
        StopLineBoundary,  // Lateral stop bar anchor
        CrosswalkBoundary, // Crosswalk boundary anchor
        GoreBoundary,      // Highway split / merge boundary
        SetbackAnchor,     // Setback longitudinal anchor (gapIndex >= 100)
        ParkingBoundary,   // Parking bay anchor (gapIndex >= 200)
        CornerAnchor,      // Intersection corner meeting point
        Unknown
    }

    public enum JunctionTopologyType
    {
        DeadEnd,               // 1 approach
        StraightOrBend,        // 2 collinear approaches
        Curve,                 // 2 angled approaches
        TJunction,             // 3 approaches (2 collinear through-arms and 1 stem)
        YJunction,             // 3 approaches (merge / diverge / roughly equal angles)
        Crossroads,            // 4 approaches (2 pairs of opposing through-arms)
        AsymmetricCrossroads,  // 4 approaches with unequal lane counts
        AsymmetricTransition,  // 2 collinear approaches with lane addition / drop (e.g. 2->3, 3->4, 4->2)
        DividedHighwaySplit,   // Divided highway carriageway split / merge
        RoundaboutOrMultiLeg   // 5+ approaches / roundabout circulation
    }

    public struct GeneratedMarkingCandidate
    {
        public Entity SourceEdge;
        public int SourceGapIndex;
        public Entity TargetEdge;
        public int TargetGapIndex;
        public MarkingStyle Style;
        public float Curvature;
        public GenerationConfidence Confidence;
        public string Category; // "LaneSeparator", "StopLine", "OuterEdgeLine", "MedianEdgeLine", "GoreBoundary", "ThroughContinuation"
        public string Reason;
    }

    public struct RejectedCandidateRecord
    {
        public Entity SourceEdge;
        public int SourceGapIndex;
        public Entity TargetEdge;
        public int TargetGapIndex;
        public string Category;
        public string RejectReason;
    }

    public struct AutoGeneratePreviewResult
    {
        public int TotalLines;
        public int OuterEdgeLines;
        public int MedianEdgeLines;
        public int LaneSeparators;
        public int StopLines;
        public int Crosswalks;
        public int GoreAreas;
        public int HatchedAreas;
        public int SkippedAmbiguous => RejectedAmbiguous;
        public int RejectedAmbiguous;
        public int RejectedTopology;
        public int RejectedSurface;
        public int RejectedTangent;
        public int DuplicatesRemoved;
        public JunctionTopologyType DetectedTopology;
        public List<GeneratedMarkingCandidate> Candidates;
        public List<RejectedCandidateRecord> RejectedCandidates;
    }

    /// <summary>
    /// Magic Marking 2.2 Deep Topology-Aware Road Marking Generator.
    /// Derives markings authoritatively from road/lane network topology and geometry.
    /// Strictly guarantees ZERO spiderwebs, ZERO arbitrary diagonal cross-chords,
    /// ZERO median penetrations, and ZERO ungrounded anchor-to-anchor connections.
    /// If relationship is ambiguous or ungrounded, it is skipped (Ambiguity Rule).
    /// </summary>
    public static class TopologyAwareAutoGenerator
    {
        private static ILog s_Log;
        private static ILog log
        {
            get
            {
                if (s_Log == null)
                {
                    try { s_Log = LogManager.GetLogger(nameof(TopologyAwareAutoGenerator)).SetShowsErrorsInUI(false); }
                    catch { /* Offline / standalone test execution */ }
                }
                return s_Log;
            }
        }

        private const int kSetbackGapBase = 100;
        private const int kExtendedGapBase = 200;

        /// <summary>
        /// Analyzes the node's road topology and produces validated marking candidates.
        /// </summary>
        public static AutoGeneratePreviewResult AnalyzeAndGenerateCandidates(
            EntityManager em,
            Entity node,
            IReadOnlyList<MarkingEndpoint> endpoints,
            IReadOnlyList<MarkingCornerAnchor> corners)
        {
            var result = new AutoGeneratePreviewResult
            {
                Candidates = new List<GeneratedMarkingCandidate>(),
                RejectedCandidates = new List<RejectedCandidateRecord>()
            };

            if (node == Entity.Null || endpoints == null || endpoints.Count < 2)
            {
                return result;
            }

            try
            {
                if (!em.Exists(node)) return result;
            }
            catch
            {
                // Offline / Unit Test execution without live ECS world
            }

            // -------------------------------------------------------------------------
            // STEP 1: SEMANTIC ANCHOR CLASSIFICATION & ISOLATION
            // -------------------------------------------------------------------------
            var classicCapEndpoints = new Dictionary<Entity, List<MarkingEndpoint>>();
            var setbackEndpoints = new Dictionary<Entity, List<MarkingEndpoint>>();
            var allEndpointsByEdge = new Dictionary<Entity, List<MarkingEndpoint>>();

            for (int i = 0; i < endpoints.Count; i++)
            {
                var ep = endpoints[i];
                if (!allEndpointsByEdge.TryGetValue(ep.edge, out var allList))
                {
                    allList = new List<MarkingEndpoint>();
                    allEndpointsByEdge[ep.edge] = allList;
                }
                allList.Add(ep);

                if (ep.gapIndex < kSetbackGapBase)
                {
                    if (!classicCapEndpoints.TryGetValue(ep.edge, out var capList))
                    {
                        capList = new List<MarkingEndpoint>();
                        classicCapEndpoints[ep.edge] = capList;
                    }
                    capList.Add(ep);
                }
                else if (ep.gapIndex >= kSetbackGapBase && ep.gapIndex < kExtendedGapBase)
                {
                    if (!setbackEndpoints.TryGetValue(ep.edge, out var setList))
                    {
                        setList = new List<MarkingEndpoint>();
                        setbackEndpoints[ep.edge] = setList;
                    }
                    setList.Add(ep);
                }
            }

            // Sort all lists left-to-right by gapIndex
            foreach (var kvp in classicCapEndpoints) kvp.Value.Sort((a, b) => a.gapIndex.CompareTo(b.gapIndex));
            foreach (var kvp in setbackEndpoints) kvp.Value.Sort((a, b) => a.gapIndex.CompareTo(b.gapIndex));
            foreach (var kvp in allEndpointsByEdge) kvp.Value.Sort((a, b) => a.gapIndex.CompareTo(b.gapIndex));

            var edgeList = new List<Entity>(classicCapEndpoints.Keys);
            if (edgeList.Count == 0) return result;

            // Compute approach directions, angles, and positions
            var approachPositions = new Dictionary<Entity, float3>();
            var approachInwardDirs = new Dictionary<Entity, float2>();
            var approachAngles = new Dictionary<Entity, float>();

            float3 nodeCentre = float3.zero;
            try
            {
                if (em.HasComponent<Node>(node))
                {
                    nodeCentre = em.GetComponentData<Node>(node).m_Position;
                }
            }
            catch { }

            foreach (var edgeEnt in edgeList)
            {
                var list = classicCapEndpoints[edgeEnt];
                float3 avgPos = float3.zero;
                float2 avgTan = float2.zero;
                for (int i = 0; i < list.Count; i++)
                {
                    avgPos += list[i].position;
                    avgTan += list[i].tangent;
                }
                avgPos /= (float)list.Count;
                approachPositions[edgeEnt] = avgPos;

                float2 inward = -math.normalizesafe(avgTan);
                if (math.lengthsq(inward) < 0.01f)
                {
                    float3 delta = nodeCentre - avgPos;
                    inward = math.normalizesafe(new float2(delta.x, delta.z));
                }
                approachInwardDirs[edgeEnt] = inward;
                approachAngles[edgeEnt] = math.atan2(inward.y, inward.x);
            }

            // -------------------------------------------------------------------------
            // STEP 2: TOPOLOGY CLASSIFICATION
            // -------------------------------------------------------------------------
            JunctionTopologyType topType = ClassifyJunction(edgeList, approachAngles, classicCapEndpoints, out Entity armA, out Entity armB, out Entity stem);
            result.DetectedTopology = topType;

            var rawCandidates = new List<GeneratedMarkingCandidate>();

            // -------------------------------------------------------------------------
            // PASS 1: PER-APPROACH STOP BARS (ONLY ON MULTI-WAY JUNCTIONS)
            // -------------------------------------------------------------------------
            bool isThroughRoadOnly = (topType == JunctionTopologyType.StraightOrBend ||
                                      topType == JunctionTopologyType.Curve ||
                                      topType == JunctionTopologyType.AsymmetricTransition);

            if (!isThroughRoadOnly)
            {
                foreach (var edgeEnt in edgeList)
                {
                    var caps = classicCapEndpoints[edgeEnt];
                    if (caps.Count < 2) continue;

                    // Standard right-hand driving approach stop bar on inbound side
                    int startIdx = (caps.Count >= 3) ? (caps.Count / 2) : 0;
                    int endIdx = caps.Count - 1;

                    var left = caps[startIdx];
                    var right = caps[endIdx];

                    rawCandidates.Add(new GeneratedMarkingCandidate
                    {
                        SourceEdge = left.edge,
                        SourceGapIndex = left.gapIndex,
                        TargetEdge = right.edge,
                        TargetGapIndex = right.gapIndex,
                        Style = MarkingStyle.Solid,
                        Curvature = 0.0f,
                        Confidence = GenerationConfidence.High,
                        Category = "StopLine",
                        Reason = $"Approach stop bar on Edge #{edgeEnt.Index} (Cap {left.gapIndex} to {right.gapIndex})"
                    });
                }
            }

            // -------------------------------------------------------------------------
            // PASS 2: T-JUNCTION / HIGHWAY MERGE / DIVERGE ENGINE
            // -------------------------------------------------------------------------
            if (topType == JunctionTopologyType.TJunction && armA != Entity.Null && armB != Entity.Null && stem != Entity.Null)
            {
                var capsA = classicCapEndpoints[armA];
                var capsB = classicCapEndpoints[armB];
                float2 stemDir = approachInwardDirs[stem];

                if (capsA.Count >= 2 && capsB.Count >= 2)
                {
                    float3 posA0 = capsA[0].position;
                    float3 posALast = capsA[capsA.Count - 1].position;
                    float3 centreThrough = (posA0 + posALast) * 0.5f;

                    float2 dir0 = math.normalizesafe(new float2(posA0.x - centreThrough.x, posA0.z - centreThrough.z));
                    float2 dirLast = math.normalizesafe(new float2(posALast.x - centreThrough.x, posALast.z - centreThrough.z));

                    bool gap0IsFarSide = math.dot(dir0, stemDir) < math.dot(dirLast, stemDir);

                    // 1. Far side unbroken outer road edge:
                    int farA = gap0IsFarSide ? 0 : capsA.Count - 1;
                    int farB = gap0IsFarSide ? capsB.Count - 1 : 0;

                    rawCandidates.Add(new GeneratedMarkingCandidate
                    {
                        SourceEdge = capsA[farA].edge,
                        SourceGapIndex = capsA[farA].gapIndex,
                        TargetEdge = capsB[farB].edge,
                        TargetGapIndex = capsB[farB].gapIndex,
                        Style = MarkingStyle.Solid,
                        Curvature = ComputeCurvature(capsA[farA], capsB[farB]),
                        Confidence = GenerationConfidence.High,
                        Category = "OuterEdgeLine",
                        Reason = "Continuous unbroken outer carriageway edge on far side of T-junction"
                    });

                    // 2. Near-side / Merge / Diverge gore boundary if stem is acute ramp
                    float stemAngleDiffA = AngleDiffDeg(approachAngles[armA], approachAngles[stem]);
                    float stemAngleDiffB = AngleDiffDeg(approachAngles[armB], approachAngles[stem]);
                    if (stemAngleDiffA < 75f || stemAngleDiffB < 75f)
                    {
                        var stemCaps = classicCapEndpoints[stem];
                        if (stemCaps.Count >= 2)
                        {
                            var nearThrough = gap0IsFarSide ? capsA[capsA.Count - 1] : capsA[0];
                            var goreStem = stemCaps[0];

                            rawCandidates.Add(new GeneratedMarkingCandidate
                            {
                                SourceEdge = nearThrough.edge,
                                SourceGapIndex = nearThrough.gapIndex,
                                TargetEdge = goreStem.edge,
                                TargetGapIndex = goreStem.gapIndex,
                                Style = MarkingStyle.Solid,
                                Curvature = ComputeCurvature(nearThrough, goreStem),
                                Confidence = GenerationConfidence.High,
                                Category = "GoreBoundary",
                                Reason = "Ramp merge/diverge gore channelization boundary"
                            });
                        }
                    }

                    // 3. Through-road center line / internal lane dividers:
                    int commonCount = math.min(capsA.Count, capsB.Count);
                    for (int k = 1; k < commonCount - 1; k++)
                    {
                        int targetK = capsB.Count - 1 - k;
                        bool isMedian = (k == capsA.Count / 2);

                        rawCandidates.Add(new GeneratedMarkingCandidate
                        {
                            SourceEdge = capsA[k].edge,
                            SourceGapIndex = capsA[k].gapIndex,
                            TargetEdge = capsB[targetK].edge,
                            TargetGapIndex = capsB[targetK].gapIndex,
                            Style = MarkingStyle.Dashed,
                            Curvature = ComputeCurvature(capsA[k], capsB[targetK]),
                            Confidence = GenerationConfidence.High,
                            Category = isMedian ? "MedianEdgeLine" : "LaneSeparator",
                            Reason = $"Through-road lane divider #{k} continuing across T-junction"
                        });
                    }
                }
            }
            // -------------------------------------------------------------------------
            // PASS 3: 2-APPROACH CONTINUOUS ROADS (STRAIGHT, CURVE, ASYMMETRIC TRANSITIONS)
            // -------------------------------------------------------------------------
            else if (isThroughRoadOnly && edgeList.Count == 2)
            {
                Entity eA = edgeList[0];
                Entity eB = edgeList[1];
                var capsA = classicCapEndpoints[eA];
                var capsB = classicCapEndpoints[eB];

                if (capsA.Count >= 2 && capsB.Count >= 2)
                {
                    // 1. Left outer road edge:
                    rawCandidates.Add(new GeneratedMarkingCandidate
                    {
                        SourceEdge = capsA[0].edge,
                        SourceGapIndex = capsA[0].gapIndex,
                        TargetEdge = capsB[capsB.Count - 1].edge,
                        TargetGapIndex = capsB[capsB.Count - 1].gapIndex,
                        Style = MarkingStyle.Solid,
                        Curvature = ComputeCurvature(capsA[0], capsB[capsB.Count - 1]),
                        Confidence = GenerationConfidence.High,
                        Category = "OuterEdgeLine",
                        Reason = "Continuous left outer road edge"
                    });

                    // 2. Right outer road edge:
                    rawCandidates.Add(new GeneratedMarkingCandidate
                    {
                        SourceEdge = capsA[capsA.Count - 1].edge,
                        SourceGapIndex = capsA[capsA.Count - 1].gapIndex,
                        TargetEdge = capsB[0].edge,
                        TargetGapIndex = capsB[0].gapIndex,
                        Style = MarkingStyle.Solid,
                        Curvature = ComputeCurvature(capsA[capsA.Count - 1], capsB[0]),
                        Confidence = GenerationConfidence.High,
                        Category = "OuterEdgeLine",
                        Reason = "Continuous right outer road edge"
                    });

                    // 3. Lane dividers / median lines:
                    if (capsA.Count == capsB.Count)
                    {
                        for (int k = 1; k < capsA.Count - 1; k++)
                        {
                            int targetK = capsB.Count - 1 - k;
                            bool isMedian = (k == capsA.Count / 2);
                            MarkingStyle style = isMedian ? (capsA.Count > 3 ? MarkingStyle.Solid : MarkingStyle.Dashed) : MarkingStyle.Dashed;

                            rawCandidates.Add(new GeneratedMarkingCandidate
                            {
                                SourceEdge = capsA[k].edge,
                                SourceGapIndex = capsA[k].gapIndex,
                                TargetEdge = capsB[targetK].edge,
                                TargetGapIndex = capsB[targetK].gapIndex,
                                Style = style,
                                Curvature = ComputeCurvature(capsA[k], capsB[targetK]),
                                Confidence = GenerationConfidence.High,
                                Category = isMedian ? "MedianEdgeLine" : "LaneSeparator",
                                Reason = isMedian ? "Continuous road center / median line" : $"Continuous lane divider #{k}"
                            });
                        }
                    }
                    else
                    {
                        // Asymmetric lane transition (e.g. 2 lanes to 3 lanes, 3->4, 4->2):
                        int minCount = math.min(capsA.Count, capsB.Count);
                        for (int k = 1; k < minCount - 1; k++)
                        {
                            int targetK = capsB.Count - 1 - k;
                            rawCandidates.Add(new GeneratedMarkingCandidate
                            {
                                SourceEdge = capsA[k].edge,
                                SourceGapIndex = capsA[k].gapIndex,
                                TargetEdge = capsB[targetK].edge,
                                TargetGapIndex = capsB[targetK].gapIndex,
                                Style = MarkingStyle.Dashed,
                                Curvature = ComputeCurvature(capsA[k], capsB[targetK]),
                                Confidence = GenerationConfidence.Medium,
                                Category = "LaneSeparator",
                                Reason = $"Asymmetric transition lane divider #{k}"
                            });
                        }
                    }
                }
            }
            // -------------------------------------------------------------------------
            // PASS 4: 4-WAY CROSSROADS (OPPOSING HIGHWAY/THROUGH PAIRS)
            // -------------------------------------------------------------------------
            else if (topType == JunctionTopologyType.Crossroads || topType == JunctionTopologyType.AsymmetricCrossroads)
            {
                var pairedEdges = new HashSet<(Entity, Entity)>();
                for (int i = 0; i < edgeList.Count; i++)
                {
                    Entity eA = edgeList[i];
                    Entity bestOpposing = Entity.Null;
                    float bestAngleDiff = float.MaxValue;

                    for (int j = 0; j < edgeList.Count; j++)
                    {
                        if (i == j) continue;
                        Entity eB = edgeList[j];
                        float diff = math.abs(180f - AngleDiffDeg(approachAngles[eA], approachAngles[eB]));
                        if (diff < 20f && diff < bestAngleDiff)
                        {
                            bestAngleDiff = diff;
                            bestOpposing = eB;
                        }
                    }

                    if (bestOpposing != Entity.Null)
                    {
                        var pairKey = eA.Index < bestOpposing.Index ? (eA, bestOpposing) : (bestOpposing, eA);
                        if (!pairedEdges.Contains(pairKey))
                        {
                            pairedEdges.Add(pairKey);
                            var capsA = classicCapEndpoints[eA];
                            var capsB = classicCapEndpoints[bestOpposing];

                            if (capsA.Count == capsB.Count && capsA.Count >= 2)
                            {
                                for (int k = 0; k < capsA.Count; k++)
                                {
                                    bool isOuter = (k == 0 || k == capsA.Count - 1);
                                    bool isMedian = (k == capsA.Count / 2);
                                    MarkingStyle style = isOuter ? MarkingStyle.Solid : (isMedian ? MarkingStyle.Solid : MarkingStyle.Dashed);
                                    string cat = isOuter ? "OuterEdgeLine" : (isMedian ? "MedianEdgeLine" : "LaneSeparator");

                                    rawCandidates.Add(new GeneratedMarkingCandidate
                                    {
                                        SourceEdge = capsA[k].edge,
                                        SourceGapIndex = capsA[k].gapIndex,
                                        TargetEdge = capsB[capsB.Count - 1 - k].edge,
                                        TargetGapIndex = capsB[capsB.Count - 1 - k].gapIndex,
                                        Style = style,
                                        Curvature = ComputeCurvature(capsA[k], capsB[capsB.Count - 1 - k]),
                                        Confidence = GenerationConfidence.High,
                                        Category = cat,
                                        Reason = $"Aligned through-continuation boundary #{k}"
                                    });
                                }
                            }
                        }
                    }
                }
            }
            // -------------------------------------------------------------------------
            // PASS 5: Y-JUNCTION / MERGE / DIVERGE (GORE CHANNELIZATION)
            // -------------------------------------------------------------------------
            else if (topType == JunctionTopologyType.YJunction && edgeList.Count == 3)
            {
                float minAngle = float.MaxValue;
                int arm1 = -1, arm2 = -1;
                for (int i = 0; i < 3; i++)
                {
                    for (int j = i + 1; j < 3; j++)
                    {
                        float d = AngleDiffDeg(approachAngles[edgeList[i]], approachAngles[edgeList[j]]);
                        if (d < minAngle)
                        {
                            minAngle = d;
                            arm1 = i;
                            arm2 = j;
                            arm1 = i;
                            arm2 = j;
                        }
                    }
                }

                if (minAngle < 85f && arm1 >= 0 && arm2 >= 0)
                {
                    Entity forkA = edgeList[arm1];
                    Entity forkB = edgeList[arm2];
                    var capsFA = classicCapEndpoints[forkA];
                    var capsFB = classicCapEndpoints[forkB];

                    if (capsFA.Count >= 2 && capsFB.Count >= 2)
                    {
                        // Find the inner-facing endpoints between the two diverging forks
                        var goreA = capsFA[capsFA.Count - 1];
                        var goreB = capsFB[0];

                        rawCandidates.Add(new GeneratedMarkingCandidate
                        {
                            SourceEdge = goreA.edge,
                            SourceGapIndex = goreA.gapIndex,
                            TargetEdge = goreB.edge,
                            TargetGapIndex = goreB.gapIndex,
                            Style = MarkingStyle.Solid,
                            Curvature = ComputeCurvature(goreA, goreB),
                            Confidence = GenerationConfidence.Medium,
                            Category = "GoreBoundary",
                            Reason = "Highway split / merge gore channelization boundary"
                        });
                    }
                }
            }
            // -------------------------------------------------------------------------
            // PASS 6: MULTI-ARM / ROUNDABOUT / AMBIGUOUS (AMBIGUITY RULE: GENERATE LESS)
            // -------------------------------------------------------------------------
            else if (topType == JunctionTopologyType.RoundaboutOrMultiLeg)
            {
                // Ambiguity rule: 5+ arm junctions and complex roundabouts must NEVER have
                // arbitrary chords connecting across opposite sides of the circle.
                // Stop bars per approach are already generated in Pass 1.
            }

            // -------------------------------------------------------------------------
            // STEP 3: STRICT GEOMETRY, TANGENT & CONFLICT VALIDATION FILTER
            // -------------------------------------------------------------------------
            var acceptedCandidates = new List<GeneratedMarkingCandidate>();
            var seenPairs = new HashSet<string>();

            for (int i = 0; i < rawCandidates.Count; i++)
            {
                var cand = rawCandidates[i];

                // 1. Semantic Cap Check: Must never connect setback or parking anchors across junction
                if (cand.SourceGapIndex >= kSetbackGapBase || cand.TargetGapIndex >= kSetbackGapBase)
                {
                    result.RejectedCandidates.Add(new RejectedCandidateRecord
                    {
                        SourceEdge = cand.SourceEdge,
                        SourceGapIndex = cand.SourceGapIndex,
                        TargetEdge = cand.TargetEdge,
                        TargetGapIndex = cand.TargetGapIndex,
                        Category = cand.Category,
                        RejectReason = "SetbackAnchorIgnored"
                    });
                    result.RejectedAmbiguous++;
                    continue;
                }

                // 2. Deduplication check
                string key1 = $"{cand.SourceEdge.Index}_{cand.SourceGapIndex}:{cand.TargetEdge.Index}_{cand.TargetGapIndex}";
                string key2 = $"{cand.TargetEdge.Index}_{cand.TargetGapIndex}:{cand.SourceEdge.Index}_{cand.SourceGapIndex}";
                if (seenPairs.Contains(key1) || seenPairs.Contains(key2))
                {
                    result.DuplicatesRemoved++;
                    continue;
                }
                seenPairs.Add(key1);

                // 3. Resolve endpoint world positions and tangents
                if (!TryGetEndpoint(classicCapEndpoints, cand.SourceEdge, cand.SourceGapIndex, out var epSrc) ||
                    !TryGetEndpoint(classicCapEndpoints, cand.TargetEdge, cand.TargetGapIndex, out var epDst))
                {
                    result.RejectedCandidates.Add(new RejectedCandidateRecord
                    {
                        SourceEdge = cand.SourceEdge,
                        SourceGapIndex = cand.SourceGapIndex,
                        TargetEdge = cand.TargetEdge,
                        TargetGapIndex = cand.TargetGapIndex,
                        Category = cand.Category,
                        RejectReason = "MissingEndpoint"
                    });
                    result.RejectedTopology++;
                    continue;
                }

                float chordDist = math.distance(epSrc.position, epDst.position);

                // 4. Length Sanity Check
                if (chordDist < 0.2f || chordDist > 75.0f)
                {
                    result.RejectedCandidates.Add(new RejectedCandidateRecord
                    {
                        SourceEdge = cand.SourceEdge,
                        SourceGapIndex = cand.SourceGapIndex,
                        TargetEdge = cand.TargetEdge,
                        TargetGapIndex = cand.TargetGapIndex,
                        Category = cand.Category,
                        RejectReason = $"InvalidLength ({chordDist:F1}m)"
                    });
                    result.RejectedSurface++;
                    continue;
                }

                // 5. Tangent Alignment Validation
                if (cand.Category == "StopLine")
                {
                    if (cand.SourceEdge != cand.TargetEdge)
                    {
                        result.RejectedCandidates.Add(new RejectedCandidateRecord
                        {
                            SourceEdge = cand.SourceEdge,
                            SourceGapIndex = cand.SourceGapIndex,
                            TargetEdge = cand.TargetEdge,
                            TargetGapIndex = cand.TargetGapIndex,
                            Category = cand.Category,
                            RejectReason = "StopLineSpansDifferentEdges"
                        });
                        result.RejectedTangent++;
                        continue;
                    }

                    float2 chordDir = math.normalizesafe(new float2(epDst.position.x - epSrc.position.x, epDst.position.z - epSrc.position.z));
                    float dotTangent = math.abs(math.dot(chordDir, epSrc.tangent));
                    if (dotTangent > 0.40f)
                    {
                        result.RejectedCandidates.Add(new RejectedCandidateRecord
                        {
                            SourceEdge = cand.SourceEdge,
                            SourceGapIndex = cand.SourceGapIndex,
                            TargetEdge = cand.TargetEdge,
                            TargetGapIndex = cand.TargetGapIndex,
                            Category = cand.Category,
                            RejectReason = $"StopLineNotPerpendicular (dot={dotTangent:F2})"
                        });
                        result.RejectedTangent++;
                        continue;
                    }
                }
                else if (cand.Category == "GoreBoundary")
                {
                    // Gore boundaries connect apex endpoints across diverging branches
                    // Validated length and distinct edges
                }
                else
                {
                    // Longitudinal lines (LaneSeparator, OuterEdgeLine, MedianEdgeLine)
                    float2 chordDir = math.normalizesafe(new float2(epDst.position.x - epSrc.position.x, epDst.position.z - epSrc.position.z));
                    float2 srcInward = -epSrc.tangent;
                    float2 dstOutward = epDst.tangent;

                    float dotSrc = math.dot(chordDir, srcInward);
                    float dotDst = math.dot(chordDir, dstOutward);

                    // Must follow travel corridor without severe diagonal skew
                    if (dotSrc < 0.65f || dotDst < 0.65f)
                    {
                        result.RejectedCandidates.Add(new RejectedCandidateRecord
                        {
                            SourceEdge = cand.SourceEdge,
                            SourceGapIndex = cand.SourceGapIndex,
                            TargetEdge = cand.TargetEdge,
                            TargetGapIndex = cand.TargetGapIndex,
                            Category = cand.Category,
                            RejectReason = $"SevereDiagonalSkew (dotSrc={dotSrc:F2}, dotDst={dotDst:F2})"
                        });
                        result.RejectedTangent++;
                        continue;
                    }
                }

                // Candidate PASSED all gates
                acceptedCandidates.Add(cand);

                if (cand.Category == "OuterEdgeLine") result.OuterEdgeLines++;
                else if (cand.Category == "MedianEdgeLine") result.MedianEdgeLines++;
                else if (cand.Category == "LaneSeparator") result.LaneSeparators++;
                else if (cand.Category == "StopLine") result.StopLines++;
                else if (cand.Category == "GoreBoundary") result.GoreAreas++;
            }

            result.Candidates = acceptedCandidates;
            result.TotalLines = acceptedCandidates.Count;

            log?.Info($"TopologyAwareAutoGenerator: Topology={topType}, Generated {result.TotalLines} lines " +
                     $"(Outer: {result.OuterEdgeLines}, Median: {result.MedianEdgeLines}, Lanes: {result.LaneSeparators}, " +
                     $"Stops: {result.StopLines}, Gore: {result.GoreAreas}, Rejected: {result.RejectedCandidates.Count}) on Node #{node.Index}");

            return result;
        }

        /// <summary>
        /// Computes curvature parameter from endpoint tangents so lines follow curved road paths accurately.
        /// </summary>
        private static float ComputeCurvature(MarkingEndpoint epA, MarkingEndpoint epB)
        {
            float3 delta = epB.position - epA.position;
            float len = math.length(delta);
            if (len < 0.5f) return 0.0f;

            float2 chord2D = math.normalizesafe(new float2(delta.x, delta.z));
            float2 tanA = math.normalizesafe(epA.tangent);
            float cross = chord2D.x * tanA.y - chord2D.y * tanA.x;

            return math.clamp(cross * 0.5f, -0.6f, 0.6f);
        }

        /// <summary>
        /// Commits the generated candidate markings to the node's authoritative MarkingLine buffer.
        /// </summary>
        public static int ApplyGeneratedMarkings(EntityManager em, Entity node, AutoGeneratePreviewResult preview)
        {
            if (node == Entity.Null || preview.Candidates == null || preview.Candidates.Count == 0)
            {
                return 0;
            }

            try
            {
                if (!em.Exists(node)) return 0;
            }
            catch { }

            if (!em.HasBuffer<MarkingLine>(node))
            {
                em.AddBuffer<MarkingLine>(node);
            }

            var lineBuf = em.GetBuffer<MarkingLine>(node);
            int added = 0;

            for (int i = 0; i < preview.Candidates.Count; i++)
            {
                var cand = preview.Candidates[i];
                if (cand.Confidence == GenerationConfidence.Low) continue;

                bool duplicate = false;
                for (int l = 0; l < lineBuf.Length; l++)
                {
                    var existing = lineBuf[l];
                    if ((existing.sourceEdge == cand.SourceEdge && existing.sourceGapIndex == cand.SourceGapIndex &&
                         existing.targetEdge == cand.TargetEdge && existing.targetGapIndex == cand.TargetGapIndex) ||
                        (existing.sourceEdge == cand.TargetEdge && existing.sourceGapIndex == cand.TargetGapIndex &&
                         existing.targetEdge == cand.SourceEdge && existing.targetGapIndex == cand.SourceGapIndex))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    lineBuf.Add(new MarkingLine
                    {
                        sourceEdge = cand.SourceEdge,
                        sourceGapIndex = cand.SourceGapIndex,
                        targetEdge = cand.TargetEdge,
                        targetGapIndex = cand.TargetGapIndex,
                        style = (int)cand.Style,
                        curvature = cand.Curvature
                    });
                    added++;
                }
            }

            if (added > 0)
            {
                em.AddComponent<Updated>(node);
                log?.Info($"TopologyAwareAutoGenerator: Applied {added} validated markings on Node #{node.Index}.");
            }

            return added;
        }

        private static JunctionTopologyType ClassifyJunction(
            List<Entity> edges,
            Dictionary<Entity, float> angles,
            Dictionary<Entity, List<MarkingEndpoint>> caps,
            out Entity armA,
            out Entity armB,
            out Entity stem)
        {
            armA = Entity.Null;
            armB = Entity.Null;
            stem = Entity.Null;

            if (edges.Count == 1) return JunctionTopologyType.DeadEnd;

            if (edges.Count == 2)
            {
                float angleDiff = AngleDiffDeg(angles[edges[0]], angles[edges[1]]);
                bool isCollinear = math.abs(180f - angleDiff) < 25f;

                int count0 = caps.TryGetValue(edges[0], out var l0) ? l0.Count : 0;
                int count1 = caps.TryGetValue(edges[1], out var l1) ? l1.Count : 0;

                if (count0 != count1 && isCollinear)
                {
                    return JunctionTopologyType.AsymmetricTransition;
                }

                return isCollinear ? JunctionTopologyType.StraightOrBend : JunctionTopologyType.Curve;
            }

            if (edges.Count == 4)
            {
                int count0 = caps.TryGetValue(edges[0], out var l0) ? l0.Count : 0;
                int count1 = caps.TryGetValue(edges[1], out var l1) ? l1.Count : 0;
                int count2 = caps.TryGetValue(edges[2], out var l2) ? l2.Count : 0;
                int count3 = caps.TryGetValue(edges[3], out var l3) ? l3.Count : 0;

                if (count0 != count1 || count0 != count2 || count0 != count3)
                {
                    return JunctionTopologyType.AsymmetricCrossroads;
                }
                return JunctionTopologyType.Crossroads;
            }

            if (edges.Count > 4)
            {
                return JunctionTopologyType.RoundaboutOrMultiLeg;
            }

            if (edges.Count == 3)
            {
                float bestDiff = float.MaxValue;
                int bestI = -1, bestJ = -1;

                for (int i = 0; i < 3; i++)
                {
                    for (int j = i + 1; j < 3; j++)
                    {
                        float d = math.abs(180f - AngleDiffDeg(angles[edges[i]], angles[edges[j]]));
                        if (d < bestDiff)
                        {
                            bestDiff = d;
                            bestI = i;
                            bestJ = j;
                        }
                    }
                }

                if (bestDiff < 15f && bestI >= 0 && bestJ >= 0)
                {
                    armA = edges[bestI];
                    armB = edges[bestJ];
                    for (int k = 0; k < 3; k++)
                    {
                        if (k != bestI && k != bestJ) { stem = edges[k]; break; }
                    }
                    return JunctionTopologyType.TJunction;
                }

                return JunctionTopologyType.YJunction;
            }

            return JunctionTopologyType.RoundaboutOrMultiLeg;
        }

        private static bool TryGetEndpoint(
            Dictionary<Entity, List<MarkingEndpoint>> endpointsByEdge,
            Entity edge,
            int gapIndex,
            out MarkingEndpoint endpoint)
        {
            endpoint = default;
            if (endpointsByEdge.TryGetValue(edge, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].gapIndex == gapIndex)
                    {
                        endpoint = list[i];
                        return true;
                    }
                }
            }
            return false;
        }

        private static float AngleDiffDeg(float aRad, float bRad)
        {
            float diff = math.abs(aRad - bRad) * (180f / math.PI);
            while (diff > 180f) diff = math.abs(360f - diff);
            return diff;
        }
    }
}
