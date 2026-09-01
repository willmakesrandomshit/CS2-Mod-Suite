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
const b_safeMode    = bindValue<boolean>("fastTrack", "safeMode",        true);
const b_visualMut   = bindValue<number> ("fastTrack", "visualMutations", 0);

function useB<T>(b: { value: T; subscribe(c: (v: T) => void): { value: T; dispose(): void } }): T {
  const [v, set] = useState<T>(b.value);
  useEffect(() => {
    const s = b.subscribe(set);
    set(s.value);
    return () => s.dispose();
  }, [b]);
  return v;
}

export const FastTrackToolbarButton: React.FC = () => {
  const isOpen = useB(b_open);
  const showBtn = useB(b_showBtn);
  const toggle = useCallback(() => trigger("fastTrack", "toggle"), []);

  if (!showBtn) return null;

  return (
    <Tooltip tooltip="FastTrack — Performance tools and monitoring">
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
  const safeMode    = useB(b_safeMode);
  const visualMut   = useB(b_visualMut);

  const [showAdvanced, setShowAdvanced] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 2500);
  };

  const close = useCallback(() => trigger("fastTrack", "close"), []);

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
    showToast(!enabled ? "Enabled visual-safe monitoring" : "Restored vanilla and paused FastTrack");
  };

  if (!isOpen) return null;

  const fpsStatus = fps >= 50 ? "status-good" : fps >= 30 ? "status-watch" : "status-problem";
  const fpsText = fps >= 50 ? "Smooth" : fps >= 30 ? "Playable" : "Lagging";

  return (
    <div className="suite-panel fasttrack-panel" role="dialog" aria-label="FastTrack Panel">
      {/* PANEL HEADER */}
      <div className="suite-header">
        <div className="header-left">
          <img src={iconSrc} alt="FastTrack" className="header-icon" />
          <div>
            <h2 className="header-title">FastTrack</h2>
            <span className="header-subtitle">Performance Diagnostics & Load-Stage Tuning</span>
          </div>
        </div>
        <div className="header-right">
          <span className={`suite-badge ${fpsStatus}`}>
            {fps} FPS • {fpsText}
          </span>
          <button className="suite-close-btn" onClick={close} title="Close Panel">✕</button>
        </div>
      </div>

      {toastMessage && (
        <div className="suite-toast">
          <span>✓ {toastMessage}</span>
        </div>
      )}

      {/* PANEL BODY */}
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
                ? `${status}. Native LOD, terrain, culling, shadows and render scale are untouched.`
                : "FastTrack is paused and vanilla values are restored."}
            </p>
          </div>
          <div className="hero-status-right">
            <button
              className={`suite-primary-btn ${enabled ? "active-pulse" : ""}`}
              onClick={toggleEnabled}
              title="Toggle visual-safe FastTrack features"
            >
              {enabled ? "Pause" : "Enable"}
            </button>
          </div>
        </div>

        {/* VISUAL SAFETY CONTRACT */}
        <div className="section-header">
          <span className="section-title">Visual Safety Contract</span>
          <span className="section-subtitle">{visualMut} visual mutations</span>
        </div>

        <div className="presets-grid">
          <div className="preset-card selected">
            <div className="preset-icon">🛡</div>
            <div className="preset-info">
              <div className="preset-name">Vanilla visuals locked</div>
              <div className="preset-desc">No LOD, culling, terrain, shadow, decal, effect or HDRP buffer changes.</div>
            </div>
            <span className="preset-check">✓</span>
          </div>
          <div className="preset-card selected">
            <div className="preset-icon">⇧</div>
            <div className="preset-info">
              <div className="preset-name">Experimental loading boost</div>
              <div className="preset-desc">Off by default. Opt in through Options → FastTrack. No speedup is guaranteed; leave off when diagnosing GPU problems.</div>
            </div>
            <span className="preset-check">{loadBoost ? "ACTIVE" : loadBoostEnabled ? "READY" : "OFF"}</span>
          </div>
        </div>
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
          {showAdvanced ? "Hide Advanced ▴" : "Advanced ▸"}
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
            <div>LOD and render scale: not controlled or measured by FastTrack.</div>
            <div>Last city-load stage: {loadSecs > 0 ? `${loadSecs.toFixed(1)} s` : "Not measured"}</div>
            <div>Safe Mode: {safeMode ? "Locked" : "FAULT"} • Visual Mutations: {visualMut}</div>
            <div>Camera: {cameraState} (read-only) • Loading Boost: {loadBoost ? "Active" : "Inactive"}</div>
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
