import { PortfolioHelp } from "./portfolio-ux";
import React, { useCallback, useEffect, useState } from 'react';
import type { ModRegistrar } from 'cs2/modding';
import { bindValue, trigger } from 'cs2/api';
import { Tooltip } from 'cs2/ui';
import iconSrc from './assets/discord-rpc-icon.svg';
import './style.scss';

const open = bindValue<boolean>('discordRPC', 'open', false);
const showButton = bindValue<boolean>('discordRPC', 'showButton', true);
const enabled = bindValue<boolean>('discordRPC', 'enabled', true);
const status = bindValue<string>('discordRPC', 'status', 'Waiting');
const message = bindValue<string>('discordRPC', 'message', 'Waiting for Discord');
const details = bindValue<string>('discordRPC', 'details', 'In the Main Menu');
const state = bindValue<string>('discordRPC', 'state', 'Ready to build');
const lastUpdate = bindValue<string>('discordRPC', 'lastUpdate', 'Not sent yet');
const source = bindValue<string>('discordRPC', 'source', 'Cities: Skylines II');
const mode = bindValue<string>('discordRPC', 'mode', 'Main Menu');
const privacy = bindValue<string>('discordRPC', 'privacy', 'City hidden');
const interval = bindValue<number>('discordRPC', 'interval', 15);
const successes = bindValue<number>('discordRPC', 'successes', 0);
const failures = bindValue<number>('discordRPC', 'failures', 0);

function useBinding<T>(b: any): T {
  const [value, setValue] = useState<T>(b.value);
  useEffect(() => { const s = b.subscribe(setValue); setValue(s.value); return () => s.dispose(); }, [b]);
  return value;
}

const portfolioBindings = { "open": open, "showButton": showButton, "enabled": enabled, "interval": interval, "successes": successes, "failures": failures };
const ToolbarButton: React.FC = () => {
  const visible = useBinding<boolean>(showButton);
  const selected = useBinding<boolean>(open);
  if (!visible) return null;
  return <Tooltip tooltip="Discord RPC — Presence status and privacy"><button type="button" className={`drpc-toolbar ${selected ? 'active' : ''}`} aria-label="Discord RPC" aria-pressed={selected} onClick={() => trigger('discordRPC', 'toggle')}><img src={iconSrc} alt="" /></button></Tooltip>;
};

const Panel: React.FC = () => {
  const isOpen = useBinding<boolean>(open);
  const isEnabled = useBinding<boolean>(enabled);
  const connection = useBinding<string>(status);
  const connectionMessage = useBinding<string>(message);
  const detailsText = useBinding<string>(details);
  const stateText = useBinding<string>(state);
  const updated = useBinding<string>(lastUpdate);
  const activitySource = useBinding<string>(source);
  const gameMode = useBinding<string>(mode);
  const privacyText = useBinding<string>(privacy);
  const seconds = useBinding<number>(interval);
  const okCount = useBinding<number>(successes);
  const failCount = useBinding<number>(failures);
  const [tab, setTab] = useState<'status'|'privacy'|'templates'|'integrations'>('status');
  const close = useCallback(() => trigger('discordRPC', 'close'), []);

  useEffect(() => {
    if (!isOpen) return;
    const key = (e: KeyboardEvent) => { if (e.key === 'Escape') close(); };
    window.addEventListener('keydown', key); return () => window.removeEventListener('keydown', key);
  }, [isOpen, close]);
  if (!isOpen) return null;

  const connected = connection === 'Connected';
  const privacyPreset = privacyText === 'City hidden / Population hidden / Treasury hidden' ? 'private'
    : privacyText === 'City shared / Population rounded / Treasury hidden' ? 'social'
    : privacyText === 'City shared / Population exact / Treasury rounded' ? 'detailed' : '';
  return <div className="drpc-panel" data-portfolio-panel role="dialog" aria-label="Discord RPC">
    <header className="drpc-header"><img src={iconSrc} alt=""/><div><h2>Discord RPC</h2><span>Presence controls</span></div><button type="button" className="drpc-close" onClick={close} aria-label="Close Discord RPC">Close</button></header>
    <div className={`status-strip ${connected ? 'ok' : isEnabled ? 'warn' : 'off'}`} role="status"><span className="status-dot" aria-hidden="true"/><div><b>{isEnabled ? connection : 'Enhanced presence off'}</b><span>{connectionMessage}</span></div></div>
    <nav aria-label="Discord RPC sections">
      {(['status','privacy','templates','integrations'] as const).map(t => <button type="button" key={t} className={tab===t?'active':''} aria-current={tab===t?'page':undefined} onClick={()=>setTab(t)}>{t}</button>)}
    </nav>
    <main>
      {tab === 'status' && <>
        <div className="drpc-section-heading"><h3>Activity preview</h3><span>Local preview</span></div>
        <section className="preview"><div className="preview-icon"><img src={iconSrc} alt=""/></div><div><small>CITIES: SKYLINES II</small><strong title={detailsText}>{detailsText}</strong><span title={stateText}>{stateText}</span><span className="preview-meta">{gameMode} / {activitySource}</span></div></section>
        <p className="drpc-caption">Confirm the activity in Discord. This preview can update before Discord accepts it.</p>
        <div className="grid"><article><label>Last update</label><b>{updated}</b></article><article><label>Update interval</label><b>{seconds}s</b></article><article><label>Accepted</label><b>{okCount}</b></article><article><label>Failed</label><b>{failCount}</b></article></div>
        <div className="actions"><button type="button" className="primary" disabled={!isEnabled} onClick={()=>trigger('discordRPC','refresh')}>Refresh presence</button><button type="button" onClick={()=>trigger('discordRPC','setEnabled',!isEnabled)}>{isEnabled?'Use vanilla presence':'Enable enhanced presence'}</button></div>
      </>}
      {tab === 'privacy' && <>
        <div className="drpc-section-heading"><h3>Privacy</h3><span>Saved with your mod settings</span></div>
        <section className="info"><b>Current sharing</b><p>{privacyText}</p><p>Save paths, account names and mod lists are never sent.</p></section>
        <div className="preset-row" aria-label="Privacy preset"><button type="button" className={privacyPreset==='private'?'active':''} aria-pressed={privacyPreset==='private'} onClick={()=>trigger('discordRPC','privacyPreset','private')}>Private</button><button type="button" className={privacyPreset==='social'?'active':''} aria-pressed={privacyPreset==='social'} onClick={()=>trigger('discordRPC','privacyPreset','social')}>Social</button><button type="button" className={privacyPreset==='detailed'?'active':''} aria-pressed={privacyPreset==='detailed'} onClick={()=>trigger('discordRPC','privacyPreset','detailed')}>Detailed</button></div>
        <p className="hint">Fine-grained controls are available in Settings → Discord RPC.</p>
      </>}
      {tab === 'templates' && <>
        <div className="drpc-section-heading"><h3>Presence layout</h3><span>Details and state lines</span></div>
        <section className="info"><p>Choose a starting layout. Edit the exact text in Settings → Discord RPC.</p></section>
        <div className="preset-row"><button type="button" onClick={()=>trigger('discordRPC','templatePreset','minimal')}>Minimal</button><button type="button" onClick={()=>trigger('discordRPC','templatePreset','city')}>City stats</button><button type="button" onClick={()=>trigger('discordRPC','templatePreset','weather')}>Weather</button></div>
        <div className="token-list">Tokens: activity / city / mode / population / money / date / season / weather / temperature / traffic</div>
      </>}
      {tab === 'integrations' && <>
        <div className="drpc-section-heading"><h3>Companion activity</h3><span>Optional</span></div>
        <section className="info"><p>Enable companion activity in Settings → Discord RPC. Active suite tools temporarily take priority, then expire back to your city presence.</p></section>
        <ul><li><b>Access Studio</b><span>Editing building access</span></li><li><b>Road Rules</b><span>Editing road rules</span></li><li><b>Event Engine</b><span>Planning an event</span></li><li><b>Traffic Stress Lab</b><span>Stress multiplier</span></li></ul>
        <p className="hint">Other mods can use the public DiscordRPCIntegration API without linking the Discord SDK.</p>
      </>}
    </main>
    <PortfolioHelp runtimeGroup={"Portfolio.DiscordRPC"} name={"Discord RPC"} version={"0.9.3-beta.3"} steps={["Check the connection status with Discord running.", "Choose a privacy preset before enabling enhanced presence.", "Review the displayed activity and use Refresh presence."]} note={"The preview is not proof that Discord accepted the activity. Check the actual Discord client."} bindings={portfolioBindings} />
    <footer><span>Uses CSII's Discord connection</span><span>0.9.3 beta.3</span></footer>
  </div>;
};

const register: ModRegistrar = registry => { registry.append('GameTopLeft', ToolbarButton); registry.append('GameTopRight', Panel); };
export default register;
