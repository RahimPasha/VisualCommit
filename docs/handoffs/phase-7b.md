# Phase 7b handoff — Azure DevOps and GitLab

Written at the end of phase 7b for the session that runs phase 8. That session has not seen any of
this phase's conversation. How the app is built is in [architecture.md](../architecture.md): its
"Hosting: the shared HTTP core", "Hosting: GitLab's API", "Hosting: Azure DevOps' API" and "The fakes
of Azure DevOps and GitLab" sections are 7b's, and its other hosting sections, "The account's
credentials in git", "The settings screen" and "The live command" say what 7b changed in 7a's. Why is
in [decisions.md](../decisions.md): D344 to D351 are the owner's answers before the start, D352 the
answer at it, D353 to D356 the owner's rulings on the research's questions of taste, D357 to D372 the
design's, D367's amendment and D373 to D376 the owner's answers on the design, D377 and D378 its
review's, D379 to D390 the build's, D391 and D392 the review before the gate's, D393 the owner's choice
before the gate's live runs, D394 and D395 the gate's, and D396 the close's cold-read check. The design is
[phase-7b-design.md](../phase-7b-design.md): section 0 the scope, the packages, the parts, the cost in
time and the earlier pictures, 1 the shared contracts, 2 to 15 one brief per work package with its "As
built" notes and the lead's rulings, 16 the open points and how each was settled ("design N.N" below).
The report,
[test-reports/phase-7b.md](../test-reports/phase-7b.md), holds the 45 checks (A1 to A14, B1 to B8, P1
to P14, N1 to N9), the live command's runs and its open points 1 to 45 with the lead's readings.
"Ruling n" is one of the lead's 31 rulings of 2026-10-31, which the design cites (their text is in
`D:/Projects/Personal/phase7b-work/design/`, outside the repo).

## Result

Everything in the plan's "Delivers" list was built, as one phase (D365). The report's "What proves
each Delivers item" is the longer form of this table.

| Delivered (plan.md, phase 7b) | Proven by |
|---|---|
| Azure DevOps Services and gitlab.com on 7a's provider model (D347): sign-in with a personal access token (D345), one account per provider (D348), tokens in the OS keychain, else the session (R3, D253) | A1 to A14 (the real window repeats A2's three accounts, A5's GitLab dialog and A6's Azure DevOps dialog); B8, P2 and N8, which sign in first; `AzureDevOpsRecognitionTests`, `GitLabRecognitionTests`, `HostingProvidersTests` (`TokenFormOf`, D378), `HostingSessionProvidersTests`, `SignInDialogProvidersTests`, `AccountsProvidersViewModelTests`, `SignInEntryProvidersTests`, `TokenEntryNamesTests`; CI's real-store steps with an `azure-devops` key (D270); the live `Check_2` and `Check_3` per provider |
| The account's token on the app's git calls to the remotes it covers, on Azure DevOps its own organization's alone (D350) | A14; B7 (the real window repeats both clones in the real process, Git for Windows' `sh` running the helper); P8, P9, P11; N4; `CoverageGitMatchTests`, `CredentialBindingSelectTests`, `AzureDevOpsCoverageVectorsTests`, `PushGuardProvidersTests`, `HostingSessionCredentialTests`, `CloneFromAccountTests`, `SignInBindingsWindowTests` (D388), `GitConfigIncludeReadTests` (D391) |
| Browse and clone (D354) | B1 to B8 (the real window repeats B1's button, B3's two lists and B7's two clones); `CloneFromAccountDialogViewModelTests`, `HostingTextsDescribeProvidersTests`, `CloneFromAccountTests`, `AppShortcutsTests`; the live `Check_2` per provider and Azure DevOps' `Check_4` (a clone through the organization's own host) |
| The request list (GitLab's merge requests, D351), its menu, checkout (D361), the badge and open in browser; avatars GitHub's alone (D349, D367) | P1 to P14 (the real window repeats P1's sections with their badges and P10's menus per provider); N5, N6; `RequestSectionProvidersTests`, `RequestCheckoutProvidersTests`, `RepositoryMenusProvidersTests`, `GraphRequestBadgeTests`, `RequestOperationsProvidersTests`, `GitRepositoryPullRequestsTests`, `RepositoryHostingProvidersTests`, `RepositoryAvatarsTests`; 7a's part P and G pictures the same files |
| Create from the branch menus and the drop menu, a draft or not | N1 to N9 (the real window repeats N2's forms and N6's status after creating, per provider, and N1 in part); `CreateRequestDialogProvidersTests`, `CreateRequestOperationProvidersTests`, `RepositoryMenusProvidersTests`; the live `Check_2` (through the providers) and `Check_3` (through the app's form) |
| 7a's screens and commands that name GitHub name the provider they act on, GitHub's in 7a's words (D351, D362) | Parts A, B, P and N in each provider's words; 7a's pictures unchanged but for the intro of its 23 part A pictures and of its real-window `a01a`, `a02b` and `a05a` (D353; the report's "Changes to expected results"); `HostingTextsProviderTests`, `AccountsProvidersViewModelTests`, `HostingTextsDescribeProvidersTests`; 7a's part A to N classes |
| A local fake of each provider's API for the default run and CI (D346) | Every part runs against `FakeAzureDevOps` and `FakeGitLab`, corrected by the live command's stage 1 before part A's checks were written (WP-LF, design 16.1); `FakeAzureDevOpsTests`, `FakeAzureDevOpsGitTests`, `FakeGitLabTests`, `FakeGitLabGitTests`, `FakeHostingServiceTests`, `Phase7bSupportTests` |
| The opt-in live command extended to Azure DevOps and GitLab (D346, D352) | `tests/VisualCommit.LiveHostingTests`' `AzureDevOpsLiveTests` and `GitLabLiveTests` (the files); their runs ("State of the repo"); `LiveSafetyTests` and `LiveScreenTests`, 582 offline cases |
| From the 7a handoff, `scripts/heavy-run.sh`'s header comment; from the design, CI's limits raised before the new tests (D377) | L1 (`a88b84d`): the script and `.github/workflows/ci.yml` |

The plan's "Done when": signing in, cloning, listing pull or merge requests and creating one work
against the fakes of Azure DevOps and GitLab (parts A, B, P and N and the lagged list's classes,
design 0.4, on the laptop and in CI's five jobs) and against the real services (the live command's
`Check_2` per provider at stage 1, its app-driven `Check_3` at stage 2, and both again in the gate's
two runs); the visual gate passed on its counted round, both passes on `cb9af6d`'s code, with every
one of the list's 1,551 files and 45 of CI's pictures inspected (the report's "Run").

Not delivered, and where each item went (plan.md's phase 7b, "Outcome"):

- **Phase 8** ("From phase 7b" and the items of 7a it extends): phase 2's commit boxes, editable
  while "Committing…" runs (D392); a netrc entry for gitlab.com, dev.azure.com or
  `<organization>.visualstudio.com`, which on git 2.31 to 2.45 answers a bound call's first 401
  before the app's helper (risk 35); the new providers' tokens in macOS's keychain, readable through
  `/usr/bin/security` (risk 34); and, by hand, the new providers' sign-ins kept across a restart of the
  installed app in Windows' Credential Manager and a Linux Secret Service, and every link that opens
  the browser, which no real window opens in 7b (risk 41; ruling 29).
- **Backlog** (plan.md's "Left out of phase 7b"): Azure DevOps Server and self-managed GitLab (D347),
  several accounts of one provider (D348), the new providers' avatars (D349), GitHub avatars where
  the listed remote is another provider's (D367), a browser sign-in or app registration (D345), any
  request to a provider while signed out (D350, D360), binding SSH remotes, an Azure DevOps fork's
  pull request whose fork is no remote (D361), quick actions read by the app (ruling 20), a
  repository name with a literal `%`, and D392's accepted cases.
- **Left to the owner** (Waiting on the owner): risk 39's sign-in dialog in a bound tab, now for three
  providers and wider on dev.azure.com; risk 40's brief torn banner; whether to harden the app
  against a git that loses a URL's user (risk 43).
- **Recorded as risks**: GitLab's rate limit against the first lists (44), what the bindings cannot
  see (45), phase 6's palette test that fails rarely on CI (46) and D386's one open case (47).

## State of the repo

Branch `phase/7b-hosting-azure-gitlab`, started on 2026-10-31 from `master` at `a29379f` (phase 7a
merged) and built on the Windows machine to 2026-11-08. The last commit that changes the app or its
tests is `cb9af6d` (the merge of D394 and D395), 346 commits on top of `a29379f`; the counted round
ran on the branch at `af9b563` (the default run) and `3dc0887` (the rest), which change only docs
after it. The Gate commit is `cb9af6d`, the code both passes ran on; every commit after it
changes docs only, the last one the one that sets the phase to "Awaiting acceptance" (status.md).

Before the merges: L1 `a88b84d` and WP0 `e8a275d` (pushed at `e3a8999`), in the main checkout. The
33 merges (`git log --oneline --merges a29379f..cb9af6d`): wave 1 WP-F `564dad4`, WP-H `3591dc4`,
WP-C `6586066`; wave 2 WP-FL `7780908`, WP-FA `e995fc4`, WP-GL `e8a5abf`, WP-AZ `767d9b9`; wave 3
WP-S `bdc92c3`; the live command's stage 1 `b377aa3` and WP-LF `22fad1b`; the priority fix `32eaee3`
and `d62dff8` (D386); wave 4 WP-A `b6de3cf`, the window's close `85d599c`, WP-B `9409aa8`, open point
8 `20e529e`, CI's three fixes `e49bd5c`, `eb218a3` and `7ddb928`, part N's checks `5561548`, WP-P
`1821206`, phase 2's checks 14 and 15 `26b44c7`, the watcher's tests `abc6df7`, WP-N `a6952d2`
(D388), D389 and D390 `98cdd86`; wave 5 the live command's stage 2 `fdb2c05`, the review's fixes
`b4117fc`, `eedffe3`, `8bf5e99` and `6333de6`, WP-R `f016bdf`, D393 `ee0aeeb` and the gate's D394 and
D395 `cb9af6d`. The Progress checklist and Notes of status.md on the phase's branch give the rest
(`git show origin/phase/7b-hosting-azure-gitlab:docs/status.md`, the merge's second parent once 7b is
merged; the acceptance resets `master`'s).

- **Default run, counted round** (`cb9af6d`'s code, at `af9b563`): 2026-11-08, 14:26 to 15:59,
  straight after D246's second run, at most four projects at once (D343): 7,888 of 7,892 passed and 4
  were skipped (the three real keychains' tests and `WindowsCredentialStoreTests`' Off-Windows test,
  D270) in 1 h 31 min 48 s (5,598 s with the build and the lock), no watchdog stop. The projects
  ended after about: phase 7a's walk-through 13 min 42 s, phase 6's 38 min 18 s,
  `VisualCommit.Hosting.Tests` 53 min 14 s, phase 7b's walk-through 53 min 51 s (15 min 33 s of its
  own), phases 0 to 2's 65 min 56 s, `VisualCommit.Git.Tests` 69 min 29 s, phase 4's 79 min 0 s, phase
  3's 83 min 8 s, phase 5's 85 min 6 s, `VisualCommit.App.Tests` 85 min 59 s, part L's 87 min 0 s and
  phase 5's undo walk-through 91 min 41 s. For comparison: 7a's gate, 5,952 tests in 1 h 12 min 18 s;
  WP-N's merge, 7,817 in 1 h 37 min 45 s; design 0.6 estimated 78 to 85 minutes.
- **Real-window pass**: D246's two runs of `Phase7bRealWindowPass` (five launches, design 14), the
  owner present, on `9b044af` (`cb9af6d`'s code): 5 of 5 in 3 min 50 s and 5 of 5 in 3 min 26 s, each
  of the 13 captures within 0.32 to 0.86 percent of its scripted twin (0.80 to 2.50 compared in place
  only). The whole pass on the counted round, at `3dc0887`, 16:00 to 16:35, straight after the default
  run: 77 of 77 in 33 min 35 s (2,091 s with its build), Q1 1,827 ms, 198 captures each within 2.35
  percent of its twin (4.08 in place). 7a's gate: 72 in 31 min 6 s; design 0.6 estimated 36 to 37
  minutes.
- **Pictures**: 7b's walk-through (`Phase7bChecksA`, `B`, `P`, `N`: 29 check methods and the
  project's view-model, menu, view and expectation classes, 179 tests in 3 min 47 s alone) saves 93
  scripted pictures. `docs/test-reports/phase-7b-pictures.sha256` lists 1,551 files: 1,150 scripted
  (phases 0 to 7b: 14, 43, 66, 265, 122, 264, 198, 85 and 93) and 401 real-window ones (7b's 13
  captures and 13 difference pictures among them). Against the references, 1,112 are the same files,
  320 changed and 119 are new. 19 Opus inspectors opened every file in batches of at most 150 (D247),
  with 45 of CI's pictures: 1,594 of 1,596 passed, and of the 2 in doubt (phase 5's
  `a2g-merge-output-opened` and its real-window difference picture, where two reads that start
  together swapped places in the activity log) three Opus skeptics each judged no defect (the
  report's "Run"). The references are in `D:/Projects/Personal/gate7b-reference/`, outside the repo:
  phases 0 to 7a's walk-throughs on `master` (`a29379f`) with git 2.56, 1,057 scripted pictures, and
  7a's gate's 375 real-window files. The counted round's copy is
  `D:/Projects/Personal/phase7b-work/gate/round-3dc0887/` (its `visual/`, the list, the comparison,
  the inspection's batches, sheets and verdicts), its runs' outputs in `gate/counted/`.
- **Live command**: stage 1 (2026-11-05, on `8faa839`) 8 of 9, GitLab's list refused for a missing
  permission (D385), and GitLab's class again with a new token (2026-11-06) 4 of 4; Azure DevOps'
  probes again for open point 8 (2026-11-07); stage 2 (on `fdb2c05`, 2026-11-08) 11 of 11, each
  provider's `Check_3` through the app. The gate's run before its pictures, on `ee0aeeb`: 595 of 596,
  GitLab's `Check_2` stopped twice on stalls of the network to gitlab.com and passed alone the third
  time, and GitHub's probes found D394. The counted round's run, on `3dc0887` after the list: 596 of
  596 in 2 min 20 s, nothing new for the fakes (the report's "Live command"). Copies in
  `D:/Projects/Personal/phase7b-work/live/`, `live-gate/` and `gate/counted/live-files/`.
- **CI**: the run of the gate's code is
  [42522049151](https://github.com/RahimPasha/VisualCommit/actions/runs/42522049151), on `9b044af`:
  all five jobs green, Windows 7,892 tests with 4 skipped and the others 7,889 with 3:
  `windows-latest` (git 2.56.0.windows.4) 25 min 42 s (Test step 23 min 41 s of 70), `macos-latest`
  (2.56.0) 35 min 9 s (33 min 9 s), `ubuntu-latest` (2.56.0) 8 min 28 s (6 min 23 s), the containers
  (2.43.0) 7 min 41 s and 8 min 19 s (5 min 28 s and 5 min 51 s of 14); every keychain step and
  Windows' "Build the opt-in projects" green. The commits after it change only docs. The phase's 29
  runs, each failure with its fix, are in the CI section of status.md on the phase's branch (as
  above); their Test steps reached 41 min 6 s on Windows and 46 min 21 s on macOS, under four fifths
  of 70 (D377).
- **Gate**: passed (step 18 of status.md's checklist on the phase's branch; the report's "Run"), both
  passes in full on the Windows machine on one code, and every picture inspected; nothing could not
  run but what the plan makes conditional: the 1920×1080 real window does not fit this screen, so
  phase 0's check D and 7b's `n09a` and `n09b` are proven scripted only. No pass touches a real
  keychain (CI's steps do), and no 7b step opens the browser (ruling 29).

## Environment

| | |
|---|---|
| Machine | Windows 11 Pro 10.0.26300 on an Intel Core i7-8650U laptop; screen 3000×2000 at 200 % scaling (1500×952 logical work area). The whole phase was built on this machine, partly by Opus sub-agents in worktrees under `D:/Projects/Personal/vc-wt/p7b-<name>`; no cloud session worked on it |
| .NET SDK | 10.0.303 (`global.json` stays at 10.0.100 with `latestFeature`); CI's containers 10.0.114 (Ubuntu's packages) and 10.0.403 (Microsoft's installer) |
| Git | 2.56.0.windows.4, first on `PATH` (D248), with `core.fsmonitor=true` in the owner's global configuration and `credential.helper=manager` in the install's system configuration (Waiting on the owner). Kept for runs with another git first on `PATH`: Git for Windows 2.43.0 in `D:/Projects/Personal/phase7b-work/verify/git243/git-2.43.0-hb` (the containers' version), and MinGit 2.31.1, 2.43.0 and 2.56.0.windows.2 in `D:/Projects/Personal/phase7b-work/research/scratch-g3-git/` (`git-2.31.1/`, `git-2.43.0/` and `git-2.56.0.2/`; 2.31 is the bindings' floor, D274 and D277). On CI at the gate: Windows 2.56.0.windows.4, macOS 2.56.0 (Homebrew, over curl 8.23.0, which loses a URL's user: its `git-url-user.txt` reads Lost; D389, risk 43), `ubuntu-latest` 2.56.0, the two containers 2.43.0 |
| Packages | Unchanged from 7a: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, Tmds.DBus.Protocol 0.94.1, xunit.v3 3.2.2 (pinned, D24), FlaUI.UIA3 5.0.0. No new package or icon |
| New projects | `tests/VisualCommit.VisualTests.Phase7b` (twelve test processes in the default run); the live command's project gains a class per provider |
| Ports | 47397, 47398 and 47399 for 7b's fakes of Azure DevOps, GitLab and GitHub, in its walk-through and its real-window part, each falling back to a free port when it is taken (D363, amending D97); 47396 reserved; 47391 to 47395 as before |
| Sandboxes | GitHub's private `RahimPasha/visualcommit-sandbox` with a fine-grained token for it alone (expires 2026-11-29); a project and repository in the owner's Azure DevOps organization, which holds other work (D352), with a token of Code "Read & write" for that organization alone (expires 2026-12-04); a private project of a free gitlab.com account that lists four more projects, which D393's guard keeps the command out of, with a fine-grained token (expires 2026-12-04). Six Windows user variables (CLAUDE.md's "What to know"); the organization's and the GitLab namespace's names never enter the repo |
| Screenshots | Scripted: scaling 1. Real window: 200 % and a work area of 1500×952, as `run.txt` records them |

## Build, run and test

The table with notes is the Commands section of [CLAUDE.md](../../CLAUDE.md#commands). New or
changed in this phase:

```
dotnet test --project tests/VisualCommit.VisualTests.Phase7b
dotnet test --project tests/VisualCommit.VisualTests.Phase7b --filter-class "*Phase7bChecksA"
bash scripts/time-scripted-check.sh "*Phase7bChecksA*"
bash scripts/time-scripted-check.sh "*Phase7aChecksA*" 7a
VISUALCOMMIT_WATCHER_LAG_MS=1500 bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase7b
dotnet test --project tests/VisualCommit.LiveHostingTests -c Release --filter-class "*LiveSafetyTests" --filter-class "*LiveScreenTests"
bash scripts/heavy-run.sh --project tests/VisualCommit.LiveHostingTests -c Release
bash scripts/heavy-run.sh --project tests/VisualCommit.LiveHostingTests -c Release --filter-class "*AzureDevOpsLiveTests" --filter-class "*GitLabLiveTests"
bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release --filter-class "*Phase7bRealWindowPass"
```

- The lagged list (D262) is design 0.4's: 7a's list, then 0.4's two commands (7b's walk-through and
  six `VisualCommit.Hosting.Tests` classes that start the app), one at a time with
  `VISUALCOMMIT_WATCHER_LAG_MS=1500`. A failure under the lag gets a fix, never a looser test.
- `scripts/time-scripted-check.sh` runs phase 7b's project when the phase is left out; it knows 0 to
  6, 7a and 7b, and exits 2 for any other phase.
- The live command acts on the owner's three accounts: tell the owner first, run its offline classes
  alone on the same commit first (582 tests at the gate), never print the whole environment, and tell
  every agent so. It reads six Windows user variables; a session started before they were set gives
  all six to the command's process alone with CLAUDE.md's PowerShell line. Each provider's findings
  land in `artifacts/live/<provider>/run.txt`, which the next run overwrites: copy them out of the
  repo, and check each, fully decoded, for the tokens, the organization's name, the namespace and any
  address before anything from it enters the repo (D372, D384).
- To run the app without touching the real keychain or a service: CLAUDE.md's variables, with
  `VISUALCOMMIT_KEYCHAIN=memory`, `VISUALCOMMIT_GITHUB_URL=none`, `VISUALCOMMIT_AZURE_DEVOPS_URL=none`
  and `VISUALCOMMIT_GITLAB_URL=none`; `http://127.0.0.1:<port>/` points a provider at a fake (design
  1.8).
- `VisualCommit.Hosting.Tests` writes `artifacts/visual/git-url-user.txt` the first time one of its
  D278 tests (`SignInBindingsWindowTests`, `WelcomeCloneFromGitHubTests`) asks `GitUrlUser` whether
  the git under test keeps a URL's user after a 401 (D389); no other project writes it.
  `VisualCommit.App.Tests`' palette growth test appends a line for each run to
  `artifacts/visual/palette-growth.txt` (D392). CI uploads both per job, with the screenshots.

## Running the visual gate

As in phase 7a's handoff, with 7b's numbers:

1. **Stop what is left**: worktrees, `wip/*` branches, fsmonitor daemons (phase 6's handoff gives the
   PowerShell); check C: and D:.
2. **References for the earlier pictures** (design 0.7), made on `master` before any real-window run:
   7b's in `D:/Projects/Personal/gate7b-reference/` (phases 0 to 7a's eight walk-throughs in a
   worktree on `master`, 750 tests in 50 min 25 s through `scripts/heavy-run.sh`). They ran while other
   runs held 7a's fake's port, so their `a09b` and `b06a` show a fallback port: make references alone.
3. **The live command before the pictures** (D335), the owner told, after its offline classes (7b:
   on `ee0aeeb`); its corrections to the fakes are merged before any picture (7b: D394 and D395 at
   `cb9af6d`), with the lead's runs at that merge.
4. **The phase's scripted pictures on this machine**:
   `bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase7b` (179 tests, 3 min 47 s
   alone), so that the real-window checks have twins.
5. **D246's two runs**, the owner present, the desktop unlocked and Windows' high contrast off:
   `bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release --filter-class "*Phase7bRealWindowPass"`
   (3 min 26 s to 3 min 50 s each). 7b's part serves its three fakes on 47397 to 47399 in the test
   process, so 7b's walk-through never runs beside it; it opens no browser, clicks no "Copy …" and
   signs in by typing the scenario's made-up tokens. Made once.
6. **The counted round**: `bash scripts/heavy-run.sh` (1 h 31 min 48 s), then
   `bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release` (never
   `--no-build`; 33 min 35 s), then at once
   `find artifacts/visual -name '*.png' | sort | xargs sha256sum > docs/test-reports/phase-7b-pictures.sha256`,
   then the live command again, its offline classes alone first on the same commit (582 of 582, then
   596 of 596 in 2 min 20 s), the owner told before each pass and the live run. 7b ran
   them one straight after another, without 7a's 10-minute rests; Q1 still measured 1,827 ms, but
   rest the laptop (risk 10).
7. **Comparison and inspection** (D247):
   `python scripts/compare-pictures.py D:/Projects/Personal/gate7b-reference <round copy>/visual <folder>`.
   A script outside the repo, the round's copy's `inspect/inspect_prep.py`, then compared each changed
   picture with its reference pixel by pixel. One whose changed pixels all lie in two 16×16 squares at
   the window's bottom corners, where the desktop shows through the rounded corners (209 real-window
   files), is marked changed only there; for each of the 111 changed beyond them it made a sheet of
   the capture, the reference and the changed pixels outside the corners in red, cropped to their box.
   Each picture's result is the `status` in the round's copy's `inspect/batch-*.json`, beside the
   batch it went to, and the sheets are in `inspect/sheets/`; CI's pictures got the share of pixels
   that differ from their Windows twins. 19 Opus inspectors: 7b's 93
   scripted pictures in four batches by part against the report, its 26 real-window files in one,
   phases 0 to 7a's 1,057 scripted pictures in nine and their 375 real-window files in four, and CI's
   45 (`gh run download <run id> -D <folder>`) in one; three skeptics for each picture in doubt.
8. **After a fix**: as 7a's handoff says (D246, D335). Copy a round's `artifacts/visual/` and its list
   out of the repo before anything runs again.

Screenshots land under `artifacts/visual/phase-7b/{scripted,real-window,timings}`. For phase 8's
gate the references are 7b's counted round's copy,
`D:/Projects/Personal/phase7b-work/gate/round-3dc0887/visual/` with its list
(`round-3dc0887/phase-7b-pictures.sha256`): every picture of phases 0 to 7b, scripted and
real-window, as `cb9af6d`'s code drew them on this machine. `D:/Projects/Personal/gate7b-reference/`
holds only phases 0 to 7a's: their walk-throughs as `master` drew them before 7b, and 7a's gate's
real-window files.

## What changed in the code

architecture.md describes each part under the section named here; this table says where to start.
Unless a project is named, a path is in `src/VisualCommit.Hosting/` in the first five rows and in
`src/VisualCommit.App/` elsewhere (`RepositoryOperations.*` and `OperationTexts.*` in its
`ViewModels/Operations/`). Core, Git and the test projects are named as in phase 7a's handoff.

| To do this | Start here |
|---|---|
| The provider model for three providers | Core `Hosting/AccountKey.cs` (`HostingProvider { GitHub, AzureDevOps, GitLab }`; Azure DevOps' key holds the organization, GitLab's the username), Core `Hosting/HostingProviders.cs` (names, ids, possessives, `InAccountsOrder`, `TokenFormOf`), `HostingOptions.cs` (each provider's endpoints or null, the three variables), `IHostingProvider.cs`, `HostingRecords.cs` (`HostedRepository`, D358; `PullRequestRefusal`). Section "Hosting: the provider and GitHub's API" |
| The shared HTTP core | `Http/HostingHttpClient.cs`, `HostingHttpProfile.cs` (with `HostingLinks`), `HostingExchange.cs`, `HostingHttpTexts.cs`; 7a's `GitHub/GitHubClient.cs` is now GitHub's profile on it. Section "Hosting: the shared HTTP core" |
| GitLab's API | `GitLab/GitLabProvider.cs`, `GitLabClient.cs` (`GitLabProfile`), `GitLabUrls.cs`, `GitLabEndpoints.cs`, `GitLabJson.cs`. Section "Hosting: GitLab's API" |
| Azure DevOps' API | `AzureDevOps/AzureDevOpsProvider.cs`, `AzureDevOpsClient.cs`, `AzureDevOpsUrls.cs`, `AzureDevOpsEndpoints.cs`, `AzureDevOpsApi.cs`, `AzureDevOpsJson.cs`. Section "Hosting: Azure DevOps' API" |
| The session, a slot per provider, a tab's hosting | `IHostingSession.cs` (members that take the provider; 7a's GitHub-named shims removed by WP-N), `HostingSession.cs` (each slot's start read), `JsonAccountStore.cs`, `RepositoryHosting.cs` (D383) and `.Avatars.cs` (D367, D395), `Tokens/TokenEntryNames.cs`; the App's `RepositoryViewModel.Hosting.cs` (`AwaitsUrlRules`, D388). Sections "Hosting: the session and the account", "Hosting: a repository's pull requests and avatars", "Tokens and the keychains" |
| The token reaching git on the new hosts | Core `Operations/CredentialBinding.cs` (`Select` with a `GitCoverage`, `InPlainForm`; D369, D379, D381), Core `Git/GitConfigIncludes.cs` with Git `GitConfiguration.cs`'s `ReadIncludedAsync` (D391), Core `Hosting/PullRequestRefs.cs`, Git `Prompts/AskPassBroker.cs`, the push guard in `RepositoryOperations.Remotes.cs`, `RepositoryOperations.Run.cs` (D388), `Services/PromptDialogs.cs`; Git `GitRunner.cs` and `WindowsJob.cs` (D386). Section "The account's credentials in git" |
| Accounts and sign-in | `ViewModels/Settings/AccountsSettingsViewModel.cs` and `AccountBlockViewModel.cs` (a block per provider, D353), `ViewModels/Dialogs/SignInDialogViewModel.cs` (a dialog per provider; `OtherProviderFormIn`, D387), `Views/Dialogs/SignInDialogView.axaml`, `Views/Dialogs/SettingsDialogView.axaml`, `MainWindowViewModel.Hosting.cs` (`SignInAsync(provider)`), `AccountTexts` in `OperationTexts.Hosting.Accounts.cs`. Section "The settings screen" |
| The provider's words | `ViewModels/Hosting/HostingTexts.Provider.cs` (name, request word, `#n` or `!n`; D355, D362) and `HostingTexts.Describe.cs` (each failure in its provider's words); the `OperationTexts.Hosting.*.cs` files |
| Browse and clone | `ViewModels/Dialogs/CloneFromGitHubDialogViewModel.cs` (`CloneProviderChoice`, the switch) and `Views/Dialogs/CloneFromGitHubDialogView.axaml`, 7a's names kept for all three providers; `WelcomeViewModel.cs` (D354's button), `MainWindowViewModel.Commands.cs` (`CloneFromAccountAsync`), `AppCommand.CloneFromGitLab` and `.CloneFromAzureDevOps` (`ViewModels/Commands/AppCommands.Palette.cs`). Section "Clone from an account" |
| Requests in the left panel, the menus, checkout, the badge | `ViewModels/Panels/PullRequestRows.cs`, `LeftPanelViewModel.cs`, `ViewModels/Menus/RepositoryMenus.Hosting.cs`, `ViewModels/Hosting/PullRequestCheckout.cs` (D361), `RepositoryOperations.PullRequests.cs`, Git `GitRepository.PullRequests.cs`, `Controls/CommitGraphControl.Badges.cs`. Section "Pull requests" |
| A new request | `ViewModels/Dialogs/CreatePullRequestDialogViewModel.cs`, `Views/Dialogs/CreatePullRequestDialogView.axaml`, `RepositoryOperations.CreatePullRequest.cs`, `CreatePullRequestTexts` in `OperationTexts.Hosting.Create.cs`. Section "Pull requests" |
| The fakes and the harness | `tests/VisualCommit.Testing/Hosting/` (`FakeHostingService*.cs`, the core taken out of `FakeGitHub`; `FakeAzureDevOps*.cs`, `FakeGitLab*.cs`, `HostingSessionGitHubForms.cs`), `AuthRemoteServer.cs`'s host mode by URL path (D363), `Scenarios.Phase7b*.cs`, `Headless/ShellDriver.Phase7b.cs`, `Headless/ShellDriver.Budget.cs` (D390), `GitUrlUser.cs` (D389). Section "The fakes of Azure DevOps and GitLab" |
| A visual check of 7b | `tests/VisualCommit.VisualTests.Phase7b/Phase7bChecks.<part>.cs` with `.Helpers.cs`, `Phase7bExpectations.<part>.cs` and `.Common.cs`, `Phase7bExpectationTests.<part>.cs`, the base `Phase7bChecks.cs` and `.Support.cs`; the real window's `tests/VisualCommit.RealWindowTests/Phase7bRealWindowPass.cs` with `.Accounts`, `.GitLab` and `.AzureDevOps.cs`, and `RealApp.Phase7b.cs` |
| The live command | `tests/VisualCommit.LiveHostingTests/AzureDevOpsLiveTests.cs` and `GitLabLiveTests.cs` (`Check_1` to `Check_3`, and Azure DevOps' `Check_4`), `AzureDevOpsLiveApi.cs`, `GitLabLiveApi.cs`, `LiveRequestLog.cs` (`AzureDevOpsReach`, `GitLabReach`), `LiveScreen.cs`, `LiveRun.cs`, `LiveSafetyTests.cs`. Section "The live command" |

Phase 8's work lies mostly elsewhere, in places 7b did not change but which carry its providers: the
askpass helper's place (`src/VisualCommit.Git/Prompts/AskPassLocator.cs`; risk 14), the build's
trim (`Directory.Build.targets`, D239), macOS's store
(`src/VisualCommit.Hosting/Tokens/MacKeychainStore.cs`; risk 34) and the real-window harness's app
path (the `AppExecutable` metadata in
`tests/VisualCommit.RealWindowTests/VisualCommit.RealWindowTests.csproj`, read by `RealApp`).

## Patterns to follow

Phase 7a's patterns still hold (its handoff's table), and those of the phases before. Phase 7b adds:

| For | Example |
|---|---|
| A hosting provider's operation | `src/VisualCommit.Hosting/GitLab/GitLabProvider.cs`'s `CreatePullRequestAsync` over `GitLabProfile` (`GitLabClient.cs`) on `Http/HostingHttpClient.cs`: the token an argument of every call and kept by none; the profile classifies each answer into a `HostingFailure` kind, a create's refusal into a `PullRequestRefusal`, and only the App words them (`HostingTexts.Describe`, D362); the core screens e-mail addresses out of every failure (D368) and logs an exception by its type and codes, never its message (D380, D382). Tests: `GitLabProviderTests`, `GitLabClientTests` over `FakeGitLab.InProcess(root)` |
| A fake's route | `tests/VisualCommit.Testing/Hosting/FakeGitLab.Router.cs`: one key per route (`user`, `projects`, `project:<key>`, `mrs:<key>`, `create:<key>`) that `FakeHostingService`'s `HoldNext`, `FailNext` and `Requests` name; state in `.Model.cs`, shapes in `.Json.cs`, git in `.Git.cs` through `AuthRemoteServer`'s host mode (D363); each answer marked MEASURED with its date, READ or ASSUMED, and an ASSUMED one confirmed by the live command before a check copies it (D370, D342). Tests: `FakeGitLabTests`, `FakeGitLabGitTests` |
| A part of the walk-through | `tests/VisualCommit.VisualTests.Phase7b/Phase7bChecks.N.cs` with `.N.Helpers.cs`, `Phase7bExpectations.N.cs` (names, texts, rows) and `Phase7bExpectationTests.N.cs`; `ServeAsync` serves the three fakes and `StartEmptyAsync` or `OpenAsync` starts the app (`Phase7bChecks.Support.cs`); the state both harnesses build is `Phase7bExpectations.PrepareRequestsAsync(copy, provider)` (`.Common.cs`); a step that must start no git call asserts it with `QuietStepMarkAsync` and `AssertNoStepCallsAsync` (D390) |
| A real-window launch | `tests/VisualCommit.RealWindowTests/Phase7bRealWindowPass.GitLab.cs`: GitLab's two launches, "Sign-in, browse, clone" and "Requests and create", each a method over the launch it shares with Azure DevOps (`SignInBrowseAndCloneAsync`, `RequestsAndCreateAsync` in `Phase7bRealWindowPass.cs`); the pass's five methods, one launch each, run in `Phase7bOrder` (design 14.2: the three accounts, then GitLab's two, then Azure DevOps' two); they sign in by typing the scenario's made-up tokens into the real dialogs with the keychain in memory, and each capture compared with its scripted twin by the twin's name |
| A live check | `tests/VisualCommit.LiveHostingTests/GitLabLiveTests.cs`: `Check_3`'s `AppDrivenAsync` drives the headless app through the dialog, the browser, the clone, the push and the form, and cleans up in `finally`; every request passes `LiveRequestLog.cs`'s guard (`GitLabReach`: the sandbox project alone, D393), which refuses it before it leaves; every note passes `LiveScreen.cs` (D372, D384) and the token search (D323); `LiveSafetyTests.cs` rehearses the same code over the fakes behind loopback-only guards |
| A test of git behaviour that versions differ in | `tests/VisualCommit.Hosting.Tests/SignInBindingsWindowTests.cs` with `tests/VisualCommit.Testing/GitUrlUser.cs`: measure the git under test once per process and assert what that git gives (D389), never what its version suggests |

Rules worth keeping:

- **No token, organization or namespace in the repo**: besides 7a's rule for tokens, the Azure DevOps
  organization's name and GitLab's namespace stay out of every file and run.txt that enters the repo
  (D372, D384); the fakes take made-up copies of the measured shapes.
- **A made-up token of a real token's full shape is written split** (`"glpat-" + …`), or GitHub's
  push protection refuses the push (Things that cost time).
- **The new providers' bindings fail closed**: a remote URL out of D369's form binds nothing; D379,
  D381 and D391 list the spellings and the includes that wave 1's checks and the review found.
- **Run the walk-through checks a fix touches, and the lagged list at each merge** (D262): the lag
  found two races that no plain run showed.

## Deviations from the plan

| What | Decision |
|---|---|
| The week's allowance used before its reset; one phase with one gate, not split per provider; the design 274.6 KB against D243's 150 to 200 (386.8 KB with its as-built notes) | D344, D365 |
| Personal access tokens, GitLab's fine-grained or legacy kind; fakes and the live command in the same project; Azure DevOps Services and gitlab.com only; one account per provider (amends D263); avatars GitHub's alone; D255 and D257 for the new hosts, Azure DevOps' token for its own organization's remotes alone; "merge request" and screens that name their provider | D345 to D351 |
| The Azure DevOps sandbox in an organization that holds other work (sets aside D260's reason for that token) | D352 |
| Three fixed blocks in the Accounts section with one intro, which changed the intro of 7a's 23 part A pictures; the clone form's button, switch and palette commands; `!n` on GitLab and Azure DevOps; the other organization's row | D353 to D356, D366, D376 |
| One session with a slot per provider; `HostedRepository` and the shared HTTP core; `x-access-token` for all three (amends D275, D278); Azure DevOps' sign-in with the organization; GitLab's checkout by `refs/merge-requests/<iid>/head`, Azure DevOps' through a remote's branch alone; one place for the providers' words, GitHub's word for word | D357 to D362 |
| The fakes' shared core, host mode by URL path, ports 47397 to 47399 (amends D97 and D266); the fakes' MEASURED, READ and ASSUMED marks; e-mail addresses screened out of every failure; D369's form for the new providers' bindings | D363, D368 to D370 |
| The live command: a class per provider and its variables; what it may reach (extends D324, D327) and screen (amends D323); made-up credentials sent to record refusals; one read outside the sandbox; no fork and no legacy token; GitLab's guard narrowed to the sandbox project (amends D371) | D364, D371 to D375, D384, D393 |
| Avatars only while the listed remote is GitHub's (amends D252 and D349) | D367 |
| CI's limits 70 and 80 minutes before the new tests (amends D232); another provider's token refused without a request | D377, D378 |
| Wave 1's adversarial checks: `Select` fails closed on more URL spellings; exceptions logged by type and codes alone | D379 to D382 |
| An account switch drops the tab's list; GitLab's Project: Read among a token's user permissions; git's whole tree at below-normal priority in a Windows job (amends phase 6's design 8.10); the dialog finds another provider's token inside a pasted value | D383, D385 to D387 |
| A network operation inside D290's window binds once the URL rules are known (extends D332); D278's checks by the measured git; a no-call step sets aside only the watchers' refreshes | D388 to D390 |
| A clone's bindings read git's conditional includes; the review before the gate's rulings (phase 2's commit boxes to phase 8, risk 44) | D391, D392 |
| The fake of GitHub answers a commit it lacks beside `author` 404 (amends D342 (3)); a lookup's 404 in a repository not known to be readable is checked against its read (amends D339 and D342 (2), the owner's approval) | D394, D395 |
| CLAUDE.md's start of a session never checks out or pushes a local `wip/...` or `backup/...` branch (the close's cold read) | D396 |

## Known issues

| Issue | How much it matters | Where |
|---|---|---|
| A bound tab whose account is refused or signed out just after an operation bound its git calls shows git's sign-in dialog; on dev.azure.com with `useHttpPath` off the prompt names no organization, so the window is wider | Low; the user can cancel. For the owner | plan risk 39, design 8.3, 16.4 |
| A git that loses a URL's user after the first 401 (git 2.56.0 without its fix `76524c2604bc`, over curl 8.23.0: CI's macOS) blinds D275's refusal of another user, so 7a's github.com gaps give the token without a prompt, on the bound host alone | Low; for the owner, who may harden (the lead recommends leaving it) | plan risk 43, D389, design 16.3 |
| What the bindings cannot see: 7a's github.com rule's raw comparison, submodules under a bound URL, a cross-host redirect into a bound path; the token reaches the covered host only | Accepted | plan risk 45, design 16.3 |
| GitLab's 100 requests a minute against the first lists: about five restored GitLab tabs with 20 or more forks' requests reach it, and the later tabs show the rate limit until their next refresh | Low until a report; for phase 8 or later | plan risk 44 |
| D392's accepted cases: an Azure DevOps 401 that is a scope failure signs the account out; a request queued for its turn when the account signs out or is replaced goes with that account's token; the providers check a token's form on the whole value, the dialog inside it; GitLab's "already open" text wraps inside `origin/main` (`n05b`) | Low | D392, design 16.3 |
| Phase 2's commit boxes and Amend stay editable while "Committing…" runs, and the commit's end clears what was typed meanwhile | Phase 8 | D392, plan's phase 8 |
| Unmeasured answers: Azure DevOps fork shapes, a legacy GitLab token's sign-in, expired and revoked tokens, 429 and 5xx bodies, Azure DevOps' `remoteUrl` for unusual names; the services can change an answer between gates, as github.com did (D394) | Low; each gate's two live runs | plan risks 42 and 38, design 3.7, 4.7, 16.1 |
| Phase 6's `PaletteGrowthTests` fails rarely on CI (3 of the phase's 29 runs, ratios 6.5 to 7.9 against 6.3); a refuted fix stays unmerged on `wip/p7b-palette-growth` | A CI run of a gate's commit can fail on it alone | plan risk 46; the Notes of status.md on the phase's branch, "The two Linux failures of 42301846503" |
| D386's open case: a process that read git's job before git joined it and shows only after the runner's last round runs at normal priority | None seen in about 480 trials | plan risk 47 |
| No run uses a real keychain in the app's own process and no 7b real window opens the browser; macOS's token items are readable through `/usr/bin/security`; a netrc entry answers before the helper on git 2.31 to 2.45 | Phase 8's checks by hand, its signing and its git versions | plan risks 41, 34, 35 |
| The run time: the default run 1 h 31 min 48 s for 7,892 tests (`VisualCommit.Hosting.Tests` 13 min 42 s alone, about 53 minutes beside the others), the real-window pass 33 min 35 s; CI's Test steps up to 41 min 6 s on Windows and 46 min 21 s on macOS against D377's 56 | Every full run, the gate and CI | plan risks 8, 12, 18; D343, D377 |
| Q1 1,827 ms of 2,000 in the counted pass, which started straight after the default run | A hot laptop fails a gate by itself | plan risk 10 |
| Pictures that vary by nature, besides 7a's: phase 5's `a2g` (two reads that start together), the fakes' fallback port where a port is taken (7a's `b06a` and `a09b`), anti-aliasing forms in phase 6's `l27e` and phase 3's `15b3` | Ordinary: an inspection expects them | the report's "Run"; the Notes of status.md on the phase's branch |
| Leftovers at this writing: seven worktrees under `D:/Projects/Personal/vc-wt/`, 24 `wip/p7b-*` branches (all merged but `wip/p7b-palette-growth`), two local `backup/*` branches with WP-A's commits from before the token-shaped literals were split (never to be pushed), and the empty folder `D:/Projects/Personal/vc-wt/p7b-live-fix/src/VisualCommit.App` | Disk and clutter | For the Do first below |
| `VisualCommit.slnx`'s comment on the live project still says it acts on the owner's GitHub account; since 7b it acts on three services (D346) | None; a change to it after the Gate commit would show in the acceptance check, so it waits | Phase 8's first code commit |
| Phase 7a's known issues still stand, but for `scripts/heavy-run.sh`'s comment (L1); its row on GitHub's empty 500 for an unknown commit now reads as plan risk 38 says, since github.com answers 404 (D394, D395) | As there | [phase 7a's handoff](phase-7a.md) |

## Things that cost time

- **The default run under contention.** `VisualCommit.Hosting.Tests` took 13 min 42 s alone and about
  53 minutes beside the other projects, `VisualCommit.App.Tests` 28 min 24 s alone and about 86
  minutes beside them; WP-N's merge run took 1 h 37 min 45 s, past design 0.6's 78 to 85. The test
  platform does not start the projects in a fixed order (design 16.2).
- **CI found what the laptop did not** (the CI section of status.md on the phase's branch): .NET off
  Windows reads `/p?page=2` as a file address; Windows' and macOS's curl keep another user's name
  behind a relative 301; git 2.43's clone sends its first request without credentials (B7);
  Homebrew's git over curl 8.23.0 loses a URL's user (D389); git's console host at normal priority on
  Windows (D386); and races that a loaded runner shows: a log line posted after its state, the
  window's close (a race in the app, `85d599c`), the watcher's dispose, `ShellDriverTests`' 0.3 s
  wait, A9's and the sign-out's waits; besides them, W3's and K10's half pixel of tolerance, within
  which a random folder name fell (both now assert exactly). CI's gits can be run here with the kept
  installs (Environment).
- **The lag found races too**: phase 2's `Check_15_amend` (`26b44c7`) and P11's exact budget of 0,
  which a late whole refresh broke (D390).
- **A flake that no fix holds**: `PaletteGrowthTests`; a first fix was refuted as a loosening, and 414
  paired trials found no measure that holds (plan risk 46).
- **GitHub's push protection** refused the push of WP-A's merge for made-up `glpat-` values of a real
  token's full shape. WP-A's branch, never pushed, was rewritten with `git filter-branch` to split each
  literal, and the merge was made again (`b6de3cf`). Every builder's brief says so since.
- **A token shown in the conversation**: on 2026-11-02 both new sandbox tokens were shown in the
  session, so both were revoked and made again; the lead's PowerShell script, which reads a token from
  the clipboard, lost one and saved a message with it in its first two forms. Tokens go through such a
  script, never the conversation.
- **The live command's premises**: a GitLab fine-grained token needs Project: Read among its user
  permissions (D385); the GitLab account listed five projects, seen in stage 1 but reported only on
  2026-11-08 (D393); the sandbox's organization allows public projects, which decided A9 (open point
  8). Read every row a live run lists.
- **Services change between gates**: github.com answered a commit it lacks beside `author` with 500
  on 2026-10-30 and 31 and 404 on 2026-11-08 (D394); the fake's correction let a fork's tab lose its
  avatars, which only the fix's review found (D395). Run each provider's live command as soon as it
  exists, and review a fake's correction as code.
- **Network stalls to gitlab.com** stopped GitLab's `Check_2` in the gate's first live run (git's push)
  and again in the class's first run alone (an app request), and its second run alone passed. A loop
  of plain requests beside that run met the same kind of stall (connected over IPv6, then 20 s without
  an answer). Run it again before suspecting the app.
- **A stale incremental build**: a fixer's scratch revert, put back with its old time, kept the old
  DLL. The main checkout's merge writes every changed file anew, and its build is the one the lead's
  runs use.
- **The design's size**: its drafting took 4.55M tokens, its review 4.52M and its fixes 3.33M, 2.84M
  and 0.82M; the report is about 445 KB. Two providers' contracts in one design pass D243's size.

## For the next phase

Reuse:

- **What the release ships**: `visualcommit-askpass` is the credential helper for all three
  providers (`--vc-credential`), so a package without it binds no account (risk 14); macOS's store
  keeps each provider's token as a login-keychain item named by `TokenEntryNames`
  (`VisualCommit:<provider id>:<host>`), which the signing decides about (risk 34); a portable
  `dotnet publish` needs `-p:VisualCommitAllRuntimes=true` (D239).
- **The checks by hand**: plan.md's phase 8, "From phase 7a" and "From phase 7b" (risk 41), and the
  macOS checklists at the end of the 7a and 7b reports.
- **The full regression against the installed build** (plan.md's phase 8, "Visual checks"): the
  real-window harness starts the app that its csproj's `AppExecutable` names (the Release build
  output), so a pass against the installed app needs that path, or a second one beside it.
- **Naming**, as 7a's and 7b's designs 0.3 named theirs: `8` in the docs
  (`docs/test-reports/phase-8.md`, `phase-8-pictures.sha256`, `artifacts/visual/phase-8/`); if the
  phase adds a walk-through, the project `tests/VisualCommit.VisualTests.Phase8`,
  `Phase8Expectations.Phase = "8"`, an `8)` case in `scripts/time-scripted-check.sh` with its default
  moved to 8 in the commit that creates the project, and the real-window link
  `..\VisualCommit.VisualTests.Phase8*\Phase8Expectations*.cs`. Ports 47391 to 47399 are taken or
  reserved: a new one takes 47400 or later, pinned in `AuthRemoteServerTests`, by a decision that
  amends D97 as D265 and D363 did.

Do first:

1. The start-of-session steps. CLAUDE.md's step 4 names `architecture.md` and `decisions.md` whole
   (about 836 and 353 KB; whether to read them by an index instead is the owner's, Waiting on the
   owner). What phase 8 needs of them, about 240 KB: in `architecture.md` "Design", "Solution layout",
   "As built (after phase 7b)", "Start-up and composition", "Prompts and credentials" with "The
   account's credentials in git", "Keyboard shortcuts", "Tokens and the keychains", "Q1 measurements",
   "Settings, session, log and data folder", "Shell and views", "Theme", "Conventions", "Tests", "CI"
   and "Package versions"; in `decisions.md` D241 to D248 and the decisions that plan.md's phase 8
   cites (D82, D198, D239, D255, D340 and D392 among them), any other by its number where a
   section cites it. Then plan.md's phase 8 and its risks 1, 6, 9, 14, 15, 31, 34, 35 and 41, with 3,
   8, 10, 12, 18, 26, 27, 42, 43 and 46, and 38 and 44, which name phase 8 unless a report comes
   first; the Known issues above and 7a's and 6's; CI's run on `master` after the merge. On the Windows machine push in the form CLAUDE.md's Commands give until the owner settles Git
   Credential Manager.
2. Before the design, the owner's answers, each recorded as a decision and carried into plan.md:
   the code-signing certificate and the Apple Developer membership, which cost money (risk 6); the
   installers per platform and where the installers, the updates and the beta are published (a
   release on GitHub is the owner's to allow, CLAUDE.md's Rules); whether to bundle git on Windows,
   and the git versions the release supports, with risk 35's netrc note and risk 15's OpenSSH; the
   macOS token store with the signing (risk 34); the icons, the branding and the user guide's form;
   the confirmation that the text boxes' placeholder contrast moves into the accessibility pass
   (plan.md's phase 8, "for the owner to confirm"; the phase 6 handoff's Waiting on the owner); and
   whether the phase is split as D241 split phase 7, by a decision that amends D241, names the
   phases and their branches and changes CLAUDE.md's counting of phases in the same commit.
3. The leftovers in Known issues, if the closing session did not remove them: the worktrees, the
   merged `wip/p7b-*` branches and the empty folder; `wip/p7b-palette-growth` (at `c90b862`) and the
   two `backup/*` branches (`backup/phase-before-split` at `d3a5780`, `backup/p7b-accounts-before-split`
   at `bf6383c`) stay local or go, never pushed; CLAUDE.md's step 1 leaves them out (D396). Then fsmonitor daemons and room on C: and D:. A baseline
   whole run only as D245 allows.
4. The design: 7b's is the model (section 0 with the lagged list in 0.4, the cost in 0.6 and the
   earlier pictures in 0.7, section 1's contracts, then a brief per package). Estimate what the phase
   adds to the default run (1 h 31 min 48 s at 7b's gate) and to the real-window pass (33 min 35 s),
   and how the full regression against the installed build runs beside them, before writing the
   checks.

Risks to watch:

- The run time (plan risks 8, 12, 18): the default run grew by about 19 minutes in 7b, and CI's macOS
  Test step reached 46 min 21 s against the 56 at which D377 raises its limit (only at a merge before
  the counted round).
- Q1 (risk 10): 1,827 ms of 2,000 with no rest before the pass; rest the laptop at phase 8's gate.
- Git's versions (risks 26, 43): CI's macOS git loses a URL's user until Homebrew ships git 2.57.0 or a
  patch, the containers run 2.43, and D340 holds from 2.46 only; each job's `git-url-user.txt` says
  which kind of git it ran.
- The palette flake (risk 46), which can fail a CI run of a gate's commit alone.
- The live tokens (risk 42): GitHub's expires on 2026-11-29, Azure DevOps' and GitLab's on 2026-12-04.
  A gate runs the live command twice (D335), so a gate after a date needs that token made again first,
  as 7a's design 14.6 and 7b's design 15.8 say, set through a script that never shows it and checked
  as CLAUDE.md's "What to know" says.

## Waiting on the owner

- **Acceptance of phase 7b**: the whole gate passed on the Gate commit, `cb9af6d`,
  and the commits after it change only docs. The macOS checklist in the
  [phase 7b report](../test-reports/phase-7b.md) ("macOS checklist for the owner", seven steps) is the
  owner's, by hand on a Mac, beside 7a's and those of phases 0 to 6.
- **A sign-in dialog in a bound tab** (plan risk 39), now for three providers: should the broker
  refuse such a prompt without a dialog, from what the session knows (amending D273 and D340)?
- **The banner's brief torn state** (plan risk 40): leave it, or read the in-progress files before and
  after `git status`?
- **A git that loses a URL's user** (plan risk 43, D389): harden now, which reopens design 16.3, or
  leave it as a recorded risk until a report, as the lead recommends?
- **The live tokens' expiry**: GitHub's on 2026-11-29, Azure DevOps' and GitLab's on 2026-12-04; a
  later live run needs new ones.
- **Decisions made during 7b without the owner**, which the owner may want to look at: the lead's
  refinements of D345, D346 and D350 after the owner's acceptance; the design's D357 to D372 (after
  its review, with the owner's answers of 2026-11-01); D377 and D378 of its review; D379 to D392, the
  lead's rulings in the build and the review before the gate, D385 among them from stage 1's
  measurement; D394 from the gate (D393 is the owner's choice, D395 the owner's approval); D396 from
  the close's cold read; and the lead's rulings in the design's As built notes and the report's open
  points.
- **For phase 8**: the questions under "Do first", step 2.
- **Still open from earlier** (status.md's "Waiting on the owner" gives each): phase 6's three
  questions asked at its acceptance; D240's connectors switch; the review before phase 7's questions
  1, 10 and 12 and its unnamed proposals; Git Credential Manager's sign-in since git 2.56; the start
  reading by index (the review's P6); CI's duration, when the project is over (D134).
