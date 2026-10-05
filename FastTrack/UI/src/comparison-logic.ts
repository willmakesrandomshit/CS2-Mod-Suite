export interface PerformanceSample {
  fps: number; mainMs: number; gpuMs: number; camera: string; altitude: number;
  adaptive: boolean; active: boolean; scale: number; moving: boolean;
}
export interface SampleSummary extends PerformanceSample { count: number; }

export const comparisonSampleCount = 12;

export function validSample(sample: PerformanceSample): boolean {
  return Number.isFinite(sample.fps) && sample.fps > 0 &&
    Number.isFinite(sample.altitude) && Number.isFinite(sample.scale) && sample.scale > 0 &&
    !sample.moving;
}

export function stableContext(a: PerformanceSample, b: PerformanceSample): boolean {
  return a.camera === b.camera && Math.abs(a.altitude - b.altitude) <= Math.max(5, Math.abs(a.altitude) * 0.02)
    && a.adaptive === b.adaptive && a.active === b.active && Math.abs(a.scale - b.scale) < 0.02;
}

export function summarise(samples: PerformanceSample[]): SampleSummary | undefined {
  if (samples.length < comparisonSampleCount || samples.some(s => !validSample(s) || !stableContext(samples[0], s))) return undefined;
  const mean = (field: 'fps' | 'mainMs' | 'gpuMs') => {
    const available = samples.map(s => s[field]).filter(v => Number.isFinite(v) && v > 0);
    return available.length ? available.reduce((a, b) => a + b, 0) / available.length : 0;
  };
  return { ...samples[0], fps: mean('fps'), mainMs: mean('mainMs'), gpuMs: mean('gpuMs'), count: samples.length };
}

export function comparableCamera(a: SampleSummary, b: SampleSummary): boolean {
  return a.camera === b.camera && Math.abs(a.altitude - b.altitude) <= Math.max(5, Math.abs(a.altitude) * 0.02);
}
