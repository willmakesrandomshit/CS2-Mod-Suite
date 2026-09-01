using System;
using System.Collections.Generic;
using Colossal.Logging;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ContourPlus
{
    public struct ProfileSample
    {
        public float Distance;
        public float Elevation;
        public float TerrainElevation;
    }

    public struct RoadPlanningGrade
    {
        public bool HasRoad;
        public string RoadName;
        public float StartElevation;
        public float EndElevation;
        public float ElevationDelta;
        public float LengthMeters;
        public float AverageGradePercent;
        public float MaxGradePercent;
        public string GuidanceCategory; // Good (0-4%), Moderate (4-8%), Steep (8-12%), Very Steep (>12%)
        public float EstimatedCutM3;
        public float EstimatedFillM3;
        public List<ProfileSample> LongitudinalProfile;
    }

    public partial class TopographySystem : GameSystemBase
    {
        public static ILog Log = LogManager.GetLogger(nameof(ContourPlus)).SetShowsErrorsInUI(false);
        public static TopographySystem Instance { get; private set; }

        private ToolSystem m_ToolSystem;
        private TerrainSystem m_TerrainSystem;

        private float m_SampleTimer = 0f;
        private const float kSampleInterval = 0.06f;

        public float CursorElevation { get; private set; } = 0f;
        public float CursorSlopePercent { get; private set; } = 0f;
        public float CursorSlopeDegrees { get; private set; } = 0f;
        public string CursorSlopeCategory { get; private set; } = "-";

        public RoadPlanningGrade ActiveRoadGrade { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            ActiveRoadGrade = new RoadPlanningGrade
            {
                LongitudinalProfile = new List<ProfileSample>()
            };

            Log.Info("Contour Plus 1.2.5-beta.1 Topography & Road Grade Engine initialized.");
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
        }

        protected override void OnUpdate()
        {
            if (!ContourPlusUISystem.IsPanelOpen) return;
            m_SampleTimer += UnityEngine.Time.deltaTime;
            if (m_SampleTimer < kSampleInterval) return;
            m_SampleTimer = 0f;

            if (Mod.SettingInstance == null || Mod.SettingInstance.EnableElevationReadout)
                UpdateCursorElevation();
            else
            {
                CursorElevation = 0f;
                CursorSlopePercent = 0f;
                CursorSlopeDegrees = 0f;
                CursorSlopeCategory = "Disabled in Settings";
            }

            if (Mod.SettingInstance == null || Mod.SettingInstance.ShowRoadGradeInspector)
                UpdateRoadPlanningGrade();
            else if (ActiveRoadGrade.HasRoad)
                ActiveRoadGrade = new RoadPlanningGrade { LongitudinalProfile = new List<ProfileSample>() };
        }

        private void UpdateCursorElevation()
        {
            try
            {
                Camera cam = Camera.main;
                if (cam == null || m_TerrainSystem == null) return;

                Ray ray = cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPos = ray.GetPoint(enter);

                    TerrainHeightData heightData = m_TerrainSystem.GetHeightData(waitForPending: false);
                    float elev = TerrainUtils.SampleHeight(ref heightData, new float3(hitPos.x, 0f, hitPos.z));

                    CursorElevation = (float)Math.Round(elev, 1);

                    // Compute true local slope gradient from orthogonal terrain height delta samples
                    float delta = 8f;
                    float hCenter = elev;
                    float hX = TerrainUtils.SampleHeight(ref heightData, new float3(hitPos.x + delta, 0f, hitPos.z));
                    float hZ = TerrainUtils.SampleHeight(ref heightData, new float3(hitPos.x, 0f, hitPos.z + delta));

                    float slopeX = (hX - hCenter) / delta;
                    float slopeZ = (hZ - hCenter) / delta;
                    float slopeMag = Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ);

                    float slopePct = slopeMag * 100f;
                    CursorSlopePercent = (float)Math.Round(math.clamp(slopePct, 0.0f, 99.9f), 1);
                    CursorSlopeDegrees = (float)Math.Round(Mathf.Atan(slopeMag) * Mathf.Rad2Deg, 1);

                    if (CursorSlopePercent < 3f) CursorSlopeCategory = "Flat (<3%)";
                    else if (CursorSlopePercent < 8f) CursorSlopeCategory = "Gentle (3-8%)";
                    else if (CursorSlopePercent < 15f) CursorSlopeCategory = "Moderate (8-15%)";
                    else if (CursorSlopePercent < 25f) CursorSlopeCategory = "Steep (15-25%)";
                    else CursorSlopeCategory = "Very Steep (>25%)";
                }
            }
            catch { }
        }

        private void UpdateRoadPlanningGrade()
        {
            Entity selected = m_ToolSystem.selected;
            if (selected == Entity.Null || !EntityManager.Exists(selected) || EntityManager.HasComponent<Deleted>(selected))
            {
                if (ActiveRoadGrade.HasRoad)
                {
                    ActiveRoadGrade = new RoadPlanningGrade
                    {
                        HasRoad = false,
                        LongitudinalProfile = new List<ProfileSample>()
                    };
                }
                return;
            }

            Entity roadEntity = selected;
            if (!EntityManager.HasComponent<Curve>(roadEntity))
            {
                if (EntityManager.HasComponent<Edge>(roadEntity))
                {
                    // edge entity
                }
                else
                {
                    return;
                }
            }

            if (EntityManager.HasComponent<Curve>(roadEntity) && m_TerrainSystem != null)
            {
                Curve curve = EntityManager.GetComponentData<Curve>(roadEntity);
                float length = math.max(1f, curve.m_Length);
                float startY = curve.m_Bezier.a.y;
                float endY = curve.m_Bezier.d.y;
                float deltaY = endY - startY;
                float avgGrade = math.abs(deltaY / length) * 100f;

                TerrainHeightData heightData = m_TerrainSystem.GetHeightData(waitForPending: false);

                // Build longitudinal elevation profile
                var profile = new List<ProfileSample>();
                int samplePoints = 16;
                float totalCut = 0f;
                float totalFill = 0f;
                float roadWidth = 14f; // typical 2-4 lane width

                float maxSegmentGrade = avgGrade;

                float3 prevPos = curve.m_Bezier.a;
                for (int i = 0; i <= samplePoints; i++)
                {
                    float t = (float)i / samplePoints;
                    float3 pos = MathUtils.Position(curve.m_Bezier, t);
                    float dist = length * t;
                    float roadElev = pos.y;
                    float terrainElev = TerrainUtils.SampleHeight(ref heightData, pos);

                    if (i > 0)
                    {
                        float stepDist = math.distance(pos, prevPos);
                        float stepDeltaY = math.abs(pos.y - prevPos.y);
                        if (stepDist > 0.1f)
                        {
                            float segGrade = (stepDeltaY / stepDist) * 100f;
                            if (segGrade > maxSegmentGrade) maxSegmentGrade = segGrade;
                        }
                    }
                    prevPos = pos;

                    float diff = roadElev - terrainElev;
                    if (diff > 0) totalFill += diff * (length / samplePoints) * roadWidth;
                    else totalCut += math.abs(diff) * (length / samplePoints) * roadWidth;

                    profile.Add(new ProfileSample
                    {
                        Distance = (float)Math.Round(dist, 1),
                        Elevation = (float)Math.Round(roadElev, 2),
                        TerrainElevation = (float)Math.Round(terrainElev, 2)
                    });
                }

                string guidance = avgGrade <= 4.0f ? "Good (0-4%)"
                    : (avgGrade <= 8.0f ? "Moderate (4-8%)"
                    : (avgGrade <= 12.0f ? "Steep (8-12%)" : "Very Steep (>12%)"));


                ActiveRoadGrade = new RoadPlanningGrade
                {
                    HasRoad = true,
                    RoadName = $"Segment #{roadEntity.Index}",
                    StartElevation = (float)Math.Round(startY, 1),
                    EndElevation = (float)Math.Round(endY, 1),
                    ElevationDelta = (float)Math.Round(deltaY, 1),
                    LengthMeters = (float)Math.Round(length, 1),
                    AverageGradePercent = (float)Math.Round(avgGrade, 1),
                    MaxGradePercent = (float)Math.Round(maxSegmentGrade, 1),
                    GuidanceCategory = guidance,
                    EstimatedCutM3 = (float)Math.Round(totalCut, 0),
                    EstimatedFillM3 = (float)Math.Round(totalFill, 0),
                    LongitudinalProfile = profile
                };
            }
        }

        protected override void OnDestroy()
        {
            Instance = null;
            base.OnDestroy();
        }
    }
}
