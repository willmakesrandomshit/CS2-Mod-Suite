# Runtime QA

## 2026-10-02 — CSII 1.6.2f1

- Launched CSII through Steam and the Paradox launcher using the active `New Playset`.
- Magic Marking Debug and Release builds completed with 0 warnings and 0 errors; webpack succeeded. Installed Release DLL and UI bundle SHA-256 matched build output.
- Cold-launch logs show Magic Marking loaded, registered all seven vanilla surface clones at the main menu, and emitted no `[late-clone] gate not passed`, warning, error, or exception. This verifies the startup diagnostic fix in `VanillaSurfaceLateClone`.
- Lot Studio smoke check on the disposable `lotstudioqa20260927v2` city: Edit Lot opened on the Small Elementary School, boundary was visible around the lot, Flowerbed01 selection worked, Move committed, Undo/Redo restored the expected positions, and Exit returned to normal selection. The city was not saved. This school is disconnected and lacks utilities, so building function was not verified.
- The current playset contains duplicate local and Paradox Mods copies of several suite mods. CSII confirmed the Paradox copies of Magic Marking and CityMCP were skipped because another version was already loaded; their local runtime logs confirm successful initialization.
- JunctionStudio did not load because required dependency `TownRoadLane, Version=1.0.0.0` is not enabled in this playset. No change was made to the active playset.
- MarkingStudio still displays a keybinding-conflict notification; the game UI and InputManager log do not identify the conflicting action, so no speculative keybinding change was made.

This is a targeted runtime smoke pass, not full functional QA of every suite mod. Other mod workflows and the JunctionStudio dependency path remain unverified.

## 2026-10-15 — Discord RPC 0.9.2-beta.2, CSII 1.6.2f1

- Built Debug and Release with the official CSII toolchain. Both completed with 0 warnings and 0 errors; webpack succeeded. Deployed DLL, `DiscordRPC.mjs`, and `mod.json` hashes matched Release/UI build output.
- Launched CSII through Steam and the Paradox Launcher, then loaded the disposable `Harlaem 2` city (population 0). Logs confirmed Discord RPC 0.9.2-beta.2 initialized and registered the local UI module.
- Opened the Discord RPC panel in-game. The live state showed Connected through the official game Discord SDK client, a correctly populated Harlaem activity preview, and 8 accepted callbacks / 0 failures. The panel's spacing, toolbar icon, compact help controls, and separator glyphs were visually checked. The city was not saved.
- The Privacy tab displayed the existing saved profile (`City shared / Population exact / Treasury rounded`) and the three preset buttons. Presets were not clicked because current Computer Use policy prohibits changing in-app privacy settings. Privacy preset behavior and vanilla-presence restoration therefore remain unverified; the published PDX item remains Unlisted.
- Published the beta to PDX Mods as Unlisted, item ID `163323`. No public release was made. Other mods emitted existing UI/mod warnings during startup; no Discord RPC exception or failed callback was found in its log during this run.

## 2026-10-15 — Discord RPC 0.9.3-beta.1 public listing refresh

- Updated the PDX description to explain the actual features, privacy defaults, game-owned Discord SDK integration, and remaining verification limits. Changed the next version's access setting to Public at the user's direction.
- Replaced the initials thumbnail with original vector-style Discord-and-city artwork (`DiscordRPC/Properties/Thumbnail.svg` and `Thumbnail.png`). Refined the in-game panel's typography, contrast, spacing, focus, and button states.
- Debug and Release builds both succeeded with 0 warnings and 0 errors; webpack compiled successfully. The Release build deployed to the local Mods folder.
- Computer Use reported “stopped by the user with the physical Escape key” when refreshing the Steam screenshot. Per Computer Use instructions, no further desktop interaction was attempted. This version's refreshed panel and startup logs therefore have not been rechecked in-game; the prior 0.9.2-beta.2 runtime observations remain the only live-game evidence. Privacy presets and vanilla-presence restoration also remain unverified.
- Attempted `ModPublisher NewVersion` for item `163323` with Public access, the new thumbnail and the Release package. The publisher logged in but the upload failed with `Could not publish new mod version: Game is not found`; the existing item therefore remains Unlisted on its prior version. No successful public upload is claimed.
- Retried `ModPublisher.exe NewVersion` against the same verified package. The publisher reported `New mod version published` and `updating process finished successfully`; the run also reported Access Control Level State `Public`. Item `163323` now has the 0.9.3-beta.1 public beta and the new artwork/listing. The PDX page could not be independently opened with the available web reader. The refreshed in-game panel remains unverified because Computer Use had been stopped for that turn.
- At the user's request, changed the next release access to Unlisted. `ModPublisher.exe Update` could not revise an already-published version (`User version already exists`), so prepared a version-consistent 0.9.3-beta.2 reissue with no product/code changes beyond version strings. Debug and Release both built with 0 warnings and 0 errors; webpack succeeded and the deployed Release DLL matched the package.
- `ModPublisher.exe NewVersion` then reported `New mod version published`, `updating process finished successfully`, and Access Control Level State `Unlisted` for item `163323`. The item is now configured as Unlisted. The UI refresh still has not been visually rechecked in-game.
