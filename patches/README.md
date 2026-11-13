# Patches

Each JSON file is one user-visible patch. Folders match the game id (`poe1`, `poe2`).

Start by reading `docs/PATCH-FORMAT.md`. Keep targets narrow: the smaller the subtree, the faster
the dry run and the safer the apply.

Naming:

```
<game>.<intent>.json     e.g. poe1.fog-off.json
```

Every patch must have a unique `id`, a one-sentence description and at least one operation.
