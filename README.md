# VisualCommit

A cross-platform desktop Git client: a visual commit graph, staging, branching, rebasing and
conflict resolution with clicks, right-clicks, double-clicks and drag-and-drop. It drives the real
`git` executable, so it behaves exactly like the command line. No account, no login, no telemetry.

VisualCommit is in early development, and today it only reads. It opens, initialises and clones
repositories in tabs that come back after a restart, and draws a repository's commit graph with
its branches, tags and stashes; 100,000 commits load in a few seconds and the first rows show
after about one. A left panel lists branches, remotes, tags and stashes with ahead/behind counts
and a filter, a details panel shows a commit's message and changed files, and changes made in a
terminal appear by themselves. Staging, committing and every other git operation come in later
phases. [docs/status.md](docs/status.md) says which phase is being built and
[docs/plan.md](docs/plan.md) what each phase adds.

## Build and run

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Git 2.30 or newer. On an
Ubuntu or Debian machine that lacks them, `bash scripts/setup-linux.sh` installs them together
with the system libraries the tests use.

```
dotnet build
dotnet run --project src/VisualCommit.App
dotnet test
```

`dotnet test` runs the unit and integration tests and a scripted walk-through of the whole app in
a headless window; it saves screenshots under `artifacts/visual/`. It does not open a window and
does not touch your settings. It takes a few minutes, and the first run builds a 100,000-commit
test repository in the temporary folder, which later runs reuse. The tests need Git 2.32 or
newer. All commands, including the Windows-only real-window test pass, are
listed in [CLAUDE.md](CLAUDE.md#commands).

Windows is developed and tested first. macOS and Linux are built and tested by CI on every push
that changes more than documentation.

## How the repository is laid out

| Folder | Contents |
|---|---|
| `src/VisualCommit.Core` | Models and interfaces; no UI and no process calls |
| `src/VisualCommit.Git` | Everything that starts `git` or reads its output |
| `src/VisualCommit.App` | The Avalonia app: views, view models, theme |
| `tests/` | Unit and integration tests, headless UI tests, and the two passes of the visual test gate |
| `docs/` | Requirements, plan, architecture, decisions, and a handoff and test report for each phase |

[docs/architecture.md](docs/architecture.md) describes how the app is built.

## Licence

VisualCommit is released under the [MIT licence](LICENSE). Third-party work it includes is listed
in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
