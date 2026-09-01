using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using TownRoadLane;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace JunctionStudio
{
    /// <summary>Click-to-fill tool for bounded faces in Town Road Lane's marking graph.</summary>
    public sealed partial class AreaPaintToolSystem : ToolBaseSystem
    {
        private ToolSystem _toolSystem;
        private DefaultToolSystem _defaultTool;
        private Entity _node;
        private int _style = 2;
        private List<PaintFace> _faces = new List<PaintFace>();
        private int _hovered = -1;
        private readonly Stack<AreaEdit> _undo = new Stack<AreaEdit>();
        private readonly Stack<AreaEdit> _redo = new Stack<AreaEdit>();

        private sealed class AreaEdit { public Entity Node; public int AreaIndex; public PaintFace Face; public int Style; }

        public override string toolID => "JunctionStudioAreaPaint";
        public Entity TargetNode => _node;
        public int CandidateCount => _faces.Count;
        public int HoveredFace => _hovered;
        public bool IsActive => _toolSystem != null && _toolSystem.activeTool == this;
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public override PrefabBase GetPrefab() => null;
        public override bool TrySetPrefab(PrefabBase prefab) => false;

        protected override void OnCreate()
        {
            base.OnCreate();
            _toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            _defaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            _faces.Clear();
            _undo.Clear();
            _redo.Clear();
            _node = Entity.Null;
        }

        protected override void OnDestroy()
        {
            _toolSystem = null;
            _defaultTool = null;
            _faces.Clear();
            _undo.Clear();
            _redo.Clear();
            base.OnDestroy();
        }

        public bool Begin(Entity node, int style)
        {
            if (node == Entity.Null || !EntityManager.Exists(node) || !EntityManager.HasBuffer<MarkingLine>(node)) return false;
            _node = node;
            _style = style;
            _faces = AreaFaceDetector.Detect(EntityManager, node);
            _hovered = -1;
            if (_faces.Count == 0) return false;
            _toolSystem.activeTool = this;
            return true;
        }

        public void SetStyle(int style) => _style = style;
        public void Stop() { if (IsActive) _toolSystem.activeTool = _defaultTool; }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            applyAction.shouldBeEnabled = true;
            secondaryApplyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            _hovered = -1;
            base.OnStopRunning();
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.Terrain;
            m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.PublicTransportRoad | Layer.TramTrack;
            m_ToolRaycastSystem.raycastFlags |= RaycastFlags.SubElements;
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (_node == Entity.Null || !EntityManager.Exists(_node)) { Stop(); return inputDeps; }
            if (GetRaycastResult(out Entity _, out RaycastHit hit)) _hovered = AreaFaceDetector.FindContaining(_faces, hit.m_HitPosition);
            else _hovered = -1;

            if (cancelAction.WasPressedThisFrame() || secondaryApplyAction.WasPressedThisFrame()) Stop();
            else if (applyAction.WasPressedThisFrame() && _hovered >= 0) Commit(_faces[_hovered], _style, true);
            return inputDeps;
        }

        private void Commit(PaintFace face, int style, bool record)
        {
            if (_node == Entity.Null || !EntityManager.Exists(_node)) return;
            DynamicBuffer<MarkingArea> areas = GetOrAddBuffer<MarkingArea>(_node);
            DynamicBuffer<MarkingAreaVertex> vertices = GetOrAddBuffer<MarkingAreaVertex>(_node);
            int areaIndex = areas.Length, first = vertices.Length;
            for (int i = 0; i < face.Vertices.Count; i++)
            {
                PaintVertex v = face.Vertices[i];
                vertices.Add(new MarkingAreaVertex { kind = v.Kind, refIndex = v.RefIndex, edgeToNext = 1, refEdgeA = v.EdgeA, refEdgeB = v.EdgeB, refGap = v.Gap, refPos = v.Position });
            }
            areas.Add(new MarkingArea { styleId = style, visible = true, firstVertex = first, vertexCount = face.Vertices.Count });
            Invalidate(_node);
            if (record)
            {
                _undo.Push(new AreaEdit { Node = _node, AreaIndex = areaIndex, Face = face, Style = style });
                _redo.Clear();
            }
            _faces = AreaFaceDetector.Detect(EntityManager, _node);
        }

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            AreaEdit edit = _undo.Pop();
            if (!RemoveArea(edit.Node, edit.AreaIndex)) return false;
            _redo.Push(edit);
            return true;
        }

        public bool Redo()
        {
            if (_redo.Count == 0) return false;
            AreaEdit edit = _redo.Pop();
            if (!EntityManager.Exists(edit.Node)) return false;
            _node = edit.Node;
            Commit(edit.Face, edit.Style, false);
            if (!EntityManager.HasBuffer<MarkingArea>(edit.Node)) return false;
            edit.AreaIndex = EntityManager.GetBuffer<MarkingArea>(edit.Node, true).Length - 1;
            _undo.Push(edit);
            return true;
        }

        private bool RemoveArea(Entity node, int index)
        {
            if (!EntityManager.Exists(node) || !EntityManager.HasBuffer<MarkingArea>(node) || !EntityManager.HasBuffer<MarkingAreaVertex>(node)) return false;
            DynamicBuffer<MarkingArea> areas = EntityManager.GetBuffer<MarkingArea>(node);
            DynamicBuffer<MarkingAreaVertex> vertices = EntityManager.GetBuffer<MarkingAreaVertex>(node);
            if (index < 0 || index >= areas.Length) return false;
            MarkingArea removed = areas[index];
            vertices.RemoveRange(removed.firstVertex, removed.vertexCount);
            areas.RemoveAt(index);
            for (int i = index; i < areas.Length; i++) { MarkingArea a = areas[i]; a.firstVertex -= removed.vertexCount; areas[i] = a; }
            Invalidate(node);
            return true;
        }

        private void Invalidate(Entity node)
        {
            if (EntityManager.HasBuffer<MarkingAreaPiece>(node)) EntityManager.GetBuffer<MarkingAreaPiece>(node).Clear();
            if (EntityManager.HasBuffer<MarkingAreaPieceVertex>(node)) EntityManager.GetBuffer<MarkingAreaPieceVertex>(node).Clear();
            if (EntityManager.HasComponent<MarkingAreaTopologyState>(node)) EntityManager.RemoveComponent<MarkingAreaTopologyState>(node);
            if (!EntityManager.HasComponent<Updated>(node)) EntityManager.AddComponent<Updated>(node);
        }

        private DynamicBuffer<T> GetOrAddBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData
            => EntityManager.HasBuffer<T>(entity) ? EntityManager.GetBuffer<T>(entity) : EntityManager.AddBuffer<T>(entity);
    }
}
