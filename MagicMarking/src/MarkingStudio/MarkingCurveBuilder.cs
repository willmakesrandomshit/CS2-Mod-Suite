using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;

namespace MarkingStudio
{
    /// <summary>
    /// Single source of truth for building geometrically valid, smooth Bezier curves between MarkingEndpoints.
    /// Handles:
    ///   - FOLLOW ROAD (authoritative road/lane geometry following)
    ///   - STRAIGHT (direct chords)
    ///   - CUSTOM CURVE (user-controlled curvature)
    ///   - Lateral offsets, elevation changes, ramps, gentle/sharp curves, medians, and islands.
    /// </summary>
    public static class MarkingCurveBuilder
    {
        public const float kPullFactor = 0.4f;
        public const float kMaxPullFactor = 0.8f;

        public static Bezier4x3 Build(float3 a, float2 ta, float3 b, float2 tb)
            => Build(a, ta, b, tb, kPullFactor, MarkingDrawingMode.FollowRoad, 0f);

        public static Bezier4x3 Build(float3 a, float2 ta, float3 b, float2 tb, float pullFactor, MarkingDrawingMode mode = MarkingDrawingMode.FollowRoad, float lateralOffset = 0f)
        {
            float chord = math.distance(a, b);
            if (chord < 1e-4f)
                return new Bezier4x3(a, a, b, b);

            float2 ab2 = new float2(b.x - a.x, b.z - a.z);
            float chord2D = math.length(ab2);
            if (chord2D < 1e-4f)
            {
                float3 mid1 = math.lerp(a, b, 1f / 3f);
                float3 mid2 = math.lerp(a, b, 2f / 3f);
                return new Bezier4x3(a, mid1, mid2, b);
            }

            float2 dirAB2 = ab2 / chord2D;
            float2 perpDir2D = new float2(-dirAB2.y, dirAB2.x);
            float3 offset3D = new float3(perpDir2D.x * lateralOffset, 0f, perpDir2D.y * lateralOffset);

            float3 startPt = a + offset3D;
            float3 endPt = b + offset3D;

            // -------------------------------------------------------------------------
            // MODE 1: STRAIGHT CHORD
            // -------------------------------------------------------------------------
            if (mode == MarkingDrawingMode.Straight)
            {
                float3 mid1 = math.lerp(startPt, endPt, 1f / 3f);
                float3 mid2 = math.lerp(startPt, endPt, 2f / 3f);
                return new Bezier4x3(startPt, mid1, mid2, endPt);
            }

            // -------------------------------------------------------------------------
            // MODE 0: FOLLOW ROAD & MODE 2: CUSTOM CURVE
            // -------------------------------------------------------------------------
            float2 ta2 = math.normalizesafe(ta);
            float2 tb2 = math.normalizesafe(tb);

            bool haveTa = math.lengthsq(ta2) > 0.01f;
            bool haveTb = math.lengthsq(tb2) > 0.01f;

            if (haveTa && haveTb)
            {
                float dotTangents = math.dot(ta2, tb2);
                float dotAtoChord = math.dot(ta2, dirAB2);
                float dotBtoChord = math.dot(tb2, dirAB2);

                // If tangents are parallel to each other and perpendicular to chord AB
                // (e.g. lateral stop line across lane), keep it straight
                if (dotTangents > 0.85f && math.abs(dotAtoChord) < 0.35f && math.abs(dotBtoChord) < 0.35f)
                {
                    float3 mid1 = math.lerp(startPt, endPt, 1f / 3f);
                    float3 mid2 = math.lerp(startPt, endPt, 2f / 3f);
                    return new Bezier4x3(startPt, mid1, mid2, endPt);
                }
            }

            // Determine natural exit direction from A (into junction/curve)
            float2 outA = haveTa ? -ta2 : dirAB2;
            if (haveTa)
            {
                float dotOut = math.dot(outA, dirAB2);
                if (dotOut < -0.35f && math.dot(ta2, dirAB2) > 0.35f)
                    outA = ta2;
            }

            // Determine natural entry direction into B
            float2 inB = haveTb ? tb2 : dirAB2;
            if (haveTb)
            {
                float dotIn = math.dot(inB, dirAB2);
                if (dotIn < -0.35f && math.dot(-tb2, dirAB2) > 0.35f)
                    inB = -tb2;
            }

            float effectivePull = pullFactor;
            if (mode == MarkingDrawingMode.FollowRoad)
            {
                effectivePull = AdaptivePullFactor(a, ta, b, tb);
            }
            else if (mode == MarkingDrawingMode.CustomCurve)
            {
                effectivePull = math.clamp(pullFactor, -1.0f, 1.0f);
            }

            float clampedPull = chord * math.clamp(math.abs(effectivePull), 0.05f, kMaxPullFactor);
            if (effectivePull < 0f && mode == MarkingDrawingMode.CustomCurve)
            {
                // Inverted curve: reverse normal deflection
                outA = new float2(-outA.y, outA.x);
                inB = new float2(-inB.y, inB.x);
            }

            // Bound sharp turn angles to prevent loops/self-intersections
            float cosTurn = math.clamp(math.dot(outA, inB), -1f, 1f);
            if (cosTurn < -0.3f)
            {
                float angle = math.acos(cosTurn);
                float maxSafeFraction = math.lerp(0.35f, 0.15f, (angle - 1.87f) / 1.27f);
                clampedPull = math.min(clampedPull, chord * math.max(0.12f, maxSafeFraction));
            }

            float3 p1 = startPt + new float3(outA.x, 0f, outA.y) * clampedPull;
            float3 p2 = endPt - new float3(inB.x, 0f, inB.y) * clampedPull;

            // Preserve authoritative elevation changes (grades, ramps, vertical curves)
            p1.y = math.lerp(startPt.y, endPt.y, 1f / 3f);
            p2.y = math.lerp(startPt.y, endPt.y, 2f / 3f);

            // Sanity validation: prevent NaN, Inf, or extreme out-of-bounds control points
            if (math.any(math.isnan(p1)) || math.any(math.isnan(p2)) ||
                math.any(math.isinf(p1)) || math.any(math.isinf(p2)) ||
                math.distance(p1, startPt) > chord * 2.5f || math.distance(p2, endPt) > chord * 2.5f)
            {
                p1 = math.lerp(startPt, endPt, 1f / 3f);
                p2 = math.lerp(startPt, endPt, 2f / 3f);
            }

            return new Bezier4x3(startPt, p1, p2, endPt);
        }

        public static Bezier4x3 Build(MarkingEndpoint src, MarkingEndpoint dst)
            => Build(src, dst, kPullFactor, MarkingDrawingMode.FollowRoad, 0f);

        public static Bezier4x3 Build(MarkingEndpoint src, MarkingEndpoint dst, float pullFactor, MarkingDrawingMode mode = MarkingDrawingMode.FollowRoad, float lateralOffset = 0f)
        {
            // Same-edge lateral line check (e.g. stop bars across lane cap)
            if (src.edge != Entity.Null && src.edge == dst.edge)
            {
                bool srcSetback = src.gapIndex >= 2000;
                bool dstSetback = dst.gapIndex >= 2000;
                if (srcSetback == dstSetback && mode != MarkingDrawingMode.CustomCurve)
                {
                    float3 mid1 = math.lerp(src.position, dst.position, 1f / 3f);
                    float3 mid2 = math.lerp(src.position, dst.position, 2f / 3f);
                    return new Bezier4x3(src.position, mid1, mid2, dst.position);
                }
            }

            return Build(src.position, src.tangent, dst.position, dst.tangent, pullFactor, mode, lateralOffset);
        }

        public static float AdaptivePullFactor(float3 a, float2 ta, float3 b, float2 tb)
        {
            float chord = math.distance(a, b);
            if (chord <= 1e-4f) return kPullFactor;

            float2 ab2 = math.normalizesafe(new float2(b.x - a.x, b.z - a.z));
            float2 ta2 = math.normalizesafe(ta);
            float2 tb2 = math.normalizesafe(tb);

            float2 outA = math.lengthsq(ta2) > 0.01f ? -ta2 : ab2;
            if (math.dot(outA, ab2) < -0.35f && math.dot(ta2, ab2) > 0.35f) outA = ta2;

            float2 inB = math.lengthsq(tb2) > 0.01f ? tb2 : ab2;
            if (math.dot(inB, ab2) < -0.35f && math.dot(-tb2, ab2) > 0.35f) inB = -tb2;

            float cos = math.clamp(math.dot(outA, inB), -1f, 1f);
            float theta = math.acos(cos);
            if (theta < 1e-3f) return 1f / 3f;

            float f = (4f / 3f) * math.tan(theta * 0.25f) / (2f * math.sin(theta * 0.5f));
            return math.clamp(f, 0.15f, kMaxPullFactor);
        }

        public static float AdaptivePullFactor(MarkingEndpoint src, MarkingEndpoint dst)
        {
            if (src.edge != Entity.Null && src.edge == dst.edge && (src.gapIndex >= 2000) == (dst.gapIndex >= 2000))
                return 0f;
            return AdaptivePullFactor(src.position, src.tangent, dst.position, dst.tangent);
        }

        public static bool TryBuild(EntityManager em, Entity node, MarkingLine line, out Bezier4x3 bez)
        {
            bez = default;
            var endpoints = MarkingEndpointExtractor.Extract(em, node);
            if (!TryFind(endpoints, line.sourceEdge, line.sourceGapIndex, out var src)) return false;
            if (!TryFind(endpoints, line.targetEdge, line.targetGapIndex, out var dst)) return false;
            bez = Build(src, dst, line.curvature, (MarkingDrawingMode)line.drawingMode, line.lateralOffset);
            return true;
        }

        public static bool TryBuild(IReadOnlyList<MarkingEndpoint> endpoints, MarkingLine line, out Bezier4x3 bez)
        {
            bez = default;
            if (endpoints == null) return false;
            if (!TryFind(endpoints, line.sourceEdge, line.sourceGapIndex, out var src)) return false;
            if (!TryFind(endpoints, line.targetEdge, line.targetGapIndex, out var dst)) return false;
            bez = Build(src, dst, line.curvature, (MarkingDrawingMode)line.drawingMode, line.lateralOffset);
            return true;
        }

        private static bool TryFind(IReadOnlyList<MarkingEndpoint> endpoints, Entity edge, int gap, out MarkingEndpoint ep)
        {
            for (int i = 0; i < endpoints.Count; i++)
            {
                if (endpoints[i].edge == edge && endpoints[i].gapIndex == gap)
                {
                    ep = endpoints[i];
                    return true;
                }
            }
            ep = default;
            return false;
        }
    }
}
