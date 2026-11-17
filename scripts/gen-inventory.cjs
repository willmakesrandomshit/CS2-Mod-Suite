// Generates docs/portfolio-inventory.json from facts that can be verified locally:
// source version (csproj), publish metadata (PublishConfiguration.xml), README status line,
// the version of the same mod installed in the local CS2 Mods folder (if any), and the
// installed game build. Fields that cannot be verified offline are null, never guessed.
//   node scripts/gen-inventory.cjs [--installed "<Mods dir>"] [--game "<Cities2_Data dir>"]
const fs = require('fs'), path = require('path');
const root = path.resolve(__dirname, '..');
const arg = (n, d) => { const i = process.argv.indexOf(n); return i > 0 ? process.argv[i + 1] : d; };
const modsDir = arg('--installed', path.join(process.env.USERPROFILE || '', 'AppData/LocalLow/Colossal Order/Cities Skylines II/Mods'));
const dataDir = arg('--game', 'C:/Program Files (x86)/Steam/steamapps/common/Cities Skylines II/Cities2_Data');

// folder -> { csproj, installed folder name(s), license }
const projects = {
  AccessStudio: { proj: 'AccessStudio/AccessStudio.csproj', installed: ['AccessStudio'], license: 'MIT' },
  CityPulse: { proj: 'CityPulse/CityPulse.csproj', installed: ['CityPulse'], license: 'MIT' },
  ContourPlus: { proj: 'ContourPlus/ContourPlus.csproj', installed: ['ContourPlus'], license: 'MIT' },
  CrashLens: { proj: 'CrashLens/CrashLens.csproj', installed: ['CrashLens'], license: 'MIT' },
  DiscordRPC: { proj: 'DiscordRPC/DiscordRPC.csproj', installed: ['DiscordRPC'], license: 'MIT' },
  EventEngine: { proj: 'EventEngine/EventEngine.csproj', installed: ['EventEngine'], license: 'MIT' },
  FastTrack: { proj: 'FastTrack/FastTrack.csproj', installed: ['FastTrack'], license: 'MIT' },
  GrowableModules: { proj: 'GrowableModules/GrowableModulesPrototype.csproj', installed: ['GrowableModulesPrototype'], license: 'MIT' },
  JunctionStudio: { proj: 'JunctionStudio/JunctionStudio.csproj', installed: ['JunctionStudio'], license: 'GPL-3.0' },
  MagicMarking: { proj: 'MagicMarking/src/MarkingStudio/MarkingStudio.csproj', installed: ['MarkingStudio'], license: 'GPL-3.0' },
  NetworkStudio: { proj: 'NetworkStudio/NetworkStudio.csproj', installed: ['NetworkStudio'], license: 'MIT' },
  Parkify: { proj: 'Parkify/Parkify.csproj', installed: ['Parkify'], license: 'MIT' },
  RoadRules: { proj: 'RoadRules/RoadRules.csproj', installed: ['RoadRules'], license: 'MIT' },
  SaveGuard: { proj: 'SaveGuard/SaveGuard.csproj', installed: ['SaveGuard'], license: 'MIT' },
  TrafficStressLab: { proj: 'TrafficStressLab/TrafficStressTester.csproj', installed: ['TrafficStressTester'], license: 'MIT' },
};
for (const extra of ['CitizenStories', 'AutoDirector']) {
  const p = `${extra}/${extra}.csproj`;
  if (fs.existsSync(path.join(root, p))) projects[extra] = { proj: p, installed: [extra], license: 'MIT' };
}

const read = f => { try { return fs.readFileSync(f, 'utf8'); } catch { return null; } };
const m = (s, re) => (s && (s.match(re) || [])[1]) || null;

function installedVersion(names) {
  for (const n of names) {
    const j = read(path.join(modsDir, n, 'mod.json'));
    const v = m(j, /"version"\s*:\s*"([^"]+)"/);
    if (v) return v;
  }
  return null;
}

function gameBuild() {
  // The build string is embedded in the shipped assemblies; reading it does not launch the game.
  const out = {};
  for (const f of ['globalgamemanagers']) {
    let b = null;
    try { b = fs.readFileSync(path.join(dataDir, f)); } catch { /* not installed here */ }
    if (b) { const v = m(b.toString('latin1'), /\b(1\.\d{1,2}\.\d{1,2}f\d+)\b/); if (v) out[f] = v; }
  }
  return out;
}

const items = [];
for (const [name, p] of Object.entries(projects)) {
  const csproj = read(path.join(root, p.proj));
  const dir = path.dirname(path.join(root, p.proj));
  const pub = read(path.join(dir, 'Properties/PublishConfiguration.xml'));
  const readme = read(path.join(root, name, 'README.md'));
  items.push({
    name,
    sourceVersion: m(csproj, /<Version>([^<]+)<\/Version>/),
    pdxModId: m(pub, /<ModId Value="(\d+)"/),
    pdxAccessLevelInMetadata: m(pub, /<AccessLevel Value="([^"]+)"/),
    publishMetadataVersion: m(pub, /<ModVersion Value="([^"]+)"/),
    installedLocalVersion: installedVersion(p.installed),
    // Only a live Paradox Mods check can establish this; it is intentionally never guessed.
    currentPublishedPdxVersion: null,
    sourceStatusLine: m(readme, /\*\*Source status:\*\*\s*([^\n]+)/),
    license: p.license,
    hasOfflineTests: ['TrafficStressLab', 'CitizenStories', 'AutoDirector'].includes(name),
    inGameVerifiedThisPass: false,
  });
}

const out = {
  generatedBy: 'scripts/gen-inventory.cjs',
  installedGameBuild: gameBuild(),
  note: 'inGameVerifiedThisPass is false for every entry: no in-game testing happened. currentPublishedPdxVersion is null: not checked against live Paradox Mods.',
  projects: items,
};
fs.writeFileSync(path.join(root, 'docs/portfolio-inventory.json'), JSON.stringify(out, null, 2) + '\n');
for (const i of items) console.log([i.name.padEnd(16), (i.sourceVersion || '-').padEnd(16), (i.installedLocalVersion || '-').padEnd(16), i.pdxModId || '-'].join(' '));
console.log('game build:', JSON.stringify(out.installedGameBuild));
