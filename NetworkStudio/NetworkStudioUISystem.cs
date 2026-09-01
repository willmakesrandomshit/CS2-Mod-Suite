using System;
using System.Collections.Generic;
using Colossal.Logging;
using Colossal.UI.Binding;
using Game.UI;
using Unity.Entities;

namespace NetworkStudio
{
    public partial class NetworkStudioUISystem : UISystemBase
    {
        private static new readonly ILog log = Mod.log;


        private ValueBinding<bool> m_IsOpenBinding;
        private ValueBinding<int> m_SelectedEdgeIdBinding;
        private ValueBinding<int> m_FurnitureCountBinding;
        private ValueBinding<string> m_FurnitureJsonBinding;
        private ValueBinding<int> m_SelectedIndexBinding;

        private NetworkStudioSystem m_NetworkStudioSystem;
        private NetworkStudioToolSystem m_ToolSystem;

        private bool m_IsOpen = false;

        protected override void OnCreate()
        {
            base.OnCreate();
            log.Info("[NetworkStudioUISystem] OnCreate");

            m_NetworkStudioSystem = World.GetOrCreateSystemManaged<NetworkStudioSystem>();
            m_ToolSystem = World.GetOrCreateSystemManaged<NetworkStudioToolSystem>();

            AddBinding(m_IsOpenBinding = new ValueBinding<bool>("NetworkStudio", "isOpen", false));
            AddBinding(m_SelectedEdgeIdBinding = new ValueBinding<int>("NetworkStudio", "selectedEdgeId", 0));
            AddBinding(m_FurnitureCountBinding = new ValueBinding<int>("NetworkStudio", "furnitureCount", 0));
            AddBinding(m_FurnitureJsonBinding = new ValueBinding<string>("NetworkStudio", "furnitureJson", "[]"));
            AddBinding(m_SelectedIndexBinding = new ValueBinding<int>("NetworkStudio", "selectedIndex", -1));

            AddBinding(new TriggerBinding<bool>("NetworkStudio", "setOpen", OnSetOpen));
            AddBinding(new TriggerBinding<int>("NetworkStudio", "selectFurniture", OnSelectFurniture));
            AddBinding(new TriggerBinding<int, bool>("NetworkStudio", "moveSignToVerge", OnMoveSignToVerge));
            AddBinding(new TriggerBinding<int, float>("NetworkStudio", "setCustomOffset", OnSetCustomOffset));
            AddBinding(new TriggerBinding<int>("NetworkStudio", "resetFurniture", OnResetFurniture));
        }

        private void OnSetOpen(bool open)
        {
            m_IsOpen = open;
            m_IsOpenBinding?.Update(m_IsOpen);

            if (m_IsOpen)
            {
                m_ToolSystem?.ActivateTool();
            }
            else
            {
                m_ToolSystem?.DeactivateTool();
                m_NetworkStudioSystem?.ClearSelection();
            }
        }

        private void OnSelectFurniture(int index)
        {
            m_NetworkStudioSystem?.SelectFurniture(index);
            m_SelectedIndexBinding?.Update(index);
        }

        private void OnMoveSignToVerge(int index, bool leftSide)
        {
            m_NetworkStudioSystem?.MoveSignToVerge(index, leftSide);
            UpdateUiState();
        }

        private void OnSetCustomOffset(int index, float offset)
        {
            m_NetworkStudioSystem?.SetCustomOffset(index, offset);
            UpdateUiState();
        }

        private void OnResetFurniture(int index)
        {
            m_NetworkStudioSystem?.ResetFurniture(index);
            UpdateUiState();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (!m_IsOpen) return;

            UpdateUiState();
        }

        private void UpdateUiState()
        {
            if (m_NetworkStudioSystem == null) return;

            int edgeId = m_NetworkStudioSystem.SelectedEdge != Entity.Null ? m_NetworkStudioSystem.SelectedEdge.Index : 0;
            m_SelectedEdgeIdBinding?.Update(edgeId);

            var items = m_NetworkStudioSystem.CurrentFurnitureList;
            m_FurnitureCountBinding?.Update(items.Count);
            m_SelectedIndexBinding?.Update(m_NetworkStudioSystem.SelectedFurnitureIndex);

            var jsonParts = new List<string>();
            foreach (var item in items)
            {
                jsonParts.Add(string.Format("{{\"index\":{0},\"name\":\"{1}\",\"category\":\"{2}\",\"offset\":{3:F2},\"onAsphalt\":{4}}}",
                    item.Index,
                    item.Name.Replace("\"", "\\\""),
                    item.Category.ToString(),
                    item.LateralOffset,
                    item.IsOnAsphalt ? "true" : "false"));
            }

            m_FurnitureJsonBinding?.Update("[" + string.Join(",", jsonParts) + "]");
        }
    }
}
