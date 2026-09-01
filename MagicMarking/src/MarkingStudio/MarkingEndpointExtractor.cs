using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;

namespace MarkingStudio
{
    /// <summary>
    /// One attach point sitting BETWEEN two adjacent carriageway lanes (or on an outer kerb)
    /// at a road edge's node end. For an edge with N touching car-driveable lanes at the node,
    /// the extractor produces N+1 endpoints — one per lane-to-lane stitch + two outer kerbs.
    /// Lanes separated by a median (divider wider than kCarriagewayGapM) contribute two
    /// endpoints at that boundary instead of one — one per carriageway edge.
    ///
    /// gapIndex is a left-to-right running counter over the emitted endpoints (0 = outer-left
    /// kerb, max = outer-right kerb). It is an opaque identity — stable for a given composition,
    /// but renumbered if the road's lane layout changes. Two extra ranges never renumber the
    /// classic set: parking-bay edges (the true kerb line on roads with parking) start at
    /// kExtendedGapBase; setback anchors (the classic row repeated kSetbackDistanceM back along
    /// the road) start at kSetbackGapBase and mirror the classic gap numbers.
    /// </summary>
    public struct MarkingEndpoint
    {
        public Entity edge;        // road edge this endpoint sits on
        public int    gapIndex;    // 0..N (where N = car-lane count on the edge composition)
        public float3 position;    // world-space point on the node-side cap of the edge
        public float2 tangent;     // normalized horizontal tangent INTO the edge from the node
    }

    /// <summary>
    /// Phase 6a — corner anchor sitting at an intersection corner (where the kerb of one road
    /// meets the kerb of the adjacent road around the node). Derived from outer kerbs (gap=0
    /// and gap=N) of every ConnectedEdge by deduplicating near-coincident kerbs from neighbour
    /// edges into a single shared anchor.
    ///
    /// Use case: polygon area tool needs anchors at intersection corners so users can fill
    /// safety islands / yellow box junctions that touch the road edges. Lane endpoints don't
    /// cover this — they sit on the carriageway, not at the kerb between two roads.
    /// </summary>
    public struct MarkingCornerAnchor
    {
        public float3 position;  // world-space position of the corner
        // The two edges whose kerbs meet here, sorted ascending by Entity.Index. -1 (Entity.Null)
        // for edgeB when the corner is a standalone outer kerb (e.g. a dead-end node) with no
        // neighbour to dedupe against.
        public Entity edgeA;
        public Entity edgeB;
    }

    /// <summary>
    /// Reads endpoints from the edge's PREFAB COMPOSITION (NetCompositionLane buffer on the
    /// composition entity referenced by Composition.m_StartNode / m_EndNode). This is
    /// direction-agnostic — composition lanes describe the static lateral layout of the
    /// carriageway, not the runtime forward/backward sublanes — so a 2-way 2-lane road yields
    /// 3 endpoints (left kerb, centre divider, right kerb) regardless of traffic direction.
    ///
    /// Algorithm:
    ///   For each ConnectedEdge of the node:
    ///     1. Resolve the composition entity for the node-side cap:
    ///          nodeIsStart  → composition.m_StartNode
    ///          nodeIsEnd    → composition.m_EndNode
    ///     2. Read NetCompositionLane[] on that entity, filter to Road-flagged lanes (cars).
    ///     3. Dedupe by lateral position (forward + backward of the same physical lane share
    ///        m_Position.x but differ in Invert flag).
    ///     4. Sort by m_Position.x ascending.
    ///     5. For each pair of adjacent sorted lanes + the two outer kerbs, emit a world-space
    ///        point by interpolating along EdgeGeometry's m_Start (or m_End) right→left segment
    ///        at parameter t = (lateralX + width/2) / width, then offset by the lane's half-width
    ///        toward the stitch.
    /// </summary>
    public static class MarkingEndpointExtractor
    {
        public static List<MarkingEndpoint> Extract(EntityManager em, Entity node)
        {
            return Extract(em, node, log: false);
        }

        private const float kAnchorMatchRadiusSq = 1.5f * 1.5f;

        public static int ResolveEndpointIndex(IReadOnlyList<MarkingEndpoint> endpoints, in MarkingAreaVertex av)
        {
            if (av.refEdgeA != Entity.Null)
            {
                for (int i = 0; i < endpoints.Count; i++)
                    if (endpoints[i].edge == av.refEdgeA && endpoints[i].gapIndex == av.refGap)
                        return i;
                int best = -1;
                float bestSq = kAnchorMatchRadiusSq;
                for (int i = 0; i < endpoints.Count; i++)
                {
                    float dx = endpoints[i].position.x - av.refPos.x;
                    float dz = endpoints[i].position.z - av.refPos.z;
                    float sq = dx * dx + dz * dz;
                    if (sq < bestSq) { bestSq = sq; best = i; }
                }
                return best;
            }
            return av.refIndex >= 0 && av.refIndex < endpoints.Count ? av.refIndex : -1;
        }

        public static int ResolveCornerIndex(IReadOnlyList<MarkingCornerAnchor> corners, in MarkingAreaVertex av)
        {
            if (av.refEdgeA != Entity.Null)
            {
                int best = -1;
                float bestSq = kAnchorMatchRadiusSq;
                int exact = -1, exactCount = 0;
                for (int i = 0; i < corners.Count; i++)
                {
                    if (corners[i].edgeA != av.refEdgeA || corners[i].edgeB != av.refEdgeB) continue;
                    exact = i;
                    exactCount++;
                    float dx = corners[i].position.x - av.refPos.x;
                    float dz = corners[i].position.z - av.refPos.z;
                    float sq = dx * dx + dz * dz;
                    if (sq < bestSq) { bestSq = sq; best = i; }
                }
                if (exactCount == 1) return exact;
                if (best >= 0) return best;
                bestSq = kAnchorMatchRadiusSq;
                for (int i = 0; i < corners.Count; i++)
                {
                    float dx = corners[i].position.x - av.refPos.x;
                    float dz = corners[i].position.z - av.refPos.z;
                    float sq = dx * dx + dz * dz;
                    if (sq < bestSq) { bestSq = sq; best = i; }
                }
                return best;
            }
            return av.refIndex >= 0 && av.refIndex < corners.Count ? av.refIndex : -1;
        }

        private const float kCarriagewayGapM = 0.75f;
        private const int kExtendedGapBase = 1000;
        private const float kExtendedDedupeM = 0.10f;
        private const int   kSetbackGapBase   = 2000;
        private const float kSetbackDistanceM = 8f;

        public static bool IsEdgeExtractionReady(EntityManager em, Entity edge)
        {
            if (edge == Entity.Null || !em.Exists(edge)) return false;
            if (em.HasComponent<Deleted>(edge)) return false;
            if (!em.HasComponent<Edge>(edge)) return false;
            if (!em.HasComponent<Composition>(edge)) return false;
            var comp = em.GetComponentData<Composition>(edge);
            if (comp.m_Edge == Entity.Null || !em.HasBuffer<NetCompositionLane>(comp.m_Edge)) return false;
            if (!em.HasComponent<EdgeGeometry>(edge)) return false;
            var geom = em.GetComponentData<EdgeGeometry>(edge);
            if (math.lengthsq(geom.m_Start.m_Right.a - geom.m_Start.m_Left.a) < 1e-6f
                && math.lengthsq(geom.m_End.m_Right.d - geom.m_End.m_Left.d) < 1e-6f) return false;
            return true;
        }

        public static bool IsEdgeAliveButUnready(EntityManager em, Entity edge)
        {
            if (edge == Entity.Null || !em.Exists(edge)) return false;
            if (em.HasComponent<Deleted>(edge)) return false;
            if (!em.HasComponent<Edge>(edge)) return false;
            return !IsEdgeExtractionReady(em, edge);
        }

        private const float kCornerDedupRadiusM = 3.0f;
        private const float kCornerInwardPullFraction = 0.35f;

        public static List<MarkingCornerAnchor> ExtractCornerAnchors(EntityManager em, Entity node)
        {
            var corners = new List<MarkingCornerAnchor>(8);
            if (node == Entity.Null || !em.Exists(node) || em.HasComponent<Deleted>(node)) return corners;
            if (!em.HasBuffer<ConnectedEdge>(node)) return corners;
            if (!em.HasComponent<Node>(node)) return corners;

            float3 nodeCentre = em.GetComponentData<Node>(node).m_Position;
            var connected = em.GetBuffer<ConnectedEdge>(node, isReadOnly: true);

            var kerbList = new List<(Entity edge, float3 pos)>(connected.Length * 2);
            for (int i = 0; i < connected.Length; i++)
            {
                var edgeEntity = connected[i].m_Edge;
                if (edgeEntity == Entity.Null || !em.Exists(edgeEntity) || em.HasComponent<Deleted>(edgeEntity)) continue;
                if (!em.HasComponent<Edge>(edgeEntity)) continue;
                if (!em.HasComponent<EdgeGeometry>(edgeEntity)) continue;

                var edge = em.GetComponentData<Edge>(edgeEntity);
                bool nodeIsStart = edge.m_Start == node;
                bool nodeIsEnd   = edge.m_End == node;
                if (!nodeIsStart && !nodeIsEnd) continue;

                var geom = em.GetComponentData<EdgeGeometry>(edgeEntity);
                float3 leftPt, rightPt;
                if (nodeIsStart)
                {
                    leftPt  = geom.m_Start.m_Left.a;
                    rightPt = geom.m_Start.m_Right.a;
                }
                else
                {
                    leftPt  = geom.m_End.m_Left.d;
                    rightPt = geom.m_End.m_Right.d;
                }
                kerbList.Add((edgeEntity, leftPt));
                kerbList.Add((edgeEntity, rightPt));
            }

            var consumed = new bool[kerbList.Count];
            float r2 = kCornerDedupRadiusM * kCornerDedupRadiusM;
            for (int i = 0; i < kerbList.Count; i++)
            {
                if (consumed[i]) continue;
                var a = kerbList[i];
                int matchIdx = -1;
                float bestD2 = r2;
                for (int j = i + 1; j < kerbList.Count; j++)
                {
                    if (consumed[j]) continue;
                    var b = kerbList[j];
                    if (b.edge == a.edge) continue;
                    float d2 = math.lengthsq(a.pos - b.pos);
                    if (d2 < bestD2) { bestD2 = d2; matchIdx = j; }
                }

                float3 rawPos;
                Entity eA, eB;
                if (matchIdx >= 0)
                {
                    var b = kerbList[matchIdx];
                    consumed[matchIdx] = true;
                    rawPos = (a.pos + b.pos) * 0.5f;
                    eA = a.edge; eB = b.edge;
                    if (eA.Index > eB.Index) (eA, eB) = (eB, eA);
                }
                else
                {
                    rawPos = a.pos;
                    eA = a.edge;
                    eB = Entity.Null;
                }

                float3 inward = rawPos - nodeCentre;
                float3 pulledPos = rawPos - inward * kCornerInwardPullFraction;
                pulledPos.y = rawPos.y;
                corners.Add(new MarkingCornerAnchor
                {
                    position = pulledPos,
                    edgeA = eA,
                    edgeB = eB,
                });
            }

            return corners;
        }

        public static List<MarkingEndpoint> Extract(EntityManager em, Entity node, bool log)
        {
            var results = new List<MarkingEndpoint>(16);
            if (node == Entity.Null || !em.Exists(node) || em.HasComponent<Deleted>(node)) return results;
            if (!em.HasBuffer<ConnectedEdge>(node)) return results;
            if (!em.HasComponent<Node>(node)) return results;

            var connected = em.GetBuffer<ConnectedEdge>(node, isReadOnly: true);
            if (log) Mod.log.Info($"extractor: node #{node.Index} has {connected.Length} ConnectedEdge(s)");
            for (int e = 0; e < connected.Length; e++)
            {
                ExtractForEdge(em, node, connected[e].m_Edge, results, log);
            }
            if (log) Mod.log.Info($"extractor: total endpoints = {results.Count}");
            return results;
        }

        private static void ExtractForEdge(EntityManager em, Entity node, Entity edgeEntity, List<MarkingEndpoint> outList, bool log)
        {
            if (edgeEntity == Entity.Null || !em.Exists(edgeEntity) || em.HasComponent<Deleted>(edgeEntity)) return;
            if (!em.HasComponent<Edge>(edgeEntity)) return;
            if (!em.HasComponent<Composition>(edgeEntity)) return;
            if (!em.HasComponent<EdgeGeometry>(edgeEntity)) return;

            var edge = em.GetComponentData<Edge>(edgeEntity);
            bool nodeIsStart = edge.m_Start == node;
            bool nodeIsEnd   = edge.m_End == node;
            if (!nodeIsStart && !nodeIsEnd) return;

            var composition = em.GetComponentData<Composition>(edgeEntity);
            Entity compEntity = composition.m_Edge;
            if (compEntity == Entity.Null || !em.Exists(compEntity) || em.HasComponent<Deleted>(compEntity)) return;
            if (!em.HasBuffer<NetCompositionLane>(compEntity)) return;
            if (!em.HasComponent<NetCompositionData>(compEntity)) return;

            var compLanes = em.GetBuffer<NetCompositionLane>(compEntity, isReadOnly: true);
            var compData = em.GetComponentData<NetCompositionData>(compEntity);
            float halfWidth = compData.m_Width * 0.5f;

            var edgeGeom = em.GetComponentData<EdgeGeometry>(edgeEntity);
            Bezier4x3 capLeftCurve, capRightCurve;
            if (nodeIsStart)
            {
                capLeftCurve  = edgeGeom.m_Start.m_Left;
                capRightCurve = edgeGeom.m_Start.m_Right;
            }
            else
            {
                capLeftCurve  = edgeGeom.m_End.m_Left;
                capRightCurve = edgeGeom.m_End.m_Right;
            }
            float3 leftAtNode  = nodeIsStart ? capLeftCurve.a  : capLeftCurve.d;
            float3 rightAtNode = nodeIsStart ? capRightCurve.a : capRightCurve.d;
            
            var lanesByX = new SortedDictionary<float, (float hw, Entity lane)>(); 
            for (int i = 0; i < compLanes.Length; i++)
            {
                var cl = compLanes[i];
                if ((cl.m_Flags & LaneFlags.Road) == 0) continue;
                if ((cl.m_Flags & (LaneFlags.Secondary | LaneFlags.Utility | LaneFlags.Master)) != 0) continue;

                float x = cl.m_Position.x;
                float laneHalfWidth = 0f;
                if (em.HasComponent<NetLaneData>(cl.m_Lane))
                {
                    var nld = em.GetComponentData<NetLaneData>(cl.m_Lane);
                    laneHalfWidth = nld.m_Width * 0.5f;
                }
                if (lanesByX.TryGetValue(x, out var existing))
                    lanesByX[x] = (math.max(existing.hw, laneHalfWidth), existing.lane);
                else
                    lanesByX[x] = (laneHalfWidth, cl.m_Lane);
            }

            if (lanesByX.Count == 0) return;

            var sorted = new List<(float x, float hw, Entity lane)>(lanesByX.Count);
            foreach (var kv in lanesByX) sorted.Add((kv.Key, kv.Value.hw, kv.Value.lane));
            var emittedX = new List<float>(sorted.Count + 3);

            float xLeftKerb = sorted[0].x - sorted[0].hw;
            int gap = 0;
            outList.Add(MakeEndpoint(em, edgeEntity, gap++, xLeftKerb, halfWidth, leftAtNode, rightAtNode, capLeftCurve, capRightCurve, nodeIsStart));
            emittedX.Add(xLeftKerb);

            for (int i = 1; i < sorted.Count; i++)
            {
                float prevRightEdge = sorted[i - 1].x + sorted[i - 1].hw;
                float nextLeftEdge  = sorted[i].x - sorted[i].hw;
                if (nextLeftEdge - prevRightEdge > kCarriagewayGapM)
                {
                    outList.Add(MakeEndpoint(em, edgeEntity, gap++, prevRightEdge, halfWidth, leftAtNode, rightAtNode, capLeftCurve, capRightCurve, nodeIsStart));
                    outList.Add(MakeEndpoint(em, edgeEntity, gap++, nextLeftEdge, halfWidth, leftAtNode, rightAtNode, capLeftCurve, capRightCurve, nodeIsStart));
                    emittedX.Add(prevRightEdge);
                    emittedX.Add(nextLeftEdge);
                }
                else
                {
                    float xMid = (prevRightEdge + nextLeftEdge) * 0.5f;
                    outList.Add(MakeEndpoint(em, edgeEntity, gap++, xMid, halfWidth, leftAtNode, rightAtNode, capLeftCurve, capRightCurve, nodeIsStart));
                    emittedX.Add(xMid);
                }
            }

            int last = sorted.Count - 1;
            float xRightKerb = sorted[last].x + sorted[last].hw;
            outList.Add(MakeEndpoint(em, edgeEntity, gap, xRightKerb, halfWidth, leftAtNode, rightAtNode, capLeftCurve, capRightCurve, nodeIsStart));
            emittedX.Add(xRightKerb);

            int classicCount = emittedX.Count;
            if (TryFindParamAtDistance(capLeftCurve, capRightCurve, nodeIsStart, kSetbackDistanceM, out float tSet))
            {
                float3 setLeft   = MathUtils.Position(capLeftCurve, tSet);
                float3 setRight  = MathUtils.Position(capRightCurve, tSet);
                float3 tanLeft   = MathUtils.Tangent(capLeftCurve, tSet);
                float3 tanRight  = MathUtils.Tangent(capRightCurve, tSet);
                for (int i = 0; i < classicCount; i++)
                {
                    float f = math.saturate((emittedX[i] + halfWidth) / math.max(0.001f, halfWidth * 2f));
                    float3 pos  = math.lerp(setLeft, setRight, f);
                    float3 tan3 = math.lerp(tanLeft, tanRight, f);
                    if (!nodeIsStart) tan3 = -tan3;
                    outList.Add(new MarkingEndpoint
                    {
                        edge     = edgeEntity,
                        gapIndex = kSetbackGapBase + i,
                        position = pos,
                        tangent  = math.normalizesafe(tan3.xz),
                    });
                }
            }
            var extendedEdges = new SortedSet<float>();
            for (int i = 0; i < compLanes.Length; i++)
            {
                var cl = compLanes[i];
                if ((cl.m_Flags & LaneFlags.Parking) == 0) continue;
                if ((cl.m_Flags & (LaneFlags.Secondary | LaneFlags.Utility | LaneFlags.Master)) != 0) continue;
                float hw = 0f;
                if (em.HasComponent<NetLaneData>(cl.m_Lane))
                    hw = em.GetComponentData<NetLaneData>(cl.m_Lane).m_Width * 0.5f;
                if (hw <= 0f) continue;
                extendedEdges.Add(cl.m_Position.x - hw);
                extendedEdges.Add(cl.m_Position.x + hw);
            }
            int extGap = kExtendedGapBase;
            foreach (float x in extendedEdges)
            {
                bool duplicate = false;
                for (int i = 0; i < emittedX.Count; i++)
                {
                    if (math.abs(emittedX[i] - x) < kExtendedDedupeM) { duplicate = true; break; }
                }
                int g = extGap++;
                if (duplicate) continue;
                outList.Add(MakeEndpoint(em, edgeEntity, g, x, halfWidth, leftAtNode, rightAtNode, capLeftCurve, capRightCurve, nodeIsStart));
                emittedX.Add(x);
            }
        }

        private static MarkingEndpoint MakeEndpoint(EntityManager em, Entity edge, int gapIndex, float lateralX, float halfWidth, float3 leftAtNode, float3 rightAtNode, in Bezier4x3 capLeftCurve, in Bezier4x3 capRightCurve, bool nodeIsStart)
        {
            float t = math.saturate((lateralX + halfWidth) / math.max(0.001f, halfWidth * 2f));
            float3 pos = math.lerp(leftAtNode, rightAtNode, t);
            
            float3 tanLeft = nodeIsStart ? MathUtils.Tangent(capLeftCurve, 0f) : -MathUtils.Tangent(capLeftCurve, 1f);
            float3 tanRight = nodeIsStart ? MathUtils.Tangent(capRightCurve, 0f) : -MathUtils.Tangent(capRightCurve, 1f);
            float3 tan3 = math.lerp(tanLeft, tanRight, t);
            if (math.lengthsq(tan3.xz) < 1e-6f && em.HasComponent<Curve>(edge))
            {
                var edgeCurve = em.GetComponentData<Curve>(edge);
                tan3 = nodeIsStart ? MathUtils.Tangent(edgeCurve.m_Bezier, 0f) : -MathUtils.Tangent(edgeCurve.m_Bezier, 1f);
            }
            float2 laneTangent = math.normalizesafe(tan3.xz);
            
            return new MarkingEndpoint { edge = edge, gapIndex = gapIndex, position = pos, tangent = laneTangent };
        }

        private static bool TryFindParamAtDistance(in Bezier4x3 left, in Bezier4x3 right, bool fromStart, float distance, out float t)
        {
            const int kSamples = 24;
            t = 0f;
            float acc = 0f;
            float prevT = fromStart ? 0f : 1f;
            float3 prev = (MathUtils.Position(left, prevT) + MathUtils.Position(right, prevT)) * 0.5f;
            for (int i = 1; i <= kSamples; i++)
            {
                float raw = i / (float)kSamples;
                float tt = fromStart ? raw : 1f - raw;
                float3 cur = (MathUtils.Position(left, tt) + MathUtils.Position(right, tt)) * 0.5f;
                float step = math.distance(prev.xz, cur.xz);
                if (acc + step >= distance)
                {
                    float frac = step > 1e-6f ? (distance - acc) / step : 0f;
                    t = math.lerp(prevT, tt, frac);
                    return true;
                }
                acc += step;
                prev = cur;
                prevT = tt;
            }
            return false;
        }
    }
}
