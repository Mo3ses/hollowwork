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
    { "type": "replace", "from": "fog", "to": "xog" }
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

## Operations

| Type | Fields | Effect |
|---|---|---|
| `replace` | `from`, `to` | literal replacement, counted per file |
| `replaceExtension` | `from`, `to` | swaps an extension or asset reference |
| `setBool` | `from`, `to` | rewrites `"key": true` to `"key": false` (or the inverse) |
| `stripBlock` | `from` | empties a named block, preserving braces |
| `removeLines` | `from` | removes lines matching a regex (opt-in, reported) |

All operations are applied in order, per file. The plan records:

```
file  metadata/environmentsettings/...env
  replace "fog" -> "xog"   3 hits
```

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
