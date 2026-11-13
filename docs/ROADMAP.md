# Roadmap

## M0 — scaffold (current)

- [x] Avalonia shell with the Hollowwork visual language
- [x] Archive detection (`GGPK` magic, `_.index.bin` name)
- [x] Patch model, loader and validation
- [x] Three sample patches
- [x] Unit tests and CI on Linux + Windows
- [ ] Open archive read-only and populate the tree

## M1 — patch engine

- [ ] `ArchiveSession` over LibGGPK3 (GGPK first)
- [ ] Target resolution and dry-run plan
- [ ] `replace`, `replaceExtension`, `setBool`
- [ ] Backup with SHA-256 manifest and restore
- [ ] Apply report and progress
- [ ] Path protection

## M2 — browser

- [ ] Text preview (AvalonEdit)
- [ ] DDS preview (Pfim)
- [ ] Export file / export directory
- [ ] Replace file
- [ ] Profiles per game

## M3 — bundles and polish

- [ ] `_.index.bin` and bundle reading
- [ ] Oodle via the game-provided library (never redistributed)
- [ ] Minimap colours and camera tweaks
- [ ] Portable builds for Linux and Windows

## Out of scope

- Process injection, memory reading, hooks
- Gameplay automation
- Accounts, licensing, telemetry
