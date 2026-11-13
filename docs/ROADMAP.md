# Roadmap

## M0 — scaffold (current)

- [x] Avalonia shell with the Hollowwork visual language
- [x] Archive detection (`GGPK` magic, `_.index.bin` name)
- [x] Patch model, loader and validation
- [x] Three sample patches
- [x] Unit tests and CI on Linux + Windows
- [ ] Open archive read-only and populate the tree

## M1 — patch engine (engine complete)

- [x] `ArchiveSession` over LibGGPK3 (GGPK first)
- [x] Target resolution and dry-run plan
- [x] `replace`, `replaceExtension`, `setBool`
- [x] Backup with SHA-256 manifest and restore
- [x] Apply report (progress UI pending)
- [x] Path protection
- [ ] UI wiring (open archive, plan preview, apply button)

## M2 — browser

- [ ] Text preview (AvalonEdit)
- [ ] DDS preview (Pfim)
- [ ] Export file / export directory
- [ ] Replace file
- [ ] Profiles per game

## M3 — bundles and polish

- [x] `_.index.bin` read and write (`BundleArchiveSession`)
- [x] Oodle resolver for a user-provided library (never redistributed)
- [x] Read validated on a real PoE 1 `Bundles2` install
- [x] Write validated on a copied index + bundle
- [ ] Minimap colours and camera tweaks
- [ ] Portable builds for Linux and Windows

## Out of scope

- Process injection, memory reading, hooks
- Gameplay automation
- Accounts, licensing, telemetry
