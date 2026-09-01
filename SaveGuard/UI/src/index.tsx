import React, { useState, useEffect, useCallback } from 'react';
import { ModRegistrar } from 'cs2/modding';
import { bindValue, trigger } from 'cs2/api';
import { FloatingButton, Tooltip } from 'cs2/ui';
import iconSrc from './assets/saveguard-icon.svg';
import './style.scss';

interface SaveTimelineItem {
  fileName: string;
  cityName: string;
  fullPath: string;
  sizeMb: number;
  timestamp: string;
  health: string;
  saveType: string;
}

const openBinding = bindValue<boolean>('SaveGuard', 'panelOpen', false);
const statusBinding = bindValue<string>('SaveGuard', 'status', 'Select a save or restore point to inspect it');
const saveCountBinding = bindValue<number>('SaveGuard', 'saveCount', 0);

const selFileNameBinding = bindValue<string>('SaveGuard', 'selFileName', 'None');
const selCityNameBinding = bindValue<string>('SaveGuard', 'selCityName', 'None');
const selFullPathBinding = bindValue<string>('SaveGuard', 'selFullPath', '-');
const selSizeMbBinding = bindValue<number>('SaveGuard', 'selSizeMb', 0);
const selTimestampBinding = bindValue<string>('SaveGuard', 'selTimestamp', '-');
const selHealthBinding = bindValue<string>('SaveGuard', 'selHealth', '-');
const selTypeBinding = bindValue<string>('SaveGuard', 'selType', '-');

const diagSummaryBinding = bindValue<string>('SaveGuard', 'diagSummary', 'Save file integrity validated');
const diagCategoryBinding = bindValue<string>('SaveGuard', 'diagCategory', 'Healthy');
const diagActionBinding = bindValue<string>('SaveGuard', 'diagAction', 'No action needed');
const diagIntegrityBinding = bindValue<string>('SaveGuard', 'diagIntegrity', '100%');
const diagIsCorruptedBinding = bindValue<boolean>('SaveGuard', 'diagIsCorrupted', false);
const testRecoveryResultBinding = bindValue<string>('SaveGuard', 'testRecoveryResult', '');

const timelineBinding = bindValue<unknown>('SaveGuard', 'timeline', []);

const asText = (value: unknown, fallback = ''): string =>
  typeof value === 'string' ? value : fallback;

const asNumber = (value: unknown, fallback = 0): number =>
  typeof value === 'number' && Number.isFinite(value) ? value : fallback;

const parseTimeline = (raw: unknown): SaveTimelineItem[] => {
  let parsed: unknown = raw;

  if (typeof raw === 'string') {
    if (raw.length === 0) return [];
    try {
      parsed = JSON.parse(raw);
    } catch {
      return [];
    }
  }

  if (!Array.isArray(parsed)) return [];

  return parsed
    .filter((item): item is Record<string, unknown> => item !== null && typeof item === 'object')
    .map((item) => ({
      fileName: asText(item.fileName, 'Unknown save'),
      cityName: asText(item.cityName),
      fullPath: asText(item.fullPath),
      sizeMb: asNumber(item.sizeMb),
      timestamp: asText(item.timestamp, 'Recent'),
      health: asText(item.health, 'UNKNOWN'),
      saveType: asText(item.saveType, 'Save'),
    }));
};

export const SaveGuardToolbarButton: React.FC = () => {
  const [isOpen, setIsOpen] = useState(openBinding.value);

  useEffect(() => {
    const sub = openBinding.subscribe(setIsOpen);
    return () => sub.dispose();
  }, []);

  const toggle = useCallback(() => trigger('SaveGuard', 'togglePanel'), []);

  return (
    <Tooltip tooltip="SaveGuard — Check and protect your saves">
      <FloatingButton
        src={iconSrc}
        selected={isOpen}
        onSelect={toggle}
      />
    </Tooltip>
  );

};

export const SaveGuardPanel: React.FC = () => {
  const [isOpen, setIsOpen] = useState(openBinding.value);
  const [status, setStatus] = useState(statusBinding.value);
  const [saveCount, setSaveCount] = useState(saveCountBinding.value);

  const [selFileName, setSelFileName] = useState(selFileNameBinding.value);
  const [selCityName, setSelCityName] = useState(selCityNameBinding.value);
  const [selFullPath, setSelFullPath] = useState(selFullPathBinding.value);
  const [selSizeMb, setSelSizeMb] = useState(selSizeMbBinding.value);
  const [selTimestamp, setSelTimestamp] = useState(selTimestampBinding.value);
  const [selHealth, setSelHealth] = useState(selHealthBinding.value);
  const [selType, setSelType] = useState(selTypeBinding.value);

  const [diagSummary, setDiagSummary] = useState(diagSummaryBinding.value);
  const [diagCategory, setDiagCategory] = useState(diagCategoryBinding.value);
  const [diagAction, setDiagAction] = useState(diagActionBinding.value);
  const [diagIntegrity, setDiagIntegrity] = useState(diagIntegrityBinding.value);
  const [diagIsCorrupted, setDiagIsCorrupted] = useState(diagIsCorruptedBinding.value);
  const [testRecoveryResult, setTestRecoveryResult] = useState(testRecoveryResultBinding.value);

  const [timelineRaw, setTimelineRaw] = useState<unknown>(timelineBinding.value ?? []);
  const [confirmRestoreItem, setConfirmRestoreItem] = useState<SaveTimelineItem | null>(null);
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 2500);
  };

  const close = useCallback(() => trigger('SaveGuard', 'closePanel'), []);

  useEffect(() => {
    const s00 = openBinding.subscribe(setIsOpen);
    const s01 = statusBinding.subscribe(setStatus);
    const s02 = saveCountBinding.subscribe(setSaveCount);

    const s03 = selFileNameBinding.subscribe(setSelFileName);
    const s04 = selCityNameBinding.subscribe(setSelCityName);
    const s05 = selFullPathBinding.subscribe(setSelFullPath);
    const s06 = selSizeMbBinding.subscribe(setSelSizeMb);
    const s07 = selTimestampBinding.subscribe(setSelTimestamp);
    const s08 = selHealthBinding.subscribe(setSelHealth);
    const s09 = selTypeBinding.subscribe(setSelType);

    const s10 = diagSummaryBinding.subscribe(setDiagSummary);
    const s11 = diagCategoryBinding.subscribe(setDiagCategory);
    const s12 = diagActionBinding.subscribe(setDiagAction);
    const s13 = diagIntegrityBinding.subscribe(setDiagIntegrity);
    const s14 = diagIsCorruptedBinding.subscribe(setDiagIsCorrupted);
    const s15 = testRecoveryResultBinding.subscribe(setTestRecoveryResult);

    const s16 = timelineBinding.subscribe((value) => setTimelineRaw(value ?? []));

    return () => {
      s00.dispose(); s01.dispose(); s02.dispose(); s03.dispose(); s04.dispose();
      s05.dispose(); s06.dispose(); s07.dispose(); s08.dispose(); s09.dispose();
      s10.dispose(); s11.dispose(); s12.dispose(); s13.dispose(); s14.dispose();
      s15.dispose(); s16.dispose();
    };
  }, []);

  const timeline = parseTimeline(timelineRaw);

  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (confirmRestoreItem) setConfirmRestoreItem(null);
        else close();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, confirmRestoreItem, close]);

  const selectSave = (fullPath: string) => {
    trigger('SaveGuard', 'selectSave', fullPath);
  };

  const refreshSaves = () => {
    trigger('SaveGuard', 'refresh');
    showToast('Save list refresh requested');
  };

  const createBackup = () => {
    trigger('SaveGuard', 'createRestorePoint');
    showToast('Restore-point creation requested — check the status below');
  };

  const executeRestore = (item: SaveTimelineItem) => {
    trigger('SaveGuard', 'selectSave', item.fullPath);
    trigger('SaveGuard', 'restoreAsCopy');
    setConfirmRestoreItem(null);
    showToast('Restored-copy creation requested — the original will remain untouched');
  };

  const runTestRecovery = () => {
    trigger('SaveGuard', 'testRecovery');
    showToast('Archive test requested — check the result below');
  };

  if (!isOpen) return null;

  return (
    <div className="suite-panel saveguard-panel" role="dialog" aria-label="SaveGuard Panel">
      {/* PANEL HEADER */}
      <div className="suite-header">
        <div className="header-left">
          <img src={iconSrc} alt="SaveGuard" className="header-icon" />
          <div>
            <h2 className="header-title">SaveGuard</h2>
            <span className="header-subtitle">Manual Save Inspection & Restore Copies</span>
          </div>
        </div>
        <div className="header-right">
          <span className={`suite-badge ${diagIsCorrupted ? 'status-problem' : 'status-good'}`}>
            {diagIsCorrupted ? 'Warning' : 'Ready'}
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
            <span className="hero-label">SAVE FILE HEALTH</span>
            <h3 className={`hero-title ${diagIsCorrupted ? 'status-problem' : 'status-good'}`}>
              {diagIsCorrupted ? 'Issues Detected' : 'Selected Save Check'}
            </h3>
            <p className="hero-desc">
              {diagSummary || 'Select a save to inspect its archive structure, then create a manual restore point if needed.'}
            </p>
          </div>
          <div className="hero-status-right">
            <button
              className="suite-primary-btn"
              onClick={createBackup}
              title="Create an immediate safety backup of current city"
            >
              + Create Restore Point
            </button>
          </div>
        </div>

        {/* RESTORE POINTS SECTION */}
        <div className="section-header">
          <span className="section-title">Restore Points</span>
          <span className="section-subtitle">{timeline.length} Available</span>
        </div>

        <div className="timeline-list">
          {timeline.length === 0 ? (
            <div className="empty-timeline-box">
              <span className="empty-icon">💾</span>
              <p>No extra restore points found yet. Click <strong>Create Restore Point</strong> above to make a safety snapshot.</p>
            </div>
          ) : (
            timeline.map((item, i) => (
              <div
                key={i}
                className={`timeline-card ${selFileName === item.fileName ? 'selected' : ''}`}
                onClick={() => selectSave(item.fullPath)}
              >
                <div className="timeline-info">
                  <div className="timeline-top">
                    <span className="timeline-name">{item.cityName || item.fileName}</span>
                    <span className={`timeline-badge ${item.health === 'Healthy' ? 'good' : 'warning'}`}>
                      {item.saveType || 'Autosave'}
                    </span>
                  </div>
                  <div className="timeline-meta">
                    <span>{item.timestamp || 'Recent'}</span>
                    <span>•</span>
                    <span>{item.sizeMb ? `${item.sizeMb.toFixed(1)} MB` : 'Validated'}</span>
                  </div>
                </div>
                <div className="timeline-actions">
                  <button
                    className="suite-secondary-btn compact"
                    onClick={(e) => {
                      e.stopPropagation();
                      selectSave(item.fullPath);
                      setConfirmRestoreItem(item);
                    }}
                    title="Restore this save snapshot"
                  >
                    Restore
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      </div>

      {/* CONFIRMATION MODAL */}
      {confirmRestoreItem && (
        <div className="suite-modal-overlay">
          <div className="suite-modal-box">
            <div className="modal-icon">⚠️</div>
            <h3 className="modal-title">Create a Restored Copy?</h3>
            <p className="modal-desc">
              SaveGuard will copy <strong>{confirmRestoreItem.fileName}</strong> into the CS2 Saves folder with a new <strong>_RESTORED_</strong> filename. It will not overwrite the source or load the copy automatically.
            </p>
            <div className="modal-actions">
              <button
                className="suite-secondary-btn"
                onClick={() => setConfirmRestoreItem(null)}
              >
                Cancel
              </button>
              <button
                className="suite-danger-btn"
                onClick={() => executeRestore(confirmRestoreItem)}
              >
                Create Restored Copy
              </button>
            </div>
          </div>
        </div>
      )}

      {/* FOOTER */}
      <div className="suite-footer">
        <span className="footer-hint">{status}</span>
        <button
          className="advanced-toggle-btn"
          onClick={() => setShowAdvanced(!showAdvanced)}
          title="Toggle diagnostic telemetry"
        >
          {showAdvanced ? 'Hide Advanced ▴' : 'Advanced ▸'}
        </button>
      </div>

      {/* ADVANCED DRAWER */}
      {showAdvanced && (
        <div className="advanced-drawer">
          <div className="drawer-row">
            <span>Selected File: <code>{selFileName}</code></span>
            <span>Integrity: <strong>{diagIntegrity}</strong></span>
          </div>
          <div className="drawer-row">
            <span>Category: {diagCategory}</span>
            <span>Action: {diagAction}</span>
          </div>
          <div className="drawer-telemetry">
            <div>Path: <code>{selFullPath}</code></div>
            {testRecoveryResult && <div style={{ marginTop: '4px', color: '#38bdf8' }}>Test Recovery: {testRecoveryResult}</div>}
          </div>
          <div style={{ marginTop: '8px', display: 'flex', gap: '8px' }}>
            <button className="suite-secondary-btn compact" onClick={refreshSaves}>
              Refresh Save List
            </button>
            <button className="suite-secondary-btn compact" onClick={runTestRecovery}>
              Test Archive Structure
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

class SaveGuardPanelBoundary extends React.Component<React.PropsWithChildren<{}>, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  componentDidCatch(error: unknown) {
    console.error('[SaveGuard] Panel render recovered from invalid UI data.', error);
  }

  private recover = () => {
    this.setState({ failed: false });
    trigger('SaveGuard', 'refresh');
  };

  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <div className="suite-panel saveguard-panel" role="alert">
        <div className="suite-header">
          <div className="header-left"><h2 className="header-title">SaveGuard</h2></div>
          <button className="suite-close-btn" onClick={() => trigger('SaveGuard', 'closePanel')} title="Close Panel">✕</button>
        </div>
        <div className="suite-body">
          <div className="empty-timeline-box">
            <p>SaveGuard received incomplete UI data. Refresh the save list to continue.</p>
            <button className="suite-primary-btn" onClick={this.recover}>Refresh SaveGuard</button>
          </div>
        </div>
      </div>
    );
  }
}

const SaveGuardPanelWithBoundary: React.FC = () => (
  <SaveGuardPanelBoundary><SaveGuardPanel /></SaveGuardPanelBoundary>
);

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append('GameTopLeft', SaveGuardToolbarButton);
  moduleRegistry.append('GameTopRight', SaveGuardPanelWithBoundary);
};

export default register;
