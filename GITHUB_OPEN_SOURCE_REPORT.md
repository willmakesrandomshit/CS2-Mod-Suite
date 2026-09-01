# GitHub open-source report

Prepared: 2026-09-01

## Repository

- **GitHub repository:** https://github.com/willmakesrandomshit/CS2-Mod-Suite
- **Intended visibility:** Public
- **Default branch:** `main`

## Projects included

FastTrack, RoadRules, SaveGuard, TrafficStressLab, AccessStudio, CityPulse,
ContourPlus, CrashLens, JunctionStudio, EventEngine, MagicMarking,
NetworkStudio, Parkify and DiscordRPC.

The first 11 are existing public-suite source projects. NetworkStudio, Parkify
and DiscordRPC are included for transparent development but are labelled
development-only; their presence here is not a public PDX release claim.

## Projects excluded

Old experiments, duplicate source trees, retired prototypes, decompiled source,
downloaded PDX packages, release staging, local saves, logs, crash dumps,
backups, generated outputs, personal screenshots and local publishing/auth data.

## Security

- **Secret scan:** PASS before Git initialisation
- **Gitleaks:** 0 findings
- **Personal path cleanup:** absolute local screenshot paths removed from
  publishing metadata; project/toolchain paths are configurable
- **Secrets found:** none
- **Personal or secret data publicly exposed at any point:** no; the repository
  had not yet been pushed when this report entry was written

## Third-party and licence review

Complete. The repository is a mixed-license aggregate: MIT for independently
authored suite folders, GPL-3.0 for MagicMarking and JunctionStudio. Town Road
Lane and RoadBuilder attribution is retained. Game/toolchain and third-party
dependency binaries are excluded. See `LICENSE`, `LICENSES/` and
`THIRD_PARTY_NOTICES.md`.

## Build after cleanup

- **Result:** PASS
- **Projects:** 14/14 Release builds
- **Compile errors:** 0
- **Compile warnings:** 0
- **UI builds:** PASS
- **Official post-processing/toolchain:** PASS

This is a build/reproducibility result, not a claim that all in-game runtime
matrices passed.

## Post-push and PDX status

- **Public repository verified:** pending
- **PDX source links added:** pending
- **Skyve review material prepared:** yes; see `SKYVE_SOURCE_REVIEW.md`

These pending entries must be updated only after the live repository and PDX
metadata are actually verified.
