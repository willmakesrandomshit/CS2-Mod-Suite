import { PortfolioHelp, useRememberedPreference } from "./portfolio-ux";
import React, { useState, useEffect, useCallback } from 'react';
import { ModRegistrar } from 'cs2/modding';
import { bindValue, trigger } from 'cs2/api';
import { FloatingButton, Tooltip } from 'cs2/ui';
import iconSrc from './assets/contourplus-icon.svg';
import './style.scss';

interface ProfilePoint {
  dist: number;
  elev: number;
  terrainElev: number;
}

// UI Panel & Cursor Telemetry
const openBinding = bindValue<boolean>('ContourPlus', 'panelOpen', false);
const elevBinding = bindValue<number>('ContourPlus', 'elevation', 0);
const slopePercentBinding = bindValue<number>('ContourPlus', 'slopePercent', 0);
const slopeDegreesBinding = bindValue<number>('ContourPlus', 'slopeDegrees', 0);
const slopeCategoryBinding = bindValue<string>('ContourPlus', 'slopeCategory', 'Flat');
const slopeUnitBinding = bindValue<string>('ContourPlus', 'slopeUnit', 'Percentage');

// Road Planning Grade Telemetry
const hasRoadBinding = bindValue<boolean>('ContourPlus', 'hasRoad', false);
const roadNameBinding = bindValue<string>('ContourPlus', 'roadName', 'None');
const startElevationBinding = bindValue<number>('ContourPlus', 'startElevation', 0);
const endElevationBinding = bindValue<number>('ContourPlus', 'endElevation', 0);
const elevationDeltaBinding = bindValue<number>('ContourPlus', 'elevationDelta', 0);
const lengthBinding = bindValue<number>('ContourPlus', 'length', 0);
const averageGradeBinding = bindValue<number>('ContourPlus', 'averageGrade', 0);
const maxGradeBinding = bindValue<number>('ContourPlus', 'maxGrade', 0);
const guidanceCategoryBinding = bindValue<string>('ContourPlus', 'guidanceCategory', 'Gentle');
const cutVolumeBinding = bindValue<number>('ContourPlus', 'cutVolume', 0);
const fillVolumeBinding = bindValue<number>('ContourPlus', 'fillVolume', 0);
const profileBinding = bindValue<ProfilePoint[]>('ContourPlus', 'profile', []);

// Live Contour Diagnostics & Overlay Controls
const contoursActiveBinding = bindValue<boolean>('ContourPlus', 'contoursActive', true);
const intervalBinding = bindValue<number>('ContourPlus', 'interval', 10);
const terrainMinElevBinding = bindValue<number>('ContourPlus', 'terrainMinElev', 0);
const terrainMaxElevBinding = bindValue<number>('ContourPlus', 'terrainMaxElev', 0);
const contourLevelsBinding = bindValue<number>('ContourPlus', 'contourLevels', 0);
const generatedSegmentsBinding = bindValue<number>('ContourPlus', 'generatedSegments', 0);
const renderedSegmentsBinding = bindValue<number>('ContourPlus', 'renderedSegments', 0);
const generationTimeMsBinding = bindValue<number>('ContourPlus', 'generationTimeMs', 0);
const highVisibilityBinding = bindValue<boolean>('ContourPlus', 'highVisibility', false);

const SPACING_PRESETS = [5, 10, 25, 50];

const portfolioBindings = { "panelOpen": openBinding, "elevation": elevBinding, "slopePercent": slopePercentBinding, "slopeDegrees": slopeDegreesBinding, "hasRoad": hasRoadBinding, "startElevation": startElevationBinding, "endElevation": endElevationBinding, "elevationDelta": elevationDeltaBinding, "length": lengthBinding, "averageGrade": averageGradeBinding, "maxGrade": maxGradeBinding, "cutVolume": cutVolumeBinding, "fillVolume": fillVolumeBinding, "contoursActive": contoursActiveBinding, "interval": intervalBinding, "terrainMinElev": terrainMinElevBinding, "terrainMaxElev": terrainMaxElevBinding, "contourLevels": contourLevelsBinding, "generatedSegments": generatedSegmentsBinding, "renderedSegments": renderedSegmentsBinding, "generationTimeMs": generationTimeMsBinding, "highVisibility": highVisibilityBinding };
export const ContourPlusToolbarButton: React.FC = () => {
  const [isOpen, setIsOpen] = useState(openBinding.value);

  useEffect(() => {
    const sub = openBinding.subscribe(setIsOpen);
    return () => sub.dispose();
  }, []);

  const toggle = useCallback(() => trigger('ContourPlus', 'togglePanel'), []);

  return (
    <Tooltip tooltip="Contour Plus — View terrain contours and road grades">
      <FloatingButton
        src={iconSrc}
        selected={isOpen}
        onSelect={toggle}
      />
    </Tooltip>
  );

};

export const ContourPlusPanel: React.FC = () => {
  const [isOpen, setIsOpen] = useState(openBinding.value);
  const [elev, setElev] = useState(elevBinding.value);
  const [slopePercent, setSlopePercent] = useState(slopePercentBinding.value);
  const [slopeDegrees, setSlopeDegrees] = useState(slopeDegreesBinding.value);
  const [slopeCategory, setSlopeCategory] = useState(slopeCategoryBinding.value);
  const [slopeUnit, setSlopeUnit] = useState(slopeUnitBinding.value);

  const [hasRoad, setHasRoad] = useState(hasRoadBinding.value);
  const [roadName, setRoadName] = useState(roadNameBinding.value);
  const [startElevation, setStartElevation] = useState(startElevationBinding.value);
  const [endElevation, setEndElevation] = useState(endElevationBinding.value);
  const [elevationDelta, setElevationDelta] = useState(elevationDeltaBinding.value);
  const [length, setLength] = useState(lengthBinding.value);
  const [averageGrade, setAverageGrade] = useState(averageGradeBinding.value);
  const [maxGrade, setMaxGrade] = useState(maxGradeBinding.value);
  const [guidanceCategory, setGuidanceCategory] = useState(guidanceCategoryBinding.value);
  const [cutVolume, setCutVolume] = useState(cutVolumeBinding.value);
  const [fillVolume, setFillVolume] = useState(fillVolumeBinding.value);
  const [profile, setProfile] = useState<ProfilePoint[]>(profileBinding.value || []);

  const [contoursActive, setContoursActive] = useState(contoursActiveBinding.value);
  const [selectedInterval, setSelectedInterval] = useState(intervalBinding.value || 10);
  const [minElev, setMinElev] = useState(terrainMinElevBinding.value);
  const [maxElev, setMaxElev] = useState(terrainMaxElevBinding.value);
  const [levelsCount, setLevelsCount] = useState(contourLevelsBinding.value);
  const [renderedSegments, setRenderedSegments] = useState(renderedSegmentsBinding.value);
  const [genTimeMs, setGenTimeMs] = useState(generationTimeMsBinding.value);
  const [highVis, setHighVis] = useState(highVisibilityBinding.value);

  const [showAdvanced, setShowAdvanced] = useRememberedPreference("ContourPlus.showAdvanced", false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 2500);
  };

  const close = useCallback(() => trigger('ContourPlus', 'closePanel'), []);

  useEffect(() => {
    const s00 = openBinding.subscribe(setIsOpen);
    const s01 = elevBinding.subscribe(setElev);
    const s02 = slopePercentBinding.subscribe(setSlopePercent);
    const s03 = slopeDegreesBinding.subscribe(setSlopeDegrees);
    const s04 = slopeCategoryBinding.subscribe(setSlopeCategory);
    const s25 = slopeUnitBinding.subscribe(setSlopeUnit);
    const s05 = hasRoadBinding.subscribe(setHasRoad);
    const s06 = roadNameBinding.subscribe(setRoadName);
    const s07 = startElevationBinding.subscribe(setStartElevation);
    const s08 = endElevationBinding.subscribe(setEndElevation);
    const s09 = elevationDeltaBinding.subscribe(setElevationDelta);
    const s10 = lengthBinding.subscribe(setLength);
    const s11 = averageGradeBinding.subscribe(setAverageGrade);
    const s12 = maxGradeBinding.subscribe(setMaxGrade);
    const s13 = guidanceCategoryBinding.subscribe(setGuidanceCategory);
    const s14 = cutVolumeBinding.subscribe(setCutVolume);
    const s15 = fillVolumeBinding.subscribe(setFillVolume);
    const s16 = profileBinding.subscribe(setProfile);

    const s17 = contoursActiveBinding.subscribe(setContoursActive);
    const s18 = intervalBinding.subscribe(setSelectedInterval);
    const s19 = terrainMinElevBinding.subscribe(setMinElev);
    const s20 = terrainMaxElevBinding.subscribe(setMaxElev);
    const s21 = contourLevelsBinding.subscribe(setLevelsCount);
    const s22 = renderedSegmentsBinding.subscribe(setRenderedSegments);
    const s23 = generationTimeMsBinding.subscribe(setGenTimeMs);
    const s24 = highVisibilityBinding.subscribe(setHighVis);

    return () => {
      s00.dispose(); s01.dispose(); s02.dispose(); s03.dispose(); s04.dispose();
      s05.dispose(); s06.dispose(); s07.dispose(); s08.dispose(); s09.dispose();
      s10.dispose(); s11.dispose(); s12.dispose(); s13.dispose(); s14.dispose();
      s15.dispose(); s16.dispose();
      s17.dispose(); s18.dispose(); s19.dispose(); s20.dispose(); s21.dispose();
      s22.dispose(); s23.dispose(); s24.dispose();
      s25.dispose();
    };
  }, []);

  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") close();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, close]);

  const toggleContours = () => {
    const next = !contoursActive;
    setContoursActive(next);
    trigger('ContourPlus', 'toggleContours', next);
    showToast(next ? 'Enabled contour lines' : 'Disabled contour lines');
  };

  const setIntervalPreset = (val: number) => {
    setSelectedInterval(val);
    trigger('ContourPlus', 'setInterval', val);
    showToast(`Set contour interval to ${val}m`);
  };

  const toggleHighVis = () => {
    trigger('ContourPlus', 'toggleHighVisibility');
    showToast(`High visibility mode toggled`);
  };

  if (!isOpen) return null;

  return (
    <div className="suite-panel contourplus-panel" data-portfolio-panel role="dialog" aria-label="Contour+ Panel">
      {/* PANEL HEADER */}
      <div className="suite-header">
        <div className="header-left">
          <img src={iconSrc} alt="Contour+" className="header-icon" />
          <div>
            <h2 className="header-title">Contour+</h2>
            <span className="header-subtitle">
              {hasRoad ? roadName : 'Topography & Elevation Overlays'}
            </span>
          </div>
        </div>
        <div className="header-right">
          <span className="suite-badge status-good">
            {elev.toFixed(1)}m Elev
          </span>
          <button className="suite-close-btn" onClick={close} title="Close Panel" aria-label="Close panel">×</button>
        </div>
      </div>

      {toastMessage && (
        <div className="suite-toast">
          <span>{toastMessage}</span>
        </div>
      )}

      {/* PANEL BODY */}
      <PortfolioHelp runtimeGroup={"Portfolio.ContourPlus"} name={"Contour Plus"} version={"1.2.5-beta.1"} steps={["Enable contours and choose an interval.", "Inspect the terrain, then use the grade-planning controls.", "Turn contours off when finished."]} note={"Sampling is bounded. Readouts are planning estimates, not surveying data."} bindings={portfolioBindings} />
      <div className="suite-body">
        {!hasRoad ? (
          /* NO ROAD SELECTED — TOPOGRAPHY OVERVIEW */
          <div className="topography-overview">
            {/* HERO CARD */}
            <div className="hero-status-card">
              <div className="hero-status-left">
                <span className="hero-label">TOPOGRAPHY OVERLAYS</span>
                <h3 className={`hero-title ${contoursActive ? (renderedSegments > 0 ? 'status-good' : 'status-warning') : 'status-muted'}`}>
                  {contoursActive ? (renderedSegments > 0 ? `Contours Active (${renderedSegments} lines)` : 'Contours Active (Sampling...)') : 'Contours Hidden'}
                </h3>
                <p className="hero-desc">
                  Elevation range: {minElev.toFixed(0)}m – {maxElev.toFixed(0)}m • {levelsCount} levels ({selectedInterval}m step)
                </p>
              </div>
              <div className="hero-status-right">
                <button
                  className={`suite-primary-btn ${contoursActive ? 'active' : ''}`}
                  onClick={toggleContours}
                  title="Toggle topography contour line overlays on terrain"
                >
                  {contoursActive ? 'Visible' : 'Enable'}
                </button>
              </div>
            </div>

            {/* CONTOUR SPACING PRESETS */}
            <div className="section-header">
              <span className="section-title">Contour Line Spacing</span>
              <span className="section-subtitle">Interval Step</span>
            </div>

            <div className="intervals-row">
              {SPACING_PRESETS.map((val) => (
                <button
                  key={val}
                  className={`interval-btn ${selectedInterval === val ? 'selected' : ''}`}
                  onClick={() => setIntervalPreset(val)}
                  title={`Show a contour line every ${val} meters of elevation`}
                >
                  {val}m
                </button>
              ))}
            </div>

            {/* LIVE DIAGNOSTICS CARD */}
            <div className="diagnostics-card">
              <div className="diag-header">
                <span className="diag-title">Pipeline Telemetry</span>
                <button className="highvis-toggle-btn" onClick={toggleHighVis} title="Toggle bold high-contrast contour lines">
                  {highVis ? 'High-Vis: ON' : 'High-Vis: OFF'}
                </button>
              </div>
              <div className="diag-grid">
                <div className="diag-item">
                  <span className="diag-label">Cursor Elev</span>
                  <span className="diag-val">{elev.toFixed(1)} m</span>
                </div>
                <div className="diag-item">
                  <span className="diag-label">Terrain Slope</span>
                  <span className="diag-val">{slopeUnit === 'Degrees' ? `${slopeDegrees.toFixed(1)}°` : `${slopePercent.toFixed(1)}%`} ({slopeCategory})</span>
                </div>
                <div className="diag-item">
                  <span className="diag-label">Active Levels</span>
                  <span className="diag-val">{levelsCount} isolines</span>
                </div>
                <div className="diag-item">
                  <span className="diag-label">Rendered Lines</span>
                  <span className="diag-val">{renderedSegments.toLocaleString()}</span>
                </div>
              </div>
            </div>

            <div className="hint-card">
              <span className="hint-icon">ROAD</span>
              <div className="hint-text">
                <strong>Inspect Road Slopes</strong>
                <p>Click any road in your city to inspect its grade percentage, slope category, elevation delta, and longitudinal profile.</p>
              </div>
            </div>
          </div>
        ) : (
          /* ROAD ELEVATION INSPECTOR */
          <div className="road-elevation-view">
            {/* HERO GRADE CARD */}
            <div className="hero-status-card">
              <div className="hero-status-left">
                <span className="hero-label">ROAD GRADE</span>
                <h3 className="hero-title status-good">
                  {averageGrade.toFixed(1)}% Grade
                </h3>
                <p className="hero-desc">
                  Status: <strong>{guidanceCategory}</strong> (Max Grade: {maxGrade.toFixed(1)}%)
                </p>
              </div>
              <div className="hero-status-right">
                <span className={`grade-tag ${averageGrade > 10 ? 'steep' : averageGrade > 5 ? 'moderate' : 'gentle'}`}>
                  {guidanceCategory}
                </span>
              </div>
            </div>

            {/* METRICS ROW */}
            <div className="metrics-summary-grid">
              <div className="metric-box">
                <span className="metric-label">ELEVATION DELTA</span>
                <span className="metric-value">
                  {elevationDelta >= 0 ? `+${elevationDelta.toFixed(1)}` : elevationDelta.toFixed(1)} m
                </span>
              </div>
              <div className="metric-box">
                <span className="metric-label">SEGMENT LENGTH</span>
                <span className="metric-value">{length.toFixed(0)} m</span>
              </div>
              <div className="metric-box">
                <span className="metric-label">START → END</span>
                <span className="metric-value">{startElevation.toFixed(1)}m → {endElevation.toFixed(1)}m</span>
              </div>
            </div>

            {/* PROFILE PREVIEW */}
            {profile.length > 0 && (
              <div className="profile-preview-card">
                <div className="section-header">
                  <span className="section-title">Elevation Profile</span>
                  <span className="section-subtitle">{profile.length} sample points</span>
                </div>
                <div className="profile-bar-chart">
                  {profile.map((p, i) => {
                    const minP = Math.min(...profile.map((pt) => pt.elev));
                    const maxP = Math.max(...profile.map((pt) => pt.elev));
                    const range = Math.max(maxP - minP, 1);
                    const pct = Math.max(10, Math.min(100, ((p.elev - minP) / range) * 100));

                    return (
                      <div
                        key={i}
                        className="profile-bar"
                        style={{ height: `${pct}%` }}
                        title={`${p.dist.toFixed(0)}m: ${p.elev.toFixed(1)}m elevation (Terrain: ${p.terrainElev.toFixed(1)}m)`}
                      />
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      {/* FOOTER */}
      <div className="suite-footer">
        <span className="footer-hint">
          {hasRoad ? `Inspecting ${roadName}` : `Cursor elevation: ${elev.toFixed(1)}m`}
        </span>
        <button
          className="advanced-toggle-btn"
          onClick={() => setShowAdvanced(!showAdvanced)}
          title="Toggle earthwork calculations"
        >
          {showAdvanced ? 'Hide Advanced' : 'Earthwork'}
        </button>
      </div>

      {/* ADVANCED DRAWER */}
      {showAdvanced && (
        <div className="advanced-drawer">
          <div className="drawer-row">
            <span>Estimated Cut Volume: <strong>{cutVolume.toFixed(0)} m³</strong></span>
            <span>Estimated Fill Volume: <strong>{fillVolume.toFixed(0)} m³</strong></span>
          </div>
          <div className="drawer-telemetry">
            <div>Terrain Slope: {slopeDegrees.toFixed(1)}° ({slopePercent.toFixed(1)}%) • Profile Points: {profile.length}</div>
          </div>
        </div>
      )}
    </div>
  );
};

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append('GameTopLeft', ContourPlusToolbarButton);
  moduleRegistry.append('GameTopRight', ContourPlusPanel);
};

export default register;
