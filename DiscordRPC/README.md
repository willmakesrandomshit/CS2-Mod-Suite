# Discord RPC

Prototype configurable Discord Rich Presence using the Discord client already
owned by Cities: Skylines II.

- **Status:** 0.9.2-beta.2 unlisted beta candidate; Discord client acceptance is verified
- **PDX:** [Unlisted beta, mod 163323](https://mods.paradoxplaza.com/mods/163323/Windows)
- **Licence:** MIT
- **Build:** follow [BUILDING.md](../BUILDING.md); Discord SDK assemblies come from the game and are not bundled
- **Privacy:** city name sharing, treasury and companion activity are off by default. Companion activity may include text supplied by another mod; enable it only for mods you trust.
- **Important limits:** the numeric client ID in source is the game's public application identifier, not a token; no credentials are included
- **Issues:** include platform, Discord client state, privacy settings, preview/connection state and redacted logs

## Release gate

Debug and Release builds and the UI bundle complete with zero warnings or errors. Version 0.9.2-beta.2 loaded in CSII 1.6.2f1, connected through the game Discord SDK, and produced accepted callbacks in the disposable Harlaem 2 city with zero failures. The panel was visually checked in-game; privacy preset actions remain unverified. This beta is published as an unlisted PDX Mods item.

Before changing the item to Public, test privacy presets, disabling enhanced presence, reconnect, and the dismissed failure panel in a disposable city with Discord desktop. Privacy preset actions and vanilla presence restoration remain unverified. Confirm the package matches the tested build and check the DiscordRPC log for failed callbacks and repeated errors.
