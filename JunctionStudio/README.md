# Junction Studio

A workflow extension for Town Road Lane that copies selected junction marking
data, applies compatible presets and stores named presets outside the save.

- **Source status:** 1.1.4-beta.1; copy/paste, redraw and topology coverage remain incomplete
- **PDX:** [mod 155242](https://mods.paradoxplaza.com/mods/155242/Windows)
- **Licence:** GPL-3.0; see [CREDITS.md](CREDITS.md)
- **Dependency:** Town Road Lane by mxerf is required but its DLL is not bundled
- **Build:** follow [BUILDING.md](../BUILDING.md) and provide `TownRoadLanePath` when it is not found in the PDX cache
- **Important limits:** approach counts must match; invalid presets are rejected; rollback is attempted but not guaranteed for every topology
- **Issues:** report both junctions, approach count, Town Road Lane version, preset operation and redraw/save-load result
