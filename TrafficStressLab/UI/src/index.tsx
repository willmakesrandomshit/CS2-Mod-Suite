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
const readyRoutes = bindValue<number>("trafficStressTester", "readyRoutes", 0);
const roadVehicles = bindValue<number>("trafficStressTester", "roadVehicles", 0);
const batchLimit = bindValue<number>("trafficStressTester", "batchLimit", 0);
const limited = bindValue<boolean>("trafficStressTester", "limited", false);

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
  { value: 1, label: "Vanilla", desc: "Stop extra departures (1x)" },
  { value: 2, label: "Light", desc: "Add one departure for each approved road route" },
  { value: 5, label: "Heavy", desc: "Add four departures for each approved road route" },
  { value: 10, label: "Extreme", desc: "Add nine departures for each approved road route" },
  { value: 25, label: "Gridlock Stress", desc: "Add 24 departures for each approved road route, within the batch cap" },
];

const portfolioBindings = { "multiplier": mult, "enabled": enabled, "panelOpen": open, "showButton": show, "generated": generated, "readyRoutes": readyRoutes, "roadVehicles": roadVehicles, "batchLimit": batchLimit, "limited": limited };
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
  const routes = useB(readyRoutes);
  const vehicles = useB(roadVehicles);
  const cap = useB(batchLimit);
  const atCap = useB(limited);

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
    setSelectedIntensity(target);
    showToast(`Departure setting: ${item?.label || target} (${target}x)`);
  };

  const stopTest = () => {
    trigger("trafficStressTester", "reset");
    showToast("Stopped extra departures; vehicles already spawned finish their trips");
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
            <span className="header-subtitle">Road Traffic Stress Tester</span>
          </div>
        </div>
        <div className="header-right">
          <span className={`suite-badge ${isRunning ? "status-watch" : "status-good"}`}>
            {isRunning ? `${m}x Departures` : "Vanilla Departures"}
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
      <PortfolioHelp runtimeGroup={"Portfolio.TrafficStressTester"} name={"Traffic Stress Lab"} version={"1.5.7-beta.1"} steps={["Use a disposable city and start with Light pressure.", "Watch road vehicles and extra departures separately.", "Stop the test to return to vanilla departures."]} note={"Amplifies approved random road routes. Roads outside those routes may stay quiet; existing vehicles finish their trips."} bindings={portfolioBindings} />
      <div className="suite-body">
        {isRunning ? (
          /* RUNNING TEST VIEW */
          <div className="running-test-card">
            <div className="running-header">
              <span className="running-pulse-dot"></span>
              <span className="running-title">TRAFFIC TEST RUNNING</span>
            </div>
            <h2 className="running-intensity-name">
              {currentScenario.label}
            </h2>
            <p className="running-desc">
              {`${m}x departures on approved random road routes. CS2 creates and routes the vehicles; this changes departures rather than the total cars in the city.`}
            </p>
            <div className="running-stats-row">
              <div className="stat-box">
                <span className="stat-label">EXTRA DEPARTURES QUEUED</span>
                <span className="stat-value">{count.toLocaleString()}</span>
              </div>
              <div className="stat-box">
                <span className="stat-label">ROAD VEHICLES</span>
                <span className="stat-value">{vehicles.toLocaleString()}</span>
              </div>
            </div>
            <button
              className="suite-danger-btn large"
              onClick={stopTest}
              title="Stop extra departures; vehicles already spawned finish their trips"
            >
              Stop Extra Departures
            </button>
            <p className="running-desc batch-status">
              {routes === 0 ? "Waiting for the next approved road route." : atCap ? `Batch cap reached (${cap} extra departures). Increase the cap in Options for more pressure.` : `${routes} approved route(s) in the last batch. Ramp: ${speed}.`}
            </p>
            <div className="running-preset-row">
              {INTENSITY_LEVELS.filter((item) => item.value > 1).map((item) => (
                <button key={item.value} className={`intensity-card ${m === item.value ? "selected" : ""}`} onClick={() => startTest(item.value)} title={item.desc}>{`${item.value}x`}</button>
              ))}
            </div>
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
          {isRunning ? `Amplifying road departures at ${m}x` : "Ready — vanilla departures"}
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
            <div>Extra Departures Queued: {count.toLocaleString()}</div>
            <div>Last Batch: {routes} approved routes · cap {cap}</div>
            <div>Road Vehicles Observed: {vehicles.toLocaleString()}</div>
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
