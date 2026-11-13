# Architecture

## Projects

```
Hollowwork.App       Avalonia UI (views, styles, view models)
Hollowwork.Core      archive access, patch execution, backup, reporting
Hollowwork.Patches   patch model, loader, validation
```

Dependency direction: `App -> Core`, `App -> Patches`, `Core -> Patches` (engine consumes
definitions). `Patches` has no UI or archive dependency.

## Layers

1. **Archive layer** (`Core/Archives`)
   - `ArchiveProbe` detects GGPK vs bundle index without parsing.
   - `ArchiveSession` (M1) wraps LibGGPK3 and exposes a read/write facade.
   - No other layer references LibGGPK3 directly.

2. **Patch layer** (`Patches`)
   - `PatchDefinition`, `PatchTarget`, `PatchOperation`.
   - `PatchLoader` parses and validates JSON.
   - Operations are pure text transforms; they never touch disk.

3. **Engine** (`Core/Patches`, M1)
   - Resolves targets against the archive tree.
   - Produces a `PatchPlan` (file, line, before/after) without writing.
   - Applies the plan atomically, one file at a time, with a journal.

4. **Backup** (`Core/Backup`, M1)
   - Copies the archive to `backups/<utc timestamp>/`.
   - Writes a SHA-256 manifest and verifies the copy.
   - Restore is a separate, confirmed action.

5. **UI** (`App`)
   - Three-pane workbench: rail, explorer, editor, patch drawer.
   - No business logic in views.

## Threading

- Archive reads and patch planning run on a background thread.
- Writes are serialized through a single writer.
- UI receives progress through `IProgress<PatchProgress>`.

## Failure policy

- Any validation error aborts before writing.
- Any write error stops the run and keeps the journal; the backup stays intact.
- The app never deletes a backup automatically.

## Dependencies

| Package | Why |
|---|---|
| `Avalonia` + `Avalonia.Desktop` + `Avalonia.Themes.Fluent` | cross-platform UI |
| `Avalonia.Fonts.Inter` | UI font |
| `LibGGPK3.LibBundledGGPK3` | GGPK and bundle access (AGPL) |
| `xunit` + `Microsoft.NET.Test.Sdk` | tests |

New dependencies need a one-line justification in the pull request.
