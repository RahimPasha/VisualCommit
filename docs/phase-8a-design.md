# Phase 8a design — Release readiness in the app

This is phase 8a's design: what the phase builds, how it is cut into work packages, the contracts the
packages share, and one brief per package for its builder. Once the phase is built it is kept as
history, as phases 3 to 7b's designs are. Its decisions are recorded only in `docs/decisions.md`
(D243): the design names them by number (D416 to D446) and does not repeat them. The phase's report,
handoff and status cite its sections as "design N.N". Binding inputs: the owner's decisions D399 to
D415; `docs/plan.md`'s "Phase 8a", "Workflow" and "Visual test gate" sections and its risks; the 7b
handoff's "Patterns to follow", "Known issues" and "For the next phase"; the lead's 75 rulings of
2026-11-19 ("ruling n"), which section 13 carries in short form. The research is outside the
repository: the eight inputs in `D:/Projects/Personal/phase8-work/research2/` ("colours.md 3.2"),
with their measurements, and the digests and web briefs of 2026-11-18 in
`D:/Projects/Personal/phase8-work/research/`, where a skeptic's file wins over its brief. What the
research did not verify is marked unverified, with the step that verifies it. Line numbers are those
of `27c5109`; an earlier phase's design is cited "phase 4 design 11.10".

A builder reads section 0, section 1 and its own section, and nothing else. Nothing a package writes
holds a real person's data: examples use made-up authors at `example.com` and `/home/demo` (ruling
63), and the owner's name stands only where D409 and `LICENSE` put it, never with an e-mail address.

## Contents

- 0 Scope, packages and parts
- 1 Shared contracts (WP0's brief)
- 2 colours: the contrast round (part L)
- 3 floor: the git floor
- 4 single: one instance per data folder, and the macOS data folder (part I)
- 5 firstrun: git's detection, guidance and setting (part F)
- 6 keyboard: the keyboard pass and the focus fixes (part K)
- 7 icon: the icon's pipeline and a placeholder mark (part B)
- 8 about: the About section and the third-party notices (part A)
- 9 uia: screen readers, names and the graph's row (part U)
- 10 perf: the performance pass, measures first
- 11 guide: the user guide, its site, pictures and links (part G)
- 12 realwindow: the real-window part for 8a's checks
- 13 The lead's rulings, what waits on the owner, and the open points

## 0. Scope, packages and parts

A builder reads section 0, section 1 and its own section, and nothing else. Binding, in this order:
D399 to D415; plan.md's "Phase 8a"; the lead's 75 rulings ("ruling n", carried in short form in
section 13); requirements.md; CLAUDE.md and plan.md's "Workflow" and "Visual test gate" (D241 to
D247). The research is outside the repository: `D:/Projects/Personal/phase8-work/research2/` (eight
inputs, cited "colours.md 3.2") and `research/` (the digests and web briefs; a web brief's skeptic
file wins where they differ). Line numbers are those of `27c5109`. "§n" is this design's section n,
the brief of one package. The design's own decisions are D416 to D446; they stand in
`docs/decisions.md` alone (D243), and the sections cite them by number.

### 0.1 Scope against the Delivers list

| Delivers item (plan.md) | Where | Proven by |
|---|---|---|
| First-run states, "Check again", "Choose git.exe", winget, macOS and Linux guidance, the missing helper (R4, D405) | §5 | Part F; `GitSetupShellTests`; real window F |
| A broken `~/.gitconfig` shows git's own line | §5 | F6 (stand-in), F6b (real git, also on CI's floor job) |
| OpenSSH older than 8.4 (risk 15, ruling 7) | §5 log line; §11 guide | `AskPassBrokerHelperTests`; the guide's page |
| The netrc note (risk 35, ruling 8) | §5 | F14; `AccountsNetrcNoteTests` |
| The git floor (D406) | §3 | CI's floor job green and required; P1's tests |
| Keyboard gaps; focus after "Choose"; the loading diff; the commit boxes (R7) | §6 | Part K; `KeyboardPassTests`; real window K; the inventory (6.3.9) in the report |
| A name on every control; the graph's selected row (D411, D49) | §9 | The sweep; `CommitGraphControlAutomationTests`; real window U; Narrator by hand |
| The contrast round (D410) | §2 | `ThemeTokensTests`' table; part L; the masked comparison (0.7) |
| Performance pass | §10 | `Phase8aPerformance`'s series in the report; Q1 in the pass |
| Third-party notices (R6) | §8 | `ThirdPartyNoticesTests` |
| About (R6, D398, D409) | §8 | Part A; real window A1 and A4 |
| Icon and branding (R5, D407) | §7 | `IconFilesTests`; part B; real window B |
| User guide (D408) | §11 | The pages; the `guide-site` artifact; part G; checks by hand |
| Two instances; the macOS data folder (plan 778) | §4 | `SingleInstanceGuardTests`; part I; real window I; `AppPathsTests` in CI's macOS job |
| `Authors`, `Copyright` (D397); the `.slnx` comment | done in `e04762b` | The files |
| The README's "Name" note names the icon (D412) | the lead at §7's merge | The README |

The test classes named here are new, but `ThemeTokensTests` and `AppPathsTests`. "Done when" is
judged in four places: the scripted run on the laptop and in CI's six jobs (the floor's on git
2.34.1 included); the real-window pass; the checks by hand (Narrator, D411; phase 6's System step,
D224); the guide workflow's site artifact. Q6, for what the app itself shows, is judged on 8a's
walk-through pictures from CI's macOS and Linux jobs, read under the report's "Other platforms".

Out of scope: the packaging (8b, D399, ruling 1); a bundled git (D405); a high-contrast palette
(D411); a registered mark (D412); risks 38, 39, 40, 43 and 44 (D413, ruling 2) and any hosting code
but the netrc note; the Bold face (ruling 57); the runtime's notices (ruling 15); macOS's `.icon`
(ruling 17); the updater's guard key (ruling 67); the window peer's `GetFocus` (ruling 35); keys the
user changes and a key for Cancel (D192); writes for speed (ruling 19); deferred background loads
without a ruling (ruling 66); D401's question (8b). To the Backlog at the close: keyboard selection
of commits that are not next to each other (ruling 50), an item status on the left panel's rows
(ruling 36).

### 0.2 What the phase builds on, and the decisions

Paths are under `src/VisualCommit.App/`, or under `src/` where they start with another project's
name. Git: `VisualCommit.Git/GitLocator.cs` (one search, no seam), `VisualCommit.Core/Git/IGitRunner.cs`
(`GitDetection`), `.../GitVersion.cs` (`Minimum` 2.30.0), `Services/GitAccess.cs`, `AppSession.cs`
(one detection per session), `ViewModels/MainWindowViewModel.cs`, `VisualCommit.Git/Prompts/AskPassBroker.cs`
(D277's gate). Launcher: `Services/IFileLauncher.cs`, `FileLauncher.cs`. Look: `Theme/Tokens.axaml`,
`ThemeTokensTests`, `Contrast`, `TextInk`, `scripts/compare-pictures.py`. Commands:
`ViewModels/Commands/AppCommand.cs`, `AppCommands.cs`, `AppCommands.Palette.cs`, `AppShortcuts.cs`.
Settings: `SettingsScreenServices`, `SettingsDialogView.axaml`, `SettingsSection`. Graph:
`Controls/CommitGraphControl*.cs`. Start: `Program.cs`, `App.axaml(.cs)`, `Views/MainWindow.axaml(.cs)`,
`VisualCommit.Core/AppPaths.cs`. Harness: `ShellDriver`, `VisualRun`, `GitIsolation`, `TempRepo`,
`LargeHistory`, `GitCallBudget`; `RealApp`, `RealWindowRun`, `Phase1RealWindowPass` (Q1);
`tests/VisualCommit.VisualTests.Phase7b` as the pattern; `.github/workflows/ci.yml` with the floor job
(`continue-on-error`; in run 44479788558 it failed 20 of 7,889 tests).

The decisions are cited, not restated (D243); each line names the mechanism and where it is built.
- **D416** (ruling 37): 8a's tests that start the app through `ShellDriver` go into 8a's project.
- **D417** (rulings 49, 54, 62; amends D192, D199, D213): "Cancel {title}", "About VisualCommit…",
  "Open the user guide" with F1, the shortcuts list's group "Help". 1.8; §6, §8, §11.
- **D418** (rulings 24 to 29; amends D198): the contrast round's tokens. 1.5; §2.
- **D419** (rulings 13, 22, 30, 31): the scan, the masked comparison, the references after wave 1. 0.7; §2.
- **D420** (rulings 69 to 75; amends D2, D406): 2.34.1, P1 on every git, the stash names, the dead
  branches, the floor job required. §3.
- **D421**, **D422** (rulings 16, 67): one instance per data folder; "Try again" starts the app there. 1.11; §4.
- **D423**, **D424** (rulings 4 to 9, 40 to 48; D405): the git program setting and the lookup, one
  screen, winget, the helper; no ssh probe, the netrc note. 1.2 to 1.4; §5.
- **D425** to **D427** (rulings 49, 52; D425 amends ruling 49's F4): the stage rows' buttons on a
  focused row; Tab out of read-only text views; keyboard moves of the splitters kept. §6.
- **D428** to **D430** (ruling 17, D407): the masters, the tool, the sizes, checks by format. 1.6; §7.
- **D431** to **D433** (rulings 14, 15, 54 to 59; D409): About, the notices, the text dialog, the
  notices as build inputs. 1.13; §8.
- **D434** (amends ruling 58): `THIRD-PARTY-NOTICES.md` and the guide spell the American way. §8, §11.
- **D435** (rulings 32 to 36, 38; amends D49): the graph a List with one spoken row; every control
  named; the status bar a live region. 1.12; §9.
- **D436** to **D438** (rulings 19, 65, 66): the measures and bounds, how the series is taken, log
  lines that say only what is true. 1.9; §10.
- **D439** to **D441** (rulings 18, 60 to 64): mdBook and `GuideShots`; two images per picture; no
  outside host. §11.
- **D442** to **D446**: the real window's launches, the masked notice, nothing pressed that leaves
  the app, the graph's events, F4's git. §12.

### 0.3 How 8a is named

`8a` wherever the 7b handoff's "Naming" writes `8` (ruling 20):
- Docs: `docs/phase-8a-design.md`, `docs/test-reports/phase-8a.md`, `phase-8a-pictures.sha256`,
  `docs/handoffs/phase-8a.md`. Folders: `artifacts/visual/phase-8a/{scripted,real-window,timings}`;
  outside every list of pictures, the guide's pictures in `artifacts/guide/pictures/` and the
  performance series in `artifacts/perf/phase-8a/` (D437; never through `RealWindowRun`, which empties
  `artifacts/visual/phase-8a/real-window/`).
- Project and namespace `VisualCommit.VisualTests.Phase8a`; classes `Phase8aChecks` and
  `Phase8aChecks<Part>`; `Phase8aExpectations.Phase = "8a"` (a `string`); expectations
  `Phase8aExpectations.Common.cs` and `.<Part>.cs`; their tests `Phase8aExpectationTests.<part>.cs`
  and `Phase8aSupportTests.cs`. A `Phase8aExpectations` file uses nothing of the 8a project.
- `scripts/time-scripted-check.sh`: an `8a)` case before `7b)`, its default `8a` in the commit that
  creates the project.
- Real window: `Phase8aRealWindowPass.cs` with `.<Part>.cs`, `RealApp.Phase8a.cs`, the csproj link
  `..\VisualCommit.VisualTests.Phase8a*\Phase8aExpectations*.cs` as `Phase8a\%(Filename)%(Extension)`;
  `Phase8aPerformance.cs` (§10).
- Parts avoid C, O, Q, R and T (D230): **A** About and notices, **B** the icon, **F** first run, **G**
  the guide's links, **I** single instance, **K** keyboard, **L** the contrast round, **U** screen
  readers. Pictures match `Phase8aExpectations.PictureName`,
  `^[abfgiklu]\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`.
- Branches `wip/p8a-<key>` from `phase/8a-release-readiness`, worktrees `D:/Projects/Personal/vc-wt/p8a-<key>`.
  No new port (ruling 20). The app's code never names a phase.

### 0.4 Work packages, waves and merges

| Package | § | Wave | Depends on | Owns |
|---|---|---|---|---|
| WP0, contracts and stubs (the lead) | 1 | 0 | none | 1.16's list |
| colours: the contrast round | 2 | 1 | WP0 | `Tokens.axaml`; added styles in `LeftPanelView`, `RightPanelView`, `CompareView`, `WorkingChangesView`, `FileHistoryView` (`.axaml`); `ThemeTokensTests.cs`, `PanelViewTests.cs`; ruling 28's files (2.4: four App.Tests files, `Phase1Expectations.cs` and the `Phase3` to `Phase7b` `Expectations.Common.cs`, `Phase7bExpectations.A.cs`, `Phase6SupportTests.cs`, `Phase6ExpectationTests.L.cs`, `Phase6Checks.L.Overlays.cs`, the HEAD helpers of phases 3 to 5, `Phase5Checks.F.Filters.cs`); `scripts/scan-contrast.py`, `compare-pictures.py`; part L |
| floor | 3 | 1 | WP0 | `GitVersion.cs`, `IGitRepository.cs`; `GitRepository.Branches.cs`, `.Search.cs`, `GitRepository.cs`; D277's code in `AskPassBroker.cs`, `AccountBlockViewModel.cs`, `AccountsSettingsViewModel.cs`, `OperationTexts.Hosting.Accounts.cs`; `OperationTexts.cs`; `RepositoryOperations.Branches.cs`; `TempRepo.cs`, `GitWords.cs`; 3.4's test files; `ci.yml` |
| single: one instance per data folder | 4 | 1 | WP0 | `AppPaths.cs`, `SingleInstanceGuard.cs`, `AppLaunch.cs`, `Program.cs`, `App.axaml.cs`, `MainWindow.axaml.cs`, `InstanceNoticeWindow.axaml(.cs)`, `InstanceNoticeViewModel.cs`, `InstanceNoticeTexts.cs`; `GitIsolation.cs:74`'s comment; `AppPathsTests.cs` and 4.5's classes; part I with `InstanceNoticeHost` |
| firstrun | 5 | 2 | §3 | `GitLocator.cs`, `GitSetupProbe.cs`, `OsRelease.cs`, `GitConfiguration.cs`, the helper in `AskPassBroker.cs`; `GitAccess.cs`, `FileLauncher.cs`; `MainWindowViewModel.cs`, `.Settings.cs`, `.Git.cs`, `RepoTabViewModel.cs`, `WelcomeViewModel.cs`, `ViewModels/GitSetup/`, `GitProgramViewModel.cs`, `AccountsSettingsViewModel.cs`; `GitSetupView`, `CommitGraphView.axaml(.cs)`, `StatusBarView.axaml`, `SettingsDialogView.axaml`, `SettingsDialogViewModel.cs`; `tests/VisualCommit.GitStandIn`, `FakeGitSetupProbe.cs`; 5.4's test files; part F |
| keyboard | 6 | 2 | §2 | `DiffTextView.cs`, `DiffPane.cs`, `HunkHeaders.cs`, `DiffView.axaml.cs`, `BlameTextEditor.cs`, `BlameView.axaml.cs`, `AppCommands.Palette.cs`, `WorkingChangesView.axaml(.cs)`, `WorkingChangesViewModel.cs`, `WelcomeView.axaml.cs`, `CreatePullRequestDialogView.axaml.cs`, `FocusReturn.cs`; `MainWindow.axaml.cs` (D427); `ActivityLogView.axaml` only if 6.3.8's test fails; `HeldGitRunner.cs`; 6.4's test files, `Phase2Checks.Helpers.cs`; part K |
| icon | 7 | 2 | §4 | `tools/VisualCommit.IconBuilder`, `assets/brand/`, `Assets/Icon/`, `AppIcon.cs`, `VisualCommit.App.csproj`, `MainWindow.axaml`, the notice window's `Icon`, `Directory.Packages.props`; `IconFile.cs`; part B |
| about: About and notices | 8 | 3 | §5, §7 | `THIRD-PARTY-NOTICES.md`, `notices/`, `EmbeddedTexts.cs`, `OperationTexts.About.cs`, the About section, `SettingsDialogView.axaml(.cs)`, `SettingsDialogViewModel.cs`, `SettingsSection.About`, the text dialog, `VcLinkButton` (`Theme/Controls.axaml`, `SignInDialogView.axaml`), `Theme/Dialogs.axaml`, `App.axaml(.cs)`, `AppCommands.cs`, the csproj, `ci.yml`; 8.4's earlier files; part A |
| uia: screen readers | 9 | 3 | §5, §6; merges after §8 | `CommitGraphControl.cs`, `CommitGraphControl.Automation.cs`, `GraphRowSpeech.cs`, `AutomationNames.cs`, the names in ten views (9.2), `AutomationSweep.cs`, `ShellDriver.Phase8a.Uia.cs`, `CommitGraphControlTests.cs`, `StatusBarTests.cs`; part U |
| perf | 10 | 3 | WP0 | `StartupTimeline.cs`, `RepositoryViewModel.cs`'s lines, `RealApp.cs`'s helpers, `Phase8aPerformance.cs`, `PerformanceSeries*.cs`; 10.4's test files |
| guide | 11 | 4 | waves 1 to 3 | `docs/guide/`, `guide.yml`, `scripts/check-guide-links.py`, `tests/VisualCommit.GuideShots`, `AppShortcuts.cs`, `AppCommands.cs`'s guide case, `.gitignore`, `ci.yml`'s build line; 11.4's test files; part G |
| realwindow | 12 | 4 | every part; the review's fixes | `Phase8aRealWindowPass*.cs`, `RealApp.Phase8a.cs`, `RealApp.cs`'s `StartInfoFor`, the csproj |

**Waves and merges.** Wave 0: WP0 in the main checkout. Wave 1: §2, §3, §4, any order; §2 is the
first package that changes an earlier picture (ruling 13). Wave 2: §5, §6, §7, any order. Wave 3: §8,
§9 and §10, built together; §9 merges after §8 and sweeps About at its merge. Wave 4: §11; the
review before the gate (status step 15) and its fixers; §12, built on the Windows machine at step
14, reviewed at step 15, merges last, after the review's fixes. At most three packages are built at
once; one owner per file and test class, and a file passes only at a merge. No two packages of one
wave own a file. The files that pass between packages, in this order: `AppCommands.Palette.cs` (WP0,
§6); `AppCommands.cs` (§8, §11); `App.axaml.cs` (WP0, §4, §8); `Program.cs`, `AppLaunch.cs` (WP0,
§4); `GitIsolation.cs` (WP0, §4); `AskPassBroker.cs`, `AccountsSettingsViewModel.cs`,
`MainWindowViewModelTests.cs` (§3, §5); `MainWindowViewModel.cs` and `.Settings.cs` (WP0, §5);
`SettingsDialogViewModel.cs` (WP0, §5, §8); `SettingsDialogView.axaml` (§5, §8);
`SettingsDialogViewTests.cs` (§5, §8, §11); `WorkingChangesView.axaml` (§2, §6, §9);
`LeftPanelView.axaml`, `CompareView.axaml` (§2, §9); `StatusBarView.axaml`, `CommitGraphView.axaml`
(§5, §9); `StatusBarTests.cs` (§6, §9); `MainWindow.axaml.cs` (§4, §6); `MainWindow.axaml` (§7, §9);
`ActivityLogView.axaml` (§6 if needed, §9); the notice window (§4, §7); `App.csproj` (§7, §8);
`RepositoryViewModel.cs` (WP0, §10); `RealApp.cs` (§10, §12); `ci.yml` (§3, §8, §11);
`Phase7aExpectations.A.cs` (§3, §8); `Phase7bExpectations.A.cs` (§2, §8); `Phase6Expectations.K.cs`,
`Phase6ExpectationTests.K.cs`, `PaletteEntriesTests.cs` (§8, §11); `AppShortcutsTests.cs` (WP0,
§11). A package adds `ShellDriver` helpers only in a new partial `ShellDriver.Phase8a.<Key>.cs`. The
lead's throughout: `AppSession.cs`, `TabServices.cs` (but §8's `SettingsSection.About`),
`PromptDialogs.cs` after WP0, `VisualCommit.slnx` (each new project's line at its merge),
`Directory.Build.props`, `CLAUDE.md`, `README.md`, `requirements.md`, `plan.md`, `status.md`,
`architecture.md`. Every other doc a merge needs is the lead's, at that merge.

At §3's merge the lead removes the broker's git version (D420): the `gitVersion` parameter of both
`AskPassBroker` constructors and of `PromptDialogs.CreateBroker`, and the argument in
`AppSession.Start` (`AppSession.cs:254`). If §6's G8 test shows a keyboard move of the activity log's
splitter unsaved (D427), its one line in `CommitGraphView.axaml.cs`, §5's in wave 2, goes to a fixer
after wave 2's merges, with §6's test.

No agent that runs a process works beside a heavy run (ruling 23): a wave's lagged list and whole
default run (D245) run after its last merge, with no builder working; the next wave starts after
them. §2's merge is read against phase 7b's references first (0.7). After wave 3, the lead runs
§10's "before" series on the rested laptop; a change it calls for is a fix package after a ruling
(ruling 66), with no section. §3's fixes are read on CI's floor job after each push of the phase
branch; the commit that makes the job green removes `continue-on-error` and moves `Minimum`
(rulings 10, 71). After the gate commit, `docs/guide/**` is docs; `guide.yml`, the shots and the
links are code, so they come before the counted round; from §8's merge the notices and `LICENSE`
count as code (D433).

**The lagged list** (D262), at each wave's last merge and at the merge of §5, §6, §9 and §10, in Git
Bash, one command at a time, before the wave's whole run: phase 7b's list (its design 0.4: 7a's list
with `GitRepositoryProviderTests`, 7b's walk-through and the six Hosting.Tests classes), the App.Tests
classes 8a extends with real git, and 8a's project once it has checks:

```
export VISUALCOMMIT_WATCHER_LAG_MS=1500
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests --filter-class "*Phase2Checks"
bash scripts/heavy-run.sh --project tests/VisualCommit.App.Tests --filter-class "*StageListRefreshTests" --filter-class "*RepositoryOperationsProtectTests" --filter-class "*CompareViewTests" --filter-class "*SearchBarTests" --filter-class "*UndoStepTests" --filter-class "*RepositoryOperationsFetchTests" --filter-class "*GitCallBudgetTests" --filter-class "*GitRepositoryProviderTests" --filter-class "*DiffTextViewTests" --filter-class "*WorkingChangesTests" --filter-class "*StatusBarTests"
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase6 --filter-method "*Phase6ChecksS.*" --filter-method "*Check_K10_*" --filter-method "*Check_K13_*" --filter-method "*Check_W1_*" --filter-method "*Check_W8_*"
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase7a
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase7b
bash scripts/heavy-run.sh --project tests/VisualCommit.Hosting.Tests --filter-class "*RepositoryHostingWiringTests" --filter-class "*SignInEntryTests" --filter-class "*WelcomeCloneFromGitHubTests" --filter-class "*PaletteCloneFromGitHubTests" --filter-class "*PullRequestOperationsTests" --filter-class "*CreatePullRequestOperationTests" --filter-class "*RepositoryHostingProvidersWiringTests" --filter-class "*SignInEntryProvidersTests" --filter-class "*CloneFromAccountTests" --filter-class "*RequestOperationsProvidersTests" --filter-class "*CreateRequestOperationProvidersTests" --filter-class "*SignInBindingsWindowTests"
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase8a
unset VISUALCOMMIT_WATCHER_LAG_MS
```

A failure under the lag is the fault R8 imitates: it gets a fix, never a looser test.

**Progress steps** (status.md): 4 WP0; 5 §3; 6 §5; 7 §2; 8 §6 and §9; 9 §8; 10 §7 and the owner's
choice; 11 §10 and the series; 12 §11 (Pages at the merge); 13 §4; 14 §12 and the lagged runs; 15
the review; 16 and 17 the gate.

### 0.5 The visual checks' parts

Each part's checks are written by one writer and one reviewer and committed just before its package
(D244), each marked for the real window or not; theme dark and 1100×700 unless a check names
another. One project, about 45 checks, each method under about 40 s alone (timed with
`bash scripts/time-scripted-check.sh "<method>" 8a`); past 8 minutes alone the lead splits part L into
a project of its own, as D230 did for phase 6.

| Part | § | Checks | Real window repeats |
|---|---|---|---|
| L contrast | 2 | 6: placeholders dark and light and in every other box, danger text and the danger button, the accent on rows | L1, L2 placeholders; L4 a the danger item by keyboard; L5 dark the danger button; L6 a to c HEAD's row |
| I single instance | 4 | 3: I1 a second start (real window only), I2 the notice dark and its Try again, I3 light | I1 hands over; I2 the notice, exit code 2, and Try again starting the app |
| F first run | 5 | 14 (F6b inside F6): firstrun.md's F1 to F13 by rulings 41 and 44, F14 the netrc note | F1 with F2's confirm and F13's keys; F4 through the native Open dialog; F5, F6 (stand-in); F6b (real git) |
| K keyboard | 6 | 8: one per keyboard.md fix and the walks to Cancel and a splitter | K1 Tab into a hunk bar and Enter; K2 arrows pick a blame run |
| B icon | 7 | 2: the mark at its sizes on dark and light | B1 the executable and the title bar; B2 the light title bar and the taskbar button |
| A About | 8 | 6: the section dark, light and 1920×1080, the license, the notices, the palette entry | A1 the section with the version's automation name; A4 the license dialog |
| U screen readers | 9 | 3: the row's name and hint, the sweep, the names | U1 the row's events and name; U3 the named buttons and splitters |
| G guide | 11 | 3: the palette row with F1, the first-run links, About's link | none (no real window opens a browser) |

Every new screen has a real-window picture: the first-run screen (F1, F4, F5, F6), its winget
confirm (F2), the notice (I2), About (A1), the text dialog (A4), the icon in the title bar and the
taskbar (B1, B2).

Checks by hand, in the report: the Narrator walk-through (D411, the owner, about 20 minutes, §9's
steps); phase 6's System step (D224); a second start from the taskbar while the app is behind
another window (§4); the guide's pictures from the gate commit's `guide-site` artifact (ruling 61:
both themes, search, no outside host); after the merge, the live site and the environment's branch
rule (ruling 64). macOS's (About in the application menu, the Command Line Tools, a second start)
go to 8b's Mac checklist.

### 0.6 Cost in time

- **Default run** (1 h 31 min 48 s at 7b's gate, 7,892 tests, four projects at once; App.Tests alone
  33 min 35 s on 2026-11-19, 28 min 24 s at 7b's gate). 8a's project: the parts' own estimates (L
  about 4 minutes alone, F and K 3 to 4 each, A about 4, U 3 to 5, I, B and G about 1 together)
  come to 15 to 20 minutes alone, a thirteenth project, about as 7b's walk-through's 15 min 33 s of
  its own. App.Tests gains about 220 tests that start no session (3 to 5 minutes of its own, which
  add to the run's end, D416); Git.Tests about 50 (one or two minutes). **+12 to +20 minutes, about
  1 h 44 min to 1 h 52 min**, an estimate; wave 1's whole run measures it.
- **Real-window pass** (33 min 35 s, 77 tests): 13 launches (L 2, A with B 1, I 3, F 4, K 2, U 1,
  D442) and I1's second process, 25 to 60 s each: **+9 to 11 minutes, about 43 to 45**. D246's two
  runs of 8a's part about 10 to 12 minutes each.
- **The perf series** (D436, D437): one run of the class at five runs takes 12 to 18 minutes of the
  laptop with the desktop, plus a rest of 10 minutes, nothing beside (ruling 23); a change adds the
  alternated runs, 40 to 60 minutes.
- **CI** (D377's limits 70 for the Test steps, 14 for the containers; the floor job's own 25 and 35,
  ruling 74). At 7b's gate: Windows 23 min 41 s (worst 41 min 6 s), macOS 33 min 9 s (worst 46 min
  21 s), Linux 6 min 23 s, containers 5 min 28 s and 5 min 51 s; the floor job's Test step 7 min 13
  s. With +10 to 15 %: macOS's worst 51 to 53 minutes, under 56; the containers' under 7, under 11
  min 12 s; the floor's under 9, under 20. No limit is raised ahead of need (ruling 21); the lead
  reads every merge's run against four fifths of each limit. A slow day of Ubuntu's mirror can time
  out the three container jobs: they are run again (ruling 74). Windows' job builds `GuideShots` with
  the opt-in projects (about a minute). The guide's workflow is separate: about 5 to 8 minutes per
  push of code, unverified until its first run.
- **Live command** (D335, ruling 2): about 2 min 30 s per whole run, twice at the gate.
- **Sum at the gate**: the default run about 1 h 50 min, the pass about 44 minutes, D246's two runs
  about 11 each, the live command twice; about 1,700 files inspected in about 12 batches (D247).

### 0.7 Earlier pictures

Ruling 3: nothing changes but what rulings 3, 13, 40, 54 and 62 name. Counted in phase 7b's counted
round (1,150 scripted, 200 real-window captures, 3 framed captures, 198 difference pictures):

| Change | Ruling | Pictures | Region |
|---|---|---|---|
| The contrast round (§2) | 13, D410 | 1,333 of 1,350: 1,137 scripted, 196 captures; placeholder 1,293, dark danger 360, danger button 41, accent on a row 13 (`research2/colours-*.txt`); 17 unchanged (`colours-none.txt`) | the scan's boxes (`research2/scan/part-*/scan.json`) |
| The window's icon (§7) | 3 | the 3 framed captures (phase 0 `A`, `B`; phase 6 `s15`) | the title bar |
| The Git program row (§5) | 40 | 9 scripted: `s14c`, `s14i`, `s18a`, `s18d`, `s19a`, `s19d`, `s20b`, `l31e-dark`, `l31e-light` | the global scope's bottom |
| The About nav button (§8) | 3, 14, D409 | 83 scripted, 8 captures | x 141–317, y 255–285 at 1100×700 (x 551–727, y 415–445 at 1920×1080) |
| Two palette commands (§8, §11) | 54 | 4 scripted (`k10a`, `k10f`, `l31c` dark, light), 2 captures; K10's constants | the scroll thumb, x 837–838 |
| The shortcuts list's "Help" group (§11) | 62 | `s14f-shortcuts-end`; `s14e-shortcuts-top` in the section's scroll bar only, if at all (§11's builder measures it) | the list's end; the scroll bar |

Every one but the framed captures is in the 1,333, so about 1,336 of 1,353 earlier files change. Every
other package proves "none": its run's scripted pictures against the current references.

How (D419): §2's comparison runs phase 7b's references
(`D:/Projects/Personal/phase7b-work/gate/round-3dc0887/visual/`, ruling 22) against wave 1's whole
run, with the scan's boxes as the expected regions (ruling 30): a difference outside them is a
finding. The run's scripted pictures, once that comparison passes, are the new references
(`D:/Projects/Personal/phase8-work/references/wave1/`); every later merge and the gate compare
against them with `scripts/compare-pictures.py --expected <file>`, whose file maps
`phase-N/<folder>/<name>.png`, or an `fnmatch` pattern, to boxes `[left, top, right, bottom]` (2.6):
the lead writes one for the regions above. The real-window captures are compared at the gate with
phase 7b's, expecting the scan's lists and the rows above. The report replaces plan.md's 35 label
pictures by the scan's lists (ruling 31) and states every changed expectation once under "Changes
to expected results" (ruling 28). Every picture is inspected (D247).

### 0.8 What the phase asks of the owner

13.3 lists it by when: now, to look at the rulings and D416 to D446; beside the build, the icon;
the spike's and the series' takeovers of the desktop, told first; before the counted round, the
chosen mark or the placeholder; before the gate, the three live tokens and D246's two runs; at the
gate, the Narrator walk-through, phase 6's System step and a second start from the taskbar; at
"Phase 8a accepted", GitHub Pages' source (ruling 64, D408).

## 1. Shared contracts (WP0's brief)

Every type, member, seam, variable and file that more than one package uses, each in its one final
form. WP0, the lead's, writes what 1.16 lists before wave 1, in the main checkout, with no change of
behaviour or picture; each contract names the package that owns its real implementation afterwards.
A package may add members to a file it owns, but never changes a signature below without the lead,
and an interface gains a member only with a default body, so that no fake changes. The sections cite
these contracts and do not repeat them.

Rules for every package:
- No earlier picture changes but what 0.7 names (ruling 3); a package that finds it must change
  another stops and asks the lead. Each section says which earlier pictures it changes, or "none",
  and how its builder proves it (its classes' run and `scripts/compare-pictures.py` against the
  current references).
- On screen the American spelling ("License", "color"); the docs keep "licence" (ruling 58), but
  `THIRD-PARTY-NOTICES.md` and the guide (D434). Every text the app shows is a constant of one texts
  class per area, which the tests read.
- The UI thread never waits on a process, a file or the network (Q2). No test reads the machine's
  `PATH`, home folder, git or settings: every new variable the app reads is in 1.14 and in
  `GitIsolation.MachineIndependence`.
- No real person's data: made-up authors at `example.com` and `/home/demo` (ruling 63); the owner's
  name only where D409 and `LICENSE` put it, never an e-mail address.

### 1.1 Projects

| Project | Made by | References | In `VisualCommit.slnx` |
|---|---|---|---|
| `tests/VisualCommit.VisualTests.Phase8a` (csproj and `AssemblyInfo.cs` as `.Phase7b`'s) | WP0 | App, Core, Git, Hosting, Testing | yes (WP0) |
| `tests/VisualCommit.GitStandIn`: a console program `visualcommit-git-stand-in(.exe)`, framework-dependent, no package (1.14) | §5 | none; referenced by `VisualCommit.Git.Tests` (§5) and `VisualCommit.RealWindowTests` (§12) | yes (the lead, at §5's merge) |
| `tools/VisualCommit.IconBuilder`: renders the icon's files (D428); never shipped, never run by a test or by CI | §7 | Svg.Skia 4.9.1, SkiaSharp 3.119.4, SkiaSharp.NativeAssets.Linux 3.119.4, HarfBuzzSharp.NativeAssets.Linux 8.3.1.3: new pins in a group "Tools" of `Directory.Packages.props`, the versions Avalonia.Skia 12.1.3 resolves (web-verify B2); no transitive pinning, so no other project's graph changes | yes (the lead, at §7's merge, in a `/tools/` folder), so CI builds it |
| `tests/VisualCommit.GuideShots`: the guide's pictures (ruling 61); `net10.0`, so `guide.yml` runs it on Linux | §11 | App, Core, Git, Hosting, Testing | no; CI's Windows job builds it with the opt-in projects, `guide.yml` runs it |
| `tests/VisualCommit.RealWindowTests` | phase 0 | gains the stand-in (§12) | no, as today |

No other package reference is added; the tool's never reaches the app's `VisualCommit.deps.json`,
which §8's notices test reads. The app's build output gains no file: the icon (§7) and the texts
(§8) are embedded in its assemblies.

### 1.2 Git detection, its seam and "Check again"

`src/VisualCommit.Core/Git/IGitRunner.cs` (WP0; then §5). `GitAvailability` keeps its four values;
the positional constructor and its test calls are unchanged.

```csharp
public enum GitSource { Path, InstallFolder, Setting, StandIn }    // new; InstallFolder also macOS's and Linux's usual folders
public sealed record GitDetection(GitAvailability Availability, string? ExecutablePath, GitVersion? Version, IGitRunner? Runner)
{
    public GitSource Source { get; init; }               // where ExecutablePath came from
    public string? ErrorText { get; init; }              // Broken: git's standard error, else its standard output, at most 4,000 characters; or why it could not start
    public int? ExitCode { get; init; }                  // Broken: git's exit code; null when it did not start
    public GitConfigFault? ConfigFault { get; init; }    // Broken with "fatal: bad config line <n> in file <f>" (plan 767)
    public string? ChosenMissing { get; init; }          // NotFound: the setting names this path and no file is there (ruling 41)
    public string? PathCandidate { get; init; }          // with ChosenMissing: the git the machine's search finds, which "Use the Git on the PATH" names; null for none
    public bool CommandLineToolsMissing { get; init; }   // macOS: /usr/bin/git left out because `xcode-select -p` failed
}
public sealed record GitConfigFault(string File, int Line);       // new
```

`src/VisualCommit.Core/Git/GitSetup.cs` (new; WP0 whole):

```csharp
public sealed record GitLookup(string? ChosenPath, string? StandIn);   // AppSettings.GitExecutable; VISUALCOMMIT_GIT_STAND_IN as read at the start
public enum GuidancePlatform { Windows, MacOS, Linux }
public sealed record LinuxDistribution(string Id, IReadOnlyList<string> IdLike, string? PrettyName);   // os-release's ID, ID_LIKE, PRETTY_NAME
public sealed record GitGuidance(GuidancePlatform Platform, string? WingetPath, LinuxDistribution? Distribution);
public interface IGitSetupProbe
{
    // Ruling 4's order: the setting (used whatever it answers; no file: ChosenMissing, no fallback), else the
    // stand-in when StandIn is set, else PATH and then the install folders.
    Task<GitDetection> DetectAsync(GitLookup lookup, IGitCallLog? callLog, IAppLog log, TimeProvider? recordClock,
        IReadOnlyDictionary<string, string?>? environment, CancellationToken cancellationToken);
    GitGuidance ReadGuidance();   // read only while the first-run screen shows (ruling 48); nothing extra runs at a start with a working git
}
public enum GitSetupState { Searching, Found, Missing, ChosenMissing, TooOld, NotWorking, HelperMissing }
public static class GitSetupStates
{
    // Git's states first; HelperMissing only with git Found (ruling 9). Null git: Searching.
    public static GitSetupState Of(GitDetection? git, string? missingHelperPath);
}
```

`src/VisualCommit.Git/GitLocator.cs` (WP0 the constant and the overload, which calls today's search
with the setting and the stand-in ignored; §5 the body). `Using Git {version} at {path}.` stays word
for word: the real-window pass waits on it.

```csharp
public const string StandInVariable = "VISUALCOMMIT_GIT_STAND_IN";
public static Task<GitDetection> DetectAsync(GitLookup lookup, IGitCallLog? callLog = null, IAppLog? log = null,
    TimeProvider? recordClock = null, IReadOnlyDictionary<string, string?>? environment = null, CancellationToken cancellationToken = default);
// Today's overload keeps its signature and calls this one with new GitLookup(null, null).
```

`src/VisualCommit.Git/GitSetupProbe.cs` (new; WP0 a stub over the overload above, its guidance the
platform's with no winget and no distribution; §5 the real one with `OsRelease.Parse`):
`public sealed class MachineGitSetupProbe : IGitSetupProbe { public static MachineGitSetupProbe Instance { get; } }`.

`src/VisualCommit.App/Services/GitAccess.cs` (WP0 the surface with today's behaviour; §5 the second
and later searches):

```csharp
public sealed class GitAccess
{
    public const string GitNotWorking = "Git is not working on this computer.";   // unchanged
    public GitAccess(Task<GitDetection> detection);        // unchanged: one fixed search (GitRepositoryProviderTests); ProbeAsync answers NotFound, ReadGuidance the running platform's
    public GitAccess(IGitSetupProbe probe, Func<GitLookup> lookup, IGitCallLog? callLog, IAppLog log,
        TimeProvider? recordClock, IReadOnlyDictionary<string, string?>? environment);   // new: the session's
    public Task<GitDetection> Detection { get; }           // the latest search, pending until it ends
    public GitDetection? Known { get; }                    // the last search that ended; null before the first
    public event EventHandler? Changed;                    // after each search ends, on the thread that ended it
    public Task<GitDetection> DetectAsync(CancellationToken cancellationToken = default);
    public Task<IGitRunner> GetRunnerAsync(CancellationToken cancellationToken = default);   // the detection's Runner, as today
    public Task<GitDetection> ProbeAsync(string path, CancellationToken cancellationToken);  // the probe with new GitLookup(path, null) and the session's call log, log, clock and environment; changes nothing
    public GitGuidance ReadGuidance();                     // the probe's
}
```

WP0: the first `DetectAsync` searches; every later call returns that search. §5: a later call while
none runs swaps in a new pending `Detection` first, so `GetRunnerAsync` meanwhile waits for the new
search; a call during a search joins it. `GetRunnerAsync`'s three texts stay; the too-old text reads
`GitVersion.Minimum`, never a literal (2.34.1 after §3, ruling 69); §5 adds ChosenMissing's. A tab's
git calls take the detection's `Runner` through `GetRunnerAsync` (`GitRepositoryProvider.cs:57-81`),
which §6's `HeldGitRunner` relies on.

`MainWindowViewModel`'s constructor gains a last parameter `GitAccess? gitAccess = null` (WP0),
which `AppSession.Start` passes. **"Check again"** (§5, all new, in a new partial
`MainWindowViewModel.Git.cs`):

```csharp
public IAsyncRelayCommand CheckGitAgainCommand { get; }   // disabled while a search runs; its button reads "Checking…"
public string? GitStatusToolTip { get; }                  // the git in use and where it came from; null while there is none
public GitSetupViewModel GitSetup { get; }                // new class: the first-run screen's state and texts, one per window
```

Its order (5.3) ends with every tab whose restore failed reopened (`RepoTabViewModel.ReopenAsync`,
new in §5); nothing else holds a runner. A git program changed in Settings while git works applies at
the next start (ruling 42). `RepoTabViewModel` gains a settable `GitSetupViewModel? GitSetup`, which
the shell's `AddTab` sets.

The first-run screen's automation ids, which §9, §11 and §12 use (§5 builds them): `GitSetup` (the
view), `GitSetupTitle`, `GitSetupBody`, `GitSetupPath`, `GitSetupGitSaid` (git's own words),
`GitSetupGuidance`, `GitSetupCommand`, `GitSetupWingetButton`, `GitSetupDownloadButton`,
`GitSetupCltButton`, `GitSetupCopyButton`, `GitSetupShowFileButton`, `GitSetupChooseButton`,
`GitSetupUsePathButton`, `GitSetupCheckAgainButton`, `GitSetupContinueButton`, `GitSetupError`,
`GitSetupMoreLink`, `GitSetupGuideLink`; the status bar's `GitStatus` (unchanged) and `GitSetupButton`;
the settings row's `GitProgramPath`, `GitProgramNote`, `GitProgramStatus`, `GitProgramBrowseButton`,
`GitProgramUsePathButton`; the Accounts section's `AccountsNetrcNote` and `AccountsNetrcDismiss`.

Prompts and git's settings (WP0 the default members; §5 the bodies in `AskPassBroker` and
`GitConfiguration`):

```csharp
// src/VisualCommit.Core/Operations/GitPrompts.cs, IGitPromptBroker
string? MissingHelperPath => null;   // the path the helper was looked for at, while it is missing
string? CheckHelper() => null;       // looks again; returns MissingHelperPath
// src/VisualCommit.Core/Git/GitConfiguration.cs, IGitConfiguration
void ForgetToolLookup() { }          // drops a finished merge-tool lookup, so the next asks the new git
// src/VisualCommit.App/Services/PromptDialogs.cs: CreateBroker gains `string? helperPath = null`, passed to AskPassBroker
```

The broker's `gitVersion` (`AskPassBroker`'s two constructors, `CreateBroker`, `AppSession.cs:254`)
goes at §3's merge, by the lead (D420).

### 1.3 The launcher's start call

```csharp
// src/VisualCommit.App/Services/IFileLauncher.cs (WP0 the member; FileLauncher's body §5, WP0's throws NotSupportedException)
// Starts command's program off the UI thread, not waited for; FileNotFoundException when the program is not
// there, the start's own exception when it cannot start.
Task StartAsync(LaunchCommand command, CancellationToken cancellationToken = default);
// src/VisualCommit.App/Services/FileLauncher.cs, LaunchCommand (WP0)
public bool InNewTerminal { get; init; }   // Windows: in a console window of its own (UseShellExecute); ignored elsewhere
// tests/VisualCommit.Testing/Headless/FakeFileLauncher.cs (WP0 whole)
// FileLaunchKind.Start; IReadOnlyList<LaunchCommand> Starts; Exception? StartFailure { get; set; }
```

The two starts (§5): on Windows with git missing and winget found (ruling 44), `new LaunchCommand("cmd.exe", []) { CommandLine = "/K winget install --id Git.Git -e --source winget", InNewTerminal = true }`,
after a confirm that shows `winget install --id Git.Git -e --source winget`; on macOS with the tools
missing (ruling 45), `new LaunchCommand("/usr/bin/xcode-select", ["--install"])`, without the app's
confirm. No check runs either: both harnesses hand them to the fake, which asserts the command
(ruling 6). Unverified: that `cmd.exe /K` started so shows a window that stays where Windows
Terminal is the default console, and that `File.Exists` finds winget's app execution alias; the lead
tries `/K echo` by hand once before §5's merge and notes whether the laptop has `winget.exe` on its
`PATH` (F1's real-window picture shows the button only then, 13.4).

### 1.4 The settings key for the git program

```csharp
// src/VisualCommit.Core/Settings/AppSettings.cs (WP0; written by §5)
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? GitExecutable { get; init; }   // "gitExecutable": the git chosen in Settings (ruling 4); absent unless set (ruling 40)
```

Absent, phase 6's part S still finds its nine properties. Only a git that is found and new enough is
saved; "Use the Git on the PATH" removes it.

### 1.5 The theme tokens (§2, D418)

`src/VisualCommit.App/Theme/Tokens.axaml`, the contrast round's alone; WP0 adds nothing.

| Key | Dark | Light | Ruling |
|---|---|---|---|
| `VcTextPlaceholderBrush` (new) | `#8F97A4` (4.75 on the box `#272C36`) | `#68707D` (5.00 on `#FFFFFF`) | 24 |
| `TextControlPlaceholderForeground`, `…PointerOver`, `…Focused` | `#8F97A4` (was `#9BA3B0`) | `#68707D` (was `#5C6572`) | 24 |
| `TextControlPlaceholderOpacity` (new `x:Double`, once, outside the theme dictionaries) | 1 | 1 | 24 |
| `VcDangerBrush` | `#EA7D86` (was `#E5606B`): 4.55 hover, 4.70 selection, 5.18 control | `#B7323D`, kept: 4.57 hover | 25 |
| `VcDangerTextBrush` on the danger | `#10121A` (was `#FFFFFF`): 6.92 | `#FFFFFF`, kept: 5.94 | 25 |
| `VcAccentOnRowBrush` (new), only in styles of a hovered or selected row | `#8B9AFF`: 4.78 hover, 4.94 selection | `#4353D8`: 4.66 hover | 26 |
| `VcLane3Brush`, `VcAccentBrush` | `#E5606B`, `#7C8CFF`, kept: accent 4.70 on control, never text on hover or selection | kept: accent 5.17 on chrome | 25, 26 |

The order on a text box stays disabled, placeholder, secondary, primary; a disabled box keeps
Fluent's background (ruling 29). New views (§4, §5, §8) use these tokens and set no colour of their
own; accent text on a hovered or selected background uses `VcAccentOnRowBrush`.

### 1.6 The app's identity, version and icon

`src/VisualCommit.App/Services/AppIdentity.cs` (new; WP0 whole; About shows it, §8):

```csharp
public sealed record AppVersionInfo(string Version, string? Commit)
{
    public string? ShortCommit { get; }   // the commit's first 7 characters
    public string Display { get; }        // "0.1.0 (3dc0887)"; "0.1.0" without a commit
    // "0.1.0+<7 to 40 hexadecimal digits>" gives the commit; any other part after '+' is dropped; null gives "0.0.0".
    public static AppVersionInfo Parse(string? informationalVersion);
}
public sealed record AppIdentity(string Name, string Creator, AppVersionInfo Version, string Copyright)
{
    public const string CreatorName = "Rahim Khajei";
    public static AppIdentity FromAssembly(Assembly assembly);   // AssemblyProduct, CreatorName, AssemblyInformationalVersion, AssemblyCopyright
}
```

`AppSession.AppVersion` stays for the start-up line and the User-Agent. Every headless start gets
`TestIdentity.Pinned` (1.15): `0.1.0 (1a2b3c4)`, so no scripted picture changes from one commit to
the next (ruling 59). The real window shows the built commit, seven characters apart from its twin.

`src/VisualCommit.App/Services/AppIcon.cs` (new; §7), named so as not to meet the glyph control
`VisualCommit.App.Controls.Icon`:

```csharp
public static class AppIcon
{
    public const string WindowIconUri = "avares://VisualCommit/Assets/Icon/VisualCommit.ico";   // MainWindow and the notice window (§7)
    public const string Image48Uri = "avares://VisualCommit/Assets/Icon/VisualCommit-48.png";
    public const string Image96Uri = "avares://VisualCommit/Assets/Icon/VisualCommit-96.png";   // About's image, drawn at 48×48 (§8)
}
```

### 1.7 The app's links

`src/VisualCommit.App/Services/AppLinks.cs` (new; WP0 whole; §11 makes every page and anchor exist):

```csharp
public static class AppLinks
{
    public const string GuideBase = "https://rahimpasha.github.io/VisualCommit/";
    public static Uri Repository { get; } = new("https://github.com/RahimPasha/VisualCommit");
    public static Uri Guide(GuidePage page);   // GuideBase + the page's path; no query string
}
public enum GuidePage { Start, FirstRun, GitMissing, GitChosenMissing, GitTooOld, GitNotWorking, HelperMissing, Shortcuts }
```

Paths: `""`, `first-run.html`, `troubleshooting/git.html#git-missing`, `#git-chosen-missing`,
`#git-too-old`, `#git-not-working`, `#helper-missing`, `reference/shortcuts.html`. Every link opens
through `IFileLauncher.OpenUrlAsync`; the site answers "not found" until Pages is turned on at the
merge (ruling 64). About's guide link (§8) and the palette's command (§11) open `Guide(GuidePage.Start)`;
the first-run screen's link its state's page (D417).

### 1.8 The palette's new commands and F1 (D417)

`ViewModels/Commands/AppCommand.cs` (WP0): two values after `CloneFromAzureDevOps`, before the
keys-only values, so that only those move:

```csharp
About,           // "About VisualCommit…", no key: the settings screen on About (§8)
OpenUserGuide,   // "Open the user guide", F1: AppLinks.Guide(GuidePage.Start) through the launcher (§11)
```

`AppCommands.Palette.cs` (WP0): `Add(AppCommand.About)` and then `Add(AppCommand.OpenUserGuide)`
after `FollowSystemTheme` (ruling 54). Until §8 and §11 add their `case` to `AppCommands.Resolve`, its
`default` keeps both unlisted, so WP0 changes no palette.

§6: first in `PaletteCommands()`, before `RepositoryRows`, on a repository tab while its
`OperationStatus` has `CanCancel` and a `RunningTitle`: `new PaletteEntry(DashboardTexts.CancelTitle(title),
snapshot.RepositoryDetail, null, true, null, false, PaletteEntryKind.Command, 0, run)`, no key, not
destructive, its detail the repository's name only while dialogs name it (`NamesRepositoryInDialogs`),
`run` executing `OperationStatus.CancelCommand` only if `CanExecute` still holds. No `AppCommand`
value; not on a dashboard (its rows' menus have it).

§11: `public const string OpenUserGuideText = "Open the user guide";` in `AppCommands`, shared by the
palette and the shortcuts list; `new(AppCommand.OpenUserGuide, [new(F1)], KeyPass.Tunnel)`; a last group
"Help" in the shortcuts list, one row "Open the user guide", F1, with a note on macOS alone: `On a Mac
keyboard F1 needs Fn unless "Use F1, F2, etc. keys as standard function keys" is on.` (ruling 62).

A package that adds a command: a value after the last palette value, never between; a `case` in
`Resolve`; an `Add` at its place; a keyless one in `AppShortcutsTests`' list; a decision.

### 1.9 The start-up timeline

`src/VisualCommit.App/Services/StartupTimeline.cs` (new; WP0 the interface, the null object and a
stub; §10 the real one):

```csharp
public interface IStartupTimeline
{
    void WindowOpened();                    // the main window's Opened
    void GitFound();                        // the first search for git ended with a usable git (1.10)
    void TabsRestored(int count);           // InitializeAsync, before the restored tabs open: the tabs that open a repository (opening.Count), dashboards not counted
    void FirstRows(string repositoryName);  // a repository's first graph rows (RepositoryViewModel.OnRowsDrawn)
}
public sealed class NullStartupTimeline : IStartupTimeline { public static NullStartupTimeline Instance { get; } }
public sealed class StartupTimeline : IStartupTimeline
{
    public StartupTimeline(DateTimeOffset processStarted, IAppLog log, TimeProvider? clock = null);
    public static IStartupTimeline FromProcess(IAppLog log);   // Process.GetCurrentProcess().StartTime; WP0: the null object
}
```

§10's lines and their rules are 10.3.1 and 10.3.2: three start-up lines, each once per process, the
first-rows line only after `TabsRestored` with 1 or more; `RepositoryViewModel`'s reload line only at
a swap whose refs' fingerprint changed, and its heap line at each load's end; `FromProcess` returns
the null object, with a Warning, when the process's start time cannot be read (D438). The headless
harness never makes a timeline.

### 1.10 The composition (WP0, the lead's files)

```csharp
// src/VisualCommit.App/Services/AppLaunch.cs (new; WP0 Timeline, then §4 the rest)
public sealed class AppLaunch
{
    public static AppLaunch Current { get; set; } = new();   // set by Program.Main before Avalonia starts; never by a harness
    public IStartupTimeline Timeline { get; init; } = NullStartupTimeline.Instance;
    public SingleInstanceGuard? Guard { get; init; }                // §4
    public AppStartMode Mode { get; init; } = AppStartMode.Normal;  // §4
    public int ExitCode { get; set; }                               // §4: 2 while the notice shows; 0 after a hand-over or a start in this process; Program.Main returns it
}
```

- `Program.Main`: `AppLaunch.Current = new() { Timeline = StartupTimeline.FromProcess(crashLog) };`
  before `BuildAvaloniaApp()`. `App.OnFrameworkInitializationCompleted` passes its timeline to
  `AppSession.Start`.
- `AppSession.Start` gains, after `tokens`: `IGitSetupProbe? gitProbe = null` (null: the machine's),
  `string? askPassHelper = null` (null: `AskPassLocator.ExpectedPath`; a path without a file gives
  HelperMissing, ruling 43), `IStartupTimeline? startup = null`, `AppIdentity? identity = null` (null:
  `FromAssembly`). It reads `GitLocator.StandInVariable` once, makes
  `GitAccess(probe, () => new GitLookup(settings.Current.GitExecutable, standIn), …)`, gives the shell
  `ct => gitAccess.DetectAsync(ct)` and `gitAccess`, the broker `helperPath`, and calls `WindowOpened`
  on the window's `Opened` and `GitFound` once, when the first search ends with
  `GitAvailability.Available` (D438).
- `MainWindowViewModel.InitializeAsync` calls `TabsRestored(opening.Count)`;
  `RepositoryViewModel.OnRowsDrawn` calls `FirstRows` once per repository view.
- `TabServices` gains two optional parameters, `startup` and `identity`, as `Startup` and `Identity`,
  never null.
- `SettingsScreenServices` gains init members, filled in `ShowSettingsAsync`: `GitDetection? Detection`
  (WP0; the record's positional `IGitConfiguration? Git` keeps its name);
  `Func<string, CancellationToken, Task<GitDetection>>? ProbeGit` (§5: Browse checks a file, through
  `GitAccess.ProbeAsync`); `Func<bool>? HomeHasNetrc` (§5: called off the UI thread; whether `.netrc` or
  `_netrc` exists in the home folder git is given, ruling 8); `Action? DismissNetrcNote` (§5: the note
  stays dismissed for the session); `AppIdentity? Identity`, `IFileLauncher? Launcher` and
  `IClipboardService? Clipboard` (WP0, for §8). The existing `GitVersion` stays, for the netrc note.
- `App` (§8) gains `public MainWindowViewModel? Shell { get; set; }`, set in the desktop path, and
  `public ICommand AboutCommand`, the macOS menu item's command.

### 1.11 The single-instance guard (§4, D421, D422)

```csharp
// src/VisualCommit.Core/AppPaths.cs
public string InstanceLockFile => Path.Combine(DataDirectory, "instance.lock");   // held with FileShare.None for the process's life, never deleted
public string InstanceFile => Path.Combine(DataDirectory, "instance.json");       // {"v":1,"pid":…,"endpoint":…,"started":…}
// src/VisualCommit.App/Services/SingleInstanceGuard.cs (new; no Avalonia type: it runs before Avalonia)
public enum GuardOutcome { Taken, Held, Unavailable }               // Unavailable: no lock file; start as today, with a Warning
public enum ForwardAnswer { Activated, Starting, Closing, NotAnswered }
public enum AppStartMode { Normal, NotAnswered }                     // NotAnswered: the notice, no session
public sealed record SingleInstanceOptions(TimeSpan Connect, TimeSpan Reply, TimeSpan WaitWhileClosing)
{
    public static SingleInstanceOptions Default { get; }            // 5 s, 3 s, 30 s
}
public sealed class SingleInstanceGuard : IDisposable
{
    public static SingleInstanceGuard TryTake(AppPaths paths, IAppLog log, SingleInstanceOptions? options = null);   // never throws
    public GuardOutcome Outcome { get; }              // Held turns Taken after a successful WaitToTake or TryTakeNow
    public string? Endpoint { get; }                  // its own endpoint while Taken, else null
    public ForwardAnswer AskToComeForward();          // Held: one JSON line each way over the endpoint instance.json names
    public bool TryTakeNow();                         // one attempt; on success Taken: the server starts, instance.json is written
    public bool WaitToTake();                         // after Closing: TryTakeNow every 250 ms until WaitWhileClosing
    public Func<ForwardAnswer>? ComeForward { get; set; }   // Taken: run on a pool thread, waited for at most Reply; null answers Starting
    public static string? LockingOffWarning(string dataDirectory, string? variableValue, bool switchOn, bool windows);   // pure: the Warning on macOS and Linux when .NET's file locking is off, else null
    public void Dispose();                            // stops the server, then releases the lock
}
// src/VisualCommit.App/Views/MainWindow.axaml.cs
public ForwardAnswer BringForward();                  // restores a minimised window to its last state and activates it; Closing after Closed
```

App sets `ComeForward` on the main window's `Opened` to
`() => Dispatcher.UIThread.InvokeAsync(window.BringForward).GetTask().GetAwaiter().GetResult()` and on
its `Closed` to `() => ForwardAnswer.Closing`. The endpoint is random per run (a pipe name, or a
socket path under `Path.GetTempPath()`) and holds nothing of the data folder's path. `Program.Main`
returns `AppLaunch.Current.ExitCode`: 0 when the first instance answers `Activated` or `Starting`
(before Avalonia starts), 2 when the notice closes unanswered (ruling 67).

The notice is `src/VisualCommit.App/Views/InstanceNoticeWindow.axaml(.cs)`, a `Window` with a
parameterless constructor (§7 gives it the icon), over `ViewModels/InstanceNoticeViewModel.cs`. Its ids:
`InstanceNotice`, `InstanceNoticeHeading`, `InstanceNoticeBody`, `InstanceNoticeIfNoWindow`,
`InstanceNoticeStatus`, `InstanceNoticeTryAgain`, `InstanceNoticeClose`. A headless test shows it
through `InstanceNoticeHost.Show(GuidancePlatform platform, string folder, ThemeVariant theme)` (new,
§4, in 8a's project), which part I and §9's sweep call. The guard never runs in a harness:
`ShellDriver` calls `AppSession.Start` itself, and the real window gives each launch its own folder.

### 1.12 Automation: names, the sweep, the graph's row (§9, D435)

`src/VisualCommit.App/ViewModels/Commands/AutomationNames.cs` (new), the fixed names, set locally in
XAML through `x:Static` (a local value beats `VcIconButton`'s binding to the tooltip, which stays as a
fallback; no tooltip changes, ruling 34):

| Control | Name |
|---|---|
| `ResolverPreviousConflict`, `ResolverNextConflict`, `ResolverPreviousFile`, `ResolverNextFile` | "Previous conflict", "Next conflict", "Previous file", "Next file" |
| `ResolverCloseButton`, `CloseDiffButton` | "Close the resolver", "Close the diff" |
| `SearchPrevious`, `SearchNext`, `SearchClose` | "Previous match", "Next match", "Close the search" |
| `ActivityLogClose`, `CompareClose`, `CompareSwap` | "Close the activity log", "Close the comparison", "Swap From and To" |
| `DismissRestoreButton`, `ActivityLogToggle` | "Dismiss the restore offer", "Activity log" |
| `LeftSplitter`, `RightSplitter`, `ActivityLogSplitter` | "Resize the left panel", "Resize the right panel", "Resize the activity log" |
| `RowStageButton`, `RowDiscardButton`, `RowUnstageButton` | `StageFileFormat` "Stage {0}", `DiscardFileFormat` "Discard {0}", `UnstageFileFormat` "Unstage {0}", over `ChangedFileRow.Key` |
| `RefHiddenToggle`, `RefSoloToggle` | `ShowRefFormat` "Show {0} in the graph", `StopSoloingRefFormat` "Stop soloing {0}", over `RefItemRow.Text` |

The formats bind with `StringFormat={x:Static …}`, or through a converter in `AutomationNames` if the
compiled binding refuses that. A stage row's buttons exist in UI Automation only on a hovered,
selected or focused row (D425). §4's, §5's and §8's controls take their names from their own texts.
The status bar's `OperationStatus` TextBlock gets `AutomationProperties.LiveSetting="Polite"` (ruling 36).

The graph: `Controls/CommitGraphControl.Automation.cs` (new) holds the graph's peer, moved from
`CommitGraphControl.cs`, now `List` with `ISelectionProvider` (name "Commit graph" and id `GraphRows`
unchanged), and one row peer, `ListItem`, id `GraphSelectedRow`, class `CommitGraphRow`, its bounds the
row's, its name the row's text, its help text the hint, `LiveSetting` Polite. It raises the row's
`Name` change only when the exposed row becomes another (another commit, the working-changes row, a
row after none), wherever the focus is, and once when the graph takes the keyboard focus; a new text
for the same row raises nothing, a new hint a HelpText change (D435). No peer per row; with no
automation client no peer exists. The `[PrivateApi]` embedded-root focus event comes only by a later
decision, if the spike shows the live region silent (ruling 32).

```csharp
// src/VisualCommit.App/Controls/GraphRowSpeech.cs (new; public and pure, since the app has no InternalsVisibleTo)
public static class GraphRowSpeech   // every word a public constant; the texts are 9.3.5's
{
    public static string Name(CommitInfo commit, IReadOnlyList<RefLabel> labels, Func<DateTimeOffset, string> formatDate, Func<RefLabel, string?>? requestOf = null);
    public static string WorkingRowName(string workingRowText);   // " · " read as ", "; its hint is empty
    public static string Hint(CommitInfo commit, int selectedCount, bool matchesSearch);   // "Commit {short SHA}" and more (ruling 33)
}
```

`tests/VisualCommit.Testing/Headless/AutomationSweep.cs` (new) walks the peers from
`ControlAutomationPeer.CreatePeerForElement(root)` and reports each interactive control element whose
name breaks one of 9.3.8's rules, `empty`, `type name`, `key text` or `duplicate`.

```csharp
public sealed record AutomationProblem(string State, string? AutomationId, string ClassName,
    AutomationControlType ControlType, string? Name, string Rule);
public static IReadOnlyList<AutomationProblem> FindProblems(TopLevel root, string state);
public static void AssertNamed(TopLevel root, string state);   // fails with every problem, one per line
```

The sweep's class, `AccessibleNameSweepTests`, is in 8a's project (ruling 37).

### 1.13 The embedded texts and About (§8)

`VisualCommit.App.csproj` embeds, and 8b ships unchanged: `LICENSE` as `VisualCommit.License.txt`,
`THIRD-PARTY-NOTICES.md` as `VisualCommit.ThirdPartyNotices.md`, and each file of the new `notices/`
folder, word for word, as `VisualCommit.Notices.<file name>` (ruling 55).
`src/VisualCommit.App/Services/EmbeddedTexts.cs` (new): the three names as constants
(`License`, `ThirdPartyNotices`, `VendorNoticePrefix`), `string Read(string resourceName)` with `\n`
line ends, `IReadOnlyList<string> VendorNotices()` in name order.

About's ids, which §9's sweep and §12 use: `SettingsNavAbout`, `SettingsAbout`, `AboutIcon`,
`AboutName`, `AboutCreator`, `AboutVersion`, `AboutCopyright`, `AboutLicense`, `AboutShowLicenseButton`,
`AboutNoticesNote`, `AboutShowNoticesButton`, `AboutRepositoryLink`, `AboutGuideLink` (its label "User
guide", its text "Open the user guide"). The text dialog's: `DialogTitle`, `TextDocumentSwitch`,
`TextDocumentPartMain` and one `TextDocumentPart<name>` per vendor file, `TextDocumentText`,
`TextDocumentCopyButton`, `ConfirmButton`.

### 1.14 Variables, files, ports

| Variable | Read by | Values | In the harnesses |
|---|---|---|---|
| `VISUALCOMMIT_GIT_STAND_IN` | `AppSession.Start`, once; §5 logs `VISUALCOMMIT_GIT_STAND_IN is set: Git is looked for only at {value} (a test switch).` as a Warning | a full path: that program is the machine's only git (`GitSource.StandIn`); `none`, or a path without a file: the machine has none. The setting still comes first | removed (null) by `MachineIndependence`; a real-window launch sets it last |
| `VISUALCOMMIT_GIT_STAND_IN_VERSION`, `VISUALCOMMIT_GIT_STAND_IN_FAILURE` | the stand-in program | its `--version` line (default `git version 2.29.0`); a text written to standard error with exit 128, whatever the arguments | removed |
| `VISUALCOMMIT_PERF_RUNS` | `Phase8aPerformance` (§10) | runs per measure, 1 to 20; unset, each fact skips (`Assert.SkipUnless`, as D270's keychain tests) | unset; the lead's series sets 5 |
| `VISUALCOMMIT_GUIDE_ROOT`, `VISUALCOMMIT_GUIDE_PICTURES` | `GuideShots` (§11) | the demo root (`/home/demo` in the workflow): absolute, outside `Path.GetTempPath()` and the repository, empty or holding the marker `.visualcommit-guide`; the pictures' folder | outside the default run |

`tests/VisualCommit.Testing/GitStandIn.cs` (new; WP0) holds the stand-in's names for every test:
`FileName` (`visualcommit-git-stand-in`, with `.exe` on Windows), `VersionVariable` and
`FailureVariable`. The stand-in references nothing and writes the two names out itself.

The stand-in answers, in order: the failure variable set, its text and exit 128; arguments exactly
`--version`, the version line and exit 0; otherwise `fatal: VisualCommit's stand-in for git answers
only --version` and exit 1. It is never named `git.exe`. A check computes a version below the current
`GitVersion.Minimum`.

The data folder gains `settings.json`'s `gitExecutable` (only when set), `instance.lock` and
`instance.json`. No new port (ruling 20). The architecture's list of test switches beside
`VISUALCOMMIT_WATCHER_LAG_MS` gains the stand-in's variable at the close.

### 1.15 The harness's additions

| Addition | Owner | What |
|---|---|---|
| `Phase8aExpectations.Common.cs` | WP0 | `Phase = "8a"`, 1100×700 and 1920×1080, `PictureName` (0.3's pattern as a `Regex`), `PinnedVersion = "0.1.0 (1a2b3c4)"` |
| `Phase8aChecks.Support.cs`, `Phase8aSupportTests.cs` | WP0 | the parts' base (as 7b's `Phase7bChecks.Support.cs`); tests of the pattern and the pin |
| `TestIdentity.Pinned` (Testing) | WP0 | ("VisualCommit", "Rahim Khajei", ("0.1.0", `1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d`), "Copyright (c) 2026 Rahim Khajei") |
| `ShellDriver.Start`, `StartWithTabs` | WP0 | `IGitSetupProbe? gitProbe = null`, `string? askPassHelper = null`, `AppIdentity? identity = null` (null: `TestIdentity.Pinned`) |
| `FakeFileLauncher`'s `Start`; `GitStandIn` (Testing) | WP0 | 1.3; 1.14 |
| `FakeGitSetupProbe` (Testing) | §5 | a queue of answers, the last repeated: Missing, ChosenMissing, TooOld, NotWorking, ConfigFault, NotStarted, CommandLineToolsMissing, `RealAt(path, source, version?)` (the machine's runner reported at a fixed path and, if given, version); `AnswerFor(path, answer)` for `ProbeAsync`; a fixed `GitGuidance` for any platform; `Lookups` |
| `GitWords` (Testing); `TempRepo.StashAsync` | §3 | git's version and words in one place; the scenarios' stashes rebuilt below 2.36 (ruling 72). Members: `Task<GitVersion> VersionAsync()`, `bool HasZdiff3(GitVersion)`, `bool FetchGuardsEveryWorktree(GitVersion)`, `bool StashNamesTheWholeBranch(GitVersion)`, `string StashBranch(string branch, GitVersion)`, `string StashText(string text, GitVersion)`, `string TrackingSetUp(string local, string remote, string remoteBranch, GitVersion)` |
| `HeldGitRunner` (Testing, `Headless/`) | §6 | an `IGitRunner` over the found git that holds the next call whose arguments start with a given word until `Release()`, with `Started` to await; given through `ShellDriver.Start(gitProbe:)` by a probe that returns `MachineGitSetupProbe`'s detection `with { Runner = held }` |
| `IconFile`, `PngHeader` (Testing, plain .NET) | §7 | `IconFile.Read(path)` gives `IReadOnlyList<IconFrame>`; `IconFrame(int Size, bool IsPng, byte[] Data)` with `Bgra()` (top-down, unpremultiplied) for a bitmap entry; `PngHeader.Read(path)` gives `(Width, Height, BitDepth, ColourType)`; the lists `IcoSizes`, `PngSizes`, `IconSetNames`, `AppImageSizes` |
| `InstanceNoticeHost` (8a's project) | §4 | 1.11 |
| `AutomationSweep` (Testing); `ShellDriver.Phase8a.Uia.cs` | §9 | 1.12; `GraphRowPeer()`, the window's peer made once, `RecordNameChanges(AutomationPeer)` |
| `RealApp.Memory()`, `RealApp.LogTime(string dataDirectory, Regex line)`, `Phase8aPerformance`, `PerformanceSeries` | §10 | the measures' helpers (perfsingle.md a) |
| `RealApp.Phase8a.cs` (`ExecutablePath`, `StandInPath`, `BuiltVersionDisplay`, the Win32 calls in a nested class), `Phase8aRealWindowPass*.cs` | §12 | the launches; the 8a link line |
| `GuideSettings`, `GuideScenario`, `GuideCamera`, `LeakGuard`, `GuideShotCatalogue` | §11 | in `GuideShots` |

Each part's `Phase8aExpectations.<Part>.cs` also holds what §12's twin of each R check reads: its
session and scenario, its picture names, its texts and colours (12.3.4). A package adds `ShellDriver`
helpers only in a new partial `ShellDriver.Phase8a.<Key>.cs`.

### 1.16 The WP0 list

In the main checkout, every item marked WP0 in 1.1 to 1.15, with no behaviour or picture changed;
the build passes with no warning and the classes it touches pass. Beyond those marks: `Program.Main`'s
line and `App`'s pass-through of the timeline; the three variables of 1.14 in
`GitIsolation.MachineIndependence`; `scripts/time-scripted-check.sh`'s `8a)` case and default;
CLAUDE.md's rows for 8a's walk-through once it runs.

Tests WP0 adds: `AppVersionInfoTests` (`0.1.0+3dc088707b6e934a3f8fd391e48019cf3d76e500` gives
`0.1.0 (3dc0887)`; `0.1.0` gives `0.1.0`; `0.1.0+local` gives `0.1.0`; null gives `0.0.0`),
`tests/VisualCommit.App.Tests/AppLinksTests.cs` (the base is `https://` + the `RepositoryUrl` owner in
lower case + `.github.io/` + the repository's name + `/`; every page https with no query; §11 adds that
each page exists, reading `VisualRun.RepoRoot`), `GitSetupStatesTests`, and the lists of commands in
`AppShortcutsTests`. The lead then runs `ShellTests`, `MainWindowViewModelTests`, `AppShortcutsTests`,
`PaletteEntriesTests`, `SettingsDialogViewTests`, `PromptDialogsTests`, `GitRepositoryProviderTests`,
`StatusBarTests` and the 8a project, and pushes before wave 1 branches from it.

## 2. colours: the contrast round (D410)

Wave 1, after WP0; part L. Branch `wip/p8a-colours`, worktree `D:/Projects/Personal/vc-wt/p8a-colours`.
Binding: D410 (amends D198), D247; plan.md 760, 768, 769, 783 and "Done when" 786 (no text pair of
either theme below 4.5:1 in `ThemeTokensTests`); rulings 3, 13, 22, 24 to 31. Research:
`research2/colours.md`, its lists `research2/colours-*.txt` and the scan in `research2/scan/`.

### 2.1 What it delivers

- Section 1.5's tokens: the placeholder's own token at full opacity, the dark danger `#EA7D86` with
  the dark label `#10121A`, and `VcAccentOnRowBrush` on hovered and selected rows only, in five views.
- `ThemeTokensTests` as one table of which text lies on which background in both themes, with no
  list of gaps; headless tests of the placeholder's template part and of the row states.
- The earlier expectations the round changes (ruling 28), each reason drafted once for 8a's report.
- `scripts/scan-contrast.py` (new) and `scripts/compare-pictures.py --expected`, so the gate can
  repeat the scan and the masked comparison (ruling 30).
- Part L's six checks, and what §12 needs to repeat five of them.

Left to others: the real-window code (§12); the comparison run and the new references (the lead,
after wave 1's whole run, 2.6); `architecture.md`'s token table and plan.md's line 768 (the lead;
ruling 31); new views use the tokens and set no colour (§5, §8). Out of the phase: a high-contrast
palette (D411).

Decisions: D418 (rulings 24 to 29; amends D198), 1.5's tokens, `#8B9AFF` chosen because it is 15
levels from `#7C8CFF` in red, beyond the picture helpers' tolerance of 8; D419 (rulings 13, 22, 30,
31), the scan, the masked comparison and the references after wave 1 (2.6).

### 2.2 Files

| File | Change |
|---|---|
| `src/VisualCommit.App/Theme/Tokens.axaml` | 1.5's values in both theme dictionaries; `TextControlPlaceholderOpacity` beside `TextControlThemeMinHeight`; the comment at `:25-26` ("a dark label is phase 8's question") says D410 settled it |
| `src/VisualCommit.App/Views/Shell/LeftPanelView.axaml` | after `TextBlock.refName.head` (`:67-69`): `Button:pointerover TextBlock.refName.head, Button.selected TextBlock.refName.head` |
| `src/VisualCommit.App/Views/Shell/RightPanelView.axaml`, `CompareView.axaml` | after `TextBlock.status.renamed`: `Button.fileRow:pointerover TextBlock.status.renamed, Button.fileRow.selected TextBlock.status.renamed` |
| `src/VisualCommit.App/Views/Shell/WorkingChangesView.axaml` | after `:57-59`: `Panel.stageRow:pointerover TextBlock.status.renamed, Panel.stageRow.selected TextBlock.status.renamed` |
| `src/VisualCommit.App/Views/Pages/FileHistoryView.axaml` | after `:65-67`: `ListBox.pageList ListBoxItem:selected TextBlock.status.renamed, ListBox.pageList ListBoxItem:pressed TextBlock.status.renamed` (its hover is `VcRowHoverBrush`, which passes) |
| `tests/VisualCommit.App.Tests/ThemeTokensTests.cs`; `PanelViewTests.cs` | rewritten; three new tests (2.4) |
| the 15 earlier test files and the HEAD helpers of 2.4 | ruling 28 |
| `scripts/scan-contrast.py` (new); `scripts/compare-pictures.py` | 2.6; without `--expected` it is unchanged |
| `tests/VisualCommit.VisualTests.Phase8a/Phase8aChecks.L.cs` (and `.L.Helpers.cs`), `Phase8aExpectations.L.cs`, `Phase8aExpectationTests.L.cs` (new); `ShellDriver.Phase8a.Colours.cs` (new, only if needed) | part L (2.5) |

Each style sets `Foreground` to `VcAccentOnRowBrush` and comes after the style it overrides (of two
triggered styles the later wins). Contracts: implements 1.5; uses 1.15's `Phase8aExpectations.Common.cs`,
`Phase8aChecks.Support.cs` and `ShellDriver.Start`. It adds none.

### 2.3 Behaviour

No new text is shown. Ratios are WCAG 2's (`tests/VisualCommit.Testing/Contrast.cs`); each token's
lowest is in 1.5's table.

**Placeholder.** Fluent's `TextBox` draws `PART_Placeholder` in `PlaceholderForeground` (set from
`TextControlPlaceholderForeground`) at `TextControlPlaceholderOpacity` (0.5). The app's resources are
searched before the Fluent theme's, so the keys in `Tokens.axaml` win; a style on the part would not,
as a template's value outranks a style. All 19 `PlaceholderText` boxes in 12 views take it, each on
`TextControlBackground`. Fluent reads neither `…PointerOver` nor `…Focused`; both stay, equal to the
token. On a box: disabled (2.46 / 2.40) < placeholder (4.75 / 5.00) < secondary (5.51 / 5.90) < typed
(11.30 / 16.51). A disabled box keeps Fluent's `TextControlBackgroundDisabled` (ruling 29): the token
gives 3.01 dark and 2.89 light there, which WCAG 1.4.3 exempts. Typed text and the counter are unchanged.

**Danger.** Every dark use of `VcDangerBrush` becomes `#EA7D86`: "D" and "U" letters, destructive menu
items (`MenuLayer.axaml.cs:524`) and palette entries, failures in the status bar, the activity log and
the dashboard, the resolver's "U", error paths, the settings section's error dot, the Remotes dialog's
"Remove", and the danger button's background (`Theme/Controls.axaml:271-274`), whose label becomes
`#10121A`. Lane 3 keeps `#E5606B` (1.25:1 from the new danger), so the graph is unchanged. Light is
unchanged.

**Accent.** Only 2.2's five styles take `VcAccentOnRowBrush`: HEAD's name in the left panel and a
renamed file's "R" in the commit details, the stage panel, the compare view and the file history,
while the row is hovered, selected or both. Focus borders, the active tab's line, primary buttons,
parent links, lane 0, the solo toggle's icon (a mark at 4.26 against 3:1) and HEAD's name on an idle
row keep `VcAccentBrush`. Under a backdrop the new colours dim as the old did. For later views: accent
text on a hovered or selected background uses `VcAccentOnRowBrush`.

### 2.4 Tests

**`ThemeTokensTests`** (App.Tests), rewritten:
- `Both_themes_hold_the_same_keys`: as today, and both hold the two new tokens.
- `Every_text_reaches_4_5_to_1_on_every_background_it_lies_on` (new; replaces the light and the dark
  method and `DarkKnownGaps`): one table, run in each variant, the failures listed. Primary, secondary,
  success, warning and danger on window, panel, chrome, **control**, hover, selection and **row hover**;
  `VcAccentBrush` on window, panel, chrome, control and row hover; `VcAccentOnRowBrush` on hover and
  selection; `VcTextPlaceholderBrush` and `TextControlForeground` on the three `TextControlBackground…`
  keys; the warning on the banner and the marker; the accent text, the danger text and the tooltip
  pairs. It pins 2.3's lowest figures and today's light figures to two places.
- `The_contrast_rounds_values_are_the_designs` (new): 1.5's values in each variant, `VcLane3Brush` and
  `VcAccentBrush` unchanged, light `VcAccentOnRowBrush` equal to light `VcAccentBrush`.
  `Phase_6s_values_are_the_designs` loses its two danger rows, which move here.
- `The_placeholder_has_its_own_token_at_full_opacity` (new): the three keys equal the token;
  `TextControlPlaceholderOpacity` resolves to `1.0` in each variant; the order on the box;
  `TextControlBackgroundDisabled` is not among the app's keys (ruling 29).

**`PanelViewTests`** (App.Tests), new, each over Dark and Light through the private `Host.Show(…, theme)`:
- `The_filter_boxs_placeholder_is_drawn_at_full_opacity_in_its_own_colour`: the empty `FilterBox`'s
  template `TextBlock` named `PART_Placeholder` is visible, `Opacity` 1, `Foreground` the token. It
  proves the key reaches the template.
- `HEADs_branch_name_takes_the_row_colour_while_its_row_is_hovered_or_selected`: `GraphRefs()`, `main`'s
  `RefName`: idle `VcAccentBrush`; pointer on the row (`Window.MouseMove`) `VcAccentOnRowBrush`; pointer
  away and the row's `IsSelected` set, `VcAccentOnRowBrush`; neither, `VcAccentBrush`.
- `A_renamed_files_R_takes_the_row_colour_while_its_row_is_hovered`: `RightPanelView` with
  `CommitDetailsViewModelTests.Details(sha) with { Files = [new ChangedFile("src/main-app.txt",
  FileChangeKind.Renamed, "src/app.txt"), new ChangedFile("docs/notes.txt", FileChangeKind.Deleted)] }`:
  the "R" idle and hovered; the "D" in `VcDangerBrush`. Selected rows and the other views: part L.

**Red before the change** (recorded in the merge note): the table (dark danger on hover, selection and
control, the label, the missing token), the values, the placeholder's keys and opacity, the three
`PanelViewTests` in dark, the placeholder's in light too.

**Earlier tests that change** (ruling 28; each reason once, in 8a's report):
1. Dark danger `#E5606B` to `#EA7D86` (D410), 13 files: `App.Tests`' `ConflictResolverViewTests.cs:47`,
   `ContextMenuAdapterTests.cs:42`, `PathTextBlockTests.cs:153`, `ThemeTokensTests.cs:72`; `Danger:` in
   `Phase1Expectations.cs:49`, `Phase3…Common.cs:89`, `Phase4…:99`, `Phase5…:102`, `Phase6…:104`,
   `Phase7a…:104`, `Phase7bExpectations.Common.cs:146`; `Phase7bExpectations.A.cs:311`;
   `Phase6SupportTests.cs:115`. Every lane list keeps `#E5606B`.
2. Dark label white to `#10121A`: `DangerText` in `Phase6Expectations.Common.cs:119`,
   `Phase7aExpectations.Common.cs:119`, `Phase7bExpectations.Common.cs:161`; `Phase6SupportTests.cs:116`;
   `Phase6ExpectationTests.L.cs:135`; L31 (`Phase6Checks.L.Overlays.cs:191`), whose
   `l31b-delete-question-dark` shows the dark label.
3. HEAD's name read as the accent: `Phase3Checks.B.Support.cs:512`, `Phase3Checks.C.Helpers.cs:183`,
   `Phase3Checks.D.Helpers.cs:123`, `Phase4Checks.A.Helpers.cs:118`, `Phase4Checks.C.cs:206`,
   `Phase5Checks.P.Helpers.cs:52`, the callers of `Phase5Checks.F.Helpers.cs`' `AssertRefMarksAsync`
   (`Phase5Checks.F.Filters.cs`),
   `PanelViewTests.cs:91`: each expects the row colour where its row is hovered or selected when it
   reads (the 13 accent pictures come from phase 3's and 5's checks); one that passes stays.

The builder finds any other by Grep (`E5606B`, `DangerText`, `Accent` beside `IsHead`) and by running
each class it changes. A test that fails for another reason is a finding, never loosened.

### 2.5 Part L's checks (outline, D244)

Dark, 1100×700 unless named; ink by `TextInk.Problems` (tolerance 8) with the old colour as `replaced`,
and the control's brush read in the tree; `ExactStateAsync()` the same before and after, since nothing
is confirmed. R: the real window repeats it (§12).

| Id | Steps | Expected |
|---|---|---|
| L1 (R) | `Scenarios.ChangesAsync`; the working-changes row selected; then `FilterBox` focused, `sea` typed | `Filter`, `Summary`, `Description`: ink `#8F97A4` on `#272C36`, more than 8 from `#616773`; focused and empty, the same; the Summary counter `#9BA3B0`; `sea` `#E4E7EC`. `l1a-placeholders-dark`, `l1b-filter-typed-dark` |
| L2 (R) | L1 in light | `#68707D` on `#FFFFFF`, more than 8 from `#ADB2B8`; counter `#5C6572`; typed `#1B1F27`. `l2a`, `l2b` |
| L3 | `Scenarios.GraphAsync`; empty, by the routes earlier checks use, each closed unconfirmed: the palette, the search bar, the Pick dialog's `Filter`, the Name dialog, the Remotes form's `Name` and `URL`, the settings screen's user name | each ink the token on `#272C36`. `l3a`…`l3f` |
| L4 (R: a) | graph: a) `feature/login`'s menu by Shift+F10, "Delete…" highlighted by arrows; b) the palette, a destructive entry highlighted; c) "Add settings page"'s `docs/notes.txt` "D" in the details, hovered, then selected; d) the Remotes dialog's "Remove" at rest, then hovered | ink `#EA7D86` on `#2F3542` (a, c, d hovered), `#2A3150` (b, c selected), `#272C36` (d at rest), more than 8 from `#E5606B`. `l4a`…`l4f` |
| L5 (R: dark) | changes; the discard of `src/Calculator.cs` asked; Cancel | dark: background `#EA7D86`, label ink `#10121A`, `Foreground` `VcDangerTextBrush`, Cancel focused, the backdrop as before; light: `#B7323D`, white label. `l5a-discard-dialog-dark`, `l5b-discard-dialog-light`; the file still modified |
| L6 (R: a to c) | graph: HEAD's `main` a) idle (`MoveToNeutral`), b) hovered (`MoveMouse`), c) selected, pointer away; d) "Add settings page"'s "R" idle, hovered, selected; e) `main` compared with `origin/main`, its "R" selected (the builder confirms the rename is listed); f) `src/main-app.txt`'s file history, the rename's row selected; g) changes: staged `src/helpers.py`'s "R" hovered, selected; h) a to c in light | `main` `#7C8CFF` on `#1A1D24`, `#8B9AFF` on `#2F3542`, `#8B9AFF` on `#2A3150`; each "R" `#8B9AFF` hovered or selected, `#7C8CFF` idle; light `#4353D8` always. `l6a`…`l6h` |

Each method under about 40 s alone. `Phase8aExpectations.L.cs` holds the colours, the old colours and the
names; `Phase8aExpectationTests.L.cs` asserts their ratios at 4.5:1, that each new colour is more than
8 from the one a check tells it from (placeholder and secondary 12, accent on a row and accent 15,
danger and lane 3 29) and the names' pattern (0.3). For §12 (12.3.4 launches 1 and 2): L1, L2, L4 a)
(the real menu by keyboard), L5 dark and L6 a) to c) (the real mouse), each capture under 3 % from its
scripted twin, its ink read at the capture's scale; `Phase8aExpectations.L` holds their sessions,
names and colours.

### 2.6 Earlier pictures and the comparison

The round changes 1,333 of phase 7b's 1,350 pictures (1,137 of 1,150 scripted, 196 of 200 captures):
placeholder 1,293, dark danger 360, danger button 41, accent on a row 13; 17 stay
(`colours-none.txt`). Ruling 13 names them all; nothing else changes.

`scripts/scan-contrast.py <pictures folder> <output folder>` (new; Python with Pillow and NumPy, `python
-I`) is `research2/scan/`'s `scan.py`, `cc.py`, `supplement.py`, `recheck_dimmed.py` and `report.py` in
one file, with the folder an argument. Its detectors look for phase 7b's colours, so it runs on phase
7b's references. The corrections from looking stay constants with their reasons (the five tooltip
pictures, each with a box measured once by hand; the lane-3 cuts and pills; the blue fringe; the
round-shape rule). It writes `scan.json`, the eight `colours-<list>.txt`, `report.txt` and
`expected.json`: per `phase-N/<folder>/<name>.png`, boxes `[left, top, right, bottom]`, each hit grown
by 3 px (6 at 200 %) and joined with its text line's hits within 24 px (48), a danger button whole.
`compare-pictures.py --expected <file>` lists per picture the changed pixels outside its boxes under
"Changed outside the expected regions", then "Changed, not expected" and "Expected, not changed"; a
key may be an `fnmatch` pattern, so the lead can write a file for 0.7's later regions.

At wave 1, after its lagged list and whole default run (0.4), the lead:
1. scans `D:/Projects/Personal/phase7b-work/gate/round-3dc0887/visual/` (ruling 22); `diff` with
   `research2/colours-*.txt` prints nothing;
2. compares those references with `artifacts/visual`, `--expected` given: the scripted 1,137 of
   `colours-any.txt` changed, the 13 of `colours-none.txt` the same file; each entry under the three
   headings is explained (a placeholder in a disabled box, which the scan cannot see) or a finding;
3. inspects every flagged picture and part L's, then copies the run's scripted pictures to
   `D:/Projects/Personal/phase8-work/references/wave1/`.

The captures are compared with phase 7b's at the gate (0.7), where every picture is inspected (D247).

### 2.7 Lagged list, time, real-window pass

The lagged list gains nothing: 8a's project is in it, and its earlier walk-throughs show the new
colours. The default run: part L about 4 minutes alone, the App.Tests additions seconds. The pass: L's
two launches, about 1.5 minutes (0.6).

### 2.8 Done when

- `Tokens.axaml`'s diff holds 1.5's values, the opacity key and the comment, nothing else.
- `ThemeTokensTests` and the three `PanelViewTests` pass, each shown red on the base first.
- The 15 files and the failing HEAD helpers changed, each reason drafted in the merge note.
- Part L passes, each method under 40 s alone, its pictures inspected by the lead.
- The scan's script reproduces the eight lists; `compare-pictures.py` without `--expected` writes the
  summary it wrote before for one earlier pair of folders.
- At wave 1: 2.6's steps pass, the lagged list and the whole default run are green, and CI is green in
  every job (the floor job as §3 leaves it).

### 2.9 Risks, unverified, open points

1. A missed earlier expectation leaves a check red: Grep, each changed class's run, wave 1's run.
2. A check that reads an area holding a placeholder as secondary text fails; Grep found none
   (`616773`, `PART_Placeholder`); wave 1's run settles it.
3. Unverified until the headless test runs: that the key outside the theme dictionaries beats Fluent's
   0.5 in both variants (read in Avalonia 12.1.3's source). An update that renames it fails that test.
4. Unverified until wave 1's comparison: the growth of 3 px and the join of 24 px. The builder tunes
   them on its own runs; they never grow to hide a difference the lead has not explained.
5. The danger list may hold lane-3 pieces under overlays (colours.md 6.5); such a picture shows under
   "Expected, not changed" when nothing else in it changes.
6. Unverified: a capture's ink at 200 % under ClearType; §12 settles it in D246's runs.

Open points: 13.4, items 1 to 3.

## 3. floor: the git floor (D406)

Wave 1, after WP0; no part. `wip/p8a-floor`, `D:/Projects/Personal/vc-wt/p8a-floor`. Binding: D406, D2,
D277, rulings 10, 21, 69 to 75, plan.md line 758, risks 26 and 31. Input: floor.md (P1 to P6) and
`research2/floor/` (the failures, git's sources and release notes at their tags).

### 3.1 What the package delivers

- CI's job "ubuntu 22.04 container, git 2.34 (the floor)" green on git 2.34.1, then required: the
  commit that turns it green moves `GitVersion.Minimum` to 2.34.1 and removes `continue-on-error`, and
  nothing else (rulings 10, 69, 71).
- The 20 failures of run 44479788558 and the assertions they hid: A, the tests take both wordings (7);
  B, stash names below 2.36: `GitWords`, the scenarios' stashes rebuilt, the app's own asserted in
  git's words (10); C, P1: a fast-forward of another branch reads the worktrees first, on every git (3).
- What the floor makes dead (ruling 73): D277's branch, the `--diff-merges` fallback and the
  common-dir workaround's premise (3.3.4).
- Its decision is D420 (rulings 69 to 75; amends D2 and D406's "2.34").

Left to others: the first-run "too old" state (§5, reading `Minimum`); the netrc note below 2.46 (§5,
ruling 8), which reuses the Accounts section's git version; the docs (the lead, 3.8). Out of the phase:
a required status check on `master` (ruling 71); new CI limits (ruling 74); the gates pinned to exactly
2.36 (every `GitIs236Async`, `IsReportGitAsync` and `== new GitVersion(2, 36, 0)`), left as they are
(ruling 73); a fix for a mirror refspec (ruling 75); the floor's review in May 2027 (D406).

### 3.2 Files

New: `tests/VisualCommit.Testing/GitWords.cs` (1.15); `tests/VisualCommit.Git.Tests/GitWordsTests.cs`;
the type `BranchCheckedOutElsewhereException` in `src/VisualCommit.Core/Git/IGitRepository.cs`, beside
`NotARepositoryException`.

| File (under `src/`) | Change |
|---|---|
| `VisualCommit.Core/Git/GitVersion.cs` | `Minimum = new(2, 34, 1)`, "(D2, D406, D420)": green commit only |
| `VisualCommit.Core/Git/IGitRepository.cs` | the exception; `FastForwardBranchAsync`'s comment |
| `VisualCommit.Git/GitRepository.Branches.cs` | P1 in `FastForwardBranchAsync` (:382-393) |
| `VisualCommit.Git/GitRepository.Search.cs` | `_noDiffMergesOption` (:26-31), the retry (:90-96), the condition (:171-174) go |
| `VisualCommit.Git/GitRepository.cs` | `ResolveCommonDirectoryAsync` (:211-238), 3.3.4 |
| `VisualCommit.Git/Prompts/AskPassBroker.cs` | `BindingsVersion` (:53-54), its condition (:253), `_gitVersion` go; comments :93, :192-195, :248 |
| `VisualCommit.App/ViewModels/Operations/RepositoryOperations.Branches.cs` | the catch (:574-596); `FastForwardFailureDisplay` (:1308) public, without case |
| `VisualCommit.App/ViewModels/Operations/OperationTexts.cs` | the two-argument `FastForwardInOtherWorktree` beside :229's |
| `VisualCommit.App/ViewModels/Operations/OperationTexts.Hosting.Accounts.cs` | `AccountUseForGitOldGit` (:98-100) goes; `SignOutQuestion(Of)` lose `gitUsesTheAccount`, always end with `GitAsksTheHelpersAgain` |
| `VisualCommit.App/ViewModels/Settings/AccountBlockViewModel.cs` | `GitThatUsesTheAccount`, `UsesTheAccount`, `_gitVersion`, the `gitVersion` parameter go |
| `VisualCommit.App/ViewModels/Settings/AccountsSettingsViewModel.cs` | keeps `gitVersion` for §5's netrc note; gives it to no block |

`.github/workflows/ci.yml`: the green commit only (3.3.5). `TempRepo.cs`: `StashAsync`, the comment at
:78 on 2.30 to 2.32. The tests of 3.4.

Contracts: implements 1.15's `GitWords` and `TempRepo.StashAsync`; uses 1.2's too-old text, which reads
`Minimum`. The broker's `gitVersion` stays on the branch, unread; the lead removes it at the merge
(0.4). Left for §5: the D277 comments at `SettingsDialogViewModel.cs:62` and `SettingsDialogView.axaml:560`.
`FastForwardFailureDisplay` becomes a new public member of the App, used by App.Tests only.

### 3.3 Behaviour

**3.3.1 The floor.** Below 2.34.1 git is `TooOld`; the texts keep their form and read `Minimum`: the
status bar `Git 2.33.1 is too old (needs 2.34.1 or newer)`, `GitAccess`'s
`Git 2.33.1 is too old; VisualCommit needs 2.34.1 or newer.`, the log's Warning
`Git 2.33.1 at /usr/bin/git is older than the minimum, 2.34.1.` 2.34.0 is too old: 2.34.1 reverts its
`.gitignore` fault (release notes 2.34.1), which the ignored-files guards rely on (risk 31).

**3.3.2 P1, the fast-forward of another branch** (ruling 70). `GitRepository.FastForwardBranchAsync`
first reads `ReadWorktreesAsync` (one `git worktree list --porcelain`, a read). When a worktree whose
`Path` is not `WorkingDirectory` (`WorktreeKey.Comparer`) has `Branch == "refs/heads/" + branch` (the
main one or a linked one; locked, or prunable with its folder gone, which git 2.35 refuses too), it
throws `BranchCheckedOutElsewhereException` and runs no fetch. Otherwise the fetch runs as today; git
2.35 and later stay the second guard (a checkout between the read and the fetch; a rebase or bisect of
the branch in another worktree, `worktree.c` v2.35.0:424-429). This worktree's own branch is not P1's:
git refuses it on every git, and `The_checked_out_branch_cannot_be_fast_forwarded_this_way` keeps its
`GitException`.

```csharp
// src/VisualCommit.Core/Git/IGitRepository.cs (new type)
public sealed class BranchCheckedOutElsewhereException(string branch, string worktreePath)
    : Exception($"{branch} is checked out in another worktree ({worktreePath}).")
{
    public string Branch { get; } = branch;               // the short name, as given
    public string WorktreePath { get; } = worktreePath;   // WorktreeInfo.Path: full, the platform's separators
}
```

`RepositoryOperations.FastForwardBranchAsync`'s body catches it and returns
`Refused(text, OperationTexts.FastForwardInOtherWorktree(ex.Branch, ex.WorktreePath))` (the helper in
`RepositoryOperations.Rebase.cs:749`). New text:
`public static string FastForwardInOtherWorktree(string branch, string path) => $"{branch} is checked out in another worktree ({path}).";`
The user sees, for `side` checked out in `/home/demo/edges-wt`:
- the status bar: `Fast-forward side failed`, danger tone (as today);
- the error dialog: title `Fast-forward failed`, summary
  `side is checked out in another worktree (/home/demo/edges-wt).`, no git output, no command, no
  "Copy output" (`StoppedFailure`; the summary through `DefaultFailureDisplay`);
- the log, Info: `<repository>: Fast-forward side: refused, side is checked out in another worktree (/home/demo/edges-wt).`
Nothing moves; the undo history records nothing. The one-argument text stays for git's own refusal,
whose output names the path. `FastForwardFailureDisplay` becomes `public static` (as
`DefaultFailureDisplay`) and looks for `"refusing to fetch into"` with `OrdinalIgnoreCase`. Both
comments of `FastForwardBranchAsync` say that git guards another worktree's branch only from 2.35
(release notes 2.35.0 lines 64-69) and the app reads the worktrees first on every git.

**3.3.3 Git's words in the tests.** What 2.34 words otherwise, from git's sources at their tags:

| What | 2.34.1 | Later | Since |
|---|---|---|---|
| A branch made with tracking | `Branch 'l' set up to track remote branch 'rb' from 'r'.` | `branch 'l' set up to track 'r/rb'.` | 2.35 (`branch.c` v2.34.1:93-95, v2.35.0:144-145) |
| Ambiguous tracking | `fatal: Not tracking: ambiguous information for ref …` | `not tracking: …` | 2.35 |
| `merge.conflictStyle=zdiff3` | exit 128, `unknown style 'zdiff3' given for …` | a merge | 2.35 |
| `fetch . a:refs/heads/b`, b in another worktree | moves b | refused | 2.35 |
| A stash's branch | the part after the last `/` | the whole name | 2.36 (`stash.c` v2.34.1:1302, v2.36.0:1319; release notes 2.36.0 line 344) |

`GitWords` (1.15; each member's comment names its version and source): `VersionAsync` is `git
--version` of TempRepo's git, read once; `HasZdiff3` and `FetchGuardsEveryWorktree` hold from 2.35.0,
`StashNamesTheWholeBranch` from 2.36.0; `StashBranch` is, below 2.36, the part after the last '/'.
`StashText` replaces, below 2.36, each branch name that follows `On `, `WIP on `, `index on ` or
`untracked files on ` at the text's start or after a space or `"`, up to the next `: `, by
`StashBranch`; nothing else changes (`… before checking out feature/clash` stays).

**P3, the scenarios' stashes** (ruling 72). `TempRepo.StashAsync(message)`, after
`git stash push --quiet --message`, when `StashNamesTheWholeBranch` is false and HEAD is a branch whose
name holds a `/`: for i (`w^2`), u (`w^3`, when there is one) and then w, it reads
`git cat-file commit <id>`, replaces only the message's leading `index on <last>: `,
`untracked files on <last>: ` or `On <last>: ` by the whole name, gives w the parents HEAD, i′ and u′,
and writes each with `git hash-object -t commit -w --stdin`; then `git stash drop --quiet` and
`git stash store -m "On <branch>: <message>" <w′>`, all before `_clock.Advance()`. Every other byte
(author, committer, dates, the message's end) is git's own. It returns w′. On 2.36 and later, on a
branch without `/` and on a detached HEAD it makes no extra call. The Filters scenario's stashes are
then `d3f16e8` and `c8f3db3` on every git (unverified until the floor job, 3.9).

**P4, the app's own stashes** stay git's real words. The expectation files keep 2.36's words (they are
linked into the real-window project, which runs on 2.56); the checks pass each stash text they compare
through `StashText`. Phase 5's U18 finds its undo's stash by `PartU.UndoStashShort` from 2.36; below
it, among the commits `git fsck --unreachable --no-reflogs` lists, the one whose subject is
`StashText(PartU.UndoStashSubject)` and whose first parent is `PartU.OtherChangeSha`, exactly one.

**3.3.4 The dead branches** (ruling 73).
- D277's: the broker binds on every git (its other conditions stay); every switch note is the
  provider's (`AccountUseForGitNoteOf`); every sign-out question ends with
  ` Git over HTTPS asks your own credential helpers again.` The text
  `This needs Git 2.31 or newer. With Git {version}, clone, fetch, pull and push still use your git credential helpers.`
  goes; no check showed it (`docs/test-reports/phase-7a.md:454`, `:494`).
- The `--diff-merges` fallback: the Changes mode always passes `--diff-merges=first-parent`, the
  command text it has today on every git from 2.31.
- The common-dir workaround. The opening call
  `git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir` stays word
  for word: phase 5's A2 picture shows it first under "Show reads" (`PartA.OpeningRevParse`,
  `Phase5Checks.A.Log.cs:575`), so adding `--path-format=absolute` to it would change an earlier picture
  (ruling 3). What goes is the premise "before Git 2.31 there is no option": when the opened folder is
  not the top level, the second call becomes `git rev-parse --path-format=absolute --git-common-dir`,
  still in the top level, and its answer is taken as it is (D420; the item is no dead code, since it
  serves a folder opened below its top, 13.4 item 4). If the builder's comparison (3.6) finds any
  earlier picture changed by this item, the builder reverts it and asks the lead.

**3.3.5 Commits, rounds and CI.**
1. Round 1, on `wip/p8a-floor`: all of the above but the green commit. The builder runs 3.4's classes
   on the laptop (git 2.56). The lead merges, pushes the phase branch and reads the floor job; no other
   push of the phase branch until that run ends (a newer push cancels it).
2. Later rounds fix what the floor job finds next; one is expected (3.9). A round's floor job takes
   about 13 minutes, 29 on a slow mirror.
3. The green commit, after a floor run with 0 failed: `Minimum`, `GitVersionTests`,
   `MainWindowViewModelTests.cs:216`, and in `ci.yml` `continue-on-error: true` (:205) and the comment's
   last sentence (:200-201, "Until phase 8a's step 5 …"). The limits (35, 25) and the step that stops
   unless git is 2.34 stay. A container job timed out by a slow mirror is run again (ruling 74).

### 3.4 Tests

Fail before the change: the 20 tests of `floor-failures.txt` on the floor job; P1's new assertions
(the exception, the path, the empty output) on every machine.

New:
- `GitWordsTests` (Git.Tests): `StashBranch` (`feature/alpha` gives `alpha` on 2.34.1 and 2.35.2,
  itself on 2.36.0; `main` and `a/b/c`); `StashText` on 2.34.1 and 2.36.0 over check 14's
  `stash@{0}: On feature/other: VisualCommit: before checking out feature/clash` (only `other` changes),
  check 23's graph row and undo label, U18's `WIP on feature/other: …`; `TrackingSetUp` on both sides of
  2.35; the predicates at 2.34.1, 2.35.0, 2.36.0; `VersionAsync` against `git --version`.
- `TempRepoTests`: a stash on `a/b` reads `stash@{0}: On a/b: m`, has the id taken on 2.56 on every git,
  and applies; on `main` no `hash-object` call.
- `BranchOperationTests`: `A_fast_forward_is_refused_…` asserts the exception (`side`, the folder of
  `edges-wt` as git lists it), no `git fetch`, `side` unmoved, the worktree clean, on every git; its
  non-fast-forward half as today. New: a prunable worktree is refused the same way; a branch checked out
  nowhere costs one `worktree list`, then the fetch.
- `RepositoryOperationsBranchesTests`: `:290` expects the summary with the path (as git lists it,
  compared as a folder), `:291` an empty output and no command; a new theory: `FastForwardFailureDisplay`
  gives `FastForwardInOtherWorktree(branch)` for 2.34's
  `fatal: Refusing to fetch into current branch refs/heads/main of non-bare repository` and for 2.35's
  `fatal: refusing to fetch into branch 'refs/heads/side' checked out at '…'`, `FastForwardNotBehind` for
  `non-fast-forward`, null otherwise.

Changed, each asserting 2.36's words from 2.36 as today:

| Test | Change |
|---|---|
| `FetchRefspecsParityTests.cs:92` | `OrdinalIgnoreCase`; `:101` stays (both say `is not a branch`) |
| `ConflictOperationTests` (:76-79, :533) | below 2.35 zdiff3 asserts exit 128 and `unknown style 'zdiff3' given for`; `MergeAsync`'s exit check (:533) takes the expected code from its caller |
| `Phase3Checks.C.cs:237` | `TrackingSetUp("alpha-copy", "origin", "feature/alpha", git)` |
| `Phase3ScenarioTests.cs:379-383` | from 2.35 as today; below, the fetch succeeds and moves `side` (P6), on its own copy |
| `Phase3Checks.C.Checkout.cs:189`, `:208`, `:280`, check 14's graph rows | through `StashText` |
| `Phase3Checks.A.Toolbar.cs:93`, `:98`'s first row, `:112`, `:135` | through `StashText` |
| `Phase5Checks.U18.cs:511`, `:607-616` | 3.3.3's P4; the comment names 2.36 |
| `Phase5ScenarioTests` :267, :279; `Phase5ChecksF`'s F14 and F15 | unchanged; P3 |
| `GitVersionTests.cs:45-53` (green commit) | `Minimum` is `new GitVersion(2, 34, 1)`; true for 2.34.1, 2.36.0, 3.0.0, false for 2.34.0, 2.30.0, 1.99.0 |
| `MainWindowViewModelTests.cs:216` (green commit) | `Git 2.20.5 is too old (needs 2.34.1 or newer)` (ruling 69) |
| `SearchTests.cs:321-333`, `NoDiffMergesRunner` | removed |
| `ActivityLogViewTests.cs:542`; `Phase6Expectations.W.cs:609` | `git rev-parse --path-format=absolute --git-common-dir` |
| `AskPassBrokerBindingTests` :148-175, :332-342, `Broker`'s `version` | removed |
| `AccountCredentialTests.cs:207-227` | removed (what a git below 2.31 sees) |
| 7a's `AccountsSettingsViewModelTests.cs:135-161` | a fact: the provider's note and the whole question, git 2.34.1 and none |
| 7b's `AccountsProvidersViewModelTests.cs:379-400` | removed |
| `Phase7aExpectations.A.cs:52`, `Phase7aExpectationTests.A.cs:43` | removed; `:45` and `Phase7bExpectationTests.A.cs:59-61` lose the flag |

The builder runs these classes alone with plain `dotnet test` (D245): in Git.Tests `GitWordsTests`,
`TempRepoTests`, `BranchOperationTests`, `FetchRefspecsParityTests`, `ConflictOperationTests`,
`Phase3ScenarioTests`, `Phase5ScenarioTests`, `SearchTests`, `AskPassBrokerBindingTests`,
`AccountCredentialTests`, `GitVersionTests`; in App.Tests `RepositoryOperationsBranchesTests`,
`MainWindowViewModelTests`, `ActivityLogViewTests`; `Phase3ChecksA`, `Phase3ChecksC`, `Phase5ChecksF`,
`Phase5ChecksU`, `Phase6ChecksW`; phase 7a's `AccountsSettingsViewModelTests` and
`Phase7aExpectationTests`, phase 7b's `AccountsProvidersViewModelTests` and `Phase7bExpectationTests`.

### 3.5 Visual checks

None. Nothing on screen changes on 2.35 or later; P1's dialog and the old-git note are in no check;
the "too old" state's checks are §5's (part F).

### 3.6 Earlier pictures

None (ruling 3). The proof: the scripted pictures of the builder's runs of `Phase3ChecksA`,
`Phase3ChecksC`, `Phase5ChecksF`, `Phase5ChecksU` and `Phase6ChecksW`, compared by
`scripts/compare-pictures.py` with the current references (0.7), show no difference, and a grep shows
no check with the activity log open on a fast-forward of another branch or on a folder opened below its
top. The floor job's pictures (`visual-ubuntu-22.04-git-floor`) show 2.34's words and are compared with
nothing.

### 3.7 Lagged list, run time, real-window pass

The lagged list gains nothing (P1's read touches no file the watcher sees; `SearchBarTests` already
covers the search). The default run: about 12 tests more, 8 fewer, seconds. The floor job's Test step
stays about 7 minutes. The real-window pass: nothing (2.56; its linked constants keep 2.36's words).

### 3.8 Done when

- The green commit's run: the floor job succeeds, 0 failed, without `continue-on-error`; every other job
  green (risk 46's ratio run again); recorded in `docs/status.md`'s CI section.
- `GitVersion.Minimum` is 2.34.1. A grep of `src/` finds no `BindingsVersion`, `GitThatUsesTheAccount`,
  `UsesTheAccount`, `AccountUseForGitOldGit`, `_noDiffMergesOption`, `new(2, 31, 0)` or
  "before Git 2.31".
- The green commit's diff of `ci.yml` is its two removals; the limits read 35 and 25.
- No 2.36-pinned gate (3.1) changes.
- 3.6's comparison shows no difference.
- At the merge the lead removes the broker's `gitVersion` (0.4) and changes: D420 in decisions.md;
  `requirements.md:21`, `README.md:40`, `CLAUDE.md:530-531` (Git 2.34.1 or newer, the app's minimum and
  the tests'); architecture.md :614 (P1), :740 (the search), the opening's common folder, :2438-2440,
  :2573-2576, :2618, :3941, :3986-3987 (D277); plan.md line 758 (2.34.1), risk 26 (2.34.1 and 3.3.3's
  five differences), risk 31 (2.30 to 2.33 fall away), a new risk (the mirror refspec); the report's
  "Changes to expected results" (3.4's checks that take 2.34's words, `PartW.ReadForms`).
- Afterwards `AccountsSettingsViewModel.cs`, `AskPassBroker.cs` and `MainWindowViewModelTests.cs` pass
  to §5, `Phase7aExpectations.A.cs` to §8 (0.4).

### 3.9 Risks, what is unverified, open points

Risks:
1. Hidden assertions: each test stopped at its first failure; 3.4 fixes what a grep finds (check 23's
   undo labels, check 14's left panel and graph), and the next round may find more.
2. The floor job used 28 min 35 s of its 35 on a slow mirror; such a day can time it out.
3. P1 reads before git writes: on 2.34 a checkout between the two, or a rebase or bisect of the branch
   in another worktree, is not caught; 2.35 and later catch both.
4. A mirror refspec (`+refs/*:refs/*`) can move a branch checked out in another worktree on 2.34, as
   in a terminal: a plan risk, not fixed (ruling 75).
5. Ubuntu's 2.34.1 carries backports (`safe.directory`, `protocol.file.allow`): "tested on 2.34.1"
   means Ubuntu's; nothing in the app depends on them.
6. Git's refusal of this worktree's own branch still reads `… in another worktree.` (today's text on
   2.35 and later); no menu offers that fast-forward.

Unverified, with the step that verifies it: the rebuilt stashes' ids on 2.34 (`Phase5ScenarioTests`,
the new pin; round 1 of the floor job); 2.34's zdiff3 refusal on standard error, and U18's `fsck`
route (round 1); the second common-dir call in no picture (3.6).

Open point: 13.4, item 4.

## 4. single: one instance per data folder, and the macOS data folder

Wave 1, after WP0; branch `wip/p8a-single`, part I. Carries rulings 3, 16, 23, 67, 68, plan.md's
line 778 and 1.11.

### 4.1 What the package delivers, and what it leaves

Delivers: a guard in `Program.Main`, keyed by the data folder (ruling 16): the first start holds
`instance.lock` for its process's life and names a per-run endpoint in `instance.json`; a second
start on the same folder asks the first to come to the front and exits 0, writing nothing but log
lines; two data folders still run two instances. When the first does not answer, a small notice
with "Try again" and "Close", no session and no start beside the other; "Close" exits 2 (ruling 67).
`MainWindow.BringForward`. A test of the data folder per platform that replaces a tautological one
(ruling 68), run by CI's macOS job, which closes plan.md's line 778. Part I's scripted checks and
the outline of I1 and I2 for §12.

Leaves: to 8b, the updater's bootstrap before the guard (its hooks run the app with their own
arguments, perfsingle.md b fact 8), a second key by install folder and the macOS limits (ruling 67);
to §7 the notice's icon; to §12 the real-window code; to the lead, architecture.md's lists, CLAUDE.md's
note that a `dotnet run` beside a running app on the default folder now hands over, plan.md's line
778 closed by the test, a plan risk for a data folder on a network share. Out of the phase: opening a
folder a second start names (the protocol keeps an empty `args`); any switch.

Decisions: D421 (rulings 16, 67), one instance per data folder, since `session.json` is rewritten
whole (`JsonSessionStore.cs:31-45`); D422, "Try again" starts the app in the same process once the
other has ended.

### 4.2 Files

Changed (each exists): `src/VisualCommit.Core/AppPaths.cs` (`InstanceLockFile`, `InstanceFile`);
`src/VisualCommit.App/Program.cs` (4.3.1); `src/VisualCommit.App/App.axaml.cs` (inside the desktop
lifetime's branch only: `StartSession`, today's three lines factored out with the guard's wiring, and
the notice); `src/VisualCommit.App/Views/MainWindow.axaml.cs` (`BringForward`, a `_closed` flag set on
`Closed`); `src/VisualCommit.App/Services/AppLaunch.cs` (WP0's: `Guard`, `Mode`, `ExitCode`);
`tests/VisualCommit.App.Tests/AppPathsTests.cs` (4.5); `tests/VisualCommit.Testing/GitIsolation.cs`,
line 74's comment, which says macOS's `ApplicationData` follows `XDG_CONFIG_HOME` (untrue since .NET 8):
it says Linux's alone does.

New: `src/VisualCommit.App/Services/SingleInstanceGuard.cs` (1.11's types; no Avalonia type, since it
runs before Avalonia, `Program.cs:10-11`); `src/VisualCommit.App/Views/InstanceNoticeWindow.axaml(.cs)`;
`src/VisualCommit.App/ViewModels/InstanceNoticeViewModel.cs` (with `NoticeRetry` `HandedOver`, `Free`,
`NoAnswer` and `NoticeOutcome` `HandedOver`, `StartHere`, `Closed`); `.../ViewModels/InstanceNoticeTexts.cs`;
in `tests/VisualCommit.App.Tests` (no session: D416) `SingleInstanceGuardTests.cs`,
`InstanceNoticeViewModelTests.cs`, `MainWindowBringForwardTests.cs`; in WP0's
`tests/VisualCommit.VisualTests.Phase8a` `InstanceNoticeHost.cs` (1.11), `Phase8aChecks.I.cs`,
`Phase8aExpectations.I.cs`, `Phase8aExpectationTests.I.cs`.

Implements 1.11 whole and 1.10's `Guard`, `Mode` and `ExitCode`. Uses `AppLaunch.Current` and `Timeline` (1.10),
`GuidancePlatform` (1.2), the 8a support (1.15), `DurableFile.Replace`, `PlatformSystemTheme.IsDarkIn`
and `Theme/Dialogs.axaml`'s classes.

### 4.3 Behaviour

#### 4.3.1 `Program.Main`

In order: a comment that an updater's start-up hook goes first (the code names no phase, 0.3);
`var paths = AppPaths.Resolve()`; the crash log on `paths.LogDirectory` and its handlers, as today;
`using var guard = SingleInstanceGuard.TryTake(paths, crashLog)`. `Taken` or `Unavailable`: mode
`Normal`. `Held`: `AskToComeForward()`; `Activated` or `Starting` return 0 at once (Avalonia never
starts); `Closing` gives `Normal` if `WaitToTake()`, else `NotAnswered`; `NotAnswered` gives
`NotAnswered`. Then WP0's line, extended: `AppLaunch.Current = new() { Timeline = …, Guard = guard,
Mode = mode }`; the lifetime as today; return `AppLaunch.Current.ExitCode`. The `using` disposes the
guard after the lifetime and on the rethrow. The waits (at most 8 s, or 30 s after `Closing`) come
before any UI thread exists (Q2).

#### 4.3.2 The guard

`TryTake` never throws. It creates the data folder and opens `instance.lock` with `OpenOrCreate`,
`ReadWrite`, `FileShare.None`, keeping the stream:
- Opened: `Taken`. It starts the server, then writes `instance.json` through `DurableFile.Replace`
  with `Utf8JsonWriter`: `{"v":1,"pid":…,"endpoint":"…","started":"<ISO 8601 with offset>"}`. If the
  old file named a socket path `<Path.GetTempPath()>VisualCommit-<16 hex>` or `/tmp/VisualCommit-<16 hex>`
  exactly and a file is there, it deletes it first (the lock was free, so its owner is gone).
- `IOException` while `instance.lock` exists: `Held`, without reading an HResult (Unix's for a held
  `flock` is unchecked).
- Anything else (`UnauthorizedAccessException`, no file, a path under a file): `Unavailable`; the start
  goes on as today.

`AskToComeForward` (on `Held` only, else `InvalidOperationException`): every 100 ms until `Connect`
has passed it reads `instance.json` (missing or half written: again), on Windows calls
`AllowSetForegroundWindow(pid)` (user32 by `[DllImport]`, as `WindowsJob.cs`; failure ignored),
connects with `PipeOptions.CurrentUserOnly`, sends one line and waits at most `Reply` for one:
client `{"v":1,"request":"come-forward","pid":<its pid>,"args":[]}`; server `{"v":1,"answer":"activated"}`,
`"starting"` or `"closing"`. Anything else is `NotAnswered`.

`TryTakeNow()` is one attempt; success makes `Outcome` `Taken` and runs `Taken`'s steps.
`WaitToTake()` calls it every 250 ms until `WaitWhileClosing` has passed. `Default` is 5 s, 3 s, 30 s.

The endpoint: on Windows a pipe name `VisualCommit-<16 hex>` (`RandomNumberGenerator.GetHexString(16,
lowercase: true)`); on macOS and Linux the socket path `<Path.GetTempPath()>VisualCommit-<16 hex>`, or
`/tmp/VisualCommit-<16 hex>` when that is over 100 UTF-8 bytes (macOS allows 104). Nothing of the data
folder's path. `Endpoint`: the guard's own while `Taken`, else null.

`Dispose` (twice is harmless): stops the server, deletes `instance.json` (best effort), closes the
lock's stream. The lock file stays (deleted, two processes could lock two files of one name).

On macOS and Linux, `TryTake` logs `LockingOffWarning` when
`DOTNET_SYSTEM_IO_DISABLEFILELOCKING` is `1` or `true` in any case, or `System.IO.DisableFileLocking`
is on: .NET then takes no lock (perfsingle.md b fact 4). On Windows it does nothing.

#### 4.3.3 The server and `ComeForward`

A background task, one client at a time; each connection is read for at most `Reply` and 4 KB. A
malformed line, another `v` or another `request` gets no answer. `ComeForward` null answers
`starting`; otherwise the guard runs it on a pool thread and waits at most `Reply`, sending its
answer, or nothing after a throw or a timeout. A server that cannot open its endpoint leaves the
guard `Taken`, with a Warning; a second start then gets the notice.

`StartSession` sets `ComeForward` on the main window's `Opened` and `Closed` as 1.11 says (no
dispatch while the dispatcher stops).

`MainWindow.BringForward()`: after `Closed`, `Closing`. Else a `Minimized` window becomes `Maximized`
when `_wasMaximized` (`MainWindow.axaml.cs:23`, `:63-71`), otherwise `Normal`; then `Activate()`;
`Activated`. While "Finishing operations" shows, the window is open: `Activated`.

When the platform refuses: Windows flashes the taskbar button (a start from the taskbar is the
foreground process, so its `AllowSetForegroundWindow` normally suffices); macOS may order the window
front without activating the app; a Linux window manager may only mark it.

#### 4.3.4 The notice

Mode `NotAnswered`: `App.OnFrameworkInitializationCompleted` sets `desktop.ShutdownMode` to
`OnExplicitShutdown` and `AppLaunch.Current.ExitCode` to 2 and shows `InstanceNoticeWindow` as
`desktop.MainWindow`. No session, no settings store, nothing written. Its theme: `settings.json`'s
`theme` read without writing by a static `InstanceNoticeViewModel.ReadTheme` (missing or unreadable:
Dark; never set aside, as `JsonSettingsStore` would), `System` by `PlatformSystemTheme.IsDarkIn(PlatformSettings?.GetColorValues())`,
set on the window's own `RequestedThemeVariant`, never the application's.

The window: `Title` "VisualCommit", width 480, `SizeToContent="Height"`, not resizable, centred on
the screen, `VcPanelBackgroundBrush`, `VcTextPrimaryBrush`, font size 12.5, padding 20, id
`InstanceNotice`. Top to bottom: the heading (`dialogTitle`, id `InstanceNoticeHeading`), the body
(`dialogText`, `InstanceNoticeBody`), the platform's sentence (`dialogText`, `InstanceNoticeIfNoWindow`),
the status (`dialogWarning`, `InstanceNoticeStatus`, only after an unanswered try), and
(`dialogButtons`) "Close" (`VcActionButton`, `InstanceNoticeClose`), then "Try again"
(`VcPrimaryButton`, `IsDefault`, `InstanceNoticeTryAgain`), which takes the focus at open as a
dialog's button does. Tab goes round the two; Enter is "Try again"; Esc, the title bar's close and
Alt+F4 are "Close". Each button's automation name is its text.

Texts (`InstanceNoticeTexts`, ruling 58):
- Title `VisualCommit`; heading `VisualCommit is already running`
- Body: `Another VisualCommit uses the same data folder, {folder}, but did not answer. Switch to its window, or close it and start VisualCommit again.`
- Windows: `If no VisualCommit window is open, end VisualCommit in Task Manager, then choose Try again.`
- macOS: `If no VisualCommit window is open, quit VisualCommit in Activity Monitor, then choose Try again.`
- Linux: `If no VisualCommit window is open, end VisualCommit in your system monitor, then choose Try again.`
- Buttons `Try again` (while it runs `Trying…`, disabled) and `Close`; status `It still did not answer.`

It says "did not answer", never that the other hangs: an elevated first instance on Windows refuses
a normal one's connection (`CurrentUserOnly` checks elevation) while it works.

"Try again" runs on a pool thread: `TryTakeNow()` gives `Free`; else `AskToComeForward()`:
`Activated` or `Starting` give `HandedOver`, `Closing` gives `Free` if `WaitToTake()` and `NoAnswer`
if not, `NotAnswered` gives `NoAnswer`. On the UI thread: `HandedOver` sets `ExitCode` 0, closes the
notice and calls `desktop.Shutdown()`. `Free` (D422) runs `StartSession`, shows the main
window as `desktop.MainWindow`, puts `ShutdownMode` back to `OnLastWindowClose`, sets `ExitCode` 0
and closes the notice. `NoAnswer` shows the status and enables "Try again",
which keeps the focus. "Close" during a try closes at once and drops its result. Every other
close leaves `ExitCode` 2 and calls `desktop.Shutdown()`.

#### 4.3.5 Log lines

Through the crash log; `{folder}` the data folder, `{pid}` from `instance.json` (`unknown` unread).
- Info: `This VisualCommit holds the data folder {folder}: a second start on it is sent here.`
- Info, by answer: `VisualCommit already runs on the data folder {folder} (process {pid}); it was asked to come to the front, and this start ends.`;
  `VisualCommit is starting on the data folder {folder} (process {pid}); this start ends.`;
  `VisualCommit on the data folder {folder} (process {pid}) is closing; this start waits up to 30 s for it.`
- Warning: `VisualCommit holds the data folder {folder} but did not answer ({why}).`, `{why}` one of
  `no instance.json`, `its endpoint refused the connection`, `no reply within 3 s`,
  `a reply this version does not read`.
- After the wait: Info `The other VisualCommit closed; this start goes on.`, Warning
  `The other VisualCommit did not close within 30 s.`
- Warning: `The data folder {folder} could not be locked against a second VisualCommit ({reason}); this one starts without that guard.`
- Warning: `File locking is turned off (DOTNET_SYSTEM_IO_DISABLEFILELOCKING or System.IO.DisableFileLocking): a second VisualCommit on the data folder {folder} is not stopped.`
- Server: Info `A second VisualCommit (process {pid}) started on this data folder: answered {activated|starting|closing}.`;
  Warning `A second VisualCommit (process {pid}) started on this data folder, and the window did not come forward within 3 s.`;
  Warning `The single-instance endpoint could not be opened ({reason}): a second start on this data folder will be told that this one did not answer.`
  (Seconds are the options' values.)

### 4.4 The macOS data folder (ruling 68)

On .NET 10, `SpecialFolder.ApplicationData` on macOS is `~/Library/Application Support`
(perfsingle.md c): the docs are right and no code changes. 4.5's test is the proof, run in CI's macOS job's Test step.

### 4.5 Tests (the builder runs these classes only, D245)

`SingleInstanceGuardTests` (App.Tests, every platform, CI's six jobs; options 300 ms, 300 ms, 2 s;
each data folder in a `TempDirectory`, named `data folder`, with a space):
1. A second guard on a taken folder is `Held`; one on another folder `Taken`.
2. `AskToComeForward` reaches a fake `ComeForward` once: `Activated`; with it null, `Starting`; the
   holder's log has the server's line with the asker's pid.
3. The test holds `instance.lock` itself, no `instance.json`: `NotAnswered` within `Connect` plus
   100 ms, with `no instance.json` in the Warning.
4. `ComeForward` answers `Closing`, the holder disposes 300 ms later: `WaitToTake` true, `Outcome`
   `Taken`, and a third guard's ask reaches it.
5. A lock file left unheld and an `instance.json` naming a stale endpoint (on macOS and Linux a file at
   that path): the next guard is `Taken` and serves; on Unix the stale file is gone.
6. `Endpoint` holds no segment of the data folder below the temp folder; Windows
   `^VisualCommit-[0-9a-f]{16}$`, elsewhere the temp folder or `/tmp/` plus that, at most 100 bytes; two
   guards' differ.
7. A data folder under a file (`<temp>/a-file/data`): `Unavailable`, no throw, a Warning naming it (a
   path under a file fails as root too, which a read-only folder would not in CI's containers).
8. `LockingOffWarning`: `1`, `TRUE` and the switch give the text on macOS and Linux; unset, `0`, and
   anything on Windows give null.
9. A malformed line, `"v":2` and a silent client get no answer and the next client is served; a
   `ComeForward` that throws or outlasts `Reply` gives `NotAnswered` and a Warning, and the next is served.
10. `Dispose` twice is harmless; afterwards `instance.json` is gone, `instance.lock` stays, a new guard
    is `Taken`.

`InstanceNoticeViewModelTests` (App.Tests): the texts per platform, word for word; `Trying…` and
disabled while a held try runs; `NoAnswer` shows the status and enables the button; `HandedOver`,
`Free` and "Close" give `HandedOver`, `StartHere` and `Closed`, "Close" also during a try, whose later
result changes nothing; the theme read: `"theme": "Light"` light, missing and unreadable Dark, the
unreadable file still there.

`MainWindowBringForwardTests` (App.Tests, `[AvaloniaFact]`, `new MainWindow()` as
`PanelViewTests.cs:325`): Maximized, then Minimized, comes back Maximized; Normal comes back Normal;
each `Activated`; after `Close()`, `Closing`.

`AppPathsTests`: `The_default_data_folder_is_a_VisualCommit_folder_in_the_users_app_data` (`:18-29`)
compares the method with its own call; it is replaced by
`The_default_data_folder_is_the_platforms_documented_one` (perfsingle.md c): `%APPDATA%\VisualCommit`
on Windows; `<home>/Library/Application Support/VisualCommit` on macOS; on Linux
`$XDG_CONFIG_HOME/VisualCommit` when it starts with `/`, else `<home>/.config/VisualCommit` (the test
processes set it, `GitIsolation.cs:40`). It reads variables' values only. `Everything_lives_under_the_data_folder`
gains the two new files.

Fail before the change: the guard's, the notice's and `BringForward`'s tests (against throwing stubs
first). The `AppPathsTests` replacement passes before and after: it proves the docs. No other earlier
test changes; no harness meets the guard (`ShellDriver.cs:195` calls `AppSession.Start`).

### 4.6 Part I's checks, in outline

The notice is built in the headless app through `InstanceNoticeHost.Show` (1.11: its theme on the
window, a fake try) and captured with `CaptureRenderedFrame` at its own size, 480 wide, scale 1; the
paths are made up (ruling 63). §9's sweep shows it the same way.

| Id | Steps | Expected | Theme, size | Real window |
|---|---|---|---|---|
| I1 | Not scripted (no harness runs `Program.Main`). Launch A on folder D; minimise it; start B on D, same exe and environment | B exits 0 within 10 s with no window; A is not iconic and visible within 2 s; the foreground noted, not asserted; D's log has B's "asked to come to the front" and A's "answered activated"; `session.json`'s bytes unchanged by B | dark, 1100×700 | yes, its own (12.3.4 launch 4) |
| I2 | Windows text, `C:\Users\demo\AppData\Roaming\VisualCommit`; open: `i2a-notice-dark`; Enter, try held: `i2b-notice-trying`; no answer: `i2c-notice-no-answer`; Esc | i2a: heading, body with the path, the Windows sentence, no status, Close then Try again, Try again focused; i2b: `Trying…`, disabled; i2c: `It still did not answer.` in the warning colour, Try again enabled and focused; Esc: `Closed`, window gone | dark, 480 wide | yes: exit 2, then Try again starting the app after the lock is freed (12.3.4 launches 5 and 6; D443 masks the body) |
| I3 | Linux text, `/home/demo/.config/VisualCommit`; open: `i3a-notice-light`; Tab, Tab, Shift+Tab, Space | as i2a in light with the Linux sentence; the focus goes Close, Try again, Close; Space: `Closed` | light, 480 wide | no |

`Phase8aExpectations.I` holds the names, the texts and the log lines' fragments that §12 reads. By
hand at the gate (0.5): a second start from the taskbar while the app is behind another window; macOS
(`open -n`, Terminal) goes to 8b's Mac checklist.

### 4.7 Earlier pictures

None. The guard runs only in `Program.Main`, App's change only under a desktop lifetime, which the
headless app lacks, and `MainWindow` gains a method and a flag. Proof: the builder's run of 4.5 and
part I; at wave 1's last merge the whole run's scripted pictures against the references
(`scripts/compare-pictures.py`) show nothing beyond §2's regions (0.7). New: `i2a-notice-dark`,
`i2b-notice-trying`, `i2c-notice-no-answer`, `i3a-notice-light`, I2's real capture. In the real-window
pass each launch's folder gains the two files and its log one Info line; no check lists a data
folder's files (Grep), and each relaunch waits for the exit.

### 4.8 Lagged list, time, the pass

Lagged list: nothing new (8a's project is listed; part I has no watcher). Default run: about 30 s.
Real-window pass: I1 about 50 s, I2's launches about 15 and 45 s: about 2 minutes (0.6).

### 4.9 Done when; risks; open points

**Done when** (the lead, at the merge): 4.5's four classes and part I pass on the laptop and in CI's
six jobs, the `AppPathsTests` replacement read in the macOS job's log; part I's pictures inspected,
each method under 40 s alone (`time-scripted-check.sh "*Phase8aChecksI*" 8a`); no new variable or
switch; `Program.Main`'s 0 and 2 read in review (§12 runs them); no build warning.

**Risks and what is unverified**, with the step that settles each:
- Two `FileShare.None` streams in one process conflicting on macOS and Linux: tests 1 and 4 in CI.
- The lifetime staying alive under `OnExplicitShutdown` while the notice closes, and `StartSession`
  after the lifetime has started: I2's second launch.
- The headless window keeping `Minimized`: `MainWindowBringForwardTests`; failing that, a public static
  `MainWindow.RestoredState(bool wasMaximized)` is tested and I1 proves the rest.
- The socket path under a real macOS `TMPDIR`; macOS bringing the app forward: 8b's Mac checklist.
- A data folder on a network share (`flock` depends on the mount): a plan risk, by the lead.
- `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` on Unix: a Warning only (test 8).
- Accepted: a first instance hung before its window keeps answering `starting`; a second start waits
  up to 30 s unseen while the first closes (13.4 item 5).

The notice says "data folder", as the docs and the guide's install page do, where perfsingle.md
wrote "settings folder". I2's second launch (about 45 s) stays: without it D422 is untested.

## 5. firstrun: git's detection, guidance and setting (R4)

Key `firstrun`, wave 2, after §3 (the texts read `GitVersion.Minimum`, 2.34.1). Carries R4, D405, plan
757, 766, 767, 773, 781, 786, rulings 4 to 9 and 40 to 48. Its decisions: D423 (rulings 4 to 6, 9,
40 to 46, 48; D405), the setting, the lookup, the screen, winget and the helper; D424 (rulings 7, 8),
no `ssh -V` probe and the netrc note.

### 5.1 Delivers and leaves

Delivers 0.1's first-run rows: the lookup and the setting, Check again, the screen's five states
(git's own words, a broken `~/.gitconfig` named), the guidance, the status bar, the settings row, the
netrc note, the ssh line, the stand-in, `FakeGitSetupProbe`, part F. Leaves the guide (§11), the sweep
(§9), the real-window code (§12), D401's question (8b; only while this screen is not shown), the Mac by
hand (8b).

### 5.2 Files and contracts

New: `src/VisualCommit.Git/OsRelease.cs` (`Parse(string) → LinuxDistribution?`; `/etc/os-release`,
else `/usr/lib/os-release`);
`src/VisualCommit.App/ViewModels/GitSetup/GitSetupViewModel.cs`, `GitSetupTexts.cs` (every text below);
`Views/Shell/GitSetupView.axaml(.cs)`; `ViewModels/MainWindowViewModel.Git.cs`;
`ViewModels/Settings/GitProgramViewModel.cs`; `tests/VisualCommit.GitStandIn/` (as
`VisualCommit.AskPass.csproj`, `AssemblyName` `visualcommit-git-stand-in`, 1.14's answers);
`tests/VisualCommit.Testing/Headless/FakeGitSetupProbe.cs`, `ShellDriver.Phase8a.FirstRun.cs`; 8a's
`Phase8aChecks.F.cs`, `Phase8aExpectations.F.cs`, `Phase8aExpectationTests.F.cs`, `GitSetupShellTests.cs`.

Changed: `src/VisualCommit.Git/GitLocator.cs` (`GitSearchContext` gains init `IsMacOS`,
`Func<bool>? CommandLineToolsInstalled`), WP0's `GitSetupProbe.cs`, `GitConfiguration.cs`,
`Prompts/AskPassBroker.cs`; under `src/VisualCommit.App/`: `Services/GitAccess.cs`,
`Services/FileLauncher.cs` (`StartAsync`, `InNewTerminal`), `ViewModels/MainWindowViewModel.cs`,
`.Settings.cs`, `RepoTabViewModel.cs`, `WelcomeViewModel.cs`, `Views/Shell/CommitGraphView.axaml(.cs)`,
`StatusBarView.axaml`, `Views/Dialogs/SettingsDialogView.axaml`,
`ViewModels/Dialogs/SettingsDialogViewModel.cs` (and the D277 comment at `:62`, which now names the
netrc note), `ViewModels/Settings/AccountsSettingsViewModel.cs` (it keeps `gitVersion` and gains
optional `Func<bool>? homeHasNetrc`, `Action? dismissNetrcNote`), the D277 comment at
`SettingsDialogView.axaml:560`; `tests/VisualCommit.Git.Tests/VisualCommit.Git.Tests.csproj` (the
stand-in's reference).

Made real: 1.2 (with `GitAccess.ProbeAsync`, `ReadGuidance` and `RepoTabViewModel.GitSetup`), 1.3's
`StartAsync`, 1.4, 1.10's `ProbeGit`, `HomeHasNetrc` and `DismissNetrcNote`, 1.14, 1.15's fake. Used:
`GitSetupStates.Of`, `AppLinks.Guide`, `FakeFileLauncher`, `ShellDriver.Start`, `GitStandIn`.
`GitSearchContext` (`GitLocator.cs`) gains init members `bool IsMacOS` and `Func<bool>?
CommandLineToolsInstalled`, its positional constructor unchanged.

### 5.3 Behaviour

**Lookup**, off the caller's thread (it may start `xcode-select`): the setting: a file there is run
whatever it answers (`Setting`); no file: `NotFound` with `ChosenMissing`, and `PathCandidate` = what
the next steps find, not run; no fallback (ruling 41). Else `StandIn`: a path to a file is the only git;
`none` or no file: none. Else the PATH, then the usual folders, as today. On macOS `/usr/bin/git`, from
either, only when `/usr/bin/xcode-select -p` exits 0 within 5 s (asked once per search); otherwise
skipped with `CommandLineToolsMissing`.

**Version.** Not started: `Broken`, `ErrorText` the exception's message. Exit ≠ 0 or no version:
`Broken`, `ErrorText` standard error, else output (≤ 4,000), `ExitCode`; `^fatal: bad config line (\d+)
in file (.+)$` gives `ConfigFault`; the Warning names that first line. `Using Git {version} at {path}.`
stays; a Debug line adds the source. Windows, no `.windows.` in the output: used, with Warning `Git
{version} at {path} is not Git for Windows; VisualCommit is tested with Git for Windows.`

**GitAccess** as 1.2; 1.14's Warning once, at the first search with a stand-in; ChosenMissing's text
`The Git chosen in Settings was not found at {path}.` `ProbeAsync` and `ReadGuidance` as 1.2.
**Guidance**, off the UI thread and only while the screen shows: on Windows
`WingetPath` = `FileLauncher.FindOnPath("winget.exe", PATH)`, the search `cmd.exe` makes; on Linux
`OsRelease`.

**States** (`GitSetupStates.Of(Git, Prompts.MissingHelperPath)`): the screen shows for Missing,
ChosenMissing, TooOld, NotWorking, and HelperMissing until "Continue without it" (this session). A
search that throws is NotWorking without a path, its message as git's words.

**Place.** One `GitSetupViewModel` per window; `AddTab` sets the new `RepoTabViewModel.GitSetup`.
`ShowsGitSetup => Repository is null && !IsOpening && !IsDashboard && GitSetup?.IsShown == true`;
`ShowsWelcome` adds `&& !ShowsGitSetup`. `CommitGraphView`'s row 0 holds `GitSetupView`; the first
button is its focus target (`kr:KeyRegion.IsMain`); it takes no focus itself, and hidden while holding
it gives it to the graph area's target. Layout as the welcome page, `MaxWidth` 520: title (16,
SemiBold), body (`secondary`), the path (`PathTextBlock`), "Git said:" over a `SelectableTextBlock`
(`VcCodeFontFamily`, `VcBorderBrush`, six lines, then a scroll bar), guidance, the command with Copy,
buttons (`VcActionButton`), `GitSetupError`, links (`VcFlatButton`, `VcAccentBrush`). Tokens only.

**Buttons** (W, M, L the guidance's platform; "chosen": the git came from the setting):

| Id | Text | Shown | Does |
|---|---|---|---|
| `GitSetupWingetButton` | `Install with winget…` | W Missing, `WingetPath` set | confirm, then 1.3's start |
| `GitSetupDownloadButton` | `Download Git for Windows` | W Missing, TooOld, NotWorking without a fault | `https://git-scm.com/install/windows` |
| `GitSetupCltButton` | `Install the Command Line Tools` | M Missing with the tools missing; M NotWorking with `xcrun: error` or `requesting installation of command line developer tools` | 1.3's start, no confirm |
| `GitSetupShowFileButton` | `Show the file` | a config fault | `RevealAsync(file)` |
| `GitSetupChooseButton` | W `Choose git.exe…`, M L `Choose the git program…` | all but HelperMissing | Choose |
| `GitSetupUsePathButton` | `Use the Git on the PATH` | ChosenMissing; TooOld, NotWorking when chosen | removes the setting, Check again |
| `GitSetupCheckAgainButton` | `Check again`; `Checking…` disabled while searching | all | Check again |
| `GitSetupContinueButton` | `Continue without it` | HelperMissing | hides the screen |
| `GitSetupCopyButton` | `Copy` | a command shows | the command to the clipboard |
| `GitSetupMoreLink` | `Other ways to install Git` | M, L Missing, TooOld | `https://git-scm.com/install/mac`, `/linux` |
| `GitSetupGuideLink` | `How VisualCommit finds Git`; helper: `What the helper does` | all | `AppLinks.Guide` of the state's page |

**Texts** (`{v}` found, `{min}` `GitVersion.Minimum`; the path in `GitSetupPath`):

| State | Title | Body | Guidance (`GitSetupGuidance`) |
|---|---|---|---|
| Missing W | `VisualCommit needs Git` | `VisualCommit works through the Git installed on this computer, and it found none: not on the PATH, nor in Git for Windows' usual install folders.` | `Install Git for Windows, then press Check again. VisualCommit does not need to restart.` |
| Missing M | same | `VisualCommit works through the Git installed on this Mac, and it found none.` + tools missing: ` Apple's Command Line Tools, which include Git, are not installed.` | tools missing: `Install the Command Line Tools, which macOS asks you to confirm, or install Git with Homebrew, then press Check again. With Homebrew, in Terminal:`; else `Install Git with Homebrew, then press Check again. In Terminal:`; command `brew install git` |
| Missing L | same | `VisualCommit works through the Git installed on this computer, and it found none on the PATH or in the usual folders.` | known: `Install it with your distribution's package manager, in a terminal, then press Check again. On {PRETTY_NAME, else ID}:` + command; unknown: `Install the package named git with your distribution's package manager, then press Check again.` |
| ChosenMissing | `The chosen Git is gone` | `VisualCommit was set to use the Git at this path, and there is no file there now:` | `The Git on the PATH is at {candidate}. Use it, choose another, or press Check again once the chosen one is back.`; none: `No other Git was found on the PATH or in the usual install folders. Choose another, or press Check again once the chosen one is back.` |
| TooOld | `Git {v} is too old` | `VisualCommit needs Git {min} or newer. It found Git {v} at:` | W `Update Git for Windows, then press Check again. Its installer updates it in place.`; M `Update the Command Line Tools in System Settings, General, Software Update, or install Git with Homebrew, then press Check again. With Homebrew, in Terminal:` + `brew install git`; L Ubuntu in `ID` or `ID_LIKE`: `The Git team's PPA for Ubuntu has the newest Git. Add it in a terminal, then press Check again:` + `sudo add-apt-repository ppa:git-core/ppa && sudo apt update && sudo apt install git`; L other: `Install a newer Git from your distribution, or choose another Git, then press Check again.` |
| NotWorking | `Git is not working` | `VisualCommit found Git at the path below, but it did not report its version.`; empty output: `…but it said nothing and exited with code {n}.`; not started: `…but it could not be started: {message}` | fault: `Git cannot read line {n} of {file}. Correct that line, or rename the file, then press Check again.`; W `Repair or reinstall Git for Windows, then press Check again.`; M tools broken: `Apple's Command Line Tools look damaged. Install them again, or select them in Terminal, then press Check again:` + `sudo xcode-select --switch /Library/Developer/CommandLineTools`; M `Reinstall Git, then press Check again.`; L `Reinstall git with your distribution's package manager, then press Check again.` |
| HelperMissing | `Part of VisualCommit is missing` | `Git works, but VisualCommit's helper for git's questions is missing. It was looked for at:` | `Without it, git and ssh cannot ask for a password, a passphrase or a host key here, and a signed-in account cannot be used for git. Install VisualCommit again to restore it.` |

Linux commands by `ID`, then each `ID_LIKE` token: `debian`, `ubuntu`: `sudo apt install git`;
`fedora`, `rhel`, `centos`: `sudo dnf install git`; `arch`: `sudo pacman -S git`; `suse`, `opensuse`,
`opensuse-*`: `sudo zypper install git`; else unknown.

**winget** (rulings 6, 44): the confirm is `ConfirmationRequest("Install Git with winget?", "VisualCommit
opens a terminal window that runs winget install --id Git.Git -e --source winget", "winget downloads Git
for Windows from its community repository, which the Git for Windows project does not maintain, and runs
the installer; Windows may ask for permission. When it has finished, close the window and press Check
again.", "Install", IsDestructive: false)`; Install starts 1.3's `cmd.exe /K` command, not waited for,
logged Info `Started in a terminal: cmd.exe /K winget install --id Git.Git -e --source winget`.
`StartAsync`: `cmd.exe` from the System folder, other bare names by `FindOnPath`; no file:
`FileNotFoundException`; `InNewTerminal` on Windows sets `UseShellExecute`. The tools' start logs
`Started: /usr/bin/xcode-select --install`. A failed start: `{winget|xcode-select} could not be
started: {message}` in `GitSetupError`.

**Choose**: the picker (`Choose git.exe`; `Choose the git program`); on Windows a name without `.exe`
gets `SettingsTexts.ProgramNotExe` unrun; `GitSetupError` reads `Checking…`; `ProbeAsync`. Found:
`settings.Update(GitExecutable = path)`, Check again. Else, settings untouched, `GitSetupError`: `Git
{v} at this path is too old: VisualCommit needs {min} or newer.`, `This Git did not report its
version. Git said: {first line}`, `This file could not be started: {message}` or
`SettingsTexts.ProgramMissing`.

**Check again** (`MainWindowViewModel.Git.cs`), 1.2's order: `Looking for Git...`; the search; `Git`,
`GitStatusText`, `GitStatusToolTip`; `CheckHelper()` off the UI thread; the screen. Found:
`ForgetToolLookup`, the merge tools' lookup after the reopenings; each tab with `FailedPath`:
`ReopenAsync()` (new: `Welcome.ClearGitError()`, `ShowPending`, `OpenAsync(remember: false)`); each
empty tab `Welcome.ClearGitError()` (new: drops an error from `GitUnavailableException`); the active
dashboard's `RefreshCommand`. In HelperMissing git works, so Check again looks for the helper only.
`InitializeAsync` uses the same refresh. The button is disabled while it searches, which drops the
focus (keyboard.md D-a): when the search ends with the focus on nothing or the window,
`GitSetupCheckAgainButton` takes it again (`Tab` if it had `:focus-visible` when pressed, else
`Unspecified`), or, the screen gone, the graph area's target.

**Status bar.** `GitStatus` unchanged; new texts `Chosen Git not found`, `Git {v} (helper missing)`;
`ToolTip.Tip` `{path}, found on the PATH`, `{path}, found in Git for Windows' install folder` (else `in
a usual install folder`), `{path}, chosen in Settings`, `{path}, from VISUALCOMMIT_GIT_STAND_IN (a test
switch)`; null without a path. `GitSetupButton` (`VcFlatButton`, hidden but in the screen's states):
`Set up Git…`, or `Details…` (HelperMissing, undoes Continue); activates the first tab showing the
screen, else a new tab.

**Settings row** (rulings 40, 42): last in the global scope, in every state of the Git section, never
in "This repository"; `Grid 132,12,*`, top margin 18. Label `Git program`; `GitProgramPath`
(`TrimmedTextBlock`, leading ellipsis): the pending choice, else the git in use, else `None found`;
`GitProgramNote`: `Found on the PATH.`, `Found in Git for Windows' install folder.` (else `Found in a
usual install folder.`), `Chosen here.`, `Chosen here. There is no file at this path.`, `From
VISUALCOMMIT_GIT_STAND_IN, a test switch.`, `No Git was found on the PATH or in the usual install
folders.`; then, while the choice differs from the git in use, ` VisualCommit uses it from its next
start.` (git works) or ` VisualCommit looks for Git again when you save.`; `GitProgramBrowseButton`
`Browse…` (Choose's checks; `GitProgramStatus`); `GitProgramUsePathButton` `Use the Git on the PATH`
while a path is chosen. Save's step 2 writes `GitExecutable`. After the screen closes, a changed setting
while git does not work runs Check again.

**Netrc note** (ruling 8): `AccountsNetrcNote` (`settingsNote notice`, `VcWarningBrush`) and
`AccountsNetrcDismiss` `Dismiss`, under the blocks, while an account is signed in with "Use for git
over HTTPS" saved on, git is below 2.46.0, and `HomeHasNetrc()` (off the UI thread) said yes: `Your home
folder has a .netrc or _netrc file. With Git {v}, an entry in that file for a host can answer git's
requests to it before your signed-in account. Git 2.46 or later stops this for git's own requests;
git-lfs reads the file with every version of Git.` `HomeHasNetrc`: either file in
`GitConfigLocations.Home(gitSettings.Variable, Directory.Exists, IsWindows)`; never its content.
Dismissed for the session.

**ssh line** (ruling 7): `AskPassBroker`, once per session, at its first Ask or Deny scope, Info `Git
and ssh get SSH_ASKPASS_REQUIRE=force, so that ssh asks through VisualCommit; OpenSSH older than 8.4
ignores it and asks in the terminal VisualCommit was started from, if there is one.`

**Helper**: `HelperFound` volatile; `CheckHelper()` looks again, Info `The askpass helper is there now
({path}).`

**FakeGitSetupProbe** (1.15) shows made-up values: `C:\Program Files\Git\cmd\git.exe`,
`C:/Users/demo/.gitconfig`; Ubuntu 22.04 (`ubuntu`, [`debian`], `Ubuntu 22.04.5 LTS`), Alpine.

### 5.4 Tests

Fail first: the lookup, the stand-in, the macOS rule, `ConfigFault`, the reopening, `StartAsync`,
`CheckHelper`, Choose's write, the row, the note.
- Git.Tests: `GitLocatorTests` (added: the order; a gone setting; the stand-in, `none`; the macOS rule
  through the context; TooOld, Broken 128 and the "answers only" case through the stand-in; the Windows
  Warning; real git with `[user` in `GIT_CONFIG_GLOBAL` gives `ConfigFault(file, 1)`, on every CI job);
  `OsReleaseTests` (quotes, escapes, `#`, a later key wins, the `/usr/lib` file), `GitStandInTests`,
  `AskPassBrokerHelperTests` (the ssh line once), `GitConfigurationForgetTests`.
- App.Tests, no session: `GitAccessTests` (waiters, swap, joining, `Changed`, `ProbeAsync`),
  `CheckGitAgainTests` (the order, reopening, errors cleared, tools asked again, HelperMissing keeps
  git), `GitSetupTextsTests` (state × platform, `{min}`, the commands), `GitSetupViewModelTests`,
  `GitProgramViewModelTests`, `AccountsNetrcNoteTests`; added to `FileLauncherTests`,
  `JsonSettingsStoreTests` (`gitExecutable` absent, written, read back), `MainWindowViewModelTests`,
  `SettingsDialogViewTests`.
- 8a's project: `GitSetupShellTests` (each state: no clipped text at 1100×700 and 1000×560).
- Earlier: `ShellTests`' NotFound window now shows the screen and stays green unchanged.

### 5.5 Part F

Dark, 1100×700, the fake probe and launcher unless named. 14 checks; RW the real window repeats.

| Check | Steps | Expected |
|---|---|---|
| F1 Missing (RW) | W with winget, one restored tab | the texts and buttons; `Git not found`, `Set up Git…`; a New tab; no git call; `f1a-missing-windows`, `f1b-missing-windows-light` |
| F2 Offers (RW: confirm, Cancel) | Install…, Cancel; Install; Download | `f2a-winget-confirm`; no start after Cancel; one `Start`, 1.3's, `InNewTerminal`; one `OpenUrl` |
| F3 Chosen gone | ChosenMissing; Use the Git on the PATH | `f3a-chosen-gone`; then the key gone, screen gone |
| F4 Found again (RW: native dialog) | Missing, then Real; Check again | `Checking…`; the tab's graph; status, tooltip; tools asked; `f4a-found-again` |
| F5 Too old (RW: stand-in) | 2.29.0 | `{v}`, `{min}`, path, Download, no winget; `f5a-too-old` |
| F6 Config fault (RW: stand-in) | Show the file | git's line, `line 1 of C:/Users/demo/.gitconfig`; `Reveal`; `f6a-config-fault`. F6b (RW): real git, `[user` in the session's global file: `fatal: bad config line 1 in file`, no picture |
| F7 Not started | | `could not be started: {message}`; `f7a-not-started` |
| F8 Choose | too old; then `RealAt(D:\Tools\PortableGit\cmd\git.exe)` | refusal, settings.json untouched, `f8a-choose-too-old`; key written, screen gone |
| F9 macOS | tools missing; Install; Copy; `xcrun: error` | `f9a-macos-missing`; `Start` of `xcode-select --install`, no confirm; `brew install git` copied; `f9b-macos-broken` |
| F10 Linux | Ubuntu, Alpine Missing; Ubuntu TooOld | `f10a-ubuntu-missing`, `f10b-alpine-missing`, `f10c-ubuntu-too-old` |
| F11 Helper (scripted only) | no helper file; Continue; `Details…` | `f11a-helper-missing`; `Git {v} (helper missing)`; welcome page; screen back |
| F12 Settings row | Browse, Save; reopen; Use the Git on the PATH; Browse too old | `f12a-row-next-start`; the key written, then removed; `f12b-row-too-old` |
| F13 Keys (RW) | F6, Tab, Enter on Check again | the first button; all in order; Enter acts; after the search Check again has the focus again |
| F14 Netrc | signed in for git, `RealAt` saying 2.43.0, `_netrc` in the session's home; Dismiss; reopen | `f14a-netrc-note`; gone; no file, no note |

`Phase8aExpectations.F` holds, for §12 (12.3.4 launches 7 to 10): F1's and F4's scenario and session,
F5's and F6's, F13's focus order with winget found, the picker's title `Choose git.exe`, F5's version
line `git version 2.29.0`, F6's failure text, the confirm's title and the picture names.

### 5.6 Earlier pictures

Ruling 40's nine: `s14c`, `s14i`, `s18a`, `s18d`, `s19a`, `s19d`, `s20b`, `l31e-dark`, `l31e-light`, at
the global scope's bottom; none else. Proof:
phase 6's parts S and L and phases 0 to 2's walk-through through `scripts/compare-pictures.py` against
`references/wave1/`: only the nine differ, there. Any other difference stops the package (ruling 3).

### 5.7 Lagged list, time, real window

Lagged list: 0.4's last line, now with part F and `GitSetupShellTests`. Default run: part F 3 to 4
minutes alone; App.Tests about 60 tests, Git.Tests about 35, a minute each. Real window (§12): four
launches (F1, F2's confirm, F13, F4; F5; F6; F6b), 3 to 4 minutes; the stand-in from `RealWindowTests`'
output, its variable set last; no press of Install, Download, Copy or a link (ruling 47 for the path).

### 5.8 Done when

5.4 passes (fail-first tests failed first); part F on the laptop and in CI's six jobs (F6b on 2.34.1);
5.6 holds; no warning; no test reads the machine's PATH, home or settings; the lead's `cmd.exe /K echo`
(1.3) stayed open; part S's settings.json has no `gitExecutable`.

### 5.9 Risks, unverified, open

- Unverified: the `cmd.exe` window where Windows Terminal is the default console, and `File.Exists`
  on winget's alias: the lead by hand before the merge; without winget F1's real picture lacks the
  button. A Mac (`xcode-select -p`, the shim, `--install`): 8b's checklist. git-scm.com's pages and
  commands: read by the input only.
- The note reads git's home only: curl's `NETRC`, or Windows without `HOME`, can escape it; the
  guide says so.
- No "Other ways to install Git" link on Windows: the download button opens that page. "Continue
  without it" and the note's Dismiss last for the session; neither writes `settings.json`.
- Open points: 13.4, items 6 and 7.

## 6. keyboard: the keyboard pass and the focus fixes (R7)

Wave 2, after §2's merge; branch `wip/p8a-keyboard`, part K. Carries R7, D392, D192, D199, D213,
D417, rulings 3, 11, 49 to 53, plan.md's lines 759, 771, 772, 774 and 784. Facts are
keyboard.md's (file and line on `e04762b`), checked again on `27c5109`.

### 6.1 What the package delivers, and what it leaves

Delivers keyboard.md's F1 to F7, each proven by a test written first that fails on today's code
(ruling 11): F1 Tab reaches the diff's hunk bars; F2 the arrows pick a blame run; F3 the keyless
palette entry "Cancel {title}"; F4 a stage row's buttons are Tab stops, drawn while focused; F5 (F5-A)
the focus comes back after "Choose"; F6 a loading diff takes the focus only if the user has not moved
it; F7 the commit clears only what it committed, and Amend is disabled while it runs. Also
`AcceptsTab` off on every read-only text view (ruling 52); tests of the paths that exist (G1 lines by
keys, G7 the walk to Cancel, G8 a splitter, G9 the providers' switch); the inventory for the report
(6.3.9); part K and the outline of its real-window K for §12.

Leaves: to §9 the row buttons' and splitters' names (1.12); to §11 the guide's lines (6.3.10, ruling
53); to §12 the real-window code; to the lead the report's inventory. Out of the phase: any new key
(D192, D213), keyboard selection of commits apart (ruling 50, Backlog), a drawn diff caret (it would
change phase 2's `07c-large-end`).

Decisions: D425 (amends ruling 49's F4), a row's buttons on a focused row only, since opacity on
every row would put every row's buttons into UI Automation, where `Phase2RealWindowPass.ClickRowButton`
(`:364`) and `Phase5RealWindowPass.ClickFileRowButton` (`:369`) wait for exactly one; D426 (rulings 49,
52; phase 4 design 11.10 carried over), Tab out of every read-only text view and the hand-over before
a focused hunk bar leaves (otherwise Avalonia picks the next focus, `MainWindow.axaml.cs:302-304`);
D427, a keyboard move of `LeftSplitter` or `RightSplitter` fitted and remembered as a drag's end is
(`PanelDragged`, `MainWindow.axaml.cs:37`), built only where G8's test shows `GridSplitter`'s arrows
do not already do it; the activity log's splitter goes to a fixer (0.4).

### 6.2 Files and contracts

Changed (each exists), under `src/VisualCommit.App/`: `Controls/Diff/DiffTextView.cs` (TabNavigation,
the bars' `GotFocus` handler, `FocusBody(Func<bool>? stillWanted = null)`, the hand-over before a
focused bar leaves); `Controls/Diff/DiffPane.cs` (`AcceptsTab = false`); `Controls/Diff/HunkHeaders.cs`
(`HunkHeaderLayer` gains an internal event `BarRemoving`, new, raised before a bar leaves in `Update`
and `Rebuild`; the resolver does not listen); `Views/Diff/DiffView.axaml.cs` (F6);
`Controls/Blame/BlameTextEditor.cs` and `Views/Pages/BlameView.axaml.cs` (F2);
`ViewModels/Commands/AppCommands.Palette.cs` (F3, from WP0); `Views/Shell/WorkingChangesView.axaml`
(from §2) and `.axaml.cs` (F4, F7's focus); `ViewModels/Panels/WorkingChangesViewModel.cs` (F7);
`Views/Shell/WelcomeView.axaml.cs`, `Views/Dialogs/CreatePullRequestDialogView.axaml.cs` (F5);
`Views/MainWindow.axaml.cs` (from §4; D427, if needed); `Views/Shell/ActivityLogView.axaml`
only if 6.3.8's test fails.

New: `Views/FocusReturn.cs` (6.3.5); `tests/VisualCommit.Testing/Headless/HeldGitRunner.cs` and
`ShellDriver.Phase8a.Keyboard.cs`; in 8a's project `KeyboardPassTests.cs`, `Phase8aChecks.K.cs`,
`Phase8aExpectations.K.cs`, `Phase8aExpectationTests.K.cs`. Test files changed (App.Tests):
`DiffTextViewTests.cs`, `ConflictPanesTests.cs`, `BlameViewTests.cs`, `WorkingChangesTests.cs`,
`StatusBarTests.cs`; and `tests/VisualCommit.VisualTests/Phase2/Phase2Checks.Helpers.cs`.

Contracts: implements 1.8's sentence for §6 and 1.15's `HeldGitRunner`; uses 1.15's
`ShellDriver.Start(gitProbe:)`, 1.2's `IGitSetupProbe`, `MachineGitSetupProbe`, `GitDetection`, 1.15's
`Phase8aChecks.Support.cs`.

### 6.3 Behaviour

**6.3.1 F1, the diff.** In `DiffTextView`'s constructor, for `_left` and `_right`: TabNavigation
`Continue` on the editor and its `TextArea`, and `pane.Headers.AddHandler(GotFocusEvent, (_, e) =>
e.Handled = true)` (as `ConflictPanes.cs:198-208`). With `AcceptsTab = false`:
- Inline, Tab from the text goes to the first hunk in view's buttons ("Stage hunk", "Discard hunk";
  "Unstage hunk" on a staged diff), then the next hunk's, then out of the diff. Side by side the left
  bars have no buttons: Tab goes to the new file's text, then to the hunks' buttons.
- Shift+Tab from the text goes to the toolbar's last enabled button ("Discard file" on an unstaged
  diff with no lines selected, "Unstage file" on a staged one).
- Enter or Space runs the focused button, which shows Fluent's frame (Tab only).
- On `BarRemoving`, when the bar holds the focused element and it has `:focus-visible`, or raised
  the action while it had, the left text area takes the focus (`Unspecified`) before the bar leaves.
  After "Stage hunk" by Enter the focus is in the text; a "Discard hunk" cancelled in its question
  keeps the bar, and D104 gives the button its focus back.
- F6 and Ctrl+End are unchanged (`KeyRegion.IsMain` on the left editor).

**6.3.2 F2, the blame.** `BlameText.AddHandler(KeyDownEvent, OnTextKeyDown, RoutingStrategies.Bubble,
handledEventsToo: true)`: after Up, Down, Left, Right, PageUp, PageDown, Home or End, with or without
Ctrl or Shift, the run of `TextArea.Caret.Line` (`BlameGutter.RunOfLine`) is picked
(`BlameViewModel.Pick`) when it is not the picked one: the gutter draws it, the right panel shows its
commit. No other key picks. `OnContextRequested`'s keyboard branch takes `SelectedRun ?? the caret's
run`, so Shift+F10 right after opening shows run 0's menu. Opening picks nothing (b6a to b6h come from
clicks). Enter does nothing new; "Show in graph" is in the run's menu. Tab leaves the text.

**6.3.3 F3, Cancel.** The entry of 1.8. On screen: "Cancel Fetch origin", no key, the repository's
name as detail while dialogs name it (KD2). Afterwards the status bar reads "Fetch origin cancelled"
(`OperationTexts.Cancelled`). `AppShortcutsTests`' lists stay unchanged.

**6.3.4 F4, the stage rows** (D425). The styles at `WorkingChangesView.axaml:77-88`
become, in this order: `Panel.stageRow StackPanel.rowActions` hidden; shown on `:pointerover`,
`.selected` and `:focus-within` (new); `.folder` hidden; then
`Panel.stageRow:focus-within:not(:pointerover):not(.selected) StackPanel.rowActions Button` opacity 0;
last `Panel.stageRow StackPanel.rowActions Button:focus-visible` opacity 1.
- F6 still gives the row's own button (`KeyRegions.cs:237-243`): `k12b`'s state, nothing drawn.
- Tab from it: "Stage" then "Discard" (unstaged) or "Unstage" (staged), each drawn and framed, then
  the next row. Shift+Tab reaches a row's own button, not its buttons. The panel is an overlay.
- After a row action started by keyboard (the button had `:focus-visible`), once the lists show its
  result, the focus is on the row now at its place in that list, else that list's last row, else the
  other list's first row, else the summary (`Tab`, 6.3.5). A Discard cancelled in its question gives
  the focus back to its row.

**6.3.5 F5, the focus after "Choose"** (F5-A: D104's rule where the opener was disabled). New
`FocusReturn.AfterRun(IAsyncRelayCommand command, Control scope, Func<InputElement?> target,
NavigationMethod method)`: when `command` stops running, at `Loaded` priority, if the focused element
is null, the window or outside `scope`, `target()` takes the focus when shown and enabled.
- `WelcomeView`: `BrowseCommand`, scope `CloneForm`, `CloneFromGitHubButton`, `Unspecified`: after
  Choose or Esc in the browser, "From GitHub…" (or "From an account…") has the focus, with no frame;
  the palette's path keeps the focus where D104 put it.
- `CreatePullRequestDialogView`: `ChooseBaseCommand`, the dialog's view, `PullRequestBaseChooseButton`,
  `Unspecified` (ruling 51). If 7a's or 7b's part N pictures change, it comes out as a known issue.
- `WorkingChangesView`: the row actions (6.3.4) and Amend (6.3.7), `Tab`, armed only when the element
  had `:focus-visible`.
`MainWindow.OnDialogRemoving` does not change.

**6.3.6 F6, the loading diff.** `DiffView` keeps `_focusAtOpen`, taken when the open's post runs: the focused element outside menus, as
D104 takes it (the window's `MenuLayer.FocusOutsideMenus`, `MainWindow.axaml.cs:336`). Both posts call
`DiffText.FocusBody(StillWanted)`; `StillWanted()` holds while the focused element is null, the
window, `_focusAtOpen` or inside this `DiffView`; `TryFocus` drops the request once it is false. A read
held while the user presses Mod+Shift+M leaves the focus in the summary; an `Empty` body that turns to
`Text` while the user types does not take it. `FocusBody()` keeps today's behaviour, and so does the
file history's diff (`TakesFocusOnOpen=false`).

**6.3.7 F7, the commit boxes.** `Commit()` keeps `(Summary, Description)` as read for `JoinedMessage`
and `IsAmend` before the await; on success it empties both boxes only if both still hold those (as
`TakeBackRedoneMessage`), and unticks Amend only if still ticked. New `CanToggleAmend => CanAmend &&
!IsCommitting` (notified by both), bound at `WorkingChangesView.axaml:419`. The boxes stay enabled:
the summary keeps its focus and accent border on a Mod+Enter commit. Amend focused by keyboard when
the commit starts gets the focus back once enabled (6.3.5). A failed commit keeps the boxes, as today.

**6.3.8 Read-only text views** (ruling 52). `AcceptsTab = false` on `DiffPane` and `BlameTextEditor`.
The activity log's output and the details panel are `SelectableTextBlock`s: a test asserts that Tab
leaves them, and only its failure changes `ActivityLogView.axaml`. No earlier check presses Tab inside
one (Grep of `Key.Tab`, `VirtualKeyShort.TAB` in `tests/`).

**6.3.9 The inventory for the report**, returned in the merge report. K a key; P the palette; M
Shift+F10 or Menu on a row that F6 or Tab reaches; T Tab or arrows. Unchanged: toolbar (K, P; theme
P, settings), tabs (K), open, clone, init (K, P), graph selection (F6, arrows, Shift), its menus (M),
left panel (Mod+Shift+E, T, M, P), drags (their menus, ruling 11), working changes' keys (K), a file's
diff (T, Enter), diff close, history, blame (Esc, K), file actions in the diff (M, P), pages (arrows,
M, Esc), search (K), resolver (K, T bar to bar), rebase list (Alt+arrows, p r s f d), conflict banner
(Mod+Enter, P, T), activity log (K, F6, arrows, M), dashboard (K, M), dialogs, settings (K, T).
Changed or proven by 8a:

| Action | After 8a | Proof |
|---|---|---|
| Stage, unstage, discard a hunk | T to its buttons (F1) | K1; `DiffTextViewTests` |
| Inline or side by side | Shift+Tab from the text | K1 |
| Stage or discard chosen lines | F6, Shift+arrows, then M or P | K1; `KeyboardPassTests` |
| A file row's buttons | T from the row (F4) | K4; `WorkingChangesTests` |
| Amend | T; disabled while committing (F7) | K7 |
| Choose a blame run; its menu | arrows; M (F2) | K2; `BlameViewTests` |
| Cancel the running operation | P "Cancel {title}" (F3); a long T walk | K3, K8; `StatusBarTests` |
| Clone browser's Choose | T, arrows, Enter; the focus after (F5) | K5 |
| Resize a panel | T to its splitter, arrows by 10 | K8 |
| Commits not next to each other | the mouse only (ruling 50) | the guide |

**6.3.10 For the guide** (ruling 53), to §11: Tab in a diff reaches the hunks' buttons, Shift+Tab its
toolbar; its caret is not drawn, Shift with the arrows selects rows, then Shift+F10 "Stage selected
lines"; the arrows in a blame pick a run, Shift+F10 its menu; Tab from a stage row reaches its
buttons; "Cancel {title}" in the palette; a focused splitter moves 10 per arrow; commits apart need
Ctrl-click (Cmd on macOS).

### 6.4 Tests

Each class runs alone with plain `dotnet test` (D245). "Fails first": run on the branch's base before
the fix, the failure named in the commit message.

`HeldGitRunner` (1.15) holds `diff` and `commit` calls. A tab's git calls take the detection's
runner (1.2); if the session's calls bypass it anyway, K6 and K7 keep their tests and drop their
pictures.

- `DiffTextViewTests` (DiffHost): Tab from the text focuses the first `StageHunkButton`, then
  `DiscardHunkButton`, then the next hunk's (fails first); side by side Tab reaches `DiffTextRight`,
  then the buttons (fails first); a keyboard-focused button whose bar scrolls out leaves the focus on
  the text (fails first); `FocusBody(() => false)` before the view shows leaves a box's focus alone.
- `ConflictPanesTests`: after a click in the ours text, Tab focuses the first bar's first button
  (fails first).
- `BlameViewTests` (`RecordingPageHost`, three runs): Down into run 2 picks it and `Shown` ends with
  its SHA (fails first); Ctrl+End picks the last; Tab leaves the text (fails first); a click still picks.
- `WorkingChangesTests`: `FakeHost` gains a held `CommitAsync`: "next" typed while committing stays
  and the commit carries the first message (fails first); nothing typed: both boxes emptied, Amend
  unticked, as today; `CanToggleAmend` false while committing. ShellDriver: F6, Tab focuses
  `RowStageButton` at opacity 1, Tab `RowDiscardButton` (fails first); the row alone focused: both at
  opacity 0; Enter on "Stage" stages the file (git's index) and the focus is on the row now first
  (fails first).
- `StatusBarTests` (the held fetch, `:64-71`): the palette's first entry "Cancel Fetch origin", Enter
  gives `OperationOutcome.Cancelled` (fails first); none with nothing running, with a held local
  "Create branch hotfix", or once `CanCancel` is false; from F6 on the right panel at most 200 Tabs
  reach `OperationCancelButton` (G7, passes today).
- `KeyboardPassTests` (8a project, D416): Shift+Tab from the diff's text focuses
  `DiscardFileButton` (fails first); Tab, Enter on "Stage hunk" stages that hunk alone and leaves the
  focus on `DiffText` (fails first); F6, Down, Shift+Down twice, Shift+F10, Enter on "Stage selected
  lines" stages two lines (G1, passes today); a blame just opened answers Shift+F10 with run 0's menu
  (fails first); after Choose and after Esc in the repository browser (fake GitHub, as
  `WelcomeCloneFromGitHubTests`) `CloneFromGitHubButton` has the focus (fails first); after the create
  form's Pick `PullRequestBaseChooseButton` has it (ruling 51; passing today, nothing changes); a held
  `diff` read with Mod+Shift+M before its release leaves the focus in `CommitSummary`, and so does an
  `Empty` body turning to `Text` (fails first); a diff opened by Enter on a row or from its menu still
  takes the focus (passes today); with a held `commit` Amend is disabled and, focused by Tab before,
  gets the focus back (fails first); the providers' switch keeps the focus and Tab goes to
  `CloneFromGitHubFilter` (G9, passes today); Tab reaches `LeftSplitter`, Right widens the left panel
  by 10 and the session's `LeftPanelWidth` holds it (G8; the second half may fail first: D427);
  the same for `ActivityLogSplitter`; Tab leaves `ActivityLogPartText` and `DetailsBody` (ruling 52).
- Changed expectation: `Phase2Checks.Helpers.VisibleRowButtons` counts a button only when drawn
  (`IsEffectivelyVisible && Opacity > 0.5`). Reason: a focused row's panel now shows with its buttons
  undrawn (D425). Check 01's assertion is unchanged.

### 6.5 Part K's checks, in outline

Written into `docs/test-reports/phase-8a.md` just before the package (D244); class `Phase8aChecksK`;
dark 1100×700 unless named; git states read with git.
- **K1 hunk by keys** (real window: yes). Changes scenario, the working row, F6 to the stage row,
  Enter opens `src/Calculator.cs`'s unstaged diff. Tab: the first "Stage hunk" framed (`k1a-hunk-button`).
  Enter: that hunk alone in the index, focus in the text, no frame (`k1b-hunk-staged`). Shift+Tab:
  "Discard file" framed (`k1c-toolbar`). F6, Down, Shift+Down twice, Shift+F10: "Stage selected lines"
  in the menu (`k1d-lines-menu`); Enter stages two lines.
- **K2 blame by arrows** (light; real window: yes). History scenario, a file of three runs or more:
  Mod+Shift+B, Down into run 2: its lines on the selection background, its commit in the right panel
  (`k2a-blame-run`); Shift+F10: its menu (`k2b-blame-menu`).
- **K3 Cancel in the palette**. A held fetch; Mod+Shift+P: first row "Cancel Fetch origin", no key
  (`k3a-palette-cancel`); Enter: "Fetch origin cancelled", no remote ref moved.
- **K4 a row's buttons by Tab**. F6 to the first unstaged row, nothing drawn on it; Tab: "Stage" drawn
  and framed (`k4a-row-stage`); Enter: the file staged, the next row focused (`k4b-row-staged`).
- **K5 the focus after Choose**. Fake GitHub signed in, the clone form, "From GitHub…" by Tab and
  Enter, a row, Enter: URL and name filled, the button focused, no frame (`k5a-chosen`; `b06a`'s form
  outside the URL's text).
- **K6 the loading diff**. A held `diff`: Enter on a row, Mod+Shift+M, release: the diff shows, the
  summary keeps the focus and its accent border (`k6a-summary-kept`).
- **K7 committing**. "First", Mod+Enter, the `commit` held: "Committing…", Amend disabled
  (`k7a-committing`); type "Next"; release: HEAD's subject "First", the summary "Next" (`k7b-committed`).
- **K8 the walks**. With K3's held fetch: F6 to the right panel, Tab until Cancel (the count in the
  report), Enter; then Tab to `LeftSplitter`, Right: the left panel 10 wider (`k8a-splitter`).

`Phase8aExpectations.K` holds, for §12 (12.3.4 launches 11 and 12), the picture names, K1's numstat
after the hunk and after the two lines, and K2's file and its count of Down to run 2.

### 6.6 Earlier pictures

None (ruling 3). By construction: F1 to F3, F6 and F7 draw only after a Tab, an arrow in the blame,
a held operation or a moment no check catches; F4's buttons stay undrawn in `k12b`'s state; F5-A gives
the focus without a frame (`b06a`, `b07a`, `b07b`). Proof: the builder runs phase 2's walk-through,
`Phase5ChecksB`, `Phase6ChecksK`, `Phase7aChecksB`, `Phase7aChecksN`, `Phase7bChecksB`, `Phase7bChecksN`
and compares them with `scripts/compare-pictures.py` against
`D:/Projects/Personal/phase8-work/references/wave1/`: no difference. A difference stops the package
(1's rule), but in part N, where the create form's fix comes out (ruling 51).

### 6.7 Lagged list, time, the pass

Lagged list: nothing new; 0.4's holds `*Phase2Checks`, `DiffTextViewTests`, `WorkingChangesTests`,
`StatusBarTests` and 8a's project. Default run: about 25 tests in App.Tests (most on a host, about a
minute), 8 checks and about 14 tests in 8a's project (3 to 4 minutes alone). Real-window pass: one
launch for K1 and K2, about 45 s (§12).

### 6.8 Done when

1. Each "fails first" test failed on the base, as its commit says, and passes; 6.4's classes and
   `RegionKeysTests`, `CommandPaletteViewTests`, `PaletteEntriesTests`, `AppShortcutsTests` pass alone.
2. Part K passes, each picture inspected; 6.6's comparison shows no difference.
3. CI's run of the merge is green in every job (the floor's as §3 leaves it).
4. The inventory and 6.3.10 are in the merge report.
5. With the owner told, the lead runs `--filter-method "*Phase2RealWindowPass.Check_17*"` and
   `"*Check_A4_real_keys*"` alone on the laptop: both pass (D425's reason).

### 6.9 Risks, the unverified, open points

1. Unverified: Tab from a focusable `TextArea` with `Continue` enters the bars, then leaves; Enter
   reaches a button inside the `TextArea`. The fail-first tests settle it.
2. Unverified: `:focus-within` with `:not()` in Avalonia 12.1.3 (the app uses `:not()` and
   `:focus-visible` only); the opacity tests settle it.
3. Unverified: `GridSplitter`'s arrows (10 per press, keyboard.md) and whether they raise
   `DragCompleted`; G8's test.
4. Unverified: that `SelectableTextBlock` leaves Tab alone; 6.3.8's test.
5. Read so in the code, proven by `HeldGitRunner`'s first test: that a tab's git calls use the
   detection's `Runner` (`GitRepositoryProvider.cs:57-81`).
6. F6's rule must allow the window's own moves (D104, the menu's return); a guard test covers them.
7. F2 picks on every caret move, as the graph's arrows select on each.

K5 answers 7a's report on where the focus returns; 7a's B6 and 7b's B7 stay as they are. Open point:
13.4, item 8.

## 7. icon: the icon's pipeline and a placeholder mark (R5)

### 7.1 What the package delivers

Status step 10; wave 2, `wip/p8a-icon`, after §4's merge (the notice window). R5, D407, D412;
rulings 3, 17; plan.md lines 764 ("Icons and branding") and 782 ("the icon in the window and the
taskbar"). Its decisions: D428 (ruling 17, D407), the masters, the tool on Svg.Skia 4.9.1 (5.x needs
SkiaSharp 4.x, which Avalonia 12.1.3 does not take), the committed files and the placeholder; D429,
the three masters and the sizes; D430, the files checked by format, size and the masters' hashes,
never by pixels.

- Three SVG masters in `assets/brand/` (new), holding a placeholder mark until the owner chooses, so
  that no package waits on the choice (ruling 17).
- `tools/VisualCommit.IconBuilder` (new), a C# console tool on Svg.Skia 4.9.1 that renders from the
  masters the `.ico`, the `.iconset` that 8b turns into the `.icns`, the PNG sizes for Linux, the two
  PNGs the app shows and a manifest. The generated files are committed. The tool is never shipped
  and never run by a test or by CI, which builds it with the solution.
- The executable's icon (`ApplicationIcon`) and the window icon of the main window and of §4's
  notice window, from one embedded `.ico`; `AppIcon`, the resource paths §8's About uses.
- `IconFilesTests`, `WindowIconTests`, the reader `IconFile`; part B's two checks and the outline of
  their real-window repeats.

Left to others: the owner's choice and the regeneration (the lead, 7.8); the README's "Name" note
(7.3), CLAUDE.md's row, the slnx line, `architecture.md` (the lead); About's image (§8); the
real-window code (§12). Out of 8a (ruling 17, D399): the `.icns` itself (`iconutil` on 8b's macOS
runner), macOS 26's layered `.icon`, `Info.plist`, `.desktop` and hicolor tree, installers' images.

### 7.2 Files

New:
- `assets/brand/icon.svg`, `icon-small.svg`, `icon-macos.svg` (7.3).
- `assets/brand/generated/VisualCommit.iconset/` (ten PNGs), `assets/brand/generated/png/icon-<N>.png`
  (eight), `assets/brand/generated/manifest.json`. `bin/` and `obj/` are git-ignored, so no output
  folder takes those names; `.gitattributes` already marks `*.png` and `*.ico` binary.
- `src/VisualCommit.App/Assets/Icon/VisualCommit.ico`, `VisualCommit-48.png`, `VisualCommit-96.png`.
- `src/VisualCommit.App/Services/AppIcon.cs` (1.6).
- `tools/VisualCommit.IconBuilder/VisualCommit.IconBuilder.csproj` (`Exe`, `IsPackable` false, no project
  reference; package references `Svg.Skia`, `SkiaSharp`, `SkiaSharp.NativeAssets.Linux`,
  `HarfBuzzSharp.NativeAssets.Linux`), `Program.cs`, `IconRenderer.cs`, `IcoWriter.cs`, `IconOutputs.cs`
  (7.3's lists, in one place).
- `tests/VisualCommit.Testing/IconFile.cs` (1.15), plain .NET, no Avalonia (the real-window project
  uses it); its lists are the tests' copy of 7.3's.
- `tests/VisualCommit.App.Tests/IconFilesTests.cs`, `WindowIconTests.cs`.
- `tests/VisualCommit.VisualTests.Phase8a/`: `Phase8aChecks.B.cs` (`Phase8aChecksB : Phase8aChecks`),
  `IconSheet.cs` (a harness control), `Phase8aExpectations.B.cs`, `Phase8aExpectationTests.B.cs`.

Changed:
- `Directory.Packages.props`: a group "Tools": `Svg.Skia` 4.9.1, `SkiaSharp` 3.119.4,
  `SkiaSharp.NativeAssets.Linux` 3.119.4, `HarfBuzzSharp.NativeAssets.Linux` 8.3.1.3; its comment says
  Svg.Skia 5.x needs SkiaSharp 4.x, the other three are what Avalonia.Skia 12.1.3 resolves and move
  with `AvaloniaVersion`, and the tool is never shipped. No transitive pinning is set, so no other
  project's graph changes.
- `src/VisualCommit.App/VisualCommit.App.csproj`: `<ApplicationIcon>Assets\Icon\VisualCommit.ico</ApplicationIcon>`
  in the first group; `<AvaloniaResource Include="Assets\Icon\**" />` beside the fonts' line, with a
  comment naming D407 and the tool.
- `src/VisualCommit.App/Views/MainWindow.axaml`: `Icon="/Assets/Icon/VisualCommit.ico"` after `Title`.
- §4's notice window: the same attribute in its `.axaml` (its name is §4's).

Section 1 used: 1.1 (the tool's row), 1.11 (the notice window), 1.15 (`Phase8aChecks.Support.cs`,
`Phase8aExpectations.Common.cs`, the pattern of 0.3).

### 7.3 Behaviour: the masters, the tool, the outputs

**The masters' rules** (any mark, the placeholder and the owner's choice alike; web-brand 1.2, 1.4,
1.5; R5):
- Paths and basic shapes, solid fills, at most simple linear gradients. No `text`, `tspan`, `image`,
  `filter`, `script`, `style`, `foreignObject` or `pattern` element; no `href`, `xlink:href` or
  `url(…)` that points outside the file (anything but `#id`).
- `icon.svg` and `icon-macos.svg` have `viewBox="0 0 1024 1024"`; `icon-small.svg` has
  `viewBox="0 0 16 16"`, its straight edges on whole units.
- Transparent around the shape. No Git logo, no orange-red rotated square, no bare branch or merge
  glyph as the main shape, no letter G, nothing of another client's.
- `icon-macos.svg` is the mark as an opaque squircle of about 824 on the 1024 canvas, with a margin of
  about 100 (web-verify B5's form for macOS before 26). It is not promised to escape macOS 26's grey
  plate; the layered `.icon` is 8b's.
- Each master's first comment says what it is: `<!-- Placeholder mark (D428): replaced by the
  owner's choice (D407). -->`, after the choice `<!-- VisualCommit's mark, chosen by the owner on
  <date> (D407). -->`.

**The placeholder**, inside each master's `svg` element:

```xml
<!-- icon.svg -->       <rect x="64" y="64" width="896" height="896" rx="208" fill="#6170F0"/><circle cx="512" cy="512" r="200" fill="#FFFFFF"/>
<!-- icon-small.svg --> <rect x="1" y="1" width="14" height="14" rx="3" fill="#6170F0"/><circle cx="8" cy="8" r="3" fill="#FFFFFF"/>
<!-- icon-macos.svg --> <rect x="100" y="100" width="824" height="824" rx="185" fill="#6170F0"/><circle cx="512" cy="512" r="184" fill="#FFFFFF"/>
```

A blue tile with a white disc: none of the five candidates and no Git symbol. By the designer's
arithmetic `#6170F0` is about 3.9:1 against a dark desk `#202020` and 3.7:1 against a light one
`#F3F3F3` (roughly Windows 11's taskbars, not measured), and the disc 4.1:1 on the tile, so most of
the mark holds 3:1 on both (Microsoft's rule, web-brand 1.4).

**The outputs** (`IconOutputs`; D429):

| File | Sizes | From | Format |
|---|---|---|---|
| `src/VisualCommit.App/Assets/Icon/VisualCommit.ico` | 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256 | `icon-small.svg` to 32, `icon.svg` above | 256 a PNG; the others 32-bit BGRA bitmaps, bottom-up, height doubled in the header, an AND mask of zeros |
| `…/Assets/Icon/VisualCommit-48.png`, `-96.png` | 48, 96 | `icon.svg` | PNG, RGBA, 8 bits |
| `assets/brand/generated/png/icon-<N>.png` | 16, 24, 32, 48, 64, 128, 256, 512 | as the `.ico` | PNG |
| `assets/brand/generated/VisualCommit.iconset/<name>.png` | `icon_16x16` 16, `icon_16x16@2x` 32, `icon_32x32` 32, `icon_32x32@2x` 64, `icon_128x128` 128, `icon_128x128@2x` 256, `icon_256x256` 256, `icon_256x256@2x` 512, `icon_512x512` 512, `icon_512x512@2x` 1024 | `icon-macos.svg` | PNG |
| `assets/brand/generated/manifest.json` | | | below |

Linux's scalable icon is `icon.svg` itself (8b copies it). The manifest has no time stamp, so a run
with nothing changed writes the same bytes:

```json
{ "v": 1, "renderer": "Svg.Skia 4.9.1, SkiaSharp 3.119.4",
  "masters": { "icon.svg": "<sha256>", "icon-small.svg": "<sha256>", "icon-macos.svg": "<sha256>" },
  "files": { "src/VisualCommit.App/Assets/Icon/VisualCommit.ico": [16, 20, 24, …, 256], "…": […] } }
```

A hash is the lower-case hex SHA-256 of the master's UTF-8 text with `\r\n` read as `\n`, so a
Windows checkout's line ends do not count (`*.svg` is text by `.gitattributes`).

**The tool**, `dotnet run --project tools/VisualCommit.IconBuilder` from any folder of the repository:
1. The root: the nearest folder upward that holds `VisualCommit.slnx`, or `--root`.
2. Every master is checked by the rules above before anything is drawn.
3. Each size: an `SKSurface` of N×N, transparent, the master's `SKPicture` scaled by N over the
   viewBox, antialiased; PNG by SkiaSharp's encoder.
4. All is written to a temporary folder, then moved into place, the manifest last; a file in the
   generated folders that is not in the list is deleted. A failure leaves the committed files as they
   were.
5. Per `.ico` size it prints the share of opaque pixels at 3:1 on `#202020` and on `#F3F3F3`, for the
   owner's rounds; nothing asserts it.

Its console texts (the app shows none of them):
- `Usage: dotnet run --project tools/VisualCommit.IconBuilder [-- --root <the repository's folder>]` (exit 2)
- `No VisualCommit.slnx above {folder}: give the repository's folder with --root.` (exit 2)
- `{path}: is missing.`, `{path}: is not well-formed XML: {message}`, `{path}: has the viewBox "{found}";
  a master needs "{expected}".`, `{path}: holds a <{element}> element, which a master may not use.`,
  `{path}: refers outside itself: {value}` (exit 1, nothing written)
- `Wrote {path} ({n} sizes: {sizes}).`, `Wrote {path}.` (the manifest), `{size} px: {a} % of the opaque
  pixels at 3:1 on #202020, {b} % on #F3F3F3.`, `Done. Commit assets/brand and src/VisualCommit.App/Assets/Icon.` (exit 0)

**The app** shows no new text; the title stays "VisualCommit"; no control gains a name (§9's sweep
sees nothing new). Avalonia hands the `.ico` to the platform (Windows' small and big icons; headless,
a stub). It is embedded three times (the apphost's and the DLL's Win32 resources, an Avalonia
resource), about 0.6 MB; the build output gains no file (1.1).

**The README's "Name" note** (D412), which the lead writes at the merge, a trademark reservation only
that keeps the files free to redistribute (web-verify B8):

> The MIT licence covers the code, and the icon's files in `assets/brand/` and those made from them,
> so a package of VisualCommit may carry them. It does not cover the name or the icon as marks: if
> you redistribute a changed version of VisualCommit, please give it another name and another icon,
> so that users can tell your version from this one.

### 7.4 Tests

The builder runs these classes and part B's only (D245).

`IconFilesTests` (App.Tests, plain `[Fact]`s, no session; the repository by `VisualRun.RepoRoot`):
1. The three masters exist and keep 7.3's rules.
2. The manifest's hashes equal the masters'. Failure: `assets/brand/{name} changed after the icon's
   files were made: run dotnet run --project tools/VisualCommit.IconBuilder and commit what it writes.`
   Its file lists equal `IconFile`'s lists.
3. The `.ico`: reserved 0, type 1, 14 entries; their sizes exactly `IcoSizes`, ascending (a width byte
   of 0 is 256); the 256 a PNG of 256×256; every other a 40-byte `BITMAPINFOHEADER` with width N,
   height 2N, 32 bits, `BI_RGB`. Each frame is not blank and not a full square: at least a quarter of
   its pixels at alpha 255, at least one at 0.
4. The `.iconset` holds exactly the ten names, each an 8-bit RGBA PNG of its size.
5. `generated/png` holds exactly the eight files, at their sizes.
6. `Assets/Icon` holds exactly the three files; the PNGs are 48×48 and 96×96.

`WindowIconTests` (App.Tests, `[AvaloniaFact]`, no session):
1. `new MainWindow().Icon` is not null.
2. §4's notice window, made as §4's own tests make it, has an `Icon`.
3. `AssetLoader.Exists` for `AppIcon`'s three URIs; the PNGs decode at 48×48 and 96×96; `new
   WindowIcon(AssetLoader.Open(new Uri(AppIcon.WindowIconUri)))` does not throw.

`Phase8aExpectationTests.B`: `Phase8aExpectations.B`'s lists equal `IconFile`'s; the picture names
match 0.3's pattern; the sheet's rows fit 1100×700.

Red before the change: all of `IconFilesTests` and `WindowIconTests` (no files, no `Icon`); the
builder writes `AppIcon` and the tests first, then the masters and the tool's run. Earlier tests that
change: none. No test reads a window's `Icon` or the app's resources (the `.Icon` hits in `tests/` are
the glyph control's); `ShellTests`, `PanelViewTests` and `MainWindowCloseTests` make a `MainWindow`
and pass unchanged.

### 7.5 Visual checks, part B (outline)

Written into the report just before the package (D244). `IconSheet` is a harness control in a
headless `Window` of 1100×700 with the app's resources, `RequestedThemeVariant` Dark or Light and
`VcWindowBackgroundBrush` behind. Row 1, headed `VisualCommit.ico`: its 14 frames, left 24, gap 12,
bottoms aligned, each an `Image` at its own pixel size (`Stretch="None"`, whole-pixel positions), the
size below in `VcTextSecondaryBrush` at 11. Row 2: `png/icon-16` to `icon-128`, then `VisualCommit.iconset`'s
`icon_16x16`, `icon_32x32` and `icon_128x128`. No repository, no git.

| Id | Steps | Expected | Theme, size | Real window |
|---|---|---|---|---|
| B1 | Open the sheet; capture `b1-icon-sizes-dark` | Every frame at its own size, none scaled: each image's bounds equal its pixel size; its centre pixel equals the frame's composited over `#14161B` within 2 per channel; the gaps `#14161B`. Inspected: the same mark at every size, nothing cut off, its parts distinct at 16 and 20, the tile's edge holding on the dark background | dark, 1100×700 | yes |
| B2 | The same; `b2-icon-sizes-light` | The same over `#FFFFFF` | light, 1100×700 | yes |

The repeats (§12, in part A's launch, 0.6), whose captures have no headless twin and are compared
with none:
- **B1, dark, 1100×700.** (a) `Icon.ExtractAssociatedIcon` of the built `VisualCommit.exe`: its opaque
  pixels equal the `.ico`'s frame of that size within 2 per channel (proves `ApplicationIcon`). (b)
  `WM_GETICON` with `ICON_SMALL` and `ICON_BIG` both give an icon; their sizes and
  `GetSystemMetricsForDpi` of `SM_CXSMICON` and `SM_CXICON` at the window's DPI go into run.txt, and an
  icon whose size is one of the `.ico`'s equals that frame within 2. (c) The framed capture
  `b1-title-bar-dark-with-frame` and the title bar's left 64 × caption height, enlarged four times,
  `b1-title-bar-icon-dark`. Inspected: the mark, sharp, left of "VisualCommit".
- **B2, after a real click on Theme.** `b2-title-bar-light-with-frame`, `b2-title-bar-icon-light`; then
  the taskbar button whose name starts with "VisualCommit", found from UI Automation's desktop root
  within 10 s, captured by its bounds as `b2-taskbar-button` (and four times enlarged). Asserted: the
  button exists. Inspected: the mark, not .NET's default icon. A hidden taskbar or no button: run.txt
  says so, the step is "Not run", and the gate checks it by hand.

§12 builds them (12.3.4 launch 3) with `RealApp.Phase8a.cs`'s `ExecutablePath` and Win32 calls (1.15),
`IconFile` and, by the link, `Phase8aExpectations.B`.

### 7.6 Earlier pictures

Ruling 3: the three framed real-window captures alone, in their title bars (the mark at the left,
the title moved right): phase 0's `A-first-start-dark-with-frame` and
`B-after-theme-click-light-with-frame`, phase 6's `s15-system-start-with-frame`. Code saves them and
compares none (`Phase0RealWindowPass.cs`, `Phase6RealWindowPass.S.cs`), and no expected result names
a title-bar icon (phase-0.md checks 1 and 5, phase-6.md S15), so none changes; the report names them
under ruling 3. No scripted picture or client-area capture can change: a headless frame has no title
bar, and nothing else changes.

Proof: the builder runs `dotnet test --project tests/VisualCommit.VisualTests --filter-class
"*Phase0Checks"`, then `python scripts/compare-pictures.py D:/Projects/Personal/phase8-work/references/wave1
artifacts/visual <out>`: nothing changed. The lead's wave-2 run shows nothing from this package; at
the gate the three captures differ from 7b's at the title bar's left only. 8a's own pictures (B1, B2,
their repeats, §8's About) follow the mark, change with the owner's choice and are inspected again;
their expectations name no shape.

### 7.7 Cost

- Lagged list: nothing (no git, no watcher).
- Default run: two App.Tests classes, about a second; part B's two checks, about 5 to 10 s each alone.
- CI: the tool's restore and build in each of the six jobs, about 10 to 30 s (not measured).
- Real-window pass: B's steps in A's launch, about 30 to 45 s.

### 7.8 Done when, and the owner's choice

The lead checks at the merge:
1. The tests of 7.4 were red before and are green; part B passes and B1 and B2 are inspected;
   `ShellTests`, `PanelViewTests` and `MainWindowCloseTests` pass unchanged.
2. A second run of the tool with nothing changed leaves `git status` clean.
3. `dotnet build` at the root has no warning; the merge's CI run is green in every job, the floor's
   included (the tool built everywhere, `ApplicationIcon` warning nowhere).
4. `dotnet list tools/VisualCommit.IconBuilder package --include-transitive` shows Svg.Skia 4.9.1 and
   SkiaSharp 3.119.4; no `.deps.json` under `src/` names `Svg.Skia` or `ShimSkiaSharp`.
5. 7.6's proof holds.
6. The lead's own: the slnx line in a new `/tools/` folder; CLAUDE.md's row "Make the icon's files"
   (`dotnet run --project tools/VisualCommit.IconBuilder`, first run date); the "Name" note (7.3) and
   rows `assets/brand` and `tools/` in the README's layout table.

**The owner's choice** (status step 10): up to three rounds (D407; five candidates in
`D:/Projects/Personal/phase8-work/icon/candidates-v1.html`; B, D and E have a dark tile that leans on
a thin edge on a dark desk, as B's note says, which the tool's printout and B1 show). The lead writes
the three masters by 7.3's rules with the new first comment, runs the tool, `IconFilesTests`,
`WindowIconTests`, parts B and A, inspects, and commits "Icon: the owner's choice": code by the gate's
rule, so before the review before the gate (status step 15) where it can, before the counted round at
the latest. The look-alike board (web-brand 1.5) is the lead's, in the report's checks by hand.

### 7.9 Risks, unverified points, open points

Unverified, and the step that verifies each:
1. Svg.Skia 4.9.1's graph, read on NuGet, never restored here: the first restore; 7.8 item 4.
2. That Avalonia 12.1.3's Win32 icon takes the `.ico`'s bitmaps and PNG and picks a frame per DPI
   (PR 14564; its release not named): B1's repeat, (b) and (c).
3. That `HeadlessIconLoaderStub` (in Avalonia.Headless 12.1.3, body not read) takes the `.ico`:
   `WindowIconTests`, `ShellTests`.
4. That `ApplicationIcon` warns nowhere on macOS and Linux (warnings are errors): the merge's CI run.
5. That Windows 11's taskbar button is reachable through UI Automation: B2's repeat, else by hand.
6. X11's window icon, an unbundled app's Dock icon, `icon-macos.svg` against macOS 26's plate and
   `iconutil` on the runner (web-verify B5, B20): 8b's checklists.

Risks: the choice comes after the counted round; Skia's pixels differ between machines, so the files
are made on one machine and never compared by pixel (D430).

Open points: 13.4, items 9 to 11.

## 8. about: the About section and the third-party notices (R6)

Wave 3, after §5 and §7; part A. Branch `wip/p8a-about`, worktree `D:/Projects/Personal/vc-wt/p8a-about`.
Binding: R6, D398, D409; plan.md 762, 763, 776, 782 and 786 ("the About screen … the complete notices
… exist"); rulings 3, 14, 15, 54 to 59; D417. Research: `research2/notices.md` and
`research2/notices-data/`, `research/repo-about.md`.

### 8.1 What it delivers

- `THIRD-PARTY-NOTICES.md` rewritten for what the build output ships on the three systems; the
  vendors' four notice files word for word under `notices/` (new); all embedded with `LICENSE` (1.13);
  `ThirdPartyNoticesTests`, which holds the list to `VisualCommit.deps.json` and the grammars' DLL.
- About, the settings screen's sixth and last section, also opened by the palette and the macOS
  application menu; a read-only text dialog; `VcLinkButton`; part A's six checks; 8.4's changes.

Left to others: the guide link's address and check (§11, part G); the sweep over About (§9); the
real-window code (§12); the icon (§7); the README and D433's doc lines (the lead). Out: the
runtime's and installers' notices (8b, ruling 15); the Bold face (ruling 57); the menu on a Mac (8b).

Decisions: D431 (rulings 14, 15, 54 to 59; D398, D409), About's content, the notices' form and the
pinned version; D432, the text dialog, since about 230 KB in a `TextBlock` stalls the UI thread and an
editor or a browser needs a file on disk; D433, the notices and `LICENSE` as build inputs, since
`ci.yml` ignores `**.md` and `LICENSE` today; D434 (amends ruling 58), the notices file says "License",
as the app shows it whole.

### 8.2 Files

| File | Change |
|---|---|
| `THIRD-PARTY-NOTICES.md` | rewritten (8.3.4) |
| `notices/Avalonia-NOTICE.md`, `…/CommunityToolkit.Mvvm-ThirdPartyNotices.txt`, `…/Onigwrap-THIRD-PARTY-NOTICES.txt`, `…/SkiaSharp-THIRD-PARTY-NOTICES.txt` (new) | word for word: Avalonia's `NOTICE.md` at `8eeda4f6`; the files of CommunityToolkit.Mvvm 8.4.2, Onigwrap 1.0.11, SkiaSharp.NativeAssets.Win32 3.119.4; sources and SHA-256 in the merge note |
| `src/VisualCommit.App/VisualCommit.App.csproj` | `EmbeddedResource`s of 1.13 (`notices\*` as `VisualCommit.Notices.%(Filename)%(Extension)`) |
| `Services/EmbeddedTexts.cs`, `ViewModels/Operations/OperationTexts.About.cs`, `ViewModels/Settings/AboutSettingsViewModel.cs` (new) | 1.13 whole; `AboutTexts` (8.3.5); 8.3.1 |
| `ViewModels/TabServices.cs` | `SettingsSection.About` after `Accounts`, "the last section (D409)"; nothing else |
| `ViewModels/Dialogs/SettingsDialogViewModel.cs` | `About`, `ShowsAbout` in `SelectedSection`'s notifications; "six sections" |
| `Views/Dialogs/SettingsDialogView.axaml`, `.axaml.cs` | `SettingsNavAbout` after Accounts (the comment "Accounts, last" becomes "before About"), the section's panel, `NavButtonOf`'s case |
| `ViewModels/Dialogs/TextDocumentDialogViewModel.cs`, `Views/Dialogs/TextDocumentDialogView.axaml(.cs)`, `Controls/DocumentTextView.cs` (new) | 8.3.2; the view found by name |
| `Theme/Controls.axaml`, `Views/Dialogs/SignInDialogView.axaml` | `VcLinkButton`: `SignInLinkButton` moved unchanged; the sign-in links use it, their `tokenLink` style stays |
| `Theme/Dialogs.axaml` | `Border.dialogCard.document`: MaxWidth 760, MaxHeight 600, Margin 40, stretched |
| `ViewModels/Commands/AppCommands.cs` | `case AppCommand.About: return Always(AboutTexts.PaletteCommand, () => shell.OpenSettingsCommand.Execute(SettingsSection.About));` |
| `App.axaml`, `App.axaml.cs` | the native menu (8.3.3) |
| `.github/workflows/ci.yml` | the trigger (8.3.4) |

Paths without a project are under `src/VisualCommit.App/`. Contracts: implements 1.13; uses 1.5's
tokens, 1.6 (`AppIdentity`, `TestIdentity.Pinned`), 1.7 (`AppLinks.Repository`,
`AppLinks.Guide(GuidePage.Start)`), 1.8 (`AppCommand.About`), 1.10 (`SettingsScreenServices.Identity`,
`.Launcher`, `.Clipboard`), 1.13's ids, 1.15; and 1.6's `AppIcon.Image96Uri` (§7). `VcLinkButton` is
§8's alone: §5's first-run links are `VcFlatButton`s.

### 8.3 Behaviour

**8.3.1 The section.** `AboutSettingsViewModel(AppIdentity identity, DialogHostViewModel dialogs,
IFileLauncher? launcher, IClipboardService? clipboard, IAppLog log)`; no `Identity` gives
`AppIdentity.FromAssembly(typeof(App).Assembly)`. The Show buttons read the texts off the UI thread
(Q2) and show the dialog unawaited; a text that cannot be read is an Error (`The text {name} could not
be read.`). The links call `OpenUrlAsync` with `AppLinks.Repository` and
`AppLinks.Guide(GuidePage.Start)`, disabled without a launcher; a failure is a Warning (`Opening the
repository's page failed.`, `Opening the user guide failed.`). About saves nothing. Down from Accounts
reaches About; Down on About stays (`SettingsDialogView.axaml.cs:111`).

The form is the Accounts section's (Spacing 18; rows `132,12,*`, `settingsLabel`, `settingsNote`,
`VcActionButton`s 10 below), tokens only (1.5), about 356 high: no scrolling at 1100×700:

| Row | Content | Ids |
|---|---|---|
| head | `Grid "48,16,*"`: the icon 48 × 48 (decorative), `AppIcon.Image96Uri` drawn with `BitmapInterpolationMode.HighQuality` as the avatars are (`CommitGraphControl.Avatars.cs:123`); "VisualCommit" 20 SemiBold over "Created by Rahim Khajei" `secondary` | `AboutIcon`, `AboutName`, `AboutCreator` |
| Version | `0.1.0 (1a2b3c4)`, its automation name the text | `AboutVersion` |
| Copyright | `Copyright (c) 2026 Rahim Khajei` | `AboutCopyright` |
| License | "MIT License"; [Show license…] | `AboutLicense`, `AboutShowLicenseButton` |
| Notices | the note; [Show third-party notices…] | `AboutNoticesNote`, `AboutShowNoticesButton` |
| Source code | link `github.com/RahimPasha/VisualCommit` (the address's host and path), its tip | `AboutRepositoryLink` |
| User guide | link "Open the user guide", its tip | `AboutGuideLink` |

The section is `SettingsAbout`. Tab: the four buttons and links, then Cancel and Save. The links
underline while hovered or focused. Every interactive control is named by its text (§9's sweep).

**8.3.2 The text dialog.** `TextDocumentDialogViewModel(string title, IReadOnlyList<TextDocumentPart>
parts, IClipboardService? clipboard, IAppLog log) : DialogViewModel<bool>`; `TextDocumentPart` (new:
`Label`, `Text`, `AutomationId`, observable `IsShown`); `Shown` (the first part at opening),
`ShowsSwitch`, `ShowCommand(part)`, `CopyAllCommand` (the shown text whole; the dialog stays; a failure
is a Warning, `Copying the text to the clipboard failed.`), `ConfirmCommand` (Close, true); Esc false.
The view on `dialogCard document`: the title (`DialogTitle`); 12 below, with several parts, a
`WrapPanel` of `VcFlatButton`s 28 high, 4 apart, the shown one `selected` (`TextDocumentSwitch`); 8
below, `DocumentTextView` (`TextDocumentText`); 20 below, "Copy all" (`TextDocumentCopyButton`, left)
and "Close" (`ConfirmButton`, `VcPrimaryButton`, right).

`DocumentTextView : CodeTextEditor` (new): read-only, wrapped, the code font at 11.5,
`Options.AcceptsTab = false` (ruling 52), no caret drawn, `VcTextPrimaryBrush` on
`VcControlBackgroundBrush`, 1 px `VcBorderBrush`; it takes the focus at opening. Up and Down scroll a
line, PageUp and PageDown the view less a line, Home and End (also with Ctrl) move the hidden caret to
the start or end and bring it into view, so End shows the true last line; Ctrl+C copies a selection.
Its peer is a Document, value the text, name the title, as `BlameTextEditor`'s. A switch shows the new
text from its top. Close and Esc give the focus back to the opening button (`_focusBeforeDialog`).

The license: "MIT License", one part. The notices: "Third-party notices", parts "Components and
licenses" (`TextDocumentPartMain`), then `VendorNotices()` in name order, ids `TextDocumentPart` + the
file name before its first '-' without dots.

**8.3.3 Palette and macOS menu.** WP0's `Add(AppCommand.About)` stands after `FollowSystemTheme`; §8's
case lists it on every kind of tab, enabled, no key, no detail. `App.axaml` declares, so that it exists
before Avalonia.Native's exporter reads it (notices.md 7):
`<NativeMenu.Menu><NativeMenu><NativeMenuItem Header="{x:Static ops:OperationTexts+AboutTexts.MenuItem}" /></NativeMenu></NativeMenu.Menu>`.
`App.axaml.cs` gains `public MainWindowViewModel? Shell { get; set; }` (set in §4's `StartSession`; a
headless test sets it) and `public ICommand AboutCommand`, the item's `Command` (set in `Initialize`):
with a shell and no dialog open, `OpenSettingsCommand` with `SettingsSection.About`; else nothing, as
Mod+,. `MacOSPlatformOptions` is untouched, so Hide, Show All and Quit stay.

**8.3.4 The notices file and CI.** An intro (VisualCommit's own MIT License; the components the build
output ships on the three systems, each under its own license; the installers add the runtime's
notices; the vendors' files kept word for word in `notices/`); `## Components`, a `###` entry each
with the machine-read lines `Packages: <id> <version>, …` (NuGet packages only) and `Files: <file>, …`
(every asset on any system), then `Source:`, the copyright lines verbatim and `License: <name>[,
<name>]. <text>`; `## License texts` (MIT, ISC, BSD-2-Clause, BSD-3-Clause (<holder>), Apache-2.0,
OFL-1.1, zlib, TextMate bundle license); `## Keeping this file true` (an updated package fails
`ThirdPartyNoticesTests`; take its nuspec, license and notice files from the NuGet cache, its commit
from the nuspec's `<repository>`).

The entries (notices.md 2, 3): Avalonia (15 packages, `libAvaloniaNative.dylib`); ANGLE in Avalonia's
build (with abseil-cpp, zlib, Chromium's compression utilities, xxHash, libc++); SkiaSharp and
HarfBuzzSharp, plus Wuffs and SPIRV-Cross, which their file omits; MicroCom.Runtime; Tmds.DBus.Protocol;
CommunityToolkit.Mvvm; AvaloniaEdit with AvalonEdit's holder; TextMateSharp; Onigwrap with Oniguruma;
TextMateSharp.Grammars 2.0.4 (a table of `grammars.tsv`'s 58 rows, VS Code's MIT for the extension
files, the 21 themes by name and origin); Inter 3.019 (OFL-1.1 with its header); JetBrains Mono NL
Regular 2.304; Lucide and Feather (today's texts). Under TextMateSharp, word for word (ruling 56):
"Six themes in the package, Dracula, Atom One Dark, Atom One Light, OneDark, Visual Studio Dark and
Visual Studio Light, state no origin in the package or in TextMateSharp's history, and TextMateSharp
is a port of microsoft/vscode-textmate based on the Java port eclipse/tm4e. They are listed here under
TextMateSharp's own declared license, the MIT License, which is all the package states for them."

`ci.yml`: `push` and `pull_request` take `paths: ['**', '!docs/**', '!**.md', '!LICENSE',
'THIRD-PARTY-NOTICES.md', 'LICENSE', 'notices/**']` in place of `paths-ignore` (a later pattern takes
back what an earlier `!` left out).

**8.3.5 Texts** (`OperationTexts.AboutTexts`): `Section` "About"; `CreatedBy(creator)` "Created by
{creator}"; labels "Version", "Copyright", "License", "Notices", "Source code", "User guide";
`MitLicense` "MIT License"; `ShowLicense` "Show license…"; `NoticesNote` "VisualCommit is built on
open-source components, each under its own license."; `ShowNotices` "Show third-party notices…";
`RepositoryTip` "Opens the repository's page in your browser."; `OpenUserGuide` "Open the user
guide"; `GuideTip` "Opens the user guide in your browser."; `LicenseTitle` "MIT License";
`NoticesTitle` "Third-party notices"; `MainNotices` "Components and licenses"; `NoticeFileLabel(file)`
("Avalonia", ".NET Community Toolkit", "Onigwrap", "SkiaSharp and HarfBuzzSharp"; else the file name);
`CopyAll` "Copy all"; `Close` "Close"; `PaletteCommand` "About VisualCommit…"; `MenuItem` "About
VisualCommit". No text holds an e-mail address.

### 8.4 Tests

**App.Tests, no session** (new):
- `ThirdPartyNoticesTests` (deps.json beside the test assembly, the resources, `VisualRun.RepoRoot`):
  `Every_shipped_package_is_named_with_its_version` (each `package` library with a `runtime`,
  `runtimeTargets`, `native` or `resources` asset, on one `Packages:` line as "<id> <version>");
  `Every_shipped_file_is_named` (each asset's file name on its entry's `Files:` line);
  `Every_named_package_ships` (in deps.json, or one of the four `SkiaSharp`/`HarfBuzzSharp`
  `.NativeAssets.macOS`/`.Linux` ids); `Every_grammar_and_theme_is_listed` (from
  `typeof(RegistryOptions).Assembly`: each `cgmanifest.json` folder and registration URL, without a
  trailing slash, in the table; each `Themes.*.json` in the list); `Every_license_named_has_its_text`
  (a heading under "License texts" or a `notices/` file); `The_app_embeds_the_texts_word_for_word`
  (the resources equal the files, `\r\n` made `\n`; `VendorNotices()` is `notices/` in name order, each
  named in the main file); `The_identity_matches_LICENSE` (`FromAssembly`'s `Copyright` is `LICENSE`'s
  third line, `Creator` "Rahim Khajei"; `LICENSE` starts "MIT License").
- `AboutSettingsViewModelTests` (`TestIdentity.Pinned`, the fakes): the texts; Show license pushes one
  dialog "MIT License" with `LICENSE`; Show notices five parts in order; the links give the fake
  launcher exactly `https://github.com/RahimPasha/VisualCommit` and `AppLinks.GuideBase`; no launcher,
  both disabled; a throwing launcher, a Warning.
- `TextDocumentDialogViewModelTests`: the first part shown; `ShowCommand` moves `IsShown`; Copy all puts
  the shown text whole; Close true, Esc false; one part, no switch.
- `DocumentTextViewTests` ([AvaloniaFact], a window, no session): Down a line, PageDown a view less a
  line, End the notices' last line, Home the first; Tab leaves; the peer is a Document with the text.

**8a's project** (new, D416): `AboutSectionTests`: the section's texts are the pin's; Show
license… by Enter, the focus in the text, Esc gives it back; the notices switch shows Avalonia's file;
the links reach the fake launcher, nothing else launched. `AboutEntryTests`: on a welcome, a repository
and a dashboard tab the palette's last command is "About VisualCommit…", enabled, keyless, and opens
About with the focus on `SettingsNavAbout`; `NativeMenu.GetMenu(Application.Current)` holds one item,
"About VisualCommit", no header holds "Avalonia"; its `Command` with `App.Shell` set opens About, and
does nothing while a dialog is open.

**Red before the change** (merge note): `ThirdPartyNoticesTests` on today's file; the lines below.

**Earlier tests that change** (each reason once in 8a's report, "Changes to expected results"); the
builder Greps for others (`SettingsNavAccounts`, `Follow the system's theme`):
1. `SettingsDialogViewTests.cs:176-187`: Down from Accounts reaches `SettingsNavAbout`, Down stays,
   three Ups reach Tools; the comment names About last (D409).
2. `PaletteEntriesTests.cs:159`, `:340`: "About VisualCommit…" after "Follow the system's theme" (ruling 54).
3. K10 (ruling 54): `Phase6Expectations.K.cs`' `OpsCommands` and `DashboardCommands` end with
   `E("About VisualCommit…")`; `OpsCommandCount` 34, `DashboardCommandCount` 23, `WebAppCommandCount`
   35, `PageDownRows` [11, 22, 33], `FirstRowInViewAtEnd` 22, `WheelRow` 27; `Phase6ExpectationTests.K.cs:318`.
   They follow from `:317-319`'s identities; K10's run confirms them. §11 moves them again.
4. `Phase7aExpectations.A.cs:55, 58`, `Phase7bExpectations.A.cs:97, 99`: `NavButtons` and `NavIds`
   end with "About", `SettingsNavAbout` (D409); 7a's real-window A1 reads the linked file.
5. `ShellDriver.Phase6.Settings.cs:124-131`: `ChooseSection` gains About, never General's default.
6. The phase 6 report's "`Shown` 33 commands": the lead's, in 8a's report.

### 8.5 Part A's checks (outline, D244)

Dark, 1100×700, an empty tab, `settings.json` unwritten and nothing launched unless named. R: §12 repeats it.

| Id | Steps | Expected |
|---|---|---|
| A1 (R) | Ctrl+Shift+P, `about`, Enter; Tab ×4; Enter on the repository link; Esc | One command row, no key. Six nav buttons 176 × 30, 2 apart, About at (141, 255), selected, focused; 8.3.1's rows with the pin; nothing clipped (`LayoutAudit`), no "@". `a1a-about-dark`. Tab: the two buttons, the repository link underlined, the guide link: `a1b-about-link-focus`. Enter records exactly the repository's address; Esc closes |
| A2 | Light in the data folder; Settings, a click on About | A1's texts; the link `#4353D8`. `a2a-about-light` |
| A3 | 1920×1080; Ctrl+,, Down ×5, Enter | About at (551, 415). `a3a-about-1920` |
| A4 (R) | Show license…; Copy all; Esc | Over the dimmed screen: "MIT License", no switch, `LICENSE`'s 21 lines in view, Copy all left, Close right, the focus in the text. `a4a-license`. The clipboard holds `LICENSE` (`\n`); Esc: the focus on Show license… |
| A5 | Show third-party notices…; End; "Avalonia"; Close | Within 1 s: the title, five switch buttons in one row, the first selected, the file's first lines, `a5a-notices-top`; End: its last line at the bottom, `a5b-notices-end`; Avalonia's file from its top, `a5c-notices-avalonia`; Close: the focus on its button |
| A6 | `Scenarios.GraphAsync`; Ctrl+Shift+P, `about`; Enter | The one row highlighted, no key, no detail, `a6a-palette-about`; About opens; `ExactStateAsync()` unchanged |

Each method under about 40 s alone. `Phase8aExpectations.A.cs` holds the texts as literals, the nav
slots, the address and the names; `Phase8aExpectationTests.A.cs` asserts the names' pattern and no "@".
For §12 (12.3.4 launch 3): A1 up to the Tab walk by real keys (no link pressed), under 3 % from
`a1a-about-dark` (and `a3a-about-1920` where 1920×1080 fits), `AboutVersion`'s name equal to the built
`VisualCommit.dll`'s `ProductVersion` read as `AppVersionInfo.Parse` does; then A4 by a real Enter on
Show license…, under 3 % from `a4a-license`, and Esc, "Copy all" never pressed.

### 8.6 Earlier pictures

Two changes, named by ruling 3 (rulings 14, 54; D409):
- The section list: `settings-list-pictures.tsv`'s 83 scripted pictures (phase 6 31, 7a 23, 7b 29) and
  8 captures (phase 6 `s14a-general`, `l31d-settings-light`; 7a `a01a`, `a02b`, `a05a`; 7b `a02d`,
  `a05b`, `a06b`) with their difference pictures, in x 141–317, y 255–285 at 1100×700 (x 551–727, y
  415–445 at 1920×1080; doubled at 200 %).
- The palette's thumb, x 837–838: `k10a-palette-commands`, `k10f-palette-names-repository`,
  `l31c-palette-dark`, `l31c-palette-light`; captures `k10a`, `l31c-palette-light`.

Nothing else: `VcLinkButton` is the sign-in theme moved unchanged, the native menu draws nothing on
Windows, no typed query of an earlier check matches the command. Proof: the builder runs phase 6's
part S, K10 and L31 and 7a's and 7b's part A (the sign-in dialogs among them), then
`compare-pictures.py --expected` against `D:/Projects/Personal/phase8-work/references/wave1/` with these
regions and wave 2's: every other picture the same file. Captures at the gate (0.7).

### 8.7 Lagged list, time, real-window pass

The lagged list gains nothing (K10, 7a's, 7b's and 8a's walk-throughs are in it; About reads no git).
Default run: App.Tests about 25 tests without a session, seconds; 8a's project part A about 3 minutes
alone, the two classes about one. The pass: A1 in the launch shared with part B, about 30 s.

### 8.8 Done when

- `ThirdPartyNoticesTests` green on the laptop and in CI's six jobs, shown red on the base first; the
  vendor files' sources and SHA-256 in the merge note.
- 8.3 built, 8.4's classes pass, 8.4's earlier changes made with their reasons drafted; no warning.
- Part A passes, each method under 40 s, its pictures inspected; 8.6's comparison as named.
- The merge's push ran CI green (the floor job as §3 leaves it); D433's doc lines are in.

### 8.9 Risks, unverified, open points

1. Unverified: `NativeMenuItem.Command` in Avalonia 12.1.3, and a `NativeMenu` on the `Application`
   under the headless platform. The builder reads the source first; without `Command`, `Click` calls a
   public `App.ShowAbout(MainWindowViewModel?)`, which the test calls.
2. Unverified: the macOS menu (read in Avalonia's source at `8eeda4f6`); 8b's Mac checklist.
3. Unverified until A5: the 1 s bound and End with wrapping. If either fails, the lead rules.
4. Microsoft's file omits Wuffs and SPIRV-Cross (found by strings); others may hide. Their licenses
   (MIT and Apache-2.0; Apache-2.0 OR MIT) are read at the source before they are written.
5. The macOS and Linux natives are proven only in CI's macOS and Linux jobs.
6. Unverified until a push changes only the notices: the `paths` order rule.
7. Unverified: `ProductVersion` carrying `+<commit>` in the real window's build.
8. Part A shows §7's placeholder; its pictures change when the chosen mark is regenerated.

While the settings screen or any dialog is open, the macOS item does nothing, as Mod+, does. Open
point: 13.4, item 12.

## 9. uia: screen readers, names and the graph's row (D411)

Wave 3, after §5's and §6's merges; merges after §8; branch `wip/p8a-uia`, part U. Carries D411, D49,
rulings 3, 12, 32 to 39, plan.md's lines 759, 770, 784 and 786, and D435. Facts are
uia.md's (Avalonia 12.1.3, FlaUI 5.0.0, the code at `e04762b`), checked again on `27c5109`.

### 9.1 What the package delivers, and what it leaves

Delivers 9.3's parts in their order, the spike first, with 9.4's tests and part U (9.5). Leaves: to
§11 the guide's pages; to §12 the real-window code; to the lead at the close
`architecture.md` (ruling 38: line 895's "one element" and the conventions at line 6224 rewritten for
the List, its row and the live regions), the Backlog's item status on the left panel's rows (ruling
36) and the report's checks by hand. Out: a `[PrivateApi]` route but by a later decision (9.3.1); the
window peer's `GetFocus` (ruling 35); a peer per row; any tooltip, help text or key (ruling 34);
VoiceOver and Orca as tested (D411).

Its decision, D435 (rulings 32 to 36, 38; amends D49): the graph a List with one exposed, spoken
row, whose name change is raised only when the row becomes another (a polite live region speaks every
change, so a fetch would otherwise read the row again unasked); every control named; the status bar a
polite live region; public API only.

### 9.2 Files and contracts

New, under `src/VisualCommit.App/`: `Controls/CommitGraphControl.Automation.cs` (9.3.2 to 9.3.4),
`Controls/GraphRowSpeech.cs` (9.3.5), `ViewModels/Commands/AutomationNames.cs` (9.3.6). Under `tests/`:
`VisualCommit.Testing/Headless/AutomationSweep.cs`; `VisualCommit.Testing/Headless/ShellDriver.Phase8a.Uia.cs`
(`GraphRowPeer()`, the window's peer made once, `RecordNameChanges(AutomationPeer)`); in App.Tests
`GraphRowSpeechTests.cs`, `CommitGraphControlAutomationTests.cs`, `AutomationSweepTests.cs`; in
`VisualCommit.VisualTests.Phase8a` `AccessibleNameSweepTests.cs`, `Phase8aChecks.U.cs`,
`Phase8aExpectations.U.cs`, `Phase8aExpectationTests.U.cs`.

Changed (each exists), under `src/VisualCommit.App/`:
- `Controls/CommitGraphControl.cs`: the nested peer (`:855-867`) moves to the new partial;
  `OnPropertyChanged`, `OnDataChanged` and got- and lost-focus overrides call
  `ScheduleAutomationUpdate()` (9.3.4); the remarks' D49 sentence. `CommitGraphControl.Badges.cs` stays:
  the new partial builds a request's words with `HostingTexts.Request` and `.Number` as `LabelTipLine`
  does, and a test pins both equal.
- Only `AutomationProperties.*` attributes and an `xmlns:cmd` line: `Views/Conflicts/ConflictResolverView.axaml`,
  `Views/Diff/DiffView.axaml`, `Views/Graph/SearchBarView.axaml`, `Views/MainWindow.axaml`, and in
  `Views/Shell/` `ActivityLogView`, `CompareView`, `LeftPanelView`, `WorkingChangesView`,
  `StatusBarView` and `CommitGraphView` (`.axaml`).
- Tests: `CommitGraphControlTests.cs` (9.4), `StatusBarTests.cs` (one test added).

Contracts: implements 1.12 and 1.15's `AutomationSweep` and `ShellDriver.Phase8a.Uia.cs`; uses 1.2's
first-run ids, `FakeGitSetupProbe` and `ShellDriver.Start(gitProbe:, askPassHelper:)` (§5, 1.15),
1.11's `InstanceNoticeHost` (§4), 1.13's About ids (§8).

### 9.3 Behaviour

#### 9.3.1 The spike, first

The builder's first commits are the peers of 9.3.2 to 9.3.5. On `wip/p8a-uia-spike`, made from them,
never merged, a throwaway `tests/VisualCommit.RealWindowTests/UiaSpike.cs` starts the Release build as
`RealApp` does, on `Scenarios.OperationsAsync()` with README.md changed, registers through FlaUI UIA3
on the window, subtree, for focus changed, LiveRegionChanged, structure changed and the Name,
HelpText and Selection property changes, and writes each (time, event, id, name) to
`artifacts/uia-spike/events.txt`. The lead runs it once, the owner told (a minute of keys and
clicks), no heavy run beside (ruling 23):
`dotnet test --project tests/VisualCommit.RealWindowTests -c Release --filter-class "*UiaSpike"`.

| Step | Does | Asks |
|---|---|---|
| S1 cold | Click "Main work", Down three times, nothing below the graph read | A LiveRegionChanged or Name event from `GraphSelectedRow` per move? |
| S2 warm | Read the graph's first child, Down three times | The same |
| S3 | F6 away and back | Focus on `GraphRows`; the row's event once |
| S4 | Click `feature/alpha` in the left panel | The row's event, the focus kept on `RefItem` |
| S5 | Ctrl+Shift+F (the local origin) | How many LiveRegionChanged from `OperationStatus` |
| S6 | Throughout | `FocusedAutomationId()` never `GraphSelectedRow` (ruling 35) |

S1 yes: the route stands. S1 no, S2 yes: the lead decides (13.4 item 14). Both no:
the build stops until a decision names `IEmbeddedRootProvider` (`[PrivateApi]`, 12.1.3) and its
guarding test (ruling 32). The events go into the merge notes.

#### 9.3.2 The graph's peer

`CommitGraphAutomationPeer : ControlAutomationPeer, ISelectionProvider`, type `List`, control and
content element, name "Commit graph" and id `GraphRows` as today. It makes one row peer and keeps it
for its life. `GetChildrenCore()` and `GetSelection()` give that row while one is exposed, else
nothing; `CanSelectMultiple` true, `IsSelectionRequired` false. The exposed row: the working-changes
row while selected; else `FocusRow` (the single selected commit, or `Selection.Focus` while several
are); else none. With 100,000 rows selected the selection still holds one peer; the hint has the count.

#### 9.3.3 The row's peer

`CommitGraphRowAutomationPeer : ControlAutomationPeer, ISelectionItemProvider` over the graph itself
(the public way to screen bounds): type `ListItem`, id `GraphSelectedRow`, class `CommitGraphRow`;
name `GraphRowSpeech.Name` or `.WorkingRowName` and help text `GraphRowSpeech.Hint` (empty on the
working-changes row), built when read, the help text overriding the base's fallback to the graph's
tooltip; `LiveSetting` Polite; `LabeledBy`, `AcceleratorKey`, `AccessKey`, `ItemStatus` null; no
children; parent the graph's peer. Bounds: `RowBounds(row)` or `WorkingRowBounds` clipped to the
graph, in the root's coordinates as the base gives the graph's; `IsOffscreen` when the clip is empty.
`HasKeyboardFocus` is the graph's `IsFocused`; `SetFocus` focuses the graph; `BringIntoView` scrolls
the row into view; `ShowContextMenu` raises the graph's keyboard menu request, as Shift+F10.
`IsSelected` true, `SelectionContainer` the graph's peer, `Select()` selects this row alone,
`AddToSelection` and `RemoveFromSelection` do nothing.

#### 9.3.4 When the row speaks (D435)

`ScheduleAutomationUpdate()` runs from `OnPropertyChanged` (`SelectedIndex`, `Selection`,
`IsWorkingRowSelected`, `ShowsWorkingRow`, `Data`, `WorkingRowText`, `Dates`, `Search`,
`PullRequests`), `OnDataChanged` and the focus overrides. It reads `ControlAutomationPeer.FromElement(this)`,
which makes no peer: without a client nothing is posted. Else it posts one `Update` per dispatcher
turn (Background, behind a flag), as `ApplySelection` sets two or three properties in a row. `Update`
compares the exposed key (the working row, a SHA, or none) with the last:

| Change | Raised |
|---|---|
| none to a row | `InvalidateChildren()`; the Selection change; the row's Name change (old null) |
| a row to none | `InvalidateChildren()`; the Selection change |
| another row | the Selection change; the Name change (old: the last name raised) |
| same row, other name / other hint | nothing, the name read fresh / the HelpText change |
| the graph takes the focus, a row exposed | the Name change (old null), once |

A move with the focus elsewhere (a ref in the left panel, a search match, a parent link, a tab switch)
is spoken too. Nothing touches `Render`, `AffectsRender`, `InvalidateVisual` or layout.

#### 9.3.5 The texts (`GraphRowSpeech`)

Each word is a public constant of `GraphRowSpeech`, which the tests read.
- A commit: `{subject}, {author}, {date}` (the Date column's own), then `, {label}` per label in drawing
  order, those under "+N" too: `current branch {name}`, `branch {name}`, `remote branch {name}`,
  `tag {name}`, a stash's own text (`stash@{0}`), `HEAD, detached`. A label with an open request adds
  the tooltip's words: ` (pull request #12: Add the login form)`, ` (draft pull request #2: …)`,
  ` (merge request !21: …)`, Azure DevOps' ` (pull request !21: …)`.
- An empty or blank subject reads `No message`. A stash row: `Stash: {subject}, {date}`, then its labels.
- The working-changes row: `WorkingRowText`, or `Working changes`, with ` · ` read as `, `.
- The hint: `Commit {short SHA}`, then `, {n} commits selected` while n ≥ 2 (the multi-selection
  panel's words, invariant digits), then `, matches the search` while an active search marks the row.

On the ops scenario (UTC): `Main work, Test Author, 2026-01-01 12:04, current branch main, remote
branch origin/main, tag v1.1`; `Stash: On main: Experiment, 2026-01-01 12:07, stash@{0}`; with a
conflict `Working changes, 1 conflict`.

#### 9.3.6 The names

1.12's table, each a constant of `AutomationNames` set as a local value, which beats `VcIconButton`'s
setter (`Theme/Controls.axaml:216`, kept as a fallback):
`AutomationProperties.Name="{x:Static cmd:AutomationNames.PreviousConflict}"`. The rows bind formats:
`StageFileFormat` `Stage {0}`, `DiscardFileFormat` `Discard {0}`, `UnstageFileFormat` `Unstage {0}` over
`ChangedFileRow.Key` (the path); `ShowRefFormat` `Show {0} in the graph`, `StopSoloingRefFormat`
`Stop soloing {0}` over `RefItemRow.Text`; as `{Binding Key, StringFormat={x:Static cmd:AutomationNames.StageFileFormat}}`,
or a converter in `AutomationNames` if the compiled binding refuses that. No tooltip, help text, key,
template or style changes. A control outside 1.12 that the sweep reports is named only at the lead's
word, from its area's texts class, never by a tooltip's change.

#### 9.3.7 The status bar

`AutomationProperties.LiveSetting="Polite"` on the TextBlock `OperationStatus`
(`StatusBarView.axaml:57-66`), one property (ruling 36). Its peer raises Name on each new `Text`, which
the bridge turns into LiveRegionChanged; the running text holds git's percentage (9.9, risk 4).

#### 9.3.8 The sweep

`AutomationSweep` walks the peers depth first from `ControlAutomationPeer.CreatePeerForElement(root)`
and checks each control element that is interactive: type Button, CheckBox, RadioButton, Edit,
ComboBox, ListItem, MenuItem, TabItem, Hyperlink, Slider or Thumb, or offering Invoke, Toggle,
SelectionItem, ExpandCollapse, RangeValue or a writable Value. Rules:
- `empty`: a null or blank name.
- `type name`: ``^(Avalonia|VisualCommit|System|Microsoft)(\.[A-Za-z_][\w`]*)+$`` ("Reader.cs" passes).
- `key text`: the name ends in a parenthesis that holds its own `AcceleratorKey`, or whose parts, split
  at ", ", are each a key text (`Ctrl+`, `Shift+`, `Alt+`, `Cmd+`, `Option+` and a key; without one
  only Esc, Enter, Tab, Space, Delete, Backspace, Home, End, PageUp, PageDown, the arrows, Menu, F1 to
  F12): "Close (Esc)" and "Previous match (Shift+Enter, Shift+F3)" fail, "Pull requests (3)" passes.
- `duplicate`: two checked peers with one automation id and one name under the same items container
  (the nearest ancestor peer whose owner is an `ItemsControl`).

`AutomationProblem` and `AssertNamed` as 1.12.

`AccessibleNameSweepTests`, one `[AvaloniaFact]` and one start each, about 15: (1) the welcome page
and the palette; (2) settings, each section, the Git program row, Accounts and each provider's sign-in
dialog; (3) About and its license's text dialog (at the merge); (4) every dialog kind through its
presenter, as `PromptDialogTests` does; (5) the ops tab: two branches hidden, then one soloed, a
commit with files and parent links, two unstaged and one staged file with one row selected and the
pointer over another, the diff's hunk bars, the restore bar after a discard, the activity log, the
graph's row, a context menu on the graph and on a file; (6) search, compare, file history, blame,
reflog; (7) the rebase list; (8) the resolver, its card, the banner; (9) the dashboard; first run
through `FakeGitSetupProbe`: (10) Windows' Missing, then by "Check again" ChosenMissing, TooOld and
NotWorking with git's words, (11) macOS's Missing, (12) Linux's Missing, (13) HelperMissing
(`askPassHelper` a path without a file); (14) §4's notice through `InstanceNoticeHost` (1.11).

#### 9.3.9 Narrator by hand, and the guide

The walk-through (D411, the owner, about 20 minutes, at the gate) on copies of the ops scenario with
README.md changed and of a stopped merge, started by the lead's PowerShell line with CLAUDE.md's
isolating variables. Narrator: Ctrl+Win+Enter. Each step: "as expected", or the words heard.
1. The app in front: its title. 2. F6 to the graph: "Commit graph, list", then the selected row.
3. Down: the next row's subject, author, date, refs. 4. HEAD's row: "…, current branch main…".
5. Home, End, PageDown: each landing row, once. 6. Shift+Down twice: each focus row; is "3 commits
selected" heard? 7. Down: one row, no count. 8. Up to "Working changes". 9. Shift+F10: the menu's
first item; Esc: back. 10. A branch clicked in the left panel: its commit's row. 11. F6, Tab to a
file: its path, "button". 12. Mod+Shift+M, then Tab to a row's button: "Stage README.md, button".
13. The status bar's toggle: "Activity log, button, Ctrl+J". 14. Ctrl+Shift+F: the result; are the
progress steps read? 15. Ctrl+F, a word, Enter: the match's row, the focus in the box. 16. Ctrl+Shift+P,
"fet": entries with keys. 17. Ctrl+,, About: the owner's name and the version. 18. The resolver:
"Next conflict, button, Alt+Down". 19. Narrator+Tab on the graph: "Commit graph, list" (ruling 35).
Silence at steps 2, 3 or 5: the live region does not reach Narrator (9.9, risk 1).

For §11 (ruling 39): "VisualCommit is tested with Narrator on Windows. Other screen readers, NVDA,
JAWS, VoiceOver and Orca among them, have not been tried. In the commit graph, the selected commit is
read when the selection moves: its message, author, date, branches and tags; the short commit id is
its description."

### 9.4 Tests

New, each written first and failing on the code before (recorded in the merge notes):
- `GraphRowSpeechTests` (App.Tests, pure, about 20): each label's words and order, "+N" labels, the
  request's words (GitHub, draft, GitLab, Azure DevOps), the empty subject, the stash row, the working
  row with and without ` · `, the hint with 1, 2 and 100,000 selected and the search.
- `CommitGraphControlAutomationTests` (App.Tests, `CommitGraphControlTests`' host, about 14): each
  rule of 9.3.2 to 9.3.4 on `Linear(100_000)`: no peer made, 100 moves, `FromElement` null; one child
  and one row instance through End, Home and ten PageDowns, one Name change each; the table's other
  rows; Shift+End's one selected peer; bounds equal `RowBounds` in window coordinates; the request's
  words equal the label's tooltip line; a capture before the peer and after ten moves back, same bytes.
- `AutomationSweepTests` (App.Tests, hand-built windows, about 10): each rule's failing and passing
  examples, an invisible button not walked, the message's id and class.
- `AccessibleNameSweepTests` (8a project): on the code before it reports at least `ActivityLogToggle`
  (type name), the three splitters (empty), the resolver's five and the search's three (key text), and
  duplicates of `RowStageButton`, `RowDiscardButton`, `RefHiddenToggle`.
- `StatusBarTests.The_operation_text_is_a_polite_live_region`: Polite; a new text, one Name change.

Earlier tests that change: `CommitGraphControlTests.The_graph_is_one_ui_automation_element_with_its_name`
becomes `The_graph_is_a_ui_automation_list_with_its_name` and asserts `List` (D435: no longer
one element); its builders `Linear`, `Branch` and `Host` become `internal`, no assertion changed. No
other: `ShellToolTipTests` reads tooltips, help texts and keys, `HideAndSoloTests` tooltips;
`CommitKeyTests`, `RegionKeysTests`, `CommandPaletteViewTests` and `Phase6RealWindowPass.K.cs:224` the
focus on `GraphRows`, which stays (ruling 35).

### 9.5 Part U's checks, in outline

Dark, 1100×700, the ops scenario with README.md changed unless named; the window's peer made through
`ShellDriver.Phase8a.Uia.cs`.
- **U1, the graph's row** (real window: yes). F6, "Main work" selected: a picture, the peer made, the
  same picture again, same bytes (`u1a-graph-row-head`); Down; Up to the stash row and the working
  row; Shift+Down twice (`u1b-graph-rows-three-selected`); the working row (`u1c-graph-working-row`);
  a ref clicked in the left panel. Each step: one child `GraphSelectedRow`, ListItem, 9.3.5's name and
  hint, one Name change per move, the focus on `GraphRows` (on `RefItem` after the click).
- **U2, the sweep** (real window: no). `AccessibleNameSweepTests` passes; the report lists the states
  and the first run's findings. One picture of the busiest state: the activity log, a selected and a
  hovered stage row, two hidden branches (`u2a-sweep-ops-tab`).
- **U3, the names** (real window: yes). `Scenarios.ConflictsStoppedAsync(Merge)`: the resolver's five
  (`u3a-resolver-names`); the ops tab: "Stage README.md", "Discard README.md", "Unstage …", "Dismiss
  the restore offer", the search's three, the comparison's two, "Activity log", "Close the activity
  log", the splitters, "Show feature/alpha in the graph", "Stop soloing feature/ff"
  (`u3b-stage-row-names`); every tooltip as before.

`Phase8aExpectations.U` holds U1's names and hints, and also, for §12's launch (12.3.4 launch 13), those
of `Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`'s working row and its next two rows and the
three names of an untracked `u-untracked.txt`, which §12 writes into that scenario (it takes no
picture, so no twin differs).
§12 repeats U1 and U3 in one launch (12.3.4 launch 13); its `run.txt` lists the names and every event
kind (D445).

### 9.6 Earlier pictures

None (ruling 3): names, live settings and peers take no part in layout or drawing. Proven by a grep
at the review (each `.axaml` hunk adds only `AutomationProperties.*` or an `xmlns` line; the graph's
diff has no `Render`, `AffectsRender`, `InvalidateVisual`, measure or arrange), by U1's same bytes, and
by `scripts/compare-pictures.py` against wave 1's references: nothing outside §8's About region.

### 9.7 Lagged list, run time, real-window pass

Lagged list: nothing new; 8a's project is its last line, and the App.Tests classes run no git. Default
run: App.Tests about 45 tests without a session (under a minute); 8a's project about 15 sweep starts
and U's three methods (3 to 5 minutes alone). Real-window pass: one launch, 45 to 60 s. The spike runs
once, outside the pass.

### 9.8 Done when

1. The spike's events are recorded; the route is the public one, or a decision names the private one.
2. 9.4's classes, `Phase8aChecksU` and `Phase8aExpectationTests.U` pass; each new class failed first.
3. 9.6's grep is clean; `IEmbeddedRootProvider`, `[PrivateApi]` and a window peer appear nowhere in
   `src/` without that decision; the build has no warning.
4. 9.6's comparison shows nothing of this package; the lagged list passes; About's states are swept.
5. §12 has the outline and `Phase8aExpectations.U`, the lead the Narrator steps, §11 the sentences.

### 9.9 Risks, the unverified and open points

1. A row's node exists only once a client has reached it (uia.md 2): S1 tests the cold case, steps 2,
   3 and 5 Narrator. Silence found at the gate reopens the route: a decision and a gate round.
2. Narrator may read a List's selection on focus, and the row twice (step 2).
3. Whether a live region's help text is spoken (the count, the search) is unverified (step 6); the
   details panel's "3 commits selected" says it too.
4. `OperationStatus` may chatter through git's progress (S5, step 14).
5. `StringFormat` with `x:Static`, and the row's parent: the build and a test verify; the fallbacks
   are a converter and the parent Avalonia sets when it lists children.
6. VoiceOver may announce a live Name change (uia.md 2), untried; Linux announces nothing (D411).

The spike's branch `wip/p8a-uia-spike` is never pushed and is deleted after §9's merge, its events
in the merge notes. Open points: 13.4, items 13 to 16.

## 10. perf: the performance pass, measures first

Wave 3, after WP0; branch `wip/p8a-perf`, no part of checks. Carries rulings 2, 19, 23, 65, 66,
plan.md's line 761, risk 10, D47, D50, D280 and 1.9, 1.14, 1.15.

### 10.1 What the package delivers, and what it leaves

Delivers: `StartupTimeline`'s real body (1.9); two log lines of `RepositoryViewModel` (the reload
and the managed heap); `RealApp.Memory()` and `RealApp.LogTime(…)`; the opt-in class
`Phase8aPerformance` with one fact for each of M2 to M7 (10.3.4); a budget for a Pull's git calls.
After wave 3 the lead runs the "before" series on the rested laptop and records it (10.3.6).

Leaves: every change a measure calls for, to a fix package after a new ruling (ruling 66); the
guide's sizes, as indications, to §11 (ruling 19), from the numbers the lead hands it at wave 4's
start; risk 10, architecture.md and CLAUDE.md's row for the series, to the lead. Out of the phase:
risks 44 and 38 (ruling 2); any write to a user's repository for speed (ruling 19, D47); deferred
background loads without a new ruling (ruling 66); an incremental reload (D47, D52); numbers on
macOS and Linux (D50); the installed build (8b).

Decisions: D436 (rulings 19, 65, 66), the measures M1 to M7, the bounds (the slowest "before" run
times 1.25) and the opt-in class, since start-up, memory and the reload were never measured and the
laptop's warmth spreads Q1 by about 30 %; D437, how the series is taken (10.3.4, 10.3.5); D438, log
lines that say only what is true (10.3.1, 10.3.2), with the heap read from
`GC.GetGCMemoryInfo().HeapSizeBytes` and no forced collection.

### 10.2 Files and contracts

Changed (each exists, or exists after WP0):
- `src/VisualCommit.App/Services/StartupTimeline.cs` (WP0's stub): the real `StartupTimeline` and
  `FromProcess` (10.3.1).
- `src/VisualCommit.App/ViewModels/RepositoryViewModel.cs`: in `RunRefreshesAsync` (:726-779) a
  stopwatch per pass of its loop and the reload line after `SwapGraph` (:761); in `LoadAsync`
  (:1700-1750) the heap line after the `loaded` line (:1749).
- `tests/VisualCommit.RealWindowTests/RealApp.cs`: `Memory()` and `LogTime(…)` beside `ReadLog`.
- `tests/VisualCommit.App.Tests/RepositoryViewModelTests.cs`, `RepositoryViewModelFilterTests.cs`,
  `RepositoryOperationsPullTests.cs`: new tests only (10.4).

New: `tests/VisualCommit.App.Tests/StartupTimelineTests.cs`;
`tests/VisualCommit.RealWindowTests/Phase8aPerformance.cs`, `PerformanceSeries.cs` (the series
file, median, range, bound), `PerformanceSeriesTests.cs`.

Implements 1.9's `StartupTimeline` and its lines, 1.15's `RealApp.Memory()`, `RealApp.LogTime` and
`Phase8aPerformance`, and reads 1.14's `VISUALCOMMIT_PERF_RUNS`. Uses 1.10's wiring as WP0 makes it
(`AppLaunch.Current.Timeline`, `TabServices.Startup`, `GitFound` once after a first search that found
a usable git, `TabsRestored(opening.Count)`, `RepositoryViewModel.OnRowsDrawn`). The app gains no
member that another package uses. M7 sits in `Phase8aPerformance` beside M2 to M6, so that one
variable and one command take the whole series.

### 10.3 Behaviour

#### 10.3.1 The start-up timeline

`StartupTimeline(processStarted, log, clock)` keeps one flag per line, set with `Interlocked`, so
each line is written once per process whatever the thread (GitFound comes on the thread that
ended the search). `N` is the clock's `GetUtcNow()` minus `processStarted` in whole milliseconds,
rounded down, 0 when negative, invariant culture, no group separator. Each line at Info:

- `WindowOpened()`: `Start-up: window opened after {N} ms since the process started.`
- `GitFound()`: `Start-up: Git found after {N} ms since the process started.`
- `TabsRestored(k)`: writes nothing; keeps `k`, the first call only.
- `FirstRows(name)`: `Start-up: first graph rows of {name} after {N} ms since the process started ({k} restored tabs).`,
  `(1 restored tab)` for one; written at the first call, and only when `TabsRestored` came before
  it with `k` of 1 or more. With no restored tab the user's own open has Q1's line, and the
  timeline writes none.

The line is literal: when the restored active tab shows no graph (a dashboard, an empty tab, a
tab whose opening failed), it comes when the user first shows a graph, and its time says so. M2
and M3 always restore a graph as the active tab.

`FromProcess(log)`: `Process.GetCurrentProcess().StartTime` as a local `DateTimeOffset`, the system
clock. When reading it throws (`InvalidOperationException`, `Win32Exception`,
`NotSupportedException`), it returns `NullStartupTimeline.Instance` and logs once at Warning:
`Start-up: the process's start time could not be read, so no start-up times are logged.` The
headless harness never makes a timeline (1.9), and `Program.Main` makes it before §4's guard is
taken, so M2 includes the guard's cost.

#### 10.3.2 The reload and the heap

`RunRefreshesAsync`: a `Stopwatch` starts before `Repository.ReadRefsAsync` (:744) at each pass of
the loop; the pass keeps `_fingerprint` as it was before `ApplyRefs`. After `SwapGraph(data)`, when
`refs.Fingerprint` differs from it: `{name}: graph reloaded after a change of the refs, {n} commits in {N} ms.`
at Info, `n` the new graph's count. A refresh with no change, a swap for the filter alone and
summary mode write nothing. Reached by the watcher's refresh and by an operation's end
(`RefreshScope.Refs`, :145; the toolbar's Fetch, `RepositoryOperations.FetchPull.cs:57-67`).

`LoadAsync`, after its `loaded` line, for every load (the first one and each reload):
`{name}: managed heap {N} MB after loading {n} commits.` at Info, `N` the heap size of the last
collection in bytes divided by 1,048,576, rounded half away from zero. Neither line starts a process
or waits on one (Q2); each is one append of the log, as the `loaded` line is; no git call is added.

#### 10.3.3 The harness's helpers

In `RealApp.cs`: `public (long WorkingSet, long Private, long PeakWorkingSet) Memory()`, after
`_process.Refresh()`, from `WorkingSet64`, `PrivateMemorySize64` and `PeakWorkingSet64`; and
`public static DateTimeOffset? LogTime(string dataDirectory, Regex line)`: the first log line that
matches, its time parsed from the text before ` [` with the format `yyyy-MM-dd HH:mm:ss.fff zzz`
(`FileAppLog.cs:63`), null when no line matches. The offset makes it an instant, so the harness
compares it with its own `DateTimeOffset.Now` on the same machine.

#### 10.3.4 The measures

The 100k repository is `LargeHistory` (`large-history-v1`), only read. Clones are made by
`git clone --local` into a `TempDirectory("perf")`, with `gc.auto 0` and `maintenance.auto false`
as every `TempRepo` has (`TempRepo.cs:162-170`), so git writes no commit-graph between "before" and
"after", made lazily inside the first fact that needs them, deleted when the class ends. M2 and M3
each make one uncounted warm-up launch first (a first start after a build is the coldest); M4 and M5
share one launch per run, so M4 is taken five times. Every launch has a data
folder of its own and the environment of `RealApp.Launch`, plus
`GitRepository.WatcherLagVariable` removed (null), so a shell left with the lag (0.4) does not
slow the series. The harness's own commits use `TempRepo.IsolatedEnvironment` with
`Perf Author <perf@example.com>`.

| # | Measure | Repository | How | Value per run | Trigger |
|---|---|---|---|---|---|
| M1 | Q1 | `LargeHistory` | phase 1's pass, unchanged | first rows, p95, longest | its bounds 2,000, 8, 33 ms stay |
| M2 | start-up, one tab | `LargeHistory` | one restored tab; wait for the first-rows line and `loaded 100000`; close | the first-rows line's N; the window and Git lines noted | none but the bound |
| M3 | start-up, ten tabs | `LargeHistory` active (index 0), clones `large-clone-1` to `-9` | as M2; close after all ten `loaded` lines; the working set then noted | the line's N, `(10 restored tabs)` | median above 1.25 × M2's |
| M4 | memory | clone `large-reload` | in M5's launch: `Memory()` 2 s after `loaded` and 2 s after the reload line; heap lines noted | working set after the load; peak after the reload | peak above twice the working set after the load |
| M5 | reload, outside commit | clone `large-reload` | `git commit --allow-empty -m "Perf run {i}"`; T0 = `DateTimeOffset.Now` when git returns; wait for the reload line with `100000 + i` commits | `LogTime(line) − T0`; the line's N noted | the line's N, median, above 2,000 ms |
| M6 | reload, fetch | `large-remote.git` (`clone --bare --local`) and its clone `large-fetch`, opened | before run i, `commit-tree` and `update-ref refs/heads/main` in the remote; `loaded`, 2 s, T0, click `FetchButton`; wait for the reload line with `100000 + i` commits | as M5; the `Fetch: Succeeded after` span (`AppLogFile.OperationEnded`) noted | as M5 |
| M7 | Pull's settings reads | `Scenarios.RemoteSyncAsync()`, no pull key | 20 calls of `ReadPullConfigurationAsync("main")`, a `GitRunner` with the scenario's `Environment`; no window | median of the 20 | median at 250 ms or more: S8's batch is a candidate |

In run i, M5's and M6's clone opens with `100000 + i − 1` commits, so each wait names its count.
Every run of M2 to M6, before and after, asserts that each repository it measures has neither
`objects/info/commit-graph` nor `objects/info/commit-graphs/` in its git folder (D47), the shared
one included; a failure names the repository and the file. Only M6 gives input; M2 to M5 need the
desktop and no mouse. The window stays as the app opens it (no resize during a measure).

#### 10.3.5 `Phase8aPerformance`

One `[Fact]` per measure: `M2_start_up_with_one_tab`, `M3_start_up_with_ten_tabs`,
`M4_M5_memory_and_the_reload_after_an_outside_commit`, `M6_the_reload_after_a_fetch`,
`M7_pulls_settings_reads`. Each starts with
`Assert.SkipUnless(PerformanceSeries.Runs is not null, "VISUALCOMMIT_PERF_RUNS turns on the performance series; only the lead sets it, on the rested laptop.")`;
`Runs` is the variable's value read as an integer from 1 to 20, null when unset; any other value
fails the fact. The class has no fixture that does work before the skip: the gate's pass shows five
skipped tests and writes nothing for them.

`PerformanceSeries` writes `artifacts/perf/phase-8a/series-<yyyyMMdd-HHmmss>-<commit>.txt` under the
`RepoRoot` metadata, `<commit>` from `git rev-parse --short=7 HEAD` there, with `-changes` when
`git status --porcelain` is not empty. Its lines: a head line with the date, the commit, git's
version and the runs; `M2 warm-up: {N} ms`; `M2 run {i}: {N} ms` (with the noted values in
brackets); per measure `M2: median {m} ms, range {a} to {b} ms, {n} runs; bound {B} ms, held` (or
`bound not set`); memory in MB of 1,048,576 bytes. Each wait has its own timeout through
`RealApp.WaitFor` (one load 120 s, ten loads 300 s, a reload 60 s), so a run that hangs fails with
its number. The real-window project has no hang watchdog (its `AssemblyInfo.cs` sets only
`CollectionBehavior`), so CLAUDE.md's 3 minutes do not stop M3's five runs (about 4 to 6 minutes);
if the watchdog is ever added there, M3 is split into one fact per run.

Bounds: constants in `Phase8aPerformance`, null until the lead writes them after the "before"
series (10.3.6); while null, a fact records and asserts only its guards; once set, each counted run
asserts its value at or below the bound.

#### 10.3.6 The series and the decisions (the lead's)

1. On wave 3's last merge, after its lagged list and whole run (0.4), with no builder working
   (ruling 23): the owner told (the desktop for about 15 minutes, M6 clicks Fetch); the laptop
   rested 10 minutes, away from the night's scheduled tasks (risk 10). Then
   `VISUALCOMMIT_PERF_RUNS=5 bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release --filter-class "*Phase8aPerformance"`.
2. The bounds, written in a commit of their own: the slowest counted run times 1.25, rounded up,
   times to 100 ms (M7's to 10 ms), memory to 10 MB. The triggers of 10.3.4 are read against the medians; they are written here,
   before the runs.
3. A trigger that fires becomes a new ruling with its numbers, or a backlog row with its reason.
   Candidates: M3, background tabs restored in the existing summary mode and promoted when shown
   (`RepositoryViewModel.Promote`), which breaks `InitializeAsync`'s "completes when every restored
   tab has loaded its history" (`MainWindowViewModel.cs:156-160`) and the waits on it, so its ruling
   lists the checks it touches; M4 to M6, an incremental reload, for the backlog unless a stall is
   seen (D47, D52); M7, S8's batch of Pull's reads, 3 calls to 1, whose ruling names the earlier
   tests it rewrites (ruling 66; lines in perfsingle.md (a)): `Phase6Checks.G.cs`,
   `Phase6Checks.S.Pull.cs`, `Phase6Expectations.S.cs`, `Phase6Checks.W.PullAll.cs`,
   `Phase6ExpectationTests.W.cs`, `RepositoryOperationsPullTests.cs`, `SettingsDialogViewTests.cs`,
   `PullConfigurationTests.cs`, `PullFastForwardSettingTests.cs`; no picture opens a Pull's row.
4. A change: the measure commit and the fix's commit in two worktrees, the class run alternately A,
   B, A, B, five times each with `VISUALCOMMIT_PERF_RUNS=1`, rested.
5. The report (`docs/test-reports/phase-8a.md`, a section of its own), one row per measure:
   before (median, range, runs), after (median, range, or "no change"), bound, decision (none,
   with the trigger's numbers; the ruling and its fix package; or the backlog row). Plan.md's risk
   10 gains the medians. The guide's page (§11) names 100,000 commits, ten restored tabs of that
   size and the memory measured, as indications on the laptop (ruling 19), and may tell the user
   how to write a commit-graph; the app writes none.

### 10.4 Tests

The builder runs these classes only, with plain `dotnet test` (D245).

`StartupTimelineTests` (App.Tests, new, no session, `ManualTimeProvider` and `RecordingLog`):
each line's exact text at Info with N from the clock (1,234.9 ms gives `1234`); a second call of
each writes nothing; `(1 restored tab)` and `(10 restored tabs)`; no first-rows line without
`TabsRestored` or after `TabsRestored(0)`, and the first `TabsRestored` wins; a clock before the
start gives 0; a hundred calls of each from parallel tasks give one line each; under `de-DE` a
time of 12,345 ms reads `12345`; `FromProcess` gives a timeline whose window line has N of 0 or
more. Every test that expects a line fails against WP0's stub.

`RepositoryViewModelTests` (extended, real git): after an outside commit and `RefreshAsync`,
exactly one `graph: graph reloaded after a change of the refs, 14 commits in {N} ms.`; a refresh
with no change writes none; the heap line once after opening (13 commits) and once after the
reload (14). `RepositoryViewModelFilterTests` (extended): `HideBranchAsync` reloads the graph and
writes no reload line. These fail before the change.

`RepositoryOperationsPullTests` (extended): a theory on `Scenarios.RemoteSyncAsync`, a
fast-forward Pull and a merge Pull (the set-ups of
`Pull_fast_forwards_counts_the_commits_and_says_when_there_was_nothing_to_pull` and
`Pull_merges_diverged_branches_as_git_pull_would_and_says_so`), holds the calls with an operation
id (`OperationCalls`) to `GitCallBudget.AssertAtMost(calls, budget, …)`, each budget the count
measured, with a comment naming git's version and the date (D280: a count that is a cost). It
passes on today's code by design and fails when a read is added. The watcher's refreshes carry no
operation id, so the lag of 0.4 cannot change the count.

`PerformanceSeriesTests` (RealWindowTests, new, no window): the median of odd and even counts, the
range, the rounding (1,513 ms × 1.25 gives 1,900; 412 MB gives 520; M7's 37 ms gives 50), the
variable's parsing (unset, `5`, `0`, `x`).

`Phase8aPerformance` is written and built here (CI's Windows job builds the project) and first runs
at the merge (10.8). No earlier test changes: none counts a log's Info lines, and the patterns that
read Q1's, the `loaded` and the first-rows lines match neither new text (Grep; `SummaryModeTests.cs:56-62`
sees no full-mode load).

### 10.5 Visual checks

None: the package has no part (0.5). The Delivers item "Performance pass" is proven by the
series' table in the report and by Q1 in phase 1's pass (0.1).

### 10.6 Earlier pictures

None. The app's log is a file; the activity log shows the operation log and the git-call log
(`AppSession.cs:205-207`), not `IAppLog`; no git call and no control is added. Proof: the builder
runs phase 1's walk-through (`dotnet test --project tests/VisualCommit.VisualTests --filter-class
"*Phase1Checks"`, the graph and the 100k repository) and
`python scripts/compare-pictures.py D:/Projects/Personal/phase8-work/references/wave1 artifacts/visual <folder>`
lists no changed picture of phase 1; wave 3's whole run confirms it for every phase.

### 10.7 Lagged list, default run, real-window pass

Lagged list: nothing new; the lead runs 0.4's list at the merge. Default run: about 15 tests in
App.Tests, under a minute of its own (the timeline's in-process; four with real git). Real-window
pass: five skipped facts and four pure tests, well under a second, no launch. The series, outside
the gate: 12 to 18 minutes per run of the class at five runs (clones about a minute, M3 about 5
minutes), plus the rest; alternation after a change, ten runs at one run each, 40 to 60 minutes.

### 10.8 Done when

- The classes of 10.4 pass on the branch and on the merge's CI run, the floor job's included
  (a Pull budget is the highest count among the six jobs; the lead raises it at the merge, with the
  reason, if the floor job counts more).
- Phase 1's pictures unchanged (10.6); Phase1RealWindowPass's Q1 code untouched.
- At the merge, the owner told, `VISUALCOMMIT_PERF_RUNS=1` with 10.3.6's command once: every fact
  passes its guards, the file holds each line of 10.3.5, and M6's reload line follows the fetch.
  Without the variable the pass's run of the class shows five skips and writes no file.
- After wave 3: the "before" series recorded, the bounds committed, each trigger decided, the report's
  table filled; a fix package only after a ruling, proven by the alternated series.

### 10.9 Risks, what is unverified, open points

- Unverified: that `Process.StartTime` reads on macOS and Linux (the Warning covers it); whether
  git 2.56's auto maintenance writes a commit-graph in a clone with git's defaults (the clones'
  settings rule it out, the guard would show it); that M6's reload line follows the fetch in the
  real app (read from the code, :145 and `FetchPull.cs:61`). The first run at the merge verifies them.
- M3 holds ten 100k histories at once; if the laptop pages, the time is the machine's, which the
  noted working set shows. Nine 100k clones are the worst case (ruling 65). M6 fetches from a local
  folder: its fetch span is no network's; the reload after it is the measure.
- Risk 10: five rested runs, the slowest × 1.25 as bound; the gate's pass runs Q1 alone, rested.
- The first run at the merge takes the desktop for about 5 minutes: before the merge's lagged list,
  never beside it (ruling 23). Open points: 13.4, items 17 and 18.

## 11. guide: the user guide, its site, pictures and links (D408)

Key `guide`, wave 4, after §5, §6, §8 and §9. Binding: D408, D411; rulings 7, 8, 18, 19, 39, 44,
46, 50, 53, 60 to 64; plan 765, 786. Input: `research2/guide.md`. Its decisions: D439 (rulings 18,
60 to 64), mdBook 0.5.4 pinned, the pictures made in the workflow at scale 2 and not committed,
`GuideShots` outside the solution, deploys from `master` only (Zensical is in beta, and history does
not grow per regeneration); D440, two images per picture; D441, the links' check fails a page that
loads anything from another host; and D417 (F1) and D434 (the guide's spelling).

### 11.1 What it delivers, and what it leaves

Delivers: 33 task-based pages built by mdBook 0.5.4 (ruling 60); a workflow that builds the site
and deploys it to GitHub Pages from `master` only; the pictures, dark and light at scale 2 from demo
data; the links' check; F1, "Open the user guide" and the shortcuts list's last group "Help" (ruling
62); tests that every page and anchor the app links to exists; part G. Leaves: About's "User guide"
link (§8) and the first-run links (§5), whose addresses part G checks; Narrator (§9); Pages turned
on at the 8a merge (ruling 64); the README's link (the lead, after that); `PRIVACY.md` and the install
steps (8b; `install.md` is a frame).

### 11.2 Files

New: `docs/guide/book.toml`, `docs/guide/guide.css`, `docs/guide/src/SUMMARY.md` and the pages;
`.github/workflows/guide.yml`; `scripts/check-guide-links.py`; `tests/VisualCommit.GuideShots/`
(csproj, `AssemblyInfo.cs`, `GuideSettings.cs`, `GuideScenario.cs`, `GuideCamera.cs`, `LeakGuard.cs`,
`GuideShotCatalogue.cs`, a shots class per guide part, `GuideCatalogueTests.cs`, `LeakGuardTests.cs`);
`tests/VisualCommit.App.Tests/GuideShortcutsPageTests.cs`; 8a's `Phase8aChecks.G.cs`,
`Phase8aExpectations.G.cs`, `Phase8aExpectationTests.G.cs`.

Changed (paths checked): `src/VisualCommit.App/ViewModels/Commands/AppShortcuts.cs` and
`AppCommands.cs` (11.3.1); in `tests/VisualCommit.App.Tests/` `AppShortcutsTests.cs`,
`SettingsDialogViewTests.cs`, `PaletteEntriesTests.cs` and WP0's `AppLinksTests.cs`; in
`tests/VisualCommit.VisualTests.Phase6/` `Phase6Expectations.S.cs`, `Phase6ExpectationTests.S.cs`,
`Phase6Checks.S.cs`, `Phase6Expectations.K.cs`, `Phase6ExpectationTests.K.cs` (11.4); `.gitignore`
(`docs/guide/book/`, `docs/guide/src/pictures/`); `.github/workflows/ci.yml`, whose step "Build the
opt-in projects" gains `dotnet build tests/VisualCommit.GuideShots --configuration Release`.

The lead's, at the merge: CLAUDE.md's Commands rows (the shots, the book, the links' check, the
guide workflow's runs), its docs map, and "What to know": "A push that changes only docs starts no
CI run; one that touches `docs/guide/**` starts the guide's workflow."; `VisualCommit.slnx`'s
comment names GuideShots as left out.

Contracts: 1.7, 1.8, 1.14's guide variables, 1.15's GuideShots classes, 1.2's first-run ids, 1.3's
`FakeFileLauncher`; `ShellDriver.Start`'s `gitProbe` and §5's `FakeGitSetupProbe`.

### 11.3 Behaviour

**11.3.1 F1 and the palette.** 1.8's binding goes into `AppShortcuts.Bindings` after `PreviousRegion`,
1.8's `OpenUserGuideText` into `AppCommands`, and into `Resolve`
`case AppCommand.OpenUserGuide: return Always(OpenUserGuideText, () => Start(OpenUserGuideAsync(), OpenUserGuideText));`.
Listed on every tab (repository, dashboard, empty, the first-run screen), always enabled; the palette
row reads `Open the user guide`, key `F1`, no detail. The new private `OpenUserGuideAsync` opens
`AppLinks.Guide(GuidePage.Start)` through `Services.Launcher.OpenUrlAsync`; without a launcher it logs Info
`Open the user guide: there is no launcher.`; an unexpected failure is `Start`'s Error
`Open the user guide failed.`. F1 opens the start page on every tab (D417). The router's
rules hold: with a dialog or a menu open, or during a drag, F1 goes on and opens nothing; Shift+F1 and
Ctrl+F1 route nowhere (D213 leaves Ctrl+F1 free).

`SettingsGroups` gains 1.8's last group after "Dialogs and menus":
`new("Help", [FromCommands(AppCommands.OpenUserGuideText, AppCommand.OpenUserGuide)], platform => platform == CommandKey.Cmd ? <1.8's note> : null)`.
On Windows and Linux: `Open the user guide` · `F1`, no note; 12 groups, 51 rows. No settings row
links to the guide, so the list's top keeps its look (ruling 62).

**11.3.2 The pages.** CommonMark with GFM tables, `> [!NOTE]` and `{ #id }` ids; keys written
`Ctrl+B (Cmd+B on macOS)`; each step readable without its picture; screen words quoted as shown;
American spelling (D434). A slug never changes once the app links to it. `SUMMARY.md` order:

| Part, page | It must say | Shots |
|---|---|---|
| Introduction: `index.md` | What the app does, the systems, free and MIT; links the README, where D404's note stays alone | `main-window` |
| `what-it-does-not-do.md` | O10 to O18, O20 to O22; no high-contrast palette (D411); commits apart only by the mouse (ruling 50) | none |
| Getting started: `install.md` | Per system: "The installers come with the public beta", build from source; "What an uninstall leaves" (data folder, keychain entries, the global git configuration, D190) | none |
| `first-run.md` | Git 2.34.1 or newer (ruling 69); the five states and buttons in §5's words; `winget install --id Git.Git -e --source winget`; the Command Line Tools or `brew install git`; Linux as §5's table; the status bar's Git box; tested with Git for Windows (ruling 46) | `first-run-` + `missing`, `missing-macos`, `missing-linux`, `too-old`, `not-working`, `winget-confirm`; `status-bar-git` |
| `open-clone-create.md` | Ctrl+O, Ctrl+Shift+C, Ctrl+Shift+I, the recent list | `welcome-page`, `clone-form` |
| `tour.md` | The window's regions | `main-window` |
| Everyday work: `see-changes.md` | Side by side, inline, images | `diff-side-by-side`, `diff-inline` |
| `stage-and-commit.md` | Files, hunks, lines; Ctrl+Shift+A, X, M; Ctrl+Enter; amend; discard | `working-changes`, `stage-hunk` |
| `branches.md` | Create, check out, rename, delete, folders, the filter | `branch-menu`, `create-branch` |
| `sync.md` | Fetch, pull, push, safe force-push, upstream, auto-fetch, remotes, Cancel | `ahead-behind`, `manage-remotes` |
| `stash-and-tags.md` | Stash, pop, apply, drop; tags | `stash-in-graph`, `create-tag` |
| Combine and rewrite: `merge.md` | Merge, fast-forward, a branch dragged onto another | `drag-menu` |
| `rebase.md` | Rebase, pull with rebase, the interactive list | `interactive-list` |
| `pick-revert-reset.md` | Cherry-pick, revert, reset | `commit-menu`, `reset-question` |
| `conflicts.md` | The resolver, continue, abort | `resolver` |
| Find history: `graph.md` | Reading it; Ctrl- or Shift-click; solo, hide | `graph-labels` |
| `search.md` | Ctrl+F, F3, Shift+F3; in changes (O7) | `search-bar` |
| `history-and-blame.md` | Ctrl+Shift+H, Ctrl+Shift+B; a blame run by mouse or arrows | `file-history`, `blame` |
| `compare.md` | Two commits or branches (O6) | `compare` |
| Stay safe: `undo-and-reflog.md` | Undo, redo, backups, reflog | `undo-done`, `reflog` |
| `protection.md` | Refusals, the automatic stash (D138), ignored files kept (D193) | `refused-operation` |
| `activity-log.md` | Ctrl+J: every git command, output, time | `activity-log` |
| Hosting accounts: `accounts.md` | Tokens for the three services; keychain or this session only (D253); sign out; clone from the account; the netrc note (ruling 8) | `accounts-section`, `sign-in-dialog` |
| `pull-requests.md` | List, open in the browser, create, check out | `pull-requests` |
| Many repositories: `tabs-and-workspaces.md` | Tabs, workspaces, the dashboard | `dashboard` |
| Make it yours: `settings.md` | The six sections, the theme, the git program row (applies at the next start, ruling 42), the palette | `settings-general`, `settings-git`, `palette` |
| `keyboard-and-screen-readers.md` | F1, F6, Shift+F6, Shift+F10 or Menu, Esc, Fn on a Mac; each item of §6.3.10 (ruling 53); a name on every control, the graph's row read (D411); Narrator on Windows the one reader tested (ruling 39); Windows' high contrast followed as light or dark (D411) | none |
| Troubleshooting: `troubleshooting/git.md` | "How VisualCommit finds Git" (ruling 4's order); `{ #git-missing }`, `{ #git-chosen-missing }`, `{ #git-too-old }`, `{ #git-not-working }` (a broken `~/.gitconfig` names its line), `{ #helper-missing }` | the first-run shots |
| `troubleshooting/sign-in-and-ssh.md` | The dialogs; "An OpenSSH older than 8.4 ignores VisualCommit's sign-in dialog and asks in the terminal VisualCommit was started from. RHEL 8 ships 8.0, and the OpenSSH built into Windows can be older where you chose it." (ruling 7); the netrc paragraph, git-lfs on every version (ruling 8) | none |
| `troubleshooting/data-and-logs.md` | Data and log folders per system (macOS's as §4 settles it); the activity log; reporting (8b's channel) | none |
| `troubleshooting/large-repositories.md` | 100,000 commits tested, first rows in about 2 s (Q1), §10's numbers as indications; no writes for speed; `git commit-graph write`, `git maintenance start` (ruling 19) | none |
| Reference: `reference/shortcuts.md` | The generated tables between `<!-- shortcuts:start -->` and `<!-- shortcuts:end -->` | none |
| `reference/about.md` | The About section and how to open it; MIT License; the notices; "This site is built with mdBook (Mozilla Public License 2.0)" and its fonts' licences as the first build shows them | `about-section` |

42 shots, 84 files, an estimated 13 to 21 MB per build.

**11.3.3 The site.** `book.toml`: title `VisualCommit user guide`, `src = "src"`, `language = "en"`;
`site-url = "/VisualCommit/"`, `default-theme = "light"`, `preferred-dark-theme = "navy"`,
`git-repository-url` the repository, `additional-css = ["guide.css"]` (at the book's root: mdBook
reads `theme/` as its theme folder); `[output.html.playground] runnable = false`. Search stays on; no
analytics, no font from another host (ruling 18). `guide.css`: `--content-max-width: 1000px`;
`html.light` and `html.rust` hide `img[src$="-dark.png"]`, `html.coal`, `html.navy` and `html.ayu`
hide `img[src$="-light.png"]`. A picture is two images, light first (D440):
`<img src="../pictures/stage-hunk-light.png" srcset="../pictures/stage-hunk-light.png 2x" alt="…">`.

**11.3.4 `guide.yml`** (guide.md 2's outline). Triggers: `push` on `docs/guide/**`, the workflow,
`scripts/check-guide-links.py`, `src/**`, `tests/VisualCommit.Testing/**`,
`tests/VisualCommit.GuideShots/**`, `Directory.*.props`, `Directory.Build.targets`, `global.json`;
`workflow_dispatch`; no `pull_request`. Concurrency per ref, `master` never cancelled. Job `build`
(ubuntu-latest, 30 min): checkout@v7, setup-dotnet@v6; "Pictures" (15 min) makes `/home/demo`, owned by
the runner, and runs `dotnet test --project tests/VisualCommit.GuideShots -c Release` with
`VISUALCOMMIT_GUIDE_ROOT=/home/demo`, `VISUALCOMMIT_GUIDE_PICTURES=$GITHUB_WORKSPACE/docs/guide/src/pictures`;
"Book" downloads `mdbook-v0.5.4-x86_64-unknown-linux-gnu.tar.gz`, checks it with `sha256sum -c`, runs
`mdbook build docs/guide`, `python3 -I scripts/check-guide-links.py --self-test`, then the check on
`docs/guide/book`; on `master` `upload-pages-artifact@v5`, elsewhere `upload-artifact@v7` as
`guide-site`, 14 days. Job `deploy`: only on `master`, `pages: write`, `id-token: write`, environment
`github-pages`, `configure-pages@v6`, `deploy-pages@v5`. mdBook publishes no hash: the lead takes the
archive's `sha256sum` once, for the package's first commit, and dates it in the report.

**11.3.5 GuideShots.** `net10.0` (the workflow runs it on Linux), Exe, xunit.v3 and
Avalonia.Headless.XUnit; references App, Core, Git, Hosting, Testing; `AssemblyMetadata` RepoRoot;
outside the solution. `AssemblyInfo.cs`: `AvaloniaTestApplication(typeof(HeadlessTestApp))`,
`HangWatchdog`, `CollectionBehavior(DisableTestParallelization = true)`, a module initializer:
`GuideSettings.Capture()`, then `HeadlessTestApp.IsolateFromTheMachine()`.
- `GuideSettings`: the root set, absolute, outside the temp folder and the repository, and empty or
  holding this project's marker `.visualcommit-guide`; else every shot fails with
  `Set VISUALCOMMIT_GUIDE_ROOT to an empty folder outside the repository and the temp folder, such as /home/demo or C:\demo.`
  It deletes only its own folders there (`Projects`, `remotes`, `.config`, `.template`). Pictures
  default to `<RepoRoot>/artifacts/guide/pictures`, emptied of `*.png` at the run's first save.
- `GuideScenario`, built once into `.template` and copied to the same paths before each start:
  `<root>/Projects/weather-station`, about 30 commits by "Alex Demo", "Sam Example" and "Kim Sample"
  at `example.com`, dated 2025-11-03 to 2025-12-19 UTC (before the app's own commits at 2026-01-02,
  `GitIsolation.AppCommitDates`); branches `main`, `feature/forecast`, `feature/units`, `fix/rounding`;
  tags `v1.0`, `v1.1`; one stash; `origin` `https://github.com/alex-demo/weather-station.git`, its refs
  written from a bare repository under `<root>/remotes`, one ahead and one behind, never fetched; two
  changed files and an image; its own `user.name` and `user.email`. `recipe-box` for the dashboard;
  conflict and rebase states on demand. Data folder `<root>/.config/VisualCommit`, fresh per start.
  Hosting shots: `FakeGitHub.InProcess`, `new HostingOptions(GitHubEndpoints.GitHubCom, () => fake.CreateHandler())`,
  login `alex-demo`. No shot fetches, pulls, pushes or clones.
- `GuideCamera`: `public const double Scale = 2;` (1 is ruling 61's fallback). Each shot is an
  `[AvaloniaTheory]` over dark and light: a fresh `ShellDriver.Start`, then
  `Session.Shell.UseTheme(Dark or Light)` and its wait before the state is set up, so no switch closes
  a menu; `SetRenderScaling(Scale)`, `Settle`, `Capture`; a crop to the control's bounds times the
  scale plus 8 pixels; `LeakGuard`; saved as `<name>-dark.png` or `-light.png`. First-run shots:
  `FakeGitSetupProbe`, a fixed `GitGuidance` per platform (ruling 48), git at
  `C:\Program Files\Git\cmd\git.exe` or `/usr/bin/git`.
- `LeakGuard`: fails the shot, saving nothing, when `VisibleTexts.In(window)` or a visible `TextBox`
  holds the user name (3 or more characters), the machine name, the temp path, the real home folder,
  the repository's path, `VisualCommit.Tests`, `Test Author`, or an e-mail address not at
  `example.com` (ruling 63); it names the shot and the text.
- `GuideShotCatalogue.Names`: the 42 names of 11.3.2.

**11.3.6 `check-guide-links.py`.** Standard library, `python3 -I`. Over each `.html` under the folder
given: every relative `href` and `src` resolves to a file and every `#fragment` to an `id` in its
target; no `script`, `img`, `iframe`, `source` or stylesheet `link` names another host or a
protocol-relative URL (D441); links to outside pages pass. Findings as
`<file>: <url>: <reason>`; exit 0 clean, 1 findings, 2 usage. `--self-test` checks one good and one
bad case per rule in a temporary folder.

### 11.4 Tests

Fail first: `AppShortcutsTests`' F1 and Help, `GuideShortcutsPageTests`, AppLinksTests' pages.
- `AppLinksTests` (WP0's, extended): per `GuidePage`, its `.md` exists under `docs/guide/src`
  (`VisualRun.RepoRoot`), `SUMMARY.md` links it, its anchor stands once as `{ #anchor }`.
- `GuideShortcutsPageTests`: the marked region equals the Markdown made from
  `AppShortcuts.Groups(CommandKey.Ctrl)` and `(Cmd)`: per group `### {Title}`, a table
  `| Action | Windows and Linux | macOS |`, a note once when both agree, else each on its own line;
  on failure it prints the expected region.
- Earlier tests that change, each reason once in the report's "Changes to expected results":
  `AppShortcutsTests` (`OpenUserGuide` leaves the keyless list; the router table gains
  `("Open the user guide", true)`; `MacShortcutTable` gains Help with its note; a fact routes F1 on
  Ctrl and Cmd); `SettingsDialogViewTests.ShortcutTable` gains `new("Help", [new("Open the user guide", "F1")], null)`;
  `PaletteEntriesTests`' two lists end `"About VisualCommit…", "Open the user guide · F1"`; part S
  (ruling 62): `PartS.ShortcutGroups` gains Help, `LastGroup = "Help"`, 11 and 50 become 12 and 51,
  `AssertLastGroupInView` takes the last group's row count, not 3; K10 (ruling 54): from §8's 34, 23
  and 35 to 35, 24 and 36, `PageDownRows` [11, 22, 33], `FirstRowInViewAtEnd` 23, `WheelRow` 28 (the
  identities `Phase6ExpectationTests.K` asserts), `OpsCommands` and `DashboardCommands` gain the row.
- GuideShots: `GuideCatalogueTests` (every picture a page names is in the catalogue and every name is
  used; each `<img>` has `alt`, light before dark, `srcset` "`<src>` 2x" exactly when `Scale` is 2);
  `LeakGuardTests` (one made-up text per rule caught; the demo's texts clean).

### 11.5 Part G's checks (outline)

Dark, 1100×700, the ops scenario; none in the real window, which opens no browser.

| Id | Steps | Expected |
|---|---|---|
| G1 | Ctrl+Shift+P, `guide` (`g1a-palette-guide`); Enter; F1 on the graph; the settings screen open, F1; Keyboard shortcuts scrolled to the end (`g1b-shortcuts-help`) | One row `Open the user guide`, `F1`; Enter closes the palette; the launcher holds `https://rahimpasha.github.io/VisualCommit/` once, twice after F1; F1 over the settings screen adds nothing; the list ends with `Help`, its row whole in view; no git call, no write |
| G2 | `FakeGitSetupProbe` in turn Missing, ChosenMissing, TooOld, NotWorking, HelperMissing: Tab to `GitSetupGuideLink` (`g2a-first-run-guide-link` on Missing), Enter; F1 | Each Enter adds only its state's URL (`troubleshooting/git.html#git-missing`, `#git-chosen-missing`, `#git-too-old`, `#git-not-working`, `#helper-missing`); F1 the start page |
| G3 | The settings screen on About; Tab to the guide link (`g3a-about-guide-link`), Enter | The row's label reads `User guide`, the link `Open the user guide` (1.13); the launcher holds the start page once |

By hand (0.5), from the gate commit's `guide-site` artifact (`gh run download <id> -n guide-site`,
served by `python -m http.server`): every picture, Light and Navy each showing their own; search
finds "rebase"; no other host in the network panel; every `SUMMARY.md` link opens. After the 8a
merge: the live address answers; the environment admits only `master` (ruling 64).

### 11.6 Earlier pictures

As rulings 54 and 62 name: `k10a`, `k10f`, `l31c-palette` dark and light (the scroll thumb,
x 837–838) and their 2 real-window twins at the gate; `s14f-shortcuts-end`, confirmed: the list's
end shows Help, so the section's column changes; `s14e-shortcuts-top` at most in the section's scroll
bar, which the builder measures. Proof: Phase6 and Phase6Look run alone, then
`scripts/compare-pictures.py` against the references current at the merge; any other difference
stops the merge. The guide's pictures are outside `artifacts/visual` and in no list.

### 11.7 Lagged list, time, real window

Nothing new for the lagged list (8a's project is in it). Default run: part G, under a minute;
App.Tests, seconds. CI's Windows job: about a minute to build GuideShots. The guide workflow: 5 to 8
minutes per push of code, an estimate until its first run. Real-window pass: nothing.

### 11.8 Done when

1. The 33 pages in `SUMMARY.md`'s order, each saying its row; those of rulings 7, 8, 19, 39, 46, 50,
   53 and D411 there.
2. 11.4's classes pass; part G passes, its four pictures inspected.
3. The lead's run of GuideShots on the laptop through `heavy-run.sh` passes, `LeakGuard` silent and
   every picture looked at; `compare-pictures.py` shows only 11.6's changes.
4. After the merge's push: the guide workflow green, no deploy job run, `guide-site` looked at; CI's
   Windows job builds GuideShots.
5. The lead's lines of 11.2 are in.

### 11.9 Risks, unverified, open points

- Unverified, read from mdBook's sources: `{ #id }`, raw `<img>` with `srcset`, the copy of
  `pictures/`, the theme classes, a CSS file at the root. The builder's first local build verifies
  them, with the links' check (the lead first puts mdBook 0.5.4 for Windows, hashed, in
  `D:/Projects/Personal/phase8-work/tools/`).
- Scale 2 never ran here; the graph's half-pixel insets or the diff may look wrong. The builder looks
  at every picture of the first run; if wrong, `Scale = 1` and no `srcset`, before the counted round.
- `LeakGuard` sees no drawn text (graph, diff); that text is the demo's, and the gate looks by hand.
- The actions' majors and the environment's default protection are the research's reading; the first
  deploy, at the 8a merge, verifies them. Until then the app's links answer "not found".
- Ruling 44: Git for Windows' own updater stays out of the guide; nobody confirmed it at its source.
- Open points: 13.4, items 19 to 21.

## 12. realwindow: the real-window part for 8a's checks

Status step 14; wave 4, branch `wip/p8a-realwindow`, worktree `D:/Projects/Personal/vc-wt/p8a-realwindow`,
built on the Windows machine after §11's merge, reviewed in step 15 with the rest, merged last, after
the review's fixes. Carries plan.md's Visual test gate step 4, D33, D58, D224, D246, rulings 6, 23, 47,
59, 65, phase 7b's ruling 29 and the 7b handoff's "A real-window launch" pattern.

### 12.1 What the package delivers, and what it leaves

Delivers `Phase8aRealWindowPass`: 13 methods, one launch each, in `Phase8aOrder`, repeating every
step the report marks R (12.5), each capture compared with its scripted twin by the twin's name, at
most 3 % different (D33, D58); B's captures have no twin and are inspected. `RealApp.Phase8a.cs`,
`Phase8aOrderTests` and the csproj's 8a link and stand-in reference. At least one capture of every new
screen: the first-run screen and its confirm, the notice, About and its text dialog, the window's and
the taskbar's icon. Its decisions: D442, one launch per session, each holding exactly its twins' tabs
and starting theme, so that the 3 % (D33) measures rendering and not a tab strip; D443, the masked
notice; D444, nothing pressed that leaves the app; D445, the graph's public events; D446, F4's git.

Leaves: the scripted checks and their expectations (§2, §4 to §9); `Phase8aPerformance` (§10), which
skips in the gate's pass (ruling 65); part G (no real window opens a browser); the checks by hand (0.5);
D246's two runs and the gate's pass (the lead, the owner told); CLAUDE.md's rows (the lead).

### 12.2 Files and contracts

New, all under `tests/VisualCommit.RealWindowTests/`:
- `Phase8aRealWindowPass.cs`: `Phase8aRun` (the fixture: `new RealWindowRun(Phase8aExpectations.Phase)`,
  `DisplayNoted`), `Phase8aOrder` (12.3.4's list), the class's limits (`LoadTimeout` 60 s, `StepTimeout`
  30 s), `Launch`, `Sized`, `Compare`, `CompareMasked` (D443), `InkOf` (12.3.3),
  `AssertDesktopReady` (D224), `AssertNeverPressed` (12.3.2), `NoteLaunch`; and `Phase8aOrderTests`.
- `Phase8aRealWindowPass.L.cs`, `.I.cs`, `.F.cs`, `.K.cs`, `.AB.cs`, `.U.cs`: one file per part, A and
  B together, as they share one launch (§7.5).
- `RealApp.Phase8a.cs`: `ExecutablePath` (§7.5's accessor); `StandInPath`, the stand-in in
  `AppContext.BaseDirectory`, asserted to exist (`The stand-in for git is missing at {path}: build
  tests/VisualCommit.RealWindowTests, which references it.`); `StandInEnvironment(standIn, version,
  failure)`; `StartBeside` (the executable through `StartInfoFor`, its output drained); `Minimise`,
  `IsIconic`, `IsVisibleOnDesktop`, `ForegroundIsThis`, `WindowIcons`, `CaptureScreen`, `TaskbarButton`;
  `OtherWindowOfProcess` (a top-level window but `InstanceNotice`); `BuiltVersionDisplay`
  (`AppVersionInfo.Parse` of the built `VisualCommit.dll`'s `FileVersionInfo.ProductVersion`);
  `RecordSelectedRowEvents` (FlaUI 5.0.0's handlers for `Name` changes and LiveRegionChanged on
  `GraphRows`' subtree, queueing kind, id and time with no UI Automation call inside a handler); a
  nested `Win32` class (`ShowWindow`, `IsIconic`, `IsWindowVisible`, `GetForegroundWindow`,
  `SendMessageW` for `WM_GETICON`, `GetSystemMetricsForDpi`), so the shared `NativeMethods.cs` stays.

Changed:
- `VisualCommit.RealWindowTests.csproj`: after 7b's link, `<Compile
  Include="..\VisualCommit.VisualTests.Phase8a*\Phase8aExpectations*.cs"
  Link="Phase8a\%(Filename)%(Extension)" />` with 7b's comment's form; beside Testing's reference,
  `<ProjectReference Include="..\VisualCommit.GitStandIn\VisualCommit.GitStandIn.csproj" />`, commented:
  the stand-in lands in this project's output, where part F names it (1.1, 1.14).
- `RealApp.cs` (after §10's merge): `Launch`'s lines from `new ProcessStartInfo` to the check's own
  variables move unchanged into a new `private static ProcessStartInfo StartInfoFor(string
  dataDirectory, IReadOnlyDictionary<string, string?>? environment)`, which `Launch` and `StartBeside`
  call. No earlier launch's environment changes.

Uses 1.2's first-run ids and `GitLocator.StandInVariable`, 1.6's `AppVersionInfo`, 1.11's paths and
ids, 1.12's names and `GraphSelectedRow`, 1.14's variables, 1.13's About ids, 1.15's `Phase8aExpectations.Common`,
`IconFile`, `GitStandIn` and each part's `Phase8aExpectations.<Part>` (1.15's last paragraph). `RealApp.Executable`
is private, hence `ExecutablePath`.

### 12.3 Behaviour

#### 12.3.1 Conventions of every method

- Read only first (D224): `RealApp.HighContrastIsOn()`; on, run.txt and the failure say `Windows'
  high contrast is on. The pass needs it off and never changes it (D224): turn it off and run again.`
- A new data folder, the session as its twins', 1100×700 (`Sized`; 200 % on the laptop), dark unless
  named; the scenario copy declared before the app, so the app stops first on a failure.
- After an input, wait for a named condition with a limit, never a fixed time.
- A picture: the pointer at the neutral point unless a menu is open or the check hovers
  (`keepPointer`); `CaptureClient`, `Run.Save`, `Run.AssertMatchesScripted` with the twin's name from
  `Phase8aExpectations.<Part>`, whose pictures must exist on the machine.
- Each launch ends closed by its window with exit code 0, unless the method asserts another; run.txt
  gets its start, its end and every value 12.3.4 reads.

#### 12.3.2 Never pressed (D444)

No step presses, clicks or sends Enter to: the winget confirm's `Install`; `GitSetupDownloadButton`,
`GitSetupMoreLink`, `GitSetupGuideLink`, `GitSetupCopyButton`, `GitSetupShowFileButton`,
`GitSetupCltButton`; About's repository and guide links and the text dialog's "Copy all"; the palette's
"Open the user guide"; the F1 key (it opens the guide, §11); any "Copy …", "Open in browser", "Show in
folder" or "Open in editor". F2's step checks after its Cancel that the log has no `Started in a
terminal:` line and that `NativeMethods.Processes()` lists no `cmd.exe` or `winget.exe` whose parent is
the app (ruling 6). Where the twin opens About from the palette, the pass asserts that the highlighted
row reads `About VisualCommit…` before it presses Enter.

#### 12.3.3 Ink in a capture

`InkOf` is `TextInk.Of`'s rule over the capture's pixels at `Scaling`, with `TextInk.Tolerance` and
the twin's "replaced" colour. Its rectangle is the element's UI Automation bounds inset to hold the
text alone (no caret, icon, key text or border toward the foreground); a box is read unfocused only.
Where no such rectangle exists, the comparison carries the colour and run.txt says so. A miss is
reported to the lead, never met by a wider tolerance (§2.9 item 6).

#### 12.3.4 The launches, in `Phase8aOrder`

1. **`Check_L_changes_placeholders_and_danger_button`** (`Scenarios.ChangesAsync`, one tab). L1: a real
   click on graph row 0, the working row; the ink of `FilterBox`, `CommitSummary`, `CommitDescription`:
   `#8F97A4` on `#272C36`; `l1a`. Ctrl+Shift+E, `sea` typed on the real keyboard: its ink `#E4E7EC`;
   `l1b`; the filter emptied as the twin's L5 starts. L5 dark: the discard of `src/Calculator.cs` asked
   as the twin asks it; a spot of the danger button `#EA7D86` (`AssertColour`), its label's ink `#10121A`;
   `l5a`; Cancel; `git status --porcelain` still lists the file modified. A real click on `ThemeSwitch`;
   L2: the same three boxes `#68707D` on `#FFFFFF`, then `sea`; `l2a`, `l2b`.
2. **`Check_L_graph_danger_item_and_head_row`** (`Scenarios.GraphAsync`). L4 a: `feature/login`'s
   menu by a real Shift+F10, real Down until "Delete…" is highlighted: its ink `#EA7D86` on `#2F3542`;
   `l4a` (pointer kept); Esc. L6: `main`'s row in the left panel a) pointer at neutral: `#7C8CFF`;
   `l6a`; b) pointer at the row's centre: `#8B9AFF` on `#2F3542`; `l6b` (pointer kept); c) a real
   click on the row, pointer at neutral: `#8B9AFF` on `#2A3150`; `l6c`.
3. **`Check_A_and_B_about_and_icon`** (one empty tab). B1 as §7.5 a) to c): the executable's
   associated icon and `WM_GETICON`'s two against the `.ico`'s frames (`IconFile`, the file under the
   `RepoRoot` metadata), within 2; `b1-title-bar-dark-with-frame` and `b1-title-bar-icon-dark` (the title
   bar's left 64 × caption height, four times, nearest-neighbour). A1: About by the twin's route;
   `AboutVersion`'s automation name equals `BuiltVersionDisplay()` (ruling 59); A1's dark picture (the
   twin shows `PinnedVersion`, seven characters apart); at 1920×1080 too when `app.Fits(1920, 1080)`,
   else run.txt says it does not fit (plan step 4). A4: Tab to `AboutShowLicenseButton`, a real Enter:
   the dialog, `TextDocumentText` focused, under 3 % from `a4a-license`; Esc, the focus back on the
   button. Esc. A real click on `ThemeSwitch`; B2:
   `b2-title-bar-light-with-frame`, `b2-title-bar-icon-light`; `TaskbarButton("VisualCommit", 10 s)`,
   captured by its bounds as `b2-taskbar-button` and `b2-taskbar-button-enlarged`; asserted to exist.
   None found: run.txt says `B2's taskbar button: Not run (no taskbar button named VisualCommit was
   found within 10 s).` and the gate checks it by hand.
4. **`Check_I1_second_start_hands_over`** (one empty tab, data folder D). The app `Minimise()`d;
   `session.json`'s bytes read; `StartBeside(D, the same environment)`: the second start exits 0 within
   10 s; the app is not iconic and is visible within 2 s; `ForegroundIsThis` noted, not asserted (the
   test host is not the foreground process); D's log holds the second's `it was asked to come to the
   front, and this start ends.` and the app's `answered activated.` (4.3.5); `session.json`'s bytes
   unchanged; the app closes with 0.
5. **`Check_I2_notice_and_exit_code_2`**. The test opens D's `InstanceLockFile` with `FileShare.None`
   and holds it; `RealApp.Launch(D)` finds the notice as the process's main window, within 15 s; not
   sized. `InstanceNoticeBody`'s name holds D's path; `CompareMasked` with `i2a-notice-dark`. A real
   click on `InstanceNoticeTryAgain`: while it reads `Trying…`, disabled, `CompareMasked` with
   `i2b-notice-trying`; within 10 s `InstanceNoticeStatus` reads `It still did not answer.`;
   `CompareMasked` with `i2c-notice-no-answer`. A real click on `InstanceNoticeClose`: exit 2 within
   5 s; D holds `instance.lock`, `logs/` and the launch's own `harness/`, nothing else.
6. **`Check_I2_notice_try_again_starts_here`**. As 5 to the notice; the test releases the lock; a real
   click on Try again: within 15 s `OtherWindowOfProcess` finds the main window (`GitStatus` in it);
   `instance.json`'s `pid` is the app's; that window closed, exit 0 within 15 s (D422).
7. **`Check_F_missing_offers_keys_and_choose`** (F1's session; `StandInEnvironment("none")`). F1: the
   screen's title, body and buttons as `Phase8aExpectations.F`; `GitStatus` `Git not found`;
   `GitSetupButton` `Set up Git…`; `f1a-missing-windows`. No `GitSetupWingetButton`: F2 is noted `Not run: the app
   found no winget.exe on this machine.` F2: a real click on it; the confirm `Install Git with winget?`;
   `f2a-winget-confirm`; a real click on Cancel; 12.3.2's two checks; the screen unchanged. F13: real F6 until the
   screen's first button has the focus; real Tab through F13's order; Enter on
   `GitSetupCheckAgainButton`: one more search logged, still Missing; once the search ends
   `GitSetupCheckAgainButton` has the focus again (5.3). F4: a real click on `GitSetupChooseButton`; `ChooseFileInDialog("Choose git.exe", git,
   30 s)`, `git` by D446, `GitLocator.FindExecutable(GitSearchContext.FromSystem())` in the
   test process; within 60 s the screen is gone and the restored repository's
   `loaded` and working-tree lines are logged; `GitStatus` `Git {v}`, its help text `{git}, chosen in
   Settings`; `settings.json`'s `gitExecutable` is `git`; `f4a-found-again`.
8. **`Check_F5_too_old_stand_in`** (F5's session; `StandInEnvironment(StandInPath, version: "git
   version 2.29.0")`). The title `Git 2.29.0 is too old`, `{min}` from `GitVersion.Minimum`,
   `GitSetupPath` the stand-in's path, Download shown, no winget button; `GitStatus`'s help text
   `{path}, from VISUALCOMMIT_GIT_STAND_IN (a test switch)`; the log's stand-in Warning once (1.14);
   `f5a-too-old` (the path differs from the twin's, ruling 47). run.txt names the stand-in's path and whether
   it holds a space.
9. **`Check_F6_config_fault_stand_in`** (`failure:` F6's text, `fatal: bad config line 1 in file
   C:/Users/demo/.gitconfig`). `Git is not working`; `GitSetupGitSaid` that line; the fault's guidance;
   `GitSetupShowFileButton` shown, never pressed; `f6a-config-fault`.
10. **`Check_F6b_real_git_broken_config`** (no stand-in; `[user` written into
    `GitIsolation.SessionGlobalConfigFile(D)` before the launch, as phase 6's `globalGitConfig`). The
    screen's title `Git is not working`; `GitSetupGitSaid` starts with `fatal: bad config line 1 in
    file` and names that file (separators and case aside); no picture; the line noted.
11. **`Check_K1_hunk_by_keys`** (`Scenarios.ChangesAsync`). The working row by a real click; real F6
    until `StageFileRow` of `src/Calculator.cs` has the focus; Enter: `DiffText` focused. Tab:
    `StageHunkButton` focused; `k1a-hunk-button`. Enter: `git diff --cached --numstat` holds that hunk
    alone (`Phase8aExpectations.K`), `DiffText` focused; `k1b-hunk-staged`. Shift+Tab:
    `DiscardFileButton` focused; `k1c-toolbar`. F6, Down, Shift+Down twice, Shift+F10: the menu holds
    "Stage selected lines"; `k1d-lines-menu` (pointer kept); Enter: two more lines staged.
12. **`Check_K2_blame_by_arrows`** (`Scenarios.HistoryAsync`, `{ "theme": "Light" }`). The twin's file
    selected as the twin selects it; Ctrl+Shift+B; real Down until the caret is in run 2; `k2a-blame-run`;
    Shift+F10: its menu; `k2b-blame-menu` (pointer kept); Esc twice.
13. **`Check_U_graph_row_and_names`** (`Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`, with
    `u-untracked.txt` written into its work tree first). A real click on display row 1;
    `GraphSelectedRow` read once (its node made); `RecordSelectedRowEvents()`. Real Up, then Down twice;
    after each, within 5 s, a `Name` change or LiveRegionChanged on `GraphSelectedRow`
    (D445); its name and help text as `Phase8aExpectations.U`; `ListItem`; one element in
    the graph's Selection pattern; `WindowLayout.RowHeight` ±1 high inside `GraphRowsBounds()`; the focus
    still `GraphRows`. Names (1.12), each noted with its `AcceleratorKey`: the resolver's five (a real
    double-click on `notes.txt`, phase 4's way; Esc); Ctrl+J: `ActivityLogToggle`, `ActivityLogClose`,
    the three splitters; Ctrl+F: the search bar's three, Esc; the untracked row hovered: `Stage
    u-untracked.txt`, `Discard u-untracked.txt`; Stage clicked, the staged row hovered: `Unstage
    u-untracked.txt`. No picture.

The app shows no text of this package's; the pass's own messages are the lines quoted here.

### 12.4 Tests

- `Phase8aRealWindowPass`, 13 methods (12.3.4). None fails first: each behaviour was proven by an
  earlier package's scripted check; this one proves the real window shows the same.
- `Phase8aOrderTests` (no app; in the whole pass, outside D246's filter): `Phase8aOrder.Methods` names
  every `[Fact]` of `Phase8aRealWindowPass` once; every capture name matches 0.3's pattern.
- Earlier tests changed: none. `StartInfoFor` moves code unchanged; the builder runs phase 0's
  `Check_8_the_real_window_matches_the_scripted_walk_through` alone once, the gate's pass all of them.

### 12.5 Visual checks

No part of its own. The R steps it repeats, by the parts' sections:

| Part | Repeated (of the part's) | Launches | Captures compared | Captures without a twin |
|---|---|---|---|---|
| L | L1, L2, L4 a, L5 dark, L6 a to c (5 of 6) | 2 | 9 | 0 |
| A, B | A1, A4; B1, B2 (4 of 8) | 1 | 2 (3 where 1920×1080 fits) | 6 |
| I | I1, I2 (2 of 3; I1 has no scripted step) | 3, and I1's second process | 3, masked | 0 |
| F | F1, F2, F4, F5, F6, F6b, F13 (7 of 14) | 4 | 5 | 0 |
| K | K1, K2 (2 of 8) | 2 | 6 | 0 |
| U | the row, the names (2 of 3) | 1 | 0 | 0 |
| G | none (no real window opens a browser) | 0 | 0 | 0 |

Dark at 1100×700 unless named (L2, K2 and B2 light). The report marks each R and gives, under "The real
window", the method of each.

### 12.6 Earlier pictures

None changed by this package: its diff is under `tests/VisualCommit.RealWindowTests` alone, so no
scripted picture can change, and `StartInfoFor` changes no launch. Proof: `git diff --stat <base>
wip/p8a-realwindow -- . ':(exclude)tests/VisualCommit.RealWindowTests'` prints nothing; at the gate the
earlier captures differ from phase 7b's only as 0.7 names (§2's, §7's three framed, §8's eight, the two
palette captures). New files: the 31 captures above (32 where 1920×1080 fits) and their 25 (26) difference pictures in
`artifacts/visual/phase-8a/real-window/`, with run.txt.

### 12.7 Lagged list, time, the pass

- Lagged list and default run: nothing. CI's Windows job compiles the project, seconds more.
- Real-window pass: 13 launches of 25 to 60 s, **+9 to 11 minutes, 43 to 45 in all** (0.6, D442). `Phase8aPerformance` skips (`VISUALCOMMIT_PERF_RUNS` unset after
  the lead's series; the skipped count shows it). D246's two runs: about 10 to 12 minutes each.

### 12.8 Done when

The lead checks at the merge:
1. `dotnet build tests/VisualCommit.RealWindowTests -c Release` has no warning; the merge's CI run is
   green in every job, its Windows job compiling the project.
2. 12.6's `git diff --stat` prints nothing.
3. The builder made the twins in one command, since each run empties the scripted folder (`dotnet test
   --project tests/VisualCommit.VisualTests.Phase8a` with `--filter-class` `"*Phase8aChecksL"`,
   `"*Phase8aChecksA"`, `"*Phase8aChecksI"`, `"*Phase8aChecksF"`, `"*Phase8aChecksK"`), then ran each
   method alone (`--filter-method`), the owner told before each, never beside a heavy run (ruling 23):
   all green, captures inspected; phase 0's method alone green.
4. `Phase8aOrderTests` pass; a Grep of the part files finds no press of 12.3.2's list.
5. D246's two runs (the lead, the owner present, after the 8a walk-through on the merge's commit) green,
   made once; each run.txt read: high contrast off, 200 %, the stand-in, winget, the icons, the taskbar,
   the version, U's events and names, every ink.

### 12.9 Risks, unverified, open points

Unverified, settled in the builder's first runs and D246's: the taskbar button's name and reach from
the desktop root, a capture outside the window; `WM_GETICON` and `ExtractAssociatedIcon`'s size under
per-monitor DPI; `ProductVersion` holding `+<commit>` (else `BuiltVersionDisplay` reads the attribute
with `System.Reflection.Metadata`); a Name change or LiveRegionChanged reaching FlaUI (first §9's spike,
ruling 32); full-coverage ink at 200 % under ClearType (§2.9 item 6); a tooltip over `main` in `l6b`;
the notice found by `GetMainWindow`; the stand-in's runtime files in this output (the first build);
F4's dialog title and box 1148 (S14's pattern).

Risks: winget's presence on the laptop decides F1's button and F2's step; a run in the worktree proves
no path with a space (the main checkout's does); the owner's icon, chosen after D246's runs, changes
B's captures, which the counted pass shows.

Settled: §5 gives the focus back after Check again (5.3), and F13 asserts it; U's untracked file
takes no picture (9.5). Open point: 13.4, item 7 (winget on the laptop).

## 13. The lead's rulings, what waits on the owner, and the open points

### 13.1 The rulings in short form

The lead wrote 75 rulings on 2026-11-19, before the drafting (D243), from the readers' questions (1 to
23) and the eight inputs' open points (24 to 75). The design cites them as "ruling n". The owner has
not seen them (13.3). A ruling that a decision amends says so.

**Scope.** 1. 8a is in-app work only: no installer, updater, signing or release; what 8b needs only
where cheap, testable and named (rulings 4, 9, 16). 2. Hosting code is left alone: risks 38, 39, 40,
43 and 44 stay; the netrc note is the one hosting-adjacent change; the live command still runs twice
at the gate. 3. No earlier picture changes but the contrast round, About's section list, the window's
icon in framed captures and what a ruling names; a package that must change another stops and asks.

**First run and git.** 4. A setting for git's program: the setting, then `PATH`, then Git for
Windows' usual folders; the status bar's Git box names the git (its path in the tooltip) and leads to
the guidance. 5. One screen in the welcome page's place, no wizard, shown only while git fails;
"Check again" replaces the detection without a restart and reopens failed tabs; both harnesses reach
the states through a seam. 6. winget on Windows only, where found, after a confirm that shows the
command, in a visible terminal; no check runs the real winget. 7. No `ssh -V` probe: the guide says
what an OpenSSH older than 8.4 does, and the log gets one Info line. 8. The Accounts section's netrc
note: an account signed in for git, git below 2.46, a `.netrc` or `_netrc` in the home folder; the
file's content is never read. 9. A missing askpass helper is a first-run state below git's, with the
path looked at. 10. The floor's work follows CI's job on git 2.34.1; a test takes both wordings where
2.34 words otherwise; `Minimum` moves only in the commit that makes the job green and required.

**Keyboard, accessibility and colours.** 11. Every main action of the toolbar, menus, dialogs,
working changes, diff, resolver, rebase list, dashboard and settings reachable by keyboard; a drag is
covered by its menus; the keys stay a fixed set (D192); each gap is shown by a failing test first.
12. A name on every interactive control, proven by a sweep; the graph exposes its selected row, not
100k elements; one Narrator walk-through by hand. 13. The contrast round is one package and one
comparison, after a pixel scan of phase 7b's pictures; new references after its merge.

**About, notices, icon, guide.** 14. About shows the name and icon, "Created by Rahim Khajei", the
version with the short commit, the copyright line, the licence with its text, the notices and the
repository's link; on macOS the application menu's "About VisualCommit", no "About Avalonia".
15. The notices cover what the build output ships; the runtime's and installers' are 8b's.
16. One instance per data folder: a second start asks the first to come forward and exits; a package
and a decision of its own. 17. The icon's pipeline (SVG masters, a tool, the generated files
committed, the window's and executable's icon) with a placeholder; the owner chooses beside the
build; macOS's `.icon` is 8b's. 18. The guide: Markdown under `docs/guide/`, about 30 task pages,
mdBook unless Zensical is out of beta and clearly better; no analytics, no outside fonts; deployed
from `master` only; pictures from a harness class, outside `artifacts/visual`; opened from the
palette, About and the first-run links; F1 if D213's table leaves it free.

**Performance.** 19. Measurement first: the measures, how they are taken on the rested laptop, a
bound each from the "before" numbers; a change only where a user would feel the cost, proven by the
same measure; the app never writes to a user's repository for speed; the guide names the tested sizes.

**Tests, gate and cost.** 20. `8a` where the 7b handoff writes `8`; a new port only by a decision.
21. CI's limits are raised only by D377's rule; the floor job has its own. 22. The references are
phase 7b's counted round until ruling 13's package replaces them. 23. No agent that runs a process
works beside a heavy run.

**Colours (colours.md).** 24. `VcTextPlaceholderBrush` `#8F97A4` dark, `#68707D` light,
`TextControlPlaceholderOpacity` 1. 25. Dark danger `#EA7D86`, the danger button's label `#10121A`;
`VcLane3Brush` keeps `#E5606B`, pinned by a test. 26. `VcAccentOnRowBrush` `#8B9AFF` dark, light
`#4353D8`. 27. `ThemeTokensTests` one table of text on background, both themes, no gaps. 28. The
earlier expectations the round changes change in its commits, each reason once in the report.
29. A disabled box keeps Fluent's background. 30. The comparison takes the scan's boxes as the
expected regions; the scan's script goes into `scripts/`. 31. The report replaces plan.md's 35 label
pictures by the scan's lists.

**Screen readers (uia.md).** 32. A spike first; the public live region is the route; the
`[PrivateApi]` focus event only by a decision, if the spike finds the region silent. 33. The row's
name: summary, author, date, refs; the short SHA its help text; a ListItem in a List. 34. Names
without the key's text; a row's buttons name their row; no tooltip changes; `ActivityLogToggle` and
the splitters named. 35. No change to the window's peer for `GetFocus`. 36. The status bar's
operation text a polite live region; an item status on the left panel's rows to the Backlog.
37. The sweep lives in 8a's walk-through project. 38. The graph's move to a List is a decision;
architecture.md is rewritten for it at the close. 39. The guide says Narrator on Windows is the one
reader tested.

**First run (firstrun.md).** 40. The "Git program" row always shown, at the global scope's bottom;
nine phase 6 pictures named; written to `settings.json` only when set. 41. A chosen git that is gone
is a state of its own, with no silent fallback. 42. A change while git works applies at the next
start; "Check again" at once. 43. The helper-missing state is proven by scripted checks only.
44. git-scm.com's winget line, in a terminal that stays open, only when git is missing; too old: the
download and "Check again". 45. On macOS `xcode-select --install` without the app's confirm.
46. A Windows git that is not Git for Windows is used, with a Warning. 47. A real-window picture may
show the stand-in's path; the difference stays under 3 %. 48. The screen's platform and winget come
from a seam.

**Keyboard (keyboard.md).** 49. Fixes F1 to F7 as given; F3, "Cancel {title}", by a decision amending
D199 and D213 (F4 amended by D425). 50. Commits not next to each other stay mouse-only. 51. The
create form's "Choose…" fixed by F5-A's means if no picture changes, else a known issue.
52. `AcceptsTab` off on every read-only text view. 53. What no settings row can show goes into the
guide.

**About and notices (notices.md).** 54. "About VisualCommit…" and "Open the user guide" at the
palette's end; six palette pictures and K10's constants named. 55. `THIRD-PARTY-NOTICES.md`: the
components and each distinct licence once; the vendors' files word for word under `notices/`.
56. The themes of unstated origin under TextMateSharp's licence, with a sentence that says so.
57. JetBrains Mono NL Bold stays out. 58. On screen "License", "MIT License"; the docs keep
"licence" (amended by D434 for the notices file and the guide). 59. The version with the short
commit, pinned by the harness.

**Guide (guide.md).** 60. mdBook 0.5.4, pinned by version and hash. 61. Pictures made in the guide's
workflow at scale 2, not committed, scale 1 the fallback; `tests/VisualCommit.GuideShots` outside the
solution, built by CI's Windows job; the branch builds an artifact and deploys nothing. 62. F1 opens
the guide; a last group "Help"; the label "Open the user guide". 63. Made-up authors at
`example.com` and `/home/demo`, with a guard against the machine's paths. 64. Pages' source is set
when 8a is merged.

**Performance, single instance, macOS (perfsingle.md).** 65. M1 to M7, bounds of the slowest
"before" run times 1.25, the triggers; nine clones for ten tabs; the lead's rested series; the gate
keeps Q1 alone. 66. A trigger for batched config reads may change earlier exact-call tests, each
named; deferred background loads need a new ruling. 67. A lock file and a per-run endpoint in
`Program.Main`; the notice with "Try again"; exit codes 0 and 2; 8b's key and the macOS limits are
8b's. 68. The macOS data folder in the docs is right; a test per platform closes plan.md's line.

**The floor (floor.md).** 69. `Minimum` 2.34.1. 70. P1's worktree check on every git, the refusal
naming the path; git's own from 2.35 the second guard. 71. "Required" means `continue-on-error`
goes; no GitHub setting. 72. The scenarios' stashes rebuilt below 2.36; the app's own asserted in
git's words. 73. What the floor makes dead goes: D277's branch, the `--diff-merges` fallback, the
common-dir workaround (its premise only, D420); the 2.36-pinned gates stay. 74. The floor job keeps
its limits; a container job timed out by a slow mirror is run again. 75. One decision for P1, the
stash naming and the patch level; the mirror refspec a plan risk.

### 13.2 What the integration settled

Beyond moving the packages' contracts into section 1: the broker's `gitVersion` goes at §3's merge,
by the lead; `Phase7aExpectations.A.cs` passes from §3 to §8; §5's links stay `VcFlatButton`s, so
`VcLinkButton` stays §8's; §5 gives the focus back after "Check again" (§12's finding); About's guide
link reads "Open the user guide" beside the label "User guide"; A4 is repeated in the real window,
so the text dialog has a real picture; part F's pictures take names that match 0.3's pattern; the
activity log's splitter goes to a fixer (D427).

### 13.3 What waits on the owner, and when

| When | What |
|---|---|
| Now, before wave 1 | Look at the 75 rulings (13.1) and D416 to D446, above all those that amend a ruling: D420 (ruling 73's common-dir item), D425 (ruling 49's F4), D434 (ruling 58). The share of the week before each stage after the design's review (D241, D415). |
| Beside the build | The icon (D407): the lead's candidates (`D:/Projects/Personal/phase8-work/icon/candidates-v1.html`), one to three rounds. Leave for a reverse image search for the look-alike board (D412 covers the name's search only). |
| §9's spike and merge | Told before the spike (a minute of keys and clicks); if the lead asks, a short Narrator listen at the merge (steps 2 to 6 and 14, about 5 minutes). |
| §10's merge and the series | Told before the first run (about 5 minutes of the desktop) and the series (rested, the desktop for 12 to 18 minutes per run, M6 clicks Fetch). |
| Before the counted round | The chosen mark regenerated, or the decision that 8a closes with the placeholder (the handoff says so, the choice moves to 8b) or that the gate waits. |
| Before the gate | The three live tokens made again (GitHub's expires on 2026-11-29, Azure DevOps' and GitLab's on 2026-12-04), through the masked script; told before each live run (D335). Present for D246's two runs of 8a's real-window checks; told before the pass. |
| At the gate, by hand | The Narrator walk-through (about 20 minutes), phase 6's System step (D224), a second start from the taskbar. |
| At "Phase 8a accepted" | GitHub Pages' source set to "GitHub Actions", by the owner or by the lead through `gh api` at the owner's word (ruling 64, D408); then the live address checked. |

### 13.4 Open points

Each with who settles it and before what. "Settled" marks what the lead ruled on 2026-11-19, after
the integration and before the design's review; a section that a ruling changes says so in its own
text.
1. Whether the scan's eight lists enter the repository (`docs/test-reports/phase-8a-contrast/`) or
   only the report's counts and the 41 danger-button names (ruling 31). The lead, before §2's merge.
   **Settled:** the eight lists enter the repository, in `docs/test-reports/phase-8a-contrast/`, in
   §2's commits: they are the names ruling 3 asks for, and the research folder is outside the
   repository. The scan's boxes (`scan.json`) stay outside it; `scripts/scan-contrast.py` makes them
   again.
2. Part L leaves the status bar's failed text and the dashboard's danger cells to `ThemeTokensTests`'
   table and the masked comparison: no scripted check reaches a failure cheaply. The lead, before
   part L's checks are written. **Settled:** accepted; the report's part L says which texts the
   table and the comparison prove and no check shows.
3. The comparison's growth (3 px, 6 at 200 %) and line join (24 px, 48) are tuned on the builder's
   runs; a placeholder in a disabled box, which the scan cannot see, is explained per picture rather
   than by a detector. The lead, at wave 1's comparison.
4. The common-dir item serves a folder opened below its top, so it is no dead code. D420 keeps the
   opening call word for word (phase 5's A2 picture) and gives the second call
   `--path-format=absolute`. The other ways: the call kept with its comment corrected, or the option
   on the opening call with the picture named under ruling 3. The lead, before §3. **Settled:**
   D420's form, unless the design's review finds a fault in it.
5. Accepted risks of §4: a first instance hung before its window keeps answering "starting"; a second
   start waits up to 30 s unseen while the first closes; a data folder on a network share depends on
   the mount's locks. The lead, at §4's merge; plan.md's risks at the close.
6. Ruling 7 asks for a log line when an ssh operation starts; the broker cannot tell ssh from HTTPS
   without the remote's URL, so the line comes once per session, at the first Ask or Deny scope
   (D424). A line per operation would need `RepositoryOperations.Run.cs`, the clone and a URL parser.
   The lead, before §5. **Settled:** once per session, as D424 says.
7. `cmd.exe /K` under Windows Terminal, winget's app execution alias, and whether the laptop has
   `winget.exe` on its `PATH` (1.3): the lead by hand before §5's merge. Without winget, F1's real
   picture lacks the button and F2's real step is "Not run", a part the machine decides, as the
   1920×1080 window. The lead, before the gate.
8. The focus after a row action started by keys (6.3.4): the row now in its place, else the list's
   last row, else the other list's first, else the summary; keyboard.md left it to Avalonia. The lead,
   before §6. **Settled:** this rule, with a test for each of its four cases.
9. A test in CI's macOS job running `iconutil -c icns` on the `.iconset` would settle web-verify B20
   in 8a; ruling 17 leaves the `.icns` to 8b, so §7 has none unless the lead adds it. Before §7.
   **Settled:** §7 adds it. One test, run only on macOS (CI's macOS job; skipped elsewhere), runs
   `iconutil -c icns` on the committed `.iconset` into a temporary folder and asserts exit code 0 and
   a file that starts with `icns`. No Mac has run the app, so CI's job is the only proof before 8b
   that the set is well formed; the `.icns` itself stays 8b's.
10. The owner's icon by the counted round, or 8a closes with the placeholder (13.3). The owner.
11. The look-alike board's reverse image search (13.3). The owner, before the gate.
12. A5's bound of 1 s and End reaching the true last line with word wrap are unverified until part A
    runs; if either fails, no wrap or a later bound. The lead, during §8.
13. A short Narrator listen by the owner after the spike or at §9's merge, so a change of route lands
    before the review, not after the gate. The lead, at the spike. **Settled:** the lead asks the
    owner for it at §9's merge (13.3 names it).
14. If the spike's S1 (cold) finds no event and S2 (warm) does: the public route stands with the risk
    recorded, or a decision names the `[PrivateApi]` route and its guarding test. The lead, at the
    spike, before §9's build goes on.
15. The hunk bars' repeated "Stage hunk" and "Discard hunk" names (`HunkHeaders.cs:252-253`) lie in
    no items container, so the sweep does not flag them; a help text with the hunk's header would
    change no picture, but no ruling takes it. Recommended: the Backlog, beside ruling 36's item. The
    lead, at the close. **Settled:** the Backlog.
16. Whether the Narrator walk-through adds a first-run state (the stand-in's variable in the start
    line). The lead, before the gate. **Settled:** yes, two steps: the first-run screen is the first
    thing a new user with a screen reader meets. The lead gives the owner the start line with the
    stand-in's variables.
17. Whether a measure that a fix changes takes its bound from the "after" series (ruling 65 says
    "before"). The lead, in that fix's ruling.
18. The Pull budget is the highest count among CI's six jobs; if the floor job counts more than the
    laptop, the lead raises it with the reason. At §10's merge.
19. The region of `s14e-shortcuts-top`'s scroll bar: §11's builder measures it, the lead adds it to
    0.7. At §11's merge.
20. mdBook publishes no checksum: the lead downloads the Linux archive once, pins its SHA-256 with the
    date in the report, and puts mdBook 0.5.4 for Windows, hashed, in
    `D:/Projects/Personal/phase8-work/tools/`. Before §11.
21. The 42 shots may cost more than planned; the lead may drop a shot whose state is too costly and
    keep its page's text. During §11.
