using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Rendering;
using Game.Tools;
using Game.UI;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace EventEngine
{
    public partial class EventEngineUISystem : UISystemBase
    {
        private static new readonly ILog log = Mod.log;

        private ValueBinding<bool> m_IsOpenBinding;
        private ValueBinding<string> m_ActiveTabBinding;
        private ValueBinding<string> m_StatusBadgeBinding;
        private ValueBinding<int> m_ActiveEventsCountBinding;
        private ValueBinding<string> m_EventsJsonBinding;
        private ValueBinding<string> m_VenuesJsonBinding;
        private ValueBinding<string> m_SelectedVenueJsonBinding;
        private ValueBinding<string> m_NextEventJsonBinding;
        private ValueBinding<string> m_ToastMessageBinding;
        private ValueBinding<bool> m_IsVenuePickingActiveBinding;
        private ValueBinding<bool> m_LauncherSelectedBinding;
        private ValueBinding<string> m_ScheduleStatusBinding;

        private EventEngineSystem m_EventSystem;
        private EventEngineVenueToolSystem m_VenueTool;
        private CameraUpdateSystem m_CameraUpdateSystem;
        private bool m_IsOpen = false;
        private string m_ActiveTab = "overview";

        protected override void OnCreate()
        {
            base.OnCreate();
            log.Info("[EventEngineUISystem] Initializing UI bindings...");

            m_EventSystem = World.GetOrCreateSystemManaged<EventEngineSystem>();
            m_VenueTool = World.GetOrCreateSystemManaged<EventEngineVenueToolSystem>();
            m_CameraUpdateSystem = World.GetOrCreateSystemManaged<CameraUpdateSystem>();

            AddBinding(m_IsOpenBinding = new ValueBinding<bool>("EventEngine", "isOpen", false));
            AddBinding(m_ActiveTabBinding = new ValueBinding<string>("EventEngine", "activeTab", "overview"));
            AddBinding(m_StatusBadgeBinding = new ValueBinding<string>("EventEngine", "statusBadge", "No Events"));
            AddBinding(m_ActiveEventsCountBinding = new ValueBinding<int>("EventEngine", "activeEventsCount", 0));
            AddBinding(m_EventsJsonBinding = new ValueBinding<string>("EventEngine", "eventsJson", "[]"));
            AddBinding(m_VenuesJsonBinding = new ValueBinding<string>("EventEngine", "venuesJson", "[]"));
            AddBinding(m_SelectedVenueJsonBinding = new ValueBinding<string>("EventEngine", "selectedVenueJson", "null"));
            AddBinding(m_NextEventJsonBinding = new ValueBinding<string>("EventEngine", "nextEventJson", "null"));
            AddBinding(m_ToastMessageBinding = new ValueBinding<string>("EventEngine", "toastMessage", ""));
            AddBinding(m_IsVenuePickingActiveBinding = new ValueBinding<bool>("EventEngine", "isVenuePickingActive", false));
            AddBinding(m_LauncherSelectedBinding = new ValueBinding<bool>("EventEngine", "launcherSelected", false));
            AddBinding(m_ScheduleStatusBinding = new ValueBinding<string>("EventEngine", "scheduleStatus", "idle"));

            // Triggers
            AddBinding(new TriggerBinding<bool>("EventEngine", "setOpen", OnSetOpen));
            AddBinding(new TriggerBinding<string>("EventEngine", "setActiveTab", OnSetActiveTab));
            AddBinding(new TriggerBinding<int>("EventEngine", "selectVenue", OnSelectVenue));
            AddBinding(new TriggerBinding<string>("EventEngine", "scheduleEventJson", OnScheduleEventJson));
            AddBinding(new TriggerBinding<int>("EventEngine", "cancelEvent", OnCancelEvent));
            AddBinding(new TriggerBinding<int>("EventEngine", "panToVenue", OnPanToVenue));

            // Quick Test Triggers
            AddBinding(new TriggerBinding<int>("EventEngine", "startEventNow", OnStartEventNow));
            AddBinding(new TriggerBinding<int, int>("EventEngine", "simulateAttendance", OnSimulateAttendance));
            AddBinding(new TriggerBinding<int>("EventEngine", "endEvent", OnEndEvent));
            AddBinding(new TriggerBinding("EventEngine", "resetAllEvents", OnResetAllEvents));
            AddBinding(new TriggerBinding("EventEngine", "toggleVenuePicker", OnToggleVenuePicker));
            AddBinding(new TriggerBinding("EventEngine", "cancelVenuePicker", CancelVenuePicking));

            log.Info("[EventEngineUISystem] Bindings successfully registered.");
        }

        private void OnSetOpen(bool open)
        {
            if (!open)
            {
                ClosePanelAndTool();
                return;
            }

            if (m_EventSystem != null) m_EventSystem.RefreshDiscoveredVenues();
            StopVenuePickingTool();
            SetPanelState(true, m_ActiveTab);
            UpdateUiState();
        }

        private void OnSetActiveTab(string tab)
        {
            m_ActiveTab = tab;
            m_ActiveTabBinding?.Update(m_ActiveTab);
        }

        private void OnSelectVenue(int entityIndex)
        {
            m_EventSystem?.SelectVenue(entityIndex);
            UpdateUiState();
        }

        [Serializable]
        public class ScheduleEventPayload
        {
            public string title;
            public int category;
            public int venueIdx;
            public int attendance;
            public int startHour;
            public int startMinute;
            public int duration;
            public float arrivalInt;
            public float depInt;
            public int carPct;
            public int transitPct;
            public int taxiPct;
        }

        private void OnScheduleEventJson(string json)
        {
            try
            {
                var payload = JsonUtility.FromJson<ScheduleEventPayload>(json);
                if (payload == null) throw new ArgumentException("The scheduling request was empty or malformed.");
                if (m_EventSystem == null) throw new InvalidOperationException("The Event Engine simulation is unavailable.");
                if (!Enum.IsDefined(typeof(EventCategory), payload.category))
                    throw new ArgumentOutOfRangeException(nameof(payload.category), "Choose a valid event category.");

                EventRecord created = m_EventSystem.ScheduleEvent(
                    payload.title,
                    (EventCategory)payload.category,
                    payload.venueIdx,
                    payload.attendance,
                    payload.startHour,
                    payload.duration,
                    payload.startMinute,
                    payload.arrivalInt,
                    payload.depInt,
                    payload.carPct,
                    payload.transitPct,
                    payload.taxiPct
                );

                StopVenuePickingTool();
                m_ScheduleStatusBinding?.Update("success");
                SetPanelState(true, "overview");
                UpdateUiState();
                log.Info($"[EventEngine] Scheduling workflow complete for event #{created.Id}; panel=open tab=overview picker=false.");
            }
            catch (Exception ex)
            {
                log.Error(ex, $"[EventEngine] Error parsing schedule event json: {json}");
                StopVenuePickingTool();
                m_EventSystem?.ShowToast($"Could not schedule event: {ex.Message}");
                m_ScheduleStatusBinding?.Update("error");
                SetPanelState(true, "schedule");
                UpdateUiState();
            }
        }

        private void OnCancelEvent(int id)
        {
            m_EventSystem?.CancelEvent(id);
            UpdateUiState();
        }

        private void OnPanToVenue(int entityIndex)
        {
            var venue = m_EventSystem?.DiscoveredVenues.FirstOrDefault(v => v.EntityIndex == entityIndex);
            if (venue != null && m_CameraUpdateSystem != null && m_CameraUpdateSystem.activeCameraController != null)
            {
                m_CameraUpdateSystem.activeCameraController.pivot = new Vector3(venue.Position.x, venue.Position.y, venue.Position.z);
                m_EventSystem.ShowToast($"Centered camera on {venue.Name}");
            }
        }

        private void OnStartEventNow(int id)
        {
            m_EventSystem?.QuickTestStartNow(id);
            UpdateUiState();
        }

        private void OnSimulateAttendance(int id, int amount)
        {
            m_EventSystem?.QuickTestSimulateAttendance(id, amount);
            UpdateUiState();
        }

        private void OnEndEvent(int id)
        {
            m_EventSystem?.QuickTestEndEvent(id);
            UpdateUiState();
        }

        private void OnResetAllEvents()
        {
            m_EventSystem?.QuickTestResetAll();
            UpdateUiState();
        }

        private void OnToggleVenuePicker()
        {
            if (m_EventSystem == null || m_VenueTool == null) return;

            if (m_EventSystem.IsVenuePickingActive)
            {
                CancelVenuePicking();
                return;
            }

            m_EventSystem.RefreshDiscoveredVenues();
            m_EventSystem.IsVenuePickingActive = true;
            m_IsOpen = false;
            SynchronizePanelBindings();
            m_VenueTool.Activate();
            m_EventSystem.ShowToast("Click a stadium or venue on the map; RMB or ESC cancels");
            log.Info("[EventEngine] Entered venue-picking mode; panel hidden temporarily, launcher remains selected.");
        }

        public void CompleteVenuePicking()
        {
            StopVenuePickingTool();
            SetPanelState(true, "schedule");
            UpdateUiState();
            log.Info("[EventEngine] Venue selection completed; panel restored on Schedule tab.");
        }

        public void CancelVenuePicking()
        {
            if (m_EventSystem == null) return;
            bool wasPicking = m_EventSystem.IsVenuePickingActive;
            StopVenuePickingTool();
            SetPanelState(true, "venues");
            if (wasPicking) m_EventSystem.ShowToast("Venue selection cancelled — no event was created");
            UpdateUiState();
            log.Info("[EventEngine] Venue selection cancelled; panel restored on Venues tab.");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (m_EventSystem == null) return;

            // Recover immediately if another CS2 tool replaces the temporary picker.
            if (m_EventSystem.IsVenuePickingActive && (m_VenueTool == null || !m_VenueTool.IsActive))
            {
                CancelVenuePicking();
                return;
            }

            if (!m_IsOpen) return;

            UpdateUiState();
        }

        private void SetPanelState(bool open, string tab)
        {
            m_IsOpen = open;
            if (!string.IsNullOrWhiteSpace(tab)) m_ActiveTab = tab;
            SynchronizePanelBindings();
        }

        private void SynchronizePanelBindings()
        {
            bool picking = m_EventSystem != null && m_EventSystem.IsVenuePickingActive;
            m_IsOpenBinding?.Update(m_IsOpen);
            m_ActiveTabBinding?.Update(m_ActiveTab);
            m_IsVenuePickingActiveBinding?.Update(picking);
            m_LauncherSelectedBinding?.Update(m_IsOpen || picking);
        }

        private void StopVenuePickingTool()
        {
            if (m_EventSystem != null) m_EventSystem.IsVenuePickingActive = false;
            m_VenueTool?.ExitToDefaultTool();
            m_IsVenuePickingActiveBinding?.Update(false);
        }

        private void ClosePanelAndTool()
        {
            StopVenuePickingTool();
            m_IsOpen = false;
            SynchronizePanelBindings();
            log.Info("[EventEngine] Panel closed; picker stopped and launcher deselected.");
        }

        private void UpdateUiState()
        {
            try
            {
                var events = m_EventSystem.Events;
                var venues = m_EventSystem.DiscoveredVenues;
                var selectedVenue = m_EventSystem.SelectedVenue;

                m_ActiveEventsCountBinding?.Update(events.Count(e => e.Phase != EventPhase.Completed));
                m_ToastMessageBinding?.Update(m_EventSystem.LastToastMessage ?? "");
                m_IsVenuePickingActiveBinding?.Update(m_EventSystem.IsVenuePickingActive);

                // Status Badge
                var liveEvent = events.FirstOrDefault(e => e.Phase == EventPhase.Live);
                var arrivingEvent = events.FirstOrDefault(e => e.Phase == EventPhase.Arrivals);
                var departingEvent = events.FirstOrDefault(e => e.Phase == EventPhase.Departing);

                if (liveEvent != null)
                    m_StatusBadgeBinding?.Update("LIVE");
                else if (departingEvent != null)
                    m_StatusBadgeBinding?.Update("Event Ending");
                else if (arrivingEvent != null)
                    m_StatusBadgeBinding?.Update("Event Starting");
                else if (events.Any(e => e.Phase == EventPhase.Scheduled))
                    m_StatusBadgeBinding?.Update("Event Scheduled");
                else
                    m_StatusBadgeBinding?.Update("No Events");

                // Next Event JSON
                var nextEv = liveEvent ?? arrivingEvent ?? events.Where(e => e.Phase == EventPhase.Scheduled).OrderBy(e => e.MinutesUntilEvent).FirstOrDefault();
                if (nextEv != null)
                {
                    m_NextEventJsonBinding?.Update(SerializeEvent(nextEv));
                }
                else
                {
                    m_NextEventJsonBinding?.Update("null");
                }

                // Events JSON
                var eventStrings = events.Select(SerializeEvent).ToList();
                m_EventsJsonBinding?.Update("[" + string.Join(",", eventStrings) + "]");

                // Venues JSON
                var venueStrings = venues.Select(SerializeVenue).ToList();
                m_VenuesJsonBinding?.Update("[" + string.Join(",", venueStrings) + "]");

                // Selected Venue JSON
                if (selectedVenue != null)
                {
                    m_SelectedVenueJsonBinding?.Update(SerializeVenue(selectedVenue));
                }
                else
                {
                    m_SelectedVenueJsonBinding?.Update("null");
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "[EventEngineUISystem] UpdateUiState exception");
            }
        }

        private string SerializeEvent(EventRecord ev)
        {
            return string.Format(
                "{{\"id\":{0},\"title\":\"{1}\",\"category\":{2},\"categoryLabel\":\"{3}\",\"categoryIcon\":\"{4}\",\"venueEntityIndex\":{5},\"venueName\":\"{6}\",\"expectedAttendance\":{7},\"currentAttendance\":{8},\"travelingCount\":{9},\"carsCount\":{10},\"transitCount\":{11},\"walkingCount\":{12},\"taxiCount\":{13},\"startHour\":{14},\"startMinute\":{15},\"durationHours\":{16},\"minutesUntilEvent\":{17},\"phase\":{18},\"phaseLabel\":\"{19}\",\"trafficImpact\":\"{20}\",\"carUsagePercent\":{21},\"transitUsagePercent\":{22},\"taxiUsagePercent\":{23}}}",
                ev.Id,
                Escape(ev.Title),
                (int)ev.Category,
                Escape(ev.GetCategoryLabel()),
                Escape(ev.GetCategoryIcon()),
                ev.VenueEntityIndex,
                Escape(ev.VenueName),
                ev.ExpectedAttendance,
                ev.CurrentAttendance,
                ev.TravelingCount,
                ev.CarsCount,
                ev.TransitCount,
                ev.WalkingCount,
                ev.TaxiCount,
                ev.StartHour,
                ev.StartMinute,
                ev.DurationHours,
                ev.MinutesUntilEvent,
                (int)ev.Phase,
                ev.Phase.ToString(),
                ev.TrafficImpact,
                ev.CarUsagePercent,
                ev.TransitUsagePercent,
                ev.TaxiUsagePercent
            );
        }

        private string SerializeVenue(VenueRecord v)
        {
            return string.Format(
                "{{\"entityIndex\":{0},\"name\":\"{1}\",\"prefabName\":\"{2}\",\"typeDescription\":\"{3}\",\"capacity\":{4},\"currentOccupants\":{5},\"transitStopsNearby\":{6},\"parkingSpacesNearby\":{7},\"posX\":{8},\"posY\":{9},\"posZ\":{10}}}",
                v.EntityIndex,
                Escape(v.Name),
                Escape(v.PrefabName),
                Escape(v.TypeDescription),
                v.Capacity,
                v.CurrentOccupants,
                v.TransitStopsNearby,
                v.ParkingSpacesNearby,
                v.Position.x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                v.Position.y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                v.Position.z.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            );
        }

        private string Escape(string s) => (s ?? "")
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ");

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}
