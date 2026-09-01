using System.Collections.Generic;
using Colossal.Logging;
using Colossal.Mathematics;
using Game;
using Game.Net;
using Game.Rendering;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MarkingStudio
{
    /// <summary>
    /// Magic Marking 2.2 Overlay System.
    /// Draws:
    ///   - Endpoint connector dots with clear snap indicator highlights
    ///   - Real-time drag preview matching selected DrawingMode (Follow Road / Straight / Custom Curve)
    ///   - Committed lines and segments with hover affordance
    ///   - Area polygon contours, paint faces, and piece outlines
    ///   - Precision snap indicators for Lane Boundary, Road Edge, Median Edge, Existing Marking, etc.
    /// </summary>
    public partial class MarkingOverlaySystem : GameSystemBase
    {
        private static readonly ILog log = Mod.log;

        private ToolSystem _toolSystem;
        private MarkingNodeToolSystem _tool;
        private OverlayRenderSystem _overlayRenderSystem;
        private MarkingStudioUISystem _uiSystem;
        private EntityQuery _nodesWithPairsQuery;
        private TerrainSystem _terrainSystem;
        private TerrainHeightData _heightData;

        // Overlay colors and styles
        private const float kHiddenSegmentWidth = 0.14f;
        private static readonly Color kColHiddenSegment = new Color(1.00f, 0.35f, 0.35f, 0.30f);

        private const float kHighlightedPairCurveWidth = 0.08f;
        private static readonly Color kColHighlightedCurve = new Color(0.40f, 0.90f, 1.00f, 0.85f);
        private static readonly Color kColHighlightedHidden = new Color(1.00f, 0.55f, 0.55f, 0.65f);

        private const float kPreviewCurveWidth = 0.10f;
        private static readonly Color kColPreviewCurve = new Color(1.00f, 1.00f, 1.00f, 0.55f);

        private const float kDotDiameter = 0.65f;
        private const float kDotOutlineWidth = 0.10f;
        private const float kDotFreeFillAlpha = 0.14f;
        private const float kDotConnectedCoreDiameter = 0.20f;
        private static readonly Color kColDotConnectedCore = new Color(1.00f, 1.00f, 1.00f, 0.90f);
        private static readonly Color kColDotOutline = new Color(0.06f, 0.08f, 0.12f, 0.90f);
        private static readonly Color kColDotFillSource = new Color(1.00f, 1.00f, 1.00f, 0.95f);
        private static readonly Color kColDotOutlineSrc = new Color(0.10f, 0.10f, 0.10f, 1.00f);

        private const float kDotDiameterHover = 0.95f;
        private const float kDotOutlineWidthHover = 0.11f;

        // Snap target indicator colors
        private static readonly Color kColSnapLaneBoundary = new Color(0.40f, 0.90f, 1.00f, 0.95f);
        private static readonly Color kColSnapRoadEdge = new Color(1.00f, 0.65f, 0.20f, 0.95f);
        private static readonly Color kColSnapMedianEdge = new Color(1.00f, 0.90f, 0.30f, 0.95f);
        private static readonly Color kColSnapExistingMarking = new Color(0.40f, 1.00f, 0.50f, 0.95f);
        private static readonly Color kColSnapIntersection = new Color(1.00f, 0.40f, 0.85f, 0.95f);

        private static readonly Color[] kEdgePalette = new[]
        {
            new Color(1.00f, 0.55f, 0.20f, 0.85f), // orange
            new Color(0.40f, 0.75f, 1.00f, 0.85f), // sky blue
            new Color(0.55f, 0.95f, 0.55f, 0.85f), // mint green
            new Color(1.00f, 0.40f, 0.75f, 0.85f), // pink
            new Color(0.80f, 0.65f, 1.00f, 0.85f), // lavender
            new Color(1.00f, 0.90f, 0.40f, 0.85f), // yellow
            new Color(0.50f, 0.95f, 0.90f, 0.85f), // teal
            new Color(0.95f, 0.65f, 0.50f, 0.85f), // salmon
        };

        private static Color EdgeDotColor(Entity edge)
        {
            int idx = (edge.Index & 0x7fffffff) % kEdgePalette.Length;
            return kEdgePalette[idx];
        }

        private const float kIntersectionDotDiameter = 0.32f;
        private const float kIntersectionDotOutlineWidth = 0.06f;
        private static readonly Color kColIntersection = new Color(1.00f, 0.30f, 0.30f, 0.60f);
        private static readonly Color kColIntersectionOutline = new Color(0.25f, 0.05f, 0.05f, 0.80f);

        private const float kCornerDotDiameter = 0.55f;
        private const float kCornerDotOutlineWidth = 0.10f;
        private static readonly Color kColCornerFill = new Color(0.95f, 0.95f, 0.95f, 0.55f);
        private static readonly Color kColCornerOutline = new Color(0.15f, 0.20f, 0.25f, 0.90f);

        private const float kAreaCandDotDiameter = 0.90f;
        private const float kAreaCandDotOutlineWidth = 0.12f;
        private static readonly Color kColAreaCandFill = new Color(1.00f, 0.85f, 0.20f, 0.14f);
        private static readonly Color kColAreaCandRing = new Color(1.00f, 0.85f, 0.20f, 0.95f);
        private static readonly Color kColAreaCandOutline = new Color(0.20f, 0.16f, 0.04f, 0.95f);
        private static readonly Color kColAreaHoverFill = new Color(1.00f, 0.95f, 0.45f, 1.00f);
        private const float kAreaHoverDotDiameter = 1.15f;
        private const float kAreaPlacedDotDiameter = 1.10f;
        private static readonly Color kColAreaPlacedFill = new Color(1.00f, 1.00f, 1.00f, 0.95f);
        private const float kAreaContourWidth = 0.16f;
        private static readonly Color kColAreaContour = new Color(1.00f, 1.00f, 1.00f, 0.90f);
        private const float kAreaPreviewWidth = 0.13f;
        private static readonly Color kColAreaPreview = new Color(1.00f, 1.00f, 1.00f, 0.55f);
        private static readonly Color kColAreaPreviewClose = new Color(0.40f, 1.00f, 0.55f, 0.95f);
        private const float kAreaStartRingDiameter = 1.70f;
        private const float kAreaStartRingWidth = 0.14f;

        private const float kNodeHoverDiameter = 5.5f;
        private const float kNodeHoverOutlineWidth = 0.22f;
        private const float kNodeHasPairsDiameter = 3.6f;
        private const float kNodeHasPairsOutlineWidth = 0.12f;
        private static readonly Color kColNodeHoverRing = new Color(0.30f, 0.95f, 1.00f, 0.70f);
        private static readonly Color kColNodeHasPairsRing = new Color(0.45f, 1.00f, 0.55f, 0.55f);
        private static readonly Color kColTransparent = new Color(0f, 0f, 0f, 0f);

        private readonly HashSet<(Entity edge, int gapIndex)> _connectedScratch = new HashSet<(Entity, int)>();
        private readonly List<float3> _areaContourScratch = new List<float3>();

        protected override void OnCreate()
        {
            base.OnCreate();
            _toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            _tool = World.GetOrCreateSystemManaged<MarkingNodeToolSystem>();
            _overlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            _uiSystem = World.GetOrCreateSystemManaged<MarkingStudioUISystem>();
            _terrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            _nodesWithPairsQuery = GetEntityQuery(
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<MarkingLine>());
        }

        private OverlayRenderSystem.StyleFlags DotStyle(float3 pos)
        {
            float ground = TerrainUtils.SampleHeight(ref _heightData, pos);
            return pos.y - ground > 0.75f
                ? (OverlayRenderSystem.StyleFlags)0
                : OverlayRenderSystem.StyleFlags.Projected;
        }

        protected override void OnUpdate()
        {
            if (_tool == null || _toolSystem.activeTool != _tool) return;

            _heightData = _terrainSystem.GetHeightData();
            var buf = _overlayRenderSystem.GetBuffer(out JobHandle deps);
            JobHandle our = JobHandle.CombineDependencies(deps, Dependency);

            DrawHasPairsRings(buf, _tool.SelectedNode);

            if (_tool.ToolState == MarkingNodeToolSystem.State.Default && _tool.HoveredNode != Entity.Null)
            {
                DrawNodeRing(buf, _tool.HoveredNode, kColNodeHoverRing, kNodeHoverDiameter, kNodeHoverOutlineWidth);
            }

            if (_tool.ToolState == MarkingNodeToolSystem.State.AreaSelecting)
            {
                DrawAreaModeOverlay(buf);
                _overlayRenderSystem.AddBufferWriter(our);
                Dependency = our;
                return;
            }
            if (_tool.ToolState == MarkingNodeToolSystem.State.AreaPainting)
            {
                DrawPaintBucketOverlay(buf);
                _overlayRenderSystem.AddBufferWriter(our);
                Dependency = our;
                return;
            }

            var endpoints = _tool.Endpoints;
            if (endpoints == null || endpoints.Count == 0)
            {
                _overlayRenderSystem.AddBufferWriter(our);
                Dependency = our;
                return;
            }

            int sourceIdx = _tool.SourceEndpointIndex;
            int hoverIdx = _tool.HoveredEndpointIndex;

            int uiHoveredLine = _uiSystem?.UIHoveredLineIndex ?? -1;
            if (uiHoveredLine < 0) uiHoveredLine = _tool?.HoveredLineInGame ?? -1;
            int hoveredSegLine = _uiSystem?.UIHoveredSegmentLineIndex ?? -1;
            int hoveredSegIdx = _uiSystem?.UIHoveredSegmentIndex ?? -1;

            int hoveredArea = _uiSystem?.UIHoveredAreaIndex ?? -1;
            if (hoveredArea < 0) hoveredArea = _tool?.HoveredAreaInGame ?? -1;

            var node = _tool.SelectedNode;
            if (hoveredArea >= 0 && node != Entity.Null)
                DrawHoveredAreaOutline(buf, node, hoveredArea);

            // 1. Committed Lines & Segments
            if (node != Entity.Null && EntityManager.HasBuffer<MarkingLine>(node) && EntityManager.HasBuffer<MarkingSegment>(node))
            {
                var lines = EntityManager.GetBuffer<MarkingLine>(node, isReadOnly: true);
                var segs = EntityManager.GetBuffer<MarkingSegment>(node, isReadOnly: true);
                int lineCount = lines.Length;
                for (int l = 0; l < lineCount; l++)
                {
                    if (!MarkingCurveBuilder.TryBuild(endpoints, lines[l], out var full)) continue;
                    bool isLineHighlighted = (l == uiHoveredLine);
                    int perLineCounter = -1;
                    for (int s = 0; s < segs.Length; s++)
                    {
                        var seg = segs[s];
                        if (seg.lineIndex != l) continue;
                        perLineCounter++;
                        bool isThisSegmentHovered = (l == hoveredSegLine && perLineCounter == hoveredSegIdx);
                        bool isHighlighted = isLineHighlighted || isThisSegmentHovered;

                        bool draw = false;
                        Color color = default;
                        float width = 0f;
                        if (isHighlighted)
                        {
                            draw = true;
                            color = seg.visible ? kColHighlightedCurve : kColHighlightedHidden;
                            width = isThisSegmentHovered ? kHighlightedPairCurveWidth * 1.8f : kHighlightedPairCurveWidth;
                        }
                        else if (!seg.visible)
                        {
                            draw = true;
                            color = kColHiddenSegment;
                            width = kHiddenSegmentWidth;
                        }

                        if (draw)
                        {
                            var segBez = MathUtils.Cut(full, new float2(seg.tStart, seg.tEnd));
                            buf.DrawCurve(color, segBez, width);
                        }

                        if (seg.tStart > 0.001f && seg.tStart < 0.999f)
                            DrawIntersectionMarker(buf, MathUtils.Position(full, seg.tStart));
                        if (seg.tEnd > 0.001f && seg.tEnd < 0.999f)
                            DrawIntersectionMarker(buf, MathUtils.Position(full, seg.tEnd));
                    }
                }
            }

            // 2. Drag preview from source to target (or to free cursor)
            if (sourceIdx >= 0 && sourceIdx < endpoints.Count)
            {
                var src = endpoints[sourceIdx];
                if (hoverIdx >= 0 && hoverIdx < endpoints.Count && hoverIdx != sourceIdx)
                {
                    var dst = endpoints[hoverIdx];
                    var bezier = MarkingCurveBuilder.Build(src, dst, MarkingCurveBuilder.kPullFactor, _tool.CurrentDrawingMode, 0f);
                    buf.DrawCurve(kColPreviewCurve, bezier, kPreviewCurveWidth);
                }
                else
                {
                    float3 to = _tool.CursorWorldPos;
                    if (math.lengthsq(to - src.position) > 0.01f)
                    {
                        if (_tool.CurrentDrawingMode == MarkingDrawingMode.FollowRoad)
                        {
                            var bezier = MarkingCurveBuilder.Build(src.position, src.tangent, to, -src.tangent, MarkingCurveBuilder.kPullFactor, MarkingDrawingMode.FollowRoad, 0f);
                            buf.DrawCurve(kColPreviewCurve, bezier, kPreviewCurveWidth);
                        }
                        else
                        {
                            buf.DrawLine(kColPreviewCurve, new Line3.Segment(src.position, to), kPreviewCurveWidth);
                        }
                    }
                }
            }

            // 3. Endpoint dots & snap indicator highlights
            _connectedScratch.Clear();
            if (node != Entity.Null && EntityManager.HasBuffer<MarkingLine>(node))
            {
                var lines = EntityManager.GetBuffer<MarkingLine>(node, isReadOnly: true);
                for (int l = 0; l < lines.Length; l++)
                {
                    _connectedScratch.Add((lines[l].sourceEdge, lines[l].sourceGapIndex));
                    _connectedScratch.Add((lines[l].targetEdge, lines[l].targetGapIndex));
                }
            }

            for (int i = 0; i < endpoints.Count; i++)
            {
                var ep = endpoints[i];
                Color edgeColor = EdgeDotColor(ep.edge);
                bool connected = _connectedScratch.Contains((ep.edge, ep.gapIndex));
                Color fill;
                Color outline;
                float diameter = kDotDiameter;
                float outlineWidth = kDotOutlineWidth;

                if (i == sourceIdx)
                {
                    fill = kColDotFillSource;
                    outline = kColDotOutlineSrc;
                    buf.DrawCircle(
                        outlineColor: new Color(1f, 1f, 1f, 0.55f),
                        fillColor: kColTransparent,
                        outlineWidth: 0.10f,
                        styleFlags: DotStyle(ep.position),
                        direction: new float2(0f, 1f),
                        position: ep.position,
                        diameter: kDotDiameter * 2.2f);
                }
                else if (i == hoverIdx)
                {
                    // Snap Kind Color Accent
                    var snapRes = _tool.CurrentSnapResult;
                    Color snapColor = edgeColor;
                    if (snapRes.DidSnap)
                    {
                        if (snapRes.Kind == SnapTargetKind.LaneBoundary) snapColor = kColSnapLaneBoundary;
                        else if (snapRes.Kind == SnapTargetKind.RoadEdge) snapColor = kColSnapRoadEdge;
                        else if (snapRes.Kind == SnapTargetKind.MedianEdge) snapColor = kColSnapMedianEdge;
                        else if (snapRes.Kind == SnapTargetKind.MarkingEndpoint) snapColor = kColSnapExistingMarking;
                    }

                    fill = snapColor;
                    outline = kColDotOutline;
                    diameter = kDotDiameterHover;
                    outlineWidth = kDotOutlineWidthHover;

                    // Draw Snap Ring Halo
                    buf.DrawCircle(
                        outlineColor: new Color(snapColor.r, snapColor.g, snapColor.b, 0.60f),
                        fillColor: kColTransparent,
                        outlineWidth: 0.08f,
                        styleFlags: DotStyle(ep.position),
                        direction: new float2(0f, 1f),
                        position: ep.position,
                        diameter: kDotDiameterHover * 1.6f);
                }
                else if (connected)
                {
                    fill = edgeColor;
                    outline = kColDotOutline;
                }
                else
                {
                    fill = new Color(edgeColor.r, edgeColor.g, edgeColor.b, kDotFreeFillAlpha);
                    outline = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0.95f);
                }

                buf.DrawCircle(
                    outlineColor: outline,
                    fillColor: fill,
                    outlineWidth: outlineWidth,
                    styleFlags: DotStyle(ep.position),
                    direction: new float2(0f, 1f),
                    position: ep.position,
                    diameter: diameter);

                if (connected && i != sourceIdx)
                {
                    buf.DrawCircle(
                        outlineColor: kColTransparent,
                        fillColor: kColDotConnectedCore,
                        outlineWidth: 0f,
                        styleFlags: DotStyle(ep.position),
                        direction: new float2(0f, 1f),
                        position: ep.position,
                        diameter: kDotConnectedCoreDiameter);
                }
            }

            // 4. Corner anchors
            var corners = _tool.CornerAnchors;
            if (corners != null)
            {
                for (int i = 0; i < corners.Count; i++)
                {
                    buf.DrawCircle(
                        outlineColor: kColCornerOutline,
                        fillColor: kColCornerFill,
                        outlineWidth: kCornerDotOutlineWidth,
                        styleFlags: DotStyle(corners[i].position),
                        direction: new float2(0f, 1f),
                        position: corners[i].position,
                        diameter: kCornerDotDiameter);
                }
            }

            _overlayRenderSystem.AddBufferWriter(our);
            Dependency = our;
        }

        private void DrawAreaModeOverlay(OverlayRenderSystem.Buffer buf)
        {
            var poly = _tool.AreaPolygon;
            var hover = _tool.AreaHover;
            float3 cursor = _tool.CursorWorldPos;

            _tool.BuildAreaContourPath(_areaContourScratch);
            if (_areaContourScratch.Count >= 2)
            {
                for (int i = 0; i < _areaContourScratch.Count - 1; i++)
                {
                    buf.DrawLine(kColAreaContour, new Line3.Segment(_areaContourScratch[i], _areaContourScratch[i + 1]), kAreaContourWidth);
                }
            }

            if (poly != null && poly.Count > 0)
            {
                float3 lastPos = poly[poly.Count - 1].position;
                bool canCloseOnHover = hover.IsValid && poly.Count >= 3 && hover.kind == poly[0].kind && hover.refIndex == poly[0].refIndex;
                float3 previewTarget = canCloseOnHover ? poly[0].position : cursor;
                Color previewCol = canCloseOnHover ? kColAreaPreviewClose : kColAreaPreview;

                if (math.lengthsq(previewTarget - lastPos) > 0.01f)
                {
                    buf.DrawLine(previewCol, new Line3.Segment(lastPos, previewTarget), kAreaPreviewWidth);
                }

                if (poly.Count >= 3)
                {
                    buf.DrawCircle(
                        outlineColor: canCloseOnHover ? kColAreaPreviewClose : kColAreaCandRing,
                        fillColor: kColTransparent,
                        outlineWidth: kAreaStartRingWidth,
                        styleFlags: DotStyle(poly[0].position),
                        direction: new float2(0f, 1f),
                        position: poly[0].position,
                        diameter: kAreaStartRingDiameter);
                }
            }

            var endpoints = _tool.Endpoints;
            if (endpoints != null)
            {
                for (int i = 0; i < endpoints.Count; i++)
                {
                    var ep = endpoints[i];
                    bool isHovered = hover.IsValid && hover.kind == MarkingNodeToolSystem.AreaAnchorKind.LaneEndpoint && hover.refIndex == i;
                    buf.DrawCircle(
                        outlineColor: isHovered ? kColDotOutline : kColAreaCandOutline,
                        fillColor: isHovered ? kColAreaHoverFill : kColAreaCandFill,
                        outlineWidth: isHovered ? kDotOutlineWidthHover : kAreaCandDotOutlineWidth,
                        styleFlags: DotStyle(ep.position),
                        direction: new float2(0f, 1f),
                        position: ep.position,
                        diameter: isHovered ? kAreaHoverDotDiameter : kAreaCandDotDiameter);
                }
            }
        }

        private void DrawPaintBucketOverlay(OverlayRenderSystem.Buffer buf)
        {
            var ring = _tool.HoveredPaintFaceRing;
            if (ring != null && ring.Count >= 3)
            {
                for (int i = 0; i < ring.Count; i++)
                {
                    float3 a = ring[i];
                    float3 b = ring[(i + 1) % ring.Count];
                    buf.DrawLine(kColAreaPreviewClose, new Line3.Segment(a, b), 0.18f);
                }
            }
        }

        private void DrawHoveredAreaOutline(OverlayRenderSystem.Buffer buf, Entity node, int areaIndex)
        {
            if (!EntityManager.HasBuffer<MarkingAreaPiece>(node) || !EntityManager.HasBuffer<MarkingAreaPieceVertex>(node)) return;
            var pieces = EntityManager.GetBuffer<MarkingAreaPiece>(node, isReadOnly: true);
            var verts = EntityManager.GetBuffer<MarkingAreaPieceVertex>(node, isReadOnly: true);

            for (int p = 0; p < pieces.Length; p++)
            {
                var piece = pieces[p];
                if (piece.areaIndex != areaIndex || piece.vertexCount < 3) continue;

                for (int v = 0; v < piece.vertexCount; v++)
                {
                    int i0 = piece.firstVertex + v;
                    int i1 = piece.firstVertex + ((v + 1) % piece.vertexCount);
                    if (i0 < verts.Length && i1 < verts.Length)
                    {
                        buf.DrawLine(kColHighlightedCurve, new Line3.Segment(verts[i0].position, verts[i1].position), 0.14f);
                    }
                }
            }
        }

        private void DrawIntersectionMarker(OverlayRenderSystem.Buffer buf, float3 pos)
        {
            buf.DrawCircle(
                outlineColor: kColIntersectionOutline,
                fillColor: kColIntersection,
                outlineWidth: kIntersectionDotOutlineWidth,
                styleFlags: DotStyle(pos),
                direction: new float2(0f, 1f),
                position: pos,
                diameter: kIntersectionDotDiameter);
        }

        private void DrawHasPairsRings(OverlayRenderSystem.Buffer buf, Entity selectedNode)
        {
            if (_nodesWithPairsQuery.IsEmptyIgnoreFilter) return;
            using var nodes = _nodesWithPairsQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                if (n == selectedNode) continue;
                if (!EntityManager.HasBuffer<MarkingLine>(n)) continue;
                var bufL = EntityManager.GetBuffer<MarkingLine>(n, isReadOnly: true);
                if (bufL.Length == 0) continue;
                DrawNodeRing(buf, n, kColNodeHasPairsRing, kNodeHasPairsDiameter, kNodeHasPairsOutlineWidth);
            }
        }

        private void DrawNodeRing(OverlayRenderSystem.Buffer buf, Entity node, Color col, float diameter, float width)
        {
            if (!EntityManager.HasComponent<Node>(node)) return;
            float3 pos = EntityManager.GetComponentData<Node>(node).m_Position;
            buf.DrawCircle(
                outlineColor: col,
                fillColor: kColTransparent,
                outlineWidth: width,
                styleFlags: DotStyle(pos),
                direction: new float2(0f, 1f),
                position: pos,
                diameter: diameter);
        }
    }
}
