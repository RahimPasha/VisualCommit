# Phase 7b visual test report — Azure DevOps and GitLab hosting

The Check, Steps and Expected columns are written before the UI they test is built: since D244
each part's checks are written and committed just before its work package is built. The result
columns are filled in when the gate runs.

Phase 7b's checks come in four parts (`docs/phase-7b-design.md` 0.3, 0.5): **A** accounts and sign-in
(WP-A), **B** browse and clone (WP-B), **P** requests in the left panel and the graph (WP-P) and **N** a
new pull or merge request (WP-N). This first version holds part A, written on 2026-11-06 before WP-A
started, after WP-LF's merge (`22fad1b`), from `plan.md`'s phase 7b, the design's sections 0, 1, 2.6,
2.9, 10, 14 and 16.1, decisions D345, D348, D350, D351, D353, D356, D360, D362, D366, D368, D373, D374,
D378 and D385, 7a's part A (its report, `Phase7aChecks.A*.cs`, `Phase7aExpectations.A.cs`, the Accounts
section and the "Add GitHub account" dialog as built), and the harness, the three fakes and the
scenario as waves 1 to 3 and WP-LF built them (`Scenarios.Phase7b*.cs`, `FakeAzureDevOps*.cs`,
`FakeGitLab*.cs`, `ShellDriver.Phase7b.cs`, `Phase7bChecks*.cs`, `Phase7bExpectations.Common.cs`). No part
A UI of 7b existed: every text is design 10.2's or 7a's, every place 10.3's, 10.4's or 7a's, and the
checks take positions from the controls' bounds (D122's rule). "What the checks share" holds what every
part relies on, then part A's conventions, methods and texts; "Checks" is the table of A1 to A14 (design
10.7's list, numbered here in its order, open point 1); "Changes to expected results" lists the earlier
pictures and tests part A changes on purpose; "Open points" lists where the design is silent or leaves
a choice to WP-A, with the reading this report takes. Tokens are written here only by their constant
names and forms, never by value; organizations, users, groups and projects are the scenario's made-up
names (design 2.6, D346, D352).

Part B was added on 2026-11-06, after WP-A's merge (`b6de3cf`) and before WP-B started, from the
design's sections 0.3 to 0.7, 1.7, 2.6, 8.2, 8.6, 9.6, 11 and 14.4, decisions D302, D350, D354 and D362,
7a's part B (its report, `Phase7aChecks.B*.cs`, `Phase7aExpectations.B.cs`, the clone form, the browser
and the palette's command as built), WP-A's Accounts section and sign-in dialogs as merged, and the fakes
and the scenario as WP-FA, WP-FL and WP-LF left them (`FakeAzureDevOps.Router.cs`' list,
`FakeGitLab.Router.cs`' `projects`, their holds and faults, `Scenarios.Phase7b*.cs`). No part B UI of 7b
existed: every text is design 11.2's or 7a's, every place 11.3's, 11.4's or 7a's, and each list's rows
are the fakes' answers in the order the providers give them (`AzureDevOpsProvider.ListRepositoriesAsync`,
`GitLabProvider.ListRepositoriesAsync`). It adds its section under "What the checks share", B1 to B8 to
"Checks" (design 11.7's list, numbered in its order, open point 11), a column and a row to "What proves
each Delivers item", its sections to "Changes to expected results", and open points 11 to 21.

Part P was added on 2026-11-06, after WP-B's merge (`9409aa8`) and before WP-P started, from the
design's sections 0.3 to 0.7, 2.6, 2.7, 3.5, 4.5, 6.6, 7.4, 9.7, 12, 13.8, 14.2, 14.4 and 16.3, decisions
D351, D355, D356, D361, D362, D366, D367 and D376 and rulings 5, 19, 21 to 24 and 29, 7a's parts P and G
(their report, `Phase7aChecks.P*.cs` and `.G*.cs`, the section, its menus, the checkout, the badge and
the branch rows as built for GitHub), WP-S's session and WP-A's and WP-B's merges as built
(`RepositoryHosting`, `HostingTexts.Describe`, the sign-in entry), and the fakes and the scenario as
WP-FA, WP-FL and WP-LF left them (`FakeGitLab.Router.cs`' `mrs:` and `project:`,
`FakeAzureDevOps.Router.cs`' `pulls:` and `repo:`, their holds, faults and ETags, `Scenarios.Phase7b*.cs`).
No part P UI of 7b existed: every text is design 12.2's or 7a's, every place 12.3's to 12.6's or 7a's,
and each list's rows are the fakes' answers in the order the providers give them
(`GitLabProvider.ListPullRequestsAsync`, `AzureDevOpsProvider.ListPullRequestsAsync`). It adds its
section under "What the checks share", P1 to P14 to "Checks" (design 12.8's list, numbered in its
order, open point 22), a column and a row to "What proves each Delivers item", its sections to
"Changes to expected results", and open points 22 to 33.

Part N was added on 2026-11-07, while WP-P was being built and before WP-N started, from the design's
sections 0.3 to 0.7, 1.5, 1.7, 2.6, 2.7, 3.3 to 3.5, 4.3 to 4.5, 6.6, 7.4, 12.2, 12.4, 12.8, 13, 14.2,
14.4 and 16.1, decisions D306, D312 to D318, D322, D350, D351, D355 and D362 and rulings 1, 3, 20 and
21, 7a's part N (its report, `Phase7aChecks.N*.cs`, `Phase7aExpectations.N.cs`, the form, the operation
and the status bar's offer as built), part P's checks and design 12 for what part N relies on from
WP-P, which was not merged (no expectation is taken from its code), WP-A's and WP-B's merges and WP-S's
session as built, the providers' creates (`GitLabProvider.CreatePullRequestAsync`,
`AzureDevOpsProvider.CreatePullRequestAsync`, their clients' refusals) and the fakes and the scenario
as WP-FA, WP-FL and WP-LF left them (`FakeGitLab.Router.cs`' and `FakeAzureDevOps.Router.cs`' `create:`
routes and refusals, `Scenarios.Phase7b*.cs`). No part N UI of 7b existed: every text is design 13.2's,
12.2's or 7a's, every place 13.4's or 7a's, and each request and body is what the providers send and
the fakes answer. It adds its section under "What the checks share", N1 to N9 to "Checks" (design
13.8's list, numbered in its order, open point 34), a column and a row to "What proves each Delivers
item", its sections to "Changes to expected results", and open points 34 to 45.

## Run

Filled in when the gate runs, for each of its two passes: date, app commit, the machine (its
operating system and version), the display scaling of the real-window pass, the screenshot
folders, and how many pictures were inspected. The gate's two passes are run on one commit by a
session on the Windows machine, except that a commit changing only
`tests/VisualCommit.RealWindowTests` re-runs only the real-window pass, and the default run of the
last commit it ran on stands for its own (D246; `docs/plan.md`'s Visual test gate, step 6). Each
round lists the commit of each pass. The two runs of the new phase's real-window checks on their
own before the first counted round are listed too, marked "before the gate". Since phase 7a so
are the live command's two runs at the gate, each with its commit (D335); "Live command" gives
what they found.

If a cloud session ran the scripted pass earlier, its run is listed here too, marked "cloud
session, before the gate", and its results stand in the Scripted column until the Windows
session replaces them with its own. It does not count as the gate's scripted pass.

## Live command

The opt-in command against the real services (`tests/VisualCommit.LiveHostingTests`; D250, D335; for
Azure DevOps Services and gitlab.com since phase 7b, D346, D352, D364; design 15), with the owner's
tokens for their sandboxes, the owner told before each run. Each run with the tokens follows a run of
the offline classes alone (`--filter-class "*LiveSafetyTests" --filter-class "*LiveScreenTests"`) on
the same commit, which is named too (15.9). Filled in per provider from its `run.txt`
(`artifacts/live/github/`, `artifacts/live/azure-devops/`, `artifacts/live/gitlab/`) for each run: the
date and time, the commit, the outcome, what it made in the sandbox and removed again, its findings
about the service's answers with what each changed in that provider's fake, and the search of run.txt
and the console output for the token. Its runs in 7b: stage 1 (status step 16), after WP-S's merge,
whose findings WP-LF put into the fakes before part A's checks were written (design 0.4, 16.1), and
Azure DevOps' `Check_1b_probes` again on 2026-11-07 for open point 8, the lead's run; stage 2
(`Check_3`, the app-driven check per provider) after WP-N's merge; at the gate one before the gate's
pictures, whose corrections to the fakes are committed before the passes, and one on the counted
round, after the real-window pass and the list of pictures (D335). The next run overwrites run.txt, so
each run's copy is kept outside the repository and named here. Nothing of a run's files is written
here: no token, address, request id, id, host name, the Azure DevOps organization's name or GitLab's
namespace or user name (D346, D352, D372, D384); D373's and D374's made-up values are named by what
they are.

### GitHub (`artifacts/live/github/run.txt`)

No run in phase 7b yet: stage 1 ran Azure DevOps' and GitLab's classes alone (15.9). GitHub's classes
run at the gate with the other two. GitHub's token expires on 2026-11-29 (plan risk 42): the gate's two
runs fall before that date, or the owner makes the token again first. Phase 7a's report holds its three
runs.

### Azure DevOps (`artifacts/live/azure-devops/run.txt`)

**Stage 1, first run** (2026-11-05, 20:51 UTC, on `8faa839`, the Windows machine; the offline classes
alone first on the same commit, 404 of 404): `bash scripts/heavy-run.sh --project
tests/VisualCommit.LiveHostingTests -c Release --filter-class "*AzureDevOpsLiveTests" --filter-class
"*GitLabLiveTests"`: 9 tests, 8 passed, in 41.9 s. Azure DevOps' 5 passed: `Check_1_the_sandbox_and_its_leftovers`, `Check_1b_probes`,
`Check_2_the_providers_and_the_session` (the app's sign-in, list, push and create of a pull request in
the sandbox), `Check_2b_probes` and `Check_4_a_clone_through_the_organization_host`. In the sandbox it
made one pull request and its branches, then abandoned the request and deleted the branches. Its run.txt
(631 lines) named 12 answers equal to the fake's and 11 that differed, among them: a made-up value in
none of the token forms (D373) answered 200 with the Anonymous identity on connectionData where the fake
answered 401; the sandbox's `info/refs` with a made-up value answered 401 with three challenges where
the fake answered 404; the bodies of the 409 and 400 creates; the members of the abandon's and the
create's answers. Run.txt holds neither the organization's name nor an address, checked fully decoded.
Copy: `D:/Projects/Personal/phase7b-work/live/azure-devops-run-20261105.txt`.

**Stage 1, `Check_1b_probes` again** (2026-11-07, 05:16 UTC, on `9409aa8`, the Windows machine, the owner
agreeing; open point 8; the offline classes alone first on the same commit, 404 of 404): the command
with `--filter-class "*AzureDevOpsLiveTests"` and `--filter-method "*Check_1b_probes"`: 1 of 1 passed,
in 20 s with the lock, 17 reads and no write. Of its 17 probes,
connectionData without a credential, with Suppress, answered 200 with the Anonymous identity
(`application/json` at `7.1-preview.1`, no `WWW-Authenticate`) where the fake answered 401 TF400813;
every other probe answered as on 2026-11-05: connectionData with the token 200, and at `7.1` 400
`VssInvalidPreviewVersionException`; D373's made-up value in none of the token forms 200 with the
Anonymous identity, and its 84-character one 401 with an empty body; the core resource area without a
credential and with the token 200; D374's made-up organization 404; the project's list 200 with one row;
the `info/refs` differences (401 where the fake answers 404) are the ones 16.1's As built keeps. What it
changed in the fakes and in A9: "What stage 1 changed in the fakes" below and open point 8. Run.txt (585
lines) and the console output (31 lines) hold neither the token nor the organization's name, searched
as written and fully decoded (URL, HTML and base64); run.txt names Azure DevOps' sign-in realm in its
challenges, as on 2026-11-05, which stays out of the repository. Copy:
`D:/Projects/Personal/phase7b-work/live/azure-devops-check1b-2026-11-07.txt`.

### GitLab (`artifacts/live/gitlab/run.txt`)

**Stage 1, first run** (the same command and run as Azure DevOps' above): `Check_1` and `Check_1b_probes`
passed; `Check_2_the_providers_and_the_session` failed at the list: GitLab answered `GET /projects` with
403 `insufficient_granular_scope` naming Project: Read among the user permissions, which the token had
only among its group and project permissions, where the fake had answered 200 (D385);
`Check_2b_probes` had nothing to probe and deleted its branches. The owner made the token again on
2026-11-05 with Project: Read among its user permissions too (D385; checked as 15.8 says). The first
run's console output with GitLab's 403: `D:/Projects/Personal/phase7b-work/live/stage1-run1-output-20261105.txt`.

**Stage 1, GitLab again** (2026-11-06, 05:20 UTC, on `652ad80`, whose code is `8faa839`'s; the offline
classes alone first, 404 of 404): the same command with `--filter-class "*GitLabLiveTests"` alone: 4 of
4 passed in 47 s with the lock. `Check_2` signed in, listed, cloned through the clone form with the
app's helper (`x-access-token` holds on gitlab.com, 16.1), pushed two branches and created a draft
merge request; `Check_2b_probes` probed it (the 409 and the missing-branch 400 as the fake sends them,
a create with nothing ahead 201 as the fake's), then closed and deleted both merge requests and both
branches. Every probed status and body matched the fake's; the fake's `GET /user`, the created request
and the probes' answers held a subset of GitLab's members. GitLab's rate limit with a token measured a
burst of 100 a minute, 304s counted (for the review before the gate, 16.1). Run.txt (48 lines) holds
neither the namespace nor an address, fully decoded; `glpat-` only in the command's own words, nothing
token-shaped. Copy: `D:/Projects/Personal/phase7b-work/live/gitlab-run-20261106.txt`.

### What stage 1 changed in the fakes (WP-LF, merged at `22fad1b`; design 16.1's As built)

Made-up copies of the measured shapes and texts, each marked MEASURED with its date in the fakes'
`.Router.cs` headers. Azure DevOps: a Basic value in none of the PAT forms (`azdo_FAKE-`, 84 characters
with `AZDO`, 52 of base32) gets connectionData's Anonymous 200 with and without Suppress; D374's made-up
organization 404 "The resource cannot be found." with a token as without; a refused PAT-shaped value's
empty 401 with the measured realm; errors without `api-version` in `Content-Type`; an empty title's 400 in
the service's words; the create's `labels`; the abandon's members; the refs POST's create and updates;
nine identity names. GitLab: `GET /user`'s 41 members and a single request's in the service's order; a
fork's pair after `prepared_at`; the burst header; D385's user permissions (`FakeGitLabPermissions.UserSection`,
`UserProjectRead`; the default token holds both). The clients: a message is one line. Kept as differences:
git's `info/refs` refusals (the fake's git is `AuthRemoteServer`'s, one challenge; the helper reads no
`wwwauth[]`). Part A rests on these answers (A8 to A10). `Check_1b`'s probe of the sandbox organization's
connectionData without a credential (16.1; open point 8) ran on 2026-11-07, the lead's run of
`Check_1b_probes` with the owner's agreement: 200 with the Anonymous identity (`application/json` at
`7.1-preview.1`, no `WWW-Authenticate`), where the fake answered 401 TF400813; its other probes answered as
before. The sandbox's organization allows public projects, so the short value's Anonymous 200 is a public
organization's answer: the fake now gives it, and no credential's, only in an organization with
`PublicProjects`, and in a private one such as Fernhill-Labs the sign-in's 302 or 401 TF400813 that no
credential gets there (READ g1 G0.2); the live command's fake allows public projects, as the sandbox's
organization does; A9 takes the fault `AnonymousIdentity` (the lead's ruling; "Changed in this report after
its first commit"). Still to come: stage 2's `Check_3` and the gate's two runs.

## What the checks share

### Conventions of every part

What every part of phase 7b relies on; each part's conventions add to these. Where an item says "as 7a",
phase 7a's report, "Conventions of every part", holds it word for word and is not repeated here.

- **Parts and the project** (design 0.3, 0.5, 2.7; D230). One project,
  `tests/VisualCommit.VisualTests.Phase7b`, about 22 to 27 test methods for the phase (29 as the parts
  are written, open point 44; under D230's 40),
  one class per part: `Phase7bChecksA`, `Phase7bChecksB`, `Phase7bChecksP`, `Phase7bChecksN` (`public
  sealed partial class … : Phase7bChecks`, in `Phase7bChecks.<part>[.<topic>].cs`), with what they compare
  against in `Phase7bExpectations.<part>.cs` (a nested class `PartA` … `PartN` of texts, picture names,
  positions and tolerances, written from this report and not from the App's texts) and their tests in
  `Phase7bExpectationTests.<part>.cs`. The real-window pass links `Phase7bExpectations*.cs` (0.3), so those
  files use nothing of the 7b project. Shared helpers live in `Phase7bChecks.Support.cs` and
  `Phase7bExpectations.Common.cs` only. Check names are the part's letter and its number in this report
  (each part numbers its design list in its order); test methods are `Check_<id>…_<what>`. A method
  serves one provider, unless it shows the providers side by side (part A's blocks, part B's palette), and
  stays under about 40 s alone, timed with `bash scripts/time-scripted-check.sh "<method>" 7b` before its
  part's merge (0.5).
- **One test method per app start**, a restart on one data folder a plain `[Fact]` with one
  `FreshApplication.RunAsync` half per start, the one `FakeTokenStore` handed to every start: as 7a. The
  real window never restarts.
- **Defaults.** 1100×700 and dark unless a check names 1920×1080 or light (0.5). Every part uses the one
  scenario "hosting7b" (2.6, below): `Phase7bChecks.ServeAsync()` gives a fresh copy served by the three
  fakes; `StartEmptyAsync(data, served, tokens, width, height)` starts one empty tab with the three fakes
  and waits until the start-up work has ended; `OpenAsync(data, served, repository = "mobile-app", signIn,
  tokens, width, height, theme, systemDark)` opens one tab on `mobile-app`, `manifest` or `web-app`, signs
  in through the session to each provider of `signIn` (by default the repository's: Azure DevOps for
  `mobile-app` and `manifest`, with the organization Fernhill-Labs; GitLab for `web-app`; `[]` none), waits
  until the watchers are quiet and rests the pointer. Every session has what phase 6's 17.2 gives it, as
  7a.
- **The providers in the harness** (design 0.2, 0.7, 1.8, 1.9; D362, D363). Every test process starts
  with `VISUALCOMMIT_GITHUB_URL`, `VISUALCOMMIT_AZURE_DEVOPS_URL` and `VISUALCOMMIT_GITLAB_URL` at `none`,
  `VISUALCOMMIT_KEYCHAIN=memory`, and without ruling 27's token variables (`GitIsolation`), so a start has
  a provider on only when the check gives its fake (`gitHub:`, `azureDevOps:`, `gitLab:` of
  `ShellDriver.Start` and `StartWithTabs`) or `hosting:` options (never both; an in-process fake is
  refused). Every HTTP client of the session goes through `LoopbackOnlyHandler`: `RefusedRequests` is empty
  at every check's end. With a provider off nothing of it shows and no request goes to it.
- **The fakes and their ports** (D363, D265). Azure DevOps' fake on `127.0.0.1:47397`, GitLab's on
  `47398` and 7b's own fake of GitHub on `47399` (`AuthRemoteServer.Phase7b*Port`; 47396 stays reserved),
  which only this project and its real-window twin bind. A fake whose port is taken falls back to a free
  one (`PortWasTaken`, noted in the gate's Run) and the copy's remotes are rewritten with `git remote
  set-url`; every expected text that holds a port goes through `Phase7bExpectations.WithPorts(text,
  served)`. Each new fake's `Requests` records each API and page request after its answer as a
  `FakeServiceRequest`: method, host, raw path, query, route key (lower-case: `connection:fernhill-labs`,
  `areas`, `user`), the authorization's form (`None`, `Bearer`, `Basic`, `BasicEmptyUser`, `PrivateToken`,
  `Query`, `Other`), the login the fake took the token for, the status and a write's body, never a token;
  GitHub's fake records 7a's `FakeGitHubRequest`. `GitRequests` record git's requests. A step's requests
  are read after a mark (each fake's `Requests.Count` before the step); "requests none" means none in any
  of the three. Holds (`HoldNext(route)`, released after 120 s at the latest) and faults (`FailNext`) are
  released and disposed in a `finally`. Each fake's `Errors` is empty at every check's end, and no request
  was answered 400 or 415 unless a step names it.
- **The keychain** is a `FakeTokenStore` (7a's, with `FailNextDelete`), new per start unless the check
  hands one over; `Calls` names each call and its key, never a token: `probe`, `save <key>`, `read <key>`,
  `delete <key>`, the key as `TokenKey.Of(account)`'s text, lower-cased: `github:127.0.0.1:47399:octo`,
  `gitlab:127.0.0.1:47398:ada`, `azure-devops:127.0.0.1:47397:fernhill-labs`. The vault probes the store
  at most once per start, when it first needs it, and reads each key at most once per start (9.4). A
  failure's reason is one of `TokenStoreReasons`' texts, as 7a.
- **Tokens** (design 1, 2.5). The scenario's tokens are named by their constants
  (`Phase7bExpectations.AzureDevOpsToken`, `GitLabToken`, `GitLabLegacyToken`, `GitHubToken`, equal to
  `ServedHosting7b`'s) and a part's own by `Part<X>`'s. No token is passed to an `Assert` method or
  written in a failure's message. Every method ends, after the app has closed, with
  `TokenTripwire.AssertAbsent(token, [data folder, the copy's mobile-app/, manifest/ and web-app/],
  calls)` for each token the method used or typed, with the session's call log of every start: no token
  in a file, `.git` included, or in any git call's command, output or error, in any of 2.5's forms (as
  written, URL-escaped, and base64 at the three alignments, which cover `x-access-token:`, Azure DevOps'
  `:` and any user name before it).
- **Pictures of an opened repository while signed in** are taken only after D322's Debug line
  `{repo}: listed {n} open pull requests of {full name}` (`AppLogFile.PullRequestsListed`, any path
  depth), which the session writes for every provider; the avatars line only for GitHub (D349). A
  picture of a state that lists nothing waits for that state instead (as 7a's parts P and G).
- **Git calls** are counted only through `GitCallBudget`, from a `long` mark of
  `ShellDriver.QuietCallMarkAsync()`, presence and order only: as 7a. The URL-rewrites read runs only while
  an account of any provider is signed in (9.9).
- **Dialogs, menus, the toolbar and the status bar** are phase 6's and 7a's: dialogs stack (D104), the
  error dialog is asserted by `AssertErrorDialog` (title, summary, `In <repo>`, git's key line and last
  line, command, buttons left to right), menus header first, git's own texts quoted as Git
  2.56.0.windows.4 prints them (D248) and asserted only by their key line: as 7a.
- **Pictures** are saved by `Shot(app, name)` under `artifacts/visual/phase-7b/scripted/` (its step times
  under `artifacts/visual/phase-7b/timings/`; the real window's captures under
  `artifacts/visual/phase-7b/real-window/`), named by design 0.3's rule
  `^[abpn]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`: the part's letter, the check's number in two
  digits, a step letter running on through the check's methods in the order of its part's methods table,
  then words that name the provider (`azure` for Azure DevOps, `gitlab`, `github`, `three` for all three:
  `a06b-azure-token-masked`). Every name is unique in the report, and `Shot` refuses one saved twice in a
  run. Before a capture with no menu open and no tooltip asked for, `Shot` rests the pointer at 7a's
  neutral point (760, 687 at 1100×700). Every picture passes `LayoutAudit.FindClippedText`
  (`AssertNoClippedText`: shortened text that shows its whole in a tooltip, and text scrolled out of a
  scroll viewer's view, are allowed). A picture whose content varies by nature says so in its Notes
  (a caret, git's version in the status bar, a temporary folder's name, git's output): as 7a.
- **R steps** (design 0.5, 14). A step marked R is repeated in the real window; its picture's name, the
  texts and states it asserts are constants in `Phase7bExpectations.<part>.cs`; its state is built only by
  code both projects compile (`VisualCommit.Testing`, the linked expectations,
  `Phase7bExpectations.PrepareRequestsAsync`), never by `FakeFileLauncher`, `FakeClipboard`,
  `FakeTokenStore`'s hooks or a held request, and it is taken signed in with the scenario's tokens. No 7b
  step opens the browser (ruling 29).
- **The real window** (design 14; D33, D319). Five launches, one method each, 1100×700, dark, in
  `Phase7bOrder`: `Check_A_three_accounts`, `Check_GitLab_sign_in_browse_and_clone`,
  `Check_GitLab_requests_and_create`, then Azure DevOps' two. The three providers are on in every launch
  (`RealApp.Hosting7bEnvironment(served)`: the three URL variables at the fakes, the keychain memory,
  available); each sign-in types the scenario's token into the real dialog, and for Azure DevOps the
  organization part A gives, or finds it prefilled from the tab. Each R picture is captured under its
  scripted twin's name and compared with it (at most 3 percent of the pixels differ, `run.txt`); every
  launch ends with `AssertCleanEnd` (exit 0, the tripwire for `served.Tokens` over the data folder and the
  launch's folders, each fake's `Errors` empty, no request a fake refused for its headers, no `avatars/`
  unless GitHub signed in). It never clicks "Open in browser" or "Copy …", opens no browser, never
  restarts, never uses a real keychain, `<organization>.visualstudio.com`, light or 1920×1080 (14.1).
- **The lagged list** (design 0.4, D262). At the merges whose section names it, the lead runs this
  project whole under `VISUALCOMMIT_WATCHER_LAG_MS=1500`; a failure under the lag gets a fix, never a
  looser test.
- **Earlier pictures** (design 0.7; plan risk 32) are compared by region against references made on
  `master` before any 7b real-window run (`D:/Projects/Personal/gate7b-reference/`) with
  `scripts/compare-pictures.py`. Expected differences: those "Changes to expected results" lists,
  `b06a`'s port when 7a's fake fell back, and the pictures that vary by nature; any other is a finding.

### Scenario facts ("hosting7b", design 2.6; as `Scenarios.Phase7b*.cs` builds it)

What every part uses; each part's own facts are in its own section.

- **The copy.** `Scenarios.Hosting7bAsync()` returns `mobile-app/`; beside it `manifest/`, `web-app/` and
  the fakes' folders `azure-devops/`, `gitlab/` and `github/`. Commits a minute apart on 2026-01-01 (UTC)
  by Octo Cat, Ada Lovelace and Test Author; every name, address (`@example.com`) and id made up.
- **Azure DevOps** (`FakeAzureDevOps`, `AzureDevOpsEndpoints.Local(47397)`: web, git and API on
  `http://127.0.0.1:47397/`, host `127.0.0.1:47397`, no organization hosts). Organization `Fernhill-Labs`
  (`Phase7bExpectations.Organization`, private), project `Mobile App` (private; `%20` in its URLs), users
  Ada Lovelace and Octo Cat (`customDisplayName` their display name); Ada's token `AzureDevOpsToken`, Code
  (Read & write), the organization's first. Repositories in this order: `mobile-app` (`main` 12:00 to
  12:02; `feature/sign-in`, `docs/guide`, `topic/no-request` 12:03 to 12:05), `mobile-app-fork` (its fork,
  plus `fix-typo` 12:11), `sandbox` and `customer-onboarding-and-identity-verification-service` (both
  empty, no default branch). mobile-app's pull requests !11 "Add the sign-in page" (Ada, `feature/sign-in`),
  !12 "Write the guide" (Octo, `docs/guide`, a draft) and !13 "Fix a typo in the greeting" (Ada, from the
  fork's `fix-typo`), each into `main`. Organization `harbor-co` (`OtherOrganization`, private), project
  `Docks`, user Test Author (no token), repository `manifest` (`main` 12:40) and !5 "Update the manifest"
  from `update-manifest`, a branch the repository lacks.
- **GitLab** (`FakeGitLab`, `GitLabEndpoints.Local(47398)`: web and git on `http://127.0.0.1:47398/`, API
  `http://127.0.0.1:47398/api/v4/`). Users `ada` (2001, "Ada Lovelace") and `octo` (2002, "Octo Cat"), both
  Developers (30) of the group `northfield`; ada's fine-grained token `GitLabToken` (the default
  permissions, User: Read and Project: Read among the user permissions included, D385) and legacy token
  `GitLabLegacyToken` (scope `api`). Projects, all private: `northfield/platform/web-app` (`main` 12:20 to
  12:22; `feature/search`, `docs/readme`, `topic/no-request` 12:23 to 12:25), `ada/web-app` (its fork, plus
  `fix-typo` 12:31) and `northfield/platform/services/billing/invoice-generation-and-reconciliation`
  (empty). web-app's merge requests !21 "Add the search box" (ada, `feature/search`), !22 "Draft: Update the
  readme" (octo, `docs/readme`) and !23 "Fix a typo in the footer" (ada, from `ada/web-app`'s `fix-typo`),
  each into `main`.
- **GitHub** (7b's own `FakeGitHub` on `http://127.0.0.1:47399/`, API `…/api/v3/`): user `octo` (1001,
  "Octo Cat", `avatar_url` `http://127.0.0.1:47399/avatars/u/1001?v=4`), his fine-grained token
  `GitHubToken`, the public `octo/web-app` (web-app's `main`).
- **The clones**, each on `main`, their remotes written last on the ports above: `mobile-app/`, origin
  `http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app` (`MobileAppUrl`, the
  service's `<organization>@` form), `main` and `feature/sign-in` tracking, local-only `local-work` 12:06
  "Local change" (Test Author); `manifest/`, origin `http://harbor-co@127.0.0.1:47397/harbor-co/Docks/_git/manifest`
  (`ManifestUrl`; D356); `web-app/`, origin `http://127.0.0.1:47398/northfield/platform/web-app.git`
  (`WebAppUrl`), upstream `http://127.0.0.1:47399/octo/web-app.git` (`WebAppUpstreamUrl`, ruling 5),
  `feature/search` tracking, local-only `local-work` 12:26.
- **Recognition** (6.2, 7.2, 9.6). mobile-app's origin is `HostedRepository(AzureDevOps, "127.0.0.1:47397",
  "Fernhill-Labs/Mobile App", "mobile-app")`: `FullName` `Fernhill-Labs/Mobile App/mobile-app`, `OwnerRoot`
  `Fernhill-Labs`. web-app's listed remote is its origin, GitLab's `northfield/platform/web-app` (the first
  recognised, ruling 5), its upstream GitHub's `octo/web-app`. manifest's origin is harbor-co's, which no
  account of the scenario covers (D356, D366).
- **The start's log lines** (9.2, 9.4), Info, in D353's order: `GitHub: 127.0.0.1:47399.`, `GitLab:
  127.0.0.1:47398.`, `Azure DevOps: 127.0.0.1:47397.` (`{Name}: off.` for a provider off), then for each
  provider on without a record `{Name}: no account on {host}.`. The real window waits for the first three
  (14.3).

### Part A: accounts and sign-in

Part A covers the settings screen's Accounts section with D353's three blocks (design 10.3; D353, D362),
a sign-in dialog per provider (10.4; D345, D360, D378, D385), the window's entry with Azure DevOps'
prefill (10.5), sign-in with the keychain and for the session only (D253), a sign-out per provider, and
each block's "Use for git over HTTPS" switch (D350): checks A1 to A14, design 10.7's list. GitHub's block
and dialog keep 7a's texts, ids and layout (D351, D362); 7a's part A checks stay in 7a's walk-through,
where the new providers are off and only GitHub's block shows. Its R pictures are A2's three accounts at
the section's end, A5's GitLab dialog with a masked token and A6's Azure DevOps dialog with the
organization typed and a masked token, each on one empty tab (10.7, 14.4).

#### Conventions of part A

- **Code** (10.1, 10.7, 10.8): `Phase7bChecks.A.cs` (one method per app start, below),
  `Phase7bChecks.A.Helpers.cs`; `Phase7bExpectations.A.cs` (nested `PartA`: the texts below, the picture
  names, the R pictures as constants, positions, tolerances, part A's own made-up values, and for WP-R
  each provider's sign-in requests as route keys in order, GitHub's `user`, GitLab's `user`, Azure DevOps'
  `connection:<organization in lower case>` then `areas`, the organization texts typed, the box with the
  keyboard focus in each dialog state, and the three-account picture's place as "the end", never a pixel
  value); `Phase7bExpectationTests.A.cs` (`AccountTexts`' and `HostingTexts`' texts equal `PartA`'s, the
  pages' addresses equal the providers' `TokenPage` and `LegacyTokenPage`, the picture names unique and
  matching 0.3's rule). The driver's part A helpers are in `ShellDriver.Phase7a.Accounts.cs` (WP-A's, 1.9):
  7a's GitHub forms unchanged, plus `OpenSignInDialogAsync(HostingProvider, timeout)` (the block's
  `AccountsSignInButton<suffix>` brought into view and clicked, then wait for `SignInDialog`, its question
  to the keychain answered and the keyboard focus in `SignInOrganization` when that box shows and is
  empty, else in `SignInToken`), `SignOutAsync(HostingProvider, bool confirm, timeout)`,
  `WaitForAccountAsync(HostingProvider, condition, timeout)`, `TypeOrganization(string)` (`TypeInto("SignInOrganization",
  …)`: focus, caret at the end, typed) and `AccountsBlock(HostingProvider)`. `TypeToken` and
  `TypeOrganization` type after what a box holds. A step that "replaces" a box's text with another clicks
  the box, presses Ctrl+A and types with `app.Type(…)`; a step that types into a box whose whole text a
  refusal left selected uses `app.Type(…)` alone and says so. "Turn the wheel over `SettingsSection`" is
  `ShellDriver.Wheel` at the section's centre, one notch at a time with the layout settled after each,
  "until the offset stops changing" (10.7).
- **Names in the checks.** "The session" is `Session.Services.Hosting` (`IHostingSession`): its
  `Providers`, `IsOn(p)`, `IsOff`, `AccountOf(p)` (an `AccountStatus`: `State`, `Place`, note kind, the
  account's key, `Login`, `Id`, `Name`, `UserId`, `UseForGit`) and `BindsGit`. A tab's hosting is its
  `RepositoryViewModel.Hosting`, its `Bindings` the session's union (9.6). "Requests" are the three fakes'
  since the step's mark (shared conventions), "calls" the store's `Calls` since its mark, "git calls" the
  call log's since a quiet mark. **A block** is the container `AccountsGitHub`, `AccountsGitLab` or
  `AccountsAzureDevOps` and what it holds; its ids are 7a's followed by the provider's suffix (`""`,
  `GitLab`, `AzureDevOps`; 1.7, 10.3): GitHub's block keeps 7a's ids (`AccountLogin`, `AccountsSignInButton`),
  the others end in their provider (`AccountsSignedOutGitLab`, `AccountLoginAzureDevOps`,
  `AccountUserAzureDevOps`, `AccountUseForGitGitLab`, `AccountsSignOutButtonAzureDevOps`).
- **No token on screen** (10.9). Before every `Shot` of part A, no visible text, automation value or
  automation name in the window holds any of part A's tokens (`GitHubToken`, `GitLabToken`,
  `GitLabLegacyToken`, `AzureDevOpsToken` and the four of `PartA` below): tested with `string.Contains`, a
  failure naming the control and the token's constant, never the token.
- **`accounts.json` is written off the UI thread** under the session's gate, as 7a: a sign-in writes it
  before it sets the account, a sign-out before it clears it, the switches' Save after the screen has
  closed (9.3, 9.4). After a Save a check waits until the parsed file shows the expected fields (polled, at
  the default wait) before it asserts the rest. It is asserted by its parsed records, found by `provider`
  and `host`, never by their order.
- **The screen at 1100×700** is 7a's (its part A conventions): the card exactly at (120, 40), 860×620; five
  nav buttons, `Accounts` last at (140, 223) to (316, 253); the section from x 336, labels in the 132-wide
  column x 336 to 468 (`settingsLabel`, `VcTextSecondaryBrush`), a row's controls from x 480 to 944;
  `Cancel` and `Save` as phase 6's part S. The section (`SettingsSection`, the `ScrollViewer` named
  `SectionScroller`) shows about 495 of its extent at once (10.3). Positions are asserted ±2 where given
  here, the card exactly. Part A uses no other size.
- **The section as it shows** (10.3), top to bottom inside `SettingsAccounts` (spacing 18): `AccountsIntro`
  (`secondary`, wrapping), then the blocks, 24 apart, in D353's order GitHub, GitLab, Azure DevOps; a block
  shows only while its provider is on (`IsShown`), GitHub's also while all three are off, then holding 7a's
  `AccountsOff`. Each block is 7a's: the row labelled with the provider's name (`GitHub`, `GitLab`, `Azure
  DevOps`) whose column holds `AccountsSignedOut<suffix>`, or `AccountLogin<suffix>` (SemiBold, the login
  in the server's spelling), on Azure DevOps followed by `AccountUserAzureDevOps` (`secondary`, the
  account's name) while it has one, then `AccountTokenPlace<suffix>` (`settingsNote`) or
  `AccountNeedsSignIn<suffix>` (`dialogWarning`), then `AccountsSignOutNote<suffix>` when set; 10 below, the
  buttons `AccountsSignInButton<suffix>` and `AccountsSignOutButton<suffix>` (8 apart) and
  `AccountsBusy<suffix>`; "Sign in…" only signed out or needing sign-in, "Sign out…" only with an account;
  then, 18 below, with an account, the row labelled `Git` with `AccountUseForGit<suffix>` and
  `AccountUseForGitNote<suffix>`. Theme brushes 7a's (dark `#9BA3B0` secondary, `#E0A23B` warning,
  `#E5606B` danger, `#7C8CFF` accent, the card `#1A1D24`).
- **The intro** (10.3, 10.9). Measured before any picture of A1: `AccountsIntro`'s text is laid out in
  exactly two lines (`TextLayout.TextLines.Count` 2), and its height equals (±0.5) that of 7a's sentence
  (`PartA.Intro7a`) measured in a `TextBlock` with `AccountsIntro`'s own font family, size, weight and line
  height and `TextWrapping.Wrap` at `AccountsIntro`'s width (`Measure` with that width and an infinite
  height), which is also two lines. So GitHub's block starts where 7a's does.
- **Scrolling** (D353, 10.3). Three signed-in blocks overflow the section at 1100×700 (about 562 of 495);
  the checks read one or two signed-in blocks as fitting, but for A12's `a12b`, which scrolls by less
  than one line of Azure DevOps' switch note and is taken from offset 0 after the wheel (open point 2). The offset, extent and viewport
  are `SectionScroller`'s `Offset.Y`, `Extent.Height` and `Viewport.Height`. "The top" is offset 0; "the
  end" is the largest offset, `Extent.Height − Viewport.Height` (±0.5), asserted before the capture; each
  is reached by turning the wheel until the offset stops changing. A sign-in or a click that focuses a
  lower button brings it into view.
- **The dialog** (10.4), one per provider, named by its title: 7a's card, a `dialogCard` 400 wide (x 350 to
  750), centred vertically, stacked over the screen, which stays open under the dialog's own backdrop
  (D104); no "In {repo}" line. Top to bottom: `DialogTitle`; `SignInHelp` (`dialogText`); the link
  `SignInTokenLink` (accent foreground, no padding, a hand cursor, 6 below the help); on GitLab the second
  link `SignInLegacyTokenLink`, 4 below the first; on Azure DevOps the label `Organization`
  (`promptLabel`) and the box `SignInOrganization` (`dialogBox`, automation name `Organization`) (open
  point 4); then the label `Token` and `SignInToken`, a `SecretTextBox` masked with `•`,
  its automation value empty while masked and its name `Token` (D103, D130); `SignInNoKeychain`
  (`dialogWarning`) when no keychain; one status line, `SignInChecking` (`secondary`), `SignInError`
  (`dialogError`) or `SignInSaveFailed` (`dialogWarning`); `CancelButton` (`VcActionButton`) and
  `ConfirmButton` "Sign in" (`VcPrimaryButton`) right-aligned. Azure DevOps' card is at most about 345 high
  (open point 7). "Sign in" is enabled while the trimmed token (and on Azure DevOps the trimmed
  organization) is not empty and no check runs; Azure DevOps' link only while the organization box gives
  an organization (`AzureDevOpsProvider.OrganizationOf`), and while disabled it has the disabled look:
  the theme's disabled foreground `VcTextDisabledBrush` (`#5F6775`, as its disabled buttons) in place of
  the accent, the arrow cursor and no underline under the pointer. Enter in `SignInToken` signs in, as 7a; a step
  whose focus is in `SignInOrganization` clicks "Sign in" instead (open point 4). Esc closes the dialog
  only. A failure from the service keeps both boxes' texts and selects the whole text of
  `SignInOrganization` after `NotFound`, of `SignInToken` after any other failure, with the keyboard focus
  there (10.4); a local refusal (D298, D378, `SignInNotAnOrganization`) sends no request.
- **Sign-in and sign-out act at once** (D256), as 7a: the screen's Cancel and Esc never undo them; only the
  switches wait for Save (D216). After a sign-in from a block the focus goes to that block's "Sign out…",
  after a sign-out to its "Sign in…" (`IdOf`, 10.3).
- **The window's entry** (10.5). A block's "Sign in…" runs `MainWindowViewModel.SignInAsync(provider)`: it
  opens that provider's dialog, Azure DevOps' prefilled with the held account's login, else with the
  `OwnerRoot` of the active tab's first Azure DevOps remote, else empty.
- **Signing in through the session** (`ShellDriver.SignInAsync(provider, token, organization)`, no dialog)
  builds only a state a step starts from (A13, A14); every state a check is about is reached through the
  screen and the dialogs, the three accounts of A2 included.
- **Every method ends**, after closing the app, with the tripwire for every token of "No token on screen"
  that the method used or typed, each fake's `Errors` and the harness's `RefusedRequests` empty, no request
  answered 400 or 415, and `Launcher.Requests` holding only the launches its steps name (A5's two, A6's
  one).
- **Git's version**, as 7a: every machine of the gate and of CI runs Git 2.43 or newer, so each block's
  switch note is `AccountUseForGitNoteOf`'s and each sign-out question has its last sentence.

#### Scenario facts part A uses (2.6, 6.4, 7.4, 9.3; the fakes as WP-LF left them)

- **GitHub's sign-in** (7a's): `GET api/v3/user` (route `user`) with `GitHubToken`, 200, `Login` `octo`.
  The account: key `github:127.0.0.1:47399:octo`, `Login` `octo`, `Id` 1001, `Name` `Octo Cat`, its avatar
  address, `UseForGit` true.
- **GitLab's sign-in** (A1, 7.4): one `GET /api/v4/user` (route `user`), `Bearer`. With `GitLabToken` or
  `GitLabLegacyToken`: 200, `Login` `ada`. With a `glpat-` token the fake never issued: 401, shape A's body
  `{"message":"401 Unauthorized"}`, no `WWW-Authenticate` (MEASURED 2026-11-06 for D373's made-up value),
  whose message the dialog shows. The account: key `gitlab:127.0.0.1:47398:ada`, `Login` `ada`, `Id` 2001,
  `Name` `Ada Lovelace`, no avatar, `UseForGit` true.
- **Azure DevOps' sign-in** (A1, 6.4; D360): at most two GETs on `http://127.0.0.1:47397/<organization as
  typed>/_apis/…?api-version=7.1-preview.1`, each with `Authorization: Basic` of `:` and the token
  (`BasicEmptyUser`) and `X-TFS-FedAuthRedirect: Suppress`, never one without the token. 1. connectionData
  (route `connection:<organization in lower case>`): an organization the fake lacks, 404 `text/plain` "The
  resource cannot be found." before any credential check (MEASURED 2026-11-05 with a token too, D374), no
  `Login`; Ada's token in Fernhill-Labs, 200 with her identity, `Login` Ada's (`Fernhill-Labs/` and her
  made-up id); a value of the fake's PAT forms (prefixed `azdo_FAKE-`, 84 characters with `AZDO` at 76, or
  52 of `a` to `z` and `2` to `7`) that it never issued, 401 with an empty body and one `Basic realm`
  (MEASURED 2026-11-05), no `Login`; a value in none of those forms, in an organization that allows public
  projects 200 with the Anonymous identity (MEASURED 2026-11-05 in the sandbox's organization, which allows
  them, MEASURED 2026-11-07), in a private one such as Fernhill-Labs what no credential gets there, 401
  TF400813 naming the anonymous user (READ g1 G0.2; open point 8), and in Fernhill-Labs the Anonymous 200
  only through the fault `FakeAzureDevOpsFault.AnonymousIdentity` (A9), no `Login`. 2. Only after a
  member's 200: the core resource area,
  `/<organization>/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf` (route `areas`, no credential
  check, no `Login`), whose `locationUrl` `http://127.0.0.1:47397/Fernhill-Labs/` gives the stored spelling.
  The account: key `azure-devops:127.0.0.1:47397:fernhill-labs` (`AccountKey` lower-cases the
  organization), `Login` `Fernhill-Labs` in whatever case it was typed, `Id` 0, `Name` `Ada Lovelace` (her
  `customDisplayName`, neither e-mail-shaped nor her sign-in name), `UserId` her made-up id in lower case,
  no avatar, `UseForGit` true.
- **`accounts.json`** (9.3, 1.8) after the sign-ins: `version` 1 and one record per provider, found by
  `provider` and `host`: GitHub's 7a's eight fields (`provider` `github`, `host` `127.0.0.1:47399`, `login`
  `octo`, `id` 1001, `name` `Octo Cat`, `avatarUrl` `http://127.0.0.1:47399/avatars/u/1001?v=4`,
  `useForGit`, `tokenState`); GitLab's the same eight (`gitlab`, `127.0.0.1:47398`, `ada`, 2001, `Ada
  Lovelace`, `avatarUrl` null, `useForGit`, `tokenState`); Azure DevOps' nine (`azure-devops`,
  `127.0.0.1:47397`, `Fernhill-Labs`, `id` the number 0, `Ada Lovelace`, `avatarUrl` null, `useForGit`,
  `tokenState`, `userId` her id in lower case). No field holds an e-mail address: the file holds no `@`.
  Its text holds no token (the tripwire).
- **The pages** (D295, 6.6, 7.4): GitHub's 7a's, `http://127.0.0.1:47399/settings/personal-access-tokens/new?name=VisualCommit&contents=write&pull_requests=write`
  (not clicked in 7b); GitLab's fine-grained `http://127.0.0.1:47398/-/user_settings/personal_access_tokens/granular/new`
  and legacy `http://127.0.0.1:47398/-/user_settings/personal_access_tokens?name=VisualCommit&scopes=api`;
  Azure DevOps' `http://127.0.0.1:47397/Fernhill-Labs/_usersSettings/tokens`. Nothing answers them: the
  fake launcher records them.
- **The prefill** (10.5): `mobile-app`'s origin's `OwnerRoot` is `Fernhill-Labs`; signed out of Azure DevOps
  its tab sends nothing (D257, D350) and its requests section is in the signed-out state (D366).
- **`web-app`'s bindings** (A14; 9.6, 8.2): with GitLab and GitHub signed in, its hosting's `Bindings`
  hold the GitLab account's for the origin's URL and the GitHub account's for the upstream's (any twins
  are WP-C's rule and are not counted); Azure DevOps' account covers neither.
- **Part A's own values** (`Phase7bExpectations.PartA`): the organization texts `Organization`
  (`Fernhill-Labs`), `OrganizationLower` (`fernhill-labs`, typed in A2 and by WP-R's three-account
  launch), `UnknownOrganization` (`Fernhill-Lab`, in the grammar, unknown to the fake) and
  `NotAnOrganization` (`Fernhill Labs`, outside the grammar); the made-up tokens `UnknownGitLabToken` (of
  GitLab's `glpat-` form, never issued), `UnknownAzureDevOpsToken` (of the fake's `azdo_FAKE-` form, never
  issued: no provider's form by `TokenFormOf`, but PAT-shaped to the fake), `PlainValue` (letters, digits
  and hyphens, of no provider's form and in none of the fake's PAT forms) and `AzureDevOpsForm` (84
  characters with `AZDO` at 0-based 76, 10.8's made-up form, Azure DevOps' by `TokenFormOf`).

#### Test methods and app starts (part A)

Nine methods, ten app starts; each starts with a fresh data folder and, but for `Check_A3_three_off`,
which needs no fake, with `ServeAsync()`. A3's two starts, on two data folders, are two methods (one
method per app start; only a restart on one data folder shares a method, A12's). Their git work is
light: an empty tab's start and each screen opening on it make one or two git processes; `mobile-app`'s
and `web-app`'s openings and a sign-in's refresh some more. Each method stays well under 40 s alone.

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_A1_A2_signed_out_to_three_signed_in` | A1, A2 | one empty tab (`StartEmptyAsync(data, served)`), the three fakes, a new store | 1100×700, dark | `a01a`, `a02a`, `a02b`, `a02c`, `a02d` |
| 2 | `Check_A3_gitlab_off` | A3 (GitLab off) | one empty tab with the fakes of GitHub and Azure DevOps alone (`ShellDriver.Start(data, gitHub: served.GitHub, azureDevOps: served.AzureDevOps)`), a new store | 1100×700, dark | `a03a` |
| 3 | `Check_A3_three_off` | A3 (the three off) | one empty tab with no fake and nothing served (`ShellDriver.Start(data)`), a new store | 1100×700, dark | `a03b` |
| 4 | `Check_A5_A8_A11_gitlab_dialog_links_and_refusals` | A5, A8 (GitLab), A11 (GitLab's and GitHub's dialogs) | one empty tab, the three fakes, a new store | 1100×700, dark | `a05a`, `a05b`, `a11a`, `a08a` |
| 5 | `Check_A6_to_A11_and_A4_azure_dialog` | A6 (empty, typed), A11 (Azure DevOps'), A8 (Azure DevOps'), A9, A10, A7, A4 | one empty tab, the three fakes, a new store | 1100×700, dark | `a06a`, `a06b`, `a11b`, `a11c`, `a08b`, `a09a`, `a10a`, `a07a`, `a04a` |
| 6 | `Check_A6_azure_dialog_prefilled_from_the_tab` | A6 (prefilled) | one tab on `mobile-app` (`OpenAsync(data, served, signIn: [])`), signed out | 1100×700, dark | `a06c` |
| 7 | `Check_A12_session_only_and_its_restart` (a `[Fact]`, two starts on one data folder) | A12 | one empty tab per start, the three fakes; one store with `Available = false` for both | 1100×700, dark | `a12a`, `a12b`, `a12c` |
| 8 | `Check_A13_one_sign_out_keeps_the_others` | A13 | one empty tab; the three providers signed in through the session after the start | 1100×700, dark | `a13a`, `a13b`, `a13c` |
| 9 | `Check_A14_each_switch_saved_alone` | A14 | two tabs (`StartWithTabs(data, [web-app, null], activeTab: 0, gitHub:, azureDevOps:, gitLab:)`); the three signed in through the session after `web-app` has loaded | 1100×700, dark | `a14a`, `a14b` |

#### Texts these checks rely on (part A)

Design 10.2's texts and 7a's that part A keeps, word for word (`OperationTexts.AccountTexts`, its `…Of(p)`
members, which return 7a's constants for GitHub, and `HostingTexts`). A `{reason}` or `{message}` loses a
full stop at its end before the sentence's own; `{org}` is the Azure DevOps organization (the account's
login, or in the dialog the organization the box gives).

- **Section list and section**: the nav button and section `Accounts` (`HostingTexts.AccountsSection`);
  the blocks' row labels `GitHub`, `GitLab`, `Azure DevOps` (`HostingTexts.Name`); the second row's label
  `Git`.
- `AccountsIntro`, one for all (10.2): `With a GitHub, GitLab or Azure DevOps account, VisualCommit lists and creates pull requests (merge requests on GitLab) and clones your repositories.`
  7a's sentence, which A1 measures it against (`PartA.Intro7a`): `With a GitHub account, VisualCommit lists and creates pull requests, shows authors' pictures and clones your repositories.`
- 7a's, the same in every block: `AccountsOff` `GitHub is turned off for this session.` (GitHub's block,
  all three off); `AccountsSignedOut` `Not signed in.`; buttons `Sign in…`, `Sign out…` (which opens the
  confirmation), `Signing out…`; `AccountTokenPlace` keychain `The token is kept in this computer's keychain.`
  and NoKeychain (D253) `No keychain was found on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again at the next start.`;
  `AccountNeedsSignIn` SessionEnded `The token was kept for the last session only. Sign in again.` (7a's
  TokenMissing, ReadFailed, SaveFailed and sign-out note stay, provoked by no 7b check);
  `AccountUseForGit` `Use for git over HTTPS`.
- `AccountUseForGitNoteOf(p, login)`: GitHub's 7a's `Clone, fetch, pull and push to GitHub over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.`;
  GitLab's `Clone, fetch, pull and push to GitLab over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.`;
  Azure DevOps' (D350) `Clone, fetch, pull and push to Fernhill-Labs's repositories on Azure DevOps over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.`
  (open point 3).
- `NeedsSignInRefusedOf` (no part A check provokes it): GitLab's `GitLab refused the token. Sign in again with a new token.`,
  Azure DevOps' `Azure DevOps refused the token. Sign in again with a new token.`
- `AccountUserAzureDevOps`: the account's `Name`, in the scenario `Ada Lovelace`.
- **The confirmations** (D291, D296; 10.2): GitHub's 7a's (`Sign out of GitHub?`). GitLab's title `Sign out of GitLab?`,
  question `Sign out of GitLab as ada? VisualCommit removes its token from this computer and no longer lists merge requests. Git over HTTPS asks your own credential helpers again.`;
  Azure DevOps' title `Sign out of Azure DevOps?`, question `Sign out of Azure DevOps for Fernhill-Labs? VisualCommit removes its token from this computer and no longer lists pull requests. Git over HTTPS asks your own credential helpers again.`;
  no detail; buttons `Cancel` and `Sign out`, the latter the primary button, not the danger one.
- **The dialogs' titles** (`HostingTexts.AddAccountTitleOf`): `Add GitHub account`, `Add GitLab account`,
  `Add Azure DevOps account`.
- `SignInHelpOf`: GitHub's 7a's. GitLab's (D385) `Paste a personal access token from GitLab. A fine-grained token needs User: Read and Project: Read among its user permissions, and Project: Read, Merge Request: Read and Create, and Code: Download and Push for the groups and projects you work in; a legacy token needs the api scope.`
  Azure DevOps' `Type your organization, then paste a personal access token made for it in Azure DevOps with the scope Code (Read & write). The organization can be its name or its address on dev.azure.com or visualstudio.com.`
- **The links** (`TokenLinkOf`, its tip; `LegacyTokenLink`): GitHub's 7a's (`Create a token on GitHub`);
  GitLab's `Create a fine-grained token on GitLab`, tip `Opens GitLab's form for a new fine-grained token in your browser.`,
  and `Or create a legacy token, filled in`, tip `Opens GitLab's form for a legacy token with the api scope in your browser.`;
  Azure DevOps' `Create a token on Azure DevOps`, tip `Opens the organization's page for new tokens in your browser.`
  Their addresses are the scenario facts' pages.
- **The boxes' labels**: `Organization` (`OrganizationLabel`, Azure DevOps'), `Token` (7a's).
- `SignInNoKeychain`: D253's text above. `SignInChecking`: `Checking the token…`.
- **`SignInError`, the service's answers** (`SignInFailedOf(failure, org)`): GitHub's 7a's. GitLab's,
  7a's with "GitLab": `BadCredentials` `GitLab refused the token: {message}.` (`GitLab refused the token: 401 Unauthorized.`);
  a `Network` failure its message as it is (D284), which the HTTP core words `Could not reach GitLab.` or
  `GitLab did not answer within 30 seconds.` (`HostingHttpTexts.Network` and `Timeout`, both of kind
  `Network`); any other kind `GitLab answered: {message}.`. Azure DevOps':
  `NotFound` `Azure DevOps has no organization named {org}.` (`Azure DevOps has no organization named Fernhill-Lab.`);
  the Anonymous identity (`failure.Detail` `AzureDevOpsProvider.AnonymousDetail`) `Azure DevOps did not take the token as a sign-in and answered as it does for a visitor. Paste the whole token, made for {org}.`;
  any other `BadCredentials` `Azure DevOps refused the token for {org}. Check that it was made for this organization and has not expired.`;
  a `Network` failure its message as it is (D284), `Could not reach Azure DevOps.` or `Azure DevOps did not
  answer within 30 seconds.`; any other kind 7a's with "Azure DevOps", `Azure DevOps answered: {message}.`.
  `SignInUnreachableOf` (`Could not reach GitLab.`, `Could not reach Azure DevOps.`; GitHub's 7a's
  `Could not reach GitHub.`) is the dialog's text when the check ends without a worded failure, as 7a's
  dialog uses `SignInUnreachable`. No part A check provokes a network failure or that text.
- **`SignInError`, local refusals**: 7a's D298 `The token can contain only letters, digits and punctuation, without spaces or line breaks.`;
  `SignInNotAnOrganization` `Type the organization's name, of letters, digits and hyphens, or its address on dev.azure.com or visualstudio.com.`;
  `SignInOtherProviderTokenOf(p, other, org)` (D378): in GitLab's dialog `This looks like a {other} token. Paste a token made on GitLab.`,
  in Azure DevOps' `This looks like a {other} token. Paste a token made for {org} on Azure DevOps.`, in
  GitHub's `This looks like a {other} token. Paste a token made on GitHub.`; before "Azure DevOps" its "a"
  is "an": `This looks like an Azure DevOps token. Paste a token made on GitLab.`
- `SignInSaveFailed` (7a's, provoked by no 7b check) and the buttons `Cancel`, `Sign in`, `Close` (7a's).

### Part B: browse and clone

Part B covers the clone form's button per provider and "From an account…" (design 11.3; D354, D261,
D302), the repository browser per provider with its switch between the signed-in providers (11.4),
listed, loading and failed in its provider's words, the two long names cut at their start and whole in
their tooltips (11.4, ruling 3), the clones of GitLab's `northfield/platform/web-app` (a nested path) and
of Azure DevOps' `mobile-app` (`Fernhill-Labs@`, `%20`) through the app's helper (D350, ruling 15), and
the palette's three commands, one signed out signing in first (11.5; D354): checks B1 to B8, design
11.7's list. GitHub's button, browser and command keep 7a's texts, ids and look (D351, D362); 7a's part
B checks stay in 7a's walk-through, where the new providers are off. Its R pictures are B3's two lists,
`b03a-gitlab-listed` and `b03b-azure-listed`, each with its provider alone signed in through Settings,
Accounts and the dialog, the screen closed by Esc, the form opened from the welcome page, no switch, the
filter empty and nothing selected (11.7, 14.4); the real window also repeats B1's button text and B7's
two clones in the real process, with Git for Windows' `sh` running the helper, comparing no capture
there. No picture of part B shows an opened repository of a new provider, the clones' tabs included
(0.4, 11.7: WP-P's merge changes them): a clone is asserted through git, the fakes' records, the call
log, the app's log and the tripwire.

#### Conventions of part B

- **Code** (11.1, 11.6, 11.7): `Phase7bChecks.B.cs` (`Phase7bChecksB`, one method per app start,
  below) and `Phase7bChecks.B.Helpers.cs` (the waits for the browser and its load, and snapshots of the
  form, the browser, its switch and its rows, copied from `Phase7aChecks.B.Helpers.cs` with 7b's names,
  naming their source); `Phase7bExpectations.B.cs` (nested `PartB`: 11.2's texts and 7a's that part B
  keeps, the failure texts and log lines below, each provider's rows as full name and detail in order
  (`PartB.GitLabRows`, `PartB.AzureDevOpsRows`, `PartB.GitHubRows`), its button text, its row to choose
  with that row's clone URL and folder name, the lists' routes and queries, the palette queries, the
  picture names, the R pictures as constants, positions and tolerances, written from this report and not
  from `CloneTexts`; for WP-R, per provider the button text, the row list, the row to choose, its URL
  through `WithPorts` and the R picture's name, 11.7); `Phase7bExpectationTests.B.cs` (`CloneTexts`'
  `…Of(p)` members and `FromAnAccount`, and the `HostingTexts.Describe` texts pictured, equal `PartB`'s;
  the picture names unique and matching 0.3's rule). Part A's driver helpers (`OpenAccountsAsync`,
  `OpenSignInDialogAsync(p)`, `TypeOrganization`, `TypeToken`, `ConfirmSignInAsync`,
  `WaitForAccountAsync(p, …)`), 7b's `SignInAsync(p, token, organization)`, phase 6's palette helpers
  (`PressPalette`, `TypeInPalette`, `PaletteSnapshot`, `Palette.Shown`) and the pointer's
  (`RestPointerOnAsync`, `WaitForToolTipAsync`) are used as they are.
- **Names in the checks** are part A's and 7a's part B's: "the form" is the clone form of the active
  tab's welcome page (`CloneForm`), "the button" its `CloneFromGitHubButton`; "the browser" the open
  `CloneFromGitHubDialogViewModel` (11.4's `Providers`, `ShowsSwitch`, `Provider` and `Title`, 7a's
  `Rows`, `Filter`, `Selected`, `StatusText`, `ErrorText`, `MoreText` and `SsoText`) and its view;
  "rows" its `Rows` as its `CloneFromGitHubItem`s draw them; "the switch" its flat buttons; "the log" the
  app's log file's lines since a mark, Debug included. The lists' routes are GitLab's `projects`, Azure
  DevOps' `repos:fernhill-labs` and GitHub's `user/repos` (7a's). A fake records a request after its
  answer, so a step that asserts requests after a load first waits until the fake holds the last one it
  expects.
- **Signing in.** Methods 1 and 2 sign in through Settings, Accounts: the block's `Sign in…`, its dialog
  (part A's `a05b` or `a06b` state, Azure DevOps with `PartA.Organization` typed), `Sign in`, then Esc and
  the screen's `IsClosed`, as WP-R's two "Sign-in, browse, clone" launches do (14.4); method 6 through
  the dialog the palette's command opens; methods 3 to 5, and method 6's first account, through the
  session (`SignInAsync(provider, token, organization)`, Azure DevOps' organization `Fernhill-Labs`), a
  state their steps start from.
- **The form** (11.3; 7a's part B): on an empty tab at 1100×700 the welcome page's `CloneForm` lies at
  x 284 to 676, its texts and controls as phase 1's `13a` shows them; opening it (`CloneRepoButton`)
  focuses `CloneUrlBox`. The button keeps its id `CloneFromGitHubButton`; its text and automation name
  bind `BrowseButtonText`; it has no tooltip. It is collapsed while no account of any provider is signed
  in, an account needing sign-in counting as none (D302), and then takes no room (`CloneUrlBox` from
  x 284 to 676). Shown, it reads `FromButtonOf` of the one signed-in provider, else `From an account…`
  (D354), whole at the URL row's right end, `VcActionButton` (`Padding` 10,4), its right edge 676 and its
  height `CloneBrowseButton`'s, 6 right of `CloneUrlBox`, which starts at x 284: a wider text narrows only
  the URL box (11.3), and the rows below stay as `13a`'s. Positions come from the controls' bounds, ±1.
  The button is asserted enabled only with no dialog over the form (B1, B2 step 1, B7 after `Choose`), as 7a's
  helper does (`Phase7aChecks.B.Helpers.cs:216-221`): under a browser over the form its command waits
  for the browser, so it is drawn disabled there, as in 7a (open point 21 for the palette's browser), and
  "shown" or "with" behind a backdrop (B3 to B5, B8) means visible.
- **The browser** (11.4; 7a's part B): `CloneFromGitHubDialog`, named by its title `Clone from {name}`,
  on the dialog layer under its backdrop (D104), a `wide` `dialogCard` 460 wide at x 320 to 780, centred
  vertically; top to bottom `CloneFromGitHubTitle` (`dialogTitle`), no repository line; with two or three
  signed-in providers the switch; `CloneFromGitHubFilter` (`dialogBox`, named `Filter`), enabled in every
  state; `CloneFromGitHubList` (named by the title), 58 to 232 high, shown in every state; the lines
  `CloneFromGitHubStatus` (`settingsNote`), `CloneFromGitHubError` (`dialogError`), `CloneFromGitHubMore`
  and `CloneFromGitHubSso` (`settingsNote`), each only with text, in that order; `CancelButton` and
  `ConfirmButton` `Choose`, right-aligned. With one provider there is no switch: 7a's card, laid out as
  7a's. A row is 28 high, `Padding` 10,0, the full name in `#E4E7EC`, 12 after it the detail in `#9BA3B0`
  at 11.5 (`PickRowPanel`; hidden when null); its automation name is the full name, its help text the
  detail, its tooltip the full name, a line break and the detail; selected `#2A3150`, under the pointer
  `#2F3542`. Theme brushes only; the card `#1A1D24`.
- **The switch** (11.4; D354, D216): under the title, 8 above the filter; one flat button per signed-in
  provider in D353's order (GitHub, GitLab, Azure DevOps), 4 apart, each `VcFlatButton` 28 high with
  `Padding` 10,0 as the settings screen's Git scope switch (`GitScopeGlobal`, `GitScopeRepository`): the
  shown provider's `selected` (the control background `#272C36`, primary text `#E4E7EC`), the others
  transparent in the secondary text `#9BA3B0`; ids `CloneFromGitHubProviderGitHub`,
  `CloneFromGitHubProviderGitLab` and `CloneFromGitHubProviderAzureDevOps`, texts and automation names
  the display names; no combo box. A click shows that provider: the title follows; its list is asked the
  first time it shows, on the dialog's one cancellation, never again in that dialog; its rows, status,
  error and lines are kept and shown while it shows; the filter is kept and the selection cleared; an
  answer for a provider not shown changes nothing on screen. The form's button opens the browser on the
  first signed-in provider in D353's order (`BrowseAsync(null)`), a palette command on its own provider
  (`BrowseAsync(provider)`).
- **Long names** (11.4; ruling 3): the full name's `TextBlock` cuts at its start (`TextTrimming`
  `LeadingCharacterEllipsis`): a name too long for its row is one collapsed line (`HasCollapsed`) that
  begins with `…` and ends with the repository's name whole; a name that fits draws as before, pixel for
  pixel. Beside a cut name the detail has no room (`PickRowPanel` gives the name its width first; open
  point 12). `LayoutAudit.FindClippedText` lets both texts of a cut row through because the item's
  tooltip holds them (its tooltip rule; the `ListBoxItem` is the third element above the text).
- **Clone URLs** (11.4; ruling 15): a row's `CloneUrl` keeps an Azure DevOps URL whose user part is
  exactly its organization, without case and without a password, as the API gave it (`Fernhill-Labs@`);
  any other user part is removed, as 7a's.
- **The palette's commands** (11.5; D354): `Clone from GitHub…`, `Clone from GitLab…` and `Clone from
  Azure DevOps…` right after `Clone repository…`, each listed while its provider is on (every part B
  start has the three on), none with a key. Run while its provider's account is not signed in, a command
  first opens the window's sign-in entry for that provider (10.5: that provider's dialog, Azure DevOps'
  empty on an empty tab with no account held); a cancelled sign-in stops there with no tab and no form; a
  signed-in provider is asked nothing. Then the form on the active tab if it is empty, else on a new tab
  at the end (7a's `TabForOpening`), and the browser over it on that provider, with the switch when two
  or more are signed in. No settings screen opens.
- **No token on screen** (as part A's): before every `Shot` of part B, no visible text, automation value
  or automation name in the window holds `GitHubToken`, `GitLabToken`, `GitLabLegacyToken` or
  `AzureDevOpsToken` (`string.Contains`; a failure names the control and the constant, never the token).
- **Pictures**: the neutral point (760, 687) is the backdrop under the browser's card (every card of
  part B ends above y 687 at 1100×700) or the status bar's empty middle under the form. No picture is
  taken of a tab that shows a repository.
- **Git's version** (8.6): on 2.46 and later (the laptop's 2.56) a bound URL's first git request carries
  the account's token; on 2.31 to 2.45 (CI's containers' 2.43) it goes without credentials and is
  refused (401), and an `<organization>@` URL's second carries the organization with an empty password,
  refused too (401: libcurl's own retry with the URL's user, H2; r7 H4b), before the app's helper
  answers and the third carries the token. Each clone's expected git requests hold for both.
- **Every method ends**, after closing the app, with the tripwire for each token it used or typed over
  the data folder, the copy's `mobile-app/`, `manifest/` and `web-app/` and, in methods 1 and 2, the
  `clones` folder (the clone, `.git` included), with the session's call log (the clone's command, output
  and error included); each fake's `Errors` and the harness's `RefusedRequests` empty; no API request
  answered 400 or 415; `Launcher.Requests` empty (part B launches nothing).

#### Scenario facts part B uses (2.6, 6.5, 7.4; the fakes as WP-FA, WP-FL and WP-LF left them)

- **GitLab's list** (A2, 7.4): one `GET /api/v4/projects` with the query
  `membership=true&archived=false&order_by=last_activity_at&sort=desc&per_page=100` (route `projects`),
  `Bearer`, `Login` `ada`: the projects ada is a member of, through the group `northfield` too, most
  recently active first (the fake's `last_activity_at` is each project's creation, a minute apart by id,
  the last added the most recent), one page and no next (so no `MoreExistOf` line). `PartB.GitLabRows`:
  1. `northfield/platform/services/billing/invoice-generation-and-reconciliation`: private, empty;
     detail `Private`;
  2. `ada/web-app`: private, a fork of web-app (`forked_from_project`); detail `Private · Fork`;
  3. `northfield/platform/web-app`: private; detail `Private`.

  Each clone URL is the fake's `http_url_to_repo`, `http://127.0.0.1:47398/<path>.git`, with no user part.
- **Azure DevOps' list** (A2, 6.5): one `GET /Fernhill-Labs/_apis/git/repositories` with the query
  `api-version=7.1` (route `repos:fernhill-labs`), `BasicEmptyUser`, `Login` Ada's, unpaged; the fake
  answers Fernhill-Labs' repositories in creation order, and the provider sorts them by name, then
  project, without case. `PartB.AzureDevOpsRows`, each private (its project `Mobile App` is):
  1. `Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service`: empty; detail
     `Private`;
  2. `Fernhill-Labs/Mobile App/mobile-app`: detail `Private`;
  3. `Fernhill-Labs/Mobile App/mobile-app-fork`: a fork of mobile-app (`isFork`); detail `Private · Fork`;
  4. `Fernhill-Labs/Mobile App/sandbox`: empty; detail `Private`.

  Not listed: harbor-co's `manifest` (another organization, D356). Each clone URL is the fake's
  `remoteUrl`, `http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/<name>`, kept with
  its `Fernhill-Labs@` (ruling 15). `MoreExistOf(AzureDevOps)` is null.
- **GitHub's list** (B2's three accounts; 7a's A2): one `GET api/v3/user/repos` with the query
  `per_page=100&sort=updated` (route `user/repos`), with the token, `Login` `octo`; `PartB.GitHubRows` is
  one row, `octo/web-app` (public, no fork, no description: no detail).
- **The clones** (B7): GitLab's row `northfield/platform/web-app`, URL
  `http://127.0.0.1:47398/northfield/platform/web-app.git` (`WebAppUrl`), folder `web-app`, its `main` at
  "Add the docs folder" (12:22); Azure DevOps' row `Fernhill-Labs/Mobile App/mobile-app`, URL
  `http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app` (`MobileAppUrl`),
  folder `mobile-app`, its `main` at "Add the docs folder" (12:02). Each bare repository's `HEAD` is
  `refs/heads/main`. Git's paths are `/northfield/platform/web-app.git/…` on GitLab's fake and
  `/Fernhill-Labs/Mobile%20App/_git/mobile-app/…` on Azure DevOps'. A clone's tab lists three open
  requests: web-app's !21 to !23 (D322's `web-app: listed 3 open pull requests of
  northfield/platform/web-app`), mobile-app's !11 to !13 (`mobile-app: listed 3 open pull requests of
  Fernhill-Labs/Mobile App/mobile-app`).
- **Holds and faults** (3.4, 4.4): `served.GitLab.HoldNext("projects")` and
  `served.AzureDevOps.HoldNext("repos:fernhill-labs")` hold the list's answer.
  `served.GitLab.FailNext("projects", FakeGitLabFault.ServerError)` answers once, before the credential is
  read, 500 `{"message":"500 Internal Server Error"}` with an `x-request-id` (`FAKE-GL-` and six digits),
  which GitLab's client words as `ServerError` with the message `500 Internal Server Error`.
  `served.AzureDevOps.FailNext("repos:fernhill-labs", FakeAzureDevOpsFault.InvalidToken)` answers once,
  after the organization's and the version's checks, 401 with an empty body, one `WWW-Authenticate:
  Basic realm`, the reason phrase `Unauthorized` and an `ActivityId`, which Azure DevOps' client words as
  `BadCredentials` with the message `Unauthorized` (no body: the reason phrase, `MessageOf`), and which
  marks the account at once (9.6's `CallAsync`).

#### Test methods and app starts (part B)

Six methods, six app starts, each from `ServeAsync()` and a fresh data folder; methods 1 and 2 also make
a `clones` folder (a `TempDirectory`) before the app. The heaviest git work is method 1's and method 2's
clone (web-app's or mobile-app's history and branches) and the opened tab's refresh; methods 3 to 6 clone
nothing. Each stays well under 40 s alone, timed with `bash scripts/time-scripted-check.sh "<method>" 7b`
before WP-B's merge (0.5).

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_B1_B3_B6_B7_gitlab_sign_in_list_and_clone` | B1 (GitLab), B3 (GitLab), B6 (GitLab), B7 (web-app) | one empty tab (`StartEmptyAsync(data, served)`), the three fakes, a new store, signed out; `clones` made before the app; GitLab signed in through Settings, Accounts | 1100×700, dark | `b01a`, `b03a`, `b06a`, `b07a` |
| 2 | `Check_B1_B3_B6_B7_azure_sign_in_list_and_clone` | B1 (Azure DevOps), B3 (Azure DevOps), B6 (Azure DevOps), B7 (mobile-app) | as method 1; Azure DevOps signed in through Settings, Accounts | 1100×700, dark | `b01b`, `b03b`, `b06b`, `b07b` |
| 3 | `Check_B4_B5_gitlab_loading_and_failed` | B4 (GitLab), B5 (GitLab) | one empty tab, a new store; GitLab signed in through the session after the start | 1100×700, dark | `b04a`, `b05a` |
| 4 | `Check_B4_B5_azure_loading_and_refused` | B4 (Azure DevOps), B5 (Azure DevOps) | one empty tab, a new store; Azure DevOps signed in through the session after the start | 1100×700, dark | `b04b`, `b05b` |
| 5 | `Check_B2_two_accounts_and_the_switch` | B2 | one empty tab, a new store; GitLab and Azure DevOps signed in through the session after the start, GitHub in step 7 | 1100×700, dark | `b02a`, `b02b`, `b02c`, `b02d`, `b02e` |
| 6 | `Check_B8_palette_three_commands` | B8 | one empty tab, a new store; GitLab signed in through the session after the start | 1100×700, dark | `b08a`, `b08b`, `b08c` |

#### Texts these checks rely on (part B)

- **Design 11.2's texts**, word for word (`OperationTexts.CloneTexts`, its `…Of(p)` members, which return
  7a's constants for GitHub): `FromButtonOf` `From GitHub…` (7a's `FromGitHubButton`), `From GitLab…`,
  `From Azure DevOps…`; `FromAnAccount` `From an account…`; `DialogTitleOf` `Clone from GitHub` (7a's),
  `Clone from GitLab`, `Clone from Azure DevOps`; `PaletteCommandOf` `Clone from GitHub…` (7a's), `Clone
  from GitLab…`, `Clone from Azure DevOps…`; `MoreExistOf` GitHub's 7a's `Only the 1,000 most recently
  updated repositories are listed.`, GitLab's `Only the 1,000 most recently active repositories are
  listed.`, none for Azure DevOps; `SomeHiddenBySso` GitHub's alone. No part B check shows a
  `MoreExistOf` line (open point 17).
- **7a's texts part B keeps** (7a design 9.2, 9.4): `Loading repositories…`; `No repositories to list`
  and `The repositories could not be loaded.` (no part B check shows them; 11.6's tests do); `Choose`;
  `Cancel`; the filter's placeholder and name `Filter`; the flags `Private`, `Fork` and `Archived`, joined
  with ` · ` before a description.
- **The switch's buttons**: `GitHub`, `GitLab`, `Azure DevOps` (`HostingTexts.Name`).
- **`HostingTexts.Describe`** (11.2) for the two failures pictured: GitLab's `ServerError` `GitLab had a
  problem. Try again later.`; Azure DevOps' `BadCredentials` with the login `Fernhill-Labs`,
  `OperationTexts.TokenRefused(AzureDevOps, "Fernhill-Labs")`: `Azure DevOps refused the token of
  Fernhill-Labs. Sign in to Azure DevOps in Settings, Accounts.` GitHub's results stay 7a's. 11.2's other
  rows (`Network`, `RateLimited`, `ApiVersion`, `Malformed`, the other kinds, `BadCredentials` without a
  login, the empty messages) are `HostingTextsDescribeProvidersTests`' (open point 19).
- **Earlier texts**: the form's (phase 1's `13a`); the palette's neighbours `Clone repository…` and
  `Init repository…` (phase 6's 5.4); the dialogs `Add GitLab account`, `Add Azure DevOps account` and
  7a's `Add GitHub account`, as part A's texts give them.
- **Log lines** (Debug included; `{id}` the request id of the answer named). WP-B's, once per failed
  list, GitHub's byte for byte with the provider's name (11.2): `Listing GitLab repositories failed:
  ServerError: 500 Internal Server Error (request {id})` and `Listing Azure DevOps repositories failed:
  BadCredentials: Unauthorized (request {id})`. The HTTP core's (5.2): `GitLab: GET /api/v4/projects
  failed: 500 (request {id}).` and `Azure DevOps: GET /Fernhill-Labs/_apis/git/repositories failed: 401
  (request {id}).` The session's (9.6): for GitLab's 500 `GitLab: listing the account's repositories
  failed: ServerError, 500 Internal Server Error (request {id})`; for Azure DevOps' 401 `Azure DevOps
  account Fernhill-Labs on 127.0.0.1:47397: Azure DevOps refused the token (the API, request {id}); it
  needs signing in again.`, and no "listing … failed" line then. The clone's prompt scope (7a design 4.6,
  D277): `web-app: Clone: git's credential get for 127.0.0.1:47398 answered from the account.` and
  `mobile-app: Clone: git's credential get for 127.0.0.1:47397 answered from the account.`; 7a's 9.3
  Warning `Cloning {url}: git's configuration could not be read; the clone goes without the account's
  token.`, never expected; D322's two lines above. Every host and port goes through `WithPorts`.

### Part P: requests in the left panel and the graph

Part P covers the left panel's requests section for the listed repository's provider (design 12.3;
D351, D355, D362, ruling 24): titled `Merge requests` on GitLab's `web-app` and `Pull requests` on Azure
DevOps' `mobile-app`, its rows with `!n`, a draft and a fork, signed out, loading, failed in
`HostingTexts.Describe`'s words, none and past 300; the panel's filter on `!n`; D356's row on
`manifest`, which asks nothing (D366, D376); ruling 5's listed remote on `web-app`, whose `upstream` is
GitHub's; the section's and a request's menus (12.4); the checkouts of `!21`, `!23` and `!11` with git's
state and `!13`'s reason (12.5; D361, ruling 19); copy link and open in browser through
`FakeFileLauncher` (ruling 29); the graph's badge and a branch row's `!21` (12.3, 12.6): checks P1 to
P14, design 12.8's list, numbered here in its order (open point 22). GitHub's section, menus, checkout,
badge and branch rows keep 7a's texts, ids and look (12.1; D351, D362). No 7b check lists a GitHub
repository (the scenario's GitHub repository is `web-app`'s `upstream` alone): 7a's part P and G checks,
which stay in 7a's walk-through with the new providers off, prove it, their pictures the same files as
`phase-7a-pictures.sha256` lists (12.9; "Changes to expected results"). Its R pictures are P1's
`p01a-gitlab-rows` and `p01c-azure-rows` (the section with its rows, the badge in the graph) and P10's
`p10a-gitlab-section-menu` and `p10c-azure-section-menu`, each in the prepared state that part N's
checks and WP-R's two "Requests and create" launches share (2.7, 12.8, 13.8, 14.2), signed in through
Settings, Accounts as those launches sign in; the real window also reads `!21`'s and `!11`'s menus,
comparing no capture there (14.4 step 2). No part P step opens the browser (ruling 29).

#### Conventions of part P

- **Code** (12.1, 12.7, 12.8): `Phase7bChecks.P.cs` (`Phase7bChecksP`, one method per app start,
  below), `Phase7bChecks.P.Checkout.cs` and `Phase7bChecks.P.Helpers.cs` (the builder's to name: copied
  from `Phase7aChecks.P.Helpers.cs` and `Phase7aChecks.G.Helpers.cs` with 7b's names, naming their
  source: the section's rows by number or message kind, its header, a wait for `PullRequestsListed`
  since a mark, a branch row's `RefPullRequest`, a graph label's bounds and its tooltip, `LabelsInRow`
  with its `Badge`, the panel's scroll); `Phase7bExpectations.P.cs` (nested `PartP`: 12.2's texts per
  provider and 7a's that part P keeps, each provider's rows in order with name, author, draft and tooltip
  (`PartP.GitLabRows`, `PartP.AzureDevOpsRows`), the badges, the menus' lines, the addresses through
  `WithPorts`, the routes and queries, the log lines, the picture names and the R pictures, written
  from this report and not from the App's texts; for WP-R, per provider the repository, the section's
  title and rows, the section's menu and `!21`'s or `!11`'s menu lines (none of them changed by the
  prepare) and the R pictures' names, 12.8); `Phase7bExpectationTests.P.cs` (the provider forms of
  `OperationTexts`' request texts and `MenuTexts`, and `HostingTexts`' `RequestsTitle`, `Number` and
  `SignInToItemOf`, equal `PartP`'s for GitLab and Azure DevOps and 7a's constants word for word for
  GitHub; the picture names unique and matching 0.3's rule). 12.7's classes are WP-P's own. Part A's and
  B's sign-in helpers and phase 3's menu helpers (`AssertMenu`, `AssertHighlighted`, `AssertMenuShape`,
  `AssertMenuAtPointer`, `AssertNoMenuAsync`) are used as they are.
- **Names in the checks** are parts A's and B's and 7a's parts P's and G's. "The section" is the active
  tab's requests section: `LeftPanel.Rows` from the `RefSectionRow` of `RefSection.PullRequests` (titled
  `Merge requests` or `Pull requests`, its key still `section:PullRequests`) to the next header, read as
  `Section(app)`'s lines (`v Merge requests 3`, a row's name, `[Kind] text`). Its "rows" are its
  `PullRequestRow`s (`PullRequestItem`, named `{N} {title}`, called by their number: `!21`), its
  "message row" its `PullRequestMessageRow` (`PullRequestMessage`, named by its text). "The hosting" is
  the tab's `RepositoryViewModel.HostingState` (`PullRequests`, `Remotes`, `RequestRemotes`,
  `Repository`, `List`, `Failure`, `ByRef`) with its `Hosting.Bindings`; a branch row's "`!n`" is its
  `RefPullRequest`. "The graph", "row n" (`SelectedIndex`'s count, no working row), a row's "labels"
  (`LabelsInRow(n)`, whose `Badge` is now a `PullRequestBadge(Number, Title, IsDraft, Provider)`) and
  "the graph's tooltip" are 7a's part G's. GitLab's route keys are `user`, `project:<path>` and
  `mrs:<path>`; Azure DevOps' `connection:fernhill-labs`, `areas`, `repo:fernhill-labs/mobile app/mobile-app`
  and `pulls:fernhill-labs/mobile app/mobile-app` (lower-case, names decoded); GitHub's 7a's.
- **Waiting for a list** (D322; 9.7, 9.9): a step that expects a list waits for its tab's
  `{repo}: listed {n} open pull requests of {full name}` since the step's mark (the line says "pull
  requests" on GitLab too, 9.9), then for the watchers to be quiet; the new providers write no avatars
  line (D349). A step that expects a state without a list (signed out, loading, failed, D356's row)
  waits for the tab's `HostingState.PullRequests` in that state, then for the section's message row,
  then for quiet watchers (7a design 17 item 3; 7a's open point 34).
- **The section** (12.3; 7a's part P): titled `RequestsTitle` of the listed repository's provider
  whenever the hosting has a `Repository`, signed out and D356's state included (ruling 24); without a
  hosted remote phase 1's placeholder `Pull requests`, closed, `0`, no toggle and no menu (7a's line
  exactly). Rows and message rows as 7a's: 26 high at depth 1; `PullRequestNumber` `!n` (secondary),
  `PullRequestTitle` cut with "…", `PullRequestAuthor` (secondary, at most 90 wide) at the right end and
  `PullRequestDraft` `Draft` left of it on a draft only; a message row's text secondary, cut with "…",
  its tooltip whole. Rows come newest first as each provider lists them (GitLab by `created_at`, Azure
  DevOps by id, both descending). D356's row (`PullRequestMessageKind.OtherOrganization`) is a disabled
  button whose tooltip shows (12.3, 7a design 17 item 7): it takes no click; its header toggles, with
  `0`.
- **Menus** are phase 3's and 7a's part P's: a right-click at the centre of a header or a row opens the
  menu with its top-left 2 right of and below the pointer (moved to lie 4 inside the window), the first
  enabled item highlighted, `MenuHeight(items, separators)` high (8 + 24 + 26 × items + 9 ×
  separators) ±2; asking for a menu selects nothing and asks no fake anything.
- **Signing in** (14.4). Methods 1 and 2 sign in through Settings, Accounts as WP-R's "Requests and
  create" launches do: `OpenAccountsAsync()`, `OpenSignInDialogAsync(p)` (Azure DevOps' dialog prefilled
  with `Fernhill-Labs` from the tab's origin, 10.5, the focus in `SignInToken`), `TypeToken(token)`,
  Enter, a wait until the dialog has closed and the account is signed in, Esc, the screen's `IsClosed`
  (open point 28). Methods 3 and 4 sign in through the section's message row (the window's entry,
  10.5). Methods 5 to 7 sign in through the session (`OpenAsync`'s `signIn`), a state their steps start
  from.
- **The prepared state** (2.7, 12.8): methods 1 and 2 call `Phase7bExpectations.PrepareRequestsAsync(served.MobileApp,
  provider)` after `ServeAsync()` and before the app starts (GitLab: `web-app/`; Azure DevOps:
  `mobile-app/`), so `Local branches` holds `topic/no-request` tracking `origin/topic/no-request` and the
  graph its local label; their R pictures are taken in that state, which no row or menu of the section
  reads. Methods 3 to 7 are not prepared.
- **The graph at 1100×700** is 7a's part G's: 440 wide, the columns Branch / Tag (x 0 to 130), Graph
  and Message only on `web-app`'s and `mobile-app`'s four-lane graphs (Date and Author give way, phase
  1's rule; `manifest`'s one-lane graph keeps Date, `2026-01-01 12:40`, by the same rule); a badge
  follows the name inside its label:
  6, the icon 10 (`IconPullRequest`), 3, then `{N}` in the label's typeface, then the label's padding 6,
  all in the label's own text colour; the name is cut first, never the badge; one badge per request, on
  the first of its labels (local before remote, `BadgesOf`). Where a name is cut, where a badge lies and
  whether a second label shows or folds into `+N` are read from the control (`LabelsInRow`,
  `BadgeInRow`), never given here (open point 26).
- **No token on screen** (as part A's): before every `Shot`, no visible text, automation value or
  automation name in the window holds `GitLabToken`, `AzureDevOpsToken` or `GitHubToken`.
- **Git's version** (8.6; part B's): from 2.46 a bound URL's first git request carries the account's
  token; on 2.31 to 2.45 it goes without credentials and gets 401 before the app's helper answers. P11's
  expected git requests hold for both.
- **Every method ends**, after closing the app, with `TokenTripwire.AssertAbsent` for each token the
  method used or typed (methods 1, 3 and 6 `GitLabToken`; 2, 4 and 7 `AzureDevOpsToken`; 5 `GitLabToken`
  and `GitHubToken`) over the data folder and the copy's `mobile-app/`, `manifest/` and `web-app/`,
  `.git` included, with the session's call log (each git call's command, output and error); each fake's
  `Errors` and the harness's `RefusedRequests` empty; no request answered 400 or 415; `Launcher.Requests`
  and `Clipboard.Texts` exactly what its steps name. Every picture passes `LayoutAudit.FindClippedText`:
  a title, a message row or a label cut with "…" whose whole text a tooltip holds is allowed. Two message rows are the one
  exception (the lead's ruling of 2026-11-07, "Changed in this report after its first commit"): D356's
  row `In harbor-co; signed in to Fernhill-Labs` (P8: `p08a`, `p08b`, `p10d`) and GitLab's `More than
  300 open merge requests` (P6: `p06a`) may be cut with "…" at 260 while their tooltips are the reason
  and the action that P8 and P6 give word for word; the audit lets through its one finding on that
  row's text and nothing else.

#### Scenario facts part P uses (2.6, 3.5, 4.5, 6.5, 6.6, 7.4; the fakes as WP-FA, WP-FL and WP-LF left them)

- **The clones' refs** (2.6). `web-app/`: local `main` (HEAD, tracking `origin/main`), `feature/search`
  (tracking `origin/feature/search`, in the folder `feature`) and `local-work` (no upstream); `origin`'s
  `main`, `feature/search`, `docs/readme`, `topic/no-request` and `HEAD`; `upstream`'s `main` and `HEAD`
  (GitHub's `octo/web-app`). `mobile-app/`: local `main` (HEAD), `feature/sign-in` (tracking) and
  `local-work`; `origin`'s `main`, `feature/sign-in`, `docs/guide`, `topic/no-request` and `HEAD`. No tag,
  no stash; each fetch line `+refs/heads/*:refs/remotes/<remote>/*`; each clone's `FETCH_HEAD` the
  template's last fetch's. Remotes are open at the opening, folders closed unless they hold HEAD. The
  prepare adds the local `topic/no-request` (methods 1 and 2).
- **web-app's list** (GitLab; 7.4 A3 and A4, 4.3 to 4.5). A list with A3 unread sends `GET
  /api/v4/projects/northfield%2Fplatform%2Fweb-app` (route `project:northfield/platform/web-app`, A3),
  then `GET /api/v4/projects/northfield%2Fplatform%2Fweb-app/merge_requests` with the query
  `state=opened&per_page=100` (route `mrs:northfield/platform/web-app`, A4), then for `!23`'s fork one
  `GET /api/v4/projects/<ada/web-app's number>` (route `project:ada/web-app`, the lookup, kept by the
  token and the id until `ForgetKept`); each `Bearer`, `Login` `ada`. A refresh with A3 read and the fork
  known sends A4's pages alone. Newest first by `created_at` (a minute apart by id), `PartP.GitLabRows`:
  1. `!23 Fix a typo in the footer`, author `ada`, head `fix-typo` of `ada/web-app` (label `ada:fix-typo`);
  2. `!22 Draft: Update the readme`, author `octo`, head `docs/readme`, a draft (its title starts
     `Draft:`, 4.2; open point 25);
  3. `!21 Add the search box`, author `ada`, head `feature/search`.

  Each into `main`; `web_url` `http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests/<iid>`;
  the section's page (`PullRequestsPage`) `http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests`.
  `ByRef`: `refs/heads/feature/search` and `refs/remotes/origin/feature/search` → !21,
  `refs/remotes/origin/docs/readme` → !22; `!23`'s head is in `ada/web-app`, no remote of `web-app`, so
  it maps nothing, and no `refs/remotes/upstream/…` ref maps anything (9.7). The fake writes each
  request's `refs/merge-requests/<iid>/head` in `gitlab/northfield/platform/web-app.git` at its ref sync
  before a list (4.5): `!23`'s at `ada/web-app`'s `fix-typo`, 12:31 "Fix a typo in the footer" by Ada
  Lovelace. On a 200 GET it sends a weak `ETag` and answers a matching `If-None-Match` with 304 (4.4).
- **mobile-app's list** (Azure DevOps; 6.5 A3, 6.6 A4, 3.3 to 3.5). A list with A3 unread sends `GET
  /Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app` with `api-version=7.1` (route
  `repo:fernhill-labs/mobile app/mobile-app`, A3), then `GET
  /Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests` with
  `searchCriteria.status=active&api-version=7.1&$top=100&$skip=0` (route `pulls:fernhill-labs/mobile app/mobile-app`,
  A4: one page of three rows, under 100, so no `$top=1` probe, 6.8); each `BasicEmptyUser`, `Login`
  Ada's. Id descending, `PartP.AzureDevOpsRows`:
  1. `!13 Fix a typo in the greeting`, author `Ada Lovelace` (`createdBy.displayName`), head `fix-typo`
     of the fork `Fernhill-Labs/Mobile App/mobile-app-fork` (`forkSource`; label `mobile-app-fork:fix-typo`);
  2. `!12 Write the guide`, author `Octo Cat`, head `docs/guide`, a draft (`isDraft`);
  3. `!11 Add the sign-in page`, author `Ada Lovelace`, head `feature/sign-in`.

  Each into `main`; the web address built by the provider (the answer has none, 6.6)
  `http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/<id>`; the section's page
  `http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequests`. `ByRef`:
  `refs/heads/feature/sign-in` and `refs/remotes/origin/feature/sign-in` → !11,
  `refs/remotes/origin/docs/guide` → !12; `!13`'s head is the fork's, no remote here: nothing. The fake
  keeps `refs/pull/<id>/merge` alone in the bare repository, never a head ref (3.5, D361).
- **The graphs** (prepared; their labels signed in with the list; rows as `GraphRows` counts them, newest
  first, no working row; without the prepare row 1's local label is not there):

  | Row | `web-app`: subject, time, author | Labels | `mobile-app`: subject, time, author | Labels |
  |---|---|---|---|---|
  | 0 | `Local change`, 12:26, Test Author | `local-work` | `Local change`, 12:06, Test Author | `local-work` |
  | 1 | `Try an idea`, 12:25, Test Author | `topic/no-request`, `origin/topic/no-request` | `Try an idea`, 12:05, Test Author | `topic/no-request`, `origin/topic/no-request` |
  | 2 | `Update the readme`, 12:24, Octo Cat | `origin/docs/readme` with `!22` | `Write the guide`, 12:04, Octo Cat | `origin/docs/guide` with `!12` |
  | 3 | `Add the search box`, 12:23, Ada Lovelace | `feature/search` with `!21`, `origin/feature/search` | `Add the sign-in page`, 12:03, Ada Lovelace | `feature/sign-in` with `!11`, `origin/feature/sign-in` |
  | 4 | `Add the docs folder`, 12:22, Test Author | `main` (HEAD), `origin/main`, `upstream/main` | `Add the docs folder`, 12:02, Test Author | `main` (HEAD), `origin/main` |
  | 5, 6 | `Add the footer` 12:21 (Ada Lovelace), `Initial commit` 12:20 (Octo Cat) | none | `Add the greeting` 12:01 (Ada Lovelace), `Initial commit` 12:00 (Octo Cat) | none |

  Badges: GitLab's `PullRequestBadge(21, "Add the search box", false, GitLab)` on row 3's first label and
  `(22, "Draft: Update the readme", true, GitLab)` on row 2's; Azure DevOps' `(11, "Add the sign-in page",
  false, AzureDevOps)` and `(12, "Write the guide", true, AzureDevOps)`; none on any other label, a
  remote label beside its local one included (one badge per request), nor on `upstream/main`.
- **`manifest/`** (D356, D366; 9.7): origin `ManifestUrl`, harbor-co's `Docks/manifest`, which no
  account of the scenario covers. With Fernhill-Labs signed in its hosting is `OtherOrganization`:
  `Repository` (Azure DevOps, `127.0.0.1:47397`, `harbor-co/Docks`, `manifest`), no list, failure or
  `ByRef`, `RequestRemotes` empty, nothing sent (D350); harbor-co's !5 is never listed. Its page
  `http://127.0.0.1:47397/harbor-co/Docks/_git/manifest/pullrequests` (D376).
- **Holds and faults** (3.4, 4.4). `served.GitLab.HoldNext("mrs:northfield/platform/web-app")` and
  `served.AzureDevOps.HoldNext("pulls:fernhill-labs/mobile app/mobile-app")` hold a list's answer.
  `served.GitLab.FailNext("mrs:northfield/platform/web-app", FakeGitLabFault.InsufficientGranular)`
  answers once, before the credential is read, 403 shape F, its `error` `insufficient_granular_scope`
  and its `error_description`
  `Access denied: This operation requires a fine-grained personal access token with the following project permissions: [Merge Request: Read].`,
  with no `WWW-Authenticate` (the project form READ, g2 G2.4), which GitLab's client words as `Forbidden`
  with that description as the message and the code as `Detail`.
  `served.AzureDevOps.FailNext("pulls:fernhill-labs/mobile app/mobile-app", FakeAzureDevOpsFault.ServerError)`
  answers once 500 with the message `TF400898: An Internal Error Occurred. Activity Id: {activity}.`
  (READ g1 G2; the status ASSUMED; `{activity}` the fake's made-up id), which Azure DevOps' client words
  as `ServerError` with that message and no `Detail`. Neither marks the account (only `BadCredentials`
  does, 9.6).
- **More than 300** (P6): `served.GitLab.AddMergeRequest(served.GitLab.Project(Scenarios.Hosting7bWebApp),
  $"More work {n}", "octo", "topic/no-request", "main")` for n 24 to 321 in order, which the fake numbers
  !24 to !321: 301 open. GitLab's ref sync writes their head refs in one `update-ref --stdin` and makes no
  commit (4.5), unlike Azure DevOps' merge refs (3.5), so the step costs one git process at the fake.

#### Test methods and app starts (part P)

Seven methods, seven app starts, each from `ServeAsync()` and a fresh data folder. Their git work is
light: `web-app`'s or `mobile-app`'s opening and a sign-in's refresh; method 5's 298 added merge
requests cost the fake one `update-ref`; methods 6 and 7 run a fetch and two checkouts, or one
checkout. Each stays well under 40 s alone, timed with `bash scripts/time-scripted-check.sh "<method>"
7b` before WP-P's merge (0.5). Part P takes 22 scripted pictures and 4 real-window captures (open point
32).

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_P1_P10_P12_P13_P14_P7_gitlab_rows_badge_menus_and_filter` | P1, P10, P12, P13 (GitLab), P14, P7 | `PrepareRequestsAsync(served.MobileApp, GitLab)`; one tab on `web-app` (`OpenAsync(data, served, WebApp, signIn: [])`), a new store; GitLab signed in through Settings, Accounts | 1100×700, dark | `p01a`, `p10a`, `p10b`, `p01b`, `p14a`, `p07a` |
| 2 | `Check_P1_P10_P12_P13_P14_azure_rows_badge_and_menus` | P1, P10, P12, P13, P14 (Azure DevOps) | `PrepareRequestsAsync(served.MobileApp, AzureDevOps)`; one tab on `mobile-app` (`OpenAsync(data, served, signIn: [])`), a new store; Azure DevOps signed in through Settings, Accounts, the organization prefilled from the tab | 1100×700, dark | `p01c`, `p10c` |
| 3 | `Check_P2_P10_P13_P4_P3_gitlab_signed_out_failed_and_loading` | P2, P10, P13, P4, P3 (GitLab) | one tab on `web-app`, signed out (`OpenAsync(data, served, WebApp, signIn: [])`), a new store; GitLab signed in from the section's row | 1100×700, dark | `p02a`, `p04a`, `p04b`, `p03a` |
| 4 | `Check_P2_P10_P13_P4_P3_P8_azure_signed_out_failed_loading_and_other_organization` | P2, P10, P13, P4, P3 (Azure DevOps), P8 | tabs `mobile-app` and `manifest` (`ShellDriver.StartWithTabs(data, [served.MobileApp.Path, served.Manifest.Path], activeTab: 0, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab)`), a new store, signed out; Azure DevOps signed in from `mobile-app`'s row | 1100×700, dark | `p02b`, `p04c`, `p08a`, `p08b`, `p10d` |
| 5 | `Check_P9_P6_P5_gitlab_listed_remote_more_than_300_and_none` | P9, P6, P5 | one tab on `web-app`, GitLab and GitHub signed in through the session (`OpenAsync(data, served, WebApp, signIn: [GitLab, GitHub])`) | 1100×700, dark | `p09a`, `p06a`, `p05a` |
| 6 | `Check_P11_gitlab_checkout_of_21_and_23` | P11 (GitLab) | one tab on `web-app`, GitLab signed in through the session (`OpenAsync(data, served, WebApp)`) | 1100×700, dark | `p11a` |
| 7 | `Check_P11_azure_checkout_of_11_and_13s_reason` | P11 (Azure DevOps) | one tab on `mobile-app`, Azure DevOps signed in through the session (`OpenAsync(data, served)`) | 1100×700, dark | `p11b` |

#### Texts these checks rely on (part P)

- **Design 12.2's texts**, word for word (`OperationTexts.Hosting.PullRequests.cs`, its provider forms,
  which return 7a's constants for GitHub; `{P}`, `{R}`, `{Rs}`, `{RT}`, `{RsT}` and `{N}` from 1.7's
  `HostingTexts`), GitLab's, then Azure DevOps' (GitHub's are the 7a report's part P texts):
  - the title `RequestsTitle`: `Merge requests`; `Pull requests` (7a's `LeftPanelViewModel.PullRequestsTitle`,
    which the placeholder keeps);
  - `SignInToSee`: `Sign in to GitLab to see merge requests…`; `Sign in to Azure DevOps to see pull requests…`;
  - `LoadingRequests`: `Loading merge requests…`; `Loading pull requests…` (7a's);
  - `NoOpenRequests`: `No open merge requests`; `No open pull requests` (7a's);
  - `MoreRequests`: `More than 300 open merge requests`; `More than 300 open pull requests` (7a's word
    for word);
  - `RequestHeader(p, n, title)`: `!21 Add the search box`; `!11 Add the sign-in page`;
  - `RequestOfBranch`: `Merge request !21: Add the search box`; `Pull request !11: Add the sign-in page`;
  - `FetchRequest(p, n, remote)`: `Fetch merge request !23`, running `Fetching merge request !23 from
    origin…`, failed `Merge request not fetched` and `Git could not fetch merge request !23 from origin.`;
    `FetchedRequest`: `Fetched !23 into fix-typo` (no Azure DevOps request is fetched, D361);
  - `OtherOrganization(repo, account)`: `In harbor-co; signed in to Fernhill-Labs`; its tooltip
    `This repository is in the Azure DevOps organization harbor-co; the signed-in account is in Fernhill-Labs, so nothing is asked of Azure DevOps for it. To see its pull requests, sign out and sign in to harbor-co in Settings, Accounts.`;
  - `MenuTexts`: `RefreshRequests` `Refresh merge requests`, `Refresh pull requests`;
    `OpenRequestsInBrowser` `Open merge requests in browser`, `Open pull requests in browser`;
    `CreateRequest` `Create merge request…` (WP-N's action; part P reads it in one menu, P9);
    `OpenRequest(p, n)` `Open merge request !21`, `Open pull request !11`; `RequestsLoading` `Merge
    requests are loading`, `Pull requests are loading`; `AddRemoteFor(p, fullName)` `Add a remote for
    Fernhill-Labs/Mobile App/mobile-app-fork to check out this pull request`; `HeadNotNamed(p)` (`Azure
    DevOps names no repository for this pull request's branch`) and `NotFetchedBy(remote, branch)`
    (`{remote} does not fetch {branch}`), which no part P check shows (12.7's classes do);
  - 7a's, for every provider: `OpenInBrowser` `Open in browser`, `CopyLink` `Copy link`, `PullRequestDraft`
    `Draft`, `FetchHeadChanged` (no check shows it); the sign-in item `HostingTexts.SignInToItemOf(p)`
    `Sign in to GitLab…`, `Sign in to Azure DevOps…` (7a's `Sign in to GitHub…`).
- **A row's tooltip** (7a's 10.3 with `{N}`): its header, a line break, `{author} · {head label} →
  {base ref}`, and ` · Draft` for a draft: `!23 Fix a typo in the footer` / `ada · ada:fix-typo → main`;
  `!22 Draft: Update the readme` / `octo · docs/readme → main · Draft`; `!21 Add the search box` /
  `ada · feature/search → main`; `!13 Fix a typo in the greeting` / `Ada Lovelace · mobile-app-fork:fix-typo → main`;
  `!12 Write the guide` / `Octo Cat · docs/guide → main · Draft`; `!11 Add the sign-in page` / `Ada
  Lovelace · feature/sign-in → main`. A head label is the branch for the base's own repository, else
  `BranchLabelOf`'s (12.3): GitLab `{Owner}:{branch}`, Azure DevOps `{Name}:{branch}` for a fork in the
  base's project. A failed row's tooltip is its text, a line break and the failure's `Detail` when it
  has one.
- **Check out** (12.5; 7a's `MenuTexts.CheckOutName` `Check out {label}`, `AlreadyCheckedOut` `{b} is
  already checked out`, `OperationTexts.CheckedOut` `Checked out {b}`): `Check out feature/search`,
  `Check out docs/readme`, `Check out ada:fix-typo`; `Check out feature/sign-in`, `Check out docs/guide`,
  `Check out mobile-app-fork:fix-typo` (disabled with `AddRemoteFor`).
- **The badge** (12.6): its text `{N}` (`!21`, `!22`, `!11`, `!12`); a label's tooltip line its name and
  ` ({draft }{R} {N}: {title})`: `feature/search (merge request !21: Add the search box)`,
  `origin/docs/readme (draft merge request !22: Draft: Update the readme)`, `feature/sign-in (pull request !11: Add the sign-in page)`,
  `origin/docs/guide (draft pull request !12: Write the guide)`; a row's labels one per line (7a's 11.2).
- **`HostingTexts.Describe`** (WP-B's 11.2) for P4's two failures: GitLab's `Forbidden`, `GitLab
  answered: Access denied: This operation requires a fine-grained personal access token with the
  following project permissions: [Merge Request: Read].` (its message's full stop dropped before the
  sentence's own; `Detail` `insufficient_granular_scope`); Azure DevOps' `ServerError`, `Azure DevOps had
  a problem. Try again later.` (`Detail` null).
- **Earlier texts**: the branch menus' other lines as phases 3 to 6 and 7a's P12 give them; the dialogs
  `Add GitLab account` and `Add Azure DevOps account` as part A's texts give them.
- **Log lines** (Debug included; `{id}` the request id of the answer named, in `PartB`'s form for each
  fake): D322's `web-app: listed {n} open pull requests of northfield/platform/web-app` and
  `mobile-app: listed {n} open pull requests of Fernhill-Labs/Mobile App/mobile-app`, none for
  `manifest`; the failed lists' Warnings, the HTTP core's (5.2) `GitLab: GET
  /api/v4/projects/northfield%2Fplatform%2Fweb-app/merge_requests failed: 403 (request {id}).` and `Azure
  DevOps: GET /Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests failed: 500
  (request {id}).`, and the session's (9.6's `CallAsync`) `GitLab: listing the pull requests of
  northfield/platform/web-app failed: Forbidden, Access denied: This operation requires a fine-grained
  personal access token with the following project permissions: [Merge Request: Read]. (request {id})`
  and `Azure DevOps: listing the pull requests of Fernhill-Labs/Mobile App/mobile-app failed:
  ServerError, TF400898: An Internal Error Occurred. Activity Id: {activity}. (request {id})`
  (`{activity}` the fake's made-up id); never `web-app: showing GitHub avatars` (D367).

### Part N: a new pull or merge request

Part N covers a new request for the listed repository's provider (design 13; D351, D355, D362; rulings
1, 3, 20, 21): `Create merge request…` on GitLab's `web-app` and `Create pull request…` on Azure DevOps'
`mobile-app`, from a local branch's, a remote branch's and a drop's menu (12.4, WP-P's items); each
provider's form (13.4) with GitLab's sent title and its quick-actions note; `local-work` pushed first
through the app's helper; the refusals "already open" and a missing branch in the App's words (13.5);
the created request listed with its badge, `Created … !n` and the status bar's `Open in browser`
(D317); long repository texts cut with their tooltips (ruling 3); signed out, that provider's sign-in
first (10.5, 13.3); and the form at 1920×1080 and in light once: checks N1 to N9, design 13.8's list,
numbered here in its order (open point 34). GitHub's form, texts, menus and pictures stay 7a's (13.1;
D351, D362): 7a's part N checks stay in 7a's walk-through, where the new providers are off, and prove
it, their pictures the same files as `phase-7a-pictures.sha256` lists ("Changes to expected results").
Every method first calls `Phase7bExpectations.PrepareRequestsAsync(copy, provider)` before `OpenAsync`
(13.8), so every picture is taken in the prepared state that part P's R methods and WP-R's "Requests
and create" launches share (2.7, 12.8, 14.2). Its R pictures are N2's `n02a-gitlab-form` and
`n02b-azure-form` and N6's `n06a-gitlab-created` and `n06b-azure-created`, steps 3 and 4 of 14.4's
"Requests and create" per provider: the prepared `topic/no-request`'s form, and the status after
creating from it, which pushes nothing (open point 35). Part N uses WP-P's menu items, section, branch
rows and badge as part P's checks and design 12 give them (WP-P was not merged when part N was written:
nothing is taken from its code), WP-A's dialogs and WP-S's `RepositoryHosting` as built, and 7a's create
(`RepositoryOperations.CreatePullRequest.cs`, `CreatePullRequestDialog*`) as 13.3 to 13.6 change it. No
part N step opens the browser (ruling 29): the offer's and `Open … !n`'s clicks reach `FakeFileLauncher`.

#### Conventions of part N

- **Code** (13.1, 13.7, 13.8): `Phase7bChecks.N.cs` (`Phase7bChecksN`, one method per app start,
  below) and `Phase7bChecks.N.Helpers.cs` (the builder's to name: copied from `Phase7aChecks.N.Helpers.cs`
  with 7b's names, naming their source: a branch row's menu by its path, the form opened from a menu
  item and its snapshot, waits for `OperationEnded` and for `PullRequestsListed` n times since a mark,
  the POST's parsed body, the queue's records since a mark, git in the copy's bare folders through
  `GitRunner` with `TempRepo.IsolatedEnvironment`); `Phase7bExpectations.N.cs` (nested `PartN`: 13.2's
  texts per provider by hand and 7a's that part N keeps, the menus' lines per provider, the heads,
  bases and titles, the new numbers, the create's routes and bodies, the log lines, the picture names,
  the R pictures and the places below, written from this report and not from the App's texts; for
  WP-R one record per provider, `PartN.GitLab` and `PartN.AzureDevOps`, holding the repository
  (`web-app`, `mobile-app`), the branch row's path (`Local branches`, `topic`, `no-request`), the menu
  item (`Create merge request…`, `Create pull request…`), the form's title (`Create merge request`,
  `Create pull request`), its head (`topic/no-request`), base (`origin/main`), their repository text
  (`northfield/platform/web-app`, `Fernhill-Labs/Mobile App/mobile-app`) and its title (`Try an idea`),
  the create's route key (`create:northfield/platform/web-app`, `create:fernhill-labs/mobile app/mobile-app`)
  with its status (201) and its body in its provider's own form (N6): GitLab's `source_branch`
  `topic/no-request`, `target_branch` `main`, `title` `Try an idea` (no `Draft: ` prefix) and
  `description` empty, with no `draft`, `target_project_id` or `allow_collaboration` member (the draft
  state is the title's prefix on GitLab, 7.4); Azure DevOps' `sourceRefName` `refs/heads/topic/no-request`,
  `targetRefName` `refs/heads/main`, `title` `Try an idea`, `description` empty and `isDraft` false, with
  no `forkSource`; whether the head is pushed first (false for both: no receive-pack), the operation's
  title (`Create merge request for topic/no-request`, `Create pull request for topic/no-request`), the
  status text (`Created merge
  request !24`, `Created pull request !14`), the new row's name first in `PullRequestRowNames()` (`!24
  Try an idea`, `!14 Try an idea`), the count listed after the create (4) and the two R pictures'
  names; 13.8); `Phase7bExpectationTests.N.cs` (`CreatePullRequestTexts`' provider forms and
  `MenuTexts.CreateRequest(p)` equal `PartN`'s for GitLab and Azure DevOps and 7a's constants word for
  word for GitHub; `PartN.GitLab` and `PartN.AzureDevOps` equal the rows below; the picture names
  unique and matching 0.3's rule). 13.7's classes are WP-N's own. Parts A's, B's and P's helpers
  (`OpenAccountsAsync`, `OpenSignInDialogAsync(p)`, `TypeToken`, `WaitForAccountAsync(p, …)`, the
  section's and the branch rows' helpers) and phase 3's menu helpers (`AssertMenu`,
  `AssertHighlighted`, `AssertMenuShape`) are used as they are.
- **Names in the checks** are parts A's, B's and P's and 7a's part N's. "The form" is the open
  `CreatePullRequestDialogViewModel` and its view `CreatePullRequestDialog` (7a's ids, D362), its
  controls by 13.4's and 7a's ids; "the offer" `OperationOfferButton`; "the operation" the create's
  entry in the tab's queue, ended when `AppLogFile.OperationEnded("{repo}", "Create {R} for {head}", …)`
  has come since the step's mark. "The POST" is the request of route `create:northfield/platform/web-app`
  (GitLab's `POST /api/v4/projects/5001/merge_requests`) or `create:fernhill-labs/mobile app/mobile-app`
  (Azure DevOps' `POST /Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests`, query
  `api-version=7.1`), its `Body` parsed as JSON; "the fake's !24" the record of `Iid` 24 in
  `served.GitLab.MergeRequests(served.GitLab.Project(Scenarios.Hosting7bWebApp))`, "the fake's !14" that
  of `Id` 14 in `served.AzureDevOps.PullRequests(served.AzureDevOps.Repository(Organization, Project,
  "mobile-app"))`; "the bare folder" the copy's `gitlab/northfield/platform/web-app.git` or
  `azure-devops/Fernhill-Labs/Mobile App/_git/mobile-app`. "Git requests" are that fake's `GitRequests`
  since the step's mark.
- **The form** (13.4; 7a's part N): 7a's layout, ids, classes and order, named by its title (`Create
  merge request`, `Create pull request`), under its backdrop (D104), no `DialogRepository` (one tab).
  Asserted from the bounds (±1): the card 460 wide at x 320 to 780 (1100×700) and 730 to 1190
  (1920×1080), centred vertically; `From` and `Into` at x 340, their values from x 388; each From and
  Into row's children where 7a's `StackPanel` put them, each 8 right of the one before by its text's
  width (13.4's `DockPanel`s, sized to their content), all inside the 370-px column (x 388 to 758): a
  repository text that would pass it is cut with "…", its tooltip its whole text, and `Choose…` stays
  whole at its row's right. **The cut rule**, asserted from the controls at run time (D122's rule) and
  never from a constant: a row's repository text is one collapsed line (`HasCollapsed`) exactly when its
  natural width (its text laid out in the control's font with no width limit) passes the room its row
  leaves it, the column's 370 less `PullRequestHead`'s or `PullRequestBase`'s width, `Choose…`'s width on
  the Into row and the text's margins (8 on From, 8 and 8 on Into, 13.4), all from the controls' bounds; a
  natural width within 1 of the room is not asserted either way; while the Into text is cut, `Choose…`'s
  right edge is the column's right (x 758, ±1: 7a's 388 plus 370, where `FormLayout` gives 759), and
  while it is whole `Choose…` stands 8 right of it; `PullRequestTitle` x 340 to 760, 32 high;
  `PullRequestDescription` 120 high; the buttons right-aligned to x 760. Azure DevOps' form lays out as
  7a's: the places of `Phase7aExpectations.PartN.FormLayout` relative to the card (copied into `PartN`,
  since no 7b file references 7a's project: "`FormLayout`'s places" below), the card 386 high, but **the
  rows' children** (`PullRequestHead`, `PullRequestHeadRepository`, `PullRequestBase`,
  `PullRequestBaseRepository`, `PullRequestBaseChooseButton`): their y and height `FormLayout`'s, their x
  by the rule above (8 right of the one before) and each width its own (a text its natural width, or the
  cut rule's room while cut; `Choose…` 82, as 7a's). `FormLayout`'s x and widths for them are 7a's texts'
  (`topic/no-pr`, `octo/hello`) and are never asserted for 7b: `PartN`'s copy says so beside them. GitLab's adds `PullRequestQuickActionsNote` (`settingsNote`, 4 below
  `PullRequestDescription`, x 340, one line) always, and `PullRequestSentTitle` (`settingsNote`, 4 below
  `PullRequestDraft`) while Draft is ticked: everything above a shown line at Azure DevOps' places, and
  `PullRequestDraft`, the error line, the buttons and the card's bottom lower by each shown line's height
  plus 4 (±1; no figure is given, open point 41). `PullRequestHeadNote` (`settingsNote`, under the head,
  wrapping) as 7a's. Theme brushes only: dark, the card `#1A1D24`, the secondary texts and the notes
  `#9BA3B0`.
- **Creating** (13.5; 7a's 12.4, 12.5): every control of the form disabled, `Create` reads `Creating…`,
  Esc ignored; `Cancel` disabled but while Create pushes (7a's ruling 59); the status bar the step's
  running text (the push's `Pushing local-work to origin/local-work…`, then `Creating merge request…` or
  `Creating pull request…`), the progress bar indeterminate, `OperationCancelButton` (`Stop git`) shown
  only while Create pushes (D315). A failure is shown on `PullRequestError`, never in a dialog (D312),
  the controls enabled again; the status bar then reads `{Title} failed` (`VcDangerBrush`).
- **Around the POST** (13.5; 7a's 12.5 step 4; D289, D293). On GitLab, after any push and before the
  POST, the provider reads the source project (7.4's A5): one `GET /api/v4/projects/northfield%2Fplatform%2Fweb-app`
  (route `project:northfield/platform/web-app`), sent with the `ETag` the HTTP core kept from the list's
  A3 read of that URL and answered 304 (5.2, 4.4; open point 36); Azure DevOps sends nothing before its
  POST within one repository (6.6). After a create, two reads of the list, D322's line twice: the
  create's own refresh, and the one after a succeeded network operation. GitLab's are
  `mrs:northfield/platform/web-app` with the query `state=opened&per_page=100`, the first 200 with the new
  list and the second 304 (the weak `ETag` over an unchanged body, 4.4), the fork `ada/web-app` kept (no
  `project:ada/web-app`); Azure DevOps' are `pulls:fernhill-labs/mobile app/mobile-app` with the query
  `searchCriteria.status=active&api-version=7.1&$top=100&$skip=0`, both 200 (its fake sends no `ETag`,
  3.4), A3 kept (no `repo:`; open point 43). Every request `Bearer` with `Login` `ada`, or
  `BasicEmptyUser` with `Login` Ada's. "Already open" reads the list once more (D316); no other refusal
  reads anything (D289). A step that pictures a create waits for the operation's end, both list lines
  since its mark and quiet watchers.
- **Waiting** (D322; 0.5): every picture of a signed-in tab waits for its step's `{repo}: listed {n}
  open pull requests of {full name}` lines since its mark and for quiet watchers (no avatars line,
  D349); a signed-out picture waits for the hosting's `SignedOut`, the section's message row and quiet
  watchers (7a design 17 item 3).
- **Signing in** (14.4). Methods 1 and 2, the R twins, sign in through Settings, Accounts, as WP-R's
  "Requests and create" launches and part P's methods 1 and 2 do (`OpenAccountsAsync()`,
  `OpenSignInDialogAsync(p)`, Azure DevOps' organization prefilled from the tab, `TypeToken(token)`,
  Enter, a wait until signed in, Esc, the screen's `IsClosed`). Methods 3 and 4 start signed out and
  sign in through the dialog a create opens (N8). Methods 5 to 7 sign in through the session
  (`OpenAsync`'s default `signIn`), a state their steps start from.
- **The prepared state** (2.7, 13.8): every method calls `PrepareRequestsAsync(served.MobileApp, provider)`
  after `ServeAsync()` and before `OpenAsync` (GitLab: `web-app/`; Azure DevOps: `mobile-app/`), so
  `Local branches` holds the closed folder `topic` with `no-request`, tracking `origin/topic/no-request`
  and level with it, and the graph its local label on row 1 (part P's scenario facts' table); the R
  pictures are taken in that state.
- **Menus** are phase 3's and part P's (header first, the first enabled item highlighted,
  `MenuHeight(items, separators)` high ±2, the top-left 2 right of and below the pointer); asking for
  one asks no fake anything. A branch menu's request line is WP-P's (12.4), where 7a's `Create pull
  request…` stood: last in its push group on a local branch, in its own group after the delete group on
  a remote branch, in its own group after the rebase group in a drop's; `Open {R} {N}` in its place once
  a request is listed open from that branch.
- **Pictures.** At 1100×700 the neutral point (760, 687) is the status bar's empty middle right of the
  offer, or the backdrop under the form or a sign-in dialog; at 1920×1080 the form covers it and N9
  rests the pointer at (760, 1000), the backdrop under the card (7a's open point 56).
- **No token on screen** (as part A's): before every `Shot`, no visible text, automation value or
  automation name in the window holds `GitLabToken`, `AzureDevOpsToken` or `GitHubToken`.
- **Git's version** (8.6; part B's): from 2.46 a bound URL's first git request carries the account's
  token; on 2.31 to 2.45 the first goes without credentials (401), and on Azure DevOps' `Fernhill-Labs@`
  URL a second carries `Fernhill-Labs` with an empty password (401), before the app's helper answers
  with `x-access-token` (B7's order, "Changed in this report after its first commit"). N4's expected git
  requests hold for both.
- **Every method ends**, after closing the app, with `TokenTripwire.AssertAbsent` for each token the
  method used or typed (methods 1, 3, 5 and 7 `GitLabToken`; 2, 4 and 6 `AzureDevOpsToken`) over the
  data folder and the copy's `mobile-app/`, `manifest/` and `web-app/`, `.git` included, with the
  session's call log (the push's command, output and error included); each fake's `Errors` and the
  harness's `RefusedRequests` empty; no request answered 400 or 415 but the POST N5 names;
  `Launcher.Requests` exactly the launches its steps name (methods 1, 2, 5 and 6: one each) and
  `Clipboard.Texts` empty. Every picture passes `LayoutAudit.FindClippedText`: a repository text, a
  head, a base, a title or a label cut with "…" whose whole text a tooltip holds is allowed.

#### Scenario facts part N uses (2.6, 2.7, 3.3 to 3.5, 4.3 to 4.5, 6.6, 7.4, 13.3; the fakes as WP-FA, WP-FL and WP-LF left them)

- **The prepared clones** (2.6, 2.7). `web-app/`: `main` (HEAD, tracking and level with `origin/main`,
  12:22 "Add the docs folder"), `feature/search` (tracking, `!21`'s head), the prepared
  `topic/no-request` (tracking and level with `origin/topic/no-request`, 12:25 "Try an idea" by Test
  Author, one commit on `main`) and `local-work` (12:26 "Local change" by Test Author, one commit on
  `main`, no upstream, in no bare folder); remotes `origin` (GitLab's `northfield/platform/web-app`) and
  `upstream` (GitHub's `octo/web-app`). `mobile-app/`: `main` (HEAD, level), `feature/sign-in`
  (tracking, `!11`'s head), the prepared `topic/no-request` (12:05 "Try an idea") and `local-work` (12:06
  "Local change", no upstream, in no bare folder); remote `origin` alone. Part P's scenario facts give
  the graphs' rows, labels and badges and the lists (`!23`, `!22`, `!21`; `!13`, `!12`, `!11`).
- **Where a request can go** (13.3; 1.5's `RequestRemotes`): `origin` alone on both (`web-app`'s
  `upstream` is GitHub's, no key, P9), signed out too (the hosting has a `Repository`, ruling 24). A3,
  kept from the list's read (7.8): `web-app` not a fork, `default_branch` `main`; `mobile-app` not a
  fork, `defaultBranch` `refs/heads/main`. So every default Into is `origin/main` with the listed
  repository's full name (`northfield/platform/web-app`, `Fernhill-Labs/Mobile App/mobile-app`), and the
  Pick's options are the remote branches of `RequestRemotes`' remotes in the refs' order, each with its
  repository's full name as its detail (D318).
- **The head's place** (13.3; D313): `topic/no-request`, level with an upstream of the same name on
  `origin`, is not pushed; `local-work`, without an upstream, is pushed first to `origin` under its own
  name with `--set-upstream` (target `origin/local-work`); `origin/topic/no-request`, a remote branch,
  never.
- **The new numbers.** No request is open from `topic/no-request` or `local-work` on either fake. GitLab
  numbers web-app's next `!24` (the `iid` next in the target, after its 23; 4.3 (h)), Azure DevOps
  Fernhill-Labs' next `!14` (one sequence per organization, after `!13`; harbor-co's `!5` is another's;
  3.2). A refused create numbers nothing. web-app's project number is 5001 (`ada/web-app` 5002, the long
  project 5003): the POST goes to `/api/v4/projects/5001/merge_requests`, whose route key the fake
  names by the project's path.
- **GitLab's create** (7.4's A5, 4.3; MEASURED 2026-11-06 where marked, `FakeGitLab.Router.cs`' header):
  the source's `GET projects/<its escaped path>`, then one `POST projects/5001/merge_requests` with a JSON
  body of `source_branch`, `target_branch`, `title` (`GitLabProvider.TitleFor(title, draft)`: `Draft: `
  before a title without one of the three prefixes while Draft is ticked, the title unchanged
  otherwise) and `description` as typed; within one project no `target_project_id` and no
  `allow_collaboration`; never `draft` (r6 5.1). The fake checks, in order: a missing parameter (400);
  the source's access; the same project and branch (422); an open request with the same projects and
  branches, 409 `{"message":["Another open merge request already exists for this source branch: !<iid>"]}`
  (MEASURED); a missing branch, 400 `{"message":{"source_branch":["does not exist"]}}` (MEASURED;
  `target_branch` likewise); else 201 with the request, `draft` from its title (MEASURED for `Draft:`),
  `web_url` `http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests/<iid>`, and its head ref
  `refs/merge-requests/<iid>/head` written in the bare folder at the source's tip. Its client words the
  409 as `Validation` with the refusal `AlreadyExists` and the 400 by its keys, `HeadMissing` for
  `source_branch` alone (`BaseMissing`, `BranchesMissing` for the others), each with GitLab's words as
  its message: `Another open merge request already exists for this source branch: !24`, `source_branch
  does not exist`. The fake runs no quick action (D370).
- **Azure DevOps' create** (6.6's A5, 3.3; MEASURED 2026-11-05 where marked, `FakeAzureDevOps.Router.cs`'
  header): one `POST /Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests?api-version=7.1`
  with a JSON body of `sourceRefName` and `targetRefName` as `refs/heads/…`, `title`, `description` and
  `isDraft`; within one repository no `forkSource`. The fake checks, in order: the title (400
  `InvalidArgumentValueException`); both refs in the repository after a ref sync, else 400
  `GitPullRequestCannotBeActivated`, `TF401398: The pull request cannot be activated because the source
  and/or the target branch no longer exists, or the requested refs are not branches` (MEASURED); an
  active one with that source and target, 409 `GitPullRequestExistsException`, `TF401179: An active pull
  request for the source and target branch already exists.` (MEASURED); else `CreateStatus`, 201
  (MEASURED), `status` `active`, `isDraft` as sent, `createdBy` the token's user Ada, `mergeStatus`
  `queued` (READ), then `succeeded` and `refs/pull/<id>/merge` at the next ref sync, never a head ref
  (D361). Its client words the 409 as `Validation` with `AlreadyExists` and the 400 as `Validation` with
  `BranchesMissing` (TF401398 names neither branch, 13.5), the message the body's. The provider builds
  the web address `http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/<id>`
  (6.6).
- **The push** (N4; 8.2, 3.5, 4.5): ada (a Developer of `northfield`, Code: Push in the default
  fine-grained token) may push to `northfield/platform/web-app`, Ada (Code (Read & write)) to
  `mobile-app`. Git's paths: `/northfield/platform/web-app.git/info/refs?service=git-receive-pack` and
  `…/git-receive-pack`; `/Fernhill-Labs/Mobile%20App/_git/mobile-app/info/refs?service=git-receive-pack`
  and `…/git-receive-pack`.
- **States the checks make.** `served.GitLab.HoldNext("create:northfield/platform/web-app")` holds the
  POST (N4). The missing branch (N5): `git update-ref -d refs/heads/topic/no-request` in the bare folder
  after the list was read, restored with `git update-ref refs/heads/topic/no-request <its SHA>`; the
  clone's refs stay (nothing is fetched). The open request (N5), added on the fake after the list was
  read, as 7a's N4: `served.GitLab.AddMergeRequest(served.GitLab.Project(Scenarios.Hosting7bWebApp), "Try
  an idea", "octo", "topic/no-request", "main")` (`!24`, author `octo`) and
  `served.AzureDevOps.AddPullRequest(served.AzureDevOps.Repository(Organization, Project, "mobile-app"),
  "Try an idea", served.AzureDevOps.User(Organization, OctoName), "topic/no-request", "main")` (`!14`, Octo
  Cat). The long names (N7): in `mobile-app/`, `git remote add long` with the URL
  `http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/customer-onboarding-and-identity-verification-service`
  (through `WithPorts`; 2.6's empty repository), then `git update-ref refs/remotes/long/main` at
  `origin/main`'s SHA and `git update-ref refs/remotes/long/topic/no-request` at
  `origin/topic/no-request`'s: two remote branches of a remote that `RequestRemotes` holds (its
  organization and project are mobile-app's, D350), whose repository's full name
  `Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service` (78 characters) is
  wider than either row leaves it; nothing is fetched from it or created there (open point 39). No fault
  is set: every refusal is the fake's own answer to the state.

#### Test methods and app starts (part N)

Seven methods, seven app starts, each from `ServeAsync()`, `PrepareRequestsAsync(served.MobileApp,
provider)` and a fresh data folder. Their git work: the opening and a sign-in's refresh; methods 1, 2
and 5 to 7 push nothing; methods 3 and 4 push `local-work` once, after the push guard's read; methods 5
and 6 delete and restore one ref in a bare folder, and method 6 writes a remote and two refs in
`mobile-app/`; the fakes' ref syncs add one `update-ref` (GitLab) or one merge commit per open request
(Azure DevOps, at most four). Each stays under 40 s alone, timed with `bash scripts/time-scripted-check.sh
"<method>" 7b` before WP-N's merge (0.5); methods 3 and 4, the heaviest (a sign-in, a push and a create with
two list reads; method 3 opens three forms, method 4 one), are the ones to split by D237's rule should one pass it. Part N
takes 22 scripted pictures and 4 real-window captures, and its seven methods bring the phase to 29,
past 0.5's about 22 to 27 (open point 44).

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_N1_N2_N6_gitlab_branch_menu_form_and_created` | N1 (GitLab's local branch), N2 (GitLab), N6 (GitLab) | `PrepareRequestsAsync(served.MobileApp, GitLab)`; one tab on `web-app` (`OpenAsync(data, served, WebApp, signIn: [])`), a new store; GitLab signed in through Settings, Accounts | 1100×700, dark | `n02a`, `n06a` |
| 2 | `Check_N1_N2_N6_azure_branch_menu_form_and_created` | N1 (Azure DevOps' local branch), N2 (Azure DevOps), N6 (Azure DevOps) | `PrepareRequestsAsync(served.MobileApp, AzureDevOps)`; one tab on `mobile-app` (`OpenAsync(data, served, signIn: [])`), a new store; Azure DevOps signed in through Settings, Accounts, the organization prefilled from the tab | 1100×700, dark | `n02b`, `n06b` |
| 3 | `Check_N8_N1_N4_N3_gitlab_signed_out_drop_pushed_first_and_draft` | N8 (GitLab), N1 (GitLab's remote branch, `local-work` and the drop), N4 (GitLab), N3 (GitLab) | prepared for GitLab; one tab on `web-app`, signed out (`OpenAsync(data, served, WebApp, signIn: [])`), a new store; GitLab signed in through the dialog a create opens | 1100×700, dark | `n08a`, `n01a`, `n01b`, `n04a`, `n03a`, `n04b`, `n03b` |
| 4 | `Check_N8_N1_N4_N3_azure_signed_out_drop_pushed_first_and_draft` | N8 (Azure DevOps), N1 (Azure DevOps' remote branch, `local-work` and the drop), N4 (Azure DevOps), N3 (Azure DevOps) | prepared for Azure DevOps; one tab on `mobile-app`, signed out (`OpenAsync(data, served, signIn: [])`), a new store; Azure DevOps signed in through the dialog a create opens, its organization prefilled | 1100×700, dark | `n08b`, `n04c`, `n03c` |
| 5 | `Check_N5_gitlab_missing_branch_and_already_open` | N5 (GitLab) | prepared for GitLab; one tab on `web-app`, GitLab signed in through the session (`OpenAsync(data, served, WebApp)`); in the steps, the bare folder's branch deleted and restored and an open request added on the fake | 1100×700, dark | `n05a`, `n05b` |
| 6 | `Check_N5_N7_azure_missing_branch_already_open_and_long_names` | N5 (Azure DevOps), N7 | prepared for Azure DevOps; one tab on `mobile-app`, Azure DevOps signed in through the session (`OpenAsync(data, served)`); in the steps, as method 5, then the remote `long` and its two refs | 1100×700, dark | `n05c`, `n05d`, `n07a`, `n07b` |
| 7 | `Check_N9_gitlab_form_at_1920x1080_and_light` | N9 | prepared for GitLab; one tab on `web-app`, GitLab signed in through the session (`OpenAsync(data, served, WebApp, width: 1920, height: 1080)`) | 1920×1080, dark, then light by the toolbar's Theme switch | `n09a`, `n09b` |

#### Texts these checks rely on (part N)

- **Design 13.2's texts**, word for word (`OperationTexts.CreatePullRequestTexts`, its provider forms,
  which return 7a's constants for GitHub; `{P}`, `{R}`, `{RT}` and `{N}` from 1.7's `HostingTexts`),
  GitLab's, then Azure DevOps' (GitHub's are 7a's part N texts):
  - `DialogTitleOf`: `Create merge request`; `Create pull request` (7a's `DialogTitle`);
  - `ReadingOf`: `Reading GitLab…`; `Reading Azure DevOps…` (no part N check shows them: A3 is kept
    from the list's read);
  - `CreatingRequest`: `Creating merge request…`; `Creating pull request…` (7a's `CreatingPullRequest`);
  - `Operation(p, head)`: the title `Create merge request for {head}`, `Create pull request for {head}`;
    the running text the `CreatingRequest` above; the error title `Create merge request failed`, `Create
    pull request failed`, and summary `GitLab could not create the merge request.`, `Azure DevOps could
    not create the pull request.` (pinned only: the form shows no error dialog, D312);
  - `Created(p, n, draft)`: `Created merge request !24`, `Created draft merge request !24`; `Created pull
    request !14`, `Created draft pull request !14`;
  - `OpenExisting(p, n)`: `Open merge request !24`; `Open pull request !14`;
  - `AlreadyOpen(p, n, head, base)`: `Merge request !24 is already open from topic/no-request into origin/main.`;
    `Pull request !14 is already open from topic/no-request into origin/main.`;
  - `PushedFirstWithUpstream(p, target)`: `Not on GitLab yet: Create pushes it to origin/local-work first and sets that as its upstream.`;
    `Not on Azure DevOps yet: Create pushes it to origin/local-work first and sets that as its upstream.`
    (`PushedFirst` and `AheadPushedFirst` likewise, which no part N check shows);
  - `HeadUnknown(p, branch, repo)`: `GitLab has no branch topic/no-request in northfield/platform/web-app to merge from. Fetch, then try again.`
    (open point 37);
  - `BranchesUnknown(p, head, base)`: `Azure DevOps cannot find topic/no-request or main. Fetch, then try again.`
    (open point 37);
  - `SentTitle(p, title)`: `Title on GitLab: Draft: Local change`; `QuickActionsNote`: `GitLab runs
    quick actions in the description, such as /close.` (GitLab's alone);
  - `BaseUnknown`, `NotVisible`, `Cancelled` (`Cancelled before the merge request was created.`,
    `Cancelled before the pull request was created.`) and `Refused` (`GitLab refused to create a merge
    request in northfield/platform/web-app with this token.`, with `Detail` ` It needs the api scope, or
    Merge Request: Create.`; Azure DevOps' without one), which no part N check shows (13.7's classes do).
- **7a's texts part N keeps** (13.2; 7a's 12.6), for every provider: `From`, `Into`; the placeholders
  `Title` and `Description`; `Draft`; `Create`, `Creating…`; `Choose…` (`SettingsTexts.Choose`), the
  Pick's title `Choose the branch to merge into` and its `Choose` and `Cancel`; `Cancel`; the offer's
  `Open in browser`; `Stop git`; the push's running text `Pushing local-work to origin/local-work…`
  (`OperationTexts.Push`); `{Title} failed`; phase 3's `Checked out {b}` (`OperationTexts.CheckedOut`).
- **The menus' items** (12.2's `MenuTexts`, WP-P's): `CreateRequest` `Create merge request…`, `Create pull
  request…`; `OpenRequest(p, n)` `Open merge request !24`, `Open pull request !14`; the section's
  `Refresh merge requests` and `Open merge requests in browser`, `Refresh pull requests` and `Open pull
  requests in browser`; the branch and drop menus' other lines as 7a's P12 and N1, N2 and N5 give them,
  with 7b's names (N1's rows).
- **The section and the branch rows** (part P's texts): a row's name `{N} {title}` and its tooltip, the
  name, a line break, `{author} · {head label} → main` and ` · Draft` for a draft; a branch row's `!n`
  with the tooltip `Merge request !24: Try an idea` or `Pull request !14: Try an idea` (`RequestOfBranch`);
  the graph's `PullRequestBadge(Number, Title, IsDraft, Provider)` drawn `!n`.
- **Earlier texts**: the dialogs `Add GitLab account` and `Add Azure DevOps account` as part A's texts
  give them (N8); the section's message rows `Sign in to GitLab to see merge requests…` and `Sign in to
  Azure DevOps to see pull requests…` (part P's).
- **Log lines** (Debug included; `{id}` the request id of the answer named, in part B's form for each
  fake): D322's `web-app: listed {n} open pull requests of northfield/platform/web-app` and `mobile-app:
  listed {n} open pull requests of Fernhill-Labs/Mobile App/mobile-app`; the operation's end, phase 3's
  `{repo}: {Title}: {Outcome} after {n} ms: {message}`; WP-N's Info lines, 7a's with `{R}` and `{N}`
  (13.5, 7a design 17 item 13), never the title or the description: `web-app: Create merge request for
  topic/no-request: into northfield/platform/web-app main, draft no, no push`, with `draft yes, push to
  origin/local-work first` for N3's `local-work`, and `web-app: Create merge request for
  topic/no-request: created !24 in northfield/platform/web-app.` (Azure DevOps' with `mobile-app`, `pull
  request`, `Fernhill-Labs/Mobile App/mobile-app` and `!14`); a refused POST's Warnings, the HTTP core's
  (5.2) `GitLab: POST /api/v4/projects/5001/merge_requests failed: {status} (request {id}).` and `Azure
  DevOps: POST /Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests failed: {status}
  (request {id}).`, the session's (`RepositoryHosting.CreatePullRequestAsync` through 9.6's `CallAsync`,
  whose words say "pull request" for every provider, as its list's do, 9.9) `GitLab: creating a pull
  request on northfield/platform/web-app failed: Validation, {message} (request {id})` and `Azure DevOps:
  creating a pull request on Fernhill-Labs/Mobile App/mobile-app failed: Validation, {message} (request
  {id})`, and the App's (7a's) `{repo}: {Title}: the request to create it failed: Validation, {message}
  (request {id})`, `{message}` the client's (the scenario facts). Never `web-app: showing GitHub avatars`
  (D367).

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). Each check is named by its part's letter
and its place in its part's design list (10.7 for part A, 11.7 for part B, 12.8 for part P, 13.8 for part
N), numbered here in its order (open points 1, 11, 22 and 34); its scenario, theme and window size end its "Check" column; its methods and starts
are in its part's table above. "R" in Notes marks the steps the real-window pass repeats (design 10.7,
11.7, 12.8, 13.8, 14.4), "n/a" says why a check is not repeated.
"Screenshots" lists the planned pictures, the scripted run's and then the real window's. Every step's
"requests" are those the three fakes recorded since the step's mark; every "calls" are the store's
`Calls` since its mark; "git calls" are the call log's, since a quiet mark (part A's "Names in the
checks"). A step numbered in a method runs on from that method's last step in an earlier row.

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| A1 | The intro and three blocks signed out (D353, D362; design 10.2, 10.3, 10.9, 0.7): one intro for the three providers, as high as 7a's sentence in the same width; the blocks GitHub, GitLab and Azure DevOps, 24 apart, each 7a's signed-out block under its provider's name; opening the section asks no fake and no keychain and adds no git call to the screen's own read; "hosting7b" served, one empty tab, the three fakes, dark, 1100×700 | **Method 1** (`Check_A1_A2_signed_out_to_three_signed_in`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)` (one empty tab, the three fakes, a new `FakeTokenStore`); wait for the welcome page. Mark the requests and the calls; take the quiet call mark (`QuietCallMarkAsync()`). 2. `OpenAccountsAsync()`. Measure the intro (part A's conventions, "The intro"). Screenshot (`a01a-three-signed-out`). | **After step 1:** the welcome page; the session's `IsOff` false, `Providers` exactly GitHub, GitLab, Azure DevOps in that order, `IsOn` true for each, `AccountOf` null for each; the app's log holds, in this order, `GitHub: 127.0.0.1:47399.`, `GitLab: 127.0.0.1:47398.`, `Azure DevOps: 127.0.0.1:47397.`, then `GitHub: no account on 127.0.0.1:47399.`, `GitLab: no account on 127.0.0.1:47398.`, `Azure DevOps: no account on 127.0.0.1:47397.` (through `WithPorts`); requests none (D257, D350); calls none; no `accounts.json`.<br>**After step 2:** the screen `SettingsDialog` on the dialog layer over the welcome page, the card exactly at (120, 40), 860×620, its five nav buttons as 7a's A1, `SettingsNavAccounts` at (140, 223) to (316, 253) in the `selected` look with the keyboard focus; `SelectedSection` `Accounts`. From x 336, top to bottom: `AccountsIntro` with 10.2's sentence (secondary, wrapped, whole), in exactly two lines and as high (±0.5) as 7a's sentence measured at its width; 18 below, `AccountsGitHub`: the label `GitHub` (x 336 to 468) and from x 480 `AccountsSignedOut` `Not signed in.`, 10 below it `AccountsSignInButton` `Sign in…`, enabled; 24 below that block, `AccountsGitLab`: the label `GitLab`, `AccountsSignedOutGitLab` `Not signed in.` and `AccountsSignInButtonGitLab` `Sign in…`, enabled, in the same columns and gaps; 24 below, `AccountsAzureDevOps`: the label `Azure DevOps`, `AccountsSignedOutAzureDevOps` `Not signed in.`, `AccountsSignInButtonAzureDevOps` `Sign in…`, enabled. In no block an `AccountLogin`, `AccountUser`, `AccountTokenPlace`, `AccountNeedsSignIn`, `AccountsSignOutNote`, `AccountsSignOutButton`, `AccountsBusy` or `Git` row, with or without a suffix; no `AccountsOff`. GitHub's block starts 18 below the intro, at 7a's place. The section fits: its extent within its viewport, offset 0. `Cancel` and `Save` as phase 6's part S; no text cut; no token on screen. Git calls since the quiet mark (`QuietCallsSinceAsync`, whole refreshes set aside by `GitCallBudget.NotWholeRefreshes`, none expected on an empty tab): exactly the screen's reads on an empty tab (phase 6's SD29: the outside read `git config --show-scope --show-origin -z --list` in `{data}`), nothing else; exact, because it is the check's meaning (ruling 28). Requests none; calls none. |  | n/a | `a01a-three-signed-out` | n/a: WP-R's three-account launch passes this state but captures only A2's end (14.4); 7a's real-window `a01a` keeps the signed-out GitHub block. The intro is measured first (10.9): one or three lines would move every block of 7a's 23 pictures. The start's log lines are those the real window waits for (14.3). Varies by nature: the status bar's git box behind the backdrop (D248). |
| A2 | One, two and three signed in, the last at the top and at the section's end (D345, D348, D353; design 10.3, 10.4, 6.4, 7.4, 9.3; R3): each sign-in through its block's "Sign in…" and its dialog with the keychain; GitHub's block 7a's; the section scrolls with three; Fernhill-Labs bold over Ada's name; the organization typed in lower case shown in the server's spelling; three records and three keys; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 1**, after A1: 3. Mark the requests and the calls. `OpenSignInDialogAsync(GitHub)`; `TypeToken(GitHubToken)`; `ConfirmSignInAsync()`; wait until the dialog has closed. Screenshot (`a02a-github-signed-in`). 4. Mark. `OpenSignInDialogAsync(GitLab)`; `TypeToken(GitLabToken)`; `ConfirmSignInAsync()`; wait until the dialog has closed. Screenshot (`a02b-gitlab-signed-in`). 5. Mark. `OpenSignInDialogAsync(AzureDevOps)`; `TypeOrganization(PartA.OrganizationLower)`; click `SignInToken`; `TypeToken(AzureDevOpsToken)`; `ConfirmSignInAsync()`; wait until the dialog has closed. 6. Turn the wheel up over `SettingsSection` until the offset stops changing. Screenshot (`a02c-three-signed-in-top`). 7. Turn the wheel down until the offset stops changing; assert the end. Screenshot (`a02d-three-signed-in-end`). 8. Press Esc. | **After step 3:** GitHub's dialog was 7a's, `Add GitHub account` with 7a's help and link and the focus in `SignInToken`. Calls exactly `probe` (the dialog's `KeychainAvailableAsync`), then `save github:127.0.0.1:47399:octo`; requests: GitHub's exactly one, `GET api/v3/user` (route `user`), `TokenPresent`, 200, `Login` `octo`; GitLab's and Azure DevOps' none. The session's `AccountOf(GitHub)` `SignedIn`, place `Keychain`, note kind `None`, the scenario facts' account; the other two null. GitHub's block as 7a's A5 step 7 shows it: `AccountLogin` `octo` (SemiBold) from x 480, `AccountTokenPlace` `The token is kept in this computer's keychain.`, `Sign out…` enabled with the keyboard focus and no `Sign in…`, the `Git` row with `AccountUseForGit` `Use for git over HTTPS` checked and 7a's note; no `AccountUser`; GitLab's and Azure DevOps' blocks as after step 2, each 24 below the one before; the section fits (open point 2); no text cut; no token on screen.<br>**After step 4:** the dialog was `Add GitLab account` (A5's look) with the focus in `SignInToken`. Calls exactly `save gitlab:127.0.0.1:47398:ada` (the probe was step 3's); requests: GitLab's exactly one, `GET /api/v4/user` (route `user`), `Bearer`, 200, `Login` `ada`; the others none. `AccountOf(GitLab)` `SignedIn`, `Keychain`, `None`, the scenario facts' account. GitLab's block: `AccountLoginGitLab` `ada` (SemiBold, from x 480), no `AccountUserGitLab`, `AccountTokenPlaceGitLab` with the keychain's text, `AccountsSignOutButtonGitLab` enabled with the focus, no `Sign in…`; its `Git` row with `AccountUseForGitGitLab` checked and `AccountUseForGitNoteGitLab` GitLab's note; GitHub's block unchanged; Azure DevOps' signed out; the section fits (open point 2); no text cut.<br>**After step 5:** the dialog was `Add Azure DevOps account` and opened with `SignInOrganization` empty and focused (an empty tab, no account held, 10.5). Calls exactly `save azure-devops:127.0.0.1:47397:fernhill-labs`; requests: Azure DevOps' exactly two, in order: `GET /fernhill-labs/_apis/connectionData`, query `api-version=7.1-preview.1` (route `connection:fernhill-labs`), `BasicEmptyUser`, 200, `Login` Ada's; then `GET /fernhill-labs/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf`, the same query (route `areas`), `BasicEmptyUser`, 200; GitHub's and GitLab's none. `AccountOf(AzureDevOps)` `SignedIn`, `Keychain`, `None`, `Login` `Fernhill-Labs` (the resource area's spelling, though `fernhill-labs` was typed, 6.4 step 2), key `azure-devops:127.0.0.1:47397:fernhill-labs`, `Id` 0, `Name` `Ada Lovelace`, `UserId` Ada's id in lower case, `UseForGit` true. The focus on `AccountsSignOutButtonAzureDevOps`, wholly inside the viewport.<br>**After step 6:** offset 0; the section scrolls (its extent above its viewport, about 562 of 495; 10.3 estimated 553, 10.3). Top to bottom: the intro, GitHub's block, GitLab's block, and Azure DevOps' from its label: `AccountLoginAzureDevOps` `Fernhill-Labs` (SemiBold) from x 480, directly under it `AccountUserAzureDevOps` `Ada Lovelace` (`secondary`, `#9BA3B0`, regular weight), then `AccountTokenPlaceAzureDevOps` with the keychain's text and `AccountsSignOutButtonAzureDevOps`; what lies below the viewport's bottom is scrolled out of view, not cut. No text cut; no token on screen.<br>**After step 7:** before the capture the offset is the end (`Extent.Height − Viewport.Height`, ±0.5) and above 0; Azure DevOps' block wholly in view, its `Git` row with `AccountUseForGitAzureDevOps` checked and `AccountUseForGitNoteAzureDevOps` Azure DevOps' note (open point 3), its last line inside the viewport; the intro and the top of GitHub's block scrolled out of view; the focus still on Azure DevOps' "Sign out…"; no text cut; no token on screen; requests and calls none since step 5.<br>**After step 8:** the screen closed; the three still signed in (D256). `accounts.json`: `version` 1 and three records with exactly the scenario facts' fields, each `useForGit` true and `tokenState` `keychain`, `userId` on Azure DevOps' alone; no `@` in the file. `Tokens.Holds` each of the three keys. |  |  | `a02a-github-signed-in`, `a02b-gitlab-signed-in`, `a02c-three-signed-in-top`, `a02d-three-signed-in-end`; real window: `a02d-three-signed-in-end` | **R** (14.4 A, `Check_A_three_accounts`, steps 3 to 7): the launch makes no request (D257, D350); `SignInThroughSettings` with `keepOpen` for GitHub, GitLab and Azure DevOps in D353's order, typing the scenario's tokens and `PartA.OrganizationLower`; each fake's requests as steps 3 to 5 give them, route keys in order (`user`; `user`; `connection:fernhill-labs`, `areas`), with `Bearer` on GitLab's and `BasicEmptyUser` on Azure DevOps', 200; `ScrollAccountsToEnd` (the real wheel until UI Automation's `VerticalScrollPercent` is 100, asserted before the capture); compared with `a02d-three-signed-in-end`; Esc; `accounts.json` holds three records, `userId` on Azure DevOps' alone, no token; `AssertCleanEnd`. The real window's store is memory, available, so it shows the keychain's text. "One, two and three signed in" read as GitHub, then GitLab, then Azure DevOps, D353's order, as WP-R's launch signs in. `a02a` to `a02c` are scripted only. Varies by nature: the status bar's git box. |
| A3 | GitLab off, its block gone; all three off (D353, D362; design 10.3, 1.8): a provider without a fake is off and its block takes no room; with all off, GitHub's block holds 7a's off text alone; "hosting7b" served, one empty tab, GitHub's and Azure DevOps' fakes alone; then a start of its own with nothing served and no fake; dark, 1100×700 | **Method 2** (`Check_A3_gitlab_off`): 1. `ServeAsync()`. `ShellDriver.Start(data, gitHub: served.GitHub, azureDevOps: served.AzureDevOps)` (no GitLab fake given: GitLab off, D362); wait until ready; rest the pointer. Mark the requests. 2. `OpenAccountsAsync()`. Screenshot (`a03a-gitlab-off`). Close the app.<br>**Method 3** (`Check_A3_three_off`), a data folder of its own, nothing served: 1. `ShellDriver.Start(data)` (no fake: the three off); wait until ready. 2. `OpenAccountsAsync()`. Screenshot (`a03b-three-off`). Close the app. | **Method 2, after step 1:** `Providers` exactly GitHub, Azure DevOps; `IsOn(GitLab)` false and `ProviderOf(GitLab)` null; the log's start lines `GitHub: 127.0.0.1:47399.`, `GitLab: off.`, `Azure DevOps: 127.0.0.1:47397.` in that order; requests none in any of the three fakes (GitLab's runs but is given to no session); calls none.<br>**Method 2, after step 2:** the intro as A1's (the one sentence, 10.2); `AccountsGitHub` as A1's; 24 below it `AccountsAzureDevOps` as A1's; `AccountsGitLab` not visible and taking no room, no control whose id ends in `GitLab` visible; the section fits; no text cut; requests none; calls none.<br>**Method 3, after step 1:** the session's `IsOff` true, `Providers` empty.<br>**Method 3, after step 2:** the intro (the same sentence); `AccountsGitHub` alone, its label `GitHub` and from x 480 `AccountsOff` `GitHub is turned off for this session.` (7a's text, D362), no `Sign in…` or `Sign out…` (7a's: no button while off); no `AccountsGitLab` or `AccountsAzureDevOps` visible; no text cut; calls none. |  | n/a | `a03a-gitlab-off`, `a03b-three-off` | n/a: the three providers are on in every real-window launch (14.2). The all-off start goes beyond 10.7's list, for D362's "with all three off the section shows 7a's off text" (open point 1); it is a method of its own, since it starts on a data folder of its own (one method per app start). Varies by nature: the status bar's git box. |
| A4 | Fernhill-Labs bold over Ada's name (design 10.3, 6.4; D368): Azure DevOps' block alone signed in, its organization as the login and the account's name under it, no e-mail address anywhere; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 5**, after A7: 11. Wait until the dialog has closed and `WaitForAccountAsync(AzureDevOps, a => a?.State == AccountState.SignedIn)`. Screenshot (`a04a-azure-signed-in`). 12. Press Esc. | **After step 11:** requests since A7's mark exactly two: `connection:fernhill-labs`, `BasicEmptyUser`, 200, `Login` Ada's, then `areas`, 200 (the held request answered after its release); calls since A7's mark exactly `save azure-devops:127.0.0.1:47397:fernhill-labs`. `AccountOf(AzureDevOps)` as A2's step 5 gives it (`Fernhill-Labs`, `Id` 0, `Name` `Ada Lovelace`, `UserId`); `AccountOf(GitHub)` and `AccountOf(GitLab)` null. The section at offset 0, fitting: GitHub's and GitLab's blocks signed out as in A1; Azure DevOps' block: the label `Azure DevOps`; `AccountLoginAzureDevOps` `Fernhill-Labs` (SemiBold) from x 480; directly under it `AccountUserAzureDevOps` `Ada Lovelace` (`secondary`, `#9BA3B0`, regular weight); then `AccountTokenPlaceAzureDevOps` `The token is kept in this computer's keychain.` (`settingsNote`); 10 below, `AccountsSignOutButtonAzureDevOps` enabled with the keyboard focus, no `Sign in…`; the `Git` row: `AccountUseForGitAzureDevOps` checked and `AccountUseForGitNoteAzureDevOps` Azure DevOps' note; no text cut; no token on screen. `accounts.json`: one record, Azure DevOps' nine fields, `useForGit` true, `tokenState` `keychain`; no `@` in the file (Ada's sign-in name, an address, is never kept: 6.4, D368). `Holds(azure-devops:127.0.0.1:47397:fernhill-labs)`.<br>**After step 12:** the screen closed; still signed in (D256). |  | n/a | `a04a-azure-signed-in` | n/a: the real window shows the bold organization over Ada's name in A2's end picture (three accounts). The name is the account's `Name` (her `customDisplayName`), never the identity's `Account.$value`. GitHub's and GitLab's blocks show no name line (`UserName` null, 10.3). |
| A5 | GitLab's dialog: empty, a masked token, both links' launcher records (D345, D385; design 10.2, 10.4, 7.4): the help naming D385's user permissions, the fine-grained link and the legacy link 4 below it; the token masked on screen and to UI Automation; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 4** (`Check_A5_A8_A11_gitlab_dialog_links_and_refusals`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)`; `OpenAccountsAsync()`. Mark the requests and the calls. 2. `OpenSignInDialogAsync(GitLab)`. Screenshot (`a05a-gitlab-dialog`). 3. `TypeToken(GitLabToken)`. Screenshot (`a05b-gitlab-token-masked`). 4. Rest the pointer on `SignInTokenLink` until its tooltip shows; then click it. 5. Rest the pointer on `SignInLegacyTokenLink` until its tooltip shows; then click it. | **After step 2:** `SignInDialog`, named `Add GitLab account`, stacked over the screen (the card under the dialog's backdrop, still on Accounts, the three blocks signed out); the card 400 wide, x 350 to 750, centred vertically; top to bottom `DialogTitle` `Add GitLab account`, `SignInHelp` with GitLab's text (whole, wrapped), `SignInTokenLink` `Create a fine-grained token on GitLab` in `VcAccentBrush` `#7C8CFF`, 6 below the help, `SignInLegacyTokenLink` `Or create a legacy token, filled in` in the accent too, 4 below the first, the label `Token`, `SignInToken` empty with the keyboard focus; no `SignInOrganization`, `SignInNoKeychain`, `SignInChecking`, `SignInError` or `SignInSaveFailed`; `Cancel` enabled and `Sign in` (`VcPrimaryButton`) disabled, right-aligned; no "In …" line. Calls `probe`; requests none; no text cut.<br>**After step 3:** the box shows one `•` per character of `GitLabToken`; its automation value empty and its name `Token`; no token on screen; `Sign in` enabled; `Launcher.Requests` empty (the picture's state owes nothing to the fake launcher, R); requests none; calls still `probe` only.<br>**After step 4:** the tooltip `Opens GitLab's form for a new fine-grained token in your browser.`; after the click `Launcher.Requests` holds one launch, `OpenUrl` `http://127.0.0.1:47398/-/user_settings/personal_access_tokens/granular/new`; the dialog still open, the box still masked and holding the token, `Sign in` enabled; requests none.<br>**After step 5:** the tooltip `Opens GitLab's form for a legacy token with the api scope in your browser.`; after the click `Launcher.Requests` holds two launches, the second `OpenUrl` `http://127.0.0.1:47398/-/user_settings/personal_access_tokens?name=VisualCommit&scopes=api`; the dialog still open as after step 4; requests none; calls still `probe` only. |  |  | `a05a-gitlab-dialog`, `a05b-gitlab-token-masked`; real window: `a05b-gitlab-token-masked` | **R** (14.4, `Check_GitLab_sign_in_browse_and_clone` step 1): `OpenSignInDialog(GitLab)` from the Accounts section; a real click on `SignInToken`; `GitLabToken` typed on the real keyboard: the box's Value pattern reads "" and its name "Token", no text element holds the token; compared with `a05b-gitlab-token-masked`. Steps 4 and 5 are scripted only (the fake launcher; no 7b step opens the browser, ruling 29) and come after `a05b`, so that the R picture's state is reached as the real window reaches it. Varies by nature: the box's caret (`a05a`, `a05b`); the status bar's git box. |
| A6 | Azure DevOps' dialog empty, with the organization typed and a masked token, and prefilled from mobile-app's tab (D360; design 10.2, 10.4, 10.5, 6.6): the organization box above the token, focused when empty; the link enabled only with an organization; the prefill from the tab's remote; "hosting7b", one empty tab, then a tab on `mobile-app` signed out, the three fakes, dark, 1100×700 | **Method 5** (`Check_A6_to_A11_and_A4_azure_dialog`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)`; `OpenAccountsAsync()`. Mark the requests and the calls. 2. `OpenSignInDialogAsync(AzureDevOps)`. Screenshot (`a06a-azure-dialog-empty`). 3. `TypeOrganization(PartA.Organization)`; click `SignInToken`; `TypeToken(AzureDevOpsToken)`. Screenshot (`a06b-azure-token-masked`). 4. Rest the pointer on `SignInTokenLink` until its tooltip shows; then click it.<br>**Method 6** (`Check_A6_azure_dialog_prefilled_from_the_tab`): 1. `ServeAsync()`. `OpenAsync(data, served, signIn: [])` (one tab on `mobile-app`, signed out). Mark the requests and the calls. 2. `OpenAccountsAsync()`; `OpenSignInDialogAsync(AzureDevOps)`. Screenshot (`a06c-azure-prefilled`). 3. `TypeToken(AzureDevOpsToken)`; press Enter; wait until the dialog has closed; wait for the log line `mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app` since the mark and for the watchers to be quiet. Press Esc. | **Method 5, after step 2:** `SignInDialog`, named `Add Azure DevOps account`, stacked over the screen as A5's; the card 400 wide, x 350 to 750, centred vertically, at most about 345 high (10.4; open point 7); top to bottom `DialogTitle` `Add Azure DevOps account`, `SignInHelp` with Azure DevOps' text (whole), `SignInTokenLink` `Create a token on Azure DevOps`, 6 below the help, disabled (no organization, 10.4), in the disabled look: the theme's disabled foreground `VcTextDisabledBrush` `#5F6775` in place of the accent and the arrow cursor, and after the capture, with the pointer moved onto it, no underline, the label `Organization`, `SignInOrganization` empty (`dialogBox`, automation name `Organization`) with the keyboard focus (focused when empty, 10.4), the label `Token`, `SignInToken` empty (open point 4); no `SignInLegacyTokenLink`, `SignInNoKeychain` or status line; `Cancel` enabled, `Sign in` disabled. Calls `probe`; requests none; no text cut.<br>**Method 5, after step 3:** `SignInOrganization` `Fernhill-Labs`; `SignInTokenLink` enabled; the token box masked, one `•` per character, its automation value empty and name `Token`; no token on screen; `Sign in` enabled; `Launcher.Requests` empty (R); requests none; calls still `probe` only; the card still within 345 high.<br>**Method 5, after step 4:** the tooltip `Opens the organization's page for new tokens in your browser.`; after the click `Launcher.Requests` holds one launch, `OpenUrl` `http://127.0.0.1:47397/Fernhill-Labs/_usersSettings/tokens` (6.6); the dialog still open, both boxes as after step 3; requests none.<br>**Method 6, after step 1:** `mobile-app`'s tab with its graph; `AccountOf(AzureDevOps)` null; requests none in any fake since the start (no account: nothing is sent for the tab, D257, D350).<br>**Method 6, after step 2:** the dialog as method 5's after step 2, but `SignInOrganization` holds `Fernhill-Labs` (the `OwnerRoot` of the tab's origin, 10.5), the keyboard focus in `SignInToken`, `SignInTokenLink` enabled, `Sign in` disabled (no token); calls `probe`; requests none; no text cut.<br>**Method 6, after step 3:** requests since the mark begin with `connection:fernhill-labs` (`GET /Fernhill-Labs/_apis/connectionData`, the prefilled organization sent as it reads), `BasicEmptyUser`, 200, then `areas`, 200; calls `save azure-devops:127.0.0.1:47397:fernhill-labs`; `AccountOf(AzureDevOps)` `SignedIn` with `Login` `Fernhill-Labs`; the log line written; after Esc the screen closed. |  |  | `a06a-azure-dialog-empty`, `a06b-azure-token-masked`, `a06c-azure-prefilled`; real window: `a06b-azure-token-masked` | **R** (14.4, Azure DevOps' "Sign-in, browse, clone" launch, step 1): `OpenSignInDialog(AzureDevOps)`; `TypeOrganization` with `PartA.Organization` (a click on `SignInOrganization`, Ctrl+A, typing; its Value read back `Fernhill-Labs`); a click on `SignInToken`, the token typed on the real keyboard: its Value "" and name "Token"; compared with `a06b-azure-token-masked`. `a06a`, `a06c` and step 4 are scripted only. **WP-P's region**: in `a06c` mobile-app's left panel and graph lie behind the backdrop; nothing is asserted there, and the lead inspects the picture again at WP-P's merge (0.4, 12.9, 16.2). Varies by nature: the boxes' caret; the status bar's git box. |
| A7 | The check running (design 10.4, 7a design 8.4): `Checking the token…` with both boxes and "Sign in" disabled while connectionData is held; nothing kept until it answers; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 5**, after A10: 10. With the keyboard focus in `SignInOrganization` and its whole text selected by A10's refusal, `app.Type(PartA.Organization)` (the selection replaced). Mark the requests and the calls. `served.AzureDevOps.HoldNext("connection:fernhill-labs")`. Click the dialog's `Sign in`; wait for the hold's `Arrived`. Screenshot (`a07a-azure-checking`). Dispose the hold (its release; A4's step 11 follows). | **After step 10:** below the token box `SignInChecking` `Checking the token…` (secondary); `SignInOrganization` (`Fernhill-Labs`) and `SignInToken` (masked) both disabled (open point 6); `Sign in` disabled, `Cancel` enabled; no `SignInError`; the hold's `Arrived` completed: one connectionData request waits at the fake, not yet recorded (requests since the mark none); `AccountOf(AzureDevOps)` null; calls since the mark none; no `accounts.json`; no text cut; no token on screen. |  | n/a | `a07a-azure-checking` | n/a: a held request (0.5). Cancel during the check is 7a's A3, the same per provider (10.4), and `SignInDialogProvidersTests`'. The release lets the check go on to A4's sign-in. |
| A8 | Refused by a 401 (design 10.2, 10.4; D373; the fakes as WP-LF left them): GitLab's dialog with a `glpat-` token GitLab never issued shows GitLab's message; Azure DevOps' with a PAT-shaped value it never issued shows the fixed text naming the organization; the token box kept and selected; nothing kept; GitLab's legacy token then signs in; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 4**, after A11's step 6: 7. With the focus still in `SignInToken`, press Ctrl+A, then `app.Type(PartA.UnknownGitLabToken)`; press Enter; wait for the check to end. Screenshot (`a08a-gitlab-refused`). 8. Mark. With the box's whole text selected by the refusal, `app.Type(GitLabLegacyToken)` (the selection replaced); press Enter; wait until the dialog has closed.<br>**Method 5**, after A11's step 6: 7. Replace `SignInToken`'s text with `PartA.UnknownAzureDevOpsToken`; press Enter; wait for the check to end. Screenshot (`a08b-azure-refused`). | **Method 4, after step 7:** requests exactly one, GitLab's `GET /api/v4/user` (route `user`), `Bearer`, 401, no `Login`; `SignInError` `GitLab refused the token: 401 Unauthorized.` (`dialogError`, `VcDangerBrush` `#E5606B`); the box enabled, its text kept and all of it selected, the keyboard focus in it; `Sign in` and `Cancel` enabled; `AccountOf(GitLab)` null; no `accounts.json`; calls still `probe` only; no text cut; no token on screen.<br>**Method 4, after step 8:** requests since the mark exactly one, `GET /api/v4/user`, `Bearer`, 200, `Login` `ada` (the legacy kind is taken as the fine-grained one, D345); calls `save gitlab:127.0.0.1:47398:ada`; the dialog closed; `AccountOf(GitLab)` `SignedIn`, `Keychain`, the scenario facts' account; GitLab's block as A2's step 4 shows it, the focus on its "Sign out…"; `accounts.json` one record, GitLab's eight fields, `tokenState` `keychain`.<br>**Method 5, after step 7:** requests exactly one, Azure DevOps' `GET /Fernhill-Labs/_apis/connectionData` (route `connection:fernhill-labs`), `BasicEmptyUser`, 401 (the empty body), no `Login`; no `areas` request; `SignInError` `Azure DevOps refused the token for Fernhill-Labs. Check that it was made for this organization and has not expired.` (`dialogError`); `SignInOrganization` still `Fernhill-Labs`, enabled; `SignInToken` enabled, its text kept and all of it selected, the keyboard focus in it; `Sign in` and `Cancel` enabled; `AccountOf(AzureDevOps)` null; no `accounts.json`; calls still `probe` only; no text cut; no token on screen. |  | n/a | `a08a-gitlab-refused`, `a08b-azure-refused` | n/a: refusals, scripted only; the real window types only the scenario's tokens. GitLab's 401 goes beyond 10.7's list, which names Azure DevOps' (open point 1). Azure DevOps' 401 bodies are empty, HTML or a GUID's, so its text is fixed (10.2, g1 G0.2). Varies by nature: the box's caret. |
| A9 | Refused by the Anonymous 200 (D360; design 10.2, 6.4; D373; 16.1): a value in none of the PAT forms is answered as a visitor, which the dialog names apart from a refused token; one request, no second read. An organization that allows public projects answers such a value so (MEASURED 2026-11-05 in the sandbox's organization, which allows them, MEASURED 2026-11-07); Fernhill-Labs is private, where the fake answers it as no credential, so the check sets the fault `FakeAzureDevOpsFault.AnonymousIdentity`, which gives the public organization's answer (open point 8); "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 5**, after A8: 8. `served.AzureDevOps.FailNext("connection:fernhill-labs", FakeAzureDevOpsFault.AnonymousIdentity)`. Mark the requests. With `SignInToken`'s whole text selected by A8's refusal, `app.Type(PartA.PlainValue)` (the selection replaced); press Enter; wait for the check to end. Screenshot (`a09a-azure-anonymous`). | **After step 8:** requests since step 8's mark exactly one, `GET /Fernhill-Labs/_apis/connectionData` (route `connection:fernhill-labs`), `BasicEmptyUser`, 200 (the Anonymous identity, the fault's), no `Login`; no `areas` request (A1 stops at the Anonymous identity, 6.4 step 2); `SignInError` `Azure DevOps did not take the token as a sign-in and answered as it does for a visitor. Paste the whole token, made for Fernhill-Labs.` (`dialogError`); `SignInOrganization` `Fernhill-Labs`; `SignInToken` enabled, its text kept and all of it selected, the focus in it; `Sign in` and `Cancel` enabled; `AccountOf(AzureDevOps)` null; no `accounts.json`; calls still `probe` only; no text cut; no token on screen. |  | n/a | `a09a-azure-anonymous` | n/a: a refusal, scripted only, so the fault is allowed. The Anonymous answer is the fault's (open point 8): `Check_1b`'s probe measured on 2026-11-07 that the sandbox's organization allows public projects, so the fake keeps the Anonymous 200 to organizations with `PublicProjects`, and Fernhill-Labs, private, answers the value without the fault with the sign-in's 401 and A8's text, which `SignInDialogProvidersTests` checks. Varies by nature: the box's caret. |
| A10 | Refused for an unknown organization (D360, D374; design 10.2, 10.4, 6.4): a 404 from connectionData named as no such organization, the organization box's text selected; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 5**, after A9: 9. Mark the requests. Replace `SignInOrganization`'s text with `PartA.UnknownOrganization`; replace `SignInToken`'s with `AzureDevOpsToken`; press Enter; wait for the check to end. Screenshot (`a10a-azure-no-organization`). | **After step 9:** requests since step 9's mark exactly one, `GET /Fernhill-Lab/_apis/connectionData` (route `connection:fernhill-lab`), `BasicEmptyUser`, 404, no `Login`; no `areas` request; no request to another organization's path; `SignInError` `Azure DevOps has no organization named Fernhill-Lab.` (`dialogError`); `SignInOrganization` enabled, holding `Fernhill-Lab`, all of it selected, with the keyboard focus (`NotFound` selects the organization box's text, 10.4); `SignInToken` kept, masked; `SignInTokenLink` enabled (`Fernhill-Lab` is in the grammar); `Sign in` and `Cancel` enabled; `AccountOf(AzureDevOps)` null; no `accounts.json`; calls still `probe` only; no text cut; no token on screen. |  | n/a | `a10a-azure-no-organization` | n/a: a refusal. The 404 with a token is MEASURED by D374's read of a made-up organization (16.1). Varies by nature: the box's caret. |
| A11 | Refused locally (D378, D298; design 10.2, 10.4, 1.2): a token of another provider's form in each provider's dialog, with no request to any fake; an organization outside the grammar in Azure DevOps' dialog; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 4**, after A5: 6. Replace `SignInToken`'s text with `PartA.AzureDevOpsForm`; press Enter. Screenshot (`a11a-gitlab-azure-token`). (A8's steps 7 and 8 follow.) 9. Mark the requests. `OpenSignInDialogAsync(GitHub)`; `TypeToken(GitLabToken)`; press Enter. 10. Press Esc.<br>**Method 5**, after A6's step 4: 5. Replace `SignInOrganization`'s text with `PartA.NotAnOrganization`; click the dialog's `Sign in`. Screenshot (`a11b-azure-not-an-organization`). 6. Replace `SignInOrganization`'s text with `PartA.Organization`; replace `SignInToken`'s with `GitHubToken`; press Enter. Screenshot (`a11c-azure-github-token`). | **Method 4, after step 6:** `SignInError` `This looks like an Azure DevOps token. Paste a token made on GitLab.` (`dialogError`); requests none in any fake; the box enabled, holding what was typed, the focus still in it (as D298's refusal, 7a's A4 step 4); `Sign in` enabled; `AccountOf(GitLab)` null; calls still `probe` only; no text cut; no token on screen.<br>**Method 4, after step 9:** GitHub's dialog, 7a's; `SignInError` `This looks like a GitLab token. Paste a token made on GitHub.`; requests none since the mark in any fake (GitHub's fake got nothing); `AccountOf(GitHub)` null.<br>**Method 4, after step 10:** the dialog closed, the screen open, GitHub's block signed out, GitLab's signed in as after A8's step 8.<br>**Method 5, after step 5:** `SignInError` `Type the organization's name, of letters, digits and hyphens, or its address on dev.azure.com or visualstudio.com.` (`dialogError`); requests none; `SignInOrganization` holding `Fernhill Labs`; `SignInTokenLink` disabled (no organization: `OrganizationOf` gives none), in the disabled look: the theme's disabled foreground `VcTextDisabledBrush` `#5F6775` in place of the accent and the arrow cursor, and after the capture, with the pointer moved onto it, no underline; the token kept, masked; `Sign in` enabled (both boxes hold text); which box has the focus is not asserted (open point 5); calls still `probe` only; no text cut.<br>**Method 5, after step 6:** `SignInError` `This looks like a GitHub token. Paste a token made for Fernhill-Labs on Azure DevOps.`; requests none in any fake; `SignInOrganization` `Fernhill-Labs`, the link enabled; `SignInToken` holding what was typed, masked, the focus still in it; `Sign in` enabled; `AccountOf(AzureDevOps)` null; calls still `probe` only; no text cut; no token on screen. |  | n/a | `a11a-gitlab-azure-token`, `a11b-azure-not-an-organization`, `a11c-azure-github-token` | n/a: local refusals, scripted only. The organization's refusal comes before D298's and D378's (10.4's order). Each dialog given each other provider's form is `SignInDialogProvidersTests`' (10.8); GitHub's step is asserted, not pictured. Varies by nature: the boxes' caret. |
| A12 | Session only where no keychain can be reached, and its restart (D253, D287; design 10.4, 10.5, 9.4): the warning in GitLab's and Azure DevOps' dialogs before anything is typed; both accounts kept in memory, never saved; the blocks' note; `tokenState` `session`; at the next start both need sign-in, with no keychain call, and Azure DevOps' dialog is prefilled with the held account's organization; "hosting7b", one empty tab per start, the three fakes, the store unavailable, dark, 1100×700 | **Method 7** (`Check_A12_session_only_and_its_restart`, a `[Fact]`), first start: 1. `ServeAsync()`; a `FakeTokenStore` with `Available = false`. `StartEmptyAsync(data, served, tokens: store)`; `OpenAccountsAsync()`. Mark the requests and the calls. 2. `OpenSignInDialogAsync(GitLab)`. Screenshot (`a12a-gitlab-no-keychain-dialog`). 3. `TypeToken(GitLabToken)`; press Enter; wait until the dialog has closed. 4. Mark the requests. `OpenSignInDialogAsync(AzureDevOps)`; `TypeOrganization(PartA.Organization)`; click `SignInToken`; `TypeToken(AzureDevOpsToken)`; press Enter; wait until the dialog has closed. Turn the wheel up over `SettingsSection` until the offset stops changing. Screenshot (`a12b-gitlab-azure-session-only`). 5. Close the app.<br>Second start, the same data folder, store and fakes: 6. Mark the requests and the calls. `StartEmptyAsync(data, served, tokens: store)`; `OpenAccountsAsync()`. Screenshot (`a12c-gitlab-azure-session-ended`). 7. Click `AccountsSignInButtonAzureDevOps`; wait for the dialog. Press Esc. Close the app. | **After step 2:** GitLab's dialog as A5's step 2, and below the token box, before anything is typed, `SignInNoKeychain` `No keychain was found on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again at the next start.` (`dialogWarning`, `VcWarningBrush` `#E0A23B`); calls `probe`; requests none.<br>**After step 3:** requests exactly one, GitLab's `GET /api/v4/user`, 200, `Login` `ada`; the dialog closed (no failed save, no "Close"-only state); `AccountOf(GitLab)` `SignedIn`, place `Session`, note kind `NoKeychain`.<br>**After step 4:** Azure DevOps' dialog showed `SignInNoKeychain` too, with the focus in the empty `SignInOrganization`; requests since step 4's mark exactly two, Azure DevOps' `connection:fernhill-labs` 200 and `areas` 200; `AccountOf(AzureDevOps)` `SignedIn`, `Session`, `NoKeychain`. Before the capture the offset is 0, asserted. The section from its top (open point 2: part A's tightest state, about 502 of 495 as WP-A's layout measures it): it scrolls at offset 0, its extent above its viewport, and the bottom of `AccountUseForGitNoteAzureDevOps` lies below the viewport by less than one of its lines (they are about 14 apart: 13.9, `PartA.A12NoteLine`, as the note's layout gives them, ±0.5), the last line's top inside the viewport, scrolled out of view, not cut; GitHub's block signed out; GitLab's `AccountLoginGitLab` `ada` and `AccountTokenPlaceGitLab` with D253's text (`settingsNote`), `Sign out…`, the `Git` row checked with GitLab's note; Azure DevOps' `AccountLoginAzureDevOps` `Fernhill-Labs`, `AccountUserAzureDevOps` `Ada Lovelace`, `AccountTokenPlaceAzureDevOps` with D253's text, `Sign out…` with the focus, the `Git` row checked with its note. `accounts.json`: two records, GitLab's and Azure DevOps', `tokenState` `session`, `useForGit` true. Calls since step 1 exactly `probe`: no save (D253); `Holds` neither key. No text cut; no token on screen.<br>**After step 6:** the start's calls none (a `session` record reads no store, D287); requests none (no check at a start); `AccountOf(GitLab)` and `AccountOf(AzureDevOps)` `NeedsSignIn`, note kind `SessionEnded`; `AccountOf(GitHub)` null. The section, fitting: GitHub's block signed out; GitLab's `ada`, `AccountNeedsSignInGitLab` `The token was kept for the last session only. Sign in again.` (`dialogWarning`), `Sign in…` and `Sign out…` enabled, the `Git` row checked; Azure DevOps' `Fernhill-Labs`, `Ada Lovelace`, `AccountNeedsSignInAzureDevOps` with the same text, both buttons enabled, the `Git` row checked. `accounts.json` byte for byte as after step 4 (a start writes nothing). No text cut.<br>**After step 7:** the dialog `Add Azure DevOps account` stacked over the screen: `SignInOrganization` holds `Fernhill-Labs` (the held account's login; the tab is empty, 10.5), the keyboard focus in `SignInToken`, `SignInNoKeychain` shown, the link enabled, `Sign in` disabled; calls `probe`; requests none. After Esc the dialog closed and the screen still open on Accounts, both accounts still needing sign-in. |  | n/a | `a12a-gitlab-no-keychain-dialog`, `a12b-gitlab-azure-session-only`, `a12c-gitlab-azure-session-ended` | n/a: D253's path needs `FakeTokenStore.Available` (a hook, 0.5), and the real window neither restarts nor has an unavailable store (14.1). Each start is a `FreshApplication.RunAsync` half (D32). The held account's prefill is asserted, not pictured; `a06c` pictures the tab's. Varies by nature: the box's caret (`a12a`); the status bar's git box. |
| A13 | One sign-out leaves the other blocks and records (D348, D291, D296; design 10.2, 10.3, 9.4): GitLab's question names its login and merge requests; after it GitLab's block is signed out and its record and key gone, GitHub's and Azure DevOps' untouched; Azure DevOps' question names the organization; "hosting7b", one empty tab, the three signed in, dark, 1100×700 | **Method 8** (`Check_A13_one_sign_out_keeps_the_others`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)`; `SignInAsync(GitHub, GitHubToken)`; `SignInAsync(GitLab, GitLabToken)`; `SignInAsync(AzureDevOps, AzureDevOpsToken, PartA.Organization)`. `OpenAccountsAsync()`. Mark the requests and the calls; read `accounts.json`. 2. Click `AccountsSignOutButtonGitLab`; wait for the confirmation. Screenshot (`a13a-gitlab-sign-out-question`). 3. Click the confirmation's `Sign out`; wait until GitLab's sign-out has ended (`WaitForAccountAsync(GitLab, a => a is null)`). Screenshot (`a13b-gitlab-signed-out`). 4. Click `AccountsSignOutButtonAzureDevOps`; wait for the confirmation. Screenshot (`a13c-azure-sign-out-question`). 5. Click `Cancel`. | **After step 1:** the three `SignedIn`, `Keychain`; the section scrolls (three signed in), at offset 0, GitLab's "Sign out…" in view; `accounts.json` three records.<br>**After step 2:** `ConfirmationDialog` stacked over the screen: `ConfirmTitle` `Sign out of GitLab?`; `ConfirmQuestion` `Sign out of GitLab as ada? VisualCommit removes its token from this computer and no longer lists merge requests. Git over HTTPS asks your own credential helpers again.`; no `ConfirmDetail`; `Cancel` and `Sign out` (`ConfirmButton`) in the primary look (`VcPrimaryButton`), not the danger one (D296); no "In …" line. Still signed in; calls none; requests none; no text cut.<br>**After step 3:** calls since the mark exactly `delete gitlab:127.0.0.1:47398:ada`; `Holds(gitlab:…:ada)` false, `Holds` GitHub's and Azure DevOps' keys true; requests none. `AccountOf(GitLab)` null; `AccountOf(GitHub)` and `AccountOf(AzureDevOps)` `SignedIn` as before. `accounts.json`: `version` 1 and two records, GitHub's and Azure DevOps', each with the fields step 1 read (the record goes, the others are kept, 9.3). The section, fitting (open point 2): GitHub's block signed in as before; GitLab's `AccountsSignedOutGitLab` `Not signed in.` and `AccountsSignInButtonGitLab` with the keyboard focus, no `AccountLoginGitLab`, `Sign out…`, `AccountsBusyGitLab` or `Git` row; Azure DevOps' block signed in as before. No text cut.<br>**After step 4:** `ConfirmTitle` `Sign out of Azure DevOps?`; `ConfirmQuestion` `Sign out of Azure DevOps for Fernhill-Labs? VisualCommit removes its token from this computer and no longer lists pull requests. Git over HTTPS asks your own credential helpers again.`; no `ConfirmDetail`; `Cancel` and `Sign out` in the primary look; still signed in; calls none since step 3.<br>**After step 5:** the question closed; the section as after step 3; `accounts.json` and the store unchanged; calls none. |  | n/a | `a13a-gitlab-sign-out-question`, `a13b-gitlab-signed-out`, `a13c-azure-sign-out-question` | n/a: the real window never signs out (14.4). "Signing out…" while the keychain works, and a delete the keychain refuses, are 7a's A8, the same per provider. GitLab's sign-out deletes no `avatars/` (9.4) and GitHub's is 7a's: `HostingSessionProvidersTests`' (9.10), since an empty tab gathers no avatars. Varies by nature: the status bar's git box. |
| A14 | Each switch saved alone (D216, D255, D350; design 10.3, 9.4, 9.6): GitLab's switch off and Save writes GitLab's record alone and drops web-app's GitLab binding, keeping GitHub's; then Azure DevOps' switch off and Save writes its record alone; the reopened screen shows each switch as saved; "hosting7b", tabs `web-app` and an empty one, the three signed in, dark, 1100×700 | **Method 9** (`Check_A14_each_switch_saved_alone`): 1. `ServeAsync()`. `StartWithTabs(data, [served.WebApp.Path, null], activeTab: 0, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab)`; `WaitForRepositoryAsync()`; `SignInAsync` to GitHub, GitLab and Azure DevOps (`PartA.Organization`); wait for the log line `web-app: listed 3 open pull requests of northfield/platform/web-app` and for the watchers to be quiet. Mark the calls; read `accounts.json`. 2. Click tab 1 (the empty tab). `OpenAccountsAsync()`; click `AccountUseForGitGitLab`; click `Save`; wait until the screen has closed, then until the parsed `accounts.json` shows GitLab's `useForGit` false and `web-app`'s hosting's `Bindings` hold no binding of GitLab's account. 3. `OpenAccountsAsync()`. Screenshot (`a14a-gitlab-switch-off`). 4. Turn the wheel down over `SettingsSection` until the offset stops changing; click `AccountUseForGitAzureDevOps`; click `Save`; wait until the screen has closed, then until the parsed `accounts.json` shows Azure DevOps' `useForGit` false. 5. `OpenAccountsAsync()`; turn the wheel down until the offset stops changing; assert the end. Screenshot (`a14b-azure-switch-off-end`). Press Esc. | **After step 1:** `web-app`'s hosting's `Bindings` hold a binding of `gitlab:127.0.0.1:47398:ada` for `http://127.0.0.1:47398/northfield/platform/web-app.git` and one of `github:127.0.0.1:47399:octo` for `http://127.0.0.1:47399/octo/web-app.git` (twins WP-C's, not counted), none of Azure DevOps' account; the session's `BindsGit` true; `accounts.json` three records, `useForGit` true.<br>**After step 2:** before Save, `AccountUseForGitGitLab` unchecked and nothing changed (D216: `accounts.json` as step 1 read it, the bindings as step 1's); after Save, the screen closed; `accounts.json` (waited for: the Save writes it off the UI thread after the screen has closed) GitLab's record `useForGit` false, its other fields unchanged; GitHub's and Azure DevOps' records exactly as step 1 read them; `AccountOf(GitLab)` still `SignedIn` with `UseForGit` false; `BindsGit` true; `web-app`'s `Bindings` (waited for) none of GitLab's account, GitHub's binding as step 1's; calls none (a switch touches no keychain).<br>**After step 3:** the section at offset 0: GitHub's block signed in, `AccountUseForGit` checked; GitLab's `ada`, the keychain's text, `Sign out…`, `AccountUseForGitGitLab` unchecked and enabled, `AccountUseForGitNoteGitLab` with its text; Azure DevOps' block from its label, its `Git` row not wholly inside the viewport (its top lies about at the viewport's bottom, open point 2; nothing more is asserted of its place); the welcome page of the empty tab behind the backdrop, the tab strip `web-app`, `New tab` (active); no text cut.<br>**After step 4:** after Save, `accounts.json` (waited for) Azure DevOps' record `useForGit` false, its other fields unchanged; GitLab's still `useForGit` false; GitHub's as step 1 read it; `AccountOf(AzureDevOps)` `UseForGit` false; `BindsGit` true (GitHub's switch is on); `web-app`'s `Bindings` as after step 2 (Azure DevOps' account covers none of its remotes); calls none.<br>**After step 5:** before the capture the offset is the end, above 0; in view GitHub's `AccountUseForGit` checked, `AccountUseForGitGitLab` unchecked, Azure DevOps' block whole with `AccountUseForGitAzureDevOps` unchecked and `AccountUseForGitNoteAzureDevOps` with its text; no text cut. After Esc the screen closed with nothing changed. |  | n/a | `a14a-gitlab-switch-off`, `a14b-azure-switch-off-end` | n/a: a setting at Save; the real window shows the switches on in `a02d`. The pictures are taken on the empty tab, so that they show no region of WP-P's, as 7a's A11. Another provider's event leaving a toggled switch alone is `AccountsProvidersViewModelTests`' (10.8). Varies by nature: the status bar's git box. |
| B1 | "From GitLab…" and "From Azure DevOps…" with one account (D354, D261, D302; design 11.3, 11.2): with the three providers on and none signed in, the form as today, no button and the URL box at its full width; after a sign-in through Settings, Accounts, the button names the one signed-in provider at the URL row's end and only the URL box narrows; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 1** (`Check_B1_B3_B6_B7_gitlab_sign_in_list_and_clone`): 1. `ServeAsync()`; `using var clones = new TempDirectory("clones")` before the app. `StartEmptyAsync(data, served)` (one empty tab, the three fakes, a new store); wait for the welcome page. Mark the requests and the calls. Click `CloneRepoButton`; wait until `CloneUrlBox` has the keyboard focus. Click `CloneCloseButton`. 2. `OpenAccountsAsync()`; `OpenSignInDialogAsync(GitLab)`; `TypeToken(GitLabToken)`; `ConfirmSignInAsync()`; wait until the dialog has closed. Press Esc; wait until the screen has closed (`IsClosed`). 3. Click `CloneRepoButton`; wait until `CloneUrlBox` has the focus and `CloneFromGitHubButton` is visible. Screenshot (`b01a-gitlab-button`).<br>**Method 2** (`Check_B1_B3_B6_B7_azure_sign_in_list_and_clone`): 1. `ServeAsync()`; `clones` as in method 1. `StartEmptyAsync(data, served)`; wait for the welcome page. Mark the requests and the calls. 2. `OpenAccountsAsync()`; `OpenSignInDialogAsync(AzureDevOps)`; `TypeOrganization(PartA.Organization)`; click `SignInToken`; `TypeToken(AzureDevOpsToken)`; `ConfirmSignInAsync()`; wait until the dialog has closed. Press Esc; wait until the screen has closed. 3. Click `CloneRepoButton`; wait until `CloneUrlBox` has the focus and `CloneFromGitHubButton` is visible. Screenshot (`b01b-azure-button`). | **Method 1, after step 1:** the session's `IsOn` true for the three providers and `AccountOf` null for each; the form open (part B's conventions, "The form"), its three boxes empty, the focus in `CloneUrlBox`, no error line; `CloneFromGitHubButton` not visible (`ShowsBrowseButton` false) and taking no room: `CloneUrlBox` from x 284 to 676, the edges of `CloneNameBox`, as phase 1's `13a`; requests none (D257, D350); calls none. After the click on `CloneCloseButton` the form closed and the welcome page's `Open`, `Clone` and `Init` buttons shown.<br>**After step 2:** calls exactly `probe` (the dialog's), then `save gitlab:127.0.0.1:47398:ada`; requests: GitLab's exactly one, `GET /api/v4/user` (route `user`), `Bearer`, 200, `Login` `ada`; Azure DevOps' and GitHub's none. `AccountOf(GitLab)` `SignedIn`, `Keychain`; the screen and the dialog closed; one tab, empty.<br>**After step 3:** the form open, its boxes empty, the focus in `CloneUrlBox`; `CloneFromGitHubButton` visible and enabled, `VcActionButton`, its text and its automation name `From GitLab…`, whole on one line, no tooltip; on the URL row right of `CloneUrlBox`, 6 apart, its right edge 676 and its height `CloneBrowseButton`'s; `CloneUrlBox` still from x 284, ending 6 left of the button; the URL row as tall as the parent-folder row; the parent-folder row, `CloneNameBox` and the page's other texts and controls as `13a` shows them. Requests and calls none since step 2. No text cut; no token on screen.<br>**Method 2, after step 2:** the dialog was `Add Azure DevOps account`, opened with `SignInOrganization` empty and focused (an empty tab, no account held, 10.5). Calls exactly `probe`, then `save azure-devops:127.0.0.1:47397:fernhill-labs`; requests: Azure DevOps' exactly two, in order, `GET /Fernhill-Labs/_apis/connectionData` with the query `api-version=7.1-preview.1` (route `connection:fernhill-labs`), `BasicEmptyUser`, 200, `Login` Ada's, then `GET /Fernhill-Labs/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf` with the same query (route `areas`), 200; GitLab's and GitHub's none. `AccountOf(AzureDevOps)` `SignedIn`, `Keychain`, `Login` `Fernhill-Labs`.<br>**After step 3:** as method 1's step 3 but for the text: `CloneFromGitHubButton` `From Azure DevOps…` (its text and automation name), whole, its right edge 676 and 6 right of `CloneUrlBox`, which starts at x 284 and ends further left than in `b01a` by the difference of the two buttons' widths (a wider text narrows only the URL box, 11.3); the rows below as `13a`'s. Requests and calls none since step 2. No text cut; no token on screen. |  |  | `b01a-gitlab-button`, `b01b-azure-button`; real window: none (the button's name read, no capture) | **R** (14.4, both "Sign-in, browse, clone" launches, step 2; no capture): after part A's dialog and Esc, a real click on `CloneRepoButton`, then the button's UI Automation name read: `PartB.GitLabButton` (`From GitLab…`) or `PartB.AzureDevOpsButton` (`From Azure DevOps…`). `b01a` and `b01b` are scripted only. Step 1's signed-out form with the three providers on is beyond 11.7's list (open point 11); the button gone after a refusal (D302) is B5's. Varies by nature: the URL box's caret; the status bar's git box (D248). |
| B2 | "From an account…" with two signed in, and the switch (D354, D216, D353; design 11.3, 11.4): the button's text with two and with three accounts; the browser opened on the first signed-in provider in D353's order, with a switch between the signed-in providers; one load per provider, begun when it is first shown; the filter kept and the selection cleared by a switch; an answer for the provider not shown changes nothing on screen; "hosting7b", one empty tab, GitLab and Azure DevOps signed in, then GitHub too, dark, 1100×700 | **Method 5** (`Check_B2_two_accounts_and_the_switch`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)`; `SignInAsync(GitLab, GitLabToken)`; `SignInAsync(AzureDevOps, AzureDevOpsToken, Organization)`. Click `CloneRepoButton`; wait until `CloneUrlBox` has the focus and `CloneFromGitHubButton` is visible. Screenshot (`b02a-gitlab-azure-from-an-account`). 2. Mark the requests. Click `CloneFromGitHubButton`; wait for the browser, until its load has ended and until GitLab's fake has recorded the `projects` request. With the focus in the filter, `app.Type("fork")`. Click the row `ada/web-app`. Screenshot (`b02b-gitlab-azure-switch-gitlab`). 3. Mark the requests. `served.AzureDevOps.HoldNext("repos:fernhill-labs")`. Click `CloneFromGitHubProviderAzureDevOps`; wait for the hold's `Arrived`. Screenshot (`b02c-gitlab-azure-switch-azure-loading`). 4. Click `CloneFromGitHubProviderGitLab`. Then dispose the hold; wait until Azure DevOps' fake has recorded the `repos:fernhill-labs` request, and let the window settle. 5. Click `CloneFromGitHubProviderAzureDevOps`. Screenshot (`b02d-gitlab-azure-switch-azure`). 6. Click the filter; press Ctrl+A, then Backspace. Press Esc. 7. `SignInAsync(GitHub, GitHubToken)`. Mark the requests. Click `CloneFromGitHubButton`; wait for the browser, until its load has ended and until GitHub's fake has recorded the `user/repos` request. Screenshot (`b02e-three-switch-github`). 8. Press Esc. | **After step 1:** `AccountOf(GitLab)` and `AccountOf(AzureDevOps)` `SignedIn`, `AccountOf(GitHub)` null; requests since the start the two sign-ins' alone (GitLab's `user`; Azure DevOps' `connection:fernhill-labs` and `areas`), no list. The form open, its boxes empty, the focus in `CloneUrlBox`; `CloneFromGitHubButton` `From an account…` (its text and automation name; two accounts, D354), whole, no tooltip, its right edge 676, 6 right of `CloneUrlBox`, which starts at x 284; the rows below as `13a`'s. No text cut; no token on screen.<br>**After step 2:** the browser over the form, the card 460 wide at x 320 to 780, centred vertically; `CloneFromGitHubTitle` `Clone from GitLab` and the dialog named so (GitLab is the first of the two in D353's order, `BrowseAsync(null)`); under the title the switch: `CloneFromGitHubProviderGitLab` `GitLab` in the selected look and 4 right of it `CloneFromGitHubProviderAzureDevOps` `Azure DevOps` in the plain look, each 28 high (part B's conventions, "The switch"); no `CloneFromGitHubProviderGitHub`; the filter's top 8 below the switch's bottom (open point 14); the filter `fork`; the rows exactly `ada/web-app` `Private · Fork` (the one of `PartB.GitLabRows` whose name or detail holds `fork`, ignoring case), selected (`#2A3150`); `Choose` enabled; no status, error, more or SSO line. Requests since the mark: GitLab's exactly one `GET /api/v4/projects` with the list's query, `Bearer`, 200, `Login` `ada`; Azure DevOps' none (its list waits until it is first shown, 11.4); GitHub's none. No text cut.<br>**After step 3:** the hold's `Arrived` completed: one `GET /Fernhill-Labs/_apis/git/repositories` waits at Azure DevOps' fake, not yet recorded (requests since the mark none). `CloneFromGitHubTitle` `Clone from Azure DevOps` and the dialog named so; `Azure DevOps` in the selected look, `GitLab` in the plain look; the filter still `fork` (kept); `Selected` null (cleared), `Choose` disabled; the list empty at its least height, 58; `CloneFromGitHubStatus` `Loading repositories…`; no error, more or SSO line; the card still 460 wide at x 320 to 780. No text cut.<br>**After step 4:** after the click `Clone from GitLab` with `GitLab` selected; the rows again exactly `ada/web-app` `Private · Fork` (kept), none selected, `Choose` disabled; no status line; no GitLab request since step 3's mark (one list per provider and dialog). After the release Azure DevOps' fake has recorded exactly one `GET /Fernhill-Labs/_apis/git/repositories` with the query `api-version=7.1`, `BasicEmptyUser`, 200; the window is unchanged: still `Clone from GitLab`, `GitLab` selected, its one row, no status line (an answer for a provider not shown changes nothing on screen, 11.4).<br>**After step 5:** `Clone from Azure DevOps` with `Azure DevOps` selected; the filter `fork`; the rows exactly `Fernhill-Labs/Mobile App/mobile-app-fork` `Private · Fork` (its name and its detail hold `fork`), none selected, `Choose` disabled; no status, error, more or SSO line; requests none since step 4's release (the answer kept, not asked again). No text cut.<br>**After step 6:** after the clearing the filter empty, its placeholder showing, and the four rows of `PartB.AzureDevOpsRows` in their order, none selected; after Esc the browser closed and the form open, its boxes empty (nothing chosen), `From an account…` shown.<br>**After step 7:** `CloneFromGitHubButton` still `From an account…` (three accounts), its bounds as after step 1; the browser on GitHub: `Clone from GitHub` (7a's title; GitHub is first in D353's order); the switch `GitHub` (selected), `GitLab`, `Azure DevOps`, left to right, 4 apart; the filter empty, its placeholder showing, enabled, with the focus (a new dialog); the rows exactly `octo/web-app`, without a detail; no status, error, more or SSO line; none selected, `Choose` disabled. Requests since the mark: GitHub's exactly one `GET api/v3/user/repos` with the query `per_page=100&sort=updated`, with the token, 200, `Login` `octo`; GitLab's and Azure DevOps' none (this dialog asks a provider's list only when it first shows it). No text cut.<br>**After step 8:** the browser closed; the form open, its boxes empty. |  | n/a | `b02a-gitlab-azure-from-an-account`, `b02b-gitlab-azure-switch-gitlab`, `b02c-gitlab-azure-switch-azure-loading`, `b02d-gitlab-azure-switch-azure`, `b02e-three-switch-github` | n/a: the switch pictures are not R (11.7), and `b02c` follows a held request (0.5). Step 4's late answer and step 7's three accounts are beyond 11.7's list (open point 11); the switch's rules are also `CloneFromAccountDialogViewModelTests`' (11.6). Where the keyboard focus goes after a click on the switch is not asserted (open point 14). Varies by nature: the filter's caret; the status bar's git box. |
| B3 | Each browser listed (design 11.4, 11.2, 6.5, 7.4; D354, ruling 15), in the state WP-R's launches reach: one empty tab, the provider alone signed in through Settings, Accounts, the form opened from the welcome page with empty boxes, the provider's repositories in its order with their flags, no switch, the filter focused and empty, nothing selected; "hosting7b", one empty tab, GitLab or Azure DevOps signed in, dark, 1100×700 | **Method 1**, after B1's steps 1 to 3: 4. Mark the requests and the calls. Click `CloneFromGitHubButton`; wait for the browser, until its load has ended and until GitLab's fake has recorded the `projects` request. Screenshot (`b03a-gitlab-listed`).<br>**Method 2**, after B1's steps 1 to 3: 4. The same, until Azure DevOps' fake has recorded the `repos:fernhill-labs` request. Screenshot (`b03b-azure-listed`). | **Method 1, after step 4:** the browser over the one empty tab's form, the card 460 wide at x 320 to 780, centred vertically; `CloneFromGitHubTitle` `Clone from GitLab`, the dialog and `CloneFromGitHubList` named so; no repository line; no switch (no `CloneFromGitHubProvider…` in the view: one provider, 7a's card); the filter empty, its placeholder `Filter` showing, enabled, with the keyboard focus (not `CloneUrlBox`); the rows in order, `PartB.GitLabRows`: `northfield/platform/services/billing/invoice-generation-and-reconciliation` `Private`, `ada/web-app` `Private · Fork`, `northfield/platform/web-app` `Private`; each item named by its full name, its help text its detail, its tooltip the full name, a line break and the detail; each 28 high; the first row's name cut at its start (B6); the other two names and their details whole, drawn as 7a's rows; three rows and no scroll bar; no status, error, more or SSO line (one page: no `MoreExistOf` line); none selected; `Cancel` enabled, `Choose` disabled. Requests since the mark: GitLab's exactly one `GET /api/v4/projects` with the query `membership=true&archived=false&order_by=last_activity_at&sort=desc&per_page=100` (route `projects`), `Bearer`, 200, `Login` `ada`; Azure DevOps' and GitHub's none; calls none (a list reads no keychain). Behind the backdrop the form as `b01a` shows it, but `CloneFromGitHubButton` `From GitLab…` is visible and drawn disabled: its command waits for the browser, as in 7a (7a report, "Changed in this report after its first commit", `octo/tool`'s entry); no settings screen. No text cut but the first row's two texts, which its tooltip holds; no token on screen.<br>**Method 2, after step 4:** as method 1's but for Azure DevOps: `Clone from Azure DevOps`; the rows in order, `PartB.AzureDevOpsRows`: `Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service` `Private`, `Fernhill-Labs/Mobile App/mobile-app` `Private`, `Fernhill-Labs/Mobile App/mobile-app-fork` `Private · Fork`, `Fernhill-Labs/Mobile App/sandbox` `Private` (by name, without case; harbor-co's `manifest` not listed, D356); the first row's name cut at its start, the other three whole with their details; four rows and no scroll bar; no more line (`MoreExistOf` null). Requests since the mark: Azure DevOps' exactly one `GET /Fernhill-Labs/_apis/git/repositories` with the query `api-version=7.1` (route `repos:fernhill-labs`), `BasicEmptyUser`, 200, `Login` Ada's; GitLab's and GitHub's none; calls none. Behind the backdrop the form as `b01b` shows it, but `CloneFromGitHubButton` `From Azure DevOps…` is visible and drawn disabled, as method 1's. No text cut but the first row's two texts; no token on screen. |  |  | `b03a-gitlab-listed`, `b03b-azure-listed`; real window: `b03a-gitlab-listed`, `b03b-azure-listed` | **R** (14.4, `Check_GitLab_sign_in_browse_and_clone` and Azure DevOps' "Sign-in, browse, clone" launch, step 2): after part A's dialog (compared with `a05b` or `a06b`), `ConfirmSignIn` with part A's requests alone (GitLab's `user`; Azure DevOps' `connection:fernhill-labs`, `areas`), Esc; a real click on `CloneRepoButton`; the button's name read (B1); a real click on it; wait until `CloneBrowserRowNames()` equals the full names of `PartB.GitLabRows` or `PartB.AzureDevOpsRows` in order and `CloneFromGitHubFilter` has the focus; no `CloneFromGitHubProvider…` element; compared with `b03a-gitlab-listed` or `b03b-azure-listed`. Their state owes nothing to a fake's hook or a held request: the start, Settings, the typed token (and organization), the clicks and the scenario only (0.5). How the form opens is the report's reading (open point 16). Varies by nature: the filter's caret; the status bar's git box. |
| B4 | Each browser loading (design 11.4, 11.2; D301): stacked over the form with its provider's title and no switch, the filter enabled with the focus, the list empty and `Loading repositories…`, "Choose" disabled, while the list's answer is held; "hosting7b", one empty tab, GitLab or Azure DevOps signed in, dark, 1100×700 | **Method 3** (`Check_B4_B5_gitlab_loading_and_failed`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)`; `SignInAsync(GitLab, GitLabToken)`. Click `CloneRepoButton`; wait until `CloneUrlBox` has the focus and `CloneFromGitHubButton` is visible. Mark the requests, the calls and the log. 2. `served.GitLab.HoldNext("projects")`. Click `CloneFromGitHubButton`; wait for the hold's `Arrived` and for the browser. Screenshot (`b04a-gitlab-loading`). 3. Dispose the hold; wait until the browser's load has ended and until GitLab's fake has recorded the `projects` request. Press Esc.<br>**Method 4** (`Check_B4_B5_azure_loading_and_refused`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)`; `SignInAsync(AzureDevOps, AzureDevOpsToken, Organization)`. Click `CloneRepoButton`; wait until `CloneUrlBox` has the focus and `CloneFromGitHubButton` is visible. Mark the requests, the calls and the log. 2. `served.AzureDevOps.HoldNext("repos:fernhill-labs")`. Click `CloneFromGitHubButton`; wait for the hold's `Arrived` and for the browser. Screenshot (`b04b-azure-loading`). 3. Dispose the hold; wait until the load has ended and until Azure DevOps' fake has recorded the `repos:fernhill-labs` request. Press Esc. | **Method 3, after step 1:** `From GitLab…` as `b01a` shows it; requests since the start GitLab's `user` alone.<br>**After step 2:** the browser over the form: the card 460 wide at x 320 to 780, centred vertically; `CloneFromGitHubTitle` `Clone from GitLab`; no switch; the filter empty, its placeholder showing, enabled, with the keyboard focus; the list empty at its least height, 58 inside its border; below it `CloneFromGitHubStatus` `Loading repositories…` (`settingsNote`, `#9BA3B0`, 11.5); no error, more or SSO line; `Cancel` enabled, `Choose` disabled. The hold's `Arrived` completed: one `GET /api/v4/projects` waits at GitLab's fake, not yet recorded (requests since the mark none); calls none. Behind the backdrop the form with `From GitLab…`. No text cut.<br>**After step 3:** before Esc, requests since the mark GitLab's exactly one `GET /api/v4/projects` with the list's query, `Bearer`, 200, and the rows `PartB.GitLabRows` in order, no status line; after Esc the browser closed and the form open, its boxes empty.<br>**Method 4, after steps 1 to 3:** the same for Azure DevOps: `From Azure DevOps…` as `b01b` shows it; `Clone from Azure DevOps`, no switch, the list empty at 58 and `Loading repositories…`, `Choose` disabled; one `GET /Fernhill-Labs/_apis/git/repositories` waiting at Azure DevOps' fake and none recorded; after the release exactly that request, `BasicEmptyUser`, 200, then `PartB.AzureDevOpsRows`; after Esc the form open. No text cut. |  | n/a | `b04a-gitlab-loading`, `b04b-azure-loading` | n/a: a held request (0.5). A filter typed while loading is 7a's B4 and `CloneFromAccountDialogViewModelTests`' (11.6). Varies by nature: the filter's caret; the status bar's git box. |
| B5 | Each browser failed in its provider's words (design 11.2, 11.4, 9.6; D338, D302, D330): GitLab's server error in `HostingTexts.Describe`'s words, no rows, the account kept; Azure DevOps' refusal of the token naming the organization, the account needing sign-in at once and the button gone from the form; each failure logged once by the browser; "hosting7b", one empty tab, GitLab or Azure DevOps signed in, dark, 1100×700 | **Method 3**, after B4's steps 1 to 3: 4. Mark the requests, the calls and the log. `served.GitLab.FailNext("projects", FakeGitLabFault.ServerError)`. Click `CloneFromGitHubButton`; wait until `CloneFromGitHubError` shows and GitLab's fake has recorded the request. Screenshot (`b05a-gitlab-failed`). 5. Press Esc.<br>**Method 4**, after B4's steps 1 to 3: 4. Mark the requests, the calls and the log. `served.AzureDevOps.FailNext("repos:fernhill-labs", FakeAzureDevOpsFault.InvalidToken)`. Click `CloneFromGitHubButton`; wait until `CloneFromGitHubError` shows, for `WaitForAccountAsync(AzureDevOps, a => a?.State == AccountState.NeedsSignIn)`, until the parsed `accounts.json` shows Azure DevOps' `tokenState` `refused` (part A's conventions) and until `CloneFromGitHubButton` is not visible. Screenshot (`b05b-azure-refused`). 5. Press Esc. | **Method 3, after step 4:** requests since the mark GitLab's exactly one `GET /api/v4/projects` with the list's query, `Bearer`, 500; the others none. `CloneFromGitHubError` `GitLab had a problem. Try again later.` (`dialogError`, `#E5606B`) below the list, which is empty at 58; no status, more or SSO line; the filter enabled with the focus; `Cancel` enabled, `Choose` disabled. `AccountOf(GitLab)` still `SignedIn` (only a 401 marks it); calls none. The log since the mark, `{id}` the failed answer's `x-request-id`: exactly one line that begins `Listing GitLab repositories failed:` (11.2, once per failure), the Warning `Listing GitLab repositories failed: ServerError: 500 Internal Server Error (request {id})`; besides it, once each, the core's Warning `GitLab: GET /api/v4/projects failed: 500 (request {id}).` and the session's Warning `GitLab: listing the account's repositories failed: ServerError, 500 Internal Server Error (request {id})`; no line that begins `Listing GitHub repositories failed:`. Behind the backdrop the form with `From GitLab…`. No text cut.<br>**After step 5:** the browser closed; the form open, its boxes empty, `From GitLab…` shown; requests none since step 4's.<br>**Method 4, after step 4:** requests since the mark Azure DevOps' exactly one `GET /Fernhill-Labs/_apis/git/repositories` with the query `api-version=7.1`, `BasicEmptyUser`, 401, and no `connection:fernhill-labs` after it (the API's 401 marks the account at once, 9.6, as 7a's D330); the others none. `CloneFromGitHubError` `Azure DevOps refused the token of Fernhill-Labs. Sign in to Azure DevOps in Settings, Accounts.` (the account's login: the organization in the server's spelling); no rows; no status, more or SSO line. `AccountOf(AzureDevOps)` `NeedsSignIn`, note kind `Refused`, `Login` `Fernhill-Labs`; `accounts.json` Azure DevOps' record with `tokenState` `refused`, `useForGit` true and its other fields unchanged; calls none (the entry stays: `Holds(azure-devops:127.0.0.1:47397:fernhill-labs)` true). The log since the mark, `{id}` the refused answer's `ActivityId`: exactly one line that begins `Listing Azure DevOps repositories failed:`, the Warning `Listing Azure DevOps repositories failed: BadCredentials: Unauthorized (request {id})`; besides it, once each, the core's Warning `Azure DevOps: GET /Fernhill-Labs/_apis/git/repositories failed: 401 (request {id}).` and the session's Warning `Azure DevOps account Fernhill-Labs on 127.0.0.1:47397: Azure DevOps refused the token (the API, request {id}); it needs signing in again.`; no line that begins `Azure DevOps: listing the account's repositories failed:`. Behind the backdrop the form without the button (D302: needing sign-in counts as none): `CloneUrlBox` again from x 284 to 676. No text cut.<br>**After step 5:** the browser closed; the form open, its boxes empty, no button; requests none since step 4's. |  | n/a | `b05a-gitlab-failed`, `b05b-azure-refused` | n/a: faults (`FailNext`, 0.5). One failure per provider, each on another path of 11.2's table (open point 13): GitLab's `ServerError` keeps the account; Azure DevOps' 401 is D302's case for a new provider. Each fault answers the list's one request. Varies by nature: the filter's caret; the status bar's git box. |
| B6 | The two long names cut at their start and whole in their tooltips (design 11.4, 11.7; ruling 3): `northfield/platform/services/billing/invoice-generation-and-reconciliation` and `Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service` keep the repository's name and lose the start of their path; the row's tooltip holds the full name and its detail; the names that fit draw as before; "hosting7b", one empty tab, GitLab or Azure DevOps signed in, dark, 1100×700 | **Method 1**, after B3's step 4: 5. Before the hover, read the first row's name `TextBlock` and its detail. Rest the pointer on the row `northfield/platform/services/billing/invoice-generation-and-reconciliation` (`RestPointerOnAsync` on its `CloneFromGitHubItem`); wait until its tooltip is open (`WaitForToolTipAsync`). Screenshot (`b06a-gitlab-long-name-tip`, the pointer kept). Then move the pointer to the neutral point.<br>**Method 2**, after B3's step 4: 5. The same on the row `Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service`. Screenshot (`b06b-azure-long-name-tip`, the pointer kept). Then move the pointer to the neutral point. | **Method 1, after step 5, before the hover:** the first row's name `TextBlock` has `TextTrimming` `LeadingCharacterEllipsis`; its layout is one line, collapsed (`HasCollapsed`); the text it draws begins with `…` and ends with `/invoice-generation-and-reconciliation`, the repository's name whole (11.4); its width is the row's but for at most the 12 gap; its detail `Private` has no room beside it and is not drawn whole (open point 12); the item's automation name is still the whole full name. The other two rows' names are not collapsed. **After the hover:** the row in the hover look (`#2F3542`), not selected, `Choose` disabled; its tooltip open and faded in, its text exactly `northfield/platform/services/billing/invoice-generation-and-reconciliation`, a line break and `Private` (`TipText`), shown whole, wrapped where the tooltip's width needs and nowhere cut, the tooltip whole inside the window; the other rows as in `b03a`; requests none since step 4's. `LayoutAudit`: no text cut but the first row's name and detail, which its tooltip holds.<br>**Method 2, after step 5:** the same for `Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service`: one collapsed line that begins with `…` and ends with `/customer-onboarding-and-identity-verification-service`; its detail `Private` without room; after the hover its tooltip `Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service`, a line break and `Private`; the other three rows' names not collapsed; requests none since step 4's. |  | n/a | `b06a-gitlab-long-name-tip`, `b06b-azure-long-name-tip` | n/a: a tooltip held by the resting pointer, after the R picture (14.4 compares the list, not a hover); `b03a` and `b03b` show the cut without a tooltip. Varies by nature: the filter's caret; the status bar's git box. |
| B7 | Choose, then Clone of GitLab's `northfield/platform/web-app` (a nested path) and of Azure DevOps' `mobile-app` (`Fernhill-Labs@`, `%20`) through the app's helper (design 11.1, 11.4, 8.2, 8.6; D350, D331, D277, D255, ruling 15; O4, R3): the plain GitLab URL, and Azure DevOps' URL with its organization as the user, filled in with the folder name; each clone asks nothing, takes the account's token as `x-access-token` through the app's helper and writes no credential into the clone; the token in no file and no git call; no picture of a clone's tab (0.4); "hosting7b", one empty tab, GitLab or Azure DevOps signed in through Settings, a `clones` folder, dark, 1100×700 | **Method 1**, after B6's step 5: 6. Mark the requests; take a quiet call mark (`QuietCallMarkAsync()`). Click the row `northfield/platform/web-app`; click `Choose`; wait until the browser has closed. Screenshot (`b07a-gitlab-web-app-chosen`). 7. Click `CloneParentBox`; `app.Type(clones.Path)` (typed, as the real window does). 8. Mark the requests, each fake's `GitRequests`, the calls and the log; take a quiet call mark. Click `CloneStartButton`. Wait until the tab shows the repository (`WaitForRepositoryAsync()`), for `web-app: listed 3 open pull requests of northfield/platform/web-app` in the log since the mark and for the watchers to be quiet. 9. Close the app. In `{clones}/web-app`, through `GitRunner` with `TempRepo.IsolatedEnvironment` (as 7a's B6): `git config --get remote.origin.url`; `git config --get-regexp ^credential`; `git config --local --list`; `git symbolic-ref HEAD`; `git rev-parse HEAD`; `git status --porcelain`; and in the copy's bare `gitlab/northfield/platform/web-app.git` `git rev-parse refs/heads/main`.<br>**Method 2**, after B6's step 5: 6. to 9. The same with the row `Fernhill-Labs/Mobile App/mobile-app` (screenshot `b07b-azure-mobile-app-chosen`), D322's `mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app`, the clone `{clones}/mobile-app` and the bare `azure-devops/Fernhill-Labs/Mobile App/_git/mobile-app`. | **Method 1, after step 6:** after the click `northfield/platform/web-app` selected (`#2A3150`) and `Choose` enabled; then the browser closed and the form: `CloneUrlBox`'s text `http://127.0.0.1:47398/northfield/platform/web-app.git` (`PartB.GitLabCloneUrl`, `WebAppUrl` through `WithPorts`: the fake's `http_url_to_repo`, no user part); `CloneNameBox` `web-app` (from the URL, 7a design 9.3); `CloneParentBox` empty; no error line; `From GitLab…` shown and enabled; requests none since the mark; git calls none since the quiet mark (choosing runs no git). What part of the URL the box shows is not asserted (open point 15). No text cut; no token on screen.<br>**After step 8:** at no time a credentials dialog or any other prompt: the log since the mark has no line that begins `web-app: Clone: git asks (`, and no dialog is open. Git calls since the quiet mark, in this order (`AssertCalledInOrder`, presence and order): `git config --show-scope --show-origin -z --list` in the data folder (the read of the user's `url.` rules before a bound clone, D331), then `git clone --progress -- http://127.0.0.1:47398/northfield/platform/web-app.git web-app` in `{clones}`. The log since the mark holds `web-app: Clone: git's credential get for 127.0.0.1:47398 answered from the account.`; no `web-app: Clone: git's credential` line ending `is not bound.`, `names another user.` or `has no token.`; no `refused the account's token`; no Warning that holds `git's configuration could not be read`. GitLab's git requests since the mark on paths under `/northfield/platform/web-app.git/`: at least one with `GivenUserName` `x-access-token` answered 200 (the account's token through the app's helper, D255, D350); from git 2.46 on the first of them so, below 2.46 the first without credentials, 401 (8.6); none with another user name; none with credentials answered 401. Azure DevOps' and GitHub's git requests none. API requests since the mark: no `user` and no `projects`; GitLab's are the new tab's refresh, `project:northfield/platform/web-app` and `mrs:northfield/platform/web-app` among them, each `Bearer`, 200; none answered 401; Azure DevOps' and GitHub's none (the clone has `origin` alone). Calls since the mark none (the token is in memory). The window: one tab, `web-app`, showing `{clones}/web-app` (the empty tab took the clone); the form closed; the recent list holds it; its hosting's listed repository GitLab's `northfield/platform/web-app`, its `Bindings` exactly one `CredentialBinding`, `Url` `http://127.0.0.1:47398/northfield/platform/web-app.git` (no `.git` twin for a URL that ends in `.git`, 8.2 step 4), account `gitlab:127.0.0.1:47398:ada`; `AccountOf(GitLab)` `SignedIn`, `Keychain`, `None`. Nothing of the tab is pictured.<br>**After step 9:** `remote.origin.url` `http://127.0.0.1:47398/northfield/platform/web-app.git` (step 6's); `--get-regexp ^credential` exits 1 and prints nothing; the keys of `--local --list` other than `core.*` exactly `remote.origin.url`, `remote.origin.fetch` (`+refs/heads/*:refs/remotes/origin/*`), `branch.main.remote` (`origin`) and `branch.main.merge` (`refs/heads/main`): nothing of the scope's `GIT_CONFIG_COUNT` pairs, no `http.`, `url.` or `credential.` key; `HEAD` `refs/heads/main`, at the bare's `refs/heads/main` ("Add the docs folder", 12:22); the working tree clean. `accounts.json` one record, GitLab's, `tokenState` `keychain`, `useForGit` true. Part B's end (its conventions): the tripwire for `GitLabToken` passes over `{clones}` with `.git`, the data folder (the log and `recent.json` included) and the call log (the clone's command, output and error included).<br>**Method 2, after step 6:** `CloneUrlBox`'s text `http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app` (`PartB.AzureDevOpsCloneUrl`, `MobileAppUrl` through `WithPorts`: the fake's `remoteUrl` with its `Fernhill-Labs@` kept, ruling 15, and `%20`); `CloneNameBox` `mobile-app`; the rest as method 1's, with `From Azure DevOps…`.<br>**After step 8:** as method 1's, with: the clone's call `git clone --progress -- http://***@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app mobile-app` in `{clones}` (the call record hides a URL's user part, `GitCommand.HideCredentials`; git gets the URL as chosen); the log's `mobile-app: Clone: git's credential get for 127.0.0.1:47397 answered from the account.` and no line that begins `mobile-app: Clone: git asks (`; Azure DevOps' git requests since the mark on paths under `/Fernhill-Labs/Mobile%20App/_git/mobile-app/`, in place of method 1's git requests: at least one with `GivenUserName` `x-access-token` answered 200; from 2.46 on the first of them so, below 2.46 the first without credentials (`GivenUserName` null), 401, the second with `GivenUserName` `Fernhill-Labs` (the URL's user with an empty password: libcurl's own retry, H2, r7 H4b), 401, and the third, the helper's answer, with `x-access-token`, 200; none answered 200 with a user name other than `x-access-token`; none with `x-access-token` answered 401; none on harbor-co's `manifest`; GitLab's and GitHub's git requests none. API requests: no `connection:` and no `repos:`; Azure DevOps' are the new tab's refresh, `repo:fernhill-labs/mobile app/mobile-app` and `pulls:fernhill-labs/mobile app/mobile-app` among them, each `BasicEmptyUser`, 200; none answered 401; GitLab's and GitHub's none. The window: one tab, `mobile-app`, showing `{clones}/mobile-app`; its listed repository `Fernhill-Labs/Mobile App/mobile-app`; its `Bindings` exactly one, `Url` `http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app` (git's URL without its user, `CredentialBinding`'s rule), account `azure-devops:127.0.0.1:47397:fernhill-labs`, `SharedOriginOwner` `fernhill-labs` (8.2 step 6: the coverage's `Organization`, the account key's lower-cased login).<br>**After step 9:** `remote.origin.url` `http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app` (as chosen, `Fernhill-Labs@` and `%20` kept); `--get-regexp ^credential` exits 1; the keys of `--local --list` as method 1's; `HEAD` `refs/heads/main` at the bare's `refs/heads/main` ("Add the docs folder", 12:02); the working tree clean. `accounts.json` one record, Azure DevOps', `tokenState` `keychain`. The tripwire for `AzureDevOpsToken` as method 1's. |  |  | `b07a-gitlab-web-app-chosen`, `b07b-azure-mobile-app-chosen`; real window: none (no capture of a clone's tab) | **R** (14.4 steps 3 and 4 of both "Sign-in, browse, clone" launches): a real click on the row (`PartB.GitLabChosen` `northfield/platform/web-app`, `PartB.AzureDevOpsChosen` `Fernhill-Labs/Mobile App/mobile-app`), then on `Choose`; `CloneUrlBox`'s Value `WithPorts(PartB.GitLabCloneUrl)` or `WithPorts(PartB.AzureDevOpsCloneUrl)`, with its `Fernhill-Labs@`; `CloneNameBox` `web-app` or `mobile-app`; `ReplaceText("CloneParentBox", clones.Path)` (a `TempDirectory` made before the app); the fake's `GitRequests` marked; a real click on `Clone`; `Opened` within 60 s (a time-out says whether `CredentialsDialog` shows); no `CredentialsDialog`; a git request answered 200 with `GivenUserName` `x-access-token` and none answered 200 with another; run.txt names the helper's path, which Git for Windows' `sh` runs, and whether it holds a space; after `app.Close()`, in the clone, `remote.origin.url` step 3's URL and `git config --get-regexp ^credential` exiting 1; `AssertCleanEnd(app, served, clones.Path)`. The real window waits only for `Opened`; the scripted check also waits for D322's line, so that the close cuts no request of the clone's tab. `b07a` and `b07b` are scripted only; the temporary folder's name is in no picture (step 7 comes after them). Where the focus returns when the browser closes is not asserted (D104). Varies by nature: a caret where the focus returned; the status bar's git box. |
| B8 | The palette's three commands, one signed out signing in first (D354, D299, D300, D351; design 11.5, 10.5): `Clone from GitHub…`, `Clone from GitLab…` and `Clone from Azure DevOps…` right after `Clone repository…`, without keys; GitLab's, signed in, asks nothing and opens its browser; Azure DevOps', signed out, opens Azure DevOps' dialog over the window first, then the browser on Azure DevOps with the switch; GitHub's, signed out and cancelled, changes nothing; no settings screen at any time; "hosting7b", one empty tab, GitLab signed in, dark, 1100×700 | **Method 6** (`Check_B8_palette_three_commands`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)`; `SignInAsync(GitLab, GitLabToken)`. Mark the requests and the calls. 2. `PressPalette()`; with the box empty read the palette's shown entries (`Palette.Shown`, not only the realised rows). `TypeInPalette("Clone from")`; read `PaletteSnapshot()`. Screenshot (`b08a-three-palette-commands`). 3. Press Esc. `PressPalette()`; `TypeInPalette("Clone from GitLab")`; press Enter. Wait for the browser, until its load has ended and until GitLab's fake has recorded the `projects` request. Press Esc. 4. Mark the requests and the calls. `PressPalette()`; `TypeInPalette("Clone from Azure DevOps")`; press Enter; wait for `SignInDialog` with the focus in `SignInOrganization`. Screenshot (`b08b-azure-sign-in-first`). 5. `TypeOrganization(PartA.Organization)`; click `SignInToken`; `TypeToken(AzureDevOpsToken)`; `ConfirmSignInAsync()`. Wait for the browser, until its load has ended and until Azure DevOps' fake has recorded the `repos:fernhill-labs` request. Screenshot (`b08c-gitlab-azure-palette-browser`). 6. Press Esc. Mark the requests and the calls. `PressPalette()`; `TypeInPalette("Clone from GitHub")`; press Enter; wait for `SignInDialog`. Click its `Cancel`. | **After step 2:** with the box empty the palette's commands hold `Clone repository…`, `Clone from GitHub…`, `Clone from GitLab…`, `Clone from Azure DevOps…`, `Init repository…` one after another (11.5: the two after GitHub's, in D353's order), the three `Clone from …` enabled, with no detail and no key; after the typing exactly three rows, `Clone from GitHub…` (highlighted), `Clone from GitLab…`, `Clone from Azure DevOps…`, each enabled, no detail, no key (one tier, enabled, none run yet: the commands' order, phase 6's 5.6); the card phase 6's (`paletteCard`): 600 wide at the top centre, x 250 to 850, from y 96, 140 high with three rows (84 with one, 28 a row more, phase 6's 5.3). Requests none; calls none. No text cut; no token on screen.<br>**After step 3:** after Enter the palette closed; no dialog (GitLab is signed in: nothing asked, 11.5); the form open on the one empty tab and the browser over it: `Clone from GitLab`, no switch (GitLab alone signed in), the filter empty with the focus, `PartB.GitLabRows`; requests since the mark GitLab's exactly one `projects`, `Bearer`, 200; calls none; no settings screen. After Esc the browser closed and the form still open, its boxes empty (D300).<br>**After step 4:** the palette closed; `SignInDialog` `Add Azure DevOps account` stacked over the window as part A's A6 step 2 gives it (`a06a`): the help, `Create a token on Azure DevOps` disabled in the disabled look, `Organization` with `SignInOrganization` empty and the keyboard focus (an empty tab, no account held, 10.5), `Token` empty, no `SignInNoKeychain`, `Cancel` enabled, `Sign in` disabled; behind it the empty tab's welcome page with the form open (step 3's); one tab; no settings screen (the window's entry, 10.5). Calls none (the start's one probe was GitLab's sign-in's, 9.4); requests none. No text cut.<br>**After step 5:** the dialog closed; requests since step 4's mark: Azure DevOps' `GET /Fernhill-Labs/_apis/connectionData` (route `connection:fernhill-labs`), `BasicEmptyUser`, 200, `Login` Ada's, then `areas`, 200, then exactly one `GET /Fernhill-Labs/_apis/git/repositories` (route `repos:fernhill-labs`), `BasicEmptyUser`, 200; GitLab's none (this dialog asks GitLab's list only when it shows it, 11.4); GitHub's none. Calls exactly `save azure-devops:127.0.0.1:47397:fernhill-labs`. `AccountOf(AzureDevOps)` `SignedIn`, `Keychain`; `accounts.json` two records, GitLab's and Azure DevOps', each `tokenState` `keychain`. The browser over the same tab's form: `Clone from Azure DevOps` (the command's provider, though GitLab comes first in D353's order: 11.4's `provider ?? providers[0]`); the switch `GitLab` (plain look) and `Azure DevOps` (selected), 4 apart; the filter empty with the focus; `PartB.AzureDevOpsRows` in order, the first cut as B6's; none selected, `Choose` disabled; still one tab; no settings screen at any time. Behind the backdrop the form with `From an account…` (two accounts). No text cut but the first row's two texts; no token on screen.<br>**After step 6:** after Enter `SignInDialog` `Add GitHub account` (7a's, the focus in `SignInToken`, part A's A2 step 3) over the window; after its `Cancel` the dialog closed and no dialog open; `AccountOf(GitHub)` null; still one tab, empty, its form open (step 6's Esc left it so), no browser (a cancelled sign-in stops there, D300, 11.5); requests none since the mark; calls none; `accounts.json` the same two records. |  | n/a | `b08a-three-palette-commands`, `b08b-azure-sign-in-first`, `b08c-gitlab-azure-palette-browser` | n/a: the real window reaches each browser from the form's button (14.4); 7a's real window repeats GitHub's palette path with the new providers off. The order of the runs is the report's (open point 18). Each command listed with its fake alone, and a sign-in that answers false, are `CloneFromAccountTests`' (11.6). Varies by nature: the palette box's caret (`b08a`), the organization box's caret (`b08b`), the filter's caret (`b08c`); the status bar's git box. |
| P1 | Each provider's section with its rows and the badge in view (D351, D355, D362; design 12.3, 12.6, 6.6, 7.4, 0.5; ruling 24; R): `web-app`'s `Merge requests` with !23, !22 and !21 newest first, each with `!n`, its title and its author, !22 a draft and !23 from the fork `ada/web-app`; `mobile-app`'s `Pull requests` with !13, !12 and !11, !12 a draft and !13 from the fork `mobile-app-fork`; `!21` and `!11` after the request's icon in the local branch's label, `!22` and `!12` on the drafts' remote branches, one badge per request; the labels' tooltips naming the request; signed in through Settings, Accounts as the real window does, in the prepared state; "hosting7b", one tab on the prepared `web-app`, then one on the prepared `mobile-app`, dark, 1100×700 | **Method 1** (`Check_P1_P10_P12_P13_P14_P7_gitlab_rows_badge_menus_and_filter`): 1. `ServeAsync()`. `Phase7bExpectations.PrepareRequestsAsync(served.MobileApp, GitLab)`. `OpenAsync(data, served, WebApp, signIn: [])` (one tab on `web-app`, signed out, a new store). Mark the requests, the calls and the log. `OpenAccountsAsync()`; `OpenSignInDialogAsync(GitLab)`; `TypeToken(GitLabToken)`; press Enter; wait until the dialog has closed and `WaitForAccountAsync(GitLab, …)` sees it signed in; press Esc; wait for the screen's `IsClosed`. Wait for `web-app: listed 3 open pull requests of northfield/platform/web-app` since the mark and for the watchers to be quiet. Screenshot (`p01a-gitlab-rows`). 6. (after P10's steps 2 and 3, P12's step 4 and P13's step 5) Rest the pointer at the centre of row 3's first label (`feature/search`) until the graph's tooltip shows. Screenshot (`p01b-gitlab-badge-tooltip`, the pointer kept). Rest it at the centre of row 2's label (`origin/docs/readme`) until the tooltip shows; read it.<br>**Method 2** (`Check_P1_P10_P12_P13_P14_azure_rows_badge_and_menus`): 1. `ServeAsync()`. `PrepareRequestsAsync(served.MobileApp, AzureDevOps)`. `OpenAsync(data, served, signIn: [])` (one tab on `mobile-app`, signed out, a new store). Mark the requests, the calls and the log. `OpenAccountsAsync()`; `OpenSignInDialogAsync(AzureDevOps)` (the organization prefilled, the focus in `SignInToken`); `TypeToken(AzureDevOpsToken)`; press Enter; wait until the dialog has closed and the account is signed in; press Esc; wait for `IsClosed`. Wait for `mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app` since the mark and for the watchers to be quiet. Screenshot (`p01c-azure-rows`). 6. (after P10's, P12's and P13's steps) Rest the pointer at the centre of row 3's first label (`feature/sign-in`) until the graph's tooltip shows; read it. Rest it on row 2's label (`origin/docs/guide`); read it. | **Method 1, after step 1:** requests since the mark: GitLab's, in this order, `GET /api/v4/user` (route `user`) 200; `GET /api/v4/projects/northfield%2Fplatform%2Fweb-app` (route `project:northfield/platform/web-app`, A3) 200; `GET /api/v4/projects/northfield%2Fplatform%2Fweb-app/merge_requests` with the query `state=opened&per_page=100` (route `mrs:northfield/platform/web-app`) 200; `GET /api/v4/projects/<ada/web-app's number>` (route `project:ada/web-app`, `!23`'s fork) 200; each `Bearer`, `Login` `ada`; nothing else; GitHub's none (`web-app`'s `upstream`, no GitHub account, D257) and Azure DevOps' none. Calls exactly `probe`, `save gitlab:127.0.0.1:47398:ada`. The screen closed, still signed in (D256). The hosting `Loaded`: `Repository` (GitLab, `127.0.0.1:47398`, `northfield/platform`, `web-app`); `RequestRemotes` exactly `origin`; `List` !23, !22, !21 as part P's scenario facts give them (`HeadRef`, `HeadRepository`, `BaseRef` `main`, `HtmlUrl`, `Author`, `IsDraft`), `MoreExist` false; `ByRef` exactly the facts' three keys. The section: the chevron down, `Merge requests` `3`; three `PullRequestItem` rows in order, `!23 Fix a typo in the footer` (`ada`), `!22 Draft: Update the readme` (`Draft`, `octo`), `!21 Add the search box` (`ada`), each `PullRequestNumber` `!23`, `!22`, `!21`, `PullRequestDraft` shown in `!22`'s alone; the tooltips as part P's texts give them (`!23`'s second line `ada · ada:fix-typo → main`); none selected; no message row; each row whole in the panel. `Local branches` holds `feature`, `local-work`, `main` and the prepared branch's folder `topic`, closed. The graph: the labels as part P's scenario facts' table gives them for `web-app`; row 3's first label `feature/search` (`LocalBranch`, `FullText` `feature/search`) with `Badge` (21, `Add the search box`, false, GitLab), drawn `!21` whole after the icon inside the label; `origin/feature/search` on the same row, shown or folded into `+1` as the column's width decides, with no badge of its own; row 2's `origin/docs/readme` with (22, `Draft: Update the readme`, true, GitLab), drawn `!22`; no `Badge` on rows 0, 1 and 4 (`upstream/main`: GitHub's remote is not listed); every label inside the 130-px column; no avatar (D349). No dialog; no text cut but by an ellipsis whose whole text a tooltip holds; no token on screen.<br>**Method 1, after step 6:** the graph's tooltip exactly `feature/search (merge request !21: Add the search box)`, a line break, `origin/feature/search (merge request !21: Add the search box)` (12.6's suffix on each of the row's labels, one per line, as 7a's G2 step 6 lists a row's); the label and its badge as before under the pointer. Row 2's tooltip exactly `origin/docs/readme (draft merge request !22: Draft: Update the readme)`. Requests none since step 1.<br>**Method 2, after step 1:** requests since the mark: Azure DevOps', in this order, `GET /Fernhill-Labs/_apis/connectionData` (route `connection:fernhill-labs`, the prefilled organization sent as it reads), query `api-version=7.1-preview.1`, 200, `Login` Ada's; `areas` 200; `GET /Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app`, query `api-version=7.1` (route `repo:fernhill-labs/mobile app/mobile-app`, A3), 200; `GET /Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests`, query `searchCriteria.status=active&api-version=7.1&$top=100&$skip=0` (route `pulls:fernhill-labs/mobile app/mobile-app`), 200 (one page: no `$top=1` probe); each `BasicEmptyUser`; nothing else; GitLab's and GitHub's none. Calls exactly `probe`, `save azure-devops:127.0.0.1:47397:fernhill-labs`. The hosting `Loaded`: `Repository` (Azure DevOps, `127.0.0.1:47397`, `Fernhill-Labs/Mobile App`, `mobile-app`); `RequestRemotes` exactly `origin`; `List` !13, !12, !11 as the facts give them, `MoreExist` false; `ByRef` exactly the facts' three keys. The section: `Pull requests` `3`; `!13 Fix a typo in the greeting` (`Ada Lovelace`), `!12 Write the guide` (`Draft`, `Octo Cat`), `!11 Add the sign-in page` (`Ada Lovelace`), `PullRequestDraft` in `!12`'s alone; the tooltips as part P's texts give them (`!13`'s second line `Ada Lovelace · mobile-app-fork:fix-typo → main`); none selected; no message row. The graph: row 3's `feature/sign-in` with (11, `Add the sign-in page`, false, AzureDevOps), drawn `!11`, `origin/feature/sign-in` without a badge of its own; row 2's `origin/docs/guide` with (12, `Write the guide`, true, AzureDevOps), drawn `!12`; no `Badge` on rows 0, 1 and 4; every label inside the column. No text cut but by an ellipsis whose whole text a tooltip holds; no token on screen.<br>**Method 2, after step 6:** row 3's tooltip exactly `feature/sign-in (pull request !11: Add the sign-in page)`, a line break, `origin/feature/sign-in (pull request !11: Add the sign-in page)`; row 2's exactly `origin/docs/guide (draft pull request !12: Write the guide)`. Requests none since step 1. |  |  | `p01a-gitlab-rows`, `p01b-gitlab-badge-tooltip`, `p01c-azure-rows`; real window: `p01a-gitlab-rows`, `p01c-azure-rows` | **R** (14.4, `Check_GitLab_requests_and_create` and Azure DevOps' "Requests and create" launch, step 1): `PrepareRequestsAsync(copy, provider)` before the launch on `web-app` or `mobile-app`; `SignInThroughSettings` with the scenario's token (Azure DevOps' organization prefilled from the tab, ruling 16), login `ada` or `Fernhill-Labs`; `WaitForListed` (D322's line alone, D349); each fake's requests since the launch as step 1 gives them, route keys in order (`user`, `project:northfield/platform/web-app`, `mrs:northfield/platform/web-app`, `project:ada/web-app`; `connection:fernhill-labs`, `areas`, `repo:fernhill-labs/mobile app/mobile-app`, `pulls:fernhill-labs/mobile app/mobile-app`), GitHub's fake none; `PullRequestRowNames()` equals `PartP.GitLabRows`' or `PartP.AzureDevOpsRows`' names in order, `PullRequestDraft` on `!22`'s or `!12`'s row alone; compared with `p01a-gitlab-rows` or `p01c-azure-rows`, the badge in it. Their state owes nothing to a hook or a held request (0.5; open point 28). `p01b` and the tooltips are scripted only. Which titles are cut at 260 and where each label's name is cut are inspected (open point 26); the fork labels read as 16.3 asks (open point 27). Varies by nature: the status bar's git box (D248). |
| P2 | Signed out (D257, D350, D351; design 12.3, 12.2, 10.5; ruling 24): each section open with `0` and one clickable row naming its provider, `Sign in to GitLab to see merge requests…` under `Merge requests`, `Sign in to Azure DevOps to see pull requests…` under `Pull requests`; no request and no URL-rewrites read; the row opens that provider's dialog over the window, Azure DevOps' prefilled with the tab's organization; "hosting7b", one tab on `web-app`; tabs `mobile-app` and `manifest`; signed out, dark, 1100×700 | **Method 3** (`Check_P2_P10_P13_P4_P3_gitlab_signed_out_failed_and_loading`): 1. `ServeAsync()`. `OpenAsync(data, served, WebApp, signIn: [])` (a new store); wait for the hosting's `PullRequests` `SignedOut`, then for the section's message row (part P's conventions); `WaitForWatchersQuietAsync()`. Screenshot (`p02a-gitlab-signed-out`). 4. (after P10's step 2 and P13's step 3) Mark the requests and the calls. Click the message row; wait for `SignInDialog` with the focus in `SignInToken`.<br>**Method 4** (`Check_P2_P10_P13_P4_P3_P8_azure_signed_out_failed_loading_and_other_organization`): 1. `ServeAsync()`. `ShellDriver.StartWithTabs(data, [served.MobileApp.Path, served.Manifest.Path], activeTab: 0, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab)` (a new store); `WaitForRepositoryAsync()`; wait for `mobile-app`'s `PullRequests` `SignedOut`, then for its message row; `WaitForWatchersQuietAsync()`. Screenshot (`p02b-azure-signed-out`). 4. (after P10's step 2 and P13's step 3) Mark the requests and the calls. Click the message row; wait for `SignInDialog` with the focus in `SignInToken`. | **Method 3, after step 1:** `AccountOf` null for each provider; requests since the start none in any fake (D257: GitHub's `upstream` neither); the call log holds no `git config --null --get-regexp ^url\.` (9.9: the rewrites are read only while an account is signed in). The hosting: `SignedOut`, `Repository` GitLab's `northfield/platform/web-app`, `List` null, `ByRef` and `Bindings` empty. The section: the chevron down, `Merge requests` `0` (titled by the listed repository's provider signed out too, ruling 24); one `PullRequestMessage` `Sign in to GitLab to see merge requests…` (secondary; cut with "…" at the default 260, where its 40 characters, about 6 px each, are wider than the row's room of about 205 px from x 42; its tooltip its whole text; where it is cut is inspected); no `PullRequestItem`; no `!n` on a branch row; no badge in the graph; no text cut but by an ellipsis.<br>**Method 3, after step 4:** `SignInDialog` `Add GitLab account` as part A's conventions give it (A5's look), over `web-app`'s window (the window's entry, 10.5; no settings screen), `SignInToken` empty with the focus; calls `probe`; requests none; the section unchanged.<br>**Method 4, after step 1:** the tab strip `mobile-app`, `manifest`; requests since the start none in any fake; no URL-rewrites read. `mobile-app`'s hosting `SignedOut`, `Repository` (Azure DevOps, `127.0.0.1:47397`, `Fernhill-Labs/Mobile App`, `mobile-app`), `List` null, `ByRef` and `Bindings` empty. The section: `Pull requests` `0`; one `PullRequestMessage` `Sign in to Azure DevOps to see pull requests…` (cut with "…" at 260, its 45 characters; its tooltip whole); no row; no `!n`; no badge; no text cut but by an ellipsis.<br>**Method 4, after step 4:** `SignInDialog` `Add Azure DevOps account` over `mobile-app`'s window, `SignInOrganization` `Fernhill-Labs` (the `OwnerRoot` of the tab's origin, 10.5), the focus in `SignInToken`, `SignInTokenLink` enabled, `Sign in` disabled (no token); calls `probe`; requests none. |  | n/a | `p02a-gitlab-signed-out`, `p02b-azure-signed-out` | n/a: 14.4 signs in before its first capture. Part A's `a06c` shows Azure DevOps' signed-out row behind its backdrop from WP-P's merge ("Changes to expected results", part P). Varies by nature: the status bar's git box. |
| P3 | Loading (D303; design 12.3, 12.2, 9.7; 7a design 17 item 1): a refresh after a failure shows `Loading merge requests…` or `Loading pull requests…` with `0` while the list's answer is held; the answer then fills the section; "hosting7b", one tab on `web-app`; tabs `mobile-app` and `manifest`; signed in, dark, 1100×700 | **Method 3**, after P4: 7. Mark the requests and the log. `served.GitLab.HoldNext("mrs:northfield/platform/web-app")`. Right-click the section's header; click `Refresh merge requests`; wait for the hold's `Arrived`, for the hosting's `PullRequests` `Loading`, then for the message row `Loading merge requests…` and for the watchers to be quiet. Screenshot (`p03a-gitlab-loading`). (Step 8 is P10's.) 9. Dispose the hold; wait for `web-app: listed 3 open pull requests of northfield/platform/web-app` since step 7's mark and for the watchers to be quiet.<br>**Method 4**, after P4: 6. Mark the requests and the log. `served.AzureDevOps.HoldNext("pulls:fernhill-labs/mobile app/mobile-app")`. Right-click the header; click `Refresh pull requests`; wait for `Arrived`, `Loading`, the message row `Loading pull requests…` and quiet watchers; read the section. Dispose the hold; wait for `mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app` since the mark and for the watchers to be quiet. | **Method 3, after step 7:** one `mrs:` request waits at the fake, so requests since the mark none (A3's answer is kept per candidate, 9.7: no `project:northfield/platform/web-app`); `PullRequests` `Loading`, `List` and `Failure` null (the run cleared P4's failure); the section `Merge requests` `0` and one `PullRequestMessage` `Loading merge requests…` (whole); no `PullRequestItem`; no `!n` on a branch row.<br>**Method 3, after step 9:** requests since step 7's mark exactly two, in order: `GET …/merge_requests` (route `mrs:northfield/platform/web-app`, query `state=opened&per_page=100`) 200, then `GET /api/v4/projects/<ada/web-app's number>` (route `project:ada/web-app`) 200, the fork's first lookup (the failed list looked up none); each `Bearer`, `Login` `ada`. `PullRequests` `Loaded`; the section `Merge requests` `3` with P1's three rows in order; no message row.<br>**Method 4, while held:** requests since the mark none (no `repo:`, 9.7); `Loading`; the section `Pull requests` `0` and `[Loading] Loading pull requests…` (7a's text word for word); no row.<br>**Method 4, after the release:** requests since the mark exactly one `GET …/mobile-app/pullrequests`, the query as P1's, `BasicEmptyUser`, 200; `Loaded`; `Pull requests` `3` with P1's three rows. |  | n/a | `p03a-gitlab-loading` | n/a: a held request (0.5). Azure DevOps' loading is asserted, not pictured: its row is 7a's text word for word. Varies by nature: the status bar's git box. |
| P4 | Failed in `HostingTexts.Describe`'s words (D338, D303, D385; design 12.3, 11.2, 9.6; 16.1's As built): signed in from the section's row, GitLab refuses the list with 403 `insufficient_granular_scope`: the section `0` and `GitLab answered:` with GitLab's message, its tooltip that text and the code; Azure DevOps answers the list with 500: `Azure DevOps had a problem. Try again later.`; each account stays signed in; "hosting7b", one tab on `web-app`; tabs `mobile-app` and `manifest`; dark, 1100×700 | **Method 3**, after P2: 5. `served.GitLab.FailNext("mrs:northfield/platform/web-app", FakeGitLabFault.InsufficientGranular)`. `TypeToken(GitLabToken)`; press Enter; wait until the dialog has closed, for the hosting's `PullRequests` `Failed`, then for the message row and for the watchers to be quiet. Screenshot (`p04a-gitlab-failed`). 6. Rest the pointer on the message row's text until its tooltip shows. Screenshot (`p04b-gitlab-failed-tooltip`, the pointer kept).<br>**Method 4**, after P2: 5. `served.AzureDevOps.FailNext("pulls:fernhill-labs/mobile app/mobile-app", FakeAzureDevOpsFault.ServerError)`. `TypeToken(AzureDevOpsToken)`; press Enter; wait until the dialog has closed, for `Failed`, the message row and quiet watchers. Screenshot (`p04c-azure-failed`). Rest the pointer on the message row until its tooltip shows; read it. | **Method 3, after step 5:** requests since step 4's mark, in order: `GET /api/v4/user` 200, `Login` `ada`; `project:northfield/platform/web-app` 200; `mrs:northfield/platform/web-app` (the query as P1's) 403; each `Bearer`; nothing else (no fork lookup: the list failed). Calls `probe`, `save gitlab:127.0.0.1:47398:ada`. `AccountOf(GitLab)` `SignedIn`, `Keychain`, note `None` (only a 401 marks it, 9.6); `accounts.json` `tokenState` `keychain`. The hosting: `Failed`, `Failure` `Forbidden`, `Provider` GitLab, `Message` `Access denied: This operation requires a fine-grained personal access token with the following project permissions: [Merge Request: Read].`, `Detail` `insufficient_granular_scope`; `List` null; `ByRef` empty; `Bindings` the GitLab account's for `WebAppUrl` (twins not counted, as A14). The section: `Merge requests` `0`; one `PullRequestMessage` `GitLab answered: Access denied: This operation requires a fine-grained personal access token with the following project permissions: [Merge Request: Read].` (cut with "…"); no `PullRequestItem`. The log since step 4's mark: once each the HTTP core's Warning `GitLab: GET /api/v4/projects/northfield%2Fplatform%2Fweb-app/merge_requests failed: 403 (request {id}).` and the session's Warning `GitLab: listing the pull requests of northfield/platform/web-app failed: Forbidden, Access denied: … [Merge Request: Read]. (request {id})` (part P's texts, whole), `{id}` the refused answer's; no `web-app: listed` line; no dialog.<br>**Method 3, after step 6:** the tooltip's two lines `GitLab answered: Access denied: This operation requires a fine-grained personal access token with the following project permissions: [Merge Request: Read].` and `insufficient_granular_scope`, whole.<br>**Method 4, after step 5:** requests since step 4's mark, in order: `connection:fernhill-labs` 200, `Login` Ada's; `areas` 200; `repo:fernhill-labs/mobile app/mobile-app` 200; `pulls:fernhill-labs/mobile app/mobile-app` (the query as P1's) 500; each `BasicEmptyUser`; nothing else, and none for harbor-co. Calls `probe`, `save azure-devops:127.0.0.1:47397:fernhill-labs`. `AccountOf(AzureDevOps)` `SignedIn`, `Keychain`. `mobile-app`'s hosting: `Failed`, `Failure` `ServerError`, `Provider` Azure DevOps, the fake's TF400898 message, `Detail` null; `List` null; `ByRef` empty; `Bindings` Ada's account's for `MobileAppUrl`. The section: `Pull requests` `0`; one `PullRequestMessage` `Azure DevOps had a problem. Try again later.`; its tooltip that text alone (no `Detail`). The log since the mark: once each `Azure DevOps: GET /Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests failed: 500 (request {id}).` and `Azure DevOps: listing the pull requests of Fernhill-Labs/Mobile App/mobile-app failed: ServerError, TF400898: An Internal Error Occurred. Activity Id: {activity}. (request {id})`; no `mobile-app: listed` line; no dialog. |  | n/a | `p04a-gitlab-failed`, `p04b-gitlab-failed-tooltip`, `p04c-azure-failed` | n/a: a fault (`FailNext`, 0.5). Which failure per provider is open point 23: GitLab's own words and code shown, never a permission derived from the code (16.1). Varies by nature: the status bar's git box. |
| P5 | No open merge requests (D303; design 12.2, 12.3, 9.7): every open merge request of `northfield/platform/web-app` closed on the fake, "Refresh merge requests" lists none: `No open merge requests`, `0`, no `!n` left on a branch row and no badge in the graph; "hosting7b", one tab on `web-app`, GitLab and GitHub signed in, dark, 1100×700 | **Method 5**, after P6: 6. Set `State = "closed"` on each open merge request of `served.GitLab.MergeRequests(served.GitLab.Project(Scenarios.Hosting7bWebApp))`. Mark the requests and the log. Right-click the section's header; click `Refresh merge requests`; wait for `web-app: listed 0 open pull requests of northfield/platform/web-app` since the mark and for the watchers to be quiet. Scroll the panel's list to its top. Screenshot (`p05a-gitlab-none`). | **After step 6:** requests since the mark exactly one `GET …/merge_requests`, the query `state=opened&per_page=100`, `Bearer`, 200 (an empty page, `x-next-page` empty); no `project:` request; GitHub's and Azure DevOps' none. The hosting `Loaded`, `List` empty with `MoreExist` false, `ByRef` empty. The section: the chevron down, `Merge requests` `0`, one `PullRequestMessage` `No open merge requests`, no `PullRequestItem`; no branch row's `RefPullRequest` shows (`PullRequestText` empty on every `RefItemRow`); no `Badge` on any label of the graph; no scroll bar. |  | n/a | `p05a-gitlab-none` | n/a: 14.4 lists the scenario only. Closing them on the fake is 7a's way. Azure DevOps' `No open pull requests` is 7a's text (open point 24). Varies by nature: the status bar's git box. |
| P6 | More than 300 (7a's ruling 14, D303; design 12.2, 12.8, 7.4, 4.4): 298 more open merge requests on `northfield/platform/web-app`; a refresh reads three pages and lists 300 newest first, then GitLab's new sentence `More than 300 open merge requests`, which opens the list in the browser; "hosting7b", one tab on `web-app`, GitLab and GitHub signed in, dark, 1100×700 | **Method 5**, after P9: 3. `served.GitLab.AddMergeRequest(served.GitLab.Project(Scenarios.Hosting7bWebApp), $"More work {n}", "octo", "topic/no-request", "main")` for n 24 to 321, in order (the fake numbers them !24 to !321). Mark the requests and the log. Right-click the section's header; click `Refresh merge requests`; wait for `web-app: listed 300 open pull requests of northfield/platform/web-app` since the mark and for the watchers to be quiet. Scroll the panel's list to its end. Screenshot (`p06a-gitlab-more-than-300`). 4. Rest the pointer on the message row until its tooltip shows. 5. Click the message row. | **After step 3:** requests since the mark exactly three `GET /api/v4/projects/northfield%2Fplatform%2Fweb-app/merge_requests` (route `mrs:northfield/platform/web-app`), `Bearer`, 200: the first with the query `state=opened&per_page=100`, then pages 2 and 3 as the `Link` headers name them, the other parameters kept; no fourth (three pages, 7.4); no `project:` request (A3 read and the fork's id known since step 1, 7.4); GitHub's and Azure DevOps' none; GitLab's `Errors` empty. The hosting `Loaded`: `List` 300 with `MoreExist` true, !321 down to !24, then !23 and !22 (!21, the 301st, not listed); `ByRef`: `refs/remotes/origin/topic/no-request` → !24 (the lowest number on one key, 7a's 7.8), `refs/remotes/origin/docs/readme` → !22, nothing for `feature/search` (its !21 not listed). The section: `Merge requests` `300`; 300 `PullRequestItem` rows, then one `PullRequestMessage` `More than 300 open merge requests`. After the scroll: a vertical scroll bar; from the bottom up `Stashes` `0`, `Tags` `0`, `More than 300 open merge requests`, `!22 Draft: Update the readme` (`Draft`, `octo`), `!23 Fix a typo in the footer` (`ada`), `!24 More work 24` (`octo`), `!25 More work 25`, …; no text cut but by an ellipsis.<br>**After step 4:** the tooltip `Open merge requests in browser`.<br>**After step 5:** `Launcher.Requests` exactly one `OpenUrl` `http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests` (`OpenPullRequestsInBrowserAsync`, 12.5); no request since step 3's; nothing selected. |  | n/a | `p06a-gitlab-more-than-300` | n/a: the 298 merge requests are the check's own and the click goes to the fake launcher (0.5). Azure DevOps' 300 is open point 24. Varies by nature: the status bar's git box. |
| P7 | The filter on `!n` (design 12.3; D303, D355): the panel's filter keeps a merge request whose `!n`, `n`, title, author or head label holds its text, ignoring case, never `#n` on GitLab; the section's count is its matches; "hosting7b", one tab on the prepared `web-app`, signed in, dark, 1100×700 | **Method 1**, after P14: 8. Click `FilterBox`; `app.Type("!21")`. Screenshot (`p07a-gitlab-filter-number`). Press Ctrl+A; `app.Type("21")`; read the panel. Press Ctrl+A; `app.Type("#21")`; read. Press Ctrl+A; `app.Type("ada")`; read. Press Ctrl+A, then Backspace. | **After `!21`:** `Local branches` `0` and `Remotes` `0` (headers only); `Merge requests` `1`: `!21 Add the search box`; `Tags` `0`, `Stashes` `0`.<br>**After `21`:** the same (`21` matches `!21`'s number, 12.3).<br>**After `#21`:** `Merge requests` `0`, no row and no message row (GitLab's number is never `#21`, 12.3); every other section `0`.<br>**After `ada`:** `Merge requests` `2`: `!23 Fix a typo in the footer` (author and head label `ada:fix-typo`) and `!21 Add the search box` (author); every other section `0`.<br>**After clearing:** the filter empty with its placeholder; the panel as before step 8 (P14's folders open, the section open with its three rows). Requests none since step 8. |  | n/a | `p07a-gitlab-filter-number` | n/a: 14.4. Azure DevOps' filter is the same rule on its rows (`RequestSectionProvidersTests`, 12.7). Varies by nature: the filter's caret; the status bar's git box. |
| P8 | D356's row, nothing asked (D356, D366, D376, D350; design 12.3, 12.4, 12.5, 9.7): with Fernhill-Labs signed in, `manifest`'s section, whose listed remote is harbor-co's, shows `In harbor-co; signed in to Fernhill-Labs` with `0`, no list and no sign-in; its tooltip says why and how to change it; the header toggles; its menu holds `Open pull requests in browser` alone; its branch menus offer no request; nothing is sent for harbor-co; "hosting7b", tabs `mobile-app` and `manifest`, Azure DevOps signed in, dark, 1100×700 | **Method 4**, after P3: 7. Mark the requests, the git requests and the log. Click the tab `manifest`; `WaitForRepositoryAsync()`; wait for its hosting's `PullRequests` `OtherOrganization`, then for the section's message row and for the watchers to be quiet. Screenshot (`p08a-azure-other-organization`). Rest the pointer on the message row's text until its tooltip shows. Screenshot (`p08b-azure-other-organization-tooltip`, the pointer kept). Click the message row; wait until the app is idle. Click the header `Pull requests`; read; click it again. (Steps 8 and 9 are P10's and P13's.) 10. Right-click `Local branches` › `main`; read; press Esc. | **After step 7:** `manifest`'s hosting: `OtherOrganization`, `Remotes` exactly `origin` → (Azure DevOps, `127.0.0.1:47397`, `harbor-co/Docks`, `manifest`), `Repository` that remote's (12.3, 9.7), `List` and `Failure` null, `ByRef` and `RequestRemotes` empty, `Bindings` none for `ManifestUrl` (D350: no account covers it). The section: the chevron down, `Pull requests` `0`; one message row (kind `OtherOrganization`) `In harbor-co; signed in to Fernhill-Labs` (the remote's `OwnerRoot` as written, the account's login in its own case; secondary; cut with "…" at 260, where its 40 characters, about 240 px, pass the row's room of about 205 px; where it is cut is inspected, open point 27), its control a disabled button; no `PullRequestItem`; no `!n` on a branch row; no badge. Requests since the mark none in any fake, and none since the start whose path begins `/harbor-co/` or whose route names `harbor-co` (D356, D376); `GitRequests` since the mark none for `harbor-co/Docks/_git/manifest`; no `manifest: listed` line. After the tooltip: exactly `This repository is in the Azure DevOps organization harbor-co; the signed-in account is in Fernhill-Labs, so nothing is asked of Azure DevOps for it. To see its pull requests, sign out and sign in to harbor-co in Settings, Accounts.`, whole (wrapped, nothing cut). After the row's click: no dialog, no `Launcher.Requests` entry, no request, nothing selected (the row takes no click: no sign-in, D356). After the header's first click: the chevron right, `0`, no row under it; after the second: open again with the row (it toggles, 12.3).<br>**After step 10:** the menu's header `main`; no line names a request (none is `Create pull request…` or starts `Open pull request`; 12.4: every branch and drop hook gives nothing while `OtherOrganization`); requests none; after Esc no menu. |  | n/a | `p08a-azure-other-organization`, `p08b-azure-other-organization-tooltip` | n/a: 14.4 has no launch on `manifest`. How the row and its tooltip read is 16.3's record (open point 27). D366's signed-out row with no Azure DevOps account is open point 32's. Varies by nature: the status bar's git box. |
| P9 | Ruling 5's listed remote (ruling 5, D357, D367; design 9.6, 9.7, 12.3, 12.4): with GitLab and GitHub signed in, `web-app` lists its `origin`'s merge requests, the first recognised remote, and asks GitHub nothing for its `upstream`; `upstream`'s branches show no number and offer no request item; no GitHub avatar; "hosting7b", one tab on `web-app`, GitLab and GitHub signed in, dark, 1100×700 | **Method 5** (`Check_P9_P6_P5_gitlab_listed_remote_more_than_300_and_none`): 1. `ServeAsync()`. `OpenAsync(data, served, WebApp, signIn: [GitLab, GitHub])`; wait for `web-app: listed 3 open pull requests of northfield/platform/web-app` since the start and for the watchers to be quiet. Screenshot (`p09a-gitlab-listed-remote`). 2. Mark the requests. Right-click `Remotes` › `upstream` › `main`; read; press Esc. Click `Remotes` › `origin` › `topic`; right-click its `no-request`; read; press Esc. | **After step 1:** requests since the start: GitLab's, in this order, `user` 200, `project:northfield/platform/web-app` 200, `mrs:northfield/platform/web-app` 200, `project:ada/web-app` 200, each `Bearer`, `Login` `ada`; GitHub's exactly one, `GET api/v3/user` (route `user`) 200, `Login` `octo`: none for `octo/web-app` (no `repo:`, `pulls:`, `commits:` or `avatar:` route; GitHub's sign-in refreshes no tab GitLab lists, 9.10); Azure DevOps' none. The hosting `Loaded`: `Remotes` exactly `origin` → (GitLab, `127.0.0.1:47398`, `northfield/platform`, `web-app`) and `upstream` → (GitHub, `127.0.0.1:47399`, `octo`, `web-app`); `Repository` `origin`'s (the first recognised in `origin`, `upstream`, name order, whatever its provider, ruling 5); `RequestRemotes` exactly `origin`; `List` P1's three; `ByRef` exactly the facts' three keys, none under `refs/remotes/upstream/`; `Bindings` the GitLab account's for `WebAppUrl` and the GitHub account's for `WebAppUpstreamUrl` (twins not counted, as A14). The section: `Merge requests` `3` with P1's three rows; `Remotes` › `upstream` › `main` without `RefPullRequest`. No avatar (D367): the tab's `Avatars` empty, no `showing GitHub avatars` line in the log.<br>**After step 2:** `upstream/main`'s menu: header `upstream/main`, no line naming a request (none starts `Open merge request`, `Open pull request`, or is `Create merge request…` or `Create pull request…`; 12.4: `upstream` is no key of `RequestRemotes`). `origin/topic/no-request`'s menu holds `Create merge request…` alone among its request lines, in the group after its delete group, where 7a's P12 step 16 had `Create pull request…` for `origin/topic/no-pr` (WP-N's action, not clicked). Requests none; after each Esc no menu. |  | n/a | `p09a-gitlab-listed-remote` | n/a: WP-R's launches sign in one provider each (14.2, ruling 29). The exact lines of `upstream/main`'s menu are `RepositoryMenusProvidersTests`' (12.7). Varies by nature: the status bar's git box. |
| P10 | The section's menu and a request's (D351, D355, D376, D114; design 12.4, 12.2, 12.5; R): loaded `Refresh merge requests` and `Open merge requests in browser` on GitLab, `Refresh pull requests` and `Open pull requests in browser` on Azure DevOps; signed out `Sign in to GitLab…` or `Sign in to Azure DevOps…` first; loading the refresh disabled with `Merge requests are loading`; D356's state `Open pull requests in browser` alone; a request's `Open in browser`, `Check out {label}` and `Copy link` under `{N} {title}`; asking for a menu selects nothing and asks nothing; "hosting7b", one tab on the prepared `web-app` or `mobile-app`; one on `web-app`; tabs `mobile-app` and `manifest`; dark, 1100×700 | **Method 1**, after P1's step 1: 2. Mark the requests. Right-click the header `Merge requests` at its centre. Screenshot (`p10a-gitlab-section-menu`, the pointer kept). Press Esc. 3. Right-click `!23`'s row. Screenshot (`p10b-gitlab-request-menu`). Press Esc. Right-click `!22`; read; Esc. Right-click `!21`; read (P12's step 4 uses this menu).<br>**Method 2**, after P1's step 1: 2. Mark the requests. Right-click `Pull requests` at its centre. Screenshot (`p10c-azure-section-menu`, the pointer kept). Press Esc. 3. Right-click `!13`; read; Esc. Right-click `!12`; read; Esc. Right-click `!11`; read (P12's step 4 uses it).<br>**Method 3**, after P2's step 1: 2. Mark the requests and the log. Right-click `Merge requests` at its centre; read (P13's step 3 uses this menu). After P3's step 7: 8. Right-click the header; rest the pointer on `Refresh merge requests` until its tooltip shows; read; press Esc.<br>**Method 4**, after P2's step 1: 2. Right-click `Pull requests` at its centre; read (P13's step 3 uses it). After P8's step 7: 8. Mark the requests. Right-click `manifest`'s header `Pull requests` at its centre. Screenshot (`p10d-azure-section-menu-other-organization`, the pointer kept). (P13's step 9 uses it.) | **Method 1, after step 2:** header `Merge requests`; `Refresh merge requests` (highlighted); `Open merge requests in browser`; 84 high ±2, its top-left at the pointer plus (2, 2); the section neither closed nor opened; requests none.<br>**Method 1, after step 3:** `!23`'s: header `!23 Fix a typo in the footer`; `Open in browser` (highlighted); `Check out ada:fix-typo` (12.5 step 2: GitLab's head ref from the base's remote `origin`, the fork no remote here; the label `BranchLabelOf`'s `{Owner}:{branch}`); —; `Copy link`; 119 high ±2; `!23` not selected, the graph's selection unchanged (D114). `!22`'s: header `!22 Draft: Update the readme`; `Open in browser` (highlighted); `Check out docs/readme` (its head on `origin`, no local branch yet); —; `Copy link`. `!21`'s: header `!21 Add the search box`; `Open in browser` (highlighted); `Check out feature/search` (its head on `origin`, the local `feature/search` tracking it); —; `Copy link`. Requests none.<br>**Method 2, after step 2:** header `Pull requests`; `Refresh pull requests` (highlighted); `Open pull requests in browser`; 84 high ±2; requests none.<br>**Method 2, after step 3:** `!13`'s: header `!13 Fix a typo in the greeting`; `Open in browser` (highlighted); `Check out mobile-app-fork:fix-typo` (disabled: "Add a remote for Fernhill-Labs/Mobile App/mobile-app-fork to check out this pull request"; D361: no head ref); —; `Copy link`. `!12`'s: header `!12 Write the guide`; `Open in browser` (highlighted); `Check out docs/guide`; —; `Copy link`. `!11`'s: header `!11 Add the sign-in page`; `Open in browser` (highlighted); `Check out feature/sign-in`; —; `Copy link`. Each 119 high ±2; requests none.<br>**Method 3, after step 2:** header `Merge requests`; `Sign in to GitLab…` (highlighted); `Open merge requests in browser`; 84 high ±2; the section neither closed nor opened; requests none.<br>**Method 3, after step 8:** header `Merge requests`; `Refresh merge requests` (disabled: "Merge requests are loading"), its tooltip shown; `Open merge requests in browser` (highlighted). After Esc no menu.<br>**Method 4, after step 2:** header `Pull requests`; `Sign in to Azure DevOps…` (highlighted); `Open pull requests in browser`; 84 high ±2; requests none.<br>**Method 4, after step 8:** header `Pull requests`; `Open pull requests in browser` (highlighted) alone, no refresh and no sign-in (D356, D376); 58 high ±2 (`MenuHeight(1, 0)`); requests none. |  |  | `p10a-gitlab-section-menu`, `p10b-gitlab-request-menu`, `p10c-azure-section-menu`, `p10d-azure-section-menu-other-organization`; real window: `p10a-gitlab-section-menu`, `p10c-azure-section-menu` | **R** (14.4 "Requests and create", step 2): a real right-click at the centre of the section's header (`Merge requests` or `Pull requests`, `PartP`'s title for the provider); `AssertMenu` with method 1's or method 2's step 2 lines; compared with `p10a-gitlab-section-menu` or `p10c-azure-section-menu` with `keepPointer`; Esc; a real right-click on `PullRequestRow("!21 Add the search box")` or `PullRequestRow("!11 Add the sign-in page")`: the labels of step 3's `!21`'s or `!11`'s menu, no capture; Esc (open point 28). `p10b`, `p10d` and the signed-out and loading menus are scripted only. The palette's request items are `RepositoryMenusProvidersTests`' (12.7). Varies by nature: the status bar's git box. |
| P11 | Checking out a request (D361, D305, D304, D255, D193; design 12.5, 8.2; ruling 19): a double-click on `!21` checks out `feature/search` without a fetch; `Check out ada:fix-typo` fetches `refs/merge-requests/23/head` into `FETCH_HEAD` only, through the app's helper, makes `fix-typo` without upstream at its SHA and checks it out; a double-click on `!11` checks out `feature/sign-in` without a fetch; `!13`'s item disabled with its reason and its double-click starting nothing; never a `refs/merge-requests` or `refs/pull` fetch line or remote-tracking ref, and no fetch of an Azure DevOps request ref; "hosting7b", one tab on `web-app`, then one on `mobile-app`, signed in, dark, 1100×700 | **Method 6** (`Check_P11_gitlab_checkout_of_21_and_23`): 1. `ServeAsync()`. `OpenAsync(data, served, WebApp)` (GitLab signed in through the session); wait for `web-app: listed 3 open pull requests of northfield/platform/web-app` since the start and for the watchers to be quiet. In `web-app/`, through `GitRunner` with `TempRepo.IsolatedEnvironment`, read `git config --local --list` and `git for-each-ref --format=%(refname)`. 2. Mark the requests and the log; take a quiet call mark. Double-click `!21`'s row; wait for the checkout's end and for the watchers to be quiet. Right-click `!21`; read; press Esc. 3. Mark the requests, the git requests and the log; take a quiet call mark. Right-click `!23`; click `Check out ada:fix-typo`; wait for both operations' ends, for `web-app: listed 3 open pull requests of northfield/platform/web-app` since the mark and for the watchers to be quiet. Screenshot (`p11a-gitlab-checked-out-fork`). Right-click `!23`; read; press Esc. 4. Close the app. In `web-app/`, as in step 1: `git symbolic-ref HEAD`; `git rev-parse HEAD`; `git for-each-ref --format=%(refname)`; `git config --get-all remote.origin.fetch` and `remote.upstream.fetch`; `git config --get-regexp ^branch\.fix-typo\.`; `git config --local --list`; `git status --porcelain`; and read `.git/FETCH_HEAD`.<br>**Method 7** (`Check_P11_azure_checkout_of_11_and_13s_reason`): 1. `ServeAsync()`. `OpenAsync(data, served)` (one tab on `mobile-app`, Azure DevOps signed in through the session); wait for `mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app` since the start and for the watchers to be quiet. In `mobile-app/`, as method 6's step 1, read `git config --local --list`, `git for-each-ref --format=%(refname)` and `.git/FETCH_HEAD`. 2. Mark the requests and the log; take a quiet call mark. Double-click `!11`'s row; wait for the checkout's end and for the watchers to be quiet. Right-click `!11`; read; press Esc. 3. Mark the requests and the log; take a quiet call mark. Right-click `!13`; rest the pointer on `Check out mobile-app-fork:fix-typo` until its tooltip shows. Screenshot (`p11b-azure-fork-reason`, the pointer kept). Press Esc. Double-click `!13`'s row; wait until the app is idle and the watchers are quiet. 4. Close the app. In `mobile-app/`: `git symbolic-ref HEAD`; `git for-each-ref --format=%(refname)`; `git config --get-all remote.origin.fetch`; `git config --local --list`; `git status --porcelain`; and read `.git/FETCH_HEAD`. | **Method 6, after step 2:** one operation, phase 3's checkout of `origin/feature/search` (12.5 step 1, D305's first way), ending Succeeded; the status bar `Checked out feature/search`; git calls since the quiet mark hold `git switch --no-guess --no-overwrite-ignore feature/search` (D193) and no `git fetch` or `git branch`; requests none (a local operation refreshes no list); HEAD on `feature/search`, which tracks `origin/feature/search` as before; `!21`'s menu `Check out feature/search` (disabled: "feature/search is already checked out").<br>**Method 6, after step 3:** two operations, Succeeded in this order: `Fetch merge request !23` (its report `Fetched !23 into fix-typo`), then the checkout of `fix-typo`; the status bar `Checked out fix-typo`; no dialog at any time. Git calls since the quiet mark, in order: `git fetch --progress --no-tags --refmap= --write-fetch-head origin refs/merge-requests/23/head`; `git branch --no-track fix-typo <sha>`, `<sha>` the full SHA of `refs/merge-requests/23/head` in the copy's `gitlab/northfield/platform/web-app.git` (`ada/web-app`'s `fix-typo`, 12:31 "Fix a typo in the footer" by Ada Lovelace); `git switch --no-guess --no-overwrite-ignore fix-typo`; no other `git fetch`, no `rev-parse` of `FETCH_HEAD` (7a's 10.5), no `git config` that writes. Git requests on `northfield/platform/web-app.git` since the mark: at least one with `GivenUserName` `x-access-token` answered 200 (D255, D350: through the app's helper); none with another user name; none with credentials answered 401. API requests since the mark: exactly one `GET …/merge_requests` (the refresh after a network operation, 7a's 7.10), the query `state=opened&per_page=100`, `Bearer`, answered 304 (the list unchanged since step 1's read, whose weak `ETag` the core kept and sent, 4.4; open point 31); no `project:` request; none answered 401. The graph's top row `Fix a typo in the footer` (Ada Lovelace) with `fix-typo`, HEAD; the local row `fix-typo` (accent) without `!n` (no upstream); the section still `3`; `!23`'s menu `Check out ada:fix-typo` (disabled: "fix-typo is already checked out").<br>**Method 6, after step 4:** HEAD `refs/heads/fix-typo` at `<sha>`; `for-each-ref` exactly step 1's refs and `refs/heads/fix-typo`: nothing under `refs/merge-requests/`, no `refs/remotes/origin/mr/…` or other remote-tracking ref of a request; `remote.origin.fetch` exactly `+refs/heads/*:refs/remotes/origin/*` and `remote.upstream.fetch` exactly `+refs/heads/*:refs/remotes/upstream/*`; `--get-regexp ^branch\.fix-typo\.` exits 1 and prints nothing; `--local --list` byte for byte step 1's; `FETCH_HEAD` one line: `<sha>`, a tab, an empty field, a tab, then text starting `'refs/merge-requests/23/head' of ` (the URL not compared; 12.5, ASSUMED, open point 30); the working tree clean.<br>**Method 7, after step 2:** one operation, phase 3's checkout of `origin/feature/sign-in`, Succeeded; the status bar `Checked out feature/sign-in`; git calls since the quiet mark hold `git switch --no-guess --no-overwrite-ignore feature/sign-in` and no `git fetch` or `git branch`; requests none; HEAD on `feature/sign-in`, tracking `origin/feature/sign-in`; `!11`'s menu `Check out feature/sign-in` (disabled: "feature/sign-in is already checked out").<br>**Method 7, after step 3:** `!13`'s menu as P10's method 2 gives it, `Check out mobile-app-fork:fix-typo` disabled, its tooltip `Add a remote for Fernhill-Labs/Mobile App/mobile-app-fork to check out this pull request` shown; 119 high ±2. After the double-click: no operation; no git call since the quiet mark (`GitCallBudget`; the double-click starts only a plan without a reason, 12.5); requests none; HEAD still `feature/sign-in`; no row of the panel selected and the graph's selection empty (`!13`'s head, the fork's tip, is no row of `mobile-app`'s graph; open point 29).<br>**Method 7, after step 4:** HEAD `refs/heads/feature/sign-in`; `for-each-ref` exactly step 1's: nothing under `refs/pull/`, no branch made; `remote.origin.fetch` exactly `+refs/heads/*:refs/remotes/origin/*`; `--local --list` byte for byte step 1's; `FETCH_HEAD` byte for byte step 1's (no fetch ran); the method's call log holds no `git fetch` naming `refs/pull/` and none since step 2's quiet mark (git fetches no Azure DevOps request ref, D361, 12.9); the working tree clean. |  | n/a | `p11a-gitlab-checked-out-fork`, `p11b-azure-fork-reason` | n/a: 14.4 checks nothing out. From git 2.46 the fetch's first git request carries the account's token; below 2.46 it goes without credentials and gets 401 (part P's conventions). A taken name (`ada-fix-typo`), a reused branch, `mr-{iid}`, a nested owner's name, `HeadNotNamed` and `NotFetchedBy` are 12.7's classes'. Varies by nature: the status bar's git box. |
| P12 | Copy link (design 12.4, 12.5, 6.6, 7.4; D251): a request's `Copy link` puts its page's address on the clipboard and asks nothing: GitLab's `web_url`, Azure DevOps' address built by the provider; "hosting7b", one tab on the prepared `web-app`, then on the prepared `mobile-app`, signed in, dark, 1100×700 | **Method 1**, after P10's step 3: 4. Click `Copy link` (`!21`'s menu).<br>**Method 2**, after P10's step 3: 4. Click `Copy link` (`!11`'s menu). | **Method 1, after step 4:** the menu closed; the last of `Clipboard.Texts` `http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests/21` (through `WithPorts`); requests none; `Launcher.Requests` empty; no operation; nothing selected; the graph's selection unchanged.<br>**Method 2, after step 4:** the last of `Clipboard.Texts` `http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/11` (the provider's, 6.6: the answer has none); otherwise as method 1's. |  | n/a | none | n/a: the real window never clicks a "Copy …" item (14.2). |
| P13 | Open in browser through `FakeFileLauncher` (ruling 29; design 12.4, 12.5; D251, D257, D376): a request's `Open in browser` and the section's `Open merge requests in browser` or `Open pull requests in browser` hand the page to the launcher and ask nothing, signed out and in D356's state too; "hosting7b", one tab on the prepared `web-app` or `mobile-app`; one on `web-app`; tabs `mobile-app` and `manifest`; dark, 1100×700 | **Method 1**, after P12: 5. Mark the requests and the log. Right-click `!21`; click `Open in browser`. Right-click the header; click `Open merge requests in browser`.<br>**Method 2**, after P12: 5. The same with `!11` and `Open pull requests in browser`.<br>**Method 3**, after P10's step 2: 3. Click `Open merge requests in browser` (the signed-out menu).<br>**Method 4**, after P10's step 2: 3. Click `Open pull requests in browser` (the signed-out menu). After P10's step 8: 9. Click `Open pull requests in browser` (`manifest`'s menu). | **Method 1, after step 5:** `Launcher.Requests` exactly two `OpenUrl`, `http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests/21`, then `http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests` (`PullRequestsPage` of the `Repository`, 12.5); requests since the mark none (the fake launcher fetches no page); no operation; no Warning in the log since the mark.<br>**Method 2, after step 5:** exactly two, `http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/11`, then `http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequests`; otherwise as method 1's.<br>**Method 3, after step 3:** `Launcher.Requests` exactly one `OpenUrl` `http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests`; requests none (D257); still signed out.<br>**Method 4, after step 3:** exactly one, `http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequests`; requests none; still signed out.<br>**Method 4, after step 9:** `Launcher.Requests` gains `OpenUrl` `http://127.0.0.1:47397/harbor-co/Docks/_git/manifest/pullrequests` (D376: the user's own browser; the `Repository`'s page, 12.5); requests none, nothing for harbor-co. |  | n/a | none | n/a: no 7b step opens the browser (ruling 29); the launcher's records are the check (12.8). The `More` row's click is P6's, the branch menus' items P14's. |
| P14 | A branch row's `!21` (D308, D355; design 12.3, 12.4, 9.7): `!21` on `feature/search` and `origin/feature/search`, `!22` on `origin/docs/readme`, the tooltip `Merge request !21: Add the search box`; none on `upstream`'s branches or the prepared `topic/no-request`; `Open merge request !21` in the branch's menu; Azure DevOps' `!11` and `!12` alike; "hosting7b", one tab on the prepared `web-app`, then on the prepared `mobile-app`, signed in, dark, 1100×700 | **Method 1**, after P1's step 6: 7. Click the header `Merge requests`. Click `Local branches` › `feature`, `Remotes` › `origin` › `feature` and `Remotes` › `origin` › `docs` (closed at the opening). Rest the pointer on the local `search` row's `!21` until its tooltip shows. Screenshot (`p14a-gitlab-branch-rows`, the pointer kept). Mark the requests. Right-click `Local branches` › `feature` › `search`; read; click `Open merge request !21`. Click the header `Merge requests`.<br>**Method 2**, after P1's step 6: 7. Click `Local branches` › `feature`, `Remotes` › `origin` › `feature` and › `docs`. Rest the pointer on the local `sign-in` row's `!11` until its tooltip shows; read it. Mark the requests. Right-click `Local branches` › `feature` › `sign-in`; read; click `Open pull request !11`. | **Method 1, after step 7:** after the first click the section closed: the chevron right, still `3`, no row under it. Then `Local branches` › `feature` › `search` with `RefPullRequest` `!21` (secondary) at the row's right end, the name left of it; `Remotes` › `origin` › `feature` › `search` `!21`; `Remotes` › `origin` › `docs` › `readme` `!22`; on `local-work`, `main` (accent), the folder `topic` (the prepared branch, closed), `origin` › `main`, `upstream` › `main` and the folders `RefPullRequest` collapsed; the tooltip `Merge request !21: Add the search box`. The menu: 7a's P12 step 14 menu of `feature/login`, with `feature/search` in place of `feature/login` and `Open merge request !21` in place of `Open pull request #1`, last in its push group after `Push feature/search to origin/feature/search` and `Force push feature/search…` (danger); 597 high ±2 as 7a's. After the click `Launcher.Requests` gains `OpenUrl` `http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests/21`; requests none. After the last click the section open again with its three rows.<br>**Method 2, after step 7:** `feature` › `sign-in` `!11`; `origin` › `feature` › `sign-in` `!11`; `origin` › `docs` › `guide` `!12`; none on `local-work`, `main`, `origin` › `main` or the folder `topic`; the tooltip `Pull request !11: Add the sign-in page`; `sign-in`'s menu holds `Open pull request !11` in the same place; after the click `Launcher.Requests` gains `OpenUrl` `http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/11`; requests none. |  | n/a | `p14a-gitlab-branch-rows` | n/a: 14.4 captures the graph's badge (P1), not the branch rows. The drop menus' items and the `Create …` items are part N's (13.8). Varies by nature: the status bar's git box. |
| N1 | From a branch's menu and a drop's (D306, D351, D355; design 13.8, 12.4, 13.3; 7a design 10.4; R in part): `Create merge request…` on GitLab and `Create pull request…` on Azure DevOps, on the prepared `topic/no-request`, on `origin/topic/no-request`, on `local-work` and in the drop of `local-work` on `origin/main`, each where 7a's `Create pull request…` stood; GitLab's four opening its form (the remote branch's after its sign-in first, N8), Azure DevOps' local branch's and drop's opening its form (the drop's after its sign-in first, N8) and a remote branch's opening it on the remote `long` (N7), its `origin/topic/no-request` and `local-work` items read only; offered signed out: GitLab's remote branch's, and Azure DevOps' remote branch's, `local-work`'s and drop's (the local branch's read signed in only, GitLab's `local-work` and drop after its sign-in); a remote branch's From its ref's name; "hosting7b", one tab on the prepared `web-app`, then on the prepared `mobile-app`, signed in or signed out, dark, 1100×700 | **Method 1** (`Check_N1_N2_N6_gitlab_branch_menu_form_and_created`): 1. `ServeAsync()`. `PrepareRequestsAsync(served.MobileApp, GitLab)`. `OpenAsync(data, served, WebApp, signIn: [])` (one tab on `web-app`, signed out, a new store). Mark the requests, the calls and the log. Sign in as part P's method 1 does: `OpenAccountsAsync()`; `OpenSignInDialogAsync(GitLab)`; `TypeToken(GitLabToken)`; press Enter; wait until the dialog has closed and `WaitForAccountAsync(GitLab, …)` sees it signed in; press Esc; wait for the screen's `IsClosed`. Wait for `web-app: listed 3 open pull requests of northfield/platform/web-app` since the mark and for the watchers to be quiet. 2. Mark the requests. Click `Local branches` › `topic`; right-click its `no-request`; read the menu. (N2's step 3 clicks its `Create merge request…`.)<br>**Method 2** (`Check_N1_N2_N6_azure_branch_menu_form_and_created`): 1. `ServeAsync()`. `PrepareRequestsAsync(served.MobileApp, AzureDevOps)`. `OpenAsync(data, served, signIn: [])` (one tab on `mobile-app`, signed out, a new store). Mark the requests, the calls and the log. Sign in as part P's method 2 does: `OpenAccountsAsync()`; `OpenSignInDialogAsync(AzureDevOps)` (the organization prefilled, the focus in `SignInToken`); `TypeToken(AzureDevOpsToken)`; press Enter; wait until the dialog has closed and the account is signed in; press Esc; wait for `IsClosed`. Wait for `mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app` since the mark and for the watchers to be quiet. 2. Mark the requests. Click `Local branches` › `topic`; right-click its `no-request`; read the menu.<br>**Method 3** (`Check_N8_N1_N4_N3_gitlab_signed_out_drop_pushed_first_and_draft`), after N8's step 1: 2. Click `Remotes` › `origin` › `topic`; right-click its `no-request`; read the menu. (N8's steps 3 and 4 click its `Create merge request…`, sign in first and take this form's picture, `n01a-gitlab-remote-branch-form`.) After N8's step 4: 5. Mark the requests. Right-click `Local branches` › `local-work`; read; click `Create merge request…`; wait for the form with `PullRequestBase` `origin/main`; read it; press Esc; wait until the form has closed. Drag `Local branches` › `local-work` onto `Remotes` › `origin` › `main` (press, move held until the drag starts, release over the row); wait for the drop menu. Screenshot (`n01b-gitlab-drop-menu`). (N4's step 6 clicks its `Create merge request…`.)<br>**Method 4** (`Check_N8_N1_N4_N3_azure_signed_out_drop_pushed_first_and_draft`), after N8's step 1: 2. Click `Remotes` › `origin` › `topic`; right-click its `no-request`; read; press Esc. Right-click `Local branches` › `local-work`; read; press Esc. Drag `local-work` onto `Remotes` › `origin` › `main`; wait for the drop menu; read it. (N8's step 3 clicks its `Create pull request…`.) | **Method 1, after step 1:** as part P's P1 method 1 after its step 1: the screen closed, still signed in (D256); requests since the mark GitLab's `user`, `project:northfield/platform/web-app`, `mrs:northfield/platform/web-app` and `project:ada/web-app`, each 200, `Bearer`, `Login` `ada`, nothing else; calls exactly `probe`, `save gitlab:127.0.0.1:47398:ada`; the section `Merge requests` `3` with P1's rows; `Local branches` holds the closed folder `topic`.<br>**Method 1, after step 2:** the menu: header `topic/no-request`; `Check out topic/no-request` (highlighted); `Merge topic/no-request into main`; `Merge topic/no-request into main (create a merge commit)`; `Rebase main onto topic/no-request`; `Interactive rebase of main onto topic/no-request…`; `Fast-forward topic/no-request to origin/topic/no-request` (disabled: "topic/no-request is up to date with origin/topic/no-request"); —; `Push topic/no-request to origin/topic/no-request`; `Force push topic/no-request…` (danger); `Create merge request…`; —; `Create branch here…`; `Create tag here…`; `Rename topic/no-request…`; `Delete topic/no-request…` (danger); —; `Change upstream…`; `Stop tracking origin/topic/no-request`; —; `Compare topic/no-request with main`; `Show reflog of topic/no-request`; `Hide topic/no-request`; `Solo topic/no-request`; —; `Copy branch name`: 7a's N1 step 2 menu with 7b's names and `Create merge request…` for `Create pull request…`, 20 items and 5 separators, 597 high ±2, inside the window. Requests none.<br>**Method 2, after step 1:** as P1 method 2 after its step 1: requests since the mark Azure DevOps' `connection:fernhill-labs`, `areas`, `repo:fernhill-labs/mobile app/mobile-app` and `pulls:fernhill-labs/mobile app/mobile-app`, each 200, `BasicEmptyUser`, nothing else; calls exactly `probe`, `save azure-devops:127.0.0.1:47397:fernhill-labs`; the section `Pull requests` `3`; the closed folder `topic`.<br>**Method 2, after step 2:** method 1's lines with `Create pull request…` in place of `Create merge request…`, 597 high ±2. Requests none.<br>**Method 3, after step 2** (signed out): header `origin/topic/no-request`; `Check out origin/topic/no-request` (highlighted); `Merge origin/topic/no-request into main`; `Merge origin/topic/no-request into main (create a merge commit)`; `Rebase main onto origin/topic/no-request`; `Interactive rebase of main onto origin/topic/no-request…`; —; `Create branch here…`; `Create tag here…`; —; `Delete topic/no-request on origin…` (danger); —; `Create merge request…`; —; `Compare origin/topic/no-request with main`; `Hide origin/topic/no-request`; `Solo origin/topic/no-request`; —; `Copy branch name`: 7a's N5 step 2 menu with 7b's names, 13 items and 5 separators, 415 high ±2; `Create merge request…` enabled (7a's 10.4; signed out `ByRef` is empty, so no menu offers `Open merge request`). Requests none. After N8's step 4 the form from it (`n01a-gitlab-remote-branch-form`): `From` `origin/topic/no-request` (the head's ref name, as 7a's N5; its tooltip the same) `northfield/platform/web-app`, no `PullRequestHeadNote` (a remote branch is never pushed, D313); `Into` `origin/main` `northfield/platform/web-app`; `PullRequestTitle` `Try an idea`, focused; the rest as N2's GitLab form; behind it the section `Merge requests` `3`.<br>**Method 3, after step 5:** `local-work`'s menu: header `local-work`; `Check out local-work` (highlighted); `Merge local-work into main`; `Merge local-work into main (create a merge commit)`; `Rebase main onto local-work`; `Interactive rebase of main onto local-work…`; —; `Push local-work…`; `Create merge request…`; —; `Create branch here…`; `Create tag here…`; `Rename local-work…`; `Delete local-work…` (danger); —; `Set upstream…`; —; `Compare local-work with main`; `Show reflog of local-work`; `Hide local-work`; `Solo local-work`; —; `Copy branch name`: 7a's P12 step 15 menu with `Create merge request…`, 17 items and 5 separators, 519 high ±2. The form from its `Create merge request…`: its title `Create merge request`; `From` `local-work` `northfield/platform/web-app`; `PullRequestHeadNote` `Not on GitLab yet: Create pushes it to origin/local-work first and sets that as its upstream.` (D313: no upstream); `Into` `origin/main` `northfield/platform/web-app`, A3's default (D318; a branch's item gives no base, 13.3), read from the list's kept answer (7.8; no request since the mark); `PullRequestTitle` `Local change`, focused; the form N4's step 6 gives from the drop, text for text (the same head and base, there given); after Esc no form and no operation. The drop menu: header `local-work → origin/main`; `Push local-work to origin/main` (highlighted); `Push local-work to origin/main and track it`; —; `Check out local-work and rebase it onto origin/main` (disabled: "local-work is up to date with origin/main"); `Check out local-work and rebase it onto origin/main interactively…`; —; `Create merge request…`; —; `Compare local-work with origin/main`: 7a's N5 step 4 drop menu, 6 items and 3 separators, 215 high ±2. Requests none since the mark.<br>**Method 4, after step 2** (signed out): `origin/topic/no-request`'s, `local-work`'s and the drop's menus as method 3's, each with `Create pull request…` in place of `Create merge request…`, 415, 519 and 215 high ±2. Requests since the start none. |  |  | `n01a-gitlab-remote-branch-form`, `n01b-gitlab-drop-menu` | **R** in part (14.4 "Requests and create", step 3): the real window right-clicks method 1's and method 2's branch row and clicks `PartN`'s menu item (N2's R step), reading no menu line and comparing no capture of the menu. The remote branch's, `local-work`'s and the drop's items are scripted only, as 7a's N5 (13.8). Azure DevOps' `origin/topic/no-request` and `local-work` items are read signed out and not clicked: they run 13.3's one start with Azure DevOps as `p`, which GitLab's same two items run clicked; the drop's click proves Azure DevOps' signed-out start (N8), and its remote-branch form is N7's on `long`. The other drop shapes are `RepositoryMenusProvidersTests`' (12.7). Varies by nature: the status bar's git box (D248); the title box's caret (`n01a`; named at WP-N's merge, whose inspection found it the one difference from the builder's copy). |
| N2 | Each provider's form (D351, D355, D362, D318; ruling 3; design 13.4, 13.3, 13.8; R): GitLab's `Create merge request` and Azure DevOps' `Create pull request`, From the prepared `topic/no-request` and Into `origin/main`, each with the listed repository's full name, titled `Try an idea`, in 7a's layout; GitLab's quick-actions note under the description; Azure DevOps' form 7a's card exactly; "hosting7b", one tab on the prepared `web-app`, then on the prepared `mobile-app`, signed in through Settings, Accounts, dark, 1100×700 | **Method 1**, after N1's step 2: 3. Click `Create merge request…`; wait for the form with `PullRequestBase` `origin/main` and the keyboard focus in `PullRequestTitle`. Screenshot (`n02a-gitlab-form`). (Step 4 is N6's.)<br>**Method 2**, after N1's step 2: 3. Click `Create pull request…`; wait for the form with `PullRequestBase` `origin/main` and the keyboard focus in `PullRequestTitle`. Screenshot (`n02b-azure-form`). | **Method 1, after step 3:** the form: its title (`CreatePullRequestDialogTitle`, and the view's automation name) `Create merge request`; no `DialogRepository`; `From` `topic/no-request` (its tooltip `topic/no-request`) and `PullRequestHeadRepository` `northfield/platform/web-app`, whole, its tooltip the same; no `PullRequestHeadNote` (an upstream of the same name, level, D313); `Into` `origin/main` (its tooltip the same) and `PullRequestBaseRepository` `northfield/platform/web-app`, whole, `Choose…` enabled and whole at the row's right (the rows measure about 275 and 331 of the column's 370 by the report's estimate, open point 38); `PullRequestTitle` `Try an idea`, focused, the caret at its end; `PullRequestDescription` empty, its placeholder `Description` shown; `PullRequestQuickActionsNote` `GitLab runs quick actions in the description, such as /close.` (`settingsNote`, one line, 4 below the description); `Draft` unticked and `PullRequestSentTitle` collapsed, taking no room; no `PullRequestError` and no `PullRequestOpenExisting`; `Cancel` and `Create` enabled, `Create` without a tooltip. The places as part N's conventions give GitLab's form: the card at x 320 to 780, centred vertically; `FormLayout`'s places relative to the card down to `PullRequestDescription` but the rows' children's x and width (part N's conventions: their y and height `FormLayout`'s, their x by the 8-px rule and each width its text's own, `Choose…` 82); `PullRequestDraft`, the buttons and the card's bottom lower than 7a's by the note's height plus 4. Requests since step 2's mark none (the form's A3 is the list's kept answer, 7.8); no operation; no text cut but by an ellipsis whose whole text a tooltip holds; no token on screen.<br>**Method 2, after step 3:** the title `Create pull request`; `From` `topic/no-request` and `Fernhill-Labs/Mobile App/mobile-app`, whole (about 323 of 370), no note; `Into` `origin/main` and `PullRequestBaseRepository` `Fernhill-Labs/Mobile App/mobile-app` by the cut rule (part N's conventions, "The form"): cut with "…" left of `Choose…` exactly when its natural width passes the room the row leaves it, measured from the controls (the row measures about 379 of 370 by the report's estimate, so a cut of a few pixels is expected; no constant fixes which, open point 38), its tooltip `Fernhill-Labs/Mobile App/mobile-app` either way, `Choose…` whole and placed as the rule gives; `PullRequestTitle` `Try an idea`, focused; no `PullRequestQuickActionsNote` and no `PullRequestSentTitle` (both collapsed: GitLab's alone, 13.4); the rest as method 1's. The card 386 high, every control at `FormLayout`'s places relative to it (±1) but the rows' children's x and width: their y and height `FormLayout`'s, their x by the 8-px rule and each width its text's own (or the cut rule's room; `Choose…` 82, as 7a's). Requests none since step 2's mark; no token on screen. |  |  | `n02a-gitlab-form`, `n02b-azure-form`; real window: `n02a-gitlab-form`, `n02b-azure-form` | **R** (14.4 "Requests and create", step 3; `Check_GitLab_requests_and_create` and Azure DevOps' launch), after part P's steps 1 and 2 in the same launch: a real click on `Local branches` › `topic`, a real right-click on its `no-request`, a real click on `PartN`'s menu item; wait until `CreatePullRequestDialog` shows, `PullRequestBase` reads `origin/main` and the focused `PullRequestTitle` holds `Try an idea` (`PartN.GitLab`, `PartN.AzureDevOps`); compared with `n02a-gitlab-form` or `n02b-azure-form`. Its state owes nothing to a hook or a held request (0.5). Where the Into row's repository text is cut is inspected (open point 38). Varies by nature: the git box; the title box's caret. |
| N3 | GitLab's Draft with its sent title and the note (ruling 20; design 13.4, 13.5, 13.8, 7.4, 4.3; D251): Draft ticked shows `Title on GitLab: Draft: Local change` under it, the quick-actions note still under the description; Create sends the title `Draft: Local change` with no `draft` member and the description as typed; GitLab's answer makes it a draft: `Created draft merge request !24` and the row `!24 Draft: Local change` with `Draft`; Azure DevOps' Draft sends `isDraft` true and the title as typed, shows neither line and reads `Created draft pull request !14`; "hosting7b", one tab on the prepared `web-app`, then on the prepared `mobile-app`, signed in, dark, 1100×700 | **Method 3**, after N4's step 6: 7. Click `Draft`. Click `PullRequestDescription`; type `A note for the reviewers.`. Screenshot (`n03a-gitlab-draft-sent-title`). (Step 8 is N4's: the POST held.) 9. Dispose N4's hold. Wait for the operation's end, for `web-app: listed 4 open pull requests of northfield/platform/web-app` twice since step 8's mark and for the watchers to be quiet. Screenshot (`n03b-gitlab-created-draft`). (Step 10 is N4's.)<br>**Method 4**, after N4's step 4: 5. Click `Draft`. Click `PullRequestDescription`; type `A note for the reviewers.`; read the form. (N4's step 6 creates and takes `n03c-azure-created-draft`.) | **Method 3, after step 7:** `Draft` ticked; `PullRequestSentTitle` shown 4 below `PullRequestDraft`: `Title on GitLab: Draft: Local change` (`settingsNote`; 13.4's `SentTitle(p, GitLabProvider.TitleFor(title.TrimEnd(), true))`); `PullRequestTitle` still `Local change` (the form sends the title as typed, ruling 20); `PullRequestDescription` `A note for the reviewers.`, `PullRequestQuickActionsNote` still 4 below it; the buttons and the card's bottom lower than after step 6 by the sent title's height plus 4 (±1). Requests none.<br>**Method 3, after step 9:** the operation's message and the status bar `Created draft merge request !24` (secondary), the offer `Open in browser` shown (13.5: on GitLab the draft is read from the answer's `draft`). Requests since step 8's mark: `project:northfield/platform/web-app` 304; then exactly one POST, 201, its `Body` exactly `source_branch` `local-work`, `target_branch` `main`, `title` `Draft: Local change` and `description` `A note for the reviewers.`, with no `draft`, `target_project_id` or `allow_collaboration` member (7.4: the title makes the draft); then the two `mrs:` reads, 200 then 304. The fake's !24: `opened`, `Title` `Draft: Local change`, `Draft` true, `Author` `ada`, `SourceBranch` `local-work`, `Source` and `Target` web-app, `TargetBranch` `main`, `Description` `A note for the reviewers.` (no quick action run, D370). web-app's hosting: `List` !24, !23, !22, !21; `ByRef` also `refs/heads/local-work` and `refs/remotes/origin/local-work` → !24. The section `Merge requests` `4`: `!24 Draft: Local change` (`Draft`, `ada`) first, then P1's three; its tooltip `!24 Draft: Local change`, a line break, `ada · local-work → main · Draft`. `Local branches` › `local-work`'s `RefPullRequest` `!24`, its tooltip `Merge request !24: Draft: Local change`. The graph's row 0: its first label `local-work` (`LocalBranch`) with `Badge` (24, `Draft: Local change`, true, GitLab), drawn `!24`; `origin/local-work` on the same row without one; the other badges as P1's. No token on screen.<br>**Method 4, after step 5:** `Draft` ticked; no `PullRequestSentTitle` and no `PullRequestQuickActionsNote` (13.4: GitLab's alone), the card's height unchanged by the tick; `PullRequestDescription` `A note for the reviewers.`. Requests none.<br>**Method 4, after N4's step 6:** the operation's message and the status bar `Created draft pull request !14` with the offer. The POST's `Body` exactly `sourceRefName` `refs/heads/local-work`, `targetRefName` `refs/heads/main`, `title` `Local change`, `description` `A note for the reviewers.` and `isDraft` true, no `forkSource`; answered 201. The fake's !14: `active`, `Title` `Local change`, `IsDraft` true, `Creator` Ada Lovelace, `SourceBranch` `local-work`, `TargetBranch` `main`, `Fork` null, `Description` `A note for the reviewers.`. The section `Pull requests` `4`: `!14 Local change` (`Draft`, `Ada Lovelace`) first; its tooltip `!14 Local change`, a line break, `Ada Lovelace · local-work → main · Draft`. `local-work`'s `!14` (tooltip `Pull request !14: Local change`); the graph's row 0's `local-work` with (14, `Local change`, true, AzureDevOps), drawn `!14`. |  | n/a | `n03a-gitlab-draft-sent-title`, `n03b-gitlab-created-draft`, `n03c-azure-created-draft` | n/a: 13.8's R steps are N2's and N6's, which create from `topic/no-request` without Draft. `[Draft]`, `(Draft)`, a title already `Draft:` and the line while unticked are `CreateRequestDialogProvidersTests`' (13.7). Varies by nature: the git box; the caret (`n03a`). |
| N4 | `local-work` pushed first (D313, D312, D315, D255, D350; design 13.5, 13.8, 8.2; 7a design 12.5): `local-work`, on no remote, shows that Create pushes it to `origin/local-work` first and sets that as its upstream, in its provider's name; Create pushes it through the app's helper with `x-access-token`, then posts, as one operation; while the POST runs the form reads `Creating…` and the status bar `Creating merge request…` without Cancel; afterwards `branch.local-work.*`, the branch on the fake and the request's ref there; "hosting7b", one tab on the prepared `web-app`, then on the prepared `mobile-app`, signed in, dark, 1100×700 | **Method 3**, after N1's step 5: 6. Click the drop menu's `Create merge request…`; wait for the form with `PullRequestBase` `origin/main`. Screenshot (`n04a-gitlab-pushed-first`). (Step 7 is N3's.) 8. Mark the requests, the git requests and the log; take a quiet call mark. `served.GitLab.HoldNext("create:northfield/platform/web-app")`. Click `Create`; wait for the hold's `Arrived`, for the status bar `Creating merge request…` and for the watchers to be quiet. In the bare folder, through `GitRunner` with `TempRepo.IsolatedEnvironment`: `git rev-parse refs/heads/local-work`. Screenshot (`n04b-gitlab-creating`). Press Esc. (Step 9 is N3's: the hold released, the create's end.) 10. Close the app. In `web-app/`, as in N8's step 1: `git config --get branch.local-work.remote`; `git config --get branch.local-work.merge`; `git rev-parse refs/heads/local-work refs/remotes/origin/local-work`; `git config --local --list`; in the bare folder: `git rev-parse refs/heads/local-work refs/merge-requests/24/head`.<br>**Method 4**, after N8's step 3: 4. Screenshot (`n04c-azure-pushed-first`), the drop's form after the sign-in. (Step 5 is N3's.) 6. Mark the requests, the git requests and the log; take a quiet call mark. Click `Create`; wait for the operation's end, for `mobile-app: listed 4 open pull requests of Fernhill-Labs/Mobile App/mobile-app` twice since the mark and for the watchers to be quiet. Screenshot (`n03c-azure-created-draft`, N3's). 7. Close the app. In `mobile-app/` and its bare folder as method 3's step 10, with `git rev-parse refs/heads/local-work refs/pull/14/merge` and `git rev-parse --verify --quiet refs/pull/14/head` in the bare folder. | **Method 3, after N8's step 1:** `git config --get-regexp ^branch\.local-work\.` exits 1 and prints nothing; `refs/heads/local-work` is not in the bare folder (exit 1).<br>**Method 3, after step 6:** the form: `From` `local-work` `northfield/platform/web-app`; `PullRequestHeadNote` `Not on GitLab yet: Create pushes it to origin/local-work first and sets that as its upstream.` (`settingsNote`, under the head, wrapping; D313: no upstream); `Into` `origin/main` `northfield/platform/web-app`, given (never `Reading GitLab…`, 7a's 12.4); `PullRequestTitle` `Local change`, focused; `PullRequestQuickActionsNote` under the description; `Draft` unticked; `Create` enabled; the rest as N2's GitLab form. The places: `From` at N2's place; `PullRequestHead` at N2's x and y, as wide as `local-work`; `PullRequestHeadRepository` at N2's y, 8 right of the head (the 8-px rule), as wide as its text; the note is in the From row, so the Into row (`Into`, `PullRequestBase`, `PullRequestBaseRepository`, `PullRequestBaseChooseButton`), the title box and everything below them lower than N2's GitLab form by the head note's height plus 4 (±1). Requests since step 5's mark none.<br>**Method 3, after step 8:** the form creating (part N's conventions), still open after Esc (12.4); the status bar `Creating merge request…`, the progress bar indeterminate, `OperationCancelButton` hidden (D315: the cancel window ends before the POST). Git calls since the quiet mark, in order (`AssertCalledInOrder`): the push guard's `git config --null --get-regexp ^remote\.` (13.5, WP-C's guards), then `git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work` (D313); no other `git push`. Git requests on `northfield/platform/web-app.git` since the mark: the receive-pack's `POST /northfield/platform/web-app.git/git-receive-pack` answered 200 with `GivenUserName` `x-access-token` (D255, D350: through the app's helper), every request answered 200 with that user name; none with another user name; below 2.46 a first one without credentials answered 401 (part N's "Git's version"); no credentials dialog. In the bare folder `refs/heads/local-work` at `Local change`'s SHA: the push ended before the POST. API requests since the mark: exactly one, `GET /api/v4/projects/northfield%2Fplatform%2Fweb-app` (route `project:northfield/platform/web-app`), `Bearer`, `Login` `ada`, 304 (A5's read of the source after the push; the held POST is recorded when it goes on, 2.3). Behind the form the refs as before the push: the queue pauses the watcher while the operation runs (D71) and the refs are read at its end, so `Remotes` › `origin` holds no `local-work` and `local-work` has no upstream.<br>**Method 3, after N3's step 9:** one operation since step 8's mark, `Create merge request for local-work`, Succeeded (N3's message); no dialog at any time. The queue's records since step 6 (`Operations.Snapshot()`): that one operation, the push among its calls (its call carries the operation's `OperationId`); no operation `Push local-work` (7a's open point 47). The left panel: `Remotes` › `origin` holds `local-work`; `Local branches` › `local-work`'s `!24`. The log: `web-app: Create merge request for local-work: into northfield/platform/web-app main, draft yes, push to origin/local-work first` (Info).<br>**Method 3, after step 10:** `branch.local-work.remote` `origin`; `branch.local-work.merge` `refs/heads/local-work`; `refs/heads/local-work` and `refs/remotes/origin/local-work` one SHA, `Local change`'s; `git config --local --list` holds no `credential` key and no URL with a user or a password, and its remote URLs are unchanged: `remote.origin.url` exactly `WithPorts(WebAppUrl, served)` and `remote.upstream.url` exactly `WithPorts(WebAppUpstreamUrl, served)`; in the bare folder `refs/heads/local-work` and `refs/merge-requests/24/head` that SHA.<br>**Method 4, after N8's step 1:** in `mobile-app/` `git config --get-regexp ^branch\.local-work\.` exits 1; `refs/heads/local-work` is not in `azure-devops/Fernhill-Labs/Mobile App/_git/mobile-app` (exit 1).<br>**Method 4, after step 4:** the drop's form after the sign-in: `From` `local-work` `Fernhill-Labs/Mobile App/mobile-app`; `PullRequestHeadNote` `Not on Azure DevOps yet: Create pushes it to origin/local-work first and sets that as its upstream.`; `Into` `origin/main`, given, its repository text and `Choose…` by the cut rule, as N2's method 2's (the same texts; the note moves the row down and leaves its widths); `PullRequestTitle` `Local change`; no quick-actions note and no sent-title line; `Create` enabled. Requests none since N8's step 3's waits.<br>**Method 4, after step 6:** one operation, `Create pull request for local-work`, Succeeded (N3's message). Git calls since the quiet mark, in order: the push guard's read, then `git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work`; no other `git push`. Git requests on `Fernhill-Labs/Mobile%20App/_git/mobile-app` since the mark: the receive-pack's `POST` answered 200 with `GivenUserName` `x-access-token`, every request answered 200 with that user name; none with another; below 2.46 the first without credentials and the second with `Fernhill-Labs` and an empty password, each 401, before the helper's (B7's order); no credentials dialog. API requests since the mark: Azure DevOps' exactly the POST, 201 (N3's `Body`), then the two `pulls:` reads, 200 and 200; nothing before the POST; GitLab's and GitHub's none. The queue's records: one operation, the push among its calls; no `Push local-work`. `Remotes` › `origin` holds `local-work`; `local-work`'s `!14`. The log: `mobile-app: Create pull request for local-work: into Fernhill-Labs/Mobile App/mobile-app main, draft yes, push to origin/local-work first`.<br>**Method 4, after step 7:** in `mobile-app/`: `branch.local-work.remote` `origin`; `branch.local-work.merge` `refs/heads/local-work`; `refs/heads/local-work` and `refs/remotes/origin/local-work` one SHA, `Local change`'s; `git config --local --list` holds no `credential` key and no URL with a password, and its remote URLs are unchanged: `remote.origin.url` exactly `WithPorts(MobileAppUrl, served)`, whose `Fernhill-Labs@` is the scenario's user in the URL (2.6, B7), not one the push wrote, and no other remote; in the bare folder `refs/heads/local-work` at that SHA and `refs/pull/14/merge` (a merge commit of `main` and `local-work` from the list's ref sync, 3.5); `refs/pull/14/head` absent (exit 1, D361). |  | n/a | `n04a-gitlab-pushed-first`, `n04b-gitlab-creating`, `n04c-azure-pushed-first` | n/a: the R twins create from `topic/no-request`, which pushes nothing (open point 35); a held request builds no R state (0.5). The real helper run by Git for Windows' `sh` for both hosts is B7's real-window clones'. Behind the form in `n04b` the refs from before the push, as 7a's `n02b`. The push's undo entry is not asserted (7a's open point 54). Varies by nature: the git box; the caret (`n04a`, `n04c`; `n04c` named at WP-N's merge, where the caret was in its off phase); the indeterminate progress bar's moving segment (`n04b`). |
| N5 | Already open and a missing branch in the App's words (D312, D316, D338, D362; design 13.5, 13.2, 13.8, 3.3, 4.3; 16.1): with the head's branch deleted on the fake behind the app, Create is refused in the App's words, GitLab's `GitLab has no branch topic/no-request in northfield/platform/web-app to merge from. Fetch, then try again.` and Azure DevOps' `Azure DevOps cannot find topic/no-request or main. Fetch, then try again.`; with the branch back and a request from it opened on the fake after the list was read, Create's 409, the list read again, `Merge request !24 is already open from topic/no-request into origin/main.` or `Pull request !14 is already open from topic/no-request into origin/main.` and `Open merge request !24` or `Open pull request !14`, which opens it; "hosting7b", one tab on the prepared `web-app`, then on the prepared `mobile-app`, signed in, dark, 1100×700 | **Method 5** (`Check_N5_gitlab_missing_branch_and_already_open`): 1. `ServeAsync()`. `PrepareRequestsAsync(served.MobileApp, GitLab)`. `OpenAsync(data, served, WebApp)` (GitLab signed in through the session); wait for `web-app: listed 3 open pull requests of northfield/platform/web-app` since the start and for the watchers to be quiet. In the bare folder, through `GitRunner` with `TempRepo.IsolatedEnvironment`: `git rev-parse refs/heads/topic/no-request` (its SHA kept), then `git update-ref -d refs/heads/topic/no-request`. 2. Click `Local branches` › `topic`; right-click its `no-request`; click `Create merge request…`; wait for the form with `PullRequestBase` `origin/main`. Mark the requests and the log; take a quiet call mark. Click `Create`; wait for the operation's end and for the watchers to be quiet. Screenshot (`n05a-gitlab-missing-branch`). 3. In the bare folder: `git update-ref refs/heads/topic/no-request <the kept SHA>`. `served.GitLab.AddMergeRequest(served.GitLab.Project(Scenarios.Hosting7bWebApp), "Try an idea", "octo", "topic/no-request", "main")`. Mark the requests and the log; take a quiet call mark. Click `Create`; wait for the operation's end, for `web-app: listed 4 open pull requests of northfield/platform/web-app` since the mark and for the watchers to be quiet. Screenshot (`n05b-gitlab-already-open`). 4. Mark the requests. Click `Open merge request !24` (`PullRequestOpenExisting`).<br>**Method 6** (`Check_N5_N7_azure_missing_branch_already_open_and_long_names`): 1. As method 5's, prepared for Azure DevOps, `OpenAsync(data, served)` (one tab on `mobile-app`, Azure DevOps signed in through the session), waiting for `mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app`; the bare folder `azure-devops/Fernhill-Labs/Mobile App/_git/mobile-app`. 2. As method 5's, with `Create pull request…`. Screenshot (`n05c-azure-missing-branch`). 3. Restore the branch as method 5's. `served.AzureDevOps.AddPullRequest(served.AzureDevOps.Repository(Organization, Project, "mobile-app"), "Try an idea", served.AzureDevOps.User(Organization, OctoName), "topic/no-request", "main")`. Mark the requests and the log; take a quiet call mark. Click `Create`; wait for the operation's end, for `mobile-app: listed 4 open pull requests of Fernhill-Labs/Mobile App/mobile-app` since the mark and for the watchers to be quiet. Screenshot (`n05d-azure-already-open`). 4. Mark the requests. Click `Open pull request !14`. (Steps 5 to 8 are N7's.) | **Method 5, after step 1:** the section `3`; `topic` › `no-request` without `!n`. In the bare folder `git rev-parse --verify --quiet refs/heads/topic/no-request` exits 1; `web-app/`'s `refs/remotes/origin/topic/no-request` unchanged (nothing fetched).<br>**Method 5, after step 2:** the menu's `Create merge request…` as N1's, the form as N2's GitLab form. One operation, `Create merge request for topic/no-request`, Failed; no dialog but the form (D312). Git calls since the quiet mark: no `git push` (level with its upstream, D313). Requests since the mark: `project:northfield/platform/web-app` 304; then exactly one POST, its `Body` N6's (`source_branch` `topic/no-request`, `target_branch` `main`, `title` `Try an idea`, `description` empty), answered 400 `{"message":{"source_branch":["does not exist"]}}`; nothing more (no refusal but "already open" reads the list again, D289). The form: `PullRequestError` `GitLab has no branch topic/no-request in northfield/platform/web-app to merge from. Fetch, then try again.` (the client's `HeadMissing` from the body's key, 13.5); no `PullRequestOpenExisting`; every control enabled, `Create` enabled; no `PullRequestHeadNote` (the clone's refs still say level). The status bar `Create merge request for topic/no-request failed` (danger), no offer. Behind the form the section still `3`. The log since the mark: `GitLab: POST /api/v4/projects/5001/merge_requests failed: 400 (request {id}).`, `GitLab: creating a pull request on northfield/platform/web-app failed: Validation, source_branch does not exist (request {id})` and `web-app: Create merge request for topic/no-request: the request to create it failed: Validation, source_branch does not exist (request {id})`. The fake holds !21 to !23 only.<br>**Method 5, after step 3:** one more operation, Failed. Git calls: no `git push`. Requests since the mark: `project:` 304; one POST with the same `Body`, answered 409 `{"message":["Another open merge request already exists for this source branch: !24"]}`; then exactly one `mrs:` read, 200 (D316's re-read, the list changed); nothing more. The form: `PullRequestError` `Merge request !24 is already open from topic/no-request into origin/main.`; under it `PullRequestOpenExisting` `Open merge request !24`; every control enabled. The status bar `Create merge request for topic/no-request failed` (danger). Behind the form the section `4`, `!24 Try an idea` (`octo`) first; `topic` › `no-request`'s `!24`. The log: the core's `… failed: 409 (request {id}).` and the session's `… failed: Validation, Another open merge request already exists for this source branch: !24 (request {id})`; one `web-app: listed 4 open pull requests of northfield/platform/web-app`. The fake holds !21 to !24, `!24` octo's alone.<br>**Method 5, after step 4:** `Launcher.Requests` exactly one `OpenUrl` `http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests/24` (`WithPorts`); the form closed (it completes with false, 7a's 12.4); requests none; the status bar unchanged.<br>**Method 6, after step 1:** as method 5's, in its bare folder and `mobile-app/`; the section `Pull requests` `3`.<br>**Method 6, after step 2:** the form as N2's Azure DevOps form. One operation, `Create pull request for topic/no-request`, Failed; no `git push`. Requests since the mark: Azure DevOps' exactly one POST, its `Body` N6's Azure DevOps body, answered 400 with the typeKey `GitPullRequestCannotBeActivated` and TF401398's message; nothing before or after it. The form: `PullRequestError` `Azure DevOps cannot find topic/no-request or main. Fetch, then try again.` (`BranchesMissing`: TF401398 names neither branch, 13.5); no `PullRequestOpenExisting`. The status bar `Create pull request for topic/no-request failed` (danger). The log: `Azure DevOps: POST /Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests failed: 400 (request {id}).`, `Azure DevOps: creating a pull request on Fernhill-Labs/Mobile App/mobile-app failed: Validation, TF401398: The pull request cannot be activated because the source and/or the target branch no longer exists, or the requested refs are not branches (request {id})` and the App's line with that message.<br>**Method 6, after step 3:** one POST, answered 409 with the typeKey `GitPullRequestExistsException` and `TF401179: An active pull request for the source and target branch already exists.`; then exactly one `pulls:` read, 200. The form: `Pull request !14 is already open from topic/no-request into origin/main.` and `Open pull request !14`. Behind it the section `Pull requests` `4`, `!14 Try an idea` (`Octo Cat`) first; `topic` › `no-request`'s `!14`.<br>**Method 6, after step 4:** `Launcher.Requests` exactly one `OpenUrl` `http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/14`; the form closed; requests none. |  | n/a | `n05a-gitlab-missing-branch`, `n05b-gitlab-already-open`, `n05c-azure-missing-branch`, `n05d-azure-already-open` | n/a: 13.8's R steps are N2's and N6's; a state broken behind the app builds no R state (0.5). 403s, 422s (the same branch, not a fork), a 400 naming the target or both, a 404, a server error and a lost answer are 13.7's classes'. Varies by nature: the git box. |
| N6 | The request listed with its badge, `Created … !n` and the offer (D293, D289, D317, D355; design 13.5, 13.8, 12.3, 12.6; ruling 13; R): Create makes `!24` on GitLab and `!14` on Azure DevOps without a push; then the list's row first, the branch row's and the graph's `!n`, `Created merge request !24` or `Created pull request !14` and `Open in browser`, whose click hands the request's page to the launcher; `Open merge request !24` or `Open pull request !14` in the branch's menu, none in the section's; the offer gone at the next operation; "hosting7b", one tab on the prepared `web-app`, then on the prepared `mobile-app`, signed in through Settings, Accounts, dark, 1100×700 | **Method 1**, after N2's step 3: 4. Mark the requests, the git requests and the log; take a quiet call mark. Click `Create`; wait for the operation's end, for `web-app: listed 4 open pull requests of northfield/platform/web-app` twice since the mark and for the watchers to be quiet. Screenshot (`n06a-gitlab-created`). 5. Mark the requests. Click `Open in browser` (`OperationOfferButton`). 6. Right-click `Local branches` › `topic` › `no-request`; read; press Esc. Right-click the header `Merge requests` at its centre; read; press Esc. 7. Mark the requests. Double-click `!21`'s row (P11's checkout); wait for its end and for the watchers to be quiet.<br>**Method 2**, after N2's step 3: 4. As method 1's, waiting for `mobile-app: listed 4 open pull requests of Fernhill-Labs/Mobile App/mobile-app` twice. Screenshot (`n06b-azure-created`). 5. Mark the requests. Click `Open in browser`. 6. Right-click `topic` › `no-request`; read; Esc. Right-click `Pull requests` at its centre; read; Esc. 7. Mark the requests. Double-click `!11`'s row; wait for its end and for the watchers to be quiet. | **Method 1, after step 4:** one operation, `Create merge request for topic/no-request`, Succeeded with `Created merge request !24`; the form closed; no dialog at any time. Git calls since the quiet mark: no `git push` (D313); git requests none. Requests since the mark: GitLab's `GET /api/v4/projects/northfield%2Fplatform%2Fweb-app` (route `project:northfield/platform/web-app`) 304; then exactly one POST (`POST /api/v4/projects/5001/merge_requests`, route `create:northfield/platform/web-app`), `Bearer`, `Login` `ada`, 201, its `Body` exactly `source_branch` `topic/no-request`, `target_branch` `main`, `title` `Try an idea` and `description` empty, with no `draft`, `target_project_id` or `allow_collaboration` member (7.4); then the two `mrs:northfield/platform/web-app` reads, 200 then 304; none 401; Azure DevOps' and GitHub's none. The fake's !24: `opened`, `Title` `Try an idea`, `Draft` false, `Author` `ada`, `SourceBranch` `topic/no-request`, `Source` and `Target` web-app, `TargetBranch` `main`, `Description` empty; `refs/merge-requests/24/head` in the bare folder at `topic/no-request`'s SHA. web-app's hosting `Loaded`: `List` !24, !23, !22, !21, `MoreExist` false; `ByRef` also `refs/heads/topic/no-request` and `refs/remotes/origin/topic/no-request` → !24. The status bar: `OperationStatus` `Created merge request !24` (secondary), its tooltip the same; the offer shown, its text and automation name `Open in browser`, its left 6 right of the text's right; no progress bar, no Cancel. The section `Merge requests` `4`: `!24 Try an idea` (`ada`, no `Draft`) first, then P1's three; its tooltip `!24 Try an idea`, a line break, `ada · topic/no-request → main`. `topic` › `no-request`'s `RefPullRequest` `!24`, its tooltip `Merge request !24: Try an idea`. The graph's row 1: its first label `topic/no-request` (`LocalBranch`) with `Badge` (24, `Try an idea`, false, GitLab), drawn `!24` whole after the icon; `origin/topic/no-request` on the same row, shown or folded into `+1` as the column decides, without a badge of its own; the other badges as P1's (`!21` on row 3's `feature/search`, `!22` on row 2's `origin/docs/readme`); every label inside the 130-px column. The log since the mark: `web-app: Create merge request for topic/no-request: into northfield/platform/web-app main, draft no, no push` and `web-app: Create merge request for topic/no-request: created !24 in northfield/platform/web-app.` (Info, 13.5), `web-app: listed 4 open pull requests of northfield/platform/web-app` twice; no Warning. No text cut but by an ellipsis whose whole text a tooltip holds; no token on screen.<br>**Method 1, after step 5:** `Launcher.Requests` exactly one `OpenUrl` `http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests/24` (the request's `web_url`, `WithPorts`); requests none; no operation; the status bar and the offer unchanged.<br>**Method 1, after step 6:** `topic/no-request`'s menu: N1's step 2 lines with `Open merge request !24` in place of `Create merge request…` (7a's ruling 11, 12.4). The section's menu: header `Merge requests`; `Refresh merge requests` (highlighted); `Open merge requests in browser`; 84 high ±2: no create item (7a's 0.1). Requests none.<br>**Method 1, after step 7:** the checkout of `feature/search` (P11's double-click), the status bar `Checked out feature/search`; the offer hidden, taking no room (D317: it goes when the user's next operation starts); requests none (a local operation reads no list).<br>**Method 2, after step 4:** one operation, `Create pull request for topic/no-request`, Succeeded with `Created pull request !14`; the form closed; no dialog. No `git push`; git requests none. Requests since the mark: Azure DevOps' exactly one POST (route `create:fernhill-labs/mobile app/mobile-app`, `POST /Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests`, query `api-version=7.1`), `BasicEmptyUser`, `Login` Ada's, 201 (`CreateStatus`, MEASURED 2026-11-05), its `Body` exactly `sourceRefName` `refs/heads/topic/no-request`, `targetRefName` `refs/heads/main`, `title` `Try an idea`, `description` empty and `isDraft` false, no `forkSource`; nothing before it; then the two `pulls:` reads, 200 and 200; GitLab's and GitHub's none. The fake's !14: `active`, `Title` `Try an idea`, `IsDraft` false, `Creator` Ada Lovelace, `SourceBranch` `topic/no-request`, `TargetBranch` `main`, `Fork` null; after the list's ref sync `MergeStatus` `succeeded` and `refs/pull/14/merge` in the bare folder, no `refs/pull/14/head` (D361). mobile-app's hosting: `List` !14, !13, !12, !11; `ByRef` also `refs/heads/topic/no-request` and `refs/remotes/origin/topic/no-request` → !14. The status bar `Created pull request !14` (secondary) with the offer, placed as method 1's. The section `Pull requests` `4`: `!14 Try an idea` (`Ada Lovelace`) first; its tooltip `!14 Try an idea`, a line break, `Ada Lovelace · topic/no-request → main`. `topic` › `no-request`'s `!14` (tooltip `Pull request !14: Try an idea`). The graph's row 1's `topic/no-request` with (14, `Try an idea`, false, AzureDevOps), drawn `!14`, `origin/topic/no-request` without one; the other badges as P1's. The log: `mobile-app: Create pull request for topic/no-request: into Fernhill-Labs/Mobile App/mobile-app main, draft no, no push`, `mobile-app: Create pull request for topic/no-request: created !14 in Fernhill-Labs/Mobile App/mobile-app.` and `mobile-app: listed 4 open pull requests of Fernhill-Labs/Mobile App/mobile-app` twice; no Warning.<br>**Method 2, after step 5:** `Launcher.Requests` exactly one `OpenUrl` `http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/14` (the provider's address, 6.6); requests none.<br>**Method 2, after step 6:** `topic/no-request`'s menu with `Open pull request !14` in place of `Create pull request…`; the section's menu `Pull requests`, `Refresh pull requests` (highlighted), `Open pull requests in browser`, 84 high ±2. Requests none.<br>**Method 2, after step 7:** `Checked out feature/sign-in`; the offer hidden; requests none. |  |  | `n06a-gitlab-created`, `n06b-azure-created`; real window: `n06a-gitlab-created`, `n06b-azure-created` | **R** (14.4 "Requests and create", step 4), after N2's R step: mark the fake and the log; a real click on `Create`; wait for `AppLogFile.OperationEnded` with `PartN`'s operation title, Succeeded, and for `PullRequestsListed` twice since the mark; since the mark one request of `PartN`'s route key, 201, its `Body` exactly `PartN`'s body (its provider's own members, as the scripted step 4 gives them, and no others), and no receive-pack request (the branch is not pushed, open point 35); on GitLab the `project:` read before it the only other request before the POST; the status bar `PartN`'s status text, `OperationOfferButton` named `Open in browser` (never clicked, 14.2); `PullRequestRowNames()` first `PartN`'s new row; compared with `n06a-gitlab-created` or `n06b-azure-created`; `AssertCleanEnd`. Steps 5 to 7 are scripted only. Where `topic/no-request`'s name is cut beside its badge is inspected (part P's open point 26). Varies by nature: the git box. |
| N7 | Long repository texts cut with their tooltips (ruling 3; design 13.4, 13.8; D350, D318): a head and a base in a repository whose full name passes the column, `Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service`, each cut with "…" in its row, its tooltip whole, `Choose…` whole inside the card; "hosting7b", one tab on the prepared `mobile-app` with a remote `long` added, signed in, dark, 1100×700 | **Method 6**, after N5's step 4: 5. Wait until the form has closed. In `mobile-app/`, through `GitRunner` with `TempRepo.IsolatedEnvironment`: `git remote add long` with `WithPorts("http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/customer-onboarding-and-identity-verification-service", served)`; `git update-ref refs/remotes/long/main` at `origin/main`'s SHA; `git update-ref refs/remotes/long/topic/no-request` at `origin/topic/no-request`'s SHA. Wait until the left panel's `Remotes` holds `long` and the watchers are quiet. Mark the requests. 6. Open `Remotes` › `long` and its folder `topic` (each clicked while closed); right-click its `no-request`; read; click `Create pull request…`; wait for the form. Click `Choose…`; wait for the Pick; read it. Click `long/main`; click `Choose`. Screenshot (`n07a-azure-long-names`). 7. Rest the pointer on `PullRequestHeadRepository` until its tooltip shows. Screenshot (`n07b-azure-long-name-tooltip`, the pointer kept). Rest it on `PullRequestBaseRepository` until its tooltip shows; read it. 8. Press Esc. | **Method 6, after step 5:** `Remotes` holds `long` (`main` and the folder `topic`) beside `origin`; the hosting's `Remotes` also `long` → (Azure DevOps, `127.0.0.1:47397`, `Fernhill-Labs/Mobile App`, `customer-onboarding-and-identity-verification-service`); `RequestRemotes` exactly `long` and `origin`; `Repository` still `origin`'s (the first recognised in `origin`, `upstream`, name order, ruling 5); the section `Pull requests` `4`.<br>**Method 6, after step 6:** `long/topic/no-request`'s menu holds `Create pull request…` in a remote branch's place (13.3: `long` is a key of `RequestRemotes`). The form: `From` `long/topic/no-request`, whole, its tooltip the same; `PullRequestHeadRepository` `Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service` cut with "…" (one line, `HasCollapsed`), its right edge at the column's right (x 758, ±1); no `PullRequestHeadNote` (a remote branch is never pushed); `Into` at first `origin/main` `Fernhill-Labs/Mobile App/mobile-app` (A3's default, D318). The Pick: title `Choose the branch to merge into`; items in the refs' order `long/main`, `long/topic/no-request`, `origin/docs/guide`, `origin/feature/sign-in`, `origin/main` (selected), `origin/topic/no-request`, the two `long/` items with the help text `Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service` and the others `Fernhill-Labs/Mobile App/mobile-app` (`DetailOf`); a row's detail drawn as far as phase 6's row leaves it room. After `Choose`: `Into` `long/main`, `PullRequestBaseRepository` the long name cut with "…" left of `Choose…`, which stays whole inside the card, its right edge at the column's right (x 758, ±1), as part N's conventions give it; no `PullRequestError` (another branch, D318); `Create` enabled; the card 386 high (a cut text keeps one line). Requests since step 5's mark none (A3 kept, the Pick reads the refs).<br>**Method 6, after step 7:** `PullRequestHeadRepository`'s tooltip shown, exactly `Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service`, whole; `PullRequestBaseRepository`'s the same.<br>**Method 6, after step 8:** no form; no operation; requests none. |  | n/a | `n07a-azure-long-names`, `n07b-azure-long-name-tooltip` | n/a: 14.1 (the real window takes the R twins' state alone, and no long name is in it). A 60-character repository cut, its tooltip whole and `Choose…` inside the card are also `CreateRequestDialogProvidersTests`' (13.7). The scenario's GitLab rows fit; a nested GitLab path past the column is cut by the same rule. Varies by nature: the git box; the caret. |
| N8 | Signed out, that provider's sign-in first (D257, D306, D350; design 13.3, 13.8, 10.5; 7a design 8.5): signed out, `Create merge request…` on `origin/topic/no-request` opens `Add GitLab account` first, `Cancel` there stopping with no form and no request, and a sign-in going on to the form; the drop's `Create pull request…` opens `Add Azure DevOps account` prefilled with `Fernhill-Labs`, and its sign-in goes on to the drop's form with its base given; "hosting7b", one tab on the prepared `web-app`, then on the prepared `mobile-app`, signed out, dark, 1100×700 | **Method 3** (`Check_N8_N1_N4_N3_gitlab_signed_out_drop_pushed_first_and_draft`): 1. `ServeAsync()`. `PrepareRequestsAsync(served.MobileApp, GitLab)`. `OpenAsync(data, served, WebApp, signIn: [])`; wait for `web-app`'s `HostingState.PullRequests` `SignedOut`, for the section's message row `Sign in to GitLab to see merge requests…` and for the watchers to be quiet. In `web-app/`, through `GitRunner` with `TempRepo.IsolatedEnvironment`: `git config --get-regexp ^branch\.local-work\.`; in the bare folder: `git rev-parse --verify --quiet refs/heads/local-work`. Mark the requests, the calls and the log. (Step 2 is N1's.) 3. Click the menu's `Create merge request…`; wait for `SignInDialog` with the keyboard focus in `SignInToken`. Screenshot (`n08a-gitlab-sign-in-first`). Click the dialog's `Cancel`. 4. Mark the requests. Right-click `Remotes` › `origin` › `topic` › `no-request`; click `Create merge request…`; wait for `SignInDialog`. `TypeToken(GitLabToken)`; press Enter; wait for the form with `PullRequestBase` `origin/main`, for `web-app: listed 3 open pull requests of northfield/platform/web-app` since the mark and for the watchers to be quiet. Screenshot (`n01a-gitlab-remote-branch-form`, N1's). Press Esc.<br>**Method 4** (`Check_N8_N1_N4_N3_azure_signed_out_drop_pushed_first_and_draft`): 1. As method 3's, prepared for Azure DevOps, `OpenAsync(data, served, signIn: [])`, the message row `Sign in to Azure DevOps to see pull requests…`, in `mobile-app/` and its bare folder. (Step 2 is N1's.) 3. Click the drop menu's `Create pull request…`; wait for `SignInDialog` with `SignInOrganization` `Fernhill-Labs` and the keyboard focus in `SignInToken`. Screenshot (`n08b-azure-sign-in-first`). Mark the requests. `TypeToken(AzureDevOpsToken)`; press Enter; wait for the form with `PullRequestBase` `origin/main`, for `mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app` since the mark and for the watchers to be quiet. | **Method 3, after step 1:** requests since the start none (D257); the hosting `SignedOut`, its `Repository` GitLab's `northfield/platform/web-app` (ruling 24), `RequestRemotes` exactly `origin`; the section `Merge requests` `0` with its message row.<br>**Method 3, after step 3:** part A's `Add GitLab account` dialog (the window's entry, 10.5; A5's texts, links and places) stacked over `web-app`'s window under its own backdrop, no settings screen; `SignInToken` empty and focused; no `CreatePullRequestDialog`; requests none. After `Cancel`: no dialog and no form; still signed out; requests since the mark none; calls exactly `probe` (the dialog's keychain question, as 7a's N6); no operation; the section as after step 1.<br>**Method 3, after step 4:** requests since the mark: GitLab's `GET /api/v4/user` (route `user`) 200, then `project:northfield/platform/web-app` 200 (the hosting's A3, which the form's start shares, 7.8), `mrs:northfield/platform/web-app` 200 and `project:ada/web-app` 200, each `Bearer`, `Login` `ada`; nothing else, none 401; GitHub's and Azure DevOps' none; the calls gain `save gitlab:127.0.0.1:47398:ada`. The form N1 gives (`n01a`), over the section's three rows. After Esc no form and no POST.<br>**Method 4, after step 1:** requests none; `SignedOut`, its `Repository` Azure DevOps' `Fernhill-Labs/Mobile App/mobile-app`; the message row `Sign in to Azure DevOps to see pull requests…`.<br>**Method 4, after step 3:** the drop's `Create pull request…` opens part A's `Add Azure DevOps account` dialog (A6's texts and places) over `mobile-app`'s window, no settings screen, `SignInOrganization` `Fernhill-Labs` (the `OwnerRoot` of the tab's origin, 10.5), the focus in `SignInToken`, `Sign in` disabled while the token box is empty; no form; requests none. After the sign-in: requests since the mark Azure DevOps' `connection:fernhill-labs` (query `api-version=7.1-preview.1`), `areas`, `repo:fernhill-labs/mobile app/mobile-app` and `pulls:fernhill-labs/mobile app/mobile-app`, each 200, `BasicEmptyUser`, nothing else; the calls since step 1's mark exactly `probe` (the dialog's keychain question) and `save azure-devops:127.0.0.1:47397:fernhill-labs`; then the drop's form with `Into` `origin/main`, given (N4's `n04c`; open point 45). |  | n/a | `n08a-gitlab-sign-in-first`, `n08b-azure-sign-in-first` | n/a: 14.4 (the real window signs in through Settings before its create). A cancelled Azure DevOps sign-in is `CreateRequestOperationProvidersTests`' ("signed out, that provider's sign-in first", 13.7). Varies by nature: the git box; the caret (`n08a`, `n08b`). |
| N9 | The form at 1920×1080 and in light (design 13.8, 13.4; no new token): GitLab's form, its quick-actions note included, centred at 1920×1080, dark, then light after the toolbar's Theme switch; "hosting7b", one tab on the prepared `web-app`, signed in, dark then light, 1920×1080 | **Method 7** (`Check_N9_gitlab_form_at_1920x1080_and_light`): 1. `ServeAsync()`. `PrepareRequestsAsync(served.MobileApp, GitLab)`. `OpenAsync(data, served, WebApp, width: 1920, height: 1080)`; wait for `web-app: listed 3 open pull requests of northfield/platform/web-app` since the start and for the watchers to be quiet. Mark the requests. 2. Click `Local branches` › `topic`; right-click its `no-request`; click `Create merge request…`; wait for the form with `PullRequestBase` `origin/main`. Screenshot (`n09a-gitlab-form-1920`, the pointer at (760, 1000)). Press Esc. 3. Click the toolbar's Theme switch (light). Right-click `topic` › `no-request`; click `Create merge request…`; wait for the form. Screenshot (`n09b-gitlab-form-1920-light`, the pointer at (760, 1000)). Press Esc. | **After step 2:** the form as N2's GitLab form gives it, its card at x 730 to 1190, centred vertically ±1, every control placed as in N2's method 1 relative to the card (±1); no text cut but by an ellipsis. Behind it the window at 1920×1080. Requests since the mark none.<br>**After step 3:** the same form, texts and places in the light theme's brushes (the card `#F5F6F8`; `From`, `Into`, the repositories and the quick-actions note `VcTextSecondaryBrush` `#5C6572`; `Create` `VcPrimaryButton`). After Esc no form; requests none since step 1's mark. |  | n/a | `n09a-gitlab-form-1920`, `n09b-gitlab-form-1920-light` | n/a: 1920×1080 and light are scripted only (14.1). The rest point (760, 1000) is the backdrop under the card (7a's open point 56). The places relative to the card are N2's, measured at 1100×700. Varies by nature: the git box; the caret. |

## What proves each Delivers item

`plan.md`'s phase 7b "Delivers", against parts A, B, P and N, part N's column and its row added with its
checks, as phase 7a's report did. "Elsewhere" names the work packages' own classes (design
0.1).

| Delivers item | Part A | Part B | Part P | Part N | Elsewhere |
|---|---|---|---|---|---|
| Azure DevOps and GitLab on 7a's provider model (D347): sign-in with a personal access token (D345), one account per provider (D348), tokens in the OS keychain, else the session (R3, D253); status step 10, "Sign-in dialog and the Accounts section with an account of each provider" | A1 to A14 (the keychain through `FakeTokenStore`): the three blocks (A1, A3), a sign-in per provider through its block and dialog, one account each (A2, A4, A6), each dialog's states and refusals (A5 to A11), session only and its restart (A12), one sign-out per provider (A13), one switch per account (A14); the real window repeats A2's three accounts, A5's GitLab dialog and A6's Azure DevOps dialog | B1, B3: GitLab's and Azure DevOps' sign-ins through Settings, Accounts and their dialogs, as WP-R's launches sign in; B8: the palette's `Clone from Azure DevOps…` signs in first through the window's entry and Azure DevOps' dialog (10.5, 11.5), and `Clone from GitHub…` cancelled adds no account (D300); B5: the list's 401 marks Azure DevOps' account as needing sign-in at once, its keychain entry kept | P1: GitLab's and Azure DevOps' sign-ins through Settings, Accounts, as WP-R's "Requests and create" launches sign in, Azure DevOps' organization prefilled from the tab (10.5); P2: the section's sign-in row opens that provider's dialog over the window, Azure DevOps' prefilled; P4: a 403 or a 500 on the list keeps the account signed in | N8: a create signed out opens that provider's dialog first, GitLab's (cancelled, then signed in) and Azure DevOps' prefilled from the tab (10.5, 13.3), and goes on to the form; N1, N2, N6: GitLab's and Azure DevOps' sign-ins through Settings, Accounts, as WP-R's "Requests and create" launches sign in | `SignInDialogProvidersTests`, `AccountsProvidersViewModelTests`, `SignInEntryProvidersTests` (10.8); `HostingSessionProvidersTests` (9.10: one A1 per provider, a second GitLab user forgets the first's entry); `HostingProvidersTests` (`TokenFormOf`, D378) and `TokenEntryNamesTests` (1.10); the real stores with an `azure-devops` key: CI's real-store steps (1.10, D270); the live `Check_2` per provider (stage 1, "Live command") and `Check_3` (stage 2) |
| The account's token on the app's git calls to the remotes it covers, for Azure DevOps its own organization's (D350) | A14: each block's "Use for git over HTTPS" saved alone, GitLab's dropping web-app's GitLab binding and keeping GitHub's; the notes naming GitLab and Fernhill-Labs's repositories on Azure DevOps (A2, A4) | B7: the clones of GitLab's `northfield/platform/web-app` and of Azure DevOps' `mobile-app` (its `Fernhill-Labs@` URL) take the account's token through the app's helper as `x-access-token`, with no credentials dialog, no credential written into the clone and the token in no file or git call; the real window repeats both in the real process, Git for Windows' `sh` running the helper | P11: `!23`'s fetch of `refs/merge-requests/23/head` through the app's helper with `x-access-token`, no credentials dialog, the token in no file or git call; P9: `web-app`'s bindings, GitLab's for `origin` and GitHub's for `upstream`; P8: `manifest`'s harbor-co origin bound to no account and nothing sent for it (D350, D356) | N4: Create's push of `local-work` to each fake through the app's helper as `x-access-token`, with no credentials dialog, no credential written into the clone and the token in no file or git call (the push's command, output and error included) | WP-C's classes (8.7), `HostingSessionCredentialTests` (9.10), `CloneFromAccountTests` (11.6: both clones with no credentials dialog, a `.git`-less GitLab URL bound with its twin, harbor-co's typed URL asking git's credentials dialog with no token sent there, D350) |
| Browse and clone the repositories of an Azure DevOps and a GitLab account (status step 11; D354's button, switch and palette commands; ruling 15) | — | B1 to B8: the form's button per provider and with two or three accounts (B1, B2), the browser's switch (B2), each browser listed, loading and failed (B3 to B5), the two long names cut with their tooltips (B6), the clones of `web-app` and `mobile-app` through the app's helper (B7), the palette's three commands, one signing in first (B8); the real window repeats B1's button text, B3's two lists and B7's two clones | — | — | `CloneFromAccountDialogViewModelTests` and `HostingTextsDescribeProvidersTests` (7b project) and `CloneFromAccountTests` (Hosting.Tests, in the lagged list) (11.6); `AppShortcutsTests`' keyless list (11.5); 7a's part B classes, run by the builder (11.6); the live `Check_2` per provider (stage 1: the list and a clone through the clone form) to `Check_4` (0.1) |
| The request list (GitLab's merge requests, D351), its menu, checkout, the badge on branches and open in browser, for each provider as 7a built them for GitHub (status step 12; D355, D356, D361, D366, D376; rulings 5, 19, 24); author avatars GitHub's alone (D349, D367) | — | — | P1 to P14: each provider's section with `!n`, a draft and a fork and the graph's badge (P1); signed out, loading, failed, none and past 300 (P2 to P6); the filter on `!21` (P7); D356's row naming both organizations, nothing asked (P8); ruling 5's listed remote, GitHub asked nothing and no avatar while GitLab's is listed (P9); the section's and a request's menus (P10); the checkouts of `!21`, `!23` and `!11` with git's state and `!13`'s reason (P11); copy link (P12); open in browser through the fake launcher (P13); a branch row's `!21` and its menu's item (P14); the real window repeats P1's sections with their badges and P10's section menus per provider and reads `!21`'s and `!11`'s menus | N6: the created request listed first with its badge on the branch's label and its branch row's `!n`, D293's request on top of the held list and the list read again (D289); N5: the list read again after "already open", and `Open merge request !24` and `Open pull request !14` opening the request through the fake launcher | `RequestSectionProvidersTests`, `RequestCheckoutProvidersTests`, `RepositoryMenusProvidersTests` and `GraphRequestBadgeTests` (7b project), `RequestOperationsProvidersTests` (Hosting.Tests, in the lagged list) and the extended `GitRepositoryPullRequestsTests` (Git.Tests) (12.7); `RepositoryHostingProvidersTests`' `<organization>.visualstudio.com` remote listed in process and `RepositoryAvatarsTests` (9.10); 7a's part P and G classes unchanged (12.7); the live `Check_2` (the list, stage 1) and `Check_4` (0.1) |
| Create from the branch menus and the drop menu, for each provider as 7a built it for GitHub (status step 13; D306, D312 to D318 for the new providers, D351, D355; rulings 1, 3, 20) | — | — | P9: `origin/topic/no-request`'s menu holds `Create merge request…` (WP-N's action, not clicked), `upstream/main`'s none; P14: a branch with a request offers `Open merge request !21` in its place | N1 to N9: the items on a local branch, a remote branch, `local-work` and a drop (N1), GitLab's four opening its form (N1, N8), Azure DevOps' local branch's and drop's (N1, N8) and a remote branch's on the remote `long` (N7), its other two read; the remote branch's offered signed out on both providers, `local-work`'s and the drop's on Azure DevOps (N1); each provider's form (N2); GitLab's Draft with its sent title and the quick-actions note, and Azure DevOps' `isDraft` (N3); `local-work` pushed first through the app's helper, then the POST, one operation (N4); already open and a missing branch in the App's words (N5); the request listed with its badge and branch row, `Created … !n` and the offer opening its page (N6); long repository texts cut with their tooltips (N7); signed out, that provider's sign-in first (N8); the form at 1920×1080 and in light (N9); the real window repeats N2's forms and N6's status after creating, per provider | `CreateRequestDialogProvidersTests` (7b project) and `CreateRequestOperationProvidersTests` (Hosting.Tests, in the lagged list) (13.7: the texts per provider, the sent title's prefixes, the rows' bounds against 7a's, a 60-character repository cut; a `/close` description sent as typed, a fork's head with `target_project_id` or `forkSource`, the 409 and the 400 forms, a 403 with and without the scope code, `CreateStatus` 200 and 201, TF401179 and TF401398, a lost answer counted as created, `manifest/` not started, signed out that provider's sign-in first, the tripwire after a push); 7a's part N classes, `CreatePullRequestOperationTests` and `RepositoryOperationsRemotesTests` unchanged (13.7); `RepositoryMenusProvidersTests` (12.7: the drop shapes and the palette's items); the live `Check_2` per provider (stage 1: a create through the providers) and `Check_3` (stage 2, the app's create) |
| 7a's screens and commands that name GitHub name the provider of the account or remote they act on (D351); status step 14 for part A's, part B's, part P's and part N's surfaces | A1 to A3: the blocks' labels and the one intro (D353); A5, A6: `Add GitLab account` and `Add Azure DevOps account` with their help, links and boxes; A8 to A11: each failure in its provider's words (D362); A12: the notes per block; A13: `Sign out of GitLab?` and `Sign out of Azure DevOps?` with their questions; GitHub's block and dialog in 7a's words (A2, A11's GitHub step), and 7a's part A pictures changed in the intro alone ("Changes to expected results") | B1: `From GitLab…` and `From Azure DevOps…`; B2: `From an account…` and the switch's `GitHub`, `GitLab` and `Azure DevOps` (D354); B3 to B5: `Clone from GitLab` and `Clone from Azure DevOps`, each failure in its provider's words (`GitLab had a problem. Try again later.`, `Azure DevOps refused the token of Fernhill-Labs. …`) and the browser's log line naming the provider; B8: `Clone from GitLab…` and `Clone from Azure DevOps…` in the palette; GitHub's in 7a's words (B2's `b02e`, B8's `Clone from GitHub…`), and 7a's part B pictures unchanged ("Changes to expected results") | P1 to P6, P10: `Merge requests` and its words on GitLab (`Sign in to GitLab to see merge requests…`, `Loading merge requests…`, `No open merge requests`, `More than 300 open merge requests`, `Refresh merge requests`, `Open merge requests in browser`, `Merge requests are loading`), `Pull requests` on Azure DevOps, titled so signed out too (ruling 24); `!n` in the rows, the menus, the branch rows, the badge and its tooltip (D355; P1, P10, P14); `Sign in to GitLab…` and `Sign in to Azure DevOps…` and their dialogs (P2, P10); each failure in `Describe`'s words naming its provider (P4); `Fetch merge request !23` and `Fetched !23 into fix-typo` (P11); D356's row and tooltip naming both organizations (P8); GitHub's in 7a's words, 7a's part P and G pictures the same files ("Changes to expected results") | N1: `Create merge request…` and `Create pull request…` in the branch and drop menus; N2: `Create merge request` and `Create pull request`, `!n` in the form's and the status bar's texts (D355); N3: `Title on GitLab: Draft: Local change` and `GitLab runs quick actions in the description, such as /close.`; N4: `Not on GitLab yet: …`, `Not on Azure DevOps yet: …` and `Creating merge request…`; N5: `GitLab has no branch …`, `Azure DevOps cannot find …`, `Merge request !24 is already open …` and `Open merge request !24`; N6: `Created merge request !24` and `Created pull request !14`, N3's draft forms; N8: `Add GitLab account` and `Add Azure DevOps account` from a create; GitHub's form, texts and status in 7a's words, 7a's part N pictures the same files ("Changes to expected results") | `HostingTextsProviderTests` (1.10); `AccountsProvidersViewModelTests` (10.2's texts per state); 7a's part A classes, run by the builder (10.6, 10.8); `HostingTextsDescribeProvidersTests` and `CloneFromAccountDialogViewModelTests` (11.6); 7a's part B classes and `HostingTextsDescribeTests` unchanged (11.6); 12.7's classes and 7a's part P and G classes unchanged (12.7); 13.7's classes (the texts per provider) |
| A local fake of each provider's API for the default run and CI (D346) | A2, A4 to A10 sign in and are refused against the fakes as stage 1 measured them and WP-LF copied them: GitLab's 401, Azure DevOps' member, empty 401, Anonymous 200 (an organization's that allows public projects, MEASURED 2026-11-05 and 2026-11-07; in Fernhill-Labs, private, the fault `AnonymousIdentity`'s, open point 8) and unknown organization's 404 | B3 to B5, B7: each fake's list as its provider asks for it (GitLab's `projects` by `last_activity_at`, Azure DevOps' unpaged list of the organization), held (`HoldNext`) and failed (GitLab's 500, Azure DevOps' empty 401 as stage 1 measured it, 16.1), and git over each fake's host mode with `x-access-token`, `Fernhill-Labs@` and `%20` | P1 to P6, P11: each fake's request list as its provider asks for it (GitLab's `mrs:` with `state=opened&per_page=100`, paged by `Link` to three pages, and the fork's lookup; Azure DevOps' `pulls:` with `$top` and `$skip`, its web addresses built by the provider), held (`HoldNext`) and failed (GitLab's 403 `insufficient_granular_scope`, Azure DevOps' 500), GitLab's 304 to a kept `ETag`, and git over each fake: GitLab's head ref `refs/merge-requests/23/head`, Azure DevOps' merge refs alone, never a head ref (D361) | N3 to N6: each fake's create as stage 1 measured it (GitLab's 201 with `draft` from a `Draft:` title, its 409 and its missing-branch 400; Azure DevOps' 201 with `isDraft`, TF401179's 409 and TF401398's 400), held (`HoldNext` on GitLab's `create:`), GitLab's 304 to the kept `ETag` before the POST, git's receive-pack over each fake's host mode with `x-access-token` and `Fernhill-Labs@`, and the request refs written, GitLab's `refs/merge-requests/24/head` and Azure DevOps' `refs/pull/14/merge`, never a head ref (D361) | `FakeAzureDevOpsTests`, `FakeAzureDevOpsGitTests`, `FakeGitLabTests`, `FakeGitLabGitTests` (3.6, 4.6), `Phase7bSupportTests` (2.8) |
| The opt-in live command extended to Azure DevOps and GitLab (D346, D352) | — | — | — | — (stage 2's `Check_3` creates through the app per provider after WP-N's merge, 15.6) | "Live command" above; `LiveSafetyTests`, `LiveScreenTests` (15.9) |

## Changes to expected results

### Part A: 7a's Accounts section takes the one intro (D353; design 0.7, 10.6)

Phase 7a's part A pictures `a01a-signed-out`, `a02a-dialog-empty`, `a02b-token-masked`, `a03a-checking`,
`a04a-not-a-token`, `a04b-refused`, `a05a-signed-in`, `a05b-signed-in-1920`, `a05c-signed-in-1920-light`,
`a06a-no-keychain-dialog`, `a06b-session-only`, `a07a-save-failed-dialog`, `a07b-save-failed-section`,
`a08a-sign-out-question`, `a08b-signing-out`, `a08c-signed-out`, `a08d-sign-out-note`,
`a09a-needs-sign-in-api`, `a09c-needs-sign-in-git`, `a10a-session-ended`, `a10b-still-signed-in`,
`a10c-token-gone` and `a11a-switch-off` (23: every part A picture but `a09b-git-refused`, the error
dialog over hello's window), and the real window's `a01a-signed-out`, `a02b-token-masked` and
`a05a-signed-in` with their difference pictures, change in the intro's two lines only: they read 10.2's
sentence, `With a GitHub, GitLab or Azure DevOps account, VisualCommit lists and creates pull requests
(merge requests on GitLab) and clones your repositories.`, in place of 7a's. In the dialog pictures the
intro lies under the dialog's backdrop. Nothing below the intro moves, since its height is 7a's (A1
measures it first, 10.9). Reason: D353's one sentence for the three providers; the new providers are off
in 7a's walk-through and its real-window launches (D362), so only GitHub's block shows. The gate compares
them by region against references made on `master` (plan risk 32; design 0.7).

The 7a report's A1 row (its step 2's `AccountsIntro`) and its part A "Texts these checks rely on" quote
7a's sentence; that report is kept as history, and this section supersedes the quote for the intro alone.

### Earlier tests (design 10.6, 10.8)

- `tests/VisualCommit.VisualTests.Phase7a/Phase7aExpectations.A.cs`: `PartA.Intro` takes 10.2's sentence,
  and `Phase7aExpectationTests.A`'s pin of it against `AccountTexts.AccountsIntro` with it.
- 7a's part A view-model tests read `model.GitHub` (the block) and pass the provider to the dialog's view
  model (10.6; `AccountsSettingsViewModelTests`, `SignInDialogViewModelTests`); their assertions stay 7a's.
- `tests/VisualCommit.Testing/Headless/ShellDriver.Phase7a.Accounts.cs` gains 1.9's provider forms beside
  7a's GitHub forms, which keep their behaviour.
- `SettingsScreenServices.SignIn` takes the provider and `MainWindowViewModel.Settings.cs` sets `SignIn =
  SignInAsync` (10.1); 7a's part A classes, `SignInEntryTests`, `SettingsDialogViewTests`,
  `Phase6ChecksS` and `Check_L31*` keep their expected results (10.8, 10.9).
- `tests/VisualCommit.VisualTests.Phase7a/Phase7aChecks.A.cs`'s `Check_A9` waits, in its steps 2 and 5,
  for hello's hosting to show signed out (`PullRequestsState.SignedOut`) before it asserts that state and
  before `a09b` (CI 42339567989 on Windows read `Loading`): a refusal swaps the session's slot, which the
  account's waits read without the UI thread, and only then posts each hosting's change to the UI thread
  (7a design 7.3, 7.8), so those waits can end before the post runs. 7a's report gives A9's steps and
  expected results; they stay as written there, this wait added.

### Part B: no earlier picture changes (D354, D362; design 0.7, 11.1, 11.3 to 11.5, 11.8)

Part B changes no earlier picture on purpose. 11.8 has the lead compare 7a's `b01a`, `b03a`, `b06a` and
`b07a`, phase 1's `13a` to `14b` and phase 6's `l26b` and `l32b` with references by region, because
WP-B changes the views they show:

- **The clone form** (11.3): the button's text and automation name bind `BrowseButtonText`, which is
  7a's `From GitHub…` while GitHub alone is signed in; its id `CloneFromGitHubButton`, its place and its
  look stay 7a's; it is collapsed while no account is signed in, as before. Both harnesses keep the new
  providers off (D362), so 7a's part B pictures of the form (`b01a-form-signed-in`, `b06a-chosen`, and
  the form under the backdrop of `b02a-loading`, `b03a-listed`, `b04a-filtered`, `b05a-failed`,
  `b05b-refused` and `b09a-cap-and-sso`) show it as before, and phase 1's `13a-clone-form`,
  `13b-clone-in-progress`, `14a-clone-failed` and `14b-clone-cancelled` (`13c-cloned` shows the cloned
  tab and stays too) and phase 6's `l26b-clone-form-dark`, `l26b-clone-form-light`,
  `l32b-clone-failed-dark` and `l32b-clone-failed-light` show the form signed out, without the button.
  Under a backdrop the button stays drawn disabled, as in 7a, its command waiting for the browser (open
  point 21 for `b03a-listed`, which 7a reaches through the palette's command). Of the earlier real-window
  pictures only 7a's `b03a-listed` shows the form, under the browser's backdrop (next bullet); it too
  shows it as before. Reason: D354 names the provider while one account is signed in, GitHub's in 7a's
  words (D351).
- **The browser** (11.4): 7a's constructor stays, with GitHub alone: no switch, 7a's card. The full name
  now cuts at its start, which changes nothing for a name that fits: 7a's rows (`octo/hello` to
  `octo/listed-0996`) all fit, so 7a's `b02a-loading`, `b03a-listed`, `b04a-filtered`, `b05a-failed`,
  `b05b-refused`, `b09a-cap-and-sso` and the real window's `b03a-listed` draw as before, pixel for pixel
  (0.7: WP-B adds only the trimming and the tooltip). The log's `Listing GitHub repositories failed:`
  lines stay byte for byte (`Phase7aExpectations.B.cs:125-127`). Reason: ruling 3's cut, which only a
  long nested path needs.
- **The palette** (11.5): `Clone from GitLab…` and `Clone from Azure DevOps…` are listed only while their
  providers are on, which no earlier walk-through or real-window launch turns on (D362), so 7a's
  `b07a-palette-command` (the empty box's `Clone repository…`, `Clone from GitHub…`, `Init
  repository…`), phase 6's `k10a` to `k10f`, `l31c-palette-dark` and `l31c-palette-light`, the real
  window's `k10a` to `k10d` and `l31c-palette-light`, and K10's lists stay; neither command has a key, so
  the Keyboard shortcuts list (`s14e`, `s14f`, changed only by 7a's nav button) gains no row. Reason:
  D354's commands per provider, D362's harnesses.
- **D248's box and how they are compared.** Each of these pictures shows the status bar's git box; the
  references on `master` are made with the same git (0.7), so no difference is expected there either.
  They are compared by region against `D:/Projects/Personal/gate7b-reference/` with
  `scripts/compare-pictures.py` at WP-B's merge (11.8) and at the gate (0.7); where a difference may lie
  is open point 20's reading.

### Part B: earlier tests (design 11.1, 11.5, 11.6)

- `AppShortcutsTests.The_commands_without_a_key_are_6_1s_list`
  (`tests/VisualCommit.App.Tests/AppShortcutsTests.cs:258-274`): `AppCommand.CloneFromGitLab` and
  `AppCommand.CloneFromAzureDevOps` join `withoutKey`, with a comment naming D354, which amends D299's
  list as D299 amended D199's. The two values come right after `CloneFromGitHub` (`AppCommand.cs:57-59`),
  so only the keys-only values after it move, as in 7a. Reason: D354's two palette commands without a key
  (11.5).
- `WelcomeCloneFromGitHubTests` (`tests/VisualCommit.Hosting.Tests/`, `:50` and `:303`) takes the
  renamed members: `ShowsBrowseButton` for `ShowsGitHubButton`, `BrowseCommand` for `BrowseGitHubCommand`.
  `tests/VisualCommit.VisualTests.Phase7a/Phase7aChecks.B.Helpers.cs` (its form snapshot, `:208`) takes
  `ShowsBrowseButton` for `ShowsGitHubButton` (1.7: 7a's part B walk-through files go with WP-B).
  `PaletteCloneFromGitHubTests` reaches none of the renamed members today (`CloneFromGitHubAsync` is
  called only in `src/`, by the palette's command) and changes only if its code comes to reach one,
  `CloneFromAccountAsync(HostingProvider.GitHub)` for `CloneFromGitHubAsync` (11.1 gives it to WP-B for
  the renamed members). Their assertions and 7a's part B expected results stay 7a's. Reason: 11.3 and
  11.5 replace the GitHub-named members (1.7: WP-B owns these files).
- `HostingTexts.Describe.cs:53` calls `OperationTexts.TokenRefused(failure.Provider, login)` (1.7, 8.5);
  GitHub's text is 7a's word for word, so `HostingTextsDescribeTests` pass unchanged.
- Unchanged: 7a's part B classes keep the one-argument forms (1.7): `CloneFromGitHubDialogViewModelTests`
  (7a's constructor, GitHub alone), `CloneFromGitHubDialogViewTests`, `Phase7aChecksB` but that one
  member, and `Phase7aExpectations.B`; and `PaletteEntriesTests`, `CommandPaletteViewTests`,
  `PickDialogViewModelTests`, phase 1's `Check_13*` and `Check_14*`, and phase 6's `Check_K10*`,
  `Check_L26*` and `Check_L32*`, which the builder runs (11.6).

### Part P: part A's prefilled Azure DevOps picture, and 7a's pictures unchanged (D351, D362; design 0.4, 0.7, 12.1, 12.9)

Part P changes one earlier picture on purpose:

- **Part A's `a06c-azure-prefilled`** (A6, method 6). Behind the dialog's and the screen's backdrops,
  left of the settings card (x 0 to 120 at 1100×700), `mobile-app`'s left panel shows the section's
  header's chevron down and `Pull requests`, as before (Azure DevOps' title is "Pull requests",
  ruling 24; its count `0`, at the panel's right end, lies behind the card), and under it the message row, which from WP-P's merge reads `Sign in to Azure
  DevOps to see pull requests…` in place of 7a's `Sign in to GitHub to see pull requests…` (12.2's
  `SignInToSee(p)`): the inspection of part A's pictures saw its start, `Sign in to Git…`, cut by the
  card. The row is cut with "…" at 260 and covered by the card from x 120, so only its start shows, now
  `Sign in to Az…` or as far as the card leaves it (where the card cuts it is inspected, not asserted).
  Nothing else there changes: signed out, no branch row shows a number and the graph no badge (D257,
  D350); A6 asserts nothing in that region (open point 10). Reason: D351 and D362: the section of a
  repository whose listed remote is Azure DevOps' names Azure DevOps, which until WP-P's merge it named
  GitHub (9.11's risk). The lead runs A6's method 6 again at WP-P's merge and inspects `a06c` (12.9,
  16.2); any other difference in that region is WP-P's defect.
- **No other picture of parts A and B changes.** Part A's other pictures are of empty tabs, `a14a` and
  `a14b` taken on the empty tab beside `web-app`'s so that they show no region of WP-P's; part B takes
  no picture of an opened repository of a new provider (11.7). Reason: 0.4's order of the parts.
- **7a's part P and G pictures are the same files** (12.1, 12.9; D362). With GitHub listed every text,
  row, menu and pixel is 7a's: 12.2's provider forms return 7a's constants for GitHub, the badge's
  `Provider` defaults to GitHub (12.6), and both harnesses keep the new providers off. So
  `Phase7aChecksP`'s 21 pictures (`p01a-not-github` to `p13a-checked-out-fork`) and `Phase7aChecksG`'s
  10 (`g01a-badges-and-avatar` to `g07a-authors-failed`) are the same files as
  `docs/test-reports/phase-7a-pictures.sha256` lists, but those 7a's gate found to vary by nature, which
  are compared by region against 0.7's references (`D:/Projects/Personal/gate7b-reference/`); at the gate
  so are the real window's `p06a-rows`, `p11c-section-menu` and `g01a-badges-and-avatar`. At WP-P's merge
  the lead runs both classes through `scripts/heavy-run.sh` and checks them with
  `tr -d '\r' < docs/test-reports/phase-7a-pictures.sha256 | grep -E '/phase-7a/scripted/[pg][0-9]' | sha256sum -c --quiet`
  (12.9). 7a's other pictures that show `hello`'s section or its badges (A9's `a09a` to `a09c`, part N's)
  stay as 0.7 says. Reason: D351 names the provider an earlier screen acts on, and GitHub keeps 7a's
  words (D362).
- **Phase 3's `Check_03_other_menus`** (`03a-tag-menu` to `03f-empty-area-menu`, its `03e-no-menu` with
  the placeholder) and every earlier picture of the placeholder (phases 0, 1, 3 and 6 pin it) stay:
  `NoHostedRemote` keeps 7a's `Pull requests` line, closed, `0`, with no menu and no request item in a
  branch menu (12.3, 12.4). The lead runs it at WP-P's merge (12.9). Reason: ruling 24 titles the
  section by the provider only while there is a listed repository.

### Part P: earlier tests (design 12.1, 12.5 to 12.7)

- `GitRepositoryPullRequestsTests` (Git.Tests) gains GitLab's cases (12.7); its 7a cases keep their
  assertions. The guard already checks with `PullRequestRefs.IsFetchableHead`, which takes GitLab's
  `refs/merge-requests/<number>/head` as well as `refs/pull/<number>/head` (WP-C, 8.4), and
  `IsPullRequestHead`, which 12.5 removes, is already gone. WP-P changes only the guard's message
  (`GitRepository.PullRequests.cs:30`, today naming `refs/pull` alone) to 12.5's `Only a request's head
  (refs/pull/<number>/head or refs/merge-requests/<number>/head) is fetched into FETCH_HEAD, not
  {sourceRef}.`, which no test pins. Reason: D361's GitLab head ref.
- `PullRequestBadge` gains a last `HostingProvider Provider = HostingProvider.GitHub` (12.6): the
  three-argument badges of 7a's `GraphPullRequestBadgeTests`, `Phase7aChecksG` and
  `Phase7aExpectations.G` equal the new records with GitHub, so they pass unchanged.
  `LeftPanelViewModel.UpdatePullRequests` gains `string? accountLogin = null` after `account` (12.3):
  7a's calls keep two arguments. The rows, the branch rows and the badge take `{N}`, GitHub's `#n`.
  `RepositoryViewModel.Menus.cs:458`'s description becomes `signing in to {P}` (12.1), a text no test
  reads. Reason: 12.2's provider forms and 12.6's badge, GitHub's output 7a's.
- Unchanged (12.7: "7a's part P and G classes and those 7a design 10.6 names pass unchanged"): 7a's
  `PullRequestSectionTests`, `PullRequestCheckoutTests`, `RepositoryMenusHostingTests`,
  `LeftPanelPullRequestsViewTests`, `GraphPullRequestBadgeTests`, `Phase7aChecksP` and `Phase7aChecksG`,
  Hosting.Tests' `PullRequestOperationsTests`, and the panel and menu classes 7a's part P left unchanged
  (`LeftPanelViewModelTests`, `PanelViewTests`, `RepositoryMenusTests`, `RepositoryMenusKeyTests`,
  `PaletteEntriesTests`, `RepositoryOperationsCheckoutTests`); `RepositoryAvatarsTests` is WP-S's (12.1).
  Phase 3's `Check_03_other_menus` and phases 0, 1 and 6's checks of the placeholder keep their expected
  results.

### Part N: no earlier picture changes (D351, D362, D317; design 0.7, 13.1, 13.4, 13.9)

Part N changes no earlier picture on purpose. 13.9 has the lead compare 7a's part N pictures and phase
3's status bar by region with 0.7's references, because WP-N changes the views they show:

- **The form** (13.4). The From and Into rows' `StackPanel`s become `DockPanel`s that place each child
  where `Spacing="8"` did, and each repository text gains `CharacterEllipsis` and a tooltip of its
  whole text. 7a's texts (`topic/no-pr`, `local-work`, `origin/topic/no-pr`, `main`, `origin/main`,
  `octo/hello`) all fit their rows, so 7a's `n01a-form`, `n02a-not-on-github`, `n02b-creating`,
  `n04a-already-open`, `n04b-no-commits`, `n05a-remote-branch-form`, `n05c-drop-form`,
  `n06b-form-after-sign-in`, `n07a-pushing`, `n07b-cancelled`, `n08a-same-branch`, `n08b-choose-base`,
  `n08c-enter-a-title`, `n09a-refused`, `n10a-form-1920` and `n10b-form-1920-light`, and the real window's
  `n01a-form` with its difference picture, draw as before, pixel for pixel; 7a's N1 and N10 assert
  `Phase7aExpectations.PartN.FormLayout` (±1) and pass unchanged. GitHub's form shows neither GitLab
  line (13.4: both collapsed, taking no room), its title and texts are 7a's constants (13.2), and no 7a
  picture rests the pointer on a repository text, so no new tooltip shows in one. Reason: ruling 3's
  cut, which only a long repository text needs, and D362's GitHub words.
- **The status bar and the offer** (13.1: used as they are). 7a's `n01b-created`, `n03a-created-draft`,
  `n05b-drop-menu` and the real window's `n01b-created` show `Created pull request #6` or `Created draft
  pull request #6` and the offer as before; every picture of phase 3's walk-through
  (`VisualCommit.VisualTests.Phase3`), each of which shows the status bar, stays, as phases 4 to 6's and
  part A's `a09b-git-refused` do (7a's "Part N: the status bar's offer"). Reason: D317's offer, 7a's,
  unchanged; only a create gives one.
- **Menus and the earlier parts.** WP-N changes no menu (the items are WP-P's, 12.4): part P's P9 reads
  `Create merge request…` in `origin/topic/no-request`'s menu without clicking it, and part N clicks it;
  no picture of parts A, B or P shows the form, the offer or a created request. Reason: 0.4's order.
- **How they are compared** (13.9). At WP-N's merge the lead runs 7a's part N (`Phase7aChecksN`) and
  phase 3's walk-through through `scripts/heavy-run.sh`, checks 7a's N pictures with
  `tr -d '\r' < docs/test-reports/phase-7a-pictures.sha256 | grep -E '/phase-7a/scripted/n[0-9]' | sha256sum -c --quiet`
  (those 7a's gate found to vary by nature, the caret's among them, by region against
  `D:/Projects/Personal/gate7b-reference/`), and compares phase 3's by region against the same references:
  a difference is allowed only in D248's git box and in what varies by nature, each opened and judged,
  and none in the status bar's middle, where the offer would lie; then part N. At the gate so are the
  real window's `n01a-form` and `n01b-created`.

### Part N: earlier tests (design 13.3 to 13.7)

- `ICreatePullRequestActions` gains `HostingProvider Provider => HostingProvider.GitHub` (13.4, a
  default), so 7a's test fakes of it (`CreatePullRequestDialogViewModelTests`) compile unchanged and
  read GitHub's texts; `CreatePullRequestTexts`' 7a members stay word for word beside the provider
  forms (13.2), so `Phase7aExpectationTests.N`'s pins pass unchanged. Reason: 13.2's provider forms,
  GitHub's 7a's.
- **The shims' removal** (13.6). `IHostingSession` loses 1.5's six default-bodied members. 7a's tests
  keep their GitHub forms through `HostingSessionGitHubForms` (1.9; in the session's namespace, so no
  using is added): `HostingContractsTests.The_off_session_has_no_provider_and_its_shims_go_through_the_provider_members`
  (`:96-112`, `session.GitHub`, `session.Account` and the one-argument `SignInAsync` on an
  `IHostingSession`) and the GitHub alone case's `session.GitHub` (`:69`) bind to those extension
  members, which go through the same provider members, so their assertions hold (a rename of the
  first, whose name speaks of shims, would be WP-N's, recorded here if made); `HostingSessionStub`
  (App.Tests) keeps its six members as plain members of the stub, its users unchanged.
  `RepositoryOperations.CreatePullRequest.cs:454` calls `GitCredentialsRefused(refused.Provider,
  LoginOf(refused))`, GitHub's text word for word (1.7). Reason: ruling 1.
- **The create's own code** (13.3, 13.5). `PullRequestForm` reads `RequestRemotes` for `Remotes` and
  compares records, never `FullName`; its refusals read `failure.Refusal`, which `GitHubProvider`
  already sets from GitHub's 422s (`GitHubProvider.cs:405-417`), then `Kind`, so no App code compares a
  service's sentence. 7a's `CreatePullRequestOperationTests`, `RepositoryOperationsRemotesTests`,
  `CreatePullRequestDialogViewModelTests` and `Phase7aChecksN` (N4's already open and no commits, N9's
  refusal and lost answer among them) keep their expected results (13.7). The "not started" line for a
  repository without a hosted remote says "no hosted remote" (13.3), a text no test reads. Reason: r1
  P8 and P11's refusals and records.
- Unchanged (13.7): 7a's part N classes, `CreatePullRequestOperationTests` and
  `RepositoryOperationsRemotesTests`, and those 7a's part N left unchanged (`OperationQueueTests`,
  `OperationQueueUiTests`, `OperationStatusViewModelTests`, `StatusBarTests`,
  `DashboardBulkViewModelTests`, `PickDialogViewModelTests`, `DialogTests`,
  `MessageDialogViewModelTests`); part P's classes (12.7), whose `Create …` items WP-N gives
  their action.

### Stays as it is

Every other picture of phases 0 to 7a (design 0.7's "must not change"): phase 6's settings pictures,
whose sections are not Accounts and whose section list is 7a's; 7a's parts B, P, G and N, which take no
picture while the Accounts section shows: B signs in through the session or opens GitHub's dialog from
the palette over the window, and P, G and N sign in through the section, as the real window does, and
close it (Esc, then the screen's `IsClosed`) before their first picture (`Phase7aChecks.P.Menus.cs`,
`Phase7aChecks.G.cs`, `Phase7aChecks.N.cs`). Part B's, part P's and part N's are their own sections above.

### Changed in this report after its first commit

An expected result changed after its UI existed goes here with its reason.

- **WP-A's build** (2026-11-06, the lead's ruling on WP-A's measurement; open point 2): **A12 step 4's
  expected result changed after its UI existed.** It read "whether it fits or scrolls by a few pixels is
  not asserted, and at offset 0 at most the last few pixels of Azure DevOps' `Git` row lie below the
  viewport"; it now reads that the section scrolls at offset 0, its extent above its viewport, and the
  bottom of `AccountUseForGitNoteAzureDevOps` lies below the viewport by less than one of its lines
  (about 14 apart in `a12b`: the ruling read 15 from the picture, and the note's layout gives 13.9,
  `settingsNote`'s 11.5 at the font's line spacing), the last line's top inside the viewport, scrolled
  out of view, not cut. The conventions' "Scrolling" says the same of `a12b`. Reason: WP-A's layout
  measures the section about 502 of 495, where the report estimated about 492, so 7 px of the
  three-line note, about half its last line, lie below the viewport; the lead opened `a12b`, saw the
  scroll bar and the last line scrolled out of view with nothing cut, and ruled that the layout is
  right and the estimate wrong.
- **WP-A's build** (2026-11-06, the lead's ruling; design 10.4): **A6's and A11's expected results for
  `a06a` and `a11b` gain the disabled look of Azure DevOps' token link.** They read only "disabled",
  and the conventions' "The dialog" gave every link the accent foreground and a hand cursor; now the
  disabled link has the theme's disabled foreground `VcTextDisabledBrush` (`#5F6775`) in place of the
  accent, the arrow cursor and, with the pointer moved onto it after the capture, no underline, and
  "The dialog" says so. Reason: WP-A's build drew the disabled link as an enabled one, in the accent,
  which invites a click that does nothing; the lead ruled that it takes the look the theme gives its
  other disabled buttons. GitHub's and GitLab's links are never disabled, so no other picture changes:
  7a's part A pictures differ from the main checkout's in the intro's two lines alone, as before.
- **WP-A's merge** (2026-11-06, the inspection of part A's pictures): **the estimate for three
  signed-in blocks is about 562 of 495, not 553** (the conventions' "Scrolling", A2 step 6 and open
  point 2). Reason: the inspector measured `a02c` and `a02d` 67 px apart at the section's two ends;
  nothing asserts the figure, and no expected result changes with it.
- **WP-B's build** (2026-11-06, the lead's ruling; design 8.2 step 6, section 6): **B7's expected
  `SharedOriginOwner` changed after its UI existed.** It read `Fernhill-Labs`; it now reads
  `fernhill-labs`. Reason: 8.2 step 6 makes a binding's owner the coverage's `Organization`, which section
  6's `CoverageOf` takes from the account key's login, and `AccountKey` lower-cases Azure DevOps'
  organization (0.2); the design's own coverage vectors give `myorg` in lower case, and the broker
  compares the owner without case, so the app answers the same prompts either way. The server's spelling
  in the report was the writer's slip.
- **The probe of open point 8** (2026-11-07, the lead's run of `Check_1b_probes` with the owner's
  agreement; the lead's ruling): **A9's expected result changed after its UI existed.** Its step 8 now
  first sets the fault `FakeAzureDevOpsFault.AnonymousIdentity` on `connection:fernhill-labs`, and its 200
  is the fault's; its text, its one request and `a09a` stay as they were (the picture the same file as
  before the change), and its "Rests on an ASSUMED answer" note is gone. The conventions' "Azure DevOps'
  sign-in" and the Delivers row of the fakes say the same. Reason: the probe read the sandbox
  organization's connectionData without a credential, with Suppress, as 200 with the Anonymous identity,
  so that organization allows public projects and the Anonymous 200 that stage 1 measured for a value in
  none of the PAT forms is a public organization's answer; the fake now keeps it to organizations with
  `PublicProjects`, and Fernhill-Labs, private, answers such a value as no credential, with the sign-in's
  401 TF400813 (READ g1 G0.2), which the dialog gives A8's text. The fault keeps A9 on the Anonymous
  answer's own text, which the dialog must still word apart from a refused token (6.4, 10.2).
- **CI's git 2.43** (2026-11-07, run 42339567989's two containers; the lead's ruling): **B7's expected
  first request below 2.46 changed after its UI existed.** Method 2's "After step 8" read "below 2.46
  the first with `GivenUserName` `Fernhill-Labs` (the URL's user with an empty password …), 401, before
  the helper's answer", and the conventions' "Git's version" said the same of an `<organization>@`
  URL's first request; both now read that the first goes without credentials (`GivenUserName` null),
  401, the second carries `Fernhill-Labs` with an empty password, 401, and the third, the helper's
  answer, `x-access-token`, 200, and `Check_B1_B3_B6_B7_azure_sign_in_list_and_clone` asserts those
  three in that order. Reason: in both containers (Ubuntu's git 2.43.0) the clone's first request had
  no `Authorization` header (`GivenUserName` null, 401), and git's source gives that order. In
  v2.43.0's `http.c`, `get_active_slot` calls `init_curl_http_auth` only once `http_auth.password` is
  set or `curl_empty_auth_enabled()` holds, which with `http.emptyAuth` unset holds only after a 401
  has restricted the methods to something beyond Basic and Digest, so the first request leaves libcurl
  `CURLAUTH_ANY` and no credentials of git's; libcurl sends no header while more than one method is
  wanted, and after the 401 its `Curl_http_auth_act` (8.5's, Ubuntu 24.04's) retries once, Basic, with
  the user it took from the URL and an empty password; that second 401 ends the transfer, git's
  `handle_curl_result` returns `HTTP_REAUTH` (no password yet), and `http_request_reauth` asks the
  app's helper (`credential_fill`). v2.46.0 adds `http.proactiveAuth`, which fills the credential
  before the first request: the laptop's token first. 8.6's "`<organization>@` sends `<organization>:`
  first" meant first before the helper, as r7 H4b and g3 G2.2's H2 row, MEASURED on 2.43.0 and 2.31.1,
  give the three requests; the report's writer read it as the first request. Measured again on the
  laptop (2026-11-07) with Git for Windows' 2.43.0 (libcurl 8.4.0) first on `PATH`: the unchanged
  method failed as in CI, and the changed one passes; against a server that logs each header, 2.43.0
  and 2.56 without the proactive key send none, then `Fernhill-Labs` with an empty password (libcurl's
  trace: "Server auth using Basic with user 'Fernhill-Labs'"), then the helper's `x-access-token`, and a
  user-less URL none, then the token. libcurl's Negotiate support (GSS-API on Ubuntu, SSPI on Windows)
  changes nothing: no Negotiate header goes on the first request under `CURLAUTH_ANY`, the fake's 401
  offers Basic alone, and git drops Negotiate after the first 401. GitLab's expectation (no user in the
  URL: the first without credentials, 401) and the token's guards stand as they were. Ubuntu's own
  curl and git builds (`8.5.0-2ubuntu10`, `1:2.43.0-1ubuntu7`) were read with their patches, not run: the
  skeptics found their changes to the retry keep the URL's user, and the next CI run's containers measure it.
- **WP-P's build** (2026-11-07, the lead's ruling; P6, P8, open point 27): **part P's conventions let
  two message rows be cut with "…" at 260 although their tooltips are not their text.** They read
  "Every picture passes `LayoutAudit.FindClippedText`: a title, a message row or a label cut with "…"
  whose whole text a tooltip holds is allowed"; they now name D356's row (`p08a`, `p08b`, `p10d`) and
  GitLab's `More than 300 open merge requests` (`p06a`) as the one exception, and the audit lets
  through only its finding on that row's text. Reason: the report contradicted itself. P8 and open
  point 27 foresee D356's row cut at 260, its 40 characters past the row's room of about 205 px, with
  the reason as its tooltip; P6 step 4 makes the More row's tooltip the action, as 7a's is, and
  GitLab's sentence, one character longer than 7a's, which fits, overflows by the end of "requests".
  WP-P's builder left methods 4 and 5 failing closed on the audit and asked for a ruling. Neither row
  hides what it says: D356's tooltip names both organizations, and the More row loses only the end of
  "requests" under the header `Merge requests`. A product change (a tooltip that also holds the row's
  text) would change P6's and P8's tooltips, written word for word, and, for GitHub, 7a's, whose
  pictures must stay the same files. The lead opened `p06a`, `p08a`, `p08b` and `p10d`.
- **WP-P's merge** (2026-11-07, the lead; part P's conventions, "The graph at 1100×700"): **the
  conventions' sentence on the graph's columns is scoped to `web-app`'s and `mobile-app`'s four-lane
  graphs, and names `manifest`'s Date column.** It read "Branch / Tag (x 0 to 130), Graph and Message
  only (Date and Author give way, phase 1's rule)" for every part P picture. Reason: the Opus inspector
  of WP-P's merge found `p08a`, `p08b` and `p10d` showing `manifest`'s Date column; phase 1's rule
  (`GraphColumns`: Date gives way only when Message would fall below 120) keeps it beside a one-lane
  graph, whose Graph column is 48 wide. No check asserts the columns, and no expected result of P1 to
  P14 changes.

## Found by the gate

Faults the gate found, and the commit that fixed each.

## Could not run

Checks or parts of the gate that could not run on the Windows machine either, why, and what that
leaves unproven. A real-window check that was not run only because the session was not on
Windows does not belong here: it stays "Not run: needs Windows" in the table, and the gate is
unfinished until a Windows session has run it.

## Other platforms

The CI run of the gate's commit and its result in each job (the three platforms and the two
bare Ubuntu containers), and which of the screenshots CI took on macOS and Linux were opened.

## macOS checklist for the owner

A short list of things to try by hand on a Mac for this phase.

## Open points

Where the design is silent, leaves a choice to WP-A, WP-B, WP-P or WP-N or rests on an answer still to be
measured, with the reading this report takes. A ruling of the lead's, or WP-A's, WP-B's, WP-P's or WP-N's
build, that changes an expected result goes under "Changes to expected results" with its reason. Points 1
to 10 are part A's, 11 to 21 part B's, 22 to 33 part P's, 34 to 45 part N's.

1. **Numbering and additions.** Design 10.7's list is unnumbered; A1 to A14 follow its order, with
   "Fernhill-Labs bold over Ada's name" (A4) after "GitLab off" (A3) as the list has it, though its picture
   is taken at the end of method 5. Beyond the list: the all-off start (A3, for D362), GitLab's 401 (A8),
   GitLab's legacy token's sign-in (A8) and GitHub's dialog given GitLab's token (A11).
2. **Scrolling with one or two signed-in blocks.** 10.3 measures only three signed-in blocks (about 553
   of 495; WP-A's layout gives about 562, the 67 px `a02c` and `a02d` lie apart, as `a12b` gives 502
   for 492). The other heights are estimated from that figure and 7a's pictures (a signed-in block about
   146 high, `a05a`; a signed-out one about 57, `a01a`; D253's two-line note 14 more, `a06b`; Azure
   DevOps' name line about 15): `a02a` about 360, A4's `a04a` about 375, `a02b` about 449, `a12c` and
   `a13b` about 464, and `a12b` (GitHub signed out, GitLab and Azure DevOps for the session only with
   D253's two-line note, Ada's name line) about 492 of 495, the tightest. The checks assert that the
   section fits in all but `a12b`; `a12b` is taken after the wheel has brought the section back to offset
   0, and its first expected result left open whether it fits or scrolls by a few pixels. WP-A's layout
   measures `a12b`'s section about 502 of 495, not 492: at offset 0, 7 px of
   `AccountUseForGitNoteAzureDevOps` (three lines, 13.9 apart) lie below the viewport, about half its last
   line. The lead opened `a12b` and ruled on 2026-11-06 that the layout is right and the estimate wrong:
   the section scrolls (its scroll bar shows), the note's last line is scrolled out of view at the
   viewport's bottom and nothing is cut; A12's step 4 now says so (under "Changed in this report after
   its first commit"). If WP-A's layout
   overflows in another of them, its expected result changes (under "Changed in this report after its
   first commit"), its picture taken at offset 0 after the wheel, as `a12b`'s. In `a14a` (three signed in,
   offset 0) Azure DevOps' `Git` row starts about at the viewport's bottom (about 496 of 495), so A14
   asserts only that it is not wholly in view.
3. **Azure DevOps' switch note.** 10.2 gives a substitution, "7a's with 'to {org}'s repositories on Azure
   DevOps'", not the sentence; the report reads it as `Clone, fetch, pull and push to Fernhill-Labs's
   repositories on Azure DevOps over HTTPS use this account's token in place of your git credential
   helpers. Git in a terminal still uses your helpers.`, the possessive written `'s` after the
   organization.
4. **Azure DevOps' dialog: the order of its rows and Enter in the organization box.** 10.4 places the
   label and `SignInOrganization` "above Token" in "7a's card": the report keeps the link 6 below the help
   and puts the organization's label and box between the link and the label `Token`. Whether Enter in
   `SignInOrganization` signs in is not said; every step whose focus is there clicks "Sign in" instead.
5. **The focus after a local refusal of the organization.** 10.4 names `SignInOrganization` for
   `NotFound` and `SignInToken` for the other failures; `SignInNotAnOrganization` (A11's `a11b`) names
   neither. The step asserts neither focus nor selection there, and the next step clicks the box it
   changes. D378's refusal is read as D298's: the focus stays in the token box, its text as typed.
6. **The organization box during the check.** 7a's 8.4 disables the token box while the check runs; A7
   reads it for both boxes.
7. **Azure DevOps' card height.** "At most about 345 high" (10.4, r3 4.2): asserted at most 345 in A6's
   `a06a` and `a06b`, which show no status line; with an error, the warning or the checking line the
   design gives no figure, and the checks assert none.
8. **The Anonymous 200 in a private organization** (16.1's As built). Settled on 2026-11-07. Stage 1
   measured a value in none of the PAT forms answered with the Anonymous 200 in the sandbox's organization,
   whose privacy it did not read, and WP-LF gave the 200 in every organization, Fernhill-Labs (private)
   included, ASSUMED. `Check_1b`'s probe of the sandbox organization's connectionData without a credential,
   with Suppress, ran on 2026-11-07 (the lead's run of `Check_1b_probes`, the owner agreeing) and answered
   200 with the Anonymous identity: the organization allows public projects, and the short value's 200 is a
   public organization's answer. So, as this point said a 200 would, the fake keeps the Anonymous 200 to
   organizations with `PublicProjects` (16.1), Fernhill-Labs (private) answers A9's value as no
   credential, with the sign-in's 401 TF400813 (READ g1 G0.2), and the dialog gives it A8's text, as
   `SignInDialogProvidersTests` checks; the live command's fake allows public projects, as the sandbox's
   organization does. Of the three ways this point gave before the probe for A9 (an organization with
   public projects added for A9; the fault `FakeAzureDevOpsFault.AnonymousIdentity` set before step 8; A9
   re-pointed at A8's text), the lead ruled the second: A9 sets the fault with
   `served.AzureDevOps.FailNext("connection:fernhill-labs", …)` before step 8 (A9 is scripted only, so a
   fault is allowed), which keeps A9's text, its one request and `a09a` but makes the Anonymous answer the
   fault's. Recorded under "Changed in this report after its first commit".
9. **Picture names for more than one provider.** 0.3 asks a name to name its provider in its words; a
   picture of the section about all three blocks, or all three off, uses `three` (`a01a-three-signed-out`,
   `a02d-three-signed-in-end`, `a03b-three-off`), and one about two providers names both, GitLab's first
   as in D353's order (`a11a-gitlab-azure-token`, `a12b-gitlab-azure-session-only`,
   `a12c-gitlab-azure-session-ended`). The rule's pattern does not test the words, and `Phase7bExpectationTests.A`
   pins only the pattern and uniqueness, so `PartA`'s names are taken from this report as written.
10. **WP-P's region in `a06c`.** The prefilled dialog stands over mobile-app's window, whose left panel
    WP-P changes; nothing there is asserted, and the lead inspects the picture again at WP-P's merge
    (16.2), as 7a's A9.
11. **Numbering and additions (part B).** Design 11.7's list is unnumbered; B1 to B8 follow its order:
    the one-account buttons (B1), two accounts and the switch (B2), each browser listed (B3), loading
    (B4) and failed (B5), the two long names (B6), the clones (B7) and the palette's commands (B8);
    "listed, loading, failed" are three checks, each with one picture per provider. Beyond the list, with
    texts and places the design gives: B1's signed-out form with the three providers on (11.3); B2's late
    answer for the provider not shown (11.4's rule) and its three accounts in the switch (D353's order);
    B6's hover pictures; B7's checks of the configuration read before the clone (D331), of the helper's
    log line, of the clone's other `.git/config` keys and of its tab's binding (as 7a's B6); B8's GitHub
    command cancelled (D300).
12. **The detail beside a cut name (B3, B6).** 11.4 gives the name its cut and its tooltip and says
    nothing of the detail. `PickRowPanel` (phase 6's 7.7) measures the name at the row's width first and
    gives the detail what is left after the 12 gap, so beside a cut name the detail `Private` has no room
    and is not drawn (at most an ellipsis); the row's tooltip holds it, which `LayoutAudit`'s tooltip
    rule accepts. The report reads the panel as it is. The other rows' names and details fit at the row's
    width (about 398 of the 460 card: its padding of 20, the list's border, the item's padding of 10), so
    they are expected whole. If WP-B keeps room for the detail beside a cut name, B3's and B6's expected
    results change, under "Changed in this report after its first commit".
13. **Which failure is pictured per provider (B5).** 11.7 says "failed in its provider's words" and
    names no fault. The report pictures one failure per provider, each on another path of 11.2's table:
    GitLab's `ServerError` (`FakeGitLabFault.ServerError`, a fixed sentence that keeps the account) and
    Azure DevOps' 401 (`FakeAzureDevOpsFault.InvalidToken`, `BadCredentials` with the organization as the
    login, which marks the account at once and so shows D302 for a new provider), as the lead's ruling on
    7a's open point 12 took `ServerError` and `BadCredentials` for GitHub. The other rows of 11.2's table,
    for both providers, are `HostingTextsDescribeProvidersTests`' (11.6).
14. **The switch's place, the card's height and the focus after a click (B2, B8).** 11.4 puts the switch
    "under the title, 8 above the filter" and gives no gap above it and no figure for the card's height.
    The report asserts the switch's buttons below the title's bottom, the filter's top 8 below the
    buttons' bottom (±1), the buttons 4 apart and 28 high, and the card 460 wide at x 320 to 780 and
    centred vertically, and no height. Where the keyboard focus goes after a click on the switch is not
    said; the report asserts no focus after one and types into the filter only after clicking it (B2
    step 6).
15. **What the URL box shows after Choose (B7).** Both chosen URLs are longer than `CloneUrlBox` is wide
    beside its button, so the box scrolls them, and which part it shows depends on the caret, which 11.3
    does not set (7a's B6 URL fitted whole). The report asserts the box's `Text` and inspects what `b07a`
    and `b07b` show; no position of the text is asserted.
16. **How the R path opens the form (B1, B3).** 14.4's step 2 reads the button and clicks it without
    saying how the form opens after Settings has closed; the report opens it, in both harnesses, with a
    click on the welcome page's `CloneRepoButton`, so that the form under the R picture is the one empty
    tab's with empty boxes. The Azure DevOps sign-in types `PartA.Organization` (`Fernhill-Labs`), as
    A6's `a06b`, which the launch compares first.
17. **What part B does not picture.** GitLab's `MoreExistOf` line (ten pages of 100), the `Empty` and
    `Unexpected` lines and the failure kinds beyond B5's two are 11.6's tests'; 11.7 names none, and the
    report takes none. Should the lead want GitLab's cap pictured, as 7a's B9 pictured GitHub's, a
    scripted-only start with `served.GitLab.AddListedProjects("northfield", 1001)` before it would show
    the line under the list at its largest (`b09a-gitlab-cap`). Part B is dark at 1100×700 (0.5): no
    light or 1920×1080 picture, as the lead's ruling on 7a's open point 17.
18. **B8's order of runs.** 11.7 asks for the three commands, "one signed out signing in first". The
    report runs GitLab's signed in first (no dialog), then Azure DevOps' signed out, whose sign-in leaves
    two accounts, so that its browser shows the command's own provider with the switch (11.4's
    `provider ?? providers[0]` against D353's order), then GitHub's signed out and cancelled, which runs
    the third command and D300's stop. It reads the empty box before any command runs, since phase 6's
    matcher lists the session's runs first (5.6), and runs each command by its whole label.
19. **The failures' log lines and messages (B5).** 11.2 gives WP-B's line (GitHub's byte for byte); the
    HTTP core's and the session's warnings are WP-H's and WP-S's as built (`HostingHttpClient.Warn`,
    `HostingSession.CallAsync` and its refusal mark), read from the code, as 7a's B5 read WP-G's and
    WP-S's. The fake's empty 401 carries the reason phrase `Unauthorized`, which Azure DevOps' client
    takes as the message when the body has none (`AzureDevOpsClient.MessageOf`), so the failure's message
    is `Unauthorized`, not empty; with the account's login `Describe` gives `TokenRefused` whatever the
    message, so 11.2's empty-message rows are not reached by B5 and stay
    `HostingTextsDescribeProvidersTests`'.
20. **What 11.8's "no earlier picture changed beyond D248's box" allows.** The report reads it as the
    lead's ruling on 7a's open point 19 and this report's "Earlier pictures" do: a difference is allowed
    only in the status bar's git box, in what "Changes to expected results" lists as changed on purpose
    (for part B, nothing), in `b06a`'s port when 7a's fake fell back and in what varies by nature (a
    caret, a temporary folder's name, git's output), each opened and judged; any other difference in
    11.8's pictures is WP-B's defect.
21. **The button under a browser the palette opened (B8; 7a's `b03a-listed`).** 7a's palette command runs
    the button's own command (`MainWindowViewModel.Commands.cs:202`, `BrowseGitHubCommand.ExecuteAsync`),
    so the form's `From GitHub…` is drawn disabled under every 7a browser, the palette's `b03a-listed`
    (scripted and real window) among them. 11.5 has `CloneFromAccountAsync` call
    `welcome.BrowseAsync(provider)`, which runs no command: read literally, the button would be drawn
    enabled under a browser a palette command opened, in `VcActionButton`'s enabled foreground where 7a's
    `b03a-listed` has `VcTextDisabledBrush`, which 0.7's and 11.1's "no earlier picture changes" forbids.
    The report expects 7a's look under every browser over the form, the button's or a palette command's,
    which WP-B reaches by running the command with the provider (for example a `BrowseCommand` that takes
    a `HostingProvider?`, `null` from the button and the provider from the palette). **The lead's
    ruling (2026-11-06):** a palette command opens the browser by running the form's browse command
    with its provider, as 7a's runs the button's own command, so the button is drawn disabled under
    every browser over the form and 7a's `b03a-listed` does not change; WP-B builds it so, and 11.5's
    `welcome.BrowseAsync(provider)` is read as reached through that command. B8's rows say only
    "with `From …`", so their assertions hold either way, and 11.8's comparison of `b03a-listed` shows
    that WP-B kept it.
22. **Numbering and additions (part P).** Design 12.8's list is unnumbered; P1 to P14 follow its order:
    the rows and the badge per provider (P1), signed out (P2), loading (P3), failed (P4), none (P5),
    past 300 (P6), the filter (P7), D356's row (P8), ruling 5's listed remote (P9), the menus (P10), the
    checkouts (P11), copy link (P12), open in browser (P13) and a branch row's `!21` (P14). The list
    names no provider for signed out, loading and failed: the report takes each for both, since each
    provider's words differ, and asserts Azure DevOps' loading row without a picture (7a's text word for word);
    none and past 300 on GitLab alone (open point 24). Beyond the list, with texts and places the design
    gives: the labels' tooltips (P1, 12.6's suffix); the signed-out, loading and D356's section menus
    (P10, 12.4) and the signed-out menu's open in browser (P13, D257); D356's tooltip, its toggle and
    `manifest`'s branch menu without a request item (P8, 12.3, 12.4); `upstream/main`'s and
    `origin/topic/no-request`'s menus, GitHub's requests and the avatars (P9, 12.4, D367); the filter's
    `ada` (P7); `!22`'s, `!12`'s and `!13`'s checkout labels (P10); the branch menus' `Open merge request
    !21` and `Open pull request !11` (P14, 12.4).
23. **Which failure is pictured per provider (P4).** 12.8 says "failed in `Describe`'s words" and names
    no fault. The report takes GitLab's 403 `insufficient_granular_scope` on the list
    (`FakeGitLabFault.InsufficientGranular`), whose `Describe` is `GitLab answered:` and GitLab's own
    message, its code the tooltip's second line, as 7a's P4 showed GitHub's 403 with its `Detail`, and as
    16.1's As built has the texts show GitLab's message and never a permission derived from the code
    (D385's kind of refusal, here the project form, READ g2 G2.4); and Azure DevOps' 500
    (`FakeAzureDevOpsFault.ServerError`), whose `Describe` is the fixed sentence and whose `Detail` is
    null (6.9's As built), so its tooltip is its text alone. A 401 is not pictured: it marks the account
    (9.6), and the section then shows the signed-out row, as 7a's 7.8; B5 shows Azure DevOps' 401 in the
    browser. The other rows of 11.2's table are `HostingTextsDescribeProvidersTests`' (11.6).
24. **Azure DevOps' past 300 and none.** 12.8 puts past 300 on GitLab's `web-app/` and says "an Azure
    DevOps list, if one is used, has its pull requests made with `Conflicts` set"; the report uses none:
    Azure DevOps' `More than 300 open pull requests` and `No open pull requests` are 7a's sentences word
    for word, and its walk (three pages of `$top=100`, then the `$top=1&$skip=300` probe) is 6.8's
    `AzureDevOpsProviderTests`' and 12.7's `RequestSectionProvidersTests`'. Should the lead want it
    against the fake, a scripted-only step at the end of method 2 would add 298 pull requests from
    `topic/no-request` with `Conflicts` set (no merge commit, 3.5), expect four `pulls:` requests
    (`$skip` 0, 100 and 200, then `$top=1&$skip=300`) and the row `More than 300 open pull requests`
    (`p06b-azure-more-than-300`).
25. **GitLab's draft title (P1, P6, P7, P10).** GitLab keeps `Draft:` in the title and sets `draft` (4.2,
    r6 5.1), and A4 maps the title as GitLab gives it (7.4); 12.3 strips nothing. So `!22`'s row reads
    `!22 Draft: Update the readme` with the `Draft` mark beside it, its tooltip `… · Draft` and its badge's
    tooltip `draft merge request !22: Draft: Update the readme`, and the report expects that. If WP-P
    strips the prefix from what the section shows, the expected texts of `!22` change, recorded under
    "Changed in this report after its first commit" with the lead's ruling.
26. **The badges' and names' places (P1, P14).** At 1100×700 the Branch / Tag column is 130 wide, and
    `feature/search` with `!21` (and `feature/sign-in` with `!11`) shares its row with the remote branch
    at the same commit: the name is cut first so that the label, its badge whole, and the row's other
    label or its `+N` end inside the column (7a's G2 rule, phase 1's fold). Whether the remote label
    shows or folds into `+1`, where each name is cut and where each badge lies the builder reads from the
    control (`LabelsInRow`, `BadgeInRow`) and writes into `PartP`, as 7a's open point 41 did; the report
    asserts the rule, the badge's text and record, and no pixel. The section's titles cut at 260 (about
    110 to 150 px beside the author; Azure DevOps' authors `Ada Lovelace` and `Octo Cat` leave less room
    than GitLab's logins) are inspected likewise.
27. **How D356's row and the fork labels read (16.3).** 16.3 asks part P's writer to record both. D356's
    row `In harbor-co; signed in to Fernhill-Labs` (40 characters, about 240 px) is cut with "…" at the
    default 260, where the room from x 42 is about 205 px, so it reads about as far as `In harbor-co;
    signed in to Fernhi…` (inspected, not asserted); its tooltip holds the whole sentence, about 230
    characters, wrapped at the tooltip's width with nothing cut. A GitLab fork's label is
    `{Owner}:{branch}` with the namespace path as owner, `ada:fix-typo`; the scenario has no nested
    fork, so a label such as `northfield/team:fix-typo` is 12.7's tests'. Azure DevOps' fork label is
    `{Name}:{branch}`, `mobile-app-fork:fix-typo`, the fork in the base's project; across projects
    `{project}/{Name}:{branch}` (12.3), which the scenario lacks.
28. **The R twins' sign-in and row (P1, P10).** 14.4 step 1 signs in through Settings and step 2
    right-clicks "part P's row". The scripted twins sign in the same way, so that the state at the
    capture owes nothing to the session's shortcut, as 7a's P6 did (its open point 30), and the report
    names `!21` and `!11` as the rows whose labels WP-R reads (`PartP`'s row menu per provider).
29. **What a double-click on `!13` does (P11).** 12.7 says "`!13` starts no git call (`GitCallBudget`)";
    the report reads that no git call at all follows the double-click since the quiet mark, and that
    the click within it selects nothing, `!13`'s head (the fork's tip) being no row of `mobile-app`'s
    graph (7a's P9 rule, FD19). Azure DevOps' `HeadSha` may lag (12.9's risk): no part P check selects
    a request's head.
30. **FETCH_HEAD after GitLab's head ref (P11).** 12.5's line `<sha>`, a tab, an empty field, a tab,
    `'refs/merge-requests/23/head' of <url>` is ASSUMED from 7a's `refs/pull` line (16.2):
    `GitRepositoryPullRequestsTests` measures it on 2.56 and CI on 2.43. If git writes it otherwise, P11's
    method 6 step 4 takes the measured line, recorded under "Changed in this report after its first
    commit".
31. **The refresh after `!23`'s fetch (P11).** The report expects its one `mrs:` request answered 304:
    the GitLab fake's weak `ETag` over an unchanged body (4.4, MEASURED) and the core's answer kept per
    token and URL. If WP-P's build shows the body changing between the two reads, the expected status
    becomes 200, recorded with its reason.
32. **What part P does not picture.** A click on a row selecting its head (7a's P9); the drop menus'
    request items and the `Create …` items' forms (part N's, 13.8); `HeadNotNamed`, `NotFetchedBy`,
    `mr-{iid}`, a nested owner's local name and `FetchHeadChanged` (12.7's classes); the palette's
    request items (`RepositoryMenusProvidersTests`); D366's signed-out row on `manifest` with no Azure
    DevOps account (`RepositoryHostingProvidersTests`, 9.10); light and 1920×1080 (0.5: part P is dark
    at 1100×700). Part P's 22 scripted pictures with part A's 29 and part B's 20 pass 0.6's estimate of 55
    to 70 for the phase before part N's; the lead may drop scripted-only pictures the inspection finds
    redundant (`p01b`, `p04b`, `p08b`, `p10b`), their steps asserted either way.
33. **The region behind `a06c` after WP-P** (open point 10): read under "Changes to expected results",
    part P: only the message row's visible start changes, and where the card cuts it is inspected at
    WP-P's merge (12.9), nothing asserted.
34. **Numbering and additions (part N).** Design 13.8's list is unnumbered; N1 to N9 follow its order:
    the menus' items (N1), each provider's form (N2), GitLab's Draft (N3), `local-work` pushed first
    (N4), the refusals (N5), the request listed with the offer (N6), long repository texts (N7), signed
    out (N8), 1920×1080 and light (N9). The list names a provider for few of them: the report takes N1
    to N6 and N8 for both providers, since each provider's words, requests and numbers differ, and N7
    and N9 for one (Azure DevOps' longest name; GitLab's form, which has the new line). Beyond the list,
    with texts and places the design gives: Azure DevOps' Draft (N3: `isDraft`, neither line); the
    creating state while the POST is held (N4, as 7a's N2); the cancelled sign-in (N8, as 7a's N6); the
    offer's click and its going at the next operation, the branch menu's `Open … !n` and the section's
    menu without a create item (N6, as 7a's N1); `Open merge request !24`'s click (N5, as 7a's N4); the
    Pick with a long detail (N7).
35. **Which branch the R twins create from (N2, N6).** 14.4 step 3 names "part N's branch row" and step 4
    a receive-pack "if the branch was pushed". The report takes the prepared `topic/no-request`, the
    state 13.8 and 14.2 make for part N, level with its upstream: Create pushes nothing, so `PartN`'s
    twins say "not pushed" and the real window asserts no receive-pack. `local-work`'s push (N4) is
    scripted only; the app's helper run by Git for Windows' `sh` for both new hosts is proven in the real
    window by B7's two clones (14.4). Should the lead want the real push, WP-R's twin would create from
    `local-work` and N4's pictures after the create would become its twins. The lead ruled on 2026-11-07, before
    WP-N: the twins create from `topic/no-request`, as this point reads it.
36. **GitLab's read of the source before the POST (N3 to N6).** 7.4's A5 reads `projects/<id>` of the
    source before every POST and posts nothing on its failure. The report expects one such read per
    create, answered 304: the HTTP core sends the `ETag` it kept from the list's A3 read of the same URL
    with the same token (5.2; `HostingHttpClient`'s kept answers) and the fake answers 304 over an
    unchanged body (4.4). If WP-N's build shows it 200 (an answer not kept), N3 to N6's expected status
    becomes 200, recorded under "Changed in this report after its first commit". Azure DevOps reads
    nothing before a POST within one repository (6.6).
37. **The missing branch and its words (N5).** 13.8 asks for "a missing branch" and names no way to make
    one. The report deletes the head's branch in the fake's bare folder after the list was read, so that
    the clone still holds `origin/topic/no-request` level with the local branch (no push, D313) while
    the service refuses, as gitlab.com and dev.azure.com would after another user deleted it: GitLab's
    400 naming `source_branch` (`HeadMissing`), Azure DevOps' TF401398 (`BranchesMissing`), both
    MEASURED in stage 1. 13.2 gives `HeadUnknown(p, branch, repo)` and `BranchesUnknown(p, head, base)`
    without saying which texts fill them: the report reads `{branch}` and `{head}` as the head's branch on
    the provider (`topic/no-request`), `{repo}` as the head's repository's full name and `{base}` as the
    base's branch (`main`), as 7a's `BaseUnknown(baseBranch, repository)` takes branch and repository, so
    `GitLab has no branch topic/no-request in northfield/platform/web-app to merge from. Fetch, then try
    again.` and `Azure DevOps cannot find topic/no-request or main. Fetch, then try again.`. If WP-N fills
    them with the form's texts (`origin/main`), the expected texts change, recorded with the lead's
    ruling. The branch is restored before the "already open" step, so that one form shows both
    refusals, as 7a's N4 showed two in one form.
38. **The rows' widths (N2, N4).** With 7a's font (`octo/hello` 59 px for 10 characters,
    `Phase7aExpectations.PartN.FormLayout`), `origin/main`, `Fernhill-Labs/Mobile App/mobile-app`,
    `Choose…` and two gaps of 8 come to about 379 of the 370-px column, so Azure DevOps' Into repository
    text is expected cut by a few pixels, `Choose…` whole; the estimate lies within a few pixels of the
    column either way, so the report asserts the cut rule (part N's conventions, "The form": cut with
    "…" exactly when the text's natural width passes the room its row leaves it, both measured from the
    controls at run time; its tooltip whole and `Choose…` whole either way) and not which: no constant in
    `PartN` says whether it is cut, and nothing in `PartN` is taken from the built control (D244). The
    lead inspects where the cut falls in `n02b` and `n04c`. GitLab's rows (about 275 and 331) and Azure
    DevOps' From row (about 323) are expected whole, as the rule gives them by 39 px or more.
39. **The long repository texts (N7).** 13.8 asks for "long repository texts cut with their tooltips",
    and the scenario's long repositories (`customer-onboarding-and-identity-verification-service`,
    `…/invoice-generation-and-reconciliation`) are empty and no remote of any clone, so no form reaches
    them. The report adds the remote `long` and two remote-tracking refs to `mobile-app/` with git while
    the app runs, after N5's steps (so that N5's pictures show the scenario's refs alone), which gives a
    head and a base in the long repository within `RequestRemotes` (D350) with no fetch and no create;
    nothing is sent for them. Should the lead prefer a scenario fact (a fork with a long path, as
    13.7's fork case adds one), N7's steps change; 13.7's class keeps the 60-character case either way.
40. **What stage 1 settled of 13.9's risks.** Azure DevOps' create status (201, `CreateStatus`, MEASURED
    2026-11-05; the client takes 200 too, 13.7) and a create with nothing ahead (201, MEASURED 2026-11-05
    on Azure DevOps and 2026-11-06 on GitLab; part N does not picture it, 13.7); GitLab's 409 and
    missing-branch 400 bodies and `draft` for a `Draft:` title (MEASURED 2026-11-06). Kept: Azure DevOps'
    `forkSource` (ASSUMED, no fork in the sandbox, D375) and `mergeStatus` `queued` in a create's answer
    (READ, which the app does not read); GitLab's `[Draft]` and `(Draft)` prefixes (READ, r6 5.1). No part
    N check uses a fork's head: no clone of the scenario has a fork as a remote, and the fork bodies
    (`target_project_id`, `forkSource.repository.id`) are `CreateRequestOperationProvidersTests`'. Stage
    2's `Check_3` creates through the app per provider after WP-N's merge.
41. **The lines' places and the card's height (N2, N3, N9).** 13.4 gives each GitLab line's class,
    margin and place and no figure for the card; 7a's card is 386 high as built. The report asserts that
    everything above a shown line stays at Azure DevOps' places and that what is below it moves down by
    its height plus its margin of 4, and gives no height: about 404 with the quick-actions note and about
    422 with the sent title too (one `settingsNote` line about 14 high, part A's 13.9).
42. **The offer's next operation (N6).** 7a's N1 ended the offer with the toolbar's Fetch. On `web-app` a
    Fetch also fetches `upstream` from GitHub's fake without an account, which no 7b check does; the
    report ends it with P11's local checkout of `!21` (`!11` on Azure DevOps), a user operation that asks
    no fake anything (D317: the offer goes when the user's next operation starts).
43. **The list reads after a create (N3, N4, N6).** As 7a's open point 51: the create's own refresh, then
    the refresh after a succeeded network operation, two list reads; GitLab's second answered 304 (the
    weak `ETag`, 4.4; part P's open point 31), Azure DevOps' both 200 (its fake sends no `ETag`, 3.4). If
    WP-N's build reads the list once (the two refreshes merged), the expected counts become one, recorded
    with its reason.
44. **What part N does not picture, and its count.** `Reading GitLab…` and `Reading Azure DevOps…` (A3 is
    kept from the list's read), Cancel during the push (7a's N7, the same code), the form's checks (the
    same branch, the Pick's Esc, an empty title: 7a's N8, the same code), `NoCommits` (GitHub's alone,
    13.5), `Refused` and `NotVisible`, a lost answer (`CreatedThenDropped`), `manifest/` not started (13.3;
    P8 shows no item there), quick actions run by the service (ruling 20: the fake runs none, D370) and a
    fork's head (open point 40) are 13.7's classes'. Part N's 22 scripted pictures bring the phase to 93
    with parts A's 29, B's 20 and P's 22, past 0.6's 55 to 70; the lead may drop scripted-only pictures
    the inspection finds redundant (`n01a`, `n03a`, `n04b`, `n07b`), their steps asserted either way.
    Its 4 R captures bring the real window to 0.6's 13. Its seven methods (13.8's "about 5 to 7") bring
    the phase to 29 with parts A's 9, B's 6 and P's 7, past 0.5's about 22 to 27 and under D230's 40,
    for the lead to accept. None merges within the conventions (one method per app start, one provider
    per method): methods 1 and 2 are the R twins, 3 and 4 start signed out, 5 and 6 each make their
    provider's refusals, and 7 starts at 1920×1080. The nearest merge, N5's GitLab steps in method 7,
    would make 28 but cannot keep either check's pictures: before N9's steps N5's open `!24` on
    `topic/no-request` puts `Open merge request !24` where N9 clicks `Create merge request…`, and after
    them `n05a` and `n05b` would be taken at 1920×1080 in light; the report keeps them apart. The lead ruled on 2026-11-07,
    before WP-N: the seven methods and the 22 pictures stand (0.5's and 0.6's figures are the design's
    estimates; D230's 40 holds), and the gate's inspection names any picture it finds redundant before
    one is dropped.
45. **The sign-in from a remote branch and a drop (N1, N8).** 7a's N6 signed in from a local branch's
    item; the report signs in from the remote branch's (GitLab) and the drop's (Azure DevOps), two more
    of 12.4's hooks, and reads a drop's base given before the sign-in as the form's base after it (13.3:
    the base is read from the refs once the sign-in has ended). If WP-N drops a drop's base across the
    sign-in, method 4's expected `Into` changes, recorded with its reason.
