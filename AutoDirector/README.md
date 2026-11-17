# Auto Director

One button on the top-left bar. The camera picks a moving car, follows and orbits it for 6-12 seconds with a randomly planned shot (follow, slow orbit, fast orbit, push-in), then cuts to another. Made for filming your city.

- **Source status:** 0.1.0-alpha.1. **Development only: not published.** It builds with the official toolchain and the shot-planning logic has offline tests (`scripts/AutoDirectorTests`), but it has **not been run in the game**.
- **Moves the camera only.** It never writes to any entity, so it cannot change the simulation or saves.
- **Unverified:** that the camera controller's rotation (pitch/yaw) and zoom values behave as assumed, how it interacts with the game's own camera input, and whether the shots look good. Click the button again to stop.
- **Not built yet:** UI hiding, citizen/building shots, a 9:16 safe-frame guide, recording.
- **Licence:** MIT
- **Build:** follow [BUILDING.md](../BUILDING.md).
