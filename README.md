# Cities: Skylines II Mod Suite

A collection of Cities: Skylines II mods focused on traffic, road markings,
diagnostics, access editing, events, performance monitoring and city-management
utilities.

This repository is the source snapshot. Paradox Mods remains the normal player
distribution channel. Some source candidates are newer than the latest public
PDX package and are labelled accordingly; source availability is not a claim
that every feature has passed runtime verification.

Repository: https://github.com/willmakesrandomshit/CS2-Mod-Suite

| Mod | Purpose | PDX | Status | Source |
|---|---|---|---|---|
| FastTrack | Performance monitoring and an opt-in loading-budget experiment | [155229](https://mods.paradoxplaza.com/mods/155229/Windows) | Beta | [FastTrack](FastTrack/) |
| Road Rules | Physical-lane access, preferences and closures | [155436](https://mods.paradoxplaza.com/mods/155436/Windows) | Beta / partially verified | [RoadRules](RoadRules/) |
| SaveGuard | Restore points and non-destructive restored copies | [155424](https://mods.paradoxplaza.com/mods/155424/Windows) | Beta | [SaveGuard](SaveGuard/) |
| Traffic Stress Lab | Additional native road-trip requests for test cities | [155230](https://mods.paradoxplaza.com/mods/155230/Windows) | Experimental beta | [TrafficStressLab](TrafficStressLab/) |
| Access Studio | Manual building service-access editing | [156034](https://mods.paradoxplaza.com/mods/156034/Windows) | Early alpha | [AccessStudio](AccessStudio/) |
| City Pulse | Read-only city diagnostics and heuristics | [155450](https://mods.paradoxplaza.com/mods/155450/Windows) | Beta | [CityPulse](CityPulse/) |
| Contour Plus | Terrain contours and grade-planning readouts | [155427](https://mods.paradoxplaza.com/mods/155427/Windows) | Beta | [ContourPlus](ContourPlus/) |
| CrashLens | Local error evidence and support-report export | [155239](https://mods.paradoxplaza.com/mods/155239/Windows) | Beta | [CrashLens](CrashLens/) |
| Junction Studio | Town Road Lane junction preset workflow | [155242](https://mods.paradoxplaza.com/mods/155242/Windows) | Beta; GPL dependency | [JunctionStudio](JunctionStudio/) |
| Event Engine | Venue event scheduling and bounded visit requests | [155652](https://mods.paradoxplaza.com/mods/155652/Windows) | Beta | [EventEngine](EventEngine/) |
| Magic Marking | Snap-and-paint road-marking editor | [155247](https://mods.paradoxplaza.com/mods/155247/Windows) | Beta; GPL-3.0 fork | [MagicMarking](MagicMarking/) |
| Network Studio | Road-furniture placement prototype | Not publicly released | Development only | [NetworkStudio](NetworkStudio/) |
| Parkify | Pedestrian-oriented park-access prototype | Not publicly released | Development only | [Parkify](Parkify/) |
| Discord RPC | Configurable Discord presence prototype | Not publicly released | Development only | [DiscordRPC](DiscordRPC/) |

## Building

See [BUILDING.md](BUILDING.md). The repository contains source and lockfiles,
not game assemblies, SDK binaries, `node_modules`, compiled DLLs or PDX login
data.

## Development

These mods are developed with AI-assisted tooling alongside manual design,
testing, debugging, runtime verification and review. Source is public so
players and reviewers can inspect how the mods work and report concrete issues.

## Contributing and security

Bug reports and focused pull requests are welcome; see
[CONTRIBUTING.md](CONTRIBUTING.md). Do not publish private logs, saves or
credentials in an issue. Security-sensitive reports should follow
[SECURITY.md](SECURITY.md).

## Licensing

This is a mixed-license aggregate. Independently authored suite projects are
MIT unless their folder says otherwise. Magic Marking and Junction Studio are
GPL-3.0. See [LICENSE](LICENSE) and
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
