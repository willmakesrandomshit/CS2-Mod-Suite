import React, { useCallback, useEffect, useState } from 'react';
import type { ModRegistrar } from 'cs2/modding';
import { bindValue, trigger } from 'cs2/api';
import { FloatingButton, Tooltip } from 'cs2/ui';
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

const ToolbarButton: React.FC = () => {
  const visible = useBinding<boolean>(showButton);
  const selected = useBinding<boolean>(open);
  if (!visible) return null;
  return <Tooltip tooltip="Discord RPC — Presence status and privacy"><FloatingButton src={iconSrc} selected={selected} onSelect={() => trigger('discordRPC', 'toggle')} /></Tooltip>;
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
  return <div className="drpc-panel" role="dialog" aria-label="Discord RPC">
    <header><img src={iconSrc}/><div><h2>Discord RPC</h2><span>Rich presence, under your control</span></div><button onClick={close}>×</button></header>
    <div className={`status-strip ${connected ? 'ok' : isEnabled ? 'warn' : 'off'}`}><b>{connection}</b><span>{connectionMessage}</span></div>
    <nav>
      {(['status','privacy','templates','integrations'] as const).map(t => <button className={tab===t?'active':''} onClick={()=>setTab(t)}>{t}</button>)}
    </nav>
    <main>
      {tab === 'status' && <>
        <section className="preview"><div className="preview-icon"><img src={iconSrc}/></div><div><small>PLAYING CITIES: SKYLINES II</small><strong>{detailsText}</strong><span>{stateText}</span><span>{gameMode} · Source: {activitySource}</span></div></section>
        <div className="grid"><article><label>Connection</label><b>{connection}</b></article><article><label>Last update</label><b>{updated}</b></article><article><label>Interval</label><b>{seconds}s</b></article><article><label>Accepted / failed</label><b>{okCount} / {failCount}</b></article></div>
        <div className="actions"><button className="primary" onClick={()=>trigger('discordRPC','refresh')}>Refresh now</button><button onClick={()=>trigger('discordRPC','setEnabled',!isEnabled)}>{isEnabled?'Disable & restore vanilla':'Enable enhanced presence'}</button></div>
      </>}
      {tab === 'privacy' && <>
        <section className="info"><h3>Privacy is explicit</h3><p>{privacyText}</p><p>Save paths, account names and mod lists are never sent. City names are hidden by default.</p></section>
        <div className="preset-row"><button onClick={()=>trigger('discordRPC','privacyPreset','private')}>Private</button><button onClick={()=>trigger('discordRPC','privacyPreset','social')}>Social</button><button onClick={()=>trigger('discordRPC','privacyPreset','detailed')}>Detailed</button></div>
        <p className="hint">Fine-grained controls are available in Settings → Discord RPC.</p>
      </>}
      {tab === 'templates' && <>
        <section className="info"><h3>Presence layouts</h3><p>Pick a layout here, then edit the exact token templates in the game settings.</p></section>
        <div className="preset-row"><button onClick={()=>trigger('discordRPC','templatePreset','minimal')}>Minimal</button><button onClick={()=>trigger('discordRPC','templatePreset','city')}>City stats</button><button onClick={()=>trigger('discordRPC','templatePreset','weather')}>Weather</button></div>
        <div className="token-list">Tokens: activity · city · mode · population · money · date · season · weather · temperature · traffic</div>
        <div className="disabled-option"><span>Discord buttons</span><b>Unavailable</b><p>The Discord Game SDK shipped with CS2 does not expose activity buttons. This control is intentionally disabled.</p></div>
      </>}
      {tab === 'integrations' && <>
        <section className="info"><h3>Companion activity</h3><p>Active suite tools temporarily take priority, then expire back to your city presence.</p></section>
        <ul><li><b>Access Studio</b><span>Editing building access</span></li><li><b>Road Rules</b><span>Editing road rules</span></li><li><b>Event Engine</b><span>Planning an event</span></li><li><b>Traffic Stress Lab</b><span>Stress multiplier</span></li></ul>
        <p className="hint">Other mods can use the public DiscordRPCIntegration API without linking the Discord SDK.</p>
      </>}
    </main>
    <footer><span>Shared official CS2 Discord client · no duplicate connection</span><span>v0.9 beta</span></footer>
  </div>;
};

const register: ModRegistrar = registry => { registry.append('GameTopLeft', ToolbarButton); registry.append('GameTopRight', Panel); };
export default register;
