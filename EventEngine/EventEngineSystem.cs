using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Logging;
using Colossal.Mathematics;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using RoutesTransportStop = Game.Routes.TransportStop;
using BuildingsParkingFacility = Game.Buildings.ParkingFacility;

namespace EventEngine
{
    /// <summary>
    /// Core Simulation and Event Scheduling Engine for Cities: Skylines II.
    /// Manages stadium, arena, concert hall, and venue event lifecycles:
    /// 1. Automatic dynamic discovery of compatible spectator venues across the city map.
    /// 2. In-world raycast venue selection and camera navigation.
    /// 3. Authentic multi-phase spectator demand curve generating native CS2 citizen trips (Cars, Transit, Walking, Taxis).
    /// 4. Post-event departure surge routing stadium occupants safely back to homes/hotels.
    /// 5. Live traffic telemetry tracking vehicles, transit riders, and pedestrians converging on the venue.
    /// </summary>
    public partial class EventEngineSystem : GameSystemBase
    {
        private static ILog log = Mod.log;
        public static EventEngineSystem Instance { get; private set; }

        private EntityQuery m_VenueBuildingQuery;
        private EntityQuery m_CitizenQuery;
        private EntityQuery m_TransitStopQuery;
        private EntityQuery m_ParkingFacilityQuery;
        private TimeSystem m_TimeSystem;

        public List<EventRecord> Events { get; private set; } = new List<EventRecord>();
        public List<VenueRecord> DiscoveredVenues { get; private set; } = new List<VenueRecord>();
        public VenueRecord SelectedVenue { get; private set; }

        public string LastToastMessage { get; private set; } = null;
        public bool IsVenuePickingActive { get; set; } = false;

        private int m_NextEventId = 1;
        private uint m_SimulationTickCounter = 0;
        private readonly Dictionary<int, HashSet<Entity>> m_DispatchedCitizens = new Dictionary<int, HashSet<Entity>>();
        private readonly HashSet<Entity> m_AssignedCitizens = new HashSet<Entity>();
        private readonly Dictionary<int, int> m_ArrivalScanCursors = new Dictionary<int, int>();
        private bool m_ClockWarningShown;

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            m_TimeSystem = World.GetOrCreateSystemManaged<TimeSystem>();

            m_VenueBuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Building>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<Game.Objects.Transform>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            m_CitizenQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Citizen>(),
                    ComponentType.ReadOnly<CurrentBuilding>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            m_TransitStopQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<RoutesTransportStop>(),
                    ComponentType.ReadOnly<Game.Objects.Transform>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>()
                }
            });

            m_ParkingFacilityQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<BuildingsParkingFacility>(),
                    ComponentType.ReadOnly<Game.Objects.Transform>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>()
                }
            });

            log.Info("[EventEngineSystem] Created — Native Spectator & Stadium Event Simulation active.");
        }

        protected override void OnUpdate()
        {
            m_SimulationTickCounter++;

            // Venue discovery is slower than lifecycle polling; UI actions also refresh it.
            if (m_SimulationTickCounter % 1024 == 1)
                RefreshDiscoveredVenues();
            if (m_SimulationTickCounter % 64 == 0)
            {
                ProcessEventLifecycles();
            }
        }

        /// <summary>
        /// Automatically discovers all compatible spectator venues (Stadiums, Arenas, Concert Halls, Parks) on the city map.
        /// </summary>
        public void RefreshDiscoveredVenues()
        {
            NativeArray<Entity> buildingEntities = default;
            NativeArray<Game.Objects.Transform> transitStops = default;
            NativeArray<Game.Objects.Transform> parkingFacilities = default;
            try
            {
                buildingEntities = m_VenueBuildingQuery.ToEntityArray(Allocator.Temp);
                transitStops = m_TransitStopQuery.ToComponentDataArray<Game.Objects.Transform>(Allocator.Temp);
                parkingFacilities = m_ParkingFacilityQuery.ToComponentDataArray<Game.Objects.Transform>(Allocator.Temp);
                var newList = new List<VenueRecord>();

                for (int i = 0; i < buildingEntities.Length; i++)
                {
                    Entity bldEnt = buildingEntities[i];
                    if (!EntityManager.Exists(bldEnt)) continue;

                    PrefabRef prefabRef = EntityManager.GetComponentData<PrefabRef>(bldEnt);
                    Entity prefabEnt = prefabRef.m_Prefab;
                    if (prefabEnt == Entity.Null || !EntityManager.Exists(prefabEnt)) continue;

                    bool isCompatible = false;
                    string typeDesc = "Entertainment Venue";
                    int baseCapacity = 10000;

                    if (EntityManager.HasComponent<AttractionData>(prefabEnt))
                    {
                        var attr = EntityManager.GetComponentData<AttractionData>(prefabEnt);
                        isCompatible = true;
                        baseCapacity = math.max(8000, attr.m_Attractiveness * 150);
                        typeDesc = "Stadium / Landmark Arena";
                    }
                    else if (EntityManager.HasComponent<ParkData>(prefabEnt))
                    {
                        isCompatible = true;
                        baseCapacity = 12000;
                        typeDesc = "Open-Air Festival Park & Sports Ground";
                    }
                    else if (EntityManager.HasComponent<LeisureProviderData>(prefabEnt))
                    {
                        var leisure = EntityManager.GetComponentData<LeisureProviderData>(prefabEnt);
                        isCompatible = true;
                        baseCapacity = math.max(5000, leisure.m_Efficiency * 100);
                        typeDesc = "Concert Hall / Theater / Leisure Complex";
                    }

                    if (isCompatible)
                    {
                        var transform = EntityManager.GetComponentData<Game.Objects.Transform>(bldEnt);
                        int occupants = 0;
                        if (EntityManager.HasBuffer<Occupant>(bldEnt))
                        {
                            occupants = EntityManager.GetBuffer<Occupant>(bldEnt).Length;
                        }

                        // Calculate transit and parking nearby
                        int transitStopCount = CountNearby(transform.m_Position, 350f, transitStops);
                        int parkingSpaces = CountNearby(transform.m_Position, 400f, parkingFacilities) * 25;

                        string name = GetBuildingDisplayName(bldEnt, prefabEnt);

                        newList.Add(new VenueRecord
                        {
                            Entity = bldEnt,
                            EntityIndex = bldEnt.Index,
                            Name = name,
                            PrefabName = EntityManager.GetName(prefabEnt) ?? "Venue",
                            TypeDescription = typeDesc,
                            Capacity = baseCapacity,
                            CurrentOccupants = occupants,
                            Position = transform.m_Position,
                            TransitStopsNearby = transitStopCount,
                            ParkingSpacesNearby = parkingSpaces
                        });
                    }
                }

                DiscoveredVenues = newList;

                // Keep selected metadata fresh without resolving recycled entity indices.
                SelectedVenue = SelectedVenue == null ? null : newList.FirstOrDefault(v => v.Entity == SelectedVenue.Entity);
                if (SelectedVenue == null) SelectedVenue = newList.FirstOrDefault();
            }
            catch (Exception ex)
            {
                log.Error(ex, "[EventEngine] Error refreshing discovered venues");
            }
            finally
            {
                if (parkingFacilities.IsCreated) parkingFacilities.Dispose();
                if (transitStops.IsCreated) transitStops.Dispose();
                if (buildingEntities.IsCreated) buildingEntities.Dispose();
            }
        }

        private string GetBuildingDisplayName(Entity bldEnt, Entity prefabEnt)
        {
            string prefabName = EntityManager.GetName(prefabEnt);
            if (!string.IsNullOrEmpty(prefabName))
            {
                return prefabName.Replace("Prefab", "").Replace("_", " ").Trim();
            }
            return $"Venue #{bldEnt.Index}";
        }

        private static int CountNearby(float3 pos, float radius, NativeArray<Game.Objects.Transform> candidates)
        {
            int count = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (math.distancesq(pos, candidates[i].m_Position) <= radius * radius)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Selects a venue by entity index or direct reference.
        /// </summary>
        public void SelectVenue(int entityIndex)
        {
            var match = DiscoveredVenues.FirstOrDefault(v => v.EntityIndex == entityIndex);
            if (match != null)
            {
                SelectedVenue = match;
                ShowToast($"Selected Venue: {SelectedVenue.Name} ({SelectedVenue.Capacity:N0} Cap)");
            }
        }

        public bool SelectVenueByEntity(Entity ent)
        {
            if (ent == Entity.Null || !EntityManager.Exists(ent)) return false;

            var match = DiscoveredVenues.FirstOrDefault(v => v.Entity == ent);
            if (match != null)
            {
                SelectedVenue = match;
                ShowToast($"Selected Venue: {SelectedVenue.Name}");
                return true;
            }

            RefreshDiscoveredVenues();
            match = DiscoveredVenues.FirstOrDefault(v => v.Entity == ent);
            if (match != null)
            {
                SelectedVenue = match;
                ShowToast($"Selected Venue: {SelectedVenue.Name}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates a new scheduled event with realistic spectator configuration.
        /// </summary>
        public EventRecord ScheduleEvent(
            string title,
            EventCategory category,
            int venueEntityIndex,
            int attendance,
            int startHour,
            int durationHours,
            int startMinute = 30,
            float arrivalIntensity = 1.0f,
            float departureIntensity = 1.0f,
            int carUsage = 45,
            int transitUsage = 40,
            int taxiUsage = 15)
        {
            var venue = DiscoveredVenues.FirstOrDefault(v => v.EntityIndex == venueEntityIndex) ?? SelectedVenue;
            if (venue == null || venue.Entity == Entity.Null || !EntityManager.Exists(venue.Entity))
                throw new InvalidOperationException("Select a valid venue before scheduling an event.");

            attendance = math.clamp(attendance, 1, 100000);
            startHour = math.clamp(startHour, 0, 23);
            startMinute = math.clamp(startMinute, 0, 59);
            durationHours = math.clamp(durationHours, 1, 24);
            carUsage = math.clamp(carUsage, 0, 100);
            transitUsage = math.clamp(transitUsage, 0, 100);
            taxiUsage = math.clamp(taxiUsage, 0, 100);
            int modeTotal = carUsage + transitUsage + taxiUsage;
            if (modeTotal <= 0)
            {
                carUsage = 45;
                transitUsage = 40;
                taxiUsage = 15;
            }
            else if (modeTotal != 100)
            {
                carUsage = (int)math.round(carUsage * 100f / modeTotal);
                transitUsage = (int)math.round(transitUsage * 100f / modeTotal);
                taxiUsage = math.max(0, 100 - carUsage - transitUsage);
            }
            Entity venueEnt = venue != null ? venue.Entity : Entity.Null;
            string venueName = venue != null ? venue.Name : "Central Arena";

            DateTime now = GetSimulationDateTime();
            DateTime scheduledStart = EventSchedule.NextStart(now, startHour, startMinute);
            int minutesUntilStart = EventSchedule.MinutesUntil(now, scheduledStart);

            string impact = attendance > 15000 ? "SEVERE" : attendance > 10000 ? "HIGH" : attendance > 5000 ? "MODERATE" : "LOW";

            var ev = new EventRecord
            {
                Id = m_NextEventId++,
                Title = string.IsNullOrWhiteSpace(title) ? $"{category} Match" : title,
                Category = category,
                VenueEntity = venueEnt,
                VenueEntityIndex = venue != null ? venue.EntityIndex : 0,
                VenueName = venueName,
                ExpectedAttendance = attendance,
                CurrentAttendance = 0,
                TravelingCount = 0,
                StartHour = startHour,
                StartMinute = startMinute,
                DurationHours = durationHours,
                MinutesUntilEvent = minutesUntilStart,
                ScheduledStartTicks = scheduledStart.Ticks,
                Phase = EventPhase.Scheduled,
                ArrivalIntensity = arrivalIntensity,
                DepartureIntensity = departureIntensity,
                CarUsagePercent = carUsage,
                TransitUsagePercent = transitUsage,
                TaxiUsagePercent = taxiUsage,
                TrafficImpact = impact
            };

            Events.Add(ev);
            ShowToast($"Scheduled: {ev.Title} at {ev.VenueName} ({ev.ExpectedAttendance:N0} Attendance)");
            log.Info($"[EventEngine] Scheduled event #{ev.Id} '{ev.Title}' at {venueName}");
            return ev;
        }

        public void CancelEvent(int id)
        {
            var match = Events.FirstOrDefault(e => e.Id == id);
            if (match != null)
            {
                CancelPendingArrivalTrips(match);
                ReleaseCitizens(match.Id);
                Events.Remove(match);
                ShowToast($"Cancelled event: {match.Title}");
                log.Info($"[EventEngine] Cancelled event #{id}");
            }
        }

        /// <summary>
        /// Processes active simulation event lifecycles, spectator trip generation, and departure surges.
        /// </summary>
        private void ProcessEventLifecycles()
        {
            if (Events.Count == 0 || !TryGetSimulationDateTime(out DateTime now)) return;
            for (int i = Events.Count - 1; i >= 0; i--)
            {
                var ev = Events[i];
                if (ev.Phase == EventPhase.Completed) continue;
                if (ev.VenueEntity == Entity.Null || !EntityManager.Exists(ev.VenueEntity) || EntityManager.HasComponent<Deleted>(ev.VenueEntity))
                {
                    // An index can be recycled for an unrelated building. Never relink it.
                    CancelEvent(ev.Id);
                    continue;
                }

                if (ev.ScheduledStartTicks <= 0)
                {
                    ev.ScheduledStartTicks = EventSchedule.NextStart(now, ev.StartHour, ev.StartMinute).Ticks;
                }

                DateTime scheduledStart = new DateTime(ev.ScheduledStartTicks, DateTimeKind.Utc);
                ev.MinutesUntilEvent = EventSchedule.MinutesUntil(now, scheduledStart);

                // 1. ARRIVAL PHASE (2 hours before event)
                if (EventSchedule.CanGenerateArrivals(ev.Phase, ev.MinutesUntilEvent))
                {
                    if (ev.Phase != EventPhase.Arrivals)
                    {
                        ev.Phase = EventPhase.Arrivals;
                        ShowToast($"🏟 {ev.Title} begins in {ev.MinutesUntilEvent}m — Spectator arrivals starting");
                    }

                    // Generate toward a cumulative target derived from the actual game clock.
                    // This avoids repeatedly injecting the same percentage every simulation
                    // update and bounds each main-thread batch to prevent frame spikes.
                    int desiredRequests = EventSchedule.ArrivalTarget(ev.ExpectedAttendance, ev.MinutesUntilEvent, ev.ArrivalIntensity);
                    int alreadyRequested = m_DispatchedCitizens.TryGetValue(ev.Id, out var requested)
                        ? requested.Count
                        : 0;
                    int batchTarget = math.min(512, math.max(0, desiredRequests - alreadyRequested));
                    if (batchTarget > 0)
                        DispatchSpectatorArrivalTrips(ev, batchTarget);
                }
                // 2. LIVE PHASE (Event underway)
                else if (now >= scheduledStart && ev.Phase != EventPhase.Live && ev.Phase != EventPhase.Departing && ev.Phase != EventPhase.Completed)
                {
                    ev.Phase = EventPhase.Live;
                    ShowToast($"🔴 LIVE: {ev.Title} is now underway at {ev.VenueName}!");
                }
                if (ev.Phase == EventPhase.Live)
                {
                    UpdateMeasuredAttendance(ev);
                    if (now >= scheduledStart.AddHours(ev.DurationHours))
                        DispatchPostEventDepartures(ev);
                }

                if (ev.Phase != EventPhase.Completed) UpdateMeasuredAttendance(ev);
                // This is a planning estimate, not measured travel-mode telemetry.
                UpdateEventTransportationTelemetry(ev);
            }
        }

        /// <summary>
        /// Dispatches authentic CS2 citizen trips toward the venue using native TripNeeded buffer elements.
        /// </summary>
        private void DispatchSpectatorArrivalTrips(EventRecord ev, int count)
        {
            if (ev.VenueEntity == Entity.Null || !EntityManager.Exists(ev.VenueEntity) || count <= 0) return;

            NativeArray<Entity> citizens = default;
            try
            {
                citizens = m_CitizenQuery.ToEntityArray(Allocator.Temp);
                int dispatched = 0;
                if (!m_DispatchedCitizens.TryGetValue(ev.Id, out var eventCitizens))
                {
                    eventCitizens = new HashSet<Entity>();
                    m_DispatchedCitizens[ev.Id] = eventCitizens;
                }
                count = math.min(count, math.max(0, ev.ExpectedAttendance - eventCitizens.Count));
                int maxScan = math.min(citizens.Length, 8192);
                m_ArrivalScanCursors.TryGetValue(ev.Id, out int cursor);
                int scanned = 0;

                for (int i = 0; i < maxScan && dispatched < count; i++)
                {
                    Entity citEnt = citizens[(cursor + i) % citizens.Length];
                    scanned++;
                    if (!EntityManager.Exists(citEnt)) continue;
                    if (m_AssignedCitizens.Contains(citEnt)) continue;
                    if (EntityManager.HasComponent<TravelPurpose>(citEnt) || EntityManager.HasComponent<CurrentTransport>(citEnt)) continue;
                    if (EntityManager.GetComponentData<CurrentBuilding>(citEnt).m_CurrentBuilding == ev.VenueEntity) continue;

                    // If citizen is currently idle at home/work, dispatch trip to venue
                    DynamicBuffer<TripNeeded> tripBuffer;
                    if (EntityManager.HasBuffer<TripNeeded>(citEnt))
                    {
                        tripBuffer = EntityManager.GetBuffer<TripNeeded>(citEnt);
                    }
                    else
                    {
                        tripBuffer = EntityManager.AddBuffer<TripNeeded>(citEnt);
                    }

                    if (tripBuffer.Length == 0)
                    {
                        tripBuffer.Add(new TripNeeded
                        {
                            m_TargetAgent = ev.VenueEntity,
                            m_Purpose = Purpose.VisitAttractions,
                            m_Data = ev.Id,
                            m_Priority = 100
                        });

                        dispatched++;
                        eventCitizens.Add(citEnt);
                        m_AssignedCitizens.Add(citEnt);
                        ev.TravelingCount = eventCitizens.Count;
                    }
                }
                m_ArrivalScanCursors[ev.Id] = citizens.Length == 0 ? 0 : (cursor + scanned) % citizens.Length;

                if (dispatched > 0)
                    log.Info($"[EventEngine] Native arrival requests event=#{ev.Id} dispatched={dispatched} total={ev.TravelingCount} venue=#{ev.VenueEntity.Index}:{ev.VenueEntity.Version}");
            }
            catch (Exception ex)
            {
                log.Error(ex, $"[EventEngine] DispatchSpectatorArrivalTrips error for event #{ev.Id}");
            }
            finally
            {
                if (citizens.IsCreated) citizens.Dispose();
            }
        }

        /// <summary>
        /// Dispatches authentic post-event departure trips sending spectators home from the venue.
        /// </summary>
        public void DispatchPostEventDepartures(EventRecord ev)
        {
            if (ev == null || ev.Phase == EventPhase.Completed || ev.VenueEntity == Entity.Null || !EntityManager.Exists(ev.VenueEntity)) return;

            try
            {
                ev.Phase = EventPhase.Departing;
                CancelPendingArrivalTrips(ev);
                ShowToast($"🏁 {ev.Title} has concluded — Post-event spectator departures underway");

                var departureCitizens = new HashSet<Entity>();
                if (m_DispatchedCitizens.TryGetValue(ev.Id, out var dispatchedCitizens))
                {
                    foreach (var citizen in dispatchedCitizens)
                    {
                        if (citizen != Entity.Null && EntityManager.Exists(citizen) &&
                            EntityManager.HasComponent<CurrentBuilding>(citizen) &&
                            EntityManager.GetComponentData<CurrentBuilding>(citizen).m_CurrentBuilding == ev.VenueEntity)
                            departureCitizens.Add(citizen);
                    }
                }

                var departures = 0;
                foreach (Entity occCit in departureCitizens)
                {
                            // Dispatch citizen back home
                            Entity homeEnt = Entity.Null;
                            if (EntityManager.HasComponent<HouseholdMember>(occCit))
                            {
                                Entity hh = EntityManager.GetComponentData<HouseholdMember>(occCit).m_Household;
                                if (EntityManager.Exists(hh) && EntityManager.HasComponent<PropertyRenter>(hh))
                                {
                                    homeEnt = EntityManager.GetComponentData<PropertyRenter>(hh).m_Property;
                                }
                            }

                            if (homeEnt == Entity.Null || !EntityManager.Exists(homeEnt)) continue;

                            DynamicBuffer<TripNeeded> tripBuffer;
                            if (EntityManager.HasBuffer<TripNeeded>(occCit))
                            {
                                tripBuffer = EntityManager.GetBuffer<TripNeeded>(occCit);
                            }
                            else
                            {
                                tripBuffer = EntityManager.AddBuffer<TripNeeded>(occCit);
                            }

                            // Never overwrite or stack on top of a vanilla trip the citizen
                            // is already trying to complete.
                            if (tripBuffer.Length != 0) continue;

                            tripBuffer.Add(new TripNeeded
                            {
                                m_TargetAgent = homeEnt != Entity.Null ? homeEnt : Entity.Null,
                                m_Purpose = Purpose.GoingHome,
                                m_Data = 0,
                                m_Priority = 100
                            });
                            departures++;
                }

                ev.Phase = EventPhase.Completed;
                ReleaseCitizens(ev.Id);
                ev.TravelingCount = 0;
                log.Info($"[EventEngine] Native departure requests event=#{ev.Id} dispatched={departures} venue=#{ev.VenueEntity.Index}:{ev.VenueEntity.Version}");
            }
            catch (Exception ex)
            {
                log.Error(ex, $"[EventEngine] DispatchPostEventDepartures error for event #{ev.Id}");
            }
        }

        private DateTime GetSimulationDateTime()
        {
            if (TryGetSimulationDateTime(out DateTime now)) return now;
            throw new InvalidOperationException("The city clock is unavailable. Load a city before scheduling an event.");
        }

        private bool TryGetSimulationDateTime(out DateTime now)
        {
            now = default;
            try
            {
                if (m_TimeSystem != null)
                {
                    now = EventSchedule.SimulationClock(m_TimeSystem.year, m_TimeSystem.daysPerYear,
                        m_TimeSystem.normalizedDate, m_TimeSystem.normalizedTime);
                    m_ClockWarningShown = false;
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (!m_ClockWarningShown) log.Warn($"[EventEngine] Scheduling paused; could not read simulation clock: {ex.Message}");
                m_ClockWarningShown = true;
            }

            return false;
        }

        private void UpdateMeasuredAttendance(EventRecord ev)
        {
            ev.CurrentAttendance = 0;
            if (!m_DispatchedCitizens.TryGetValue(ev.Id, out var dispatched))
            {
                ev.TravelingCount = 0;
                return;
            }

            if (ev.VenueEntity != Entity.Null && EntityManager.Exists(ev.VenueEntity) &&
                EntityManager.HasBuffer<Occupant>(ev.VenueEntity))
            {
                DynamicBuffer<Occupant> occupants = EntityManager.GetBuffer<Occupant>(ev.VenueEntity);
                for (int i = 0; i < occupants.Length; i++)
                {
                    if (dispatched.Contains(occupants[i].m_Occupant))
                        ev.CurrentAttendance++;
                }
            }

            ev.TravelingCount = math.max(0, dispatched.Count - ev.CurrentAttendance);
        }

        private void CancelPendingArrivalTrips(EventRecord ev)
        {
            if (ev == null || !m_DispatchedCitizens.TryGetValue(ev.Id, out var dispatched))
                return;

            try
            {
                foreach (Entity citizen in dispatched)
                {
                    if (citizen == Entity.Null || !EntityManager.Exists(citizen) ||
                        !EntityManager.HasBuffer<TripNeeded>(citizen))
                        continue;

                    DynamicBuffer<TripNeeded> trips = EntityManager.GetBuffer<TripNeeded>(citizen);
                    for (int i = trips.Length - 1; i >= 0; i--)
                    {
                        TripNeeded trip = trips[i];
                        if (trip.m_Data == ev.Id && trip.m_TargetAgent == ev.VenueEntity &&
                            trip.m_Purpose == Purpose.VisitAttractions)
                        {
                            trips.RemoveAt(i);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn($"[EventEngine] Pending arrival cleanup was incomplete for event #{ev.Id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Projects a planning split from outstanding requests; not native mode telemetry.
        /// </summary>
        private void UpdateEventTransportationTelemetry(EventRecord ev)
        {
            if (ev.TravelingCount <= 0)
            {
                // Never manufacture convincing-looking telemetry from expected attendance.
                // Until native trip requests are actually issued, every live counter stays zero.
                ev.CarsCount = 0;
                ev.TransitCount = 0;
                ev.TaxiCount = 0;
                ev.WalkingCount = 0;
            }
            else
            {
                ev.CarsCount = (int)(ev.TravelingCount * (ev.CarUsagePercent / 100f));
                ev.TransitCount = (int)(ev.TravelingCount * (ev.TransitUsagePercent / 100f));
                ev.TaxiCount = (int)(ev.TravelingCount * (ev.TaxiUsagePercent / 100f));
                ev.WalkingCount = math.max(0, ev.TravelingCount - (ev.CarsCount + ev.TransitCount + ev.TaxiCount));
            }
        }

        // ==========================================
        // QUICK TEST / DEVELOPER ACTIONS
        // ==========================================

        public void QuickTestStartNow(int eventId)
        {
            var ev = Events.FirstOrDefault(e => e.Id == eventId) ?? (Events.Count > 0 ? Events[0] : null);
            if (ev != null)
            {
                ev.MinutesUntilEvent = 0;
                ev.ScheduledStartTicks = GetSimulationDateTime().Ticks;
                ev.Phase = EventPhase.Live;
                DispatchSpectatorArrivalTrips(ev, math.min(ev.ExpectedAttendance, 2500));
                ShowToast($"[Test Mode] Forced Event LIVE: {ev.Title}");
            }
        }

        public void QuickTestSimulateAttendance(int eventId, int attendance)
        {
            attendance = math.clamp(attendance, 1, 100000);
            var ev = Events.FirstOrDefault(e => e.Id == eventId) ?? (Events.Count > 0 ? Events[0] : null);
            if (ev == null)
            {
                ev = ScheduleEvent("Championship Derby (Test)", EventCategory.FootballMatch, SelectedVenue?.EntityIndex ?? 0, attendance, 20, 3);
            }

            ev.ExpectedAttendance = attendance;
            ev.MinutesUntilEvent = 15;
            ev.ScheduledStartTicks = GetSimulationDateTime().AddMinutes(15).Ticks;
            ev.Phase = EventPhase.Arrivals;
            DispatchSpectatorArrivalTrips(ev, math.min(attendance, 3500));
            ShowToast($"[Test Mode] Requested up to {math.min(attendance, 3500):N0} spectator trips toward {ev.VenueName}");
        }

        public void QuickTestEndEvent(int eventId)
        {
            var ev = Events.FirstOrDefault(e => e.Id == eventId) ?? (Events.Count > 0 ? Events[0] : null);
            if (ev != null)
            {
                DispatchPostEventDepartures(ev);
                ShowToast($"[Test Mode] Ended Event & Dispatched Departures: {ev.Title}");
            }
        }

        public void QuickTestResetAll()
        {
            for (int i = 0; i < Events.Count; i++)
                CancelPendingArrivalTrips(Events[i]);
            Events.Clear();
            m_DispatchedCitizens.Clear();
            m_AssignedCitizens.Clear();
            m_ArrivalScanCursors.Clear();
            ShowToast("[Test Mode] Reset all scheduled and active events");
        }

        public void ShowToast(string message)
        {
            LastToastMessage = message;
            log.Info($"[EventEngine Toast] {message}");
        }

        protected override void OnDestroy()
        {
            for (int i = 0; i < Events.Count; i++)
                CancelPendingArrivalTrips(Events[i]);
            Events.Clear();
            m_DispatchedCitizens.Clear();
            m_AssignedCitizens.Clear();
            m_ArrivalScanCursors.Clear();
            Instance = null;
            base.OnDestroy();
        }

        private void ReleaseCitizens(int eventId)
        {
            if (m_DispatchedCitizens.TryGetValue(eventId, out var citizens))
                foreach (var citizen in citizens) m_AssignedCitizens.Remove(citizen);
            m_DispatchedCitizens.Remove(eventId);
            m_ArrivalScanCursors.Remove(eventId);
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            for (int i = 0; i < Events.Count; i++) CancelPendingArrivalTrips(Events[i]);
            Events.Clear();
            DiscoveredVenues.Clear();
            SelectedVenue = null;
            m_DispatchedCitizens.Clear();
            m_AssignedCitizens.Clear();
            m_ArrivalScanCursors.Clear();
            IsVenuePickingActive = false;
            LastToastMessage = null;
            m_SimulationTickCounter = 0;
            base.OnGamePreload(purpose, mode);
        }
    }
}
