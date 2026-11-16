# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] — 2026-11-16

First public build.

### Added

- **Archive access.** Opens `Content.ggpk` and Bundles2 (`_.index.bin`) on Linux and Windows, with
  a user-provided Oodle library. Tree explorer with text and DDS preview.
- **Patch engine.** Declarative JSON patches with seven operation types, dry-run planning, path
  protection, hash-verified backup and restore, explicit file allowlists and per-target
  operations.
- **11 PoE1 patches.** Camera zoom, map revealed, minimap colors, clean terrain, Delirium off,
  fog off, no loading screen, particles off, performance (15 shaders), remove MTX effects and
  shadows off.
- **FLIP tab.** Read-only currency screener over the public hourly aggregate, with LIQUID,
  BALANCED and AGGRESSIVE presets, category filters, an in-app guide and a local response cache.
- **AUTOMATION tab.** PoE1 auto-flask on Linux/Proton: reads HP/ES/MP from the game process with
  `process_vm_readv` and sends the configured keys through X11 XTest. Opt-in, focus-guarded, and
  separate from the patch engine.

### Security

- Every write is preceded by a full, SHA-256-verified copy of the archive.
- The patch engine never injects into, hooks or reads the game process.
- FLIP and AUTOMATION only run when the tab is open and you press the action button.

[0.1.0]: https://github.com/Mo3ses/hollowwork/releases/tag/v0.1.0
