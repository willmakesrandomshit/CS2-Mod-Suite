using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Tools;
using Game.UI;
using Unity.Entities;

namespace RoadRules
{
    public partial class RoadRulesUISystem : UISystemBase
    {
        public static RoadRulesUISystem Instance { get; private set; }
        public bool PanelOpen => m_PanelOpenBinding?.value == true;

        private ToolSystem m_ToolSystem;
        private DefaultToolSystem m_DefaultTool;
        private RoadRulesToolSystem m_RoadRulesTool;

        private ValueBinding<string> m_StatusBinding;
        private ValueBinding<bool> m_HasSelectionBinding;
        private ValueBinding<bool> m_ToolActiveBinding;
        private ValueBinding<int> m_SelectedEdgeIdBinding;
        private ValueBinding<int> m_LaneCountBinding;
        private ValueBinding<int> m_SelectedLaneIdxBinding;
        private ValueBinding<int> m_AllowedVehiclesBinding;
        private ValueBinding<bool> m_IsLocalAccessBinding;
        private ValueBinding<bool> m_IsClosedBinding;
        private ValueBinding<float> m_SpeedLimitBinding;
        private ValueBinding<string> m_PresetNameBinding;
        private ValueBinding<bool> m_HasClipboardBinding;
        private ValueBinding<bool> m_PanelOpenBinding;

        private ValueBinding<string> m_CarriagewayNameBinding;
        private ValueBinding<int> m_TotalSubLanesBinding;
        private ValueBinding<int> m_RawCarLanesBinding;
        private ValueBinding<int> m_DirectionMatchedCarLanesBinding;
        private ValueBinding<int> m_CarriagewayMatchedCarLanesBinding;
        private ValueBinding<int> m_FilteredOutSubLanesBinding;
        private ValueBinding<string> m_PhysicalLaneDebugBinding;

        private ValueBinding<int> m_RebuildsCountBinding;
        private ValueBinding<int> m_VehiclesReroutedBinding;
        private ValueBinding<int> m_ActiveEnforcedBinding;
        private ValueBinding<int> m_ExistingPathsSignaledBinding;
        private ValueBinding<bool> m_NativeUpdateRequestedBinding;
        private ValueBinding<string> m_EnforcementSummaryBinding;
        private ValueBinding<int> m_ObservedCarsBinding;
        private ValueBinding<int> m_ObservedTrucksBinding;
        private ValueBinding<int> m_ObservedBusesBinding;
        private ValueBinding<int> m_ObservedTaxisBinding;
        private ValueBinding<int> m_ObservedEmergencyBinding;
        private ValueBinding<int> m_ObservedServicesBinding;
        private ValueBinding<int> m_ObservedViolationsBinding;

        private RawValueBinding m_LanesBinding;

        private ValueBinding<string> m_ActiveToolNameBinding;
        private ValueBinding<string> m_LastRawEntityBinding;
        private ValueBinding<string> m_LastResolvedEdgeBinding;
        private ValueBinding<string> m_LastFailureReasonBinding;
        private ValueBinding<string> m_LastRawComponentsBinding;
        private ValueBinding<string> m_ModVersionBinding;
        private ValueBinding<bool> m_PropagationActiveBinding;
        private ValueBinding<string> m_PropagationMessageBinding;

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_DefaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            m_RoadRulesTool = World.GetOrCreateSystemManaged<RoadRulesToolSystem>();

            AddBinding(m_StatusBinding = new ValueBinding<string>("RoadRules", "status", "Road Rules ready — Select a road"));
            AddBinding(m_HasSelectionBinding = new ValueBinding<bool>("RoadRules", "hasSelection", false));
            AddBinding(m_ToolActiveBinding = new ValueBinding<bool>("RoadRules", "toolActive", false));
            AddBinding(m_ActiveToolNameBinding = new ValueBinding<string>("RoadRules", "activeToolName", "DefaultTool"));
            AddBinding(m_LastRawEntityBinding = new ValueBinding<string>("RoadRules", "lastRawEntity", "None"));
            AddBinding(m_LastResolvedEdgeBinding = new ValueBinding<string>("RoadRules", "lastResolvedEdge", "None"));
            AddBinding(m_LastFailureReasonBinding = new ValueBinding<string>("RoadRules", "lastFailureReason", "None"));
            AddBinding(m_LastRawComponentsBinding = new ValueBinding<string>("RoadRules", "lastRawComponents", "None"));
            AddBinding(m_ModVersionBinding = new ValueBinding<string>("RoadRules", "modVersion", "1.4.5-beta.1 BETA"));

            AddBinding(m_SelectedEdgeIdBinding = new ValueBinding<int>("RoadRules", "selectedEdgeId", 0));
            AddBinding(m_LaneCountBinding = new ValueBinding<int>("RoadRules", "laneCount", 0));
            AddBinding(m_SelectedLaneIdxBinding = new ValueBinding<int>("RoadRules", "selectedLaneIdx", 0));
            AddBinding(m_AllowedVehiclesBinding = new ValueBinding<int>("RoadRules", "allowedVehicles", (int)VehicleAccessFlags.All));
            AddBinding(m_IsLocalAccessBinding = new ValueBinding<bool>("RoadRules", "isLocalAccess", false));
            AddBinding(m_IsClosedBinding = new ValueBinding<bool>("RoadRules", "isClosed", false));
            AddBinding(m_SpeedLimitBinding = new ValueBinding<float>("RoadRules", "speedLimit", 0f));
            AddBinding(m_PresetNameBinding = new ValueBinding<string>("RoadRules", "presetName", "Default"));
            AddBinding(m_HasClipboardBinding = new ValueBinding<bool>("RoadRules", "hasClipboard", false));
            AddBinding(m_PanelOpenBinding = new ValueBinding<bool>("RoadRules", "panelOpen", false));

            AddBinding(m_CarriagewayNameBinding = new ValueBinding<string>("RoadRules", "carriagewayName", "Selected Carriageway"));
            AddBinding(m_TotalSubLanesBinding = new ValueBinding<int>("RoadRules", "totalSubLanes", 0));
            AddBinding(m_RawCarLanesBinding = new ValueBinding<int>("RoadRules", "rawCarLanes", 0));
            AddBinding(m_DirectionMatchedCarLanesBinding = new ValueBinding<int>("RoadRules", "directionMatchedCarLanes", 0));
            AddBinding(m_CarriagewayMatchedCarLanesBinding = new ValueBinding<int>("RoadRules", "carriagewayMatchedCarLanes", 0));
            AddBinding(m_FilteredOutSubLanesBinding = new ValueBinding<int>("RoadRules", "filteredOutSubLanes", 0));
            AddBinding(m_PhysicalLaneDebugBinding = new ValueBinding<string>("RoadRules", "physicalLaneDebug", "None"));
            AddBinding(m_PropagationActiveBinding = new ValueBinding<bool>("RoadRules", "propagationActive", false));
            AddBinding(m_PropagationMessageBinding = new ValueBinding<string>("RoadRules", "propagationMessage", string.Empty));

            AddBinding(m_RebuildsCountBinding = new ValueBinding<int>("RoadRules", "rebuildsCount", 0));
            AddBinding(m_VehiclesReroutedBinding = new ValueBinding<int>("RoadRules", "vehiclesRerouted", 0));
            AddBinding(m_ActiveEnforcedBinding = new ValueBinding<int>("RoadRules", "activeEnforced", 0));
            AddBinding(m_ExistingPathsSignaledBinding = new ValueBinding<int>("RoadRules", "existingPathsSignaled", 0));
            AddBinding(m_NativeUpdateRequestedBinding = new ValueBinding<bool>("RoadRules", "nativeUpdateRequested", false));
            AddBinding(m_EnforcementSummaryBinding = new ValueBinding<string>("RoadRules", "enforcementSummary", "VANILLA"));
            AddBinding(m_ObservedCarsBinding = new ValueBinding<int>("RoadRules", "observedCars", 0));
            AddBinding(m_ObservedTrucksBinding = new ValueBinding<int>("RoadRules", "observedTrucks", 0));
            AddBinding(m_ObservedBusesBinding = new ValueBinding<int>("RoadRules", "observedBuses", 0));
            AddBinding(m_ObservedTaxisBinding = new ValueBinding<int>("RoadRules", "observedTaxis", 0));
            AddBinding(m_ObservedEmergencyBinding = new ValueBinding<int>("RoadRules", "observedEmergency", 0));
            AddBinding(m_ObservedServicesBinding = new ValueBinding<int>("RoadRules", "observedServices", 0));
            AddBinding(m_ObservedViolationsBinding = new ValueBinding<int>("RoadRules", "observedViolations", 0));

            AddBinding(m_LanesBinding = new RawValueBinding("RoadRules", "lanes", WriteLanes));

            // Tool and Panel Controls
            AddBinding(new TriggerBinding("RoadRules", "togglePanel", OnTogglePanel));
            AddBinding(new TriggerBinding("RoadRules", "closePanel", OnClosePanel));
            AddBinding(new TriggerBinding("RoadRules", "activateTool", OnActivateTool));

            // Lane and Vehicle Mutators
            AddBinding(new TriggerBinding<int>("RoadRules", "selectLane", OnSelectLane));
            AddBinding(new TriggerBinding<int>("RoadRules", "toggleVehicle", OnToggleVehicle));
            AddBinding(new TriggerBinding<int>("RoadRules", "setAllowedVehicles", OnSetAllowedVehicles));
            AddBinding(new TriggerBinding<bool>("RoadRules", "setLocalAccess", OnSetLocalAccess));
            AddBinding(new TriggerBinding<bool>("RoadRules", "setClosed", OnSetClosed));
            AddBinding(new TriggerBinding("RoadRules", "resetLane", OnResetLane));

            // Presets and Clipboard
            AddBinding(new TriggerBinding<string>("RoadRules", "applyPreset", OnApplyPreset));
            AddBinding(new TriggerBinding<string>("RoadRules", "applyPresetAll", OnApplyPresetAll));
            AddBinding(new TriggerBinding("RoadRules", "copyRules", OnCopyRules));
            AddBinding(new TriggerBinding("RoadRules", "pasteRules", OnPasteRules));
        }

        public void SetPanelOpen(bool open)
        {
            if (m_PanelOpenBinding != null && m_PanelOpenBinding.value != open)
            {
                m_PanelOpenBinding.Update(open);
                if (open)
                {
                    RoadRulesToolSystem.log.Info("[RoadRules] PANEL OPEN");
                }
                else
                {
                    RoadRulesToolSystem.log.Info("[RoadRules] PANEL CLOSED");
                }
            }
        }

        private void OnTogglePanel()
        {
            if (m_ToolSystem != null && m_RoadRulesTool != null && m_DefaultTool != null)
            {
                if (m_ToolSystem.activeTool == m_RoadRulesTool)
                {
                    RoadRulesToolSystem.log.Info("[RoadRules] TogglePanel: Deactivating RoadRulesTool, switching to DefaultTool.");
                    m_ToolSystem.activeTool = m_DefaultTool;
                    SetPanelOpen(false);
                }
                else
                {
                    RoadRulesToolSystem.log.Info("[RoadRules] TogglePanel: REQUEST TOOL ACTIVATION -> RoadRulesTool");
                    m_ToolSystem.activeTool = m_RoadRulesTool;
                    SetPanelOpen(true);
                    RoadRulesToolSystem.log.Info($"[RoadRules] ACTIVE TOOL = {m_ToolSystem.activeTool?.toolID}");
                }
            }
            else
            {
                SetPanelOpen(!m_PanelOpenBinding.value);
            }
        }

        private void OnClosePanel()
        {
            RoadRulesToolSystem.log.Info("[RoadRules] OnClosePanel called.");
            if (m_ToolSystem != null && m_DefaultTool != null && m_ToolSystem.activeTool == m_RoadRulesTool)
            {
                m_ToolSystem.activeTool = m_DefaultTool;
            }
            SetPanelOpen(false);
        }

        private void OnActivateTool()
        {
            RoadRulesToolSystem.log.Info("[RoadRules] OnActivateTool: REQUEST TOOL ACTIVATION -> RoadRulesTool");
            if (m_ToolSystem != null && m_RoadRulesTool != null)
            {
                m_ToolSystem.activeTool = m_RoadRulesTool;
            }
            SetPanelOpen(true);
            RoadRulesToolSystem.log.Info($"[RoadRules] ACTIVE TOOL = {m_ToolSystem?.activeTool?.toolID}");
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            SetPanelOpen(false);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (World == null || !World.IsCreated) return;

            try
            {
                var sys = RoadRulesSystem.Instance;
                var pathfindSys = RoadRulesPathfindSystem.Instance;

                bool isToolActive = m_ToolSystem != null && m_RoadRulesTool != null && m_ToolSystem.activeTool == m_RoadRulesTool;
                m_ToolActiveBinding?.Update(isToolActive);
                m_ActiveToolNameBinding?.Update(m_ToolSystem?.activeTool?.toolID ?? "Unknown");

                if (pathfindSys != null)
                {
                    m_RebuildsCountBinding?.Update(pathfindSys.RebuildsTriggeredCount);
                    m_VehiclesReroutedBinding?.Update(pathfindSys.VehiclesReroutedCount);
                    m_ActiveEnforcedBinding?.Update(pathfindSys.ActiveEnforcedEntitiesCount);
                    m_ExistingPathsSignaledBinding?.Update(pathfindSys.ExistingPathsSignaledLastApply);
                    m_NativeUpdateRequestedBinding?.Update(pathfindSys.NativeUpdateRequestedLastApply);
                    m_EnforcementSummaryBinding?.Update(pathfindSys.EnforcementSummary ?? "VANILLA");
                    m_ObservedCarsBinding?.Update(pathfindSys.ObservedCars);
                    m_ObservedTrucksBinding?.Update(pathfindSys.ObservedTrucks);
                    m_ObservedBusesBinding?.Update(pathfindSys.ObservedBuses);
                    m_ObservedTaxisBinding?.Update(pathfindSys.ObservedTaxis);
                    m_ObservedEmergencyBinding?.Update(pathfindSys.ObservedEmergency);
                    m_ObservedServicesBinding?.Update(pathfindSys.ObservedServices);
                    m_ObservedViolationsBinding?.Update(pathfindSys.ObservedViolations);
                }

                if (sys != null)
                {
                    m_StatusBinding?.Update(sys.LastStatus ?? "Road Rules ready");
                    m_LastRawEntityBinding?.Update(sys.LastRawHitString ?? "None");
                    m_LastResolvedEdgeBinding?.Update(sys.LastResolvedEdgeString ?? "None");
                    m_LastFailureReasonBinding?.Update(sys.LastFailureReason ?? "None");
                    m_LastRawComponentsBinding?.Update(sys.LastRawComponentsString ?? "None");
                    m_PhysicalLaneDebugBinding?.Update(sys.PhysicalLaneDebugSummary ?? "None");
                    m_ModVersionBinding?.Update("1.4.5-beta.1 BETA");
                    m_PropagationActiveBinding?.Update(sys.PropagationNoticeActive);
                    m_PropagationMessageBinding?.Update(sys.PropagationNotice ?? string.Empty);

                    bool hasEdge = sys.SelectedEdge != Entity.Null && sys.CurrentRecord != null && EntityManager.Exists(sys.SelectedEdge) && !EntityManager.HasComponent<Deleted>(sys.SelectedEdge);
                    m_HasSelectionBinding?.Update(hasEdge);
                    m_HasClipboardBinding?.Update(sys.HasClipboard);

                    if (hasEdge && sys.CurrentRecord.PhysicalLanes != null && sys.CurrentRecord.PhysicalLanes.Count > 0)
                    {
                        m_SelectedEdgeIdBinding?.Update(sys.SelectedEdge.Index);
                        m_CarriagewayNameBinding?.Update(sys.CurrentRecord.CarriagewayName ?? "Selected Carriageway");
                        m_TotalSubLanesBinding?.Update(sys.CurrentRecord.TotalEdgeSubLanes);
                        m_RawCarLanesBinding?.Update(sys.CurrentRecord.RawCarLaneCount);
                        m_DirectionMatchedCarLanesBinding?.Update(sys.CurrentRecord.DirectionMatchedCarLaneCount);
                        m_CarriagewayMatchedCarLanesBinding?.Update(sys.CurrentRecord.CarriagewayMatchedCarLaneCount);
                        m_FilteredOutSubLanesBinding?.Update(sys.CurrentRecord.FilteredOutSubLanes);
                        m_LaneCountBinding?.Update(sys.CurrentRecord.PhysicalLanes.Count);
                        m_SelectedLaneIdxBinding?.Update(sys.SelectedLaneIndex);

                        int idx = sys.SelectedLaneIndex;
                        if (idx >= 0 && idx < sys.CurrentRecord.PhysicalLanes.Count)
                        {
                            var lane = sys.CurrentRecord.PhysicalLanes[idx];
                            m_AllowedVehiclesBinding?.Update((int)lane.AllowedVehicles);
                            m_IsLocalAccessBinding?.Update(lane.LocalAccessOnly);
                            m_IsClosedBinding?.Update(lane.IsClosed);
                            m_SpeedLimitBinding?.Update(lane.CustomSpeedLimitKph);
                            m_PresetNameBinding?.Update(lane.Preset.ToString());
                        }
                    }
                    else
                    {
                        m_SelectedEdgeIdBinding?.Update(0);
                        m_CarriagewayNameBinding?.Update("None");
                        m_TotalSubLanesBinding?.Update(0);
                        m_RawCarLanesBinding?.Update(0);
                        m_DirectionMatchedCarLanesBinding?.Update(0);
                        m_FilteredOutSubLanesBinding?.Update(0);
                        m_LaneCountBinding?.Update(0);
                        m_SelectedLaneIdxBinding?.Update(0);
                        m_AllowedVehiclesBinding?.Update((int)VehicleAccessFlags.All);
                        m_IsLocalAccessBinding?.Update(false);
                        m_IsClosedBinding?.Update(false);
                        m_SpeedLimitBinding?.Update(0f);
                        m_PresetNameBinding?.Update("Default");
                    }

                    m_LanesBinding?.Update();
                }
            }
            catch (Exception ex)
            {
                RoadRulesSystem.Log.Error(ex, "RoadRulesUISystem.OnUpdate: Error updating UI bindings.");
            }
        }

        private void WriteLanes(IJsonWriter writer)
        {
            try
            {
                var list = RoadRulesSystem.Instance?.CurrentRecord?.PhysicalLanes ?? new List<PhysicalRoadLane>();
                writer.ArrayBegin(list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    var lane = list[i];
                    writer.TypeBegin("PhysicalRoadLane");
                    writer.PropertyName("laneIndex");
                    writer.Write(lane.PhysicalIndex);
                    writer.PropertyName("allowedVehicles");
                    writer.Write((int)lane.AllowedVehicles);
                    writer.PropertyName("isClosed");
                    writer.Write(lane.IsClosed);
                    writer.PropertyName("isLocalAccess");
                    writer.Write(lane.LocalAccessOnly);
                    writer.PropertyName("preset");
                    writer.Write(lane.Preset.ToString());
                    writer.PropertyName("lanePositionLabel");
                    writer.Write(lane.LanePositionLabel ?? $"Lane {lane.PhysicalIndex + 1}");
                    writer.PropertyName("lateralOffset");
                    writer.Write(lane.LateralOffset);
                    writer.PropertyName("routingCount");
                    writer.Write(lane.RoutingEntities.Count);
                    writer.PropertyName("primaryEntityId");
                    writer.Write(lane.PrimaryDisplayEntity.Index);
                    writer.TypeEnd();
                }
                writer.ArrayEnd();
            }
            catch (Exception ex)
            {
                RoadRulesSystem.Log.Error(ex, "RoadRulesUISystem.WriteLanes: Error serializing lane restrictions.");
                writer.ArrayBegin(0);
                writer.ArrayEnd();
            }
        }

        private void OnSelectLane(int index) => RoadRulesSystem.Instance?.SelectLane(index);
        private void OnToggleVehicle(int flag) => RoadRulesSystem.Instance?.ToggleVehicleAccess((VehicleAccessFlags)flag);
        private void OnSetAllowedVehicles(int bitmask) => RoadRulesSystem.Instance?.SetAllowedVehicles(bitmask);
        private void OnSetLocalAccess(bool isLocal) => RoadRulesSystem.Instance?.SetLocalAccess(isLocal);
        private void OnSetClosed(bool isClosed) => RoadRulesSystem.Instance?.SetClosed(isClosed);
        private void OnResetLane() => RoadRulesSystem.Instance?.ResetLane();

        private void OnApplyPreset(string presetStr)
        {
            if (Enum.TryParse<LaneRulePreset>(presetStr, true, out var preset))
            {
                RoadRulesSystem.Instance?.ApplyPreset(preset);
            }
        }
        private void OnApplyPresetAll(string presetStr)
        {
            if (Enum.TryParse<LaneRulePreset>(presetStr, true, out var preset))
            {
                RoadRulesSystem.Instance?.ApplyPresetToAllLanes(preset);
            }
        }
        private void OnCopyRules() => RoadRulesSystem.Instance?.CopyRules();
        private void OnPasteRules() => RoadRulesSystem.Instance?.PasteRules();

        protected override void OnDestroy()
        {
            m_ToolSystem = null;
            m_DefaultTool = null;
            m_RoadRulesTool = null;
            Instance = null;
            base.OnDestroy();
        }
    }
}
