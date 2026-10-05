const fs = require('fs');
const path = require('path');
const vm = require('vm');
const assert = require('assert/strict');
const root = path.resolve(__dirname, '..');
const ts = require(path.join(root, 'FastTrack/UI/node_modules/typescript'));
function load(file) {
  const exports = {};
  const result = ts.transpileModule(fs.readFileSync(path.join(root, file), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 }
  });
  vm.runInNewContext(result.outputText, { exports });
  return exports;
}
const prefs = load('shared/portfolio-logic.ts');
const compare = load('FastTrack/UI/src/comparison-logic.ts');
let passed = 0;
function check(name, fn) { fn(); passed++; console.log('PASS: ' + name); }
check('Missing storage leaves preferences usable', () => assert.equal(prefs.readPreference(undefined, 'guide', true), true));
const blocked = { getItem() { throw Error('denied'); }, setItem() { throw Error('denied'); } };
check('Blocked storage does not break panels', () => { assert.equal(prefs.readPreference(blocked, 'guide', false), false); prefs.writePreference(blocked, 'guide', true); });
const memory = new Map();
const store = { getItem: key => memory.get(key) ?? null, setItem: (key, value) => memory.set(key, value) };
check('Guide dismissal survives reopening', () => { prefs.writePreference(store, 'guide', false); assert.equal(prefs.readPreference(store, 'guide', true), false); });
check('Corrupt preferences use the actual default', () => { memory.set('guide', '{bad json'); assert.equal(prefs.readPreference(store, 'guide', true), true); });
check('Reports omit private strings, objects and invalid measurements', () => {
  const text = prefs.formatSnapshot('Example', '1.2', { city: 'private-city', token: 'secret', nested: { secret: true }, fps: NaN, active: true, count: 3 });
  assert(!text.includes('private-city') && !text.includes('secret') && !text.includes('NaN'));
  assert(text.includes('active: true') && text.includes('count: 3') && text.includes('no game logs'));
});
const sample = { fps: 40, mainMs: 20, gpuMs: 0, camera: 'Overview', altitude: 250, adaptive: false, active: false, scale: 1, moving: false };
const samples = Array.from({ length: compare.comparisonSampleCount }, () => ({ ...sample }));
check('Incomplete capture produces no result', () => assert.equal(compare.summarise(samples.slice(1)), undefined));
check('Stable capture computes observed mean and unavailable GPU', () => {
  const result = compare.summarise(samples); assert.equal(result.fps, 40); assert.equal(result.gpuMs, 0); assert.equal(result.count, 12);
});
check('Camera movement invalidates capture', () => assert.equal(compare.summarise(samples.map((s, i) => ({ ...s, moving: i === 6 }))), undefined));
check('Changing detail during capture invalidates it', () => assert.equal(compare.summarise(samples.map((s, i) => ({ ...s, scale: i === 6 ? 0.5 : 1 }))), undefined));
check('Invalid FPS cannot enter result', () => assert.equal(compare.summarise(samples.map((s, i) => ({ ...s, fps: i === 6 ? 0 : 40 }))), undefined));
check('Comparison may change detail but must retain camera context', () => {
  const before = compare.summarise(samples);
  assert(compare.comparableCamera(before, { ...before, adaptive: true, scale: 0.5 }));
  assert(!compare.comparableCamera(before, { ...before, altitude: 500 }));
});
console.log(`${passed} tests passed.`);
