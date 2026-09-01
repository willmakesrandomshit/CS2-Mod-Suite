# Magic Marking — Snap & Paint Road Markings

A visual road-marking editor with curved lines, per-segment controls, manual
polygon fills and a paint-bucket workflow for enclosed marking cells.

- **Source status:** 2.4.1-beta.1; redraw, fill persistence and compatibility need broader runtime coverage
- **PDX:** [mod 155247](https://mods.paradoxplaza.com/mods/155247/Windows)
- **Licence:** GPL-3.0 modified fork; see [CREDITS.md](CREDITS.md) and [LICENSE](LICENSE)
- **Build:** follow [BUILDING.md](../BUILDING.md); C# is under `src/MarkingStudio` and React UI source is under `src/MarkingStudioUI`
- **Important limits:** visual only; it does not alter traffic routing; do not enable it alongside Town Road Lane because both replace the native secondary-lane generator
- **Issues:** include selected network/junction, tool and style, expected geometry, save/load or topology-edit steps and a screenshot
