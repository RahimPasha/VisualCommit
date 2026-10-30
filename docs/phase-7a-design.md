# Phase 7a design — Hosting foundation and GitHub

This is phase 7a's design: what the phase builds, how it is cut into work packages, the contracts the
packages share, and one brief per package for its builder. Once the phase is built it is kept as
history, as phases 3 to 6's designs are. Its decisions are recorded only in `docs/decisions.md`
(D243): the design names them by number (D265 to D338) and does not repeat them. Binding inputs:
`docs/plan.md`'s "Phase 7a" and "Workflow" sections, D249 to D264, and the lead's 32 rulings of
2026-10-28 (cited as "ruling n"). Line numbers are those of `cea102c`. Git facts were measured on Git
2.56.0.windows.4 in isolated folders; where CI's 2.43 or 2.55 may differ, the section says so (D248).

A builder reads section 0, section 1 and its own section, and nothing else.

## Contents

- 0 Scope, packages and parts
- 1 Shared contracts (WP0's brief)
- 2 WP-F: the fake of GitHub and the harness
- 3 WP-K: keychain stores and the vault
- 4 WP-C: credentials into git, push guard, prompt group
- 5 WP1: the first package (W1, S8, R8, G23's split)
- 6 WP-G: GitHub's API client and provider
- 7 WP-S: the hosting session
- 8 WP-A: accounts and sign-in (part A)
- 9 WP-B: browse and clone (part B)
- 10 WP-P: pull requests in the left panel (part P)
- 11 WP-V: the graph's badge and avatars (part G)
- 12 WP-N: a new pull request (part N)
- 13 WP-R: the real-window pass
- 14 WP-LIVE: the opt-in live command
- 15 Open points
- 16 As built: wave 1
- 17 As built: wave 2

## 0. Scope, packages and parts

### 0.1 Scope against the Delivers list

| Delivers item (`plan.md`) | Where |
|---|---|
| Provider model for 7b | WP0, WP-G, WP-S |
| Token sign-in in Settings' Accounts; keychain, else the session (D249, D253, D256, D263) | WP-K, WP-S, WP-A |
| Real stores of macOS and Linux on CI (D259) | WP-K (CI steps handed to the lead) |
| The account's token on the app's git calls to github.com (D255) | WP-C, WP-S |
| Browse and clone (D261) | WP-B |
| Pull-request list and menu, create, badge, open in browser (D251) | WP-P, WP-N, WP-V |
| Avatars from GitHub only (D252) | WP-G, WP-S, WP-V |
| The opt-in live command (D250, D260) | WP-LIVE |
| From phase 3: "Create pull request" in the branch and drop menus; the section's menu; the token's way into git | WP-P; WP-C (D255) |
| From phase 6: push guard and `pr/*`; prompt group per account | WP-C (rulings 8, 9) |
| From phase 6: the wheel test; the real-window csproj's comment | L1 |
| From the review: W1, S8, R8; G23's split (D254, D264) | WP1 |

Out of scope: GitHub Enterprise Server and GHE.com (D258); more than one account (D263); the device
flow (D249); unauthenticated requests (D257); a pull-request page, column or timer; a "Create pull
request…" for the current branch in the section's menu; a palette entry for Accounts or signing in
(the owner's answer of 2026-10-29, D299);
Security.framework on macOS (phase 8, ruling 20); Azure DevOps and GitLab (7b).

The owner's decisions bind and are not restated: D249 (token only), D250 (fake and live command),
D251 (the section, menu, badge), D252 (avatars from GitHub only), D253 (session only without a
keychain), D254 (W1, S8, R8 in the first package), D255 (the app's helper for github.com), D256
(Accounts in Settings, at once), D257 (no request signed out), D258 (github.com only), D259 (real
stores on CI), D260 (live token in a Windows user variable), D261 ("From GitHub…" only signed in),
D262 (R8 local only), D263 (one account), D264 (G23's split).

### 0.2 Design statements that shape the whole phase

**Credentials (D255, rulings 1, 2 and 4 to 7; ruling 3 withdrawn, D277).** A network operation's
prompt scope carries `CredentialBinding`s (1.2). For each bound URL the scope's environment gets two
`GIT_CONFIG_COUNT` pairs, `credential.<url>.helper` empty, then `credential.<url>.helper` =
`!'<helper>' --vc-credential`, numbered after any pairs the process or the session environment
already holds. Git reads these pairs after every file and before an inherited `GIT_CONFIG_PARAMETERS`
and `-c` (measured on 2.56), so a helper listed there would be asked after the app's and get its
`store` and `erase`: the scope's environment also removes `GIT_CONFIG_PARAMETERS` (D329). The helper is
`visualcommit-askpass` in a new mode; it asks the app over the existing pipe, and the app answers from
the account without UI, in Ask scopes and in Deny scopes that have bindings (only those Deny scopes
are registered and keep the pipe; their prompts stay refused, D274). The answer is
`username=x-access-token` and the token, never the login. No prompt is ever answered from the
account. Nothing is written to any configuration or store; the token never appears in an argument, a
URL, a config value, an environment variable, a log, a picture or a file.

- Which URLs are bound: for every remote, the URLs git uses (fetch URLs after `insteadOf`; push URLs
  after `pushInsteadOf` when the remote has no `pushurl`, after `insteadOf` when it has one), with
  scheme `https`, the GitHub web host, and no user part or the account's login (case-insensitive).
  The binding's URL is git's with `user@` removed (D278). A URL naming another user is not bound
  (ruling 1), and a bound path that another user's URL of the repository shares is dropped (D278).
  Measured on 2.56: `insteadOf` takes the longest matching prefix; `pushInsteadOf` applies to push
  URLs of a remote without `pushurl` and does not rewrite a URL with a user part its prefix lacks;
  `git remote get-url --push --all` shows the result, which WP-C's parity test compares with Core's
  `UrlRewrites`. The rules are read by one `git config --null --get-regexp ^url\.` only while an
  account is signed in and GitHub is not off, so no earlier count of git calls moves: a tab reads
  them with its refs; a dashboard row without a tab reads them at each network operation's start
  while the account binds git, and lists nothing until a tab adopts it (D332). The refs read's
  `^remote\.` form stays (phase 6's operation pictures show it); the `url` and `pushurl` values it
  already returns are kept in `RepoRefs.RemoteUrls`.
- Clone: an `https://github.com/...` URL without another user is bound (browser or typed), after the
  user's `insteadOf` rules as for a tab (D331); the browser fills the plain `clone_url` (ruling 5).
- Bound only while signed in with "Use for git over HTTPS" on, the token in hand, the helper found
  (`AskPassBroker.HelperFound`) and the detected git not older than 2.31. Otherwise D82's order
  holds, untouched.
- On git older than 2.31 (`GIT_CONFIG_COUNT` ignored) the account is not used for git: "Use for git
  over HTTPS" has no effect, git asks the user's helpers and the app's dialog as today (D82's order),
  and the token never reaches git. An askpass answer would be stored by git in every helper of the
  list (measured on 2.56), against D255 and R3, so ruling 3's fallback is not built: the lead withdrew
  it after the review (the owner's answer of 2026-10-29, D277). The Accounts section notes that the
  option needs Git 2.31 or newer (8.2).
- The API's 401 marks the account "needs sign-in"; git's `erase` to the app's helper does so only
  when a `GET /user` with the token then answers 401 too (D330: a stale `http.extraHeader` or a
  redirect makes git erase a valid token). The scope records the erase, the failed operation's result
  carries it, and the error dialog says that GitHub refused git's credentials, not the token, and
  points to Settings, Accounts (ruling 4, D276, 4.7).
- The keychain is read once, at the session's start in the background and at sign-in, never from a
  `get` (ruling 6). A `get` before that read ends waits for it, at most 10 s.
- Tracing (ruling 7, measured on 2.56): `GIT_TRACE_CURL` and `GIT_CURL_VERBOSE` print
  `Authorization: Basic <redacted>` by default; with `GIT_TRACE_REDACT=0` the base64 of
  `x-access-token:<token>` reaches stderr, which the call record keeps. `GIT_TRACE` and
  `GIT_TRACE_PACKET` print neither. So every scope with bindings sets `GIT_TRACE_REDACT=1`.
  Redaction exists since 2.27, so 2.43 and 2.55 behave alike (not run there).

**Push guard (ruling 8).** On a remote recognised as GitHub, a candidate source `refs/pull/<x>/head`
or `/merge` counts only when `<x>` is all digits. Recognition is by the configured URL alone (D279),
needs no account and makes no request. The two-namespace refusal stays.

**Prompt group (ruling 9).** Kept answers are keyed by (account, prompt text); a row that fails with
`AuthenticationFailed` makes the group forget its kept answers.

**Pull requests (D251, D257, rulings 10 to 15).**
- The section: a repository without a GitHub remote keeps today's placeholder row exactly (phase 3's
  `03e-no-menu` stays). With one: a message row for signed out (how to sign in), loading, an error
  (`HostingTexts.Describe`, 1.7: GitHub's words, or the app's for a rate limit, single sign-on, the API
  version, a server error, an unreadable answer and the network, D338), and no open pull requests; else rows `#n title author`, drafts marked. Up to 300
  open pull requests (3 pages of 100), then a row saying more exist.
- The repository listed: the first GitHub remote in the order `origin`, `upstream`, then by ordinal
  name (`HostingRemoteOrder`, 1.5); when `GET /repos/{o}/{r}` says it is a fork, its `parent`.
- Refreshed as D289 says; no timer.
- The menu: "Open in browser", "Check out {head}", "Copy link". A pull request whose head is a branch
  of one of the repository's remotes: that remote branch's existing checkout. Otherwise one
  operation fetches `refs/pull/<n>/head` into `FETCH_HEAD` only and creates a local branch without
  upstream at the head's SHA (`<head ref>`, or `<owner>-<head ref>` when taken), and the existing
  checkout follows as its own operation; the label is "Check out {owner}:{head ref}". Never a
  `refs/pull` fetch line, never `refs/remotes/<r>/pr/<n>` (D305).
- A branch with an open pull request has "Open pull request #n" in place of "Create pull request…"; a
  local branch has it only through an upstream of the same name (D313). "Create pull request…" is
  left out (not disabled) without a GitHub remote (D77, D92).
- The create form has a "Draft" check box; the push (when needed) and the create are one operation of
  the repository's queue; a local branch whose upstream has another name (made from `origin/main`) is
  pushed under its own name, its upstream left as it is (D313); afterwards the list shows it and the
  status bar's message offers to open it; the browser is not opened by itself.
- The badge: compact, inside the branch's label in the graph, after its name; the name is cut, never
  the badge. The left panel's branch row shows `#n` too.

**Accounts (D249, D253, D256, D259, rulings 16 to 21).** Dialog title "Add GitHub account". One
wording for every platform's store: "this computer's keychain". Session only: "No keychain was found
on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again
at the next start." The account record (never the token) is kept in `accounts.json`, so a later start
shows "needs sign-in" with the login. A save that fails after the token checked out keeps it for the
session, with that message, and offers no "Try again". The Accounts section is the last of the
settings screen's sections. Sign in and sign out act at once (D256); the "Use for git over HTTPS"
switch is a setting and applies at Save (D216). Stores are the app's own: advapi32 `Cred*`,
`/usr/bin/security -i` with the secret on stdin, the Secret Service through Tmds.DBus.Protocol 0.94.1
(the version Avalonia 12.1.3 brings; a direct reference pinned to it). macOS's weaker exposure is a
new plan risk. Windows' real store is tested only where CI turns it on.

**Avatars (D252, ruling 22).** In the graph's Author column and the details panel's author line,
only for a repository with a GitHub remote while signed in. Authors are matched by e-mail: the first
page of `GET /repos/{o}/{r}/commits?sha=<default branch>&per_page=100`, then at most 20 lookups
`commits?author=<e-mail>&per_page=1` per repository and app session for e-mails of the loaded
graph's commits that a GitHub remote's branch reaches (`refs/remotes/<remote>/*`), never of commits
that are only local or only on another remote (D311). Images are fetched without the token, kept in
`avatars/` in the data folder by user id, and fetched again after 7 days; the e-mail map expires
likewise. Signing out deletes `avatars/`: the images, `index.json` and the e-mail maps (D291). An
author GitHub does not know looks as today.

**Harness (rulings 23, 24).** The fake extends `AuthRemoteServer`: one host on 127.0.0.1 serving git,
`/api/v3/` and avatars, fixed port 47395 (47396 reserved for a second 7a walk-through project; D265).
The app recognises the fake by exact scheme, host and port. Both harnesses default to GitHub off and
an in-memory keychain.

### 0.3 How 7a is named

`7a` everywhere the docs write it (ruling 25):
- Project `tests/VisualCommit.VisualTests.Phase7a`, namespace `VisualCommit.VisualTests.Phase7a`, classes
  `Phase7aChecks` and `Phase7aChecks<Part>`, `Phase7aExpectations.Phase = "7a"` (a `string`).
- Folders `artifacts/visual/phase-7a/{scripted,real-window,timings}`; report
  `docs/test-reports/phase-7a.md`; list `docs/test-reports/phase-7a-pictures.sha256`.
- `VisualRun.FolderFor(string)` and `Save(this Screenshot, string, string)`; `Screens.RealWindowFolder`,
  `ScriptedFile` and `Save` with `string`; `RealWindowRun(string)` with `Phase` a `string`. Each keeps
  an `int` overload that delegates with `ToString(CultureInfo.InvariantCulture)`, so phases 0 to 6
  change nothing. `VisualRun.PhasesStarted` becomes `HashSet<string>`.
- `scripts/time-scripted-check.sh`: a `case "$phase"` over `7a`, `6`, `5`, `4`, `3`, `0|1|2`, and
  `*) echo "unknown phase: $phase" >&2; exit 2`. Its default becomes `7a` in the commit that creates
  the 7a project, not before.
- Real window: `Phase7aRealWindowPass[.<Part>].cs`, `RealApp.Phase7a.cs`; the csproj links
  `..\VisualCommit.VisualTests.Phase7a*\Phase7aExpectations*.cs`.
- Part letters avoid the requirement prefixes C, O, Q, R and T, as phase 6's do (D230): the create
  form's part is **N** (new pull request). Picture names match `^[abpgn]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`.

### 0.4 Work packages

Built in git worktrees on branches `wip/p7a-<name>` made from `phase/7a-hosting-github`, merged back
with `--no-ff`; at most three built worktrees at once (D245's load). One owner per file and per test
class; a file passes between packages only at a merge, in the order below. Builders run the test
classes they touch; whole-project and full runs are the lead's, through `scripts/heavy-run.sh`.

- **L1, the lead's first code commit.** The real-window csproj's comment as ruling 30 says: 27 min
  39 s for phases 0 to 6, and later gates' times in CLAUDE.md; `ShellDriverTests`' wheel test waits
  through a new `ShellDriver.WaitForControlAtAsync(Point, TimeSpan?)` in `ShellDriver.Pointer.cs` at
  the default wait (ruling 31). No drafter: this paragraph is its brief.
- **WP0, the lead's contracts and stubs.** Section 1's list (1.10), in the main checkout, with no
  change of behaviour or pictures.
- **WP-F** (section 2), **WP-K** (3), **WP-C** (4), **WP1** (5), **WP-G** (6), **WP-S** (7), **WP-A**
  (8, part A), **WP-B** (9, part B), **WP-P** (10, part P), **WP-V** (11, part G), **WP-N** (12,
  part N), **WP-R** (13), **WP-LIVE** (14).

**Waves.**
1. L1, then WP0 (lead, main checkout).
2. Wave 1: WP-F, WP-K and WP-C. WP1 after WP-C's merge (`OperationsHarness.cs` passes from WP-C to
   WP1 there); WP-G after WP-F's merge (it tests against `FakeGitHub`'s handler).
3. Wave 2: WP-S after WP-F, WP-K, WP-C, WP1 and WP-G; at its merge the lead puts `HostingSession` in
   `AppSession.Start` in place of `NullHostingSession`. Then, one after another, each after its
   part's checks are written and committed (D244): WP-A; WP-B; WP-P (`AppCommands.Palette.cs` passes
   from WP-B); WP-V (part G's whole-window pictures show WP-P's section and the branch rows' `#n`);
   WP-N (part N clicks WP-P's items, and its pictures after a create show WP-V's badge). Parts A and B
   come earlier, so a picture of theirs that shows an opened GitHub repository would change at WP-P's
   and WP-V's merges: part B takes none (9.7); part A's check 9 names the region and is inspected again
   at those merges (8.7, 10.8, 11.6).
4. Wave 3: WP-R on the Windows machine after every part; WP-LIVE after WP-B and WP-N.

**The lagged list** (R8; D262, ruling 29). At WP1's merge and at each later merge whose section names
it: WP1 adds the lag (5.2) and `GitCallBudgetTests`, and before it the list would run unlagged and
prove nothing. In Git Bash, one at a time:

```
export VISUALCOMMIT_WATCHER_LAG_MS=1500
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests --filter-class "*Phase2Checks"
bash scripts/heavy-run.sh --project tests/VisualCommit.App.Tests --filter-class "*StageListRefreshTests" --filter-class "*RepositoryOperationsProtectTests" --filter-class "*CompareViewTests" --filter-class "*SearchBarTests" --filter-class "*UndoStepTests" --filter-class "*RepositoryOperationsFetchTests" --filter-class "*GitCallBudgetTests" --filter-class "*GitRepositoryProviderTests"
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase6 --filter-method "*Phase6ChecksS.*" --filter-method "*Check_K10_*" --filter-method "*Check_K13_*" --filter-method "*Check_W1_*" --filter-method "*Check_W8_*"
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase7a     # once it exists
bash scripts/heavy-run.sh --project tests/VisualCommit.Hosting.Tests --filter-class "*RepositoryHostingWiringTests" --filter-class "*SignInEntryTests" --filter-class "*WelcomeCloneFromGitHubTests" --filter-class "*PaletteCloneFromGitHubTests" --filter-class "*PullRequestOperationsTests" --filter-class "*CreatePullRequestOperationTests"     # D334's six, once the first exists
unset VISUALCOMMIT_WATCHER_LAG_MS
```

xunit v3 ANDs filters of different kinds, so each command uses one kind. A failure under the lag is
the fault class R8 imitates: it gets a fix, never a looser test.

**Progress steps 4 to 16** (`docs/status.md`):

| Step | Packages |
|---|---|
| 4 First package | L1, WP1 |
| 5 Harness | WP-F (and WP0's safety nets) |
| 6 Provider model | WP0, WP-G (recognition), WP-S (accounts, hosts) |
| 7 Tokens in the keychain | WP-K |
| 8 GitHub's API | WP-G |
| 9 The token reaching git, prompt group, push guard | WP-C, WP-S (bindings) |
| 10 Sign-in dialog and accounts | WP-A |
| 11 Browse and clone | WP-B |
| 12 Section, menu, badge, open in browser | WP-P, WP-V (the graph's badge) |
| 13 Create pull request | WP-P (menu items), WP-N |
| 14 Avatars | WP-G, WP-S, WP-V |
| 15 Each part's checks | the writers before WP-A, WP-B, WP-P, WP-V, WP-N (no package) |
| 16 The live command | WP-LIVE |

WP-R serves step 18 (the gate's real-window pass and D246's two runs before it).

### 0.5 The visual checks' parts

Written per part just before its package, by one writer and one reviewer (D244), each check marked
for the real-window pass or not. One project, `VisualCommit.VisualTests.Phase7a`, about 27 methods
(under D230's 40). Theme: dark unless a check names light; 1100×700 unless it names 1920×1080. Every
part uses WP-F's one scenario, "hosting" (2.6), with its names and numbers.

| Part | Package | What its checks cover | Real window repeats |
|---|---|---|---|
| A Accounts | WP-A | Settings' Accounts section signed out; the "Add GitHub account" dialog empty, checking, refused, session-only (D253); signed in with the keychain and for the session only; sign out with its confirmation; "needs sign-in" after a refused token and after a start whose token is gone; still signed in after a restart (scripted only: the fake store is shared by two starts); the "Use for git over HTTPS" switch | The section signed out; the dialog with a masked token; the signed-in row |
| B Browse and clone | WP-B | "From GitHub…" while signed in; the browser loading, listed, filtered, failed; a clone through the app's helper with no credentials dialog, the clone's `.git/config` with the plain URL and no credential key; the palette's "Clone from GitHub…" signed out signs in first | The browser's list; a clone completing in the real process (the helper through Git for Windows' `sh`) |
| P Pull requests | WP-P | The section's states (not GitHub unchanged, signed out, loading, error, none, rows with a draft, more than 300); filter; a click selects the head commit; a branch row's `#n`; the section's and a pull request's menus; check out of a same-repository and a fork's pull request (git state checked); copy link; open in browser (the fake launcher's record); "Open pull request #n" in a branch menu | The section with rows and its menu; open in browser in the real process, the default browser opened once (D321) |
| N New pull request | WP-N | "Create pull request…" from `ForLocalBranch`, `ForRemoteBranch` and `ForDrop`; the form; a branch not on GitHub pushed first; draft; GitHub's 422 in the form; the created pull request in the list and the status bar's offer | The form; the status after creating |
| G Graph | WP-V | The badge on a local and a remote branch's label, a long name cut with the badge kept, its tooltip; avatars in the Author column and the details panel; an unknown author as today | The graph with a badge and avatars, and the details panel |

For every R step: its picture's name is a constant in `Phase7aExpectations.<part>.cs`, with the texts,
rows and menus the step asserts; its state is built only by code both projects compile
(`VisualCommit.Testing` or the linked expectations, a part's own state by its `PartX.PrepareAsync`,
2.6), never by `FakeFileLauncher`, `FakeClipboard`,
`FakeTokenStore` hooks or a held request; it is taken signed in with the scenario's `OctoToken`.
Part A's R pictures are on one empty tab; part B's listed picture in the state the palette's path
reaches; P, N and G's after the two Debug lines of 1.9 (D322).

What the scripted run cannot prove and the real window or CI does: the real process's start with the
fake's URL; the helper run by Git for Windows' `sh` from a folder with a space (the laptop's
`Visual Git`); the native window and scaling; the browser opening; the real keychains (CI, D259, not
the pass). A restart that keeps the sign-in stays scripted: the real window's store is memory.

### 0.6 Cost in time

- **Default run** (58 min 13 s at the baseline, with G23 stopped): 11 test projects instead of 9,
  with `VisualCommit.VisualTests.Phase7a` and `VisualCommit.Hosting.Tests`. The 7a project: about 27
  check methods with light git work and about 20 classes of expectation, view-model, menu and
  headless view tests, whose `[AvaloniaFact]`s run one at a time on the project's one headless UI
  thread: about 10 minutes alone, 18 to 25 beside the others. `VisualCommit.Hosting.Tests`: its unit classes about 1
  minute; the six classes that start the app through `ShellDriver` with real git (named in 0.4's
  lagged list), about 40 cases with clones, checkouts and pushes, about 4 to 6 minutes on its own
  headless UI thread (D334). New
  classes in `VisualCommit.App.Tests` and `VisualCommit.Git.Tests`, about 3 to 4 minutes of their
  processes; G23's split, two more app starts. About 18 to 22 minutes of work alone, and the run is
  bound by the processors: **+8 to +14 minutes, about 66 to 72**. `dotnet test` runs at most
  `Environment.ProcessorCount` modules at once by default (its documented
  `--max-parallel-test-modules`; not measured here), 8 on the laptop, so 3 of the 11 start only when
  others end; the full runs at the waves' merges (D245) record each project's end.
  `VisualCommit.App.Tests` already ends last (58 min 12 s): hosting view-model tests that need no
  window go to `VisualCommit.Hosting.Tests` or the 7a project, not App.Tests. Scenarios are shared:
  one template per process, several checks per app start where the steps allow (as phase 6's W).
- **CI.** Windows' Test step (32 min 40 s on the last run) and macOS's (24 min 53 s, 43 once) grow by
  about a sixth to a quarter, Windows' to about 37 to 41 minutes; the containers' (5 min 22 s and 5 min
  26 s of their 14) by about as much. WP-F's and WP-S's merges and the gate read every job's Test step
  against four fifths of its limit (44 of 55 minutes; 11 min 12 s of the containers' 14). One past it
  goes to the lead: D232's rule raises Windows' or macOS's limit; for a container, whose 14 minutes
  D232 kept, the lead decides (D134). The keychain steps add seconds per job, and building the live
  project (D337) adds the time of one more incremental Release build to Windows' job (not measured).
- **Real-window pass** (27 min 39 s for phases 0 to 6). Phase 6's eight methods and nine launches
  added 2 min 42 s (24 min 57 s for phases 0 to 5): about 18 s a launch. Each of 7a's five launches
  adds a real sign-in, the fake's start and a scenario copy, one to three captures and, for B, a small
  clone: about 25 to 45 s. **+3 to +5 minutes, about 31 to 33**, with +6 to +8 as the ceiling; D246's
  run of the 7a class alone about 3 to 5 minutes. The gate measures both; CLAUDE.md carries the number
  (ruling 30).
- **Live command**: about 2 minutes a run; twice per gate, once before the gate's pictures and once on
  the counted round, and again alone after a fix to its project only (ruling 26, D335; 13.7).

### 0.7 Earlier pictures

- **Change, by D256:** the settings screen's section list gains "Accounts" below "Keyboard
  shortcuts". About 35 of phase 6's pictures show the screen (`s14a` to `s14i`, `s15*`, `s16*`,
  `s17*`, `s18*`, `s19*`, `l31d`, `l31e`, and the real-window `s14a-general`, `l31d-settings-light`);
  each changes in the list's column only. WP-A records them in the 7a report's "Changes to expected
  results" and updates the earlier tests that assert the list; the gate compares them by region
  against references made on `master` (risk 32).
- **Already differ, not 7a's:** the status bar's `Git 2.56.0` box (D248). Every picture that shows it
  differs from `docs/test-reports/phase-6-pictures.sha256` whatever 7a does, so `sha256sum -c` against
  that list proves nothing for it. Such a picture below is compared with references made with the same
  git: by region with `master`'s (`scripts/compare-pictures.py`), no difference outside that box (9.3,
  11.6, 12.8), or byte for byte where its section says so (G23's, 5.5, 5.6).
- **Must not change:** the "Pull requests" placeholder and its absent menu; every branch, remote-branch
  and drop menu; the clone form (D261); the palette's earlier results ("Clone from GitHub…" is listed
  only while GitHub is not off, D299); the details panel (the avatar collapsed, laid out pixel for
  pixel as today); the graph; the left panel's automation name. G23's pictures (`g23g`, `g23h`,
  `g23i`) stay byte-identical; WP1 says how each is reached (5.5).

## 1. Shared contracts (WP0's brief)

Every type, seam, variable, file and port that more than one package uses. WP0 writes what 1.10
lists before wave 1, with no change of behaviour or pictures; each file's owner after WP0 is named.
A package may add members to a file it owns, but never to an interface another package implements
(`IHostingSession` and `IRepositoryHosting` keep 1.5's members, all WP0's), and never changes a
signature below without the lead. Names
and neighbours were checked in the code at `cea102c`.

Rules for every package: no token in an argument, URL, config value, environment variable, log line,
exception message, picture, automation value or file; a type holding a token hides it in
`ToString()`; never log an `HttpRequestMessage` or `HttpResponseMessage`; every network call has a
30 s limit and the caller's cancellation token (the watchdog is at 180 s); results reach the UI
thread through a post, and the UI thread never waits on the network, a keychain or a file (Q2).

### 1.1 Projects

| Project | Made by | References | In `VisualCommit.slnx` |
|---|---|---|---|
| `src/VisualCommit.Hosting` (`net10.0`; namespaces `VisualCommit.Hosting`, `.GitHub`, `.Tokens`) | WP0 | Core only; `Tmds.DBus.Protocol` (WP-K adds the reference) | yes |
| `src/VisualCommit.App` | | + Hosting (WP0) | yes |
| `tests/VisualCommit.Hosting.Tests` (`AssemblyInfo.cs` as `.Phase6`'s: `[AvaloniaTestApplication(typeof(HeadlessTestApp))]`, a module initializer calling `HeadlessTestApp.IsolateFromTheMachine()`, `[HangWatchdog]`; its `[AvaloniaFact]`s run beside the 7a project's, so every fake in it is on port 0 or in process, D265) | WP0, a smoke test; WP-F, WP-K, WP-G and WP-S add classes; WP-A, WP-B, WP-P and WP-N add their `ShellDriver` classes (D334) | App, Core, Git, Hosting, Testing; `xunit.v3`, `Avalonia.Headless.XUnit` | yes |
| `tests/VisualCommit.VisualTests.Phase7a` (as `.Phase6`'s csproj; links `tests/VisualCommit.App.Tests/RecordingActions.cs` and `RecordingLog.cs`) | WP-F | App, Core, Git, Hosting, Testing | yes (WP-F) |
| `tests/VisualCommit.LiveHostingTests` (`net10.0-windows`, `Exe`) | WP-LIVE | App, Core, Git, Hosting, Testing | no; CI's Windows job builds it and never runs it (D337) |

`Directory.Packages.props` (WP0): `Tmds.DBus.Protocol` `0.94.1`, with a comment: the version
Avalonia 12.1.3 resolves; it moves with `AvaloniaVersion` (as xunit does, D24).

### 1.2 Core

**`src/VisualCommit.Core/Hosting/AccountKey.cs`** (WP0; then WP-S), namespace `VisualCommit.Core.Hosting`:

```csharp
public enum HostingProvider { GitHub }   // 7b adds AzureDevOps, GitLab
// One account on one host (D263). Host and login lower-case (invariant); the login's own case is HostingAccount.Login.
public sealed record AccountKey(HostingProvider Provider, string Host, string Login)   // normalising constructor
{
    public string ProviderId { get; }   // "github"
    public string Id { get; }           // "github:github.com:octocat"; Host "127.0.0.1:47395" for the fake
}
```

**`src/VisualCommit.Core/Operations/CredentialBinding.cs`** (WP0; then WP-C):

```csharp
// A URL whose credentials come from an account (D255): git's URL after insteadOf, without user@.
public sealed record CredentialBinding(string Url, AccountKey Account)
{
    // WP-C (4.4): the bound URLs of the remotes' fetch and push URLs, with D278's rules.
    public static IReadOnlyList<CredentialBinding> Select(IReadOnlyList<RemoteEffectiveUrls> remotes, Uri webBase, AccountKey account);
}
public sealed class GitCredential(string token)
{
    public const string UserName = "x-access-token";
    public string Token { get; } = token;
    public override string ToString() => "GitCredential(***)";
}
public interface IGitCredentialSource   // bound URLs only, never UI (D255, ruling 6)
{
    // git's get: the token, or null (signed out, switch off, refused, still unread after 10 s).
    Task<GitCredential?> GetAsync(AccountKey account, CancellationToken cancellationToken);
    void Refused(AccountKey account);   // git's erase to the app's helper (ruling 4); WP-S checks the token first (D330)
}
public sealed class NullGitCredentialSource : IGitCredentialSource { public static NullGitCredentialSource Instance { get; } }
```

**Operations** (WP0; then WP-C unless named): `GitPrompt` gains a last positional `AccountKey? Account
= null` (the binding whose URL the prompt names); `IGitPromptBroker.Begin(string title, string
repositoryName, PromptMode mode, string? operationId = null, PromptGroup? group = null,
IReadOnlyList<CredentialBinding>? bindings = null)`, ignored by `NullGitPromptBroker`; `PromptScope.RefusedAccount`
(`AccountKey?`, protected set); `GitOperation.CredentialBindings` (init, default `[]`, beside
`PromptGroup`); `OperationQueue.cs` (then WP-N): `:631`'s `Begin` passes the bindings, and after the
run (`:701`) a failed result gets `RefusedAccount = scope.RefusedAccount`; `OperationResult.RefusedAccount`
(`OperationReport.cs`, then WP-N, which adds `OperationOffer`, 12.2); `PromptGroup.cs` (whole):
`int AnswersVersion` and `void ForgetAnswers()` (increments it), which WP-C's `PromptDialogs` and
`BulkRun` use.

**`src/VisualCommit.Core/Git/RemoteUrl.cs`** (WP0, whole) and `RepoRefs` (WP0):
`public sealed record RemoteUrl(string Remote, string Url, bool IsPushUrl);` (as configured; never
shown) and `RepoRefs.RemoteUrls` (`IReadOnlyList<RemoteUrl>`, configuration order, default `[]`).
`GitRepository.ReadRemoteKeysAsync` (`src/VisualCommit.Git/GitRepository.Refs.cs:80`) keeps the
`remote.<r>.url` and `remote.<r>.pushurl` values it already reads; no new git call, the form unchanged.

**`src/VisualCommit.Core/Git/UrlRewrites.cs`** (WP0 signatures with an identity body; then WP-C):

```csharp
public sealed record UrlRewrite(string Base, string Prefix, bool ForPush);   // url.<Base>.insteadOf = Prefix
public sealed record RemoteEffectiveUrls(string Remote, IReadOnlyList<string> Fetch, IReadOnlyList<string> Push);
public sealed record UrlRewrites(IReadOnlyList<UrlRewrite> Rules)
{
    public static UrlRewrites None { get; }
    public static UrlRewrites Parse(IEnumerable<(string Key, string Value)> entries);   // config --null --get-regexp ^url\.
    public IReadOnlyList<RemoteEffectiveUrls> Apply(IReadOnlyList<RemoteUrl> urls);    // git's rules (4.3)
}
```

**`AppPaths.cs`** (WP0): `AccountsFile` (`<data>/accounts.json`) and `AvatarsDirectory` (`<data>/avatars`).

### 1.3 Git layer

- `Task<UrlRewrites> IGitRepository.ReadUrlRewritesAsync(CancellationToken)`: one
  `git config --null --get-regexp ^url\.`, exit 1 meaning none. WP0's body in a new
  `src/VisualCommit.Git/GitRepository.Hosting.cs` returns `None` without a call; WP-C reads;
  `FakeRepositoryBase` returns `None`.
- `Task<string?> FetchToFetchHeadAsync(string remote, string sourceRef, NetworkOptions options, CancellationToken)`
  on `IGitRepository`: a pull request's head into `FETCH_HEAD` only, no ref and no config written;
  the commit's SHA read from `FETCH_HEAD`'s line, or null when that line is not this fetch's (10.5).
  WP0's body in a new `GitRepository.PullRequests.cs` throws `NotSupportedException`; WP-P builds it.
- `AskPassBroker` (`AskPassBroker.cs:53`; WP0 signature, WP-C body):
  `AskPassBroker(IGitPromptHandler handler, IAppLog? log = null, string? helperPath = null, IGitCredentialSource? credentials = null, IReadOnlyDictionary<string, string?>? sessionEnvironment = null, Func<GitVersion?>? gitVersion = null)`.
  The session's git environment (D220) with the process's gives the `GIT_CONFIG_COUNT` already
  present; `gitVersion` is a function because the broker is made before git's detection ends
  (`AppSession.cs:217`), and below 2.31 no binding is used (D277).
- A bound scope's environment (WP-C): 4.6.
- `RepositoryWatcher` gains a last parameter `TimeSpan? eventLag = null`; `GitRepository.WatcherLag`
  is `public static TimeSpan`, read once per process from `GitRepository.WatcherLagVariable`
  (`"VISUALCOMMIT_WATCHER_LAG_MS"`) where `CreateWatcher()` (`GitRepository.cs:168`) makes the
  watcher (WP1, D282).

### 1.4 Hosting: options, provider, records

**`src/VisualCommit.Hosting/HostingOptions.cs`** (WP0 whole; then WP-G):

```csharp
public sealed record GitHubEndpoints(Uri WebBase, Uri ApiBase)   // web, git and avatars; API
{
    public static GitHubEndpoints GitHubCom { get; }   // https://github.com/, https://api.github.com/
    public static GitHubEndpoints Local(int port);     // http://127.0.0.1:<port>/ and .../api/v3/
    public string Host { get; }   // "github.com" or "127.0.0.1:<port>": matched exactly with the scheme (ruling 23)
}
public sealed record HostingOptions(GitHubEndpoints? GitHub, Func<HttpMessageHandler> CreateHandler)
{
    public const string GitHubVariable = "VISUALCOMMIT_GITHUB_URL";
    public static HostingOptions Off { get; }   // nothing recognised, no request (the harnesses' default)
    // Unset: GitHubCom; "none": Off; "http://127.0.0.1:<port>/": Local(port); else Off and a warning.
    public static HostingOptions FromEnvironment(Func<string, string?> readEnvironment, IAppLog log);
    public static Func<HttpMessageHandler> DefaultHandler(GitHubEndpoints endpoints);   // 6.2
}
```

**`GitHub/GitHubApi.cs`** (WP0; then WP-G): `Version = "2026-03-10"`, `MediaType = "application/vnd.github+json"`.

**`src/VisualCommit.Hosting/HostingRecords.cs`** (WP0 whole; then WP-G):

```csharp
public sealed record HostedRepository(HostingProvider Provider, string Host, string Owner, string Name)
{ public string FullName { get; } }   // "owner/name"; compared case-insensitively
public sealed record HostingAccount(AccountKey Key, string Login, long Id, string? Name, Uri? AvatarUrl, bool UseForGit);
public sealed record RepositoryInfo(HostedRepository Repository, string DefaultBranch, bool IsPrivate, bool IsFork,
    bool IsArchived, string? Description, Uri CloneUrl, Uri HtmlUrl, DateTimeOffset UpdatedAt, bool CanPush,
    HostedRepository? Parent, string? ParentDefaultBranch);
// MoreExist: the cap was reached (1,000 repositories; 300 pull requests, ruling 14).
public sealed record RepositoryList(IReadOnlyList<RepositoryInfo> Repositories, bool MoreExist, bool SomeHiddenBySso);
public sealed record PullRequest(int Number, string Title, string? Author, bool IsDraft, Uri HtmlUrl,
    string HeadRef, string HeadSha, HostedRepository? HeadRepository,   // null: the fork was deleted
    string BaseRef, HostedRepository BaseRepository, DateTimeOffset CreatedAt)
{ public bool IsFromFork { get; } }   // HeadRepository is null or differs from BaseRepository
public sealed record PullRequestList(IReadOnlyList<PullRequest> PullRequests, bool MoreExist);
public sealed record NewPullRequest(string Title, string Body, string Head, string? HeadOwner, string Base, bool Draft);   // HeadOwner only across forks
public sealed record AuthorAvatar(long UserId, string Login, Uri AvatarUrl);
public sealed record AuthorMap(IReadOnlyDictionary<string, AuthorAvatar?> ByEmail);   // lower-case e-mail; null: GitHub does not know it
public enum HostingFailureKind { Network, BadCredentials, SsoRequired, RateLimited, Forbidden, NotFound, ApiVersion, Validation, ServerError, Malformed }
// Message: GitHub's own words (ruling 15), or for a network failure or time-out WP-G's fixed texts (D284).
// Detail: a 422's form (6.3, A5) or X-Accepted-GitHub-Permissions; RequestId: X-GitHub-Request-Id, for the log.
public sealed record HostingFailure(HostingFailureKind Kind, string Message, string? Detail = null,
    DateTimeOffset? RetryAt = null, Uri? Url = null, string? RequestId = null);
public sealed record HostingResult<T>(T? Value, HostingFailure? Failure)
{ public bool Succeeded { get; } public static HostingResult<T> Ok(T value); public static HostingResult<T> Fail(HostingFailure failure); }
```

**`IHostingProvider.cs`** (WP0; then WP-G):

```csharp
public interface IHostingProvider
{
    HostingProvider Kind { get; }
    GitHubEndpoints Endpoints { get; }
    HostedRepository? Recognise(string url);   // no network; null for another host
    bool SourceCanExist(string remoteRef);     // ruling 8: false only for refs/pull/<x>/head|merge, <x> not all digits
    Task<HostingResult<HostingAccount>> CheckTokenAsync(string token, CancellationToken ct);                          // A1
    Task<HostingResult<RepositoryList>> ListRepositoriesAsync(string token, CancellationToken ct);                    // A2
    Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(string token, HostedRepository repository, CancellationToken ct);   // A3
    Task<HostingResult<PullRequestList>> ListPullRequestsAsync(string token, HostedRepository repository, CancellationToken ct); // A4
    Task<HostingResult<PullRequest>> CreatePullRequestAsync(string token, HostedRepository baseRepository, NewPullRequest request, CancellationToken ct); // A5
    // A6: branch null asks the lookups only (no commits page); at most 20 lookups a call.
    Task<HostingResult<AuthorMap>> ReadAuthorsAsync(string token, HostedRepository repository, string? branch, IReadOnlyCollection<string> moreEmails, CancellationToken ct);
}
```

WP-G builds `public sealed class GitHubProvider(GitHubEndpoints endpoints, Func<HttpMessageHandler> createHandler, IAppLog log, TimeProvider clock) : IHostingProvider, IDisposable`,
which WP-S constructs, and `AvatarCache(string directory, Func<HttpMessageHandler> createHandler, TimeProvider clock, IAppLog log)`
with `Task<string?> FileOfAsync(AuthorAvatar avatar, CancellationToken)` (the local image's path, or
null; fetched without the token; a cancelled download writes nothing, 6.4, 7.5) and
`public static readonly TimeSpan Lifetime` (7 days).

### 1.5 Hosting: the session

**`src/VisualCommit.Hosting/IHostingSession.cs`** (WP0 whole with `NullHostingSession` and
`NullRepositoryHosting`; then WP-S, which implements the members and adds none, because WP-C's
`HostingSessionStub` and WP-V's fakes implement them too):

```csharp
public enum AccountState { SignedIn, NeedsSignIn }
public enum TokenPlace { Keychain, Session }
public enum AccountNoteKind { None, NoKeychain, SaveFailed, Refused, TokenMissing, ReadFailed, SessionEnded }
// NoteKind: which of the App's texts applies (8.2); Note: the store's reason, when one exists.
public sealed record AccountStatus(HostingAccount Account, AccountState State, TokenPlace? Place, AccountNoteKind NoteKind, string? Note);
// Failure: GitHub refused the token or was not reached; SaveFailedReason: kept for the session (ruling 19).
public sealed record SignInResult(AccountStatus? Status, HostingFailure? Failure, string? SaveFailedReason);

public interface IHostingSession : IGitCredentialSource, IDisposable   // events through the post delegate (UI thread)
{
    bool IsOff { get; }  IHostingProvider? GitHub { get; }  AccountStatus? Account { get; }  event EventHandler? AccountChanged;
    Task StartAsync(CancellationToken ct);         // accounts.json and the token, once (ruling 6)
    Task<bool> KeychainAvailableAsync(CancellationToken ct);   // cached probe, never prompts
    Task<SignInResult> SignInAsync(string token, CancellationToken ct);   // A1; replaces (D263); at once (D256)
    Task<string?> SignOutAsync(CancellationToken ct);   // null, or why the keychain kept its entry; deletes avatars/ (D291)
    void SetUseForGit(bool useForGit);             // at Save (D216)
    HostedRepository? Recognise(string url);
    bool SourceCanExist(string remoteUrl, string remoteRef);   // push guard; true when not recognised
    IReadOnlyList<CredentialBinding> BindingsForClone(string url);   // url after the user's insteadOf (9.3)
    bool BindsGit { get; }   // signed in, token in hand, UseForGit on, not off
    // A dashboard row without a tab (4.7, D332): empty unless BindsGit; else HostingRules.Bindings of rewrites.Apply(remoteUrls).
    IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites);
    Task<HostingResult<RepositoryList>> ListRepositoriesAsync(CancellationToken ct);
    IRepositoryHosting ForRepository(string workingDirectory);   // NullRepositoryHosting.Instance when off
}

public enum PullRequestsState { NotGitHub, SignedOut, Loading, Failed, Loaded }
// Remotes: remote → its GitHub repository; Repository: the one listed (0.2); ByRef: full ref name
// (refs/heads/x through an upstream of the same name, refs/remotes/origin/x) → its open pull request (7.8).
public sealed record RepositoryHostingState(PullRequestsState PullRequests, IReadOnlyDictionary<string, HostedRepository> Remotes,
    HostedRepository? Repository, PullRequestList? List, HostingFailure? Failure, IReadOnlyDictionary<string, PullRequest> ByRef)
{ public static RepositoryHostingState NotGitHub { get; } }

public static class HostingRemoteOrder   // 0.2's order: origin, upstream, then by ordinal name
{ public static IReadOnlyList<string> Of(IEnumerable<string> remoteNames); }

public sealed record AuthorAvatars(IReadOnlyDictionary<string, string> FileByEmail)   // lower-case e-mail → local PNG or JPEG
{ public static AuthorAvatars None { get; } public string? FileOf(string email); }   // FileOf lower-cases (invariant)

public interface IRepositoryHosting : IDisposable
{
    RepositoryHostingState State { get; }
    IReadOnlyList<CredentialBinding> Bindings { get; }   // empty unless signed in, switch on, token in hand
    AuthorAvatars Avatars { get; }
    bool WantsUrlRewrites { get; }                     // signed in and not off: read UrlRewrites with the refs
    event EventHandler? Changed;                       // through the post delegate
    void Update(RepoRefs refs, UrlRewrites rewrites);  // UI thread, after each refs read
    Task RefreshPullRequestsAsync(CancellationToken ct);
    void ShowAvatarsFor(IReadOnlyCollection<string> emails);   // authors of graph commits on GitHub remotes' branches (D311); capped lookups
    // A3 of the first GitHub remote in HostingRemoteOrder (Parent, ParentDefaultBranch); UI thread; kept per candidate.
    Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(CancellationToken ct);
    Task<HostingResult<PullRequest>> CreatePullRequestAsync(HostedRepository baseRepository, NewPullRequest request, CancellationToken ct);
}
public sealed class NullHostingSession : IHostingSession { public static NullHostingSession Instance { get; } }   // IsOff; BindsGit false; every hosting NotGitHub
// NotGitHub, no bindings, Avatars.None, WantsUrlRewrites false, never Changed; Dispose does nothing.
public sealed class NullRepositoryHosting : IRepositoryHosting { public static NullRepositoryHosting Instance { get; } }
```

WP-S builds `HostingSession(AppPaths paths, HostingOptions options, ITokenStore tokens, IAppLog log,
Action<Action> post, TimeProvider clock)` and `JsonAccountStore` (7.2). The lead puts it in
`AppSession.Start` at WP-S's merge (7.11).

### 1.6 Hosting: tokens

**`src/VisualCommit.Hosting/Tokens/ITokenStore.cs`** (WP0 whole; then WP-K):

- `record TokenKey(string Provider, string Host, string Account)` with `static TokenKey Of(AccountKey)`
  (lower-case); `record TokenStoreProbe(bool Available, string? Reason)`; `enum TokenStoreOutcome { Done,
  NotFound, Failed }`; `record TokenStoreResult(TokenStoreOutcome Outcome, string? Reason = null)`;
  `class TokenRead(outcome, token, reason)` with `Outcome`, `Token`, `Reason` and a `ToString()` that
  never shows the token.
- `interface ITokenStore`: `string Name` (the log only; no shown text names a store, ruling 17), `bool
  KeepsTokensAcrossRestarts`, `ProbeAsync(ct)` (never prompts; bounded), `SaveAsync(key, token, ct)`,
  `ReadAsync(key, ct)` → `TokenRead`, `DeleteAsync(key, ct)`.
- `static class TokenStores`: `Variable = "VISUALCOMMIT_KEYCHAIN"` (`memory` | `unavailable`),
  `TestsVariable = "VISUALCOMMIT_KEYCHAIN_TESTS"` (`windows` | `macos` | `secret-service`),
  `FromEnvironment(Func<string, string?> readEnvironment, IAppLog log)`: `memory` a
  `MemoryTokenStore(true)`, `unavailable` `MemoryTokenStore(false)` (D253), unset the platform's (WP0:
  memory until WP-K); logs the choice. `sealed class MemoryTokenStore(bool available = true) : ITokenStore`.
- WP-K adds `TokenVault(ITokenStore store, IAppLog log)` with `KeychainAvailableAsync`, `KeepAsync(TokenKey,
  string, ct)` → `(TokenPlace Place, string? SaveFailedReason)`, `ReadAsync` (memory first, the store
  once per run) and `ForgetAsync` → a reason or null; and `TokenEntryNames` and `TokenStoreReasons`
  (3.3), which WP-S and WP-A read.

### 1.7 App

- **`AppSession.Start`** (lead): after `readClock`, `HostingOptions? hosting = null, ITokenStore? tokens = null`
  (null: `FromEnvironment(Environment.GetEnvironmentVariable, log)` of each, the process's environment,
  never `readEnvironment`, which stays auto-fetch's reader); the session (WP0 `NullHostingSession`, from
  WP-S's merge `HostingSession`) goes to `CreateBroker` and `TabServices` and is disposed after
  `Shell.CloseAll()`. `public Task HostingStarted { get; }` is the session's `StartAsync`, started on
  the thread pool (`Task.Run`) once the window exists; `ShellDriver.Start` pumps the UI thread until it
  completes (2.5).
- **`TabServices`** (lead): last parameter `IHostingSession? hosting = null`; `Hosting` (default
  `NullHostingSession.Instance`); `public Func<Task<bool>>? SignInToGitHub { get; set; }`, set by the
  window, true when an account is signed in afterwards.
- **`IFileLauncher`** (WP0 with `FileLauncher`'s body; then WP-P): `Task OpenUrlAsync(Uri url, CancellationToken ct = default);`
  `http` and `https` only, `TopLevel.Launcher.LaunchUriAsync` on the UI thread, a warning when it
  returns false.
- **`PromptDialogs.CreateBroker`** (WP0; then WP-C): `CreateBroker(IGitPromptHandler handler, IAppLog log, IGitCredentialSource? credentials = null, IReadOnlyDictionary<string, string?>? sessionEnvironment = null, Func<GitVersion?>? gitVersion = null)`.
- **`IRepositoryActions`** (WP0; lead afterwards), a block "Hosting" of seven actions:
  `CreatePullRequestAsync(string headRef, string? baseRef)`, `OpenPullRequestAsync(PullRequest)`,
  `CheckOutPullRequestAsync(PullRequest)`, `CopyPullRequestLinkAsync(PullRequest)`,
  `RefreshPullRequestsAsync()`, `OpenPullRequestsInBrowserAsync()`, `SignInToGitHubAsync()` (runs
  `TabServices.SignInToGitHub`), each a `Task`. WP0's stubs return `Task.CompletedTask`: the first in
  `RepositoryOperations.CreatePullRequest.cs` (then WP-N), the other six in
  `RepositoryOperations.PullRequests.cs` (then WP-P). `RecordingActions` is a `DispatchProxy`.
- **Call points** (WP0, then the named owner): `RepositoryOperations.Run.cs:383`, in the operation's
  initializer, `CredentialBindings = Repository.Hosting.Bindings` (WP-C: before `:344` a local
  `bindings`, `Services.Hosting.BindingsFor(Refs.RemoteUrls, rules)` for a summary row's network
  operation while `BindsGit`, else `Repository.Hosting.Bindings`; `:383` becomes `CredentialBindings =
  bindings`; 4.7, D332); `AutoFetchScheduler.CreateOperation(IGitRepository repository, IReadOnlyList<CredentialBinding>? bindings = null)`,
  at `:160` with `repository.Hosting.Bindings` (WP-C); `WelcomeViewModel.cs:253`'s `Begin` with
  `bindings: _services.Hosting.BindingsForClone(url)` (WP-B, which passes the URL after the user's
  `insteadOf` rules, 9.3).
- **`ViewModels/RepositoryViewModel.Hosting.cs`** (WP0; then WP-S): `IRepositoryHosting Hosting { get; private set; }`
  (`services.Hosting.ForRepository(workingDirectory)`; `NullRepositoryHosting.Instance` in summary
  mode: `ForSummary` calls a private constructor overload with `bool summary` that sets `IsSummary`
  before the set-up method runs, so no `RepositoryHosting` is made for a dashboard row), observable
  `RepositoryHostingState HostingState` and `AuthorAvatars Avatars`, `ApplyHosting(RepoRefs refs)`
  called from `ApplyRefs` (`RepositoryViewModel.cs:1706`), a set-up method the constructor calls that
  subscribes `Hosting.Changed` (both properties; `LeftPanel.UpdatePullRequests(HostingState)` outside
  summary mode), `PromoteHosting()` called from `Promote` (`RepositoryViewModel.cs:204`; empty in
  WP0), and disposal. Only WP-V's `RepositoryViewModel.Avatars.cs` overrides `OnPropertyChanged`.
  `IsWatcherQuiet` (`RepositoryViewModel.cs:319-323`) gains `&& IsHostingQuiet`, and its doc comment
  "nor a URL-rewrites read the hosting started"; this file has `private bool IsHostingQuiet => true;`
  (WP-S's body, 7.10).
- WP0 stubs, then the named owner: `LeftPanelViewModel.UpdatePullRequests(RepositoryHostingState)`
  (keeps the state, changes no row; WP-P); `RepositoryMenuContext.Hosting` (`RepositoryHostingState?`,
  init, filled in `RepositoryViewModel.MenuContext`, `RepositoryViewModel.Menus.cs:52`, null for
  `NotGitHub`; WP-P); `SettingsScreenServices.Hosting` (`IHostingSession?`, init, set at
  `MainWindowViewModel.Settings.cs:60`; WP-A); `SettingsSection.Accounts` appended last to the enum in
  `TabServices.cs:218` (no button until WP-A); `MainWindowViewModel.Hosting.cs`'s `private Task<bool>
  SignInToGitHubAsync()` (false; WP-A), assigned to `Services.SignInToGitHub` by the lead's line in the
  constructor.
- **`ViewModels/Hosting/HostingTexts.cs`** (WP0; lead afterwards), texts more than one package shows:
  `GitHub = "GitHub"`, `AccountsSection = "Accounts"`, `Keychain = "this computer's keychain"`,
  `SignInInSettings = "Sign in to GitHub in Settings, Accounts."`, `AddAccountTitle = "Add GitHub account"`,
  `SignInToGitHubItem = "Sign in to GitHub…"`. Each package's other texts go in a file of its own
  (`OperationTexts.Hosting.<Area>.cs`). `HostingTexts` is a `static partial class`.
- **`ViewModels/Hosting/HostingTexts.Describe.cs`** (WP0 the signature, with the body `failure.Message`;
  WP-B the body and its tests, 9.1; lead afterwards): `public static string Describe(HostingFailure
  failure, string? login)`, the one wording of a GitHub failure that WP-B, WP-P and WP-N show (D338);
  `login` is the signed-in account's, or null. Its text is shown as it is, never wrapped in another
  sentence; a caller adds only what its own table names (WP-P's tooltip `Detail`, WP-N's create-only
  rows, 12.6). WP-A's sign-in dialog keeps its own texts (8.2).

  | `Kind` | Text |
  |---|---|
  | `Network` | `Message` as it is (D284) |
  | `BadCredentials` | with a login `OperationTexts.TokenRefused(login)` (WP-C's, D276); without, `Message`, with a full stop added when it lacks one, then " Sign in to GitHub in Settings, Accounts." |
  | `RateLimited` | "GitHub's rate limit was reached. Try again after 14:05." (`RetryAt` in local time, `HH:mm`, invariant culture); without `RetryAt` "… Try again in a few minutes." |
  | `SsoRequired` | "acme requires single sign-on: authorise the token for it on GitHub, then try again.", the organisation the path segment after `orgs/` in `Detail`'s URL, else "The organisation" |
  | `ApiVersion`; `ServerError`; `Malformed` | "This version of VisualCommit needs an update to work with GitHub."; "GitHub had a problem. Try again later."; "GitHub's answer could not be read." |
  | `Forbidden`, `NotFound`, `Validation` | GitHub's words (ruling 15): "GitHub answered: {Message}." (a full stop at `Message`'s end dropped first) |
- **Owners after WP0**: `AppCommands.Palette.cs` WP-B, then WP-P at WP-B's merge;
  `MainWindowViewModel.Commands.cs` WP-B; `LeftPanelViewModel*` and `LeftPanelView.axaml` WP-P;
  `CommitGraphControl*`, `RepositoryGraphView.axaml(.cs)`, `RightPanelView.axaml(.cs)` and
  `CommitDetailsViewModel.cs` WP-V; `IStatusBarOperation.cs`, `OperationStatusViewModel.cs`,
  `StatusBarView.axaml` and `DashboardBulkViewModel.cs` WP-N.

### 1.8 Variables, files, ports

| Variable | Read by | Values | Default in the harnesses |
|---|---|---|---|
| `VISUALCOMMIT_GITHUB_URL` | `HostingOptions.FromEnvironment` | unset: github.com; `none`: off; `http://127.0.0.1:<port>/`: the fake | `none` |
| `VISUALCOMMIT_KEYCHAIN` | `TokenStores.FromEnvironment` | unset: the platform's; `memory`; `unavailable` (any other: memory and a warning, D269) | `memory` |
| `VISUALCOMMIT_KEYCHAIN_TESTS` | WP-K's real-store test classes | `windows`, `macos`, `secret-service` | unset; CI's keychain steps only |
| `VISUALCOMMIT_WATCHER_LAG_MS` | `GitRepository.WatcherLag` (WP1) | 0 to 10,000 | unset; the lead's lagged runs |
| `VISUALCOMMIT_LIVE_GITHUB_TOKEN`, `VISUALCOMMIT_LIVE_GITHUB_REPO` | `VisualCommit.LiveHostingTests` | the owner's token; `owner/name` | removed from every test process (Windows user variables, D260) |
| `GIT_CONFIG_COUNT` and pairs, `GIT_TRACE_REDACT=1` | git, from a bound scope | 0.2 | — |

`GitIsolation.MachineIndependence` (`tests/VisualCommit.Testing/GitIsolation.cs:297`, WP0; then WP-F)
gains `VISUALCOMMIT_GITHUB_URL=none` and `VISUALCOMMIT_KEYCHAIN=memory` and removes `GH_TOKEN`,
`GITHUB_TOKEN`, `GH_ENTERPRISE_TOKEN`, `GITHUB_ENTERPRISE_TOKEN`, `GH_HOST` and the live variables, so
every test process and the real-window app start off and in memory (ruling 24; `AppSession.Start`
reads both variables from the process, never through its `readEnvironment`, 1.7); it leaves the lag
variable alone; a check's own variables override.

Data folder: `accounts.json` (WP-S: `version`, and per account `provider`, `host`, `login`, `id`,
`name`, `avatarUrl`, `useForGit`, `tokenState`; never a token); `avatars/` (WP-G: `<user id>.png`
or `.jpg` and `index.json`); `avatars/authors/` (WP-S: the e-mail maps, D291). All written through
`DurableFile`, with camelCase field names as `settings.json`'s (D336); a sign-out deletes `avatars/`
whole (7.5). `settings.json` is unchanged.

Port: `AuthRemoteServer.Phase7aVisualChecksPort = 47395` (WP-F; D265).

### 1.9 Seams and their fakes

- Desktop app: options from the environment (github.com), the platform's store, `HostingSession`, the
  browser. Headless (`ShellDriver.Start`): `Off`, or with `gitHub:` the fake's `Endpoints` through a
  `LoopbackOnlyHandler`, or with `hosting:` those options; a `FakeTokenStore` (new per start, or the one
  given); `HostingSession` over them; `FakeFileLauncher` records `FileLaunchKind.OpenUrl`. Real window
  (`RealApp.Launch`): `none` unless a 7a check sets the fake's base, `memory`, `HostingSession`, the real
  launcher (13.5, D321). `TabServices` built by hand: `NullHostingSession`, `NullGitCredentialSource`,
  a null launcher.

Test infrastructure other packages use (owner WP-F unless named; members in section 2):
- `VisualCommit.Testing.Hosting.FakeGitHub` (2.3, 2.4): the fake on a port or in process; users,
  tokens (`github_pat_FAKE-<login>-<n>`, classic `ghp_FAKE-<login>-<n>`), repositories, pull
  requests; `Requests` and `GitRequests` (snapshots, safe from another thread); `HoldNext(route)`
  returning `FakeGitHubHold : IDisposable` with `Task Arrived`; `FailNext(route, FakeGitHubFault)`.
- `FakeTokenStore : ITokenStore` (`tests/VisualCommit.Testing/Headless/`, 2.5).
- `ShellDriver.Start` and `StartWithTabs` gain, last, `FakeGitHub? gitHub = null, FakeTokenStore? tokens = null, HostingOptions? hosting = null`
  (`gitHub` with `hosting`: `ArgumentException`), and `Tokens`, `GitHub`, `Hosting`, `RefusedRequests`
  (the network tripwire) and `SignInAsync(string token)` (through the session, no dialog);
  `TokenTripwire.AssertAbsent(string token, IEnumerable<string> folders, IGitCallLog? calls)`;
  `Scenarios.HostingAsync()`, `ServeHostingAsync(...)` → `ServedHosting` (2.6). WP-C:
  `AskPassRunner.RunCredentialAsync`, `RecordingCredentialSource`.
- WP1: `tests/VisualCommit.Testing/Headless/GitCallBudget.cs` (`RefreshForm`, `RefreshForms`,
  `NotWholeRefreshes`, `AssertOnlyWholeRefreshes`, `AssertAtMost`; then WP-S adds the optional
  `git config --null --get-regexp ^url\.` form) and `ShellDriver.Budget.cs` (`QuietCallMarkAsync()`,
  `QuietCallsSinceAsync(long, Func<string, bool>?)`). 7a's checks count git calls only through them
  (ruling 28); 2.8's copied call assertions take the same `long` mark and prove presence and order only.
- `tests/VisualCommit.Testing/AppLogFile.cs`: WP-S adds `PullRequestsListed(repo)` for its Debug line
  `{repo}: listed {n} open pull requests of {owner/name}`; then WP-V adds `AvatarsShown(repo)` for
  `{repo}: showing GitHub avatars for {n} authors`. Both lines are written on the UI thread after the
  state is applied (D322).

### 1.10 The WP0 list

In the main checkout after L1, every item marked WP0 in 1.1 to 1.8, with no behaviour or picture
changed; the build and the classes it touches pass. Besides: Hosting's `AssemblyInfo.cs` with
`InternalsVisibleTo("VisualCommit.Hosting.Tests")`; `CredentialBinding.Select` throwing
`NotImplementedException`; a `RefReadingTests` case for `RemoteUrls`; a smoke test of
`FromEnvironment`'s `none` and `memory`; `FileLaunchKind.OpenUrl`; compile fixes in fakes.

Files that stay the lead's throughout: `AppSession.cs`, `TabServices.cs`, `MainWindowViewModel.cs`,
`IRepositoryActions.cs`, `IGitRepository.cs`, `FakeRepositoryBase.cs`, `Directory.Packages.props`,
`VisualCommit.slnx` (WP-F adds its project's line in its own worktree), `.github/workflows/ci.yml`,
`CLAUDE.md`, the docs, `Theme/Icons.axaml` and `THIRD-PARTY-NOTICES.md` (11.1).

## 2. WP-F: the fake of GitHub and the harness

### 2.1 Goal and files

What later packages test against: a local GitHub (git, API, avatars) on `AuthRemoteServer`, its routes
in process, a fake keychain, `ShellDriver`'s seams and two tripwires, the 7a scenario and project,
and the phase as a string. Status step 5; D250, rulings 23 to 25; D265 to D267. Wave 1, branch
`wip/p7a-fake`. No product code, no part's checks, no real-window pass code (WP-R), no picture
changes. WP-F owns 1.9's files and, after WP0, `GitIsolation.cs` (no change expected).

- `tests/VisualCommit.Testing/AuthRemoteServer.cs`: host mode (2.2).
- New in `tests/VisualCommit.Testing/Hosting/`: `FakeGitHub.cs`, `.Router.cs`, `.Json.cs`, `.Git.cs`,
  `.Model.cs` (`FakeUser`, `FakeRepository`, `FakePullRequest`, `FakeTokenKind`, `FakeGitHubFault`,
  `FakeGitHubRequest`, `FakeGitHubHold`), `FakeGitHubHandler.cs`, `LoopbackOnlyHandler.cs`,
  `TokenTripwire.cs`; in `Headless/`: `FakeTokenStore.cs`, `ShellDriver.Phase7a.cs`.
- `Headless/ShellDriver.cs`: `Start` (`:108-157`), `StartWithTabs` (`:164-180`), constructor (`:44-61`).
- `Scenarios.Phase7a.cs` (new); `TempRepo.cs`: `CommitFileAsAsync` beside `CommitFileAsync` (`:497`).
- The phase as a string (2.7); `tests/VisualCommit.VisualTests.Phase7a/*` (2.8) and its slnx line after
  `Phase6Look`'s.

### 2.2 `AuthRemoteServer`: host mode (D266)

Every existing start and behaviour stays. Added:

```csharp
public const int Phase7aVisualChecksPort = 47395;   // 7a's walk-through and real-window twin; 47396 reserved
public interface IAuthRemoteRoutes
{
    bool Claims(string method, string path);   // from the request line alone
    Task<AuthRemoteResponse> ServeAsync(AuthRemoteRouteRequest request, CancellationToken cancellationToken);
}
public sealed record AuthRemoteRouteRequest(string Method, string Host, string Path, string Query,
    IReadOnlyDictionary<string, string> Headers, byte[] Body);   // headers case-insensitive
public sealed record AuthRemoteResponse(int Status, string Reason, IReadOnlyList<KeyValuePair<string, string>> Headers, byte[] Body);
public sealed record AuthRemoteHostOptions(IAuthRemoteRoutes Routes,
    Func<string, string?> LoginOfToken,      // Basic password → login; null: unknown or revoked
    Func<string, bool> IsPublic,             // "octo/hello" → anonymous upload-pack
    Func<string, string, bool> MayPush,      // ("octo/hello", login) → receive-pack
    string Realm = "GitHub");
public static Task<AuthRemoteServer> StartForHostAsync(string folder, AuthRemoteHostOptions options, int port = 0, bool fallBackToFreePort = false);
public void Serve(string ownerAndName);      // registers <folder>/<owner>/<name>.git
```

- **Two levels.** Keys `owner/name`; `_served` (`:85`) becomes a `ConcurrentDictionary` so `Serve` adds
  after the start. `Serve` runs no git: the folder must exist with `HEAD` and `objects/`, each segment
  of letters, digits, `.`, `-`, `_` (`:394`'s rule). `RepositoryOf` (`:949-964`) takes two segments;
  `/octo/hello.git/…` and `/octo/hello/…` both name `octo/hello.git`; `PATH_INFO` goes to
  `http-backend` as requested. Git fact (2.56): with `GIT_PROJECT_ROOT=<folder>`, both
  `PATH_INFO=/acme/tool.git/info/refs` and `/acme/tool/info/refs` answer (`http-backend` tries
  `<path>.git` itself); Git Bash's MSYS rewrote `PATH_INFO` until `MSYS2_ENV_CONV_EXCL='*'`, .NET's
  process start does not. Holds and releases take `"octo/hello"`.
- **The hook** runs in `HandleAsync` after `PassGateAsync` (`:707`), before `AuthenticatedUser`
  (`:709`): a claimed request gets `100 Continue` if asked, its body read, `ServeAsync`'s answer
  written with `Content-Length` and `Connection: close`, and an `AuthRemoteRequest` recorded.
- **Token mode.** Basic with any non-empty user name and a password `LoginOfToken` knows signs in as
  that login (`REMOTE_USER`). `AuthRemoteRequest` (`:1466`) gains a last `string? GivenUserName = null`;
  the password is never recorded. 401 says `Basic realm="GitHub"`.
- **Anonymous reads.** Upload-pack of an `IsPublic` repository with no `Authorization` is served as
  `anonymous`, as GitHub does; a wrong header gets 401; a private one always needs a token. So a proof
  of the app's helper clones the private `octo/hello`.
- **Push**: `git-receive-pack` needs a login with `MayPush`, else 403 `text/plain` "Permission to
  octo/hello.git denied to ada.".

### 2.3 `FakeGitHub`

```csharp
public sealed partial class FakeGitHub : IAsyncDisposable
{
    public const int InProcessPort = 1;   // InProcess's Endpoints = Local(1), never listened on
    public static Task<FakeGitHub> StartAsync(string root, int port = 0, bool fallBackToFreePort = false, CancellationToken ct = default);
    public static FakeGitHub InProcess(string root);
    public GitHubEndpoints Endpoints { get; }  public int Port { get; }  public bool PortWasTaken { get; }
    public HttpMessageHandler CreateHandler();                  // the same routes in process
    public int MaxPerPage { get; set; } = 100;
    public FakeUser AddUser(string login, long id, string? name = null);
    public string AddToken(string login, FakeTokenKind kind = FakeTokenKind.FineGrained, IReadOnlyCollection<string>? scopes = null, DateTimeOffset? expires = null);
    public void Revoke(string token);
    public Task<FakeRepository> AddRepositoryAsync(string owner, string name, string? sourceRepository = null, bool isPrivate = false, string? forkOf = null, string? description = null, CancellationToken ct = default);
    public void AddListedRepositories(string owner, int count);   // API-only records "<owner>/listed-0001"…, no folder; git gets 404
    public FakePullRequest AddPullRequest(string owner, string repository, string title, string author, string headRef, string baseRef, string? headRepository = null, bool draft = false, int? number = null);
    public void LinkEmail(string email, string login);
    public FakeRepository Repository(string fullName);
    public Task SyncRefsAsync(string fullName, CancellationToken ct = default);
    public IReadOnlyList<FakeGitHubRequest> Requests { get; }   // snapshots
    public IReadOnlyList<AuthRemoteRequest> GitRequests { get; }
    public IReadOnlyList<string> Errors { get; }               // server and ref sync; checks assert empty
    public FakeGitHubHold HoldNext(string route);               // Arrived completes when the request waits
    public void FailNext(string route, FakeGitHubFault fault);
}
public enum FakeTokenKind { Classic, FineGrained }
```

- `StartAsync`: host mode on `root`, `Endpoints = Local(Port)`; `InProcess`: no server. Neither deletes
  anything under `root`; disposing releases every hold, then the server.
- `FakeUser`: `Login`, `Id`, `Name`, settable `IsOrganization`, `AvatarBytes` (`image/png`, default
  `TestImages.SquareOn(64, 64, colour, #FFFFFF, 24)`: 1001 `#2F81F7`, 1002 `#8250DF`, 1003 `#1F883D`,
  others `#BF3989`).
- Tokens: `github_pat_FAKE-<login>-<n>` or classic `ghp_FAKE-<login>-<n>` (`scopes` default `repo,
  workflow`), `n` from 1 (r5 4.1: nothing for push protection to flag); `Revoke` acts on the next
  request, git's included.
- `AddRepositoryAsync`: `<root>/<owner>/<name>.git`; with `sourceRepository` one `git clone --bare
  --no-hardlinks`, else an existing folder in place or `git init --bare --initial-branch main`.
  `FakeRepository`: `Id` (3001 up), `Owner`, `Name`, `FullName`, `IsPrivate`, `ForkOf`, `Description`,
  `DefaultBranch` (from `HEAD`), `BarePath`, settable `IsArchived`, `UpdatedAt` (`AppClocks.HarnessTime`),
  `Pushers` (the owner if a user).
- `AddPullRequest`, in memory: `Number` (given, else the repository's highest plus one), `Title`,
  `Author` (a known user), `HeadRef`, `HeadRepository` (`owner/name`, null for the same repository; an
  unknown name is a deleted fork), `BaseRef`, `IsDraft`, settable `State`, `MaintainerCanModify`,
  `HeadSha`, `CreatedAt = HarnessTime`. `LinkEmail` ignores case.
- **Ref sync** (D267), before the list, a create and every git request of a repository: one
  `git for-each-ref --format=%(objectname) %(refname) refs/heads/ refs/pull/` in it and one per fork
  with open pull requests; then each `refs/pull/<n>/head` that differs from its head: same
  repository, one `git update-ref --stdin` for all; a fork's, one
  `git fetch --quiet --no-tags <fork> +refs/heads/<b>:refs/pull/<n>/head …` per fork (objects copied,
  as GitHub's shared network). A deleted fork's SHA is its existing ref; none is an `Errors` line.
  Git facts (2.56): that fetch writes the ref, its objects and a harmless `FETCH_HEAD`;
  `rev-list --count refs/heads/main..refs/pull/7/head` gave 1, `main..main` 0; `git log` in an empty
  bare repository exits 128.
- **Holds**: the next request with the route waits before anything is decided until the hold is
  disposed, at most 120 s (then released with an `Errors` line). `git:octo/hello` delegates to
  `AuthRemoteServer.HoldNextRequest`. **`FailNext`**: once, after the header checks, before auth.
- `FakeGitHubRequest(Method, Host, Path, Query, Route, TokenPresent, Login, Conditional, Status, Body)`,
  `Body` for POST and PATCH only; never a token. State under one lock; git outside it, through
  `GitRunner` with `TempRepo.IsolatedEnvironment` (`AuthRemoteServer.cs:118-119`).

### 2.4 Routes, checks, shapes, headers and faults

API base `<web>api/v3/`; owner and name case-insensitive; route keys lower-case.

| Key | Request | Answer |
|---|---|---|
| `user` | `GET api/v3/user` | user with `name` |
| `user/repos` | `GET api/v3/user/repos` | paged: those the login owns or may push to, and the listed records |
| `repo:o/r` | `GET api/v3/repos/{o}/{r}`, `…/repositories/{id}` | repository; `parent`, `source` on a fork |
| `pulls:o/r` | `GET …/repos/{o}/{r}/pulls` | paged, number descending |
| `create:o/r` | `POST …/repos/{o}/{r}/pulls` | 201, 403 or 422 |
| `update:o/r` | `PATCH …/pulls/{n}` | 200; `{"state":"closed"}` closes |
| `commits:o/r` | `GET …/repos/{o}/{r}/commits` | paged; 409 when empty |
| `avatar:<id>` | `GET avatars/u/{id}` (any query) | the user's bytes, no auth |
| `page:o/r` | `GET {o}/{r}`, `{o}/{r}/pulls`, `{o}/{r}/pull/{n}`, `settings/…` | `text/html` `<!doctype html><title>octo/hello #1</title>` |
| `git:o/r` | git (TCP only) | 2.2 |

In process any host is served by path and recorded (WP-G's cross-host tests); git's paths get 404.

**Checks**, in order (r5 10.3); errors are `{"message","documentation_url":"https://docs.github.com/rest","status"}`:
1. no `User-Agent` → 403 "Request forbidden by administrative rules. Please make sure your request has a User-Agent header.";
2. `X-GitHub-Api-Version` missing or not `GitHubApi.Version` → 400 "Unsupported 'X-GitHub-Api-Version' header: <value>." (stricter than GitHub, D266);
3. `Accept` without `application/vnd.github+json` → 415 "Unsupported 'Accept' header.";
4. a pending fault;
5. no `Authorization`, or `Basic` → 401 "Requires authentication" (stricter than GitHub, D266); `Bearer` or `token` with an unknown or revoked token → 401 "Bad credentials";
6. a private repository the login cannot see (not owner nor pusher, or a classic token without `repo`) → 404 "Not Found".

**A2**: `per_page` (30, capped at `MaxPerPage`), `page`, `sort` (`full_name` default; `updated`,
`pushed`, `created` by `UpdatedAt` descending, then name), `direction`, `visibility`, `affiliation`;
`type` with either → 422 "If you specify visibility or affiliation, you cannot specify type.".
**A4**: `state` (`open` default, `closed`, `all`); `head` as `owner:ref`, ignored without `:` as on
GitHub; `base`; paging. **A5** body `title`, `head` (`branch`, or `owner:branch` of the base or a fork
of it), `base`, `body`, `draft`, `maintainer_can_modify`; refusals in order, each 422 "Validation
Failed" with `errors[0]`: no title `{field title, code missing_field}`; base not a branch
`{field base, code invalid}`; head not a branch `{field head, code invalid}`; an open one with that
head label and base `{code custom, message "A pull request already exists for <owner>:<branch>."}`;
nothing ahead (`rev-list --count`, a fork's head fetched into `refs/pull/<n>/head` first and deleted
on refusal) `{code custom, message "No commits between <base> and <head ref>"}`. A fine-grained
token whose login cannot push to the base → 403 "Resource not accessible by personal access token",
`X-Accepted-GitHub-Permissions: pull_requests=write`. Else 201, author the token's login,
`refs/pull/<n>/head` written. **A6**: `sha` (default branch), `author` (e-mail, or login through
`LinkEmail`), paging; one `git log --format=%H%x00%an%x00%ae%x00%aI%x00%cn%x00%ce%x00%cI%x00%s <sha> --`;
unknown `sha` → 422 "No commit found for SHA: <sha>"; empty → 409 "Git Repository is empty.". The
texts of the version, `type`, unknown-SHA and empty answers are from memory (r5 14); WP-LIVE records
the real ones and the lead corrects the fake.

**Shapes** (snake_case, UTC `yyyy-MM-ddTHH:mm:ssZ`, `<web>` = `Endpoints.WebBase`): simple user `login`,
`id`, `type`, `avatar_url` `<web>avatars/u/<id>?v=4`, `html_url` (A1 adds `name`); repository `id`,
`name`, `full_name`, `owner`, `private`, `fork`, `archived`, `description`, `default_branch`,
`html_url` `<web>o/r`, `clone_url` `<web>o/r.git`, `ssh_url` `git@127.0.0.1:o/r.git`, `created_at`,
`updated_at`, `pushed_at`, `permissions` (`admin`, `maintain`, `push` from `Pushers`, `triage`,
`pull`); pull request `id` (4000 + number), `number`, `state`, `draft`, `title`, `body`, `html_url`
`<web>o/r/pull/<n>`, `created_at`, `updated_at`, `user`, `head` and `base` (`label` `owner:ref`, `ref`,
`sha`, `user`, `repo`, null for a deleted fork), `maintainer_can_modify`; commit `sha`, `html_url`,
`commit` (`author`, `committer`: `name`, `email`, `date`; `message`), `author` and `committer` (simple
user when linked, else `null`).

**Headers** (r5 10.4): `Content-Type: application/json; charset=utf-8`; `ETag: W/"<32 hex of SHA-256
of the body>"`, a matching `If-None-Match` → 304, no body, not counted; `X-RateLimit-Limit: 5000`,
`-Remaining`, `-Used` (per login), `-Reset` (`HarnessTime` plus an hour, Unix seconds),
`-Resource: core`; `Link` (`next`, `last`; `prev`, `first` past page 1) with absolute URLs on
`ApiBase`, other parameters kept; `X-OAuth-Scopes` for classic tokens only;
`GitHub-Authentication-Token-Expiration: yyyy-MM-dd HH:mm:ss UTC` when set; `X-GitHub-Request-Id:
FAKE-<4 digits>`.

**Faults** (`FakeGitHubFault`'s static members, r5 10.5): `BadCredentials` (401); `PrimaryRateLimit`
(403, remaining 0, "API rate limit exceeded for user ID <id>."); `SecondaryRateLimit` (403,
`Retry-After: 60`, "You have exceeded a secondary rate limit. Please wait a few minutes before you
try again."); `SsoRequired` (403, `X-GitHub-SSO: required; url=<web>orgs/acme/sso?authorization_request=FAKE`,
"Resource protected by organization SAML enforcement."); `SsoPartialResults` (A2 normal,
`X-GitHub-SSO: partial-results; organizations=1002`); `Forbidden` (403 "Resource not accessible by
personal access token", `X-Accepted-GitHub-Permissions` by route: `metadata=read`, `pull_requests=read`
or `=write`, `contents=read`); `NotFound`; `ApiVersionGone` (410); `ServerError` (500 "Server Error");
`BadGateway` (502, `text/html`); `Malformed` (200, `{"login":`); `CreatedThenDropped` (a create made
in full, then the connection closed without an answer: WP-N's lost POST); `Moved` (301 to
`api/v3/repositories/<id>` plus the rest); `MovedTo(Uri)`. An avatar request with `Authorization` is
answered and logged `TokenPresent`; checks assert none.

### 2.5 The harness

- **`ShellDriver.Start`/`StartWithTabs`** gain, last, `FakeGitHub? gitHub = null, FakeTokenStore? tokens = null, HostingOptions? hosting = null`
  (passed on at `:179`; `gitHub` with `hosting`: `ArgumentException`) and call `AppSession.Start(…,
  hosting: options, tokens: tokens ?? new FakeTokenStore())`. They return synchronously, so before
  their last `Settle()` they pump: `Dispatcher.UIThread.RunJobs()` and a 15 ms sleep until
  `Session.HostingStarted.IsCompleted`, at most `DefaultWaitTimeout`, then a `TimeoutException`
  "Timed out after 60 s waiting for the hosting session's start." or the start's own exception
  rethrown. They never block on the task (its posts need the UI thread). A test that holds the
  start's store read (`FakeTokenStore.HoldNext()`) tests the session itself (7.13), not through `Start`.
  With a fake, `options = new HostingOptions(gitHub.Endpoints, () => new LoopbackOnlyHandler(HostingOptions.DefaultHandler(gitHub.Endpoints)(), refused))`;
  with `hosting`, those options as given (WP-LIVE: github.com, no loopback filter); with neither,
  `HostingOptions.Off with { CreateHandler = () => new LoopbackOnlyHandler(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }, refused) }`.
  `ShellDriver.Phase7a.cs`: `Tokens`, `GitHub`, `Hosting` (the options passed), `RefusedRequests`, and
  `SignInAsync(token)`: `Session.Services.Hosting.SignInAsync`; anything but `SignedIn` throws with the
  failure's kind and message, never the token; then `Settle()`. It throws until WP-S's merge.
- **`LoopbackOnlyHandler : DelegatingHandler`**: a host that parses as a loopback IP address passes
  (`localhost` does not); anything else is recorded and throws
  `HttpRequestException("The test harness lets only loopback requests through.")`.
- **`FakeTokenStore`**: `Name` `fake`, `KeepsTokensAcrossRestarts` true; `bool Available { get; set; } = true`;
  `ProbeAsync` → `(Available, null or "The fake keychain is set unavailable.")`; `SaveAsync` `Failed`
  when unavailable or once after `FailNextSave(string reason)`; `ReadAsync`, `DeleteAsync` `Done` or
  `NotFound`; `IDisposable HoldNext()` holds the next call; `IReadOnlyList<string> Calls` like
  `save github:127.0.0.1:47395:octo`, never a token; `bool Holds(TokenKey)`, `void Remove(TokenKey)` (a
  token gone from the keychain). Thread-safe; one instance across two starts is the scripted restart.
- **`TokenTripwire.AssertAbsent`**: every file under the folders, `.git` included, searched for the
  token as UTF-8, UTF-16LE, `Uri.EscapeDataString(token)`, base64 of `x-access-token:<token>` (ruling
  7) and base64 of the token's UTF-8 (the macOS store's form, 3.5); and each `GitCallRecord`'s `CommandText`, `StandardOutput`, `StandardError`. A find
  names the file or command and the form, never the token.
- **`TempRepo.CommitFileAsAsync(path, content, message, authorName, authorEmail)`**: `CommitFileAsync`
  with that author; the clock advances.

### 2.6 The scenario "hosting" (`Scenarios.Phase7a.cs`)

`Scenarios.HostingAsync()`: a copy (`TempRepo.CopyAsync`, one git process) of a template built once
per process through `TemplateAsync("hosting", …)` (`Scenarios.cs:327`), about 50 git processes.
Commits a minute apart from 2026-01-01 12:00 by Octo Cat `octo@example.com`, Ada Lovelace
`ada@example.com` and TempRepo's Test Author `author@example.com` (unknown to GitHub). Every part
uses it (0.5). State a part's R picture depends on is made by `PartX.PrepareAsync(TempRepo hello)` in
its linked `Phase7aExpectations.<X>.cs`, called by both projects on the copy before the app opens
(11.5, 12.8); the rest goes in the part's own helpers.

- `github/octo/hello.git` (private): `main` 12:00 "Initial commit" (Octo), 12:01 "Add the greeting"
  (Ada), 12:02 "Add the docs folder" (Test Author); one commit each on `main`: `feature/login` 12:03
  "Add the login form" (Ada), `docs/readme` 12:04 "Update the readme" (Octo),
  `feature/a-very-long-branch-name-that-the-graph-must-cut` 12:05 "Shorten long labels" (Octo),
  `topic/no-pr` 12:06 "Try an idea" (Test Author); `refs/pull/1..5/head` written.
- `github/ada/hello.git` (public, a fork of octo/hello by ada): `fix-typo` 12:11 "Fix a typo in the
  greeting" (Ada), fetched into octo/hello's `refs/pull/5/head`.
- `github/acme/tool.git` (public, organisation acme): `main` 12:07 "Initial commit" (Ada); its fork
  `github/octo/tool.git` adds `fix-typo` 12:08 "Fix a typo in the readme" (Octo), fetched into acme's
  `refs/pull/7/head`; a fork `gone/tool` with `idea` 12:09 "An old idea" (Ada), fetched as
  `refs/pull/8/head`, then its folder deleted.
- `github/octo/notes.git` (private, empty), `github/octo/website.git` (public, archived, empty).
- `hello/` (the template's `TempRepo`): `main` tracking `origin/main`, `feature/login` tracking its
  remote branch, local-only `local-work` 12:10 "Local change" (Test Author); on `main`, clean.
- `tool/`: clone of `octo/tool`, `upstream` `acme/tool`, `fix-typo` tracking `origin/fix-typo`, on `main`.
- Remote URLs written last as `http://127.0.0.1:47395/<owner>/<name>.git` (D265).

`Scenarios.ServeHostingAsync(TempRepo hello, int port = AuthRemoteServer.Phase7aVisualChecksPort, …)`
→ `ServedHosting : IAsyncDisposable` (`GitHub`, `Hello`, `Tool`, `OctoToken`, `OctoClassicToken`,
`AdaToken`): the fake on `<copy>/github` with fallback; users octo 1001 "Octo Cat", acme 1002
(organisation) "Acme", ada 1003 "Ada Lovelace"; tokens `github_pat_FAKE-octo-1`, `ghp_FAKE-octo-2`,
`github_pat_FAKE-ada-3`; the six repositories (`UpdatedAt` hello `HarnessTime`, tool an hour, notes
a day, website 30 days, ada/hello 31 days before; `acme/tool`'s pusher ada); octo/hello #1 "Add the
login form" (ada, `feature/login`), #2 "Update the readme" (octo, draft), #3 "Shorten long labels"
(octo), #4 "Try an idea" (octo, closed), #5 "Fix a typo in the greeting" (ada, from `ada/hello`);
acme/tool #7 "Fix a typo in the readme" (octo, from `octo/tool`), #8 "An old idea" (ada, from
`gone/tool`, deleted); `LinkEmail` for octo and ada. It runs `git remote set-url` in the clones only
when the port is not 47395. Only the 7a walk-through and its real-window twin bind 47395 (D265).

### 2.7 The phase as a string (ruling 25, 0.3)

- `VisualRun.FolderFor(string)`, `Save(this Screenshot, string, string)`; the `int` forms (`:37-38`,
  `:57-69`) delegate; `PhasesStarted` (`:33`) a `HashSet<string>`.
- `Screens.RealWindowFolder(string)`, `ScriptedFile(string, string)`, `Save(this Bitmap, string, string)`
  (`:20-33`) with delegating `int` forms; `RealWindowRun(string)` with `Phase` a `string`, and
  `RealWindowRun(int)` delegating (`new(phase: 1)`, `Phase1RealWindowPass.cs:15`, still binds).
- `scripts/time-scripted-check.sh` lines 34 to 56: `phase="${2:-7a}"` and `case "$phase" in 7a) …Phase7a ;;
  6) <today's L split> ;; 5) <U split> ;; 4) ;; 3) ;; 0|1|2) …VisualTests ;; *) echo "unknown phase: $phase" >&2; exit 2 ;; esac`
  before `dotnet build`; usage and comment say `7a`; in the 7a project's commit.
- Real-window csproj after `:63`: `<Compile Include="..\VisualCommit.VisualTests.Phase7a*\Phase7aExpectations*.cs" Link="Phase7a\%(Filename)%(Extension)" />`,
  with phase 6's comment shape.

### 2.8 The project `tests/VisualCommit.VisualTests.Phase7a`

- csproj and `AssemblyInfo.cs` as phase 6's, plus links to `tests/VisualCommit.App.Tests/RecordingActions.cs`
  and `RecordingLog.cs` (internal), so menu tests live outside App.Tests (0.6); `Phase7aChecks.cs`:
  "what is where", `public abstract partial class Phase7aChecks`.
- `Phase7aChecks.Support.cs`: `protected const string Phase = Phase7aExpectations.Phase`; copied from
  `Phase6Checks.Support.cs` with 7a's names: `Shot` and its picture and timing helpers (`:855-886`,
  `:1651-1697`; timings in `artifacts/visual/phase-7a/timings/`), the menu, dialog, status,
  clipped-text and tooltip assertions (`:888-1014`, `:1545-1566`), the call assertions `CallsSince`,
  `AssertCalled` and `AssertCalledInOrder` (`:1434-1461`), taking a `long` mark from `QuietCallMarkAsync`
  in place of the `int` count and reading `Sequence > mark` (a doc comment: presence and order only; a
  count goes through `GitCallBudget`, 5.3), and the picture pattern of 0.3. Shared helpers live here only.
  `ServeAsync()` (a copy served on 47395); `OpenAsync(TempDirectory data, ServedHosting served, string repository = "hello", bool signIn = true,
  FakeTokenStore? tokens = null, int width = 1100, int height = 700, AppTheme theme = AppTheme.Dark,
  bool systemDark = true)`: one tab, `gitHub:`, waits for the repository, signs in with `OctoToken`
  when asked and then awaits `app.WaitForWatchersQuietAsync()` (the sign-in's URL-rewrites read
  ends inside it, 5.3), rests the pointer.
- `Phase7aExpectations.Common.cs`: `public const string Phase = "7a"`, sizes, `ServerPort`, `WithPort`
  (as `Phase6Expectations.Common.cs:170`), the scenario's names and numbers; BCL, Avalonia, Xunit,
  Core, Git and Testing only (the pass compiles it). Parts add their own files.

No `[AvaloniaFact]` of one assembly overlaps another, so a fake per check on 47395 is safe; plain
`[Fact]`s use port 0.

### 2.9 Tests (the builder runs these classes only, D245)

- `AuthRemoteServerTests`: `:414-418` pins 47395 too; two levels with `Serve` after the start; a
  `.git`-less clone; a claimed path answered before auth, an unclaimed one still 404; token mode
  records login and `GivenUserName`; public fetch without credentials, its push refused; private
  asks; `MayPush` false → 403; `HoldNextRequest("octo/hello")`.
- `VisualRunTests`: `FolderFor("7a")` ends `artifacts/visual/phase-7a/scripted`; `FolderFor(6) == FolderFor("6")`.
- `Phase7aScenarioTests` (Git.Tests): 2.6's layout, authors, upstreams, URLs and `refs/pull`; a copy
  is one git process; served on port 0 the URLs rewritten; in process octo/hello lists #5, #3, #2
  (draft), #1, acme/tool #8 (head repository null), #7; octo's `user/repos?sort=updated`: hello,
  tool, notes, website.
- `FakeGitHubTests` (Hosting.Tests, in process): every route, check, header and fault of 2.4 (each
  fault once, then normal); `HoldNext` and `Arrived`; A5's ref and refusals; A6 and 409; avatars;
  `AddListedRepositories(…, 1001)` paged with `MoreExist` at ten pages.
- `FakeGitHubGitTests` (Hosting.Tests, port 0): `octo/hello` cloned with `x-access-token` and the
  token in the URL (the fake's own test); revoked → refused; ada's push to `octo/hello` → 403;
  anonymous clone of `acme/tool`; `git fetch origin refs/pull/7/head` in a clone writes only `FETCH_HEAD`.
- `FakeTokenStoreTests` (Hosting.Tests): 2.5's members; one instance across two "starts".
- `Phase7aSupportTests`: the process holds 1.8's safety nets (`none`, `memory`, the token and live
  variables absent); a start without `gitHub` is off with `RefusedRequests` empty; with a fake on port
  0, `Hosting.CreateHandler()` refuses and records `https://example.com/` and passes the fake's
  `api/v3/user`; `hosting:` with `gitHub:` throws; `TokenTripwire` finds each form in a file and a
  call and passes clean ones; `WithPort`; the picture pattern; the phase folder.

Also: build `tests/VisualCommit.RealWindowTests` (compile only); `bash scripts/time-scripted-check.sh x 7`
exits 2 before building.

Commits: (1) the phase as a string; (2) `AuthRemoteServer`; (3) `FakeGitHub*`, `FakeGitHubHandler`;
(4) `FakeTokenStore`, `LoopbackOnlyHandler`, `TokenTripwire`, `ShellDriver`; (5) `CommitFileAsAsync`
and the scenario; (6) the 7a project, slnx line, real-window link, the timing script's default; each
with its classes.

### 2.10 At the merge; done when

The lead: CLAUDE.md's "One phase's scripted checks" and "Scripted visual walk-through only" gain
`dotnet test --project tests/VisualCommit.VisualTests.Phase7a` (one part: `--filter-class
"*Phase7aChecksA"`); "Time scripted checks alone": `7a` when left out, an unknown phase exits 2. Done
when the seven classes pass, the real-window project builds, the 7a project is in the solution, the
script defaults to `7a` and refuses `7`, `AuthRemoteServerTests` pins 47391 to 47395, no picture of phases 0 to 6 changed, and CI's five jobs are green with each Test step under
four fifths of its limit, the containers' included (0.6).

## 3. WP-K: keychain stores and the vault

### 3.1 Goal and files

A GitHub token is kept in this computer's keychain and never in a file (R3); where no keychain can be
reached, or a save fails, it is kept in memory for the session (D253, rulings 18, 19). Carries the
storage half of the sign-in item and "Real stores of macOS and Linux on CI" (D259); rulings 6, 17,
20, 21; D268 to D272, D333. Status step 7, wave 1, branch `wip/p7a-keychains`. Out: `HostingSession` and
`accounts.json` (WP-S); every shown text (WP-A); `FakeTokenStore` (WP-F); `ci.yml`, `docs/` (lead).

| File | Change |
|---|---|
| `src/VisualCommit.Hosting/VisualCommit.Hosting.csproj` | `<PackageReference Include="Tmds.DBus.Protocol" />` (version: WP0's line) |
| `Tokens/ITokenStore.cs` (WP0's) | `FromEnvironment`'s unset and unknown branches (3.7); `MemoryTokenStore`'s names |
| `Tokens/TokenEntryNames.cs`, `Tokens/TokenStoreReasons.cs` | new |
| `Tokens/WindowsCredentialStore.cs`, `Tokens/WindowsCredentialNative.cs` | new |
| `Tokens/MacKeychainStore.cs`, `Tokens/TokenProcessRunner.cs` | new |
| `Tokens/SecretServiceStore.cs`, `Tokens/SecretServiceAddress.cs`, `Tokens/SecretServiceCalls.cs` | new |
| `Tokens/TokenVault.cs` | new (1.6's signature) |
| `scripts/setup-linux.sh` | `dbus`, `gnome-keyring` |
| `tests/VisualCommit.Hosting.Tests/Tokens/*.cs` | 3.9's classes |

### 3.2 Rules for every store

- `ProbeAsync` never prompts and never unlocks, creates or reads an entry; it is bounded.
- Constructors do nothing (no process, P/Invoke or D-Bus connection), so `FromEnvironment` costs
  nothing and a store is touched only when the vault first needs it.
- An expected failure (not found, locked, refused, too long, time limit) is a result, never an
  exception. A store honours its cancellation token at every wait; the vault gives none once a save
  or read has started, so no entry is left half-written (D272).
- No token, nor its base64, in a log line, exception message, process argument, environment variable,
  file or `ToString()` (`TokenRead`'s and `TokenProcessResult`'s included); byte buffers that held a
  token are cleared after use.
- Each store logs its platform detail itself (the Win32 error, `security`'s exit code and first error
  line, the D-Bus error name): Warning for a failure, Debug for a success; the key as
  `{host}/{account}`. `Reason`s carry only a `TokenStoreReasons` text (D268), so WP-A may show one.
- No call blocks its caller's thread (Q2): Windows' calls run on the thread pool.

### 3.3 Entry names and reasons

`TokenEntryNames.For(TokenKey key, string application = "VisualCommit")` (r6 7). `TokenKey`'s parts
are lower-case; provider `github` is shown as `GitHub` (a map; 7b adds its own). For
`TokenKey("github", "github.com", "octocat")`:

| Store | Field | Value |
|---|---|---|
| Windows | target, user, comment | `VisualCommit:github:github.com:octocat`, `octocat`, `GitHub token for octocat, kept by VisualCommit` |
| macOS | service `-s`, account `-a`, label `-l` | `VisualCommit:github:github.com`, `octocat`, `VisualCommit: GitHub (octocat)` |
| Linux | attributes; label | `application=VisualCommit`, `provider=github`, `host=github.com`, `account=octocat`, `xdg:schema=io.github.rahimpasha.VisualCommit.Token`; `VisualCommit: GitHub token for octocat` |

The fake's host gives `VisualCommit:github:127.0.0.1:47395:octocat`. Real-store tests pass
`application = "VisualCommit-test:" + Guid.NewGuid()`. A name never starts with `git:` or `gh:`, so
the app never touches an entry of Git Credential Manager or GitHub CLI. A key part that is empty or
holds a control character, `'`, `\` or a space is refused before any call (`Failed`, `Refused`).

`TokenStoreReasons` (D268): `NoKeychain` "no keychain was found on this computer" (probe: none
reachable); `CannotUse` "the keychain cannot be used in this session" (Windows error 1312 or a
session type below local machine; macOS "User interaction is not allowed"); `Locked` "the keychain
stayed locked" (an unlock prompt dismissed: macOS "User canceled the operation", Secret Service
`dismissed` or `IsLocked`); `NoAnswer` "the keychain did not answer in time"; `TooLong` "the token
is too long for the keychain" (Windows, over 1,280 characters); `Unreadable` "the keychain's entry could not be read" (not valid base64 or UTF-8); `Refused` "the
keychain refused it" (anything else).

### 3.4 Windows (`WindowsCredentialStore`, `Name` "Windows Credential Manager")

- `WindowsCredentialNative` (`[SupportedOSPlatform("windows")]`, internal): `[DllImport("advapi32.dll",
  SetLastError = true, CharSet = CharSet.Unicode)]` in the style of `src/VisualCommit.Git/WindowsJob.cs:68-79`:
  `CredWriteW(ref CREDENTIALW, uint)`, `CredReadW(string, uint, uint, out IntPtr)`, `CredDeleteW(string,
  uint, uint)`, `CredFree(IntPtr)`, `CredGetSessionTypes(uint, [Out] uint[])`; `bool` returns with
  `[return: MarshalAs(UnmanagedType.Bool)]`; `CREDENTIALW` sequential, Unicode, `LastWritten` as
  `System.Runtime.InteropServices.ComTypes.FILETIME`.
- `WindowsCredentialStore(IAppLog log, string application = "VisualCommit")`; names from
  `TokenEntryNames.For(key, application)`. The store has no platform attribute. Save first asks
  `internal static bool Fits(string token)` (`token.Length <= 1280`: at most 2,560 UTF-16LE bytes,
  `CRED_MAX_CREDENTIAL_BLOB_SIZE`) and returns `Failed(TooLong)` when it is false; then every call
  checks `OperatingSystem.IsWindows()` (false: `NoKeychain`), then calls the native class.
- Save: generic type (1), `CRED_PERSIST_LOCAL_MACHINE` (2), blob the UTF-16LE bytes in
  `Marshal.AllocHGlobal` memory, cleared and freed in `finally`; `CredWriteW` replaces an entry of the
  same target.
- Read: `CredReadW`; 1168 (`ERROR_NOT_FOUND`) `NotFound`; else copy `CredentialBlobSize` bytes,
  decode, clear the copy, `CredFree` in `finally`. Delete: `CredDeleteW`; 1168 `NotFound`.
- Probe: `CredGetSessionTypes(7, persist)`; available when `persist[1] >= 2`, else `CannotUse`
  (the Warning gives `persist[1]`). Error 1312 on any call is `CannotUse`.

### 3.5 macOS (`MacKeychainStore`, `Name` "macOS keychain")

`MacKeychainStore(IAppLog log, ITokenProcessRunner? runner = null, string? keychainFile = null, string application = "VisualCommit")`.
`ITokenProcessRunner.RunAsync(string fileName, IReadOnlyList<string> arguments, string? standardInput, TimeSpan limit, CancellationToken)`
→ `TokenProcessResult` (a sealed class, not a record: a read's `StandardOutput` is the token in base64;
`int? ExitCode`, `string StandardOutput`, `string StandardError`; `ExitCode` null when stopped at the
limit; `ToString()` gives the exit code only). The default runner: `ArgumentList`, three streams
redirected, UTF-8, the process tree killed at the limit or on cancel, standard input and output never
logged. The program is always `/usr/bin/security`, by full path.

- Save: `security -i`, standard input one line, then closed:
  `add-generic-password -U -s '<service>' -a '<account>' -l '<label>' -w '<base64>'` (and ` '<file>'`
  with a keychain file), `\n`. `<base64>` is the token's UTF-8 in base64, so the line holds only
  `[A-Za-z0-9+/=]` inside the quotes whatever was pasted; a file path holding `'` is refused. Limit
  60 s (macOS may show its own unlock dialog). `security -i`'s exit code on a failed command is not
  known (r6), so the entry is read back and compared; a mismatch is `Failed(Refused)`.
- Read: `find-generic-password -s <service> -a <account> -w [<file>]`, limit 60 s. Exit 0: one
  trailing `\n` removed, strict base64, strict UTF-8 (`Unreadable` otherwise). Exit 44, or `could not
  be found` in standard error: `NotFound`. Delete: `delete-generic-password -s … -a … [<file>]`; 0
  `Done`, 44 `NotFound`.
- Errors, of every command: the limit `NoAnswer`; `User interaction is not allowed` `CannotUse`;
  `User canceled the operation` (an unlock dialog dismissed, errSecUserCanceled) `Locked`; else
  `Refused`, logging the exit code and standard error's first line. A save whose `security -i` writes
  one of the two texts to standard error ends with that reason, without the read-back.
- Probe: `/usr/bin/security` missing: `NoKeychain`. With a keychain file: available when it exists.
  Else `security default-keychain -d user`, limit 5 s, available on exit 0 with output.
- Exposure (ruling 20): an item made through `security` trusts `/usr/bin/security`, so any process of
  the user can read it without a prompt, as on Windows and Linux. The lead records the plan risk.

### 3.6 Linux (`SecretServiceStore`, `Name` "Secret Service")

`SecretServiceStore(IAppLog log, SecretServiceOptions? options = null, string application = "VisualCommit")`.
`SecretServiceOptions`: `Func<string, string?> ReadEnvironment`, `Func<string, bool> SocketExists`,
`Func<string, DBusConnectionOptions> ConnectionOptions` (default `address => new
DBusConnectionOptions(address) { AutoConnect = false }`), `SecretServiceLimits` (`Connect` 2 s,
`NameQuery` 5 s, `OpenSession` 10 s, `Call` 25 s, `Prompt` 120 s). Tmds.DBus.Protocol 0.94.1:
`DBusConnection` (`ConnectAsync()`, `CallMethodAsync`, `AddMatchAsync`, `GetMessageWriter()`),
`MessageWriter.WriteMethodCallHeader(destination, path, interface, member, signature, flags)`,
`DBusErrorReplyException.ErrorName`. `ConnectAsync` takes no token: each step is bounded with
`WaitAsync(limit, token)` and the connection disposed when it runs out.

- **Address** (`SecretServiceAddress.Resolve(readEnvironment, socketExists)`, pure): a non-empty
  `DBUS_SESSION_BUS_ADDRESS`; else `unix:path=<XDG_RUNTIME_DIR>/bus` when that socket exists (bytes
  other than `[-0-9A-Za-z_/.\*]` escaped as `%xx`); else none. Never `DBusAddress.Session`, whose X11
  fallback this store must not try.
- **Probe** (r6 4): no address, unavailable at once; connect (`Connect`); `NameHasOwner
  ("org.freedesktop.secrets")`, when false `ListActivatableNames` must hold it (`NameQuery` each);
  `OpenSession("plain", <variant "">)` (`OpenSession`; `NotSupported` or the limit: unavailable); then
  `Session.Close`. Every unavailable result carries `NoKeychain`, with the detail logged. Never
  `ReadAlias`, `Unlock` or `CreateItem`, so no dialog.
- **Each operation**: one connection and one `plain` session, closed in `finally`; calls
  `Call`-bounded (D271).
  - Read: `Service.SearchItems` (the four attributes, not `xdg:schema`) gives (unlocked, locked);
    none: `NotFound`. Only locked: `Service.Unlock(locked)` and its prompt. `Service.GetSecrets([first],
    session)`; the value as UTF-8 (`Unreadable` otherwise), bytes cleared. More than one match: the
    first, the count logged at Debug.
  - Save: the collection is `Service.ReadAlias("default")`; when `/`,
    `/org/freedesktop/secrets/collection/login` if its `Locked` can be read; else
    `Service.CreateCollection({Label: "Login"}, "default")` and its prompt. When `Locked`, `Unlock`
    and its prompt. `Collection.CreateItem({Label, Attributes (all five)}, (session, [], utf8,
    "text/plain; charset=utf-8"), replace: true)` and its prompt; an item path `/` after the prompt
    is read from `Completed`'s result.
  - Delete: `SearchItems`; unlock the locked ones; `Item.Delete()` on every match, each with its
    prompt. None: `NotFound`.
  - **Prompts** (a path other than `/`): `AddMatchAsync` for `Completed` on
    `org.freedesktop.Secret.Prompt` at that path before `Prompt.Prompt("")`; wait (`Prompt` limit,
    the caller's token). `dismissed`: `Failed(Locked)`. The limit or a cancel: `Prompt.Dismiss()` as
    a best effort, then `NoAnswer` (or the cancel). The match is removed in `finally`.
  - Errors: `org.freedesktop.Secret.Error.IsLocked` `Locked`; any other reply error `Refused`, logged
    with its name; a limit `NoAnswer`.

### 3.7 Memory store, the choice, the vault

- `MemoryTokenStore(true)`: `Name` "memory", available; `(false)`: "memory, no keychain", probe
  unavailable with `NoKeychain`, every save `Failed(NoKeychain)`. Both: a dictionary of the process;
  `KeepsTokensAcrossRestarts` false.
- `TokenStores.FromEnvironment`: unset or empty gives `WindowsCredentialStore` on Windows,
  `MacKeychainStore` on macOS, `SecretServiceStore` on Linux, `MemoryTokenStore(false)` elsewhere. An
  unknown value: `MemoryTokenStore(true)` and a Warning ``VISUALCOMMIT_KEYCHAIN=<value> is not known;
  tokens are kept in memory.`` (D269). Otherwise one Info line, `Token store: <Name>.`
- `TokenVault(ITokenStore store, IAppLog log)` (D272). State under one lock: `memory` (key to
  token), `reads` (key to the store read's task, kept for the run), the probe task.
  - `KeychainAvailableAsync(ct)`: the store's probe, started once per run without the caller's token
    and awaited with `WaitAsync(ct)`. Logs once: `Keychain: available.` or `Keychain: not available
    (<detail>).`
  - `KeepAsync(key, token, ct)`: empty token, `ArgumentException`; `ct` checked first. The token goes
    into `memory` at once and the key's read record is dropped. Probe unavailable: `(Session,
    NoKeychain)`. Else `SaveAsync`, which the caller's token no longer stops: `Done` gives
    `(Keychain, null)`; anything else `(Session, reason)`, and the store is not touched again (an
    older entry may stay; read at a later start, its token is refused at first use, ruling 4). Logs
    `Token for <host>/<account> kept in the keychain.` (Debug) or `… kept for this session: <reason>.` (Info).
  - `ReadAsync(key, ct)`: `memory` first (`Done`); else the key's recorded read, or a new one shared
    by concurrent callers: probe unavailable `Failed(NoKeychain)`; else `store.ReadAsync`, `Done`
    putting the token in `memory`. Every outcome is kept for the run: the store is read at most once
    per key and run (ruling 6).
  - `ForgetAsync(key, ct)`: removes `memory` and the read record at once. Probe unavailable: null.
    Else `DeleteAsync`: `Done` or `NotFound` null; `Failed` its reason, logged as a Warning naming
    the entry (`VisualCommit:github:github.com:octocat`) for whoever removes it by hand.

### 3.8 `scripts/setup-linux.sh`

In the package block (`:75-80`), after `ssh-keygen`'s line:
`command -v dbus-run-session >/dev/null 2>&1 || packages+=(dbus)` and
`command -v gnome-keyring-daemon >/dev/null 2>&1 || packages+=(gnome-keyring)`; the comment list
(`:58-66`) gains `dbus, gnome-keyring  the Secret Service store's tests run under a private bus (D259)`.
A second run installs nothing (CI's "Run the script again"). The builder checks on CI's bare container
that `apt-get --no-install-recommends gnome-keyring` brings what `gnome-keyring-daemon --unlock`
needs; a missing package goes on the same line.

### 3.9 Tests (`tests/VisualCommit.Hosting.Tests/Tokens/`)

None touches a keychain unless `VISUALCOMMIT_KEYCHAIN_TESTS` names it (D270). No class without the
skip makes a platform store's call that can reach `advapi32`, `/usr/bin/security` or a session bus:
a store is only constructed (its constructor does nothing, 3.2), or its call stops before the
platform (the length check, `NoKeychain` off Windows, an empty address, a missing socket) or goes to
a test double (a recording runner, a silent listener). A `MemoryLog : IAppLog` records lines; every class asserts the token's text in no line
(`MacKeychainStoreTests` also its base64).

| Class | Proves |
|---|---|
| `TokenEntryNamesTests` | `TokenKey.Of` lower-cases; each store's names for a key, a fake host with a port and the test prefix; no `git:` or `gh:`; refused key parts |
| `TokenVaultTests` (a scripted store with held calls) | keep with and without a keychain (no save call without); a failed save keeps the token readable with its reason; memory first; one store read per key and run, also after `NotFound` and `Failed`; two concurrent reads, one call; a cancel before the save keeps nothing, after it is ignored; forget clears both and returns a failed delete's reason; one probe per run |
| `TokenStoresTests` | unset gives the platform's type (no call made); `memory`; `unavailable`; an unknown value gives memory and the Warning; one log line each |
| `WindowsCredentialStoreTests` | `Fits`: true for 1,280 characters, false for 1,281, on every system; `SaveAsync` with 1,281 characters `TooLong` on every system (refused before any call); off Windows every call `NoKeychain`, a 1,280-character save among them; on Windows no call is made but the 1,281-character save. The real 1,280-character save is `WindowsTokenStoreRealTests`' alone |
| `MacKeychainStoreTests` (a recording runner) | `/usr/bin/security` by full path; `-i` and its exact stdin line in base64 (a token holding `'`, a space and a line feed); the read-back; read and delete arguments with and without a file; exit 44, "could not be found", the limit, "User interaction is not allowed", "User canceled the operation" (`Locked`, in a read and in a save, which then makes no read-back); a read-back mismatch; the token in no argument; a read's `TokenProcessResult.ToString()` holds neither the token nor its base64 |
| `SecretServiceStoreTests` | `Resolve`'s cases and escaping; probe without an address at once; a missing socket within `Connect` plus 2 s; a silent bus (a `DBusConnectionOptions` subclass whose `SetupAsync` returns a `TcpClient` stream to a listener that never writes; limits 300 ms) within `Connect` plus 2 s. If `SetupAsync` cannot be overridden from outside, the silent bus is a Unix socket and that case runs on macOS and Linux only |
| `WindowsTokenStoreRealTests`, `MacKeychainTokenStoreRealTests`, `SecretServiceTokenStoreRealTests` | `Assert.SkipUnless(TokenStores.TestsVariable names windows / macos / secret-service, …)` first (xunit.v3 3.2.2). Under `VisualCommit-test:<guid>` with a dummy `vc-test-<guid>`: probe available; read `NotFound`; save; read equal; save another; read equal; delete `Done`; read `NotFound`; delete `NotFound`; delete again in `finally`. A failed assertion's message quotes the `MemoryLog` lines (the platform's detail, such as the Win32 error). Windows also saves 1,280 characters. macOS makes its own keychain (`security create-keychain -p <random> <temp>/vc-test.keychain-db`, `set-keychain-settings`, `unlock-keychain -p`) and runs `security delete-keychain` in `finally`; the login keychain is never named |

The builder runs only these classes, for example `dotnet test --project tests/VisualCommit.Hosting.Tests
--filter-class "*TokenVaultTests"`; on a Linux machine also
`dbus-run-session -- sh -c 'echo pw | gnome-keyring-daemon --unlock --components=secrets >/dev/null && VISUALCOMMIT_KEYCHAIN_TESTS=secret-service dotnet test --project tests/VisualCommit.Hosting.Tests --filter-class "*SecretServiceTokenStoreRealTests"'`,
with `XDG_DATA_HOME` an empty folder holding `keyrings/` (mode 700). Never on the laptop with the
variable set to `windows`. No visual checks of its own: part A shows the vault through
`FakeTokenStore`.

Commits: (1) `TokenEntryNames`, `TokenStoreReasons` and their class; (2) `TokenVault` and its class;
(3) Windows; (4) macOS and the runner; (5) Linux and the csproj's reference; (6) `FromEnvironment`;
(7) `setup-linux.sh`.

### 3.10 Handed to the lead; done when

- **CI** (`.github/workflows/ci.yml`): both jobs' `Build` step gains `id: build`. Each keychain step
  follows `Test`, `timeout-minutes: 5`, with an `if:` that still holds after a failed `Test`, so that
  one failing test does not hide the keychain's result: `build-and-test` with
  `if: ${{ !cancelled() && steps.build.outcome == 'success' && runner.os == 'Windows' }}`, env
  `VISUALCOMMIT_KEYCHAIN_TESTS: windows`,
  `dotnet test --project tests/VisualCommit.Hosting.Tests --configuration Release --no-build --filter-class "*WindowsTokenStoreRealTests"`;
  the same for `macOS` (`macos`, `*MacKeychainTokenStoreRealTests`); `bare-linux-container` (both),
  `if: ${{ !cancelled() && steps.build.outcome == 'success' }}`, env `secret-service`:
  `export XDG_DATA_HOME="$(mktemp -d)"; mkdir -p -m 700 "$XDG_DATA_HOME/keyrings"`
  then `dbus-run-session -- sh -c 'echo "throwaway-$GITHUB_RUN_ID" | gnome-keyring-daemon --unlock --components=secrets >/dev/null && dotnet test --project tests/VisualCommit.Hosting.Tests --configuration Release --no-build --filter-class "*SecretServiceTokenStoreRealTests"'`.
  Then read each job's keychain step.
- **Windows' runner** (the lead's ruling, D333): `WindowsTokenStoreRealTests` is tried in CI's Windows
  job. If the runner refuses it with error 1312 (no logon session; or the probe's session type below
  local machine, which shows the same lack first; both `CannotUse`, the detail in the step's output,
  3.9), the class is taken off that job's list (the step leaves `ci.yml`) and the phase's report
  records it. It is never skipped
  silently on that job: the class asserts the probe available, so a refusal fails the step.
- **Plan risk** (ruling 20): "macOS keeps the token as a login-keychain item made by
  `/usr/bin/security`; any process of the user can read it through `security` without a prompt, as
  on Windows and Linux. Accepted until phase 8 signs the app, which may move to Security.framework
  for a per-app access list."
- **CLAUDE.md Commands**: the Secret Service command above, once it has worked.

Done when the classes pass on the laptop with `VISUALCOMMIT_KEYCHAIN_TESTS` unset (the real classes
skipped); CI's macOS and both container jobs pass their keychain step with their real store (the
step runs also after a failed `Test`); the Windows job passes its own or, refused as above, no longer
has it and the report says so; `setup-linux.sh`'s second run in both containers installs nothing; no
token text appears in any log line of any test.

## 4. WP-C: credentials into git, push guard, prompt group

### 4.1 Goal and files

The app's own git calls to a bound GitHub URL take their credentials from the signed-in account
through the app's helper, and from nothing else (D255, rulings 1, 2 and 4 to 9, 0.2; D273 to D279,
D329, D332). Carries
"The account's token on the app's git calls to github.com", phase 3's "the token's way into git",
phase 6's push guard and prompt group per account. Status step 9, wave 1, branch
`wip/p7a-credentials`. Out: `IGitCredentialSource`'s implementation and recognition (WP-S, WP-G);
which repository is bound and when (WP-S, through `Select`); the clone's binding (WP-B);
`OperationQueue.cs` (WP0's lines).

| File | Change |
|---|---|
| `src/VisualCommit.AskPass/AskPassProtocol.cs` | request kind, git's input, answer text (4.5) |
| `src/VisualCommit.AskPass/Program.cs` | `Main` (`:15-26`) sends `--vc-credential` to `CredentialMode` first |
| `src/VisualCommit.AskPass/CredentialMode.cs` | new |
| `src/VisualCommit.Git/Prompts/AskPassBroker.cs` | WP0's constructor's body; `Begin` (`:94-107`), `EnvironmentFor` (`:134-158`), `ServeAsync` (`:229-257`), `Scope.AskAsync` (`:314-373`) |
| `src/VisualCommit.Core/Operations/CredentialBinding.cs` (WP0's) | `Select` (4.4) |
| `src/VisualCommit.Core/Git/UrlRewrites.cs` (WP0's) | `Parse`, `Apply` |
| `src/VisualCommit.Git/GitRepository.Hosting.cs` (WP0's) | `ReadUrlRewritesAsync` |
| `src/VisualCommit.Core/Git/GitCommand.cs`; `src/VisualCommit.Git/GitRunner.cs` | an authority ends at a control character too (4.3): `ReplaceCredentials` (`:94-127`), `OutputCollector.RecordedOutput` (`:656-659`) |
| `src/VisualCommit.Core/Git/FetchRefspecs.cs` | `SourceOf` overload beside `:269-282` |
| `src/VisualCommit.App/ViewModels/Operations/RepositoryOperations.Remotes.cs` | guard `:202-220` |
| `.../Operations/RepositoryOperations.Run.cs` | the local `bindings` before `:344`, which WP0's `:383` then assigns (4.7); `ShowResultAsync` `:606-641`, `DeferredOf` `:674-688` |
| `.../Operations/AutoFetchScheduler.cs` | `CreateOperation` `:183-200`; call `:160` (WP0's, kept) |
| `.../Operations/OperationTexts.Hosting.Credentials.cs` | new |
| `src/VisualCommit.App/Services/PromptDialogs.cs` | `CreateBroker` `:86-91` (WP0's signature), group keys `:253-284`, `:327-335` |
| `src/VisualCommit.App/ViewModels/Workspaces/BulkRun.cs` | after `row.Result = result;` (`:432`) |
| `tests/VisualCommit.Testing/AskPassRunner.cs`; `RecordingCredentialSource.cs` | `RunCredentialAsync` (action, stdin, environment); new |
| `tests/VisualCommit.App.Tests/OperationsHarness.cs` | `OpenAsync` (`:130-148`) gains `IHostingSession? hosting = null`, passed to `TabServices`; the file passes to WP1 at the merge |
| `tests/VisualCommit.App.Tests/HostingSessionStub.cs` | new: `ForRepository` with settable `Bindings`, settable `BindsGit` with `BindingsFor` binding the given URLs to the stub's account through `Select`, a `RecordingCredentialSource`, `SourceCanExist` applying ruling 8 to a set of URLs |

### 4.2 Git facts (2.56; isolated, with `HOME` there too)

- A per-call `credential.<url>.helper` empty value clears every helper collected for a matching URL,
  the user's own URL-scoped ones too (the host's, or one more specific: with a user, a deeper path);
  the helper after it is the only one asked (`git credential fill`, five cases).
- `credential.<url>` keys match: scheme exactly; host without case; `:443` the same as none; the path
  as a prefix ending at `/` (`…/hello.git` matches `…/hello.git/info/refs`, not `…/hello` nor
  `…/hello.gitx`); the path's case counts. A key without a user matches a URL with any user
  (`alice@` too); a key with a user, only that user. `git config --get-urlmatch` agrees; URL matching
  dates from 1.8.5, so 2.43 and 2.55 should behave alike (not run there).
- The helper's input on `get` without `useHttpPath`: `protocol`, `host` as written (`GitHub.com`,
  `github.com:443`, `127.0.0.1:47395`), `username` when the URL has one; no `path`. `store` and
  `erase` add `username` and `password`. 2.46 and later add `capability[]` and `wwwauth[]` lines;
  2.43 sends neither.
- A helper's `username=` replaces one from the URL (the request goes out as `x-access-token`).
- `!'<path>' --vc-credential` runs through `sh` with `get`, `store` or `erase` appended: a path with
  spaces and an apostrophe works quoted `'…'\''…'`; forward slashes and backslashes both work.
- `GIT_CONFIG_COUNT`: `abc` and `-1` fail every call with 128; a missing pair fails with 128; empty
  is 0. Env pairs come after every file, and before an inherited `GIT_CONFIG_PARAMETERS` and `-c`. A
  helper listed there, or brought in by an `include.path` there, is asked after the app's helper and
  gets its `store` and `erase` with the token; an empty `credential.helper=` there clears the app's
  helper. Git rebuilds `GIT_CONFIG_PARAMETERS` from its own `-c` values for its children, so the
  app's `-c` values survive the inherited variable's removal.
- An `http.<url>.extraHeader` with an `Authorization` header (actions/checkout leaves one in
  `.git/config`) replaces the helper's credential: the server answers 401 and git sends `erase` with
  the account's valid token. A 302 to another host does the same (the review's runs).
- Rewrites (`git remote get-url --all` / `--push --all`): `insteadOf` is a plain, case-sensitive string
  prefix (`https://github.com/chain` rewrites `…/chainx/y`); the longest wins, a tie goes to the base
  configured first; an empty `insteadOf` matches every URL; no chaining. A remote with `pushurl` pushes
  to its values after `insteadOf`; one without uses, for each `url`, the `pushInsteadOf` rewrite of the
  configured value; if any `url` has one, the push URLs are those rewrites alone; else the fetch URLs.
  `pushInsteadOf` never applies after `insteadOf`, and `https://github.com/` does not rewrite
  `https://alice@github.com/…`. A remote without `url` fetches from its name.
- `config --null --get-regexp ^url\.` prints `url.<base>.insteadof\n<value>\0` (variable lower case,
  base as written); exit 1 when none. After a value without a trailing `/` (`https://host`), today's
  `GitCommand.HideCredentials` runs through the `\0` into the next key and leaves its user information
  in clear (the review's runs); phase 5's undo read (`GitRepository.Undo.cs:155`) leaks the same way.

### 4.3 `UrlRewrites` and its read

`Parse`: entries whose key starts `url.` and ends `.insteadof` or `.pushinsteadof` (without case); the
base between, dots and case kept; a value-less entry skipped; rules in entry order. `Apply`: per
remote, in order of first appearance; fetch URLs each `url` rewritten; push URLs as 4.2; a URL's
rewrite is the rule of its kind with the longest prefix, ties to the base first seen, then the earlier
value; an empty prefix matches; a remote without `url` has no fetch URL. `ReadUrlRewritesAsync`: one
`config --null --get-regexp ^url\.` in the working folder through `Read(...)` as `ReadRemoteKeysAsync`
(`GitRepository.Refs.cs:80-122`), with `GIT_CONFIG_PARAMETERS` removed as in a bound scope (4.6), so
it sees the rules the bound calls apply; exit 1 is `None`; other failures throw as there. WP-C does
not call it from the refs read (WP-S does, only when `WantsUrlRewrites`), so no count of git calls
moves.

Its output is recorded through `GitCommand.HideCredentials` (D103), so an authority must end at the
`\0` (4.2): `ReplaceCredentials` (`GitCommand.cs:94-127`) and `OutputCollector.RecordedOutput`
(`GitRunner.cs:656-659`) share
`public static bool EndsAuthority(char c) => c is '/' or '?' or '#' || char.IsWhiteSpace(c) || char.IsControl(c);`
on `GitCommand` (public: Core grants no `InternalsVisibleTo`); both doc comments say "or a control
character". The runner hides the raw stream (`GitRunner.cs:544`, `:644`).

### 4.4 `CredentialBinding.Select` (D278)

`Select(remotes, webBase, account)`, for WP-S (`Bindings`) and WP-B (`BindingsForClone`, the clone's
URL after the user's rules as one remote's fetch URL): every fetch and push URL, in order, that parses
as an absolute URI with `webBase`'s scheme and host (without case) and port (the default port the
same as none), and whose user information is empty or exactly the login (without case; a password makes it unbound). The
binding's `Url` is the URL text with `user@` removed and nothing else changed. Then a binding is
dropped when another URL of the remotes has the same scheme and host, a path equal to the binding's or
under it at a `/`, and a user other than the login (a key without a user would answer that user too,
4.2). Distinct by `Url` (ordinal), in remote order.

### 4.5 The helper and the protocol

`visualcommit-askpass --vc-credential <action>` (`CredentialMode`):
1. Reads standard input to its end, at most 64 KiB (`AskPassProtocol.MaxLineBytes`; more: no answer,
   the rest drained). `ParseGitCredentialInput`: `key=value` lines split at the first `=`, a trailing
   `\r` dropped, an empty line ends; keeps `protocol`, `host`, `username`, `password`; ignores the rest
   (`capability[]`, `wwwauth[]`, `state[]`, `path`, `url`, `password_expiry_utc`).
2. `store`, or an unknown or missing action: exit 0 at once, without the pipe (D273).
3. Needs `protocol`, `host`, the pipe and token variables; `erase` also `password`. Else exit 0.
4. `VISUALCOMMIT_ASKPASS_DENY` does not stop this mode (it still stops prompts, `Program.cs:30`).
5. Connects as prompts do (5 s; on Windows `WindowsPipeUser.OwnsPipe`), sends one
   `AskPassCredentialRequest` line, reads the answer with `CredentialAnswerTimeout` (30 s).
6. `get` with a non-null answer: writes `username=x-access-token\npassword=<token>\n`, UTF-8 without
   BOM; nothing when the answer is null, empty or holds CR, LF or NUL. Never announces `capability[]`.
7. Exit 0 always, also on any exception; nothing to standard error.

`AskPassProtocol.cs`: `CredentialArgument = "--vc-credential"`; `CredentialUserName = "x-access-token"`
(a test keeps it equal to `GitCredential.UserName`); `CredentialAnswerTimeout`;
`record AskPassCredentialRequest(Token, Operation, Action, Protocol, Host, string? Username, string? SecretHash)`,
one JSON line `{"token","operation","credential":"get"|"erase","protocol","host","username","secretHash"}`;
`FormatCredentialRequest`, `ParseCredentialRequest` (null unless `credential` is `get` or `erase` and
the strings are there); `record GitCredentialInput(Protocol, Host, Username, Password)` whose
`ToString` hides the password; `SecretHash(string)` = lower-case hex SHA-256 of the UTF-8 bytes. The
erase carries that hash, never the password. `ParseRequest` keeps refusing these lines; answers use
`FormatAnswer`/`TryParseAnswer` unchanged.

### 4.6 The broker

- Construction: `_credentials = credentials ?? NullGitCredentialSource.Instance`. The inherited count
  is the session environment's `GIT_CONFIG_COUNT` when it has the key (null is 0), else the
  process's; empty or absent 0; not an integer of 0 or more: bindings are never used, one Warning
  `GIT_CONFIG_COUNT is not a number git accepts; GitHub's token is not given to git.`
- Bindings are used only when `HelperFound`, a source was given, the count is valid, the mode is
  Ask or Deny and `gitVersion()` is not below 2.31 (D277); de-duplicated by `Url`. Otherwise ignored:
  D82's order, untouched (D274). A version not yet known counts as 2.31: an older git ignores the
  pairs, so the token cannot reach it either way.
- `EnvironmentFor(id, mode, bindings)`. Without used bindings: exactly today's three cases. With:
  Ask's variables, or Deny's with the pipe, token and operation id set (`DENY=1` and
  `GCM_INTERACTIVE=never` kept); plus, for binding `i` from `n` = the inherited count:
  `GIT_CONFIG_KEY_<n+2i>` = `credential.<url>.helper`, `GIT_CONFIG_VALUE_<n+2i>` empty;
  `GIT_CONFIG_KEY_<n+2i+1>` the same key, value `!'<helper>' --vc-credential` (`HelperPath` with `\`
  as `/`, each `'` as `'\''`); `GIT_CONFIG_COUNT` = `n + 2k`; `GIT_TRACE_REDACT=1` (ruling 7);
  `GIT_CONFIG_PARAMETERS` = null (D329, 4.2; the command's environment is applied last,
  `GitRunner.cs:260-270`, so this removes the session's value and the process's). Invariant culture;
  inherited pairs stay, and the reset clears them for the bound URL. When the inherited value (the
  session environment's when it has the key, else the process's) is not empty, one Debug line per
  scope: `<repository>: <title>: the inherited command-line git configuration is left out of calls
  bound to the account.` Unbound scopes keep it (D274).
- `Begin` registers Ask scopes as today and a Deny scope with used bindings; a Deny scope without
  them is neither registered nor given the pipe (D274).
- `ServeAsync`: a line that `ParseCredentialRequest` reads goes to `AnswerCredentialAsync`; any other
  to the prompt path as today.
- `AnswerCredentialAsync`: the token compared in fixed time (`:268`), the scope looked up (`:274`);
  none, or no used bindings: null. The binding: the first whose scheme and authority equal
  `protocol` and `host` (host without case, `:443`/`:80` dropped); none: null. A `username` other than
  the login (without case) or `x-access-token`: null (D275). `get`: null when the scope's refused set
  holds the account; else `await _credentials.GetAsync(account, scopeClosing)`; null, or a token with
  CR, LF or NUL (a Warning): null; else the token, its `SecretHash` kept in the scope's handed-out
  set. `erase`: when the hash is in the handed-out set, `_credentials.Refused(account)` (the session
  checks the token before it marks the account, 7.6, D330), the account into the
  refused set, `RefusedAccount = account`; else ignored (D273). Logs (Debug unless named):
  `<repository>: <title>: git's credential <action> for <host>` with `answered from the account`,
  `has no token`, `is not bound` or `names another user`; the erase at Info, `<repository>:
  <title>: <host> refused the account's token.` Never a URL path, never the token.
- `Scope.AskAsync` (`:314`): a Deny scope: null at once without the handler. A `Username` or
  `Password` prompt whose URL's scheme and host equal a used binding's and whose user is none, the
  login or `x-access-token` carries `Account`. Refused in this scope: null, no dialog (D273; the
  error dialog follows). Otherwise the handler as today. No prompt is answered from the account and
  `GetAsync` is never called from here: ruling 3's fallback is not built (D277).
- Threads: answers on the thread pool; `GetAsync` holds a pipe connection at most as long as the
  source waits (10 s); 16 connections stay enough; no UI thread waits on the pipe.

### 4.7 Call points, the refused token's dialog, the guard, the group

**Call points.** WP0's `CreateOperation(repository.Repository, repository.Hosting.Bindings)`
(`AutoFetchScheduler.cs:160`) stays; `CreateOperation` sets `CredentialBindings = bindings ?? []`. In
`RunAsync`, before `:344` (`var operation = new GitOperation(…)`), a local `bindings` is
`Repository.Hosting.Bindings`, or a dashboard row's bindings below; WP0's line in the operation's
initializer (`:378-383`), `CredentialBindings = Repository.Hosting.Bindings`, becomes
`CredentialBindings = bindings`.

**A dashboard row without a tab** (D332) runs on a summary-mode view model, whose hosting is the
null object. For an `OperationKind.Network` operation while `Repository.IsSummary`,
`Services.Hosting.BindsGit` and `Repository.Refs` is set, before `:344`: await
`Repository.Repository.ReadUrlRewritesAsync` (the lifetime token linked to `options.Cancellation`),
make the closed and cancelled checks (`:328-339`) again, then `bindings =
Services.Hosting.BindingsFor(Repository.Refs.RemoteUrls, rules)`; a failed read logs one warning and
binds nothing (`bindings = []`, D82's order). This covers Fetch all and Pull all
(`BulkRun.cs:423`) and a row's Fetch and Pull (`DashboardViewModel.cs:408-412`); a row that follows a
tab runs on the tab's view model. Signed out nothing is read, so no dashboard count moves. Only network calls carry a
scope's environment (`NetworkOptions.From`), so local calls and settings reads see no pair. The
app's own `NetworkOptions.Configuration` `-c` values (`GitRepository.Remotes.cs:666-668`) never carry
a `credential.*` or `include.*` key: git reads them after the pairs (4.2).

**The refused token's dialog** (ruling 4, D276, D330). In `ShowResultAsync` and `DeferredOf`, a failed
result with `RefusedAccount` takes, before `DescribeFailure`, `FailureDisplay(text.ErrorTitle,
OperationTexts.GitCredentialsRefused(login), extra)`:
- Summary `GitHub refused git's credentials for {login}. If the token has expired or was revoked,
  sign in to GitHub again in Settings, Accounts.`, built as `$"{HostingTexts.GitHub} refused git's
  credentials for {login}. If the token has expired or was revoked, sign in to {HostingTexts.GitHub}
  again in Settings, {HostingTexts.AccountsSection}."` (`SignInInSettings` does not fit: "again"
  stands inside it); `{login}` is `Services.Hosting.Account.Account.Login` (its own case) when that
  key equals `RefusedAccount`, else `RefusedAccount.Login`.
- Why not "the token": git's refusal does not prove the token bad. A stale `http.extraHeader` or a
  redirect makes git erase a valid token (4.2), and 7.6's check then keeps the account signed in
  while the dialog still shows (D330). `OperationTexts.TokenRefused(login)`, `GitHub refused the
  token of {login}. Sign in to GitHub in Settings, Accounts.`, built as `$"{HostingTexts.GitHub}
  refused the token of {login}. {HostingTexts.SignInInSettings}"`, is for the API's 401 only, which
  is certain: `HostingTexts.Describe`'s `BadCredentials` with a login (1.7) and the create form's
  POST (12.6). Both texts are in `OperationTexts.Hosting.Credentials.cs`.
- Extra: `ErrorAction(OperationTexts.OpenSettingsButton /* "Open settings…" */, () => openSettings(SettingsSection.Accounts))`
  when `Services?.OpenSettings` is set, as the editor failure does (`RepositoryOperations.Files.cs:159-173`).
- The layout, title, git's output and "Copy output" are today's; a bulk run's row keeps the same
  display (D195); auto-fetch shows nothing new.

**Push guard (ruling 8, D279).** `FetchRefspecs.SourceOf(string trackingRef, string remote, Func<string, bool> sourceCanExist)`:
the two-argument form with the candidates the predicate refuses removed before rules 1 to 3 (none
left: `NotFetched`); always true equals the two-argument form. `RefuseIfPushStopsFetchAsync` takes the
remote's first `RemoteUrl` with `IsPushUrl` false from `Refs?.RemoteUrls`; with one, `source =>
Services?.Hosting.SourceCanExist(url, source) ?? true` goes to both `SourceOf` calls; without, the
two-argument form. Texts unchanged; the two-namespace refusal stays (its candidates are branches). No
git call is added.

**Prompt group (ruling 9).** `GroupState.Kept` is keyed by `(string Account, string Text)`, account
`prompt.Account?.Id ?? ""`, also for the password kept after a user name; `Kept` gains `int Version`
(the group's `AnswersVersion` when kept); `Recall` drops entries of a lower version. `Recall` and
`KeepInGroup` take the `PromptGroup`; the per-scope `_kept` stays. `BulkRun`, after `row.Result =
result;`: a failed result whose `Failure.Kind` is `AuthenticationFailed` calls `ForgetAnswers()`.

### 4.8 Tests

Credentials in tests: `AuthRemoteServer.Credentials = new("x-access-token", "github_pat_FAKE-octocat-1")`;
bindings to `server.Url` with `AccountKey(GitHub, "127.0.0.1:<port>", "octocat")`; the "user helper" is
`credential.helper=store --file='<temp>/user-store'` in the TempRepo's own global file
(`repo.Environment["GIT_CONFIG_GLOBAL"]`), never the process's isolation file.

| Class (project) | Proves |
|---|---|
| `CredentialHelperTests` (Git) | parsing recorded inputs of 2.43 and 2.56 (`capability[]`, `wwwauth[]`, `state[]`, `path`, unknown keys, `=` in a value); the answer text; CR or LF prints nothing; through `RunCredentialAsync`: `get` prints two lines and exits 0; no answer, no pipe, a dead pipe, the deny variable, `store`, an unknown action: exit 0 with nothing; `store` makes no connection; `CredentialUserName` equals `GitCredential.UserName` |
| `AskPassBrokerBindingTests` (Git) | pairs after the process's and the session's count; a session null; an invalid count, no helper, no source, mode None: no pair; Deny without bindings byte-identical to today and unregistered, with bindings registered with the pipe; quoting a path with a space and an apostrophe; de-duplication; `GIT_TRACE_REDACT=1`; used bindings map `GIT_CONFIG_PARAMETERS` to null, without them there is no such key; one Debug line for a non-empty inherited value, none for an empty one; `gitVersion` 2.30.0: no pair and the environment byte-identical to today's, 2.31.0 or unknown: the pairs; a prompt to a Deny scope never reaches the handler |
| `AccountCredentialTests` (Git, `AuthRemoteServer`, `RecordingCredentialSource`) | fetch, push and clone with the token: no prompt, the user helper's file absent; a revoked token: `AuthenticationFailed` at once, no prompt, `Refused` once, `RefusedAccount` set, the scope's second remote unanswered; an unbound URL: user helper, then the handler (git then stores alice's password there, its own way); Deny: bound fetches, unbound fails within 10 s; the helper copied by `CopyHelperTo` into a folder with a space; `config --list --show-origin` of repository, clone and global file unchanged, no `credential` key in the clone; an inherited `GIT_CONFIG_COUNT=1` pair applies; `GIT_TRACE_CURL=1` under an inherited `GIT_TRACE_REDACT=0` prints neither token nor base64; the version unknown and the pairs stripped (what git older than 2.31 sees): user helper, then the handler (which cancels), `GetAsync` never called, the user's file without the token; `GIT_CONFIG_PARAMETERS` `'credential.helper'='store --file=<temp>/inherited-store.txt'` in GitRunner's session environment (the test process's isolation strips it), or an `include.path` file setting such a helper: the bound fetch succeeds, the file absent; unbound, the inherited helper is asked (`TempRepo.cs:91-94`'s null must not hide it); an `http.<server>/.extraheader` with another `Authorization`: `AuthenticationFailed`, `Refused` once (7.6 checks the token), the user's file absent |
| `UrlRewritesTests`, `UrlRewritesParityTests` (Git) | `Parse` and `Apply` on each fact of 4.2; two repositories with the rules and remotes (one with the empty prefix): `Apply(RemoteUrls, ReadUrlRewritesAsync)` equals `git remote get-url --all` and `--push --all` per remote; one `config` call, exit 1 `None`; a global file with two credential-bearing bases without a trailing slash: the recorded call (`CommandText` starting `git config --null --get-regexp`) holds neither token, asserted as `RemoteOperationTests.cs:494` does; the same for the undo read (`config --local --null --list`) with two local `url` rules |
| `GitRunnerTests` (Git; the theory near `:449`) | `HideCredentials` and `RemoveCredentials` of `url.https://oauth2:T1@a.example.insteadof\nhttps://a.example\0url.https://oauth2:T2@b.example.insteadof\nhttps://b.example\0`, and of a value `ssh://git@c.example\0` followed by a credential-bearing key: each token comes out as `***@` (or is removed); `RecordedOutput` takes a control character as an authority's end |
| `CredentialBindingSelectTests` (Git) | bound: no user, the login in any case, push URLs, `:443`, the fake's exact port; not bound: another user, a password, `http` for github.com, ssh, another host or port; `/o/r` and `/o/r/sub` dropped beside `https://alice@github.com/o/r`, `/o/rx` kept; the URL text kept but for the user |
| `FetchRefspecsSourcePredicateTests` (Git) | the overload |
| `PushGuardGitHubTests` (App) | a bare remote with `+refs/pull/*/head:refs/remotes/origin/pr/*` beside the default line, recognised by `HostingSessionStub`: `pr/fix-typo` pushed; `pr/7` refused with today's text; not recognised: `pr/fix-typo` refused as in phase 6; the two-namespace case refused |
| `PromptGroupAccountTests` (App) | one text under two accounts asks twice, under one once; `ForgetAnswers` asks again, also for a kept Cancel and a kept password |
| `BulkRunForgetAnswersTests` (App) | a row failing with `AuthenticationFailed` raises `AnswersVersion`; another failure does not |
| `AutoFetchCredentialTests` (App, `OperationsHarness` with `hosting`) | auto-fetch with a binding succeeds without a prompt; without one fails at once; no handler call |
| `RefusedTokenDisplayTests` (App) | a refused fetch shows `GitCredentialsRefused`'s summary, word for word as 4.7 gives it, with the login's own case, never `TokenRefused`'s, also while `HostingSessionStub` keeps the account signed in; "Open settings…" opens Accounts; no credentials dialog; a deferred bulk row, on a row without a tab, the same; `TokenRefused("octocat")` and `GitCredentialsRefused("octocat")` equal 4.7's two sentences |
| `DashboardCredentialTests` (App; a `DashboardViewModel` as `DashboardRefreshTests` builds one, a `HostingSessionStub`, `AuthRemoteServer`) | rows without a tab, `BindsGit` true: Fetch all and Pull all started while the rows still read, and a row's Fetch, succeed with no prompt, no handler or user-helper call, the user helper's file absent, one `^url\.` read per operation; `BindsGit` false: no `^url\.` call, the user's helper asked as today; `BindsGit` set while the dashboard is open binds the next operation |

The builder runs these classes and the touched old ones: `AskPassTests`, `AuthenticatedRemoteTests`,
`FetchRefspecsTests`, `FetchRefspecsParityTests`, `PromptDialogsTests`, `RepositoryOperationsPushTests`,
`AutoFetchSchedulerTests`, `BulkRunTests`, `DashboardRefreshTests`. No visual checks of its own: part A pictures the refused
token's dialog (title "Fetch failed", `GitCredentialsRefused`'s summary, "Open settings…") and Accounts' "needs sign-in";
part B the clone through the helper. No earlier picture changes.

Commits: (1) protocol, `CredentialMode`, `Program.cs`, `RunCredentialAsync`, `CredentialHelperTests`;
(2) `UrlRewrites`, its read, `EndsAuthority` and classes; (3) `Select` and its class; (4) the broker and
`RecordingCredentialSource` with their classes; (5) `CreateOperation`, `OperationsHarness`,
`HostingSessionStub`, the dialog and texts with their classes; (6) the push guard; (7) the group;
(8) a dashboard row's bindings with `DashboardCredentialTests`.

### 4.9 At the merge; done when

The lead merges WP-C before WP1 starts (`OperationsHarness.cs` passes to WP1); gives `AppSession.Start`'s
`CreateBroker` call 7.11's form (WP0's session argument, `gitEnvironment` and `gitVersion`); runs the
classes above; amends D82 (D255). At closing, `docs/architecture.md`'s "Prompts and credentials"
gains the helper's mode.

Done when every class passes on the laptop and in CI on Windows (2.56), macOS (2.56), Linux (2.55)
and both containers (2.43); no earlier test or picture changes; the token is in no argument, URL,
config value, environment variable, log line or file of any test run, the user helper's and the
inherited helper's files included, also in the case of git older than 2.31 (D277).

## 5. WP1: the first package (W1, S8, R8, G23's split)

### 5.1 Goal and files

Carries plan.md 7a's "From the review" item (D254) and D264: W1's held read clock (ruling 27), S8
(ruling 28), R8's watcher lag (ruling 29, D262), G23's split (ruling 32); D280 to D283. Test code, the
watcher and its creation: no UI, no user-visible text, no part, no picture changes. Out: L1, batched
config reads (backlog), CI (D262), hosting code. Wave 1 after WP-C's merge (it receives
`OperationsHarness.cs`); branch `wip/p7a-first`.

Files: `src/VisualCommit.Git/RepositoryWatcher.cs`, `GitRepository.cs` (the lag only),
`src/VisualCommit.App/Services/GitRepositoryProvider.cs` (a log line); new
`tests/VisualCommit.Testing/Headless/GitCallBudget.cs`, `ShellDriver.Budget.cs` (a partial; never
`ShellDriver.cs`, WP-F's) and `tests/VisualCommit.App.Tests/GitCallBudgetTests.cs`; the tests 5.3 to
5.5 name. After WP1's merge `GitCallBudget.cs` passes to WP-S and part S's files to WP-A.

### 5.2 R8: the watcher's lag (D282)

**Variable.** `VISUALCOMMIT_WATCHER_LAG_MS`, whole milliseconds 0 to 10,000; anything else (empty, a
sign, a space, a decimal point, over 10,000) and 0 mean no lag. In `GitRepository.cs` beside
`CreateWatcher` (`:168-169`):

```csharp
public const string WatcherLagVariable = "VISUALCOMMIT_WATCHER_LAG_MS";
/// The lag every watcher of this process holds each file-system event for (R8, D262): read once.
public static TimeSpan WatcherLag => ProcessWatcherLag.Value;
private static readonly Lazy<TimeSpan> ProcessWatcherLag =
    new(() => ParseWatcherLag(Environment.GetEnvironmentVariable(WatcherLagVariable)));
internal static TimeSpan ParseWatcherLag(string? value) =>
    int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var ms) && ms <= 10_000
        ? TimeSpan.FromMilliseconds(ms) : TimeSpan.Zero;
public IRepositoryWatcher CreateWatcher() => new RepositoryWatcher(GitDirectory, CommonDirectory, _log,
    workingDirectory: WorkingDirectory, allIgnored: AllIgnoredAsync, eventLag: WatcherLag);
```

Process-wide, with no harness parameter, so it reaches every harness (`OperationsHarness.cs:134`
opens through `GitRepository.OpenAsync`) and the real app.

**`RepositoryWatcher`.** New last parameter `TimeSpan? eventLag = null` (null or not positive: none).
- No lag: `Watch` (`:271-295`) wires the handlers exactly as today.
- A lag: `Watch` wires each `Created`, `Changed`, `Deleted`, `Renamed` and `Error` event to `Hold`,
  which writes `(due, deliver)` into one unbounded single-reader `Channel` in arrival order (`due` =
  `Stopwatch.GetTimestamp()` at arrival plus the lag; `deliver` calls the handler the event would
  have reached, `OnGitFolderEvent`, `OnWorkingTreeEvent` or `OnError`, `:297-348`). One consumer task,
  started in `Start` inside its `_started` guard, reads in order, waits until the timestamp passes
  `due` (a `Task.Delay` that returns early is waited again), then delivers. The system clock, never
  `_time`: tests that hold `_time` wait for `EventCounted` before moving it.
- The pause check, the `Directory.Exists` check and `EventCounted` run at delivery, late, as on macOS:
  a write inside `Pause()` whose pause ended before delivery counts as an outside change. `IsQuiet`
  (`:120-129`) is unchanged and does not see held events.
- `Dispose` (`:192-214`) also completes the channel's writer; the consumer stops on cancellation and
  drops what it holds. It keeps the `CancellationToken` taken at construction and checks `_disposed`
  before each delivery. A delivery that throws is logged at Error, "A held event of the repository
  watcher failed.", and the consumer goes on, on the thread pool; nothing reaches the UI thread.

**Log line.** `GitRepositoryProvider`'s constructor (`:20-26`), once per session when the lag is
positive, at Info: `VISUALCOMMIT_WATCHER_LAG_MS=1500: every repository watcher holds each file-system
event for 1500 ms before it counts it (a test switch, D262).` (the number is the lag).

**Tests** (`tests/VisualCommit.Git.Tests/RepositoryWatcherTests.cs`):
1. `A_lag_reports_a_write_made_inside_a_pause_that_ended_before_it`: a plain folder, as
   `Nothing_is_raised_before_start_or_after_dispose` (`:179`); `eventLag: TimeSpan.FromSeconds(1)`; a
   stopwatch, then `using (watcher.Pause()) { write a file }`: `Changed` within 10 s with
   `Last.GitFolder`, the first `EventCounted` at least 1 s after the stopwatch started.
2. `A_lagged_watcher_disposed_with_events_held_drops_them`: write, `Dispose` at once, wait lag +
   `Settle`: no `Changed`, no Error entry.
3. `The_lag_variable_takes_whole_milliseconds_from_0_to_10000` over `ParseWatcherLag`: `"1500"`, `"0"`,
   `"10000"` as given; `"10001"`, `"-1"`, `"+5"`, `" 1500"`, `"1.5"`, `"abc"`, `""`, null: zero.

The class's five cases on `repository.CreateWatcher()` (`:51, 222, 238, 254, 273`) pass at 1,500 ms,
but their "nothing within `Settle`" assertions (`:65, 83, 262`) prove less, so the class is not on the
lagged list (0.4).

### 5.3 S8: counts of git calls (D280, D281)

**Rule** (7a's checks count git calls only through these helpers): with a live watcher, (1) mark with
the watchers quiet (`QuietCallMarkAsync`), never by `Snapshot().Count`; (2) read with them quiet again
(`QuietCallsSinceAsync`); (3) count only the kind measured: calls outside every operation that are
whole refreshes are set aside, anything else fails and prints the calls; (4) `<=` a budget (the count
measured today) when the number is a cost, `==` when it is the test's meaning; (5) a step whose calls
must be only whole refreshes changes neither a ref nor a working file outside the app. A moved or
added ref (a commit, `git tag`, `git branch`) makes the refresh reload the history (`git log
--date-order --format=…`, `RepositoryViewModel.cs:713-721`), and a working-tree write can close a burst
of its own (`git check-ignore --stdin -z` and a lone status read, `RepositoryWatcher.cs:446`,
`RepositoryViewModel.cs:1645`). Neither is a refresh form, and `GitCallBudget` gets no `git log` form.
A working-tree-only burst gets a form of its own only when a lagged run (0.4) shows one in a counted
step, such as the app's own write of working files delivered late (part P's fork checkout; the lead's
ruling after the draft).
Such a step's outside change is a write of git config (`TempRepo.ConfigAsync`, as
`Phase6SupportTests.cs:208`). A bound alone does not cure C18; steps 1 to 3 do.

```csharp
public sealed record RefreshForm(string Name, Func<string, bool> Matches, bool Optional = false);
public static class GitCallBudget
{
    // Part S's six forms, moved from Phase6Checks.S.Helpers.cs:722-730 unchanged, none optional.
    public static IReadOnlyList<RefreshForm> RefreshForms { get; }
    // Moved from :739-762. Required forms each as often as the others; an optional form 0 times or as
    // often; every call of a form outside every operation. Null when whole.
    public static string? NotWholeRefreshes(IReadOnlyCollection<GitCallRecord> calls, IReadOnlyList<RefreshForm>? forms = null);
    // Fails "{what} made git calls that are no whole refresh ({reason}):" and the calls, one a line.
    public static void AssertOnlyWholeRefreshes(IReadOnlyCollection<GitCallRecord> calls, string what);
    // setAsideWholeRefreshes: refresh-form calls outside every operation are not counted and must be
    // whole refreshes. Fails "{what}: budget {budget}, used {used}. The calls:" and each command with its operation id.
    public static void AssertAtMost(IReadOnlyCollection<GitCallRecord> calls, int budget, string what, bool setAsideWholeRefreshes = false);
    public static void AssertAtMost(IReadOnlyCollection<string> commands, int budget, string what);
}
```

WP-S later adds `RefreshForm("git config --null --get-regexp ^url\\.", …, Optional: true)`. That read
is started by the hosting, not awaited by any refresh (7.10), so `IsWatcherQuiet` also waits for it
(`IsHostingQuiet`, 1.7): every quiet wait below waits for it through `IsWatcherQuiet`, and a read
still running never straddles a mark. A sign-in starts one too: 7a's `OpenAsync` waits for the
watchers to be quiet after its sign-in (2.8), and a step that signs in marks after such a wait.

**`ShellDriver.Budget.cs`**: `Task<long> QuietCallMarkAsync()` (`WaitForWatchersQuietAsync`,
`ShellDriver.Phase3.cs:286`, then `CallMark()`) and `Task<IReadOnlyList<GitCallRecord>>
QuietCallsSinceAsync(long sequence, Func<string, bool>? inFolder = null)`, at the default wait (60 s).
Tabs only: dashboards' summary view models have no watcher (D207) and start no URL-rewrites read of
the hosting (D332).

**`OperationsHarness.cs`**, after `WaitForIdleAsync` (`:156-157`): `WaitForWatcherQuietAsync()` polls
`Repository.IsWatcherQuiet` (`RepositoryViewModel.cs:319-323`) every 15 ms, at most
`ShellDriver.DefaultWaitTimeout`, else `TimeoutException` "Timed out after 60 s waiting for the
repository watcher to be quiet."; `QuietCallMarkAsync()` awaits `WaitForWatcherQuietAsync()`, then
takes the highest `Sequence` in `Calls.Snapshot()`, 0 for none; `QuietCallsSinceAsync(long sequence)`
awaits it too, then returns the calls with `Sequence > sequence`.

**Moves.** `Phase6Checks.S.Helpers.cs`: `RefreshForms` and `NotWholeRefreshes` (`:717-762`) deleted;
`QuietMarkAsync` (`:711-715`) becomes `=> app.QuietCallMarkAsync()`; `AssertNoCallsAsync`
(`:768-773`) and `AssertScreenReadsAsync` (`:806-808`) call `GitCallBudget`. `Phase6SupportTests`:
the unit case `:157-184` moves to `GitCallBudgetTests`; the app case `:186-217` stays, marking and
reading through the new pair and naming `GitCallBudget.RefreshForms.Count`.

**`GitCallBudgetTests`** (App.Tests): the moved case; an optional form counted 0 or n times passes, 1
of 2 fails; `AssertAtMost` passes at its budget and above it fails with "budget 2, used 3" and each
command; with `setAsideWholeRefreshes` a whole refresh is not counted and a lone status read fails; an
`OperationsHarness` case on `Scenarios.OperationsAsync()`: `var mark = await
harness.QuietCallMarkAsync();`, `await harness.Repo.ConfigAsync("pull.ff", "true");` (rule 5), a wait
(polling `Calls.Snapshot()`, at most `ShellDriver.DefaultWaitTimeout`) for a status-form call with
`Sequence > mark` (the refresh awaits its status read last, `RepositoryViewModel.cs:730`), then `var
calls = await harness.QuietCallsSinceAsync(mark);`, `calls.Count >= GitCallBudget.RefreshForms.Count`
and `AssertOnlyWholeRefreshes(calls, "the watcher's refresh after a write to .git/config")`.

**Converted to budgets** (Git.Tests has no watcher; each number is a cost):
- `IgnoredInTheWayTests.cs:334` → `AssertAtMost(linearCalls, 4, "the rebase check on a branch only ahead (U2)")`; `:335` stays.
- `RefReadingTests.cs:368` → `AssertAtMost` over `"git " + string.Join(' ', arguments)`, 5, "the refs read"; `:369-370` stay.
- `Phase6ScenarioTests.cs:123` → `var built = …Count;` and `AssertAtMost(template.Calls.Snapshot(), 28,
  "the workspace template's build")`; `:126` and `:153` → `Equal(built, …)`.

**Converted to quiet marks with whole refreshes set aside** (the meaning still exact):
- `Phase6Checks.K.Helpers.cs:114-119` `AssertNoCall` → `async Task AssertNoCallAsync` (quiet read,
  `AssertOnlyWholeRefreshes`); marks at `K.Dashboard.cs:81, 253, 287, 322` → `await
  app.QuietCallMarkAsync()`; calls at `:88, 260, 294, 337` awaited (K13).
- `K.Helpers.cs:325-336` `AssertOnlyTheWorktreeReadAsync`: after its two waits a quiet read, exactly
  one `PartK.WorktreeList` outside every operation, the rest whole refreshes; mark at `K.cs:76` quiet.
- `W.Tabs.cs:165, 183-184` (W8 step 7): quiet mark and read; no call in folders of rows without a
  tab, only whole refreshes in docs' and mobile's.
- `W.Tabs.cs:188, 216-217` (W8 step 9): `mark2` quiet; `WaitForWatchersQuietAsync` before `:216`.
- `W.Welcome.cs:275, 294-295` (W1 part B step 10): quiet mark and read; none in the eight rows
  without a tab, only whole refreshes in web-app's and docs'.
- `App.Tests/UndoStepTests.cs:41, 47` and `RepositoryOperationsFetchTests.cs:118, 124`: quiet mark;
  `AssertOnlyWholeRefreshes(await app.QuietCallsSinceAsync(mark), …)`.

Steps that read at once still do (W8's `AssertNothingReads` after a settle). "The dashboard read
nothing" stays in the log assertions beside them (`W.Tabs.cs:181-182, 193-194`, `W.Welcome.cs:291-293`).

**Left exact** (Git.Tests unless named):
- No watcher, the number is the behaviour: `GitRunnerTests.cs:674`; `SearchTests.cs:282, 304, 316,
  359`; `ConflictOperationTests.cs:493`, `GitRepositoryOpenTests.cs:177, 202, 241`;
  `IgnoredInTheWayTests.cs:641-643, 648` (exact sets) and `:651-657` (the branch taken);
  `MergeToolHelpTests.cs:157, 267`; the `Single`/`Empty` on calls in `GraphFilterTests`,
  `PseudoRefProbeTests`, `FakeSshTests`, `RemoteOperationTests`, `RebaseOperationTests`,
  `KeepIgnoredCheckoutTests`, `GitLocatorTests`, `CloneTests`.
- The counted kind is no refresh's: App.Tests' `RepositoryOperationsKeepIgnoredTests.cs:80, 115, 145,
  248, 283, 324-326, 387, 840`, `UndoRecordingTests.cs:457, 466`, `RepositoryOperationsProtectTests.cs:93`,
  `…FetchTests.cs:396`, `…PushTests.cs:670, 751`, `…HistoryTests.cs:173`, `…SequenceTests.cs:38, 561`,
  `UndoRestoreTests.cs:41`, `ShellDriverPhase5ProtectTests.cs:31, 43, 46`, `KeptCommitGoneTests.cs:68`;
  every `Single`/`Empty` on calls in phases 3 to 5's walk-throughs; `Phase6Checks.K.cs:215`,
  `K.Dashboard.cs:156`, `W.Failures.cs:122`, `W.Tabs.cs:142`, `L.Overlays.cs:155`; part G's
  operation-filtered reads; `Phase5Checks.F.cs:98, 216, 259, 284, 316`.
- Folders without a watcher: `W.Restart.cs:85, 106`, `W.Rows.cs:70`, `W.Tabs.cs:195`,
  `W.PullAll.cs:106-110`, `K.cs:349-353`, `K.Dashboard.cs:234`; App.Tests' `WorkspaceFolderProbeTests.cs:47, 61`,
  `ShellTests.cs:40`, `DashboardViewTests.cs:338`.
- Exact sets, quiet already: `S.Helpers.cs:822`, `S.Pull.cs:257` (9 is `:254`'s set's size) (D280).
- To confirm under the lag: `SearchBarTests.cs:97, 116`, `CompareViewTests.cs:181-189`. A lagged
  failure gets this section's treatment, not a looser count.
- Not git calls: the fake recorders' counts in App.Tests (`host.Calls`, `spy.Calls`, …).

### 5.4 W1: a held read clock

A `ManualTimeProvider` (`tests/VisualCommit.Testing/ManualTimeProvider.cs`) never moved, in every
part W check that asserts a pass's count (W1 both methods, W2, W9; ruling 27), not as
`OpenWorkspaceAsync`'s default.
- W1 part A, `Phase6Checks.W.Welcome.cs:44`: `ShellDriver.Start(data.Path, 1100, 700, readClock: new
  ManualTimeProvider())`. Steps 4 (`:148`, 1) and 6 (`:179`, 8) unchanged. Step 8: `logMark =
  app.LogMark();` before the click (`:199`), without `var` (the method declares `logMark` at `:133`); `AssertNothingReads(app)` after `app.Settle()` (`:200`);
  `Assert.Empty(PassLines(app, logMark))` after `:208`; the comment `:206-207` says every row was read
  0 s before by the held clock, so the activation starts no pass (WD35).
- W1 part B (step 10's count, 0): `:244-249`'s `StartWithTabs` with `readClock:`.
- W2: `Phase6Checks.W.Rows.cs:51` `OpenWorkspaceAsync(data, copy, readClock: new ManualTimeProvider())`.
- W9: the second start, `Phase6Checks.W.Restart.cs:72`, with `readClock:`.

None relies on real seconds: the clock feeds only the 10 s rule and the patience timers
(`DashboardViewModel.cs:539, 661`; `DashboardRowViewModel.cs:258, 575`). On a held clock a pass ends
only when its reads have (`RunPassAsync`, `:601-707`); `DashboardRefreshTests` still pins the overdue
rule. The clock is shown nowhere: no picture changes.

Report note (7a's "Notes"): "Phase 6's W1 (both methods), W2 and W9's second start now give the app a
held read clock (`ManualTimeProvider`, never moved), as W8 did. On 2026-10-26, under a full run's
load, W1 logged `read 7 repositories` where 8 were expected: a read's git call ran past
`GitCallPatience` (10 s on the session's real clock) and its pass ended without it. On a held clock no
patience timer fires, so a pass ends only when its reads have. Steps, expected results and pictures
are unchanged; W1's step 8 also asserts that the dashboard's activation read nothing and started no
pass."

### 5.5 G23's split (D283)

`Phase6ChecksG.Check_G23_pull_refused_inside_the_integration` (`Phase6Checks.G.cs:309-440`) becomes
three `[AvaloniaFact]`s in its place, each building IG1 as step 9 does (`:322-325`) before its own
`OpenAsync` and ending with step 15's checks (`:434-439`). Steps keep their numbers, assertions and
picture names; doc comments split `:309-315`'s text.

| Test | Before the start | Steps | Pulls | Pictures |
|---|---|---|---|---|
| `Check_G23_pull_rebase_refused_inside_the_integration` | IG1, `pull.rebase = merges` | 9 (`:327-336`), 10, 11, 15 | 2 | `g23g-pull-rebase-merges-blocked` |
| `Check_G23_pull_merge_that_adds_nothing_goes_on` | IG1 | 9, 12 without its `--unset`, 15 | 1 | none |
| `Check_G23_pull_merge_refused_then_ff_only_refused_by_git` | IG1 and `branch.main.merge = refs/heads/vendor` (IG1v) | 9 on IG1v, 13 without its outside write and tooltip wait, 14, 15 | 2 | `g23h-pull-merge-blocked`, `g23i-pull-not-fast-forward` |

Step 9 on IG1v asserts `PartG.Ig1vPanel` and `PartG.PullFromOriginVendor` (`Phase6Expectations.G.cs:244,
489`). Step 14 still writes `pull.ff = only` through `ConfigOutsideAsync`. Three tests: two more app
starts than today.

How each picture's state is reached (2.56; today's path and the split's compared file by file in `.git`):
- Each pull's `git fetch --progress origin` prints nothing and moves no ref; it writes `FETCH_HEAD`.
  Refs and reflogs are the same on both paths.
- Step 12's `git merge --no-edit --no-overwrite-ignore FETCH_HEAD` prints `Already up to date.`,
  exits 0, adds no reflog entry and writes `ORIG_HEAD` = HEAD.
- `git config --unset pull.rebase` removes the emptied `[pull]` section: after steps 10 to 13
  `.git/config` is byte for byte the third test's.
- `g23g`: today's steps 9 to 11 on today's state: identical.
- `g23h`: the repository differs only in `ORIG_HEAD` (today the side merge, from step 12; split "Main
  work"), which `src` never reads; the window keeps no trace of the earlier pulls. Expected identical.
- `g23i`: step 14's refused `git merge --ff-only` (exit 128, `fatal: Not possible to fast-forward,
  aborting.`) writes `ORIG_HEAD` = HEAD on both paths: identical.
- 2.43 and 2.55: not run; an older git may keep the empty `[pull]` header (today's path only), and
  the app's `git config --get pull.rebase` exits 1 either way.

Report note: "G23's part B (`Check_G23_pull_refused_inside_the_integration`, steps 9 to 15, five
pulls) is split by D264 into three tests: steps 9 to 11 (`g23g`); 9 and 12; 9, 13 and 14 (`g23h`,
`g23i`). Each builds IG1 before its own start with the configuration its steps need and ends with step
15's checks; the third starts on IG1v (`branch.main.merge = refs/heads/vendor` written before the
start, where step 13 wrote it while the app ran). Reason: the hang watchdog stopped G23 after 180 s in
all four default runs on Git 2.56, each pull 10 or more times slower under the run's load (D237),
while G23 alone passes in about 40 s. Steps, expected results and pictures are unchanged; at `g23h`
the repository differs only in `ORIG_HEAD`, which the app never reads."

### 5.6 Tests the builder runs; commits

Before changing part W or G, keep reference pictures (each run empties `artifacts/visual/phase-6/scripted/`):

```
dotnet test --project tests/VisualCommit.VisualTests.Phase6 --filter-method "*Check_W1_*" --filter-method "*Check_W2_*" --filter-method "*Check_W9_*" --filter-method "*Check_G23_pull_refused_inside_the_integration"
mkdir -p ../p7a-wp1-reference && cp artifacts/visual/phase-6/scripted/w[129][a-z]-*.png artifacts/visual/phase-6/scripted/g23[ghi]-*.png ../p7a-wp1-reference/
```

At each commit, after `dotnet build`:

```
dotnet test --project tests/VisualCommit.Git.Tests --filter-class "*RepositoryWatcherTests" --filter-class "*IgnoredInTheWayTests" --filter-class "*RefReadingTests" --filter-class "*Phase6ScenarioTests"
dotnet test --project tests/VisualCommit.App.Tests --filter-class "*GitCallBudgetTests" --filter-class "*UndoStepTests" --filter-class "*RepositoryOperationsFetchTests"
dotnet test --project tests/VisualCommit.VisualTests.Phase6 --filter-class "*Phase6SupportTests"
dotnet test --project tests/VisualCommit.VisualTests.Phase6 --filter-method "*Phase6ChecksS.*" --filter-method "*Check_K10_*" --filter-method "*Check_K13_*" --filter-method "*Check_W8_*"
dotnet test --project tests/VisualCommit.VisualTests.Phase6 --filter-method "*Check_W1_*" --filter-method "*Check_W2_*" --filter-method "*Check_W9_*" --filter-method "*Check_G23_*"
for f in ../p7a-wp1-reference/*.png; do cmp "$f" "artifacts/visual/phase-6/scripted/$(basename "$f")"; done
```

The last line prints nothing. Then with `VISUALCOMMIT_WATCHER_LAG_MS=1500` in front:
`RepositoryWatcherTests`, the App.Tests command and the S/K10/K13/W8 command (with `*Check_W1_*`).

Commits: (1) R8; (2) S8's harness (`GitCallBudget`, `ShellDriver.Budget.cs`, `OperationsHarness`,
`GitCallBudgetTests`, part S's helpers, `Phase6SupportTests`); (3) S8's conversions; (4) W1's held
clock, after the reference pictures; (5) G23's split, then the comparison.

### 5.7 At the merge; done when

The lead merges with `--no-ff`, builds at the root and runs 0.4's lagged list (its first run); adds
5.4's and 5.5's notes and S8's to the 7a report: "S8 (D254): three counts of git calls that are a cost
became budgets (`IgnoredInTheWayTests`, `RefReadingTests`, `Phase6ScenarioTests`); seven with a live
watcher are now marked and read with the watchers quiet and whole refreshes set aside (K10, K13, W8
steps 7 and 9, W1 step 10, `UndoStepTests`, `RepositoryOperationsFetchTests`); counts that are a
test's meaning stay exact."; adds a CLAUDE.md row for the lagged run once it has worked; at closing,
`docs/architecture.md` names the lag switch (test-only, D262) and `GitCallBudget`.

Done when the build passes; 5.6's classes pass unlagged and lagged at 1,500 ms; without the variable
the watcher wires its handlers exactly as before; `RefreshForms` and `NotWholeRefreshes` exist only in
`GitCallBudget`; `ShellDriver.cs` is untouched; 5.3's conversions are made and nothing on its "left
exact" list changed; W1, W2 and W9 hold their read clocks; G23 is three tests of at most two pulls and
`cmp` prints nothing for `g23g`, `g23h`, `g23i` and every `w1*`, `w2*` and `w9*` picture.

## 6. WP-G: GitHub's API client and provider

### 6.1 Goal and files

Status steps 6 (recognition), 8 (GitHub's API) and 14 (avatar download and cache): the client,
`GitHubProvider : IHostingProvider`, and `AvatarCache`, tested against `FakeGitHub`'s in-process
handler. Carries D252, D257's no-token rule from the client's side, D258, rulings 8, 14, 15, 22, 23;
D284 to D286; D336 (`index.json`'s camelCase). Out: the session, accounts, tokens' storage, bindings, the e-mail maps (WP-S), UI.
Wave 1 after WP-F's merge, branch `wip/p7a-github`. Owns `HostingOptions.cs`, `GitHubApi.cs`,
`HostingRecords.cs` and `IHostingProvider.cs` after WP0, with no signature change. WP-G runs no git.

| File | Change |
|---|---|
| `src/VisualCommit.Hosting/GitHub/GitHubClient.cs` | new, internal |
| `src/VisualCommit.Hosting/GitHub/GitHubJson.cs` | new: internal DTOs and `GitHubJsonContext` (source generation, `JsonKnownNamingPolicy.SnakeCaseLower`, unknown fields ignored) |
| `src/VisualCommit.Hosting/GitHub/GitHubProvider.cs` | new (1.4's constructor) |
| `src/VisualCommit.Hosting/GitHub/GitHubUrls.cs` | new, internal: recognition |
| `src/VisualCommit.Hosting/AvatarCache.cs` | new (1.4); `index.json` through its own source-generated context with `JsonKnownNamingPolicy.CamelCase` (D336), never `GitHubJsonContext` |
| `src/VisualCommit.Hosting/HostingOptions.cs` | `DefaultHandler` as 6.2 says, if WP0's differs |
| `tests/VisualCommit.Hosting.Tests/GitHub*Tests.cs`, `AvatarCacheTests.cs`, `HostingOptionsTests.cs` | new |

### 6.2 The client (`GitHubClient`) (D285)

```csharp
internal sealed class GitHubClient : IDisposable
{
    public GitHubClient(GitHubEndpoints endpoints, Func<HttpMessageHandler> createHandler, IAppLog log, TimeProvider clock, TimeSpan? timeout = null);
    public static string UserAgent { get; }   // "VisualCommit/0.1.0"
    public Task<GitHubReply<T>> SendAsync<T>(HttpMethod method, string token, Uri url, object? body, JsonTypeInfo<T> type, CancellationToken ct);
    public Task<GitHubReply<List<T>>> GetPagesAsync<T>(string token, Uri first, JsonTypeInfo<List<T>> type, int maxPages, CancellationToken ct);
}
internal sealed record GitHubReply<T>(T? Value, HostingFailure? Failure, int Status, bool MoreExist,
    string? OAuthScopes, string? Sso, string? RequestId);
```

- One `HttpClient` over `createHandler()` per client, `Timeout` infinite; each request gets a linked
  token cancelled after 30 s (`timeout`, settable for tests). The caller's cancellation throws
  `OperationCanceledException`; the 30 s running out is a `Network` failure.
- Headers on every request: `Accept: application/vnd.github+json`, `X-GitHub-Api-Version:`
  `GitHubApi.Version`, `User-Agent: VisualCommit/<version>` (the Hosting assembly's informational
  version up to its `+`, `0.1.0` today; `Directory.Build.props:17`), and `Authorization: Bearer
  <token>` only when the request's scheme, host and port equal `ApiBase`'s. POST and PATCH bodies are
  JSON, `application/json; charset=utf-8`. Responses over 10 MB are `Malformed`.
- **Redirects by hand** (the handler's `AllowAutoRedirect` is false): 301, 302, 307 and 308 are
  repeated to `Location` (resolved against the request's URL) with the same method, body and
  headers, at most three hops, and only while its scheme, host and port are `ApiBase`'s. A redirect
  to another scheme, host or port (no request is sent there; the warning's `<status or kind>` is
  `redirected to <scheme>://<host>:<port>`, never a path or query), a fourth redirect, or one without
  `Location` is `Malformed`.
- **Paging.** `GetPagesAsync` follows `Link: rel="next"` as given, only while its scheme, host, port
  and path prefix are `ApiBase`'s (else `Malformed`, logged), up to `maxPages`; `MoreExist` when a
  `next` remains after the cap. Caps: 10 pages of repositories (1,000), 3 of pull requests (300).
- **One request at a time** per client (a `SemaphoreSlim(1)` waited with the caller's token; the 30 s
  starts once sent), as GitHub asks.
- **ETags** in memory: per (SHA-256 fingerprint of the token, URL), the last 200 response's `ETag` and
  body; sent as `If-None-Match`; a 304 returns the kept body. Never on disk; dropped with the client.
- **Classification** into `HostingFailure` (r5 7.2), in order:

| Condition | Kind | `Message`, `Detail`, `RetryAt` |
|---|---|---|
| No response (`HttpRequestException`, `IOException`) | `Network` | "Could not reach GitHub." (D284); the exception to the log only |
| The 30 s ran out | `Network` | "GitHub did not answer within 30 seconds." |
| 401 | `BadCredentials` | GitHub's `message` |
| 403 and `X-GitHub-SSO: required; url=…` | `SsoRequired` | `message`; `Detail` the URL |
| 403 or 429 with `X-RateLimit-Remaining: 0` | `RateLimited` | `message`; `RetryAt` from `X-RateLimit-Reset` |
| 403 or 429 with `Retry-After`, or a `message` containing "secondary rate limit" | `RateLimited` | `message`; `RetryAt` now plus `Retry-After`, else 60 s |
| other 403 | `Forbidden` | `message`; `Detail` `X-Accepted-GitHub-Permissions` |
| 404 | `NotFound` | `message` |
| 410, or 400 whose `message` names `X-GitHub-Api-Version` | `ApiVersion` | `message` |
| 422 | `Validation` | `message`; `Detail` A5's |
| 5xx | `ServerError` | `message`, else "GitHub had a problem: HTTP <code>." |
| anything else, or a body that does not parse | `Malformed` | "GitHub's answer could not be read." |

  `RequestId` is `X-GitHub-Request-Id`; `Url` the request's URL without its query. 409 is passed as
  `Status` without a failure (A6's empty repository). "Now" is the client's `TimeProvider`.
- **Logging.** A failure is one warning, `GitHub: <method> <path without query> failed: <status or
  kind> (request <id>).`; a network failure passes the exception. Never a header, body, query string,
  token, e-mail address or request or response object. No line for a success.
- **Threads.** All async, `ConfigureAwait(false)`; JSON parsed on the pool; callers post results.

`HostingOptions.DefaultHandler(endpoints)`: a `SocketsHttpHandler` with `AllowAutoRedirect = false`,
`AutomaticDecompression = DecompressionMethods.All`, `UseCookies = false`, `ConnectTimeout` 10 s, and
`UseProxy = false` when `ApiBase`'s host is a loopback address (the environment proxy does not skip
loopback by itself), the system's proxy otherwise.

### 6.3 `GitHubProvider`

`Kind` `GitHub`; `Endpoints`. Paths relative to `ApiBase`, each segment escaped with
`Uri.EscapeDataString` (a branch's `/` as `%2F`).

- **`Recognise(url)`** (r1 11; no network; null otherwise). With `GitHubCom`, host `github.com` or
  `www.github.com` (case-insensitive) in: `https://[user[:pw]@]host[:443]/o/r`,
  `http://[user@]host[:80]/o/r`, `ssh://[user@]host[:22]/o/r`, `ssh://[user@]ssh.github.com:443/o/r`,
  scp-like `user@host:o/r`, `git://host/o/r`. With `Local(port)`: only `http://127.0.0.1:<that port>/o/r`
  (ruling 23; phase 3's `:47391` and phase 6's `:47393` are not GitHub). Path exactly two segments,
  then an optional `.git` (once) and `/`; owner `^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$`, name
  `^[A-Za-z0-9._-]{1,100}$` and not `.` or `..`; no query, fragment or percent escape. Owner and name
  as written; the user part is never kept.
- **`SourceCanExist(ref)`** (ruling 8): false only for `refs/pull/<x>/head` or `/merge` where `<x>` is
  not one or more ASCII digits (`refs/pull/fix/typo/head` included); true otherwise.
- **A1 `CheckTokenAsync`**: `GET user` → `HostingAccount(new AccountKey(GitHub, Endpoints.Host, login),
  login, id, name, avatar URL (6.4's rule, else null), UseForGit: true)`. An empty token is refused
  without a request (`BadCredentials`, "The token is empty.").
- **A2 `ListRepositoriesAsync`**: `GET user/repos?per_page=100&sort=updated`, up to 10 pages; never
  `type`. Each item → `RepositoryInfo` (`CanPush` from `permissions.push`, `Parent` null);
  `SomeHiddenBySso` when any page has `X-GitHub-SSO` starting `partial-results`.
- **A3 `ReadRepositoryAsync`**: `GET repos/{o}/{r}` (ETag); a fork's `parent` → `Parent` and
  `ParentDefaultBranch`. A 301 to `repositories/<id>` is followed.
- **A4 `ListPullRequestsAsync`**: `GET repos/{o}/{r}/pulls?state=open&per_page=100`, up to 3 pages
  (`MoreExist`). `Author` `user.login` or null; `HeadRepository` from `head.repo`, null when
  `head.repo` is null (a deleted fork); `IsDraft` `draft`.
- **A5 `CreatePullRequestAsync`**: `POST repos/{o}/{r}/pulls` on the base repository with `title`,
  `body`, `base`, `draft`, `head` = `Head`, or `"{HeadOwner}:{Head}"` across forks, and
  `maintainer_can_modify: true` exactly when `HeadOwner` is set. 201 → the `PullRequest`. Never
  retried. A 422's `Detail`: `errors[0].message` when present ("A pull request already exists for …",
  "No commits between …"), else `<field> is invalid` (`invalid`), `<field> is missing` (`missing`,
  `missing_field`), `<field> already exists` (`already_exists`), else GitHub's `message`. WP-N matches
  these forms.
- **A6 `ReadAuthorsAsync`** (D252): with a branch, `GET repos/{o}/{r}/commits?sha={branch}&per_page=100`,
  first page only: each commit's lower-cased `commit.author.email` → `AuthorAvatar(author.id,
  author.login, author.avatar_url)`, or null when `author` is null or `{}`; the first commit of an
  e-mail wins. With a null branch, no commits page. Then, for at most 20 e-mails of `moreEmails` not
  yet in the map (lower-cased, distinct, in order; the caller's addresses only, which 7.9 limits to
  commits on GitHub remotes' branches, D311), `GET …/commits?author={email}&per_page=1`: an empty
  list or a null author → null. A first page answering 409 → an empty map, succeeded. A failing lookup
  stops the lookups and leaves its e-mail out (not null); a failing first page fails the call.

### 6.4 `AvatarCache` (D286)

`AvatarCache(string directory, Func<HttpMessageHandler> createHandler, TimeProvider clock, IAppLog log)`,
`directory` = `AppPaths.AvatarsDirectory`; `FileOfAsync(AuthorAvatar, CancellationToken)`; `Lifetime`
7 days.

- **Images** `<id>.png` or `<id>.jpg`, keyed by the GitHub user id. Fetched when missing or when
  `index.json` says it was fetched more than `Lifetime` ago by `clock`; a failed refresh keeps the old
  file. The request: `avatar_url` with `s=64` added to its query (r5 I1, undocumented; harmless if
  ignored), `User-Agent` as the client's, `Accept: image/png, image/jpeg`, no `Authorization` ever, 30 s,
  redirects by hand as 6.2 but only while scheme, host and port stay the avatar URL's own (any other
  redirect: null and the warning "The avatar of user <id> was redirected to <scheme>://<host>:<port>.",
  no request there), at most 1 MB, at most four downloads at once, one per id at a time. A
  download runs under the token of the call that started it; once that is cancelled it writes neither
  the file nor `index.json` (the sign-out deletes `avatars/` after its runs end, 7.5). Kept
  only when the bytes start with PNG's signature (`89 50 4E 47 0D 0A 1A 0A`) or JPEG's (`FF D8 FF`);
  otherwise null and a warning ("The avatar of user <id> is not a PNG or JPEG image."). Written to
  `DurableFile.TemporaryOf(path)`, flushed, then moved over the file, as `DurableFile.Replace` does
  (`src/VisualCommit.Core/DurableFile.cs:47-52`).
- **`index.json`**: `{"users":{"1003":{"file":"1003.png","fetchedAt":"2026-01-02T09:00:00Z"}}}` (camelCase,
  D336: its own source-generated context with `JsonKnownNamingPolicy.CamelCase`, never the snake-case
  `GitHubJsonContext`), through `DurableFile.Replace`. Dates come from `clock`, so the harness's frozen clock never expires anything
  within a run and a test moves a `ManualTimeProvider`.
- **Which avatar URLs** (D252): `GitHubProvider` keeps an `avatar_url` only when it is `https` on
  `avatars.githubusercontent.com` (github.com) or on `WebBase`'s scheme, host and port (the fake);
  else the author counts as unknown, with one warning per session. Every file operation off the UI
  thread; an I/O failure is a warning and null, never an exception to the caller.

### 6.5 Tests (Hosting.Tests, against `FakeGitHub.InProcess(root).CreateHandler()`)

FakeGitHub's members used: `AddUser`, `AddToken`, `Revoke`, `AddRepositoryAsync`, `AddPullRequest`,
`LinkEmail`, `MaxPerPage`, `FailNext` with `FakeGitHubFault` (`Moved`, `MovedTo(Uri)` among them),
`HoldNext`, `Requests` (`Host`, `TokenPresent`, `Conditional`, `Body`), `FakeUser.AvatarBytes`.

- `HostingOptionsTests`: `GitHubCom` and `Local(47395)` with their hosts; `FromEnvironment` (unset →
  github.com, the fake's URL → `Local`, `http://localhost:47395/` and junk → `Off` with a warning);
  `DefaultHandler`'s settings; over a recording handler, A1 goes to exactly
  `https://api.github.com/user` with the four headers.
- `GitHubRecognitionTests`: every form above under both endpoints, with the refusals
  (`gist.github.com`, `github.com.evil.example`, one or three segments, `localhost`, ports 47391 and
  47393, `https://127.0.0.1:47395/…`, file paths); `SourceCanExist`'s table.
- `GitHubClientTests`: the headers on each route; `MovedTo` another host, for a GET and for a POST
  (and to `https` on the fake's port) → `Malformed`, no request there; `Moved` followed with the
  token; four hops → `Malformed`; paging with `MaxPerPage = 2` (A2 at 10 pages, A4 at 3,
  `MoreExist`); a `Link` to another host → `Malformed`; a second identical call is `Conditional`, 304,
  same value, and another token's is not; each fault to its kind (`RetryAt` from the fake's reset, 60 s
  for the secondary, SSO's URL, the permission); a missing version header → `ApiVersion`; a held
  request and a 1 s `timeout` → the time-out text; cancelling a held call throws; a second call waits
  while the first is held; a log line has no query, token or e-mail.
- `GitHubProviderTests`: A1 to A6 from scenario-like data (a fork, a draft, a deleted fork, a closed
  one left out); A2 never sends `type`; A5 same repository (bare `head`, no `maintainer_can_modify`),
  across forks (`owner:branch`, `true`), draft, each 422's `Detail`, the fine-grained 403's `Detail`
  `pull_requests=write`, one POST per call; A6's map, `{}` and null authors, the 20-lookup cap with 25
  e-mails, a null branch asking no commits page, 409 → empty, a failing lookup left out; foreign
  avatar hosts dropped.
- `AvatarCacheTests`: a PNG saved as `<id>.png` with no token in the request, a JPEG as `.jpg`, text
  refused; within 7 days no request, after 7 days (moved clock) one, a failed refresh keeps the file;
  two calls for one id make one request; a `MovedTo` another host gives null, the warning and no
  request there, one to the fake's own avatar path is followed; `index.json` round trip; a held
  download whose call is cancelled writes neither the file nor `index.json` after its release.

The builder runs these five classes with `--filter-class`. Commits: (1) `HostingOptions`,
`GitHubUrls`, recognition, `SourceCanExist`; (2) `GitHubClient`, `GitHubJson`; (3) `GitHubProvider`;
(4) `AvatarCache`; each with its tests.

### 6.6 At the merge; done when

The lead runs the lagged list (0.4) only when WP-G merges after WP1; no CLAUDE.md row;
`docs/architecture.md`'s hosting section at closing. No visual checks of its own: parts B, P, N and G
show its results through WP-S. No earlier picture changes.

Done when the five classes pass; nothing in Hosting logs a token, header, body or query; a request
goes to no host but the API's with a token; no redirect is followed to another host; recognition never takes another loopback port;
`HostingRecords` and `IHostingProvider` are unchanged in signature; CI's five jobs are green.

## 7. WP-S: the hosting session

### 7.1 Goal and files

`HostingSession`, the one object behind `IHostingSession` (1.5), and `RepositoryHosting`, the per-tab
`IRepositoryHosting`. Carries the provider model's accounts, hosts and recognition (status step 6);
the session half of sign-in (D249, D253, D256, D263; rulings 16 to 19); the account's token for the
app's git calls, as bindings and as the credential source (step 9; D255, rulings 1, 4, 5, 6); the data
of the pull-request section and badges (D251, D257, rulings 10 to 15); the e-mail maps and avatar
files (step 14; D252, ruling 22); D287 to D293, D330 (the 401 check, 7.6), D332, D336 (camelCase
files). No UI. Wave 2, first; branch `wip/p7a-session`. No
part of its own: parts A, B, P, N and G show it. No earlier picture changes (both harnesses start with
GitHub off).

Create in `src/VisualCommit.Hosting/`: `HostingSession.cs` (7.3 to 7.7); `JsonAccountStore.cs` with
`AccountsJsonContext` (7.2); `RepositoryHosting.cs` (7.8) and `RepositoryHosting.Avatars.cs` (7.9);
`AuthorMapStore.cs` (the e-mail maps and the lookup budget, 7.9; its JSON through its own
source-generated context with `JsonKnownNamingPolicy.CamelCase`, D336, never `GitHubJsonContext`); `HostingRules.cs`, a static class of
pure functions (`ListedCandidate`, `Bindings`, `ByRef`). `IHostingSession.cs` stays as WP0 wrote it:
WP-S implements its members and adds none (section 1's rule). Change:
`src/VisualCommit.App/ViewModels/RepositoryViewModel.Hosting.cs` (7.10),
`tests/VisualCommit.Testing/Headless/GitCallBudget.cs` (the optional form) and
`tests/VisualCommit.Testing/AppLogFile.cs` (`PullRequestsListed`).

### 7.2 `accounts.json` (D287, D292)

`JsonAccountStore(string filePath, IAppLog log)` with `AccountsFile Load()` and `void Save(AccountsFile file)`.
System.Text.Json source generation, `PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase` (D336),
`WriteIndented`, comments and trailing commas allowed (as `JsonSessionStore.cs:134-141` and
`JsonSettingsStore.cs:121-126`); written with `DurableFile.Replace` (`src/VisualCommit.Core/DurableFile.cs:47`).

```json
{ "version": 1,
  "accounts": [
    { "provider": "github", "host": "github.com", "login": "octocat", "id": 583231,
      "name": "The Octocat", "avatarUrl": "https://avatars.githubusercontent.com/u/583231?v=4",
      "useForGit": true, "tokenState": "keychain" } ] }
```

- Never a token, nor a field that could hold one. `tokenState` is a string: `keychain` (saved in the
  store), `session` (memory only: no keychain, or the save failed), `refused` (GitHub refused it);
  missing or unknown: `keychain`.
- Missing file: no account. An unreadable file is moved to `accounts.unreadable.json`, a warning is
  logged, and the session starts with no account (as `JsonSessionStore.Load`, `:56-101`).
- The session uses the first record whose `provider` is `github` and whose `host` equals
  `Endpoints.Host`; other records are kept and ignored. A save replaces that record (or appends it).
- Writes run off the UI thread, one at a time under the session's gate. A failed write is logged as an
  error; the state in memory still changes.

### 7.3 The session object

`HostingSession(AppPaths paths, HostingOptions options, ITokenStore tokens, IAppLog log, Action<Action> post, TimeProvider clock)`.

- `IsOff` is `options.GitHub is null`. Off: `GitHub` and `Account` null, every call that would ask
  GitHub fails or returns empty without a request, `StartAsync` reads nothing, `ForRepository` returns
  `NullRepositoryHosting.Instance` (`NotGitHub`).
- On: `GitHub` is `new GitHubProvider(options.GitHub, options.CreateHandler, log, clock)`, disposed
  with the session; one `TokenVault(tokens, log)`; one `AvatarCache(paths.AvatarsDirectory,
  options.CreateHandler, clock, log)`; one `AuthorMapStore`.
- **The account slot.** A private sealed class (not a record: its generated `ToString` would print the
  token) holding `AccountStatus Status`, `string? Token`, `string TokenState`; `ToString()` gives the
  login and the state only. The current slot is swapped whole with `Volatile.Write` and read
  lock-free, so `GetAsync` and `Bindings` never wait on the UI thread.
- **The gate.** One `SemaphoreSlim(1)` serialises the start's read, a sign-in's second half, a
  sign-out, a refusal and the switch; no network call runs under it.
- **Events.** `AccountChanged` only through `post`, once per slot change; then `OnAccountChanged()` is
  posted to each live `RepositoryHosting` (a list under a lock).
- `Dispose` cancels the session's lifetime (the start's read, a sign-in's second half, refreshes,
  avatar fetches), disposes the provider and raises nothing more.

### 7.4 `StartAsync` (ruling 6, D287)

Called once when the window exists, on the thread pool (`Task.Run`, 7.11), with `ConfigureAwait(false)`
throughout: it never needs the UI thread to complete, and reaches it only through `post`. Off: nothing. Else
load `accounts.json`; no record for this host: no slot and no store call (a user who never signs in
never touches a keychain). With one, by `tokenState`: `session`: `NeedsSignIn`, `SessionEnded`;
`refused`: `NeedsSignIn`, `Refused` (neither calls the store); `keychain`: one
`vault.ReadAsync(TokenKey.Of(key), lifetime)`, the run's only keychain read: `Done` gives `SignedIn`
in the `Keychain`; `NotFound` `NeedsSignIn` with `TokenMissing`; `Failed` `NeedsSignIn` with
`ReadFailed` and the store's reason as `Note`. Then set the slot, complete `_started`, post the
events. No call to GitHub at start. Until then `Account` is null; `AppSession.HostingStarted` lets the
harness wait.

### 7.5 Sign-in, sign-out, the switch

**`SignInAsync(token, ct)`** (D256: at once; D263: replaces; D288):
1. `token` as given (the dialog trims it and refuses anything but printable ASCII, 8.4). Off: `Validation`, "GitHub
   is turned off."
2. `GitHub.CheckTokenAsync(token, ct)` (A1), not under the gate. Failure: `SignInResult(null, failure,
   null)`; nothing changes, the current account's state is kept (a new token's 401 never marks the
   current account refused). Cancelled: the exception goes to the caller; nothing changed.
3. From here the caller's token is no longer used; the session's lifetime token is. Wait for
   `_started` and take the gate.
4. The key `new AccountKey(HostingProvider.GitHub, Endpoints.Host, account.Login)`; `UseForGit` the
   previous record's value, or true when there was none (D255's default).
5. A previous record with another key: `vault.ForgetAsync(old)`; a returned reason is logged.
6. `vault.KeepAsync(TokenKey.Of(key), token, lifetime)` → `(Place, SaveFailedReason)`. `Keychain`:
   `tokenState` `keychain`, `NoteKind.None`. `Session` without a reason (D253): `session`,
   `NoKeychain`. `Session` with one (ruling 19): `session`, `SaveFailed`, the reason as `Note`.
7. Write the record; set the slot `SignedIn`; post `AccountChanged` and `OnAccountChanged()`.
8. Return `SignInResult(status, null, saveFailedReason)`.

**`SignOutAsync(ct)`**: wait for `_started`, take the gate, `vault.ForgetAsync(key, ct)` (its reason
is the return value), remove the record, clear the slot, post the events. The record goes even when
the keychain kept its entry. Then, still under the gate: 7.9's runs are cancelled and awaited,
`AuthorMapStore` drops its maps from memory (its lookup counts stay), and `avatars/` is deleted
whole, images, `index.json` and `authors/` (D291). A file that cannot be deleted gets one Warning
naming it; the rest still goes and the result is unchanged.

**`SetUseForGit(bool)`** (D216: Settings' Save): no slot or the same value: nothing. Else the slot's
`HostingAccount` `with { UseForGit = value }`, the record written, the events posted; bindings follow.

**`KeychainAvailableAsync`**: `vault.KeychainAvailableAsync`.

### 7.6 The credential source and refusals (rulings 4, 6)

- **`GetAsync(account, ct)`**, on the broker's thread: `await _started.Task.WaitAsync(TimeSpan.FromSeconds(10),
  clock, ct)`; on timeout log "GitHub: the keychain's read had not ended after 10 s; git's request for
  {host} went without the account's token." and return null. Then null unless the slot's key equals
  `account`, its state is `SignedIn`, `UseForGit` is on and it holds a token; else
  `new GitCredential(token)`.
- **A 401** (`BadCredentials`) from any call made with the account's token: when the key matches a
  `SignedIn` slot, under the gate: slot `NeedsSignIn`, `Refused`, token dropped (the vault's copy is
  not read again), record `tokenState` `refused`, events posted, logged once with its source ("git"
  or "the API, request {id}"). Another key: ignored.
- **`Refused(account)`** (git's `erase`; D330): returns at once. When the key matches a `SignedIn`
  slot, one A1 (`GET /user`) with the slot's token through the helper below, on the thread pool, 30 s
  at most; an erase while that check runs joins it. Its 401 marks the account as above, source "git".
  A 200 keeps the slot `SignedIn` and logs one Warning: "GitHub account {login} on {host}: git was
  refused, yet the token is valid (an http.extraHeader, a proxy or a redirect?)." Any other answer
  keeps it too, with the Warning "GitHub account {login} on {host}: git was refused; the token could
  not be checked: {message}" (D284's texts for `Network`). Another key: ignored. The broker's refused set and the operation's
  `RefusedAccount` stay as 4.6 says, and its error dialog says `GitCredentialsRefused` whatever the
  check finds (4.7).
- Every API call goes through one private helper: it takes the slot's token (no request without one:
  D257), calls the provider with the session's lifetime token linked to the caller's, and applies the
  401 rule.

### 7.7 Recognition, clones, the account's repositories

- `Recognise(url)`: `GitHub?.Recognise(url)`. `SourceCanExist(remoteUrl, remoteRef)`: true when off or
  not recognised; else `GitHub.SourceCanExist(remoteRef)`.
- `BindingsForClone(url)`: when the slot is `SignedIn` with a token and `UseForGit`,
  `CredentialBinding.Select([new RemoteEffectiveUrls("origin", [url], [url])], WebBase, key)`; else
  empty. `url` is the clone's URL after the user's `insteadOf` rules, as git uses it: WP-B applies them
  first (9.3, D331), so a rule that names another user leaves the clone unbound, as D290 does for a
  tab.
- `BindsGit`: not off, the slot `SignedIn` with a token and `UseForGit` (read lock-free).
  `BindingsFor(remoteUrls, rewrites)` (D332): empty unless `BindsGit`; else `HostingRules.Bindings`
  of `rewrites.Apply(remoteUrls)` with the slot's key, as `RepositoryHosting` with its rules known (7.8).
- `ListRepositoriesAsync`: A2 through the helper; signed out `Fail(BadCredentials, "Not signed in.")`
  without a request.
- `ForRepository(workingDirectory)`: off, `NullRepositoryHosting.Instance`; else a new `RepositoryHosting`, added to
  the live list, removed at its `Dispose`.

### 7.8 `RepositoryHosting`

State changes happen on the UI thread: `Update` and `OnAccountChanged` are called there; network work
continues with `ConfigureAwait(false)` and applies its result through `post`. `Bindings` is an
immutable list swapped whole (read from any thread: `AutoFetchScheduler`'s tick). `Changed` is raised
through `post` only when the state, the bindings or the avatars differ, compared member by member
(dictionaries by content).

**`Update(refs, rewrites)`**:
1. `effective = rewrites.Apply(refs.RemoteUrls)`; `_rulesKnown = WantsUrlRewrites` at this call.
2. `Remotes`: each remote recognised by its first effective fetch URL, else by its first configured
   `url`. SSH URLs are recognised (the API works by owner and name) but never bound.
3. The candidate (`HostingRules.ListedCandidate`): the first GitHub remote in `HostingRemoteOrder`.
   `State.Repository` is the candidate, or, once A3 has answered for it, its `Parent` when it is a
   fork. A changed candidate drops the A3 answer and the list.
4. Bindings, `ByRef` and the state (below).
5. The first `Update` with a candidate while signed in starts `RefreshPullRequestsAsync`; so does a
   later one whose candidate changed (D289).

**States**: no candidate: `NotGitHub`. Candidate, slot null or `NeedsSignIn`: `SignedOut`, list and
failure null (no request). Signed in without a list for the candidate: `Loading`. A refresh while a
list is held keeps `Loaded` with the old list until the answer. Answer: `Loaded` with the list, or
`Failed` with the failure and no list.

**Bindings** (`HostingRules.Bindings`): empty unless the slot is `SignedIn` with a token, `UseForGit`
is on and `_rulesKnown` (D290); else `CredentialBinding.Select(effective, Endpoints.WebBase, key)`
(WP-C's rules, D278).

**`ByRef`** (`HostingRules.ByRef(refs, remotes, list)`). Index the open pull requests by (head
repository's `FullName` lower-cased, `HeadRef`); a null `HeadRepository` gets no badge. A local branch
`refs/heads/x` with `UpstreamRemote` r (a GitHub remote) and `UpstreamRemoteRef` `refs/heads/x` (the
same name, ordinal; 12.3 step 3) maps when (`Remotes[r].FullName`, x) is in the index. A local branch
tracking another name (`topic` on `origin/main`) maps nothing (D313). A remote branch maps through `refs.SourceOf(fullName)`:
`IsBranch`, its remote a GitHub remote, its `RemoteRef` `refs/heads/n`, the same lookup. Two pull
requests on one key: the lowest number wins. O(refs).

**`RefreshPullRequestsAsync(ct)`.** Signed out or no candidate: no request. One run at a time; a call
during a run asks for one more, whose applied state completes its task. The caller's token ends only
its wait; the run goes on under the hosting's lifetime (cancelled at `Dispose`).
1. No A3 answer for the candidate: A3 through the session's helper (one kept per candidate; a failure
   is not kept). Failure: `Failed`.
2. The listed repository: the candidate, or its `Parent` when `IsFork` and `Parent` is set.
3. A4 on it (up to 300, `MoreExist`). Then post: `Loaded` or `Failed`, `ByRef` again, `Changed`, and
   on `Loaded` the Debug line `{repo}: listed {n} open pull requests of {owner/name}` ({repo} the
   working folder's name), after the state is applied (1.9).

**Triggers** (D289; no timer): the first `Update`, a changed candidate, sign-in (`OnAccountChanged`),
each succeeded network operation of the repository's queue (auto-fetch and WP-N's create included; a
repeated read is answered 304 by the ETag), and the section's menu.

**`OnAccountChanged()`**: bindings and state again; signed in: refresh; signed out or `NeedsSignIn`:
list, failure and `ByRef` cleared, `Avatars` `None`, bindings empty; `_rulesKnown` false until the
next `Update` made while `WantsUrlRewrites`.

**`ReadRepositoryAsync`**: the kept A3 answer of the candidate, or A3 now. **`CreatePullRequestAsync(baseRepository,
request)`**: A5 on `baseRepository` through the helper; on success, when `baseRepository` is the listed
repository, the new pull request goes on top of the held list at once (`ByRef` again, `Changed`; D293).

**`WantsUrlRewrites`**: the slot is `SignedIn` (with or without the switch) and not off.

### 7.9 Avatars (D252, ruling 22, D291)

`ShowAvatarsFor(emails)` (UI thread; WP-V passes the authors of the graph's commits that a GitHub
remote's branch reaches, 11.3, D311; GitHub is asked about no other address): copied and lower-cased,
then worked off the UI thread; a call during a run folds into one more with the latest set. Signed
out: `Avatars` stays `None`, no request, no disk read. A sign-out cancels a run, which then writes
nothing (7.5).
1. Wait for the listed repository (the A3 answer). The branch: `ParentDefaultBranch` when the listed
   one is the parent, else `DefaultBranch`.
2. The map: `AuthorMapStore` keeps one per repository, shared by its tabs, in
   `avatars/authors/github_<host>_<owner>_<name>.json` (lower case, `:` as `_`): `fetchedAt` and
   `authors` of `email`, `userId`, `login`, `avatarUrl`, `checkedAt` (camelCase, D336; user fields null for an
   e-mail GitHub does not know); `DurableFile`, one writer per file.
3. One `ReadAuthorsAsync(token, repository, branch or null, emails)`: the branch when the map is
   missing or its `fetchedAt` is 7 days old or more (by `clock`), else null; the e-mails the map lacks
   or whose `checkedAt` is 7 days old, at most 20 per repository per app session, shared by its tabs,
   in the set's order. A failure: logged, nothing more this run.
4. Images: for each e-mail that maps to a user, `AvatarCache.FileOfAsync`, four at a time. Then post
   `Avatars = new AuthorAvatars(fileByEmail)` and `Changed`. An e-mail GitHub does not know, or whose
   image could not be had, has no entry: it looks as today.

### 7.10 `RepositoryViewModel.Hosting.cs` and `GitCallBudget`

WP0 gives `Hosting`, `HostingState`, `Avatars`, `ApplyHosting(RepoRefs)`, the set-up method with the
`Changed` handler, `PromoteHosting()` and disposal. WP-S adds:
- **The rules read.** Fields `_rewrites` (`None`), `_rulesRead`, `_rulesGeneration`. `ApplyHosting(refs)`:
  `WantsUrlRewrites` false: `_rulesRead = false`, `_rewrites = None`, `Hosting.Update(refs, None)`.
  True and `_rulesRead`: `Hosting.Update(refs, _rewrites)` at once, then a new read. True and not yet
  read: only the read. The read: `Repository.ReadUrlRewritesAsync(_lifetime.Token)` (off the UI
  thread; the continuation on it, as the refs read's); a stale generation is dropped; else `_rulesRead
  = true` and, when the rules differ from `_rewrites` (`Rules.SequenceEqual`) or this was the first
  read, `Hosting.Update(refs, rules)`. A failed read logs a warning and keeps the old rules.
- **The `Changed` handler** also: `WantsUrlRewrites` false: `_rulesRead = false`, `_rewrites = None`;
  true, `_rulesRead` false and `Refs` set: start the read (a sign-in while the tab is open).
- **Quiet** (D281). `private Task? _rulesReading`, set on the UI thread at every start of the read;
  `IsHostingQuiet => Volatile.Read(ref _rulesReading) is not { IsCompleted: false }`. The runner
  records the call before the task completes (`GitRunner.cs:231`). No refresh awaits the read: that
  would miss the sign-in's and the first load's reads, and slow every refresh.
- **After a network operation.** In the set-up method: subscribe `Queue.Finished` (raised off the UI
  thread, `OperationQueue.cs:819`); post, and when `e.Operation.Kind == OperationKind.Network`,
  `e.Result.Outcome == OperationOutcome.Succeeded` and not disposed,
  `_ = Hosting.RefreshPullRequestsAsync(CancellationToken.None)`. Unsubscribe in the disposal.
  Summary-mode view models hold `NullRepositoryHosting.Instance` (1.7) and skip both, so
  `IsHostingQuiet` stays true; their network operations read the rules themselves (4.7, D332).
- **Promotion.** `PromoteHosting()` replaces the null object with `services.Hosting.ForRepository(Path)`,
  subscribes as above and raises `PropertyChanged` for `Hosting`.
- No `OnPropertyChanged` override (WP-V's partial has the only one).
- **`GitCallBudget`.** Add `RefreshForm("git config --null --get-regexp ^url\\.", …, Optional: true)`
  (D281): counted 0 times or as often as the other forms. Signed out (every earlier check) it is 0.

### 7.11 Wiring for the lead's `AppSession.Start`

After `readEnvironment ??=` (`:161`), before `CreateBroker` (`:217`):

```csharp
var hostingSession = new HostingSession(paths,
    hosting ?? HostingOptions.FromEnvironment(Environment.GetEnvironmentVariable, log),
    tokens ?? TokenStores.FromEnvironment(Environment.GetEnvironmentVariable, log),
    log, action => Dispatcher.UIThread.Post(action), TimeProvider.System);
var prompts = PromptDialogs.CreateBroker(promptHandler ?? new PromptDialogs(promptPresenter, log), log,
    hostingSession, gitEnvironment, gitVersion: () => gitFound.Task.IsCompletedSuccessfully ? gitFound.Task.Result.Version : null);
// TabServices (:227-245): …, readClock: readClock, hosting: hostingSession);
```

Both come from the process's environment (1.7, 1.8). The lead adds an `AppSessionTests` case: a
session in the tests' environment logs "GitHub: off." and `Token store: memory.`.

In the constructor after `Initialization` (`:63`): `HostingStarted = Task.Run(() => services.Hosting.StartAsync(_lifetime.Token));`.
In `Dispose`, after `Shell.CloseAll()` (`:279`): `Services.Hosting.Dispose();`.

### 7.12 What is logged, and never

Logged: "GitHub: {host}." or "GitHub: off." at construction (WP-R waits for it); the start's outcome, a
sign-in (with the token's place and any reason), a sign-out, a refusal (warning, its source and request
id), git's erase of a token the check found valid or could not check (7.6's two warnings), each naming
host and login, as "GitHub account octocat on github.com: token read from the keychain."; 7.8's
Debug line; failures at warning with kind, GitHub's message and request id. Never a
token, a `GitCredential`, a slot, an `Authorization` value or an HTTP request or response; exceptions
from the HTTP stack by message only.

### 7.13 Tests

In `tests/VisualCommit.Hosting.Tests`, over `FakeGitHub.InProcess(root)`, `FakeTokenStore`, a
`TempDirectory` data folder, `ManualTimeProvider`, and a `HostingTestThread` helper (one thread with
its own `SynchronizationContext`, used as `post` and where tests call `Update` and the refreshes):
- `HostingRulesTests`: the candidate order; fetch and push URLs bound through `Select`;
  `https://OctoCat@github.com/o/r` bound as `https://github.com/o/r`; another user's URL dropping the
  `/o/r` and `/o/r/sub` bindings; ssh, `git://`, other hosts not bound; the fake's port bound,
  `:47391` not; empty when signed out, switched off, refused or rules unknown; `ByRef` for a local
  branch, a remote branch, a fork's head, a deleted fork, and an open pull request whose head is `main`
  mapping local `main` but not `topic`, which tracks `origin/main`.
- `JsonAccountStoreTests`: a round trip whose text has 7.2's camelCase names exactly (`avatarUrl`,
  `useForGit`, `tokenState`); no token in the text; an unreadable file set aside;
  other hosts' records kept; a missing `tokenState` is `keychain`.
- `HostingSessionStartTests`: no record, no store call; each `tokenState` and read outcome to its
  state and `NoteKind`; one store read; off reads nothing; `AccountChanged` once; `GetAsync` waits for
  a held read (`FakeTokenStore.HoldNext`) and gives null after 10 s on the manual clock.
- `HostingSessionSignInTests`: success (one `GET /user`, the token only in `Authorization`); no
  keychain; save failed; a 401 and a network failure change nothing; cancelled before the answer
  (`HoldNext("user")`) changes nothing, after it completes; another login forgets the old key; the
  switch carried; sign-out with and without a kept entry; sign-in and sign-out serialised; a
  sign-out with images, `index.json` and a map on disk and an image held (`HoldNext("avatar:1003")`)
  leaves no `avatars/`, also after the hold's release.
- `HostingSessionCredentialTests`: `GetAsync` by key, state and switch; an A4 401 marks and persists
  `refused`; `Refused` does so only when the fake's `GET /user` then answers 401 (the token `Revoke`d);
  with 200 (the extraHeader's case) or a network failure the slot stays `SignedIn`, `accounts.json`
  is not written and one Warning is logged; two `Refused` calls while the check is held
  (`HoldNext("user")`) make one request; another key ignored; the next start after a refusal reads no
  store; `BindsGit` and `BindingsFor`: false and empty when off, signed out, refused or switched off;
  signed in, equal to a `RepositoryHosting`'s `Bindings` for the same URLs and rules.
- `RepositoryHostingTests`: each state, signed out with no request; a fork's parent listed; a changed
  candidate; three refreshes while `HoldNext("pulls:…")` give at most two A4; no `Changed` for an
  unchanged refresh; sign-out clears; `Dispose` cancels; a created pull request on top only for the
  listed base.
- `RepositoryHostingAvatarsTests`: one commits page; 25 unknown e-mails over two hostings of one
  repository give 20 lookups; an unknown e-mail not asked again; the map reused within 7 days (a null
  branch), read again after; images without a token; nothing while signed out.

Also in Hosting.Tests (D334): `RepositoryHostingWiringTests` (`[AvaloniaFact]`, a `TempRepo` with a
remote at the URL of a fake on port 0, `TabServices` built by hand with a `HostingSession`):
signed out, no `^url\.` call; signed in, one per refresh (mark and read each after `IsWatcherQuiet`)
and an `insteadOf` rule applied to the bindings; sign-in while open starts the read; a succeeded
`Network` operation refreshes the pull requests, a `Local` and a failed one do not; a summary view
model holds `NullRepositoryHosting.Instance`, and once promoted a real hosting; the Debug line once
per applied list. With a fake repository (as `RepositoryWatcherQuietTests`' `HeldRepository`) whose
`ReadUrlRewritesAsync` the test holds, `IsWatcherQuiet` is false after a sign-in with the tab open,
and after a refresh whose status read has ended, until the read is released. In `GitCallBudgetTests`:
the optional form's case.

The builder runs these classes only, for example `dotnet test --project tests/VisualCommit.Hosting.Tests
--filter-class "*HostingSession*"`.

### 7.14 Commits, merge, done when

Commits, each with its tests: (1) `HostingRules`; (2) `JsonAccountStore`; (3) the session; (4)
`RepositoryHosting`; (5) avatars and `AuthorMapStore`; (6) `RepositoryViewModel.Hosting.cs`,
`GitCallBudget`, `AppLogFile`.

The lead at the merge: 7.11 into `AppSession`; through `scripts/heavy-run.sh`,
`VisualCommit.Hosting.Tests` whole, the lagged list (0.4) and `Phase6ChecksS` (its counts must not move).

Done when 7.13's classes pass; no token in any file, log or call record of them (WP-F's tripwire over
their data folders); with GitHub off the app starts exactly as before (no new git call, no request);
the lead's wiring is in; CI's five jobs are green with each Test step under four fifths of its limit,
the containers' included (0.6).

## 8. WP-A: accounts and sign-in (part A)

### 8.1 Goal and files

The settings screen's Accounts section, the "Add GitHub account" dialog, the window's sign-in entry,
and part A's checks: the Delivers item "Sign-in to GitHub … in the settings screen's Accounts
section" (D249, D253, D256, D263; rulings 16 to 19; D294 to D298; D277's note on git older than 2.31),
status step 10. Wave 2, after
WP-S's merge and part A's checks (D244); branch `wip/p7a-accounts`. Out: avatars in Settings, the
refused token's error dialog (WP-C's, 4.7), the palette.

Create: `src/VisualCommit.App/ViewModels/Settings/AccountsSettingsViewModel.cs`;
`ViewModels/Dialogs/SignInDialogViewModel.cs` and `Views/Dialogs/SignInDialogView.axaml(.cs)` (found by
name, `Views/Dialogs/DialogViewLocator.cs:11`); `ViewModels/Operations/OperationTexts.Hosting.Accounts.cs`
(`OperationTexts.AccountTexts`); part A's code (8.7).

Change:
- `ViewModels/Dialogs/SettingsDialogViewModel.cs`: `SettingsScreenServices` (`:32-53`) gains
  `public Func<Task<bool>>? SignIn { get; init; }` and `public GitVersion? GitVersion { get; init; }`
  (the detected git's, null when unknown; D277) after `FileExists` (`:52`); the constructor (`:100`,
  after `Shortcuts`) makes `Accounts`; `SelectedSection`'s `NotifyPropertyChangedFor` (`:127`) adds
  `ShowsAccounts` (`=> SelectedSection == SettingsSection.Accounts`, beside `:136`); after Save's step
  2 (`:240-245`), `if (Accounts.IsChanged) _services.Hosting!.SetUseForGit(Accounts.UseForGit);`;
  `Accounts.Dispose()` in `OnCancel` (`:195-199`) and before `Complete(true)` (`:347`). The class
  comment (`:55-70`) names five sections and D256's exception.
- `Views/Dialogs/SettingsDialogView.axaml`: the nav button after `SettingsNavShortcuts` (`:77-85`,
  before `</StackPanel>` at `:86`); the section after Keyboard shortcuts' (before `</Panel>` at `:475`).
  `SettingsDialogView.axaml.cs`: `NavButtonOf` (`:37-42`) maps `Accounts` to `SettingsNavAccounts`;
  Up and Down (`:96-112`) walk the buttons already.
- `ViewModels/MainWindowViewModel.Settings.cs:59-72`: `SignIn = SignInToGitHubAsync`,
  `GitVersion = Git?.Version` (`MainWindowViewModel.cs:139`);
  `MainWindowViewModel.Hosting.cs` (WP0's): `SignInToGitHubAsync` (8.5).
- `tests/VisualCommit.Testing/Headless/ShellDriver.Phase6.Settings.cs:122-131`: `ChooseSection` maps
  `Accounts` to `SettingsNavAccounts`; `tests/VisualCommit.App.Tests/SettingsDialogViewTests.cs:159-195` (8.6).

### 8.2 Texts (`AccountTexts`; `HostingTexts` where named)

A `{reason}` or `{message}` loses a full stop at its end before the sentence's own.

| Id | Text |
|---|---|
| nav button, section; row label | `HostingTexts.AccountsSection` "Accounts"; `HostingTexts.GitHub` "GitHub" |
| `AccountsIntro` | "With a GitHub account, VisualCommit lists and creates pull requests, shows authors' pictures and clones your repositories." |
| `AccountsOff` | "GitHub is turned off for this session." |
| `AccountsSignedOut` | "Not signed in." |
| `AccountsSignInButton`, `AccountsSignOutButton`, `AccountsBusy` | "Sign in…", "Sign out…" (it opens the confirmation, as "Discard all changes…" and "Drop stash…" do), "Signing out…" |
| `AccountTokenPlace`, keychain | "The token is kept in this computer's keychain." (with `HostingTexts.Keychain`) |
| … `NoKeychain` (D253) | "No keychain was found on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again at the next start." |
| … `SaveFailed` | "The token could not be saved in this computer's keychain: {reason}. VisualCommit keeps it until it closes and asks for it again at the next start." |
| `AccountNeedsSignIn`, `Refused` | "GitHub refused the token. Sign in again with a new token." |
| … `TokenMissing` | "The token is no longer in this computer's keychain. Sign in again." |
| … `ReadFailed` | "The token could not be read from this computer's keychain: {reason}. Sign in again." |
| … `SessionEnded` | "The token was kept for the last session only. Sign in again." |
| `AccountsSignOutNote` | "Signed out, but the token could not be removed from this computer's keychain: {reason}." |
| second row label; `AccountUseForGit` | "Git"; "Use for git over HTTPS" |
| `AccountUseForGitNote` | "Clone, fetch, pull and push to GitHub over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers." (git 2.31 or newer, or the version unknown) |
| `AccountUseForGitOldGit` (D277) | "This needs Git 2.31 or newer. With Git {version}, clone, fetch, pull and push still use your git credential helpers." (in place of `AccountUseForGitNote` when the detected git is older than 2.31; `{version}` as `GitVersion.ToString()`) |
| confirmation | title "Sign out of GitHub?"; question "Sign out of GitHub as {login}? VisualCommit removes its token and the authors' pictures it saved from this computer, and no longer lists pull requests. Git over HTTPS asks your own credential helpers again." (D291) (the last sentence left out when the detected git is older than 2.31: git asked those helpers all along); detail empty; confirm "Sign out", `IsDestructive: false` (D296) |
| dialog title | `HostingTexts.AddAccountTitle` "Add GitHub account" |
| `SignInHelp` | "Paste a personal access token from GitHub. A fine-grained token needs read and write access to Contents and Pull requests of your repositories; a classic token needs the repo scope. To push changes to workflow files, it also needs Workflows (fine-grained) or workflow (classic)." |
| `SignInTokenLink` | "Create a token on GitHub"; tooltip "Opens GitHub's page for a new token in your browser." |
| token label; `SignInNoKeychain` | "Token"; the D253 text above, word for word |
| `SignInChecking` | "Checking the token…" |
| `SignInError`, `BadCredentials` | "GitHub refused the token: {message}." ("GitHub refused the token: Bad credentials.") |
| … `Network` | `{message}` as it is ("Could not reach GitHub.", "GitHub did not answer within 30 seconds.") |
| … any other kind | "GitHub answered: {message}." |
| … local | "The token can contain only letters, digits and punctuation, without spaces or line breaks." |
| `SignInSaveFailed` | "Signed in as {login}, but the token could not be saved in this computer's keychain: {reason}. VisualCommit keeps it until it closes and asks for it again at the next start." |
| buttons | "Cancel", "Sign in"; after a failed save, one "Close" |

Which text shows comes from `AccountStatus.State`, `Place` and `NoteKind`; `Note` is the `{reason}`.
No scope or expiry warning at sign-in (D297).

### 8.3 The Accounts section

`AccountsSettingsViewModel(IHostingSession? hosting, Func<Task<bool>>? signIn, IDialogService dialogs, IAppLog log, GitVersion? gitVersion = null)`
(the screen passes `SettingsScreenServices.GitVersion`):
`ObservableObject, IDisposable`. It reads `hosting.Account` and follows `AccountChanged` (on the UI
thread); `Dispose` unsubscribes. No git call and no request when the screen opens, so part S's counts
(`AssertScreenReadsAsync`) do not move.

Members: `IsOff` (null or `IsOff`), `IsSignedOut`, `HasAccount`, `Login`, `TokenPlaceText`,
`NeedsSignInText`, `ShowsSignIn` (signed out or needs sign-in, not off; D294), `ShowsSignOut` (an
account), `[ObservableProperty] bool UseForGit` (from the account; reset when `AccountChanged` brings
another), `IsChanged` (`HasAccount` and differs), `UseForGitNote` (`AccountUseForGitOldGit` when
`gitVersion` is known and below 2.31, else `AccountUseForGitNote`; D277), `BusyText`, `SignOutNote`,
`SignInCommand`, `SignOutCommand`, and `event EventHandler<string>? FocusRequested` that the screen
forwards (`SettingsDialogViewModel.cs:175`).

- **Sign in…** runs `signIn()` (the window's entry): the dialog stacks over the screen (D104);
  afterwards, signed in: focus to `AccountsSignOutButton`.
- **Sign out…** asks the confirmation through `dialogs.ConfirmAsync`, stacked; declined: nothing.
  Accepted: `BusyText`, both buttons disabled, `hosting.SignOutAsync(CancellationToken.None)` (at
  once, D256; the screen's Cancel cannot undo it); a returned reason sets `SignOutNote`; focus to
  `AccountsSignInButton`. `SignOutNote` clears at the next sign-in or when the screen closes.
- **The switch** applies at Save only (D216); Cancel and Esc drop it.

Layout, inside the shown-section panel (`SettingsDialogView.axaml:94`), as part S's sections:
`StackPanel Name="SettingsAccounts" Spacing="18"`, `IsVisible="{Binding ShowsAccounts}"`,
`DataContext="{Binding Accounts}"`:
1. `AccountsIntro`, class `secondary`, wrapping.
2. `Grid ColumnDefinitions="132,12,*"`: label "GitHub" (`settingsLabel`, `Margin="0,1,0,0"`); column 2
   a `StackPanel`: `AccountsOff`; or `AccountsSignedOut`; or `AccountLogin` (`FontWeight="SemiBold"`)
   then `AccountTokenPlace` (`settingsNote`) or `AccountNeedsSignIn` (`dialogWarning`); then
   `AccountsSignOutNote` (`dialogWarning`); then a horizontal `StackPanel` (`Spacing="8"`,
   `Margin="0,10,0,0"`) of `AccountsSignInButton` and `AccountsSignOutButton` (`VcActionButton`) and
   `AccountsBusy` (`secondary`). Off: no buttons.
3. With an account: `Grid` "Git" | `CheckBox` `AccountUseForGit` (`VcCheckBox`) and
   `AccountUseForGitNote` (`settingsNote`, its text `UseForGitNote`). The switch stays enabled on
   older git: it is kept for a newer one.

Nav button `SettingsNavAccounts`, as `SettingsNavShortcuts` (no error dot). At 1100×700 the card is
860×620 at (120, 40) and the new button 176×30 at (140, 223), 2 below "Keyboard shortcuts"; the
section's text from x 336, the row's controls from x 480 to 944. At 1920×1080 the card is at (530,
200), the button at (550, 383), the controls from x 890. Theme brushes only; no scrolling at 1100×700.

### 8.4 The "Add GitHub account" dialog

`SignInDialogViewModel(IHostingSession hosting, IFileLauncher? launcher, IAppLog log) : DialogViewModel<AccountStatus?>`;
its own `CancellationTokenSource`, cancelled in `OnCancel` (D234's `CancelAll` included);
`CancelledResult` is the signed-in status once a failed save has signed in, else null.

- Opening: `KeychainAvailableAsync`; false shows `SignInNoKeychain` before anything is typed.
- `Token` (masked `SecretTextBox`, `PasswordChar` `PromptDialogViewModel.MaskCharacter`,
  `PromptDialogViewModels.cs:21`; automation id `SignInToken`, name "Token"; its peer hides the text,
  D103/D130). Sign in is enabled when the trimmed token is not empty and no check runs.
- Sign in (Enter too): trimmed; any character outside printable ASCII, U+0021 to U+007E (a space, a
  line break, a control character, U+200B, U+FEFF, a non-ASCII letter): the local error, no request
  (D298; GitHub's tokens are letters, digits and `_`, and a non-ASCII header value would fail as
  `Network`). Else `IsChecking`: `SignInChecking`, the box and Sign in disabled, Cancel enabled;
  `hosting.SignInAsync(token, cts.Token)`.
  - Failure: the error text by kind, the box enabled with its text kept and selected, focus in it.
  - Success without a save reason: `Complete(status)`; `Token` cleared.
  - Success with `SaveFailedReason` (ruling 19, D298): `SignInSaveFailed`, the box disabled and
    cleared, Cancel hidden, the confirm button "Close"; Close, Enter and Esc close it signed in.
  - Cancel during the check: the dialog closes; if GitHub had already answered, the sign-in still
    completes (D288) and Settings shows it.
- The link (D295): `launcher.OpenUrlAsync(new Uri(endpoints.WebBase,
  "settings/personal-access-tokens/new?name=VisualCommit&contents=write&pull_requests=write"))`; the
  fake's base gives `http://127.0.0.1:47395/settings/…` in the launcher's record.

Layout (`dialogCard`, 400 wide; content 358): `DialogTitle` (`dialogTitle`); `SignInHelp`
(`dialogText`); the link (a `Button` with a local `ControlTheme` copied from `ParentLinkButton`,
`RightPanelView.axaml:19-34`: accent foreground, no padding, hand cursor, underlined on pointer-over
and while focused; `Margin="0,6,0,0"`, reached by Tab); the label "Token" (`promptLabel`, as
`CredentialsView.axaml:14-18`); the box; `SignInNoKeychain` (`dialogWarning`); `SignInChecking`
(`secondary`) or `SignInError` (`dialogError`) or `SignInSaveFailed` (`dialogWarning`);
`dialogButtons` with `CancelButton` (`VcActionButton`) and `ConfirmButton` (`VcPrimaryButton`). Root
automation id `SignInDialog`, name the title; no "In {repo}" line. At 1100×700 x 350 to 750, at
1920×1080 x 760 to 1160, centred vertically.

### 8.5 The window's entry

`MainWindowViewModel.Hosting.cs`: `private async Task<bool> SignInToGitHubAsync()`: off or no `GitHub`:
false, no dialog. `SignedIn`: true, no dialog. A sign-in dialog already open: await it. Else `await
Dialogs.ShowAsync(new SignInDialogViewModel(_tabServices.Hosting, _tabServices.Launcher, _log))` on
top of whatever is open (D104). Returns `Account is { State: AccountState.SignedIn }`. Failures are
logged and give false. Every sign-in item of the app (`HostingTexts.SignInToGitHubItem`, "Sign in to
GitHub…") ends here. No palette command signs in to GitHub or opens Accounts in 7a (the owner's answer
of 2026-10-29, D299); the palette reaches this method only through "Clone from GitHub…" while signed
out (9.5).

### 8.6 Earlier tests and pictures (0.7)

- `SettingsDialogViewTests.The_first_focus_is_the_section_button_up_and_down_move_and_enter_shows_without_saving`
  (`:159`): the third Down of `:177-179` now reaches `SettingsNavAccounts`; assert that, press Down
  once more (it stays), then Up twice to `SettingsNavTools`, and keep the Enter that shows Tools.
  `AssertNav`, `AssertFrame` (`Phase6Checks.S.Helpers.cs:181`, `:245-246`) and
  `Phase6Checks.L.Overlays.cs:604` name the four buttons by id and need no change. WP-A runs
  `Phase6ChecksS` and `--filter-method "*Check_L31*"` in `VisualCommit.VisualTests.Phase6Look`.
- For the writer, the 7a report's "Changes to expected results": "Phase 6's pictures that show the
  settings screen (checks S14 to S19 and L31; each one listed) change in the section list only: a
  fifth button, 'Accounts', 176 × 30, 2 below 'Keyboard shortcuts' (at 1100×700 x 140 to 316, y 223
  to 253; at 1920×1080 x 550 to 726, y 383 to 413), in the look of the buttons that are not selected.
  Nothing else in them changes. Reason: D256 adds the Accounts section, last in the list. The gate
  compares them by region against references made on master (plan risk 32)." The writer lists the
  pictures by opening each candidate of 0.7; pictures where the screen is closed do not change.

### 8.7 Part A's checks

The writer writes them into `docs/test-reports/phase-7a.md` before WP-A starts (D244); the builder
codes them: `Phase7aChecks.A.cs` (`public sealed partial class Phase7aChecksA : Phase7aChecks`, one
`[AvaloniaFact]` per group), `Phase7aChecks.A.Helpers.cs`; `Phase7aExpectations.A.cs` (nested `PartA`:
texts, picture names `a<nn><letter>-…`, R pictures as constants, positions, tolerances, written from
the report independently of `AccountTexts`); `Phase7aExpectationTests.A.cs` (`AccountTexts`,
`HostingTexts` and the link's URL equal `PartA`'s; names unique);
`tests/VisualCommit.Testing/Headless/ShellDriver.Phase7a.Accounts.cs`: `OpenAccountsAsync()`,
`AccountsSnapshot()` (through `SettingsSnapshot`), `SignInDialog`, `OpenSignInDialogAsync()`,
`TypeToken(string)` (`TypeInto("SignInToken", …)`), `ConfirmSignInAsync()` (waits for the check),
`SignOutAsync(bool confirm)`, `WaitForAccountAsync(Func<AccountStatus?, bool>)`.

Scope (about seven methods, several checks per start; the fake on 47395, a `FakeTokenStore`; R
pictures on one empty tab, 0.5):
1. Signed out: the section, five nav buttons, Accounts selected. R.
2. The dialog empty; a token typed and masked (the peer's value empty); the link's record in
   `FakeFileLauncher` (`OpenUrl`). R: the dialog with a masked token.
3. Checking (`HoldNext("user")`); Cancel then: nothing saved (no `accounts.json`, no store call).
4. Refused: an unknown token gives "GitHub refused the token: Bad credentials."; nothing saved.
5. Signed in with the keychain: the row, the switch on; `accounts.json`'s fields without the token;
   the store holds the key. R: the signed-in row. One picture at 1920×1080, one light.
6. Session only (D253): `Available = false`, the warning at opening, the note in the section,
   `tokenState` `session`, the store empty.
7. Save failed (ruling 19): `FailNextSave`, the dialog's message and its one Close, the section's note.
8. Sign out: the row's button reads "Sign out…" and the confirmation's own button "Sign out" (both in
   `PartA`); the confirmation's picture; afterwards signed out, store and file empty of the account; a
   sign-out whose delete fails shows its note.
9. Needs sign-in after a refused token, the API's way and git's (D330): `Revoke`, a repository with
   a GitHub remote opened (its pull-request read gets 401), the section's note; "Sign in…" again
   with `OctoClassicToken` works. Then that token `Revoke`d with `hello` open, and a fetch: its error
   dialog (WP-C's: "Fetch failed", the summary "GitHub refused git's credentials for octo. If the
   token has expired or was revoked, sign in to GitHub again in Settings, Accounts.", "Open
   settings…" opening Accounts), and the section's note again once the session's `GET /user` has
   answered 401. The fetch is the first request with that token after the revoke: after the API's
   401 the account binds nothing (7.6, 7.8), and git would ask through the credentials dialog. Its pictures of `hello`'s
   window show the left panel's "Pull requests" section, today's placeholder (WP0's stub) until WP-P
   merges and then
   "Sign in to GitHub to see pull requests…" (10.2): the expected result names that region as WP-P's
   and the check asserts nothing in it; the lead inspects these pictures again at WP-P's and WP-V's
   merges (0.4, 10.8, 11.6).
10. Needs sign-in after a start whose token is gone (`FakeTokenStore.Remove`) and after a session-only
    start; still signed in after a restart, with one store read and no `GET /user` at the second
    start (scripted only: one `FakeTokenStore` for two starts on one data folder).
11. The switch: off and Save gives `useForGit` false, the reopened screen shows it off, the
    repository's `Hosting.Bindings` is empty; Cancel after a toggle changes nothing.

Every method ends with `TokenTripwire.AssertAbsent` for each token over the data folder and the
repositories' folders, with the session's call log; every picture passes `LayoutAudit.FindClippedText`;
Esc on the dialog closes it only; Enter in the box signs in.

### 8.8 Tests besides the checks (in the 7a project unless named, 0.6)

- `AccountsSettingsViewModelTests`: each state and `NoteKind` to its texts and visibility;
  `IsChanged`; Save calls `SetUseForGit` once only when changed, Cancel never; a declined confirmation
  changes nothing; the sign-out note; with `gitVersion` 2.30.0 the note is `AccountUseForGitOldGit`
  ("With Git 2.30.0, …") and the confirmation lacks its last sentence, with 2.31.0 or null both are
  as 8.2 says; `Dispose` unsubscribes; off shows `AccountsOff` and no button.
- `SignInDialogViewModelTests`: enablement; the local error without a request (an inner space, a line
  feed, U+200B inside, a leading U+FEFF, which `Trim` keeps); each failure's text
  (`Network`'s message as it is); success; the failed save's state and its Esc; Cancel during the
  check cancels the token (the fake's held request ends) and nothing changes; the link's exact URL;
  the no-keychain line.
- `SignInEntryTests` (Hosting.Tests, D334; `[AvaloniaFact]`, `ShellDriver`): off gives false, no dialog; signed in gives
  true, no dialog; signed out stacks the dialog over an open one; two calls share one dialog.

The builder runs these three classes, `"*Phase7aChecksA"`, `--filter-method
"*The_first_focus_is_the_section_button*"` in App.Tests, and 8.6's two phase 6 runs.

### 8.9 Commits, merge, done when

Commits: (1) `AccountTexts`, the section's view model, its tests; (2) the section's view, nav button,
Save step, 8.6's test; (3) the dialog and its tests; (4) the window's entry; (5) part A's code.

The lead at the merge runs `Phase7aChecksA` with the other 7a classes, `SettingsDialogViewTests`,
`Phase6ChecksS`, `Check_L31*` and the lagged list; inspects part A's pictures.

Done when part A's checks and 8.8's classes pass; the phase 6 settings tests pass with 8.6's change
only; no token in any file, log, call record, automation value or picture text; the report's "Changes
to expected results" text is with the writer.

## 9. WP-B: browse and clone (part B)

### 9.1 Goal and files

"Browse and clone" (D261, ruling 5; D299 to D302, D331, D338), status step 11, part B. Wave 2, after WP-A's merge
and part B's checks (D244); branch `wip/p7a-clone`. It builds the clone form's "From GitHub…" button
(only while signed in), the repository browser, the keyless palette command "Clone from GitHub…"
(which signs in first), and the proof that a clone of a github.com URL takes the account's token
through the app's helper (WP0 wrote the call point, `WelcomeViewModel.cs:253`; WP-C and WP-S the
binding). WP-B holds no token. No earlier picture changes: the button is collapsed while signed out,
and the palette command is listed only while GitHub is not off.

Create: `ViewModels/Dialogs/CloneFromGitHubDialogViewModel.cs` (`CloneFromGitHubDialogViewModel :
DialogViewModel<string?>` and `GitHubRepositoryRow`); `Views/Dialogs/CloneFromGitHubDialogView.axaml`
and `.axaml.cs`; `ViewModels/Operations/OperationTexts.Hosting.Clone.cs` (`OperationTexts.CloneTexts`).

Change: `ViewModels/WelcomeViewModel.cs` (from WP0); `Views/Shell/WelcomeView.axaml:177-182`;
`ViewModels/Commands/AppCommand.cs:55` (`CloneFromGitHub` after `FollowSystemTheme`, so no value
before it moves); `AppCommands.cs:164` (its case in `Resolve`); `AppCommands.Palette.cs:82` (its row;
the file passes to WP-P at WP-B's merge); `ViewModels/MainWindowViewModel.Commands.cs:181`
(`CloneFromGitHubAsync()` beside `CloneRepository()`, using the private `TabForOpening()`, `:198`);
`tests/VisualCommit.App.Tests/AppShortcutsTests.cs:258-274` (`AppCommand.CloneFromGitHub` joins
`withoutKey`, with a comment naming D299); `ViewModels/Hosting/HostingTexts.Describe.cs` (from WP0:
`Describe`'s body as 1.7's table says, D338; it passes to the lead at WP-B's merge, and WP-P and WP-N
call it).

### 9.2 Texts (`CloneTexts`)

`FromGitHubButton` "From GitHub…"; `DialogTitle` "Clone from GitHub"; `PaletteCommand` "Clone from
GitHub…" (none of the words earlier checks typed in the palette); `Loading` "Loading repositories…";
`Empty` "No repositories to list"; `MoreExist` "Only the 1,000 most recently updated repositories are
listed."; `SomeHiddenBySso` "Some organisations' repositories are hidden until the token is authorised
for single sign-on."; `Unexpected` "The repositories could not be loaded."; `Choose` "Choose";
`Private`, `Fork`, `Archived`.

### 9.3 The clone form's URL row

`WelcomeView.axaml:178-182` holds `CloneUrlBox` directly in the form's `StackPanel`; it moves into a
`DockPanel` built as the parent-folder row (`:185-201`):

```xml
<DockPanel>
  <Button Name="CloneFromGitHubButton" DockPanel.Dock="Right"
          Theme="{StaticResource VcActionButton}" Margin="6,0,0,0" Padding="10,4"
          Command="{Binding BrowseGitHubCommand}"
          IsVisible="{Binding ShowsGitHubButton}" IsEnabled="{Binding CanEditClone}"
          AutomationProperties.AutomationId="CloneFromGitHubButton"
          AutomationProperties.Name="From GitHub…">
    <TextBlock Text="From GitHub…" />
  </Button>
  <TextBox Name="CloneUrlBox" … />   <!-- unchanged -->
</DockPanel>
```

A collapsed child takes no room in a `DockPanel`, so signed out the box is laid out as today. Phase
1's `13a` to `14b` and phase 6's `l26b`, `l32b` show it. Each also shows the status bar's `Git 2.56.0`
box, so its hash differs from `docs/test-reports/phase-6-pictures.sha256` whatever WP-B does (D248),
and `sha256sum -c` proves nothing. At WP-B's merge the lead makes references of those checks on
`master` with the same git and compares them by region (`scripts/compare-pictures.py`): no difference
outside the status bar's git box. The box keeps its name (`WelcomeView.axaml.cs:40`'s
focus). No tooltip.

`WelcomeViewModel`:
- `public bool ShowsGitHubButton => _services.Hosting.Account is { State: AccountState.SignedIn };`
  "needs sign-in" hides it too (D302). The constructor (`:27-34`) subscribes to `AccountChanged` and
  raises `ShowsGitHubButton`'s change; `Dispose` (`:293`) unsubscribes.
- `[RelayCommand(CanExecute = nameof(CanEditClone))] private async Task BrowseGitHubAsync()`;
  `IsCloning`'s `NotifyCanExecuteChangedFor` (`:93`) gains it. It makes the dialog with
  `_services.Hosting.ListRepositoriesAsync` and `_services.Log`, calls `_ = dialog.Start()` before
  `await _services.DialogHost.ShowAsync(dialog)` (as `WorkspaceCommands.cs:233-236`), and on an answer,
  unless a clone started meanwhile, clears `ErrorText` and sets `CloneUrl`; `OnCloneUrlChanged`
  (`:301`) fills the folder name unless the user typed one. The user presses Clone as today.
- The clone's scope: WP0's `Begin("Clone", name, PromptMode.Ask, bindings: …)` at `:253` stays; a
  typed URL is bound by the same rule, after the user's `insteadOf` rules as git applies them (D331).
  When `_services.Hosting.BindingsForClone(url)` of the typed URL is not empty, the clone first reads
  the rules once with phase 6's outside read, `_services.GitSettings.ReadAsync(null)` (`git config
  --show-scope --show-origin -z --list` in the data folder under its ceiling, local and worktree
  entries dropped: `git clone` applies the global rules but none of a repository around its folder,
  while `git config` run there lists that repository's too; measured on 2.56), takes
  `UrlRewrites.Parse` of its `url.` entries, and binds `BindingsForClone(rules.Apply([new
  RemoteUrl("origin", url, false)])[0].Fetch[0])`. A rule that rewrites the URL to another user (a
  global `url."https://alice@github.com/".insteadOf "https://github.com/"`) leaves it unbound: the
  user's helpers and git's dialog, D82's order. No `GitSettings`, or a failed read (one Warning,
  `Cloning {url}: git's configuration could not be read; the clone goes without the account's token.`
  with the URL through `GitCommand.HideCredentials`): no binding. Signed out nothing is read, so no
  earlier count moves. This read keeps an inherited `GIT_CONFIG_PARAMETERS`, so it lists inherited
  command-line `url.` rules (scope `command`), which the bound clone leaves out (D329); a difference
  changes only whether the clone is bound (unbound: D82's order), and the helper never answers another
  user (D275).

### 9.4 The repository browser (D301)

`CloneFromGitHubDialogViewModel(Func<CancellationToken, Task<HostingResult<RepositoryList>>> list, IAppLog log)`;
a sibling of `PickDialogViewModel`, which stays untouched.
- `Title` "Clone from GitHub"; no repository line.
- `Rows` (`ObservableCollection<GitHubRepositoryRow>`): the rows the filter lets through, in the list's
  order (most recently updated first), rebuilt in place as `PickDialogViewModel.OnFilterChanged` does.
- `GitHubRepositoryRow(string FullName, string? Detail, string CloneUrl)` with `ToolTip` (the full
  name, then the detail on a second line). `Detail` joins with ` · ` the flags `Private`, `Fork`,
  `Archived` (in that order, each when true), then the description when not empty (`Private · Fork ·
  A tiny demo`); null when nothing is left. `CloneUrl` is `RepositoryInfo.CloneUrl.AbsoluteUri` with
  any user part removed (`GitCommand.RemoveCredentials`; ruling 5).
- `Filter`: a row stays when its full name or detail contains the text, ignoring case; a selected row
  the filter hides is no longer selected.
- `Selected`; `ConfirmCommand` needs it; `Complete(Selected.CloneUrl)`.
- `Start()` begins the load once and returns it (`Load` exposes the task), on the dialog's own
  `CancellationTokenSource`, cancelled in `OnCancel` (Cancel, Esc, D234's `CancelAll`); the result is
  applied on the UI thread; one that arrives after the dialog closed changes nothing.

| State | `StatusText` (`settingsNote`) | `ErrorText` (`dialogError`) | Rows |
|---|---|---|---|
| Loading | `Loading repositories…` | — | none |
| Listed, some rows | — | — | the rows |
| Listed, none | `No repositories to list` | — | none |
| Failed | — | `HostingTexts.Describe(Failure, login)` (1.7, D338), `login` the signed-in account's | none |
| Unexpected exception | — | `The repositories could not be loaded.` | none |

`MoreText` (`CloneTexts.MoreExist` when `MoreExist`) and `SsoText` (`SomeHiddenBySso`), both
`settingsNote`, under the status. Log: a failure once, as a warning, `Listing GitHub repositories
failed: {Kind}: {Message} (request {RequestId})`; an unexpected exception as an error.

The view copies `PickDialogView.axaml`'s structure on the `wide` card (460):

| Element | Name and automation id | Notes |
|---|---|---|
| root | `CloneFromGitHubDialog`, name the title | `local:DialogView`; the filter takes the focus; Enter confirms with a selection |
| title | `CloneFromGitHubTitle` | `dialogTitle` |
| filter | `CloneFromGitHubFilter`, name `Filter` | `dialogBox`, placeholder `Filter`; enabled while loading, and what it holds filters the rows when they come |
| list | `CloneFromGitHubList`, name the title | as `PickList`: bordered, `MaxHeight="232"`, `MinHeight="58"`, no horizontal scroll bar; items 28 high, `Padding="10,0"`, Pick's styles |
| item | `CloneFromGitHubItem`, name `FullName`, help text `Detail` | a `PickRowPanel` (`PickDialogView.axaml.cs:44`): the full name, then 12 apart the detail in `VcTextSecondaryBrush` at 11.5 with `TextTrimming="CharacterEllipsis"` (cut at its end), hidden when null |
| lines | `CloneFromGitHubStatus`, `…Error`, `…More`, `…Sso` | each visible only with text |
| buttons | `CancelButton` `Cancel`, `ConfirmButton` `Choose` | `dialogButtons`; `VcActionButton`, `VcPrimaryButton` |

Centred, 460 wide at 1100×700 and 1920×1080; the list shows 8 rows before it scrolls; existing tokens
only.

### 9.5 The palette command (D299, D300)

- `AppCommands.Resolve`: `case AppCommand.CloneFromGitHub:` returns
  `new(new(!shell.Services.Hosting.IsOff, true, CloneTexts.PaletteCommand, null, null), () => Start(shell.CloneFromGitHubAsync(), CloneTexts.PaletteCommand))`.
- `PaletteCommands` (`AppCommands.Palette.cs:82`): `Add(AppCommand.CloneFromGitHub);` right after
  `Add(AppCommand.CloneRepository);`. Not listed while GitHub is off, so phase 6's K10 lists and
  `PaletteEntriesTests` stay. No key and no row in the Keyboard shortcuts list (`AppShortcuts.cs:242-247`
  lists keyed commands only), so `s14e`/`s14f` stay.
- `MainWindowViewModel.CloneFromGitHubAsync()`: (1) unless signed in, run `_tabServices.SignInToGitHub`;
  null or false: stop, no tab, no form; (2) `var welcome = TabForOpening().Welcome;`
  `welcome.OpenCloneFormCommand.Execute(null);` `await welcome.BrowseGitHubCommand.ExecuteAsync(null);`.
  Cancelling the browser leaves the form open.
- No other palette command: none signs in to GitHub or opens Accounts in 7a (the owner's answer of
  2026-10-29, D299).

Git fact (2.56): a clone run with the scope's `GIT_CONFIG_COUNT` pairs and `GIT_TRACE_REDACT=1` writes
none of them into the clone: `.git/config` held `core.*`, `remote.origin.url` (as given),
`remote.origin.fetch`, `branch.main.remote` and `branch.main.merge`, and no `credential.*` key. Git
never writes environment configuration, so 2.43 and 2.55 behave alike.

### 9.6 Tests (in the 7a project unless named)

- `CloneFromGitHubDialogViewModelTests` (a delegate for the session): loading, Choose disabled; rows
  in order with each detail form; the filter on name and description, a hidden selection cleared,
  the order restored; Choose answers the URL, a user part removed; the more and SSO lines; failures
  as `HostingTexts.Describe` words them (`BadCredentials` as `OperationTexts.TokenRefused(login)`,
  `Network` as it is, `RateLimited` with its time); the empty line; Cancel and Esc cancel the delegate's token, a late answer
  changes nothing.
- `CloneFromGitHubDialogViewTests` (headless): the view is found; ids and names; 460 wide; rows 28
  high; a detail only with text and cut at its end; each line per state; the error in
  `VcDangerBrush`; the filter focused; Enter confirms.
- `WelcomeCloneFromGitHubTests` (Hosting.Tests, D334; `ShellDriver` with `FakeGitHub`): the button absent with GitHub off
  and signed out, `CloneUrlBox` then as wide as `CloneNameBox`; it appears after `SignInAsync` and goes
  after sign-out; disabled while cloning; Choose fills the URL and the folder name, a typed folder name
  kept; a clone of the private `octo/hello` completes with no credentials dialog, `remote.origin.url`
  the chosen URL, no `credential` key, and `TokenTripwire.AssertAbsent(token, [clone, data folder],
  calls)` passes; `http://alice@127.0.0.1:<port>/octo/hello.git` opens git's credentials dialog as today;
  so does the typed `http://127.0.0.1:<port>/octo/hello.git` under a rule
  `url."http://alice@127.0.0.1:<port>/".insteadOf "http://127.0.0.1:<port>/"` in the session's global
  git file, with no `get` answered from the account; signed out, the clone makes no configuration
  read.
- `PaletteCloneFromGitHubTests` (Hosting.Tests, D334; `ShellDriver`): not listed with GitHub off; right after "Clone
  repository…" with the fake; signed out it runs `SignInToGitHub` first (a stub on `TabServices`):
  false leaves tabs and welcome page as they were; true opens the form on the empty active tab with
  the browser over it; signed in it does not ask.
- `AppShortcutsTests.The_commands_without_a_key_are_6_1s_list` (App.Tests) gains the command.
- `HostingTextsDescribeTests` (`[Fact]`): each row of 1.7's table, with and without a login, with and
  without `RetryAt` (the expected time computed by the same local-time conversion) and with an SSO URL
  that names no organisation; a `Message` ending in a full stop gets no second one; `BadCredentials`
  without a login and a `Message` of "Bad credentials" reads "Bad credentials. Sign in to GitHub in
  Settings, Accounts."

The builder also runs, unchanged: `AppShortcutsTests`, `PaletteEntriesTests`, `CommandPaletteViewTests`,
`PromptDialogsTests`, `PickDialogViewModelTests`; `--project tests/VisualCommit.VisualTests
--filter-method "*Phase1Checks.Check_13*"` and `"*Phase1Checks.Check_14*"`; in `Phase6Look`
`"*Check_L26*"` and `"*Check_L32*"`; in `Phase6` `"*Check_K10*"`. Never a whole project.

### 9.7 Part B's checks

Code: `Phase7aChecks.B.cs` (`Phase7aChecksB`), `Phase7aChecks.B.Helpers.cs`, `Phase7aExpectations.B.cs`
(texts, the list's rows in order, the R picture names), `Phase7aExpectationTests.B.cs` (pins them
against `CloneTexts`). Pictures `b<nn><step>-<what>.png`. They show (R: repeated in the real window):
the clone form signed in, with "From GitHub…"; the browser loading (`HoldNext("user/repos")`), listed
with private, fork, archived and described repositories (R, in the state the palette's path reaches:
empty tab, form open with empty boxes, filter focused and empty), filtered, and failed
(`FailNext("user/repos", …)`); Choose, then Clone of `octo/hello` through the app's helper with no
credentials dialog, the plain URL and no `credential` key, the token tripwire over the clone, the data
folder and the call log (R: in the real process, the helper run by Git for Windows' `sh` from the
laptop's `Visual Git`); the palette's "Clone from GitHub…" signed out: "Add GitHub account" first,
then the form and the browser; a cancelled sign-in changes nothing. The 1,000 cap and the SSO line
may be pictured with `AddListedRepositories(…, 1001)` and `SsoPartialResults`, else the unit tests
prove them. No picture of part B shows an opened GitHub repository, the clone included: its left
panel and graph are WP-P's and WP-V's, which merge later (0.4); the clone's result is asserted through
git, the call log and the tripwire.

Commits: (1) texts, `HostingTexts.Describe`'s body and `HostingTextsDescribeTests`, the view model and
its tests; (2) the view and its tests; (3) the welcome page,
`WelcomeCloneFromGitHubTests`; (4) the palette command, `CloneFromGitHubAsync`, `AppShortcutsTests`,
`PaletteCloneFromGitHubTests`.

### 9.8 At the merge; done when

The lead runs part B's checks, the classes above, 9.3's comparison and the lagged list;
`AppCommands.Palette.cs` passes to WP-P. Done when the five new classes and the listed ones pass; part
B's checks pass and their pictures are inspected; nothing writes or logs a token; no earlier picture
changed beyond D248's box.

## 10. WP-P: pull requests in the left panel (part P)

### 10.1 Goal and files

The pull-request list and menu and "open in browser" (D251, D257, rulings 10 to 15; D303 to D308;
D338, the error row's text; status steps 12 and 13; part P), and from phase 3 "Create pull request" in the branch and drop menus
and the section's menu (the form is WP-N's). Wave 2, after WP-B's merge and part P's checks (D244);
branch `wip/p7a-pull-requests`. It builds the left panel's "Pull requests" section for a repository
with a GitHub remote (today's placeholder stays exactly for every other); the pull request's and the
section's menus, "Create pull request…" and "Open pull request #n" in the branch and drop menus, the
palette's pull-request items; checkout (same repository, and through `FETCH_HEAD`), open in browser,
copy link, refresh, sign in; the left panel branch row's `#n`. WP-P holds no token. Examples below use
the scenario of 2.6.

Create: `ViewModels/Panels/PullRequestRows.cs` (`PullRequestRow`, `PullRequestMessageRow`,
`PullRequestMessageKind { SignIn, Loading, Failed, None, More }`); `ViewModels/Hosting/PullRequestCheckout.cs`
(the pure plan); `ViewModels/Menus/RepositoryMenus.Hosting.cs`; `ViewModels/Operations/OperationTexts.Hosting.PullRequests.cs`;
`tests/VisualCommit.App.Tests/PullRequestFetchHeadTests.cs` (10.6).

Change: `ViewModels/Panels/LeftPanelViewModel.cs`, `LeftPanelViewModel.Actions.cs`, `RefListRows.cs`;
`Views/Shell/LeftPanelView.axaml` and `.axaml.cs`; `ViewModels/RepositoryViewModel.Menus.cs`
(`OpenRefRowMenu` `:206`, `DoubleTapRefRow` `:411`, a new `ActivatePullRequestMessage`);
`ViewModels/Menus/RepositoryMenus.cs` (hooks at `:259`, `:330-332`, `:419-448`, `:631-664`);
`ViewModels/Commands/AppCommands.Palette.cs` (after the remotes, `:211-214`);
`RepositoryOperations.PullRequests.cs` (WP0's six stubs) and `src/VisualCommit.Git/GitRepository.PullRequests.cs`;
`Services/IFileLauncher.cs` and `FileLauncher.cs` (WP0's `OpenUrlAsync`, changed only where a test
shows it wrong); `tests/VisualCommit.App.Tests/HostingSessionStub.cs` (WP-C's; a settable `State` on its
repository hosting).

### 10.2 Texts (a part of `OperationTexts`; menu labels and reasons in `MenuTexts`)

| Name | Text |
|---|---|
| `SignInToSeePullRequests` | `Sign in to GitHub to see pull requests…` |
| `LoadingPullRequests`; `NoOpenPullRequests` | `Loading pull requests…`; `No open pull requests` |
| `MorePullRequests` | `More than 300 open pull requests` |
| `PullRequestHeader(n, title)` | `#{n} {title}` |
| `OpenInBrowser`; `CopyLink` | `Open in browser`; `Copy link` |
| check out | `MenuTexts.CheckOutName(head)`: `Check out feature/login`, `Check out ada:fix-typo` |
| `RefreshPullRequests`; `OpenPullRequestsInBrowser` | `Refresh pull requests`; `Open pull requests in browser` |
| sign in | `HostingTexts.SignInToGitHubItem` `Sign in to GitHub…` |
| `CreatePullRequest`; `OpenPullRequest(n)` | `Create pull request…`; `Open pull request #{n}` |
| `PullRequestsLoading` (reason) | `Pull requests are loading` |
| `AddRemoteFor(fullName)` (reason) | `Add a remote for {owner/name} to check out this pull request` |
| `PullRequestOfBranch(n, title)` | `Pull request #{n}: {title}` |
| `FetchPullRequest(n, remote)` | `OperationText("Fetch pull request #{n}", "Fetching pull request #{n} from {remote}…", "Pull request not fetched", "Git could not fetch pull request #{n} from {remote}.")` |
| `FetchedPullRequest(n, branch)` | `Fetched #{n} into {branch}` |
| `FetchHeadChanged` | `Another fetch changed FETCH_HEAD; try again.` |

None of these appears without a GitHub remote, so no earlier palette query meets them.

### 10.3 The section (D303, D304)

`LeftPanelViewModel` keeps `_hosting` (`RepositoryHostingState.NotGitHub` until the first
`UpdatePullRequests`, so every existing test sees today's panel). `UpdatePullRequests(state)` stores
it, drops `_selectedKey` when it is a `pr:` key no longer listed, and calls `Rebuild()`. `Rebuild`
(`:213`) at `:267-268`:

| `_hosting.PullRequests` | Header (`AddSection`, key `section:PullRequests`) | Rows when open, depth 1 |
|---|---|---|
| `NotGitHub` | today's line exactly: closed, 0, `canToggle: false` | — |
| `SignedOut` | count 0 | message `SignIn`, clickable |
| `Loading`, `List` null | count 0 | message `Loading` |
| `Loading`, `List` kept | the list's matches | the rows (a refresh does not flicker) |
| `Failed` | the kept list's matches, else 0 | message `Failed` (`HostingTexts.Describe(Failure, null)` as it is, 1.7, D338: the state holds no login, and a 401 ends `SignedOut`, 7.8; tooltip that text and, on a second line, `Detail`), then the kept rows |
| `Loaded`, none open | 0 | message `None` |
| `Loaded` | matches | the matching rows, newest first, then `More` when `List.MoreExist` |

Message rows show while filtering too. A pull request matches when `#{n}`, its title, author or head
label (`feature/login`, a fork's `ada:fix-typo`) contains the filter, ignoring case.

`PullRequestRow : RefListRow` (key `pr:{n}`, text `#{n} {title}`, depth 1) holds the `PullRequest` and
`Number`, and shows `NumberText` `#1`, `Title`, `Author` (empty when null), `IsDraft`, `HeadLabel`,
`TargetSha` (`HeadSha`), `ToolTip` (`#1 Add the login form`, then `{author} · {head label} → {base
ref}`, then ` · Draft` for a draft) and `[ObservableProperty] IsSelected`; `ShowsSameAs` compares them
all. `PullRequestMessageRow : RefListRow` (key `pr-message:{Kind}`, depth 1, `canActivate` only for
`SignIn` and `More`) holds `Kind`, `Text` and `ToolTip` (the `More` row's is `Open pull requests in browser`).

`Activate` (`:111`): a `PullRequestRow` whose head `IsInGraph` says is no row clears the selection and
raises `NothingActivated` (FD19's rule); otherwise it becomes `_selectedKey`, rows are marked and
`RefActivated(HeadSha)` selects and reveals the commit. A message row does nothing there. The three
loops that mark `IsSelected` (`:147`, `:157`, `:187`) go through one helper over `RefItemRow` and
`PullRequestRow`. `ItemKeys` (`:425`) and `TargetOf(refs, key)` (`:197`) learn `pr:` keys from
`_hosting.List`, so a clicked pull request stays selected across `Update` and `UpdatePullRequests`
while the graph's single selection is its head (`SyncSelection`, D98).

`LeftPanelViewModel.Actions.cs`: `RefRowTarget.PullRequest`; `TargetOf` (`:29-43`) gives `None` for
the section's header only while `CanToggle` is false (`:34`), `Section` otherwise, `PullRequest` for a
`PullRequestRow`, `None` for a message row.

**The branch row's `#n`** (D308). `RefItemRow` gains `(int Number, string Title)? pullRequest = null`
and `PullRequestText` (`#1`), `PullRequestToolTip` (`Pull request #1: Add the login form`),
`ShowsPullRequest`; `ShowsSameAs` compares them. `AddFolder`'s leaves (`:350-353`) pass it for local
and remote branches whose full name is in `_hosting.ByRef`; without a GitHub remote no row changes.

**View** (`LeftPanelView.axaml`). The item template (`:133-198`) gains, after `RefSoloToggle` and
before `RefName`, `<TextBlock Name="RefPullRequest" DockPanel.Dock="Right" Classes="secondary count"
Margin="6,0,0,0" Text="{Binding PullRequestText}" ToolTip.Tip="{Binding PullRequestToolTip}"
IsVisible="{Binding ShowsPullRequest}" AutomationProperties.AutomationId="RefPullRequest" />`: from the
right, the counts, the toggles, `#1`, then the name, which is cut first; collapsed, it takes no room.
Two new templates, both `RefRowButton`, 26 high:
- `PullRequestRow`: `Button` (`Classes.selected`, `Command="{Binding ActivateCommand}"`, automation id
  `PullRequestItem`, name `Text`, no `RefDrag.Source`); a `DockPanel Margin="0,0,12,0"` with a `Border`
  of `Indent`, an 18-wide `Border` (the chevron's room), docked right the author (`PullRequestAuthor`,
  `secondary`, `MaxWidth="90"`, `CharacterEllipsis`, `Margin="8,0,0,0"`) and `Draft`
  (`PullRequestDraft`, `secondary`, visible for a draft), docked left `#1` (`PullRequestNumber`,
  `secondary`, `Margin="0,0,6,0"`), then the title (`PullRequestTitle`, `CharacterEllipsis`); tooltip
  `ToolTip` on the `DockPanel`.
- `PullRequestMessageRow`: the same frame with the text (`PullRequestMessageText`, `secondary`,
  `CharacterEllipsis`, tooltip `ToolTip`); automation id `PullRequestMessage`, name `Text`.

At the default 260 the title has about 110 to 150 px. Tokens only; both themes; no new token or icon;
`RefList`'s automation name stays. `LeftPanelView.axaml.cs`: the constructor (`:19-24`) adds a
`Button.ClickEvent` handler on `RefList`; a click on a clickable `PullRequestMessageRow` in the active
repository's panel (the test of `OnDoubleTapped`, `:75-85`) calls `repository.ActivatePullRequestMessage(row)`.

### 10.4 Menus (`RepositoryMenus.Hosting.cs`; D306, D307)

Builders stay pure functions of the context (D86); without a GitHub remote `context.Hosting` is null
and every hook gives null, so every earlier menu is as it was.
- `ForPullRequest(RepositoryMenuContext context, int number, IRepositoryActions actions)`: the pull
  request from `context.Hosting?.List`; gone: the header alone. Header `#1 Add the login form` (cut by
  the view, whole in its tooltip); `[Open in browser, Check out {head}]`, `[Copy link]`. "Check out"
  takes the plan's label and reason.
- `ForSection` (`:419-448`) gains `RefSection.PullRequests => PullRequestsSection(context, actions)`:
  null without hosting; header `Pull requests`; signed out `[Sign in to GitHub…, Open pull requests
  in browser]`; otherwise `[Refresh pull requests, Open pull requests in browser]`, Refresh disabled
  with `Pull requests are loading` while `Loading`.
- `BranchPullRequestItem`: null without hosting and for a remote branch of a remote not in
  `Hosting.Remotes`; `Open pull request #n` when `Hosting.ByRef` holds the branch (ruling 11; a local
  branch only through an upstream of the same name, 7.8), else `Create pull request…` →
  `CreatePullRequestAsync(branch.FullName, null)`. Never disabled (signed
  out, WP-N's action signs in first). In `ForLocalBranch`'s push group (`:259`: `[push, forcePush,
  item]`) and in a group of its own after `ForRemoteBranch`'s delete group (`:330-332`); graph labels
  share these builders.
- `DropPullRequestItem`: in each of `ForDrop`'s three `Build` calls, a group between `rebaseItems`
  and the compare group, when the target is a remote branch of a GitHub remote or a local branch whose
  upstream is a branch of one under the same name (12.3 step 3); `Open pull request #n` when the source has one in `ByRef`, else `Create
  pull request…` → `CreatePullRequestAsync(sourceRef, targetRef)`.

`RepositoryViewModel.Menus.cs`: `OpenRefRowMenu` gains `RefRowTarget.PullRequest when row is
PullRequestRow pr => OpenMenu(layer, RepositoryMenus.ForPullRequest(MenuContext, pr.Number, actions), …)`;
`DoubleTapRefRow` gains: when the plan has no reason, `Start(MenuActions.CheckOutPullRequestAsync(pr.PullRequest),
"checking out a pull request")`; `ActivatePullRequestMessage(row)`: `SignIn` starts
`SignInToGitHubAsync()`, `More` starts `OpenPullRequestsInBrowserAsync()`. Palette:
`AppCommands.RepositoryItems` adds, after the remotes (`:211-214`), `ForPullRequest(…)` for each listed
pull request, the header as detail (D212).

### 10.5 Checkout (D305)

`PullRequestCheckout.Plan(RepositoryHostingState state, RepoRefs refs, PullRequest pr)`, pure; the menu,
the double-click and the operation use it. Remotes are tried in `HostingRemoteOrder`; two repositories
are the same when host and full name are equal ignoring case.
1. **Head on a remote.** The first remote `r` whose repository is `pr.HeadRepository` and whose
   `refs/remotes/{r}/{head ref}` `refs.SourceOf` maps as a branch of `r` named the head ref: label
   `Check out {local}` with `local = refs.LocalNameOf(remoteRef)`; reason
   `MenuTexts.AlreadyCheckedOut(local)` while HEAD is on it. The operation runs the existing
   `CheckoutAsync(remoteRef)` (tracking branch, D93, D201); when the ref is not among the refs yet, it
   first awaits the existing `FetchAsync(r)` and checks out only if the ref then exists. This covers
   #1 in `hello/` and #7 in `tool/` (head on `origin`, the user's fork).
2. **Otherwise** the base remote: the first whose repository is `pr.BaseRepository`. None: label
   `Check out {owner}:{head ref}`, reason `AddRemoteFor(base full name)` (a fork cloned without its
   parent). Owner is `HeadRepository.Owner`, or `pr-{n}` when the fork was deleted (label then `Check
   out {head ref}`: #8, `Check out idea`). The local name is the first of `{head ref}`,
   `{owner}-{head ref}`, `{owner}-{head ref}-2`, `-3`, … that is no local branch and conflicts with
   none as a path (`a` against `a/b`), or a local branch at `pr.HeadSha` (reused). Reused and
   current: `AlreadyCheckedOut`. Reused: `CheckoutAsync("refs/heads/" + local)` alone. New: one
   Network operation `FetchPullRequest(n, remote)`, `RefreshScope.Refs`, `Undo =
   UndoScope.CreateBranch(local, Refs?.Head.BranchName)`: `sha = await Git.FetchToFetchHeadAsync(remote,
   "refs/pull/{n}/head", options, ct)`; null: the operation fails with
   `StoppedFailure(FetchHeadChanged)` and creates no branch; else `Git.CreateBranchAsync(local, sha,
   checkout: false, noTrack: true, keepIgnored: KeepIgnored, options)`; report `Fetched #5 into
   fix-typo`. On success, `CheckoutAsync("refs/heads/" + local)`:
   the existing checkout with its automatic stash, detached-HEAD question, blocked dialog and undo, as
   its own operation (0.2).

`FetchToFetchHeadAsync` (`GitRepository.PullRequests.cs`) runs, through `RunRemoteCommandAsync`
(`GitRepository.Remotes.cs:687`), `git fetch --progress --no-tags --refmap= --write-fetch-head
<remote> <sourceRef>`; `sourceRef` must match `refs/pull/<digits>/head` (else `ArgumentException`).
No `--prune`, no `--tags` (D99). After the fetch it reads `FETCH_HEAD` itself (`ReadGitFile`, as
`FetchMarkedNothingForMerge` does, `GitRepository.Remotes.cs:618-626`; the file is per worktree), never
`rev-parse FETCH_HEAD` in a call of its own: another fetch in the same worktree (a second tab on it,
plan.md risk 33, or a fetch in a terminal) can rewrite the file in between. It returns the SHA only when
the file is one line of three tab-separated fields, a full SHA, an empty second field and a third that
starts `'<sourceRef>' of `; else null. The URL after `of` is not compared: git writes it after
`insteadOf` and without a trailing `.git`.

Operations (`RepositoryOperations.PullRequests.cs`):
- `OpenPullRequestAsync(pr)`: `LaunchAsync("Open in browser", l => l.OpenUrlAsync(pr.HtmlUrl))`
  (`RepositoryOperations.Files.cs:457`): not an operation; a failure is logged.
- `OpenPullRequestsInBrowserAsync()`: the same for `{WebBase}{owner}/{name}/pulls` of
  `HostingState.Repository`, else of the first GitHub remote's; an info line when neither is known.
- `CopyPullRequestLinkAsync(pr)`: `CopyAsync("Copy link", pr.HtmlUrl.AbsoluteUri)` (`:481`).
- `RefreshPullRequestsAsync()`: `Repository.Hosting.RefreshPullRequestsAsync`, outside the queue; a
  failure logged as a warning; nothing asked while signed out (D257).
- `SignInToGitHubAsync()`: `Services?.SignInToGitHub?.Invoke()`; the session refreshes at sign-in.
- `CheckOutPullRequestAsync(pr)`: the plan; a plan with a reason does nothing (a stale menu).

Git facts (2.56): with `+refs/pull/*/head:refs/remotes/origin/pr/*` added, `fetch.prune=true` and
`remote.origin.tagOpt=--tags`, `git fetch origin refs/pull/7/head` created `refs/remotes/origin/pr/7`
and fetched tags, while `git fetch --progress --no-tags --refmap= --write-fetch-head origin
refs/pull/8/head` changed no ref (a stale ref kept, no new tag) and no configuration; `FETCH_HEAD`
held one line, `<sha>\t\t'refs/pull/8/head' of <url>`, which `git rev-parse --verify --quiet
FETCH_HEAD^{commit}` gave. For a remote whose URL `myalias:` an `insteadOf` rule rewrites to
`file:///…/srv.git`, the line read `of file:///…/srv`. A missing pull request: `fatal: couldn't find remote ref
refs/pull/99/head`, exit 128, `FETCH_HEAD` empty (read it only after a successful fetch). `git branch
--no-track alice-feature <sha>` wrote no `branch.*` key, also with `branch.autoSetupMerge=always`; an
existing name exits 128. `--refmap` exists since 2.1 and `--write-fetch-head` since 2.29, so 2.43 and
2.55 are expected to behave alike.

### 10.6 Tests (in the 7a project unless named; menu and plan tests use the linked `RecordingActions`)

- `PullRequestSectionTests` (`LeftPanelViewModel`): `NotGitHub` gives today's rows (`"> Pull requests 0"`,
  no toggle); each state's rows; author, draft, a fork's head label; 300 rows then `More`; the
  filter on `#1`, `1`, title, author and head label, the count the matches; a click selects
  (`RefActivated(HeadSha)`), and with `IsInGraph` false raises `NothingActivated`; the key survives
  `Update` and `UpdatePullRequests` and follows `SyncSelection`; a branch row's `#n` from `ByRef`;
  `TargetOf` for each row.
- `PullRequestCheckoutTests`: same repository; head on `origin` as the user's fork while `upstream` is
  the base; a remote branch not read yet; a fork; no base remote; a deleted fork; candidates taken,
  conflicting as a path, reused at the head, current.
- `RepositoryMenusHostingTests`: `ForPullRequest`'s lines for both labels; the section's menus signed
  in, signed out, loading, and null without hosting; `ForLocalBranch`, `ForRemoteBranch` and the
  three `ForDrop` shapes with and without hosting (exact lines), "Open pull request #1" in place of
  "Create pull request…", a non-GitHub remote's branch left out, a drop onto a local branch whose
  upstream has another name left out; `RepositoryItems` order.
- `LeftPanelPullRequestsViewTests` (headless view): the templates' ids, 26-high rows, the `#n`
  collapsed without a pull request and the name's width unchanged then; a click on the sign-in row
  calls `SignInToGitHubAsync`; a double-click on a row calls `CheckOutPullRequestAsync`.
- `PullRequestOperationsTests` (Hosting.Tests, D334; `ShellDriver` with `FakeGitHub` on port 0, signed in): #1's checkout tracks
  `origin/feature/login`; a not-yet-fetched head is fetched first; #5's checkout leaves `for-each-ref`
  but the new branch, and `git config --list --local`, as before, the branch without upstream at the
  fork's head, then checked out; a taken name gives `ada-fix-typo`; a reused branch is checked out
  without a fetch; open in browser records `OpenUrl` with `html_url`; copy link puts it on
  `FakeClipboard`; refresh asks the fake again; no API request while signed out.
- `GitRepositoryPullRequestsTests` (Git.Tests): `FetchToFetchHeadAsync`'s command form, its effect
  with the `refs/pull` line, `fetch.prune` and `tagOpt` set, the missing ref's `GitException`, the
  refusal of another source ref; the SHA it returns is the line's, with no `rev-parse` call; null when
  `FETCH_HEAD` is rewritten after the fetch (another ref's line, two lines, an empty file; an
  `IGitRunner` wrapper, as `PseudoRefProbeTests`' `RecordingRunner`, that rewrites it when the fetch
  call ends), and in a linked worktree the worktree's own file.
- `PullRequestFetchHeadTests` (App.Tests): `ShellDriver` has no seam for the repository, so this class
  opens `Scenarios.OperationsAsync()`'s repository through `OperationsHarness.OpenAsync(repo, prompts:
  null, wrap: r => <a DispatchProxy, as RepositoryOperationsHistoryTests' StoppingRevert, whose
  FetchToFetchHeadAsync returns null without a fetch>, hosting: stub)`, `stub` a `HostingSessionStub`
  whose repository hosting's `State` (settable, raising `Changed`; WP-P adds it) maps `origin` to the
  base of a `PullRequest` built by hand (a fork's head, plan 2). `CheckOutPullRequestAsync` fails with
  `FetchHeadChanged`'s text, creates no branch (`for-each-ref` as before) and leaves HEAD where it was.

The builder also runs, unchanged: `LeftPanelViewModelTests`, `PanelViewTests`, `RepositoryMenusTests`,
`RepositoryMenusKeyTests`, `PaletteEntriesTests`, `RepositoryOperationsCheckoutTests`, and
`--project tests/VisualCommit.VisualTests.Phase3 --filter-method "*Check_03_other_menus*"` (`03e`).

### 10.7 Part P's checks

Code: `Phase7aChecks.P.cs` (`Phase7aChecksP`), `.P.Menus.cs`, `.P.Checkout.cs`, `.P.Helpers.cs`,
`Phase7aExpectations.P.cs` (rows, menus, R picture names), `Phase7aExpectationTests.P.cs`; pictures
`p<nn><step>-<what>.png`. The scenario's `hello/` (#1, #2 draft, #3, #5 from the fork `ada/hello`)
and `tool/` (#7, #8); more than 300 by `AddPullRequest` on octo/hello in the check (one `update-ref`
at the next list). Several checks share one app start where the steps allow.

They show (R: repeated in the real window): the section unchanged without a GitHub remote (`03e`'s
state); signed out; loading (`HoldNext("pulls:octo/hello")`); failed, in `HostingTexts.Describe`'s words (1.7); none (the
check closes them on the fake); rows with a draft and a fork (R); more than 300; the filter; a click
selecting the head commit and one selecting nothing; a branch row's `#1`; the section's menu (R) and
the pull requests' menus; "Create pull request…" and "Open pull request #1" in a branch menu and a
drop's; check out of #1 and #5 with git's state checked; copy link; open in browser through
`FakeFileLauncher` (R: in the real process, the default browser opened once, 13.5, D321). No earlier picture changes.

Commits: (1) `FetchToFetchHeadAsync` and its class; (2) rows, `LeftPanelViewModel`, the branch row's
`#n`, `PullRequestSectionTests`; (3) the view and its tests; (4) the plan, the operations, their tests;
(5) the menus, hooks, `OpenRefRowMenu`, `DoubleTapRefRow`, `ActivatePullRequestMessage`, the palette,
their tests.

### 10.8 At the merge; done when

The lead runs part P's checks, the classes above and the lagged list, and part A's check 9 again,
inspecting its pictures of `hello`'s window, whose "Pull requests" section is now this package's (0.4,
8.7); WP-V starts after it. Done when
the new and listed classes pass; part P's checks pass and their pictures are inspected; `refs/pull`
is never a fetch line or a `refs/remotes/<r>/pr/<n>` ref; no earlier picture changed.

## 11. WP-V: the graph's badge and avatars (part G)

### 11.1 Goal and files

A branch with an open pull request carries a compact badge inside its label in the graph, after its
name; authors GitHub knows show a 16-px round avatar in the graph's Author column and in the details
panel's author line. Everything else looks exactly as today: repositories without a GitHub remote,
signed out, authors GitHub does not know. Carries "pull-request badge on branches" and "Author
avatars, from GitHub only" (D251, D252, D257), rulings 12 and 22, D309 to D311, part G; progress steps
12 and 14. Wave 2, after WP-P's merge (part G's whole-window pictures show its section and the
branch rows' `#n`, 0.4) and part G's checks; branch `wip/p7a-graph`.

Before the package, **the lead** commits in the main checkout: in `src/VisualCommit.App/Theme/Icons.axaml`,
after `IconTag` (`:29`), `<StreamGeometry x:Key="IconPullRequest">M15 18 a3 3 0 1 0 6 0 a3 3 0 1 0 -6 0
M3 6 a3 3 0 1 0 6 0 a3 3 0 1 0 -6 0 M13 6 h3 a2 2 0 0 1 2 2 v7 M6 9 v12</StreamGeometry>` (Lucide
"git-pull-request"; bounds 3,3,18,18); in `THIRD-PARTY-NOTICES.md:33-36`, "git-pull-request" joins the
icons derived from Feather.

Create: `src/VisualCommit.App/Controls/CommitGraphControl.Badges.cs` (`PullRequestsProperty`, the
badge's measuring and drawing, `PullRequestBadge`, the tooltip's text);
`Controls/CommitGraphControl.Avatars.cs` (`AvatarsProperty`, the decoded images,
`ShowsAvatarFor(string email)` for tests, the `AvatarsApplied` event); `Controls/AvatarBitmaps.cs`
(`public static Task<Bitmap?> DecodeAsync(string file, CancellationToken ct)`: on the thread pool,
`Bitmap.DecodeToWidth(stream, 32)`; null for a missing or unreadable file);
`ViewModels/RepositoryViewModel.Avatars.cs`.

Change: `CommitGraphControl.cs` (the two properties in `OnPropertyChanged`, `:395-438`; `LabelsTipAt`,
`:825-842`; `PlacedLabel`, `:866`, gains a last `PullRequestBadge? Badge = null`; `LabelsInRow`,
`:312-316`, passes it); `CommitGraphControl.Render.cs` (`PlaceLabels` `:543-623`, `DrawLabels`
`:370-400`, `LabelVisual` `:713`, `RowVisual` `:710`, `CreateRowVisual` `:473-489`, `AuthorLayout` and
`_authorCache` `:79`, `:495-515`, `DrawTextCells` and `DrawCell` `:434-450`, `Palette` `:720-821` with
`PullRequestIcon = Icon("IconPullRequest")`); `Views/Graph/RepositoryGraphView.axaml:52-64`
(`PullRequests="{Binding HostingState.ByRef}"` and `Avatars="{Binding Avatars}"` after `Search`) and
`.axaml.cs` (the log line); `ViewModels/Panels/CommitDetailsViewModel.cs`;
`Views/Shell/RightPanelView.axaml:132-135` and `.axaml.cs`; `tests/VisualCommit.Testing/AppLogFile.cs`
(`AvatarsShown`). Not touched: `CommitGraphData` (labels fixed per instance, `CommitGraphData.cs:288-344`),
`RepositoryViewModel.Hosting.cs`, the left panel, `GraphColumns`, `Tokens.axaml`.

### 11.2 The badge (D309)

**Data.** `PullRequestsProperty`: `StyledProperty<IReadOnlyDictionary<string, PullRequest>?>`, bound to
`HostingState.ByRef`. Handled in `OnPropertyChanged` as `SearchProperty` is (`:434`), not through
`AffectsRender`: when the new map gives any ref another (number, title, draft) than the old, or adds or
removes one, `_rowCache.Clear()`, `InvalidateVisual()`, `UpdateHover()`; an equal map does nothing.

**Which labels.** A `CurrentBranch`, `LocalBranch` or `RemoteBranch` label whose `RefName` is in the
map. In one row a pull request's badge is drawn on its first such label only (local before remote,
`CommitGraphData.cs:334-341`): a branch level with its upstream shows one badge; one ahead shows a
badge on each row. A label folded into "+N" has none.

**Size** (constants in `CommitGraphControl.Badges.cs`): after the name, `BadgeGap` 6, the icon
`LabelIconSize` 10 (`DrawIcon`, `:425-432`), `BadgeIconGap` 3, then `#1` (`"#" + number`, invariant)
in `LabelTypeface` at `LabelFontSize` 11; `BadgeWidth` about 36 px for two digits. Colours: the
label's own (`LabelLayout`'s text brush, `:670-676`; `LabelColors`' icon pen). No new token. Without
the icon resource the space is kept and nothing drawn.

**Placing** (`PlaceLabels`): `widths[i] = LabelWidth(...) + BadgeWidth(badge of i)`, so a label with a
badge folds the next ones into "+N" sooner by the existing rule. The first label's cut becomes
`textRoom = room - LabelWidth(0, HasIcon(kind)) - BadgeWidth(badge)`: the name is cut with "…" by
`CutToFit`, never the badge. In the 130-px column (`GraphColumns.RefsWidth`; room 124 from
`LabelStart` 6) the badge leaves about 76 px of name (63 beside the current branch's check), so
`feature/login` stays whole or loses a letter or two and a long one is cut; part G's writer records
the measured widths. `LabelVisual` gains `PullRequestBadge? Badge` and `TextLayout? BadgeLayout`;
`DrawLabels` draws the badge at the name layout's right edge plus 6. The label's bounds include the
badge: a click, right-click, drag or drop on it is one on its label (whose menu holds "Open pull
request #1", WP-P).

**`PlacedLabel`.** `Text` stays the name as drawn and `FullText` the whole name: both feed the menus
(`RepositoryGraphView.axaml.cs:144-145`, `LabelOf`), the drag's `DragSource.Name` and `DropTarget.Name`
(`CommitGraphControl.Drag.cs:39`, `:53-55`) and about 25 earlier assertions
(`CommitGraphControlTests.cs:147`, `Phase1Checks.Graph.cs:250`, …). The pull request is named by
`Badge` (`public sealed record PullRequestBadge(int Number, string Title, bool IsDraft)`) and by the
tooltip.

**Tooltip** (`LabelsTipAt`): each label's line is its name, followed, when the map holds its ref, by
" (pull request #1: Add the login form)" or " (draft pull request #2: Update the readme)"; also for
labels hidden in "+N". Rows without such a ref: as today.

### 11.3 Avatars (D310, D311)

**Data.** `AvatarsProperty`: `StyledProperty<AuthorAvatars>`, default `None`, bound to
`RepositoryViewModel.Avatars`. On a change the control starts `AvatarBitmaps.DecodeAsync` for each file
it has not decoded (keyed by path; never in `Render`, D59), awaits them together and, unless a newer
change came meanwhile, keeps the bitmaps, clears `_rowCache` and `_authorCache`, `InvalidateVisual()`,
`UpdateHover()`, and raises `AvatarsApplied` with the number of authors shown. Until then rows draw as
today. A file that fails to decode: that author as today, not logged. Decoded bitmaps live as long as
the control (a few dozen 32-px images). An avatar replaced under the same path shows from the next
start.

**The log line.** `RepositoryGraphView.axaml.cs` handles `AvatarsApplied` and calls the view model's
`NoteAvatarsShown(int count)` (`RepositoryViewModel.Avatars.cs`), which writes at Debug
`{repo}: showing GitHub avatars for {n} authors` (1.9); `AppLogFile.AvatarsShown(repo)` matches it.

**Graph** (`CreateRowVisual`, `DrawTextCells`): a row's image is the bitmap of
`Avatars.FileOf(commit.AuthorEmail)` when the Author column is visible. With one, it is drawn 16×16 at
(`Author.X` + 8, row top + 5), clipped to a circle (`PushGeometryClip` with an `EllipseGeometry`), with
high-quality interpolation; the name at `Author.X` + 8 + 16 + 6, its layout 22 px narrower.
`_authorCache` is keyed by `(string Name, bool WithAvatar)`. `RowVisual` gains `Bitmap? Avatar`. A
dimmed row (search) dims its image with its text (`Render.cs:173`). `CellTipAt`
(`CommitGraphControl.cs:799`) is unchanged. Rows without an image: today's layout exactly.

**Details panel.** `CommitDetailsViewModel` gains `public AuthorAvatars Avatars { get; set; }` (default
`None`; setting it recomputes the file for the commit shown) and `[ObservableProperty] public partial
string? AuthorAvatarFile { get; private set; }`, set in `Apply` (`:404`) from `details.AuthorEmail` and
null in `Clear` (`:435`). In `RightPanelView.axaml` the author's value becomes:

```xml
<DockPanel Grid.Row="0" Grid.Column="1">
  <Ellipse Name="DetailsAuthorAvatar" DockPanel.Dock="Left" Width="16" Height="16"
           Margin="0,0,6,0" VerticalAlignment="Top" IsVisible="False"
           AutomationProperties.AutomationId="DetailsAuthorAvatar"
           AutomationProperties.Name="Author's avatar" />
  <SelectableTextBlock Name="DetailsAuthor" Classes="value" Text="{Binding AuthorText}"
                       AutomationProperties.AutomationId="DetailsAuthor" />
</DockPanel>
```

A collapsed child takes no room and the last child fills, so with no image the text has today's
width, place and wrapping (a horizontal `StackPanel` would stop the wrapping, r2 6.3).
`RightPanelView.axaml.cs` follows `AuthorAvatarFile` (subscribing again on `DataContextChanged`): null
hides the ellipse; a file is decoded through `AvatarBitmaps` and, unless a newer file came, becomes
the ellipse's `Fill` (`ImageBrush`, `Stretch.UniformToFill`) and the ellipse shows.

**Telling the session the authors** (`RepositoryViewModel.Avatars.cs`): RepositoryViewModel overrides
no `OnPropertyChanged` today and `OnGraphChanged` is taken (`RepositoryViewModel.Find.cs:200`), so this
partial overrides `protected override void OnPropertyChanged(PropertyChangedEventArgs e)` (base first;
the only override, 1.7):
- `Graph`: follows the new graph's `Changed` while it loads and has fewer than 2,000 rows
  (`CommitGraphData.Changed`, `IsComplete`), then updates the authors.
- `HostingState`: updates the authors; on `SignedOut` or `NotGitHub` forgets the set last sent.
- `Avatars`: `Details.Avatars = Avatars` (changes raised before `Details` exists are ignored).

Updating the authors: nothing in summary mode, nor unless `HostingState.PullRequests` is `Loading`,
`Failed` or `Loaded` (D257). Else the distinct author e-mails (`CommitAt(i).AuthorEmail`, lower-case
invariant) of those of the first 2,000 rows that a GitHub remote's remote-tracking branch reaches,
newest first, at most 200; when the set differs from the last sent, `Hosting.ShowAvatarsFor(set)`.
GitHub is never told the address of a commit that is only local or only on another remote (D311).
The rows are found in one pass in row order: a set of SHAs starts with the targets of `Graph.Refs`'
`RemoteBranch` refs whose `RemoteName` is a key of `HostingState.Remotes` (`refs/remotes/<remote>/*`),
and a row whose SHA is in it adds its parents (the log's order shows no parent before its children,
`--date-order`). A remote branch whose tip is not loaded (a graph filter hides it) adds nothing.

**Matching** (git fact, 2.56): the graph's and the details' e-mails are read with `%aE`
(`GitRepository.Commits.cs:15`, `:20`), which applies `.mailmap` (with `New Name <new@example.com> Old
Name <old@example.com>`, a commit by `old@example.com` gives `%ae` old and `%aE` new). GitHub's commit
list holds the raw address, so a mapped author is matched only when the lookup by the mapped address
finds an account. 2.43 and 2.55 apply `.mailmap` alike. Git keeps an address's case; the map is
lower-case.

Without a GitHub remote, signed out and under `NullHostingSession`, `HostingState` stays `NotGitHub`
and `Avatars` `None`: no label is wider, no author moves, the ellipse stays collapsed.

### 11.4 Tests (in the 7a project; `[AvaloniaFact]` in a window of its own, as `CommitGraphControlTests`' `Host`, `:1011-1045`, unless `[Fact]`)

- `GraphPullRequestBadgeTests`: `IconPullRequest`'s bounds; a label with a pull request is
  `BadgeWidth` wider, `Badge` set, `Text` and `FullText` the name; a long name cut and the badge kept
  inside the column; "+N" folding with a badge; one badge per pull request per row; the tooltip's
  lines; an equal new map keeps the row cache, a changed one updates `LabelsInRow` without a new
  `Data`; with an empty map every `LabelsInRow` and the captured frame equal a control never given it.
- `GraphAvatarTests` (PNG files in a temporary folder): after decoding, `ShowsAvatarFor` is true, the
  image's pixels lie at the column's x + 8 to + 24, the name starts at + 30, `AvatarsApplied` raised
  once; an author without an image is laid out and drawn as today; a broken file draws as today; a
  hidden Author column draws none; a dimmed row dims its image.
- `CommitDetailsAvatarTests`: `[Fact]` the file follows the e-mail (case ignored), a later `Avatars`
  change, `Clear`; the view: no file, `DetailsAuthor`'s bounds equal a panel with `None`; with a file
  the ellipse 16×16 at the column's left and the text 22 further.
- `RepositoryAvatarsTests` (`[Fact]`, fakes of `IHostingSession` and `IRepositoryHosting` in hand-built
  `TabServices`): the set (lower-case, newest first, ≤ 200, from ≤ 2,000 rows); only authors of
  commits a GitHub remote's branch reaches: a local-only commit's author, and the author of a commit
  only on a remote that is not GitHub, are not in it unless one of their commits is reached; nothing when
  `NotGitHub`, `SignedOut` or summary; not sent twice; sent again after a sign-out and sign-in; a
  loading graph followed until 2,000 rows; `Details.Avatars` follows; `NoteAvatarsShown`'s line.

The builder also runs `CommitGraphControlTests`, `GraphReviewFixTests`, `HideAndSoloTests`,
`GraphSelectionTests`, `GraphColumnsTests`, `IconsTests`, `ThemeTokensTests`,
`CommitDetailsViewModelTests`, `ShellDriverPhase5GraphTests`, each with `--filter-class`.

### 11.5 Part G's checks

Scenario 2.6 on 47395, `hello/`, signed in, the list `Loaded`: octo (1001) and ada (1003) linked,
Test Author unknown; #1 on `feature/login` and `origin/feature/login`, #3 on the long branch name.
Every check on `hello` first calls `PartG.PrepareAsync(served.Hello)` on the copy, before `OpenAsync`,
and its R picture is taken in that state (2.6). In `Phase7aExpectations.G.cs`:
`public static async Task PrepareAsync(TempRepo hello)` runs `hello.CheckoutAsync("feature/login")`,
`hello.CommitFileAsync(PartG.PreparedPath, PartG.PreparedContent, PartG.PreparedMessage)` and
`hello.CheckoutAsync("main")`, so `feature/login` is one commit ahead of `origin/feature/login` and the
two labels sit on two rows. The commit is the newest (TempRepo's clock, the same in both projects),
so it is the top row and its SHA and every row index match in the real window.

| Check | Shows | Real window |
|---|---|---|
| G1 Badges | `#1` on `feature/login`'s and `origin/feature/login`'s labels; the tooltip over the local one; `LabelsInRow`'s `Badge` and unchanged `FullText` | the graph with badges and avatars, and the details panel (one capture) |
| G2 Cut | the long name cut with "…", `#3` whole, the label inside the column; its tooltip whole | |
| G3 Avatars | the Author column with octo's and ada's images and Test Author as today; the details panel of an octo commit with its image, and of Test Author's as today; the fake's avatar requests carried no token | |
| G4 Light | G1's and G3's graph in light | |
| G5 No GitHub | signed in, a repository without a GitHub remote: its graph and details pictures are the same files as with GitHub off, and no API request names it | |

About 5 methods and one picture at 1920×1080. Code: `Phase7aChecks.G.cs` (`Phase7aChecksG`),
`.G.Helpers.cs`, `Phase7aExpectations.G.cs` (R picture names, the row and author, `PrepareAsync`),
`Phase7aExpectationTests.G.cs` (with the pin: on `Scenarios.HostingAsync()` after `PrepareAsync`,
the graph's order, `git log --date-order --format=%H%x09%ae --branches --remotes --tags HEAD`, puts
the prepared commit first and part G's commit at `PartG`'s row constant with its author). Pictures of the opened repository are taken after both Debug
lines (D322).

Commits: (1) the badge with `GraphPullRequestBadgeTests`; (2) avatars in the graph, `AvatarBitmaps`,
`GraphAvatarTests`; (3) the bindings, `RepositoryViewModel.Avatars.cs`, the log line,
`RepositoryAvatarsTests`; (4) the details panel, `CommitDetailsAvatarTests`.

### 11.6 At the merge; done when

The lead runs the lagged list; phase 1's checks (`--project tests/VisualCommit.VisualTests
--filter-class "*Phase1Checks"`), phase 3's walk-through, `VisualCommit.VisualTests.Phase5` and
`VisualCommit.VisualTests.Phase6Look`, each through `scripts/heavy-run.sh`, comparing their graph and
details pictures with `master`'s references by region (`scripts/compare-pictures.py`; every picture
shows D248's box): no difference; then part G; then part A's check 9 again, inspecting its pictures of
`hello`'s window (0.4, 8.7).

Done when the four classes and the listed ones pass; part G passes with every picture inspected; no
earlier picture of the graph or details panel differs; no `FullText` changed; the UI thread does no
file work for avatars.

## 12. WP-N: a new pull request (part N)

### 12.1 Goal and files

"Create pull request…" opens a form; "Create" pushes the head when GitHub lacks it and creates the
pull request, as one operation of the repository's queue (`OperationKind.Network`, so WP-S's refresh
follows it); the list then shows it and the status bar says "Created pull request #n" with a button
that opens it. Carries the action behind WP-P's items (create from the branch and drop menus), D251,
D257, rulings 11, 13 and 15, D312 to D318, D338 (`HostingTexts.Describe`, 12.6), part N. Progress step 13. Wave 2, after WP-P and WP-V;
branch `wip/p7a-create`.

Create: `ViewModels/Dialogs/CreatePullRequestDialogViewModel.cs` (the view model,
`ICreatePullRequestActions` and its records); `Views/Dialogs/CreatePullRequestDialogView.axaml` and
`.axaml.cs` (a `DialogView`, found by `DialogViewLocator`, `:58-71`);
`ViewModels/Operations/OperationTexts.Hosting.Create.cs` (`OperationTexts.CreatePullRequestTexts`).

Change: `RepositoryOperations.CreatePullRequest.cs` (WP0's stub: `CreatePullRequestAsync(string
headRef, string? baseRef)` and a private nested `PullRequestForm : ICreatePullRequestActions`);
`src/VisualCommit.Core/Operations/OperationReport.cs`; `OperationQueue.cs:674` and `:738`;
`ViewModels/Operations/IStatusBarOperation.cs`, `OperationStatusViewModel.cs`;
`ViewModels/Workspaces/DashboardBulkViewModel.cs:51-84`; `Views/Shell/StatusBarView.axaml`. Not
touched: `RepositoryMenus.cs` (WP-P's), `RepositoryOperations.Remotes.cs` (WP-C's guards, called
only), `RepositoryViewModel.Hosting.cs`.

### 12.2 The status bar's offer (D317)

- Core: `public sealed record OperationOffer(string Label, Func<Task> Run);` and `public OperationOffer?
  Offer { get; init; }` on `OperationReport` and `OperationResult`; `OperationQueue.cs:674`: `Offer =
  report.Outcome == OperationOutcome.Succeeded ? report.Offer : null`; `ReportStaleLocksAsync`'s `with`
  (`:738`) keeps it.
- `IStatusBarOperation`: `string? OfferText`, `bool HasOffer`, `ICommand OfferCommand`;
  `DashboardBulkViewModel`: none (`null`, `false`, a static command that cannot execute).
- `OperationStatusViewModel`: a field `_resultOffer`, set in `ShowResult` (`:220`) for a non-background
  result to `result.Offer` (null for every other result, the stage panel's and auto-fetch's failure
  included; a background success changes nothing, so "Created pull request #9" and its offer stay
  while auto-fetch runs), cleared in `Update` (`:272-275`) where `_resultText` is reset when an
  operation of the user starts. `HasOffer` is true only while the shown text is that result's: not
  under ssh's notice (`Show`, `:343`), not while an operation runs. `OfferCommand` (async) awaits
  `offer.Run()`. `CompactText` (a dashboard row) is unchanged.
- `StatusBarView.axaml`, between `OperationStatus` (`:57-66`) and `OperationProgress` (`:67`):

```xml
<Button Name="OperationOfferButton" Theme="{StaticResource VcFlatButton}"
        IsVisible="{Binding ActiveTab.OperationStatus.HasOffer}"
        Command="{Binding ActiveTab.OperationStatus.OfferCommand}"
        CornerRadius="0" Margin="6,0,0,0"
        AutomationProperties.AutomationId="OperationOfferButton"
        AutomationProperties.Name="{Binding ActiveTab.OperationStatus.OfferText}">
  <TextBlock Text="{Binding ActiveTab.OperationStatus.OfferText}" FontSize="11.5" VerticalAlignment="Center" />
</Button>
```

Hidden, it takes no room, so every status bar before a create is as today.

### 12.3 How the action starts (`CreatePullRequestAsync`)

Inside `GuardAsync(CreatePullRequestTexts.Operation(head))`, on the UI thread:
1. `HostingState.PullRequests == NotGitHub`: log "not started" and return. `SignedOut`: `await
   Services.SignInToGitHub()`; false: return.
2. The head, from `Refs` now: a local branch, or a remote branch of a remote in `HostingState.Remotes`
   whose `Refs.SourceOf(gitRef)` is a branch. Anything else: log "not started", return.
3. The head's place on GitHub (computed again at each Create, from the refs then; D313):
   - A remote branch: its remote's repository and branch; never pushed.
   - A local branch `x` whose upstream is a branch of a GitHub remote under the same name
     (`UpstreamRemoteRef == "refs/heads/" + x`, ordinal: the name on the remote, not the
     remote-tracking name; git's `push.default=simple`): that repository and branch; pushed first to
     its upstream (`PushRequest(remote, x, UpstreamRemoteRef)`, no `--set-upstream`) when `Ahead > 0`
     or `UpstreamGone`.
   - Any other local branch: no upstream; a local one; another host's; or a GitHub remote's branch of
     another name, as "Create branch here" on `origin/main` or `git switch -c topic origin/main` make
     (phase 3's design 2.4). Its place is the first GitHub remote `r` in `HostingRemoteOrder` and `x`
     under its own name, chosen by that order and not by the upstream's remote (a branch made from
     `upstream/main` in a fork would otherwise go into the parent); the push is
     `refs/heads/x:refs/heads/x`. Without an upstream it is always pushed first, with `--set-upstream`.
     With one, the upstream is left as it is (no `--set-upstream`; the owner, 2026-10-29), and the
     branch is pushed first only when GitHub lacks it: no remote branch's `refs.SourceOf` is `r`'s
     `refs/heads/x`, or `Git.CountCommitsAsync(that remote branch, "refs/heads/x")`, read when the
     place is computed, is above 0. `HeadNote()`: null when that remote branch is at
     `x`'s tip in the refs now or the count is 0; `AheadPushedFirst` with the count when it exists;
     `PushedFirst` when it is missing. A branch GitHub already holds never reads "Not on GitHub yet".
4. The default title: `ReadSubjectAsync(head tip)` (`Run.cs:240`); empty when null.
5. The base: with `baseRef` (a drop), that remote branch of a GitHub remote, or the upstream of that
   local branch, which `DropPullRequestItem` offers only for an upstream of the same name (step 3);
   else the default, filled when `StartAsync` ends.
6. `ShowDialogAsync(new CreatePullRequestDialogViewModel(form, Log))` (`Run.cs:730`, which sets "In
   {repo}" when several tabs hold repositories). Its answer is not used.

**Base options** (`PullRequestEnd(Text, Repository)`, D318):
- Every remote branch of a GitHub remote whose `SourceOf` is a branch, in the refs' order: text its
  name (`origin/main`), repository `Remotes[r].FullName`. `origin/HEAD` is never a ref
  (`GitRepository.Refs.cs:218-220`).
- When no GitHub remote is the listed repository (a fork cloned without its parent): that
  repository's default branch, text `owner:branch` (GitHub's label, `octo:main`).
- Default: `Hosting.ReadRepositoryAsync()` (A3 of the first GitHub remote): repository `Parent ??
  Repository`, branch `ParentDefaultBranch` when `Parent` is set, else `DefaultBranch`; shown as the
  remote branch of the remote that is that repository when the refs hold it, else as `owner:branch`.
  A failed read leaves Into empty and puts the failure on the error line.
- The stacked Pick: `new PickDialogViewModel(ChooseBaseTitle, options-with-details, current,
  SettingsTexts.PickConfirm)` (`PickDialogViewModel.cs:44`), through `ShowDialogAsync` on top of the
  form (D104); Esc closes the Pick only.

### 12.4 The form

`CreatePullRequestDialogViewModel : DialogViewModel<bool>` (true when created; `CancelledResult`
false). Its own `CancellationTokenSource` runs `StartAsync` and is cancelled in `OnCancel` (D234's
`CancelAll`). It never runs git or HTTP itself:

```csharp
public sealed record PullRequestEnd(string Text, string Repository);           // "origin/main", "octo/hello"
public sealed record CreatePullRequestStart(PullRequestEnd? Base, string? Error);
public sealed record CreatePullRequestOutcome(bool Created, string? Error, int? ExistingNumber);
public interface ICreatePullRequestActions
{
    PullRequestEnd Head { get; }                    // "topic/no-pr", "octo/hello"
    string DefaultTitle { get; }
    PullRequestEnd? GivenBase { get; }              // the drop's target, or null
    string? HeadNote();                             // from the refs now (and 12.3 step 3's count); null when GitHub has the head as it is
    Task<CreatePullRequestStart> StartAsync(CancellationToken ct);   // the default base (A3)
    Task<PullRequestEnd?> ChooseBaseAsync(PullRequestEnd? current);  // the stacked Pick
    bool IsSameBranch(PullRequestEnd baseEnd);      // head and base: one branch of one repository
    Task<CreatePullRequestOutcome> CreateAsync(PullRequestEnd baseEnd, string title, string body, bool draft);
    Task OpenExistingAsync();                       // the pull request the last CreateAsync found open
}
```

States: **loading** (`StartAsync` runs, no given base: Into reads "Reading GitHub…" in the secondary
colour; a base chosen meanwhile wins); **ready**; **creating** (`IsCreating`: every control disabled,
Create reads "Creating…", Cancel disabled, `HandleEscape` does nothing, as `RemotesDialogViewModel`'s
`IsSaving`, `:116-146`); **failed** (the error line, controls enabled again, the head note read again);
**done** (`Complete(true)`). Changing the title, description, base or Draft clears the error line and
the "Open pull request #n" button. Create can execute when not creating, a base is set, the title is
not blank and `IsSameBranch` is false (D318); its disabled reason is its tooltip
(`ToolTip.ShowOnDisabled`): "Choose the branch to merge into." or "Enter a title."; the same branch
also puts `SameBranch` on the error line. Title and description are trimmed of trailing white space
when sent; nothing else is checked locally.

**Layout** (`CreatePullRequestDialogView.axaml`, root id `CreatePullRequestDialog`, name the title):
`Panel.dialogBackdrop` > `Border.dialogCard.wide` (460, padding 20) > `StackPanel`:
1. `CreatePullRequestDialogTitle` (`dialogTitle`) "Create pull request"; `DialogRepository` when shown.
2. `Grid ColumnDefinitions="48,*"`, margin 0,14,0,0, rows 8 apart; labels "From" and "Into"
   (`secondary`). From: `PullRequestHead` (trimmed, max 220, tooltip the name) and
   `PullRequestHeadRepository` (secondary), spacing 8; under them `PullRequestHeadNote`
   (`settingsNote`) when set. Into: `PullRequestBase`, `PullRequestBaseRepository`,
   `PullRequestBaseChooseButton` (`VcActionButton`, `SettingsTexts.Choose` "Choose…"), as
   `MergeToolValue` (`SettingsDialogView.axaml:352-362`).
3. `PullRequestTitle`: `TextBox.dialogBox`, 32 high, placeholder and name "Title"; focused, caret at
   the end; Enter creates.
4. `PullRequestDescription`: 120 high, margin 0,8,0,0, as `MessageDescription`
   (`MessageDialogView.axaml:48-57`); Mod+Enter creates (D215).
5. `PullRequestDraft`: `VcCheckBox`, margin 0,12,0,0, "Draft", unticked.
6. `PullRequestError` (`dialogError`) when set; `PullRequestOpenExisting` (`VcActionButton`, margin
   0,8,0,0) when `ExistingNumber` is set: runs `OpenExistingAsync` and completes with false.
7. `dialogButtons`: `CancelButton` "Cancel", `ConfirmButton` (`VcPrimaryButton`) "Create"/"Creating…".

About 415 high (440 and 460 with the repository line and an error), centred at 1100×700 (x 320) and
1920×1080; both themes; no new token.

### 12.5 The operation

`PullRequestForm.CreateAsync` runs, on the UI thread, `RunAsync(CreatePullRequestTexts.Operation(head),
OperationKind.Network, pushes ? RefreshScope.Refs : RefreshScope.None, run, options)` with
`options = new RunOptions { Prompts = PromptMode.Ask, ShowsErrorDialog = false, Undo = pushes ?
UndoScope.Push(branch, remote, force: false) : UndoScope.None }` (D312, D314; no dedupe key). `run`:
1. When pushing: `context.SetRunningText(OperationTexts.Push(branch, target).RunningText)`; the guards
   as `PushToAsync` runs them (`RepositoryOperations.Remotes.cs:160`), `RefuseIfNotABranch(...) ??
   await RefuseIfPushStopsFetchAsync(...)`, a refusal returned as it is; then `UndoCapture.Push(remote,
   await Git.PushAsync(request, NetworkOptions.From(context), context.CancellationToken))`, as `:165`.
   The push's credentials come from WP-C's bindings on the scope.
2. `context.SetRunningText(CreatingPullRequest)`, then `context.EndCancelWindow()`
   (`OperationContext.cs:85`; D315): a Cancel before it ends the operation as cancelled; after it
   Cancel is no longer offered. The POST keeps the 30 s limit and gets `CancellationToken.None`.
3. The open pull requests listed for this head and base (`Hosting.State.List`) are noted. `await
   Hosting.CreatePullRequestAsync(baseRepository, new NewPullRequest(title, body, headBranch,
   headRepository == baseRepository ? null : headRepository.Owner, baseBranch, draft),
   CancellationToken.None)`; repositories compared by `FullName`, ignoring case.
4. Created: `await Hosting.RefreshPullRequestsAsync(CancellationToken.None)` when the base is
   `State.Repository` (the list then shows it, and the badge follows), then `OperationReport.Done
   (Created(number, draft)) with { Offer = new OperationOffer(OpenInBrowser, () => OpenPullRequestAsync
   (created)) }` (WP-P's guarded action). The browser never opens by itself (ruling 13).
5. Failed: worded as 12.6; the report is `Failed(text, StoppedFailure(words))`
   (`RepositoryOperations.Run.cs:918`, `.Branches.cs:1296`), and the words also reach the form. A
   422 whose `Detail` starts "A pull request already exists": the list is read again; an open one from
   this head into this base gives `AlreadyOpen` and `ExistingNumber`; none: GitHub's words. `Network`
   or `ServerError` (the POST may have arrived, `CreatedThenDropped`): the list is read again; one
   from this head into this base not listed before counts as created (step 4) (D316).

**Afterwards** (from the `OperationResult`): succeeded, the form completes; cancelled, the error line
says `Cancelled`; failed, the plan's words, or for a git failure: a guard's refusal its own summary;
`PushRejected` `OperationTexts.PushRejected(remote, branch)` (`OperationTexts.cs:380`);
`AuthenticationFailed` with `RefusedAccount` the error dialog's summary,
`OperationTexts.GitCredentialsRefused(login)` (4.7, D276; the POST's 401 has `TokenRefused`, 12.6); any other `PushFailed(branch, target,
git's first line)`. Git's output is in the activity log, as for the Remotes form. A cancel that left
lock files still opens the lock dialog on top of the form. After a failure the status bar says
"Create pull request for topic failed" (danger), as any operation.

Logged: (Info) "{repo}: Create pull request for topic: into octo/hello main, draft no, push to
origin/topic first" (or "no push") and the number created; (Warning) an API failure's kind, GitHub's
message and `RequestId`. Never a token, the title, the description, a request or response. The form
opens at once; A3, the push, the POST and the list are awaited without blocking the UI thread; the
window's close waits for the operation, and `CancelAll` closes the form, never the operation.

Git fact (2.56): `git push --progress --set-upstream origin refs/heads/feature:refs/heads/feature`
prints ` * [new branch]      feature -> feature`, exits 0 and writes `branch.feature.remote=origin`
and `branch.feature.merge=refs/heads/feature`; a commit later `%(upstream:track)` is `[ahead 1]`; an
up-to-date push prints "Everything up-to-date". 2.43 and 2.55 print the same summary line. With `topic`
made by `git branch topic origin/main` and one commit, `git push --progress origin
refs/heads/topic:refs/heads/topic` exited 0 and wrote `refs/remotes/origin/topic` at the tip;
`branch.topic.merge` stayed `refs/heads/main`, the bare repository's `main` did not move, and
`%(upstream:track)` still read `[ahead 1]`. `git rev-list --count` exits 128 for a missing remote
branch, so the count is read only when it exists.

### 12.6 Texts (`CreatePullRequestTexts`)

"GitHub" comes from `HostingTexts.GitHub`. Counts use the singular for one (D113). Form and status
bar: `DialogTitle` "Create pull request"; `From`, `Into`; `ReadingGitHub` "Reading GitHub…";
`TitlePlaceholder` "Title", `DescriptionPlaceholder` "Description", `Draft` "Draft"; `Create`
"Create", `Creating` "Creating…"; `ChooseBaseTitle` "Choose the branch to merge into"; reasons
`ChooseBase` "Choose the branch to merge into." and `EnterTitle` "Enter a title."; `OpenExisting(n)`
"Open pull request #1"; `OpenInBrowser` "Open in browser". Operation: `Operation(head)` =
`new($"Create pull request for {head}", "Creating pull request…", "Create pull request failed", "GitHub
could not create the pull request.")`; `CreatingPullRequest` "Creating pull request…"; `Created(n,
draft)` "Created pull request #9" or "Created draft pull request #9". Head notes:
`PushedFirstWithUpstream` "Not on GitHub yet: Create pushes it to origin/local-work first and sets
that as its upstream."; `PushedFirst` "Not on GitHub yet: Create pushes it to origin/local-work
first."; `AheadPushedFirst` "1 commit is not on GitHub yet: Create pushes it to origin/feature first."
or "2 commits are not on GitHub yet: Create pushes them to origin/feature first."; `GonePushedFirst`
"origin/feature is gone: Create pushes the branch to it again first."

| When | Text |
|---|---|
| head and base one branch | `SameBranch`: "Choose a branch other than main to merge into." |
| 422 "already exists", found in the list | `AlreadyOpen`: "Pull request #1 is already open from feature/login into origin/main." |
| 422, `Detail` starts "No commits between" | `NoCommits`: "origin/main already has every commit of topic: there is nothing to merge." |
| 422, `Detail` "head is invalid" | `HeadNotAccepted`: "GitHub does not accept me:topic as the branch to merge from. It must be in octo/hello or in a fork of it." |
| 422, `Detail` "base is invalid" | `BaseUnknown`: "GitHub has no branch release in octo/hello. Choose another branch to merge into." |
| any other 422, or "already exists" not listed | `GitHubSaid`: GitHub's words (ruling 15), "{Message}: {Detail}" or the message alone |
| 403, head and base in different repositories | `ForkTokenRefused`: "GitHub refused to create a pull request in octo/hello with this token. A fine-grained token cannot open pull requests in another owner's repository; sign in with a classic token that has the repo scope to do that." |
| other 403 | `Refused`: "GitHub refused to create a pull request in octo/hello with this token." and, with `Detail`, " It needs the permission pull_requests=write." |
| 401 | `OperationTexts.TokenRefused(login)` (WP-C's, 4.7, D276) through `HostingTexts.Describe(failure, login)`, `{login}` the signed-in account's: "GitHub refused the token of octo. Sign in to GitHub in Settings, Accounts." |
| git's `erase` during the push (`RefusedAccount`) | `OperationTexts.GitCredentialsRefused(login)` (WP-C's, 4.7, D276, D330): "GitHub refused git's credentials for octo. If the token has expired or was revoked, sign in to GitHub again in Settings, Accounts." |
| 404 | `NotVisible`: "This account cannot see octo/hello on GitHub." |
| any other failure (`Network`, `RateLimited`, `SsoRequired`, `ApiVersion`, `ServerError`, `Malformed`) | `HostingTexts.Describe(failure, login)` (1.7, D338), as it is: "Could not reach GitHub.", "GitHub's rate limit was reached. Try again after 14:05.", … |
| a git failure of the push, other than rejected | `PushFailed`: "Git could not push topic to origin/topic: {git's first line}" |
| cancelled | `Cancelled`: "Cancelled before the pull request was created." |

The same-organisation fork that needs `head_repo` (r5 A5) is not built: its 422 shows GitHub's words.

### 12.7 Tests (in the 7a project unless named)

- `OperationQueueOfferTests` (`VisualCommit.Git.Tests`, beside `OperationQueueTests`): a succeeded
  report's offer reaches `OperationResult` and `Finished`; a failed, conflicts or cancelled one has
  none; a deduplicated second caller gets the same result.
- `StatusBarOfferTests` (`[AvaloniaFact]`): the offer after a success; gone when the user's next
  operation starts and after "Auto-fetch failed"; kept through a background success; hidden under
  ssh's notice; `OperationOfferButton` collapsed before any create, the bar's other parts where they
  are today; never on a dashboard.
- `CreatePullRequestDialogViewModelTests` (`[Fact]`, a fake `ICreatePullRequestActions`): defaults;
  loading, a base chosen meanwhile winning; the disabled reasons; the same branch; creating (all
  disabled, Esc ignored, "Creating…"); a failure's words and the note read again; the existing pull
  request's button; edits clearing the error; Cancel cancelling `StartAsync`.
- `CreatePullRequestOperationTests` (Hosting.Tests, D334; `[AvaloniaFact]`, `ShellDriver.Start(gitHub:, tokens:)`, a
  `FakeGitHub` on port 0, `SignInAsync`): a pushed branch (the POST's head, base and draft; the report,
  the offer, the list); an unpushed one (one operation record; the running texts in order, the POST
  held by `HoldNext("create:octo/hello")`; `branch.local-work.merge`; Cancel offered while pushing
  only); an ahead one pushed to its upstream; `topic` made by "Create branch here" on `origin/main`
  with one commit (tracking `origin/main`): From topic and Into origin/main without `SameBranch`, the
  note `PushedFirst`, the push to `origin/topic` with `refs/heads/main` in the fake's bare repository
  unmoved and `branch.topic.merge` still `refs/heads/main`, then the note null, and `AheadPushedFirst`
  after one more commit; a remote-branch head; a fork's head (`HeadOwner`); each
  error row through `FailNext`, the POST's 401 giving `OperationTexts.TokenRefused(login)` and a
  push's `RefusedAccount` (the token `Revoke`d) `OperationTexts.GitCredentialsRefused(login)`; `CreatedThenDropped` counted as created; signed out with the sign-in
  refused; `TokenTripwire.AssertAbsent` after a create that pushed.

The builder also runs `OperationQueueTests`, `OperationQueueUiTests`, `OperationStatusViewModelTests`,
`StatusBarTests`, `DashboardBulkViewModelTests`, `RepositoryOperationsRemotesTests`,
`PickDialogViewModelTests`, `DialogTests`, `MessageDialogViewModelTests`, each with `--filter-class`.

### 12.8 Part N's checks

Scenario 2.6 on 47395, `hello/`, signed in with `OctoToken` through `ShellDriver.SignInAsync`.
`topic/no-pr` is pushed with no open pull request (#4 is closed); `local-work` is not on GitHub. Every
check first calls `PartN.PrepareAsync(served.Hello)` on the copy, before `OpenAsync`, and its R
pictures are taken in that state (2.6). In `Phase7aExpectations.N.cs`: `public static Task
PrepareAsync(TempRepo hello) => hello.GitAsync("branch", "--track", "topic/no-pr",
"origin/topic/no-pr");`, a local branch tracking `origin/topic/no-pr` (default title "Try an idea"),
so the left panel's local branches hold its row and the graph its local label.

| Check | Shows | Real window |
|---|---|---|
| N1 From a branch | `ForLocalBranch`'s "Create pull request…" on `topic/no-pr`; the form's defaults (topic/no-pr · octo/hello into origin/main · octo/hello, title "Try an idea"); after Create the list's row, the badge, "Created pull request #N" and "Open in browser", whose click records `OpenUrl` with the `html_url` | the form; the status after creating |
| N2 Not on GitHub | `local-work`'s note; "Creating…" with "Creating pull request…" (the POST held); then `branch.local-work.merge`, the branch on the fake, one activity-log entry | |
| N3 Draft | the POST's `draft: true`; the row marked draft; "Created draft pull request #N" | |
| N4 GitHub's 422 | already open (a pull request added on the fake after the list was read), with "Open pull request #N"; no commits between | |
| N5 Remote branch, drop | `ForRemoteBranch`'s item and form; `local-work` dropped on `origin/main`: `ForDrop`'s item, Into origin/main | |
| N6 Signed out | "Add GitHub account" stacks first; Cancel: no form, no request | |

The form also in light and at 1920×1080. About 6 methods. Code: `Phase7aChecks.N.cs`
(`Phase7aChecksN : Phase7aChecks`), `.N.Helpers.cs`, `Phase7aExpectations.N.cs` (the texts by hand,
the R pictures, branch, base, title and status, `PrepareAsync`), `Phase7aExpectationTests.N.cs` (with
the pin: on `Scenarios.HostingAsync()` after `PrepareAsync`, `branch.topic/no-pr.remote` is `origin`
and `branch.topic/no-pr.merge` `refs/heads/topic/no-pr`). Requests are counted
on `FakeGitHub.Requests`, git calls through `GitCallBudget` only (ruling 28). No earlier picture
changes: the offer's button is collapsed until a create; status-bar pictures are compared by region
against `master`'s references with the same git (D248's box).

Commits: (1) Core's offer, the queue's line, `OperationQueueOfferTests`; (2) the status bar,
`StatusBarOfferTests`; (3) texts, the form, `CreatePullRequestDialogViewModelTests`; (4)
`RepositoryOperations.CreatePullRequest.cs`, `CreatePullRequestOperationTests`.

### 12.9 At the merge; done when

The lead runs the lagged list; phase 3's walk-through (`dotnet test --project
tests/VisualCommit.VisualTests.Phase3`, through `scripts/heavy-run.sh`), comparing its status-bar
pictures by region with `master`'s; part N; then WP-LIVE may start.

Done when the four classes and the listed ones pass; part N's checks pass with every picture
inspected; no earlier picture changes beyond D248's box; the token tripwire is clean; Create never
leaves the UI waiting.

## 13. WP-R: the real-window pass

### 13.1 Goal and files

Phase 7a's real-window checks: the steps each part's report marks "R" (0.5), repeated in the built app
on the Windows desktop with the real mouse and keyboard, and compared with their scripted twins at
most 3 % apart (D33, D58). It serves status step 18 (the gate's real-window pass and D246's two runs
before the counted round). Rulings 23 to 25; D319 to D322. Wave 3, on the Windows machine, after WP-A,
WP-B, WP-P, WP-V and WP-N have merged; branch `wip/p7a-real-window`. The builder writes and builds it;
only the lead runs it.

What it proves that the scripted run cannot: the real process takes the fake's URL from
`VISUALCOMMIT_GITHUB_URL` and the memory keychain from `VISUALCOMMIT_KEYCHAIN`, and its own
`SocketsHttpHandler` reaches the fake on loopback; the token typed on the real keyboard stays masked to
UI Automation (`SecretTextBox`, `src/VisualCommit.App/Controls/SecretTextBox.cs:16`, whose peer's value
is empty while masked, `:35`); a clone in the real process through `visualcommit-askpass.exe` beside
`src/VisualCommit.App/bin/Release/net10.0/VisualCommit.exe`, whose path holds the space of
`D:/Projects/Personal/Visual Git`, run by Git for Windows' `sh`; the native window and scaling; if
allowed, "Open in browser" reaching a real browser. Out: restarts (the store is memory); the real
keychains (CI); 1920×1080 and light (scripted only); every "Copy …" item, never clicked.

| File | Change |
|---|---|
| `tests/VisualCommit.RealWindowTests/Phase7aRealWindowPass.cs` | new: the "what is where" comment, `Phase7aRun`, `Phase7aOrder`, the launches, waits and comparisons |
| `Phase7aRealWindowPass.A.cs`, `.B.cs`, `.N.cs`, `.G.cs`, `.P.cs` | new: one method per part |
| `RealApp.Phase7a.cs` | new: the hosting environment, the sign-in, the new screens' reads |
| `RealApp.Phase6.Settings.cs` | `SettingsNavIds` (`:22-28`) gains `["Accounts"] = "SettingsNavAccounts"`; `ShownSettingsSection` (`:48-53`) gains `ShownElements("AccountsIntro").Count == 1 ? "Accounts"` before `: null` |

Not WP-R's: the csproj's 7a link (WP-F), its comment (L1), `Screens` and `RealWindowRun` with a
`string` phase (WP-F). It reads `Phase7aExpectations.Phase`, `ServerPort`, `WithPort` and each part's
`PartA` … `PartN` (texts, R picture names, rows, menus; `PartG`'s and `PartN`'s `PrepareAsync`), linked
by WP-F's wildcard, and the automation
ids the briefs name: WP-A's `SettingsNavAccounts`, `AccountsIntro`, `AccountsSignedOut`, `AccountLogin`,
`AccountTokenPlace`, `AccountsSignInButton`, `AccountUseForGit`, `SignInDialog`, `SignInToken`,
`SignInError`; WP-B's `CloneFromGitHubButton`, `CloneFromGitHubDialog`, `CloneFromGitHubFilter`,
`CloneFromGitHubItem`; WP-P's `PullRequestItem`; WP-N's `CreatePullRequestDialog`, `PullRequestBase`,
`PullRequestTitle`, `PullRequestDescription`, `PullRequestDraft`, `OperationOfferButton`; WP-V's
`DetailsAuthor`, `DetailsAuthorAvatar`. It adds no product code and holds only the fake's tokens.

### 13.2 Conventions (phase 6's, `Phase6RealWindowPass.cs:23-41`, plus these)

- One method per part, one launch each, at 1100×700, dark (D319): `Check_A_accounts`,
  `Check_B_browse_and_clone`, `Check_N_new_pull_request`, `Check_G_badge_and_avatars`,
  `Check_P_pull_requests_and_browser`, in that order (`Phase7aOrder`, as `Phase6Order`,
  `Phase6RealWindowPass.cs:75-98`); P is last so that the browser step is the last input.
- Declare the scenario and the fake first, then the app, so the app is stopped first:
  `using var hello = await Scenarios.HostingAsync();`
  `await using var served = await Scenarios.ServeHostingAsync(hello);`
  `await PartX.PrepareAsync(hello);` (only where 13.4 names it: the same call the scripted twin makes,
  2.6)
  `using var app = LaunchServed(served, emptyTab: …);`.
- Every launch signs in by typing `served.OctoToken` into the real dialog (D320).
- After an input, wait for a named condition (a log line, a UI Automation element or text, a request
  in the fake's log), each with one of the pass's limits; never a fixed time.
- `Compare(app, name)` with the scripted picture of the same name (`PartX`'s constant), the pointer at
  the neutral point unless a menu is open; at most 3 %. The 7a walk-through's pictures must exist.
- Never pass a token to an `Assert` method: test with `string.Contains` and fail with a message
  naming the place. Never click "Copy link", "Copy …", "Open file", "Open in editor", "Show in folder"
  or the status bar's "Open in browser"; never change a desktop setting (D224).

### 13.3 The run, the helpers, `RealApp.Phase7a.cs`

```csharp
public sealed class Phase7aRun : IDisposable
{
    public RealWindowRun Run { get; } = new(phase: Phase7aExpectations.Phase);   // "7a"
    internal bool DisplayNoted { get; set; }
    public void Dispose() => Run.Dispose();
}
[TestCaseOrderer(typeof(Phase7aOrder))]
public partial class Phase7aRealWindowPass(Phase7aRun fixture) : IClassFixture<Phase7aRun>
```

Limits as phase 6's (`:110-120`): `LoadTimeout`, `OperationTimeout`, `NetworkTimeout` 60 s,
`StepTimeout` 30 s. Copied, with a comment naming the source: `Sized` (`:183-201`), `Opened`
(`:208-221`), `Compare` (`:269-279`), `AssertMenu` with `ItemLabels`, `DisabledItems` and the two
regular expressions (`:284-330`, `:699-703`). New:
- `RealApp LaunchServed(ServedHosting served, bool emptyTab)`: `Run.NewDataDirectory()`; one tab,
  `new TabState(null)` or `new TabState(served.Hello.Path)`; `RealApp.Launch(data, session,
  RealApp.HostingEnvironment(served.GitHub.Port))`; `Sized`; then `app.WaitForLogLine(AppLogFile
  .Containing($"GitHub: 127.0.0.1:{port}."), LoadTimeout)` (WP-S's start line) and `Opened(app,
  "hello", 0, 0)` or the welcome page's `CloneRepoButton`. `NotePort(served.GitHub)` writes to run.txt
  when 47395 was taken.
- `FakeGitHubRequest WaitForFake(FakeGitHub gitHub, int mark, Func<FakeGitHubRequest, bool> match, string what, TimeSpan timeout)`:
  polls `gitHub.Requests` after `mark` every 100 ms.
- `void WaitForHostingSettled(RealApp app, int logMark)`: waits for `AppLogFile.PullRequestsListed`
  and `AppLogFile.AvatarsShown` since the mark (D322).
- `void AssertCleanEnd(RealApp app, ServedHosting served, params string[] folders)`: `Assert.Equal(0,
  app.Close())`; `TokenTripwire.AssertAbsent(served.OctoToken, [app.DataDirectory, .. folders], calls:
  null)` (the app's log is in the data folder); `served.GitHub.Errors` empty; no API request answered
  400 or 415; no `avatar:` request with `TokenPresent`.

```csharp
public sealed partial class RealApp
{
    public const string SignInDialogId = "SignInDialog";
    public const string CloneFromGitHubDialogId = "CloneFromGitHubDialog";
    public const string CreatePullRequestDialogId = "CreatePullRequestDialog";
    public static string HelperPath { get; }   // beside Executable (RealApp.cs:25): visualcommit-askpass.exe
    // VISUALCOMMIT_GITHUB_URL = http://127.0.0.1:<port>/ and VISUALCOMMIT_KEYCHAIN = memory, applied last (RealApp.cs:211-222).
    public static IReadOnlyDictionary<string, string?> HostingEnvironment(int port);
    public void OpenAccountsSection(TimeSpan timeout);   // a real Ctrl+, then ChooseSettingsSection("Accounts")
    public AutomationElement WaitForSignInDialog(TimeSpan timeout);   // stacked; the focus in SignInToken
    public string SecretBoxValue(string automationId);   // the Value pattern; "" while masked
    // Types the token, asserts SecretBoxValue("SignInToken") == "" and the name "Token", clicks the
    // dialog's own ConfirmButton (FindIn: the screen has one too), waits until SignInDialog is gone.
    public void TypeTokenAndConfirm(string token, TimeSpan timeout);
    // OpenAccountsSection, a click on "Sign in…", WaitForSignInDialog, TypeTokenAndConfirm, waits until
    // AccountLogin reads login, then Esc: the sign-in stays (D256).
    public void SignInThroughSettings(string token, string login, TimeSpan timeout);
    public IReadOnlyList<string> CloneBrowserRowNames();   // CloneFromGitHubItem names, in order
    public IReadOnlyList<string> PullRequestRowNames();    // PullRequestItem names in RefList, top to bottom
    public AutomationElement PullRequestRow(string name);
}
```

`RefRow` (`RealApp.Checks.cs:177`) finds `RefItem` and `Section` elements only, so the section's header
is `RefRow(LeftPanelViewModel.PullRequestsTitle)` (`LeftPanelViewModel.cs:22`) and its rows come from
`PullRequestRow`.

### 13.4 The five launches

Each launch names its preparation (13.2): none for A, B and P; `PartN.PrepareAsync` for N and
`PartG.PrepareAsync` for G, each the call its scripted twin makes (11.5, 12.8).

**A. Accounts** (one empty tab).
1. Launch. `served.GitHub.Requests` is empty (D257).
2. A real Ctrl+, then a click on "Accounts": the five nav buttons' names are General, Git, Tools,
   Keyboard shortcuts, Accounts; "Accounts" selected; "Not signed in." and "Sign in…" enabled. Still no
   request. `Compare` with part A's signed-out picture.
3. "Sign in…": "Add GitHub account" stacks over the screen, the focus in the token box. Type the
   token: the box's value is empty, its name "Token"; no text element holds the token. `Compare` with
   the masked-token picture.
4. "Sign in": since the mark, exactly one fake request, `GET api/v3/user`, `TokenPresent`, 200. The
   row reads `octo` and "The token is kept in this computer's keychain."; the switch on. `Compare`.
5. Esc closes the screen; `accounts.json` names `octo`, host `127.0.0.1:<port>`, `useForGit` true,
   and does not contain the token. `AssertCleanEnd`.

**B. Browse and clone** (one empty tab).
1. Launch; a real Ctrl+Shift+P, `TypeInPalette("Clone from GitHub", …)`; the highlighted row is
   "Clone from GitHub…"; Enter.
2. Signed out, "Add GitHub account" comes first (D261, D300): `WaitForSignInDialog`, then
   `TypeTokenAndConfirm`. Settings stays closed, so no `AccountLogin` shows; step 3's wait is the
   sign-in's proof.
3. The form opens with "Clone from GitHub" over it: wait for `CloneFromGitHubDialog`, then until
   `CloneBrowserRowNames()` equals part B's list, in order, and `CloneFromGitHubFilter` has the focus.
   `Compare`.
4. A click on `octo/hello` (private), then "Choose": `CloneUrlBox` holds
   `WithPort("http://127.0.0.1:47395/octo/hello.git")`, `CloneNameBox` `hello`.
5. `ReplaceText("CloneParentBox", clones.Path)` (`using var clones = new TempDirectory("rw7a-clone")`
   before the app); mark `served.GitHub.GitRequests`; a click on "Clone". `Opened(app, "hello", 0, 0)`
   within `NetworkTimeout` (a time-out says whether `CredentialsDialog` shows). No
   `CredentialsDialog`. Since the mark, `octo/hello`'s git requests include one answered 200 whose
   `GivenUserName` is `x-access-token` and none with another user name.
6. run.txt: `B: the clone went through {HelperPath}` and whether that path holds a space.
7. After `app.Close()`, with `GitRunner` and `TempRepo.IsolatedEnvironment`, in `clones/hello`:
   `git config --get remote.origin.url` prints step 4's URL; `git config --get-regexp ^credential`
   exits 1. `AssertCleanEnd(app, served, clones.Path)`.

**N. New pull request** (one tab on `hello`).
1. Launch; `SignInThroughSettings`; `WaitForHostingSettled`.
2. A real right-click on part N's branch row (`RefRow(LeftPanelViewModel.LocalBranchesTitle, …)`),
   then "Create pull request…".
3. `CreatePullRequestDialog`: wait until `PullRequestBase` shows part N's base and the focused title
   box holds its default title. `Compare` with the form's picture.
4. Mark the fake and the log; a click on "Create". Wait for `AppLogFile.OperationEnded("hello",
   <part N's operation title>, OperationOutcome.Succeeded)`. Since the mark: one `create:octo/hello`
   request, 201, its `Body` with part N's title, head, base and `"draft":false`; if the branch needed a
   push, a receive-pack request with `GivenUserName` `x-access-token`. The status reads part N's
   "Created pull request #N", `OperationOfferButton` named "Open in browser" (never clicked), and
   `PullRequestRowNames()` holds the new row. `Compare` with the status picture.
5. `AssertCleanEnd`.

**G. Badge and avatars** (one tab on `hello`).
1. Launch; `SignInThroughSettings`; `WaitForHostingSettled`. The fake answered `avatar:<id>` for part
   G's authors without a token; `avatars/<id>.png` exist in the data folder.
2. A click on part G's commit row (`app.GraphRowPoint(row, lanes)`); wait for `DetailsAuthor` with
   part G's author and `DetailsAuthorAvatar` shown. `Compare` with the graph-and-details picture.
3. `AssertCleanEnd`.

**P. Pull requests, and the browser** (one tab on `hello`).
1. Launch; `SignInThroughSettings`; `WaitForHostingSettled`; `PullRequestRowNames()` equals part P's
   list (the draft marked). `Compare`.
2. A right-click on the "Pull requests" header: `AssertMenu` with part P's section menu (header "Pull
   requests"; "Refresh pull requests", "Open pull requests in browser"); `Compare` with
   `keepPointer: true`; Esc.
3. A right-click on part P's row: its menu's labels ("Open in browser", "Check out {head}", "Copy link").
4. The browser step (13.5), or Esc.
5. `AssertCleanEnd`.

### 13.5 Open in browser (D321)

`private const bool OpensTheBrowser = true;` at the top of `Phase7aRealWindowPass.P.cs`: the owner's
answer of 2026-10-29, that the pass may open the default browser once, on the fake's page.
- `true`: mark the fake's requests and the log; a real click on "Open in browser". Wait up to
  `NetworkTimeout` for a request with route `page:octo/hello`, path `octo/hello/pull/<n>`, status 200
  and `TokenPresent` false: the real launcher handed the URL to the default browser, which fetched it.
  No `[WRN]` line in the app's log since the mark. Then a real click at the neutral point (the status
  bar's empty middle, `WindowLayout.cs:73-85`) takes the activation back. run.txt: "P: the browser
  opened {url}; its tab is left open." The tab is the owner's to close.
- `false`, only where the step cannot run (no default browser, or one that never fetches the fake's
  page, 15, point 2) and with the owner's word: Esc; run.txt: "P: Open in browser not clicked"; the
  report moves the step to the checks by hand at acceptance.

Risks, not tried: a browser that upgrades `http://127.0.0.1` to HTTPS or shows a first-run page
fetches nothing and the step fails after 60 s; a machine without a default browser shows Windows'
"How do you want to open this?". The app's window stays on top (`HwndTopMost`, `RealApp.cs:243-245`).

Git facts (2.56): a helper given as `!'<path with a space>/helper.sh' --vc-credential` in
`GIT_CONFIG_COUNT` pairs ran under Git for Windows' `sh` with `--vc-credential get`, and `git credential
fill` returned its user name and password, so 0.2's quoting carries `Visual Git`; git writes nothing of
the environment's configuration into a clone, so B7's `--get-regexp ^credential` exits 1.

### 13.6 Time

0.6 gives the estimate, of the pass and of D246's run of the 7a class alone, and how it is reached.
Phase 6's real-window `s14a-general` and `l31d-settings-light` change with their twins (0.7); no code
changes for them.

### 13.7 The lead's steps and the gate

The builder runs `dotnet build tests/VisualCommit.RealWindowTests -c Release` (no warnings) and the
parts' expectation classes (`dotnet test --project tests/VisualCommit.VisualTests.Phase7a
--filter-class "*Phase7aExpectationTests*"`); never the pass.

At the merge, CLAUDE.md: a Commands row "A phase's own real-window checks alone (Windows only; D246's
two runs)": `bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release
--filter-class "*Phase7aRealWindowPass"` (it takes the mouse like the pass and empties
`artifacts/visual/phase-7a/real-window/`); in "What to know", that the 7a pass starts `FakeGitHub` on
47395 in the test process, so the 7a walk-through never runs beside it, and that the owner's default
browser opens one tab near the end (D321); after the gate, the measured length.

The gate (status step 18), as phase 6's handoff ("Running the visual gate", steps 1 to 6), with:
1. Worktrees removed and fsmonitor daemons stopped.
2. Owner told: WP-LIVE's command once (14.6, D335). Its corrections to the fake (14.8) are made and
   committed now, before any picture of the gate is taken, so they land before the passes. Made once.
3. `bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase7a`: 7a's scripted pictures
   on this machine (0.6 gives the times of steps 3 to 5).
4. Tell the owner, who is present; desktop unlocked, high contrast off: D246's two runs of the row
   above, one after the other; fix what fails; record both in the report's "Run". Made once.
5. The counted round: `bash scripts/heavy-run.sh`, then, owner told, the whole pass, then at once the
   list `docs/test-reports/phase-7a-pictures.sha256`, then WP-LIVE's command again (14.6), which
   writes nothing under `artifacts/visual`.
6. Inspection (D247): 7a's real-window captures with their twins and difference pictures in one batch
   (about nine captures), besides the earlier phases' batch.
7. After a fix that changes only `tests/VisualCommit.RealWindowTests`, D246's rule; the live command
   does not run again for it (its project builds nothing there). After a fix that changes only
   `tests/VisualCommit.LiveHostingTests`, D335's: only the live command runs again, on that commit, and
   the Gate commit stays the passes' commit (closing step 6); that push's CI run, whose Windows job
   builds the project (D337), is read as any other. Any other fix, a correction to the fake
   among them, starts a new counted round.

For D335 (confirmed on 2026-10-29), the lead changes, besides the rows above: CLAUDE.md's "Phase N accepted" row,
whose `git diff --stat` gains `':(exclude)tests/VisualCommit.LiveHostingTests'`; CLAUDE.md's closing
step 6, whose "last commit that changed anything other than docs" leaves that folder out too; and
`docs/plan.md`'s Visual test gate step 6, which states the exception beside D246's. The design edits
neither file.

### 13.8 Done when

The project builds in Release with the five methods in `Phase7aOrder`'s order; each R step of the
parts' reports has its step here and its comparison under the twin's name; each launch's state comes
from the same `PartX.PrepareAsync` its scripted twin calls (none for A, B and P); no "Copy …" click; no
token passed to an `Assert`; the browser step runs (`OpensTheBrowser` true, D321); the CLAUDE.md rows are with
the lead. At the gate: both D246 runs and the counted pass green, every capture within 3 % and
inspected.

## 14. WP-LIVE: the opt-in live command

### 14.1 Goal and files

The Delivers item "an opt-in command, Windows-only and never in the default run or CI, that signs in,
clones, lists pull requests and creates one on real GitHub with the owner's fine-grained token for a
sandbox repository, read from an environment variable" (D250, D260, ruling 26; D323 to D328, D335, D337), status
step 16. Wave 3, after WP-B and WP-N have merged; branch `wip/p7a-live`. It also records what r5 14
leaves open, so the lead can correct the fake. It takes no screenshots and runs twice per gate: once
before the gate's pictures, so that its corrections to the fake land before the passes, and once on the
counted round (D335, 13.7). Never in a cloud session (Windows-only; no token there). CI's Windows job
compiles it and never runs it (D337, amending ruling 26), so a change elsewhere that breaks it shows on
that push, not at the gate.

| File | What |
|---|---|
| `tests/VisualCommit.LiveHostingTests/VisualCommit.LiveHostingTests.csproj` | new (14.2) |
| `.../AssemblyInfo.cs` | the headless app, the watchdog, no parallelism, the module initializer |
| `.../LiveSettings.cs` | the two variables, `Require()`, `LiveSandbox` |
| `.../LiveRun.cs` | the class fixture: notes, and `artifacts/live/github/run.txt` at its end |
| `.../LiveRequestLog.cs` | a `DelegatingHandler` recording the app's requests, never a header's or body's value |
| `.../GitHubLiveApi.cs` | the test's own API calls: read, close, delete, the duplicate create |
| `.../GitHubLiveTests.cs` | `GitHubLiveOrder` and the ordered class `GitHubLiveTests` |

Not WP-LIVE's: `ShellDriver.Start`'s `hosting:` parameter (WP-F, 2.5); the slnx comment, CLAUDE.md and
`ci.yml` (the lead's). It uses WP-A's `ShellDriver.Phase7a.Accounts.cs` (`OpenAccountsAsync`,
`OpenSignInDialogAsync`, `TypeToken`, `ConfirmSignInAsync`, `WaitForAccountAsync`), WP-B's ids
(`CloneRepoButton`, `CloneFromGitHubButton`, `CloneFromGitHubFilter`, `CloneFromGitHubItem`), WP-N's
form ids and `CreatePullRequestTexts`, and `LeftPanelViewModel.LocalBranchesTitle` (`LeftPanelViewModel.cs:20`).

### 14.2 The project

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- The opt-in live command of phase 7a (D250, D260): the whole app in Avalonia's headless mode
       against real GitHub, with the owner's fine-grained token for one sandbox repository. It signs
       in through the "Add GitHub account" dialog, finds the sandbox in the repository browser,
       clones it over HTTPS through the app's own credential helper, lists its pull requests,
       pushes a branch visualcommit-live/<UTC time> with one commit and opens a draft pull request
       through the form, reads it back, then closes it and deletes the branch. The token and the
       sandbox come from the Windows user variables VISUALCOMMIT_LIVE_GITHUB_TOKEN and
       VISUALCOMMIT_LIVE_GITHUB_REPO (owner/name); without them every test fails at once.
       Windows-only and opt-in: NOT in VisualCommit.slnx, so "dotnet build" and "dotnet test" at
       the repo root never touch it; CI's Windows job builds it and never runs it (D337). Run it with the live command in CLAUDE.md's
       Commands, at a gate before its pictures and on the counted round (ruling 26, D335): about
       two minutes with its build (CLAUDE.md has the measured time). No screenshots; no keychain
       (the token stays in memory);
       its findings in artifacts/live/github/run.txt, never with the token. -->

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia.Headless.XUnit" />
    <PackageReference Include="xunit.v3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\VisualCommit.App\VisualCommit.App.csproj" />
    <ProjectReference Include="..\..\src\VisualCommit.Core\VisualCommit.Core.csproj" />
    <ProjectReference Include="..\..\src\VisualCommit.Git\VisualCommit.Git.csproj" />
    <ProjectReference Include="..\..\src\VisualCommit.Hosting\VisualCommit.Hosting.csproj" />
    <ProjectReference Include="..\VisualCommit.Testing\VisualCommit.Testing.csproj" />
  </ItemGroup>
  <ItemGroup>
    <AssemblyMetadata Include="RepoRoot" Value="$(RepoRoot)" />
  </ItemGroup>
</Project>
```

`$(RepoRoot)` comes from `tests/Directory.Build.props`. Through Git's reference to
`VisualCommit.AskPass` (`src/VisualCommit.Git/VisualCommit.Git.csproj:9`) the helper lands beside the
tests, as in every walk-through's `bin`.

### 14.3 `AssemblyInfo.cs` and the variables

```csharp
[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]
[assembly: HangWatchdog]
[assembly: CollectionBehavior(DisableTestParallelization = true)]   // one sandbox, steps in order

namespace VisualCommit.LiveHostingTests
{
    internal static class ProcessIsolation
    {
        [ModuleInitializer]
        internal static void Isolate()
        {
            LiveSettings.CaptureAndRemove();          // first: the isolation removes both too (1.8)
            HeadlessTestApp.IsolateFromTheMachine();
            if (Environment.GetEnvironmentVariable(HangWatchdogAttribute.LimitVariable) is null)
            {
                // Check_2's step limits sum to about 510 s (14.4), above the default 180 s.
                Environment.SetEnvironmentVariable(HangWatchdogAttribute.LimitVariable, "600");
            }
        }
    }
}
```

An xunit.v3 test project is an executable whose entry point is generated into the test assembly, so
its module initializer runs before `Main`, any test and Avalonia's set-up (which isolates again,
`HeadlessTestApp.cs:14-16`). Should that change, the read finds the variables gone and every test
fails with the message below, never silently. The watchdog reads its limit
(`VISUALCOMMIT_TEST_HANG_SECONDS`) once, when xunit constructs `[assembly: HangWatchdog]`
(`HangWatchdogAttribute.cs:30-33`), which is after `Main` has started and so after the initializer;
the isolation does not remove the variable, and a value the owner set is kept.

`LiveSettings` (internal, static): `TokenVariable = "VISUALCOMMIT_LIVE_GITHUB_TOKEN"`,
`RepositoryVariable = "VISUALCOMMIT_LIVE_GITHUB_REPO"`. `CaptureAndRemove()` reads both (trimmed) into
private statics once, then `Environment.SetEnvironmentVariable(name, null)` for each, so no git or app
process inherits them. `Require()` returns `LiveSandbox(string Owner, string Name, string Token)`
(`FullName`; `ToString()` without the token), or throws `InvalidOperationException`:

> The live command needs two Windows user environment variables, set once on this machine (D260):
> VISUALCOMMIT_LIVE_GITHUB_TOKEN, a fine-grained token for the sandbox repository only, and
> VISUALCOMMIT_LIVE_GITHUB_REPO, that repository as owner/name. {detail} Set them in Windows'
> Environment Variables dialog, then start a new terminal.

`{detail}`: "Missing: VISUALCOMMIT_LIVE_GITHUB_TOKEN and VISUALCOMMIT_LIVE_GITHUB_REPO.", or one of
them, or `VISUALCOMMIT_LIVE_GITHUB_REPO is "{value}", not owner/name.` (two non-empty segments of
letters, digits, `.`, `-`, `_`). Every test calls `Require()` first: none is skipped (a skip could
pass for green at a gate).

### 14.4 The class (`GitHubLiveTests.cs`)

`[TestCaseOrderer(typeof(GitHubLiveOrder))] public sealed class GitHubLiveTests(LiveRun run) : IClassFixture<LiveRun>`,
two methods in this order.

**`Check_1_the_sandbox_and_its_leftovers`** (`[Fact]`):
1. `Require()`; both names in `GitIsolation.MachineIndependence` with null; neither in the process
   environment any more.
2. `GitHubLiveApi.ReadRepositoryAsync`: `GET https://api.github.com/repos/{o}/{r}`: 200 with
   `permissions.push` true; else fail with "GitHub answered {status} {message} for {o}/{r} (request
   {id}): 401 means the token is wrong or expired, 404 that it cannot see the repository or the name
   is wrong." Record visibility, default branch and `X-GitHub-Api-Version-Selected`.
3. Leftovers of a stopped run (D324): open pull requests (`GET …/pulls?state=open&per_page=100`) whose
   `head.ref` starts with `visualcommit-live/` and whose head repository is the sandbox are closed;
   branches under it (`GET …/git/matching-refs/heads/visualcommit-live/`) are deleted; each recorded.
   Nothing else in the sandbox is touched.

**`Check_2_sign_in_clone_list_push_and_create`** (`[AvaloniaFact]`; D249, D255, D257, D261 and D251's
list and create against real GitHub). `stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss'Z'",
CultureInfo.InvariantCulture)`, `branch = "visualcommit-live/" + stamp`; `using var data = new
TempDirectory("live-data")`, `using var clones = new TempDirectory("live-clone")`. Each network step's
limit is 60 s, the clone's 120 s and each `GitHubLiveApi` call's 30 s: about 510 s in all, under the
watchdog's 600 s that the module initializer sets (14.3). A stop by the watchdog ends the process
mid-method: step 9's close and delete do not run and `LiveRun` writes no run.txt, the notes already in
the test output show the step it stopped in, and the next run's `Check_1` removes the leftover draft
and branch (D324).
1. **Start.** `var requests = new LiveRequestLog.Store();`
   `var hosting = new HostingOptions(GitHubEndpoints.GitHubCom, () => new LiveRequestLog(HostingOptions.DefaultHandler(GitHubEndpoints.GitHubCom)(), requests));`
   `using var app = ShellDriver.StartWithTabs(data.Path, [null], hosting: hosting);` (its default
   `FakeTokenStore` keeps the token in memory, never in a keychain). `Hosting.IsOff` false; the log
   holds "GitHub: github.com."; no request.
2. **Sign in.** `OpenAccountsAsync()`, `OpenSignInDialogAsync()`, `TypeToken(token)`,
   `ConfirmSignInAsync()`, `WaitForAccountAsync(s => s is { State: AccountState.SignedIn })`. The login
   equals the sandbox's owner, ignoring case (else "The token belongs to {login}, not to {owner}, the
   sandbox's owner."). On a failure the dialog's error line is the message. Since the start: exactly
   one request, `GET https://api.github.com/user`, 200, with `Authorization`. Esc closes the screen.
3. **Browse.** `CloneRepoButton`, then `CloneFromGitHubButton`; wait until the rows are listed, or
   fail with the status or error line. Record the count, `MoreExist`, how many rows are not the
   sandbox and whether each is public (no names). Type the sandbox's name into the filter; the row
   whose `FullName` equals `{owner}/{name}` ignoring case (else "The browser does not list
   {owner}/{name}."); select; Choose. `CloneUrlBox` is `https://github.com/{owner}/{name}.git`.
4. **Clone.** `ReplaceText("CloneParentBox", clones.Path)`; click `CloneStartButton`;
   `WaitForRepositoryAsync(TimeSpan.FromSeconds(120))`. A `CredentialsDialog` meanwhile fails at once:
   "Git asked for credentials: the app's helper did not answer (D255)." Then, with `GitRunner` and
   `GitIsolation.SessionGitEnvironment(data.Path)` over the process's isolation: `remote.origin.url`
   is step 3's URL; `git config --get-regexp ^credential` exits 1 in the clone; `git config
   --show-origin --get-all credential.helper` exits 1 (no helper of the user's or the system's, so Git
   Credential Manager never starts). The private sandbox (D326) proves the token reached the clone.
5. **Pull requests.** Wait until `HostingState.PullRequests` is `Loaded`; the section shows the rows or
   "No open pull requests"; record the count.
6. **Branch, push and create.** In the clone, with `GitRunner`, `TempRepo.IsolatedEnvironment` and the
   identity `VisualCommit live test <live-test@visualcommit.invalid>` dated now (D325): `git switch -c
   <branch>`; write `visualcommit-live/<stamp>.txt` ("VisualCommit live test {stamp}" and a line feed);
   `git add`; `git commit -m "VisualCommit live test {stamp}"`. Wait for the row
   `RefRowAsync(LeftPanelViewModel.LocalBranchesTitle, "visualcommit-live", stamp)`; right-click it;
   `ClickMenuItem("Create pull request…")`. In the form: wait for the base (the default branch); title
   `VisualCommit live test {stamp}`; description "Opened and closed by VisualCommit's live command
   (tests/VisualCommit.LiveHostingTests). Nothing to review."; tick Draft; Create. Wait for "Created
   draft pull request #N". No credentials dialog; the clone's config gains `branch.<branch>.remote=origin`
   and `branch.<branch>.merge=refs/heads/<branch>`; `HostingState.ByRef["refs/heads/" + branch]` is #N,
   a draft.
7. **Read back.** `GitHubLiveApi.ReadPullRequestAsync(N)`: 200; `draft` true, `state` open, `title` as
   typed, `head.ref` the branch, `head.sha` the clone's `HEAD`, `base.ref` the default branch,
   `user.login` the account's login.
8. **The duplicate create** (r5 14): `POST …/pulls` with the same head, base and title, draft true:
   record the status (422 expected) and the body as GitHub sent it.
9. **`finally`**, each in its own `try`: `PATCH …/pulls/{N}` `{"state":"closed"}` (200) and `DELETE
   …/git/refs/heads/{branch}` (204), both recorded. A failed clean-up fails the test after the record
   is kept (the next `Check_1` removes the leftover).
10. **Tripwires** (D327). `TokenTripwire.AssertAbsent(token, [data.Path, clones.Path], app.Session.GitCalls)`
    (the calls taken before `app.Dispose()`); every recorded request was `https`; every one with
    `Authorization` went to `api.github.com` (avatars from `avatars.githubusercontent.com` without it);
    none came before step 2.

The `[AvaloniaFact]` runs on the headless UI thread; every network wait is awaited, never `.Result`.

### 14.5 `LiveRequestLog`, `GitHubLiveApi`, run.txt (D323)

- `LiveRequestLog(HttpMessageHandler inner, Store store) : DelegatingHandler` records per request: time,
  method, scheme, host, path without the query (a query may hold an author's e-mail), status, whether
  `Authorization` was sent, and of the response only `X-GitHub-Request-Id`,
  `X-GitHub-Api-Version-Selected`, whether `X-OAuth-Scopes` was present, and
  `GitHub-Authentication-Token-Expiration`'s date. `Store` is thread-safe; one serves every handler
  the session creates (the client's and `AvatarCache`'s).
- `GitHubLiveApi(LiveSandbox sandbox)`: its own `HttpClient` over a `SocketsHttpHandler`
  (`AllowAutoRedirect` false), 30 s per call, `Authorization: Bearer <token>`, `Accept:
  application/vnd.github+json`, `X-GitHub-Api-Version: 2026-03-10` (`GitHubApi.Version`), `User-Agent:
  VisualCommit-live-tests`. A failure's message holds the status, GitHub's `message` and the request
  id, never a request or response object.
- `LiveRun` collects notes in order and at its `Dispose` writes `artifacts/live/github/run.txt`
  (outside `artifacts/visual`; `artifacts/` is git-ignored). Before writing, the text is searched for
  the token as written, URL-escaped and as the base64 of `x-access-token:<token>`; a find withholds the
  file and fails the run. `LiveRun.Note(text)` searches each note for the same forms before it keeps
  it or writes it to the test output, so a note holding the token (a response body, a prefix cut
  wrongly) never reaches the console: a find keeps "A note was withheld: it held the token." in its
  place and fails the run as a find in the file does. It records: date, git version, the
  sandbox's name, visibility and default branch; the token's kind by its prefix, `X-OAuth-Scopes`
  absent or present, the expiry date; `GET /user`'s status for a token with no account permission;
  the API version GitHub selected; the browser's count and how many rows lie outside the sandbox,
  each public or not; the leftovers removed; the clone's time; the open pull requests before; #N with
  its head SHA; the duplicate create's status and body; the clean-up's statuses; per host, the app's
  requests with and without `Authorization`; `X-RateLimit-Remaining` at the end; on a failure, the
  step and the failing call's request id.

The token is never passed to an `Assert` method, an exception message, a git argument or environment,
or a file; it is compared only with `string.Equals` or `string.Contains`. No `Capture`, no `VisualRun.Save`.

### 14.6 The command and the owner's token

`bash scripts/heavy-run.sh --project tests/VisualCommit.LiveHostingTests -c Release`: a whole-project
run, the lead's, under the lock (D245). It does not take the mouse; the owner is told because it acts
on their account. At a gate (13.7, D335): once before the gate's pictures, its corrections to the fake
committed before the passes; once on the counted round, after the real-window pass and the pictures
list (D328); and alone, on that commit, after a fix that changes only
`tests/VisualCommit.LiveHostingTests`, which keeps the Gate commit where it was.

Once, before the first run with them (status step 16), the owner:
1. Makes a **private** repository on their account, for example `visualcommit-sandbox`, with one
   commit on its default branch (a README).
2. On github.com: Settings, Developer settings, Personal access tokens, Fine-grained tokens, "Generate
   new token". Name "VisualCommit live test"; an expiry of their choice; resource owner their account;
   Repository access "Only select repositories", the sandbox alone; Repository permissions: Contents
   "Read and write", Pull requests "Read and write" (Metadata "Read-only" comes with them); no account
   permission. GitHub's permissions page gives Pull requests write for `POST` and `PATCH …/pulls`, read
   for the lists; Contents write for `DELETE …/git/refs/{ref}` and git's push; Metadata read for
   `GET /repos/{o}/{r}` and `GET /user/repos`; it names none for `GET /user`.
3. In Windows: Settings, System, About, "Advanced system settings", "Environment Variables…", under
   "User variables" two new ones: `VISUALCOMMIT_LIVE_GITHUB_TOKEN` (the token) and
   `VISUALCOMMIT_LIVE_GITHUB_REPO` (`<owner>/visualcommit-sandbox`); not typed in a shell, so the token
   lands in no shell history. Then a new terminal and a new Claude Code session.

Every shell of that session, and of every agent it starts, inherits the token. In such a session
nobody prints the whole environment (`env`, `printenv`, `set`, `export -p`, `Get-ChildItem Env:`):
it would put the token in the transcript. A variable is read by its name, or the list is filtered
(`env | grep -v '^VISUALCOMMIT_LIVE_'`; in PowerShell `Get-ChildItem Env: | Where-Object Name
-notlike 'VISUALCOMMIT_LIVE_*'`). The lead's brief to every agent of that session says so.

### 14.7 Git facts (2.56, isolated)

- With `GIT_CONFIG_NOSYSTEM=1`, an empty global file and an empty `XDG_CONFIG_HOME`, `git config
  --list --show-origin --show-scope` printed nothing: no `credential.helper` (the system file `C:/Program
  Files/Git/etc/gitconfig` holds `credential.helper=manager`, `http.sslbackend=openssl` and
  `http.sslcainfo`).
- Then `git ls-remote https://github.com/octocat/Hello-World.git HEAD` (anonymous,
  `GIT_TERMINAL_PROMPT=0`) answered, and `GIT_TRACE_CURL` showed "schannel: SSL/TLS connection with
  github.com port 443": without the system file Git for Windows uses Windows' own certificate store,
  so HTTPS to github.com needs no CA bundle setting under the isolation.
- `git check-ref-format --branch visualcommit-live/20261028-153000Z` accepts the name.
- A proxy in `http_proxy`, `https_proxy` or `all_proxy` is removed by the isolation (D112): the
  command assumes a direct connection, as the laptop has.

### 14.8 Builder's tests, the lead's steps, done when

The builder: `dotnet build tests/VisualCommit.LiveHostingTests -c Release`; without the variables
(Git Bash: `env -u VISUALCOMMIT_LIVE_GITHUB_TOKEN -u VISUALCOMMIT_LIVE_GITHUB_REPO dotnet test --project
tests/VisualCommit.LiveHostingTests --filter-class "*GitHubLiveTests"`) both tests fail at once with the
message, none skipped; with them, once, when the lead says the owner has made the token: both pass,
run.txt read, no `visualcommit-live/` branch or open pull request left in the sandbox.

The lead: before the run with the token, the owner's three steps (15, point 4). At the merge:
`VisualCommit.slnx` gains, beside the real-window comment, "VisualCommit.LiveHostingTests is left out
on purpose too: it acts on the owner's GitHub account with a token from the environment and runs only
by its own command (D250)."; in `ci.yml` (D337) the Windows step "Build the real-window pass"
(`ci.yml:67-71`) becomes "Build the opt-in projects", `if: runner.os == 'Windows'`, running `dotnet
build tests/VisualCommit.RealWindowTests --configuration Release` and then `dotnet build
tests/VisualCommit.LiveHostingTests --configuration Release`, its comment saying that both projects
are outside the solution, that neither runs there, and that building them keeps them from going
stale unnoticed; the lead reads that job's step on the merge's run; CLAUDE.md's Commands
row "The opt-in live command against real GitHub (Windows only; the owner's sandbox token, D250,
D260)" with the command, "about two minutes with its build; its findings in
`artifacts/live/github/run.txt`", and in "What to know" the two variables, where they are set, never in
CI or the default run, that CI's Windows job only builds the project (D337), the leftovers removed at its start, and 14.6's rule: in a session where they
are set, never print the whole environment, filter it by name; plan.md's Visual test gate and the 7a
report gain the live run's step and a "Live command" section with run.txt's findings; the recorded
422 body and the `user/repos` finding go to the fake (2.4) where they differ, rerunning
`FakeGitHubTests` and the part checks that show those texts. Tick step 16 after a green run with
run.txt read. At the gate the lead runs it as 13.7 says (D335): once before the gate's pictures, whose
corrections to the fake are committed before the passes; again on the counted round; and alone on a
later commit that changes only this project, the Gate commit staying the passes'. For D335 the lead
also changes CLAUDE.md's "Phase N accepted" row and closing step 6, and plan.md's
Visual test gate step 6 (13.7); the design edits neither file.

Done when the project builds; it is in no solution, CI's Windows job builds it and no CI step runs it
(D337); without the variables both tests
fail with the one message; with them both pass, the draft is created and read back, closed and its
branch deleted; run.txt holds the r5 14 findings and no token, and no note reached the test output
unsearched; `TokenTripwire` passes; every request
with the token went to `api.github.com`; the CLAUDE.md row is with the lead.

## 15. Open points

What is still open, each point with who settles it and when. Everything else the design leaves to
the lead or the owner was settled on 2026-10-29: in the decisions the design names, and the briefs'
sizes, which the lead accepted, in `docs/status.md`.

1. **Facts left to the live run** (14.5): GitHub's exact texts for the version, `type`, unknown-SHA,
   empty-repository and duplicate-create answers (the fake's are from memory, 2.4); whether github.com
   accepts `X-GitHub-Api-Version: 2026-03-10`; how a fine-grained token's `GET /user/repos` and
   `GET /user` answer; whether `s=64` still sizes `v=4` avatar URLs, and whether they answer without
   a redirect to another host (which gives no picture, 6.4; LiveRequestLog's statuses show it). Settled by the live
   command's first run with the token (status step 16, 14.8): the lead corrects the fake (2.4) from
   run.txt then, and again after the gate's first live run, before its pictures (13.7, step 2).
2. **A browser that upgrades `http://127.0.0.1:47395/` to HTTPS**, or shows a first-run page (13.5),
   not tried: settled at D246's two runs of the 7a real-window checks (13.7, step 4), with the owner
   present; if the step fails there for that reason, the owner chooses between changing the browser's
   setting and `OpensTheBrowser` false (D321).
3. **Git facts run on 2.56 only**: the two-level and `.git`-less `http-backend` paths (2.2) and a
   fork's `refs/pull/<n>/head` fetched into `FETCH_HEAD` (10.5). Settled by CI's jobs on 2.43 to 2.56, which the lead reads at
   WP-F's and WP-P's merges; a difference goes back to that package.
4. **The owner's sandbox, token and variables** (14.6): the owner's three steps, before the live
   command's run with the token (status step 16). The lead asks for them when WP-LIVE starts at the
   latest, so that its builder does not wait.

## 16. As built: wave 1

What wave 1's build and reviews (2026-10-29) changed or settled against the sections above, with
the lead's rulings on what the builders handed over. The report and the handoff cite these as
"design 16.N"; a section above that differs from them is read with them.

1. **0.4, the lagged list.** Its `VisualCommit.App.Tests` command also names
   `--filter-class "*GitRepositoryProviderTests"`, so that every lagged run checks the lag's Info
   line (WP1's review, finding 5). Its first run was at WP1's merge (`a4a388e`), with plain
   `dotnet test` and the variable in front: every class passed.
2. **2.3, `FailNext`.** It refuses avatar and page routes as it refuses git's
   (`NotSupportedException`): the router answers them before it takes a fault, so a fault on one
   never applied (WP-G's review, finding 7; the lead's ruling, `8b02805`). `AvatarCacheTests`' moved
   cases use a scripted handler.
3. **2.2 and 2.5, the fake in process and the host's auth.** `ShellDriver.Start` and `StartWithTabs`
   refuse an in-process fake given as `gitHub:`; it goes in
   `hosting: new HostingOptions(fake.Endpoints, fake.CreateHandler)`. In process the scenario's
   clones keep port 47395's URLs, which are not the fake's endpoints (`Local(1)`), so the app
   recognises neither remote and git reaches nothing: a test that needs either serves with `port: 0`
   (WP-F's review, finding 1). A header the host cannot read (an unknown token, another scheme, an
   empty user name, bad base64) gets 401 on every path it does not claim, a missing repository's
   included; claimed routes are answered before auth whatever the header. The data folder's
   tripwire fails on a file it cannot read (held without read sharing) after about 0.4 s instead of
   passing it, so a check that runs it while the app holds a file stops the app first.
   `Phase7aChecks.Support.cs` has no error-dialog assertion yet: the first of parts B and N, or the
   refused token's path, adds a 7a one, without phase 6's helpers for git 2.36's output.
4. **3.7, the vault.** Each key's keeps and forgets reach the store in the order they were called,
   and a store read waits for the key's last one; `ForgetAsync`'s token stops only the caller's
   wait, and the delete still runs. A refused key is logged through `TokenEntryNames.ForLog`
   (`{host}/{account}`, with control, separator and format characters and `\` as `\uXXXX`). The
   unknown value's warning reads "VISUALCOMMIT_KEYCHAIN is neither memory nor unavailable: tokens
   are kept in memory." and never repeats the value (D269 unchanged).
5. **3.9, `SecretServiceStoreTests`' bounds.** Each step is bounded at 30 s, not `Connect` plus 2 s;
   the silent bus also has a lower bound (`Connect` less 50 ms); the probe without an address and
   the cancel case have no time bound (WP-K's review, finding 1). `WindowsCredentialStoreTests.
   Off_Windows_every_call_finds_no_keychain` skips on Windows: four skipped tests in a Windows
   default run, three on macOS and Linux (D270 amended).
6. **3.10, the containers.** CI's bare containers run with `--cap-add=IPC_LOCK` (the lead, `8b02805`):
   Ubuntu's gnome-keyring gives its daemon that file capability when `setcap` is installed, and a
   container without it in its bounding set cannot start the daemon (WP-K's builder; to be seen on
   CI). THIRD-PARTY-NOTICES.md names Tmds.DBus.Protocol among the packages; plan.md's risk 34 is
   ruling 20's.
7. **4.7 and 4.8.** The password kept after a user name is kept under the account git's next prompt
   carries: the Username prompt's account when the typed user is empty, the login, or
   `x-access-token`; none for another user (`AskPassBroker.IsAccountUser` is the one rule for the
   broker and the prompt group). `AccountCredentialTests`' Deny fetch is bounded at 30 s, not 10.
   The `CredentialBindingSelectTests` row follows D278: beside alice's `/o/r`, `/o/r` is dropped and
   `/o/r/sub` kept; beside alice's `/o/r/sub`, both are dropped; `/o/rx` is kept. The process's
   `GIT_CONFIG_COUNT` is tested through an internal constructor that takes the process's variables,
   accepted in place of a child process. The dashboard row's rules read is cancelled by
   `options.Cancellation` only, not by the view model's lifetime: a tab closed during the read is
   caught by the `IsClosed` check after it, and no lifetime token is exposed (the lead's ruling).
   `AppSession.Start` already gives `CreateBroker` the hosting session, `gitEnvironment` and the
   `gitVersion` function; WP-S's merge only swaps in `HostingSession`.
8. **5.2 and 5.4.** `RepositoryWatcher` counts held and delivered events (internal, moved only
   under a lag), so the dispose test can fail; its lag is 2 s. `OperationsHarness.
   WaitForOpeningCleanUpReadAsync()` comes before the quiet mark in the tests whose mark could race
   the opening's backups read (`A_bulk_runs_part_cancelled_before_its_call_queues_nothing`,
   `UndoStepTests`, `GitCallBudgetTests`' harness case); `OpenAsync` itself does not wait (the lead's
   ruling: a test that marks right after opening waits for the read itself).
9. **5.7, the pictures.** `w1e-scan-checklist` shows a temporary folder's random name, and
   `g23i-pull-not-fast-forward` takes one of two forms in 7 pixels of the toolbar's Branch icon
   (x 315 to 319, y 49 to 52, at most 6 of 255) from run to run on the same code, split or not.
   Both vary by nature; every other `w1*`, `w2*`, `w9*`, `g23g` and `g23h` picture is byte-identical.
10. **6.2 and 6.3.** A kept answer holds `Link`, `X-GitHub-SSO` and `X-OAuth-Scopes` beside its ETag
    and body, and a 304 takes them where it lacks them, so a repeated paged list still pages on and
    keeps "more exist". A cancellation that is neither the caller's nor the 30 s limit's is no
    answer (`Network`, "Could not reach GitHub."). A bare 429 (no `Retry-After`, no
    `X-RateLimit-Remaining: 0`) is `RateLimited` at now plus 60 s, not `Malformed`; the fake's form
    and github.com's `http` form accept a user part, a password included, which is never kept and
    never bound (WP-C binds https only). The lead keeps both readings (WP-G's review, finding 5).
11. **4.6, the prompt channel on macOS and Linux** (found by CI's run on `8b02805`, fixed at
    `9d48d0b`). .NET backs every `NamedPipeServerStream` of one name with one listening Unix socket,
    counted per live instance, and unlinks and closes it with the last instance (dotnet/runtime's
    `NamedPipeServerStream.Unix.cs`, `SharedServer.Dispose`); a connection queued on it is then
    dropped. `AskPassBroker.ListenAsync` kept one waiting instance and made the next only after
    `_ = ServeAsync(pipe)` returned, and an answer without UI (4.6's `get` and `erase`) runs to its
    end inline, the instance disposed, so a helper queued meanwhile got no answer and exited 0: on
    macOS a bulk run's lost `erase` gave "Git could not fetch from origin." without "Open
    settings…"; a lost `get` gives git's fallback prompt. On macOS and Linux the broker now holds a
    keeper instance while it listens, which never waits for a connection and goes when listening
    ends, and every instance there passes `MaxConnections + 1`; Windows, where each instance is its
    own kernel object, is unchanged. `AfterAccept` (internal, null in the app) holds the loop between
    an accept and its serve: `AskPassChannelTests` (a `get`, an `erase` and a prompt queued behind a
    credential answer) failed on CI's three Linux jobs and on macOS before the keeper (run
    40910893877, merge `6fc2d28`: the queued client got "Connection reset by peer" on Linux and an
    end without a line on macOS; nothing else failed) and passes with it. Diagnostics: a
    Warning when no instance is left while the broker listens on macOS or Linux, Debug lines for
    `ServeAsync`'s exits, for an `erase` that marks nothing and for the bound-URL counts (`Begin`,
    and `RepositoryOperations` after the URL rules read), none with a token, hash or URL path;
    `CredentialDashboard.Describe` prints a failing credential dashboard test's rows, git calls, the
    server's requests, the stub's counts, the log and git's trace of the helper's runs, every token
    masked. Found by four investigators and a judge; built, reviewed and fixed by agents that did not
    review their own work.
12. **0.7, part A's earlier pictures.** The list of settings pictures that change when Accounts joins
    the screen is short by `s20b-merge-tool-pick` (the screen under the Pick dialog); the report's
    "Changes to expected results" lists it (31 scripted pictures).

## 17. As built: wave 2

What wave 2's builds and reviews changed or settled against the sections above, package by package,
with the lead's rulings. The report and the handoff cite these as "design 17.N".

1. **WP-S, merged at `7bea4fb`** (six commits as 7.14 lists them and the review's fixes, `978ea87`;
   the lead's 7.11 wiring at `04952c8`). The builder's readings, which the lead keeps:
   - 7.5 step 6: "`Session` without a reason" is the vault's `TokenStoreReasons.NoKeychain`, which
     gives `NoteKind.NoKeychain` with no note and a null `SaveFailedReason`; any other reason is
     `SaveFailed` with it as note and as `SaveFailedReason` (the report's part A, ruling 3).
   - 7.5 step 4: `UseForGit` comes from the previous record whoever signs in.
   - 7.5's sign-out and the switch run on the thread pool, never on the caller's thread (the review's
     one major finding). A sign-out honours its token only until it holds the gate; from then on
     neither that token nor the session's lifetime stops it (the vault's forget waits with
     `CancellationToken.None`, bounded by the store's own limits), so neither a dialog's Cancel nor
     the app closing leaves half a sign-out. `SignInAsync`'s second half runs where the provider's
     answer completes: on the thread pool with a real network; a test handler that answers
     synchronously would run the vault's save and the file write on the caller's thread.
   - 7.6: a 401 marks the account refused only while the same key and the same token are signed in;
     a stale call's 401 never marks a newer token.
   - 7.8: a refresh shows `Loading` over a failure but keeps a held list until its answer; a run's
     unexpected exception is a `Network` failure with the exception's message, never a section left
     loading; `Changed` is also raised when `WantsUrlRewrites` changes (`IHostingSession.cs`'s comment
     says so), so a sign-in with the tab open starts the rules read for a remote that only an
     `insteadOf` rule makes GitHub's. `HostingRules.Remotes` (7.8 step 2's recognition) is a fourth
     function beside the three 7.1 names.
   - 7.9: images are fetched only for the e-mails of the set; a failed run applies nothing; a sign-out
     disposes the `AvatarCache` and makes a new one (its index would point at deleted files); an
     avatar run starts only while signed in. The 20 lookups count the e-mails passed into the one
     `ReadAuthorsAsync`, as 7.9 step 3 says, those the commits page then answers included: a few
     lookups may wait for the next app session. The review proposed counting only lookups made; the
     lead keeps 7.9's one call.
   - 7.10: one rules read per refresh: the `Changed` handler starts one only when none is running.
   - `AccountRecord.UseForGit` has a setter, not `init`: the source-generated reader sets a missing
     `init` member to its default, which would turn the switch off.
   - Outside 7.1's file list: `RefreshForms` has seven forms with the optional one, so
     `Phase6SupportTests` (two counts) and `GitCallBudgetTests` count the required forms only; the
     lead accepts the edit.
   - Internal seams for the tests: `HostingSession.Switching` (the switch's write; a `ShellDriver`
     test waits for `AccountChanged` instead), `RefusalCheck`, `HostingCount`; `HostingSessionSetup`
     (in `VisualCommit.Hosting.Tests`) with a `NetworkDown` switch that fails requests before the fake.
   - New log lines: "GitHub: no account on {host}." at a start without a record; at a sign-in
     "GitHub account {login} on {host}: signed in; the token is kept in the keychain." or "… kept for
     this session: {reason}."; Warnings "GitHub account {login} on {host}: GitHub refused the token
     (git | the API, request {id}); it needs signing in again." and "GitHub: {what} failed: {kind},
     {message} (request {id})".
   - For WP-V: `RepositoryHosting.ShowAvatarsFor` works once the hosting has a candidate, after an
     `Update` with a GitHub remote: call it after the refs are applied.
2. **WP-A, merged at `42b6be2`** (8.9's five commits and the review's fixes `476a96d`, `381a066`,
   `ecfa9df`; six minor findings, no blocker or major). The builder's readings, which the lead keeps:
   - 8.3: `SettingsAccounts` sits in a `Panel` that carries `IsVisible`, as General does, since an
     `IsVisible` binding on the `StackPanel` whose `DataContext` is `Accounts` would bind against it.
     The switch resets only when `AccountChanged` brings another account (another key, or none); a
     change of the same account, a refusal included, keeps the user's toggle. Two states the session
     never makes have a text: needs sign-in with no note kind shows the refused text, signed in for
     the session with none the no-keychain text. After the entry or a sign-out returns, the section
     reads the session's `Account` at once (it is set before the task ends; `AccountChanged` comes
     later through the post). A sign-out that throws is logged and read as having no reason.
   - 8.4: the dialog exposes `Opened` (the keychain question at its opening) for the driver; after a
     refusal the box gets the focus with its text selected, after a failed save "Close" does (Enter
     closes). Any exception from `SignInAsync` but the dialog's own cancellation, and a result with
     neither status nor failure, show "Could not reach GitHub." (`AccountTexts.SignInUnreachable`, word
     for word `GitHubClient.NetworkText`, which is internal; D284) and go to the log only.
   - The link is a `Button` with its own theme, `SignInLinkButton` (`ParentLinkButton`'s look without
     its right margin), underlined on pointer-over and focus.
   - Outside 8.1's list, with the lead's leave: `FakeTokenStore.FailNextDelete` and its test (the
     report's ruling 1); `AuthRemoteServer.HostAccess` keeps the user name of a readable Basic header
     whose token it does not know (`(null, userName, false, null)`), so a refused request records
     `GivenUserName` (2.2, 4.4; A9 step 5 asserts `x-access-token`), with its `AuthRemoteServerTests`
     case. The lead moved `AssertErrorDialog` and `ButtonsOf` to `Phase7aChecks.Support.cs` at the
     merge, for parts B and N.
   - The report's A8 step 6 expects a conditional `GET api/v3/user` answered `304` (the report's
     "Changed in this report after its first commit"); a few looks are asserted through their
     resources (`VcPrimaryButton`, not `danger`; the light theme's colours), not by pixel.
   - Found by CI's Windows job on `c25326e` (run 40925952120): part A's empty-tab start did not wait
     for the app's start-up work, so git's detection (`git --version`, `InitializeAsync`) landed after
     A1's quiet mark on the slow runner. `StartEmptyAsync` now waits for `WaitUntilReadyAsync`, as
     phase 6's start helpers do; a later part that starts an empty tab and counts git calls does the
     same.
3. **Part P's checks, the lead's rulings that bind WP-P and WP-V** (the report's open points 23, 29
   and 34). When a failed refresh follows a list of the same repository with the same account, WP-P's
   section keeps the rows it last showed under the failed row (10.3, D303); 7.8 stays as built, the
   hosting reporting the failure without a list, and WP-P keeps the rows itself, with a
   `PullRequestSectionTests` case. D322's two Debug lines come only when a list or the avatars are
   applied, so D322 binds the pictures of a state with a list, the ones the real window repeats; a
   picture of the loading or failed state waits for that state (the view model's
   `HostingState.PullRequests`, the section's message row, quiet watchers). Part P's checks run again at
   WP-V's merge, and WP-V's builder says whether its avatars' line comes in those states.
4. **WP-B, merged at `b361388`** (9.7's four commits, ruling 11's scenario `15c10bc`, part B's code, and
   the review's fixes `ba08068`: one major, three minor). The builder's readings, which the lead keeps:
   - The browser's view model takes a third, optional parameter, `Func<string?>? login`, read when a
     failure is applied, so the refused text names the account (the report's ruling 13); after the
     API's 401 the account needs sign-in but keeps its login.
   - "From GitHub…" is drawn disabled while its browser is open: its command is an `AsyncRelayCommand`,
     which refuses a second run while the first runs.
   - A row's description has its runs of white space and line ends made one space; one that is only
     white space is none. The list's warning leaves out "(request …)" when the failure has no id; a
     result with neither list nor failure shows the unexpected text and logs an Error.
   - 9.3's read before a bound clone runs under the clone's own cancellation (cancelling then gives
     "Clone cancelled."); while it runs the tab is not empty (`RunningClone` is a completed task once
     `IsCloning`), so no opening takes that tab (the review's minor finding).
   - Paths the checks compare with git's (`RepositoryViewModel.Path`, the recent list) use git's own
     spelling of the folder (`rev-parse --show-toplevel`), since macOS's temporary folder is behind the
     `/private/var` link (the review's major finding).
   - Outside 9.1's list: `Scenarios.Phase7a.cs` (`ToolDescription`) and `Phase7aScenarioTests`, by ruling
     11. `HostingTexts.Describe.cs` passes to the lead; its fixed sentences are public consts.
   - The lead at the merge moved the empty tab's start to `Phase7aChecks.Support.cs`
     (`StartEmptyAsync`, waiting for the start-up work, item 2), for parts A and B and those to come;
     part B's sign-in dialog assertion (`AssertSignInDialog`) repeats most of part A's private one and
     may become one shared helper when a third part needs it.
5. **WP-B's merge run and part B's pictures** (the whole default run on `e40ddc8`; status Notes). Part
   B's ten pictures were inspected by four agents against rows B1 to B9 and pass. What the pictures
   cannot show: the URL row of the form behind the browser lies under the card, so `b05a` and `b05b`
   are the same outside it and "From GitHub…" gone after the API's refusal is only `AssertForm`'s; and
   in `b07b` the welcome page's three buttons lie under the sign-in dialog, asserted by the check. One
   observation for phase 8's keyboard-accessibility pass (closing step 20 moves it into plan.md):
   after "Choose" closes the browser, no control of the form has the keyboard focus (`b06a`), so a
   keyboard user must Tab into the parent folder's box; D104 gives the focus back only to a dialog
   below, and the report does not assert where it goes (B6).
6. **Part G's checks, the lead's rulings that bind WP-V** (the report's open points 35 to 44). At
   1100×700 the graph's Author column never shows (phase 1's widths, D49), so the real window's `g01a`
   shows the badges and the details panel's image, and the graph's images are pictured scripted only,
   at 1920×1080 (35). `AvatarsApplied` and its line `{repo}: showing GitHub avatars for {n} authors`
   come with the column shown or hidden, `{n}` the authors whose image the control decoded, with a
   `GraphAvatarTests` case for the hidden column (42); the line is the active tab's, as the window's
   one graph shows it, so B8 step 4 waits for the avatars' requests and `hello`'s `Avatars` instead
   (43); `CaptureForPicture` in `Phase7aChecks.Support.cs` also waits while the active tab's
   `Details.AuthorAvatarFile` is set and `DetailsAuthorAvatar` is not shown (44, outside 11.1's list);
   WP-V's builder says in its handoff that its line is not written when a run applies nothing (39).
   At WP-V's merge, besides 11.6's runs, the lead runs part P's checks and part A's check 9 again
   (item 3) and part B's B6 and B8, whose `AvatarsShown` and avatar waits WP-V codes in
   `Phase7aChecks.A.cs`, `.B.cs` and part P's files.
7. **WP-P, merged at `1958755`** (10.7's five commits, part P's code, and the review's fixes `190c199`
   and `7535264`: one blocker, four major, three minor). The builder's readings, which the lead keeps:
   - The panel's state keeps WP0's name `HostingState`. Message rows that take no click are disabled
     buttons, which keep their look and, through `ToolTip.ShowOnDisabled`, their tooltip (P4 step 6).
   - Ruling 23: the panel keeps the last list it showed (`_keptList`), tagged with the account that
     listed it: `UpdatePullRequests(state, AccountKey?)`, the one line WP-P changed in WP-S's
     `RepositoryViewModel.Hosting.cs`, passes the session's account. A sign-out, a repository without
     a GitHub remote, another repository or another account's failure drops it; a loading state
     without a list shows the kept rows and no loading row. `ForPullRequest` takes the row's own pull
     request, so a kept row's menu is whole while the hosting holds no list.
   - Checkout (10.5): the plan maps a head on a remote to that remote's `refs/heads/<head ref>` and
     compares local names ignoring case; a branch is reused only when its exact name points at the
     head. "Fetch pull request #n" is a network operation and the checkout follows as its own;
     `FETCH_HEAD` is read from the worktree's own git folder, off the UI thread. A deleted fork's head
     label is its ref alone (#8 `idea`); the tooltip leaves out the author when GitHub gave none. The
     palette lists every listed pull request's three items, all 300 when the list is full.
   - Where part P's code checks less than the report says (no expected result changed): the graph
     row's highlight in P9 is asserted through the view model and judged in `p09a` and `p09b`; P2
     step 4's dialog by its title, focus and empty box (part B's `AssertSignInDialog` holds the card);
     P13's "no `git config` that writes" as no `git config` call without `--get` or `--list`; P7's
     bottom rows' order on the view model; P6's "signed in" through `WaitForAccountAsync(SignedIn)`;
     P12's drag as press, two held moves and release. Every part P picture goes through `ShotP`: no
     token's constant on screen before it, `AssertNoClippedText` after it (a cut text passes only
     when it or one of its three nearest ancestors carries the whole text as tooltip).
   - Ruling 31's extra wait is not needed: `a09b` shows the section's sign-in row.
   - Design 15 point 3 (a fork's `refs/pull/<n>/head` into `FETCH_HEAD` on git 2.43 and 2.55) is
     settled by CI's run on the merge; on 2.56 `GitRepositoryPullRequestsTests` and P13 prove it.
8. **Part N's checks, the lead's rulings that bind WP-N and WP-R** (the report's open points 45 to
   61). 12.4 is amended (59): while the create's cancel window is open (12.5 step 1, the push), the
   form's `Cancel` stays enabled and cancels the operation through the queue, the form staying open
   with `Cancelled` on its error line; once the window ends (12.5 step 2, before the POST, D315) it is
   disabled again, and Esc stays ignored while creating. The status bar's Cancel lies under the form's
   backdrop (D69), so without this no control the user can reach would cancel the push. 12.7's
   "creating (all disabled …)" case changes with it. `CreatePullRequestOperationTests` also asserts
   D314's undo entry after a create that pushed and none after one that did not (54), and adds three
   cases on `tool/`: the head's place by `HostingRemoteOrder` (D313), a fork's parent's default branch
   as the default base, and an `owner:branch` base (D318) (61). A remote-branch head's text is the
   ref's name, `origin/topic/no-pr` (48). 12.5 step 4 keeps its own refresh, so two `listed` lines
   follow a create, and 13.4 N step 4 waits for both before its compare (51). At WP-N's merge the
   status-bar pictures are compared with the last full run's, as ruling 19 (60).
9. **The headless harness's double-click, merged at `ccd1ebe`** (CI's run 40965836423 on WP-P's merge
   failed `LeftPanelPullRequestsViewTests`' double-click on pull request #5 on all five jobs; status
   Notes). Avalonia 12.1.3's `MouseDevice` counts a press on from the last one when it lies within the
   double-tap size (4 by 4 pixels) around it and the double-tap time (500 ms, by the window's input
   clock) after it, whatever control either went to, and `Gestures` raises `DoubleTapped` only on an even
   press whose control is the last odd press's. #5's row had taken the clicked sign-in row's place, so
   on a fast machine the double-click's presses were counted 2 and 3. `ShellDriver` now records every
   press in the window (a tunnel handler that sees handled presses too). `DoubleClick` waits, running
   the dispatcher, until the clock stands more than the double-tap time past a press within that size
   of its spot. `DoubleClick(Visual)` reads the control's centre again after that wait, and
   `DoubleClick` throws when its presses are not counted 1 and 2. `HeadlessInputClock.Read` throws
   while the clock is paused, where the wait would never end. Three `ShellDriverTests` pin the wait,
   the rectangle's corner and the point just outside it; the regression test failed before the fix
   with the counts [1, 2, 3]. Built, reviewed by an agent that did not build it (five minor findings:
   three fixed in code, the docs one in `architecture.md` and plan.md's risk 22, the fifth ruled
   here). Of all the tests' double-clicks, only phase 3's check 32 step 4 also waited on the laptop
   (249 ms after `OpenMenu`'s right-click at its spot; its pictures are the same files). Check 9 step 7
   and W8 step 5 come about a second after a press at their spot, so on faster machines they may wait.
   **The lead's ruling on `Click` and `RightClick`**, which have the same trap: they stay as they are.
   Two presses at one spot within 500 ms are a double-click for a user too, so these methods do what
   the window would do. The builder's scan of the tests found no pair of presses that reaches a
   double-tap handler this way. A throw would break the tests whose back-to-back clicks are counted 2
   and 3 on purpose (`ShellDriverPhase3Tests`' modifiers, the graph's ctrl-clicks), and a warning that
   nothing reads would prove nothing. Builders' briefs say so from WP-V on. Also latent: a click in
   the conflict resolver at the spot that opened it, within 500 ms, is swallowed by its double-click
   guard (`ConflictResolverView.axaml.cs:284-286`). No test makes such a click.
10. **WP-V, merged at `8810136`** (11.5's four commits, the rulings' waits `f8c438c`, part G's checks
    `7d85ed6`, and the review's fixes `9b28c54`, `a744bcf` and `77aa2d4`: one major, six minor, two
    ruled here). The builder's readings, which the lead keeps:
    - A tab tells its hosting the authors once, when its loading graph is whole or reaches 2,000 rows,
      not at every `Changed` before (which would spend WP-S's lookups on sets about to change), and
      never the empty set before any: a placeholder graph or a history no GitHub branch reaches tells
      it nothing. The review found the loading graph's path untested (major); `RepositoryAvatarsTests`
      now gives the state before the graph on a pumped UI thread and asserts the one send by
      `OnFollowedGraphChanged` (a mutation that drops the handler fails both cases).
    - `ShowsAvatarFor` is true when the control has applied an image for the address, whether or not
      the Author column shows (11.4's "after decoding"); with the column hidden only the pixels tell
      that none is drawn (`GraphAvatarTests`' hidden-column case). A change back to `None` raises
      `AvatarsApplied(0)` and writes the line with 0 (ruling 42), and a tab made active with other
      avatars raises it again (ruling 43).
    - A row without an image is proved in the pictures by its 16×16 box holding no pixel within ±24
      of octo's or ada's colour and the 4×4 block at its centre not all white (a 6-px white square
      fills it, a glyph's stroke does not: in `g06a` the centre pixel of row 5 is ada's name's "A");
      an image row's block is all white. `g05b` equal to `g05a` is asserted pixel for pixel on the two
      captures (the review found the files the same by SHA-256 too).
    - The 7a project's `*.G.cs` files are no generated code (`.editorconfig`, as phase 6's), so the
      analyzers run on them. B6 runs as `--filter-method "*Check_B3_B6_B7*"`.
    - The builder's confirmations: the avatars' line follows the avatars, not the list, so it comes in
      the loading and failed states once A3 and the commits page answer (17 item 3); and it is not
      written when a run applies nothing (ruling 39; G7 asserts it).
    **The lead's rulings.** Part P's loading and failed pictures (`p03a`, `p11b`, `p04a`, `p04b`) keep
    their state waits (rulings 29 and 34): the avatars' line can come during them, but at 1100×700 the
    Author column is hidden and none of them shows the details panel of an octo or ada commit, so no
    pixel of theirs depends on it. `p12a`, listed among part P's pictures that gain badges, shows its
    open branch menu over the label column, so it does not change; the inspection at the merge
    records it.
11. **Two races in the tests that macOS showed, merged at `33313ef`** (CI run 40982132445 on
    `ccd1ebe`; four investigators, a judge for each, a builder, a reviewer that did not build it, its
    one minor finding fixed; workflow `ww0ovir23`). Neither is a product bug; both fixes are in the
    tests alone, and each race was reproduced on Windows by a probe before its fix (failing with CI's
    message) and closed after it.
    - **P13** (`79e631b`, the report's `5f7d1aa`). A sign-in lists every open tab with a GitHub remote,
      the one in the background included (7.8's triggers, D289), through the session's one client,
      which sends one request at a time (D285). When `hello`'s list went first, `tool`'s list was
      answered after `hello`'s line, once the fake had synced acme/tool's refs, and was recorded after
      step 2's mark. Step 1 now also waits for `tool`'s list line (`WaitForListLineAsync`, split out of
      `WaitForListedAsync`); not for `tool`'s `AvatarsShown`, which only the shown tab writes (11.3).
      The report changes only the wait, and its convention "Waiting for a list" says so for every
      sign-in with more than one GitHub tab open. Rejected: filtering `ApiSince` to `hello`'s routes
      (it loosens step 2), holding the request at the fake (the one client would hold `hello`'s list
      behind it), and opening `tool` later (it changes method 3). WP-V's builder had also seen it
      fail once in three runs on the laptop, and CI's Windows job failed it on `8810136`.
    - **`KeyRouterTests.Every_key_declines_during_a_branch_drag`** (`b3fe91a`). The drag's press
      selects row 4, whose details nothing waited for; during a drag the router takes no key, so the
      keys go to the focused control and can move the focus. With one timing Ctrl+Tab and
      Ctrl+Shift+Tab reached the details' file row, and a declined Mod+Enter on it opened a diff
      whose late focus move fell between the test's read of the focus and a key's delivery. Headless
      input runs the dispatcher's jobs and renders before it delivers, so a focus move queued by an
      asynchronous load lands first. The test now waits for row 4's details and gives the graph the
      focus before every key, asserting no diff opened; `AssertNothingRan` asserts no diff for all
      three `PressEveryKey` tests. Judging the key where it was delivered was rejected: CI's run
      would then have passed while a declined key opened a diff in the middle of the drag.
    - **For phase 8's keyboard pass (closing step 3 moves it into plan.md):** a diff that is still
      loading keeps a pending focus and takes the focus when its text arrives, even when the user
      has moved the focus elsewhere meanwhile (`DiffView.axaml.cs:131-135`, `:146-149`;
      `DiffTextView.cs:209-213`, `:605-617`; an investigator showed it in a run on Windows). It is
      phase 6's behaviour and outside 7a's scope.
12. **G1's check for every platform, merged at `5219eae`** (CI's macOS job on `8810136` and
    `0aa7cdb`; a builder, a reviewer that did not build it, its one major and two minor findings
    fixed; workflow `wzf5zbhxr`). WP-V's review had added a pixel check that read a text's ink edge
    to ±1 px; macOS's glyph rasterisation (CoreText under Skia; DirectWrite on Windows, FreeType on
    Linux) ends the name's ink 1.44 px before its layout edge. The graph now gives its tests the
    places its drawing uses, from one computation the drawing shares (`BadgeInRow` with
    `PlacedBadge`, `AuthorInRow` with `PlacedAuthor`; `LabelCornerRadius`, `LabelIconStroke` and
    `IconGridSize` public), and part G's ten pictures are the same files. The checks compare those
    places with the report's exactly and hold the ink by outline rules (the report's "How part G
    proves its places"). The review found that #1 could move 2 to 3 px unseen (the scan stopped
    before the label's right padding and the "1"'s outline ends 1.3 px inside its box); the fix scans
    the label's middle rows to its edges and holds each text's ink to its glyphs' outlines. The
    builder's audit found no other text-ink read in parts A, B, G and P: parts A, B and P read no
    pixel, and part G's image and colour rules read bitmap pixels or rely on the theme's colours.
13. **WP-N, merged at `93e3410`** (12.8's four commits `cde34f7`, `663366f`, `be3652e`, `3f541d5`; part
    N's checks `3cd2718`, `587ca0e`, `951feb6`, `e808454`, `1774a03`; the review's fixes `942dd2a`,
    `5fec99a`; workflow `wfgi3aatg`: a builder, a second builder that asserted what the first had left
    out, a reviewer that built none of it, and a fixer for its two major and six minor findings). The
    report's "Part N as built" records what changed in the checks.
    - **The builders' readings** (nothing outside the package changed). Ruling 59:
      `ICreatePullRequestActions` gains `CanCancelCreate`, `CanCancelCreateChanged` and `CancelCreate()`,
      and the form's `CancelButton` binds its own `CancelFormCommand`: it closes the form when idle, and
      while creating it is enabled only inside the push's cancel window, cancelling through
      `RunOptions.Cancellation`; a create without a push offers no Cancel. `PullRequestEnd` gains `Branch`,
      GitHub's name of the branch (`SameBranch`, the POST's base). A create that pushes starts with the
      push's running text, so the status bar never shows `Creating pull request…` before the push. A
      drop's given base skips `StartAsync`, so Into never reads `Reading GitHub…` (N5). `SameBranch`
      stays on the error line while the base is the head's own branch, and Create's tooltip is the same
      text (ruling 49 left it open). The card measures 386 high (12.4's "about 415").
    - **Log lines (ruling 53):** Info `{repo}: Create pull request for {head}: into {owner/name}
      {branch}, draft yes|no, push to {target} first` (or `no push`) and `… created #{n} in
      {owner/name}.`; Warning `{repo}: Create pull request for {head}: the request to create it failed:
      {Kind}, {Message} (request {id})`, worded so that it stays true when D316's read then finds the
      pull request; three Info lines for a create not started (no GitHub remote, no branch GitHub can
      hold, a base that is no branch on GitHub). Neither the title nor the description is logged.
    - **The review's major findings, fixed.** (1) After a failed create the head's note was the one
      computed before the push: a create that pushed `local-work` and then met a 403 still said `Not on
      GitHub yet: …`. `CreateAsync` now computes the place again after every outcome but a success (the
      queue has applied the operation's refs refresh by then), so 12.4's "the head note read again"
      holds after a failure and after a cancel, and the next Create does not push again. (2) Three 12.6
      rows had no test. `CreatePullRequestOperationTests` (13 cases) now reaches a rejected push (the
      bare repository's branch moved to a commit that is no ancestor, by `commit-tree` and `update-ref`)
      and a declined one (a `pre-receive` hook that exits 1) through real git state, and `GitHubSaid`
      through "already exists" not listed. Those tests showed that "git's first line" was `Enumerating
      objects: 4, done.`: the pack lines have no percentage, so the progress filter keeps them. It is now
      the first line of git's cleaned output after the pack lines and `To {url}` (`PushReportLine`): a
      hook's message as `remote: …`, a silent hook's `! [remote rejected] … (pre-receive hook declined)`.
    - **The minor findings, fixed:** N8 step 6 clicks the Pick's row (`PickAsync`); "one operation" is
      counted in N4 and N9; `AssertNewFormAsync` checks "the form as N1's" (focus and caret, the
      description, Draft, the buttons) in N1, N2, N5, N6 and N10; N4 step 1 asserts #6's head and base
      and the section's rows; N1 asserts the sign-in again after the screen closes; N9 waits for
      `listed 5`; Ctrl+A is `app.CommandModifier`'s; the fork case's undo entry is `Assert.Single`; the
      `owner:branch` case creates from a local `idea` into `acme:main` (from `octo:main` GitHub would
      answer "No commits between"); the SSO text names the fake's organisation.
    - **Tests' own facts.** 12.7's `topic` case is named `next`: git cannot hold `refs/heads/topic`
      beside `topic/no-pr` (a directory and a file of one name). The fork case asserts first that the
      fake refuses octo's fine-grained token across owners (`ForkTokenRefused`), then signs in with
      `OctoClassicToken`.
    - **The lead's rulings.** The Pick's long item in `n08b` shows no detail: phase 6's row (its design
      7.7) cuts the detail, not the text, so the expected result changes and the check keeps the
      detail of all five items through the dialog. The "any other 422" arm of the create's `Describe`
      stays untested: the fake answers the form no 422 beyond the four 12.6 names, a new fault kind
      would change WP-F's fake for one line of a `switch`, and `GitHubSaid`'s words are tested.
