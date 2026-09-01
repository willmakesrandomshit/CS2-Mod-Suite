import React, { useEffect, useMemo, useState, useCallback } from "react";
import type { ModRegistrar } from "cs2/modding";
import { bindValue, trigger } from "cs2/api";
import { FloatingButton, Tooltip } from "cs2/ui";
import iconSrc from "./assets/crashlens-icon.svg";
import "./style.scss";

const open       = bindValue<boolean>("crashLens", "open", false);
const show       = bindValue<boolean>("crashLens", "showButton", true);
const health     = bindValue<string>("crashLens", "health", "Scanning");
const summary    = bindValue<string>("crashLens", "summary", "Building session baseline");
const errors     = bindValue<number>("crashLens", "errors", 0);
const warnings   = bindValue<number>("crashLens", "warnings", 0);
const mods       = bindValue<number>("crashLens", "mods", 0);
const duplicates = bindValue<number>("crashLens", "duplicates", 0);
const suspects   = bindValue<string>("crashLens", "suspects", "[]");
const activity   = bindValue<string>("crashLens", "activity", "[]");
const reportPath = bindValue<string>("crashLens", "reportPath", "");

function useB<T>(b: { value: T; subscribe(c: (v: T) => void): { value: T; dispose(): void } }) {
  const [v, set] = useState(b.value);
  useEffect(() => {
    const s = b.subscribe(set);
    set(s.value);
    return () => s.dispose();
  }, [b]);
  return v;
}

type Suspect = {
  name: string;
  confidence: string;
  score: number;
  errors: number;
  warnings: number;
  reason: string;
};

const safe = <T,>(raw: string, fallback: T): T => {
  try {
    return JSON.parse(raw) as T;
  } catch (e) {
    return fallback;
  }
};

export const CrashLensToolbarButton: React.FC = () => {
  const isOpen = useB(open);
  const showBtn = useB(show);
  const toggle = useCallback(() => trigger("crashLens", "toggle"), []);

  if (!showBtn) return null;

  return (
    <Tooltip tooltip="CrashLens — Investigate errors and mod problems">
      <FloatingButton
        src={iconSrc}
        selected={isOpen}
        onSelect={toggle}
      />
    </Tooltip>
  );

};

export const CrashLensPanel: React.FC = () => {
  const isOpen = useB(open);
  const h = useB(health);
  const sum = useB(summary);
  const e = useB(errors);
  const w = useB(warnings);
  const m = useB(mods);
  const d = useB(duplicates);
  const rawS = useB(suspects);
  const rawA = useB(activity);
  const path = useB(reportPath);

  const [selectedSuspect, setSelectedSuspect] = useState<Suspect | null>(null);
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 2500);
  };

  const list = useMemo(() => safe<Suspect[]>(rawS, []), [rawS]);
  const events = useMemo(() => safe<string[]>(rawA, []), [rawA]);
  const isHealthy = h === "Healthy" && list.length === 0;

  const close = useCallback(() => trigger("crashLens", "close"), []);

  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (ev: KeyboardEvent) => {
      if (ev.key === "Escape") {
        if (selectedSuspect) setSelectedSuspect(null);
        else close();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, selectedSuspect, close]);

  const exportReport = () => {
    trigger("crashLens", "export");
    showToast("Report export requested — the path appears below when ready");
  };

  const rescan = () => {
    trigger("crashLens", "rescan");
    showToast("Log rescan requested");
  };

  if (!isOpen) return null;

  return (
    <div className="suite-panel crashlens-panel" role="dialog" aria-label="CrashLens Panel">
      {/* PANEL HEADER */}
      <div className="suite-header">
        <div className="header-left">
          <img src={iconSrc} alt="CrashLens" className="header-icon" />
          <div>
            <h2 className="header-title">CrashLens</h2>
            <span className="header-subtitle">Mod & Crash Diagnostics</span>
          </div>
        </div>
        <div className="header-right">
          <span className={`suite-badge ${isHealthy ? 'status-good' : 'status-problem'}`}>
            {isHealthy ? 'No Leads' : `${list.length} Leads`}
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
            <span className="hero-label">STABILITY DIAGNOSTICS</span>
            <h3 className={`hero-title ${isHealthy ? 'status-good' : 'status-problem'}`}>
              {isHealthy ? 'No Supported Issue Patterns Found' : `${list.length} Leads Found`}
            </h3>
            <p className="hero-desc">
              {isHealthy
                ? `CrashLens found no supported error patterns in the logs it scanned. This is not proof that every mod is healthy.`
                : `${e} errors and ${w} warnings were recorded. Review the evidence-ranked leads below; a lead is not automatic proof.`}
            </p>
          </div>
          <div className="hero-status-right">
            <button
              className="suite-primary-btn"
              onClick={exportReport}
              title="Generate comprehensive diagnostic report"
            >
              Export Report
            </button>
          </div>
        </div>

        {/* DETECTED PROBLEMS LIST */}
        <div className="section-header">
          <span className="section-title">Detected Issues</span>
          <span className="section-subtitle">{list.length} Flagged</span>
        </div>

        <div className="problems-list">
          {list.length === 0 ? (
            <div className="clean-status-box">
              <span className="clean-icon">🛡️</span>
              <h4>No Leads From This Scan</h4>
              <p>No supported crash patterns, missing dependencies, or duplicate assemblies were identified in the scanned logs.</p>
            </div>
          ) : (
            list.map((item, idx) => (
              <div
                key={idx}
                className={`problem-card ${item.confidence === 'High' ? 'critical' : 'warning'}`}
                onClick={() => setSelectedSuspect(selectedSuspect?.name === item.name ? null : item)}
              >
                <div className="problem-header">
                  <span className="problem-tag">
                    {item.confidence === 'High' ? '🔴 Mod Error' : '🟡 Warning'}
                  </span>
                  <span className="problem-mod-name">{item.name}</span>
                </div>
                <p className="problem-reason">{item.reason || 'Unhandled exception in mod execution loop'}</p>
                <div className="problem-footer">
                  <span>{item.errors} errors • {item.warnings} warnings</span>
                  <span className="problem-details-btn">
                    {selectedSuspect?.name === item.name ? 'Hide Details ▴' : 'View Details ▸'}
                  </span>
                </div>

                {selectedSuspect?.name === item.name && (
                  <div className="problem-expanded-box">
                    <div className="expanded-row">
                      <span>Confidence Score:</span>
                      <strong>{item.score}% ({item.confidence})</strong>
                    </div>
                    <div className="expanded-desc">
                      Recommended Action: Verify this mod is updated for the current game patch. If crashes persist, temporarily disable this mod.
                    </div>
                  </div>
                )}
              </div>
            ))
          )}
        </div>

        {/* QUICK ACTIONS */}
        <div className="action-toolbar">
          <button className="suite-secondary-btn compact" onClick={rescan} title="Scan the current and previous logs again">
            Rescan Logs
          </button>
          {path && (
            <button
              className="suite-secondary-btn compact"
              onClick={() => trigger("crashLens", "openFolder")}
              title="Open the local CrashLens report folder"
            >
              Open Report Folder
            </button>
          )}
          {path && (
            <span className="report-hint">Report: <code>{path.split('\\').pop()}</code></span>
          )}
        </div>
      </div>

      {/* FOOTER */}
      <div className="suite-footer">
        <span className="footer-hint">{sum}</span>
        <button
          className="advanced-toggle-btn"
          onClick={() => setShowAdvanced(!showAdvanced)}
          title="Toggle raw activity stream"
        >
          {showAdvanced ? 'Hide Details ▴' : 'Technical Details ▸'}
        </button>
      </div>

      {/* ADVANCED DRAWER */}
      {showAdvanced && (
        <div className="advanced-drawer">
          <div className="drawer-row">
            <span>Active Mods: <strong>{m}</strong></span>
            <span>Duplicates: <strong>{d}</strong></span>
          </div>
          <div className="drawer-telemetry">
            <div>Recent Activity Stream:</div>
            {events.length === 0 ? (
              <div style={{ color: '#64748b', marginTop: '4px' }}>No raw events logged</div>
            ) : (
              events.slice(0, 5).map((ev, i) => (
                <div key={i} style={{ fontSize: '10px', marginTop: '2px', color: '#94a3b8' }}>
                  • {ev}
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
};

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append('GameTopLeft', CrashLensToolbarButton);
  moduleRegistry.append('GameTopRight', CrashLensPanel);
};

export default register;
