# Contributing

## Setup

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Hollowwork.App
```

Linux and Windows are first-class. If you touch the UI, run the app on both.

## Naming — no third-party app or product names

**Never name another application, client, tool or product** — not in source, comments, docs, UI
strings, patch files, test names, commit messages, branch names, issues, pull requests, reviews or
chat. This rule has no exception for "just a comparison" or "just a credit".

Use generic wording instead:

| Instead of the name | Write |
|---|---|
| another mod manager / client | `reference suite`, `reference app`, `another client` |
| an external reverse-engineering dump | `external reference dump` |
| a feature they ship | `the equivalent feature in the reference suite` |

Rationale: the repository documents its own behavior. Borrowed names imply endorsement, drag in
licensing and support expectations, and turn a technical document into an advertisement for
someone else's product. Comparisons stay useful when they describe the *mechanism* — target paths,
operations, coverage, confidence — and not the brand.

If a comparison needs provenance, point at the artifact's own identifier (a file hash, a version
string, a commit id), never at the product name.

## Pull requests

- One concern per pull request.
- No third-party names, as described above. Check the diff, the commit message, the branch name
  and the pull request title/body before opening.
- New dependency? Add a one-line justification.
- New patch? Follow `docs/PATCH-FORMAT.md` and keep targets narrow.
- No network calls, no telemetry, no process interaction.
- Commit style: `feat:`, `fix:`, `docs:`, `test:`, `refactor:`.

## Releasing

Releases are cut from `main`, never from a branch, and publishing is deliberate: merging a pull
request never produces a release.

1. Bump `Version` in `Directory.Build.props` and add the matching section to `CHANGELOG.md`.
2. Merge that change.
3. Run the **release** workflow manually with the version, or push a `v<version>` tag. Both paths
   build the same artifacts.
4. The workflow builds `linux-x64` and `win-x64`, writes `SHA256SUMS.txt` and opens a **draft**
   release. A manual run creates the tag only when you publish the draft.
5. Review the assets and the generated notes, then publish.

To withdraw a release that was published by mistake, delete it and the tag, then re-run the
workflow with the corrected version.

## Code

- `nullable` is on; do not suppress warnings without a comment.
- Business logic belongs in `Hollowwork.Core`, not in views.
- Every operation that writes must be covered by a test.
