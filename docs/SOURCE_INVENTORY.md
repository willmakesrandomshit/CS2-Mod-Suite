# Public source inventory

Audit date: 2026-09-01

This inventory describes the source snapshot in this repository. `PDX baseline`
is the last locally verified public package version; `source version` is the
version declared by the included source candidate. A newer source version is
not evidence that its binary is currently available from Paradox Mods.

All 14 included projects passed a clean Release build on 2026-09-01 with the
installed Cities: Skylines II modding toolchain: 14/14 projects, 0 compile
errors and 0 compile warnings. That build is a source/toolchain check, not a
substitute for the outstanding in-game runtime matrices noted below.

| Project | Classification | PDX ID / baseline | Source version | Purpose | Important current limitation | Licence |
|---|---|---:|---:|---|---|---|
| FastTrack | Public source ready | 155229 / 1.3.4 | 1.3.5-beta.2 | Adaptive native LOD at distant camera heights plus performance monitoring | Opt-in LOD implementation builds; in-game visual and performance regression is pending | MIT |
| RoadRules | Public source ready | 155436 / 1.4.4 | 1.4.5-beta.1 | Physical-lane access, preferences and closures | Selective class and overlay matrix remains partially verified; some rules are soft preferences | MIT |
| SaveGuard | Public source ready | 155424 / 1.2.3 | 1.2.4-beta.1 | Restore points and restored copies | Recovery is never guaranteed; restored-copy load coverage is incomplete | MIT |
| TrafficStressLab | Public source ready | 155230 / 1.5.4 | 1.5.5-beta.1 | Additional bounded native road-trip requests | High multipliers are experimental; requests are not guaranteed visible vehicles | MIT |
| AccessStudio | Public source ready | 156034 / 0.3.2-alpha.1 | 0.3.3-alpha.1 | Manual service-access editing | Remote Service Point is experimental and support varies by building/network | MIT |
| CityPulse | Public source ready | 155450 / 3.0.3 | 3.0.4-beta.1 | Read-only city observations and heuristics | Diagnostic scores are clues, not proof of a cause | MIT |
| ContourPlus | Public source ready | 155427 / 1.2.4 | 1.2.5-beta.1 | Terrain contours and grade-planning readouts | Bounded sampling can raise effective intervals; not surveying data | MIT |
| CrashLens | Public source ready | 155239 / 1.1.3 | 1.1.4-beta.1 | Local error evidence and support export | Suspect ranking is not attribution; exported reports require privacy review | MIT |
| JunctionStudio | Public source ready | 155242 / 1.1.3 | 1.1.4-beta.1 | Town Road Lane junction preset workflow | Town Road Lane is required; topology/redraw coverage remains incomplete | GPL-3.0 |
| EventEngine | Public source ready | 155652 / 0.2.0-beta.1 | 0.2.1-beta.1 | Venue event scheduling and bounded visit requests | Revised lifecycle still needs broader runtime verification | MIT |
| MagicMarking | Public source ready with upstream attribution | 155247 / 2.4.0 | 2.4.1-beta.1 | Snap-and-paint road-marking editor | Visual only; incompatible with simultaneously enabling Town Road Lane | GPL-3.0 |
| NetworkStudio | Development only | Historical metadata 155650; not represented as released | 0.1.0 | Road-furniture placement prototype | Runtime lifecycle and save behaviour are unverified | MIT |
| Parkify | Development only | Historical metadata 155651; not represented as released | 0.1.0 | Pedestrian-oriented access prototype | Supported scope, routing and teardown are unverified | MIT |
| DiscordRPC | Development only | None | 0.9.0-beta.1 | Configurable Discord presence prototype | Visible client acceptance has not passed a release gate | MIT |

## Source boundaries

- Public source is limited to the 14 folders listed above plus root
  documentation and build/verification scripts.
- Old experiments, duplicate source snapshots, downloaded PDX packages,
  decompiled game/mod source, release staging folders, saves, logs, dumps,
  backups, screenshots and credentials are excluded.
- Deliberate per-mod `Properties/Thumbnail.png` files are included because they
  are original listing assets. Other screenshots are excluded.
- Generated `bin`, `obj`, `Library`, `dist`, `node_modules` and build-log
  content is ignored and is not part of the repository.
- Publishing XML is included only after absolute screenshot paths were removed
  and thumbnail references were made relative. No authentication/session data
  is included.

## Security and privacy review

- Gitleaks 8.30.1 reported no findings in the allowlisted source tree.
- A second repository-specific scanner found no private keys, bearer tokens,
  webhook URLs, credentials, personal email addresses or personal profile
  paths in publishable files.
- Discord RPC's numeric application ID is a public Discord application/client
  identifier, not a token or credential.
- No potential secret was found that requires a publication stop.

## Third-party and licence review

The suite is deliberately not covered by one blanket MIT claim. Magic Marking
is a modified GPL-3.0 fork of Town Road Lane. Junction Studio directly uses
Town Road Lane data and types and is conservatively GPL-3.0. The other folders
are MIT. Game/toolchain assemblies and dependency binaries are not included.
See [the root licence](../LICENSE) and
[third-party notices](../THIRD_PARTY_NOTICES.md).

## Build inputs

Builds require a local licensed Cities: Skylines II installation and its
official modding toolchain. Paths are supplied through `CSII_TOOLPATH` or
`-ToolPath`; no personal machine path is required by project source. Junction
Studio additionally resolves a locally installed Town Road Lane DLL. See
[BUILDING.md](../BUILDING.md).
