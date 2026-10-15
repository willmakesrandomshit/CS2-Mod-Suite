# Discord RPC

Show what you are doing in Cities: Skylines II on Discord, with a live activity preview and clear controls for what you share.

- **Status:** 0.9.3-beta.2 Unlisted beta, published to PDX Mods
- **PDX:** [Discord RPC on Paradox Mods, item 163323](https://mods.paradoxplaza.com/mods/163323/Windows)
- **Licence:** MIT
- **Build:** follow [BUILDING.md](../BUILDING.md); Discord SDK assemblies come from the game and are not bundled
- **Privacy:** city name sharing, treasury and companion activity start off; population starts rounded. Companion activity may contain text from another mod, so only enable integrations you trust.
- **Limit:** CSII and the Discord desktop client must be running for live activity. This mod reuses CSII's official Discord Game SDK client and does not create a second connection.

## Verified so far

Version 0.9.2-beta.2 built with zero warnings or errors and loaded in CSII 1.6.2f1. It connected through the game's Discord SDK and produced accepted callbacks in the disposable Harlaem 2 city with zero failures. The panel was visually checked in-game. Version 0.9.3-beta.2 and its artwork are published as Unlisted; its panel refresh has build verification but still needs its own in-game visual check.

Privacy preset actions and vanilla-presence restoration remain unverified. Before calling the public beta fully verified, test privacy presets, disabling enhanced presence, reconnect, and the dismissed failure panel in a disposable city with Discord desktop. Check the actual Discord client and inspect the DiscordRPC log for failed callbacks and repeated errors.

## Reporting issues

Include the game version, whether Discord desktop is running, the shown connection state, selected privacy options, and relevant redacted log lines. Do not include save paths, account information, or tokens.
