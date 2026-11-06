# Phase 7b design — Azure DevOps and GitLab

This is phase 7b's design: what the phase builds, how it is cut into work packages, the contracts the
packages share, and one brief per package for its builder. Once the phase is built it is kept as
history, as phases 3 to 7a's designs are. Its decisions are recorded only in `docs/decisions.md`
(D243): the design names them by number (D357 to D378) and does not repeat them. The phase's report,
handoff and status cite its sections as "design N.N". Binding inputs: `docs/plan.md`'s "Phase 7b" and
"Workflow" sections and its risks; D344 to D356 and the decisions they leave standing; the 7a handoff's
"For the next phase", "Known issues" and "Things that cost time"; the lead's 31 rulings of 2026-10-31
("ruling n"). The research is outside the repository, in
`D:/Projects/Personal/phase7b-work/research/`: notes r1 to r8 ("r5 4.2") and their gap rounds ("g1
G0.3": g1 at the end of r5, g2 of r6, g3 of r7, g4 of r4, g4's D4 at the end of r1). A fact keeps its
mark, MEASURED, READ or ASSUMED, wherever a fake or a test copies it. A fake that copies an ASSUMED
answer names it for the live command to confirm (D342); an answer the live command does not reach,
outside the requests D371 and D374 admit, needing a token D364 does not make (a read-only write, a selected-projects token's list) or left so by D375 (an Azure DevOps fork's pull-request
shapes, a legacy GitLab token's sign-in), keeps its READ or ASSUMED mark and is named as kept. Line
numbers are those of `5057892` (7a's code as merged); 7a's design is cited "7a design N.N".

A builder reads section 0, section 1 and its own section, and nothing else. Nothing a package writes
holds a token, or an e-mail address, name, GUID or organization of the owner's or of any real person
or organization in the research's samples and captures (section 1; D346, D352): examples use
`<organization>` or the fakes' made-up names, and a copy of a captured answer keeps its member names
and shape with made-up values, the services' published constants aside (section 1).

## Contents

- 0 Scope, packages and parts
- 1 Shared contracts (WP0's brief)
- 2 WP-F: the fakes' shared core and the harness
- 3 WP-FA: the fake of Azure DevOps
- 4 WP-FL: the fake of GitLab
- 5 WP-H: the shared HTTP core
- 6 WP-AZ: Azure DevOps' client and provider
- 7 WP-GL: GitLab's client and provider
- 8 WP-C: credentials, push guard and prompts on the new hosts
- 9 WP-S: the session for three providers
- 10 WP-A: accounts and sign-in (part A)
- 11 WP-B: browse and clone (part B)
- 12 WP-P: requests in the left panel and the graph (part P)
- 13 WP-N: a new pull or merge request (part N)
- 14 WP-R: the real-window pass
- 15 WP-LIVE: the live command for Azure DevOps and GitLab
- 16 Open points

## 0. Scope, packages and parts

### 0.1 Scope against the Delivers list

| Delivers item (`plan.md`) | Where | Proven by |
|---|---|---|
| Azure DevOps and gitlab.com on 7a's provider model (D347, D348) | WP0, WP-AZ, WP-GL, WP-S | The recognition classes (6.8, 7.6), `HostingSessionProvidersTests` and `RepositoryHostingProvidersTests` (9.10); parts A to N |
| Sign-in with a personal access token (D345); tokens in the keychain (R3) | WP-AZ, WP-GL (the check), WP-S, WP-A | Part A; `SignInDialogProvidersTests` (10.8); `HostingSessionProvidersTests` (9.10) and `TokenEntryNamesTests`' cases for the new providers' entry names (1.10); the live `Check_2` and `Check_3` |
| The account's token on git calls to the remotes it covers (D350) | WP-C, WP-S | `CoverageGitMatchTests`, `CredentialBindingSelectTests` and `PushGuardProvidersTests` (8.7); `HostingSessionCredentialTests` (9.10); part B's clones; the live `Check_2` |
| Browse and clone | WP-AZ, WP-GL (A2), WP-B | Part B; `CloneFromAccountTests` (11.6); the live `Check_2` to `Check_4` |
| The request list (D351), its menu, checkout, the badge, open in browser | WP-AZ, WP-GL (A4), WP-P | Part P; 12.7's classes; `RepositoryHostingProvidersTests`' `<organization>.visualstudio.com` remote listed in process (9.10); the live `Check_2` and `Check_4` |
| Create from the branch menus and the drop menu | WP-AZ, WP-GL (A5), WP-N | Part N; 13.7's classes; the live `Check_2` and `Check_3` |
| Avatars stay GitHub's alone (D349) | WP-S | `RepositoryAvatarsTests` (9.10); 7a's part G pictures unchanged |
| 7a's screens name the provider they act on (D351) | WP0, WP-A, WP-B, WP-P, WP-N, WP-C | Parts A to N; `HostingTextsProviderTests` (1.10); 7a's pictures as 0.7 says |
| A local fake of each provider's API (D346) | WP-F, WP-FA, WP-FL | `FakeAzureDevOpsTests`, `FakeGitLabTests` and their git classes (3.6, 4.6); `Phase7bSupportTests` (2.8) |
| The live command for Azure DevOps and GitLab (D346, D352) | WP-LIVE | `LiveSafetyTests` and `LiveScreenTests` offline; its runs with the owner's tokens (15.9) |
| The 7a handoff's Known issue: `scripts/heavy-run.sh`'s header comment | L1 | The file |

Plan.md's "Done when" is judged in three places. Against the fakes: parts A, B, P and N and the five
`ShellDriver` classes of 0.4's lagged list, on the laptop and in CI's five jobs. Against the services:
the live command's `Check_2` (stage 1) and `Check_3` (stage 2) per provider, on the Windows machine
with the owner's tokens, and again at the gate (D335). The visual gate: WP-R's five launches and the
inspection, on the Windows machine.

The owner's decisions D344 to D356 and their answers of 2026-11-01 (the acceptance of D367, amending
D252 and D349, and D373 to D376) bind and are not restated. The lead's rulings are carried by D357
(the session), D358 (records and HTTP), D359 (credentials), D360 (Azure DevOps' sign-in), D361
(checkout), D362 (texts), D363 (harness), D364 (the live command) and D365 (one phase), the design
review's by D377 (CI's limits, 0.6) and D378 (another provider's token form refused at sign-in, 10.4),
and the packages' own choices by the decisions their sections cite.

Out of scope: Azure DevOps Server and self-managed GitLab (D347); several accounts of one provider
(D348); the new providers' avatars (D349), and GitHub avatars for a repository whose listed remote is
another provider's but which also has a GitHub remote (D367, amending D252 and D349); a browser sign-in or an
app registration (D345); a request while no account of its provider is signed in, an anonymous one
included (D257, D350, D360); Azure DevOps' global tokens ("All accessible organizations", which stop
working on 2026-12-01, r5 0.6); binding SSH remotes (recognised, never bound, as 7a's); checking out an
Azure DevOps fork's pull request whose fork is no remote here (D361); a live proof of an Azure DevOps
fork's pull request or of a legacy GitLab token (D375); interpreting GitLab's quick actions (sent as
typed, ruling 20); an answer to plan risk 39, which stays the owner's (16.4).

### 0.2 Design statements that shape the whole phase

The decisions are cited, not restated (D243); each line names a mechanism every package builds on
and the sections that build it.

- **Provider model** (D357, D358, D366, D367; rulings 1 to 6; 1.4, 1.5, 9): one `HostingSession`, a slot
  per provider; Azure DevOps' key holds the organization, which `AccountKey` lower-cases
  (`HostingAccount.Login` keeps the server's spelling), GitLab's the username; the listed remote is the
  first recognised one of any provider; `OtherOrganization` sends nothing (D356, D376); avatars stay
  GitHub's while the listed remote is GitHub's.
- **HTTP** (D358, D368; ruling 4; 1.4, 5): one core with a profile per provider, which strips a leading
  BOM and screens e-mail addresses out of every failure.
- **Credentials** (D359, D369; rulings 8 to 15; 1.2, 8): one binding per remote URL, the new providers'
  only in g3 G3.4's form, with the `.git` and credential twins (8.2 steps 4, 4b); D278's drop compares
  both paths in git's credential view and fails closed; `username=x-access-token` for all three.
- **Requests** (D360, D361; rulings 16 to 20; 5, 6, 7, 12, 13): sign-in by connectionData and
  `GET /user`; GitLab's checkout by `refs/merge-requests/<iid>/head`, Azure DevOps' through a remote's
  branch alone; a refusal reaches the App as a `PullRequestRefusal`, never a service's sentence (today
  GitHub's 422 is read, `RepositoryOperations.CreatePullRequest.cs:374`, `:419`, `:421`, `:423`).
- **Texts and entries** (D362; rulings 7, 21 to 24; 1.2, 1.7): GitHub's texts word for word, the
  others' from `HostingProviders` and `HostingTexts`; the new providers off in both harnesses unless a
  check gives a fake (0.7).
- **Harness** (D363, D370; rulings 25 to 29; 2 to 4): one fakes' core out of `FakeGitHub`, host mode by
  URL path, no fake of `<organization>.visualstudio.com`, the tripwire at three base64 alignments.
- **Live command** (D364, D371 to D375; ruling 30; 15): stage 1 runs once WP-S has merged, so the fakes
  are corrected before the parts' checks copy them (0.4).
- **One phase** (D365; ruling 31): the design stage about 20 to 23 points of the week, the build and
  gate 45 to 55; the design before the reset of 2026-11-05 (D344), the build and gate after.

### 0.3 How 7b is named

`7b` wherever the docs write it (the 7a handoff's "Naming", 7a design 0.3 as the pattern):
- Project and namespace `VisualCommit.VisualTests.Phase7b`, classes `Phase7bChecks` and
  `Phase7bChecks<Part>`, `Phase7bExpectations.Phase = "7b"` (a `string`, which 7a's string overloads
  serve unchanged).
- Folders `artifacts/visual/phase-7b/{scripted,real-window,timings}`; report
  `docs/test-reports/phase-7b.md`; list `docs/test-reports/phase-7b-pictures.sha256`.
- `scripts/time-scripted-check.sh`: a `7b)` case running the 7b project before `7a)`; its default
  becomes `7b` in the commit that creates the project, not before.
- Real window: `Phase7bRealWindowPass.cs` with `.Accounts.cs`, `.AzureDevOps.cs`, `.GitLab.cs`, and
  `RealApp.Phase7b.cs`; the csproj links `..\VisualCommit.VisualTests.Phase7b*\Phase7bExpectations*.cs`
  as `Phase7b\%(Filename)%(Extension)`. The 7b project's tests of the expectations are
  `Phase7bExpectationTests.<part>.cs` and `Phase7bSupportTests.cs`, which the link's pattern does not
  match; a `Phase7bExpectations` file uses nothing of the 7b project (no `RequestTestData`, no
  `Phase7bChecks`).
- Part letters avoid C, O, Q, R and T (D230): **A** accounts and sign-in, **B** browse and clone,
  **P** requests in the left panel and the graph, **N** a new request. Picture names match
  `^[abpn]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$` and name the provider in their words
  (`a03a-azure-dialog`, `p02b-gitlab-rows`).
- The live command's folders `artifacts/live/azure-devops/` and `artifacts/live/gitlab/` beside 7a's
  `github/` (D323).

### 0.4 Work packages

Built in git worktrees on branches `wip/p7b-<name>` made from `phase/7b-hosting-azure-gitlab` (never
from `master`: the 7a handoff, "Worktrees start from `master`"), merged back with `--no-ff`; at most
three built at once (D245). One owner per file and per test class; a file passes between packages only
at a merge, in the order below. Builders run the test classes they touch with plain `dotnet test`;
whole-project and full runs are the lead's, through `scripts/heavy-run.sh`.

- **L1, the lead's first code commit.** `scripts/heavy-run.sh`'s header comment (its line 4 says about
  55 minutes) carries no measured time any more: "the default run's length on the laptop is in
  CLAUDE.md's What to know", as the real-window csproj's comment says for its pass. And
  `.github/workflows/ci.yml`'s matrix limits (D377): Test step 55 to 70 minutes, job 65 to 80, with a
  sentence for 7b in the comment above the Test step (`ci.yml:59-64`). In the same commit the lead
  updates the docs that state the limits: CLAUDE.md's "What to know" (55 and 65 to 70 and 80, 44 to
  56, and D377's rule: every Test step, a container's past 11 min 12 s of its 14, no raise after the
  counted round's commit), plan risk 8 and status.md's CI section.
- **WP0, the lead's contracts and stubs.** Section 1's list (1.10), in the main checkout, with no
  change of behaviour or pictures. The 14 packages are sections 2 to 15; WP-LF (after WP-S's merge and
  stage 1, before wave 4), a fix package like the review's fixes, has no section and is not counted
  among them.

**Waves.**
1. L1, then WP0 (lead, main checkout).
2. Wave 1: WP-F, WP-H and WP-C, side by side (no file in common).
3. Wave 2: WP-FA and WP-FL after WP-F's merge; WP-AZ after WP-FA's, WP-H's and WP-C's (its recognition
   calls WP-C's `CredentialBinding.InPlainForm`, which WP0 leaves throwing `NotSupportedException`,
   1.2), WP-GL after WP-FL's and WP-H's (each client is tested against its fake in process); at most
   three at once.
4. Wave 3: WP-S after WP-AZ, WP-GL and WP-C; from its merge the desktop app has the new providers. The
   owner's steps of 15.8 are done before that merge (16.4). Then WP-LIVE's first stage, run by the lead
   with the owner's tokens (status step 16); what it finds goes into the fakes by WP-LF before part A's
   checks are written (D342).

   **WP-LF, the fix package** (branch `wip/p7b-live-fix`, the lead or one fixer): takes stage 1's
   run.txt files, D373's and D374's probes' answers among them; may change `FakeAzureDevOps*.cs`,
   `FakeGitLab*.cs`, `src/VisualCommit.Hosting/AzureDevOps/*.cs` and `GitLab/*.cs`, and the test classes
   it runs where they pin a corrected answer, all free after their packages' merges; marks each
   corrected answer MEASURED with its date in its doc comment; runs `FakeAzureDevOpsTests`,
   `FakeAzureDevOpsGitTests`, `FakeGitLabTests`, `FakeGitLabGitTests`, the clients' and providers'
   classes (6.8, 7.6), 9.10's classes over the fakes and the lagged list below; merges before part A's
   checks are written, its CI run read as at any merge (the wave's whole default run stays WP-S's, 9.11).
5. Wave 4, one after another, each after its part's checks are committed (D244): WP-A; WP-B; WP-P
   (`AppCommands.Palette.cs` passes from WP-B); WP-N (part N clicks WP-P's menu items, and its
   pictures after a create show WP-P's badge). Part B takes no picture of an opened repository of a new
   provider, which WP-P's merge would change; part A's are of the settings screen, but for the Azure
   DevOps dialog prefilled from mobile-app's tab, whose region behind is WP-P's (12.9 runs it again).
6. Wave 5: WP-LIVE's second stage (the app-driven check per provider) after WP-N; then the review
   before the gate (status step 17) and its fixes; WP-R, built on the Windows machine after every part,
   merges after them.

Shims (ruling 1): 7a's GitHub-named members of `IHostingSession` (1.5) stay while a caller uses them;
WP-A, WP-B and WP-P move their callers, and WP-N (to which `IHostingSession.cs` passes from WP-S)
removes what is left; its "done when" says none is. 7a's tests keep their GitHub forms through WP-S's
`HostingSessionGitHubForms` (1.9).

Files that pass at a merge: the skeletons `FakeAzureDevOps.cs` and `FakeGitLab.cs` and the seeding
stubs `Scenarios.Phase7b.AzureDevOps.cs` and `.GitLab.cs` (WP-F to WP-FA and WP-FL);
`IHostingSession.cs` (WP-S to WP-N); `RepositoryViewModel.Hosting.cs` (WP-S to WP-P);
`AppCommands.Palette.cs` (WP-B to WP-P); `HostingTexts.Describe.cs` (WP-B to the lead);
`RepositoryOperations.Run.cs` (WP-C to the lead).

**The lagged list** (D262). At WP-F's merge and at each later merge whose section names it, in Git
Bash, one at a time, with `VISUALCOMMIT_WATCHER_LAG_MS=1500` exported: 7a's list as 7a design 0.4 gives
it (with 7a design 16.1's `GitRepositoryProviderTests`), then 7b's walk-through once it has checks and the classes that start the app
through `ShellDriver` with real git, each once it exists, in one command of one filter kind (xunit v3
ANDs kinds):

```
bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests.Phase7b
bash scripts/heavy-run.sh --project tests/VisualCommit.Hosting.Tests --filter-class "*RepositoryHostingProvidersWiringTests" --filter-class "*SignInEntryProvidersTests" --filter-class "*CloneFromAccountTests" --filter-class "*RequestOperationsProvidersTests" --filter-class "*CreateRequestOperationProvidersTests"
```

A failure under the lag is the fault class R8 imitates: it gets a fix, never a looser test.

At the last merge of each wave the lead runs the whole default run through `bash scripts/heavy-run.sh`
with no arguments (D245), after the lagged list when that merge's section names one (in waves 1 and 2
only WP-F's does): wave 1 at whichever of WP-F, WP-H and WP-C merges last (this run also measures
16.2's order of twelve projects at four lanes and App.Tests' own time, 0.6); wave 2 at the last of
WP-FA, WP-FL, WP-AZ and WP-GL; wave 3 at WP-S, whose merge closes it (WP-LF, merged after it, runs only
the classes and the lagged list its paragraph names); wave 4 at WP-N (13.9). Each run
records the projects' ends and CI's Test steps against four fifths of their limits (0.6).

**Progress steps 4 to 24** (`docs/status.md`):

| Step | Packages |
|---|---|
| 4 First package | L1, WP0 |
| 5 Harness | WP-F, WP-FA, WP-FL; the lead's CLAUDE.md rows |
| 6 Provider model for three providers | WP0, WP-AZ and WP-GL (recognition), WP-S |
| 7 Azure DevOps' API | WP-H, WP-AZ |
| 8 GitLab's API | WP-H, WP-GL |
| 9 The token on git, push guard, prompt group | WP-C, WP-S |
| 10 Sign-in dialog and the Accounts section | WP-A |
| 11 Browse and clone | WP-B |
| 12 Section, menu, checkout, badge, open in browser | WP-P |
| 13 Create | WP-N (WP-P's menu items) |
| 14 7a's screens name the provider (D351) | WP-A, WP-B, WP-P, WP-N for their surfaces; WP-C for the credential texts |
| 15 Each part's checks written | The writers, before WP-A, WP-B, WP-P and WP-N (no package) |
| 16 The sandboxes and the live command | The owner's steps (15.8); WP-LIVE's two stages; WP-LF |
| 17 The review before the gate | The lead's, no package: after WP-N's merge and WP-LIVE's stage 2, before WP-R's merge and D246's two runs; its fixers by area, each fix with its tests |
| 18 The gate | WP-R and the lead (7a design 13.7; 14.5) |
| 19 to 24 Closing | The lead's closing steps (CLAUDE.md), no package |

### 0.5 The visual checks' parts

Written per part just before its package, by one writer and one reviewer (D244), each check marked
for the real-window pass or not. One project, `VisualCommit.VisualTests.Phase7b`, about 22 to 27
methods (under D230's 40). A check method serves one provider, unless it shows the providers side by
side (part A's blocks, part B's palette), and stays under about 40 s alone, timed with
`bash scripts/time-scripted-check.sh "<method>" 7b` before its part's merge. Theme dark unless a check
names light; 1100×700 unless it names 1920×1080. Every part uses WP-F's scenario "hosting7b" (2.6):
an Azure DevOps repository, one of another organization (D356), a GitLab repository under a nested
group whose `upstream` is GitHub's (ruling 5), and GitHub's fake for three accounts; made-up names
only.

| Part | Package | What its checks cover (the section's list) | Real window repeats |
|---|---|---|---|
| A Accounts and sign-in | WP-A | The three blocks in D353's order, signed out to three signed in (scrolled), an off one hidden; both new dialogs in each state, Azure DevOps' refusals among them; the organization bold over the user; sign-out and switches per provider; the intro (10.7) | Both dialogs with the organization and a masked token; three accounts |
| B Browse and clone | WP-B | D354's button and switch; each provider's browser, long names cut; its failure; clones through the app's helper; the palette's three commands (11.7) | Each provider's list; a clone of `web-app` and of `mobile-app` in the real process (11.7) |
| P Requests | WP-P | "Merge requests" and "Pull requests" in each state, D356's row, ruling 5's listed remote; menus; checkouts with git's state; copy link and open in browser; the badge and branch rows (12.8) | The section and its menu per provider; the badge |
| N New request | WP-N | The menus' items; each provider's form, GitLab's Draft and note; the push first; refusals in the App's words; the request listed and the offer; long names cut (13.8) | Each provider's form; the status after creating |

Every real-window step keeps 7a design 0.5's rules: its picture's name and asserted texts are
constants in `Phase7bExpectations.<part>.cs`; its state is built only by code both projects compile
(part P's R methods, part N's checks and WP-R's two "Requests and create" launches share one prepared state,
WP-F's `Phase7bExpectations.PrepareRequestsAsync(copy, provider)` in `Phase7bExpectations.Common.cs`,
2.7, 12.8, 13.8, 14.2); it is taken signed in with the scenario's tokens. Part P's and N's pictures
wait for D322's first Debug line (the new providers write no avatars line, D349). No 7b step opens
the browser (ruling 29). Only the real window, CI or the live command prove the real process's start with two
more variables, the helper run by Git for Windows' `sh` for the new hosts from a folder with a space,
the native window, and the services' answers and their acceptance of `x-access-token`.

### 0.6 Cost in time

- **Default run** (1 h 12 min 18 s at 7a's gate). The 7b project's 150 to 250 tests take 3 to 5 minutes
  alone, 10 to 18 beside the others; `VisualCommit.Hosting.Tests` gains 5 to 9 minutes, `Git.Tests`
  under one. `VisualCommit.App.Tests` (59 min 31 s of its own) ends the run, so no hosting view-model
  test goes there; it still gains WP-C's `PushGuardProvidersTests`, four cases, each with a push
  (8.7), and the extended `RefusedTokenDisplayTests`, `PromptGroupAccountTests` and
  `AppShortcutsTests`, whose own time adds directly to the run's end; wave 1's full run measures it
  (0.4). **+6 to +12 minutes, about 78 to 85**; up to +15 if the new project takes a lane before App.Tests starts (r4 5.2). At WP-N's
  full run the lead takes xUnit's timing report (`--report-xunit`, CLAUDE.md's Commands) for the 7b
  project and Hosting.Tests, and splits by D237's rule any test of 25 s or more alone before the gate.
  CLAUDE.md takes the gate's times; no script or project file carries one.
- **CI.** During 7a the Windows Test step reached 35 min 21 s and macOS's 37 min 57 s; in phase 6
  macOS's reached 43 min 12 s once. r4 5.2's +15 to 25 % brings macOS's worst to 43 min 39 s to 47 min
  26 s, past D232's 44 minutes from +16 %, and Windows' to 40 min 39 s to 44 min 11 s. A raise first
  triggered by the gate commit's own run would be a non-docs change after the Gate commit, which
  CLAUDE.md allows only with both passes run again, so L1 raises the limits first (D377): in
  `.github/workflows/ci.yml` the build-and-test matrix's Test step goes from 55 to 70 minutes
  (`ci.yml:66`; Windows, macOS and Linux share it) and its job from 65 to 80 (`:32`); the containers
  keep 14 and 25. The lead reads each merge's run against four fifths of each limit (56 of 70; 11 min
  12 s of the containers' 14) and raises by D377's rule, which covers the containers' limits too (kept since D134), but only at a merge
  before the counted round. No CI limit changes after the counted round's commit; if that commit's run
  passes four fifths, the handoff records it under Known issues and "For the next phase", and the next
  phase's first code commit raises the limit.
- **Real-window pass** (31 min 6 s for 72 checks; 7a's five launches took about 41 s each, its part
  +3 min 27 s for 9 captures, 5 sign-ins, 1 clone and 1 create, r4 5.1): 7b's five (ruling 29) hold 13
  captures, 7 typed sign-ins, 2 clones and 2 creates, about 1.4 to 1.6 times 7a's part, so **+4.5 to 6
  minutes, about 36 to 37**, and D246's runs of the 7b class 4 to 6 minutes each.
- **Pictures**: about 55 to 70 scripted (r4 5.2) and 26 real-window files; the gate's list about 1,520
  files in about 11 batches of at most 150 (D247); 0.7's references on `master` about an hour of the
  laptop (phases 0 to 7a's eight walk-throughs).
- **Live command**: about a minute per provider with its build; twice per gate (D335). GitHub's token
  expires on 2026-11-29 (risk 42; 15.8, 16.4).

### 0.7 Earlier pictures

- **Change, by D353:** the intro becomes one sentence for the three providers; with the new providers
  off in 7a's walk-through only GitHub's block shows. 7a's 23 pictures of the section and the dialogs
  over it (`a01a` to `a11a`, r4 6.2) and the real-window `a01a`, `a02b` and `a05a` change in the intro's
  region; WP-A records them under the 7b report's "Changes to expected results" (10.6).
- **Must not change:** every other picture of phases 0 to 7a (GitHub's words and `#n`, "From GitHub…",
  the palette, the placeholder, the menus, the graph). Cut repository texts (ruling 3) that fit draw
  pixel for pixel as today: WP-B and WP-N add only trimming and a tooltip.
- **How the gate compares:** references made on `master` before any 7b real-window run
  (`D:/Projects/Personal/gate7b-reference/`: phases 0 to 7a's walk-throughs with git 2.56 on the
  laptop, and 7a's gate's real-window files), compared by window region with
  `scripts/compare-pictures.py` (plan risk 32). Expected: the intro's region, `b06a`'s port when the
  fake fell back, the pictures that vary by nature (31 at 7a's gate), nothing else; any other
  difference is a finding. Every picture is inspected, in batches of at most about 150 (D247).

## 1. Shared contracts (WP0's brief)

Every type, seam, variable, file and port that more than one package uses. WP0 writes what 1.10 lists
before wave 1, with no change of behaviour or pictures; each file's owner after WP0 is named. A
package may add members to a file it owns, but never to an interface another package implements
(`IHostingProvider`, `IHostingSession` and `IRepositoryHosting` keep 1.4's and 1.5's members), and
never changes a signature below without the lead.

Rules for every package: 7a design 1's (no token in an argument, URL, config value, environment
variable, log, message, picture, automation value or file; `ToString()` hides it; no
`HttpRequestMessage` or `HttpResponseMessage` logged; 30 s and the caller's cancellation on every
network call; the UI thread never waits on the network, a keychain or a file, Q2). New: no e-mail
address, name, GUID or organization of the owner's, or of any real person or organization in the
research's samples and captures (g1 G3.2's samples, the research folder's `scratch-*` captures), in a
test, a fake, a picture or a doc: a copy keeps member names, order and shapes, with made-up values
(addresses at `example.com`, made-up GUIDs), while the services' published constants stay as they are
(the Anonymous identity's `aaaaaaaa-…`, `AzureDevOpsApi.CoreAreaId` and the other resource-area ids;
1.4, 6.3, 15.4, 15.9); no offset-less Azure DevOps date read as
`DateTimeOffset` (g4 D4 §3: a `JsonException` east of UTC only); a JSON member `required` only where
every answer has it (g4 D4 §4: one empty repository without `defaultBranch` would make the whole list
`Malformed`).

### 1.1 Projects

| Project | Made by | References | In `VisualCommit.slnx` |
|---|---|---|---|
| `src/VisualCommit.Hosting`: namespaces `.Http` (WP-H), `.AzureDevOps` (WP-AZ), `.GitLab` (WP-GL) | 7a | unchanged | yes |
| `tests/VisualCommit.Hosting.Tests`: its fakes on port 0 or in process, since it runs beside both walk-throughs (D265) | 7a | unchanged | yes |
| `tests/VisualCommit.VisualTests.Phase7b` (csproj and `AssemblyInfo.cs` as `.Phase7a`'s, the same two links) | WP-F | App, Core, Git, Hosting, Testing | yes (WP-F) |
| `tests/VisualCommit.LiveHostingTests` | 7a | unchanged | no; CI's Windows job builds it (D337) |

No package reference is added.

### 1.2 Core

**`src/VisualCommit.Core/Hosting/AccountKey.cs`** (WP0; then WP-S):

```csharp
public enum HostingProvider { GitHub, AzureDevOps, GitLab }   // appended: 7a's value keeps its ordinal
public sealed record AccountKey(HostingProvider Provider, string Host, string Login)   // unchanged otherwise
{
    public string ProviderId => HostingProviders.Id(Provider);
    public string Id => $"{ProviderId}:{Host}:{Login}";   // "gitlab:gitlab.com:ada", "azure-devops:dev.azure.com:<organization>"
}
```

**`src/VisualCommit.Core/Hosting/HostingProviders.cs`** (WP0 whole; the lead afterwards), the one place
for a provider's name in Core and Hosting (ruling 21; the App's words are in `HostingTexts`, 1.7):

```csharp
public static class HostingProviders
{
    public static IReadOnlyList<HostingProvider> InAccountsOrder { get; }   // GitHub, GitLab, AzureDevOps (D353)
    public static string DisplayName(HostingProvider provider);             // "GitHub", "GitLab", "Azure DevOps"
    public static string Possessive(HostingProvider provider);              // "GitHub's", "GitLab's", "Azure DevOps'"
    public static string Id(HostingProvider provider);                      // "github", "azure-devops", "gitlab"
    public static HostingProvider? FromId(string? id);                      // without case; null for another
    public static HostingProvider? TokenFormOf(string token);               // the provider whose token form it has (D378)
}
```

`TokenFormOf` reads only a prefix and the length: GitHub for a token that starts (ordinal, case kept)
with `github_pat_`, `ghp_`, `gho_`, `ghu_`, `ghs_` or `ghr_` (`LiveSettings.cs:20`); GitLab for `glpat-`
(r6 2.4, READ); Azure DevOps for exactly 84 characters whose 0-based characters 76 to 79 are `AZDO`
(r5 5, READ); null otherwise (GitHub's 40-hex classic tokens, Azure DevOps' 52-character legacy ones,
anything unprefixed). The sign-in dialogs (10.4) and each provider's local refusal (5.4, 6.4, 7.4)
refuse another provider's form without a request; none requires its own.

**`src/VisualCommit.Core/Hosting/PullRequestRefs.cs`** (WP0 with 7a's GitHub rule moved here from
`GitHub/GitHubUrls.cs:46-63`, which then calls it; then WP-C):

```csharp
public static class PullRequestRefs
{
    // A request's head ref that D305's fetch takes: refs/pull/<n>/head, refs/merge-requests/<n>/head; null on Azure DevOps (D361).
    // WP0 builds it whole, as 8.4 says.
    public static string? HeadOf(HostingProvider provider, int number);
    // refs/pull/<digits>/head or refs/merge-requests/<digits>/head (WP0: the first form only, 7a's).
    public static bool IsFetchableHead(string sourceRef);
    // The push guard (ruling 14): false only for a request ref that cannot exist on the provider: one whose
    // number segment <x> is not all digits (GitHub refs/pull/<x>/head|merge, 7a's; GitLab
    // refs/merge-requests/<x>/<rest>; Azure DevOps refs/pull/<x>/<rest>), and on Azure DevOps
    // refs/pull/<digits>/head (r5 0.4, MEASURED); true otherwise (refs/merge-requests/foo, refs/pull/fix).
    // WP0: GitHub's rule, true for the others.
    public static bool SourceCanExist(HostingProvider provider, string remoteRef);
}
```

**`src/VisualCommit.Core/Operations/CredentialBinding.cs`** (WP0; then WP-C). `GitCredential`,
`IGitCredentialSource` and `NullGitCredentialSource` unchanged (`UserName = "x-access-token"` for all
three, ruling 8). New, beside them:

```csharp
// What one account's token covers for git (D359): the provider builds it, Select applies it.
public sealed record GitCoverage(AccountKey Account, Uri Origin)   // Origin: scheme, host and port the binding is on
{
    public string? Organization { get; init; }   // Azure DevOps: the first path segment on Origin must exist and be it (G3.4); null: any path
    public bool OrganizationHost { get; init; }  // Azure DevOps on the service: https://<Organization>.visualstudio.com, any path, covered too
    public bool GitTwin { get; init; }           // GitLab: a URL whose path lacks .git is bound with its .git twin (ruling 12)
    public bool HelperUserIsAccounts { get; init; }   // x-access-token@ counts as the account's user (ruling 11; Azure DevOps and GitLab)
}
public sealed record CredentialBinding(string Url, AccountKey Account)
{
    // On an origin that serves other accounts (dev.azure.com): the organization a prompt must name to be this account's
    // (its raw path's first segment, or with an empty path its user, without case; a path-less x-access-token prompt
    // is the account's too; 8.3); null: the origin is the account's alone.
    public string? SharedOriginOwner { get; init; }
    // WP-C (8.2): G3.4's form, the .git and credential twins (steps 4, 4b), the drop in git's credential view (step 5).
    public static IReadOnlyList<CredentialBinding> Select(IReadOnlyList<RemoteEffectiveUrls> remotes, GitCoverage coverage);
    public static IReadOnlyList<CredentialBinding> Select(IReadOnlyList<RemoteEffectiveUrls> remotes, Uri webBase, AccountKey account);   // 7a's; = Select(remotes, new GitCoverage(account, webBase))
    // g3 G3.4's form on the URL's text (8.2 step 2): the one rule WP-C's Select and WP-AZ's recognition both apply.
    public static bool InPlainForm(string url);
}
```

WP0's body of the coverage form applies 7a's rules on `Origin` and throws `NotSupportedException`
when `Organization`, `OrganizationHost` or `GitTwin` is set, as `InPlainForm` does; WP-C builds them,
G3.4's rule, the credential twin and the fail-closed drop. With only `Origin` set, `Select` stays 7a's,
byte for byte.

### 1.3 Git layer

- `IGitRepository.FetchToFetchHeadAsync` keeps its signature; its guard
  (`GitRepository.PullRequests.cs:30-33`, WP0; then WP-P) calls `PullRequestRefs.IsFetchableHead`, so
  a GitLab head is fetched once WP-C widens the rule; its exception text names both forms (WP-P).
- `AskPassBroker` (WP-C): `AccountOf` (`AskPassBroker.cs:831-841`) attaches a prompt to a binding with
  a `SharedOriginOwner` only when the prompt's raw URL path, with no segment empty, `.` or `..` or
  holding `%` or `\`, begins with that organization without case, or the path is empty and its user is
  the organization without case or `x-access-token` (8.3); the warning at `:144`
  ("GitHub's token is not given to git.") names no provider. `IsAccountUser` (`:602-608`) is unchanged:
  the Azure DevOps login is the organization. The helper and the protocol are unchanged.
- No new git call; `ReadUrlRewritesAsync` and every count of git calls stay 7a's.

### 1.4 Hosting: options, endpoints, provider, records, the HTTP core

**`src/VisualCommit.Hosting/HostingOptions.cs`** (WP0 whole; then WP-S). 7a's positional record and
its twelve test calls keep compiling, with the new providers off:

```csharp
public sealed record HostingOptions(GitHubEndpoints? GitHub, Func<HttpMessageHandler> CreateHandler)
{
    public const string GitHubVariable = "VISUALCOMMIT_GITHUB_URL";
    public const string AzureDevOpsVariable = "VISUALCOMMIT_AZURE_DEVOPS_URL";
    public const string GitLabVariable = "VISUALCOMMIT_GITLAB_URL";
    public AzureDevOpsEndpoints? AzureDevOps { get; init; }                  // null: off
    public Func<HttpMessageHandler>? CreateAzureDevOpsHandler { get; init; }  // null: every request refused (Off's RefusingHandler)
    public GitLabEndpoints? GitLab { get; init; }
    public Func<HttpMessageHandler>? CreateGitLabHandler { get; init; }       // null: every request refused (Off's RefusingHandler)
    public bool AllOff => GitHub is null && AzureDevOps is null && GitLab is null;
    public static HostingOptions Off { get; }   // the three off (the harnesses' default)
    // Each variable as 7a's: unset, the service; "none", off; http://127.0.0.1:<port>/, Local(port); else off and a warning naming the variable.
    // Each provider it turns on gets its handler set explicitly: DefaultHandler(AzureDevOps.WebBase), DefaultHandler(GitLab.ApiBase).
    public static HostingOptions FromEnvironment(Func<string, string?> readEnvironment, IAppLog log);
    public static Func<HttpMessageHandler> DefaultHandler(GitHubEndpoints endpoints);   // 7a's
    public static Func<HttpMessageHandler> DefaultHandler(Uri apiBase);                 // the same rules for any API
}
```

A new provider's handler is never a real one by default: `Off with { AzureDevOps =
AzureDevOpsEndpoints.Service }` without its handler reaches no service. Members computed from others
(`AllOff`; the endpoints' `Host`; `FullName`, `OwnerRoot`; 1.2's `ProviderId` and `Id`; 1.5's
`RequestRemotes`) are expression-bodied, never stored, since the records are copied with `with`
(15.5; `ShellDriver.Phase7a.cs:94`).

**`AzureDevOps/AzureDevOpsEndpoints.cs`** (WP0; then WP-AZ) and **`GitLab/GitLabEndpoints.cs`** (WP0;
then WP-GL); `GitHubEndpoints` is unchanged:

```csharp
public sealed record AzureDevOpsEndpoints(Uri WebBase, bool OrganizationHosts)   // web, git and API on one origin (D285)
{
    public static AzureDevOpsEndpoints Service { get; }   // https://dev.azure.com/, OrganizationHosts true
    public static AzureDevOpsEndpoints Local(int port);   // http://127.0.0.1:<port>/, OrganizationHosts false (D363)
    public string Host => WebBase.Authority;              // "dev.azure.com" or "127.0.0.1:<port>"
    public Uri ApiOf(string organization);                // WebBase + the escaped organization + "/"
    public string? ListProject { get; init; }             // the live command's sandbox project (D352); null: the organization's list
}
public sealed record GitLabEndpoints(Uri WebBase, Uri ApiBase)
{
    public static GitLabEndpoints GitLabCom { get; }      // https://gitlab.com/ and its api/v4/
    public static GitLabEndpoints Local(int port);        // http://127.0.0.1:<port>/ and its api/v4/
    public string Host => WebBase.Authority;              // "gitlab.com" or "127.0.0.1:<port>"
}
```

**`IHostingProvider.cs`** (WP0 whole; the lead afterwards). `Endpoints` leaves the interface (each
provider keeps its own), and so does A6, which stays on `GitHubProvider` (D349):

```csharp
public interface IHostingProvider
{
    HostingProvider Kind { get; }
    string Host { get; }                     // its accounts' AccountKey.Host
    Uri WebBase { get; }                     // the web and git origin
    HostedRepository? Recognise(string url); // any organization, no account, no network; SSH recognised, never bound
    bool SourceCanExist(string remoteRef);   // PullRequestRefs.SourceCanExist(Kind, remoteRef)
    GitCoverage CoverageOf(AccountKey account);                     // D359
    bool Covers(HostedRepository repository, AccountKey account);   // its host; on Azure DevOps the account's organization (D350)
    Uri TokenPage(string? organization);                            // the dialog's link; Azure DevOps' needs the organization
    Uri PullRequestsPage(HostedRepository repository);              // "Open … in browser"
    string? PullRequestHeadRef(PullRequest pull);                   // PullRequestRefs.HeadOf(Kind, pull.Number)
    Task<HostingResult<HostingAccount>> CheckTokenAsync(string token, string? organization, CancellationToken ct);         // A1
    Task<HostingResult<RepositoryList>> ListRepositoriesAsync(string token, HostingAccount account, CancellationToken ct);  // A2
    // A3, A4 and A5 as 7a's (7a design 1.4)
}
```

WP0 gives `GitHubProvider` these members with 7a's behaviour: `CoverageOf` 7a's rule on its web base,
`Covers` by host, `TokenPage` D295's link (moved from `SignInDialogViewModel.cs:188`; the text
`TokenPage`, `OperationTexts.Hosting.Accounts.cs:60`, stays, 7a's checks read it), `PullRequestsPage`
`{web}/{owner}/{name}/pulls` (moved from `RepositoryOperations.PullRequests.cs:125-131`); WP0 points
those two callers at `TokenPage(null)` and `PullRequestsPage(repository)`, so that nothing reads
`Endpoints` through the interface; `organization` and `account` ignored. `GitHubProvider` keeps 7a's
public `CheckTokenAsync(string token, CancellationToken ct)` and `ListRepositoriesAsync(string token,
CancellationToken ct)` unchanged for the whole phase: they are not 1.5's shims, and WP-N does not remove
them. The interface's A1 and A2 are public overloads beside them that call them. So 7a's
`GitHubProviderTests`, `HostingOptionsTests.cs:134`, the live project's `GitHubLiveTests.cs:643` and
`LiveSafetyTests.cs:784`, and WP0's `HostingSession` (`HostingSession.cs:205`, `:314`, `:751`, which holds a
`GitHubProvider` until WP-S calls through the interface) compile unchanged.

WP-AZ builds `public sealed class AzureDevOpsProvider(AzureDevOpsEndpoints endpoints,
Func<HttpMessageHandler> createHandler, IAppLog log, TimeProvider clock) : IHostingProvider, IDisposable`,
WP-GL `GitLabProvider(GitLabEndpoints endpoints, …)` likewise; WP-S constructs both. Their members
beyond the interface, which other packages call:

```csharp
public static class AzureDevOpsApi   // AzureDevOps/AzureDevOpsApi.cs (WP-AZ), as 7a's GitHubApi; the live command's probes send the same
{ public const string Version = "7.1", ConnectionDataVersion = "7.1-preview.1", CoreAreaId = "79134c72-4a58-4b42-976c-04e7115f32bf"; }
// AzureDevOpsProvider (6.2, 6.4, 6.6):
public AzureDevOpsEndpoints Endpoints { get; }
public static string? OrganizationOf(string? text, AzureDevOpsEndpoints endpoints);   // a name or either URL form; null: not usable (WP-A's box)
public const string AnonymousDetail = "anonymous";   // HostingFailure.Detail of A1's refusal of the Anonymous identity
internal void ForgetKept();                           // WP-S at that provider's sign-out
// GitLabProvider (7.4):
public GitLabEndpoints Endpoints { get; }
public Uri LegacyTokenPage { get; }                    // the prefilled legacy form; TokenPage(_) is the fine-grained one
public static string TitleFor(string title, bool draft);   // the title A5 sends; WP-N's form shows it
internal void ForgetKept();
```

**`HostingRecords.cs`** (WP0; then WP-H). Changed members only:

```csharp
public sealed record HostedRepository(HostingProvider Provider, string Host, string Owner, string Name)   // Owner: the namespace path
{
    public string FullName => $"{Owner}/{Name}";        // what the App shows ("group/sub/project", "<organization>/<project>/<repository>")
    public string OwnerRoot => Owner.Split('/')[0];     // GitHub's owner, GitLab's top namespace, Azure DevOps' organization
}   // equality as 7a's: provider, host, owner and name, without case
public sealed record HostingAccount(AccountKey Key, string Login, long Id, string? Name, Uri? AvatarUrl, bool UseForGit,
    string? UserId = null);            // Azure DevOps' user GUID (Id 0 there); GitLab's numeric id is Id
public enum PullRequestRefusal { None, AlreadyExists, NoCommits, HeadMissing, BaseMissing, BranchesMissing }
// HostingFailure: + HostingProvider Provider { get; init; } = GitHub (every client sets it); + PullRequestRefusal Refusal { get; init; } (A5's reason)
// NewPullRequest: + HostedRepository? HeadRepository { get; init; } (across forks; GitHub keeps HeadOwner)
```

`RepositoryInfo` keeps its members: a service without `UpdatedAt` or `CanPush` gives
`DateTimeOffset.MinValue` and `true` (the App shows neither), and a provider returns its list in the
order the browser shows. `RequestId` takes Azure DevOps' `ActivityId` and GitLab's request id. WP-H maps
GitHub's 422 sentences to `Refusal`; the App reads `Refusal` from WP-N on.

**`src/VisualCommit.Hosting/Http/`** (WP-H), internal, seen by `VisualCommit.Hosting.Tests` through 7a's
`InternalsVisibleTo`:

```csharp
internal sealed record HostingExchange(HttpMethod Method, Uri Url, int Status, byte[] Body, IReadOnlyDictionary<string, string> Headers)
{ public string? Reason { get; init; } }   // Body: a leading BOM stripped; Headers (here and in the reply) without case, values joined by ", "
internal sealed record HostingReply<T>(T? Value, HostingFailure? Failure, int Status, Uri? Next, IReadOnlyDictionary<string, string> Headers, string? RequestId);
internal abstract class HostingHttpProfile
{
    public abstract HostingProvider Provider { get; }
    public abstract Uri TokenOrigin { get; }                                    // the one origin the token goes to
    public abstract JsonSerializerContext Json { get; }                         // request bodies; a type outside it: ArgumentException
    public abstract void Authorize(HttpRequestMessage request, string token);
    public virtual void AddHeaders(HttpRequestMessage request) { }
    public abstract HostingFailure Classify(HostingExchange exchange);          // any status but 2xx, 304 and a redirect
    public virtual HostingFailure? RefuseSuccess(HostingExchange exchange) => null;   // every 2xx and 304, before parsing
    public virtual HostingFailure? RefuseRedirect(HostingExchange exchange, Uri location) => null;   // every redirect, same-origin too; null: 7a's rule
    public virtual bool SucceedsEmpty(int status) => false;                     // GitHub: 409
    public virtual Uri? NextPage(HostingExchange exchange) => null;             // GitHub, GitLab: HostingLinks.Next
    public virtual string? RequestIdOf(IReadOnlyDictionary<string, string> headers) => null;
    public virtual IReadOnlyCollection<string> KeptHeaderNames => [];           // kept with an answer, served under a 304's own
    public virtual string WarningOf(HostingExchange exchange, HostingFailure failure) => exchange.Status.ToString(CultureInfo.InvariantCulture);
        // <status or kind> of the core's warning for a failure the profile gave (Classify, RefuseSuccess, RefuseRedirect)
}
internal sealed class HostingHttpClient(HostingHttpProfile profile, Func<HttpMessageHandler> createHandler, IAppLog log,
    TimeProvider clock, TimeSpan? timeout = null, long? keptBytes = null) : IDisposable
{   // 7a's MaxBodyBytes (10 MB), MaxRedirects (3) and DefaultTimeout (30 s)
    public Task<HostingReply<T>> SendAsync<T>(HttpMethod method, string token, Uri url, object? body, JsonTypeInfo<T> type, CancellationToken ct);
    public Task<HostingReply<List<T>>> GetPagesAsync<T>(string token, Uri first, JsonTypeInfo<List<T>> type, int maxPages, CancellationToken ct,
        Action<IReadOnlyDictionary<string, string>>? pageHeaders = null);
    public HostingFailure MalformedAnswer(HttpMethod method, Uri url, string? requestId, string? token = null);   // a success lacking what the app needs, with its warning
    public HostingFailure Failed(HttpMethod method, Uri url, HostingFailure failure, string? requestId, string? token = null);   // a client's own failure from a success, finished, warned by its kind
        // token: the request's, screened as in the core's own failures (WP-GL's hunt 3, R1); 7a's GitHub passes none
    public void ForgetKept();
}
internal static class HostingHttpTexts   // "Could not reach {name}.", "{name} did not answer within 30 seconds.", "{possessive} answer could not be read.", "{name} had a problem: HTTP {status}."
{ public static string Network(HostingProvider p); public static string Timeout(HostingProvider p); public static string Malformed(HostingProvider p); public static string ServerError(HostingProvider p, int status); }
internal static class HostingLinks { public static Uri? Next(IReadOnlyDictionary<string, string> headers); }   // Link rel="next"
```

GitHub's results equal 7a's `GitHubClient.NetworkText`, `TimeoutText` and `MalformedText`
(`GitHubClient.cs:44-50`) and its 5xx text (`:453`). The core sets `Provider` on every failure, and
`Url` and `RequestId` where the profile left them null (every `Url` without its userinfo and query,
D380, each path segment holding an address `<e-mail>`, and a `RequestId` kept only of 1 to 128 of
`[A-Za-z0-9:._-]`, D382), and screens e-mail addresses (D368); a failure a
client builds itself from a reply that succeeded goes through `Failed` to get the same. `RefuseRedirect`
is asked of every redirect with a `Location`, one to another origin and a fourth one included, before
7a's rule; the redirect's exchange has an empty `Body` and the request's `Url`, query kept (WP-H's
as built, 5). `GitHubClient` becomes GitHub's profile on the core with
`GitHubClientTests` unchanged (5). Azure DevOps' `{"count", "value"}` pages, by `$top` and `$skip`,
are walked by its own client through `SendAsync`.

### 1.5 Hosting: the session

**`src/VisualCommit.Hosting/IHostingSession.cs`** (WP0 whole with `NullHostingSession` and
`NullRepositoryHosting`; then WP-S, which implements the members and adds none, since WP-C's
`HostingSessionStub` and the test stubs implement them too; then WP-N, which removes the shims):

```csharp
public sealed class HostingAccountChangedEventArgs(HostingProvider provider) : EventArgs { public HostingProvider Provider { get; } = provider; }
public sealed record SignInRequest(HostingProvider Provider, string Token, string? Organization = null);   // ToString hides the token

public interface IHostingSession : IGitCredentialSource, IDisposable   // events through the post delegate (UI thread)
{
    bool IsOff { get; }                                         // every provider off
    IReadOnlyList<HostingProvider> Providers { get; }           // those on, in HostingProviders.InAccountsOrder
    bool IsOn(HostingProvider provider);
    IHostingProvider? ProviderOf(HostingProvider provider);     // null while off
    AccountStatus? AccountOf(HostingProvider provider);         // signed in or needing sign-in; null without one
    event EventHandler<HostingAccountChangedEventArgs>? AccountChanged;   // 7a's subscribers compile unchanged
    Task StartAsync(CancellationToken ct);                      // accounts.json and each account's token, once
    Task<bool> KeychainAvailableAsync(CancellationToken ct);    // the one cached probe
    Task<SignInResult> SignInAsync(SignInRequest request, CancellationToken ct);   // A1; replaces that provider's account (D348), at once (D256)
    Task<string?> SignOutAsync(HostingProvider provider, CancellationToken ct);    // GitHub's alone deletes avatars/
    void SetUseForGit(HostingProvider provider, bool useForGit);
    HostedRepository? Recognise(string url);                    // by every provider on
    bool SourceCanExist(string remoteUrl, string remoteRef);    // the recognising provider's rule; true when none recognises it
    IReadOnlyList<CredentialBinding> BindingsForClone(string url);
    bool BindsGit { get; }                                      // any provider's account binds git
    IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites);   // the union
    Task<HostingResult<RepositoryList>> ListRepositoriesAsync(HostingProvider provider, CancellationToken ct);
    IRepositoryHosting ForRepository(string workingDirectory);  // NullRepositoryHosting.Instance while all are off

    // 7a's shims (ruling 1), default bodies; WP-N's "done when" says none is left.
    IHostingProvider? GitHub => ProviderOf(HostingProvider.GitHub);
    AccountStatus? Account => AccountOf(HostingProvider.GitHub);
    Task<SignInResult> SignInAsync(string token, CancellationToken ct) => SignInAsync(new SignInRequest(HostingProvider.GitHub, token), ct);
    Task<string?> SignOutAsync(CancellationToken ct) => SignOutAsync(HostingProvider.GitHub, ct);
    void SetUseForGit(bool useForGit) => SetUseForGit(HostingProvider.GitHub, useForGit);
    Task<HostingResult<RepositoryList>> ListRepositoriesAsync(CancellationToken ct) => ListRepositoriesAsync(HostingProvider.GitHub, ct);
}

public enum PullRequestsState { NoHostedRemote, SignedOut, Loading, Failed, Loaded, OtherOrganization }   // NotGitHub renamed; D356's appended
public sealed record RepositoryHostingState(/* 7a's six */)
{
    public static RepositoryHostingState NoHostedRemote { get; }   // 7a's NotGitHub
    // Remotes of Repository's provider and host (on Azure DevOps of its OwnerRoot, without case): where a request can go;
    // empty without Repository and while PullRequests is OtherOrganization. Computed from Remotes, Repository and
    // PullRequests, never stored, since the record is copied with `with` (1.4).
    public IReadOnlyDictionary<string, HostedRepository> RequestRemotes => …;
}
```

`AccountStatus`, `SignInResult`, 7a's three account enums, `HostingRemoteOrder`, `AuthorAvatars` and
`IRepositoryHosting` keep their members; `ShowAvatarsFor` takes GitHub remotes' authors only.
`Remotes` keeps every recognised remote; `List` and `ByRef` are the listed provider's, empty while
`OtherOrganization`, whose `Repository` is the remote's: that state holds while an Azure DevOps account
is held, signed in or needing sign-in, that does not cover the listed remote (D366). WP0's `HostingSession` (7a's, GitHub alone)
answers the new members for GitHub (another provider: no account, not on, a sign-in a `Validation`
failure without a request); WP-S builds it for three providers with 7a's constructor, and
`JsonAccountStore`'s records per provider and host in place of `GitHubOn`, `WithGitHub` and
`WithoutGitHub` (`JsonAccountStore.cs:118-159`).

### 1.6 Hosting: tokens

No new store, vault member or variable: `TokenKey.Of(AccountKey)` carries provider, host and login,
each lower-cased (`ITokenStore.cs:11-14`). `TokenEntryNames.ProviderNames` (`Tokens/TokenEntryNames.cs:23-26`, WP0)
takes `HostingProviders.DisplayName` for `azure-devops` and `gitlab`: entries
`VisualCommit:gitlab:gitlab.com:<login>` and `VisualCommit:azure-devops:dev.azure.com:<organization>`,
labels `VisualCommit: Azure DevOps (<organization>)`, whose space is safe in `security -i`'s quotes
(r8 S11, READ; 1.10's tests and CI's macOS real-store step pin it). The vault reads each account's
token once per run (WP-S).

### 1.7 App

- **`TabServices`** (lead): `public Func<HostingProvider, Task<bool>>? SignIn { get; set; }` in place of
  `SignInToGitHub` (`TabServices.cs:201`), true when that provider's account is signed in afterwards.
  WP0 moves the five callers (r3 6) to `SignIn(HostingProvider.GitHub)`, and
  `MainWindowViewModel.Hosting.cs`'s `SignInToGitHubAsync()` becomes `SignInAsync(HostingProvider
  provider)`, false for another provider until WP-A; `MainWindowViewModel.cs:71` assigns it.
- **`IRepositoryActions`** (lead): `SignInToGitHubAsync()` becomes `SignInToHostAsync()`: signs in to the
  listed repository's provider (WP0 renames; WP-P owns the body in `RepositoryOperations.PullRequests.cs`).
- **`ViewModels/Hosting/HostingTexts.Provider.cs`** (WP0 whole; the lead afterwards), the App's one place
  for a provider's words (ruling 21, D351, D355). For GitHub each returns 7a's constant
  (`HostingTexts.cs:9-22`), which stays:

  ```csharp
  public static string Name(HostingProvider p);            // HostingProviders.DisplayName
  public static string Possessive(HostingProvider p);      // HostingProviders.Possessive
  public static string Request(HostingProvider p);         // "pull request"; GitLab "merge request"
  public static string Requests(HostingProvider p);        // "pull requests"; "merge requests"
  public static string RequestTitle(HostingProvider p);    // "Pull request"; "Merge request"
  public static string RequestsTitle(HostingProvider p);   // "Pull requests"; "Merge requests": the section's title
  public static string Number(HostingProvider p, int n);   // "#12"; GitLab and Azure DevOps "!12" (D355)
  public static string AddAccountTitleOf(HostingProvider p);   // "Add GitHub account", "Add GitLab account", "Add Azure DevOps account"
  public static string SignInToItemOf(HostingProvider p);      // "Sign in to GitHub…"
  public static string SignInInSettingsOf(HostingProvider p);  // "Sign in to GitHub in Settings, Accounts."
  ```

- **`HostingTexts.Describe(HostingFailure failure, string? login)`** keeps its signature and reads
  `failure.Provider`; WP-B words it for the three providers (GitHub's results unchanged; `SsoRequired`
  stays GitHub's).
- **`OperationTexts.Hosting.Credentials.cs`** (WP0; then WP-C): `TokenRefused(HostingProvider
  provider, string login)` and `GitCredentialsRefused(HostingProvider provider, string login)`, GitHub's
  output word for word. 7a's one-argument forms stay, delegating with GitHub, so that 7a's tests keep
  them unchanged. The callers in src/ move to the provider forms in their owners' files: WP-B's
  `HostingTexts.Describe.cs:53`, WP-C's `RefusedDisplay` (`RepositoryOperations.Run.cs:749`) and WP-N's
  `RepositoryOperations.CreatePullRequest.cs:454`.
- **Texts the live command composes** (15.6): `OperationTexts.MenuTexts.CreateRequest(p)` (WP-P, 12.2)
  and `OperationTexts.CreatePullRequestTexts.Created(p, n, draft)` (WP-N, 13.2) are public.
- **Accounts' ids** (WP-A): containers `AccountsGitHub`, `AccountsGitLab`, `AccountsAzureDevOps`;
  GitHub's block keeps 7a's ids; the others' end in the provider, `AccountsSignInButtonGitLab`,
  `AccountLoginAzureDevOps`; the sign-in dialog's new box is `SignInOrganization`. The repository
  browser's provider switch (WP-B, 11.4) has one id per provider, `CloneFromGitHubProviderGitHub`,
  `…GitLab`, `…AzureDevOps` (D362). Every other surface keeps 7a's ids (`CloneFromGitHub*`,
  `PullRequest*`).
- **Owners after WP0**: `RepositoryOperations.Run.cs` (`RefusedDisplay` alone) and the guard's comment
  in `RepositoryOperations.Remotes.cs` WP-C, in wave 1; `RepositoryViewModel.Hosting.cs` (then WP-P),
  `RepositoryViewModel.Avatars.cs` WP-S; `AccountsSettingsViewModel.cs`, `SignInDialog*`,
  `SettingsDialogView.axaml`, `SettingsDialogViewModel.cs`, `MainWindowViewModel.Hosting.cs`,
  `MainWindowViewModel.Settings.cs` WP-A; `AppCommands.Palette.cs` (then WP-P), the other
  `AppCommands*.cs`, `AppCommand.cs`, `MainWindowViewModel.Commands.cs`, `WelcomeViewModel.cs`,
  `WelcomeView.axaml`, `CloneFromGitHubDialog*`,
  `HostingTexts.Describe.cs`, App.Tests' `AppShortcutsTests.cs`, Hosting.Tests' 7a classes
  `WelcomeCloneFromGitHubTests` and `PaletteCloneFromGitHubTests` WP-B; `LeftPanelViewModel*`,
  `LeftPanelView.axaml`, `PullRequestRows.cs`, `RefListRows.cs`, `PullRequestCheckout.cs`,
  `RepositoryMenus.Hosting.cs`, `RepositoryViewModel.Menus.cs`, `RepositoryOperations.PullRequests.cs`,
  `CommitGraphControl.Badges.cs`, Git.Tests' `GitRepositoryPullRequestsTests` WP-P;
  `RepositoryOperations.CreatePullRequest.cs`, `CreatePullRequestDialog*` WP-N; each
  `OperationTexts.Hosting.<Area>.cs` its area's package. 7a's walk-through files go with their part's
  7b package (A WP-A, B WP-B, P and G WP-P but `RepositoryAvatarsTests.cs` WP-S, N WP-N, the rest
  WP-F).

### 1.8 Variables, files, ports

| Variable | Read by | Values | Default in the harnesses |
|---|---|---|---|
| `VISUALCOMMIT_GITHUB_URL` | `HostingOptions.FromEnvironment` | 7a's | `none` |
| `VISUALCOMMIT_AZURE_DEVOPS_URL` | the same | unset: `dev.azure.com` and `<organization>.visualstudio.com`; `none`: off; `http://127.0.0.1:<port>/`: the fake | `none` |
| `VISUALCOMMIT_GITLAB_URL` | the same | unset: gitlab.com; `none`; `http://127.0.0.1:<port>/` | `none` |
| `VISUALCOMMIT_LIVE_AZURE_DEVOPS_TOKEN`, `VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO` | `VisualCommit.LiveHostingTests` | the token; `<organization>/<project>/<repository>` | removed from every test process (Windows user variables, D346) |
| `VISUALCOMMIT_LIVE_GITLAB_TOKEN`, `VISUALCOMMIT_LIVE_GITLAB_REPO` | the same | the token; `<namespace path>/<project>` | removed likewise |
| 7a's other variables | as 7a design 1.8 | | as there |

`GitIsolation.MachineIndependence` (`tests/VisualCommit.Testing/GitIsolation.cs:345-360`, WP0; then
WP-F) gains the two new URL variables at `none` beside `:358`'s, and `HostingTokenVariables`
(`:310-319`) gains `GITLAB_TOKEN`, `GITLAB_ACCESS_TOKEN`, `OAUTH_TOKEN`, `GITLAB_HOST`, `GITLAB_URI`,
`GL_HOST`, `GITLAB_API_HOST`, `GL_TOKEN`, `CI_JOB_TOKEN`, `AZURE_DEVOPS_EXT_PAT`, `SYSTEM_ACCESSTOKEN`,
`GCM_AZREPOS_SP_SECRET`, `GCM_AZREPOS_WIF_ASSERTION` and the four new live variables (ruling 27; g4
D2.3), so every test process and the real-window app start with the new providers off and no tool's
token. 7a's two pins check containment and stay; WP-F pins the new names in `Phase7bSupportTests`.

Data folder: `accounts.json` keeps `version` 1; a record's `provider` is `github`, `gitlab` or
`azure-devops`, `id` a JSON number (0 for Azure DevOps), the new `userId` a string (Azure DevOps
alone), `avatarUrl` null for the new providers, nothing else added (WP-S tests that 7a's reader shape
reads a 7b file, g4 D4 §6). `avatars/` is GitHub's alone; `settings.json` is unchanged.

Ports (D363; `AuthRemoteServer.cs:51-83`, WP-F, pinned in `AuthRemoteServerTests`):
`Phase7bAzureDevOpsPort = 47397`, `Phase7bGitLabPort = 47398`, `Phase7bGitHubPort = 47399` (7b's own
fake of GitHub, so no 7b picture depends on 7a's 47395 in the process beside it); 47396 stays reserved
for a second 7a project. Only 7b's walk-through and its real-window twin bind them, with D265's
fallback; every other test uses port 0 or a fake in process, whose endpoints are `Local(1)` (GitHub,
7a's), `Local(2)` (Azure DevOps) and `Local(3)` (GitLab).

### 1.9 Seams and their fakes

- As 7a design 1.9, with the three providers: the desktop app has them on; headless, `Off` or the
  fakes given (each through a `LoopbackOnlyHandler`) or `hosting:` options; the real window each
  `none` unless a 7b check sets its fake's base (`RealApp.Phase7b.cs`).

Test infrastructure other packages use (owner WP-F unless named; members in the section named):
- **The fakes' core** (2.3, `Testing/Hosting/FakeHostingService*.cs`), the base of `FakeAzureDevOps` and
  `FakeGitLab`; `FakeGitHub`'s public API (7a design 2.3) is unchanged. A repository is named by its
  folder under `Root` without a final `.git` (`Fernhill-Labs/Mobile App/_git/mobile-app`, `g/s/p`) in
  every callback, hold and gate, whichever registered path reached it. Route keys (`RouteOf`'s, of holds
  and faults) are lower-case; a git hold's key is `git:` and the folder's name in the folder's case
  (`git:Fernhill-Labs/Mobile App/_git/mobile-app`, `_git` included), matched without case (3.5, 4.5):

```csharp
public enum FakeAuthorizationForm { None, Bearer, Basic, BasicEmptyUser, PrivateToken, Query, Other }   // BasicEmptyUser ":" + token; Query a private_token parameter; two headers Other
public sealed record FakeServiceRequest(string Method, string Host, string Path, string Query, string? Route,
    FakeAuthorizationForm Authorization, string? Login, int Status, string? Body);   // Path raw, as received; Body of POST, PUT, PATCH
public class FakeServiceFault   // each fake derives a sealed one with static members and a private constructor (3.4, 4.4)
{
    protected FakeServiceFault(string name);
    public string Name { get; }
    public static FakeServiceFault Dropped { get; }             // the core answers these two
    public static FakeServiceFault ServedThenDropped { get; }
}
// FakeServiceHold : IDisposable (Route, Arrived, Released, Abandoned); FakeServiceCall (Request, Route, Fault, Authorization,
// Token, settable Login; ToString() hides the token).
public abstract class FakeHostingService : IAsyncDisposable
{
    // Public: Port, PortWasTaken, Root, DisplayName, MaxPerPage, CreateHandler(), Requests, GitRequests, Errors (snapshots),
    // HoldNext(string route) → FakeServiceHold, FailNext(string route, FakeServiceFault fault), Revoke(string token), DisposeAsync().
    protected FakeHostingService(string root, string displayName, int inProcessPort);
    protected abstract string Realm { get; }
    protected abstract bool Claims(string method, string path);         // the request line's raw path
    protected abstract string? RouteOf(string method, string path);    // the lower-case key of holds and faults; null: none
    protected abstract Task<AuthRemoteResponse> AnswerAsync(FakeServiceCall call, CancellationToken ct);
    protected abstract bool IsPublic(string repository);
    protected abstract bool MayPush(string repository, string login);
    protected virtual string PushRefusal(string repository, string login);           // 7a's text
    protected virtual string? FetchRefusal(string repository, string login) => null; // non-null: a 403 text/plain
    protected virtual string? LoginOfToken(string repository, string token);         // TokenLoginLocked
    protected virtual Task BeforeGitAsync(string repository, CancellationToken ct);  // the ref sync; none
    protected Lock Gate { get; }
    protected Task StartServerAsync(int port, bool fallBackToFreePort);              // sets Port, PortWasTaken
    protected void AddTokenLocked(string token, string login);                       // known until Revoke
    protected string? TokenLoginLocked(string token);                                // null: unknown or revoked
    protected Task<string> BareRepositoryAsync(string urlPath, bool gitSuffix, string? source, CancellationToken ct);   // 2.3's rule, then ServeAt
    protected Task<GitResult> GitAsync(string folder, string? input, IReadOnlyDictionary<string, string?>? environment,
        CancellationToken ct, params string[] arguments);                           // isolated, plus environment (commit-tree's names, dates)
    protected SemaphoreSlim LockOf(string repository);
    protected void RecordError(string text);
}
```

- **`FakeAzureDevOps`** (WP-F's skeleton, built by WP-FA, 3) and **`FakeGitLab`** (likewise WP-FL, 4):

```csharp
public sealed partial class FakeAzureDevOps : FakeHostingService   // and likewise FakeGitLab: InProcessPort 3, GitLabEndpoints
{
    public const int InProcessPort = 2;   // Endpoints: Local(Port), in process Local(2)
    public static Task<FakeAzureDevOps> StartAsync(string root, int port = 0, bool fallBackToFreePort = false, CancellationToken ct = default);
    public static FakeAzureDevOps InProcess(string root);
    public AzureDevOpsEndpoints Endpoints { get; }
    public int CreateStatus { get; set; } = 201;
    public FakeAzureOrganization AddOrganization(string name, bool publicProjects = false);
    public FakeAzureProject AddProject(string organization, string name, bool isPublic = false, string? description = null);
    public FakeAzureUser AddUser(string organization, string displayName);
    public string AddToken(string organization, FakeAzureUser user, bool codeWrite = true);
    public void Expire(string token);
    public Task<FakeAzureRepository> AddRepositoryAsync(string organization, string project, string name,
        string? sourceRepository = null, FakeAzureRepository? forkOf = null, CancellationToken ct = default);
    public FakeAzurePullRequest AddPullRequest(FakeAzureRepository target, string title, FakeAzureUser creator, string sourceBranch,
        string targetBranch, FakeAzureRepository? fork = null, bool draft = false, string? description = null, int? id = null);
    public FakeAzureRepository Repository(string organization, string project, string name);
    public IReadOnlyList<FakeAzurePullRequest> PullRequests(FakeAzureRepository repository);
    public Task SyncRefsAsync(FakeAzureRepository repository, CancellationToken ct = default);
}
public sealed partial class FakeGitLab : FakeHostingService
{
    public FakeGitLabUser AddUser(string username, long id, string? name = null);
    public string AddToken(string username, FakeGitLabTokenKind kind = FakeGitLabTokenKind.FineGrained,
        IReadOnlyCollection<string>? permissions = null, DateOnly? expires = null);
    public void Expire(string token);
    public Task<FakeGitLabProject> AddProjectAsync(string path, string? sourceRepository = null,
        string visibility = "private", FakeGitLabProject? forkOf = null, CancellationToken ct = default);
    public void Grant(string path, string username, int accessLevel);   // a group or project and all below it
    public void AddListedProjects(string namespacePath, int count);     // API only
    public FakeMergeRequest AddMergeRequest(FakeGitLabProject target, string title, string author, string sourceBranch,
        string targetBranch, FakeGitLabProject? source = null, string? description = null, int? iid = null);
    public FakeGitLabProject Project(string path);                      // without case
    public IReadOnlyList<FakeMergeRequest> MergeRequests(FakeGitLabProject target);
    public void Move(string path, string newPath);   // the API's GET 301 to projects/<id> (405 for other methods) and the web
        // pages' 301 to the new path (4.3); git keeps serving the old path, fetch and push, 200 with no redirect (MEASURED r6 6.1, READ g2 G3)
    public Task SyncRefsAsync(FakeGitLabProject target, CancellationToken ct = default);
}
public enum FakeGitLabTokenKind { Legacy, FineGrained }
```

- **`ShellDriver.Start` and `StartWithTabs`** gain, after 7a's last parameter, `FakeAzureDevOps?
  azureDevOps = null, FakeGitLab? gitLab = null` (with `hosting:`, an `ArgumentException`; an in-process
  fake refused, as 7a design 16.3), and `SignInAsync(HostingProvider provider, string token, string?
  organization = null, CancellationToken cancellationToken = default)` beside 7a's, through the session.
- **`ShellDriver.Phase7a.Accounts.cs`** (WP-A) gains, beside 7a's GitHub forms:
  `OpenSignInDialogAsync(HostingProvider provider, TimeSpan? timeout = null)`, `SignOutAsync(HostingProvider
  provider, bool confirm, TimeSpan? timeout = null)`, `WaitForAccountAsync(HostingProvider provider,
  Func<AccountStatus?, bool> condition, TimeSpan? timeout = null)`, `TypeOrganization(string
  organization)` (box `SignInOrganization`) and `Control AccountsBlock(HostingProvider provider)`.
- **Host mode** (2.2): `AuthRemoteServer.ServeAt(string urlPath, string folder, bool gitSuffix)` (paths
  matched without case, decoded per segment; a folder may be served at several paths); `UrlOf(string
  urlPath)` names a `ServeAt` path too. `AuthRemoteHostOptions` gains, last and optional,
  `Func<string, string, string>? PushRefusal` (repository, login → the 403's text; null: 7a's),
  `Func<string, string, string?>? LoginOfTokenIn` (repository, token → login, in place of `LoginOfToken`)
  and `Func<string, string, string?>? FetchRefusal` (repository, login → a 403 `text/plain` after auth,
  before http-backend; null: none). The fakes' core wires its virtuals to them.
- **`TokenTripwire.AssertAbsent`** keeps its signature and searches each token as written, URL-escaped
  and at the three base64 alignments, which covers `x-access-token:`, Azure DevOps' `:` and any user.
- **`Scenarios.Hosting7bAsync()`** and `ServeHosting7bAsync(TempRepo copy, int azureDevOpsPort = 47397,
  int gitLabPort = 47398, int gitHubPort = 47399, bool ownsCopy = false, CancellationToken ct = default)`
  → `ServedHosting7b` (2.6): the three fakes, `MobileApp`, `Manifest`, `WebApp`, `AzureDevOpsToken`,
  `GitLabToken`, `GitLabLegacyToken`, `GitHubToken`, `Tokens`, `Organization`, `OtherOrganization`, the
  remotes written with the ports (port 0 for each in `VisualCommit.Hosting.Tests`). Its two halves are
  seeded by `SeedHosting7bAzureDevOpsAsync` and `SeedHosting7bGitLabAsync`: WP-F's stubs, returning the
  tokens, which WP-FA and WP-FL fill (3.5, 4.5).
- **`HostingSessionGitHubForms`** (WP-S, `tests/VisualCommit.Testing/Hosting/HostingSessionGitHubForms.cs`,
  declared in namespace `VisualCommit.Hosting`, not the folder's `VisualCommit.Testing.Hosting`, so no
  caller gains a `using`): C# 14 extension members on `IHostingSession` (`GitHub`, `Account`,
  `SignInAsync(string, CancellationToken)`,
  `SignOutAsync(CancellationToken)`, `SetUseForGit(bool)`, `ListRepositoriesAsync(CancellationToken)`)
  delegating with `HostingProvider.GitHub`, for tests alone: 7a's tests compile through them once
  `HostingSession` declares no GitHub-named member, and after WP-N removes 1.5's shims. Hosting.Tests'
  files find it through their enclosing namespace `VisualCommit.Hosting.Tests`; the Testing project's,
  Phase7a's and the live project's files through the `using VisualCommit.Hosting;` they already have
  (`ShellDriver.Phase7a.Accounts.cs` and `Phase7aChecks.P.Menus.cs` among them). Until WP-N a call on an
  `IHostingSession`-typed receiver takes the interface's default body, and one on a
  `HostingSession`-typed receiver the extension. Only tests see it: src/ references no test project.
- **The app's log** (`tests/VisualCommit.Testing/AppLogFile.cs`): WP-F widens `PullRequestsListed` to any
  path depth. The session writes D322's `{repo}: listed {n} open pull requests of {full name}` for every
  provider (a log line, not a screen) and the avatars line for GitHub alone, and at its construction one
  Info line per provider, `{Name}: {host}.` or `{Name}: off.`, GitHub's byte for byte (9.2; WP-R waits
  for them).
- 7a's `GitCallBudget`, `ShellDriver.Budget.cs`, `AskPassRunner` and `RecordingCredentialSource` serve
  7b unchanged.
- The classes that start the app through `ShellDriver` with real git are in `VisualCommit.Hosting.Tests`
  (D334) and in 0.4's lagged list, one per package from WP-S to WP-N.

### 1.10 The WP0 list

In the main checkout after L1, every item marked WP0 in 1.1 to 1.9, with no behaviour or picture
changed; the build and the classes it touches pass. Beyond the marks: 7a's `HostingSession` ignores
the new endpoints until WP-S; `NoHostedRemote` replaces 32 hits; `RecordingActions`' and two 7a test
files' names follow `SignInToHostAsync`; the App's reads of `IsOff` that mean GitHub
(`AppCommands.cs:170`, `MainWindowViewModel.Hosting.cs:27`, `AccountsSettingsViewModel.cs:56`) become
`IsOn(HostingProvider.GitHub)`.

The callers WP0's own changes force it to move: `SignInDialogViewModel.cs:188` opens the provider's
`TokenPage(null)` and `RepositoryOperations.PullRequests.cs:125-131` its `PullRequestsPage(repository)`, so
nothing reads `Endpoints` through the interface (1.4); `MainWindowViewModel.Settings.cs:74` sets
`SignIn = () => SignInAsync(HostingProvider.GitHub)` until WP-A; the four test assignments of
`Services.SignInToGitHub` (`PaletteCloneFromGitHubTests.cs:72`, `:104`, `:148`, `SignInEntryTests.cs:100`)
become `SignIn = _ => …`; `PullRequestRefs.HeadOf` is built whole (8.4's rule). Compile fixes in
`HostingSessionStub` and the 7a project's stubs; no fix in `GitHubProviderTests`, `HostingOptionsTests`'
7a cases, `HostingSession`'s A1 and A2 calls or the live project's GitHub files, which keep 7a's
two-argument forms (1.4). The live project is in no solution, so WP0's checks also run
`dotnet build tests/VisualCommit.LiveHostingTests -c Release` (no warnings) before the push that wave 1
branches from.

Tests, in `tests/VisualCommit.Hosting.Tests` (it references App and Core, and holds `HostingRulesTests`)
unless named:
- `HostingProvidersTests`: names, possessives, ids, order; `TokenFormOf` (1.2, D378): each form, 83 and 85
  characters, `AZDO` at index 75 and at 77, `GLPAT-x` and `GHP_x` (null: case), and the fakes' tokens
  `github_pat_FAKE-octo-1` (GitHub), `glpat-FAKE-ada-1` (GitLab) and `azdo_FAKE-Fernhill-Labs-1` (null).
- `HostingTextsProviderTests`: GitHub's word for word, the others' words and numbers.
- `HostingOptionsTests`: cases for the two variables.
- `TokenEntryNamesTests`: the keys `TokenKey.Of` gives for (GitLab, `gitlab.com`, `ada`) and (Azure DevOps,
  `dev.azure.com`, `Fernhill-Labs`): `Entry`, `WindowsComment`, `MacService`, `MacLabel`
  (`VisualCommit: GitLab (ada)`, `VisualCommit: Azure DevOps (fernhill-labs)`), `SecretServiceLabel` and
  the attributes. `MacKeychainStoreTests`: the Azure DevOps key's `security -i` line, its label
  `-l 'VisualCommit: Azure DevOps (fernhill-labs)'` within its quotes. `RealTokenStoreSteps.RunAsync`,
  which CI's three real-store steps run (D270), also saves, reads and deletes one `azure-devops` key on
  `dev.azure.com`, so macOS' spaced label goes through `security -i` once (`ci.yml` unchanged).
- `PullRequestRefsTests` (Git.Tests): 7a's GitHub vectors.

Files that stay the lead's throughout: `AppSession.cs`, `TabServices.cs`, `MainWindowViewModel.cs`,
`IRepositoryActions.cs`, `IGitRepository.cs`, `FakeRepositoryBase.cs`, `IHostingProvider.cs`,
`HostingProviders.cs`, `HostingTexts.Provider.cs`, `Directory.Packages.props`, `VisualCommit.slnx`
(WP-F adds its project's line in its own worktree), `VisualCommit.LiveHostingTests.csproj` (its
comment at stage 1's merge), `.github/workflows/ci.yml`, `CLAUDE.md`, the docs and
`THIRD-PARTY-NOTICES.md`.

**As built** (WP0, `e8a275d`, and its review's fixes, 2026-11-01). Where WP0 differs from the text
above, a later package compares with WP0's commit, not with `5057892`:
- Three 7a tests give the two new variables `none`: `HostingOptionsTests`'
  `None_is_off_without_a_log_line` and
  `A_value_that_is_not_accepted_turns_GitHub_off_and_stays_out_of_the_warning` (through its
  `GitHubAlone` helper) and `HostingSmokeTests`' `None_turns_GitHub_off_without_a_word_in_the_log`.
  Under 1.8 an unset new variable means the service, so GitHub's variable alone at `none` is no
  longer `Off`. Their assertions are 7a's. "No fix in `HostingOptionsTests`' 7a cases" above and
  9.11's "unchanged" are read from WP0's commit.
- `Select(remotes, GitCoverage)` also throws `NotSupportedException` when `HelperUserIsAccounts`
  alone is set (8.2: any member beyond `Origin`), not only for 1.2's three; with `Origin` alone it
  is 7a's.
- With nothing set, `FromEnvironment` carries `dev.azure.com` and gitlab.com with their default
  handlers. 7a's `HostingSession` ignores them until WP-S, so nothing reaches them. From WP-S's merge
  a hand run also needs the two new variables at `none`, and the harnesses start with four hosting
  variables. The lead updates CLAUDE.md's "What to know" by that merge.
- `FromEnvironment`'s warning takes the provider's name from `HostingProviders.DisplayName`;
  GitHub's text is 7a's byte for byte.
- `GitHubProvider.TokenPagePath`, a public constant, holds D295's path, since Hosting does not
  reference the App; `HostingContractsTests` pins it equal to the App's `TokenPage` text.
- `HostingContractsTests` (Hosting.Tests, beyond 1.10's list) pins the moved members, `HostingSession`
  answering the provider members for GitHub alone (another provider: a `Validation` failure, "{Name}
  is turned off.", with `Provider` set, as 9.4 and 9.6 keep), the null session's shims, the records'
  defaults and `RequestRemotes`. It passes to WP-S, whose session it tests.

## 2. WP-F: the fakes' shared core and the harness

### 2.1 Goal and files

The harness every later package tests against. Status step 5; rulings 25 to 28; D363. Wave 1,
`wip/p7b-fake`, after WP0. No product or real-window code; no picture of phases 0 to 7a changes;
`FakeGitHub`'s public API (7a design 2.3) and answers stay, and `FakeGitHubTests` and
`FakeGitHubGitTests` pass unchanged.

- `Testing/Hosting/`: new `FakeHostingService.cs`, `.Core.cs`, `.Model.cs`, `.Handler.cs`; `FakeGitHub*.cs`
  moved onto the core; `TokenTripwire.cs`; `LoopbackOnlyHandler.cs` unchanged; skeletons
  `FakeAzureDevOps.cs`, `FakeGitLab.cs` (2.3), passed to WP-FA and WP-FL at the merge (0.4).
- `Testing/`: `AuthRemoteServer.cs`, `AppLogFile.cs`, `GitIsolation.cs` (WP0's names), `Scenarios.Phase7b.cs`
  with stubs `Scenarios.Phase7b.AzureDevOps.cs` and `.GitLab.cs` (passed likewise), `Headless/ShellDriver.cs`,
  `Headless/ShellDriver.Phase7a.cs` (`HarnessHosting`; 7a's `SignInAsync`, its messages GitHub's word for
  word), new `Headless/ShellDriver.Phase7b.cs`.
- The 7b project and its slnx line after `VisualCommit.slnx:33`; the timing script; the real-window
  csproj's link; 7a's `Phase7aChecks.cs`, `.Support.cs`, `Phase7aSupportTests.cs`.

### 2.2 `AuthRemoteServer`: host mode by URL path (g4 D3.4)

7a's `Serve` (`AuthRemoteServer.cs:433-455`) and two-segment lookup (`:1238-1258`) are untouched; a
`ServeAt` registration is looked up only when 7a's finds nothing, so 7a's answers stay byte for byte.

1.9's members: `ServeAt`, `UrlOf` (`:195`) for a `ServeAt` path too (`http://127.0.0.1:<port>/` + segments
`Uri.EscapeDataString`d, + `.git` with `gitSuffix`), and `AuthRemoteHostOptions` (`:1824-1830`) with
`PushRefusal`, `LoginOfTokenIn` and `FetchRefusal` last.

- `urlPath` is decoded (`Fernhill-Labs/Mobile App/_git/mobile-app`); `folder`, relative to the root,
  holds `HEAD` and `objects`; no segment is empty, `.`, `..`, or holds `\` or a control character.
  `gitSuffix`: `<last>.git` names it too (GitLab; never Azure DevOps, which keeps `.git` in a name, r5
  6.1). A path registered again with its folder changes nothing, with another throws; two paths may share
  a folder (Azure DevOps' `<org>/_git/<repo>` when the repository is named as its project, r5 6.1).
- **Lookup**, after `Claims` (`:833`), which keep the raw path (GitLab's `%2F`, r6 6.2): each segment is
  percent-decoded as strict UTF-8; one that decodes to hold `/` or `\`, to `.` or `..`, or to invalid
  UTF-8 is in no repository (404, no http-backend, no `Errors` line). The longest registration whose
  segments equal the path's first ones without case (r5 6.1, r6 6.1, MEASURED), with a segment after it,
  is the repository. `PATH_INFO` (`:1173`) is `/` + the folder + the decoded rest (http-backend decodes
  nothing, g4 D3.2); records keep the raw path.
- Callbacks, holds and gates take the folder without a final `.git`. A 401 has one `Basic realm` (Azure
  DevOps' other two challenges, g1 G4, MEASURED, are not imitated); Basic needs a non-empty user
  (`:1038`), as both services do (r8 C2, r6 11).

### 2.3 The fakes' core (`FakeHostingService*.cs`)

`FakeServiceCore<TFault, TRequest>` (internal) takes from `FakeGitHub.cs:35-58` and `.Git.cs` what is no
provider's: the lock, the isolated home and `GitRunner`, holds (120 s), faults once per route, the
request log, `Errors` (with the server's), per-repository locks, the server's start, and a dispose that
releases holds first. `FakeGitHub` keeps its tables and types over a `FakeServiceCore<FakeGitHubFault,
FakeGitHubRequest>`, `FakeGitHubHandler` its type and message over `.Handler.cs`'s conversion.
`FakeHostingService` is 1.9's surface over the same core, for the subclasses:

- A request: `RouteOf`, its hold, its fault; `Dropped` answers nothing; else `AnswerAsync`, which
  answers a named fault where its checks put it; `ServedThenDropped` then drops; the record last.
  `Bearer`, `Basic`, `PRIVATE-TOKEN` and a `private_token` parameter give `Authorization` and `Token`
  (two credentials: `Other`). The virtuals reach host mode through `AuthRemoteHostOptions`.
  `FailNext("git:…")` throws `NotSupportedException`; `HoldNext("git:<name>")` is the server's
  `HoldNextRequest` (in process `InvalidOperationException`). The in-process handler's drop says "The
  fake of {DisplayName} closed the connection without an answer.".
- `BareRepositoryAsync`: `<Root>/<urlPath>` (+ `.git`), in place when it holds `HEAD`, else `clone --bare
  --no-hardlinks` of `source`, else `init --bare --initial-branch main`; then `ServeAt`.
- Skeletons: `FakeAzureDevOps : FakeHostingService` with `InProcessPort = 2`, `Endpoints`
  (`AzureDevOpsEndpoints.Local(Port)`), `StartAsync(root, port = 0, fallBackToFreePort = false, ct)`,
  `InProcess(root)`, no route, realm "Azure DevOps"; `FakeGitLab` likewise (3, "GitLab"); 1.9's other
  members are WP-FA's and WP-FL's.

### 2.4 `ShellDriver`, `AppLogFile`

- 1.9's `Start`, `StartWithTabs` (`ShellDriver.cs:146-156`, `:228-253`) keep the fakes as `AzureDevOps` and
  `GitLab`. `HarnessHosting` (`ShellDriver.Phase7a.cs:85-98`) sets each given fake's endpoints and puts a
  `LoopbackOnlyHandler` in all three handler slots.
- 1.9's `SignInAsync(provider, token, organization)`, in `ShellDriver.Phase7b.cs`: a `SignInRequest` with
  7a's wait (`ShellDriver.Phase7a.cs:39-62`), messages naming `HostingProviders.DisplayName`, never the
  token or the organization.
- `AppLogFile.PullRequestsListed` (`AppLogFile.cs:112-117`): `(?<repository>[^/]+(?:/[^/]+)+)`, any depth,
  spaces allowed; 7a's lines match as before.

### 2.5 Ports, the tripwire

- After `AuthRemoteServer.cs:83`: `Phase7bAzureDevOpsPort = 47397`, `Phase7bGitLabPort = 47398`,
  `Phase7bGitHubPort = 47399`, in D265's comment shape; 47396 stays reserved.
- `TokenTripwire` (`TokenTripwire.cs:144-160`) keeps its forms and adds "base64 at offset k", k = 0, 1,
  2: `base64(k zero bytes + token)` less its first quantum when k > 0 and any last partial one, in files
  and calls. Each is found after any prefix whose length leaves k over 3: `:`, `oauth2:`,
  `<organization>:` (g4 D1.6, MEASURED: 7a's miss 1 or 2 over).

### 2.6 The scenario "hosting7b" (`Scenarios.Phase7b.cs`)

`Hosting7bAsync()`: a one-process copy of a template built once per process (`TemplateAsync("hosting7b",
…)`, about 70 git processes), returning `mobile-app/`; commits a minute apart by 7a's Ada Lovelace, Octo
Cat and Test Author; made-up names only. Bare folders by 2.3's rule:

- `azure-devops/Fernhill-Labs/Mobile App/_git/`: `mobile-app` (main 12:00-12:02; `feature/sign-in`,
  `docs/guide`, `topic/no-request` 12:03-12:05), `mobile-app-fork` (+ `fix-typo` 12:11), `sandbox` (empty,
  so no `defaultBranch`, r5 0.7, MEASURED), `customer-onboarding-and-identity-verification-service` (empty);
  `azure-devops/harbor-co/Docks/_git/manifest` (main 12:40).
- `gitlab/`: `northfield/platform/web-app.git` (main 12:20-12:22; `feature/search`, `docs/readme`,
  `topic/no-request` 12:23-12:25), `ada/web-app.git` (+ `fix-typo` 12:31),
  `northfield/platform/services/billing/invoice-generation-and-reconciliation.git` (empty).
- `github/octo/web-app.git` (web-app's main).

Clones on `main`, remotes written last on 1.8's ports: `mobile-app/` origin
`http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app` (the service's
`<organization>@` form, r5 0.5, MEASURED; `%20` ASSUMED, r5 6.1), `feature/sign-in` tracking, local-only
`local-work`; `manifest/` origin `http://harbor-co@127.0.0.1:47397/harbor-co/Docks/_git/manifest`
(D356); `web-app/` origin `http://127.0.0.1:47398/northfield/platform/web-app.git`, upstream
`http://127.0.0.1:47399/octo/web-app.git` (ruling 5), `feature/search` tracking, local-only `local-work`
12:26 "Local change" (Test Author), in no bare folder (13.7 and 13.8 push it first).

`ServeHosting7bAsync(TempRepo copy, int azureDevOpsPort = 47397, int gitLabPort = 47398, int gitHubPort =
47399, bool ownsCopy = false, CancellationToken ct = default)` → `ServedHosting7b : IAsyncDisposable`
(1.9's members; `Organization` "Fernhill-Labs", `OtherOrganization` "harbor-co"): the fakes with
fallback; GitHub's octo 1001, `github_pat_FAKE-octo-1`, public `octo/web-app`; then
`SeedHosting7bAzureDevOpsAsync` and `SeedHosting7bGitLabAsync` (`Scenarios.Phase7b.AzureDevOps.cs`,
`.GitLab.cs`), stubs returning the tokens, which WP-FA and WP-FL fill with the halves 3.5 and 4.5 list
(`!11` to `!13` and `!5`; `!21` to `!23`). It starts every fake on a port, none in process; one on any
port other than its fixed one (it fell back, or was asked for port 0, as in Hosting.Tests) gets its
clones' remotes rewritten with `git remote set-url`, keeping `<organization>@` and `%20` (D265, 7a's
rule).

### 2.7 The project, the script, the link

- `VisualCommit.VisualTests.Phase7b`: csproj and `AssemblyInfo.cs` as 7a's (1.1); `Phase7bChecks.cs`
  ("what is where", `public abstract partial class Phase7bChecks`); `Phase7bChecks.Support.cs`, 7a's with
  7b's names (`Shot`, `phase-7b/timings/`, the assertions, 0.3's picture pattern), plus `ServeAsync()` and
  `OpenAsync(data, served, repository = "mobile-app", HostingProvider[]? signIn = null, tokens, width,
  height, theme, systemDark)`: one tab, the three fakes, a sign-in to each provider named (default the
  repository's; `[]` none), then `WaitForWatchersQuietAsync`; `Phase7bExpectations.Common.cs` (the pass
  compiles it: BCL, Avalonia, Xunit, Core, Git, Testing only): `Phase = "7b"`, sizes, the ports,
  `WithPorts(text, served)`, `WebBase(port)`, the scenario's names, the palette, and
  `PrepareRequestsAsync(TempRepo copy, HostingProvider provider)`: `git branch --track topic/no-request
  origin/topic/no-request` in the provider's clone (`web-app/`, `copy.OpenSibling("web-app")`, for
  GitLab; `copy`'s `mobile-app/` for Azure DevOps), the one prepared state that part P's R methods,
  part N's checks and WP-R's "Requests and create" share (12.8, 13.8, 14.2).
- `time-scripted-check.sh:37-43`: `7b)` before `7a)`, usage and default `7b` in the project's commit;
  the real-window csproj after `:70`: 0.3's link in 7a's comment shape.

### 2.8 Tests (the builder runs these classes only, D245)

- `AuthRemoteServerTests` (Git.Tests): the pin (`:415-419`) adds 47397 to 47399; a four-level path with a
  space cloned through `%20` and in another case; `gitSuffix` both ways; the longer of two nested paths;
  `%2F`, `%5C`, `%2E%2E`, `.`, `%C3%28` each 404, `Errors` empty; `UrlOf`; `PushRefusal`; `FetchRefusal`'s
  403 before http-backend; `LoginOfTokenIn` refusing (401, `GivenUserName` kept); one folder at two paths
  named alike in callbacks; `ServeAt`'s refusals.
- `GitIsolationTests`: a started process given ruling 27's names keeps none, the URL variables `none`.
- `FakeHostingServiceTests` (Hosting.Tests, a test subclass): holds, a fault once, both drops, `Revoke` on
  API and git, each `FakeAuthorizationForm`, git holds, `GitAsync`'s environment, dispose.
- `TokenTripwireFormsTests` (Hosting.Tests): fake tokens after `x-access-token:`, `:`, `ab:`, `abcd:`,
  `oauth2:` and alone, with and without a trailing newline: found, named by offset.
- `Phase7bSupportTests`: ruling 27's names pinned; a start without fakes off; the three on port 0, their
  endpoints and the tripwire's refusal of `https://example.com/`; `hosting:` with a new fake and an
  in-process one refused; 2.6's layout, URLs and authors, `mobile-app/`'s and `web-app/`'s `local-work`
  in no bare folder;
  every remote rewritten on port 0; the log pattern; `WithPorts`; the picture pattern.
- `Phase7bExpectationTests.Common.cs` (7b project; as 7a's `Phase7aExpectationTests.N.cs:42` pins
  `PrepareAsync`): after `PrepareRequestsAsync` on `Scenarios.Hosting7bAsync()`,
  `branch.topic/no-request.remote` is `origin` and `branch.topic/no-request.merge`
  `refs/heads/topic/no-request`, in `web-app/` for GitLab and `mobile-app/` for Azure DevOps.

### 2.9 At the merge; done when

The lead changes CLAUDE.md's rows: "One phase's scripted checks" and "Scripted visual walk-through
only" gain `tests/VisualCommit.VisualTests.Phase7b`; "Time scripted checks alone" defaults to `7b`; the
lagged-list row points at this design's 0.4; the note on running the app without the real settings
gains `VISUALCOMMIT_AZURE_DEVOPS_URL=none` and `VISUALCOMMIT_GITLAB_URL=none`, every test process
starting with all four; "What to know" says 7b's walk-through binds 47397 to 47399 (1.8; 14.5 adds the
real-window part). `docs/test-reports/TEMPLATE.md`'s "Live command" section is written per provider
(`artifacts/live/<provider>/run.txt`); the live command's rows are stage 1's (15.9). The lead runs 0.4's
lagged list; the whole default run follows the merge that closes wave 1, whichever of WP-F, WP-H and
WP-C is last (0.4).

Done when these six classes, `FakeGitHubTests`, `FakeGitHubGitTests` and `Phase7aSupportTests` pass, the
real-window project builds, the 7b project is in the solution, the script defaults to `7b` and refuses
`7`, no earlier picture changed, and CI's five jobs are green, each Test step under four fifths of its
limit (0.6).

**As built** (WP-F, `3a890f0` to its last commit, 2026-11-01). Where the build differs from the text
above or settles what it left open:
- `AuthRemoteServer`: 7a's lookup is `TwoLevelRepositoryOf`, unchanged and first; `ServeAt`'s are a
  list of their own. A segment is also in no repository when empty, beyond ASCII or holding `%`
  without two hex digits, and so is one after the registration's that needs decoding at all:
  `PATH_INFO`'s rest is the raw one, which the push guard reads too. `FolderOf` and `UrlOf` find a
  `ServeAt` repository by folder or path without case; `ServedRepositories` lists it. `FetchRefusal`
  is asked of every signed-in request that is no push; `LoginOfTokenIn` only for a path in a served
  repository, `LoginOfToken` elsewhere (a known token on an unserved path: 404, not 3.5's 401). A
  path registered again with the other `gitSuffix` throws.
- The core is `FakeHostingService.Core.cs`. Its dispose also releases a request waiting at a hold
  (over TCP the stop may still cut it off; 7a's left it waiting in process). `FakeHostingService`
  adds a protected `ServeAt(urlPath, folder, gitSuffix)` (WP-FA's `{o}/_git/{r}`) and refuses a
  second `git:` hold until the first is disposed (`FakeGitHub`'s stays shared, as in 7a). `Dropped`
  is recorded with status 0; `FakeServiceCall.Fault` is never one of the two drops; a
  `private_token` is recorded as `***`. An `Authorization` with a comma (two headers), an empty token
  or another scheme is `Other`. The bare rule takes a folder holding `HEAD` before `source`.
- The tripwire skips an offset form under 8 characters; the log pattern's segments exclude `\r`, `\n`.
- `ShellDriver`: 7a's `SignInAsync(string, …)` is untouched; the provider form keeps the token and
  the organization out of a failure's text. A new fake with `hosting:` throws.
- Scenario: mobile-app's `local-work` is 12:06; `Phase7bSupportTests` pins subjects, authors and
  times. The stubs seed nothing and return `azdo_FAKE-Fernhill-Labs-1`, `glpat-FAKE-ada-1` and
  `glpat-FAKE-ada-2`, which `Phase7bExpectations`' constants and `Phase7bSupportTests` pin: WP-FA and
  WP-FL keep them or change the constants, and run `Phase7bSupportTests` before their merges.
- 7b support: `OpenAsync` always waits for the watchers; its default sign-in throws until WP-S.
  `Phase7bExpectationTests` is partial.
- 7a paths touched, keeping 7a's answers: `HarnessHosting`, `Start`/`StartWithTabs`, `FakeGitHub*`,
  `AuthRemoteServer`'s lookup, `HostAccess` and `PATH_INFO`, the tripwire, the log pattern.

## 3. WP-FA: the fake of Azure DevOps

### 3.1 Goal and files

Azure DevOps for WP-AZ, WP-S, the scenario and the walk-through (D346): API, pages and git on one
origin, `http://127.0.0.1:<port>/{o}/…`, on 1.9's core and host mode, the same routes in process; wave
2, branch `wip/p7b-fake-azure`. Written from r5 and g1, not from the client: shapes and texts copied
word for word from the sections cited, each answer's mark in its doc comment (as
`FakeGitHub.Model.cs:259-343`); stricter only as D370 says; no `<organization>.visualstudio.com`. In `tests/VisualCommit.Testing/Hosting/`:
`FakeAzureDevOps.cs` (WP-F's skeleton, 0.4), and new `.Router.cs` (3.7 in its header comment),
`.Model.cs` (3.2's types, `FakeAzureDevOpsFault`), `.Json.cs`, `.Git.cs`; WP-F's stub
`Testing/Scenarios.Phase7b.AzureDevOps.cs` (3.5). Test classes `FakeAzureDevOpsTests`,
`FakeAzureDevOpsGitTests` (Hosting.Tests).

### 3.2 Members

1.9's members (`FakeAzureDevOps`), on 1.9's core; `AddToken` gives `azdo_FAKE-<organization>-<n>`.

A user has a fixed GUID and `<name>@example.com`. Names follow r5 6.1; lookups ignore case, answers
use the stored spelling (MEASURED r5 2.1); PR ids run per organization (ASSUMED g1 G0.13). Settable:
a user's `CustomDisplayName` (the display name until set, null leaves it out; WP-LF) and `MetaTypeId`
(0; 255 for WP-AZ's test, g1 G0.4), a project's `Members`
and a repository's `Contributors` (empty: everyone), `IsDisabled`, a PR's `Status` and `Conflicts`.
`AddRepositoryAsync` is 7a's (7a design 2.3) at `<root>/{o}/{p}/_git/{r}`.

### 3.3 Routes and checks

Below `Endpoints.WebBase`, names without case (MEASURED); in keys `…` is `{o}/{p}`, route keys lower-case.
Claimed: a path with an `_apis` segment, and every GET but git's `…/info/refs`. In process any host is
served by path and git's paths get 404, as 7a's.

| Key | Request | Answer |
|---|---|---|
| `connection:{o}` | `GET {o}/_apis/connectionData` | the caller's identity (3.4) |
| `areas` | `GET _apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf?organizationName={o}`, or under `{o}/` | `{"id","name":"core","locationUrl":"<web>{o, stored}/"}`; root form, unknown: 404 TF400677 (MEASURED g1 G4.2); no credential check (with a token ASSUMED) |
| `repos:{o}[/{p}]` | `GET {o}[/{p}]/_apis/git/repositories` | `{"value","count"}`, readable projects, unpaged, `$top` ignored (MEASURED); unknown project 404 TF200016 (ASSUMED) |
| `repo:…/{r}` | `GET …/repositories/{name or id}` | with `_links`; 404 TF401019 (MEASURED) |
| `pulls:…/{r}` | `GET …/pullrequests` | `searchCriteria.status` (`active` default, `abandoned`, `completed`, `all`; others `active`, ASSUMED), `$skip`, `$top` (absent 101, READ community; capped at `MaxPerPage`, ASSUMED); id descending, `count` the page's (MEASURED) |
| `pull:…/{n}` | `GET …/pullrequests/{n}`, `{o}/{p}/_apis/git/pullrequests/{n}` | with `artifactId`; 404 TF401180 (MEASURED) |
| `create:…/{r}` | `POST …/pullrequests` | below |
| `update:…/{n}` | `PATCH …/pullrequests/{n}` | `status`, `title`, `description` (READ); others 400 `InvalidArgumentValueException` (READ g1 G1.1, status ASSUMED) |
| `refs:…/{r}` | `GET …/refs?filter=`, `POST …/refs` | GET MEASURED; POST deletes and creates, r5 4's results at 200 (MEASURED 2026-11-05 for succeeded rows; `staleOldObjectId` and the other statuses at 200 ASSUMED) |
| `page` | any other GET | 200 `text/html`, any path (MEASURED g1 G4.3) |

**Checks**, in order (ASSUMED but for 1):
1. Unknown organization: 404 `text/plain` "The resource cannot be found.", `X-TFS-ServiceError`, no
   `ActivityId` (MEASURED r5 1.3 without a credential; with a token the same, ASSUMED until D374's
   probe, 3.7).
2. `api-version`, r5 1.4's words (MEASURED): absent, 400 `VssInvalidApiResourceVersionException` in
   the fake's words (D370); malformed, that typeKey; above 7.2, `VssVersionOutOfRangeException`;
   no `-preview` on `connection` and `areas`, or `7.2` elsewhere, `VssInvalidPreviewVersionException`;
   `7.1-preview.2` on a git route, 404 "Page not found".
3. A pending fault.
4. Credentials, `Authorization: Basic` alone, its user ignored (READ g1 G1.0):
   - none, another scheme (`Bearer` too) or a password not PAT-shaped (`azdo_FAKE-…`, 84
     characters holding `AZDO`, or the older 52 of `a`-`z` and `2`-`7`, ASSUMED from r5 0.6 and 5's
     "base32-looking"): no credential (READ
     g1 G0.2): Anonymous for a public project's reads and for `connection` in an organization with
     `publicProjects`; else 302 to
     `https://spsprodfake.vssps.visualstudio.com/_signin?…`, or with `X-TFS-FedAuthRedirect: Suppress`
     401 TF400813 naming `aaaaaaaa-…` (MEASURED);
   - PAT-shaped, unknown, revoked or another organization's: 401, empty body, one `WWW-Authenticate:
     Basic realm="https://tfsprodfake.visualstudio.com/"` (READ g1 G2; the last ASSUMED; the realm's
     form, without `.app.`, MEASURED 2026-11-05, WP-LF's `TokenRealm`);
   - expired: 401 `AccessCheckException` "Access Denied: The Personal Access Token used has expired.",
     led by a UTF-8 BOM (READ g1 G2; on dev.azure.com ASSUMED).
5. The route: a hidden project or repository 404 TF401019; a read-only token's write 401 TF400813
   naming the user (ASSUMED g1 G2); a non-contributor's create 403 `GitNeedsPermissionException`
   TF401027 (READ text, 403 ASSUMED).

**Create** (`sourceRefName`, `targetRefName`, `title`, `description`, `isDraft`,
`forkSource.repository.id`): no title, 400 `InvalidArgumentValueException` (ASSUMED); a ref outside
`refs/heads/` or a missing branch, 400 `GitPullRequestCannotBeActivated` TF401398 (READ); an active one
with that source and target, 409 `GitPullRequestExistsException` TF401179 (READ, a pasted status line);
else `CreateStatus` (ASSUMED 200 or 201, g1 G0.12), `status: active`, `mergeStatus: queued`, the
token's user as `createdBy`; nothing ahead is created (ASSUMED).

### 3.4 Shapes, headers, faults

camelCase; dates `HarnessTime`, 7 fractional digits and `Z`. Identity r5 1.2's for Anonymous (the
instance and deployment ids the fake's own) and, for a member, g1 G3.2 sample 1's member names and
nesting, with the fake user's own GUID, name and a made-up example.com address (as `Account.$value`);
repositories r5 2.1's, `remoteUrl` `http://{o}@127.0.0.1:<port>/{o}/{p}/_git/{r}` (a space `%20`, ASSUMED); PRs r5 3.1's, `createdBy.url`
on `spsprodfake.vssps.visualstudio.com`; errors r5 1.4's with `$id`. What a client must survive
(MEASURED unless marked): `properties.Account` with a capital `A` (g4 D4 §1); `defaultBranch` absent
when empty; `isFork` absent unless true; `parentRepository` in `repo:` only (READ; the list's ASSUMED);
lists in creation order (name order rests on two rows, g1 G4.4); a list's
`repository.project.lastUpdateTime` `"0001-01-01T00:00:00"` (g4 D4 §3); descriptions cut to 400
characters in lists; no web address on a PR; a fork's `sourceRefName` `refs/pull/<n>/source` and `forkSource {name, objectId: null, repository}`
(READ g1 G2.6); `Content-Type` with `api-version=<served>`; `ActivityId` = `X-TFS-Session` =
`X-VSS-E2EID`; no `ETag`, `Link` or continuation.

**Faults** (`FakeAzureDevOpsFault`, once, then normal; r5 8.4, g1 G2.2): `InvalidToken`,
`Unauthorized`, `Expired` (check 4's); `SignInRedirect` (a 302 to the fake's own `/_signin?…` despite
Suppress, ASSUMED); `NonAuthoritative`
(203 `text/html`, defensive, g1 G0.1); `AnonymousIdentity` (with a token, g1 G0.3); `Throttled` (429
TF400733, `Retry-After`, `X-RateLimit-*`, READ; typeKey ASSUMED); `Delayed` (`X-RateLimit-Delay`, READ);
`VersionRefused`; `NotFound`; `RouteMissing`; `OrganizationMissing`; `ServerError` (500 TF400898,
status ASSUMED); `BadGateway` (503 HTML, ASSUMED); `Malformed`; `CreatedThenDropped` (7a's).

### 3.5 Git, refs and the scenario's half

- Host mode serves `{o}/{p}/_git/{r}`, `gitSuffix: false` (no `.git` stripped), and `{o}/_git/{r}` when
  `{r}` and `{p}` match (MEASURED r5 6.1). `LoginOfToken` gives `{o}/<user id>` for a known
  unexpired token under any non-empty user, `{o}` and `x-access-token` included (READ r5 0.5);
  `IsPublic` the project's; `MayPush` a contributor with `codeWrite`, else 403 TF401027 for
  `GenericContribute` (READ text, ASSUMED on git); check 4's realm, for git's agent (MEASURED g1 G0.7;
  the 302 to other agents is not imitated). `LoginOfToken(repository, token)` refuses a token of
  another organization than the repository's first segment, 401 as on the API (ASSUMED).
- **Ref sync** (D267's pattern; before `pulls`, `pull`, `create` and git's ref listing): an active PR
  without `Conflicts` gets `refs/pull/<n>/merge` when a tip moved, `git commit-tree <source>^{tree} -p
  <target> -p <source> -m "Merge pull request <n> from <source> into <target>"` (parents and message
  MEASURED r5 3.3; the tree nothing reads, D361), the creator at `HarnessTime` as author and
  committer; a fork's source fetched first into `refs/fake/source/<n>`. One `update-ref --stdin` writes
  them and deletes `refs/fake/source/*` and the refs of abandoned, completed and conflicted PRs
  (completed MEASURED). Never `/head` (MEASURED) or `/source` (ASSUMED). `mergeStatus` is `queued` until
  the first sync after a create (READ sample). `HoldNext("git:{o}/{p}/_git/{r}")`, the folder name as
  1.9 gives it and matched without case, holds the repository's next git request through host mode.
- **Cost**: each merge commit is one git process, about 0.1 s alone (MEASURED at the design's review:
  301 `commit-tree` calls in 31.4 s with 2.56 on the laptop); it runs inside the request's 30 s (5.2)
  and slower under the default run's load (D237). So no test or check gives one fake repository more
  than 20 active PRs without `Conflicts`; larger lists are scripted (6.8's 301 rows), or made with
  `Conflicts` set (no merge commit, only a delete line, which `update-ref --stdin` accepts for a
  missing ref). WP-FA's choice: one sync may write all its merge commits with one `git hash-object -t
  commit -w --stdin-paths` over files holding the texts `commit-tree` would write (MEASURED at the
  review: the same SHAs; 301 objects in 0.5 s), the trees from one `git rev-parse <source>^{tree} …`.
- **The scenario's half** (`SeedHosting7bAzureDevOpsAsync`, WP-F's stub, 1.9): organization
  `Fernhill-Labs` with project `Mobile App` and users Ada Lovelace and Octo Cat (the scenario's authors),
  Ada's token returned; `harbor-co` with project `Docks` and a user of its own, neither Ada nor Octo; WP-F's bare folders served in
  place by `AddRepositoryAsync` (`mobile-app`, `mobile-app-fork` as its fork, `sandbox` and
  `customer-onboarding-and-identity-verification-service` empty, `manifest`); mobile-app's !11 "Add the
  sign-in page" (Ada, `feature/sign-in`), !12 "Write the guide" (Octo, `docs/guide`, draft), !13 "Fix a
  typo in the greeting" (Ada, from the fork's `fix-typo`); manifest's !5 (harbor-co's user). `FakeAzureDevOpsGitTests` pins
  it through `ServeHosting7bAsync` on port 0.

### 3.6 Tests (the builder runs these classes only, D245)

- `FakeAzureDevOpsTests` (in process): every route, check, 3.4 item and fault once, a fault then the
  normal answer; Anonymous with no credential, a short password and `Bearer`; the empty 401 for
  unknown, revoked and other-organization tokens; `areas` both ways; `SignInRedirect` on the fake's own
  origin and the no-Suppress 302 off it; a 401-character description; create at 201 and 200, `queued`
  then `succeeded`; `HoldNext`; no `Requests` entry holds a token.
- `FakeAzureDevOpsGitTests` (port 0, real git): clones with `x-access-token` and `{o}@`
  (`GivenUserName`), at `{o}/_git/{r}`, with a space through `%20`; `.git` not served; `ls-remote` shows
  `refs/pull/<n>/merge` and no `/head`, whose fetch fails "couldn't find remote ref"; the merge ref's
  parents, and its removal on abandon; revoked and expired 401; a non-contributor's push 403; a public
  repository cloned anonymously, its push asking for credentials; another organization's token 401;
  the scenario's half (3.5).

### 3.7 ASSUMED answers for the live command (D342)

WP-LIVE's probes (r5 9, g1 G6) settle, before part A's checks (0.4), each mark MEASURED once the fix
package has copied it: a member's connectionData, a Code-only token; a made-up short value and a
made-up 84-character PAT-shaped one, each with and without Suppress, to the sandbox organization's
connectionData and the sandbox repository's `info/refs` (D373; check 4's short password and unknown
PAT-shaped value, and git's 401); check 1 with a token (D374's probe of a made-up organization, the
token sent only after its read without a credential answers 404); `areas` with a token; create (status, `queued`, the merge ref,
`isDraft`); the 409 and 400, no title, nothing ahead; abandon; the refs delete; `createdBy`; the
sandbox's row (`ListProject`); any BOM. Kept READ or ASSUMED: a read-only write and the expired body,
which need a token D364 does not make (15.7, 16.1); with no fork in the sandbox (D375),
`parentRepository`, the fork shapes and `/source`; outside D352 and D374, another organization's token, an unknown project,
TF401027's statuses, 429's typeKey, 5xx bodies, `$top`'s default and cap.

### 3.8 Done when

Both classes pass; each answer's mark is in its doc comment and 3.7 heads `.Router.cs`; no 7a test or
picture changes; no token, address or organization of the owner's in the files; no value from the
research's captures.

**As built** (WP-FA, `70ff3d4` to its last commit, 2026-11-01). Where the build differs from the text
above or settles what it left open:
- Beyond 1.9: `User(organization, displayName)` and `TfsRealm`, which replaces 2.3's skeleton realm
  "Azure DevOps" on every 401, git's included (g1 G4.1's measured form for git's first request, with
  no credential); since WP-LF the API's empty 401 to a refused PAT-shaped value names `TokenRealm`, the
  form without `.app.` measured on 2026-11-05, which git's refusal of a credential carried too, kept
  as a difference: `AuthRemoteServer` gives every git 401 one realm and the helper reads no `wwwauth[]`.
  A user's address is its words joined by `.` at example.com. A read-only token's login is
  `{o}/<user id>#read`, since `MayPush` gets the login alone.
- Keys: decoded names, lower-cased; a project (as the fake's own `url` names it) or repository named by
  GUID keys by its name; `pull:` and `update:` are `{o}/{p}/{n}`; both refs methods share `refs:`. `api-version` from the query or `Accept`, ASCII digits within an int. An
  `_apis` path no route serves is 404 "Page not found" after check 1; pages pass no check but a fault.
- Check 4: `areas` checks nothing; no credential reads only a public project's `repos:{o}/{p}`, `repo`,
  `pulls`, `pull` and `refs` GET; `AnonymousIdentity` off `connection` is no credential. `Unauthorized`
  names the token's user, else Anonymous; `NotFound` the route's repository, else project, else
  organization. A hidden project's list is 404 TF200016, as an unknown one's; a create's unknown or
  other organization's `forkSource.repository.id` 404 TF401019.
- `$top`: `MaxPerPage` caps one sent; none gives 101 rows whatever `MaxPerPage` is.
- Shapes: `ActivityId` from a counter; `size` the objects' bytes from the folder's listing; refs'
  `creator` the TFS identity; `closedDate` on a closed one; a create's `lastMerge…Commit`s the tips at
  the create. A PR alone (`pull:`, create, update) has its `repository` whole but `defaultBranch` and
  ends `url, _links, supportsIterations, artifactId` (r5's single GET); a list keeps the trimmed form.
- Writes: update takes `status`, `title`, `description`; refs POST deletes and creates under `refs/heads/` and
  `refs/tags/` (`succeeded`, `staleOldObjectId`, `unresolvableToCommit`, `refNameConflict` for a name
  that is a ref's folder or has one as its own or differs from one only in case, or has a folder
  that differs from one of a ref's only in case, names and folders compared without case so that
  every file system answers alike, else `invalidRefName`; WP-LF),
  an update that moves a ref 400;
  a non-contributor's update and refs POST 403 TF401027.
- Git: git's callbacks find a repository `x.git` as `x`; `x` beside it is refused. Sync: one
  `commit-tree` per merge, not the hash-object batch; also before `refs` GET; a missing branch keeps
  its ref. A disabled repository is not public to git; no `FetchRefusal`.
- Scenario: harbor-co's user is Test Author, without a token; !5 is from `update-manifest`, which
  WP-F's template lacks, so it never gets a merge ref. Ada's token stays `azdo_FAKE-Fernhill-Labs-1`.
  No file outside 3.1's list changed.

## 4. WP-FL: the fake of GitLab

### 4.1 Goal and files

gitlab.com for WP-GL, WP-S, the scenario and the walk-through (D346): `api/v4/`, pages and git on
`http://127.0.0.1:<port>/`, on WP-F's core and host mode (2.2, 2.3), the same routes in process; wave
2, branch `wip/p7b-fake-gitlab`. Written from r6 and g2 as 3.1 says for r5 (texts word for word, marks
in doc comments, stricter only as D370 says). In `tests/VisualCommit.Testing/Hosting/`:
`FakeGitLab.cs` (WP-F's skeleton, 0.4), and new `.Router.cs` (4.7 in its header comment), `.Model.cs`
(4.2's types, `FakeGitLabFault`), `.Json.cs`, `.Git.cs`; WP-F's stub `Testing/Scenarios.Phase7b.GitLab.cs`
(4.5). Test classes `FakeGitLabTests`, `FakeGitLabGitTests` (Hosting.Tests).

### 4.2 Members

1.9's members (`FakeGitLab`, `FakeGitLabTokenKind`), on 1.9's core, with `FakeAzureDevOps`' `InProcessPort` (3),
`StartAsync`, `InProcess` and `Endpoints` (`GitLabEndpoints`); `AddToken` gives `glpat-FAKE-<username>-<n>`,
`AddListedProjects` `<ns>/listed-0001` on.

`permissions`: legacy scopes (default `api`) or fine-grained labels (default r6 2.2's), on every
project the user reaches ("Only specific…" not modelled, as in 7a). Users have `<username>@example.com`;
a project (`Id` from 5001) under a username is that user's (Owner, 50), groups nest to 20 levels (READ
r6 0.3); settable `IsArchived`, `MergeRequestsEnabled`. A merge request (`Id` from 7001, `Iid` per
target) takes `Draft` from its title.

### 4.3 Routes and checks

Below `Endpoints.ApiBase`; `:id` a number or the path escaped (`%2F` or `%2f`), without case, a trailing
`/` allowed (MEASURED r6 3.2); bodies form or JSON. Claimed: `api/v4/…`, a `.git`-less `info/refs`
(4.5) and every GET but git's; in process as 3.3 says.

| Key | Request | Answer |
|---|---|---|
| `user`, `self` | `GET user`, `GET personal_access_tokens/self` | the token's user; the token (READ r6 1.3, 1.4) |
| `projects` | `GET projects` | `membership=true` the user's, through groups too (READ g2 G5.4), else the public and the user's; `archived`, `simple` (20 keys, MEASURED), `order_by`, `sort`; `per_page` 20, clamped at `MaxPerPage` (MEASURED) |
| `project:<path>` | `GET projects/:id` | `.git` or double escaping 404 "404 Project Not Found"; an unescaped `/` 404 E "404 Not Found" (MEASURED) |
| `mrs:<path>` | `GET …/merge_requests` | `state` (`all` default, READ; an unknown one 400 E, MEASURED), `source_branch`, `target_branch`; `created_at` descending (MEASURED) |
| `mr:<path>/<iid>` | `GET …/merge_requests/:iid` | unknown 404 "404 Not found" (MEASURED g2 G4); not digits 400 E (MEASURED) |
| `create:<path>` | `POST …/merge_requests` | below |
| `update:…`, `delete:…` | `PUT`, `DELETE …/merge_requests/:iid` | `state_event` `close`, `reopen`; DELETE 204 for an Owner alone, else 403; unknown 404 "404 Not found" (READ g2 G1.5, G1.6) |
| `branches:<path>` | `GET …/repository/branches?search=^…`, `DELETE …/branches/:branch` | 204; 404 "404 Branch Not Found"; 403 without push (READ g2 G1.7) |
| `page` | any other GET | 200 `text/html`; a moved project's 301 to its new path (MEASURED g2 G4) |

**Checks**, in order (READ g2 G2.1):
1. A pending fault.
2. The token from `Authorization: Bearer` alone; `PRIVATE-TOKEN`, `private_token` and Basic are none
   (D370).
3. None: 401 A "401 Unauthorized" without `WWW-Authenticate`, but `projects` (with `membership=true`
   empty) and a public project's reads answer anonymously (MEASURED); unknown: that 401 (READ); expired or revoked: 401 F
   `invalid_token` with g2 G2.2's texts and `WWW-Authenticate` (READ).
4. A legacy token without the route's scope (`read_user`, `read_api` or `api` on `user`; `read_api` or
   `api` on other reads; `api` on writes): 403 F
   `insufficient_scope`, `scope` a space-separated list holding `api`, in body and header (READ g2 G2.3;
   the lists ASSUMED, g2 G2.3's three).
5. A fine-grained token without the route's permission (r6 2.2): 403 F `insufficient_granular_scope`,
   g2 G2.4's text naming `user` or `project` and the label, no `WWW-Authenticate` (READ), but a public
   project's reads pass (READ); a project hidden from the user 404 A "404 Not Found" (READ).
6. The route: unknown or hidden 404 A "404 Project Not Found" (MEASURED); an old path's GET 301
   `text/plain` "This resource has been moved permanently to <api>projects/<id>[/rest]" with that
   `Location` and the query (MEASURED r6 3.2; its order ASSUMED), any other method 405 A "Non GET
   methods are not allowed for moved projects" (READ).

**Create** (READ g2 G1.1; (a)'s place ASSUMED): (a) a missing `source_branch`, `target_branch` or
`title`, 400 E "<name> is missing", joined by ", "; (b) below Developer (30) on the source, or its
merge requests off, 403 A (READ g2 G1.2, not 422); (c) an unknown `target_project_id`, 404 A "404 Not
found"; (d) a target unreadable or with merge requests off, 403 A; (e) 422 B "You can't use same
project/branch for source and target" or "Source project is not a fork of the target project" (that
body ASSUMED); (f) 409 B "Another open merge request already exists for this source branch: !<iid>"
for an open one with the same projects and branches; (g) 400 C `{"source_branch":["does not exist"]}`,
then `target_branch`; (h) 201: `iid` next in the target, the title as sent, `draft` when it starts
`Draft:`, `[Draft]` or `(Draft)` without case (READ r6 5.1), the description stored as sent (the
service runs its quick actions, READ g2 G1.4: no test may rely on either), nothing ahead allowed,
the head ref written.

### 4.4 Shapes, headers, faults

snake_case; `Content-Type: application/json`, no charset; dates with milliseconds and `Z` (MEASURED).
Users r6 1.3's; projects r6 3.1's (simple MEASURED, the rest READ), `http_url_to_repo`
`<web><path>.git`, `ssh_url_to_repo` `git@127.0.0.1:<path>.git`, an empty repository `default_branch:
"main"`, `empty_repo: true` (ASSUMED); merge requests r6 4.1's (MEASURED) with `references` `!<iid>`,
`web_url` `<web><path>/-/merge_requests/<iid>`, `author.public_email` (g4 D4 §5), no source path;
tokens r6 1.4's, fine-grained `scopes: ["granular"]` (ASSUMED); errors r6 10's shapes A to G. Headers
(MEASURED r6 7, 8, 13.1): `ratelimit-*` on every API answer (`throttle_authenticated_api` 2000 with a
token, READ); `x-request-id`; a weak `ETag` on a 200 GET, its 304 counted; lists' `Link` and `x-*`
page headers, `x-next-page` "" on the last; past the end `[]`.

**Faults** (`FakeGitLabFault`, once, then normal): `Unauthorized`; `Expired`, `Revoked`;
`InsufficientScope`, `InsufficientGranular` (the route's list or label); `Forbidden`; `NotFound`;
`RateLimited` (429 G "Retry later\n", `Retry-After: 60`, `RateLimit-ResetTime`, the `ratelimit-*`) and
`EndpointRateLimited` (429 D "This endpoint has been requested too many times. Try again later.",
`Retry-After: 600`, `RateLimit-Name` alone), both READ r6 8; `Timeout` (408 D, READ); `ServerError`
(500 A), `BadGateway` (502 HTML), both ASSUMED; `Malformed`; `CreatedThenDropped` (7a's); `MovedTo(Uri)`.

### 4.5 Git, refs and the scenario's half

- Host mode serves each project at its path with `gitSuffix: true`, and a moved one's old path too:
  fetch and push 200, no redirect (MEASURED r6 6.1, push READ g2 G3). A claimed `GET <path>/info/refs`
  without `.git` answers 301 to `/<path>.git/info/refs?<query>` (MEASURED r6 6.1), which git follows
  (MEASURED r7 6). Paths match without case, as gitlab.com's do (MEASURED; host mode, 1.9).
- `LoginOfToken` the username for a live token under any non-empty user (READ r6 11), else 401 with
  realm `GitLab` (MEASURED; expired and revoked READ g2 G3); `IsPublic` `public` alone; `MayPush`
  Developer or above with `api`, `write_repository` or Code: Push, else 403 `text/plain` "You are not
  allowed to upload code." or g2 G3's fine-grained text; a fetch with none of `api`, `read_api`,
  `read_repository`, `write_repository` or Code: Download 403 "You are not allowed to download code."
  or its fine-grained text (READ g2 G3), through `FetchRefusal` (1.9).
- **Ref sync** (D267's, GitLab's ref; before `mrs`, `mr`, `create` and git's ref listing): each merge
  request's `refs/merge-requests/<iid>/head` in the target at its source's tip, by one `update-ref
  --stdin`, a fork's by one fetch (MEASURED r6 4.2), a deleted one's removed (ASSUMED); no `/merge`,
  which exists only when computed (MEASURED). `HoldNext("git:<path>")` holds the project's next git request through host mode.
- **The scenario's half** (`SeedHosting7bGitLabAsync`, WP-F's stub, 1.9): users ada 2001 and octo 2002
  (Ada Lovelace, Octo Cat), ada's fine-grained and legacy tokens returned; both Developers (30) on
  group `northfield`; WP-F's bare folders served in place by `AddProjectAsync` (`northfield/platform/web-app`,
  `ada/web-app` as its fork, `northfield/platform/services/billing/invoice-generation-and-reconciliation`
  empty); web-app's !21 "Add the search box" (ada, `feature/search`), !22 "Draft: Update the readme" (octo,
  `docs/readme`), !23 "Fix a typo in the footer" (ada, from `ada/web-app`'s `fix-typo`).
  `FakeGitLabGitTests` pins it through `ServeHosting7bAsync` on port 0.

### 4.6 Tests (the builder runs these classes only, D245)

- `FakeGitLabTests` (in process): every route, check, 4.4 item and fault once, a fault then the normal
  answer; `PRIVATE-TOKEN` and `?private_token=` 401; membership through a group three levels up;
  `AddListedProjects(…, 1001)` to its last page; `:id` six ways (two escapes, a number, a trailing `/`,
  `.git`, unescaped); "404 Not found" with its lower-case `f`; create's refusals in order, three draft
  prefixes, a fork's create; DELETE 204, 403, then 404; a counted 304; no `Requests` entry holds a token.
- `FakeGitLabGitTests` (port 0, real git): `g/s/p.git` cloned with `x-access-token`, the username and
  through the 301; `ls-remote` shows a same-project and a fork MR's head, no `/merge`; its fetch writes
  only `FETCH_HEAD`; unknown, revoked and expired 401; a `read_user` fetch and a Code: Download push
  403; an anonymous clone of a public project, its push asking; a moved project at its old path: clone,
  fetch and push answer 200 with the token and no 301 (on CI's 2.43 too); a path in another case; the
  scenario's half (4.5).

### 4.7 ASSUMED answers for the live command (D342)

WP-LIVE's probes (r6 14, g2, 15.7) settle, before part A's checks (0.4), for the fine-grained kind
alone (D375): `user`, and `self`, which D364's token may not read (no Personal Access Token: Read,
r6 2.2), so check 5's 403 and its text; create (201, the 409, the
missing-branch 400, `Draft:`, nothing ahead); a made-up `glpat-` value's 401 on `GET user` (D373); git
(user names, the head ref, D340's Basic); `ratelimit-*` and a 304 with a token; a non-simple
row; the clean-up's PUT, DELETE and branch delete. Kept as marked: a legacy token's sign-in (`user`,
`self`) and check 4's 403 with its `scope` lists, on GitLab's docs and source (READ, the lists
ASSUMED; D375); a selected-projects token's list (ASSUMED, as 7.5) and git's 403s (READ g2 G3, as 4.5 marks them),
which need a token D364 does not make; the expired and revoked 401s (READ, 15.7). Kept ASSUMED: the moved project's query
order, an empty repository's `default_branch`, an unknown `target_project_id`, the 5xx bodies.

### 4.8 Done when

Both classes pass; each answer's mark is in its doc comment and 4.7 heads `.Router.cs`; no 7a test or
picture changes; no token or address of the owner's, and no value from the research's captures
(section 1), in the files.

**As built** (WP-FL, `4937997`, `33e3a0a`, the review's fixes `56218ae`, 2026-11-01). Where the build settles what 4.2 to 4.5 left open:
- `AddToken`'s n counts per user. The fine-grained default is D364's list (no Personal Access Token:
  Read); `FakeGitLabPermissions` holds the labels, scopes and defaults. Expiry is read on `HarnessTime`'s
  date. Grants are by path, a project's own moving with `Move`; `order_by=name` and `path` sort by the
  last segment. Public beyond 1.9: settable `Visibility`, `Description`, `LastActivityAt`;
  `FakeMergeRequest.State`, `HeadSha`. `AddListedProjects`' projects are private and git-less
  (`empty_repo`, `main`), listed through a grant or the user's own namespace alone.
- Check 3 without a token: `user`, `self` and writes 401; a project's reads go on, an unknown or hidden
  one 404 "404 Project Not Found" (MEASURED r6 3.2). Check 4's lists: G2.3's three, `api read_api` on
  other reads, `api` on writes (ASSUMED); check 5 names `user` on `user`, `self`, `projects`. A route miss
  is 404 E before any check; a non-digit iid's 400 E after them.
- Create: (a) joins in Grape's declared order, title, source_branch, target_branch (ASSUMED); (e) takes
  the whole fork network (READ `validate_fork`); (f) names `!<iid>`, a fork's too (READ `to_reference`,
  not r6 5.2's `path!iid`). The build's other ASSUMED choices head `.Router.cs` for WP-LIVE.
- `author.public_email` and `email` are the made-up address. An open MR whose source branch is gone
  keeps its head ref, no `Errors` line (7a's records one). The ref sync reads each state once.
- Git: the `.git`-less `info/refs` 301 has key `page` and an absolute `Location`; the 401 body is the
  server's "Authentication required.", realm GitLab. Refusals keep `GitAccess#check`'s order: a legacy
  token's missing ability, then a hidden project's 404 text (as a 403), then a fine-grained label. The
  token reaches `MayPush`, `PushRefusal` and `FetchRefusal` through a `[ThreadStatic]` set by
  `LoginOfToken`: `AuthRemoteServer.HostAccess` asks the four callbacks in one synchronous call, which a
  change there must keep.
- Outside 4.1's list: `Phase7bSupportTests`' loopback probe (WP-F's) takes `/nothing/here.git/info/refs`,
  since the fake answers 4.5's 301 to `<path>.git/info/refs` for a `.git`-less path;
  `Scenarios.Hosting7bGitLabGroup` is new.
- Git 2.43 was not run here: the research's MinGit lacks `git-http-backend`; the moved-project test uses
  nothing version-bound, and CI's containers run it.

## 5. WP-H: the shared HTTP core

### 5.1 Goal and files

Status steps 7 and 8. `GitHubClient`'s rules (7a design 6.2; D284, D285) move into one internal core
that every provider's client drives through a profile (D358, ruling 4, r1 P7); GitHub's behaviour
does not change, and `GitHubClientTests` pass as they are, but for D380 (5.2's Logging, Bodies and
Answers; 5.3's reset). GitHub's 422 details become a
`PullRequestRefusal` (r1 P8), and another provider's token is refused without a request (D378). Wave 1,
branch `wip/p7b-http`; no git, no UI, no picture.

| File (under `src/VisualCommit.Hosting/` or `tests/VisualCommit.Hosting.Tests/`) | Change |
|---|---|
| `Http/HostingHttpClient.cs`, `HostingHttpProfile.cs`, `HostingExchange.cs` (with `HostingReply<T>`), `HostingHttpTexts.cs` | new (5.2) |
| `GitHub/GitHubClient.cs`; `GitHub/GitHubProvider.cs` | GitHub's profile (5.3); A5's refusal and another provider's token (5.4) |
| `GitHub/GitHubJson.cs`; `HostingRecords.cs` | doc comments only (`Detail`, `RequestId`, `Owner` for three providers) |
| `HostingHttpClientTests.cs` new; `GitHubClientTests.cs`, `GitHubProviderTests.cs` | 5.5 |

### 5.2 The core (`HostingHttpClient`)

7a's rules as built (`GitHubClient.cs:41-651`), with the profile's hooks in place of GitHub's code:

- **Sending.** One `HttpClient` over `createHandler()`, `Timeout` infinite; one request at a time (a
  `SemaphoreSlim(1)` waited with the caller's token); 30 s (`timeout`) from the send; the caller's
  cancellation throws, the 30 s give `Network` with `HostingHttpTexts.Timeout`, no answer `Network` with
  `Network`; work on the pool (`Task.Run`, `ConfigureAwait(false)`); a call cancelled while it waits
  sends nothing, even when it is given the turn (WP-S, `1fe31fa`). Every request gets `User-Agent:`
  `HostingHttpClient.UserAgent` (7a's value), then `profile.AddHeaders`; `profile.Authorize(request, token)`
  only when its scheme, host and port are `TokenOrigin`'s (D285). A body is serialised through
  `profile.Json`; a type outside it is an `ArgumentException`.
- **Redirects.** For a 301, 302, 307 or 308 the core first asks `RefuseRedirect(exchange, location)`,
  on every redirect, the same-origin ones included; a failure it gives ends the request, and nothing is
  sent to `location`. Otherwise 7a's rule: repeated on
  `TokenOrigin`'s origin up to three hops; none, a fourth or another origin is `Malformed`, with 7a's
  warnings (`<status> without Location`, `<status>, a fourth redirect`, `redirected to <origin>`).
- **Bodies.** At most `MaxBodyBytes` (10 MB) by `Content-Length` or by reading, else `Malformed` ("an
  answer over 10 MB"). A leading `EF BB BF` is removed before anything reads the body, for every
  provider (g1 G0.8 and G7, MEASURED: System.Text.Json refuses it); `HostingExchange.Body` is without it.
  A body its `Content-Encoding` does not decode (gzip, deflate, br: .NET throws `InvalidDataException`
  or `InvalidOperationException`, MEASURED) is `Malformed`, warning `a body that could not be
  decompressed (<type>)`, never an exception out of the core (D380).
- **Answers.** A 2xx or 304 is offered to `RefuseSuccess(exchange)` (Azure DevOps' 203 and HTML, 6.3),
  then parsed with the caller's type: no value is `Malformed`. A 304 to a kept answer
  gives the kept body, the kept headers under the 304's own. `SucceedsEmpty(status)` is success without
  a value (GitHub's 409). Anything else goes to `Classify`. A hook that throws on an answer (a header
  value out of range) is `Malformed`, warning `Malformed (<type>)` (D380). On every failure the core
  sets `Provider` to the profile's, `Url` (the profile's own, else the request's) without its userinfo
  and query (D380: a same-origin `Location` can hold a user and password) and with each path
  segment that holds an e-mail-shaped run once percent-decoded as `<e-mail>` (D382: the server
  names a redirect's or a next page's path), and `RequestId` (`RequestIdOf`) where the profile left
  it null, kept, the profile's own too, only when it matches `^[A-Za-z0-9:._-]{1,128}$` (D382);
  it replaces each e-mail-shaped run (an `@` or a full-width at sign, U+FF20, D382) in `Message`
  and `Detail` with `<e-mail>` (D368: Azure DevOps' messages name identities, g1 G2, and reach the
  log), which changes no GitHub text.
- **Kept answers.** 7a's: a GET's 200 with an `ETag` all of `!` to `~` (one with obs-text, which .NET
  refuses to send back, is not kept, D380), per (SHA-256 of the token, URL), at most 512 and
  `keptBytes` (32 MB) of bodies, oldest first; `If-None-Match` on the next GET; never on disk;
  `ForgetKept()`. The headers kept are `profile.KeptHeaderNames` (7a's three for GitHub,
  `GitHubClient.cs:74`). Header dictionaries ignore case (gitlab.com sends `ratelimit-*` and
  `x-request-id` in lower case); a header given twice is joined with `, `.
- **Pages.** `GetPagesAsync` follows `NextPage(exchange)` while its URL keeps `TokenOrigin`'s origin and
  its path starts with `TokenOrigin.AbsolutePath` (GitHub's `ApiBase`), else `Malformed` with 7a's
  warning `Link to <origin>`; at `maxPages` with a next page left, `Next` is it. A failing page fails the
  list. `pageHeaders` is called with each page's headers (GitHub's 5.3). `HostingLinks.Next` reads
  `Link`'s `rel="next"` for GitHub's and GitLab's `NextPage` (`GitHubClient.cs:527-546`, moved).
- **Logging.** One warning per failure, `<name>: <method> <path> failed: <status or kind> (request <id>).`,
  `<path>` screened as the failure's `Url` and `<id>` only of the request id's shape (D382);
  never a header, body, query, token or an exception's text (D380): no exception object reaches the
  log, and one is named by `Describe`, its type with its `HttpRequestError` and an inner
  `SocketException`'s code, as in `Network (HttpRequestException ConnectionError, SocketException
  ConnectionRefused)`, since .NET's messages quote what a proxy or server sent (MEASURED: a proxy's
  password, a token echoed back, a cookie, an address, body bytes). This amends 7a design 6.2's "a
  network failure passes the exception" and 7.12's "by message only" for every such log call in
  `src/VisualCommit.Hosting/` (`AvatarCache`'s one included), and, by D382, for every catch-all
  there and every log call there that wrote an exception's message; GitHub's lines are 7a's otherwise.
- **Helpers.** `SameOrigin`, `OriginOf`, `IsRedirect`, `Resolve`, `ReadBoundedAsync` and the constants
  are internal statics here; `GitHubClient`'s forward to them, so `AvatarCache.cs:152-183` compiles
  unchanged. `MalformedAnswer` gives a provider's
  "succeeded but lacks what the app needs" with the warning (`GitHubProvider.cs:421-426`'s).

`HostingHttpTexts` names the provider through `HostingProviders.DisplayName` and `.Possessive` (1.2):
`Network`, `Timeout`, `Malformed` and `ServerError`; for GitHub each equals 7a's (`GitHubClient.cs:44-50`,
`:453`). `HostingExchange.Reason` is the reason phrase, for GitHub's fallback (`GitHubClient.cs:416`;
`GitHubClientTests.cs:377` expects "Unauthorized").

### 5.3 GitHub on the core

`GitHubClient` keeps its surface: the constructor (`GitHubClient.cs:92`), the constants (`:44-65`, still
`const`, since `GitHubClientTests` uses `MalformedText` in attributes), `UserAgent`, `Endpoints`,
`SendAsync`, `GetPagesAsync`, `GitHubReply<T>`, `ForgetKept` and the statics. It holds one
`HostingHttpClient` and a private `GitHubProfile`: `Provider` GitHub; `TokenOrigin` `ApiBase`;
`Authorize` `Bearer`; `AddHeaders` `Accept` and `X-GitHub-Api-Version` (`:384-386`); `Classify` 7a's
table (`:407-456`) on `HostingExchange`, its clock for `RetryAt`, an `X-RateLimit-Reset` of digits past
9999-12-31 23:59:59 UTC `Malformed` with 7a's `MalformedText` (D380); `SucceedsEmpty` 409; `NextPage` the
`HostingLinks.Next`; `RequestIdOf` `X-GitHub-Request-Id`; `KeptHeaderNames` `Link`,
`X-GitHub-SSO`, `X-OAuth-Scopes`; `Json` `GitHubJsonContext.Default`. `GitHubReply` from the core's
reply: `MoreExist` is `Next` not null; for pages, through the overload, `OAuthScopes` the first page's
and `Sso` the first `partial-results` one, else the last page's (`:222-227`; `GitHubClientTests.cs:261`
needs page one's).

### 5.4 Refusals in `GitHubProvider`

A5 (`GitHubProvider.cs:170-199`) sets `Refusal` on a `Validation` failure by the client's `Detail`
(`GitHubClient.cs:466-483`), the forms the App matches today
(`RepositoryOperations.CreatePullRequest.cs:374,419,421,423`): starts with "A pull request already
exists" `AlreadyExists`; starts with "No commits between" `NoCommits`; "head is invalid" `HeadMissing`;
"base is invalid" `BaseMissing`; else `None`. `Message` and `Detail` stay as they are, so GitHub's words
are still shown. GitHub never gives `BranchesMissing`.

The private `Refusal(token)` that every method asks before its request (`GitHubProvider.cs:322-330`)
gains a third case after 7a's two (D378, amending D298): a token whose `HostingProviders.TokenFormOf`
(1.2) is GitLab or Azure DevOps is `BadCredentials` with 7a's `Message` "Bad credentials" and
`Detail = "other-provider:" + HostingProviders.Id(form)`, without a request. A token of GitHub's form, or
of none, goes on as in 7a.

### 5.5 Tests (the builder runs these classes only)

- `HostingHttpClientTests`, a test profile naming GitLab over `RecordingHttpHandler` scripts: the
  token on `TokenOrigin` alone (not another origin, not a hop); headers on every hop; redirects (same
  origin repeated with method, body and headers; the fourth, none and another origin `Malformed`,
  nothing sent there; `RefuseRedirect` on a same-origin one); 10 MB both ways; a BOM'd 200 and a BOM'd
  error; `RefuseSuccess`; `SucceedsEmpty`; `Provider`, `Url` and `RequestId` on every kind; e-mail
  screening; kept answers (304 with body and kept headers, another token's and a POST not
  conditional, the budget, `ForgetKept`); headers without case and joined; `HostingLinks.Next`; pages
  (cap and `Next`, off origin or path, a failing page, the observer); one at a time and a waiting call cancelled; the texts, GitHub's equal to
  `GitHubClient`'s constants; no query, token or header in a log line; a body type outside `Json`;
  D380's rows, on strings and over a raw socket on 127.0.0.1 with the real handler; D382's path
  screen and request id shape. D382's catch-alls are tested where they are: `RepositoryHostingTests`,
  `RepositoryHostingAvatarsTests`, `HostingSessionCredentialTests`, `HostingSessionSignInTests`,
  `SecretServiceStoreTests` and `TokenVaultTests` each give one a made-up secret (D382).
- `GitHubClientTests`: unchanged; one method: every failure of the fault table names GitHub. D380
  changes three Network lines and one exception assertion, and adds the reset and a redirect's userinfo.
- `GitHubProviderTests`: one theory, A5 against `FakeGitHub` for the four forms and an other 422
  (`None`), and a 403 (`None`); one theory, `CheckTokenAsync` with `glpat-FAKE-ada-1` and with
  `new string('a', 76) + "AZDO" + "abcd"` is `BadCredentials` with `Detail` `other-provider:gitlab` and
  `other-provider:azure-devops`, and `Fake.Requests` stays empty.

Commits: (1) the core and its tests; (2) `GitHubClient` on it; (3) the refusals and the doc comments.

### 5.6 At the merge; done when

The lead runs `VisualCommit.Hosting.Tests` and the 7a walk-through (every GitHub request now passes the
core); no lagged list, no CLAUDE.md row, no picture. The whole default run follows the merge that closes
wave 1, whichever of WP-F, WP-H and WP-C is last (0.4). Done when the three classes pass with
`GitHubClientTests`' 7a cases untouched but for D380's Network lines and exception assertion;
nothing under `Http/` names a provider but through `HostingProviders`; no token, header, body, query
or exception's text reaches a log (D380); `AvatarCache.cs` is unchanged but for its one log call on an
HTTP-stack exception (D380); CI's five jobs are green.

**As built** (WP-H, `2ed2447`, `549ec8d`, `43bcb65`, review fixes `4bb76fe`, 2026-11-01). Where it
differs from 5.1 to 5.5 or settles what they left open:
- Beyond 1.4, now written there: `HostingHttpProfile.WarningOf` (a profile's failure's `<status or
  kind>`, 6.3's "redirected outside the organization") and `HostingHttpClient.Failed`, which finishes a
  failure a client builds from a success (6.4) as the core finishes its own; `MalformedAnswer` is one.
  A failure built without the core sets `Provider` itself (default GitHub). `HostingLinks` is in
  `HostingHttpProfile.cs`.
- An answer's order: the core's stops (no answer, the time, a redirect, 10 MB), `RefuseSuccess` (2xx,
  304), `SucceedsEmpty` (any status), then the parse or `Classify`. `RefuseRedirect` is asked of every
  redirect with a `Location`, a fourth too, before 7a's rule, with the request's `Url` (query kept) and
  an empty `Body`. `SendAsync`'s `Next` is always null; `pageHeaders` sees each page that gave a value,
  before the next is asked.
- `ScreenEmail` replaces the whole non-blank run around an `@` or a U+FF20. A branch name with one goes
  too, GitHub's words included (`Refusal` still holds), so 5.2's "changes no GitHub text" holds only
  for branches without one and the App's D316 fallback shows `<e-mail>` there: the lead decides
  whether to put it to the owner.
- Statics are `public` on the internal class; `Profile` and `Clock` are properties; the 30 s run on
  real time, a profile reads its own clock for `RetryAt`. Bodies go as `application/json; charset=utf-8`.
  `GitHubClient` adds `internal MalformedAnswer`; a list's `OAuthScopes` and `Sso` are 7a's in every
  case. `GitHubProvider`: `public const OtherProviderDetail`, `internal static RefusalOf(detail)`; the
  token refusal guards every method. Nothing under `Http/` names a provider. Outside section 5 the
  design's text changed in 1.4 and 6.3 only.
- D380 (H1 to H3): no exception object reaches the log; `Describe` names one by its type, its
  `HttpRequestError` when not `Unknown` and the first inner `SocketException`'s code (`AvatarCache`'s
  one HTTP log call uses it, its only change). Every `Url` drops the userinfo. A body that cannot be
  decompressed, an answer a profile hook throws on (`RequestIdOf`'s gives no id) and GitHub's reset
  past year 9999 are `Malformed`. `Uri.IdnHost` throws on some hosts `Uri.TryCreate` takes (MEASURED,
  listed at `IdnHostOf`), so hosts are read through `IdnHostOf`: such a host is on no origin, named
  `(an unreadable host)`, and a redirect or next page there is `Malformed`; `GitHubUrls` and the
  avatar check read hosts so too. 7a assertions changed: `GitHubClientTests`' fault table's last line,
  `No_answer_…`, `A_cancellation_…`, `HostingSessionCredentialTests`' Network line, `AvatarCacheTests`'
  `(TaskCanceledException)`.
- D382 (H4 to H6) and the third hunt: every catch-all of `src/VisualCommit.Hosting/`, and every log call
  there that wrote an exception's message, names it by `Describe` alone and passes no exception
  (`RepositoryHosting`'s two runs, `HostingSession`'s refusal check and sign-out deletion with its seam
  `DeleteAvatarFile`, the Secret Service's probe, `TokenVault`'s four and `WindowsCredentialStore`'s
  four, these untested: only the real store reaches them); the pull requests' failure has
  `HostingHttpTexts.Network`. The typed file and JSON catches (`AuthorMapStore`, `AvatarCache`'s save
  and index, `JsonAccountStore`) still pass theirs: no server's text reaches them. `ScreenPath`
  screens the warning's path and every `Url`: a segment holding an address once percent-decoded is
  `<e-mail>`. `ShapedRequestId` keeps an id (the reply's, a failure's, the log's) only of
  `^[A-Za-z0-9:._-]{1,128}\z`, which admits every token form (D378), so by 5.2's "never a token" the
  core also screens the request's token, and the credential `Authorize` sends when it differs (a
  `Basic` in Base64), in any case (hosts are read in lower case): `<token>` in each failure it
  finishes (`Message`, `Detail`, `Url`'s path) and its warning, and no id holding one. `Failed` and
  `MalformedAnswer` screen a token only when a client passes the request's (GitLab does since
  WP-GL's hunt 3; 7a's GitHub passes none). An `ETag` beyond `!` to `~` is not kept (D380). No 7a
  assertion changed; `HostingSessionSetup.Fault` throws a given exception.
- CI at WP-C's merge (run 41311809889 on `6586066`): off Windows `Uri.TryCreate("/p?page=2",
  UriKind.Absolute)` gives `file:///p%3Fpage=2`, so `HostingLinks.Next` gave a relative link as a next
  page on Linux and macOS (on Windows `//host/p` reads as a share). It now takes only a text that
  begins with an RFC 3986 scheme; two more rows of its theory. A redirect's `Location` is read by
  .NET's header parser, which keeps a relative one relative on every platform (the relative-redirect
  tests passed there). Wave 2's clients check the scheme of every address they read from an answer;
  7a's `GitHubProvider.Info` (`clone_url`, `html_url`) and `AuthorMapStore`'s avatar do not: the
  review before the gate weighs them.

## 6. WP-AZ: Azure DevOps' client and provider

### 6.1 Goal and files

Status steps 6 and 7: `AzureDevOpsProvider : IHostingProvider` (1.4) on WP-H's core, tested against
`FakeAzureDevOps.InProcess(root).CreateHandler()` (1.9) and `RecordingHttpHandler` scripts; rulings 2, 3,
9, 14 to 20. Out: bindings (WP-C), the session (WP-S), screens. Wave 2 after WP-H, WP-FA and WP-C
(6.2 calls WP-C's `InPlainForm`), branch `wip/p7b-azure`; no git. Copied facts keep their marks; 6.8
lists the ASSUMED (D342).

| File (`src/VisualCommit.Hosting/AzureDevOps/`, `tests/VisualCommit.Hosting.Tests/`) | Change |
|---|---|
| `AzureDevOpsEndpoints.cs` | WP0's, owned now, unchanged |
| `AzureDevOpsApi.cs` (public, 1.4), `AzureDevOpsUrls.cs`, `AzureDevOpsClient.cs`, `AzureDevOpsJson.cs` (internal) | new (6.2, 6.3, 6.7) |
| `AzureDevOpsProvider.cs` | new (6.4 to 6.6) |
| `AzureDevOps{Recognition,Client,Provider,Json}Tests.cs` | new (6.8) |

### 6.2 `AzureDevOpsUrls`

**`Recognise`**: no network, any organization, read by hand (`GitHubUrls.cs:8-9`); hosts without case,
a user part (no password) allowed and never kept. With `Service`:
`https://[user@]dev.azure.com[:443]/<org>/<project>/_git/<repo>`, `…/<org>/_git/<repo>` (project = repo,
r5 6.1, MEASURED); `https://[user@]<org>.visualstudio.com[:443]/[DefaultCollection/]<project>/_git/<repo>`
and `[DefaultCollection/]_git/<repo>`, one label before `.visualstudio.com`, never `vssps`, `app`,
`vs-ssh` or a trailing dot (r7 0.9; G3.2 V17, V23); SSH, never bound: `[user@]ssh.dev.azure.com:v3/<org>/<project>/<repo>`,
its `ssh://…[:22]/v3/…` form, and both on `vs-ssh.visualstudio.com` (r5 6.1; `_ssh`, ASSUMED there, is
not taken). After `_git/` an optional `_full/` or `_optimized/` is skipped; one trailing `/` is allowed;
`.git` belongs to the name (not stripped, r5 6.1, MEASURED). An `http` or `https` URL passes `CredentialBinding.InPlainForm`
(1.2, WP-C's: g3 G3.4's steps 2 to 4), so a recognised URL names what git reaches; an SSH form passes the
same character, segment and escape rules on its path after `v3/` (it is never bound, so this keeps
recognition's one table). The organization (the first path segment, after `v3/` on SSH, or the label
before `.visualstudio.com`) matches `^[A-Za-z0-9-]+$` (D360's grammar); `DefaultCollection` on dev.azure.com and a
project starting `_` are refused; project and repository are percent-decoded as UTF-8. Result:
`HostedRepository(AzureDevOps, endpoints.Host, "<org>/<project>", <repo>)`, `Host` `dev.azure.com` for
both families (r5 6.2). With `Local(port)`: only `http://[user@]127.0.0.1:<that port>/` in the
dev.azure.com forms. G3.2's 38 rows are the table, each with its own recognition answer: a row is
recognised when it has one of the forms above, passes `InPlainForm` (which has no organization clause,
since recognition takes any organization) and names its organization in the grammar. V15 and V16 are
recognised and bound; V11 (another user) is recognised though never bound; V6, X9 and X10, which G3.4
refuses only for the organization's grammar, are not recognised; every other row is recognised exactly
where G3.4 binds it.

**`AzureDevOpsProvider.OrganizationOf(string? text, AzureDevOpsEndpoints endpoints)`** (1.4; ruling 16,
the one rule WP-A's box uses), trimmed: a text without `://` is the name; an `http` or `https` URL, its
user part ignored, gives its first path segment on `WebBase`'s host and port, or with `OrganizationHosts`
the label of `<label>.visualstudio.com` without case; the result must match `^[A-Za-z0-9-]+$`
(D360; r5 6.1 READ allows fewer, which A1's 404 then answers), else null. **`WebUrlOf(repository)`** `{WebBase}<org>/<project>/_git/<repo>`, each segment
`Uri.EscapeDataString`d (`webUrl`'s form, r5 2.1, MEASURED); **`ApiPath(repository, rest)`**
`<org>/<project>/_apis/git/repositories/<repo><rest>` on `WebBase`.

### 6.3 `AzureDevOpsClient` and its profile

`AzureDevOpsClient(endpoints, createHandler, log, clock, TimeSpan? timeout = null, int pageSize = 100)`
holds a `HostingHttpClient`. The API is always on `WebBase` (`ApiOf(<org>)`), for
`<org>.visualstudio.com` remotes too (one service, r5 6.2, MEASURED), so the token has one origin (D285).
`AzureDevOpsApi` (1.4) holds `Version` `7.1`, `ConnectionDataVersion` `7.1-preview.1` (`7.1` is a 400
there, r5 0.1, MEASURED) and `CoreAreaId`; the client's own `AnonymousId`
`aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` and `AnonymousDescriptor` `System:PublicAccess;`. Every URL
carries `api-version=`.

The profile: `TokenOrigin` `WebBase`; `Authorize` `Basic` of UTF-8 `":" + token` (r5 0.6, READ);
`AddHeaders` `X-TFS-FedAuthRedirect: Suppress` (the sign-in 302 becomes a 401, r5 1.3, MEASURED) and
`Accept: application/json`; `RequestIdOf` `ActivityId`, else `X-VSS-E2EID`; no `NextPage`,
`SucceedsEmpty` or kept headers (no `ETag`, r5 7, MEASURED); `Json` `AzureDevOpsJsonContext`.
`RefuseRedirect`: on A1's connectionData every redirect is `BadCredentials` (D360). Elsewhere,
`BadCredentials` for a `Location` whose path is `/_signin` or whose host ends `.vssps.visualstudio.com`,
and `Malformed`, with `WarningOf` giving `redirected outside the organization`, for a same-origin `Location` whose first
path segment differs, without case, from the request's. None is followed. `RefuseSuccess`: `BadCredentials` for a 203, or a 2xx whose
`Content-Type` is `text/html` or body starts `<` (g1 G0.1). `Classify` by status and `typeKey`, never
the English (ruling 20; r5 1.4, g1 G2):

| Answer | Kind |
|---|---|
| 401 with any body: empty, HTML, `UnauthorizedRequestException`, `AccessCheckException` (g1 G0.2) | `BadCredentials` |
| 400 `VssInvalidPreviewVersionException`, `VssVersionOutOfRangeException`, `VssInvalidApiResourceVersionException` | `ApiVersion` |
| 403 (`GitNeedsPermissionException` among them); 404, JSON or not (an unknown organization is text, r5 1.3) | `Forbidden`; `NotFound` |
| 409 `GitPullRequestExistsException`; 400 `GitPullRequestCannotBeActivated` (no side named, g1 G2.1) | `Validation`, `Refusal` `AlreadyExists`; `BranchesMissing` |
| other 400, 409, 422 | `Validation` |
| 429 | `RateLimited`, `RetryAt` now + `Retry-After` (a day at most), else `X-RateLimit-Reset`, else 60 s |
| 5xx; anything else | `ServerError`; `Malformed` with `HostingHttpTexts.Malformed` |

`Message`: the body's `message`, else `value.Message` (TF400677's shape), else `X-TFS-ServiceError`
URL-decoded, else the reason phrase, else `HTTP <status>` (a 5xx without one: `HostingHttpTexts.ServerError`).

`GetPagesAsync<T>(token, url, type, maxPages, ct)` walks `{"count","value"}` with `$top=<pageSize>&$skip=<rows>`:
a short page ends it; after `maxPages` full pages one `$top=1` request says whether more exist; a row
is kept once by its id (a new one between pages shifts them).

### 6.4 A1 `CheckTokenAsync(token, organization, ct)` (D360)

An empty token, or one with more than visible ASCII, is `BadCredentials` without a request (7a's rule,
`GitHubProvider.cs:322-330`), and so is one whose `HostingProviders.TokenFormOf` (1.2) is GitHub or
GitLab, with `Detail = "other-provider:" + HostingProviders.Id(form)` (D378); an organization
`OrganizationOf` rejects is `Validation` without one (WP-A checks first). Then, with the token, never
anonymously (D257, D350):
1. `GET <org>/_apis/connectionData?api-version=7.1-preview.1`. A 404 is `NotFound`, which at A1 means
   no such organization (connectionData is in every one, r5 1.3, MEASURED without a credential; with a
   token ASSUMED until D374's probe of a made-up organization, 6.8); WP-A words it so. A 200 is
   `BadCredentials` with `Detail = AnonymousDetail` (the token ignored, g1 G0.3) when `authenticatedUser` or a GUID `id` is missing, the
   `id` is `AnonymousId`, the `descriptor` starts `AnonymousDescriptor` (without case) or
   `properties.Account.$value` is `Anonymous` (r5 1.2, MEASURED); never by `metaTypeId` (g1 G0.4).
2. Only after step 1's 200 has passed those checks (any other answer or failure of step 1 ends A1
   with no second request): `GET <org>/_apis/resourceAreas/<CoreAreaId>?api-version=7.1-preview.1`: `locationUrl`, on `WebBase`'s
   origin with one segment equal to the typed name without case, gives the spelling (MEASURED
   anonymously, g1 G4.2; with a token ASSUMED). Any other answer or failure but the caller's
   cancellation keeps the typed name, with one warning; the sign-in goes on.

These two are the reads WP-LIVE's guard lets through (D352, D371). The account: key (AzureDevOps,
`endpoints.Host`, `<org>`), `Login` that spelling, `Id` 0, no avatar, `UseForGit` true, `UserId` the `id`
as lower-case text, and `Name` the first of `customDisplayName` and `providerDisplayName` that is
neither e-mail-shaped (D368's test: an `@` with a non-blank character on each side, anywhere in the
text) nor equal to `Account.$value` without case; if neither qualifies, null, and the Accounts block
shows the organization alone (10.3). Both can be addresses (g1 G3.3). `Account.$value`, the sign-in
name (an e-mail), serves step 1 and this comparison alone, and is never kept or logged (D346).

### 6.5 A2 and A3

- **A2** `GET <account.Login>/_apis/git/repositories?api-version=7.1`, or `<org>/<ListProject>/…` when
  set (D352, D364); never `includeLinks` or `includeAllUrls` (15 KB a row, g1 G4.4); unpaged (r5
  2.1, MEASURED) within the core's 10 MB, about 8,480 rows (g1 G4.4). Sorted by name, then project,
  `OrdinalIgnoreCase` (g1 G4.4). A row: `Repository` from `project.name` and `name`; `DefaultBranch`
  without `refs/heads/`, empty when absent (an empty repository, MEASURED); `IsPrivate`
  `project.visibility == "private"`; `IsFork` `isFork`; `IsArchived` false; `Description` null;
  `CloneUrl` `remoteUrl` with its `<org>@` (ruling 15); `HtmlUrl` `webUrl`; `UpdatedAt` and `CanPush`
  as 1.4; `Parent` from `parentRepository` when present (ASSUMED in a list), `ParentDefaultBranch` null.
  A row with `isDisabled` (it cannot be cloned, r5 2.1, MEASURED) or without readable URLs is left out.
- **A3** `GET ApiPath(repository, "")` (name or id without case, MEASURED); a fork's parent read by a
  second GET gives `ParentDefaultBranch`, null if that fails. In A3 to A5 an `Owner` not of two
  segments, or another provider's or host's repository, is `Validation` without a request. Every A3 to
  A5 request is below `ApiPath(repository, …)`, never the project-level `…/_apis/git/pullrequests/{n}`
  or an organization-wide route, so D352's guard (15.4) lets the app's own requests through; the client
  tests pin each path.

### 6.6 A4, A5 and the other members

- **A4** `ApiPath(repository, "/pullrequests?searchCriteria.status=active")` through the walk, 3 pages
  of 100 (7a's 300). `Number` `pullRequestId`; `Author` `createdBy.displayName`, null when it is
  e-mail-shaped (6.4's test), never `uniqueName` (an e-mail, r5 3.1); `IsDraft` `isDraft`; `HtmlUrl` `WebUrlOf(base)/pullrequest/<id>` (the answer has none;
  MEASURED on both hosts, r5 3.2); `BaseRef` `targetRefName` without `refs/heads/`; `BaseRepository`
  the one asked; `CreatedAt` `creationDate`; `HeadSha` `lastMergeSourceCommit.commitId` or empty.
  Without `forkSource` (MEASURED): `HeadRef` `sourceRefName` without `refs/heads/`, the head the base.
  With it (READ, g1 G2.6, where `sourceRefName` is `refs/pull/<id>/source`): `HeadRef` `forkSource.name`
  without `refs/heads/`, `HeadRepository` its repository's project and name in the base's organization
  (r5 3.4), null without them. No checkout ref (D361).
- **A5** `POST ApiPath(base, "/pullrequests")`: `sourceRefName` and `targetRefName` as `refs/heads/…`
  (r5 4, READ), `title`, `description`, `isDraft`; the path names the repository (Microsoft's CLI,
  g1 G2, READ). A `HeadRepository` other than the base: one GET of it first, its `id` sent as
  `forkSource.repository.id` (g1 G1.2; the minimal shape ASSUMED); one of another organization is
  `Validation` without a request. 200 and 201 succeed (g1 G0.12) into A4's record; once, never retried;
  `HeadOwner` unread; `NoCommits` and `HeadMissing` never given (r5 4: unknown).
- **Others**: `Endpoints` (public), `Host`, `WebBase` the endpoints'; `SourceCanExist`, `PullRequestHeadRef` through
  `PullRequestRefs`; `CoverageOf(account)` `new GitCoverage(account, WebBase) { Organization =
  account.Login, OrganizationHost = endpoints.OrganizationHosts, HelperUserIsAccounts = true }`
  (D359); `Covers` provider, host, and `OwnerRoot` equal to `account.Login` without case (D350);
  `TokenPage(org)` `{WebBase}<org>/_usersSettings/tokens` (r5 3.2, MEASURED), `ArgumentException` when
  `OrganizationOf` gives null; `PullRequestsPage` `WebUrlOf(repository)/pullrequests` (any path answers
  200, g1 G4.3, so no test leans on it); `internal ForgetKept()`; `Dispose`. Failures the provider makes
  itself set `Provider`.

### 6.7 JSON (`AzureDevOpsJsonContext`)

7a's options with camelCase (`GitHubJson.cs:228-231`); `required` only where every answer has it (g4
D4 §4): a list's `value`; a repository's `id`, `name`, `project.name`; a request's `pullRequestId`,
`title`, `sourceRefName`, `targetRefName`. `[JsonPropertyName("Account")]`, `"$type"`, `"$value"` (g4
D4 §1, MEASURED: camelCase misses `Account` silently); `$id` skipped. Dates are `string?` read by
`DateOf` (`AssumeUniversal | AdjustToUniversal`, `MinValue` when unreadable); no member is a
`DateTimeOffset` (g4 D4 §3). Errors: `message`, `typeKey`, `value.Message`.

### 6.8 Tests (the builder runs these four classes only)

The fake serves 1.9's and 3.3's answers (`MetaTypeId`, `SignInRedirect` and `NonAuthoritative`
included). Scripted through `RecordingHttpHandler`: the HTML 401 with a BOM, the Anonymous descriptor
with another id, an HTML 200 on connectionData, `parentRepository` in a list, and A4's 301 rows with no
fake (its ref sync would make 301 merge commits, 3.5): pages at `$top=100&$skip=0`, `100` and `200`
with 100 rows each, then the `$top=1&$skip=300` probe with one row, give 300 rows and `MoreExist` true
in exactly four requests, each below `ApiPath(repository, "/pullrequests…")`.
- `AzureDevOpsRecognitionTests`: 6.2's forms under `Service` and `Local`, the SSH forms' path rules,
  G3.2's 38 rows (V1 to V26 and X1 to X12; the ten rows 8.7 adds, P1 to P5, Y1, Y2 and U1 to U3, are
  `Select`'s and `InPlainForm`'s alone) with 6.2's answers, other ports; `OrganizationOf`'s forms and refusals.
- `AzureDevOpsClientTests`: the headers of 6.3 on every request (`Basic` decoded to `:` and the
  token), the token on `WebBase` alone; each row of 6.3's table, with the 401's four bodies, a 302 to
  `spsprod<x>.vssps.visualstudio.com/_signin` and to the fake's `/_signin` (nothing sent there), a
  same-origin 302 from connectionData `BadCredentials`, a 302 from `/fake-org/_apis/…` to
  `/other-org/_apis/…` `Malformed` with nothing sent to `other-org`, 203, an HTML 200, a BOM'd error,
  `X-TFS-ServiceError`, a TF401027 text's e-mail screened; `RequestId`; the warning
  `Azure DevOps: GET /<org>/_apis/… failed: 401 (request <id>).`; the walk at `pageSize` 2.
- `AzureDevOpsProviderTests`, against the fake: A1 (the account; the Anonymous id, its descriptor with
  another id, `metaTypeId` 255 on a member accepted; an unknown organization `NotFound`, and a 401, the
  Anonymous 200, a 203 and an HTML 200, each with exactly one request; the spelling read failing or
  differing; exactly 6.4's two paths; `Name` null for a member added with a made-up example.com address
  as display name and no `CustomDisplayName`, "Ada" when its `CustomDisplayName` is "Ada", null when both
  are addresses; no request for an empty token, a bad organization, or `github_pat_FAKE-octo-1` and
  `glpat-FAKE-ada-1`, which give `BadCredentials` with `Detail` `other-provider:github` and
  `other-provider:gitlab`); A2 (order, `ListProject`, an empty repository, a disabled one left out, a
  fork's parent); A3's parent; A4 (a draft, a fork, the URL, no `uniqueName`, a null `Author` for
  an e-mail-shaped `displayName`); A5 (the body, a fork after one GET, 200 and 201, the three refusals,
  one POST, another organization); the other members, `Covers` without case; `Provider` on every failure.
- `AzureDevOpsJsonTests`: made-up copies of r5's and g1's shapes with their marks (anonymous
  connectionData MEASURED, a member's READ, errors with `$id` and TF400677's `value`, a repository
  without `defaultBranch` MEASURED, the zero date MEASURED); `DateOf`; no `DateTimeOffset` member.

**ASSUMED answers the fake copies, for WP-LIVE's first stage** (D342): 3.7's (D373's made-up values
among them, and D374's made-up organization read with a token, on which 6.4 step 1's `NotFound`
rests), and the client's own: the resource area with a token, the POST by name, messages naming an
e-mail. Each answer found goes into the fake before part A's checks (0.4). Kept as marked, with no fork
in the sandbox (D375): A5's fork body (ASSUMED), and the fork shapes A2 and A4 read, as 3.7 keeps them.

Commits: (1) `AzureDevOpsUrls`; (2) the client and the JSON; (3) the provider; each with its tests.

### 6.9 At the merge; done when

No lagged list (nothing wired until WP-S), no CLAUDE.md row, no picture. If WP-AZ's is the last of wave
2's merges, the lead's whole default run follows it (0.4, D245). Done when the four classes pass; no
request goes without a token or to an origin but `WebBase`'s; no redirect is followed off it; no request
leaves the account's organization (at A1, the typed one); no e-mail reaches a record, a log line or a
failure; no Azure DevOps sentence is parsed; no value from the research's captures; CI's five jobs are
green.

**As built** (WP-AZ, `e83bfb0` to `b03bbc5`, the reviews' fixes `1b6204a` to `b95e496`, the merge's
rulings, 2026-11-01). Where the build differs from the text or settles what it left open:
- `AzureDevOpsUrls` (internal): `Recognise` (also with an `out` user), `OrganizationOf`, `WebUrlOf`,
  `ApiPath`, `NamesOf` and `IsName` (no empty, `.` or `..` name, none with `/` or `\`: A2 drops such
  rows and refuses such a `ListProject`, A3 to A5 refuse them unsent, A4's fork source is no head).
  An http(s) URL's host and port are .NET's, as `Select` reads them (empty user or port, leading
  zeros, `127.1`), so a repository form is recognised exactly where `Select` binds it (V11, SSH and a
  project or repository starting with `_`, as `_full`, aside); the path is read by hand, the
  organization as written (V16), `_git`, `DefaultCollection` and `_full`/`_optimized` (only before a
  name) without case, escapes decoded (`%5Fgit`); SSH takes port 22 alone. `OrganizationOf` cuts at
  `?` or `#`, refuses a `\`, ignores a user with a password, and takes `<label>.visualstudio.com` on
  https:443 alone.
- `AzureDevOpsClient` (internal) returns `AzureDevOpsReply<T>` (`MoreExist`); `GetPagesAsync<T>`
  takes at most `PageSize` rows of a page (if `$top` is ignored); a failing probe fails it, any probe
  row sets `MoreExist`, also one already seen; a null row is `Malformed` (in A2 too, D380).
  `SignInPageText` answers a 3xx of connectionData (300, 303, 305 by `Classify`; by A1, keeping the
  core's one warning, one the core stops, with no readable `Location`, a body over 10 MB or one that
  could not be decompressed, and a 304 it cannot parse), any 3xx but 304 (the core's success, never
  asked for) to the sign-in (also its host with a trailing dot), a 203 and an HTML 2xx or 304; none
  is followed. A same-origin redirect out of `InPlainForm` is `Malformed` as one into another
  organization, and so is one in it to a web page (the first `_` segment after the organization not
  `_apis`, as `_git/<repo>/_apis`; `<status>, redirected outside the API`). `Detail` is null on every
  classified failure; `RetryAt` lies between now and a day ahead; outside A1's 3xx, an answer the
  core stops first (over 10 MB, or not to be decompressed) stays `Malformed`, a 203 or a 3xx to the
  sign-in too; a 301, 302, 307 or 308's body is not read.
- Provider: public texts for its own refusals, `PullRequestPages` (3); D378's refusal guards A1 to A5.
  A2 refuses a foreign account or a login outside the grammar unsent. A row's `remoteUrl` and
  `webUrl` reach the record only when `Recognise` reads each as that repository, `remoteUrl` without
  a user or with the organization, `webUrl` without one; else A2 drops the row and A3 is `Malformed`.
  `Covers` also checks the account key's provider and host. The provider passes the request's token
  to the core's `MalformedAnswer` and `Failed` (WP-GL's R1); records holding it are not screened (7's
  known limit). Checked: `Login` in the grammar, `UserId` a GUID's lower-case text; A2 one request in
  10 MB, A4 300 rows at most, no warning per row left out; no cache but the core's 512 answers.
- For WP-S: construct with `HostingOptions`' handler; `ForgetKept` is internal. For WP-A: `TokenPage`
  throws `ArgumentException` where `OrganizationOf` gives null. For WP-LF: the ASSUMED answers are in
  the provider tests' comment. No fake fix; no file outside 6.1's list changed.

## 7. WP-GL: GitLab's client and provider

### 7.1 Goal and files

Status steps 6 and 8: `GitLabProvider` on WP-H's core, tested against `FakeGitLab`'s in-process handler
and scripted `RecordingHttpHandler` answers; rulings 4, 11, 12, 14, 17 to 20. Not bindings or
`PullRequestRefs`' rule (WP-C), the session or texts. Wave 2, branch `wip/p7b-gitlab`; runs no git. Files in
`src/VisualCommit.Hosting/GitLab/`: `GitLabEndpoints.cs` (WP0's signatures kept); new, internal but the
provider: `GitLabUrls.cs` (7.2), `GitLabJson.cs` (shapes, `GitLabJsonContext` with 7a's options,
`GitHubJson.cs:228-231`, and `SnakeCaseLower`), `GitLabClient.cs` (`GitLabProfile`, 7.3),
`GitLabProvider.cs` (7.4); tests `GitLab{Recognition,Client,Provider}Tests.cs` in Hosting.Tests.

### 7.2 Recognition (`GitLabUrls.Recognise(GitLabEndpoints, string?)`)

No network; read by hand as `GitHubUrls.cs:126-186` does (copied: not this package's file).
With `GitLabCom`, host `gitlab.com` or `www.gitlab.com` (which 308s, r6 6.1 MEASURED) in
`https://[user[:pw]@]host[:443]/p`, `http://[user@]host[:80]/p`, `ssh://[user@]host[:22]/p`, scp-like
`user@host:p`, and `ssh://[user@]altssh.gitlab.com:443/p`; no `git://`. With `Local(port)` only
`http://127.0.0.1:<that port>/p`, no user part. `p`: one `.git` and one `/` stripped; 2 to 22
segments (subgroups "nest up to 20 levels", r6 6.1 READ, unclear whether with the top-level group, so
a namespace of up to 21); each `[A-Za-z0-9_.-]+`, never `.`, `..` or `-` (a web URL's `/-/`); no
`%`, `?`, `#`, `\`, space or control character. `Owner` the namespace path, `Name` the last segment,
as written.

### 7.3 The profile (`GitLabProfile(GitLabEndpoints endpoints, TimeProvider clock)`)

- **Requests.** `TokenOrigin` `ApiBase`'s; `Authorization: Bearer <token>`, which both kinds take (r6
  1.1 READ), one header name for the request log and the redirect rule (g4 D1); `Accept:
  application/json`; no version header (r6 9). A URL is `new Uri(ApiBase.AbsoluteUri + relative)`, a
  project id `Uri.EscapeDataString(FullName)`, which keeps `%2F` to the request line (r6 6.2 MEASURED);
  a test pins it.
- **Redirects, paging, kept answers** are the core's. A moved project's GET is 301 to
  `/api/v4/projects/<id>[/rest]?<query>` on the same origin (r6 3.2 MEASURED), followed; a non-GET
  there is 405 (READ). `NextPage`: `HostingLinks.Next`, as GitHub's (r6 7 MEASURED); `RequestIdOf`:
  `x-request-id` (the core's headers ignore case); `KeptHeaderNames` `Link`, since a 304 has no body
  (r6 8). A 304 counts against the limit (r6 8 MEASURED): kept answers save bytes, not budget,
  and 2,000 a minute per user (READ) is far above 7a's refreshes.
- **Message**, GitLab's words (r6 10, g2 G2): `error_description` (shape F) ?? `message` as a string
  (A) ?? its first item (B) ?? each field and its first message, `"; "` between (C) ?? `message.error`
  (D) ?? `error` (E); any other body (`text/plain`, HTML, unknown) gives `<code> <reason phrase>`.
- **`Classify`**, in order. Every failure carries `Provider` GitLab, `Url` without query, `RequestId`.
  Kinds come from the status and F's `error` code alone, never from a sentence or the `scope` list,
  whose members vary by route (g2 G0.4):

| Condition | Kind | Detail, `Refusal`, `RetryAt` |
|---|---|---|
| 401, any body (A for an unknown token, F `invalid_token` for an expired or revoked one, g2 G2.1-2) | `BadCredentials` | |
| 403 with F's `error` `insufficient_scope` or `insufficient_granular_scope` | `Forbidden` | Detail the code: the token lacks a permission |
| other 403 (MRs disabled, no access, g2 G1.2) | `Forbidden` | |
| 404 (`404 Project Not Found`, an MR's `404 Not found`, a route's E, g2 G0.2) | `NotFound` | |
| 409 | `Validation` | `AlreadyExists` on a POST to `…/merge_requests` |
| 400 with shape C | `Validation` | on that POST, C's keys: `source_branch` `HeadMissing`, `target_branch` `BaseMissing`, both `BranchesMissing` (g2 G1.1) |
| other 400, 405, 422 | `Validation` | |
| 429, either form (r6 8 READ) | `RateLimited` | now + `Retry-After` (≤ 86,400 s, `GitHubClient.cs:493-507`), else `ratelimit-reset` (epoch), else `ratelimit-resettime` (date), else now + 60 s |
| 408, 5xx | `ServerError` | |
| anything else | `Malformed` | the core's text |

  A 403 is never a rate limit (r6 8); no `SsoRequired` (r6 11) or `ApiVersion` (r6 9); no `NoCommits`:
  GitLab creates a request without commits (g2 G1.4).

### 7.4 `GitLabProvider`

`public sealed class GitLabProvider : IHostingProvider, IDisposable`, 1.4's constructor and members, with
`const int RepositoryPages = 10, RequestPages = 3, MaxForkLookups = 20`; `ForgetKept` drops the client's
kept answers and the forks' paths.

- `Kind` GitLab; `Host`, `WebBase` the endpoints'. `SourceCanExist(r)` and `PullRequestHeadRef(p)` are
  `PullRequestRefs`' for GitLab (`refs/merge-requests/<iid>/head`, in the target for forks too, r6 4.2
  MEASURED). `CoverageOf(a)`: a `GitCoverage` of `a` on `WebBase`'s origin, `GitTwin` and
  `HelperUserIsAccounts` true. `Covers(r, a)`: a GitLab record whose `Host` is `a.Host`, without
  case.
- `TokenPage(_)`: `{web}-/user_settings/personal_access_tokens/granular/new`, the fine-grained form
  (no prefill); `LegacyTokenPage`, redirected to the legacy form prefilled (g2 G5.3 READ):
  `{web}-/user_settings/personal_access_tokens?name=VisualCommit&scopes=api`. `PullRequestsPage(r)`:
  `{web}{Owner}/{Name}/-/merge_requests`, segments escaped (g2 G4 MEASURED).
- The token needs (D345; r6 2.1, 2.2, g2 G3, READ): legacy `api`; fine-grained User: Read (A1), Project:
  Read (A2, A3, lookups), Merge Request: Read (A4) and Create (A5), Code: Download and Push (git; Push
  alone cannot fetch). An empty token, or one with anything but visible ASCII, is refused without a
  request, as `GitHubProvider.cs:322-330` ("401 Unauthorized" for the second): a list without a token
  is 200 `[]` (r6 1.2 MEASURED). A token whose `HostingProviders.TokenFormOf` (1.2) is GitHub or Azure
  DevOps is refused so too (D378): `BadCredentials`, "401 Unauthorized", `Detail = "other-provider:" +
  HostingProviders.Id(form)`; a `glpat-` token, or one of no form, goes on.
- **A1** `GET user` for both kinds, never `personal_access_tokens/self` (ruling 17): key (GitLab, `Host`,
  `username`), `id`, `name` unless it is e-mail-shaped (6.4's test; then null), no avatar, `UseForGit`
  true; the shape has no address member.
- **A2** `GET projects?membership=true&archived=false&order_by=last_activity_at&sort=desc&per_page=100`,
  never `simple`, 10 pages. Rows: `path_with_namespace`; `default_branch` or `""` (empty project);
  `IsPrivate` unless `public`; `IsFork` `forked_from_project` present; `http_url_to_repo`, `web_url`;
  `last_activity_at` or `MinValue`; `CanPush` the higher `permissions` `access_level` ≥ 30 (ASSUMED, r6
  3.1), `true` without; `Parent` null. `required` only `id` and `path_with_namespace` (1's rule); a row
  without a readable URL is left out with one warning naming its id.
- **A3** `GET projects/<id>`, a 301 followed; named by the answer's `path_with_namespace` (7a design
  19.2, integration-1); `forked_from_project` gives `Parent` and `ParentDefaultBranch`.
- **A4** `GET projects/<id>/merge_requests?state=opened&per_page=100` (`open` is a 400), 3 pages:
  `iid`, `title`, `author.username`, `draft`, `web_url`, `source_branch`, `sha` or `""`,
  `target_branch`, the repository asked as base, `created_at`; `required` those every MR has (g4 D4 §5:
  `web_url` missing lost the list). `HeadRepository`: the base when `source_project_id` equals
  `target_project_id`, with no request; else `GET projects/<source id>`, once per distinct id, kept by
  id until `ForgetKept`, at most `MaxForkLookups` a call in list order. A 403 or 404 there (an
  unreadable fork) and a fork past the cap give null, which WP-P treats as 7a's deleted fork (its head
  still comes from the base, r6 4.2); another failure fails the call.
- **A5**, never retried: `GET projects/<id>` of the source (`HeadRepository`, else the base; `HeadOwner`
  unread) and, if it differs, of the base, following a 301; a failure is returned, nothing posted. One
  `POST projects/<source's numeric id>/merge_requests` (so no 405): `source_branch`, `target_branch`,
  `title` `TitleFor(title, Draft)`, `description` as typed (quick actions run, ruling 20),
  `target_project_id` when the ids differ, and then `allow_collaboration: true` for a `public` source,
  the docs' condition (7a's `maintainer_can_modify`, `GitHubProvider.cs:179-188`); never `draft` (r6
  5.1). A 2xx gives the request. `TitleFor`, GitLab's `draft_title`: unchanged when it starts with
  `Draft:`, `[Draft]` or `(Draft)` in any case, else `Draft: ` first (r6 5.1 READ; the expression
  ASSUMED from the docs).

### 7.5 The fake's answers these tests rest on

MEASURED: shape A's 401, an MR's `404 Not found`, the 301, `Link`, `x-request-id`, ETags, 304s, a
fork MR's ids. READ, for the live command to confirm (D342) with the fine-grained token alone (D375):
Bearer, `GET user` and its 403 without User: Read (4.3's check 5, whose shape `self` shows), a made-up
`glpat-` value's 401 (D373), 405, create's 409 B, 400 C, 400 E, 422 B and 403 (g2 G1), `draft` for a
`Draft:` title. Kept READ: a legacy token's Bearer, `GET user` and 403s, on GitLab's docs (D375), and
F's 401s (15.7). ASSUMED: `CanPush`'s levels, an empty project's `default_branch`, a token limited
to some projects listing all (r6 14). Scripts serve shape D, both 429s, HTML, a 403 with
`ratelimit-remaining: 0`.

### 7.6 Tests (the builder runs these three classes)

- `GitLabRecognitionTests`: 7.2's forms under both endpoints; a namespace of 21 segments, not 22;
  refusals (`gitlab.example.com`, `gitlab.com.evil.example`, one segment, `/-/tree/main`, `..`, `%2F`,
  a query, `git://`, other loopback ports, `https` on the fake's port, GitHub's URLs); `SourceCanExist`, `PullRequestHeadRef` as `PullRequestRefs`';
  `CoverageOf`, `Covers` and the three pages, nested.
- `GitLabClientTests`: Bearer alone (the fake's authorization form); `%2F` as received; `Move`
  followed for a GET with the token, a POST to the old path `Validation`; another origin, four hops, a
  `Link` elsewhere `Malformed`, no request there; paging at `MaxPerPage = 2`; a repeat 304 with the same
  value, another token's not; 7.3's rows and messages; `Refusal` only on the create POST; a 409 or C
  body in made-up words classified alike, a 403 naming a rate limit or `api` unchanged; `RetryAt`'s four
  sources; a warning names GitLab, no query, token or the fake user's address.
- `GitLabProviderTests`: A1 for both kinds (one `GET /api/v4/user`), refused when unknown, revoked,
  expired, without User: Read, empty, non-ASCII; a user whose name is a made-up `example.com` address
  gives `Name` null; `github_pat_FAKE-octo-1` and `new string('a', 76) + "AZDO" + "abcd"` are
  `BadCredentials` with `Detail` `other-provider:github` and `other-provider:azure-devops`, and the fake's
  `Requests` stays empty; A2's query, cap, rows (nested, `internal`, fork, levels 20, 30, 40, empty, no
  archived); A3 by path, moved, fork; A4's fields and cap, no lookup for
  its own MRs, one for a fork's two, a hidden fork null, a failing lookup failing, 25 forks 20
  lookups, none next call, again after `ForgetKept`; A5 in one project and across forks (GETs, POST
  path and body by a recording handler), Draft titles, `/close` sent as typed, each refusal, one POST a
  call, none after a failed GET, a moved source.

Commits: (1) `GitLabUrls`; (2) `GitLabJson`, `GitLabProfile`; (3) `GitLabProvider`; each with tests.

### 7.7 At the merge; done when

No lagged list (nothing starts the app), no CLAUDE.md row, no picture. The whole default run follows
the merge that closes wave 2, whichever of WP-FA, WP-FL, WP-AZ and WP-GL is last (0.4). Done when the
three classes pass; the token goes to no other origin or header; no kind rests on GitLab's English or `scope`;
recognition takes no other host or port; 1.4's signatures hold; CI's five jobs are green.

**As built** (WP-GL, `f77a34f` to `3765433`; the reviews' and hunts' fixes `2c7f4cb`, `0309c38`, `c076f58`, `b47e015`, `570520a`, 2026-11-01):
- `GitLabClient.cs` holds `GitLabProfile` (`ProjectId`, `Url`) and a static `Create`; `GitLabUrls`'
  `SplitPathWithNamespace` reads an answer's path and `IsSegment` a segment by 7.2's rules.
- 7.2 refuses an authority with `?`, `#` or `\`, an ssh or scp-like one with `%`, `[` or `]`, and an
  https or http one with two `@`: git then reaches another host than the last `@`'s
  (`https://evil.example#@gitlab.com/g/p`, `ssh://evil.example%2F@gitlab.com/g/p`) or, curl
  rejecting it, none (MEASURED); ssh keeps the last `@`. An empty port (`gitlab.com:/g/p`) is the
  scheme's default, as git and curl read it.
- Words: a 408 or 5xx without GitLab's gives the shared "GitLab had a problem: HTTP <n>.", as
  GitHub's and 6.3's (WP-B's `Describe` shows its own `ServerError` text); another status `<code>
  <reason>`. `Detail` is shape F's code alone. A body not UTF-8 holds no words. A refusal needs a
  POST to `…/merge_requests`. Only a GET's redirect under the API's path, read decoded with no dot
  segment or backslash (`..%2F`), is followed; another is `Malformed`, nothing sent on
  (`<status>, a redirected POST`, `…, redirected outside the API`).
- `RetryAt`: `Retry-After`'s seconds or IMF-fixdate, else the next source, within a day of now; a
  `ratelimit-reset` past 9999 is `Malformed` (D380).
- Local refusals set `Provider` GitLab; `OtherProviderDetail` equals GitHub's. A1: a `username` not
  one segment, or holding the token in any case, is `Malformed`; `Name` is null when `ScreenEmail`
  changes it; `Login` keeps its case. A4's author is null unless one segment.
- The provider's own failures reach `MalformedAnswer` with the token (1.4's optional `token`, as
  `Failed`'s), screened as the core's are. A known limit: records (`RepositoryInfo`, rows, authors)
  are not (gitlab.com would have to name a project by the account's token).
- An answer's address is read only as absolute `http` or `https`. A2 and A4 keep the first row of
  each id (iid), at most their pages of `PerPage` (100), `MoreExist` when cut. A2 leaves out a row
  it cannot read, or a null one, with a warning each; `CanPush` true without `permissions`. A4 fails
  whole on a request null, numbered below 1 or without such a `web_url`; A5 on such a created one.
- A4 keeps at most 512 forks, oldest out first, by the token's SHA-256 and the id; a 403 or 404 as
  null, one past the cap not: 25 forks give 20 lookups, then 5, then none, after `ForgetKept` 20.
- A5: "differs" is `HostedRepository` equality; an unreadable head, read before the POST, posts nothing.
- For WP-S: the constructor calls `createHandler()` once; `ForgetKept` is internal. For WP-N:
  `TitleFor` does not trim. For WP-LF: copied answers are marked MEASURED, READ or ASSUMED. The
  fake is unchanged. `d6484ae` does not build alone (`c9301a4` adds the provider its fixture names).
- The lead's, for 16.3: 7a's `GitHubUrls` has the three authority gaps and the empty port's; 7a's
  GitHub A1 takes any username, its A2 and A4 throw on `[null]` and take any number; the core follows
  a non-GET's redirect for GitHub and WP-AZ, and a next URL that repeats, to the page cap.

## 8. WP-C: credentials, push guard and prompts on the new hosts

### 8.1 Goal and files

Git calls to a remote a new provider's account covers take its token through 7a's helper; another
organization's remote, and any URL git could read otherwise than the app, keep D82's order (D350,
D359, rulings 8 to 15). Wave 1, branch `wip/p7b-credentials`. Out: `CoverageOf` and recognition
(WP-AZ, WP-GL), the bindings' union and the guard's dispatch (WP-S), the clone (WP-B). 7a design 4
holds where not changed here.

Files: `CredentialBinding.cs` (8.2), `PullRequestRefs.cs` (8.4), `AskPassBroker.cs` (`:144`,
`AccountOf` `:831-841`), `OperationTexts.Hosting.Credentials.cs` (8.5), `PromptDialogs.cs` (the comment
at `:298-299`), `RepositoryOperations.Run.cs` (`RefusedDisplay` `:738-750` alone, then the lead's),
`RepositoryOperations.Remotes.cs` (the guard's comment at `:209-210`, for every provider), `HostingSessionStub.cs`, and `tests/VisualCommit.Git.Tests/AzureDevOpsCoverageVectors.cs`, new (8.7).
Classes: `CredentialBindingSelectTests`, `AskPassBrokerBindingTests`, `PromptGroupAccountTests`,
`RefusedTokenDisplayTests`, WP0's `PullRequestRefsTests` (extended); `AzureDevOpsCoverageVectorsTests`,
`CoverageGitMatchTests` (Git.Tests), `PushGuardProvidersTests` (App.Tests) (new).

### 8.2 `Select` with a coverage (D369)

With only `Origin` set the body is 7a's (`CredentialBinding.cs:29-55`) byte for byte; GitHub's coverage
is that. With any other member set:

1. **Coverage.** `OrganizationHost` without `Organization`, or `GitTwin` with it, throws
   `ArgumentException`; an `Organization` not matching `^[A-Za-z0-9-]+$` binds nothing. Covered hosts:
   `Origin` by 7a's `IsOn` (`:94-97`), and with `OrganizationHost` `https://<Organization>.visualstudio.com`
   on port 443, the host without case; never a wildcard (r7 0.9).
2. **G3.4's form** (`InPlainForm`, public, 1.2; WP-AZ's recognition calls it), one rule for both Azure
   DevOps hosts and GitLab. The text holds only U+0021 to
   U+007E and no `\`, `?` or `#` (rule 2, narrowed to printable ASCII); the path is empty or
   `/`-separated segments, none `.` or `..`, none empty but one trailing; a `%` only before two hex
   digits encoding none of `2E`, `2F`, `5C`, `25`, `00`-`1F`, `7F`; each segment, its escapes decoded,
   is valid UTF-8 (no overlong form, no surrogate, nothing above U+10FFFF), which covers `C0` and `C1`
   (`%20` and UTF-8 escapes of names pass). On `Origin` with `Organization`, `Select` (not `InPlainForm`, which takes no coverage, 1.2) also
   requires a first segment, non-empty, that matches the grammar and equals `Organization` without case. A URL whose
   path is empty or `/` alone is never bound there: `https://dev.azure.com`, `https://dev.azure.com/`,
   `https://dev.azure.com:443`, the same with a user (`myorg` or any other), and the fake's
   `http://127.0.0.1:<port>/`. Its per-URL key would make git ask the app's helper for every
   organization's URL on the host, a user-less one answered with the token (MEASURED on 2.31.1, 2.43.0
   and 2.56 by the design review). `InPlainForm` stays true for an empty path (GitLab,
   `<organization>.visualstudio.com`): the refusal is `Select`'s, not the form's.
3. **Users.** None, `Login` without case (the organization, GitLab's username; 0.2), and with
   `HelperUserIsAccounts` `x-access-token` (ruling 11). `oauth2@` or any other is another user (D275);
   no password.
4. **Twin.** With `GitTwin`, a bound URL of two or more segments whose path, one trailing `/` removed,
   does not end in `.git` (ordinal) adds, right after it, its text without `user@` and without that
   `/`, plus `.git`.

   4b. **Credential twin.** A bound URL, step 4's twins included, whose path (the text after the
   authority) holds a raw `!`, `$`, `&`, `'`, `(`, `)`, `*`, `+`, `,`, `;`, `=`, `:`, `@`, `[` or `]`
   adds, right after it, its `Url` with each such character of the path written `%XX` (upper-case
   hex), other escapes and characters kept. git's credential side decodes the whole path and re-encodes
   these characters (`credential.c:674-682`, `:223-238`), while `url_normalize` keeps a key's raw
   reserved character raw: the raw form's `credential.<url>.*` pairs never match their own URL, and its
   `http.<url>.proactiveAuth` pair does. The raw form's http pair gives 2.46 and later the proactive
   request; the twin's credential pairs give the reset and the app's helper on every git (MEASURED on
   2.31.1, 2.43.0 and 2.56 by the design review: raw only, APP 0 and the user's helper answers; with
   the twin, APP 1). A raw `"`, `<`, `>`, `^`, `` ` ``, `{`, `|` or `}` needs no twin: `url_normalize`
   escapes it on both sides.
5. **Drop.** (D379) An `http` or `https` URL (the scheme in any case) that `UrlParts.Of` cannot read
   leaves no URL on any covered host bound, whatever its text, and is never bound: .NET refuses a
   percent-encoded host (`%64ev.azure.com`, `dev%2Eazure.com`), which git's credential code and curl
   decode, so the app cannot know which host and path git uses (V26: git reads user `evil.example\`).
   So does an `http` or `https` URL whose host, as .NET reads it or as git's credential code does
   (after the first `@` before the first `/`, `?` or `#`, up to that character), holds a character
   outside U+0021 to U+007E (`gitlab<U+FF0E>com`, a fullwidth `dev.azure.com`, U+3002, U+200B):
   .NET keeps it as written, git's curl may map it into a covered host, and after the origin's
   redirect git builds the credential from the ASCII target without the URL's user (the hunt on the
   fixed code, As built). (D379) So does any URL in git's remote helper grammar, `<helper>::<address>`
   (`^[A-Za-z0-9][A-Za-z0-9+.-]*::`, an empty name too), whatever its address: git reads it as the
   address over `git-remote-<helper>`, for `ftp`, `ftps` and Windows' `http.exe` or `https.exe`
   remote-curl on it, so a binding's key answers another user's or an out-of-form address there,
   while the app reads no such URL (R1 extended; the hunt's third finding, As built).
   (D379) For this step a URL also counts as on a covered host when a measured redirect may lead it
   into the covered origin: scheme `http` or `https`, any port, a host that, without case and with one
   trailing `.` removed, is the covered host or `www.` followed by it (gitlab.com answers `http://`
   with 301 and `www.` with 308, r6 6.1; dev.azure.com `http://` with 302, r5; git follows and builds
   the credential again from the target, without the URL's user). Such a variant is never bound; with
   no user or the account's it fails the host closed when out of the form on an origin with
   `Organization` (the review's amendment, As built), else it drops nothing. Other users' URLs: fetch
   and push URLs on a covered host with a user outside 3's set, the user as git's credential code
   reads it (up to the first `@` before the first `/`, `?` or `#`, through white space and control
   characters: `bob<U+00A0>@`); a URL whose user git reads otherwise than the app counts as another
   user's not in 2's form (the hunt on the fixed code, As built). If one is not in 2's
   form (on `Origin` with `Organization` a first segment is still required, so an empty path fails, but
   it needs only the grammar, not equality; elsewhere 2's form alone), no URL on that host is bound
   (fail closed, ruling 10). Else a binding, twins of both kinds included, is dropped when another
   user's path equals its path or lies under it at a `/` (ordinal, case kept), and with `GitTwin` also
   when their projects do, each view without one final `.git` (D379: gitlab.com answers `…/p` with 301
   and `…/p.git/` with 308 into `…/p.git/info/refs`, r6 6.1, so another user's `…/g/p`, `…/g/p/`,
   `…/g/p.git` and `…/g/p.git/` each drop all four; Azure DevOps serves `…/r/` itself and keeps `.git`,
   r5, so its path rule stays), both put in git's
   credential view: every `%XX` decoded (2's form leaves none that forms `/`, a dot segment, `%`, `\`
   or a control character), the trailing `/` removed (the form allows one), then every byte outside
   `A-Z a-z 0-9 - . _ ~ /` written `%XX` in upper-case hex. git matches a `credential.<url>` key against
   the request in that view (`credential_from_url` decodes the path and trims its slashes,
   `credential_format` re-encodes it; g3 G3.2's READ notes). On the key, `url_normalize` decodes only
   unreserved escapes, upper-cases the other hex, escapes raw unsafe characters and keeps raw reserved
   ones, and the prefix match ignores one trailing `/`. The credential view on both sides is coarser
   than git's key view, so it never drops too little; what it drops beyond git (a key with a raw
   reserved character, which git never matches) only leaves that URL to the user's helpers (D82). A
   binding and its 4b twin have one view, so they drop together. MEASURED on 2.31.1, 2.43.0 and 2.56
   by the design review: a key `…/r%28x%29` answers `evil@…/r(x)` and `evil@…/r(x%29`; `…/r^x` and
   `…/r%5Ex` answer each other's `evil@` spelling; `…/r%5ex` answers `evil@…/r%5Ex`; `…/r{x}` answers
   `evil@…/r%7Bx%7D`; `…/r/` answers `evil@…/r`, on gitlab.com too (and on github.com, whose `Select`
   stays 7a's, 16.3). 7a's raw compare (`:100-102`) misses G0.5's `…/_git/%52` (MEASURED on 2.31.1 and
   2.56); its `./R` and `x/../` fail 2.
6. **Result.** `Url` is 7a's (D278), a twin's as step 4 or 4b writes it; distinct by `Url`, in the
   remotes' order; `SharedOriginOwner` is `Organization` on bindings on `Origin` when it is set
   (dev.azure.com and the fake), else null.

`<organization>@` URLs bind because the login is the organization, and git's `get` with
`username=<organization>` passes `IsAccountUser` (`AskPassBroker.cs:602-608`) unchanged. The helper
answers `x-access-token` (ruling 8; READ, for the live command to confirm); GitLab usernames compare
without case (ASSUMED, r6; harmless, git sends `x-access-token`).

### 8.3 The broker and the prompt group

- `:144` becomes `GIT_CONFIG_COUNT is not a number git accepts; no account's token is given to git.`
- `AccountOf`: the binding is the first on the prompt's origin, as now (one account per origin, 9.2).
  With a `SharedOriginOwner` the prompt carries the account only when (a) its URL's raw path (as the
  prompt writes it, never .NET's `AbsolutePath`, which resolves `..`, g3 G0.4), split at `/` after its
  leading one, has no segment that is empty, `.` or `..` or holds `%` or `\`, and its first segment
  equals the owner without case, or (b) the path is empty and its user is the owner without case or
  `x-access-token`; else null; then `IsAccountUser` as now. With `useHttpPath` off (macOS, Linux,
  every test run) `Username for 'https://dev.azure.com': ` carries no account, and
  `Password for 'https://<organization>@dev.azure.com': ` and
  `Password for 'https://x-access-token@dev.azure.com': ` do; with it on (Git for Windows' system
  file, r7 5) the path names it. `AnswerCredentialAsync` (`:653-740`) is unchanged (r2 7.4).
- Accepted: after a refusal in a scope, a path-less dev.azure.com Username prompt shows git's dialog
  (plan risk 39, wider there); typing the organization or `x-access-token` as user name there makes
  the next Password prompt the account's, refused without a dialog (D273), and the operation ends
  with 8.5's text.
- `PromptDialogs`' group rule (`:300`) stays: a shared origin's Username prompt carries the account only
  when its path names the organization, as the Password prompt after it then does; in the accepted case
  a group asks for that password once more.

### 8.4 Request refs and the push guard (ruling 14)

- `IsFetchableHead`: `refs/pull/<d>/head` or `refs/merge-requests/<d>/head`, `<d>` one or more ASCII
  digits, nothing after. `HeadOf`: `n < 1` throws `ArgumentOutOfRangeException`; GitHub
  `refs/pull/<n>/head`, GitLab `refs/merge-requests/<n>/head`, Azure DevOps null.
- `SourceCanExist`: GitHub 7a's (`GitHubUrls.cs:46-63`). GitLab: false for `refs/merge-requests/<x>/<rest>`
  whose first segment `<x>` is not all ASCII digits (iids are digits, r6, MEASURED). Azure DevOps: false
  for `refs/pull/<x>/<rest>` with such an `<x>`, and for `refs/pull/<digits>/head`, which never exists
  there (r5 0.4, MEASURED; 1.2). True otherwise: an unknown suffix (`/train`) may exist.
- The guard (`RepositoryOperations.Remotes.cs:211-214`) is unchanged; the session asks the provider that
  recognises the first configured `url`, another organization's included (D279, no request; WP-S).

### 8.5 Texts

```csharp
public static string GitCredentialsRefused(HostingProvider provider, string login) =>
    $"{HostingTexts.Name(provider)} refused git's credentials for {login}. If the token has expired or was revoked, sign in to {HostingTexts.Name(provider)} again in Settings, {HostingTexts.AccountsSection}.";
public static string TokenRefused(HostingProvider provider, string login) =>
    $"{HostingTexts.Name(provider)} refused the token of {login}. {HostingTexts.SignInInSettingsOf(provider)}";
```

GitHub's equal 7a's word for word (D351); the login is the account's `Login` in its own case, the
organization on Azure DevOps. `RefusedDisplay` reads `Services?.Hosting.AccountOf(refused.Provider)`
in place of `Account` (`:745`) and calls `GitCredentialsRefused(refused.Provider, login)` (`:749`).
7a's one-argument forms stay, delegating with GitHub, for 7a's tests (1.7). WP-B moves
`HostingTexts.Describe.cs:53` and WP-N moves `RepositoryOperations.CreatePullRequest.cs:454`.
`RefusedTokenDisplayTests` keeps 7a's assertions of the one-argument forms and adds the per-provider
ones.

### 8.6 What each git does (accepted; g3 G2, MEASURED on 2.31.1, 2.43.0, 2.56)

| | 2.31 to 2.45 (the containers' 2.43) | 2.46 on (2.56) |
|---|---|---|
| A bound URL's first request | anonymous, 401, the helper, the token; `<organization>@` sends `<organization>:` first (H2) | the token |
| A netrc entry for the host | answers the first 401, user-less URLs too (N3; plan risk 35) | never |
| gitlab.com's 301 to `.git` | the token through the twin (C3; without, the user's helpers) | the token |
| A moved GitLab project's old path (served directly, 200, MEASURED r6 6.1) | as the first row | the token |
| A same-host redirect of a git URL out of a binding (none known: gitlab.com serves a moved project's old path, MEASURED r6 6.1; Azure DevOps redirects none of its git URLs, r7 10.1 ASSUMED, visualstudio.com 200 MEASURED r5; a dot segment, which old git treats as a redirect, g3 G2, is never bound, 8.2) | the user's helpers (C1') | the token (C1) |

Pairs: 3 per bound URL, twins of either kind (8.2 steps 4 and 4b) counted (D340): 6 for a
`.git`-less GitLab URL or a URL with a credential twin, 12 for both; no `useHttpPath` pair (ruling 13);
`GIT_TRACE_REDACT=1`; none below 2.31 (D277). git-lfs asks the user's helpers, sending no path
without `useHttpPath` (g3 G4): accepted, as for 7a's bindings; never another organization's.

### 8.7 Tests

`AzureDevOpsCoverageVectors`: 48 rows with G3.4's answer, the bound text, its twin and owner, git's two
views and the APP column. G3.2's V1 to V26 and X1 to X12 (MEASURED); then the design review's, not
G3.2's: P1 `https://dev.azure.com`, P2 `https://dev.azure.com/`, P3 `https://dev.azure.com:443` and
P4 `https://myorg@dev.azure.com/` (in the form, not bound; their key, were it bound, reaches the app's
helper for `D/otherorg/p/_git/r` with no user and for its `otherorg@` form with `username=otherorg`)
and P5 `https://myorg.visualstudio.com` (bound as written, owner null; its key does not reach
`https://otherorg.visualstudio.com/p/_git/r`); Y1 `D/myorg/p/_git/r(x)`, twin `…/r%28x%29`, and Y2
`D/myorg/My%20P/_git/a&b`, twin `…/a%26b` (bound; the raw key alone APP 0, with the twin APP 1;
urlmatch basic; both views `myorg`), P and Y MEASURED by the design review; U1
`D/myorg/%E0%80%AE%E0%80%AE/otherorg/p/_git/r` and U2 `D/myorg/p/_git/r%ED%A0%80` (not valid UTF-8,
not bound) and U3 `D/myorg/p/_git/r%C3%A9` (bound), their git columns ASSUMED as X6's (APP 1, both
views `myorg`) until `CoverageGitMatchTests` first runs. Bound: V1, V2, V10, V12 to V16, X1, P5, Y1,
Y2, U3 (13); 35 not. Account `(AzureDevOps, dev.azure.com, myorg)`, `Origin` `https://dev.azure.com/`,
`OrganizationHost`, `HelperUserIsAccounts`.

| Class (project) | Proves |
|---|---|
| `AzureDevOpsCoverageVectorsTests` (Git) | each row's answer, `Url` and twin, `SharedOriginOwner` (`myorg`; null on V15, V16, P5), and `InPlainForm` per row by 8.2 step 2's rule alone, which has no user, host or organization clause (the first-segment clause on `Origin` with `Organization` is `Select`'s): true, though unbound, on P1 to P4, on V6, X9 and X10, which only the organization's grammar refuses (6.2), and on V11, V17 and V23, refused for their user or host; an organization outside the grammar binds nothing; the two `ArgumentException`s |
| `CoverageGitMatchTests` (Git; offline `git credential fill` in the broker's scope) | per row: the per-URL key in the three pairs reaches the helper as APP says (the broker's Debug line), Y1's and Y2's only with the twin; P1 to P4's key reaches it for `…/otherorg/p/_git/r` without a user (why `Select` refuses them), P5's not for otherorg.visualstudio.com; for each credential-view case of `CredentialBindingSelectTests`, dropped or not, the binding's pairs ask the helper with the other user exactly where `Select` drops; prefix keys for `myorg`, `otherorg`, `MyOrg`, `myorg.visualstudio.com` give G3.2's two views (`fatal` too); a bound row returns the token; G0.5's three spellings reach the helper beside `…/myorg/P/_git/R` and `Select` binds nothing on the host; GitLab: `…/g/p` alone misses `…/g/p.git/info/refs`, with its twin both reach it; `…/otherorg/p/_git/r` gives `Username for 'https://dev.azure.com': ` without an account, the path form with `-c credential.https://dev.azure.com.useHttpPath=true`; `…/myorg/…` without a token carries the account only with it on; wave 1's percent-encoded hosts reach the app's helper through the host's key, the user's helper unasked, and `Select` binds nothing beside them (D379); a user with white space or a control character (`bob<U+00A0>@`, `<U+3000>bob@`, `bob<U+0085>@`, `bob<TAB>@`, on gitlab.com and `myorg.visualstudio.com`) reaches the app's helper alone through the key, as another user, the user's helper unasked, and without it (`Select` binds nothing beside it) the user's helper is asked with that user (the hunt on the fixed code); given in UTF-8, a host outside ASCII (`evil@gitlab<U+FF0E>com`, fullwidth `gitlab` and `dev`) is kept as written by git's credential code, so the key misses and the user's helper is asked with `evil`, and `Select` binds nothing beside it (the hunt); not offline, against two local host servers: another user's URL that a 301 (`…/g/p` to `…/g/p.git`) or another port's 308 leads into a binding takes the token through its key in `ls-remote`, and `Select` drops it (D379), `bob<U+00A0>@` too (the hunt); bob's URL on 127.0.0.1 spelt outside ASCII (fullwidth digits and full stops, U+FF0E, U+3002, U+200B) that an absolute 301 leads into a binding takes the token where git's curl maps the host (2.56's does), and `Select` binds nothing beside it, the user's helper asked (the hunt); bob's `.git`-less URL behind a remote helper prefix that runs remote-curl on every git of the platform (`http.exe::` and `HTTP.EXE::` on Windows, `ftp::` and `ftps::` elsewhere) takes the token through the 301 and the binding, and `Select` binds nothing beside it, the user's helper asked (D379, the hunt's third finding). CI's 2.43 too |
| `CredentialBindingSelectTests` (Git) | 7a's cases through both overloads; GitLab (`Ada`): `.git` one binding, `…/p` and `…/p/` two, `…/g/p.git/` one and no twin, `Ada@` and `x-access-token@` bound, `oauth2@`, lookalikes, `http`, ssh, `/../`, `//`, `?`, `%2E%2E` not; `alice@…/g/p.git` drops `…/g/p` and its twin (D379), `alice@…/g/%70.git` drops `…/g/p.git`, `bob@…/g/p` drops `…/g/p/` and its twin `…/g/p.git` (D379: each of `…/g/p`, `…/g/p/`, `…/g/p.git`, `…/g/p.git/` as another user's drops each of them bound; `…/g/pq.git`, `…/g/p.gitx`, `…/g/p.git.git` drop by path alone), `alice@…/g/x/../p.git` leaves the host unbound, an `http` or `https` URL .NET cannot read (V26's form, wave 1's percent-encoded hosts, one naming no covered host) leaves every covered host unbound and another scheme's fails nothing (D379), so does an `http` or `https` URL that .NET reads whose host is outside printable ASCII (`evil@gitlab<U+FF0E>com/g/p`, fullwidth `gitlab`, `gitlab<U+3002>com`, `evil@` and no user and the account's on a fullwidth `dev.azure.com`, `http://` too, U+00AD, U+200B, U+00A0, a trailing U+FF0E, the fakes' 127.0.0.1 spelt so, and `bücher.example`, which names no covered host), while another scheme's, a punycode host and such a character after the host fail nothing (the hunt on the fixed code), so does any URL in git's remote helper grammar whatever its address (`ftp::https://evil@dev.azure.com/myorg/p/_git/r`, `ftps::` and `HTTP.EXE::` out-of-form addresses under V1, `https.exe::`, `ftp::https://evil@myorg.visualstudio.com/…`, `ftp::https://bob@gitlab.com/g/p`, the fakes' origins, the binding's own address, `gcrypt::ftp::…`, `1x::`, `::`, `hg::ssh://…`), while `+x::`, `x_y::`, a `::` after the scheme or in an IPv6 host and `ftp://` fail nothing, and `InPlainForm` needs an RFC 3986 scheme (D379, the hunt's third finding), `alice@…/h/p.git` and `bob@…/john.doe/p.git` (in the form) drop nothing; another user's `http://`, `www.`, other-port or `www.…com.` URL drops or fails as on the covered host, one with no user drops nothing, none is bound, `wwwgitlab.com`, `www.www.` and `ftp://` count as other hosts (D379); another user's name with white space or a control character before, inside or after it (U+00A0, U+0020, a tab, U+000B, U+0001, U+0085, U+2028, U+3000; `Ada<U+00A0>@`; with a password) leaves its own host unbound (gitlab.com, from `www.gitlab.com` and `http://` too; `myorg.visualstudio.com`, dev.azure.com kept bound; dev.azure.com; the GitLab fake's origin), and such a character after the first `/` or `?` is in no user (the hunt on the fixed code); Azure DevOps (`myorg`), dropped in the credential view: `…/r%28x%29` beside `evil@…/r(x)`, `…/r(x)` with its twin beside `evil@…/r%28x%29` and `evil@…/r(x%29`, `…/r^x` beside `evil@…/r%5Ex` and `…/r%5Ex` beside `evil@…/r^x`, `…/r%5ex` beside `evil@…/r%5Ex`, `…/r{x}` beside `evil@…/r%7Bx%7D`, `…/r/` beside `evil@…/r`; not dropped: `…/r(x)` beside `evil@…/R(x)`, `otherorg@…/otherorg/My%20Project/_git/r` and `evil@…/myorg/p/_git/R` beside `…/r`; an `otherorg@` `https://dev.azure.com/` beside `…/myorg/p/_git/r` leaves the host unbound; the fake's `http://127.0.0.1:<port>/` is not bound |
| `AskPassBrokerBindingTests` (Git) | the warning; three providers' bindings in one scope: 3 pairs per URL, 6 with a twin of either kind, 12 with both, no key with `usehttppath`, `GIT_TRACE_REDACT=1`; at 2.30.0 today's environment; `get` with `MyOrg`, `x-access-token` or no user answered, `otherorg`, `oauth2` not, each by its origin's account; `AccountOf` through `AskPassRunner.RunAsync` for 8.3's forms, an escaped segment, a `…/myorg/../otherorg/…` prompt (no account), an `x-access-token@dev.azure.com` prompt (the account's), visualstudio.com and gitlab.com (7a's rule); after a refusal the organization's Password prompt never reaches the handler, the path-less Username prompt does |
| `PullRequestRefsTests` (WP0's) | 8.4's heads; GitHub's 14 vectors and true `refs/merge-requests/foo/head`; GitLab false `…/foo/head`, `…/a/b/head`, `…/x/train`, `…//head`, true `…/5/head`, `…/5/train`, `refs/merge-requests/foo`, `refs/pull/fix/head`; Azure DevOps false `refs/pull/fix/merge`, `refs/pull/7/head`, true `refs/pull/7/merge`, `refs/pull/fix` |
| `PushGuardProvidersTests` (App; bare remotes) | only r7 9's rows that push (the refusals and the ref rules are `PullRequestRefsTests`' on strings; App.Tests ends the run, 0.6): with GitLab's `merge-requests/*/head` line `merge-requests/foo` pushed and the next fetch exiting 0 (r7 9's F3); with Azure DevOps' `pull/*/merge` line `pr/fix` pushed, on the account's remote and on one without an account (D279); with a `pull/*/head` line `pr/7` pushed |
| `RefusedTokenDisplayTests`, `PromptGroupAccountTests` (App) | 7a's assertions of the one-argument texts kept; the texts per provider; a refused GitLab or Azure DevOps fetch names its provider and login; a group keys an organization's prompts by the account, a path-less one by none |

`HostingSessionStub` gains `HostingSessionStub(GitCoverage coverage, string? token)` (7a's constructor
stays, with GitHub's coverage), `Coverage` and `RecognisedUrls` (`Dictionary<string, HostingProvider>`, ordinal) beside
`GitHubUrls`; `SourceCanExist` is `PullRequestRefs.SourceCanExist` of the URL's provider, `BindingsFor`
`Select` with the coverage, `AccountOf` and `IsOn` the stub's provider alone.

Also run 7a design 4.8's classes, `GitRepositoryPullRequestsTests`, `GitHubRecognitionTests` and
`HostingSessionCredentialTests`. HTTP on the new hosts' paths needs WP-F's host mode, so WP-S's and
WP-B's `ShellDriver` classes and the live command carry it. No visual check; part B pictures the
clones through the helper (0.5); no earlier picture changes.

### 8.8 Merge; done when

Commits: `PullRequestRefs`; `Select` and the Git classes; the broker; the texts, `RefusedDisplay`, the
stub and the App classes. The lead merges WP-C before WP-AZ's worktree is made and before WP-S; when
WP-C's merge is the last of wave 1 (WP-F, WP-H, WP-C), the whole default run follows it (0.4, D245),
which also measures App.Tests' new time (0.6). At closing the lead writes both twins, git's credential
view, the drop and 8.6 into `docs/architecture.md`'s "Prompts and credentials" and widens plan risks
35 and 39. Done when every class passes on the laptop and in CI on Windows, macOS, Linux (2.56) and
both containers (2.43); 7a's tests pass unchanged but for the warning's two pins
(`AskPassBrokerBindingTests.cs:97`, `:123`); `AzureDevOpsCoverageVectors`' 48 rows (G3.2's 38 and the
ten 8.7 adds, P1 to P5 among them) give G3.4's answer as 8.2 words it, on strings and in git; no token
is in an argument, URL, config value, log or file.

**As built** (WP-C, `67546a3` to `9d7d037` and the review's fixes, 2026-11-01). Where the build settles what 8.2 to 8.7 left open:
- `InPlainForm` needs a scheme before `://`, in RFC 3986's grammar (`[A-Za-z][A-Za-z0-9+.-]*`, so
  not git's `ftp::https`; the hunt's third finding, D379); its `%` rule covers all the text after `://`, its UTF-8
  rule each path segment; the path starts at the first `/` after `://`.
- D379 (R1, the lead's rulings on wave 1's findings): `NamedIn` is gone (every covered host is `http`
  or `https`); its literal test was narrower than git, not wider: `git credential fill` decodes
  `%64ev.azure.com` as curl does (MEASURED on 2.56).
- `x-access-token@` compares ordinal, as `IsAccountUser`. The account's user with a password is
  neither bound nor another user's (7a).
- The credential view keeps the leading `/`, so an empty-path binding (`https://gitlab.com`, P5) drops
  beside any other user's URL on its host. Order per URL: it, its credential twin, its `.git` twin,
  that twin's credential twin.
- Step 5 as the review amended it (for the lead to confirm): on an origin with `Organization`
  (dev.azure.com, the fake) a URL without a password, out of the form or with no first segment in the
  grammar, fails the host closed whatever its user: `…/r/..%5C…`, a raw `\` or `%C0%AE` under V1 get
  the token through V1's key with no user (MEASURED on 2.56). Any URL on a covered host with one
  trailing dot (`dev.azure.com.`, git's `match_host`) counts as another user's there whatever its
  user, never bound (V23): the key would ask only the app's helper, which binds no such host
  (MEASURED). 7a's frozen github.com rule has the same gap, which 16.3 does not list yet.
- D379 (R2): `CoveredHost.IsRedirectedFrom`. A trailing dot on the covered scheme and port keeps
  V23's rule above (git's `match_host` reads the key there, no redirect); every other variant takes
  R2's. 7a's github.com rule keeps the `http://` and `www.` gap too (for 16.3).
- D379 (R3): with `GitTwin` a binding drops when the two views meet as before or their projects do
  (one final `.git` after a non-empty segment removed, ordinal): `bob@…/g/p/sub` drops the twin
  `…/g/p.git` too. These drops reach beyond git's key view, so the credential-view cases' git check
  takes a case's dropped bindings together.
- 62 of D379's new and changed tests failed before it (the 12 in git at `Select`, their git half
  holding). R1's TLS transport (`http.curloptResolve`) is not in the harness, which has no TLS
  server; `credential fill` shows the key match. R4: no change (16.3, risk 39: the lead's).
- The hunt on the fixed code (2026-11-01): `UrlParts` ends the authority where
  `GitCommand.EndsAuthority` does (also at white space and control characters, for the prose it
  redacts), while git's `credential_from_url` reads the user up to the first `@` before the first
  `/`, `?` or `#`, and .NET reads it too (`bob%C2%A0`). Beside `https://bob<U+00A0>@gitlab.com/g/p.git`
  the binding of `…/g/p.git` stayed: git asked the app's helper alone, which refused bob, and never
  the user's (MEASURED by the hunt on 2.31, 2.43 and 2.56, for C0 controls, U+0085, U+00A0, U+2028,
  U+3000 and the like, before, inside or after the name); with a `.git`-less `bob<U+00A0>@…/g/p` the
  301 rebuilt the credential without bob and the token answered. `Select` with a coverage now reads
  git's user too and, where it differs from `UrlParts`', counts the URL as another user's not in 2's
  form, so its host fails closed (8.2 step 5). `UrlParts` and `EndsAuthority` are unchanged, so 7a's
  `Select` stays byte for byte: beside `https://github.com/o/r.git` it binds that and
  `https://bob<U+00A0>@github.com/o/r.git`, the user kept (against D278's `user@` removed), a gap for
  16.3 beside the trailing-dot and `www.` ones. Two `@` in the authority need nothing: .NET refuses
  them (R1). Failing closed also takes `Ada:p<U+00A0>w@` (git: user `Ada` with a password) as
  another user's; git asks no helper there, so only that host's bindings are lost. 21 new `Select`
  tests and 7 in git (their git half holding) failed before the fix.
- The hunt on the fixed code, its second finding (2026-11-01): .NET reads a host outside printable
  ASCII as written (`Uri.Host` `gitlab<U+FF0E>com`, its `IdnHost` `gitlab.com`; so for fullwidth
  `dev` and `gitlab`, U+3002, U+00AD and U+200B, and a trailing U+FF0E gives `gitlab.com.`), so
  `CoveredHost` saw no covered host and kept the bindings beside `https://evil@gitlab<U+FF0E>com/g/p`.
  Given the URL in UTF-8, as the app gives git every URL, git's credential code keeps the host's
  bytes, the key misses and the user's own helper is asked (MEASURED by the hunt on 2.43 and 2.56;
  the claim's fold to `dev.azure.com` came from its harness writing stdin in code page 437). But
  2.56's curl maps the host, and after an absolute 301 or 302 into the origin git builds the
  credential again from the ASCII target, without the user, and the token answers: MEASURED on
  2.56.0.windows.4 against a local host for 127.0.0.1 in fullwidth digits and full stops, with
  U+FF0E, U+3002 or a trailing U+200B (`CoverageGitMatchTests`), and by the hunt through a local
  proxy for gitlab.com's 301 and dev.azure.com's `http://` 302; 2.43's curl refused fullwidth
  letters and mapped U+FF0E elsewhere. `Select` with a coverage now takes an `http` or `https` URL
  whose host, as .NET reads it or as git's credential code does, holds a character outside U+0021
  to U+007E as R1's unreadable one: every covered host fails, whatever its user, one naming no
  covered host too (`https://bücher.example/x`; its punycode spelling is ASCII and fails nothing).
  For the lead: comparing `IdnHost` instead would rest on .NET's mapping, which curl builds differ
  from, so the fail is not narrowed; a remote on an IDN host beside a bound one loses the bindings,
  never a token. The transport test asserts the token taken only where git reaches the host (a curl
  that cannot map it never connects, and then asserts that no helper was asked); `Select`'s half
  holds on every git. 7a's github.com rule keeps the gap (`http://evil@<fullwidth github>.com/…`
  through github.com's `http://` redirect), for 16.3 beside the trailing-dot and `www.` ones. 18 new
  `Select` rows and 7 tests in git (at `Select`; the transport ones' token half holding) failed
  before the fix.
- The hunt on the fixed code, its third finding (2026-11-01; D379, R1 extended): git reads
  `<helper>::<address>` (`transport_get`, before any other reading; `is_urlschemechar`'s
  `[A-Za-z0-9][A-Za-z0-9+.-]*`) as the address over `git-remote-<helper>`, and `ftp`, `ftps` and
  Windows' `http.exe` or `https.exe` (any case) are remote-curl on it. `HasHttpScheme` took the
  text before the first `:` as the scheme, so neither R1's nor the non-ASCII rule ran, and
  `UrlParts.Of` gave null (`ftp::https://…`) or an empty host (`https.exe::…`): beside
  `ftp::http://evil@…/myorg/p/_git/r` the app's helper refused evil and the user's was never asked,
  and an out-of-form `..%5C` address under V1 and bob's `.git`-less GitLab URL through the 301 took
  the token (MEASURED by the hunt on 2.56.0.windows.4 and 2.43.0.windows.1, whose MinGit has no ftp
  helper but runs `http.exe::`). `Select` with a coverage now fails every covered host for any URL
  in that grammar, whatever its address, before any other rule: the reading that fails closed, for
  the lead (reading the address as git does would also have to follow a helper that runs git again
  on it, `gcrypt::ftp::https://…`; a `hg::` or `gcrypt::` remote beside a bound one loses the
  bindings, never a token). `InPlainForm` needs an RFC 3986 scheme, so recognition takes no such
  URL. 26 new string tests and the 2 transport tests (at `Select`, the token half holding) failed
  before the fix; the transport needs `git-http-backend`, which MinGit lacks, so CI's containers run
  its `ftp::` rows on 2.43. 7a's github.com rule keeps the gap (`ftp::https://bob@github.com/o/r.git`
  keeps the binding), for 16.3 beside R8's list.
- For the lead, two minors the hunt showed, not changed: (1) a remote on a host no coverage covers
  may 302 into an out-of-form path under a binding (X4's `..%5C` shape) and take the token there
  (MEASURED by the hunt on 2.43 and 2.56); `Select` cannot see it. Its options: in bound scopes
  `http.followRedirects=false` once and `http.<bound url>.followRedirects=initial` per bound URL and
  twin (3k pairs become 4k+1, 8.6 and D340's count; an unbound remote's redirect, a vanity domain's
  or `http://gitlab.com`'s, then fails in bound scopes), or the gap accepted in 16.3 and risk 39
  beside R4. (2) R2's any port makes every server on the fakes' 127.0.0.1 a variant of the Azure
  fake's host: beside its remote, `http://127.0.0.1:47398/john.doe/web-app.git` or
  `http://127.0.0.1:8080/` (no user, out of the form or no first segment in the grammar) fails it
  closed. Never a token, only a binding lost, and 2.6's remotes are not hit. Narrowing R2 (to the
  measured shapes, or no IP literal) amends R2's "any port" and takes away the other-port stand-in
  for `www.` in `CoverageGitMatchTests`' redirect theory, which would then need a TLS or DNS harness.
- `AccountOf`: a prompt path of `/` alone or with a trailing `/` holds an empty segment: no account.
- MEASURED on 2.56 by `CoverageGitMatchTests` (CI's 2.43 to confirm; the vectors' marks say so): U1
  to U3's git columns are X6's; P1 to P4's views `none`, P5's `vs-myorg`.
- `HostingSessionStub`: 7a's constructor keeps the login's case through a private one (`AccountKey`
  lower-cases it); `SourceCanExist` asks `GitHubUrls`, then `RecognisedUrls`, via `PullRequestRefs`.
  `PushGuardProvidersTests` proves the App's guard alone (local remotes, nothing bound): recognition
  with no account is WP-S's (9.6, 9.10).
- 8.5's texts were WP0's (`e8a275d`). `CoverageGitMatchTests`: 157 tests, 105 s alone (155 took 74 s,
  148 took 90 s, 129 took 48 s).
- For the lead: `docs/architecture.md:2257` quotes the old warning; `GitRepository.PullRequests.cs:30`
  names `refs/pull` alone (WP-P). For WP-AZ and WP-GL (and WP-S, which unions them): `Select` throws
  on `OrganizationHost` without `Organization` and on `GitTwin` with it.
- CI at WP-C's merge (run 41311809889 on `6586066`): the same-port cases of the redirect theory and
  the two `<helper>::` transport tests failed on Windows (2.56.0.windows.2, libcurl 8.22.0) and
  macOS (Homebrew's 2.56.0), at their "before" half: git asked the app's helper for bob, so no
  token went. The local host's 301 gave a relative `Location`, which curl resolves against bob's
  URL, his name kept, and whether git's rebuilt credential has it differs by curl: those two keep
  it, 2.56.0.windows.4 (8.23.0), CI's Ubuntu 2.56.0 and 2.43 drop it (MEASURED on the laptop with
  MinGit 2.56.0.windows.2 given the install's `git-http-backend`). gitlab.com's 301 names the
  whole address (`Location: https://gitlab.com/gitlab-org/cli.git/info/refs?service=git-upload-pack`,
  MEASURED 2026-11-01), so the tests' host now sends it absolute, as the IDN test's did, and the
  token is taken on both gits; no assertion changed. Behind a host that redirects relatively, such
  a curl keeps another user's name and D82's order is lost instead; `Select`'s drop holds either way.

## 9. WP-S: the session for three providers

### 9.1 Goal and files

`HostingSession` and `RepositoryHosting` (7a design 7) for three providers on D357's model
(rulings 1, 2, 5, 6; r1 P1, P12, P13). From its merge the desktop app has the new providers on
(`FromEnvironment`'s defaults); both harnesses keep them off unless a check gives a fake, so no earlier
picture changes. Wave 3, `wip/p7b-session`.

Files: `HostingSession.cs`, `JsonAccountStore.cs`, `RepositoryHosting.cs` and `.Avatars.cs`,
`HostingRules.cs`, `IHostingSession.cs` (bodies only; to WP-N at the merge), `HostingOptions.cs` (the off
handler's message names no provider), `AccountKey.cs` (docs), `RepositoryViewModel.Hosting.cs` (to
WP-P at the merge) and `.Avatars.cs`; 9.10's test classes, `ProvidersSessionSetup.cs` and
`tests/VisualCommit.Testing/Hosting/HostingSessionGitHubForms.cs` (namespace `VisualCommit.Hosting`, 1.9).

### 9.2 The session object

7a's constructor (7a design 7.3).
- **Providers**: one per endpoint `options` give, with its handler (a null one refuses every request;
  `FromEnvironment` gives each provider it turns on its `DefaultHandler`, 1.4), disposed with the
  session. Two on one web origin throw `ArgumentException` (the broker answers from the first binding
  on an origin, `AskPassBroker.cs:658`). The vault exists when any provider is on; `AvatarCache`
  and `AuthorMapStore` only when GitHub is (`HostingSession.cs:74-81`).
- **Construction lines** in `HostingProviders.InAccountsOrder`: `{Name}: {provider.Host}.` or
  `{Name}: off.`, GitHub's byte for byte (`:80,84`).
- **Entries**: per provider on, the provider (GitHub's typed, for A6), its `AccountSlot?` (7a's class,
  `:803-822`), its refusal check and its switch chain; slots swapped whole, read lock-free.
- **One gate** serialises the start's read and every provider's sign-in second half, sign-out, refusal
  and switch, so the file changes one write at a time; no network call under it. Per slot: record,
  then slot, then `AccountChanged(provider)` through `post`, once per change, then
  `OnAccountChanged(provider)` on each live hosting.
- **Internal**: `SlotOf`, `AnySignedIn`, `CallAsync`, `CallGitHubAsync` (9.6), `SwitchingOf`,
  `RefusalCheckOf`; 7a's `Slot`, `Switching`, `RefusalCheck` stay GitHub's.
- **Shims**: the class declares none of 7a's GitHub-named members; 1.5's default bodies serve callers
  through the interface, and 1.9's `HostingSessionGitHubForms` 7a's tests (about 65 calls in seven
  files).

### 9.3 `accounts.json` per provider

7a's file, `version` 1. `AccountsFile.For(HostingProvider, string host)`, `With(AccountRecord)` and
`Without(HostingProvider, string host)` replace `GitHubOn`, `WithGitHub` and `WithoutGitHub`
(`JsonAccountStore.cs:118-159`), matching `HostingProviders.Id` and host without case; every other
record is kept, an off or unknown provider's too (D292). `AccountRecord` gains `UserId` (`string?`,
last, `[JsonIgnore(Condition = WhenWritingNull)]`), so GitHub's and GitLab's records keep 7a's eight
names (`JsonAccountStoreTests.cs:42-43`). `Id` stays a JSON number, 0 for Azure DevOps (1.8; g4 D4 §6,
MEASURED; `JsonAccountStore.cs:50-55`); a 7a save drops `userId`, which 7b takes again at the next check
(9.5). The new providers' `avatarUrl` is null (D349); no field of a GitLab or Azure DevOps record holds an
e-mail address (6.4, 7.4). GitHub's record is 7a's, unchanged.

The key for all three is `new AccountKey(provider.Kind, provider.Host, account.Login)` (D357).

### 9.4 Start, sign-in, sign-out, the switch

- **`StartAsync`** (7a design 7.4): all off, nothing read. Else one `Load()` under the gate; per
  provider on, in order, its record takes 7a's states by `tokenState`, each `keychain` record one
  `vault.ReadAsync`, one after another; none: `{Name}: no account on {host}.`. Then `_started`, then the
  events. No request.
- **`SignInAsync(SignInRequest r, ct)`** (7a design 7.5 per provider): `r.Provider` off, `Validation`
  "{Name} is turned off." (GitHub's 7a's, `:28`) without a request. Else `CheckTokenAsync(r.Token,
  r.Organization, linked)` outside the gate; a failure changes nothing. Then 7a's steps on that provider's record: another key forgotten (D348),
  `UseForGit` kept or true, record, slot, event. Other providers are untouched. A check whose answer
  names the token (ignoring case), as given or as the request sent it (the core's `SecretsOf`: Azure
  DevOps' `Basic` credential too), in the login, the display name, the organization, the user id or
  the avatar's whole address is a malformed answer, with the provider's malformed text, before
  anything is kept or logged, so no record or sign-in line carries an echoed token (D382; the core
  already screens it out of its own warnings and failures).
- **`SignOutAsync(provider, ct)`**: 7a's steps for that slot, then its provider's `ForgetKept()`
  (`GitHubProvider.cs:311`; the others' `internal ForgetKept`, 1.4). Only GitHub's runs `ForgetAvatarsAsync`
  (`:661-713`; D291, D349).
- **`SetUseForGit(provider, value)`**: 7a's chain per entry; one switch writes one record.

### 9.5 The credential source and refusals

- **`GetAsync(account, ct)`**: 7a's 10 s wait (its warning naming `account.Provider`); a token only
  when the slot of `account.Provider` binds git and its key equals `account`.
- **An API 401** marks that slot as 7a's (same key, same token; 7a design 17.1).
- **`Refused(account)`** (D330): one check at a time per provider, so GitLab's never waits on GitHub's:
  `CheckTokenAsync(token, organization, lifetime)`, the slot's login as Azure DevOps' organization. A
  401 marks it, source "git". A 200 compares the user (D357): Azure DevOps' `UserId` (ignoring
  case), GitHub's and GitLab's `Id`. Another user marks it, source "git, another user"; the same keeps
  it with 7a's Warning, no write; a held null or 0 (a 7a save) takes the check's, writes the record and
  posts the event. Any other answer: 7a's Warning. Another organization's prompt never comes here
  (WP-C's `SharedOriginOwner`).

### 9.6 Recognition, bindings, lists, calls

- `Recognise(url)`: the providers on, in order, first answer. `SourceCanExist(remoteUrl, remoteRef)`:
  the recogniser's rule, true when none (ruling 14; the caller passes the first configured url, D279).
- `HostingRules.Bindings(…, effective, Func<AccountKey, GitCoverage> coverageOf)` beside 7a's `Uri
  webBase` form, which becomes `key => new GitCoverage(key, webBase)`. **The union**: for each provider
  whose slot binds git, in order, `Bindings(…, provider.CoverageOf)`, concatenated; `BindsGit` is any;
  `BindingsFor` (D332) and `BindingsForClone` are the union.
- `ListRepositoriesAsync(provider, ct)`: A2 through `CallAsync` with the slot's account.
- **`CallAsync<T>(HostingProvider provider, string what, HostedRepository? about, Func<IHostingProvider,
  string, HostingAccount, CancellationToken, Task<HostingResult<T>>> call, CancellationToken ct)`**: 7a's
  helper (`:412-436`) on that slot. Without a request: off, `Validation` "{Name} is turned off."; not
  signed in, `BadCredentials` "Not signed in."; `about` not `Covers`ed, `Validation` "Not the signed-in
  account's repository." (D350); each with `Provider = provider`. `CallGitHubAsync<T>` takes GitHub's
  typed provider (A6, D349).

### 9.7 `RepositoryHosting` for three providers

7a design 7.8, with:
- **Candidate**: `ListedCandidate` unchanged over every provider's remotes (ruling 5); its provider's
  slot decides the state, A3, A4, the refresh and the create. **`CanList`**: that slot signed in and
  its provider `Covers` the candidate.
- **States**: no candidate, `NoHostedRemote`; an Azure DevOps candidate while an Azure DevOps account
  is held, signed in or needing sign-in, that does not cover it, `OtherOrganization`: `Repository` the
  candidate, no list, failure or `ByRef`, no request (D366, D356); else 7a's. `RequestRemotes`
  is empty in both (1.5).
- **Bindings**: 9.6's union; `WantsUrlRewrites` is `AnySignedIn` as last raised with `Changed`, since
  the rules decide every provider's recognition (D290, r2 7.5).
- **`ByRef`** keys heads by (`HeadRepository`, `HeadRef`) with the record's equality, never `FullName`
  (`HostingRules.cs:102,121`): a GitHub mirror of a GitLab head's path maps nothing; an
  `<organization>.visualstudio.com` remote maps a `dev.azure.com` head (one `Host`).
- **Refresh**: 7a's, `CanList` for `IsSignedIn` (`:72`), through `CallAsync(candidate.Provider, …, about:
  listed)`; on `Loaded` the Debug line `{repo}: listed {n} open pull requests of {listed.FullName}` for
  every provider (`:393`), of any depth.
- **`OnAccountChanged(provider)`** (`:187-210`): `WantsUrlRewrites` false, `_rulesKnown` false. The
  candidate's provider: `CanList`, `Apply` and a refresh; else list, failure and `ByRef` cleared.
  Another provider: `Apply` alone. GitHub not signed in: the avatars go (9.8).
- **Create**: `CallAsync(base.Provider, …, about: base)`; on top of the held list as D293.
- `NoRemote` (`:19`) and `NullRepositoryHosting`'s: "This repository has no hosted remote."

### 9.8 Avatars, GitHub's alone (D367, D349)

`ShowAvatarsFor` (`RepositoryHosting.Avatars.cs:25-44`) acts only while the candidate is GitHub's and
GitHub's slot is signed in, and keeps only authors whose `Repository.Provider` is GitHub; A6 goes
through `CallGitHubAsync` (`:135-138`); `RunAvatarWork` (`HostingSession.cs:443-472`) checks GitHub's
slot. The avatars go (`StopAvatars`, `None`) when the candidate becomes none or another provider's
(`:102-109`) and at GitHub's sign-out, never at another provider's.

### 9.9 The App's partials, the log

- `RepositoryViewModel.Hosting.cs:161` passes the key of `AccountOf(HostingState.Repository.Provider)`.
  The rules read stays 7a's (7a design 7.10): one `^url\.` per refresh while any account is signed in.
- `RepositoryViewModel.Avatars.cs`: `UpdateAuthors` (`:107-131`) also needs `HostingState.Repository`
  to be GitHub's; `:52` also forgets the set for `OtherOrganization` and another provider's repository;
  `AuthorsOnGitHub` (`:143`) reaches only from GitHub's remotes.
- The log (7a design 7.12): a line naming GitHub as the account's service takes the slot's
  `DisplayName`, GitHub's byte for byte; "pull request" stays (1.9); the organization shows as the login.

### 9.10 Tests

`ProvidersSessionSetup` is 7a's `HostingSessionSetup` over the three fakes in process, each optional;
organizations `fake-org` and `other-org`, user `ada`, group `fake-group/sub`.
- **`HostingSessionProvidersTests`**: the construction lines; `HostingOptions.FromEnvironment(_ => null,
  log) with { CreateHandler = refusing, CreateAzureDevOpsHandler = refusing, CreateGitLabHandler =
  refusing }`, `refusing` giving a `RecordingHttpHandler` whose script fails every request (no socket):
  three on, each host's remotes recognised (`<organization>.visualstudio.com` too), the handlers'
  record empty; `none`: three off; one origin twice throws. Start: no record, no store call; three
  records, three reads, events in D353's order; an off provider's record kept unread. Sign-in per
  provider: one A1 to its fake, 9.3's record (`id` the number 0, `userId`), others untouched; a user
  whose display name (Azure DevOps, no `CustomDisplayName`) or name (GitLab) is a made-up `example.com`
  address: the `accounts.json` written holds no `@`; a second GitLab user forgets the first's entry;
  off fails without a request; two at once keep both records. GitLab's sign-out keeps
  `avatars/` and GitHub's slot; GitHub's deletes them, keeping GitLab's. `SourceCanExist`:
  `refs/merge-requests/x/head` refused on a GitLab remote, allowed on GitHub's; `refs/pull/x/merge`
  refused on Azure DevOps'.
- **`HostingSessionCredentialTests`** (extended): `GetAsync` by provider and key; GitLab's 401 marks
  GitLab alone; two providers' checks at once; Azure DevOps' check names the organization; another
  user's `userId` or `id` ends refused; a record without `userId` takes it; `BindingsFor` over GitHub,
  GitLab, `fake-org` and `other-org` remotes binds the first three, GitLab's twin too; with `AzureDevOps`
  `Service`, `https://fake-org.visualstudio.com/P/_git/r` is bound to `fake-org`'s account
  (`OrganizationHost`, 8.2), its `SharedOriginOwner` null.
- **`HostingRulesTests`** (extended): `origin` GitLab and `upstream` GitHub list GitLab's; `ByRef` by
  records (the mirror, both Azure DevOps hosts, a nested path without case).
- **`RepositoryHostingProvidersTests`**: each state per provider; a GitLab candidate with GitHub alone
  signed in asks neither fake; `OtherOrganization`: refresh, `ReadRepositoryAsync` and create leave the
  fake's count, `RequestRemotes` empty; a sign-in to `other-org` swaps two tabs' states; GitHub's
  sign-in refreshes no GitLab-listed tab; a three-segment Debug line; GitHub's sign-out keeps GitLab's
  bindings; avatars as 9.8. With `AzureDevOps = AzureDevOpsEndpoints.Service` and
  `CreateAzureDevOpsHandler = fake.CreateHandler` (in process, 3.3), a remote
  `https://fake-org.visualstudio.com/P/_git/r`: recognised, `Covers` true for `fake-org`, A4 in the
  fake's `Requests` with `Host` `dev.azure.com` and the path
  `/fake-org/P/_apis/git/repositories/r/pullrequests` (6.3), the state `Loaded`, `ByRef` mapped.
- **`RepositoryHostingProvidersWiringTests`** (`[AvaloniaFact]`, `ShellDriver.Start` with
  `ServeHosting7bAsync`'s fakes on port 0, 1.9; lagged list): Azure DevOps `origin`
  and GitLab `upstream` both bound, one `^url\.` per refresh, one Debug line per list; GitLab alone
  signed in, the Azure DevOps fake gets nothing; `OtherOrganization`, no request after the sign-in's,
  the remote unbound, and a fetch in `manifest/` with Fernhill-Labs signed in sends the fake no request
  with Fernhill-Labs' token: until git's credentials dialog is cancelled, each of its `GitRequests`
  (`AuthRemoteRequest`s, `FakeGitHub.cs:319`) for `harbor-co/Docks/_git/manifest` has no `UserName`
  and a `GivenUserName` null or `harbor-co` (git's empty-password retry, r7 H4b), never
  `x-access-token`, and the log has no "… answered from the account." line (`AskPassBroker.cs:738`,
  D277), as for 11.6's typed URL; a succeeded fetch refreshes the listed provider's list alone.
- **`JsonAccountStoreTests`** (extended): a three-record round trip with exact names; `For`, `With`,
  `Without`; **a copy of 7a's `AccountRecord`, `AccountsFile` and `AccountsJsonContext` (word for word at
  `5057892`) reads a file 7b wrote**, GitHub's record equal; that copy's save, read by 7b, has `UserId`
  null.
- **`RepositoryAvatarsTests`**: `AuthorsOnGitHub` leaves out a GitLab remote's branches; nothing is sent
  while GitLab's repository is listed beside a GitHub remote.

### 9.11 Commits, merge, risks, done when

Commits, each with its tests: store and rules; session; hosting; App; wiring. WP-S's merge closes wave
3, so the lead, through `scripts/heavy-run.sh`, runs the lagged list, then the whole default run (0.4,
D245): `VisualCommit.Hosting.Tests` passes whole, `Phase6ChecksS` and 7a's walk-through keep their
counts and pictures, and `AppSessionTests`' two off lines hold.

Risks: plan risk 39 holds per account; risk 34's items become three; until WP-P's merge a hand run
shows 7a's GitHub words for a new provider's repository (no harness run sees it).

Done when these classes and 7a's session and hosting classes pass, the latter unchanged; with the new
providers off nothing changes (no new git call or request); the tripwire finds no token; `HostingSession`
declares no GitHub-named member; CI's five jobs are green, each Test step under four fifths of its
limit (0.6).

**As built** (WP-S, `46ab4af` to its last commit, 2026-11-02), where the build differs or settles
what was open:
- `HostingSession`: a private `Entry` per provider on; `BindingsOf(effective, rulesKnown)` is the
  union `BindingsFor`, `BindingsForClone` and the tabs share; `Endpoints` is gone. A provider on
  without a handler gets `HostingOptions.RefusingHandler()`. One origin twice throws;
  `FromEnvironment` turns the later provider in D353's order off instead, warning `{variable} names
  the origin of {Name}: {Name} is off.` D382's screen (9.4 amended, `EchoedToken`) seeks each form
  of the core's `SecretsOf` in the user's names, id and avatar address and A3's
  names: a malformed answer. `avatarUrl` is GitHub's alone.
- 9.5: a 200 with the held user (or the check's unknown) logs 7a's warning; a held user unknown takes
  the check's under the gate while the slot holds that token.
- `RepositoryHosting`: a held account not covering the candidate: `OtherOrganization` on Azure
  DevOps, `SignedOut` elsewhere (unreachable). `WantsUrlRewrites` is the value last raised, in a
  `Changed` handler that event's (`Update` counts the rules known by it). `Changed` also comes when
  the listed provider's account (key, login) changes, for WP-P's row. A3's answer, the list or
  failure and a created request are kept with their account, unused under another (D348); a run's
  answer applies only for the account it asked and sent with; 7a's GitHub path too (D383). Another
  provider's candidate cancels the run in flight, ending its wait there (a disposed session drops
  its `Finish`), and drops every A3 read in flight of another provider, replaced or forgotten too
  (`_readings`), given up newest first before the run's and avatar run's stops free that
  provider's turn (none of another provider starts after; the core sends nothing for a request
  cancelled before its turn): none waiting is sent, none applies, keeps or warns; its own run
  starts at once. A read a `ReadRepositoryAsync` caller awaits goes on for it alone. Within one
  provider, 7a's `_again`. `StopAvatars` cancels the avatar run (9.8). `CallAsync`'s `what` and the
  Debug line pass names through `ScreenEmail` (D368); the state's stay as given.
- Outside 9.1: WP-H's `Http/HostingHttpClient.cs` (`SecretsOf` internal; `ExchangeAsync` sends
  nothing for a request cancelled before its turn) and one internal `SecretsOf` on each client and
  provider. Two guards have no test that fails without them, since no seam orders the races they
  close (`SemaphoreSlim` hands its count to a waiter cancelled just before a release; a run or
  avatar run between the drop and its stop): that core check, and `ReadInfoAsync`'s refusal of
  another provider's read; the lead accepted them so (2026-11-02).
- Tests: `HostingSessionGitHubForms.Account` is `AccountStatus` with `!` (C# 14); its `AccountsFile`
  forms keep 7a's store tests unchanged. `GiveUpObserver` stands for a late UI thread.
  `ProvidersSessionSetup` (`repositories: true`: an open request per fake and Azure DevOps
  organization) wraps the in-process Azure DevOps fake in `ServiceAnswers` for
  `AzureDevOpsEndpoints.Service`, its `Local(2)` addresses on dev.azure.com (WP-LF may give the fake
  a base); its disposal seeks its and `Track`'s tokens in the data folder and log in the tripwire's
  forms, and `FAKE-` but request ids in the log. 7a's copy is `JsonAccountStore7a.cs`. The wiring
  class matches git calls by `Repository.WorkingDirectory`; its tripwire searches them.
  `HostingContractsTests` (passed to WP-S): one test renamed.
- For WP-P: the left panel gets the key of `AccountOf(Repository.Provider)`, null without one. For
  WP-N: `IHostingSession.cs` changed `NullRepositoryHosting`'s text and three docs.

## 10. WP-A: accounts and sign-in (part A)

### 10.1 Goal and files

D353's three blocks, a sign-in dialog per provider, the window's entry for any provider, and part A's
checks (status steps 10, 14). Wave 4, after WP-S's merge, WP-LF's (the fix package of WP-LIVE's first
stage, 0.4) and part A's checks (D244); branch `wip/p7b-accounts`. GitHub's block and dialog keep 7a's
texts, ids and layout (D351, D362); 7a's pictures change in the intro only (0.7).

Owns 1.7's WP-A files, 7a's and 7b's part A files and `ShellDriver.Phase7a.Accounts.cs`; creates
`ViewModels/Settings/AccountBlockViewModel.cs` and 10.8's classes. In `SettingsDialogViewModel.cs`,
`SettingsScreenServices.SignIn` (`:59`) becomes `Func<HostingProvider, Task<bool>>?` and Save's step
(`:266-270`) `Accounts.ApplySwitches()`; `MainWindowViewModel.Settings.cs:74` becomes `SignIn =
SignInAsync`.

### 10.2 Texts (`AccountTexts`)

7a's constants stay word for word, and each `…Of(p)` returns them for GitHub; names and request words
come from `HostingTexts`' provider members (1.7). `{org}` is the Azure DevOps login. `AccountsIntro`,
one for all, composed: "With a GitHub, GitLab or Azure DevOps account, VisualCommit lists and creates
pull requests (merge requests on GitLab) and clones your repositories."

| Member | GitLab | Azure DevOps |
|---|---|---|
| `NeedsSignInRefusedOf` | 7a's with "GitLab" | 7a's with "Azure DevOps" |
| `AccountUseForGitNoteOf(p, login)` | 7a's with "GitLab" | 7a's with "to {org}'s repositories on Azure DevOps" (D350) |
| `SignOutTitleOf`; `SignOutQuestionOf(p, login, git)` | "Sign out of GitLab?"; "Sign out of GitLab as {login}? VisualCommit removes its token from this computer and no longer lists merge requests." + 7a's git sentence | "Sign out of Azure DevOps?"; "Sign out of Azure DevOps for {org}? VisualCommit removes its token from this computer and no longer lists pull requests." + it |
| `SignInHelpOf` | "Paste a personal access token from GitLab. A fine-grained token needs User: Read and Project: Read among its user permissions, and Project: Read, Merge Request: Read and Create, and Code: Download and Push for the groups and projects you work in; a legacy token needs the api scope." (D385) | "Type your organization, then paste a personal access token made for it in Azure DevOps with the scope Code (Read & write). The organization can be its name or its address on dev.azure.com or visualstudio.com." |
| `TokenLinkOf`; tip | "Create a fine-grained token on GitLab"; "Opens GitLab's form for a new fine-grained token in your browser." | "Create a token on Azure DevOps"; "Opens the organization's page for new tokens in your browser." |
| `LegacyTokenLink`; tip | "Or create a legacy token, filled in"; "Opens GitLab's form for a legacy token with the api scope in your browser." | — |
| `OrganizationLabel`; `SignInNotAnOrganization` | — | "Organization"; "Type the organization's name, of letters, digits and hyphens, or its address on dev.azure.com or visualstudio.com." |
| `SignInFailedOf(failure, org)`; `SignInUnreachableOf` | 7a's with "GitLab" ("GitLab refused the token: {message}."); "Could not reach GitLab." | `NotFound` "Azure DevOps has no organization named {org}."; anonymous "Azure DevOps did not take the token as a sign-in and answered as it does for a visitor. Paste the whole token, made for {org}."; other `BadCredentials` "Azure DevOps refused the token for {org}. Check that it was made for this organization and has not expired."; other kinds 7a's with "Azure DevOps" |
| `SignInOtherProviderTokenOf(p, other, org)` (D378) | "This looks like a {other} token. Paste a token made on GitLab." | "This looks like a {other} token. Paste a token made for {org} on Azure DevOps." |

`SignInOtherProviderTokenOf` serves GitHub's dialog too, with "on GitHub"; its `org` is the dialog's
organization, read only by Azure DevOps' text (null for the others); before "Azure DevOps" its "a" is
"an". It names no Settings, Accounts, since a provider that is off has no block there (D362).
Azure DevOps' refusals are fixed texts, since its 401 bodies are empty, HTML or a GUID's (g1 G0.2).
"Anonymous" is `failure.Detail == AzureDevOpsProvider.AnonymousDetail` and an unknown organization is
A1's `NotFound`, connectionData's 404 (1.4, 6.4), so the App reads no service sentence (r1). The
`NotFound` text rests on that 404, MEASURED without a token (r5 1.3) and with one by D374's read of a
made-up organization, which WP-LF copies into the fake (0.4, 3.7). GitLab's permission
names (r6 2.2, g2 G5.3) and Azure DevOps' scope (r5 5) are READ; the texts follow the forms the owner
sees when making the live tokens (status step 16). Log lines take the provider's name.

### 10.3 The Accounts section

`AccountsSettingsViewModel(IHostingSession? hosting, Func<HostingProvider, Task<bool>>? signIn,
IDialogService dialogs, IAppLog log, GitVersion? gitVersion = null)`: `GitHub`, `GitLab`, `AzureDevOps`,
`Blocks` (`HostingProviders.InAccountsOrder`), `IsChanged` (any block), `ApplySwitches()`
(`SetUseForGit(p, v)` per changed block), `FocusRequested`, `Dispose`. `AccountChanged` reaches
`e.Provider`'s block alone, so another provider's event never resets a toggled switch.

`AccountBlockViewModel` holds 7a's members (`AccountsSettingsViewModel.cs:55-129`) over
`AccountOf(Provider)`, plus `Provider`, `Label`, `IdSuffix` (`""`, `"GitLab"`, `"AzureDevOps"`),
`IdOf(string id) => id + IdSuffix`, `UserName` (Azure DevOps' `Account.Name`, else null) and `IsShown`:
`IsOn(Provider)`; GitHub's also while all are off or no session, showing 7a's `AccountsOff`. Sign in runs
`signIn(Provider)`; sign out asks `SignOutQuestionOf` and calls `SignOutAsync(Provider, CancellationToken.None)`
(D256); focus goes to `IdOf(…)` of 7a's ids. No git call or request when the screen opens.

View (`SettingsDialogView.axaml:490-562`): the intro, then a `StackPanel` (`Spacing="24"`) of three
`ContentControl`s, `AccountsGitHub`, `AccountsGitLab`, `AccountsAzureDevOps`, visible with `IsShown`,
drawn by one `DataTemplate`: 7a's two grids (`:497-561`) in a `StackPanel` (`Spacing="18"`), each id
`{Binding IdSuffix, StringFormat=<7a id>{0}}`, and `AccountUser` (`secondary`) under `AccountLogin` while
`UserName` is set. With GitHub's block alone the section draws 7a's pixels but for the intro, which must
wrap to 7a's two lines (610 wide; 7a's first line held 96 characters, the new sentence 148). Three
signed-in blocks need about 553 of 495 at 1100×700, so `SettingsSection` scrolls (D353), and a focused
lower button is brought into view.

### 10.4 The sign-in dialog per provider

`SignInDialogViewModel(IHostingSession hosting, HostingProvider provider, IFileLauncher? launcher,
IAppLog log, string? organization = null)` keeps 7a design 8.4's behaviour for each, and adds
`Provider`, the texts of 10.2, `ShowsOrganization`, `[ObservableProperty] string Organization`,
`ShowsLegacyLink`, `OpenLegacyTokenPageCommand` and `CanOpenTokenPage`:
- **The organization** is `AzureDevOpsProvider.OrganizationOf(Organization, provider.Endpoints)` (1.4,
  6.2), the one rule, of the provider `ProviderOf(AzureDevOps)` gives; the dialog parses nothing itself.
- **Sign in**: Azure DevOps' needs both boxes; a null organization gives `SignInNotAnOrganization`
  without a request; then D298; then a token whose `HostingProviders.TokenFormOf` (1.2) is a provider
  other than the dialog's is refused without a request, with `SignInOtherProviderTokenOf(provider,
  form, organization)` (D378). A token of the dialog's own form, or of none, goes on: no dialog requires its own
  provider's form (r5 5, r6 2.4). Then `SignInAsync(new SignInRequest(provider, token, organization))`.
  `NotFound` selects `SignInOrganization`'s text; other failures `SignInToken`'s.
- **Links**: `ProviderOf(provider).TokenPage(organization)`, Azure DevOps' enabled only with an
  organization (with the fake `http://127.0.0.1:47397/Fernhill-Labs/_usersSettings/tokens`);
  `SignInLegacyTokenLink` opens `GitLabProvider.LegacyTokenPage` (1.4).
- **Layout**: 7a's card; GitLab's second link 4 below the first; Azure DevOps' label and
  `SignInOrganization` (`dialogBox`, name "Organization") above `Token`, focused when empty; at most
  about 345 high (r3 4.2).

### 10.5 The window's entry

`MainWindowViewModel.SignInAsync(HostingProvider provider)`: off or no provider, false; signed in, true;
a sign-in dialog of any provider open, awaited, then the answer for `provider` with no second dialog
(ruling 22); else the provider's dialog, stacked (D104), Azure DevOps' prefilled with the held account's
login, else the `OwnerRoot` of the active tab's first Azure DevOps remote in `HostingRemoteOrder`.
Failures are logged and give false. WP-B, WP-P and WP-N move their own entries.

### 10.6 Earlier tests and pictures (0.7)

`Phase7aExpectations.A.cs`' `Intro` and its pin take the new sentence; 7a's tests read `model.GitHub`
and pass the provider; `ShellDriver.Phase7a.Accounts.cs` gains 1.9's provider forms beside 7a's. For
the 7b report's "Changes to expected results": "Phase 7a's `a01a` to `a11a` but `a09b` (23), and the
real window's `a01a`, `a02b` and `a05a`, change in the intro's two lines only; nothing below moves. Reason: D353's one sentence; the new providers are off in 7a's walk-through, so
only GitHub's block shows. The gate compares them by region (plan risk 32)."

### 10.7 Part A's checks

`Phase7bChecksA` over `ServeHosting7bAsync` (2.6) and a `FakeTokenStore`:
the intro's height equal to 7a's sentence in the same width; three blocks signed out; one, two and three
signed in, the last at the top and at the section's end (its largest offset: `ShellDriver.Wheel` turned
over `SettingsSection` until the offset stops changing, asserted before the capture); GitLab off, its
block gone; `Fernhill-Labs` bold over Ada's name; GitLab's dialog with both links' launcher records; Azure DevOps' dialog empty, prefilled from
`mobile-app`'s tab (the region behind is WP-P's, as in 7a design 8.7 item 9),
checking, refused by a 401, by the Anonymous 200 and for an unknown organization, and refused locally;
session only; one sign-out leaving the other blocks and records; each switch saved alone. Every method
ends with `TokenTripwire.AssertAbsent`; every picture passes `LayoutAudit.FindClippedText`. Real window:
both dialogs with the organization typed on an empty tab and a masked token; three accounts. For WP-R,
`Phase7bExpectations.A.cs` holds each provider's sign-in requests (route keys, in order), the
organization text typed, the box that has the focus, the R pictures' names and the three-account
picture's position as "the end", not a pixel value (the real window turns the real wheel until UI
Automation's `VerticalScrollPercent` is 100 and asserts it, 14.3).

### 10.8 Tests besides the checks

- `AccountsProvidersViewModelTests` (7b project, stub session): order; `IsShown`, all off too; 10.2's
  texts per state; `UserName`; one provider's event and another's switch; `ApplySwitches`; each command's
  provider; focus by `IdOf`; old git; `Dispose`.
- `SignInDialogProvidersTests` (7b project, real session over the fakes in process): the box through
  `OrganizationOf` on names, both URL forms, `<organization>@`, the fake's base, `app.vssps.visualstudio.com`, `%`, a
  space, an empty segment, another host; enablement; each failure's text (the fake's 404 and Anonymous
  answers MEASURED without a token, r5 1.3, 1.2; with one, the 404 by D374's read of a made-up
  organization and the refusals of made-up values by D373's probes, MEASURED as WP-LF copied them from
  stage 1, 3.7); each dialog given the other two providers' forms (`github_pat_FAKE-octo-1`,
  `glpat-FAKE-ada-1`, `new string('a', 76) + "AZDO" + "abcd"`) shows `SignInOtherProviderTokenOf` and
  no fake logs a request, while its own form goes on to its fake (D378); the login in the server's
  spelling after `fernhill-labs` is typed; the links' URLs and enablement.
- `SignInEntryProvidersTests` (Hosting.Tests, `ShellDriver`, fakes on port 0; lagged list): each title;
  off false, signed in true; GitLab's call under GitHub's open dialog opens none; one provider's two calls
  share a dialog; both prefills; the block's Sign in stacked over the screen.

The builder also runs 7a's part A classes, `SignInEntryTests` and `Phase6ChecksS`.

### 10.9 Risks; merge; done when

An intro of one or three lines would move every block in 23 pictures: the check measures it first.
The lead at the merge
runs part A's 7a and 7b classes, `SettingsDialogViewTests`, `Phase6ChecksS`, `Check_L31*` and the
lagged list, and inspects both parts' pictures. Done when they pass with 10.6's changes only, no token
is in any file, log, call record, automation value or picture text, and the report's paragraph is with
the writer.

**As built** (WP-A, `4b8edaf` to its last commit, 2026-11-06). Where the build differs from the text
or settles what it left open:
- `AccountsSettingsViewModel` subscribes to `AccountChanged` once and hands each event to
  `BlockOf(e.Provider)` (7a's one subscriber holds). `AccountBlockViewModel.Follow` is public; it
  resets a switch for another account and, untoggled, to its account's on every event (a toggle
  survives the same account's). While any block signs out, every block's buttons are disabled
  (`Joins`). An older git's note is 7a's for every provider. Ids: `{Binding IdSuffix,
  StringFormat='AccountLogin{0}'}` in one template over `ContentControl`s.
- Dialog: `HelpText`, `TokenLinkText`, `TokenLinkTip` added. The organization is `OrganizationOf`'s on
  the session's Azure DevOps endpoints, sent as it reads; off, nothing parses. A name of a provider's
  token form, or equal to the token box's text, is no organization: refused unsent, the link disabled,
  never echoed. Local refusals move no focus; only `NotFound` asks `SignInOrganization` (`dialogBox`,
  `Margin="0"`). `SignInFailedOf` reads `failure.Provider`.
- Prefill: the held account's login, else the active tab's `HostingState.Remotes` in
  `HostingRemoteOrder`. Driver: 1.9's forms plus `static AccountIdOf(p, id)`; a block's button is
  brought into view first. 7a's files changed as 10.6 says, plus `Phase7aChecks.A.cs:479`.
- `SignInDialogProvidersTests` serves the fakes on port 0 (1.8); the service's URL forms run on
  `AzureDevOpsEndpoints.Service` with a handler that refuses every request.
- Report: no expected result changed; `SettingsNavAccounts` at x 141 (±2). For the lead:
  `Check_A12` fails on open point 2's bound alone, asserted after its last step: at `a12b`'s offset 0
  7 px of Azure DevOps' `Git` row (its note wraps to three lines) lie below the viewport, over
  `PartA.A12FewPixels` (3); the texts and gaps are the report's, so it needs a ruling under "Changed in
  this report". Azure DevOps' disabled link (`a06a`, `a11b`) keeps the accent, as the report's "accent
  foreground" reads and `AssertDialog` asserts; a disabled look needs a ruling. Open point 8: A9 as
  given (ASSUMED).
- For WP-B, WP-P and WP-N: `TabServices.SignIn(p)` serves every provider (false while off).
  `SignInEntryProvidersTests` passes under the 1.5 s watcher lag.

## 11. WP-B: browse and clone (part B)

### 11.1 Goal and files

D354's clone button and the browser's switch, the palette's three commands, the browser per provider,
Azure DevOps' clone URL kept with its `<organization>@`, and `Describe` for three providers (status
steps 11, 14). A clone of any provider is bound at 7a's call point
(`WelcomeViewModel.cs:339-363`, D331 unchanged) by the session's union (WP-S 9.6, WP-C 8.2). Wave 4,
after WP-A's merge and part B's checks (D244); branch `wip/p7b-clone`. With GitHub alone signed in, or
the new providers off, no earlier picture changes (0.7).

Owns 1.7's WP-B files (with `AppCommand.cs`'s two values, `AppShortcutsTests.cs`, and the 7a classes
`WelcomeCloneFromGitHubTests` and `PaletteCloneFromGitHubTests` for the renamed members) and 7a's and
7b's part B files; it moves `HostingTexts.Describe.cs:53` to the provider form, and 7a's part B tests
keep the one-argument forms (1.7). At the merge `AppCommands.Palette.cs` passes to WP-P and
`HostingTexts.Describe.cs` to the lead.

### 11.2 Texts

`CloneTexts` (7a's constants stay; each `…Of(p)` returns them for GitHub): `FromButtonOf(p)` "From
GitLab…", "From Azure DevOps…"; `FromAnAccount` "From an account…"; `DialogTitleOf(p)` "Clone from
{name}"; `PaletteCommandOf(p)` "Clone from {name}…"; `MoreExistOf(p)` for GitLab "Only the 1,000 most
recently active repositories are listed." (WP-GL's 10 pages by `last_activity_at`), null for Azure
DevOps (unpaged); SSO GitHub's alone. Log: `Listing {name} repositories failed: …`, GitHub's byte for
byte (`Phase7aExpectations.B.cs:125-127`).

`HostingTexts.Describe(failure, login)` (`HostingTexts.Describe.cs:45-63`) names `failure.Provider`;
GitHub's results stay 7a's:

| Kind | Text |
|---|---|
| `Network` | the message as it is |
| `BadCredentials` | with a login `OperationTexts.TokenRefused(p, login)` (1.7); without, the message with a full stop, then `SignInInSettingsOf(p)`; an empty message (Azure DevOps' empty 401, g1 G0.2), `SignInInSettingsOf(p)` alone |
| `RateLimited` | "{possessive} rate limit was reached. Try again after {0}." or "… in a few minutes." (`HostingTexts.Possessive`, 1.7) |
| `SsoRequired` | GitHub's; another provider's as the last row |
| `ApiVersion`, `ServerError`, `Malformed` | "This version of VisualCommit needs an update to work with {name}.", "{name} had a problem. Try again later.", "{possessive} answer could not be read." |
| other | "{name} answered: {message}."; an empty message "{name} answered without a reason." |

### 11.3 The clone form's button (D354)

`WelcomeViewModel`: `ShowsBrowseButton` (any provider's account signed in; needing sign-in counts as
none, D302) replaces `ShowsGitHubButton` (`:106`); `BrowseButtonText` is `FromButtonOf` of the one
signed-in provider, `FromAnAccount` with two or three. The `AccountChanged` handler (`:407`) raises both.
`BrowseCommand` replaces `BrowseGitHubCommand` (in `:97`'s list) and runs `BrowseAsync(null)`;
`public Task BrowseAsync(HostingProvider? provider)` replaces `BrowseGitHubAsync` (`:243-253`): the
signed-in providers in `HostingProviders.InAccountsOrder`; none, or `provider` not among them, nothing;
else the browser with `provider ?? providers[0]`, `hosting.ListRepositoriesAsync`, and
`p => AccountOf(p)?.Account.Login`; `_ = dialog.Start()` before showing. `WelcomeView.axaml:181-198`:
the button's text and automation name bind `BrowseButtonText`, its id stays `CloneFromGitHubButton`; a
wider text narrows only the URL box.

### 11.4 The browser per provider

`CloneFromGitHubDialogViewModel(IReadOnlyList<HostingProvider> providers, HostingProvider provider,
Func<HostingProvider, CancellationToken, Task<HostingResult<RepositoryList>>> list, IAppLog log,
Func<HostingProvider, string?>? login = null)`; 7a's constructor (`:79`) stays, with GitHub alone.
- `Providers`, `ShowsSwitch` (two or more), `[ObservableProperty] HostingProvider Provider`; `Title` is
  `DialogTitleOf(Provider)` and raised with it.
- One load per provider, begun when it is first shown, on the dialog's one cancellation (Cancel, Esc,
  D234); each provider's rows, status, error and lines are kept and shown while it is; an answer for a
  provider not shown changes nothing on screen. A switch keeps `Filter` and clears `Selected`.
- The switch: under the title, 8 above the filter, flat buttons 4 apart in the settings screen's scope
  switch's look (`VcFlatButton`, 28 high, `Padding="10,0"`, `selected` on the shown one;
  `SettingsDialogView.axaml:207-215`, `:613-616`; no combo box, D216); ids `CloneFromGitHubProviderGitHub`,
  `…GitLab`, `…AzureDevOps`, names the display names. Hidden with one provider: 7a's card.
- Rows (`GitHubRepositoryRow.Of`, `:19-56`): `FullName` is `HostedRepository.FullName` (1.4); `CloneUrl`
  keeps an Azure DevOps URL whose user part is exactly its `OwnerRoot`, without case and without a
  password, as the API gave it (ruling 15, r7 10.1); any other user part is removed as 7a's.
- The full name (`CloneFromGitHubDialogView.axaml:47`) cuts at its start (`LeadingCharacterEllipsis`),
  so a nested path keeps the repository's name; the item's tooltip (`:105`) holds it whole. A name that
  fits draws as before, pixel for pixel. Ids stay `CloneFromGitHub*` (1.7).
- About 8,480 Azure DevOps rows fit in 10 MB (g1 G4.4): the `ListBox` virtualises and the filter
  rebuilds in place, as 7a's.

### 11.5 The palette's three commands (D354)

- `AppCommand.cs:57-59`: `CloneFromGitLab`, `CloneFromAzureDevOps` right after `CloneFromGitHub`; only
  the keys-only values after it move, as in 7a.
- `AppCommands.Resolve` (`:167-171`), per provider: `new(new(shell.Services.Hosting.IsOn(p), true,
  PaletteCommandOf(p), null, null), () => Start(shell.CloneFromAccountAsync(p), PaletteCommandOf(p)))`.
  `AppCommands.Palette.cs:83` adds the two after GitHub's, in D353's order; the harnesses keep them off,
  so phase 6's K10 and 7a's `b07a` stay.
- `MainWindowViewModel.CloneFromAccountAsync(HostingProvider provider)` replaces `CloneFromGitHubAsync`
  (`MainWindowViewModel.Commands.cs:184-202`): unless that provider's account is signed in,
  `_tabServices.SignIn(provider)`, whose false stops with no tab; then the form and
  `welcome.BrowseAsync(provider)`. `AppShortcutsTests`' keyless list gains both.

### 11.6 Tests

- `CloneFromAccountDialogViewModelTests` (7b project, a delegate per provider): one provider, no switch,
  its title; two and three in order, the asked one shown; each list asked once however often shown; the
  filter kept, the selection cleared; a late answer for a hidden provider; GitLab's 1,000 line, none for
  Azure DevOps, SSO never; `Fernhill-Labs@` kept, `Fernhill-Labs:x@` and `alice@` removed; Cancel
  cancels every load; the log line per provider.
- `HostingTextsDescribeProvidersTests` (7b project): 11.2's rows for GitLab and Azure DevOps, with and
  without a login and `RetryAt`, and the empty messages; 7a's `HostingTextsDescribeTests` pass unchanged.
- `CloneFromAccountTests` (Hosting.Tests, `ShellDriver`, fakes on port 0; lagged list): the button
  hidden, "From GitHub…" at 7a's width, "From GitLab…", "From an account…" with two, following
  sign-outs; clones of `northfield/platform/web-app` and of `mobile-app` (`Mobile%20App`) through the
  app's helper with no credentials dialog, `remote.origin.url` as chosen (`Fernhill-Labs@` kept), no
  `credential` key, the tripwire; a typed `.git`-less GitLab URL bound with its twin (on CI's 2.43
  containers through the fake's 301, 4.5, which proves g3 G2's twin over HTTP); with Fernhill-Labs
  signed in, `harbor-co`'s typed URL asks git's credentials dialog (D350), and until the dialog is
  answered no token goes there: each of the Azure DevOps fake's `GitRequests` for
  `harbor-co/Docks/_git/manifest` has no `UserName` and a `GivenUserName` null or `harbor-co` (git's
  empty-password retry with the URL's user, r7 H4b, g3 G2.3), never `x-access-token`, and the log has
  no "… answered from the account." line (`AskPassBroker.cs:738`, the one place the app gives git a
  token, D277); each palette command listed with its fake alone, signing in first (false leaves the
  tabs), its browser on its provider.

The builder also runs 7a's part B classes, `AppShortcutsTests`, `PaletteEntriesTests`,
`CommandPaletteViewTests`, `PickDialogViewModelTests`; `"*Phase1Checks.Check_13*"`, `"*Check_14*"`; in
`Phase6Look` `"*Check_L26*"`, `"*Check_L32*"`; in `Phase6` `"*Check_K10*"`.

### 11.7 Part B's checks

`Phase7bChecksB` (0.5's row): "From GitLab…" and "From Azure DevOps…" with one account; "From an
account…" with two and the switch; each browser listed, loading (`HoldNext`), failed in its provider's
words, with `customer-onboarding-and-identity-verification-service` and
`invoice-generation-and-reconciliation` cut and their tooltips; the clones above; the palette's three
commands, one signed out signing in first. No picture shows an opened repository of a new provider
(WP-P's merge changes it): a clone is asserted through git, the call log and the tripwire. Real window:
GitLab's and Azure DevOps' lists, and a clone of `web-app` (`northfield/platform`, a nested path) and
of `mobile-app` (`<organization>@`, `%20`) in the real process, Git for Windows' `sh` running the helper
(14.4). Each R list picture is taken with that provider alone signed in: no switch, the filter empty,
nothing selected. It is reached from the welcome page's "From GitLab…" or "From Azure DevOps…" after a
sign-in through Settings closed by Esc, as 14.4 reaches it. The switch pictures are not R. For WP-R,
`Phase7bExpectations.B.cs` holds, for GitLab and Azure DevOps each, the button text, the row list, the
row to choose (`web-app`, `mobile-app`), its clone URL (through `WithPorts`) and the R pictures' names.

### 11.8 Risks; merge; done when

The lead compares `b01a`, `b03a`, `b06a`, `b07a`, phase 1's `13a` to `14b` and phase 6's `l26b` and
`l32b` with references by region (0.7). At the merge the lead runs part B's checks, 11.6's classes and the lagged list. Done when they pass, part
B's pictures are inspected, nothing writes or logs a token, and no earlier picture changed beyond
D248's box.

## 12. WP-P: requests in the left panel and the graph (part P)

### 12.1 Goal and files

7a's section, menus, checkout, badge and branch rows' number (7a design 10, 11.2) for the listed
repository's provider, in its words and numbers (D351, D355); D356's row. Status steps 12 and 14; rulings 5, 19, 21, 23, 24; D361, D362.
Wave 4 after WP-B's merge and part P's checks (D244); branch `wip/p7b-requests`. No new
request, git command or token handling. With GitHub listed every text, row, menu and pixel is 7a's.

Files: 1.7's WP-P files with `OperationTexts.Hosting.PullRequests.cs`, `AppCommands.Palette.cs` (its
comment `:217-218`) and `GitRepository.PullRequests.cs`; `RefListRows.cs`; one argument at
`RepositoryViewModel.Hosting.cs:161` (from WP-S); `RepositoryViewModel.Menus.cs:458`'s description,
`signing in to {P}`; Git.Tests'
`GitRepositoryPullRequestsTests.cs`;
`LeftPanelView.axaml(.cs)` unchanged unless a test shows otherwise; 7a's part P and G files but
`RepositoryAvatarsTests.cs`, unchanged; in the 7b project `RequestTestData.cs` (2.6's requests as
records; WP-N reads it), 12.7's classes and part P's files; Hosting.Tests' `RequestOperationsProvidersTests.cs`.

### 12.2 Texts (`OperationTexts.Hosting.PullRequests.cs`)

7a's members stay word for word; each provider form returns them for GitHub and is built from 1.7's
`HostingTexts`: `{P}` `Name`, `{R}` `Request`, `{Rs}` `Requests`, `{RT}` `RequestTitle`, `{RsT}`
`RequestsTitle`, `{N}` `Number`; `p` is the listed repository's provider, or a request's
`BaseRepository.Provider`.

| Members (`p` first) | Texts |
|---|---|
| `SignInToSee`, `LoadingRequests`, `NoOpenRequests`, `MoreRequests` | `Sign in to {P} to see {Rs}…`; `Loading {Rs}…`; `No open {Rs}`; `More than 300 open {Rs}` |
| `RequestHeader`, `RequestOfBranch`, `RequestToolTip` | `{N} {title}`; `{RT} {N}: {title}`; 7a's with `{N}` |
| `FetchRequest(p, n, remote)`, `FetchedRequest` | `Fetch {R} {N}` / `Fetching {R} {N} from {remote}…` / `{RT} not fetched` / `Git could not fetch {R} {N} from {remote}.`; `Fetched {N} into {branch}` |
| `OtherOrganization(repo, account)` and `…ToolTip` (D356) | `In {repo}; signed in to {account}`; `This repository is in the {Name(AzureDevOps)} organization {repo}; the signed-in account is in {account}, so nothing is asked of {Name(AzureDevOps)} for it. To see its {Requests(AzureDevOps)}, sign out and sign in to {repo} in Settings, Accounts.` |
| `MenuTexts`: `RefreshRequests`, `OpenRequestsInBrowser`, `CreateRequest`, `OpenRequest`, `RequestsLoading`, `AddRemoteFor` | `Refresh {Rs}`; `Open {Rs} in browser`; `Create {R}…`; `Open {R} {N}`; `{RsT} are loading`; `Add a remote for {fullName} to check out this {R}` |
| `MenuTexts.HeadNotNamed(p)`, `NotFetchedBy(remote, branch)` | `{P} names no repository for this {R}'s branch`; `{remote} does not fetch {branch}` |

`OpenInBrowser`, `CopyLink`, `PullRequestDraft` and `FetchHeadChanged` serve all three; the sign-in
item is `HostingTexts.SignInToItemOf(p)`.

### 12.3 The section (`LeftPanelViewModel`)

- **Title.** `AddPullRequests` (`:416-461`) titles the header `{RsT}` of `state.Repository.Provider`
  whenever there is a `Repository` (ruling 24). `NoHostedRemote` keeps the placeholder line exactly
  (`:419-422`; phases 0, 1, 3 and 6 pin it, r3 0.10); the key stays `section:PullRequests`.
- **States**: 7a design 10.3's, plus `OtherOrganization`: count 0, it toggles, one row of the appended
  `PullRequestMessageKind.OtherOrganization` (no click) naming the remote's `Repository.OwnerRoot` as
  written and the account's login in its own case: `UpdatePullRequests` gains `string? accountLogin =
  null` after `account`, passed at `RepositoryViewModel.Hosting.cs:161` as
  `AccountOf(provider)?.Account.Login` (else the key's, which `AccountKey` lower-cases). No list, no sign-in (D356); `_keptList` goes as
  for `NoHostedRemote` (`:126`).
- **Rows** take `{N}` (`PullRequestRows.cs:37,48`). The filter (`LeftPanelViewModel.cs:313-317`) matches
  `{N}` (`!21` and `21` on GitLab, never `#21`), title, author and head label.
- **Labels.** `HeadLabelOf` (`PullRequestRows.cs:80-84`) uses a new `public static string
  BranchLabelOf(HostedRepository repository, string branch)`, which WP-N also uses: GitHub and GitLab
  `{Owner}:{branch}` (GitLab's namespace path); Azure DevOps `{Name}:{branch}`, a fork being in the
  base's organization (r5 3.4), or `{project}/{Name}:{branch}` across projects. A head without
  repository shows its branch (7a's).
- **Branch rows** (`LeftPanelViewModel.cs:578-582`; `RefListRows.cs:151,165-166`) take the
  `PullRequest` in place of the tuple, showing `{N}` and `RequestOfBranch`. `ByRef` is the listed
  provider's (WP-S 9.7): `web-app/`'s GitHub `upstream` branches show none.
- `.Actions.cs`: `OtherOrganization`'s header is `Section`, its row `None` (a disabled button whose
  tooltip shows, 7a design 17 item 7).

### 12.4 Menus (`RepositoryMenus.Hosting.cs`)

Pure (D86); `context.Hosting` stays null for `NoHostedRemote` (`RepositoryViewModel.Menus.cs:68`).
Every hook reads `RequestRemotes` (1.5) for `Remotes` (`:82`, `:111-118`): a remote branch has an item only when its remote is a key there, a local branch only when it is
not empty, a drop only onto such a base (`IsGitHubBase` becomes `IsRequestBase`). While
`OtherOrganization` every branch and drop hook gives null whatever `RequestRemotes` holds (WP-S 9.7
makes it empty): no menu offers a create there (D350).
- `ForPullRequest` (`:24-47`): 12.2's header, a gone one's `{N}` (`:32`).
- Section (`:54-70`): header `{RsT}`; signed out `SignInToItemOf` and `OpenRequestsInBrowser`;
  `OtherOrganization` `OpenRequestsInBrowser` alone (D376: the user's own browser; the app sends
  nothing); else `RefreshRequests`, disabled with `RequestsLoading` while loading, and
  `OpenRequestsInBrowser`.
- Branch and drop items: `OpenRequest(p, n)` or `CreateRequest(p)` (WP-N). The palette's request items
  come through `ForPullRequest` (`AppCommands.Palette.cs:219-222`).

### 12.5 Checkout (D361)

`PullRequestCheckout.Plan` stays pure over `RequestRemotes` in `HostingRemoteOrder` (`:50`).
1. **Head on a remote**: 7a's step 1, every provider: a request of the repository itself
   (`feature/search`, `feature/sign-in`), and a fork that is one of the remotes.
2. **Otherwise.** GitHub 7a's. GitLab the same with `PullRequestRefs.HeadOf(GitLab, iid)`, which
   gitlab.com keeps in the target for a fork's request too (r6 4.2, MEASURED); label `Check out
   {BranchLabelOf}`; local names 7a's with the namespace path's `/` made `-`, `mr-{iid}` for a head
   without repository (an unreadable fork, or past WP-GL's 20 lookups). Azure DevOps has no head ref (r5
   0.4, MEASURED): never fetched; disabled `Check out {label}` with `AddRemoteFor(p, the fork's full name)`
   for a fork that is no remote here, `HeadNotNamed(p)` without `HeadRepository`, `NotFetchedBy` for its
   own branch that the remote's fetch lines leave out. `DoubleTapRefRow`
   (`RepositoryViewModel.Menus.cs:433-437`) starts only a plan without a reason.

`RepositoryOperations.PullRequests.cs`: `FetchPullRequestAsync` fetches `PullRequestRefs.HeadOf(p, n)`
(`:156`) with 12.2's texts. `OpenPullRequestsInBrowserAsync` (`:115-133`), whose `:125-131` WP0 points
at GitHub's `PullRequestsPage(repository)` (1.10), opens
`ProviderOf(repository.Provider)?.PullRequestsPage(repository)` of `Repository`, else of
`RequestRemotes`' first, in place of WP0's GitHub-only call; `:127`'s log line says "no hosted
remote". `SignInToHostAsync` (`:135-145`): nothing
for `NoHostedRemote` or `OtherOrganization`, else `Services?.SignIn?.Invoke(state.Repository.Provider)`.

`GitRepository.PullRequests.cs`: the guard (`:30-33`) says "Only a request's head
(refs/pull/<number>/head or refs/merge-requests/<number>/head) is fetched into FETCH_HEAD, not
{sourceRef}."; `IsPullRequestHead` goes, unused. The test measures FETCH_HEAD after
`refs/merge-requests/5/head` on 2.56: `<sha>\t\t'refs/merge-requests/5/head' of <url>` (ASSUMED from 7a
design 10.5's `refs/pull` line), so `FetchHeadShaOf` serves unchanged.

### 12.6 The badge (`CommitGraphControl.Badges.cs`)

`PullRequestBadge` (`:168`) gains a last `HostingProvider Provider = HostingProvider.GitHub` (7a's
constructions stay equal), set by `BadgeOf` (`:64`) from `pull.BaseRepository.Provider`; `BadgeText`
(`:100`) is `{N}`, measured as today; the tooltip (`:158-161`) ` ({draft }{R} {N}: {title})`. The map
carries each request's provider: no property or binding is added.

### 12.7 Tests

- `RequestSectionProvidersTests` (7b project, over `RequestTestData`): each state's title and rows per
  provider; `NoHostedRemote` 7a's line; `!21`, a draft, fork labels; 300 and "More than 300 open merge
  requests"; the filter on `!21`, `21`, not `#21`; a branch row's `!21`; D356's row with `harbor-co` and
  `Fernhill-Labs` (without the argument the key's lower-case login, `fernhill-labs`), no click, count 0,
  no kept list; open or closed kept from GitHub's state to GitLab's.
- `RequestCheckoutProvidersTests` (7b): 12.5's cases; Azure DevOps never `FetchHead`; `mr-23`, a nested owner.
- `RepositoryMenusProvidersTests` (7b, `RecordingActions`): exact lines of `ForPullRequest`, the section
  (in, out, loading, `OtherOrganization`), `ForLocalBranch`, `ForRemoteBranch`, `ForDrop` per provider;
  `upstream/main` and drops onto it without an item; `OtherOrganization` without one; the palette's.
- `GraphRequestBadgeTests` (7b, `[AvaloniaFact]` as 7a's `GraphPullRequestBadgeTests`): `!21` drawn and
  measured, `Badge.Provider`, GitLab's draft and Azure DevOps' tooltip lines; a GitHub map's labels,
  widths and frame as 7a's.
- `GitRepositoryPullRequestsTests` (extended): a bare remote's `refs/merge-requests/5/head`; with
  GitLab's `merge-requests/*/head` line, `fetch.prune` and `tagOpt` no ref or configuration written;
  the line measured, its SHA returned; `…/x/head` and `refs/pull/5/merge` refused.
- `RequestOperationsProvidersTests` (Hosting.Tests, D334, lagged list; `[AvaloniaFact]`, `ShellDriver.Start`
  with both fakes on port 0 and `Scenarios.Hosting7bAsync()`): !21 tracks `origin/feature/search`; !23
  through `FETCH_HEAD` adds only its branch at the fake's head ref, the local configuration unchanged;
  !11 tracks `origin/feature/sign-in`, fetched first when unread; !13 starts no git call
  (`GitCallBudget`); open in browser records `web_url`, `…/_git/mobile-app/pullrequest/11` and each
  section's page; copy link; refresh asks only the listed fake; `manifest/` asks nothing after the
  sign-in; signed out, no request.

7a's part P and G classes and those 7a design 10.6 names pass unchanged.

### 12.8 Part P's checks

Scenario 2.6 on 1.8's ports (`web-app/`, `mobile-app/`, `manifest/`). They show (R: real window): per
provider the title and rows with `!n`, a draft and a fork, the badge in view (R); signed out; loading
(`HoldNext`); failed in `Describe`'s words; none; past 300 (on GitLab's `web-app/`, whose "More than 300
open merge requests" is the new sentence; Azure DevOps' is 7a's GitHub sentence word for word, and an
Azure DevOps list, if one is used, has its pull requests made with `Conflicts` set, 3.5); the filter;
D356's row, nothing asked; ruling 5's listed remote; the section's menu (R) and a request's; checkout of
!21, !23, !11 with git's state, !13's reason; copy link; open in browser through `FakeFileLauncher`
(ruling 29); a branch row's `!21`. A picture without a list waits for its state (7a design 17 item 3).
About 6 to 8 methods. Every part P method that takes an R picture (the section with its rows and badge
per provider, the section's menu) first calls `Phase7bExpectations.PrepareRequestsAsync(copy, provider)`
(2.7) before `OpenAsync`, and its R pictures are taken in that state (Local branches holds
`topic/no-request`, the graph its local label), the one that part N's checks and WP-R's launch share
(13.8, 14.2); the other part P methods are not prepared. For WP-R, `Phase7bExpectations.P.cs` holds per
provider the repository, the section's title and rows, the section's and a row's menu lines (none of
them changed by the prepare) and the R pictures' names.

### 12.9 Merge, risks, done when

Commits: the guard; texts and the section; plan and operations; menus; the badge; each with its class.
At the merge the lead runs the lagged list (0.4), `Phase7aChecksP` and `Phase7aChecksG` through
`scripts/heavy-run.sh` with their pictures checked against `phase-7a-pictures.sha256` (those 7a's gate
found to vary by nature, by region against 0.7's references), phase 3's `*Check_03_other_menus*`, and
part P; part A's prefilled Azure DevOps picture (10.7), whose region behind is WP-P's, is run and
inspected again, and a change there is recorded under the report's "Changes to expected results".

Risks, accepted: Azure DevOps' `HeadSha` is the source at the last merge and may lag (r5 3.1), so a
click may select an older commit; GitLab drops a head ref 14 days after a request closes (r6 4.2), and
its fetch fails in git's words. The remote's `%20` stays ASSUMED (r5 6.1): the sandbox's names hold no
space, so the live command does not settle it and the fake's answer stands (16.2).
`forkSource.name` stays READ (g1 G0.6): the sandbox has no fork (D375).

Done when these classes pass, 7a's unchanged; part P passes with every picture inspected; 7a's P and G
pictures are the same files; no `refs/merge-requests` or `refs/pull` fetch line or remote-tracking ref
is written; git fetches no Azure DevOps request ref; nothing is sent for `OtherOrganization`; WP-P's
files call none of 1.5's shims; CI's five jobs are green.

## 13. WP-N: a new pull or merge request (part N)

### 13.1 Goal and files

7a's create (7a design 12, 17 item 13) for the listed repository's provider, behind WP-P's menu items,
in the provider's words and number (D351, D355); a fork's head through `NewPullRequest.HeadRepository`;
refusals from `PullRequestRefusal` (r1 P8); the last of 1.5's shims removed. Rulings 1, 3, 20, 21;
D362. Wave 4 after WP-P's merge and part N's checks; branch `wip/p7b-create`. GitHub's form,
texts and pictures stay 7a's.

Files: `RepositoryOperations.CreatePullRequest.cs`; `ViewModels/Dialogs/CreatePullRequestDialogViewModel.cs`;
`Views/Dialogs/CreatePullRequestDialogView.axaml(.cs)`; `OperationTexts.Hosting.Create.cs`;
`src/VisualCommit.Hosting/IHostingSession.cs` (from WP-S, the shims alone); 7a's part N files, which
pass unchanged; in the 7b project `CreateRequestDialogProvidersTests.cs` and part N's files;
Hosting.Tests' `CreateRequestOperationProvidersTests.cs`. The status bar's offer ("Open in browser") and
WP-C's guards are used as they are.

### 13.2 Texts (`CreatePullRequestTexts`)

7a's members stay word for word; the provider forms return them for GitHub. `{P}`, `{R}`, `{RT}`, `{N}`
as 12.2.

| Members (`p` first) | Texts |
|---|---|
| `DialogTitleOf`, `ReadingOf`, `CreatingRequest` | `Create {R}`; `Reading {P}…`; `Creating {R}…` |
| `Operation(p, head)` | `Create {R} for {head}` / `Creating {R}…` / `Create {R} failed` / `{P} could not create the {R}.` |
| `Created(p, n, draft)`, `OpenExisting(p, n)`, `AlreadyOpen(p, n, head, base)` | `Created {draft }{R} {N}`; `Open {R} {N}`; `{RT} {N} is already open from {head} into {base}.` |
| `PushedFirstWithUpstream`, `PushedFirst`, `AheadPushedFirst` | 7a's with `{P}`: `Not on GitLab yet: Create pushes it to origin/local-work first.` |
| `BaseUnknown`, `NotVisible`, `Cancelled` | 7a's with `{P}` and `{R}` |
| `HeadUnknown(p, branch, repo)` | `{P} has no branch {branch} in {repo} to merge from. Fetch, then try again.` |
| `BranchesUnknown(p, head, base)` | `{P} cannot find {head} or {base}. Fetch, then try again.` |
| `Refused(p, repo, permission)` | `{P} refused to create a {R} in {repo} with this token.`; with `Detail` GitHub's 7a sentence, GitLab's ` It needs the api scope, or Merge Request: Create.` (r6 2.2, READ) |
| `SentTitle(p, title)`; `QuickActionsNote` | `Title on {P}: {title}`; `{Name(GitLab)} runs quick actions in the description, such as /close.` |

`From`, `Into`, the placeholders, `Draft`, `Create`, `Creating…`, the reasons, `SameBranch`, `NoCommits`,
`GonePushedFirst`, `PushFailed` and `OpenInBrowser` name no provider. `HeadNotAccepted` and
`ForkTokenRefused` stay GitHub's alone.

### 13.3 The start, the head and the base

`CreatePullRequestAsync` (`:29-75`) as 7a design 12.3 with `p` the listed repository's provider:
`NoHostedRemote` or `OtherOrganization` logs "not started" (`:41`'s line says "no hosted remote"; D350,
D356; WP-P offers no item there);
`SignedOut` awaits `Services.SignIn(p)` (1.7) first. In `PullRequestForm` every `Remotes` read becomes
`RequestRemotes` (`:123`, `:140`, `:161-194`, `:514`, `:528`, `:538-540`, `:563`): head and base are on the
listed provider, on Azure DevOps in its organization, so a GitLab head never meets a GitHub base (r3
0.12). An `owner:branch` text is WP-P's `PullRequestRow.BranchLabelOf`.
`PullRequestEnd` gains `HostedRepository? Hosted { get; init; }`, set wherever an end is made;
`IsSameBranch` (`:253`), `RepositoryOf`, `OpenFromHeadIntoBase` (`:485-491`) and `CreatedAsync`'s
listed check (`:404`) compare records, never `FullName` (r1 P11). `Login` (`:112`) and `LoginOf`
(`:470-471`) read `AccountOf(p)`.

### 13.4 The form

`ICreatePullRequestActions` gains `HostingProvider Provider => HostingProvider.GitHub` (a default, so
7a's test fakes compile); `PullRequestForm` gives `p`, from which the view model's `Title`, Into's
reading text, `OpenExistingText` and the errors are worded. Two GitLab lines, collapsed otherwise, so
GitHub's and Azure DevOps' forms lay out as 7a's:
- `PullRequestSentTitle` (`settingsNote`, margin 0,4,0,0, after `PullRequestDraft`) while Draft is
  ticked: `SentTitle(p, GitLabProvider.TitleFor(title.TrimEnd(), true))`, `Title on GitLab: Draft: Add
  the search box`, a `[Draft] x` unchanged (`TitleFor`, 1.4). The form sends the
  title as typed with the Draft flag and GitLab's provider applies the same `TitleFor` (WP-GL 7.4): one
  rule shows and sends it (ruling 20).
- `PullRequestQuickActionsNote` (`settingsNote`, margin 0,4,0,0, under `PullRequestDescription`):
  `QuickActionsNote`. The description goes as typed: a `/close` line closes the new request (g2 G1.4,
  READ).

**Repository texts cut** (ruling 3; r3 4.5). The From and Into rows' horizontal `StackPanel`s
(`CreatePullRequestDialogView.axaml:34-44`, `:53-71`) become `DockPanel`s with `HorizontalAlignment="Left"`:
From `PullRequestHead` docked left, then `PullRequestHeadRepository` (`Margin="8,0,0,0"`) filling; Into
`PullRequestBase` left, `PullRequestBaseChooseButton` right, then `PullRequestBaseRepository`
(`Margin="8,0,8,0"`) filling. Sized to their content they place each child where `Spacing="8"` did; past
the 370-px column the repository text is cut with "…" (`CharacterEllipsis`), its whole text the tooltip.
Ids, names and the tab order stay.

### 13.5 The operation

7a design 12.5 and 17 item 13 with:
- **Request** (`:362-363`): `sameRepository = place.Repository.Equals(baseRepository)`; `new
  NewPullRequest(title, body, branch, sameRepository ? null : place.Repository.Owner, baseBranch, draft)
  { HeadRepository = sameRepository ? null : place.Repository }`. GitHub reads `HeadOwner` (7a's), GitLab
  posts to the source project with `target_project_id` (WP-GL 7.4), Azure DevOps sends `forkSource`
  (WP-AZ 6.6).
- **Created**: any success, Azure DevOps' 200 and 201 alike (g1 G0.12, its provider's). `Created` takes
  draft from the box on GitHub (7a's) and from the answer's `IsDraft` elsewhere: a GitLab title typed
  `Draft: x` without the box reads "Created draft merge request !24".
- **Refusals** (`:374`, `:416-430`) by `failure.Refusal`, then `Kind`; no arm compares `Message` or
  `Detail` with a sentence, which are only shown:

| `Refusal` or `Kind` | Words |
|---|---|
| `AlreadyExists` | D316's re-read: `AlreadyOpen` and `ExistingNumber`, else `ProviderSaid` |
| `NoCommits` | `NoCommits` (GitHub's alone; GitLab creates one without commits, g2 G1.4) |
| `HeadMissing`; `BaseMissing`; `BranchesMissing` | GitHub `HeadNotAccepted`, others `HeadUnknown`; `BaseUnknown`; `BranchesUnknown` (GitLab's 400 naming both, TF401398, which names none) |
| other `Validation` | `ProviderSaid`: 7a's `GitHubSaid`, `{Message}: {Detail}` or the message |
| `Forbidden`; `NotFound` | GitHub 7a's two rows, others `Refused`; `NotVisible` |
| `Network`, `ServerError` | D316's re-read, else `HostingTexts.Describe(failure, login)` |
| any other | `HostingTexts.Describe(failure, login)` |

- **Log lines** as 7a design 17 item 13 with `{R}` and `{N}`, never the title or the description.

### 13.6 The shims

With WP-A's, WP-B's and WP-P's callers moved and `:112`, `:470-471` here, WP-N deletes 1.5's six
default-bodied members (`GitHub`, `Account`, `SignInAsync(string, …)`, `SignOutAsync(CancellationToken)`,
`SetUseForGit(bool)`, `ListRepositoriesAsync(CancellationToken)`) and any member of `NullHostingSession`
that served only them. `:454` calls `GitCredentialsRefused(refused.Provider, LoginOf(refused))`; 7a's
one-argument credential texts stay (1.7). `src/` builds without the shims; tests keep 7a's session forms
through `HostingSessionGitHubForms` (1.9); no using is added to a 7a file or to another package's file.

### 13.7 Tests

- `CreateRequestDialogProvidersTests` (7b project; `[Fact]` with a fake `ICreatePullRequestActions`,
  `[AvaloniaFact]` for the view): 13.2's texts per provider, GitHub's equal to 7a's constants; title and
  reading text by `Provider`; `PullRequestSentTitle` ticked and not, for `Draft: x`, `[Draft] x`,
  `(draft) x` and plain, never on GitHub or Azure DevOps; the note on GitLab only; with 7a's names the
  From and Into children's bounds equal those of a copy of 7a's `StackPanel` rows in the same window; a
  60-character repository cut, its tooltip whole, `Choose…` inside the card.
- `CreateRequestOperationProvidersTests` (Hosting.Tests, D334, lagged list; `[AvaloniaFact]`,
  `ShellDriver.Start` with both fakes on port 0, signed in). GitLab: one POST to the project, title and
  description as typed (a `/close` line too), `Draft: ` first with the box; the row, the badge, "Created
  draft merge request !n", the offer opening `web_url`; `local-work` pushed first with `x-access-token`;
  a fork's head (`ada/web-app` as a remote) posted to the fork with `target_project_id`; the 409, then
  "Open merge request !21"; the 400 forms; a 403 with and without the scope code. Azure DevOps:
  `CreateStatus` 201 and 200; `isDraft`; a fork's `forkSource.repository.id`; TF401179 and TF401398 in
  the App's words. Both: `CreatedThenDropped` counted as created; `manifest/` not started, no request;
  signed out, that provider's sign-in first; the tripwire after a create that pushed.

7a's part N classes, `CreatePullRequestOperationTests` and `RepositoryOperationsRemotesTests` pass
unchanged.

### 13.8 Part N's checks

Scenario 2.6 on 1.8's ports, `web-app/` and `mobile-app/` signed in; every check first calls
`Phase7bExpectations.PrepareRequestsAsync(copy, provider)` before `OpenAsync` (a local branch tracking
`origin/topic/no-request`, as 7a design 12.8), and its R pictures are taken in that state, the one part
P's R methods share (12.8, 14.2). They show (R: real window): "Create merge request…"
and "Create pull request…" from a branch's menu and a drop's; each provider's form (R); GitLab's Draft
with its sent title and the note; `local-work` pushed first; already open and a missing branch in the
App's words; the request listed with its badge, "Created … !n" and the offer (R: the status after
creating); long repository texts cut with their tooltips; signed out, that provider's sign-in first; the
form in light and at 1920×1080 once. Pictures wait for D322's first line; requests are counted on the
fakes, git calls by `GitCallBudget` (7a design 12.8). About 5 to 7 methods. For WP-R,
`Phase7bExpectations.N.cs` holds per provider the branch row, the menu item, the form's base and title,
the create's route key, the status text and the R pictures' names.

### 13.9 Merge, risks, done when

Commits: texts and the form; the operation; the shims' removal; each with its class. WP-N's merge
closes wave 4, so the lead, through `scripts/heavy-run.sh`, runs the lagged list, then the whole default
run (0.4, D245), and 7a's part N and phase 3's walk-through with their form and status-bar pictures
compared by region with 0.7's references, then part N; WP-LIVE's second stage and WP-R may then start.

Risks, for the live command (D342): Azure DevOps' create status and a create with nothing ahead
(unknown, r5 4); GitLab's 409 and 400 forms (READ, g2 G1.1) and `draft` for a `Draft:` title (READ).
Azure DevOps' fork body (`forkSource`) stays ASSUMED (g1 G1.2): the sandbox has no fork (D375), so the
live command does not confirm it and only the fake holds it. Quick actions run as typed, the note their
only guard (ruling 20).

Done when these classes pass, 7a's part N classes unchanged; part N passes with every picture inspected;
7a's N pictures and phase 3's status bar are as 0.7 says; no App code compares a service's sentence
(`StartsWith("A pull request`, `"head is invalid"` and the like are gone from `src/VisualCommit.App`);
`IHostingSession.cs` declares none of 7a's GitHub-named members and `src/` calls none, nor a one-argument
credential text (outside `OperationTexts.Hosting.Credentials.cs`, which keeps them, 1.7); the tripwire
is clean; CI's five jobs are green, each Test step under four fifths of its limit (0.6).

## 14. WP-R: the real-window pass

### 14.1 Goal and files

The steps parts A, B, P and N mark "R" (0.5), repeated in the built app on the Windows desktop with the
real mouse and keyboard and compared with their scripted twins at most 3 % apart (D33, D58); status step
18. Ruling 29; D363. Wave 5 on the Windows machine, after the four parts; `wip/p7b-real-window`;
only the lead runs it. It proves what 0.5 says only the real window does, with a clone through
`visualcommit-askpass.exe` of an `<organization>@` URL with `%20` and of a nested GitLab path. Out: any
browser (ruling 29), restarts, the real keychains, `<organization>.visualstudio.com`, light, 1920×1080.

Files, in `tests/VisualCommit.RealWindowTests/`: `Phase7bRealWindowPass.cs` (what is where, `Phase7bRun`,
`Phase7bOrder`, the helpers), `.Accounts.cs`, `.GitLab.cs`, `.AzureDevOps.cs`, `RealApp.Phase7b.cs` (the csproj is not WP-R's). It uses 7a's `RealApp.Phase7a.cs:61-286` as it is, the
parts' constants in `Phase7bExpectations.<part>.cs` (10.7, 11.7, 12.8, 13.8) and `PrepareRequestsAsync`
(`Phase7bExpectations.Common.cs`, 2.7), 2.6's scenario and 1.7's ids, and holds only the fakes' tokens.

### 14.2 Conventions (7a design 13.2's, plus these)

- Five methods, one launch each, 1100×700, dark, in `Phase7bOrder`: `Check_A_three_accounts`,
  `Check_GitLab_sign_in_browse_and_clone`, `Check_GitLab_requests_and_create`, then Azure DevOps' two
  (two per provider and one for three accounts, ruling 29, in place of D319's one per part).
- Declared so that the app stops first: `using var copy = await Scenarios.Hosting7bAsync();`, `await
  using var served = await Scenarios.ServeHosting7bAsync(copy, ownsCopy: true);`, the prepare that
  every twin the launch compares calls (`PrepareRequestsAsync` for "Requests and create", none for the
  others), then the app. The twins of one launch share one prepared state.
- The three providers are on in every launch, as in every twin (2.7). Each sign-in types the scenario's
  token into the real dialog (D320's form), and for Azure DevOps the organization as part A gives it, or
  finds it prefilled from the tab's remote (ruling 16).
- Waits on named conditions with a limit; no token in an `Assert` argument; never a click on "Open in
  browser" or any "Copy …".

### 14.3 The run, the helpers, `RealApp.Phase7b.cs`

`Phase7bRun` (`RealWindowRun(phase: Phase7bExpectations.Phase)`) is the class fixture of
`[TestCaseOrderer(typeof(Phase7bOrder))] public partial class Phase7bRealWindowPass`, with 7a's limits
(`Phase7aRealWindowPass.cs:104-116`); `Sized`, `Opened`, `Compare` and `AssertMenu` copied, naming their
source. New:
- `Launch(ServedHosting7b served, string? repository)`: a new data folder, one tab (empty or the clone),
  `RealApp.Launch(..., RealApp.Hosting7bEnvironment(served))`, `Sized`; then each provider's start line
  `{DisplayName}: 127.0.0.1:<port>.` (1.9, 9.2; GitHub's as `HostingSession.cs:80`) and the welcome
  page's `CloneRepoButton` or `Opened`. run.txt names a fake that fell back.
- `WaitForFake(FakeHostingService fake, int mark, Func<FakeServiceRequest, bool> match, string what,
  TimeSpan timeout)` (7a's form for GitHub's); `WaitForListed(app, logMark, repository)`: D322's
  `PullRequestsListed`, alone for a new provider (D349).
- `AssertCleanEnd(app, served, params string[] folders)`: exit 0; `TokenTripwire.AssertAbsent` for each
  of `served.Tokens` over the data folder and `folders`; each fake's `Errors` empty; no request a fake
  refused for its headers; no `avatars/` unless GitHub signed in.

```csharp
public sealed partial class RealApp
{
    public static IReadOnlyDictionary<string, string?> Hosting7bEnvironment(ServedHosting7b served);   // three URL variables, keychain memory (as RealApp.Phase7a.cs:46-50)
    public static string AccountsSuffix(HostingProvider provider);       // "", "GitLab", "AzureDevOps" (1.7)
    public void ScrollAccountsTo(HostingProvider provider, TimeSpan timeout);   // real wheel turns over SettingsSection until the block shows
    public void ScrollAccountsToEnd(TimeSpan timeout);                    // real wheel turns over SettingsSection until UI Automation's VerticalScrollPercent is 100; asserted before the capture
    public AutomationElement OpenSignInDialog(HostingProvider provider, TimeSpan timeout);   // Accounts, scroll, a click on AccountsSignInButton<suffix>, SignInDialog shown
    public void TypeOrganization(string organization);                   // a click on SignInOrganization, Ctrl+A, typing; its Value read back
    public void SignInThroughSettings(HostingProvider provider, string token, string? organization, string login, TimeSpan timeout, bool keepOpen = false);
}   // the last: dialog, organization unless prefilled, a click on SignInToken, TypeToken, ConfirmSignIn, AccountLogin<suffix> = login, Esc unless keepOpen
```

### 14.4 The five launches

**A. Three accounts** (an empty tab). No fake request at the launch (D257, D350). `SignInThroughSettings`
with `keepOpen` for GitHub, GitLab and Azure DevOps (D353's order); each fake has part A's check
requests, in order, with `Bearer` (GitLab) or `BasicEmptyUser` (Azure DevOps), 200.
`ScrollAccountsToEnd`: part A's three-account picture is taken at the section's end, its largest offset,
as its twin's, which turns `ShellDriver`'s wheel until the offset stops changing (10.7); `Compare`. Esc;
`accounts.json` holds three records, `userId` on Azure DevOps' alone, no token. `AssertCleanEnd`.

**Sign-in, browse, clone**, GitLab's then Azure DevOps' (an empty tab; `clones` a `TempDirectory` first).
1. `OpenSignInDialog`; for Azure DevOps `TypeOrganization`; a click on `SignInToken`, `TypeToken`.
   `Compare` with part A's dialog picture. `ConfirmSignIn`: part A's check requests alone; Esc.
2. The clone form's button reads "From GitLab…" or "From Azure DevOps…" (D354); a click; wait until
   `CloneBrowserRowNames()` is part B's list. `Compare`.
3. Part B's row, "Choose": `CloneUrlBox` holds part B's URL through `WithPorts` (Azure DevOps' with its
   `<organization>@`, ruling 15). Parent `clones.Path`; mark `GitRequests`; "Clone"; `Opened` within 60 s;
   no `CredentialsDialog`; a 200 git request with `GivenUserName` `x-access-token`, none answered 200 with
   another (git before 2.46 first sends the URL's `<organization>` without a password, a 401, r7 H4b).
4. run.txt: the helper's path and whether it holds a space. After `app.Close()`, in the clone:
   `remote.origin.url` is step 3's URL, `git config --get-regexp ^credential` exits 1.
   `AssertCleanEnd(app, served, clones.Path)`.

**Requests and create**, GitLab's then Azure DevOps' (a tab on `web-app` or `mobile-app`).
`PrepareRequestsAsync(copy, provider)` before the launch, as both parts' twins.
1. `SignInThroughSettings` (Azure DevOps' organization prefilled); `WaitForListed`;
   `PullRequestRowNames()` is part P's rows; GitHub's fake has no request (web-app's upstream, D257).
   `Compare` with part P's section picture, the badge in it.
2. A right-click on "Merge requests" or "Pull requests": `AssertMenu` with part P's; `Compare` keeping
   the pointer; Esc. A right-click on part P's row: its labels; Esc.
3. Part N's branch row, "Create merge request…" or "Create pull request…": wait for
   `CreatePullRequestDialog` with part N's base and title. `Compare`.
4. Mark; "Create"; `AppLogFile.OperationEnded` with part N's title, succeeded. Since the mark one create
   request as part N names it, its body with part N's title, source and target, and a receive-pack with
   `x-access-token` if the branch was pushed; the status as part N's, `OperationOfferButton` "Open in
   browser" never clicked, the row listed. `Compare`. `AssertCleanEnd`.

### 14.5 The lead's steps; done when

0.6 gives the time. The builder runs `dotnet build tests/VisualCommit.RealWindowTests -c Release` (no
warnings) and the parts' expectation classes, never the pass. At the merge the lead adds CLAUDE.md's row
for `--filter-class "*Phase7bRealWindowPass"` (it empties `artifacts/visual/phase-7b/real-window/`) and,
in "What to know", that 7b's part binds 47397 to 47399 in the test process, so 7b's walk-through never
runs beside it, and opens no browser. The gate runs as 7a design 13.7, with the live command for the
three providers (D335), each run after the offline classes pass alone (15.9), and 7b's 13 captures in a
batch.

Done when the project builds in Release with the five methods in `Phase7bOrder`; each R step of the four
parts has its step and its comparison under its twin's name; each launch's state is the one prepared
state that all the twins it compares share; no browser or "Copy …" click; and, at the gate, D246's two
runs and the counted pass are green, every capture within 3 % and inspected.

## 15. WP-LIVE: the live command for Azure DevOps and GitLab

### 15.1 Goal, stages, files

D346 extends 7a's command (7a design 14, 20) to the two new providers: per provider, sign-in, clone,
the request list and a create in the owner's sandbox, as far as D352, D371 and D374 allow (D364;
status step 16).

**Stage 1** (`wip/p7b-live1`, right after WP-S's merge) is `Check_1` (with `Check_1b_probes` where
15.5's sums need it), `Check_2` and Azure DevOps' `Check_4`. The lead runs it with the owner's tokens.
WP-LF, the fix package (0.4), then puts its findings into the fakes, marked MEASURED with the date,
before part A's checks are written. **Stage 2** (`wip/p7b-live2`, after WP-N) is `Check_3`. The gate
runs the whole project (D335).

Files in `tests/VisualCommit.LiveHostingTests/`: new `AzureDevOpsLiveTests.cs`, `AzureDevOpsLiveApi.cs`,
`GitLabLiveTests.cs`, `GitLabLiveApi.cs`, `LiveScreen.cs`; changed `LiveSettings.cs`, `LiveRun.cs`,
`LiveRequestLog.cs`, `LiveProbes.cs`, `AssemblyInfo.cs` and `LiveSafetyTests.cs` (with `LiveScreenTests`).
The GitHub files do not change.

### 15.2 Variables

`CaptureAndRemove()` (`AssemblyInfo.cs:31`) takes 7a's two variables and 1.8's four; the watchdog keeps
600 s. `Require(HostingProvider)` gives `LiveAzureSandbox(Organization, Project, Repository, Token)`,
the organization `[A-Za-z0-9-]+` (g3 G3.4), or `LiveGitLabSandbox(Namespace, Project, Token)`, 2 to 21
segments; other segments are 7a's `Segment`, and 7a's `Require()` stays GitHub's. `ToString()` hides
the organization; the failure message names the variables and format, never the value, and says
"looks like a token" for a value holding the token, starting `glpat-`, or of 52 or 84 characters
without `/`. `KindOf` reads only a prefix or a length (r5 5).

### 15.3 The screen (D372, g4 D1.7)

`LiveScreen` has `AddOrganization`, `AddAddress`, `Learn(JsonElement)`,
`LearnFromAnswer(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, string? body)` (the ids
in an Azure DevOps answer's headers, every value of a repeated one, such as r5 1.3's three `WWW-Authenticate`,
and in a body that is not JSON), `LearnFromGit`, `Apply` and `SurvivorIn`.

**Learning.** The organization comes from the variable. Addresses come from every body the test's own
API reads, before that body is noted: `Account.$value`, `descriptor`, `createdBy.uniqueName`, `/user`'s
`email`, `commit_email` and `public_email`, `author.public_email`, and the clone's `git log --all
--format=%ae%n%ce`. Ids come from every Azure DevOps answer that API reads, headers and body, before it
is noted: connectionData's `instanceId` (the organization's host id, which is the sign-in URL's `hid=`;
MEASURED r5 1.3, p15 and p16), `authenticatedUser.id` and `subjectDescriptor` (never the Anonymous
`aaaaaaaa-…`, g1 G0.3); the tenant in `X-VSS-ResourceTenant` and after `login.microsoftonline.com/` in
`WWW-Authenticate`; every `hid=` in a `Location`, an `X-TFS-FedAuthRedirect` or a body; and the `hid`
of a sign-in URL's `context=`, base64 of JSON `{"ht":2,"hid":"<a second host id>",…}` (MEASURED r5
p03, p15). An organization's id gives back its name without authentication (D372). Each method's first
call is the identity read (connectionData, `GET user`), once per run.

**Addresses.** An address-shaped string is D368's run (an `@` between non-blank characters), unless its
`@` ends a URL's user information (`<scheme>://<user>@`, git's `***@` included) or it is git's scp-like
`git@<host>:`. The exception is judged on the text before the organization is replaced (the
organization rule screens those user parts); `SurvivorIn`, which reads screened text, also takes an `@`
right after one of the screen's placeholders as such an end.

**`Apply`** replaces each learned address, as written and URL-escaped; each
learned id anywhere, inside vssps' `A<id>` path segment too, in any case, with and without hyphens, as
written and URL-escaped, by `<the organization's id>` (either host id), `<the tenant>` or `<the user's
id>`; the query and `#ctx=` fragment of any URL whose path ends `/_signin`, in a `Location`, an
`X-TFS-FedAuthRedirect` or an "Object moved" body's `href` (with `&amp;` as written), by `<the sign-in
query>`, so that `reply_to`, `hid` and `context` are never shown; then the organization in any case at
a boundary (a text end, a character outside `[A-Za-z0-9-]`, a `%XX` escape); and any other
address-shaped string except D325's and `example.com` ones. The sign-in query, any other `hid=` value
and any `context=` that reads as a sign-in context (ASP.NET's UrlToken form, as the service sends it:
base64url without padding, then a digit counting the `=`) go first, before the learned values: a learned
id's placeholder would end the query's run and leave its `context=`, the second host id in base64,
behind. A second `Apply` changes nothing.

It runs in `Note`, `Screen` and run.txt after the token search; in `FailureOf`, rethrowing the screened
text as an `XunitException`; and in `Check(bool, Func<string>)`, the only assertion given values (g4
D1.4). In each, a screened text in which `SurvivorIn` still finds a value is withheld as one holding the
token is (the kind of value named, never the value), and the run fails at its end.

**`SurvivorIn`** searches run.txt raw, then percent-decoded again until a pass changes nothing
(`Uri.UnescapeDataString` leaves an invalid escape such as `%C0%AE` as it is, so the loop stops on no
change, never on "no `%XX` left"). It looks for the organization at `Apply`'s boundaries, for each
learned address and each learned id in every form `Apply` replaces, and for any address-shaped string,
sign-in query or sign-in context `Apply` would replace. D374's made-up organization (15.4) is no
secret: run.txt names it, and it is no survivor. The data folder's `accounts.json` and log are searched
only for the learned addresses, as written and URL-escaped: both name the organization (the login, the
clone's command line), and the log holds git's `https://***@…` URLs. A survivor withholds run.txt and fails the run (D323).

**`LiveRun`** loses `sealed`, GitHub's constructor and render unchanged; `LiveRun(HostingProvider)`
gives the fixtures `AzureDevOpsLiveRun` and `GitLabLiveRun` their token, `artifacts/live/<id>/run.txt`
and screen. `LiveTokenForms.FoundIn` adds the three base64 alignments (g4 D1.6).

### 15.4 The request log and D352's guard (D371)

`LiveRequest` gains init-only `Provider`, `Form` (none, Basic, Bearer, another scheme,
`PRIVATE-TOKEN`, `JOB-TOKEN`, a query token) and `Headers` (rate-limit, expiry, `api-version`).
`RequestId` reads `ActivityId` or `x-request-id`. `LiveRequestLog(inner, store, LiveReach)` sits beside
7a's constructor. `AzureDevOpsLiveApi` and `GitLabLiveApi` send through it, each over an inner handler
of its own built as GitHub's public constructor builds its one (`GitHubLiveApi.cs:237-242`:
`SocketsHttpHandler` with `AllowAutoRedirect = false`, `AutomaticDecompression = All`, no cookies, a
10 s `ConnectTimeout`), so `GitHubLiveApi.cs` stays unchanged (15.1). A redirect is recorded as the answer and never followed:
a hop the handler followed by itself would pass neither the guard nor the store, and the probe would
record the target's answer instead of the 302. A refusal names the origin, never the path;
`ReachProblems` and `PerHost` print through the screen and name a request's origin, never its path;
a refused request has the guard's line alone.

**`LiveReach.AzureDevOps`** admits only `https://dev.azure.com:443`, with Basic or no credential. It
refuses a segment that is empty, is `.` or `..`, or holds `/`, `\` or `%` once decoded. Matching
without case, it admits only:
1. `{o}/_apis/connectionData`.
2. `_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf` with `organizationName={o}`, or that id
   under `{o}/` (D360).
3. `{o}/{p}/_apis/git/repositories` alone (`ListProject`'s list).
4. `{o}/{p}/_apis/git/repositories/{r}` itself, and below it only `pullrequests`, `pullrequests/{n}`
   (`{n}` digits) and `refs`, which the app (6.5) and the command send: a segment below it can name
   another collection (Forks' `forks/{collectionId}`), so nothing else is admitted (WP-LIVE1's
   third review; stage 2 widens the list if `Check_3` needs more).
5. Anything below `{o}/{p}/_git/{r}`.
6. `{x}/_apis/connectionData`, where `{x}` is the one made-up organization the run draws, `vc-none-`
   and 12 random lower-case hex digits (D374, amending D352 and D371): without a credential, and with
   Basic only after that path answered 404 without one in this run, and only once; never once a
   read without one was answered otherwise, refused or left without an answer, first or later.

The query may hold only the parameters the app's client (6.5) and the command send: `api-version`,
`$top`, `$skip`, `searchCriteria.status`, `filter` and `service`, none repeated, and `organizationName`
in item 2's root form alone; another, such as Merge Bases' `otherRepositoryId`, `hostId` or
`accountName`, could name another repository or organization. For either provider, a request whose
`Host` header names another authority than its URL's is refused: the handler would send that header's
host as the TLS server name and the `Host`, with the credential, while the store records the URL's.

Here `{o}`, `{p}` and `{r}` are the names `VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO` gives (15.2), compared
without case. A project or repository named by an id is refused, since no request of the app's (6.5:
every A3 to A5 path is `ApiPath`, built from names) or of the command's own names one by id. Everything
else is refused, `{o}/_apis/git/repositories`, `_apis/projects`, any other organization and the
project-level `pullrequests/{n}` among them; the app's own requests stay inside (6.5). The probes send
`AzureDevOpsApi`'s versions and area id (1.4), as the client does.

**`LiveReach.GitLab`** admits only `https://gitlab.com:443/api/v4/`, with Bearer or no credential, and
no other host (D349).

### 15.5 Stage 1

Each method's call limits are summed as 7a's are (`AssemblyInfo.cs`'s comment) and kept under the
watchdog's 600 s, with five leftovers at full limits. Azure DevOps' probes go into a method of their
own, `Check_1b_probes`, after `Check_1` in the order, when `Check_1`'s sum passes it, as D373's and
D374's calls make likely; GitLab's likewise. The module initializer's comment gives the new sums.

**`Check_1_the_sandbox_and_its_leftovers`** (`[Fact]`; ordered as `GitHubLiveTests.cs:35`):
1. `Require` (the variables gone), the identity read, and the sandbox: 200, private (D326), its
   default branch.
2. Clear the leftovers (D324), touching only the sandbox's `refs/heads/visualcommit-live/` branches and
   their requests.
   - **Azure DevOps.** Abandon active pull requests without `forkSource`; they cannot be deleted. Then
     one `POST …/refs` with zero `newObjectId`s.
   - **GitLab.** Close each merge request, then delete it; on a 403, note "closed, kept" (g2 G1.6).
     Delete each `search=^visualcommit-live/` branch; a 404 means gone (g2 G1.7).
3. Run 15.7's `Check_1` probes, unless `Check_1b_probes` takes them.

**`Check_2_the_providers_and_the_session`** (`[AvaloniaFact]`, as 7a 14.4):
1. **Start.** `ShellDriver.StartWithTabs(…, hosting:)` with `HostingOptions.Off with { AzureDevOps =
   AzureDevOpsEndpoints.Service with { ListProject = sandbox.Project }, CreateAzureDevOpsHandler = … }`
   (GitLab: `GitLab = GitLabEndpoints.GitLabCom`, `CreateGitLabHandler`), the handler a `LiveRequestLog`
   over `HostingOptions.DefaultHandler(…)` with the provider's `LiveReach` (a null one refuses every
   request, 1.4); no request.
2. **Sign in** with `app.SignInAsync(provider, token, organization)` (1.9). Only the sign-in's reads
   leave, each 200 and each in the `LiveRequestLog` store (a provider whose requests bypass it fails the
   run, as `GitHubLiveTests.cs:646`); the login is the organization or the namespace's first segment,
   without case; `accounts.json` is as 1.8.
3. **List.** The session lists the sandbox. On Azure DevOps, `CloneUrl` keeps `<organization>@`
   (ruling 15).
4. **Clone** through `CloneUrlBox`, with 7a 14.4 step 4's checks; the app's helper answers git.
5. **Load.** `PullRequests` reaches `Loaded`.
6. **Push** D325's commit on `visualcommit-live/<stamp>` with `Push {branch}…`.
7. **Create.** `Hosting.CreatePullRequestAsync(…, Draft: true)` succeeds; `ByRef` holds it.
8. **Probe** the create (15.7).
9. **Clean up**, in `finally`.
10. **Tripwires.** `TokenTripwire.AssertAbsent` and `ReachProblems` find nothing, and no request came
    before the sign-in.

**`Check_4_a_clone_through_the_organization_host`** (Azure DevOps) starts and signs in as `Check_2`'s
steps 1 and 2 do, then clones `https://<organization>.visualstudio.com/<project>/_git/<repository>`
with no credentials dialog, then lists from `dev.azure.com`. It is the only test of `OrganizationHost`
against the service (D363), and it runs in stage 1 so that a refusal of git's user name on that host
brings ruling 8's fallback before part B (16.1).

### 15.6 Stage 2

**`Check_3_the_app_signs_in_clones_lists_pushes_and_creates`** is 7a 14.4's `Check_2`, run on 7b's
surfaces. It starts the app as `Check_2`'s step 1 does: without `ListProject`, A2's
`{o}/_apis/git/repositories` would meet the guard's refusal and fail the run. Then:
1. Sign in through the provider's dialog, on Azure DevOps with the Organization box (1.9's `ShellDriver`
   provider forms, WP-A's).
2. Pick the row by `FullName`, without case, in "From Azure DevOps…" or "From GitLab…" (1.7).
3. The section is titled `RequestsTitle(p)`. `OperationTexts.MenuTexts.CreateRequest(p)` with Draft
   ticked (GitLab's title shows `Draft: …`) ends with the status `CreatePullRequestTexts.Created(p, n,
   true)` (1.7).
4. Read back, clean up and the tripwires as `Check_2`.

### 15.7 Probes, premises, the fake beside, run.txt

**The fake beside.** Each probe also goes to an in-process fake as `LiveFake` (`LiveProbes.cs:215`),
5 s a call, with its own token: `LiveAzureFake` (`fake-org`, the sandbox's project and repository
names) or `LiveGitLabFake` (`ada/<project>`), each with a README on `main` and the run's branch.
`LiveServiceAnswer.Beside` replaces each side's names, ids, dates and origin (g4 D1.7.6), then compares
the status, the media type, `api-version` and BOM, `typeKey`, the TF code and `message` (GitLab's shapes
A to F, `scope` as a set, r6 10), `WWW-Authenticate` and a 2xx's top-level names. Each note says "the
fake: same" or what differs; a non-2xx body is shown screened, a 2xx by shape.

**Azure DevOps probes** (r5 9, g1 G6, 3.7). `Check_1`: connectionData with and without Suppress and at
`7.1`; the core area, anonymous and with the token; D373's made-up values, a short string and an
84-character PAT-shaped one, each with and without Suppress, to connectionData and the sandbox
repository's `info/refs` (g1 G0.7, G5); D374's made-up organization; the sandbox project's repository
list (`{o}/{p}/_apis/git/repositories`, `ListProject`; 3.7's "the sandbox's row"). `Check_2`: the
create's status, `mergeStatus` and `isDraft`; when the merge ref appears; `createdBy`; a duplicate, a
missing branch, an empty title, nothing ahead; the abandon and the merge ref's removal; the refs delete.

**D374's probe** sends `{x}/_apis/connectionData` (15.4) first without a credential. Only when that
answers 404, the unknown organization's answer (MEASURED r5 1.3), does it send the token's read;
otherwise it notes the skip and sends nothing with the token. The fake beside gets both reads.

**GitLab probes** (r6 14, g2, 4.7). `Check_1`: `user`, then `personal_access_tokens/self`, whose 403
`insufficient_granular_scope` is expected and noted, not a premise (D364's token has no Personal Access
Token: Read, r6 2.2; it shows 4.3's check 5); a made-up
`glpat-` value on `GET user` (D373); the projects with `permissions`; the merge requests;
`ratelimit-*` across a 304. `Check_2`: `draft`, `references`; a duplicate, a missing branch, nothing
ahead.

**Made-up values** (D373) go through the guard as the token does (D371). The token-shaped ones are drawn
at run time, so no such literal is committed: the 84-character one letters and digits with `AZDO` at
0-based characters 76 to 79, the Azure DevOps form of `HostingProviders.TokenFormOf` (1.2, D378; r5 5),
GitLab's `glpat-` and 20 letters and digits (the older length, r6 2.4); each is drawn
again while `LiveTokenForms.FoundIn` finds the token in it. The short one is a fixed plain string (g1
G5). Notes and run.txt name each by its kind and length, never as written, so neither 7a's token search
nor 15.2's "looks like a token" rule meets one.

**Premises** fail the run after their note (D342). Azure DevOps: the identity's `id` is a GUID other
than `aaaaaaaa-…` (g1 G0.3); the organization is `locationUrl`'s first segment and `remoteUrl`'s user;
no made-up value earns a 2xx but one ruling 16 refuses (a 203, an HTML body, the Anonymous id); D374's
token read, when sent, answers 404 as the read without a credential did (A1's `NotFound`, 6.4); a
duplicate is 409 `GitPullRequestExistsException`, a missing branch 400
`GitPullRequestCannotBeActivated`; the create 200 or 201; `createdBy.id` is `UserId`. GitLab: `user` is
200 with the login; every request is Bearer; a made-up value gets 401; a duplicate 409; a missing branch
400 with `source_branch` (7.3); `draft` follows `Draft:`.

What 3.7 and 4.7 keep READ or ASSUMED stays so, as do the expired and revoked bodies. With no fork in
the Azure DevOps sandbox and no legacy GitLab token (D375), the fork shapes and `/source` stay as 3.7
marks them and a legacy token's sign-in rests on GitLab's docs (READ): the live command proves the
fine-grained GitLab token alone.

**run.txt** holds each provider's notes in order: 7a 14.5's items, the probes (D374's made-up name and
any skip among them), the identity's member names, and whether the server spells the organization as
the variable does.

### 15.8 The owner's steps (D364)

Once, before WP-S's merge (0.4, 16.4), so before stage 1. The owner sets the four variables in Windows'
Environment Variables dialog, as 7a design 14.6 step 3 does, then quits VS Code entirely and starts it
again: a new Claude Code session inside a running VS Code does not inherit them (7a design 20.1; status
Notes, 2026-10-30).

**Azure DevOps**, in D352's organization:
1. A private project `visualcommit-sandbox`, with a README, and no fork of its repository (D375).
2. A token "VisualCommit live test" for that organization alone, never "All accessible organizations"
   (r5 5); at most 30 days; Code "Read & write" alone.
3. `VISUALCOMMIT_LIVE_AZURE_DEVOPS_TOKEN`, and `VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO` =
   `<organization>/visualcommit-sandbox/visualcommit-sandbox`.

The token's date goes under "Waiting on the owner" (no header carries it).

**GitLab:**
1. A new free account (D346); sign-up may ask for verification once (g2 G5.1). Public e-mail: "Do not
   show on profile".
2. A private project `visualcommit-sandbox`, with a README.
3. A fine-grained token (g2 G5.3), named and described, at most 30 days, "Only my personal projects",
   with (r6 2.2) User: Read; Project: Read; Merge Request: Read, Create, Update, Delete; Branch: Read,
   Delete; Code: Download, Push, and Project: Read among its user permissions too, which listing the
   projects needs (D385, MEASURED 2026-11-05). No second, legacy token is made (D375).
4. `VISUALCOMMIT_LIVE_GITLAB_TOKEN`, and `VISUALCOMMIT_LIVE_GITLAB_REPO` =
   `<username>/visualcommit-sandbox`.

GitHub's token expires on 2026-11-29 (risk 42), and the two new ones at most 30 days after they are
made: the gate's live runs fall before the earliest, or the owner makes a new one.

**The lead's check and the command's process.** The lead checks the six live variables by name, and
each but `VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO` by length. Beyond that it prints only true or false for: GitHub's token starts `github_pat_`; GitLab's
starts `glpat-` (r6 2.4); Azure DevOps' is 84 characters long and contains `AZDO` (r5 5; no index is
tested); `VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO` matches
`^[A-Za-z0-9-]+/visualcommit-sandbox/visualcommit-sandbox$` (D364); `VISUALCOMMIT_LIVE_GITLAB_REPO` ends
`/visualcommit-sandbox`. No character of `VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO` is printed, whole or in
part, nor its length: its first segment is the organization's name (D346, D352). No part of a token is printed beyond
the fixed prefixes `github_pat_` and `glpat-`. GitHub's repository variable keeps 7a's rules. A session
whose shells lack the variables gives all six to the command's own process from the Windows user scope,
never printed, in PowerShell:
`foreach ($n in 'VISUALCOMMIT_LIVE_GITHUB_TOKEN','VISUALCOMMIT_LIVE_GITHUB_REPO','VISUALCOMMIT_LIVE_AZURE_DEVOPS_TOKEN','VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO','VISUALCOMMIT_LIVE_GITLAB_TOKEN','VISUALCOMMIT_LIVE_GITLAB_REPO') { [Environment]::SetEnvironmentVariable($n, [Environment]::GetEnvironmentVariable($n,'User'), 'Process') }; & 'C:\Program Files\Git\bin\bash.exe' scripts/heavy-run.sh --project tests/VisualCommit.LiveHostingTests -c Release`,
followed by a stage's `--filter-class` arguments (15.9). A missing user variable leaves its name unset,
and that class then fails at once. CLAUDE.md's filter `env | grep -v '^VISUALCOMMIT_LIVE_'` covers all
six (D346).

### 15.9 Tests, the lead's steps, done when

**`LiveScreenTests`:** the organization at each boundary, in any case, never inside `orgx` or `org-x`;
addresses and `%40`; learning from made-up copies of r5 1.2's and g1 G3.2's answers (their member names and nesting, with
made-up values, section 1); kept addresses; encoded survivors; withholding;
`FailureOf`; GitHub unchanged. And: a log holding `git clone --progress --
https://***@dev.azure.com/o/p/_git/r r` has no survivor, nor an `accounts.json` whose login is the
organization; a note holding `https://o@dev.azure.com/o/p/_git/r` comes out with both `o`s screened and
the rest kept, and run.txt holding that has no survivor; run.txt holding the organization (raw,
`%2F`-escaped or double-escaped) or a learned address (raw or with `%40`) has one, and D325's address or
D374's made-up organization none; `%C0%AE` ends the decoding; a made-up `instanceId` as `hid=` in a
302's `Location` and in its "Object moved" body (`&amp;hid=`); the same id in a vssps URL
`https://spsprodfake.vssps.visualstudio.com/A<id>/_apis/Identities/…`, in upper case, without hyphens
and URL-escaped; a `context=` whose base64 JSON holds another `hid`; a tenant in `X-VSS-ResourceTenant`
and in a `Bearer` `WWW-Authenticate` after a `Basic` one; the Anonymous `aaaaaaaa-…` id and the area ids `79134c72-…` and `4e080c62-…`
left untouched.

**`LiveSafetyTests`** keeps 7a's cases and adds: the variables; the token after `:`, `oauth2:`,
`x-access-token:` and 4- to 6-character organizations; each `Form` and request id; the guard's
admissions, the repository path itself with nothing after it among them, and its refusals of another
organization, project or repository, a GUID as `{p}` or `{r}`, `{o}/_apis/git/repositories`,
`_apis/projects`, `pullrequests/{n}`, `..`, `%2E%2E`, `%2F`, `%5C`, an empty segment, the organization
host, `vssps`, `http`, another port and another area; D374's path, admitted for the run's made-up
organization without a credential and with Basic only after its 404 in that run (refused before it and
after a 200, a 302 or a 401), and refused for another made-up name and for any other path under the
run's (`{x}/_apis/git/repositories`, `{x}/{p}/_git/{r}`); D374's probe over a stub that answers the read
without a credential 200 sends nothing with the token and notes the skip; GitLab's refusals of another host, a web path, `PRIVATE-TOKEN` and a query token; each new live
API follows no redirect (the handler its public constructor builds has `AllowAutoRedirect` off, and
over a stub's 302 the API records a 302, one request in the store); D373's made-up values: their
shapes, none holding 16 of the token's characters in a row, none noted as written; the clean-up's
selection.

Without the variables, every live test fails at once; none is skipped (7a design 14.8).

**The lead**, before each run with the tokens (stage 1, stage 2, the gate's two runs, a D335 re-run),
first runs the offline classes alone on the same commit; they need no variable and make no request:
`bash scripts/heavy-run.sh --project tests/VisualCommit.LiveHostingTests -c Release --filter-class
"*LiveSafetyTests" --filter-class "*LiveScreenTests"`. It goes on only when they pass: in one process a
failed class stops nothing, and xunit v3 runs the collections in a random order, so the live run itself
cannot gate on them. Then, the owner told, it runs stage 1 (status step 16; about a minute per
provider): `bash scripts/heavy-run.sh --project tests/VisualCommit.LiveHostingTests -c Release
--filter-class "*AzureDevOpsLiveTests" --filter-class "*GitLabLiveTests"`; then WP-LF (0.4); stage 2
likewise. The gate's runs stay the whole project (D335), each after that offline run.

**CLAUDE.md.** Before stage 1's first run, in the commit that records the tokens' dates under "Waiting
on the owner", the lead rewrites "What to know"'s paragraph on the live command: the six variables,
15.8's check (which replaces "by name, length and prefix only" for the new ones), 15.8's PowerShell line
in place of the two-variable one, and the offline run that comes first, whose failure stops the run with
the tokens. At stage 1's merge: the live command's row with the per-class filters, the run.txt
folders `artifacts/live/azure-devops/` and `artifacts/live/gitlab/`, and the sentence that the offline
run comes first and that its failure stops the run with the tokens; the offline row, which becomes
`dotnet test --project tests/VisualCommit.LiveHostingTests -c Release --filter-class "*LiveSafetyTests"
--filter-class "*LiveScreenTests"` and adds the screen of the organization's name and the addresses
(D372); the csproj's comment (1.10). After stage 2: the live command's row with the measured times. 2.9
has the other rows.

**Done when** the project builds outside any solution (D337); the offline classes pass, alone before
each run with the tokens; with the tokens every live test passes, each request read back and cleaned
up, only `visualcommit-live/` branches touched; no run.txt holds a token, the organization, its ids or
an address; no value from the research's captures is in the files (section 1); the tripwire and `ReachProblems` find nothing.

**As built** (WP-LIVE1, `06574c5` to its last commit, with its reviews' and hunts' fixes, 2026-11-02),
stage 1 only; where the build differs or settles what was open:
- `LiveRequest`'s headers are `KeptHeaders` (`api-version` from `Content-Type`); `LiveCredentialForm`
  is public. `LiveReach` judges paths as sent and as written, below `{r}` only item 4's three forms. It
  refuses, read raw, a `Host` header among the request's headers, a method-override header (`_` read
  as `-`) among the request's or the content's, and a method not sent to the path: GET; also POST on
  `pullrequests`, `refs` and `…/merge_requests`; PATCH on `pullrequests/{n}`; PUT and DELETE on
  `…/merge_requests/{iid}`; DELETE alone on a GitLab branch. D374's Basic is admitted once, and never
  after another answer, no answer, or a refused or failed credential-less request naming the made-up
  organization. `ReachProblems(requests, reach)` names origins.
- `LiveServiceApi`, `LiveServiceAnswer` and the fakes beside are in `LiveProbes.cs`, `LiveSteps` in
  `AzureDevOpsLiveTests.cs`, `LiveApp` in `GitLabLiveTests.cs`; the fixtures' internal constructors let
  `LiveSafetyTests` drive the steps over stubs.
- Steps 8 and 9 are `Check_2b_probes`, after Check_2 in both orders, which hands it `LiveMade`;
  it cleans up in `finally`. `LiveSteps.Sums`: Azure DevOps 300, 560, 480, 530, 330 s; GitLab 570, 215,
  540, 385 s. The identity read is each method's first call, once per run.
- Check_2b probes beside the fake's own draft; Azure DevOps' empty title after the abandon, nothing
  ahead from `<branch>-even` once its refs POST took; GitLab's `-even` is pushed through the app. A
  probe with no answer breaks its premise.
- Clean-up and Check_1's leftovers send each call on its own (`LiveSteps.AttemptAsync`); `RefFailures`
  fails each ref a 200 left; a GitLab merge request is the sandbox's when both project ids are equal.
- info/refs probes go as git (`git/<version>`, `Accept: */*`, `Git-Protocol: version=2`), the core
  area as A1 sends it; run.txt adds git's version, the clone's time, the create's status and the head.
- The screen (amending 15.3) also hides GitLab's namespace (D384) and withholds, before `Apply`, a text
  whose reading shows a learned value in a form `Apply` does not replace; `SurvivorIn` also reports any
  `hid=` and searches every reading of the decodings (512 at most); a text that, its raw addresses and
  learned addresses' forms taken out, shows an address in a reading is withheld too (an unlearned
  address at the organization's domain, its at sign escaped). Addresses are learned whole; forms and
  boundaries are in `LiveScreen`'s comments. The Git area id matches g1's. Accepted by the lead: an
  address with `=` or `/` learned from a URL's query or path and quoted elsewhere keeps the head
  before its widest learned tail (the last verification's minor; no answer seen holds one).
- For the lead: the live classes are built, never run (risks: the log-line wait, the push dialog,
  `accounts.json`'s record); CLAUDE.md's rows and the csproj's comment are the lead's (15.9). No file
  outside 15.1's list changed.

## 16. Open points

Each point names what settles it and who meets it.

### 16.1 Settled by the live command (WP-LIVE; stage 1 before part A's checks, then WP-LF)

- The ASSUMED and READ answers listed in 3.7, 4.7, 6.8, 7.5, 8.2, 8.6, 13.9's risks and 15.7's
  premises, D373's made-up values and D374's made-up organization among them, settled by WP-LIVE's
  stage 1 and WP-LF (0.4). What 3.7 and 4.7 keep READ or ASSUMED stays so (D375; g1 G0.6's
  `refs/pull/<n>/source` among them), as do the expired and revoked bodies (15.7).
- `x-access-token` for git on both services (READ, 8.2), measured by stage 1's `Check_2`, and by
  `Check_4` on `<organization>.visualstudio.com`, before part B; a refusal brings ruling 8's fallback, a
  user name per provider (WP-C).

**As built (WP-LF, 2026-11-06).** Stage 1 (Azure DevOps 2026-11-05; GitLab 2026-11-06, after D385's
first run) is in the fakes, marked MEASURED with its date where the run read that answer (listed in
the `.Router.cs` headers):
- Azure DevOps, as the fake answered: a member's connectionData both ways and its 400 at `7.1`; the
  core area; a made-up PAT-shaped value's empty 401; D374's made-up organization, 404 both ways;
  `ListProject`; the create's 201, `isDraft`, `createdBy.id`, `succeeded` and the merge ref at the next
  read, gone after the abandon; `x-access-token` through the helper on both hosts.
- Azure DevOps, corrected: a Basic value in none of the PAT forms (`azdo_FAKE-`, 84 with `AZDO`, 52
  base32, a form ASSUMED: an unknown one's 401, READ) gets connectionData's Anonymous 200 both ways (the empty user
  measured, a user ASSUMED alike); measured errors without `api-version` in `Content-Type`; an empty
  title's 400 in the service's words (a CR LF), `eventId` 0; the create's `labels`; the abandon
  without `mergeStatus` or `lastMergeCommit`; the refs POST's updates without `customMessage` or
  `rejectedBy`, and a create (200 `succeeded`; a folder clash or a name's or folder's case clash
  `refNameConflict`, ASSUMED; a move 400); nine identity names; the empty 401's realm without `.app.` (git's keeps G4.1's).
- GitLab, as the fake answered: every probed status and body. Corrected: `user`'s 41 members and a
  single merge request's in the service's order (the create's without `first_contribution`); a fork's
  pair after `prepared_at` (r6 4.1); with a token `burst_traffic_per_user_free_plan` and 100 (read on
  `GET user`'s 200 and 304, ASSUMED alike elsewhere); D385: `FakeGitLabPermissions.UserSection` and
  `UserProjectRead`, which `GET projects` needs (else check 5's 403 naming `user`: its words measured
  via the app's message, code and headers taken from `self`'s); the default holds both.
- Clients: A1 already refused the Anonymous 200 (`BadCredentials`, `AnonymousDetail`, one request) and
  A2 gives the 403 as `Forbidden` by the code (7.3), both pinned; changed: a message is one line (a
  service's CR LF never splits the log).
- ASSUMED: the Anonymous 200 in a private organization: the run never read the sandbox organization's
  policy, and g1 G0.2 (READ) gives a private one r5 1.3's 302 or 401. Check_1b's 17th probe reads its
  connectionData with no credential, with Suppress; run it before part A's checks: a 401 TF400813
  makes the rule MEASURED; on a 200 the fake keeps the 200 to `PublicProjects`, `LiveAzureFake`'s
  organization public. A1 refuses either.
- Kept (reasons in the headers): git's `info/refs` refusals of the made-up values (one challenge and
  realm; the helper reads no `wwwauth[]`); `queued` in a create's answer; what 3.7 and 4.7 keep (the
  expired and revoked bodies, a read-only write, the fork shapes, a legacy token, other organizations,
  TF401027, 429 and 5xx). WP-S's `ServiceAnswers` shim stays.
- For parts A to N and the review: WP-A words `AnonymousDetail` apart from a refused token. GitLab's
  `Forbidden` with `insufficient_granular_scope` means the route lacks a permission, on A2 Project:
  Read among the user permissions (D385), on A1 User: Read: the texts show GitLab's message (10.2,
  11.2), never a permission derived from the code, and `SignInHelpOf` names both. A fake GitLab token
  with an explicit list needs `UserProjectRead` to list. GitLab's measured 100 a minute (a burst, 304s
  counted) supersedes 7.3's and 4.4's 2,000; the review weighs part P's refreshes and A2's and A4's
  requests against it.

### 16.2 Settled by a measurement, a CI run or the lead

- MTP's order of twelve projects at four lanes (0.6): wave 1's full run (0.4). CI's Test steps against
  four fifths of the new limits (D377; 56 of 70): the lead at each merge before the counted round (0.6).
  Settled by wave 1's run (2026-11-01, 1 h 16 min 56 s, 6,862 tests): MTP starts Phase5Undo,
  Git.Tests, Phase6 and Hosting.Tests, and App.Tests (61 min 20 s of its own) takes the first free
  lane, Hosting.Tests' at 15 min 34 s; the 7b project takes Phase6's, after App.Tests has started, so
  r4 5.2's +15 does not arise. The run ends at Hosting.Tests' time plus App.Tests' own: each minute
  wave 2 adds to Hosting.Tests (0.6's 5 to 9) adds one to the run (status.md's Notes). Wave 2's
  run (2026-11-01, 1 h 14 min 20 s, 7,494 tests) shows the order is not fixed: MTP started
  App.Tests, Phase7a, Git.Tests and Hosting.Tests, App.Tests ran from the start (67 min 28 s of its
  own) and the run ended with Phase5Undo at about 74 min; Hosting.Tests took 17 min 12 s (+1 min 38
  s, under 0.6's 5 to 9). With App.Tests first the run is bound by App.Tests and the last lane's
  three or four walk-throughs, not by Hosting.Tests. Wave 3's run (2026-11-02, 1 h 20 min 21 s,
  7,561 tests) started Hosting.Tests, Phase6Look, Phase7a and Git.Tests; App.Tests (61 min 10 s of
  its own) took the lane the 7a and 7b walk-throughs freed at about 17 min and ended at about 79,
  Phase5Undo last at about 80 (24 min 30 s of its own, 13 min 56 s in wave 2); Hosting.Tests 20 min
  2 s. Each order so far lies within 0.6's 78 to 85 min.
- FETCH_HEAD's line for `refs/merge-requests/<iid>/head` (ASSUMED): WP-P's test on 2.56, CI on 2.43. The
  GitLab twin over HTTP before 2.46: WP-B's `CloneFromAccountTests` on CI's 2.43 containers (4.5's 301).
- Up to three keychain reads at start (9.4) on a slow macOS store, against git's 10 s wait: CI, the gate.
- The intro's two lines (10.3), the `DockPanel` rows (13.4, a `MaxWidth` fallback), the leading ellipsis
  (11.4): their classes and the lead's region comparison (0.7); part A's prefilled Azure DevOps picture
  again at WP-P's merge.
- How Azure DevOps' `remoteUrl` writes a name's space, non-ASCII and reserved characters (`& @ ! ( )`,
  and `'` in a repository's; r5 6.1, READ): space and non-ASCII ASSUMED escaped, the reserved ones
  ASSUMED either way. The live command settles it only where the sandbox's names hold one, which
  `visualcommit-sandbox` does not, so the fake's API answer stands; 8.2 step 4b's credential twin makes
  either spelling of a reserved character work.
- The research folder is outside the repository, so WP-FA, WP-FL and WP-AZ are built on the laptop; the
  scenario's made-up names (2.6) stay (16.4).
- One recognition parser for the three providers after wave 2 (`GitHubUrls.cs:126-186` is copied): the
  lead's choice.
- At closing the lead widens plan risks 35 (g3 N3) and 39 (8.3), adds a risk for what the binding
  cannot see (16.3's github.com gaps, submodules, the cross-host redirect; D379, D381), and writes both
  twins, git's credential view, the drop and 8.6 into `docs/architecture.md` (8.8).

### 16.3 Accepted, with what would reopen them

- GitHub's D278 drop keeps its raw comparison, which misses an unreserved escape (`%52`, g3 G0.5), an
  unsafe character raw on one side and escaped on the other, a reserved character escaped in the
  binding and raw in the other URL, and a binding with a trailing `/` beside another user's URL without
  one (`https://github.com/o/r/` beside `https://bob@github.com/o/r`; MEASURED on 2.31.1, 2.43.0 and
  2.56 by the design review). Extending D369 changes 7a's frozen `Select`, which only the lead reopens.
  Wave 1's adversarial check found more of 7a's github.com rule's gaps, which the new coverages close
  for themselves (D379, D381): beside a bound github.com remote, another user's URL spelt with a
  trailing dot on the host, with `www.`, with `http://`, with white space or a control character in
  its user part, with a host outside printable ASCII, or behind a remote helper's `<name>::` prefix
  can lose D82's order, because git reaches
  github.com and asks for the credential without that user (the token reaches github.com only).
  Reopened by the owner or a later phase that moves GitHub onto `SelectCovered`.
- Submodules (D379's R4): the scope's `GIT_CONFIG_COUNT` pairs reach a submodule's fetch through git's
  on-demand recursion, as in 7a for GitHub; only a submodule URL at or under a bound URL takes the
  token. Reopened by O10.
- A cross-host redirect into a bound path (D381's R6): a remote on a host no coverage covers that
  redirects into an out-of-form path under a binding (X4's `..%5C` shape) gets the account's token
  there, because git builds the credential again from the redirect's target, which `Select` never
  sees; MEASURED on 2.43 and 2.56. The token reaches only the covered host. Stopping it
  (`http.followRedirects=false` with `initial` per bound URL) costs D340's pair count and every
  unbound remote's redirect in a bound scope. Reopened by a measured case where the target is
  another service, or by the owner.
- R2's "any port" (D379) on the fakes' `127.0.0.1` origin (D381's R7): any local server's URL whose
  first segment is outside the organization's grammar counts as a variant of the Azure DevOps fake's
  host and fails it closed, so in a test or a hand run pointed at a fake a bound fake remote can lose
  its binding beside an unrelated local remote; never a token. Reopened by a scenario that puts the
  two side by side.
- A name with a literal `%` is neither recognised nor bound (8.2 step 2 refuses `%25`); Azure DevOps'
  `\`, `%5C`, `%252E`, `%C0%AE` are unmeasured (g3 G6) and refused.
- The app's log and `accounts.json` name the organization (the login, the clone's command line); in the
  live command they stay in its temporary data folder (`%TEMP%`) and are only searched for the learned
  addresses (15.3), and what reaches a note goes through the screen.
- D356's row is cut at 260 px, its tooltip whole; a GitLab fork's label is `{Owner}:{branch}`: part P's
  writer records how both read. The live screen over-screens an organization named by a common word.

### 16.4 Waiting on the owner

- Plan risk 39, carried from 7a: whether the broker refuses git's sign-in prompt without a dialog in a
  bound tab whose account was just refused or signed out (amending D273 and D340), now for three
  providers. Until answered 7b keeps 7a's behaviour.
- Before WP-S's merge (wave 3): 15.8's steps for Azure DevOps and GitLab, the six user variables set,
  and VS Code quit entirely and started again (a new Claude Code session inside a running VS Code does
  not inherit them). The tokens' dates go under status's "Waiting on the owner"; their 30 days must
  cover the gate's two live runs, or the owner renews before them (as for GitHub's token, 2026-11-29,
  plan risk 42; 7a design 14.6).

The owner's answers of 2026-11-01 settled the rest of what this section asked: D367 (amending D252 and
D349; 0.1, 9.8 to 9.10), D373 (15.7), D374 (15.4, 15.7), D375 (16.1, 15.8), D376 (12.4), and the
scenario's made-up names (Fernhill-Labs, harbor-co, northfield, Mobile App; 2.6), none of which matches
the owner's organization, so they stay (D346, D352).

