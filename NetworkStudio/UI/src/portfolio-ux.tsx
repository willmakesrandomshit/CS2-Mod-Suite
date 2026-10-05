import { useEffect, useMemo, useRef, useState } from "react";
import { bindValue, trigger } from "cs2/api";
import { formatSnapshot, readPreference, writePreference } from "./portfolio-logic";
import "./portfolio-ux.scss";

function storage() {
  try { return window.localStorage; } catch { return undefined; }
}

export function useRememberedPreference(key: string, fallback: boolean) {
  const qualified = `cs2.portfolio.ui.v1.${key}`;
  const [value, setValue] = useState(() => readPreference(storage(), qualified, fallback));
  useEffect(() => writePreference(storage(), qualified, value), [qualified, value]);
  return [value, setValue] as const;
}

interface Binding {
  value: unknown;
  subscribe(callback: (value: unknown) => void): { value?: unknown; dispose(): void };
}

export interface HelpText {
  help: string; hide: string; dismiss: string; support: string; copy: string;
  copied: string; manual: string; report: string;
}

const english: HelpText = {
  help: "Quick guide", hide: "Hide guide", dismiss: "Got it", support: "Support snapshot",
  copy: "Copy snapshot", copied: "Copied", manual: "Select the text and press Ctrl+C to copy.",
  report: "Status snapshot",
};

function useTextBinding(binding: ReturnType<typeof bindValue<string>>) {
  const [value, setValue] = useState(binding.value);
  useEffect(() => {
    const sub = binding.subscribe(setValue);
    setValue(sub.value);
    return () => sub.dispose();
  }, [binding]);
  return value;
}

export function PortfolioHelp({ name, version, runtimeGroup, steps, note, bindings, text = english }: {
  name: string; version: string; steps: string[]; note: string;
  runtimeGroup: string; bindings: Record<string, Binding>; text?: HelpText;
}) {
  const [guide, setGuide] = useRememberedPreference(`${name}.guide`, true);
  const [support, setSupport] = useState(false);
  const [values, setValues] = useState<Record<string, unknown>>({});
  const reportRef = useRef<HTMLTextAreaElement>(null);
  const backend = useMemo(() => ({
    version: bindValue<string>(runtimeGroup, "runtimeVersion", "Waiting for game system"),
    game: bindValue<string>(runtimeGroup, "gameVersion", "Unknown"),
    compatibility: bindValue<string>(runtimeGroup, "compatibility", ""),
    copy: bindValue<string>(runtimeGroup, "copyResult", ""),
  }), [runtimeGroup]);
  const runtimeVersion = useTextBinding(backend.version);
  const gameVersion = useTextBinding(backend.game);
  const compatibility = useTextBinding(backend.compatibility);
  const feedback = useTextBinding(backend.copy);
  useEffect(() => { trigger(runtimeGroup, "checkCompatibility"); }, [runtimeGroup]);
  const report = `${formatSnapshot(name, version, values)}\nRuntime assembly: ${runtimeVersion}\nGame: ${gameVersion}${compatibility ? `\nCompatibility: ${compatibility}` : ""}`;

  // Subscribe only while a report is visible. Every subscription is released
  // on close/unmount; closed panels do no reporting work.
  useEffect(() => {
    if (!support) return;
    const next: Record<string, unknown> = {};
    const subs = Object.keys(bindings).map(key => {
      next[key] = bindings[key].value;
      const sub = bindings[key].subscribe(value => setValues(previous => ({ ...previous, [key]: value })));
      if (sub.value !== undefined) next[key] = sub.value;
      return sub;
    });
    setValues(next);
    return () => subs.forEach(sub => sub.dispose());
  }, [support, bindings]);

  const copy = () => {
    const input = reportRef.current;
    if (!input) return;
    input.focus();
    input.select();
    trigger(runtimeGroup, "copySnapshot", report);
  };

  return <section className="portfolio-help" aria-label={`${name} help`}>
    <div className="portfolio-help-actions">
      <button type="button" aria-expanded={guide} onClick={() => setGuide(!guide)}>{guide ? text.hide : text.help}</button>
      <button type="button" aria-expanded={support} onClick={() => { setSupport(!support); trigger(runtimeGroup, "checkCompatibility"); }}>{text.support}</button>
    </div>
    {compatibility && <p role="status">{compatibility}</p>}
    {guide && <div className="portfolio-guide">
      <ol>{steps.map(step => <li key={step}>{step}</li>)}</ol>
      <p>{note}</p>
      <button type="button" onClick={() => setGuide(false)}>{text.dismiss}</button>
    </div>}
    {support && <div className="portfolio-support">
      <textarea ref={reportRef} readOnly spellCheck={false} aria-label={text.report}
        value={report} onFocus={event => event.currentTarget.select()} />
      <button type="button" onClick={copy}>{text.copy}</button>
      <span role="status">{feedback === "Copied" ? text.copied : feedback ? text.manual : ""}</span>
    </div>}
  </section>;
}
