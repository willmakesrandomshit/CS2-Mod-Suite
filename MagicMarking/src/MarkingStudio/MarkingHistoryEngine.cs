using System;
using System.Collections.Generic;
using Colossal.Logging;
using Game.Common;
using Unity.Entities;

namespace MarkingStudio
{
    public struct MarkingHistorySnapshot
    {
        public string ActionName;
        public Entity TargetNode;
        public List<MarkingLine> Lines;
        public List<MarkingArea> Areas;
        public List<MarkingAreaVertex> Vertices;
        public DateTime Timestamp;
    }

    /// <summary>
    /// Magic Marking 2.1 Bounded Undo / Redo History Engine.
    /// Manages command/delta snapshots across all editing, template, fill, and transform actions.
    /// </summary>
    public class MarkingHistoryEngine
    {
        private static readonly ILog log = Mod.log;
        public static MarkingHistoryEngine Instance { get; } = new MarkingHistoryEngine();

        private const int kMaxHistoryDepth = 50;
        private readonly Stack<MarkingHistorySnapshot> _undoStack = new Stack<MarkingHistorySnapshot>();
        private readonly Stack<MarkingHistorySnapshot> _redoStack = new Stack<MarkingHistorySnapshot>();

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;
        public event Action OnHistoryChanged;

        private MarkingHistoryEngine() { }

        public void PushSnapshot(EntityManager em, Entity node, string actionName)
        {
            if (node == Entity.Null || !em.Exists(node)) return;

            var snap = new MarkingHistorySnapshot
            {
                ActionName = actionName ?? "Edit Markings",
                TargetNode = node,
                Lines = new List<MarkingLine>(),
                Areas = new List<MarkingArea>(),
                Vertices = new List<MarkingAreaVertex>(),
                Timestamp = DateTime.Now
            };

            if (em.HasBuffer<MarkingLine>(node))
            {
                var lBuf = em.GetBuffer<MarkingLine>(node);
                for (int i = 0; i < lBuf.Length; i++) snap.Lines.Add(lBuf[i]);
            }

            if (em.HasBuffer<MarkingArea>(node))
            {
                var aBuf = em.GetBuffer<MarkingArea>(node);
                for (int i = 0; i < aBuf.Length; i++) snap.Areas.Add(aBuf[i]);
            }

            if (em.HasBuffer<MarkingAreaVertex>(node))
            {
                var vBuf = em.GetBuffer<MarkingAreaVertex>(node);
                for (int i = 0; i < vBuf.Length; i++) snap.Vertices.Add(vBuf[i]);
            }

            _undoStack.Push(snap);
            if (_undoStack.Count > kMaxHistoryDepth)
            {
                // Trim bottom
                var list = new List<MarkingHistorySnapshot>(_undoStack);
                list.RemoveAt(list.Count - 1);
                _undoStack.Clear();
                for (int i = list.Count - 1; i >= 0; i--) _undoStack.Push(list[i]);
            }

            _redoStack.Clear();
            OnHistoryChanged?.Invoke();
        }

        public bool Undo(EntityManager em)
        {
            if (_undoStack.Count == 0) return false;

            var snap = _undoStack.Pop();
            if (snap.TargetNode != Entity.Null && em.Exists(snap.TargetNode))
            {
                // Push current state to Redo
                PushRedoSnapshot(em, snap.TargetNode, snap.ActionName);
                ApplySnapshot(em, snap);
                OnHistoryChanged?.Invoke();
                log.Info($"Undo: reverted '{snap.ActionName}' on Node #{snap.TargetNode.Index}");
                return true;
            }
            return false;
        }

        public bool Redo(EntityManager em)
        {
            if (_redoStack.Count == 0) return false;

            var snap = _redoStack.Pop();
            if (snap.TargetNode != Entity.Null && em.Exists(snap.TargetNode))
            {
                PushUndoOnly(em, snap.TargetNode, snap.ActionName);
                ApplySnapshot(em, snap);
                OnHistoryChanged?.Invoke();
                log.Info($"Redo: re-applied '{snap.ActionName}' on Node #{snap.TargetNode.Index}");
                return true;
            }
            return false;
        }

        private void PushRedoSnapshot(EntityManager em, Entity node, string actionName)
        {
            var snap = new MarkingHistorySnapshot
            {
                ActionName = actionName,
                TargetNode = node,
                Lines = new List<MarkingLine>(),
                Areas = new List<MarkingArea>(),
                Vertices = new List<MarkingAreaVertex>(),
                Timestamp = DateTime.Now
            };

            if (em.HasBuffer<MarkingLine>(node))
            {
                var lBuf = em.GetBuffer<MarkingLine>(node);
                for (int i = 0; i < lBuf.Length; i++) snap.Lines.Add(lBuf[i]);
            }

            if (em.HasBuffer<MarkingArea>(node))
            {
                var aBuf = em.GetBuffer<MarkingArea>(node);
                for (int i = 0; i < aBuf.Length; i++) snap.Areas.Add(aBuf[i]);
            }

            if (em.HasBuffer<MarkingAreaVertex>(node))
            {
                var vBuf = em.GetBuffer<MarkingAreaVertex>(node);
                for (int i = 0; i < vBuf.Length; i++) snap.Vertices.Add(vBuf[i]);
            }

            _redoStack.Push(snap);
        }

        private void PushUndoOnly(EntityManager em, Entity node, string actionName)
        {
            var snap = new MarkingHistorySnapshot
            {
                ActionName = actionName,
                TargetNode = node,
                Lines = new List<MarkingLine>(),
                Areas = new List<MarkingArea>(),
                Vertices = new List<MarkingAreaVertex>(),
                Timestamp = DateTime.Now
            };

            if (em.HasBuffer<MarkingLine>(node))
            {
                var lBuf = em.GetBuffer<MarkingLine>(node);
                for (int i = 0; i < lBuf.Length; i++) snap.Lines.Add(lBuf[i]);
            }

            if (em.HasBuffer<MarkingArea>(node))
            {
                var aBuf = em.GetBuffer<MarkingArea>(node);
                for (int i = 0; i < aBuf.Length; i++) snap.Areas.Add(aBuf[i]);
            }

            if (em.HasBuffer<MarkingAreaVertex>(node))
            {
                var vBuf = em.GetBuffer<MarkingAreaVertex>(node);
                for (int i = 0; i < vBuf.Length; i++) snap.Vertices.Add(vBuf[i]);
            }

            _undoStack.Push(snap);
        }

        private void ApplySnapshot(EntityManager em, MarkingHistorySnapshot snap)
        {
            var node = snap.TargetNode;

            // Restore Lines
            if (!em.HasBuffer<MarkingLine>(node)) em.AddBuffer<MarkingLine>(node);
            var lBuf = em.GetBuffer<MarkingLine>(node);
            lBuf.Clear();
            for (int i = 0; i < snap.Lines.Count; i++) lBuf.Add(snap.Lines[i]);

            // Restore Areas
            if (!em.HasBuffer<MarkingArea>(node)) em.AddBuffer<MarkingArea>(node);
            var aBuf = em.GetBuffer<MarkingArea>(node);
            aBuf.Clear();
            for (int i = 0; i < snap.Areas.Count; i++) aBuf.Add(snap.Areas[i]);

            // Restore Vertices
            if (!em.HasBuffer<MarkingAreaVertex>(node)) em.AddBuffer<MarkingAreaVertex>(node);
            var vBuf = em.GetBuffer<MarkingAreaVertex>(node);
            vBuf.Clear();
            for (int i = 0; i < snap.Vertices.Count; i++) vBuf.Add(snap.Vertices[i]);

            em.AddComponent<Updated>(node);
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnHistoryChanged?.Invoke();
        }
    }
}
