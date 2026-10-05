import { useEffect, useRef, useState } from 'react';
import { bindValue } from 'cs2/api';
import { comparableCamera, comparisonSampleCount, stableContext, summarise, validSample } from './comparison-logic';
import type { PerformanceSample, SampleSummary } from './comparison-logic';

const sequence = bindValue<number>('fastTrack', 'sampleSequence', 0);
const measuring = bindValue<boolean>('fastTrack', 'canMeasure', false);
const fps = bindValue<number>('fastTrack', 'fps', 0);
const main = bindValue<number>('fastTrack', 'mainThreadMs', 0);
const gpu = bindValue<number>('fastTrack', 'gpuMs', 0);
const camera = bindValue<string>('fastTrack', 'cameraState', 'Unknown');
const altitude = bindValue<number>('fastTrack', 'cameraAltitude', 0);
const moving = bindValue<boolean>('fastTrack', 'cameraMoving', false);
const adaptive = bindValue<boolean>('fastTrack', 'adaptiveLodEnabled', false);
const active = bindValue<boolean>('fastTrack', 'adaptiveLodActive', false);
const scale = bindValue<number>('fastTrack', 'lodScale', 1);

export function PerformanceComparison() {
  const [available, setAvailable] = useState(measuring.value);
  const [baseline, setBaseline] = useState<SampleSummary>();
  const [comparison, setComparison] = useState<SampleSummary>();
  const [progress, setProgress] = useState('');
  const capture = useRef<{ target: 'baseline' | 'comparison'; samples: PerformanceSample[] }>();
  const previousSequence = useRef(sequence.value);

  useEffect(() => {
    const enabledSub = measuring.subscribe(value => {
      setAvailable(value);
      if (!value && capture.current) { capture.current = undefined; setProgress('Capture cancelled: monitoring is paused.'); }
    });
    setAvailable(enabledSub.value);
    const sampleSub = sequence.subscribe(tick => {
      if (tick <= previousSequence.current) {
        if (tick < previousSequence.current || tick === 0) {
          capture.current = undefined; setBaseline(undefined); setComparison(undefined); setProgress('City changed; capture a new baseline.');
        }
        previousSequence.current = tick;
        return;
      }
      previousSequence.current = tick;
      const pending = capture.current;
      if (!pending) return;
      const sample: PerformanceSample = { fps: fps.value, mainMs: main.value, gpuMs: gpu.value, camera: camera.value,
        altitude: altitude.value, moving: moving.value, adaptive: adaptive.value, active: active.value, scale: scale.value };
      if (!validSample(sample) || (pending.samples.length && !stableContext(pending.samples[0], sample))) {
        capture.current = undefined; setProgress('Capture cancelled: camera or detail state changed. Hold the view steady and retry.'); return;
      }
      pending.samples.push(sample);
      setProgress(`${pending.target === 'baseline' ? 'Baseline' : 'Comparison'}: ${pending.samples.length}/${comparisonSampleCount} samples`);
      if (pending.samples.length === comparisonSampleCount) {
        const result = summarise(pending.samples);
        if (pending.target === 'baseline') setBaseline(result); else setComparison(result);
        capture.current = undefined;
        setProgress('Capture complete.');
      }
    });
    return () => { enabledSub.dispose(); sampleSub.dispose(); capture.current = undefined; };
  }, []);

  const start = (target: 'baseline' | 'comparison') => {
    if (!measuring.value || capture.current) return;
    if (target === 'baseline') { setBaseline(undefined); setComparison(undefined); }
    else setComparison(undefined);
    capture.current = { target, samples: [] };
    setProgress(`Capturing ${target}; hold the camera and simulation speed steady.`);
  };
  const display = (sample: SampleSummary) => `${sample.fps.toFixed(1)} FPS · main ${sample.mainMs > 0 ? sample.mainMs.toFixed(1) + ' ms' : 'unavailable'} · GPU ${sample.gpuMs > 0 ? sample.gpuMs.toFixed(1) + ' ms' : 'unavailable'}`;
  const cameraMatches = baseline && comparison && comparableCamera(baseline, comparison);
  return <section className="portfolio-help">
    <strong>Performance comparison</strong>
    <p>Collects 12 one-second observations of smoothed counters. Keep the same view, simulation speed and graphics settings; change one option between captures.</p>
    <div>
      <button type="button" disabled={!available || !!capture.current} onClick={() => start('baseline')}>Capture baseline</button>
      <button type="button" disabled={!available || !baseline || !!capture.current} onClick={() => start('comparison')}>Capture comparison</button>
      {capture.current && <button type="button" onClick={() => { capture.current = undefined; setProgress('Capture cancelled.'); }}>Cancel</button>}
    </div>
    {!available && <p>Enable FastTrack and its profiler in Options to measure.</p>}
    <p role="status">{progress}</p>
    {baseline && <p>Baseline: {display(baseline)} · Adaptive Detail {baseline.adaptive ? 'on' : 'off'} ({Math.round(baseline.scale * 100)}%)</p>}
    {comparison && <p>Comparison: {display(comparison)} · Adaptive Detail {comparison.adaptive ? 'on' : 'off'} ({Math.round(comparison.scale * 100)}%)</p>}
    {baseline && comparison && <p>{cameraMatches
      ? `Observed FPS change: ${((comparison.fps / baseline.fps - 1) * 100).toFixed(1)}%. This does not establish a causal speedup.`
      : 'Camera conditions differ. Capture both samples again from the same view.'}</p>}
  </section>;
}
