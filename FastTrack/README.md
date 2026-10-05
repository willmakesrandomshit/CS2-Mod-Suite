# FastTrack

Adaptive performance detail and diagnostic monitoring. The first optimizer is
opt-in camera-aware LOD: it reduces the game's native LOD distance at aerial
views, restores full detail at close views, and never goes below the stock Low
LOD value or a lower value already set by the player. It does not change
simulation logic or save data. Performance gains and visual impact still need
in-game benchmarking.

- **Source status:** 1.3.5-beta.2; adaptive LOD implementation build-verified, in-game visual/performance validation pending
- **PDX:** [mod 155229](https://mods.paradoxplaza.com/mods/155229/Windows)
- **Licence:** MIT
- **Build:** follow [BUILDING.md](../BUILDING.md); C# is in the project root and React UI source is in `UI/src`
- **Important limits:** adaptive LOD trades distant detail for rendering work; it is off by default and is not a guaranteed FPS boost. Loading tuning is off by default. Unavailable counters remain unavailable; main-thread time does not identify a cause
- **Issues:** include the exact scene, enable/pause/settings state, comparison without FastTrack, frame evidence and redacted logs
