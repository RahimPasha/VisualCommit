# Phase 2 handoff — Diff and commit workflow

Written at the end of phase 2 for the session that runs phase 3. That session has not seen any
of this phase's conversation. How the app is built is in [architecture.md](../architecture.md);
this file says what was proven, what to copy, and what to watch out for.

## Result

Everything in the plan's "Delivers" list was delivered.

| Delivered | Proven by |
|---|---|
| Diff viewer, inline and side by side | Visual checks 3, 4, 7, 8, 21 (21 across a real process restart); `DiffLayoutTests`, `DiffTextViewTests` |
| Syntax highlighting and word-level highlights | Visual checks 3, 4, 12, 21; `DiffHighlighterTests`, `DiffWordHighlightsTests`, `DiffTextViewTests` |
| Image diff; fallbacks for binary and very large files | Visual checks 5, 6, 7; `WorkingChangesTests` (Git), `DiffParserTests` |
| Working-changes row in the graph | Visual checks 1, 20, 23, 24; `WorkingChangesTests` (App) |
| Staged and unstaged lists | Visual checks 1, 2, 9, 22; `WorkingChangesTests` (App and Git) |
| Stage, unstage and discard by file, hunk and line | Visual checks 9 to 13, 17 to 19, 22; `WritingTests`, `PatchFuzzTests`, `DiffSelectionTests` |
| Commit and amend, with a summary and description editor | Visual checks 14, 15, 16, 24, 25; `WritingTests`, `WorkingChangesTests` (App) |
| A recoverable snapshot before every discard | Visual checks 13, 17, 18, 19; `WritingTests`, `WorkingChangesTests` (App: restore over newer work, conflicts, a closed tab) |

Not delivered: nothing. Moved to phase 3 (`plan.md` lists them): stage, unstage and discard in
a file's right-click menu (D70; buttons today), and the operation queue taking over the
one-at-a-time writes (D76).

## State of the repo

The visual test gate passed on `2a13804`, the gate's commit: `dotnet test` (557 tests) and the
real-window pass (14 tests, phases 0, 1 and 2) on the same commit, every picture of both
inspected. The report is [test-reports/phase-2.md](../test-reports/phase-2.md); CI run
[38097840078](https://github.com/RahimPasha/VisualCommit/actions/runs/38097840078) built and
tested that commit in all five jobs, each with 557 tests passed. The commits after it change
only docs. The phase is "Awaiting acceptance" on `phase/2-diff-commit`; nothing is merged into
`master` yet. Only the real window at 1920×1080 could not run, as the plan allows on this
screen.

Before the gate, an independent review of the code (an Opus agent) found 14 problems. All were
fixed in `b559de2`, each with a test; D74 to D76 record the changes of behaviour.

## Environment

| | |
|---|---|
| Machine | Windows 11 Pro 10.0.26300; screen 3000×2000 at 200% scaling (1500×952 logical work area). The whole phase was built on this machine; no cloud session worked on it |
| .NET SDK | 10.0.303 |
| Git | 2.36.0.windows.1 locally; 2.55.0 on the CI runners |
| Packages | Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0 (built against Avalonia 12.0.0), TextMateSharp and TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, D24), FlaUI.UIA3 5.0.0; the font JetBrains Mono NL 2.304 is a file in the repo (OFL, in `THIRD-PARTY-NOTICES.md`) |
| Screenshots | Scripted: scaling 1. Real window: 200% |

## Build, run and test

The table with notes is the Commands section of [CLAUDE.md](../../CLAUDE.md#commands); nothing
new was needed.

```
dotnet build
dotnet run --project src/VisualCommit.App
dotnet test
dotnet test --project tests/VisualCommit.VisualTests --filter-class "*Phase2Checks"
dotnet test --project tests/VisualCommit.RealWindowTests -c Release
```

The default run now takes about 5 to 6 minutes on the development laptop (all three test
projects run side by side; the scripted walk-through is the longest), and up to a third longer
when the laptop is hot. CI takes about 3.5 minutes.

## Running the visual gate

As in phase 1's handoff, with phase 2 added:

1. `dotnet test` at the repo root, after a few minutes' rest if the laptop has been busy. It
   writes `artifacts/visual/phase-{0,1,2}/scripted/`, emptying each first.
2. Rest three minutes, tell the owner, and run
   `dotnet test --project tests/VisualCommit.RealWindowTests -c Release` (never `--no-build`).
   It takes about 6 minutes and uses the real mouse and keyboard.
3. Open every picture. Before any new run, save `sha256sum` of the pictures you inspected: the
   next run empties the folders. `docs/test-reports/phase-2-pictures.sha256` lists all 187
   files phase 2's gate inspected; `tr -d '\r' < docs/test-reports/phase-2-pictures.sha256 | sha256sum -c`
   shows which scripted pictures changed. Scripted pictures are the same byte for byte from run
   to run except phase 0's `06c` and phase 1's `11a`, `13b`, `14a`, `14b`; real-window pictures
   are never byte-identical and must all be opened. The comparison passes a hover background
   (phase 1's `08b`): only the inspection catches such things.

## What changed in the code

| To do this | Start here |
|---|---|
| Write to the repository | `GitRepository.Writes.cs` (stage, unstage, patches, snapshots, discard, restore, commit). Every call gets `GIT_EDITOR=:` and `GIT_LITERAL_PATHSPECS=1` from `GitRunner` (D68, D74) |
| Run a write from the UI | `RepositoryViewModel.WriteAsync`: one at a time (D76), the watcher paused (D71), then the status (and refs, for a commit) read again and git's message shown in the stage panel. Phase 3's operation queue should take this over |
| Ask the user to confirm | `IDialogService.ConfirmAsync` (`DialogHostViewModel`, `ConfirmationView`; D69). Credential prompts can use the same layer |
| Show a file's diff | `RepositoryViewModel.OpenDiff` / `OpenCommitDiff`, `DiffViewModel`, `DiffView`, `Controls/Diff/DiffTextView` |
| Build a patch of some lines | `PatchBuilder.Build` (Core); `PatchFuzzTests` checks it against git |
| Read the working tree | `GitRepository.ReadStatusAsync` (`WorkingTreeStatus`), `ReadDiffAsync`, `ReadFileAsync` |
| A file row with actions | `ChangedFileList` and the `FileFolderButton` theme; the stage panel's rows in `WorkingChangesView` |

## Patterns to follow

| For | Example |
|---|---|
| A git write and its test | `GitRepository.StageAsync` with `WritingTests`: a scenario repo, the real `GitRepository`, the result read back with plain git |
| Many cases against real git in one call | `PatchFuzzTests`: build every case's repo content with one `git fast-import`, run the operation once for all, read back with one `git cat-file --batch` |
| A view model that writes, with real git | `WorkingChangesTests` (App): `AnsweringDialogs` scripts the confirmations, `WaitingDialogs` holds one open |
| A scenario repo | `Scenarios.ChangesAsync` (built once per process, copied per test), `CrlfAsync` and `ConflictAsync` (built each time), pinned in `ScenarioTests` |
| A visual check that writes | `Phase2Checks.Stage.cs` and `.Discard.cs`: act through the UI, wait for the status, assert against git and `Phase2Expectations.cs` |
| A real-window check | `Phase2RealWindowPass`: wait for the log line `<name>: working tree: N staged, M unstaged` |
| A context-menu action | None yet; phase 3 sets the pattern (D70 lists the file actions it must add) |

Rules worth keeping:

- A diff, a patch or a blob travels as Latin-1 text, one character per byte (D66); never as
  UTF-8, or bytes that are not UTF-8 change on the way.
- A read must never write (D47): `git diff` writes the index even with `GIT_OPTIONAL_LOCKS=0`
  (Git 2.36), so an unstaged diff is read with `git diff-files -p`.
- A test that asserts what the user sees must assert what is in view, not what is in the
  control's data: check 7's test passed while the picture differed from its expected result.

## Deviations from the plan

| What | Decision |
|---|---|
| The working-changes row only when there are changes | D60 (owner's choice) |
| Inline by default; the mode is a setting | D61 (owner's choice) |
| A restore bar after a discard | D62, D73 (owner's choice) |
| A single click opens a diff, in place of the graph | D63 (owner's choice) |
| AvaloniaEdit with TextMateSharp called directly; JetBrains Mono NL | D64 |
| What counts as very large | D65 |
| Latin-1 transport; patches built from git's own lines | D66 |
| Snapshots as commits under `refs/visualcommit/backup/discard-*` | D67, D75 |
| No editor; the message on standard input | D68 |
| Dialogs inside the window | D69 |
| Buttons for stage and discard until phase 3's menus | D70 |
| The watcher watches the working tree; the app's writes pause it (part of D52 brought forward) | D71 |
| Fixed identity and dates for the app's commits in tests | D72 |
| Literal pathspecs | D74 |
| Safer discard and restore | D75 |
| Writes one at a time; type changes; word-highlight budget | D76 |

## Known issues

| Issue | How much it matters | Where |
|---|---|---|
| A file with a conflict is listed (U) and shows "This file has a merge conflict"; it cannot be discarded, and staging it marks it resolved as `git add` does | Expected until phase 3's conflict banner and phase 4's resolver | `WorkingChangesViewModel`, `DiffViewModel` |
| A type change (a file that became a symbolic link) shows both halves but offers no hunk or line actions | By design (D76); rare | `DiffParser.ParseTypeChange`, `DiffViewModel.ActsOnLines` |
| A repository inside the working tree is listed as an untracked folder; its "diff" is a message, and it is never discarded. Staging it adds it as an embedded repository, as `git add` does | Low; submodules are not in scope yet | `RepositoryViewModel.DiscardAsync` |
| Only the last discard has a restore bar; older snapshots stay as refs with no UI, and nothing ever deletes them | Low; phase 5's undo (O2) reads them | `GitRepository.SaveSnapshotAsync` |
| A path that vanished is passed over when staging; unstaging or discarding a tracked path that left the index meanwhile still fails with git's message | Low; a refresh shows the real state | `GitRepository.Writes.cs` |
| In a diff of many long lines, the lines past the word-highlight budget have no word highlights | By design (D76) | `DiffWordHighlights` |
| On Linux the watcher watches every folder of the working tree (risk 13 in `plan.md`) | Low unless the tree is huge | `RepositoryWatcher` |
| Earlier phases' issues remain (ssh prompts, the left panel's selection, the welcome page's wrapping, Q1's margin): see phase 1's handoff | As there | |

## Things that cost time

- **Shell quirks.** In the Bash tool, a heredoc turns `\n` inside a string into a real line
  break and fails on apostrophes; write scripts with the Write tool. `sed -i` in Git Bash drops
  the carriage returns of a CRLF file. PowerShell 5.1 garbles dashes and backticks in docs.
- **The laptop's heat.** After an hour of tests every project ran a third slower and phase 1's
  check 18 missed its 5 seconds once. Rest before the gate's runs.
- **Pictures are overwritten.** Every run empties the screenshot folders: hash what you
  inspected before starting another run.
- **`git check-ignore` refuses literal pathspecs** (`fatal: pathspec magic not supported`); it
  runs without `GIT_LITERAL_PATHSPECS`.
- **Timing limits in tests.** A tokenizing time limit of 50 ms per line was flaky under load
  (now a second); a watcher test that waited a fixed time for its own change failed once the
  ignore check really ran. Wait for the event, not for a duration.
- **macOS decodes PNGs as RGBA**, Windows as BGRA: check the pixel format when sampling.
- **xUnit's analyzer (xUnit1051)** fails the build when a test calls a method that takes a
  `CancellationToken` without `TestContext.Current.CancellationToken`.
- **A sub-agent's worktree** (`.claude/worktrees/agent-a60aa26c7fe44ae22`, from phase 1) could
  not be removed: the permission system refused `git worktree remove --force --force`. It is
  git-ignored and harmless; the owner can remove it.

## For the next phase

Reuse: `WriteAsync` and the watcher's `Pause` for every operation until the queue replaces them,
`IDialogService` for confirmations and prompts, `PatchBuilder` and `DiffTextView` (phase 4's
resolver can build on the text control), `ChangedFileList` for file rows, `Scenarios.ConflictAsync`
for conflict states.

Do first:

1. Read the Known issues above and phase 1's, and check the latest CI runs.
2. Decide the operation queue's shape: it must keep D76's one-at-a-time rule and D71's pause,
   and show git's message for a failure (C11).
3. Write phase 3's checks into `docs/test-reports/phase-3.md` before the UI.

Risks to watch:

- Destructive operations (reset, force-push, branch delete) need backup refs like D67's
  snapshots and a confirmation (Q3); reuse the dialog layer.
- The default test run is already 5 to 6 minutes on the laptop (risk 12): phase 3's checks with
  remotes and fetches will add to it.

## Waiting on the owner

- Accept phase 2, or say what to change: "Phase 2 accepted" merges `phase/2-diff-commit` into
  `master`.
- When you have a Mac at hand: the seven-step checklist at the end of the
  [phase 2 test report](../test-reports/phase-2.md), and phase 0's and 1's, still open.
