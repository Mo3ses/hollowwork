# Contributing

## Setup

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Hollowwork.App
```

Linux and Windows are first-class. If you touch the UI, run the app on both.

## Pull requests

- One concern per pull request.
- New dependency? Add a one-line justification.
- New patch? Follow `docs/PATCH-FORMAT.md` and keep targets narrow.
- No network calls, no telemetry, no process interaction.
- Commit style: `feat:`, `fix:`, `docs:`, `test:`, `refactor:`.

## Code

- `nullable` is on; do not suppress warnings without a comment.
- Business logic belongs in `Hollowwork.Core`, not in views.
- Every operation that writes must be covered by a test.
