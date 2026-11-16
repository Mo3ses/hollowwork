# Patch parity — Hollowwork vs ExileForge

Static comparison of Hollowwork's JSON patches against the ExileForge RE dump
(`/home/mo3ses/Downloads/ExileForge-RE`, `src-annotated`).

- Hollowwork: `patches/poe1/*.json` (10 files), `patches/poe2/` (0 files).
- ExileForge: 73 `.cs` files under `Talagio90.Patches` and `Talagio90.Patches2`,
  including base classes and interfaces. The actual patch count is lower.
- Scope: **PoE1 only** in this document. PoE2 is listed as a coverage gap.
- No runtime diff was performed. File sets below are derived from roots,
  extensions, explicit path lists and decompiled conditions.

## Legend

| Column | Meaning |
|---|---|
| **Same files?** | `yes` = same target set as far as the static data shows; `partial` = overlap, but one side is broader/narrower; `no` = different target |
| **Same edit?** | `same` = same semantic change; `near` = same intent with different anchors/coverage; `subset` = one implementation changes less; `different` = different operation |
| **Confidence** | `observed` = strings/paths read directly; `inferred` = derived from decompiled control flow; `unknown` = obfuscated or not recoverable |

Root+extension matching is not equivalent to an explicit path list. Hollowwork
plans every file under a root with a matching extension; ExileForge often uses
curated lists, exclusions and per-file conditions. A `partial` result can still
mean a large file-count difference.

## Matrix

| Patch | Hollowwork target | Hollowwork operation | ExileForge component | ExileForge target / operation | Same files? | Same edit? | Confidence | Notes |
|---|---|---|---|---|---|---|---|---|
| **Camera zoom** | `metadata/characters`, `.ot` | `replaceRegex` on `CreateCameraZoomNode(5000.0, 5000.0, ...)` with `{zoom}`; inject `team = 1` hook | `Camera` | Curated `.ot` list (includes `character.ot` and many camera/zoom nodes); same engine strings; `ZoomLevel` parameter | partial | near | observed | Ours is root-wide and may not cover the many camera nodes outside `metadata/characters`; theirs is path-list based |
| **Shadows off** | `metadata/environmentsettings`, `.env` | `setBool shadows_enabled=false` | `Shadow` (`BaseEnvPatch`) | Same `.env` root; replace `"shadows_enabled": true` → `false` | yes | same | observed | Closest match in the set |
| **No loading screen** | `metadata/ui/loadingstate`, `.ui` | Insert `visible=false`/`mouse_enabled=false` after `begin Frame`, `Picture`, `Label` | `NoLoadingScreenPatch` | Same root; inserts `visible = false;` and `mouse_enabled = false;` into loading UI text | yes | near | observed | Same intent; anchor coverage may differ |
| **Minimap colors** | `shaders/*.hlsl` | Three `replaceRegex` for `walkable_color`, `walkability_map_color`, unexplored lerp; colors `{explored}`, `{edge}`, `{unexplored}` | `Minimap` | Explicit `shaders/minimap_visibility_pixel.hlsl` and `minimap_blending_pixel.hlsl`; same regex family; color settings + reveal threshold | partial | near | observed | Ours scans all shaders; theirs targets two files and exposes more settings |
| **Map revealed** | `shaders/*.hlsl` | Replace exact red `res_color` line and add `res_color = max(res_color, 1.0);` | `Minimap` (reveal part) | `res_color = max(res_color, RevealThreshold);` on the minimap visibility shader | partial | near | observed | Ours is a separate patch, hardcoded 1.0, and scans every shader |
| **Clean terrain** | `metadata/terrain/doodads`, `.ao`; four excludes | 8 `stripBlock` operations | `CleanTerrain` | `.ao` + `.aoc`; roots `metadata/effects/misc`, `metadata/terrain`, `metadata/terrain/doodads`; curated block list | partial | subset | observed / unknown blocks | Ours is narrower on roots and extensions; block list is larger in ExileForge |
| **Delirium off** | `metadata/effects/environment/league_affliction`, `metadata/miscellaneousobjects/affliction`, `.ao` | 8 `stripBlock` operations | `Delirium` | Same two roots; `.ao` + `.aoc`; curated block list | partial | subset | observed / unknown blocks | Missing `.aoc`; block list may differ |
| **Remove MTX** | `metadata/effects/microtransactions`, `.ao` | 7 `stripBlock` operations | `RemoveMTX` | 21 roots, including items/armours/quivers/weapons and many microtransaction subfolders; `.ao` + `.aoc` | partial | subset | observed / unknown blocks | Ours only touches the microtransactions root; theirs is much broader |
| **Particles off** | `metadata/particles/enviro_effects`, `metadata/effects/environment`, `.pet` | `setText "0"` | `Particles` | `.pet` + `.trl`; roots `metadata/effects/environment`, `metadata/particles`, Delirium/spirit/tormented paths; large keep/zero lists | partial | subset / different | observed / unknown lists | Ours is a blanket empty stub on two roots; theirs has curated exclusions |
| **Fog off** | `metadata/environmentsettings`, `.env` | Replace quoted `"fog"` key with `"xog"` | `Fog` (`BaseEnvPatch`) | Same `.env` root; observed `ReplaceMap` `"audio"` → `"xaudio"` and `ShouldSkip` requires `"audio"` | partial | different | observed / intent unclear | The decompiled class does not show a `"fog"` replacement; verify against a real `.env` before changing ours |
| **PoE2 patch set** | none | none | `CleanTerrainPoe2`, `DeliriumPoe2`, `RemoveMTXPOE2NEW`, `Black_Screen_Poe2`, etc. | Multiple PoE2 classes | no | — | observed | Largest coverage gap |

## ExileForge patches not represented in Hollowwork

Not exhaustive; these are the classes visible in the dump that have no direct
Hollowwork JSON counterpart:

- `BlackScreen`, `Black_Screen_Poe2`
- `BossLifeBar`, `ShowMonsterLife`, `MonsterRarityRadar`
- `NoMonsters`, `NoMonstersWithHPBar`, `NoMonstersWithHPBarPoe2`
- `CustomPathIcon`, `CustomPathEffect`
- `ColorMods`, `DynamicColorModsPoe1`, `ColorModsPoe2`
- `AudioFolderPatch`, `RemoveSound`
- `AtlasFog`, `Rain`, `Clouds`
- `MapBeamColor`, `MapBeamColors`, `MapBeamEffect`
- `ReduceTextureResolution`
- `NoPetPatch`, `Corpse`, `CorpsePoe2`, `RemoveBloom`
- `RemoveSkillEffect`, `RemoveMonsterEffect`, `RemoveCharacterEffectPoe2`

Some are variants/helpers; the list is a coverage map, not a promise that each
should be ported.

## What is not proven

- Exact file sets require running both planners against the same archive and
  diffing the resulting paths. The static roots/lists only approximate this.
- ExileForge block lists are obfuscated; `stripBlock` coverage is inferred from
  the strings and calls that remain readable.
- The `Fog` discrepancy may be a decompiler/name artifact. It is called out as
  a risk, not as a definitive statement of ExileForge's user-visible behavior.
- No runtime comparison was performed.

## Backlog derived from the matrix

**P0 — correctness before breadth**

1. **Run a file-set diff** on the real Bundles2 index: Hollowwork planner vs
   ExileForge path lists, per patch. This settles `partial` rows.
2. **Camera audit**: compare our `metadata/characters` root against the curated
   `.ot` list; add missing camera nodes or narrow the target.
3. **Minimap/map reveal**: restrict to the two minimap shaders and parameterize
   the reveal threshold; stop scanning every `shaders/*.hlsl` for a single line.
4. **Fog decision**: validate the quoted `"fog"` key against the real `.env`
   and document why it differs from ExileForge's `Fog`.

**P1 — close the PoE1 coverage gap**

5. Clean terrain: add `.aoc`, `metadata/effects/misc`, `metadata/terrain`, and
   align the block/exclusion lists.
6. Delirium: add `.aoc` and align the block list.
7. Remove MTX: expand roots carefully; avoid stripping legitimate item art.
8. Particles: add `.trl` and the missing roots, keeping chests/stash/crafting
   exclusions.
9. No loading: compare the insertion anchors so both hide the same widgets.

**P2 — new surface**

10. PoE2 patch set for every PoE1 patch that has a PoE2 counterpart.
11. Missing feature classes: black screen, boss life bars, no monsters, path
    icons/effects, audio removal, color mods, rain/clouds, texture reduction.
12. Safety tooling that is not parity: dry-run **asset-path lint** (the
    `hellfog` → `hellxog` class of bug), per-file diff, and patch profiles.
