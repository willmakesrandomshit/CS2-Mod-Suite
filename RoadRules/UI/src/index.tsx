import { PortfolioHelp, useRememberedPreference } from "./portfolio-ux";
import React, { useState, useEffect, useCallback } from 'react';
import { ModRegistrar } from 'cs2/modding';
import { bindValue, trigger } from 'cs2/api';
import { FloatingButton, Tooltip } from 'cs2/ui';
import iconSrc from './assets/roadrules-icon.svg';
import './style.scss';

interface LaneInfo {
  laneIndex: number;
  allowedVehicles: number;
  isClosed: boolean;
  isLocalAccess: boolean;
  preset: string;
  lanePositionLabel?: string;
  lateralOffset?: number;
  routingCount?: number;
  primaryEntityId?: number;
}

const openBinding = bindValue<boolean>('RoadRules', 'panelOpen', false);
const toolActiveBinding = bindValue<boolean>('RoadRules', 'toolActive', false);
const activeToolNameBinding = bindValue<string>('RoadRules', 'activeToolName', 'DefaultTool');
const lastRawEntityBinding = bindValue<string>('RoadRules', 'lastRawEntity', 'None');
const lastResolvedEdgeBinding = bindValue<string>('RoadRules', 'lastResolvedEdge', 'None');
const lastFailureReasonBinding = bindValue<string>('RoadRules', 'lastFailureReason', 'None');
const lastRawComponentsBinding = bindValue<string>('RoadRules', 'lastRawComponents', 'None');
const modVersionBinding = bindValue<string>('RoadRules', 'modVersion', '1.4.4 BETA');

const statusBinding = bindValue<string>('RoadRules', 'status', 'Road Rules ready');
const hasSelectionBinding = bindValue<boolean>('RoadRules', 'hasSelection', false);
const selectedEdgeIdBinding = bindValue<number>('RoadRules', 'selectedEdgeId', 0);
const carriagewayNameBinding = bindValue<string>('RoadRules', 'carriagewayName', 'Selected Carriageway');
const totalSubLanesBinding = bindValue<number>('RoadRules', 'totalSubLanes', 0);
const rawCarLanesBinding = bindValue<number>('RoadRules', 'rawCarLanes', 0);
const directionMatchedCarLanesBinding = bindValue<number>('RoadRules', 'directionMatchedCarLanes', 0);
const carriagewayMatchedCarLanesBinding = bindValue<number>('RoadRules', 'carriagewayMatchedCarLanes', 0);
const filteredOutSubLanesBinding = bindValue<number>('RoadRules', 'filteredOutSubLanes', 0);
const physicalLaneDebugBinding = bindValue<string>('RoadRules', 'physicalLaneDebug', 'None');
const propagationActiveBinding = bindValue<boolean>('RoadRules', 'propagationActive', false);
const propagationMessageBinding = bindValue<string>('RoadRules', 'propagationMessage', '');

const rebuildsCountBinding = bindValue<number>('RoadRules', 'rebuildsCount', 0);
const vehiclesReroutedBinding = bindValue<number>('RoadRules', 'vehiclesRerouted', 0);
const activeEnforcedBinding = bindValue<number>('RoadRules', 'activeEnforced', 0);
const existingPathsSignaledBinding = bindValue<number>('RoadRules', 'existingPathsSignaled', 0);
const nativeUpdateRequestedBinding = bindValue<boolean>('RoadRules', 'nativeUpdateRequested', false);
const enforcementSummaryBinding = bindValue<string>('RoadRules', 'enforcementSummary', 'VANILLA');
const observedCarsBinding = bindValue<number>('RoadRules', 'observedCars', 0);
const observedTrucksBinding = bindValue<number>('RoadRules', 'observedTrucks', 0);
const observedBusesBinding = bindValue<number>('RoadRules', 'observedBuses', 0);
const observedTaxisBinding = bindValue<number>('RoadRules', 'observedTaxis', 0);
const observedEmergencyBinding = bindValue<number>('RoadRules', 'observedEmergency', 0);
const observedServicesBinding = bindValue<number>('RoadRules', 'observedServices', 0);
const observedViolationsBinding = bindValue<number>('RoadRules', 'observedViolations', 0);

const laneCountBinding = bindValue<number>('RoadRules', 'laneCount', 0);
const selectedLaneIdxBinding = bindValue<number>('RoadRules', 'selectedLaneIdx', 0);
const allowedVehiclesBinding = bindValue<number>('RoadRules', 'allowedVehicles', 63);
const isLocalAccessBinding = bindValue<boolean>('RoadRules', 'isLocalAccess', false);
const isClosedBinding = bindValue<boolean>('RoadRules', 'isClosed', false);
const presetNameBinding = bindValue<string>('RoadRules', 'presetName', 'Default');
const hasClipboardBinding = bindValue<boolean>('RoadRules', 'hasClipboard', false);
const lanesBinding = bindValue<LaneInfo[]>('RoadRules', 'lanes', []);

const VEHICLE_FLAGS = [
  { flag: 1, label: 'Cars', icon: 'CAR', tooltip: 'Private road vehicles. Blocked by the native public-lane gate.' },
  { flag: 2, label: 'Heavy Traffic', icon: 'HGV', tooltip: 'Freight is discouraged by CS2’s native heavy-traffic rule; this is soft avoidance.' },
  { flag: 4, label: 'Buses', icon: 'BUS', tooltip: 'Part of CS2’s shared public-lane category.' },
  { flag: 8, label: 'Taxis', icon: 'TAXI', tooltip: 'Part of CS2’s shared public-lane category.' },
  { flag: 16, label: 'Emergency', icon: 'EMS', tooltip: 'Part of CS2’s shared public-lane category and not independently blocked.' },
  { flag: 32, label: 'Services', icon: 'SVC', tooltip: 'Supported service vehicles share CS2’s public-lane category.' },
];

const QUICK_PRESETS = [
  { id: 'DefaultAll', label: 'All Allowed', desc: 'Restore the selected lane’s exact vanilla state', all: false },
  { id: 'TruckBan', label: 'No Heavy Traffic', desc: 'Native soft avoidance for heavy vehicles', all: false },
  { id: 'TransitOnly', label: 'Public/Service Lane', desc: 'Native shared gate: buses, taxis, emergency and supported service vehicles', all: false },
  { id: 'LocalAccessOnly', label: 'Local Bias (Experimental)', desc: 'Soft route bias; true destination-only semantics are not exposed', all: false },
  { id: 'LaneClosed', label: 'Close Lane', desc: 'Hard-block the selected physical lane', all: false },
  { id: 'LaneClosed', label: 'Close Road', desc: 'Hard-block every physical lane on this carriageway', all: true },
];

const portfolioBindings = { "panelOpen": openBinding, "toolActive": toolActiveBinding, "hasSelection": hasSelectionBinding, "selectedEdgeId": selectedEdgeIdBinding, "totalSubLanes": totalSubLanesBinding, "rawCarLanes": rawCarLanesBinding, "directionMatchedCarLanes": directionMatchedCarLanesBinding, "carriagewayMatchedCarLanes": carriagewayMatchedCarLanesBinding, "filteredOutSubLanes": filteredOutSubLanesBinding, "propagationActive": propagationActiveBinding, "rebuildsCount": rebuildsCountBinding, "vehiclesRerouted": vehiclesReroutedBinding, "activeEnforced": activeEnforcedBinding, "existingPathsSignaled": existingPathsSignaledBinding, "nativeUpdateRequested": nativeUpdateRequestedBinding, "observedCars": observedCarsBinding, "observedTrucks": observedTrucksBinding, "observedBuses": observedBusesBinding, "observedTaxis": observedTaxisBinding, "observedEmergency": observedEmergencyBinding, "observedServices": observedServicesBinding, "observedViolations": observedViolationsBinding, "laneCount": laneCountBinding, "selectedLaneIdx": selectedLaneIdxBinding, "allowedVehicles": allowedVehiclesBinding, "isLocalAccess": isLocalAccessBinding, "isClosed": isClosedBinding, "hasClipboard": hasClipboardBinding };
export const RoadRulesToolbarButton: React.FC = () => {
  const [isOpen, setIsOpen] = useState(openBinding.value);
  const [isToolActive, setIsToolActive] = useState(toolActiveBinding.value);

  useEffect(() => {
    const sub0 = openBinding.subscribe(setIsOpen);
    const sub1 = toolActiveBinding.subscribe(setIsToolActive);
    return () => {
      sub0.dispose();
      sub1.dispose();
    };
  }, []);

  const toggle = useCallback(() => trigger('RoadRules', 'togglePanel'), []);

  return (
    <Tooltip tooltip="Road Rules — Manage lane and vehicle access">
      <FloatingButton
        src={iconSrc}
        selected={isOpen || isToolActive}
        onSelect={toggle}
      />
    </Tooltip>
  );
};

export const RoadRulesPanel: React.FC = () => {
  const [isOpen, setIsOpen] = useState(openBinding.value);
  const [isToolActive, setIsToolActive] = useState(toolActiveBinding.value);
  const [activeToolName, setActiveToolName] = useState(activeToolNameBinding.value);
  const [lastRawEntity, setLastRawEntity] = useState(lastRawEntityBinding.value);
  const [lastResolvedEdge, setLastResolvedEdge] = useState(lastResolvedEdgeBinding.value);
  const [lastFailureReason, setLastFailureReason] = useState(lastFailureReasonBinding.value);
  const [lastRawComponents, setLastRawComponents] = useState(lastRawComponentsBinding.value);
  const [modVersion, setModVersion] = useState(modVersionBinding.value);

  const [status, setStatus] = useState(statusBinding.value);
  const [hasSelection, setHasSelection] = useState(hasSelectionBinding.value);
  const [selectedEdgeId, setSelectedEdgeId] = useState(selectedEdgeIdBinding.value);
  const [carriagewayName, setCarriagewayName] = useState(carriagewayNameBinding.value);
  const [totalSubLanes, setTotalSubLanes] = useState(totalSubLanesBinding.value);
  const [rawCarLanes, setRawCarLanes] = useState(rawCarLanesBinding.value);
  const [directionMatchedCarLanes, setDirectionMatchedCarLanes] = useState(directionMatchedCarLanesBinding.value);
  const [carriagewayMatchedCarLanes, setCarriagewayMatchedCarLanes] = useState(carriagewayMatchedCarLanesBinding.value);
  const [filteredOutSubLanes, setFilteredOutSubLanes] = useState(filteredOutSubLanesBinding.value);
  const [physicalLaneDebug, setPhysicalLaneDebug] = useState(physicalLaneDebugBinding.value);
  const [propagationActive, setPropagationActive] = useState(propagationActiveBinding.value);
  const [propagationMessage, setPropagationMessage] = useState(propagationMessageBinding.value);

  const [rebuildsCount, setRebuildsCount] = useState(rebuildsCountBinding.value);
  const [vehiclesRerouted, setVehiclesRerouted] = useState(vehiclesReroutedBinding.value);
  const [activeEnforced, setActiveEnforced] = useState(activeEnforcedBinding.value);
  const [existingPathsSignaled, setExistingPathsSignaled] = useState(existingPathsSignaledBinding.value);
  const [nativeUpdateRequested, setNativeUpdateRequested] = useState(nativeUpdateRequestedBinding.value);
  const [enforcementSummary, setEnforcementSummary] = useState(enforcementSummaryBinding.value);
  const [observedCars, setObservedCars] = useState(observedCarsBinding.value);
  const [observedTrucks, setObservedTrucks] = useState(observedTrucksBinding.value);
  const [observedBuses, setObservedBuses] = useState(observedBusesBinding.value);
  const [observedTaxis, setObservedTaxis] = useState(observedTaxisBinding.value);
  const [observedEmergency, setObservedEmergency] = useState(observedEmergencyBinding.value);
  const [observedServices, setObservedServices] = useState(observedServicesBinding.value);
  const [observedViolations, setObservedViolations] = useState(observedViolationsBinding.value);

  const [laneCount, setLaneCount] = useState(laneCountBinding.value);
  const [selectedLaneIdx, setSelectedLaneIdx] = useState(selectedLaneIdxBinding.value);
  const [allowedVehicles, setAllowedVehicles] = useState(allowedVehiclesBinding.value);
  const [isLocalAccess, setIsLocalAccess] = useState(isLocalAccessBinding.value);
  const [isClosed, setIsClosed] = useState(isClosedBinding.value);
  const [presetName, setPresetName] = useState(presetNameBinding.value);
  const [hasClipboard, setHasClipboard] = useState(hasClipboardBinding.value);
  const [lanes, setLanes] = useState<LaneInfo[]>(lanesBinding.value || []);

  const [showAdvanced, setShowAdvanced] = useRememberedPreference("RoadRules.showAdvanced", false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 2500);
  };

  const close = useCallback(() => trigger('RoadRules', 'closePanel'), []);

  useEffect(() => {
    const s00 = openBinding.subscribe(setIsOpen);
    const s01 = statusBinding.subscribe(setStatus);
    const s02 = hasSelectionBinding.subscribe(setHasSelection);
    const s03 = selectedEdgeIdBinding.subscribe(setSelectedEdgeId);
    const s04 = laneCountBinding.subscribe(setLaneCount);
    const s05 = selectedLaneIdxBinding.subscribe(setSelectedLaneIdx);
    const s06 = allowedVehiclesBinding.subscribe(setAllowedVehicles);
    const s07 = isLocalAccessBinding.subscribe(setIsLocalAccess);
    const s08 = isClosedBinding.subscribe(setIsClosed);
    const s09 = presetNameBinding.subscribe(setPresetName);
    const s10 = hasClipboardBinding.subscribe(setHasClipboard);
    const s11 = lanesBinding.subscribe(setLanes);
    const s12 = toolActiveBinding.subscribe(setIsToolActive);
    const s13 = activeToolNameBinding.subscribe(setActiveToolName);
    const s14 = lastRawEntityBinding.subscribe(setLastRawEntity);
    const s15 = lastResolvedEdgeBinding.subscribe(setLastResolvedEdge);
    const s16 = lastFailureReasonBinding.subscribe(setLastFailureReason);
    const s17 = lastRawComponentsBinding.subscribe(setLastRawComponents);
    const s18 = modVersionBinding.subscribe(setModVersion);
    const s19 = carriagewayNameBinding.subscribe(setCarriagewayName);
    const s20 = totalSubLanesBinding.subscribe(setTotalSubLanes);
    const s21 = filteredOutSubLanesBinding.subscribe(setFilteredOutSubLanes);
    const s22 = rawCarLanesBinding.subscribe(setRawCarLanes);
    const s23 = directionMatchedCarLanesBinding.subscribe(setDirectionMatchedCarLanes);
    const s24 = physicalLaneDebugBinding.subscribe(setPhysicalLaneDebug);
    const s25 = rebuildsCountBinding.subscribe(setRebuildsCount);
    const s26 = vehiclesReroutedBinding.subscribe(setVehiclesRerouted);
    const s27 = activeEnforcedBinding.subscribe(setActiveEnforced);
    const s28 = carriagewayMatchedCarLanesBinding.subscribe(setCarriagewayMatchedCarLanes);
    const s29 = propagationActiveBinding.subscribe(setPropagationActive);
    const s30 = propagationMessageBinding.subscribe(setPropagationMessage);
    const s31 = existingPathsSignaledBinding.subscribe(setExistingPathsSignaled);
    const s32 = nativeUpdateRequestedBinding.subscribe(setNativeUpdateRequested);
    const s33 = enforcementSummaryBinding.subscribe(setEnforcementSummary);
    const s34 = observedCarsBinding.subscribe(setObservedCars);
    const s35 = observedTrucksBinding.subscribe(setObservedTrucks);
    const s36 = observedBusesBinding.subscribe(setObservedBuses);
    const s37 = observedTaxisBinding.subscribe(setObservedTaxis);
    const s38 = observedEmergencyBinding.subscribe(setObservedEmergency);
    const s39 = observedServicesBinding.subscribe(setObservedServices);
    const s40 = observedViolationsBinding.subscribe(setObservedViolations);

    return () => {
      s00.dispose(); s01.dispose(); s02.dispose(); s03.dispose(); s04.dispose();
      s05.dispose(); s06.dispose(); s07.dispose(); s08.dispose(); s09.dispose();
      s10.dispose(); s11.dispose(); s12.dispose(); s13.dispose(); s14.dispose();
      s15.dispose(); s16.dispose(); s17.dispose(); s18.dispose(); s19.dispose();
      s20.dispose(); s21.dispose(); s22.dispose(); s23.dispose(); s24.dispose();
      s25.dispose(); s26.dispose(); s27.dispose();
      s28.dispose(); s29.dispose(); s30.dispose();
      s31.dispose(); s32.dispose();
      s33.dispose(); s34.dispose(); s35.dispose(); s36.dispose();
      s37.dispose(); s38.dispose(); s39.dispose(); s40.dispose();
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

  const selectRoadTool = () => {
    trigger('RoadRules', 'activateTool');
    showToast('Road selector active — click any carriageway on the map');
  };

  const selectLane = (idx: number) => {
    trigger('RoadRules', 'selectLane', idx);
  };

  const applyPreset = (presetId: string, label: string, all: boolean) => {
    trigger('RoadRules', all ? 'applyPresetAll' : 'applyPreset', presetId);
    showToast(all ? `Applied to all physical lanes: ${label}` : `Applied to selected lane: ${label}`);
  };

  const setLocalAccessMode = (val: boolean) => {
    trigger('RoadRules', 'setLocalAccess', val);
    showToast(val ? 'Experimental local route bias enabled' : 'Local route bias removed');
  };

  const setClosedMode = (val: boolean) => {
    trigger('RoadRules', 'setClosed', val);
    showToast(val ? 'Lane closed to traffic' : 'Lane reopened');
  };

  const copyRules = () => {
    trigger('RoadRules', 'copyRules');
    showToast('Copied road rules');
  };

  const pasteRules = () => {
    trigger('RoadRules', 'pasteRules');
    showToast('Pasted rules to road');
  };

  const resetLane = () => {
    trigger('RoadRules', 'resetLane');
    showToast('Reset lane to default rules');
  };

  const currentLane = lanes.find((l) => l.laneIndex === selectedLaneIdx);

  if (!isOpen) return null;

  return (
    <div className="suite-panel road-rules-panel" data-portfolio-panel role="dialog" aria-label="Road Rules Panel">
      {/* PANEL HEADER */}
      <div className="suite-header">
        <div className="header-left">
          <img src={iconSrc} alt="Road Rules" className="header-icon" />
          <div>
            <h2 className="header-title">Road Rules</h2>
            <span className="header-subtitle">
              {hasSelection ? `${carriagewayName} (${laneCount} ${laneCount === 1 ? 'Lane' : 'Lanes'})` : 'Lane & Vehicle Access'}
            </span>
          </div>
        </div>
        <div className="header-right">
          {hasSelection && (
            <span className="suite-badge status-good">
              {presetName || 'Custom'}
            </span>
          )}
          <button className="suite-close-btn" onClick={close} title="Close Panel" aria-label="Close panel">×</button>
        </div>
      </div>

      {toastMessage && (
        <div className="suite-toast">
          <span>{toastMessage}</span>
        </div>
      )}

      {/* PANEL BODY */}
      <PortfolioHelp runtimeGroup={"Portfolio.RoadRules"} name={"Road Rules"} version={"1.4.5-beta.1"} steps={["Select a road and inspect its physical lanes.", "Choose a lane rule or preset, then review the overlay.", "Observe actual vehicles before applying the rule more widely."]} note={"Some rules are soft preferences. A painted overlay is not proof of enforcement."} bindings={portfolioBindings} />
      <div className="suite-body">
        {!hasSelection ? (
          /* EMPTY STATE */
          <div className="suite-empty-state">
            <h3 className="empty-title">Select a Road</h3>
            <p className="empty-desc">
              Select a road to close physical lanes, discourage heavy traffic, or apply CS2's shared public/service-lane gate. Press Esc or right-click to cancel selection.
            </p>
            <button
              className={`suite-primary-btn large ${isToolActive ? 'active-pulse' : ''}`}
              onClick={selectRoadTool}
              title="Activate road selection tool"
            >
              {isToolActive ? 'Click Road on Map...' : 'Select Road'}
            </button>
            <div className="empty-hint">
              {isToolActive ? 'Road selector active — click any carriageway' : 'Point and click any road segment to inspect'}
            </div>
          </div>
        ) : (
          /* FOCUSED ROAD INSPECTOR */
          <div className="road-inspector">
            {/* CARRIAGEWAY BANNER */}
            <div className="carriageway-banner">
              <div className="banner-left">
                <span className="banner-tag">ACTIVE CARRIAGEWAY</span>
                <span className="banner-name">{carriagewayName}</span>
              </div>
              <div className="banner-right">
                <span className="lane-count-tag">{laneCount} Editable {laneCount === 1 ? 'Lane' : 'Lanes'}</span>
              </div>
            </div>

            {propagationActive && (
              <div className="propagation-notice">{propagationMessage}</div>
            )}

            {/* 1-CLICK QUICK RULES */}
            <div className="section-header">
              <span className="section-title">Quick Rules</span>
              <span className="section-subtitle">One-Click Presets</span>
            </div>

            <div className="quick-presets-row">
              {QUICK_PRESETS.map((p) => (
                <button
                  key={p.id}
                  className={`quick-preset-btn ${!p.all && presetName === p.id ? 'active' : ''}`}
                  onClick={() => applyPreset(p.id, p.label, p.all)}
                  title={p.desc}
                >
                  {p.label}
                </button>
              ))}
            </div>

            {/* LANE SELECTOR TABS */}
            <div className="section-header">
              <span className="section-title">Physical Lanes (Cross-Section)</span>
              <span className="section-subtitle">Left to Right in Travel Direction</span>
            </div>

            <div className="lane-tabs-row">
              {Array.from({ length: laneCount }, (_, i) => {
                const lane = lanes.find((l) => l.laneIndex === i);
                const isSelected = selectedLaneIdx === i;
                const isLaneClosed = lane?.isClosed || false;
                const isLocal = lane?.isLocalAccess || false;
                const posLabel = lane?.lanePositionLabel || `Lane ${i + 1}`;

                return (
                  <button
                    key={i}
                    className={`lane-tab-btn ${isSelected ? 'selected' : ''} ${isLaneClosed ? 'closed' : ''}`}
                    onClick={() => selectLane(i)}
                    title={`Lane #${i + 1} (${posLabel})${isLaneClosed ? ' [CLOSED]' : isLocal ? ' [LOCAL ACCESS]' : ''}`}
                  >
                    <span className="lane-tab-name">Lane {i + 1}</span>
                    <span className="lane-tab-pos">{posLabel}</span>
                    <span className={`lane-tab-tag ${isLaneClosed ? 'closed' : isLocal ? 'local' : 'open'}`}>
                      {isLaneClosed ? 'Closed' : isLocal ? 'Local' : 'Open'}
                    </span>
                  </button>
                );
              })}
            </div>

            {/* FOCUSED LANE CONTROLS */}
            <div className="lane-controls-card">
              <div className="lane-controls-header">
                <div>
                  <span className="lane-active-label">
                    Lane {selectedLaneIdx + 1} {currentLane?.lanePositionLabel ? `(${currentLane.lanePositionLabel})` : ''} Vehicle Access
                  </span>
                </div>
                {isClosed ? (
                  <span className="lane-state-tag closed">Closed to Traffic</span>
                ) : isLocalAccess ? (
                  <span className="lane-state-tag local">Local Bias</span>
                ) : (
                  <span className="lane-state-tag open">Active</span>
                )}
              </div>

              <div className="native-semantics-note">
                Class status below is read-only. CS2 exposes a shared public-lane gate—not six independent hard masks.
              </div>

              {/* VERIFIED VEHICLE ELIGIBILITY */}
              <div className="vehicle-chips-grid">
                {VEHICLE_FLAGS.map((v) => {
                  const isAllowed = (allowedVehicles & v.flag) !== 0 && !isClosed;
                  return (
                    <div
                      key={v.flag}
                      className={`vehicle-chip ${isAllowed ? 'allowed' : 'restricted'}`}
                      title={v.tooltip}
                    >
                      <span className="chip-icon">{v.icon}</span>
                      <span className="chip-name">{v.label}</span>
                      <span className="chip-status">{isClosed ? 'Closed' : isAllowed ? 'Allowed' : 'Blocked'}</span>
                    </div>
                  );
                })}
              </div>

              {/* SPECIAL RESTRICTIONS */}
              <div className="special-restrictions-row">
                <button
                  className={`special-toggle ${isLocalAccess ? 'active' : ''}`}
                  onClick={() => setLocalAccessMode(!isLocalAccess)}
                  disabled={isClosed}
                  title="Apply a soft path-cost bias against through traffic; this is not a hard destination-only gate"
                >
                  <span className="special-icon">LOCAL</span>
                  <div className="special-info">
                    <span className="special-title">Local Bias (Experimental)</span>
                    <span className="special-desc">Discourage through-routing</span>
                  </div>
                  <span className="special-check">{isLocalAccess ? 'ON' : 'OFF'}</span>
                </button>

                <button
                  className={`special-toggle closed ${isClosed ? 'active' : ''}`}
                  onClick={() => setClosedMode(!isClosed)}
                  title="Completely close lane to all vehicles in CS2 simulation"
                >
                  <span className="special-icon">CLOSE</span>
                  <div className="special-info">
                    <span className="special-title">Close Lane</span>
                    <span className="special-desc">Block all traffic</span>
                  </div>
                  <span className="special-check">{isClosed ? 'CLOSED' : 'OPEN'}</span>
                </button>
              </div>
            </div>

            {/* ACTION TOOLBAR */}
            <div className="action-toolbar">
              <button
                className="suite-secondary-btn compact"
                onClick={selectRoadTool}
                title="Select another road segment"
              >
                Pick Another Road
              </button>
              <div className="action-buttons-group">
                <button
                  className="suite-secondary-btn compact"
                  onClick={copyRules}
                  title="Copy current carriageway rules"
                >
                  Copy Rules
                </button>
                <button
                  className="suite-secondary-btn compact"
                  onClick={pasteRules}
                  disabled={!hasClipboard}
                  title={hasClipboard ? 'Paste copied rules onto this carriageway' : 'Copy a road first'}
                >
                  Paste Rules
                </button>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* FOOTER */}
      <div className="suite-footer">
        <span className="footer-hint">{status}</span>
        <button
          className="advanced-toggle-btn"
          onClick={() => setShowAdvanced(!showAdvanced)}
          title="Toggle debug diagnostics"
        >
          {showAdvanced ? 'Hide Debug ▴' : 'Advanced Debug ▸'}
        </button>
      </div>

      {/* ADVANCED DRAWER */}
      {showAdvanced && (
        <div className="advanced-drawer">
          <div className="drawer-row">
            <span>Selected Edge ID: <code>#{selectedEdgeId}</code></span>
            <span>Bitmask: <code>0x{allowedVehicles.toString(16).toUpperCase()}</code></span>
            <button className="suite-secondary-btn compact" onClick={resetLane}>
              Reset Lane
            </button>
          </div>
            <div className="drawer-telemetry">
              <div><strong>Enforcement:</strong> {enforcementSummary}</div>
              <div><strong>Native Simulation Telemetry:</strong> Graph Rebuilds: <strong>{rebuildsCount}</strong> • Vehicles Rerouted: <strong>{vehiclesRerouted}</strong> • Enforced Entities: <strong>{activeEnforced}</strong></div>
            <div>Last rule apply: Native update requested: <strong>{nativeUpdateRequested ? 'YES' : 'NO'}</strong> • Existing-path vehicles signaled: <strong>{existingPathsSignaled}</strong> • New-route selection is not inferred.</div>
            <div>Carriageway: <strong>{carriagewayName}</strong> • Physical: <strong>{laneCount}</strong> • Internal candidates: <strong>{carriagewayMatchedCarLanes}</strong></div>
            <div>SubLanes: <strong>{totalSubLanes} Total</strong> (Raw CarLanes: {rawCarLanes}, Direction-Matched: {directionMatchedCarLanes}, Carriageway-Matched: {carriagewayMatchedCarLanes}, Filtered: {filteredOutSubLanes})</div>
            <div>Active Tool: <strong style={{ color: isToolActive ? '#4ade80' : '#f87171' }}>{activeToolName}</strong> (Tool Active: {isToolActive ? 'YES' : 'NO'})</div>
            <div>Last Hit: <code>{lastRawEntity}</code> → Resolved: <code>{lastResolvedEdge}</code></div>
              <div>Resolution: <span>{lastFailureReason}</span></div>
              <div><strong>Observed new lane entries:</strong> Cars {observedCars} • Trucks {observedTrucks} • Buses {observedBuses} • Taxis {observedTaxis} • Emergency {observedEmergency} • Services {observedServices} • <strong>Violations {observedViolations}</strong></div>
            {physicalLaneDebug !== 'None' && (
              <pre style={{ margin: '6px 0', fontSize: '10px', background: 'rgba(0,0,0,0.3)', padding: '6px', borderRadius: '4px', whiteSpace: 'pre-wrap' }}>
                {physicalLaneDebug}
              </pre>
            )}
            {lastRawComponents !== 'None' && (
              <div className="raw-components">Components: {lastRawComponents}</div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append('GameTopLeft', RoadRulesToolbarButton);
  moduleRegistry.append('GameTopRight', RoadRulesPanel);
};

export default register;
