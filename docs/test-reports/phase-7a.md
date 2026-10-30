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
the checks take positions from the controls' bounds (D122's rule). Part B was added on 2026-10-29,
while WP-A was being built and before WP-B started, from the design's sections 0, 1, 2.6, 6.2, 9, 13.4 B, 16
and 17, decisions D255, D261, D276, D299 to D302, D331 and D338, phase 1's clone checks 13 and 14, and
the harness, the fake and the scenario as wave 1 and WP-S built them (`5613450`). No part B UI existed,
nor WP-A's dialog: every text is design 9.2's, 1.7's or an earlier package's, every place design 9.3's or
9.4's or the clone form's as phase 1 drew it. Part P was added on 2026-10-29, while WP-B was being built and
before WP-P started, from the design's sections 0, 1, 2.6, 7.8, 10, 13.4 P, 13.5, 16 and 17, decisions D251,
D257, D303 to D308, D321, D322 and D338, phase 1's and phase 3's left panel and menus, and the harness, the
fake and the scenario as wave 1 and WP-S built them (`c25326e`). No part P UI existed: every text is design
10.2's, 10.3's, 1.7's or an earlier package's, every place 10.3's or the left panel's as phase 1 drew it.
Part G was added on 2026-10-29, while WP-P was being built and before WP-V started, from the design's
sections 0, 1, 2.3, 2.6, 6.3, 6.4, 7.8, 7.9, 11, 13.4 G, 16 and 17, decisions D252, D257, D286, D309 to
D311 and D322, phase 1's graph and details panel, and the harness, the fake, the scenario and WP-S's
`RepositoryHosting` as built (`c8e7a62`). No part G UI existed: every text is design 11.2's, 11.3's or an
earlier package's, every place 11.2's or 11.3's or the graph's and the details panel's as phase 1 drew
them, and the labels' widths were measured (open point 41). Part N was added on 2026-10-29, while WP-P
was being built and before WP-V and WP-N started, from the design's sections 0, 1, 2.3, 2.4, 2.6, 6.2,
6.3, 7.8, 7.10, 10.4, 11.2, 12, 13.4 N, 16 and 17, decisions D251, D257, D276, D289, D293, D306, D312 to
D318, D322 and D338, phase 3's menus, status bar and pick dialog, and the harness, the fake, the scenario
and WP-S's `RepositoryHosting` as built (`30d8843`). No part N UI existed, nor WP-P's menu items or
WP-V's badge: every text is design 12.6's, 10.2's or an earlier package's, every place 12.4's or the
status bar's and the menus' as phase 3 drew them.
"What the checks share" holds what every part relies on, then each part's conventions, test methods and
texts; "Checks" is the table of A1 to A11 (design 8.7's numbering), B1 to B9 (design 9.7's list, numbered
here in its order), P1 to P15 (design 10.7's list, numbered here in its order), G1 to G7 (design 11.5's
table, G1 to G5, then G6 and G7 beyond it, open point 40) and N1 to N10 (design 12.8's table, N1 to N6,
then N7 to N10 beyond it, open point 46); "Changes to expected results"
lists the earlier pictures and tests each part changes on purpose; "Open points and the lead's rulings"
lists where the design is silent or its sections disagree, with the reading this report takes. Tokens
are written here only by their constant names, never by value.

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
  number in its section of the design (A1 to A11, design 8.7; B1 to B9 and P1 to P15, design 9.7's and
  10.7's lists in their order, which this report numbers); test methods are `Check_<id>…_<what>`.
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
  WP-S writes its line only when a list is applied (`RepositoryHosting.Finish`), and WP-V its own only
  when avatars are applied (`RepositoryHosting.FinishAvatars` applies nothing after a failed read), so a
  picture of the loading or failed state, which holds no list, waits for that state instead (part P's
  conventions; open point 34), and one whose authors' read failed, or of a tab that asks for no
  avatars, for its warning or its state (part G's conventions; open point 39).
- **Git calls** are counted only through `GitCallBudget` (ruling 28); `CallsSince`, `AssertCalled` and
  `AssertCalledInOrder` take a `long` mark from `ShellDriver.QuietCallMarkAsync()` and prove presence and
  order only (2.8). Signed out, no part adds a git call to an earlier count: the URL-rewrites read
  (`git config --null --get-regexp ^url\.`) runs only while signed in (D281), and a clone reads git's
  configuration only when the account binds its URL (9.3).
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
  held request, and it is taken signed in with `OctoToken`. Part A's R pictures are on one empty tab;
  part B's one, `b03a-listed`, in the state the palette's path reaches (part B's conventions); part P's
  two, `p06a-rows` and `p11c-section-menu`, on one tab on `hello` signed in through the Accounts screen as
  the real window does, after D322's lines (part P's conventions); part G's one, `g01a-badges-and-avatar`,
  in the same way on `hello` prepared by `PartG.PrepareAsync` (part G's conventions); part N's two,
  `n01a-form` and `n01b-created`, in the same way on `hello` prepared by `PartN.PrepareAsync` (part N's
  conventions).
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

What part A uses, which later parts share; each later part's own facts are in its own section (part
B's in "Scenario facts part B uses", part P's in "Scenario facts part P uses", part G's in "Scenario
facts part G uses", part N's in "Scenario facts part N uses").

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

### Part B: browse and clone

Part B covers the clone form's "From GitHub…" (design 9.3; D261, D302), the repository browser "Clone
from GitHub" loading, listed, filtered and failed, with the 1,000 cap's and single sign-on's lines (9.4;
D301, D338), the keyless palette command "Clone from GitHub…", which signs in first (9.5; D299, D300),
and a clone of the private `octo/hello` chosen in the browser that takes the account's token through
the app's helper and writes none (0.2, 4.6, 9.3; D255, D331, ruling 5): checks B1 to B9, numbered here
in the order of design 9.7's list, which numbers none. It uses WP-A's sign-in dialog and the window's
entry (8.4, 8.5) and WP-C's and WP-S's binding as they are. Its one R picture is B3's `b03a-listed`, in
the state the palette's path reaches (0.5): one empty tab, the form open with empty boxes, the filter
focused and empty; the real window also repeats B7's palette path and B6's clone, comparing no capture
there (13.4 B; what only it proves, 13.1). No picture of part B shows an opened GitHub repository, the
clone included (0.4, 9.7): the clone's result is asserted through git, the fake's records, the call
log, the app's log and the tripwire.

#### Conventions of part B

- **Code** (9.7): `Phase7aChecks.B.cs` (`Phase7aChecksB`, one method per app start, below) and
  `Phase7aChecks.B.Helpers.cs` (part B's helpers, the builder's to name: waits for the browser and its
  load, and snapshots of the browser and of the form holding what the rows assert);
  `Phase7aExpectations.B.cs` (nested `PartB`: 9.2's texts, the two failure texts below, `PartB.Rows`
  (full name and detail, in order), the clone's URL and folder name, the palette query, the picture
  names, the R picture `b03a-listed`, positions and tolerances; written from this report, not from
  `CloneTexts`); `Phase7aExpectationTests.B.cs` (`CloneTexts` and the two `HostingTexts.Describe` texts
  equal `PartB`'s; the picture names unique and matching the rule). Part A's driver helpers and phase
  6's palette helpers (`PressPalette`, `TypeInPalette`, `PaletteSnapshot`) are used as they are.
- **Names in the checks** are part A's, and: "the form" is the clone form of the named tab's welcome
  page (`CloneForm`); "the browser" the open `CloneFromGitHubDialogViewModel` and its view; "rows" its
  `Rows` as its `CloneFromGitHubItem`s draw them; "the log" the app's log file's lines since a mark,
  Debug included (`AppLogFile.ReadLines`). The fake records a request after its answer, so a step that
  asserts requests after a load first waits until `GitHub.Requests` holds the last one it expects.
- **Signing in.** B1, B5 and B9 sign in through the session (`SignInAsync(OctoToken)`), a state their
  steps start from; B3, B6, B7 and B8 through WP-A's dialog that the palette's command opens, typing
  `OctoToken`, as the real window does (13.4 B).
- **The form** (phase 1's checks 13 and 14; 9.3): on an empty tab at 1100×700 the welcome page's
  `CloneForm` lies at x 284 to 676, its texts and controls as phase 1's `13a` shows them; opening it
  focuses `CloneUrlBox` (`WelcomeView.axaml.cs:36-40`). `CloneFromGitHubButton` (named `From GitHub…`,
  no tooltip) is collapsed signed out or needing sign-in (D302) and takes no room; signed in it shows
  whole at the URL row's right end, `VcActionButton`, enabled while no clone runs (`CanEditClone`).
  Positions come from the controls' bounds, ±1.
- **The browser** (9.4; D301): `CloneFromGitHubDialog`, named `Clone from GitHub`, on the dialog layer
  under its backdrop (D104), a `wide` `dialogCard`; top to bottom `CloneFromGitHubTitle` (`dialogTitle`),
  no repository line; `CloneFromGitHubFilter` (`dialogBox`, named `Filter`), enabled in every state;
  `CloneFromGitHubList` (named `Clone from GitHub`) as `PickList`, 58 to 232 high, shown in every state;
  the lines `CloneFromGitHubStatus` (`settingsNote`), `CloneFromGitHubError` (`dialogError`),
  `CloneFromGitHubMore` and `CloneFromGitHubSso` (`settingsNote`), each only with text, in that order
  (open point 18); `CancelButton` (`VcActionButton`) and `ConfirmButton` `Choose` (`VcPrimaryButton`),
  right-aligned. A row (`CloneFromGitHubItem`) has `Padding` 10,0, its detail cut at its end and hidden
  when null. Theme brushes only: the rows give the dark values; the card is `#1A1D24`.
- **The palette's command** (9.5; D299, D300) is listed while GitHub is not off, as at every part B
  start. Signed out it runs the window's sign-in entry first (8.5); a cancelled sign-in stops there; a
  sign-in goes on to the active tab if empty, else to a new tab at the end (5.10's `TabForOpening`), and
  opens its form and the browser over it. Signed in it asks nothing.
- **No token on screen** (as part A's): before every `Shot` of part B, no visible text, automation value
  or name holds `OctoToken`, `OctoClassicToken` or `AdaToken`.
- **Pictures**: the neutral point (760, 687) is the status bar's empty middle under the form, or the
  backdrop below the open card (every card ends well above y 687 at 1100×700, the browser at its
  largest in `b09a` too).
- **Every method ends**, after closing the app, with the tripwire for `OctoToken`, `OctoClassicToken` and
  `AdaToken` over the data folder, the copy's `hello/` and `tool/` and, in method 2, the `clones` folder
  (the clone, `.git` included), with the session's call log; the fake's `Errors` and `RefusedRequests`
  empty; no API request answered 400 or 415; `Launcher.Requests` empty (part B launches nothing).

#### Scenario facts part B uses (2.6, as `Scenarios.Phase7a.cs` builds it)

- **octo's list.** The session's `ListRepositoriesAsync` is WP-G's A2, `GET api/v3/user/repos` with the
  query `per_page=100&sort=updated`, route `user/repos`, at most 10 pages (`GitHubProvider.cs:74-82`),
  answered with the repositories octo owns or may push to, most recently updated first; `PartB.Rows`:
  1. `octo/hello`: private, updated at `HarnessTime`; detail `Private`;
  2. `octo/tool`: public, a fork of `acme/tool`, an hour before, described `Small command-line tools.`
     (ruling 11); detail `Fork · Small command-line tools.`;
  3. `octo/notes`: private, empty, a day before; detail `Private`;
  4. `octo/website`: public, archived, empty, 30 days before; detail `Archived`.

  Not listed: `acme/tool` and `ada/hello`. Only `octo/tool` has a description (ruling 11). Each row's
  clone URL is the fake's `clone_url`, `http://127.0.0.1:47395/<owner>/<name>.git`, with no user part.
- **The long list** (B9 only): `AddListedRepositories("octo", 1001)` before the start adds API-only
  records `octo/listed-0001` to `octo/listed-1001`, public, without flags or description, each a minute
  older than the one before from 60 days before `HarnessTime`: after the four, so the first 1,000 are the
  four and `listed-0001` to `listed-0996`, and the tenth page of 100 names an eleventh (`MoreExist`).
- **Faults on the list** (2.3, 2.4; once, after the header checks, before auth): `SsoPartialResults`, the
  normal answer with `X-GitHub-SSO: partial-results; organizations=1002` (`SomeHiddenBySso`); `ServerError`,
  500 `Server Error`; `BadCredentials`, 401 `Bad credentials`, which marks the account at once (7.6,
  D330). Every answer's `X-GitHub-Request-Id` is `FAKE-` and four digits.

#### Test methods and app starts (part B)

Four methods, four app starts, each from `ServeAsync()` and a fresh data folder; each stays well under a
minute alone (the heaviest git work is method 2's clone of `octo/hello`, seven commits, and its opening).

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_B1_B2_B4_form_loading_and_filter` | B1, B2, B4 | one empty tab (`ShellDriver.Start(data, gitHub: served.GitHub)`), a new store; signed in through the session after the form opens | 1100×700, dark | `b01a`, `b02a`, `b04a` |
| 2 | `Check_B3_B6_B7_palette_sign_in_listed_and_clone` | B7, B3, B6 | one empty tab, a new store, signed out; `clones` (a `TempDirectory`) made before the app | 1100×700, dark | `b07a`, `b07b`, `b03a`, `b06a` |
| 3 | `Check_B5_B9_cap_single_sign_on_and_failures` | B9, B5 | one empty tab, a new store; `served.GitHub.AddListedRepositories("octo", 1001)` before the start; signed in through the session | 1100×700, dark | `b09a`, `b05a`, `b05b` |
| 4 | `Check_B8_cancelled_sign_in_and_a_new_tab` | B8 | one tab on `hello` (`StartWithTabs(data, [served.Hello.Path], activeTab: 0, gitHub: served.GitHub)`), a new store, signed out | 1100×700, dark | none |

#### Texts these checks rely on (part B)

- **Design 9.2's texts**, word for word (`OperationTexts.CloneTexts`): `FromGitHubButton` `From GitHub…`;
  `DialogTitle` `Clone from GitHub`; `PaletteCommand` `Clone from GitHub…`; `Loading` `Loading
  repositories…`; `Empty` `No repositories to list`; `MoreExist` `Only the 1,000 most recently updated
  repositories are listed.`; `SomeHiddenBySso` `Some organisations' repositories are hidden until the token
  is authorised for single sign-on.`; `Unexpected` `The repositories could not be loaded.`; `Choose`
  `Choose`; the flags `Private`, `Fork`, `Archived`, joined with ` · ` before a description (9.4). No check
  shows `Empty` or `Unexpected` (9.6's tests do).
- **The browser's other texts** (9.4): the filter's placeholder and name `Filter`; `Cancel`.
- **`HostingTexts.Describe`** (1.7; D338) for the two failures pictured: `ServerError` `GitHub had a
  problem. Try again later.`; `BadCredentials` with the login `octo`, `OperationTexts.TokenRefused("octo")`
  (D276), `GitHub refused the token of octo. Sign in to GitHub in Settings, Accounts.`
- **Earlier texts**: the form's (part B's conventions, "The form"); the palette's neighbours `Clone
  repository…` and `Init repository…` (phase 6's 5.4); WP-A's dialog, `Add GitHub account` and the rest
  as part A's texts give them.
- **Log lines** (Debug included; `{id}` the request's `X-GitHub-Request-Id`): a failed list logs three
  Warnings, each naming the one request: WP-B's, once (9.4), `Listing GitHub repositories failed: {Kind}:
  {Message} (request {id})`; WP-G's client's (6.2) `GitHub: GET /api/v3/user/repos failed: {status}
  (request {id}).`; and WP-S's (17.1), for `BadCredentials` `GitHub account octo on 127.0.0.1:47395: GitHub
  refused the token (the API, request {id}); it needs signing in again.`, for any other kind `GitHub:
  listing the account's repositories failed: {Kind}, {Message} (request {id})` (`HostingSession.CallAsync`).
  4.6's `hello: Clone: git's credential get for 127.0.0.1:47395 answered from the account.`; 9.3's Warning
  `Cloning {url}: git's configuration could not be read; the clone goes without the account's token.`,
  never expected; WP-S's `hello: listed 4 open pull requests of octo/hello` (D322).

### Part P: pull requests in the left panel

Part P covers the left panel's "Pull requests" section (design 10.3; D251, D303, D304, D308): without a
GitHub remote (today's placeholder), signed out, loading, failed (D338), none, with rows and with more
than 300; the panel's filter; a click on a pull request; a branch row's `#n`; the section's and a pull
request's menus (10.4; D307); "Open pull request #n" and "Create pull request…" in branch and drop menus
(D306); the checkout of a same-repository and of a fork's pull request (10.5; D305); copy link and open
in browser: checks P1 to P15, numbered here in the order of design 10.7's list. It uses WP-A's dialog
(the section's sign-in row runs the window's entry, 8.5) and WP-S's `RepositoryHosting` as built (7.8,
17 item 1). Its R pictures are P6's `p06a-rows` and P11's `p11c-section-menu` (the shared R steps);
the real window also reads P11's pull request menu and repeats P15's open in browser with the
default browser, comparing no capture there (13.4 P, 13.5; D321).

#### Conventions of part P

- **Code** (10.7): `Phase7aChecks.P.cs` (`Phase7aChecksP`, one method per app start, below), `.P.Menus.cs`,
  `.P.Checkout.cs` and `.P.Helpers.cs` (the builder's to name: a snapshot of the section, the header's
  chevron and count and each row's kind, automation id, name, number, title, author, draft, tooltip and
  selection; a row's control by number or message kind; a wait for `PullRequestsListed` since a mark; a
  branch row's `RefPullRequest`; a drag of a branch row onto another, as `Phase3Checks.Support.cs` makes
  it); `Phase7aExpectations.P.cs` (nested `PartP`: 10.2's texts, the rows of `hello` and `tool`, the
  menus' lines, the addresses, the added pull requests' data, the picture names and the R pictures,
  written from this report); `Phase7aExpectationTests.P.cs` (part P's `OperationTexts` and `MenuTexts`
  equal `PartP`'s; the picture names unique and matching the rule).
- **Names in the checks** are part A's and B's, and: "the section" is the active tab's "Pull requests"
  section (`LeftPanel.Rows` from the `RefSectionRow` `Pull requests` to the next header); its "rows" its
  `PullRequestRow`s (`PullRequestItem`, named `#{n} {title}`, called by their number: `#1`), its "message
  row" its `PullRequestMessageRow` (`PullRequestMessage`, named by its text); "`hello`'s hosting" is part
  A's (`State`: `PullRequests`, `Repository`, `List`, `Failure`, `ByRef`); a branch row's "`#n`" its
  `RefPullRequest`.
- **Waiting for a list** (D322): a step that expects a list waits for its tab's `PullRequestsListed` line
  with its count since the step's mark, then for the watchers to be quiet. WP-S writes that line only
  when it applies a list (`RepositoryHosting.Finish`), so a step that expects a state without one (P2
  step 1, P3 step 7, P4 step 5; `p02a`, `p03a`, `p11b`, `p04a`, `p04b`) waits instead for the tab's view
  model's `HostingState.PullRequests` in that state, then the section's message row, then the quiet
  watchers (open point 34): the hosting's own `State` changes before the post whose handler
  (`RepositoryViewModel.OnHostingChanged`) updates the section. From WP-V's merge a picture of
  a signed-in tab also waits for its tab's `AvatarsShown` once since the start (open point 29), and no
  "exactly" of a row counts the avatars' requests (routes `commits:` and `avatar:`, 7.9). A sign-in
  lists every open tab with a GitHub remote (7.8, D289) through the one client, which sends one request
  at a time (D285), so a step that signs in with more than one such tab open waits for each tab's line
  before the next step counts requests (P13 step 1); for a tab in the background, the list line alone,
  since only the shown tab's graph writes `AvatarsShown` (11.3).
- **The section** (10.3): without a GitHub remote phase 1's placeholder (closed, `0`, no toggle, no menu).
  With one, the header toggles and is open at the opening; its count is the rows' matches (`0` beside a
  message row); rows and message rows are 26 high at depth 1 (`Indent` 24 as a branch row's, the 18-wide
  chevron room, 12 free at the right); a row shows `PullRequestNumber` `#n` (secondary), `PullRequestTitle` (cut with "…"),
  at the right end `PullRequestAuthor` (secondary, at most 90 wide) and, left of it on a draft only,
  `PullRequestDraft` `Draft` (secondary; open point 25); a message row its text (secondary, cut with "…").
  Pull requests come newest first, as the fake lists them (number descending). A branch row's `#n` is
  secondary, at the row's right before its counts and toggles; the name is cut first (D308).
- **Menus** are phase 3's (4a, D105, D114): a right-click at the centre of a header or a row opens the
  menu with its top-left 2 right of and below the pointer (moved to lie 4 inside the window), the first
  enabled item highlighted, 8 + 24 + 26 × items + 9 × separators high ±2 (`AssertMenuShape`); asking for
  a menu selects nothing and asks GitHub nothing.
- **Signing in.** P2 and P4 sign in through the section's row, method 2 through the Accounts screen as
  the real window does (open point 30), methods 3 and 4 through the session (`SignInAsync`).
- **No token on screen**, as part A's, for `OctoToken`, `OctoClassicToken` and `AdaToken`.
- **Every method ends**, after closing the app, with the tripwire for those three tokens over the data
  folder, the copy's `hello/` and `tool/` and, in method 1, the `ops` copy, with the session's call log;
  the fake's `Errors` and `RefusedRequests` empty; no API request answered 400 or 415; from WP-V's merge
  no `avatar:` request with a token; `Launcher.Requests` exactly the launches its steps name.

#### Scenario facts part P uses (2.6, as `Scenarios.Phase7a.cs` builds it)

- **`hello/`**: local `main` (HEAD, tracking `origin/main`), `feature/login` (tracking
  `origin/feature/login`, in the folder `feature`), `local-work` (no upstream); `origin`'s `main`,
  `docs/readme`, `feature/login`, `feature/a-very-long-branch-name-that-the-graph-must-cut` ("the long
  branch") and `topic/no-pr`, and `refs/remotes/origin/HEAD`; no tag, no stash. At the opening only the
  sections and `origin` are open.
- **Its list**, octo/hello's open pull requests: #5 `Fix a typo in the greeting` (ada; head `fix-typo` of
  `ada/hello`, label `ada:fix-typo`), #3 `Shorten long labels` (octo, the long branch), #2 `Update the
  readme` (octo, a draft, `docs/readme`), #1 `Add the login form` (ada, `feature/login`); #4 is closed.
  Base `main`; `html_url` `http://127.0.0.1:47395/octo/hello/pull/<n>`. `ByRef`: `refs/heads/feature/login`
  and `refs/remotes/origin/feature/login` → #1, `refs/remotes/origin/docs/readme` → #2, the long branch's
  remote ref → #3. #5's head, `refs/pull/5/head` of the copy's `github/octo/hello.git` (ada's 12:11
  commit), is no ref of `hello/` and no row of its graph; the template's build left its object
  unreachable in `hello/.git`, so no check asserts that P13's fetch transferred it.
- **`tool/`**: `origin` octo/tool (a fork of acme/tool), `upstream` acme/tool; `main` (HEAD), `fix-typo`
  tracking `origin/fix-typo`. It lists acme/tool's: #8 `An old idea` (ada, `idea` of the deleted
  `gone/tool`), #7 `Fix a typo in the readme` (octo, `fix-typo` of octo/tool).
- **`ops`** (method 1): a copy of phase 3's scenario (`Scenarios.OperationsAsync()`, `03e`'s), whose
  `origin` is a folder beside it: no GitHub remote (open point 21).

#### Test methods and app starts (part P)

Four methods, four app starts, each from `ServeAsync()` and a fresh data folder; each stays well under a
minute alone (the heaviest: P7's 297 pull requests, one `update-ref` at the fake and three pages; P13's
fetch and two checkouts).

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_P2_P11_P4_P3_P1_signed_out_failed_loading_and_not_github` | P2, P11, P4, P3, P1 | tabs `hello` and `ops` (`StartWithTabs(data, [served.Hello.Path, ops.Path], activeTab: 0, gitHub: served.GitHub)`), `ops` made before the app; a new store, signed out | 1100×700, dark | `p02a`, `p11a`, `p04a`, `p04b`, `p03a`, `p11b`, `p01a` |
| 2 | `Check_P6_P11_P14_P15_P9_P10_P8_P12_rows_menus_and_selection` | P6, P11, P14, P15, P9, P10, P8, P12 | one tab on `hello` (`OpenAsync(data, served, signIn: false)`); signed in through the Accounts screen | 1100×700, dark | `p06a`, `p11c`, `p11d`, `p09a`, `p09b`, `p10a`, `p08a`, `p08b`, `p12a`, `p12b` |
| 3 | `Check_P13_P6_checkout_and_a_forks_tab` | P13, P6 | tabs `hello` and `tool` (`StartWithTabs(data, [served.Hello.Path, served.Tool.Path], activeTab: 0, gitHub: …)`); signed in through the session once `hello` has loaded | 1100×700, dark | `p13a`, `p06b` |
| 4 | `Check_P7_P5_more_than_300_and_none` | P7, P5 | one tab on `hello`, signed in through the session (`OpenAsync(data, served)`) | 1100×700, dark | `p07a`, `p05a` |

#### Texts these checks rely on (part P)

- **Design 10.2's texts**, word for word (`OperationTexts`; menu labels and reasons in `MenuTexts`):
  `SignInToSeePullRequests` `Sign in to GitHub to see pull requests…`; `LoadingPullRequests` `Loading pull
  requests…`; `NoOpenPullRequests` `No open pull requests`; `MorePullRequests` `More than 300 open pull
  requests`; `PullRequestHeader(n, title)` `#{n} {title}`; `OpenInBrowser` `Open in browser`; `CopyLink`
  `Copy link`; check out `MenuTexts.CheckOutName(head)`, `Check out feature/login`, `Check out
  ada:fix-typo`; `RefreshPullRequests` `Refresh pull requests`; `OpenPullRequestsInBrowser` `Open pull
  requests in browser`; `HostingTexts.SignInToGitHubItem` `Sign in to GitHub…`; `CreatePullRequest`
  `Create pull request…`; `OpenPullRequest(n)` `Open pull request #{n}`; `PullRequestsLoading` `Pull
  requests are loading`; `PullRequestOfBranch(n, title)` `Pull request #{n}: {title}`;
  `FetchPullRequest(n, remote)` `Fetch pull request #{n}` (running `Fetching pull request #{n} from
  {remote}…`); `FetchedPullRequest(n, branch)` `Fetched #{n} into {branch}`. No check shows
  `AddRemoteFor` or `FetchHeadChanged` (10.6's classes do).
- **10.3's**: `Draft`; a row's tooltip `#{n} {title}`, a line break, `{author} · {head label} → {base
  ref}` and, for a draft, ` · Draft` (open point 26), so #5's `#5 Fix a typo in the greeting` and `ada ·
  ada:fix-typo → main`, #3's second line `octo · feature/a-very-long-branch-name-that-the-graph-must-cut →
  main`, #2's `octo · docs/readme → main · Draft`, #1's `ada · feature/login → main`; the failed row's
  tooltip its text, a line break, the failure's `Detail`; the `More` row's `Open pull requests in browser`.
- **`HostingTexts.Describe`** for `Forbidden` (1.7): `GitHub answered: Resource not accessible by personal
  access token.`
- **Earlier texts**: the title `Pull requests` (`LeftPanelViewModel.PullRequestsTitle`);
  `MenuTexts.AlreadyCheckedOut` `{b} is already checked out`; `OperationTexts.CheckedOut` `Checked out
  {b}`; the branch and drop menus' other lines as phases 3 to 6 give them (the rows quote them whole).
- **Log lines**: `hello: listed {n} open pull requests of octo/hello`, `tool: listed 2 open pull requests
  of acme/tool` (D322); a failed list's Warnings, WP-G's `GitHub: GET /api/v3/repos/octo/hello/pulls
  failed: {status} (request {id}).` and WP-S's `GitHub: listing the pull requests of octo/hello failed:
  {Kind}, {Message} (request {id})`.

### Part G: the graph's badge and avatars

Part G covers the graph's pull-request badge (design 11.2; D309): `#n` after the pull request's icon
inside a branch's label, on a local and on a remote branch, a long name cut with the badge kept, and the
tooltip's suffix; and the authors' avatars (11.3; D252, D310, D311): 16-px round images in the graph's
Author column and before the details panel's author, fetched without the token into `avatars/` (6.4,
7.9), an author GitHub does not know as today: checks G1 to G5 of design 11.5's table, and G6 (signed
out, as today) and G7 (the authors' read failed, as today) beyond it (open point 40). It uses WP-P's
section and branch rows (its pictures show them, 0.4) and WP-S's `RepositoryHosting` as built (7.8, 7.9,
17 item 1). Its R picture is G1's `g01a-badges-and-avatar` (13.4 G). What only the real window proves
for it: the real process's own handler fetching the avatars without the token, and their decoding and
drawing at the display's scaling.

#### Conventions of part G

- **Code** (11.5): `Phase7aChecks.G.cs` (`Phase7aChecksG`, one method per app start, method 3's two
  starts excepted, open point 38) and `.G.Helpers.cs` (the builder's helpers); `Phase7aExpectations.G.cs`
  (nested `PartG`: `PrepareAsync` and its data, `Row`, `Author`, `Lanes`, 11.2's tooltip suffixes, the
  labels table below, the picture names and the R picture, written from this report);
  `Phase7aExpectationTests.G.cs` (11.5's pin, with `PartG.Row` 4 and `octo@example.com`; the picture
  names unique and matching the rule).
- **Names in the checks** are parts A's, B's and P's, and: "the graph" is the active tab's
  `CommitGraphControl` (`GraphRows`), "row n" its commit row n as `SelectedIndex` counts (no working
  row); a row's "labels" are `LabelsInRow(n)` (`Text` as drawn, `FullText`, `Kind`, `RefName`,
  `IsOverflow`, `Bounds` in the graph's coordinates, and WP-V's `Badge`, a `PullRequestBadge(Number,
  Title, IsDraft)` or null); "the graph's tooltip" is its `ToolTip.GetTip` once the pointer rests on a
  row's labels; "the avatars" are `hello`'s view model's `Avatars`; "the author line" is `DetailsAuthor`
  with `DetailsAuthorAvatar`, measured from `DetailsDate`'s left, the value column's.
- **Every check on `hello`** calls `PartG.PrepareAsync(served.Hello)` after `ServeAsync()` and before the
  app starts (11.5), so that `feature/login`'s and `origin/feature/login`'s labels sit on rows 0 and 5.
- **Waiting** (D322; open points 34, 39 and 42). A picture of the signed-in `hello` with its list waits
  for `hello: listed 4 open pull requests of octo/hello` and `hello: showing GitHub avatars for 2 authors`
  since the step's mark (also at 1100×700, where the Author column is hidden), then for the watchers to
  be quiet; a picture of the details panel then waits for `DetailsAuthor`'s text and, where the line has
  an image, `DetailsAuthorAvatar` shown (decoded off the UI thread, 11.3; `Shot` waits for it too, open
  point 44). Where no avatar is applied WP-V's line never comes: G7 waits for WP-S's warning of the
  failed read, G5 and G6 for the hosting's state and quiet watchers.
- **The graph's columns.** At 1100×700 the graph is 440 wide and, with `hello`'s 5 lanes, shows Branch /
  Tag (x 0 to 130), Graph (130 to 226) and Message (226 to 440) only: Date and Author give way (D49,
  phase 1's rule), so the graph's avatars are pictured at 1920×1080, where it is 1260 wide with all six
  columns, Author at x 928 to 1068 (open point 35). A label is 18 high at its row's top + 4; a badge
  follows the name: 6, the icon 10 (`IconPullRequest`), 3, then `#n` in the label's typeface, then the
  label's padding 6, all in the label's own text colour (11.2; no new token). Positions are in the
  graph's coordinates, ±1 for a label's edges; a badge's name edge, icon and `#n`, and an author's
  image and name, exactly to one decimal as the control lays them out (since 2026-10-30, "Changed in
  this report after its first commit"); the widths were measured (open point 41).
- **An avatar as drawn** (11.3): 16×16, clipped to a circle, at (Author's x + 8, the row's top + 5), the
  name from Author's x + 30 (today + 8); in the details panel the ellipse 16×16 at the value column's
  left, its top at `DetailsAuthor`'s, the text 22 further; a row or a line without an image exactly as
  today. The fake's images (2.3) are 64×64 squares, octo's (1001) `#2F81F7` and ada's (1003) `#1F883D`,
  each with a white square of 24 in its centre: drawn, a coloured disc with a white centre.
- **Pictures.** The neutral point (760, 687) is the status bar's empty middle at 1100×700 and, at
  1920×1080, the graph's empty area below row 8, its last, where nothing has a tooltip or a
  pointer-over look. `g01a` to `g07a` collide with no name in phase 7a's folder (phase 5's part G
  names, `g22a` to `g25i`, are in phase 5's).
- **No token on screen**, as part A's, for `OctoToken`, `OctoClassicToken` and `AdaToken`.
- **Every method ends** as part P's do (the tripwire over the data folder, both of method 3's, and the
  copy's `hello/` and `tool/`), with no `avatar:` request with a token and `Launcher.Requests` empty.

#### Scenario facts part G uses (2.6 and `PartG.PrepareAsync`, 11.5)

`PrepareAsync` checks out `feature/login`, commits `PartG.PreparedPath` `src/login.txt`
(`PreparedContent` `login form\nremember me\n`, `PreparedMessage` `Remember the login`; open point 37) as
Test Author at 12:12 (TempRepo's clock goes on from the template's last commit, 12:11) and checks out
`main`. The prepared `hello`'s graph has 5 lanes (`PartG.Lanes`) and these rows; its labels signed in
with octo/hello's list (part P's scenario facts), each from x 6, with the right edge measured:

| Row | Subject, time | Author | Labels as drawn, signed in with the list | Today (off, signed out, not GitHub) |
|---|---|---|---|---|
| 0 | `Remember the login`, 12:12 | Test Author | `feature/login` `#1` (to 117.8) | `feature/login` (to 86.4) |
| 1 | `Local change`, 12:10 | Test Author | `local-work` (to 74.6) | the same |
| 2 | `Try an idea`, 12:06 | Test Author | `origin/topic/no-pr` (to 113.3) | the same |
| 3 | `Shorten long labels`, 12:05 | Octo Cat | `origin/featur…` `#3` (to 126.0) | `origin/feature/a-ve…` (to 127.9) |
| 4 | `Update the readme`, 12:04 | Octo Cat | `origin/docs/r…` `#2` (to 128.2) | `origin/docs/readme` (to 123.0) |
| 5 | `Add the login form`, 12:03 | Ada Lovelace | `origin/featur…` `#1` (to 124.1) | `origin/feature/login` (to 121.0) |
| 6 | `Add the docs folder`, 12:02 | Test Author | `main` (check mark, to 56.8), `+1` (`origin/main`, 60.8 to 85.5) | the same |
| 7 | `Add the greeting`, 12:01 | Ada Lovelace | none | none |
| 8 | `Initial commit`, 12:00 | Octo Cat | none | none |

- **Badges**: rows 0 and 5 `PullRequestBadge(1, "Add the login form", false)`, row 3 `(3, "Shorten long
  labels", false)`, row 4 `(2, "Update the readme", true)`; no other label has one, `+1` included;
  `FullText` is the whole name everywhere; `ByRef` as part P's facts give it.
- **The avatars' run** (7.9, 11.3; D311): `origin`'s branches reach rows 2 to 8 (0 and 1 are local
  only), so the set is `author@example.com`, `octo@example.com`, `ada@example.com`, newest first. One
  `GET api/v3/repos/octo/hello/commits`, query `sha=main&per_page=100` (route `commits:octo/hello`), maps
  all three (main's commits by Test Author, Ada and Octo; Test Author's with no linked user), so no
  lookup (`author=`) follows; then `GET avatars/u/1001` and `avatars/u/1003`, query `v=4&s=64`, without
  the token (6.4). `avatars/` then holds exactly `1001.png` and `1003.png` (the fake's bytes),
  `index.json` (users `1001` and `1003`) and `authors/github_127.0.0.1_47395_octo_hello.json` (in e-mail
  order: ada 1003, `author@example.com` without a user, octo 1001; avatar URLs
  `http://127.0.0.1:47395/avatars/u/<id>?v=4`); the avatars map octo's and ada's addresses to the two
  images; WP-V's line counts 2 authors (open point 42).

#### Test methods and app starts (part G)

Four methods, five app starts, each from `ServeAsync()`, `PartG.PrepareAsync` and a fresh data folder;
each stays well under a minute alone (`hello`'s opening, one sign-in, at most seven requests).

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_G1_G2_G3_badges_tooltips_and_details` | G1, G2, G3 (the details panel) | one tab on `hello` (`OpenAsync(data, served, signIn: false)`), a new store; signed in through the Accounts screen, as the real window does | 1100×700, dark | `g01a`, `g01b`, `g02a`, `g03a` |
| 2 | `Check_G6_G3_G4_signed_out_avatars_and_light_at_1920x1080` | G6, G3 (the Author column), G4, G6's sign-out | one tab on `hello` (`OpenAsync(data, served, signIn: false, width: 1920, height: 1080)`), a new store; signed in through the session after G6, signed out through the screen after G4 | 1920×1080, dark, then light by the toolbar's Theme switch | `g06a`, `g03b`, `g04a` |
| 3 | `Check_G5_not_github_as_with_github_off` (a `[Fact]`, two starts on one copy, a data folder each) | G5 | `hello`'s `origin` set to the copy's `github/octo/hello.git` folder; `StartWithTabs(data1, [served.Hello.Path], activeTab: 0)` (GitHub off), then `OpenAsync(data2, served)` | 1100×700, dark | `g05a`, `g05b` |
| 4 | `Check_G7_the_authors_read_fails` | G7 | one tab on `hello`; `GitHub.FailNext("commits:octo/hello", FakeGitHubFault.ServerError)`, then `OpenAsync(data, served)` | 1100×700, dark | `g07a` |

#### Texts these checks rely on (part G)

- **11.2's tooltip**: each label's line its name, followed, when `ByRef` holds its ref, by ` (pull request
  #{n}: {title})`, for a draft ` (draft pull request #{n}: {title})`, also for a label folded into "+N";
  rows without such a ref as today (one name per line). The badge's text: `#` and the number (invariant).
- **11.3's**: `DetailsAuthorAvatar`'s name `Author's avatar`. An avatar has no tooltip (`CellTipAt` is
  unchanged).
- **Earlier texts**: the details panel's (phase 1: the person as `{name} <{email}>`); the section's
  (part P's).
- **Log lines** (Debug included; `{id}` the request's `X-GitHub-Request-Id`): WP-S's `hello: listed 4 open
  pull requests of octo/hello`; WP-V's `hello: showing GitHub avatars for 2 authors`; G7's Warnings,
  WP-G's client's `GitHub: GET /api/v3/repos/octo/hello/commits failed: 500 (request {id}).` and WP-S's
  `GitHub: reading the authors of octo/hello failed: ServerError, Server Error (request {id})`
  (`HostingSession.CallAsync`, 17 item 1).

### Part N: a new pull request

Part N covers "Create pull request…" from `ForLocalBranch`, `ForRemoteBranch` and `ForDrop` (design
10.4, 12.3; D306), signed out signing in first (8.5); the form (12.4; D318) with the head's note (D313),
the Pick, the same branch and an empty title; Create as one Network operation (12.5; D312, D315), with
or without a push, Cancel during the push only (open point 59); a draft; GitHub's 422s, a 403 and a
lost answer (12.6; D316); the created pull request in the list, on the branch row and the graph's label
(D293, D289), and the status bar's `Open in browser` (12.2; D317, ruling 13): N1 to N6 of 12.8's
table, then N7 to N10 (open point 46). It uses WP-P's menus and section, WP-V's badge (0.4), WP-A's
dialog and WP-S's `RepositoryHosting` as built. Its R pictures are N1's `n01a-form` and `n01b-created` (13.4 N); only the
real window proves the real process's POST through its own handler and the form at the display's scaling.

#### Conventions of part N

- **Code** (12.8): `Phase7aChecks.N.cs` (`Phase7aChecksN`, one method per app start) and `.N.Helpers.cs`
  (the builder's: snapshots of the form and of the status bar, waits for `OperationEnded` and for
  `PullRequestsListed` n times since a mark); `Phase7aExpectations.N.cs` (nested `PartN`: `PrepareAsync`,
  12.6's texts by hand, heads, bases, titles, numbers, statuses, menus, picture names and the R pictures,
  from this report); `Phase7aExpectationTests.N.cs` (12.8's pin; `CreatePullRequestTexts` equal
  `PartN`'s; the picture names unique and matching the rule).
- **Names in the checks** are parts A's, B's, P's and G's, and: "the form" is the open
  `CreatePullRequestDialogViewModel` and its view `CreatePullRequestDialog`, its controls by 12.4's ids;
  "the offer" `OperationOfferButton`; "the operation" the create's entry in `hello`'s queue, ended when
  `AppLogFile.OperationEnded("hello", "Create pull request for {head}", …)` has come since the step's
  mark; "the POST" the request of route `create:octo/hello` (`POST api/v3/repos/octo/hello/pulls`), its
  `Body` parsed as JSON; "the fake's #6" the record of number 6 in `GitHub.PullRequests("octo/hello")`.
- **The form**: 12.4's layout, ids, classes and order, named `Create pull request`, under its backdrop
  (D104), no `DialogRepository` (one tab). Asserted (±1 from the bounds): the card 460 wide at x 320 to
  780 (1100×700) and 730 to 1190 (1920×1080), centred vertically, its height read (about 415; 386 as built, see "Changed in this report after its first commit"); `From`
  and `Into` at x 340, their values from x 388; `PullRequestTitle` x 340 to 760, 32 high;
  `PullRequestDescription` 120 high; the buttons right-aligned to x 760. Theme brushes only.
- **Creating** (12.4, 12.5): every control of the form disabled, `Create` reads `Creating…`, Esc
  ignored; `Cancel` disabled but while Create pushes, when it cancels the operation (open point 59);
  the status bar the step's running text, the progress bar indeterminate, `OperationCancelButton`
  (tooltip `Stop git`) shown only while Create pushes (D315), under the backdrop. A failure is shown on
  `PullRequestError`, never in a dialog (D312), the controls enabled again; the status bar then reads
  `{Title} failed` (`VcDangerBrush`) or `{Title} cancelled` (secondary), as phase 3's 4e.
- **After a create** (12.5 step 4; D289, D293): the POST, then two `GET api/v3/repos/octo/hello/pulls`
  (query `state=open&per_page=100`), each `Conditional`: the create's own refresh, answered 200 with the
  new list, and the one after a succeeded network operation, answered 304 (open point 51), each writing
  `hello: listed {n} open pull requests of octo/hello`. A step that pictures it waits for the
  operation's end, both lines since its mark and quiet watchers; no "exactly" counts the avatars'
  requests (part P's rule).
- **Waiting** (D322): every picture of the signed-in `hello` waits as part P's conventions say
  (`AvatarsShown` once since the start, the step's own list lines); N6's signed-out picture waits for
  `SignedOut`, the section's message row and quiet watchers.
- **Pictures.** At 1100×700 the neutral point (760, 687) is the status bar's empty middle, right of the
  offer's button, or the backdrop below the form; at 1920×1080 it lies on the form, so N10 rests the
  pointer at (760, 1000), the backdrop under it (open point 56). No earlier name starts with `n`.
- **No token on screen**, as part A's, for `OctoToken`, `OctoClassicToken` and `AdaToken`.
- **Every method ends** as part P's do (the tripwire over the data folder, `hello/` and `tool/`), with
  `Launcher.Requests` exactly the launches its steps name (N1's and N4's).

#### Scenario facts part N uses (2.6 and `PartN.PrepareAsync`, 12.8)

- **`PartN.PrepareAsync(served.Hello)`** runs `git branch --track topic/no-pr origin/topic/no-pr` after
  `ServeAsync()` and before the app starts, in every method and in N1's R twin (13.4 N): a local
  `topic/no-pr` (folder `topic` of `Local branches`, closed at the opening) at `Try an idea` (Test
  Author, 12:06, one commit on `main`), tracking `origin/topic/no-pr` and level with it; the rest as
  part P's facts give it.
- **The graph**, newest first, rows from 0: 0 `Local change` (`local-work`); 1 `Try an idea`
  (`topic/no-pr`, `origin/topic/no-pr`); 2 `Shorten long labels`; 3 `Update the readme`; 4 `Add the
  login form`; 5 `Add the docs folder` (`main`, `origin/main`); 6 `Add the greeting`; 7 `Initial commit`.
- **On GitHub**: part P's list, #4 (`Try an idea`) closed; the fake numbers a new one 6 (2.3), its page
  `http://127.0.0.1:47395/octo/hello/pull/6`; octo may push to octo/hello. A3, kept from the list's
  read (7.8): not a fork, default branch `main`, so every default Into is `origin/main` · `octo/hello`
  (D318); the Pick's options are `origin`'s five remote branches.
- **The head's place** (12.3 step 3; D313): `topic/no-pr` and `main`, level with upstreams of the same
  name on `origin`, are not pushed; `local-work`, without an upstream, is pushed first to `origin` under
  its own name with `--set-upstream`; `origin/topic/no-pr`, a remote branch, never.

#### Test methods and app starts (part N)

Seven methods, seven app starts, each from `ServeAsync()`, `PartN.PrepareAsync` and a fresh data folder;
each stays well under a minute alone (the heaviest: method 2's push and two creates).

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_N1_from_a_branch_and_the_offer` | N1 | one tab on `hello` (`OpenAsync(data, served, signIn: false)`), a new store; signed in through the Accounts screen, as the real window does | 1100×700, dark | `n01a`, `n01b` |
| 2 | `Check_N2_N7_N3_not_on_github_cancel_and_draft` | N2, N7, N3 | one tab on `hello`, signed in through the session (`OpenAsync(data, served)`) | 1100×700, dark | `n02a`, `n07a`, `n07b`, `n02b`, `n03a` |
| 3 | `Check_N4_N8_already_open_the_form_and_no_commits` | N4, N8 | as method 2; `AddPullRequest` on the fake once the list is read | 1100×700, dark | `n04a`, `n08a`, `n08b`, `n08c`, `n04b` |
| 4 | `Check_N5_remote_branch_and_drop` | N5 | as method 2 | 1100×700, dark | `n05a`, `n05b`, `n05c` |
| 5 | `Check_N6_signed_out_signs_in_first` | N6 | one tab on `hello` (`OpenAsync(data, served, signIn: false)`), a new store, signed out | 1100×700, dark | `n06a`, `n06b` |
| 6 | `Check_N9_refused_and_a_lost_answer` | N9 | as method 2; `FailNext` in the steps | 1100×700, dark | `n09a` |
| 7 | `Check_N10_the_form_at_1920x1080_and_light` | N10 | as method 2 (`OpenAsync(data, served, width: 1920, height: 1080)`) | 1920×1080, dark, then light by the toolbar's Theme switch | `n10a`, `n10b` |

#### Texts these checks rely on (part N)

- **Design 12.6's texts**, word for word (`OperationTexts.CreatePullRequestTexts`): `DialogTitle` `Create
  pull request`; `From`, `Into`; `ReadingGitHub` `Reading GitHub…`; `TitlePlaceholder` `Title`,
  `DescriptionPlaceholder` `Description`, `Draft` `Draft`; `Create` `Create`, `Creating` `Creating…`;
  `ChooseBaseTitle` `Choose the branch to merge into`; the reasons `ChooseBase` `Choose the branch to
  merge into.` and `EnterTitle` `Enter a title.`; `OpenExisting(n)` `Open pull request #{n}`;
  `OpenInBrowser` `Open in browser`; `Operation(head)`: title `Create pull request for {head}`, running
  text `Creating pull request…`, error title `Create pull request failed` and summary `GitHub could not
  create the pull request.` (pinned only: the form shows no error dialog, D312); `CreatingPullRequest`
  `Creating pull request…`; `Created(n, draft)` `Created pull request #{n}` and `Created draft pull
  request #{n}`. In the checks' cases: `PushedFirstWithUpstream` `Not on GitHub yet: Create pushes it to
  origin/local-work first and sets that as its upstream.`; `SameBranch` `Choose a branch other than main
  to merge into.`; `Refused` `GitHub refused to create a pull request in octo/hello with this token.`
  and, with `Detail`, ` It needs the permission pull_requests=write.`; `Cancelled` `Cancelled before the
  pull request was created.`; `AlreadyOpen` `Pull request #6 is already open from topic/no-pr into
  origin/main.`; `NoCommits` `origin/topic/no-pr already has every commit of main: there is nothing to
  merge.` 12.6's other texts are 12.7's classes'.
- **The status bar** (12.2, D317; phase 3's 4e): the offer's text and name `Open in browser`; Cancel's
  tooltip `Stop git`; the push's running text `Pushing local-work to origin/local-work…`
  (`OperationTexts.Push`); `{Title} failed`, `{Title} cancelled`; the toolbar's Fetch's `Fetched from all
  remotes`.
- **Earlier texts**: the menus' lines as P12 gives them; the Pick's `Choose` and `Cancel` and the form's
  `Choose…` (`SettingsTexts.PickConfirm`, `SettingsTexts.Choose`); part A's dialog (N6).
- **Log lines** (Debug included; `{id}` the request's `X-GitHub-Request-Id`): `hello: listed {n} open pull
  requests of octo/hello`; `hello: showing GitHub avatars for 2 authors`; the operation's end, `hello:
  {Title}: {Outcome} after {n} ms: {message}` (phase 3's 1.7); WP-N's Info line (12.5, open point 53); a
  refused POST's Warnings, WP-G's `GitHub: POST /api/v3/repos/octo/hello/pulls failed: {status} (request
  {id}).` and WP-S's `GitHub: creating a pull request on octo/hello failed: {Kind}, {Message} (request
  {id})` (`HostingSession.CallAsync`).

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). Each check is named by its part's letter
and its number: A and its number in design 8.7, B and its place in design 9.7's list (B1 to B9, numbered
here), P and its place in design 10.7's list (P1 to P15, numbered here), G and its number in design
11.5's table (G1 to G5), then G6 and G7 beyond it (open point 40), N and its number in design 12.8's
table (N1 to N6), then N7 to N10 beyond it (open point 46); its scenario, theme and window size
end its "Check" column; its methods and starts are in its part's table above. "R" in Notes marks the
steps the real-window pass repeats (design 13.4 A, B, P, G and N, and 13.5), "n/a" says why a check is not
repeated. "Screenshots" lists the planned pictures, the scripted run's and then the real window's. Every
step's "requests" are those the fake recorded since the step's mark; every "calls" are the store's `Calls`
since its mark; "git calls" are the call log's, since a quiet mark (part A's "Names in the checks", which
parts B, P, G and N add to). In part A's rows and methods table, "part A" and "part B" of A5
and A10 name the two halves of those checks (A5 at 1100×700 and at 1920×1080; A10's starts after a
session-only sign-in and after one with the keychain), not the report's parts A and B; part G's G3 is
named by its halves' places, "the details panel" and "the Author column".

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
| B1 | The clone form signed in, with "From GitHub…" (design 9.3, 9.2; D261, D302): signed out the form as today, no button and the URL box at its full width; after a sign-in the button at the URL row's end and the box narrower; "hosting" served, one empty tab, dark, 1100×700 | **Method 1** (`Check_B1_B2_B4_form_loading_and_filter`): 1. `ServeAsync()`. `ShellDriver.Start(data, gitHub: served.GitHub)` (one empty tab, a new store); wait for the welcome page. Mark the requests and the calls. Click `CloneRepoButton`; wait until `CloneUrlBox` has the keyboard focus. 2. `SignInAsync(OctoToken)`; wait until `CloneFromGitHubButton` is visible. Screenshot (`b01a-form-signed-in`). | **After step 1:** the form open (part B's conventions, "The form"), its three boxes empty, the focus in `CloneUrlBox`, no error line; `CloneFromGitHubButton` not visible (signed out, D261) and taking no room: `CloneUrlBox` from x 284 to 676, the edges of `CloneNameBox`; the session's `IsOff` false and its `Account` null; requests none (D257); calls none.<br>**After step 2:** requests since the mark exactly one, `GET api/v3/user`, `TokenPresent`, 200, `Login` `octo`; calls `probe`, `save github:127.0.0.1:47395:octo`; the session's `Account` `SignedIn` with `UseForGit` true, its `BindsGit` true. The form: `CloneFromGitHubButton` `From GitHub…` visible and enabled (`VcActionButton`), on the URL row right of `CloneUrlBox`, 6 apart; its right edge 676 and its height `CloneBrowseButton`'s; `CloneUrlBox` still from x 284, ending 6 left of the button; the URL row as tall as the parent-folder row; no tooltip on the button; the boxes still empty, the focus still in `CloneUrlBox`; the page's other texts and controls as phase 1's `13a` shows them; no text cut. | | n/a | `b01a-form-signed-in` | n/a: the real window reaches the form through the palette with the browser over it (13.4 B), so the button shows only under the backdrop of `b03a`. The step tests the button's appearance on `AccountChanged`; its absence with GitHub off and after a sign-out, and its being disabled while a clone runs, are `WelcomeCloneFromGitHubTests`' (9.6). Varies by nature: the URL box's caret; the status bar's git box (D248). |
| B2 | The browser loading (design 9.4, 9.2; D301): stacked over the form with its title, the filter enabled with the focus, the list empty and `Loading repositories…`, "Choose" disabled; "hosting", one empty tab, signed in, dark, 1100×700 | **Method 1**, after B1: 3. Mark the requests. `GitHub.HoldNext("user/repos")`. Click `CloneFromGitHubButton`; wait for the hold's `Arrived` and for the browser. Screenshot (`b02a-loading`). | **After step 3:** the browser (part B's conventions, "The browser") over the form: the card 460 wide at x 320 to 780, centred vertically; `CloneFromGitHubTitle` `Clone from GitHub`; no repository line; the filter empty, its placeholder `Filter` showing, enabled, with the keyboard focus; the list empty at its least height, 58 inside its border; below it `CloneFromGitHubStatus` `Loading repositories…` (`settingsNote`, `#9BA3B0`, 11.5); no error, more or SSO line; `Cancel` enabled, `Choose` disabled. The hold's `Arrived` completed: one `GET api/v3/user/repos` waits at the fake, not yet recorded (requests since the mark none). Calls none since step 2 (a list reads no keychain). Behind the backdrop the form as `b01a` shows it. No text cut. | | n/a | `b02a-loading` | n/a: a held request (0.5). Varies by nature: the filter's caret; the status bar's git box. |
| B3 | The browser listed (design 9.4, 9.2, 0.5; D301), in the state the palette's path reaches: one empty tab, the form open with empty boxes, the scenario's four repositories in GitHub's order with their flags, the filter focused and empty, "Choose" disabled until a row is chosen; "hosting", one empty tab, signed in through the palette's dialog, dark, 1100×700 | **Method 2** (`Check_B3_B6_B7_palette_sign_in_listed_and_clone`), after B7's steps 1 to 4: 5. Wait for the browser, until its load has ended and until the fake has recorded the `user/repos` request. Screenshot (`b03a-listed`). | **After step 5:** the browser over the one empty tab's form, the card 460 wide at x 320 to 780, centred vertically; `CloneFromGitHubTitle` `Clone from GitHub`; no repository line; the filter empty, its placeholder showing, enabled, with the keyboard focus (not `CloneUrlBox`, which the form's opening focused first); the rows in order, `PartB.Rows`: `octo/hello` `Private`, `octo/tool` `Fork · Small command-line tools.`, `octo/notes` `Private`, `octo/website` `Archived`; each `CloneFromGitHubItem` named by its full name, its help text its detail, its tooltip the full name, a line break and the detail; each 28 high, the full name in `#E4E7EC`, the detail 12 after it in `#9BA3B0` at 11.5, whole; four rows and no scroll bar; no status, error, more or SSO line; none selected; `Cancel` enabled, `Choose` disabled. Requests since step 1's mark: the sign-in's `GET api/v3/user` (200), then exactly one `GET api/v3/user/repos` with the query `per_page=100&sort=updated`, `TokenPresent`, 200, `Login` `octo`. Behind the backdrop the welcome page of the one tab (`New tab`) with the form open, its three boxes empty and `From GitHub…` visible; no settings screen. No text cut; no visible text or automation value holds a token. | | | `b03a-listed`; real window: `b03a-listed` | **R** (13.4 B step 3): after B7's real path, wait for `CloneFromGitHubDialog`, then until `CloneBrowserRowNames()` equals `PartB.Rows`' full names in order and `CloneFromGitHubFilter` has the focus; compared with `b03a-listed`. Its state owes nothing to a fake's hook: the start, the palette, the typed token and the scenario only (0.5). The form's opening posts the focus to `CloneUrlBox` (`WelcomeView.axaml.cs:36-40`, at `Loaded`) while the command opens the browser over it (9.5); the filter's focus is the R state 9.7 names, in both harnesses. Its described row is ruling 11's. Varies by nature: the filter's caret; the status bar's git box. |
| B4 | The browser filtered (design 9.4; D301, D300): a filter typed while loading applies when the rows come; it matches the detail's flag, ignoring case; a selected row it hides is no longer selected; emptied, the rows come back in the list's order; Esc leaves the form open; "hosting", one empty tab, signed in, dark, 1100×700 | **Method 1**, after B2: 4. With the focus in the filter, `app.Type("private")`. 5. Dispose the hold; wait until the browser's load has ended and until the fake has recorded the `user/repos` request. Screenshot (`b04a-filtered`). 6. Click the row `octo/notes`. Click the filter; press Ctrl+A; `app.Type("tool")`. 7. Press Ctrl+A, then Backspace. 8. Press Esc. | **After step 4:** the filter holds `private`; still `Loading repositories…`, no rows, `Choose` disabled.<br>**After step 5:** requests since step 3's mark exactly one, `GET api/v3/user/repos` with the query `per_page=100&sort=updated`, `TokenPresent`, 200, `Login` `octo`. The rows in order: `octo/hello` `Private`, `octo/notes` `Private` (of the four, the two whose detail holds the text); no status, error, more or SSO line; none selected, `Choose` disabled; the list at its least height, 58; the filter still focused, holding `private`. No text cut.<br>**After step 6:** after the click `octo/notes` selected (`#2A3150`), `Choose` enabled; after `tool` the rows exactly `octo/tool` `Fork · Small command-line tools.`, none selected (`Selected` null), `Choose` disabled.<br>**After step 7:** the filter empty, its placeholder showing; the four rows of `PartB.Rows` in their order; none selected; `Choose` disabled.<br>**After step 8:** the browser closed; the form open, its three boxes empty (nothing chosen), `From GitHub…` visible; requests since step 3's mark still that one; calls none. | | n/a | `b04a-filtered` | n/a: scripted only (13.4 B repeats the list, not the filter), and its state follows a held request (0.5). The filter on a description is `CloneFromGitHubDialogViewModelTests`' (9.6). Varies by nature: the filter's caret; the status bar's git box. |
| B5 | The browser failed (design 9.4, 1.7, 7.6; D338, D276, D302, D330): a server error in `HostingTexts.Describe`'s words, no rows, the account kept; then the API's refusal, `GitHub refused the token of octo. …`, the account needing sign-in at once and "From GitHub…" gone from the form; "hosting" with B9's long list, one empty tab, signed in, dark, 1100×700 | **Method 3** (`Check_B5_B9_cap_single_sign_on_and_failures`), after B9: 4. Mark the requests and the log. `GitHub.FailNext("user/repos", FakeGitHubFault.ServerError)`. Click `CloneFromGitHubButton`; wait until `CloneFromGitHubError` shows and the fake has recorded the request. Screenshot (`b05a-failed`). 5. Press Esc. Mark the requests, the calls and the log. `GitHub.FailNext("user/repos", FakeGitHubFault.BadCredentials)`. Click `CloneFromGitHubButton`; wait until `CloneFromGitHubError` shows, for `WaitForAccountAsync(a => a?.State == AccountState.NeedsSignIn)`, until the parsed `accounts.json` shows `tokenState` `refused` (part A's conventions) and until `CloneFromGitHubButton` is not visible. Screenshot (`b05b-refused`). 6. Press Esc. | **After step 4:** requests since the mark exactly one, `GET api/v3/user/repos`, `TokenPresent`, 500; `CloneFromGitHubError` `GitHub had a problem. Try again later.` (`dialogError`, `#E5606B`) below the list, which is empty at 58; no status, more or SSO line; the filter enabled with the focus; `Cancel` enabled, `Choose` disabled. The session's `Account` still `SignedIn` (only a 401 marks it); calls none. The log since the mark, `{id}` the failed request's id (`FAKE-` and four digits): exactly one line that begins `Listing GitHub repositories failed:` (WP-B's; 9.4 logs a failure once), the Warning `Listing GitHub repositories failed: ServerError: Server Error (request {id})`; besides it, once each, WP-G's client's Warning `GitHub: GET /api/v3/user/repos failed: 500 (request {id}).` and WP-S's Warning `GitHub: listing the account's repositories failed: ServerError, Server Error (request {id})` (part B's texts, "Log lines"). Behind the backdrop the form with `From GitHub…`. No text cut.<br>**After step 5:** requests since the mark exactly one, `GET api/v3/user/repos`, `TokenPresent`, 401, and no `GET api/v3/user` after it (the API's 401 marks the account at once, D330); `CloneFromGitHubError` `GitHub refused the token of octo. Sign in to GitHub in Settings, Accounts.`; no rows. The session's `Account` `NeedsSignIn`, note kind `Refused`, login `octo`; `accounts.json` `tokenState` `refused`, `useForGit` true, the record otherwise unchanged; calls none (the entry stays: `Holds(…octo)` true). The log since the mark, `{id}` the refused request's id: exactly one line that begins `Listing GitHub repositories failed:`, the Warning `Listing GitHub repositories failed: BadCredentials: Bad credentials (request {id})`; besides it, once each, WP-G's client's Warning `GitHub: GET /api/v3/user/repos failed: 401 (request {id}).` and WP-S's Warning `GitHub account octo on 127.0.0.1:47395: GitHub refused the token (the API, request {id}); it needs signing in again.`; no line `GitHub: listing the account's repositories failed:` (the 401 marks the account instead, 7.6). Behind the backdrop the form without `From GitHub…` (D302): `CloneUrlBox` again from x 284 to 676. No text cut.<br>**After step 6:** the browser closed; the form open, its boxes empty, no `From GitHub…`; requests none since step 5's. | | n/a | `b05a-failed`, `b05b-refused` | n/a: faults (`FailNext`, 0.5). `ServerError` is the failure pictured for 9.7's "failed", `BadCredentials` a step beyond 9.7's list (open points 12, 13 and 15); the other kinds, `Unexpected` and `Empty` are `HostingTextsDescribeTests`' and `CloneFromGitHubDialogViewModelTests`' (9.6). B9's long list does not show: each fault answers the first page. Varies by nature: the filter's caret; the status bar's git box. |
| B6 | Choose, then Clone of the private `octo/hello` through the app's helper (design 9.3, 9.4, 9.5, 0.2, 4.6; D255, D331, ruling 5; O4, R3): the plain URL and the folder name filled; the clone asks nothing, takes the account's token as `x-access-token` through the app's helper and writes no credential into the clone; the token in no file and no git call; no picture of the clone (0.4); "hosting", one empty tab, signed in through the palette's dialog, a `clones` folder, dark, 1100×700 | **Method 2**, after B3: 6. Mark the requests and take a quiet call mark (`QuietCallMarkAsync()`). Click the row `octo/hello`; click `Choose`; wait until the browser has closed. Screenshot (`b06a-chosen`). 7. Click `CloneParentBox`; `app.Type(clones.Path)` (typed, as the real window does, not through the fake folder dialog). 8. Mark the requests, the git requests, the calls and the log, and take a quiet call mark (`QuietCallMarkAsync()`). Click `CloneStartButton`. Wait until the tab shows the repository (`WaitForRepositoryAsync()`), for `hello: listed 4 open pull requests of octo/hello` in the log since the mark (from WP-V's merge also `AvatarsShown("hello")`, D322) and for the watchers to be quiet. 9. Close the app. In `{clones}/hello`, through `GitRunner` with `TempRepo.IsolatedEnvironment` (as 13.4 B step 7): `git config --get remote.origin.url`; `git config --get-regexp ^credential`; `git config --local --list`; `git symbolic-ref HEAD`; `git rev-parse HEAD`; `git status --porcelain`. | **After step 6:** after the click `octo/hello` selected and `Choose` enabled; then the browser closed and the form: `CloneUrlBox` `http://127.0.0.1:47395/octo/hello.git` (the plain clone URL, ruling 5; `WithPort`), shown whole; `CloneNameBox` `hello` (from the URL, 9.3); `CloneParentBox` empty; no error line; `From GitHub…` visible and enabled; requests since the mark none; git calls since the quiet mark none (choosing runs no git). No text cut.<br>**After step 8:** at no time a credentials dialog or any other prompt: the log since the mark has no line `hello: Clone: git asks (`, and no dialog is open. Git calls since the quiet mark, in this order (presence and order, `AssertCalledInOrder`): `git config --show-scope --show-origin -z --list` in the data folder (9.3's read of the user's `url.` rules, D331), then `git clone --progress -- http://127.0.0.1:47395/octo/hello.git hello` in `{clones}`. The log since the mark holds `hello: Clone: git's credential get for 127.0.0.1:47395 answered from the account.`; no `hello: Clone: git's credential` line ending `is not bound.`, `names another user.` or `has no token.`; no `refused the account's token`; no Warning `Cloning …: git's configuration could not be read; …`. Git requests on `octo/hello` since the mark (paths under `/octo/hello.git/`): the first without credentials, 401 (2.2); at least one with `GivenUserName` `x-access-token` answered 200 (the account's token through the app's helper, D255); none with another user name; none with credentials answered 401. API requests since the mark: no `user` and no `user/repos`; `hello`'s refresh (7.8), `GET api/v3/repos/octo/hello` and `GET api/v3/repos/octo/hello/pulls`, each `TokenPresent`, 200 (from WP-V's merge the avatars' too); none answered 401. Calls since the mark none (the token is in memory, ruling 6). The window: one tab, `hello`, showing `{clones}/hello` (the empty tab took the clone); the form closed; the recent list holds `hello`; `hello`'s hosting's `Bindings` exactly one `CredentialBinding`, `Url` `http://127.0.0.1:47395/octo/hello.git`, account `github:127.0.0.1:47395:octo`; the session's `Account` `SignedIn`, `Keychain`, `None`.<br>**After step 9:** `remote.origin.url` `http://127.0.0.1:47395/octo/hello.git` (step 6's); `--get-regexp ^credential` exits 1 and prints nothing; the keys of `--local --list` other than `core.*` exactly `remote.origin.url`, `remote.origin.fetch` (`+refs/heads/*:refs/remotes/origin/*`), `branch.main.remote` (`origin`) and `branch.main.merge` (`refs/heads/main`): nothing of the scope's `GIT_CONFIG_COUNT` pairs, no `http.` or `url.` key (9.5's git fact); `HEAD` `refs/heads/main`, at the commit of `hello/`'s `origin/main` ("Add the docs folder"); the working tree clean. `accounts.json` one record, `tokenState` `keychain`, `useForGit` true. Part B's end (its conventions): the tripwire passes over `{clones}` with `.git`, the data folder (the log and `recent.json` included) and the call log (the clone's command, output and error included). | | | `b06a-chosen`; real window: none (no capture of the clone's tab) | **R** (13.4 B steps 4 to 7): a real click on `octo/hello`, then "Choose": `CloneUrlBox` `WithPort("http://127.0.0.1:47395/octo/hello.git")`, `CloneNameBox` `hello`; `ReplaceText("CloneParentBox", clones.Path)` (a `TempDirectory("rw7a-clone")` made before the app); a real click on "Clone"; `Opened(app, "hello", 0, 0)` within `NetworkTimeout` (a time-out says whether `CredentialsDialog` shows); no `CredentialsDialog`; `octo/hello`'s git requests include one answered 200 whose `GivenUserName` is `x-access-token` and none with another user name; run.txt `B: the clone went through {HelperPath}` and whether that path holds a space; after `app.Close()` the two `git config` reads; `AssertCleanEnd(app, served, clones.Path)`. The real window waits only for `Opened` before closing; the scripted check also waits for D322's lines, so that the close cuts no request of the clone's tab. `b06a` is scripted only; the temporary folder's name is in no picture (step 7 comes after it). Where the focus returns when the browser closes is not asserted (D104). Varies by nature: a caret where the focus returned; the status bar's git box. |
| B7 | The palette's "Clone from GitHub…" signed out (design 9.5, 8.5, 9.2; D299, D300, D261): listed right after "Clone repository…", without a key; "Add GitHub account" first, over the welcome page, the form not yet open; after the sign-in the form and the browser on the same empty tab, and no settings screen; "hosting", one empty tab, signed out, dark, 1100×700 | **Method 2**: 1. `ServeAsync()`; `using var clones = new TempDirectory("clones")` before the app. `ShellDriver.Start(data, gitHub: served.GitHub)` (one empty tab, a new store); wait for the welcome page. Mark the requests and the calls. 2. `PressPalette()`; with the box empty read the palette's shown entries (`Palette.Shown`, not only the realised rows). `TypeInPalette("Clone from GitHub")`; read `PaletteSnapshot()`. Screenshot (`b07a-palette-command`). 3. Press Enter; wait for `SignInDialog` with the focus in `SignInToken`. Screenshot (`b07b-sign-in-first`). 4. `TypeToken(OctoToken)`; `ConfirmSignInAsync()`. | **After step 2:** with the box empty the palette's commands hold `Clone repository…`, `Clone from GitHub…`, `Init repository…` one after another, `Clone from GitHub…` enabled, with no detail and no key; after the typing one row, `Clone from GitHub…`, highlighted, enabled, no key; the card as phase 6's (`paletteCard`: 600 wide at the top centre, x 250 to 850, from y 96, 84 high with one row). Requests none; calls none.<br>**After step 3:** the palette closed; `SignInDialog` `Add GitHub account` as part A's conventions ("The dialog") and A2's step 3 give it (the help, the link, `Token`, the box empty with the focus, no `SignInNoKeychain`, `Cancel` enabled, `Sign in` disabled), over the empty tab's welcome page, which shows its `Open`, `Clone` and `Init` buttons, not the form (it opens after the sign-in, 9.5); one tab; no settings screen (the entry is the window's, 8.5); calls `probe`; requests none.<br>**After step 4:** the dialog closed; requests since the mark begin with exactly one `GET api/v3/user`, `TokenPresent`, 200, `Login` `octo`; calls `probe`, `save github:127.0.0.1:47395:octo`; the session's `Account` `SignedIn`, `Keychain`, `None`; `accounts.json` one record, `tokenState` `keychain`, `useForGit` true; still one tab (the form and the browser over it, which open after the sign-in, are B3's step 5's, which waits for them); no settings screen at any time. | | | `b07a-palette-command`, `b07b-sign-in-first`; real window: none (its steps 1 and 2 compare nothing) | **R** (13.4 B steps 1 and 2): a real Ctrl+Shift+P, `TypeInPalette("Clone from GitHub", …)`, the highlighted row `Clone from GitHub…`, Enter; `WaitForSignInDialog`, then `TypeTokenAndConfirm` (the box's value "" and its name `Token`); Settings stays closed, so B3's wait for the browser is the sign-in's proof. Varies by nature: the palette box's caret (`b07a`), the token box's caret (`b07b`); the status bar's git box. |
| B8 | A cancelled sign-in changes nothing (design 9.5, 8.5; D300): from a repository tab, "Clone from GitHub…" signed out, then the dialog's Cancel, leaves no account, no new tab and no form; signed in from there, the form opens on a new tab at the end with the browser over it, and Esc on the browser leaves the form open; "hosting", one tab on `hello`, signed out, dark, 1100×700 | **Method 4** (`Check_B8_cancelled_sign_in_and_a_new_tab`): 1. `ServeAsync()`. `StartWithTabs(data, [served.Hello.Path], activeTab: 0, gitHub: served.GitHub)`; `WaitForRepositoryAsync()`; `WaitForWatchersQuietAsync()`. Mark the requests and the calls. 2. `PressPalette()`; `TypeInPalette("Clone from GitHub")`; read `PaletteSnapshot()`; press Enter; wait for `SignInDialog`. 3. Click the dialog's `Cancel`. 4. Mark the requests, the calls and the log. `PressPalette()`; `TypeInPalette("Clone from GitHub")`; press Enter; wait for `SignInDialog`; `TypeToken(OctoToken)`; press Enter; wait for the browser and until its load has ended; wait for `hello: listed 4 open pull requests of octo/hello` in the log since the mark (from WP-V's merge also until the fake has recorded `avatar:1001` and `avatar:1003` since the mark and `hello`'s view model's `Avatars` maps octo's and ada's addresses, open point 43) and for the watchers to be quiet (`WaitForWatchersQuietAsync()`); then until the fake has recorded the `user/repos` request and `GET api/v3/repos/octo/hello/pulls` (part B's "Names in the checks"). 5. Press Esc. | **After step 1:** one tab, `hello`; the session's `Account` null; requests none (D257).<br>**After step 2:** before Enter, the palette's first row `Clone from GitHub…`, highlighted; then `SignInDialog` over `hello`'s window; calls `probe`; requests none.<br>**After step 3:** the dialog closed and no dialog open; the session's `Account` null; no `accounts.json`; the tabs exactly one, `hello`, active and showing `hello` (no new tab, no welcome page, no form; D300); requests none; calls since step 1's mark exactly `probe`.<br>**After step 4:** requests since the mark: first `GET api/v3/user`, `TokenPresent`, 200, `Login` `octo`; then, in an order not asserted, exactly one `GET api/v3/user/repos` (200) and `hello`'s refresh, `GET api/v3/repos/octo/hello` and `GET api/v3/repos/octo/hello/pulls` (200; from WP-V's merge the avatars' requests too); none answered 401; calls exactly `save github:127.0.0.1:47395:octo` (the start's one probe was step 2's). Two tabs: `hello`, then a new tab at the end, active and empty (5.10's `TabForOpening`, where "Clone repository…" opens its form), its form open with its three boxes empty and `From GitHub…` visible, the browser over it with `PartB.Rows` in order and the filter focused and empty; `hello`'s tab still on the copy's `hello/`.<br>**After step 5:** the browser closed; the new tab's form still open (D300), its boxes empty; still two tabs, the new one active; no request since step 4's; calls none since. | | n/a | none | n/a: 13.4 B has no such step, and a cancelled dialog adds no screen to the real window. No picture (open point 16): the cancel is tested from `hello`'s tab, where `TabForOpening` would have opened a new tab, and a picture there shows an opened GitHub repository (0.4, 9.7); after the sign-in the new tab shows what `b03a` shows but for the tab strip. `PaletteCloneFromGitHubTests` (9.6) covers the window's entry answering false and true on an empty active tab, with a stub. |
| B9 | The 1,000 cap and single sign-on's line (design 9.4, 9.2, 6.3; D301, ruling 14): ten pages read, the first 1,000 repositories in GitHub's order, the list at its largest with a scroll bar, and under it `Only the 1,000 most recently updated repositories are listed.` and `Some organisations' repositories are hidden until the token is authorised for single sign-on.`; "hosting" with 1,001 more repositories of octo, one empty tab, signed in, dark, 1100×700 | **Method 3**: 1. `ServeAsync()`; `served.GitHub.AddListedRepositories("octo", 1001)`. `ShellDriver.Start(data, gitHub: served.GitHub)` (one empty tab, a new store); `SignInAsync(OctoToken)`. Click `CloneRepoButton`; wait until `CloneUrlBox` has the focus and `CloneFromGitHubButton` shows. 2. Mark the requests. `GitHub.FailNext("user/repos", FakeGitHubFault.SsoPartialResults)`. Click `CloneFromGitHubButton`; wait for the browser, until its load has ended and until the fake has recorded ten `user/repos` requests. Screenshot (`b09a-cap-and-sso`). 3. Press Esc. | **After step 2:** requests since the mark exactly ten `GET api/v3/user/repos`, each `TokenPresent`, 200, `Login` `octo`: the first with the query `per_page=100&sort=updated`, then pages 2 to 10 as the `Link` headers name them (`page=2` to `page=10`, the other parameters kept); no eleventh (the provider stops at ten pages, 6.3). The rows: 1,000 (`Rows.Count`), the first four `PartB.Rows`, then `octo/listed-0001`, `octo/listed-0002`, … `octo/listed-0996` in that order, these without a detail; the list 232 high inside its border, scrolled to its top, showing 8 whole rows (`octo/hello` to `octo/listed-0004`) and a vertical scroll bar, no horizontal one; below it `CloneFromGitHubMore` `Only the 1,000 most recently updated repositories are listed.` and under it `CloneFromGitHubSso` `Some organisations' repositories are hidden until the token is authorised for single sign-on.`, both `settingsNote` (`#9BA3B0`, 11.5) and whole, wrapping where they need; no status or error line; none selected, `Choose` disabled; the card still 460 wide at x 320 to 780, centred vertically. No text cut.<br>**After step 3:** the browser closed; the form open, its boxes empty, `From GitHub…` visible. | | n/a | `b09a-cap-and-sso` | n/a: needs `AddListedRepositories` and a fault (0.5); the real window lists only the scenario. Pictured, as 9.7 allows (open point 14); the texts are also `CloneFromGitHubDialogViewModelTests`' and the paging `FakeGitHubTests`' (2.9). Varies by nature: the filter's caret; the status bar's git box. |
| P1 | The section without a GitHub remote, signed in (design 0.2, 10.3, 0.7; D251, D307): a repository whose remotes GitHub does not have keeps phase 1's placeholder exactly, closed with `0`, no toggle and no menu (`03e`'s state), no pull-request item in its branch menus (D306), and no request names it; "hosting" served beside phase 3's `ops`, tabs `hello` and `ops`, signed in, dark, 1100×700 | **Method 1**, after P3: 10. Mark the requests and the log. Click the tab `ops`; `WaitForRepositoryAsync()`; `WaitForWatchersQuietAsync()`. 11. Mark the log; right-click the header `Pull requests` at its centre; `AssertNoMenuAsync` with that mark. Screenshot (`p01a-not-github`). Click the header. 12. Mark the requests. Right-click `Local branches` › `main`; read; Esc. | **After step 10:** `ops`' hosting `State.PullRequests` `NotGitHub`, `Remotes` and `Bindings` empty; the section is phase 1's placeholder: the header `Pull requests` with the chevron right and `0`, no row under it (`Tags` next); no `PullRequestItem`, `PullRequestMessage` or visible `RefPullRequest` in the panel; the session still `SignedIn`; requests since the mark none.<br>**After step 11:** no menu opened (phase 3's check 3 step 5: no menu in the layer or the window, no `Context menu opened` line since the mark); in the picture the left panel as `03e-no-menu` shows `ops`' (the tab strip here `hello`, `ops`); the click then left the section closed with `0` (10.3: no toggle); requests none.<br>**After step 12:** `ops`' `main` menu (header `main`, the current branch's) holds no `Create pull request…` and no line starting `Open pull request #` (D306: without a GitHub remote `context.Hosting` is null, signed in or not); requests none; after Esc no menu. | | n/a | `p01a-not-github` | n/a: 13.4 P is on `hello`. The repository is open point 21's reading. Step 12 takes no picture; the menus' exact lines without hosting are `RepositoryMenusHostingTests`'. Varies by nature: the status bar's git box (D248). |
| P2 | Signed out (design 10.2, 10.3, 8.5; D257, D303, D281): `hello`'s section open with `0` and one clickable row `Sign in to GitHub to see pull requests…`; no request and no URL-rewrites read; the row opens "Add GitHub account" over `hello`'s window; "hosting", tabs `hello` and `ops`, signed out, dark, 1100×700 | **Method 1** (`Check_P2_P11_P4_P3_P1_signed_out_failed_loading_and_not_github`): 1. `ServeAsync()`; `using var ops = await Scenarios.OperationsAsync()`. `StartWithTabs(data, [served.Hello.Path, ops.Path], activeTab: 0, gitHub: served.GitHub)` (a new store); `WaitForRepositoryAsync()`; wait for `hello`'s view model's `HostingState.PullRequests` `SignedOut`, then for the section's message row (part P's conventions); `WaitForWatchersQuietAsync()`. Screenshot (`p02a-signed-out`). 4. (after P11's steps 2 and 3) Mark the requests and the calls. Click the message row; wait for `SignInDialog` with the focus in `SignInToken`. | **After step 1:** the session's `Account` null; requests since the start none (D257); the call log holds no `git config --null --get-regexp ^url\.` (D281). `hello`'s hosting: `SignedOut`, `Repository` octo/hello, `List` null, `ByRef` and `Bindings` empty. The section: the chevron down, `0`; one `PullRequestMessage` `Sign in to GitHub to see pull requests…` (secondary; cut with "…" at the default 260, where its 39 characters (about 6 px each) are wider than the row's room of about 205 px from x 42; its tooltip its whole text, open point 22, not asserted; where it is cut is inspected); no `PullRequestItem`; no `#n` on a branch row; the other sections as with GitHub off; no text cut but by an ellipsis.<br>**After step 4:** `SignInDialog` `Add GitHub account` as part A's conventions give it, over `hello`'s window (the window's entry, 8.5; no settings screen), its box empty with the focus; calls `probe`; requests none; the section unchanged. | | n/a | `p02a-signed-out` | n/a: 13.4 P signs in before its first capture. A9's pictures show this state from WP-P's merge ("Changes to expected results", part P). Varies by nature: the git box. |
| P3 | Loading (design 10.3, 10.2, 7.8, 17 item 1; D303): a refresh after a failure shows `Loading pull requests…` with `0` while GitHub's answer is held; the answer then fills the section; "hosting", tabs `hello` and `ops`, signed in, dark, 1100×700 | **Method 1**, after P4: 7. Mark the requests and the log. `GitHub.HoldNext("pulls:octo/hello")`. Right-click the section's header; click `Refresh pull requests`; wait for the hold's `Arrived`, for `hello`'s view model's `HostingState.PullRequests` `Loading`, then for the section's message row `Loading pull requests…` and for the watchers to be quiet (part P's conventions: no `listed` line comes while the answer is held). Screenshot (`p03a-loading`). 8. (P11.) 9. Dispose the hold; wait for `hello: listed 4 open pull requests of octo/hello` since step 7's mark and for the watchers to be quiet. | **After step 7:** one `GET api/v3/repos/octo/hello/pulls` waits at the fake, so requests since the mark none (A3's answer is kept per candidate, 7.8: no `repo:octo/hello`); `State` `Loading`, `List` and `Failure` null (the run cleared P4's failure, 17 item 1); the section: `0` and one `PullRequestMessage` `Loading pull requests…`; no `PullRequestItem`; no `#n` on a branch row.<br>**After step 9:** requests since step 7's mark exactly one `GET api/v3/repos/octo/hello/pulls`, query `state=open&per_page=100`, `TokenPresent`, 200; `State` `Loaded`; the section `4` with part P's four rows in order, no message row. | | n/a | `p03a-loading` | n/a: a held request (0.5). Varies by nature: the git box. From WP-V's merge the graph's Author column shows avatars here (A3 answered, 7.9; open point 29). |
| P4 | An error in `HostingTexts.Describe`'s words (design 10.3, 1.7, 7.6, 7.8; D338, D303, ruling 15): signed in from the section's row, GitHub refuses the list with 403; the section `0` and `GitHub answered: Resource not accessible by personal access token.`, its tooltip that text and `pull_requests=read`; the account stays signed in; "hosting", tabs `hello` and `ops`, dark, 1100×700 | **Method 1**, after P2: 5. `GitHub.FailNext("pulls:octo/hello", FakeGitHubFault.Forbidden)`. `TypeToken(OctoToken)`; press Enter; wait until the dialog has closed, for `hello`'s view model's `HostingState.PullRequests` `Failed`, then for the section's message row and for the watchers to be quiet (part P's conventions: a failure writes no `listed` line). Screenshot (`p04a-failed`). 6. Rest the pointer on the message row's text until its tooltip shows. Screenshot (`p04b-failed-tooltip`, the pointer kept). | **After step 5:** requests since step 4's mark, in order: `GET api/v3/user` 200, `Login` `octo`; `GET api/v3/repos/octo/hello` (`repo:octo/hello`) 200; `GET api/v3/repos/octo/hello/pulls`, query `state=open&per_page=100`, 403; each `TokenPresent`; nothing else (from WP-V's merge the avatars' too). Calls `probe`, `save github:127.0.0.1:47395:octo`. The session's `Account` `SignedIn`, `Keychain`, `None` (only a 401 marks it, 7.6); `accounts.json` `tokenState` `keychain`. `hello`'s hosting: `Failed`, `Failure` `Forbidden` with `Message` `Resource not accessible by personal access token` and `Detail` `pull_requests=read`, `List` null, `ByRef` empty, `Bindings` the scenario facts' one binding. The section: `0`; one `PullRequestMessage` `GitHub answered: Resource not accessible by personal access token.` (cut with "…"); no `PullRequestItem`. The log since step 4's mark: once each WP-G's Warning `GitHub: GET /api/v3/repos/octo/hello/pulls failed: 403 (request {id}).` and WP-S's Warning `GitHub: listing the pull requests of octo/hello failed: Forbidden, Resource not accessible by personal access token (request {id})`, `{id}` the refused request's; no `hello: listed` line; no dialog.<br>**After step 6:** the tooltip's two lines `GitHub answered: Resource not accessible by personal access token.` and `pull_requests=read`. | | n/a | `p04a-failed`, `p04b-failed-tooltip` | n/a: a fault (`FailNext`, 0.5). Which failure is open point 24; a failure after a list, open point 23. Varies by nature: the git box. WP-V: avatars (open point 29). |
| P5 | No open pull requests (design 10.2, 10.3, 7.8; D303, D289): every open pull request of octo/hello closed on the fake, "Refresh pull requests" lists none: `No open pull requests`, `0`, no `#n` left on a branch row; "hosting", one tab on `hello`, signed in, dark, 1100×700 | **Method 4**, after P7: 6. Set `State = "closed"` on each open pull request of `GitHub.PullRequests("octo/hello")`. Mark the requests and the log. Right-click the section's header; click `Refresh pull requests`; wait for `hello: listed 0 open pull requests of octo/hello` since the mark and for the watchers to be quiet. Scroll the panel's list to its top. Screenshot (`p05a-none`). | **After step 6:** requests since the mark exactly one `GET api/v3/repos/octo/hello/pulls` (query `state=open&per_page=100`), `TokenPresent`, 200; no `repo:octo/hello`. `hello`'s hosting `Loaded`, `List` empty with `MoreExist` false, `ByRef` empty. The section: the chevron down, `0`, one `PullRequestMessage` `No open pull requests`, no `PullRequestItem`; no branch row's `RefPullRequest` shows (`PullRequestText` empty on every `RefItemRow`); no scroll bar. | | n/a | `p05a-none` | n/a: 13.4 P repeats the rows and the menu only. Closing them on the fake is 10.7's way. Varies by nature: the git box. WP-V: the graph's badges go with the list (open point 29). |
| P6 | Rows with a draft and a fork (design 10.2, 10.3, 0.2, 7.8; D251, D303, D322; R): signed in as the real window does, `hello`'s section lists octo/hello's four open pull requests newest first with `#n`, title and author, #2 marked `Draft`, #5 from the fork `ada/hello`; `tool`, a fork, lists its parent's; "hosting", one tab on `hello`; tabs `hello` and `tool`; dark, 1100×700 | **Method 2** (`Check_P6_P11_P14_P15_P9_P10_P8_P12_rows_menus_and_selection`): 1. `ServeAsync()`. `OpenAsync(data, served, signIn: false)`. Mark the requests and the log. `OpenAccountsAsync()`; `OpenSignInDialogAsync()`; `TypeToken(OctoToken)`; press Enter; wait until signed in; press Esc. Wait for `hello: listed 4 open pull requests of octo/hello` since the mark and for the watchers to be quiet. Screenshot (`p06a-rows`).<br>**Method 3**, after P13's step 3: 4. Click the tab `tool`; `WaitForRepositoryAsync()`; wait for `tool: listed 2 open pull requests of acme/tool` since the start and for the watchers to be quiet. Screenshot (`p06b-fork-lists-parent`). 5. Right-click `#7`; read the menu; Esc. Right-click `#8`; read; Esc. | **After step 1:** requests since the mark: `GET api/v3/user` 200; then `GET api/v3/repos/octo/hello` and `GET api/v3/repos/octo/hello/pulls` (query `state=open&per_page=100`), exactly one each, `TokenPresent`, 200, `Login` `octo` (from WP-V's merge the avatars' too); none 401. The screen closed, still signed in (D256). `hello`'s hosting `Loaded`: `List` #5, #3, #2, #1, `MoreExist` false; `ByRef` as part P's scenario facts give it. The section: the chevron down, `4`; four `PullRequestItem` rows in order, named `#5 Fix a typo in the greeting` (author `ada`), `#3 Shorten long labels` (`octo`), `#2 Update the readme` (`Draft`, `octo`), `#1 Add the login form` (`ada`), `PullRequestDraft` shown in #2's only; tooltips as part P's texts give them; none selected; no message row; each row whole in the panel; no text cut but by an ellipsis; no token on screen. The graph: no pull-request badge in any branch label and no avatar in the Author column until WP-V's merge (open point 29).<br>**After step 4:** over the method, exactly one request of route `repo:octo/tool` (`GET api/v3/repos/octo/tool`, its answer naming `parent` acme/tool) and one of `pulls:acme/tool`, each `TokenPresent`, 200; none of `pulls:octo/tool`. `tool`'s hosting: `Remotes` `origin` octo/tool and `upstream` acme/tool, `Repository` acme/tool (the parent, 0.2), `List` #8, #7; `ByRef` exactly `refs/heads/fix-typo` and `refs/remotes/origin/fix-typo` → #7 (#8's deleted fork maps nothing). The section `2`: `#8 An old idea` (`ada`), `#7 Fix a typo in the readme` (`octo`), no `Draft`; the local `fix-typo` row's `RefPullRequest` `#7`.<br>**After step 5:** #7's menu: header `#7 Fix a typo in the readme`; `Open in browser` (highlighted); `Check out fix-typo` (10.5 plan 1: its head on `origin`, the user's fork); —; `Copy link`. #8's: header `#8 An old idea`; `Open in browser` (highlighted); `Check out idea` (a deleted fork, from `upstream`, 10.5); —; `Copy link`. | | | `p06a-rows`, `p06b-fork-lists-parent`; real window: `p06a-rows` | **R** (13.4 P step 1): launch on `hello`; `SignInThroughSettings`; `WaitForHostingSettled`; `PullRequestRowNames()` equals the four names above in order, `PullRequestDraft` shown in #2's row only; compared with `p06a-rows`. Its state owes nothing to a hook: the twin signs in through the screen too (open point 30). `p06b` is scripted only; #8's tooltip is not asserted (open point 27). Which titles are cut at 260 is inspected (10.3: about 110 to 150 px). Varies by nature: the git box. WP-V: badges and avatars in the graph of both pictures (open point 29). |
| P7 | More than 300 (design 10.2, 10.3, 6.3, 7.8; ruling 14, D303): 297 more open pull requests on octo/hello; a refresh reads three pages and lists 300 newest first, then `More than 300 open pull requests`, which opens the list in the browser; "hosting", one tab on `hello`, signed in, dark, 1100×700 | **Method 4** (`Check_P7_P5_more_than_300_and_none`): 1. `ServeAsync()`. `OpenAsync(data, served)`; wait for `hello: listed 4 open pull requests of octo/hello` and for the watchers to be quiet. 2. `GitHub.AddPullRequest("octo", "hello", $"More work {n}", "octo", "topic/no-pr", "main")` for n 6 to 302, in order (the fake numbers them #6 to #302; open point 28). Mark the requests and the log. Right-click the section's header; click `Refresh pull requests`; wait for `hello: listed 300 open pull requests of octo/hello` since the mark and for the watchers to be quiet. 3. Scroll the panel's list to its end. Screenshot (`p07a-more-than-300`). 4. Rest the pointer on the message row until its tooltip shows. 5. Click the message row. | **After step 2:** requests since the mark exactly three `GET api/v3/repos/octo/hello/pulls`, `TokenPresent`, 200: the first with the query `state=open&per_page=100`, then pages 2 and 3 as the `Link` headers name them (`page=2`, `page=3`, the other parameters kept); no fourth (6.3); no `repo:octo/hello`. `GitHub.Errors` empty. `hello`'s hosting `Loaded`: `List` 300 with `MoreExist` true, #302 down to #6, then #5, #3, #2 (#1, the 301st, not listed); `ByRef`: `refs/remotes/origin/topic/no-pr` → #6 (the lowest number on one key, 7.8), docs/readme's → #2, the long branch's → #3, nothing for `feature/login`. The section: `300`; 300 `PullRequestItem` rows, then one `PullRequestMessage` `More than 300 open pull requests`.<br>**After step 3:** a vertical scroll bar; from the bottom up `Stashes` `0`, `Tags` `0`, `More than 300 open pull requests`, `#2 Update the readme` (`Draft`, `octo`), `#3 Shorten long labels`, `#5 Fix a typo in the greeting`, then `#6 More work 6`, `#7 More work 7`, …; no text cut but by an ellipsis.<br>**After step 4:** the tooltip `Open pull requests in browser`.<br>**After step 5:** `Launcher.Requests` one `OpenUrl` `http://127.0.0.1:47395/octo/hello/pulls` (`OpenPullRequestsInBrowserAsync`, 10.5); no request since step 2's; nothing selected. | | n/a | `p07a-more-than-300` | n/a: the 297 pull requests are the check's own and the click goes to the fake launcher (0.5); 13.4 P lists the scenario only. Varies by nature: the git box. WP-V: open point 29. |
| P8 | The filter (design 10.3; D303): the panel's filter keeps a pull request whose `#n`, title, author or head label holds its text, ignoring case, beside the branches it keeps; the section's count is its matches; "hosting", one tab on `hello`, signed in, dark, 1100×700 | **Method 2**, after P10: 10. Click `FilterBox`; `app.Type("ADA")`. Screenshot (`p08a-filter-author`). 11. Press Ctrl+A; `app.Type("login")`. Screenshot (`p08b-filter-login`). 12. Press Ctrl+A; `app.Type("#2")`. 13. Press Ctrl+A, then Backspace. | **After step 10:** `Local branches` `0` and `Remotes` `0` (headers only); `Pull requests` `2`: `#5 Fix a typo in the greeting` (author and head label) and `#1 Add the login form` (author); `Tags` `0`, `Stashes` `0`.<br>**After step 11:** `Local branches` `1`: `feature` › `login` with `#1`; `Remotes` `1`: `origin` › `feature` › `login` with `#1`; `Pull requests` `1`: `#1 Add the login form` (title and head label); the others `0`.<br>**After step 12:** `Pull requests` `1`: `#2 Update the readme`; every other section `0`.<br>**After step 13:** the filter empty with its placeholder; the panel as before step 10 (P10's folders open, the section open with four rows). Requests none since step 10. | | n/a | `p08a-filter-author`, `p08b-filter-login` | n/a: 13.4 P. The other fields and `1` alone are `PullRequestSectionTests`' (10.6). Varies by nature: the filter's caret; the git box. |
| P9 | A click on a pull request (design 10.3; D304, D98, FD19): #1's row selected and the graph's one selection its head, `feature/login`'s commit; #5, whose head is no row of `hello`'s graph, selects nothing; "hosting", one tab on `hello`, signed in, dark, 1100×700 | **Method 2**, after P15: 7. Click `#1`'s row. Screenshot (`p09a-head-selected`). 8. Click `#5`'s row. Screenshot (`p09b-nothing-selected`). | **After step 7:** `#1` in the `selected` look, no other row of the panel selected; the graph's selection exactly `feature/login`'s commit (`git rev-parse refs/heads/feature/login` in `hello/`, "Add the login form" by Ada Lovelace), its row highlighted and in view; the details panel shows it; requests none.<br>**After step 8:** no row of the panel selected; the graph's selection empty, no row highlighted, the working row not selected; the details panel's placeholder (`SelectNothing`, FD19's rule); requests none. | | n/a | `p09a-head-selected`, `p09b-nothing-selected` | n/a: 13.4 P. The key across refreshes and `SyncSelection` are `PullRequestSectionTests`'. Varies by nature: the git box. WP-V: open point 29. |
| P10 | A branch row's `#n` (design 10.3; D308, ruling 12): `#1` on `feature/login` and `origin/feature/login`, `#3` on the long branch, whose name is cut first, the tooltip `Pull request #1: Add the login form`; none on a branch without an open pull request; the section closes and keeps its count; "hosting", one tab on `hello`, signed in, dark, 1100×700 | **Method 2**, after P9: 9. Click the header `Pull requests`. Click `Local branches` › `feature` and `Remotes` › `origin` › `feature`. Rest the pointer on the local `login` row's `#1` until its tooltip shows. Screenshot (`p10a-branch-rows`, the pointer kept). Click the header `Pull requests`. | **After step 9:** after the first click the section closed: the chevron right, still `4`, no row under it. Then: `Local branches` › `feature` › `login` with `RefPullRequest` `#1` (secondary) at the row's right end, the name left of it; `Remotes` › `origin` › `feature` › `a-very-long-branch-name-that-the-graph-must-cut` cut with "…" and `#3` whole; › `login` `#1`; on `local-work`, `main` (accent) and `origin` › `main` and on the folders `RefPullRequest` collapsed (no room); the tooltip `Pull request #1: Add the login form`. After the second click the section open again with its four rows. | | n/a | `p10a-branch-rows` | n/a: 13.4 P (part G's real window shows the graph's badge). Varies by nature: the git box. WP-V: open point 29. |
| P11 | The section's menu and a pull request's (design 10.4, 10.2, 10.5; D307, D114, D257; R): signed out `Sign in to GitHub…` and `Open pull requests in browser`, the latter asking GitHub nothing; loading, `Refresh pull requests` disabled with `Pull requests are loading`; loaded, both enabled; a pull request's `Open in browser`, `Check out {head}`, `Copy link`; "hosting", tabs `hello` and `ops`; one tab on `hello`; dark, 1100×700 | **Method 1**, after P2's step 1: 2. Mark the requests and the log. Right-click the header `Pull requests` at its centre. Screenshot (`p11a-section-menu-signed-out`). 3. Click `Open pull requests in browser`. After P3's step 7: 8. Right-click the header; rest the pointer on `Refresh pull requests` until its tooltip shows. Screenshot (`p11b-section-menu-loading`). Press Esc.<br>**Method 2**, after P6: 2. Mark the requests. Right-click the header at its centre. Screenshot (`p11c-section-menu`, the pointer kept). Press Esc. 3. Right-click `#1`'s row. Screenshot (`p11d-pull-request-menu`). (Step 4 is P14's.) 5. Right-click `#5`; read; Esc. Right-click `#2`; read; Esc. | **After step 2 (method 1):** header `Pull requests`; `Sign in to GitHub…` (highlighted); `Open pull requests in browser`; 84 high ±2 at the pointer plus (2, 2); the section neither closed nor opened.<br>**After step 3:** `Launcher.Requests` exactly one `OpenUrl` `http://127.0.0.1:47395/octo/hello/pulls`; requests none (D257); still signed out.<br>**After step 8:** header `Pull requests`; `Refresh pull requests` (disabled: "Pull requests are loading"), its tooltip shown; `Open pull requests in browser` (highlighted).<br>**After step 2 (method 2):** header `Pull requests`; `Refresh pull requests` (highlighted); `Open pull requests in browser`; 84 high ±2 at the pointer plus (2, 2); requests none.<br>**After step 3:** header `#1 Add the login form`; `Open in browser` (highlighted); `Check out feature/login`; —; `Copy link`; 119 high ±2; `#1` not selected, the graph's selection unchanged (D114).<br>**After step 5:** #5's: header `#5 Fix a typo in the greeting`; `Open in browser` (highlighted); `Check out ada:fix-typo` (the base remote `origin`, 10.5 step 2); —; `Copy link`. #2's: header `#2 Update the readme`; `Open in browser` (highlighted); `Check out docs/readme` (its head on `origin`, no local branch yet); —; `Copy link`. Requests none. | | | `p11a-section-menu-signed-out`, `p11b-section-menu-loading`, `p11c-section-menu`, `p11d-pull-request-menu`; real window: `p11c-section-menu` | **R** (13.4 P steps 2 and 3): a real right-click at the centre of `RefRow(LeftPanelViewModel.PullRequestsTitle)`; `AssertMenu` with method 2's step 2 lines; compared with `p11c-section-menu` with `keepPointer`; Esc; a real right-click on `PullRequestRow("#1 Add the login form")`: the labels of step 3, no capture; then P15's click. `p11a`, `p11b`, `p11d` are scripted only. The palette's pull request items are `RepositoryMenusHostingTests`'. Varies by nature: the git box. |
| P12 | "Open pull request #n" and "Create pull request…" in branch and drop menus (design 10.4, 0.2; D306, D313): `Open pull request #1` last in `feature/login`'s push group, `Create pull request…` in `local-work`'s; `Open pull request #2` and `Create pull request…` in a group after `origin/docs/readme`'s and `origin/topic/no-pr`'s delete groups; a drop's group between rebase and compare; `Open pull request #1` opens #1's page; "hosting", one tab on `hello`, signed in, dark, 1100×700 | **Method 2**, after P8: 14. Right-click `Local branches` › `feature` › `login`. Screenshot (`p12a-branch-menu-open`). Click `Open pull request #1`. 15. Right-click `Local branches` › `local-work`; read; Esc. 16. Click `Remotes` › `origin` › `docs`; right-click its `readme`; read; Esc. Click `Remotes` › `origin` › `topic`; right-click its `no-pr`; read; Esc. 17. Drag `Local branches` › `feature` › `login` onto `Local branches` › `main` (press, move held until the drag starts, release over the row); wait for the drop menu. Screenshot (`p12b-drop-menu-open`). Press Esc. 18. Drag `local-work` onto `Remotes` › `origin` › `main`; read; Esc. | **After step 14:** header `feature/login`; `Check out feature/login` (highlighted); `Merge feature/login into main`; `Merge feature/login into main (create a merge commit)`; `Rebase main onto feature/login`; `Interactive rebase of main onto feature/login…`; `Fast-forward feature/login to origin/feature/login` (disabled: "feature/login is up to date with origin/feature/login"); —; `Push feature/login to origin/feature/login`; `Force push feature/login…` (danger); `Open pull request #1`; —; `Create branch here…`; `Create tag here…`; `Rename feature/login…`; `Delete feature/login…` (danger); —; `Change upstream…`; `Stop tracking origin/feature/login`; —; `Compare feature/login with main`; `Show reflog of feature/login`; `Hide feature/login`; `Solo feature/login`; —; `Copy branch name`: 20 items and 5 separators, 597 high ±2, inside the window. After the click `Launcher.Requests` gains `OpenUrl` `http://127.0.0.1:47395/octo/hello/pull/1` (`OpenPullRequestAsync`, 10.5); requests none.<br>**After step 15:** header `local-work`; `Check out local-work` (highlighted); `Merge local-work into main`; `Merge local-work into main (create a merge commit)`; `Rebase main onto local-work`; `Interactive rebase of main onto local-work…`; —; `Push local-work…`; `Create pull request…`; —; `Create branch here…`; `Create tag here…`; `Rename local-work…`; `Delete local-work…` (danger); —; `Set upstream…`; —; `Compare local-work with main`; `Show reflog of local-work`; `Hide local-work`; `Solo local-work`; —; `Copy branch name`: 519 high ±2.<br>**After step 16:** header `origin/docs/readme`; `Check out origin/docs/readme` (highlighted); `Merge origin/docs/readme into main`; `Merge origin/docs/readme into main (create a merge commit)`; `Rebase main onto origin/docs/readme`; `Interactive rebase of main onto origin/docs/readme…`; —; `Create branch here…`; `Create tag here…`; —; `Delete docs/readme on origin…` (danger); —; `Open pull request #2`; —; `Compare origin/docs/readme with main`; `Hide origin/docs/readme`; `Solo origin/docs/readme`; —; `Copy branch name`: 415 high ±2. Then `origin/topic/no-pr`'s: header `origin/topic/no-pr`; the same lines with `origin/topic/no-pr` for `origin/docs/readme` and `Delete topic/no-pr on origin…` (danger), and `Create pull request…` in place of `Open pull request #2` (its only pull request, #4, is closed); 415 high ±2.<br>**After step 17:** header `feature/login → main`; `Merge feature/login into main` (highlighted); `Merge feature/login into main (create a merge commit)`; `Fast-forward main to feature/login`; —; `Check out feature/login and rebase it onto main` (disabled: "feature/login is up to date with main"); `Check out feature/login and rebase it onto main interactively…`; —; `Open pull request #1`; —; `Compare feature/login with main`: 241 high ±2.<br>**After step 18:** header `local-work → origin/main`; `Push local-work to origin/main` (highlighted); `Push local-work to origin/main and track it`; —; `Check out local-work and rebase it onto origin/main` (disabled: "local-work is up to date with origin/main"); `Check out local-work and rebase it onto origin/main interactively…`; —; `Create pull request…`; —; `Compare local-work with origin/main`: 215 high ±2. After each Esc no menu; no ref, file or request changed by steps 15 to 18. | | n/a | `p12a-branch-menu-open`, `p12b-drop-menu-open` | n/a: 13.4 P. Without a GitHub remote the items are left out (P1 step 12, signed in; `RepositoryMenusHostingTests`; every earlier menu check runs with GitHub off). `Create pull request…` is WP-N's action, clicked in part N. Varies by nature: the git box. |
| P13 | Checking out a pull request (design 10.5, 0.2; D305, D304, D255, D193; 10.8): a double-click on #1 checks out `feature/login` (its head on `origin`) without a fetch; "Check out ada:fix-typo" fetches `refs/pull/5/head` into `FETCH_HEAD` only, through the app's helper, makes `fix-typo` without upstream at its SHA and checks it out; never a `refs/pull` fetch line or a `refs/remotes/<r>/pr/<n>` ref; each item then disabled; "hosting", tabs `hello` and `tool`, signed in, dark, 1100×700 | **Method 3** (`Check_P13_P6_checkout_and_a_forks_tab`): 1. `ServeAsync()`. `StartWithTabs(data, [served.Hello.Path, served.Tool.Path], activeTab: 0, gitHub: served.GitHub)`; `WaitForRepositoryAsync()`; `SignInAsync(OctoToken)`; wait for `hello: listed 4 open pull requests of octo/hello` and for `tool: listed 2 open pull requests of acme/tool`, both since the start, and for the watchers to be quiet. In `hello/`, through `GitRunner` with `TempRepo.IsolatedEnvironment`, read `git config --local --list`. 2. Mark the requests and the log; take a quiet call mark. Double-click `#1`'s row; wait for the checkout's end and for the watchers to be quiet. Right-click `#1`; read; Esc. 3. Mark the requests, the git requests and the log; take a quiet call mark. Right-click `#5`; click `Check out ada:fix-typo`; wait for both operations' ends, for `hello: listed 4 open pull requests of octo/hello` since the mark and for the watchers to be quiet. Screenshot (`p13a-checked-out-fork`). Right-click `#5`; read; Esc. (Steps 4 and 5 are P6's.) 6. Close the app. In `hello/`, as in step 1: `git symbolic-ref HEAD`; `git rev-parse HEAD`; `git for-each-ref --format=%(refname)`; `git config --get-all remote.origin.fetch`; `git config --get-regexp ^branch\.fix-typo\.`; `git config --local --list`; `git status --porcelain`; and read `.git/FETCH_HEAD`. | **After step 2:** one operation, phase 3's checkout of `origin/feature/login` (D305), ending Succeeded; the status bar `Checked out feature/login`; git calls since the quiet mark hold `git switch --no-guess --no-overwrite-ignore feature/login` (D193) and no `git fetch` or `git branch`; requests none (a local operation refreshes no list, 7.10); HEAD on `feature/login`; #1's menu `Check out feature/login` (disabled: "feature/login is already checked out").<br>**After step 3:** two operations, Succeeded in this order: `Fetch pull request #5` (report `Fetched #5 into fix-typo`), then the checkout of `fix-typo`; the status bar `Checked out fix-typo`; no dialog at any time. Git calls since the quiet mark, in order: `git fetch --progress --no-tags --refmap= --write-fetch-head origin refs/pull/5/head`; `git branch --no-track fix-typo <sha>`, `<sha>` the full SHA of `refs/pull/5/head` in the copy's `github/octo/hello.git`; `git switch --no-guess --no-overwrite-ignore fix-typo`; no other `git fetch`, no `rev-parse` of `FETCH_HEAD` (10.5), no `git config` that writes. Git requests on `octo/hello` since the mark: at least one with `GivenUserName` `x-access-token` answered 200 (D255); none with another user name; none with credentials answered 401. API requests since the mark: exactly one `GET api/v3/repos/octo/hello/pulls` (the refresh after a network operation, 7.10), query `state=open&per_page=100`, `TokenPresent`, `Conditional` true and answered 304 (octo/hello's list unchanged since step 1's read, whose ETag the client keeps and sends, design 16 item 10, as in A8 step 6); none answered 401. The graph's top row "Fix a typo in the greeting" (Ada Lovelace) with `fix-typo`, HEAD; the local row `fix-typo` (accent) without `#n` (no upstream, 7.8, D313); the section still `4`; #5's menu `Check out ada:fix-typo` (disabled: "fix-typo is already checked out").<br>**After step 6:** HEAD `refs/heads/fix-typo` at `<sha>`; `for-each-ref` exactly the refs of part P's scenario facts and `refs/heads/fix-typo`: nothing under `refs/pull/`, no `refs/remotes/origin/pr/` (10.8); `remote.origin.fetch` exactly `+refs/heads/*:refs/remotes/origin/*`; `--get-regexp ^branch\.fix-typo\.` exits 1 and prints nothing; `--local --list` byte for byte step 1's; `FETCH_HEAD` one line: `<sha>`, a tab, an empty field, a tab, then text starting `'refs/pull/5/head' of ` (the URL not compared, 10.5); the working tree clean. | | n/a | `p13a-checked-out-fork` | n/a: 13.4 P checks nothing out. The first git request without credentials gets 401 (2.2). #1's operation title is open point 32. `fetch.prune`, `tagOpt`, a rewritten `FETCH_HEAD`, a taken name and a reused branch are 10.6's classes'. Varies by nature: the git box. WP-V: open point 29. |
| P14 | Copy link (design 10.4, 10.5; D251): #1's `Copy link` puts #1's page address on the clipboard and asks GitHub nothing; "hosting", one tab on `hello`, signed in, dark, 1100×700 | **Method 2**, after P11's step 3: 4. Click `Copy link`. | **After step 4:** the menu closed; `FakeClipboard.Text` `http://127.0.0.1:47395/octo/hello/pull/1` (`html_url`, `WithPort`); requests none; `Launcher.Requests` unchanged; no operation; nothing selected. | | n/a | none | n/a: the real window never clicks a "Copy …" item (13.2). |
| P15 | Open in browser (design 10.4, 10.5, 13.5; D251, D321; R): #1's `Open in browser` hands #1's page to the launcher and asks GitHub nothing; in the real process the default browser opens once on the fake's page; "hosting", one tab on `hello`, signed in, dark, 1100×700 | **Method 2**, after P11's step 5: 6. Mark the requests and the log. Right-click `#1`; click `Open in browser`. | **After step 6:** `Launcher.Requests` exactly one `OpenUrl` `http://127.0.0.1:47395/octo/hello/pull/1`; requests since the mark none (the fake launcher fetches no page); no operation; no Warning in the log since the mark. The section's and the `More` row's `…/octo/hello/pulls` are P11's and P7's, the branch menu's item P12's. | | | none; real window: none | **R** (13.4 P step 4, 13.5; D321): with `OpensTheBrowser` true, mark the fake and the log; a real click on #1's `Open in browser`; within `NetworkTimeout` at least one request of route `page:octo/hello`, path `octo/hello/pull/1`, 200, `TokenPresent` false (a browser may fetch a page more than once); no `[WRN]` line since the mark; a real click at the neutral point takes the activation back; run.txt `P: the browser opened http://127.0.0.1:<port>/octo/hello/pull/1; its tab is left open.`; then `AssertCleanEnd`. The owner closes the tab; where the step cannot run, D321's `false` with the owner's word moves it to the checks by hand at acceptance. |
| G1 | Badges (design 11.2, 11.5, 0.5; D309, D251, D322; R): `#1` after the pull request's icon inside `feature/login`'s label (row 0) and `origin/feature/login`'s (row 5), `#2` on the draft's `origin/docs/readme`, `#3` on the long branch, none on any other label; the tooltip over the local label; `LabelsInRow`'s `Badge` set and `FullText` the whole name; "hosting", one tab on the prepared `hello`, signed in, dark, 1100×700 | **Method 1** (`Check_G1_G2_G3_badges_tooltips_and_details`): 1. `ServeAsync()`. `PartG.PrepareAsync(served.Hello)`. `OpenAsync(data, served, signIn: false)`. Mark the requests and the log. `OpenAccountsAsync()`; `OpenSignInDialogAsync()`; `TypeToken(OctoToken)`; press Enter; wait until signed in; press Esc. Wait for `hello: listed 4 open pull requests of octo/hello` and `hello: showing GitHub avatars for 2 authors` since the mark, then for the watchers to be quiet. 2. Click row 4 (`GraphRowPoint(PartG.Row)`, `Update the readme`); wait until `DetailsAuthor` reads `Octo Cat <octo@example.com>` and `DetailsAuthorAvatar` shows. Screenshot (`g01a-badges-and-avatar`). 3. Rest the pointer at the centre of row 0's label (`GraphLabelBounds(0, "feature/login")`) until the graph's tooltip shows. Screenshot (`g01b-badge-tooltip`, the pointer kept). | **After step 1:** the screen closed, still signed in (D256); requests since the mark: `GET api/v3/user` 200, then exactly one `GET api/v3/repos/octo/hello` and one `GET api/v3/repos/octo/hello/pulls` (query `state=open&per_page=100`), each `TokenPresent`, 200, `Login` `octo` (the avatars' requests are G3's); none 401. Each of the two lines once since the mark. `hello`'s hosting `Loaded` (#5, #3, #2, #1), `ByRef` exactly as part G's scenario facts give it. The graph: `Columns.LaneCount` 5 (`PartG.Lanes`); the columns Branch / Tag, Graph and Message only; every row's labels exactly as the facts' table's signed-in column (text as drawn, `FullText`, `Kind`, `RefName`, `Badge`; from x 6, the right edge ±1, y the row's top + 4 to + 22): row 0 `feature/login` (`LocalBranch`, filled, the name whole to x 80.4) with the icon from x 86.4 and `#1` from x 99.4, the label to 117.8; row 5 `origin/featur…` (`RemoteBranch`, `FullText` `origin/feature/login`) with `#1`; row 4 `origin/docs/r…` with `#2` (`IsDraft` true); row 3 `origin/featur…` with `#3`; no `Badge` on rows 1, 2 and 6 (`main` and its `+1`). The section's four rows as P6 gives them.<br>**After step 2:** row 4 selected and in view; the details panel: `DetailsSubject` `Update the readme`; the author line `Octo Cat <octo@example.com>` with `DetailsAuthorAvatar` visible before it (16×16, its left at `DetailsDate`'s, its top at `DetailsAuthor`'s, showing octo's image: in the picture the ellipse's centre pixel white and the pixel 5 left of it `#2F81F7`, ±24 per channel, G3's rule for the graph) and `DetailsAuthor`'s left 22 right of `DetailsDate`'s; `2026-01-01 12:04`; Committer `Test Author <author@example.com>`; `Changed files (1)`, M `README.md`. The left panel as in `p06a`. No request since step 1; no text cut but by an ellipsis; no token on screen.<br>**After step 3:** the graph's tooltip exactly `feature/login (pull request #1: Add the login form)`; the label and its badge as before under the pointer; no request. | | | `g01a-badges-and-avatar`, `g01b-badge-tooltip`; real window: `g01a-badges-and-avatar` | **R** (13.4 G steps 1 to 3): `PartG.PrepareAsync(hello)` before the launch on `hello`; `SignInThroughSettings`; `WaitForHostingSettled` (both lines since the mark); the fake answered `avatar:1001` and `avatar:1003` without a token, and `avatars/1001.png` and `avatars/1003.png` are in the data folder; a real click at `GraphRowPoint(PartG.Row, PartG.Lanes)` (row 4, 5 lanes); wait for `DetailsAuthor` `Octo Cat <octo@example.com>` and `DetailsAuthorAvatar`; compared with `g01a-badges-and-avatar`; `AssertCleanEnd`. At 1100×700 the capture's avatar is the details panel's only (open point 35). `g01b` is scripted only. Where each cut name ends is inspected against the facts' table (open point 41). Varies by nature: the git box. |
| G2 | A long name cut, the badge kept (design 11.2, 11.5; D309): the long branch's name cut with "…" so that its label, `#3` whole, ends inside the 130-px column; `origin/feature/login`'s and `origin/docs/readme`'s names cut for their badges too; the tooltip whole, a draft's naming it a draft; a row without a pull request's tooltip as today; "hosting", one tab on the prepared `hello`, signed in, dark, 1100×700 | **Method 1**, after G1: 4. Rest the pointer at the centre of row 3's label until the graph's tooltip shows. Screenshot (`g02a-cut-badge-tooltip`, the pointer kept). 5. Rest it on row 4's label until the tooltip shows; read it. 6. Rest it on row 6's `main` until the tooltip shows; read it. | **After step 4:** row 3's label: text `origin/featur…`, `FullText` `origin/feature/a-very-long-branch-name-that-the-graph-must-cut`, `Badge` (3, `Shorten long labels`, false), from x 6 to 126.0 (inside the column's 130), the name's "…" ending at about x 86.7, the icon from about 92.7, `#3` whole before the label's padding; rows 5 and 4 cut the same way (the facts' table); the tooltip exactly `origin/feature/a-very-long-branch-name-that-the-graph-must-cut (pull request #3: Shorten long labels)`, whole (on one line or wrapped, nothing cut).<br>**After step 5:** the tooltip exactly `origin/docs/readme (draft pull request #2: Update the readme)`.<br>**After step 6:** the tooltip exactly `main`, a line break, `origin/main` (no pull request: as today). No request since step 1. | | n/a | `g02a-cut-badge-tooltip` | n/a: 13.4 G captures G1's state only, which shows the cut labels too. Where the tooltip wraps is inspected. Varies by nature: the git box. |
| G3 | Avatars (design 11.3, 11.5, 7.9, 6.4; D252, D310, D311, D286): octo's and ada's images in the Author column and before the details panel's author, Test Author's rows and line as today; the avatars' requests without the token; `avatars/` holding the two images, their index and the e-mail map; "hosting", one tab on the prepared `hello`, signed in; dark, 1100×700 (the details panel) and 1920×1080 (the Author column) | **Method 1**, after G2: 7. Click row 5 (`Add the login form`); wait until `DetailsAuthor` reads `Ada Lovelace <ada@example.com>`, `DetailsAuthorAvatar` shows and a capture (`app.Capture()`, not saved) shows ada's image in it by the expected result's pixel rule (until ada's file is decoded the ellipse keeps octo's image, 11.3; open point 44). 8. Click row 6 (`Add the docs folder`); wait until `DetailsAuthor` reads `Test Author <author@example.com>`. Screenshot (`g03a-unknown-author-details`).<br>**Method 2**, after G6: 2. Mark the requests and the log. `SignInAsync(OctoToken)`; wait for both lines since the mark, then for the watchers to be quiet, then until `DetailsAuthorAvatar` shows (row 4, selected since G6). Screenshot (`g03b-avatars-1920`). | **After step 7 (method 1):** requests since step 1's mark, besides G1's: exactly one `GET api/v3/repos/octo/hello/commits`, query `sha=main&per_page=100`, `TokenPresent`, 200, after the repository's; no other `commits:` request (no `author=` lookup); then `avatar:1001` and `avatar:1003` exactly once each, `GET avatars/u/<id>` with the query `v=4&s=64`, `TokenPresent` false, 200; no `avatar:1002`. The avatars exactly `octo@example.com` → `<data>/avatars/1001.png` and `ada@example.com` → `<data>/avatars/1003.png`; `avatars/` exactly as part G's scenario facts give it, each image byte for byte its user's default avatar (`TestImages.SquareOn(64, 64, …)`, 2.3). The author line `Ada Lovelace <ada@example.com>` with `DetailsAuthorAvatar` visible at G1's places, showing ada's image (in that capture the ellipse's centre pixel white and the pixel 5 left of it `#1F883D`, ±24 per channel).<br>**After step 8:** row 6 selected; `DetailsAuthorAvatar` not visible and `DetailsAuthor`'s left at `DetailsDate`'s (its bounds as with no avatars, 11.3); `Add the docs folder`, `2026-01-01 12:02`, no Committer row, `Changed files (1)`, A `docs/index.md`; no request since step 1.<br>**After step 2 (method 2):** requests since the mark: `GET api/v3/user` 200, then `repo:octo/hello`, `pulls:octo/hello` and `commits:octo/hello` exactly once each, as in method 1, and `avatar:1001` and `avatar:1003` once each without the token; `avatars/` as in method 1. The graph's six columns, Author at x 928 to 1068: rows 3, 4 and 8 octo's image at x 936 to 952, y the row's top + 5 to + 21, `Octo Cat` from x 958; rows 5 and 7 ada's and `Ada Lovelace` from x 958; rows 0, 1, 2 and 6 `Test Author` from x 936, no image; no author cut; in the picture each image's centre pixel white and the pixel 5 left of it its user's colour (`#2F81F7`, `#1F883D`; ±24 per channel); `ShowsAvatarFor` true for `octo@example.com` and `ada@example.com`, false for `author@example.com`. Row 4 still selected, its author line with octo's image as in G1's step 2 (its pixel rule); the badges as G1's (the Branch / Tag column is 130 wide at every size). No text cut but by an ellipsis. | | n/a | `g03a-unknown-author-details`, `g03b-avatars-1920` | n/a: G1's capture repeats the details panel's image; 1920×1080 is scripted only (13.1). D311's limit (no address of a local-only commit sent) is `RepositoryAvatarsTests`' (11.4): here every author also has a commit on `origin`'s branches. `index.json`'s and the map's dates are not asserted (the session's clock is the system's). Varies by nature: the git box. |
| G4 | Light (design 11.5, 11.2, 11.3; D309, D310): G1's and G3's graph and the details panel in the light theme; "hosting", one tab on the prepared `hello`, signed in, light, 1920×1080 | **Method 2**, after G3: 3. Click `ThemeSwitch`; wait for the light theme. Screenshot (`g04a-light-1920`). | **After step 3:** still signed in; no request since step 2. The window light: the labels in light's lane colours, each badge in its label's text colour (`#FFFFFF` on `feature/login`'s filled label, the main text colour on the remote ones; no new token, 11.2), the same badges, cuts and places as in `g03b`; the same images at the same places, the names in light's secondary colour; row 4 selected (`#DDE1FA`), its author line with octo's image; no text cut but by an ellipsis. | | n/a | `g04a-light-1920` | n/a: light and 1920×1080 are scripted only (13.1). Varies by nature: the git box. |
| G5 | A repository without a GitHub remote, signed in (design 11.5, 11.1, 11.3, 0.2; D252, D251, D257): its graph and details panel the same picture as with GitHub off; no API request names it; "hosting"'s prepared `hello` with `origin` a folder, with GitHub off, then signed in; dark, 1100×700 | **Method 3** (`Check_G5_not_github_as_with_github_off`, a `[Fact]`): 1. `ServeAsync()`. `PartG.PrepareAsync(served.Hello)`. In `hello/`, `git remote set-url origin <the copy's github/octo/hello.git as a folder path>` (`served.Hello.GitAsync`). 2. `ShellDriver.StartWithTabs(data1, [served.Hello.Path], activeTab: 0)` at `OpenAsync`'s size and themes, without `gitHub:` (off); `WaitForRepositoryAsync()`; `WaitForWatchersQuietAsync()`. Click row 4; wait until `DetailsAuthor` reads `Octo Cat <octo@example.com>`. Screenshot (`g05a-not-github-off`). Read every row's labels and the author line's bounds; close the app. 3. Mark the requests. `OpenAsync(data2, served)` (signed in through the session). Click row 4; wait as in step 2. Screenshot (`g05b-not-github-signed-in`). Read as in step 2; close the app. | **After step 2:** `hello`'s hosting `NotGitHub`; the section phase 1's placeholder (closed, `0`); every label as the facts' table's "today" column, no `Badge`; `DetailsAuthorAvatar` not visible.<br>**After step 3:** the session's `Account` `SignedIn`; requests since the mark exactly one, `GET api/v3/user` 200 (the sign-in's): none of routes `repo:`, `pulls:`, `commits:`, `page:` or `avatar:`; `hello`'s hosting `NotGitHub`, `Remotes` and `ByRef` empty, the avatars `None`; no `listed` and no `showing GitHub avatars` line since the start; no `avatars/` in `data2`; every row's labels and the author line's bounds equal to step 2's; the section the placeholder, as P1 shows it. `g05b` is the same file as `g05a` (equal SHA-256). | | n/a | `g05a-not-github-off`, `g05b-not-github-signed-in` | n/a: two starts, one with GitHub off; 13.4 G captures G1's state only. `hello` with its remote a folder keeps octo's and ada's commits and the branches with open pull requests, so a broken guard would show images and badges (open point 38). |
| G6 | Signed out, the graph and the details panel as today (design 11.1, 11.3, 7.9, 7.5; D257, D252, D309, D291): GitHub on and signed out, `hello`'s labels without a badge and its authors without an image; no request; after a sign-out that follows the avatars, the same and no `avatars/`; "hosting", one tab on the prepared `hello`, signed out, dark, 1920×1080 (the sign-out light) | **Method 2** (`Check_G6_G3_G4_signed_out_avatars_and_light_at_1920x1080`): 1. `ServeAsync()`. `PartG.PrepareAsync(served.Hello)`. `OpenAsync(data, served, signIn: false, width: 1920, height: 1080)`. Wait for `hello`'s view model's `HostingState.PullRequests` `SignedOut`, then for the section's message row and for the watchers to be quiet. Click row 4; wait until `DetailsAuthor` reads `Octo Cat <octo@example.com>`. Screenshot (`g06a-signed-out-1920`).<br>**Method 2**, after G4: 4. `OpenAccountsAsync()`. Mark the requests and the calls. `SignOutAsync(confirm: true)`; press Esc. Wait for `hello`'s view model's `HostingState.PullRequests` `SignedOut`, the section's message row, until the graph's `ShowsAvatarFor` is false for `octo@example.com` and `ada@example.com`, then for the watchers to be quiet. | **After step 1:** requests since the start none (D257); the call log holds no `git config --null --get-regexp ^url\.` (D281); `hello`'s hosting `SignedOut`, `ByRef` empty; the avatars `None`; no `avatars/` in the data folder; no `listed` and no `showing GitHub avatars` line. The section's one row `Sign in to GitHub to see pull requests…` (P2's). The graph's six columns, Author at x 928 to 1068: every label as the facts' table's "today" column, no `Badge`; every author's name from x 936, no image; `ShowsAvatarFor` false for the three addresses; row 4 selected, `DetailsAuthorAvatar` not visible, `DetailsAuthor`'s left at `DetailsDate`'s. No text cut but by an ellipsis.<br>**After step 4:** requests since the mark none; calls exactly `delete github:127.0.0.1:47395:octo`; the session's `Account` null; `hello`'s hosting `SignedOut`, `ByRef` empty; the avatars `None`; no `avatars/` in the data folder (D291, 7.5); the section's one row `Sign in to GitHub to see pull requests…`; every label as the facts' table's "today" column, no `Badge`; every author's name from x 936, no image; row 4 still selected, `DetailsAuthorAvatar` not visible, `DetailsAuthor`'s left at `DetailsDate`'s. | | n/a | `g06a-signed-out-1920` | n/a: 1920×1080 is scripted only (13.1). Compared by region with `g03b`, taken next in the same window: they differ only in the four badged labels, the Author column's five images and names, the author line and the left panel from its "Pull requests" section down. Step 4 takes no picture: its state is `g06a`'s in light, asserted through the controls; A8 signs out an empty tab, where no image was shown. Varies by nature: the git box. |
| G7 | The authors' read fails (design 7.9, 11.3, 11.1; D252, D310): GitHub answers the commits page with 500; no image is fetched and every author stays as today, the badges as G1's; the failure logged and nothing more asked; "hosting", one tab on the prepared `hello`, signed in, dark, 1100×700 | **Method 4** (`Check_G7_the_authors_read_fails`): 1. `ServeAsync()`. `PartG.PrepareAsync(served.Hello)`. `GitHub.FailNext("commits:octo/hello", FakeGitHubFault.ServerError)`. `OpenAsync(data, served)` (signed in through the session). Wait for `hello: listed 4 open pull requests of octo/hello` and for WP-S's warning `GitHub: reading the authors of octo/hello failed: …` since the start, then for the watchers to be quiet. 2. Click row 4; wait until `DetailsAuthor` reads `Octo Cat <octo@example.com>`. Screenshot (`g07a-authors-failed`). | **After step 1:** requests since the start: `GET api/v3/user` 200; one `repo:octo/hello` and one `pulls:octo/hello`, 200; one `GET api/v3/repos/octo/hello/commits` (query `sha=main&per_page=100`), `TokenPresent`, 500; no other `commits:` and no `avatar:` request; none 401. The log since the start: WP-G's `GitHub: GET /api/v3/repos/octo/hello/commits failed: 500 (request {id}).` and WP-S's `GitHub: reading the authors of octo/hello failed: ServerError, Server Error (request {id})` once each, both naming the one request's id; no `showing GitHub avatars` line. The session's `Account` `SignedIn` (only a 401 marks it, 7.6). `hello`'s hosting `Loaded`, `ByRef` as the facts give it; the avatars `None`; no `avatars/` in the data folder (the map is written only with an answer, an image only once fetched).<br>**After step 2:** the labels and badges as G1's step 1; row 4 selected, `DetailsAuthorAvatar` not visible, `DetailsAuthor`'s left at `DetailsDate`'s; `ShowsAvatarFor` false for the three addresses; still no `avatar:` request, no second `commits:` request (a failure asks nothing more this run, 7.9 step 3, and the same set is not sent twice, 11.3) and no `showing GitHub avatars` line. | | n/a | `g07a-authors-failed` | n/a: a fault (0.5). An image's own failure is `AvatarCacheTests`' and `GraphAvatarTests`' (open point 40). Varies by nature: the git box. |
| N1 | From a branch (design 12.8, 12.3, 12.4, 12.5, 12.2, 10.4, 0.5; D306, D313, D317, D293, D289, D322, ruling 13; R): `topic/no-pr`'s `Create pull request…` (`ForLocalBranch`) opens the form From `topic/no-pr` · `octo/hello` Into `origin/main` · `octo/hello`, titled `Try an idea`; Create makes #6 without a push; then the list's row, the branch row's and the graph's `#6`, `Created pull request #6` and `Open in browser`, whose click hands #6's page to the launcher; `Open pull request #6` in the branch's menu; the offer gone at the next operation; "hosting", one tab on the prepared `hello`, signed in, dark, 1100×700 | **Method 1** (`Check_N1_from_a_branch_and_the_offer`): 1. `ServeAsync()`. `PartN.PrepareAsync(served.Hello)`. `OpenAsync(data, served, signIn: false)`. Mark the requests and the log. `OpenAccountsAsync()`; `OpenSignInDialogAsync()`; `TypeToken(OctoToken)`; press Enter; wait until signed in; press Esc. Wait for `hello: listed 4 open pull requests of octo/hello` and `hello: showing GitHub avatars for 2 authors` since the mark, then for the watchers to be quiet. 2. Mark the requests. Click `Local branches` › `topic`; right-click its `no-pr`; read the menu. Click `Create pull request…`; wait for the form with `PullRequestBase` `origin/main` and the keyboard focus in `PullRequestTitle`. Screenshot (`n01a-form`). 3. Mark the requests, the git requests and the log; take a quiet call mark. Click `Create`; wait for the operation's end, for `hello: listed 5 open pull requests of octo/hello` twice since the mark and for the watchers to be quiet. Screenshot (`n01b-created`). 4. Mark the requests. Click `Open in browser` (`OperationOfferButton`). 5. Right-click `topic` › `no-pr`; read; Esc. Right-click the header `Pull requests` at its centre; read; Esc. 6. Mark the requests. Click the toolbar's Fetch; wait for its end and for `hello: listed 5 open pull requests of octo/hello` since the mark. | **After step 1:** the screen closed, still signed in (D256); requests since the mark: `GET api/v3/user` 200, then exactly one `GET api/v3/repos/octo/hello` and one `GET api/v3/repos/octo/hello/pulls`, each `TokenPresent`, 200, `Login` `octo`; none 401. `hello`'s hosting `Loaded` (#5, #3, #2, #1), `ByRef` as part P's scenario facts give it, nothing for `topic/no-pr`; the section `4` with P6's rows; `Local branches` holds the closed folder `topic`.<br>**After step 2:** the menu: header `topic/no-pr`; `Check out topic/no-pr` (highlighted); `Merge topic/no-pr into main`; `Merge topic/no-pr into main (create a merge commit)`; `Rebase main onto topic/no-pr`; `Interactive rebase of main onto topic/no-pr…`; `Fast-forward topic/no-pr to origin/topic/no-pr` (disabled: "topic/no-pr is up to date with origin/topic/no-pr"); —; `Push topic/no-pr to origin/topic/no-pr`; `Force push topic/no-pr…` (danger); `Create pull request…`; —; `Create branch here…`; `Create tag here…`; `Rename topic/no-pr…`; `Delete topic/no-pr…` (danger); —; `Change upstream…`; `Stop tracking origin/topic/no-pr`; —; `Compare topic/no-pr with main`; `Show reflog of topic/no-pr`; `Hide topic/no-pr`; `Solo topic/no-pr`; —; `Copy branch name`: P12's `feature/login` menu with `Create pull request…` for `Open pull request #1`, 20 items and 5 separators, 597 high ±2, inside the window. The form (part N's conventions): `From` `topic/no-pr` (tooltip `topic/no-pr`) `octo/hello`, no `PullRequestHeadNote` (an upstream of the same name, level, D313); `Into` `origin/main` `octo/hello`, `Choose…` enabled; `PullRequestTitle` `Try an idea`, focused, the caret at its end; `PullRequestDescription` empty, its placeholder `Description` shown; `Draft` unticked; no `PullRequestError` and no `PullRequestOpenExisting`; `Cancel` and `Create` enabled. Requests since the mark none (the form's A3 is the list's kept answer, 7.8); no text cut but by an ellipsis; no token on screen.<br>**After step 3:** one operation, `Create pull request for topic/no-pr`, Succeeded with `Created pull request #6`; the form closed; no dialog at any time. Git calls since the quiet mark: no `git push` (D313); git requests none. Requests since the mark: exactly one POST, `TokenPresent`, `Login` `octo`, 201, its `Body` exactly `title` `Try an idea`, `body` empty, `head` `topic/no-pr`, `base` `main` and `draft` false, without `maintainer_can_modify` (6.3); then the two `GET …/pulls` of part N's conventions; none 401. The fake's #6: open, `Title` `Try an idea`, `Body` empty, `Author` `octo`, `HeadRef` `topic/no-pr`, `HeadRepository` null, `BaseRef` `main`, `IsDraft` false; `refs/pull/6/head` of `github/octo/hello.git` at `topic/no-pr`'s SHA. `hello`'s hosting: `List` #6, #5, #3, #2, #1; `ByRef` also `refs/heads/topic/no-pr` and `refs/remotes/origin/topic/no-pr` → #6. The status bar: `OperationStatus` `Created pull request #6` (secondary), its tooltip the same; the offer shown, text and name `Open in browser`, its left 6 right of the text's right; no progress bar, no Cancel. The section `5`: `#6 Try an idea` (`octo`, no `Draft`) first, then P6's four; `topic` › `no-pr`'s `RefPullRequest` `#6` (tooltip `Pull request #6: Try an idea`). The graph's row 1: its first label's `FullText` `topic/no-pr` with `Badge` `(6, "Try an idea", false)`, no other label of the row with one (11.2); the other badges by 11.2's rule from `ByRef`, as before the create: `#3` on row 2, `#2` on row 3, `#1` on row 4's first label `feature/login` only. The log: `hello: Create pull request for topic/no-pr: into octo/hello main, draft no, no push` (Info, open point 53); no Warning since the mark. No token on screen.<br>**After step 4:** `Launcher.Requests` exactly one `OpenUrl` `http://127.0.0.1:47395/octo/hello/pull/6` (`WithPort`); requests none; no operation; the status bar and the offer unchanged.<br>**After step 5:** `topic/no-pr`'s menu: step 2's lines with `Open pull request #6` in place of `Create pull request…` (ruling 11). The section's menu: header `Pull requests`; `Refresh pull requests` (highlighted); `Open pull requests in browser`; 84 high ±2: no `Create pull request…` (0.1; open point 45). Requests none.<br>**After step 6:** the status bar `Fetched from all remotes`; the offer hidden, taking no room (D317: it goes when the user's next operation starts); requests of route `pulls:octo/hello` since the mark exactly one, `Conditional`, 304 (D289). | | | `n01a-form`, `n01b-created`; real window: `n01a-form`, `n01b-created` | **R** (13.4 N steps 1 to 5): `PartN.PrepareAsync(hello)` before the launch on `hello`; `SignInThroughSettings`; `WaitForHostingSettled`; a real click on `Local branches` › `topic`, a real right-click on its `no-pr` (`RefRow(LeftPanelViewModel.LocalBranchesTitle, …)`), then `Create pull request…`; wait until `PullRequestBase` reads `origin/main` and the focused `PullRequestTitle` holds `Try an idea`; compared with `n01a-form`; mark the fake and the log; a real click on `Create`; wait for `AppLogFile.OperationEnded("hello", "Create pull request for topic/no-pr", Succeeded)` and for `PullRequestsListed` twice since the mark (open point 51); since the mark one `create:octo/hello` request, 201, its `Body` with step 3's title, head, base and `"draft":false`, and no receive-pack request; the status bar `Created pull request #6`, `OperationOfferButton` named `Open in browser` (never clicked, 13.2); `PullRequestRowNames()` first `#6 Try an idea`; compared with `n01b-created`; `AssertCleanEnd`. Steps 4 to 6 are scripted only. Where `topic/no-pr`'s name is cut beside its badge is inspected (open point 55). Varies by nature: the git box; the title box's caret (`n01a`). |
| N2 | Not on GitHub (design 12.8, 12.3, 12.5, 12.6, 12.1; D313, D312, D315, D255): `local-work`, on no remote, shows that Create pushes it first and sets its upstream; Create pushes it through the app's helper, then posts, as one operation; while the POST runs the form reads `Creating…` and the status bar `Creating pull request…` without Cancel; afterwards `branch.local-work.merge`, the branch on the fake, one operation per Create; "hosting", one tab on the prepared `hello`, signed in, dark, 1100×700 | **Method 2** (`Check_N2_N7_N3_not_on_github_cancel_and_draft`): 1. `ServeAsync()`. `PartN.PrepareAsync(served.Hello)`. `OpenAsync(data, served)`; wait for `hello: listed 4 open pull requests of octo/hello` and `hello: showing GitHub avatars for 2 authors` since the start and for the watchers to be quiet. In `hello/`, through `GitRunner` with `TempRepo.IsolatedEnvironment`: `git config --get-regexp ^branch\.local-work\.`; in the copy's `github/octo/hello.git`: `git rev-parse --verify --quiet refs/heads/local-work`. 2. Mark the requests. Right-click `Local branches` › `local-work`; read the menu. Click `Create pull request…`; wait for the form with `PullRequestBase` `origin/main`. Screenshot (`n02a-not-on-github`). (Steps 3 and 4 are N7's.) 5. Click `Draft`. Click `PullRequestDescription`; type `A note for the reviewers.` (N3). Mark the requests, the git requests and the log; take a quiet call mark. `GitHub.HoldNext("create:octo/hello")`. Click `Create`; wait for the hold's `Arrived`, for the status bar `Creating pull request…` and for the watchers to be quiet. In `github/octo/hello.git`, as in step 1: `git rev-parse refs/heads/local-work`. Screenshot (`n02b-creating`). Press Esc. 6. Dispose the hold. Wait for the operation's end, for `hello: listed 5 open pull requests of octo/hello` twice since step 5's mark and for the watchers to be quiet. Screenshot (`n03a-created-draft`, N3's). 7. Close the app. In `hello/`, as in step 1: `git config --get branch.local-work.remote`; `git config --get branch.local-work.merge`; `git rev-parse refs/heads/local-work refs/remotes/origin/local-work`; in `github/octo/hello.git`: `git rev-parse refs/heads/local-work refs/pull/6/head`. | **After step 1:** `--get-regexp ^branch\.local-work\.` exits 1 and prints nothing; `refs/heads/local-work` is not in `github/octo/hello.git` (exit 1).<br>**After step 2:** the menu: P12 step 15's lines (`Push local-work…`, then `Create pull request…`, in its push group), 519 high ±2. The form: `From` `local-work` `octo/hello`; `PullRequestHeadNote` `Not on GitHub yet: Create pushes it to origin/local-work first and sets that as its upstream.` (`settingsNote`, under the head, D313: no upstream); `Into` `origin/main` `octo/hello`; `PullRequestTitle` `Local change`, focused; the rest as N1's form. Requests since the mark none.<br>**After step 5:** the form creating (part N's conventions), still open after Esc (12.4); the status bar `Creating pull request…`, the progress bar indeterminate, `OperationCancelButton` hidden (D315: the cancel window ends before the POST). Git calls since the quiet mark, in order (`AssertCalledInOrder`): the push guard's `git config --null --get-regexp ^remote\.` (12.5 step 1, WP-C's guards), then `git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work` (D313), and no other `git push`. Git requests on `octo/hello` since the mark: a `POST …/git-receive-pack` answered 200 with `GivenUserName` `x-access-token` (D255); none with another user name. In `github/octo/hello.git`, `refs/heads/local-work` at `Local change`'s SHA: the push ended before the POST. No API request recorded since the mark (the held POST is recorded when it goes on, 2.3). Behind the form the refs as before the push: the queue pauses the watcher while the operation runs (D71, `OperationQueue`) and 12.5 reads the refs at its end, so `Remotes` › `origin` holds no `local-work` and `local-work` has no upstream.<br>**After step 6:** one operation since step 5's mark, Succeeded with `Created draft pull request #6` (N3); no dialog. Requests since step 5's mark: exactly one POST, 201 (N3's `Body`), then the two `GET …/pulls` of part N's conventions. The queue's records since step 3 (`Operations.Snapshot()`, the activity log's source): two operations, each `Create pull request for local-work`, the first Cancelled (N7), the second Succeeded with the push among its calls (the push's call carries its `OperationId`); no operation `Push local-work` (open point 47). The left panel: `Remotes` › `origin` holds `local-work`; `Local branches` › `local-work`'s `RefPullRequest` `#6`.<br>**After step 7:** `branch.local-work.remote` `origin`; `branch.local-work.merge` `refs/heads/local-work`; `refs/heads/local-work` and `refs/remotes/origin/local-work` one SHA, `Local change`'s; in `github/octo/hello.git` `refs/heads/local-work` and `refs/pull/6/head` that SHA. | | n/a | `n02a-not-on-github`, `n02b-creating` | n/a: 13.4 N repeats N1; a held request builds no R state (0.5). The push's running text is N7's picture. Behind the form in `n02b` the refs from before the push (no `Remotes` › `origin` › `local-work`, `local-work` without an upstream): the watcher is paused during the operation (D71) and the refs are read at its end, which step 6 shows. The push's undo entry is not asserted (open point 54). Varies by nature: the git box; the caret (`n02a`); the indeterminate progress bar's moving segment (`n02b`). |
| N3 | Draft (design 12.8, 12.4, 12.5, 10.3, 11.2, 6.3; D251): `Draft` ticked sends `draft: true` with the typed description; the list's row marked `Draft`; `Created draft pull request #6`; the description never logged; "hosting", one tab on the prepared `hello`, signed in, dark, 1100×700 | **Method 2**, steps 5 and 6 (N2's): `Draft` ticked and `A note for the reviewers.` typed before Create; Screenshot after step 6 (`n03a-created-draft`). | **After step 5:** before Create, `Draft` ticked, the description `A note for the reviewers.`, and N7's error line gone (12.4: changing Draft clears it).<br>**After step 6:** the POST's `Body` exactly `title` `Local change`, `body` `A note for the reviewers.`, `head` `local-work`, `base` `main` and `draft` true, without `maintainer_can_modify`; the fake's #6: open, `IsDraft` true, `Title` `Local change`, `Body` `A note for the reviewers.`, `Author` `octo`, `HeadRef` `local-work`, `HeadRepository` null, `BaseRef` `main`. The status bar `Created draft pull request #6` with the offer `Open in browser`. The section `5`: `#6 Local change` (`octo`, `PullRequestDraft` `Draft`) first, then P6's four, `Draft` in #6's and #2's rows only; #6's tooltip `#6 Local change`, a line break, `octo · local-work → main · Draft` (10.3). `hello`'s `ByRef` also `refs/heads/local-work` (an upstream of the same name now) and `refs/remotes/origin/local-work` → #6. The graph's row 0: its first label's `FullText` `local-work` with `Badge` `(6, "Local change", true)`. No log line since step 5's mark holds `A note for the reviewers.` (12.5); WP-N's Info line names the push to `origin/local-work` and the draft (open point 53). | | n/a | `n03a-created-draft` | n/a: 13.4 N (N1's create is the R one). One create shows N2 and N3 (open point 47). Where `local-work`'s name is cut beside its badge is inspected (open point 55). Varies by nature: the git box. |
| N4 | GitHub's 422 in the form (design 12.8, 12.5, 12.6, 6.3, 2.4; D312, D316, D338): a pull request from `topic/no-pr` into `main` opened on the fake after the list was read: Create's 422 "already exists", the list read again, `Pull request #6 is already open from topic/no-pr into origin/main.` and `Open pull request #6`, which opens it; `main` into `origin/topic/no-pr`: `origin/topic/no-pr already has every commit of main: there is nothing to merge.`; "hosting", one tab on the prepared `hello`, signed in, dark, 1100×700 | **Method 3** (`Check_N4_N8_already_open_the_form_and_no_commits`): 1. `ServeAsync()`. `PartN.PrepareAsync(served.Hello)`. `OpenAsync(data, served)`; wait for `hello: listed 4 open pull requests of octo/hello` and `hello: showing GitHub avatars for 2 authors` since the start and for the watchers to be quiet. `served.GitHub.AddPullRequest("octo", "hello", "Try an idea", "octo", "topic/no-pr", "main")` (open point 58). 2. Click `Local branches` › `topic`; right-click its `no-pr`; click `Create pull request…`; wait for the form with `PullRequestBase` `origin/main`. Mark the requests and the log. Click `Create`; wait for the operation's end, for `hello: listed 5 open pull requests of octo/hello` since the mark and for the watchers to be quiet. Screenshot (`n04a-already-open`). 3. Mark the requests. Click `Open pull request #6`. (Steps 4 to 7 are N8's.) 8. Mark the requests and the log; take a quiet call mark. Click `Create`; wait for the operation's end and for the watchers to be quiet. Screenshot (`n04b-no-commits`). Press Esc. | **After step 1:** the fake's octo/hello holds #6 open, from `topic/no-pr` into `main` (the fake's next number, 2.3); the section still `4` (nothing asks, D289); `topic` › `no-pr` without `#n`, its menu with `Create pull request…`.<br>**After step 2:** one operation, `Create pull request for topic/no-pr`, Failed; no dialog but the form (D312). Requests since the mark: exactly one POST, `Body` with `title` `Try an idea`, `head` `topic/no-pr`, `base` `main`, `draft` false, answered 422 (`Validation Failed`, `errors[0].message` `A pull request already exists for octo:topic/no-pr.`, 2.4); then exactly one `GET …/pulls`, `Conditional`, 200 (the list read again, 12.5 step 5); nothing more (a failure starts no refresh, D289). The fake still holds #1 to #6 only. The form: `PullRequestError` `Pull request #6 is already open from topic/no-pr into origin/main.`; under it `PullRequestOpenExisting` `Open pull request #6`; every control enabled, `Create` enabled; no `PullRequestHeadNote`. The status bar `Create pull request for topic/no-pr failed` (danger), no offer. Behind the form the section `5`, `#6 Try an idea` (`octo`) first; `topic` › `no-pr`'s `#6`. The log since the mark: WP-G's `GitHub: POST /api/v3/repos/octo/hello/pulls failed: 422 (request {id}).` and WP-S's `GitHub: creating a pull request on octo/hello failed: Validation, Validation Failed (request {id})`.<br>**After step 3:** `Launcher.Requests` exactly one `OpenUrl` `http://127.0.0.1:47395/octo/hello/pull/6`; the form closed (12.4: it completes with false); requests none; the status bar unchanged.<br>**After step 8:** one operation, `Create pull request for main`, Failed. Requests since the mark: exactly one POST, `Body` exactly `title` `Add the docs folder`, `body` empty, `head` `main`, `base` `topic/no-pr`, `draft` false, answered 422 (`errors[0].message` `No commits between topic/no-pr and main`); no `GET …/pulls` (12.5 reads the list again only for "already exists", a lost answer and a server error). Git calls since the quiet mark: no `git push` (`main` level with its upstream, D313). The form: `PullRequestError` `origin/topic/no-pr already has every commit of main: there is nothing to merge.`; no `PullRequestOpenExisting`; every control enabled. The status bar `Create pull request for main failed` (danger). After Esc no form; no request. | | n/a | `n04a-already-open`, `n04b-no-commits` | n/a: 13.4 N. The other 422s (head or base invalid, GitHub's own words) are `CreatePullRequestOperationTests`' (12.7). Varies by nature: the git box. |
| N5 | A remote branch's and a drop's (design 12.8, 12.3, 12.4, 10.4; D306, D313): `origin/topic/no-pr`'s `Create pull request…` (`ForRemoteBranch`) opens the form From `origin/topic/no-pr`; Create never pushes and makes #6 from `topic/no-pr`; `local-work` dropped on `origin/main` (`ForDrop`): `Create pull request…` opens the form Into `origin/main`, given, with N2's note; "hosting", one tab on the prepared `hello`, signed in, dark, 1100×700 | **Method 4** (`Check_N5_remote_branch_and_drop`): 1. `ServeAsync()`. `PartN.PrepareAsync(served.Hello)`. `OpenAsync(data, served)`; wait as method 2's step 1. 2. Mark the requests. Click `Remotes` › `origin` › `topic`; right-click its `no-pr`; read the menu. Click `Create pull request…`; wait for the form with `PullRequestBase` `origin/main`. Screenshot (`n05a-remote-branch-form`). 3. Mark the requests, the git requests and the log; take a quiet call mark. Click `Create`; wait for the operation's end, for `hello: listed 5 open pull requests of octo/hello` twice since the mark and for the watchers to be quiet. 4. Drag `Local branches` › `local-work` onto `Remotes` › `origin` › `main` (press, move held until the drag starts, release over the row); wait for the drop menu. Screenshot (`n05b-drop-menu`). Click `Create pull request…`; wait for the form. Screenshot (`n05c-drop-form`). 5. Mark the requests; take a quiet call mark. Press Esc. | **After step 2:** the menu: header `origin/topic/no-pr`; `Check out origin/topic/no-pr` (highlighted); `Merge origin/topic/no-pr into main`; `Merge origin/topic/no-pr into main (create a merge commit)`; `Rebase main onto origin/topic/no-pr`; `Interactive rebase of main onto origin/topic/no-pr…`; —; `Create branch here…`; `Create tag here…`; —; `Delete topic/no-pr on origin…` (danger); —; `Create pull request…`; —; `Compare origin/topic/no-pr with main`; `Hide origin/topic/no-pr`; `Solo origin/topic/no-pr`; —; `Copy branch name`: P12 step 16's, 415 high ±2. The form: `From` `origin/topic/no-pr` (open point 48) `octo/hello`, no `PullRequestHeadNote` (a remote branch is never pushed, D313); `Into` `origin/main` `octo/hello`; `PullRequestTitle` `Try an idea`; the rest as N1's form. Requests since the mark none.<br>**After step 3:** one operation, `Create pull request for origin/topic/no-pr`, Succeeded with `Created pull request #6`. Git calls since the quiet mark: no `git push`; git requests none. Requests since the mark: exactly one POST, 201, `Body` as N1's (`head` `topic/no-pr`, `base` `main`, `draft` false), then the two `GET …/pulls`; the fake's #6 as N1's. `hello`'s `ByRef` also `refs/heads/topic/no-pr` and `refs/remotes/origin/topic/no-pr` → #6; the status bar `Created pull request #6` with the offer.<br>**After step 4:** the drop menu: header `local-work → origin/main`; `Push local-work to origin/main` (highlighted); `Push local-work to origin/main and track it`; —; `Check out local-work and rebase it onto origin/main` (disabled: "local-work is up to date with origin/main"); `Check out local-work and rebase it onto origin/main interactively…`; —; `Create pull request…`; —; `Compare local-work with origin/main`: P12 step 18's, 215 high ±2. The form: `From` `local-work` `octo/hello` with N2's note; `Into` `origin/main` `octo/hello`, given (never `Reading GitHub…`, 12.4); `PullRequestTitle` `Local change`; `Create` enabled. The status bar still `Created pull request #6` with the offer (the form starts no operation).<br>**After step 5:** the form closed; no operation; requests none; git calls since the quiet mark: no `git push`. | | n/a | `n05a-remote-branch-form`, `n05b-drop-menu`, `n05c-drop-form` | n/a: 13.4 N. The other drop shapes (onto a local branch whose upstream has the same name, and one whose upstream has another name, left out) are `RepositoryMenusHostingTests`' (10.6). Varies by nature: the git box; the caret (`n05a`, `n05c`). |
| N6 | Signed out, the sign-in first (design 12.8, 12.3 step 1, 8.5, 10.4; D257, D306): signed out `Create pull request…` is offered, never disabled, and opens "Add GitHub account" first; Cancel there: no form, no request; a sign-in goes on to the form; "hosting", one tab on the prepared `hello`, signed out, dark, 1100×700 | **Method 5** (`Check_N6_signed_out_signs_in_first`): 1. `ServeAsync()`. `PartN.PrepareAsync(served.Hello)`. `OpenAsync(data, served, signIn: false)`; wait for `hello`'s `HostingState.PullRequests` `SignedOut`, the section's message row `Sign in to GitHub to see pull requests…` and quiet watchers. Mark the requests, the calls and the log. 2. Click `Local branches` › `topic`; right-click its `no-pr`; read the menu. Click `Create pull request…`; wait for `SignInDialog` with the keyboard focus in `SignInToken`. Screenshot (`n06a-sign-in-first`). 3. Click the dialog's `Cancel`. 4. Mark the requests. Right-click `topic` › `no-pr`; click `Create pull request…`; wait for `SignInDialog`. `TypeToken(OctoToken)`; press Enter; wait for the form with `PullRequestBase` `origin/main`, for `hello: listed 4 open pull requests of octo/hello` and `hello: showing GitHub avatars for 2 authors` since the mark and for the watchers to be quiet. Screenshot (`n06b-form-after-sign-in`). Press Esc. | **After step 1:** requests since the start none (D257).<br>**After step 2:** the menu: N1 step 2's lines, `Create pull request…` enabled (10.4; signed out `ByRef` is empty, so no `Open pull request #n` anywhere). Part A's "Add GitHub account" dialog (the window's entry, 8.5) stacked over `hello`'s window under its own backdrop, the box empty and focused; no `CreatePullRequestDialog`; requests none.<br>**After step 3:** no dialog and no form; still signed out; requests since step 1's mark none; the calls exactly `probe` (the dialog's keychain question, part A's ruling 2); no operation; the section as at step 1.<br>**After step 4:** requests since the mark: `GET api/v3/user` 200; then exactly one `GET api/v3/repos/octo/hello` (the hosting's A3, which the form's start shares, 7.8) and one `GET …/pulls`, each `TokenPresent`, 200; none 401; the calls gain `save github:127.0.0.1:47395:octo`. The form as N1's step 2 gives it, over the section's four rows. After Esc no form; no POST. | | n/a | `n06a-sign-in-first`, `n06b-form-after-sign-in` | n/a: 13.4 N (the real window signs in through Settings first). Varies by nature: the git box; the caret. |
| N7 | Cancel while pushing (design 12.5, 12.4, 12.6, 12.7; D315, D312, D69; open point 59): while Create pushes, the form's `Cancel` cancels the operation (the status bar's `Cancel` lies under the backdrop): no POST, nothing pushed, `Cancelled before the pull request was created.` in the form, which stays open; "hosting", one tab on the prepared `hello`, signed in, dark, 1100×700 | **Method 2**, after N2's step 2: 3. Mark the requests, the git requests and the log; take a quiet call mark. `GitHub.HoldNext("git:octo/hello")`. Click `Create`; wait for the hold's `Arrived` and the status bar `Pushing local-work to origin/local-work…`. Screenshot (`n07a-pushing`). Press Esc. 4. Click the form's `Cancel` (`CancelButton`); wait for the operation's end. Dispose the hold. Wait for the watchers to be quiet. In `hello/`, as in step 1: `git config --get-regexp ^branch\.local-work\.` and `git rev-parse --verify --quiet refs/remotes/origin/local-work`; in `github/octo/hello.git` step 1's `rev-parse`. Screenshot (`n07b-cancelled`). | **After step 3:** the form creating (part N's conventions) with `Cancel` enabled while the push runs (open point 59), still open after Esc; the status bar `Pushing local-work to origin/local-work…` (12.5 step 1), the progress bar indeterminate, `OperationStatus.CanCancel` true and `OperationCancelButton` shown under the backdrop, its tooltip `Stop git` (never clicked: the backdrop takes the click, D69). Git calls since the quiet mark: the push guard's `git config --null --get-regexp ^remote\.`; the held push is not among them yet, because the call log records a call when git ends, so after step 4 the calls since this mark are, in order (`AssertCalledInOrder`), that line, then `git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work`. No API request since the mark.<br>**After step 4:** one operation, `Create pull request for local-work`, Cancelled; the status bar `Create pull request for local-work cancelled` (secondary), no offer, no Cancel; no dialog but the form, still open (no lock file left, open point 52). The form: `PullRequestError` `Cancelled before the pull request was created.`; every control enabled again, `Create` reads `Create`; `PullRequestHeadNote` still N2's (read again, 12.4). Since the mark: no `create:` request; no `POST …/git-receive-pack`. The reads: `--get-regexp ^branch\.local-work\.` still exits 1, no `refs/remotes/origin/local-work` (exit 1); no `refs/heads/local-work` in `github/octo/hello.git` (exit 1). | | n/a | `n07a-pushing`, `n07b-cancelled` | n/a: a held request (0.5). The request the disposed hold lets go is not asserted (open point 52). Under open point 59's other way, step 4 and `n07b` go and step 3 asserts the form's `Cancel` disabled. Varies by nature: the git box; the indeterminate progress bar's moving segment (`n07a`). |
| N8 | The form's checks (design 12.4, 12.3, 12.6; D318, D104): `main`'s form opens Into `origin/main`, its own branch: `Choose a branch other than main to merge into.` and Create disabled; `Choose…` stacks the Pick of `origin`'s branches, Esc closing the Pick only; a chosen base clears the line; an empty title disables Create with `Enter a title.`; "hosting", one tab on the prepared `hello`, signed in, dark, 1100×700 | **Method 3**, after N4's step 3: 4. Mark the requests. Right-click `Local branches` › `main`; click `Create pull request…` (its push group's last item); wait for the form with `PullRequestBase` `origin/main`. Screenshot (`n08a-same-branch`). 5. Click `Choose…`; wait for the Pick. Screenshot (`n08b-choose-base`). Press Esc. 6. Click `Choose…`; click `origin/topic/no-pr`; click `Choose`. 7. Click `PullRequestTitle`; press Ctrl+A, then Delete; rest the pointer on `Create` until its tooltip shows. Screenshot (`n08c-enter-a-title`, the pointer kept). Click `PullRequestTitle`; type `Add the docs folder`. (Step 8 is N4's.) | **After step 4:** the form: `From` `main` `octo/hello`, no note; `Into` `origin/main` `octo/hello`; `PullRequestTitle` `Add the docs folder`; `PullRequestError` `Choose a branch other than main to merge into.` (open point 49); `Create` disabled; `Choose…` enabled. Requests since the mark none.<br>**After step 5:** the Pick (phase 3's `PickDialog`, its card over the form under its own backdrop, D104): title `Choose the branch to merge into`; `PickFilter` empty and focused; `PickList`'s items in the refs' order, `origin/docs/readme`, `origin/feature/a-very-long-branch-name-that-the-graph-must-cut`, `origin/feature/login`, `origin/main` (selected), `origin/topic/no-pr`, each with the detail `octo/hello` (open point 50) where its row leaves room for it: phase 6's row (its design 7.7) gives an item's text its own width and cuts the detail rather than the text, so the long item, whose text fills the row, shows none, while the dialog gives `octo/hello` as the detail of all five (`DetailOf`, each item's help text); no `owner:branch` item (`origin` is the listed repository, D318); `Cancel` and `Choose`. After Esc the Pick closed and the form as after step 4.<br>**After step 6:** the Pick closed; the form's `Into` `origin/topic/no-pr` `octo/hello`; no `PullRequestError`; `Create` enabled. Requests none.<br>**After step 7:** with the title empty, `Create` disabled and its tooltip `Enter a title.` shown (`ToolTip.ShowOnDisabled`); the placeholder `Title` shown; no error line. After typing, `Create` enabled. Requests none since step 4's mark. | | n/a | `n08a-same-branch`, `n08b-choose-base`, `n08c-enter-a-title` | n/a: 13.4 N. `Choose the branch to merge into.` (no base: A3 failed or still reading) is `CreatePullRequestDialogViewModelTests`' (12.7): A3 is kept from the list's read, so no check reaches it. Varies by nature: the git box; the caret (`n08a`). |
| N9 | A refusal, and a lost answer counted as created (design 12.5, 12.6, 2.4, 6.2, 7.6; D312, D316, D338): GitHub's 403 shows `GitHub refused to create a pull request in octo/hello with this token. It needs the permission pull_requests=write.` in the form; a POST whose answer is lost after GitHub made the pull request is found in the list read again and counts as created; "hosting", one tab on the prepared `hello`, signed in, dark, 1100×700 | **Method 6** (`Check_N9_refused_and_a_lost_answer`): 1. `ServeAsync()`. `PartN.PrepareAsync(served.Hello)`. `OpenAsync(data, served)`; wait as method 2's step 1. 2. Click `Local branches` › `topic`; right-click its `no-pr`; click `Create pull request…`; wait for the form with `PullRequestBase` `origin/main`. `GitHub.FailNext("create:octo/hello", FakeGitHubFault.Forbidden)`. Mark the requests and the log. Click `Create`; wait for the operation's end and for the watchers to be quiet. Screenshot (`n09a-refused`). 3. `GitHub.FailNext("create:octo/hello", FakeGitHubFault.CreatedThenDropped)`. Mark the requests and the log. Click `Create`; wait for the operation's end, then for a `hello: listed 5 open pull requests of octo/hello` line logged after the operation's end line (D289's refresh, which the queue starts only once it has logged the end), and for the watchers to be quiet. | **After step 2:** one operation, `Create pull request for topic/no-pr`, Failed; no dialog but the form. Requests since the mark: exactly one POST, `TokenPresent`, answered 403 `Resource not accessible by personal access token` with `X-Accepted-GitHub-Permissions` `pull_requests=write` (the fault, 2.4); no `GET …/pulls`; still signed in (only a 401 marks the account, 7.6). The form: `PullRequestError` `GitHub refused to create a pull request in octo/hello with this token. It needs the permission pull_requests=write.` (`Refused` with `Detail`: head and base in one repository); every control enabled. The status bar `Create pull request for topic/no-pr failed` (danger). The log since the mark: WP-G's `GitHub: POST /api/v3/repos/octo/hello/pulls failed: 403 (request {id}).` and WP-S's `GitHub: creating a pull request on octo/hello failed: Forbidden, Resource not accessible by personal access token (request {id})`. The fake has no #6.<br>**After step 3:** one operation, Succeeded with `Created pull request #6` (D316); the form closed; the status bar `Created pull request #6` with the offer. Requests since the mark: exactly one POST (`Body` as N1's), the connection then closed without an answer (`CreatedThenDropped`, 2.4); then `GET …/pulls`, the first answered 200 with #6 (the list read again, 12.5 step 5), the last of route `pulls:octo/hello` since the mark `Conditional` and 304 (D289's, after the operation's end); none 401 (open point 57). The fake's #6 as N1's. The section `5`, `#6 Try an idea` first; `hello`'s `ByRef` as after N1's create. | | n/a | `n09a-refused` | n/a: faults (0.5). The other rows of 12.6 (401, 404, a rate limit, single sign-on, a fork's token, a rejected, a declined and a refused push, `GitHubSaid` through "already exists" not listed) are `CreatePullRequestOperationTests`' (12.7); no test reaches the "any other 422" arm, because the fake answers the form no 422 beyond the four 12.6 names (the lead's ruling at WP-N's merge). Step 2's `X-Accepted-GitHub-Permissions` is shown by the form's text with `Detail`, because the fake's record keeps no answer headers; the Warnings' `{id}` is any id, because it keeps no request id. Varies by nature: the git box. |
| N10 | The form at 1920×1080 and in light (design 12.8, 12.4; no new token): N1's form centred at 1920×1080, dark, then light after the toolbar's Theme switch; "hosting", one tab on the prepared `hello`, signed in, dark then light, 1920×1080 | **Method 7** (`Check_N10_the_form_at_1920x1080_and_light`): 1. `ServeAsync()`. `PartN.PrepareAsync(served.Hello)`. `OpenAsync(data, served, width: 1920, height: 1080)`; wait as method 2's step 1. Mark the requests. 2. Click `Local branches` › `topic`; right-click its `no-pr`; click `Create pull request…`; wait for the form with `PullRequestBase` `origin/main`. Screenshot (`n10a-form-1920`, the pointer at (760, 1000)). Press Esc. 3. Click the toolbar's Theme switch (light). Right-click `topic` › `no-pr`; click `Create pull request…`; wait for the form. Screenshot (`n10b-form-1920-light`, the pointer at (760, 1000)). Press Esc. | **After step 2:** the form as N1's step 2 gives it, its card at x 730 to 1190, centred vertically ±1, every control placed as at 1100×700 relative to the card (±1); no text cut but by an ellipsis. Behind it the window at 1920×1080 with the graph's six columns. Requests since the mark none.<br>**After step 3:** the same form, texts and places in the light theme's brushes (the card `#F5F6F8`, `From`, `Into` and the repositories `VcTextSecondaryBrush` `#5C6572`, `Create` `VcPrimaryButton`). After Esc no form; requests none since step 1's mark. | | n/a | `n10a-form-1920`, `n10b-form-1920-light` | n/a: 1920×1080 and light are scripted only (13.1). The rest point (760, 1000) is the backdrop under the card (open point 56). The places relative to the card are `PartN.FormLayout`'s, measured at 1100×700 and asserted there by N1 (±1). Varies by nature: the git box; the caret. |

## What proves each Delivers item

`plan.md`'s phase 7a "Delivers", against parts A, B, P, G and N; "Elsewhere" names the work packages' own
classes (design 0.1).

| Delivers item | Part A | Part B | Part P | Part G | Part N | Elsewhere |
|---|---|---|---|---|---|---|
| The provider model for 7b | — | — | P6 (recognition, and the listed repository a fork's parent through A3 and A4, 0.2), P1 (a remote GitHub does not have) | G5 (a remote GitHub does not have: no badge, no avatar, no request naming it) | N1, N2, N5: the default base from A3 with one GitHub remote (N1; 12.3, D318), the head's place for an upstream of the same name (N1), for none (N2) and for a remote branch (N5; D313) | WP-G's and WP-S's classes; `CreatePullRequestOperationTests` (12.7: a fork's head; the remotes' order, a fork's parent's default branch and an `owner:branch` base, open point 61) |
| Sign-in to GitHub with a token in Settings, Accounts; keychain, else the session; one account (D249, D253, D256, D258, D263; R3) | A1 to A11 (the keychain through `FakeTokenStore`); the real window repeats A1, A2 and A5 | B7, B8: the sign-in the palette's "Clone from GitHub…" asks for first, and a cancelled one (D300); the real window repeats B7's | P2, P4: the section's sign-in row and the window's entry (8.5); P11 shows "Sign in to GitHub…" in the section's signed-out menu, not clicked (every sign-in item ends in that entry, 8.5) | — | N6: a create signed out signs in first through the window's entry (12.3, 8.5); a cancelled sign-in opens no form | the real stores: WP-K's classes on CI (D259, D333); the section menu's sign-in item and its action: `RepositoryMenusHostingTests` (the linked `RecordingActions`, 10.6) |
| The account's token on the app's git calls to github.com (D255) | A9 (the fetch's credentials from the app's helper as `x-access-token`, git's refusal), A11 (the switch) | B6: a clone through the app's helper as `x-access-token`, no credentials dialog, no credential written; the real window repeats it in the real process | P13: #5's fetch of `refs/pull/5/head` through the app's helper as `x-access-token` | — | N2: Create's push through the app's helper as `x-access-token` | WP-C's classes; `WelcomeCloneFromGitHubTests` (a typed URL, another user's URL, D331's rule) |
| Browse and clone (D261) | — | B1 to B9; the real window repeats B3, B6 and B7 | — | — | — | `CloneFromGitHubDialogViewModelTests`, `CloneFromGitHubDialogViewTests`, `WelcomeCloneFromGitHubTests`, `PaletteCloneFromGitHubTests`, `HostingTextsDescribeTests` (9.6) |
| Pull-request list and menu, create, badge, open in browser (D251) | — | — | P1 to P15: the section's states and rows, the filter, a click, the branch rows' `#n`, the menus, checkout, copy link, open in browser; the real window repeats P6, P11 and P15 (the default browser, D321) | G1, G2: the graph's badge on a local and on a remote branch's label, a long name cut with the badge kept, the tooltip; none signed out (G6) or without a GitHub remote (G5); the real window repeats G1's capture | N1 to N10: the form and its checks, a create without and with a push, a draft, Cancel during the push, the 422s, a 403 and a lost answer, the created pull request in the list, on the branch row and on the graph's label, and the status bar's "Open in browser" (D317); the real window repeats N1 | `OperationQueueOfferTests`, `StatusBarOfferTests`, `CreatePullRequestDialogViewModelTests`, `CreatePullRequestOperationTests` (12.7); `GraphPullRequestBadgeTests` (11.4); `PullRequestSectionTests`, `PullRequestCheckoutTests`, `RepositoryMenusHostingTests`, `LeftPanelPullRequestsViewTests`, `PullRequestOperationsTests`, `GitRepositoryPullRequestsTests`, `PullRequestFetchHeadTests` (10.6) |
| Avatars from GitHub only (D252) | A8 in part: a sign-out leaves no `avatars/` (D291) | — | — | G3 (the Author column at 1920×1080 and the details panel, an unknown author as today, the images fetched without the token into `avatars/`), G4 (light), G5 (no GitHub remote), G6 (signed out, and after a sign-out that followed the avatars: no image, no `avatars/`, D291), G7 (the authors' read failed); the real window repeats G1's capture, the details panel's image (open point 35) | — | `GraphAvatarTests`, `CommitDetailsAvatarTests`, `RepositoryAvatarsTests` (11.4); `AvatarCacheTests`, `GitHubProviderTests` (6.5) |
| The opt-in live command (D250) | — | — | — | — | — | WP-LIVE |
| From phase 3: "Create pull request" in the branch, remote-branch and drop menus; the section's menu | — | — | P11 (the section's menu, D307), P12 (`Open pull request #n` and `Create pull request…` in branch menus, `feature/login`'s and `local-work`'s, remote-branch menus, `origin/docs/readme`'s and `origin/topic/no-pr`'s, and drop menus, D306); P1 (none without a GitHub remote) | — | N1 (`ForLocalBranch`), N5 (`ForRemoteBranch` and `ForDrop`), N6 (signed out) and N8 (`main`'s): each item opens the form; N1 (the section's menu holds no create item, 0.1) | `RepositoryMenusHostingTests` (10.6); the form's view model: `CreatePullRequestDialogViewModelTests` (12.7) |
| From phase 3: a provider's token reaches git | A9 | B6 | P13 | — | N2 | WP-C's classes |
| From phase 6: the push guard; the prompt group per account | — | — | — | — | N2, N7: the guard's read before Create's push (12.5 step 1); no refusal in the scenario | WP-C's classes (the guard's refusals) |
| From phase 6: the wheel test; the real-window csproj's comment | — | — | — | — | — | L1 (`ShellDriverTests`; the file) |
| From the review: W1, S8, R8; G23's split | — | — | — | — | — | WP1 |

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

### Part B: the clone form, the palette and one earlier test (D261, D299; design 0.7, 9.3, 9.5, 9.8)

Part B changes no earlier picture on purpose, and one earlier test's list:

- **The clone form** (9.3): "From GitHub…" is collapsed while no account is signed in, and a collapsed
  child takes no room in its `DockPanel`; both harnesses start with GitHub off (rulings 23, 24), so every
  earlier picture of the form shows it as before: phase 1's `13a-clone-form`, `13b-clone-in-progress`,
  `14a-clone-failed` and `14b-clone-cancelled` (9.3's "13a to 14b"; `13c-cloned`, in that range, shows
  the cloned tab rather than the form and stays too) and phase 6's `l26b-clone-form-dark`,
  `l26b-clone-form-light`, `l32b-clone-failed-dark` and `l32b-clone-failed-light`. No real-window
  picture of an earlier phase shows the form.
- **The palette** (9.5, D299): "Clone from GitHub…" is listed only while GitHub is not off, so phase 6's
  palette pictures (`k10a` to `k10f`, `l31c-palette-dark` and `l31c-palette-light`; the real window's
  `k10a` to `k10d` and `l31c-palette-light`), K10's lists and `PaletteEntriesTests` stay; it has no
  key, so the Keyboard shortcuts list (`s14e`, `s14f`, which change only by part A's nav button) gains
  no row.
- **One earlier test changes on purpose**: `AppShortcutsTests.The_commands_without_a_key_are_6_1s_list`
  (`tests/VisualCommit.App.Tests/AppShortcutsTests.cs:258-274`): `AppCommand.CloneFromGitHub` joins
  `withoutKey`, with a comment naming D299, which amends D199's list (9.1, 9.6). The command comes after
  `FollowSystemTheme` in `AppCommand` (`AppCommand.cs:55`), so no earlier value moves.
- **What 9.8's "no earlier picture changed beyond D248's box" means.** Each picture of the form and of
  the palette above shows the status bar's `Git 2.56.0` box, so its file differs from the last gate's
  list (`docs/test-reports/phase-6-pictures.sha256`, made with Git 2.36) in that box whatever WP-B does
  (D248), and `sha256sum -c` against that list proves nothing for it. At WP-B's merge the lead makes
  references on `master` with the same git and compares WP-B's pictures with them by region
  (`scripts/compare-pictures.py`; design 0.7, 9.3): of phase 1's checks 13 and 14 and phase 6's L26 and
  L32, which 9.3 names, and, since 9.8's sentence covers the palette's pictures too, of phase 6's K10
  and L31, scripted (wave 1 had no reference of `k10e` and `k10f`); the real window's `k10a` to `k10d`
  and `l31c-palette-light` are compared at the gate (the shared conventions' "Earlier pictures"). The
  binding criterion is 9.3's: no difference outside the status bar's git box, and none is expected
  inside it against references made with the same git. Wave 1's comparison with its references
  (2026-10-29) found differences outside it where wave 1 changed nothing, in what varies by nature: a
  focused box's caret (`l26b-clone-form-light`, `k10a`) and the temporary folder's name in the parent
  box and in git's message (`13b`, `14a`, `14b`, `l32b-clone-failed-dark` and `-light`). The report
  reads 9.3's criterion as allowing these too (a caret in `13a`, `l26b` and the palette's box, the
  folder's name in `13b`, `14a`, `14b` and `l32b`), each difference opened and judged, and any other
  difference as WP-B's defect; that reading waits for the lead's ruling (open point 19), and until it,
  9.3's sentence binds.

### Part P: part A's check 9 and the left panel (design 0.4, 0.7, 8.7, 10.8)

Part P changes the pictures of one earlier check on purpose, and no earlier test:

- **A9's pictures** (`a09a-needs-sign-in-api`, `a09b-git-refused`, `a09c-needs-sign-in-git`). From WP-P's
  merge the left panel of `hello`'s window behind their backdrops (in `a09a` and `a09c` the part left of
  the settings card) shows WP-P's section in place of the placeholder: the header `Pull requests` with
  the chevron down and `0`, and under it one row `Sign in to GitHub to see pull requests…` (10.2; cut
  with "…" at 260, as in P2), so
  the `Tags` and `Stashes` headers lie 26 lower; no branch row shows a `#n`. In each of the three the
  account needs sign-in, which `RepositoryHosting` shows as `SignedOut` with no list (7.8); A9 takes no
  picture while signed in (its step 4's rows are pictured by P6's `p06a-rows`). Nothing else in them
  changes, and A9 asserts nothing in that region. Reason: D251 and D303: a repository with a GitHub
  remote shows the section's states, and A9's `hello` has one. The lead runs A9 again at WP-P's merge
  and inspects its three pictures (10.8, 0.4; open point 31), and again at WP-V's (11.6), where they do
  not change further: needing sign-in, the graph shows no badge or avatar (D257).
- **Everything else stays** (10.8, 0.7). A repository without a GitHub remote, and every start with
  GitHub off (both harnesses' default, rulings 23 and 24), keeps the placeholder (`03e-no-menu`, which
  10.6 runs unchanged), every branch, remote-branch and drop menu (10.4's hooks give nothing without
  hosting), the palette (`RepositoryItems` adds pull requests only for a listed repository) and the
  branch rows (`RefPullRequest` collapsed takes no room, D308), so no earlier phase's picture changes;
  part A's other pictures are of empty tabs and part B takes none of an opened GitHub repository (9.7).
  No earlier test changes: 10.6's builder runs `LeftPanelViewModelTests`, `PanelViewTests`,
  `RepositoryMenusTests`, `RepositoryMenusKeyTests`, `PaletteEntriesTests` and
  `RepositoryOperationsCheckoutTests` unchanged. The comparison at WP-P's merge is as ruling 19 set it at
  WP-B's: by region against the last full run's pictures, the git box, A9's region above and what varies
  by nature set aside.

### Part G: part P's and A9's graph and details panel (design 0.4, 11.6, 17 item 3; D309, D310)

Part G changes, on purpose, the pictures of part P's checks, which the lead runs again at WP-V's merge
(17 item 3; open point 29) with part A's check 9 (11.6), and no earlier phase's picture or test. From
WP-V's merge, in `hello`'s window signed in with a list, at 1100×700, where the graph shows Branch /
Tag, Graph and Message only (the Author column is hidden, open point 35), with the widths part G
measured (open point 41):

- **The badges** (11.2). Part P's `hello` is not prepared, so `feature/login` and `origin/feature/login`
  share the row `Add the login form`: there `feature/…` with `#1` (x 6 to 100.2: the name cut so that
  the badge and the `+1` fit) and `+1` (`origin/feature/login`, x 104.2 to 128.9), where `feature/login`
  and `+1` were; on `Update the readme` `origin/docs/r…` with `#2` (to 128.2), where `origin/docs/readme`
  was whole; on `Shorten long labels` `origin/featur…` with `#3` (to 126.0), where `origin/feature/a-ve…`
  was; every other row as before. So in `p06a`, `p11c`, `p11d`, `p09a`, `p09b`, `p10a`, `p08a`, `p08b`,
  `p12a`, `p12b` and `p13a` (whose top row, `fix-typo`, HEAD and without upstream, has none, D313), and
  in the real window's `p06a-rows` and `p11c-section-menu`. In `p07a` (#1 not listed, `topic/no-pr`'s
  #6 is): `Try an idea` `origin/topic/…` with `#6` (to 125.2), `#3` and `#2` as above, `Add the login
  form` as before. In
  `p06b` (`tool`): `Fix a typo in the readme` `fix-typo` with `#7` (to 92.7), then `+1` (96.7 to 121.4),
  where `fix-typo` and `+1` were.
- **The avatars** show only in a details panel with a commit by octo or ada selected: in `p09a`, whose
  `Add the login form` (Ada Lovelace) P9's click selects, ada's image before the author and the text 22
  further (11.3), nothing else in the panel moving; and so in `p13a` unless the checkouts leave nothing
  selected: both commits the selection can then be on, `Add the login form` (#1's double-click selects
  it, D304) and the new top row `Fix a typo in the greeting`, are Ada Lovelace's, and the panel's image
  follows the e-mail (11.3); the inspection at the merge settles which commit is selected. Both are
  taken once the image shows (`Shot`'s wait, open point 44). No part P picture shows the graph's Author
  column, so P3's Notes and open point 29 ("avatars in the Author column … in `p03a` and `p04a`")
  change nothing visible there (open point 35).
- **Unchanged**: `p02a` and `p11a` (signed out), `p04a` and `p04b` (failed: no list, no badge), `p03a`
  and `p11b` (loading without a list), `p05a` (no open pull request), `p01a` (`ops`), and A9's `a09a`,
  `a09b` and `a09c` (needing sign-in: `SignedOut`, `ByRef` empty, `Avatars` `None`, D257). `a09b` comes
  after step 4's list and avatars, which leave the graph through the same posts as the section's rows
  (the hosting's `Changed`, then `OnHostingChanged`), so open point 31's possible wait for `SignedOut`
  before it covers the badges too.
- **Waits and requests.** Part P's conventions already wait for `AvatarsShown` and count no `commits:` or
  `avatar:` request exactly; `tool`'s line counts 1 author (open point 42). P4's "nothing else" gains
  `commits:octo/hello`, `avatar:1001` and `avatar:1003` (A3 answered; only the list failed). From WP-V's
  merge `Shot` also waits for the details panel's image (open point 44). At the merge
  `AppLogFile.AvatarsShown` (11.1) exists, and the waits that A9 step 4 and B6 step 8 name "from WP-V's
  merge" are added to their code (`Phase7aChecks.A.cs`, `.B.cs`) with it, as part P's are to its files;
  B8 step 4's would come only while `hello`'s tab is active, which it no longer is, so B8 waits for the
  avatars' requests instead (open point 43). The lead runs A9, B6 and B8 again then, beside part P's
  checks (11.6 names A9, 17 item 3 part P).
- **Everything else stays** (11.6, 0.7): every earlier start has GitHub off (`NotGitHub`,
  `AuthorAvatars.None`), so no earlier picture of the graph or the details panel changes; no `FullText`
  changes, so no earlier test does (11.4's list runs unchanged); part A's other pictures are of empty
  tabs and part B takes none of an opened GitHub repository. The comparison at WP-V's merge is 11.6's,
  read as ruling 19 set it at WP-B's: by region against the last full run's pictures, the git box, the
  regions above and what varies by nature set aside.

### Part N: the status bar's offer (design 12.2, 12.8, 12.9; D317)

Part N changes no earlier picture and no earlier test on purpose (12.8, 12.9):

- **The status bar** (12.2). `OperationOfferButton` shows only while the bar shows a succeeded user
  operation's result that carries an offer, and only a create gives one; hidden, it takes no room, so
  every earlier picture of the status bar is as before: phase 3's walk-through's operation texts (its
  results, `{Title} failed` and `{Title} cancelled`), phases 4 to 6's, and part A's `a09b-git-refused`
  (a failure: no offer). A dashboard's rows have none (`DashboardBulkViewModel`: null, false and a
  command that cannot run; `CompactText` unchanged), so phase 6's dashboard pictures stay.
- **The queue** (12.2). `OperationResult.Offer` is null but for a succeeded report that carries one, and
  `ReportStaleLocksAsync` keeps it; no earlier operation makes one, so no earlier result, status text,
  log line or count of git calls changes. 12.7's builder runs `OperationQueueTests`,
  `OperationQueueUiTests`, `OperationStatusViewModelTests`, `StatusBarTests`, `DashboardBulkViewModelTests`,
  `RepositoryOperationsRemotesTests`, `PickDialogViewModelTests`, `DialogTests` and
  `MessageDialogViewModelTests` unchanged.
- **Menus and the earlier parts.** WP-N changes no menu (the items are WP-P's, 10.4): P12's `Create pull
  request…` lines stay as written, and part N clicks them; no picture of parts A, B, P or G shows the
  form, the offer or a created pull request.
- **The comparison at WP-N's merge** (12.9). The lead runs phase 3's walk-through
  (`VisualCommit.VisualTests.Phase3`, through `scripts/heavy-run.sh`), every picture of which shows the
  status bar, and compares it by region with references that 12.9 calls `master`'s and the report
  reads as ruling 19 set them at WP-B's merge, the last full run's pictures (open point 60): a
  difference is allowed only in the status bar's git box (D248) and in what varies by nature,
  each opened and judged, and none in the status bar's middle, where the offer would lie; then part N.

### Stays as it is

Every other earlier picture and expectation: both harnesses start with GitHub off and the keychain in
memory (rulings 23, 24), so the welcome page and its clone form (no "From GitHub…", D261), the palette
(no Accounts or sign-in entry and no "Clone from GitHub…", D299), the left panel's "Pull requests"
placeholder, every menu, and the graph and the details panel (no badge and no avatar, 11.3) stay as
they are, and no earlier count of git calls moves (signed out, a clone reads no configuration, 9.3).
Pictures that show the status bar's git box differ from `phase-6-pictures.sha256` for D248 whatever
parts A, B, P, G and N do; they are compared by region with `master`'s references made with the same git.

### Changed in this report after its first commit

A method split when the checks first run, a mark for the real-window pass changed after it was written,
or an expected result changed for a reason the build or the gate finds, goes here with its reason.

- **Part N as built** (2026-10-30, WP-N's merge, the lead's record after the build's review; no check
  loosened):
  - **N8 step 5:** the Pick's long item `origin/feature/a-very-long-branch-name-that-the-graph-must-cut`
    shows no detail in `n08b`. Phase 6's row (its design 7.7) gives an item's text its own width and cuts
    the detail rather than the text, and this text fills the row. The expected result assumed every row
    had room; it now says so, and the check still asserts the detail `octo/hello` of all five items
    through the dialog.
  - **N7 step 3, N2's held push:** the call log records a call when git ends, so while the push is held
    only the push guard's call is in it; the push's line is asserted, in order after the guard's, once
    step 4's Cancel has ended it. Step 5's held POST comes after the push, so N2's list is unchanged.
  - **N2 step 6:** the two operations are read from the queue's records (`Operations.Snapshot()`), the
    activity log's source, and the push's call by its `OperationId`, not from `ActivityLogModel`, whose
    rows are built for the worktree a view shows.
  - **N9 step 2 and the Warnings:** the fake's record keeps no answer headers and no request id, so the
    403's `X-Accepted-GitHub-Permissions` is proven by the form's text with `Detail`, and `{id}` is any id.
  - **Measured, within the conventions' allowance:** the card 386 high (design 12.4's "about 415", which
    nothing asserts); `From`, `Into` and the title box at x 341, the card's 1-px border and 20 padding,
    within ±1 of 340. N1 measures the form's 14 places relative to the card (`PartN.FormLayout`) and N1
    and N10 assert them.
  - **Varies by nature:** `n02b` and `n07a` also differ between runs in the indeterminate progress bar's
    moving segment, now in N2's and N7's Notes.
  - **12.6's rows:** `CreatePullRequestOperationTests` now covers a rejected and a declined push (real git
    state: a moved branch, a `pre-receive` hook) and `GitHubSaid`; the "any other 422" arm stays
    untested, since the fake answers the form no 422 beyond the four 12.6 names (N9's Notes).

- **How part G proves its places** (2026-10-30, CI runs 40993671970 and 41001188676, macOS; the lead's
  record): G1's check read where the name's ink ends in `g01a` to ±1 px, and macOS's glyph
  rasterisation ends it at x 79 against the layout edge 80.4 (its glyphs' outline ends at 79.72), so
  the check failed there while the drawing was right. The places are layout facts: the control now
  gives its tests the places its drawing uses (`BadgeInRow`, `AuthorInRow`), and G1's three positions
  and the Author column's image and name places are compared with the report's exactly, to one
  decimal. The pictures' ink is held by rules that hold on every platform: a text's ink within its
  glyphs' outlines (the bundled Inter, unhinted) ±1; the icon's within its stroked outline and an
  image's within its square, with no allowance (Skia rasterises paths and images alike everywhere);
  none in the gaps between them or in the label's padding. No expected result changed.

- **A8 step 6, the second sign-in's `GET api/v3/user`** (2026-10-29, WP-A's build, the lead's record):
  the same token signs in again in one app session, so WP-G's client sends it conditionally with the
  ETag it kept from the first answer (design 6.2, 16 item 10), and the fake answers `304`, not `200`.
  The check expects a conditional request answered `304` at this step only; every other step that
  expects `200` asserts exactly that.
- **Part B, `octo/tool`'s row** (2026-10-29, WP-B's merge, the lead's record): the lead's ruling 11 gave
  `octo/tool` the description `Small command-line tools.`, so its row's detail is `Fork · Small
  command-line tools.` in B3, B4 step 6, B8 and B9 (the scenario facts and the rows above say so), not
  `Fork` as first written; the filter on `private` still keeps two rows. Under the open browser the
  form's "From GitHub…" is drawn disabled (its command is the one running), which "visible" covers.
- **B8 step 4's wait from WP-V's merge** (2026-10-29, part G's checks, the lead's ruling on open point
  43): the step named `AvatarsShown("hello")`, but WP-V's line comes from the window's one graph,
  which shows the active tab, and the sign-in there makes the new empty tab active before `hello`'s
  avatars can come, so the line would not come while B8 runs. From WP-V's merge the step waits instead
  until the fake has recorded `avatar:1001` and `avatar:1003` and `hello`'s view model's `Avatars`
  maps octo's and ada's addresses; B8 takes no picture, and its expected results are unchanged.
- **P13 step 1's wait** (2026-10-30, CI run 40982132445 on `ccd1ebe`, macOS; the lead's record, after the judges' verdict): the
  sign-in lists `tool`'s pull requests too, though it is the tab in the background (design 7.8's
  triggers, D289), and GitHub's client sends one request at a time (D285). When `hello`'s list goes
  first, `tool`'s `GET api/v3/repos/acme/tool/pulls` is answered after `hello`'s line, once the fake has
  synced acme/tool's refs. On macOS it was recorded after step 2's mark, and step 2's "requests none"
  failed (its first list: not conditional, 200). Step 1 now also waits for `tool: listed 2 open pull
  requests of acme/tool` since the start; not for `tool`'s `AvatarsShown`, which only the shown tab
  writes (11.3) and so comes in step 4. The expected results are unchanged; P6 step 4's wait, since the
  start, now finds its list line at once.

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

To try by hand on a Mac (CI runs parts A, B, P, G and N headless with the fake keychain and the fake of GitHub,
and the real store's class against a throw-away keychain; these are what only a person on a Mac sees):

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
5. **Clone from GitHub** (part B; D261, D255, D300): signed out, Cmd+Shift+P, "Clone from GitHub…": "Add
   GitHub account" comes first; after signing in, the clone form opens with "Clone from GitHub" over it,
   listing your repositories, the most recently updated first, a private one marked "Private". Choose a
   private one, a parent folder, then Clone: it clones with no credentials dialog and no keychain prompt
   (the app's helper runs through macOS's `/bin/sh`); `git config --local --get-regexp credential` in the
   clone prints nothing, and Keychain Access holds no new `github.com` internet password.
6. **Pull requests** (part P; D251, D305): in a clone of a github.com repository with open pull
   requests, signed in, the left panel's "Pull requests" lists them newest first with number, title and
   author, a draft marked "Draft". Right-click one: "Open in browser" opens the default browser once, on
   its page; "Check out …" of a pull request from a fork fetches it with no credentials dialog and makes
   a local branch without upstream, and `git config --get-all remote.origin.fetch` still prints only
   `+refs/heads/*:refs/remotes/origin/*`.
7. **Badge and avatars** (part G; D309, D252): in the same clone, signed in, the graph's label of a branch
   with an open pull request shows the pull request's icon and `#n` after its name (a long name is cut
   with "…", never the badge).
   In a window wide enough for the graph's Author column, authors with a GitHub account show a small
   round picture before their name, and the details panel shows it before a selected commit's author;
   signed out, neither shows.
8. **A new pull request** (part N; D313, D317): in the same clone, signed in, right-click a local branch
   that is not on GitHub and choose "Create pull request…": the form says "Not on GitHub yet: Create
   pushes it to origin/<branch> first and sets that as its upstream." and merges into the repository's
   default branch. "Create" pushes with no credentials dialog and no keychain prompt, then the status
   bar reads "Created pull request #<n>" with "Open in browser", which opens its page once; the list
   shows it first.

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

Part B's points (11 on) were written on 2026-10-29, before WP-B starts; each waits for the lead's ruling.

11. **A described repository (part B: B3, B4, B9; design 9.7, 2.6).** 9.7 pictures the browser "listed
    with private, fork, archived and described repositories", but no repository of the scenario as built
    has a description: 2.6 names none, `ServeHostingAsync` passes no `description:`, and
    `FakeRepository.Description` is set only by `AddRepositoryAsync`. So `b03a` shows `Private`, `Fork`,
    `Private`, `Archived` and no description, and the ` · ` join and a description's place are proven only
    by `CloneFromGitHubDialogViewModelTests` (9.6, "rows in order with each detail form"). The report's
    reading: the scenario gains one description, on `octo/tool` (its row then reads `Fork · ` and the
    text, the join 9.4 gives), passed as `description:` to `ServeHostingAsync`'s `AddRepositoryAsync("octo",
    "tool", …)`, by the lead in a commit of its own before WP-B's branch is cut (the scenario is every
    part's, and `Phase7aScenarioTests` pins it), or by WP-B's builder in its part B commit with the lead's
    leave. The text is the lead's to choose (the design gives none); it must not contain `private`, or
    B4's filter would keep `octo/tool` too. The scenario is code both projects compile, so the R picture
    may show it and 13.4 B still names no preparation (a `PartB.PrepareAsync(TempRepo hello)` could not
    reach the fake). Until the ruling, `PartB.Rows`' second row is `octo/tool` `Fork`, and B3, B4 and B9
    depend on it. **Ruling:** WP-B's builder gives `octo/tool` the description `Small command-line tools.` in the
    scenario's data (design 2.6's fake), with the fake's own tests; it holds no "private".
12. **Which failure is pictured (part B: B5; design 9.7, 1.7).** 9.7 names "failed (`FailNext("user/repos",
    …)`)" without a fault. The report pictures `ServerError`, one fixed sentence that changes no account,
    and, beyond 9.7's list, `BadCredentials`: the one failure of the list that changes the account (the
    API's 401 marks it at once, 7.6, D330), and so the one place where D302's rule shows in the app ("From
    GitHub…" gone under the browser). The other kinds stay with 9.6's unit tests. **Ruling:** accepted: `ServerError`, and `BadCredentials` for D302.
13. **The login in the refused text (part B: B5; design 9.3, 9.4).** 9.4's table words a failure as
    `HostingTexts.Describe(Failure, login)`, "`login` the signed-in account's", but 9.4's constructor is
    `(list, log)`, and 9.3 makes the dialog with `_services.Hosting.ListRepositoriesAsync` and
    `_services.Log` only, which name no login. The report expects `GitHub refused the token of octo. Sign in
    to GitHub in Settings, Accounts.` whichever way WP-B passes the login (a constructor parameter, or the
    session's account read when the failure is applied: a needs-sign-in account keeps its login), as 9.6's
    view-model test expects `OperationTexts.TokenRefused(login)`. **Ruling:** accepted: the login is the session's signed-in account's (`IHostingSession.Account`) when
    the list fails, as 9.4's text needs; the report's expected text stands.
14. **The cap and single sign-on pictured (part B: B9; design 9.7).** 9.7 lets them be pictured "else the
    unit tests prove them". The report pictures both in one start, scripted only
    (`AddListedRepositories("octo", 1001)` before it, `SsoPartialResults` on the first page): where the two
    lines lie under the list, and the list at its largest with its scroll bar, are layout that no unit test
    shows; the cost is ten loopback requests and a list of 1,000 rows. **Ruling:** accepted.
15. **Steps beyond 9.7's list (part B)**, with texts and places the design gives: B1's signed-out form (the
    button collapsed, the URL box at full width; 9.3, 9.6); B4's filter typed while loading (9.4: "what it
    holds filters the rows when they come"), the hidden selection cleared and the order restored (9.4); B5's
    refused token and D302; B6's checks of 9.3's configuration read before the clone (D331), of 4.6's log
    line, of the clone's other `.git/config` keys (9.5's git fact) and of `hello`'s binding after the clone;
    B7's order in the empty palette (9.5); B8's sign-in from a repository tab, which opens a new tab (9.5,
    5.10's `TabForOpening`), and Esc on the browser leaving the form open (D300's last clause; B4's step 8
    too). **Ruling:** accepted.
16. **A cancelled sign-in from a repository tab, without a picture (part B: B8; D300).** "Makes no tab"
    can only fail where a tab would be made, so B8 starts on `hello`'s tab, where `TabForOpening` would
    open a new one. Every picture there shows an opened GitHub repository, which part B never takes (0.4,
    9.7), and after the sign-in the new tab looks as `b03a` but for its tab strip, so B8 has no picture;
    `PaletteCloneFromGitHubTests` covers the empty-tab case with a stub (9.6). **Ruling:** accepted.
17. **No 1920×1080 or light picture (part B).** 9.7 names none; 9.4's "460 wide at 1100×700 and
    1920×1080" is `CloneFromGitHubDialogViewTests`' (9.6), and the browser uses theme brushes only. The
    report takes none. Should the lead want one, as part A's A5 has, a fifth start at 1920×1080 in the
    light theme would picture the browser listed (`b03b-listed-1920-light`), scripted only. **Ruling:** accepted: part B is dark at 1100×700 (0.5).
18. **The browser's lines and its empty list (part B: B2, B5, B9; design 9.4).** 9.4 puts `MoreText` and
    `SsoText` "under the status", and its view table lists the four lines after the list and before the
    buttons; the report reads them as below the list, in the order status, error, more, SSO. It reads the
    list as shown in every state, empty at its least height (58) while loading and after a failure, as
    `PickList` is (9.4 gives it no visibility rule). **Ruling:** accepted.
19. **What 9.8's "no earlier picture changed beyond D248's box" allows (part B; design 9.3, 9.8, 0.7).**
    9.3 and 9.8 allow no difference outside the status bar's git box against references made on `master`
    with the same git. Wave 1's comparison with its references (2026-10-29) found differences outside it
    in pictures wave 1 did not change: a caret (`l26b-clone-form-light`, `k10a`) and the temporary
    folder's name (`13b`, `14a`, `14b`, `l32b-clone-failed-dark` and `-light`), so the sentence read
    literally fails whatever WP-B does. The report's reading: a difference may also lie in what varies by
    nature (the shared conventions' "Pictures": a focused box's caret, a temporary folder's name, git's
    output), each opened and judged; anything else is WP-B's defect. And 9.3 names references for phase
    1's checks 13 and 14 and phase 6's L26 and L32 only, while 9.8's sentence covers the palette's
    pictures too, which show the git box: the report reads the lead's references at WP-B's merge as
    covering phase 6's K10 and L31 as well (scripted; wave 1 had no reference of `k10e` and `k10f`), the
    real window's `k10a` to `k10d` and `l31c-palette-light` being compared at the gate ("Changes to
    expected results", part B). Until the ruling, 9.3's sentence binds. **Ruling:** the references at WP-B's merge are the pictures of the last full run before it
    (same laptop, same git), already inspected, and the region comparison covers every earlier phase's
    scripted pictures, K10 and L31 included. A difference is allowed only in the status bar's git
    box, in what the report lists as changed on purpose, and in what varies by nature as the shared
    conventions and design 16 item 9 name it (a temporary folder's name, a caret, anti-aliased edges and
    icons, the indeterminate bar, `a2d`'s reads), each opened and judged; anything else is WP-B's
    defect.
20. **D244's limit on the shared conventions (part B; D244, `plan.md`'s Visual test gate, step 1).** D244
    holds a report's shared conventions to about 30 KB, against phase 6's 291 KB, which was that report's
    whole "What the checks share", its parts' sections included. Measured the same way, this report's
    section held 29.5 KB with part A alone and holds 40.6 KB with part B: what every part shares
    ("Conventions of every part" and "Scenario facts") 13.9 KB, part A 15.9 KB, part B 10.7 KB after its
    trim (its browser's layout left to `PartB` and the rows, what only the real window proves to 13.1,
    the git of `octo/hello` to the shared facts, the clone's path to B6). Parts P, N and G are still to
    come. The report's reading: the limit binds what every part shares, and each part's section holds
    only what its rows do not say, at about 10 KB; read as the whole section's, it could hold only by
    moving each part's conventions under its rows, which changes where they are and not what is read.
    The lead's ruling is asked. **Ruling:** accepted: D244's limit of about 30 KB binds what every part shares ("Conventions of
    every part" and "Scenario facts"); each part's own section holds only what its rows do not say,
    at about 10 KB. Part A's section stays as written; parts P, N and G keep to it.

Part P's points (21 on) were written on 2026-10-29, while WP-B was being built and before WP-P starts;
each waits for the lead's ruling.

21. **A repository without a GitHub remote (part P: P1; design 10.7, 0.2).** 10.7 shows "the section
    unchanged without a GitHub remote (`03e`'s state)", but both repositories of "hosting" have GitHub
    remotes. The report's reading: method 1 opens a copy of phase 3's "ops" (`Scenarios.OperationsAsync()`,
    the repository `03e` shows, its `origin` a folder) in a second tab, signed in, so that P1 proves what
    `03e`, taken with GitHub off, cannot: a sign-in leaves such a repository's section, menu and requests
    as they were. Its template is built once in the 7a project's process. The other way, a plain
    `TempRepo` with one commit, would show the same section but not `03e`'s panel. **Ruling:** accepted.
22. **The message rows' tooltips (part P: P2, P3, P5; design 10.3).** 10.3 gives the failed row's
    tooltip and the `More` row's, not the sign-in, loading and none rows', whose texts may be cut at 260.
    The report's reading: each is its own text, as a branch row's name tooltip is; no check asserts or
    pictures them. **Ruling:** accepted.
23. **A failure after a list (part P: P4; design 10.3, 7.8, 17 item 1; D303).** 10.3's failed state shows
    "the kept list's matches … then the kept rows", but `RepositoryHosting` as built answers a failed
    refresh with `Failed` and `List` null (7.8: "`Failed` with the failure and no list"; `Finish` keeps
    the answer's list, null), so no state holds a list with a failure. The report's reading: the section
    shows the state, so after a failed refresh the rows go and the failed row stands alone with `0`. P4
    pictures a failure on the first read, the same either way; no check depends on the reading. The lead
    rules whether WP-P keeps the last rows itself (D303's "a refresh keeps the rows it had") or follows
    the state, and 10.3 or 7.8 is amended to match. **Ruling:** 10.3 and D303 bind the section: when a failure follows a list of the same
    repository with the same account, WP-P keeps the rows it last showed, under the failed row (the
    kept list's matches, then the kept rows); a sign-out or another account clears them. 7.8 stays
    as built (the hosting reports the failure without a list); WP-P keeps the rows itself, and a
    `PullRequestSectionTests` case proves it. P4 stays a first read's failure.
24. **Which failure is pictured (part P: P4; design 10.7, 1.7).** 10.7 names "failed, in
    `HostingTexts.Describe`'s words" without a kind. The report pictures `Forbidden` on the list
    (`FailNext("pulls:octo/hello", Forbidden)`): the kind shown in GitHub's words (ruling 15), the one
    whose failed row has a second tooltip line (`Detail`, `X-Accepted-GitHub-Permissions`), and a real
    case (a fine-grained token without Pull requests access); part B pictured `ServerError`. The other
    kinds are `HostingTextsDescribeTests`' and `PullRequestSectionTests`'. **Ruling:** accepted.
25. **The draft's text and place (part P: P6, P7; design 10.3, 10.2).** `Draft` is not in 10.2's table;
    10.3's template shows the text `Draft` (and the tooltip ` · Draft`). It docks `PullRequestAuthor`, then
    `PullRequestDraft`, to the right, so `Draft` lies left of the author, with no margin given between
    them. The report reads the text `Draft` and that order, asserts both whole on #2 only, and leaves
    the gap to the builder, inspected. **Ruling:** accepted.
26. **A row's tooltip (part P: P6; design 10.3).** 10.3 writes the tooltip as `#1 Add the login form`,
    "then" `{author} · {head label} → {base ref}`, "then" ` · Draft`. The report reads the first "then" as
    a line break (as the failed row's "on a second line") and the second as the same line:
    `#2 Update the readme`, line break, `octo · docs/readme → main · Draft`. **Ruling:** accepted.
27. **A deleted fork's head label (part P: P6's `tool`; design 10.3, 10.5).** 10.3 gives the head label
    of a same-repository head (`feature/login`) and a fork's (`ada:fix-typo`), not of #8's, whose fork
    `gone/tool` is gone (`HeadRepository` null). P6 asserts #8's name, author and menu (`Check out idea`,
    10.5) but not its tooltip; the filter is not tried on it. **Ruling:** accepted.
28. **The 297 added pull requests (part P: P7; design 10.7, 2.3).** 10.7 adds them "by `AddPullRequest`
    on octo/hello in the check" without data. The report names them `More work {n}`, author octo, head
    `topic/no-pr` (a branch of octo/hello whose only pull request, #4, is closed), base `main`, added in
    order so the fake numbers them #6 to #302. So `origin/topic/no-pr` gets `#6` (the lowest number on
    one key, 7.8) and `feature/login` loses `#1` (#1 is the 301st). Test data, not app texts; the lead may
    name others, and P7's expected results follow. **Ruling:** accepted.
29. **WP-V's merge and part P's pictures (part P; design 11.6, 0.4, D322).** 11.6 runs part A's check 9
    again, not part P's checks, but every picture of part P except `p01a` (no GitHub remote), `p02a` and
    `p11a` (signed out, D257) shows a signed-in GitHub repository's graph, which WP-V changes: badges on
    the labels of branches with an open pull request (11.2) and avatars in the Author column and the
    details panel (11.3), in `p03a` and `p04a` too, since avatars wait only for A3's answer (7.9). The report's
    reading: the lead runs part P's checks again at WP-V's merge and inspects those regions; from then on
    each signed-in picture waits for its tab's `AvatarsShown` once since the start, a refresh's picture
    for the refresh's own `PullRequestsListed` line. WP-V's builder confirms that its line comes in the
    loading and failed states too, else the four pictures of those states (`p03a`, `p11b`, `p04a`,
    `p04b`) wait for the avatars' state instead; WP-S's line never comes in them (open point 34). **Ruling:** accepted: the lead runs part P's checks again at WP-V's merge and inspects the badges'
    and avatars' regions; WP-V's builder says whether its line comes in the loading and failed
    states, and part P's waits follow.
30. **The R twin's sign-in (part P: P6, P11; design 13.4 P, 0.5).** 13.4 P signs in through Settings
    (`SignInThroughSettings`: the screen, the dialog, the token, Esc). Method 2 signs in the same way,
    not through the session (which methods 3 and 4 use for states their steps only start from), so that
    `p06a` and `p11c` are taken in the state the real window reaches, the focus after the screen closes
    included, and owe nothing to a hook (0.5). **Ruling:** accepted.
31. **A9's `a09b` from WP-P's merge (part P; design 10.8).** A9's step 5 waits for the account and
    `accounts.json` before `a09b`; WP-P's section follows the account through two posts on the UI thread
    (the session's `AccountChanged`, then the hosting's `Changed`), which the capture's settling runs, so
    `a09b` shows the sign-in row. Should the lead's run at the merge show the four rows there, A9's step 5
    gains a wait for `hello`'s `HostingState.PullRequests` `SignedOut` (a wait, no expected result
    changed), recorded under "Changed in this report after its first commit". **Ruling:** accepted.
32. **#1's checkout's operation (part P: P13; design 10.5; D305).** 10.5 runs the existing
    `CheckoutAsync(remoteRef)` for a head on a remote, so the operation is phase 3's checkout of
    `origin/feature/login` (its activity row and running text name `origin/feature/login`) while the
    menu's item reads `Check out feature/login`; the report goes by 10.5 and asserts the status bar's
    `Checked out feature/login`. **Ruling:** accepted.
33. **Steps beyond 10.7's list (part P)**, with texts and places the design gives: the signed-out and
    loading section menus (10.4) and the signed-out `Open pull requests in browser` asking nothing (D257);
    P2's click on the sign-in row (8.5); P3's refresh after a failure (17 item 1); P6's `tool`, a fork
    listing its parent's pull requests with #7's and #8's checkout labels (0.2, 10.5); P7's `More` row
    click; P10's section closing with its count; P12's remote-branch menus and a drop onto a remote
    branch; P1's branch menu signed in without a GitHub remote (D306); P13's double-click (D304), the
    items disabled once checked out, `fix-typo` without `#n` (D313) and the `FETCH_HEAD` line. **Ruling:** accepted.
34. **D322 and the states without a list (part P: P3, P4, P11; D322, design 7.8, 17 item 1).** D322
    takes a picture of an opened GitHub repository while signed in only after WP-S's `{repo}: listed …`
    line, but `RepositoryHosting.Finish` writes that line only when it applies a list: never in the
    failed state, and for a held list only once the answer is let go and applied, which ends the
    loading state. So `p03a` and `p11b`
    (loading) and `p04a` and `p04b` (failed) cannot follow it, and a builder who waited for it there
    would wait for a line that never comes. The report's reading: D322 binds the pictures of a list,
    which are the states the real window repeats (its reason is the R twins; these four are scripted
    only, a held request and a fault, 0.5); a picture of a state without a list waits for the tab's
    view model's `HostingState.PullRequests` in that state, the section's message row and the watchers
    being quiet (part P's conventions), and the shared convention says so. The lead rules on this
    reading of D322, and whether D322's text is amended to match; open point 29 asks the same of WP-V's
    `AvatarsShown` in these states. **Ruling:** accepted as the lead's reading of D322, whose text stays as recorded: its two lines are
    written only when a list (or the avatars) is applied, so it binds the pictures of a state with a
    list, which are the ones the real window repeats; a picture of the loading or failed state waits
    for that state as part P's conventions say. Design 17 records the reading.

Part G's points (35 on) were written on 2026-10-29, while WP-P was being built and before WP-V starts;
each waits for the lead's ruling.

35. **The Author column at 1100×700 (part G: G1, G3; design 0.5, 11.5, 13.4 G; D49, D319).** At
    1100×700 the graph is 440 wide (phase 1), and the Author column shows only when the message keeps
    120 beside Author's 140 and Date's 120, 380 in all, where the 440 less Branch / Tag's 130 and the
    Graph column's 48 at least leaves 262 at most: never at that size (with `hello`'s 5 lanes Date gives
    way too). So 0.5's and 11.5's "the graph with badges and avatars, and the details panel (one
    capture)" can show, at the real window's 1100×700 (D319; the laptop holds no 1920×1080 window, 13.1),
    the badges and the details panel's image, never an image in the graph. The report's reading: `g01a` shows the badges and
    the details panel's image; the graph's images are pictured scripted only, at 1920×1080 (G3's second
    half, G4, G6); the real window still proves the avatars' requests, files and decoding through the
    details panel. The other way, narrowing the side panels through the session's `LeftPanelWidth` and
    `RightPanelWidth` (at their least, 180 and 280, the graph gets 640 and shows Author), is state both
    projects could write, but the design names no such step and it would change the window every other
    7a picture shows; the report does not take it. By the same arithmetic no part P picture shows the
    graph's Author column: P3's Notes and open point 29 ("avatars in the Author column … in `p03a` and
    `p04a` too") are read as no visible change there; only a details panel shows an image (part G's
    changes to expected results). **Ruling:** accepted: the real window's `g01a` shows the badges and the details panel's image at 1100×700 (D319), the graph's images are scripted only, at 1920×1080, and the side panels keep the width every other picture has. P3's Notes and open point 29 read as no visible change in the graph's Author column at 1100×700; the run of part P at WP-V's merge inspects the badges and the details panels.
36. **"One picture at 1920×1080" (part G: G3, G4, G6; design 11.5).** Read as one start at 1920×1080 with
    three pictures: signed out (`g06a`), signed in (`g03b`), and light after the toolbar's Theme switch
    (`g04a`), as part A's A5 (ruling 4). The Author column needs that size (open point 35), and one
    window makes `g06a` and `g03b` comparable region by region: what WP-V adds, and nothing else. **Ruling:** accepted, as ruling 4.
37. **`PartG`'s data (part G; design 11.5, 13.4 G).** 11.5 names `PartG.PreparedPath`, `PreparedContent`
    and `PreparedMessage`, and "the row and author", without values. The report names them:
    `src/login.txt`, `login form\nremember me\n` and `Remember the login` (a commit by Test Author, 12:12
    on TempRepo's clock in both projects); `PartG.Row` 4, `Update the readme` by Octo Cat
    (`octo@example.com`): an octo commit (11.5's G3) whose row carries the draft's badge; and
    `PartG.Lanes` 5, which the real window's `GraphRowPoint(row, lanes)` takes, worked out by hand from
    the graph's lane rule (phase 1; the five branch tips above `main`'s commit each start a lane) and
    asserted by the scripted check (`Columns.LaneCount`). Test data, not app texts; the lead may name
    others, and the rows and the facts' table follow. **Ruling:** accepted.
38. **G5's repository (part G: G5; design 11.5, 0.2).** 11.5 names "a repository without a GitHub remote".
    Part P's `ops` has only Test Author's commits, whom GitHub does not know, so its graph would show no
    image even if the guard failed. The report's reading: G5 takes the prepared `hello` with `origin`
    set to the copy's own `github/octo/hello.git` by its folder path, which recognition refuses (6.3):
    the same commits by octo and ada and the same branches with open pull requests on the fake, so a
    broken guard would show images, badges and requests. The two starts are one `[Fact]` with a data
    folder each; their pictures are expected to be the same file, as phase 1's `03b` and `03c` are. **Ruling:** accepted.
39. **D322 where no avatar is applied (part G: G5, G6, G7; D322, design 7.9, 17 items 1 and 3).** As in
    point 34: WP-V's line comes only when the avatars are applied; `FinishAvatars` applies nothing after
    a failed run, and a `NotGitHub` or `SignedOut` tab asks for none, so G7's picture waits for WP-S's
    warning of the failed read, and G5's and G6's for the hosting's state and quiet watchers; the shared
    conventions' bullet says so. WP-V's builder confirms that its line is not written when a run applies
    nothing. **Ruling:** accepted, as ruling 34; WP-V's builder confirms in its handoff that its line is not written when a run applies nothing.
40. **Steps beyond 11.5's table (part G).** G6 (signed out at 1920×1080: no badge, no image, no request;
    11.1, 11.3, D257; and after a sign-out that follows the avatars, D291, 7.5, without a picture) and G7
    (the authors' read failed: every author as today, the failure logged, nothing fetched; 7.9 step 3,
    11.3), numbered after 11.5's five; in G1 the requests and the labels of
    every row; in G2 the remote labels cut for their badges and the draft's and an unbadged row's
    tooltips; in G3 ada's details, the requests and `avatars/`; in G5 the whole window's identity. An
    image's own failure (a 404, a body that is not PNG or JPEG, a file that does not decode) is not
    checked here: `FailNext` refuses avatar routes (16 item 2) and `ServedHosting` hands out no
    `FakeUser` whose `AvatarBytes` a check could change, so it stays with `AvatarCacheTests` and
    `GraphAvatarTests` (6.5, 11.4). **Ruling:** accepted.
41. **The measured widths (part G: G1, G2, G6; design 11.2).** 11.2 leaves them to this report. Measured
    on 2026-10-29 with Avalonia 12.1.3's `TextLayout` in the harness's configuration (Skia, HarfBuzz,
    Inter SemiBold 11): `#1` 12.4, `#2` 13.9, `#3` 14.2, so a one-digit badge takes 31.4 to 33.2
    (6 + 10 + 3 + `#n`) and leaves 79 to 81 of name to a label alone in its row (11.2 estimated 76);
    `feature/login` (68.4) stays whole, while `origin/feature/login` (103.0), `origin/docs/readme` (105.0)
    and `origin/topic/no-pr` (95.3) are cut, and in part P's rows, where `origin/feature/login` folds into
    `+1`, `feature/login` keeps 52: `feature/…`. 11.2's "`feature/login` stays whole or loses a letter or
    two" holds for the local label; the report reads the other cuts as 11.2's rule, not a defect. Row 5's
    cut is close: one letter more (`origin/feature…`, 81.7) misses its room (80.6) by 1.0. Part G's
    changes to expected results also use, measured the same way: `#6` 14.2, `#7` 13.5, `+1` 12.7 (a `+1`
    label 24.7), `fix-typo` 42.2 (whole beside `#7` and the `+1`, its label 6 to 92.7), `origin/topic/no-pr`
    cut to `origin/topic/…` (74.0; beside `#6` its room is 78.8, `origin/topic/n…` 81.2) and part P's
    `feature/…` (50.9; room 52.0, `feature/l…` 54.2). The drawn
    texts are asserted exactly and the edges ±1; should WP-V's measure differ, the rows change, with a
    record under "Changed in this report after its first commit". **Ruling:** accepted.
42. **WP-V's count (part G; design 11.3).** "Raises `AvatarsApplied` with the number of authors shown" is
    read as the number of authors whose image the control decoded, whatever the Author column's
    visibility: 11.3's data paragraph (decode, keep the bitmaps, raise the event) is unconditional, and
    only the drawing waits for the column ("when the Author column is visible"). So at 1100×700, where
    the column is hidden (open point 35), the control still decodes and raises `AvatarsApplied`, and WP-V
    writes `hello: showing GitHub avatars for 2 authors` (octo and ada); read literally, "shown" would
    give 0 or no line there. G1 step 1, the real window's `WaitForHostingSettled` (launches N, G and P,
    13.3), every part P picture of a signed-in tab and A9 step 4's and B6 step 8's waits depend on this
    reading; the lead is asked to bind WP-V to it (a `GraphAvatarTests` case: the event and its count
    with the column hidden). In part P's method
    3 `tool`'s line counts 1: `tool` lists acme/tool, whose `main` holds only ada's commit, and the
    lookup of octo's address there finds none, so octo has no image in `tool` (no part P picture shows
    `tool`'s details panel). The line's `{n} authors` is 1.9's text as given, also for 1. A change back
    to `None` (G6's sign-out) is a change too; no check waits for, or asserts, a line it writes. **Ruling:** accepted, and binding on WP-V: the count is the number of authors whose image the control decoded, with the Author column shown or hidden, and the event and its line come either way, with a `GraphAvatarTests` case for the hidden column.
43. **WP-V's line for a tab that is not active (part G: part B's B8 step 4; design 11.3; D322).** The
    window has one `RepositoryGraphView`, bound to the active tab's repository (`MainWindow.axaml:39`,
    `CommitGraphView.axaml:58`), and WP-V writes its line from that control's `AvatarsApplied` (11.3),
    so a tab's line comes only while its graph is shown: when its avatars are applied while it is
    active, or when it is made active with them (again at each such activation). In B8 step 4 the
    sign-in opens the form on a new tab at the end, active, before `hello`'s avatars' requests can have
    been answered, so `hello`'s line does not come while B8 runs, and the wait that B8 step 4 names
    "from WP-V's merge" would time out, or pass only when the avatars won the race. The report's
    reading: D322 binds pictures, and B8 takes none; from WP-V's merge B8 step 4 waits instead until the
    fake has recorded `avatar:1001` and `avatar:1003` since the mark and `hello`'s view model's `Avatars`
    maps octo's and ada's addresses, which keeps its requests whole and the close from cutting one, and
    B8's row changes so at the merge. A9 step 4, B6 step 8 and every picture's wait are on the active
    tab and keep the line. **Ruling:** accepted: the line stays the active tab's (11.3), and B8 step 4's wait from WP-V's merge is changed now, recorded under "Changed in this report after its first commit", so that WP-V codes it.
44. **`Shot` and the details panel's image (part G; design 11.3, 11.1; D237).** `Shot` waits only for
    `WaitForCommitDetails` (the details' read, `CommitDetailsViewModel.IsLoading`); the ellipse's image is
    decoded after it, off the UI thread, so from WP-V's merge a picture of an octo or ada commit's details
    (`p09a`, `p13a`, `g01a`, `g03b`) could be taken before the image shows under load. The report's
    reading: `CaptureForPicture` (`Phase7aChecks.Support.cs`) also waits, after `WaitForCommitDetails`,
    while the active tab's `Details.AuthorAvatarFile` is set and `DetailsAuthorAvatar` is not shown;
    WP-V adds it outside 11.1's file list (with GitHub off the file is always null, so nothing changes;
    no 7a check has an image that does not decode, open point 40). The steps that name the wait keep
    it, and a step that moves from one image to another (G3 step 7) waits for the image's pixels, since
    the ellipse keeps the last image until the new one is decoded (11.3). `ShellDriver.WaitForCommitDetails`
    is left as it is (every phase's walk-through calls it); the real window's one capture with an image,
    `g01a`, waits for it by name (13.4 G step 2), and its other captures select no commit by octo or ada. **Ruling:** accepted: WP-V adds the wait to `CaptureForPicture` in `Phase7aChecks.Support.cs`, outside 11.1's list.

Part N's points (45 on) were written on 2026-10-29, while WP-P was being built and before WP-V and WP-N
start; each waits for the lead's ruling.

45. **No `Create pull request…` in the section's menu (part N: N1; design 0.1, 10.4; D307).** 0.5's row
    names `ForLocalBranch`, `ForRemoteBranch` and `ForDrop`; 0.1 puts "a Create pull request… for the
    current branch in the section's menu" out of scope, and 10.4's `ForSection` gives the section two
    items, which P11 asserts. The report's reading: no check looks for the item there or in the palette
    (10.4 lists pull requests only); N1 step 5 asserts that the section's menu after a create still holds
    its two items and no create item. The graph's labels share the branch menus' builders (10.4), so the
    left panel's rows stand for them. **Ruling:** accepted.
46. **Checks and steps beyond 12.8's table (part N).** N7 (Cancel during the push, D315; 12.7's "Cancel
    offered while pushing only"; point 59), N8 (the form's checks: the same branch, the Pick and Esc on it, an
    empty title; 12.4, D318), N9 (a 403 in the form, and a lost answer counted as created; 12.6, D316)
    and N10 (12.8's "the form also in light and at 1920×1080"), numbered after its six; and in N1 the
    offer's click and its going at the next operation (12.2, D317), `Open pull request #6` in the branch's
    menu after the create (ruling 11) and the section's menu (point 45); in N3 the description sent and
    never logged (12.5); in N4 the no-commits case with a base chosen in the Pick; in N5 a create from a
    remote branch, never pushed (D313), before the drop; in N6 the sign-in going on to the form (12.3 step
    1). 12.6's other rows stay with `CreatePullRequestOperationTests` (12.7). **Ruling:** accepted.
47. **One create for N2 and N3 (part N: N2, N3; design 12.8, 12.1).** 12.8 names no head for N3, and
    `topic/no-pr`'s create is N1's (R). N3 ticks Draft on N2's create of `local-work`: one create shows
    the push first, the held POST and the draft. N7's Cancel comes first in the same start, so "one
    activity-log entry" is read as one operation per Create, the push and the POST together: two
    entries, both `Create pull request for local-work`, and none `Push local-work`. **Ruling:** accepted.
48. **A remote branch's head text (part N: N5; design 12.4, 12.3).** `PullRequestEnd.Text` is given for a
    local head (`topic/no-pr`) and a base (`origin/main`), not for a remote-branch head. The report reads
    it as the ref's name as its menu names it, `origin/topic/no-pr`, as a base's text is, so `From` reads
    `origin/topic/no-pr` and the operation is `Create pull request for origin/topic/no-pr`; the POST's
    `head` is GitHub's branch, `topic/no-pr`. Should WP-N show GitHub's branch name instead, N5's `From`
    and its step 3's wait change. **Ruling:** accepted: a remote-branch head's text is the ref's name as its menu names it, `origin/topic/no-pr`.
49. **The same branch when the form opens (part N: N8; design 12.4, 12.6).** 12.4 puts `SameBranch` on
    the error line whenever head and base are one branch, and disables Create; the default base can be
    the head's own (`main`'s form Into `origin/main`). The report reads that the line shows as soon as the
    base is set, in the form's first state, and leaves Create's tooltip then unasserted (12.4 gives
    reasons only for no base and an empty title); `SameBranch`'s branch is GitHub's branch's name
    (`main`), as 12.6's example writes it. **Ruling:** accepted.
50. **The Pick's items (part N: N8; design 12.3; D318).** 12.3's "options-with-details" names no detail;
    the report reads each option's as its repository (`PullRequestEnd.Repository`, `octo/hello`), "the
    refs' order" as the refs read lists remote branches (by full name; `origin/HEAD` is no ref), and the
    current base as the selected item; Esc closes the Pick only (12.3, D104). **Ruling:** accepted.
51. **Two refreshes after a create (part N: N1, N3, N5, N9; design 12.5 step 4, 7.10; D289, D322).**
    12.5 step 4 awaits a refresh when the base is the listed repository, and the succeeded Network
    operation then starts D289's (`RepositoryViewModel.OnQueueFinished`, as built): two `GET …/pulls`,
    the first answered 200 (the list changed since the ETag), the second 304, each writing `hello: listed
    5 open pull requests of octo/hello` (`RepositoryHosting.Finish` writes it for a 304's kept list too).
    A picture after a create waits for both lines. 13.4 N step 4 waits for the operation's end only; the
    report reads D322 as making the real window wait for both lines too before its compare. Should WP-N
    leave step 4's refresh to D289's, one line comes and the rows change. **Ruling:** accepted, also for the real window: 13.4 N step 4 waits for both `listed` lines before its compare (design 17 item 8), and WP-N keeps 12.5 step 4's own refresh.
52. **A held push and its Cancel (part N: N7; design 2.2, 2.3, 12.5; D315).** `HoldNext("git:octo/hello")`
    holds the push's first request (git's `info/refs` without credentials, 2.2) before anything is
    decided, and the form's Cancel (point 59) stops git while it waits; `AuthRemoteServer` counts a
    client that went away as no error. The hold is disposed after the operation's end, and the request it lets go may be recorded, so
    N7 asserts no `git-receive-pack` POST and nothing pushed, not that request. Git had written nothing,
    so no lock file and no lock dialog are expected. **Ruling:** accepted, with ruling 59.
53. **WP-N's log lines (part N: N1, N3; design 12.5).** 12.5 gives the Info line by its example, `{repo}:
    Create pull request for topic: into octo/hello main, draft no, push to origin/topic first` or "no
    push", "and the number created" without a form. N1 asserts `hello: Create pull request for
    topic/no-pr: into octo/hello main, draft no, no push` word for word; N3 only that the line names the
    push to `origin/local-work` and the draft, and that no line holds the description; the number's line
    and WP-N's Warning are the builder's to word. **Ruling:** accepted: WP-N's builder words the number's line and the Warning, and the handoff gives them.
54. **The push's undo entry (part N: N2, N3; D314).** D314 records Create's push in the undo history as
    any push; 12.7 names no test of it and the design gives no text of the entry beyond phase 5's push
    entry. No part N row asserts it; the report asks whether `CreatePullRequestOperationTests` should (the
    entry after a create that pushed, none after one that did not). **Ruling:** `CreatePullRequestOperationTests` asserts D314: one undo entry for the push, as any push's, after a create that pushed, and none after one that did not (binding on WP-N, design 17 item 8).
55. **The labels with a badge (part N: N1, N3; design 11.2).** After a create `topic/no-pr`'s and
    `local-work`'s labels share their rows with their remote branches', and their widths beside `#6` were
    not measured (point 41 measured part G's and part P's). The rows assert `Badge` and `FullText`; where
    each name is cut is inspected. **Ruling:** accepted.
56. **The rest point at 1920×1080 (part N: N10).** (760, 687) lies inside the form's card at 1920×1080 (x
    730 to 1190), so part N's 1920×1080 pictures rest the pointer at (760, 1000), the backdrop under the
    card (which ends above y 770 at its tallest, 12.4), as phase 6's SD2 did; `Shot` takes the point, in
    `Phase7aChecks.Support.cs`. **Ruling:** accepted.
57. **The requests after a lost answer (part N: N9; design 12.5 step 5; D316).** 12.5 reads the list again
    after a lost POST and then, counted as created, runs step 4, whose refresh may make another read; N9
    asserts the POST once, the first `GET …/pulls` answered 200 with #6 and the last 304 (D289's), not how
    many come between. Step 4's refresh can answer 304 before the operation ends, so N9 waits for D289's
    own line, the first `listed` line after the operation's end line (the queue logs the end before it
    raises `Finished`, which starts D289's refresh), not for a 304. **Ruling:** accepted.
58. **A pull request opened elsewhere (part N: N4; design 12.8, 2.3).** 12.8's "a pull request added on
    the fake after the list was read" is `AddPullRequest("octo", "hello", "Try an idea", "octo",
    "topic/no-pr", "main")` once the list's line has come: #6, octo's own `topic/no-pr` into `main`, not a
    draft. Test data, not app texts; the lead may name others, and N4's expected results follow. **Ruling:** accepted.
59. **No reachable Cancel while Create pushes (part N: N7, the "Creating" convention; design 12.4, 12.5,
    12.7; D315, D69, D104).** D315 offers Cancel during the push only, but 12.4 disables the form's
    `Cancel` and ignores Esc while creating, and the status bar's `OperationCancelButton`, which the
    queue shows while the cancel window is open, lies under the form's backdrop: `DialogLayer` sits over
    the main grid, status bar row included (`MainWindow.axaml`), and `Panel.dialogBackdrop` has a
    background over the whole window (D69), so a click on the button lands on the backdrop
    (`ShellDriver.Click` sends pointer input at the point, as the real mouse does). No control the user
    can reach cancels the push while the form is open, and no other point names the gap. Two ways out:
    (a) 12.4 amended: while the operation's cancel window is open (12.5 step 1), the form's `Cancel`
    stays enabled and cancels the operation through the queue (`IOperationQueue.Cancel`, by a member
    `ICreatePullRequestActions` gains), the form staying open; disabled again once the window ends (12.5
    step 2); Esc still ignored; 12.7's `CreatePullRequestDialogViewModelTests` "creating (all disabled …)"
    changes with it; (b) the push cannot be cancelled from the UI while the form is open, D315's offer
    being the queue's `CanCancel` alone. The report takes (a), which keeps D315's "offered", and N7 clicks
    the form's `Cancel` during the held push. Under (b), N7's step 4 and `n07b` go, its step 3 asserts the
    form's `Cancel` disabled, `OperationStatus.CanCancel` true and `OperationCancelButton` shown with
    `Stop git`, and the cancel is proven only by `CreatePullRequestOperationTests` (12.7). No check clicks
    `OperationCancelButton` through the backdrop. N7's steps 3 and 4 wait for the ruling, and WP-N is not
    built before it (D244). **Ruling:** way (a): while the operation's cancel window is open (12.5 step 1, the push), the form's `Cancel` stays enabled and cancels the operation through the queue, the form staying open with `Cancelled` on its error line (12.5's "Afterwards"); once the window ends (12.5 step 2) it is disabled again; Esc stays ignored while creating. 12.4 and 12.7's "creating (all disabled …)" case are amended so (design 17 item 8), and N7 stands as written.
60. **The references at WP-N's merge (part N; design 12.9, 12.8; ruling 19).** 12.9 and 12.8 compare
    phase 3's status-bar pictures by region "with `master`'s" references; ruling 19 was given for WP-B's
    merge, where it made the references the pictures of the last full run before the merge (same laptop,
    same git). The report reads 12.9 as ruling 19 reads 9.8, as parts P and G read 10.8 and 11.6: the
    last full run's pictures, a difference allowed only in the git box and in what varies by nature,
    each opened and judged, none in the status bar's middle. Should the lead hold to `master`'s, the
    references are made on `master` with the same git before the comparison. **Ruling:** accepted, as ruling 19: the references are the last full run's pictures before WP-N's merge, on this laptop with the same git.
61. **The provider model's cases part N cannot reach (part N; design 12.3, 12.7; D313, D318).** `hello`
    has one GitHub remote and is not a fork, so no part N check exercises `HostingRemoteOrder` (the head's
    place on the first GitHub remote, D313), a fork's parent's default branch as the default base (A3's
    `ParentDefaultBranch`, 12.3) or an `owner:branch` base (D318). 12.7's `CreatePullRequestOperationTests`
    names a fork's head (`HeadOwner`) but none of the three. The report asks whether that class adds
    them, for instance on `tool/` (`origin` octo/tool, a fork of acme/tool; `upstream` acme/tool); until
    the ruling, the Delivers row names them under that class without a case of 12.7's. **Ruling:** `CreatePullRequestOperationTests` adds the three cases on `tool/`: the head's place by `HostingRemoteOrder` (D313), a fork's parent's default branch as the default base (A3's `ParentDefaultBranch`), and an `owner:branch` base (D318) (binding on WP-N, design 17 item 8); the Delivers row names them there.
