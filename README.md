# VisualCommit

A cross-platform desktop Git client: a visual commit graph, staging, branching, rebasing and
conflict resolution with clicks, right-clicks, double-clicks and drag-and-drop. It drives the real
`git` executable, so it behaves exactly like the command line. No account, no login, no telemetry.

VisualCommit is in early development. Today it opens to an empty shell: the main window with its
regions laid out, a dark and a light theme, and Git detection. Nothing can be done with a
repository yet. [docs/status.md](docs/status.md) says which phase is being built and
[docs/plan.md](docs/plan.md) what each phase adds.

## Build and run

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Git 2.30 or newer.

```
dotnet build
dotnet run --project src/VisualCommit.App
dotnet test
```

`dotnet test` runs the unit and integration tests and a scripted walk-through of the whole app in
a headless window; it saves screenshots under `artifacts/visual/`. It does not open a window and
does not touch your settings. All commands, including the Windows-only real-window test pass, are
listed in [CLAUDE.md](CLAUDE.md#commands).

Windows is developed and tested first. macOS and Linux are built and tested by CI on every push.

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
