# Discord RPC

Prototype configurable Discord Rich Presence using the Discord client already
owned by Cities: Skylines II.

- **Status:** 0.9.1-beta.1 development-only; visible Discord client acceptance has not been verified for release
- **Licence:** MIT
- **Build:** follow [BUILDING.md](../BUILDING.md); Discord SDK assemblies come from the game and are not bundled
- **Privacy:** city name sharing, treasury and companion activity are off by default. Companion activity may include text supplied by another mod; enable it only for mods you trust.
- **Important limits:** the numeric client ID in source is the game's public application identifier, not a token; no credentials are included
- **Issues:** include platform, Discord client state, privacy settings, preview/connection state and redacted logs

## Release gate

The 0.9.1 beta source, UI bundle, and both Debug and Release assemblies build with zero warnings and errors. This is an offline candidate; its revised panel and connection handling have not been exercised in a running game.

Before any public listing, use a disposable city and a real Discord desktop client to verify: accepted activity appears in Discord, privacy presets hide the promised fields, disabling enhanced presence restores vanilla activity, reconnect works, and dismissing the optional disconnect panel does not reopen it until a new failure. Check `DiscordRPC.Mod.log` for failed callbacks and repeated errors. Confirm the packaged DLL and UI bundle match the tested build.
