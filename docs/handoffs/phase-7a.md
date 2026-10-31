# Phase 7a handoff — Hosting foundation and GitHub

Written at the end of phase 7a for the session that runs phase 7b. That session has not seen any
of this phase's conversation. How the app is built is in [architecture.md](../architecture.md)
(its "Hosting", "Tokens and the keychains", "Clone from GitHub", "Pull requests", "The fake of
GitHub" and "The live command" sections are 7a's), why in [decisions.md](../decisions.md): D241 to
D248 (the review before phase 7) changed how the phase was built; D249 to D264 are the owner's
answers at the phase's start, before the design; D265 to D338 the design's, with the owner's
answers to its review; D339 to D341 those of the review and the runs after it; D342 the live run's;
D343 the gate's, the owner's choice. The design is
[phase-7a-design.md](../phase-7a-design.md): section 0 the scope and the packages, 1 the shared
contracts, 2 to 14 one brief per work package, 15 the open points, 16 to 21 what the build, the
review, the live run and the gate settled ("design N.N" below). The report,
[test-reports/phase-7a.md](../test-reports/phase-7a.md), holds the 52 checks (A1 to A11, B1 to B9,
P1 to P15, G1 to G7, N1 to N10), the live command's runs and the lead's rulings on its open points.

## Result

Everything in the plan's "Delivers" list was built. The report's "What proves each Delivers item"
is the longer form of this table.

| Delivered (plan.md, phase 7a) | Proven by |
|---|---|
| The provider model that 7b builds on: `HostingProvider`, `AccountKey`, `IHostingProvider` (A1 to A6, recognition, the push guard's rule), `IHostingSession` and `IRepositoryHosting`, tokens keyed by provider and host | P1, P6, G5, N1, N2, N5; `GitHubRecognitionTests`, `GitHubProviderTests`, `HostingRulesTests`, `HostingSessionStartTests`, `HostingSessionSignInTests`, `RepositoryHostingTests`, `RepositoryHostingWiringTests`, `CreatePullRequestOperationTests` (a fork's head, the remotes' order) |
| Sign-in with a personal access token in Settings, Accounts; one account; the keychain, else the session (D249, D253, D256, D258, D263); the macOS and Linux stores on CI (D259) | A1 to A11 (the real window repeats A1, A2, A5); B7, B8; P2, P4, P11; N6; `AccountsSettingsViewModelTests`, `SignInDialogViewModelTests`, `SignInEntryTests`, `JsonAccountStoreTests`, `TokenStoresTests`, `WindowsCredentialStoreTests`, `SecretServiceStoreTests`, `FakeTokenStoreTests`; the real stores' `WindowsTokenStoreRealTests`, `MacKeychainTokenStoreRealTests` and `SecretServiceTokenStoreRealTests` in CI's keychain steps, green from run 40878208671 (`8b02805`) on; Windows' runner gave no error 1312, so D333's fallback was not needed |
| The account's token on the app's git calls to github.com through the app's own credential helper (D255) | A9, A11, B6 (the real window repeats it in the real process), P13, N2; `AccountCredentialTests`, `CredentialBindingSelectTests`, `UrlRewritesTests`, `UrlRewritesParityTests`, `AskPassChannelTests` (Git.Tests), `RefusedTokenDisplayTests` (App.Tests), `HostingSessionCredentialTests`, `WelcomeCloneFromGitHubTests`; the live command's clone (2.6 s, no dialog, no helper of the user's) |
| Browse and clone (D261) | B1 to B9 (the real window repeats B3, B6, B7); `CloneFromGitHubDialogViewModelTests`, `CloneFromGitHubDialogViewTests`, `PaletteCloneFromGitHubTests`, `HostingTextsDescribeTests` |
| The pull-request list and its menu, create, the badge, open in browser (D251) | P1 to P15 (the real window repeats P6, P11 and P15, which opens the default browser, D321); G1, G2, G5, G6 (the real window repeats G1); N1 to N10 (the real window repeats N1); `PullRequestSectionTests`, `LeftPanelPullRequestsViewTests`, `PullRequestCheckoutTests`, `PullRequestOperationsTests`, `GitRepositoryPullRequestsTests`, `PullRequestFetchHeadTests`, `RepositoryMenusHostingTests`, `GraphPullRequestBadgeTests`, `CreatePullRequestDialogViewModelTests`, `OperationQueueOfferTests`, `StatusBarOfferTests` |
| Author avatars from GitHub only (D252) | G3 to G7, A8 (no `avatars/` after a sign-out, D291); `GraphAvatarTests`, `CommitDetailsAvatarTests`, `RepositoryAvatarsTests`, `RepositoryHostingAvatarsTests`, `AvatarCacheTests` |
| The opt-in live command (D250) | `tests/VisualCommit.LiveHostingTests` (the file); its runs with the owner's token, 80 of 80 and 112 of 112 (the report's "Live command"); `LiveSafetyTests`, 109 offline cases |
| From phase 3: "Create pull request…" in `ForLocalBranch`, `ForRemoteBranch` and `ForDrop`; the section's menu | P11, P12, N1, N5, N6, N8; `RepositoryMenusHostingTests` |
| From phase 3: a provider's token reaches git | A9, B6, P13, N2; WP-C's classes above. D255 settled it: the app's own helper, in place of the user's helpers for github.com |
| From phase 6: the push guard and `pr/*` | N2, N7 (the guard's read before Create's push); `PushGuardGitHubTests` (ruling 8: `refs/pull/<digits>/head` counts; the two-namespace refusal stays) |
| From phase 6: the prompt group per account | `PromptGroupAccountTests` (ruling 9: kept answers keyed by account and prompt text) |
| From phase 6: the wheel test | `ShellDriverTests`' wheel test through `ShellDriver.WaitForControlAtAsync` (L1, `f1dc80e`) |
| From phase 6: the real-window csproj's comment | The file (L1) |
| From the review: W1, S8, R8 (D254), and G23's split (D264) | WP1 (`a4a388e`): W1 on a held read clock (design 5.4); `GitCallBudget` and `GitCallBudgetTests` (D280, D281); the watcher lag `VISUALCOMMIT_WATCHER_LAG_MS` (D262, D282), whose lagged list (design 0.4) passed at every merge that named it; G23's three tests (D283), their pictures the same files but for `g23i`'s seven pixels that vary by nature (design 16.9) |

The plan's "Done when": sign-in, clone, the list and a create work for GitHub against the fake (A,
B, P and N) and against real GitHub (the live command's `Check_2` signs in, clones, lists and
creates a draft, which it closes again); the visual gate:
<TO FILL AT THE GATE'S END: passed on which commit, in which round>.

Not delivered, and where each item went:

- **Azure DevOps and GitLab**: phase 7b (D241).
- **Backlog** ("Left out of phase 7a"): GitHub Enterprise Server and GHE.com (D258), several
  accounts at once (D263), the device flow (D249), pull requests and avatars signed out (D257); and
  by the design (design 0.1, 0.2; D297, D299): a page, column or timer for pull requests, a "Create
  pull request…" for the current branch in the section's menu, a palette entry for Accounts or
  signing in, a scope or expiry warning at sign-in, S8's batched config reads (design 5.1), a pull
  request from a fork in the same organisation, which needs `head_repo` (design 12.6), and letting
  through a new branch under two namespaces' pattern lines, which the push guard still refuses.
- **Phase 8's keyboard pass** (plan.md's phase 8, "From phase 7a"): no control of the clone form
  has the focus after "Choose" closes the browser (`b06a`, design 17.5); a loading diff takes the
  focus when its text arrives, after the user moved it (phase 6's behaviour, design 17.11).
- **Phase 8, with the release's signing and supported gits**: macOS's keychain item, readable by
  any process of the user through `/usr/bin/security` (risk 34); the netrc note, whether the
  Accounts section says that a netrc answers first on git 2.31 to 2.45 (risk 35). Lapsed: design
  18.2's draft question, since GitHub accepted the draft (design 20.1).
- **Phase 8's checks by hand** (plan.md's phase 8, "From phase 7a", risk 41): a sign-in kept in
  Windows' Credential Manager across a restart of the installed app, and on a Linux desktop the
  Secret Service's prompts and the browser; the Mac's are this phase's macOS checklist.
- **Left open, unscheduled**: risks 36 to 40 (40: the banner's brief torn state, design 21 item 4)
  and GitHub's empty-repository 409 text, which the sandbox cannot show (design 20.8, plan risk 42).

## State of the repo

Branch `phase/7a-hosting-github`, started on 2026-10-28 from `master` at `5fe9148` and built on the
Windows machine to 2026-10-31. The last commit that changes code is `a9d02c1` (2026-10-31); the
branch's head at this writing is `8dd323d`, which changes docs only.
<TO FILL AT THE GATE'S END: the branch's last commit, the number of commits on top of `5fe9148`, and the Gate commit>.

Before the merges: L1 `f1dc80e` and WP0 `d3cd314` to `8bfdd6b`, commits on the branch. The merges
(`git log --oneline --merges phase/7a-hosting-github ^master`): wave 1 WP-K `2582e53`, WP-F
`c562ff4`, WP-C `afb91a7`, WP-G `1d6b688`, WP1 `a4a388e`; the prompt channel's held test `6fc2d28`
and fix `9d48d0b`; WP-S `7bea4fb`, WP-A `42b6be2`, WP-B `b361388`, WP-P `1958755`, the double-click
fix `ccd1ebe`, WP-V `8810136`, the macOS races `33313ef`, G1's fix `5219eae`, WP-N `93e3410`; WP-R
`5cfce9f`, WP-LIVE `b695778`; the review's fixers `8e98d4a` to `e5d09fb`; the live run's corrections
`5c20ee6` and `c6491cb`. Between the fixers and the live run: `1d297a0` (two test races, part P's
`End`), `745ce9a` (D341) and `7015753` (the create test's fence). After the live run: `f926b86` and
`a140017`, the gate's splits `939e9fa` to `377d354`, Check_N `723f2d3`, D343 `391f7f2` and the torn
reads `a9d02c1`; status.md's Progress names the rest. Merged into `master`:
<TO FILL AT THE GATE'S END: merge>.

- **Default run, counted round** (`a9d02c1`'s code):
  <TO FILL AT THE GATE'S END: date, tests passed of how many and skipped, time, each project's end>.
  The round's third run before it, on
  `391f7f2` (2026-10-30, 22:41 to 23:51, rested, at most four projects at once, D343): 5,946 of
  5,950 passed and 4 were skipped (the three real keychains' tests and
  `WindowsCredentialStoreTests`' Off-Windows test, D270) in 1 h 8 min 39 s; the projects ended
  after: part L's walk-through 6 min 53 s, `VisualCommit.Hosting.Tests` 15 min 5 s, phase 7a's 29
  min 45 s (14 min 40 s of its own), phase 6's 31 min 56 s, phase 4's 51 min 43 s, phase 5's 53 min
  38 s, `VisualCommit.Git.Tests` 55 min 13 s, phases 0 to 2's 61 min 43 s, `VisualCommit.App.Tests`
  61 min 53 s, phase 5's undo walk-through 67 min 30 s and phase 3's 68 min 34 s. For comparison:
  the baseline on `5fe9148`, 5,001 tests in 58 min 13 s (G23 stopped, D264); phase 6's gate, 5,037
  in 56 min 25 s; design 0.6 estimated 66 to 72 minutes. The two runs before it stopped on the
  watchdog, the reason for D343 (Things that cost time).
- **Real-window pass**: D246's two runs of `Phase7aRealWindowPass` (5 methods, one launch per part,
  D319), the owner present: 4 of 5 on `8271c8e` (Check_N, "Found by the gate" item 3), then 5 of 5
  on `723f2d3` in 2 min 55 s (3 min 17 s with its build), every capture within 1.15 percent of its
  scripted twin (2.59 compared in place only; the limit is 3). The whole pass on the counted round:
  <TO FILL AT THE GATE'S END: tests passed, time with and without its build, Q1 within the pass, the largest difference of a capture from its twin>.
  Phase 6's gate: 67 in 27 min 39 s; design 0.6 estimated 31 to 33 minutes.
- **Pictures**: 7a's walk-through (classes `Phase7aChecksA`, `B`, `P`, `G`, `N`: 27 check methods
  and about 20 classes of expectation, view-model, menu and view tests, 205 tests in 3 min 9 s on
  `a140017`) saves 85 scripted pictures. `docs/test-reports/phase-7a-pictures.sha256`:
  <TO FILL AT THE GATE'S END: the number of files (scripted, real-window, difference pictures) by phase, the inspectors and their batches, what they found>.
  The references for the earlier pictures are in `D:/Projects/Personal/gate7a-reference/` (outside the repo; status.md's Notes).
- **Live command**: run 1 (step 16, `0ffbcc2`'s build of `7015753`'s code) 80 of 80 in 33 s; run 2,
  the gate's run before its pictures (`f926b86`), 112 of 112 in 63 s with its build, every probe
  "the fake: same". The counted round's run: <TO FILL AT THE GATE'S END: commit, result, findings>.
- **CI**: the last run recorded is 41172878322 on `391f7f2`: its first attempt failed two tests on
  `windows-latest` ("Found by the gate" item 4, fixed in `a9d02c1`), the other four jobs passed, and
  the Windows job passed on attempt 2 (Test step 31 min 56 s of 55). The run of the gate's commit:
  <TO FILL AT THE GATE'S END: run link, each job's result, time and Test step, the keychain steps>.
- **Gate**: <TO FILL AT THE GATE'S END: passed or not; what could not run>. The real window at
  1920×1080 cannot run on this screen, as the plan allows: G3's Author column with its avatars is
  pictured scripted only (design 17.6), and no pass touches a real keychain (design 0.5; CI's
  keychain steps do).

## Environment

| | |
|---|---|
| Machine | Windows 11 Pro 10.0.26300 on an Intel Core i7-8650U laptop; screen 3000×2000 at 200% scaling (1500×952 logical work area). The whole phase was built on this machine, partly by sub-agents in worktrees (`D:/Projects/Personal/vc-wt/p7a-<key>` and `.claude/worktrees/`, all removed); no cloud session worked on it |
| .NET SDK | 10.0.303 (`global.json` stays at 10.0.100 with `latestFeature`) |
| Git | 2.56.0.windows.4 since 2026-10-28 (D248), first on `PATH`, with `core.fsmonitor=true` in the owner's global configuration and `credential.helper=manager` in the new install's system configuration (Waiting on the owner). Visual Studio's 2.55.0.windows.5 is there too (plan risk 28). On CI: Windows 2.56.0.windows.2, macOS 2.56.0, `ubuntu-latest` 2.55.0, the two containers 2.43.0 |
| Packages | Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, D24), FlaUI.UIA3 5.0.0; new: Tmds.DBus.Protocol 0.94.1, a direct reference pinned to the version Avalonia 12.1.3 brings, for the Secret Service. New Lucide icon: git-pull-request (from Feather), in `THIRD-PARTY-NOTICES.md` with the package |
| New projects | `src/VisualCommit.Hosting`; `tests/VisualCommit.Hosting.Tests` and `tests/VisualCommit.VisualTests.Phase7a` (eleven test processes in the default run); `tests/VisualCommit.LiveHostingTests` (`net10.0-windows`, not in the solution; CI's Windows job builds it, D337) |
| Ports | 47395 for 7a's walk-through and its real-window part (D265; a class falls back to a free port when it is taken); 47396 reserved for a second 7a walk-through project; 47391 to 47394 as before |
| GitHub | The owner's private sandbox `RahimPasha/visualcommit-sandbox` (default branch `main`) and a fine-grained token for it alone, expiring 2026-11-29, in two Windows user variables (D260) |
| Screenshots | Scripted: scaling 1. Real window: 200% and a work area of 1500×952, as `run.txt` records them |

## Build, run and test

The table with notes is the Commands section of [CLAUDE.md](../../CLAUDE.md#commands). New in this
phase (CLAUDE.md's rows, and the hosting project run whole through the lock, as D245 asks):

```
bash scripts/heavy-run.sh --project tests/VisualCommit.Hosting.Tests
dotnet test --project tests/VisualCommit.VisualTests.Phase7a
dotnet test --project tests/VisualCommit.VisualTests.Phase7a --filter-class "*Phase7aChecksA"
bash scripts/time-scripted-check.sh "<method filter>"
VISUALCOMMIT_WATCHER_LAG_MS=1500 bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests --filter-class "*Phase2Checks"
bash scripts/heavy-run.sh --project tests/VisualCommit.Git.Tests --report-xunit --report-xunit-filename timing.xml --results-directory <folder>
dotnet test --project tests/VisualCommit.LiveHostingTests -c Release --filter-class "*LiveSafetyTests"
bash scripts/heavy-run.sh --project tests/VisualCommit.LiveHostingTests -c Release
```

- `bash scripts/heavy-run.sh` with no arguments is the whole default run with at most four projects
  side by side (D343); with `--project` it adds no limit. Plain `dotnet test` and CI run as many as
  there are processors. Who holds the lock and how to stop a run: CLAUDE.md.
- The lagged list (design 0.4 with 16.1's extra class) is run when merging each wave (D262); a
  failure under the lag gets a fix, never a looser test.
- The real keychains' tests write in the machine's real store, so on Windows and macOS only CI's
  steps "Keychain (…)" run them (D270); the Linux one, under a private bus and a throwaway keyring,
  is safe on any machine (run so far only in CI's two containers, first on `8b02805`; it needs
  `dbus` and `gnome-keyring`, which `scripts/setup-linux.sh` installs, and a container needs
  `--cap-add=IPC_LOCK`):
  `export XDG_DATA_HOME="$(mktemp -d)"; mkdir -p -m 700 "$XDG_DATA_HOME/keyrings"; VISUALCOMMIT_KEYCHAIN_TESTS=secret-service dbus-run-session -- sh -c 'echo throwaway | gnome-keyring-daemon --unlock --components=secrets >/dev/null && dotnet test --project tests/VisualCommit.Hosting.Tests --filter-class "*SecretServiceTokenStoreRealTests"'`.
- The live command acts on the owner's GitHub account: tell the owner first. It reads
  `VISUALCOMMIT_LIVE_GITHUB_TOKEN` and `VISUALCOMMIT_LIVE_GITHUB_REPO`: never print the whole
  environment, and tell every agent so. A session started before they were set gives them to the
  command's process alone, never printed, in PowerShell:
  `$env:VISUALCOMMIT_LIVE_GITHUB_TOKEN = [Environment]::GetEnvironmentVariable('VISUALCOMMIT_LIVE_GITHUB_TOKEN','User'); $env:VISUALCOMMIT_LIVE_GITHUB_REPO = [Environment]::GetEnvironmentVariable('VISUALCOMMIT_LIVE_GITHUB_REPO','User'); & 'C:\Program Files\Git\bin\bash.exe' scripts/heavy-run.sh --project tests/VisualCommit.LiveHostingTests -c Release`.
  Its findings land in `artifacts/live/github/run.txt`, which the next run overwrites: copy it out
  of the repo (status.md's Notes name the folder used).
- To run the app without touching the real keychain, besides CLAUDE.md's `VISUALCOMMIT_DATA_DIR`
  and git variables: `VISUALCOMMIT_KEYCHAIN=memory`; `VISUALCOMMIT_GITHUB_URL=none` turns GitHub off
  and `http://127.0.0.1:<port>/` points it at a fake (architecture.md gives the rules).

## Running the visual gate

As in phase 6's handoff, with the live command, D246's two runs and D343 (design 13.7):

1. **Stop what is left**: every worktree, `wip/p7a-*` branch and fsmonitor daemon (phase 6's handoff
   gives the PowerShell), the main tree's daemon again before each heavy run; check C: and D:.
2. **The live command before the pictures** (D335), the owner told. Its corrections to the fake are
   committed before any picture (7a: run 2 on `f926b86`, its one correction `a140017`).
3. **The phase's scripted pictures on this machine**:
   `bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase7a` (3 min 9 s and 3 min
   24 s), so that the real-window checks have twins.
4. **D246's two runs**, the owner present, the desktop unlocked and Windows' high contrast off:
   `bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release --filter-class "*Phase7aRealWindowPass"`
   (3 to 4 minutes each). 7a's part serves the fake on 47395 in the test process, so 7a's
   walk-through never runs beside it; part P's launch clicks "Open in browser" and leaves one tab on
   the fake's page in the owner's default browser (D321), which the owner closes before the next
   run of the pass. Made once.
5. **The counted round**: rest the laptop 10 minutes, `bash scripts/heavy-run.sh` (about 1 h 10
   min), rest, tell the owner, then
   `bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release` (never
   `--no-build`; <TO FILL AT THE GATE'S END: its time>), then at once the list
   `find artifacts/visual -name '*.png' | sort | xargs sha256sum > docs/test-reports/phase-7a-pictures.sha256`,
   then the live command again; it writes nothing under `artifacts/visual`.
6. **Inspection** (D247, batches of at most about 150): 7a's scripted pictures by part against the
   report; 7a's real-window captures with their twins and difference pictures in one batch (about
   nine captures); the earlier phases' pictures by region against the references:
   `python scripts/compare-pictures.py D:/Projects/Personal/gate7a-reference/artifacts/visual artifacts/visual <output folder>`,
   each change judged against the report's "Changes to expected results" and in full where it lies
   elsewhere; and CI's macOS and Linux pictures of the gate's commit, fetched with
   `gh run download <run id> -D <folder>` (CLAUDE.md), which the report's "Other platforms" names.
   The references are phases 0 to 6's walk-throughs on `master` (`5fe9148`, git 2.56) and phase 6's
   gate's 364 real-window files (`reference-verdicts.md` there). 7a's gate:
   <TO FILL AT THE GATE'S END: inspectors, batches, verdicts>.
7. **After a fix**: one that changes only `tests/VisualCommit.RealWindowTests` re-runs only the pass
   (D246); one that changes only `tests/VisualCommit.LiveHostingTests` only the live command (D335);
   any other, a correction to the fake among them, starts a new counted round. Copy a round's
   `artifacts/visual/` and its list out of the repo before anything runs again.

Screenshots land under `artifacts/visual/phase-7a/{scripted,real-window,timings}`. 7b makes its own
references on `master` once 7a is merged, if its checks change earlier pictures in many places.

## What changed in the code

architecture.md describes each part under the section named here; this table says where to start.
Unless a project is named, a path is in `src/VisualCommit.Hosting/` in the first four rows and in
the avatars' row after its semicolon, and in `src/VisualCommit.App/` elsewhere
(`RepositoryOperations.*` and `OperationTexts.*` in its `ViewModels/Operations/`).

| To do this | Start here |
|---|---|
| The provider model | Core `Hosting/AccountKey.cs` (`HostingProvider { GitHub }`, which 7b extends, and `AccountKey`); `IHostingProvider.cs`, `HostingRecords.cs` (`HostingFailure` and its kinds, `HostingResult<T>`), `HostingOptions.cs` (`GitHubEndpoints`, `FromEnvironment`), `HostingRules.cs`. Section "Hosting: the provider and GitHub's API" |
| GitHub's REST API | `GitHub/GitHubProvider.cs` (A1 to A6, `Recognise`, `SourceCanExist`), `GitHub/GitHubClient.cs` (one request at a time, kept answers, redirects, paging, classification), `GitHub/GitHubUrls.cs`, `GitHub/GitHubJson.cs`, `GitHub/GitHubApi.cs` (the pinned version) |
| The session, the account and a tab's hosting | `IHostingSession.cs` (`IHostingSession`, `IRepositoryHosting`, `HostingRemoteOrder`, `NullHostingSession`), `HostingSession.cs`, `JsonAccountStore.cs` (`accounts.json`, `AccountsFile.GitHubOn`), `RepositoryHosting.cs`; the App's `AppSession.cs` (`AppSession.Start`, `HostingStarted`), `TabServices.Hosting`, `RepositoryViewModel.Hosting.cs`. Section "Hosting: the session and the account" |
| Tokens and the keychains | `Tokens/`: `ITokenStore.cs` (`TokenKey`, `ITokenStore`, `TokenStores`, `MemoryTokenStore`), `WindowsCredentialStore.cs` with `WindowsCredentialNative.cs`, `MacKeychainStore.cs` with `TokenProcessRunner.cs`, `SecretServiceStore.cs` with `SecretServiceAddress.cs` and `SecretServiceCalls.cs`, `TokenEntryNames.cs`, `TokenStoreReasons.cs`, `TokenVault.cs`; CI's keychain steps in `.github/workflows/ci.yml`; `scripts/setup-linux.sh` (dbus, gnome-keyring). Section "Tokens and the keychains" |
| The token reaching git | Core `Operations/CredentialBinding.cs` (`CredentialBinding.Select`, D278; `IGitCredentialSource`), `Git/UrlRewrites.cs`, `Git/RemoteUrl.cs`; AskPass `CredentialMode.cs` and `AskPassProtocol.cs` (`--vc-credential`); Git `Prompts/AskPassBroker.cs` (the three `GIT_CONFIG_COUNT` pairs per binding, D340; `GIT_CONFIG_PARAMETERS` removed, D329; the keeper instance on macOS and Linux); the push guard's `RefuseIfPushStopsFetchAsync` in `RepositoryOperations.Remotes.cs`; a bulk run's kept answers keyed by account in `Services/PromptDialogs.cs` (ruling 9). Section "The account's credentials in git" |
| Sign-in and the Accounts section | `ViewModels/Settings/AccountsSettingsViewModel.cs`, `ViewModels/Dialogs/SignInDialogViewModel.cs`, `Views/Dialogs/SignInDialogView.axaml`, the settings screen's `SettingsAccounts` in `Views/Dialogs/SettingsDialogView.axaml`, the window's entry in `MainWindowViewModel.Hosting.cs`, `AccountTexts` in `OperationTexts.Hosting.Accounts.cs` |
| Browse and clone | `ViewModels/Dialogs/CloneFromGitHubDialogViewModel.cs`, `Views/Dialogs/CloneFromGitHubDialogView.axaml`, the clone form's "From GitHub…" on the welcome page (`WelcomeViewModel.cs`, `Views/Shell/WelcomeView.axaml`), `AppCommand.CloneFromGitHub` (`ViewModels/Commands/AppCommands.Palette.cs`, D299), `CloneTexts`; `ViewModels/Hosting/HostingTexts.Describe.cs` (D338), the one wording of GitHub's failures. Section "Clone from GitHub" |
| The pull requests' section, menus and checkout | `ViewModels/Panels/PullRequestRows.cs`, `LeftPanelViewModel.cs` and `.Actions.cs`, `Views/Shell/LeftPanelView.axaml` (the section and the branch rows' `#n`), `ViewModels/Menus/RepositoryMenus.Hosting.cs`, `ViewModels/Hosting/PullRequestCheckout.cs`, `RepositoryOperations.PullRequests.cs`, Git `GitRepository.PullRequests.cs` (the fetch into `FETCH_HEAD`, D305). Section "Pull requests" |
| A new pull request and the status bar's offer | `ViewModels/Dialogs/CreatePullRequestDialogViewModel.cs`, `Views/Dialogs/CreatePullRequestDialogView.axaml`, `RepositoryOperations.CreatePullRequest.cs` (D312 to D316, D318), `CreatePullRequestTexts`; Core's `OperationOffer` in `Operations/OperationReport.cs`, `ViewModels/Operations/IStatusBarOperation.cs`, `Views/Shell/StatusBarView.axaml` (D317) |
| The badge | `Controls/CommitGraphControl.Badges.cs` (`BadgeInRow`, `PlacedBadge`, D309) |
| Avatars | `Controls/CommitGraphControl.Avatars.cs`, `Controls/AvatarBitmaps.cs`, `ViewModels/RepositoryViewModel.Avatars.cs` (`AuthorsOnGitHub`, D311, D339), `Views/Shell/RightPanelView.axaml` (`DetailsAuthorAvatar`); `RepositoryHosting.Avatars.cs`, `AvatarCache.cs` (D286), `AuthorMapStore.cs` (D291) |
| The fake of GitHub and the harness | `tests/VisualCommit.Testing/Hosting/` (`FakeGitHub.cs`, `.Router.cs`, `.Model.cs`, `.Json.cs`, `.Git.cs`, `FakeGitHubHandler.cs`, `LoopbackOnlyHandler.cs`, `TokenTripwire.cs`); `AuthRemoteServer.cs`'s host mode (D266, `Phase7aVisualChecksPort`); `Scenarios.Phase7a.cs`; `Headless/FakeTokenStore.cs`, `Headless/GitCallBudget.cs`, `RecordingCredentialSource.cs`; `Headless/ShellDriver.Phase7a.cs` and `.Phase7a.Accounts.cs`; the watcher lag in `src/VisualCommit.Git/RepositoryWatcher.cs` and `GitRepository.cs` (D282). Section "The fake of GitHub" |
| A visual check of 7a | `tests/VisualCommit.VisualTests.Phase7a/Phase7aChecks.<part>.cs` with `.Helpers.cs`, `Phase7aExpectations.<part>.cs` and `Phase7aExpectationTests.<part>.cs`; the base `Phase7aChecks.cs` and `Phase7aChecks.Support.cs`; the real window's `tests/VisualCommit.RealWindowTests/Phase7aRealWindowPass.cs` with `.A`, `.B`, `.N`, `.G`, `.P.cs` and `RealApp.Phase7a.cs` |
| The live command | `tests/VisualCommit.LiveHostingTests/GitHubLiveTests.cs` (`Check_1` to `Check_3`, `GitHubLiveOrder`), `LiveProbes.cs`, `LiveRequestLog.cs`, `GitHubLiveApi.cs`, `LiveRun.cs`, `LiveSettings.cs`, `LiveSafetyTests.cs`. Section "The live command" |

The places that name GitHub where 7b adds its providers: `HostingProvider`; `HostingOptions.GitHub`
and `GitHubEndpoints` (one endpoint, github.com or the fake, D258); `HostingSession`, which makes one
`GitHubProvider` and uses the first `github` record of its host; `TokenEntryNames`' provider map
("GitHub"; 7b adds its own, design 3.3); the binding's host and the helper's answer
`username=x-access-token`, which is GitHub's (design 0.2); `HostingTexts.Describe` and the texts that
say "GitHub" ("Add GitHub account", "Clone from GitHub", "Sign in to GitHub…");
`AppCommand.CloneFromGitHub`; `HostingRemoteOrder`'s "first GitHub remote".

## Patterns to follow

Phase 6's patterns still hold (its handoff's table), and those of the phases before. Phase 7a adds:

| For | Example |
|---|---|
| A provider and its client | `src/VisualCommit.Hosting/GitHub/GitHubProvider.cs` over `GitHubClient.cs`: the token an argument of every call and kept by none; a failure a `HostingFailure` of a fixed kind, worded once by `HostingTexts.Describe` (D338); 30 s per request and the caller's cancellation; `Authorization` only to the API's own origin (D285); JSON through a source-generated context, a needed field `required`. Tests: `GitHubProviderTests`, `GitHubClientTests` over `FakeGitHub.InProcess(root).CreateHandler()` |
| A token store | `src/VisualCommit.Hosting/Tokens/MacKeychainStore.cs`: every outcome a result with a `TokenStoreReasons` text, a constructor that touches nothing, a process runner the tests replace; its real test `tests/VisualCommit.Hosting.Tests/Tokens/MacKeychainTokenStoreRealTests.cs`, skipped unless `VISUALCOMMIT_KEYCHAIN_TESTS` names it (D270). A new provider needs no new store: `TokenKey.Of(AccountKey)` carries it |
| A fake host's routes | `tests/VisualCommit.Testing/Hosting/FakeGitHub.Router.cs`: each route a lower-case key that `HoldNext`, `FailNext` and the records name; state in `.Model.cs`, shapes in `.Json.cs`, git through `AuthRemoteServer`'s host mode in `.Git.cs`; stricter than the host (D266); GitHub's words taken from the live command's run.txt, not from memory (D342). Tests: `FakeGitHubTests`, `FakeGitHubGitTests` |
| A hosting scenario | `tests/VisualCommit.Testing/Scenarios.Phase7a.cs` ("hosting", design 2.6: the users and their tokens, constants such as `OctoToken`; the repositories `octo/hello` and `acme/tool`, forks and pull requests), pinned by `tests/VisualCommit.Git.Tests/Phase7aScenarioTests.cs`; a part's own state in `PartX.PrepareAsync`, which both harnesses call |
| A part of the walk-through | `tests/VisualCommit.VisualTests.Phase7a/Phase7aChecks.N.cs` with `Phase7aChecks.N.Helpers.cs`, `Phase7aExpectations.N.cs` (picture names, texts, rows) and `Phase7aExpectationTests.N.cs`; empty-tab starts through `StartEmptyAsync`, which waits for the start-up work (design 17.2); a picture of a signed-in repository only after D322's two Debug lines; `TokenTripwire` over the data folder at the end |
| A real-window part | `tests/VisualCommit.RealWindowTests/Phase7aRealWindowPass.N.cs`: one method and one launch per part in `Phase7aOrder` (D319), signed in by typing the fake's token into the real dialog (D320), each capture compared with its scripted twin by the twin's name; elements found inside their container (`FindIn`), never by a window-wide search |
| A live probe | `tests/VisualCommit.LiveHostingTests/LiveProbes.cs`: each probe answered by GitHub and by an in-process fake, noted "the fake: same" or the difference; requests through `LiveRequestLog.cs`, which refuses a token to any host but `api.github.com:443`; every note searched for the token before it reaches run.txt or the test output (D323); offline cases in `LiveSafetyTests.cs` |
| An app test with real git that a prompt could hang | `tests/VisualCommit.Hosting.Tests/CreatePullRequestOperationTests.cs`: a prompt handler that refuses and records, so a prompt fails git at once instead of waiting for a dialog nothing answers (design 19.8); D334's six app-starting classes live in `VisualCommit.Hosting.Tests` |

Rules worth keeping:

- **No token anywhere**: not in an argument, URL, configuration value, environment variable, log,
  exception, picture, automation value or file (design 1); a type that holds one hides it in
  `ToString()`; `TokenTripwire` searches every form.
- **A status read during a terminal's git can be torn**: git 2.56 names a stopped pick in
  `rebase-merge/done` 55 to 85 ms before it writes the message, and `MERGE_HEAD` before `MERGE_MSG`;
  a test of an outside operation waits for the final state, and the app reads a missing message
  again (design 21 item 4).
- **Compare layout places, not glyph ink**: macOS ends a text's ink 1.44 px before its layout edge
  (design 17.12).
- **Run the walk-through checks a fix touches** before merging it: two of the review's fixes changed
  expectations of checks their fixers had not run (design 19.7).

## Deviations from the plan

| What | Decision |
|---|---|
| Phase 7 split into 7a and 7b; the phase planned within the weekly allowance | D241 |
| How the phase was built: bounded agents with self-contained briefs, a design of about 150 to 200 KB (7a's grew to about 345 KB, accepted by the lead: status.md's Notes), checks written per part, test classes during the build and whole runs through the lock, D246's real-window rounds, inspectors' batches | D242 to D247 |
| Sign-in by a personal access token, no device flow; a fake of GitHub's API in the default run and CI and an opt-in live command; the list in the left panel's section with a menu (amends D92); avatars from GitHub only; the session where no keychain can be reached; github.com only; one account | D249 to D253, D258, D263, D307 |
| Accounts in the settings screen acting at once (an exception to D216); no request to GitHub while signed out; "From GitHub…" only while signed in | D256, D257, D261, D302 |
| The app's own credential helper for github.com in place of the user's helpers (an exception to Q5), bound only on git 2.31 and later (ruling 3's fallback withdrawn), with `GIT_CONFIG_PARAMETERS` removed in a bound scope, `erase` marking the account only after `GET /user` answers 401, clones bound after `insteadOf`, dashboard rows bound as tabs | D255, D273 to D278, D329 to D332 |
| From git 2.46 on, `http.<url>.proactiveAuth=basic` per binding, so no netrc answers before the app's helper | D340 |
| The push guard counts `refs/pull/<digits>/head` on a GitHub remote, recognised by its configured URL; the prompt group keyed by account | D279; rulings 8 and 9 (design 0.2) |
| The first package: W1, S8, R8 (the lag local only) and G23's split | D254, D262, D264, D280 to D283 |
| The harness: port 47395 (amends D97, as D150, D229 and D238 did); the fake as `AuthRemoteServer`'s host mode; the six app-starting classes in `VisualCommit.Hosting.Tests`; camelCase JSON; Windows' real store tried on CI | D265 to D267, D333, D334, D336 |
| The live command at the gate before the pictures and on the counted round, a fix to its project alone re-running only it (extends D246); CI's Windows job builds it, never runs it | D328, D335, D337 |
| "Clone from GitHub…" in the palette, keyless (amends D199's list); the status bar's offer as a general mechanism | D299, D317 |
| Avatars looked up from each author's own commit on the remote that holds it (keeps D311's addresses; amends design 6.3's lookups; D342 amends its statuses) | D339, D342 |
| D237's 60 s for every wait of the default run that waits for git or load | D341 |
| GitHub's answers as the live command recorded them govern the client, the lookups and the fake | D342 |
| The laptop's whole default run with at most four projects at once (the owner's choice at the gate) | D343 |

## Known issues

| Issue | How much it matters | Where |
|---|---|---|
| A bound tab whose account the session refuses through the API, or signs out of, after an operation bound its git calls and before git's credential request shows git's "Sign in to <host>" dialog, where ruling 9 has a bound repository never prompt | Low; a window of under about a second; the user can cancel. For the owner | plan risk 39, design 19.8, D273, D340 |
| A refresh that straddles a terminal merge's index shows "Conflicts resolved: commit to finish the merge." for about 0.3 s and one refresh, Continue enabled (git refuses to commit). Besides, a stop whose message tidies to nothing reads it at every status (one `git config --get core.commentChar` each) | Cosmetic, brief; removing it needs a status read before and after; for the owner to weigh | plan risk 40, design 21 item 4 |
| A user's netrc answers a bound call before the app's helper on git 2.31 to 2.45 (the containers' 2.43 among them); git-lfs asks HOME's netrc first on every version | Low; whether the Accounts section says so is open | plan risk 35, design 19.4 |
| A fork whose renamed parent is reached through an `upstream` remote under the old name is not mapped (badges, checkout and the create form's base can miss); a transfer to another owner is not in the fake | Low until a report | plan risk 36, design 19.2 |
| Test runners outside TempRepo's and a session's environment still see the machine's HOME, so a machine netrc entry for 127.0.0.1 could answer their loopback calls | Only on a machine with such an entry | plan risk 37 |
| GitHub answers an unknown SHA beside `author` with an empty 500; such an address is asked again at every run, and 20 of them at the head of the newest-first list keep the later authors waiting | Low; the live command's probes show a change of GitHub's | plan risk 38, design 20.3, 20.6 |
| macOS's token item, made by `/usr/bin/security`, can be read by any process of the user without a prompt | Accepted until phase 8 signs the app | plan risk 34, ruling 20 |
| No run uses a real keychain in the app's own process: a sign-in kept across a real restart, macOS's locked-keychain prompt and the Secret Service's prompts on a Linux desktop are tried by no machine; Linux providers other than gnome-keyring are untried | Until phase 8's checks by hand | plan risk 41, design 3.9, 0.5 |
| Local operations in a bound tab keep an inherited `GIT_CONFIG_PARAMETERS`; a hook or git-lfs filter they run asks the user's helpers in D82's order | By design (only network operations carry bindings) | design 19.4's git-2 |
| An exit of the process during a sign-out's keychain delete can leave the record and its entry, or `avatars/` without a record; a sign-out in a run whose keychain probe found none leaves the entry, which a Warning names for removal by hand; a losing overlapping avatar decode is disposed, but no test produces it | Rare | design 19.2's life-1 and tokens-1, 19.3's leak-2 |
| Not measured on GitHub: the empty repository's 409, a retired version's status, the `ServerError` fault's body, a repository the token cannot read answering a lookup 404, git with a select-only token on a public repository outside its selection (the fake's are from memory, marked so); the create form's "any other 422" arm is untested | Low | plan risk 42, design 20.1, 20.8, 15 point 1, 17.13 |
| After "Choose" closes the repository browser no control of the clone form has the focus; a loading diff takes the focus when its text arrives | Keyboard users; phase 8's keyboard pass | design 17.5, 17.11 |
| Cosmetic notes of the inspections: `g02a`'s tooltip wraps after a hyphen; the badge's 1-px strokes on fractional x draw as two half-intensity pixels; `#1`'s title starts 2 px left of the others; the create form's card grows with its note and error line | Cosmetic | status.md's Notes (WP-V's and WP-N's merges) |
| Undo is enabled after a create that pushed, from the push's entry; the create itself is not undone | By design | D314, the report's open point 54 |
| When the walk-through's classes run side by side, one may serve the fake on a free port instead of 47395 (`b06a` showed 59807 once) | Expected variance in a later comparison | status.md's Notes, D265 |
| Kept undo chains of 30 to 40 s in `VisualCommit.Git.Tests` and `VisualCommit.App.Tests` (`UndoStepBranchTests`, `UndoStepFileTests`, `UndoStepTests`, `UndoRecordingTests`) lie near the watchdog's 180 s under load; no split shortens them | D343 keeps them off the limit on the laptop; a stop again is the owner's call | plan risk 12, D237, D264 |
| Phase 1's in-memory layout of 100k commits took 2,620 ms against its 2 s once under the run's load (WP-B's merge run), and passed alone three times | A flaky timing under load | `GraphLayoutTests`; plan risk 12 |
| The run time: the default run <TO FILL AT THE GATE'S END: the counted round's time and tests> (the third run on `391f7f2`: 1 h 8 min 39 s at four projects, 5,950 tests), the real-window pass <TO FILL AT THE GATE'S END: time>; CI's Test steps during the phase up to 35 min 21 s on Windows (`8810136`) and 37 min 57 s on macOS (`fdb1986`), against D232's 55 and the 44 at which the lead raises it | Every full run, the gate and CI | plan risks 8, 12, 18; D232, D343 |
| Q1 within the counted pass: <TO FILL AT THE GATE'S END: ms> | A hot laptop fails a gate by itself | plan risk 10 |
| Phase 6's known issues still stand, but for the wheel test, the csproj's comment, the timing script, the push guard's `pr/*` refusal and the prompt group, which 7a fixed | As there | [phase 6's handoff](phase-6.md) |

## Things that cost time

- **The default run's load at the gate.** Three rested runs: the first (1 h 2 min 5 s) stopped
  `VisualCommit.Git.Tests` on the watchdog in a test of 14 s alone, so D237's rule was applied to
  every test of 25 s or more in xUnit's timing report (41; 11 split, 30 kept, design 21 item 1); the
  second, stopped by the watchdog 1,898 s in, in one undo chain of `VisualCommit.App.Tests`, made the
  owner choose D343; the third passed in 1 h 8 min 39 s. The stops move with the load from project
  to project. Time the long tests before a gate.
- **Nothing runs beside a heavy run**: the first baseline ran beside research agents and failed in
  four places; the unrested run at the review's merge stopped two projects (design 19.7).
- **CI found what the laptop did not**: the torn reads, after the laptop had passed the same code
  (design 21 item 4); the prompt channel's lost connections on macOS and Linux, since .NET backs
  every pipe instance of one name with one Unix socket (16.11); macOS's glyph ink (17.12);
  `KeyRouterTests`' drag and P13 (17.11; the laptop showed P13 once, in WP-V's build); part A's
  quiet mark before `git --version` (17.2); a decode that ended before its `await` (19.3's ci-1); a
  headless double-click that Avalonia counted as a triple click, since it counts presses within
  500 ms and 4×4 px whatever was pressed (17.9; risk 22).
- **Two elements with one automation id.** `Check_N_new_pull_request` searched the whole window for
  `PullRequestTitle` and found the left panel's row text before the form's box; D246's first run
  found it, and the check now searches inside the form (`723f2d3`).
- **A fake written from memory hid two defects of the app**: GitHub's version refusal carries its
  text in `errors` as one string, and an unknown SHA beside `author` is an empty 500; only the live
  command's first run showed them (D342). Run a provider's live command as soon as it exists.
- **The live command's variables in a session started before them**: VS Code's shells lack user
  variables set after VS Code started; the PowerShell line above gives them to the command alone.
- **Races in tests surfaced only by whole runs**: the create test whose prompt dialog nothing
  answered (design 19.8), the session test that counted the client's warning (19.7), part P's `End`
  in `finally` hiding a step's failure (19.7), and 106 waits shorter than 60 s (D341).
- **The design's size and the agents.** The design took 9 drafting agents (3.66M tokens), 20 for its
  review (3.20M) and 9 for its fixes (2.10M); wave 1 alone took 22 agents and 3 h 35 min. Two first
  builders stopped unfinished and a second finished from their commits (D242). An API safeguard's
  false positive ("reasoning_extraction") stopped a builder after its commit; prompts since ask for
  an explanation of a mechanism, not for reasoning.
- **Worktrees start from `master`**: the gate's split workflow had one on `5fe9148`, rebased onto the
  phase before anything was taken (design 21 item 1).
- **Git Credential Manager since git 2.56** made a `git push` wait on its sign-in window; the pushes
  used `git -c credential.helper= -c 'credential.helper=!gh auth git-credential' push ...`.

## For the next phase

Reuse:

- **The provider model**: `IHostingProvider`'s A1 to A6, `Recognise` and `SourceCanExist` per
  provider; `HostingFailure`'s kinds, worded as `HostingTexts.Describe` does; `AccountKey` and
  `TokenKey` carry provider and host, so the stores, the vault and `accounts.json` (which keeps
  other hosts' records, D292) need no new store.
- **The credential path**: bindings per remote URL, the helper's `get` and `erase`, the refused set
  (D273), D340's pairs and the 2.31 floor (D277); each provider's user name and host rules are 7b's
  design's. The UI (the list above) is worded for GitHub today.
- **The harness**: `AuthRemoteServer`'s host mode for a fake's git, a fake per provider with its own
  route keys, the scenario's pattern, `FakeTokenStore`, `GitCallBudget`, `TokenTripwire`, D322's
  Debug lines, and the live command's structure (D323 to D327).
- **Naming**, as design 0.3 named 7a's: `7b` wherever the docs write it, the project
  `tests/VisualCommit.VisualTests.Phase7b`, `Phase7bExpectations.Phase = "7b"`, a `7b` case in
  `scripts/time-scripted-check.sh` (its default set to 7b in the commit that creates the project),
  the real-window link `..\VisualCommit.VisualTests.Phase7b*\Phase7bExpectations*.cs`. Ports 47391 to
  47395 are taken and 47396 is reserved for a second 7a project: 7b's take new constants, pinned in
  `AuthRemoteServerTests`, and a decision that amends D97 as D265 did.

Do first:

1. The start-of-session steps; read plan.md's phase 7b and its risks 4 and 5, 34 to 42, 8, 10, 12,
   18, 22 and 26; the Known issues above and phase 6's; CI's run on `master` after the merge.
2. Before the design, the owner's answers, each recorded as a decision and carried into plan.md and
   requirements.md (status.md's "Waiting on the owner" says Azure DevOps' and GitLab's accounts and
   sign-in are asked then): sign-in and test accounts or tokens for Azure DevOps and GitLab (risks 4
   and 5), and whether each gets a live command; and the questions 7a's decisions leave open for 7b:
   avatars, which D252 and O4 give to GitHub only while plan.md's 7b lists "author avatars, as 7a
   built them for GitHub"; how a self-hosted GitLab's host is given and recognised, where 7a knows
   one fixed host per provider (D258, `HostingOptions`); whether D263's one account is one per
   provider or one in all; D255's exception to Q5 and D257's "no request signed out" for the new
   hosts.
3. Check that no worktree, `wip/*` branch or fsmonitor daemon is left and that C: and D: have room.
   A baseline whole run only as D245 allows (the machine changed, or code not pushed), through
   `bash scripts/heavy-run.sh`.
4. The design: 7a's is the model (sections 0 and 1, then a brief per package); keep within D243's
   size where the scope allows, and split 7b per provider if its design outgrows a week of the
   allowance (plan.md's phase 7b, D241). Estimate what it adds to the default run
   (<TO FILL AT THE GATE'S END: the counted round's time and tests>; the third run on `391f7f2`: 1 h 8
   min 39 s at four projects, 5,950 tests) and to the real-window pass
   (<TO FILL AT THE GATE'S END: its time>) before writing the checks.

Risks to watch:

- The run time (plan risks 8, 12, 18): a twelfth test project lengthens the four-at-a-time run on
  the laptop; CI's Windows and macOS Test steps reached 35 and 38 minutes during 7a, against the 44
  at which the lead raises D232's limit.
- The kept undo chains near the watchdog (Known issues) and Q1 on a warm laptop (risk 10).
- Git's versions (risk 26): D340 holds from 2.46 only, and the containers' 2.43 runs the older
  branches.
- The prompt channel on macOS and Linux (design 16.11), which every provider's credentials go
  through, and the real keychains, which only CI's steps touch (D270).
- Risks 34 to 42; the live token expires on 2026-11-29.

## Waiting on the owner

- **Acceptance of phase 7a**: <TO FILL AT THE GATE'S END: acceptance>.
- **A sign-in dialog in a bound tab** (plan risk 39, design 19.8): should the broker refuse such a
  prompt without a dialog, from what the session knows (amending D273 and D340)?
- **The banner's brief torn state** (plan risk 40, design 21 item 4): leave it, or have the refresh read the
  in-progress files before and after `git status`?
- **The kept undo chains**: a limit of their own if one stops a rested run again (D264).
- **For phase 7b**: Azure DevOps' and GitLab's sign-in and test accounts or tokens (risks 4 and 5),
  and the questions under "Do first", step 2. The live command's token for GitHub expires on
  2026-11-29; a later live run needs a new one.
- **Decisions made during 7a without the owner**, which the owner may want to look at: the design's
  D265 to D338 beyond the eight questions the owner answered on 2026-10-29 (status.md lists them);
  D339 to D341 from the review and the runs after it, and D342 from the live run; and the lead's
  rulings in the design's sections 16 to 21 and the report.
- **When you have a Mac at hand**: the eight-step checklist at the end of the
  [phase 7a report](../test-reports/phase-7a.md), and those of phases 0 to 6, at the end of the
  project.
- **Still open from earlier** (status.md's "Waiting on the owner" gives each): phase 6's three
  questions asked at its acceptance; D240's connectors switch, which did not keep the claude.ai
  connectors out; the review before phase 7's questions 1, 10 and 12 and its unnamed proposals; Git
  Credential Manager's sign-in since git 2.56; CI's duration, when the project is over (D134).
