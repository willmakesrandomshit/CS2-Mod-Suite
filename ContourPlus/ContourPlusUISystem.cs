using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.UI;

namespace ContourPlus
{
    public partial class ContourPlusUISystem : UISystemBase
    {
        public static bool IsPanelOpen { get; private set; }
        // Cursor Telemetry
        private ValueBinding<float> m_ElevationBinding;
        private ValueBinding<float> m_SlopePercentBinding;
        private ValueBinding<float> m_SlopeDegreesBinding;
        private ValueBinding<string> m_SlopeCategoryBinding;
        private ValueBinding<string> m_SlopeUnitBinding;

        // Road Planning Grade Telemetry
        private ValueBinding<bool> m_HasRoadBinding;
        private ValueBinding<string> m_RoadNameBinding;
        private ValueBinding<float> m_StartElevationBinding;
        private ValueBinding<float> m_EndElevationBinding;
        private ValueBinding<float> m_ElevationDeltaBinding;
        private ValueBinding<float> m_LengthBinding;
        private ValueBinding<float> m_AverageGradeBinding;
        private ValueBinding<float> m_MaxGradeBinding;
        private ValueBinding<string> m_GuidanceCategoryBinding;
        private ValueBinding<float> m_CutVolumeBinding;
        private ValueBinding<float> m_FillVolumeBinding;

        // Contour Overlay Telemetry & Diagnostics
        private ValueBinding<bool> m_ContoursActiveBinding;
        private ValueBinding<float> m_IntervalBinding;
        private ValueBinding<float> m_TerrainMinElevBinding;
        private ValueBinding<float> m_TerrainMaxElevBinding;
        private ValueBinding<int> m_ContourLevelsBinding;
        private ValueBinding<int> m_GeneratedSegmentsBinding;
        private ValueBinding<int> m_RenderedSegmentsBinding;
        private ValueBinding<float> m_GenerationTimeMsBinding;
        private ValueBinding<bool> m_HighVisibilityBinding;

        private RawValueBinding m_ProfileBinding;

        protected override void OnCreate()
        {
            base.OnCreate();

            AddBinding(m_ElevationBinding = new ValueBinding<float>("ContourPlus", "elevation", 0f));
            AddBinding(m_SlopePercentBinding = new ValueBinding<float>("ContourPlus", "slopePercent", 0f));
            AddBinding(m_SlopeDegreesBinding = new ValueBinding<float>("ContourPlus", "slopeDegrees", 0f));
            AddBinding(m_SlopeCategoryBinding = new ValueBinding<string>("ContourPlus", "slopeCategory", "-"));
            AddBinding(m_SlopeUnitBinding = new ValueBinding<string>("ContourPlus", "slopeUnit", "Percentage"));

            AddBinding(m_HasRoadBinding = new ValueBinding<bool>("ContourPlus", "hasRoad", false));
            AddBinding(m_RoadNameBinding = new ValueBinding<string>("ContourPlus", "roadName", "-"));
            AddBinding(m_StartElevationBinding = new ValueBinding<float>("ContourPlus", "startElevation", 0f));
            AddBinding(m_EndElevationBinding = new ValueBinding<float>("ContourPlus", "endElevation", 0f));
            AddBinding(m_ElevationDeltaBinding = new ValueBinding<float>("ContourPlus", "elevationDelta", 0f));
            AddBinding(m_LengthBinding = new ValueBinding<float>("ContourPlus", "length", 0f));
            AddBinding(m_AverageGradeBinding = new ValueBinding<float>("ContourPlus", "averageGrade", 0f));
            AddBinding(m_MaxGradeBinding = new ValueBinding<float>("ContourPlus", "maxGrade", 0f));
            AddBinding(m_GuidanceCategoryBinding = new ValueBinding<string>("ContourPlus", "guidanceCategory", "-"));
            AddBinding(m_CutVolumeBinding = new ValueBinding<float>("ContourPlus", "cutVolume", 0f));
            AddBinding(m_FillVolumeBinding = new ValueBinding<float>("ContourPlus", "fillVolume", 0f));

            // Contour overlay bindings
            AddBinding(m_ContoursActiveBinding = new ValueBinding<bool>("ContourPlus", "contoursActive", true));
            AddBinding(m_IntervalBinding = new ValueBinding<float>("ContourPlus", "interval", 10f));
            AddBinding(m_TerrainMinElevBinding = new ValueBinding<float>("ContourPlus", "terrainMinElev", 0f));
            AddBinding(m_TerrainMaxElevBinding = new ValueBinding<float>("ContourPlus", "terrainMaxElev", 0f));
            AddBinding(m_ContourLevelsBinding = new ValueBinding<int>("ContourPlus", "contourLevels", 0));
            AddBinding(m_GeneratedSegmentsBinding = new ValueBinding<int>("ContourPlus", "generatedSegments", 0));
            AddBinding(m_RenderedSegmentsBinding = new ValueBinding<int>("ContourPlus", "renderedSegments", 0));
            AddBinding(m_GenerationTimeMsBinding = new ValueBinding<float>("ContourPlus", "generationTimeMs", 0f));
            AddBinding(m_HighVisibilityBinding = new ValueBinding<bool>("ContourPlus", "highVisibility", false));

            AddBinding(m_PanelOpenBinding = new ValueBinding<bool>("ContourPlus", "panelOpen", false));
            AddBinding(new TriggerBinding("ContourPlus", "togglePanel", OnTogglePanel));
            AddBinding(new TriggerBinding("ContourPlus", "closePanel", OnClosePanel));

            // Triggers for contours
            AddBinding(new TriggerBinding<bool>("ContourPlus", "toggleContours", OnToggleContours));
            AddBinding(new TriggerBinding<float>("ContourPlus", "setInterval", OnSetInterval));
            AddBinding(new TriggerBinding("ContourPlus", "toggleHighVisibility", OnToggleHighVisibility));
            AddBinding(new TriggerBinding("ContourPlus", "refreshContours", OnRefreshContours));

            AddBinding(m_ProfileBinding = new RawValueBinding("ContourPlus", "profile", WriteProfile));
        }

        private ValueBinding<bool> m_PanelOpenBinding;

        private void OnToggleContours(bool enabled)
        {
            ContourOverlaySystem.Instance?.SetContoursEnabled(enabled);
        }

        private void OnSetInterval(float interval)
        {
            ContourOverlaySystem.Instance?.SetInterval(interval);
        }

        private void OnToggleHighVisibility()
        {
            ContourOverlaySystem.Instance?.ToggleHighVisibility();
        }

        private void OnRefreshContours()
        {
            ContourOverlaySystem.Instance?.ForceRegenerate();
        }


        private void OnTogglePanel()
        {
            if (m_PanelOpenBinding != null)
            {
                m_PanelOpenBinding.Update(!m_PanelOpenBinding.value);
                IsPanelOpen = m_PanelOpenBinding.value;
            }
        }

        private void OnClosePanel()
        {
            if (m_PanelOpenBinding != null)
            {
                m_PanelOpenBinding.Update(false);
                IsPanelOpen = false;
            }
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (World == null || !World.IsCreated) return;
            base.OnGamePreload(purpose, mode);
            IsPanelOpen = false;
            m_PanelOpenBinding?.Update(false);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            var sys = TopographySystem.Instance;
            if (sys != null)
            {
                m_ElevationBinding.Update(sys.CursorElevation);
                m_SlopePercentBinding.Update(sys.CursorSlopePercent);
                m_SlopeDegreesBinding.Update(sys.CursorSlopeDegrees);
                m_SlopeCategoryBinding.Update(sys.CursorSlopeCategory);
                m_SlopeUnitBinding.Update(Mod.SettingInstance == null ? "Percentage" : Mod.SettingInstance.Unit.ToString());

                var r = sys.ActiveRoadGrade;
                m_HasRoadBinding.Update(r.HasRoad);
                m_RoadNameBinding.Update(r.RoadName ?? "-");
                m_StartElevationBinding.Update(r.StartElevation);
                m_EndElevationBinding.Update(r.EndElevation);
                m_ElevationDeltaBinding.Update(r.ElevationDelta);
                m_LengthBinding.Update(r.LengthMeters);
                m_AverageGradeBinding.Update(r.AverageGradePercent);
                m_MaxGradeBinding.Update(r.MaxGradePercent);
                m_GuidanceCategoryBinding.Update(r.GuidanceCategory ?? "-");
                m_CutVolumeBinding.Update(r.EstimatedCutM3);
                m_FillVolumeBinding.Update(r.EstimatedFillM3);

                m_ProfileBinding.Update();
            }

            var contourSys = ContourOverlaySystem.Instance;
            if (contourSys != null)
            {
                m_ContoursActiveBinding.Update(contourSys.ContoursEnabled);
                m_IntervalBinding.Update(contourSys.IntervalMeters);
                m_TerrainMinElevBinding.Update(contourSys.TerrainMinElevation);
                m_TerrainMaxElevBinding.Update(contourSys.TerrainMaxElevation);
                m_ContourLevelsBinding.Update(contourSys.ContourLevelsCount);
                m_GeneratedSegmentsBinding.Update(contourSys.GeneratedSegmentsCount);
                m_RenderedSegmentsBinding.Update(contourSys.RenderedSegmentsCount);
                m_GenerationTimeMsBinding.Update(contourSys.LastGenerationTimeMs);
                m_HighVisibilityBinding.Update(contourSys.HighVisibilityMode);
            }
        }


        private void WriteProfile(IJsonWriter writer)
        {
            var list = TopographySystem.Instance?.ActiveRoadGrade.LongitudinalProfile ?? new List<ProfileSample>();
            writer.ArrayBegin(list.Count);
            foreach (var p in list)
            {
                writer.TypeBegin("ProfileSample");
                writer.PropertyName("dist");
                writer.Write(p.Distance);
                writer.PropertyName("elev");
                writer.Write(p.Elevation);
                writer.PropertyName("terrainElev");
                writer.Write(p.TerrainElevation);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }

        protected override void OnDestroy()
        {
            IsPanelOpen = false;
            base.OnDestroy();
        }
    }
}
