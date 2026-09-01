# Third-party notices

The repository does not include Cities: Skylines II game assemblies, the
official modding toolchain or third-party dependency binaries.

| Dependency/source | Used by | Licence/status | Bundled? |
|---|---|---|---|
| Cities: Skylines II and official modding toolchain | All projects | Proprietary game/tooling; required locally | No |
| React and React DOM | UI projects | MIT | Source package lockfiles only |
| TypeScript | UI projects | Apache-2.0 | No |
| webpack and common loaders/plugins | UI projects | MIT-compatible package licences; see lockfiles | No |
| Sass/Dart Sass | UI projects | MIT | No |
| Town Road Lane by mxerf | Magic Marking; Junction Studio | GPL-3.0 | Magic Marking is a modified source fork; Junction Studio references an external installation |
| RoadBuilder `ExtendedUISystemBase` pattern by JadHajjar | Magic Marking | MIT; changes are identified in the source header | Adapted source in the GPL project |
| G87 road-marking asset packs | Magic Marking integration | External assets retain their author's terms | Asset binaries are not included |
| Discord Game SDK/CS2 Discord integration | Discord RPC | Supplied with the game | No |

Harmony and QCommon are not referenced by the projects in this snapshot.
Where present, package lockfiles record npm transitive dependency versions;
other projects currently rely on the declared `package.json` ranges. Each
fetched package retains its own licence.

