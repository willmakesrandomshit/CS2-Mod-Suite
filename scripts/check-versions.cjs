// Offline consistency check: csproj <Version> vs PublishConfiguration.xml ModVersion
// and the "source version" column of docs/SOURCE_INVENTORY.md. Needs no game files.
const fs = require('fs'), path = require('path');
const root = path.resolve(__dirname, '..');
const inv = fs.readFileSync(path.join(root, 'docs/SOURCE_INVENTORY.md'), 'utf8');
const rows = {};
for (const l of inv.split('\n')) {
  const c = l.split('|').map(s => s.trim());
  if (c.length > 7 && /^[A-Za-z]+$/.test(c[1])) rows[c[1]] = c[4];
}
function* walk(d) {
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    if (['node_modules', 'bin', 'obj', '.git', 'dist'].includes(e.name)) continue;
    const p = path.join(d, e.name);
    if (e.isDirectory()) yield* walk(p); else if (e.name.endsWith('.csproj')) yield p;
  }
}
let bad = 0;
for (const proj of walk(root)) {
  const v = (fs.readFileSync(proj, 'utf8').match(/<Version>([^<]+)<\/Version>/) || [])[1];
  const dir = path.dirname(proj);
  const top = path.relative(root, proj).split(path.sep)[0];
  const msgs = [];
  const pub = path.join(dir, 'Properties/PublishConfiguration.xml');
  if (v && fs.existsSync(pub)) {
    const pv = (fs.readFileSync(pub, 'utf8').match(/<ModVersion Value="([^"]+)"/) || [])[1];
    if (pv && pv !== v) msgs.push(`PublishConfiguration ${pv} != csproj ${v}`);
  }
  if (v && rows[top] && rows[top] !== v) msgs.push(`SOURCE_INVENTORY ${rows[top]} != csproj ${v}`);
  console.log((msgs.length ? 'MISMATCH ' : 'ok       ') + top + ' ' + (v || '(no version)') + (msgs.length ? ' - ' + msgs.join('; ') : ''));
  bad += msgs.length;
}
process.exit(bad ? 1 : 0);
