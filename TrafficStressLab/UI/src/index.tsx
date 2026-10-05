import { PortfolioHelp, useRememberedPreference } from "./portfolio-ux";
import React, { useEffect, useState, useCallback } from "react";
import type { ModRegistrar } from "cs2/modding";
import { bindValue, trigger } from "cs2/api";
import { FloatingButton, Tooltip } from "cs2/ui";
import iconSrc from "./assets/traffic-stress-icon.svg";
import "./traffic-stress.scss";

const mult = bindValue<number>("trafficStressTester", "multiplier", 1);
const enabled = bindValue<boolean>("trafficStressTester", "enabled", false);
const open = bindValue<boolean>("trafficStressTester", "panelOpen", false);
const show = bindValue<boolean>("trafficStressTester", "showButton", true);
const generated = bindValue<number>("trafficStressTester", "generated", 0);
const ramp = bindValue<string>("trafficStressTester", "ramp", "Fast");

function useB<T>(b: { value: T; subscribe(c: (v: T) => void): { value: T; dispose(): void } }): T {
  const [v, set] = useState<T>(b.value);
  useEffect(() => {
    const s = b.subscribe(set);
    set(s.value);
    return () => s.dispose();
  }, [b]);
  return v;
}

const INTENSITY_LEVELS = [
  { value: 1, label: "Vanilla", desc: "Stop extra request generation (1x)" },
  { value: 2, label: "Light", desc: "Generate additional requests at the 2x setting" },
  { value: 5, label: "Heavy", desc: "Generate additional requests at the 5x setting" },
  { value: 10, label: "Extreme", desc: "Generate additional requests at the 10x setting" },
  { value: 25, label: "Gridlock Stress", desc: "Aggressive 25x request pressure with a larger bounded queue" },
];

const portfolioBindings = { "multiplier": mult, "enabled": enabled, "panelOpen": open, "showButton": show, "generated": generated };
export const TrafficStressToolbarButton: React.FC = () => {
  const visible = useB(show);
  const isOpen = useB(open);
  if (!visible) return null;

  return (
    <Tooltip tooltip="Traffic Stress Lab — Test your road network under traffic">
      <FloatingButton
        src={iconSrc}
        selected={isOpen}
        onSelect={() => trigger("trafficStressTester", "togglePanel")}
      />
    </Tooltip>
  );

};

export const TrafficStressPanel: React.FC = () => {
  const m = useB(mult);
  const on = useB(enabled);
  const visible = useB(open);
  const count = useB(generated);
  const speed = useB(ramp);

  const [selectedIntensity, setSelectedIntensity] = useState<number>(m > 1 ? m : 2);
  const [showAdvanced, setShowAdvanced] = useRememberedPreference("TrafficStressLab.showAdvanced", false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 2500);
  };

  const close = useCallback(() => trigger("trafficStressTester", "closePanel"), []);

  useEffect(() => {
    if (!visible) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") close();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [visible, close]);

  const isRunning = on && m > 1;
  const currentScenario = INTENSITY_LEVELS.find((s) => s.value === m) || INTENSITY_LEVELS[0];

  const startTest = (val?: number) => {
    const target = val !== undefined ? val : selectedIntensity;
    trigger("trafficStressTester", "setMultiplier", target);
    const item = INTENSITY_LEVELS.find((s) => s.value === target);
    showToast(`Request multiplier set to ${item?.label || target} (${target}x)`);
  };

  const stopTest = () => {
    trigger("trafficStressTester", "reset");
    showToast("Stopped new extra requests; queued requests and existing vehicles will finish normally");
  };

  if (!visible) return null;

  return (
    <div className="suite-panel traffic-stress-panel" data-portfolio-panel role="dialog" aria-label="Traffic Stress Lab Panel">
      {/* PANEL HEADER */}
      <div className="suite-header">
        <div className="header-left">
          <img src={iconSrc} alt="Traffic Stress Lab" className="header-icon" />
          <div>
            <h2 className="header-title">Traffic Stress Lab</h2>
            <span className="header-subtitle">Additional Traffic-Request Tester</span>
          </div>
        </div>
        <div className="header-right">
          <span className={`suite-badge ${isRunning ? "status-watch" : "status-good"}`}>
            {isRunning ? `${m}x Requests` : "No Extra Requests"}
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
      <PortfolioHelp runtimeGroup={"Portfolio.TrafficStressTester"} name={"Traffic Stress Lab"} version={"1.5.5-beta.1"} steps={["Use a disposable city and start with Light pressure.", "Watch request counts and actual traffic separately.", "Stop the test to stop generating new extra requests."]} note={"Queued requests and existing vehicles finish normally after stopping. Requests are not guaranteed visible vehicles."} bindings={portfolioBindings} />
      <div className="suite-body">
        {isRunning ? (
          /* RUNNING TEST VIEW */
          <div className="running-test-card">
            <div className="running-header">
              <span className="running-pulse-dot"></span>
              <span className="running-title">TRAFFIC TEST RUNNING</span>
            </div>
            <h2 className="running-intensity-name">
              {currentScenario.label} ({m}x Request Setting)
            </h2>
            <p className="running-desc">
              Submitting additional random-traffic requests to CS2's normal dispatcher. The setting is not a guaranteed vehicle-count multiplier; spawning still depends on the city and dispatcher.
            </p>
            <div className="running-stats-row">
              <div className="stat-box">
                <span className="stat-label">EXTRA REQUESTS SUBMITTED</span>
                <span className="stat-value">{count.toLocaleString()}</span>
              </div>
              <div className="stat-box">
                <span className="stat-label">DISPATCH RAMP</span>
                <span className="stat-value">{speed}</span>
              </div>
            </div>
            <button
              className="suite-danger-btn large"
              onClick={stopTest}
              title="Stop generating new extra requests; already queued requests and vehicles continue normally"
            >
              Stop New Requests
            </button>
          </div>
        ) : (
          /* IDLE TEST CONFIGURATION */
          <div className="idle-test-view">
            <div className="section-header">
              <span className="section-title">Traffic Intensity</span>
              <span className="section-subtitle">Select stress level</span>
            </div>

            <div className="intensity-selector-grid">
              {INTENSITY_LEVELS.map((item) => {
                const isSelected = selectedIntensity === item.value;
                return (
                  <button
                    key={item.value}
                    className={`intensity-card ${isSelected ? "selected" : ""}`}
                    onClick={() => setSelectedIntensity(item.value)}
                    title={item.desc}
                  >
                    <span className="intensity-label">{item.label}</span>
                    <span className="intensity-mult">{item.value}x</span>
                  </button>
                );
              })}
            </div>

            <div className="test-action-box">
              <button
                className="suite-primary-btn large"
                onClick={() => startTest(selectedIntensity)}
                title="Start traffic stress simulation"
              >
                Start Test ({selectedIntensity}x)
              </button>
              <p className="footer-hint">Use a disposable test city first. High settings can congest the network and reduce simulation speed.</p>
            </div>
          </div>
        )}
      </div>

      {/* FOOTER */}
      <div className="suite-footer">
        <span className="footer-hint">
          {isRunning ? `Submitting requests at the ${m}x setting` : "Ready — no extra requests are being generated"}
        </span>
        <button
          className="advanced-toggle-btn"
          onClick={() => setShowAdvanced(!showAdvanced)}
          title="Toggle advanced simulation options"
        >
          {showAdvanced ? "Hide Advanced" : "Advanced"}
        </button>
      </div>

      {/* ADVANCED DRAWER */}
      {showAdvanced && (
        <div className="advanced-drawer">
          <div className="drawer-row">
            <span>Ramp Speed: <strong>{speed}</strong></span>
            <span>Multiplier: <code>{m}x</code></span>
          </div>
          <div className="drawer-telemetry">
            <div>Vanilla Traffic Baseline: 1.0x</div>
            <div>Extra Requests Submitted: {count.toLocaleString()}</div>
          </div>
          <div style={{ marginTop: "8px", display: "flex", gap: "8px" }}>
            <button
              className="suite-secondary-btn compact"
              onClick={() => trigger("trafficStressTester", "reset")}
            >
              Reset Simulation State
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append("GameTopLeft", TrafficStressToolbarButton);
  moduleRegistry.append("GameTopRight", TrafficStressPanel);
};

export default register;
