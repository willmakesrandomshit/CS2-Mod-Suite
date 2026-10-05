# Portfolio source pass — 5 October 2026

## Scope

Inspected actual source locations, manifests, startup registration, current Git
changes and panel implementations. This portfolio has 22 distinct mods. The
standalone MarkingStudio checkout is another copy of Magic Marking, not a 23rd
product. Old backups, decompiled references and verification utilities are not
products. Source versions are not claims about published packages.

| Mod | Source location | Current pass / existing workflow |
|---|---|---|
| Access Studio | Suite | Guide for probe, supported access editing and vanilla reset |
| City Pulse | Suite | Evidence-first diagnostic guide; remembered advanced drawer |
| Contour Plus | Suite | Contour/grade guide and support snapshot |
| CrashLens | Suite | Lead-review and existing report-export guide |
| Discord RPC | Suite | Privacy/connection guide; client acceptance remains a separate check |
| Event Engine | Suite | Venue scheduling guide and remembered advanced drawer |
| FastTrack | Suite | 12-observation baseline/comparison capture; active detail state reporting |
| Junction Studio | Suite | Non-mutating replacement summary, explicit confirmation, previous-marking restore |
| Magic Marking | Suite + standalone checkout | Existing styles/templates/undo workflow; marking-conflict detection |
| Network Studio | Suite | Inspection guide; relocation remains withheld |
| Parkify | Suite | Read-only scope and count inspection guide |
| Road Rules | Suite | Physical-lane rule guide and remembered advanced drawer |
| SaveGuard | Suite | Backup/restored-copy guide; actual recovery remains a runtime gate |
| Traffic Stress Lab | Suite | Bounded request guide and remembered advanced drawer |
| Demand Lens | Standalone local source | Building evidence guide; hides score without observed buildings |
| Lane Doctor | Standalone local source | Repair-preview guide; hides score without scanned segments |
| Parking Pulse | Standalone local source | Facility occupancy guide; hides score without observed facilities |
| Service Doctor | Standalone local source | Facility evidence guide; hides scores without observed facilities |
| Traffic Pulse | Standalone local source | Replaces fixed speed with bounded road-flow sampling; removes invented density/propagation evidence |
| Transit Pulse | Standalone local source | Line/stop guide; hides score without observed lines |
| Lot Studio | Separate private repository | Localized guide, remembered drawers, support snapshot, accessible header Exit, styled inputs |
| CityMCP | Separate source repository | Dismissible pairing guide, Escape close, title-only dragging, safe status copy |

## Shared changes

- Self-contained shared UI copies supply restrained input/control styling,
  keyboard focus indicators and a short first-use guide. Guide dismissal and
  existing advanced/history/outliner drawer preferences use versioned UI keys.
  Mutation modes, entity IDs and destructive confirmations are never persisted
  through this preference helper. Blocked/corrupt browser storage falls back
  without breaking the panel.
- Support snapshots show UI version, runtime assembly version, game application
  version and available numeric/boolean bindings. Native clipboard copying uses
  an explicit game-side action; selectable text remains available as fallback.
  Reports omit bound strings, objects, file paths and city names. CityMCP excludes
  its pairing code. These are status snapshots, not automatic log exports.
- Compatibility checks enumerate loaded assemblies only on an explicit panel
  request. They flag duplicate own assemblies and the known Town Road Lane /
  Magic Marking combination. They cannot prove that every dependency is enabled,
  cover every conflict, or rescue Junction Studio if its required assembly prevents
  the mod itself loading. No per-frame assembly enumeration was added.
- Existing presets, catalog search, undo and recovery implementations were
  inspected and retained. No extra simulation optimisations or blanket editing
  support were invented.

## Focused behavior changes

FastTrack collects 12 one-second observations of its existing smoothed counters.
Capture rejects missing FPS, fast camera movement, height/category changes and
changing detail state. Pause cancels capture; city preload clears results; panel
unmount releases subscriptions. Comparisons expose unavailable counters and
camera mismatches. The observed FPS percentage is not a causal performance claim;
unchanged view, simulation speed, workload and graphics remain necessary.

Junction Studio previews counts before replacing markings, captures a validated
rollback before writing, and retains the previous successful state for the same
session. Confirmation and restoration check node identity and ordered approach
entities. Closing, canceling, selection change or city preload disarms pending
replacement. Unsupported marking approaches now fail capture instead of silently
dropping lines. The preview is a summary, not a world-space overlay.

Traffic Pulse previously initialized live speed to 46.2 km/h and never updated it.
It now computes duration-weighted speed from available flow data on at most 120
road segments. Unavailable stopped ratios, speed limits and per-segment vehicle
counts are labelled unavailable; connected roads are not called upstream/downstream
without direction evidence. Congestion, queue pressure and suggested causes remain
explicit heuristics. City preload clears observations, selection and snapshots.

## Evidence and remaining gate

The final pass completed 46/46 Debug/Release builds: all 22 products plus the
standalone MarkingStudio checkout. All builds have zero errors. CityMCP retains
two pre-existing assembly-reference conflict warnings per configuration; all other
builds have zero C# warnings. All 22 React bundles (21 products plus the duplicate
checkout) build successfully; CityMCP uses native IMGUI. Local staging results
record every build. LocalModsPath and CSII_USERDATAPATH overrides keep outputs in
staging; the 33 installed DLL/UI/CSS files checked before and after the final pass
were byte-identical. No PDX package is published.

The new pure preference/report/comparison checks pass 11/11. Lot Studio's tests
pass 38/38. FastTrack also passes TypeScript checking after its browser types were
scoped correctly and narrow declarations added for its existing game controls.

No Computer Use, game launch, current-panel visual inspection or runtime feature
test occurred in this pass: the user's prohibition remains active. Native copy,
storage persistence in Cohtml, simultaneous panel focus, gameplay compatibility,
FastTrack captures, Junction restore and the full Lot Studio recovery/save gate
remain unverified in-game. Full error-log copying remains CrashLens's existing
export workflow; other panels provide safe status snapshots only.

Next: visually review the shared controls, then validate SaveGuard restore-copy
loading, Road Rules enforcement, diagnostic evidence and the editor recovery
workflows in disposable cities. Do not promote a release gate from source builds.

Source checkpoints preserve the existing UI/optimizer work required by these
integrations. Artwork and publishing metadata remain unstaged. The six standalone
non-Git tools and the renamed MarkingStudio checkout have local source archives;
their source was not added to this public repository.
