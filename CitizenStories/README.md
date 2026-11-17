# Citizen Stories

Select a citizen and a card shows a short story built from the game's own data about them: age group, education, job or school, commute time, household size, rent, health, mood and what they are doing right now.

- **Source status:** 0.1.0-alpha.1. **Development only: not published.** Builds with the official toolchain and the story logic has offline tests (`scripts/CitizenStoriesTests`), but it has **not been run in the game**.
- **Read-only:** it only reads the selected entity and never writes to the simulation or saves.
- **Names** are generated from the citizen's stable pseudo-random seed; the game's citizens have no real names.
- **Not yet built:** follow-cam, clip recording, share cards, workplace/home building names. Whether the game exposes enough data for those is unverified.
- **Licence:** MIT
- **Build:** follow [BUILDING.md](../BUILDING.md).
