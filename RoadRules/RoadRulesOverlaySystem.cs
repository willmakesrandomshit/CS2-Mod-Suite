using Colossal.Mathematics;
using Game;
using Game.Common;
using Game.Net;
using Game.Rendering;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace RoadRules
{
    /// <summary>
    /// Renders real-time in-world visual overlays for Road Rules:
    /// - Cyan highlight on the hovered road edge
    /// - Outline highlight on the currently selected road edge
    /// - Clean, single-strip overlay per PHYSICAL road lane
    /// - Distinct visual states for Selected, Closed (Red), Transit/Local (Orange), and Active (Yellow)
    /// </summary>
    public partial class RoadRulesOverlaySystem : GameSystemBase
    {
        private ToolSystem m_ToolSystem;
        private RoadRulesToolSystem m_RoadRulesTool;
        private RoadRulesSystem m_RulesSystem;
        private OverlayRenderSystem m_OverlayRenderSystem;

        private static readonly Color kHoverColor = new Color(0.2f, 0.85f, 1f, 0.75f);
        private static readonly Color kSelectedEdgeColor = new Color(0.2f, 0.95f, 0.4f, 0.4f);
        private static readonly Color kOtherCarriagewayLaneColor = new Color(0.4f, 0.7f, 1f, 0.45f);
        private static readonly Color kSelectedLaneActiveColor = new Color(1f, 0.85f, 0.1f, 0.95f);
        private static readonly Color kSelectedLaneClosedColor = new Color(0.95f, 0.2f, 0.2f, 0.95f);
        private static readonly Color kSelectedLaneSpecialColor = new Color(0.95f, 0.6f, 0.1f, 0.95f);

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_RoadRulesTool = World.GetOrCreateSystemManaged<RoadRulesToolSystem>();
            m_RulesSystem = World.GetOrCreateSystemManaged<RoadRulesSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
        }

        protected override void OnUpdate()
        {
            if (m_ToolSystem == null || m_RoadRulesTool == null || m_OverlayRenderSystem == null) return;
            if (m_ToolSystem.activeTool != m_RoadRulesTool) return;

            var buffer = m_OverlayRenderSystem.GetBuffer(out var dependencies);
            dependencies.Complete();

            // 1. Highlight Selected Edge & Carriageway
            Entity selectedEdge = m_RulesSystem?.SelectedEdge ?? Entity.Null;
            if (selectedEdge != Entity.Null && EntityManager.Exists(selectedEdge) && !EntityManager.HasComponent<Deleted>(selectedEdge))
            {
                if (EntityManager.HasComponent<Curve>(selectedEdge))
                {
                    Curve edgeCurve = EntityManager.GetComponentData<Curve>(selectedEdge);
                    buffer.DrawCurve(kSelectedEdgeColor, edgeCurve.m_Bezier, 3.5f);
                }

                // 2. Render all editable PHYSICAL lanes for this carriageway
                var record = m_RulesSystem?.CurrentRecord;
                if (record != null && record.PhysicalLanes != null && record.PhysicalLanes.Count > 0)
                {
                    int selectedIdx = m_RulesSystem.SelectedLaneIndex;

                    for (int i = 0; i < record.PhysicalLanes.Count; i++)
                    {
                        var physicalLane = record.PhysicalLanes[i];
                        var bezier = physicalLane.DisplayCurve.m_Bezier;

                        // Safety check on bezier curve
                        if (math.lengthsq(bezier.d - bezier.a) < 0.01f) continue;

                        if (i == selectedIdx)
                        {
                            // Selected Physical Lane: Bright, high-contrast overlay
                            Color highlightColor = physicalLane.IsClosed ? kSelectedLaneClosedColor :
                                                   (physicalLane.LocalAccessOnly || (physicalLane.AllowedVehicles & VehicleAccessFlags.PrivateCars) == 0) ? kSelectedLaneSpecialColor :
                                                   kSelectedLaneActiveColor;

                            buffer.DrawCurve(highlightColor, bezier, 3.2f);
                        }
                        else
                        {
                            // Other physical lanes in the carriageway: subtle context lines
                            Color otherColor = physicalLane.IsClosed ? new Color(0.8f, 0.2f, 0.2f, 0.45f) : kOtherCarriagewayLaneColor;
                            buffer.DrawCurve(otherColor, bezier, 1.6f);
                        }
                    }
                }
            }

            // 3. Highlight Hovered Edge (if different from selected)
            Entity hoveredEdge = m_RoadRulesTool.HoveredEdge;
            if (hoveredEdge != Entity.Null && hoveredEdge != selectedEdge && EntityManager.Exists(hoveredEdge) && !EntityManager.HasComponent<Deleted>(hoveredEdge))
            {
                if (EntityManager.HasComponent<Curve>(hoveredEdge))
                {
                    Curve hoveredCurve = EntityManager.GetComponentData<Curve>(hoveredEdge);
                    buffer.DrawCurve(kHoverColor, hoveredCurve.m_Bezier, 2.5f);
                }
            }
        }
    }
}
