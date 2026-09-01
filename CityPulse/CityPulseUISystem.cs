using System;
using System.Collections.Generic;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Common;
using Game.Rendering;
using Game.UI;
using Unity.Entities;
using Unity.Mathematics;

namespace CityPulse
{
    public partial class CityPulseUISystem : UISystemBase
    {
        public static ILog Log = LogManager.GetLogger(nameof(CityPulse)).SetShowsErrorsInUI(false);

        private AnalyticsScheduler m_Scheduler;
        private CameraUpdateSystem m_CameraUpdateSystem;

        private ValueBinding<bool> m_VisibleBinding;
        private ValueBinding<MasterCityOverviewData> m_OverviewBinding;
        private RawValueBinding m_AlertsBinding;

        // Domain Bindings
        private ValueBinding<TrafficSummaryData> m_TrafficSummaryBinding;
        private RawValueBinding m_TrafficBottlenecksBinding;

        private ValueBinding<TransitSummaryData> m_TransitSummaryBinding;
        private RawValueBinding m_TransitLinesBinding;
        private RawValueBinding m_TransitStopsBinding;

        private ValueBinding<ParkingSummaryData> m_ParkingSummaryBinding;
        private RawValueBinding m_ParkingFacilitiesBinding;

        private ValueBinding<ServiceSummaryData> m_ServiceSummaryBinding;
        private RawValueBinding m_ServiceFacilitiesBinding;

        private ValueBinding<BuildingSummaryData> m_BuildingSummaryBinding;
        private RawValueBinding m_BuildingIssuesBinding;
        private ValueBinding<SelectedBuildingDetail> m_SelectedBuildingBinding;

        private ValueBinding<NetworkSummaryData> m_NetworkSummaryBinding;
        private RawValueBinding m_NetworkDefectsBinding;

        private bool m_IsVisible = false;

        protected override void OnCreate()
        {
            base.OnCreate();
            Log.Info("CityPulseUISystem 3.0.4-beta.1 initializing...");

            m_Scheduler = World.GetOrCreateSystemManaged<AnalyticsScheduler>();
            m_CameraUpdateSystem = World.GetOrCreateSystemManaged<CameraUpdateSystem>();

            // UI State & Master Overview
            AddBinding(m_VisibleBinding = new ValueBinding<bool>("cityPulse", "visible", m_IsVisible));
            AddBinding(m_OverviewBinding = new ValueBinding<MasterCityOverviewData>("cityPulse", "overview", m_Scheduler.OverviewData));
            AddBinding(m_AlertsBinding = new RawValueBinding("cityPulse", "alerts", writer =>
            {
                var alerts = m_Scheduler?.TopCityAlerts ?? new List<UnifiedCityAlert>();
                writer.ArrayBegin(alerts.Count);
                for (int i = 0; i < alerts.Count; i++) alerts[i].Write(writer);
                writer.ArrayEnd();
            }));

            // Traffic
            AddBinding(m_TrafficSummaryBinding = new ValueBinding<TrafficSummaryData>("cityPulse", "traffic", m_Scheduler.TrafficSummary));
            AddBinding(m_TrafficBottlenecksBinding = new RawValueBinding("cityPulse", "trafficBottlenecks", writer =>
            {
                var list = m_Scheduler?.TrafficBottlenecks ?? new List<TrafficBottleneckData>();
                writer.ArrayBegin(list.Count);
                for (int i = 0; i < list.Count; i++) list[i].Write(writer);
                writer.ArrayEnd();
            }));

            // Transit
            AddBinding(m_TransitSummaryBinding = new ValueBinding<TransitSummaryData>("cityPulse", "transit", m_Scheduler.TransitSummary));
            AddBinding(m_TransitLinesBinding = new RawValueBinding("cityPulse", "transitLines", writer =>
            {
                var lines = m_Scheduler?.TransitLines ?? new List<TransitLineEntry>();
                writer.ArrayBegin(lines.Count);
                for (int i = 0; i < lines.Count; i++) lines[i].Write(writer);
                writer.ArrayEnd();
            }));
            AddBinding(m_TransitStopsBinding = new RawValueBinding("cityPulse", "transitStops", writer =>
            {
                var stops = m_Scheduler?.TransitStops ?? new List<TransitStopEntry>();
                writer.ArrayBegin(stops.Count);
                for (int i = 0; i < stops.Count; i++) stops[i].Write(writer);
                writer.ArrayEnd();
            }));

            // Parking
            AddBinding(m_ParkingSummaryBinding = new ValueBinding<ParkingSummaryData>("cityPulse", "parking", m_Scheduler.ParkingSummary));
            AddBinding(m_ParkingFacilitiesBinding = new RawValueBinding("cityPulse", "parkingFacilities", writer =>
            {
                var facs = m_Scheduler?.ParkingFacilities ?? new List<ParkingFacilityEntry>();
                writer.ArrayBegin(facs.Count);
                for (int i = 0; i < facs.Count; i++) facs[i].Write(writer);
                writer.ArrayEnd();
            }));

            // Services
            AddBinding(m_ServiceSummaryBinding = new ValueBinding<ServiceSummaryData>("cityPulse", "services", m_Scheduler.ServiceSummary));
            AddBinding(m_ServiceFacilitiesBinding = new RawValueBinding("cityPulse", "serviceFacilities", writer =>
            {
                var sFacs = m_Scheduler?.ServiceFacilities ?? new List<ServiceFacilityEntry>();
                writer.ArrayBegin(sFacs.Count);
                for (int i = 0; i < sFacs.Count; i++) sFacs[i].Write(writer);
                writer.ArrayEnd();
            }));

            // Buildings
            AddBinding(m_BuildingSummaryBinding = new ValueBinding<BuildingSummaryData>("cityPulse", "buildings", m_Scheduler.BuildingSummary));
            AddBinding(m_BuildingIssuesBinding = new RawValueBinding("cityPulse", "buildingIssues", writer =>
            {
                var issues = m_Scheduler?.BuildingIssues ?? new List<BuildingIssueEntry>();
                writer.ArrayBegin(issues.Count);
                for (int i = 0; i < issues.Count; i++) issues[i].Write(writer);
                writer.ArrayEnd();
            }));
            AddBinding(m_SelectedBuildingBinding = new ValueBinding<SelectedBuildingDetail>("cityPulse", "selectedBuilding", m_Scheduler.SelectedBuilding));

            // Network
            AddBinding(m_NetworkSummaryBinding = new ValueBinding<NetworkSummaryData>("cityPulse", "network", m_Scheduler.NetworkSummary));
            AddBinding(m_NetworkDefectsBinding = new RawValueBinding("cityPulse", "networkDefects", writer =>
            {
                var defects = m_Scheduler?.NetworkDefects ?? new List<NetworkDefectEntry>();
                writer.ArrayBegin(defects.Count);
                for (int i = 0; i < defects.Count; i++) defects[i].Write(writer);
                writer.ArrayEnd();
            }));

            // Timeline
            AddBinding(m_TimelineBinding = new RawValueBinding("cityPulse", "timeline", writer =>
            {
                var timeline = m_Scheduler?.CityTimeline ?? new List<CityTimelinePoint>();
                writer.ArrayBegin(timeline.Count);
                for (int i = 0; i < timeline.Count; i++) timeline[i].Write(writer);
                writer.ArrayEnd();
            }));

            // Before / After
            AddBinding(m_BeforeAfterBinding = new ValueBinding<BeforeAfterComparisonData>("cityPulse", "beforeAfter", m_Scheduler.GetBeforeAfterComparison(0)));

            AddBinding(new TriggerBinding("cityPulse", "toggleVisible", () =>
            {
                m_IsVisible = !m_IsVisible;
                m_VisibleBinding.Update(m_IsVisible);
            }));

            AddBinding(new TriggerBinding("cityPulse", "closeVisible", () =>
            {
                m_IsVisible = false;
                m_VisibleBinding.Update(false);
            }));

            AddBinding(new TriggerBinding<float, float, float>("cityPulse", "jumpToPosition", (x, y, z) =>
            {
                CameraNavigator.JumpToCoordinates(m_CameraUpdateSystem, new float3(x, y, z));
            }));

            AddBinding(new TriggerBinding<int>("cityPulse", "captureBaseline", (entityIndex) =>
            {
                m_Scheduler?.CaptureBaselineForEntity(entityIndex);
                m_BeforeAfterBinding?.Update(m_Scheduler.GetBeforeAfterComparison(entityIndex));
            }));

            AddBinding(new TriggerBinding<int>("cityPulse", "checkBeforeAfter", (entityIndex) =>
            {
                m_BeforeAfterBinding?.Update(m_Scheduler.GetBeforeAfterComparison(entityIndex));
            }));

            AddBinding(new TriggerBinding<string, int>("cityPulse", "launchSuiteTool", (toolName, entityIndex) =>
            {
                m_Scheduler?.LaunchSuiteTool(toolName, entityIndex, float3.zero);
            }));

            m_Scheduler.OnDataUpdated += HandleDataUpdated;
            Log.Info("CityPulseUISystem v3.0.3 bindings registered successfully.");
        }

        private RawValueBinding m_TimelineBinding;
        private ValueBinding<BeforeAfterComparisonData> m_BeforeAfterBinding;

        private void HandleDataUpdated()
        {
            if (World == null || !World.IsCreated) return;

            m_OverviewBinding?.Update(m_Scheduler.OverviewData);
            m_AlertsBinding?.Update();

            m_TrafficSummaryBinding?.Update(m_Scheduler.TrafficSummary);
            m_TrafficBottlenecksBinding?.Update();

            m_TransitSummaryBinding?.Update(m_Scheduler.TransitSummary);
            m_TransitLinesBinding?.Update();
            m_TransitStopsBinding?.Update();

            m_ParkingSummaryBinding?.Update(m_Scheduler.ParkingSummary);
            m_ParkingFacilitiesBinding?.Update();

            m_ServiceSummaryBinding?.Update(m_Scheduler.ServiceSummary);
            m_ServiceFacilitiesBinding?.Update();

            m_BuildingSummaryBinding?.Update(m_Scheduler.BuildingSummary);
            m_BuildingIssuesBinding?.Update();
            m_SelectedBuildingBinding?.Update(m_Scheduler.SelectedBuilding);

            m_NetworkSummaryBinding?.Update(m_Scheduler.NetworkSummary);
            m_NetworkDefectsBinding?.Update();
            m_TimelineBinding?.Update();
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            m_IsVisible = false;
            m_VisibleBinding?.Update(false);
        }

        protected override void OnDestroy()
        {
            if (m_Scheduler != null)
            {
                m_Scheduler.OnDataUpdated -= HandleDataUpdated;
            }
            base.OnDestroy();
        }
    }
}
