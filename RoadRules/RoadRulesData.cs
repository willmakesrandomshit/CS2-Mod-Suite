using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;

namespace RoadRules
{
    [Flags]
    public enum VehicleAccessFlags
    {
        None = 0,
        // These are Road Rules UI concepts, not a claim that CS2 exposes six
        // independent native pathfinding masks. RoadRuleTranslator is the
        // authoritative mapping to the smaller set of real engine mechanisms.
        PrivateCars = 1 << 0,  // 1
        Trucks = 1 << 1,       // 2
        Buses = 1 << 2,        // 4
        Taxis = 1 << 3,        // 8
        Emergency = 1 << 4,    // 16
        Service = 1 << 5,      // 32
        All = PrivateCars | Trucks | Buses | Taxis | Emergency | Service
    }

    public enum LaneRulePreset
    {
        DefaultAll,
        TruckBan,
        TransitOnly,
        LocalAccessOnly,
        EmergencyAndServiceOnly,
        LaneClosed,
        Custom
    }

    public class PhysicalRoadLane
    {
        public int PhysicalIndex;
        public List<Entity> RoutingEntities = new List<Entity>();
        public Entity PrimaryDisplayEntity;
        public Entity CanonicalEntity;
        public Entity MasterEntity;
        public Curve DisplayCurve;
        public float LateralOffset;
        public int CrossSectionIndex;
        public ushort NativeSubIndex;
        public bool IsInvert;
        public ushort CarriagewayGroup;
        public string LanePositionLabel = "Lane";

        public VehicleAccessFlags AllowedVehicles = VehicleAccessFlags.All;
        public bool LocalAccessOnly = false;
        public bool IsClosed = false;
        public float CustomSpeedLimitKph = 0f;
        public LaneRulePreset Preset = LaneRulePreset.DefaultAll;

        public void SetClosed(bool closed)
        {
            IsClosed = closed;
            if (closed)
            {
                AllowedVehicles = VehicleAccessFlags.None;
                LocalAccessOnly = false;
            }
            else if (AllowedVehicles == VehicleAccessFlags.None)
            {
                AllowedVehicles = VehicleAccessFlags.All;
            }
            Preset = closed ? LaneRulePreset.LaneClosed : LaneRulePreset.Custom;
        }

        public static PhysicalRoadLane CreateDefault(
            int physicalIndex,
            List<Entity> routingEntities,
            Entity primaryEntity,
            Entity canonicalEntity,
            Entity masterEntity,
            Curve displayCurve,
            float lateralOffset,
            int crossSectionIndex,
            ushort nativeSubIndex,
            bool isInvert,
            ushort carriagewayGroup,
            string posLabel)
        {
            return new PhysicalRoadLane
            {
                PhysicalIndex = physicalIndex,
                RoutingEntities = routingEntities != null ? new List<Entity>(routingEntities) : new List<Entity>(),
                PrimaryDisplayEntity = primaryEntity,
                CanonicalEntity = canonicalEntity,
                MasterEntity = masterEntity,
                DisplayCurve = displayCurve,
                LateralOffset = lateralOffset,
                CrossSectionIndex = crossSectionIndex,
                NativeSubIndex = nativeSubIndex,
                IsInvert = isInvert,
                CarriagewayGroup = carriagewayGroup,
                LanePositionLabel = posLabel,
                AllowedVehicles = VehicleAccessFlags.All,
                LocalAccessOnly = false,
                IsClosed = false,
                CustomSpeedLimitKph = 0f,
                Preset = LaneRulePreset.DefaultAll
            };
        }

        public PhysicalRoadLane Clone()
        {
            return new PhysicalRoadLane
            {
                PhysicalIndex = this.PhysicalIndex,
                RoutingEntities = new List<Entity>(this.RoutingEntities),
                PrimaryDisplayEntity = this.PrimaryDisplayEntity,
                CanonicalEntity = this.CanonicalEntity,
                MasterEntity = this.MasterEntity,
                DisplayCurve = this.DisplayCurve,
                LateralOffset = this.LateralOffset,
                CrossSectionIndex = this.CrossSectionIndex,
                NativeSubIndex = this.NativeSubIndex,
                IsInvert = this.IsInvert,
                CarriagewayGroup = this.CarriagewayGroup,
                LanePositionLabel = this.LanePositionLabel,
                AllowedVehicles = this.AllowedVehicles,
                LocalAccessOnly = this.LocalAccessOnly,
                IsClosed = this.IsClosed,
                CustomSpeedLimitKph = this.CustomSpeedLimitKph,
                Preset = this.Preset
            };
        }
    }

    public class EdgeRulesRecord
    {
        public Entity EdgeEntity;
        public bool IsInvert;
        public ushort CarriagewayGroup;
        public string CarriagewayName = "Selected Carriageway";

        public int TotalEdgeSubLanes;
        public int RawCarLaneCount;
        public int DirectionMatchedCarLaneCount;
        public int CarriagewayMatchedCarLaneCount;
        public int FilteredOutSubLanes;
        public string CandidateDiagnostics = string.Empty;
        public List<Entity> SharedRoutingEntities = new List<Entity>();
        public Dictionary<Entity, string> RoutingAssignmentReasons = new Dictionary<Entity, string>();

        public List<PhysicalRoadLane> PhysicalLanes = new List<PhysicalRoadLane>();

        public EdgeRulesRecord Clone()
        {
            var copy = new EdgeRulesRecord
            {
                EdgeEntity = this.EdgeEntity,
                IsInvert = this.IsInvert,
                CarriagewayGroup = this.CarriagewayGroup,
                CarriagewayName = this.CarriagewayName,
                TotalEdgeSubLanes = this.TotalEdgeSubLanes,
                RawCarLaneCount = this.RawCarLaneCount,
                DirectionMatchedCarLaneCount = this.DirectionMatchedCarLaneCount,
                CarriagewayMatchedCarLaneCount = this.CarriagewayMatchedCarLaneCount,
                FilteredOutSubLanes = this.FilteredOutSubLanes,
                CandidateDiagnostics = this.CandidateDiagnostics,
                SharedRoutingEntities = new List<Entity>(this.SharedRoutingEntities),
                RoutingAssignmentReasons = new Dictionary<Entity, string>(this.RoutingAssignmentReasons),
                PhysicalLanes = new List<PhysicalRoadLane>(this.PhysicalLanes.Count)
            };
            for (int i = 0; i < this.PhysicalLanes.Count; i++)
            {
                copy.PhysicalLanes.Add(this.PhysicalLanes[i].Clone());
            }
            return copy;
        }
    }
}
