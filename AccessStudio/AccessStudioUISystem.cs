using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.SceneFlow;
using Game.Tools;
using Game.UI;
using Unity.Entities;

namespace AccessStudio
{
    public sealed partial class AccessStudioUISystem : UISystemBase
    {
        private AccessStudioProbeSystem m_Probe;
        private AccessStudioToolSystem m_Tool;
        private AccessStudioPocSystem m_Poc;
        private ToolSystem m_ToolSystem;
        private DefaultToolSystem m_DefaultTool;
        private bool m_Open;

        private ValueBinding<bool> m_OpenBinding;
        private ValueBinding<bool> m_ToolActiveBinding;
        private ValueBinding<bool> m_HasSelectionBinding;
        private ValueBinding<string> m_StatusBinding;
        private ValueBinding<string> m_DiagnosticBinding;
        private ValueBinding<bool> m_RoadSelectionBinding;
        private ValueBinding<bool> m_RemoteSelectionBinding;
        private ValueBinding<bool> m_HasOverrideBinding;
        private ValueBinding<string> m_PocStateBinding;
        private ValueBinding<string> m_PocStatusBinding;
        private ValueBinding<string> m_PocTelemetryBinding;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Probe = World.GetOrCreateSystemManaged<AccessStudioProbeSystem>();
            m_Tool = World.GetOrCreateSystemManaged<AccessStudioToolSystem>();
            m_Poc = World.GetOrCreateSystemManaged<AccessStudioPocSystem>();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_DefaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();

            AddBinding(m_OpenBinding = new ValueBinding<bool>("AccessStudio", "open", false));
            AddBinding(m_ToolActiveBinding = new ValueBinding<bool>("AccessStudio", "toolActive", false));
            AddBinding(m_HasSelectionBinding = new ValueBinding<bool>("AccessStudio", "hasSelection", false));
            AddBinding(m_StatusBinding = new ValueBinding<string>("AccessStudio", "status", "Activate the probe and click a building."));
            AddBinding(m_DiagnosticBinding = new ValueBinding<string>("AccessStudio", "diagnostic", "No building selected."));
            AddBinding(m_RoadSelectionBinding = new ValueBinding<bool>("AccessStudio", "roadSelection", false));
            AddBinding(m_RemoteSelectionBinding = new ValueBinding<bool>("AccessStudio", "remoteSelection", false));
            AddBinding(m_HasOverrideBinding = new ValueBinding<bool>("AccessStudio", "hasOverride", false));
            AddBinding(m_PocStateBinding = new ValueBinding<string>("AccessStudio", "pocState", "NO OVERRIDE"));
            AddBinding(m_PocStatusBinding = new ValueBinding<string>("AccessStudio", "pocStatus", "Select a building, then choose Move Service Access."));
            AddBinding(m_PocTelemetryBinding = new ValueBinding<string>("AccessStudio", "pocTelemetry", "No service-access POC is active."));

            AddBinding(new TriggerBinding("AccessStudio", "toggle", Toggle));
            AddBinding(new TriggerBinding("AccessStudio", "activate", Activate));
            AddBinding(new TriggerBinding("AccessStudio", "refresh", Refresh));
            AddBinding(new TriggerBinding("AccessStudio", "clear", Clear));
            AddBinding(new TriggerBinding("AccessStudio", "close", Close));
            AddBinding(new TriggerBinding("AccessStudio", "moveService", MoveService));
            AddBinding(new TriggerBinding("AccessStudio", "placeRemote", PlaceRemote));
            AddBinding(new TriggerBinding("AccessStudio", "cancelMove", CancelMove));
            AddBinding(new TriggerBinding("AccessStudio", "resetVanilla", ResetVanilla));
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            m_Open = false;
            m_OpenBinding.Update(false);
            m_Probe?.ClearSelection();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (!m_Open || m_Probe == null) return;
            m_ToolActiveBinding.Update(m_Tool != null && m_Tool.IsActive);
            m_HasSelectionBinding.Update(m_Probe.SelectedBuilding != Entity.Null);
            m_StatusBinding.Update(m_Probe.Status);
            m_DiagnosticBinding.Update(m_Probe.DiagnosticText);
            m_RoadSelectionBinding.Update(m_Tool != null && m_Tool.Mode == AccessStudioToolMode.SelectServiceRoad);
            m_RemoteSelectionBinding.Update(m_Tool != null && m_Tool.Mode == AccessStudioToolMode.SelectRemoteServicePoint);
            m_HasOverrideBinding.Update(m_Poc != null && m_Poc.HasOverride(m_Probe.SelectedBuilding));
            m_PocStateBinding.Update(m_Poc?.State ?? "UNAVAILABLE");
            m_PocStatusBinding.Update(m_Poc?.Status ?? "POC system unavailable.");
            m_PocTelemetryBinding.Update(m_Poc?.Telemetry ?? "No telemetry.");
        }

        private void Toggle()
        {
            if (m_Open) Close();
            else Activate();
        }

        private void Activate()
        {
            m_Open = true;
            m_OpenBinding.Update(true);
            m_Tool?.Activate();
        }

        private void Refresh()
        {
            m_Probe?.RefreshSnapshot();
        }

        private void Clear()
        {
            m_Probe?.ClearSelection();
        }

        private void MoveService()
        {
            if (m_Probe == null || m_Probe.SelectedBuilding == Entity.Null)
            {
                Mod.Log.Warn("[AccessStudio] MOVE SERVICE ACCESS requested without a selected building.");
                return;
            }
            m_Tool?.BeginServiceRoadSelection();
        }

        private void CancelMove()
        {
            m_Tool?.CancelServiceRoadSelection();
        }

        private void PlaceRemote()
        {
            if (m_Probe == null || m_Probe.SelectedBuilding == Entity.Null)
            {
                Mod.Log.Warn("[AccessStudio] PLACE REMOTE SERVICE POINT requested without a selected building.");
                return;
            }
            m_Tool?.BeginRemoteServicePointSelection();
        }

        private void ResetVanilla()
        {
            if (m_Probe != null) m_Poc?.QueueReset(m_Probe.SelectedBuilding);
        }

        private void Close()
        {
            m_Open = false;
            m_OpenBinding.Update(false);
            m_Tool?.CancelServiceRoadSelection();
            if (m_ToolSystem != null && m_DefaultTool != null && m_ToolSystem.activeTool == m_Tool)
                m_ToolSystem.activeTool = m_DefaultTool;
        }
    }
}
