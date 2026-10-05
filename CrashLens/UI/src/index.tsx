import { PortfolioHelp, useRememberedPreference } from "./portfolio-ux";
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

const portfolioBindings = { "open": open, "showButton": show, "errors": errors, "warnings": warnings, "mods": mods, "duplicates": duplicates };
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
  const [showAdvanced, setShowAdvanced] = useRememberedPreference("CrashLens.showAdvanced", false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 2500);
  };

  const list = useMemo(() => safe<Suspect[]>(rawS, []), [rawS]);
  const events = useMemo(() => safe<string[]>(rawA, []), [rawA]);
  // "No evidence found" is a completed, limited scan, not proof that the
  // installed mods are healthy. Keep it neutral and distinguish it from the
  // initial scan so an empty list never reads as a false all-clear.
  const isScanning = h === "Scanning";
  const hasNoSupportedPatterns = h === "No evidence found" && list.length === 0;
  const statusClass = isScanning || hasNoSupportedPatterns
    ? "status-neutral"
    : h === "Watch"
      ? "status-warning"
      : h === "Action recommended" || h === "Needs attention"
        ? "status-problem"
        : "status-neutral";
  const statusLabel = isScanning
    ? "Scanning"
    : hasNoSupportedPatterns
      ? "Scan complete"
      : list.length === 1
        ? "1 Lead"
        : `${list.length} Leads`;
  const statusTitle = isScanning
    ? "Scanning log files"
    : hasNoSupportedPatterns
      ? "No Supported Patterns Found"
      : h === "Action recommended"
        ? `${list.length} Leads to Review`
        : h === "Needs attention"
          ? "Errors Found — Review Leads"
          : h === "Watch"
            ? "Warnings Found — Review Leads"
            : `${list.length} Leads Found`;

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
    <div className="suite-panel crashlens-panel" data-portfolio-panel role="dialog" aria-label="CrashLens Panel">
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
          <span className={`suite-badge ${statusClass}`}>
            {statusLabel}
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
      <PortfolioHelp runtimeGroup={"Portfolio.CrashLens"} name={"CrashLens"} version={"1.1.4-beta.1"} steps={["Wait for the log scan to finish.", "Review each lead and its supporting evidence.", "Export a report when reporting an issue."]} note={"A suspect ranking does not prove which mod caused an error. Review exported logs before sharing."} bindings={portfolioBindings} />
      <div className="suite-body">
        {/* HERO STATUS CARD */}
        <div className="hero-status-card">
          <div className="hero-status-left">
            <span className="hero-label">STABILITY DIAGNOSTICS</span>
            <h3 className={`hero-title ${statusClass}`}>
              {statusTitle}
            </h3>
            <p className="hero-desc">
              {isScanning
                ? sum || 'The first log scan is still in progress.'
                : hasNoSupportedPatterns
                  ? `${sum}. This limited scan is not proof that every mod is healthy.`
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
              <h4>{isScanning ? 'Scan in progress' : 'No leads from this scan'}</h4>
              <p>{isScanning
                ? 'CrashLens is sampling the available log files. Results will appear here when the scan finishes.'
                : 'No supported error patterns were identified in the scanned log sections. This is a limited check, not a guarantee that every mod is healthy.'}</p>
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
                    {item.confidence === 'High' ? 'Mod error' : 'Warning'}
                  </span>
                  <span className="problem-mod-name">{item.name}</span>
                </div>
                <p className="problem-reason">{item.reason || 'Unhandled exception in mod execution loop'}</p>
                <div className="problem-footer">
                  <span>{item.errors} errors | {item.warnings} warnings</span>
                  <span className="problem-details-btn">
                    {selectedSuspect?.name === item.name ? 'Hide Details' : 'View Details'}
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
          {showAdvanced ? 'Hide Details' : 'Technical Details'}
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
                  - {ev}
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
