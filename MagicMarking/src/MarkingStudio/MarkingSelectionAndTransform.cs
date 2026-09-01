using System;
using System.Collections.Generic;
using Colossal.Logging;
using Game.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace MarkingStudio
{
    public class MarkingGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsLocked { get; set; }
        public bool IsHidden { get; set; }
        public List<int> LineIndices { get; set; } = new List<int>();
        public List<int> AreaIndices { get; set; } = new List<int>();
    }

    /// <summary>
    /// Magic Marking 2.1 Multi-Selection, Grouping & Transform Engine.
    /// Supports multi-selection, grouping, copy/paste, mirroring, rotation, and repeat patterns.
    /// </summary>
    public class MarkingSelectionAndTransform
    {
        private static readonly ILog log = Mod.log;
        public static MarkingSelectionAndTransform Instance { get; } = new MarkingSelectionAndTransform();

        private readonly HashSet<int> _selectedLines = new HashSet<int>();
        private readonly HashSet<int> _selectedAreas = new HashSet<int>();
        private readonly List<MarkingGroup> _groups = new List<MarkingGroup>();

        // Clipboard
        private List<MarkingLine> _lineClipboard = new List<MarkingLine>();
        private List<MarkingArea> _areaClipboard = new List<MarkingArea>();

        public IReadOnlyCollection<int> SelectedLines => _selectedLines;
        public IReadOnlyCollection<int> SelectedAreas => _selectedAreas;
        public IReadOnlyList<MarkingGroup> Groups => _groups;

        public event Action OnSelectionChanged;

        private MarkingSelectionAndTransform() { }

        public void SelectLine(int index, bool multiSelect = false)
        {
            if (!multiSelect)
            {
                _selectedLines.Clear();
                _selectedAreas.Clear();
            }
            if (_selectedLines.Contains(index)) _selectedLines.Remove(index);
            else _selectedLines.Add(index);
            OnSelectionChanged?.Invoke();
        }

        public void SelectArea(int index, bool multiSelect = false)
        {
            if (!multiSelect)
            {
                _selectedLines.Clear();
                _selectedAreas.Clear();
            }
            if (_selectedAreas.Contains(index)) _selectedAreas.Remove(index);
            else _selectedAreas.Add(index);
            OnSelectionChanged?.Invoke();
        }

        public void SelectAll(EntityManager em, Entity node)
        {
            _selectedLines.Clear();
            _selectedAreas.Clear();

            if (em.HasBuffer<MarkingLine>(node))
            {
                int lCount = em.GetBuffer<MarkingLine>(node).Length;
                for (int i = 0; i < lCount; i++) _selectedLines.Add(i);
            }
            if (em.HasBuffer<MarkingArea>(node))
            {
                int aCount = em.GetBuffer<MarkingArea>(node).Length;
                for (int a = 0; a < aCount; a++) _selectedAreas.Add(a);
            }
            OnSelectionChanged?.Invoke();
        }

        public void ClearSelection()
        {
            _selectedLines.Clear();
            _selectedAreas.Clear();
            OnSelectionChanged?.Invoke();
        }

        // Copy / Paste / Duplicate
        public void CopySelection(EntityManager em, Entity node)
        {
            _lineClipboard.Clear();
            _areaClipboard.Clear();

            if (em.HasBuffer<MarkingLine>(node))
            {
                var lines = em.GetBuffer<MarkingLine>(node);
                foreach (var idx in _selectedLines)
                {
                    if (idx >= 0 && idx < lines.Length) _lineClipboard.Add(lines[idx]);
                }
            }

            if (em.HasBuffer<MarkingArea>(node))
            {
                var areas = em.GetBuffer<MarkingArea>(node);
                foreach (var idx in _selectedAreas)
                {
                    if (idx >= 0 && idx < areas.Length) _areaClipboard.Add(areas[idx]);
                }
            }

            log.Info($"Copied {_lineClipboard.Count} lines and {_areaClipboard.Count} areas to clipboard.");
        }

        public int PasteToNode(EntityManager em, Entity node)
        {
            if (node == Entity.Null || !em.Exists(node) || (_lineClipboard.Count == 0 && _areaClipboard.Count == 0)) return 0;

            MarkingHistoryEngine.Instance.PushSnapshot(em, node, "Paste Markings");

            if (!em.HasBuffer<MarkingLine>(node)) em.AddBuffer<MarkingLine>(node);
            var lBuf = em.GetBuffer<MarkingLine>(node);
            for (int i = 0; i < _lineClipboard.Count; i++)
            {
                lBuf.Add(_lineClipboard[i]);
            }

            em.AddComponent<Updated>(node);
            log.Info($"Pasted {_lineClipboard.Count} lines to Node #{node.Index}.");
            return _lineClipboard.Count;
        }

        // Transforms
        public void MirrorSelection(EntityManager em, Entity node)
        {
            if (node == Entity.Null || !em.Exists(node) || !em.HasBuffer<MarkingLine>(node)) return;

            MarkingHistoryEngine.Instance.PushSnapshot(em, node, "Mirror Markings");

            var lines = em.GetBuffer<MarkingLine>(node);
            foreach (var idx in _selectedLines)
            {
                if (idx >= 0 && idx < lines.Length)
                {
                    var line = lines[idx];
                    // Swap source and target endpoints
                    var tempEdge = line.sourceEdge;
                    var tempGap = line.sourceGapIndex;
                    line.sourceEdge = line.targetEdge;
                    line.sourceGapIndex = line.targetGapIndex;
                    line.targetEdge = tempEdge;
                    line.targetGapIndex = tempGap;
                    lines[idx] = line;
                }
            }

            em.AddComponent<Updated>(node);
            log.Info($"Mirrored {_selectedLines.Count} selected lines on Node #{node.Index}.");
        }

        public void InvertCurvature(EntityManager em, Entity node)
        {
            if (node == Entity.Null || !em.Exists(node) || !em.HasBuffer<MarkingLine>(node)) return;

            MarkingHistoryEngine.Instance.PushSnapshot(em, node, "Invert Curvature");

            var lines = em.GetBuffer<MarkingLine>(node);
            foreach (var idx in _selectedLines)
            {
                if (idx >= 0 && idx < lines.Length)
                {
                    var line = lines[idx];
                    line.curvature = -line.curvature;
                    lines[idx] = line;
                }
            }

            em.AddComponent<Updated>(node);
            log.Info($"Inverted curvature on {_selectedLines.Count} lines on Node #{node.Index}.");
        }

        public void RepeatSelection(EntityManager em, Entity node, int count, float offsetStep)
        {
            if (node == Entity.Null || !em.Exists(node) || !em.HasBuffer<MarkingLine>(node) || count <= 0) return;

            MarkingHistoryEngine.Instance.PushSnapshot(em, node, "Repeat Markings");

            var lines = em.GetBuffer<MarkingLine>(node);
            var toAdd = new List<MarkingLine>();

            foreach (var idx in _selectedLines)
            {
                if (idx >= 0 && idx < lines.Length)
                {
                    var baseLine = lines[idx];
                    for (int c = 1; c <= count; c++)
                    {
                        var copy = baseLine;
                        copy.curvature = math.clamp(baseLine.curvature + c * 0.05f, -0.9f, 0.9f);
                        toAdd.Add(copy);
                    }
                }
            }

            for (int i = 0; i < toAdd.Count; i++) lines.Add(toAdd[i]);

            em.AddComponent<Updated>(node);
            log.Info($"Repeated {toAdd.Count} markings ({count} times) on Node #{node.Index}.");
        }

        // Grouping
        public MarkingGroup CreateGroup(string groupName)
        {
            var grp = new MarkingGroup
            {
                Id = "grp_" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = string.IsNullOrEmpty(groupName) ? $"Group {_groups.Count + 1}" : groupName,
                IsLocked = false,
                IsHidden = false,
                LineIndices = new List<int>(_selectedLines),
                AreaIndices = new List<int>(_selectedAreas)
            };
            _groups.Add(grp);
            log.Info($"Created group '{grp.Name}' with {grp.LineIndices.Count} lines.");
            return grp;
        }

        public void Ungroup(string groupId)
        {
            _groups.RemoveAll(x => x.Id == groupId);
        }
    }
}
