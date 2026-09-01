using System;
using System.Collections.Generic;
using Colossal.UI.Binding;
using Unity.Entities;
using Unity.Mathematics;

namespace CityPulse
{
    public enum AlertSeverity
    {
        Advisory,
        Warning,
        Critical
    }

    public enum DiagnosticModuleType
    {
        Traffic,
        Transit,
        Parking,
        Services,
        Buildings,
        Network
    }

    // Unified Top City Alert DTO
    public struct UnifiedCityAlert : IJsonWritable
    {
        public int Id;
        public DiagnosticModuleType SourceModule;
        public AlertSeverity Severity;
        public string Title;
        public string MeasuredEvidence;
        public string InferredCause;
        public string Confidence;
        public int EntityIndex;
        public float3 Position;
        public string ActionLabel;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(UnifiedCityAlert));
            writer.PropertyName("id");
            writer.Write(Id);
            writer.PropertyName("sourceModule");
            writer.Write(SourceModule.ToString());
            writer.PropertyName("severity");
            writer.Write(Severity.ToString());
            writer.PropertyName("title");
            writer.Write(Title ?? "City Alert");
            writer.PropertyName("measuredEvidence");
            writer.Write(MeasuredEvidence ?? "");
            writer.PropertyName("inferredCause");
            writer.Write(InferredCause ?? "");
            writer.PropertyName("confidence");
            writer.Write(Confidence ?? "UNSPECIFIED");
            writer.PropertyName("entityIndex");
            writer.Write(EntityIndex);
            writer.PropertyName("posX");
            writer.Write(Position.x);
            writer.PropertyName("posY");
            writer.Write(Position.y);
            writer.PropertyName("posZ");
            writer.Write(Position.z);
            writer.PropertyName("actionLabel");
            writer.Write(ActionLabel ?? "Inspect");
            writer.TypeEnd();
        }
    }

    // Master City Health & Domain Summaries DTO
    public struct MasterCityOverviewData : IJsonWritable
    {
        public int MasterCityHealthScore; // 0 to 100
        public int TrafficHealthScore;
        public int TransitHealthScore;
        public int ParkingHealthScore;
        public int ServiceHealthScore;
        public int BuildingHealthScore;
        public int NetworkHealthScore;

        public int CriticalAlertCount;
        public int WarningAlertCount;
        public int AdvisoryAlertCount;

        public bool LegacyModDetected;
        public string LegacyModWarningMessage;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(MasterCityOverviewData));
            writer.PropertyName("masterCityHealthScore");
            writer.Write(MasterCityHealthScore);
            writer.PropertyName("trafficHealthScore");
            writer.Write(TrafficHealthScore);
            writer.PropertyName("transitHealthScore");
            writer.Write(TransitHealthScore);
            writer.PropertyName("parkingHealthScore");
            writer.Write(ParkingHealthScore);
            writer.PropertyName("serviceHealthScore");
            writer.Write(ServiceHealthScore);
            writer.PropertyName("buildingHealthScore");
            writer.Write(BuildingHealthScore);
            writer.PropertyName("networkHealthScore");
            writer.Write(NetworkHealthScore);

            writer.PropertyName("criticalAlertCount");
            writer.Write(CriticalAlertCount);
            writer.PropertyName("warningAlertCount");
            writer.Write(WarningAlertCount);
            writer.PropertyName("advisoryAlertCount");
            writer.Write(AdvisoryAlertCount);

            writer.PropertyName("legacyModDetected");
            writer.Write(LegacyModDetected);
            writer.PropertyName("legacyModWarningMessage");
            writer.Write(LegacyModWarningMessage ?? "");
            writer.TypeEnd();
        }
    }

    // 1. TRAFFIC DATA MODELS
    public struct TrafficBottleneckData : IJsonWritable
    {
        public int Id;
        public int EntityIndex;
        public string LocationName;
        public string Severity;
        public float MeasuredSpeedKph;
        public float SpeedLimitKph;
        public float StoppedRatioPercent;
        public int VehicleCount;
        public float QueuePressurePercent;
        public string InferredCause;
        public string Confidence;
        public string FlowPropagation;
        public float3 Position;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(TrafficBottleneckData));
            writer.PropertyName("id");
            writer.Write(Id);
            writer.PropertyName("entityIndex");
            writer.Write(EntityIndex);
            writer.PropertyName("locationName");
            writer.Write(LocationName ?? "Intersection");
            writer.PropertyName("severity");
            writer.Write(Severity ?? "Moderate");
            writer.PropertyName("measuredSpeedKph");
            writer.Write(MeasuredSpeedKph);
            writer.PropertyName("speedLimitKph");
            writer.Write(SpeedLimitKph);
            writer.PropertyName("stoppedRatioPercent");
            writer.Write(StoppedRatioPercent);
            writer.PropertyName("vehicleCount");
            writer.Write(VehicleCount);
            writer.PropertyName("queuePressurePercent");
            writer.Write(QueuePressurePercent);
            writer.PropertyName("inferredCause");
            writer.Write(InferredCause ?? "");
            writer.PropertyName("confidence");
            writer.Write(Confidence ?? "UNSPECIFIED");
            writer.PropertyName("flowPropagation");
            writer.Write(FlowPropagation ?? "");
            writer.PropertyName("posX");
            writer.Write(Position.x);
            writer.PropertyName("posY");
            writer.Write(Position.y);
            writer.PropertyName("posZ");
            writer.Write(Position.z);
            writer.TypeEnd();
        }
    }

    public struct TrafficSummaryData : IJsonWritable
    {
        public int CityHealthScore;
        public int ActiveVehicles;
        public float AverageCitySpeedKph;
        public float CongestionIndex;
        public int BottleneckCount;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(TrafficSummaryData));
            writer.PropertyName("cityHealthScore");
            writer.Write(CityHealthScore);
            writer.PropertyName("activeVehicles");
            writer.Write(ActiveVehicles);
            writer.PropertyName("averageCitySpeedKph");
            writer.Write(AverageCitySpeedKph);
            writer.PropertyName("congestionIndex");
            writer.Write(CongestionIndex);
            writer.PropertyName("bottleneckCount");
            writer.Write(BottleneckCount);
            writer.TypeEnd();
        }
    }

    // 2. TRANSIT DATA MODELS
    public struct TransitLineEntry : IJsonWritable
    {
        public int EntityIndex;
        public string Name;
        public string Category;
        public int StopCount;
        public int VehicleCount;
        public int Passengers;
        public int Waiting;
        public int Capacity;
        public float UtilizationPercent;
        public float AvgSpeedKph;
        public float HeadwayMinutes;
        public bool BunchingDetected;
        public float BunchingDeviation;
        public string WorstStopName;
        public float3 WorstStopPos;
        public string MeasuredEvidence;
        public string InferredCause;
        public string Confidence;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(TransitLineEntry));
            writer.PropertyName("entityIndex");
            writer.Write(EntityIndex);
            writer.PropertyName("name");
            writer.Write(Name ?? "Line");
            writer.PropertyName("category");
            writer.Write(Category ?? "Healthy");
            writer.PropertyName("stopCount");
            writer.Write(StopCount);
            writer.PropertyName("vehicleCount");
            writer.Write(VehicleCount);
            writer.PropertyName("passengers");
            writer.Write(Passengers);
            writer.PropertyName("waiting");
            writer.Write(Waiting);
            writer.PropertyName("capacity");
            writer.Write(Capacity);
            writer.PropertyName("utilizationPercent");
            writer.Write(UtilizationPercent);
            writer.PropertyName("avgSpeedKph");
            writer.Write(AvgSpeedKph);
            writer.PropertyName("headwayMinutes");
            writer.Write(HeadwayMinutes);
            writer.PropertyName("bunchingDetected");
            writer.Write(BunchingDetected);
            writer.PropertyName("bunchingDeviation");
            writer.Write(BunchingDeviation);
            writer.PropertyName("worstStopName");
            writer.Write(WorstStopName ?? "");
            writer.PropertyName("worstStopPosX");
            writer.Write(WorstStopPos.x);
            writer.PropertyName("worstStopPosY");
            writer.Write(WorstStopPos.y);
            writer.PropertyName("worstStopPosZ");
            writer.Write(WorstStopPos.z);
            writer.PropertyName("measuredEvidence");
            writer.Write(MeasuredEvidence ?? "");
            writer.PropertyName("inferredCause");
            writer.Write(InferredCause ?? "");
            writer.PropertyName("confidence");
            writer.Write(Confidence ?? "UNSPECIFIED");
            writer.TypeEnd();
        }
    }

    public struct TransitStopEntry : IJsonWritable
    {
        public int EntityIndex;
        public string Name;
        public int WaitingCount;
        public string LineName;
        public float3 Position;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(TransitStopEntry));
            writer.PropertyName("entityIndex");
            writer.Write(EntityIndex);
            writer.PropertyName("name");
            writer.Write(Name ?? "Stop");
            writer.PropertyName("waitingCount");
            writer.Write(WaitingCount);
            writer.PropertyName("lineName");
            writer.Write(LineName ?? "");
            writer.PropertyName("posX");
            writer.Write(Position.x);
            writer.PropertyName("posY");
            writer.Write(Position.y);
            writer.PropertyName("posZ");
            writer.Write(Position.z);
            writer.TypeEnd();
        }
    }

    public struct TransitSummaryData : IJsonWritable
    {
        public int TotalLines;
        public int ActiveVehicles;
        public int TotalPassengers;
        public int TotalWaiting;
        public float AvgUtilization;
        public int OvercrowdedCount;
        public int BunchingCount;
        public int HealthScore;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(TransitSummaryData));
            writer.PropertyName("totalLines");
            writer.Write(TotalLines);
            writer.PropertyName("activeVehicles");
            writer.Write(ActiveVehicles);
            writer.PropertyName("totalPassengers");
            writer.Write(TotalPassengers);
            writer.PropertyName("totalWaiting");
            writer.Write(TotalWaiting);
            writer.PropertyName("avgUtilization");
            writer.Write(AvgUtilization);
            writer.PropertyName("overcrowdedCount");
            writer.Write(OvercrowdedCount);
            writer.PropertyName("bunchingCount");
            writer.Write(BunchingCount);
            writer.PropertyName("healthScore");
            writer.Write(HealthScore);
            writer.TypeEnd();
        }
    }

    // 3. PARKING DATA MODELS
    public struct ParkingFacilityEntry : IJsonWritable
    {
        public int EntityIndex;
        public string Name;
        public int ParkedCars;
        public int Capacity;
        public float UtilizationPercent;
        public string Status;
        public string MeasuredBreakdown;
        public string InferredCause;
        public string Confidence;
        public float3 Position;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(ParkingFacilityEntry));
            writer.PropertyName("entityIndex");
            writer.Write(EntityIndex);
            writer.PropertyName("name");
            writer.Write(Name ?? "Parking Facility");
            writer.PropertyName("parkedCars");
            writer.Write(ParkedCars);
            writer.PropertyName("capacity");
            writer.Write(Capacity);
            writer.PropertyName("utilizationPercent");
            writer.Write(UtilizationPercent);
            writer.PropertyName("status");
            writer.Write(Status ?? "Normal");
            writer.PropertyName("measuredBreakdown");
            writer.Write(MeasuredBreakdown ?? "");
            writer.PropertyName("inferredCause");
            writer.Write(InferredCause ?? "");
            writer.PropertyName("confidence");
            writer.Write(Confidence ?? "UNSPECIFIED");
            writer.PropertyName("posX");
            writer.Write(Position.x);
            writer.PropertyName("posY");
            writer.Write(Position.y);
            writer.PropertyName("posZ");
            writer.Write(Position.z);
            writer.TypeEnd();
        }
    }

    public struct ParkingSummaryData : IJsonWritable
    {
        public int TotalOffStreetSpaces;
        public int TotalParkedCars;
        public float NetworkUtilizationPercent;
        public int FullFacilitiesCount;
        public int UnderutilizedCount;
        public int TotalFacilities;
        public int HealthScore;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(ParkingSummaryData));
            writer.PropertyName("totalOffStreetSpaces");
            writer.Write(TotalOffStreetSpaces);
            writer.PropertyName("totalParkedCars");
            writer.Write(TotalParkedCars);
            writer.PropertyName("networkUtilizationPercent");
            writer.Write(NetworkUtilizationPercent);
            writer.PropertyName("fullFacilitiesCount");
            writer.Write(FullFacilitiesCount);
            writer.PropertyName("underutilizedCount");
            writer.Write(UnderutilizedCount);
            writer.PropertyName("totalFacilities");
            writer.Write(TotalFacilities);
            writer.PropertyName("healthScore");
            writer.Write(HealthScore);
            writer.TypeEnd();
        }
    }

    // 4. SERVICES DATA MODELS
    public struct ServiceFacilityEntry : IJsonWritable
    {
        public int EntityIndex;
        public string Name;
        public string Type;
        public string Status;
        public float EfficiencyPercent;
        public string MeasuredBreakdown;
        public string InferredCause;
        public string Confidence;
        public float3 Position;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(ServiceFacilityEntry));
            writer.PropertyName("entityIndex");
            writer.Write(EntityIndex);
            writer.PropertyName("name");
            writer.Write(Name ?? "Service Facility");
            writer.PropertyName("type");
            writer.Write(Type ?? "General");
            writer.PropertyName("status");
            writer.Write(Status ?? "Optimal");
            writer.PropertyName("efficiencyPercent");
            writer.Write(EfficiencyPercent);
            writer.PropertyName("measuredBreakdown");
            writer.Write(MeasuredBreakdown ?? "");
            writer.PropertyName("inferredCause");
            writer.Write(InferredCause ?? "");
            writer.PropertyName("confidence");
            writer.Write(Confidence ?? "UNSPECIFIED");
            writer.PropertyName("posX");
            writer.Write(Position.x);
            writer.PropertyName("posY");
            writer.Write(Position.y);
            writer.PropertyName("posZ");
            writer.Write(Position.z);
            writer.TypeEnd();
        }
    }

    public struct ServiceSummaryData : IJsonWritable
    {
        public int TotalFacilities;
        public int CriticalCount;
        public int WarningCount;
        public int OptimalCount;
        public float AverageEfficiency;
        public int HealthScore;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(ServiceSummaryData));
            writer.PropertyName("totalFacilities");
            writer.Write(TotalFacilities);
            writer.PropertyName("criticalCount");
            writer.Write(CriticalCount);
            writer.PropertyName("warningCount");
            writer.Write(WarningCount);
            writer.PropertyName("optimalCount");
            writer.Write(OptimalCount);
            writer.PropertyName("averageEfficiency");
            writer.Write(AverageEfficiency);
            writer.PropertyName("healthScore");
            writer.Write(HealthScore);
            writer.TypeEnd();
        }
    }

    // 5. BUILDINGS DATA MODELS
    public struct SelectedBuildingDetail : IJsonWritable
    {
        public int EntityIndex;
        public string Name;
        public string Zone;
        public int HealthScore;
        public int Occupants;
        public int Workers;
        public int TotalJobs;
        public int Vacancies;
        public float EfficiencyPercent;
        public string PrimaryProblem;
        public string MeasuredBreakdown;
        public string InferredDiagnosis;
        public string Confidence;
        public bool IsAbandoned;
        public bool IsDamaged;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(SelectedBuildingDetail));
            writer.PropertyName("entityIndex");
            writer.Write(EntityIndex);
            writer.PropertyName("name");
            writer.Write(Name ?? "Selected Building");
            writer.PropertyName("zone");
            writer.Write(Zone ?? "Special");
            writer.PropertyName("healthScore");
            writer.Write(HealthScore);
            writer.PropertyName("occupants");
            writer.Write(Occupants);
            writer.PropertyName("workers");
            writer.Write(Workers);
            writer.PropertyName("totalJobs");
            writer.Write(TotalJobs);
            writer.PropertyName("vacancies");
            writer.Write(Vacancies);
            writer.PropertyName("efficiencyPercent");
            writer.Write(EfficiencyPercent);
            writer.PropertyName("primaryProblem");
            writer.Write(PrimaryProblem ?? "Optimal Operation");
            writer.PropertyName("measuredBreakdown");
            writer.Write(MeasuredBreakdown ?? "");
            writer.PropertyName("inferredDiagnosis");
            writer.Write(InferredDiagnosis ?? "");
            writer.PropertyName("confidence");
            writer.Write(Confidence ?? "UNSPECIFIED");
            writer.PropertyName("isAbandoned");
            writer.Write(IsAbandoned);
            writer.PropertyName("isDamaged");
            writer.Write(IsDamaged);
            writer.TypeEnd();
        }
    }

    public struct BuildingIssueEntry : IJsonWritable
    {
        public int EntityIndex;
        public string BuildingName;
        public string Zone;
        public string Severity;
        public string PrimaryIssue;
        public string MeasuredEvidence;
        public string InferredCause;
        public string Confidence;
        public int HealthScore;
        public float3 Position;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(BuildingIssueEntry));
            writer.PropertyName("entityIndex");
            writer.Write(EntityIndex);
            writer.PropertyName("buildingName");
            writer.Write(BuildingName ?? "Building");
            writer.PropertyName("zone");
            writer.Write(Zone ?? "Special");
            writer.PropertyName("severity");
            writer.Write(Severity ?? "Moderate");
            writer.PropertyName("primaryIssue");
            writer.Write(PrimaryIssue ?? "");
            writer.PropertyName("measuredEvidence");
            writer.Write(MeasuredEvidence ?? "");
            writer.PropertyName("inferredCause");
            writer.Write(InferredCause ?? "");
            writer.PropertyName("confidence");
            writer.Write(Confidence ?? "UNSPECIFIED");
            writer.PropertyName("healthScore");
            writer.Write(HealthScore);
            writer.PropertyName("posX");
            writer.Write(Position.x);
            writer.PropertyName("posY");
            writer.Write(Position.y);
            writer.PropertyName("posZ");
            writer.Write(Position.z);
            writer.TypeEnd();
        }
    }

    public struct BuildingSummaryData : IJsonWritable
    {
        public int TotalBuildingsScanned;
        public int TroubledCount;
        public int WorkerShortageCount;
        public int LowEfficiencyCount;
        public int AbandonedCount;
        public int HealthScore;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(BuildingSummaryData));
            writer.PropertyName("totalBuildingsScanned");
            writer.Write(TotalBuildingsScanned);
            writer.PropertyName("troubledCount");
            writer.Write(TroubledCount);
            writer.PropertyName("workerShortageCount");
            writer.Write(WorkerShortageCount);
            writer.PropertyName("lowEfficiencyCount");
            writer.Write(LowEfficiencyCount);
            writer.PropertyName("abandonedCount");
            writer.Write(AbandonedCount);
            writer.PropertyName("healthScore");
            writer.Write(HealthScore);
            writer.TypeEnd();
        }
    }

    // 6. NETWORK DATA MODELS
    public struct NetworkDefectEntry : IJsonWritable
    {
        public int Id;
        public int EntityIndex;
        public string Title;
        public string Severity;
        public string Category;
        public string WhatWeFound;
        public string WhyItMatters;
        public string Evidence;
        public string RepairStrategy;
        public bool IsRepairable;
        public string CurrentPreview;
        public string ProposedPreview;
        public float3 Position;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(NetworkDefectEntry));
            writer.PropertyName("id");
            writer.Write(Id);
            writer.PropertyName("entityIndex");
            writer.Write(EntityIndex);
            writer.PropertyName("title");
            writer.Write(Title ?? "Network Defect");
            writer.PropertyName("severity");
            writer.Write(Severity ?? "Warning");
            writer.PropertyName("category");
            writer.Write(Category ?? "Road");
            writer.PropertyName("whatWeFound");
            writer.Write(WhatWeFound ?? "");
            writer.PropertyName("whyItMatters");
            writer.Write(WhyItMatters ?? "");
            writer.PropertyName("evidence");
            writer.Write(Evidence ?? "");
            writer.PropertyName("repairStrategy");
            writer.Write(RepairStrategy ?? "Manual Alignment");
            writer.PropertyName("isRepairable");
            writer.Write(IsRepairable);
            writer.PropertyName("currentPreview");
            writer.Write(CurrentPreview ?? "");
            writer.PropertyName("proposedPreview");
            writer.Write(ProposedPreview ?? "");
            writer.PropertyName("posX");
            writer.Write(Position.x);
            writer.PropertyName("posY");
            writer.Write(Position.y);
            writer.PropertyName("posZ");
            writer.Write(Position.z);
            writer.TypeEnd();
        }
    }

    public struct NetworkSummaryData : IJsonWritable
    {
        public int TotalSegments;
        public int ScannedSegments;
        public int CriticalCount;
        public int ErrorCount;
        public int WarningCount;
        public int InfoCount;
        public int HealthScore;
        public string LastStatus;
        public int UndoCount;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(NetworkSummaryData));
            writer.PropertyName("totalSegments");
            writer.Write(TotalSegments);
            writer.PropertyName("scannedSegments");
            writer.Write(ScannedSegments);
            writer.PropertyName("criticalCount");
            writer.Write(CriticalCount);
            writer.PropertyName("errorCount");
            writer.Write(ErrorCount);
            writer.PropertyName("warningCount");
            writer.Write(WarningCount);
            writer.PropertyName("infoCount");
            writer.Write(InfoCount);
            writer.PropertyName("healthScore");
            writer.Write(HealthScore);
            writer.PropertyName("lastStatus");
            writer.Write(LastStatus ?? "Scanner Ready");
            writer.PropertyName("undoCount");
            writer.Write(UndoCount);
            writer.TypeEnd();
        }
    }

    // 7. TIMELINE & HISTORICAL METRICS DTO
    public struct CityTimelinePoint : IJsonWritable
    {
        public string TimeLabel;
        public int MasterHealth;
        public float AvgSpeedKph;
        public float StoppedRatioPercent;
        public int TotalProblemCount;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(CityTimelinePoint));
            writer.PropertyName("timeLabel");
            writer.Write(TimeLabel ?? "");
            writer.PropertyName("masterHealth");
            writer.Write(MasterHealth);
            writer.PropertyName("avgSpeedKph");
            writer.Write(AvgSpeedKph);
            writer.PropertyName("stoppedRatioPercent");
            writer.Write(StoppedRatioPercent);
            writer.PropertyName("totalProblemCount");
            writer.Write(TotalProblemCount);
            writer.TypeEnd();
        }
    }

    // 8. DISTRICT COMPARISON DTO
    public struct DistrictMetricEntry : IJsonWritable
    {
        public int DistrictIndex;
        public string DistrictName;
        public int TrafficHealth;
        public int TransitHealth;
        public int ServiceHealth;
        public int ParkingStressPercent;
        public int ProblemCount;

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(DistrictMetricEntry));
            writer.PropertyName("districtIndex");
            writer.Write(DistrictIndex);
            writer.PropertyName("districtName");
            writer.Write(DistrictName ?? "District");
            writer.PropertyName("trafficHealth");
            writer.Write(TrafficHealth);
            writer.PropertyName("transitHealth");
            writer.Write(TransitHealth);
            writer.PropertyName("serviceHealth");
            writer.Write(ServiceHealth);
            writer.PropertyName("parkingStressPercent");
            writer.Write(ParkingStressPercent);
            writer.PropertyName("problemCount");
            writer.Write(ProblemCount);
            writer.TypeEnd();
        }
    }

    // 9. BEFORE / AFTER COMPARISON DTO
    public struct BeforeAfterComparisonData : IJsonWritable
    {
        public bool HasBaseline;
        public int TargetEntityIndex;
        public string LocationName;
        public float BaselineSpeedKph;
        public float CurrentSpeedKph;
        public float SpeedDeltaPercent;
        public float BaselineStoppedRatio;
        public float CurrentStoppedRatio;
        public float StoppedRatioDeltaPercent;
        public float BaselineQueuePressure;
        public float CurrentQueuePressure;
        public float QueuePressureDeltaPercent;
        public string Verdict; // "IMPROVED", "UNCHANGED", "WORSE"

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(nameof(BeforeAfterComparisonData));
            writer.PropertyName("hasBaseline");
            writer.Write(HasBaseline);
            writer.PropertyName("targetEntityIndex");
            writer.Write(TargetEntityIndex);
            writer.PropertyName("locationName");
            writer.Write(LocationName ?? "Selected Location");
            writer.PropertyName("baselineSpeedKph");
            writer.Write(BaselineSpeedKph);
            writer.PropertyName("currentSpeedKph");
            writer.Write(CurrentSpeedKph);
            writer.PropertyName("speedDeltaPercent");
            writer.Write(SpeedDeltaPercent);
            writer.PropertyName("baselineStoppedRatio");
            writer.Write(BaselineStoppedRatio);
            writer.PropertyName("currentStoppedRatio");
            writer.Write(CurrentStoppedRatio);
            writer.PropertyName("stoppedRatioDeltaPercent");
            writer.Write(StoppedRatioDeltaPercent);
            writer.PropertyName("baselineQueuePressure");
            writer.Write(BaselineQueuePressure);
            writer.PropertyName("currentQueuePressure");
            writer.Write(CurrentQueuePressure);
            writer.PropertyName("queuePressureDeltaPercent");
            writer.Write(QueuePressureDeltaPercent);
            writer.PropertyName("verdict");
            writer.Write(Verdict ?? "UNCHANGED");
            writer.TypeEnd();
        }
    }
}
