# Phase 8a design — Release readiness in the app

This is phase 8a's design: what the phase builds, how it is cut into work packages, the contracts
the packages share, and one brief per package for its builder. Once the phase is built it is kept as
history, as phases 3 to 7b's designs are. Its decisions are recorded only in `docs/decisions.md`
(D243): the design names them by number, D416 to D453, and does not repeat them. The phase's report,
handoff and status cite its sections as "design N.N". Binding inputs: the owner's decisions D399 to
D415; `docs/plan.md`'s "Phase 8a", "Workflow" and "Visual test gate" sections and its risks; the 7b
handoff's "Patterns to follow", "Known issues" and "For the next phase"; the lead's 75 rulings of
2026-11-19 and ruling 76 of 2026-11-22 (D457) ("ruling n"), which section 13 carries, the 75 in
short form. The research is outside the repository: the eight inputs in
`D:/Projects/Personal/phase8-work/research2/` ("colours.md 3.2"), with their measurements, and the
digests and web briefs of 2026-11-18 in `D:/Projects/Personal/phase8-work/research/`, where a
skeptic's file wins over its brief. What the research did not verify is marked unverified, with the
step that verifies it. Line numbers are those of `27c5109`; an earlier phase's design is cited
"phase 4 design 11.10".

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
D399 to D415; plan.md's "Phase 8a"; the lead's rulings ("ruling n": the 75 of 2026-11-19, carried in
short form in section 13, and D457's ruling 76); requirements.md; CLAUDE.md and plan.md's "Workflow"
and "Visual test gate" (D241 to D247). The research is outside the repository:
`D:/Projects/Personal/phase8-work/research2/` (eight inputs, cited "colours.md 3.2") and `research/`
(the digests and web briefs; a web brief's skeptic file wins where they differ). Line numbers are
those of `27c5109`. "§n" is this design's section n, the brief of one package. The design's own
decisions are D416 to D453; they stand in `docs/decisions.md` alone (D243), and the sections cite
them by number.

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
| Icon and branding (R5, D407) | §7 | `IconFilesTests` (items 7 and 8 the `.icns`, D447; item 8 in CI's macOS job); part B; real window B |
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
- **D447** (the design's review, 13.5; amends D429, keeps D407 as the owner wrote it): the tool
  writes the `.icns`; the candidates' rounds kept in `docs/brand/icon/`. §7.
- **D448** (the review; amends D443): the notice's data folder on a line of its own, trimmed at its
  start; the masked comparison masks that line after asserting equal heights; the guard's server
  admits the current user only. 1.11; §4, §12.
- **D449** (the review; amends D423): winget started by the full path the screen found, through
  `cmd.exe /S /K`, in winget's folder; a chosen git and the stand-in full paths (`.exe` on Windows); a
  chosen `/usr/bin/git` keeps the macOS gate; no third-party package source named; the real-window
  first-run checks need winget on the gate's machine. 1.3, 1.14; §5, §12.
- **D450** (the review; amends D445): the graph row's proof a `LiveRegionChanged` event; a Name change
  alone fails. §9, §12.
- **D451** (the review): the guide's workflow runs the two App.Tests classes that read the pages; the
  hand check takes the `guide-site` artifact of the gate commit or of a `workflow_dispatch` run on it;
  D441's check also reads CSS and every loading link; the workflow's token read-only; the demo's
  GitHub owner `demo--alex`. §11.
- **D452** (the review; amends D417): "Cancel {title}" cancels only while the titled operation is
  still the one running; K10 takes a fourth PageDown from §11's merge. 1.8; §6, §8, §11.
- **D453** (the owner's answer of 2026-11-20; amends D405, D423 and ruling 44): winget is offered for
  a missing and for a too-old git, not for one that does not work. 1.3; §5, §12.

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
| colours: the contrast round | 2 | 1 | WP0 | `Tokens.axaml`; added styles in `LeftPanelView`, `RightPanelView`, `CompareView`, `WorkingChangesView`, `FileHistoryView` (`.axaml`); `ThemeTokensTests.cs`, `PanelViewTests.cs`; ruling 28's files (2.4: four App.Tests files, `Phase1Expectations.cs` and the `Phase3` to `Phase7b` `Expectations.Common.cs`, `Phase7bExpectations.A.cs`, `Phase6SupportTests.cs`, `Phase6ExpectationTests.L.cs`, `Phase6Checks.L.Overlays.cs`, the HEAD helpers of phases 3 to 5, `Phase5Checks.F.Filters.cs`); `scripts/scan-contrast.py`, `compare-pictures.py`; `docs/test-reports/phase-8a-contrast/` (2.2); part L |
| floor | 3 | 1 | WP0 | `GitVersion.cs`, `IGitRepository.cs`; `GitRepository.Branches.cs`, `.Search.cs`, `GitRepository.cs`; D277's code in `AskPassBroker.cs`, `AccountBlockViewModel.cs`, `AccountsSettingsViewModel.cs`, `OperationTexts.Hosting.Accounts.cs`; `OperationTexts.cs`; `RepositoryOperations.Branches.cs`; `TempRepo.cs`, `GitWords.cs`; 3.4's test files; `ci.yml` |
| single: one instance per data folder | 4 | 1 | WP0 | `AppPaths.cs`, `SingleInstanceGuard.cs`, `AppLaunch.cs`, `Program.cs`, `App.axaml.cs`, `MainWindow.axaml.cs`, `InstanceNoticeWindow.axaml(.cs)`, `InstanceNoticeViewModel.cs`, `InstanceNoticeTexts.cs`; `GitIsolation.cs:74`'s comment; `AppPathsTests.cs` and 4.5's classes; part I with `InstanceNoticeHost` |
| firstrun | 5 | 2 | §3 | `IGitRunner.cs`, `GitLocator.cs`, `GitSetupProbe.cs`, `OsRelease.cs`, `VisualCommit.Core/ProgramSearch.cs`, `GitConfiguration.cs`, the helper in `AskPassBroker.cs`; `GitAccess.cs`, `FileLauncher.cs`; `MainWindowViewModel.cs`, `.Settings.cs`, `.Git.cs`, `RepoTabViewModel.cs`, `WelcomeViewModel.cs`, `ViewModels/GitSetup/`, `GitProgramViewModel.cs`, `AccountsSettingsViewModel.cs`; `GitSetupView`, `CommitGraphView.axaml(.cs)`, `StatusBarView.axaml`, `SettingsDialogView.axaml`, `SettingsDialogViewModel.cs`; `tests/VisualCommit.GitStandIn`, `FakeGitSetupProbe.cs`, `MergeToolHelpTests.cs` (its `HeldToolHelpRunner` made `internal`, 5.2); 5.4's test files; part F |
| keyboard | 6 | 2 | §2 | `DiffTextView.cs`, `DiffPane.cs`, `HunkHeaders.cs`, `DiffView.axaml.cs`, `BlameTextEditor.cs`, `BlameView.axaml.cs`, `AppCommands.Palette.cs`, `WorkingChangesView.axaml(.cs)`, `WorkingChangesViewModel.cs`, `WelcomeView.axaml.cs`, `CreatePullRequestDialogView.axaml.cs`, `FocusReturn.cs`; `MainWindow.axaml.cs` (D427); `ActivityLogView.axaml` only if 6.3.8's test fails; `HeldGitRunner.cs`; 6.4's test files, `Phase2Checks.Helpers.cs`; part K |
| icon | 7 | 2 | §4 | `tools/VisualCommit.IconBuilder`, `assets/brand/`, `Assets/Icon/`, `AppIcon.cs`, `VisualCommit.App.csproj`, `MainWindow.axaml`, the notice window's `Icon`, `Directory.Packages.props`, `.gitattributes` (`*.icns binary`, D447); `IconFile.cs`; part B |
| about: About and notices | 8 | 3 | §5, §7 | `THIRD-PARTY-NOTICES.md`, `notices/`, `EmbeddedTexts.cs`, `OperationTexts.About.cs`, the About section, `SettingsDialogView.axaml(.cs)`, `SettingsDialogViewModel.cs`, `SettingsSection.About`, the text dialog, `VcLinkButton` (`Theme/Controls.axaml`, `SignInDialogView.axaml`), `Theme/Dialogs.axaml`, `App.axaml(.cs)`, `AppCommands.cs`, the csproj, `ci.yml`; 8.4's earlier files; part A |
| uia: screen readers | 9 | 3 | §5, §6; merges after §8 | `CommitGraphControl.cs`, `CommitGraphControl.Automation.cs`, `GraphRowSpeech.cs`, `AutomationNames.cs`, the names in ten views (9.2), `AutomationSweep.cs`, `ShellDriver.Phase8a.Uia.cs`, `CommitGraphControlTests.cs`, `StatusBarTests.cs`; part U |
| perf | 10 | 3 | WP0 | `StartupTimeline.cs`, `RepositoryViewModel.cs`'s lines, `RealApp.cs`'s helpers, `Phase8aPerformance.cs`, `PerformanceSeries*.cs`; 10.4's test files |
| guide | 11 | 4 | waves 1 to 3 | `docs/guide/`, `guide.yml`, `scripts/check-guide-links.py`, `tests/VisualCommit.GuideShots`, `AppShortcuts.cs`, `AppCommands.cs`'s guide case, `.gitignore`, `ci.yml`'s build line; 11.4's test files; part G |
| restore: the restore order (ruling 76, D457) | 14 | 4 | §10 (M2 and M3 prove it) | `RestoreOrder.cs`, `MainWindowViewModel.cs`, `RepositoryViewModel.cs`'s `FirstPage`, the optional `restoreClock` in `TabServices.cs`, `AppSession.cs` and `ShellDriver.cs` (ruling 87), `RestoreOrderTests.cs`, `RepositoryFirstPageTests.cs`, `RestoreOrderShellTests.cs`, `SettingsEntryTests.cs` (14.2) |
| realwindow | 12 | 4 | every part; the review's fixes | `Phase8aRealWindowPass*.cs`, `RealApp.Phase8a.cs`, `RealApp.cs`'s `StartInfoFor`, the csproj |

**Waves and merges.** Wave 0: WP0 in the main checkout. Wave 1: §2, §3, §4, any order; §2 is the
first package that changes an earlier picture (ruling 13). Wave 2: §5, §6, §7, any order. Wave 3:
§8, §9 and §10, built together; §9 merges after §8 and sweeps About at its merge. Wave 4: §11; the
review before the gate (status step 19) and its fixers; §12, built on the Windows machine at step
18, reviewed at step 19, merges last, after the review's fixes; and `restore`, ruling 76's restore
order (D457 (5); `wip/p8a-restore`): its design section first (section 14, written on 2026-11-23 as
wave 4 started), its tests written first and failing on today's code, a review, and the proof by M2
and M3 with the two builds run alternately and by the lead's probe, subject to the week's share the
owner is asked for before wave 4 (D241). At most three packages are built at once; one owner per
file and test class, and a file passes only at a merge. No two packages of one wave own a file. The
files that pass between packages, in this order: `AppCommands.Palette.cs` (WP0, §6);
`AppCommands.cs` (§8, §11); `App.axaml.cs` (WP0, §4, §8); `Program.cs`, `AppLaunch.cs` (WP0, §4);
`GitIsolation.cs` (WP0, §4); `AskPassBroker.cs`, `AccountsSettingsViewModel.cs`,
`MainWindowViewModelTests.cs` (§3, §5); `MainWindowViewModel.cs` and `.Settings.cs` (WP0, §5;
`MainWindowViewModel.cs` then restore); `IGitRunner.cs`, `GitLocator.cs`, `GitSetupProbe.cs`,
`GitAccess.cs`, `FileLauncher.cs` (WP0, §5); `GitAccessTests.cs` (WP0, whose code review added it
with four tests of the fixed search and of a failing listener; §5 extends the class and does not
create it); `SettingsDialogViewModel.cs` (WP0, §5, §8); `SettingsDialogView.axaml` (§5, §8);
`SettingsDialogViewTests.cs` (§5, §8, §11); `WorkingChangesView.axaml` (§2, §6, §9);
`LeftPanelView.axaml`, `CompareView.axaml` (§2, §9); `StatusBarView.axaml`, `CommitGraphView.axaml`
(§5, §9); `StatusBarTests.cs` (§6, §9); `MainWindow.axaml.cs` (§4, §6); `MainWindow.axaml` (§7, §9);
`ActivityLogView.axaml` (§6 if needed, §9); the notice window (§4, §7); `App.csproj` (§7, §8);
`RepositoryViewModel.cs` (WP0, §10, restore); `RealApp.cs` (§10, §12); `ci.yml` (§3, §8, §11);
`Phase7aExpectations.A.cs` (§3, §8); `Phase7bExpectations.A.cs` (§2, §8); `Phase6Expectations.K.cs`,
`Phase6ExpectationTests.K.cs`, `PaletteEntriesTests.cs`, `CommandPaletteViewTests.cs` (§8, §11);
`AppShortcutsTests.cs` (WP0, §11); `SettingsEntryTests.cs` (restore, which no earlier package of 8a
owned). A package adds `ShellDriver` helpers only in a new partial `ShellDriver.Phase8a.<Key>.cs`;
the one exception is ruling 87's optional `restoreClock`, which restore adds to `ShellDriver.cs`'s
`Start` and `StartWithTabs`. The lead's throughout: `AppSession.cs` with `AppSessionTests.cs`,
`TabServices.cs` (but §8's `SettingsSection.About` and, with `AppSession.cs`, restore's
`restoreClock`, ruling 87), `PromptDialogs.cs` after WP0, `VisualCommit.slnx` (each new project's
line at its merge), `Directory.Build.props`, `CLAUDE.md`, `README.md`, `requirements.md`, `plan.md`,
`status.md`, `architecture.md`, the icon's rounds in `docs/brand/icon/` (D447). Every other doc a
merge needs is the lead's, at that merge.

At §3's merge the lead removes the broker's git version (D420): the `gitVersion` parameter of both
`AskPassBroker` constructors and of `PromptDialogs.CreateBroker`, and the argument in
`AppSession.Start` (`AppSession.cs:254`). If §6's G8 test shows a keyboard move of the activity log's
splitter unsaved (D427), its one line in `CommitGraphView.axaml.cs`, §5's in wave 2, goes to a fixer
after wave 2's merges, with §6's test.

No agent that runs a process works beside a heavy run (ruling 23): a wave's lagged list and whole
default run (D245) run after its last merge, with no builder working; the next wave starts after
them. A mid-wave merge's lagged list runs only while no builder of the wave runs a process: the
builders pause until it ends. A builder runs only the classes and methods its section names, never a
whole project; whole projects are the lead's, through `scripts/heavy-run.sh` (D245). §2's merge is
read against phase 7b's references first (0.7). After wave 3, the lead runs §10's "before" series on
the rested laptop; a change it calls for is a fix package after a ruling (ruling 66), with no
section, but for D457's `restore`, which has one (wave 4). §3's fixes are read on CI's floor job
after each push of the phase branch; the commit that makes the job green removes `continue-on-error`
and moves `Minimum` (rulings 10, 71). After the gate commit, `docs/guide/**` stays docs: a change to
it is checked by the guide workflow's run, whose step "App's links" runs `AppLinksTests` and
`GuideShortcutsPageTests`, the two App.Tests classes that read the pages (D451, 11.3.4), and the
lead reads that run and records it beside CI's in `status.md`. `guide.yml`, the shots and the links
are code, so they come before the counted round; from §8's merge the notices and `LICENSE` count as
code (D433).

**The lagged list** (D262), at each wave's last merge and at the merge of §5, §6, §9 and §10, in Git
Bash, one command at a time, before the wave's whole run: phase 7b's list (its design 0.4: 7a's list
with `GitRepositoryProviderTests`, 7b's walk-through and the six Hosting.Tests classes), the App.Tests
classes 8a extends with real git, and 8a's project once it has checks:

```
export VISUALCOMMIT_WATCHER_LAG_MS=1500
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests --filter-class "*Phase2Checks"
bash scripts/heavy-run.sh --project tests/VisualCommit.App.Tests --filter-class "*StageListRefreshTests" --filter-class "*RepositoryOperationsProtectTests" --filter-class "*CompareViewTests" --filter-class "*SearchBarTests" --filter-class "*UndoStepTests" --filter-class "*RepositoryOperationsFetchTests" --filter-class "*GitCallBudgetTests" --filter-class "*GitRepositoryProviderTests" --filter-class "*DiffTextViewTests" --filter-class "*WorkingChangesTests" --filter-class "*StatusBarTests" --filter-class "*RepositoryViewModelTests" --filter-class "*RepositoryViewModelFilterTests" --filter-class "*RepositoryOperationsPullTests" --filter-class "*RepositoryOperationsBranchesTests"
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase6 --filter-method "*Phase6ChecksS.*" --filter-method "*Check_K10_*" --filter-method "*Check_K13_*" --filter-method "*Check_W1_*" --filter-method "*Check_W8_*"
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase7a
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase7b
bash scripts/heavy-run.sh --project tests/VisualCommit.Hosting.Tests --filter-class "*RepositoryHostingWiringTests" --filter-class "*SignInEntryTests" --filter-class "*WelcomeCloneFromGitHubTests" --filter-class "*PaletteCloneFromGitHubTests" --filter-class "*PullRequestOperationsTests" --filter-class "*CreatePullRequestOperationTests" --filter-class "*RepositoryHostingProvidersWiringTests" --filter-class "*SignInEntryProvidersTests" --filter-class "*CloneFromAccountTests" --filter-class "*RequestOperationsProvidersTests" --filter-class "*CreateRequestOperationProvidersTests" --filter-class "*SignInBindingsWindowTests"
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase8a
unset VISUALCOMMIT_WATCHER_LAG_MS
```

A failure under the lag is the fault R8 imitates: it gets a fix, never a looser test.

**Progress steps** (status.md), in the waves' order: 4 WP0; 5 §2; 6 §3; 7 §4; 8 wave 1's close (the
lagged list, the whole default run, §2's comparison and the new references); 9 §5; 10 §6; 11 §7 and
the owner's choice; 12 wave 2's close; 13 §8; 14 §9; 15 §10; 16 wave 3's close and the "before"
series; 17 §11; 18 §12, built; 19 the code review before the gate and its fixes, then §12's merge; 20
the live command before the pictures; 21 the gate; 22 to 27 the closing steps.

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
| K keyboard | 6 | 8: one per keyboard.md fix and the walks to Cancel and a splitter | K1 Tab into a hunk bar and Enter, with its Shift+Tab and lines menu (`k1a` to `k1d`); K2 arrows pick a blame run, with its run menu (`k2a`, `k2b`) |
| B icon | 7 | 2: the mark at its sizes on dark and light | B1 the executable and the title bar; B2 the light title bar and the taskbar button |
| A About | 8 | 6: the section dark, light and 1920×1080, the license, the notices, the palette entry | A1 the section with the version's automation name; A4 the license dialog |
| U screen readers | 9 | 3: the row's name and hint, the sweep, the names | U1 the row's events and name; U3 the named buttons and splitters |
| G guide | 11 | 3: the palette row with F1, the first-run links, About's link | none (no real window opens a browser) |

Every new screen has a real-window picture: the first-run screen (F1, F5, F6), its winget
confirm (F2), the notice (I2), About (A1), the text dialog (A4), the icon in the title bar and the
taskbar (B1, B2). F1, F2 and F5 need `winget.exe` on the gate's machine's `PATH`: without it they fail,
never "Not run" (D449; the laptop has it, 13.4 item 7).

Checks by hand, in the report: the Narrator walk-through (D411, the owner, about 20 minutes, §9's
steps); phase 6's System step (D224); a second start while the app is behind another window (§4):
the lead gives the owner a `.cmd` file outside the repository that sets CLAUDE.md's isolating
variables and starts the Release build, and the owner starts it twice, the second time while another
window is in front; the guide's pictures from the `guide-site` artifact of the gate commit, or of a
`workflow_dispatch` run on it (ruling 61: both themes, search, no outside host); after the merge,
the live site and the environment's branch rule (ruling 64). macOS's (About in the application menu,
the Command Line Tools, a second start) go to 8b's Mac checklist.

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
  laptop with the desktop (the estimate; about 8 minutes as taken, 10.7), plus a rest of 10 minutes,
  nothing beside (ruling 23); a change adds the alternated runs, 40 to 60 minutes.
- **CI** (D377's limits 70 for the Test steps, 14 for the containers; the floor job's own 25 and 35,
  ruling 74). At 7b's gate: Windows 23 min 41 s (worst 41 min 6 s), macOS 33 min 9 s (worst 46 min
  21 s), Linux 6 min 23 s, containers 5 min 28 s and 5 min 51 s; the floor job's Test step 7 min 13
  s. With +10 to 15 %: macOS's worst 51 to 53 minutes, under 56; the containers' under 7, under 11
  min 12 s; the floor's under 9, under 20. No limit is raised ahead of need (ruling 21); the lead
  reads every merge's run against four fifths of each limit. A slow day of Ubuntu's mirror can time
  out the three container jobs: they are run again (ruling 74). Windows' job builds `GuideShots` with
  the opt-in projects (about a minute). The guide's workflow is separate: about 7 to 10 minutes per
  push of code, about 2 of them its step "App's links" (D451), unverified until its first run.
- **Live command** (D335, ruling 2): about 2 min 30 s per whole run, twice at the gate.
- **Sum at the gate**: the default run about 1 h 50 min, the pass about 44 minutes, D246's two runs
  about 11 each, the live command twice; about 1,700 files inspected in about 12 batches (D247).

### 0.7 Earlier pictures

Ruling 3: nothing changes but what rulings 3, 13, 40, 54 and 62 name. Counted in phase 7b's counted
round (1,150 scripted, 200 real-window captures, 3 framed captures, 198 difference pictures):

| Change | Ruling | Pictures | Region |
|---|---|---|---|
| The contrast round (§2) | 13, D410 | 1,333 of 1,350: 1,137 scripted, 196 captures; placeholder 1,293, dark danger 360, danger button 41, accent on a row 13 (`research2/colours-*.txt`); 17 unchanged (`colours-none.txt`) | the scan's boxes (`research2/scan/part-*/scan.json`) |
| The window's icon (§7) | 3 | the 3 framed captures (phase 0 `A`, `B`; phase 6 `s15`) | the title bar; at the gate also §2's `FilterBox` placeholder: their twins' scan box `[29, 203, 99, 235]` moved to the client area's place in the framed capture ((13, 58) in phase 7b's 2226×1471 references, so `[42, 261, 112, 293]`) |
| The Git program row (§5) | 40 | 9 scripted: `s14c`, `s14i`, `s18a`, `s18d`, `s19a`, `s19d`, `s20b`, `l31e-settings-git-dark`, `l31e-settings-git-light` | the global scope's bottom |
| The About nav button (§8) | 3, 14, D409 | 83 scripted, 8 captures | x 140–317, y 255–285 at 1100×700 (x 550–727, y 415–445 at 1920×1080) |
| Two palette commands (§8, §11) | 54 | 4 scripted (`k10a`, `k10f`, `l31c` dark, light), 2 captures; K10's constants and `CommandPaletteViewTests`' count (and, from §11, a fourth PageDown in both, D452; 11.4) | the scroll thumb, x 837–838 |
| The shortcuts list's "Help" group (§11) | 62 | `s14f-shortcuts-end`; `s14e-shortcuts-top` in the section's scroll bar only: its thumb's lower end, (955, 260) to (957, 265) at 1100×700, the builder's measure, confirmed at §11's merge | the list's end; the scroll bar |

Every one but the framed captures is in the 1,333, so about 1,336 of 1,353 earlier files change. The
three framed captures also change in the left panel's `FilterBox` placeholder (§2, 1.5), outside the
scan's boxes, which cover only their client-area twins (`colours-placeholder.txt` lines 1, 2 and 935;
the scan's `expected.json` has no `with-frame` key). The lead's file gives each of the three the title
bar and its twin's box moved by the frame. Every
other package proves "none": its run's scripted pictures against the current references. Six of
8a's own pictures change at later merges, and the lead's `--expected` file for each merge names
them: `k3a-palette-cancel` in the palette's scroll thumb (§8's and §11's merges), `l3a-palette-placeholder` in the palette's scroll thumb (§8's and §11's merges) and
`l3f-settings-name-placeholder` in the settings screen (the Git program row at §5's merge, the
About button at §8's); and part F's `f12a-row-next-start`, `f12b-row-too-old` and
`f14a-netrc-note`, which show the settings screen's section list, in x 140–317, y 255–285 at §8's
merge.

How (D419): §2's comparison runs phase 7b's references
(`D:/Projects/Personal/phase7b-work/gate/round-3dc0887/visual/`, ruling 22) against wave 1's whole
run, with the scan's boxes as the expected regions (ruling 30): a difference outside them is a
finding. The run's scripted pictures, once that comparison passes, are the new references
(`D:/Projects/Personal/phase8-work/references/wave1/`); from then on every later merge and the gate
compare against the references of the last closed wave (wave 2's from its close,
`D:/Projects/Personal/phase8-work/references/wave2/`, and after wave 3's close wave 3's; the lead,
2026-11-21) with `scripts/compare-pictures.py --expected <file>`, whose file maps
`phase-N/<folder>/<name>.png`, or an `fnmatch` pattern, to boxes `[left, top, right, bottom]` in the
picture's own pixels (2.6): the lead writes one for the regions above. `--expected` may be given more
than once; a picture's boxes are those of every key that matches it, exact or a pattern, from every
file, united; a picture whose size changed goes under the fourth heading, "Changed size", and counts
as unexpected. The real-window captures are compared at the gate with phase 7b's, the scan's
`expected.json` and the lead's file for the rows above both given. The report replaces plan.md's 35
label pictures by the scan's lists (ruling 31) and states every changed expectation once under "Changes
to expected results" (ruling 28). Every picture is inspected (D247).

### 0.8 What the phase asks of the owner

13.3 lists it by when: now, to look at the rulings and D416 to D453 (the question on winget
was answered on 2026-11-20, D453); beside the build, the icon's rounds
(`docs/brand/icon/`, D447); the takeovers of the desktop (the spike, §6's merge, §12's build, the
series), told first; before the counted round, no choice of the icon: the owner chose the mark
before §7 was built (1.17, 7.8), so nothing waits there; before the gate, the three live tokens and
D246's two runs; at the gate, the Narrator walk-through, phase 6's System step and a second start
through the lead's isolating start file; at "Phase 8a accepted", before `master` is pushed, GitHub
Pages' source (ruling 64, D408), and the environment's branch rule if it admits more than `master`.

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
    public string? ChosenMissing { get; init; }          // NotFound: the setting names this path and no file is there (ruling 41), or a setting that is not a full path (on Windows, not an `.exe`), 1.14
    public string? PathCandidate { get; init; }          // with ChosenMissing: the git the machine's search finds (the PATH, then the install folders), which "Use the Git it found" names; null for none (the button hidden)
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
    // Ruling 4's order: the setting (used whatever it answers; no file, or a value that is not a full path (on
    // Windows, not an .exe): ChosenMissing; on macOS a chosen /usr/bin/git while xcode-select -p fails: NotFound
    // with CommandLineToolsMissing; no fallback), else the stand-in when StandIn is set, else PATH and then the
    // install folders.
    Task<GitDetection> DetectAsync(GitLookup lookup, IGitCallLog? callLog, IAppLog log, TimeProvider? recordClock,
        IReadOnlyDictionary<string, string?>? environment, CancellationToken cancellationToken);
    GitGuidance ReadGuidance();   // read once per session, the first time a state shows the first-run screen, which shows only once it is read (ruling 48; 5.3); nothing extra runs at a start with a working git
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
platform's with no winget and no distribution; §5 the real one with `OsRelease.Parse` and, on Windows,
`WingetPath` = `ProgramSearch.FindOnPath("winget.exe", PATH, ';', File.Exists)`):
`public sealed class MachineGitSetupProbe : IGitSetupProbe { public static MachineGitSetupProbe Instance { get; } }`.
VisualCommit.Git cannot reach App's `FileLauncher`, so §5 moves `FileLauncher.FindOnPath`'s body and
comment word for word to `src/VisualCommit.Core/ProgramSearch.cs` (new, §5's), and
`FileLauncher.FindOnPath` stays as a one-line forward to it:
`public static string? FindOnPath(string program, string? searchPath, char separator, Func<string, bool> fileExists)`.

`src/VisualCommit.App/Services/GitAccess.cs` (WP0 the surface with today's behaviour; §5 the second
and later searches):

```csharp
public sealed class GitAccess
{
    public const string GitNotWorking = "Git is not working on this computer.";   // unchanged
    public GitAccess(Task<GitDetection> detection);        // unchanged: one fixed search (GitRepositoryProviderTests); ProbeAsync answers NotFound, ReadGuidance new GitGuidance(<the running platform>, null, null), reading no file and no PATH
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
search; a call during a search joins it only when the lookup gives what that search was told (as
built since step 19, `785351d`): a call told another lookup (a git chosen meanwhile) swaps in a new
pending `Detection` at once too, and asks the probe once the running search has ended, however it
ended; a lookup that cannot be read fails its search and is never joined. `GetRunnerAsync`'s three
texts stay; the too-old text reads `GitVersion.Minimum`, never a literal (2.34.1 after §3, ruling
69); §5 adds ChosenMissing's. A tab's git calls take the detection's `Runner` through
`GetRunnerAsync` (`GitRepositoryProvider.cs:57-81`), which §6's `HeldGitRunner` relies on.

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

`GitConfigurationForgetTests` (§5) use `MergeToolHelpTests`' fake runner, `HeldToolHelpRunner`
(`MergeToolHelpTests.cs:389`, private today; §5 makes it `internal` where it is, 5.2), never the real
`git mergetool --tool-help`.

The broker's `gitVersion` (`AskPassBroker`'s two constructors, `CreateBroker`, `AppSession.cs:254`)
goes at §3's merge, by the lead (D420).

### 1.3 The launcher's start call

```csharp
// src/VisualCommit.App/Services/IFileLauncher.cs (WP0 the member with its default body; FileLauncher's body §5)
// Starts command's program off the UI thread, not waited for; FileNotFoundException when a bare name is not
// found, the start's own exception when it cannot start.
Task StartAsync(LaunchCommand command, CancellationToken cancellationToken = default) => Task.FromException(new NotSupportedException());
// src/VisualCommit.App/Services/FileLauncher.cs, LaunchCommand (WP0)
public bool InNewTerminal { get; init; }   // Windows: in a console window of its own (UseShellExecute); ignored elsewhere
// src/VisualCommit.App/Services/FileLauncher.cs, FileLauncher (§5); pure
public static LaunchCommand ResolveStart(LaunchCommand command, Func<string, string?> findOnPath, string systemFolder);
// tests/VisualCommit.Testing/Headless/FakeFileLauncher.cs (WP0 whole)
// FileLaunchKind.Start; IReadOnlyList<LaunchCommand> Starts; Exception? StartFailure { get; set; }
```

`FileLauncher` and `FakeFileLauncher` implement `StartAsync`. WP0 adds the member with its default
body and the fake's; `FileLauncher`'s is §5's, and until then the default answers for it.

How §5 starts a program (D449). `LaunchCommand.ToStartInfo()` (existing, public and pure) sets
`UseShellExecute = true` and redirects nothing when `InNewTerminal` is set on Windows; such a command
removes no variables, since a start through the shell takes no environment. `ResolveStart` is pure:
`cmd.exe` resolves to `systemFolder`'s, any other bare name through `findOnPath`, a full path stays as it
is, and a bare name that is not found throws `FileNotFoundException`. `StartAsync` is
`Process.Start(ResolveStart(command, …).ToStartInfo())` off the UI thread, not waited for, its
`findOnPath` being `FindOnPath` over the `PATH` and its `systemFolder` `Environment.SystemDirectory`.
The tests call `ResolveStart` and `ToStartInfo` and assert the result; the only call of the real
`StartAsync` in a test names, without `InNewTerminal`, a full path to a program that does not exist in an
empty `TempDirectory`: it reads no `PATH` (the rule above), `ResolveStart` keeps the path, and
`Process.Start` throws the start's own exception before anything starts. No test starts `cmd.exe`,
winget, `xcode-select`, a terminal or a browser.

The two starts (§5): on Windows with git missing or too old and winget found (ruling 44, D453), with `wingetPath` the
guidance's `WingetPath`,
`new LaunchCommand("cmd.exe", []) { CommandLine = "/S /K \"\"" + wingetPath + "\" install --id Git.Git -e --source winget\"", WorkingDirectory = Path.GetDirectoryName(wingetPath), InNewTerminal = true }`,
after a confirm that shows `winget install --id Git.Git -e --source winget`; on macOS with the tools
missing (ruling 45), `new LaunchCommand("/usr/bin/xcode-select", ["--install"])`, without the app's
confirm. Given a bare name, `cmd.exe` looks first in its current folder and then along the `PATH`, with
every `PATHEXT` extension, so the app names the file it found and starts `cmd.exe` in that file's
folder; with `/S`, `cmd.exe` strips only the outer pair of quotes, so the path stays one piece. The
confirm and the guide show git-scm.com's line `winget install --id Git.Git -e --source winget`
unchanged; the log line stays `Started in a terminal: {the command's DisplayText}`. No check runs
either start: both harnesses hand them to the fake, which asserts the command (ruling 6), the winget
start with the seam's `WingetPath`, quoted so, and that working folder, `Path.GetDirectoryName(wingetPath)`:
a test asserts the literal folder on Windows and that expression's value elsewhere. Settled (the lead,
2026-11-19): the laptop has `winget.exe` on its `PATH`, an app execution alias for which `File.Exists`
is true; the real-window checks F1, F2 and F5 need winget on the gate's machine (D449, D453; 12.3.4 launches 7 and 8).
Still to try by hand once before §5's merge, with the owner told:
`cmd.exe /S /K ""<the path found>" --version"`, whose window should stay open where Windows Terminal is
the default console (13.4 item 7).

### 1.4 The settings key for the git program

```csharp
// src/VisualCommit.Core/Settings/AppSettings.cs (WP0; written by §5)
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? GitExecutable { get; init; }   // "gitExecutable": the git chosen in Settings (ruling 4); absent unless set (ruling 40)
```

Absent, phase 6's part S still finds its nine properties. Only a git that is found and new enough is
saved; `GitSetupUsePathButton` and the settings row's `GitProgramUsePathButton` remove it.

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
`run` executing `OperationStatus.CancelCommand` only if `CanExecute` still holds and
`OperationStatus.RunningTitle` still equals the title it was listed with; otherwise it does nothing and
logs Info `Cancel {title}: that operation is no longer running.` (D452). No `AppCommand` value; not on a
dashboard (its rows' menus have it).

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
public enum HeldResolution { Taken, HandedOver, NotAnswered }        // how a held start's request ended (step 19, D464 (1))
public enum AppStartMode { Normal, NotAnswered }                     // NotAnswered: the notice, no session
public sealed record SingleInstanceOptions(TimeSpan Connect, TimeSpan Reply, TimeSpan WaitWhileClosing)
{
    public static SingleInstanceOptions Default { get; }            // 5 s, 3 s, 30 s
}
public sealed class SingleInstanceGuard : IDisposable
{
    public static SingleInstanceGuard TryTake(AppPaths paths, IAppLog log, SingleInstanceOptions? options = null);   // never throws
    public GuardOutcome Outcome { get; }              // Held turns Taken after a successful TryTakeNow or ResolveHeld
    public string? Endpoint { get; }                  // its own endpoint while Taken, else null
    public ForwardAnswer AskToComeForward();          // Held: one JSON line each way over the endpoint instance.json names
    public bool TryTakeNow();                         // one attempt; on success Taken: the server starts, instance.json is written
    public HeldResolution ResolveHeld();              // Held: the whole request, Program.Main's and Try again's (4.3.2)
    public HeldResolution WaitToTake();               // after Closing: TryTakeNow every 250 ms until WaitWhileClosing, asking whoever took the folder meanwhile (4.3.2)
    public Func<ForwardAnswer>? ComeForward { get; set; }   // Taken: run on a pool thread, waited for at most Reply; null answers Starting
    public static string? LockingOffWarning(string dataDirectory, string? variableValue, bool switchOn, bool windows);   // pure: the Warning on macOS and Linux when .NET's file locking is off, else null
    public void Dispose();                            // Taken: stops the server, deletes instance.json, then releases the lock; Held or Unavailable: touches no file
}
// src/VisualCommit.App/Views/MainWindow.axaml.cs
public ForwardAnswer BringForward();                  // restores a minimised window to its last state and activates it; Closing after Closed
```

App sets `ComeForward` on the main window's `Opened` to
`() => Dispatcher.UIThread.InvokeAsync(window.BringForward).GetTask().GetAwaiter().GetResult()` and on
its `Closed` to `() => ForwardAnswer.Closing`. The endpoint is random per run (a pipe name, or a
socket path under `Path.GetTempPath()`) and holds nothing of the data folder's path. The server is made
with `PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous`, so only the current user can connect
(D448). `Program.Main` returns `AppLaunch.Current.ExitCode`: 0 when the first instance answers
`Activated` or `Starting` (before Avalonia starts), 2 when the notice closes unanswered (ruling 67).
As built since step 19 (D464, ruling 100), `ResolveHeld` gives that 0 as `HandedOver` also when the
answer comes from a VisualCommit that took the folder while the start waited for a closing one.

The notice is `src/VisualCommit.App/Views/InstanceNoticeWindow.axaml(.cs)`, a `Window` with a
parameterless constructor (§7 gives it the icon), over `ViewModels/InstanceNoticeViewModel.cs`. Its ids:
`InstanceNotice`, `InstanceNoticeHeading`, `InstanceNoticeFolder` (the data folder on a line of its own,
trimmed at its start, D448), `InstanceNoticeBody`, `InstanceNoticeIfNoWindow`,
`InstanceNoticeStatus`, `InstanceNoticeTryAgain`, `InstanceNoticeClose`. A headless test shows it
through `InstanceNoticeHost.Show(GuidancePlatform platform, string folder, ThemeVariant theme)` (new,
§4, in 8a's project), which part I and §9's sweep call; §4 may add optional parameters after these
three for its own checks, such as the try that I2 holds and answers (4.6). The guard never runs in a harness:
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
| `RefHiddenToggle`, `RefSoloToggle` | `ShowRefFormat` "Show {0} in the graph", `StopSoloingRefFormat` "Stop soloing {0}", over the ref's full name (the string the row's tooltip shows for a branch or a tag, `RefItemRow.ToolTip`, or a property of its own that holds the same), not over `RefItemRow.Text` (the lead, 2026-11-21) |

The formats bind with `StringFormat={x:Static …}`, or through a converter in `AutomationNames` if the
compiled binding refuses that. A stage row's buttons exist in UI Automation only on a hovered,
selected or focused row (D425). §4's, §5's and §8's controls take their names from their own texts.
The status bar's `OperationStatus` gets `AutomationProperties.LiveSetting="Polite"` (ruling 36): a
`TextBlock` until step 19, since then a `Controls/StatusText`, a subclass of `TextBlock`
(`StatusBarView.axaml:76-87`; 9.3.7, D463 (1)).

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
| `VISUALCOMMIT_GIT_STAND_IN` | `AppSession.Start`, once; §5 logs a Warning: for a full path `VISUALCOMMIT_GIT_STAND_IN is set (a test switch): unless Settings names a Git, Git is looked for only at {value}.`; for `none` and for a value that is not a full path (or, on Windows, does not end in `.exe`) `VISUALCOMMIT_GIT_STAND_IN is set (a test switch): unless Settings names a Git, this computer is treated as having no Git.` | a full path (on Windows ending in `.exe`): that program is the machine's only git (`GitSource.StandIn`); `none`, a path without a file, or any other value: the machine has none. The setting still comes first | removed (null) by `MachineIndependence`; a real-window launch sets it last |
| `VISUALCOMMIT_GIT_STAND_IN_VERSION`, `VISUALCOMMIT_GIT_STAND_IN_FAILURE` | the stand-in program | its `--version` line (default `git version 2.29.0`); a text written to standard error with exit 128, whatever the arguments | removed |
| `VISUALCOMMIT_PERF_RUNS` | `Phase8aPerformance` (§10) | runs per measure, 1 to 20; unset, each fact skips (`Assert.SkipUnless`, as D270's keychain tests) | unset; the lead's series sets 5 |
| `VISUALCOMMIT_GUIDE_ROOT`, `VISUALCOMMIT_GUIDE_PICTURES` | `GuideShots` (§11) | the demo root (`/home/demo` in the workflow): absolute, outside `Path.GetTempPath()` and the repository, empty or holding the marker `.visualcommit-guide`; the pictures' folder | outside the default run |

What the lookup runs (D449): a setting (1.4) or a stand-in that is not `Path.IsPathFullyQualified`, or
on Windows does not end in `.exe`, is not run: the setting gives `ChosenMissing` with a Warning that says
why, the stand-in counts as `none`. On macOS a chosen `/usr/bin/git` is not run while `xcode-select -p`
fails (`CommandLineToolsMissing`). `GitLocatorTests` (§5) has one case per rule.

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
| `FakeGitSetupProbe` (Testing) | §5 | a queue of answers, the last repeated: Missing, ChosenMissing, TooOld, NotWorking, ConfigFault, NotStarted, CommandLineToolsMissing, `RealAt(path, source, version?)` (the machine's runner reported at a fixed path and, if given, version); `AnswerFor(path, answer)`: the answer to every lookup whose `ChosenPath` is `path` (how `ProbeAsync` reaches it, as `new GitLookup(path, null)`); a fixed `GitGuidance` for any platform; `Lookups`, every lookup received, probes included, in order |
| `GitWords` (Testing); `TempRepo.StashAsync` | §3 | git's version and words in one place; the scenarios' stashes rebuilt below 2.36 (ruling 72). Members: `Task<GitVersion> VersionAsync()`, `bool HasZdiff3(GitVersion)`, `bool FetchGuardsEveryWorktree(GitVersion)`, `bool StashNamesTheWholeBranch(GitVersion)`, `string StashBranch(string branch, GitVersion)`, `string StashText(string text, GitVersion)`, `string TrackingSetUp(string local, string remote, string remoteBranch, GitVersion)` |
| `HeldGitRunner` (Testing, `Headless/`) | §6 | an `IGitRunner` over the found git that holds the next call whose first argument equals a given word (an unstaged file's diff is `diff-files`, `GitRepository.Changes.cs:201`; a commit is `commit`) until `Release()`, with `Started` to await; given through `ShellDriver.Start(gitProbe:)` by a probe that returns `MachineGitSetupProbe`'s detection `with { Runner = held }` |
| `IconFile`, `PngHeader` (Testing, plain .NET) | §7 | `IconFile.Read(path)` gives `IReadOnlyList<IconFrame>`; `IconFrame(int Size, bool IsPng, byte[] Data)` with `Bgra()` (top-down, unpremultiplied) for a bitmap entry; `PngHeader.Read(path)` gives `(Width, Height, BitDepth, ColourType)`; the lists `IcoSizes`, `PngSizes`, `IconSetNames`, `AppImageSizes`; `IcnsTypes`, the `.icns`'s entries as `(Type, Size)` in the file's order (`icp4` 16, `icp5` 32, `ic11` 32, `ic12` 64, `ic07` 128, `ic13` 256, `ic08` 256, `ic14` 512, `ic09` 512, `ic10` 1024, D447; without `icp4` and `icp5` if 7.3 drops them), and `IconFile.ReadIcns(path)`, its entries in order as `(string Type, byte[] Data)`; `IconFile.RepositoryPath(string relative)`, public, the path under `VisualRun.RepoRoot`, through which 8a's project reads `assets/brand/generated/` |
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

### 1.17 As built: WP0, wave 1 and wave 2

What the first package, wave 1 (§2, §3, §4) and wave 2 (§5, §6, §7) built otherwise than the
sections above say, as far as a later package needs it. The lead keeps this subsection; a later
wave adds to it. What wave 2 tells §12's launches is in 12.3.5.

- **WP0** (`fc8a063`). `GitAccessTests` (App.Tests) exists, with four tests; §5 extends it (0.4). A
  listener of `GitAccess.Changed` that throws is logged at Error and cannot replace the search's
  result. `TabServices.Identity` falls back to the app assembly's identity; only `ShellDriver` pins
  `TestIdentity.Pinned`. The 8a project's start helpers are `StartEmptyAsync` and `OpenAsync`, which
  take a theme and a probe; `Phase8aExpectations.Common` holds no colours, so each part brings its
  own. `AppShortcutsTests`' keyless list holds `About` and `OpenUserGuide`; §11 takes the second out
  when it binds F1. The stand-in's variable is read from the process's environment, so a headless
  test gives a probe (`gitProbe`) and never sets the variable.
- **The broker's `gitVersion` is gone** (the lead, at §3's merge; D420): `AskPassBroker`'s two
  constructors, `PromptDialogs.CreateBroker` and `AppSession.Start` no longer take or give it.
  `AccountsSettingsViewModel` keeps its `gitVersion`, unread, for §5's netrc note.
- **The floor** (§3, `a63390c`; `GitVersion.Minimum` is 2.34.1 since `bba59c7`, when CI's floor job
  became required). `BranchCheckedOutElsewhereException(branch, worktreePath)` is in
  `IGitRepository.cs`; `RepositoryOperations.FastForwardFailureDisplay` is public. In the harness,
  `GitWords` has the members 1.15 lists, and `TempRepo.StashAsync` rebuilds a stash's branch word
  below 2.36 through the internal `RenameStashBranchAsync`. A test that depends on git's words or
  version goes through `GitWords`, never through a literal.
- **The contrast round** (§2, `3b9d159`). The tokens are 1.5's. The palettes of the earlier phases'
  expectations gained members: `AccentOnRow` in phases 3, 5 and 6, `TextPlaceholder` in phase 5;
  the real-window project links those files and compiles with them. An earlier check that reads
  HEAD's name, or a renamed file's letter, on a hovered or selected row expects the row's accent
  (the report's "Changes to expected results" lists them). `scripts/scan-contrast.py <pictures>
  <output> --jobs N` writes one `scan.json`, the eight lists, `report.txt` and `expected.json`; its
  boxes are wider than "each hit grown by 3 px" (a hit is widened along its text line by ink of its
  kind within 24 px, a dot's within 4). `scripts/compare-pictures.py --expected <file>`, given once
  or more, unites the boxes of every key that matches a picture, exact or a pattern, and writes the
  four headings of 2.6; its difference pictures draw changes inside the boxes in amber, outside in
  red, the boxes' edges in blue. The accent on a row changes in 15 earlier pictures, two more than
  the list's 13 (`06a` of phase 3, `u18j` of phase 5; both are in `colours-any.txt`).
  `Phase8aExpectations.L` holds part L's colours by token, its texts, its 31 picture names and the
  real window's two launches (`ChangesLaunch`, `GraphLaunch`) for §12. Part L alone takes about
  two minutes.
- **Single instance** (§4, `b70abdb`). `InstanceNoticeHost` is internal to the 8a project:
  `Show(GuidancePlatform platform, string folder, ThemeVariant theme, HeldNoticeTry? tryAgain =
  null)`, with `Window`, `Notice`, `Try`, `Find<T>(automationId)`, `PressKey`, `Capture`; §9's sweep
  calls it with three arguments. `InstanceNoticeViewModel(GuidancePlatform, string folder,
  Func<Task<NoticeRetry>> tryAgain)`; the try's mapping is the public static
  `InstanceNoticeViewModel.RetryOn(SingleInstanceGuard)`. `SingleInstanceGuard.ServerPipeOptions` is
  the public constant the server is made with (D448). When `instance.json` cannot be replaced (on
  Windows a second start may have it open) the holder tries again for up to a second, then logs the
  Warning `The data folder {folder} has no instance.json ({reason}): a second start on it will be
  told that this one did not answer.`, a line 4.3.5 does not list. The guard reads
  `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` and the switch `System.IO.DisableFileLocking` on macOS and
  Linux for its Warning; 1.14 does not list them, because the runtime reads them once at the
  process's start and a test cannot remove them. `InstanceNoticeWindow.axaml` has its `Icon` since
  §7's merge (`6fe69bd`). `Phase8aExpectations.I` holds the masked element's id (`FolderId`), the
  picture names and
  the real window's launch facts (exit codes, limits, the data folder's entries after "Close") for
  §12. `Program.Main`'s exit codes and the notice under a real desktop lifetime have run in no
  test: §12's launches 4 to 6 are their first run. Since step 19's first round (merged at
  `67ff55d`; D464, ruling 100) one method, `SingleInstanceGuard.ResolveHeld`, answers a held start
  for `Program.Main` and for `RetryOn` with a `HeldResolution`, and `WaitToTake` returns one too
  (1.11); the waits, the modes on macOS and Linux, the stale socket's probe and the new log lines
  are 4.3's, the new tests 4.5's. The exit codes and the notice's texts are as they were.
- **Wave 1's close** (the report's "Wave 1's comparison"). The references of every later merge are
  `D:/Projects/Personal/phase8-work/references/wave1/` until wave 2's close; from then they are those
  of the last closed wave (wave 2's, `D:/Projects/Personal/phase8-work/references/wave2/`, and after
  wave 3's close wave 3's; the lead, 2026-11-21). "Try again" keeps its width while it reads
  `Trying…` (`9a82a8a`), and L6's `l6h` waits for the history's diff (`24d335f`). For §12: in L4 a's
  launch the menu opens under a pointer that has not moved, and Windows may then hover an item
  that the scripted picture does not show hovered, so the pointer is moved to the neutral point
  before Shift+F10; `l2a`, `l2b`, `l5a` and `l5b` show the unstaged list scrolled to bring
  `src/Calculator.cs` into view, and the real window scrolls it the same way.
- **The icon** (the owner, 2026-11-20). The owner chose Lens and said yes to round 2's masters
  (`docs/brand/icon/round-2/`), with the outlined eye at the small sizes. §7 builds with them and
  writes no placeholder (7.8); part B's checks and every picture that shows the icon show the
  owner's mark from the start. The look-alike check is made by eye, with no image search.
- **First run: the lookup** (§5, merged at `53fc353`). The search runs on the thread pool. Public
  additions: `GitLocator.IsRunnablePath` (the machine's rule), `FirstLine`, `MacOSSystemGit`,
  `ErrorTextLimit`; `GitAccess.RunningPlatform`, `GitAccess.GitNotFound`. On macOS
  `GitSearchContext.FromSystem()` asks `/usr/bin/xcode-select -p` (5 s) at most once per search,
  on meeting `/usr/bin/git`, the harness's searches too (the lead: no start of a program). A
  stand-in the locator does not run logs Info `Git was not looked for on this computer:
  VISUALCOMMIT_GIT_STAND_IN names no git program ({value}).` at each search no setting answers.
- **First run: the screen** (`e34fc68`, `1daecda`). `CommitGraphView`'s row 0 holds the
  `ContentControl` `GitSetupSlot`, whose content is `RepoTabViewModel.GitSetupForView`: the
  screen once it has first shown (`GitSetupViewModel.HasShown`), so a session that finds git makes
  no `GitSetupView`. The buttons sit in the `WrapPanel` `GitSetupButtons`, the links in
  `GitSetupLinks`, in 5.3's table order, as `GitSetupTexts.For` gives them; Copy sits beside the
  command. Each button of `GitSetupButtons` carries `KeyRegion.IsMain` (Copy and the links do
  not), so the graph area's target is the first shown. The links are `VcFlatButton`s of class
  `gitSetupLink`, coloured by the view's own styles: `VcAccentBrush`, `VcAccentOnRowBrush` on hover.
- **First run: Check again and the starts.** Choose, `Use the Git…` and the settings screen's
  close (a setting changed while git does not work) wait for a running Check again to end, then
  run their own (`GitSetupViewModel.CheckAgainAfterRunningAsync`): the command runs one search at
  a time, and the search it then starts reads the setting just changed. `GitAccess` joins a
  running search only for a call told the same lookup, and searches after it for a call told
  another (1.2, as built since step 19). `CheckHelper()` runs only while git works and the helper
  was missing. The view model, not `FileLauncher.StartAsync`, logs `Started in a terminal: …` and
  `Started: …`, with the `DisplayText` of the command before `ResolveStart` (`cmd.exe`, not its
  full path). The screen's launches and Copy (`WingetCommand`, `CommandLineToolsCommand`,
  `DownloadCommand`, `ShowFileCommand`, `MoreLinkCommand`, `GuideLinkCommand` and `CopyCommand`)
  allow concurrent runs, so their buttons stay enabled, and keep the keyboard focus, while they
  run, winget's under its confirm (all but winget's since step 19, `2a2a1fc`). Since step 19
  Choose's probe of a file stops after `GitSetupViewModel.ChosenProbeLimit` (10 s; the file is then
  refused with `GitSetupTexts.ChooseNoAnswer`) and with the window, and Choose and the row's Browse
  get the keyboard focus back after a cancelled pick or a refused file (5.3).
- **First run: the row, the netrc note, the ssh line.** After the row's `Use the Git it found` (the
  chosen git gone, another found), `GitProgramPath` reads `None found` and `GitProgramNote` only
  `VisualCommit looks for Git again when you save.` (the lead's ruling). After the row's `Use the
  Git on the PATH` drops a chosen git that is in use and works, `GitProgramNote` reads `Chosen
  here. VisualCommit looks for Git again at its next start.` (the source's note, then
  `GitSetupTexts.NextStartLooksAgainSuffix`; ruling 100's words). The netrc question is one
  of `SettingsDialogViewModel.Loaded`'s reads, not asked again that session once the note is
  dismissed. `MainWindowViewModel.HomeHasNetrc(variable, directoryExists, fileExists, windows)` is
  public static, for the tests. The ssh line is `AskPassBroker.AskPassRequireLineFor(OperatingSystem.IsWindows())`:
  the constant `AskPassRequireLineWindows` on Windows, `AskPassRequireLineUnix` elsewhere (5.3,
  ruling 100).
- **First run: names (§9).** Each button's and link's automation name is its text, bound where the
  text changes, so `GitSetupCheckAgainButton`'s reads `Checking…` during a search. `GitStatus`'s
  help text is `GitStatusToolTip`, none without a path; `GitSetupButton` sits left of `GitStatus`.
  The panels, the label `Git said:`, the row's `Grid` `GitProgramRow` and `AccountsNetrcNoteRow`
  have no automation id. `GitProgramPath`'s tooltip is its path, with no help text. Since step 19
  `GitSetupTitle` and `GitSetupError` are polite live regions, and Check again's own text is no
  live region (D463 (2)); the error line shows with its text in one step, so a reader hears a
  refusal only if it reached the line during Choose's `Checking…`, and the title only once it has
  read the screen (ruling 100). The buttons that get the keyboard focus back after an outcome are
  described by it through `AutomationProperties.HelpText` (D463 (3), ruling 101; 5.3, 9.3.7):
  Choose by `ErrorText`, the error line, Check again by `Title`, the title, and the row's
  `GitProgramBrowseButton` by `GitProgramViewModel.StatusText`, its status, which is empty after a
  pass (ruling 101: by the row's path and note when it has no status, step 19's third round);
  `GitProgramStatus` is no live region.
- **First run: the harness.** A headless test reaches a state by `StartEmptyAsync(data, gitProbe:,
  askPassHelper:)` with a `FakeGitSetupProbe`, then `WaitForGitSetupAsync`, as `GitSetupShellTests`
  does for twelve states (`ShellDriver.Phase8a.FirstRun.cs` also has `GitSetupPart`,
  `GitSetupText`, `GitSetupButtonIds`, `GitSetupLinkIds`). The probe adds `Windows(wingetPath)`,
  `MacOS`, `Linux(distribution)`, `Ubuntu`, `Alpine` (`PRETTY_NAME` `Alpine Linux v3.20`) and
  `HoldNext()`, whose `ProbeHold` (`Started`, `Release()`) holds the next lookup once recorded.
  `FakeGitAnswer.ChosenMissing` throws for a lookup without a `ChosenPath`, so a start that reaches
  it by Check again (9.3.8's state 10) writes `gitExecutable` first, as F3 does. `RealAt`'s log
  lines name the machine's git. The stand-in's namespace is `VisualCommit.GitStandInProgram`.
- **First run, for §12.** `Phase8aExpectations.F` (BCL, Avalonia and Core only) holds launches 7
  to 10's facts: `RealWindowPictures`, `MissingLaunchScenario`, `MissingLaunchStandIn`,
  `TooOldVersionLine`, `NativePickerTitle`, `NativePickerWithin` (30 s), `FoundAgainWithin` (60 s),
  `ConfirmClosesWithin` (5 s), `MostF6Presses` (4), `FocusOrderWithWinget`, the two failure
  messages, the log lines and F6's texts. Since step 19 it also holds `ChooseNoAnswer` and
  `NextStartLooksAgainSuffix`, which `Phase8aExpectationTests.F` holds to the app's.
- **The keyboard pass** (§6, merged in `e463392`). `FocusReturn.AfterRun` (`Views/FocusReturn.cs`)
  takes a last, optional `Func<IInputElement?, bool>? stillLost`, and `FocusReturn.LostFrom(TopLevel?
  topLevel, InputElement pressed, Func<bool> stillThere)` is that test for a pressed element (`2185f06`;
  6.3.5 as corrected). `AppCommands.CancelNoLongerRunning(string title)` (public, static) gives D452's
  Info line. `HunkHeaderLayer.BarRemoving` (internal) is raised by `Rebuild` too, which every
  `DiffPane.Redraw` calls, so a framed hunk button also hands the focus to the text, without a frame,
  when the bars are made again for a theme switch or a large diff's late colouring.
- **What 6.9 left open** (§6). Tab from the diff's text (`Continue`) enters its bars' buttons in hunk
  order and then leaves the diff, and Enter runs a framed button inside the text area; `:focus-within`
  with `:not()` works in Avalonia 12.1.3; `GridSplitter`'s arrows move 10 per press and raise no
  `DragCompleted`, so `MainWindow` posts `PanelDragged` at `Loaded` after an arrow the splitter handled,
  on `LeftSplitter` and `RightSplitter` (`94842f7`, D427); `DetailsBody` and `ActivityLogPartText` take
  no keyboard focus, so Tab never stops in them and `ActivityLogView.axaml` is unchanged; a tab's git
  calls take the detection's runner. The blame picks the caret's run after a caret key with any
  modifier (`2185f06`; 6.3.2 as corrected). Since step 19 (rulings 100 and 101; merged at `a60056e`
  and `950ca7d`) a pick by the pointer, a click in the gutter or a right-click (one on the text
  inside a selection aside), puts the caret on the picked run's first line in view, a line drawn
  in part at the view's top counting, at the view's left edge when the text is scrolled across, so
  that the next arrow moves it one line from a line the user sees and the text scrolls only to show
  a line out of view or one that ends left of the edge (6.3.2).
- **The stage rows** (§6). A row that holds the keyboard focus shows its `rowActions` with its buttons
  at `Opacity` 0 (D425), in the visual tree and in UI Automation: a hovered row and a focused other row
  show two rows' buttons there, and §9's names (1.12) and its sweep meet a focused row's buttons too.
  Tab and Shift+Tab walk both lists in their rows' order, rows below the view included, by their
  items panel, `RowOrderStackPanel`, which the left panel's list and the commit details' and a
  comparison's file lists share since step 19's second round (D466, ruling 101; merged at
  `39c632f`), where wave 2 had `WorkingChangesView`'s own tunnel `KeyDown` (`55b8d3a`); 6.3.4 has
  its rules. After a row's action by the keys the focus moves only if the run took it away (6.3.5 as
  corrected). After a switch of tabs, since step 19 (D462 (1)), the window gives a focus that the
  switch took away (to nothing, the window or an element no longer shown or enabled) to the same
  panel's target in the tab now shown, as F6 would give it without its memory of the other tab's
  element, when that tab has a repository and shows that panel, else to the graph area's target,
  with the frame if the element that lost it had it; nothing while a dialog is open or a tab still
  opens, and a focus placed meanwhile stays (`MainWindow.Keys.cs`, `FocusAfterSwitch`;
  `KeyboardPassTests.TabSwitch.cs`).
- **Part K's harness** (§6). `HeldGitRunner` has `Probe`, `HoldNext(string word)`, `Started` (a
  `Task<GitCommand>`) and `Release()`; a row's Stage is `add --all -- <path>` (`K.AddWord`).
  `ShellDriver.Phase8a.Keyboard.cs` holds `FocusVisible`, `StageRows(list)`, `StageRow(list, path)`,
  the static `StageRowOf(visual)` and `RowButton(row, id)`, `FocusedRowPath()`, `TabToAsync(expected,
  what, shift = false)` (one press, then a wait whose failure names the focus) and `DescribeFocus()`.
  For §12, `Phase8aExpectations.K` holds the 50 ids (`K.Ids`), K1's walk (`CalculatorWalk`, its 18
  (row, id) stops), `DownsToLines` 4 and `ShiftDownsForLines` 2 on the hunk that remains, the
  `--numstat` lines, K2's `BlameDowns` (run, line, subject; none for run 1) and the launches
  `ChangesLaunch` and `HistoryLaunch`. K8's walks took 8 Tabs from the right panel to Cancel, then 1 to
  `LeftSplitter`: the splitters come after the panels in `MainWindow.axaml`, before the status bar.
  Since step 19's second round Tab visits every row of a list before it leaves it (6.3.4), so a
  walk's count can grow; the gate records K8's (ruling 101).
  Method 3 writes the copy's `origin` URL with forward slashes before `HoldIn` (the report's K3 note).
  Part K alone takes about two minutes. Phase 2's `17c-restored` flips between two files from run to
  run, on the branch's base too.
- **For the guide** (§11; 6.3.10 as built). The blame's run is picked by the arrows, PageUp, PageDown,
  Home and End, with any modifier; Tab from a stage row's own button reaches its Stage and Discard
  buttons (icons, tooltips `Stage` and `Discard`; `Unstage` on a staged row), then the next row's own
  button; "Cancel {title}" is in the palette, keyless, only while the running operation can be
  cancelled; a focused splitter moves 10 per arrow, and a side panel keeps its width as after a drag.
- **The icon's masters** (§7, `6fe69bd`). Round 2's three masters, copied byte for byte into
  `assets/brand/`, each with `width` and `height` equal to its viewBox; `icon-macos.svg`'s plate is
  832 wide, 96 in from each edge, radius 187 (7.3: about 824 and 100). `IconFilesTests` item 1 also
  holds the first comment to its chosen form, so a placeholder's comment fails it, and refuses a
  `width` or `height` other than the viewBox's; a further test holds `icon-small.svg`'s straight
  edges to whole units and refuses a `transform`, a `stroke` other than `none` and a `style` in it,
  which it does not follow (tests of that check run on drawings of their own).
- **The icon's tool** (`tools/VisualCommit.IconBuilder`). It owns `assets/brand/generated/` and
  `src/VisualCommit.App/Assets/Icon/` (not `assets/brand/` beside the masters): each run deletes
  every other file and every empty folder in them, so no later package puts a file of its own
  there; `IconFilesTests` items 4 to 6 also fail on any other entry in `generated/png/`,
  `generated/VisualCommit.iconset/` and `Assets/Icon/`. It rewrites every file on each run, the same
  bytes when no master changed. `Placement` (in `Program.cs`) copies each file aside before
  replacing or deleting it and puts all back on a failure; 7.3 lists the texts. `--root` is taken
  as given, without looking for `VisualCommit.slnx`. Its printout at the build: 100 % of the opaque
  pixels at 3:1 on `#202020` at every `.ico` size (the blue about 3.01:1, just over the floor), 71
  to 82 % on `#F3F3F3`.
- **`IconFile`** (Testing) is 1.15's, with more. `IconSetNames` is `(Name, Size)` pairs;
  `IcnsFiles` names the `.iconset` file whose bytes each `IcnsTypes` entry holds, in order;
  `Masters`, `SmallMasterUpTo` (32) and `IcoPngSize` (256); the folders and files as constants
  (`IcoFile`, `PngFolder`, `IconSetFolder`, `IcnsFile`, `ManifestFile` and others) with
  `PngFile(n)`, `AppImageFile(n)` and `IconSetFile(name)`; `IsPng`; `DecodePng(byte[])`, which
  gives `(Width, Height, Bgra)` of an 8-bit RGBA or RGB PNG, not interlaced, with a reader of its
  own. `IconFrame.Bgra()` decodes the PNG entry, the 256, too. `PngHeader.Read` takes a path or bytes.
- **The app's icon.** `MainWindow` and `InstanceNoticeWindow` carry
  `Icon="/Assets/Icon/VisualCommit.ico"`, loaded as each window is made (its cost unmeasured, §10);
  the `.ico` is 162,197 bytes. `AppIcon` is 1.6's. About's `VisualCommit-96.png` is `icon.svg` at
  96: the tile `#4C5BE0` inside a transparent margin of one sixteenth (6 px, 3 at 48×48), the line,
  the eye's outline and the pupil white. The app shows no new text, and no control gains a name.
- **Part B.** `IconSheet` (internal to the 8a project) has no ids: the checks read its `Heading`,
  `Row1`, `Row1Sizes`, `Row2` and `Row2Files`. A size is the bare number at 11 in SemiBold, centred
  under its image on a whole pixel (at the regular weight `TextInk` could not read `20` and `32` on
  white); the heading is 13 in `VcTextPrimaryBrush`; the rows' bottoms lie on y 312 and 488. The
  checks make a plain `Window` and start no app (B1's picture 1.1 s after its start, B2's 0.15 s).
  `Phase8aExpectations.B` holds launch 3's facts for §12: `RealWindowPictures` (six names in the
  launch's order), `WindowTitle`, `TitleBarCropWidth` 64, `Enlargement` 4,
  `TaskbarButtonNameStart`, `TaskbarButtonLimit` 10 s, `TaskbarButtonNotRun` (run.txt's line) and
  `IconTolerance` 2.
- **Settled by the merge's CI run** (44851353430). `IconFilesTests` item 8 ran in the macOS job
  and passed: Apple's `iconutil -c icns -o <icns> <iconset>` takes the `.iconset`, and `-c iconset
  -o <folder> <icns>` reads the tool's `.icns` back with `icp4` and `icp5` in it, so D447's ten
  entries stay (7.8 item 3). The build warns in no job, so `ApplicationIcon` is quiet on macOS
  and Linux (7.9 item 4), and the tool builds in all six jobs. Part I's checks were not run at §7's build (the notice's headless frame has
  no title bar). 7.6's comparison changed only phase 0's `06c-restarted-first-frame`, by the
  first-frame race phase-0.md's check 6 records.
- **At wave 2's merge** (the lead, 2026-11-20). Three faults that belonged to no package were fixed
  on the merged branch, each with a test that failed first and each read by a second agent. Enter on
  the selected row of the repository browser's list chooses it (`25dd5c8`): Avalonia's `ListBox`
  takes Enter on an item for itself, so `DialogView`'s own Enter never saw it; now
  `CloneFromGitHubDialogView`, the one view of all three providers, takes plain Enter on
  `CloneFromGitHubList` on the way down and runs `ConfirmButton`'s command while a row is selected
  and the command can run, and with no row selected the key stays the item's. So K5's step 6 and
  6.3.9's route "T, arrows, Enter" hold as written. The activity log's splitter moved by Up or Down
  is fitted and kept as a drag's end is (`9b63f26`, D427): `CommitGraphView` posts
  `ActivityLogDragged` at `Loaded` after an arrow the splitter handled, and the test
  `Up_on_the_activity_logs_splitter_reached_by_Tab_heightens_the_log_by_10_and_keeps_it` is no
  longer skipped. F6b starts with the private `MachineSearchProbe` of `Phase8aChecks.F.Helpers.cs`
  (`fc123ec`): the machine's search for git with the fixed guidance `new
  GitGuidance(GitAccess.RunningPlatform, null, null)`, so that check reads neither the `PATH` for
  `winget.exe` nor `/etc/os-release`. `VisualCommit.slnx` holds the stand-in and, in a folder
  `/tools/`, the icon's tool (`578d07c`). A second round (D454, `24b9d16`): the browser's Enter
  stands once in `DialogView`, as `ConfirmsOnEnter(ListBox)`, which the pick dialog's `PickList`
  uses too, and `ConfirmsOnEnterOnChecked(ItemsControl)` does the same for the checked option of
  `ChoiceOptions` and `PushRemotes`; so 6.3.9's "dialogs" are no longer unchanged, and §11's
  page of keys may say that Enter in such a list confirms. The icon's tool names the folder whose
  reading failed (`ff71793`). `KeyboardPassTests` (`1355a20`) also states where Ctrl+Tab leaves
  the focus: from a stage row's own button or its Stage on nothing, the first F6 then starting at
  the left panel; from the graph or the summary on the same element. The lead's ruling: step 19's
  round fixes the first in `WorkingChangesView` (the graph area's target takes the focus), and no
  picture changes. D462 (1) replaced that ruling in step 19 with the window's rule for every panel
  ("The stage rows" above): from a stage row, or its Stage reached by Tab, to a tab whose stage
  panel shows, the focus now goes to that tab's first stage row, with the frame
  (`Ctrl_Tab_from_a_focused_stage_row_gives_the_focus_to_the_other_tabs_first_row_with_the_frame`
  and `Ctrl_Tab_from_a_stage_rows_Stage_reached_by_Tab_gives_the_focus_to_the_other_tabs_first_row_with_the_frame`,
  which replace the two that stated "on nothing"), and no picture changes; from the graph or the
  summary it stays where it was. A row's Discard cancelled in its question gives the focus back to
  the row's own button, with the frame (6.3.4), which a test now holds.
- **The performance pass** (§10, merged at `86ecb3a`, 2026-11-21). `StartupTimeline` has a second
  public overload, `FromProcess(IAppLog, Func<DateTimeOffset>)`, for the test of its Warning, and
  its texts are public constants (`WindowOpenedFormat`, `GitFoundFormat`, `FirstRowsFormat`,
  `OneRestoredTab`, `RestoredTabsFormat`, `StartTimeUnreadable`). The series' clones are made with
  `git clone --local --no-hardlinks`, with `gc.auto=0` and `maintenance.auto=false`: a hard link
  would let `TempDirectory.Dispose` clear the read-only attribute of the shared 100k pack.
  `Phase8aPerformance` runs its facts in the order M2, M3, M4_M5, M6, M7 (`Phase8aPerformanceOrder`;
  the fixture is `Phase8aPerformanceRun`). Beyond 10.3.5's lines its file holds one trigger line per
  measure, a `<measure> failed: …` line before a failure is rethrown, two summary lines and two
  bounds for M4 (after the load, the peak after the reload), and `no line` for a noted value that
  is missing. M6 puts the pointer on Fetch before the settle (`RealApp.SetPointerOn`, a third
  helper beside `Memory()` and `LogTime(…)`, §12's to use or to leave), so that T0 to the reload
  line holds no pointer travel; the span it notes is the Fetch operation's, which the queue counts
  to the end of its refresh, the reload included (10.3.4's "fetch span" and 10.9 read so). The Pull
  budget is 23 git calls, for a fast-forward and for a merge; each run of the theory appends its
  count, its budget and git's version to `artifacts/visual/pull-budget.txt`, which CI uploads with
  each job's pictures. `PerformanceSeriesTests` has five methods (19 cases). 10.6's comparison ran
  against wave 2's references. Not run by the builder, and the lead's on the desktop:
  `Phase8aPerformance` itself (10.8).
- **About and the notices** (§8, merged at `f014b2b`; its fixes after the merge at `85e2884`).
  `AboutSettingsViewModel` takes four optional parameters after 8.3.1's five (`readText` and
  `vendorNotices`, through which a test makes a text unreadable, the settings screen's `lifetime`,
  which a Save that closes the screen cancels too since step 19, and `saving`, since step 19
  (ruling 100, `ce144f4`), the screen's Save as it runs, which a Show press waits for), and its four
  commands allow concurrent runs, so that their buttons keep the keyboard focus (8.3.1);
  `AboutSettingsViewModel.PartId(file)` gives a vendor file's switch id and
  `EmbeddedTexts.FileNameOf` is new. The text dialog's card is 760 × 600 at (170, 50) at 1100×700;
  with one text and no switch the text lies 12 below the title (with the switch: 12, then 8). The
  notices file and `notices/` are as 8.2 and 8.3.4 now say: the 12 Avalonia packages that ship,
  entries without a `Packages:` line, the copyright lines with their holders' addresses, the
  vendors' files byte for byte. `ThirdPartyNoticesTests` has eight tests (8.4).
- **The text view** (§8, `DocumentTextView`). Its keys are 8.3.2's as ruled. It takes them in a
  tunnel `KeyDown` and a tunnel `TextInput` handler, before AvaloniaEdit's bindings see them, and
  removes the Enter bindings from its own text area's input handlers: AvaloniaEdit 12.0.0 matches
  its keys over each input handler's own `KeyBindings` list (the handler's `KeyDown` on the text
  area), not through Avalonia's `InputElement.KeyBindings`, and each text area holds its own copy, so
  every other editor keeps its keys; a later AvaloniaEdit that changes this would bring the jumps
  to the hidden caret back, which `DocumentTextViewTests` would show. The view runs the platform's
  copy and select-all itself, as `ConflictResultEditor` runs undo and redo: AvaloniaEdit picks those
  keys by the operating system (Cmd on macOS) while the headless platform's command key is Ctrl
  everywhere, and the copy test's fixed Ctrl+C failed CI's macOS job in run 44969394972. The text
  takes the focus at opening without the keyboard's frame, as the diff's text does. A5's bound is
  asserted at the method's end (8.5).
- **Screen readers** (§9, merged at `769f459`; D455, D456). The peers are 9.3.2 to 9.3.5's: the
  spike asked for no change (D455), and nothing uses `[PrivateApi]`. `AutomationNames` holds 1.12's
  17 names and 5 formats, set as local values in ten views; the compiled binding takes `StringFormat`
  with `x:Static` (9.9 item 5). `ShellDriver.Phase8a.Uia.cs` has `WindowPeer()` (made once),
  `GraphControl()`, `GraphPeer()`, `GraphRowPeer()` and `RecordNameChanges(peer)`, which returns a
  `NameChangeRecorder` (`Changes`, `Count`, `Last`). The sweep walks no control that `IsVisible`
  hides, on it or above it, and walks one drawn at opacity 0; its rules, its states and its
  exception are 9.3.8's as built, with D456. Part U's expectation tests are the class
  `Phase8aExpectationTestsU`, which a filter `*Phase8aExpectationTests` does not match. U1's first
  Name change after the peer is made has the old value null (`U.FirstOldName`), each later one the
  name raised before. For §12, `Phase8aExpectations.U` holds launch 13's facts (`GraphRowLaunch`,
  `MergeMoves`, `EventWait` 5 s, `LaunchNameIds`, `LaunchRowNames`); launch 13 reads no branch row,
  so the branch row's help text is read in no real window. Since step 19's first round (merged at
  `a60056e`; D463 (1)) the status bar's `OperationStatus` is a `Controls/StatusText`, a subclass of
  `TextBlock` styled as one, named by its text with git's stage and percentage, whose peer raises
  the Name change only when its `SpokenText` changes; `IStatusBarOperation` gained `SpokenText`
  (`OperationStatusViewModel.SpokenText`, `BulkRun.SpokenText`,
  `DashboardBulkViewModel.SpokenText`), so the live region speaks an operation's start, ssh's notice
  and its result, and on a dashboard a run's start, ssh's notice while it shows, Cancel all's text
  as it was at the click and the result, not the progress
  (`StatusBarTests.The_operation_texts_live_region_speaks_the_start_and_the_result_not_the_progress`).
  A new implementer of `IStatusBarOperation` gives its `SpokenText`. The first-run screen's
  `GitSetupTitle` and `GitSetupError` and the notice's `InstanceNoticeStatus` are polite live
  regions (D463 (2); "First run: names" above, 4.3.4); the tests take each peer as a client reaches
  it, walking down from the window's peer (`ClientWalk`, 8a's project;
  `InstanceNoticeLiveRegionTests`). Since step 19's second round the buttons that get the focus
  back after those outcomes are described by them (D463 (3); 9.3.7), and `ClientWalk.RecordFocus`
  (with `FocusRecorder` and `FocusEvent`) records each focus event as Windows' bridge takes it, the
  focused peer's id, name and help text read at that moment (`OutcomeAtFocusTests`).
- **At wave 3's merges** (the lead, 2026-11-21). The notices quote the copyright lines word for
  word, their holders' e-mail addresses included (8.3.4 as written): the builder had read its
  brief's "no e-mail address" as reaching third parties' notices, where it meant the owner's address
  and About's own texts, which hold no "@" (8.3.5). The vendors' notice files are byte for byte the
  vendors' (8.2). Their sources and SHA-256, which 8.8 asks for, are recorded here, since no merge's
  message holds them: `notices/Avalonia-NOTICE.md` is `NOTICE.md` at commit
  `8eeda4f6f546165b3f72e63c9f42247abb306905` of AvaloniaUI/Avalonia, the commit that Avalonia
  12.1.3's nuspec names (16,954 bytes,
  `dd6f80852d16320c865a2e403ffd1a8b07a0e34c5b37122c0bc2343639ff68e6`; the lead fetched it again on
  2026-11-21 and it hashed the same); `notices/CommunityToolkit.Mvvm-ThirdPartyNotices.txt` is
  `ThirdPartyNotices.txt` of CommunityToolkit.Mvvm 8.4.2's package (8,600 bytes,
  `7774f8b0ab66bfb47ec154ce24a2d6d574febc44a74d940b8a037d1c56756163`);
  `notices/Onigwrap-THIRD-PARTY-NOTICES.txt` is `THIRD-PARTY-NOTICES.TXT` of Onigwrap 1.0.11's
  package (4,875 bytes, `5662cc07e23c65fd5d3790d9b8a4d7113f1de881de150a0e93296a2faa1418a9`);
  `notices/SkiaSharp-THIRD-PARTY-NOTICES.txt` is `THIRD-PARTY-NOTICES.txt` of
  SkiaSharp.NativeAssets.Win32 3.119.4's package, the same bytes as in
  HarfBuzzSharp.NativeAssets.Win32 8.3.1.3's (139,775 bytes,
  `21504c46c4c58aa64c1055bd2dcbc5f9a136b4b8c412ed3cc6740e22c5b127f5`). `sha256sum notices/*` prints
  these four values in a checkout on any machine. D433's lines are in CLAUDE.md, plan.md's gate step
  6 and the README. Enter in the text dialog's text is the dialog's, and the text takes no key that
  would use its hidden caret (8.3.2). D456 rules on what §9's sweep left (9.3.6, 9.3.8), with two
  corrections from the build: state 3 sweeps the About section, the license's text dialog (one text,
  no switch) and the notices' dialog on Avalonia's file, reached by its switch; and no
  `RefItemRow.ToolTip` is null. Part U's method 2, step 6: Esc in the resolver's result leaves the
  resolver open, then `ResolverCloseButton` (`96d94bf`; 9.5), and launch 13 closes the resolver by a
  real click on that button and reads the graph's first child before it records the row's events
  (12.3.4). Narrator may now also speak a branch row's help text (9.3.9's step 10); it is written
  down as heard at the gate. A5's bound stays (8.5, 13.4 item 12). The cold case stays as D455 has
  it (9.9 item 1). The fetch hold (`FetchHold`, merged at `eba3717`): on Linux and macOS a cancelled
  fetch's hold script lives on (D36), and it wrote a gave-up marker when the hold was armed again
  between its two looks at `release` (part K's K3_K8 in one container job of CI's run 44969394972);
  the script now decides inside its loop, and a new `FetchHoldTests` test holds the script in its
  `rm` while the hold is armed again. Not this wave's, for the review before the gate (status step
  19): the name a branch row gives for a moment while its folder opens (the spike's S4,
  "Avalonia.Controls.DockPanel"); a check whose `using var hold` throws in `Dispose` hides the first
  exception. The second is answered in step 19's first round (merged at `876c197`): `FetchHold`'s
  `Dispose` releases and never fails, writing a hold that gave up to the test's output instead, and
  a check that holds a fetch ends its body with a look of its own after its last held step,
  `Release` or `Assert.Empty(hold.GaveUp())` (the latter added then to `BulkRunTests` and phase 6's
  W5 and W8), so it never passes on a hold that held nothing.
- **At wave 3's close** (the lead, 2026-11-22). The one fault that the inspection of wave 3's new
  pictures found (the report's "Wave 3's comparison with wave 2's references"),
  `a1b-about-link-focus`, where Fluent's focus frame lay on a focused link's bounds, its text's line
  box, over the first and last letters and the underline, is fixed in `VcLinkButton`
  (`Theme/Controls.axaml`; `8ef7e86`, `77c1936` and `5177cec`, merged at `fcf8c1e`). Its
  `FocusAdorner` is null, and its template is a `Panel` with the content and, laid over it, the
  `Border` `FocusFrame` (2 px, margin -4) holding `FocusFrameInner` (1 px), which take Fluent's
  `SystemControlFocusVisualPrimaryBrush` and `SystemControlFocusVisualSecondaryBrush` under
  `:focus-visible` alone. The frame reaches 4 px outside the bounds on every side, so that its inner
  line keeps at least a device pixel of background outside the line box at every display scaling
  from 1 to 2: the ink reaches the line box's left and right, and at 1.5 and 1.75 the descenders
  and the underline reach its last row. At wave 3's close the margin was -4,-3, 3 px above and
  below, and at 1.5 the inner line lay on the text's first and last rows and at 1.75 touched them;
  step 19's second round made it -4 (ruling 101; `c12749c`, merged at `db7fbce`). Its negative
  margin gives back its borders, so nothing moves; it is never hit (`IsHitTestVisible` false), so a
  pointer on it reaches what lies under it; without the keyboard's focus it draws nothing, and a
  link focused by a click is underlined without it. The rule for whoever uses the theme: the button
  does not clip (`ClipToBounds` false), and neither may what holds it; 4 px round a link stay free
  of other controls (GitLab's two token links lie 4 apart, so the frame of one reaches the other's
  line box and stops there: with the legacy link focused, its frame touches the token link's
  descenders at 1.5 and keeps one device row from them at the other scalings, accepted by ruling
  101). `LinkFrame` (`tests/VisualCommit.Testing`) reads a focused link against the same window
  unfocused: the text's pixels unchanged but for the underline's rows, the underline shown, a whole
  frame in the theme's frame colour on four sides outside the text, and background between frame
  and ink; since step 19's second round also in pictures drawn at a scaling above 1 (`Problems`'
  `scale`, with the text's bounds in the pictures' device pixels; the underline may take
  `UnderlineRowsAt(scale)` rows, 2 at 1 and 3 from 1.25 to 2). A1's step 9 asserts it
  (`AssertFramedLink`), and `LinkFocusFrameTests` (App.Tests) holds it for About's links, in dark
  and in light, and for GitLab's two token links, with nothing moved, nothing drawn outside the
  bounds without the focus, no other control under the frame, the frame never hit and no frame
  after a click; since step 19's second round also for GitHub's and Azure DevOps' token link, the
  app's last `VcLinkButton`, with nothing moved, no other control under the frame and the frame
  never hit, and for all six links in dark at scalings 1 to 2 in steps of 0.25 (three theories, 30
  cases): 42 cases where it had 12, and `LinkFrameTests` 10. `VcRadioButton` (`Theme/Dialogs.axaml`)
  built its focus frame as two borders round the ring and the content and was not changed then; that
  the frame took clicks just outside the button while it had the keyboard's focus went to the list
  of the review before the gate (status step 19). It did: its strokes, up to 7 px outside the
  button, took the pointer, and a click in the gap above Hard in the Reset dialog checked Hard. Step
  19 rebuilt it as the link's (`763a1c5`, merged at `ce144f4`): the `Border` `FocusFrame` holding
  `FocusFrameInner` is laid over the ring and the content with `IsHitTestVisible` false, its lines
  on the pixels they had, so it is only drawn, never hit, and moves nothing; `RadioFocusFrameTests`
  (App.Tests) hold it for the Reset dialog's three options and the settings screen's Theme and Pull
  options. Since step 19's second round (ruling 101; `9a04ebb`, merged at `db7fbce`) the Theme
  options, at the top of General, draw it whole: while a settings section is at its top, its
  scroller and the scroller's presenter clip to their bounds grown 4 px upward
  (`SettingsDialogView.ClipSection`, `SectionTopReach`), and a section scrolled is clipped as before
  (`SettingsDialogViewTests.A_scrolled_section_shows_none_of_its_rows_above_its_top`), so a radio
  button that Tab brings to the edge of a scrolled section has its frame cut there (seen at the
  minimum window in the repository scope with "Other" shown; accepted, ruling 101); Theme's note,
  4 px below the options, has its top 2 px inside the frame's bottom, and at a scaling of 1 two
  faint pixels of its ink lie on Dark's frame (accepted, ruling 101). `LayoutAudit` counts a
  parent's clip geometry as a clip. The references of every later merge are wave 3's,
  `D:/Projects/Personal/phase8-work/references/wave3/` (1,235 scripted pictures;
  `a1b-about-link-focus` there is the fix's of wave 3's close: step 19's second round changes it in
  the frame's rows alone, as it changes part G's `g3a-about-guide-link`, which has no reference
  there). The "before" series (10.3.6, as built) was taken three times, none of them quiet
  throughout (D457 (1)), the second because of the first's build and the third because of the
  second's updater: the first (00:47, `86ef5b0`) and the third (05:53, `1afb980`) ran M2 within two
  minutes of a Release build that the command itself made, the third because a docs commit after the
  build changed the commit the build is stamped with, and the second (04:59, `fcf8c1e`) met Office's
  updater, an idle-triggered task of Windows' Task Scheduler, as M3 began. From now on a series is
  taken as D457 (2) and CLAUDE.md's row for it say: the real-window project built in Release on the
  very commit at least ten minutes before, nothing committed in between, the Task Scheduler read for
  what is due in the next ten minutes and afterwards for what ran. The bounds are the ones D457
  gives, in a commit of their own: M2 6,100 ms, from the second series alone; M4 370 MB after the
  load and 440 MB at the peak; M5 4,600 ms; M6 8,800 ms; M7 630 ms; M3's stays unset until its
  change's "after" series. M4's trigger did not fire. M5's and M6's fired in every series and go to
  plan.md's Backlog: no stall of the window was seen, and every change that would cut the reload
  amends D47 or D52. M7's fired in every series and goes to the Backlog, by two judges to one; a
  rested median of about 600 ms, or the owner, reopens it. M3's fired wherever a quiet M3 is set
  against the quiet M2 (D457 (4)) and gets a change in wave 4: ruling 76 (13.1), the restore order,
  by which the session's active restored tab opens first and alone and the others after its first
  page, or after 5 s at the latest, at most four at once, with `InitializeAsync`'s contract kept.
  Wave 4 builds and proves that package, `restore`, subject to the week's share the owner is asked
  for before wave 4 (D241); it gets a design section of its own when wave 4 starts, written from
  D457 (5). §11's page `troubleshooting/large-repositories.md` takes from the report's table
  ("The performance series") what 10.3.6's step 5 names, as indications on the laptop (ruling 19):
  100,000 commits, ten restored tabs of that size and the memory measured; its "first rows in about
  2 s (Q1)" is one restored tab's.
- **After wave 3's close: CI's two failures and the row list** (the lead, 2026-11-22 and 23; the
  report's "CI's two failures after the link frame's merge, and the row list" has the account and
  the numbers). CI's run 45019059991 on the link frame's merge failed one test on macOS,
  `LinkFocusFrameTests`' case for `SignInLegacyTokenLink`, and one on Windows,
  `AccessibleNameSweepTests.State_05_the_ops_tab`; run 45035307735 on the two fixes' merges and run
  45133434382 on the row list's merge are green in all six jobs. **The underline's rule in
  `LinkFrame`** was wrong, not the app. Avalonia 12.1.3 draws an underline in pieces that stop a
  line's thickness short of each letter that crosses it, between fractional positions, so the column
  where a piece ends holds whatever part of a pixel the piece covers; macOS draws the `y` of
  "legacy" over columns 473 to 475 of the underline's rows where Windows and Linux cover 472 to 476,
  which left column 472, where the piece before the `y` ends, clear of the letters. The rule now
  asks, in every column clear of the letters, for at least half a pixel of the link's colour over
  the background, and lets one fainter column pass where a piece ends: at the text's first and last
  columns and, alone, directly beside a letter that crosses the line (`LinkFrameTests`, App.Tests, 7
  cases on made pictures; `8f50a34`, merged at `70a8fc0`). Its bounds, both on the safe side: it
  cannot excuse a piece that Avalonia leaves out, one shorter than a twelfth of an em between two
  crossing letters close together, which none of the app's link texts has, nor two clear columns in
  a gap wider than a letter's ink; either fails with a message that names the columns. On macOS, CI
  is its only proof. **The row list.** Windows' failure came from a flaw of the app that the test's
  wait let show: a hide's reload of the graph, which runs outside the operations queue, marks the
  hidden branch's two rows in the left panel, `RowCollection` then removed and inserted every row
  between them, and a row pressed before the swap and released after it lost its click; the test did
  not wait for the reload. Forced, the timing gives CI's message exactly; nothing more proves that
  this happened on CI's runner. `48f403c` (merged at `809b77f`) made the list tell the view only of
  the rows that show something new. A review of that change, and then headless tests of the real
  app, showed that it replaced such rows and that Avalonia's virtualizing panel takes a replace of
  the top row in view as a slot of no height, which left a scrolled list rows short or sent it to
  its top. The fix (merged at `bad98b1`) is the rule that stands, in `RowCollection`'s comment with
  its measurements and in architecture.md's "Working changes and writes". It holds for a rebuild
  whose changed part, between the rows that stay at the list's start and end, keeps its number of
  rows: (1) a list whose rows have one height, the stage lists, calls `ReplaceAll(rows, showSame)`,
  which removes each row it tells of and inserts its rebuilt row at its index; (2) a row that shows
  the same stays, and a row that can show its rebuilt row by changing its own properties does so
  (`IUpdatableRow`: the left panel's ref rows and section headers, and since step 19 a file list's
  rows, `ChangedFileRow`, for a folder's opening), the view told nothing of the list; (3) a list of
  mixed heights, the left panel, calls `ReplaceAll(rows, showSame, sameItem)` with what names an
  item, its rows' keys: the rows told of go by runs of consecutive rows, a run of same-item rows
  replaced row by row and any other run removed and inserted, and runs are never joined; (4) the
  list is reset above 64 rows told of. A rebuild that changes the number of rows removes that whole
  part and inserts its new rows, as before phase 8a, with a reset above 64 rows removed and inserted
  together, and a press on a row inside it is lost. As built since step 19 (D462 (3)) that holds
  only for a rebuild that both removes and inserts rows: a row that can change itself counts as
  kept at the list's start and end too, and a rebuild that only inserts rows or only removes them
  (a folder or section opened or closed, a branch created or deleted, its header showing its new
  count itself) tells the view of them in one Add or one Remove, however many, and never resets.
  Two limits are known. A row that keeps its key but cannot change itself (a pull request row that
  shows something new or whose request changed, a message row whose text changes, a ref row whose
  name, tooltip or level changes; a remote, folder or section header that opens or closes has
  changed itself since step 19) is replaced: when it is the top row in view, the offset and the
  extent end short by the heights of the consecutive replaced rows from it down, and a list scrolled
  by less goes to its top and makes every control again, the click under a press lost with them;
  and a press on a replaced row itself loses its click wherever the row is, as before phase 8a. And
  a row left in place between the first and the last row that shows something new stays in the
  panel's estimate, so deep in a long list the offset and the extent can end further off than
  before phase 8a. In the 105 pairs of cases on record no row moved in the window and no click was
  lost that the code before kept, except in the first limit's own case (seven pull request rows
  replaced at the top under an offset of 172 px); deep in the list, kept rows in view may get new
  controls, as they did before.
  The lead chose that over joining two runs, which a round of the fix had tried: where a join
  gained, it brought the offset 1 to 47 px nearer (47 for two renames at the bottom of 60 branches,
  6 for a fetch), in a list where it is off already; where both runs lay below the top row in view
  it gained nothing, and where it reset the whole list (two renames far apart at the bottom of 100
  branches) it ended 7 px further off; and it took the controls, and the click under a press, of the
  rows between the runs. For whoever adds a list of rows or a row: use the form that fits the rows'
  heights; a row's `ShowsSameAs` compares everything the row holds that its actions use, not only
  what it shows, because a row that shows the same is kept as the old object (`PullRequestRow`
  compares its whole record since `e40c9ae`); a row that implements `IUpdatableRow` raises a change
  for every property a template binds, the derived ones too, and its `CanUpdateTo` says yes only
  when all that `UpdateTo` does not copy is the same; and a walk-through that hides or solos a
  branch waits for the graph's reload (`WaitForGraphReloadAsync`) before it goes on, as the sweep's
  state 05 and U2/U3's step 9 now do, because no wait for the operations queue covers it.
  `RowCollectionTests`, `LeftPanelScrollTests`, `LeftPanelViewModelTests` and `StageListScrollTests`
  (App.Tests) hold the rule: each of the closing round's 20 wrong forms of `RowCollection`, tried
  against the first three, fails one of them, two of the 20 only since the lead's two facts
  (`a9a7847`). What is older than the phase, and what the fix's rounds left open, went to the list
  of the review before the gate (status step 19): Avalonia's estimate after a remove at or above the
  first row it has made, or an insert above it, which near the top of the left panel misses by
  pixels and deep in it can give rows in view new controls, lose a press on one or draw them
  elsewhere (87 px lower after a fetch that drops the top remote branch in view, by up to 131 px at
  the undo of a rename); a rebuild that changes the list's length (one that only inserts or only
  removes rows is one change since step 19, above); the row that itself shows something new, which
  still loses its press, its focus and its tooltip; the keyboard focus that Enter or Space on a
  section header, a folder or a file list's tree folder, or Space on a hidden row's eye, drops
  (answered in step 19's first round, merged at `c0bedc6`, D462 (3): the toggled folder or section
  keeps its row and its control, and with them the focus, and an eye or solo button that hides
  itself gives the focus to its branch's row); the other phases' walk-throughs, not yet searched
  for a hide or a solo followed only by `WaitForIdleAsync`; the cost of a solo with thousands of
  branches now that each row changes itself in place, not measured; and one more timeout of an
  older test, seen once (the report has it).

- **Wave 4: the restore order, as built, proven and merged** (the lead, 2026-11-23; section 14 with
  its 14.10; rulings 86 to 93; D458; merge `aedb12f`). The proof is taken again before the gate
  (D461, 2026-11-24; 14.10's amendment). What a later package or session must know. A start whose
  active tab is a restored repository's beside other restored tabs opens them through `RestoreOrder`
  (`ViewModels/RestoreOrder.cs`): the active tab first and alone, the others at its graph's first
  page (`RepositoryViewModel.FirstPage`), at its opening's end without one, at its close or after 5
  s on `TabServices.RestoreClock`, then at most `OpensAtOnce` (4) before their own first pages; a
  waiting tab that is shown opens at once, one that is closed never opens. Every other start is
  unchanged, and `InitializeAsync` and the harness's `WaitUntilReadyAsync` keep their contracts, so
  no wait changed. `AppSession.Start` and `ShellDriver`'s `Start` and `StartWithTabs` take the
  optional `restoreClock`, last in each list: a test that starts the app with several restored
  repository tabs and must not meet the 5 s gives a `ManualTimeProvider` that it never moves, and
  then only the first tab's page, end or close releases the others. The order writes `Restore order:
  …` lines, at Info at its start and its release. In the real window phase 1's check 15 opens its
  100,000-commit tab after `linear`'s first page. M3's bound is 8,900 ms and M2's 9,500 ms
  (`Phase8aPerformance`, `23bea54`), and CLAUDE.md has the command that runs M2 and M3 alone for two
  builds by turns. "Check again" still reopens the tabs whose restore failed all at once (ruling
  90).

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
  repeat the scan and the masked comparison (ruling 30); the scan's eight lists in
  `docs/test-reports/phase-8a-contrast/`.
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
| `docs/test-reports/phase-8a-contrast/` (new) | the eight lists the scan writes on phase 7b's references (2.6), `colours-any.txt`, `-none`, `-placeholder`, `-placeholder-double-backdrop`, `-danger`, `-danger-on-row`, `-danger-button` and `-accent-on-row`, one `phase-N/<folder>/<name>.png` per line, committed in §2's commits: they are the names ruling 3 asks for, and the research folder is outside the repository (13.4 item 1). The scan's other output (`scan.json`, `report.txt`, `expected.json`) stays outside it; the script makes it again |
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
| L6 (R: a to c) | graph: HEAD's `main` a) idle (`MoveToNeutral`), b) hovered (`MoveMouse`), c) selected, pointer away; d) "Add settings page"'s "R" idle, hovered, selected; e) `main` compared with `origin/main`, its "R" selected (the builder confirms the rename is listed); f) `src/main-app.txt`'s file history, the rename's row selected; g) changes: staged `src/helpers.py`'s "R" hovered, selected; h) a to c in light | `main` `#7C8CFF` on `#1A1D24`, `#8B9AFF` on `#2F3542`, `#8B9AFF` on `#2A3150`; each "R" `#8B9AFF` hovered or selected, `#7C8CFF` idle; light `#4353D8` always. One picture per state, `l6a` to `l6m` (a to c are `l6a` to `l6c`, as the real window names them; the report's part L names each) |

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
`compare-pictures.py --expected <file>` may be given more than once. A picture's boxes are those of
every key that matches it, exact or an `fnmatch` pattern, from every file, united; a box is in the
picture's own pixels, never scaled by the script. The summary then has four headings, always in this
order: "Changed outside the expected regions" (per picture, the changed pixels outside its boxes),
"Changed, not expected", "Expected, not changed" and "Changed size": a picture whose width or height
differs from its reference's (for which `compare` today returns only the two sizes) goes there and
counts as unexpected, whatever its boxes. So the lead can write a file for 0.7's later regions and
give it beside the scan's `expected.json` at the gate.

At wave 1, after its lagged list and whole default run (0.4), the lead:
1. scans `D:/Projects/Personal/phase7b-work/gate/round-3dc0887/visual/` (ruling 22); `diff` with
   `research2/colours-*.txt` prints nothing;
2. compares those references with `artifacts/visual`, `--expected` given: the scripted 1,137 of
   `colours-any.txt` changed, the 13 of `colours-none.txt` the same file; each entry under the four
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
- The eight lists are committed in `docs/test-reports/phase-8a-contrast/`, each equal line for line
  to the scan's and to `research2/colours-<list>.txt`, and nothing else of the scan's output is.
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

Open points: 13.4, items 1 to 3; item 1 is settled, and 2.2 and 2.8 carry it.

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
  nowhere costs one `worktree list`, then the fetch; `ParseWorktrees` of a block whose path holds a line
  break (`worktree /srv/a`, then `b`, `HEAD …`, `branch refs/heads/side` and the empty line) gives
  `Path.GetFullPath("/srv/a")` with `refs/heads/side`, so P1 still sees the branch and names the path up
  to the break. A path that holds an empty line loses its branch: accepted (3.9, risk 7).
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

None. Nothing on screen changes on 2.35 or later but the second common-dir call's text where that call
is made (3.6); P1's dialog and the old-git note are in no check; the "too old" state's checks are §5's
(part F).

### 3.6 Earlier pictures

None on the laptop (ruling 3). The proof: the scripted pictures of the builder's runs of
`Phase3ChecksA`, `Phase3ChecksC`, `Phase5ChecksF`, `Phase5ChecksU` and `Phase6ChecksW`, compared by
`scripts/compare-pictures.py` with the current references (0.7), show no difference, and a grep shows
no check with the activity log open on a fast-forward of another branch or on a folder opened below its
top. The grep's premise holds on the laptop only: `TempDirectory` keeps `Path.GetTempPath()` as it is,
while git prints the top level as its real path, so on macOS (`/var` is `/private/var`) and on a TEMP
with a linked or short (8.3) name (CI's Windows job's, probably) the opened folder is never the top level
as written, and every open whose first answer names the common folder relatively (a main worktree's
`.git`) makes the second common-dir call (`ResolveCommonDirectoryAsync`, 3.3.4).
There CI's pictures that show the reads, phase 5's A2 (`a2d-show-reads`), show
`git rev-parse --path-format=absolute --git-common-dir` where they showed `git rev-parse --git-common-dir`.
The lead reads the macOS job's after round 1 (3.8), and the report notes the change under "Other
platforms". The floor job's pictures (`visual-ubuntu-22.04-git-floor`) show 2.34's words and are
compared with nothing.

**As read (the lead, 2026-11-24; the finding floor-2 of the review before the gate, ruling 99).**
The picture cannot show it. In the macOS job's pictures of CI's run 45306019513
(`visual-macos-latest`, `phase-5/scripted/a2d-show-reads.png`; the status bar reads Git 2.56.1) the
activity log stands at its end, as it does on the laptop: it shows the refused checkout's own last
calls, at that operation's depth (`git rev-parse HEAD --symbolic-full-name …`, `git for-each-ref`),
and then, under "Show reads", the reads that follow it (`git status`, `git diff-tree`, `git
for-each-ref`, `git config --null --get-regexp ^remote\.` with exit 1, `git stash list`, `git
symbolic-ref -q HEAD`, `git rev-parse -q --verify HEAD`, `git status`; the laptop's picture has the
same rows without the first `git status`). The opening's reads lie far above what the panel shows,
so this picture shows neither the opening's first call nor its second, on any platform, and no other
scripted picture is taken with "Show reads" ticked (phase 5's `a2a` and `a2b` show the log's top,
without the reads). What holds the second call instead is a test:
`ActivityLogViewTests.A_repository_opened_through_a_link_shows_its_openings_reads_under_Show_reads`
opens one repository through a link and one in a folder below its top and asserts the log's first
two rows, the opening's call and then `git rev-parse --path-format=absolute --git-common-dir`; it
runs on every platform and passed in that run's macOS job. The sentence above, 3.3.4's "phase 5's A2
picture shows it first" and 3.8's item on the picture are wrong about what the picture shows: A2
holds the opening's call by an assertion on the log's rows (`Phase5Checks.A.Log.cs`,
`AssertShowReads`), not by its picture. They stay as written, with this note.

### 3.7 Lagged list, run time, real-window pass

The lagged list gains nothing of §3's own: 0.4's App.Tests command names
`RepositoryOperationsBranchesTests`, which 3.4 changes (P1's read touches no file the watcher sees;
`SearchBarTests` already covers the search). The default run: about 13 tests more, 8 fewer, seconds.
The floor job's Test step stays about 7 minutes. The real-window pass: nothing (2.56; its linked
constants keep 2.36's words).

### 3.8 Done when

- The green commit's run: the floor job succeeds, 0 failed, without `continue-on-error`; every other job
  green (risk 46's ratio run again); recorded in `docs/status.md`'s CI section.
- `GitVersion.Minimum` is 2.34.1. A grep of `src/` finds no `BindingsVersion`, `GitThatUsesTheAccount`,
  `UsesTheAccount`, `AccountUseForGitOldGit`, `_noDiffMergesOption`, `new(2, 31, 0)` or
  "before Git 2.31".
- The green commit's diff of `ci.yml` is its two removals; the limits read 35 and 25.
- No 2.36-pinned gate (3.1) changes.
- 3.6's comparison on the laptop shows no difference.
- After round 1 (3.3.5), the lead reads phase 5's `a2d-show-reads` in that run's macOS artifact
  (`visual-macos-latest`): its "Show reads" shows the second call as
  `git rev-parse --path-format=absolute --git-common-dir`, as 3.6 expects; the report notes it under
  "Other platforms".
  **Read on 2026-11-24** in run 45306019513's macOS pictures: the picture shows the log's end and
  cannot show the opening's calls; a test holds the second call (3.6's note).
- At the merge the lead removes the broker's `gitVersion` (0.4) and changes: D420 in decisions.md;
  `requirements.md:21`, `README.md:40` and `:55`, `CLAUDE.md:530-531` (Git 2.34.1 or newer, the app's
  minimum and the tests'), and CLAUDE.md's sentence on the floor job in "What to know" (from this
  commit its failure fails the run, as the other jobs' does); architecture.md :614 (P1), :740 (the search), the opening's common folder,
  :2438-2440, :2573-2576, :2618, :3941, :3986-3987 (D277); plan.md line 758 (2.34.1), risk 26 (2.34.1
  and 3.3.3's five differences), risk 31 (2.30 to 2.33 fall away), two new risks: the mirror refspec
  (3.9, risk 4), and "on 2.34 P1 does not see a branch being rebased or bisected in another worktree,
  nor a checkout between its read and the fetch; git 2.35 and later refuse both" (3.9, risk 3); the
  report's "Changes to expected results" (3.4's checks that take 2.34's words, `PartW.ReadForms`).
- Afterwards `AccountsSettingsViewModel.cs`, `AskPassBroker.cs` and `MainWindowViewModelTests.cs` pass
  to §5, `Phase7aExpectations.A.cs` to §8 (0.4).

### 3.9 Risks, what is unverified, open points

Risks:
1. Hidden assertions: each test stopped at its first failure; 3.4 fixes what a grep finds (check 23's
   undo labels, check 14's left panel and graph), and the next round may find more.
2. The floor job used 28 min 35 s of its 35 on a slow mirror; such a day can time it out.
3. P1 reads only the worktrees' branch lines, before git writes. On 2.34 it never sees a branch being
   rebased or bisected in another worktree (that worktree's HEAD is detached, so no branch line is
   listed), nor a checkout between its read and the fetch; 2.35 and later refuse both. Reading
   `rebase-merge/head-name`, `rebase-apply/head-name` and `BISECT_START` in each such worktree, as
   `worktree.c` v2.35.0:424-429 does, is not taken: git 2.35 and later refuse both cases themselves,
   and on 2.34 the rebase's end cannot update the moved branch (`sequencer.c` v2.34.1 updates it only
   from `orig-head`'s value, else `could not update refs/heads/<branch>`): that worktree is left on a
   detached HEAD with the rebased commits, the branch at the fast-forwarded commit, and nothing is lost
   (the fast-forwarded commits are still on the remote). plan.md records it as a risk (3.8).
4. A mirror refspec (`+refs/*:refs/*`) can move a branch checked out in another worktree on 2.34, as
   in a terminal: a plan risk, not fixed (ruling 75).
5. Ubuntu's 2.34.1 carries backports (`safe.directory`, `protocol.file.allow`): "tested on 2.34.1"
   means Ubuntu's; nothing in the app depends on them.
6. Git's refusal of this worktree's own branch still reads `… in another worktree.` (today's text on
   2.35 and later); no menu offers that fast-forward.
7. `ParseWorktrees` ends a block at every empty line, and without `-z` git prints a worktree's path as
   it is, on 2.34 and 2.56 alike (release notes 2.36.0 lines 110-112): a path that holds an empty line
   (never on Windows) loses its branch line, so P1 misses that branch on 2.34, where 2.35 and later
   refuse it. Accepted; a path with a single line break keeps its branch (3.4).

Unverified, with the step that verifies it: the rebuilt stashes' ids on 2.34 (`Phase5ScenarioTests`,
the new pin; round 1 of the floor job); 2.34's zdiff3 refusal on standard error, and U18's `fsck`
route (round 1); the second common-dir call in no laptop picture (3.6), and its new text in the macOS
job's A2 picture (round 1, 3.8).

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
other has ended; D448 (amends D443), the notice's folder on a line of its own, trimmed at its start,
and a server that admits the current user only.

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
`GuidancePlatform` (1.2), the 8a support (1.15), `DurableFile.Replace`, `PlatformSystemTheme.IsDarkIn`,
`Theme/Dialogs.axaml`'s classes and `Controls/TrimmedTextBlock.cs`.

### 4.3 Behaviour

#### 4.3.1 `Program.Main`

In order: a comment that an updater's start-up hook goes first (the code names no phase, 0.3);
`var paths = AppPaths.Resolve()`; the crash log on `paths.LogDirectory` and its handlers, as today;
`using var guard = SingleInstanceGuard.TryTake(paths, crashLog)`. `Taken` or `Unavailable`: mode
`Normal`. `Held`: `ResolveHeld()` (4.3.2; as built since step 19, D464 (1)), the one answer to a
held start, which "Try again" shares (4.3.4): `HandedOver` (an answer `Activated` or `Starting`)
returns 0 at once (Avalonia never starts); `Taken` (the lock taken after a `Closing`, or within the
250 ms after an unanswered ask) gives `Normal`; `NotAnswered` gives `NotAnswered`. Then WP0's line,
extended: `AppLaunch.Current = new() { Timeline = …, Guard = guard, Mode = mode }`; the lifetime as
today; return `AppLaunch.Current.ExitCode`. The `using` disposes the guard after the lifetime and on
the rethrow. The waits come before any UI thread exists (Q2), each with a limit (D464): at most
about 8.5 s, the lock's 250 ms, `Connect` and `Reply`, then the lock's 250 ms after an unanswered
ask; after `Closing`, `WaitWhileClosing` (30 s) on one clock however often the folder changes
hands, plus at most one ask in flight (a 100 ms connect and `Reply`) and the lock's 250 ms.

#### 4.3.2 The guard

`TryTake` never throws. It creates the data folder and opens `instance.lock` with `OpenOrCreate`,
`ReadWrite`, `FileShare.None`, keeping the stream. On macOS and Linux (as built since step 19, D464
(4)) the data folder, when the guard creates it, is the user's alone (0700,
`Directory.CreateDirectory(path, mode)`, which gives the mode to the last folder alone: a missing
folder above it, such as `~/.config`, gets the system's default mode, as `mkdir -p` gives it, and
the data folder's mode keeps other users out of all below it), and so is a lock file it creates
(0600, `FileStreamOptions.UnixCreateMode`): another user who could open the lock file could hold its
lock; a folder or a lock file that exists keeps its mode. `instance.json` keeps the mode
`DurableFile.Replace` gives every file it writes.
- Opened: `Taken`. It starts the server, then writes `instance.json` through `DurableFile.Replace`
  with `Utf8JsonWriter`: `{"v":1,"pid":…,"endpoint":"…","started":"<ISO 8601 with offset>"}`. If the
  old file named a socket path `<Path.GetTempPath()>VisualCommit-<16 hex>` or `/tmp/VisualCommit-<16 hex>`
  exactly and a file is there, it deletes it first, but since step 19 (D464 (5)) only when no
  server answers on it (one attempt to connect, 100 ms): a free lock does not prove its owner gone,
  since a data folder copied while its instance runs names that instance's live socket, which a
  start on the copy leaves to it.
- `IOException` while `instance.lock` exists: the open is tried again every 50 ms for 250 ms (a virus
  scanner or the indexer may hold the file for a moment), then `Held`, without reading an HResult
  (Unix's for a held `flock` is unchecked). `TryTakeNow` makes no such retries.
- Anything else (`UnauthorizedAccessException`, no file, a path under a file): `Unavailable`; the start
  goes on as today.

`AskToComeForward` (on `Held` only, else `InvalidOperationException`): until `Connect` has passed it
makes an attempt every 100 ms, and re-reads `instance.json` before each; a missing or half-written
file and an endpoint that refuses the connection are alike: again after 100 ms (so a start that read
a crashed run's endpoint reaches the new holder once that one has rewritten the file). Each attempt
connects with `PipeOptions.CurrentUserOnly`; on Windows it first calls `AllowSetForegroundWindow(pid)`
(user32 by `[DllImport]`, as `WindowsJob.cs`; failure ignored) and connects with a 100 ms timeout.
Connected, it sends one line and waits at most `Reply` for one:
client `{"v":1,"request":"come-forward","pid":<its pid>,"args":[]}`; server `{"v":1,"answer":"activated"}`,
`"starting"` or `"closing"`. Anything else is `NotAnswered`.

`TryTakeNow()` is one attempt; success makes `Outcome` `Taken` and runs `Taken`'s steps.
`WaitToTake()` calls it every 250 ms until `WaitWhileClosing` has passed. `Default` is 5 s, 3 s, 30 s.

As built since step 19 (D464 (1) to (3), ruling 100). `ResolveHeld()` (on `Held` only, as
`AskToComeForward`) is a held start's whole request, which `Program.Main` and "Try again" make
alike: `AskToComeForward()`; `Activated` or `Starting` give `HandedOver`; `Closing` goes to
`WaitToTake()`; `NotAnswered` has the lock tried again every 50 ms for 250 ms, since a holder that
ended during the ask (closed, crashed, or ended by the user as the notice advises) has let it go:
`Taken` if it comes free, else `NotAnswered`. `WaitToTake()` returns a `HeldResolution` and keeps
one clock: it tries the lock every 250 ms, `Taken` once it comes free, `NotAnswered` once
`WaitWhileClosing` has passed; at each look it reads `instance.json`, and while that names an
endpoint other than the one an ask last reached (another start took the folder the closing one let
go), it makes one attempt to reach that holder (a 100 ms connect, then the request and `Reply`),
the lock tried again between two looks: `Activated` or `Starting` give `HandedOver`, `NotAnswered`
has the lock tried for 250 ms as above, `Closing` is waited for within what is left of the same
`WaitWhileClosing`, and a refused connection is tried again at the next look. So a new holder that
ends before it answers leaves the folder to the waiting start at once. Accepted (ruling 100): a new
holder that keeps the lock while its endpoint refuses holds a waiting start until the wait ends,
and the log names that case; a start whose ask is in flight as a closing holder's server is
cancelled gets no answer, and if another start takes the lock within its 250 ms it shows the
notice instead of asking that start, which "Try again" then hands over to.

The endpoint: on Windows a pipe name `VisualCommit-<16 hex>` (`RandomNumberGenerator.GetHexString(16,
lowercase: true)`); on macOS and Linux the socket path `<Path.GetTempPath()>VisualCommit-<16 hex>`, or
`/tmp/VisualCommit-<16 hex>` when that is over 100 UTF-8 bytes (macOS allows 104). Nothing of the data
folder's path. `Endpoint`: the guard's own while `Taken`, else null.

`Dispose` (twice is harmless): on a guard that holds the lock (`Taken`, also after a successful
`TryTakeNow` or `WaitToTake`), it stops the server, deletes `instance.json` (best effort), then closes
the lock's stream. The deletion comes before the release, so the file it deletes is its own. A `Held`
or `Unavailable` guard's `Dispose` touches no file: `instance.json` belongs to the instance that holds
the lock. The lock file stays (deleted, two processes could lock two files of one name).

On macOS and Linux, `TryTake` logs `LockingOffWarning` when
`DOTNET_SYSTEM_IO_DISABLEFILELOCKING` is `1` or `true` in any case, or `System.IO.DisableFileLocking`
is on: .NET then takes no lock (perfsingle.md b fact 4). On Windows it does nothing.

#### 4.3.3 The server and `ComeForward`

A background task, one client at a time; each connection is read for at most `Reply` and 4 KB. The
server is a `NamedPipeServerStream` on the endpoint, made with `SingleInstanceGuard.ServerPipeOptions`,
a public constant (the app has no `InternalsVisibleTo`; test 6 reads it):
`PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous` (D448; Windows: the pipe admits the current
user alone; macOS and Linux: the peer's uid is checked). A connection it refuses is dropped
unanswered, and the next is awaited. A malformed line, another `v` or another `request` gets no
answer. `ComeForward` null answers `starting`; otherwise the guard runs it on a pool thread and waits
at most `Reply`, sending its answer, or nothing after a throw or a timeout. A server that cannot open
its endpoint leaves the guard `Taken`, with a Warning; a second start then gets the notice.

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
`InstanceNotice`. Top to bottom: the heading (`dialogTitle`, id `InstanceNoticeHeading`), the folder
(`TrimmedTextBlock`, class `dialogRepository`, `TextTrimming="LeadingCharacterEllipsis"`, id
`InstanceNoticeFolder`: one line, never wrapped; its text, `ToolTip.Tip` and automation name are the
data folder's full path, so the notice's height never depends on the path's length, D448), the body
(`dialogText`, `InstanceNoticeBody`), the platform's sentence (`dialogText`, `InstanceNoticeIfNoWindow`),
the status (`dialogWarning`, `InstanceNoticeStatus`, only after an unanswered try), and
(`dialogButtons`) "Close" (`VcActionButton`, `InstanceNoticeClose`), then "Try again"
(`VcPrimaryButton`, `IsDefault`, `InstanceNoticeTryAgain`), which takes the focus at open as a
dialog's button does. Tab goes round the two; Enter is "Try again"; Esc, the title bar's close and
Alt+F4 are "Close". Each button's automation name is its text. The window's
`AutomationProperties.Name` is the heading and its `AutomationProperties.HelpText` the body followed
by the platform's sentence (one space between), so that a screen reader speaks them when the notice
opens with the focus on a button; no picture changes.

Texts (`InstanceNoticeTexts`, ruling 58):
- Title `VisualCommit`; heading `VisualCommit is already running`
- Body: `Another VisualCommit uses the same data folder but did not answer. Switch to its window, or close it and start VisualCommit again.`
- Windows: `If no VisualCommit window is open, end VisualCommit in Task Manager, then choose Try again.`
- macOS: `If no VisualCommit window is open, quit VisualCommit in Activity Monitor, then choose Try again.`
- Linux: `If no VisualCommit window is open, end VisualCommit in your system monitor, then choose Try again.`
- Buttons `Try again` (while it runs `Trying…`, disabled) and `Close`; status `It still did not answer.`

It says "did not answer", never that the other hangs: an elevated first instance on Windows refuses
a normal one's connection (`CurrentUserOnly` checks elevation) while it works.

"Try again" runs on a pool thread (`InstanceNoticeViewModel.RetryOn`): `TryTakeNow()` gives `Free`;
else, as built since step 19 (D464 (1)), `ResolveHeld()` (4.3.2), as `Program.Main` makes it:
`Taken` gives `Free`, `HandedOver` gives `HandedOver`, `NotAnswered` gives `NoAnswer`. On the UI
thread: `HandedOver` sets `ExitCode` 0, closes the notice and calls `desktop.Shutdown()`. `Free`
(D422) runs `StartSession`, shows the main window as `desktop.MainWindow`, puts `ShutdownMode` back
to `OnLastWindowClose`, sets `ExitCode` 0 and closes the notice. `NoAnswer` shows the status and
enables "Try again", which keeps the focus, given back if its disabling took it. "Close" during a
try closes at once and drops its result. Every other close leaves `ExitCode` 2 and calls
`desktop.Shutdown()`.

The status (`InstanceNoticeViewModel.StatusLine`, empty until a try goes unanswered) is a polite
live region since step 19 (D463 (2)); it shows with its text in one step, before a screen reader
can have reached it, so a reader most likely finds the words only when it reads the notice
(ruling 100). "Try again", which has the focus again after an unanswered try, is described by the
status: its `AutomationProperties.HelpText` is `StatusLine`, empty before, so that the focus given
back is announced with `It still did not answer.` (D463 (3); 9.3.7).
`OutcomeAtFocusTests.Try_again_is_described_by_the_status_when_the_focus_comes_back_after_an_unanswered_try`
holds that focus event.

#### 4.3.5 Log lines

Through the crash log; `{folder}` the data folder, `{pid}` from `instance.json` in a start's lines
and from the request in the server's (as built, a line that names a process is written only once
its id was read, so none reads `unknown`).
- Info: `This VisualCommit holds the data folder {folder}: a second start on it is sent here.`
- Info, by answer: `VisualCommit already runs on the data folder {folder} (process {pid}); it was asked to come to the front, and this start ends.`;
  `VisualCommit is starting on the data folder {folder} (process {pid}); this start ends.`;
  `VisualCommit on the data folder {folder} (process {pid}) is closing; this start waits up to 30 s for it.`
  As built since step 19 (D464 (2)), the closing line names 30 s for the instance first asked; for
  one that took the folder meanwhile and answers `closing` too, it names what is left of the wait,
  in tenths of a second rounded down (`12.3`), never more.
- Warning: `VisualCommit holds the data folder {folder} but did not answer ({why}).`, `{why}` one of
  `no instance.json`, `its endpoint refused the connection`, `no reply within 3 s`,
  `a reply this version does not read`.
- After the wait: Info `The other VisualCommit closed; this start goes on.`, Warning
  `The other VisualCommit did not close within 30 s.`, its seconds since step 19 those the last
  closing line named; when the wait ends on a new holder whose endpoint refused the last attempt,
  the Warning is instead `VisualCommit holds the data folder {folder} but did not answer (its
  endpoint refused the connection).`, since the closing one did close.
- New since step 19 (D464): Info `Another VisualCommit took the data folder while this start
  waited; it is asked instead.`, once for each new holder's endpoint, as the waiting start first
  sees it; Info `The data folder {folder} is free now; this start goes on.`, when the lock comes
  free within the 250 ms after an unanswered ask.
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

`SingleInstanceGuardTests` (App.Tests, every platform, CI's six jobs; each data folder in a
`TempDirectory`, named `data folder`, with a space). Each guard's options are the test's own (they
belong to each guard, 1.11): a guard expected to answer, serve, take or wait gets `Connect` 5 s,
`Reply` 5 s and `WaitWhileClosing` 30 s, and every wait ends as soon as its condition holds; 300 ms
only on the side whose expected result is that no answer comes, where a stall under load can only
make that result surer. No test asserts an elapsed time under 1 s: the default run's load stretches
every step (D237), and a 250 ms window on the real clock failed in CI before
(Git.Tests' `RepositoryWatcherTests.cs:206-210`).
1. A second guard on a taken folder is `Held`; one on another folder `Taken`.
2. `AskToComeForward` reaches a fake `ComeForward` once: `Activated`; with it null, `Starting`; the
   holder's log has the server's line with the asker's pid.
3. The test holds `instance.lock` itself, no `instance.json`: `NotAnswered`, with at least `Connect`
   (300 ms) elapsed and at most `Connect` plus 5 s, with `no instance.json` in the Warning.
4. `ComeForward` answers `Closing`, the holder disposes 300 ms later: `WaitToTake` gives `Taken`
   (true before step 19, which made it a `HeldResolution`), `Outcome` `Taken`, and a third guard's
   ask reaches it.
5. A lock file left unheld and an `instance.json` naming a stale endpoint (on macOS and Linux a file at
   that path): the next guard is `Taken` and serves; on Unix the stale file is gone.
5a. A guard is `Taken`; the test replaces its `instance.json` with one naming a stale endpoint (as 5),
   starts a second guard's ask and writes the holder's file back 300 ms later: the ask gets
   `Activated`.
6. `Endpoint` holds no segment of the data folder below the temp folder; Windows
   `^VisualCommit-[0-9a-f]{16}$`, elsewhere the temp folder or `/tmp/` plus that, at most 100 bytes; two
   guards' differ. `SingleInstanceGuard.ServerPipeOptions` includes `PipeOptions.CurrentUserOnly`.
7. A data folder under a file (`<temp>/a-file/data`): `Unavailable`, no throw, a Warning naming it (a
   path under a file fails as root too, which a read-only folder would not in CI's containers).
8. `LockingOffWarning`: `1`, `TRUE` and the switch give the text on macOS and Linux; unset, `0`, and
   anything on Windows give null.
9. A malformed line, `"v":2` and a silent client get no answer and the next client is served; a
   `ComeForward` that throws, or one that blocks on a gate the test opens only after the holder's
   Warning, gives the asker (`Reply` 300 ms) `NotAnswered`; the holder (`Reply` 5 s) logs the Warning,
   and the next client is served.
10. `Dispose` twice is harmless; afterwards `instance.json` is gone, `instance.lock` stays, a new guard
    is `Taken`.
11. A `Held` guard asks (`Activated`) and is disposed: the holder's `instance.json` is unchanged byte
    for byte, and a third guard's `AskToComeForward` gets `Activated`. The same holds for a `Held`
    guard disposed after `NotAnswered`.

Step 19 added these (D464, ruling 100; the instance package, merged at `67ff55d`); 15 passed before
the fix too and guards what a simpler form of it would have broken, and the others were committed
failing first:
12. `Two_starts_that_wait_for_a_closing_holder_end_one_taken_and_the_other_handed_over_to_it`, a
    theory over the two callers, `ResolveHeld` as `Program.Main` makes it and `RetryOn` as "Try
    again" does: two starts wait for one closing holder, which lets the folder go once both have its
    answer; one ends `Taken`, the other `HandedOver` to it, with `Another VisualCommit took the data
    folder…` and the new holder's `starting` line in its log and no `did not close within`.
13. `A_holder_that_ends_while_it_is_asked_leaves_the_folder_to_the_asker`, over the same two
    callers: the test holds the lock and serves the endpoint `instance.json` names, reads the
    request and ends, the lock first: `Taken`, with `no reply within 5 s` and `The data folder
    {folder} is free now; this start goes on.`
14. `A_new_holder_that_ends_before_it_answers_a_waiting_start_leaves_the_folder_to_it_at_once`, a
    theory over a new holder that crashed (its `instance.json` left) and one that closed (deleted
    first): the waiter's `Connect` is 30 s, and it takes the folder within a third of that after the
    lock is let go, `Taken`, with no Warning.
15. `A_new_holder_that_does_not_answer_a_waiting_start_is_named_so_when_the_wait_runs_out`
    (`WaitWhileClosing` 5 s, nothing serving the new holder's endpoint): `NotAnswered`, with the
    Warning `…did not answer (its endpoint refused the connection).` and no `did not close within`.
16. `A_new_holder_that_is_closing_too_is_waited_for_within_what_is_left_and_the_log_says_how_long`
    (`WaitWhileClosing` 5 s, both holders answering `closing`): `NotAnswered`; the first closing
    line names 5 s, the second fewer, and the last line `The other VisualCommit did not close within
    {those seconds} s.`
17. `A_folder_copied_while_its_instance_runs_leaves_that_instances_endpoint_to_it`: a guard on a
    copy of a running guard's data folder is `Taken` with an endpoint of its own; on macOS and Linux
    the running guard's socket is still there; an ask on the original folder gets `Starting`, and
    the running guard logs no Warning.
18. `On_macOS_and_Linux_the_lock_file_and_the_data_folder_the_guard_makes_are_the_users_alone` (the
    lock file and the data folder, both made by the guard: no group or other bit; the missing folder
    above the data folder is made, its mode not asserted, since it gets the system's default) and
    `On_macOS_and_Linux_a_lock_file_and_a_data_folder_that_exist_keep_their_modes`; both skipped on
    Windows.

Tests 14 to 16 add about 11 s to the class, 15 and 16 waiting out their 5 s on purpose. Test 18,
and test 17's one assertion that the fix changes, fail only on macOS and Linux: their failing first
was shown by reasoning alone, no builder having such a machine. CI's run 45421164866 (on `b2ce493`,
the six merges) was their first run, and that of test 5 with the probe before the stale file's
deletion and of tests 13 to 16, which serve sockets at temporary paths: test 18's first test failed
there in the macOS job and the four Linux jobs, since it expected the missing folder above the data
folder to be the user's alone too and .NET gives the mode asked for to the last folder alone, so it
was narrowed to the lock file and the data folder and renamed (`e3002ac`; D464 (4) as corrected);
the others passed (ruling 100).

`InstanceNoticeViewModelTests` (App.Tests): the texts per platform, word for word; the folder line's
text is the folder given; `Trying…` and disabled while a held try runs; `NoAnswer` shows the status
and enables the button; `HandedOver`, `Free` and "Close" give `HandedOver`, `StartHere` and `Closed`,
"Close" also during a try, whose later result changes nothing; the theme read: `"theme": "Light"`
light, missing and unreadable Dark, the unreadable file still there.

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
| I1 | Not scripted (no harness runs `Program.Main`). Launch A on folder D; minimise it; start B on D, same exe and environment | B exits 0 within 10 s with no window; A is not iconic and visible within 2 s; the foreground noted, not asserted; D's log has B's "asked to come to the front" and A's "answered activated"; `session.json`'s and `instance.json`'s bytes unchanged by B | dark, 1100×700 | yes, its own (12.3.4 launch 4) |
| I2 | Windows text, `C:\Users\demo\AppData\Roaming\VisualCommit`; open: `i2a-notice-dark`; Enter, try held: `i2b-notice-trying`; no answer: `i2c-notice-no-answer`; Esc | i2a: heading, the folder line with the path, the body, the Windows sentence, no status, Close then Try again, Try again focused; i2b: `Trying…`, disabled; i2c: `It still did not answer.` in the warning colour, Try again enabled and focused; Esc: `Closed`, window gone | dark, 480 wide | yes: exit 2, then Try again starting the app after the lock is freed (12.3.4 launches 5 and 6; D443, as D448 amends it, asserts equal heights and masks the folder line) |
| I3 | Linux text, `/home/demo/.config/VisualCommit`; open: `i3a-notice-light`; Tab, Tab, Shift+Tab, Space | as i2a in light with the Linux sentence; the focus goes Close, Try again, Close; Space: `Closed` | light, 480 wide | no |

`Phase8aExpectations.I` holds the names (among them `InstanceNoticeFolder`, the element D448 masks),
the texts and the log lines' fragments that §12 reads. By hand at the gate (0.5), isolated: the lead
gives the owner a `.cmd` file outside the repository that sets CLAUDE.md's isolating variables
(`VISUALCOMMIT_DATA_DIR` to a folder of its own, `GIT_CONFIG_NOSYSTEM=1`, `GIT_CONFIG_GLOBAL` to an
empty file of its own, `XDG_CONFIG_HOME` to an empty folder, `VISUALCOMMIT_KEYCHAIN=memory`, and
`VISUALCOMMIT_GITHUB_URL`, `VISUALCOMMIT_AZURE_DEVOPS_URL` and `VISUALCOMMIT_GITLAB_URL` set to `none`)
and starts the Release build; the owner starts it twice, the second time while another window is in
front of the first. macOS (`open -n`, Terminal) goes to 8b's Mac checklist.

### 4.7 Earlier pictures

None. The guard runs only in `Program.Main`, App's change only under a desktop lifetime, which the
headless app lacks, and `MainWindow` gains a method and a flag. Proof: the builder's run of 4.5 and
part I; at wave 1's last merge the whole run's scripted pictures against the references
(`scripts/compare-pictures.py`) show nothing beyond §2's regions (0.7). New: `i2a-notice-dark`,
`i2b-notice-trying`, `i2c-notice-no-answer`, `i3a-notice-light`, I2's three real captures. In the real-window
pass each launch's folder gains the two files and its log one Info line; no check lists a data
folder's files (Grep), and each relaunch waits for the exit.

### 4.8 Lagged list, time, the pass

Lagged list: nothing new (8a's project is listed; part I has no watcher). Default run: about 35 s
(test 9 waits out the holder's 5 s `Reply`), without step 19's tests 14 to 16, which add about 11 s
(4.5).
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
no `ssh -V` probe and the netrc note; D449 (amends D423), winget started by its path and the full paths
a git must have.

The owner answered on winget on 2026-11-20 (D453): the screen offers it for a missing and for a
too-old git, not for one that does not work, and this section says so throughout.

### 5.1 Delivers and leaves

Delivers 0.1's first-run rows: the lookup and the setting, Check again, the screen's five states
(git's own words, a broken `~/.gitconfig` named), the guidance, the status bar, the settings row, the
netrc note, the ssh line, the stand-in, `FakeGitSetupProbe`, part F. Leaves the guide (§11), the sweep
(§9), the real-window code (§12), D401's question (8b; only while this screen is not shown), the Mac by
hand (8b).

### 5.2 Files and contracts

New: `src/VisualCommit.Git/OsRelease.cs` (`Parse(string) → LinuxDistribution?`; `/etc/os-release`,
else `/usr/lib/os-release`); `src/VisualCommit.Core/ProgramSearch.cs` (a static class in
`VisualCommit.Core`: `public static string? FindOnPath(string program, string? searchPath, char separator, Func<string, bool> fileExists)`,
`FileLauncher.FindOnPath`'s body and comment moved word for word, D130's rule of fully qualified
folders with them; `FileLauncher.FindOnPath` stays as a one-line forward, so its callers, its tests and
the comment at `KnownEditors.cs:295` are unchanged; VisualCommit.Git, which cannot reach App's
`FileLauncher`, calls it);
`src/VisualCommit.App/ViewModels/GitSetup/GitSetupViewModel.cs`, `GitSetupTexts.cs` (every text below);
`Views/Shell/GitSetupView.axaml(.cs)`; `ViewModels/MainWindowViewModel.Git.cs`;
`ViewModels/Settings/GitProgramViewModel.cs`; `tests/VisualCommit.GitStandIn/` (as
`VisualCommit.AskPass.csproj`, `AssemblyName` `visualcommit-git-stand-in`, 1.14's answers);
`tests/VisualCommit.Testing/Headless/FakeGitSetupProbe.cs`, `ShellDriver.Phase8a.FirstRun.cs`; 8a's
`Phase8aChecks.F.cs`, `Phase8aExpectations.F.cs`, `Phase8aExpectationTests.F.cs`, `GitSetupShellTests.cs`.

Changed: `src/VisualCommit.Git/GitLocator.cs` (`GitSearchContext` gains init `IsMacOS`,
`Func<bool>? CommandLineToolsInstalled`), WP0's `GitSetupProbe.cs`, `GitConfiguration.cs`,
`Prompts/AskPassBroker.cs`; under `src/VisualCommit.App/`: `Services/GitAccess.cs`,
`Services/FileLauncher.cs` (`StartAsync`'s body, `ResolveStart`, `ToStartInfo`'s case for
`InNewTerminal`, `FindOnPath` a forward to `ProgramSearch`), `ViewModels/MainWindowViewModel.cs`,
`.Settings.cs`, `RepoTabViewModel.cs`, `WelcomeViewModel.cs`, `Views/Shell/CommitGraphView.axaml(.cs)`,
`StatusBarView.axaml`, `Views/Dialogs/SettingsDialogView.axaml`,
`ViewModels/Dialogs/SettingsDialogViewModel.cs` (and the D277 comment at `:62`, which now names the
netrc note), `ViewModels/Settings/AccountsSettingsViewModel.cs` (it keeps `gitVersion` and gains
optional `Func<bool>? homeHasNetrc`, `Action? dismissNetrcNote`), the D277 comment at
`SettingsDialogView.axaml:560`; `tests/VisualCommit.Git.Tests/VisualCommit.Git.Tests.csproj` (the
stand-in's reference); `tests/VisualCommit.Git.Tests/MergeToolHelpTests.cs` (its fake runner
`HeldToolHelpRunner`, a private nested class at `:389`, made `internal` for `GitConfigurationForgetTests`).

Made real: 1.2 (with `GitAccess.ProbeAsync`, `ReadGuidance` and `RepoTabViewModel.GitSetup`), 1.3's
`StartAsync`, `ResolveStart` and `ToStartInfo`'s case for `InNewTerminal`, 1.4, 1.10's `ProbeGit`,
`HomeHasNetrc` and `DismissNetrcNote`, 1.14, 1.15's fake. Used: `GitSetupStates.Of`, `AppLinks.Guide`,
`FakeFileLauncher`, `ShellDriver.Start`, `GitStandIn`.
`GitSearchContext` (`GitLocator.cs`) gains init members `bool IsMacOS` and `Func<bool>?
CommandLineToolsInstalled`, its positional constructor unchanged.

### 5.3 Behaviour

**Lookup**, off the caller's thread (it may start `xcode-select`): the setting: a file there is run
whatever it answers (`Setting`); no file: `NotFound` with `ChosenMissing`, and `PathCandidate` = what
the next steps find, not run; no fallback (ruling 41). Else `StandIn`: a path to a file is the only git;
`none` or no file: none. Else the PATH, its fully qualified folders only (as `ProgramSearch.FindOnPath`, D130; today's
search also takes a relative entry, which would run a git from the app's current folder, D449), then
the usual folders. A setting or a stand-in that
is not `Path.IsPathFullyQualified`, or on Windows does not end in `.exe`, is never run (D449): the
setting gives `ChosenMissing` as a gone file does, with a Warning `The Git chosen in Settings is not
used: {path} is not a full path.` or, on Windows, `The Git chosen in Settings is not used: {path} is
not an .exe file.`; the stand-in counts as `none`. In this rule "on Windows" means the machine the app
runs on (`OperatingSystem.IsWindows()`), and "a full path" is `Path.IsPathFullyQualified` on that
machine; the guidance's platform chooses texts only. So a test gives a chosen path that is a full path
on the machine it runs on (the report's table of paths per platform). On macOS `/usr/bin/git`, from the setting, the PATH
or the usual folders, is run only when `/usr/bin/xcode-select -p` exits 0 within 5 s (asked once per
search); otherwise it is skipped with `CommandLineToolsMissing`, and a chosen one gives `NotFound` with
no fallback (the screen's Missing M with the tools missing).

**Version.** Not started: `Broken`, `ErrorText` the exception's message. Exit ≠ 0 or no version:
`Broken`, `ErrorText` standard error, else output (≤ 4,000), `ExitCode`; `^fatal: bad config line (\d+)
in file (.+)$` gives `ConfigFault`; the Warning names that first line. `Using Git {version} at {path}.`
stays; a Debug line adds the source. Windows, no `.windows.` in the output: used, with Warning `Git
{version} at {path} is not Git for Windows; VisualCommit is tested with Git for Windows.`

**GitAccess** as 1.2; 1.14's Warning once, at the first search with a stand-in: `VISUALCOMMIT_GIT_STAND_IN
is set (a test switch): unless Settings names a Git, Git is looked for only at {value}.`, or, for `none`
and for a value that is not a full path (or, on Windows, does not end in `.exe`), as 1.14 has it (a full
path with no file gets the first), `VISUALCOMMIT_GIT_STAND_IN is set (a test switch): unless Settings
names a Git, this computer is treated as having no Git.`; ChosenMissing's text
`The Git chosen in Settings was not found at {path}.`, or, for a setting the lookup does not run, the
Lookup's Warning text: `The Git chosen in Settings is not used: {path} is not a full path.` or, on
Windows, `The Git chosen in Settings is not used: {path} is not an .exe file.` `ProbeAsync` and
`ReadGuidance` as 1.2; the fixed-detection constructor's `ReadGuidance` is
`new GitGuidance(<the running platform>, null, null)` and reads no file and no `PATH`.
**Guidance**, off the UI thread and only when a state shows the screen: on Windows
`MachineGitSetupProbe.ReadGuidance` sets `WingetPath` = `ProgramSearch.FindOnPath("winget.exe", PATH,
';', File.Exists)`: fully qualified `PATH` folders only, and the path 1.3's start runs. `cmd.exe`'s own
search of a bare name, which starts in its current folder and tries each `PATHEXT` extension, is never
relied on. On Linux `OsRelease`. It is read once per session, off the UI thread, the first time a
state shows the screen. The screen shows only once it is read: the refresh's last step, "the screen",
awaits it, so `Initialization` and Check again end with the screen and its guidance, and no state
shows another platform's texts or a winget button that comes later.

**States** (`GitSetupStates.Of(Git, Prompts.MissingHelperPath)`): the screen shows for Missing,
ChosenMissing, TooOld, NotWorking, and HelperMissing until "Continue without it" (this session). A
search that throws is NotWorking without a path, its message as git's words.

**Place.** One `GitSetupViewModel` per window; `AddTab` sets the new `RepoTabViewModel.GitSetup`.
`ShowsGitSetup => Repository is null && !IsOpening && !IsDashboard && GitSetup?.IsShown == true`;
`ShowsWelcome` adds `&& !ShowsGitSetup`. `CommitGraphView`'s row 0 holds the `ContentControl`
`GitSetupSlot`, whose content is `RepoTabViewModel.GitSetupForView`: the screen once it has first
shown (`GitSetupViewModel.HasShown`), null before, so a session that finds git makes no
`GitSetupView`. Each button of the `WrapPanel` `GitSetupButtons` carries `kr:KeyRegion.IsMain` (Copy
and the links do not), so the first shown is the graph area's focus target
(`CommitGraphView.GraphAreaFocusTarget`); the view takes no focus itself, and hidden while holding
it gives it to the graph area's target. Layout as the welcome page, `MaxWidth` 520: title (16,
SemiBold), body (`secondary`), the path (`PathTextBlock`), "Git said:" over a `SelectableTextBlock`
(`VcCodeFontFamily`, `VcBorderBrush`, six lines, then a scroll bar), guidance, the command with Copy,
buttons (`VcActionButton`), `GitSetupError`, links (`VcFlatButton`s of class `gitSetupLink` in the
`WrapPanel` `GitSetupLinks`; their text `VcAccentBrush`, `VcAccentOnRowBrush` on hover, set by
`GitSetupView`'s own styles). Tokens only.
`GitSetupView`'s `AutomationProperties.Name` is the state's title and its `AutomationProperties.HelpText`
the state's body, so that a screen reader speaks them when the screen shows with the focus on its first
button (§9's sweep and Narrator steps 20 and 21 read them); no picture changes. Choose and Check
again have help texts of their own since step 19 (below).

**Buttons** (W, M, L the guidance's platform; "chosen": the git came from the setting):

| Id | Text | Shown | Does |
|---|---|---|---|
| `GitSetupWingetButton` | Missing, ChosenMissing: `Install with winget…`; TooOld: `Update with winget…` | W with `WingetPath` set: Missing; ChosenMissing without a candidate; TooOld when the git was found on the machine, not chosen (a chosen git changes by `Use the Git on the PATH` or Choose). Never NotWorking (D453) | confirm, then 1.3's start |
| `GitSetupDownloadButton` | `Download Git for Windows` | W Missing, TooOld, NotWorking, ChosenMissing without a candidate | `https://git-scm.com/install/windows` |
| `GitSetupCltButton` | `Install the Command Line Tools` | M Missing with the tools missing; M NotWorking with `xcrun: error` or `requesting installation of command line developer tools` | 1.3's start, no confirm |
| `GitSetupShowFileButton` | `Show the file` | a config fault | `RevealAsync(file)` |
| `GitSetupChooseButton` | W `Choose git.exe…`, M L `Choose the git program…` | all but HelperMissing | Choose |
| `GitSetupUsePathButton` | ChosenMissing: `Use the Git it found`; TooOld, NotWorking: `Use the Git on the PATH` | ChosenMissing with a candidate; TooOld, NotWorking when chosen | removes the setting, Check again |
| `GitSetupCheckAgainButton` | `Check again`; `Checking…` disabled while searching | all | Check again |
| `GitSetupContinueButton` | `Continue without it` | HelperMissing | hides the screen |
| `GitSetupCopyButton` | `Copy` | a command shows | the command to the clipboard |
| `GitSetupMoreLink` | `Other ways to install Git` | M, L Missing, TooOld, ChosenMissing without a candidate | `https://git-scm.com/install/mac`, `/linux` |
| `GitSetupGuideLink` | `How VisualCommit finds Git`; helper: `What the helper does` | all | `AppLinks.Guide` of the state's page |

As built: the buttons that start something or copy (winget's, the Command Line Tools', Download,
Show the file, Copy and the two links) allow a run while one runs, so each stays enabled, and keeps
the keyboard focus, while the launcher starts a program off the UI thread or the clipboard answers;
a second press starts the same again (winget's since §5, the others since step 19, `2a2a1fc`).
Choose and Check again are disabled while they run, and give the focus back as below.

**Texts** (`{v}` found, `{min}` `GitVersion.Minimum`; the path in `GitSetupPath`):

| State | Title | Body | Guidance (`GitSetupGuidance`) |
|---|---|---|---|
| Missing W | `VisualCommit needs Git` | `VisualCommit works through the Git installed on this computer, and it found none: not on the PATH, nor in Git for Windows' usual install folders.` | `Install Git for Windows, then press Check again. VisualCommit does not need to restart.` |
| Missing M | same | `VisualCommit works through the Git installed on this Mac, and it found none.` + tools missing: ` Apple's Command Line Tools, which include Git, are not installed.` | tools missing: `Install the Command Line Tools, which macOS asks you to confirm, or install Git with Homebrew, then press Check again. With Homebrew, in Terminal:`; else `Install Git with Homebrew, then press Check again. In Terminal:`; command `brew install git` |
| Missing L | same | `VisualCommit works through the Git installed on this computer, and it found none on the PATH or in the usual folders.` | known: `Install it with your distribution's package manager, in a terminal, then press Check again. On {PRETTY_NAME, else ID}:` + command; unknown: `Install the package named git with your distribution's package manager, then press Check again.` |
| ChosenMissing | `The chosen Git is gone` | `VisualCommit was set to use the Git at this path, and there is no file there now:`; a setting the lookup does not run (not a full path, or not `.exe` on Windows; `GitSetupTexts` applies the lookup's rule to the path): `VisualCommit was set to use the Git at this path, but it runs Git only from a full path to the program itself:` | `VisualCommit found another Git at {candidate}.`; none: `No other Git was found on the PATH or in the usual install folders. Choose another, or press Check again once the chosen one is back.` |
| TooOld | `Git {v} is too old` | `VisualCommit needs Git {min} or newer. It found Git {v} at:` | W `Update Git for Windows, then press Check again. Its installer updates it in place.`; M `Update the Command Line Tools in System Settings, General, Software Update, or install Git with Homebrew, then press Check again. With Homebrew, in Terminal:` + `brew install git`; L, every distribution, no command: `Install a newer Git from your distribution, or choose another Git, then press Check again.` (`GitSetupMoreLink` is the way to other sources; the app names no third-party package source, D449) |
| NotWorking | `Git is not working` | `VisualCommit found Git at the path below, but it did not report its version.`; empty output: `…but it said nothing and exited with code {n}.`; not started: `…but it could not be started: {message}` | fault: `Git cannot read line {n} of {file}. Correct that line, or rename the file, then press Check again.`; W `Repair or reinstall Git for Windows, then press Check again.`; M tools broken: `Apple's Command Line Tools look damaged. Install them again, or select them in Terminal, then press Check again:` + `sudo xcode-select --switch /Library/Developer/CommandLineTools`; M `Reinstall Git, then press Check again.`; L `Reinstall git with your distribution's package manager, then press Check again.` |
| HelperMissing | `Part of VisualCommit is missing` | `Git works, but VisualCommit's helper for git's questions is missing. It was looked for at:` | `Without it, git and ssh cannot ask for a password, a passphrase or a host key here, and a signed-in account cannot be used for git. Install VisualCommit again to restore it.` |

With a fault the guidance names the file's line as the cure, and the download stays as D405 offers it.

Linux commands by `ID`, then each `ID_LIKE` token: `debian`, `ubuntu`: `sudo apt install git`;
`fedora`, `rhel`, `centos`: `sudo dnf install git`; `arch`: `sudo pacman -S git`; `suse`, `opensuse`,
`opensuse-*`: `sudo zypper install git`; else unknown.

**winget** (rulings 6, 44; D453): the confirm is `ConfirmationRequest("Install Git with winget?", "VisualCommit
opens a terminal window that runs winget install --id Git.Git -e --source winget", "winget downloads Git
for Windows from its community repository, which the Git for Windows project does not maintain, and runs
the installer; Windows may ask for permission. When it has finished, close the window and press Check
again.", "Install", IsDestructive: false)`. For a too-old git the title is `Update Git with winget?`,
the button `Update` and the last text `winget downloads the newest Git for Windows from its community
repository, which the Git for Windows project does not maintain, and runs the installer, which updates
the one installed; Windows may ask for permission. When it has finished, close the window and press
Check again.`; the command is the same, since winget's install upgrades a package that is installed
(unverified on a Git for Windows that its own installer put there: the laptop has the newest; the
download stays beside it). Install, or Update, starts 1.3's command (D449),
`new LaunchCommand("cmd.exe", []) { CommandLine = "/S /K \"\"" + wingetPath + "\" install --id Git.Git -e --source winget\"", WorkingDirectory = Path.GetDirectoryName(wingetPath), InNewTerminal = true }`
with `wingetPath` the guidance's `WingetPath`, not waited for, logged Info `Started in a terminal: {the
command's DisplayText}`. winget runs from the path the guidance found, never from its bare name: given
a bare name, `cmd.exe` would look first in its current folder (a cloned repository when the app was
started there) and then in every `PATH` folder under each `PATHEXT` extension. Started in winget's
folder with that file's full path, it runs the file the screen checked; `/S` and the outer pair of
quotes keep the path one piece whatever characters it holds. The working folder is
`Path.GetDirectoryName(wingetPath)`: a test asserts the literal folder on Windows and that expression's
value elsewhere. The confirm and the guide show
git-scm.com's line `winget install --id Git.Git -e --source winget` unchanged.
`StartAsync` is `Process.Start(ResolveStart(command, findOnPath, Environment.SystemDirectory).ToStartInfo())`
off the UI thread, not waited for, with `findOnPath` the forward `FileLauncher.FindOnPath` over the
`PATH`, `Path.PathSeparator` and `File.Exists`.
`public static LaunchCommand FileLauncher.ResolveStart(LaunchCommand command, Func<string, string?> findOnPath, string systemFolder)`
is pure: `cmd.exe` resolves to `systemFolder`'s, any other bare name through `findOnPath`, a full path
stays as it is (the tools' `/usr/bin/xcode-select`); a bare name that is not found throws
`FileNotFoundException`, before any start. `LaunchCommand.ToStartInfo()` sets `UseShellExecute = true`
and redirects nothing when `InNewTerminal` is set on Windows; elsewhere `InNewTerminal` changes nothing.
The tools' start logs `Started: /usr/bin/xcode-select --install`. A failed start: `{winget|xcode-select}
could not be started: {message}` in `GitSetupError`.

**Choose**: the picker (`Choose git.exe`; `Choose the git program`); on Windows a name without `.exe`
gets `SettingsTexts.ProgramNotExe` unrun; `GitSetupError` reads `Checking…`; `ProbeAsync`. As
built since step 19 (ruling 100), the probe (`GitSetupViewModel.CheckChosenAsync`, Choose's and
the row's Browse's) is stopped after `GitSetupViewModel.ChosenProbeLimit`, 10 s, which stops the
program it runs (one that does not exit on `--version`), and ends with the window (Choose's token is
the window's lifetime, `GitSetupServices.Lifetime`) or, for Browse, with the settings screen, whose
token Cancel, Esc, the session's end and a Save that closes the screen cancel; a probe so ended
refuses nothing and changes nothing. Found: `settings.Update(GitExecutable = path)`, then Check
again once a running one has ended (`GitSetupViewModel.CheckAgainAfterRunningAsync`, as for `Use
the Git…`: the command runs one search at a time, and the search it then starts reads the setting
just changed; a call during a search told another lookup searches after it, 1.2). Else, settings
untouched, `GitSetupError`: `Git {v} at this path is too old: VisualCommit needs {min} or newer.`,
`This Git did not report its version. Git said: {first line}`, `This file could not be started:
{message}`, `This file did not report its version within 10 seconds, so VisualCommit stopped it.`
(`GitSetupTexts.ChooseNoAnswer`, since step 19; Choose can be pressed again) or
`SettingsTexts.ProgramMissing`. Choose is disabled while its file dialog is open and its file is
probed, which drops the keyboard focus, and a cancelled pick or a refused file starts no search
that would place it: since step 19, once its run has ended with the focus on nothing, on the window
or on a control no longer shown or enabled, the button takes it back, as by Tab when it had
`:focus-visible` as it was pressed (`FocusReturn.AfterRun` with `FocusReturn.LostFrom`, from
`GitSetupView`'s click handler), and the settings row's Browse does the same (`SettingsDialogView`);
a Choose that found git hides the button, and the search's end places the focus. The button that
gets the focus back is described by the outcome through `AutomationProperties.HelpText`, which a
reader is told with the focus (D463 (3); 9.3.7): Choose by `ErrorText`, the error line, which reads
`Checking…` while its file is probed and the refusal once it is refused (empty before); the row's
Browse by `GitProgramViewModel.StatusText`, its status, with the same `Checking…` and refusals, and
empty after a file that passes (ruling 101: by the row's path and note when it has no status, step
19's third round). The error line also carries a failed start of winget or of the Command Line
Tools (`{winget|xcode-select} could not be started: {message}`, above), so Choose is then described
by a failure that is not its own until a new state, a chosen file or another start replaces it; and
that failure is spoken nowhere as it happens: its button keeps the focus, so no focus event follows,
and the error line's live region has no node (D455). Accepted, with a Backlog row (ruling 101): a
text that changes while its button keeps the focus needs another means than a description.

**Check again** (`MainWindowViewModel.Git.cs`), 1.2's order: `Looking for Git...`; the search; `Git`,
`GitStatusText`, `GitStatusToolTip`; `CheckHelper()` off the UI thread, only while git works and the
helper was missing; the screen. Found:
`ForgetToolLookup`, the merge tools' lookup after the reopenings; each tab with `FailedPath`:
`ReopenAsync()` (new: `Welcome.ClearGitError()`, `ShowPending`, `OpenAsync(remember: false)`); each
empty tab `Welcome.ClearGitError()` (new: drops an error from `GitUnavailableException`); the active
dashboard's `RefreshCommand`. In HelperMissing git works, so Check again looks for the helper only.
`InitializeAsync` uses the same refresh. The button is disabled while it searches, which drops the
focus (keyboard.md D-a): when the search ends with the focus on nothing or the window,
`GitSetupCheckAgainButton` takes it again (`Tab` if it had `:focus-visible` when pressed, else
`Unspecified`), or, the screen gone, the graph area's target. The title is a polite live region
since step 19 (D463 (2)), heard by a reader that has read the screen (ruling 100); the button is
described by the title at all times, its `AutomationProperties.HelpText` being `Title`, so that the
focus given back is announced with the title of the state the search ended on (D463 (3); 9.3.7).

**Status bar.** `GitStatus` unchanged but for its help text (below); new texts `Chosen Git not found`, `Git {v} (helper missing)`;
`ToolTip.Tip` `{path}, found on the PATH`, `{path}, found in Git for Windows' install folder` (else `in
a usual install folder`), `{path}, chosen in Settings`, `{path}, from VISUALCOMMIT_GIT_STAND_IN (a test
switch)`; null without a path. `GitStatus` gets `AutomationProperties.HelpText` equal to its tooltip's
text (`GitStatusToolTip`), since 12.3.4's launches 7 and 8 read it through UI Automation; no picture
changes. `GitSetupButton` (`VcFlatButton`, hidden but in the screen's states):
`Set up Git…`, or `Details…` (HelperMissing, undoes Continue); activates the first tab showing the
screen, else a new tab.

**Settings row** (rulings 40, 42): last in the global scope, in every state of the Git section, never
in "This repository"; `Grid 132,12,*`, top margin 18. Label `Git program`; `GitProgramPath`
(`TrimmedTextBlock`, leading ellipsis): the pending choice, else the git in use, else `None found`;
`GitProgramNote`: `Found on the PATH.`, `Found in Git for Windows' install folder.` (else `Found in a
usual install folder.`), `Chosen here.`, `Chosen here. There is no file at this path.` (for a setting
the lookup does not run, `Chosen here. VisualCommit runs Git only from a full path to the program
itself.`), `From VISUALCOMMIT_GIT_STAND_IN, a test switch.`, `No Git was found on the PATH or in the
usual install folders.`; then, while the choice differs from the git in use, ` VisualCommit uses it
from its next start.` (git works) or ` VisualCommit looks for Git again when you save.`; after `Use
the Git it found` while the chosen git is gone and another was found, the note is `VisualCommit
looks for Git again when you save.` alone (no git is in use, yet one was found, so none of the texts
above is true); after `Use the Git on the PATH` drops a chosen git that is in use and works, also on
reopening after Save, the note is `Chosen here. VisualCommit looks for Git again at its next
start.` (the source's note, then `GitSetupTexts.NextStartLooksAgainSuffix`; ruling 100's words: the
next start's search looks in the usual install folders too, so it need not use the Git on the
PATH; before step 19 the row said ` VisualCommit uses it from its next start.`, of the git Save
drops); a chosen git in use that does not work keeps ` VisualCommit looks for Git again when you
save.`; `GitProgramBrowseButton` `Browse…` (Choose's checks, its probe's limit, its focus given back
and its description by its status, empty after a pass, as above (ruling 101: by the row's path and
note when it has no status, step 19's third round); `GitProgramStatus`, no live region);
`GitProgramUsePathButton`
`Use the Git on the PATH` while a path is chosen; `Use the Git it found` instead in ChosenMissing with a
candidate, as on the screen, and while a pending choice would give way to a git in use that the install
folders gave (`GitSource.InstallFolder`). Save's step 2 writes `GitExecutable`. After the screen
closes, a changed setting while git does not work runs Check again, once a running one has ended (as
Choose).

**Netrc note** (ruling 8): `AccountsNetrcNote` (classes `settingsNote notice warning`: the existing
`TextBlock.notice.warning` style gives `VcWarningBrush`) and
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

As built since step 19 (ruling 100, which amends ruling 7; D424 stands), that is the line on macOS
and Linux, `AskPassBroker.AskPassRequireLineUnix`; on Windows it is
`AskPassBroker.AskPassRequireLineWindows`, `Git and ssh get SSH_ASKPASS_REQUIRE=force, so that ssh
asks through VisualCommit; OpenSSH older than 8.4 ignores it, and on Windows no terminal shows its
question: cancel the operation if it waits.`, because the app starts every git without a console
window, so ssh never reaches the terminal the app was started from. The broker logs
`AskPassBroker.AskPassRequireLineFor(OperatingSystem.IsWindows())`. Nothing was measured with an
old OpenSSH on Windows: the words hold whether ssh waits or fails. The guide's page
(`troubleshooting/sign-in-and-ssh.md`) has a section, "An old OpenSSH does not ask through
VisualCommit", with a line for each case.

**Helper**: `HelperFound` volatile; `CheckHelper()` looks again, Info `The askpass helper is there now
({path}).`

**FakeGitSetupProbe** (1.15) shows made-up values: `C:\Program Files\Git\cmd\git.exe`,
`C:/Users/demo/.gitconfig`; Ubuntu 22.04 (`ubuntu`, [`debian`], `Ubuntu 22.04.5 LTS`), Alpine. Its
ChosenMissing answer takes a candidate or none.

### 5.4 Tests

Fail first: the lookup, the stand-in, the macOS rule, `ConfigFault`, the reopening, `ResolveStart` and
`ToStartInfo`'s case for `InNewTerminal`, `CheckHelper`, Choose's write, the row, the note.
- Git.Tests: `GitLocatorTests` (added: the order; a gone setting; the stand-in, `none`; one case per
  rule of D449: a setting that is not a full path, a stand-in that is not, a setting and a stand-in
  without `.exe` on Windows, a chosen `/usr/bin/git` while `xcode-select -p` fails, a relative `PATH` entry that holds git
  skipped; the macOS rule
  through the context; TooOld, Broken 128 and the "answers only" case through the stand-in; the Windows
  Warning; real git with `[user` in `GIT_CONFIG_GLOBAL` gives `ConfigFault(file, 1)`, on every CI job);
  `OsReleaseTests` (quotes, escapes, `#`, a later key wins, the `/usr/lib` file), `GitStandInTests`,
  `AskPassBrokerHelperTests` (the ssh line once), `GitConfigurationForgetTests` (over `MergeToolHelpTests`'
  fake runner, `HeldToolHelpRunner`; never the real `git mergetool --tool-help`).
- App.Tests, no session: `GitAccessTests` (the class exists since WP0, with four tests, 0.4; added
  here: waiters, swap, joining, `Changed`, `ProbeAsync`; the
  fixed-detection constructor's `ReadGuidance` has no winget and no distribution),
  `CheckGitAgainTests` (the order, reopening, errors cleared, tools asked again, HelperMissing keeps
  git), `GitSetupTextsTests` (state × platform, `{min}`, the commands; L TooOld shows no command,
  whatever `ID` or `ID_LIKE`), `GitSetupViewModelTests` (the winget button by state, D453: Missing and a too-old git found on the
  machine with `WingetPath` set, not a too-old chosen git, never NotWorking, none without `WingetPath`;
  TooOld's button and confirm read Update; Install hands the launcher one `Start` whose
  command line holds the seam's `WingetPath`, quoted as 5.3 has it, and whose `WorkingDirectory` is that
  path's folder; ChosenMissing with a candidate and without one), `GitProgramViewModelTests`,
  `AccountsNetrcNoteTests`; added to `FileLauncherTests`: `ResolveStart` (`cmd.exe` to the given System
  folder's, a bare name through the given search, a full path kept, a bare name not found throws
  `FileNotFoundException`), `ToStartInfo` of the winget command (on Windows `UseShellExecute` true and
  nothing redirected, elsewhere false; `Arguments` equal to its `CommandLine` exactly; its
  `WorkingDirectory`), and the only call of the real `StartAsync`, which names, without `InNewTerminal`,
  a full path to a program that does not exist in an empty `TempDirectory`, reads no `PATH` and throws
  the start's own exception before anything starts; `JsonSettingsStoreTests` (`gitExecutable` absent,
  written, read back), `MainWindowViewModelTests`, `SettingsDialogViewTests`. No test starts `cmd.exe`, winget,
  `xcode-select`, a terminal or a browser.
- 8a's project: `GitSetupShellTests` (each state: no clipped text at 1100×700 and 1000×560; the view's
  automation name and help text are the state's title and body).
- Earlier: `ShellTests`' NotFound window now shows the screen and stays green unchanged.

### 5.5 Part F

Dark, 1100×700, the fake probe and launcher unless named. 14 checks; RW the real window repeats.

| Check | Steps | Expected |
|---|---|---|
| F1 Missing (RW) | W with winget, one restored tab | the texts and buttons; `Git not found`, `Set up Git…`; a New tab; no git call; `f1a-missing-windows`, `f1b-missing-windows-light` |
| F2 Offers (RW: confirm, Cancel) | Install…, Cancel; Install; Download | `f2a-winget-confirm`; no start after Cancel; one `Start`, 1.3's, with the seam's `WingetPath` and its folder, `InNewTerminal`; one `OpenUrl` |
| F3 Chosen gone | ChosenMissing with a candidate; Use the Git it found | `f3a-chosen-gone`; then the key gone, screen gone |
| F4 Found again (RW: native dialog) | Missing, then Real; Check again | `Checking…`; the tab's graph; status, tooltip; tools asked; `f4a-found-again` |
| F5 Too old (RW: stand-in) | W with winget, 2.29.0; Update…, Cancel | `{v}`, `{min}`, path, Download, `Update with winget…` (D453); `f5a-too-old`; the confirm `Update Git with winget?`, `f5b-winget-update-confirm`; no start after Cancel |
| F6 Config fault (RW: stand-in) | Show the file | git's line, `line 1 of C:/Users/demo/.gitconfig`; `Reveal`; `f6a-config-fault`. F6b (RW): real git, `[user` in the session's global file: `fatal: bad config line 1 in file`, no picture |
| F7 Not started | | `could not be started: {message}`; `f7a-not-started` |
| F8 Choose | too old; then `RealAt(D:\Tools\PortableGit\cmd\git.exe)` | refusal, settings.json untouched, `f8a-choose-too-old`; key written, screen gone |
| F9 macOS | tools missing; Install; Copy; `xcrun: error` | `f9a-macos-missing`; `Start` of `xcode-select --install`, no confirm; `brew install git` copied; `f9b-macos-broken` |
| F10 Linux | Ubuntu, Alpine Missing; Ubuntu TooOld | `f10a-ubuntu-missing`, `f10b-alpine-missing`, `f10c-ubuntu-too-old` (the generic Linux text, no command or Copy, `Other ways to install Git`) |
| F11 Helper (scripted only) | no helper file; Continue; `Details…` | `f11a-helper-missing`; `Git {v} (helper missing)`; welcome page; screen back |
| F12 Settings row | Browse, Save; reopen; Use the Git on the PATH; Browse too old | `f12a-row-next-start`; the key written, then removed; `f12b-row-too-old` |
| F13 Keys (RW) | F6, Tab, Enter on Check again | the first button; all in order; Enter acts; after the search Check again has the focus again |
| F14 Netrc | signed in for git, `RealAt` saying 2.43.0, `_netrc` in the session's home; Dismiss; reopen | `f14a-netrc-note`; gone; no file, no note |

`Phase8aExpectations.F` holds, for §12 (12.3.4 launches 7 to 10): F1's and F4's scenario and session,
F5's and F6's, F13's focus order with winget found, the picker's title `Choose git.exe`, F5's version
line `git version 2.29.0`, F6's failure text, the confirm's title and the picture names.

### 5.6 Earlier pictures

Ruling 40's nine: `s14c`, `s14i`, `s18a`, `s18d`, `s19a`, `s19d`, `s20b`, `l31e-settings-git-dark`, `l31e-settings-git-light`, at
the global scope's bottom; none else among the earlier pictures, and of 8a's own
`l3f-settings-name-placeholder` (0.7). Proof: the builder runs only the classes and methods named here,
never a whole project: `--filter-class "*Phase6ChecksS"` in `VisualCommit.VisualTests.Phase6`,
`--filter-method "*Check_L31*"` in `VisualCommit.VisualTests.Phase6Look`, and `--filter-class
"*Phase0Checks" --filter-class "*Phase2Checks"` in `VisualCommit.VisualTests`, and compares their
pictures through `scripts/compare-pictures.py` against `references/wave1/`: only the nine differ, there.
Whole projects are the lead's, through `scripts/heavy-run.sh`; wave 2's whole run at its close (status
step 12) covers the rest. Any other difference stops the package (ruling 3).

### 5.7 Lagged list, time, real window

Lagged list: 0.4's last line, now with part F and `GitSetupShellTests`. Default run: part F 3 to 4
minutes alone; App.Tests about 60 tests, Git.Tests about 35, a minute each. Real window (§12): four
launches (F1, F2's confirm, F13, F4; F5; F6; F6b), 3 to 4 minutes; the stand-in from `RealWindowTests`'
output, its variable set last; no press of Install, Download, Copy or a link (ruling 47 for the path).

### 5.8 Done when

5.4 passes (fail-first tests failed first); part F on the laptop and in CI's six jobs (F6b on 2.34.1);
5.6 holds; no warning; no test reads the machine's PATH, home or settings, and none starts `cmd.exe`,
winget, `xcode-select`, a terminal or a browser; the lead's by-hand `cmd.exe /S /K ""<the path found>"
--version"` (1.3) showed winget's version in a window that stayed open; part S's settings.json has no
`gitExecutable`.

### 5.9 Risks, unverified, open

- Unverified: whether `cmd.exe /S /K` started with `UseShellExecute` shows a window that stays open
  where Windows Terminal is the default console: the lead's by-hand `cmd.exe /S /K ""<the path
  found>" --version"` before the merge, the owner told. Settled (the lead, 2026-11-19): the laptop's
  winget is `%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe`, an app execution alias that `File.Exists`
  finds on the `PATH`; the real-window F1, F2 and F5 need winget on the gate's machine and fail without it
  (D449). A Mac (`xcode-select -p`, the shim, `--install`): 8b's checklist. git-scm.com's pages and
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
do not already do it; the activity log's splitter goes to a fixer (0.4); D452 (amends D417), "Cancel
{title}" cancels only while the titled operation is still the one running (6.3.3).

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
- Shift+Tab from the text goes to the toolbar's last shown button ("Discard file" on an unstaged
  diff with no lines selected, "Unstage file" on a staged one).
- Enter or Space runs the focused button, which shows Fluent's frame (Tab only).
- On `BarRemoving`, when the bar holds the focused element and it has `:focus-visible`, or raised
  the action while it had, the left text area takes the focus (`Unspecified`) before the bar leaves.
  After "Stage hunk" by Enter the focus is in the text; a "Discard hunk" cancelled in its question
  keeps the bar, and D104 gives the button its focus back.
- F6 and Ctrl+End are unchanged (`KeyRegion.IsMain` on the left editor).

**6.3.2 F2, the blame.** `BlameText.AddHandler(KeyDownEvent, OnTextKeyDown, RoutingStrategies.Bubble,
handledEventsToo: true)`: after Up, Down, Left, Right, PageUp, PageDown, Home or End, with any modifier
(on macOS Cmd and Option with an arrow move the caret too), the run of `TextArea.Caret.Line`
(`BlameGutter.RunOfLine`) is picked
(`BlameViewModel.Pick`) when it is not the picked one: the gutter draws it, the right panel shows its
commit. No other key picks. `OnContextRequested`'s keyboard branch takes `SelectedRun ?? the caret's
run`, so Shift+F10 right after opening shows run 0's menu. Opening picks nothing (b6a to b6h come from
clicks). Enter does nothing new; "Show in graph" is in the run's menu. Tab leaves the text.

As built since step 19 (rulings 100 and 101; the review's keyboard-5, the speech package's two
rounds, merged at `a60056e` and `950ca7d`): a pick by the pointer, a click in the gutter or a
right-click on the gutter or the text, also puts the caret, which is not drawn, on the picked run
and clears a selection (`BlameView.PutCaretOn`), without scrolling the text, so that the arrows go
on from the run the user picked and not from where the caret was left. The caret goes to the first
of the run's lines that is in view: the line drawn at the view's top, also when part of it lies
above, or the run's first line when that one lies lower (`FirstLineInView`, read from the lines'
heights, which a redraw leaves as they were). It stands at the view's left edge (`AtLeftEdge`):
column 1 while the text is not scrolled across; else the first caret position at or right of the
edge, its x kept for Up and Down (`Caret.DesiredXPos`), or, on a line that ends left of the edge
(an empty one, or `}`), the line's end, with an x half a space right of the edge kept, which
AvaloniaEdit rounds on the next line to the nearest caret position, one at or right of the edge in
the blame's monospace text. So the next arrow moves the caret one line from a line the user sees,
and AvaloniaEdit, which brings a moved caret into view, scrolls only when the line it reaches lies
out of view or ends left of the edge: from a run whose first line lies above the view the next Down
scrolls nothing, also from an empty line and when the pick clears a selection, and one Up from a run
whose first line is in view picks the run above. The first round put the caret on the run's first
line at column 1, which the next arrow scrolled up to when it lay above the view (ruling 100) and,
with the text scrolled across, back across (found in the second round). A click on the text leaves
the caret to AvaloniaEdit, on the line clicked, and a right-click on the text inside a selection
leaves the selection and the caret as they were. The tests, in `BlameViewTests` (App.Tests;
700 × 400, dark):
`A_click_in_the_gutter_puts_the_caret_on_its_runs_first_line_so_that_the_arrows_go_on_from_that_run`
(three runs, nothing scrolled: a click beside line 5 puts the caret on line 4, run 2's first, and
Up on line 3, run 1 picked); on two runs, lines 1 to 45 over 150 characters long and 46 to 85
short, `A_click_in_the_gutter_moves_no_text_however_far_its_runs_first_line_lies` (scrolled 60
across and down until line 46 is the last in view: a click on line 40 puts it on the line drawn at
the top, Down one line lower with nothing scrolled; a click on line 46 puts it on 46, and Up on 45,
run 0 picked),
`A_click_on_a_run_whose_first_line_shows_in_part_at_the_top_puts_the_caret_there_and_one_Up_picks_the_run_above`
(line 46 cut 6 px at the top: a click on line 50 puts it on 46, Down on 47 with nothing scrolled,
and after a click on line 52 Up on 45, run 0 picked),
`A_click_in_the_gutter_moves_no_text_across_when_the_runs_first_line_in_view_is_empty` (run 0's
even lines empty, scrolled 60 across with the empty line 20 at the top: a click on line 30 puts it
on 20, Down on 21 with nothing scrolled) and
`A_click_in_the_gutter_that_clears_a_selection_in_view_moves_no_text_across` (scrolled 60 across,
part of line 30 selected: a click on line 40 puts it on the line drawn at the top, the selection
gone, Down one line lower with nothing scrolled); in `KeyboardPassTests` (8a's project, a blame not
scrolled), `After_a_right_click_in_the_gutter_and_Esc_Up_picks_the_run_above_the_one_clicked` and
`A_right_click_on_the_text_inside_a_selection_keeps_the_selection_and_one_outside_it_takes_the_caret_to_the_run`.

**6.3.3 F3, Cancel.** The entry of 1.8. Its `run` keeps the title it was listed with and runs
`OperationStatus.CancelCommand` only if `CanExecute` still holds and `OperationStatus.RunningTitle`
still equals that title; otherwise it does nothing and logs Info
`Cancel {title}: that operation is no longer running.` (D452). `CancelCommand` cancels whatever runs
now (`OperationStatusViewModel.cs:201-202`): the titled fetch may have ended and an operation queued
behind it, or the merge tool, started. On screen: "Cancel Fetch origin", no key, the repository's
name as detail while dialogs name it (KD2). Afterwards the status bar reads "Fetch origin cancelled"
(`OperationTexts.Cancelled`). `AppShortcutsTests`' lists stay unchanged.

**6.3.4 F4, the stage rows** (D425). The styles at `WorkingChangesView.axaml:77-88`
become, in this order: `Panel.stageRow StackPanel.rowActions` hidden; shown on `:pointerover`,
`.selected` and `:focus-within` (new); `.folder` hidden; then
`Panel.stageRow:focus-within:not(:pointerover):not(.selected) StackPanel.rowActions Button` opacity 0;
last `Panel.stageRow StackPanel.rowActions Button:focus-visible` opacity 1.
- F6 still gives the row's own button (`KeyRegions.cs:237-243`): `k12b`'s state, nothing drawn.
- Tab from it: "Stage" then "Discard" (unstaged) or "Unstage" (staged), each drawn and framed, then
  the next row's own button, in the rows' order, rows below the view included (the lists' items
  panel, below). Shift+Tab reaches a row's own button, not its buttons. The panel is an overlay.
- After a row action started by keyboard (the button had `:focus-visible`), once the lists show its
  result, the focus is on the row now at its place in that list, else that list's last row, else the
  other list's first row, else the summary (`Tab`, 6.3.5). When the action leaves the tree clean (a
  Discard of the only change), the panel goes with the focused button
  (`RepositoryViewModel.cs:1418-1422`): the focus then goes to the graph (`GraphRows`, the main
  region's target), `Tab`. A Discard cancelled in its question gives the focus back to its row.

**Tab through a list's rows** (D466, ruling 101; step 19's second round, merged at `39c632f`). A
list that virtualizes makes only the rows near its view, reuses the controls of the others and adds
the control of a row it makes last, and Avalonia's Tab follows the order of the controls: an opened
folder's rows came last, rows below the view were never reached, and Tab into a scrolled list
landed in its middle. Wave 2 mended the stage lists alone, with a tunnel `KeyDown` in
`WorkingChangesView` (`55b8d3a`). Every list that virtualizes and whose rows hold Tab stops (its Tab
navigation `Continue`) now has one items panel, `RowOrderStackPanel`
(`Views/RowOrderStackPanel.cs`), a `VirtualizingStackPanel` that answers Avalonia's Tab navigation
itself (`ICustomKeyboardNavigation`): the left panel's `RefList`, the commit details' `FileList`,
the comparison's `CompareFileList` and the stage panel's `UnstagedFileList` and `StagedFileList`,
these two with `BackwardToRowStart`. For Tab and Shift+Tab alone:
- Inside a row the focus goes from stop to stop in the row's own order
  (`RowOrderStackPanel.StopsOf`: the row's elements that take the focus by Tab, shown and enabled:
  its button, then a branch's eye or solo button, a file's Stage and Discard or Unstage).
- From a row's last stop Tab goes to the next row's first, the row made and brought into view
  within the key press; from a row's first stop Shift+Tab goes to the previous row's last stop, in
  the stage lists to its own button (D425). A row without a stop (its button disabled) is passed
  over.
- Tab into the list from a stop before it reaches its first row, and Shift+Tab from a stop after it
  its last (the row's own button in the stage lists), the list scrolled there: Avalonia asks the
  panel when its own move lands in the list.
- Past the last row Tab, and before the first Shift+Tab, leave the list as Avalonia leaves it, to
  the stop after or before the list, except that a list of the same kind right beside it in the same
  region is entered at its end: the stage panel's two lists when no stop lies between them (Unstage
  all disabled, as it is while its own run goes on).
- A list whose Tab navigation is not `Continue` is left to Avalonia. F6, the arrows and Enter or
  Space on a row are not touched.

The lists that are one stop (the activity log's and the interactive rebase's rows, `Once`; the list
boxes of the file history, the reflog, the dashboard, the repository browser and the Pick dialog)
are not changed, nor the lists without a stop in their rows (the palette's, the multi-selection
list's) and the lists that do not virtualize, which insert each control at its item's index.
`WorkingChangesView`'s `RowAt`, which places the focus after a row's action, takes a row's stops
from `RowOrderStackPanel.StopsOf`. Tab visits every row before it leaves a list, a long list costing
as many presses; F6 leaves a region in one press (ruling 101). 6.3.9 has the tests.

**6.3.5 F5, the focus after "Choose"** (F5-A: D104's rule where the opener was disabled). New
`FocusReturn.AfterRun(IAsyncRelayCommand command, Control scope, Func<InputElement?> target,
NavigationMethod method, Func<IInputElement?, bool>? stillLost = null)`: when `command`'s current or next
run ends, at `Loaded` priority, if the focus is lost, `target()` takes it when shown and enabled. Lost:
nothing, the window or an element not shown has the focus, or one outside `scope`; with `stillLost`,
what that says of the focused element (the scope then serves only to find the window).
`FocusReturn.LostFrom(TopLevel? topLevel, InputElement pressed, Func<bool> stillThere)` is that test for
a pressed element: nothing, the window, an element not shown or not enabled, or `pressed` itself once
`stillThere()` is false. A focus the user moved meanwhile elsewhere stays.
- `WelcomeView`: `BrowseCommand`, scope `CloneForm`, `CloneFromGitHubButton`, `Unspecified`: after
  Choose or Esc in the browser, "From GitHub…" (or "From an account…") has the focus, with no frame;
  the palette's path keeps the focus where D104 put it.
- `CreatePullRequestDialogView`: `ChooseBaseCommand`, the dialog's view, `PullRequestBaseChooseButton`,
  `Unspecified` (ruling 51). If 7a's or 7b's part N pictures change, it comes out as a known issue.
- `WorkingChangesView`: the row actions (6.3.4), `Tab`, armed only when the pressed button had
  `:focus-visible`, and Amend (6.3.7), each with `LostFrom`: a row's pressed button keeps the focus
  while it still stands on its row (a failed action), while Amend itself counts as lost and is given
  the focus again; the target is null once the panel's `DataContext` is no longer the one the action
  was armed in (a switch of tabs), so the view does nothing then, and the window's rule below places
  a focus that the switch took away. Amend's return was armed only when Amend had `:focus-visible`;
  as built since step 19 (D462 (2), ruling 100) it is armed whenever Amend has the focus as a commit
  starts (`OnViewModelChanged`, at `IsCommitting`), `Tab` when Amend had the frame and `Unspecified`
  otherwise: the palette's Commit, the one path a user has to start a commit with Amend focused,
  reaches it without the frame, which D104 gives back without it.
`MainWindow.OnDialogRemoving` does not change.

As built since step 19 (D462, ruling 100; the focus package, merged at `c0bedc6`), the focus is also
kept, or given back, at these places, each only while the focus is lost, a focus the user or a view
placed meanwhile staying where it is:
- **A switch of tabs** (D462 (1); `MainWindow.Keys.cs`, `OnShellChanging`, `FocusAfterSwitch`). The
  window notes where the focus lies, outside a menu, as the active tab begins to change: its region,
  that region's panel and whether it had `:focus-visible`. Once the tab now shown is laid out and
  its views have asked for what they ask themselves (two posts at `Loaded`: a dashboard's list, an
  open diff's text), a focus that is then on nothing, the window or an element no longer shown or
  enabled goes to the same panel's target in that tab, as F6 would give it without its memory of
  the other tab's element (`KeyRegionNavigator.MarkedTargetIn`, D213), when that tab has a
  repository and shows that panel; else to the graph area's target (a welcome page's Open, the
  first-run screen's); `Tab` when the element that lost it had the frame. Nothing while a dialog is
  open, while the tab still opens (it has no graph area's target yet) or when the focus lay outside
  every region; a focus that the switch leaves shown (the graph, the summary of a stage panel still
  shown) stays. It acts on every change of the active tab.
- **A question cancelled** (D462 (2); `FocusReturn.AfterKeyboardPress(Button button,
  Func<InputElement?>? instead = null)`, new), at a button whose command may ask first and is
  disabled while it runs, so that D104's return finds it disabled. A press while the button has
  `:focus-visible` arms it, a click nothing; once the run has ended with the focus taken away, the
  button takes it back, `Tab`, while it still shows, enabled, with the data it was pressed for, and
  when the run leaves the button hidden, `instead()` takes it, `Unspecified`. At `DiffView`'s
  `DiscardFileButton` and `DiscardLinesButton` (a cancelled Discard lines clears the selection, as
  before, so its button goes and the diff's text takes the focus, `DiffTextView.BodyFocusTarget`),
  and at `WorkingChangesView`'s `StageAllButton` (its question on conflict markers),
  `DiscardAllButton` and `RestoreDiscardButton`.
- **A stage row's menu** (D462 (2); `WorkingChangesView.ArmRowMenuReturn`). A row's menu opened by
  the keys (Shift+F10 or the Menu key) while the row or one of its buttons has the focus arms a
  return: once the row has left its list (its Stage, Unstage or Discard chosen from the menu, or a
  later change that removes it), the focus goes where 6.3.4 sends it after the row's own buttons,
  the row now at the file's place first, with the frame if the focused element had it as the menu
  opened. The return is dropped when the focus moves to another element of the window's regions,
  when the panel shows another tab's lists and when another row's menu is opened so.
- **Amend**, as the `WorkingChangesView` item above has it.
- **An eye or solo button that hides itself** (D462 (3); `LeftPanelView.OnFocusLeavingList`): a
  focused eye or solo button that hides itself (its branch shown again, its solo ended) leaves the
  focus on nothing; once the layout has followed, the branch's row takes it, `Tab` when the button
  had the focus by Tab or the arrows, while the panel shows the same tab and the row still stands
  for that branch.
- **The kept rows** (D462 (3); `IUpdatableRow`, `RowCollection`; 1.17's "As built" on the rows): a
  section or folder of the left panel, and a tree folder of the stage lists, the commit's details or
  a comparison, opened or closed by Enter or Space (or a click), keeps its row and its control, and
  with them the focus, and its frame if it had one.

Since step 19 §5's Choose (`GitSetupView`) and the Git program row's Browse (`SettingsDialogView`)
call `AfterRun` too, after a cancelled pick or a refused file (5.3).

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
the summary keeps its focus and accent border on a Mod+Enter commit. Amend focused when the commit
starts gets the focus back once enabled, with the frame only if it had it (6.3.5; as built since
step 19, where it was Amend focused by keyboard). A failed commit keeps the boxes, as today.

**6.3.8 Read-only text views** (ruling 52). `AcceptsTab = false` on `DiffPane` and `BlameTextEditor`.
The activity log's output and the details panel are `SelectableTextBlock`s: a test asserts that Tab
leaves them, and only its failure changes `ActivityLogView.axaml`. No earlier check presses Tab inside
one (Grep of `Key.Tab`, `VirtualKeyShort.TAB` in `tests/`).

**6.3.9 The inventory for the report**, returned in the merge report. K a key; P the palette; M
Shift+F10 or Menu on a row that F6 or Tab reaches; T Tab or arrows. Unchanged: toolbar (K, P; theme
P, settings), tabs (K), open, clone, init (K, P), graph selection (F6, arrows, Shift), its menus (M),
left panel (Mod+Shift+E, arrows, M, P; its Tab changed, below), drags (their menus, ruling 11),
working changes' keys (K), a file's diff (T, Enter), diff close, history, blame (Esc, K), file
actions in the diff (M, P), pages (arrows, M, Esc), search (K), resolver (K, T bar to bar), rebase
list (Alt+arrows, p r s f d), conflict banner (Mod+Enter, P, T), activity log (K, F6, arrows, M),
dashboard (K, M), dialogs, settings (K, T). Changed or proven by 8a:

| Action | After 8a | Proof |
|---|---|---|
| Stage, unstage, discard a hunk | T to its buttons (F1) | K1; `DiffTextViewTests` |
| Inline or side by side | Shift+Tab from the text | K1 (a scripted-only step added to its method 1 after step 10: Tab until `DiffText` has the focus, Shift+Tab until `SideBySideDiffButton` has the focus, at most four presses each followed by a wait for the focused id, Enter: `SideBySideDiffButton` selected, no git change) |
| Stage or discard chosen lines | F6, Shift+arrows, then M or P | K1; `KeyboardPassTests` |
| A file row's buttons | T from the row (F4) | K4; `WorkingChangesTests` |
| Amend | T; disabled while committing (F7) | K7 (disabled while committing); `KeyboardPassTests` (focused by Tab, the focus given back); since step 19 the user's path, the palette's Commit, by `KeyboardPassTests.Amend_focused_when_a_commit_starts_from_the_palette_gets_the_focus_back` (the focus given back without the frame, D104; D462 (2)), the two before starting the commit from the code |
| Choose a blame run; its menu | arrows; M (F2) | K2; `BlameViewTests` |
| Cancel the running operation | P "Cancel {title}" (F3); a long T walk | K3, K8; `StatusBarTests` |
| Clone browser's Choose | T, arrows, Enter; the focus after (F5) | K5 |
| A pick dialog's choice; a choice or push dialog's option | T into the list, arrows, Enter on the selected row; Enter on the checked option (D454) | `KeyboardPassTests` |
| Resize a panel | T to its splitter, arrows by 10 | K8 |
| Switch tabs with the focus in a panel | K, P; since step 19 a focus the switch took away goes to the same panel's target in the tab now shown, else the graph area's, with the frame if it had it (6.3.5; D462 (1)) | `KeyboardPassTests`: `Ctrl_Tab_from_a_focused_stage_row_gives_the_focus_to_the_other_tabs_first_row_with_the_frame` and `Ctrl_Tab_from_a_stage_rows_Stage_reached_by_Tab_gives_the_focus_to_the_other_tabs_first_row_with_the_frame`, renamed and rewritten at step 19 (they were `…_leaves_the_focus_on_nothing_and_F6_then_starts_at_the_left_panel`), the graph's and the summary's two as they were, and the ten facts of `KeyboardPassTests.TabSwitch.cs` |
| A question cancelled after a press by the keys; Stage, Unstage or Discard from a stage row's menu opened by the keys | since step 19 the focus back on the button, with the frame (on the diff's text, without it, after Discard lines); the row now at the file's place (6.3.5; D462 (2)) | `KeyboardPassTests.FocusReturns.cs` (Amend's palette fact among them) |
| A section, folder or tree folder opened or closed by Enter or Space; an eye or solo button that hides itself | since step 19 the focus kept on its control, with the frame; the branch's row (6.3.5; D462 (3)) | `KeyboardPassTests.Toggles.cs` |
| Tab through a list's rows: the left panel's, a commit's or a comparison's changed files, the stage lists | since step 19's second round T in the rows' order, also after rows were inserted or the list scrolled, rows below the view included; Tab into a list reaches its first row, Shift+Tab its last (6.3.4; D466) | `KeyboardPassTests.RowOrder.cs`, eight facts: six walks, each from the stop before the list through every stop its rows hold to the stop after it and back with Shift+Tab, each stop framed (the left panel after a folder opened, a branch hidden and a branch made outside the app, and after a scroll down and up; a commit's and a comparison's tree after a folder closed and opened; the unstaged list after a file made outside the app came first; the two stage lists walked as one when no stop lies between them), and two guards, the activity log's and the interactive rebase's rows one stop after a row was inserted or moved |
| Commits not next to each other | the mouse only (ruling 50) | the guide |

**6.3.10 For the guide** (ruling 53), to §11: Tab in a diff reaches the hunks' buttons, Shift+Tab its
toolbar; its caret is not drawn, Shift with the arrows selects rows, then Shift+F10 "Stage selected
lines"; the arrows in a blame pick a run, Shift+F10 its menu; Tab from a stage row reaches its
buttons; "Cancel {title}" in the palette; a focused splitter moves 10 per arrow; commits apart need
Ctrl-click (Cmd on macOS).

### 6.4 Tests

Each class runs alone with plain `dotnet test` (D245). "Fails first": run on the branch's base before
the fix, the failure named in the commit message.

`HeldGitRunner` (1.15) holds the next call whose first argument equals its word: `diff-files`, an
unstaged file's diff (`GitRepository.Changes.cs:201`; a staged file's is `diff --cached`, an untracked
one's `diff --no-index`, a commit's `diff-tree` or `diff`), and `commit`
(`GitRepository.Writes.cs:219`). A tab's git calls take the detection's runner (1.2); if the
session's calls bypass it anyway, K6 and K7 keep their tests and drop their pictures.

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
  opacity 0. Four focus tests, one per case of 6.3.4's rule (each fails first): Enter on "Stage" of
  the first of three unstaged rows stages the file (git's index) and the focus is on the row now
  first; on the last of three, on the unstaged list's last row; on the only unstaged row, on the
  staged list's first row; the summary last. The app never shows the panel with both lists empty:
  they empty only with a clean tree, and then `ApplyStatus` deselects the working row and the panel
  goes (`RepositoryViewModel.cs:1418-1422`). So the fourth test hosts `WorkingChangesView` alone over
  `FakeHost`, whose `StageAsync` applies `WorkingTreeStatus.Clean` before it returns: the only row's
  own button focused with `NavigationMethod.Tab`, Tab, Enter on "Stage", and `CommitSummary` has the
  focus. A fifth, in the app (ShellDriver): Enter on "Discard" of the only change, confirmed, and
  `GraphRows` has the focus (6.3.4's clean tree; fails first).
- `StatusBarTests` (the held fetch, `:64-71`): the palette's first entry "Cancel Fetch origin", Enter
  gives `OperationOutcome.Cancelled` (fails first); none with nothing running, with a held local
  "Create branch hotfix", or once `CanCancel` is false; the entry taken during the held fetch, a held
  "Push main" queued behind it, the fetch released: Enter on "Cancel Fetch origin" cancels nothing,
  the push ends `OperationOutcome.Succeeded` once released, and `LogLines()` holds
  `Cancel Fetch origin: that operation is no longer running.` (D452; fails first); from F6 on the
  right panel at most 200 Tabs reach `OperationCancelButton` (G7, passes today).
- `KeyboardPassTests` (8a project, D416): Shift+Tab from the diff's text focuses
  `DiscardFileButton` (fails first); Tab, Enter on "Stage hunk" stages that hunk alone and leaves the
  focus on `DiffText` (fails first); F6 round the regions back to the diff's text, Ctrl+Home, Down
  past the first hunk to row 4 of the
  second (its header row 0), Shift+Down twice, Shift+F10, Enter on "Stage selected lines" stages that
  hunk's added empty line and `Negate` alone: `--numstat` `2 0` staged and `3 1` unstaged, the index's
  file `IndexAfterG1` (`Phase8aExpectations.K.StagedAfterG1`, `UnstagedAfterG1`; G1, passes today);
  a blame just opened answers Shift+F10 with run 0's menu
  (fails first); after Choose and after Esc in the repository browser (fake GitHub, as
  `WelcomeCloneFromGitHubTests`) `CloneFromGitHubButton` has the focus (fails first); after the create
  form's Pick, closed by Esc and by its choice, `PullRequestBaseChooseButton` has it (ruling 51; fails
  first: on the base the focus is on nothing after the Pick's Esc, the button disabled while
  `ChooseBaseCommand` ran); a held
  `diff-files` read (an unstaged file, `GitRepository.Changes.cs:201`) with Mod+Shift+M before its
  release leaves the focus in `CommitSummary`, and so does an
  `Empty` body turning to `Text` (fails first); a diff opened by Enter on a row still
  takes the focus (passes today; no menu of a stage row opens a diff), and a read held while the row's
  menu is opened by Shift+F10 and closed by Esc still gives the text the focus (6.9 item 6); with a
  held `commit` Amend is disabled and, focused by Tab before,
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
- **K1 hunk by keys** (real window: yes). Changes scenario, the working row; F6 gives the unstaged
  list's first row, and `src/Calculator.cs` is the seventh: F6 and then 18 Tabs along 6.3.4's order
  (each row's own button, its Stage, its Discard) to its row; Enter opens `src/Calculator.cs`'s unstaged
  diff. Tab: the first "Stage hunk" framed (`k1a-hunk-button`).
  Enter: that hunk alone in the index, focus in the text, no frame (`k1b-hunk-staged`). Shift+Tab:
  "Discard file" framed (`k1c-toolbar`). Tab back into the text, Ctrl+Home, four Downs and two
  Shift+Downs (rows 4 and 5 of the remaining hunk), Shift+F10: "Stage selected lines"
  in the menu (`k1d-lines-menu`); Enter stages two lines. Then, scripted only, 6.3.9's step for
  "Inline or side by side". The builder confirms the counts; `Phase8aExpectations.K` holds them.
- **K2 blame by arrows** (light; real window: yes). History scenario, a file of three runs or more:
  Mod+Shift+B, Down into run 2: its lines on the selection background, its commit in the right panel
  (`k2a-blame-run`); Shift+F10: its menu (`k2b-blame-menu`).
- **K3 Cancel in the palette**. A held fetch; Mod+Shift+P: first row "Cancel Fetch origin", no key
  (`k3a-palette-cancel`); Enter: "Fetch origin cancelled", no remote ref moved.
- **K4 a row's buttons by Tab**. F6 to the first unstaged row, nothing drawn on it; Tab: "Stage" drawn
  and framed (`k4a-row-stage`); Enter: the file staged, the next row focused (`k4b-row-staged`).
- **K5 the focus after Choose**. Fake GitHub signed in, the clone form, "From GitHub…" by Tab and
  Enter, a row, Enter: URL and name filled, the button focused, no frame (`k5a-chosen`; `b06a`'s form
  outside the URL's text). K5 starts as phase 7a's `StartEmptyAsync(data, served)` does, through a
  private helper in part K's own file; `Phase8aChecks.Support.cs` is not changed for it.
- **K6 the loading diff**. A held `diff-files` read (an unstaged file, `GitRepository.Changes.cs:201`):
  Enter on its row, Mod+Shift+M, release: the diff shows, the summary keeps the focus and its accent
  border (`k6a-summary-kept`).
- **K7 committing**. "First", Mod+Enter, the `commit` held: "Committing…", Amend disabled
  (`k7a-committing`); type "Next"; release: HEAD's subject "First", the summary "Next" (`k7b-committed`).
- **K8 the walks**. With K3's held fetch: F6 to the right panel, Tab until Cancel (the count in the
  report), Enter; then Tab to `LeftSplitter`, Right: the left panel 10 wider (`k8a-splitter`).

`Phase8aExpectations.K` holds, for §12 (12.3.4 launches 11 and 12), the picture names, K1's counts
of Tabs, Downs and Shift+Downs, K1's numstat after the hunk and after the two lines, and K2's file and
its count of Down to run 2.

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
`StatusBarTests` and 8a's project. Default run: about 30 tests in App.Tests (most on a host, about a
minute), 8 checks and about 14 tests in 8a's project (3 to 4 minutes alone). Real-window pass: two
launches, K1 on the changes scenario and K2 on the history scenario in light (D442; 12.3.4 launches
11 and 12), about 45 s each.

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

K5 answers 7a's report on where the focus returns; 7a's B6 and 7b's B7 stay as they are. 13.4 item 8
is settled: 6.3.4's rule, with a test per case (6.4).

## 7. icon: the icon's pipeline and a placeholder mark (R5)

**Read first (the lead, 2026-11-20).** The owner chose the mark, Lens, and said yes to its masters
before this package starts: `docs/brand/icon/round-2/icon.svg`, `icon-small.svg` and
`icon-macos.svg`, with the outlined eye at the small sizes and the tile `#4C5BE0`. They are the
package's masters as they stand, copied into `assets/brand/`; no placeholder is written. Wherever
this section says "the placeholder", read the owner's mark: 7.3's placeholder drawing and its
`#6170F0` contrast figures are not built (the owner's blue is 3.0:1 on a dark desk and 4.9:1 on a
light one, white on it 5.4:1), and 7.8's step "the chosen one becomes the three masters" is done.
The checks are the report's part B, which describes the owner's mark.

### 7.1 What the package delivers

Status step 11; wave 2, `wip/p8a-icon`, after §4's merge (the notice window). R5, D407, D412;
rulings 3, 17; plan.md lines 764 ("Icons and branding") and 782 ("the icon in the window and the
taskbar"). Its decisions: D428 (ruling 17, D407), the masters, the tool on Svg.Skia 4.9.1 (5.x needs
SkiaSharp 4.x, which Avalonia 12.1.3 does not take), the committed files and the placeholder; D429,
the three masters and the sizes; D430, the files checked by format, size and the masters' hashes,
never by pixels; D447 (amends D429, keeps D407 as the owner wrote it), the `.icns` written by the
tool and the candidates' rounds kept in `docs/brand/icon/`.

- Three SVG masters in `assets/brand/` (new), holding a placeholder mark until the owner chooses, so
  that no package waits on the choice (ruling 17).
- `tools/VisualCommit.IconBuilder` (new), a C# console tool on Svg.Skia 4.9.1 that renders from the
  masters the `.ico`, the `.icns` (`assets/brand/generated/VisualCommit.icns`, D407, D447) and the
  `.iconset` it is made from, the PNG sizes for Linux, the two PNGs the app shows and a manifest. The
  generated files are committed. The tool is never shipped and never run by a test or by CI, which
  builds it with the solution.
- The executable's icon (`ApplicationIcon`) and the window icon of the main window and of §4's
  notice window, from one embedded `.ico`; `AppIcon`, the resource paths §8's About uses.
- `IconFilesTests` (its item 8, `iconutil` on the `.iconset` and the `.icns`, in CI's macOS job),
  `WindowIconTests`, the reader `IconFile`; part B's two checks and the outline of their real-window
  repeats.

Left to others: the owner's choice, its rounds in `docs/brand/icon/` and the regeneration (the lead,
7.8, D447); the README's "Name" note (7.3), CLAUDE.md's row, the slnx line, `architecture.md` (the
lead); About's image (§8); the real-window code (§12). Out of 8a (ruling 17, D399): macOS 26's
layered `.icon`, `Info.plist`, `.desktop` and hicolor tree, installers' images; 8b's bundle takes the
committed `.icns`.

### 7.2 Files

New:
- `assets/brand/icon.svg`, `icon-small.svg`, `icon-macos.svg` (7.3).
- `assets/brand/generated/VisualCommit.iconset/` (ten PNGs), `assets/brand/generated/VisualCommit.icns`
  (D447), `assets/brand/generated/png/icon-<N>.png` (eight), `assets/brand/generated/manifest.json`.
  `bin/` and `obj/` are git-ignored, so no output folder takes those names; `.gitattributes` already
  marks `*.png` and `*.ico` binary, and gains `*.icns` (below).
- `src/VisualCommit.App/Assets/Icon/VisualCommit.ico`, `VisualCommit-48.png`, `VisualCommit-96.png`.
- `src/VisualCommit.App/Services/AppIcon.cs` (1.6).
- `tools/VisualCommit.IconBuilder/VisualCommit.IconBuilder.csproj` (`Exe`, `IsPackable` false, no project
  reference; package references `Svg.Skia`, `SkiaSharp`, `SkiaSharp.NativeAssets.Linux`,
  `HarfBuzzSharp.NativeAssets.Linux`), `Program.cs`, `IconRenderer.cs`, `IcoWriter.cs`, `IcnsWriter.cs`
  (D447), `IconOutputs.cs` (7.3's lists, in one place).
- `tests/VisualCommit.Testing/IconFile.cs` (1.15), plain .NET, no Avalonia (the real-window project
  uses it); its lists, `IcnsTypes` among them, are the tests' copy of 7.3's.
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
- `.gitattributes`: `*.icns binary` after `*.ico binary` (D447).

Section 1 used: 1.1 (the tool's row), 1.11 (the notice window), 1.15 (`Phase8aChecks.Support.cs`,
`Phase8aExpectations.Common.cs`, the pattern of 0.3; `IconFile`'s row, with `IcnsTypes`, `ReadIcns`
and `RepositoryPath`).

### 7.3 Behaviour: the masters, the tool, the outputs

**The masters' rules** (any mark, the placeholder and the owner's choice alike; web-brand 1.2, 1.4,
1.5; R5):
- Paths and basic shapes, solid fills, at most simple linear gradients: every element in SVG's
  namespace and one of `svg`, `g`, `defs`, `title`, `desc`, `path`, `rect`, `circle`, `ellipse`, `line`,
  `polyline`, `polygon`, `linearGradient` and `stop` (the tool's `MasterRules` and `IconFilesTests`
  item 1 hold this list; any other element gets `holds a <{element}> element`). No `text`, `tspan`, `image`,
  `filter`, `script`, `style`, `foreignObject` or `pattern` element; no `href`, `xlink:href` or
  `url(…)` that points outside the file (anything but `#id`).
- `icon.svg` and `icon-macos.svg` have `viewBox="0 0 1024 1024"`; `icon-small.svg` has `viewBox="0 0
  16 16"`, its straight edges on whole units. A master's `width` and
  `height` are absent or exactly its viewBox's size, `1024`, or `16` for `icon-small.svg` (`16px` is
  refused): Svg.Skia draws the viewBox onto them, and the tool scales by the size over the viewBox's
  size, so another value would draw the mark smaller or stretched. `icon-small.svg` is drawn with
  fills alone: `IconFilesTests`' check of its straight edges refuses a `transform`, a `stroke` other
  than `none` and a `style` attribute in it, which that check does not follow.
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

**The outputs** (`IconOutputs`; D429, D447):

| File | Sizes | From | Format |
|---|---|---|---|
| `src/VisualCommit.App/Assets/Icon/VisualCommit.ico` | 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256 | `icon-small.svg` to 32, `icon.svg` above | 256 a PNG; the others 32-bit BGRA bitmaps, bottom-up, height doubled in the header, an AND mask of zeros |
| `…/Assets/Icon/VisualCommit-48.png`, `-96.png` | 48, 96 | `icon.svg` | PNG, RGBA, 8 bits |
| `assets/brand/generated/png/icon-<N>.png` | 16, 24, 32, 48, 64, 128, 256, 512 | as the `.ico` | PNG |
| `assets/brand/generated/VisualCommit.iconset/<name>.png` | `icon_16x16` 16, `icon_16x16@2x` 32, `icon_32x32` 32, `icon_32x32@2x` 64, `icon_128x128` 128, `icon_128x128@2x` 256, `icon_256x256` 256, `icon_256x256@2x` 512, `icon_512x512` 512, `icon_512x512@2x` 1024 | `icon-macos.svg` | PNG |
| `assets/brand/generated/VisualCommit.icns` | the `.iconset`'s ten, as ten entries in this order: `icp4` 16 (`icon_16x16`), `icp5` 32 (`icon_32x32`), `ic11` 32 (`icon_16x16@2x`), `ic12` 64 (`icon_32x32@2x`), `ic07` 128 (`icon_128x128`), `ic13` 256 (`icon_128x128@2x`), `ic08` 256 (`icon_256x256`), `ic14` 512 (`icon_256x256@2x`), `ic09` 512 (`icon_512x512`), `ic10` 1024 (`icon_512x512@2x`) | the `.iconset`'s PNGs (`icon-macos.svg`), no second render | big-endian: the magic `icns`, the file's length in 4 bytes; per entry its type (4 ASCII bytes), its length with the 8-byte header in 4 bytes, and the very bytes of the `.iconset` file named beside it (D447) |
| `assets/brand/generated/manifest.json` | | | below |

Linux's scalable icon is `icon.svg` itself (8b copies it); 8b's bundle takes the committed `.icns`.
If `iconutil` refuses the PNG data in `icp4` and `icp5` (7.4 item 8), a fixer drops those two
entries after §7's merge, when CI's macOS job shows the refusal (the builder's own run skips item 8,
and a `wip` branch is not pushed), so that macOS scales the 1x 16 and 32 from `ic11` and `ic12`, and
says so here, in `IconOutputs.IcnsEntries` and in `IconFile.IcnsTypes` and `IconFile.IcnsFiles`
(1.15, 1.17), and makes `IconFilesTests` item 8's read-back expect the eight names the `.icns` then
holds. The manifest lists the `.icns` with its entries'
sizes in order. The manifest has no time stamp, so a run with nothing changed writes the same bytes:

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
   viewBox, antialiased; PNG by SkiaSharp's encoder. `IcnsWriter` then puts the `.icns` together from
   the `.iconset`'s PNG bytes, as the table above says.
4. All is written to a temporary folder, then moved into place, the manifest last; a file in
   `assets/brand/generated/` or `src/VisualCommit.App/Assets/Icon/` that is not in the list is
   deleted, and every empty folder in them. Each file replaced or deleted is first copied aside, and a
   failure puts each back from its copy and deletes the new ones, so it leaves the committed files as
   they were (a folder deleted is not made again: git keeps none).
5. Per `.ico` size it prints the share of opaque pixels at 3:1 on `#202020` and on `#F3F3F3`, for the
   owner's rounds; nothing asserts it.

Its console texts (the app shows none of them):
- `Usage: dotnet run --project tools/VisualCommit.IconBuilder [-- --root <the repository's folder>]` (exit 2)
- `No VisualCommit.slnx above {folder}: give the repository's folder with --root.` (exit 2)
- `{path}: is missing.`, `{path}: is not well-formed XML: {message}`, `{path}: has the viewBox "{found}";
  a master needs "{expected}".`, `{path}: holds a <{element}> element, which a master may not use.`,
  `{path}: refers outside itself: {value}`, `{path}: has the width "{found}"; a master needs none or
  "{size}".` (the same with `height`), `{path}: could not be drawn: Svg.Skia made no picture of it.`
  (exit 1, nothing written)
- `{path}: could not be written: {message}`, `{path}: could not be deleted: {message}`, `{folder}: could
  not be read: {message}` (an owned folder), each followed by `{path}: could not be put back as it was:
  {message}` for a file it could not restore (exit 1: every file replaced or deleted is put back from its
  copy, every new one deleted, and no `Wrote` or `Deleted` line is printed)
- `Wrote {path} ({n} sizes: {sizes}).` (`1 size` for a file of one size), `Wrote {path}.` (the manifest),
  `Deleted {path}, which this tool does not write.` (a file in an owned folder; these lines are printed
  only once every step has succeeded), `{size} px: {a} % of the opaque
  pixels at 3:1 on #202020, {b} % on #F3F3F3.`, `Done. Commit assets/brand and src/VisualCommit.App/Assets/Icon.` (exit 0)

**The app** shows no new text; the title stays "VisualCommit"; no control gains a name (§9's sweep
sees nothing new). Avalonia hands the `.ico` to the platform (Windows' small and big icons; headless,
a stub). It is embedded three times (the apphost's and the DLL's Win32 resources, an Avalonia
resource), about 0.5 MB (three copies of the 162,197-byte `.ico` and the two PNGs; not measured on a
build); the build output gains no file (1.1).

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
7. The `.icns` (D447), read by `IconFile.ReadIcns`: it starts with `icns`; its length field equals the
   file's length; its entries are exactly `IconFile.IcnsTypes`, in order; each entry's data equals,
   byte for byte, the `.iconset` file 7.3's table names beside its type.
8. On macOS only (`Assert.SkipUnless(OperatingSystem.IsMacOS(), "iconutil exists only on macOS; CI's
   macOS job runs this (D447).")`, so the builder's run skips it and CI's macOS job runs it; 13.4
   item 9): `/usr/bin/iconutil -c icns` on the `.iconset` and `/usr/bin/iconutil -c iconset` on the
   committed `.icns`, each into a `TempDirectory` of its own. Each exits with 0, iconutil's standard
   error in the message otherwise; the first gives a file that starts with `icns`, the second exactly
   the ten `.iconset` names (eight if 7.3 drops `icp4` and `icp5`). Nothing is written into the
   repository. If iconutil refuses the PNG data in `icp4` and `icp5`, a fixer drops those two
   entries after the merge (7.3).

`WindowIconTests` (App.Tests, `[AvaloniaFact]`, no session):
1. `new MainWindow().Icon` is not null.
2. §4's notice window, made as §4's own tests make it, has an `Icon`.
3. `AssetLoader.Exists` for `AppIcon`'s three URIs; the PNGs decode at 48×48 and 96×96; `new
   WindowIcon(AssetLoader.Open(new Uri(AppIcon.WindowIconUri)))` does not throw.

`Phase8aExpectationTests.B`: `Phase8aExpectations.B`'s lists equal `IconFile`'s; the picture names
match 0.3's pattern; the sheet's rows fit 1100×700.

Red before the change: all of `IconFilesTests` (item 8 on macOS alone; elsewhere it skips) and
`WindowIconTests` (no files, no `Icon`); the builder writes `AppIcon` and the tests first, then the
masters and the tool's run. Earlier tests that change: none. No test reads a window's `Icon` or the
app's resources (the `.Icon` hits in `tests/` are the glyph control's); `ShellTests`,
`PanelViewTests` and `MainWindowCloseTests` make a `MainWindow` and pass unchanged.

### 7.5 Visual checks, part B (outline)

Written into the report just before the package (D244). `IconSheet` is a harness control in a
headless `Window` of 1100×700 with the app's resources, `RequestedThemeVariant` Dark or Light and
`VcWindowBackgroundBrush` behind. Row 1, headed `VisualCommit.ico`: its 14 frames, left 24, gap 12,
bottoms aligned, each an `Image` at its own pixel size (`Stretch="None"`, whole-pixel positions),
the size below, the bare number at 11 in SemiBold, centred under its image on a whole pixel, in
`VcTextSecondaryBrush`; the heading at 13 in `VcTextPrimaryBrush` (the report's open point 44). Row
2: `png/icon-16` to `icon-128`, then `VisualCommit.iconset`'s
`icon_16x16`, `icon_32x32` and `icon_128x128`. The sheet reads its files through
`IconFile.RepositoryPath` (1.15): the `.ico` at `src/VisualCommit.App/Assets/Icon/VisualCommit.ico`,
its frames by `IconFile.Read`, and row 2's PNGs under `assets/brand/generated/`, which are no Avalonia
resources. No scenario repository, no git.

| Id | Steps | Expected | Theme, size | Real window |
|---|---|---|---|---|
| B1 | Open the sheet; capture `b1-icon-sizes-dark` | Every frame at its own size, none scaled: each image's bounds equal its pixel size; its centre pixel equals the frame's composited over `#14161B` within 2 per channel; one pixel that every master leaves transparent, the top-left pixel of each drawn image, equals the row's background `#14161B`, so that the composition over the background is proven and not only the pupil at the centre; the gaps `#14161B`. Inspected: the same mark at every size, nothing cut off, its parts distinct at 16 and 20 in the `.ico`'s frames (the `.iconset`'s `icon_16x16`, macOS's picture for
screens that are not Retina, is soft in round 2 and is judged only as the same mark), the tile's edge holding on the dark background | dark, 1100×700 | yes |
| B2 | The same; `b2-icon-sizes-light` | The same over `#FFFFFF` | light, 1100×700 | yes |

The repeats (§12, in part A's launch, 0.6), whose captures have no headless twin and are compared
with none:
- **B1, dark, 1100×700.** (a) The built `VisualCommit.exe`'s icon asked at 64 pixels,
  `Icon.ExtractIcon(ExecutablePath, 0, 64)` (`ExecutableIconAt`), a frame the `.ico` holds at any
  scaling: its opaque pixels equal the `.ico`'s frame of the size it came at within 2 per channel
  (proves `ApplicationIcon`); the size asked and the size got go into run.txt. As built since step
  19 (ruling 100; `3e32b9b` on `wip/p8a-realwindow`, which merges with the part), where it was
  `Icon.ExtractAssociatedIcon` at its own size: that gives the icon at `SM_CXICON` for the test
  host's DPI, 56 at 175 %, a size the `.ico` lacks (reasoned, not measured: the laptop runs at
  200 %, where it gave 64). (b)
  `WM_GETICON` with `ICON_SMALL` and `ICON_BIG` both give an icon; their sizes and
  `GetSystemMetricsForDpi` of `SM_CXSMICON` and `SM_CXICON` at the window's DPI go into run.txt (the
  pair `ICON_SMALL` and `SM_CXSMICON` inspected, not asserted), and an
  icon whose size is one of the `.ico`'s equals that frame within 2. (c) The framed capture
  `b1-title-bar-dark-with-frame` and the title bar's left 64 × caption height, enlarged four times,
  `b1-title-bar-icon-dark`. Inspected: the mark, sharp, left of "VisualCommit".
- **B2, after a real click on Theme.** `b2-title-bar-light-with-frame`, `b2-title-bar-icon-light`; then
  the taskbar buttons whose names start with "VisualCommit", looked for from UI Automation's desktop
  root for 10 s until one shows on the display that holds the app's window
  (`RealApp.TaskbarButtons`; `RealApp.DisplayBounds`, `MonitorFromWindow` and `GetMonitorInfo`'s
  `rcMonitor`). run.txt notes that display and each button found, with its name, bounds, taskbar and
  whether it lies on that display; only those on it count. Exactly one: its bounds asserted, and it
  is captured by its bounds as `b2-taskbar-button` (and four times enlarged). Inspected: the mark,
  not .NET's default icon. None found (a hidden taskbar or no button), or found but none on that
  display: the method does not fail on it, run.txt has a "Not run" line of its own for each
  (`Phase8aExpectations.B.TaskbarButtonNotRun`, `Phase8aRealWindowPass.NoTaskbarButtonOnDisplay`),
  and the report's B2 says "Not run" for that step, which the gate then looks at by hand. More than
  one on that display: none is captured, each is noted, and the launch fails at its end, saying to
  close every other VisualCommit or unpin it. As built since step 19 (ruling 100; `e3c0884` and
  `82922f5` on `wip/p8a-realwindow`, which merges with the part), where the first button found was
  captured: a button does not say whose process it is, so another running or pinned VisualCommit's
  could have been captured as this launch's, and Windows shows a window's button on every display's
  taskbar by default, so a count over every taskbar would fail with no other VisualCommit running
  (reasoned, not measured: the laptop has one display).

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
the gate the three captures differ from 7b's in the title bar (§7) and in the `FilterBox` placeholder (§2, 1.5). 8a's own pictures (B1, B2,
their repeats, §8's About) follow the mark, change with the owner's choice and are inspected again;
their expectations name no shape.

### 7.7 Cost

- Lagged list: nothing (no git, no watcher).
- Default run: two App.Tests classes, about a second; part B's two checks, about 5 to 10 s each alone.
- CI: the tool's restore and build in each of the six jobs, about 10 to 30 s (not measured); in the
  macOS job `IconFilesTests` item 8's two `iconutil` calls, about a second (not measured).
- Real-window pass: B's steps in A's launch, about 30 to 45 s.

### 7.8 Done when, and the owner's choice

The lead checks at the merge:
1. The tests of 7.4 were red before and are green; part B passes and B1 and B2 are inspected;
   `ShellTests`, `PanelViewTests` and `MainWindowCloseTests` pass unchanged.
2. A second run of the tool with nothing changed leaves `git status` clean.
3. `dotnet build` at the root has no warning; the merge's CI run is green in every job, the floor's
   included (the tool built everywhere, `ApplicationIcon` warning nowhere), and the macOS job's log
   shows `IconFilesTests` item 8 passed, not skipped.
4. `dotnet list tools/VisualCommit.IconBuilder package --include-transitive` shows Svg.Skia 4.9.1 and
   SkiaSharp 3.119.4; no `.deps.json` under `src/` names `Svg.Skia` or `ShimSkiaSharp`.
5. 7.6's proof holds.
6. The lead's own: the slnx line in a new `/tools/` folder; CLAUDE.md's row "Make the icon's files"
   (`dotnet run --project tools/VisualCommit.IconBuilder`, first run date); the "Name" note (7.3) and
   rows `assets/brand` and `tools/` in the README's layout table.

**The owner's choice** (status step 11): up to three rounds (D407, D447). Each round is committed in
`docs/brand/icon/round-<n>/`, docs only, so it starts no CI run and is no package's file
(`assets/brand/` is §7's, 0.4): each candidate an SVG of its own, the contact sheet `candidates.html`
with a ladder from 16 to 256 pixels on a dark and a light desk, and a `README.md` with the date, what
the owner chose and what the owner asked to change, the record of the human part that D407 names.
The lead committed round 1 on 2026-11-19 (`306f61f`), before the owner's first letter: `a-lens.svg`,
`b-merge-v.svg`, `c-check.svg`, `d-ring.svg`, `e-viewfinder.svg`; each later round adds
`round-<n>/`. The owner chose A, Lens, on 2026-11-20. Round 2 (`docs/brand/icon/round-2/`, the
same day) holds its three masters, drawn by 7.3's rules, with a runner-up for the small sizes, and
the owner said yes to it that day, with the outlined eye at the small sizes and the blue unchanged.
So §7's masters are round 2's `icon.svg`, `icon-small.svg` and `icon-macos.svg` as they stand, copied
into `assets/brand/`, and no placeholder is written: where this section says "the placeholder", read
the owner's mark (7.3's first comment is already in the files). Two
things round 2 found for §7: `icon-small.svg` fills its 16 grid, so that its edges fall on whole
pixels at 16 to 32, and is transparent at its rounded corners alone (its `rx` stays 3.42 or more, so
that the corner pixel at 16 is clear, 7.4 item 3); and round 2's pictures were rendered by Edge, so
§7's builder compares the tool's renders of `icon-small.svg` at 16, 20, 24, 30 and 32 with round 2's
`small-zoom.png` before the files are frozen, and reports each pixel that differs by more than a
tenth. B, D and E have a dark tile that leans on a thin edge on a dark desk, as B's note says,
which the tool's printout and B1 show. The chosen one becomes the three masters: the lead writes
them by 7.3's rules with the new first comment, runs the tool, `IconFilesTests`, `WindowIconTests`,
parts B and A, inspects, and commits "Icon: the owner's choice": code by the gate's rule, so before
the review before the gate (status step 19) where it can, before the counted round at the latest.
The look-alike board (web-brand 1.5) is the lead's, in the report's checks by hand.

### 7.9 Risks, unverified points, open points

Unverified, and the step that verifies each:
1. Svg.Skia 4.9.1's graph, read on NuGet, never restored here: the first restore; 7.8 item 4.
2. That Avalonia 12.1.3's Win32 icon takes the `.ico`'s bitmaps and PNG and picks a frame per DPI
   (PR 14564; its release not named): B1's repeat, (b) and (c).
3. That `HeadlessIconLoaderStub` (in Avalonia.Headless 12.1.3, body not read) takes the `.ico`:
   `WindowIconTests`, `ShellTests`.
4. That `ApplicationIcon` warns nowhere on macOS and Linux (warnings are errors): the merge's CI run.
5. That Windows 11's taskbar button is reachable through UI Automation: B2's repeat, else by hand.
6. X11's window icon, an unbundled app's Dock icon and `icon-macos.svg` against macOS 26's plate
   (web-verify B5): 8b's checklists.
7. That `iconutil` is on CI's macOS runner, converts the `.iconset` and reads back the tool's `.icns`,
   the PNG data in `icp4` and `icp5` included (web-verify B20): `IconFilesTests` item 8 in the merge's
   CI run (7.8 item 3).

Risks: the choice comes after the counted round; Skia's pixels differ between machines, so the files
are made on one machine and never compared by pixel (D430).

Open points: 13.4, items 10 and 11 (item 9, settled, is 7.4's item 8).

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
| `notices/Avalonia-NOTICE.md`, `…/CommunityToolkit.Mvvm-ThirdPartyNotices.txt`, `…/Onigwrap-THIRD-PARTY-NOTICES.txt`, `…/SkiaSharp-THIRD-PARTY-NOTICES.txt` (new) | byte for byte as each vendor ships it (the lead, at §8's merge): Avalonia's `NOTICE.md` at `8eeda4f6`; the files of CommunityToolkit.Mvvm 8.4.2, Onigwrap 1.0.11, SkiaSharp.NativeAssets.Win32 3.119.4; CommunityToolkit's and SkiaSharp's with CRLF, the other two with LF; the SHA-256 in git the vendors' own (`494981a`; the sources and the values in 1.17, "At wave 3's merges") |
| `.gitattributes` | `notices/** -text` (the lead, at §8's merge), so that git changes no line end in `notices/` on any machine; the app reads the files with `\r\n` made `\n` (1.13) |
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
`AppIdentity.FromAssembly(typeof(App).Assembly)`. As built, optional parameters follow:
`Func<string, string>? readText` and `Func<IReadOnlyList<string>>? vendorNotices` (null takes the
app's own, `EmbeddedTexts.Read` and `.VendorNotices`; a test makes a text unreadable through them),
`CancellationToken lifetime`, the settings screen's token, which Cancel, Esc and the session's end
cancel, and since step 19 a Save that closes the screen too (Save did not before): texts read after
it show no dialog; and, since step 19 (ruling 100; the firstrun package, merged at `ce144f4`),
`Func<Task?>? saving`, the screen's Save as it runs (`ConfirmCommand.ExecutionTask`). A press of
Show license… or Show third-party notices… whose texts are read while Save runs waits for that Save:
one that closes the screen shows nothing, and one that keeps it open (a refused field, a key still
decided by another file, a failed write) shows the text dialog over it afterwards, after the error
dialog a failed write shows. The Show buttons read the texts off the UI thread (Q2) and show the
dialog unawaited; a second press while the texts are read, or wait for Save, opens nothing more; a
text that cannot be read is an Error (`The text {name} could not be read.`) and shows no dialog.
The four commands allow concurrent runs, so their buttons stay enabled while they run: a disabled
button would lose the keyboard focus, which the dialog gives back to it when it closes. The links
call `OpenUrlAsync` with `AppLinks.Repository` and `AppLinks.Guide(GuidePage.Start)`, disabled
without a launcher; a failure is a Warning (`Opening the repository's page failed.`, `Opening the
user guide failed.`). About saves nothing. Down from Accounts reaches About; Down on About stays
(`SettingsDialogView.axaml.cs:111`).

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
is a Warning, `Copying the text to the clipboard failed.`; since step 19, `cef5fd2`, it allows
concurrent runs, so that "Copy all" stays enabled, and keeps the keyboard focus, while another
program holds the clipboard and the copy waits, a second press copying again), `ConfirmCommand`
(Close, true); Esc false.
The view on `dialogCard document`: the title (`DialogTitle`); 12 below, with several parts, a
`WrapPanel` of `VcFlatButton`s 28 high, 4 apart, the shown one `selected` (`TextDocumentSwitch`); 8
below, `DocumentTextView` (`TextDocumentText`); 20 below, "Copy all" (`TextDocumentCopyButton`, left)
and "Close" (`ConfirmButton`, `VcPrimaryButton`, right).

`DocumentTextView : CodeTextEditor` (new): read-only, wrapped, the code font at 11.5,
`Options.AcceptsTab = false` (ruling 52), `CutCopyWholeLine` and `EnableImeSupport` off, as
`DiffPane` has them (without a selection the copy would take the hidden caret's line, and nothing is
typed), no caret drawn (its brush transparent), `VcTextPrimaryBrush` on `VcControlBackgroundBrush`,
1 px `VcBorderBrush`, a padding of 8, 6, 2, 6, a selection in `VcSelectionBrush` with the text
keeping its colour; it takes the focus at opening, without the keyboard's frame. Its keys (the lead,
after §8's merge, 2026-11-21): Up and Down alone scroll a line, PageUp and PageDown alone the view
less a line; Home and End, alone or with the platform's command key (Ctrl; Cmd on a real Mac), go to
the start or the end and clear a selection, and after End the view keeps to the end while the
wrapped lines below are measured, so End shows the true last line, until the reader moves up (a
scroll whose offset goes up while the extent does not shrink and the viewport stays lets the end
go). The platform's copy (`HotkeyConfiguration.Copy`: the command key with C, and Ctrl+Insert)
copies a selection and nothing without one; its select-all (`HotkeyConfiguration.SelectAll`) selects
the whole text without scrolling; the view runs both itself (1.17). Enter is the dialog's: the text
takes none, so a plain Enter runs Close and the focus goes back to the opening button, and Enter
with a modifier does nothing; a held Enter on "Show license…" opens and closes the dialog in turn,
as any dialog with a default button that a button opens by Enter. Every other key that would move,
extend or use the hidden caret, or type, is taken and does nothing: typed text, Backspace, Delete,
Left and Right with any modifiers, Shift with any of the reading keys, and every other binding of
the editor's (Insert, Ctrl+D and Ctrl+I among them); cut, paste, undo and redo stay the editor's and
do nothing in the read-only text. Tab and Shift+Tab leave the text. Left as they are, by the lead:
PageUp and PageDown with Ctrl, Alt or Meta and no Shift, which Avalonia's scroll viewer takes and
pages by a whole viewport; Home and End clearing a selection made with the mouse. Its peer is a
Document, value the text, name the title, as `BlameTextEditor`'s. A switch shows the new text from
its top. Close, Esc and a plain Enter in the text give the focus back to the opening button
(`_focusBeforeDialog`).

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

As built: a component that is no NuGet package has no `Packages:` line: Inter, JetBrains Mono, Wuffs
and SPIRV-Cross have a `Files:` line alone, and Lucide and Feather, whose outlines are path data
inside `VisualCommit.dll`, have neither; the test checks a package's files on the entry whose
`Packages:` line names it. The copyright lines stand word for word, their holders' e-mail addresses
included (the lead, at §8's merge, 2026-11-21: the builder's brief's "no e-mail address" meant the
owner's address and About's own texts, 8.3.5, not third parties' notices);
`ThirdPartyNoticesTests.The_notices_files_only_addresses_are_its_copyright_lines` admits exactly the
six addresses those lines carry (Tmds.DBus's two, zlib's two authors, Oniguruma's and the yaml
grammar's), each inside a fenced block. "Keeping this file true" also says that guard: no line
outside a fenced block holds an at sign (its prose writes none), and the addresses are exactly those
on the test's list, which a new one has to join.

The entries (notices.md 2, 3): Avalonia (the 12 packages of 12.1.3 that ship,
`libAvaloniaNative.dylib`; `Avalonia.Angle.Windows.Natives` and `Avalonia.AvaloniaEdit` stand in
ANGLE's and AvaloniaEdit's entries, and `Avalonia.BuildServices` ships nothing); ANGLE in Avalonia's
build (with abseil-cpp, zlib, Chromium's compression utilities, xxHash, libc++); SkiaSharp and
HarfBuzzSharp, plus Wuffs and SPIRV-Cross, which their file omits; MicroCom.Runtime;
Tmds.DBus.Protocol; CommunityToolkit.Mvvm; AvaloniaEdit with AvalonEdit's holder; TextMateSharp;
Onigwrap with Oniguruma; TextMateSharp.Grammars 2.0.4 (a table of `grammars.tsv`'s 58 rows, VS
Code's MIT for the extension files, the 21 themes by name and origin); Inter 3.019 (OFL-1.1 with its
header); JetBrains Mono NL Regular 2.304 (two copyright lines: the first line of JetBrains Mono's
license file, `OFL.txt` at v2.304, and the font file's own, "The JetBrains Mono NL Project Authors",
as Inter's entry quotes its fonts' own line); Lucide and Feather (today's texts). Under
TextMateSharp, word for word (ruling 56): "Six themes in the package, Dracula, Atom One Dark, Atom
One Light, OneDark, Visual Studio Dark and Visual Studio Light, state no origin in the package or in
TextMateSharp's history, and TextMateSharp is a port of microsoft/vscode-textmate based on the Java
port eclipse/tm4e. They are listed here under TextMateSharp's own declared license, the MIT License,
which is all the package states for them."

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
  third line, `Creator` "Rahim Khajei"; `LICENSE` starts "MIT License"); and, as built,
  `The_notices_files_only_addresses_are_its_copyright_lines` (every line that holds an "@" lies
  inside a fenced block, and the addresses found are exactly the six on its list, 8.3.4). As built,
  NuGet's `_._` placeholder ships nothing, and a package's files are looked for on the entry whose
  `Packages:` line names it.
- `AboutSettingsViewModelTests` (`TestIdentity.Pinned`, the fakes): the texts; Show license pushes one
  dialog "MIT License" with `LICENSE`; Show notices five parts in order; the links give the fake
  launcher exactly `https://github.com/RahimPasha/VisualCommit` and `AppLinks.GuideBase`; no launcher,
  both disabled; a throwing launcher, a Warning; as built, a text that cannot be read, an Error and
  no dialog, and, from the review,
  `The_texts_are_read_off_the_callers_thread_and_a_second_press_meanwhile_opens_nothing_more` and
  `A_screen_closed_while_the_texts_are_read_shows_no_dialog`. Since step 19, in
  `SettingsDialogViewModelTests`, a press whose texts are read while Save runs (8.3.1):
  `Show_license_pressed_while_save_writes_opens_no_dialog_that_outlives_the_screen`,
  `Show_license_pressed_while_a_save_runs_that_keeps_the_screen_open_shows_its_dialog_once_after_the_save`
  and `Texts_still_read_when_save_closes_the_screen_show_no_dialog`.
- `TextDocumentDialogViewModelTests`: the first part shown; `ShowCommand` moves `IsShown`; Copy all puts
  the shown text whole; Close true, Esc false; one part, no switch; since step 19
  `Copy_all_stays_enabled_while_a_busy_clipboard_holds_the_copy`.
- `DocumentTextViewTests` ([AvaloniaFact], a window, no session): Down a line, PageDown a view less a
  line, End the notices' last line, Home the first (alone and with the platform's command key); Tab
  leaves; the peer is a Document with the text. As built also:
  `A_wheel_up_after_End_leaves_the_end_though_the_lines_it_brings_in_wrap`;
  `The_copy_key_copies_a_selection_and_nothing_without_one` (every `HotkeyConfiguration.Copy`
  gesture); `The_select_all_key_selects_the_whole_text_and_leaves_the_view_where_it_is`;
  `Keys_that_would_move_extend_or_use_the_hidden_caret_or_type_do_nothing` (after three PageDowns,
  and with a selection made by the mouse, each key held for three repeats);
  `Cut_paste_undo_and_redo_change_nothing_and_leave_the_view_where_it_is`;
  `Enter_with_or_without_modifiers_leaves_the_view_where_it_is_and_plain_Enter_goes_on_unhandled`
  (all 16 mixes of Shift, Ctrl, Alt and Meta); and the guard
  `Another_editor_still_takes_its_keys_line_breaks_typing_deleting_and_moving_its_caret` (a plain
  AvaloniaEdit editor made after a `DocumentTextView` keeps its keys).

**8a's project** (new, D416): `AboutSectionTests`: the section's texts are the pin's; Show license…
by Enter, the focus in the text, Esc gives it back; the notices switch shows Avalonia's file (from
its top, after End has reached the notices' end); the links reach the fake launcher, nothing else
launched; and, as built,
`Enter_in_the_licenses_text_closes_the_dialog_as_Close_does_and_gives_the_focus_back` (Enter with
Shift, Ctrl, Alt, Meta or Ctrl+Shift leaves the dialog open with the focus in the text; a plain
Enter closes it with true, the focus back on Show license… with its frame, the settings screen still
on About). `AboutEntryTests`: on a welcome, a repository and a dashboard tab the palette lists
"About VisualCommit…" once, right after "Follow the system's theme" (from §11's merge "Open the user
guide" follows it, D417), enabled, keyless, and opens About with the focus on `SettingsNavAbout`;
`NativeMenu.GetMenu(Application.Current)` holds one item, "About VisualCommit", no header holds
"Avalonia"; its `Command` with `App.Shell` set opens About, and does nothing while a dialog is open.

**Red before the change** (merge note): `ThirdPartyNoticesTests` on today's file; the lines below.

**Earlier tests that change** (each reason once in 8a's report, "Changes to expected results"); the
builder Greps for others (`SettingsNavAccounts`, `Follow the system's theme`, `Shown.Count`):
1. `SettingsDialogViewTests.cs:176-187`: Down from Accounts reaches `SettingsNavAbout`, Down stays,
   three Ups reach Tools; the comment names About last (D409).
2. `PaletteEntriesTests.cs:159`, `:340`: "About VisualCommit…" after "Follow the system's theme" (ruling 54).
3. K10 (ruling 54): `Phase6Expectations.K.cs`' `OpsCommands` and `DashboardCommands` end with
   `E("About VisualCommit…")`; `OpsCommandCount` 34, `DashboardCommandCount` 23, `WebAppCommandCount`
   35, `PageDownRows` [11, 22, 33], `FirstRowInViewAtEnd` 22, `WheelRow` 27; `Phase6ExpectationTests.K.cs:318`
   and the identity `PageDownRows[^1] == OpsCommandCount - 1`, added there as
   `Assert.Equal(PartK.OpsCommandCount - 1, PartK.PageDownRows[^1])` (33, the last of 34 rows).
   They follow from `:317-319`'s identities; K10's run confirms them. §11 moves them again, with a
   fourth PageDown (D452).
4. `Phase7aExpectations.A.cs:55, 58`, `Phase7bExpectations.A.cs:97, 99`: `NavButtons` and `NavIds`
   end with "About", `SettingsNavAbout` (D409); 7a's real-window A1 reads the linked file.
5. `ShellDriver.Phase6.Settings.cs:124-131`: `ChooseSection` gains About, never General's default.
6. The phase 6 report's "`Shown` 33 commands": the lead's, in 8a's report.
7. `CommandPaletteViewTests.cs:99-116` (`The_keys_move_the_highlight_into_view_and_typing_filters`, ops
   scenario): 34 commands; PageDown rows 11, 22, 33 (the comment drops "clamped": 33 is reached
   exactly); Up from row 0 wraps to 33, then 32 (ruling 54).

### 8.5 Part A's checks (outline, D244)

Dark, 1100×700, an empty tab, `settings.json` unwritten and nothing launched unless named. R: §12 repeats it.

| Id | Steps | Expected |
|---|---|---|
| A1 (R) | Ctrl+Shift+P, `about`, Enter; Tab ×3; Enter; Tab; Esc | One command row, no key. Six nav buttons 176 × 30, 2 apart, About at (141, 255), selected, focused; 8.3.1's rows with the pin; nothing clipped (`LayoutAudit`), no "@". `a1a-about-dark`. Tab ×3: Show license…, Show third-party notices…, then the repository link, underlined: `a1b-about-link-focus`. Enter records exactly the repository's address; Tab: the guide link; Esc closes |
| A2 | Light in the data folder; Settings, a click on About | A1's texts; the link `#4353D8`. `a2a-about-light` |
| A3 | 1920×1080; Ctrl+,, Down ×5, Enter | About at (551, 415). `a3a-about-1920` |
| A4 (R) | Show license…; Copy all; Esc | Over the dimmed screen: "MIT License", no switch, `LICENSE`'s 21 lines in view, Copy all left, Close right, the focus in the text. `a4a-license`. The clipboard holds `LICENSE` (`\n`); Esc: the focus on Show license… |
| A5 | Show third-party notices…; End; "Avalonia"; Close | Within 1 s: the title, five switch buttons in one row, the first selected, the file's first lines, `a5a-notices-top`; End: its last line at the bottom, `a5b-notices-end`; Avalonia's file from its top, `a5c-notices-avalonia`; Close: the focus on its button |
| A6 | `Scenarios.GraphAsync`; Ctrl+Shift+P, `about`; Enter | The one row highlighted, no key, no detail, `a6a-palette-about`; About opens; `ExactStateAsync()` unchanged |

Each method under about 40 s alone. `Phase8aExpectations.A.cs` holds the texts as literals, the nav
slots, the address and the names; `Phase8aExpectationTests.A.cs` asserts the names' pattern and no "@".
About's places are asserted within 2, as 7a's and 7b's section list is (Accounts at (140, 223) and
(550, 383)), and About lies exactly 32 below Accounts with the same left (the lead, 2026-11-21); so
0.7's and 8.6's region starts at x 140 (x 550). Method 1 takes A4 after A1's first Tab, which lands
on Show license…, and A1's walk goes on after Esc gives the focus back (part A's reading, the lead,
2026-11-21). As built, A5's bound of 1 s is asserted at the method's end, after its last step, so
that a slow opening still lets the later steps run and save their pictures; the time measured goes
to the test's output either way, and the expected result is unchanged. At §8's merge (the lead,
2026-11-21) the bound stays and is asserted wherever the check runs: it held in every run of the
lead and in all six jobs of CI's run 44969394972, and failed only in runs on the laptop while other
agents built and tested beside them (the builder's, 1,001 to 2,119 ms; a fixer's, 1,164 to 1,759
ms); no ruling comes until the whole default run or a CI job fails it (13.4 item 12).
For §12 (12.3.4 launch 3): A1 up to the Tab walk, not including it, by real keys (no link pressed),
under 3 % from `a1a-about-dark` (and `a3a-about-1920` where 1920×1080 fits), `AboutVersion`'s name
equal to the built `VisualCommit.dll`'s `ProductVersion` read as `AppVersionInfo.Parse` does and
ending with ` (`, seven hexadecimal digits and `)`: a Release build without a commit fails the launch,
saying so, and the lead rules then (the lead, 2026-11-21); then A4 by a real Enter on Show license…,
under 3 % from `a4a-license`, and Esc, "Copy all" never pressed.

### 8.6 Earlier pictures

Two changes, named by ruling 3 (rulings 14, 54; D409):
- The section list: `settings-list-pictures.tsv`'s 83 scripted pictures (phase 6 31, 7a 23, 7b 29) and
  8 captures (phase 6 `s14a-general`, `l31d-settings-light`; 7a `a01a`, `a02b`, `a05a`; 7b `a02d`,
  `a05b`, `a06b`) with their difference pictures, in x 140–317, y 255–285 at 1100×700 (x 550–727, y
  415–445 at 1920×1080; doubled at 200 %).
- The palette's thumb, x 837–838: `k10a-palette-commands`, `k10f-palette-names-repository`,
  `l31c-palette-dark`, `l31c-palette-light`; captures `k10a`, `l31c-palette-light`.

Nothing else: `VcLinkButton` is the sign-in theme moved unchanged (so at §8's merge; its keyboard
frame was rebuilt at wave 3's close, 1.17), the native menu draws nothing on Windows, no typed query
of an earlier check matches the command. Proof: the builder runs phase 6's part S, K10 and L31 and
7a's and 7b's part A (the sign-in dialogs among them), then `compare-pictures.py --expected` against
the references of the last closed wave (wave 2's from its close,
`D:/Projects/Personal/phase8-work/references/wave2/`; the lead, 2026-11-21) with these regions and
the six of 8a's own pictures that 0.7 names for §8 (`k3a-palette-cancel` and
`l3a-palette-placeholder` in the thumb, `l3f-settings-name-placeholder` and part F's
`f12a-row-next-start`, `f12b-row-too-old` and `f14a-netrc-note` in the About button's region): every
other picture the same file. Captures at the gate (0.7).

### 8.7 Lagged list, time, real-window pass

The lagged list gains nothing (K10, 7a's, 7b's and 8a's walk-throughs are in it; About reads no git).
Default run: App.Tests about 25 tests without a session, seconds; 8a's project part A about 3 minutes
alone, the two classes about one. The pass: A1 in the launch shared with part B, about 30 s.

### 8.8 Done when

- `ThirdPartyNoticesTests` green on the laptop and in CI's six jobs, shown red on the base first;
  the vendor files' sources and SHA-256 recorded at the merge (as built: in 1.17, "At wave 3's
  merges", a merge's message holding no such note).
- 8.3 built, 8.4's classes pass, 8.4's earlier changes made with their reasons drafted; no warning.
- Part A passes, each method under 40 s, its pictures inspected; 8.6's comparison as named.
- The merge's push ran CI green (the floor job as §3 leaves it); D433's doc lines are in.

### 8.9 Risks, unverified, open points

1. Unverified: `NativeMenuItem.Command` in Avalonia 12.1.3, and a `NativeMenu` on the `Application`
   under the headless platform. The builder reads the source first; without `Command`, `Click` calls a
   public `App.ShowAbout(MainWindowViewModel?)`, which the test calls. **Settled** by the build:
   `NativeMenuItem.Command` exists, and `NativeMenu.GetMenu(Application.Current)` holds the item under
   the headless platform (`AboutEntryTests`), so the item's command is `App.AboutCommand` and no
   public `App.ShowAbout` was needed.
2. Unverified: the macOS menu (read in Avalonia's source at `8eeda4f6`); 8b's Mac checklist. Still
   open: no Mac has run the app; 8b's checklist settles it.
3. Unverified until A5: the 1 s bound and End with wrapping. If either fails, the lead rules.
   **Settled:** End with wrapping shows the true last line (8.3.2: the view keeps to the end while the
   wrapped lines below are measured; A5's step 4 and
   `DocumentTextViewTests.End_shows_the_last_line_and_Home_the_first`); the bound stays as 8.5 and
   13.4 item 12 say, and the lead rules only if the whole default run or a CI job fails it.
4. Microsoft's file omits Wuffs and SPIRV-Cross (found by strings); others may hide. Their licenses
   (MIT and Apache-2.0; Apache-2.0 OR MIT) are read at the source before they are written.
   **Settled** for the two: each has an entry of its own, its license as read at the source, and
   its copyright line was checked against its source at §8's merge (on the default branch: the
   files at the shipped libraries' commits were not read). That others may hide stays a risk.
5. The macOS and Linux natives are proven only in CI's macOS and Linux jobs. **Settled** by CI's run
   44969394972: `ThirdPartyNoticesTests` passed in all six jobs, these natives among what it checks.
6. Unverified until a push changes only the notices: the `paths` order rule. Still open, until such
   a push.
7. Unverified: `ProductVersion` carrying `+<commit>` in the real window's build. Still open until
   §12's first run (12.3.4 launch 3).
8. Settled (1.17, 7.8): the owner chose the mark before §7 was built, so part A shows it from its
   first run; should the masters change again, part A's pictures change with part B's and are
   inspected again.

While the settings screen or any dialog is open, the macOS item does nothing, as Mod+, does. Open
point: 13.4, item 12.

## 9. uia: screen readers, names and the graph's row (D411)

Wave 3, after §5's and §6's merges; merges after §8; branch `wip/p8a-uia`, part U. Carries D411, D49,
rulings 3, 12, 32 to 39, plan.md's lines 759, 770, 784 and 786, D435 and D450. Facts are
uia.md's (Avalonia 12.1.3, FlaUI 5.0.0, the code at `e04762b`), checked again on `27c5109`.

### 9.1 What the package delivers, and what it leaves

Delivers 9.3's parts in their order, the spike first, with 9.4's tests and part U (9.5). Leaves: to
§11 the guide's pages; to §12 the real-window code; to the lead at the close `architecture.md`
(ruling 38: line 895's "one element" and the conventions at line 6224 rewritten for the List, its
row and the live regions), the Backlog's item status on the left panel's rows (ruling 36) and the
report's checks by hand. Out: a `[PrivateApi]` route but by a later decision (9.3.1); the window
peer's `GetFocus` (ruling 35); a peer per row; any tooltip, help text or key (ruling 34), but the
branch row's help text (D456 (3), 9.3.6); VoiceOver and Orca as tested (D411).

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
| S1 cold | Click "Main work", Down three times, nothing below the graph read | A LiveRegionChanged from `GraphSelectedRow` per move? |
| S2 warm | Read the graph's first child, Down three times | The same |
| S3 | F6 away and back | Focus on `GraphRows`; the row's event once |
| S4 | Click `feature/alpha` in the left panel | The row's event, the focus kept on `RefItem` |
| S5 | Ctrl+Shift+F (the local origin) | How many LiveRegionChanged from `OperationStatus` |
| S6 | Throughout | `FocusedAutomationId()` never `GraphSelectedRow` (ruling 35) |

The row's event, in S1 to S4, is a LiveRegionChanged from `GraphSelectedRow`: the focus is never on
the row, so Narrator speaks it only through that event, and a Name change alone counts as none, though
`events.txt` records it (D450). S1 yes: the route stands. S1 no, S2 yes: the lead decides (13.4 item
14). Both no: the build stops until a decision names `IEmbeddedRootProvider` (`[PrivateApi]`, 12.1.3)
and its guarding test (ruling 32). The events go into the merge notes.

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

1.12's table, each a constant of `AutomationNames` set as a local value, which beats
`VcIconButton`'s setter (`Theme/Controls.axaml:216`, kept as a fallback):
`AutomationProperties.Name="{x:Static cmd:AutomationNames.PreviousConflict}"`. The rows bind
formats: `StageFileFormat` `Stage {0}`, `DiscardFileFormat` `Discard {0}`, `UnstageFileFormat`
`Unstage {0}` over `ChangedFileRow.Key` (the path); `ShowRefFormat` `Show {0} in the graph`,
`StopSoloingRefFormat` `Stop soloing {0}` over the ref's full name (the string the row's tooltip
shows for a branch or a tag, `RefItemRow.ToolTip`, or a property of its own that holds the same),
not over `RefItemRow.Text`, as the menus word it: `Show feature/alpha in the graph`,
`Show origin/feature/alpha in the graph`, `Stop soloing feature/ff` (the lead, 2026-11-21); as
`{Binding Key, StringFormat={x:Static cmd:AutomationNames.StageFileFormat}}`, or a converter in
`AutomationNames` if the compiled binding refuses that. As built, the compiled binding takes
`StringFormat` with `x:Static`, so no converter exists, and the toggles bind
`{Binding ToolTip, StringFormat=…}` over `RefItemRow.ToolTip`. No tooltip, help text, key, template
or style changes, but one help text, §9's one exception to ruling 34 (D456 (3)): the branch row's
`RefItem` button binds `AutomationProperties.HelpText` to `RefItemRow.ToolTip`, the ref's full name
(`feature/alpha`, `origin/feature/alpha`), never `NameToolTip`, which reads "… is hidden in the
graph" for a hidden branch; no tooltip is added to the button, and nothing drawn changes. 9.3.8's
exemption rests on it, since the button had no help text of its own (9.9 item 7). No
`RefItemRow.ToolTip` is null: a folder row's help text is its path (`feature`, `origin/feature`), a
remote's row its name, a stash's row its message. The rows of `RefList`'s three other templates give
none: a section (`RefSectionRow`) has no tooltip, and an open pull request (`PullRequestRow`) and a
message of the Pull requests section (`PullRequestMessageRow`) have theirs on a part inside the
button, which the button's peer does not read (9.9 item 7). A control outside 1.12 that the sweep
reports is named only at the lead's word, from its area's texts class, never by a tooltip's change.
Since step 19 four buttons more carry a help text, the outcome after which they get the focus back,
and no name changes with it (D463 (3); 9.3.7).

#### 9.3.7 The status bar

`AutomationProperties.LiveSetting="Polite"` on `OperationStatus` (`StatusBarView.axaml:76-87`), one
property (ruling 36): a `TextBlock` until step 19, since then a `Controls/StatusText`, a subclass of
`TextBlock` (below). As a `TextBlock` its peer raised Name on each new `Text`, which the bridge
turns into LiveRegionChanged, and the running text holds git's percentage (9.9, risk 4).

As built since step 19 (D463 (1), ruling 100; the speech package, merged at `a60056e`), the live
region speaks what an operation says, not each step of its progress. `OperationStatus` is a
`Controls/StatusText` (new), a `TextBlock` styled as one (`StyleKeyOverride`), whose peer raises the
Name change only when its `SpokenText` changes, bound after `Text` to
`IStatusBarOperation.SpokenText` (`StatusBarView.axaml`): an operation's start, ssh's notice and its
result (`OperationStatusViewModel.SpokenText`, the running text without git's stage and percentage),
and on a dashboard a run's start, ssh's notice while it shows, Cancel all's text as it was when
clicked and the result (`BulkRun.SpokenText`, `DashboardBulkViewModel.SpokenText`), never the count
of rows done. The element's name is the text shown, percentage included, its control type Text and
its class `TextBlock`, as the real-window pass reads them; a new implementer of
`IStatusBarOperation` gives its `SpokenText`. A switch of tabs speaks the status of the tab now
shown when its spoken text differs from the one before, and nothing when the two are equal, as
before the fix (ruling 100).
`StatusBarTests.The_operation_texts_live_region_speaks_the_start_and_the_result_not_the_progress`
holds it: twenty progress values, two Name changes, the start and the result.

The first-run screen's title and error line (`GitSetupTitle`, `GitSetupError`) and the notice's
status (`InstanceNoticeStatus`) are polite live regions too, and nothing else of those screens:
"Check again"'s own text, `Checking…` while a search runs, is none (D463 (2); ruling 99's "nothing
else"). What a reader is told of them is bounded by Windows' bridge, which makes a peer's node, the
one that carries its events, only for a peer a client was handed (D455), while a control that is
not shown is no child of its parent's peer. The error line and the status show with their text in
one step, so no client holds them as they change: a client that has read the screen hears the title
when a search ends on another state, and the error line only if it reached the line during Choose's
`Checking…` (the refusal then); the notice's status, which shows with its words after the first
unanswered try and is not changed by a second, is found when a reader reads the notice, never told
as it comes. The tests take each peer as a client reaches it, walking down from the window's peer
(`ClientWalk`, 8a's project):
`GitSetupShellTests.The_title_is_a_polite_live_region_that_a_client_which_read_the_screen_hears_when_a_search_ends_on_another_state`,
`GitSetupShellTests.The_error_line_is_a_polite_live_region_that_a_client_which_reached_it_during_Chooses_check_hears_refuse_the_file`
and
`InstanceNoticeLiveRegionTests.The_status_is_a_polite_live_region_that_a_client_reaches_only_once_a_try_went_unanswered`.
So, as built since step 19 (D463 (3), ruling 101), the outcome is said where the focus lands: each
button that gets the keyboard focus back after such an outcome is described by it through
`AutomationProperties.HelpText`: the notice's Try again by the status line (`StatusLine`, empty
before an unanswered try), the first-run screen's Choose by the error line (`ErrorText`:
`Checking…` while its file is probed, then the refusal; also a failed start of winget or of the
Command Line Tools, 5.3) and its Check again by the title (`Title`, at all times), and the Git
program row's Browse by its status (`GitProgramViewModel.StatusText`: `Checking…` while its file
is probed, then the refusal; empty after a pass) (ruling 101: by the row's path and note when it has
no status, step 19's third round). In Avalonia 12.1.3 a peer's help text is UI Automation's
HelpText on Windows and `accessibilityHelp` on macOS, which VoiceOver speaks as a hint. The
window's peer raises a focus change at each change of the focused control, and Windows' bridge
makes the focused peer's node for it there and then (`RootAutomationNode`,
`AutomationNode.GetOrCreate`), so the description reaches a reader with the focus also when no
client had reached the button before, which a live region's change does not (D455).
`OutcomeAtFocusTests` (8a's project, four facts) records each focus event as the bridge takes it,
the focused peer's id, name and help text read at that moment (`ClientWalk.RecordFocus`), and reads
each button as a client reaches it: no help text before an outcome (Check again's is the title from
the start), `Checking…` while Choose's or Browse's file is held in its probe, and the focus back on
the button with the outcome as its help text, its name its own. What Narrator says is the gate's
listen (D460).

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
  A part may also be two key texts joined by " to " ("Ctrl+1 to Ctrl+8", the form in which the
  app's own key texts give a run of keys, `ShortcutText.FormatAll`): `AutomationSweep.IsKeyRange`,
  kept by the lead at §9's merge (D456 (4)); it only makes the sweep stricter.
- `duplicate`: two checked peers with one automation id and one name under the same items container
  (the nearest ancestor peer whose owner is an `ItemsControl`). As built (D456 (2)), the search for
  the container also ends at the root of the dialog the control lies in, the nearest peer whose
  owner is a `DialogView` (every view of the window's dialog layer is one): two dialogs that lie
  over each other, a sign-in dialog over the settings screen, each have a Cancel, told apart by
  their dialogs, while the same name twice in one dialog is still a duplicate, and a list inside a
  dialog is still the nearer container.

A scroll bar and what lies inside it (its thumb, its line and page buttons) are not checked: the
walk stops at a peer of control type ScrollBar, before its rules and its children (D456 (1)). A
scroll bar is a part of a scrolling control's template; a screen reader announces it by its type and
its value, and a reader scrolls with the keys of the control that holds it, so naming Fluent's
template parts would take a theme style over every scrolling control for a name nobody waits to
hear. The rule goes by the control type, so it also leaves out the graph's own `GraphScrollBar`,
which is no template part; it shows only while the graph overflows, and no swept state shows it.

The `duplicate` rule has one exemption (the lead, 2026-11-21): it does not apply to `RefItem` under
`RefList`. `AutomationSweep` holds that one exemption by the automation id, with its reason in a
comment: a row of the branch list is told apart by its place under its section and its folders, and
by its help text, the ref's full name (`feature/alpha`, `origin/feature/alpha`), which its button
sets from `RefItemRow.ToolTip` (D456 (3), 9.3.6). As first built the row's peer gave no help text:
the full name is the tooltip of the row's name text, `RefName`, not of its button, and Avalonia's
peer falls back only to its own control's tooltip (9.9 item 7); with the help text the reason holds
as written. The left panel as a tree with levels is the Backlog's (ruling 36). A `RefItem` row keeps
its automation name, the row's text (the leaf: `main`, `feature`, `alpha`), as every earlier test
and every real-window helper finds it (`RealApp.RefRow`, `Phase1Checks.Graph`): no earlier test,
helper or picture changes. The toggles inside the rows are unique by their full names (9.3.6). Any
other duplicate the sweep's first run finds is the lead's at §9's merge, the same way: that run
found, outside 1.12's table, only Fluent's scroll bars without a name and the Cancel of a sign-in
dialog over the settings screen's, which D456 (1) and (2) settle without a name.

`AutomationProblem` and `AssertNamed` as 1.12. `AssertNamed` throws the sweep's own
`AutomationSweepException`, since the Testing project references only `xunit.v3.extensibility.core`,
which has no `Assert`; `AccessibleNameSweepTests` gathers each state's problems through
`FindProblems` and fails once at its fact's end, with every problem state by state, so that one run
gives the whole list.

`AccessibleNameSweepTests`, one `[AvaloniaFact]` each, 14 as built, 13 with one start of the app
(state 14 shows §4's notice window alone, no app started): (1) the welcome page and the palette;
(2) settings, each section (About among them), the Git program row, Accounts
with the three providers' fakes started on ports the system picks (the section shows no sign-in
button while a provider is off) and each provider's sign-in dialog; (3) About, added at §9's merge
on the tree that holds §8 (D456 (5)): the settings screen on About, the license's text dialog (one
text, no switch) and the notices' dialog on Avalonia's file, reached by its switch, since the
license's dialog has no vendor file; each text dialog is closed before the next opens; (4) every
dialog kind through the dialog host (the remotes, a branch name, a tag, a stash, a reset, a pick, a
choice, a push, the finishing list, an error, a message, a workspace's name, the repository browser
over the three providers, the add-repositories checklist with two repositories found and the
create-request form with a fixed fake whose base is given), then a confirmation and git's six prompt
kinds through `PromptDialogPresenter`, as `PromptDialogTests` does, 22 states in all; (5) the ops
tab: two branches hidden, then one soloed, a commit with files and parent links, two unstaged and
one staged file with one row selected and the pointer over another, the diff's hunk bars, the
restore bar after a discard, the activity log, the graph's row, a context menu on the graph and on a
file; (6) search, compare, file history, blame, reflog; (7) the rebase list; (8) the resolver's card
(`image.png`) and the text resolver (`new.txt`, reached by its Next file) under the merge's banner,
on `ConflictsStop.MergeKinds`; (9) the dashboard and a row's menu; first run through
`FakeGitSetupProbe`: (10) Windows' Missing, then by "Check again" ChosenMissing, TooOld and
NotWorking with git's words, (11) macOS's Missing, (12) Linux's Missing, (13) HelperMissing
(`askPassHelper` a path without a file), in each state of (10) to (13) also `GitSetup`'s name the
state's title (`GitSetupTitle`'s text) and its help text the state's body (`GitSetupBody`'s text),
which §5 sets so that Narrator speaks them (9.3.9, steps 20 and 21); (14) §4's notice through
`InstanceNoticeHost` (1.11).

#### 9.3.9 Narrator by hand, and the guide

The walk-through (D411, the owner, about 20 minutes, at the gate) on one session of two tabs, a copy
of the ops scenario with README.md changed, active, and a stopped merge's copy second, started by the
lead's PowerShell line with CLAUDE.md's isolating variables and `VISUALCOMMIT_TIME_ZONE=UTC`, so that
the dates heard are 9.3.5's; steps 20 to 21a, last, by a second start, the lead's start line with
`VISUALCOMMIT_GIT_STAND_IN=none` beside the isolating variables, made after the first app has been
closed, since one instance runs per data folder (D421), so that the app finds no Git and shows the
first-run screen (1.14) (the lead, 2026-11-21). Narrator: Ctrl+Win+Enter. Each step: "as expected", or
the words heard; a step whose words differ is written down as heard.
1. The app in front: its title. 2. F6 to the graph: "Commit graph, list" alone while no row is
selected (9.3.2, 9.3.4). 3. Down: the next row's subject, author, date, refs; the first Down selects
the working-changes row, and each later step that assumed a selection is worded from the row the
keys really reach, as part U's hand check has them. 4. HEAD's row: "…, current branch main…".
5. Home, End, PageDown: each landing row, once: Home "Working changes"; End the last row, "Initial
commit…"; PageDown on the last row moves nothing, so nothing new is heard (9.3.4). 6. Up until "Main
work" is heard, then Shift+Down twice: "Alpha two…", then "Alpha one…"; is "3 commits selected"
heard? 7. Down: one row, no count. 8. Up to "Working changes". 9. Shift+F10: the menu's first item;
Esc: back. 10. A branch clicked in the left panel: its commit's row. 11. F6 to the right panel: the
first changed file's row, its name, "button". 12. Ctrl+Shift+M (the commit message), Shift+Tab to
README.md's row, then Tab: "Stage README.md, button". 13. The status bar's toggle: "Activity log,
button, Ctrl+J".
14. Ctrl+Shift+F: the start, `Fetching from all remotes…`, and the result, `Fetched from all
remotes`, are read, git's stages and percentages not (9.3.7, D463 (1)); is the start heard?
15. Ctrl+F, `alpha` (it matches Alpha one and Alpha two), Enter: the match's row, its name and ",
matches the search" if its hint is read; the focus stays in the box. 16. Ctrl+Shift+P, "fet":
entries with keys. 17. Ctrl+,, Down to About, Enter, then Narrator+W: the owner's name and the
version. 18. The resolver: "Next conflict, button, Alt+Down". 19. Narrator+Tab on the graph: "Commit
graph, list" (ruling 35). 20. The first-run screen shows: the state's title, "VisualCommit needs
Git", and its body. 21. Tab to "Check again", Enter: no "Checking…" is read while the search runs
(the button is disabled and without the focus then, and its text is no live region, 9.3.7; Narrator
may name the window as the focus leaves the button); when it ends (no Git found again) the focus is
given back, and Narrator reads "Check again, button" and, as its description, the title of the
state the search ended on, here "VisualCommit needs Git" (5.3; D463 (3): the button's help text is
the title at all times, so the Tab to it may already be heard with it).
21a. Shift+Tab to "Choose git.exe…" (the button before "Check again"), Enter, and in the Open
dialog a program that is no git (`C:\Windows\System32\whoami.exe`, which answers `--version` with
an error of its own): Choose runs it whatever the stand-in says and refuses it, `GitSetupError`
shows `This Git did not report its version. Git said: {first line}`, nothing is saved, the focus
comes back to "Choose git.exe…", and Narrator reads "Choose git.exe…, button" with the refusal as
its description (D463 (3)).
Narrator reads a help text as what its settings call advanced detail ("Hear advanced detail, like
help text, on buttons and other controls", whose default was not checked): when no description
follows "button" in steps 21 and 21a, how that setting stood is written down.
Steps 14 and 21 were reworded and step 21a added at step 19 (ruling 100), with the reasons in the
report: the live region spoke git's progress before (9.3.7), "Checking…" was never spoken, and no
step reached a refused Choose. The notice's Try again, described by its status in the same way,
has no step: an unanswered try needs a process that holds the data folder's `instance.lock` and
answers nothing, as part I's launch 5 has in its test process, and the walk-through stages none;
`InstanceNoticeLiveRegionTests` holds the status and the focus back on Try again, and
`OutcomeAtFocusTests` that focus event with the status as its description.
Silence where a row's name is expected (step 3's rows, step 5's Home and End): the live region does
not reach Narrator (9.9, risk 1). Step 2's lone "Commit graph, list" and step 5's PageDown on the last
row are no finding.

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
  As built, 22 tests; the "old null" of a row after none and of the focus's arrival is asserted after
  a name has been raised first (another row's before the time with none; the same row's before the
  focus comes back), so that a peer that always passed its last name would fail.
- `AutomationSweepTests` (App.Tests, hand-built windows, about 10): each rule's failing and passing
  examples, an invisible button not walked, the message's id and class. As built, 12, with D456's
  cases: an unnamed scroll bar and its thumb not checked while an unnamed button beside them is; the
  same name in two stacked dialogs no problem, twice in one dialog still one; and "Go to tab (Ctrl+1
  to Ctrl+8)" breaking the `key text` rule.
- `AccessibleNameSweepTests` (8a project): on the code before it reports at least `ActivityLogToggle`
  (type name), the three splitters (empty), the resolver's five and the search's three (key text), and
  duplicates of `RowStageButton`, `RowDiscardButton`, `RefHiddenToggle`. As built, on the code before
  the names 12 of its then 13 facts failed (all but state 14's notice, state 3 coming at the merge),
  each with these where its state shows them, and, outside 1.12's table, Fluent's scroll bars
  without a name and the stacked dialogs' Cancel (D456 (1), (2)); now all 14 facts report nothing.
- `StatusBarTests.The_operation_text_is_a_polite_live_region`: Polite; a new text, one Name change
  (since step 19, a new spoken text, here the start). Since step 19 (D463):
  `StatusBarTests.The_operation_texts_live_region_speaks_the_start_and_the_result_not_the_progress`
  (9.3.7); `OperationStatusViewModelTests` and `DashboardBulkViewModelTests` for `SpokenText`; the
  live regions as a client reaches them, `GitSetupShellTests` and `InstanceNoticeLiveRegionTests`
  over `ClientWalk` (8a's project; 9.3.7); since step 19's second round the buttons described by
  their outcome at the focus event, `OutcomeAtFocusTests` (8a's project, four facts; 9.3.7).
- `Phase8aExpectationTestsU` (8a project, 6 tests): part U's expectations against the app's
  constants and `GraphRowSpeech`.

Earlier tests that change: `CommitGraphControlTests.The_graph_is_one_ui_automation_element_with_its_name`
becomes `The_graph_is_a_ui_automation_list_with_its_name` and asserts `List` (D435: no longer
one element); its builders `Linear`, `Branch` and `Host` become `internal`, no assertion changed.
`KeyboardPassTests.ReadmesStageFact` (8a's project, `1355a20`) becomes `Button RowStageButton "Stage
README.md" of README.md in UnstagedFileList [Right], framed`, since `FocusFact` reads the automation
name that 1.12's `StageFileFormat` sets. No other earlier test: `ShellToolTipTests` reads tooltips,
help texts and keys, `HideAndSoloTests` tooltips;
`CommitKeyTests`, `RegionKeysTests`, `CommandPaletteViewTests` and `Phase6RealWindowPass.K.cs:224` the
focus on `GraphRows`, which stays (ruling 35).

### 9.5 Part U's checks, in outline

Dark, 1100×700, the ops scenario with README.md changed unless named; the window's peer made through
`ShellDriver.Phase8a.Uia.cs`.
- **U1, the graph's row** (real window: yes). A click on `Local branches › main`, then F6, "Main
  work" selected: a picture, the peer made, the same picture again, same bytes
  (`u1a-graph-row-head`); Down; Up; Shift+Down twice (`u1b-graph-rows-three-selected`); six Ups
  through the stash to the working-changes row (`u1c-graph-working-row`); a branch clicked in the
  left panel. Each step: one child `GraphSelectedRow`, ListItem, 9.3.5's name and hint, one Name
  change per move, the focus on `GraphRows` (on `RefItem` after the click). The order is the lead's
  (2026-11-21), as part U has it: from the working-changes row or a stash, Shift with an arrow has
  no anchor and moves as without (phase 4's 4h), so three rows are selected from "Main work" first.
  As built: the branch clicked is `alpha`, after a click on its folder `Local branches › feature`,
  which raises nothing; the first Name change after the peer is made has the old value null, each
  later one the name raised before. Method 2 does the same on the stopped merge with
  `u-untracked.txt` (launch 13's session): "Ours edits" clicked, then Up (`Working changes, 1
  conflict`, no hint), Down and Down ("Ours edits", "Kinds edits").
- **U2, the sweep** (real window: no). `AccessibleNameSweepTests` passes; the report lists the states
  and the first run's findings. One picture of the busiest state: the activity log, a selected and a
  hovered stage row, two hidden branches (`u2a-sweep-ops-tab`). As built, part U's methods also
  sweep with `AssertNamed`: method 3 the ops tab at `u2a`, after the solo at `u3b` and with the
  restore bar, method 2 the resolver.
- **U3, the names** (real window: yes). `Scenarios.ConflictsStoppedAsync(Merge)`: the resolver's five
  (`u3a-resolver-names`); the ops tab: "Stage README.md", "Discard README.md", "Unstage …", "Dismiss
  the restore offer", the search's three, the comparison's two, "Activity log", "Close the activity
  log", the splitters, "Show feature/alpha in the graph", "Stop soloing feature/ff"
  (`u3b-stage-row-names`); every tooltip as before. As built: in `u3a` the resolver spans the right
  panel's column (phase 4's design 11.2), so no stage panel shows beside it. The double-click on
  `notes.txt` leaves the keyboard focus in the resolver's result, where Esc is the resolver's own
  and closes nothing (phase 4's design 11.10), so method 2 presses Esc, asserts that the resolver
  stays, then clicks `ResolverCloseButton` and waits until the resolver has closed (the lead at §9's
  merge, 2026-11-21; `96d94bf`). Method 3, on "ops" with README.md changed, `src/core.txt` staged and
  `u-untracked.txt` untracked, also reads the comparison's two ends (`From main`, `To
  feature/conflict`, a comparison of branches), "Show origin/feature/alpha in the graph", "Unstage
  src/core.txt" and, after a discard of README.md, "Restore"; it asserts through each row's peer that
  the two `alpha` rows' help texts are `feature/alpha` and `origin/feature/alpha`, with no tooltip on
  the row's button (D456 (3)), and that the discard's backup ref holds README.md as it was (D67).

`Phase8aExpectations.U` holds U1's names and hints, and also, for §12's launch (12.3.4 launch 13), those
of `Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`'s working row and its next two rows and the
three names of an untracked `u-untracked.txt`, which §12 writes into that scenario (it takes no
picture, so no twin differs).
§12 repeats U1 and U3 in one launch (12.3.4 launch 13); its `run.txt` lists the names and every event
kind (D445), and each move must raise a LiveRegionChanged from `GraphSelectedRow`: a Name change alone
fails and is written to `run.txt` (D450). The launch reads the graph's first child before it records
the row's events (D455), and closes the resolver by a real click on `ResolverCloseButton`, as method
2 does (the lead at §9's merge, 2026-11-21).

### 9.6 Earlier pictures

None (ruling 3): names, live settings and peers take no part in layout or drawing. Proven by a grep
at the review (each `.axaml` hunk adds only `AutomationProperties.*` or an `xmlns` line; the graph's
diff has no `Render`, `AffectsRender`, `InvalidateVisual`, measure or arrange), by U1's same bytes, and
by `scripts/compare-pictures.py` against the references of the last closed wave (wave 2's from its
close, `D:/Projects/Personal/phase8-work/references/wave2/`; the lead, 2026-11-21): nothing outside
what §8 changes (0.7's rows for §8: the About button's region and the palette's scroll thumb, with
0.7's six of 8a's own pictures), since §9 merges after §8.

### 9.7 Lagged list, run time, real-window pass

Lagged list: nothing new; 8a's project is its last line, and the App.Tests classes run no git. Default
run: App.Tests about 45 tests without a session (under a minute); 8a's project about 15 sweep starts
and U's three methods (3 to 5 minutes alone). Real-window pass: one launch, 45 to 60 s. The spike runs
once, outside the pass.

### 9.8 Done when

1. The spike's events are recorded; the route is the public one, or a decision names the private one.
2. 9.4's classes, `Phase8aChecksU` and `Phase8aExpectationTestsU` pass; each new class failed first.
3. 9.6's grep is clean; `IEmbeddedRootProvider`, `[PrivateApi]` and a window peer appear nowhere in
   `src/` without that decision; the build has no warning.
4. 9.6's comparison shows nothing of this package; the lagged list passes; About's states are swept.
5. §12 has the outline and `Phase8aExpectations.U`, the lead the Narrator steps, §11 the sentences.

### 9.9 Risks, the unverified and open points

1. A row's node exists only once a client has reached it (uia.md 2): S1 tests the cold case, steps 3
   and 5 Narrator. Silence found at the gate reopens the route: a decision and a gate round. The
   spike showed it (D455): cold, no event; warm, every move. Whether Narrator warms the row by
   itself is what the owner's listen shows, brought forward from §9's merge. Still open: at §9's
   merge (the lead, 2026-11-21) the cold case stays as D455 has it, and nothing is built for it: the
   builder found no public way in Avalonia 12.1.3 to make the row's node exist without a client, and
   an item status on the graph's list would change ruling 35's words and is the Backlog's (ruling
   36). The owner's listen (13.4 items 13 and 14), asked again at that merge, decides whether the
   route stands; silence brings a new decision before the review (D455).
2. Narrator may read a List's selection on focus, and the row twice (step 11's first F6). Open until
   the owner's listen and the Narrator walk-through at the gate.
3. Whether a live region's help text is spoken (the count, the search) is unverified (step 6); the
   details panel's "3 commits selected" says it too. Open until the walk-through at the gate.
4. `OperationStatus` may chatter through git's progress (S5, step 14). Open until the walk-through
   at the gate. **Settled** by step 19's first round (D463 (1), ruling 100; 9.3.7): the live region
   speaks an operation's start, ssh's notice and its result, not git's stages and percentages, as
   `StatusBarTests.The_operation_texts_live_region_speaks_the_start_and_the_result_not_the_progress`
   holds (two Name changes for twenty progress values); whether Narrator speaks the start and the
   result is step 14's listen at the gate.
5. `StringFormat` with `x:Static`, and the row's parent: the build and a test verify; the fallbacks
   are a converter and the parent Avalonia sets when it lists children. **Settled** by §9's build:
   the compiled binding takes `StringFormat` with `x:Static`, so no converter exists (9.3.6); the
   row's peer gives the graph's peer as its parent (`GetParentCore`), which
   `CommitGraphControlAutomationTests` asserts.
6. VoiceOver may announce a live Name change (uia.md 2), untried; Linux announces nothing (D411).
   Open, and not 8a's to try (D411).
7. Unverified until §9's first run of U3 (the lead, 2026-11-21): the peer's fallback from a control's
   tooltip to its help text, read in Avalonia 12.1.3's source (9.3.3), which no code has run yet.
   9.3.8's exemption rests on it (a `RefItem`'s full name is its tooltip), and so does the Narrator
   walk-through's step 11, where a file's path is heard only as its help text. **Settled** by §9's
   first run of U3: the peer gives a control's own tooltip as its help text where the control sets
   none (`CloseDiffButton`, `ActivityLogClose`, `DismissRestoreButton`, the rows' buttons and the
   eyes, each asserted through its peer), and a file row carries its tooltip on its own button, so
   step 11 holds. The branch row was the case it did not cover: its `RefItem` button has no tooltip
   of its own (the full name is the tooltip of its name text, `RefName`), so its peer gave no help
   text and the exemption lacked its reason. D456 (3) binds the button's help text to
   `RefItemRow.ToolTip` (9.3.6, 9.3.8), and part U's method 3 asserts it for both `alpha` rows.

The spike's branch `wip/p8a-uia-spike` is never pushed and is deleted after §9's merge, its events
in the merge notes. Open points: 13.4, items 13 to 16; item 16's two first-run steps are 9.3.9's
steps 20 and 21.

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

As built since step 19 (D465 (2), ruling 100), `SwapGraph` also keeps a scrolled graph's top commit
at the top: `RepositoryViewModel.TopRowAnchor` takes the commit the control draws at the top, at
the offset rounded to a whole pixel as the control draws it, and commits that come or go above it
move the offset by as many rows, its part of a row kept; at the top (an offset under half a pixel
included), with the working-changes row at the top, or when that commit left the graph, the offset
stays. It acts on every swap, a hide's or a solo's included, and the working-changes row's one-row
move (D60, `ApplyStatus`) is guarded by the same drawn offset (`bc467e3`, `53b9c77`). Neither
writes a line or calls git, and M5's and M6's graphs, never scrolled, are left where they were, so
what 10.3.4 measures is unchanged.

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
`Runs` is `PerformanceSeries.ParseRuns(Environment.GetEnvironmentVariable("VISUALCOMMIT_PERF_RUNS"))`:
the value read as an integer from 1 to 20, null when unset; any other value fails the fact.
`PerformanceSeriesTests` tests `ParseRuns` and never sets the variable: the real-window project runs
every class in one process (its `AssemblyInfo.cs` turns parallel runs off), so a variable set there
would make the gate's pass run the series. The class has no fixture that does work before the skip:
the gate's pass shows five skipped tests and writes nothing for them.

`PerformanceSeries` writes `artifacts/perf/phase-8a/series-<yyyyMMdd-HHmmss>-<commit>.txt` under the
`RepoRoot` metadata, `<commit>` from `git rev-parse --short=7 HEAD` there, with `-changes` when
`git status --porcelain` is not empty. Both reads run through a `GitRunner` whose environment is
`TempRepo.IsolatedEnvironment(home.Path)` (`home` a `TempDirectory` of its own, as in
`Phase7bRealWindowPass`'s plain git) plus `GIT_OPTIONAL_LOCKS=0`, as the app's reads have (D47):
the real-window project's process is not isolated (it calls no `GitIsolation.IsolateThisProcess`),
and a plain `git status` would read the machine's and the lead's global configuration and refresh
the index under `index.lock` in the lead's checkout.
The file's lines: a head line with the date, the commit, git's
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

As built (the lead, 2026-11-22; D457). The series was taken three times, five runs each: at 00:47
on `86ef5b0`, at 04:59 on `fcf8c1e` and at 05:53 on `1afb980`, between which only docs, the link
button's theme and its tests changed (the files in `artifacts/perf/phase-8a/`, copies in
`D:/Projects/Personal/phase8-work/wave3/perf-before/`, `perf-before-2/` and `perf-before-3/`). The
class took 7 min 8 s, 7 min 4 s and 7 min 20 s, or 549, 433 and 520 s with the lock and, in the
first and the third, the Release build that the command made first, against 10.7's 12 to 18
minutes. None was quiet throughout (D457 (1)): the first and the third ran M2 within two minutes of
that build, the third because a docs commit after the build changed the commit the build is
stamped with, and the second met Office's updater, an idle-triggered task of Windows' Task
Scheduler, as M3 began. So step 1 is taken as D457 (2) says from now on: the real-window project
built in Release on the very commit at least ten minutes before, nothing committed in between, the
Task Scheduler read for what is due in the next ten minutes and afterwards for what ran (CLAUDE.md's
row and note). Step 2's bounds are D457 (3)'s, in a commit of their own: M2 6,100 ms, from the
second series alone, because the other two ran M2 straight after a build; M4 370 MB after the load
and 440 MB at the peak after the reload; M5 4,600 ms; M6 8,800 ms; M7 630 ms, each of these from the
slowest of all fifteen runs; M3's stays unset until its change's "after" series and is written from
that one (13.4 item 17). If the unchanged build's M2 passes 6,100 ms in a series taken as D457 (2)
says, the bound was drawn too tight and is raised by a ruling with the numbers. Step 3's triggers
(D457 (4)): M4's did not fire; M5's and M6's fired in every series (the reload line's medians
against 2,000 ms), and so did M7's (against 250 ms), and those three go to plan.md's Backlog with
their numbers, Pull's reads with what reopens the row (a rested median of about 600 ms, or the
owner); M3's fired wherever a quiet M3 is set against the quiet M2 (the fastest of its fifteen
runs, 6,523 ms, above 1.25 times 4,621 ms, 5,776 ms; the class's own trigger line compares within
one series and read "not fired" in the first and the third only because their M2 was disturbed),
and it gets a change in wave 4, ruling 76 (13.1). That is not step 3's candidate, which breaks
the contract step 3 cites, now at `MainWindowViewModel.cs:171-176`, and goes to the Backlog.
Step 3's "unless a stall is seen" was looked at by the lead's probe, by hand and outside any
test (00:59; the script in `D:/Projects/Personal/phase8-work/wave3/reload-probe/`, its output
`wave3/reload-probe-run1.txt`): an empty commit made outside the app three times on a 100,000-commit
clone, and the window's thread asked an empty message every 15 ms; during the three reloads, of
3.0 to 3.4 s, the longest answer took 36 ms, idle 25 ms, so no stall was seen. Step 4 proves ruling
76's change: M2 and M3 with the two builds run alternately, five times each, taken as D457 (2)
says, and beside them the lead's probe of the start with several tabs (by hand, outside any test;
`D:/Projects/Personal/phase8-work/wave3/startup10-probe/`), the tab's own clock and the window's
answers beside nine large and beside nine small tabs. It is proven when the fix's M3 median is at
or under 1.25 times its M2 median while the base's is above, and the fix's M2 lies within the base's
range; if the alternation does not show it, the change is reverted and the row goes to the Backlog
with the numbers. Step 5's table is the report's section "The performance series", and plan.md's
risk 10 holds the medians.

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
operation id, so the lag of 0.4 cannot change the count. As built since step 19 (D465 (1), ruling
100), Pull's boolean read asks git a second time (`git config --type=bool --fixed-value --get
<key> <value>`) only when its first command fails and the value in effect can be a boolean; the
theory's set-ups and M7's scenario set no pull key, so neither reads a boolean, and neither the
budgets nor M7's reads change.

`PerformanceSeriesTests` (RealWindowTests, new, no window): the median of odd and even counts, the
range, the rounding (1,513 ms × 1.25 gives 1,900; 412 MB gives 520; M7's 37 ms gives 50),
`ParseRuns` (null, `5`, `0`, `x`), called with the value and never through the variable (10.3.5).

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
`python scripts/compare-pictures.py D:/Projects/Personal/phase8-work/references/wave2 artifacts/visual <folder>`
(the references of the last closed wave, wave 2's from its close; the lead, 2026-11-21) lists no
changed picture of phase 1; wave 3's whole run confirms it for every phase.

### 10.7 Lagged list, default run, real-window pass

Lagged list: nothing new; the lead runs 0.4's list at the merge. Default run: about 15 tests in
App.Tests, under a minute of its own (the timeline's in-process; four with real git). Real-window
pass: five skipped facts and four pure tests, well under a second, no launch. The series, outside
the gate: 12 to 18 minutes per run of the class at five runs (clones about a minute, M3 about 5
minutes), plus the rest; alternation after a change, ten runs at one run each, 40 to 60 minutes.
As taken on 2026-11-22 (10.3.6, as built), a run of the class at five runs took 7 min 4 s to 7 min
20 s, 433 s with the lock where the build was made beforehand and 549 and 520 s where the command
built first: about 8 minutes of the desktop (CLAUDE.md's row), not 12 to 18.

### 10.8 Done when

- The App.Tests classes of 10.4 pass on the branch and on the merge's CI run, the floor job's
  included (a Pull budget is the highest count among the six jobs; the lead raises it at the merge,
  with the reason, if the floor job counts more). `PerformanceSeriesTests`, in a project CI only
  builds, passes in the builder's run of its class alone (`dotnet test --project
  tests/VisualCommit.RealWindowTests -c Release --filter-class "*PerformanceSeriesTests"`, no window)
  and in the gate's pass.
- Phase 1's pictures unchanged (10.6); Phase1RealWindowPass's Q1 code untouched.
- At the merge, the owner told, `VISUALCOMMIT_PERF_RUNS=1` with 10.3.6's command once: every fact
  passes its guards, the file holds each line of 10.3.5, and M6's reload line follows the fetch.
  Without the variable the pass's run of the class shows five skips and writes no file.
- After wave 3: the "before" series recorded, the bounds committed, each trigger decided, the report's
  table filled; a fix package only after a ruling, proven by the alternated series. Done at wave 3's
  close (D457): the series recorded, taken three times (10.3.6, as built), each trigger decided and
  the report's table filled ("The performance series"); the bounds are the ones D457 gives, in a
  commit of their own. Waiting for ruling 76's package in wave 4: its alternated series with the
  lead's probe beside it, M3's bound from that series, and the table's "after" for M3.

### 10.9 Risks, what is unverified, open points

- Unverified: that `Process.StartTime` reads on macOS and Linux (the Warning covers it); whether
  git 2.56's auto maintenance writes a commit-graph in a clone with git's defaults (the clones'
  settings rule it out, the guard would show it); that M6's reload line follows the fetch in the
  real app (read from the code, :145 and `FetchPull.cs:61`). The first run at the merge verifies them.
  As run: at the first run (2026-11-21, on `4ca874a`) M6's reload line followed the fetch, its
  4,718 ms inside the Fetch operation's 8,158 ms, and every fact of that run and of the three series
  passed its guards, so no measured repository held a commit-graph; that `Process.StartTime` reads
  on macOS and Linux stays unverified, since the series runs on the laptop alone.
- M3 holds ten 100k histories at once; if the laptop pages, the time is the machine's, which the
  noted working set shows. Nine 100k clones are the worst case (ruling 65). M6 fetches from a local
  folder: its fetch span is no network's; the reload after it is the measure. As run: the working
  set after the ten loads was 780.6 to 811.1 MB in the three series, and the lead's probes of the
  start with several tabs (by hand, outside any test, the cases alternately, three of each, so
  indications; `D:/Projects/Personal/phase8-work/wave3/startup10-probe/`) put the delay before the
  active tab's refs are read, in the about eighty git processes that ten openings start at once, not
  in the nine large walks: it came beside nine tabs of 2,000 commits too (D457).
- Risk 10: five rested runs, the slowest × 1.25 as bound; the gate's pass runs Q1 alone, rested.
  Settled for the bounds by D457 (3): M2's from the take that D457 (2) describes, the second series,
  the others from all fifteen runs, which no build touched, and M3's from its change's "after"
  series. For the review before the gate (status step 19; D457's last paragraph): the series and
  the real-window pass start the app from Git Bash, whose PATH finds git itself
  (`ucrt64\bin\git.exe`), while the probes, like a user who starts the app from the desktop, found
  `cmd\git.exe`, Git for Windows' launcher, which starts git as a child; Q1 by the tab's clock was
  1,771 to 2,813 ms in the probes (1,970 ms in the probe of 00:59), against 1,400 to 1,827 ms in the
  last gates' passes.
- The first run at the merge takes the desktop for about 5 minutes: before the merge's lagged list,
  never beside it (ruling 23); it took 3 min 47 s, 295 s with the lock (2026-11-21). Open points:
  13.4, items 17 and 18, both settled.

## 11. guide: the user guide, its site, pictures and links (D408)

Key `guide`, wave 4, after §5, §6, §8 and §9. Binding: D408, D411; rulings 7, 8, 18, 19, 39, 44,
46, 50, 53, 60 to 64; plan 765, 786. Input: `research2/guide.md`. Its decisions: D439 (rulings 18,
60 to 64), mdBook 0.5.4 pinned, the pictures made in the workflow at scale 2 and not committed,
`GuideShots` outside the solution, deploys from `master` only (Zensical is in beta, and history does
not grow per regeneration); D440, two images per picture; D441, the links' check fails a page that
loads anything from another host; D451 (the design's review): the workflow's step "App's links", the
hand check's artifact, CSS and every loading link in the links' check, a read-only token, the demo's
owner `demo--alex`; D452, K10's fourth PageDown; and D417 (F1) and D434 (the guide's spelling).

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
`SettingsDialogViewTests.cs`, `PaletteEntriesTests.cs`, `CommandPaletteViewTests.cs` and WP0's
`AppLinksTests.cs`; in `tests/VisualCommit.VisualTests.Phase6/` `Phase6Expectations.S.cs`,
`Phase6ExpectationTests.S.cs`, `Phase6Checks.S.cs`, `Phase6Expectations.K.cs`,
`Phase6ExpectationTests.K.cs` and a comment in `Phase6Checks.K.cs`; a comment in
`tests/VisualCommit.RealWindowTests/Phase6RealWindowPass.K.cs` (11.4); `.gitignore`
(`docs/guide/book/`, `docs/guide/src/pictures/`); `.github/workflows/ci.yml`, whose step "Build the
opt-in projects" gains `dotnet build tests/VisualCommit.GuideShots --configuration Release`.

The lead's, at the merge: CLAUDE.md's Commands rows (the shots, the book, the links' check, the
guide workflow's runs), its docs map, and "What to know": "A push that changes only docs starts no
CI run; one that touches `docs/guide/**` starts the guide's workflow, which also runs `AppLinksTests`
and `GuideShortcutsPageTests`; read its run as CI's."; `VisualCommit.slnx`'s comment names GuideShots
as left out.

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
| `first-run.md` | Git 2.34.1 or newer (ruling 69); the five states and buttons in §5's words; `winget install --id Git.Git -e --source winget`; the Command Line Tools or `brew install git`; Linux as §5's table (a too-old Git: the distribution's newer Git, or git-scm.com's Linux page for other sources; the guide names no PPA); the status bar's Git box; tested with Git for Windows (ruling 46) | `first-run-` + `missing`, `missing-macos`, `missing-linux`, `too-old`, `not-working`, `winget-confirm`; `status-bar-git` |
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
| `troubleshooting/sign-in-and-ssh.md` | The dialogs; since step 19 (guide-2; ruling 7 as ruling 100 amends it) the section "An old OpenSSH does not ask through VisualCommit": "An OpenSSH older than 8.4 ignores VisualCommit's sign-in dialog. RHEL 8 ships 8.0, and the OpenSSH built into Windows can be older where you chose it.", then a bullet per system (on Linux and macOS it asks in the terminal VisualCommit was started from, if there is one: answer there, or cancel the operation in VisualCommit; on Windows no terminal shows its question, not even the one VisualCommit was started from, since VisualCommit runs git without a console window: cancel the operation if it waits) and the ways round it (update OpenSSH, load the key into an SSH agent before VisualCommit starts, or use the remote's HTTPS address; `ssh -V`, which may name another ssh than git's); the netrc paragraph, git-lfs on every version (ruling 8) | none |
| `troubleshooting/data-and-logs.md` | Data and log folders per system (macOS's as §4 settles it); the activity log; reporting (8b's channel) | none |
| `troubleshooting/large-repositories.md` | 100,000 commits tested, first rows in about 2 s (Q1), §10's numbers as indications, from the report's section "The performance series" (10.3.6 step 5); no writes for speed; `git commit-graph write`, `git maintenance start` (ruling 19) | none |
| Reference: `reference/shortcuts.md` | The generated tables between `<!-- shortcuts:start -->` and `<!-- shortcuts:end -->` | none |
| `reference/about.md` | The About section and how to open it; MIT License; the notices; "This site is built with mdBook (Mozilla Public License 2.0)" and its fonts' licences as the first build shows them; since step 19 (S19-12) the four libraries the built site ships, with their licences: highlight.js (BSD 3-Clause), clipboard.js, elasticlunr and mark.js (MIT) | `about-section` |

42 shots, 84 files, an estimated 13 to 21 MB per build.

**11.3.3 The site.** `book.toml`: title `VisualCommit user guide`, `src = "src"`, `language = "en"`;
`site-url = "/VisualCommit/"`, `default-theme = "light"`, `preferred-dark-theme = "navy"`,
`git-repository-url` the repository, `additional-css = ["guide.css"]` (at the book's root: mdBook
reads `theme/` as its theme folder); `[output.html.playground] runnable = false`. Search stays on; no
analytics, no font from another host (ruling 18). `guide.css`: `--content-max-width: 1000px`;
`html.light` and `html.rust` hide `img[src$="-dark.png"]`, `html.coal`, `html.navy` and `html.ayu`
hide `img[src$="-light.png"]`. A picture is two images, light first (D440):
`<img src="pictures/stage-hunk-light.png" srcset="pictures/stage-hunk-light.png 2x" alt="…">`, the
path relative to the page: `pictures/` from a page at the root of `src`, `../pictures/` from
`troubleshooting/` and `reference/`.

**11.3.4 `guide.yml`** (guide.md 2's outline). Triggers: `push` on `docs/guide/**`, the workflow,
`scripts/check-guide-links.py`, `src/**`, `tests/VisualCommit.Testing/**`,
`tests/VisualCommit.GuideShots/**`, `Directory.*.props`, `Directory.Build.targets`, `global.json`;
`workflow_dispatch`; no `pull_request`. `permissions: contents: read` at the workflow's top, and
checkout with `persist-credentials: false` (D451). Concurrency per ref, `master` never cancelled.
Job `build` (ubuntu-latest, 30 min): checkout@v7, setup-dotnet@v6; "App's links" (5 min) runs
`dotnet test --project tests/VisualCommit.App.Tests -c Release --filter-class "*AppLinksTests" --filter-class "*GuideShortcutsPageTests"`,
the two classes that read `docs/guide/src` (11.4), which ci.yml leaves out (D451); "Pictures" (15
min) makes `/home/demo`, owned by the runner, and runs
`dotnet test --project tests/VisualCommit.GuideShots -c Release` with
`VISUALCOMMIT_GUIDE_ROOT=/home/demo`, `VISUALCOMMIT_GUIDE_PICTURES=$GITHUB_WORKSPACE/docs/guide/src/pictures`;
"Book" downloads `mdbook-v0.5.4-x86_64-unknown-linux-gnu.tar.gz`, checks it with `sha256sum -c`, runs
`mdbook build docs/guide`, `python3 -I scripts/check-guide-links.py --self-test`, then the check on
`docs/guide/book`; on `master` `upload-pages-artifact@v5`, elsewhere `upload-artifact@v7` as
`guide-site`, 14 days. Job `deploy`: only on `master`, `pages: write` and `id-token: write` (the only
job that has them), environment `github-pages`, `configure-pages@v6`, `deploy-pages@v5`. mdBook
publishes no hash: the lead takes the archive's `sha256sum` once, for the package's first commit, and
dates it in the report. Pages' source is set to GitHub Actions at "Phase 8a accepted", before `master`
is pushed, so that the merge's run on `master` can deploy (11.5). After the gate commit,
`docs/guide/**` stays docs (0.4): a change to it is checked by this workflow's run, "App's links"
included, which the lead reads and records beside CI's in `status.md` (D451).

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
  tags `v1.0`, `v1.1`; one stash; `origin` `https://github.com/demo--alex/weather-station.git`, its refs
  written from a bare repository under `<root>/remotes`, one ahead and one behind, never fetched; two
  changed files and an image; its own `user.name` and `user.email`. `recipe-box` for the dashboard;
  conflict and rebase states on demand. Data folder `<root>/.config/VisualCommit`, fresh per start.
  Hosting shots: `FakeGitHub.InProcess`, `new HostingOptions(GitHubEndpoints.GitHubCom, () => fake.CreateHandler())`,
  login `demo--alex`, a name GitHub cannot assign (no two hyphens in a row) and `GitHubUrls`' owner
  pattern accepts (D451); any other login the fake serves, such as a request's author, takes the same
  form (`demo--sam`). No shot fetches, pulls, pushes or clones.
- `GuideCamera`: `public const double Scale = 2;` (1 is ruling 61's fallback). Each shot is an
  `[AvaloniaTheory]` over dark and light: a fresh `ShellDriver.Start`, then
  `Session.Shell.UseTheme(Dark or Light)` and its wait before the state is set up, so no switch closes
  a menu; `SetRenderScaling(Scale)`, `Settle`, `Capture`; a crop to the control's bounds times the
  scale plus 8 pixels (as built the 8 pixels stand only round a dialog's card or a menu, 11.10); `LeakGuard`; saved as `<name>-dark.png` or `-light.png`. First-run shots:
  `FakeGitSetupProbe`, a fixed `GitGuidance` per platform (ruling 48), git at
  `C:\Program Files\Git\cmd\git.exe` or `/usr/bin/git`.
- `LeakGuard`: fails the shot, saving nothing, when `VisibleTexts.In(window)` or a visible `TextBox`
  holds the user name (3 or more characters), the machine name, the temp path, the real home folder,
  the repository's path, `VisualCommit.Tests`, `Test Author`, an e-mail address not at
  `example.com`, or a `github.com/` address whose owner is not `demo--alex`, other than
  `github.com/RahimPasha/VisualCommit`, `AppLinks.Repository`'s host and path, which About shows
  (8.3.1; ruling 63, D451); it names the shot and the text.
- `GuideShotCatalogue.Names`: the 42 names of 11.3.2.

**11.3.6 `check-guide-links.py`.** Standard library, `python3 -I`. Over each `.html` under the folder
given: every relative `href`, `src` and `srcset` entry resolves to a file and every `#fragment` to an
`id` in its target; nothing that loads names another host or a protocol-relative URL (D441, D451):
`script`, `img` (its `src` and each `srcset` entry), `iframe`, `source`, `video` (with its `poster`),
`audio`, `track`, `object`, `embed`, a `link` whose `rel` is `stylesheet`, `icon`, `preload`,
`modulepreload` or `manifest`, and every `url(...)` in an inline `style`. Over each `.css` file under
the folder: every `url(...)` and `@import` resolves to a file of the site (a `data:` URL passes).
Links to outside pages pass. Findings as `<file>: <url>: <reason>`; exit 0 clean, 1 findings, 2
usage. `--self-test` checks one good and one bad case per rule, and one bad case per element, `rel`
and CSS form, in a temporary folder.

As widened by step 19's review (guide-1): the check reads every element's `href`, `xlink:href`, `src`,
`srcset`, `imagesrcset`, `poster`, `data`, `background`, `ping`, `action`, `formaction`, `cite` and
`longdesc`, a `meta` refresh's address, an `iframe`'s `srcdoc` as a page of its own (a `<base href>`
of its own on another host, or protocol-relative, fails as the page's would), every `url(...)` in
SVG's presentation attributes `cursor`, `mask`, `fill`, `stroke`, `filter`, `clip-path`,
`marker-start`, `marker-mid` and `marker-end`, and in CSS (a
`style` attribute or element, a `.css` file) every `url(...)`, `@import` and every string directly in
an `image-set(...)` or `-webkit-image-set(...)` (not `type()`'s), with CSS escapes decoded (`\75 rl(` is `url(`). Every address
loads except the ones a user goes to: an `a`'s or `area`'s `href` (an SVG `a`'s `xlink:href`), a
`form`'s `action` and a `button`'s or `input`'s `formaction`, the `cite` of a `blockquote`, `q`,
`del` or `ins`, an `img`'s or `iframe`'s `longdesc`, and a `link`'s `href` whose every `rel` is
`alternate`, `author`, `canonical`, `help`, `license` or `prev`
(so `next`, which Firefox prefetches, `prefetch`, `preconnect`, `dns-prefetch`, `prerender`, `apple-touch-icon`,
`mask-icon`, `search` and a `link` without `rel` load). A `data:` URL passes except where it would
be a document, a script or a style sheet: an `iframe`'s, `frame`'s, `embed`'s or `script`'s `src`,
an `object`'s `data`, a `link` to a style sheet, module or manifest, a refresh, an `@import`. An
element of class `playground` or `mdbook-runnable` fails, since mdBook's `book.js` fetches from
`play.rust-lang.org` for it. `xmlns` attributes are never read. The self-test has a case with a
file of the site, one with a host and one protocol-relative for each loading form, a failing case
for each `data:` document form, and passing cases for the navigation forms and inline SVG's
namespaces (227 cases; the built site of 2026-11-23 stays clean). The fixes' reader found four
forms more, and the package's fixer made them hold: the `next` link loads, a `srcdoc`'s own base
and SVG's presentation attributes are read, and an image set's `type()` is no address (245 cases;
the same site stays clean).

### 11.4 Tests

Fail first: `AppShortcutsTests`' F1 and Help, `GuideShortcutsPageTests`, AppLinksTests' pages.
- `AppLinksTests` (WP0's, extended): per `GuidePage`, its `.md` exists under `docs/guide/src`
  (`VisualRun.RepoRoot`), `SUMMARY.md` links it, its anchor stands once as `{ #anchor }`.
- `GuideShortcutsPageTests`: the marked region equals the Markdown made from
  `AppShortcuts.Groups(CommandKey.Ctrl)` and `(Cmd)`: per group `### {Title}`, a table
  `| Action | Windows and Linux | macOS |`, a note once when both agree, else each on its own line;
  on failure it prints the expected region.
- Earlier tests that change, each reason once in the report's "Changes to expected results":
  `AppShortcutsTests` (`OpenUserGuide` leaves the keyless list; the router table gains `("Open the
  user guide", true)`; `MacShortcutTable` gains Help with its note; a fact routes F1 on Ctrl and
  Cmd); `SettingsDialogViewTests.ShortcutTable` gains `new("Help", [new("Open the user guide",
  "F1")], null)`; `PaletteEntriesTests`' two lists end `"About VisualCommit…", "Open the user guide
  · F1"`; part S (ruling 62): `PartS.ShortcutGroups` gains Help, `LastGroup = "Help"`, 11 and 50
  become 12 and 51, `AssertLastGroupInView` takes the last group's row count, not 3; the fact
  `Phase6ExpectationTests.S.The_shortcut_list_has_eleven_groups_and_fifty_rows` is renamed
  `The_shortcut_list_has_twelve_groups_and_fifty_one_rows`, and the summaries that state eleven
  groups and 50 rows, or `Dialogs and menus` with its three rows, are corrected
  (`Phase6Checks.S.cs:341-342` and `:409`, `Phase6Expectations.S.cs:805`,
  `Phase6ExpectationTests.S.cs:359`, `SettingsDialogViewTests.cs:308` on `810913e`; ruling 83); K10
  (ruling 54, D452): from §8's 34, 23 and 35 to 35, 24 and 36, `PageDownRows` [11, 22, 33, 34] (a
  fourth PageDown: with rows 0 to 34 the third lands on 33, and Down must wrap from the last row),
  `FirstRowInViewAtEnd` 23, `WheelRow` 28 (the identities `Phase6ExpectationTests.K` asserts, with
  §8's `Assert.Equal(PartK.OpsCommandCount - 1, PartK.PageDownRows[^1])`), the new literal at
  `Phase6ExpectationTests.K.cs:318`, four PageDowns and their rows in the comments at
  `Phase6Checks.K.cs:97`, `Phase6RealWindowPass.K.cs:73` and `Phase6Expectations.K.cs:277` (both
  loops read the constant and change no more), and the summaries that state §8's numbers
  (`Phase6Expectations.K.cs:271-272`, `:283`, `:286` and `Phase6RealWindowPass.K.cs:23` on
  `810913e`; `PaletteEntriesTests.cs:147`, `:148` and `:211`; ruling 83), `OpsCommands` and
  `DashboardCommands` gain the row, and the report's K10 "PageDown three times" becomes four, with
  its list of rows, the PageDowns' rows, the wheel's row (28, `Open settings…`) and its `Shown`
  counts (35, 24, 36); `CommandPaletteViewTests.cs:99-116`
  (`The_keys_move_the_highlight_into_view_and_typing_filters`, ops scenario): 35 commands, PageDown
  rows 11, 22, 33 and 34 (a fourth PageDown for the same reason: `:111-112` presses Down and expects
  row 0), Up from row 0 wraps to 34, then 33 (ruling 54).
- What no check shows (ruling 82, 2026-11-23), as facts of a class of §11's own in App.Tests:
  without a launcher the command logs the Info line and opens nothing; a launcher that throws gives
  `Start`'s Error line; F1's route on a dashboard tab and on a welcome tab opens the start page
  (`FakeFileLauncher.Requests`). F1 with a menu open or during a drag is the router's rule for every
  binding: the builder reports whether the router's table in `AppShortcutsTests` runs F1 there, and
  adds the fact if it does not.
- GuideShots: `GuideCatalogueTests` (every picture a page names is in the catalogue and every name is
  used; each `<img>` has `alt`, light before dark, `srcset` "`<src>` 2x" exactly when `Scale` is 2);
  `LeakGuardTests` (one made-up text per rule caught; the demo's texts and About's
  `github.com/RahimPasha/VisualCommit` clean).

### 11.5 Part G's checks (outline)

Dark, 1100×700; G1 and G3 on the ops scenario, G2 on one empty tab for each of its two starts
(ruling 78: 5.3 shows the screen only on a tab with no repository); none in the real window, which
opens no browser.

| Id | Steps | Expected |
|---|---|---|
| G1 | Ctrl+Shift+P, `guide` (`g1a-palette-guide`); Enter; F1 on the graph; the settings screen open, F1; Keyboard shortcuts scrolled to the end (`g1b-shortcuts-help`) | One row `Open the user guide`, `F1`; Enter closes the palette; the launcher holds `https://rahimpasha.github.io/VisualCommit/` once, twice after F1; F1 over the settings screen adds nothing; the list ends with `Help`, its row whole in view; no git call, no write |
| G2 | Missing, ChosenMissing, TooOld and NotWorking from `FakeGitSetupProbe` in one start, the next by Check again; HelperMissing in a second start, `RealAt(…)` with `askPassHelper` a path without a file (1.10). In each state: Tab to `GitSetupGuideLink` (`g2a-first-run-guide-link` on Missing), Enter; F1 | Each Enter adds only its state's URL (`troubleshooting/git.html#git-missing`, `#git-chosen-missing`, `#git-too-old`, `#git-not-working`, `#helper-missing`); F1 the start page |
| G3 | The settings screen on About; Tab to the guide link (`g3a-about-guide-link`), Enter | The row's label reads `User guide`, the link `Open the user guide` (1.13); the launcher holds the start page once |

By hand (0.5), from the `guide-site` artifact of the gate commit, or of a `workflow_dispatch` run on
it (`gh run download <id> -n guide-site`, served by `python -m http.server`): every picture, Light
and Navy each showing their own; search finds "rebase"; no other host in the network panel; every
`SUMMARY.md` link opens. At "Phase 8a accepted" (ruling 64): before `master` is pushed, Pages' source
is set to GitHub Actions (the owner, or the lead through `gh api` at the owner's word); after the
push, the guide's run on `master` is read, and run again by `workflow_dispatch` if it ran before the
setting; the live address answers; the `github-pages` environment's branch rule is read, and if it
admits more than `master`, the owner's leave is asked to set it.

**As the report wrote part G (2026-11-23; rulings 77 to 85, 13.1).** "No git call" is read as none
made by the palette's row, F1 or a link: on the ops tab, whose watcher is live, each key is marked
and read with the watchers quiet (D280), the palette's opening held as phase 6's K10 holds it (its
worktree read once, and besides it only whole refreshes); on G2's empty tabs the plain count stands.
G2's first start writes `gitExecutable` before it, since `FakeGitAnswer.ChosenMissing` throws for a
lookup without a `ChosenPath` (1.17); its four states are the fake's answers, the not-working one
part F's broken configuration. The report adds from 11.3.1: F1 with the palette open, Shift+F1 and
Ctrl+F1 on the graph, and the palette's row on the first-run screen. G2 reads its link's frame,
Fluent's on a `VcFlatButton`, by a helper of part G's own and not by `LinkFrame`: against a capture
taken before the focus moved, no pixel differs inside the tight box of the link text's ink, and some
pixel differs in the button's bounds outside it, for both link texts (ruling 80). Where the focus is
after Enter on a first-run link is not part G's: the screen's launching buttons and links go on step
19's list (ruling 81). The hand check also follows the five first-run addresses to their sections in
the built site, and is the lead's, on the gate commit's `guide-site` (ruling 79).

### 11.6 Earlier pictures

As rulings 54 and 62 name: `k10a`, `k10f`, `l31c-palette` dark and light (the scroll thumb, x
837–838) and their 2 real-window twins at the gate, and 8a's own `k3a-palette-cancel` and
`l3a-palette-placeholder` in the same thumb (0.7; ruling 84); `s14f-shortcuts-end`, confirmed: the
list's end shows Help, so the section's column changes; `s14e-shortcuts-top` at most in the
section's scroll bar, which the builder measures. Proof: the builder runs, besides 11.4's classes,
only `--filter-method "*Phase6ChecksS.*" --filter-method "*Check_K10_*"` in Phase6 (one command, as
0.4 writes them) and `--filter-method "*Check_L31_overlays*"` in Phase6Look, never a whole project;
at the merge the lead runs Phase6, Phase6Look and 8a's project whole, one after the other, through
`scripts/heavy-run.sh` (D245), then `scripts/compare-pictures.py --expected` against the references
current at the merge, with the boxes of these pictures, `s14e`'s from the builder's measure (ruling
84); any other difference stops the merge. The guide's pictures are outside `artifacts/visual` and
in no list.

### 11.7 Lagged list, time, real window

Nothing new for the lagged list (8a's project is in it). Default run: part G, under a minute;
App.Tests, seconds. CI's Windows job: about a minute to build GuideShots. The guide workflow: 7 to 10
minutes per push of code (about 2 for App.Tests' build and its two classes), an estimate until its
first run. Real-window pass: nothing.

### 11.8 Done when

1. The 33 pages in `SUMMARY.md`'s order, each saying its row; those of rulings 7, 8, 19, 39, 46, 50,
   53 and D411 there.
2. 11.4's classes pass; part G passes, its four pictures inspected.
3. The lead's run of GuideShots on the laptop through `heavy-run.sh` passes, `LeakGuard` silent and
   every picture looked at; `compare-pictures.py` shows only 11.6's changes.
4. After the merge's push: the guide workflow green, its step "App's links" included, no deploy job
   run, `guide-site` looked at; CI's Windows job builds GuideShots.
5. The lead's lines of 11.2 are in.

### 11.9 Risks, unverified, open points

- Unverified, read from mdBook's sources: `{ #id }`, raw `<img>` with `srcset`, the copy of
  `pictures/`, the theme classes, a CSS file at the root, and that mdBook's own CSS names only files
  of the site (11.3.6). The builder's first local build verifies them, with the links' check (the
  lead first puts mdBook 0.5.4 for Windows, hashed, in `D:/Projects/Personal/phase8-work/tools/`).
- Scale 2 never ran here; the graph's half-pixel insets or the diff may look wrong. The builder looks
  at every picture of the first run; if wrong, `Scale = 1` and no `srcset`, before the counted round.
- `LeakGuard` sees no drawn text (graph, diff); that text is the demo's, and the gate looks by hand.
- The actions' majors and the environment's default protection are the research's reading; the first
  deploy, on `master`'s push at "Phase 8a accepted" with Pages' source set before it (11.5), verifies
  them, and a branch rule that admits more than `master` waits on the owner's leave. Until then the
  app's links answer "not found".
- Ruling 44: Git for Windows' own updater stays out of the guide; nobody confirmed it at its source.
- Open points: 13.4, items 19 to 21.

### 11.10 As built and merged (the lead, 2026-11-23)

**Built** (run `wf_7dddbd2e-deb`, three builders one after the other, five reviewers, a fixer; 18
commits, the last `982f5e5`). As 11.3 has it, with these departures. *The site*: `book.toml` turns
mdBook's print page off, because `print.html` rewrites a raw image's `src` for the root and leaves
its `srcset`, which then points outside the site, and the links' check finds it; `guide.css` hides
the other theme's picture by the theme class on `html`, verified on the built pages with 11.9's
other five points; `SUMMARY.md` has the introduction and "What VisualCommit does not do" as prefix
chapters; the shortcuts page writes a group's note once when both platforms share it and otherwise
each with its column's name. *The workflow*: as 11.3.4, its push trigger gaining
`tests/Directory.Build.props`, which names `RepoRoot`. *The shots*: `SetRenderScaling` right after
the theme's wait and before the state, since a scaling set later re-reads the pointer's last place
and hovered a row; a `DelegatingHandler` that adds `/api/v3` in front of the in-process fake of
GitHub, with GitLab and Azure DevOps given their endpoints and no handler, so the Accounts section
lists all three and nothing leaves the process; Git reported as 2.43.0 at its usual path in every
shot but the first-run ones; `LeakGuard`'s floor of three characters for the machine's name too;
files beyond 11.2's list in the project's own folder (`GuideRun.cs`, `GuideSteps.cs`,
`WeatherStationFiles.cs`, `GuideSettingsTests.cs`); `status-bar-git` without its tooltip, which
opens in a top level of its own; the demo's functions spelled the American way. *The tests*:
`AppLinksTests` counts an anchor only as `{ #id }` at the end of a heading, outside code and
comments; `GuideCatalogueTests` reads what an `<img>` loads, each dark image's own `srcset` too;
`GuideSettings` clears only the catalogue's files from a folder the variable names;
`ShortcutTextTests`' table gained F1's row.

**Checked statement by statement, and corrected** (ruling 94 and 95; runs `wf_be89944c-5a7`,
`wf_3220124e-9d8` and `wf_28644568-d81`; 17 commits, the last `bc24568`). The reviewers had sampled
the pages; the pass read all of them: 1,157 statements and 131 universal claims against the source,
84 pictures opened. Corrected: 73 statements and 11 texts that did not match their picture, 9 more
of the same kinds the fixers saw, 11 universal claims, and 9 points of the checkers; 14 framing
faults of pictures and two more the checkers found. *The frames, as built*: `GuideFrame.Of` and
`GuideFrame.Area` end at the control's or the area's own edges, and only `GuideFrame.Card`, a
dialog's card or a menu, keeps the 8 pixels (11.3.5 gave them to every frame, and a panel's frame
then showed a strip of its neighbour); a graph's picture ends at its last whole row; a menu that
would reach past the graph's area fails its shot; the welcome page and the clone form are framed
from the heading to their last control; the reflog and the drag menu are shot 1440 wide; the stash's
picture shows its changed file. *The demo*: `origin/main`'s own commit is made with `commit-tree`,
without a checkout, so that the reflog holds no helper branch; its SHA stays. Left as the app draws
it: the graph's widest dates touching the scroll bar, and the resolver's scroll bar across the
conflict's last line (ruling 95's list).

**Merged** (`57ad86e`; 11.8). On it, with no agent at work: 11.4's classes with `KeyRouterTests` and
`ShortcutTextTests`, 81 of 81; `VisualCommit.VisualTests.Phase6` whole, 133 of 133 in 12 min 13 s;
`.Phase6Look` whole, 19 of 19 in 2 min 36 s; `.Phase8a` whole with part G, 154 of 154 in 5 min 51 s;
the comparison with wave 3's references by `--expected` (the report's "§11's merge"): of the 287
pictures those three runs wrote, 253 are the references' files, 4 are part G's new ones, the 8
expected ones changed in their boxes (three of them also in a caret or an edge pixel), and 22 others
differ in the kinds that vary from run to run; none changed size. The lead's run of GuideShots
through `scripts/heavy-run.sh` with the root `C:\demo`: 119 of 119 in 6 min 13 s (441 s with the
lock), `LeakGuard` silent, and its 84 pictures are byte for byte the ones opened on the branch. The
book built and the links' check found nothing (its self-test 117 cases). The lead's lines of 11.2
are in CLAUDE.md and `VisualCommit.slnx`. On the push (`f0b4f8a`) CI's run 45306019513 is green in
all six jobs, its Windows job building GuideShots, and the guide workflow's first run, 45306019644,
is green: its step "App's links", the 119 tests of its pictures at `/home/demo`, the book and the
links' check, no deploy job run, and the lead looked at its `guide-site` (11.8 item 4; status.md's
CI section). The site's hand check stays the gate's (ruling 79).

## 12. realwindow: the real-window part for 8a's checks

Status step 18; wave 4, branch `wip/p8a-realwindow`, worktree `D:/Projects/Personal/vc-wt/p8a-realwindow`,
built on the Windows machine after §11's merge, reviewed in step 19 with the rest, merged last, in that
step, after the review's fixes. Carries plan.md's Visual test gate step 4, D33, D58, D224, D246, rulings 6, 23, 47,
59, 65, phase 7b's ruling 29 and the 7b handoff's "A real-window launch" pattern.

### 12.1 What the package delivers, and what it leaves

Delivers `Phase8aRealWindowPass`: 13 methods, one launch each, in `Phase8aOrder`, repeating every
step the report marks R (12.5), each capture compared with its scripted twin by the twin's name, at
most 3 % different (D33, D58); B's captures have no twin and are inspected. `RealApp.Phase8a.cs`,
`Phase8aOrderTests` and the csproj's 8a link and stand-in reference. At least one capture of every new
screen: the first-run screen and its confirm, the notice, About and its text dialog, the window's and
the taskbar's icon. Its decisions: D442, one launch per session, each holding exactly its twins' tabs
and starting theme, so that the 3 % (D33) measures rendering and not a tab strip; D443 as D448 amends
it, the notice compared with its folder line masked, once its height equals its twin's; D444, nothing
pressed that leaves the app; D445 as D450 amends it, the graph row's LiveRegionChanged; D446, F4's git;
D449 and D453, F1, F2 and F5 need winget on the gate's machine.

Leaves: the scripted checks and their expectations (§2, §4 to §9); `Phase8aPerformance` (§10), which
skips in the gate's pass (ruling 65); part G (no real window opens a browser); the checks by hand (0.5);
D246's two runs and the gate's pass (the lead, the owner told); CLAUDE.md's rows (the lead).

### 12.2 Files and contracts

New, all under `tests/VisualCommit.RealWindowTests/`:
- `Phase8aRealWindowPass.cs`: `Phase8aRun` (the fixture: `new RealWindowRun(Phase8aExpectations.Phase)`,
  `DisplayNoted`), `Phase8aOrder` (12.3.4's list), the class's limits (`LoadTimeout` 60 s, `StepTimeout`
  30 s), `Launch`, `Sized`, `Compare`, `CompareMasked`, `InkOf` (12.3.3),
  `AssertDesktopReady` (D224), `AssertNeverPressed` (12.3.2), `NoteLaunch`; and `Phase8aOrderTests`.
  `CompareMasked` (D443 as D448 amends it) first asserts that the capture's height divided by
  `Scaling` equals the twin's height within 1 px, else fails with `The notice is {h} high and its
  scripted twin {t}: their layouts differ, so the masked comparison cannot hold.`; then paints
  `InstanceNoticeFolder`'s UI Automation bounds, relative to the client area, one flat colour in the
  capture and, divided by `Scaling`, in the twin (a copy in the test's temporary folder; the scripted
  file is never changed); then compares at 3 %. `Phase8aExpectations.I` names `InstanceNoticeFolder`.
- `Phase8aRealWindowPass.L.cs`, `.I.cs`, `.F.cs`, `.K.cs`, `.AB.cs`, `.U.cs`: one file per part, A and
  B together, as they share one launch (§7.5).
- `RealApp.Phase8a.cs`: `ExecutablePath` (§7.5's accessor); `StandInPath`, the stand-in in
  `AppContext.BaseDirectory`, asserted to exist (`The stand-in for git is missing at {path}: build
  tests/VisualCommit.RealWindowTests, which references it.`); `StandInEnvironment(standIn, version,
  failure)`; `StartBeside` (the executable through `StartInfoFor`, its output drained); `Minimise`,
  `IsIconic`, `IsVisibleOnDesktop`, `ForegroundIsThis`, `WindowIcons`, `CaptureScreen`,
  `TaskbarButtons` and `DisplayBounds` (as built since step 19, ruling 100, in place of one
  `TaskbarButton`: every button named so, and the display that holds the window; 12.3.4 launch 3);
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

Uses 1.2's first-run ids, `ProgramSearch.FindOnPath` and `GitLocator.StandInVariable`, 1.6's
`AppVersionInfo`, 1.11's paths and ids, 1.12's names and `GraphSelectedRow`, 1.14's variables, 1.13's
About ids, 1.15's `Phase8aExpectations.Common`, `IconFile`, `GitStandIn` and each part's
`Phase8aExpectations.<Part>` (1.15's last paragraph). `RealApp.Executable` is private, hence
`ExecutablePath`.

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
folder" or "Open in editor". F2's step, after its Cancel, waits at most 5 s for no `ConfirmationDialog`
and fails before it sends any further key if one is still there; then it checks that the log has no
`Started in a terminal:` line and that `NativeMethods.Processes()` lists no `cmd.exe` or `winget.exe`
whose parent is the app (ruling 6). F13 asserts the focused element's id after each Tab and sends
Enter only while the focus is on `GitSetupCheckAgainButton`. Where the twin opens About from the
palette, the pass asserts that the highlighted row reads `About VisualCommit…` before it presses Enter.

#### 12.3.3 Ink in a capture

`InkOf` is `TextInk.Of`'s rule over the capture's pixels at `Scaling`, with `TextInk.Tolerance` and
the twin's "replaced" colour. Its rectangle is the element's UI Automation bounds inset to hold the
text alone (no caret, icon, key text or border toward the foreground; `CommitSummary`'s rectangle ends at its
40 px right padding, so that `SummaryCounter` lies outside it); a box is read unfocused, but
the text typed into the focused `FilterBox` in launch 1 (L1's and L2's `sea`), which is read less the
caret's column. No key is sent to a window that is not the foreground window: a launch whose first
input is a key (launches 3, 5 and 6) first asserts `ForegroundIsThis`.
As built since step 19 (ruling 100), no read relies on the comparison for its colour: every text
read has its rectangle (`BoxTextArea` and `TypedArea` always give one, and `LabelArea`, where UI
Automation gives a label no text element, reads in the element's bounds inset by `LabelInset`, left
of any key text, and run.txt says so), and for L6 a to c, where UI Automation gives `main`'s name
no text element, the read is a problem (`NoHeadNameArea`), noted and asserted after the picture's
comparison, the decision in the static `HeadInk`: the comparison matches colours within 48 a
channel (D58) and cannot tell `#7C8CFF` from `#8B9AFF`. A miss is reported to the lead, never met
by a wider tolerance (§2.9 item 6).

#### 12.3.4 The launches, in `Phase8aOrder`

1. **`Check_L_changes_placeholders_and_danger_button`** (`Scenarios.ChangesAsync`, one tab). L1: a real
   click on graph row 0, the working row; the ink of `FilterBox`, `CommitSummary`, `CommitDescription`:
   `#8F97A4` on `#272C36`; `l1a`. Ctrl+Shift+E, `sea` typed on the real keyboard: its ink `#E4E7EC`;
   `l1b`; the filter emptied as the twin's L5 starts. L5 dark: the discard of `src/Calculator.cs` asked
   as the twin asks it; a spot of the danger button `#EA7D86` (`AssertColour`), its label's ink `#10121A`;
   `l5a`; Cancel; `git status --porcelain` still lists the file modified. A real click on `ThemeSwitch`;
   L2: the same three boxes `#68707D` on `#FFFFFF`; `l2a`; Ctrl+Shift+E, `sea` typed on the real
   keyboard: its ink `#1B1F27` on `#FFFFFF`, less the caret's column; `l2b`.
2. **`Check_L_graph_danger_item_and_head_row`** (`Scenarios.GraphAsync`). L4 a: `feature/login`'s
   menu by a real Shift+F10, real Down until "Delete…" is highlighted: its ink `#EA7D86` on `#2F3542`;
   `l4a` (pointer kept); Esc. L6: `main`'s row in the left panel a) pointer at neutral: `#7C8CFF`;
   `l6a`; b) pointer at the row's centre: `#8B9AFF` on `#2F3542`; `l6b` (pointer kept); c) a real
   click on the row, pointer at neutral: `#8B9AFF` on `#2A3150`; `l6c`.
3. **`Check_A_and_B_about_and_icon`** (one empty tab). B1 as §7.5 a) to c): the executable's
   icon (as built since step 19, ruling 100, asked at 64 px, `ExecutableIconSize`, through
   `Icon.ExtractIcon`, a frame the `.ico` holds at any scaling, with the size asked and the size got
   in run.txt, where it was the associated icon, whose size Windows takes from the scaling) and
   `WM_GETICON`'s two against the `.ico`'s frames (`IconFile`, the file under the
   `RepoRoot` metadata), within 2; `b1-title-bar-dark-with-frame` and `b1-title-bar-icon-dark` (the title
   bar's left 64 × caption height, four times, nearest-neighbour). Before A1's first key (B1 presses
   nothing, so A1's Ctrl+Shift+P is the launch's first input) `ForegroundIsThis` is asserted (12.3.3);
   if it fails, one real click on the window's title bar, away from its left end (where the icon
   opens the system menu) and from the caption buttons, then `ForegroundIsThis` asserted again within
   5 s, else the method fails before any key (12.3.5's second ruling, for the main window too; no
   click at the neutral point; the lead, 2026-11-21). A1: About by the twin's route;
   `AboutVersion`'s automation name equals `BuiltVersionDisplay()` (ruling 59) and also ends with
   ` (`, seven hexadecimal digits and `)`; if the pass's Release build carries no commit, the launch
   fails saying so, and the lead rules then (unverified until §12's first run); A1's dark picture (the
   twin shows `PinnedVersion`, seven characters apart); at 1920×1080 too when `app.Fits(1920, 1080)`,
   else run.txt says it does not fit (plan step 4). A4: Tab to `AboutShowLicenseButton`, a real Enter:
   the dialog, the focus in the text by part K's text wait (UI Automation's focused element is
   `TextDocumentText`, or lies inside its bounds and is no button, since AvaloniaEdit's inner
   `TextArea` has no automation id; run.txt says which held; the lead, 2026-11-21), under 3 % from
   `a4a-license`; Esc, the focus back on the button. Esc. A real click on `ThemeSwitch`; B2:
   `b2-title-bar-light-with-frame`, `b2-title-bar-icon-light`; then, as built since step 19
   (ruling 100), run.txt notes the display that holds the app's window (`DisplayBounds`:
   `MonitorFromWindow`, then `GetMonitorInfo`'s `rcMonitor`, in screen pixels), and
   `TaskbarButtons("VisualCommit", 10 s, display)` looks from the desktop root every 100 ms until a
   look finds a button named so on that display, and gives every such button of that look, on every
   taskbar (at the limit, the last look's). run.txt notes each with its name, its bounds, its
   taskbar (`Shell_TrayWnd`, or `Shell_SecondaryTrayWnd` on another display) and whether it lies on
   the window's display (its centre inside it, `TaskbarEntry.LiesOn`); only those on it count
   (`SelectTaskbarButton`). Exactly one there: its bounds asserted, and it is captured by its bounds
   as `b2-taskbar-button` and `b2-taskbar-button-enlarged`. None found: the method does not fail on
   it, and run.txt says `B2's taskbar button: Not run (no taskbar button named VisualCommit was
   found within 10 s).`; found, but none on the window's display: the same, with `B2's taskbar
   button: Not run (taskbar buttons named VisualCommit were found within 10 s, but none on the
   display that holds the app's window, at {display} (screen pixels); each is noted above).`
   (`NoTaskbarButtonOnDisplay`); either Not-run line leaves that step to the gate's check by hand,
   and the report's B2 says "Not run" for it. More than one on the window's display: none is
   captured, and the method fails at its end, together with B1's problems, by
   `MoreThanOneTaskbarButtonMessage`, which names each button and says to close every other
   VisualCommit, or unpin it: a button does not say whose process it is, Windows gives each
   executable's path a button of its own, and by default it shows one app's button on every
   display's taskbar, so only the window's display counts.
4. **`Check_I1_second_start_hands_over`** (data folder D; a session of one empty tab written before the start, `RealApp.Launch(D, session)`). The app `Minimise()`d;
   `session.json`'s and `instance.json`'s bytes read; `StartBeside(D, the same environment)`: the second
   start exits 0 within 10 s; the app is not iconic and is visible within 2 s; `ForegroundIsThis` noted,
   not asserted (the test host is not the foreground process); D's log holds the second's `it was asked
   to come to the front, and this start ends.` and the app's `answered activated.` (4.3.5);
   `session.json`'s bytes unchanged; `instance.json`'s bytes unchanged (a `Held` guard's `Dispose`
   touches no file, 1.11); the app closes with 0.
5. **`Check_I2_notice_and_exit_code_2`**. The test opens D's `InstanceLockFile` with `FileShare.None`
   and holds it; `RealApp.Launch(D)`, without a session (nothing written into D before the start, as the twin has
   none), finds the notice as the process's main window, within 15 s; not
   sized. `InstanceNoticeFolder`'s automation name is D's path; `CompareMasked` with
   `i2a-notice-dark`. Before any key: `ForegroundIsThis` and `InstanceNoticeTryAgain` as UI Automation's focused element; if
   either fails, a real click on the notice's title bar (its non-client area, which focuses no control)
   and both asserted again within 5 s, else the method fails before any key with `The notice is not the
   foreground window with Try again focused: a real Enter would reach another window.`; then a real
   Enter, as the twin presses it (4.6; the lead's ruling of 2026-11-20), so the focus frame matches:
   while it reads `Trying…`, disabled,
   `CompareMasked` with `i2b-notice-trying`; within 10 s `InstanceNoticeStatus` reads
   `It still did not answer.`; `CompareMasked` with `i2c-notice-no-answer`. A real click on
   `InstanceNoticeClose`: exit 2 within 5 s; D holds `instance.lock`, `logs/` and the launch's own
   `harness/`, nothing else.
6. **`Check_I2_notice_try_again_starts_here`**. As 5 to the notice; the test releases the lock; launch 5's guard
   (`ForegroundIsThis` and Try again focused, a title-bar click only if not, no key until both hold); a
   real Enter on Try again, as the twin presses it (4.6), so the focus frame matches: within 15 s `OtherWindowOfProcess` finds the main window (`GitStatus` in it);
   `instance.json`'s `pid` is the app's; that window closed, exit 0 within 15 s (D422).
7. **`Check_F_missing_offers_keys_and_choose`** (F1's session; `StandInEnvironment("none")`). Before the
   app starts, the test looks for winget as the app does, `ProgramSearch.FindOnPath("winget.exe", PATH,
   ';', File.Exists)` over its own `PATH`, which `RealApp.Launch` passes on unchanged (as D446 finds
   git in the test process); none: the method fails with `winget.exe is not on this machine's PATH: F1,
   F2 and F5 need it (install App Installer from the Microsoft Store).` (D449; never "Not run"); found:
   run.txt names the path. F1: before any read or capture, a wait for `GitSetupGuidance` and
   `GitSetupWingetButton` (the screen shows only once its guidance is read, 5.3); the screen's title,
   body and buttons as `Phase8aExpectations.F`,
   `GitSetupWingetButton` among them; `GitStatus` `Git not found`; `GitSetupButton` `Set up Git…`;
   `f1a-missing-windows`. F2: a real click on `GitSetupWingetButton`; the confirm `Install Git with
   winget?`; `f2a-winget-confirm`; a real click on the confirm's `CancelButton`; no `ConfirmationDialog`
   within 5 s, else the method fails before any further key; 12.3.2's two checks; the screen unchanged.
   F13: at least one real F6, then real F6 until the screen's first button has the focus, at most four
   presses (after F2's Cancel the button may already hold the focus, D104); real Tab through F13's order, each Tab
   followed by `WaitForFocusedElement` of the order's next id, whose failure ends the method before any
   further key; Enter only once the focus is on `GitSetupCheckAgainButton`: one more search logged,
   still Missing; once the search ends `GitSetupCheckAgainButton` has the focus again (5.3). F4: a
   real click on `GitSetupChooseButton`; `ChooseFileInDialog("Choose git.exe", git, 30 s)`, `git` by
   D446, `GitLocator.FindExecutable(GitSearchContext.FromSystem())` in the test process; within 60 s
   the screen is gone and the restored repository's `loaded` and working-tree lines are logged;
   `GitStatus` `Git {v}`, its help text `{git}, chosen in Settings`; `settings.json`'s `gitExecutable`
   is `git`; `f4a-found-again`.
8. **`Check_F5_too_old_stand_in`** (F5's session; `StandInEnvironment(StandInPath, version: "git
   version 2.29.0")`). Before any read or capture, a wait for `GitSetupGuidance` and
   `GitSetupWingetButton` (5.3). The title `Git 2.29.0 is too old`, `{min}` from `GitVersion.Minimum`,
   `GitSetupPath` the stand-in's path, Download shown, and `GitSetupWingetButton` reading `Update with
   winget…` (D453: the stand-in's git counts as found on the machine; the method fails as launch 7's
   does when the machine has no winget), not clicked; `GitStatus`'s help text
   `{path}, from VISUALCOMMIT_GIT_STAND_IN (a test switch)`; the log's stand-in Warning once (1.14);
   `f5a-too-old` (the path differs from the twin's, ruling 47). run.txt names the stand-in's path and whether
   it holds a space.
9. **`Check_F6_config_fault_stand_in`** (`failure:` F6's text, `fatal: bad config line 1 in file
   C:/Users/demo/.gitconfig`). Before any read or capture, a wait for `GitSetupGuidance` (5.3).
   `Git is not working`; `GitSetupGitSaid` that line; the fault's guidance;
   `GitSetupDownloadButton` and `GitSetupShowFileButton` shown, never pressed; `f6a-config-fault`.
10. **`Check_F6b_real_git_broken_config`** (no stand-in; `[user` written into
    `GitIsolation.SessionGlobalConfigFile(D)` before the launch, as phase 6's `globalGitConfig`). Before
    any read, a wait for `GitSetupGuidance` (5.3). The screen's title `Git is not working`; `GitSetupGitSaid` starts with `fatal: bad config line 1 in
    file` and names that file (separators and case aside); no picture; the line noted.
11. **`Check_K1_hunk_by_keys`** (`Scenarios.ChangesAsync`). The working row by a real click; a real F6,
    which gives the unstaged list's first row, then 18 real Tabs along 6.3.4's order (each row's own
    button, its Stage, its Discard) until `StageFileRow` of `src/Calculator.cs`, the seventh row, has
    the focus; Enter: `DiffText` focused. Tab:
    `StageHunkButton` focused; `k1a-hunk-button`. Enter: `git diff --cached --numstat` holds that hunk
    alone (`Phase8aExpectations.K`), `DiffText` focused; `k1b-hunk-staged`. Shift+Tab:
    `DiscardFileButton` focused; `k1c-toolbar`. Tab back into the text, Ctrl+Home, four Downs and two
    Shift+Downs (rows 4 and 5 of the remaining hunk; the counts `Phase8aExpectations.K`'s), Shift+F10: the menu holds
    "Stage selected lines"; `k1d-lines-menu` (pointer kept); Enter: two more lines staged.
12. **`Check_K2_blame_by_arrows`** (`Scenarios.HistoryAsync`, `{ "theme": "Light" }`). The twin's file
    selected as the twin selects it; Ctrl+Shift+B; three real Downs, each followed by a wait for the
    picked run's commit in the right panel (`DetailsSubject`, or the stage panel, read by
    `UnstagedTitle`, for the run not committed yet); `k2a-blame-run`;
    Shift+F10: its menu; `k2b-blame-menu` (pointer kept); Esc twice.
13. **`Check_U_graph_row_and_names`** (`Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`, with
    `u-untracked.txt` written into its work tree first). A real click on display row 1; the graph's
    first child, `GraphSelectedRow`, read once (`FindFirstChild` of `GraphRows`: its node made, D455,
    12.3.5's third ruling); then `RecordSelectedRowEvents()`. Real Up, then Down twice;
    after each, within 5 s, a LiveRegionChanged from `GraphSelectedRow` (D445 as D450 amends it; a
    `Name` change alone fails and is written to run.txt); its name and help text as
    `Phase8aExpectations.U`; `ListItem`; one element in the graph's Selection pattern;
    `WindowLayout.RowHeight` ±1 high inside `GraphRowsBounds()`; the focus still `GraphRows`. Names
    (1.12), each noted with its `AcceleratorKey`: the resolver's five (a real double-click on
    `notes.txt`, phase 4's way; then a real click on `ResolverCloseButton` and a wait until the
    resolver has closed: the double-click leaves the keyboard focus in the resolver's result, where
    Esc is the resolver's own and closes nothing, phase 4's design 11.10; the lead at §9's merge,
    2026-11-21, as method 2 does); Ctrl+J: `ActivityLogToggle`, `ActivityLogClose`, the three
    splitters; Ctrl+F: the search bar's three, Esc; the untracked row hovered: `Stage u-untracked.txt`,
    `Discard u-untracked.txt`; Stage clicked, the staged row hovered: `Unstage u-untracked.txt`. No
    picture.

The app shows no text of this package's; the pass's own messages are the lines quoted here.

#### 12.3.5 What wave 2's build tells the launches

What §5's, §6's and §7's builders, reviewers and fixers found that 12.3.4 does not say, each item
checked against the merged code (the lead, 2026-11-20). `F`, `K` and `B` are
`Phase8aExpectations.F`, `.K` and `.B`.

**The lead's rulings.** (1) Part F's resting point: in `f5a` (launch 8) and the scripted-only `f10c`
the neutral point lies on `GitStatus`'s text, which has a tooltip since §5, and a real pointer
resting there opens it. §12 gives part F a resting point of its own that lies on no control with a
tooltip or a hover look (a member of `Phase8aExpectations.F`), rests the scripted `f5a` and `f10c`
there too, and shows by the files' hashes that both scripted pictures stay the same files (headless
no pointer is drawn, and nothing hovers at either point); if one changes, §12 tells the lead before
going on. (2) Launches 5 and 6: the fallback click on the notice's title bar lands away from its
left end, where the icon now opens the system menu. (3) From the spike (D455), for launch 13: the
launch reads the graph's first child (`FindFirstChild` of `GraphRows`) before its first move; cold,
a move raises no event from `GraphSelectedRow`, so without that read every move would fail D450's
rule for the wrong reason. It covers launch 3's main window too (the lead,
2026-11-21; 12.3.4 launch 3): its fallback click on the title bar lands away from the left end and
from the caption buttons.

**Part F (launches 7 to 10).**

- Launch 7 runs with `StandInEnvironment("none")`. Each search that no setting answers logs Info
  `Git was not looked for on this computer: VISUALCOMMIT_GIT_STAND_IN names no git program (none).`
  and nothing else, so F13's "one more search logged" is one more of these lines. `GitAccess`'s
  Warning `VISUALCOMMIT_GIT_STAND_IN is set (a test switch): unless Settings names a Git, this
  computer is treated as having no Git.` comes once, at the start's search.
- Launch 7, F2 then F13: `WingetCommand` allows concurrent executions, so `GitSetupWingetButton`
  stays enabled under its confirm, and after Cancel D104 should give it the focus back without the
  outline (`NavigationMethod.Unspecified`); no scripted check asserts that focus. The first real F6
  then leaves the graph area, and a later one brings the focus back to the button with the outline,
  within `Phase8aExpectations.F.MostF6Presses` (4).
- Launch 7, F13's Enter on Check again: while the search runs, `GitSetupCheckAgainButton` is
  disabled and its UI Automation name reads `Checking…`; with the stand-in `none` the search ends
  almost at once, so the launch need not see either. When `CheckGitAgainCommand.IsRunning` falls,
  `GitSetupView` posts the focus back at `DispatcherPriority.Loaded`, only if the focus is then on
  nothing or the window, and by Tab, since the button had `:focus-visible` when Enter pressed it.
  Wait for it with `WaitForFocusedElement`; never read it at once.
- Launch 7, F4 through Choose: the native picker's title is `Choose git.exe` (the guidance's
  platform), and on Windows a file whose name does not end in `.exe` is refused unrun. Choose first
  probes the file (`GitAccess.ProbeAsync`, which ignores the stand-in); the probe logs Info `Using
  Git {v} at {git}.` and Debug `The Git in use came from the setting.`. It then writes
  `gitExecutable`, waits for any running Check again to end and runs its own, whose search logs both
  lines again: a wait on the Info line must not take the probe's for the search's. The failed tab
  then reopens (`RepoTabViewModel.ReopenAsync`, no new recent entry), and the merge tools' lookup
  starts once the reopening has ended.
- Launch 8: the stand-in's path must be fully qualified and end in `.exe`. Otherwise the app counts
  it as `none`: the screen shows Missing, not TooOld, and the log has the Info `… names no git
  program ({value}).` and the Warning for `none`. A full `.exe` path with no file there gives the
  Warning `… Git is looked for only at {path}.` and still Missing. A good path logs that Warning
  once and, per search, the Warning `Git 2.29.0 at {path} is older than the minimum, 2.34.1.`.
- Launch 8's `f5a`: the neutral point (760, 687) lies on `GitStatus`'s text `Git 2.29.0 is too old
  (needs 2.34.1 or newer)` (in `f6a`, launch 9's, it lies left of `GitSetupButton`, on nothing).
  `GitStatus` now has the tooltip `{path}, from VISUALCOMMIT_GIT_STAND_IN (a test switch)`, which a
  real pointer resting there opens after the hover delay. The scripted `f5a` shows none (part F's
  `Picture` asserts it closed), so the capture must be taken without it: the lead's first ruling
  above.
- Launch 9: the stand-in writes `VISUALCOMMIT_GIT_STAND_IN_FAILURE`'s text and a line end to
  standard error and exits 128, whatever the arguments. The app keeps the text trimmed as git's
  words, reads `ConfigFault` (`C:/Users/demo/.gitconfig`, line 1) from it, and logs the Warning `Git
  at {path} did not report its version: exit 128, "fatal: bad config line 1 in file
  C:/Users/demo/.gitconfig".`, besides `GitAccess`'s `… Git is looked for only at {path}.` once.
  `GitStatus`'s help text is `{path}, from VISUALCOMMIT_GIT_STAND_IN (a test switch)`.
- Launch 10: `GitSetupPath` is the first `git.exe` in a fully qualified folder of the `PATH` the app
  inherits (a relative entry is skipped, D449), else the usual folders. The scripted twin compares
  it with `GitLocator.FindExecutable(GitSearchContext.FromSystem())` in the test process, and the
  launch can do the same because `RealApp.Launch` passes the `PATH` on. `git --version` runs with
  the session's git environment, so it reads the session's broken global file; this was seen by hand
  on git 2.56 only. The Warning `Git at {path} did not report its version: exit {n}, "fatal: bad
  config line 1 in file …".` names git's first line.
- The stand-in: with 12.2's `<ProjectReference
  Include="..\VisualCommit.GitStandIn\VisualCommit.GitStandIn.csproj" />`, its framework-dependent
  app host `visualcommit-git-stand-in.exe` (`GitStandIn.FileName` in Testing) lands beside the test
  assembly, as it does in Git.Tests' output, where `GitStandInTests` run it.
- The screen shows only once its guidance is read, and the guidance is read once per session, at the
  screen's first show, so `GitSetupWingetButton` either shows with the screen's first frame or not
  at all. Its `WingetPath` is looked for over the app's own `PATH`
  (`ProgramSearch.FindOnPath("winget.exe", PATH, ';', File.Exists)`).
- `GitSetupButton` (`Set up Git…`, a `VcFlatButton` named by its text) sits left of `GitStatus` in
  the status bar's right-hand group. `GitStatus` has no help text while no git path is known, as in
  launch 7's F1.

**Part K (launches 11 and 12).**

- Launch 11's walk: the real F6 gives `README.md`'s `StageFileRow` (its UI Automation name the
  path); the 18 real Tabs then reach, in order, the (row, id) pairs of
  `Phase8aExpectations.K.CalculatorWalk` (built from `UnstagedRows` and `RowStops`: each row's
  `StageFileRow`, `RowStageButton`, `RowDiscardButton`), ending on `src/Calculator.cs`'s
  `StageFileRow`. Tab and Shift+Tab between rows are the lists' items panel's, `RowOrderStackPanel`
  (6.3.4; since step 19's second round, where they were `WorkingChangesView`'s tunnel `KeyDown`):
  it makes the next row, scrolls the list to it and gives it the focus within the key press, and
  each Tab is waited on by its focused element, never by time. `src/Calculator.cs` lies below the
  list's view at 1100×700, and the walk leaves the list scrolled as the scripted twin leaves it.
- The real pointer stays off the stage rows through the walk and at every capture (the neutral
  point): a row that holds the keyboard focus has its `RowStageButton` and `RowDiscardButton` in UI
  Automation at `Opacity` 0 (D425), and a hovered other row adds its own, drawn, so a find of a
  row's button by id alone can meet two rows' buttons. The same holds for
  `Phase2RealWindowPass.ClickRowButton` and `Phase5RealWindowPass.ClickFileRowButton`, which wait
  for exactly one; design 6.8 item 5's runs of `*Phase2RealWindowPass.Check_17*` and
  `*Check_A4_real_keys*` alone, the owner told, are the lead's.
- In launch 11 nothing may move the focus between the real Enter on the row and the text's wait: the
  diff's text takes the focus only while the focus is still where it was when the diff opened, or on
  nothing, the window or inside the diff (6.3.6).
- After the real Enter on `Stage hunk` the focus stays on the button until the diff's re-read makes
  the bars again; the text takes it then, not at the key. So the text's wait (no button) comes after
  `git diff --cached --numstat -- src/Calculator.cs` prints `K.StagedAfterHunk` (`1 1
  src/Calculator.cs`) and one `StageHunkButton` is left. The scripted twin waits for the view
  model's `StagedTitle` `Staged files (4)` and the re-read; the app's `StagedTitle` text block has
  that automation id, which `K.Ids` does not hold.
- The chosen lines are taken on the one hunk left after `k1b`: Ctrl+Home, `K.DownsToLines` (4)
  Downs, `K.ShiftDownsForLines` (2) Shift+Downs. The diff's caret is not drawn and its selection is
  not in UI Automation (the text's peer is a plain `Document`), but `StageLinesButton` and
  `DiscardLinesButton` show in place of `StageFileButton` and `DiscardFileButton` while lines are
  selected; they show from the first Shift+Down on, so they prove a selection, not its two rows,
  which git's lines after Enter prove: `K.StagedAfterLines` (`3 1 src/Calculator.cs`), with
  `K.UnstagedAfterLines` (`2 0 src/Calculator.cs`).
- Launch 12: `K.BlameDowns` gives each Down's run, line and subject: (0, line 2, `Add the parser`),
  (1, line 3, none: the stage panel shows, `UnstagedTitle` reads `Unstaged files (1)`), (2, line 4,
  `Rename the parser to reader`); `K.DownsToRun2` is 3. The blame picks after a caret key with any
  modifier held, and only while the keyboard focus is in `BlameText`, which takes it when the page
  opens from Ctrl+Shift+B; the page opens with the caret on line 1 and no run picked, and a click on
  the text would pick its line's run.
- `Phase8aExpectations.K.ChangesLaunch` (`Check_K1_hunk_by_keys`, "changes", dark, 1100×700, `k1a`
  to `k1d`) and `HistoryLaunch` (`Check_K2_blame_by_arrows`, "history", light, 1100×700, `k2a`,
  `k2b`) are `K.Launch(Method, Scenario, Dark, Size, Captures)` records; `K.Ids` holds the 50 ids
  part K's steps wait on or read.

**Part B (launch 3) and the notice (launches 5 and 6).**

- `IconFile.Read(IconFile.RepositoryPath(IconFile.IcoFile))` gives the 14 frames in the file's
  order, `IconFile.IcoSizes` (16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256);
  `IconFrame.Bgra()` gives a frame's pixels top-down and unpremultiplied for the bitmaps and for the
  256's PNG alike, so B1 (a) and (b) compare the extracted icon's alpha-255 pixels with it directly,
  within `Phase8aExpectations.B.IconTolerance` (2). `IconFile.RepositoryPath` resolves under the
  `RepoRoot` metadata of the `VisualCommit.Testing` assembly, which the real-window project already
  references; `IconFile` itself uses no Avalonia type.
- B1 (a): on the laptop `Icon.ExtractAssociatedIcon` of the Debug `VisualCommit.exe` gave 32×32, a
  size the `.ico` holds. The Release build the pass's command makes carries the same
  `ApplicationIcon`, but neither it nor the size under the test host's DPI awareness has been tried.
  As built since step 19 (ruling 100), B1 (a) asks the size: `Icon.ExtractAssociatedIcon` gives the
  icon at `SM_CXICON` for the test host's DPI, 56 at 175 %, a size the `.ico` lacks, so launch 3
  would fail there with a correct icon (reasoned, not measured: the laptop runs at 200 %, where the
  first runs got 64, 12.10); `ExecutableIconAt(ExecutableIconSize)` takes
  `Icon.ExtractIcon(ExecutablePath, 0, 64)`, a frame the `.ico` holds at any scaling, and
  `Phase8aOrderTests` hold the built executable's icon asked at 64, 48 and 96 against those frames,
  with no app.
- The `.ico`'s frames at 32 and below are drawn from `icon-small.svg` (the tile filling its square
  but for its rounded corners, radius 3.75 of 16); from 36 up from `icon.svg`, the tile inside a
  transparent margin of one sixteenth (31.5 px at 36). At the laptop's 200 % `SM_CXSMICON` (32) and
  `SM_CXICON` (64) are both frames of the `.ico`; at 175 % the small metric, 28, is none, so B1
  (c)'s rule for a size the `.ico` lacks applies.
- `Phase8aExpectations.B` holds launch 3's facts: `RealWindowPictures`
  (`b1-title-bar-dark-with-frame`, `b1-title-bar-icon-dark`, `b2-title-bar-light-with-frame`,
  `b2-title-bar-icon-light`, `b2-taskbar-button`, `b2-taskbar-button-enlarged`, in that order),
  `WindowTitle` (`VisualCommit`), `TitleBarCropWidth` 64, `Enlargement` 4, `TaskbarButtonNameStart`
  (`VisualCommit`), `TaskbarButtonLimit` (10 s), `TaskbarButtonNotRun` (run.txt's line, word for
  word the report's) and `IconTolerance` 2; B2's second Not-run line and its failure for more than
  one button, since step 19, are the pass's own (`NoTaskbarButtonOnDisplay`,
  `MoreThanOneTaskbarButtonMessage`). The file uses `Avalonia.Media` (`Color`, `FontWeight`)
  as 7b's linked expectations do; the real-window project, which linked no
  `Phase8aExpectations*.cs` when this was written, links them since §12's build by 12.2's 8a link
  line (`39490c7` on `wip/p8a-realwindow`).
- The notice window carries the same `Icon`, so launches 5 and 6 show the mark at the left of its
  title bar; I2's captures are its client area and do not change. Their fallback click on the
  notice's title bar must land away from that left end: a click on a window's icon opens its system
  menu (and a double click closes the window) instead of only activating it. Launch 3's fallback
  click on the main window's title bar does the same, and also keeps away from the caption buttons
  (the lead's ruling (2), for the main window too; 12.3.4 launch 3).
- In the light theme the white line's ends meet a light title bar or taskbar and open the tile's
  outline at left and right; `b2-title-bar-icon-light` and `b2-taskbar-button` are inspected with
  that as the mark, not a cut.

### 12.4 Tests

- `Phase8aRealWindowPass`, 13 methods (12.3.4). None fails first: each behaviour was proven by an
  earlier package's scripted check; this one proves the real window shows the same.
- `Phase8aOrderTests` (no app; in the whole pass, outside D246's filter): `Phase8aOrder.Methods` names
  every `[Fact]` of `Phase8aRealWindowPass` once; every capture name matches 0.3's pattern. As built
  on `wip/p8a-realwindow` since step 19 the class has the four tests more that 12.10 lists, six
  tests and eight cases in all: B1 (a)'s icon asked at 64, 48 and 96, B2's buttons on the window's
  display and more than one there, and L6 without a rectangle for `main`, each with no app.
- Earlier tests changed: none. `StartInfoFor` moves code unchanged; the builder runs phase 0's
  `Check_8_the_real_window_matches_the_scripted_walk_through` alone once, the gate's pass all of them.

### 12.5 Visual checks

No part of its own. The R steps it repeats, by the parts' sections:

| Part | Repeated (of the part's) | Launches | Captures compared | Captures without a twin |
|---|---|---|---|---|
| L | L1, L2, L4 a, L5 dark, L6 a to c (5 of 6) | 2 | 9 | 0 |
| A, B | A1, A4; B1, B2 (4 of 8) | 1 | 2 (3 where 1920×1080 fits) | 6 |
| I | I1, I2 (2 of 3; I1 has no scripted step) | 3, and I1's second process | 3, the folder line masked (D448) | 0 |
| F | F1, F2, F4, F5, F6 with F6b, F13 (6 of 14) | 4 | 5 | 0 |
| K | K1, K2 (2 of 8) | 2 | 6 | 0 |
| U | the row, the names (2 of 3) | 1 | 0 | 0 |
| G | none (no real window opens a browser) | 0 | 0 | 0 |

Dark at 1100×700 unless named (L2, K2 and B2 light). The report marks each R and gives, under "The real
window", the method of each.

### 12.6 Earlier pictures

None changed by this package: its diff is under `tests/VisualCommit.RealWindowTests` but for the three
files of 8a's walk-through that 12.3.5's first ruling changes (`Phase8aExpectations.F.cs`,
`Phase8aChecks.F.cs`, `Phase8aChecks.F.Helpers.cs`; ruling 96), whose two scripted pictures stay the
same files, so no scripted picture can change, and `StartInfoFor` changes no launch. Proof: `git diff
--stat phase/8a-release-readiness...wip/p8a-realwindow -- . ':(exclude)tests/VisualCommit.RealWindowTests'`, in its
three-dot form, which compares the part's branch with the two branches' merge base (`f0b4f8a`) and so
leaves out whatever the phase's branch has gained since (the review before the gate, realwindow-3;
ruling 99), prints those three files alone; at the gate the
earlier captures differ from phase 7b's only as 0.7 names (§2's, §7's three framed and §2's placeholder in the same three, §8's eight, the two
palette captures). New files: the 31 captures above (32 where 1920×1080 fits) and their 25 (26) difference pictures in
`artifacts/visual/phase-8a/real-window/`, with run.txt.

### 12.7 Lagged list, time, the pass

- Lagged list and default run: nothing. CI's Windows job compiles the project, seconds more.
- Real-window pass: 13 launches of 25 to 60 s, **+9 to 11 minutes, 43 to 45 in all** (0.6, D442). `Phase8aPerformance` skips (`VISUALCOMMIT_PERF_RUNS` unset after
  the lead's series; the skipped count shows it). D246's two runs: about 10 to 12 minutes each.

### 12.8 Done when

The lead checks at the merge (status step 19):
1. `dotnet build tests/VisualCommit.RealWindowTests -c Release` has no warning; the merge's CI run is
   green in every job, its Windows job compiling the project.
2. 12.6's `git diff --stat`, in its three-dot form, prints `Phase8aExpectations.F.cs`,
   `Phase8aChecks.F.cs` and `Phase8aChecks.F.Helpers.cs` alone (it read "prints nothing" until
   2026-11-24, which ruling 96's change of part F's resting point had made untrue; ruling 99).
3. During the build (status step 18), the builder made the twins in one command, since each run
   empties the scripted folder (`dotnet test --project tests/VisualCommit.VisualTests.Phase8a` with
   `--filter-class` `"*Phase8aChecksL"`, `"*Phase8aChecksA"`, `"*Phase8aChecksI"`, `"*Phase8aChecksF"`,
   `"*Phase8aChecksK"`), then ran each
   method alone (`--filter-method`), the owner told before each, never beside a heavy run (ruling 23):
   all green, captures inspected; phase 0's method alone green.
4. `Phase8aOrderTests` pass; a Grep of the part files finds no press of 12.3.2's list.
5. D246's two runs (the lead, the owner present, after the 8a walk-through on the merge's commit) green,
   made once; each run.txt read: high contrast off, 200 %, the stand-in, winget, the icons, the
   taskbar (since step 19 launch 3's line for the display that holds the app's window, and each
   button's `on` or `not on` the window's display), the version, U's events and names, every ink.

### 12.9 Risks, unverified, open points

Unverified, settled in the builder's first runs and D246's: the taskbar button's name and reach from
the desktop root, a capture outside the window; `WM_GETICON`'s sizes under per-monitor DPI (B1 (a)
asks its size since step 19, ruling 100, and `Phase8aOrderTests` hold `Icon.ExtractIcon` at 64, 48
and 96, with no app); `ProductVersion` holding `+<commit>` (else `BuiltVersionDisplay` reads the
attribute with `System.Reflection.Metadata`); a LiveRegionChanged from `GraphSelectedRow` reaching
FlaUI (settled by §9's spike, ruling 32 and D450: its FlaUI client received one per move, but only
once it had read the graph's first child, D455, so launch 13 reads it first; launch 13's own run,
with its events' counts, is left to D246's runs); full-coverage ink at 200 % under ClearType (§2.9
item 6); a tooltip over `main` in `l6b`; the notice found by `GetMainWindow`; the stand-in's runtime
files in this output (the first build); F4's dialog title and box 1148 (S14's pattern). Unverified
since step 19 (ruling 100): B2's rule per display, reasoned from Windows' setting "When using
multiple displays, show my taskbar apps on" (all taskbars by default) and held only by
`Phase8aOrderTests`' made-up bounds, since the laptop has one display and the rule meets a second
only if one is attached at the gate; and whether the laptop's taskbar gives one button per program,
which launch 3's next run shows.

Risks: a gate's machine without `winget.exe` on its `PATH` fails launch 7 (D449); a run in the
worktree proves no path with a space (the main checkout's does); another VisualCommit, running or
pinned to the taskbar, whose button lies on the display that holds the window fails launch 3 at its
end, with the advice to close it or unpin it (ruling 100). The owner's icon is no longer one:
the owner chose the mark before §7 was built (1.17, 7.8), so B's captures show it from D246's runs on
and nothing waits there.

Settled: §5 gives the focus back after Check again (5.3), and F13 asserts it; U's untracked file
takes no picture (9.5); launch 13 closes the resolver by a real click on `ResolverCloseButton`,
since Esc in the resolver's result closes nothing (the lead at §9's merge, 2026-11-21; 12.3.4);
winget is on the laptop's `PATH` (the lead, 2026-11-19; 13.4 item 7). No open point is §12's: 13.4
item 7's by-hand try of the terminal window is §5's, before its merge.

### 12.10 As built and first run (the lead, 2026-11-23)

**Built** (run `wf_b0d89b0f-cab`; `wip/p8a-realwindow`, 20 commits from `f0b4f8a`, the last
`83f3f6b`; rulings 96 to 98, D459). As 12.2 to 12.5 have it, with these differences.
`RealApp.Phase8a.cs` holds more than 12.2 lists: `FocusedElement`, `FocusedId` and `WaitForFocusOn`,
`ClickTitleBar` (one click at the title bar's middle, refused when that point belongs to another
window), `SetPointerOnTitleBar`, `VisibleFrameOffset` (the window manager's frame, for B1's crop),
`ChooseFileInDialogGuarded`, `BuiltProductVersion`, `ReadOr` and `IsReadFailure` (reads that never
throw), and the result types of its helpers; its calls into the app have a limit
(`SendMessageTimeoutW`, `ShowWindowAsync`). `StartBeside` returns a wrapper that stops a second
start left running. Every key goes through `Keys`, `KeysOn` or `TypeKeys`: the foreground window
asserted before each, never F1, an Enter or a Space only on an element that is none of 12.3.2's list
(to which the update confirm's `Update` is added) and, where a key is meant for one element, only
while that element has the focus. `RecordSelectedRowEvents` uses no cache request: FlaUI 5.0.0
passes none when it registers a handler, so each sender's id is read outside the handlers. `Compare`
is split into `Capture`, the reads, `Compare` and the reads' assertions, so that a failed read never
hides the comparison's share. Launches 8 to 10 write no session, as their twins have none. B1's icon
results are asserted at launch 3's end.

**Launch 5 as built** (D459, amending 12.3.4's paragraph): the guard with its click on the notice's
title bar, then the notice's five words by name, then `i2a`; `CompareMasked` accepts a height within
3 px and 5 %; the status line's words before `i2c`. **Launch 7's F4**: the guarded dialog, and the
help text and the setting held exactly to the path the dialog handed the app. **Launch 11**: the
caret keys wait on the focus and the toolbar's buttons (ruling 96). **Launch 12**: the gutter's
three spots. **Launch 3**: the guard before B1 (c).

**Run** (12.8 items 3 and 4). The twins made in the worktree (phase 0's checks, 7 tests; 8a's
walk-through, 154 tests); then each method alone, the owner told in the chat before each round: on
`b6fdd8a` at 21:15 to 21:24, 11 of 13 green (launches 5 and 13 failed in the harness, ruling 97); on
`83f3f6b` at 22:28 to 22:38, 13 of 13 and phase 0's method (ruling 98). The 28 compared captures of
the main window differ from their twins by 0.14 to 1.76 %, the same shares in both rounds; the
notice's three by 2.90 to 3.19 %. What run.txt settles of 12.9's unverified points: the taskbar's
button is found from the desktop root, named `VisualCommit - 1 running window`; `WM_GETICON` gives
32 and 48 px icons at the window's 192 DPI and the executable's icon is 64 px, each equal to its
frame of the `.ico`; the build's `ProductVersion` holds `+<commit>` and About shows `0.1.0
(<seven>)`; a LiveRegionChanged from `GraphSelectedRow` reaches FlaUI at each move; the notice is
the process's main window; the stand-in's files land in the pass's output; F4's dialog is titled
`Choose git.exe` and its box takes the path by its value. A 1920×1080 window does not fit the
laptop's work area. The runs' files are in `D:/Projects/Personal/phase8-work/wave4/realwindow/`
(`run1/`, `run2/`). Left for the merge and the gate: 12.8 items 1, 2 and 5 (CI on the merge; D246's
two runs).

**The review's three fixes** (status step 19; ruling 99's *realwindow*, ruling 100). On
`wip/p8a-realwindow` after `83f3f6b`, in the part's own worktree, never run on the desktop by their
builder or fixer: `3e32b9b`, `e3c0884`, `c80689d` and `166299a`, then the reader's two points,
`3ffec5c` (the test, failing), `82922f5` and `902288e`, the head; four files of
`tests/VisualCommit.RealWindowTests` (`Phase8aRealWindowPass.cs`, `.AB.cs`, `.L.cs` and
`RealApp.Phase8a.cs`). *B1 (a)*: `ExecutableIconAt(ExecutableIconSize)`,
`Icon.ExtractIcon(ExecutablePath, 0, 64)` in place of `Icon.ExtractAssociatedIcon`, compared with
the `.ico`'s frame of the size it came at by `PixelsAgainstFrame`, the rule `IconAgainstFrame` now
calls; run.txt gets the size asked and the size got (12.3.4 launch 3, 12.3.5). *B2*:
`RealApp.TaskbarButtons` and `DisplayBounds` in place of `TaskbarButton`; `TaskbarEntry` holds its
taskbar's class and `LiesOn`; the choice is the static `SelectTaskbarButton`, the failure
`MoreThanOneTaskbarButton`'s, and buttons that show only on other displays give a Not-run line of
their own (`NoTaskbarButtonOnDisplay`); `TaskbarButtonPictures` returns its problem, which launch 3
asserts with B1's at its end (12.3.4 launch 3). On one display every button lies on it, so on the
laptop the display rule changes nothing: with one button launch 3 does what it did, with two kinds
of run.txt line more (`B2's taskbar button: the display that holds the app's window at {…} (screen
pixels)`, and on each button's line its taskbar and `on` or `not on` the window's display), and with
more than one, where it captured the first, it now fails at its end; whether the laptop's taskbar
gives one button per program is what launch 3's next run shows (12.9). *L6*: `ReadInk` takes a
rectangle that cannot be missing, and L6 a to c read through `ReadHeadInk`, which hands the static
`HeadInk` the ink rule: without a rectangle for `main`'s name it reads nothing and gives
`NoHeadNameArea`'s problem, noted and asserted after the picture's comparison (12.3.3).
*`Phase8aOrderTests`* gains four tests, six cases:
`The_executables_icon_comes_at_the_size_asked_and_equals_that_frame` (a theory at
`ExecutableIconSize`, 48 and 96, which reads the built `VisualCommit.exe` and starts no app),
`More_than_one_taskbar_button_named_VisualCommit_fails_naming_each`,
`B2_counts_only_the_taskbar_buttons_on_the_display_that_holds_the_window` and
`L6_without_a_rectangle_for_main_is_a_problem`; with 12.4's two, six tests and eight cases, and its
summary now names the rules of the steps it holds with no window. The fixer ran the class alone: the
display fact failed on `3ffec5c` as meant, and 8 of 8 passed on `82922f5` and on `902288e`. No
earlier test changed, and no scripted picture can change. Left for the lead before the part's merge:
launches 2 and 3 run again on these fixes, the owner told (ruling 99).

## 13. The lead's rulings, what waits on the owner, and the open points

### 13.1 The rulings in short form

The lead wrote 75 rulings on 2026-11-19, before the drafting (D243), from the readers' questions (1 to
23) and the eight inputs' open points (24 to 75). The design cites them as "ruling n". The owner has not
seen them (13.3). A ruling that a decision amends says so. Ruling 76 came later, on 2026-11-22, from
D457 (5), and stands at the end in D457's own words.

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
what an OpenSSH older than 8.4 does, and the log gets one Info line per session, at the first Ask or
Deny scope (amended by D424; the ruling said per ssh operation). 8. The Accounts section's netrc
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
the splitters named (amended by D456 for one control: the branch row's help text). 35. No change to
the window's peer for `GetFocus`. 36. The status bar's operation text a polite live region; an item
status on the left panel's rows to the Backlog. 37. The sweep lives in 8a's walk-through project.
38. The graph's move to a List is a decision; architecture.md is rewritten for it at the close. 39.
The guide says Narrator on Windows is the one reader tested.

**First run (firstrun.md).** 40. The "Git program" row always shown, at the global scope's bottom;
nine phase 6 pictures named; written to `settings.json` only when set. 41. A chosen git that is gone
is a state of its own, with no silent fallback. 42. A change while git works applies at the next
start; "Check again" at once. 43. The helper-missing state is proven by scripted checks only.
44. git-scm.com's winget line, in a terminal that stays open, only when git is missing; too old: the
download and "Check again" (amended by D453, the owner's answer: also for a too-old git). 45. On macOS
`xcode-select --install` without the app's confirm.
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
keeps Q1 alone (amended by D457: M3's bound comes from its change's "after" series, ruling 76). 66.
A trigger for batched config reads may change earlier exact-call tests, each named; deferred
background loads need a new ruling. 67. A lock file and a per-run endpoint in `Program.Main`; the
notice with "Try again"; exit codes 0 and 2; 8b's key and the macOS limits are 8b's. 68. The macOS
data folder in the docs is right; a test per platform closes plan.md's line.

**The floor (floor.md).** 69. `Minimum` 2.34.1. 70. P1's worktree check on every git, the refusal
naming the path; git's own from 2.35 the second guard. 71. "Required" means `continue-on-error`
goes; no GitHub setting. 72. The scenarios' stashes rebuilt below 2.36; the app's own asserted in
git's words. 73. What the floor makes dead goes: D277's branch, the `--diff-merges` fallback, the
common-dir workaround (its premise only, D420); the 2.36-pinned gates stay. 74. The floor job keeps
its limits; a container job timed out by a slow mirror is run again. 75. One decision for P1, the
stash naming and the patch level; the mirror refspec a plan risk.

**The restore order (D457, the lead, 2026-11-22).** 76. When the session's active restored tab is a
repository's, it opens first and alone; the other restored tabs open at the earliest of its graph's
first page, its opening or first load failing or ending without a page, its tab closed, or 5 s on
the session's clock, and then at most four at once, as "Add repositories" opens and a dashboard
reads (D207); the package measures the window's answers and may lower the four with its numbers. A
waiting tab that the user shows opens at once; a waiting tab that is closed never opens. When the
active tab is a dashboard or holds no repository, every restored tab opens at once, as today.
`InitializeAsync` keeps its contract, so no harness wait changes; the start-up line still counts
every restored tab; the merge tools' lookup still starts when the first restored load ends (D202).
D457 also amends ruling 65: M3's bound comes from its change's "after" series (13.4 item 17).

**Part G's checks (the lead, 2026-11-23, before §11's build; the report's open points 99 to 113).**
77. "No git call" in G1 and G3 means none made by the palette's row, F1 or a link, read with the
quiet marks beside the live watcher (D280); G2's empty tabs keep the plain count. 78. G2 runs on one
empty tab for each start, `gitExecutable` written before the first, its four states the fake's
answers. 79. The report's additions from 11.3.1 stay (F1 with the palette open, Shift+F1 and
Ctrl+F1, the palette's row on the first-run screen); the site's hand check gains the five first-run
addresses and is the lead's, on the gate commit's `guide-site`; the pages are read against 11.3.2
once, at §11's merge, and at the gate only those changed since. 80. G2 reads its link's frame by a
helper of its own: against a capture taken before the focus moved, no pixel differs inside the tight
box of the link text's ink and some pixel differs in the button's bounds outside it, for both link
texts; `LinkFrame` stays `VcLinkButton`'s; a frame that touches the ink stops the builder, and the
lead rules before the merge. 81. Where the focus is after Enter on a first-run link is not part G's:
the first-run screen's launching buttons and links, whose commands allow no concurrent runs, go on
the list of the review before the gate, measured with a launcher that answers late. 82. §11's
builder adds facts for what no check shows: the Info line without a launcher, the Error line when
the launcher throws, F1's route on a dashboard tab and on a welcome tab; F1 under a menu and during
a drag stay the router's table's. 83. Phase 6's fact is renamed
`The_shortcut_list_has_twelve_groups_and_fifty_one_rows`, and the builder corrects the comments and
summaries 11.4 lists and names any other. 84. The lead's `--expected` file at §11's merge names 8a's
own `k3a` and `l3a` with 11.6's pictures, and holds boxes for `s14f` and `s14e`, the second from the
builder's measure; the lead runs 8a's project whole there, besides Phase6 and Phase6Look. 85. F1 on
a Mac goes into 8b's Mac checklist at the close; the focus frame's colour stays named by its key.

**The restore order's package (the lead, 2026-11-23, before its build; section 14's choices 1 to 18
and open points).** 86. Choices 1 to 3, 5 to 9 and 12 to 18 stand as section 14 has them: a
`RestoreOrder` made only for a start whose active tab is a restored repository's beside another
restored tab, living until that start has ended and keeping no closed tab;
`RepositoryViewModel.FirstPage` as the first-page signal; the calls from `Activate`, `CloseTab`
(before the tab's `Dispose`) and `CloseAll`; a slot held to its tab's first page, its opening's end
or its close, never freed by a clock; a shown tab without a slot and never subscribed; the active
tab as it is when the openings begin; tab order; the log lines; "reverted" read as "not merged", the
proof running on the package's branch; the merge tools' lookup at `FirstLoadEnded`; the count under
the order's lock; a waiting tab showing what an opening tab shows; the 5 s counted from the first
opening's start; a missing git taking the order; the first tab's close releasing inside `CloseTab`.
87. The 5 s run on a clock of the order's own, `TabServices.RestoreClock`: the session's clock
unless the harness gives one, as `ReadClock` is for a dashboard's 10 s rule, so in the app D457's "5
s on the session's clock" holds to the letter and no test's outcome depends on the system clock. The
builder adds the optional `restoreClock` to the lead's `TabServices.cs` and `AppSession.cs` and to
`ShellDriver.cs`'s `Start` and `StartWithTabs`, last in each list, and nothing else there. 88. The
probe keeps four at once when the middle value of the longest answers of the fix's three ten-large
launches is at or under the highest of the longest answers of the base's three one-tab launches of
the same session; a trial of two reads the same statistic in a session of its own, and two stays
only if it meets the rule with an active-tab clock no later than four's. One launch's longest answer
is a maximum, and one disturbed launch must not start a trial that costs three builds. 89. "The
fix's M2 lies within the base's range" is read of the fix's M2 median, between the lowest and the
highest of the base's five runs: M2 runs unchanged code in both builds. 90. "Check again" keeps
reopening the failed tabs all at once in 8a and gets a Backlog row at the close;
`NamesRepositoryInDialogs` and Add open tabs during the wait stay on the list of the review before
the gate; M3's bound without a merge, and a release that delays the first frame, are ruled when
their case arises.

**The restore order's build (the lead, 2026-11-23, at its end; 14.8 steps 1 and 2).** 91. The
package stands as built and reviewed (`wip/p8a-restore`, `aa83146`), with its departures from
section 14: the page's action completes `FirstPage` only for a page with a row; the order's handler
hands the tab's view model to the watch, which reads `FirstPage` and the graph's count in its own
continuation; the tabs let into slots are queued under the lock and asked for in the order let in,
also when an opening ends inside its own call (a provider that fails at once), so 14.3.4's "have
been asked to open when it returns" holds for a `Closed` that is not itself reached from inside an
asking call, which then asks for them right after; `Open` checks under the lock that the entry still
holds its tab and asks for no tab closed since it was let in. The order is not declared
single-threaded: the lock stays, and the one gap left, a close on another thread between that check
and the provider's call, is accepted, since the app reaches the order only on the UI thread and such
an opening ends as a closed slot tab's does. Four test methods more than 14.4 lists stand (a slot
freed once at its tab's close or first page with a tab shown meanwhile; a waiting tab's drop
starting no lookup, on the dispatcher; the asks' order when openings fail inside their call; a tab
closed after it was let in): 33 new tests by xUnit's count where 14.7 said about 29.
`AboutEntryTests` and `RequestOperationsProvidersTests`, which the package did not run, are the
merge's lagged list's. Nothing of this touches the proof's rule (rulings 88 and 89).

**The proof and the Task Scheduler (the lead, 2026-11-23 at 13:31, before the proof's first launch;
14.8 steps 3, 4 and 6).** 92. The laptop has a task every 15 minutes (`ActionCenterDeferralTask`),
hourly ones (`EasyDuplicateFinder`, Zoom's updater) and Office's, so "waits out anything due" is
carried out by the scripts. The probe's session reads once what is due in its next 40 minutes and,
before a launch, waits out a task due within the next 45 seconds (its time, 15 s more, and its end).
The series reads what is due in the next ten minutes before each run and waits out what is due
within five, a run's own length (about 3 minutes) with a margin; a task due later than that falls
after the run. A run has met a scheduled task when the reading after it shows a task's last run
between its begin and its end; it is then taken again with its pair's other run, as step 6 says. One
task is left out of that: `\Microsoft\Windows\Location\Notifications`, which runs every few minutes
on an event (13:24:00 and 13:29:05 today), shows no next time, so it cannot be waited out, and ends
at once with 0x80070002, its program missing on this Windows build: it starts nothing, the report
names it, and it voids no run. Any other task without a next time that a run meets voids it. The
waits lengthen the series from about 30 minutes to about 55; the owner is told.

**The restore order's proof (the lead, 2026-11-23, at its end; 14.8 steps 5 to 8; D458).** 93. Four
at once stays: ruling 88's rule is met (230 ms against 335 ms), and no trial of two is made. The
series is proven by D457 (5)'s rule on five undisturbed pairs [amended by D461 on 2026-11-24: its
third condition is not shown by that series, which is taken again; ruling 99] (the report's "The
restore order's proof"). Four of nine pairs met a task the scheduler had not shown as due and were
taken again, as step 6 says; once four pairs were kept, their values fixed the rule's outcome for
any fifth pair, so the lead would have ended the series there had the last retake met a task too,
which it did not. M3's bound is 8,900 ms (7,077 ms; step 8). M2's bound goes from 6,100 to 9,500 ms,
as D457 (3) provides: the slowest of the fourteen runs that themselves met no scheduled task, on the
base and on the fix, whose one-tab start makes no order, is 7,563 ms, the fix's (from the base's
runs alone, 7,374 ms, it would be 9,300 ms); a run of a dropped pair counts for this when its own
run met no task. The fix's M2 median lies 794 ms above the base's, with ranges that overlap wholly;
the fix adds no wait and no process on the one-tab path, the window opened 416 ms later in its kept
one-tab runs (medians), before anything of the fix runs, and counted from the window's opening its
one-tab first rows came sooner than the base's in four of the nine pairs and later in five: read as
the laptop's spread, and put on the list of the review before the gate, with the bound's reading
that both builds count. 14.9's open point 4 (M3's bound without a merge) did not arise. Its open
point 5 (a release that delays the first frame) is answered by the probe and the alternation
together. The release does come before the first frame: its line stands 180 to 256 ms before the
tab's `first graph rows drawn` in the probe's six launches with ten tabs. No cost of it shows beyond
the spread: by the tab's own clock its first rows came as soon beside nine tabs as alone (the fix's
medians 1,840 and 1,913 ms against 2,113 ms alone), and in the series, counted from the window's
opening, the fix's M3 first rows came 255 ms after its M2's in the five kept pairs (medians 2,511
and 2,256 ms) and 236 ms before them over all nine (1,957 and 2,193 ms). The order stays as built.

**The guide's pages and pictures before §11's merge (the lead, 2026-11-23; ruling 79, 11.8).** 94.
§11's reviewers read the pages by sampling and found nine false statements, so before the merge
every statement of the 33 pages was checked against the source and every one of the 84 pictures
opened, each finding verified by a second reader (run `wf_be89944c-5a7`, read only): 1,157
statements, 73 confirmed faults in the text (six major), 11 alt texts or sentences that do not match
their picture, 14 framing defects of pictures, no leak. All confirmed findings are applied in the
package's worktree before the merge (run `wf_3220124e-9d8`), under these rulings: each with its
verified replacement, overlapping ones merged into one text; `install.md` gains a fourth item, the
backups left in repositories under `refs/visualcommit/backup/`, though 11.3.2's row names three; the
graph's widest dates touching the overlay scroll bar are the app's look and change neither a page
nor a shot (they go on the list of the review before the gate); the resolver's page sentence on the
status bar is corrected and its shot is not rebuilt to run the merge in the app;
`troubleshooting/large-repositories.md` takes the restore order's numbers as indications (one
restored tab about 5 s, ten about 5 to 7 s) and says that the tab in front opens first, and
`tabs-and-workspaces.md` says how several restored tabs open (D458). What the pass found in the app
itself, not in the guide, goes on the same list: a repository picked under Recent repositories opens
in that page's tab even when another tab shows it.

**§11 as built (the lead, 2026-11-23, at its merge; 11.8).** 95. The package stands as built,
reviewed and corrected (`wip/p8a-guide`, 35 commits from `124337a`, the last `bc24568`; merge
`57ad86e`), with its departures from section 11, which 11.10 lists: mdBook's print page is off; a
shot sets the render scaling before its state; the 8 pixels of margin stand only round a dialog's
card or a menu; the hosting shots put a prefix handler in front of the fake of GitHub and list
GitLab and Azure DevOps with every request refused; `LeakGuard` holds the machine's name to the same
floor of three characters as the user's; every shot but the first-run ones reports Git 2.43.0; the
window is 1440 wide for the diffs, the file pages, the drag menu and the reflog; `guide.yml` is also
started by `tests/Directory.Build.props`; a pictures folder named by the variable loses only the
catalogue's files. Ruling 80: the first-run links' frame touches no ink (0 pixels differ inside the
ink's box of either link text, 1,146 and 918 outside it). Ruling 82: `AppShortcutsTests`' router
table runs no key under a menu or a drag; `KeyRouterTests`' three facts that every key declines
under a menu, a dialog and a branch drag press every tunnelling key, F1 among them since its
binding, and `OpenUserGuideCommandTests` keeps four facts in App.Tests (the two that started the app
on "ops" repeated those and went, D416). Part G's open point 103: no read of the settings screen
lands after G1's step 7 mark; typing `guide` makes no git call; `ExactStateAsync`'s own git does not
reach the session's call log. After ruling 94's pass one more pass settled nine statements its
fixers had seen and left and swept the pages' universal claims (run `wf_28644568-d81`: 131 claims,
11 narrowed, one of them major: the file history lists the commits in the history of HEAD or of the
chosen commit, not every commit that changed the file). For the list of the review before the gate:
the resolver's overlay scroll bar lies across the last line of a conflict that ends at the panes'
end (seen in the guide's `resolver` picture, which stays); whether ruling 7's sentence that an old
OpenSSH asks "in the terminal VisualCommit was started from" holds on Windows, where the app is a
window program and starts git without a console; and the guide's About page, which names mdBook, the
fonts and Font Awesome but not highlight.js, which the site also ships. From the gate commit on
`docs/guide/**` stays docs (0.4): a correction to a page is checked by the guide's workflow, and a
change to the app's words, keys or behaviour needs its page's change, which no test finds but for
the pages, anchors and key tables that the two App.Tests classes hold.

**§12 as built, before its first runs (the lead, 2026-11-23; 12.8).** 96. The package was written
and compiled, never run, by run `wf_b0d89b0f-cab` (four builders, three reviewers, a fixer;
`wip/p8a-realwindow`, 15 commits from `f0b4f8a`, the last `b6fdd8a`). It stands with these readings.
12.6's proof leaves out, besides `tests/VisualCommit.RealWindowTests`, the three files of 8a's
walk-through that 12.3.5's first ruling changes (`Phase8aExpectations.F.cs`, `Phase8aChecks.F.cs`,
`Phase8aChecks.F.Helpers.cs`: part F's resting point, (400, 687)); the scripted `f5a-too-old` and
`f10c-ubuntu-too-old` are the same files after it, by their SHA-256. Every key is sent only while
the app's window is the foreground window, not only a launch's first, and each Enter is pinned to
the element it is meant for. F4 confirms Windows' Open dialog through a guarded form of S14's
helper: the path set through the file box's value alone, the Enter sent only while the dialog is the
foreground window with that box focused; the help text and `gitExecutable` are held exactly to the
path the dialog handed the app, and that path to the machine's git, case and separators aside. The
update confirm's `Update` joins what no step presses. K1's caret keys (Ctrl+Home, the Downs, the
second Shift+Down) are exempt from 12.3.1's named condition, the diff's caret not being in UI
Automation: they wait on the focus and the toolbar's buttons, and git's lines prove the rows. B1's
crop starts at the visible frame's corner, read from the window manager, and the title bar's height
is the window's top to the client area's top; B2 waits for the client area drawn light and, without
failing, for the title bar's redraw; B1's icon results are asserted at the launch's end. F4's
confirmed Open dialog leaves entries in the user's recent-files lists of Windows' common dialogs, as
phase 6's S14 does; run.txt says so.

**§12's first runs (the lead, 2026-11-23, 21:15 to 21:24, the owner told; 12.8 item 3).** 97. Each
of the 13 methods ran alone, on `b6fdd8a`, after 8a's walk-through and phase 0's checks had made the
twins in the worktree: 11 passed at once, their 28 compared captures between 0.14 and 1.76 % from
their twins. Two failed in the harness. Launch 13 threw on a read of an event sender's id from UI
Automation's cache, which the sender does not carry; the read falls back to a current one. Launch 5
failed D448's precondition, the notice 216 high against its twin's 218; D459 settles it (the height
within 3 px, 5 % for the notice alone, its words held by name, the guard before the first picture).
The fixes and the inspection of the 28 captures are run `wf_1d035720-312`'s; the lead then runs
launches 5 and 13 again, the owner told.

**§12's second runs (the lead, 2026-11-23, 22:28 to 22:38, the owner told).** 98. After the fixes
(`b7a6281`, `20d1d38`, then `7618b73`, `4795797` and `83f3f6b` for what the checker of the fix and
the inspectors of the first runs' captures found), each of the 13 methods ran alone again, on
`83f3f6b`: all passed, and so did phase 0's
`Check_8_the_real_window_matches_the_scripted_walk_through` alone (12.4). The inspectors opened the
30 captures of the eleven methods that had passed and found no fault of the app; the lead took their
points: run.txt notes every value a launch asserts, each exit code among them; K2's gutter is read
at three spots, which the 3 % cannot tell apart; a word of the notice that UI Automation does not
give fails; the dead cache request is gone. Launch 5 in full: its three pictures differ by 3.19,
3.00 and 2.90 % under D459's 5 %, the heights 216 against 218 and 237.5 against 240. Launch 13 in
full: each of Up, Down, Down raised one LiveRegionChanged from `GraphSelectedRow`, beside one Name
change (D450). 12.8's items 3 and 4 hold; items 1, 2 and 5 are the merge's and the gate's. 12.10 has
the part as built.

**The review before the gate (the lead, 2026-11-23 and 24; status step 19).** 99. Run
`wf_151e85d5-daa`, read only, from 22:45 to about midnight: first a compiler of every item the notes
kept for the review, 35 items (S19-01 to S19-35); then ten reviewers by area and three across the
phase; a verifier for each major and one per reviewer for its minors; and a last reader for the
changed source files nobody had read, who found nothing more. **48 findings: 43 confirmed, five of
them major, and five refuted.** The majors: the keyboard focus left on nothing when the active tab
changes while it lies in the stage panel (S19-01) or in another region whose rows the new tab does
not have (keyboard-1, keyboard-2); the same after Enter or Space folds a section, a folder or a tree
folder (keyboard-3); the blame's arrows going on from the hidden caret and not from the run a click
picked (keyboard-5); and the status bar's polite live region told of every progress tick of git
(screenreaders-1). Refuted, each by its verifier: the placeholder in a disabled box (colours-2: the
order 2.3 promises is measured on the enabled box, and holds); the first-run links' accent while
disabled (colours-3: the state cannot be reached with the app's launcher); "Discard hunk" given its
focus back without the frame (keyboard-8: D104's return, as 6.3.1 writes it); the live region
speaking the new tab's status at a tab switch (screenreaders-2: 9.3.7's rule, a tab switch being a
move that is spoken, 9.3.4; the speech package changes when that region speaks, so what a tab switch
speaks after it is ruled at that package's merge, and 9.3.7 is amended there); and two tabs on one
worktree from Recent repositories (concurrency-lifetime-5: the state phases 5 and 6 accepted, D209).
The result is `D:/Projects/Personal/phase8-work/wave4/review/review-result.json`.

**The fixes** (run `wf_ccc5f713-fa5`, started on 2026-11-24 at 00:04): 38 of the confirmed findings
in eight packages, each built with its tests first where a test can hold it, read by an independent
reader and corrected, in worktrees from `57ebeff` (`D:/Projects/Personal/vc-wt/p8a-fix-<key>` on
`wip/p8a-fix-<key>`; the real-window part's three in that part's own worktree). The lead's rulings
to their builders:

- *focus*, two packages one after the other in one worktree. The loss at a tab change is one fix,
  made where the tab changes (the window) and not in the stage panel's view alone, as the lead first
  ruled on 2026-11-20: a focus that lies in a region of the window when the active tab changes, and
  is on nothing afterwards, goes to that region's target in the new tab (the stage panel's rows to
  the graph area's target when the new tab shows no such row), with the keyboard's frame if it had
  it. Of 8a's four Ctrl+Tab facts the two that state today's loss (from a stage row's own button and
  from its Stage) are rewritten; the graph's and the summary's, which state a focus that stays, stay
  (the builder's brief said "the four", wrongly; its result is read for that). With it keyboard-7
  and keyboard-9; and keyboard-6, per site, armed only when the pressed button had the keyboard's
  frame, so that mouse paths and earlier pictures stay. Then the rows: a row that only changes
  itself keeps its control (`IsOpen` becomes something a row updates in place, and `RowCollection`'s
  scans count a row that can update itself as kept: keyboard-3, screenreaders-4,
  performance-restore-rows-3), and where a control truly goes (the eye of a branch that is shown
  again, keyboard-4) the focus goes to its row. No scripted picture may change but for a focus that
  is now kept.
- *speech*. The live region speaks an operation's start and its result, not its progress, and
  nothing a sighted user sees changes (screenreaders-1); a polite live setting on the first-run
  screen's title and its error or checking line and on the notice's status, nothing else
  (screenreaders-3); a pick by the pointer moves the blame's caret to the picked run's first line
  (keyboard-5).
- *instance*. instance-1 to instance-4, each with a test; the notice's look and texts, every wait's
  limit and the exit codes stay; the narrower modes apply only to a file or folder the app itself
  creates, and nothing changes on Windows.
- *git*. The boolean read keeps its first command and acts only when that command fails, with what
  git 2.34.1 has (floor-1, performance-restore-rows-5); the commit at the top of a scrolled graph
  stays there across a reload, the unscrolled graph behaves as today, and the restore order's code
  is not touched (performance-restore-rows-4); the log's line and the guide's page on an old OpenSSH
  say what is true on each system (security-privacy-1, guide-2); the guide's About page names the
  four libraries the built site ships (S19-12).
- *firstrun*. firstrun-1 to firstrun-3, icon-about-1 to icon-about-3, concurrency-lifetime-1 and
  concurrency-lifetime-2; a radio button's drawn pixels do not change; firstrun-3's new note says
  what the next start uses, and a check that shows the old note is reported, not changed. Its
  builder also renders a link's frame at the scalings 1.25, 1.5 and 1.75 and reports (S19-18), on
  which the lead then rules.
- *harness*. No test is loosened. `FetchHold`'s `Dispose` no longer hides the exception that failed
  a check first, and a hold that held nothing still fails its check (concurrency-lifetime-3,
  tests-ci-2); the dashboard's wait no longer hangs on a line a pass rightly does not write, and a
  dashboard that did not read still fails it (concurrency-lifetime-4, tests-ci-1); phase 1's check
  10 takes `10b-second-tab` with the pointer at the neutral point and no tooltip open (tests-ci-3);
  the links' check reads more of the forms that load (guide-1); `OperationsHarness`' awaits get
  limits that name the step (S19-32).
- *realwindow*, in the part's own worktree, never run by its builder. A missing rectangle for L6's
  ink fails the launch (realwindow-4); more than one taskbar button named for VisualCommit fails
  B2's taskbar step with the advice to close the other one (realwindow-2); the executable's icon is
  extracted at a size the `.ico` holds, whatever the scaling (realwindow-1). The lead runs launches
  2 and 3 again on them, the owner told.

**The lead's own five.** performance-restore-rows-1: D461, the restore order's proof is taken again
(14.10's amendment). performance-restore-rows-2: the series' script corrected for it (D461 (2)).
floor-2: the macOS picture read (3.6's note). realwindow-3: 12.6's command in its three-dot form and
12.8's item 2. colours-1: text that the app dims on purpose on rows the user still acts on lies
under 4.5:1 in both themes and outside `ThemeTokensTests`' table (a hidden branch's name and, in
solo mode, every branch left out; a dropped row of the interactive rebase; the rows a search dims;
all three older than the phase). D410 is the owner's answer and D418 says "no list of gaps", so it
is the owner's (13.3; status.md's "Waiting on the owner"), asked before step 19's whole default run,
because a change of those colours changes earlier pictures.

**The list's items ruled without a change.** S19-03 and S19-04 (the restore order's risk 3):
accepted as bounded, by the first tab's trigger of at most 5 s and a free slot, as for a tab that is
still opening today. S19-05: the one-tab path adds no wait and no process; D461 settles what the
series showed. S19-07 and S19-10 (the overlay scroll bar over the graph's widest dates, and over a
conflict's last line): the app's look, rows for plan.md's Backlog at the close. S19-08 (Recent
repositories opens a second tab on a worktree): the state phases 5 and 6 accepted (D209), a Backlog
row for the owner. S19-09: corrected at §11's merge already. S19-15: A4's 12 below the title and
U2/U3's two ends are written into those checks' expected results, with the reason in the report.
S19-19: no fault. S19-22: the pass and the series keep the test host's PATH in 8a, as every gate
since phase 0 measured, so Q1's limit and the bounds stay comparable; a Backlog row for git found
behind Git for Windows' launcher (D457's last note; plan.md's risk 10). S19-26 and S19-28 (a row
that shows something new loses its press; Avalonia's estimate after a change above the view):
Backlog rows, the second older than the phase. S19-27: no fault in the other phases' walk-throughs;
in 8a's own, U2/U3's step 4 hides a branch and waits for its rows but not for the graph's reload, as
step 9 did before its fix: the lead adds that wait at the fixes' merge, with the reason in the
report. S19-29: no fault found by reading; a solo's cost with thousands of branches stays
unmeasured. S19-31: not reproduced by reading; the test stays on the lagged list with its message.
S19-33: no fix touches the order's code, and the git package's builder is told so. S19-32's other
question: four projects at once stays (D343); one stop in a run that counted for nothing decides
nothing, and `OperationsHarness`' new limits name the step the next time. S19-34: the part merges
last, after its three fixes; CLAUDE.md's rows at that merge name 8a's own filter (`--filter-class
"*Phase8aRealWindowPass"`) and say that winget must be on the PATH or launch 7 fails (D449), that
F4's Open dialog leaves entries in the user's recent-files lists, that the part minimises the window
and starts a second process, and that `Phase8aOrderTests` run only in the whole pass. S19-35: the
route holds as built; the listen is the gate's (D460).

**The review's fixes as built, and what the lead ruled on them (the lead, 2026-11-24; D462 to
D465).** 100. The fix run (`wf_ccc5f713-fa5`, 00:04 to 06:58 on 2026-11-24, 22 agents, 7.16M tokens)
built ruling 99's packages, each with its tests first, committed failing where a test could fail
first; an independent reader then read each package against its findings, and a fixer applied what
the reader found. All 38 findings are answered. The seven readers found 33 points more, one of them
major (the harness package's own new tests gave their one second to real git work, which the default
run's load would have failed); the fixers applied what was theirs and left the rulings and the docs
to the lead. Six packages were merged on 2026-11-24, each with its test classes run on the merged
tree: focus `c0bedc6`, speech `a60056e`, instance `67ff55d`, git `9e225d1`, firstrun `ce144f4` and
harness `876c197`; the real-window part's three fixes are on its branch and merge with the part
(12). The results are outside the repository
(`D:/Projects/Personal/phase8-work/wave4/review/fix-result.json`, the picture comparisons under
`…/review/compare/`). What the packages asked of the lead, and the rulings:

- *focus*. "That region's target in the new tab" (ruling 99) is the same panel's: when the tab now
  shown has a repository of its own and shows the panel the focus lay in, the focus goes to that
  panel's target as F6 would give it, without F6's memory of the other tab's element; otherwise to
  the graph area's target (a welcome page's Open, the first-run screen's), and it stays on nothing
  while a tab still opens. A dialog that is open, or a focus the user or a view placed meanwhile,
  stays. The window does this at every change of the active tab (`MainWindow.Keys.cs`,
  `FocusAfterSwitch`; D462). The reader found that a focus in the left panel went to the empty
  panel's Filter box on a tab without a repository: fixed, the graph area's target is taken there.
  Amend's return is armed whenever Amend has the focus as a commit starts, with the frame only when
  it had one: ruling 99's "armed only when the pressed button had the keyboard's frame" was
  keyboard-6's, and the palette's Commit, the one path a user has, reaches Amend without the frame
  (D104). A cancelled Discard lines clears the selection, as before the fix, so its button goes and
  the diff's text takes the focus. A toggled folder or section keeps its row and its control, and a
  rebuild that only inserts or only removes rows is one change that never resets the list; the reset
  above 64 rows stays for a rebuild that keeps the count and for one that both removes and inserts.
  `GraphReviewFixTests.Opening_a_folder_inserts_its_rows_without_resetting_the_list`, an earlier
  test, now holds one Add of the folder's 40 rows and the same folder object where it held one
  Remove and 41 Adds: accepted, its point (no reset) stands. Accepted as a limit, measured in
  `LeftPanelScrollTests`: a branch created above the rows in view of a scrolled left panel keeps
  every row and tells the list of one Add, but Avalonia's estimate still draws the rows in view 22
  px lower, 4 px above their true places, with new controls, and a press held across it loses its
  click; the numbers are the same without the fix (S19-28's Backlog row). The builder also saw that
  Tab walks the left panel's rows in the order their controls were made, not the rows' order, once
  rows were inserted: older than the package, and fixed in the second round (below).
- *speech*. The status bar's live region speaks an operation's start, ssh's notice and its result,
  and on a dashboard a run's start, ssh's notice, Cancel all's text as it was at the click and the
  result; the text shown and its name keep the percentage (`Controls/StatusText`,
  `IStatusBarOperation.SpokenText`; D463, 9.3.7). What a switch of tabs speaks, the question ruling
  99 left: the status of the tab now shown when its spoken text differs from the one before, nothing
  when the two are equal, as before the fix. The reader read Avalonia 12.1.3's bridge: a hidden
  control is no child of its parent's peer, and a peer's events reach Windows only once a client was
  handed its node (D455's cold case). The first-run screen's error line and the notice's status show
  with their text in one step, so the polite live setting ruling 99 gave them is most likely never
  heard there, and the title only by a client that has read the screen; the tests now take each peer
  as a client reaches it (`ClientWalk`) and say so. Check again's own text, which the builder had
  made a live region, is none again: ruling 99's "nothing else" stands. The lead's ruling on what is
  left: each button that gets the keyboard focus back after such an outcome is described by the
  outcome (its help text), as a form's field is by its error; built in the second round (D463). The
  Narrator walk-through's steps 14 and 21 change with it, the reason in the report. Ruling 99's
  caret, "the picked run's first line", scrolls the text at the next arrow when that line lies above
  the view: amended to the first line of the picked run that is in view (second round). A
  right-click on the blame's text inside a selection keeps the selection.
- *instance*. As built: `SingleInstanceGuard.ResolveHeld` is the one answer to a held start that
  `Program.Main` and Try again share; a start that waits for a closing VisualCommit looks at
  `instance.json` at each try of the lock and asks whoever took the folder meanwhile, once per look,
  on the same 30 s; an ask that goes unanswered tries the lock for 250 ms more before the notice; on
  macOS and Linux the lock file the guard makes is the user's alone (0600) and so is the data folder
  when the guard makes it (0700), while a missing folder above it gets the system's default mode and
  whatever exists keeps its mode; a stale socket is deleted only when no server answers on it (D464;
  1.11, 4.3). Accepted: a new holder that keeps the lock while its endpoint refuses holds a waiting
  start until the closing wait ends, where the first form of the fix gave up after five seconds, and
  the log names the case. Accepted as known: a start whose ask is in flight at the moment a closing
  holder's server is cancelled gets no answer, and if another start takes the lock within its 250 ms
  it shows the notice instead of asking that start; Try again then hands over. The three tests that
  can fail only on macOS and Linux (the two of the modes, the copied folder's) were shown failing by
  reasoning alone, no builder having such a machine: CI's macOS, Linux and container jobs were their
  first run. **CI's run 45421164866** (on `b2ce493`, the six merges) passed on Windows and failed in
  the five other jobs on one test of 8,699, the modes' first: it expected a missing folder above the
  data folder to be the user's alone too, as the package's reader had read the SDK's documentation,
  and .NET gives the mode asked for to the last folder alone (`FileSystem.Unix.cs`,
  `CreateParentsAndDirectory`: every folder above it gets the default). The lead's ruling: the guard
  narrows what is the app's own, the lock file and the data folder, whose mode already keeps other
  users out of all below it; a parent gets what `mkdir -p` gives it. The test holds the two (renamed
  `…the_lock_file_and_the_data_folder_the_guard_makes_are_the_users_alone`), the comment says so,
  and D464 (4) is corrected. The other tests that could fail only there passed: the modes kept, the
  copied data folder, and the four that play a holder on a raw socket; so did the floor's new rows
  of `PullConfigurationTests` on git 2.34.1.
- *git*. As built: when `git config --type=bool --get <key>` fails and the value in effect can be a
  boolean, `git config --type=bool --fixed-value --get <key> <value>` converts the entries of that
  value alone, one command for every case where floor-1's verified fix had three (D465;
  `--fixed-value` is in git since 2.30, under D420's floor). The graph's anchor is the commit the
  control draws at the top, at the offset rounded to a pixel, and it acts on every swap of the
  graph, a hide's or solo's included: accepted (D465). The ssh line, ruling 7 amended: on Windows
  the log reads `Git and ssh get SSH_ASKPASS_REQUIRE=force, so that ssh asks through VisualCommit;
  OpenSSH older than 8.4 ignores it, and on Windows no terminal shows its question: cancel the
  operation if it waits.` (`AskPassBroker.AskPassRequireLineWindows`), because the app starts every
  git without a console window, so ssh never reaches the terminal the app was started from; macOS
  and Linux keep ruling 7's words (`AskPassRequireLineUnix`), and the guide's page has a section per
  case, "An old OpenSSH does not ask through VisualCommit". D424 stands. Nothing was measured with
  an old OpenSSH on Windows: the words hold whether ssh waits or fails. The floor's job is the first
  run of the new rows on git 2.34.1. Found at the merge: the fixer's fact
  `An_offset_drawn_as_the_top_of_the_graph_shows_new_commits_at_the_top` failed twice on the merged
  tree, at 26.4 for 0.4. It commits a file from outside at an offset of 0.4, and whenever the
  watcher's status read landed between the file's write and the commit, the working-changes row's
  one-row move (D60) pushed the view a row down, being guarded by the raw offset where the control
  draws at the rounded one; the fixer had named that guard as a fault left. The lead fixed it with a
  test first (`bc467e3` failing, `53b9c77`): the guard uses the offset as it is drawn, as the anchor
  does.
- *firstrun*. The Git program row's note after "Use the Git on the PATH" drops a chosen git that is
  in use reads `Chosen here. VisualCommit looks for Git again at its next start.`: the builder's
  "uses the Git on the PATH from its next start" can be false, the next start's search looking in
  the usual install folders too (the words change in the second round; the report has them).
  Choose's new refusal, `This file did not report its version within 10 seconds, so VisualCommit
  stopped it.`, and its ten seconds stay as built: Choose can be pressed again. A press of Show
  license… or Show third-party notices… whose texts are read while Save runs waits for that Save:
  one that closes the screen shows nothing, one that keeps it open shows the text afterwards (the
  reader's form; the builder's dropped the press). concurrency-lifetime-1's path cannot be reached
  through the app today, Save's read back waiting for the start's search; the fix removes that
  reliance and its shell test stays as a guard. S19-18, measured by the builder: a link's frame is
  right at scalings 1 and 1.25; at 1.5 its inner line lies on the text's first and last rows, the
  descenders' lowest among them, and at 1.75 it touches them (the 3 px above and below round to 4
  device pixels at 1.5, the two lines to 3 and 2). By `LinkFrame`'s own rule that is a fault at two
  common scalings: fixed in the second round. The builder also found the Theme options' frame cut by
  4 px at its top by the section's scroller, older than the phase: second round.
- *harness*. `OperationsHarness`' step limit keeps its 15 s before the hang watchdog's: a harness
  step still running then fails alone, by name and with a stack, where the watchdog would stop the
  whole process; a test whose harness step ended between 165 and 180 s counted passed before and
  fails now, which the class's remarks say (S19-32, accepted). A harness still open at that moment
  also writes a note that names the operation its queue runs, so a stall in an await of the test's
  own is named too. The links' check now takes every address as loading unless a user goes to it
  (11.3.6; 245 self-test cases). `10b-second-tab` is from now on the form without the new tab's
  tooltip, the file of waves 1 and 2's references (phase 1's report, check 10). The real window's
  own wait for a dashboard's read has the form the harness's had and is not changed: the pass runs
  alone.
- *realwindow*. As built on the part's branch: B1 (a) asks the executable's icon at 64 pixels
  (`Icon.ExtractIcon`), a frame the `.ico` holds at any scaling; B2 notes every taskbar button named
  for VisualCommit and counts those on the display that holds the app's window: one is captured,
  none there or none at all is a "Not run" line, more than one fails the launch at its end with the
  advice to close or unpin the other (the reader's point: Windows shows a window's button on every
  display's taskbar by default, so counting all taskbars would fail with no other VisualCommit
  running); L6 a to c fail on a missing rectangle for main's name. The expected results of the three
  change with the reasons in the report. Whether the laptop's taskbar gives one button per program
  is what launch 3's next run shows.

**The second round.** Three packages in worktrees of their own, `wip/p8a-after-<key>`, built, read
and corrected as the first; the briefs are in
`D:/Projects/Personal/phase8-work/wave4/review/after-packages.json`. *after-speech*: the outcome
said where the focus lands (the notice's Try again, the first-run screen's Choose and Check again,
the row's Browse), the blame's caret on the picked run's first line in view, the row's note in the
lead's words with both new texts in the expectations. *after-order*: Tab goes through a list's rows
in the rows' order, also after rows were inserted, for every list of the app whose rows hold stops,
with one helper shared with the stage lists' walk. *after-frames*: the Theme options' frame drawn
whole with nothing else moved, and a link's frame clear of its text at every scaling from 1 to 2.
The lead adds to U2/U3's step 4 the wait for the graph's reload (ruling 99, S19-27).

**The second round as built, and what the lead ruled on it (the lead, 2026-11-24; D466).** 101. Run
`wf_11b1bda1-1ef` (08:00 to 10:21 on 2026-11-24, nine agents, 2.87M tokens) built ruling 100's three
packages, each with its tests first, read by an independent reader (ten points, all minor) and
corrected. Merged on 2026-11-24 at 10:23: `950ca7d` (after-speech), `39c632f` (after-order) and
`db7fbce` (after-frames). The result is
`D:/Projects/Personal/phase8-work/wave4/review/after-result.json`, the picture comparisons under
`…/review/compare/after-*`. As built, and the rulings:

- *after-speech*. Four buttons are described by their outcome through
  `AutomationProperties.HelpText`: the notice's Try again by `StatusLine`, the first-run screen's
  Choose by `ErrorText` and its Check again by `Title`, the Git program row's Browse by
  `StatusText`, which is its refusal and, while the file is probed, `Checking…`, as Choose's is
  (D463 (3)). The builder read Avalonia 12.1.3 for it: a peer's help text is UI Automation's
  HelpText on Windows and `accessibilityHelp` on macOS, and the window's peer raises a focus change
  for which the bridge makes the focused peer's node there and then (`RootAutomationNode`,
  `AutomationNode.GetOrCreate`), so the description reaches a reader with the focus also when no
  client had reached the button before, which a live region's change does not (D455).
  `OutcomeAtFocusTests` hold each focus event with its name and help text; what Narrator says is the
  gate's listen (D460). The blame: a pick by the pointer puts the caret on the picked run's first
  line in view, a line drawn in part at the view's top counting, and at the view's left edge when
  the text is scrolled across, so that the next Up or Down scrolls nothing, also from an empty line
  and when the pick clears a selection. The row's note reads as ruled
  (`GitSetupTexts.NextStartLooksAgainSuffix`), and both new texts are in the expectations. Two of
  the reader's points came to the lead. The error line also carries a failed start of winget or of
  the Command Line Tools, so Choose can be described by a failure that is not its own, and that
  failure is spoken nowhere at the moment it happens, its button keeping the focus: accepted and
  recorded; a text that changes while its button keeps the focus needs another means than a
  description (a Backlog row). And after a Browse that passes, the description is empty and nothing
  tells the row's new path and note: ruled by D463 (3)'s own rule, Browse is described by the row at
  all times, by its status while it has one and else by the path and the note; built in a third
  round (below).
- *after-order*. `RowOrderStackPanel`, the items panel the left panel's list, the commit details'
  and the comparison's file lists and the two stage lists now share, answers Avalonia's Tab
  navigation itself (D466): the stage lists' own walk of wave 2 moved into it. The builder's sweep
  of every list of the app is in its account: the virtualizing lists whose rows hold stops went
  wrong after an insert or a scroll (an opened folder's rows last, rows below the view never
  reached, Tab into a scrolled list landing in its middle); the lists that are one stop (the
  activity log, the interactive rebase, the list boxes) and the lists that make every control hold,
  and two of them have guards now. Accepted: Tab into such a list reaches its first row and
  Shift+Tab from after it its last, the list scrolled there; the left panel's walk visits every row,
  those below the view included, before it leaves, as the stage lists' has since wave 2 (F6 leaves a
  region in one press, and the arrows walk rows as before). K8's count of Tabs from the filter to
  the splitter can grow at the gate, where it is recorded.
- *after-frames*. While a settings section is at its top, its scroller's clip reaches 4 px higher,
  so a Theme option's frame is drawn whole and nothing else moves; scrolled, the section is clipped
  as before. A link's frame lies 4 px outside the link on every side (it was 3 above and below), and
  `LinkFocusFrameTests` render every `VcLinkButton` of the app at scalings 1 to 2 in steps of 0.25
  (42 cases in all): About's two, GitLab's two, and GitHub's and Azure DevOps' token link.
  `a1b-about-link-focus` and `g3a-about-guide-link` change in the frame's rows alone; every settings
  picture is the same file as before. `LayoutAudit` counts a parent's clip geometry, and `LinkFrame`
  reads pictures drawn at a scaling above 1. Three things came to the lead, each accepted with its
  numbers and a Backlog row at the close. The Theme note: at a scaling of 1 Dark's frame lies under
  two faint ink pixels of the note's "follows"; at other scalings Dark's and System's frames touch
  the note's ink or keep one row from it, and Light's keeps two rows or more. Clearing it means the
  note 2 px lower, which moves the rest of General in every picture of it. GitLab's legacy token
  link: its frame's top now reaches the token link's line box, one device row from that link's
  descenders at 1, 1.25, 1.75 and 2 and touching them at 1.5, where 3 px had left two or three rows;
  the two links would have to lie 6 apart, which moves the lower half of phase 7b's dialog. And a
  radio button's frame is cut by the scroller when Tab brings the button to the edge of a scrolled
  section: seen at the minimum window in the repository scope with "Other" shown (6 px of the
  frame's bottom); with the default content no framed control reaches an edge. Its fix changes where
  a section scrolls. The scaled link cases have run on Windows alone: CI's macOS and Linux jobs are
  their first run there.

**The third round** (one package in a worktree of its own, `wip/p8a-after-browse`, from `db7fbce`,
built, read and corrected as the others): the Git program row's Browse described by the row at all
times, and where the keyboard focus goes when "Use the Git on the PATH" or "Use the Git it found",
pressed by the keys, hides itself with the choice it drops.

**The third round as built, ruling 101 corrected in two sentences, and a fourth round (the lead,
2026-11-24).** 102. Run `wf_fa279534-fed` (10:26 to 11:37 on 2026-11-24, 12 agents, 3.32M tokens)
built the third round's package and brought the docs up to the second round, each part read
independently and corrected. Merged at 11:38: the docs `ba4570e`, the package `c9c3279`. The result
is `D:/Projects/Personal/phase8-work/wave4/review/third-result.json`.

- *after-browse*, as built. `GitProgramViewModel.BrowseHelpText` is the row's status while it has
  one and else its path and its note, joined by a full stop and a space (`C:\Program
  Files\Git\cmd\git.exe. Found on the PATH.`), raised with `StatusText` and with `Choice`; Browse's
  help text is bound to it, so a Browse that passes is told when the focus comes back (D463 (3) as
  amended). "Use the Git on the PATH" on the settings row, pressed by the keys, hid itself and left
  the focus on nothing: the focus now goes to Browse, with the frame
  (`FocusReturn.AfterKeyboardPressThatHides`, a return for a button that its own command hides; D462
  (2) as amended). The reader found two more of the kind in the same views: the Accounts section's
  netrc note's Dismiss, whose focus now goes to the Accounts section's button, and the first-run
  screen's "Use the Git on the PATH", after whose search Check again got the focus without the frame
  and now gets it with the frame when the button was pressed with it. The facts answer Browse's
  probe of a chosen file with a made-up git (`FakeGitAnswer.AvailableAt`), so their set-up no longer
  depends on the probe's ten seconds.
- The lead's rulings on what came back. Whether a press was by the keys is told by the pressed
  control's look at its Click (focused, with `:focus-visible`), everywhere in the app; Avalonia
  keeps that look on a focused button that is then clicked, so such a click counts as it looks:
  accepted, the frame was showing already. The fixer met it in `f4a-found-again`: giving the graph
  area's target the frame after the first-run screen's search made a clicked Check again, still
  framed from the step before, frame the graph; that part was withdrawn and the picture is the same
  file. So after a search that finds git and closes the first-run screen the graph area's target
  gets the focus without the frame, also after a key press, and Check again gets it without the
  frame after a Choose that passes: accepted as they are, a Backlog row. The netrc note's target,
  the Accounts section's button: accepted.
- Ruling 101 corrected in two sentences. "So that the next Up or Down scrolls nothing" said too
  much: the pick itself scrolls nothing, and the next Up or Down does not scroll the text across; as
  in any editor it scrolls by a line when the caret leaves the view (Up from the line at the view's
  top, Down from the last line in view), which no test holds. And "K8's count of Tabs from the
  filter to the splitter" named the wrong walk: K8's two walks start in the right panel and cross
  the details' file list alone, and the walk from the filter to the left splitter is
  `KeyboardPassTests`' own, which records no count.
- The references (the report's writer asked): step 19's whole default run is compared with wave 3's
  references first; when that comparison holds, its pictures are the references for the gate, as at
  each wave's close (D419).

**The fourth round** (`wip/p8a-after-hide`, from `c9c3279`, run `wf_17596548-fdb`, started at
11:40). The third round's fixer saw by reading that the stage panel's restore bar's Dismiss seems to
hide its bar with itself too, and had not swept the app. One sweep closes the class before the gate:
two independent readers list every control that its own press hides, removes or disables, one from
the views and one from the commands; a builder settles each by a fact that presses it by the keys,
with a fix where the focus is lost and a guard where it is kept; a reader checks the fixes and makes
a third sweep.

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
| Now, before wave 1 | Look at the 75 rulings (13.1) and D416 to D453: above all those that amend a ruling, D420 (ruling 73's common-dir item), D424 (ruling 7's log line), D425 (ruling 49's F4) and D434 (ruling 58), and D447 to D452, what the design's review changed (13.5). The question on winget for a git that is too old or not working (13.4 item 22) was answered on 2026-11-20: a missing and a too-old git, D453. The share of the week before each stage after the design's review (D241, D415). |
| Beside the build | The icon (D407, D447): the lead's candidates, one to three rounds, each committed in `docs/brand/icon/round-<n>/`: each candidate an SVG of its own, the contact sheet `candidates.html` with a ladder from 16 to 256 pixels on a dark and a light desk, and a `README.md` with the date, what the owner chose and what the owner asked to change (7.8). Round 1 is committed (2026-11-19, `306f61f`), before the owner's first letter. Leave for a reverse image search for the look-alike board (D412 covers the name's search only). |
| §6's merge | Told before two earlier real-window checks run alone on the laptop, phase 2's `Check_17` and phase 5's `Check_A4_real_keys` (6.8 item 5; about 2 minutes of the desktop). |
| §9's spike and merge | Told before the spike (a minute of keys and clicks); at §9's merge, a short Narrator listen (9.3.9's steps 2 to 6 and 14, about 5 minutes; 13.4 item 13). |
| §10's merge and the series | Told before the first run (about 5 minutes of the desktop) and the series (rested, the desktop for 12 to 18 minutes per run as estimated, about 8 as taken (10.7); M6 clicks Fetch). |
| The restore order's proof (ruling 76, D457; wave 4) | Asked first for the week's share (D241; answered on 2026-11-23: all of wave 4). Told in the chat just before each takeover, with no agent at work (ruling 23; 14.7, 14.8): the lead's probe (two warm-up launches and 18 counted ones on data folders of its own, no input, about 9 minutes) and the alternated series of M2 and M3 (the two builds five times each, taken as D457 (2) says, about 30 minutes), each after a rest of at least ten minutes behind its builds: about 50 minutes of the rested desktop in all, about 70 with a trial of two at once, about 80 when four stays after that trial. **Taken on 2026-11-23**, the owner told at 13:26: 13:36 to 14:52, the probe 8 minutes and the series 67 with its waits for the scheduler (ruling 92) and four pairs taken again; no trial of two. |
| The review's fixes and the proof taken again (status step 19; ruling 99, D461) | Told in the chat just before each takeover, with no agent at work: launches 2 and 3 of the real-window part run alone again on their fixes (about two minutes), and the restore order's alternated series taken again, ten pairs, the builds' order alternating (about an hour and a half of the rested desktop, more with pairs taken again). **Asked before step 19's whole default run:** what to do with the three texts the app dims on purpose, which lie under 4.5:1 (colours-1): fix the names and the dropped rows in 8a, which changes earlier pictures, or keep all three as recorded exceptions; the search's dimming cannot reach 4.5:1 by its opacity and needs a form of its own either way (status.md's "Waiting on the owner"). |
| §12's build (status step 18) | Told before each of its 13 methods runs alone and before phase 0's `Check_8` runs alone (12.4, 12.8 item 3): about 15 minutes of the desktop in all, never beside a heavy run (ruling 23). |
| Before the counted round | Nothing: the owner chose the mark before §7 was built (1.17, 7.8), and §7 built with it, so nothing waits there. |
| Before the gate | The three live tokens made again (GitHub's expires on 2026-11-29, Azure DevOps' and GitLab's on 2026-12-04), through the masked script; told before each live run (D335). Present for D246's two runs of 8a's real-window checks; told before the pass. |
| At the gate, by hand | The Narrator walk-through (9.3.9's 21 steps, about 20 minutes; its one session of two tabs, the ops copy active and the stopped merge second, started by the lead's line with CLAUDE.md's isolating variables and `VISUALCOMMIT_TIME_ZONE=UTC`; steps 20 and 21 on the first-run screen, by a second start with `VISUALCOMMIT_GIT_STAND_IN=none` after the first app has been closed, D421), phase 6's System step (D224), and a second start through the lead's isolating start file (4.6): a `.cmd` file outside the repository that sets CLAUDE.md's isolating variables and starts the Release build, which the owner starts twice, the second time while another window is in front. |
| At "Phase 8a accepted" | Before `master` is pushed, GitHub Pages' source set to "GitHub Actions", by the owner or by the lead through `gh api` at the owner's word (ruling 64, D408). After the push, the guide's run on `master` read, and run again by `workflow_dispatch` if it ran before the setting; then the live address checked. The `github-pages` environment's branch rule read: if it admits more than `master`, the owner's leave asked to set it. |

### 13.4 Open points

Each with who settles it and before what. "Settled" marks what the lead ruled, each with its date
(2026-11-19, after the integration and before the design's review, where the mark names none); a
section that a ruling changes says so in its own text. Items 7 and 9 also say what the lead settled
on the review, and item 22 is the review's question for the owner (13.5).
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
   D420's form, which the design's review kept (T8 adds to 3.6 where the second call runs).
5. Accepted risks of §4: a first instance hung before its window keeps answering "starting"; a second
   start waits up to 30 s unseen while the first closes; a data folder on a network share depends on
   the mount's locks. The lead, at §4's merge; plan.md's risks at the close.
6. Ruling 7 asks for a log line when an ssh operation starts; the broker cannot tell ssh from HTTPS
   without the remote's URL, so the line comes once per session, at the first Ask or Deny scope
   (D424). A line per operation would need `RepositoryOperations.Run.cs`, the clone and a URL parser.
   The lead, before §5. **Settled:** once per session, as D424 says.
7. `cmd.exe /S /K` under Windows Terminal, winget's app execution alias, and whether the laptop has
   `winget.exe` on its `PATH` (1.3). The lead, before §5's merge. **Settled** in part (the lead,
   2026-11-19, on the review): the laptop has it, `%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe`,
   an app execution alias (a reparse point) for which `File.Exists` is true, version v1.29.380, found
   on the `PATH`. The real-window checks F1, F2 and F5 need winget on the gate's machine and fail without
   it, with `winget.exe is not on this machine's PATH: F1, F2 and F5 need it (install App Installer from the Microsoft Store).`;
   there is no "Not run" (D449, 12.3.4 launch 7). **Settled** for the rest (the lead, by hand on the
   laptop, 2026-11-20, before §5's merge): started
    as 1.3 gives it, with `UseShellExecute` and `cmd.exe /S /K ""<the path found>" --version"`, the
    console's window opens and stays open where Windows Terminal is the default console, and
    `cmd.exe /S /C ""<the path found>" --version"` ends with exit code 0 and prints `v1.29.380`, so
    `cmd.exe` starts the alias by its full path. No install was run.
8. The focus after a row action started by keys (6.3.4): the row now in its place, else the list's
   last row, else the other list's first, else the summary; keyboard.md left it to Avalonia. The lead,
   before §6. **Settled:** this rule, with a test for each of its four cases; when the action leaves
   the tree clean, the panel goes and the graph takes the focus (6.3.4).
9. A test in CI's macOS job running `iconutil -c icns` on the `.iconset` would settle web-verify B20
   in 8a; the draft left the `.icns` to 8b (ruling 17 names the `.iconset` for it), so §7 had none
   unless the lead added it. Before §7. **Settled:** §7 adds it, and on the review the tool writes
   the `.icns` itself (D447): `assets/brand/generated/VisualCommit.icns`, from the `.iconset`'s ten
   PNGs, which 8b's bundle takes. `IconFilesTests` item 8, run only on macOS (CI's macOS job; skipped
   elsewhere), runs `iconutil -c icns` on the committed `.iconset` and `iconutil -c iconset` on the
   committed `.icns`, each into a temporary folder: each exits 0, the first gives a file that starts
   with `icns`, the second the ten names. If `iconutil` refuses the PNG data in `icp4` and `icp5`, a
   fixer drops those two entries after §7's merge, when CI's macOS job shows it, and 7.3 says so. No Mac has run the app, so CI's job is the only
   proof before 8b that the set and the `.icns` are well formed (7.4).
10. The owner's icon by the counted round, or 8a closes with the placeholder (13.3). The owner.
    **Settled:** the owner chose Lens and said yes to round 2's masters on 2026-11-20 (1.17, 7.8).
11. The look-alike board's reverse image search (13.3). The owner, before the gate. **Settled:** no
    image search; the lead compares by eye and records it in the report's checks by hand (the owner,
    2026-11-20).
12. A5's bound of 1 s and End reaching the true last line with word wrap are unverified until part A
    runs; if either fails, no wrap or a later bound. The lead, during §8. **Settled (the lead,
    2026-11-21):** the bound stays 8.5's and is asserted by the scripted check wherever it runs; the
    check also writes the time it measured to its output. If the whole default run or one of CI's
    jobs fails it, the lead rules then, with the measured times, and no bound is loosened ahead of
    that. **Settled (the lead, 2026-11-21), at §8's merge:** the bound stays, asserted at the
    method's end wherever the check runs (8.5); it held in every run of the lead and in all six jobs
    of CI's run 44969394972, and failed only in runs on the laptop while other agents built and
    tested beside them (1,001 to 2,119 ms; 1,164 to 1,759 ms); no ruling until the whole default run
    or a CI job fails it. End with word wrap reaches the true last line (8.3.2, 8.9 item 3).
13. A short Narrator listen by the owner after the spike or at §9's merge, so a change of route lands
    before the review, not after the gate. The lead, at the spike. **Settled:** the lead asks the
    owner for it at §9's merge (13.3 names it).
14. If the spike's S1 (cold) finds no event and S2 (warm) does: the public route stands with the risk
    recorded, or a decision names the `[PrivateApi]` route and its guarding test. The lead, at the
    spike, before §9's build goes on. **Settled (the lead, 2026-11-21; D455):** the spike found
    exactly this (S1 no event, S2 one Name change and one LiveRegionChanged per move; S3, S4 and S6
    as 9.3.1 expects; its events are in the merge notes), and the public route stands with the risk
    recorded. The owner's Narrator listen (item 13) is brought forward to the first build the owner
    can hear; silence there brings a new decision on the route before the review.
15. The hunk bars' repeated "Stage hunk" and "Discard hunk" names (`HunkHeaders.cs:252-253`) lie in
    no items container, so the sweep does not flag them; a help text with the hunk's header would
    change no picture, but no ruling takes it. Recommended: the Backlog, beside ruling 36's item. The
    lead, at the close. **Settled:** the Backlog.
16. Whether the Narrator walk-through adds a first-run state (the stand-in's variable in the start
    line). The lead, before the gate. **Settled:** yes, two steps: the first-run screen is the first
    thing a new user with a screen reader meets. The lead gives the owner the start line with the
    stand-in's variables. **Settled (the lead, 2026-11-21):** the start is one session of two tabs,
    the ops copy active and the stopped merge second, by the lead's start line with CLAUDE.md's
    isolating variables and `VISUALCOMMIT_TIME_ZONE=UTC`, so that the dates heard are 9.3.5's; steps
    20 and 21 by a second start with `VISUALCOMMIT_GIT_STAND_IN=none` after the first app has been
    closed, since one instance runs per data folder (D421). The owner hears the steps at the gate; a
    step whose words differ is written down as heard (9.3.9).
17. Whether a measure that a fix changes takes its bound from the "after" series (ruling 65 says
    "before"). The lead, in that fix's ruling. **Settled (the lead, 2026-11-22; D457):** M3's bound
    comes from its change's "after" series (ruling 76's package) and stays unset until then, because
    a bound from before would let the contention come back unseen; the others come from the
    "before" series, M2's from the second series alone and the rest from all fifteen runs.
18. The Pull budget is the highest count among CI's six jobs; if the floor job counts more than the
    laptop, the lead raises it with the reason. At §10's merge. **Settled (the lead, at §10's merge
    on 2026-11-21):** the budget's theory passed in all six jobs of CI's run 44952559495 on
    `86ecb3a`, the floor job's git 2.34.1 among them, so no job counted more than 23 git calls for a
    fast-forward or a merge, and the budget stays 23 (`status.md`'s CI section).
19. The region of `s14e-shortcuts-top`'s scroll bar: §11's builder measures it, the lead adds it to
    0.7. At §11's merge. **Settled (the lead, at §11's merge on 2026-11-23):** the thumb's lower
    end, (955, 260) to (957, 265); the track lies at x 955 to 956. 0.7's row has it, and the merge's
    comparison found the change there and nowhere else in the picture.
20. mdBook publishes no checksum: the lead downloads the Linux archive once, pins its SHA-256 with
    the date in the report, and puts mdBook 0.5.4 for Windows, hashed, in
    `D:/Projects/Personal/phase8-work/tools/`. Before §11. **Settled (the lead, 2026-11-23, before
    §11):** GitHub shows a SHA-256 digest for each file of the release `v0.5.4` (published
    2026-07-06), and the two archives downloaded that day hash to them: the Linux archive
    `mdbook-v0.5.4-x86_64-unknown-linux-gnu.tar.gz`
    `3f28de05dafca9d0f2eab99c662116b0e37b89b1d96a08f8f430b9eeae958cd7`, which `guide.yml` checks,
    and the Windows archive `mdbook-v0.5.4-x86_64-pc-windows-msvc.zip`
    `8a6b2421aa522de06d746871d2b8fff9c8d71773467e310a4516d2566e7f2de4`, whose `mdbook.exe` is at
    `D:/Projects/Personal/phase8-work/tools/mdbook-0.5.4/` (SHA-256
    `ffa917c9b6b608aa8b6189dcb38a548ff2615832113ad6c6e26f6ef9c8d77a44`).
21. The 42 shots may cost more than planned; the lead may drop a shot whose state is too costly and
    keep its page's text. During §11. **Settled (the lead, at §11's merge):** no shot is dropped: a
    shot takes 2 to 15 s and the whole project takes 5 to 7 minutes on the laptop.
22. Whether winget is offered for a git that is too old or not working: the owner's D405 offers the
    winget install when git is missing, too old or not working, and ruling 44 and D423 narrowed it to
    a missing git. (a) For a missing and a too-old git, the lead's recommendation, since winget's
    install upgrades an installed package and cannot repair one that does not work. (b) For a missing
    git only, as ruling 44. (c) In all three states, as D405 reads. §5's builder starts only after the
    answer, which becomes a decision of its own; until then §5 stands as ruling 44 has it (13.3). The
    owner, before wave 2. **Answered** (the owner, 2026-11-20): (a), a missing and a too-old git; D453.

### 13.5 What the design's review changed

The review of 2026-11-19 read the draft at `85169db` through four lenses (git's safety, the tests and
the gate, the contracts, the owner's scope) with five readers, and a verifier checked each blocker and
major. It made 58 findings: 17 blockers and majors, all confirmed (two blockers, O1 and O3, and 13
majors after verification; A1 and B5 judged minor), and 41 minors. Four pairs say the same thing: S1
and O5, S2 and A2, T2 and B4, B1 within O1. The lead accepted every finding as ruled; O3 alone went
to the owner (D453). The findings' full text, the verifiers' reasoning and the lead's rulings stay outside
the repository, in `D:/Projects/Personal/phase8-work/design/review/`.

The new decisions, with the findings behind them:
- **D447** (amends D429, keeps D407 as the owner wrote it): O1 and B1, the tool writes the `.icns`
  (0.1, 0.4, 1.15, 7.1 to 7.4, 7.8, 7.9, 13.4 item 9), with T6's item 9 (7.4); O2, each round of candidates
  committed in `docs/brand/icon/round-<n>/` (7.8, 13.3).
- **D448** (amends D443): T2 and B4, the notice's data folder on a line of its own, trimmed at its
  start, and the masked comparison's check of equal heights (1.11, 4.3.4, 4.5, 4.6, 12.2, 12.3.4
  launches 5 and 6, 12.5); O6, the guard's server for the current user only (1.11, 4.3.3, 4.5).
- **D449** (amends D423): S1 and O5, winget started by the full path the screen found, through
  `cmd.exe /S /K`, in winget's folder (1.3, 5.3, 5.4, 5.5, 12.3.4, 13.4 item 7); S5, a chosen git and
  the stand-in full paths (`.exe` on Windows), and a chosen `/usr/bin/git` behind macOS's gate (1.14,
  5.3, 5.4); O4, no third-party package source named (5.3, 5.4, 5.5, 11.3.2); T7, F1 and F2 (and, with D453, F5) need
  winget on the gate's machine (0.5, 12.3.4 launch 7, 13.4 item 7).
- **D450** (amends D445): O12, the graph row's proof a `LiveRegionChanged` event, a Name change alone
  failing (9.3.1, 12.3.4 launch 13).
- **D451**: B5, T4 and O17, the guide workflow's step "App's links" and the hand check's
  `guide-site` artifact of the gate commit or of a `workflow_dispatch` run on it (0.4, 0.5, 11.2,
  11.3.4, 11.5, 11.7, 11.8); O7, the workflow's token read-only (11.3.4); O8, D441's check reading CSS
  and every loading link (11.3.6); O9, the demo's GitHub owner `demo--alex` (11.3.5).
- **D452** (amends D417): S3, "Cancel {title}" cancelling only while the titled operation still runs
  (1.8, 6.3.3, 6.4); T1, K10's fourth PageDown from §11's merge (0.7, 8.4, 11.4).

Corrections of text alone, by finding and section: S2 and A2 (1.11, 4.3.2, 4.5, 4.6, 12.3.4 launch
4); S4 (4.3.2, 4.5); S6 (3.4, 3.8, 3.9); S7 (12.3.2, 12.3.4 launch 7); S8 (10.3.5); T3 (4.5); T5
(0.4, 5.6, 11.6); T6, items 1 (0.4, 2.2, 2.8), 8 (6.4) and 16 (9.3.9); T8 (3.6, 3.8); T9 (0.7, 2.6); T10
and A4 (1.2, 1.3, 5.3, 5.4); T11 (0.4); T12, A6 and B11 (6.7, 12.5); A1 (0.4, 1.2, 5.2, 5.3); A3
(1.15, 6.4, 6.5); A5 (1.3); A7 (0.4); B2 (0.4, 0.7, 8.4, 11.4); B3 (8.4); B6 (11.3.3); B7 (11.5); B8
(10.3.5, 10.8); B9 and O13 (13.3; B9's Narrator items in 9.3.9); B10 (13.1, 13.3); B12 and O10
(11.3.4, 11.5, 11.9, 13.3); B13 (1.15, 7.5); B14 (8.5); O11 (4.3.4, 5.3, 9.3.9); O14 (1.14, 5.3);
O15 (3.8); O16 (0.5, 4.6, 13.3).

After the fixers' and the checkers' passes the lead ruled on what they left open: git's own search of
the `PATH` takes fully qualified folders only, as every other search of the app's does (5.3, D449);
a keyboard Discard that leaves the tree clean gives the focus to the graph (6.3.4); a fixer, not
§7's builder, drops the `.icns`'s two smallest entries if CI's macOS job shows `iconutil` refusing
them (7.3); 0.4's list of files that pass between packages names the files WP0 writes and §5
finishes; no Windows state shows a command to copy, so the winget line shows in the confirm and the
guide alone (1.3).

What the build changed is in 1.17, wave by wave, and each changed expectation is in the report
(`docs/test-reports/phase-8a.md`, "Changes to expected results"). WP0's code review made 20 findings,
all minor; wave 1's reviews 11 majors (the same three faults seen through more than one lens) and
12 minors, all applied. CI's floor job passed on git 2.34.1 at its first round after §3's merge.

O3, winget for a git that is too old or not working, went to the owner, who answered on 2026-11-20:
a missing and a too-old git, not one that does not work (**D453**; 0.2, 1.3, 5.3, 5.4, 5.5's F5,
12.3.4 launch 8, 13.1's ruling 44, 13.3, 13.4 item 22).

## 14. restore: the restore order (ruling 76, D457)

Key `restore`, wave 4, after §10's merge (§10's measures M2 and M3 prove it); branch
`wip/p8a-restore`, worktree `D:/Projects/Personal/vc-wt/p8a-restore`; no part of checks. Binding:
D457 (5), ruling 76 in 13.1, with D457's reasons; D45 and D50 (a slow load in one tab does not
touch another; Q1's clock is the tab's own); D202 (the merge tools' lookup); D207 (four at once);
D436 and D437 as D457 (2) takes them (the same measure, the two builds run alternately); D241 (its
cost, inside the week's share the owner gave on 2026-11-23, "All of wave 4 now"); rulings 3, 19,
23, 65 (as D457 amends it) and 66; plan.md's risk 10. Inputs, outside the repository: the panel's
accounts of M3 (`D:/Projects/Personal/phase8-work/wave3/perf-m3-result.json`: the analysts
"where", "changes", "user" and "cost", then three judges), the lead's probe of the start with
several tabs (`D:/Projects/Personal/phase8-work/wave3/startup10-probe/`: `startup_tabs_probe.ps1`,
`run1.txt`, `run2-small.txt`) and the three "before" series (`wave3/perf-before*/`). Where D457
leaves a choice to the package, this section makes it from the code and lists it in 14.9 with the
lead's ruling on it (rulings 86 to 90 in 13.1, 2026-11-23). Code is cited as it stands at
`810913e`.

### 14.1 What it delivers, and what it leaves

Delivers:
- The restore order. When `InitializeAsync` finds the active tab a restored repository's tab and
  at least one other restored tab, a `RestoreOrder` opens the active tab first and alone and the
  others at its trigger (14.3.2), at most `RestoreOrder.OpensAtOnce` (4) at once (14.3.3); a
  waiting tab the user shows opens at once, a waiting tab that is closed never opens (14.3.4). In
  every other start the restored tabs open by today's code, unchanged (14.3.5).
- `RepositoryViewModel.FirstPage`, the signal "its graph has its first page" (14.2, 14.3.2).
- The order's log lines (14.3.8); its tests, written first and failing on today's code (14.4);
  `SettingsEntryTests`' test of the merge-tool lookup rewritten, as D457 (5) names it, and a second
  test of the lookup beside it (14.4).
- The proof, the lead's (14.8): the lead's probe, which measures the window's answers and may
  lower the four, then M2 and M3 with the two builds run alternately, five times each; then, if
  proven, M3's bound from the fix's runs (D457 (3), 13.4 item 17).

Leaves:
- "Check again" (`MainWindowViewModel.OpenWhatWaitedForGitAsync`) reopens every tab whose restore
  failed at once, as today, in 8a: ruling 76 orders the restore, and that reopening comes later,
  after a search that found git. It gets a row in plan.md's Backlog at the close, beside D457's
  rows (ruling 90; 14.9, open point 1).
- 10.3.6's candidate, background tabs restored in summary mode and promoted when shown: plan.md's
  Backlog with the memory it would save (D457 (5)), the lead's at the close.
- A lower process priority for the background tabs' git calls (the panel's other candidate, over
  `GitCommand.BelowNormalPriority`, Windows only): not ruled, not built.
- What comes before the window opens (2.7 to 4.9 s in the probes; 8b's published build), the PATH
  the pass and the series start the app with (the review before the gate, D457's last paragraph),
  numbers on macOS and Linux (D50).
- The docs a merge needs (architecture.md, plan.md's risk 10 and Backlog, the report's "The
  performance series", CLAUDE.md's row for the alternated command): the lead's (14.8).

Decisions: D457 (5) rules the order and how it is proven; the section adds no decision. Its
choices (14.9) stand as the lead ruled them on 2026-11-23 (rulings 86 to 90 in 13.1), and the lead
records any that changes the architecture.

### 14.2 Files and contracts

Paths without a project are under `src/VisualCommit.App/`. Each was read at `810913e`.

| File | Change | Whose (0.4) |
|---|---|---|
| `ViewModels/RestoreOrder.cs` | new: the order | restore |
| `ViewModels/MainWindowViewModel.cs` | the field `_restore`; `InitializeAsync`'s openings; `Activate`, `CloseTab` and `CloseAll` tell the order | WP0, then §5 (merged); restore's in wave 4 |
| `ViewModels/RepositoryViewModel.cs` | `FirstPage` and its five completions (14.3.2) | WP0, then §10 (merged); restore's in wave 4 |
| `ViewModels/TabServices.cs` | the constructor's optional `restoreClock` and the property `RestoreClock` (`restoreClock ?? Clock`), modelled on `readClock` and `ReadClock`; nothing else | the lead's; ruling 87 |
| `AppSession.cs` | `Start`'s optional `restoreClock`, passed on to `TabServices` as `readClock` is; nothing else | the lead's; ruling 87 |
| `tests/VisualCommit.App.Tests/RestoreOrderTests.cs` | new (14.4) | restore |
| `tests/VisualCommit.App.Tests/RepositoryFirstPageTests.cs` | new (14.4) | restore |
| `tests/VisualCommit.App.Tests/RestoreOrderShellTests.cs` | new (14.4) | restore |
| `tests/VisualCommit.App.Tests/SettingsEntryTests.cs` | one test rewritten, one added; `Shell` gives `TabServices` a `ManualTimeProvider` as `restoreClock` (ruling 87); `HeldRepositories` gains a lock around `Opened` and its openings (14.4) | no package in 0.4; restore's |
| `tests/VisualCommit.Testing/Headless/ShellDriver.cs` | `Start`'s and `StartWithTabs`' optional `restoreClock`, passed on to `AppSession.Start`; nothing else | the lead's; ruling 87 |

Read and unchanged: `MainWindowViewModel.Settings.cs` (`LookUpMergeToolsAfterAsync` takes the
order's `[FirstLoadEnded]` as it takes today's list, 14.3.6), `.Session.cs` (`RestoreTabs`, which
makes the pending tabs), `.Workspaces.cs` (`ReadActiveDashboardAtStartAsync`, `OnOpenRequested`), `.Commands.cs`
(`ActivateTabShowing`), `.Git.cs` (`ShowGitSetup`, `OpenWhatWaitedForGitAsync`),
`RepoTabViewModel.cs` (`ShowPending`, `OpenAsync`, `ShowAsync`, `RepositoryOpened`); the
harnesses `HeldGitRunner`, `ManualTimeProvider` and `FakeRepositoryBase`, and `ShellDriver`'s other
files (`ShellDriver.Phase6.Workspaces.cs` among them, whose overloads keep their lists);
`tests/VisualCommit.RealWindowTests/Phase8aPerformance.cs`, which both builds run as it is and in
which the lead writes M3's bound after the proof (14.8).

The lead's files stay as they are but for ruling 87 (14.9 choice 4), which has the package's
builder add the restore clock to three files of the lead's, `TabServices.cs` and `AppSession.cs`
(0.4) and `ShellDriver.cs`, as the table and the block below say, and nothing else there; the
order needs no other service. In wave 4 neither §11 nor §12 owns a file of this package; the
review's fixers (status step 19) take `MainWindowViewModel.cs`, `RepositoryViewModel.cs` or
`SettingsEntryTests.cs` only after restore's merge, or the lead rules. If restore is not merged
(14.8 step 9), those three pass back as they are, and the lead's three never gain the clock.

```csharp
// ViewModels/RepositoryViewModel.cs
/// Completes once, never faulted, when the first full start's history has its first page in
/// Graph, or when that start has ended without one: a history with no commits, a failure of its
/// refs or its load, its cancellation, the view model closed (14.3.2). A summary-mode view
/// model's completes at the full start that follows its promotion, or at Dispose. Over a
/// TaskCompletionSource made with TaskCreationOptions.RunContinuationsAsynchronously.
public Task FirstPage { get; }
```

```csharp
// ViewModels/RestoreOrder.cs (new). Public: the app has no InternalsVisibleTo (1.12), and the
// tests read its constants. Used by MainWindowViewModel alone; no interface.
public sealed class RestoreOrder
{
    public const int OpensAtOnce = 4;                     // D207's four (14.3.3)
    public static readonly TimeSpan LongestWait =         // on TabServices.RestoreClock (14.3.2)
        TimeSpan.FromSeconds(5);
    // The log lines of 14.3.8: StartedFormat, ReleasedFormat, FirstPageReason, EndedReason,
    // ClosedReason, WaitedReason, ShownFormat, DroppedFormat, StoppedFormat.

    // first: the active restored tab. restored: every restored tab, in tab order, first among
    // them. open: the window's (tab, path) => tab.OpenAsync(path, remember: false), path being
    // the tab's PendingPath as the order is made. clock: TabServices.RestoreClock (ruling 87).
    // lifetime: InitializeAsync's token.
    public RestoreOrder(
        RepoTabViewModel first,
        IReadOnlyList<RepoTabViewModel> restored,
        Func<RepoTabViewModel, string, Task<bool>> open,
        TimeProvider clock,
        IAppLog log,
        CancellationToken lifetime);

    public IReadOnlyList<Task> Start();       // once; a task per restored tab, in order (14.3.6)
    public Task FirstLoadEnded { get; }       // a task of any tab the order opened ended (14.3.6)
    public void Shown(RepoTabViewModel tab);  // a waiting tab opens at once (14.3.4)
    public void Closed(RepoTabViewModel tab); // a waiting tab never opens; the first's releases
    public void Stop();                       // nothing more opens; the waiting are dropped
}
```

```csharp
// ViewModels/TabServices.cs, the lead's (ruling 87), modelled on readClock and ReadClock: the
// constructor's last parameter TimeProvider? restoreClock = null, and RestoreClock =
// restoreClock ?? Clock, set beside ReadClock.
/// <summary>
/// The clock of the 5 s the other restored tabs wait for the active one at the most:
/// <see cref="Clock"/> unless the harness gives one, so that a test moves it or holds it still
/// while every other time of the session runs on.
/// </summary>
public TimeProvider RestoreClock { get; }
```

`AppSession.Start` gains `TimeProvider? restoreClock = null`, last in its list, and passes it to
`TabServices` as `restoreClock:`, beside `readClock:`. `ShellDriver.Start` and `StartWithTabs` gain
the same parameter, last in each list; `StartWithTabs` passes it to `Start`, and `Start` to
`AppSession.Start`. Last, because `StartWithTabs` and the overload of `StartWithTabs` in
`ShellDriver.Phase6.Workspaces.cs` hand `Start` their arguments by position. The doc comments of
`AppSession.Start` and `ShellDriver.Start` each gain a sentence for it in the form of their
`readClock` sentence ("null: the session's clock", "without it the session's own clock"), saying
what the clock is as the block above does, in the comment's last paragraph, which names no phase;
no new comment names a phase, package or section. `ShellDriver` gains no property for it: its
constructor and `ReadClock` stay as they are, and a test keeps its own reference to the clock it
gives. In the app nothing gives either clock, so `RestoreClock` is `Clock`, `TimeProvider.System`.

`MainWindowViewModel`:
- `private RestoreOrder? _restore;`, set by `InitializeAsync` and set back to null when its start
  has ended (14.3.1).
- `InitializeAsync`, after `_tabServices.Startup.TabsRestored(restored.Count)`: when
  `!cancellationToken.IsCancellationRequested && ActiveTab is { PendingPath: not null } first &&
  restored.Count > 1`, `_restore = new RestoreOrder(first, restored, (tab, path) =>
  tab.OpenAsync(path, remember: false), _tabServices.RestoreClock, _log, cancellationToken)`,
  `opening = _restore.Start()`, and the lookup waits for `[_restore.FirstLoadEnded]`; otherwise
  today's `restored.Select(tab => tab.OpenAsync(tab.PendingPath!, remember: false)).ToList<Task>()`,
  word for word, and the lookup waits for `opening`, as today. Then `_ =
  LookUpMergeToolsAfterAsync(...)` with that list; the `await
  Task.WhenAll(opening.Append(ReadActiveDashboardAtStartAsync()));` inside `try { … } finally {
  _restore = null; }`; then, unchanged, `UpdateCanClose();`. Its doc comment gains one sentence on
  the order; "Completes when every restored tab has loaded its history." stays word for word.
- `Activate(tab)`: right after `ActiveTab = tab;`, `_restore?.Shown(tab);`.
- `CloseTab(tab)`: right after `tab.RepositoryOpened -= OnRepositoryOpened;` and before
  `tab.Dispose();`, `_restore?.Closed(tab);`. The order thus hears of the close before `Dispose`
  completes the tab's `FirstPage` or cancels its load, and before the closed active tab's neighbour
  is activated.
- `CloseAll()`: right after `_closed = true;`, `_restore?.Stop();`, before any tab is disposed.

### 14.3 Behaviour

#### 14.3.1 The order and its state

The order exists only for a start whose active tab is a restored repository's tab beside at least
one other restored tab (14.3.5); it is made once, in `InitializeAsync`, after git's search, and
lives until that start has ended: `InitializeAsync` sets `_restore = null` in a `finally` around
its `await Task.WhenAll(...)`, which completes only when no entry waits any more (14.3.6), so a
later `Shown`, `Closed` or `Stop` would have nothing to do. The window keeps no closed tab alive in
any state that lasts the session (`ForgetOpened`, `KeepUntilIdleAsync`; phase 6's design 8.11), and
neither may the order. `RepoTabViewModel.Dispose` leaves `Repository` set and
`RepositoryViewModel.Dispose` leaves `Graph` set, so an order kept for the session would keep every
closed restored tab's whole history (about 58 MB of working set at 100,000 commits, by the panel's
estimate). Its state is guarded by a `Lock`, as `BulkSlots`' is, and `open` is always called
outside it: in the app every call comes on the UI thread (`InitializeAsync` and the window's
commands run there, and the order's awaits resume on the UI thread's `SynchronizationContext`), and
so do `RestoreOrderTests`, on the headless dispatcher (14.4); in `SettingsEntryTests`' two lookup
tests, which stay `[Fact]` on the thread pool (14.4), `Closed` comes on the test's thread and the
order's continuations, and the openings they start, run on pool threads beside it, and the lock
guards those calls.

It keeps one entry per restored tab, in tab order at the start, with the tab, its folder (its
`PendingPath` when the order is made), its task for `Start`'s list and one of five states:
- **Waiting**: not asked to open.
- **Opening with a slot**: opened after the release, counted among the `OpensAtOnce`.
- **Opening without a slot**: the first tab, or a waiting tab the user showed.
- **Settled**: its trigger came (the first tab's, 14.3.2; a slot's, 14.3.3): its slot, if it held
  one, is free; it may still be loading its history.
- **Dropped**: closed while it waited, or left waiting when the order stopped; never opened.

An entry forgets its tab and its `FirstPage` when `Closed` is called for its tab, and when its task
in `Start`'s list has completed. `Shown` and `Closed` then find no entry for that tab and do
nothing, as they do for a settled entry. So a tab closed during a long start (a share, D207's
minutes) is let go at its close, not when the start ends. The order keeps no list of the tabs
beside its entries (the `restored` it is given is read once), and when the last entry's task has
completed it disposes its registration on the lifetime, so the session's token keeps no reference
to it either.

And the order's own phase: **first** (the first tab opens alone), **released** (once, at the first
tab's trigger), **stopped** (once, at `Stop` or at the lifetime's cancellation; a stopped order
opens nothing more). `Stop` completes the task of every entry it has not opened, the first's
included, so a lifetime cancelled before `Start` has opened the first tab still lets
`InitializeAsync` end.

`Start` logs the started line (14.3.8), opens the first tab without a slot and watches it; the
others stay waiting. At the first tab's trigger the order logs the release line and fills the
slots (14.3.3). A waiting tab opens in tab order, the order fixed at the start, the first tab
skipped.

To open an entry it watches (the first tab, a slot's tab) the order subscribes, once, to the tab's
`RepositoryOpened`, then calls `open(tab, folder)` and keeps the task it returns; a shown tab it
watches for nothing (14.3.4), so it only calls `open` and keeps the task.
`RepoTabViewModel.ShowAsync` raises `RepositoryOpened` after `Repository = opened` and before
`opened.StartAsync()`; the handler takes `tab.Repository.FirstPage` and unsubscribes. It subscribes
before the call because a provider whose task is already complete (a test's fake) reaches
`ShowAsync` inside the call. The handler is also removed when the entry settles, when its
opening's task ends, when its tab is closed and when the order stops: an opening that fails raises
no `RepositoryOpened`, and the user's later opening in that tab, now an empty tab showing the
welcome page's error, must not reach the order. An event or a `FirstPage` for an entry that has settled
changes nothing, and an entry frees its slot at most once.

#### 14.3.2 The triggers, and where the code observes them

For the first tab, the others are released at the earliest of D457's four:

1. **Its graph has its first page**: `RepositoryViewModel.FirstPage` of the view model the tab
   shows, completed with a row in `Graph`. `FirstPage` is completed by `TrySetResult`, at the
   first of five places in `RepositoryViewModel.cs`:
   - in `LoadAsync`'s page action, posted to the UI thread through `OnUiThread`, right after
     `data.Append(page, rows)`, when `data` is `Graph` (the first load's data; a reload's is
     swapped in only at its end, so a reload's pages never meet the condition);
   - in `StartFullAsync`, right after `Graph = data.Value;`, when `Graph.Count` is above 0: a
     small history's first page can be appended before the refs' continuation sets `Graph`;
   - in `StartFullAsync`, right after `await loading;`: the first load has ended, with or without
     a page;
   - in `StartFullAsync`'s two catches, cancellation and failure, last in each (in the failure's,
     after `ErrorText` is set and `CompleteFirstRead()` is called), so that a test that awaits
     `FirstPage` reads `ErrorText` set;
   - in `Dispose`, beside `CompleteFirstRead()`.
   Neither of the existing signals serves. `FirstRead` completes once the refs and the status are
   applied, before a 100,000-commit `git log --date-order` prints its first commit (D47);
   `OnRowsDrawn` is raised by `RepositoryGraphView`'s `RowsDrawn` handler for a drawn frame, which
   the headless harness does not draw unless a test captures (`ShellDriver.WaitForAsync`'s "It does
   not render"); and `OpenAsync`'s task ends after the whole history and, when the session's filter
   named branches that are gone, after a second load (`_changedWhileStarting`).
2. **Its opening or its first load fails or ends without a page**: the task `open` returned ends
   before a first page (`OpenAsync` returns false when the provider's open fails, the folder is no
   repository or the tab closed meanwhile), or `FirstPage` completes while `Graph.Count` is 0 (an
   empty history, a failure of the refs or of the load in `StartFullAsync`, the view model closed).
3. **Its tab is closed**: `CloseTab` calls `Closed(first)` (14.2), which releases inside the call
   (14.3.4): at once, under the lock and on the caller's thread, before `CloseTab` disposes the tab
   and activates its neighbour. The close is not one of the tasks the watch awaits.
4. **5 s have passed on the session's clock**: `Task.Delay(LongestWait, clock, token)`, armed when
   `Start` asks for the first tab's opening, where D50's clock (`OpenAsync`'s stopwatch) starts
   too; `clock` is `TabServices.RestoreClock` (ruling 87), which is `TabServices.Clock` unless the
   harness gives one, as `ReadClock` is for a dashboard's 10 s rule. `AppSession.Start` gives
   `TabServices` no `clock`, and the app gives `AppSession.Start` no `restoreClock`, so in the app
   the two are one clock, `TimeProvider.System`, and the 5 s run on the session's clock to the
   letter of D457. A view-model test gives `TabServices` a `ManualTimeProvider` as `restoreClock`
   and moves it; a shell test gives one through `ShellDriver.StartWithTabs` and holds it still
   (14.4). `token` is the lifetime's, linked to a source the order cancels at the release or the
   stop, so that no timer stays armed after either.

The order awaits `Task.WhenAny` of 1, 2 and 4. Its continuation runs after the event that completed
it, never inside the page's action (`FirstPage`'s source runs its continuations asynchronously).
The release happens once, under the lock:
- `Closed(first)` releases at once with `ClosedReason`.
- Otherwise the continuation releases with the reason it finds (14.3.8): the 5 s if the delay ended
  first, the first page if `Graph.Count` is above 0, otherwise "ended without a first page".
- The release, whichever event brings it, cancels the watch's linked source, so its delay ends.
- A continuation that finds the order released or stopped does nothing.

A waiting tab's own opening counts its Q1 clock from its own start (`OpenAsync`'s stopwatch), so
its `first graph rows drawn after` line keeps D50's meaning. The first tab's first-rows line and
the start-up line are those of 14.3.6.

#### 14.3.3 The four at once

At the release, and each time a slot is freed, the order starts waiting tabs in tab order while
fewer than `OpensAtOnce` entries hold a slot. A slot is taken when the order opens a waiting tab
after the release, and freed at the earliest of that tab's own three events: its graph's first
page; its opening's task ending, or its `FirstPage` ending without a row; its close. No clock frees
a slot. D457 (5) gives the 5 s to the active tab alone, and both models it names hold a slot until
the work ends: "Add repositories" until `OpenAtTopAsync` returns
(`AddRepositoriesDialogViewModel.ConfirmAsync`, the `finally` that releases), and a dashboard read
even past `GitCallPatience` ("it keeps its slot and ends later", `DashboardRowRead`). Each freed
slot opens the next waiting tab; a stopped order opens none. A slot's close frees it inside
`Closed`, synchronously, as in 14.3.4. The other two of its events reach the order through the
slot's own `Task.WhenAny`, as the first tab's do.

Four is D207's: "Add repositories" opens `AddRepositoriesDialogViewModel.OpensAtOnce` (4) at once
through a `SemaphoreSlim`, and a dashboard reads `DashboardViewModel.MaxReads` (4) at once through
its `ReadSlots`. The order keeps a count of its own under its lock rather than a semaphore: the
next tab must be the next in tab order, and a shown tab leaves the line. The count is shared with
neither a dashboard's `ReadSlots` nor `BulkSlots`: a dashboard read meanwhile takes its own four.

A slot holds only until the first page, not until the whole load: the probe put the cost before
the first page, in the processes that the openings start and the walks before git's first line
(D457's account: the active tab's refs read after 3.2 to 6.2 s instead of 0.7 to 1.4 s), while
after it the load is git's own walk printing the rest (D47), the app's parsing and its pages on the
UI thread, which D457's probe did not find to be the cost ("not in the nine large walks"); the
probe's ten-large case measures it (14.8 step 4). So more than four tabs can be loading after their
first pages, but never more than four released tabs are opening before theirs; a tab the user shows
opens beside them, without a slot (14.3.4). A tab on a share that does not answer (D207's
minutes) holds its slot until its opening ends, as an "Add repositories" open does. Meanwhile the
next tabs wait, and the user can show any waiting tab, which opens at once (14.3.4). The lead's
probe measures the window's answers with four, and the lead may lower the constant with its
numbers (14.8 steps 4, 5); tests are written in terms of `OpensAtOnce`, so a lower value changes no
test.

#### 14.3.4 A waiting tab shown, a waiting tab closed, and what it shows meanwhile

**Shown.** Every activation passes through `MainWindowViewModel.Activate`, the one place that
sets `ActiveTab`: the tab strip, the palette's "Go to …" rows, Ctrl+Tab and the tab keys
(`AppCommands`), all through `ActivateTabCommand`; `ActivateTabShowing` (the welcome page's Open
and Open repository…, which match a tab by `RepositoryPath ?? PendingPath`); a dashboard row's
open (`OnOpenRequested`, which finds a tab by its pending folder); `ShowWorkspace`;
`ShowGitSetup`; and `CloseTab`'s neighbour. `Shown(tab)` on a waiting entry opens it at once,
without a slot and watching nothing for slots, and logs the shown line; the other waiting tabs
keep waiting for the first tab's trigger and, after it, for slots. On any other entry, or a tab
the order does not hold, `Shown` does nothing.

**Closed.** `Closed(tab)` on a waiting entry drops it: it never opens, its task in `Start`'s list
completes, and the dropped line is logged. On the first tab before the release, it releases inside
the call (reason "its tab was closed"), under the lock with the openings outside it, before it
returns. So a neighbour the release opened is already opening when `CloseTab`'s `Activate` shows
it, and `Shown` finds it opening (14.3.7 race 4, 14.4 test 4). The watch then finds the order
released. On an entry holding a slot, it
frees the slot. The tab's own opening ends as today, after `CloseTab`'s `Dispose`: `OpenAsync`
returns false once the provider answers, or the load stops.

`Shown`, `Closed` and `Stop` act synchronously. Each decides under the lock on the caller's thread
and calls `open` only after leaving the lock. So `Closed` on the first tab, or on a slot's tab, has
opened the next waiting tabs before it returns, and before `CloseTab` activates the neighbour.

**Meanwhile.** A waiting tab shows what a restored tab shows today before its repository opens, so
no text is added and no texts class changes: its folder's name in the tab strip
(`ShowPending`: `Title`, `IsOpening` true), its close button (`CanClose`, since `PendingPath` is
set), no repository; on screen, were it shown, the graph area's empty state of an opening tab
(`CommitGraphView`: no welcome page while `IsOpening`, no graph without a repository, no
`TabWaitingText`), "No repository" and "Ready" in the status bar. Since a shown waiting tab opens at
once, that state lasts as long as an opening tab's does today. It is no `IsEmpty` tab, so it cannot
become a dashboard or take an opening from the palette (`TabForOpening`).

#### 14.3.5 The cases that open every restored tab at once

No order is made, and `InitializeAsync` runs today's loop word for word, when at the openings the
active tab:
- is a dashboard (`IsDashboard`; D207 keeps its rows' reads at four, and
  `ReadActiveDashboardAtStartAsync` reads it beside the openings, as today);
- holds no repository: a tab saved without a folder, which shows the welcome page or the first-run
  screen;
- is the only restored tab (nothing waits), or no tab is restored.

Nor is one made when the lifetime is already cancelled at the openings (the window is closing):
today's loop runs, as today.

"The active tab" is `ActiveTab` when `InitializeAsync` reaches the openings, after git's search:
`RestoreTabs` mapped the saved index to a tab, and a user who switched tabs during the search (not
gated, `ActivateTab`) is on the tab that counts (14.9 choice 7). A start whose git is missing or
broken takes the order too when its active tab is restored: the first opening fails at once, the
others are released and fail as today (`FailedPath`, the welcome page's error from the missing git).

#### 14.3.6 InitializeAsync, the start-up line and the merge tools' lookup

`Start` returns one task per restored tab, in `restored`'s order. A tab's task completes when the
task its `open` returned completes, with that task's outcome (the same result, fault or
cancellation, as today's list would carry); a dropped tab's completes when it is dropped. So `await
Task.WhenAll(opening.Append(ReadActiveDashboardAtStartAsync()))` completes when every restored tab
has loaded its history, failed, or was closed, as today, and `InitializeAsync`'s contract and every
wait built on it hold: `ShellDriver.WaitUntilReadyAsync` (`Session.Initialization`),
`WaitForRepositoryAsync` and `WaitForTabOpenedAsync`, which call it, `Phase6Checks.W.Restart`'s
assertion that `Initialization` completed, and M3's wait for ten `loaded` lines. No harness wait
changes. A tab is waiting at most until the first tab's trigger, 5 s at the latest, and then until
a slot frees, at a released tab's first page, the end of its opening or its close, so the start
ends when the openings end, as today's does; a view-model test whose restore clock stands still
ends it by the events it makes.

The start-up line counts every restored tab: `TabsRestored(restored.Count)` is called before the
order is made, unchanged, so M3's line still reads `(10 restored tabs)`. The timeline's first-rows
line is written at the first `FirstRows` call, which is the first tab's while it is shown, as today.

The merge tools' lookup still starts when the first restored load ends (D202):
`LookUpMergeToolsAfterAsync` gets `[_restore.FirstLoadEnded]` in place of the order's tasks, and
starts at it; `MainWindowViewModel.Settings.cs` is unchanged. `FirstLoadEnded` completes when the
first task of a tab the order opened ends, whether it showed its graph or failed: the first tab's,
a released tab's or a shown tab's, whichever ends first. A waiting tab that is closed never loaded,
so its drop completes its task for `InitializeAsync` but not the lookup's wait: the lookup does not
start while the active tab still opens alone. If the order stops before it has opened any tab
(14.3.1), `FirstLoadEnded` completes at the stop; once it has opened one, `FirstLoadEnded`
completes when the first such task ends: a tab's task ends when its opening ends, at the latest at
its disposal while its history loads (`RepositoryViewModel.Dispose` cancels the load), or, while
the provider still opens it, only when the provider answers (`RepoTabViewModel.OpenAsync` awaits it
without a token, then returns false; 14.3.4). The lookup runs below normal priority (D202), beside
the others' openings.

#### 14.3.7 The races, each with what happens

1. **First page and 5 s together**: the first handled under the lock releases; the second finds the
   order released and does nothing. The same holds for a slot's events (its first page and its
   opening's end together).
2. **Shown while the release opens it**: `Shown` and the release both decide under the lock; the
   tab opens once, by whichever comes first; the other finds it opening and does nothing.
3. **Closed while waiting, at the release**: a tab dropped before the release reaches it never
   opens; one already opened by the release is an opening tab that closes: its close frees its slot,
   and its opening ends as today (14.3.4).
4. **The active tab closed while it opens**: `CloseTab` calls `Closed` before it disposes the tab.
   `Closed` releases ("its tab was closed") and opens waiting tabs in tab order into the free slots.
   Then `CloseTab` disposes the tab; its `FirstPage`, or its opening's end when the provider
   answers, finds the order released. Last, `CloseTab` activates the neighbour.
   - A neighbour the release opened is already opening, so `Shown` does nothing (14.4 test 4:
     `[b, a, c]`).
   - A neighbour still waiting (one beyond the `OpensAtOnce` the release opened) is shown and opens
     at once, without a slot.
5. **The window closes while tabs wait**: `CloseAll` calls `Stop` before it disposes the tabs, and
   `AppSession.Dispose` cancels the lifetime first, which stops the order too. A stopped order
   opens nothing more, drops every waiting tab (the stopped line, 14.3.8) and cancels its delay;
   `InitializeAsync` completes once the opened tabs' tasks end: their disposal ends a load, and an
   opening that still waits on the provider ends when the provider answers (14.3.6; 14.4 test 10's
   `Fail(b)`).
   Today the real-window pass closes a three-tab start while the large tab still loads (phase 1's
   check 15, its restart). Under the order `graph`, the active tab there, opens first, and its
   first page (13 commits) releases `linear` and the 100,000-commit tab within moments; the pass
   waits for `graph`'s `loaded` line, which comes after that first page, so both are opening when
   the close comes, and the window still closes while the large tab loads, as today.
6. **A tab shown before the release**: it opens at once; its first page releases nobody; the others
   wait for the first tab's trigger.
7. **The user leaves the first tab before its trigger** (another tab, a dashboard): nothing
   changes; the first tab's trigger still releases the others, 5 s at the latest.
8. **A dashboard read meanwhile**: a background dashboard starts no read of its own (D207); shown,
   it reads its rows, and a row whose folder is a waiting tab's finds no registered tab
   (`OpenRepositories.TabForFolder` knows view models only), so it reads through a summary-mode
   view model, which the tab adopts when it opens (`TryAdopt`, `Promote`): phase 6's protocol, as
   today for a row read while a restored tab is still opening. A row's open request for a waiting
   tab's folder shows that tab, which opens at once (14.3.4).
9. **"Check again" while tabs wait**: it reopens only tabs with a `FailedPath` that are not opening
   (`OpenWhatWaitedForGitAsync`); a waiting tab has a `PendingPath` and `IsOpening`, so it is left
   to the order.
10. **Two restored tabs on one worktree**: they open one after the other when one of them is the
    first tab, side by side at the release otherwise; `ShowAsync`'s registry rules (D209, 3.3 of
    phase 6's design) hold either way, and when one of them is the first tab, the first registered
    tab now is the active one.
11. **A slot's tab that does not answer** (a share): its slot stays taken until its opening ends or
    the tab is closed; the next tabs wait, as behind an "Add repositories" open; a waiting tab the
    user shows opens at once (14.3.4).
12. **Dialogs' names during the wait**: `NamesRepositoryInDialogs` counts tabs with a repository,
    so while only the first tab has one, a dialog that its own operation opens names its repository
    only if a dashboard is open; today that holds in the first moments of every start with several
    tabs, and the order lengthens it to the first tab's trigger, 5 s at most (14.9 risk 3).
13. **Add open tabs during the wait**: `OpenRepositoryFolders` lists tabs with a repository, so a
    dashboard's Add open tabs leaves out the waiting tabs until they open
    (`WorkspaceCommands.AddOpenTabs`), as today for a tab still opening; until the first tab's
    trigger, 5 s at most, and then a free slot (14.9 risk 3).

#### 14.3.8 What the app logs

Through the session's log, as constants of `RestoreOrder` (each line says only what happened,
D438; `{N}` on the order's clock, `TabServices.RestoreClock`, the session's in the app, since the
first tab began opening, in whole milliseconds rounded down, invariant culture, no group
separator, as `StartupTimeline`'s):
- Info, once, at `Start`: `StartedFormat`, `Restore order: {first} opens first; the other restored
  tabs ({k}) wait for its first page, at most 5 s, then open at most {OpensAtOnce} at once (a tab
  shown meanwhile opens at once).`
- Info, once, at the release: `ReleasedFormat`, `Restore order: {first}: {reason}, {N} ms after it
  began opening; the other restored tabs ({k}) start opening, at most {OpensAtOnce} at once.`, with
  `{k}` the count still waiting at the release, the reason one of `FirstPageReason` `its graph has
  its first page`, `EndedReason` `its opening ended without a first page`, `ClosedReason` `its tab
  was closed`, `WaitedReason` `5 s passed without a first page`.
- Debug: `ShownFormat` `Restore order: {tab} was shown while it waited and opens now.`;
  `DroppedFormat` `Restore order: {tab} was closed while it waited and does not open.`;
  `StoppedFormat` `Restore order: the window closed with {k} restored tabs still waiting; they do
  not open.` (only when one waits).

`{first}` and `{tab}` are the tab's folder name as `ShowPending` shows it
(`WorkspaceTexts.FolderName`). The lines name no path. No earlier test or measure reads them: the
patterns of Q1's, the `loaded`, the first-rows and the start-up lines (`Phase8aPerformance`,
`Phase1RealWindowPass`, `AppLogFile`) match none, and the lead's probe adds `Restore order:` to the
lines it keeps (14.8 step 4).

### 14.4 Tests

The builder runs these classes and methods only, with plain `dotnet test` (D245). The first
commit holds the tests and stubs of 14.2's members (`FirstPage` a task that never completes,
`RestoreOrder` with its constants and a `Start` that opens every tab at once; `InitializeAsync`
not yet calling it) and ruling 87's `restoreClock` in its three files, complete, since the tests
give it; every test below marked "fails first" fails on that commit, `SettingsEntryTests`'
two among them; the merge note names that commit. Every wait on a task in the view-model classes
below (`FirstPage`, `InitializeAsync`, `lookups.Started`) is bounded
(`WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)`), so that on the stub
commit each test fails on its own timeout, not through the hang watchdog. The fakes' commits are by `Demo Author <demo@example.com>`;
folders are under `Path.GetTempPath()`'s `restored`, never made, as `SettingsEntryTests` has them,
and the real repositories are `TempRepo`s.

**`RestoreOrderTests`** (App.Tests, new, `[AvaloniaFact]`, no session, no git). The lab is a window
over fakes on the headless dispatcher, as `CheckGitAgainTests`' `Lab` and
`DashboardTestSupport.Window` make one. The order's continuations, the tabs' openings (`ShowAsync`,
`OpenRepositories`, which is used on the UI thread only) and the window's handlers then run on one
thread, as in the app, and never on two at once. Its `TabServices` is made as
`SettingsEntryTests.Shell` makes it, given the lab's provider, a `ManualTimeProvider` as
`restoreClock` (ruling 87; not as `clock`, which stays the system's, as in `CheckGitAgainTests`'
`Lab`), a `RecordingLog` given both to `TabServices` (`log`) and to the `MainWindowViewModel`
(`log:`), since the order writes through the window's `_log` (`MainWindowViewModel.cs:68`), and,
where a test names one, an `InMemoryWorkspaceStore`; git found at once
(`_ => Task.FromResult(GitFound)`), so `InitializeAsync` reaches the openings before it returns, as
`SettingsEntryTests` relies on. The provider, `HeldRestores : IRepositoryProvider`, records each
`OpenAsync` path in `Opened`, in call order, and holds it until the test answers:
`Answer(path, history)` completes it with a `RestoreFake : FakeRepositoryBase` over that folder,
`Fail(path)` with an `IOException`; `HeldRestores` keeps `Opened` and its openings under a lock.
After each `Answer`, `Fail`, `SendFirstPage`, `End`, `Advance` and command the test runs the
dispatcher's jobs.
`WaitForOpenedAsync(count)` and every wait for a positive result go through
`DashboardTestSupport.WaitUntilAsync`, which runs the dispatcher's jobs and fails after 60 s. A
`RestoreFake`'s `ReadRefsAsync` gives `main` at its one commit, or throws when its history says so;
its `LoadCommitsAsync(refs, onPage, …)` hands its commit at `SendFirstPage()` and ends at `End()`
(with no page for a history without commits, or throwing for one whose load fails);
`CreateWatcher()` a quiet watcher, as `RepositoryViewModelFilterTests.PagedRepository`'s.
`Task.Delay(TimeSpan, TimeProvider, CancellationToken)` over `ManualTimeProvider` fires inside
`Advance`, as `SearchViewModel`'s delay does under `SearchViewModelTests`; what it resumes is posted
to the context the await captured, so the test pumps after each `Advance`. A negative assertion is
made after `DashboardTestSupport.SettleAsync`, which runs what was posted, at a sync point that the
manual clock keeps true: with the restore clock unmoved and no page sent, nothing but the test can
release. Timers: the restore clock carries the order's timers alone, since auto-fetch's timer and
every other time of the window run on `TabServices.Clock`. So the tests' statements about timers
are about the restore clock's `ArmedTimers`: one armed while the first tab is watched before the
release (test 2), none after the release (test 2) or the stop (test 10), and none at all in a start
that makes no order (test 12).

Fail first:
1. `Behind_an_active_restored_tab_only_its_opening_is_asked_for_at_first`: tabs `a`, `b`, `c`, `b`
   active; `InitializeAsync` started; `Opened` is `[b]`. (Today `[a, b, c]`.)
2. `The_others_open_in_tab_order_once_the_active_tabs_graph_has_its_first_page`: as 1; `b`
   answered; once `b`'s `Refs` are read, `Opened` is still `[b]` and the restore clock's
   `ArmedTimers` is 1; `b`'s first page; `Opened` becomes `[b, a, c]` and `ArmedTimers` is 0 (the
   release cancelled the delay); `InitializeAsync` is not complete until all three loads have
   ended, and completes when they have, every tab with its graph; the log has the started line with
   `(2)` and the release line with `FirstPageReason` and `0 ms` (the restore clock stood still).
3. `When_the_active_tabs_opening_or_first_load_fails_or_ends_without_a_page_the_others_open`
   (theory: the provider's open fails; the history has no commits and its load ends; the refs read
   throws; the load throws): `[b, a, c]`, the reason `EndedReason`.
4. `Closing_the_active_tab_while_it_opens_opens_the_others`: `CloseTabCommand` on `b` before its
   opening is answered; `[b, a, c]`, the reason `ClosedReason`; `c`, the neighbour now active,
   opened once (released before `Activate` shows it); `Fail(b)` ends `b`'s opening;
   `InitializeAsync` completes once `a`'s and `c`'s loads end. `Closed` releases synchronously
   inside `CloseTab` (14.3.2 item 3), so the order is deterministic.
5. `After_5_s_on_the_restore_clock_the_others_open_without_a_first_page`: `b` answered, no page;
   `Advance` 4,999 ms: `[b]`, and the restore clock's `ArmedTimers` is still 1; 1 ms more: `[b, a,
   c]`, the reason `WaitedReason`, `5000 ms`; then `b`'s page changes nothing.
6. `At_most_OpensAtOnce_open_at_once_and_each_freed_slot_opens_the_next_in_tab_order`: the active
   tab first, then `OpensAtOnce + 2` waiting; after its first page exactly the first `OpensAtOnce`
   waiting are asked; one of them sends its first page: the next opens; another fails: the last
   opens; a third is closed, and its held opening is then failed (`Fail`), since a closed tab's
   opening ends only when the provider answers: nothing more opens (none waits); all end and
   `InitializeAsync` completes.
7. `No_slot_is_freed_by_the_clock`: the active tab and `OpensAtOnce + 1` waiting; after its first
   page the first `OpensAtOnce` waiting are asked; `Advance` 10 s (twice `LongestWait`): `Opened`
   still holds `OpensAtOnce + 1` paths; one of the asked sends its first page: the last opens. It
   fails first, because the stub opens every tab at once.
8. `A_waiting_tab_that_is_shown_opens_at_once_and_the_others_still_wait`: `a`, `b`, `c`, `b`
   active, answered, no page; `ActivateTabCommand` on `c`: `[b, c]` and the shown line; once
   `c`'s refs are read, still `[b, c]`; `c`'s first page sent before `b`'s: still `[b, c]` and no
   release line; `b`'s first page: `[b, c, a]`.
9. `A_waiting_tab_that_is_closed_never_opens_and_the_start_still_ends`: `CloseTabCommand` on `a`
   while it waits: the dropped line; `b`'s first page: `[b, c]`, never `a`; `InitializeAsync`
   completes once `b`'s and `c`'s loads end; the session's tabs are `b` and `c`.
10. `Closing_the_window_while_tabs_wait_opens_none_of_them` (theory: `CloseAll`; the lifetime's
    token cancelled): `[b]`; the stopped line with `2`; `InitializeAsync` completes once `b`'s
    opening ends (`Fail(b)`); no timer is left armed on the restore clock.

Pass on today's code by design, as guards of "as today":
11. `With_a_dashboard_or_an_empty_tab_active_every_restored_tab_is_asked_for_at_once` (theory: a
    dashboard active, with a workspace store; an empty tab active): six restored tabs, all six in
    `Opened` as soon as `InitializeAsync` has started; no `Restore order:` line.
12. `A_lone_restored_tab_opens_as_today`: one restored tab, active: asked at once; no `Restore
    order:` line; no timer armed on the restore clock.
13. `The_start_up_timeline_counts_every_restored_tab_before_any_opens`: a recording
    `IStartupTimeline` given to `TabServices` (`startup`): `TabsRestored(3)` is recorded while
    `Opened` is empty.
14. `A_restored_tab_that_is_closed_is_let_go` (theory: closed while it waits; answered, loaded and
    closed after `InitializeAsync` has completed). The tab is closed through `CloseTabCommand` in a
    `[MethodImpl(MethodImplOptions.NoInlining)]` helper that returns only a `WeakReference` to it,
    as `DashboardTabTests.CloseSecondTab` does. The test fails each opening the provider holds, as
    they come, until `InitializeAsync` completes, except, in the second case, the opening of the tab
    it closes, which it answers and loads (on today's code and on the stub the first case's closed
    tab is among those failed, since they ask for it at once). Once `InitializeAsync` has completed,
    after at most five rounds of `GC.Collect()`, `GC.WaitForPendingFinalizers()` and `SettleAsync`,
    the reference is no longer alive, as
    `DashboardTabTests.A_tab_a_dashboard_opened_is_let_go_when_it_closes` asserts. It passes on
    today's code and on the first commit's stub; it fails on an order that keeps its entries' tabs
    for the session (14.3.1).

**`RepositoryFirstPageTests`** (App.Tests, new, `[Fact]`, a `RepositoryViewModel` over a fake,
made as `RepositoryViewModelFilterTests`' test
`A_comparisons_link_follows_the_pages_of_a_graph_that_still_loads` makes one, with
`MainWindowViewModelTests.FakeSettings`). Fail first (the stub never completes; each wait on
`FirstPage` is bounded, as above, so on the stub each test fails with a timeout):
1. `FirstPage_completes_when_the_first_page_is_in_the_graph_and_not_before`: `StartAsync`; once
   `Refs` is read, `FirstPage` is not complete; the first page: it completes, `Graph.Count` is 1
   and `Graph.IsComplete` false.
2. `FirstPage_completes_when_the_first_load_ends_without_a_page` (theory: no commits; the refs
   read throws; the load throws): it completes, never faulted; `HasNoCommits`, or `ErrorText` set.
3. `FirstPage_completes_when_the_view_model_closes_before_a_page`: `Dispose` after the refs read.
4. `A_summary_view_models_FirstPage_waits_for_its_promoted_start`: `ForSummary` over a
   `TabServices` with a fake provider; its summary `StartAsync` completes `FirstRead`, not
   `FirstPage`; `Promote` and `StartAsync`, then the first page: `FirstPage` completes.

**`RestoreOrderShellTests`** (App.Tests, new, `[AvaloniaFact]`, real git, `ShellDriver`). Fail
first (today no `Restore order:` line is written). Both start with `restoreClock:` a
`ManualTimeProvider` they never move (ruling 87, 14.9 choice 4), so no release can come from the
clock between their steps, while the session's own clock runs on as in any shell test:
1. `Behind_an_active_repository_tab_the_others_open_after_the_release_and_every_history_loads`:
   three `TempRepo`s `first`, `second`, `third` of one commit each; `StartWithTabs(data, [first,
   second, third], activeTab: 1, restoreClock: clock)`; `WaitUntilReadyAsync`: each tab's graph
   complete, `second` active; `app.Session.Services.RestoreClock` is that clock (`Assert.Same`, as
   `ShellTests` asserts the read clock's), so a driver or a session that did not pass it on fails
   here; in `LogLines()`, one started line naming `second` with `(2)`, one release line, its reason
   `FirstPageReason` (each repository has one commit, and the clock stands still); the `first:
   opened after` and `third: opened after` Debug lines come after the release line. The release line
   is written before any waiting tab is opened, so the order of lines holds on a slow machine too.
2. `A_restored_tab_shown_while_it_waits_opens_at_once`: as 1, with `held.HoldNext("rev-parse")`
   called before `StartWithTabs`, on a `HeldGitRunner` given as `gitProbe: held.Probe` (`Start`
   pumps the UI thread before it returns, so a hold set after it would miss `second`'s call);
   `held.Started`, awaited with `WaitAsync(ShellDriver.DefaultWaitTimeout, …)` as
   `KeyboardPassTests` awaits it, is `second`'s opening (the held command's folder is `second`'s);
   at once `ActivateTabCommand` on the third tab; the shown line for `third` in `LogLines()` at
   once (`Shown` writes it inside the command); then, before `held.Release()`,
   `WaitForLogLineAsync` for `third: opened after` within `ShellDriver.DefaultWaitTimeout` (60 s,
   D237), which in `LogLines()` comes after the shown line and before any release line (second's
   first git call is still held, so only an at-once open writes it there);
   `held.Release()`; `WaitUntilReadyAsync`: every graph complete; `first: opened after` after the
   release line. The hold is the order's first git call; while it holds, `second` can neither
   show a page nor end its opening, and the clock stands still, so nothing can release the others
   before `third`'s `opened after` line, however slow the machine. Today the held call may be any
   tab's, and no shown line comes.
These join 0.4's lagged list at restore's merge, beside the App.Tests classes 8a extends with
real git (14.7).

**The earlier test that changes** (its reason once in 8a's report, "Changes to expected
results"):
`SettingsEntryTests.The_merge_tool_lookup_starts_once_when_the_first_restored_tabs_load_ends`
asserts `Assert.Equal([first, second], repositories.Opened)` right after `InitializeAsync` starts
and fails `second` first. Rewritten, same name, in the first commit with the new tests; it fails
there, since `Opened` is `[first, second]` on that commit: `first` active; `Opened` is `[first]`
and no lookup has started; `Fail(first)`: the lookup starts (`lookups.Started`), and `second` is
asked (a poll of at most 30 s until `Opened` is `[first, second]`); `Fail(second)`:
`InitializeAsync` completes; `lookups.Count` is 1. Its comment names the active tab's load as the
first that ends and ruling 76 for the second's later opening. The reason: ruling 76 opens the
active restored tab first and alone (D457 (5)). The class stays `[Fact]`: its tabs' openings only
fail, as today's test's do on the thread pool, and `HeldRepositories` gains a lock around `Opened`
and its openings (today a plain `List` and `Dictionary`), since the order now asks for `second`
from its continuation.

Beside it, new and in the same first commit,
`Behind_a_closed_active_restored_tab_the_lookup_waits_for_a_load_to_end`, which proves that the
lookup follows the first load to end, whichever tab's (D202), and not a waiting tab's drop (14.3.6):
three tabs `first`, `second`, `third`, `first` active; `Opened` is `[first]`; `CloseTabCommand` on
`third` while it waits: no lookup has started; `CloseTabCommand` on `first` while its opening is
held: `second` is asked (`Opened` is `[first, second]`, never `third`) and no lookup has started;
`Fail(second)`: the lookup starts (`lookups.Started`) while `first`'s opening is still held;
`Fail(first)`: `InitializeAsync` completes; `lookups.Count` is 1. It fails first, since `Opened` is
`[first, second, third]` at the start on that commit. For both, `Shell` gives `TabServices` a
`ManualTimeProvider` as `restoreClock` (ruling 87), which the tests never move, so the 5 s cannot
come between their steps. The class's third test of the lookup,
`Without_a_restored_repository_the_lookup_starts_once_git_has_been_looked_for`, is unchanged.

**Earlier tests that restore tabs and stay unchanged**, each read for this section, with why it
holds:
- Behind an active repository tab, each waiting on `InitializeAsync` (directly, or through
  `WaitUntilReadyAsync`, `WaitForRepositoryAsync` or `WaitForTabOpenedAsync`) before anything it
  asserts or captures:
  - App.Tests: `ActivityLogViewTests`' two starts of two repositories
    (`A_rows_menu_copies_and_each_tab_shows_its_own_log_with_its_rows_left_open`;
    `A_repository_opened_through_a_link_shows_its_openings_reads_under_Show_reads`, whose order
    of reads is within one worktree); `DialogRepositoryNameTests`; `TabKeysTests`'
    `In_the_resolvers_result_Ctrl_Tab_leaves_it_and_Ctrl_PageDown_switches`; `ShellDriverTests`'
    `StartWithTabs_writes_a_session_file_with_those_tabs_before_the_app_starts`.
  - Hosting.Tests: `RequestOperationsProvidersTests`'
    `Open_copy_and_refresh_ask_only_the_listed_fake_per_provider` and
    `Signed_out_nothing_is_asked_of_either_provider`.
  - Phase 1: `Phase1Checks.Check_15_three_tabs_restored_after_a_restart`.
  - Phase 6: `Check_K13_keys_in_the_conflict_resolver`; part L:
    `Check_L33_tab_strip_shrinks_then_scrolls` (scratch active, six repository tabs and the
    dashboard behind).
  - Phase 7a: `Check_P2_P11_P4_P3_P1_signed_out_failed_loading_and_not_github` and
    `Check_P13_P6_checkout_and_a_forks_tab` (P13 waits for the background tab's pull requests
    after the sign-in, which comes after `WaitForRepositoryAsync`).
  - Phase 7b: `Check_P2_P10_P13_P4_P3_P8_azure_signed_out_failed_loading_and_other_organization`.
  - Phase 8a: `KeyboardPassTests`'
    `A_keyboard_Stage_held_while_the_user_goes_to_another_tab_leaves_that_tabs_lists_alone` and
    `CtrlTabFromAsync`'s four callers.
- With a dashboard or an empty tab active, so no order is made: `SettingsEntryTests`' toolbar
  test, `ActivityLogViewTests`, `CommandPaletteViewTests` and `ToolbarTests`' starts with an empty
  second tab active, `DashboardSessionTests`, `DashboardTabTests`, phase 6's
  `Check_W1_add_open_tabs_rename_and_delete` and `Phase6SupportTests`' tab restored behind the
  dashboard, 8a's `AboutEntryTests`.
- With one restored tab: `RepoTabsTests`, `CheckGitAgainTests`, `MainWindowViewModelTests`,
  `AppSessionTests` (a repository tab and a dashboard), `Phase6Checks.W.Restart` (W9) and every
  other walk-through's start.
- The real window:
  `Phase1RealWindowPass.Checks_15_and_16_tabs_window_and_panels_across_a_real_restart` waits for
  the three `loaded` lines, then on the restart for `graph`'s, and its captures show the tab
  strip's folder names whether the others still wait or have opened; `Phase8aPerformance`'s M3
  waits for its first-rows line and the ten `loaded` lines (both builds alike).

### 14.5 Visual checks

None: the package has no part (0.5). The order changes when restored tabs open, not what a screen
shows once the start has ended or while it runs: a waiting tab shows what an opening restored tab
shows today (14.3.4), and no text, control or state is added. The gate's existing checks that
restore several tabs behind an active repository tab cover it, each with its pictures unchanged:
the scripted checks take them after `InitializeAsync` (14.4's first list), and the real window's
check 15 takes 15c after `graph`'s `loaded` line, while the other two tabs may still wait or open,
which the tab strip shows as it shows opened tabs (14.4's last item): phase 1's check 15 in both
harnesses, whose real-window captures are compared with the scripted ones; part L's L33; phase 6's
K13; 7a's P2 and P13; 7b's P2 on Azure DevOps. The order itself is proven by 14.4's tests, by M2 and
M3 and by the lead's probe (14.8).

### 14.6 Earlier pictures

None may change, and none should (14.5). The builder proves it against the references that are
current, wave 3's (`D:/Projects/Personal/phase8-work/references/wave3/`): each command below, then
`python scripts/compare-pictures.py D:/Projects/Personal/phase8-work/references/wave3 artifacts/visual <folder>`
with `<folder>` outside the repository (`D:/Projects/Personal/phase8-work/wave4/restore/compare/`),
before the next command, since a run empties its phase's folder:
- `dotnet test --project tests/VisualCommit.VisualTests --filter-method "*Phase1Checks.Check_15*"`
- `dotnet test --project tests/VisualCommit.VisualTests.Phase6 --filter-method "*Check_K13_keys_in_the_conflict_resolver*"`
- `dotnet test --project tests/VisualCommit.VisualTests.Phase6Look --filter-method "*Check_L33*"`
- `dotnet test --project tests/VisualCommit.VisualTests.Phase7a --filter-method "*Check_P2_P11*" --filter-method "*Check_P13_P6*"`
- `dotnet test --project tests/VisualCommit.VisualTests.Phase7b --filter-method "*Check_P2_P10_P13_P4_P3_P8_azure*"`

Of the pictures each run wrote, none may be listed as changed; a difference stops the builder,
who asks the lead (ruling 3). The lagged list at the merge (14.7), which runs phase 7a's, 7b's and
8a's walk-throughs whole and phase 6's K13 among its checks, and the whole default run at wave 4's
close confirm it for every phase.

### 14.7 Lagged list, time, real window

**Lagged list.** The order is on the start path of six classes of 0.4's list (phase 6's K13, 7a's
P2 and P13, 7b's P checks, `RequestOperationsProvidersTests`, 8a's `KeyboardPassTests`), so the
lead runs 0.4's list at restore's merge, with the App.Tests command gaining
`--filter-class "*RestoreOrderShellTests"`, no builder at work (ruling 23). The watcher's lag holds
file-system events; the order reads none, so the lag moves only what a tab does after it opened.
`RestoreOrderShellTests` hold their restore clock still (ruling 87), so no lag can bring the 5 s
between their steps.

**Default run.** App.Tests gains about 29 tests: `RestoreOrderTests` (on the headless dispatcher),
`RepositoryFirstPageTests` and `SettingsEntryTests`' new test without git or a window, seconds in
all; `RestoreOrderShellTests`, two starts with real git, about 10 to 20 s. Each start of the
default run with several restored tabs behind an active repository tab (about 20 in 14.4's first
list, with phase 1's restart and `CtrlTabFromAsync`'s callers) opens the others after the active tab's first page instead of beside it: a fraction of a second
each on small repositories, an estimate; phase 1's check 15 opens the 100,000-commit tab after
`linear`'s first page.

**Real-window pass.** Nothing new; phase 1's check 15 changes only in the order of its openings.

**The proof's cost on the desktop** (14.8; the owner told in the chat just before each, as the
owner asked on 2026-11-23 for wave 4's takeovers): the two builds made at least ten minutes
before; the probe, two warm-up launches and 18 counted ones of about 10 to 30 s with no input,
about 9 minutes; if two is tried, two builds (two's in the fix's worktree, four's in the third,
14.8 step 5), ten minutes and a session of its own of 12 launches (three warm-ups and nine
counted), about 6 minutes, and, if four then stays, a revert, a build and ten minutes more; the
alternated series, ten runs of M2 and M3 at one run each, about 2.5 to 3 minutes per run with the
clones, about 30 minutes, more for each pair a scheduled task makes the lead take again (14.8 step
6); about 50 minutes of the desktop in all without the trial of two, about 70 with it, about 80
when four stays after it, nothing beside it (ruling 23), so §11's builder pauses or has ended.
13.3's row for the proof named about half an hour for the series alone. The lead brings that row
and 0.6's series line to these times in the commit that puts the section into the design, as
ruling 90 has it.

### 14.8 Done when

1. **The builder.** 14.4's tests committed first with 14.2's stubs, failing on that commit as 14.4
   marks, `SettingsEntryTests`' rewritten test and its new one among them; then 14.3 built; 14.4's
   classes and `SettingsEntryTests` pass (`dotnet test --project tests/VisualCommit.App.Tests
   --filter-class "*RestoreOrderTests" --filter-class "*RepositoryFirstPageTests" --filter-class
   "*RestoreOrderShellTests" --filter-class "*SettingsEntryTests"`), and so do, run by class or method, the App.Tests and 8a's tests of
   14.4's first list; no warning; 14.6's comparisons as named; the reason for the report's "Changes
   to expected results" drafted.
2. **The review.** An independent review of the branch's diff, started by the lead, each finding
   verified, the confirmed fixed with a test, before the proof, so that the proven build is the one
   that merges.
3. **The lead: two builds.** The base, the commit `wip/p8a-restore` was made from
   (`git merge-base phase/8a-release-readiness wip/p8a-restore`), detached in
   `D:/Projects/Personal/vc-wt/p8a-restore-base`; the fix, the branch's last commit, in
   `D:/Projects/Personal/vc-wt/p8a-restore`. In each,
   `dotnet build tests/VisualCommit.RealWindowTests -c Release`, which builds the app's Release;
   at least ten minutes before step 4; no agent at work (ruling 23); nothing committed in either
   worktree until step 6 ends but step 5's commits, after which this step is taken again for the
   fix; Windows' Task Scheduler read for what is due in the next ten minutes (CLAUDE.md's note),
   again before each later step that starts more than ten minutes after the last reading, before
   and after each run of step 6, and at the end for what ran.
4. **The lead: the probe** (D457's, by hand, outside any test). A copy of
   `startup_tabs_probe.ps1` in `D:/Projects/Personal/phase8-work/wave4/restore/probe/`, its runs
   written there, its clones those of `wave3/startup10-probe/clones/` (its `-Prepare` reports any
   commit-graph first), `Restore order:` added to the lines it keeps, `-Exe` each build's
   `VisualCommit.exe` in its worktree's Release output. Each build first makes one uncounted
   warm-up launch (`-Label warm`, as wave 3's probe did), since a first start after a build is the
   coldest (D437); then three rounds of the three cases, `-Tabs 1` (one large), `-Tabs 10` (ten
   large), `-Tabs 10 -Background small` (one large and nine of 2,000 commits), with the builds'
   order alternating per round (base then fix, fix then base, base then fix), since the laptop's
   warmth spreads the numbers (D436); 18 counted launches. Noted per launch: the active tab's own
   clock (`large-clone-0: first graph rows drawn after {N} ms`, D50's), the window's answers
   (median, 95th percentile, longest, the count of 50 ms or more), when every `loaded` line was
   seen, and the fix's two `Restore order:` lines.
5. **The lead: the four** (ruling 88). Four stays when the middle value of the longest answers of
   the fix's three ten-large launches is at or under the highest of the longest answers of the
   base's three one-tab launches of step 4's session. The middle value, because a launch's longest
   answer is a maximum, which one scheduled task or one collection can make, and one disturbed
   launch must not start a trial that costs three builds and their rests. Otherwise the lead first
   detaches a third worktree, `D:/Projects/Personal/vc-wt/p8a-restore-four`, at the fix's commit,
   which keeps four's build; a fixer makes a commit of `OpensAtOnce = 2` alone on
   `wip/p8a-restore`; the lead builds it in the fix's worktree, and four's in the third, as step 3
   says. A trial of two is a session of its own: one uncounted warm-up launch of each of the three
   builds, then three rounds of the base's one-tab case, four's ten-large case and two's ten-large
   case, the three builds' order rotating per round; the rule reads all of them from that session.
   The trial reads the same statistic for two's and four's ten-large launches against the base's
   one-tab launches of that session, and two stays only if it meets the rule above and the median
   of its active tab's own clock is no later than four's; else four stays. When four stays after
   two was tried, a second commit reverts the first on `wip/p8a-restore`, and step 3 is taken again
   for the fix (built, ten minutes, the Task Scheduler read) before step 6. The numbers go into the
   report either way (14.9 choice 10).
6. **The lead: the alternated series.** Ten counted runs, base first, alternating, five per build,
   each in its worktree. Before each run the lead reads what is due in the next ten minutes and
   waits out anything due; after each run, what ran since it began. A run that met a scheduled task, or
   failed other than by M2's bound, is taken again together with the other build's run beside it,
   so that each build keeps five undisturbed counted values; the dropped runs' files go to
   `D:/Projects/Personal/phase8-work/wave4/restore/series/dropped/` and the report names them. The
   command:
   `VISUALCOMMIT_PERF_RUNS=1 bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release --filter-method "*Phase8aPerformance.M2_*" --filter-method "*Phase8aPerformance.M3_*"`.
   Each run writes one M2 and one M3 counted value (after a warm-up each) into its own
   `artifacts/perf/phase-8a/series-<date>-<commit>.txt`; the first run checks that the file holds
   M2's and M3's lines alone (a new form of the command; CLAUDE.md gains its row once it works).
   The ten counted runs' files are copied to `D:/Projects/Personal/phase8-work/wave4/restore/series/`. A counted
   M2 above 6,100 ms fails its fact after its value is written; it still counts, and on the base it
   means the bound was drawn too tight (D457 (3)), which the lead raises by a ruling with the
   numbers.
7. **The rule** (D457 (5)'s): from each build's five M2 and five M3 values, the medians and the
   ranges. Proven when the fix's M3 median is at or under 1.25 times the fix's M2 median, the
   base's M3 median is above 1.25 times the base's M2 median, and the fix's M2 median lies between
   the lowest and the highest of the base's five M2 runs (ruling 89, 14.9 choice 11).
8. **Proven: the merge.** The lead merges `wip/p8a-restore` (the lagged list of 14.7; CI's run on
   the push green in all six jobs, the floor's included); writes M3's bound in a commit of its own,
   the fix's slowest counted M3 run times 1.25, rounded up to 100 ms (`PerformanceSeries.Bound`),
   in `Phase8aPerformance.M3BoundMilliseconds` (D457 (3), 13.4 item 17); and fills the report's
   table (M3's "after", the alternation's ten values, the probe's numbers, the four as kept),
   plan.md's risk 10 (the medians), architecture.md and 1.17's "as built".
9. **Not proven: no merge.** The phase branch never holds the change, so nothing is reverted there;
   `wip/p8a-restore` stays local (D396); the lead writes plan.md's Backlog row "The restore order
   (ruling 76)" with both builds' medians and ranges and the probe's numbers, and the report's table
   says so; the section stays as history with an "as built: not merged" line. M3's bound is then the
   lead's to rule (14.9 open point 4).
10. **CI.** The merge's push runs green in all six jobs; `RestoreOrderShellTests` runs on Windows,
    macOS and Linux there (the order's code is the same on each; its times are measured on the
    laptop alone, D50).

### 14.9 Risks, unverified, open points

**The choices this section makes for the package, with the lead's rulings of 2026-11-23 (rulings
86 to 90 in 13.1):**
1. **Where the order lives**: a new public `RestoreOrder`, made by `InitializeAsync` only when the
   active tab is a restored repository's tab beside another restored tab and the lifetime is not
   cancelled; every other start keeps today's loop word for word. So M2's start (one tab) and a
   dashboard's start run today's code, which keeps "as today" literal and the alternation's M2 a
   measure of nothing new; `MainWindowViewModel.cs` changes in four places; the tests reach its
   constants. It lives only until its start has ended, and an entry forgets a closed tab at once
   (14.3.1), so the order keeps no closed tab or its graph. It keeps no list of the tabs beside its
   entries, and once every entry's task has completed it disposes its registration on the lifetime.
   **Confirmed (ruling 86).**
2. **The first-page signal**: `RepositoryViewModel.FirstPage`, completed at the first page in
   `Graph` or at the first start's end, failure, cancellation or the view model's close. `FirstRead`
   is too early, `OpenAsync`'s task too late, `OnRowsDrawn` needs a drawn frame (14.3.2).
   **Confirmed (ruling 86).**
3. **How "shown" and "closed" reach the order**: calls from `Activate`, through which every
   activation passes, from `CloseTab` before the tab's `Dispose` and before its neighbour is
   activated, and from `CloseAll` before any tab is disposed; no new event.
   **Confirmed (ruling 86).**
4. **The clock**: `TabServices.RestoreClock`, a clock of the order's own, as `ReadClock` is for a
   dashboard's 10 s rule: `TabServices.Clock` unless the harness gives one. `TabServices` gains the
   optional constructor parameter `restoreClock` and the property, and `AppSession.Start`,
   `ShellDriver.Start` and `StartWithTabs` the optional `restoreClock`, which they pass on; the
   builder adds that and nothing else to those three files of the lead's (14.2). In the app the two
   are one clock, `TimeProvider.System`, so D457's "5 s on the session's clock" holds to the letter.
   A view-model test gives a `ManualTimeProvider` as `restoreClock` and moves it, and that clock
   carries the order's timers alone; the shell tests give one they never move, so no release comes
   from the clock between their steps; `SettingsEntryTests`' `Shell` gives one for its two lookup
   tests (14.4). **Changed (ruling 87): the order's own clock, `TabServices.RestoreClock`, given
   through the three files as `readClock` is, in place of `TabServices.Clock` with no harness
   parameter.**
5. **The slot**: held from a released tab's opening to the earliest of its own first page, its end
   without one, or its close (14.3.3). No clock frees it: D457 gives the 5 s to the first tab alone,
   and its models, "Add repositories" and a dashboard's reads, hold a slot until the work ends. The
   probe put the cost before the first page. More than four loads may run after their first pages,
   but never more than four released tabs' openings before theirs (a shown tab opens beside them,
   without a slot). **Confirmed (ruling 86).**
6. **A shown waiting tab** opens at once, takes no slot and frees none, and the order never
   subscribes to its `RepositoryOpened` (14.3.1); the others keep their slots and their wait for
   the first tab. **Confirmed (ruling 86).**
7. **"The active tab"** is `ActiveTab` when the openings begin, after git's search, so a user who
   switched tabs during the search is on the tab that opens first. **Confirmed (ruling 86).**
8. **The waiting tabs' order**: tab order at the start, the first tab skipped.
   **Confirmed (ruling 86).**
9. **The log lines** of 14.3.8, two at Info per start that has the order, the rest at Debug.
   **Confirmed (ruling 86).**
10. **The probe's rule for the four** (14.8 step 5): kept while the middle value of the longest
    answers of the fix's three ten-large launches is at or under the highest of the longest answers
    of the base's three one-tab launches in the same session; else two is tried once, in a session
    of its own in which a third worktree keeps four's build, so that the base's one-tab case, four's
    ten-large case and two's ten-large case alternate and the rule reads all three from that
    session, since only builds run alternately in one session can be told apart from the laptop's
    warmth (D436); each build makes one uncounted warm-up launch first (D437). The trial reads the
    same statistic for two's and four's ten-large launches, and two stays only if it meets the rule
    and the median of its active tab's own clock is no later than four's. The middle value, because
    a launch's longest answer is a maximum, which one scheduled task or one collection can make,
    and one disturbed launch must not start a trial that costs three builds and their rests.
    **Changed in its statistic (ruling 88): the middle value of the fix's three longest answers,
    where the section had each of the three, against the highest of the base's three.**
11. **The reading of "the fix's M2 lies within the base's range"**: (a) the fix's M2 median between
    the lowest and the highest of the base's five M2 runs, the reading taken; (b) each of the fix's
    five M2 runs within that range, the reading not taken. The section proposed (a), because M2
    runs today's code in both builds (choice 1), so the clause checks that the two builds were
    measured alike, which their medians show; under (b) one warm run of unchanged code, which says
    nothing about the order, would fail the proof. D457 writes "median" in its other two clauses
    and not in this one, so (b) was the stricter reading open to the lead; 14.8 step 7 states (a)
    as the rule. **Ruled (ruling 89): reading (a).**
12. **"Reverted"** read as "not merged": the proof runs on the branch before the merge.
    **Confirmed (ruling 86).**
13. **The merge tools' lookup** waits for `RestoreOrder.FirstLoadEnded`, the first task of a tab the
    order opened to end (14.3.6): a waiting tab's drop completes its task for `InitializeAsync`, but
    not the lookup's wait, since a tab that never opened has no load to end (D457 (5): "when the
    first restored load ends"). **Confirmed (ruling 86).**
14. **The four kept as a count of the order's own**, under its `Lock`, in tab order, rather than a
    `SemaphoreSlim` as "Add repositories" has: the next tab must be the next in tab order and a
    shown tab leaves the line, which a semaphore's waiters do not give; shared with neither a
    dashboard's `ReadSlots` nor `BulkSlots`, whose reads and runs take their own four (14.3.3).
    **Confirmed (ruling 86).**
15. **What a waiting tab shows**: `ShowPending`'s state, as a restored tab shows today before its
    repository opens, with no new text and no texts class changed, because a shown waiting tab opens
    at once, so that state lasts as long as an opening tab's does today (14.3.4).
    **Confirmed (ruling 86).**
16. **The 5 s counted from the moment `Start` asks for the first tab's opening**, where D50's clock,
    `OpenAsync`'s stopwatch, starts too, so the release line's `{N}` and the tab's own Q1 clock
    count from the same moment (14.3.2 item 4). **Confirmed (ruling 86).**
17. **A start whose git is missing takes the order** when its active tab is restored: the first
    opening fails at once and releases the others, which fail as today, so 14.3.5 adds no case for
    it (14.3.5). **Confirmed (ruling 86).**
18. **`Closed(first)` releases synchronously inside `CloseTab`, before the neighbour's
    activation**: `CloseTab` calls it before `Dispose`, and the release opens the waiting tabs in
    tab order into the slots before `Activate` shows the neighbour, so a neighbour the release
    reached takes its slot in turn rather than opening as shown, and the order is the same on any
    thread (14.3.2 item 3, 14.3.7 race 4, 14.4 test 4). **Confirmed (ruling 86).**

**Risks and what is unverified:**
1. The release opens up to four tabs on the UI thread right after the first page is appended; the
   release runs in its own continuation, after the page's action, but whether the four openings'
   first steps on the UI thread (the provider's call, then each continuation's view model, session
   write and watcher) come before the first frame with rows is drawn, and so push back the
   first-rows line, the code does not settle; only M3 shows it. If they do, the lead rules (a
   release posted after the first frame is one way; ruling 76 needs no change for it).
2. The time until every restored tab has loaded grows, since the others no longer all start at
   once; nothing but tests waits on `InitializeAsync` (`WaitUntilReadyAsync` has no limit but the
   hang watchdog's 3 minutes, the other waits `ShellDriver.DefaultWaitTimeout`'s 60 s, M3's 300 s);
   the probe's "all loaded lines seen by" shows it, unmeasured until then.
3. `NamesRepositoryInDialogs` stays false longer while only the first tab has a repository
   (14.3.7 race 12); no check opens a dialog in a start's first seconds. Likewise
   `OpenRepositoryFolders` lists tabs with a repository, so a dashboard's Add open tabs leaves out
   the waiting tabs until they open (`WorkspaceCommands.AddOpenTabs`), as today for a tab still
   opening; until the first tab's trigger, 5 s at most, and then a free slot (race 13). For the list
   of the review before the gate, where ruling 90 keeps both.
4. D289's pull-request reads of a background tab come at its first refs read, now later by the
   first tab's trigger and a slot; D289's letter holds, and P13 waits for them after the start.
5. The probe starts the app from PowerShell, whose PATH finds `cmd\git.exe`, the series from Git
   Bash, whose PATH finds git itself (D457's last paragraph): each compares the two builds within
   itself, never with the other.
6. macOS and Linux: the same code, run by CI's jobs, measured nowhere (D50).
7. A fix after the merge that touches the order's code (a CI failure on another system, the
   review's) changes what the series proved; whether the proof is taken again is the lead's.
8. A share that does not answer holds a slot for minutes, as in "Add repositories". Four such tabs
   hold every slot, and the rest wait until one ends or the user shows a tab, which opens at once
   (14.3.4). `InitializeAsync` ends when the openings end, as today; only the waiting tabs'
   openings come later.

**Open points, each with who settles it and the lead's ruling on it (ruling 90):**
1. Whether "Check again" reopens failed tabs through the order (today all at once): the lead,
   before the review before the gate, or a Backlog row. **Ruled (ruling 90): it keeps reopening
   the failed tabs all at once in 8a; it gets a row in plan.md's Backlog at the close, beside
   D457's rows, and is not on the list of the review before the gate. Risk 3's two items
   (`NamesRepositoryInDialogs`, Add open tabs) stay on that list.**
2. Choices 1 to 18 above: the lead, before the builder starts. **Settled by rulings 86 to 89.**
3. The probe's rule for the four (choice 10) and its numbers: the lead, at step 5. **Ruled
   (ruling 90): the rule is ruling 88's; its numbers come at 14.8 step 5.**
4. M3's bound when the change is not merged (unset, or drawn from the base's alternated runs): the
   lead, at step 9. **Ruled when the case arises (ruling 90).**
5. A release that delays the first frame (risk 1): the lead, on the alternation's numbers.
   **Ruled when the case arises (ruling 90).**
6. 13.3's row "The restore order's proof" and 0.6's series line take 14.7's times: the lead, when
   the section enters the design. **Ruled (ruling 90): the lead brings them to 14.7's times in the
   commit that puts the section into the design.**

### 14.10 As built, proven and merged (the lead, 2026-11-23)

**Built and reviewed** (14.8 steps 1 and 2; ruling 91). `wip/p8a-restore`, five commits from
`3f67850`: `b0780f4`, the tests with the stubs and ruling 87's clock, on which 24 of the four
classes' 35 tests fail as 14.4 marks them; `59b3585`, the order; and after the review (one major and
seven minors, six of the eight in the tests) `5285967`, `3a6654c`, the two failing tests of the
asks' order and of a tab closed after it was let in, and `aa83146`, their fix. Where the build
departs from 14.2 to 14.4, ruling 91 has it: `FirstPage` completes at a page only when the page has
a row; the tabs let into slots are queued under the lock and asked for in the order let in, so
`Closed` has asked for them when it returns unless it was itself reached from inside an asking call;
`Open` asks for no tab closed since it was let in; four test methods beyond 14.4's list. The four
classes hold 39 tests, 33 of them new.

**Proven** (steps 3 to 7; rulings 88, 89, 92 and 93; D458; the numbers are in the report's "The
restore order's proof"). The probe, three alternated rounds: by its own clock the active tab's first
rows came after 1,769 to 2,025 ms beside nine large tabs and 1,907 to 2,858 ms beside nine small
ones, where the base read 5,182 to 7,627 and 5,079 to 7,811 ms; four at once stays (230 ms against
335 ms), and no trial of two was made. The alternated series, five undisturbed pairs of nine taken:
the fix's M3 median 6,876 ms against 1.25 times its M2 median, 7,170 ms; the base's 10,078 ms
against 6,177.5 ms; the fix's M2 median 5,736 ms within the base's 4,753 to 6,143 ms. The takeover
lasted from 13:36 to 14:52, where 14.7 had estimated about 50 minutes: four pairs met a task the
scheduler had not shown as due and were taken again.

**Amended on 2026-11-24 (D461; the review before the gate, performance-restore-rows-1 and -2; ruling
99).** The series does not show the third condition. Pair 5's base run, which gives the top of the
base's M2 range (6,143 ms), began 15 s after `Office Feature Updates` started at 14:11:00, the task
for which pair 4 was dropped, and the reading after a run could not see a task that started before
it. Without that run the base's range is 4,753 to 5,179 ms and the fix's M2 median lies above it;
the first two conditions and the probe stand without it. The series is taken again before the gate,
ten pairs, the builds' order alternating by pair, with a script that waits before every run and
reads the scheduler from 60 s before a run's begin (`…/wave4/restore/series2/run_series2.sh`); D457
(5)'s rule and its consequence apply to the retake. The order stays merged meanwhile.

**Merged** (step 8). `aedb12f`; the bounds in `23bea54`, a commit of their own: M3's 8,900 ms, and
M2's raised from 6,100 to 9,500 ms by ruling 93. On `23bea54`, 15:01 to 16:03, with no builder at
work: the package's four classes, 39 of 39, then 0.4's lagged list through `scripts/heavy-run.sh`
with `RestoreOrderShellTests` added to its App.Tests command: 25, 209, 21, 206, 178, 126 and 144
tests, none failed, `AboutEntryTests` and `RequestOperationsProvidersTests` among them. CI's run
45286046457 on the push (`5e23aba`) is green in all six jobs, the floor's included, with
`RestoreOrderShellTests` run on Windows, macOS and Linux (step 10; status.md's CI section).

**The open points of 14.9.** 4 (M3's bound without a merge) did not arise. 5 (a release that delays
the first frame): the release does come before the first frame, 180 to 256 ms before the tab's
`first graph rows drawn`, and no cost of it shows beyond the spread (ruling 93). Risk 3's two items
(`NamesRepositoryInDialogs` and a dashboard's Add open tabs while tabs wait) and ruling 90's "Check
again" stand as ruled: the first two are on the list of the review before the gate, the third gets a
Backlog row at the close.

**For the review before the gate** (status.md's step 19): the fix's one-tab start read above the
base's in the series and the probe, within the laptop's spread by the lead's reading (ruling 93),
and M2's bound counts both builds on that reading; and the series' script began a run 7 to 29 s
after a waited task's time without checking that the task had ended, which the script now does.
**The review's answer (2026-11-24):** on the one-tab path the code adds no wait and no process
(S19-05); what the series showed for it is not a proof of the third condition, and D461 has it taken
again; the script's first correction covered only a task it had waited for, and the retake's script
covers the rest (S19-06).
