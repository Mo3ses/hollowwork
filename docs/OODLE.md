# Oodle

`Bundles2` content is compressed with **Oodle**, a proprietary codec from RAD Game Tools / Epic.
Hollowwork does not ship Oodle, in any form.

## Why

- Oodle is closed source and its license does not allow redistributing the library on its own.
- This repository is AGPL-3.0: shipping a proprietary binary would break the source-complete
  promise of the distribution.
- The game links Oodle statically, so there is no DLL/`.so` to borrow from the install.

## What the user provides

| Platform | File names searched |
|---|---|
| Linux | `liboo2corelinux64.so.9`, `liboo2core.so`, `liboo2core.so.9` |
| Windows | `oo2core.dll`, `oo2core_9_win64.dll`, `oo2core_8_win64.dll` |
| macOS | `liboo2core.dylib` (untested) |

Search order:

1. `--oodle <path>` (CLI) or the configured path (app).
2. `HOLLOWWORK_OODLE` environment variable.
3. Next to the executable.
4. `%LOCALAPPDATA%\Hollowwork\oo2core\` (Windows) or
   `~/.local/share/Hollowwork/oo2core/` (Linux).

The library is loaded with `NativeLibrary.SetDllImportResolver`, so any file name works as long as
the path is given.

## Validation done on a real install

PoE 1 Steam, 3.29.3d, `Bundles2`:

```
index      : 20.829.707 bytes
files      : 1.245.702
bundles    : 46.884
read       : metadata/environmentsettings/1-9-6.env (2472 bytes)
sha256     : 76e02847bd25d601...
```

Write path exercised on a **copy** (`_.index.bin` + one bundle): read → write → flush → reopen,
content preserved, new bundle created under `LibGGPK3/`. The real install was never written.

## Proton fallback

If you cannot obtain a Linux Oodle library, the Windows build runs under Proton exactly like the
game. A launcher is provided under `packaging/linux/`. The Windows build still needs `oo2core.dll`
next to the executable, supplied by the user.
