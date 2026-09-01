# Traffic Stress Lab

Submits additional bounded native random-road-traffic requests for disposable
city stress testing. Request counts are not guaranteed visible vehicles.

- **Source status:** 1.5.5-beta.1; high multipliers remain experimental
- **PDX:** [mod 155230](https://mods.paradoxplaza.com/mods/155230/Windows)
- **Licence:** MIT
- **Build:** follow [BUILDING.md](../BUILDING.md); assembly/project name remains `TrafficStressTester`
- **Important limits:** Stop cancels queued test clones on a simulation tick; already dispatched vehicles continue; vanilla dispatch/pathfinding controls outcomes
- **Issues:** use a test city and report multiplier, duration, request telemetry, simulation speed and drain behaviour after Stop
