# QA Record

## 22 October 2026 — local build and disposable-city smoke test

- Game version observed: Cities: Skylines II `1.6.2f1 (767.21d1) [6300.26419]`.
- Debug and Release builds completed with zero warnings and zero errors; the
  official mod post-processor succeeded.
- Release source DLL and deployed DLL SHA-256 matched:
  `35CBCBB1873A3CD30BA0ECC0ABE3576B3E04349D150BC2BADE5246ED239143DC`.
- Loaded the disposable `Harlaemlotstudioqa` city. `Modding.log` confirms the
  deployed `GrowableModulesPrototype.dll` loaded; the mod log records `OnLoad`
  and the exact deployed path.
- Runtime registration succeeded for `Modular Growables Duplex 01` using
  `EU_ResidentialLow01_L1_2x2` + `EU_ResidentialLow01_L2_2x2`, zone `EU
  Residential Low`, footprint `4x2`, and two composed meshes.
- `Modding.log` and `SceneFlow.log` contained no Growable Modules errors or
  exceptions in the inspected entries. `GrowableModulesPrototype.Mod.log`
  contained no error or exception entries.
- No buildings or roads were placed and the disposable city was not saved.
- A single-cell paint attempt produced no unambiguous visible residential zone;
  it is not counted as a successful zoning test. No further map edits were made.

### Evidence limits

- **Verified:** current DLL deployed and loaded; generated prefab registration
  completed in a live city.
- **Not verified:** normal zoned spawning, appearance/rendering, placement
  orientation, simulation/service behavior, functionality after city save and
  reload, and reset/recovery. The open test city is an empty diagnostic save
  without a functioning residential district, and a residential zone could not
  be established clearly in this pass. The visible building picker is not
  evidence that zoning can spawn this generated prefab.
- Do not describe the prototype as playable or release-ready until those
  runtime checks pass in a disposable city with suitable residential zoning.

## Earlier local feasibility build

- Reflected the installed `Game.dll`: `PrefabSystem.DuplicatePrefab`,
  `UpdatePrefab`, and `RemovePrefab` are public; `BuildingPrefab` exposes lot
  dimensions and mesh geometry; `ObjectMeshInfo` holds a mesh and local
  transform; `ZoneUtils.CELL_SIZE` is 8 metres.
- An earlier Debug/Release build and deployment used hash
  `061629D54548A21CF0E35F43ECA8AC748C2C821E5EE99988BCC2EFB7F5AF0164`.
  This hash is superseded by the current-session hash above.

## 22 October 2026 — follow-up after centered-mesh build

- Rebuilt the current source in Debug and Release; both builds completed with 0 warnings and 0 errors, including the game mod post-processor.
- Release and deployed DLL SHA-256 both match: `AFF6892483B0A970CDAAD12A90688F0798F58B464B1608649A095070257B043F`.
- In a clean Steam launch, the game loaded the deployed mod and registered `Modular Growables Duplex 01` from the two logged vanilla low-density residential sources with a `4x2` footprint and two meshes.
- The mod log has no error or exception entries; inspected Modding/SceneFlow entries show the mod and no attributed exception.
- Loaded the disposable `growablesqa` city. The developer/object browser did not open with Home or Tab, so no generated instance could be rendered or inspected. No city edits were made or saved.
- **Verification status: partial.** Build, deployment, load, and prefab registration are verified. Spawn, visual alignment/orientation, and gameplay behavior remain unverified; this is not a playable-prototype pass.
