# FastTrack

Read-only performance monitoring with an optional experimental loading-budget
tuning mode. It does not lower render scale, LOD, shadows, terrain or effects,
and it is not a guaranteed FPS boost.

- **Source status:** 1.3.5-beta.1; extended runtime regression pending
- **PDX:** [mod 155229](https://mods.paradoxplaza.com/mods/155229/Windows)
- **Licence:** MIT
- **Build:** follow [BUILDING.md](../BUILDING.md); C# is in the project root and React UI source is in `UI/src`
- **Important limits:** unavailable counters remain unavailable; main-thread time does not identify a cause; loading tuning is off by default
- **Issues:** include the exact scene, enable/pause/settings state, comparison without FastTrack, frame evidence and redacted logs
