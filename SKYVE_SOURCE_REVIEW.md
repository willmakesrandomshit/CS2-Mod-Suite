# Skyve source-review material

Prepared: 2026-09-01

Repository: https://github.com/willmakesrandomshit/CS2-Mod-Suite

This document is evidence for a legitimate compatibility review. Publishing
source does not automatically clear a Skyve classification, and this document
does not claim that it should.

## Road Rules

- **Paradox Mods:** [155436](https://mods.paradoxplaza.com/mods/155436/Windows)
- **Last locally verified public baseline:** 1.4.4
- **Source candidate in this repository:** 1.4.5-beta.1
- **Source folder:** https://github.com/willmakesrandomshit/CS2-Mod-Suite/tree/main/RoadRules
- **Verification status:** builds cleanly; runtime matrix partially verified

Changes represented by the current source candidate include:

- physical lanes are grouped by cross-section/lateral position rather than by
  carriageway master identity alone;
- sibling lanes are no longer intentionally collapsed into one lane group;
- helper and connector members are assigned to a physical-lane group instead
  of becoming editable phantom lanes;
- overlays use the selected physical lane's curve;
- lane reopen/reset restores the captured original access and speed state;
- unsupported presets fail closed instead of presenting fake enforcement;
- Advanced Debug exposes grouping/member evidence;
- repeated inspection/log churn and teardown behaviour were tightened.

Known limitations are not hidden:

- the exact one/two/three/four-lane, ramp, bridge, asymmetric and
  merge/diverge runtime matrix is not complete;
- Public/Service Lane is not a strict bus-only guarantee;
- No Heavy Traffic and Local Bias are soft routing preferences;
- vehicles with an existing path can remain temporarily after a rule changes;
- a source build passing is not proof that every vehicle class obeys every
  restriction in a live city.

The useful review request is: inspect the source, reproduce against the current
public PDX package and attach a redacted Skyve/log report with the selected road
prefab, visible lane count, Advanced Debug members and newly routed vehicle
behaviour. Do not request removal of the warning based on version metadata or
this document alone.

## FastTrack

- **Paradox Mods:** [155229](https://mods.paradoxplaza.com/mods/155229/Windows)
- **Last locally verified public baseline:** 1.3.4
- **Source candidate in this repository:** 1.3.5-beta.1
- **Source folder:** https://github.com/willmakesrandomshit/CS2-Mod-Suite/tree/main/FastTrack
- **Verification status:** builds cleanly; extended in-game regression pending

The current source does not intentionally mutate render scale, LOD, shadows,
terrain, effects or culling. Performance readings are diagnostic and the
loading-budget experiment is opt-in and off by default. It is not advertised as
a guaranteed FPS boost. A legitimate review should still compare a current PDX
download in representative gameplay/editor scenes and include evidence if GPU
spikes or visual corruption reproduce.

## How to request review

Skyve exposes a **Request a review** action on a mod's Compatibility page. Use
**Report an issue** for a reproducible problem, or **Add missing info** for
missing/incorrect compatibility details. Include the PDX ID, exact package
version, source link, minimal playset, reproduction steps and redacted evidence.
The Skyve project also maintains a public GitHub repository, but its issue
tracker currently restricts new issue creation; do not spam maintainers or use
unrelated issues as a workaround.
