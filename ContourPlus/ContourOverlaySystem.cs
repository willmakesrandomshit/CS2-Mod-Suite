using System;
using System.Collections.Generic;
using System.Diagnostics;
using Colossal.Logging;
using Colossal.Mathematics;
using Game;
using Game.Common;
using Game.Rendering;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ContourPlus
{
    public struct ContourLineSegment
    {
        public Line3.Segment Line;
        public float Elevation;
        public bool IsMajor;
    }

    /// <summary>
    /// Real-time camera-aware topographic contour line generation and rendering pipeline.
    /// Samples live terrain elevation and extracts true equal-elevation isolines using Marching Squares.
    /// Renders overlays directly through CS2 OverlayRenderSystem.
    /// </summary>
    public partial class ContourOverlaySystem : GameSystemBase
    {
        public static ILog Log = LogManager.GetLogger(nameof(ContourPlus)).SetShowsErrorsInUI(false);
        public static ContourOverlaySystem Instance { get; private set; }

        private TerrainSystem m_TerrainSystem;
        private OverlayRenderSystem m_OverlayRenderSystem;

        // User Configuration
        public bool ContoursEnabled { get; set; } = false;
        public float IntervalMeters { get; set; } = 5f;
        public bool HighVisibilityMode { get; set; } = false;

        // Visual Styling
        private static readonly Color kMinorContourColor = new Color(0.96f, 0.78f, 0.22f, 0.88f); // Golden amber
        private static readonly Color kMajorContourColor = new Color(1.00f, 0.46f, 0.08f, 0.98f); // Vivid deep orange
        private static readonly Color kMinorHighVisColor = new Color(1.00f, 0.90f, 0.20f, 1.00f);
        private static readonly Color kMajorHighVisColor = new Color(1.00f, 0.30f, 0.00f, 1.00f);

        private const float kMinorLineWidth = 1.8f;
        private const float kMajorLineWidth = 3.2f;
        private const float kHighVisMinorWidth = 2.8f;
        private const float kHighVisMajorWidth = 4.8f;
        private const float kVerticalTerrainOffset = 0.35f; // Prevents z-fighting with terrain geometry

        // Throttling and Camera Caching
        private Vector3 m_LastCameraPos = Vector3.zero;
        private float m_LastCameraHeight = 0f;
        private float m_LastInterval = 10f;
        private bool m_LastEnabled = false;
        private float m_RegenTimer = 0f;
        private const float kMinRegenInterval = 0.5f; // Main-thread terrain work: cap at 2Hz
        private const int kMaximumContourLevels = 256;
        private const int kMaximumCachedSegments = 100000;

        // Cached Contour Geometry
        private readonly List<ContourLineSegment> m_CachedMinorSegments = new List<ContourLineSegment>();
        private readonly List<ContourLineSegment> m_CachedMajorSegments = new List<ContourLineSegment>();

        // Live Diagnostic Metrics
        public float TerrainMinElevation { get; private set; } = 0f;
        public float TerrainMaxElevation { get; private set; } = 0f;
        public int ContourLevelsCount { get; private set; } = 0;
        public int GeneratedSegmentsCount { get; private set; } = 0;
        public int RenderedSegmentsCount { get; private set; } = 0;
        public float LastGenerationTimeMs { get; private set; } = 0f;
        public int SampledPointsCount { get; private set; } = 0;

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;

            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            SyncSettings();

            Log.Info("[ContourPlus] ContourOverlaySystem initialized with Marching Squares isoline engine.");
        }

        public void SetContoursEnabled(bool enabled)
        {
            if (ContoursEnabled != enabled)
            {
                ContoursEnabled = enabled;
                if (Mod.SettingInstance != null)
                {
                    Mod.SettingInstance.ContoursEnabled = enabled;
                    Mod.SettingInstance.ApplyAndSave();
                }
                ForceRegenerate();
                Log.Info($"[ContourPlus] ContoursEnabled set to: {enabled}");
            }
        }

        public void SetInterval(float interval)
        {
            if (interval < 1f) interval = 1f;
            if (Math.Abs(IntervalMeters - interval) > 0.01f)
            {
                IntervalMeters = interval;
                if (Mod.SettingInstance != null)
                {
                    Mod.SettingInstance.Interval = (ContourPlusSetting.ContourInterval)(int)Math.Round(interval);
                    Mod.SettingInstance.ApplyAndSave();
                }
                ForceRegenerate();
                Log.Info($"[ContourPlus] Contour Interval set to: {interval}m");
            }
        }

        public void ToggleHighVisibility()
        {
            HighVisibilityMode = !HighVisibilityMode;
            if (Mod.SettingInstance != null)
            {
                Mod.SettingInstance.HighVisibility = HighVisibilityMode;
                Mod.SettingInstance.ApplyAndSave();
            }
            Log.Info($"[ContourPlus] HighVisibilityMode set to: {HighVisibilityMode}");
        }

        public void ForceRegenerate()
        {
            m_LastCameraPos = new Vector3(float.MinValue, 0f, 0f);
        }

        protected override void OnUpdate()
        {
            SyncSettings();
            if (!ContoursEnabled)
            {
                if (m_CachedMinorSegments.Count > 0 || m_CachedMajorSegments.Count > 0)
                {
                    m_CachedMinorSegments.Clear();
                    m_CachedMajorSegments.Clear();
                    RenderedSegmentsCount = 0;
                    GeneratedSegmentsCount = 0;
                }
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            m_RegenTimer += UnityEngine.Time.deltaTime;

            // 1. Check if contour regeneration is needed
            Vector3 camPos = cam.transform.position;
            float camDist = Vector3.Distance(camPos, m_LastCameraPos);
            float heightDelta = Mathf.Abs(camPos.y - m_LastCameraHeight);
            bool intervalChanged = Math.Abs(IntervalMeters - m_LastInterval) > 0.01f;
            bool enabledChanged = (ContoursEnabled != m_LastEnabled);

            bool needsRegen = enabledChanged || intervalChanged ||
                              (m_RegenTimer >= kMinRegenInterval && (camDist > 50f || heightDelta > 20f || m_CachedMinorSegments.Count == 0));

            if (needsRegen)
            {
                m_RegenTimer = 0f;
                m_LastCameraPos = camPos;
                m_LastCameraHeight = camPos.y;
                m_LastInterval = IntervalMeters;
                m_LastEnabled = ContoursEnabled;

                RegenerateContours(cam);
            }

            // 2. Submit cached contour lines to OverlayRenderSystem
            RenderContourOverlays();
        }

        private void RegenerateContours(Camera cam)
        {
            var sw = Stopwatch.StartNew();

            m_CachedMinorSegments.Clear();
            m_CachedMajorSegments.Clear();

            if (m_TerrainSystem == null)
            {
                sw.Stop();
                LastGenerationTimeMs = (float)sw.Elapsed.TotalMilliseconds;
                return;
            }

            // Find camera ground focus point
            Vector3 groundFocus = GetCameraGroundFocus(cam);

            // Compute adaptive sampling radius and grid resolution based on altitude
            float altitude = math.max(100f, cam.transform.position.y - groundFocus.y);
            float sampleRadius = math.clamp(altitude * 2.2f, 400f, 1800f);
            int gridResolution = sampleRadius > 1100f ? 100 : (sampleRadius > 700f ? 90 : 80);

            float minX = groundFocus.x - sampleRadius;
            float maxX = groundFocus.x + sampleRadius;
            float minZ = groundFocus.z - sampleRadius;
            float maxZ = groundFocus.z + sampleRadius;
            float step = (sampleRadius * 2f) / (gridResolution - 1);

            // Fetch terrain height data safely
            TerrainHeightData heightData;
            try
            {
                heightData = m_TerrainSystem.GetHeightData(waitForPending: false);
            }
            catch (Exception ex)
            {
                Log.Warn($"[ContourPlus] GetHeightData exception: {ex.Message}");
                sw.Stop();
                return;
            }

            float[,] heightGrid = new float[gridResolution, gridResolution];
            float minH = float.MaxValue;
            float maxH = float.MinValue;
            int validSamples = 0;

            for (int z = 0; z < gridResolution; z++)
            {
                float worldZ = minZ + z * step;
                for (int x = 0; x < gridResolution; x++)
                {
                    float worldX = minX + x * step;
                    float3 worldPos = new float3(worldX, 0f, worldZ);
                    float h = TerrainUtils.SampleHeight(ref heightData, worldPos);

                    if (float.IsNaN(h) || float.IsInfinity(h)) h = 0f;

                    heightGrid[x, z] = h;
                    if (h < minH) minH = h;
                    if (h > maxH) maxH = h;
                    validSamples++;
                }
            }

            SampledPointsCount = validSamples;
            TerrainMinElevation = (float)Math.Round(minH, 1);
            TerrainMaxElevation = (float)Math.Round(maxH, 1);

            if (minH >= maxH || validSamples == 0)
            {
                sw.Stop();
                LastGenerationTimeMs = (float)sw.Elapsed.TotalMilliseconds;
                return;
            }

            // Extract Marching Squares Contours
            float interval = IntervalMeters > 0.5f ? IntervalMeters : 10f;
            int requestedLevels = (int)math.floor((maxH - minH) / interval) + 1;
            if (requestedLevels > kMaximumContourLevels)
            {
                // Preserve the selected spacing when practical, but keep pathological maps
                // and 1 m mode from multiplying hundreds of levels by the full sample grid.
                interval *= math.ceil((float)requestedLevels / kMaximumContourLevels);
            }
            float majorMultiplier = interval <= 5f ? 5f : (interval <= 10f ? 5f : (interval <= 25f ? 2f : 2f));

            float startLevel = Mathf.Ceil(minH / interval) * interval;
            float endLevel = Mathf.Floor(maxH / interval) * interval;
            int levelsCount = 0;

            for (float level = startLevel; level <= endLevel + 0.001f; level += interval)
            {
                levelsCount++;
                bool isMajor = Mathf.Abs(level % (interval * majorMultiplier)) < 0.01f || Mathf.Abs(level % 50f) < 0.01f || Mathf.Abs(level % 100f) < 0.01f;

                for (int z = 0; z < gridResolution - 1; z++)
                {
                    for (int x = 0; x < gridResolution - 1; x++)
                    {
                        float h0 = heightGrid[x, z];
                        float h1 = heightGrid[x + 1, z];
                        float h2 = heightGrid[x + 1, z + 1];
                        float h3 = heightGrid[x, z + 1];

                        float cellMin = Mathf.Min(Mathf.Min(h0, h1), Mathf.Min(h2, h3));
                        float cellMax = Mathf.Max(Mathf.Max(h0, h1), Mathf.Max(h2, h3));
                        if (level < cellMin || level > cellMax) continue;

                        int caseIndex = 0;
                        if (h0 >= level) caseIndex |= 1;
                        if (h1 >= level) caseIndex |= 2;
                        if (h2 >= level) caseIndex |= 4;
                        if (h3 >= level) caseIndex |= 8;

                        if (caseIndex == 0 || caseIndex == 15) continue;

                        float wx0 = minX + x * step;
                        float wx1 = minX + (x + 1) * step;
                        float wz0 = minZ + z * step;
                        float wz1 = minZ + (z + 1) * step;

                        float renderElev = level + kVerticalTerrainOffset;

                        float t0 = Mathf.Approximately(h1, h0) ? 0.5f : math.clamp((level - h0) / (h1 - h0), 0f, 1f);
                        float t1 = Mathf.Approximately(h2, h1) ? 0.5f : math.clamp((level - h1) / (h2 - h1), 0f, 1f);
                        float t2 = Mathf.Approximately(h2, h3) ? 0.5f : math.clamp((level - h3) / (h2 - h3), 0f, 1f);
                        float t3 = Mathf.Approximately(h3, h0) ? 0.5f : math.clamp((level - h0) / (h3 - h0), 0f, 1f);

                        float3 p0 = new float3(math.lerp(wx0, wx1, t0), renderElev, wz0);
                        float3 p1 = new float3(wx1, renderElev, math.lerp(wz0, wz1, t1));
                        float3 p2 = new float3(math.lerp(wx0, wx1, t2), renderElev, wz1);
                        float3 p3 = new float3(wx0, renderElev, math.lerp(wz0, wz1, t3));

                        AddMarchingSquaresSegments(caseIndex, p0, p1, p2, p3, h0, h1, h2, h3, level, isMajor);
                    }
                }
            }

            ContourLevelsCount = levelsCount;
            GeneratedSegmentsCount = m_CachedMinorSegments.Count + m_CachedMajorSegments.Count;

            sw.Stop();
            LastGenerationTimeMs = (float)sw.Elapsed.TotalMilliseconds;
        }

        private void AddMarchingSquaresSegments(
            int caseIndex,
            float3 p0, float3 p1, float3 p2, float3 p3,
            float h0, float h1, float h2, float h3,
            float level, bool isMajor)
        {
            switch (caseIndex)
            {
                case 1:
                case 14:
                    AddSegment(p3, p0, level, isMajor);
                    break;
                case 2:
                case 13:
                    AddSegment(p0, p1, level, isMajor);
                    break;
                case 3:
                case 12:
                    AddSegment(p3, p1, level, isMajor);
                    break;
                case 4:
                case 11:
                    AddSegment(p1, p2, level, isMajor);
                    break;
                case 5:
                    float avgCenter5 = (h0 + h1 + h2 + h3) * 0.25f;
                    if (avgCenter5 >= level)
                    {
                        AddSegment(p3, p2, level, isMajor);
                        AddSegment(p0, p1, level, isMajor);
                    }
                    else
                    {
                        AddSegment(p3, p0, level, isMajor);
                        AddSegment(p1, p2, level, isMajor);
                    }
                    break;
                case 6:
                case 9:
                    AddSegment(p0, p2, level, isMajor);
                    break;
                case 7:
                case 8:
                    AddSegment(p2, p3, level, isMajor);
                    break;
                case 10:
                    float avgCenter10 = (h0 + h1 + h2 + h3) * 0.25f;
                    if (avgCenter10 >= level)
                    {
                        AddSegment(p0, p3, level, isMajor);
                        AddSegment(p1, p2, level, isMajor);
                    }
                    else
                    {
                        AddSegment(p0, p1, level, isMajor);
                        AddSegment(p2, p3, level, isMajor);
                    }
                    break;
            }
        }

        private void AddSegment(float3 start, float3 end, float elevation, bool isMajor)
        {
            if (m_CachedMinorSegments.Count + m_CachedMajorSegments.Count >= kMaximumCachedSegments)
                return;
            var item = new ContourLineSegment
            {
                Line = new Line3.Segment(start, end),
                Elevation = elevation,
                IsMajor = isMajor
            };

            if (isMajor) m_CachedMajorSegments.Add(item);
            else m_CachedMinorSegments.Add(item);
        }

        private void RenderContourOverlays()
        {
            int total = m_CachedMinorSegments.Count + m_CachedMajorSegments.Count;
            RenderedSegmentsCount = total;
            if (total == 0 || m_OverlayRenderSystem == null) return;

            var buffer = m_OverlayRenderSystem.GetBuffer(out var dependencies);
            dependencies.Complete();

            Color minorCol = HighVisibilityMode ? kMinorHighVisColor : kMinorContourColor;
            Color majorCol = HighVisibilityMode ? kMajorHighVisColor : kMajorContourColor;
            float minorWidth = HighVisibilityMode ? kHighVisMinorWidth : kMinorLineWidth;
            float majorWidth = HighVisibilityMode ? kHighVisMajorWidth : kMajorLineWidth;

            // Draw minor contours
            for (int i = 0; i < m_CachedMinorSegments.Count; i++)
            {
                buffer.DrawLine(minorCol, m_CachedMinorSegments[i].Line, minorWidth, true);
            }

            // Draw major contours (thicker, brighter)
            for (int i = 0; i < m_CachedMajorSegments.Count; i++)
            {
                buffer.DrawLine(majorCol, m_CachedMajorSegments[i].Line, majorWidth, true);
            }
        }

        private Vector3 GetCameraGroundFocus(Camera cam)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter) && enter < 5000f)
            {
                return ray.GetPoint(enter);
            }
            return cam.transform.position + cam.transform.forward * 400f;
        }

        protected override void OnDestroy()
        {
            Instance = null;
            m_CachedMinorSegments.Clear();
            m_CachedMajorSegments.Clear();
            base.OnDestroy();
        }

        private void SyncSettings()
        {
            var settings = Mod.SettingInstance;
            if (settings == null) return;

            bool regenerate = false;
            bool enabled = settings.ContoursEnabled;
            float interval = (float)settings.Interval;
            if (ContoursEnabled != enabled)
            {
                ContoursEnabled = enabled;
                regenerate = true;
            }
            if (Math.Abs(IntervalMeters - interval) > 0.01f)
            {
                IntervalMeters = interval;
                regenerate = true;
            }
            HighVisibilityMode = settings.HighVisibility;
            if (regenerate) ForceRegenerate();
        }
    }
}
