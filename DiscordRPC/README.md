# Discord RPC

Prototype configurable Discord Rich Presence using the Discord client already
owned by Cities: Skylines II.

- **Status:** 0.9.0-beta.1 development-only; visible client acceptance has not been verified for release
- **Licence:** MIT
- **Build:** follow [BUILDING.md](../BUILDING.md); Discord SDK assemblies come from the game and are not bundled
- **Privacy:** city name sharing and treasury are off by default; inspect behaviour before enabling more detail
- **Important limits:** the numeric client ID in source is the game's public application identifier, not a token; no credentials are included
- **Issues:** include platform, Discord client state, privacy settings, preview/connection state and redacted logs
