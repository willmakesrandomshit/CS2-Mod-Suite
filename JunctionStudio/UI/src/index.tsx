import { PortfolioHelp, useRememberedPreference } from "./portfolio-ux";
import React, { useEffect, useState, useCallback } from "react";
import type { ModRegistrar } from "cs2/modding";
import { bindValue, trigger } from "cs2/api";
import { FloatingButton, Tooltip } from "cs2/ui";
import iconSrc from "./assets/junction-studio-icon.svg";
import "./junction-studio.scss";

const selected     = bindValue<boolean>("junctionStudio", "selected", false);
const hasClipboard = bindValue<boolean>("junctionStudio", "hasClipboard", false);
const presets      = bindValue<string>("junctionStudio", "presets", "");
const message      = bindValue<string>("junctionStudio", "message", "Junction Studio ready");
const open         = bindValue<boolean>("junctionStudio", "panelOpen", false);
const previewArmed = bindValue<boolean>("junctionStudio", "previewArmed", false);
const canRestore = bindValue<boolean>("junctionStudio", "canRestorePrevious", false);

function useB<T>(b: any): T {
  const [v, set] = useState<T>(b.value);
  useEffect(() => {
    const s = b.subscribe(set);
    set(s.value);
    return () => s.dispose();
  }, [b]);
  return v;
}

const portfolioBindings = { "selected": selected, "hasClipboard": hasClipboard, "panelOpen": open, "previewArmed": previewArmed, "canRestorePrevious": canRestore };
export const JunctionStudioToolbarButton: React.FC = () => {
  const isOpen = useB<boolean>(open);
  const toggle = useCallback(() => trigger("junctionStudio", "togglePanel"), []);

  return (
    <Tooltip tooltip="Junction Studio — Reuse Town Road Lane markings">
      <FloatingButton
        src={iconSrc}
        selected={isOpen}
        onSelect={toggle}
      />
    </Tooltip>
  );

};

export const JunctionStudioPanel: React.FC = () => {
  const visible = useB<boolean>(open);
  const ready = useB<boolean>(selected);
  const clip = useB<boolean>(hasClipboard);
  const names = useB<string>(presets).split("\n").filter(Boolean);
  const status = useB<string>(message);
  const preview = useB<boolean>(previewArmed);
  const restore = useB<boolean>(canRestore);
  const [name, setName] = useState("");
  const [showAdvanced, setShowAdvanced] = useRememberedPreference("JunctionStudio.showAdvanced", false);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);

  const close = useCallback(() => trigger("junctionStudio", "closePanel"), []);

  useEffect(() => {
    if (!visible) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        if (confirmDelete) setConfirmDelete(null);
        else if (preview) trigger("junctionStudio", "cancelPreview");
        else close();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [visible, confirmDelete, preview, close]);

  const copyJunction = () => {
    trigger("junctionStudio", "copy");
  };

  const pasteJunction = () => {
    trigger("junctionStudio", "paste");
  };

  const savePreset = () => {
    if (!name.trim()) return;
    trigger("junctionStudio", "savePreset", name.trim());
    setName("");
  };

  const applyPreset = (n: string) => {
    trigger("junctionStudio", "applyPreset", n);
  };

  const deletePreset = (n: string) => {
    if (confirmDelete !== n) {
      setConfirmDelete(n);
      setTimeout(() => setConfirmDelete((cur) => (cur === n ? null : cur)), 3500);
      return;
    }
    setConfirmDelete(null);
    trigger("junctionStudio", "deletePreset", n);
  };

  if (!visible) return null;

  return (
    <div className="suite-panel junction-studio-panel" data-portfolio-panel role="dialog" aria-label="Junction Studio Panel">
      {/* PANEL HEADER */}
      <div className="suite-header">
        <div className="header-left">
          <img src={iconSrc} alt="Junction Studio" className="header-icon" />
          <div>
            <h2 className="header-title">Junction Studio</h2>
            <span className="header-subtitle">Town Road Lane Marking Presets</span>
          </div>
        </div>
        <div className="header-right">
          <span className={`suite-badge ${ready ? "status-good" : "status-watch"}`}>
            {ready ? "Selected" : "No Selection"}
          </span>
          <button className="suite-close-btn" onClick={close} title="Close Panel" aria-label="Close panel">×</button>
        </div>
      </div>

      {/* PANEL BODY */}
      <PortfolioHelp runtimeGroup={"Portfolio.JunctionStudio"} name={"Junction Studio"} version={"1.1.4-beta.1"} steps={["Enable Town Road Lane and select a junction with its marking tool.", "Copy markings or save a named preset.", "Apply to a compatible junction and inspect the result."]} note={"Requires Town Road Lane. Magic Marking conflicts with that dependency. This tool changes visual markings, not traffic routing."} bindings={portfolioBindings} />
      <div className="suite-body">
        {preview && <div className="portfolio-help" role="status">
          <p>{status}</p>
          <button type="button" onClick={() => trigger("junctionStudio", "confirmApply")}>Confirm replacement</button>
          <button type="button" onClick={() => trigger("junctionStudio", "cancelPreview")}>Cancel</button>
        </div>}
        {restore && <button type="button" className="suite-secondary-btn" onClick={() => trigger("junctionStudio", "restorePrevious")}>Restore previous markings</button>}
        {!ready ? (
          <div className="suite-empty-state">
            <h3 className="empty-title">Select a Junction in Town Road Lane</h3>
            <p className="empty-desc">
              Open the Town Road Lane marking tool and select a road junction. Junction Studio will then let you copy its visual lines and filled areas, or save them as a named preset.
            </p>
          </div>
        ) : (
          /* SELECTED JUNCTION WORKFLOW */
          <div className="junction-view">
            {/* HERO ACTIONS */}
            <div className="hero-status-card">
              <div className="hero-status-left">
                <span className="hero-label">CURRENT SELECTION</span>
                <h3 className="hero-title status-good">Junction Markings Ready</h3>
                <p className="hero-desc">{status}</p>
              </div>
            </div>

            {/* COPY & PASTE ACTION ROW */}
            <div className="copy-paste-toolbar">
              <button
                className="suite-primary-btn"
                onClick={copyJunction}
                title="Copy the current Town Road Lane lines, segment styles, visibility and filled areas"
              >
                Copy Markings
              </button>
              <button
                className="suite-primary-btn"
                onClick={pasteJunction}
                disabled={!clip}
                title={clip ? "Paste copied markings to this junction" : "Copy junction markings first"}
              >
                Paste Markings
              </button>
            </div>

            {/* USER PRESETS */}
            <div className="section-header" style={{ marginTop: "14px" }}>
              <span className="section-title">Saved Presets</span>
              <span className="section-subtitle">{names.length} Saved</span>
            </div>

            <div className="save-preset-row">
              <input
                type="text"
                placeholder="New preset name..."
                value={name}
                onChange={(e) => setName(e.target.value)}
                onKeyDown={(e) => { if (e.key === "Enter") savePreset(); }}
                className="suite-input"
              />
              <button
                className="suite-secondary-btn"
                onClick={savePreset}
                disabled={!name.trim()}
              >
                Save
              </button>
            </div>

            {names.length > 0 && (
              <div className="saved-presets-list">
                {names.map((item) => (
                  <div key={item} className="saved-preset-item">
                    <span className="preset-label" onClick={() => applyPreset(item)}>
                      {item}
                    </span>
                    <div className="preset-item-actions">
                      <button
                        className="suite-secondary-btn compact"
                        onClick={() => applyPreset(item)}
                      >
                        Preview
                      </button>
                      <button
                        className="suite-secondary-btn compact delete"
                        onClick={() => deletePreset(item)}
                        title={confirmDelete === item ? "Click again to permanently delete" : "Delete preset"}
                      >
                        {confirmDelete === item ? "Confirm?" : "Delete"}
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {/* FOOTER */}
      <div className="suite-footer">
        <span className="footer-hint">{status}</span>
        <button
          className="advanced-toggle-btn"
          onClick={() => setShowAdvanced(!showAdvanced)}
          title="Toggle topology diagnostics"
        >
          {showAdvanced ? "Hide Advanced" : "Advanced Topology"}
        </button>
      </div>

      {/* ADVANCED DRAWER */}
      {showAdvanced && (
        <div className="advanced-drawer">
          <div className="drawer-row">
            <span>Clipboard: <strong>{clip ? "Has Copied Data" : "Empty"}</strong></span>
            <span>Selection: <strong>{ready ? "Active Node" : "None"}</strong></span>
          </div>
          <div className="drawer-telemetry">
            <div>Presets contain Town Road Lane visual marking data. Junction Studio does not edit traffic lanes, signals, priority or pathfinding.</div>
          </div>
        </div>
      )}
    </div>
  );
};

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append("GameTopLeft", JunctionStudioToolbarButton);
  moduleRegistry.append("GameTopRight", JunctionStudioPanel);
};

export default register;
