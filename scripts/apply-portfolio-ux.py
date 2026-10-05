"""Apply the reviewed shared UI to the suite and explicitly supplied local checkouts.

Never deploys or publishes. UI copies are self-contained for each mod's bundler.
Run from the suite; --work/--lot/--marking point to existing source checkouts.
"""
import argparse
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GUIDES = {
    'AccessStudio': ('Access Studio', ['Activate the probe and select a supported building.', 'Choose Move Service Access and pick a valid road.', 'Use Reset Vanilla to restore the original access.'], 'Remote access is experimental. Verify service routing in a disposable city.'),
    'CityPulse': ('City Pulse', ['Wait for a city scan, then choose a diagnostic category.', 'Select an alert and compare its measured evidence with the inferred cause.', 'Use the location action to inspect the affected area.'], 'Scores and inferred causes are diagnostic clues, not proof.'),
    'ContourPlus': ('Contour Plus', ['Enable contours and choose an interval.', 'Inspect the terrain, then use the grade-planning controls.', 'Turn contours off when finished.'], 'Sampling is bounded. Readouts are planning estimates, not surveying data.'),
    'CrashLens': ('CrashLens', ['Wait for the log scan to finish.', 'Review each lead and its supporting evidence.', 'Export a report when reporting an issue.'], 'A suspect ranking does not prove which mod caused an error. Review exported logs before sharing.'),
    'DiscordRPC': ('Discord RPC', ['Check the connection status with Discord running.', 'Choose a privacy preset before enabling enhanced presence.', 'Review the displayed activity and use Refresh now.'], 'The preview is not proof that Discord accepted the activity. Check the actual Discord client.'),
    'EventEngine': ('Event Engine', ['Choose a supported venue.', 'Set a start time, attendance and duration.', 'Review the active event and stop it when finished.'], 'Visit requests are bounded and do not guarantee that every requested visitor appears.'),
    'FastTrack': ('FastTrack', ['Keep the camera and simulation speed steady while measuring.', 'Capture a baseline, change one option, then capture a comparison.', 'Inspect Adaptive Detail status; zoom in to check restored detail.'], 'A comparison is observational. Camera, simulation and other mods can affect FPS.'),
    'JunctionStudio': ('Junction Studio', ['Enable Town Road Lane and select a junction with its marking tool.', 'Copy markings or save a named preset.', 'Apply to a compatible junction and inspect the result.'], 'Requires Town Road Lane. Magic Marking conflicts with that dependency. This tool changes visual markings, not traffic routing.'),
    'MagicMarking': ('Magic Marking', ['Activate the marking tool and select a road or junction.', 'Choose a line or area style and inspect the drawing preview.', 'Use Undo/Redo to recover from a change.'], 'Enable Magic Marking or Town Road Lane, not both. Road markings do not change traffic rules.'),
    'NetworkStudio': ('Network Studio', ['Select a road segment.', 'Inspect the detected furniture and distance from the road centre.', 'Clear selection or close the panel when finished.'], 'This candidate is inspection-only. Furniture relocation is withheld pending recovery and reload verification.'),
    'Parkify': ('Parkify', ['Load a disposable city.', 'Inspect the park and pedestrian-lane counts.', 'Use the game to inspect actual access and service routing.'], 'This candidate is read-only. It does not create access or suppress road-access warnings.'),
    'RoadRules': ('Road Rules', ['Select a road and inspect its physical lanes.', 'Choose a lane rule or preset, then review the overlay.', 'Observe actual vehicles before applying the rule more widely.'], 'Some rules are soft preferences. A painted overlay is not proof of enforcement.'),
    'SaveGuard': ('SaveGuard', ['Select the disposable save you want to protect.', 'Create a restore point before changing the city.', 'Restore into a separate copy and verify that copy loads.'], 'A successful backup does not guarantee recovery. Keep an independent copy of valuable saves.'),
    'TrafficStressLab': ('Traffic Stress Lab', ['Use a disposable city and start with Light pressure.', 'Watch request counts and actual traffic separately.', 'Stop the test to stop generating new extra requests.'], 'Queued requests and existing vehicles finish normally after stopping. Requests are not guaranteed visible vehicles.'),
    'DemandLens': ('Demand Lens', ['Wait for the building scan.', 'Select a troubled building and inspect measured evidence.', 'Compare the suggested cause with the actual building status.'], 'Building health is a heuristic, not a replacement for the game simulation.'),
    'LaneDoctor': ('Lane Doctor', ['Scan the road network.', 'Select an issue and inspect evidence and the repair preview.', 'Only apply a supported repair after reviewing the affected road.'], 'Use a disposable city for repairs. Keep a save made before the repair.'),
    'ParkingPulse': ('Parking Pulse', ['Wait for a parking scan.', 'Select a facility and inspect capacity and occupancy.', 'Compare full and underused facilities with surrounding access.'], 'Occupancy is an observation. It does not establish why drivers chose a facility.'),
    'ServiceDoctor': ('Service Doctor', ['Wait for the facility scan.', 'Inspect a facility with a warning or low efficiency.', 'Compare vehicle usage, load and measured evidence.'], 'Suggested causes require checking the actual service building.'),
    'TrafficPulse': ('Traffic Pulse', ['Wait for traffic observations.', 'Select a bottleneck and inspect speed and queue pressure.', 'Capture a baseline before changing the road network.'], 'Speed and congestion estimates are sampled. Inferred causes are not proven.'),
    'TransitPulse': ('Transit Pulse', ['Wait for the line scan.', 'Compare line utilisation, waiting passengers and bunching.', 'Inspect the affected stop before changing service.'], 'Headways and utilisation are observations, not guaranteed timetable performance.'),
    'LotStudio': ('Lot Studio', ['Select a supported building and choose Edit Lot.', 'Select a decoration, then Move, Rotate or Place; use Undo to recover.', 'Exit before saving. Reset requires a second confirmation.'], 'Building recovery and the complete save/reload workflow still require the current runtime QA gate.'),
}

def write(path, text):
    path.write_text(text, encoding='utf-8', newline='\n')

def install_runtime(ui, manifest):
    project_dir = ui.parent if ui.name != 'MarkingStudioUI' else ui.parent / 'MarkingStudio'
    mod_path = project_dir / 'Mod.cs'
    mod_text = mod_path.read_text(encoding='utf-8')
    namespace = re.search(r'namespace\s+([\w.]+)', mod_text).group(1)
    template = (ROOT / 'shared/PortfolioSupportUISystem.cs.template').read_text(encoding='utf-8')
    write(project_dir / 'PortfolioSupportUISystem.cs', template.replace('__NAMESPACE__', namespace).replace('__ID__', manifest['id']))
    if 'UpdateAt<PortfolioSupportUISystem>' not in mod_text:
        match = re.search(r'^([ \t]*)updateSystem.UpdateAt<', mod_text, re.M)
        if not match: raise RuntimeError(f'No system registration point: {mod_path}')
        mod_text = mod_text[:match.start()] + match[1] + 'updateSystem.UpdateAt<PortfolioSupportUISystem>(SystemUpdatePhase.UIUpdate);\n' + mod_text[match.start():]
        write(mod_path, mod_text)
    projects = list(project_dir.glob('*.csproj'))
    if len(projects) != 1: raise RuntimeError(f'Ambiguous project: {project_dir}')
    text = projects[0].read_text(encoding='utf-8')
    if 'Include="UnityEngine.IMGUIModule"' not in text:
        match = re.search(r'^([ \t]*)</ItemGroup>', text, re.M)
        indent = match[1]
        child_indent = indent + ('\t' if '\t' in indent else '  ')
        text = text[:match.start()] + child_indent + '<Reference Include="UnityEngine.IMGUIModule"><Private>false</Private></Reference>\n' + text[match.start():]
        write(projects[0], text)
    webpack = ui / 'webpack.config.js'
    text = webpack.read_text(encoding='utf-8')
    if "'cs2/api':" not in text and '"cs2/api":' not in text:
        # These two early prototypes originally imported only cs2/bindings.
        text = re.sub(r'(externals:\s*\{)', r"\1\n    'cs2/api': 'cs2/api',", text, count=1)
        write(webpack, text)

def apply(mod, ui, source, marker, lot=False):
    name, steps, note = GUIDES[mod]
    src = ui / 'src'
    for filename in ('portfolio-ux.tsx', 'portfolio-logic.ts', 'portfolio-ux.scss'):
        shutil.copyfile(ROOT / 'shared' / filename, src / filename)
    text = source.read_text(encoding='utf-8')
    manifest_path = ui / 'mod.json'
    if not manifest_path.exists(): manifest_path = ui.parent / 'mod.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    install_runtime(ui, manifest)
    runtime_prop = 'runtimeGroup={' + json.dumps('Portfolio.' + manifest['id']) + '} '
    bindings = re.findall(r'''const\s+(\w+\$?)\s*=\s*bindValue<(boolean|number)>\s*\(\s*["'][^"']+["']\s*,\s*["']([^"']+)["']''', text)
    pairs = ', '.join(f'{json.dumps(key)}: {var}' for var, _, key in bindings[:40])
    config = f'const portfolioBindings = {{ {pairs} }};\n'
    if 'PortfolioHelp' in text:
        text = re.sub(r'^const portfolioBindings = .*?;\n', lambda _: config, text, count=1, flags=re.M)
        if 'runtimeGroup=' not in text:
            text = text.replace('<PortfolioHelp ', '<PortfolioHelp ' + runtime_prop, 1)
        write(source, text)
        return
    version = manifest['version']
    # Status reports deliberately omit strings: bindings can contain city names,
    # file paths, user-written templates or large JSON payloads.
    imports = './portfolio-ux' if source.parent == src else '../portfolio-ux'
    text = f'import {{ PortfolioHelp, useRememberedPreference }} from "{imports}";\n' + text
    # Only display drawers are remembered, never mutation modes/confirmation.
    for pref in ('showAdvanced', 'showHistory', 'showOutliner'):
        text = re.sub(rf'(\[{pref},\s*\w+\]\s*=\s*)useState\(false\)', rf'\1useRememberedPreference("{mod.lower()}.{pref}", false)', text)
    # Place module constants after imports; preceding a binding declaration is
    # unsafe because of TDZ, so insert before the first component declaration.
    pos = re.search(r'^(?:export const|const ToolbarButton|const ProbePanel|export function|function MarkingStudioPanel)', text, re.M)
    if not pos:
        # Magic Marking has several helper components before its main panel.
        pos = re.search(r'^function\s+\w+', text, re.M)
    if not pos:
        pos = re.search(r'^const\s+\w+:\s*React.FC', text, re.M)
    if not pos:
        raise RuntimeError(f'No safe component insertion point: {source}')
    text = text[:pos.start()] + config + text[pos.start():]
    props = f'name={{{json.dumps(name)}}} version={{{json.dumps(version)}}} steps={{{json.dumps(steps)}}} note={{{json.dumps(note)}}} bindings={{portfolioBindings}}'
    if lot:
        # Lot Studio uses the game's localisation dictionary for new text.
        props = ('name="Lot Studio" version={' + json.dumps(version) + '} '
                 'steps={[1,2,3].map(i => locale.translate(`LotStudio.Guide.Step.${i}`) ?? "")} '
                 'note={locale.translate("LotStudio.Guide.Note") ?? ""} bindings={portfolioBindings} '
                 'text={{ help: locale.translate("LotStudio.Help") ?? "Quick guide", hide: locale.translate("LotStudio.Help.Hide") ?? "Hide guide", '
                 'dismiss: locale.translate("LotStudio.Help.Dismiss") ?? "Got it", support: locale.translate("LotStudio.Support") ?? "Support snapshot", '
                 'copy: locale.translate("LotStudio.Support.Copy") ?? "Copy snapshot", copied: locale.translate("LotStudio.Support.Copied") ?? "Copied", '
                 'manual: locale.translate("LotStudio.Support.Manual") ?? "Select the text and press Ctrl+C.", report: locale.translate("LotStudio.Support.Report") ?? "Status snapshot" }}')
    if marker not in text:
        raise RuntimeError(f'Missing panel marker {marker}: {source}')
    text = text.replace(marker, f'<PortfolioHelp {runtime_prop}{props} />\n      ' + marker, 1)
    if lot:
        text = text.replace('createPortal(<div style={toolbarStyle}>', 'createPortal(<div data-portfolio-panel style={toolbarStyle}>')
    else:
        text = re.sub(r'(<(?:div|section)\s+className="[^"]*panel[^"\n]*")', r'\1 data-portfolio-panel', text, count=1)
    write(source, text)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--work', type=Path)
    parser.add_argument('--lot', type=Path)
    parser.add_argument('--marking', type=Path)
    args = parser.parse_args()
    for mod in list(GUIDES)[:14]:
        if mod == 'MagicMarking':
            ui = ROOT / mod / 'src/MarkingStudioUI'
            source = ui / 'src/mods/marking-studio-panel.tsx'
            marker = '<PanelStickyChrome>'
        else:
            ui = ROOT / mod / 'UI'
            source = ui / 'src/index.tsx'
            marker = {'AccessStudio': '<main>', 'DiscordRPC': '<main>', 'NetworkStudio': '<div className="ns-content">', 'Parkify': '<div className="pk-content">'}.get(mod, '<div className="suite-body">')
            if mod == 'AccessStudio': marker = '<p className="as-status">'
        apply(mod, ui, source, marker)
    if args.work:
        for mod in list(GUIDES)[14:20]:
            ui = args.work / mod / 'UI'
            marker = {'TrafficPulse': '<div className="tp-health-banner">', 'LaneDoctor': '<div className="ld-health-card">'}.get(mod, '<div className="panel-body">')
            apply(mod, ui, ui / 'src/index.tsx', marker)
    if args.lot:
        ui = args.lot / 'LotStudioUI'
        apply('LotStudio', ui, ui / 'src/mods/lot-studio-action.tsx', '<div style={modeRowStyle}>', lot=True)
        locale = args.lot / 'Localization/EnglishLocale.cs'
        text = locale.read_text(encoding='utf-8')
        if '"LotStudio.Help"' not in text:
            _, steps, note = GUIDES['LotStudio']
            labels = {'Help':'Quick guide', 'Help.Hide':'Hide guide', 'Help.Dismiss':'Got it', 'Support':'Support snapshot', 'Support.Copy':'Copy snapshot', 'Support.Copied':'Copied', 'Support.Manual':'Select the text and press Ctrl+C to copy.', 'Support.Report':'Status snapshot', 'Guide.Note':note}
            labels.update({f'Guide.Step.{i}': step for i, step in enumerate(steps, 1)})
            lines = '\n'.join(f'                {{ "LotStudio.{key}", {json.dumps(value)} }},' for key, value in labels.items())
            text = text.replace('                { "LotStudio.EditLot",', lines + '\n                { "LotStudio.EditLot",', 1)
            write(locale, text)
    if args.marking:
        ui = args.marking / 'src/MarkingStudioUI'
        apply('MagicMarking', ui, ui / 'src/mods/marking-studio-panel.tsx', '<PanelStickyChrome>')

if __name__ == '__main__': main()
