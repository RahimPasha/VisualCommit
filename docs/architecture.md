# Tracery — Architecture

**Nothing is built yet.** This file currently describes the intended design. From Phase 0 on it
describes what is actually built, and it is updated at the end of every phase. Where the code and
this file disagree, fix this file. The reasons behind the main choices are in
[decisions.md](decisions.md).

## Design

- **One process, all C#.** An Avalonia desktop app using MVVM (CommunityToolkit.Mvvm). The UI calls
  the git layer directly; there is no web server or API layer in between.
- **Git engine: the real `git` executable.** A process runner starts git, streams its output and
  parses the machine-readable formats (`log` with a custom format, `status --porcelain=v2`,
  `blame --porcelain`, unified diffs). Hunk and line staging build a patch and apply it to the index.
- **Operation queue per repo.** One writing operation at a time, with progress, cancellation and a
  record in the activity log (T4).
- **Safety net.** Before a destructive operation the app stores a backup reference under
  `refs/tracery/backup/`. Undo/redo (O2) restores from these and from the reflog.
- **Prompts.** Git's credential, passphrase and rebase-editor prompts are redirected to small
  helper hooks that talk to the app, so they appear as in-app dialogs.
- **Editors.** Diff, blame and conflict views are built on AvaloniaEdit with TextMate grammars for
  syntax highlighting.
- **Hosting.** One provider interface with GitHub, Azure DevOps and GitLab implementations.
  Tokens live in the OS keychain.
- **Storage.** Settings and session state as JSON in the per-user app-data folder. Logs are local files.
- **Tests.** xUnit. Git-layer tests run real git against temporary repos. UI tests use Avalonia's
  headless mode and capture screenshots.
- **CI.** GitHub Actions builds and tests on Windows, macOS and Linux on every push.
- **Packaging.** Velopack for installers and auto-update on all three platforms.

## Solution layout

The solution file, `Directory.Build.props` and `Directory.Packages.props` sit at the repo root.
App projects go under `src/`, test projects under `tests/`. A project is created in the phase
that first needs it, so `Tracery.Hosting` arrives in Phase 7. Packages use the latest stable
release at the time Phase 0 runs, pinned centrally; the handoff records the versions.

| Project | Contents |
|---|---|
| `src/Tracery.Core` | Domain models and interfaces (including settings and logging); no UI, no process calls |
| `src/Tracery.Git` | Git runner, output parsers, operation queue, repo watcher |
| `src/Tracery.Hosting` | GitHub, Azure DevOps and GitLab clients; token store |
| `src/Tracery.App` | Avalonia views, view models, theme, custom controls (graph, diff, resolver); settings store and logging set-up |
| `tests/Tracery.Testing` | Shared test helpers: the temporary-repo builder and the scenario repos |
| `tests/Tracery.Git.Tests` | Integration tests against temporary repos |
| `tests/Tracery.App.Tests` | View-model tests and headless UI tests |
| `tests/Tracery.VisualTests` | Scripted walk-throughs, the real-window pass and screenshot capture for the visual test gate |

## As built

Empty until Phase 0. Each phase adds what it built here: the main types and where they live, how
data flows from a git command to the screen, and the conventions the code follows.
