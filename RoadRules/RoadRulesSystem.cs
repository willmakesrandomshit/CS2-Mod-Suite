using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Colossal.Logging;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using SubLane = Game.Net.SubLane;
using CarLane = Game.Net.CarLane;
using PedestrianLane = Game.Net.PedestrianLane;
using ParkingLane = Game.Net.ParkingLane;
using TrackLane = Game.Net.TrackLane;
using UtilityLane = Game.Net.UtilityLane;
using ConnectionLane = Game.Net.ConnectionLane;
using SecondaryLane = Game.Net.SecondaryLane;
using PrefabRef = Game.Prefabs.PrefabRef;
using NetLaneData = Game.Prefabs.NetLaneData;
using PathMethod = Game.Pathfind.PathMethod;

namespace RoadRules
{
    public struct EdgeCarriagewayKey : IEquatable<EdgeCarriagewayKey>
    {
        public Entity EdgeEntity;
        public bool IsInvert;
        public ushort CarriagewayGroup;

        public bool Equals(EdgeCarriagewayKey other)
        {
            return EdgeEntity.Equals(other.EdgeEntity) && IsInvert == other.IsInvert && CarriagewayGroup == other.CarriagewayGroup;
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeCarriagewayKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EdgeEntity.GetHashCode();
                hash = (hash * 397) ^ IsInvert.GetHashCode();
                hash = (hash * 397) ^ CarriagewayGroup.GetHashCode();
                return hash;
            }
        }
    }

    public partial class RoadRulesSystem : GameSystemBase
    {
        public static ILog Log = LogManager.GetLogger(nameof(RoadRules)).SetShowsErrorsInUI(false);
        public static RoadRulesSystem Instance { get; private set; }

        private ToolSystem m_ToolSystem;
        private Game.Prefabs.PrefabSystem m_PrefabSystem;

        // In-memory rule registry keyed by edge + carriageway direction
        private Dictionary<EdgeCarriagewayKey, EdgeRulesRecord> m_RuleRegistry = new Dictionary<EdgeCarriagewayKey, EdgeRulesRecord>();

        // Selected Edge & Active Inspection State
        public Entity SelectedEdge { get; private set; } = Entity.Null;
        public bool SelectedIsInvert { get; private set; } = false;
        public ushort SelectedCarriagewayGroup { get; private set; } = 0;
        public EdgeRulesRecord CurrentRecord { get; private set; }
        public int SelectedLaneIndex { get; private set; } = 0;

        // Copy / Paste Buffer
        public EdgeRulesRecord ClipboardRecord { get; private set; }
        public bool HasClipboard => ClipboardRecord != null && ClipboardRecord.PhysicalLanes.Count > 0;

        public string LastStatus { get; private set; } = "Road Rules ready — Select a road segment";

        // Diagnostic Telemetry
        public string LastRawHitString { get; private set; } = "None";
        public string LastResolvedEdgeString { get; private set; } = "None";
        public string LastFailureReason { get; private set; } = "None";
        public string LastRawComponentsString { get; private set; } = "None";
        public int LastTotalSubLanes { get; private set; } = 0;
        public int LastRawCarLanes { get; private set; } = 0;
        public int LastDirectionMatchedCarLanes { get; private set; } = 0;
        public int LastCarriagewayMatchedCarLanes { get; private set; } = 0;
        public int LastFilteredOutSubLanes { get; private set; } = 0;
        public string LastCarriagewayDirectionName { get; private set; } = "None";
        public string PhysicalLaneDebugSummary { get; private set; } = "None";
        public bool PropagationNoticeActive => m_PropagationFramesRemaining > 0;
        public string PropagationNotice { get; private set; } = string.Empty;
        private int m_PropagationFramesRemaining;
        private Entity m_LastObservedToolSelection = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
            Log.Info("RoadRulesSystem initialized.");
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            ClearSelection();
            m_RuleRegistry.Clear();
            ClipboardRecord = null;
            m_LastObservedToolSelection = Entity.Null;
        }

        protected override void OnUpdate()
        {
            if (World == null || !World.IsCreated || m_ToolSystem == null) return;

            if (m_PropagationFramesRemaining > 0)
            {
                m_PropagationFramesRemaining--;
                if (m_PropagationFramesRemaining == 0) PropagationNotice = string.Empty;
            }

            try
            {
                if (SelectedEdge != Entity.Null && (!EntityManager.Exists(SelectedEdge) || EntityManager.HasComponent<Deleted>(SelectedEdge)))
                {
                    ClearSelection();
                }

                Entity selected = m_ToolSystem.selected;
                if (selected != Entity.Null && selected != m_LastObservedToolSelection)
                {
                    m_LastObservedToolSelection = selected;
                    InspectEntity(selected, float3.zero);
                }
                else if (selected == Entity.Null)
                {
                    m_LastObservedToolSelection = Entity.Null;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RoadRulesSystem.OnUpdate: Exception during selection polling.");
            }
        }

        public void ClearSelection()
        {
            SelectedEdge = Entity.Null;
            SelectedIsInvert = false;
            SelectedCarriagewayGroup = 0;
            CurrentRecord = null;
            SelectedLaneIndex = 0;
            PhysicalLaneDebugSummary = "None";
            LastStatus = "Road Rules ready — Select a road segment";
        }

        public void InspectEntity(Entity rawHitEntity, float3 hitWorldPos)
        {
            if (rawHitEntity == Entity.Null || !EntityManager.Exists(rawHitEntity) || EntityManager.HasComponent<Deleted>(rawHitEntity))
            {
                return;
            }

            try
            {
                Entity resolvedEdge = ResolveToEdge(rawHitEntity, out Entity directHitLane);

                if (resolvedEdge == Entity.Null || !EntityManager.Exists(resolvedEdge) || EntityManager.HasComponent<Deleted>(resolvedEdge))
                {
                    Log.Debug($"[RoadRules] Clicked entity #{rawHitEntity.Index} could not be resolved to a valid Edge.");
                    return;
                }

                if (!EntityManager.HasComponent<Edge>(resolvedEdge) || !EntityManager.HasBuffer<SubLane>(resolvedEdge))
                {
                    Log.Debug($"[RoadRules] Resolved entity #{resolvedEdge.Index} has no SubLane buffer.");
                    return;
                }

                // Determine clicked carriageway direction and group
                DetermineClickedDirection(resolvedEdge, directHitLane, hitWorldPos, out bool isInvert, out ushort group, out string dirName);

                SelectedEdge = resolvedEdge;
                SelectedIsInvert = isInvert;
                SelectedCarriagewayGroup = group;
                SelectedLaneIndex = 0;
                LastCarriagewayDirectionName = dirName;

                var key = new EdgeCarriagewayKey
                {
                    EdgeEntity = resolvedEdge,
                    IsInvert = isInvert,
                    CarriagewayGroup = group
                };

                m_RuleRegistry.TryGetValue(key, out var previousRecord);
                var record = BuildDefaultRecordForCarriageway(resolvedEdge, isInvert, group, dirName);
                if (previousRecord?.PhysicalLanes != null && previousRecord.PhysicalLanes.Count > 0)
                {
                    if (previousRecord.PhysicalLanes.Count == record.PhysicalLanes.Count)
                    {
                        for (int i = 0; i < record.PhysicalLanes.Count; i++)
                            CopyRuleState(previousRecord.PhysicalLanes[i], record.PhysicalLanes[i]);
                    }
                    else
                    {
                        Log.Warn($"[RoadRules] TOPOLOGY CHANGED edge=#{resolvedEdge.Index} group={group}: oldPhysical={previousRecord.PhysicalLanes.Count}, newPhysical={record.PhysicalLanes.Count}. Ambiguous rules were cleared instead of remapped.");
                    }
                }
                m_RuleRegistry[key] = record;

                CurrentRecord = record;
                LastTotalSubLanes = record.TotalEdgeSubLanes;
                LastRawCarLanes = record.RawCarLaneCount;
                LastDirectionMatchedCarLanes = record.DirectionMatchedCarLaneCount;
                LastCarriagewayMatchedCarLanes = record.CarriagewayMatchedCarLaneCount;
                LastFilteredOutSubLanes = record.FilteredOutSubLanes;

                // Build physical lane diagnostic string
                var sb = new StringBuilder();
                sb.AppendLine($"Selected Edge: #{SelectedEdge.Index} | {dirName} | Group: {record.CarriagewayGroup}");
                sb.AppendLine($"Total SubLanes: {record.TotalEdgeSubLanes} | Raw CarLanes: {record.RawCarLaneCount} | Direction-Matched: {record.DirectionMatchedCarLaneCount} | Carriageway-Matched: {record.CarriagewayMatchedCarLaneCount}");
                sb.AppendLine($"Physical Lane Groups: {record.PhysicalLanes.Count} | Editable UI Lanes: {record.PhysicalLanes.Count}");
                for (int i = 0; i < record.PhysicalLanes.Count; i++)
                {
                    var pl = record.PhysicalLanes[i];
                    string entityList = string.Join(", ", pl.RoutingEntities.Select(e => $"#{e.Index}"));
                    sb.AppendLine($"Physical Lane #{i + 1} — {pl.LanePositionLabel}");
                    sb.AppendLine($"  Lateral Offset: {pl.LateralOffset:+0.00;-0.00;0.00}m");
                    sb.AppendLine($"  Canonical Entity: {FormatEntity(pl.CanonicalEntity)}");
                    sb.AppendLine($"  Routing Member Count: {pl.RoutingEntities.Count}");
                    sb.AppendLine($"  Routing Member IDs: [{entityList}]");
                    sb.AppendLine($"  Carriageway Group: {pl.CarriagewayGroup}");
                    sb.AppendLine($"  Master: {FormatEntity(pl.MasterEntity)}");
                    sb.AppendLine($"  Cross-section Index: {pl.CrossSectionIndex} (native sub-index {pl.NativeSubIndex})");
                }
                if (!string.IsNullOrWhiteSpace(record.CandidateDiagnostics))
                {
                    sb.AppendLine("Internal candidate audit:");
                    sb.Append(record.CandidateDiagnostics);
                }
                PhysicalLaneDebugSummary = sb.ToString();

                if (CurrentRecord.PhysicalLanes.Count > 0)
                {
                    LastStatus = $"Selected Road #{SelectedEdge.Index} ({dirName} — {CurrentRecord.PhysicalLanes.Count} Lanes)";
                }
                else
                {
                    LastStatus = $"Road #{SelectedEdge.Index} ({dirName}) has no editable traffic lanes";
                }

                Log.Info($"[RoadRules] SELECTED Road #{SelectedEdge.Index} [{dirName}, Group={group}]: Total SubLanes={record.TotalEdgeSubLanes}, Raw CarLanes={record.RawCarLaneCount}, Direction-Matched={record.DirectionMatchedCarLaneCount}, Carriageway-Matched={record.CarriagewayMatchedCarLaneCount}, Filtered Out={record.FilteredOutSubLanes}, Physical Lanes={CurrentRecord.PhysicalLanes.Count}");
                for (int i = 0; i < CurrentRecord.PhysicalLanes.Count; i++)
                {
                    var pl = CurrentRecord.PhysicalLanes[i];
                    string entityList = string.Join(", ", pl.RoutingEntities.Select(e => $"#{e.Index}"));
                    Log.Info($"   Physical Lane {i + 1} -> [{entityList}] ({pl.LanePositionLabel}, Offset={pl.LateralOffset:F2}m, Invert={pl.IsInvert})");
                }
                Log.Info("[RoadRules] PHYSICAL LANE CANDIDATE AUDIT\n" + record.CandidateDiagnostics.TrimEnd());
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[RoadRules] Road inspection failed for entity #{rawHitEntity.Index}");
                LastStatus = "Road selection error — Unsupported network entity";
            }
        }

        public Entity ResolveToEdge(Entity ent, out Entity directHitLane)
        {
            directHitLane = Entity.Null;

            if (ent == Entity.Null)
            {
                LastRawHitString = "Null";
                LastResolvedEdgeString = "None";
                LastFailureReason = "Raycast returned Entity.Null";
                LastRawComponentsString = "None";
                return Entity.Null;
            }

            if (!EntityManager.Exists(ent))
            {
                LastRawHitString = $"#{ent.Index}:{ent.Version} (Non-Existent)";
                LastResolvedEdgeString = "None";
                LastFailureReason = $"Entity #{ent.Index}:{ent.Version} does not exist in EntityManager";
                LastRawComponentsString = "None";
                return Entity.Null;
            }

            LastRawHitString = $"#{ent.Index}:{ent.Version}";

            // Capture raw entity components
            try
            {
                using (var compTypes = EntityManager.GetComponentTypes(ent, Allocator.Temp))
                {
                    var names = new List<string>(compTypes.Length);
                    for (int i = 0; i < compTypes.Length; i++)
                    {
                        var t = compTypes[i].GetManagedType();
                        names.Add(t != null ? t.Name : $"TypeIndex_{compTypes[i].TypeIndex}");
                    }
                    LastRawComponentsString = string.Join(", ", names);
                }
            }
            catch (Exception ex)
            {
                LastRawComponentsString = $"Error dumping components: {ex.Message}";
            }

            Entity current = ent;

            // 1. Unwrap Temp preview entity if present
            if (EntityManager.HasComponent<Temp>(current))
            {
                var temp = EntityManager.GetComponentData<Temp>(current);
                if (temp.m_Original != Entity.Null && EntityManager.Exists(temp.m_Original))
                {
                    current = temp.m_Original;
                }
            }

            // If raw hit entity is a lane, record it
            if (EntityManager.HasComponent<CarLane>(current) || EntityManager.HasComponent<Lane>(current))
            {
                directHitLane = current;
            }

            // 2. Direct match if it is an Edge
            if (EntityManager.HasComponent<Edge>(current))
            {
                if (!EntityManager.HasComponent<Deleted>(current))
                {
                    LastResolvedEdgeString = $"#{current.Index}:{current.Version}";
                    LastFailureReason = "None (Direct Edge match)";
                    return current;
                }
                else
                {
                    LastFailureReason = $"Direct Edge #{current.Index} is marked Deleted";
                    return Entity.Null;
                }
            }

            // 3. Unwind Owner chain (e.g. Lane -> SubLane -> Edge)
            int safetyDepth = 0;
            Entity search = current;
            while (safetyDepth < 10 && EntityManager.HasComponent<Owner>(search))
            {
                if (directHitLane == Entity.Null && (EntityManager.HasComponent<CarLane>(search) || EntityManager.HasComponent<Lane>(search)))
                {
                    directHitLane = search;
                }

                Entity owner = EntityManager.GetComponentData<Owner>(search).m_Owner;
                if (owner == Entity.Null || !EntityManager.Exists(owner) || owner == search) break;

                if (EntityManager.HasComponent<Temp>(owner))
                {
                    var temp = EntityManager.GetComponentData<Temp>(owner);
                    if (temp.m_Original != Entity.Null && EntityManager.Exists(temp.m_Original))
                    {
                        owner = temp.m_Original;
                    }
                }

                if (EntityManager.HasComponent<Edge>(owner))
                {
                    if (!EntityManager.HasComponent<Deleted>(owner))
                    {
                        LastResolvedEdgeString = $"#{owner.Index}:{owner.Version}";
                        LastFailureReason = $"None (Resolved via Owner chain at depth {safetyDepth + 1})";
                        return owner;
                    }
                    else
                    {
                        LastFailureReason = $"Owner Edge #{owner.Index} is marked Deleted";
                        return Entity.Null;
                    }
                }

                search = owner;
                safetyDepth++;
            }

            // 4. If Node was clicked, resolve to first valid connected edge
            if (EntityManager.HasComponent<Node>(current) && EntityManager.HasBuffer<ConnectedEdge>(current))
            {
                var connected = EntityManager.GetBuffer<ConnectedEdge>(current);
                for (int i = 0; i < connected.Length; i++)
                {
                    Entity edgeEnt = connected[i].m_Edge;
                    if (edgeEnt != Entity.Null && EntityManager.Exists(edgeEnt) && EntityManager.HasComponent<Edge>(edgeEnt) && !EntityManager.HasComponent<Deleted>(edgeEnt))
                    {
                        LastResolvedEdgeString = $"#{edgeEnt.Index}:{edgeEnt.Version}";
                        LastFailureReason = $"None (Resolved from Node connected edge #{i})";
                        return edgeEnt;
                    }
                }
            }

            LastResolvedEdgeString = "None";
            LastFailureReason = $"No Edge found in hierarchy for #{ent.Index}";
            return Entity.Null;
        }

        private void DetermineClickedDirection(Entity edgeEnt, Entity directHitLane, float3 hitWorldPos, out bool isInvert, out ushort group, out string dirName)
        {
            isInvert = false;
            group = 0;
            dirName = "Forward Carriageway";

            // If a specific CarLane was hit directly:
            if (directHitLane != Entity.Null && EntityManager.Exists(directHitLane) && EntityManager.HasComponent<CarLane>(directHitLane))
            {
                var carLane = EntityManager.GetComponentData<CarLane>(directHitLane);
                isInvert = (carLane.m_Flags & CarLaneFlags.Invert) != 0;
                group = carLane.m_CarriagewayGroup;
                dirName = isInvert ? "Opposing / Inverted Carriageway" : "Forward Carriageway";
                return;
            }

            // Otherwise, find the closest CarLane to the hitWorldPos
            if (EntityManager.HasBuffer<SubLane>(edgeEnt))
            {
                var subLanes = EntityManager.GetBuffer<SubLane>(edgeEnt);
                float minDistanceSq = float.MaxValue;
                bool found = false;

                for (int i = 0; i < subLanes.Length; i++)
                {
                    Entity laneEnt = subLanes[i].m_SubLane;
                    if (laneEnt == Entity.Null || !EntityManager.Exists(laneEnt) || !EntityManager.HasComponent<CarLane>(laneEnt)) continue;
                    if (EntityManager.HasComponent<PedestrianLane>(laneEnt) || EntityManager.HasComponent<ParkingLane>(laneEnt) || EntityManager.HasComponent<TrackLane>(laneEnt)) continue;

                    if (EntityManager.HasComponent<Curve>(laneEnt))
                    {
                        var curve = EntityManager.GetComponentData<Curve>(laneEnt);
                        float3 mid = 0.5f * (curve.m_Bezier.b + curve.m_Bezier.c);
                        float distSq = math.distancesq(hitWorldPos, mid);

                        if (distSq < minDistanceSq)
                        {
                            minDistanceSq = distSq;
                            var carLane = EntityManager.GetComponentData<CarLane>(laneEnt);
                            isInvert = (carLane.m_Flags & CarLaneFlags.Invert) != 0;
                            group = carLane.m_CarriagewayGroup;
                            dirName = isInvert ? "Opposing / Inverted Carriageway" : "Forward Carriageway";
                            found = true;
                        }
                    }
                }

                if (found) return;
            }

            // Default fallback
            isInvert = false;
            group = 0;
            dirName = "Forward Carriageway";
        }

        private class CandidateLaneInfo
        {
            public Entity Entity;
            public Entity OwnerEntity;
            public CarLane CarLaneData;
            public Curve CurveData;
            public float AvgLateralOffset;
            public float MinLateralOffset;
            public float MaxLateralOffset;
            public float CurveLength;
            public bool IsMaster;
            public bool IsSlave;
            public bool IsFullSlave;
            public bool IsPhysicalSeed;
            public Entity CanonicalEntity;
            public Entity MasterEntity;
            public ushort NativeSubIndex;
            public ushort NativeMinIndex;
            public ushort NativeMaxIndex;
            public int BufferIndex;
            public int PhysicalGroupIndex = -1;
            public float Width;
            public string Role;
            public string AssignmentReason;
            public string ComponentSummary;
            public string PrefabSummary;
            public Lane LaneData;
            public PathMethod PathMethods;
        }

        private EdgeRulesRecord BuildDefaultRecordForCarriageway(Entity edgeEnt, bool targetIsInvert, ushort targetGroup, string dirName)
        {
            var record = new EdgeRulesRecord
            {
                EdgeEntity = edgeEnt,
                IsInvert = targetIsInvert,
                CarriagewayGroup = targetGroup,
                CarriagewayName = dirName
            };

            if (edgeEnt == Entity.Null || !EntityManager.Exists(edgeEnt) || !EntityManager.HasBuffer<SubLane>(edgeEnt))
            {
                return record;
            }

            try
            {
                var subLanes = EntityManager.GetBuffer<SubLane>(edgeEnt);
                record.TotalEdgeSubLanes = subLanes.Length;

                // Step 1: Collect valid drivable CarLanes matching direction and group
                var candidates = new List<CandidateLaneInfo>();

                // Get edge curve geometry for lateral reference
                Curve edgeCurve = default;
                bool hasEdgeCurve = EntityManager.HasComponent<Curve>(edgeEnt);
                if (hasEdgeCurve)
                {
                    edgeCurve = EntityManager.GetComponentData<Curve>(edgeEnt);
                }

                float3 up = new float3(0, 1, 0);
                float[] sampleT = new float[] { 0.10f, 0.25f, 0.50f, 0.75f, 0.90f };

                for (int i = 0; i < subLanes.Length; i++)
                {
                    Entity laneEnt = subLanes[i].m_SubLane;
                    if (laneEnt == Entity.Null || !EntityManager.Exists(laneEnt))
                    {
                        record.FilteredOutSubLanes++;
                        continue;
                    }

                    // Strict exclusion of non-vehicle sublanes
                    if (!EntityManager.HasComponent<CarLane>(laneEnt) || !EntityManager.HasComponent<Curve>(laneEnt))
                    {
                        record.FilteredOutSubLanes++;
                        continue;
                    }

                    record.RawCarLaneCount++;

                    if (EntityManager.HasComponent<PedestrianLane>(laneEnt) ||
                        EntityManager.HasComponent<ParkingLane>(laneEnt) ||
                        EntityManager.HasComponent<TrackLane>(laneEnt) ||
                        EntityManager.HasComponent<UtilityLane>(laneEnt) ||
                        EntityManager.HasComponent<ConnectionLane>(laneEnt) ||
                        EntityManager.HasComponent<SecondaryLane>(laneEnt) ||
                        EntityManager.HasComponent<Temp>(laneEnt) ||
                        EntityManager.HasComponent<Deleted>(laneEnt))
                    {
                        record.FilteredOutSubLanes++;
                        continue;
                    }

                    var carLane = EntityManager.GetComponentData<CarLane>(laneEnt);
                    bool isLaneInvert = (carLane.m_Flags & CarLaneFlags.Invert) != 0;

                    // Filter by direction
                    if (isLaneInvert != targetIsInvert)
                    {
                        record.FilteredOutSubLanes++;
                        continue;
                    }

                    record.DirectionMatchedCarLaneCount++;

                    // CarriagewayGroup is the canonical CS2 cross-section boundary. Multiple
                    // same-direction carriageways can share one Edge (notably divided highways,
                    // bridges and transitions); exposing all of them created the 2→3 and 2→4 bugs.
                    if (carLane.m_CarriagewayGroup != targetGroup)
                    {
                        record.FilteredOutSubLanes++;
                        continue;
                    }
                    record.CarriagewayMatchedCarLaneCount++;

                    var laneCurve = EntityManager.GetComponentData<Curve>(laneEnt);
                    float len = MathUtils.Length(laneCurve.m_Bezier);
                    bool isMaster = EntityManager.HasComponent<MasterLane>(laneEnt);
                    bool isSlave = EntityManager.HasComponent<SlaveLane>(laneEnt);
                    bool isFullSlave = false;
                    Entity canonicalEntity = laneEnt;
                    Entity masterEntity = isMaster ? laneEnt : Entity.Null;
                    ushort nativeSubIndex = 0;
                    ushort nativeMinIndex = 0;
                    ushort nativeMaxIndex = 0;
                    string role = isMaster ? "shared carriageway master/controller" : "physical full-lane seed";
                    if (isSlave)
                    {
                        var slave = EntityManager.GetComponentData<SlaveLane>(laneEnt);
                        nativeSubIndex = slave.m_SubIndex;
                        nativeMinIndex = slave.m_MinIndex;
                        nativeMaxIndex = slave.m_MaxIndex;
                        isFullSlave = (slave.m_Flags & (SlaveLaneFlags.StartingLane | SlaveLaneFlags.EndingLane)) == 0;
                        if (slave.m_MasterIndex < subLanes.Length)
                        {
                            masterEntity = subLanes[slave.m_MasterIndex].m_SubLane;
                        }
                        // A CS2 MasterLane is a carriageway-level controller whose range can span
                        // several full slave lanes. It is deliberately NOT the physical-lane key.
                        // The full slave entity and its lateral center are the canonical strip.
                        canonicalEntity = laneEnt;
                        role = isFullSlave
                            ? "physical full-lane seed (slave of shared carriageway master)"
                            : "partial slave/helper representation";
                    }

                    float width = 0f;
                    string prefabSummary = "none";
                    if (EntityManager.HasComponent<PrefabRef>(laneEnt))
                    {
                        var prefab = EntityManager.GetComponentData<PrefabRef>(laneEnt).m_Prefab;
                        prefabSummary = $"#{prefab.Index}:{prefab.Version}";
                        if (m_PrefabSystem != null && m_PrefabSystem.TryGetPrefab<Game.Prefabs.PrefabBase>(prefab, out var prefabBase))
                            prefabSummary = $"{prefabBase.name} ({prefabSummary})";
                        if (prefab != Entity.Null && EntityManager.Exists(prefab) && EntityManager.HasComponent<NetLaneData>(prefab))
                            width = EntityManager.GetComponentData<NetLaneData>(prefab).m_Width;
                    }
                    Lane laneData = EntityManager.HasComponent<Lane>(laneEnt)
                        ? EntityManager.GetComponentData<Lane>(laneEnt)
                        : default;

                    // Sample lateral offset across road curve at t = 0.10, 0.25, 0.50, 0.75, 0.90
                    float totalOffset = 0f;
                    float minOffset = float.MaxValue;
                    float maxOffset = float.MinValue;
                    if (hasEdgeCurve)
                    {
                        for (int s = 0; s < sampleT.Length; s++)
                        {
                            float t = sampleT[s];
                            float3 roadPos = MathUtils.Position(edgeCurve.m_Bezier, t);
                            float3 roadTan = math.normalizesafe(MathUtils.Tangent(edgeCurve.m_Bezier, t), new float3(0, 0, 1));
                            float3 leftNormal = math.normalizesafe(math.cross(up, roadTan), new float3(1, 0, 0));

                            float3 lanePos = MathUtils.Position(laneCurve.m_Bezier, t);
                            float d = math.dot(lanePos - roadPos, leftNormal);
                            totalOffset += d;
                            minOffset = math.min(minOffset, d);
                            maxOffset = math.max(maxOffset, d);
                        }
                    }
                    else
                    {
                        float3 mid = 0.5f * (laneCurve.m_Bezier.b + laneCurve.m_Bezier.c);
                        totalOffset = mid.x * sampleT.Length;
                        minOffset = mid.x;
                        maxOffset = mid.x;
                    }

                    float avgOffset = totalOffset / sampleT.Length;

                    string relationship = $"Master={isMaster} Slave={isSlave}";
                    if (isMaster)
                    {
                        var master = EntityManager.GetComponentData<MasterLane>(laneEnt);
                        relationship += $" masterGroup={master.m_Group} range={master.m_MinIndex}-{master.m_MaxIndex} masterFlags={master.m_Flags}";
                    }
                    if (isSlave)
                    {
                        var slave = EntityManager.GetComponentData<SlaveLane>(laneEnt);
                        relationship += $" slaveGroup={slave.m_Group} masterIndex={slave.m_MasterIndex} subIndex={slave.m_SubIndex} range={slave.m_MinIndex}-{slave.m_MaxIndex} slaveFlags={slave.m_Flags}";
                    }

                    candidates.Add(new CandidateLaneInfo
                    {
                        Entity = laneEnt,
                        OwnerEntity = EntityManager.HasComponent<Owner>(laneEnt)
                            ? EntityManager.GetComponentData<Owner>(laneEnt).m_Owner
                            : edgeEnt,
                        CarLaneData = carLane,
                        CurveData = laneCurve,
                        AvgLateralOffset = avgOffset,
                        MinLateralOffset = minOffset,
                        MaxLateralOffset = maxOffset,
                        CurveLength = len,
                        IsMaster = isMaster,
                        IsSlave = isSlave,
                        IsFullSlave = isFullSlave,
                        IsPhysicalSeed = !isMaster && (!isSlave || isFullSlave),
                        CanonicalEntity = canonicalEntity,
                        MasterEntity = masterEntity,
                        NativeSubIndex = nativeSubIndex,
                        NativeMinIndex = nativeMinIndex,
                        NativeMaxIndex = nativeMaxIndex,
                        BufferIndex = i,
                        Width = width,
                        Role = role,
                        PrefabSummary = prefabSummary,
                        LaneData = laneData,
                        ComponentSummary = $"CarLane=True {relationship} Secondary=False Connection=False",
                        PathMethods = subLanes[i].m_PathMethods
                    });
                }

                // Step 2: discover PHYSICAL strips from the road cross-section. A MasterLane is
                // not a lane identity: CS2 intentionally assigns one MasterLane to a range of
                // full SlaveLanes (see SlaveLane.m_MinIndex/m_MaxIndex). The previous
                // sameCanonicalLane shortcut therefore collapsed every real lane in that range.
                // Full slave lanes (or ordinary non-master lanes) are the seeds; duplicate
                // representations only merge when their sampled lateral centerlines coincide.
                var clusters = new List<List<CandidateLaneInfo>>();
                var seedCandidates = candidates.Where(x => x.IsPhysicalSeed).ToList();
                if (seedCandidates.Count == 0)
                    seedCandidates = candidates.Where(x => !x.IsMaster).ToList();
                if (seedCandidates.Count == 0)
                    seedCandidates = candidates.ToList();

                foreach (var cand in seedCandidates)
                {
                    bool addedToCluster = false;
                    for (int c = 0; c < clusters.Count; c++)
                    {
                        var cluster = clusters[c];
                        float clusterAvgOffset = cluster.Average(x => x.AvgLateralOffset);
                        float tolerance = GetLateralMergeTolerance(cand, cluster);
                        if (math.abs(cand.AvgLateralOffset - clusterAvgOffset) <= tolerance)
                        {
                            cluster.Add(cand);
                            addedToCluster = true;
                            break;
                        }
                    }

                    if (!addedToCluster)
                    {
                        clusters.Add(new List<CandidateLaneInfo> { cand });
                    }
                }

                // Step 3: Sort clusters physically Left to Right in travel direction
                if (!targetIsInvert)
                {
                    // Forward: Leftmost lane has highest positive lateral offset along road left normal
                    clusters = clusters.OrderByDescending(c => c.Average(x => x.AvgLateralOffset)).ToList();
                }
                else
                {
                    // Inverted: Leftmost lane in travel direction has lowest lateral offset
                    clusters = clusters.OrderBy(c => c.Average(x => x.AvgLateralOffset)).ToList();
                }

                // Step 4: assign stable cross-section indices and create physical-lane records.
                // The shared MasterLane is only a member when this carriageway really has one
                // physical strip. On multi-lane carriageways it is carriageway-wide metadata;
                // applying a lane-scoped rule to it would leak that rule into the neighbour.
                int physicalCount = clusters.Count;
                for (int i = 0; i < physicalCount; i++)
                {
                    var cluster = clusters[i];
                    float physicalOffset = cluster.Average(x => x.AvgLateralOffset);
                    foreach (CandidateLaneInfo member in cluster)
                    {
                        member.PhysicalGroupIndex = i;
                        member.AssignmentReason =
                            $"physical seed at distinct cross-section center {physicalOffset:+0.00;-0.00;0.00}m";
                        record.RoutingAssignmentReasons[member.Entity] = member.AssignmentReason;
                    }

                    var routingEntities = cluster.Select(x => x.Entity).Distinct().ToList();

                    // The individual full-lane curve is what the overlay must highlight. Never
                    // prefer the group master that geometrically sits between several strips.
                    var primaryCand = cluster
                        .OrderByDescending(x => x.IsFullSlave)
                        .ThenByDescending(x => !x.IsMaster)
                        .ThenByDescending(x => x.CurveLength)
                        .First();

                    string posLabel = physicalCount switch
                    {
                        1 => "Single Lane",
                        2 => i == 0 ? "Left / Inner" : "Right / Outer",
                        3 => i == 0 ? "Left / Fast" : i == 1 ? "Middle" : "Right / Slow",
                        _ => i == 0 ? "Left / Inner" : i == physicalCount - 1 ? "Right / Outer" : $"Middle #{i + 1}"
                    };

                    record.PhysicalLanes.Add(PhysicalRoadLane.CreateDefault(
                        i,
                        routingEntities,
                        primaryCand.Entity,
                        primaryCand.CanonicalEntity,
                        primaryCand.MasterEntity,
                        primaryCand.CurveData,
                        physicalOffset,
                        i,
                        primaryCand.NativeSubIndex,
                        targetIsInvert,
                        targetGroup,
                        posLabel
                    ));
                }

                foreach (CandidateLaneInfo cand in candidates.Where(x => !seedCandidates.Contains(x)))
                {
                    int nearestIndex = FindClosestPhysicalLane(record.PhysicalLanes, cand.AvgLateralOffset, out float distance);
                    if (cand.IsMaster && physicalCount > 1)
                    {
                        cand.AssignmentReason =
                            $"shared carriageway master spans {physicalCount} physical lanes; excluded from lane-scoped propagation";
                        record.SharedRoutingEntities.Add(cand.Entity);
                        record.RoutingAssignmentReasons[cand.Entity] = cand.AssignmentReason;
                        continue;
                    }

                    if (nearestIndex < 0)
                    {
                        cand.AssignmentReason = "no physical cross-section seed was available";
                        record.RoutingAssignmentReasons[cand.Entity] = cand.AssignmentReason;
                        continue;
                    }

                    PhysicalRoadLane targetLane = record.PhysicalLanes[nearestIndex];
                    float attachTolerance = math.max(1.0f, cand.Width > 0.1f ? cand.Width * 0.55f : 1.5f);
                    if (!cand.IsMaster && distance > attachTolerance)
                    {
                        cand.AssignmentReason =
                            $"partial/helper center is {distance:0.00}m from nearest strip; excluded to avoid a phantom lane or cross-lane leak";
                        record.RoutingAssignmentReasons[cand.Entity] = cand.AssignmentReason;
                        continue;
                    }

                    cand.PhysicalGroupIndex = nearestIndex;
                    cand.AssignmentReason = cand.IsMaster
                        ? "single-lane carriageway master belongs exclusively to this strip"
                        : $"partial/helper representation attached to nearest cross-section center (delta {distance:0.00}m)";
                    if (!targetLane.RoutingEntities.Contains(cand.Entity))
                        targetLane.RoutingEntities.Add(cand.Entity);
                    record.RoutingAssignmentReasons[cand.Entity] = cand.AssignmentReason;
                }

                // Main edge lanes are only part of the routing topology. At junctions, merges,
                // ramps and side connections CS2 inserts separate CarLane entities owned by the
                // edge or its end nodes. A selective rule that does not cover those connectors can
                // be bypassed even though the visible asphalt lane carries the right flag.
                AttachConnectedRoutingEntities(edgeEnt, record);
                record.CandidateDiagnostics = BuildCandidateDiagnostics(edgeEnt, record, candidates, edgeCurve, hasEdgeCurve);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[RoadRules] Error filtering lanes from edge #{edgeEnt.Index}");
            }

            return record;
        }

        private void AttachConnectedRoutingEntities(Entity edgeEnt, EdgeRulesRecord record)
        {
            if (record?.PhysicalLanes == null || record.PhysicalLanes.Count == 0 ||
                edgeEnt == Entity.Null || !EntityManager.Exists(edgeEnt))
                return;

            var owners = new HashSet<Entity> { edgeEnt };
            if (EntityManager.HasComponent<Edge>(edgeEnt))
            {
                Edge edge = EntityManager.GetComponentData<Edge>(edgeEnt);
                if (edge.m_Start != Entity.Null && EntityManager.Exists(edge.m_Start)) owners.Add(edge.m_Start);
                if (edge.m_End != Entity.Null && EntityManager.Exists(edge.m_End)) owners.Add(edge.m_End);
            }

            var anchorsByLane = new List<List<Game.Pathfind.PathNode>>(record.PhysicalLanes.Count);
            var assignedLane = new Dictionary<Entity, int>();
            for (int laneIndex = 0; laneIndex < record.PhysicalLanes.Count; laneIndex++)
            {
                PhysicalRoadLane physicalLane = record.PhysicalLanes[laneIndex];
                var anchors = new List<Game.Pathfind.PathNode>();
                for (int i = 0; i < physicalLane.RoutingEntities.Count; i++)
                {
                    Entity laneEntity = physicalLane.RoutingEntities[i];
                    // A routing member may never belong to two neighboring physical strips in the
                    // same selected carriageway. That was a second route for cross-lane leakage.
                    if (!assignedLane.ContainsKey(laneEntity))
                        assignedLane[laneEntity] = laneIndex;
                    if (!EntityManager.Exists(laneEntity) || !EntityManager.HasComponent<Lane>(laneEntity)) continue;
                    Lane lane = EntityManager.GetComponentData<Lane>(laneEntity);
                    anchors.Add(lane.m_StartNode);
                    anchors.Add(lane.m_EndNode);
                }
                anchorsByLane.Add(anchors);
            }

            bool changed;
            int safetyPass = 0;
            do
            {
                changed = false;
                safetyPass++;
                foreach (Entity owner in owners)
                {
                    if (!EntityManager.HasBuffer<SubLane>(owner)) continue;
                    DynamicBuffer<SubLane> subLanes = EntityManager.GetBuffer<SubLane>(owner);
                    for (int i = 0; i < subLanes.Length; i++)
                    {
                        Entity candidate = subLanes[i].m_SubLane;
                        if (candidate == Entity.Null || assignedLane.ContainsKey(candidate) ||
                            record.SharedRoutingEntities.Contains(candidate) || !EntityManager.Exists(candidate) ||
                            !EntityManager.HasComponent<CarLane>(candidate) || !EntityManager.HasComponent<Lane>(candidate) ||
                            EntityManager.HasComponent<Deleted>(candidate) || EntityManager.HasComponent<Temp>(candidate))
                            continue;

                        // Only connector/sibling representations are attached here. Ordinary
                        // neighboring full physical lanes were already assigned from cross-section
                        // geometry and must remain independent.
                        bool isConnector = EntityManager.HasComponent<NodeLane>(candidate) ||
                                           EntityManager.HasComponent<ConnectionLane>(candidate) ||
                                           EntityManager.HasComponent<SecondaryLane>(candidate) ||
                                           EntityManager.HasComponent<SlaveLane>(candidate);
                        if (!isConnector) continue;

                        Lane connectorLane = EntityManager.GetComponentData<Lane>(candidate);
                        var matchingLanes = new List<int>();
                        for (int laneIndex = 0; laneIndex < anchorsByLane.Count; laneIndex++)
                        {
                            List<Game.Pathfind.PathNode> anchors = anchorsByLane[laneIndex];
                            if (MatchesAnyAnchor(connectorLane.m_StartNode, anchors) ||
                                MatchesAnyAnchor(connectorLane.m_EndNode, anchors))
                                matchingLanes.Add(laneIndex);
                        }
                        if (matchingLanes.Count == 0) continue;

                        int selectedLane = matchingLanes[0];
                        string reason;
                        if (matchingLanes.Count == 1)
                        {
                            reason = $"connector/helper topology matches only Physical Lane {selectedLane + 1}";
                        }
                        else
                        {
                            float offset = ComputeLateralOffset(edgeEnt, candidate, out _, out _);
                            float bestDistance = float.MaxValue;
                            int bestLane = -1;
                            float secondDistance = float.MaxValue;
                            foreach (int laneIndex in matchingLanes)
                            {
                                float distance = math.abs(offset - record.PhysicalLanes[laneIndex].LateralOffset);
                                if (distance < bestDistance)
                                {
                                    secondDistance = bestDistance;
                                    bestDistance = distance;
                                    bestLane = laneIndex;
                                }
                                else if (distance < secondDistance)
                                {
                                    secondDistance = distance;
                                }
                            }

                            // An exactly ambiguous helper is carriageway-wide. Do not inject a
                            // lane-scoped restriction into it and accidentally restrict a neighbour.
                            if (bestLane < 0 || math.abs(secondDistance - bestDistance) < 0.20f)
                            {
                                record.SharedRoutingEntities.Add(candidate);
                                record.RoutingAssignmentReasons[candidate] =
                                    $"shared connector matches Physical Lanes [{string.Join(", ", matchingLanes.Select(x => (x + 1).ToString()))}]; excluded from lane-scoped propagation";
                                continue;
                            }
                            selectedLane = bestLane;
                            reason =
                                $"connector matched multiple anchors; nearest cross-section center selected Physical Lane {selectedLane + 1} (delta {bestDistance:0.00}m)";
                        }

                        PhysicalRoadLane target = record.PhysicalLanes[selectedLane];
                        target.RoutingEntities.Add(candidate);
                        assignedLane[candidate] = selectedLane;
                        record.RoutingAssignmentReasons[candidate] = reason;
                        changed = true;

                        // Follow split connector segments through nodes internal to the same
                        // owner, but do not propagate onto another edge's visible full lanes.
                        if (connectorLane.m_StartNode.GetOwnerIndex() == owner.Index)
                            anchorsByLane[selectedLane].Add(connectorLane.m_StartNode);
                        if (connectorLane.m_EndNode.GetOwnerIndex() == owner.Index)
                            anchorsByLane[selectedLane].Add(connectorLane.m_EndNode);
                    }
                }
            }
            while (changed && safetyPass < 8);
        }

        private static bool MatchesAnyAnchor(Game.Pathfind.PathNode node, List<Game.Pathfind.PathNode> anchors)
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                if (node.EqualsIgnoreCurvePos(anchors[i])) return true;
            }
            return false;
        }

        private static float GetLateralMergeTolerance(CandidateLaneInfo candidate, List<CandidateLaneInfo> cluster)
        {
            float candidateWidth = candidate.Width > 0.1f ? candidate.Width : 3.5f;
            float clusterWidth = cluster.Count > 0
                ? cluster.Average(x => x.Width > 0.1f ? x.Width : candidateWidth)
                : candidateWidth;
            return PhysicalLaneCrossSection.GetMergeTolerance(candidateWidth, clusterWidth);
        }

        private static int FindClosestPhysicalLane(List<PhysicalRoadLane> lanes, float offset, out float distance)
        {
            int result = -1;
            distance = float.MaxValue;
            if (lanes == null) return result;
            for (int i = 0; i < lanes.Count; i++)
            {
                float candidateDistance = math.abs(offset - lanes[i].LateralOffset);
                if (candidateDistance < distance)
                {
                    distance = candidateDistance;
                    result = i;
                }
            }
            return result;
        }

        private float ComputeLateralOffset(Entity edgeEnt, Entity laneEnt, out float minOffset, out float maxOffset)
        {
            minOffset = 0f;
            maxOffset = 0f;
            if (edgeEnt == Entity.Null || laneEnt == Entity.Null ||
                !EntityManager.Exists(edgeEnt) || !EntityManager.Exists(laneEnt) ||
                !EntityManager.HasComponent<Curve>(laneEnt))
                return 0f;

            Curve laneCurve = EntityManager.GetComponentData<Curve>(laneEnt);
            if (!EntityManager.HasComponent<Curve>(edgeEnt))
            {
                float fallback = (0.5f * (laneCurve.m_Bezier.b + laneCurve.m_Bezier.c)).x;
                minOffset = fallback;
                maxOffset = fallback;
                return fallback;
            }

            Curve edgeCurve = EntityManager.GetComponentData<Curve>(edgeEnt);
            float3 up = new float3(0, 1, 0);
            float[] sampleT = { 0.10f, 0.25f, 0.50f, 0.75f, 0.90f };
            float total = 0f;
            minOffset = float.MaxValue;
            maxOffset = float.MinValue;
            for (int i = 0; i < sampleT.Length; i++)
            {
                float t = sampleT[i];
                float3 edgePosition = MathUtils.Position(edgeCurve.m_Bezier, t);
                float3 tangent = math.normalizesafe(MathUtils.Tangent(edgeCurve.m_Bezier, t), new float3(0, 0, 1));
                float3 left = math.normalizesafe(math.cross(up, tangent), new float3(1, 0, 0));
                float3 lanePosition = MathUtils.Position(laneCurve.m_Bezier, t);
                float offset = math.dot(lanePosition - edgePosition, left);
                total += offset;
                minOffset = math.min(minOffset, offset);
                maxOffset = math.max(maxOffset, offset);
            }
            return total / sampleT.Length;
        }

        private string BuildCandidateDiagnostics(
            Entity edgeEnt,
            EdgeRulesRecord record,
            List<CandidateLaneInfo> baseCandidates,
            Curve edgeCurve,
            bool hasEdgeCurve)
        {
            var audit = new StringBuilder();
            audit.AppendLine("MODEL: full-lane cross-section centers define physical lanes; MasterLane is carriageway-wide metadata, not a physical-lane key.");
            for (int i = 0; i < record.PhysicalLanes.Count; i++)
            {
                PhysicalRoadLane lane = record.PhysicalLanes[i];
                audit.AppendLine(
                    $"GROUP Physical Lane #{i + 1}: crossSectionIndex={lane.CrossSectionIndex} offset={lane.LateralOffset:+0.00;-0.00;0.00}m " +
                    $"canonical={FormatEntity(lane.CanonicalEntity)} master={FormatEntity(lane.MasterEntity)} nativeSubIndex={lane.NativeSubIndex} " +
                    $"routingMemberCount={lane.RoutingEntities.Count} routingMemberIDs=[{string.Join(", ", lane.RoutingEntities.Select(FormatEntity))}]");
            }
            if (record.SharedRoutingEntities.Count > 0)
            {
                audit.AppendLine(
                    $"SHARED/EXCLUDED carriageway-wide entities: [{string.Join(", ", record.SharedRoutingEntities.Distinct().Select(FormatEntity))}]");
            }

            var emitted = new HashSet<Entity>();
            for (int i = 0; i < baseCandidates.Count; i++)
            {
                CandidateLaneInfo candidate = baseCandidates[i];
                AppendRoutingEntityDiagnostics(audit, edgeEnt, record, candidate.Entity, candidate);
                emitted.Add(candidate.Entity);
            }

            for (int laneIndex = 0; laneIndex < record.PhysicalLanes.Count; laneIndex++)
            {
                foreach (Entity entity in record.PhysicalLanes[laneIndex].RoutingEntities)
                {
                    if (emitted.Add(entity))
                        AppendRoutingEntityDiagnostics(audit, edgeEnt, record, entity, null);
                }
            }
            foreach (Entity entity in record.SharedRoutingEntities)
            {
                if (emitted.Add(entity))
                    AppendRoutingEntityDiagnostics(audit, edgeEnt, record, entity, null);
            }
            return audit.ToString();
        }

        private void AppendRoutingEntityDiagnostics(
            StringBuilder audit,
            Entity edgeEnt,
            EdgeRulesRecord record,
            Entity entity,
            CandidateLaneInfo known)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
            {
                audit.AppendLine($"ENTITY {FormatEntity(entity)} no longer exists");
                return;
            }

            Entity owner = known?.OwnerEntity ?? (EntityManager.HasComponent<Owner>(entity)
                ? EntityManager.GetComponentData<Owner>(entity).m_Owner
                : Entity.Null);
            bool hasCarLane = EntityManager.HasComponent<CarLane>(entity);
            CarLane carLane = hasCarLane ? EntityManager.GetComponentData<CarLane>(entity) : default;
            bool isMaster = EntityManager.HasComponent<MasterLane>(entity);
            bool isSlave = EntityManager.HasComponent<SlaveLane>(entity);
            MasterLane master = isMaster ? EntityManager.GetComponentData<MasterLane>(entity) : default;
            SlaveLane slave = isSlave ? EntityManager.GetComponentData<SlaveLane>(entity) : default;
            bool isFullSlave = isSlave && (slave.m_Flags & (SlaveLaneFlags.StartingLane | SlaveLaneFlags.EndingLane)) == 0;
            Entity masterEntity = known?.MasterEntity ?? Entity.Null;
            if (isMaster) masterEntity = entity;
            if (isSlave && masterEntity == Entity.Null && owner != Entity.Null && EntityManager.Exists(owner) && EntityManager.HasBuffer<SubLane>(owner))
            {
                DynamicBuffer<SubLane> ownerLanes = EntityManager.GetBuffer<SubLane>(owner);
                if (slave.m_MasterIndex < ownerLanes.Length)
                    masterEntity = ownerLanes[slave.m_MasterIndex].m_SubLane;
            }

            Lane lane = EntityManager.HasComponent<Lane>(entity)
                ? EntityManager.GetComponentData<Lane>(entity)
                : default;
            bool hasCurve = EntityManager.HasComponent<Curve>(entity);
            Curve curve = hasCurve ? EntityManager.GetComponentData<Curve>(entity) : default;
            float offset = known?.AvgLateralOffset ?? ComputeLateralOffset(edgeEnt, entity, out _, out _);
            float minOffset;
            float maxOffset;
            if (known != null)
            {
                minOffset = known.MinLateralOffset;
                maxOffset = known.MaxLateralOffset;
            }
            else
            {
                offset = ComputeLateralOffset(edgeEnt, entity, out minOffset, out maxOffset);
            }

            string prefabSummary = known?.PrefabSummary ?? "none";
            float width = known?.Width ?? 0f;
            if (known == null && EntityManager.HasComponent<PrefabRef>(entity))
            {
                Entity prefab = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
                prefabSummary = FormatEntity(prefab);
                if (m_PrefabSystem != null && m_PrefabSystem.TryGetPrefab<Game.Prefabs.PrefabBase>(prefab, out var prefabBase))
                    prefabSummary = $"{prefabBase.name} ({prefabSummary})";
                if (prefab != Entity.Null && EntityManager.Exists(prefab) && EntityManager.HasComponent<NetLaneData>(prefab))
                    width = EntityManager.GetComponentData<NetLaneData>(prefab).m_Width;
            }

            PathMethod pathMethods = known?.PathMethods ?? FindPathMethods(owner, entity);
            int physicalGroup = -1;
            for (int i = 0; i < record.PhysicalLanes.Count; i++)
            {
                if (record.PhysicalLanes[i].RoutingEntities.Contains(entity))
                {
                    physicalGroup = i;
                    break;
                }
            }
            string assignment = physicalGroup >= 0 ? $"Physical Lane {physicalGroup + 1}" : "SHARED/EXCLUDED";
            string reason = record.RoutingAssignmentReasons.TryGetValue(entity, out string value)
                ? value
                : known?.AssignmentReason ?? "not assigned";
            bool helper = isMaster || EntityManager.HasComponent<NodeLane>(entity) ||
                          EntityManager.HasComponent<ConnectionLane>(entity) ||
                          EntityManager.HasComponent<SecondaryLane>(entity) ||
                          (isSlave && !isFullSlave);
            string relationship = isMaster
                ? $"Master group={master.m_Group} range={master.m_MinIndex}-{master.m_MaxIndex}"
                : isSlave
                    ? $"Slave group={slave.m_Group} master={FormatEntity(masterEntity)} subIndex={slave.m_SubIndex} range={slave.m_MinIndex}-{slave.m_MaxIndex} flags={slave.m_Flags}"
                    : "independent";
            string curveSummary = hasCurve
                ? $"a={FormatFloat3(curve.m_Bezier.a)} b={FormatFloat3(curve.m_Bezier.b)} c={FormatFloat3(curve.m_Bezier.c)} d={FormatFloat3(curve.m_Bezier.d)}"
                : "none";
            string laneNodes = EntityManager.HasComponent<Lane>(entity)
                ? $"start={FormatPathNode(lane.m_StartNode)} middle={FormatPathNode(lane.m_MiddleNode)} end={FormatPathNode(lane.m_EndNode)}"
                : "none";
            string direction = hasCarLane && (carLane.m_Flags & CarLaneFlags.Invert) != 0 ? "reverse/invert" : "forward";
            ushort group = hasCarLane ? carLane.m_CarriagewayGroup : (ushort)0;

            audit.AppendLine(
                $"ENTITY {FormatEntity(entity)} prefab={prefabSummary} owner={FormatEntity(owner)} carriagewayGroup={group} relationship=[{relationship}] " +
                $"laneIndex(start/middle/end)={lane.m_StartNode.GetLaneIndex()}/{lane.m_MiddleNode.GetLaneIndex()}/{lane.m_EndNode.GetLaneIndex()} " +
                $"lateralOffset(avg/min/max)={offset:+0.00;-0.00;0.00}/{minOffset:+0.00;-0.00;0.00}/{maxOffset:+0.00;-0.00;0.00}m width={width:0.00}m " +
                $"curve=[{curveSummary}] connected=[{laneNodes}] laneFlags={(hasCarLane ? carLane.m_Flags.ToString() : "none")} " +
                $"vehicle/pathType=Car/{pathMethods} direction={direction} helper/internal={helper} assignment={assignment} reason=\"{reason}\"");
        }

        private PathMethod FindPathMethods(Entity owner, Entity laneEntity)
        {
            if (owner == Entity.Null || !EntityManager.Exists(owner) || !EntityManager.HasBuffer<SubLane>(owner))
                return (PathMethod)0;
            DynamicBuffer<SubLane> lanes = EntityManager.GetBuffer<SubLane>(owner);
            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i].m_SubLane == laneEntity)
                    return lanes[i].m_PathMethods;
            }
            return (PathMethod)0;
        }

        private static string FormatEntity(Entity entity) =>
            entity == Entity.Null ? "null" : $"#{entity.Index}:{entity.Version}";

        public void SelectLane(int index)
        {
            if (CurrentRecord != null && index >= 0 && index < CurrentRecord.PhysicalLanes.Count)
            {
                SelectedLaneIndex = index;
            }
        }

        private static void CopyRuleState(PhysicalRoadLane source, PhysicalRoadLane destination)
        {
            destination.AllowedVehicles = source.AllowedVehicles;
            destination.LocalAccessOnly = source.LocalAccessOnly;
            destination.IsClosed = source.IsClosed;
            destination.CustomSpeedLimitKph = source.CustomSpeedLimitKph;
            destination.Preset = source.Preset;
        }

        public void ToggleVehicleAccess(VehicleAccessFlags flag)
        {
            if (CurrentRecord == null || SelectedLaneIndex >= CurrentRecord.PhysicalLanes.Count) return;

            var lane = CurrentRecord.PhysicalLanes[SelectedLaneIndex];
            SetAllowedVehicles((int)(lane.AllowedVehicles ^ flag));
        }

        public void ApplyPreset(LaneRulePreset preset)
        {
            if (CurrentRecord == null || SelectedLaneIndex >= CurrentRecord.PhysicalLanes.Count) return;
            if (!IsSupportedPreset(preset)) return;

            var lane = CurrentRecord.PhysicalLanes[SelectedLaneIndex];
            lane.Preset = preset;

            switch (preset)
            {
                case LaneRulePreset.DefaultAll:
                    lane.AllowedVehicles = VehicleAccessFlags.All;
                    lane.LocalAccessOnly = false;
                    lane.IsClosed = false;
                    break;
                case LaneRulePreset.TruckBan:
                    lane.AllowedVehicles = VehicleAccessFlags.All & ~VehicleAccessFlags.Trucks;
                    lane.LocalAccessOnly = false;
                    lane.IsClosed = false;
                    break;
                case LaneRulePreset.TransitOnly:
                    lane.AllowedVehicles = RoadRuleTranslator.PublicLaneUsers;
                    lane.LocalAccessOnly = false;
                    lane.IsClosed = false;
                    break;
                case LaneRulePreset.LocalAccessOnly:
                    lane.AllowedVehicles = VehicleAccessFlags.All;
                    lane.LocalAccessOnly = true;
                    lane.IsClosed = false;
                    break;
                case LaneRulePreset.EmergencyAndServiceOnly:
                    LastStatus = "Emergency & Service Only is unsupported: CS2 groups buses, taxis, emergency and service access on public lanes.";
                    return;
                case LaneRulePreset.LaneClosed:
                    lane.AllowedVehicles = VehicleAccessFlags.None;
                    lane.LocalAccessOnly = false;
                    lane.IsClosed = true;
                    break;
            }

            ApplyRuleToSimulation(lane);
            LastStatus = $"Applied preset '{preset}' to Physical Lane {SelectedLaneIndex + 1} ({lane.LanePositionLabel})";
        }

        public void ApplyPresetToAllLanes(LaneRulePreset preset)
        {
            if (CurrentRecord == null) return;
            if (!IsSupportedPreset(preset)) return;

            int originalSelection = SelectedLaneIndex;
            for (int i = 0; i < CurrentRecord.PhysicalLanes.Count; i++)
            {
                SelectedLaneIndex = i;
                ApplyPreset(preset);
            }
            SelectedLaneIndex = math.clamp(originalSelection, 0, math.max(0, CurrentRecord.PhysicalLanes.Count - 1));
            LastStatus = $"Applied preset '{preset}' to ALL {CurrentRecord.PhysicalLanes.Count} physical lanes of {CurrentRecord.CarriagewayName}";
        }

        private bool IsSupportedPreset(LaneRulePreset preset)
        {
            if (!Enum.IsDefined(typeof(LaneRulePreset), preset) || preset == LaneRulePreset.EmergencyAndServiceOnly || preset == LaneRulePreset.Custom)
            {
                LastStatus = "Unsupported preset. Use All Allowed, No Heavy Traffic, Public/Service Lane, Local Bias or Close Lane.";
                return false;
            }
            return true;
        }

        public void SetAllowedVehicles(int bitmask)
        {
            if (CurrentRecord == null || SelectedLaneIndex >= CurrentRecord.PhysicalLanes.Count) return;

            var lane = CurrentRecord.PhysicalLanes[SelectedLaneIndex];
            VehicleAccessFlags requested = (VehicleAccessFlags)bitmask & VehicleAccessFlags.All;
            if (!RoadRuleTranslator.TryAccept(requested, requested == VehicleAccessFlags.None, false, out RoadRuleTranslation translation))
            {
                LastStatus = "Unsupported class combination — " + translation.Description;
                return;
            }

            lane.AllowedVehicles = translation.EffectiveVehicles;
            lane.IsClosed = translation.HardClosed;
            lane.LocalAccessOnly = false;
            lane.Preset = LaneRulePreset.Custom;

            ApplyRuleToSimulation(lane);
            LastStatus = $"Physical Lane {SelectedLaneIndex + 1}: {translation.Description}";
        }

        public void SetLocalAccess(bool isLocal)
        {
            if (CurrentRecord == null || SelectedLaneIndex >= CurrentRecord.PhysicalLanes.Count) return;

            var lane = CurrentRecord.PhysicalLanes[SelectedLaneIndex];
            lane.LocalAccessOnly = isLocal;
            if (isLocal)
            {
                lane.AllowedVehicles = VehicleAccessFlags.All;
                lane.IsClosed = false;
            }
            lane.Preset = LaneRulePreset.Custom;

            ApplyRuleToSimulation(lane);
            LastStatus = isLocal
                ? $"Physical Lane {SelectedLaneIndex + 1}: experimental local route bias enabled"
                : $"Physical Lane {SelectedLaneIndex + 1}: local route bias removed";
        }

        public void SetClosed(bool isClosed)
        {
            if (CurrentRecord == null || SelectedLaneIndex >= CurrentRecord.PhysicalLanes.Count) return;

            var lane = CurrentRecord.PhysicalLanes[SelectedLaneIndex];
            lane.SetClosed(isClosed);

            ApplyRuleToSimulation(lane);
            LastStatus = isClosed ? $"Physical Lane {SelectedLaneIndex + 1} is now CLOSED" : $"Physical Lane {SelectedLaneIndex + 1} REOPENED";
        }

        public void ResetLane()
        {
            if (CurrentRecord == null || SelectedLaneIndex >= CurrentRecord.PhysicalLanes.Count) return;

            var lane = CurrentRecord.PhysicalLanes[SelectedLaneIndex];
            lane.AllowedVehicles = VehicleAccessFlags.All;
            lane.LocalAccessOnly = false;
            lane.IsClosed = false;
            lane.Preset = LaneRulePreset.DefaultAll;
            lane.CustomSpeedLimitKph = 0f;

            ApplyRuleToSimulation(lane);
            LastStatus = $"Reset Physical Lane {SelectedLaneIndex + 1} to vanilla defaults";
        }

        public void CopyRules()
        {
            if (CurrentRecord == null) return;
            ClipboardRecord = CurrentRecord.Clone();
            LastStatus = $"Copied rules from {CurrentRecord.CarriagewayName} ({ClipboardRecord.PhysicalLanes.Count} physical lanes)";
        }

        public void PasteRules()
        {
            if (CurrentRecord == null || !HasClipboard) return;

            if (CurrentRecord.PhysicalLanes.Count != ClipboardRecord.PhysicalLanes.Count)
            {
                LastStatus = $"Paste rejected safely: source has {ClipboardRecord.PhysicalLanes.Count} physical lanes, destination has {CurrentRecord.PhysicalLanes.Count}.";
                return;
            }

            int count = CurrentRecord.PhysicalLanes.Count;
            for (int i = 0; i < count; i++)
            {
                var src = ClipboardRecord.PhysicalLanes[i];
                var dst = CurrentRecord.PhysicalLanes[i];

                dst.AllowedVehicles = src.AllowedVehicles;
                dst.LocalAccessOnly = src.LocalAccessOnly;
                dst.IsClosed = src.IsClosed;
                dst.CustomSpeedLimitKph = src.CustomSpeedLimitKph;
                dst.Preset = src.Preset;

                ApplyRuleToSimulation(dst);
            }

            LastStatus = $"Pasted rules to {count} physical lanes of {CurrentRecord.CarriagewayName}";
        }

        private void ApplyRuleToSimulation(PhysicalRoadLane lane)
        {
            RoadRulesPathfindSystem.Instance?.EnforcePhysicalLane(lane);
            NotifyPathfindUpdate(lane);
            m_PropagationFramesRemaining = 300;
            PropagationNotice = "Rerouting traffic… New paths receive the rule now; vehicles with existing routes may take a short time to clear.";
        }

        public void NotifyPathfindUpdate(PhysicalRoadLane lane)
        {
            if (lane != null && lane.RoutingEntities != null)
            {
                for (int i = 0; i < lane.RoutingEntities.Count; i++)
                {
                    Entity ent = lane.RoutingEntities[i];
                    if (ent != Entity.Null && EntityManager.Exists(ent))
                    {
                        if (!EntityManager.HasComponent<PathfindUpdated>(ent))
                        {
                            EntityManager.AddComponent<PathfindUpdated>(ent);
                        }
                        if (!EntityManager.HasComponent<Updated>(ent))
                        {
                            EntityManager.AddComponent<Updated>(ent);
                        }
                    }
                }
            }

            if (SelectedEdge != Entity.Null && EntityManager.Exists(SelectedEdge))
            {
                if (!EntityManager.HasComponent<PathfindUpdated>(SelectedEdge))
                {
                    EntityManager.AddComponent<PathfindUpdated>(SelectedEdge);
                }
                if (!EntityManager.HasComponent<Updated>(SelectedEdge))
                {
                    EntityManager.AddComponent<Updated>(SelectedEdge);
                }
            }
        }

        public void SetStatus(string msg)
        {
            LastStatus = msg;
        }

        public IReadOnlyDictionary<EdgeCarriagewayKey, EdgeRulesRecord> GetAllRules() => m_RuleRegistry;

        /// <summary>
        /// Connector lanes may belong to two independently edited physical lanes. The strictest
        /// effective state wins so resetting one road cannot silently reopen a connector still
        /// governed by another active rule.
        /// </summary>
        public PhysicalRoadLane ResolveEffectiveRule(Entity routingEntity, PhysicalRoadLane fallback)
        {
            var effective = new PhysicalRoadLane
            {
                AllowedVehicles = VehicleAccessFlags.All,
                LocalAccessOnly = false,
                IsClosed = false,
                CustomSpeedLimitKph = 0f,
                Preset = LaneRulePreset.DefaultAll
            };
            bool found = false;

            foreach (var pair in m_RuleRegistry)
            {
                EdgeRulesRecord record = pair.Value;
                if (record?.PhysicalLanes == null) continue;
                for (int i = 0; i < record.PhysicalLanes.Count; i++)
                {
                    PhysicalRoadLane lane = record.PhysicalLanes[i];
                    if (lane?.RoutingEntities == null || !lane.RoutingEntities.Contains(routingEntity)) continue;

                    found = true;
                    effective.AllowedVehicles &= lane.AllowedVehicles;
                    effective.LocalAccessOnly |= lane.LocalAccessOnly;
                    effective.IsClosed |= lane.IsClosed;
                    if (lane.CustomSpeedLimitKph > 0f &&
                        (effective.CustomSpeedLimitKph <= 0f || lane.CustomSpeedLimitKph < effective.CustomSpeedLimitKph))
                        effective.CustomSpeedLimitKph = lane.CustomSpeedLimitKph;
                }
            }

            if (!found && fallback != null)
            {
                effective.AllowedVehicles = fallback.AllowedVehicles;
                effective.LocalAccessOnly = fallback.LocalAccessOnly;
                effective.IsClosed = fallback.IsClosed;
                effective.CustomSpeedLimitKph = fallback.CustomSpeedLimitKph;
                effective.Preset = fallback.Preset;
            }

            return effective;
        }

        protected override void OnDestroy()
        {
            Instance = null;
            base.OnDestroy();
        }

        private static string FormatFloat3(float3 value) => $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";
        private static string FormatPathNode(Game.Pathfind.PathNode node) =>
            $"owner#{node.GetOwnerIndex()}/lane{node.GetLaneIndex()}/t{node.GetCurvePos():0.000}/secondary={node.IsSecondary()}";
    }
}
