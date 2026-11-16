# Patch format

A patch is a JSON file under `patches/poe1/` or `patches/poe2/`. It is data, not code.

## Shape

```json
{
  "id": "poe1.fog-off",
  "name": "Fog off",
  "description": "Disables the fog block inside environment settings.",
  "game": "poe1",
  "targets": [
    { "root": "metadata/environmentsettings", "extensions": [".env"] }
  ],
  "operations": [
    { "type": "replace", "from": "\"fog\"", "to": "\"xog\"" }
  ]
}
```

| Field | Required | Notes |
|---|---|---|
| `id` | yes | unique, lowercase, `game.slug` |
| `name` | yes | short, shown in the drawer |
| `description` | yes | one sentence, no marketing |
| `game` | yes | `poe1` or `poe2` |
| `targets` | yes | at least one root + extension list |
| `operations` | yes | at least one operation |

A target is a directory root (relative to the archive root) plus the file extensions to walk.
The engine resolves the subtree lazily and reports the file count during the dry run.

Add `files` when the root is wide but only a few paths matter. The list is an exact allowlist of
archive paths; everything else under the root is ignored without being read. Use it for shaders
and other large folders where a one-line edit would otherwise walk thousands of files:

```json
{ "root": "shaders", "extensions": [".hlsl"],
  "files": ["shaders/minimap_visibility_pixel.hlsl"] }
```

Matching is case-insensitive and accepts backslashes. `exclude` still applies on top of `files`.
An empty list is a validation error: omit the field to walk the whole root.

Operations normally live at the patch level and run against every file the targets select. Add
`operations` **inside a target** to scope them: when present, that list replaces the patch-level
list for the files of that target only. Use it when two files share the same pattern but need
different replacements:

```json
"targets": [
  { "root": "shaders", "extensions": [".hlsl"], "files": ["shaders/blur.hlsl"],
    "operations": [ { "type": "replaceRegex", "from": "(float4\\s+ApplyBlur...", "to": "$1..." } ] },
  { "root": "shaders", "extensions": [".hlsl"], "files": ["shaders/depthawareblur.hlsl"],
    "operations": [ { "type": "replaceRegex", "from": "(float4\\s+ApplyBlur...", "to": "$1..." } ] }
]
```

A patch with no patch-level operations is valid as long as every target carries its own list. An
empty `operations` array on a target is a validation error.

## Operations

| Type | Fields | Effect |
|---|---|---|
| `replace` | `from`, `to` | literal replacement, counted per file |
| `replaceExtension` | `from`, `to` | swaps an extension or asset reference |
| `setBool` | `from`, `to` | rewrites `"key": true` to `"key": false` (or the inverse) |
| `stripBlock` | `from` | empties a named block, preserving braces |
| `removeLines` | `from` | removes lines matching a regex (opt-in, reported) |

All operations are applied in order, per file. Any operation also accepts `unless`: when the file
already contains that literal text, the operation is skipped and reported as `0 hits`. Use it to
keep a patch idempotent, so re-applying it to an already patched archive does nothing:

```json
{ "type": "replace", "from": "...", "to": "...", "unless": "res_color = max(res_color, " }
```

The plan records:

```
file  metadata/environmentsettings/...env
  replace "\"fog\"" -> "\"xog\""   1 hit
```

Quote JSON keys when the intent is to disable a block. A bare `fog` replacement also matches
asset names such as `hellfog_03_embers_grd.ao`, and the game then fails to load the missing file.

## Rules

- `from` must be non-empty for every operation except `stripBlock`.
- Unknown operation types fail validation.
- Duplicate `id` values fail the whole directory load.
- Operations never run against paths protected in `paths_to_protect.json`.
- A patch cannot declare a target outside the archive root.

## Authoring

1. Copy an existing patch.
2. Change `id`, `name`, `description`.
3. Point `targets` at the smallest subtree that contains the files.
4. Add one operation per intent.
5. Run the app, select the patch, read the dry-run report.
6. Only then apply against a backed-up archive.
