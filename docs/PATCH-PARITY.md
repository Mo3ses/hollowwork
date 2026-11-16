# Patch parity — Hollowwork vs. a reference suite

Static comparison of Hollowwork's JSON patches against an **external reference dump**
(`src-annotated`, produced outside this repository). The dump is not vendored here and its
identity is deliberately not recorded — see the naming rule in `CONTRIBUTING.md`.

- Hollowwork: `patches/poe1/*.json` (10 files), `patches/poe2/` (0 files).
- Reference dump: 73 `.cs` files across two patch namespaces, including base classes and
  interfaces. The actual patch count is lower.
- Scope: **PoE1 only**. PoE2 is listed as a coverage gap.
- No runtime diff was performed. File sets below are derived from roots, extensions, explicit
  path lists and decompiled conditions.

## Legend

| Column | Meaning |
|---|---|
| **Same files?** | `yes` = same target set as far as the static data shows; `partial` = overlap, but one side is broader or narrower; `no` = different target |
| **Same edit?** | `same` = same semantic change; `near` = same intent with different anchors or coverage; `subset` = one implementation changes less; `different` = different operation |
| **Confidence** | `observed` = strings and paths read directly; `inferred` = derived from decompiled control flow; `unknown` = obfuscated or not recoverable |

Root plus extension matching is not equivalent to an explicit path list. Hollowwork plans every
file under a root with a matching extension; the reference suite often uses curated lists,
exclusions and per-file conditions. A `partial` verdict can still mean a large file-count
difference.

## Matrix

| Patch | Hollowwork target | Hollowwork operation | Reference component | Reference target / operation | Same files? | Same edit? | Confidence | Notes |
|---|---|---|---|---|---|---|---|---|
| **Camera zoom** | `metadata/characters/character.ot` | `replaceRegex` on the single `CreateCameraZoomNode(5000.0, 5000.0, ...)` node; `team = 1` fallback guarded by `unless` | `Camera` | Curated `.ot` list covering the player node and 38 scripted camera objects; zoom level parameter | partial | different | observed | Ours changes the player's zoom only. The other 38 node-bearing files are boss intros, arenas and league set pieces; rewriting their zoom is a separate feature |
| **Shadows off** | `metadata/environmentsettings`, `.env` | `setBool shadows_enabled=false` | `Shadow` (env base class) | Same `.env` root; replace `"shadows_enabled": true` with `false` | yes | same | observed | Closest match in the set |
| **No loading screen** | `metadata/ui/loadingstate`, `.ui` | Insert `visible=false` / `mouse_enabled=false` after `begin Frame`, `Picture`, `Label` | loading-state patch | Same root; inserts `visible = false;` and `mouse_enabled = false;` into loading UI text | yes | near | observed | Same intent; anchor coverage may differ |
| **Minimap colors** | `shaders/*.hlsl` | Three `replaceRegex` for `walkable_color`, `walkability_map_color`, unexplored lerp; colors `{explored}`, `{edge}`, `{unexplored}` | `Minimap` | Explicit `shaders/minimap_visibility_pixel.hlsl` and `minimap_blending_pixel.hlsl`; same regex family; color settings plus reveal threshold | partial | near | observed | Ours scans every shader; theirs targets two files and exposes more settings |
| **Map revealed** | `shaders/*.hlsl` | Replace the exact red `res_color` line and add `res_color = max(res_color, 1.0);` | `Minimap` (reveal part) | `res_color = max(res_color, threshold);` on the minimap visibility shader | partial | near | observed | Ours is a separate patch, hardcoded to 1.0, and scans every shader |
| **Clean terrain** | `metadata/terrain/doodads`, `.ao`; four excludes | 8 `stripBlock` operations | terrain cleaner | `.ao` + `.aoc`; roots `metadata/effects/misc`, `metadata/terrain`, `metadata/terrain/doodads`; curated block list | partial | subset | observed / unknown blocks | Ours is narrower on roots; the reference block list is larger |
| **Delirium off** | `metadata/effects/environment/league_affliction`, `metadata/miscellaneousobjects/affliction`, `.ao` | 8 `stripBlock` operations | delirium patch | Same two roots; `.ao` + `.aoc`; curated block list | partial | subset | observed / unknown blocks | `.aoc` adds nothing in this build; block list may differ |
| **Remove MTX** | `metadata/effects/microtransactions`, `.ao` | 7 `stripBlock` operations | MTX remover | 21 roots, including items/armours/quivers/weapons and many microtransaction subfolders; `.ao` + `.aoc` | partial | subset | observed / unknown blocks | Ours only touches the microtransactions root; theirs is much broader |
| **Particles off** | `metadata/particles/enviro_effects`, `metadata/effects/environment`, `.pet` | `setText "0"` | particle patch | `.pet` + `.trl`; roots `metadata/effects/environment`, `metadata/particles`, Delirium/spirit/tormented paths; large keep/zero lists | partial | subset / different | observed / unknown lists | Ours is a blanket empty stub on two roots; theirs has curated exclusions |
| **Fog off** | `metadata/environmentsettings`, `.env` | Replace the quoted `"fog"` key with `"xog"` | env patch (`Fog`) | Same `.env` root; observed replace map `"audio"` → `"xaudio"` with a `"audio"` pre-check | partial | different | observed / intent unclear | The decompiled class does not show a `"fog"` replacement; verify against a real `.env` before changing ours |
| **PoE2 patch set** | none | none | several PoE2 classes | Clean terrain, delirium, MTX, black screen, and others | no | — | observed | Largest coverage gap |

## Measured reach

Counts taken from the local Steam `Bundles2` install by running the Hollowwork planner targets and
the reference roots and extensions against the same index. **Files walked**, not files modified:
both implementations decide per file whether an operation applies.

| Patch | Hollowwork files | Reference files (roots) | Ratio | Reading |
|---|---:|---:|---|---|
| Camera zoom | **1** (of 39 node-bearing files) | **39 node-bearing files** out of 100 curated paths | 39× | Full scan of 13,304 `.ot` files under `metadata`: 39 contain `CreateCameraZoomNode`, ours edits the player one. The rest are scripted cinematics |
| Shadows off | 1,466 | 1,466 | 1× | Same root, same extension; parity |
| Fog off | 1,466 | 1,466 | 1× | Same root; the operation differs, not the reach |
| No loading screen | **3** | **3** | 1× | Exact file-set parity |
| Minimap colors | **127** | **2** | 63× | Ours scans every shader; two files are relevant |
| Map revealed | **127** | **2** | 63× | Same: a single `res_color` line in one shader |
| Clean terrain | 13,091 | 17,186 | 1.3× | Missing `metadata/effects/misc` and `metadata/terrain` outside doodads |
| Delirium off | 21 | 21 | 1× | Equal; the `.aoc` difference is inert (see below) |
| Remove MTX | 7,711 | 22,626 | 2.9× | 14,917 of the extra files are `metadata/items/armours` (17,188) and `metadata/items/weapons` (2,721) |
| Particles off | 2,087 | 20,294 | 9.7× | The reference also walks `metadata/particles/monster_effects` (13,012) and `metadata/particles/masters` |

### Correction: `.aoc` is inert

Earlier rows list `.aoc` as a missing extension. The current build has **zero `.aoc` files** under
`metadata/terrain/doodads`, `metadata/effects/microtransactions` and
`metadata/miscellaneousobjects/affliction`. Adding `.aoc` to our targets changes nothing today; it
only costs a second directory pass. Keep it out unless a future build ships those files.

### What the numbers change

- **Minimap and map reveal are the clearest defects.** Walking 127 shaders to edit two is 125 wasted
  reads per run, and it makes the dry-run preview unreadable.
- **Camera is not a coverage bug — it is a narrower feature.** A full scan of the `.ot` corpus finds
  39 files with a `CreateCameraZoomNode`, and only one is the player camera. The other 38 are boss
  intros, arena framing and league set pieces. Pinning those to a fixed zoom is a different mod
  with a different failure mode: it removes the camera work the encounter was authored around.
  Decide it on purpose, as its own patch, not by widening this one.
- **Remove MTX should not simply copy the broader roots.** Two thirds of the reference reach is
  item art. Widening without exclusions would strip blocks from every armour and weapon model.
  The honest move is to add the microtransaction subfolders and leave `metadata/items` alone.
- **Particles can grow, but deliberately.** `metadata/particles/monster_effects` is 13k files; a
  blanket walk there is a real performance and correctness risk. Add roots one at a time and diff
  before and after.

## Reference patches not represented in Hollowwork

Not exhaustive; these are the components visible in the dump with no direct Hollowwork JSON
counterpart:

- Black screen (PoE1 and PoE2)
- Boss life bars, monster life display, monster rarity radar
- No monsters, no monsters with an HP bar
- Custom path icons, custom path effects
- Color mods, dynamic color mods (PoE1 and PoE2)
- Audio folder removal, sound removal
- Atlas fog, rain, clouds
- Map beam color, beam colors, beam effects
- Texture resolution reduction
- Pet removal, corpse removal, bloom removal
- Skill effect, monster effect and character effect removal

Some are variants or helpers; the list is a coverage map, not a promise that each should be ported.

## What is not proven

- Exact file sets require running both planners against the same archive and diffing the resulting
  paths. Static roots and lists only approximate this.
- The reference block lists are obfuscated; `stripBlock` coverage is inferred from the strings and
  calls that remain readable.
- The fog discrepancy may be a decompiler artifact. It is recorded as a risk, not as a definitive
  statement of the other tool's user-visible behavior.
- No runtime comparison was performed.

## Backlog derived from the matrix

**P0 — correctness before breadth**

1. **Finish the file-set diff**: the Hollowwork side is measured (see *Measured reach*); the
   remaining step is a per-file comparison of which files each side actually modifies, not just
   walks.
2. **Camera audit — done.** 39 `.ot` files contain `CreateCameraZoomNode`; ours edits the player
   node and nothing else. Keep that scope. Scripted-camera zoom is a separate, opt-in patch.
3. **Minimap / map reveal**: restrict to the two minimap shaders and parameterize the reveal
   threshold; stop scanning every `shaders/*.hlsl` for a single line.
4. **Fog decision**: validate the quoted `"fog"` key against the real `.env` and document why it
   differs.

**P1 — close the PoE1 coverage gap**

5. Clean terrain: add `metadata/effects/misc` and `metadata/terrain`, and align the block and
   exclusion lists.
6. Delirium: align the block list; no target change is needed.
7. Remove MTX: expand roots carefully; avoid stripping legitimate item art.
8. Particles: add `.trl` and the missing roots, keeping chest, stash and crafting exclusions.
9. No loading: compare the insertion anchors so both hide the same widgets.

**P2 — new surface**

10. PoE2 patch set for every PoE1 patch that has a PoE2 counterpart.
11. Missing feature classes: black screen, boss life bars, no monsters, path icons and effects,
    audio removal, color mods, rain and clouds, texture reduction.
12. Safety tooling that is not parity: dry-run **asset-path lint** (the `hellfog` → `hellxog`
    class of bug), per-file diff, and patch profiles.
