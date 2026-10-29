# Phase 7a visual test report — Hosting foundation and GitHub

The Check, Steps and Expected columns are written before the UI they test is built: since D244
each part's checks are written and committed just before its work package is built. The result
columns are filled in when the gate runs.

Phase 7a's checks come in five parts, each written just before its package (`docs/phase-7a-design.md`
0.4, 0.5): **A** accounts and sign-in (WP-A), **B** browse and clone (WP-B), **P** pull requests
(WP-P), **G** the graph's badge and avatars (WP-V) and **N** a new pull request (WP-N). This first
version holds part A, written on 2026-10-29 before WP-A started, from `plan.md`'s phase 7a, the design's
sections 0, 1, 2.5, 2.6, 7, 8, 13.4 and 16, decisions D249, D253, D256, D263, D265, D277, D287, D288,
D291, D294 to D298 and D330, and the harness and scenario as wave 1 built them
(`tests/VisualCommit.Testing`, `tests/VisualCommit.VisualTests.Phase7a` at `92ce1ba`). No part A UI
existed: every text is design 8.2's or an earlier package's, every position design 8.3's or 8.4's, and
the checks take positions from the controls' bounds (D122's rule). "What the checks share" holds what
every part relies on, then part A's conventions, test methods and texts; "Checks" is the table of A1
to A11 (design 8.7's numbering); "Changes to expected results" lists the earlier pictures part A changes
on purpose; "Open points for the lead" lists where the design is silent or its sections disagree, with
the reading this report takes. Tokens are written here only by their constant names, never by value.

## Run

Filled in when the gate runs, for each of its two passes: date, app commit, the machine (its
operating system and version), the display scaling of the real-window pass, the screenshot
folders, and how many pictures were inspected. The gate's two passes are run on one commit by a
session on the Windows machine, except that a commit changing only
`tests/VisualCommit.RealWindowTests` re-runs only the real-window pass, and the default run of the
last commit it ran on stands for its own (D246; `docs/plan.md`'s Visual test gate, step 6). Each
round lists the commit of each pass. The two runs of the new phase's real-window checks on their
own before the first counted round are listed too, marked "before the gate".

If a cloud session ran the scripted pass earlier, its run is listed here too, marked "cloud
session, before the gate", and its results stand in the Scripted column until the Windows
session replaces them with its own. It does not count as the gate's scripted pass.

## What the checks share

### Conventions of every part

What every part of phase 7a relies on; each part's conventions add to these.

- **Parts and the project** (design 0.3, 0.5, 2.8; D230). One project,
  `tests/VisualCommit.VisualTests.Phase7a`, about 27 test methods for the whole phase (under D230's
  40), one class per part: `Phase7aChecksA`, `Phase7aChecksB`, `Phase7aChecksP`, `Phase7aChecksG`,
  `Phase7aChecksN` (`public sealed partial class … : Phase7aChecks`, in `Phase7aChecks.<part>[.<topic>].cs`),
  with what they compare against in `Phase7aExpectations.<part>.cs` (a nested class `PartA` … `PartN`
  of texts, picture names, positions and tolerances, written from this report and not from the App's
  texts; the real-window pass links them) and their tests in `Phase7aExpectationTests.<part>.cs`.
  Shared helpers live in `Phase7aChecks.Support.cs` only. Check names are the part's letter and its
  number in its section of the design (A1 to A11, design 8.7); test methods are `Check_<id>…_<what>`.
- **One test method per app start** (phase 3's rule, `FreshApplication`), each on a fresh copy of the
  scenario and a fresh data folder; a restart on one data folder is a plain `[Fact]` with one
  `FreshApplication.RunAsync` half per start (D32), the one `FakeTokenStore` handed to every start
  (the scripted restart, design 2.5). The real window never restarts: its keychain is memory (0.5).
- **Defaults.** 1100×700 and dark unless a check names 1920×1080 or light (0.5). Every part uses the
  one scenario "hosting" (2.6): `Phase7aChecks.ServeAsync()` gives a fresh copy served by the fake of
  GitHub; `Phase7aChecks.OpenAsync(data, served, …)` opens one tab on `hello` (or `tool`), signs in with
  `OctoToken` through the session unless `signIn: false`, waits until the watchers are quiet (the
  sign-in's URL-rewrites read included, 5.3) and rests the pointer; `ShellDriver.Start(data, gitHub:
  served.GitHub, tokens: …)` starts one empty tab. Every session has what phase 6's 17.2 gives it: the
  frozen record clock, the fixed backup clock, auto-fetch off, the session's own git configuration in
  the data folder, the fixed tool catalogues, a fake system theme and dates in UTC.
- **GitHub in the harness** (rulings 23, 24; design 1.8, 1.9, 2.5; D265). Every test process has
  `VISUALCOMMIT_GITHUB_URL=none` and `VISUALCOMMIT_KEYCHAIN=memory` and none of the token variables, so
  a start is off unless the check gives `gitHub:` (the fake on a port) or `hosting:`. The session's
  every HTTP client goes through `LoopbackOnlyHandler`: `RefusedRequests` is empty at every check's end.
  The fake listens on `127.0.0.1:47395` (`AuthRemoteServer.Phase7aVisualChecksPort`, D265), which only
  this project and its real-window twin bind; when the port is taken it falls back to a free one
  (`PortWasTaken`, noted in the gate's Run) and every expected text that holds the port goes through
  `Phase7aExpectations.WithPort`. `GitHub.Requests` records each API, avatar and page request
  (method, path, route key such as `user` or `repo:octo/hello`, `TokenPresent`, `Login`, status, never a
  token) after its answer; `GitHub.GitRequests` each git request with its `GivenUserName`. A step's
  requests are read after a mark (`Requests.Count` before the step). Holds (`HoldNext`) and faults
  (`FailNext`, API routes only, design 16.2) are released and disposed in a `finally`; `GitHub.Errors`
  is empty at every check's end, and no API request was answered 400 or 415.
- **The keychain** is a `FakeTokenStore` (2.5): new per start unless the check hands one over;
  `Available`, `FailNextSave(reason)`, `HoldNext()`, `Holds(key)`, `Remove(key)`; `Calls` names each call
  and its key, never a token: `probe`, `save <key>`, `read <key>`, `delete <key>`, the key as
  `TokenKey`'s text, `github:127.0.0.1:47395:octo`. The app's vault probes the store at most once per
  start, when it first needs it (design 3.7), and reads it at most once per key and start (ruling 6).
  A failure's reason is one of `TokenStoreReasons`' seven texts (D268); the checks use `Locked`, "the
  keychain stayed locked".
- **Tokens** (design 1, 13.2). The scenario's tokens are named here by their constants
  (`Phase7aExpectations.OctoToken`, `OctoClassicToken`, `AdaToken`, equal to `ServedHosting`'s) and a
  part's own by `Part<X>`'s. No token is ever passed to an `Assert` method or written in a failure's
  message: a check tests with `string.Contains` and names the place. Every method ends, after the app
  has closed (design 16.3: the tripwire fails on a file the app still holds), with
  `TokenTripwire.AssertAbsent(token, [data folder, the copy's hello/ and tool/], calls)` for each token
  the method used or typed, with the session's call log (every start's), so no token is in a file,
  `.git` included, in any of the forms 2.5 lists, or in any git call's command, output or error.
- **Pictures of an opened GitHub repository while signed in** are taken only after D322's two Debug
  lines: WP-S's `{repo}: listed {n} open pull requests of {owner/name}` (`AppLogFile.PullRequestsListed`)
  and, from WP-V's merge, WP-V's `{repo}: showing GitHub avatars for {n} authors` (`AvatarsShown`).
- **Git calls** are counted only through `GitCallBudget` (ruling 28); `CallsSince`, `AssertCalled` and
  `AssertCalledInOrder` take a `long` mark from `ShellDriver.QuietCallMarkAsync()` and prove presence and
  order only (2.8). Signed out, no part adds a git call to an earlier count: the URL-rewrites read
  (`git config --null --get-regexp ^url\.`) runs only while signed in (D281).
- **Dialogs, menus, the toolbar and the status bar** are phase 6's (its report's "Conventions of every
  part"): dialogs stack (D104), the error dialog is asserted by `ErrorTitle`, `ErrorSummary`,
  `ErrorRepository` (`In <repo>`: an error dialog names its repository always, with one tab too;
  `ErrorDialogViewModel.ForFailure` takes it, `RepositoryOperations.Run.cs:770`), `ErrorOutput`,
  `ErrorCommand` and then its buttons left to right (7a's own assertion, added by the first part that
  needs one, design 16.3), menus header first. Git's own texts are quoted as Git 2.56.0.windows.4
  prints them (D248) and asserted only by their line marked "key"; `ErrorOutput` is
  `GitOutputCleaner.Clean` of the failed call's standard error on every git. A fetch of all remotes
  (the toolbar's Fetch, `GitRepository.FetchAsync(null)`) follows each failed remote's output with the
  app's line `error: Could not fetch <remote>` (D109; `GitRepository.Remotes.cs:66-67`, as phase 6's W6
  asserts it), so its `ErrorOutput` ends with that line, and its `ErrorCommand` is the first failed
  call's, `git fetch --progress <remote>` for the last or only remote (no `--no-auto-maintenance`). The
  account's credentials reach git through the environment (`GIT_CONFIG_COUNT` pairs, design 4.2), never
  in the command, so `ErrorCommand` reads the same signed in.
- **Pictures** are saved by `Shot(app, name)` under `artifacts/visual/phase-7a/scripted/`, named by design
  0.3's rule `^[abpgn]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`: the part's letter, the check's
  number in two digits (`a01`), a step letter running on through the check's methods in the order of
  its part's methods table, then words (`a02b-token-masked`). Every name is unique in the report and
  `Shot` refuses one saved twice in a run. Before a capture with no menu open and no tooltip asked for,
  `Shot` rests the pointer at the neutral point (760, 687) at every size (`ShellDriver.MoveToNeutral`,
  `WindowLayout.NeutralX` and `NeutralY`; `Phase7aChecks.Support.cs`, `CaptureForPicture`): at 1100×700
  the status bar's empty middle, or the backdrop below a card (the settings card ends at y 660, the
  sign-in dialog at x 750); at 1920×1080 it lies inside the settings card (x 530 to 1390, y 200 to 880),
  which for part A is the Accounts section's empty area below its rows, in the label column (x 746 to
  878), where nothing has a tooltip or a pointer-over look. A later part whose 1920×1080 picture has a
  control under that point names its own rest point, as phase 6's SD2 did with (760, 1000); 7a's
  harness has none yet. Every picture passes `LayoutAudit.FindClippedText`
  (`AssertNoClippedText`). A picture whose content varies by nature says so in its check's Notes and is
  compared by inspection: a focused text box's caret, git's version in the status bar's `Git 2.56.0` box
  (the machine's git, D248; the same in both passes of one machine, other on CI's jobs), a temporary
  folder's name, git's output.
- **R steps** (design 0.5). A step marked R is repeated in the real window; its picture's name is a
  constant in `Phase7aExpectations.<part>.cs` with the texts, rows and menus the step asserts; its state
  is built only by code both projects compile (`VisualCommit.Testing`, the linked expectations, a part's
  `Part<X>.PrepareAsync`), never by `FakeFileLauncher`, `FakeClipboard`, `FakeTokenStore`'s hooks or a
  held request, and it is taken signed in with `OctoToken`. Part A's R pictures are on one empty tab.
- **The real window** (design 13; D319, D320, D33). One method and one launch per part, at 1100×700,
  dark, in the order A, B, N, G, P; each launch signs in by typing `OctoToken` into the real "Add GitHub
  account" dialog; the app gets `VISUALCOMMIT_GITHUB_URL=http://127.0.0.1:<port>/` and
  `VISUALCOMMIT_KEYCHAIN=memory`, so its keychain is the memory store, available. Each R picture is
  captured under its scripted twin's name in `artifacts/visual/phase-7a/real-window/` and compared with
  it (at most 3 percent of the pixels differ, `run.txt`); every launch ends with `AssertCleanEnd` (exit
  0, the tripwire over the data folder, the fake's `Errors` empty, no 400 or 415, no avatar request with
  a token). It never clicks "Copy …", "Open file", "Open in editor" or "Show in folder", never changes a
  desktop setting (D224), and runs on the development machine: Windows 11 at 200 percent, whose work area
  of 1500×952 logical pixels holds no 1920×1080 window, so every 1920×1080 or light step is scripted only.
  What only the real window proves for part A: the real process's start with the fake's URL and the
  memory keychain, its own handler reaching the fake on loopback, the token typed on the real keyboard
  and masked to UI Automation (`SecretTextBox`), the native window and its scaling. The real keychains
  are WP-K's classes on CI (D259, D333), never the pass.
- **Earlier pictures** (design 0.7; plan risk 32) are compared by region against references made on
  `master` with the same git, never by `phase-6-pictures.sha256` alone: every picture with the status
  bar's git box differs from it whatever 7a does (D248).

### Scenario facts ("hosting", design 2.6; as `Scenarios.Phase7a.cs` builds it)

What part A uses; later parts add theirs.

- **The fake** serves the copy's `github/` at `http://127.0.0.1:47395/` (`GitHubEndpoints.Local(47395)`:
  web base `http://127.0.0.1:47395/`, API `http://127.0.0.1:47395/api/v3/`, `Host` `127.0.0.1:47395`, which
  the app matches with the scheme exactly, ruling 23). The account key of octo is `AccountKey.Id`
  `github:127.0.0.1:47395:octo`; its token's key `TokenKey("github", "127.0.0.1:47395", "octo")`.
- **Users**: `octo` (id 1001, name "Octo Cat"), `acme` (1002, an organisation, "Acme"), `ada` (1003, "Ada
  Lovelace"); each user's `avatar_url` is `http://127.0.0.1:47395/avatars/u/<id>?v=4`.
- **Tokens**: `OctoToken` (octo's fine-grained token: every R picture and most sign-ins),
  `OctoClassicToken` (octo's classic token with `repo` and `workflow`: A9's second sign-in), `AdaToken`
  (ada's; only in the tripwire). `FakeGitHub.Revoke(token)` acts on the next request, git's included.
- **What the fake answers** (2.4): `GET api/v3/user` (route `user`) with a known token 200 and the user
  with `login`, `id`, `name`, `avatar_url`; with an unknown or revoked token 401 "Bad credentials"; any
  API request with a revoked token 401. Git over HTTP (2.2): a request without credentials to the private
  `octo/hello` 401 with `Basic realm="GitHub"`; Basic with any user name and a known token passes; a
  revoked token 401. Nothing answers the token page of design 8.4: the fake launcher records it.
- **Repositories part A opens**: `hello/`, a clone of the private `octo/hello`: `origin`
  `http://127.0.0.1:47395/octo/hello.git` (no other remote, no `pushurl`), `main` tracking `origin/main`,
  `feature/login` tracking `origin/feature/login`, local-only `local-work`; on `main`, clean. octo/hello's
  open pull requests are #5, #3, #2 (a draft) and #1, so WP-S logs `hello: listed 4 open pull requests of
  octo/hello`. `tool/` is never opened by part A; the tripwire searches it.
- **Bindings** (design 4.4, 7.8; D278): signed in with the switch on and the URL rules read, `hello`'s
  `RepositoryViewModel.Hosting.Bindings` is exactly one `CredentialBinding` with `Url`
  `http://127.0.0.1:47395/octo/hello.git` and the account `github:127.0.0.1:47395:octo` (fetch and push
  URL the same; no `insteadOf` rule in the session's configuration).
- **`accounts.json`** (7.2, D336) after a sign-in as octo: `version` 1 and one record with exactly these
  fields: `provider` `github`, `host` `127.0.0.1:47395`, `login` `octo`, `id` 1001, `name` `Octo Cat`,
  `avatarUrl` `http://127.0.0.1:47395/avatars/u/1001?v=4`, `useForGit` (true after a first sign-in, D255),
  `tokenState` (`keychain`, `session` or `refused`). It is asserted by its parsed properties; its text
  holds no token (the tripwire).

### Part A: accounts and sign-in

Part A covers the settings screen's Accounts section (design 8.3; D256, D294), the "Add GitHub account"
dialog (8.4; D249, D295, D297, D298), sign-in with the keychain and for the session only (D253, ruling 19),
sign-out with its confirmation (D291, D296), "needs sign-in" after the API's and git's refusals (D330)
and at a start whose token is gone, a restart that keeps the sign-in, and the "Use for git over HTTPS"
switch (D216, D255): checks A1 to A11 of design 8.7. It shows the vault through `FakeTokenStore` (WP-K
has no checks of its own) and WP-C's refused-token dialog (4.7). Its R pictures are A1's, A2's masked
token and A5's signed-in row, on one empty tab (13.4 A).

#### Conventions of part A

- **Code** (8.7): `Phase7aChecks.A.cs` (one method per app start, below), `Phase7aChecks.A.Helpers.cs`;
  `Phase7aExpectations.A.cs` (nested `PartA`: the texts below, picture names, R pictures as constants,
  positions, tolerances, and part A's own tokens: `PartA.UnknownToken`, a token of the fine-grained form
  the fake never issued, and `PartA.TokenWithSpace`, `OctoToken`'s text with one space inside);
  `Phase7aExpectationTests.A.cs` (`AccountTexts`, `HostingTexts` and the link's URL equal `PartA`'s; the
  picture names unique and matching the rule). The driver's part A helpers are in
  `tests/VisualCommit.Testing/Headless/ShellDriver.Phase7a.Accounts.cs`: `OpenAccountsAsync()` (Ctrl+, ,
  wait until the screen shows and its reads have ended, a click on `SettingsNavAccounts`: the real
  window's `OpenAccountsSection`), `AccountsSnapshot()` (the screen's `SettingsSnapshot()`: each visible
  control of the card with an automation id, its text, check and enabled state, in reading order),
  `SignInDialog` (the open dialog's view model, or null), `OpenSignInDialogAsync()` (a click on
  `AccountsSignInButton`, then wait for `SignInDialog` with the keyboard focus in `SignInToken`),
  `TypeToken(string)` (`TypeInto("SignInToken", …)`: focus, caret at the end, typed, so what the box
  held stays in front; a step that replaces the box's selected text types with `app.Type(…)` into the
  focused box instead, and says so), `ConfirmSignInAsync()`
  (a click on the dialog's own `ConfirmButton`, found inside `SignInDialog`, then wait until the check has
  ended: the dialog closed, or showing `SignInError` or `SignInSaveFailed`), `SignOutAsync(bool confirm)`
  ("Sign out…", the confirmation, then its "Sign out" or "Cancel", and wait for the section's state) and
  `WaitForAccountAsync(Func<AccountStatus?, bool>)` (on `Session.Services.Hosting.Account`).
- **Names in the checks.** "The session" is `Session.Services.Hosting` (the `IHostingSession`, design
  1): "the session's `Account`", "`IsOff`", "`BindsGit`". `ShellDriver.Hosting` is another thing, the
  `HostingOptions` the start was given, and no check reads it. "`hello`'s hosting" is the
  `IRepositoryHosting` of `hello`'s view model (`RepositoryViewModel.Hosting`): its `Bindings` and
  `State.PullRequests`. "Requests" are `GitHub.Requests` since the step's mark, "git requests"
  `GitHub.GitRequests`, "calls" the store's `Calls`; git's own calls are always written "git calls"
  and read only through `QuietCallMarkAsync` and `QuietCallsSinceAsync` ("Git calls" above).
- **No token on screen** (8.9's "done when"). Before every `Shot` of part A, whether or not its
  expected result repeats it (A2's and A5's do), no visible text, automation value or automation name in the
  window holds any of the five tokens (`OctoToken`, `OctoClassicToken`, `AdaToken`,
  `PartA.UnknownToken`, `PartA.TokenWithSpace`): tested with `string.Contains`, a failure naming the
  control and the token's constant, never the token.
- **`accounts.json` is written off the UI thread** under the session's gate (7.2). A sign-in writes it
  before it sets the account (7.5 step 7) and a sign-out before it clears it, but a refusal sets the
  account first (7.6) and the switch's Save writes it after the screen has closed (7.5,
  `SetUseForGit`). So after a refusal (A9) or the switch (A11) a check waits until the parsed file
  shows the expected fields (polled, at the default wait) before it asserts the rest of it, as it waits
  for the bindings.
- **Automation ids** are the element names of design 8.3 and 8.4: on the screen `SettingsNavAccounts`,
  `SettingsAccounts`, `AccountsIntro`, `AccountsOff`, `AccountsSignedOut`, `AccountLogin`,
  `AccountTokenPlace`, `AccountNeedsSignIn`, `AccountsSignOutNote`, `AccountsSignInButton`,
  `AccountsSignOutButton`, `AccountsBusy`, `AccountUseForGit`, `AccountUseForGitNote`; in the dialog
  `SignInDialog` (named by its title), `DialogTitle`, `SignInHelp`, `SignInTokenLink`, `SignInToken` (named
  "Token"), `SignInNoKeychain`, `SignInChecking`, `SignInError`, `SignInSaveFailed`, `CancelButton`,
  `ConfirmButton` (open point 6).
- **The screen at 1100×700** (8.3, part S of phase 6): the card exactly at (120, 40), 860×620; `SettingsNav`
  at x 140 to 316 with five buttons 176×30, 2 apart, from y about 95: General, Git, Tools, Keyboard
  shortcuts, Accounts, the last at (140, 223) to (316, 253), the shown section's button in the `selected`
  look, no error dot on Accounts; the section from x 336; in Accounts, labels in the 132-wide column x 336
  to 468 (`settingsLabel`, `VcTextSecondaryBrush`), the row's controls from x 480 to 944; `Cancel` and
  `Save` as part S has them; no scroll bar. At 1920×1080 the card is at (530, 200), 860×680 (D216's
  largest, as phase 6's S14 step 14), the button at (550, 383), the section from x 746, the controls from
  x 890, and `Cancel` and `Save` on y 827 to 859. Positions are asserted ±2 where given here,
  the card exactly.
- **The section as it shows** (8.3), top to bottom inside `SettingsAccounts` (spacing 18): `AccountsIntro`
  (`secondary`, wrapping); the row labelled "GitHub" whose column holds `AccountsOff`, or
  `AccountsSignedOut`, or `AccountLogin` (SemiBold, the login in its own case) followed by `AccountTokenPlace`
  (`settingsNote`) or `AccountNeedsSignIn` (`dialogWarning`); then `AccountsSignOutNote` (`dialogWarning`)
  when set; then, 10 below, a row of `AccountsSignInButton` and `AccountsSignOutButton` (`VcActionButton`,
  8 apart) and `AccountsBusy` (`secondary`). "Sign in…" shows only signed out or needing sign-in, "Sign
  out…" only with an account (D294). With an account, the row labelled "Git" holds `AccountUseForGit`
  (`VcCheckBox`) and `AccountUseForGitNote` (`settingsNote`). Theme brushes only: dark
  `VcTextSecondaryBrush` `#9BA3B0`, `VcWarningBrush` `#E0A23B`, `VcDangerBrush` `#E5606B`, `VcAccentBrush`
  `#7C8CFF`, the card `VcPanelBackgroundBrush` `#1A1D24`; light `#5C6572`, `#895A17`, `#B7323D`, `#4353D8`,
  `#F5F6F8`.
- **The dialog** (8.4): a `dialogCard` 400 wide (content 358), x 350 to 750 at 1100×700 and 760 to 1160 at
  1920×1080, centred vertically, stacked over the screen, which stays open under the dialog's own
  backdrop (D104); no "In {repo}" line. Top to bottom: `DialogTitle` (`dialogTitle`); `SignInHelp`
  (`dialogText`); the link (accent foreground, no padding, a hand cursor, underlined on pointer-over and
  while focused, 6 below, reached by Tab); the label "Token" (`promptLabel`); the box; `SignInNoKeychain`
  (`dialogWarning`) when no keychain; one status line, `SignInChecking` (`secondary`) or `SignInError`
  (`dialogError`) or `SignInSaveFailed` (`dialogWarning`); `CancelButton` (`VcActionButton`) and
  `ConfirmButton` (`VcPrimaryButton`) right-aligned. The box is a `SecretTextBox` masked with
  `PromptDialogViewModel.MaskCharacter` (`•`); its automation value is empty while masked (D103, D130).
  "Sign in" is enabled while the trimmed token is not empty and no check runs. Enter in the box signs in;
  Esc closes the dialog only, never the screen below it.
- **Sign-in and sign-out act at once** (D256): the screen's Cancel and Esc never undo them; only the switch
  waits for Save (D216). After a sign-in from the section the focus goes to "Sign out…", after a sign-out
  to "Sign in…" (8.3).
- **Signing in through the session** (`ShellDriver.SignInAsync(token)`, no dialog) builds a state a step
  only starts from (A8, A9, A10 part B, A11); every state a check is about is reached through the screen
  and the dialog, A5's signed-in row at 1920×1080 included (method 2 signs in through the dialog there).
- **Every method ends**, after closing the app, with the tripwire for `OctoToken`, `OctoClassicToken`,
  `AdaToken`, `PartA.UnknownToken` and `PartA.TokenWithSpace`, the fake's `Errors` and the harness's
  `RefusedRequests` empty, no API request answered 400 or 415, and `Launcher.Requests` holding only the
  launches its steps name (A2's one).
- **Git's version.** Every machine of the gate and of CI runs Git 2.43 or newer, so the switch's note is
  `AccountUseForGitNote` and the sign-out question has its last sentence in every check;
  `AccountUseForGitOldGit` and the shorter question are 8.8's view-model tests' (D277).

#### Test methods and app starts (part A)

Eight methods, eleven app starts; each starts with `ServeAsync()` and a fresh data folder. Their git work
is light (an empty tab's start and each screen opening on it make one or two git processes; `hello`'s
opening about 11, a screen opening on its tab 7), so every method stays well under a minute alone.

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_A1_A2_A5_signed_out_dialog_and_signed_in` | A1, A2, A5 part A | one empty tab (`ShellDriver.Start(data, gitHub: served.GitHub)`), a new store | 1100×700, dark | `a01a`, `a02a`, `a02b`, `a05a` |
| 2 | `Check_A5_signed_in_at_1920x1080` | A5 part B | one empty tab, a new store; signed in through the dialog | 1920×1080, dark, then light by the toolbar's Theme switch | `a05b`, `a05c` |
| 3 | `Check_A3_A4_A7_checking_refused_and_save_failed` | A3, A4, A7 | one empty tab, a new store | 1100×700, dark | `a03a`, `a04a`, `a04b`, `a07a`, `a07b` |
| 4 | `Check_A6_A10_session_only_and_its_restart` (a `[Fact]`, two starts on one data folder) | A6, A10 part A | one empty tab per start; one store with `Available = false` for both | 1100×700, dark | `a06a`, `a06b`, `a10a` |
| 5 | `Check_A8_sign_out` | A8 | one empty tab; signed in through the session after the start | 1100×700, dark | `a08a`, `a08b`, `a08c`, `a08d` |
| 6 | `Check_A9_needs_sign_in_after_a_refused_token` | A9 | one empty tab, signed in through the session; `hello` opened in it later | 1100×700, dark | `a09a`, `a09b`, `a09c` |
| 7 | `Check_A10_restart_keeps_the_sign_in_and_finds_the_token_gone` (a `[Fact]`, three starts on one data folder) | A10 part B | one empty tab per start; one store and one fake for all | 1100×700, dark | `a10b`, `a10c` |
| 8 | `Check_A11_use_for_git_switch` | A11 | two tabs (`StartWithTabs(data, [hello, null], activeTab: 0, gitHub: …)`); signed in through the session after `hello` has loaded | 1100×700, dark | `a11a` |

#### Texts these checks rely on (part A)

Design 8.2's texts, word for word (`OperationTexts.AccountTexts`, and `HostingTexts` where named). A
`{reason}` or `{message}` loses a full stop at its end before the sentence's own.

- **Section list and section**: the nav button and section `Accounts` (`HostingTexts.AccountsSection`); the
  row label `GitHub` (`HostingTexts.GitHub`); the second row's label `Git`.
- `AccountsIntro`: `With a GitHub account, VisualCommit lists and creates pull requests, shows authors' pictures and clones your repositories.`
- `AccountsOff`: `GitHub is turned off for this session.` `AccountsSignedOut`: `Not signed in.`
- Buttons: `Sign in…` (`AccountsSignInButton`), `Sign out…` (`AccountsSignOutButton`, which opens the
  confirmation), `Signing out…` (`AccountsBusy`).
- `AccountTokenPlace`: keychain `The token is kept in this computer's keychain.`; `NoKeychain` (D253)
  `No keychain was found on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again at the next start.`;
  `SaveFailed` `The token could not be saved in this computer's keychain: {reason}. VisualCommit keeps it until it closes and asks for it again at the next start.`
- `AccountNeedsSignIn`: `Refused` `GitHub refused the token. Sign in again with a new token.`;
  `TokenMissing` `The token is no longer in this computer's keychain. Sign in again.`; `ReadFailed`
  `The token could not be read from this computer's keychain: {reason}. Sign in again.`; `SessionEnded`
  `The token was kept for the last session only. Sign in again.`
- `AccountsSignOutNote`: `Signed out, but the token could not be removed from this computer's keychain: {reason}.`
- `AccountUseForGit`: `Use for git over HTTPS`. `AccountUseForGitNote` (git 2.31 or newer, or unknown):
  `Clone, fetch, pull and push to GitHub over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.`
  `AccountUseForGitOldGit` (no check shows it): `This needs Git 2.31 or newer. With Git {version}, clone, fetch, pull and push still use your git credential helpers.`
- **The confirmation** (D291, D296): title `Sign out of GitHub?`; question `Sign out of GitHub as {login}? VisualCommit removes its token and the authors' pictures it saved from this computer, and no longer lists pull requests. Git over HTTPS asks your own credential helpers again.`
  (the last sentence left out on git older than 2.31); no detail; buttons `Cancel` and `Sign out`, the
  latter the primary button, not the danger one.
- **The dialog**: title `Add GitHub account` (`HostingTexts.AddAccountTitle`); `SignInHelp`
  `Paste a personal access token from GitHub. A fine-grained token needs read and write access to Contents and Pull requests of your repositories; a classic token needs the repo scope. To push changes to workflow files, it also needs Workflows (fine-grained) or workflow (classic).`;
  the link `Create a token on GitHub`, tooltip `Opens GitHub's page for a new token in your browser.`, its
  address the fake's base and `settings/personal-access-tokens/new?name=VisualCommit&contents=write&pull_requests=write`
  (D295); the label `Token`; `SignInNoKeychain` the D253 text above; `SignInChecking` `Checking the token…`;
  `SignInError` for `BadCredentials` `GitHub refused the token: {message}.` (`GitHub refused the token: Bad credentials.`),
  for `Network` the message as it is (`Could not reach GitHub.`, `GitHub did not answer within 30 seconds.`,
  D284), for any other kind `GitHub answered: {message}.`, and locally (D298)
  `The token can contain only letters, digits and punctuation, without spaces or line breaks.`;
  `SignInSaveFailed` `Signed in as {login}, but the token could not be saved in this computer's keychain: {reason}. VisualCommit keeps it until it closes and asks for it again at the next start.`;
  buttons `Cancel` and `Sign in`, after a failed save one `Close`. No scope or expiry warning (D297).
- **The keychain's reason** the checks provoke: `TokenStoreReasons.Locked`, `the keychain stayed locked` (D268).
- **WP-C's refused-token dialog** (4.7, D276, D330), which A9 shows: title `Fetch failed` (the toolbar
  Fetch's error title); summary `OperationTexts.GitCredentialsRefused("octo")`,
  `GitHub refused git's credentials for octo. If the token has expired or was revoked, sign in to GitHub again in Settings, Accounts.`;
  buttons `Copy output`, `Open settings…` (`OperationTexts.OpenSettingsButton`), `Close`. Git's key line
  (2.56.0.windows.4, replayed against a local server answering 401 on 2026-10-29):
  `fatal: Authentication failed for 'http://127.0.0.1:47395/octo/hello.git/'`.
- **WP-P's text in A9's pictures, asserted by no part A check** (10.2): the left panel's "Pull requests"
  section signed out reads `Sign in to GitHub to see pull requests…` from WP-P's merge.

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). Each check is named A and its number in
design 8.7; its scenario, theme and window size end its "Check" column; its methods and starts are in
part A's table above. "R" in Notes marks the steps the real-window pass repeats (design 13.4 A), "n/a" says
why a check is not repeated. "Screenshots" lists the planned pictures, the scripted run's and then the real
window's. Every step's "requests" are those the fake recorded since the step's mark; every "calls" are the
store's `Calls` since its mark; "git calls" are the call log's, since a quiet mark (part A's "Names in
the checks").

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| A1 | The Accounts section signed out (design 8.2, 8.3, 0.7; D256, D257, D294): the settings screen's fifth button "Accounts", last, 2 below "Keyboard shortcuts"; the section signed out with its introduction and "Sign in…"; opening it asks neither GitHub nor the keychain and adds no git call to the screen's own read; "hosting" served, one empty tab, dark, 1100×700 | **Method 1** (`Check_A1_A2_A5_signed_out_dialog_and_signed_in`): 1. `ServeAsync()`. `ShellDriver.Start(data, gitHub: served.GitHub)` (one empty tab, a new `FakeTokenStore`); wait for the welcome page. Take the quiet call mark (`QuietCallMarkAsync()`). 2. `OpenAccountsAsync()`. Screenshot (`a01a-signed-out`). | **After step 1:** the welcome page; the session's `IsOff` false and its `Account` null; requests none (D257); calls none; no `accounts.json` in the data folder.<br>**After step 2:** the screen `SettingsDialog` on the dialog layer over the welcome page, the card exactly at (120, 40), 860×620. `SettingsNav` holds five buttons, in order `General`, `Git`, `Tools`, `Keyboard shortcuts`, `Accounts`, each 176×30 and 2 apart; `SettingsNavAccounts` at (140, 223) to (316, 253), in the `selected` look, with the keyboard focus, no error dot; the other four in the unselected look. `SelectedSection` is `Accounts`; only `SettingsAccounts` shows. From x 336: `AccountsIntro` `With a GitHub account, VisualCommit lists and creates pull requests, shows authors' pictures and clones your repositories.` (secondary, wrapped, whole); 18 below, the label `GitHub` (x 336 to 468) and, from x 480, `AccountsSignedOut` `Not signed in.`, then 10 below `AccountsSignInButton` `Sign in…`, enabled. No `AccountLogin`, `AccountTokenPlace`, `AccountNeedsSignIn`, `AccountsSignOutNote`, `AccountsSignOutButton`, `AccountsBusy`, `AccountsOff`; no `Git` row (`AccountUseForGit` absent). `Cancel` and `Save` as phase 6's part S; no scroll bar; no text cut. Git calls since the quiet mark (`QuietCallsSinceAsync`, whole refreshes set aside by `GitCallBudget.NotWholeRefreshes`, none expected on an empty tab): exactly the screen's reads on an empty tab (phase 6's SD29: the outside read `git config --show-scope --show-origin -z --list` in `{data}`), nothing else (8.3); exact, because it is the check's meaning, not a cost (ruling 28). Requests none; calls none. | | | `a01a-signed-out`; real window: `a01a-signed-out` | **R** (13.4 A steps 1 and 2): the launch makes no request; a real Ctrl+, and a click on "Accounts"; the five buttons' names read through UI Automation in the order above, "Accounts" selected, "Not signed in." and "Sign in…" enabled, still no request; the capture compared with `a01a-signed-out`. Varies by nature: the status bar's git box behind the backdrop (D248). |
| A2 | The "Add GitHub account" dialog (design 8.2, 8.4; D103, D104, D130, D249, D295): stacked over the screen with its help, the link, the token box and its buttons; the link's page recorded by the fake launcher; a typed token masked on screen and to UI Automation; Esc closes the dialog only; "hosting", one empty tab, dark, 1100×700 | **Method 1**, after A1: 3. `OpenSignInDialogAsync()`. Screenshot (`a02a-dialog-empty`). 4. `TypeToken(OctoToken)`. Screenshot (`a02b-token-masked`). 5. Rest the pointer on the link until its tooltip shows; then click it. 6. Press Esc. | **After step 3:** `SignInDialog`, named `Add GitHub account`, stacked over the screen (the card under the dialog's backdrop, still on Accounts); the card 400 wide, x 350 to 750, centred vertically; top to bottom `DialogTitle` `Add GitHub account`, `SignInHelp` (8.2's text, whole), the link `SignInTokenLink` `Create a token on GitHub` in `VcAccentBrush` `#7C8CFF`, 6 below the help, the label `Token`, `SignInToken` empty with the keyboard focus; no `SignInNoKeychain` (the keychain is there), no `SignInChecking`, `SignInError` or `SignInSaveFailed`; `Cancel` enabled and `Sign in` (`VcPrimaryButton`) disabled, right-aligned; no "In …" line. Calls: `probe` (the dialog's `KeychainAvailableAsync`, 8.4); requests none; no text cut.<br>**After step 4:** the box shows one `•` per character of `OctoToken`; its automation value is empty and its name `Token`; no visible text, automation value or name in the window holds `OctoToken`; `Sign in` enabled, `Cancel` enabled; `Launcher.Requests` empty (the picture's state owes nothing to the fake launcher, R); requests none; calls still `probe` only.<br>**After step 5:** the link's tooltip `Opens GitHub's page for a new token in your browser.`; after the click `Launcher.Requests` holds one launch, `OpenUrl` `http://127.0.0.1:47395/settings/personal-access-tokens/new?name=VisualCommit&contents=write&pull_requests=write` (D295); the dialog still open, the box still masked and holding the token (its automation value empty), `Sign in` enabled; requests none; calls still `probe` only.<br>**After step 6:** the dialog closed; the screen still open on Accounts, as after step 2; the session's `Account` null; no `accounts.json`; requests none; calls `probe` only. | | | `a02a-dialog-empty`, `a02b-token-masked`; real window: `a02b-token-masked` | **R** (13.4 A step 3): a real click on "Sign in…"; "Add GitHub account" stacks over the screen with the focus in the token box; `OctoToken` typed on the real keyboard: the box's Value pattern reads "" and its name "Token", no text element holds the token; compared with `a02b-token-masked`. Steps 5 and 6 are scripted only (the link's launch is the fake launcher's; the real window never opens it); they come after `a02b`, so that the R picture's state is reached as the real window reaches it, with no tooltip or pointer-over underline left on the link. Varies by nature: the box's caret (`a02a`, `a02b`); the status bar's git box. |
| A3 | The check running, and Cancel during it (design 8.4, 7.5; D288): `Checking the token…` with the box and "Sign in" disabled; Cancel closes the dialog and nothing is kept; "hosting", one empty tab, dark, 1100×700 | **Method 3** (`Check_A3_A4_A7_checking_refused_and_save_failed`): 1. `ServeAsync()`. `ShellDriver.Start(data, gitHub: served.GitHub)` (one empty tab). `OpenAccountsAsync()`; `OpenSignInDialogAsync()`. 2. Mark the requests. `GitHub.HoldNext("user")`. `TypeToken(OctoToken)`; click the dialog's `Sign in`, then wait for the hold's `Arrived`. Screenshot (`a03a-checking`). 3. Click `Cancel`. Dispose the hold; then wait until `GitHub.Requests` holds a request of route `user` since step 2's mark (the released request, recorded once the fake has answered it). | **After step 2:** below the box `SignInChecking` `Checking the token…` (secondary); the box disabled, still masked; `Sign in` disabled, `Cancel` enabled; the hold's `Arrived` completed: one `GET api/v3/user` waits at the fake, not yet recorded (requests since the mark none); the session's `Account` null; calls `probe` only.<br>**After step 3:** the dialog closed at once, before the hold's release; the screen on Accounts, `Not signed in.` and `Sign in…`. Once the released request is recorded, requests since step 2's mark exactly that one: `GET api/v3/user`, route `user`, `TokenPresent`, 200, `Login` `octo`, its answer reaching no client (the app had dropped the connection at Cancel); only then: the session's `Account` null (GitHub had not answered when Cancel was clicked, D288), the section unchanged, no `accounts.json`; calls exactly `probe`: nothing saved, read or deleted (design 8.7's "no store call" read as no call but the opening's probe, open point 2). | | n/a | `a03a-checking` | n/a: a held request (0.5). The fake answers and records a held request whatever became of its client: `FakeGitHub.ServeAsync` waits at the hold on the server's own token, not the connection's, and records after the answer; the write to the gone client then fails, and `AuthRemoteServer`'s connection loop swallows that `IOException` or `SocketException` (`AuthRemoteServer.cs:813-815`), so no `Errors` line is expected. Step 3's wait for that record keeps it out of A4's requests: A4's step 4 takes its mark after it. |
| A4 | A token refused, locally and by GitHub (design 8.4, 8.2, 2.4; D298): a token with a space is refused before any request; an unknown token gets `GitHub refused the token: Bad credentials.` with the box kept and selected; nothing is kept; "hosting", one empty tab, dark, 1100×700 | **Method 3**, after A3: 4. Mark the requests (after A3's wait for the released request). `OpenSignInDialogAsync()` (a new dialog, its box empty). `TypeToken(PartA.TokenWithSpace)`; press Enter. Screenshot (`a04a-not-a-token`). 5. With the focus still in the box press Ctrl+A, then `app.Type(PartA.UnknownToken)` (the selection replaced; not `TypeToken`, which would append), press Enter; wait for the check to end. Screenshot (`a04b-refused`). | **After step 4:** `SignInError` `The token can contain only letters, digits and punctuation, without spaces or line breaks.` (`dialogError`, `VcDangerBrush` `#E5606B`); requests none (D298); the box enabled, holding what was typed; `Sign in` enabled; not signed in; calls `probe` only.<br>**After step 5:** requests exactly one, `GET api/v3/user` (route `user`) with a token, 401; `SignInError` `GitHub refused the token: Bad credentials.`; the box enabled, its text kept and all of it selected, the keyboard focus in it; `Sign in` and `Cancel` enabled; the session's `Account` null; no `accounts.json`; calls `probe` only. | | n/a | `a04a-not-a-token`, `a04b-refused` | n/a: a refusal, not a screen the real window adds. The local refusal is an addition to 8.7's list, with 8.4's text (open point 7). Varies by nature: the box's caret. |
| A5 | Signed in with the keychain (design 8.3, 8.4, 7.2, 7.5; D249, D255, D256, D263, D287, D336; R3): the row with the login, the token's place and "Sign out…", the switch on; `accounts.json`'s eight fields and no token; the store holding the key; at 1920×1080, dark and light; "hosting", one empty tab, dark, 1100×700; part B 1920×1080, dark then light | **Method 1**, after A2: 7. Mark the requests and the calls. `OpenSignInDialogAsync()`; `TypeToken(OctoToken)`; press Enter; wait until the dialog has closed. Screenshot (`a05a-signed-in`).<br>**Method 2** (`Check_A5_signed_in_at_1920x1080`, a new start): 8. `ServeAsync()`. `ShellDriver.Start(data, width: 1920, height: 1080, gitHub: served.GitHub)` (one empty tab, a new store). `OpenAccountsAsync()`; `OpenSignInDialogAsync()`. 9. `TypeToken(OctoToken)`; press Enter; wait until the dialog has closed. Screenshot (`a05b-signed-in-1920`). 10. Press Esc. Click `ThemeSwitch` (the light theme). `OpenAccountsAsync()`. Screenshot (`a05c-signed-in-1920-light`). | **After step 7:** requests exactly one, `GET api/v3/user` (route `user`) with a token, 200, `Login` `octo`; calls `save github:127.0.0.1:47395:octo` (one save; the probe was made in step 3); `Tokens.Holds(TokenKey("github", "127.0.0.1:47395", "octo"))`. The dialog closed (no "Close"-only state). The session's `Account`: `SignedIn`, place `Keychain`, note kind `None`, login `octo`, id 1001, `UseForGit` true. `accounts.json`: `version` 1, one record with exactly the scenario facts' eight fields, `useForGit` true, `tokenState` `keychain`. The section: `AccountLogin` `octo` (SemiBold) from x 480, `AccountTokenPlace` `The token is kept in this computer's keychain.` (`settingsNote`), no `AccountNeedsSignIn`, no note; `Sign out…` enabled with the keyboard focus, no `Sign in…`; the `Git` row: `AccountUseForGit` `Use for git over HTTPS`, checked, and `AccountUseForGitNote` `Clone, fetch, pull and push to GitHub over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.`; controls within x 480 to 944; no scroll bar; no text cut; no visible text or automation value holds a token.<br>**After step 8:** the settings card exactly at (530, 200), 860×680 (D216's largest; phase 6's S14 step 14); `SettingsNavAccounts` at (550, 383), 176×30, selected; the section from x 746, `Not signed in.` and `Sign in…` from x 890; `Cancel` and `Save` on y 827 to 859; over it the "Add GitHub account" dialog, its card 400 wide, x 760 to 1160, centred vertically (8.4), otherwise as A2's step 3 shows it; calls `probe`; requests none.<br>**After step 9:** the dialog closed; requests since the start exactly one, `GET api/v3/user`, `TokenPresent`, 200, `Login` `octo` (the sign-in's); calls `probe`, `save github:127.0.0.1:47395:octo`; the session's `Account` `SignedIn`, place `Keychain`, note kind `None`. The card and the places of step 8 (860×680 at (530, 200), the section from x 746, the row's controls from x 890, `Cancel` and `Save` on y 827 to 859); the section's texts as after step 7, `Sign out…` enabled with the keyboard focus; no scroll bar; no text cut.<br>**After step 10:** still signed in (D256); no request and no store call since step 9; the window and the card in the light theme (`#F5F6F8` card, secondary text `#5C6572`, the check's accent `#4353D8`), the same texts and places as step 9, the focus on `SettingsNavAccounts` (a new opening); no text cut. | | | `a05a-signed-in`, `a05b-signed-in-1920`, `a05c-signed-in-1920-light`; real window: `a05a-signed-in` | **R** (13.4 A steps 4 and 5): a real click on "Sign in": since the mark exactly one request, `GET api/v3/user`, `TokenPresent`, 200; the row reads `octo` and `The token is kept in this computer's keychain.`, the switch on; compared with `a05a-signed-in`; Esc closes the screen; `accounts.json` names `octo`, host `127.0.0.1:<port>`, `useForGit` true, and holds no token; `AssertCleanEnd`. The real window's store is memory, available, so it shows the keychain's text; the real keychains are CI's (D259). Part B (steps 8 to 10) is scripted only: 1920×1080 does not fit the development machine's screen and light is scripted only (13.1). "One at 1920×1080, one light" read as two pictures of one start (open point 4). Varies by nature: the status bar's git box. |
| A6 | Session only where no keychain can be reached (D253; design 8.2 to 8.4, 3.7, 7.5): the dialog's warning before anything is typed; the sign-in kept in memory, never saved; the section's note; `tokenState` `session`; "hosting", one empty tab, the store unavailable, dark, 1100×700 | **Method 4** (`Check_A6_A10_session_only_and_its_restart`, a `[Fact]`), first start: 1. `ServeAsync()`; a `FakeTokenStore` with `Available = false`. Start one empty tab with it (`gitHub: served.GitHub`, `tokens:`). `OpenAccountsAsync()`; `OpenSignInDialogAsync()`. Screenshot (`a06a-no-keychain-dialog`). 2. `TypeToken(OctoToken)`; press Enter; wait until the dialog has closed. Screenshot (`a06b-session-only`). 3. Close the app. (The second start is A10 part A.) | **After step 1:** the dialog as A2's step 3, and below the box, before anything is typed, `SignInNoKeychain` `No keychain was found on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again at the next start.` (`dialogWarning`, `VcWarningBrush` `#E0A23B`); calls `probe`; requests none.<br>**After step 2:** requests exactly one, `GET api/v3/user`, 200; the dialog closed (no failed save: no "Close"-only state, open point 3); the session's `Account`: `SignedIn`, place `Session`, note kind `NoKeychain`. The section: `AccountLogin` `octo`, `AccountTokenPlace` with D253's text above (`settingsNote`); `Sign out…` focused; the `Git` row, the switch checked, its note. `accounts.json`: one record, `tokenState` `session`, `useForGit` true. Calls exactly `probe`: no save (D253: the token is never written); `Tokens.Holds(…octo)` false. | | n/a | `a06a-no-keychain-dialog`, `a06b-session-only` | n/a: the real window's keychain is the memory store, available; D253's path needs `FakeTokenStore.Available` (a hook, 0.5). Varies by nature: the box's caret (`a06a`). |
| A7 | A save that fails after the token checked out (ruling 19; D268, D298; design 8.4, 7.5): the dialog stays open with `Signed in as octo, but …` and one "Close"; the account kept for the session with the section's note; no "Try again"; "hosting", one empty tab, dark, 1100×700 | **Method 3**, after A4: 6. Mark the requests. `Tokens.FailNextSave(TokenStoreReasons.Locked)`. With the keyboard focus still in the box and all of its text selected by A4's refusal (step 5's expected result; no Ctrl+A here, so the step also proves the selection is replaced), `app.Type(OctoToken)`, not `TypeToken`, whose `TypeInto` would put the caret at the end and keep `PartA.UnknownToken` in front; press Enter; wait for the check to end. Screenshot (`a07a-save-failed-dialog`). 7. Click `Close`. Screenshot (`a07b-save-failed-section`). 8. Press Esc. | **After step 6:** the selection was replaced (the box held only `OctoToken` when sent: requests since the mark exactly one, `GET api/v3/user`, `TokenPresent`, 200, `Login` `octo`); `SignInSaveFailed` `Signed in as octo, but the token could not be saved in this computer's keychain: the keychain stayed locked. VisualCommit keeps it until it closes and asks for it again at the next start.` (`dialogWarning`); the box disabled and empty; `Cancel` hidden; one button, `Close` (`ConfirmButton`), enabled; no `Try again` anywhere in the window. The session's `Account`: `SignedIn`, place `Session`, note kind `SaveFailed`, note `the keychain stayed locked`. `accounts.json`: `tokenState` `session`, `useForGit` true. Calls since step 6: `save github:127.0.0.1:47395:octo` (failed); `Holds(…octo)` false.<br>**After step 7:** the dialog closed, still signed in. The section: `AccountLogin` `octo`, `AccountTokenPlace` `The token could not be saved in this computer's keychain: the keychain stayed locked. VisualCommit keeps it until it closes and asks for it again at the next start.` (`settingsNote`); `Sign out…` focused, no `Sign in…`; the `Git` row, checked; no `Try again`.<br>**After step 8:** the screen closed; still signed in (D256). | | n/a | `a07a-save-failed-dialog`, `a07b-save-failed-section` | n/a: needs `FailNextSave` (a hook, 0.5). Closing by Enter and by Esc is 8.8's `SignInDialogViewModelTests`'. |
| A8 | Sign out with its confirmation (D256, D291, D296; design 8.2, 8.3, 7.5, 3.7): the question with the login and the primary "Sign out"; Cancel changes nothing; "Signing out…" while the keychain works; afterwards signed out, the store and the file empty of the account, no `avatars/`; a delete the keychain refuses: signed out with its note, which the next opening no longer shows; "hosting", one empty tab, dark, 1100×700 | **Method 5** (`Check_A8_sign_out`): 1. `ServeAsync()`. Start one empty tab (`gitHub: served.GitHub`); `SignInAsync(OctoToken)`. `OpenAccountsAsync()`. Mark the requests and the calls. 2. Click `Sign out…`; wait for the confirmation. Screenshot (`a08a-sign-out-question`). 3. Click `Cancel`. 4. `Tokens.HoldNext()`. Click `Sign out…`, then the confirmation's `Sign out`; wait for the hold's `Arrived`. Screenshot (`a08b-signing-out`). 5. Dispose the hold; `WaitForAccountAsync(a => a is null)`. Screenshot (`a08c-signed-out`). 6. `OpenSignInDialogAsync()`; `TypeToken(OctoToken)`; press Enter; wait until signed in. `Tokens.FailNextDelete(TokenStoreReasons.Locked)` (a hook `FakeTokenStore` does not have yet: steps 6 and 7 depend on the lead's ruling, open point 1). `SignOutAsync(confirm: true)`. Screenshot (`a08d-sign-out-note`). 7. Press Esc. `OpenAccountsAsync()`. | **After step 2:** `ConfirmationDialog` stacked over the screen: `ConfirmTitle` `Sign out of GitHub?`; `ConfirmQuestion` `Sign out of GitHub as octo? VisualCommit removes its token and the authors' pictures it saved from this computer, and no longer lists pull requests. Git over HTTPS asks your own credential helpers again.`; no `ConfirmDetail`; `Cancel` and `Sign out` (`ConfirmButton`) in the primary look (`VcPrimaryButton`), not the danger one (D296); no "In …" line. Still signed in; calls none; requests none.<br>**After step 3:** the question closed; the section as before it; `accounts.json` and the store unchanged; calls none.<br>**After step 4:** `AccountsBusy` `Signing out…` beside the buttons; `Sign out…` disabled (no `Sign in…` shows); calls `delete github:127.0.0.1:47395:octo`, held.<br>**After step 5:** the section: `AccountsSignedOut` `Not signed in.`, `Sign in…` enabled with the keyboard focus; no `AccountLogin`, `Sign out…`, `AccountsBusy`, note or `Git` row. The session's `Account` null; `accounts.json` holds `version` 1 and no record (7.5); no `avatars/` folder in the data folder (D291); `Holds(…octo)` false; calls since step 4 exactly the one `delete`; requests since step 1's mark none.<br>**After step 6:** the second sign-in made one `GET api/v3/user` (200) and `save github:127.0.0.1:47395:octo`; the sign-out one `delete github:127.0.0.1:47395:octo`, which failed. The section: `Not signed in.`, below it `AccountsSignOutNote` `Signed out, but the token could not be removed from this computer's keychain: the keychain stayed locked.` (`dialogWarning`), then `Sign in…` with the focus. The session's `Account` null; `accounts.json` no record (the record goes even when the keychain kept its entry, 7.5); `Holds(…octo)` true (the entry kept).<br>**After step 7:** the reopened section shows `Not signed in.` and `Sign in…` and no note (`SignOutNote` clears when the screen closes, 8.3). | | n/a | `a08a-sign-out-question`, `a08b-signing-out`, `a08c-signed-out`, `a08d-sign-out-note` | n/a: the real window never signs out (13.4 A). `a08b` needs a held store call, `a08d` a failed delete (hooks, 0.5). **Depends on open point 1**: `FakeTokenStore` as wave 1 built it cannot fail a delete (its `DeleteAsync` answers only `Done` or `NotFound`, `Available` changes only the probe and the save, and the vault skips the store when its one probe found none), so steps 6 and 7 and `a08d` cannot be coded until `FailNextDelete` exists; steps 1 to 5 can. |
| A9 | Needs sign-in after a refused token, the API's way and git's (D330, D276, D273, D255; design 7.6, 7.8, 4.6, 4.7, 8.3): a revoked token refused by the pull-request read of `hello` marks the account; "Sign in…" again with `OctoClassicToken` works; that token revoked, a fetch through the app's helper fails with WP-C's dialog, no credentials dialog, and the account needs sign-in again once the session's `GET /user` has answered 401; "Open settings…" opens Accounts; "hosting", one empty tab then `hello`, dark, 1100×700 | **Method 6** (`Check_A9_needs_sign_in_after_a_refused_token`): 1. `ServeAsync()`. Start one empty tab (`gitHub: served.GitHub`); `SignInAsync(OctoToken)`. 2. `GitHub.Revoke(OctoToken)`. Mark the requests and the calls. `Folders.Answer(served.Hello.Path)`; click `OpenRepoButton`; `WaitForRepositoryAsync()`; `WaitForAccountAsync(a => a?.State == AccountState.NeedsSignIn)`; wait until the parsed `accounts.json` shows `tokenState` `refused` (part A's conventions); `WaitForWatchersQuietAsync()`. 3. `OpenAccountsAsync()`. Screenshot (`a09a-needs-sign-in-api`). 4. Mark. `OpenSignInDialogAsync()`; `TypeToken(OctoClassicToken)`; press Enter; wait until signed in. Press Esc. Wait for the log line `hello: listed 4 open pull requests of octo/hello` since the mark (and, from WP-V's merge, `AvatarsShown("hello")`) and for the watchers to be quiet. 5. `GitHub.Revoke(OctoClassicToken)`. Mark the requests, the git requests, the calls and the call log. Click `FetchButton`; wait for the fetch's end and the error dialog; wait for a request `GET api/v3/user` answered 401 since the mark, for `WaitForAccountAsync(a => a?.State == AccountState.NeedsSignIn)` and until the parsed `accounts.json` shows `tokenState` `refused`. Screenshot (`a09b-git-refused`). 6. Click `Open settings…` (`ErrorExtraButton`); wait for the screen and its reads. Screenshot (`a09c-needs-sign-in-git`). | **After step 2:** `hello`'s tab open with its graph; requests since the mark: each made with a token and answered 401, the first `GET api/v3/repos/octo/hello` (route `repo:octo/hello`, the refresh's first read, 7.8 step 1), none answered 200 and none made after the account was marked (no request without the account's token, 7.6). The session's `Account`: `NeedsSignIn`, note kind `Refused`; `accounts.json` (waited for: 7.6 sets the account before it writes the file): `tokenState` `refused`, `useForGit` true, the record otherwise as after the sign-in; calls none (the entry stays, `Holds(…octo)` true); `hello`'s hosting: `State.PullRequests` `SignedOut`, `Bindings` empty; no dialog of any kind (the API's refusal shows only in Accounts).<br>**After step 3:** the screen over `hello`'s window, on Accounts: `AccountLogin` `octo`, `AccountNeedsSignIn` `GitHub refused the token. Sign in again with a new token.` (`dialogWarning`); `Sign in…` and `Sign out…` both enabled; the `Git` row, checked, with its note. The left panel's "Pull requests" section behind the backdrop is WP-P's region: today's placeholder until WP-P merges, then `Sign in to GitHub to see pull requests…` (10.2); nothing is asserted there.<br>**After step 4:** requests since the mark begin with `GET api/v3/user` with a token, 200, `Login` `octo`; the refresh's `GET api/v3/repos/octo/hello` and `GET api/v3/repos/octo/hello/pulls` follow, 200 (from WP-V's merge the avatars' requests too), none answered 401; calls exactly `save github:127.0.0.1:47395:octo` (the same key, no forget: D263); the session's `Account` `SignedIn`, `Keychain`, `None`; `accounts.json` one record, `tokenState` `keychain` (a sign-in writes the file before it sets the account, 7.5); `hello`'s hosting's `Bindings` exactly the scenario facts' one binding.<br>**After step 5:** no credentials dialog and no prompt at any time. The error dialog over `hello`'s window: `ErrorTitle` `Fetch failed`; `ErrorSummary` `GitHub refused git's credentials for octo. If the token has expired or was revoked, sign in to GitHub again in Settings, Accounts.`; `ErrorRepository` `In hello`; `ErrorOutput` git's standard error cleaned (`GitOutputCleaner.Clean`), holding the key line `fatal: Authentication failed for 'http://127.0.0.1:47395/octo/hello.git/'` and ending with the app's line `error: Could not fetch origin` (the toolbar's Fetch is `FetchAsync(null)`, D109; the lines before it are git's); `ErrorCommand` `git fetch --progress origin` (`hello`'s only remote, so the last fetch, without `--no-auto-maintenance`; the account reaches git through the environment, not the command); buttons left to right `Copy output`, `Open settings…`, `Close`. Git requests on `octo/hello` since the mark: each answered 401; at least one carries credentials with `GivenUserName` `x-access-token` (the app's helper answered git's `get` from the account, D255); none carries another user name. API requests since the mark: exactly one, `GET api/v3/user` with a token, 401 (the session's check of git's `erase`, D330). The session's `Account` `NeedsSignIn`, `Refused`; `accounts.json` (waited for) `tokenState` `refused`, `useForGit` true; calls none. `hello`'s remote branches as before the fetch.<br>**After step 6:** the error dialog closed; the screen open with `SelectedSection` `Accounts` and its button selected; the section as after step 3. Behind the backdrop, the same WP-P region as step 3. | | n/a | `a09a-needs-sign-in-api`, `a09b-git-refused`, `a09c-needs-sign-in-git` | n/a: the real window revokes nothing; the refusal's paths are WP-C's and WP-S's classes' as well (`RefusedTokenDisplayTests`, `AccountCredentialTests`, `HostingSessionCredentialTests`). The fetch is the first request with `OctoClassicToken` after its revoke: step 4's waits keep a later API request (the pull requests' or, from WP-V's merge, the avatars') from marking the account first, after which the fetch would be unbound and git would ask through the credentials dialog. 7a's first error-dialog assertion (design 16.3) is added here. **WP-P's region**: the three pictures show `hello`'s left panel; the lead inspects them again at WP-P's and WP-V's merges (0.4, 10.8, 11.6). Varies by nature: git's output in `ErrorOutput` (only the key line and the app's last line `error: Could not fetch origin` are asserted) and the status bar's git box. |
| A10 | Starts after a sign-in (ruling 6; D253, D287; design 7.4): after a session-only sign-in the account needs sign-in with no keychain call; after a keychain sign-in the next start is signed in with one store read and no request to GitHub; a token gone from the keychain gives "needs sign-in"; "hosting", one empty tab per start, dark, 1100×700 | **Part A, method 4's second start** (after A6, one data folder, the same store, still unavailable, and the same fake): 4. Mark the requests and the calls. Start one empty tab on the same data folder (`gitHub: served.GitHub`, `tokens:` the same store). `OpenAccountsAsync()`. Screenshot (`a10a-session-ended`).<br>**Part B, method 7** (`Check_A10_restart_keeps_the_sign_in_and_finds_the_token_gone`, a `[Fact]`, one data folder, one store, one fake): 5. `ServeAsync()`. First start, one empty tab; `SignInAsync(OctoToken)`; close the app. 6. Mark. Second start; `OpenAccountsAsync()`. Screenshot (`a10b-still-signed-in`). Close the app. 7. `Tokens.Remove(TokenKey("github", "127.0.0.1:47395", "octo"))`. Mark. Third start; `OpenAccountsAsync()`. Screenshot (`a10c-token-gone`). Close the app. | **After step 4:** the start's calls: none (a `session` record reads no store, D287); requests none (no `GET /user` at a start). The session's `Account`: `NeedsSignIn`, note kind `SessionEnded`. The section: `AccountLogin` `octo`, `AccountNeedsSignIn` `The token was kept for the last session only. Sign in again.` (`dialogWarning`); `Sign in…` and `Sign out…` enabled; the `Git` row, checked. `accounts.json` as step 2 of A6 left it.<br>**After step 5:** `accounts.json` one record, `tokenState` `keychain`; `Holds(…octo)` true.<br>**After step 6:** the second start's calls exactly `probe`, `read github:127.0.0.1:47395:octo` (one store read, ruling 6); requests since the mark none (no `GET /user`, D287; an empty tab lists nothing). The session's `Account` `SignedIn`, `Keychain`, `None`. The section as A5's step 7 shows it, the focus on `SettingsNavAccounts`. `accounts.json` byte for byte as after step 5.<br>**After step 7:** the third start's calls exactly `probe`, `read github:127.0.0.1:47395:octo` (not found); requests none. The session's `Account` `NeedsSignIn`, note kind `TokenMissing`. The section: `AccountLogin` `octo`, `AccountNeedsSignIn` `The token is no longer in this computer's keychain. Sign in again.`; `Sign in…` and `Sign out…` enabled; the `Git` row, checked. `accounts.json` byte for byte as after step 5 (a start writes nothing). | | n/a | `a10a-session-ended`, `a10b-still-signed-in`, `a10c-token-gone` | n/a: scripted only (0.5): a restart that keeps the sign-in needs one store for two starts, and the real window's store is memory. Each start is a `FreshApplication.RunAsync` half (D32). |
| A11 | The "Use for git over HTTPS" switch (D216, D255, D290; design 8.3, 7.5, 7.8): off and Save gives `useForGit` false and no bindings for `hello`; the reopened screen shows it off; Cancel and Esc after a toggle change nothing; on and Save binds again; "hosting", tabs `hello` and an empty one, dark, 1100×700 | **Method 8** (`Check_A11_use_for_git_switch`): 1. `ServeAsync()`. `StartWithTabs(data, [served.Hello.Path, null], activeTab: 0, gitHub: served.GitHub)`; `WaitForRepositoryAsync()`; `SignInAsync(OctoToken)`; wait for `hello: listed 4 open pull requests of octo/hello` and for the watchers to be quiet. 2. Click tab 1 (the empty tab). `OpenAccountsAsync()`; click `AccountUseForGit`; click `Save`; wait until the screen has closed, then until the parsed `accounts.json` shows `useForGit` false and `hello`'s hosting's `Bindings` is empty (part A's conventions). 3. `OpenAccountsAsync()`. Screenshot (`a11a-switch-off`). 4. Click `AccountUseForGit`; click `Cancel`. `OpenAccountsAsync()`; click `AccountUseForGit`; press Esc. `OpenAccountsAsync()`. 5. Click `AccountUseForGit`; click `Save`; wait until the screen has closed, then until the parsed `accounts.json` shows `useForGit` true and `hello`'s hosting's `Bindings` holds one binding. | **After step 1:** `hello`'s hosting's `Bindings` is the scenario facts' one binding; the session's `BindsGit` true; `accounts.json` `useForGit` true.<br>**After step 2:** before Save, the box unchecked and nothing changed (D216: `accounts.json` `useForGit` true, the bindings as step 1's); after Save, the screen closed; `accounts.json` (waited for: the switch's Save writes it off the UI thread after the screen has closed, 7.2, 7.5) `useForGit` false, the record otherwise unchanged (`tokenState` `keychain`); the session's `Account` still `SignedIn` with `UseForGit` false and its `BindsGit` false; `hello`'s hosting's `Bindings` empty (waited for: the events go through the UI thread); calls none (the switch touches no keychain).<br>**After step 3:** the section: `AccountLogin` `octo`, `The token is kept in this computer's keychain.`, `Sign out…`; `AccountUseForGit` `Use for git over HTTPS` unchecked and enabled, `AccountUseForGitNote` with its text; the welcome page of the empty tab behind the backdrop, the tab strip `hello`, `New tab` (active).<br>**After step 4:** after Cancel and after Esc the screen closed with `accounts.json` `useForGit` false and `hello`'s hosting's `Bindings` empty; the last opening shows the switch unchecked.<br>**After step 5:** `accounts.json` (waited for) `useForGit` true, the record otherwise unchanged; `hello`'s hosting's `Bindings` the one binding again (waited for); the session's `BindsGit` true; calls none. | | n/a | `a11a-switch-off` | n/a: a setting at Save; the real window shows the switch on in `a05a`. The picture is taken on the empty tab so that it shows no region of WP-P's or WP-V's (open point 5). |

## What proves each Delivers item

`plan.md`'s phase 7a "Delivers", against part A; "later parts" are B, P, N and G, written before their
packages, or the work packages' own classes (design 0.1).

| Delivers item | Part A | Elsewhere |
|---|---|---|
| The provider model for 7b | — | later parts; WP-G's and WP-S's classes |
| Sign-in to GitHub with a token in Settings, Accounts; keychain, else the session; one account (D249, D253, D256, D258, D263; R3) | A1 to A11 (the keychain through `FakeTokenStore`); the real window repeats A1, A2 and A5 | the real stores: WP-K's classes on CI (D259, D333) |
| The account's token on the app's git calls to github.com (D255) | A9 (the fetch's credentials from the app's helper as `x-access-token`, git's refusal), A11 (the switch) | WP-C's classes; part B's clone |
| Browse and clone (D261) | — | later parts (B) |
| Pull-request list and menu, create, badge, open in browser (D251) | — | later parts (P, N, G) |
| Avatars from GitHub only (D252) | A8 in part: a sign-out leaves no `avatars/` (D291) | later parts (G) |
| The opt-in live command (D250) | — | WP-LIVE |
| From phase 3: "Create pull request" in the branch, remote-branch and drop menus; the section's menu | — | later parts (P, N) |
| From phase 3: a provider's token reaches git | A9 | WP-C's classes |
| From phase 6: the push guard; the prompt group per account | — | WP-C's classes |
| From phase 6: the wheel test; the real-window csproj's comment | — | L1 (`ShellDriverTests`; the file) |
| From the review: W1, S8, R8; G23's split | — | WP1 |

## Changes to expected results

### Phase 6: the settings screen gains "Accounts" (D256; design 0.7, 8.6)

Phase 6's pictures that show the settings screen (checks S14 to S20 and L31) change in the section list
only: a fifth button, "Accounts", 176 × 30, 2 below "Keyboard shortcuts" (at 1100×700 x 140 to 316, y
223 to 253; at 1920×1080 x 550 to 726, y 383 to 413), in the look of the buttons that are not selected.
Nothing else in them changes. Reason: D256 adds the Accounts section, last in the list. The gate compares
them by region against references made on `master` (plan risk 32). Listed by opening every picture of
checks S14 to S21 and L31 and the real window's, and scanning phase 6's other pictures for the settings
card (the laptop's `artifacts/visual/phase-6/` as wave 1's whole run left it on 2026-10-29):

- **Scripted** (31): `s14a-general`, `s14b-tools`, `s14c-git-global`, `s14d-git-repository`,
  `s14e-shortcuts-top`, `s14f-shortcuts-end`, `s14g-tools-other-error`, `s14h-general-1920`,
  `s14i-git-global-1920` (the last two at 1920×1080); `s16a-general-from-file`, `s16b-minutes-error`,
  `s16d-auto-fetch-off`; `s17b-editor-pick`, `s17c-tools-editor-chosen`; `s18a-name-refused`,
  `s18b-repository-global-identity`, `s18d-global-repository-sets-its-own`, `s18e-repository-own-identity`;
  `s19a-global-ff-only`, `s19c-repository-same-as-global`, `s19d-global-repository-sets-its-own`,
  `s19e-repository-rebase`, `s19g-branch-rebase-notice`, `s19h-branch-rebase-marked`, `s19i-other-value`;
  `s20b-merge-tool-pick`; `l31d-settings-light`, `l31d-settings-system-dark`, `l31d-settings-system-light`,
  `l31e-settings-git-dark`, `l31e-settings-git-light`. In `s17b`, `s20b`, `l31e-settings-git-dark` and
  `-light` the Pick dialog stacks over the screen and the new button shows beside it under the Pick
  dialog's backdrop. `s20b` is not in design 0.7's list (open point 9).
- **Real window** (2, with their difference pictures): `s14a-general` and `l31d-settings-light`.
- **Not changed** (the screen closed): `s15a` to `s15e`, `s16c-stash-dialog-untracked`,
  `s17a-open-file-asks`, `s17d-file-menu-with-editor`, `s18c-committed-as-global`,
  `s18f-committed-as-repository`, `s19b-pull-refused`, `s19f-pulled-rebased`, `s20a-merge-tool-disabled`,
  `s20c-merge-tool-enabled`, `s21a-off-old-commit-blocked`, `s21b-off-checkout-ran`, and the real window's
  `s15-system-start`, `s15-system-start-with-frame` and `s15-theme-click`.

### Earlier tests (design 8.6)

- `SettingsDialogViewTests.The_first_focus_is_the_section_button_up_and_down_move_and_enter_shows_without_saving`
  (`tests/VisualCommit.App.Tests/SettingsDialogViewTests.cs:159`): the third Down of `:177-179` now reaches
  `SettingsNavAccounts`; the test asserts that, presses Down once more (the focus stays), then Up twice to
  `SettingsNavTools`, and keeps the Enter that shows Tools.
- `tests/VisualCommit.Testing/Headless/ShellDriver.Phase6.Settings.cs:122-131`: `ChooseSection` maps
  `SettingsSection.Accounts` to `SettingsNavAccounts`.
- Unchanged: `AssertNav` and `AssertFrame` (`Phase6Checks.S.Helpers.cs:181`, `:245-246`) and
  `Phase6Checks.L.Overlays.cs:604`, which name the four buttons by id; part S's counts of the screen's
  reads (`AssertScreenReadsAsync`): the Accounts section reads no git, keychain or GitHub when the screen
  opens (8.3). WP-A runs `Phase6ChecksS` and `--filter-method "*Check_L31*"` in
  `VisualCommit.VisualTests.Phase6Look`.

### Stays as it is

Every other earlier picture and expectation: both harnesses start with GitHub off and the keychain in
memory (rulings 23, 24), so the welcome page, the palette (no Accounts or sign-in entry, D299), the
left panel's "Pull requests" placeholder and every menu stay as they are, and no earlier count of git
calls moves. Pictures that show the status bar's git box differ from `phase-6-pictures.sha256` for D248
whatever part A does; they are compared by region with `master`'s references made with the same git.

### Changed in this report after its first commit

A method split when the checks first run, a mark for the real-window pass changed after it was written,
or an expected result changed for a reason the build or the gate finds, goes here with its reason.

## Found by the gate

Faults the gate found, and the commit that fixed each.

## Could not run

Checks or parts of the gate that could not run on the Windows machine either, why, and what that
leaves unproven. A real-window check that was not run only because the session was not on
Windows does not belong here: it stays "Not run: needs Windows" in the table, and the gate is
unfinished until a Windows session has run it.

## Other platforms

The CI run of the gate's commit and its result in each job (the three platforms and the two
bare Ubuntu containers), and which of the screenshots CI took on macOS and Linux were opened. For part A
also each job's keychain step: the real store's class on macOS and in both containers, and on Windows
the class or, refused with error 1312, its removal from the job (D259, D333; design 3.10).

## macOS checklist for the owner

To try by hand on a Mac (CI runs part A headless with the fake keychain, and the real store's class
against a throw-away keychain; these are what only a person on a Mac sees):

1. **Sign in** (D249, D295): Cmd+, , "Accounts" (last in the list), "Sign in…". "Create a token on GitHub"
   opens the default browser on github.com's new fine-grained token page with the name VisualCommit and
   Contents and Pull requests set to read and write. Paste the token: the box shows dots; "Sign in"
   shows "Checking the token…", then the row reads your login and "The token is kept in this computer's
   keychain.", with "Use for git over HTTPS" on.
2. **The keychain prompt** (ruling 20, D268): if the login keychain is locked, macOS asks for its
   password. Dismissing that prompt gives "Signed in as <login>, but the token could not be saved in this
   computer's keychain: the keychain stayed locked. …" with one "Close", and the section says the token
   is kept until the app closes. Unlocked, Keychain Access shows an item labelled `VisualCommit: GitHub
   (<login>)`, service `VisualCommit:github:github.com`, account `<login>` (design 3.3).
3. **A restart**: quit with Cmd+Q and start again: Settings, Accounts shows the account signed in without
   asking for the token (no keychain prompt is expected: the item trusts `/usr/bin/security`, plan risk
   34); a repository with a github.com remote fetches without a credentials dialog.
4. **Sign out**: "Sign out…", then "Sign out" in the question: the section reads "Not signed in." and
   the Keychain Access item is gone.

## Open points and the lead's rulings

Where the design is silent or two of its sections disagree, the reading this report takes, and the
lead's ruling on it (2026-10-29, before this report was first committed and before WP-A starts).

1. **A delete that fails (A8, steps 6 and 7; design 8.7 item 8).** `FakeTokenStore` as wave 1 built it
   cannot fail a delete: its `DeleteAsync` answers only `Done` or `NotFound`; `Available` changes only
   the probe and the save; a hold (`HoldNext`) only delays the call, which the vault makes with
   `CancellationToken.None`; and when the start's one probe found no keychain, `TokenVault.DeleteAsync`
   skips the store and returns null. No combination of `FailNextSave`, `Remove`, `HoldNext` and
   `Available` reaches "a sign-out whose delete fails shows its note", so A8's steps 6 and 7 and `a08d`
   cannot be coded with the fakes as built. The report needs a new
   `FakeTokenStore.FailNextDelete(string reason)`, as `FailNextSave`: the next `DeleteAsync` records
   `delete <key>` (and waits at a hold like any call), keeps the token (`Holds` stays true) and returns
   `TokenStoreResult(Failed, reason)` once; the one after it is normal. The vault then logs its Warning
   and returns the reason (`TokenVault.cs:206-214`), which the section shows as `AccountsSignOutNote`.
   With it a `FakeTokenStoreTests` case (`tests/VisualCommit.Hosting.Tests/FakeTokenStoreTests.cs`): after
   a save, `FailNextDelete(Locked)`; a delete gives `Failed` with `Locked`, `Holds` true, `Calls` ending
   `delete <key>`; a second delete gives `Done` and `Holds` false. The report's reading of who adds it:
   the lead, on `phase/7a-hosting-github` in one commit of its own (the method and its test) before
   WP-A's branch is cut, since `FakeTokenStore.cs` is WP-F's file; or else WP-A's builder in WP-A's
   commit (5), part A's code (design 8.9), with the lead's leave. Until the ruling, A8's steps 6 and 7
   depend on it; steps 1 to 5 do not. **Ruling:** WP-A's builder adds `FailNextDelete` and its `FakeTokenStoreTests` case, as specified
   here, in WP-A's commit (5); `FakeTokenStore.cs` passes to WP-A at WP-S's merge, and nobody else edits
   it before then. A8's steps 6 and 7 and `a08d` stand.
2. **"No store call" (A3).** Design 8.7 says Cancel during the check leaves "no store call", while 8.4
   probes the keychain when the dialog opens (`KeychainAvailableAsync`), which `FakeTokenStore.Calls`
   records as `probe`. The report expects `Calls` to be exactly `probe`: nothing saved, read or deleted. **Ruling:** accepted: `Calls` exactly `probe`.
3. **What `SignInResult` carries without a keychain (A6).** Which note kind applies is settled: design
   7.5 step 6 says "Session without a reason", but the vault as built returns `(Session,
   TokenStoreReasons.NoKeychain)` when no keychain can be reached, and the lead's wave 1 notes for WP-S
   (the vault) settle it: "With no keychain reachable, `KeepAsync`'s reason is exactly
   `TokenStoreReasons.NoKeychain`; any other reason is a failed save (ruling 19), so the note kinds
   NoKeychain and SaveFailed are told apart by that reason or by `KeychainAvailableAsync`." A6 expects
   note kind `NoKeychain` and D253's text in the section, never `SaveFailed`'s. Still open, for WP-S to
   decide as it builds 7.5: that the session's `SignInResult.SaveFailedReason` is null for that reason
   (7.5 step 8 passes on the vault's reason), so that the dialog closes on success (8.4's "success without
   a save reason") rather than showing the "Close"-only state. A6 takes that reading. **Ruling:** accepted, and it binds WP-S: design 7.5 step 6's "Session without a reason" is the vault's
   `TokenStoreReasons.NoKeychain`, which gives `NoKeychain` and a null `SaveFailedReason`; any other
   reason is `SaveFailed`. The lead checks it at WP-S's merge.
4. **"One picture at 1920×1080, one light" (A5).** Read as two pictures from one start at 1920×1080 that
   signs in through the dialog: dark, then light after the toolbar's Theme switch (`a05b`, `a05c`). A
   light picture at 1100×700 would need a start of its own. **Ruling:** accepted.
5. **Pictures over `hello` (A11).** Only A9's pictures are named in 8.7 as showing `hello`'s window, which
   changes at WP-P's and WP-V's merges. A11's picture is taken with a second, empty tab active, so that it
   shows no such region; its bindings are still `hello`'s. **Ruling:** accepted.
6. **Automation ids.** Design 13.1 names some of part A's ids; for the rest the report reads design 8.3's
   and 8.4's element names as ids (`SignInHelp`, `SignInTokenLink`, `SignInNoKeychain`, `SignInChecking`,
   `SignInError`, `SignInSaveFailed`, `AccountNeedsSignIn`, `AccountsSignOutNote`, `AccountsSignOutButton`,
   `AccountsBusy`, `AccountUseForGitNote`, `AccountsOff`, `SettingsAccounts`), with the dialogs' usual
   `DialogTitle`, `CancelButton` and `ConfirmButton`. **Ruling:** accepted: these names are the ids WP-A gives the elements.
7. **Steps beyond 8.7's list**, with texts and places the design gives: A4's local refusal (D298), A8's
   `Signing out…` under a held delete, A11's switch turned on again and Saved, A2's tooltip of the link,
   and A5's dialog at 1920×1080 (8.4's x 760 to 1160, asserted, not pictured). **Ruling:** accepted.
8. **GitHub off.** No check shows the section with GitHub off (`AccountsOff`, no button; D294): 8.7 lists
   none, and 8.8's `AccountsSettingsViewModelTests` covers it. A step on an empty tab started without
   `gitHub:` would picture it in about a second. **Ruling:** no picture: 8.8's view-model test covers the state, as 8.7 plans.
9. **`s20b-merge-tool-pick`** shows the settings screen under the Pick dialog and changes with the
   others, though design 0.7's list of candidates leaves out `s20*`; the report lists it (31 scripted
   pictures in all). **Ruling:** accepted; design 0.7's list is short by this picture, which the design's as-built notes
   record.
10. **`accounts.json` after a sign-out (A8).** Design 7.5 removes the record; the report expects the file
    kept with `version` 1 and no record, not deleted. **Ruling:** accepted: 7.5's sign-out removes the record and 7.2's save writes the file.
