# Building the suite

## Requirements

- Windows with Cities: Skylines II and the official modding toolchain installed
- .NET SDK 8 (the CS2 projects target the toolchain's .NET Framework 4.8 setup)
- Node.js 20 or newer and npm for UI projects
- Town Road Lane installed or built separately for Junction Studio

Set `CSII_TOOLPATH` to the directory containing `Mod.props` and `Mod.targets`.
The in-game modding toolchain normally creates this user environment variable.
You can instead pass `-p:CS2ToolPath="X:\path\to\.ModdingToolchain"` to MSBuild.

## Build everything

From the repository root:

```powershell
./scripts/build-all.ps1
```

The script runs `npm ci` where a lockfile is present and `npm install` for the
remaining development-only/beta UIs, then performs clean Release builds. The
official toolchain post-processes assemblies and normally deploys each result
to the local CS2 Mods directory. It does not publish to Paradox Mods.

## Build one project

```powershell
cd FastTrack/UI
npm ci
cd ..
dotnet restore FastTrack.csproj
dotnet clean FastTrack.csproj -c Release
dotnet build FastTrack.csproj -c Release --no-restore
```

For Junction Studio, pass `TownRoadLanePath` if the DLL is not in the usual PDX
cache location:

```powershell
dotnet build JunctionStudio/JunctionStudio.csproj -c Release `
  -p:TownRoadLanePath="X:\path\to\TownRoadLane.dll"
```

Magic Marking also supports its inherited non-Windows reference-only setup,
but the authoritative release build uses the official Windows toolchain.

## Output

Managed output is written below each project under `bin/Release/net48`. UI
bundles are written to `UI/dist` (or `MagicMarking/src/MarkingStudioUI/dist`).
Those generated paths are intentionally ignored by Git.
