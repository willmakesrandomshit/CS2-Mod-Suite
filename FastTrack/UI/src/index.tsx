import { PortfolioHelp, useRememberedPreference } from "./portfolio-ux";
import { PerformanceComparison } from './performance-comparison';
import React, { useEffect, useState, useCallback } from "react";
import type { ModRegistrar } from "cs2/modding";
import { bindValue, trigger } from "cs2/api";
import { FloatingButton, Tooltip } from "cs2/ui";
import iconSrc from "./assets/fasttrack-icon.svg";
import "./style.scss";

// Bindings
const b_enabled     = bindValue<boolean>("fastTrack", "enabled",         true);
const b_fps         = bindValue<number> ("fastTrack", "fps",             0);
const b_loadSecs    = bindValue<number> ("fastTrack", "loadSeconds",     0);
const b_mainMs      = bindValue<number> ("fastTrack", "mainThreadMs",    0);
const b_gpuMs       = bindValue<number> ("fastTrack", "gpuMs",           0);
const b_drawCalls   = bindValue<number> ("fastTrack", "drawCalls",       0);
const b_open        = bindValue<boolean>("fastTrack", "open",            false);
const b_showBtn     = bindValue<boolean>("fastTrack", "showButton",      true);
const b_status      = bindValue<string> ("fastTrack", "status",          "Measuring");
const b_bottleneck  = bindValue<string> ("fastTrack", "bottleneck",      "Measuring");
const b_cameraState = bindValue<string> ("fastTrack", "cameraState",     "Overview");
const b_isGpuBound  = bindValue<boolean>("fastTrack", "isGpuBound",      false);
const b_isCpuBound  = bindValue<boolean>("fastTrack", "isCpuBound",      false);
const b_loadBoost   = bindValue<boolean>("fastTrack", "loadBoostActive", false);
const b_loadBoostEnabled = bindValue<boolean>("fastTrack", "loadBoostEnabled", false);
const b_lodScale    = bindValue<number> ("fastTrack", "lodScale",        1);
const b_adaptiveLod = bindValue<boolean>("fastTrack", "adaptiveLodEnabled", false);
const b_lodActive   = bindValue<boolean>("fastTrack", "adaptiveLodActive",  false);

function useB<T>(b: { value: T; subscribe(c: (v: T) => void): { value: T; dispose(): void } }): T {
  const [v, set] = useState<T>(b.value);
  useEffect(() => {
    const s = b.subscribe(set);
    set(s.value);
    return () => s.dispose();
  }, [b]);
  return v;
}

const portfolioBindings = { "enabled": b_enabled, "fps": b_fps, "loadSeconds": b_loadSecs, "mainThreadMs": b_mainMs, "gpuMs": b_gpuMs, "drawCalls": b_drawCalls, "open": b_open, "showButton": b_showBtn, "isGpuBound": b_isGpuBound, "isCpuBound": b_isCpuBound, "loadBoostActive": b_loadBoost, "loadBoostEnabled": b_loadBoostEnabled, "lodScale": b_lodScale, "adaptiveLodEnabled": b_adaptiveLod, "adaptiveLodActive": b_lodActive };
export const FastTrackToolbarButton: React.FC = () => {
  const isOpen = useB(b_open);
  const showBtn = useB(b_showBtn);
  const toggle = useCallback(() => trigger("fastTrack", "toggle"), []);

  if (!showBtn) return null;

  return (
    <Tooltip tooltip="FastTrack — Adaptive detail and performance monitoring">
      <FloatingButton
        src={iconSrc}
        selected={isOpen}
        onSelect={toggle}
      />
    </Tooltip>
  );

};

export const FastTrackPanel: React.FC = () => {
  const enabled     = useB(b_enabled);
  const fps         = useB(b_fps);
  const loadSecs    = useB(b_loadSecs);
  const mainMs      = useB(b_mainMs);
  const gpuMs       = useB(b_gpuMs);
  const drawCalls   = useB(b_drawCalls);
  const isOpen      = useB(b_open);
  const status      = useB(b_status);
  const bottleneck  = useB(b_bottleneck);
  const cameraState = useB(b_cameraState);
  const isGpuBound  = useB(b_isGpuBound);
  const isCpuBound  = useB(b_isCpuBound);
  const loadBoost   = useB(b_loadBoost);
  const loadBoostEnabled = useB(b_loadBoostEnabled);
  const lodScale    = useB(b_lodScale);
  const adaptiveLod = useB(b_adaptiveLod);
  const lodActive   = useB(b_lodActive);

  const [showAdvanced, setShowAdvanced] = useRememberedPreference("FastTrack.showAdvanced", false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 2500);
  };

  const close = useCallback(() => trigger("fastTrack", "close"), []);
  const toggleAdaptiveLod = useCallback(() => {
    trigger("fastTrack", "toggleAdaptiveLod");
    showToast(adaptiveLod ? "Adaptive Detail off — native LOD restored" : "Adaptive Detail on — applies at distant views");
  }, [adaptiveLod]);

  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") close();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, close]);

  const toggleEnabled = () => {
    trigger("fastTrack", "setEnabled", !enabled);
    showToast(!enabled ? "FastTrack enabled" : "Native LOD restored; FastTrack paused");
  };

  if (!isOpen) return null;

  const fpsStatus = fps >= 50 ? "status-good" : fps >= 30 ? "status-watch" : "status-problem";
  const fpsText = fps <= 0 ? "Measuring" : fps >= 50 ? "Smooth" : fps >= 30 ? "Playable" : "Lagging";

  return (
    <div className="suite-panel fasttrack-panel" data-portfolio-panel role="dialog" aria-label="FastTrack Panel">
      {/* PANEL HEADER */}
      <div className="suite-header">
        <div className="header-left">
          <img src={iconSrc} alt="FastTrack" className="header-icon" />
          <div>
            <h2 className="header-title">FastTrack</h2>
            <span className="header-subtitle">Adaptive Detail & Performance</span>
          </div>
        </div>
        <div className="header-right">
          <span className={`suite-badge ${fpsStatus}`}>
            {fps} FPS • {fpsText}
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
      <PortfolioHelp runtimeGroup={"Portfolio.FastTrack"} name={"FastTrack"} version={"1.3.5-beta.2"} steps={["Keep the camera and simulation speed steady while measuring.", "Capture a baseline, change one option, then capture a comparison.", "Inspect Adaptive Detail status; zoom in to check restored detail."]} note={"A comparison is observational. Camera, simulation and other mods can affect FPS."} bindings={portfolioBindings} />
      <div className="suite-body">
        {/* HERO STATUS CARD */}
        <div className="hero-status-card">
          <div className="hero-status-left">
            <span className="hero-label">CURRENT PERFORMANCE</span>
            <h3 className={`hero-title ${fpsStatus}`}>
              {fps} FPS ({fpsText})
            </h3>
            <p className="hero-desc">
              {enabled
                ? `${status}. Adaptive Detail changes only the game's native LOD distance and restores it at close views.`
                : "FastTrack is paused; any LOD adjustment is restored."}
            </p>
          </div>
          <div className="hero-status-right">
            <button
              className={`suite-primary-btn ${enabled ? "active-pulse" : ""}`}
              onClick={toggleEnabled}
              title="Pause monitoring and restore adaptive LOD"
            >
              {enabled ? "Pause" : "Enable"}
            </button>
          </div>
        </div>

        {/* OPTIMIZATION */}
        <div className="section-header">
          <span className="section-title">Optimization</span>
          <span className="section-subtitle">Opt-in · restored at close view</span>
        </div>

        <div className="presets-grid">
          <button type="button" aria-pressed={adaptiveLod} className={`preset-card ${adaptiveLod ? "selected" : ""}`} onClick={toggleAdaptiveLod}>
            <div className="preset-icon">LOD</div>
            <div className="preset-info">
              <div className="preset-name">Adaptive Detail</div>
              <div className="preset-desc">Reduces distant LOD at aerial camera heights. Full detail returns when you zoom in. Tune the maximum reduction in Options → FastTrack.</div>
            </div>
            <span className="preset-check">{!enabled && adaptiveLod ? "PAUSED" : adaptiveLod ? (lodActive ? `ACTIVE ${Math.round(lodScale * 100)}%` : "ON") : "OFF"}</span>
          </button>
          <div className={`preset-card ${loadBoost ? "selected" : ""}`}>
            <div className="preset-icon">LOAD</div>
            <div className="preset-info">
              <div className="preset-name">Experimental loading boost</div>
              <div className="preset-desc">Off by default. Opt in through Options → FastTrack. No speedup is guaranteed; leave off when diagnosing GPU problems.</div>
            </div>
            <span className="preset-check">{loadBoost ? "ACTIVE" : loadBoostEnabled ? "READY" : "OFF"}</span>
          </div>
        </div>
        <PerformanceComparison />
      </div>

      {/* FOOTER */}
      <div className="suite-footer">
        <span className="footer-hint">
          {`Sampled performance: ${bottleneck}`}
        </span>
        <button
          className="advanced-toggle-btn"
          onClick={() => setShowAdvanced(!showAdvanced)}
          title="Toggle deep frametime & thread telemetry"
        >
          {showAdvanced ? "Hide Advanced" : "Advanced"}
        </button>
      </div>

      {/* ADVANCED DRAWER */}
      {showAdvanced && (
        <div className="advanced-drawer">
          <div className="drawer-row">
            <span>Main Thread: <strong>{mainMs > 0 ? `${mainMs.toFixed(1)} ms` : "Unavailable"}</strong></span>
            <span>GPU Frame: <strong>{gpuMs > 0 ? `${gpuMs.toFixed(1)} ms` : "Unavailable"}</strong></span>
            <span>Draw Calls: <strong>{drawCalls.toLocaleString()}</strong></span>
          </div>
          <div className="drawer-telemetry">
            <div>Adaptive LOD: {lodActive ? `reduced to ${Math.round(lodScale * 100)}% of your current setting` : adaptiveLod ? "enabled; waiting for a distant view" : "off"}. Render scale stays game-controlled.</div>
            <div>Last city-load stage: {loadSecs > 0 ? `${loadSecs.toFixed(1)} s` : "Not measured"}</div>
            <div>Camera: {cameraState} | Loading Boost: {loadBoost ? "Active" : "Inactive"}</div>
            <div>Timing hint: {isGpuBound ? "GPU over target" : isCpuBound ? "Main thread over target" : "See sampled performance above"}. Not a proven cause.</div>
          </div>
        </div>
      )}
    </div>
  );
};

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append("GameTopLeft", FastTrackToolbarButton);
  moduleRegistry.append("GameTopRight", FastTrackPanel);
};

export default register;
