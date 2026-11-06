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
whose findings WP-LF put into the fakes before part A's checks were written (design 0.4, 16.1); stage 2
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
`wwwauth[]`). Part A rests on these answers (A8 to A10). Still to come: `Check_1b`'s probe of the sandbox
organization's connectionData without a credential, which settles whether the Anonymous 200 holds in a
private organization (16.1; open point 8): 16.1 asks for it before part A's checks, but WP-LF added it to
the live command for stage 2 (status step 16, after WP-N's merge; the status's WP-LF note), so it runs
with stage 2 unless the lead runs it before WP-A's merge; then stage 2's `Check_3` and the gate's two runs.

## What the checks share

### Conventions of every part

What every part of phase 7b relies on; each part's conventions add to these. Where an item says "as 7a",
phase 7a's report, "Conventions of every part", holds it word for word and is not repeated here.

- **Parts and the project** (design 0.3, 0.5, 2.7; D230). One project,
  `tests/VisualCommit.VisualTests.Phase7b`, about 22 to 27 test methods for the phase (under D230's 40),
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
- **Scrolling** (D353, 10.3). Three signed-in blocks overflow the section at 1100×700 (about 553 of 495);
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
  (MEASURED 2026-11-05), no `Login`; a value in none of those forms, 200 with the Anonymous identity
  (MEASURED 2026-11-05 in the sandbox's organization; in a private organization such as Fernhill-Labs
  ASSUMED, open point 8), no `Login`. 2. Only after a member's 200: the core resource area,
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

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). Each check is named by its part's letter
and its place in design 10.7's list, numbered here in its order (open point 1); its scenario, theme and
window size end its "Check" column; its methods and starts are in its part's table above. "R" in Notes
marks the steps the real-window pass repeats (design 10.7, 14.4), "n/a" says why a check is not repeated.
"Screenshots" lists the planned pictures, the scripted run's and then the real window's. Every step's
"requests" are those the three fakes recorded since the step's mark; every "calls" are the store's
`Calls` since its mark; "git calls" are the call log's, since a quiet mark (part A's "Names in the
checks"). A step numbered in a method runs on from that method's last step in an earlier row.

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| A1 | The intro and three blocks signed out (D353, D362; design 10.2, 10.3, 10.9, 0.7): one intro for the three providers, as high as 7a's sentence in the same width; the blocks GitHub, GitLab and Azure DevOps, 24 apart, each 7a's signed-out block under its provider's name; opening the section asks no fake and no keychain and adds no git call to the screen's own read; "hosting7b" served, one empty tab, the three fakes, dark, 1100×700 | **Method 1** (`Check_A1_A2_signed_out_to_three_signed_in`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)` (one empty tab, the three fakes, a new `FakeTokenStore`); wait for the welcome page. Mark the requests and the calls; take the quiet call mark (`QuietCallMarkAsync()`). 2. `OpenAccountsAsync()`. Measure the intro (part A's conventions, "The intro"). Screenshot (`a01a-three-signed-out`). | **After step 1:** the welcome page; the session's `IsOff` false, `Providers` exactly GitHub, GitLab, Azure DevOps in that order, `IsOn` true for each, `AccountOf` null for each; the app's log holds, in this order, `GitHub: 127.0.0.1:47399.`, `GitLab: 127.0.0.1:47398.`, `Azure DevOps: 127.0.0.1:47397.`, then `GitHub: no account on 127.0.0.1:47399.`, `GitLab: no account on 127.0.0.1:47398.`, `Azure DevOps: no account on 127.0.0.1:47397.` (through `WithPorts`); requests none (D257, D350); calls none; no `accounts.json`.<br>**After step 2:** the screen `SettingsDialog` on the dialog layer over the welcome page, the card exactly at (120, 40), 860×620, its five nav buttons as 7a's A1, `SettingsNavAccounts` at (140, 223) to (316, 253) in the `selected` look with the keyboard focus; `SelectedSection` `Accounts`. From x 336, top to bottom: `AccountsIntro` with 10.2's sentence (secondary, wrapped, whole), in exactly two lines and as high (±0.5) as 7a's sentence measured at its width; 18 below, `AccountsGitHub`: the label `GitHub` (x 336 to 468) and from x 480 `AccountsSignedOut` `Not signed in.`, 10 below it `AccountsSignInButton` `Sign in…`, enabled; 24 below that block, `AccountsGitLab`: the label `GitLab`, `AccountsSignedOutGitLab` `Not signed in.` and `AccountsSignInButtonGitLab` `Sign in…`, enabled, in the same columns and gaps; 24 below, `AccountsAzureDevOps`: the label `Azure DevOps`, `AccountsSignedOutAzureDevOps` `Not signed in.`, `AccountsSignInButtonAzureDevOps` `Sign in…`, enabled. In no block an `AccountLogin`, `AccountUser`, `AccountTokenPlace`, `AccountNeedsSignIn`, `AccountsSignOutNote`, `AccountsSignOutButton`, `AccountsBusy` or `Git` row, with or without a suffix; no `AccountsOff`. GitHub's block starts 18 below the intro, at 7a's place. The section fits: its extent within its viewport, offset 0. `Cancel` and `Save` as phase 6's part S; no text cut; no token on screen. Git calls since the quiet mark (`QuietCallsSinceAsync`, whole refreshes set aside by `GitCallBudget.NotWholeRefreshes`, none expected on an empty tab): exactly the screen's reads on an empty tab (phase 6's SD29: the outside read `git config --show-scope --show-origin -z --list` in `{data}`), nothing else; exact, because it is the check's meaning (ruling 28). Requests none; calls none. |  | n/a | `a01a-three-signed-out` | n/a: WP-R's three-account launch passes this state but captures only A2's end (14.4); 7a's real-window `a01a` keeps the signed-out GitHub block. The intro is measured first (10.9): one or three lines would move every block of 7a's 23 pictures. The start's log lines are those the real window waits for (14.3). Varies by nature: the status bar's git box behind the backdrop (D248). |
| A2 | One, two and three signed in, the last at the top and at the section's end (D345, D348, D353; design 10.3, 10.4, 6.4, 7.4, 9.3; R3): each sign-in through its block's "Sign in…" and its dialog with the keychain; GitHub's block 7a's; the section scrolls with three; Fernhill-Labs bold over Ada's name; the organization typed in lower case shown in the server's spelling; three records and three keys; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 1**, after A1: 3. Mark the requests and the calls. `OpenSignInDialogAsync(GitHub)`; `TypeToken(GitHubToken)`; `ConfirmSignInAsync()`; wait until the dialog has closed. Screenshot (`a02a-github-signed-in`). 4. Mark. `OpenSignInDialogAsync(GitLab)`; `TypeToken(GitLabToken)`; `ConfirmSignInAsync()`; wait until the dialog has closed. Screenshot (`a02b-gitlab-signed-in`). 5. Mark. `OpenSignInDialogAsync(AzureDevOps)`; `TypeOrganization(PartA.OrganizationLower)`; click `SignInToken`; `TypeToken(AzureDevOpsToken)`; `ConfirmSignInAsync()`; wait until the dialog has closed. 6. Turn the wheel up over `SettingsSection` until the offset stops changing. Screenshot (`a02c-three-signed-in-top`). 7. Turn the wheel down until the offset stops changing; assert the end. Screenshot (`a02d-three-signed-in-end`). 8. Press Esc. | **After step 3:** GitHub's dialog was 7a's, `Add GitHub account` with 7a's help and link and the focus in `SignInToken`. Calls exactly `probe` (the dialog's `KeychainAvailableAsync`), then `save github:127.0.0.1:47399:octo`; requests: GitHub's exactly one, `GET api/v3/user` (route `user`), `TokenPresent`, 200, `Login` `octo`; GitLab's and Azure DevOps' none. The session's `AccountOf(GitHub)` `SignedIn`, place `Keychain`, note kind `None`, the scenario facts' account; the other two null. GitHub's block as 7a's A5 step 7 shows it: `AccountLogin` `octo` (SemiBold) from x 480, `AccountTokenPlace` `The token is kept in this computer's keychain.`, `Sign out…` enabled with the keyboard focus and no `Sign in…`, the `Git` row with `AccountUseForGit` `Use for git over HTTPS` checked and 7a's note; no `AccountUser`; GitLab's and Azure DevOps' blocks as after step 2, each 24 below the one before; the section fits (open point 2); no text cut; no token on screen.<br>**After step 4:** the dialog was `Add GitLab account` (A5's look) with the focus in `SignInToken`. Calls exactly `save gitlab:127.0.0.1:47398:ada` (the probe was step 3's); requests: GitLab's exactly one, `GET /api/v4/user` (route `user`), `Bearer`, 200, `Login` `ada`; the others none. `AccountOf(GitLab)` `SignedIn`, `Keychain`, `None`, the scenario facts' account. GitLab's block: `AccountLoginGitLab` `ada` (SemiBold, from x 480), no `AccountUserGitLab`, `AccountTokenPlaceGitLab` with the keychain's text, `AccountsSignOutButtonGitLab` enabled with the focus, no `Sign in…`; its `Git` row with `AccountUseForGitGitLab` checked and `AccountUseForGitNoteGitLab` GitLab's note; GitHub's block unchanged; Azure DevOps' signed out; the section fits (open point 2); no text cut.<br>**After step 5:** the dialog was `Add Azure DevOps account` and opened with `SignInOrganization` empty and focused (an empty tab, no account held, 10.5). Calls exactly `save azure-devops:127.0.0.1:47397:fernhill-labs`; requests: Azure DevOps' exactly two, in order: `GET /fernhill-labs/_apis/connectionData`, query `api-version=7.1-preview.1` (route `connection:fernhill-labs`), `BasicEmptyUser`, 200, `Login` Ada's; then `GET /fernhill-labs/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf`, the same query (route `areas`), `BasicEmptyUser`, 200; GitHub's and GitLab's none. `AccountOf(AzureDevOps)` `SignedIn`, `Keychain`, `None`, `Login` `Fernhill-Labs` (the resource area's spelling, though `fernhill-labs` was typed, 6.4 step 2), key `azure-devops:127.0.0.1:47397:fernhill-labs`, `Id` 0, `Name` `Ada Lovelace`, `UserId` Ada's id in lower case, `UseForGit` true. The focus on `AccountsSignOutButtonAzureDevOps`, wholly inside the viewport.<br>**After step 6:** offset 0; the section scrolls (its extent above its viewport, about 553 of 495, 10.3). Top to bottom: the intro, GitHub's block, GitLab's block, and Azure DevOps' from its label: `AccountLoginAzureDevOps` `Fernhill-Labs` (SemiBold) from x 480, directly under it `AccountUserAzureDevOps` `Ada Lovelace` (`secondary`, `#9BA3B0`, regular weight), then `AccountTokenPlaceAzureDevOps` with the keychain's text and `AccountsSignOutButtonAzureDevOps`; what lies below the viewport's bottom is scrolled out of view, not cut. No text cut; no token on screen.<br>**After step 7:** before the capture the offset is the end (`Extent.Height − Viewport.Height`, ±0.5) and above 0; Azure DevOps' block wholly in view, its `Git` row with `AccountUseForGitAzureDevOps` checked and `AccountUseForGitNoteAzureDevOps` Azure DevOps' note (open point 3), its last line inside the viewport; the intro and the top of GitHub's block scrolled out of view; the focus still on Azure DevOps' "Sign out…"; no text cut; no token on screen; requests and calls none since step 5.<br>**After step 8:** the screen closed; the three still signed in (D256). `accounts.json`: `version` 1 and three records with exactly the scenario facts' fields, each `useForGit` true and `tokenState` `keychain`, `userId` on Azure DevOps' alone; no `@` in the file. `Tokens.Holds` each of the three keys. |  |  | `a02a-github-signed-in`, `a02b-gitlab-signed-in`, `a02c-three-signed-in-top`, `a02d-three-signed-in-end`; real window: `a02d-three-signed-in-end` | **R** (14.4 A, `Check_A_three_accounts`, steps 3 to 7): the launch makes no request (D257, D350); `SignInThroughSettings` with `keepOpen` for GitHub, GitLab and Azure DevOps in D353's order, typing the scenario's tokens and `PartA.OrganizationLower`; each fake's requests as steps 3 to 5 give them, route keys in order (`user`; `user`; `connection:fernhill-labs`, `areas`), with `Bearer` on GitLab's and `BasicEmptyUser` on Azure DevOps', 200; `ScrollAccountsToEnd` (the real wheel until UI Automation's `VerticalScrollPercent` is 100, asserted before the capture); compared with `a02d-three-signed-in-end`; Esc; `accounts.json` holds three records, `userId` on Azure DevOps' alone, no token; `AssertCleanEnd`. The real window's store is memory, available, so it shows the keychain's text. "One, two and three signed in" read as GitHub, then GitLab, then Azure DevOps, D353's order, as WP-R's launch signs in. `a02a` to `a02c` are scripted only. Varies by nature: the status bar's git box. |
| A3 | GitLab off, its block gone; all three off (D353, D362; design 10.3, 1.8): a provider without a fake is off and its block takes no room; with all off, GitHub's block holds 7a's off text alone; "hosting7b" served, one empty tab, GitHub's and Azure DevOps' fakes alone; then a start of its own with nothing served and no fake; dark, 1100×700 | **Method 2** (`Check_A3_gitlab_off`): 1. `ServeAsync()`. `ShellDriver.Start(data, gitHub: served.GitHub, azureDevOps: served.AzureDevOps)` (no GitLab fake given: GitLab off, D362); wait until ready; rest the pointer. Mark the requests. 2. `OpenAccountsAsync()`. Screenshot (`a03a-gitlab-off`). Close the app.<br>**Method 3** (`Check_A3_three_off`), a data folder of its own, nothing served: 1. `ShellDriver.Start(data)` (no fake: the three off); wait until ready. 2. `OpenAccountsAsync()`. Screenshot (`a03b-three-off`). Close the app. | **Method 2, after step 1:** `Providers` exactly GitHub, Azure DevOps; `IsOn(GitLab)` false and `ProviderOf(GitLab)` null; the log's start lines `GitHub: 127.0.0.1:47399.`, `GitLab: off.`, `Azure DevOps: 127.0.0.1:47397.` in that order; requests none in any of the three fakes (GitLab's runs but is given to no session); calls none.<br>**Method 2, after step 2:** the intro as A1's (the one sentence, 10.2); `AccountsGitHub` as A1's; 24 below it `AccountsAzureDevOps` as A1's; `AccountsGitLab` not visible and taking no room, no control whose id ends in `GitLab` visible; the section fits; no text cut; requests none; calls none.<br>**Method 3, after step 1:** the session's `IsOff` true, `Providers` empty.<br>**Method 3, after step 2:** the intro (the same sentence); `AccountsGitHub` alone, its label `GitHub` and from x 480 `AccountsOff` `GitHub is turned off for this session.` (7a's text, D362), no `Sign in…` or `Sign out…` (7a's: no button while off); no `AccountsGitLab` or `AccountsAzureDevOps` visible; no text cut; calls none. |  | n/a | `a03a-gitlab-off`, `a03b-three-off` | n/a: the three providers are on in every real-window launch (14.2). The all-off start goes beyond 10.7's list, for D362's "with all three off the section shows 7a's off text" (open point 1); it is a method of its own, since it starts on a data folder of its own (one method per app start). Varies by nature: the status bar's git box. |
| A4 | Fernhill-Labs bold over Ada's name (design 10.3, 6.4; D368): Azure DevOps' block alone signed in, its organization as the login and the account's name under it, no e-mail address anywhere; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 5**, after A7: 11. Wait until the dialog has closed and `WaitForAccountAsync(AzureDevOps, a => a?.State == AccountState.SignedIn)`. Screenshot (`a04a-azure-signed-in`). 12. Press Esc. | **After step 11:** requests since A7's mark exactly two: `connection:fernhill-labs`, `BasicEmptyUser`, 200, `Login` Ada's, then `areas`, 200 (the held request answered after its release); calls since A7's mark exactly `save azure-devops:127.0.0.1:47397:fernhill-labs`. `AccountOf(AzureDevOps)` as A2's step 5 gives it (`Fernhill-Labs`, `Id` 0, `Name` `Ada Lovelace`, `UserId`); `AccountOf(GitHub)` and `AccountOf(GitLab)` null. The section at offset 0, fitting: GitHub's and GitLab's blocks signed out as in A1; Azure DevOps' block: the label `Azure DevOps`; `AccountLoginAzureDevOps` `Fernhill-Labs` (SemiBold) from x 480; directly under it `AccountUserAzureDevOps` `Ada Lovelace` (`secondary`, `#9BA3B0`, regular weight); then `AccountTokenPlaceAzureDevOps` `The token is kept in this computer's keychain.` (`settingsNote`); 10 below, `AccountsSignOutButtonAzureDevOps` enabled with the keyboard focus, no `Sign in…`; the `Git` row: `AccountUseForGitAzureDevOps` checked and `AccountUseForGitNoteAzureDevOps` Azure DevOps' note; no text cut; no token on screen. `accounts.json`: one record, Azure DevOps' nine fields, `useForGit` true, `tokenState` `keychain`; no `@` in the file (Ada's sign-in name, an address, is never kept: 6.4, D368). `Holds(azure-devops:127.0.0.1:47397:fernhill-labs)`.<br>**After step 12:** the screen closed; still signed in (D256). |  | n/a | `a04a-azure-signed-in` | n/a: the real window shows the bold organization over Ada's name in A2's end picture (three accounts). The name is the account's `Name` (her `customDisplayName`), never the identity's `Account.$value`. GitHub's and GitLab's blocks show no name line (`UserName` null, 10.3). |
| A5 | GitLab's dialog: empty, a masked token, both links' launcher records (D345, D385; design 10.2, 10.4, 7.4): the help naming D385's user permissions, the fine-grained link and the legacy link 4 below it; the token masked on screen and to UI Automation; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 4** (`Check_A5_A8_A11_gitlab_dialog_links_and_refusals`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)`; `OpenAccountsAsync()`. Mark the requests and the calls. 2. `OpenSignInDialogAsync(GitLab)`. Screenshot (`a05a-gitlab-dialog`). 3. `TypeToken(GitLabToken)`. Screenshot (`a05b-gitlab-token-masked`). 4. Rest the pointer on `SignInTokenLink` until its tooltip shows; then click it. 5. Rest the pointer on `SignInLegacyTokenLink` until its tooltip shows; then click it. | **After step 2:** `SignInDialog`, named `Add GitLab account`, stacked over the screen (the card under the dialog's backdrop, still on Accounts, the three blocks signed out); the card 400 wide, x 350 to 750, centred vertically; top to bottom `DialogTitle` `Add GitLab account`, `SignInHelp` with GitLab's text (whole, wrapped), `SignInTokenLink` `Create a fine-grained token on GitLab` in `VcAccentBrush` `#7C8CFF`, 6 below the help, `SignInLegacyTokenLink` `Or create a legacy token, filled in` in the accent too, 4 below the first, the label `Token`, `SignInToken` empty with the keyboard focus; no `SignInOrganization`, `SignInNoKeychain`, `SignInChecking`, `SignInError` or `SignInSaveFailed`; `Cancel` enabled and `Sign in` (`VcPrimaryButton`) disabled, right-aligned; no "In …" line. Calls `probe`; requests none; no text cut.<br>**After step 3:** the box shows one `•` per character of `GitLabToken`; its automation value empty and its name `Token`; no token on screen; `Sign in` enabled; `Launcher.Requests` empty (the picture's state owes nothing to the fake launcher, R); requests none; calls still `probe` only.<br>**After step 4:** the tooltip `Opens GitLab's form for a new fine-grained token in your browser.`; after the click `Launcher.Requests` holds one launch, `OpenUrl` `http://127.0.0.1:47398/-/user_settings/personal_access_tokens/granular/new`; the dialog still open, the box still masked and holding the token, `Sign in` enabled; requests none.<br>**After step 5:** the tooltip `Opens GitLab's form for a legacy token with the api scope in your browser.`; after the click `Launcher.Requests` holds two launches, the second `OpenUrl` `http://127.0.0.1:47398/-/user_settings/personal_access_tokens?name=VisualCommit&scopes=api`; the dialog still open as after step 4; requests none; calls still `probe` only. |  |  | `a05a-gitlab-dialog`, `a05b-gitlab-token-masked`; real window: `a05b-gitlab-token-masked` | **R** (14.4, `Check_GitLab_sign_in_browse_and_clone` step 1): `OpenSignInDialog(GitLab)` from the Accounts section; a real click on `SignInToken`; `GitLabToken` typed on the real keyboard: the box's Value pattern reads "" and its name "Token", no text element holds the token; compared with `a05b-gitlab-token-masked`. Steps 4 and 5 are scripted only (the fake launcher; no 7b step opens the browser, ruling 29) and come after `a05b`, so that the R picture's state is reached as the real window reaches it. Varies by nature: the box's caret (`a05a`, `a05b`); the status bar's git box. |
| A6 | Azure DevOps' dialog empty, with the organization typed and a masked token, and prefilled from mobile-app's tab (D360; design 10.2, 10.4, 10.5, 6.6): the organization box above the token, focused when empty; the link enabled only with an organization; the prefill from the tab's remote; "hosting7b", one empty tab, then a tab on `mobile-app` signed out, the three fakes, dark, 1100×700 | **Method 5** (`Check_A6_to_A11_and_A4_azure_dialog`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)`; `OpenAccountsAsync()`. Mark the requests and the calls. 2. `OpenSignInDialogAsync(AzureDevOps)`. Screenshot (`a06a-azure-dialog-empty`). 3. `TypeOrganization(PartA.Organization)`; click `SignInToken`; `TypeToken(AzureDevOpsToken)`. Screenshot (`a06b-azure-token-masked`). 4. Rest the pointer on `SignInTokenLink` until its tooltip shows; then click it.<br>**Method 6** (`Check_A6_azure_dialog_prefilled_from_the_tab`): 1. `ServeAsync()`. `OpenAsync(data, served, signIn: [])` (one tab on `mobile-app`, signed out). Mark the requests and the calls. 2. `OpenAccountsAsync()`; `OpenSignInDialogAsync(AzureDevOps)`. Screenshot (`a06c-azure-prefilled`). 3. `TypeToken(AzureDevOpsToken)`; press Enter; wait until the dialog has closed; wait for the log line `mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app` since the mark and for the watchers to be quiet. Press Esc. | **Method 5, after step 2:** `SignInDialog`, named `Add Azure DevOps account`, stacked over the screen as A5's; the card 400 wide, x 350 to 750, centred vertically, at most about 345 high (10.4; open point 7); top to bottom `DialogTitle` `Add Azure DevOps account`, `SignInHelp` with Azure DevOps' text (whole), `SignInTokenLink` `Create a token on Azure DevOps`, 6 below the help, disabled (no organization, 10.4), in the disabled look: the theme's disabled foreground `VcTextDisabledBrush` `#5F6775` in place of the accent and the arrow cursor, and after the capture, with the pointer moved onto it, no underline, the label `Organization`, `SignInOrganization` empty (`dialogBox`, automation name `Organization`) with the keyboard focus (focused when empty, 10.4), the label `Token`, `SignInToken` empty (open point 4); no `SignInLegacyTokenLink`, `SignInNoKeychain` or status line; `Cancel` enabled, `Sign in` disabled. Calls `probe`; requests none; no text cut.<br>**Method 5, after step 3:** `SignInOrganization` `Fernhill-Labs`; `SignInTokenLink` enabled; the token box masked, one `•` per character, its automation value empty and name `Token`; no token on screen; `Sign in` enabled; `Launcher.Requests` empty (R); requests none; calls still `probe` only; the card still within 345 high.<br>**Method 5, after step 4:** the tooltip `Opens the organization's page for new tokens in your browser.`; after the click `Launcher.Requests` holds one launch, `OpenUrl` `http://127.0.0.1:47397/Fernhill-Labs/_usersSettings/tokens` (6.6); the dialog still open, both boxes as after step 3; requests none.<br>**Method 6, after step 1:** `mobile-app`'s tab with its graph; `AccountOf(AzureDevOps)` null; requests none in any fake since the start (no account: nothing is sent for the tab, D257, D350).<br>**Method 6, after step 2:** the dialog as method 5's after step 2, but `SignInOrganization` holds `Fernhill-Labs` (the `OwnerRoot` of the tab's origin, 10.5), the keyboard focus in `SignInToken`, `SignInTokenLink` enabled, `Sign in` disabled (no token); calls `probe`; requests none; no text cut.<br>**Method 6, after step 3:** requests since the mark begin with `connection:fernhill-labs` (`GET /Fernhill-Labs/_apis/connectionData`, the prefilled organization sent as it reads), `BasicEmptyUser`, 200, then `areas`, 200; calls `save azure-devops:127.0.0.1:47397:fernhill-labs`; `AccountOf(AzureDevOps)` `SignedIn` with `Login` `Fernhill-Labs`; the log line written; after Esc the screen closed. |  |  | `a06a-azure-dialog-empty`, `a06b-azure-token-masked`, `a06c-azure-prefilled`; real window: `a06b-azure-token-masked` | **R** (14.4, Azure DevOps' "Sign-in, browse, clone" launch, step 1): `OpenSignInDialog(AzureDevOps)`; `TypeOrganization` with `PartA.Organization` (a click on `SignInOrganization`, Ctrl+A, typing; its Value read back `Fernhill-Labs`); a click on `SignInToken`, the token typed on the real keyboard: its Value "" and name "Token"; compared with `a06b-azure-token-masked`. `a06a`, `a06c` and step 4 are scripted only. **WP-P's region**: in `a06c` mobile-app's left panel and graph lie behind the backdrop; nothing is asserted there, and the lead inspects the picture again at WP-P's merge (0.4, 12.9, 16.2). Varies by nature: the boxes' caret; the status bar's git box. |
| A7 | The check running (design 10.4, 7a design 8.4): `Checking the token…` with both boxes and "Sign in" disabled while connectionData is held; nothing kept until it answers; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 5**, after A10: 10. With the keyboard focus in `SignInOrganization` and its whole text selected by A10's refusal, `app.Type(PartA.Organization)` (the selection replaced). Mark the requests and the calls. `served.AzureDevOps.HoldNext("connection:fernhill-labs")`. Click the dialog's `Sign in`; wait for the hold's `Arrived`. Screenshot (`a07a-azure-checking`). Dispose the hold (its release; A4's step 11 follows). | **After step 10:** below the token box `SignInChecking` `Checking the token…` (secondary); `SignInOrganization` (`Fernhill-Labs`) and `SignInToken` (masked) both disabled (open point 6); `Sign in` disabled, `Cancel` enabled; no `SignInError`; the hold's `Arrived` completed: one connectionData request waits at the fake, not yet recorded (requests since the mark none); `AccountOf(AzureDevOps)` null; calls since the mark none; no `accounts.json`; no text cut; no token on screen. |  | n/a | `a07a-azure-checking` | n/a: a held request (0.5). Cancel during the check is 7a's A3, the same per provider (10.4), and `SignInDialogProvidersTests`'. The release lets the check go on to A4's sign-in. |
| A8 | Refused by a 401 (design 10.2, 10.4; D373; the fakes as WP-LF left them): GitLab's dialog with a `glpat-` token GitLab never issued shows GitLab's message; Azure DevOps' with a PAT-shaped value it never issued shows the fixed text naming the organization; the token box kept and selected; nothing kept; GitLab's legacy token then signs in; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 4**, after A11's step 6: 7. With the focus still in `SignInToken`, press Ctrl+A, then `app.Type(PartA.UnknownGitLabToken)`; press Enter; wait for the check to end. Screenshot (`a08a-gitlab-refused`). 8. Mark. With the box's whole text selected by the refusal, `app.Type(GitLabLegacyToken)` (the selection replaced); press Enter; wait until the dialog has closed.<br>**Method 5**, after A11's step 6: 7. Replace `SignInToken`'s text with `PartA.UnknownAzureDevOpsToken`; press Enter; wait for the check to end. Screenshot (`a08b-azure-refused`). | **Method 4, after step 7:** requests exactly one, GitLab's `GET /api/v4/user` (route `user`), `Bearer`, 401, no `Login`; `SignInError` `GitLab refused the token: 401 Unauthorized.` (`dialogError`, `VcDangerBrush` `#E5606B`); the box enabled, its text kept and all of it selected, the keyboard focus in it; `Sign in` and `Cancel` enabled; `AccountOf(GitLab)` null; no `accounts.json`; calls still `probe` only; no text cut; no token on screen.<br>**Method 4, after step 8:** requests since the mark exactly one, `GET /api/v4/user`, `Bearer`, 200, `Login` `ada` (the legacy kind is taken as the fine-grained one, D345); calls `save gitlab:127.0.0.1:47398:ada`; the dialog closed; `AccountOf(GitLab)` `SignedIn`, `Keychain`, the scenario facts' account; GitLab's block as A2's step 4 shows it, the focus on its "Sign out…"; `accounts.json` one record, GitLab's eight fields, `tokenState` `keychain`.<br>**Method 5, after step 7:** requests exactly one, Azure DevOps' `GET /Fernhill-Labs/_apis/connectionData` (route `connection:fernhill-labs`), `BasicEmptyUser`, 401 (the empty body), no `Login`; no `areas` request; `SignInError` `Azure DevOps refused the token for Fernhill-Labs. Check that it was made for this organization and has not expired.` (`dialogError`); `SignInOrganization` still `Fernhill-Labs`, enabled; `SignInToken` enabled, its text kept and all of it selected, the keyboard focus in it; `Sign in` and `Cancel` enabled; `AccountOf(AzureDevOps)` null; no `accounts.json`; calls still `probe` only; no text cut; no token on screen. |  | n/a | `a08a-gitlab-refused`, `a08b-azure-refused` | n/a: refusals, scripted only; the real window types only the scenario's tokens. GitLab's 401 goes beyond 10.7's list, which names Azure DevOps' (open point 1). Azure DevOps' 401 bodies are empty, HTML or a GUID's, so its text is fixed (10.2, g1 G0.2). Varies by nature: the box's caret. |
| A9 | Refused by the Anonymous 200 (D360; design 10.2, 6.4; D373; 16.1): a value in none of the PAT forms is answered as a visitor, which the dialog names apart from a refused token; one request, no second read; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 5**, after A8: 8. Mark the requests. With `SignInToken`'s whole text selected by A8's refusal, `app.Type(PartA.PlainValue)` (the selection replaced); press Enter; wait for the check to end. Screenshot (`a09a-azure-anonymous`). | **After step 8:** requests since step 8's mark exactly one, `GET /Fernhill-Labs/_apis/connectionData` (route `connection:fernhill-labs`), `BasicEmptyUser`, 200 (the Anonymous identity), no `Login`; no `areas` request (A1 stops at the Anonymous identity, 6.4 step 2); `SignInError` `Azure DevOps did not take the token as a sign-in and answered as it does for a visitor. Paste the whole token, made for Fernhill-Labs.` (`dialogError`); `SignInOrganization` `Fernhill-Labs`; `SignInToken` enabled, its text kept and all of it selected, the focus in it; `Sign in` and `Cancel` enabled; `AccountOf(AzureDevOps)` null; no `accounts.json`; calls still `probe` only; no text cut; no token on screen. |  | n/a | `a09a-azure-anonymous` | n/a: a refusal. **Rests on an ASSUMED answer** (open point 8): the fake answers such a value with the Anonymous 200 in Fernhill-Labs, a private organization, as stage 1 measured it in the sandbox's organization, whose privacy the run did not read; `Check_1b`'s new probe settles it (16.1). The probe runs with stage 2, after WP-N's merge (status step 16), so A9's expected result and `a09a` stay ASSUMED through WP-A's merge unless the lead runs it first; on a 200, A9 changes as open point 8 says. Varies by nature: the box's caret. |
| A10 | Refused for an unknown organization (D360, D374; design 10.2, 10.4, 6.4): a 404 from connectionData named as no such organization, the organization box's text selected; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 5**, after A9: 9. Mark the requests. Replace `SignInOrganization`'s text with `PartA.UnknownOrganization`; replace `SignInToken`'s with `AzureDevOpsToken`; press Enter; wait for the check to end. Screenshot (`a10a-azure-no-organization`). | **After step 9:** requests since step 9's mark exactly one, `GET /Fernhill-Lab/_apis/connectionData` (route `connection:fernhill-lab`), `BasicEmptyUser`, 404, no `Login`; no `areas` request; no request to another organization's path; `SignInError` `Azure DevOps has no organization named Fernhill-Lab.` (`dialogError`); `SignInOrganization` enabled, holding `Fernhill-Lab`, all of it selected, with the keyboard focus (`NotFound` selects the organization box's text, 10.4); `SignInToken` kept, masked; `SignInTokenLink` enabled (`Fernhill-Lab` is in the grammar); `Sign in` and `Cancel` enabled; `AccountOf(AzureDevOps)` null; no `accounts.json`; calls still `probe` only; no text cut; no token on screen. |  | n/a | `a10a-azure-no-organization` | n/a: a refusal. The 404 with a token is MEASURED by D374's read of a made-up organization (16.1). Varies by nature: the box's caret. |
| A11 | Refused locally (D378, D298; design 10.2, 10.4, 1.2): a token of another provider's form in each provider's dialog, with no request to any fake; an organization outside the grammar in Azure DevOps' dialog; "hosting7b", one empty tab, the three fakes, dark, 1100×700 | **Method 4**, after A5: 6. Replace `SignInToken`'s text with `PartA.AzureDevOpsForm`; press Enter. Screenshot (`a11a-gitlab-azure-token`). (A8's steps 7 and 8 follow.) 9. Mark the requests. `OpenSignInDialogAsync(GitHub)`; `TypeToken(GitLabToken)`; press Enter. 10. Press Esc.<br>**Method 5**, after A6's step 4: 5. Replace `SignInOrganization`'s text with `PartA.NotAnOrganization`; click the dialog's `Sign in`. Screenshot (`a11b-azure-not-an-organization`). 6. Replace `SignInOrganization`'s text with `PartA.Organization`; replace `SignInToken`'s with `GitHubToken`; press Enter. Screenshot (`a11c-azure-github-token`). | **Method 4, after step 6:** `SignInError` `This looks like an Azure DevOps token. Paste a token made on GitLab.` (`dialogError`); requests none in any fake; the box enabled, holding what was typed, the focus still in it (as D298's refusal, 7a's A4 step 4); `Sign in` enabled; `AccountOf(GitLab)` null; calls still `probe` only; no text cut; no token on screen.<br>**Method 4, after step 9:** GitHub's dialog, 7a's; `SignInError` `This looks like a GitLab token. Paste a token made on GitHub.`; requests none since the mark in any fake (GitHub's fake got nothing); `AccountOf(GitHub)` null.<br>**Method 4, after step 10:** the dialog closed, the screen open, GitHub's block signed out, GitLab's signed in as after A8's step 8.<br>**Method 5, after step 5:** `SignInError` `Type the organization's name, of letters, digits and hyphens, or its address on dev.azure.com or visualstudio.com.` (`dialogError`); requests none; `SignInOrganization` holding `Fernhill Labs`; `SignInTokenLink` disabled (no organization: `OrganizationOf` gives none), in the disabled look: the theme's disabled foreground `VcTextDisabledBrush` `#5F6775` in place of the accent and the arrow cursor, and after the capture, with the pointer moved onto it, no underline; the token kept, masked; `Sign in` enabled (both boxes hold text); which box has the focus is not asserted (open point 5); calls still `probe` only; no text cut.<br>**Method 5, after step 6:** `SignInError` `This looks like a GitHub token. Paste a token made for Fernhill-Labs on Azure DevOps.`; requests none in any fake; `SignInOrganization` `Fernhill-Labs`, the link enabled; `SignInToken` holding what was typed, masked, the focus still in it; `Sign in` enabled; `AccountOf(AzureDevOps)` null; calls still `probe` only; no text cut; no token on screen. |  | n/a | `a11a-gitlab-azure-token`, `a11b-azure-not-an-organization`, `a11c-azure-github-token` | n/a: local refusals, scripted only. The organization's refusal comes before D298's and D378's (10.4's order). Each dialog given each other provider's form is `SignInDialogProvidersTests`' (10.8); GitHub's step is asserted, not pictured. Varies by nature: the boxes' caret. |
| A12 | Session only where no keychain can be reached, and its restart (D253, D287; design 10.4, 10.5, 9.4): the warning in GitLab's and Azure DevOps' dialogs before anything is typed; both accounts kept in memory, never saved; the blocks' note; `tokenState` `session`; at the next start both need sign-in, with no keychain call, and Azure DevOps' dialog is prefilled with the held account's organization; "hosting7b", one empty tab per start, the three fakes, the store unavailable, dark, 1100×700 | **Method 7** (`Check_A12_session_only_and_its_restart`, a `[Fact]`), first start: 1. `ServeAsync()`; a `FakeTokenStore` with `Available = false`. `StartEmptyAsync(data, served, tokens: store)`; `OpenAccountsAsync()`. Mark the requests and the calls. 2. `OpenSignInDialogAsync(GitLab)`. Screenshot (`a12a-gitlab-no-keychain-dialog`). 3. `TypeToken(GitLabToken)`; press Enter; wait until the dialog has closed. 4. Mark the requests. `OpenSignInDialogAsync(AzureDevOps)`; `TypeOrganization(PartA.Organization)`; click `SignInToken`; `TypeToken(AzureDevOpsToken)`; press Enter; wait until the dialog has closed. Turn the wheel up over `SettingsSection` until the offset stops changing. Screenshot (`a12b-gitlab-azure-session-only`). 5. Close the app.<br>Second start, the same data folder, store and fakes: 6. Mark the requests and the calls. `StartEmptyAsync(data, served, tokens: store)`; `OpenAccountsAsync()`. Screenshot (`a12c-gitlab-azure-session-ended`). 7. Click `AccountsSignInButtonAzureDevOps`; wait for the dialog. Press Esc. Close the app. | **After step 2:** GitLab's dialog as A5's step 2, and below the token box, before anything is typed, `SignInNoKeychain` `No keychain was found on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again at the next start.` (`dialogWarning`, `VcWarningBrush` `#E0A23B`); calls `probe`; requests none.<br>**After step 3:** requests exactly one, GitLab's `GET /api/v4/user`, 200, `Login` `ada`; the dialog closed (no failed save, no "Close"-only state); `AccountOf(GitLab)` `SignedIn`, place `Session`, note kind `NoKeychain`.<br>**After step 4:** Azure DevOps' dialog showed `SignInNoKeychain` too, with the focus in the empty `SignInOrganization`; requests since step 4's mark exactly two, Azure DevOps' `connection:fernhill-labs` 200 and `areas` 200; `AccountOf(AzureDevOps)` `SignedIn`, `Session`, `NoKeychain`. Before the capture the offset is 0, asserted. The section from its top (open point 2: part A's tightest state, about 502 of 495 as WP-A's layout measures it): it scrolls at offset 0, its extent above its viewport, and the bottom of `AccountUseForGitNoteAzureDevOps` lies below the viewport by less than one of its lines (they are about 14 apart: 13.9, `PartA.A12NoteLine`, as the note's layout gives them, ±0.5), the last line's top inside the viewport, scrolled out of view, not cut; GitHub's block signed out; GitLab's `AccountLoginGitLab` `ada` and `AccountTokenPlaceGitLab` with D253's text (`settingsNote`), `Sign out…`, the `Git` row checked with GitLab's note; Azure DevOps' `AccountLoginAzureDevOps` `Fernhill-Labs`, `AccountUserAzureDevOps` `Ada Lovelace`, `AccountTokenPlaceAzureDevOps` with D253's text, `Sign out…` with the focus, the `Git` row checked with its note. `accounts.json`: two records, GitLab's and Azure DevOps', `tokenState` `session`, `useForGit` true. Calls since step 1 exactly `probe`: no save (D253); `Holds` neither key. No text cut; no token on screen.<br>**After step 6:** the start's calls none (a `session` record reads no store, D287); requests none (no check at a start); `AccountOf(GitLab)` and `AccountOf(AzureDevOps)` `NeedsSignIn`, note kind `SessionEnded`; `AccountOf(GitHub)` null. The section, fitting: GitHub's block signed out; GitLab's `ada`, `AccountNeedsSignInGitLab` `The token was kept for the last session only. Sign in again.` (`dialogWarning`), `Sign in…` and `Sign out…` enabled, the `Git` row checked; Azure DevOps' `Fernhill-Labs`, `Ada Lovelace`, `AccountNeedsSignInAzureDevOps` with the same text, both buttons enabled, the `Git` row checked. `accounts.json` byte for byte as after step 4 (a start writes nothing). No text cut.<br>**After step 7:** the dialog `Add Azure DevOps account` stacked over the screen: `SignInOrganization` holds `Fernhill-Labs` (the held account's login; the tab is empty, 10.5), the keyboard focus in `SignInToken`, `SignInNoKeychain` shown, the link enabled, `Sign in` disabled; calls `probe`; requests none. After Esc the dialog closed and the screen still open on Accounts, both accounts still needing sign-in. |  | n/a | `a12a-gitlab-no-keychain-dialog`, `a12b-gitlab-azure-session-only`, `a12c-gitlab-azure-session-ended` | n/a: D253's path needs `FakeTokenStore.Available` (a hook, 0.5), and the real window neither restarts nor has an unavailable store (14.1). Each start is a `FreshApplication.RunAsync` half (D32). The held account's prefill is asserted, not pictured; `a06c` pictures the tab's. Varies by nature: the box's caret (`a12a`); the status bar's git box. |
| A13 | One sign-out leaves the other blocks and records (D348, D291, D296; design 10.2, 10.3, 9.4): GitLab's question names its login and merge requests; after it GitLab's block is signed out and its record and key gone, GitHub's and Azure DevOps' untouched; Azure DevOps' question names the organization; "hosting7b", one empty tab, the three signed in, dark, 1100×700 | **Method 8** (`Check_A13_one_sign_out_keeps_the_others`): 1. `ServeAsync()`. `StartEmptyAsync(data, served)`; `SignInAsync(GitHub, GitHubToken)`; `SignInAsync(GitLab, GitLabToken)`; `SignInAsync(AzureDevOps, AzureDevOpsToken, PartA.Organization)`. `OpenAccountsAsync()`. Mark the requests and the calls; read `accounts.json`. 2. Click `AccountsSignOutButtonGitLab`; wait for the confirmation. Screenshot (`a13a-gitlab-sign-out-question`). 3. Click the confirmation's `Sign out`; wait until GitLab's sign-out has ended (`WaitForAccountAsync(GitLab, a => a is null)`). Screenshot (`a13b-gitlab-signed-out`). 4. Click `AccountsSignOutButtonAzureDevOps`; wait for the confirmation. Screenshot (`a13c-azure-sign-out-question`). 5. Click `Cancel`. | **After step 1:** the three `SignedIn`, `Keychain`; the section scrolls (three signed in), at offset 0, GitLab's "Sign out…" in view; `accounts.json` three records.<br>**After step 2:** `ConfirmationDialog` stacked over the screen: `ConfirmTitle` `Sign out of GitLab?`; `ConfirmQuestion` `Sign out of GitLab as ada? VisualCommit removes its token from this computer and no longer lists merge requests. Git over HTTPS asks your own credential helpers again.`; no `ConfirmDetail`; `Cancel` and `Sign out` (`ConfirmButton`) in the primary look (`VcPrimaryButton`), not the danger one (D296); no "In …" line. Still signed in; calls none; requests none; no text cut.<br>**After step 3:** calls since the mark exactly `delete gitlab:127.0.0.1:47398:ada`; `Holds(gitlab:…:ada)` false, `Holds` GitHub's and Azure DevOps' keys true; requests none. `AccountOf(GitLab)` null; `AccountOf(GitHub)` and `AccountOf(AzureDevOps)` `SignedIn` as before. `accounts.json`: `version` 1 and two records, GitHub's and Azure DevOps', each with the fields step 1 read (the record goes, the others are kept, 9.3). The section, fitting (open point 2): GitHub's block signed in as before; GitLab's `AccountsSignedOutGitLab` `Not signed in.` and `AccountsSignInButtonGitLab` with the keyboard focus, no `AccountLoginGitLab`, `Sign out…`, `AccountsBusyGitLab` or `Git` row; Azure DevOps' block signed in as before. No text cut.<br>**After step 4:** `ConfirmTitle` `Sign out of Azure DevOps?`; `ConfirmQuestion` `Sign out of Azure DevOps for Fernhill-Labs? VisualCommit removes its token from this computer and no longer lists pull requests. Git over HTTPS asks your own credential helpers again.`; no `ConfirmDetail`; `Cancel` and `Sign out` in the primary look; still signed in; calls none since step 3.<br>**After step 5:** the question closed; the section as after step 3; `accounts.json` and the store unchanged; calls none. |  | n/a | `a13a-gitlab-sign-out-question`, `a13b-gitlab-signed-out`, `a13c-azure-sign-out-question` | n/a: the real window never signs out (14.4). "Signing out…" while the keychain works, and a delete the keychain refuses, are 7a's A8, the same per provider. GitLab's sign-out deletes no `avatars/` (9.4) and GitHub's is 7a's: `HostingSessionProvidersTests`' (9.10), since an empty tab gathers no avatars. Varies by nature: the status bar's git box. |
| A14 | Each switch saved alone (D216, D255, D350; design 10.3, 9.4, 9.6): GitLab's switch off and Save writes GitLab's record alone and drops web-app's GitLab binding, keeping GitHub's; then Azure DevOps' switch off and Save writes its record alone; the reopened screen shows each switch as saved; "hosting7b", tabs `web-app` and an empty one, the three signed in, dark, 1100×700 | **Method 9** (`Check_A14_each_switch_saved_alone`): 1. `ServeAsync()`. `StartWithTabs(data, [served.WebApp.Path, null], activeTab: 0, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab)`; `WaitForRepositoryAsync()`; `SignInAsync` to GitHub, GitLab and Azure DevOps (`PartA.Organization`); wait for the log line `web-app: listed 3 open pull requests of northfield/platform/web-app` and for the watchers to be quiet. Mark the calls; read `accounts.json`. 2. Click tab 1 (the empty tab). `OpenAccountsAsync()`; click `AccountUseForGitGitLab`; click `Save`; wait until the screen has closed, then until the parsed `accounts.json` shows GitLab's `useForGit` false and `web-app`'s hosting's `Bindings` hold no binding of GitLab's account. 3. `OpenAccountsAsync()`. Screenshot (`a14a-gitlab-switch-off`). 4. Turn the wheel down over `SettingsSection` until the offset stops changing; click `AccountUseForGitAzureDevOps`; click `Save`; wait until the screen has closed, then until the parsed `accounts.json` shows Azure DevOps' `useForGit` false. 5. `OpenAccountsAsync()`; turn the wheel down until the offset stops changing; assert the end. Screenshot (`a14b-azure-switch-off-end`). Press Esc. | **After step 1:** `web-app`'s hosting's `Bindings` hold a binding of `gitlab:127.0.0.1:47398:ada` for `http://127.0.0.1:47398/northfield/platform/web-app.git` and one of `github:127.0.0.1:47399:octo` for `http://127.0.0.1:47399/octo/web-app.git` (twins WP-C's, not counted), none of Azure DevOps' account; the session's `BindsGit` true; `accounts.json` three records, `useForGit` true.<br>**After step 2:** before Save, `AccountUseForGitGitLab` unchecked and nothing changed (D216: `accounts.json` as step 1 read it, the bindings as step 1's); after Save, the screen closed; `accounts.json` (waited for: the Save writes it off the UI thread after the screen has closed) GitLab's record `useForGit` false, its other fields unchanged; GitHub's and Azure DevOps' records exactly as step 1 read them; `AccountOf(GitLab)` still `SignedIn` with `UseForGit` false; `BindsGit` true; `web-app`'s `Bindings` (waited for) none of GitLab's account, GitHub's binding as step 1's; calls none (a switch touches no keychain).<br>**After step 3:** the section at offset 0: GitHub's block signed in, `AccountUseForGit` checked; GitLab's `ada`, the keychain's text, `Sign out…`, `AccountUseForGitGitLab` unchecked and enabled, `AccountUseForGitNoteGitLab` with its text; Azure DevOps' block from its label, its `Git` row not wholly inside the viewport (its top lies about at the viewport's bottom, open point 2; nothing more is asserted of its place); the welcome page of the empty tab behind the backdrop, the tab strip `web-app`, `New tab` (active); no text cut.<br>**After step 4:** after Save, `accounts.json` (waited for) Azure DevOps' record `useForGit` false, its other fields unchanged; GitLab's still `useForGit` false; GitHub's as step 1 read it; `AccountOf(AzureDevOps)` `UseForGit` false; `BindsGit` true (GitHub's switch is on); `web-app`'s `Bindings` as after step 2 (Azure DevOps' account covers none of its remotes); calls none.<br>**After step 5:** before the capture the offset is the end, above 0; in view GitHub's `AccountUseForGit` checked, `AccountUseForGitGitLab` unchecked, Azure DevOps' block whole with `AccountUseForGitAzureDevOps` unchecked and `AccountUseForGitNoteAzureDevOps` with its text; no text cut. After Esc the screen closed with nothing changed. |  | n/a | `a14a-gitlab-switch-off`, `a14b-azure-switch-off-end` | n/a: a setting at Save; the real window shows the switches on in `a02d`. The pictures are taken on the empty tab, so that they show no region of WP-P's, as 7a's A11. Another provider's event leaving a toggled switch alone is `AccountsProvidersViewModelTests`' (10.8). Varies by nature: the status bar's git box. |

## What proves each Delivers item

`plan.md`'s phase 7b "Delivers", against part A; parts B, P and N add their columns and rows when their
checks are written, as phase 7a's report did. "Elsewhere" names the work packages' own classes (design
0.1).

| Delivers item | Part A | Elsewhere |
|---|---|---|
| Azure DevOps and GitLab on 7a's provider model (D347): sign-in with a personal access token (D345), one account per provider (D348), tokens in the OS keychain, else the session (R3, D253); status step 10, "Sign-in dialog and the Accounts section with an account of each provider" | A1 to A14 (the keychain through `FakeTokenStore`): the three blocks (A1, A3), a sign-in per provider through its block and dialog, one account each (A2, A4, A6), each dialog's states and refusals (A5 to A11), session only and its restart (A12), one sign-out per provider (A13), one switch per account (A14); the real window repeats A2's three accounts, A5's GitLab dialog and A6's Azure DevOps dialog | `SignInDialogProvidersTests`, `AccountsProvidersViewModelTests`, `SignInEntryProvidersTests` (10.8); `HostingSessionProvidersTests` (9.10: one A1 per provider, a second GitLab user forgets the first's entry); `HostingProvidersTests` (`TokenFormOf`, D378) and `TokenEntryNamesTests` (1.10); the real stores with an `azure-devops` key: CI's real-store steps (1.10, D270); the live `Check_2` per provider (stage 1, "Live command") and `Check_3` (stage 2) |
| The account's token on the app's git calls to the remotes it covers, for Azure DevOps its own organization's (D350) | A14: each block's "Use for git over HTTPS" saved alone, GitLab's dropping web-app's GitLab binding and keeping GitHub's; the notes naming GitLab and Fernhill-Labs's repositories on Azure DevOps (A2, A4) | WP-C's classes (8.7), `HostingSessionCredentialTests` (9.10); parts B, P and N |
| 7a's screens and commands that name GitHub name the provider of the account or remote they act on (D351); status step 14 for part A's surfaces | A1 to A3: the blocks' labels and the one intro (D353); A5, A6: `Add GitLab account` and `Add Azure DevOps account` with their help, links and boxes; A8 to A11: each failure in its provider's words (D362); A12: the notes per block; A13: `Sign out of GitLab?` and `Sign out of Azure DevOps?` with their questions; GitHub's block and dialog in 7a's words (A2, A11's GitHub step), and 7a's part A pictures changed in the intro alone ("Changes to expected results") | `HostingTextsProviderTests` (1.10); `AccountsProvidersViewModelTests` (10.2's texts per state); 7a's part A classes, run by the builder (10.6, 10.8); parts B, P and N |
| A local fake of each provider's API for the default run and CI (D346) | A2, A4 to A10 sign in and are refused against the fakes as stage 1 measured them and WP-LF copied them: GitLab's 401, Azure DevOps' member, empty 401, Anonymous 200 (ASSUMED in a private organization, open point 8) and unknown organization's 404 | `FakeAzureDevOpsTests`, `FakeAzureDevOpsGitTests`, `FakeGitLabTests`, `FakeGitLabGitTests` (3.6, 4.6), `Phase7bSupportTests` (2.8) |
| The opt-in live command extended to Azure DevOps and GitLab (D346, D352) | — | "Live command" above; `LiveSafetyTests`, `LiveScreenTests` (15.9) |

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

### Stays as it is

Every other picture of phases 0 to 7a (design 0.7's "must not change"): phase 6's settings pictures,
whose sections are not Accounts and whose section list is 7a's; 7a's parts B, P, G and N, which take no
picture while the Accounts section shows: B signs in through the session or opens GitHub's dialog from
the palette over the window, and P, G and N sign in through the section, as the real window does, and
close it (Esc, then the screen's `IsClosed`) before their first picture (`Phase7aChecks.P.Menus.cs`,
`Phase7aChecks.G.cs`, `Phase7aChecks.N.cs`). Parts B, P and N add their own sections here when their
checks are written.

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

Where the design is silent, leaves a choice to WP-A or rests on an answer still to be measured, with the
reading this report takes. A ruling of the lead's, or WP-A's build, that changes an expected result goes
under "Changes to expected results" with its reason.

1. **Numbering and additions.** Design 10.7's list is unnumbered; A1 to A14 follow its order, with
   "Fernhill-Labs bold over Ada's name" (A4) after "GitLab off" (A3) as the list has it, though its picture
   is taken at the end of method 5. Beyond the list: the all-off start (A3, for D362), GitLab's 401 (A8),
   GitLab's legacy token's sign-in (A8) and GitHub's dialog given GitLab's token (A11).
2. **Scrolling with one or two signed-in blocks.** 10.3 measures only three signed-in blocks (about 553
   of 495). The other heights are estimated from that figure and 7a's pictures (a signed-in block about
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
8. **The Anonymous 200 in a private organization** (16.1's As built). Stage 1 measured a value in none of
   the PAT forms answered with the Anonymous 200 in the sandbox's organization, whose privacy it did not
   read; WP-LF gives the 200 in every organization, Fernhill-Labs (private) included, ASSUMED. `Check_1b`'s
   new probe reads the sandbox organization's connectionData without a credential. 16.1 asks for it
   before part A's checks, but WP-LF added it to the live command for stage 2 (status step 16, after
   WP-N's merge; the status's WP-LF note), so A9's expected result and `a09a` stay ASSUMED through WP-A's
   merge, unless the lead runs the probe before it. A 401 makes A9's answer MEASURED and changes nothing.
   A 200 means the sandbox's organization allows public projects: the fake then keeps the Anonymous 200 to
   organizations with `PublicProjects` (16.1), Fernhill-Labs (private) would answer A9's value as no
   credential, with the sign-in's 401, and the dialog would show A8's text. A9's expected result then
   changes when the probe has run, recorded under "Changed in this report after its first commit" with
   the probe's date, the lead ruling which way: an organization with public projects, which the scenario
   lacks, added for A9 (its organization text and expected texts changed with it); the fault
   `FakeAzureDevOpsFault.AnonymousIdentity` set with `served.AzureDevOps.FailNext("connection:fernhill-labs",
   …)` before step 8 (A9 is scripted only, so a fault is allowed), which keeps A9's text and picture but
   makes the Anonymous answer the fault's; or A9 re-pointed at A8's text, the Anonymous answer's text left
   to `SignInDialogProvidersTests` (10.8).
9. **Picture names for more than one provider.** 0.3 asks a name to name its provider in its words; a
   picture of the section about all three blocks, or all three off, uses `three` (`a01a-three-signed-out`,
   `a02d-three-signed-in-end`, `a03b-three-off`), and one about two providers names both, GitLab's first
   as in D353's order (`a11a-gitlab-azure-token`, `a12b-gitlab-azure-session-only`,
   `a12c-gitlab-azure-session-ended`). The rule's pattern does not test the words, and `Phase7bExpectationTests.A`
   pins only the pattern and uniqueness, so `PartA`'s names are taken from this report as written.
10. **WP-P's region in `a06c`.** The prefilled dialog stands over mobile-app's window, whose left panel
    WP-P changes; nothing there is asserted, and the lead inspects the picture again at WP-P's merge
    (16.2), as 7a's A9.
