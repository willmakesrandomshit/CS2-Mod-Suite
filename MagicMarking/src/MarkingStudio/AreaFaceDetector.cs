using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;

namespace MarkingStudio
{
    internal sealed class PaintFace
    {
        public readonly List<PaintVertex> Vertices = new List<PaintVertex>();
        public readonly List<float3> Ring = new List<float3>();
        public float Area;
    }

    internal struct PaintVertex
    {
        public byte Kind;
        public int RefIndex;
        public Entity EdgeA;
        public Entity EdgeB;
        public int Gap;
        public float3 Position;
        public int LineToNext;
    }

    /// <summary>Turns the editor's logical marking curves into bounded planar faces for the
    /// one-click paint bucket. Each vertex retains the stable anchor identity used by saves.</summary>
    internal static class AreaFaceDetector
    {
        private sealed class GraphVertex
        {
            public PaintVertex Anchor;
            public readonly List<int> Outgoing = new List<int>();
        }

        private sealed class HalfEdge
        {
            public int From, To, Reverse, Line;
            public float T0, T1, Angle;
        }

        private struct LinePoint
        {
            public float T;
            public int Vertex;
        }

        public static List<PaintFace> Detect(EntityManager em, Entity node)
        {
            var result = new List<PaintFace>();
            if (node == Entity.Null || !em.Exists(node) || !em.HasBuffer<MarkingLine>(node)) return result;
            var endpoints = MarkingEndpointExtractor.Extract(em, node);
            var linesBuffer = em.GetBuffer<MarkingLine>(node, true);
            var lines = new MarkingLine[linesBuffer.Length];
            var curves = new Bezier4x3[lines.Length];
            var valid = new bool[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = linesBuffer[i];
                valid[i] = MarkingCurveBuilder.TryBuild(endpoints, lines[i], out curves[i]);
            }

            var vertices = new List<GraphVertex>();
            var endpointIds = new Dictionary<string, int>();
            var spatialIds = new List<int>();
            var points = new List<LinePoint>[lines.Length];
            for (int i = 0; i < points.Length; i++) points[i] = new List<LinePoint>();

            int EndpointVertex(Entity edge, int gap, float3 position)
            {
                string key = edge.Index + ":" + edge.Version + ":" + gap;
                if (endpointIds.TryGetValue(key, out int id)) return id;
                int legacy = FindEndpoint(endpoints, edge, gap);
                id = vertices.Count;
                vertices.Add(new GraphVertex { Anchor = new PaintVertex { Kind = 0, RefIndex = legacy, EdgeA = edge, EdgeB = Entity.Null, Gap = gap, Position = position } });
                endpointIds[key] = id;
                return id;
            }

            int IntersectionVertex(MarkingIntersectionAnchor hit)
            {
                for (int i = 0; i < spatialIds.Count; i++)
                {
                    int id = spatialIds[i];
                    if (DistanceSqXZ(vertices[id].Anchor.Position, hit.position) < 0.04f) return id;
                }
                int created = vertices.Count;
                vertices.Add(new GraphVertex { Anchor = new PaintVertex { Kind = 2, RefIndex = hit.PackedRef, EdgeA = Entity.Null, EdgeB = Entity.Null, Gap = 0, Position = hit.position } });
                spatialIds.Add(created);
                return created;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (!valid[i]) continue;
                int a = EndpointVertex(lines[i].sourceEdge, lines[i].sourceGapIndex, curves[i].a);
                int b = EndpointVertex(lines[i].targetEdge, lines[i].targetGapIndex, curves[i].d);
                points[i].Add(new LinePoint { T = 0f, Vertex = a });
                points[i].Add(new LinePoint { T = 1f, Vertex = b });
            }

            foreach (MarkingIntersectionAnchor hit in MarkingIntersectionExtractor.ExtractAll(em, node))
            {
                if (hit.lineA < 0 || hit.lineB >= lines.Length || !valid[hit.lineA] || !valid[hit.lineB]) continue;
                int id = IntersectionVertex(hit);
                points[hit.lineA].Add(new LinePoint { T = hit.tA, Vertex = id });
                points[hit.lineB].Add(new LinePoint { T = hit.tB, Vertex = id });
            }

            var halfEdges = new List<HalfEdge>();
            for (int line = 0; line < lines.Length; line++)
            {
                if (!valid[line]) continue;
                List<LinePoint> ordered = points[line].OrderBy(x => x.T).ToList();
                for (int i = 0; i + 1 < ordered.Count; i++)
                {
                    if (ordered[i].Vertex == ordered[i + 1].Vertex || ordered[i + 1].T - ordered[i].T < 0.001f) continue;
                    AddPair(vertices, halfEdges, curves[line], line, ordered[i], ordered[i + 1]);
                }
            }

            for (int i = 0; i < vertices.Count; i++)
                vertices[i].Outgoing.Sort((x, y) => halfEdges[x].Angle.CompareTo(halfEdges[y].Angle));

            var used = new bool[halfEdges.Count];
            for (int start = 0; start < halfEdges.Count; start++)
            {
                if (used[start]) continue;
                var edgeCycle = new List<int>();
                int current = start;
                for (int guard = 0; guard <= halfEdges.Count; guard++)
                {
                    if (used[current] && current != start) { edgeCycle.Clear(); break; }
                    used[current] = true;
                    edgeCycle.Add(current);
                    HalfEdge edge = halfEdges[current];
                    List<int> outgoing = vertices[edge.To].Outgoing;
                    int reverseAt = outgoing.IndexOf(edge.Reverse);
                    if (reverseAt < 0) { edgeCycle.Clear(); break; }
                    current = outgoing[(reverseAt - 1 + outgoing.Count) % outgoing.Count];
                    if (current == start) break;
                }
                if (edgeCycle.Count < 3 || current != start) continue;

                PaintFace face = BuildFace(vertices, halfEdges, curves, edgeCycle);
                // With the left-face traversal, bounded cells are counter-clockwise and the
                // infinite exterior is clockwise. Tiny slivers are not useful paint targets.
                if (face.Area > 1f && face.Ring.Count >= 3) result.Add(face);
            }
            return result.OrderBy(x => x.Area).ToList();
        }

        public static int FindContaining(IReadOnlyList<PaintFace> faces, float3 point)
        {
            for (int i = 0; i < faces.Count; i++) if (Contains(faces[i].Ring, point)) return i;
            return -1;
        }

        private static PaintFace BuildFace(List<GraphVertex> vertices, List<HalfEdge> edges, Bezier4x3[] curves, List<int> cycle)
        {
            var face = new PaintFace();
            for (int i = 0; i < cycle.Count; i++)
            {
                HalfEdge edge = edges[cycle[i]];
                PaintVertex anchor = vertices[edge.From].Anchor;
                anchor.LineToNext = edge.Line;
                face.Vertices.Add(anchor);
                const int samples = 8;
                for (int s = 0; s < samples; s++)
                {
                    float t = math.lerp(edge.T0, edge.T1, s / (float)samples);
                    face.Ring.Add(Position(curves[edge.Line], t));
                }
            }
            face.Area = SignedArea(face.Ring);
            return face;
        }

        private static void AddPair(List<GraphVertex> vertices, List<HalfEdge> edges, Bezier4x3 curve, int line, LinePoint a, LinePoint b)
        {
            int forward = edges.Count, reverse = forward + 1;
            float3 aNext = Position(curve, math.lerp(a.T, b.T, 0.02f));
            float3 bNext = Position(curve, math.lerp(b.T, a.T, 0.02f));
            edges.Add(new HalfEdge { From = a.Vertex, To = b.Vertex, Reverse = reverse, Line = line, T0 = a.T, T1 = b.T, Angle = Angle(vertices[a.Vertex].Anchor.Position, aNext) });
            edges.Add(new HalfEdge { From = b.Vertex, To = a.Vertex, Reverse = forward, Line = line, T0 = b.T, T1 = a.T, Angle = Angle(vertices[b.Vertex].Anchor.Position, bNext) });
            vertices[a.Vertex].Outgoing.Add(forward);
            vertices[b.Vertex].Outgoing.Add(reverse);
        }

        private static int FindEndpoint(IReadOnlyList<MarkingEndpoint> endpoints, Entity edge, int gap)
        {
            for (int i = 0; i < endpoints.Count; i++) if (endpoints[i].edge == edge && endpoints[i].gapIndex == gap) return i;
            return -1;
        }

        private static float3 Position(Bezier4x3 b, float t)
        {
            float u = 1f - t;
            return b.a * (u * u * u) + b.b * (3f * u * u * t) + b.c * (3f * u * t * t) + b.d * (t * t * t);
        }

        private static float Angle(float3 a, float3 b) => math.atan2(b.z - a.z, b.x - a.x);
        private static float DistanceSqXZ(float3 a, float3 b) { float x = a.x - b.x, z = a.z - b.z; return x * x + z * z; }

        private static float SignedArea(IReadOnlyList<float3> ring)
        {
            float sum = 0f;
            for (int i = 0; i < ring.Count; i++) { float3 a = ring[i], b = ring[(i + 1) % ring.Count]; sum += a.x * b.z - b.x * a.z; }
            return sum * 0.5f;
        }

        private static bool Contains(IReadOnlyList<float3> ring, float3 p)
        {
            bool inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                float3 a = ring[i], b = ring[j];
                if (((a.z > p.z) != (b.z > p.z)) && p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z + 1e-7f) + a.x) inside = !inside;
            }
            return inside;
        }
    }
}
