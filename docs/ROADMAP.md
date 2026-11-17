# Maintenance roadmap (2026-11-17)

Evidence: offline tests pass (11 UI-logic, 56 Traffic Stress Lab); Release builds
with 0 errors/0 warnings (built without deploying; game not launched). In-game
behaviour was **not** re-verified in this pass. No GitHub issues/PRs existed in
CS2-Mod-Suite, RoadRules or CityMCP.

## P0 - do next (needs you in-game, disposable city)
1. **Discord RPC**: test privacy presets, "disable enhanced presence", reconnect, failure panel, new icon. Then decide public vs Unlisted.
2. **RoadRules** (PDX 155436): finish the class/overlay runtime matrix; vehicles with an existing path linger after a rule change - decide fix or document.
3. **Junction Studio**: never loaded in the last QA (Town Road Lane not enabled). Run with TRL enabled; copy/paste, redraw, topology coverage.
4. **SaveGuard**: actually load a restored copy end to end (recovery path is the highest-trust claim).
5. **Source ahead of PDX**: FastTrack 1.3.5, RoadRules 1.4.5, SaveGuard 1.2.4, TSL 1.5.7 etc. are unreleased; verify each in-game before any publish (needs your approval).

## P1 - no game needed
6. Merge this branch (docs/build/CI/lockfile fixes).
7. Extract pure logic (policies, parsers) from big files (RoadRulesSystem 1.5k lines, MagicMarking 2k) into testable files like TrafficStressPolicy, add offline tests.
8. De-duplicate PortfolioSupportUISystem.cs (13 drifted copies of one template) into a shared source include.
9. Replace silent `catch {}` (SaveGuard Setting.cs, ContourPlus TopographySystem, MagicMarking AutoGenerator, DiscordRPC) with logged failures; SuiteBridge ones are probably intentional.
10. Align UI toolchains (TypeScript 4.8.4 vs 5.x, webpack 5.93-5.97) and clear `npm audit` (11 findings, build-time only, not shipped).

## P2 - features / housekeeping
11. Licence hygiene: Magic Marking + Junction Studio GPL-3.0 (keep isolated; JS only references TRL DLL - confirm GPL stance with a licence read); Magic Marking cannot coexist with TRL.
12. Network Studio / Parkify / GrowableModules: decide ship or archive (all unverified prototypes).
13. Keep README/SOURCE_INVENTORY versions in sync (add a check script).
