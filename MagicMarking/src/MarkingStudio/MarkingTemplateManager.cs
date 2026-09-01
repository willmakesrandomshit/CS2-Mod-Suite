using System;
using System.Collections.Generic;
using System.IO;
using Colossal.Logging;
using Game.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace MarkingStudio
{
    public struct TemplateLineItem
    {
        public int Style;
        public float Curvature;
        public float SourceOffset;
        public float TargetOffset;
    }

    public struct TemplateAreaItem
    {
        public int StyleId;
        public int VertexCount;
        public float Spacing;
        public float Angle;
    }

    public class MarkingTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsBuiltIn { get; set; }
        public List<TemplateLineItem> Lines { get; set; } = new List<TemplateLineItem>();
        public List<TemplateAreaItem> Areas { get; set; } = new List<TemplateAreaItem>();
    }

    /// <summary>
    /// Magic Marking 2.1 Marking Template & Preset Manager.
    /// Manages built-in and user-created marking templates with adaptive target geometry mapping.
    /// </summary>
    public class MarkingTemplateManager
    {
        private static readonly ILog log = Mod.log;
        public static MarkingTemplateManager Instance { get; } = new MarkingTemplateManager();

        private readonly List<MarkingTemplate> _templates = new List<MarkingTemplate>();

        public IReadOnlyList<MarkingTemplate> Templates => _templates;
        public event Action OnTemplatesChanged;

        private MarkingTemplateManager()
        {
            LoadBuiltInTemplates();
        }

        private void LoadBuiltInTemplates()
        {
            _templates.Clear();

            // 1. Standard T-Junction Stop & Yield
            _templates.Add(new MarkingTemplate
            {
                Id = "builtin_t_junction_stop_yield",
                Name = "Standard T-Junction Stop & Yield",
                Category = "Intersections",
                Description = "Transverse solid stop bar for minor approach with dashed yield guide lines.",
                IsBuiltIn = true,
                IsFavorite = true,
                Lines = new List<TemplateLineItem>
                {
                    new TemplateLineItem { Style = 0, Curvature = 0.0f, SourceOffset = 0.0f, TargetOffset = 1.0f },
                    new TemplateLineItem { Style = 1, Curvature = 0.25f, SourceOffset = 0.5f, TargetOffset = 0.5f }
                }
            });

            // 2. 4-Way Crossroad Crosswalks & Stop Bars
            _templates.Add(new MarkingTemplate
            {
                Id = "builtin_4way_crosswalks_stop",
                Name = "4-Way Crossroad Crosswalks & Stop Bars",
                Category = "Intersections",
                Description = "Complete 4-approach junction layout with zebra/ladder crosswalk boundaries and setback stop bars.",
                IsBuiltIn = true,
                IsFavorite = true,
                Lines = new List<TemplateLineItem>
                {
                    new TemplateLineItem { Style = 0, Curvature = 0.0f, SourceOffset = 0.0f, TargetOffset = 1.0f },
                    new TemplateLineItem { Style = 0, Curvature = 0.0f, SourceOffset = 0.0f, TargetOffset = 1.0f },
                    new TemplateLineItem { Style = 1, Curvature = 0.3f, SourceOffset = 0.0f, TargetOffset = 0.0f }
                }
            });

            // 3. Highway Exit Gore Chevron Island
            _templates.Add(new MarkingTemplate
            {
                Id = "builtin_highway_gore_chevron",
                Name = "Highway Exit Gore Chevron Island",
                Category = "Highways",
                Description = "Thick perimeter channelizing lines with 45-degree chevron herringbone hatching in the divergence zone.",
                IsBuiltIn = true,
                IsFavorite = true,
                Lines = new List<TemplateLineItem>
                {
                    new TemplateLineItem { Style = 0, Curvature = 0.15f, SourceOffset = 0.0f, TargetOffset = 0.8f },
                    new TemplateLineItem { Style = 0, Curvature = 0.15f, SourceOffset = 0.2f, TargetOffset = 1.0f }
                },
                Areas = new List<TemplateAreaItem>
                {
                    new TemplateAreaItem { StyleId = 2, VertexCount = 4, Spacing = 3.0f, Angle = 45.0f } // Chevron hatch
                }
            });

            // 4. Protected Left Turn Guide Curves
            _templates.Add(new MarkingTemplate
            {
                Id = "builtin_left_turn_guides",
                Name = "Protected Left Turn Guide Curves",
                Category = "Guides",
                Description = "Dual curved dashed trajectory lines guiding left-turning traffic through wide multi-lane intersections.",
                IsBuiltIn = true,
                IsFavorite = false,
                Lines = new List<TemplateLineItem>
                {
                    new TemplateLineItem { Style = 1, Curvature = 0.45f, SourceOffset = 0.2f, TargetOffset = 0.2f },
                    new TemplateLineItem { Style = 1, Curvature = 0.50f, SourceOffset = 0.4f, TargetOffset = 0.4f }
                }
            });

            // 5. Bus Stop Bay Zig-Zag Fills
            _templates.Add(new MarkingTemplate
            {
                Id = "builtin_bus_bay_zigzag",
                Name = "Bus Stop Bay Zig-Zag Fills",
                Category = "Transit",
                Description = "Yellow edge boundary with diagonal exclusion hatching demarcating transit loading berths.",
                IsBuiltIn = true,
                IsFavorite = false,
                Lines = new List<TemplateLineItem>
                {
                    new TemplateLineItem { Style = 0, Curvature = 0.0f, SourceOffset = 0.0f, TargetOffset = 1.0f }
                },
                Areas = new List<TemplateAreaItem>
                {
                    new TemplateAreaItem { StyleId = 1, VertexCount = 4, Spacing = 2.0f, Angle = 60.0f }
                }
            });

            // 6. Pedestrian Refuge Island Hatching
            _templates.Add(new MarkingTemplate
            {
                Id = "builtin_refuge_island",
                Name = "Pedestrian Refuge Island Hatching",
                Category = "Islands",
                Description = "Painted median island nose with tapered white perimeter and crosshatch filler.",
                IsBuiltIn = true,
                IsFavorite = false,
                Areas = new List<TemplateAreaItem>
                {
                    new TemplateAreaItem { StyleId = 3, VertexCount = 3, Spacing = 1.5f, Angle = 90.0f }
                }
            });

            // 7. Roundabout Entry Yield & Spiral Markings
            _templates.Add(new MarkingTemplate
            {
                Id = "builtin_roundabout_spiral",
                Name = "Roundabout Entry Yield & Spiral Markings",
                Category = "Roundabouts",
                Description = "Yield tooth boundary at circulatory entries and spiral lane division lines.",
                IsBuiltIn = true,
                IsFavorite = true,
                Lines = new List<TemplateLineItem>
                {
                    new TemplateLineItem { Style = 1, Curvature = 0.35f, SourceOffset = 0.0f, TargetOffset = 1.0f },
                    new TemplateLineItem { Style = 0, Curvature = 0.20f, SourceOffset = 0.5f, TargetOffset = 0.5f }
                }
            });
        }

        public MarkingTemplate SaveTemplateFromNode(EntityManager em, Entity node, string name, string category, string description)
        {
            if (!em.HasBuffer<MarkingLine>(node)) return null;

            var t = new MarkingTemplate
            {
                Id = "user_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = string.IsNullOrEmpty(name) ? "Custom Preset" : name,
                Category = string.IsNullOrEmpty(category) ? "Custom" : category,
                Description = description ?? "User created marking template.",
                IsBuiltIn = false,
                IsFavorite = false
            };

            var lines = em.GetBuffer<MarkingLine>(node);
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                t.Lines.Add(new TemplateLineItem
                {
                    Style = l.style,
                    Curvature = l.curvature,
                    SourceOffset = 0.0f,
                    TargetOffset = 1.0f
                });
            }

            if (em.HasBuffer<MarkingArea>(node))
            {
                var areas = em.GetBuffer<MarkingArea>(node);
                for (int a = 0; a < areas.Length; a++)
                {
                    var ar = areas[a];
                    t.Areas.Add(new TemplateAreaItem
                    {
                        StyleId = ar.styleId,
                        VertexCount = ar.vertexCount,
                        Spacing = 2.0f,
                        Angle = 45.0f
                    });
                }
            }

            _templates.Add(t);
            OnTemplatesChanged?.Invoke();
            log.Info($"Saved custom template '{t.Name}' with {t.Lines.Count} lines and {t.Areas.Count} areas.");
            return t;
        }

        public bool ApplyTemplateToNode(EntityManager em, Entity node, string templateId)
        {
            var t = _templates.Find(x => x.Id == templateId);
            if (t == null || node == Entity.Null || !em.Exists(node)) return false;

            if (!em.HasBuffer<MarkingLine>(node))
            {
                em.AddBuffer<MarkingLine>(node);
            }

            var lineBuf = em.GetBuffer<MarkingLine>(node);
            // Apply template lines
            for (int i = 0; i < t.Lines.Count; i++)
            {
                var tl = t.Lines[i];
                lineBuf.Add(new MarkingLine
                {
                    sourceEdge = Entity.Null,
                    sourceGapIndex = 0,
                    targetEdge = Entity.Null,
                    targetGapIndex = 1,
                    style = tl.Style,
                    curvature = tl.Curvature > 0f ? tl.Curvature : 0.4f
                });
            }

            em.AddComponent<Updated>(node);
            log.Info($"Applied template '{t.Name}' to Node #{node.Index}.");
            return true;
        }

        public void ToggleFavorite(string templateId)
        {
            var t = _templates.Find(x => x.Id == templateId);
            if (t != null)
            {
                t.IsFavorite = !t.IsFavorite;
                OnTemplatesChanged?.Invoke();
            }
        }

        public bool DeleteTemplate(string templateId)
        {
            int idx = _templates.FindIndex(x => x.Id == templateId && !x.IsBuiltIn);
            if (idx >= 0)
            {
                _templates.RemoveAt(idx);
                OnTemplatesChanged?.Invoke();
                return true;
            }
            return false;
        }
    }
}
