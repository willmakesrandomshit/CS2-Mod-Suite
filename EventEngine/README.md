# Event Engine

Discovers venue candidates and schedules bounded native citizen visit requests
around a selected game-clock time.

- **Source status:** 0.2.1-beta.1; revised full lifecycle still needs runtime verification
- **PDX:** [mod 155652](https://mods.paradoxplaza.com/mods/155652/Windows)
- **Licence:** MIT
- **Build:** follow [BUILDING.md](../BUILDING.md); scheduling logic is in `EventSchedule.cs` and React UI source is in `UI/src`
- **Important limits:** attendance and traffic depend on vanilla; capacity/mode shares are estimates; cancelling cannot recall journeys vanilla already consumed; managed events clear on city exit
- **Issues:** include venue, scheduled game date/time, duration, requested attendance and lifecycle transitions observed
