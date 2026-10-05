import { PortfolioHelp, useRememberedPreference } from "./portfolio-ux";
import React, { useEffect, useState } from 'react';
import { ModRegistrar } from 'cs2/modding';
import { bindValue, trigger } from 'cs2/api';
import { FloatingButton, Tooltip } from 'cs2/ui';
import iconSrc from './assets/access-studio.svg';
import './style.scss';

const open$ = bindValue<boolean>('AccessStudio', 'open', false);
const active$ = bindValue<boolean>('AccessStudio', 'toolActive', false);
const selected$ = bindValue<boolean>('AccessStudio', 'hasSelection', false);
const status$ = bindValue<string>('AccessStudio', 'status', 'Activate the probe and click a building.');
const diagnostic$ = bindValue<string>('AccessStudio', 'diagnostic', 'No building selected.');
const roadSelection$ = bindValue<boolean>('AccessStudio', 'roadSelection', false);
const remoteSelection$ = bindValue<boolean>('AccessStudio', 'remoteSelection', false);
const hasOverride$ = bindValue<boolean>('AccessStudio', 'hasOverride', false);
const pocStatus$ = bindValue<string>('AccessStudio', 'pocStatus', 'Select a building, then choose Move Service Access.');
const pocTelemetry$ = bindValue<string>('AccessStudio', 'pocTelemetry', 'No service-access POC is active.');

function useBinding<T>(binding: any): T {
  const [value, setValue] = useState<T>(binding.value);
  useEffect(() => {
    const subscription = binding.subscribe(setValue);
    return () => subscription.dispose();
  }, [binding]);
  return value;
}

const portfolioBindings = { "open": open$, "toolActive": active$, "hasSelection": selected$, "roadSelection": roadSelection$, "remoteSelection": remoteSelection$, "hasOverride": hasOverride$ };
const ToolbarButton: React.FC = () => {
  const open = useBinding<boolean>(open$);
  const active = useBinding<boolean>(active$);
  return (
    <Tooltip tooltip="Access Studio — Edit supported building service access">
      <FloatingButton src={iconSrc} selected={open || active} onSelect={() => trigger('AccessStudio', 'toggle')} />
    </Tooltip>
  );
};

const ProbePanel: React.FC = () => {
  const open = useBinding<boolean>(open$);
  const active = useBinding<boolean>(active$);
  const selected = useBinding<boolean>(selected$);
  const status = useBinding<string>(status$);
  const diagnostic = useBinding<string>(diagnostic$);
  const roadSelection = useBinding<boolean>(roadSelection$);
  const remoteSelection = useBinding<boolean>(remoteSelection$);
  const placing = roadSelection || remoteSelection;
  const hasOverride = useBinding<boolean>(hasOverride$);
  const pocStatus = useBinding<string>(pocStatus$);
  const pocTelemetry = useBinding<string>(pocTelemetry$);
  const [showAdvanced, setShowAdvanced] = useRememberedPreference("AccessStudio.showAdvanced", false);

  useEffect(() => {
    if (!open) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !placing) trigger('AccessStudio', 'close');
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open, placing]);

  if (!open) return null;
  return (
    <section className="as-panel" data-portfolio-panel>
      <header>
        <div><strong>Access Studio</strong><small>PUBLIC EARLY ALPHA</small></div>
        <button aria-label="Close" onClick={() => trigger('AccessStudio', 'close')}>×</button>
      </header>
      <div className={`as-state ${active ? 'active' : ''}`}>{active ? 'SELECT A BUILDING' : 'PROBE PAUSED'}</div>
      <PortfolioHelp runtimeGroup={"Portfolio.AccessStudio"} name={"Access Studio"} version={"0.3.3-alpha.1"} steps={["Activate the probe and select a supported building.", "Choose Move Service Access and pick a valid road.", "Use Reset Vanilla to restore the original access."]} note={"Remote access is experimental. Verify service routing in a disposable city."} bindings={portfolioBindings} />
      <p className="as-status">{status}</p>
      <div className="as-actions">
        <button onClick={() => trigger('AccessStudio', 'activate')}>{active ? 'Selection Tool Active' : 'Select Building'}</button>
        <button disabled={!selected} onClick={() => trigger('AccessStudio', 'refresh')}>Refresh</button>
        <button disabled={!selected} onClick={() => trigger('AccessStudio', 'clear')}>Clear</button>
      </div>
      <div className={`as-poc-state ${placing ? 'armed' : hasOverride ? 'custom' : ''}`}>{remoteSelection ? 'EXPERIMENTAL · SELECT REMOTE ROAD' : roadSelection ? 'SELECT ACCESS ROAD' : hasOverride ? 'CUSTOM ACCESS ACTIVE' : selected ? 'READY' : 'NO BUILDING SELECTED'}</div>
      <p className="as-status">{remoteSelection ? 'Click a valid vehicle road within 50 m. The building keeps its normal frontage. Right-click or Esc cancels.' : roadSelection ? 'Choose a different nearby vehicle road. Right-click or Esc cancels.' : pocStatus}</p>
      <div className="as-actions as-poc-actions">
        <button disabled={!selected || placing || hasOverride} onClick={() => trigger('AccessStudio', 'moveService')}>Move Service Access</button>
        <button className="experimental" disabled={!selected || placing || hasOverride} onClick={() => trigger('AccessStudio', 'placeRemote')}>Place Remote Point <small>EXPERIMENTAL</small></button>
        {placing
          ? <button onClick={() => trigger('AccessStudio', 'cancelMove')}>Cancel</button>
          : <button disabled={!hasOverride} onClick={() => trigger('AccessStudio', 'resetVanilla')}>Reset Vanilla</button>}
      </div>
      <button className="as-advanced-toggle" onClick={() => setShowAdvanced(!showAdvanced)}>
        {showAdvanced ? 'Hide Advanced Diagnostics' : 'Advanced Diagnostics'}
      </button>
      {showAdvanced && (
        <div className="as-advanced">
          <pre className="as-telemetry">{pocTelemetry}</pre>
          <pre>{diagnostic}</pre>
        </div>
      )}
      <footer>Remote Road C is experimental and not stable. Use a disposable city and Reset Vanilla before saving important cities.</footer>
    </section>
  );
};

const register: ModRegistrar = (moduleRegistry: any) => {
  moduleRegistry.append('GameTopLeft', ToolbarButton);
  moduleRegistry.append('GameTopRight', ProbePanel);
};

export default register;
