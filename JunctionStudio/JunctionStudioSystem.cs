using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Common;
using Game.Net;
using Game.UI;
using TownRoadLane;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace JunctionStudio
{
    public sealed partial class JunctionStudioSystem : UISystemBase
    {
        private const string Group = "junctionStudio";
        private MarkingNodeToolSystem _markingTool;
        private JunctionPresetLibrary _library = new JunctionPresetLibrary();
        private JunctionPreset _clipboard;
        private ValueBinding<bool> _selected;
        private ValueBinding<bool> _hasClipboard;
        private ValueBinding<string> _presets;
        private ValueBinding<string> _message;
        private ValueBinding<bool> _panelOpen;
        private string _lastMessage = "Select a junction with Town Road Lane.";
        private JunctionPreset _pendingPreset;
        private Entity _pendingNode;
        private List<Entity> _pendingApproaches;
        private JunctionPreset _previousPreset;
        private Entity _previousNode;
        private List<Entity> _previousApproaches;
        private ValueBinding<bool> _previewArmed;
        private ValueBinding<bool> _canRestorePrevious;

        private static string LibraryPath => Path.Combine(
            Application.persistentDataPath, "ModsData", "JunctionStudio", "presets.xml");

        protected override void OnCreate()
        {
            base.OnCreate();
            _markingTool = World.GetOrCreateSystemManaged<MarkingNodeToolSystem>();
            LoadLibrary();

            _selected = new ValueBinding<bool>(Group, "selected", false, ValueWriters.Create<bool>());
            _hasClipboard = new ValueBinding<bool>(Group, "hasClipboard", false, ValueWriters.Create<bool>());
            _presets = new ValueBinding<string>(Group, "presets", string.Empty, ValueWriters.Create<string>());
            _message = new ValueBinding<string>(Group, "message", _lastMessage, ValueWriters.Create<string>());
            _panelOpen = new ValueBinding<bool>(Group, "panelOpen", false, ValueWriters.Create<bool>());
            AddBinding(_selected);
            AddBinding(_hasClipboard);
            AddBinding(_presets);
            AddBinding(_message);
            AddBinding(_panelOpen);
            _previewArmed = new ValueBinding<bool>(Group, "previewArmed", false);
            _canRestorePrevious = new ValueBinding<bool>(Group, "canRestorePrevious", false);
            AddBinding(_previewArmed);
            AddBinding(_canRestorePrevious);
            AddBinding(new TriggerBinding(Group, "confirmApply", ConfirmApply));
            AddBinding(new TriggerBinding(Group, "cancelPreview", CancelPreview));
            AddBinding(new TriggerBinding(Group, "restorePrevious", RestorePrevious));
            AddBinding(new TriggerBinding(Group, "copy", Copy));
            AddBinding(new TriggerBinding(Group, "paste", Paste));
            AddBinding(new TriggerBinding<string>(Group, "savePreset", SavePreset, ValueReaders.Create<string>()));
            AddBinding(new TriggerBinding<string>(Group, "applyPreset", ApplyPreset, ValueReaders.Create<string>()));
            AddBinding(new TriggerBinding<string>(Group, "deletePreset", DeletePreset, ValueReaders.Create<string>()));
            AddBinding(new TriggerBinding(Group, "togglePanel", () => { if (_panelOpen.value) CancelPreview(); _panelOpen.Update(!_panelOpen.value); }));
            AddBinding(new TriggerBinding(Group, "closePanel", () => { CancelPreview(); _panelOpen.Update(false); }));
            Publish();
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            _clipboard = null;
            _pendingPreset = _previousPreset = null;
            _pendingApproaches = _previousApproaches = null;
            _lastMessage = "Select a junction with Town Road Lane.";
            _panelOpen?.Update(false);
            Publish();
        }

        protected override void OnUpdate()
        {
            if (World == null || !World.IsCreated) return;
            base.OnUpdate();
            if (_pendingPreset != null && SelectedNode != _pendingNode) CancelPreview();
            Publish();
        }

        protected override void OnDestroy()
        {
            _markingTool = null;
            _clipboard = null;
            base.OnDestroy();
        }

        private Entity SelectedNode => _markingTool == null ? Entity.Null : _markingTool.SelectedNode;

        private void Publish()
        {
            Entity node = SelectedNode;
            _selected.Update(node != Entity.Null && EntityManager.Exists(node) && !EntityManager.HasComponent<Deleted>(node) && EntityManager.HasComponent<Node>(node));
            _hasClipboard.Update(_clipboard != null);
            _presets.Update(string.Join("\n", _library.Presets.Select(x => x.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)));
            _message.Update(_lastMessage);
            _previewArmed.Update(_pendingPreset != null);
            _canRestorePrevious.Update(_previousPreset != null && SelectedNode == _previousNode && EntityManager.Exists(_previousNode));
        }

        private void Copy()
        {
            if (!TryCapture("Clipboard", out _clipboard)) return;
            _lastMessage = $"Copied {_clipboard.Lines.Count} lines and {_clipboard.Areas.Count} areas.";
            Publish();
        }

        private void Paste()
        {
            if (_clipboard == null) { _lastMessage = "Clipboard is empty."; return; }
            Apply(_clipboard);
        }

        private void SavePreset(string rawName)
        {
            string name = SanitiseName(rawName);
            if (string.IsNullOrWhiteSpace(name)) { _lastMessage = "Enter a preset name."; return; }
            if (!TryCapture(name, out JunctionPreset preset)) return;
            _library.Presets.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            _library.Presets.Add(preset);
            SaveLibrary();
            _lastMessage = $"Saved preset ‘{name}’.";
            Publish();
        }

        private void ApplyPreset(string name)
        {
            JunctionPreset preset = _library.Presets.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (preset == null) { _lastMessage = "Preset was not found."; return; }
            Apply(preset);
        }

        private void DeletePreset(string name)
        {
            int removed = _library.Presets.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) SaveLibrary();
            _lastMessage = removed > 0 ? $"Deleted preset ‘{name}’." : "Preset was not found.";
            Publish();
        }

        private bool TryCapture(string name, out JunctionPreset preset)
        {
            preset = null;
            Entity node = SelectedNode;
            if (!TryGetApproaches(node, out List<Entity> approaches, out float baseline, out float3 centre))
            {
                _lastMessage = "Select a valid road junction with Town Road Lane first.";
                return false;
            }

            var edgeIndex = new Dictionary<Entity, int>();
            for (int i = 0; i < approaches.Count; i++) edgeIndex[approaches[i]] = i;
            preset = new JunctionPreset { Name = name, ApproachCount = approaches.Count, BaselineAngle = baseline };

            if (EntityManager.HasComponent<MarkingOverride>(node))
                preset.HiddenCategories = (uint)EntityManager.GetComponentData<MarkingOverride>(node).hide;

            if (EntityManager.HasBuffer<MarkingLine>(node))
            {
                foreach (MarkingLine line in EntityManager.GetBuffer<MarkingLine>(node, true))
                {
                    if (!edgeIndex.TryGetValue(line.sourceEdge, out int a) || !edgeIndex.TryGetValue(line.targetEdge, out int b))
                    {
                        _lastMessage = "A marking references an unsupported approach. Capture was cancelled without changing the junction.";
                        preset = null;
                        return false;
                    }
                    preset.Lines.Add(new LineRecord { SourceApproach = a, SourceGap = line.sourceGapIndex, TargetApproach = b, TargetGap = line.targetGapIndex, Style = line.style, Curvature = line.curvature });
                }
            }
            if (EntityManager.HasBuffer<MarkingSegment>(node))
            {
                foreach (MarkingSegment segment in EntityManager.GetBuffer<MarkingSegment>(node, true))
                    preset.Segments.Add(new SegmentRecord { Line = segment.lineIndex, Start = segment.tStart, End = segment.tEnd, Visible = segment.visible, Style = segment.style });
            }
            if (EntityManager.HasBuffer<MarkingArea>(node))
            {
                foreach (MarkingArea area in EntityManager.GetBuffer<MarkingArea>(node, true))
                    preset.Areas.Add(new AreaRecord { Style = area.styleId, Visible = area.visible, FirstVertex = area.firstVertex, VertexCount = area.vertexCount });
            }
            if (EntityManager.HasBuffer<MarkingAreaVertex>(node))
            {
                foreach (MarkingAreaVertex vertex in EntityManager.GetBuffer<MarkingAreaVertex>(node, true))
                {
                    float3 local = RotateY(vertex.refPos - centre, -baseline);
                    preset.Vertices.Add(new VertexRecord {
                        Kind = vertex.kind, LegacyIndex = vertex.refIndex, EdgeToNext = vertex.edgeToNext,
                        ApproachA = edgeIndex.TryGetValue(vertex.refEdgeA, out int a) ? a : -1,
                        ApproachB = edgeIndex.TryGetValue(vertex.refEdgeB, out int b) ? b : -1,
                        Gap = vertex.refGap, LocalX = local.x, LocalY = local.y, LocalZ = local.z
                    });
                }
            }
            return true;
        }

        private void Apply(JunctionPreset preset, bool confirmed = false)
        {
            Entity node = SelectedNode;
            if (!TryGetApproaches(node, out List<Entity> approaches, out float baseline, out float3 centre))
            {
                _lastMessage = "Select a valid destination junction first.";
                return;
            }
            if (approaches.Count != preset.ApproachCount)
            {
                _lastMessage = $"Preset needs {preset.ApproachCount} approaches; this junction has {approaches.Count}.";
                return;
            }

            if (!ValidatePreset(preset, approaches.Count, out string validationError))
            {
                _lastMessage = "Preset was not applied: " + validationError;
                Publish();
                return;
            }

            if (!confirmed)
            {
                _pendingPreset = preset;
                _pendingNode = node;
                _pendingApproaches = approaches;
                _lastMessage = $"Preview ‘{preset.Name}’: replace markings with {preset.Lines.Count} lines and {preset.Areas.Count} areas across {approaches.Count} approaches. Nothing changed yet.";
                Publish();
                return;
            }
            if (!TryCapture("Previous markings", out JunctionPreset rollback) || !ValidatePreset(rollback, approaches.Count, out validationError))
            {
                _lastMessage = "Cannot safely capture the current markings. No changes were made.";
                Publish();
                return;
            }

            try
            {
                WritePresetToNode(node, preset, approaches, baseline, centre);
            }
            catch (Exception ex)
            {
                Mod.Log.Error(ex, $"Could not apply Junction Studio preset '{preset.Name}'");
                if (rollback != null && ValidatePreset(rollback, approaches.Count, out _))
                {
                    try { WritePresetToNode(node, rollback, approaches, baseline, centre); }
                    catch (Exception rollbackEx) { Mod.Log.Error(rollbackEx, "Junction Studio automatic rollback failed"); }
                }
                _lastMessage = "Preset failed and the previous markings were restored where possible. Check the log.";
                Publish();
                return;
            }

            _lastMessage = $"Applied ‘{preset.Name}’: {preset.Lines.Count} lines, {preset.Areas.Count} areas.";
            _previousPreset = rollback;
            _previousNode = node;
            _previousApproaches = approaches;
            Publish();
        }

        private void CancelPreview()
        {
            if (_pendingPreset != null) _lastMessage = "Preview cancelled; markings unchanged.";
            _pendingPreset = null;
            _pendingApproaches = null;
            Publish();
        }

        private void ConfirmApply()
        {
            if (_pendingPreset == null) return;
            if (SelectedNode != _pendingNode || !TryGetApproaches(_pendingNode, out var approaches, out _, out _) ||
                !approaches.SequenceEqual(_pendingApproaches))
            {
                CancelPreview();
                _lastMessage = "Junction changed; select the preset again.";
                Publish();
                return;
            }
            var preset = _pendingPreset;
            _pendingPreset = null;
            _pendingApproaches = null;
            Apply(preset, confirmed: true);
        }

        private void RestorePrevious()
        {
            if (_previousPreset == null || SelectedNode != _previousNode) return;
            if (!TryGetApproaches(_previousNode, out var approaches, out _, out _) || !approaches.SequenceEqual(_previousApproaches))
            {
                _lastMessage = "Junction topology changed; the saved markings cannot safely be restored.";
                Publish();
                return;
            }
            CancelPreview();
            var previous = _previousPreset;
            Apply(previous, confirmed: true);
        }

        private void WritePresetToNode(Entity node, JunctionPreset preset, List<Entity> approaches, float baseline, float3 centre)
        {

            DynamicBuffer<MarkingLine> lines = GetOrAddBuffer<MarkingLine>(node); lines.Clear();
            foreach (LineRecord line in preset.Lines)
                lines.Add(new MarkingLine { sourceEdge = approaches[line.SourceApproach], sourceGapIndex = line.SourceGap, targetEdge = approaches[line.TargetApproach], targetGapIndex = line.TargetGap, style = line.Style, curvature = line.Curvature });

            DynamicBuffer<MarkingSegment> segments = GetOrAddBuffer<MarkingSegment>(node); segments.Clear();
            foreach (SegmentRecord segment in preset.Segments)
                segments.Add(new MarkingSegment { lineIndex = segment.Line, tStart = segment.Start, tEnd = segment.End, visible = segment.Visible, style = segment.Style });

            DynamicBuffer<MarkingArea> areas = GetOrAddBuffer<MarkingArea>(node); areas.Clear();
            foreach (AreaRecord area in preset.Areas)
                areas.Add(new MarkingArea { styleId = area.Style, visible = area.Visible, firstVertex = area.FirstVertex, vertexCount = area.VertexCount });

            DynamicBuffer<MarkingAreaVertex> vertices = GetOrAddBuffer<MarkingAreaVertex>(node); vertices.Clear();
            foreach (VertexRecord vertex in preset.Vertices)
            {
                float3 pos = centre + RotateY(new float3(vertex.LocalX, vertex.LocalY, vertex.LocalZ), baseline);
                vertices.Add(new MarkingAreaVertex {
                    kind = vertex.Kind, refIndex = vertex.LegacyIndex, edgeToNext = vertex.EdgeToNext,
                    refEdgeA = vertex.ApproachA >= 0 ? approaches[vertex.ApproachA] : Entity.Null,
                    refEdgeB = vertex.ApproachB >= 0 ? approaches[vertex.ApproachB] : Entity.Null,
                    refGap = vertex.Gap, refPos = pos
                });
            }

            var markingOverride = new MarkingOverride { hide = (MarkingCategory)preset.HiddenCategories };
            if (EntityManager.HasComponent<MarkingOverride>(node)) EntityManager.SetComponentData(node, markingOverride);
            else EntityManager.AddComponentData(node, markingOverride);

            if (EntityManager.HasBuffer<MarkingAreaPiece>(node)) EntityManager.GetBuffer<MarkingAreaPiece>(node).Clear();
            if (EntityManager.HasBuffer<MarkingAreaPieceVertex>(node)) EntityManager.GetBuffer<MarkingAreaPieceVertex>(node).Clear();
            if (EntityManager.HasComponent<MarkingAreaTopologyState>(node)) EntityManager.RemoveComponent<MarkingAreaTopologyState>(node);
            if (!EntityManager.HasComponent<Updated>(node)) EntityManager.AddComponent<Updated>(node);
        }

        private static bool ValidatePreset(JunctionPreset preset, int approachCount, out string reason)
        {
            reason = string.Empty;
            if (preset == null) { reason = "Preset data is empty."; return false; }
            if (preset.Lines == null || preset.Segments == null || preset.Areas == null || preset.Vertices == null)
            { reason = "Preset contains a missing data section."; return false; }
            if (preset.Lines.Count > 512 || preset.Segments.Count > 2048 || preset.Areas.Count > 512 || preset.Vertices.Count > 8192)
            { reason = "Preset exceeds Junction Studio's safety limits."; return false; }

            for (int i = 0; i < preset.Lines.Count; i++)
            {
                LineRecord line = preset.Lines[i];
                if (line == null || line.SourceApproach < 0 || line.SourceApproach >= approachCount ||
                    line.TargetApproach < 0 || line.TargetApproach >= approachCount ||
                    line.SourceGap < 0 || line.TargetGap < 0 || !IsFinite(line.Curvature))
                { reason = $"Line {i + 1} has an invalid approach, gap, or curve value."; return false; }
            }
            for (int i = 0; i < preset.Segments.Count; i++)
            {
                SegmentRecord segment = preset.Segments[i];
                if (segment == null || segment.Line < 0 || segment.Line >= preset.Lines.Count ||
                    !IsFinite(segment.Start) || !IsFinite(segment.End) || segment.Start < 0f ||
                    segment.End > 1f || segment.Start > segment.End)
                { reason = $"Segment {i + 1} has an invalid line or range."; return false; }
            }
            for (int i = 0; i < preset.Vertices.Count; i++)
            {
                VertexRecord vertex = preset.Vertices[i];
                if (vertex == null || vertex.ApproachA < -1 || vertex.ApproachA >= approachCount ||
                    vertex.ApproachB < -1 || vertex.ApproachB >= approachCount || vertex.Gap < 0 ||
                    !IsFinite(vertex.LocalX) || !IsFinite(vertex.LocalY) || !IsFinite(vertex.LocalZ))
                { reason = $"Area vertex {i + 1} is invalid."; return false; }
            }
            for (int i = 0; i < preset.Areas.Count; i++)
            {
                AreaRecord area = preset.Areas[i];
                if (area == null || area.FirstVertex < 0 || area.VertexCount < 3 || area.VertexCount > preset.Vertices.Count ||
                    area.FirstVertex > preset.Vertices.Count - area.VertexCount)
                { reason = $"Area {i + 1} references invalid vertices."; return false; }
            }
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private DynamicBuffer<T> GetOrAddBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData
            => EntityManager.HasBuffer<T>(entity) ? EntityManager.GetBuffer<T>(entity) : EntityManager.AddBuffer<T>(entity);

        private bool TryGetApproaches(Entity node, out List<Entity> approaches, out float baseline, out float3 centre)
        {
            approaches = new List<Entity>(); baseline = 0f; centre = float3.zero;
            if (node == Entity.Null || !EntityManager.Exists(node) || !EntityManager.HasComponent<Node>(node) || !EntityManager.HasBuffer<ConnectedEdge>(node)) return false;
            centre = EntityManager.GetComponentData<Node>(node).m_Position;
            var angled = new List<(Entity edge, float angle)>();
            foreach (ConnectedEdge connected in EntityManager.GetBuffer<ConnectedEdge>(node, true))
            {
                Entity edgeEntity = connected.m_Edge;
                if (!EntityManager.HasComponent<Edge>(edgeEntity)) continue;
                Edge edge = EntityManager.GetComponentData<Edge>(edgeEntity);
                Entity other = edge.m_Start == node ? edge.m_End : edge.m_Start;
                if (!EntityManager.HasComponent<Node>(other)) continue;
                float3 delta = EntityManager.GetComponentData<Node>(other).m_Position - centre;
                angled.Add((edgeEntity, math.atan2(delta.z, delta.x)));
            }
            angled.Sort((a, b) => a.angle.CompareTo(b.angle));
            approaches.AddRange(angled.Select(x => x.edge));
            if (angled.Count > 0) baseline = angled[0].angle;
            return approaches.Count >= 2;
        }

        private static float3 RotateY(float3 value, float angle)
        {
            float c = math.cos(angle), s = math.sin(angle);
            return new float3(value.x * c - value.z * s, value.y, value.x * s + value.z * c);
        }

        private static string SanitiseName(string name)
        {
            if (name == null) return string.Empty;
            string clean = new string(name.Trim().Where(c => !char.IsControl(c) && c != '\n' && c != '\r').ToArray());
            return clean.Length > 48 ? clean.Substring(0, 48) : clean;
        }

        private void LoadLibrary()
        {
            try
            {
                if (!File.Exists(LibraryPath)) return;
                if (new FileInfo(LibraryPath).Length > 5 * 1024 * 1024)
                    throw new InvalidDataException("Preset library exceeds the 5 MB safety limit.");
                using (FileStream stream = File.OpenRead(LibraryPath))
                    _library = (JunctionPresetLibrary)new XmlSerializer(typeof(JunctionPresetLibrary)).Deserialize(stream);
                if (_library == null) _library = new JunctionPresetLibrary();
                if (_library.Presets == null) _library.Presets = new List<JunctionPreset>();
                _library.Presets.RemoveAll(x => x == null);
                if (_library.Presets.Count > 256) _library.Presets.RemoveRange(256, _library.Presets.Count - 256);
            }
            catch (Exception ex) { Mod.Log.Warn(ex, "Could not load preset library; starting empty"); _library = new JunctionPresetLibrary(); }
        }

        private void SaveLibrary()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                string temp = LibraryPath + ".tmp";
                using (FileStream stream = File.Create(temp)) new XmlSerializer(typeof(JunctionPresetLibrary)).Serialize(stream, _library);
                if (File.Exists(LibraryPath)) File.Replace(temp, LibraryPath, null); else File.Move(temp, LibraryPath);
            }
            catch (Exception ex) { Mod.Log.Error(ex, "Could not save preset library"); _lastMessage = "Could not save presets; check the log."; }
        }
    }
}
