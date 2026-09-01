using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;

namespace MarkingStudio
{
    public enum SnapTargetKind
    {
        None,             // Free Position (no snap)
        LaneBoundary,     // Internal lane boundary
        RoadEdge,         // Outer road pavement edge / curb
        MedianEdge,       // Median divider edge
        MarkingEndpoint,  // Existing marking endpoint / connection
        LineIntersection, // Crossing between two lines
        CornerAnchor      // Intersection curb return corner
    }

    public struct SnapResult
    {
        public bool DidSnap;
        public SnapTargetKind Kind;
        public float3 SnapPosition;
        public float3 SnapNormal;
        public string Description;
        public float DistanceSq;
    }

    /// <summary>
    /// Magic Marking 2.2 Precision Snapping Engine.
    /// Provides intelligent geometric attraction and visual indicators across:
    ///   - Lane Boundary
    ///   - Road Edge
    ///   - Median Edge
    ///   - Existing Marking
    ///   - Line Intersection
    ///   - Free Position
    /// </summary>
    public static class AdvancedSnapEngine
    {
        public const float kDefaultSnapThresholdM = 1.6f;
        public const float kDefaultSnapThresholdSq = kDefaultSnapThresholdM * kDefaultSnapThresholdM;

        public static SnapResult FindBestSnap(
            float3 cursorWorldPos,
            Entity selectedNode,
            IReadOnlyList<MarkingEndpoint> endpoints,
            IReadOnlyList<MarkingCornerAnchor> corners,
            IReadOnlyList<MarkingIntersectionAnchor> intersections,
            HashSet<(Entity edge, int gap)> connectedAnchors = null,
            bool enableEndpointSnap = true,
            bool enableIntersectionSnap = true,
            bool enableMidpointSnap = true)
        {
            var best = new SnapResult
            {
                DidSnap = false,
                Kind = SnapTargetKind.None,
                SnapPosition = cursorWorldPos,
                SnapNormal = new float3(0, 1, 0),
                Description = "Free Position",
                DistanceSq = float.MaxValue
            };

            // 1. Line Crossing / Intersections Snap (Highest Priority)
            if (enableIntersectionSnap && intersections != null)
            {
                for (int i = 0; i < intersections.Count; i++)
                {
                    var inter = intersections[i];
                    float dSq = math.distancesq(cursorWorldPos, inter.position);
                    if (dSq <= kDefaultSnapThresholdSq && dSq < best.DistanceSq)
                    {
                        best.DidSnap = true;
                        best.Kind = SnapTargetKind.LineIntersection;
                        best.SnapPosition = inter.position;
                        best.SnapNormal = new float3(0, 1, 0);
                        best.Description = "Line Intersection";
                        best.DistanceSq = dSq;
                    }
                }
            }

            // 2. Marking Endpoint / Lane Boundary / Road Edge / Median Edge Snap
            if (enableEndpointSnap && endpoints != null)
            {
                for (int i = 0; i < endpoints.Count; i++)
                {
                    var ep = endpoints[i];
                    float dSq = math.distancesq(cursorWorldPos, ep.position);
                    if (dSq <= kDefaultSnapThresholdSq && dSq < best.DistanceSq)
                    {
                        best.DidSnap = true;

                        bool isConnected = connectedAnchors != null && connectedAnchors.Contains((ep.edge, ep.gapIndex));

                        if (isConnected)
                        {
                            best.Kind = SnapTargetKind.MarkingEndpoint;
                            best.Description = "Existing Marking";
                        }
                        else if (ep.gapIndex == 0 || ep.gapIndex >= 10)
                        {
                            best.Kind = SnapTargetKind.RoadEdge;
                            best.Description = "Road Edge";
                        }
                        else if (ep.gapIndex == 1)
                        {
                            best.Kind = SnapTargetKind.MedianEdge;
                            best.Description = "Median Edge";
                        }
                        else
                        {
                            best.Kind = SnapTargetKind.LaneBoundary;
                            best.Description = $"Lane Boundary #{ep.gapIndex}";
                        }

                        best.SnapPosition = ep.position;
                        best.SnapNormal = new float3(ep.tangent.x, 0, ep.tangent.y);
                        best.DistanceSq = dSq;
                    }
                }
            }

            // 3. Corner / Road Edge Anchors
            if (corners != null)
            {
                for (int i = 0; i < corners.Count; i++)
                {
                    var c = corners[i];
                    float dSq = math.distancesq(cursorWorldPos, c.position);
                    if (dSq <= kDefaultSnapThresholdSq && dSq < best.DistanceSq)
                    {
                        best.DidSnap = true;
                        best.Kind = SnapTargetKind.RoadEdge;
                        best.SnapPosition = c.position;
                        best.SnapNormal = new float3(0, 1, 0);
                        best.Description = "Road Edge Corner";
                        best.DistanceSq = dSq;
                    }
                }
            }

            return best;
        }

        public static float3 ApplyAngleLock(float3 startPos, float3 currentPos, float snapAngleDegrees = 45.0f)
        {
            float3 diff = currentPos - startPos;
            float len = math.length(new float2(diff.x, diff.z));
            if (len < 0.1f) return currentPos;

            float angleRad = math.atan2(diff.z, diff.x);
            float stepRad = math.radians(snapAngleDegrees);
            float snappedAngle = math.round(angleRad / stepRad) * stepRad;

            return new float3(
                startPos.x + math.cos(snappedAngle) * len,
                currentPos.y,
                startPos.z + math.sin(snappedAngle) * len
            );
        }
    }
}
