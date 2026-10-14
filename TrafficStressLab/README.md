# Traffic Stress Lab

Amplifies approved random road departures for disposable city stress testing.
The native spawner creates vehicles on existing approved routes. 25x adds up to
24 extra departures per route; a shared batch cap bounds the extra work.

- **Source status:** 1.5.7-beta.1; approved road departure amplification; runtime comparison pending
- **PDX:** [mod 155230](https://mods.paradoxplaza.com/mods/155230/Windows)
- **Licence:** MIT
- **Build:** follow [BUILDING.md](../BUILDING.md); assembly/project name remains `TrafficStressTester`
- **Important limits:** follows native random traffic routes; other streets may stay quiet; Stop ends extra departures while existing vehicles finish; batch caps can reduce the effective rate
- **Issues:** report preset, ramp/cap, road vehicles, approved route telemetry, simulation speed and drain behaviour after Stop
- **Evidence:** [14 October QA record](../docs/traffic-stress-qa-2026-10-14.md)
