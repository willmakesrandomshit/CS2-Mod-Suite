# In-game QA runbook (for an agent that can control the game)

Offline checks (`scripts/test-portfolio.cjs`, `scripts/*Tests`, `scripts/build-all.ps1 -NoDeploy`) never
prove runtime behaviour. Record game results separately in `docs/qa.md` with: game build, mod build,
playset, date, city, procedure, observation, logs, pass/fail.

## What was tried on 2026-11-17 (game build 1.6.4f1, read from the game's own files)

| Attempt | Result |
|---|---|
| `steam://rungameid/949230` from this session | Started the Paradox Launcher UI on the desktop. Not headless. Closed. |
| `Cities2.exe -batchmode -nographics -logFile <own log>` (hidden window) | Process started and logged `Game version: 1.6.4f1 (1097.158a)`, then `[SceneFlow] [ERROR] A platform service integration failed to initialize`, after which `GameManager.Update` threw NullReferenceException every frame (log grew ~200 MB in under a minute). Killed. **Direct headless launch is not a usable test path.** Head of the log: `qa-evidence` (not committed). |
| Desktop control (mouse/keyboard/screenshots) | **No such tool exists in the session that did this work.** Only a browser pane and Chrome extension, which cannot drive a native game. |

Side observation: the game log says `Persistent folder version is outdated 1.6.2f1`, i.e. the game's
user-data folder predates the 1.6.4f1 update; the first normal launch will migrate it.

## Capability still required
A launch through Steam/Paradox Launcher (platform services need it) **plus** desktop automation with
screenshots. A hidden Windows desktop or virtual display would also work if the agent can capture and
inject input there. Without that, UI workflows cannot be executed by an agent.

## Checks that can run from logs once the game is up (no UI input needed)
1. Mod load: each suite mod logs `<Mod> <version> loaded`; grep Player.log and `Logs/<Mod>.log`.
2. No `Exception`/`[ERROR]` lines from the mod's namespace at main menu.
3. Version string in the log equals `mod.json` / csproj version (catches stale installs).

## Per-mod runtime checklist
See `docs/INGAME-CHECKLIST.md` (SaveGuard restore, RoadRules lane matrix, Discord presets, Junction Studio
with Town Road Lane, FastTrack LOD, Citizen Stories, Auto Director, BuildGuard edit preview).

## Safety rules for any automated run
- Only disposable cities; never write to an existing save.
- Install candidates to a separate Mods directory or a dedicated playset, not over the player's installs.
- Do not edit Steam launch options or account settings.
