# Modular Growables

An experimental Cities: Skylines II mod that combines two compatible vanilla
low-density residential building prefabs into a wider growable variant. It
reuses the game's building meshes and clones the base prefab so its existing
simulation components are retained.

The current build registers one automatically selected duplex variant. It does
not yet provide a player-facing builder or let players choose modules. Its
source pair and visual composition are chosen from built-in prefabs at runtime.

This is a local technical preview, not a release candidate. A disposable-city
session confirmed that the current build loads and registers the generated
prefab. The building has not yet been observed spawning from zoning, and its
rendering, service behavior, and save/reload behavior remain unverified. It
contains no third-party or AI-generated building assets.

See [docs/qa.md](docs/qa.md) for the evidence record and remaining checks.
