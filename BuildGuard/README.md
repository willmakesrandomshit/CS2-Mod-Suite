# BuildGuard

Shows what an edit will remove or change **before you commit to it**, using the game's own edit preview.

- **Source status:** 0.1.0-alpha.1. **Development only: not published.** Builds with the official toolchain; the summary logic has offline tests (`scripts/BuildGuardTests`). **Not run in the game.**
- **How it works (verified in the game's assemblies, not yet at runtime):** the vanilla edit tools create `Temp` preview entities carrying `TempFlags` (Create, Delete, Replace, Upgrade, Modify) and a cost. BuildGuard reads those and counts buildings removed/replaced, road segments created/removed/changed, intersections changed and net cost.
- **Read-only:** it never creates, edits or deletes an entity, and does not block or confirm the edit. Uninstalling leaves nothing behind.
- **Not examined yet:** zoning, pedestrian paths, service connections, other mods' data (Magic Marking, Road Rules, ...). The panel says so and never claims anything is "safe".
- **Unknown until tested in-game:** whether upgrade/replace operations always produce these Temp entities, whether building removal shows up as Delete in every tool, and whether the panel position is usable.
- **Not built (Release 2+):** preservation and re-application of customizations; a guarded confirm step.
- **Licence:** MIT
