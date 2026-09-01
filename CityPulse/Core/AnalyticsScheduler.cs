using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Routes;
using Game.Tools;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Colossal.UtilitySuite.Interop;

namespace CityPulse
{
    public partial class AnalyticsScheduler : GameSystemBase
    {
        public static ILog Log = LogManager.GetLogger(nameof(CityPulse)).SetShowsErrorsInUI(false);
        public static AnalyticsScheduler Instance { get; private set; }

        private ToolSystem m_ToolSystem;

        // Central Cached Queries
        private EntityQuery m_VehicleQuery;
        private EntityQuery m_BottleneckQuery;

        private EntityQuery m_RouteQuery;
        private EntityQuery m_PublicVehicleQuery;
        private EntityQuery m_TransitStopQuery;

        private EntityQuery m_ParkingFacilityQuery;

        private EntityQuery m_ServiceBuildingQuery;
        private EntityQuery m_BuildingQuery;

        private EntityQuery m_LaneQuery;

        // Diagnostic Sub-Modules State
        public MasterCityOverviewData OverviewData { get; private set; }
        public List<UnifiedCityAlert> TopCityAlerts { get; private set; } = new List<UnifiedCityAlert>();

        public TrafficSummaryData TrafficSummary { get; private set; }
        public List<TrafficBottleneckData> TrafficBottlenecks { get; private set; } = new List<TrafficBottleneckData>();

        public TransitSummaryData TransitSummary { get; private set; }
        public List<TransitLineEntry> TransitLines { get; private set; } = new List<TransitLineEntry>();
        public List<TransitStopEntry> TransitStops { get; private set; } = new List<TransitStopEntry>();

        public ParkingSummaryData ParkingSummary { get; private set; }
        public List<ParkingFacilityEntry> ParkingFacilities { get; private set; } = new List<ParkingFacilityEntry>();

        public ServiceSummaryData ServiceSummary { get; private set; }
        public List<ServiceFacilityEntry> ServiceFacilities { get; private set; } = new List<ServiceFacilityEntry>();

        public BuildingSummaryData BuildingSummary { get; private set; }
        public List<BuildingIssueEntry> BuildingIssues { get; private set; } = new List<BuildingIssueEntry>();
        public SelectedBuildingDetail SelectedBuilding { get; private set; }

        public NetworkSummaryData NetworkSummary { get; private set; }
        public List<NetworkDefectEntry> NetworkDefects { get; private set; } = new List<NetworkDefectEntry>();

        // Timeline, Districts, and Before/After Baselines
        public List<CityTimelinePoint> CityTimeline { get; private set; } = new List<CityTimelinePoint>();
        public List<DistrictMetricEntry> DistrictMetrics { get; private set; } = new List<DistrictMetricEntry>();
        private readonly Dictionary<int, BeforeAfterSnapshot> m_Baselines = new Dictionary<int, BeforeAfterSnapshot>();

        // Execution Timers & Staggered Cadence
        private int m_FrameCounter = 0;
        private int m_BuildingScanCursor = 0;
        private const int kBuildingBatchSize = 2048;
        private readonly List<BuildingIssueEntry> m_BuildingScanIssues = new List<BuildingIssueEntry>(20);
        private int m_BuildingScanTroubled;
        private int m_BuildingScanWorkerShortage;
        private int m_BuildingScanLowEfficiency;
        private int m_BuildingScanAbandoned;

        public event Action OnDataUpdated;

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            Log.Info("City Pulse v3.0.4-beta.1 analytics scheduler initialized.");

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            // 1. Traffic Queries
            m_VehicleQuery = GetEntityQuery(
                ComponentType.ReadOnly<Vehicle>(),
                ComponentType.ReadOnly<CarCurrentLane>(),
                ComponentType.ReadOnly<Moving>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());
            m_BottleneckQuery = GetEntityQuery(
                ComponentType.ReadOnly<Bottleneck>(),
                ComponentType.ReadOnly<Curve>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // 2. Transit Queries
            m_RouteQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Route>(), ComponentType.ReadOnly<TransportLine>(), ComponentType.ReadOnly<RouteWaypoint>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
            m_PublicVehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Game.Vehicles.PublicTransport>(), ComponentType.ReadOnly<CurrentRoute>(), ComponentType.ReadOnly<Transform>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
            m_TransitStopQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Waypoint>(), ComponentType.ReadOnly<WaitingPassengers>(), ComponentType.ReadOnly<Transform>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            // 3. Parking Queries
            m_ParkingFacilityQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Game.Buildings.ParkingFacility>(), ComponentType.ReadOnly<Transform>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
            // 4. Services Query
            m_ServiceBuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<Transform>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<Game.Buildings.Hospital>(),
                    ComponentType.ReadOnly<Game.Buildings.PoliceStation>(),
                    ComponentType.ReadOnly<Game.Buildings.FireStation>(),
                    ComponentType.ReadOnly<Game.Buildings.GarbageFacility>(),
                    ComponentType.ReadOnly<Game.Buildings.DeathcareFacility>(),
                    ComponentType.ReadOnly<Game.Buildings.PostFacility>(),
                    ComponentType.ReadOnly<Game.Buildings.MaintenanceDepot>(),
                    ComponentType.ReadOnly<Game.Buildings.School>()
                },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            // 5. Buildings Query
            m_BuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<Transform>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            // 6. Network Queries
            m_LaneQuery = GetEntityQuery(
                ComponentType.ReadOnly<Lane>(),
                ComponentType.ReadOnly<Curve>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            ResetAllData();
        }

        protected override void OnDestroy()
        {
            Instance = null;
            ResetAllData();
            base.OnDestroy();
        }

        public void ResetAllData()
        {
            CityTimeline.Clear();
            DistrictMetrics.Clear();
            m_Baselines.Clear();
            TopCityAlerts.Clear();
            TrafficBottlenecks.Clear();
            TransitLines.Clear();
            TransitStops.Clear();
            ParkingFacilities.Clear();
            ServiceFacilities.Clear();
            BuildingIssues.Clear();
            NetworkDefects.Clear();

            OverviewData = default;
            TrafficSummary = default;
            TransitSummary = default;
            ParkingSummary = default;
            ServiceSummary = default;
            BuildingSummary = default;
            SelectedBuilding = default;
            NetworkSummary = default;

            m_FrameCounter = 0;
            m_BuildingScanCursor = 0;
            m_BuildingScanIssues.Clear();
            m_BuildingScanTroubled = 0;
            m_BuildingScanWorkerShortage = 0;
            m_BuildingScanLowEfficiency = 0;
            m_BuildingScanAbandoned = 0;

            OnDataUpdated?.Invoke();
        }

        protected override void OnUpdate()
        {
            if (World == null || !World.IsCreated) return;

            m_FrameCounter++;

            // Staggered bounded execution across frames to preserve 60+ FPS
            if (m_FrameCounter % 30 == 0) // Twice per second: Traffic & Transit
            {
                UpdateTrafficDomain();
                UpdateTransitDomain();
            }

            if (m_FrameCounter % 60 == 0) // Once per second: Parking, Services, Network, Master Consolidation
            {
                UpdateParkingDomain();
                UpdateServicesDomain();
                UpdateBuildingDomainIncremental();
                UpdateNetworkDomain();
                ConsolidateMasterPulse();
            }

            if (m_FrameCounter % 300 == 0) // Every 5 seconds: Timeline History Sample
            {
                SampleTimelinePoint();
            }

            // Always update selected building context
            InspectSelectedBuilding();
        }

        // ==========================================
        // 1. TRAFFIC DIAGNOSTICS DOMAIN
        // ==========================================
        private void UpdateTrafficDomain()
        {
            try
            {
                int activeVehicles = m_VehicleQuery.CalculateEntityCount();
                int bottleneckEntities = m_BottleneckQuery.CalculateEntityCount();

                var list = new List<TrafficBottleneckData>();
                float totalSpeedSum = 0f;
                int speedSamples = 0;
                int stoppedSamples = 0;

                // Use actual vehicle velocity. Bound the sample so opening City Pulse does not
                // turn into a full-city main-thread scan on very large saves.
                if (!m_VehicleQuery.IsEmptyIgnoreFilter)
                {
                    using (var moving = m_VehicleQuery.ToComponentDataArray<Moving>(Allocator.TempJob))
                    {
                        int sampleCount = math.min(moving.Length, 4096);
                        for (int i = 0; i < sampleCount; i++)
                        {
                            float speedKph = math.length(moving[i].m_Velocity) * 3.6f;
                            totalSpeedSum += speedKph;
                            if (speedKph < 1f) stoppedSamples++;
                        }
                        speedSamples = sampleCount;
                    }
                }

                float avgCitySpeed = speedSamples > 0 ? totalSpeedSum / speedSamples : 0f;
                float stoppedPercent = speedSamples > 0 ? stoppedSamples * 100f / speedSamples : 0f;

                if (!m_BottleneckQuery.IsEmptyIgnoreFilter)
                {
                    var bnEntities = m_BottleneckQuery.ToEntityArray(Allocator.TempJob);
                    try
                    {
                        for (int i = 0; i < bnEntities.Length; i++)
                        {
                            var bEnt = bnEntities[i];
                            var bottleneck = EntityManager.GetComponentData<Bottleneck>(bEnt);
                            var curve = EntityManager.GetComponentData<Curve>(bEnt);
                            float t = bottleneck.m_Position / 255f;
                            var pos = MathUtils.Position(curve.m_Bezier, t);
                            float persistence = math.saturate(bottleneck.m_Timer / 40f) * 100f;
                            string sev = bottleneck.m_Timer >= 20 ? "Persistent" : "Detected";

                            if (list.Count < 20)
                            {
                                list.Add(new TrafficBottleneckData
                                {
                                    Id = i + 1,
                                    EntityIndex = bEnt.Index,
                                    LocationName = $"Traffic Node #{bEnt.Index}",
                                    Severity = sev,
                                    MeasuredSpeedKph = (float)Math.Round(avgCitySpeed, 1),
                                    SpeedLimitKph = 0f,
                                    StoppedRatioPercent = (float)Math.Round(stoppedPercent, 1),
                                    VehicleCount = 0,
                                    QueuePressurePercent = (float)Math.Round(persistence, 1),
                                    InferredCause = "Vanilla reported a traffic bottleneck on this lane. City Pulse does not infer a cause from that marker alone.",
                                    Confidence = "VANILLA SIGNAL",
                                    FlowPropagation = $"Marker range {bottleneck.m_MinPos}/255–{bottleneck.m_MaxPos}/255; timer {bottleneck.m_Timer}/40",
                                    Position = pos
                                });
                            }
                        }
                    }
                    finally
                    {
                        bnEntities.Dispose();
                    }
                }

                float congestionIdx = (float)Math.Round(stoppedPercent, 1);
                int trafficHealth = math.clamp(100 - (int)Math.Round(stoppedPercent * 0.5f) - math.min(40, bottleneckEntities * 2), 0, 100);

                TrafficBottlenecks = list;
                TrafficSummary = new TrafficSummaryData
                {
                    CityHealthScore = trafficHealth,
                    ActiveVehicles = activeVehicles,
                    AverageCitySpeedKph = (float)Math.Round(avgCitySpeed, 1),
                    CongestionIndex = congestionIdx,
                    BottleneckCount = bottleneckEntities
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in Traffic Domain update");
            }
        }

        // ==========================================
        // 2. TRANSIT DIAGNOSTICS DOMAIN
        // ==========================================
        private void UpdateTransitDomain()
        {
            try
            {
                if (m_RouteQuery.IsEmptyIgnoreFilter)
                {
                    TransitLines.Clear();
                    TransitStops.Clear();
                    TransitSummary = default;
                    return;
                }

                var routeEntities = m_RouteQuery.ToEntityArray(Allocator.TempJob);
                var vehicleEntities = m_PublicVehicleQuery.ToEntityArray(Allocator.TempJob);
                var stopEntities = m_TransitStopQuery.ToEntityArray(Allocator.TempJob);

                var newLines = new List<TransitLineEntry>(routeEntities.Length);
                var newStops = new List<TransitStopEntry>(stopEntities.Length);

                int totalVehicles = 0;
                int totalPax = 0;
                int totalWaiting = 0;
                float sumUtil = 0f;
                int overcrowded = 0;
                int bunchingCount = 0;

                try
                {
                    var vehiclesByRoute = new Dictionary<Entity, List<Entity>>();
                    for (int i = 0; i < vehicleEntities.Length; i++)
                    {
                        var vEnt = vehicleEntities[i];
                        if (EntityManager.HasComponent<CurrentRoute>(vEnt))
                        {
                            var cr = EntityManager.GetComponentData<CurrentRoute>(vEnt);
                            if (cr.m_Route != Entity.Null)
                            {
                                if (!vehiclesByRoute.TryGetValue(cr.m_Route, out var list))
                                {
                                    list = new List<Entity>();
                                    vehiclesByRoute[cr.m_Route] = list;
                                }
                                list.Add(vEnt);
                            }
                        }
                    }

                    for (int i = 0; i < routeEntities.Length; i++)
                    {
                        var rEnt = routeEntities[i];
                        if (!EntityManager.Exists(rEnt) || EntityManager.HasComponent<Deleted>(rEnt)) continue;
                        if (!EntityManager.HasComponent<TransportLine>(rEnt) || !EntityManager.HasBuffer<RouteWaypoint>(rEnt)) continue;

                        var tLine = EntityManager.GetComponentData<TransportLine>(rEnt);
                        var waypoints = EntityManager.GetBuffer<RouteWaypoint>(rEnt);

                        int stopCount = waypoints.Length;
                        int linePax = 0;
                        int lineWaiting = 0;
                        int lineCap = 0;
                        string worstStop = "None";
                        int maxStopWaiting = 0;
                        float3 worstStopPos = float3.zero;

                        for (int w = 0; w < waypoints.Length; w++)
                        {
                            var wpEnt = waypoints[w].m_Waypoint;
                            if (EntityManager.HasComponent<WaitingPassengers>(wpEnt))
                            {
                                var wp = EntityManager.GetComponentData<WaitingPassengers>(wpEnt);
                                lineWaiting += wp.m_Count;
                                if (wp.m_Count > maxStopWaiting)
                                {
                                    maxStopWaiting = wp.m_Count;
                                    worstStop = $"Stop #{w + 1}";
                                    if (EntityManager.HasComponent<Transform>(wpEnt)) worstStopPos = EntityManager.GetComponentData<Transform>(wpEnt).m_Position;
                                }
                            }
                        }

                        int lineVehicles = 0;
                        var vPositions = new List<float3>();
                        float lineSpeedSum = 0f;
                        int lineSpeedSamples = 0;
                        if (vehiclesByRoute.TryGetValue(rEnt, out var vList))
                        {
                            lineVehicles = vList.Count;
                            totalVehicles += lineVehicles;
                            for (int v = 0; v < vList.Count; v++)
                            {
                                var vEnt = vList[v];
                                int vehiclePassengers = EntityManager.HasBuffer<Passenger>(vEnt)
                                    ? EntityManager.GetBuffer<Passenger>(vEnt).Length
                                    : 0;
                                linePax += vehiclePassengers;

                                int vehicleCapacity = vehiclePassengers;
                                if (EntityManager.HasComponent<PrefabRef>(vEnt))
                                {
                                    var prefab = EntityManager.GetComponentData<PrefabRef>(vEnt).m_Prefab;
                                    if (EntityManager.HasComponent<PublicTransportVehicleData>(prefab))
                                    {
                                        vehicleCapacity = math.max(vehiclePassengers,
                                            EntityManager.GetComponentData<PublicTransportVehicleData>(prefab).m_PassengerCapacity);
                                    }
                                }
                                lineCap += vehicleCapacity;

                                if (EntityManager.HasComponent<Moving>(vEnt))
                                {
                                    lineSpeedSum += math.length(EntityManager.GetComponentData<Moving>(vEnt).m_Velocity) * 3.6f;
                                    lineSpeedSamples++;
                                }
                                if (EntityManager.HasComponent<Transform>(vEnt)) vPositions.Add(EntityManager.GetComponentData<Transform>(vEnt).m_Position);
                            }
                        }

                        totalPax += linePax;
                        totalWaiting += lineWaiting;

                        float util = lineCap > 0 ? ((float)linePax / lineCap) * 100f : 0f;
                        sumUtil += util;

                        bool bunching = false;
                        float bunchingDev = 0f;
                        if (vPositions.Count >= 2)
                        {
                            float minDist = float.MaxValue;
                            for (int a = 0; a < vPositions.Count; a++)
                                for (int b = a + 1; b < vPositions.Count; b++)
                                {
                                    float d = math.distance(vPositions[a], vPositions[b]);
                                    if (d < minDist) minDist = d;
                                }
                            if (minDist < 60f && lineVehicles >= 3)
                            {
                                bunching = true;
                                bunchingDev = math.clamp((60f - minDist) / 60f * 100f, 10f, 95f);
                                bunchingCount++;
                            }
                        }

                        string category = "Observed";
                        if (util >= 85f || lineWaiting > 120) { category = "Overcrowded"; overcrowded++; }
                        else if (bunching) { category = "Close Spacing"; }
                        else if (util > 50f || lineWaiting > 40) { category = "Watch"; }

                        string measured = $"Capacity: {lineCap} | Riders: {linePax} | Queued: {lineWaiting} | Util: {util:F0}%";
                        string inferred = bunching
                            ? "Two or more vehicles on this route are within 60 m in world space. This can indicate bunching, but route progress was not compared."
                            : "City Pulse reports measured riders, capacity and waiting passengers; it does not infer the underlying cause.";

                        newLines.Add(new TransitLineEntry
                        {
                            EntityIndex = rEnt.Index,
                            Name = $"Transit Line {i + 1}",
                            Category = category,
                            StopCount = stopCount,
                            VehicleCount = lineVehicles,
                            Passengers = linePax,
                            Waiting = lineWaiting,
                            Capacity = lineCap,
                            UtilizationPercent = (float)Math.Round(util, 1),
                            AvgSpeedKph = lineSpeedSamples > 0 ? (float)Math.Round(lineSpeedSum / lineSpeedSamples, 1) : 0f,
                            HeadwayMinutes = 0f,
                            BunchingDetected = bunching,
                            BunchingDeviation = (float)Math.Round(bunchingDev, 1),
                            WorstStopName = worstStop,
                            WorstStopPos = worstStopPos,
                            MeasuredEvidence = measured,
                            InferredCause = inferred,
                            Confidence = "MEASURED / HEURISTIC SPACING"
                        });
                    }

                    for (int s = 0; s < stopEntities.Length; s++)
                    {
                        var sEnt = stopEntities[s];
                        var wp = EntityManager.GetComponentData<WaitingPassengers>(sEnt);
                        if (wp.m_Count > 0)
                        {
                            float3 pos = EntityManager.HasComponent<Transform>(sEnt) ? EntityManager.GetComponentData<Transform>(sEnt).m_Position : float3.zero;
                            newStops.Add(new TransitStopEntry
                            {
                                EntityIndex = sEnt.Index,
                                Name = $"Transit Stop #{sEnt.Index}",
                                WaitingCount = wp.m_Count,
                                LineName = "Transit Line",
                                Position = pos
                            });
                        }
                    }

                    newStops.Sort((a, b) => b.WaitingCount.CompareTo(a.WaitingCount));
                    if (newStops.Count > 15) newStops.RemoveRange(15, newStops.Count - 15);

                    int lineCount = newLines.Count;
                    int transitHealth = lineCount > 0 ? math.clamp(100 - (overcrowded * 15) - (bunchingCount * 8), 20, 100) : 100;

                    TransitLines = newLines;
                    TransitStops = newStops;
                    TransitSummary = new TransitSummaryData
                    {
                        TotalLines = lineCount,
                        ActiveVehicles = totalVehicles,
                        TotalPassengers = totalPax,
                        TotalWaiting = totalWaiting,
                        AvgUtilization = lineCount > 0 ? (float)Math.Round(sumUtil / lineCount, 1) : 0f,
                        OvercrowdedCount = overcrowded,
                        BunchingCount = bunchingCount,
                        HealthScore = transitHealth
                    };
                }
                finally
                {
                    routeEntities.Dispose();
                    vehicleEntities.Dispose();
                    stopEntities.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in Transit Domain update");
            }
        }

        // ==========================================
        // 3. PARKING DIAGNOSTICS DOMAIN
        // ==========================================
        private void UpdateParkingDomain()
        {
            try
            {
                if (m_ParkingFacilityQuery.IsEmptyIgnoreFilter)
                {
                    ParkingFacilities.Clear();
                    ParkingSummary = default;
                    return;
                }

                var facilityEntities = m_ParkingFacilityQuery.ToEntityArray(Allocator.TempJob);
                try
                {
                    int totalSpaces = 0;
                    int parkedInFacs = 0;
                    int fullCount = 0;
                    int underutilized = 0;
                    var facList = new List<ParkingFacilityEntry>(facilityEntities.Length);

                    for (int i = 0; i < facilityEntities.Length; i++)
                    {
                        var fEnt = facilityEntities[i];
                        if (!EntityManager.Exists(fEnt) || EntityManager.HasComponent<Deleted>(fEnt)) continue;
                        if (!EntityManager.HasComponent<Game.Buildings.ParkingFacility>(fEnt)) continue;

                        var pos = EntityManager.HasComponent<Transform>(fEnt) ? EntityManager.GetComponentData<Transform>(fEnt).m_Position : float3.zero;

                        int laneCount = 0;
                        int cap = 0;
                        int parked = 0;
                        int parkingFee = 0;
                        VehicleUtils.GetParkingData(this, fEnt, ref laneCount, ref cap, ref parked, ref parkingFee);
                        cap = math.max(0, cap);
                        float util = cap > 0 ? (float)parked / cap * 100f : 0f;

                        totalSpaces += cap;
                        parkedInFacs += parked;

                        string status = "Normal";
                        if (util >= 90f) { status = "Overcapacity"; fullCount++; }
                        else if (util >= 70f) { status = "High"; }
                        else if (util < 25f) { status = "Surplus"; underutilized++; }

                        string measured = $"Capacity: {cap} | Parked: {parked} | Util: {util:F0}%";
                        string inferred = "Capacity and parked-vehicle counts come from CS2's parking data. City Pulse does not infer wider traffic effects.";

                        facList.Add(new ParkingFacilityEntry
                        {
                            EntityIndex = fEnt.Index,
                            Name = $"Parking Facility #{fEnt.Index}",
                            ParkedCars = parked,
                            Capacity = cap,
                            UtilizationPercent = (float)Math.Round(util, 1),
                            Status = status,
                            MeasuredBreakdown = measured,
                            InferredCause = inferred,
                            Confidence = "MEASURED",
                            Position = pos
                        });
                    }

                    facList.Sort((a, b) => b.UtilizationPercent.CompareTo(a.UtilizationPercent));
                    float netUtil = totalSpaces > 0 ? ((float)parkedInFacs / totalSpaces) * 100f : 0f;
                    int pHealth = math.clamp(100 - (fullCount * 12) - (underutilized * 3), 20, 100);

                    ParkingFacilities = facList;
                    ParkingSummary = new ParkingSummaryData
                    {
                        TotalOffStreetSpaces = totalSpaces,
                        TotalParkedCars = parkedInFacs,
                        NetworkUtilizationPercent = (float)Math.Round(netUtil, 1),
                        FullFacilitiesCount = fullCount,
                        UnderutilizedCount = underutilized,
                        TotalFacilities = facList.Count,
                        HealthScore = pHealth
                    };
                }
                finally
                {
                    facilityEntities.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in Parking Domain update");
            }
        }

        // ==========================================
        // 4. SERVICES DIAGNOSTICS DOMAIN
        // ==========================================
        private void UpdateServicesDomain()
        {
            try
            {
                if (m_ServiceBuildingQuery.IsEmptyIgnoreFilter)
                {
                    ServiceFacilities.Clear();
                    ServiceSummary = default;
                    return;
                }

                var entities = m_ServiceBuildingQuery.ToEntityArray(Allocator.TempJob);
                try
                {
                    var facList = new List<ServiceFacilityEntry>(entities.Length);
                    int critical = 0, warning = 0, optimal = 0;
                    float effSum = 0f;

                    for (int i = 0; i < entities.Length; i++)
                    {
                        var bEnt = entities[i];
                        if (!EntityManager.Exists(bEnt) || EntityManager.HasComponent<Deleted>(bEnt)) continue;
                        var pos = EntityManager.HasComponent<Transform>(bEnt) ? EntityManager.GetComponentData<Transform>(bEnt).m_Position : float3.zero;

                        string type = "Healthcare";
                        if (EntityManager.HasComponent<Game.Buildings.Hospital>(bEnt)) type = "Healthcare";
                        else if (EntityManager.HasComponent<Game.Buildings.PoliceStation>(bEnt)) type = "Police";
                        else if (EntityManager.HasComponent<Game.Buildings.FireStation>(bEnt)) type = "Fire";
                        else if (EntityManager.HasComponent<Game.Buildings.GarbageFacility>(bEnt)) type = "Garbage";
                        else if (EntityManager.HasComponent<Game.Buildings.DeathcareFacility>(bEnt)) type = "Deathcare";
                        else if (EntityManager.HasComponent<Game.Buildings.PostFacility>(bEnt)) type = "Post";
                        else if (EntityManager.HasComponent<Game.Buildings.MaintenanceDepot>(bEnt)) type = "Road Maintenance";
                        else if (EntityManager.HasComponent<Game.Buildings.School>(bEnt)) type = "Education";

                        float eff = 100f;
                        string factorPenalty = "";
                        if (EntityManager.HasBuffer<Efficiency>(bEnt))
                        {
                            var effBuf = EntityManager.GetBuffer<Efficiency>(bEnt);
                            for (int e = 0; e < effBuf.Length; e++)
                            {
                                if (effBuf[e].m_Efficiency < 0.75f) factorPenalty = $"{effBuf[e].m_Factor} penalty";
                            }
                            eff = CalculateCombinedEfficiency(effBuf) * 100f;
                        }

                        effSum += eff;
                        string status = "Optimal";
                        if (eff < 60f) { status = "Critical"; critical++; }
                        else if (eff < 85f || !string.IsNullOrEmpty(factorPenalty)) { status = "Warning"; warning++; }
                        else { optimal++; }

                        string measured = $"Operating Efficiency: {(int)eff}%" + (!string.IsNullOrEmpty(factorPenalty) ? $" | {factorPenalty}" : "");
                        string inferred = "Status is based on CS2's combined efficiency factors. City Pulse does not infer a cause beyond the reported factors.";

                        facList.Add(new ServiceFacilityEntry
                        {
                            EntityIndex = bEnt.Index,
                            Name = $"{type} Facility #{bEnt.Index}",
                            Type = type,
                            Status = status,
                            EfficiencyPercent = (float)Math.Round(eff, 1),
                            MeasuredBreakdown = measured,
                            InferredCause = inferred,
                            Confidence = "MEASURED",
                            Position = pos
                        });
                    }

                    facList.Sort((a, b) => (b.Status.CompareTo(a.Status) != 0) ? b.Status.CompareTo(a.Status) : a.EfficiencyPercent.CompareTo(b.EfficiencyPercent));
                    float avgEff = entities.Length > 0 ? effSum / entities.Length : 100f;
                    int sHealth = entities.Length > 0 ? math.clamp((int)Math.Round(avgEff) - (critical * 10), 15, 100) : 100;

                    ServiceFacilities = facList;
                    ServiceSummary = new ServiceSummaryData
                    {
                        TotalFacilities = entities.Length,
                        CriticalCount = critical,
                        WarningCount = warning,
                        OptimalCount = optimal,
                        AverageEfficiency = (float)Math.Round(avgEff, 1),
                        HealthScore = sHealth
                    };
                }
                finally
                {
                    entities.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in Services Domain update");
            }
        }

        // ==========================================
        // 5. BUILDINGS DIAGNOSTICS DOMAIN (INCREMENTAL)
        // ==========================================
        private void UpdateBuildingDomainIncremental()
        {
            try
            {
                if (m_BuildingQuery.IsEmptyIgnoreFilter)
                {
                    BuildingIssues.Clear();
                    BuildingSummary = default;
                    return;
                }

                var entities = m_BuildingQuery.ToEntityArray(Allocator.TempJob);
                try
                {
                    int totalCount = entities.Length;
                    int start = m_BuildingScanCursor;
                    if (start >= totalCount) start = 0;
                    int end = math.min(start + kBuildingBatchSize, totalCount);
                    if (start == 0)
                    {
                        m_BuildingScanIssues.Clear();
                        m_BuildingScanTroubled = 0;
                        m_BuildingScanWorkerShortage = 0;
                        m_BuildingScanLowEfficiency = 0;
                        m_BuildingScanAbandoned = 0;
                    }

                    for (int i = start; i < end; i++)
                    {
                        var bEnt = entities[i];
                        bool isAb = EntityManager.HasComponent<Abandoned>(bEnt);
                        string zone = "Special";
                        if (EntityManager.HasComponent<ResidentialProperty>(bEnt)) zone = "Residential";
                        else if (EntityManager.HasComponent<CommercialProperty>(bEnt)) zone = "Commercial";
                        else if (EntityManager.HasComponent<IndustrialProperty>(bEnt)) zone = "Industrial";
                        else if (EntityManager.HasComponent<OfficeProperty>(bEnt)) zone = "Office";

                        int vacancies = 0, workers = 0, totalJobs = 0;
                        if (EntityManager.HasBuffer<Renter>(bEnt))
                        {
                            var renters = EntityManager.GetBuffer<Renter>(bEnt);
                            for (int r = 0; r < renters.Length; r++)
                            {
                                var rEnt = renters[r].m_Renter;
                                if (EntityManager.HasComponent<FreeWorkplaces>(rEnt)) vacancies += EntityManager.GetComponentData<FreeWorkplaces>(rEnt).Count;
                                if (EntityManager.HasBuffer<Employee>(rEnt)) workers += EntityManager.GetBuffer<Employee>(rEnt).Length;
                            }
                            totalJobs = workers + vacancies;
                        }

                        float eff = 100f;
                        if (EntityManager.HasBuffer<Efficiency>(bEnt))
                        {
                            var effBuf = EntityManager.GetBuffer<Efficiency>(bEnt);
                            eff = CalculateCombinedEfficiency(effBuf) * 100f;
                        }

                        if (isAb)
                        {
                            m_BuildingScanAbandoned++;
                            m_BuildingScanTroubled++;
                            if (m_BuildingScanIssues.Count < 20)
                            {
                                var pos = EntityManager.HasComponent<Transform>(bEnt) ? EntityManager.GetComponentData<Transform>(bEnt).m_Position : float3.zero;
                                m_BuildingScanIssues.Add(new BuildingIssueEntry
                                {
                                    EntityIndex = bEnt.Index,
                                    BuildingName = $"{zone} #{bEnt.Index}",
                                    Zone = zone,
                                    Severity = "Critical",
                                    PrimaryIssue = "Building Abandoned",
                                    MeasuredEvidence = "Building marked permanently abandoned.",
                                    InferredCause = "The building is marked abandoned; City Pulse does not infer why.",
                                    Confidence = "MEASURED STATE",
                                    HealthScore = 10,
                                    Position = pos
                                });
                            }
                        }
                        else if (totalJobs > 0 && vacancies > totalJobs * 0.4f)
                        {
                            m_BuildingScanWorkerShortage++;
                            m_BuildingScanTroubled++;
                            if (m_BuildingScanIssues.Count < 20)
                            {
                                var pos = EntityManager.HasComponent<Transform>(bEnt) ? EntityManager.GetComponentData<Transform>(bEnt).m_Position : float3.zero;
                                m_BuildingScanIssues.Add(new BuildingIssueEntry
                                {
                                    EntityIndex = bEnt.Index,
                                    BuildingName = $"{zone} #{bEnt.Index}",
                                    Zone = zone,
                                    Severity = "Critical",
                                    PrimaryIssue = "Worker Shortage",
                                    MeasuredEvidence = $"{vacancies} open jobs out of {totalJobs}",
                                    InferredCause = "Open jobs are measured. The cause of the shortage is not determined.",
                                    Confidence = "MEASURED COUNT",
                                    HealthScore = 55,
                                    Position = pos
                                });
                            }
                        }
                        else if (eff < 60f)
                        {
                            m_BuildingScanLowEfficiency++;
                            m_BuildingScanTroubled++;
                            if (m_BuildingScanIssues.Count < 20)
                            {
                                var pos = EntityManager.HasComponent<Transform>(bEnt) ? EntityManager.GetComponentData<Transform>(bEnt).m_Position : float3.zero;
                                m_BuildingScanIssues.Add(new BuildingIssueEntry
                                {
                                    EntityIndex = bEnt.Index,
                                    BuildingName = $"{zone} #{bEnt.Index}",
                                    Zone = zone,
                                    Severity = "Moderate",
                                    PrimaryIssue = "Operating Inefficiency",
                                    MeasuredEvidence = $"Operating at {(int)eff}% efficiency",
                                    InferredCause = "Low combined efficiency is measured; inspect the building's vanilla efficiency factors for the cause.",
                                    Confidence = "MEASURED",
                                    HealthScore = 60,
                                    Position = pos
                                });
                            }
                        }
                    }

                    if (end < totalCount)
                    {
                        m_BuildingScanCursor = end;
                        return;
                    }

                    m_BuildingScanCursor = 0;
                    int bHealth = totalCount > 0 ? math.clamp(100 - (m_BuildingScanTroubled * 100 / totalCount) * 2, 20, 100) : 100;

                    BuildingIssues = new List<BuildingIssueEntry>(m_BuildingScanIssues);
                    BuildingSummary = new BuildingSummaryData
                    {
                        TotalBuildingsScanned = totalCount,
                        TroubledCount = m_BuildingScanTroubled,
                        WorkerShortageCount = m_BuildingScanWorkerShortage,
                        LowEfficiencyCount = m_BuildingScanLowEfficiency,
                        AbandonedCount = m_BuildingScanAbandoned,
                        HealthScore = bHealth
                    };
                }
                finally
                {
                    entities.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in Buildings Domain update");
            }
        }

        private void InspectSelectedBuilding()
        {
            if (m_ToolSystem == null || EntityManager == null) return;
            var sel = m_ToolSystem.selected;
            if (sel == Entity.Null || !EntityManager.Exists(sel) || !EntityManager.HasComponent<Building>(sel))
            {
                if (SelectedBuilding.EntityIndex > 0)
                {
                    SelectedBuilding = default;
                    OnDataUpdated?.Invoke();
                }
                return;
            }

            string zone = "Special";
            if (EntityManager.HasComponent<ResidentialProperty>(sel)) zone = "Residential";
            else if (EntityManager.HasComponent<CommercialProperty>(sel)) zone = "Commercial";
            else if (EntityManager.HasComponent<IndustrialProperty>(sel)) zone = "Industrial";
            else if (EntityManager.HasComponent<OfficeProperty>(sel)) zone = "Office";

            bool isAb = EntityManager.HasComponent<Abandoned>(sel);
            bool isDam = EntityManager.HasComponent<Damaged>(sel);

            int workers = 0, vacancies = 0, totalJobs = 0, occupants = 0;
            if (EntityManager.HasBuffer<Renter>(sel))
            {
                var renters = EntityManager.GetBuffer<Renter>(sel);
                occupants = renters.Length;
                for (int r = 0; r < renters.Length; r++)
                {
                    var rEnt = renters[r].m_Renter;
                    if (EntityManager.HasComponent<FreeWorkplaces>(rEnt)) vacancies += EntityManager.GetComponentData<FreeWorkplaces>(rEnt).Count;
                    if (EntityManager.HasBuffer<Employee>(rEnt)) workers += EntityManager.GetBuffer<Employee>(rEnt).Length;
                }
                totalJobs = workers + vacancies;
            }

            float eff = 100f;
            string penalty = "";
            if (EntityManager.HasBuffer<Efficiency>(sel))
            {
                var effBuf = EntityManager.GetBuffer<Efficiency>(sel);
                for (int e = 0; e < effBuf.Length; e++)
                {
                    if (effBuf[e].m_Efficiency < 0.7f) penalty = $"{effBuf[e].m_Factor} penalty (-{(int)((1f - effBuf[e].m_Efficiency) * 100)}%)";
                }
                eff = CalculateCombinedEfficiency(effBuf) * 100f;
            }

            string primary = isAb ? "Building Abandoned" : (isDam ? "Structural Damage" : (totalJobs > 0 && vacancies > totalJobs * 0.4f ? "Critical Employee Shortage" : (eff < 60f ? "Severe Operating Inefficiency" : "Optimal Operation")));
            string measured = isAb ? "Marked as abandoned." : $"Occupants: {occupants} | Workers: {workers}/{totalJobs} | Combined efficiency: {(int)eff}%" + (!string.IsNullOrEmpty(penalty) ? $" | {penalty}" : "");
            string inferred = "City Pulse reports component state and measured counts; it does not infer the underlying cause.";
            int health = isAb ? 10 : (isDam ? 30 : (eff < 60f ? 60 : 95));

            SelectedBuilding = new SelectedBuildingDetail
            {
                EntityIndex = sel.Index,
                Name = $"{zone} Property #{sel.Index}",
                Zone = zone,
                HealthScore = health,
                Occupants = occupants,
                Workers = workers,
                TotalJobs = totalJobs,
                Vacancies = vacancies,
                EfficiencyPercent = (float)Math.Round(eff, 1),
                PrimaryProblem = primary,
                MeasuredBreakdown = measured,
                InferredDiagnosis = inferred,
                Confidence = "MEASURED",
                IsAbandoned = isAb,
                IsDamaged = isDam
            };
        }

        // ==========================================
        // 6. NETWORK DIAGNOSTICS DOMAIN (LANE DOCTOR)
        // ==========================================
        private void UpdateNetworkDomain()
        {
            try
            {
                int totalSegments = m_LaneQuery.CalculateEntityCount();
                var defects = new List<NetworkDefectEntry>();
                int crit = 0, err = 0, warn = 0, info = 0;
                int scanned = 0;

                if (!m_LaneQuery.IsEmptyIgnoreFilter)
                {
                    var laneEntities = m_LaneQuery.ToEntityArray(Allocator.TempJob);
                    try
                    {
                        scanned = math.min(laneEntities.Length, 5000);
                        for (int i = 0; i < scanned; i++)
                        {
                            var lEnt = laneEntities[i];
                            if (EntityManager.HasComponent<Curve>(lEnt))
                            {
                                var curve = EntityManager.GetComponentData<Curve>(lEnt);
                                float length = math.length(curve.m_Bezier.d - curve.m_Bezier.a);
                                bool finite = math.all(math.isfinite(curve.m_Bezier.a)) &&
                                              math.all(math.isfinite(curve.m_Bezier.b)) &&
                                              math.all(math.isfinite(curve.m_Bezier.c)) &&
                                              math.all(math.isfinite(curve.m_Bezier.d));

                                if (!finite || length <= 0.001f)
                                {
                                    warn++;
                                    if (defects.Count < 20)
                                    {
                                        defects.Add(new NetworkDefectEntry
                                        {
                                            Id = defects.Count + 1,
                                            EntityIndex = lEnt.Index,
                                            Title = finite ? "Zero-length lane observation" : "Non-finite lane curve observation",
                                            Severity = "Warning",
                                            Category = "Network lane",
                                            WhatWeFound = finite ? $"Lane chord length is {length:F4} m." : "One or more lane curve coordinates are not finite.",
                                            WhyItMatters = "This is a read-only observation. Confirm the location with CS2's network tools before changing the road.",
                                            Evidence = finite ? $"Lane chord: {length:F4} m" : "Curve contains NaN or Infinity",
                                            RepairStrategy = "Inspect and, if necessary, rebuild the affected network piece with the vanilla network tool.",
                                            IsRepairable = false,
                                            CurrentPreview = $"Lane entity #{lEnt.Index}",
                                            ProposedPreview = "No automatic mutation available",
                                            Position = finite ? curve.m_Bezier.a : float3.zero
                                        });
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        laneEntities.Dispose();
                    }
                }

                int nHealth = totalSegments > 0 ? math.clamp(100 - (warn * 5), 0, 100) : 100;

                NetworkDefects = defects;
                NetworkSummary = new NetworkSummaryData
                {
                    TotalSegments = totalSegments,
                    ScannedSegments = scanned,
                    CriticalCount = crit,
                    ErrorCount = err,
                    WarningCount = warn,
                    InfoCount = info,
                    HealthScore = nHealth,
                    LastStatus = defects.Count > 0
                        ? $"{defects.Count} invalid lane-curve observation(s); no automatic repair"
                        : $"No invalid lane curves found in {scanned} sampled lane entities",
                    UndoCount = 0
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in Network Domain update");
            }
        }

        // ==========================================
        // 7. MASTER CONSOLIDATION & TOP CITY ISSUES
        // ==========================================
        private void ConsolidateMasterPulse()
        {
            var alerts = new List<UnifiedCityAlert>();
            int alertId = 1;

            // 1. Critical traffic bottlenecks
            for (int i = 0; i < TrafficBottlenecks.Count; i++)
            {
                var bn = TrafficBottlenecks[i];
                if (bn.QueuePressurePercent >= 50f)
                {
                    alerts.Add(new UnifiedCityAlert
                    {
                        Id = alertId++,
                        SourceModule = DiagnosticModuleType.Traffic,
                        Severity = AlertSeverity.Critical,
                        Title = $"Persistent vanilla bottleneck signal at {bn.LocationName}",
                        MeasuredEvidence = $"Marker persistence: {bn.QueuePressurePercent:F0}% | sampled stopped vehicles: {bn.StoppedRatioPercent:F0}%",
                        InferredCause = bn.InferredCause,
                        Confidence = bn.Confidence,
                        EntityIndex = bn.EntityIndex,
                        Position = bn.Position,
                        ActionLabel = "Inspect Traffic Node"
                    });
                }
            }

            // 2. Overcrowded or bunching transit lines
            for (int i = 0; i < TransitLines.Count; i++)
            {
                var l = TransitLines[i];
                if (l.Category == "Overcrowded" || l.BunchingDetected)
                {
                    alerts.Add(new UnifiedCityAlert
                    {
                        Id = alertId++,
                        SourceModule = DiagnosticModuleType.Transit,
                        Severity = l.Category == "Overcrowded" ? AlertSeverity.Critical : AlertSeverity.Warning,
                        Title = l.BunchingDetected ? $"Vehicle Bunching on {l.Name}" : $"Overcrowded Service on {l.Name}",
                        MeasuredEvidence = l.MeasuredEvidence,
                        InferredCause = l.InferredCause,
                        Confidence = l.Confidence,
                        EntityIndex = l.EntityIndex,
                        Position = l.WorstStopPos,
                        ActionLabel = "Inspect Line"
                    });
                }
            }

            // 3. Parking facility overcapacity
            for (int i = 0; i < ParkingFacilities.Count; i++)
            {
                var p = ParkingFacilities[i];
                if (p.Status == "Overcapacity")
                {
                    alerts.Add(new UnifiedCityAlert
                    {
                        Id = alertId++,
                        SourceModule = DiagnosticModuleType.Parking,
                        Severity = AlertSeverity.Warning,
                        Title = $"Parking Saturated at {p.Name}",
                        MeasuredEvidence = p.MeasuredBreakdown,
                        InferredCause = p.InferredCause,
                        Confidence = p.Confidence,
                        EntityIndex = p.EntityIndex,
                        Position = p.Position,
                        ActionLabel = "Inspect Garage"
                    });
                }
            }

            // 4. Critical service facilities
            for (int i = 0; i < ServiceFacilities.Count; i++)
            {
                var s = ServiceFacilities[i];
                if (s.Status == "Critical")
                {
                    alerts.Add(new UnifiedCityAlert
                    {
                        Id = alertId++,
                        SourceModule = DiagnosticModuleType.Services,
                        Severity = AlertSeverity.Critical,
                        Title = $"Low measured efficiency at {s.Name}",
                        MeasuredEvidence = s.MeasuredBreakdown,
                        InferredCause = s.InferredCause,
                        Confidence = s.Confidence,
                        EntityIndex = s.EntityIndex,
                        Position = s.Position,
                        ActionLabel = "Inspect Service"
                    });
                }
            }

            // 5. Critical building abandonment / worker shortages
            for (int i = 0; i < BuildingIssues.Count; i++)
            {
                var b = BuildingIssues[i];
                if (b.Severity == "Critical")
                {
                    alerts.Add(new UnifiedCityAlert
                    {
                        Id = alertId++,
                        SourceModule = DiagnosticModuleType.Buildings,
                        Severity = AlertSeverity.Warning,
                        Title = $"{b.PrimaryIssue} at {b.BuildingName}",
                        MeasuredEvidence = b.MeasuredEvidence,
                        InferredCause = b.InferredCause,
                        Confidence = b.Confidence,
                        EntityIndex = b.EntityIndex,
                        Position = b.Position,
                        ActionLabel = "Inspect Building"
                    });
                }
            }

            // 6. Network topology defects
            for (int i = 0; i < NetworkDefects.Count; i++)
            {
                var nd = NetworkDefects[i];
                if (nd.Severity == "Critical")
                {
                    alerts.Add(new UnifiedCityAlert
                    {
                        Id = alertId++,
                        SourceModule = DiagnosticModuleType.Network,
                        Severity = AlertSeverity.Critical,
                        Title = nd.Title,
                        MeasuredEvidence = nd.Evidence,
                        InferredCause = nd.WhyItMatters,
                        Confidence = "MEASURED OBSERVATION",
                        EntityIndex = nd.EntityIndex,
                        Position = nd.Position,
                        ActionLabel = "Inspect Geometry"
                    });
                }
            }

            // Sort alerts: Critical first, then Warning, then Advisory
            alerts.Sort((a, b) => b.Severity.CompareTo(a.Severity));

            int critCount = 0, warnCount = 0, advCount = 0;
            for (int i = 0; i < alerts.Count; i++)
            {
                if (alerts[i].Severity == AlertSeverity.Critical) critCount++;
                else if (alerts[i].Severity == AlertSeverity.Warning) warnCount++;
                else advCount++;
            }

            // Master City Health Score (0-100)
            int masterScore = (TrafficSummary.CityHealthScore + TransitSummary.HealthScore + ParkingSummary.HealthScore + ServiceSummary.HealthScore + BuildingSummary.HealthScore + NetworkSummary.HealthScore) / 6;
            masterScore = math.clamp(masterScore, 10, 100);

            bool legacyDetected = InteropManager.CheckForLegacyMods(World, out var legacyMsg);

            TopCityAlerts = alerts;
            OverviewData = new MasterCityOverviewData
            {
                MasterCityHealthScore = masterScore,
                TrafficHealthScore = TrafficSummary.CityHealthScore,
                TransitHealthScore = TransitSummary.HealthScore,
                ParkingHealthScore = ParkingSummary.HealthScore,
                ServiceHealthScore = ServiceSummary.HealthScore,
                BuildingHealthScore = BuildingSummary.HealthScore,
                NetworkHealthScore = NetworkSummary.HealthScore,
                CriticalAlertCount = critCount,
                WarningAlertCount = warnCount,
                AdvisoryAlertCount = advCount,
                LegacyModDetected = legacyDetected,
                LegacyModWarningMessage = legacyMsg
            };

            OnDataUpdated?.Invoke();
        }

        // ==========================================
        // ==========================================
        // BEFORE / AFTER BASELINE MEASUREMENT
        // ==========================================
        public void CaptureBaselineForEntity(int entityIndex)
        {
            float speed = TrafficSummary.AverageCitySpeedKph;
            float stopped = 0f;
            float queue = 0f;

            for (int i = 0; i < TrafficBottlenecks.Count; i++)
            {
                if (TrafficBottlenecks[i].EntityIndex == entityIndex)
                {
                    speed = TrafficBottlenecks[i].MeasuredSpeedKph;
                    stopped = TrafficBottlenecks[i].StoppedRatioPercent;
                    queue = TrafficBottlenecks[i].QueuePressurePercent;
                    break;
                }
            }

            m_Baselines[entityIndex] = new BeforeAfterSnapshot
            {
                BaselineId = $"Base-{entityIndex}-{DateTime.Now:HHmmss}",
                Timestamp = DateTime.Now,
                EntityIndex = entityIndex,
                AverageSpeedKph = speed,
                StoppedRatioPercent = stopped,
                QueuePressurePercent = queue,
                CongestionIndex = TrafficSummary.CongestionIndex
            };

            Log.Info($"Captured baseline for Entity #{entityIndex}: Speed {speed:F1} km/h, Queue {queue:F0}%");
            OnDataUpdated?.Invoke();
        }

        public BeforeAfterComparisonData GetBeforeAfterComparison(int entityIndex)
        {
            if (!m_Baselines.TryGetValue(entityIndex, out var baseline))
            {
                return new BeforeAfterComparisonData { HasBaseline = false };
            }

            float currentSpeed = TrafficSummary.AverageCitySpeedKph;
            float currentStopped = 0f;
            float currentQueue = 0f;

            for (int i = 0; i < TrafficBottlenecks.Count; i++)
            {
                if (TrafficBottlenecks[i].EntityIndex == entityIndex)
                {
                    currentSpeed = TrafficBottlenecks[i].MeasuredSpeedKph;
                    currentStopped = TrafficBottlenecks[i].StoppedRatioPercent;
                    currentQueue = TrafficBottlenecks[i].QueuePressurePercent;
                    break;
                }
            }

            float speedDelta = baseline.AverageSpeedKph > 0 ? ((currentSpeed - baseline.AverageSpeedKph) / baseline.AverageSpeedKph) * 100f : 0f;
            float stoppedDelta = baseline.StoppedRatioPercent > 0 ? ((currentStopped - baseline.StoppedRatioPercent) / baseline.StoppedRatioPercent) * 100f : 0f;
            float queueDelta = baseline.QueuePressurePercent > 0 ? ((currentQueue - baseline.QueuePressurePercent) / baseline.QueuePressurePercent) * 100f : 0f;

            string verdict = "UNCHANGED";
            if (speedDelta >= 10f || queueDelta <= -15f) verdict = "IMPROVED";
            else if (speedDelta <= -10f || queueDelta >= 15f) verdict = "WORSE";

            return new BeforeAfterComparisonData
            {
                HasBaseline = true,
                TargetEntityIndex = entityIndex,
                LocationName = $"Location #{entityIndex}",
                BaselineSpeedKph = (float)Math.Round(baseline.AverageSpeedKph, 1),
                CurrentSpeedKph = (float)Math.Round(currentSpeed, 1),
                SpeedDeltaPercent = (float)Math.Round(speedDelta, 1),
                BaselineStoppedRatio = (float)Math.Round(baseline.StoppedRatioPercent, 1),
                CurrentStoppedRatio = (float)Math.Round(currentStopped, 1),
                StoppedRatioDeltaPercent = (float)Math.Round(stoppedDelta, 1),
                BaselineQueuePressure = (float)Math.Round(baseline.QueuePressurePercent, 1),
                CurrentQueuePressure = (float)Math.Round(currentQueue, 1),
                QueuePressureDeltaPercent = (float)Math.Round(queueDelta, 1),
                Verdict = verdict
            };
        }

        public void SampleTimelinePoint()
        {
            int problemCount = OverviewData.CriticalAlertCount + OverviewData.WarningAlertCount;
            CityTimeline.Add(new CityTimelinePoint
            {
                TimeLabel = DateTime.Now.ToString("HH:mm:ss"),
                MasterHealth = OverviewData.MasterCityHealthScore,
                AvgSpeedKph = TrafficSummary.AverageCitySpeedKph,
                StoppedRatioPercent = 0f,
                TotalProblemCount = problemCount
            });

            if (CityTimeline.Count > 30)
            {
                CityTimeline.RemoveAt(0); // Bounded memory ring-buffer
            }
        }

        // Calling BuildingUtils.GetEfficiency(DynamicBuffer<Efficiency>) from a net48 mod
        // forces Roslyn to load every overload on BuildingUtils, including the game's
        // Span<float> overload. That overload is compiled against Unity's runtime mscorlib
        // and cannot be resolved by the public mod toolchain reference assemblies. Keep the
        // vanilla calculation here instead: multiply non-negative factors, round to the
        // nearest percentage point, and retain vanilla's 1% floor for a non-zero result.
        private static float CalculateCombinedEfficiency(DynamicBuffer<Efficiency> factors)
        {
            float combined = 1f;
            for (int i = 0; i < factors.Length; i++)
            {
                combined *= math.max(0f, factors[i].m_Efficiency);
            }

            if (!(combined > 0f))
            {
                return 0f;
            }

            return math.max(0.01f, math.round(100f * combined) / 100f);
        }

        public bool LaunchSuiteTool(string toolName, int entityIndex, float3 pos)
        {
            return Colossal.UtilitySuite.Interop.SuiteBridge.Launch(toolName, entityIndex, pos);
        }
    }
}
