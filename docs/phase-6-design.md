# Phase 6 design: multi-repo and productivity

Working design notes for phase 6, written on 2026-10-18 before any phase 6 code exists. The
builders implement from this file; its final facts are folded into `architecture.md` at the end of
the phase, after which this file is kept as history, as `phase-3-design.md` to `phase-5-design.md`
are. The last section lists the owner's answers after the reviews and the decisions this design
makes; they are recorded in `decisions.md` from D195 on. Where this file and merged code differ later, the code's doc comments say what is
built; tell the lead. Amended on 2026-10-19 with what wave 1 (WP-S, WP1, WP2) found while building,
each checked with real git or measured (D231).
Amended again on 2026-10-19 with WP3's and WP4's findings (merged in `3146d8d` and `1d99be3`), checked with real git (2.36, replayed on 2.55): 0.5, 1.1 to 1.4, 1.12, 4.5, 4.13, 8.10, 8.13, 10.1 to 10.4, 10.8, 10.9, 14.14, 16.2, 18.
Amended on 2026-10-19 with the visual test report's rulings (`docs/test-reports/phase-6.md`, "The lead's rulings on this report's open points"; the ids in brackets, WD4, WD5, WD35, SD29, SD30 and GD15, are that report's decisions): part L in its own project `VisualCommit.VisualTests.Phase6Look` and the report's counts, the upstream named by its tracking ref, a palette row without a key, and the report's amendments: 2.6, 2.10, 2.12, 5.3, 7.8, 8.1, 8.4, 8.7, 10.4, 11.14, 14, 14.11, 16.2, 17.2 to 17.6, 18.

Sources: `plan.md` "Phase 6", the phase 5 handoff's "For the next phase", decisions D45, D46, D69,
D78, D81, D84, D85, D93, D95, D97, D104, D112, D130, D149, D163, D179, the owner's answers D187 to
D194 and, after the design's reviews, D195 to D202, a code map and ten research notes made by sub-agents on 2026-10-18 (they derived every git
fact with Git 2.36 in throw-away repositories, replayed with the Git 2.55 inside Visual Studio where
words, exit codes or files may differ, and read Avalonia 12.1.3's assemblies for what the platform
does), and the lead's two sets of rulings on the questions those notes and the section drafts
raised. The notes lived in the session's scratch folder; everything this design relies on is copied
into section 0.

The owner's answers, taken as given (2026-10-18, at the start of phase 6):

1. (D187) **The dashboard is a tab of its own**, at the start of the tab strip; opening a
   repository from it opens that repository in a repo tab beside it.
2. (D188) **Opening a workspace shows its dashboard only**; a repository opens as a tab only when
   the user opens it from there.
3. (D189) **Workspaces are kept in `workspaces.json`**, a file of its own beside `settings.json`
   and `session.json`; which dashboards are open is session state, kept with the tabs.
4. (D190) **The identity, the pull mode and the merge tool are git's own configuration**: global,
   and in a "This repository" section the repository's own values, which take precedence.
5. (D191) **The command palette opens with Ctrl+Shift+P** (Cmd+Shift+P on macOS) and lists the
   app's commands and the objects they act on.
6. (D192) **The keyboard shortcuts are a fixed set**: every main action has a key, shown in the
   palette, the menus and the tooltips, and listed in the settings screen.
7. (D193) **Every checkout keeps ignored files by default**; a setting, on by default, turns it off;
   this design proposes which other operations it covers (section 9).
8. (D194) **The theme can be Dark, Light or System**, System following the operating system's mode
   as it changes; Dark stays the default.

The owner's answers after the design's reviews (2026-10-18), taken as given:

9. (D195) **A bulk run's failures stay in their rows**: "Fetch failed" / "Pull failed" with
   "Details…", which opens the same error dialog; no dialog opens during the run, and a single row's
   own Fetch or Pull opens it at once; amends D95 for bulk runs (section 4.6).
10. (D196) **Keep ignored files protects every operation that can lose one**: every checkout, merge,
    rebase, cherry-pick, revert and stash apply or pop, but not the hard reset, whose snapshot already
    saves what it overwrites (section 9).
11. (D197) **The Settings button sits at the start of the toolbar's right group**, left of Search
    (7.2).
12. (D198) **Of the contrast fixes, the light lane colours 1, 2, 4 and 7 change**, with the light
    warning, success and danger text; the dark danger button keeps white text, and the dark theme's
    gaps are left to phase 8's accessibility pass (11.10 to 11.13).
13. (D199) **The fixed key table of 6.1 is adopted as it is**, the commands without a key named and
    reached through the palette; every keyed button shows its key in its tooltip, the commit button and
    the resolver's and the interactive list's buttons included; the commit command is also a palette
    entry (5.4, 6.1, 6.8).
14. (D200) **The Git section always opens on "All repositories (global)"**; "This repository" is one
    click away, and edits in both scopes are kept while switching (7.4).
15. (D201) **A ref one remote fetches from two refs checks out without an upstream** (GitHub's
    `refs/pull/*/head` line beside the default), saying so in the status bar; push and delete onto it
    stay disabled with R2, and "Create branch here…" from it passes `--no-track` (10.4, 10.6).
16. (D202) **The merge tools git knows are looked up in the background** once start-up has settled,
    and the last list is kept in the data folder by git's path and version, so "Choose…" is ready at
    once (8.10).

17. (D221, confirmed by the owner the same day) **Turning Keep ignored files off restores phase 5
    exactly**: D193's "checkouts then follow git's own default" is read as turning off D193's
    extension only, so a checkout of an older commit of the current branch keeps D179's
    `--no-overwrite-ignore` whatever the setting, and undo and redo keep D169's guards (9.1).

Conventions, as in phase 5's design: `<b>` a local branch's short name, `<r>` a remote, `<sha>` a
full commit id, "short SHA" its first 7 characters, "Mod" the platform's command modifier (Ctrl, or
Cmd on macOS, as `HotkeyConfiguration.CommandModifiers` reports it). The app passes git full ref
names or full SHAs wherever git accepts them. Every git call goes through `GitRunner` with D30, D68,
D74, D102 and D128's environment; reads add `GIT_OPTIONAL_LOCKS=0` (D47). Counts in texts use the
singular for one (D113). Sizes are logical pixels at 1100×700 and 1920×1080. Phase 6 adds no
package.

## Contents

0. Facts checked before deciding
1. Workspaces
2. The dashboard tab
3. The repositories behind the rows
4. Bulk fetch and pull
5. The command palette
6. Keyboard shortcuts
7. The settings screen
8. Git's own settings
9. Keeping ignored files
10. Fetch refspecs
11. Theme
12. Open in editor and Open file
13. The welcome page and the tab strip
14. Texts
15. UI inventory: menus, dialogs, colours, icons, automation ids
16. What changes for earlier phases
17. Tests and harness
18. Work breakdown
19. Decisions

## 0. Facts checked before deciding

Each subsection gives the facts its section relies on: the commands run, git's output and exit
codes on Git 2.36.0.windows.1 (the development machine's, whose words the reports quote) and, where
they may differ, on Git 2.55.0.windows.5, and the measurements, taken on the development laptop
while other work loaded it unless said otherwise. Experiments ran in throw-away repositories cut off
from the machine's configuration.

### 0.1 Reading a repository's state for the dashboard

Run on 2026-10-18 with Git 2.36.0.windows.1 and 2.55.0.windows.5 in throw-away repositories,
isolated as the tests are (`GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`, `HOME` and
`XDG_CONFIG_HOME` in the lab, `LANGUAGE=en`, `LC_ALL=en_US.UTF-8`, `GIT_TERMINAL_PROMPT=0`), every
read with `GIT_OPTIONAL_LOCKS=0` (D47). Both gits gave the same output and exit code unless a row
says otherwise. Timings come from the shared laptop and are indications.

| What | Result |
|---|---|
| What a row reads | The tab's own reads, nothing new (3.1): `ReadRefsAsync` (`src/VisualCommit.Git/GitRepository.Refs.cs:32-54`), five calls side by side (from phase 6 `git config --null --get-regexp ^remote\.` in place of `git remote`, which also gives section 10's fetch refspecs: still five): `git for-each-ref --format=<RefFormat, 11 fields> refs/heads refs/remotes refs/tags`, `git remote`, `git stash list --no-show-signature --no-color --encoding=UTF-8 --format=%H%x1f%P%x1f%aN%x1f%aE%x1f%aI%x1f%cI%x1f%gs`, `git symbolic-ref -q HEAD`, `git rev-parse -q --verify HEAD` (plus `rev-parse <tag>^{}` for a tag of a tag); and `ReadStatusAsync` (`GitRepository.Changes.cs:19-29`): `git status --porcelain=v2 -z --untracked-files=all --renames`, then the operation in progress from the git folder's files (D89; two to four more calls only while one is in progress). Six processes per read; the first read adds `OpenAsync`'s `git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir` (`GitRepository.cs:87`) |
| Opening a folder (`OpenAsync`) | A repository, and an unborn one: exit 0 (0.8's first row). A plain folder: exit 128, `fatal: not a git repository (or any of the parent directories): .git` (`NotARepositoryException`). A bare repository: exit 128, stdout `false`, `fatal: this operation must be run in a work tree` (`NotARepositoryException`). A folder inside another repository whose own `.git` was removed: exit 0 with the outer repository (0.8), and `git status` there reads the outer repository with exit 0 (`? inner/i.txt`). A missing folder: `git -C missing …` exits 128 (`fatal: cannot change to 'missing': No such file or directory`), but `OpenAsync` tests `Directory.Exists` first (`GitRepository.cs:80-83`), and `Process.Start` with a missing working folder throws without an exit code (seen on .NET Framework only). An orphaned linked worktree: exit 128, `fatal: not a git repository: <path>` (2.36), `fatal: not a git repository: (NULL)` (2.55), `fatal: gitfile does not point to a valid repository: <path>/.git` (2.56, found by CI run 39831538867; also for a `.git` file naming a plain folder), all `NotARepositoryException` (`431fb42`); a `.git` file that is no gitfile, `fatal: invalid gitfile format: <path>` on all three, stays git's text (a `GitException`). A repository owned by another user: exit 128, `fatal: unsafe repository ('<path>' is owned by someone else)` (2.36), `fatal: detected dubious ownership in repository at '<path>'` (2.55), each followed by git's `safe.directory` advice: a `GitException` with git's text |
| Branch and upstream (`for-each-ref --format="%(refname) [%(upstream)] [%(upstream:track)]" refs/heads`, exit 0) | Level `refs/heads/main [refs/remotes/origin/main] []`; ahead `[[ahead 1]]`; behind `[[behind 1]]`; diverged `[[ahead 1, behind 1]]`; upstream deleted on the remote and pruned `refs/heads/feature [refs/remotes/origin/feature] [[gone]]`; no upstream `refs/heads/topic [] []`; no remote `refs/heads/main [] []`; unborn: no line. `ParseTrack` (`GitRepository.Refs.cs:218-240`) makes them `GitRef.Ahead`, `Behind` and `UpstreamGone`, so a row's counts are the left panel's |
| HEAD | `symbolic-ref -q HEAD`: `refs/heads/main` exit 0 on a branch and when unborn, nothing and exit 1 when detached. `rev-parse -q --verify HEAD`: the SHA exit 0, nothing and exit 1 when unborn |
| Why not one `status --porcelain=v2 --branch` per row | A branch may be named `(detached)`: `git check-ref-format --branch "(detached)"` prints it (exit 0), `git branch "(detached)"` and `git checkout -q "(detached)"` exit 0, and `status --porcelain=v2 --branch -z` then prints `# branch.head (detached)` as for a detached HEAD, while `symbolic-ref -q HEAD` prints `refs/heads/(detached)` (exit 0) against nothing (exit 1). An upstream without a `# branch.ab` line means "gone" only on a born branch: the clone of an empty repository prints `# branch.oid (initial)`, `# branch.head main`, `# branch.upstream origin/main` and nothing else. A stopped rebase prints `(detached)` and no upstream. Pull and the toolbar's states need `RepoRefs` anyway |
| The changes (`status --porcelain=v2 -z --untracked-files=all --renames`, exit 0) | A changed file `1 .M N... … a.txt`; one staged and changed again `1 MM N... …`; a new staged file `1 A. … s.txt`; untracked `? u.txt`; conflicted `u UU N... … b.txt`; a clean tree nothing. `status.showUntrackedFiles=no` hides untracked files unless the option is given, which the app always gives. A file staged and changed again counts as staged and as unstaged, as in the stage panel |
| `FETCH_HEAD` as "last fetched" | A fetch with nothing new rewrites it (207 bytes here); `git fetch broken` (`fatal: '../no-such.git' does not appear to be a git repository`, exit 128) leaves it **0 bytes with a new time**, on both gits. Its time is that worktree's last attempt by any program, failed ones included; a clone writes none; the app's fast-forward writes none (`--no-write-fetch-head`). So no "last fetched" column (2.6) |
| Cost of one read | Quiet machine (5 to 20 % CPU): `git --version` 98 to 103 ms (2.36) and 147 to 158 ms (2.55); one `status` 119 to 124 and 161 to 175 ms; a row's read (the six calls side by side) 270 to 405 and 335 to 399 ms. At 100 % CPU the same day: `git --version` 419 to 665 ms, a row's read 744 to 1558 ms. One status read costs about one process start in a small repository (76 to 300 ms by load); about 0.2 s in a 20,000-file tree with `core.fscache=true`, 0.9 to 1.0 s without |
| Ten rows (clean, ahead, behind, diverged, changes, conflict, detached, no upstream, gone, no remote) | Quiet machine, 2.36 / 2.55: four at a time 1766 to 1880 / 1856 to 2052 ms; all ten at once 1692 to 1761 / 1941 to 2219 ms; one after another 2751 to 2892 / 2942 to 3189 ms; the first show (opening and reading, four at a time) 1954 to 2024 / 2442 to 2578 ms. At 100 % CPU four at a time 3.9 to 10.2 s (2.36) and 4.3 to 4.9 s (2.55). Measured from Git Bash, which starts one `sh` per row and forks each call: upper bounds, the app starts git from .NET. One `status --branch` per row instead would cost about a third (10 × (rev-parse + status) side by side: 525 to 668 ms) |
| A stale index (2.36, a 20,000-file tree) | `GIT_OPTIONAL_LOCKS=0` keeps every read from writing (D47), so also from refreshing the index's stat data: after every file was touched with its content unchanged, **each read took 3.1 s with `core.fscache=true` (6.1 s without), every time**, until something wrote the index (a terminal `git status`, `git update-index -q --refresh`, any write of the app's); then 0.12 to 0.13 s. The tabs' `ReadStatusAsync` pays the same today. Plan risk 29 (16.2) |
| A folder on a share that does not answer (the reviews, Windows PowerShell 5.1, the Win32 call .NET makes) | `[IO.Directory]::Exists('\\no-such-host\share\repo')`: `False` after 2.5 to 4.8 s; `[IO.Directory]::Exists('\\10.255.255.1\share\repo')` (an address nothing answers): `False` after 271.8 s, twice. Windows keeps no negative answer, so every check pays it again. A missing local drive or a local path from another machine answers at once; a hung NFS or SMB mount on Linux or macOS can block for ever (not measured). `GitRepository.OpenAsync` makes the same call before its first await (`GitRepository.cs:80-83`), on the caller's thread: `GitRepositoryProvider.OpenAsync` (`Services/GitRepositoryProvider.cs:28-32`) goes on inline once git is found, so today a restored tab's open makes it on the UI thread. Hence 2.10's checks on the thread pool |
| fsmonitor | With the owner's `core.fsmonitor=true` the first `status` in a repository starts a `fsmonitor--daemon` there (`git fsmonitor--daemon status`: `fsmonitor-daemon is watching '<path>'`, exit 0) that stays after the app exits: a dashboard of ten repositories leaves ten daemons. Accepted as git's behaviour with the user's configuration (Q5), noted in the handoff (16.2). Under `GIT_OPTIONAL_LOCKS=0` the reads never write the daemon's token into the index, so they gain nothing from it until something else writes the index |

### 0.2 Fetch and pull outcomes on 2.36 and 2.55

Run on 2026-10-18 in `scratchpad/p6-lab/sec-04/` (r02's `pull-cases.sh` with cases E3, E4, N, L and M
added; outputs `pull-v236.txt` and `pull-v255.txt`) with Git 2.36.0.windows.1 and 2.55.0.windows.5
(Visual Studio's, its `mingw64/bin` first on `PATH`), isolated as the tests are (`GIT_CONFIG_NOSYSTEM=1`,
an empty `GIT_CONFIG_GLOBAL`, `HOME` and `XDG_CONFIG_HOME` in the lab, `GIT_TERMINAL_PROMPT=0`,
`LANGUAGE=en`, `LC_ALL=en_US.UTF-8`, `MSYSTEM` unset). Each case: a fresh bare `origin.git` (`main`,
`f.txt` = one/two/three), a clone `work` whose `main` tracks `origin/main`, a clone `other` that pushes
the upstream's commits. The commands are the app's pull steps (D84, `IntegratePullAsync`,
`GitRepository.Remotes.cs:123-237`): `git fetch --progress origin`, then `git merge --no-edit
FETCH_HEAD` (nothing set), `git merge --ff-only FETCH_HEAD` (`pull.ff=only`, or a rebase setting on a
branch with nothing of its own) or `git rebase` (`pull.rebase=true`). Output is standard error and
output together, lines holding `%` dropped (as the app's cleaner does); every fetch exited 0 unless the
row says otherwise. Cases E5 and E6, and E with 9.10's `--no-overwrite-ignore`, were run during the
design's revision, isolated the same way, in `scratchpad/p6-lab/sec-04-e5/` (`e5.sh`, `e-ignore.sh`;
outputs `e5-v236.txt`, `e5-v255.txt`, `e-ignore-v236.txt`, `e-ignore-v255.txt`).

| Case | Integration | Output, 2.36 | Exit 2.36 | What 2.55 prints differently | Exit 2.55 |
|---|---|---|---|---|---|
| A fast-forward | `merge --no-edit FETCH_HEAD` | `Updating 48f7874..cee16fd` / `Fast-forward` / ` g.txt \| 1 +` / ` h.txt \| 1 +` / ` 2 files changed, 2 insertions(+)` | 0 | the fetch's `pack-reused 0 (from 0)` | 0 |
| B up to date | `merge --no-edit FETCH_HEAD` | `Already up to date.` (the fetch printed nothing) | 0 | nothing | 0 |
| C diverged, conflict | `merge --no-edit FETCH_HEAD` | `Auto-merging f.txt` / `CONFLICT (content): Merge conflict in f.txt` / `Automatic merge failed; fix conflicts and then commit the result.`; `status --porcelain`: `UU f.txt` | 1 | nothing | 1 |
| D1 unstaged change in the way of a fast-forward | `merge --no-edit FETCH_HEAD` | `error: Your local changes to the following files would be overwritten by merge:` / `\tf.txt` / `Please commit your changes or stash them before you merge.` / `Updating 48f7874..f42f336` / `Aborting` | 1 | `Updating …` after `Aborting` (the two streams interleave) | 1 |
| D2 unstaged change in the way of a real merge | `merge --no-edit FETCH_HEAD` | the same block and `Aborting`, no `Updating` | **128** | adds `Merge with strategy ort failed.` | **2** |
| D3 staged change (another file), real merge | `merge --no-edit FETCH_HEAD` | `Your local changes to the following files would be overwritten by merge:` / `  s.txt` (two spaces, no `error:`) | **128** | `error: Your local changes …:` / `  s.txt` / `Merge with strategy ort failed.` | **2** |
| D4 untracked file in the way of a fast-forward | `merge --no-edit FETCH_HEAD` | `error: The following untracked working tree files would be overwritten by merge:` / `\tn.txt` / `Please move or remove them before you merge.` / `Updating …` / `Aborting` | 1 | `Updating …` after `Aborting` | 1 |
| E diverged, `pull.ff=only` | `merge --ff-only FETCH_HEAD` | `fatal: Not possible to fast-forward, aborting.`; the same with 9.10's `merge --ff-only --no-overwrite-ignore FETCH_HEAD` | 128 | **nine `hint:` lines before the `fatal:` line**: `hint: Diverging branches can't be fast-forwarded, you need to either:` / `hint:` / `hint: \tgit merge --no-ff` / `hint:` / `hint: or:` / `hint:` / `hint: \tgit rebase` / `hint:` / `hint: Disable this message with "git config set advice.diverging false"` | 128 |
| E2 fast-forward, `pull.ff=only` | `merge --ff-only FETCH_HEAD` | `Updating …` / `Fast-forward` / ` g.txt \| 1 +` / ` 1 file changed, 1 insertion(+)` | 0 | nothing | 0 |
| E3 up to date, `pull.ff=only` | `merge --ff-only FETCH_HEAD` | `Already up to date.` | 0 | nothing | 0 |
| E4 diverged, `pull.ff=only` and `pull.rebase=true` | `merge --ff-only FETCH_HEAD`; then `git pull` itself | both `fatal: Not possible to fast-forward, aborting.` (`pull.ff=only` wins over a rebase setting in `git pull` too, as in the app) | 128, 128 | both with E's nine hints | 128, 128 |
| E5 diverged, `merge.ff=only`, `pull.ff` unset (E6: `branch.main.mergeOptions=--ff-only` instead, the same) | `merge --no-edit FETCH_HEAD`, also with `--no-overwrite-ignore` | `fatal: Not possible to fast-forward, aborting.` (the merge's own setting refuses, with no `--ff-only` on the command line) | 128 | E's nine hints | 128 |
| N `pull.rebase=true`, nothing of its own | `merge --ff-only FETCH_HEAD` | as E2 | 0 | nothing | 0 |
| F1 diverged, `pull.rebase=true`, clean | `rebase` | `Rebasing (1/1)` / `ESC[KSuccessfully rebased and updated refs/heads/main.` | 0 | no `ESC[K` | 0 |
| F2 the same with a conflict | `rebase` | `Auto-merging f.txt` / `CONFLICT (content): Merge conflict in f.txt` / `Rebasing (1/1)` / `error: could not apply 60005e9... Local edit` / four `hint:` lines / `Could not apply 60005e9... Local edit`; `UU f.txt` | 1 | `Rebasing (1/1)` first; a fifth hint `hint: Disable this message with "git config set advice.mergeConflict false"`; last line `Could not apply 60005e9... # Local edit` | 1 |
| F3 the same with an unstaged change | `rebase` | `error: cannot rebase: You have unstaged changes.` / `error: Please commit or stash them.` | 1 | nothing | 1 |
| G no upstream (`git pull` itself; the app does not start) | `pull` | `There is no tracking information for the current branch.` … `git branch --set-upstream-to=origin/<branch> topic` | 1 | nothing | 1 |
| H detached HEAD (`git pull` itself) | `pull` | `You are not currently on a branch.` / `Please specify which branch you want to merge with.` / … | 1 | nothing | 1 |
| I upstream deleted on the remote | none: the app stops here | the fetch prints nothing; `FETCH_HEAD` holds only `<sha>\tnot-for-merge\tbranch 'main' of …`; a `merge --no-edit FETCH_HEAD` would print `Already up to date.` (0) | 0 | nothing | 0 |
| J the remote cannot be reached | the fetch | `fatal: '<path>/nowhere.git' does not appear to be a git repository` / `fatal: Could not read from remote repository.` / `Please make sure you have the correct access rights` / `and the repository exists.` | 128 | nothing | 128 |
| K a merge in progress (the toolbar refuses first) | `merge --no-edit FETCH_HEAD` | `error: Merging is not possible because you have unmerged files.` / two `hint:` lines / `fatal: Exiting because of an unresolved conflict.` | 128 | nothing | 128 |
| L two remotes, one unreachable (one fetch per remote, D109) | `git remote` → `broken`, `origin`; `fetch --progress broken`; `fetch --progress --no-auto-maintenance origin` | `broken`: J's four lines, 128; `origin`: fetched, 0 | 128, 0 | nothing | 128, 0 |
| M no remotes | `git remote`; `git fetch --progress` | nothing, 0; nothing, 0 | 0 | nothing | 0 |

What the table settles:

- **Exit codes do not classify.** A merge refused for local changes exits 1 (D1, D4), 128 (D2, D3 on
  2.36) or 2 (D2, D3 on 2.55). `GitFailureClassifier` reads the text only
  (`GitFailureClassifier.cs:71-73`), and every refusal keeps its classifying line on both versions:
  `would be overwritten by merge:` (D1 to D4), `Not possible to fast-forward` (E, E4, E5, E6), `cannot
  rebase:` (F3). 2.55's hints come before E's `fatal:` line and carry tabs, but under no block header,
  so `BlockedPaths` stays empty.
- **E, E5 and E6 have no summary of their own today**: `PullRefusalDisplay`
  (`RepositoryOperations.Remotes.cs:144-147`) sends `git merge …` failures to `BlockedByChangesDisplay`
  (`RepositoryOperations.Protect.cs:321-331`), which returns null for `GitFailureKind.NotFastForward`
  without blocked paths, so the dialog says "Git could not pull origin/main into main.". 4.8 adds one
  for a `git merge --ff-only …` refusal (E, E4), which only `pull.ff=only` gives; E5 and E6 fail the same
  way on a plain `git merge --no-edit …`, refused by `merge.ff` or `branch.<b>.mergeOptions`, which a
  `pull.ff` text would misname, so they keep the default.
- **A repository without remotes "fetches" successfully**: `FetchAsync(null, …)` runs no fetch when it
  finds no remote (`GitRepository.Remotes.cs:43-63`), so the toolbar's Fetch says "Fetched from all
  remotes". The bulk run leaves such rows out by the toolbar's reason (4.2).

Two more facts, for the bulk run's limits:

| What | Result |
|---|---|
| Two fetches at once into two worktrees of one repository | `work` (a clone) and `work2` (`git -C work worktree add ../work2 -b side`), 1,500 new branches pushed to `origin`, then `git -C work fetch --progress origin` and `git -C work2 fetch --progress origin` started together. **2.36** (one round): `work` exit 0; `work2` exit 1 with 4 lines `error: cannot lock ref 'refs/remotes/origin/b1091': unable to resolve reference 'refs/remotes/origin/b1091': Permission denied`. **2.55** (three rounds of three): `work` exit 0 every time; `work2` exit 1 every time, with 37, 37 and 21 lines `error: cannot lock ref 'refs/remotes/origin/rv255x1-1': Unable to create '<work>/.git/refs/remotes/origin/rv255x1-1.lock': File exists.` A review's re-run on 2.36 (four rounds of 1,500): no collision in the first, both fetches exit 1 in the second and third, only the second fetch in the fourth; no lock file was left, every ref was there afterwards (what one fetch failed to write, the other wrote) and the next fetch worked. **One new commit is enough** (a review's re-run in `scratchpad/p6-review/lab/verify-perf-threads-6/`, `work2`'s upload-pack input held 4 s while `work`'s fetch ran; 2.36 and 2.55 alike): `work` exit 0, `work2` exit 1 with `error: cannot lock ref 'refs/remotes/origin/main': is at <new> but expected <old>` and ` ! <old>..<new>  main       -> origin/main  (unable to update local ref)`. A fetch checks each ref's old value, read before its transfer, so two fetches of one common git folder that overlap while the remote has something new lose one of them, few refs or many; a fetch that finds nothing new writes no shared ref. Both worktrees write the remote-tracking refs of the one common git folder (so do two tabs on one worktree, 3.5). Fetches in two different repositories share nothing (`FETCH_HEAD` is per worktree: `git rev-parse --git-path FETCH_HEAD` in a linked worktree prints `…/.git/worktrees/<n>/FETCH_HEAD`, r08 2.5) |
| How many prompts can wait at once (read in the code, not run) | `AskPassBroker.MaxConnections = 4` (`AskPassBroker.cs:26`) is both the semaphore of connected helpers (`:36`) and the pipe's instance count (`:205`, `:214`); a connection holds its slot until the user answers (`ServeAsync`, `:219-247`). The helper connects with a 5-second limit (`AskPassProtocol.ConnectTimeout`, `AskPassProtocol.cs:32`; `VisualCommit.AskPass/Program.cs:52`) and on a timeout exits 1 printing nothing (`:17-25`); git then fails with `could not read Username …`, which the classifier calls `AuthenticationFailed`: "Fetch failed", not cancelled. Once connected, the helper waits for the answer without a limit (`Program.cs:61-63`). So four prompts open at once make a fifth git's prompt fail within 5 s |

### 0.3 Git's configuration on 2.36 and 2.55

Run on 2026-10-17 and 2026-10-18 with Git 2.36.0.windows.1 and 2.55.0.windows.5 (Visual Studio's,
its `mingw64/bin` first on `PATH`) in throw-away folders, isolated as section 0 says, with `HOME`
and `XDG_CONFIG_HOME` in the lab as well (`<LAB>`). Both gits alike unless a row says otherwise.

| What | Result |
|---|---|
| The read | `git config --show-scope --show-origin -z --list` prints per entry `<scope>\0<origin>\0<key>\n<value>\0`. A key written without a value (a bare `rebase` under `[pull]`) prints `<key>\0` with **no** `\n`, unlike an empty value (`<key>\n\0`). Scopes `system`, `global`, `local`, `worktree`, `command`. Origins `file:<path>`: the repository's own file relative (`file:.git/config`) in the main worktree, absolute in a linked one; `GIT_CONFIG_PARAMETERS`, `GIT_CONFIG_COUNT` and `-c` print `command line:`. Without `-z` a path with a backslash is C-quoted (`file:"C:\\Users\\…"`), with `-z` nothing is. Paths git builds itself (`HOME`, `XDG_CONFIG_HOME`) have forward slashes. Byte-identical on both gits (`cmp`, 2026-10-18). Exit 0, also with nothing set (no output) |
| Includes | An included file's entries take the including file's scope, with the included file as origin (`global	file:<LAB>/inc.gitconfig	merge.tool=meld`); `.git/config`'s includes show as `local`. `includeIf "gitdir:…"` and `"onbranch:…"` apply only inside a matching repository; elsewhere their entries are absent. An MSYS-style include path (`/c/Users/…`) is not loaded on Git for Windows, which warns three times per call (`warning: encountered old-style '/c/Users/…' that should be '%(prefix)//c/Users/…'`) |
| Outside a repository | System and global entries only, exit 0. A data folder inside a repository (a home folder kept in git) lists that repository's `local` entries, or fails with `fatal: detected dubious ownership` when another user owns it |
| `--global --get`, `--global --list` | Read only the file `--global` writes, without includes: with both `~/.gitconfig` and `$XDG_CONFIG_HOME/git/config`, `--global --list` leaves the XDG file out; an included value is invisible (`--global --get merge.tool` exit 1 while `--get merge.tool` gives `meld`). Never used by the app (8.1) |
| Which file `--global` writes | `GIT_CONFIG_GLOBAL` when set (created when missing, if its folder exists; set but empty, no global file is read, `~/.gitconfig` included, and a write fails, exit 4, found while building WP2); otherwise `~/.gitconfig` if it exists, else `$XDG_CONFIG_HOME/git/config` (`~/.config/git/config` with the variable unset) if only that exists; with neither, `~/.gitconfig` is created. `~` is `HOME`; on Windows with `HOME` unset it is `HOMEDRIVE`+`HOMEPATH` when both are set and that folder exists, else `USERPROFILE`: with `HOME`, `XDG_CONFIG_HOME` and `GIT_CONFIG_GLOBAL` unset and the two set to different folders, reads and a `--global` write used `HOMEDRIVE`+`HOMEPATH`'s `.gitconfig` and left `USERPROFILE`'s alone; with `HOMEPATH` naming a missing folder, or unset, `USERPROFILE`'s (2026-10-18, 2.36's `mingw64\bin\git.exe` and `cmd\git.exe` and 2.55's alike). A plain read reads both, the XDG file first, so `~/.gitconfig` wins. With `GIT_CONFIG_GLOBAL` set neither is read, **but `$XDG_CONFIG_HOME/git/ignore` (or `$HOME/.config/git/ignore`) still is**: its `*.secret` hid `a.secret` from `git status` under an empty lab global file. `git var GIT_CONFIG_GLOBAL` names the global files on 2.55 (exit 0); on 2.36 it is a usage error (`usage: git var (-l \| <variable>)`, exit 129) |
| Writing | `git config --global --replace-all <key> <value>` adds an absent key, replaces a single value and every value of a multi-valued key (exit 0); the plain form refuses a multi-valued key (`error: cannot overwrite multiple values with a single value`, exit 5). `--replace-all pull.rebase true` turns a bare key into `rebase = true` (both, 2026-10-18). `--unset-all` removes every value (exit 0; an emptied section goes too) and **exits 5 for an absent key**; `--unset` exits 5 on a multi-valued key. A value starting with `-` is a value (`--replace-all user.name "-n Bob"` stores `-n Bob`, exit 0). Round-trips: `Ann "The Dev" O'Neil`, `Zoë 日本`, `  padded  ` (stored quoted), a line break (stored `\n`), `a # not comment ; semi \ back`. `--local` in a linked worktree writes the common `.git/config`, with or without `extensions.worktreeConfig`; `--worktree` without the extension: `fatal: --worktree cannot be used with multiple working trees unless the config extension worktreeConfig is enabled.` (exit 128) |
| Writes that fail | A held lock (`<file>.lock` present): `error: could not lock config file <file>: File exists`, **exit 255**, `--global` and `--local` alike. `GIT_CONFIG_GLOBAL` in a missing folder: `error: could not lock config file <LAB>/nofolder/gitconfig: No such file or directory` (255), the folder not created, reads empty (`--global --get` exit 1, `--list` exit 0; also 0.10) (both cases on both gits, 2026-10-18). A folder the user may not write: `… Permission denied`, 255. A read-only file is written anyway on Windows (exit 0). A broken file (`[user` unclosed) fails every git call, `git --version` included: `fatal: bad config line 1 in file <file>`, exit 128. Git's manual's exit 4 ("cannot write") occurred only with `GIT_CONFIG_GLOBAL` set but empty, found while building WP2 (both gits): `error: could not write config file : Is a directory`, exit 4; `--unset-all` there exits 5 (8.2) |
| A value in an included file | The write goes into the main file and may not win: with `[user] email = e@x` above `[include] path = inc.gitconfig` (which sets `user.name = Inc`), `git config --global user.name New` writes into the existing `[user]` above the include, and `--get user.name` still gives `Inc` (`--global --get` gives `New`). `--global --unset merge.tool` of an included value exits 5 and changes nothing. A repository's own includes alike. Hence every write is read back (8.4) |
| `includeIf` and a global write | With `[user] email = personal@example.com` and `[includeIf "gitdir:<LAB>/repo1/"] path = work.gitconfig` (setting `user.email = work@example.com`) in the global file, the read in `repo1` lists global `personal@example.com`, then global `work@example.com` from `work.gitconfig`, which wins; the read in the data folder under 8.1's ceiling lists only `personal@example.com`. A global write of `user.email` lands in `[user]` above the include and replaces the personal address: in `repo1` git still uses `work@example.com`, while every repository outside the include now commits with the new value (`git var GIT_AUTHOR_IDENT`; 2.36, 2026-10-18). Hence 8.2's global scope from the data folder's read |
| Worktree scope | With `extensions.worktreeConfig=true` and `git config --worktree user.name "WT Name"` in a linked worktree, the read there lists `local … Local Name` then `worktree file:<LAB>/A/repo/.git/worktrees/wt/config.worktree WT Name`; the worktree value wins |
| Pull: one key at a time | Diverged branches, `git pull` in a copy: nothing set → `fatal: Need to specify how to reconcile divergent branches.` (128); `pull.rebase` `true`, `merges`, `interactive`, `i`, `1`, `yes` rebase (`merges` with `--rebase-merges`; `interactive` with the list confirmed unchanged, D102); `false` merges; **`m` merges on 2.36 and rebases with `--rebase-merges` on 2.55**; `preserve`, `p`, `b`, `foo` → `fatal: invalid value for 'pull.rebase': '<v>'` (128). `pull.ff` `true` and `false` merge; `only` → `fatal: Not possible to fast-forward, aborting.` (2.55 adds a `hint: Diverging branches can't be fast-forwarded…` block; 128); `foo` → `fatal: invalid value for 'pull.ff': 'foo'` |
| Pull: case and numbers | 2026-10-18, diverged branches: `pull.rebase` `TRUE`, `True`, `Yes`, `ON`, `2`, `0x10`, `-1`, `010` and `1K` rebase on 2.36 (`TRUE`, `Yes`, `0x10` and `1K` also on 2.55), `0x0` and `00` merge: the boolean words ignore case, and a number is an integer in decimal, `0x` hex or leading-`0` octal with an optional unit `k`, `m` or `g` in either case, which must fit in 32 bits (`99999999999` is a `bad boolean config value` on both). `merges`, `interactive`, `i` and `only` compare exactly: `pull.rebase` `MERGES`, `Interactive`, `I` and `pull.ff` `ONLY`, `Only` → `fatal: invalid value for '<key>': '<v>'` (128) on both. A unit letter alone: `k`, `G` and `M` merge on 2.36 (read as false); on 2.55 they are bad booleans (`pull.rebase=M`: `fatal: invalid value`, 128), while `m` is `merges` there (row above). Pull's reader (`GitRepository.Remotes.cs:611-625`) lower-cases and asks `git config --type=bool`, so it follows each git |
| Pull: combinations | `pull.ff=only` beats every rebase setting (`pull.rebase=true`, `merges`, `branch.main.rebase=true`): refused. `branch.<b>.rebase` beats `pull.rebase` both ways, and an invalid `pull.rebase` is then never read (`branch.main.rebase=true pull.rebase=foo` rebases). `pull.ff=false` is ignored when rebasing. `pull.rebase=false merge.ff=only` refuses (`merge.ff` reaches pull's merge); `pull.rebase=false pull.ff=true merge.ff=false` fast-forwards (`pull.ff=true` beats `merge.ff`). Branch subsections are case-sensitive and may hold `/`, `.` and spaces (`branch.Feature/x.y.rebase`) |
| Pull: precedence per key | Global `pull.ff=only` with local `pull.rebase=true`: refused; with local `pull.rebase=true pull.ff=true`: rebases. Global `pull.ff=only` with local `pull.rebase=false`: refused; with local `pull.rebase=false pull.ff=true`: merges. Git for Windows' installer writes its pull choice into the **system** file: here `C:\Program Files\Git\etc\gitconfig` holds `pull.rebase = false` |
| Pull: when git reads its settings | `GIT_TRACE=1 git pull` with `pull.rebase=foo` stops at `fatal: invalid value for 'pull.rebase': 'foo'` **before fetching** (the remote-tracking branch unchanged). The app today fetches first, then fails with "pull.rebase is set to foo, which this app does not understand." (8.9). `git pull --rebase` reads no rebase setting but does read `pull.ff`, refusing `foo`, `ONLY` (and `k` on 2.55) with `fatal: invalid value for 'pull.ff'` before fetching, while `only` and `false` give way to the rebase (2.36 and 2.55, found while building WP2; D231) |
| `branch.autoSetupRebase` | `git -c branch.autoSetupRebase=always checkout -b x --track origin/main` writes `branch.x.rebase true` itself: a branch can carry a rebase setting nobody chose |
| Identity | `git var GIT_AUTHOR_IDENT` prints `Name <email> <unix time> <zone>` (`Ann O'Neil <ann@example.com> 1792295653 -0700`), in or outside a repository; `GIT_COMMITTER_IDENT` alike; `-c committer.name=C` changes only the committer, `author.name` only the author; `GIT_AUTHOR_NAME` and `GIT_AUTHOR_EMAIL` beat all configuration. With no identity both print `Author identity unknown` / `*** Please tell me who you are.` / `Run` / `  git config --global user.email "you@example.com"` / `  git config --global user.name "Your Name"` / `to set your account's default identity.` / `Omit --global to set the identity only in this repository.` and last `fatal: unable to auto-detect email address (got '<user>@<COMPUTERNAME>.(none)')` (the machine's names); with `user.useConfigOnly=true` and nothing set `fatal: no email was given and auto-detection is disabled`; with an email only `fatal: no name was given and auto-detection is disabled`; with `user.name=` (empty) `fatal: empty ident name (for <ann@example.com>) not allowed` (all exit 128, both gits, 2026-10-18). An email only without `useConfigOnly`: the name comes from the Windows account (exit 0) |
| How git cleans a name | `a<b>c` → `abc` (without a word); a line break dropped (`line oneline two`); outer spaces trimmed; `"quoted"` → `quoted`; a name of punctuation only (`.,:;`, `...`): 2.36 `fatal: name consists only of disallowed characters: .,:;` (128), 2.55 accepts it. An email `<e@x.org>` → `e@x.org`; `e @x.org` is accepted |
| `git mergetool --tool-help` | Exit 0 on both. 2.36: the header `'git mergetool --tool=<tool>' may be set to one of the following:`, one tool per line after two tabs (here `vimdiff`, `vimdiff1` to `vimdiff3`), a blank line, `The following tools are valid, but not currently available:` and 29 names (`araxis`, `bc`, `bc3`, `bc4`, `codecompare`, `deltawalker`, `diffmerge`, `diffuse`, `ecmerge`, `emerge`, `examdiff`, `guiffy`, `gvimdiff`, `gvimdiff1` to `3`, `kdiff3`, `meld`, `nvimdiff`, `nvimdiff1` to `3`, `opendiff`, `p4merge`, `smerge`, `tkdiff`, `tortoisemerge`, `winmerge`, `xxdiff`), then `Some of the tools listed above only work in a windowed` / `environment. If run in a terminal-only session, they will fail.`. 2.55 adds a description after each name (`vscode           Use Visual Studio Code (requires a graphical session)`) and lists `vscode` as available here (2.36 has no `vscode`). A user-defined tool shows under `\tuser-defined:` as `\t\tmytool.cmd <the rest of the key's value>`. Availability is `type` on the tool's command; `mergetool.<tool>.path` is not consulted (kdiff3 stays "not currently available" with a path set), though `git mergetool --tool=kdiff3` uses it |
| Its duration | 2026-10-18, outside a repository, other work on the laptop: **32.4 s on 2.36, 46.2 s on 2.55**; the same day again (`GIT_CONFIG_NOSYSTEM=1`, an empty global file, outside a repository): 20.7 s on 2.36, 22.7 s on 2.55; 2026-10-17 under the default test run: 28.8 to 40.4 s (2.36), 37.0 s (2.55), once 58.6 s. So about 20 to 60 s. `git config --show-scope --show-origin -z --list`: 0.109 s (2.55, the same day); five such reads under load 0.229 s |
| An unknown tool | `git mergetool --tool=foo --no-prompt -- f.txt`: 2.36 prints `Merging:` / `f.txt`, exit 1, nothing on standard error; 2.55 adds `error: mergetool.foo.cmd not set for tool 'foo'`. A known tool that is missing: `The merge tool kdiff3 is not available as 'kdiff3.exe'` (exit 1; `_BACKUP_`, `_BASE_`, `_LOCAL_`, `_REMOTE_` files left, which D149's clean-up removes) |
| `merge.guitool` | Read by `git mergetool` only in GUI mode (`--gui`; on 2.55 also `mergetool.guiDefault`); the app always passes `--tool=<merge.tool>` (D149), so it never matters |
| The harnesses today | `GitIsolation.IsolateThisProcess` (`tests/VisualCommit.Testing/GitIsolation.cs:21-49`) and `RealApp.Launch` (`tests/VisualCommit.RealWindowTests/RealApp.cs:163-173`) give the app under test one shared global file, `%TEMP%/VisualCommit.Tests/isolation/gitconfig`, created empty once and never reset; `GitRunner` adds only its static `DefaultEnvironment` (`src/VisualCommit.Git/GitRunner.cs:41-50`). The owner's `~/.config/git/ignore` and the identity variables reach the app under test in both (0.10) |

### 0.4 Ignored files on 2.36 and 2.55

Git 2.36.0.windows.1 and 2.55.0.windows.5 (Visual Studio's `mingw64/bin` first on `PATH`), isolated as
the tests are (`GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`, `HOME` and `XDG_CONFIG_HOME` in
the lab, `LANGUAGE=en`, `LC_ALL=en_US.UTF-8`, `GIT_EDITOR=:`, `GIT_TERMINAL_PROMPT=0`, fixed dates):
research note r04's cases, this design's T1 to T9 and U1 to U4, and the reviews' U5 and long argument
lists; the same on both gits unless a row says otherwise. Shapes: **S1X** an ignored `secret.env`
("LOCAL", `!! secret.env`) that the target tracks; **S1F** the same, the target a fast-forward; **S1O** D179's shape: `base`, "Add secret" (tracks
it), "Edit secret", "Ignore secret" (`git rm --cached`, `.gitignore`), "X", "LOCAL" on disk; **S1D**
`main` stops tracking and ignores it after `base`, `feature` edits README only, `feature2` edits
`secret.env`; **S2** an ignored folder `build/` where the target has a file `build`; **S3** an ignored
file `logs` where the target has `logs/a.txt`; **S4** a file in an ignored `node_modules/` that the
target force-adds.

| What | Command (case) | Result |
|---|---|---|
| Which commands have the option | `git <cmd> -h` | checkout, switch, merge: `--[no-]overwrite-ignore  update ignored files (default)` (2.36: `--overwrite-ignore`); rebase, cherry-pick, revert, reset, stash, restore, pull: none (`git rebase --no-overwrite-ignore`: `error: unknown option 'no-overwrite-ignore'`, 129; cherry-pick prints its usage, 129). UNVERIFIED on 2.30 |
| Checkouts without it | `git switch --no-guess t`, `--create x t`, `--detach <sha>`, `--create t2 --track refs/remotes/origin/t` (S1X, S2, S3, S4) | exit 0. S1X: `secret.env` = t's bytes; S2: `build` a file, `build/out.bin` deleted; S3: the file `logs` gone; S4: `node_modules/pkg/index.js` overwritten |
| Checkouts with it | the option just before the positional argument: `git switch --create x --no-overwrite-ignore t`, `git switch --create x2 --track --no-overwrite-ignore refs/remotes/origin/t`, `git switch --no-guess --no-overwrite-ignore --end-of-options -dash` | exit 1, nothing changed, no branch created; stderr `error: The following untracked working tree files would be overwritten by checkout:` / `\tsecret.env` / `Please move or remove them before you switch branches.` / `Aborting` (S3's `logs` likewise); S2: `error: Updating the following directories would lose untracked files in them:` / `\tbuild` / an empty line / `Aborting`. Also refused when the file holds the target's exact bytes |
| Ignored or untracked | `git check-ignore --stdin -z` on S2's and S3's files | `build\0build/out.bin\0`, `logs\0logs/a.txt\0`, exit 0; a tracked (force-added) file that matches a rule: nothing, exit 1 |
| A real merge | `git merge --no-edit t`, `--no-ff`, `-s recursive`, each with and without the option (S1X, S2, S3) | exit 0 (`Merge made by the 'ort' strategy.`), the file overwritten, the folder's file deleted: **the option does nothing for a real merge** |
| A fast-forward | `git merge --ff-only --no-overwrite-ignore t`, `git merge --no-edit --no-overwrite-ignore t` (S1F) | exit 1, stdout `Updating 85d293b..61c2ae6`, stderr `error: The following untracked working tree files would be overwritten by merge:` / `\tsecret.env` / `Please move or remove them before you merge.` / `Aborting`; nothing changed. Without the option: overwritten. The pull's form (`git fetch -q . t`, then `git merge --ff-only --no-overwrite-ignore FETCH_HEAD` / `git merge --no-edit --no-overwrite-ignore FETCH_HEAD`): refused (1) / overwritten (0) |
| m15, a merge keeping HEAD's deletion; m16, modify/delete | `git merge --no-edit feature`; `git merge --no-edit feature2`, with and without the option (S1D) | m15: exit 0, README merged, "LOCAL" kept. m16: exit 1, `CONFLICT (modify/delete): secret.env deleted in HEAD and modified in feature2.  Version feature2 of secret.env left in tree.`, `DU secret.env`, the file overwritten |
| Rebase | `git rebase t`, `git rebase` (the pull's, upstream `origin/t`), `--apply t`, `-i t` (S1X); `git rebase main` with F1 adding `secret.env` and F2 deleting it | exit 0, overwritten; the second: `secret.env` **gone** |
| Interactive, leading picks unchanged (T5) | `git rebase -i <Add secret>`, todo `pick <Edit>`, `pick <Ignore>`, `exec true`, `pick <X>` (S1O) | exit 0, "LOCAL" kept. HEAD's reflog `rebase (start): checkout 8e0d71e…` (the onto given) has the new value `4cf1176` (Ignore secret): git skipped the two leading picks and checked out the last |
| Interactive, a label first (T6); reordered (T7) | T6: `label vc-1`, `pick <Edit>`, `pick <Ignore>`, `pick <X>` (the app's shape for a reworded group); T7: `pick <X>`, `pick <Edit>`, `pick <Ignore>` | exit 0, `secret.env` **gone** in both; T6: the checkout of Add secret overwrote it, Ignore secret's pick (a fast-forward, `rebase: fast-forward`) deleted it |
| Cherry-pick, revert | `git cherry-pick t` (S1X, S2, S3); `git revert --no-edit HEAD` re-adding a deleted file; reverting "Ignore secret" | exit 0: overwritten, deleted, replaced; the reverts overwrite |
| Modify/delete of a pick (T9) and a revert (T4) | onto X (S1O): `git cherry-pick <Edit secret>`; `git revert --no-edit <Edit secret>` | exit 1. T9: `CONFLICT (modify/delete): secret.env deleted in HEAD and modified in ef5f8aa (Edit secret).  Version ef5f8aa (Edit secret) of secret.env left in tree.`, `secret.env` = "old2". T4: `… modified in parent of ef5f8aa (Edit secret).  Version parent of ef5f8aa (Edit secret) of secret.env left in tree.`, `secret.env` = "old" |
| Reset | `git reset --hard t`, `--merge`, `--keep` (S1X, S2, S3) | exit 0, overwritten or deleted (the app's hard reset saves them first, D127) |
| Stash | `git stash apply stash@{0}` (also `pop`, `pop --index` for ST1): ST1 a staged new file, ST2 its untracked part, ST3 modify/delete | ST1: exit 0, overwritten. ST2: exit 1, `secret.env already exists, no checkout`, "LOCAL" kept, the tracked part applied anyway. ST3: 2.36 exit 1, `… left in tree at secret.env~Stashed changes.`, "LOCAL" kept; **2.55** exit 1, `… left in tree.`, overwritten |
| read-tree, restore | `git read-tree -m -u HEAD t`, `git restore --source=t --worktree -- secret.env` | exit 0, overwritten; `read-tree -n` reports nothing; `--no-overwrite-ignore` exits 129 |
| The app's reads | `git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD t --`; `git merge-base --all HEAD t`; `git diff-tree -r -z --no-renames --name-only --diff-filter=AMT <base> t --` | `secret.env\0`; one base; `secret.env\0`. S1D: HEAD→feature lists `secret.env` too, base→feature only `README`: the base tells m15 from m16 |
| A merge of an ancestor (U4) | the `--diff-filter=A` read to `<Add secret>`, the base read; `git merge --no-edit <Add secret>` | `secret.env\0`; the base is Add secret, base→source lists nothing; git `Already up to date.`, "LOCAL" kept |
| A rebase onto an ancestor (U2) | `git rebase` on `main` whose upstream is Add secret (S1O, linear) | `Current branch main is up to date.`, exit 0, "LOCAL" kept; `git merge-base --is-ancestor up HEAD` exit 0 |
| An ahead-only branch with a merge (U5) | U0 tracks `secret.env`, U1 follows it (the upstream); `feat` from U0 adds `f.txt`; `main` is U1 and "Stop tracking secret" (`git rm --cached`, `.gitignore`); `git switch feat`, `git merge main`, `git switch main`, `git merge --ff-only feat`; `secret.env` "LOCAL", `!! secret.env`. Then `git merge-base --is-ancestor @{u} HEAD`, 9.6's read (3) with `--diff-filter=AMT`, `git rev-list --min-parents=2 @{u}..HEAD`, `git rebase`, and on a fresh copy `git rebase --rebase-merges` | is-ancestor exit 0; (3) lists the two single-parent commits only: `--diff-filter` leaves out a merge, which has no path without `-m` (without the filter it is listed); rev-list prints the merge. `git rebase`: `Successfully rebased and updated refs/heads/<b>.`, the branch made linear, `secret.env` **gone**; `--rebase-merges`: HEAD the same commit as before, `secret.env` **gone** too: git checks out `<onto>` and replays whatever the result |
| A side branch from a commit outside the range (found while building WP1) | U0 tracks `secret.env`; `feat` from U0 adds `f.txt`; `main` stops tracking and ignores it (U1), adds `local.txt` and merges `feat`; `<onto>` U2 follows U1 with `u2.txt`; `secret.env` "LOCAL". `git rebase --rebase-merges U2` on `main`; a plain `git rebase U2` | `--rebase-merges`: the todo `label onto` / `reset <U0>` / `pick F` / … / `merge -C …`: git resets to the side branch's original branch point, which writes `secret.env` over the ignored file, and the next `reset onto` deletes it: exit 0, `secret.env` **gone** (2.36 and 2.55). Neither `<onto>` nor the tip has the file and no commit of the range writes it, so 9.6's reads (1) to (4) do not see it. The plain rebase replays F's change only and keeps it. `rebase.rebaseMerges` (Git 2.42 and later) makes a plain `git rebase` keep merges too; 2.36 ignores the key |
| `diff-tree --stdin` | `git diff-tree --stdin [--no-commit-id] [-R] [--root] -r -z --no-renames --name-only --diff-filter=…` | a line `<c>` diffs c against its first parent (a merge prints nothing without `-m`; a root commit nothing without `--root`, `README\0` with it); `<c> <p>` diffs p→c; `-R` reverses; without `--no-commit-id` each output starts `<c>\0`. **Names are not resolved**: `HEAD`, `main~3`, `zzz`, `<sha>^1 HEAD` come back unchanged, exit 0: full SHAs only |
| A range's picks; parents | `git log --no-renames --name-only -z --root --no-merges --no-show-signature --format=%x1e%H --diff-filter=AMT <onto>..<tip> --`; `git rev-list --stdin --no-walk=unsorted --parents` fed `<X>` and `<Edit>` on standard input, one per line (the arguments form `git rev-list --no-walk=unsorted --parents <X> <Edit>` gives the same) | `\x1e<sha>\0\nx.txt\0\x1e<sha>\0\n.gitignore\0…` (a deletion filtered out); `<X> <its parent>` / `<Edit> <its parent>`, in the order given |
| Long argument lists | `git rev-list --no-walk=unsorted --parents` with N SHAs as arguments, started by .NET's `Process.Start` (2.36, `C:\Program Files\Git\cmd\git.exe`, a lab repository of 1,001 commits); the same 999 SHAs in reverse order on standard input (`--stdin`) | 700 SHAs (28,772 characters) and 790 (32,462): exit 0; 800 (32,872) and 999 (41,031) do not start: `System.ComponentModel.Win32Exception: The filename or extension is too long` (Windows' 32,767-character command line), which `GitRunner` turns into a `GitException` (`GitRunner.cs:95-101`). On standard input: exit 0, 999 lines in the order given, on both gits |
| A file named HEAD | `git diff-tree … --diff-filter=A HEAD main~3` with files `HEAD` and `main` in the working tree | `fatal: ambiguous argument 'HEAD': both revision and filename`, 128; with `--` at the end `secret.env\0`, 0. `git log … main~3..HEAD` and `merge-base --is-ancestor` are not affected |
| `merge-tree --write-tree` | `git merge-tree --write-tree --no-messages HEAD t` | 2.55: a tree, exit 0 (exact); 2.36: no such option (from Git 2.38, above the app's minimum 2.30) |

Timings (r04, three runs each under a full test run's load; indications): 20,001 ignored files under
`node_modules/`, a target adding 501 paths: `diff-tree --diff-filter=A HEAD t` 155 to 169 ms (2.36),
256 to 276 ms (2.55); `check-ignore --stdin -z` on one path 216 and 316 ms; a refused `switch
--no-overwrite-ignore` 321 to 542 ms; listing every ignored file (`ls-files --others --ignored
--exclude-standard`) 340 to 621 ms, growing with them. A process start is most of each; the tree
diffs and `stat` calls never touch the ignored folders.

Not run: Git 2.30 (before 2.34 unpack-trees treated ignored files as untracked where no exclude list
was set, so read-tree, `reset --merge` and possibly the sequencer refuse there instead; inferred),
`rebase --autostash`, Continue and Skip whose later picks add files, the aborts, the `resolve` and
octopus strategies, criss-cross merges, `core.excludesFile`, case-only collisions on Windows,
symbolic links, sparse checkouts, submodules, CI's 2.56 and 2.43.

### 0.5 Fetch refspecs on 2.36 and 2.55

Research note r05 (a bare server with `feature/x`, `main`, `secret`, `team/a`, `team/b`, `trunk`, `up`
and `refs/pull/1/head`; each case a fresh repository whose `origin` has the fetch lines named;
`LC_ALL=C`), this design's probe (no server, tracking refs made with `update-ref`), the reviews' lab
(the rows that say 2.36) and WP3's builders and reviewers (what says WP3: 2.36, replayed on 2.55),
isolated as in 0.4; the same on both gits unless said. `--track X` stands for `git switch --create <b> --track X`.

| Case | Configuration | What git does |
|---|---|---|
| The read | `git config --null --get-regexp '^remote\..*\.fetch$'` | `remote.<name>.fetch\n<value>\0` per line in git's reading order (global before local; a second `[remote "origin"]` section later); a name keeps its case and dots (`remote.Team.Dot.x.fetch`); exit 1, no output, when none. Without `--local`: `git fetch` uses global lines too |
| Names and lines in one read (2.36) | `git config --null --get-regexp '^remote\.'` beside `git remote`, in two repositories: remotes `Team.Dot.x`, `onlyprune` (only `remote.onlyprune.prune`) and `origin`, a `remote.pushDefault`, a legacy `.git/remotes/legacyfile` and `.git/branches/legacybranch`; remotes with only a `fetch`, a `url`, a `pushurl` or a `mirror` key | `git remote` lists exactly the subsections of the `remote.<name>.<var>` keys (`Team.Dot.x`, `onlyprune`, `origin`; `onlyfetch`, `withurl`, `onlypushurl`, `mirrorish`), a dotted name whole; `remote.pushdefault` comes in lower case with no subsection; neither lists the legacy files, although `git remote get-url legacyfile` resolves |
| A default | `+refs/heads/*:refs/remotes/origin/*` | `--track refs/remotes/origin/feature/x` → `%(upstream:remoteref)` `refs/heads/feature/x` |
| B1 renaming | `+refs/heads/*:refs/remotes/origin/up/*` | the fetch stores `feature/x -> origin/up/feature/x`; `git switch --create main2 --track refs/remotes/origin/up/main` → `branch 'main2' set up to track 'origin/main'.`, merge `refs/heads/main`; `git checkout feature/x` creates `feature/x` tracking `origin/up/feature/x`; `git checkout up/feature/x` → `error: pathspec … did not match`, 1 |
| B1, today's deletion | `git push --progress --force-with-lease=refs/heads/up/feature/x:<sha> origin --delete refs/heads/up/feature/x` | ` ! [rejected]        (delete) -> up/feature/x (stale info)`, 1. Mapped (`… refs/heads/feature/x:<sha> origin --delete refs/heads/feature/x`): ` - [deleted]         feature/x`, 0, and `refs/remotes/origin/up/feature/x` deleted with it |
| B3, today's drop push | `+refs/heads/*:refs/remotes/origin/u/*`; `git push --progress origin refs/heads/topic:refs/heads/u/main` | ` * [new branch]      topic -> u/main`, 0: **a new branch `u/main` on the server**, and a local `refs/remotes/origin/u/u/main` |
| B2 another namespace | `+refs/heads/*:refs/remotes/mirror/*` on `origin` | `git remote` lists `origin` only; today's split deletes on `mirror`: `fatal: 'mirror' does not appear to be a git repository`, 128; `--track refs/remotes/mirror/main` tracks `origin/main`; `git remote remove origin` deletes `refs/remotes/mirror/*` and keeps a stray `refs/remotes/origin/stray` |
| C1 one ref | `refs/heads/main:refs/remotes/origin/trunk` | `--track refs/remotes/origin/trunk` → tracks `origin/main`, merge `refs/heads/main` |
| C2 two lines, one remote | the default and `refs/heads/main:refs/remotes/origin/trunk` | with the server's `trunk`: `fatal: Cannot fetch both refs/heads/trunk and refs/heads/main to refs/remotes/origin/trunk`, 128; without it, `--track refs/remotes/origin/trunk`: the first line in configuration order wins (`refs/heads/trunk`; swapped, `refs/heads/main`); `git fetch` fills both `origin/main` and `origin/trunk` from `main`, while `git push origin main` updates only the first line's tracking ref, `origin/main` (git's `remote_find_tracking`; WP3) |
| P GitHub's pull requests | the default, then `+refs/pull/*/head:refs/remotes/origin/pr/*` | `--track refs/remotes/origin/pr/1` → merge `refs/heads/pr/1` (the first line); `git pull` → `… no such ref was fetched`, 1. `git push --progress origin refs/heads/pr/1:refs/heads/pr/1` (today's drop) → ` * [new branch]      pr/1 -> pr/1`, and from then on **every fetch of origin fails**: `fatal: Cannot fetch both refs/heads/pr/1 and refs/pull/1/head to refs/remotes/origin/pr/1`, 128 |
| P3 no upstream (2.36) | P's lines: `git switch --create pr/1 --no-track --no-overwrite-ignore refs/remotes/origin/pr/1` | exit 0, `pr/1` at the pull request's commit, no `branch.pr/1.*` key |
| An implicit upstream (2.36) | `branch.autoSetupMerge` unset; under P `git switch --create review1 refs/remotes/origin/pr/1` and `git branch review2 refs/remotes/origin/pr/1`; under H the same from `refs/remotes/origin/main` | P: exit 0, each with `remote = origin`, `merge = refs/heads/pr/1` (the first line), as `--track` gives; then `git push --progress origin refs/heads/review1:refs/heads/pr/1` creates `pr/1` on the server and the next fetch fails as in P. H: `fatal: not tracking: ambiguous information …`, 128, the branch left behind, for both |
| P2 not a branch | the pull-request line only | `--track refs/remotes/origin/pr/1` → `branch 'pr/1' set up to track 'origin/refs/pull/1/head'.`, merge `refs/pull/1/head`; `git pull --ff-only` works |
| D a prefix | `+refs/heads/team/*:refs/remotes/origin/*` | `team/a -> origin/a`; `--track refs/remotes/origin/a` → merge `refs/heads/team/a`; `git checkout team/a` creates `team/a` tracking `origin/a` |
| E negative | the default, then `^refs/heads/secret` | `origin/secret` stays where it was; `--track refs/remotes/origin/secret` → `fatal: cannot set up tracking information; starting point 'refs/remotes/origin/secret' is not a branch`, 128; a push naming `refs/heads/secret` re-creates the excluded tracking ref |
| X an excluded exact line (WP3) | `refs/heads/secret:refs/remotes/origin/s`, then `^refs/heads/secret` | the fetch never writes `origin/s`; `git branch --track probe refs/remotes/origin/s` still tracks `refs/heads/secret`: git's tracking setup does not apply a negative line to an exact line's source, while its fetch does |
| N an excluded candidate | the default, `refs/heads/main:refs/remotes/origin/trunk`, `^refs/heads/trunk` | the fetch stores `main -> origin/trunk`, but `--track refs/remotes/origin/trunk` → `… is not a branch`, 128, in either line order: a ref with any excluded candidate maps to nothing |
| Line forms | each alone, or beside the default | `^refs/heads/team/*` excludes `team/a` and `team/b`; `^secret` excludes nothing (full names only); `^refs/heads/secret:refs/remotes/origin/secret` and `+^refs/heads/secret` → `fatal: invalid refspec '…'`, 128; `^+refs/heads/secret` is accepted and excludes nothing; `refs/heads/*:refs/remotes/origin/x`, `refs/heads/x:refs/remotes/origin/*`, `refs/heads/*/*:refs/remotes/origin/*/*` are invalid; `refs/heads/*/x:refs/remotes/origin/*/y` maps `feature/x` to `origin/feature/y`; `refs/heads/f*:refs/remotes/origin/g*` maps it to `origin/geature/x` (`*` matches any text, slashes included, inside a component too) and `f` to `origin/g` (the text may be empty; WP3); `refs/heads/main` (no destination) and an empty value fetch into `FETCH_HEAD` only, while a pattern line with no destination (`refs/heads/*`) or an empty one (`refs/heads/*:`) is invalid; `@` alone as a source means `HEAD`; a negative line naming a 40-character commit id is invalid (WP3). **An invalid line on any remote breaks every remote**: `fatal: invalid refspec '…'`, 128, for `--track` and `git push origin …` alike, and for `git for-each-ref`, which reads the upstreams, so the app's refs read fails with it (WP3, 10.1) |
| O2 a short source | `main:refs/remotes/origin/m` | `--track refs/remotes/origin/m` → `branch.mm.merge = main` (not a full name), `%(upstream:remoteref)` `main` |
| An empty source | `:refs/remotes/origin/x` | `git branch --track probe refs/remotes/origin/x` → 0, `branch 'probe' set up to track 'origin/'.`, `branch.probe.merge` empty |
| G no line | none (`git config --get-all remote.origin.fetch` 1); a tracking ref made by hand | `git fetch origin` → `* branch HEAD -> FETCH_HEAD`; `--track refs/remotes/origin/main` and `--set-upstream-to` → `… is not a branch`, 128, no branch left; a branch with `remote = origin`, `merge = refs/heads/main` shows all four upstream atoms empty |
| H two remotes | `origin` and `backup` both `+refs/heads/*:refs/remotes/origin/*` | `--track refs/remotes/origin/main` → `fatal: not tracking: ambiguous information for ref 'refs/remotes/origin/main'`, hints naming both, 128, **the new branch left behind**; `git checkout main` → `fatal: 'main' matched multiple (2) remote tracking branches` |
| T nested names | remotes `origin`, `team`, `team/fork`, default lines (`RefReadingTests`' shape) | `--track refs/remotes/team/fork/main` → the same fatal (`team` maps it to `refs/heads/fork/main`, `team/fork` to `refs/heads/main`), 128, branch left behind |
| F mirrors | `git remote add --mirror=fetch` writes `fetch = +refs/*:refs/*`; `--mirror=push` writes `mirror = true` | the fetch writes `refs/heads/*` directly, nothing under `refs/remotes/`; with `mirror = true` **every push that names a refspec fails**: `fatal: --mirror can't be combined with refspecs`, 128 (Push, deletions, tags, undo's `RemoteRef`). The fetch line beside origin's default (WP3): on another remote, `--track refs/remotes/origin/main` → `fatal: not tracking: ambiguous information …`, 128, as H (that remote's line also maps every `refs/remotes/origin/*`, from the ref of the same name on it); on origin itself the first line in configuration order wins, as C2; alone, `refs/remotes/origin/main` maps to itself, which is not a branch |
| L legacy | `.git/remotes/legacy` with `Pull: +refs/heads/*:refs/remotes/legacy/*` | `git remote` lists nothing, no config line, `git fetch legacy` works (2.55 warns it is deprecated), `--track refs/remotes/legacy/main` → `… is not a branch` |
| Plumbing | `for-each-ref --format='%(upstream)\|%(upstream:remoteref)\|%(push)' refs/remotes/origin/up/main`; `rev-parse --symbolic-full-name refs/remotes/origin/up/main@{upstream}`; `git remote show -n origin` | empty atoms; `fatal: no such branch`, 128; a translated human list that does not pair a tracking ref with its name. **Git has no read of the reverse mapping**; `--track` and `--set-upstream-to` compute it but write configuration |
| The parity probe | `git branch --track probe <T>` for B1, H, C2 (wildcard line first), N, P2, G, T | B1: 0, `remote=origin merge=refs/heads/main`; H and T: 128, `not tracking: ambiguous information`, `probe` left behind; C2: 0, `merge=refs/heads/trunk`; N and G: 128, `is not a branch`, nothing left; P2: 0, `merge=refs/pull/1/head`. As r05's `switch --create … --track` on both gits |

### 0.6 Avalonia 12.1.3 and the system's theme

No git involved. Read by note r06 in the IL of the 12.1.3 assemblies in the NuGet cache
(`lib/net10.0`, `ref/net10.0`; .NET Framework 4.8 `ildasm`); read again on 2026-10-18: the strings
`ImmersiveColorSet` and `WindowsThemeElement` in `Avalonia.Win32`, the headless binding of
`DefaultPlatformSettings`, `DBusPlatformSettings.ToColorScheme`, Fluent's tooltip keys and colours.
"This machine" was run there, read-only.

| What | Result |
|---|---|
| The app today | `AppTheme { Dark, Light }` (`src/VisualCommit.Core/Settings/AppSettings.cs:4-8`); `ThemeService.Apply` sets `Application.RequestedThemeVariant` to `Dark` or `Light` (`Services/ThemeService.cs:23-27`); `App.axaml:5` starts at `Dark`; `AppSession.Start` applies the saved theme before the window exists (`AppSession.cs:118-120`). Custom-drawn controls read their tokens for their `ActualThemeVariant` and redraw on `ActualThemeVariantChanged`: the graph (`Controls/CommitGraphControl.cs:147-148`, `:759`), the diff (`Controls/Diff/DiffTextView.cs:112`, `:557`), the blame text (`Controls/Blame/BlameTextEditor.cs:67`, `:191`), the conflict panes (`Controls/Conflicts/ConflictPanes.cs:210`, `:707`) and the result (`Controls/Conflicts/ConflictResultEditor.cs:118`, `:380`). **Except `DragLayer`**: it reads its brushes at each draw but redraws only when the drag changes (`Views/Shell/DragLayer.cs:71`, `:106-108`), so a theme change mid-drag keeps the old colours until the pointer moves. Every `.axaml` uses `{DynamicResource Vc…}` (D29) |
| `ThemeVariant.Default` on the application | Doc: "If set on Application, system theme is inherited": `ThemeVariant.UpdateActualThemeVariant` gives the application `GetPlatformThemeVariant()` = `IPlatformSettings.GetColorValues().ThemeVariant`; without that service, Light (`PlatformThemeVariant { Light = 0, Dark = 1 }`). With `Dark` or `Light` requested, the application's `ActualThemeVariant` is that variant whatever the platform says. `Application.RegisterServices` subscribes to `PlatformSettings.ColorValuesChanged` before `App.axaml` loads; with an explicit request its handler only sets the same variant again |
| Windows | `Win32PlatformSettings`: with high contrast on, Light when the scheme's name contains "White", else Dark; otherwise UISettings' background colour (Dark when `R+G+B < 765 - (R+G+B)`), which Settings > Personalization > Colors > "Choose your app mode" sets. The window procedure re-reads it on `WM_SETTINGCHANGE` with `ImmersiveColorSet` or `WindowsThemeElement` and raises `ColorValuesChanged` only when the values differ. On build 22000 and later `SetFrameThemeVariant` sets `DWMWA_USE_IMMERSIVE_DARK_MODE` (20) from the window's variant, so the title bar follows the app (phase 0's report, check 5) |
| This machine (2026-10-18, read-only; Windows 11 Pro 10.0.26300) | `reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" /v AppsUseLightTheme`: `AppsUseLightTheme REG_DWORD 0x1` (exit 0). `reg query "HKCU\Control Panel\Accessibility\HighContrast" /v Flags`: `Flags REG_SZ 126` (exit 0), bit 0 (`HCF_HIGHCONTRASTON`) clear. UISettings' background through WinRT (r06): `#FFFFFF`. So System means **Light** here today |
| macOS | `NativePlatformSettings` registers a native callback (`RegisterColorsChange`) that re-reads `PlatformTheme` and raises the event when the values differ. What `libAvaloniaNative.dylib` watches: **not checked**; CI's macOS job is headless and cannot show it |
| Linux | `Avalonia.X11` binds `DBusPlatformSettings`. It starts at Light; with a D-Bus session it reads the portal `org.freedesktop.portal.Settings`, namespace `org.freedesktop.appearance`, key `color-scheme`: `ToColorScheme(uint)` gives Dark for `1` and Light for anything else (0 no preference, 2 prefer light). The constructor starts the first read without waiting for it, then watches `SettingChanged`, so the first frame can be light and turn dark a moment later. Without a D-Bus session or a portal: Light, never raised |
| Headless | `AvaloniaHeadlessPlatform` binds a `DefaultPlatformSettings` singleton: always Light, never raised; `AvaloniaHeadlessPlatformOptions` has no option for it |
| Faking the platform's settings | Not possible: `IPlatformSettings` is `[NotClientImplementable]` with an internal abstract member in the reference assembly; `AvaloniaLocator.Current`, `AvaloniaLocator.CurrentMutable` and `DefaultPlatformSettings.OnColorValuesChanged` are internal there. Reflection on the `lib` assembly would work, but is private API any minor version may change |
| What the app can build on | `Application.PlatformSettings` (public, `IPlatformSettings?`). `GetColorValues()` returns `PlatformColorValues` (`ThemeVariant`, `ContrastPreference`, `AccentColor1` to `3`; a public constructor and init setters). `ColorValuesChanged`, an `EventHandler<PlatformColorValues>`, is raised on the UI thread through `Dispatcher.UIThread.Send`, also for a change of accent colour alone |
| Fluent's tooltip | `ToolTip`'s control theme takes `{DynamicResource ToolTipBackground}`, `ToolTipForeground` and `ToolTipBorderBrush`, and `ToolTipBorderThemeThickness` (1), `ToolTipBorderThemePadding` (8,5,8,7), `ToolTipContentThemeFontSize` (12), `ToolTipContentMaxWidth`, `OverlayCornerRadius`. In both variants the three brushes are `SystemControlBackgroundChromeMediumLowBrush`, `SystemControlForegroundBaseHighBrush` and `SystemControlTransientBorderBrush`, at Fluent's defaults (the app sets only Fluent's accent, `App.axaml:21-22`): **dark** background `#2B2B2B`, text `#FFFFFF`, border `#000000` at opacity 0.36; **light** `#F2F2F2`, `#000000`, `#000000` at 0.14. A key of the same name in the application's resources wins, as the `TextControl…` keys of `Theme/Tokens.axaml:60-71` and `:124-135` show |
| Today's pictures with a tooltip | Scanned (`p6-lab/sec-11/scan_tooltips.py`): the local pictures of the baseline run on `acdac12` (status step 2) and phase 5's gate's real-window pictures, for 300 or more pixels of exactly `#2B2B2B`. 56 match, 48 scripted and 8 real-window, all of phases 3 to 5 (16.1 names them). None matches Fluent's light `#F2F2F2`: no gate picture shows a light tooltip |

### 0.7 Avalonia's keys and menu items

No git involved. Read on 2026-10-17 and 2026-10-18 from Avalonia 12.1.3 and AvaloniaEdit 12.0.0 in the
NuGet cache (`%USERPROFILE%\.nuget\packages`, `<package>/12.1.3/lib/net10.0`,
`avalonia.avaloniaedit/12.0.0`) with `ildasm.exe` 4.8.3928.0
(`C:/Program Files (x86)/Microsoft SDKs/Windows/v10.0A/bin/NETFX 4.8 Tools/x64/`,
`ildasm -text -item="<type>" <assembly>`) and a reader of the user-string heap. Nothing was run on a
Mac or a Linux desktop: what Avalonia does there is read from its IL.

| What | Read in | Result |
|---|---|---|
| The platforms' hotkeys | `Avalonia.Input.Platform.PlatformHotkeyConfiguration` (`Avalonia.Base.dll`); `Avalonia.Win32.dll`, `.X11.dll`, `.Headless.dll`, `.Native.dll` whole | `PlatformHotkeyConfiguration(commandModifiers, selectionModifiers = Shift, wholeWordTextActionModifiers = Control)`, `cmd` the command modifier: Copy cmd+C, Ctrl+Insert; Cut cmd+X; Paste cmd+V, Shift+Insert; Undo cmd+Z; Redo cmd+Y, cmd+Shift+Z; SelectAll cmd+A; line moves Home, End, document moves cmd+Home, cmd+End (each also with Shift); OpenContextMenu Apps; Back Alt+Left; PageLeft, PageRight, PageUp, PageDown are Shift+PageUp, Shift+PageDown, PageUp, PageDown. **Win32** `(Control, Shift, Control)`, Shift+F10 added to OpenContextMenu; **X11** `(Control, Shift, Control)`; **headless** the default (Control), so CI's headless macOS job sees Ctrl; **macOS** (Avalonia.Native) `(Meta, Shift, Alt)`, cmd+Left, cmd+Right (with Shift) added to the line moves. macOS's redo is Cmd+Y **and** Cmd+Shift+Z (architecture.md says Cmd+Shift+Z only) |
| Matching | `Avalonia.Input.KeyGesture` | `Matches(e)`: `e.KeyModifiers == gesture.KeyModifiers` exactly; the keys after mapping the number pad's `+`, `-`, `.` to the main keys; number-pad digits not mapped |
| A gesture's text | `KeyGesture`, `Avalonia.Input.Platform.KeyGestureFormatInfo` | `ToString("p")`: modifiers in the order Ctrl, Shift, Alt, Meta, then the key, joined by `+`, in the platform's `KeyGestureFormatInfo`: Win32 `Win`, `Ctrl`, `Alt`, `Shift`; X11 `Super`, …; headless `Cmd`, …; macOS `⌘ ⌃ ⌥ ⇧` and the key symbols `⌫ ↓ ↘ ⎋ ↖ ← ↩ ⇟ ⇞ → ␣ ⇥ ↑`. Cmd+Shift+P is drawn **`⇧+⌘+P`** on macOS, neither D191's "Cmd+Shift+P" nor Apple's `⇧⌘P`. The invariant `ToString()` uses `Key.ToString()` (`OemComma`, `D1`) |
| `Key` names | `Avalonia.Input.Key` | `Return = Enter = 6`, `PageUp = Prior = 19`, `PageDown = Next = 20` (duplicates); `Tab = 3`; `OemComma = 0x8E` |
| The menu item's key | `Avalonia.Controls.xml`; `Avalonia.Controls.Converters.PlatformKeyGestureConverter`; `Avalonia.Themes.Fluent.dll`'s user strings | `MenuItem.InputGesture` only draws ("Setting this property does not cause the input gesture to be handled"); `MenuItem.HotKey` registers a real gesture. Fluent draws `InputGesture` in `TextBlock#PART_InputGestureText` (`SharedSizeGroup="MenuItemIGT"`, margin `MenuInputGestureTextMargin`, colour `MenuFlyoutItemKeyboardAcceleratorTextForeground` and its `PointerOver`, `Pressed`, `Disabled` variants) through `KeyGestureConverter`, which returns `ToString("p", null)`. The app hides it (`Theme/Menus.axaml:58-60`) |
| UI Automation's accelerator key | `ControlAutomationPeer::GetAcceleratorKeyCore`, `MenuItemAutomationPeer`'s (`Avalonia.Controls.dll`); `Avalonia.Win32.Automation.dll` (`avalonia.win32/12.1.3`) | `ControlAutomationPeer` returns `AutomationProperties.GetAcceleratorKey(Owner)`; `MenuItemAutomationPeer` that or, when blank, `InputGesture?.ToString()` (invariant). The Win32 provider's `AutomationNode.GetPropertyValue` returns `Peer.GetAcceleratorKey()` for 30006 (`UIA_AcceleratorKeyPropertyId`) and reads `GetItemStatus()` (30026) and `GetHelpText()`; `AutomationProperties.ItemStatusProperty` exists. Not checked at runtime |
| Tab with modifiers | `Avalonia.Input.KeyboardNavigationHandler::OnKeyDown`, `::SetOwner` | `SetOwner` adds `OnKeyDown` to the top level's `KeyDownEvent` (Direct and Bubble, `handledEventsToo: false`) when the top level is made, before any handler of `MainWindow`. Tab with **any** modifiers moves the focus (Shift: backwards), `Handled` = whether it moved. Ctrl+Tab marked handled on the tunnelling pass stops it |
| What a `TextBox` takes | `Avalonia.Controls.TextBox::OnKeyDown` | The platform's SelectAll, Copy, Cut, Paste, Undo, Redo (Undo and Redo handled whenever `IsUndoEnabled`); the line and document moves; the arrows (with Shift and the word modifier); Back and Delete (with the word modifier); **Enter whenever `AcceptsReturn`, whatever the modifiers** (it tests only `AcceptsReturn` and `IsReadOnly`, then types `NewLine`); Tab when `AcceptsTab`; Space; nothing during an input method's preedit. Other Ctrl or Cmd letters and every Mod+Shift+letter go on unhandled |
| What AvaloniaEdit's `TextArea` takes | `ildasm -text AvaloniaEdit.dll` | Caret moves (arrows, Home, End, PageUp, PageDown with none, Shift or the word modifier; Alt+Shift box selection); `ApplicationCommands` Copy, Cut, Paste, SelectAll, Undo, Redo, Find, Replace on its `PlatformCommandKey`, picked **by an OS test** (`RuntimeInformation.IsOSPlatform(OSX)` → Meta, else Control; plan risk 2); Delete; Insert (overstrike); **Ctrl+D (DeleteLine), Ctrl+I (IndentSelection) with a hard-coded Control**; Tab, Shift+Tab; Enter; Backspace; the search panel's F3, Shift+F3, Esc, Alt+R, Alt+A. The app removes the search panel (`Controls/Diff/CodeTextEditor.cs:72`) and the result's undo bindings (`Controls/Conflicts/ConflictResultEditor.cs:101`, `:247-249`); `ResultTextArea.OnKeyDown` (`:423-432`) leaves Tab with Control to `KeyboardNavigationHandler`, which moves the focus out (phase 4 design 11.10) |

What this decides: the app writes key texts itself (6.4), since Avalonia's macOS text (`⇧+⌘+P`), its
invariant text (`OemComma`) and UI Automation's text from `InputGesture` all differ from D191's words,
and `MenuModel` stays free of Avalonia types (`MenuModel.cs:6`); menus draw keys with the app's own
template (6.7), `PART_InputGestureText` hidden, `InputGesture` and `HotKey` unset; the window takes its
keys on the tunnelling pass (6.2), as phase 5's Mod+F and phase 3's Shift+F10, and must take Ctrl+Tab
there or `KeyboardNavigationHandler` moves the focus first; Mod+Enter in the commit description
(`AcceptsReturn="True"`) would type a line break, so it is taken on the way down (6.10); no key of 6.1
is a `TextBox` or `TextArea` gesture but Mod+F (taken since phase 5, the editors' search panel removed)
and undo and redo (D163's bubbling rules).

Not checked: a real Mac or Linux desktop; whether Avalonia.Native's default application menu takes
Cmd+Q, Cmd+H or Cmd+W before the window (believed: Quit, Hide, Hide others, Show all, not Close);
whether IBus or Fcitx take Ctrl+Shift+U or Ctrl+Shift+E in an Avalonia text box; the virtual keys of a
non-US layout's number row and comma (AZERTY's unshifted `&é"'(` row believed to report `D1` to `D5`);
the runtime order of the window's own `KeyDown` handlers; which duplicate `Key` name `Enum.ToString`
prints.

### 0.8 Workspaces, folders and tabs

Run on 2026-10-18 with Git 2.36.0.windows.1 and 2.55.0.windows.5 in throw-away folders (`<lab>`),
isolated as the tests are, with `HOME`, `XDG_CONFIG_HOME` and `GIT_CEILING_DIRECTORIES` inside
`<lab>`. File-system timings: Windows PowerShell 5.1 (.NET Framework 4.8), not .NET 10, on the shared
laptop, indications only. Text widths: Inter Regular's advance widths at 12.5 (the window's size,
`MainWindow.axaml:14`), read with fontTools from `Avalonia.Fonts.Inter` 12.1.3 without kerning,
within a pixel or two of what Avalonia draws.

| What | Command, output, exit code | Git |
|---|---|---|
| A repository's top folder | `git -C <lab>/t1/projects/api rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir` (the app's opening call, `GitRepository.cs:87`) → `true`, `<lab>/t1/projects/api`, `<lab>/t1/projects/api/.git`, `.git`, exit 0. `git rev-parse --show-prefix` there → an empty line, exit 0 | 2.36, 2.55 |
| Through an NTFS junction (`link-to-projects` → `projects`, attributes `Directory, ReparsePoint`) | `git -C <lab>\t1\link-to-projects\api rev-parse --show-toplevel --git-common-dir` → `<lab>/t1/projects/api` (the physical path), `.git`, exit 0; `--show-prefix` → empty, exit 0 | 2.36, 2.55 |
| Through an 8.3 short name (`A-RATH~1` for `A-Rather-Long-Folder-Name`; drive C: makes them, and CI's Windows temp folder is `C:\Users\RUNNER~1\…`, `TempRepoTests.cs:52-53`) | `git -C <lab>\t1\A-RATH~1\repo rev-parse --show-toplevel` → `<lab>/t1/A-Rather-Long-Folder-Name/repo`, exit 0, also with a trailing `\`; `--show-prefix` → empty | 2.36, 2.55 |
| The same folder in capitals | `git -C <LAB>\T1\PROJECTS\API rev-parse --show-toplevel` → `<lab>/t1/projects/api`, in the file system's own case; `--show-prefix` → empty | 2.36, 2.55 |
| A folder inside another repository whose own `.git` was removed (`outer/inner2`) | the opening call → `true`, `<lab>/t1/outer`, `<lab>/t1/outer/.git`, `../.git`, **exit 0**; `--show-prefix` → `inner2/`. `git status` there reads the outer repository, exit 0 | 2.36, 2.55 |
| A folder with an **empty** `.git` folder inside another repository (`outer/emptygit`) | the same: git passes over the empty `.git` and opens `outer`, exit 0; `--show-prefix` → `emptygit/`. A `.git` entry of its own does not prove a folder is a repository | 2.36, 2.55 |
| A `.git` file naming a folder that does not exist (`gitdir: ./nowhere`) | exit 128; 2.36 `fatal: not a git repository: <lab>/t1/outer/badfile/./nowhere`, 2.55 `fatal: not a git repository: (NULL)`. Both contain `not a git repository`, which `OpenAsync` turns into `NotARepositoryException` (`GitRepository.cs:99-102`) | 2.36, 2.55 |
| A bare repository | `git -C bare.git rev-parse --show-prefix` → an empty line, exit 0. `OpenAsync` refuses a bare repository first (first line `false`, `GitRepository.cs:90-93`), so the prefix is asked only after a successful open | 2.36, 2.55 |
| A junction and a OneDrive folder | `fsutil reparsepoint query`: the junction's tag `0xa0000003` (`IO_REPARSE_TAG_MOUNT_POINT`), a folder of the owner's OneDrive (Files On-Demand) `0x9000601a` (`IO_REPARSE_TAG_CLOUD_6`). The OneDrive folder and every folder looked at under it are `Directory, ReparsePoint`, with an empty PowerShell `LinkType` (`Junction` for the junction): the reparse-point attribute takes in every OneDrive folder. .NET's `FileSystemInfo.LinkTarget` is non-null only for a symbolic link or a junction (its documentation and `FileSystem.Windows.cs`; UNVERIFIED: no .NET 10 was run) | — |
| Scan cost, the development machine's projects folder (D:) | breadth-first to depth 4 with 1.8's rules: 208 folders searched, 42 repositories, 19 folders left out by name, no links; 339 ms the first run, 60 and 66 ms after. MSYS `find` over the same folder: 726 folders in 1,135 ms, machine loaded | — |
| Scan cost, the user's home folder (C:) | every reparse point left out: 8,212 folders, 36 repositories, 18 reparse points, 1 unreadable folder; 15.1 s cold, 5.1 s warm. Only links left out (OneDrive followed): 8,477 folders, **37** repositories (one in OneDrive), 3.8 s warm. Links and Hidden or System folders left out: **488** folders, 19 repositories, 139 ms; the other 18 repositories lie in hidden folders (`AppData`: tools' own clones) | — |
| Scan cost, `C:\Program Files` | 2,773 folders, no repository; 9.2 s cold, 1.8 s warm | — |
| `workspaces.json` held or shut out (.NET 10 on the development machine, the reviews' probes; `JsonSessionStore`'s calls: `File.ReadAllText`, `File.Move(…, overwrite: true)` to the set-aside name, the save's `.tmp` and `File.Move(…, overwrite: true)`) | Another handle has the file open for writing (`FileShare.Read`, `None` or `ReadWrite`): the read throws `IOException` (a sharing violation) and so does the set-aside, which leaves the file in place and makes no copy; a save throws `UnauthorizedAccessException`; once the handle closes the next save succeeds and the file holds only the new list. The file's own ACL granting the user nothing: the read throws `UnauthorizedAccessException`, the set-aside fails too and the file stays. Only the read-only attribute: the read succeeds and every save throws `UnauthorizedAccessException`. On Linux and macOS `FileShare` is advisory (`flock`) and a rename needs only the folder's permission (UNVERIFIED: not run there) | — |
| The tab strip today (`Views/Shell/RepoTabsView.axaml`) | A horizontal `StackPanel` of tabs inside a horizontal `StackPanel` with "+" after them (lines 14-20, 66-74): nothing shrinks or scrolls; the window clips the strip, "+" first. A tab is a `Border` 120 to 220 wide (line 27) with a 1-px right line; `TabButton`'s padding is 14,0,8,0 (`Theme/Controls.axaml:90`); the close button, 22 wide with a 6-px right margin (line 50), shows on every tab but a lone empty one. So a tab is `clamp(51 + title, 120, 220)` wide, a title at most 169 px. "+" is 34 wide with 2 px either side: 38 (line 68). `New tab` is 49.7 px (its tab 120), `web-app` 53.2, `design-system` 87.9, `Web` 26.9, `W…` 22.1, "…" alone 10.3. At 1000 wide 8 tabs of 120 fit (8 × 120 + 38 = 998) and a ninth pushes "+" out; 4 tabs fit whatever their titles (4 × 220 + 38 = 918). No earlier picture has more than 3 tabs (phase 1's `15a`, phase 5's `a1a`) | — |
| The welcome page's error wrap | The welcome stack is at most 440 wide with a margin of 24 (`WelcomeView.axaml:11`) in the graph column, `width − 260 − 400` (`MainWindow.axaml:25-29`): 392 px of text at 1100×700, 440 at 1920×1080, 292 at 1000 wide. Phase 1's `11a`: line 1 `C:\Users\rpkha\AppData\Local\Temp\VisualCommit.Tests\plain-` (379.1 px of 392), line 2 `a373a2259cd0 is not a git repository.`; the real window broke at the same hyphen. Unicode's line breaking (UAX #14): `\` is of class PR and letters AL, and LB24 forbids a break between them; `:` allows one before `\`; a hyphen allows one after it. So a Windows path breaks only after `C:` (`C:` alone when the run up to the first hyphen is wider than the line: at 292 px that run is 379) or after a hyphen inside a name. Inter has U+200B and U+FEFF, both with no advance, and lacks U+2060 (read from all six faces). With 13.2's rule the lines become `C:\Users\rpkha\AppData\Local\Temp\VisualCommit.Tests\` (345.5 px) and `plain-a373a2259cd0 is not a git repository.` (256.0 px) | — |

### 0.9 Editors and merge tools on the machine

Looked at on 2026-10-17 and 2026-10-18 by file listing and the programs' `--help` only; **no editor
was started with a file**, which would open windows on the owner's desktop.

| What | Result |
|---|---|
| Editors installed here | VS Code 1.142.0 at `%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe` (not under `%ProgramFiles%`); Cursor 3.9.16 at `%LOCALAPPDATA%\Programs\cursor\Cursor.exe`; Notepad++ at `%ProgramFiles(x86)%\Notepad++\notepad++.exe` (**not** under `%ProgramFiles%`); IntelliJ IDEA Community 2020.3.2 at `%ProgramFiles%\JetBrains\IntelliJ IDEA Community Edition 2020.3.2\bin\idea64.exe`, beside `fsnotifier64.exe` and `runnerw64.exe` (so `*64.exe` is no rule); Visual Studio 2022 Community's `devenv.exe`; `%SystemRoot%\System32\notepad.exe`. Not installed: VS Code Insiders, VSCodium, Sublime Text, Vim |
| `code` on `PATH` | `where code` finds first `…\cursor\resources\app\codeBin\code.cmd` (Cursor's shim), then `…\Microsoft VS Code\bin\code.cmd`: a name on `PATH` can be another program |
| The commands are batch files | VS Code's `bin\code.cmd` runs `"%~dp0..\Code.exe" "%~dp0..\<hash>\resources\app\out\cli.js" %*` with `ELECTRON_RUN_AS_NODE=1`; Cursor's `cursor.cmd` and `codeBin\code.cmd` alike. `CreateProcess` hands a `.cmd` to `cmd.exe`, which parses the arguments, and a file name may hold `&`, `%` and `^` (a cloned repository chooses its names). How .NET 10 quotes `ArgumentList` for a `.cmd`: not checked; the design avoids it (12.4) |
| Line options | `code --help` (1.142.0) and `cursor --help` (3.9.16) show `-g --goto <file:line[:character]>`; whether `Code.exe` and `Cursor.exe` started directly, without `cli.js`, honour `--goto`: not checked. Sublime's `:line`, Notepad++'s `-n<line>`, JetBrains' `--line`, gedit's `+<line>`, Kate's `--line`, `xed --line`, `bbedit +<line>`: from the vendors' documentation, not run |
| `core.editor` here | Git for Windows' installer wrote `core.editor = "C:\Program Files (x86)\Notepad++\notepad++.exe" -multiInst -notabbar -nosession -noPlugin` into the system file: a shell command line meant to block until the file is closed. With nothing set, `git var GIT_EDITOR` prints `vi` (2.36) or `vim` (2.55); `EDITOR=nano` does not beat `core.editor`, `GIT_EDITOR=gedit` does |
| `PATHEXT` here | `.COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC;.PY;.PYW` (the process adds `.CPL`, 0.10); `assoc .py` gives `.py=Python.File`. `.wsh`, `.msc`, `.py` and `.pyw` are not in Open file's fixed list (`RepositoryOperations.Files.cs:117-121`), so they open without a question today (risk 20) |
| Merge tools found here | 2.36: the vimdiff family only (terminal tools). 2.55: also `vscode`, which runs `code` from `PATH`: Cursor's shim on this machine |
| A file check on a network path | `[IO.File]::Exists`, the Win32 call `File.Exists` and `Directory.Exists` make, on `\\192.0.2.1\share\Ed.exe` (a server that does not answer): false after 271,791 ms, and again after 271,853 ms (Windows keeps no negative answer); on a host name that does not resolve 4,782 ms, then 4,704 ms; on a drive letter that does not exist 0 ms (2026-10-18). A program chosen through Other… may lie on such a path (12.5), so 12.1 and 12.5 check none on the UI thread |
| Not checked | 12.2's macOS and Linux install paths (no such machine here); `--tool-help` on an idle machine; which git version added the `vscode` merge tool |

### 0.10 Harness facts

Run on 2026-10-18 with Git 2.36.0.windows.1 and Visual Studio's 2.55.0.windows.5 in a lab folder,
isolated as the tests are, with `HOME` and `XDG_CONFIG_HOME` in the lab; the processor stood at about
70 % with other work, so times are indications.

| What | Result |
|---|---|
| The ten repositories the TempRepo way (r10, 2.36) | `CreateAsync`, `CommitAsync` (2 processes each), `AddBareRemoteAsync`, `push --set-upstream`, a sibling clone for the remote's commits, `fetch`: 80 processes plus 10 blank git folders, `real 0m22.091s` loaded; at the default run's 1.2 s per git process (phase 5's reflog scenario: 145 calls in over 3 minutes), 1 to 2 minutes for a process's first check |
| The lean way (17.1's steps) | **28 git processes for the ten**, plus the 10 blank folders made once per process and shared: 8.9 to 14.3 s. billing's merge prints `CONFLICT (content): Merge conflict in invoice.txt`, exit 1. Refs and `git status --porcelain=v2 --branch` byte-identical on 2.36 and 2.55, SHAs included: fast-import's commits depend only on the stream |
| Relative remote URLs | `url = ../remotes/<name>.git` resolves against the working tree's top: `git -C docs fetch --progress origin` exit 0; from a subfolder a fetch exit 0 (2.36) and `ls-remote` exit 0 (2.55, r10); `git remote -v` prints `origin	../remotes/docs.git (fetch)`. A copy fetches from its own `remotes/` |
| Copying the template | 544 files in 379 folders, 1.3 MB (561 with 2.55), `cp -r` 2,369 to 2,655 ms; 221 are `hooks/*.sample` (13 per git folder on 2.36, 14 on 2.55 with `sendemail-validate.sample`; 17 git folders); without them 323 files, 1,532 to 1,594 ms. Outside the virus scanner's exclusion (`%TEMP%\VisualCommit.Tests` is inside it); r10: 590 files with the sibling clones, 3.06 to 3.47 s; `File.Copy` not timed |
| What a fetch's progress counts | `Counting objects` counts the objects to send **and the boundary objects**, the same over the file remote and over HTTP (2.36's `git http-backend` as CGI, protocol v2): docs and mobile (one new commit, one new file) `remote: Enumerating objects: 4, done.`, `remote: Counting objects:  25% (1/4)`, `50% (2/4)`, `75% (3/4)`, `100% (4/4)`, `remote: Total 3 (delta 1)`; design-system (three commits, one file each) `Enumerating objects: 11`, `Counting objects:   9% (1/11)`, `18%`, `27%`, `36%`, `45% (5/11)`, `54%`, `63%`, `72%`, `81%`, `90% (10/11)`, `100% (11/11)`, `Total 9 (delta 3)`. Exit 0; the other five fetch and print nothing. r10's holds at 33, 66 and 88 % cannot occur |
| A hold with no percentage | `remote.origin.uploadpack = sh '<lab>/hold-upload-pack.sh'`, a script that touches `held-<folder name>`, waits at most 600 rounds of `sleep 0.1` for a file `release`, then runs `exec git upload-pack "$1"`. In the global file alone it holds every `origin`: `api` (nothing new) and `docs` held (`held-api.git`, `held-docs.git`, nothing printed), exit 0 after the release, docs printing `From ../remotes/docs` / `   23e2ad1..245b729  main       -> origin/main` (2.36). Visual Studio's 2.55 (without `MSYSTEM`) made the markers but did not wait (1,890 bytes within 4 s): its `usr/bin` has `sh.exe` but no `sleep.exe`, which phase 5's `Scenarios.WaitingMergeToolCommand` and `RepositoryOperationsQueueTests`' `pre-receive` hook need too. **The section in the global file gives every repository a remote**: in one without a remote `git remote` prints `origin` (both gits), `git remote -v` `origin` with no URL, `git remote get-url origin` `error: No such remote 'origin'` (exit 2), and `git fetch --progress origin` is held (`held-origin`), then ends `fatal: 'origin' does not appear to be a git repository`, 128 (2.36). Appended as text to a repository's own `.git/config`, a second `[remote "origin"]` header merges with the first: `git remote -v` unchanged, the fetch held and exit 0 after the release, a repository without a remote still listing none (2.36). A value holding `\` (`sh 'C:\Users\…\hold-upload-pack.sh'`) makes every git call that reads the file fail, `git --version` included: `fatal: bad config line 2 in file …`, 128; with `/`, or every `\` doubled, it reads back unchanged and holds (2.36). The script touches its marker before it looks for `release` and never removes it: a fetch after the release still makes one, and a killed fetch's stays. Each round is a process: 100 rounds took 14.9 s and about 3.6 s of processor, about 36 ms a round; four loops at once about two of the laptop's 8 processors (2.36, loaded) |
| `GIT_CONFIG_GLOBAL` in a missing folder | `git config --list --show-scope` and `git status` exit 0, `git config --global user.name` exit 1 with nothing printed; a write exits 255 and makes no folder (0.3); both gits. A missing file in an existing folder is made by the first write, exit 0 (r10) |
| What reaches the app under test today | The owner's `~/.config/git/ignore`, read even with `GIT_CONFIG_GLOBAL` set (0.3), in both harnesses (not `TempRepo`'s calls, which set `XDG_CONFIG_HOME`); `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL`, `GIT_COMMITTER_NAME`, `GIT_COMMITTER_EMAIL` and `EMAIL`, which beat every file (0.3). `GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` are the harnesses' own (`GitIsolation.AppCommitDates`, D72) |
| `MSYSTEM` (r02) | Visual Studio's 2.55 crashes (exit -1073741819, 139 from Git Bash) whenever it runs a shell with `MSYSTEM=MINGW64`, which every process started from Git Bash has (`dotnet test` from the Bash tool too); 2.36 does not. Not reproduced on 2026-10-18: a hook, a shell `core.sshCommand` and `FakeSshTests` (7 of 7) ran under Visual Studio's 2.55 with `MSYSTEM=MINGW64` inherited. Git for Windows sets `MSYSTEM` again for the shells it starts, so WP-L removes it from the tests' isolation all the same (`GitIsolation.ShellVariables`) |
| `PATHEXT` here | `.COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC;.PY;.PYW;.CPL` in the process, the machine's value without `.CPL`, `.PY;.PYW` from Python's installer (0.9); Windows' default ends at `.MSC` |
| Port 47393 | Not in Windows' excluded ranges (`netsh interface ipv4 show excludedportrange protocol=tcp`: only 50000-50059), nothing on 47390 to 47394 (`Get-NetTCPConnection`); on Linux in the ephemeral range, as 47391 (phase 3 design 7.3), so the fallback to a free port (`PortWasTaken`) stays |
| The renaming refspecs (17.1) | `git branch --track probe refs/remotes/<T>`, then `branch.probe.remote` and `.merge`: `fork/up/main` → `branch 'probe' set up to track 'fork/main'.`, `fork`, `refs/heads/main`; `origin/pr/1` → `… track 'origin/pr/1'.`, `origin`, `refs/heads/pr/1` (git takes the first candidate; R2); `upstream/pr/7` → `… track 'upstream/refs/pull/7/head'.`, `upstream`, `refs/pull/7/head` (R4); `origin/feature/x` the identity. Exit 0, both gits |
| The ignored files without the app's check (17.1) | After `git update-index -q --refresh`, each on a fresh copy: `git switch --no-guess --no-overwrite-ignore vendor` and `git switch --create vendor2 --track --no-overwrite-ignore refs/remotes/origin/vendor` → `error: The following untracked working tree files would be overwritten by checkout:` / `\tsecret.env` / `Please move or remove them before you switch branches.` / `Aborting`, exit 1, `secret.env` still `LOCAL`; `git switch --no-guess --no-overwrite-ignore tools` → `error: Updating the following directories would lose untracked files in them:` / `\tbuild` / (empty line) / `Aborting`, exit 1. Without the option `vendor` is checked out (0) and `secret.env` reads `vendor`. `git merge --no-edit --no-overwrite-ignore refs/heads/vendor` → `Merge made by the 'ort' strategy.`, 0, overwritten; `git rebase refs/heads/vendor`, `git cherry-pick refs/heads/vendor`, `git revert --no-edit HEAD~1` (re-adds `old`), `git stash apply stash@{0}` (`new file:   secret.env`, `stashed`): 0, each overwrites it. Identical on 2.55. With `vendor` pushed a second time as `vendor-remote` (`git push origin vendor:vendor-remote`, no `-u`: `refs/remotes/origin/vendor-remote` and no local branch; listed last in one `-u` push, it would make `vendor` track `origin/vendor-remote`): `git switch --create vendor-remote --track --no-overwrite-ignore refs/remotes/origin/vendor-remote` → the same block, exit 1, no `refs/heads/vendor-remote`, HEAD still `main`, `secret.env` `LOCAL`; beside the local `vendor`, `git switch --create vendor --track --no-overwrite-ignore refs/remotes/origin/vendor` → `fatal: a branch named 'vendor' already exists`, 128 (2.36) |
| `VisualCommit.App.Tests` | 422 headless test methods (`[AvaloniaFact]`, `[AvaloniaTheory]`) in 58 files and 57 classes (`CommitGraphControlTests` is split over two), 754 plain ones (a `grep`, `72c790c`); 214 of the plain ones lie in those 57 classes: 166 in the 24 files that hold both kinds, 48 in `RepositoryMenusTests`' three partial files without a headless test (`RepositoryMenusTests.cs` 34, `.Pages.cs` 13, `.ActivityLog.cs` 1); many of them build real-git scenarios (`Scenarios.OperationsAsync`). Last to end in step 2's baseline (37 min 26 s of 37 min 29 s, `acdac12`, the laptop otherwise idle) and at phase 5's gate (43 min 8 s of 43 min 22 s). Avalonia.Headless.XUnit runs headless tests one at a time on the assembly's one UI thread; whether that thread or the processors set the length was not measured then: WP-S measured the processors (17.3) |
| Phase 5's walk-throughs (r10, `artifacts/visual/phase-5/timings/`) | `Phase5`: 34 methods, 143 pictures, 681 s summed from each method's start to its last picture (20.0 s per method, 4.8 s per picture); `Phase5Undo`: 55 methods, 121 pictures, 1,305 s. `Phase5` ended at 18 min 56 s idle and 29 min 50 s at the gate: wall time 1.67 to 2.6 times the UI thread's |
| `GitCallLog` | Reads kept in a ring of 2,000 (`GitCallLog.DefaultReadCapacity`, `src/VisualCommit.Git/GitCallLog.cs:23`), so a count taken before an input shifts when reads drop out; `GitCallRecord.Sequence` (`src/VisualCommit.Core/Git/GitCallRecord.cs:51`) and `WorkingDirectory` (`:36`) mark and filter calls exactly |

## 1. Workspaces (C1, D187 to D189)

A workspace is a named, ordered list of repositories checked and updated together (C1). It is the
app's own data: creating, changing or deleting one changes nothing on disk but `workspaces.json`
(D189). Each has one dashboard tab (section 2). Section 13 has the welcome page's Workspaces section
and the tab strip.

### 1.1 The model (`src/VisualCommit.Core/Workspaces/Workspace.cs`, pure but for `WorkspacePaths.Same`'s short-name expansion, 1.2)

Sealed records: `WorkspaceList` (`Version`, default `CurrentVersion` = 1; `Workspaces`;
`Find(string id)`, ordinal), `Workspace(string Id, string Name, IReadOnlyList<WorkspaceRepository>
Repositories)` (`MaxNameLength` = 100), `WorkspaceRepository(string Path)` (an object, so that a later
version can add fields).

An `Id` is `Guid.NewGuid().ToString("N")`, never shown; the session names a dashboard by it (1.11),
so a rename keeps an open dashboard. A `Name` is trimmed, 1 to 100 characters, without control
characters (line breaks and tabs included), unique ignoring case on every platform (a name is no
path). `Repositories` keep the user's order (added at the end, moved by 1.10); a path is the working
tree's top as git reports it, `IGitRepository.WorkingDirectory` (absolute, no trailing separator,
the physical path in the file system's case with long names, 0.8), as the tabs keep it. A repository
may be in several workspaces, once in each; there is no limit on either. A linked worktree is a
repository of its own: one row, as it is one tab.

### 1.2 What "the same repository" means

`WorkspacePaths` (Core, pure but for `WorkspacePaths.Same`'s short-name expansion): `Normalise(path)` is `Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))`
for a fully qualified path, and returns any other path (only a hand-edited file has one) as it is;
`Same(a, b)` compares the normalised paths with `WorktreeKey.Comparer` (ignoring case on Windows and
macOS, exactly elsewhere), as the recent list and the graph filters do. Every match of a folder uses
`Same`: the workspace code, the dashboard's rows and pending tabs (2.3), the palette (5.10).
On Windows `Path.GetFullPath` also expands 8.3 short names, and for a path with a `~` it asks the file
system for the long names (`GetLongPathName`), so a share that does not answer holds the caller until
the file system gives up. Accepted (WP4): such paths are rare, since what the folder dialog and git
give are long names; a path without a `~`, and any path elsewhere, is worked out from its text alone.

The app writes only paths git reported; other spellings come from a hand-edited or copied file and
from the harnesses' `TempRepo.Path` (through `C:\Users\RUNNER~1` on CI's Windows and `/var` on macOS,
where git reports `C:\Users\runneradmin` and `/private/var`); comparing texts alone would make every
such row "Not a repository". A read never rewrites the saved text. Two rules:

1. **Is the saved folder the working tree's top?** `IGitRepository.OpenedAtTopAsync(CancellationToken)`,
   for `OpenedFolder`: true with no git call when `WorktreeKey.Comparer` finds it equal to
   `WorkingDirectory` (so an 8.3 short name makes no call, since the opening's `Path.GetFullPath` has
   expanded it, and neither do capitals on Windows and macOS, which the comparer finds equal; WP4);
   otherwise one `git rev-parse --show-prefix` in it with the read environment
   (D47): an empty line (exit 0) is the top spelled another way (0.8), anything else (`inner2/`) a
   folder inside; a failure throws `GitException`. The interface's default answers true (the fakes
   need no change). The opening call stays as it is (`GitRepository.cs:87`, pinned by phase 5).
2. **Is a repository already in the workspace?** `WorkspaceMembership.Contains(workspace,
   workingDirectory, openedWorkingDirectoryOf)`: a saved path is `Same` as it, or a row whose
   repository is open (3.4) reports that working directory. "Add repository…", "Add open tabs",
   "Locate…" and the scan's "Already in this workspace" ask it.

### 1.3 `workspaces.json` and its store (D189)

`AppPaths.WorkspacesFile`, beside `SettingsFile` and `SessionFile`, indented and camelCase: `{
"version": 1, "workspaces": [ { "id": "3f2c…", "name": "Platform", "repositories": [ { "path":
"D:\\src\\api" }, … ] } ] }`. `IWorkspaceStore` (Core, shaped like `ISettingsStore`): `Current`;
`IsReadOnly` (nothing is saved this session) and its `ReadOnlyCause` (`NewerVersion`, a newer
version's file; `Unreadable`, a file that could be neither read nor set aside); `Notice` (what the
welcome page says about the file, and while `SaveFailed` the dashboard too, 2.5; or null); `SaveFailed` (true from a failed save until a save
succeeds); `Update(Func<WorkspaceList, WorkspaceList>)` (applies and saves; a failed save is logged,
never thrown); `Changed` (after `Update` changed `Current`, on the caller's thread, with `Notice` and
`SaveFailed` already set). `JsonWorkspaceStore` (App, a source-generated `WorkspacesJsonContext` with
`JsonSessionStore`'s options) copies `JsonSessionStore`, except that it never writes over a file it
could not read: a lock; no save for a change that gives the same object; `workspaces.json.tmp`
swapped in by `File.Move(…, overwrite: true)`; `static Write(path, list)` for the harnesses; an
optional `IWorkspaceFiles` (read, write, move; `PhysicalWorkspaceFiles` the real one, both in
`JsonWorkspaceStore.cs`), through which tests make a read, a move or a save fail: `FileShare`'s locks
are advisory on Linux and macOS, so a test that locks the file would need a branch per platform (0.8).

- No file: an empty list; the first change writes the file.
- Bad JSON, or a read that throws `IOException`, `UnauthorizedAccessException` or
  `NotSupportedException` (one that throws `IOException` is tried twice more, 100 ms apart, as a virus
  scanner or a backup may hold the file a moment): logged at Warning and moved aside to the first free
  name of `workspaces.unreadable.json`, `workspaces.unreadable-2.json`, `workspaces.unreadable-3.json`,
  … (never over an earlier copy, which may hold the only other list). Then:
  - the move succeeds: an empty list, `Notice` `workspaces.json could not be read, so it was set aside
    as {file}.`, `{file}` the name it got;
  - the move fails too, so the file is still in place (another program holds it, or its permissions
    shut the user out, 0.8): an empty list, read-only with `Unreadable`, `Notice` `workspaces.json
    could not be read, so changes to workspaces are not saved this session.`; nothing writes over the
    file, and the next start reads it again.
- `version` above 1: what this version understands is read, logged at Warning; read-only with
  `NewerVersion`, `Notice` `workspaces.json was made by a newer version of VisualCommit, so workspaces
  cannot be changed in this version.` (every edit is disabled, 1.5, so nothing is left to save); while read-only, for either cause, `Update` changes `Current` and raises
  `Changed` but never writes (every edit is disabled, 1.5). `version` missing or 0 (the generated
  serializer reads a missing one as 0): version 1.
- A `null` list reads as empty; a workspace with a blank id or name, a second with an id already
  seen, a blank path and a path the workspace holds already (`Same`) are dropped; names are trimmed;
  names differing only in case are both kept. A folder that is gone stays (1.10).
- A failed save (the `.tmp` write or the swap throws `IOException` or `UnauthorizedAccessException`:
  the file has the read-only attribute, or another program holds it, 0.8) is logged at Error and the
  change applies for the session; `SaveFailed` becomes true and `Notice` `workspaces.json could not be
  saved, so changes to workspaces are lost when VisualCommit closes.`, shown by the welcome page (13.1)
  and the dashboard (2.5), until a save succeeds (each save writes the whole list, so one that succeeds
  keeps every earlier change); then the load's notice, if any, comes back. Two instances on one data
  folder: the last save wins, as for `settings.json` and `session.json`.

`AppSession.Start` makes the store after the session, a `JsonWorkspaceStore` on `AppPaths.WorkspacesFile`
(`AppSession.Workspaces`; `AppSessionTests`, `fef0569`), and passes it to
`TabServices` (a new optional `IWorkspaceStore? workspaces`, in memory when null, for view-model
tests), which gains `Workspaces`; `WorkspaceItems`, an `ObservableCollection<WorkspaceItem(string Id,
string Name, string CountText, string ToolTipText)>` shared by every welcome page as
`RecentRepositories.Items` is, rebuilt on `Changed`, sorted by name (`OrdinalIgnoreCase`) then id;
`WorkspaceCommands` (1.5); and `WorkspaceHost` (`IWorkspaceHost?`), set by the window.
`MainWindowViewModel` implements `IWorkspaceHost`: `ShowWorkspace(string workspaceId,
RepoTabViewModel? from)`, the one way a workspace opens (placed by 2.2), called by the welcome page,
`WorkspaceCommands` and the palette (`from` the active tab), never through an event; and
`OpenRepositoryFolders()`, the working trees of the tabs with a repository open, in tab order.

### 1.4 The edits (`src/VisualCommit.Core/Workspaces/WorkspaceEdits.cs`, pure but for `WorkspacePaths.Same`'s short-name expansion, 1.2)

Each returns the same object when it changes nothing, so the store neither saves nor raises
`Changed`: `Create(list, id, name)` appends `Workspace(id, name, [])`; `Rename(list, id, name)`;
`Delete(list, id)`; `Append(list, id, paths)` appends, in the order given, each path not held yet
(`Same`); `Remove(list, id, path)`; `Move(list, id, path, delta)` by −1 or +1, nothing at either end;
`Replace(list, id, oldPath, newPath)` puts `newPath` in `oldPath`'s place (Locate), the same list when
`newPath` is held already.

### 1.5 The commands

`WorkspaceCommands` (`ViewModels/Workspaces/`, one per session) holds every flow of 1.6 to 1.10, so
the welcome page, the dashboard and the palette run the same code, with the store, the dialog layer,
the folder dialog (`IFolderPicker`), the repository provider and the host. While the store is
read-only every command but Open is disabled with `EditReason`: `workspaces.json was made by a newer
version of VisualCommit` (`NewerVersion`) or `workspaces.json could not be read` (`Unreadable`, 1.3).
Errors use the C11 error dialog with the workspace's name as its "In {name}" line and git's output
where git said something.

| Command | Label | Offered on | Dialog | Disabled when (reason) |
|---|---|---|---|---|
| Create | `New workspace…` | the welcome page (13.1), the palette | the name dialog (1.6) | read-only |
| Open | a click on the welcome item, its menu's `Open`; the palette's `Open workspace {name}` | the welcome page, the palette | none | never |
| Rename | `Rename workspace…` | the welcome item's menu, the dashboard's `…` menu (2.5), the palette on a dashboard tab (5.4) | the name dialog | read-only |
| Delete | `Delete workspace…` | as Rename | a confirmation (1.6) | read-only |
| Add repository | `Add repository…` | the dashboard's header and empty state (2.5), the palette on a dashboard tab (5.4) | the folder dialog, perhaps 1.9's | read-only |
| Add open tabs | `Add open tabs` | the dashboard's `…` menu and empty state, the palette on a dashboard tab (5.4) | none | read-only; `No tab has a repository open`; `Every open repository is in this workspace` |
| Remove | `Remove from workspace` | a row's menu, Delete on a row (on macOS also Cmd+Backspace, 2.7) | none: no question | read-only; while a run of its dashboard holds the row (in line, reading or running, 4.3) `Wait for Fetch all to finish` / `Wait for Pull all to finish` (1.10, 4.7) |
| Move | `Move up`, `Move down` | a row's menu, Alt+Up and Alt+Down (2.7) | none | read-only; `It is the first repository` / `It is the last repository` |
| Locate | `Locate…` | the menu of a row in any state but Ready (1.10) | the folder dialog | read-only |

### 1.6 Creating, renaming and deleting a workspace

**The name dialog**: `WorkspaceNameDialogViewModel : NameDialogViewModel`, drawn by `NameDialogView`
through the base type, with factories `Create` and `Rename` as `BranchNameDialogViewModel`'s:
titles `New workspace` and `Rename workspace`; the rename's context line `Rename {name}`; the box
empty or holding the name selected (`SelectsNameOnOpen`), placeholder `Workspace name`; confirm
`Create`, disabled until the name is valid, or `Rename`, disabled until the trimmed name differs
(ordinal: a change of case alone may be saved).

`DialogText.WorkspaceNameError(name, existing)` checks the trimmed name in this order, shown once the
box was edited (D114): empty `Enter a name.` (`DialogText.EnterName`); over 100 characters `This is
not a valid workspace name: it has more than 100 characters.`; a control character `This is not a
valid workspace name: it has a line break or another control character.`; another workspace has it,
ignoring case (on rename, itself left out) `A workspace named {existing} exists.` (the other's
spelling).

**Creating**: `Create`, then `ShowWorkspace(id, from)` (from a welcome page its empty tab becomes the
dashboard, 2.2): an empty dashboard offering `Add repository…` and `Add open tabs` (2.5); nothing is
added by itself. **Renaming**: `Rename`; the dashboard's title, the welcome pages and the palette
follow `Changed`. **Deleting**: `IDialogService.ConfirmAsync`, destructive, confirm `Delete`, title
`Delete workspace?`, question `Delete the workspace {name}? Its {N} repositories stay where they are:
nothing on disk changes.` / `Delete the workspace {name}? Its repository stays where it is: nothing on
disk changes.` / `Delete the workspace {name}? Nothing on disk changes.`, detail while its dashboard is
open `Its dashboard tab closes.`; then the dashboard closes as by its close button (2.1,
`DashboardViewModel.WorkspaceDeleted` from `Changed`), and tabs opened from it stay. Workspace edits
are not in the undo history (D154).

### 1.7 Adding repositories

**`Add repository…`**: the folder dialog titled `Add a repository to {workspace}` (one folder,
`AllowMultiple = false`; the harnesses' `FakeFolderPicker` answers one):

| The folder | What happens |
|---|---|
| Cancelled | Nothing |
| A working tree's top (`OpenAsync` succeeds, `OpenedAtTopAsync` true) | Its `WorkingDirectory` is appended at once, no dialog; nothing when held already (1.2). Either way the command returns the path, and the dashboard selects that row and scrolls it into view (2.6): the click's visible answer; the new row is read at once (2.10's trigger 6), so it leaves `Reading…` without F5 |
| Inside a working tree but not its top, not a repository, or a bare repository | Scanned (1.8); 1.9's dialog lists what was found, the repository holding the folder, if any, first and unchecked, so a home folder kept in git still lets the user add `~/src`'s repositories |
| `GitException` (such as "dubious ownership") | Error dialog `Adding a repository failed`: `Git could not open {folder}.`, git's output below |
| `GitUnavailableException` | `Adding a repository failed`, the exception's message as the summary |

A folder inside a repository is scanned, not replaced by its top (a saved folder must be a top),
unlike a tab's Open, which opens the repository around any folder.

**`Add open tabs`**: appends, in tab order, `IWorkspaceHost.OpenRepositoryFolders()` (dashboards,
empty tabs and tabs still opening left out) that the workspace does not hold (1.2); no dialog.
The added rows are read at once (2.10's trigger 6): a row whose folder is open in a tab follows that
tab without a git call.

### 1.8 The folder scan (`src/VisualCommit.Core/Workspaces/RepositoryScanner.cs`)

`RepositoryScanner.Scan(root, progress, cancellationToken, options, fileSystem)` reads only the file
system (no git), on `Task.Run` from 1.9's dialog. `ScanOptions(int MaxDepth = 4, int MaxFolders =
10_000)`; `ScanProgress(int FoldersSearched, int Found)`; `ScanResult(IReadOnlyList<string>
Candidates, int FoldersSearched, bool StoppedAtLimit)`; `IScanFileSystem` (a folder's folders with
name, link and hidden flags; whether one has a `.git` entry), `PhysicalScanFileSystem` the real one,
so tests can fake unreadable or 10,000 folders.

- Breadth-first from the chosen folder (depth 0, never a candidate) to depth 4: a folder with a
  `.git` folder or file is a candidate and is not opened (submodules, O10 "Later", and nested
  repositories are left out); depth-4 folders are not opened: `<root>/a/b/c/repo` is found,
  `<root>/a/b/c/d/repo` not.
- Left out below the root: `node_modules`, `bower_components`, `vendor`, `packages`, `bin`, `obj`,
  `target`, `build`, `dist`, `out` (ignoring case) and names starting with `.`; links, the folders
  with the reparse-point attribute whose `FileSystemInfo.LinkTarget` is not null (loops, a repository
  twice), while other reparse points, such as every OneDrive folder, are followed (0.8); on Windows,
  Hidden or System folders (the home folder: 488 folders instead of 8,477, only tools' clones in
  `AppData` lost, 0.8); folders whose listing throws `UnauthorizedAccessException`, `IOException` or
  `DirectoryNotFoundException`, quietly.
- At most 10,000 folders, counted as taken from the queue; the limit sets `StoppedAtLimit` and keeps
  what was found. Progress at most every 100 ms and at the end; the token stops at the next folder.

**Confirming** (`AddRepositoriesDialogViewModel`): each candidate is opened with
`IRepositoryProvider.OpenAsync`, 4 at a time, and kept when `OpenedAtTopAsync` is true; one git
refuses or passed over (an empty `.git` inside another repository, 0.8) "could not be opened". Kept
ones with one working directory appear once, ordered by their relative path (`OrdinalIgnoreCase`).
Each open logs `Opened the repository at …` at Info, as a tab's does; nothing else is read.

### 1.9 The "Add repositories" dialog

`AddRepositoriesDialogViewModel : DialogViewModel<IReadOnlyList<string>?>` (view
`AddRepositoriesDialogView.axaml`) answers the working trees to append in the list's order, or null:
the wide card, 460 wide at both sizes (418 inside), centred, about 400 high with eight rows.

| Part | Searching | Done |
|---|---|---|
| Title (`AddRepositoriesTitle`) | `Add repositories to {workspace}` | the same |
| `ScanFolderText` (a `PathTextBlock`, 13.2) | `Looking for repositories in {folder}…` | `Found {N} repositories in {folder}.` / `Found 1 repository in {folder}.` / `No repositories were found in {folder}.` / with the enclosing repository listed `No other repositories were found in {folder}.` |
| `ScanProgressText` (secondary) | `Searched {n} folders, found {k} repositories` (`1 folder`, `1 repository`; `N0`, invariant culture) | hidden |
| `FoundList` (bordered, `MaxHeight` 232: 8 rows of 28, scrolls) | hidden | per repository a `FoundRepository` row (named by the repository): `FoundRepositoryCheck` (`VcCheckBox`), the name (primary), then (secondary, cut with "…", whole in the tooltip) its path relative to the chosen folder; or `Already in this workspace` (checked, disabled); or for the enclosing repository `Contains {folder name}` (unchecked). Others start checked |
| `ScanStoppedText` | — | `Stopped after 10,000 folders: choose a folder further down to search the rest.` |
| `ScanFailedText` (secondary) | — | `1 more folder looks like a repository, but git could not open it.` / `{N} more folders look like repositories, but git could not open them.`; tooltip their relative paths, at most 10, then `…` |
| Buttons | `Cancel`; disabled `Add repositories` | `Cancel`; `Add {N} repositories` / `Add 1 repository`, `{N}` the rows checked and enabled, which are the answer (the `Already in this workspace` rows are left out, a `Contains {folder name}` row counts once checked); disabled `Add repositories` while that count is 0, also when every row found is already in the workspace |

No progress bar: an indeterminate one would differ between two captures of a step. Cancel and Esc
stop the search and the opens and add nothing; Space toggles the focused box; Tab goes round the
boxes and the buttons. The rows the checklist adds are read at once (2.10's trigger 6), as 1.7's.

### 1.10 Folders that are gone, or no longer repositories

A repository stays in the list when its folder is gone or no longer a repository (a drive not
mounted, a list from another machine). `WorkspaceFolderProbe.ProbeAsync(provider, savedPath,
cancellationToken)` sorts a saved folder before anything reads it; every row opens its repository
through it (2.10), and Locate and the add flows use its parts. `ProbeAsync` runs its existence check
on the thread pool, and its `IRepositoryProvider.OpenAsync` runs the whole open there (2.10), so no
part of a probe blocks the UI thread.

| State (`WorkspaceFolderState`) | Found by | Short text (2.6) | Tooltip, and the reason of the row's Open, Fetch and Pull (2.7) |
|---|---|---|---|
| `Ready` | `OpenAsync` succeeds, `OpenedAtTopAsync` true | — | — |
| `FolderNotFound` | not fully qualified, or `directoryExists` false (the probe's `Func<string, bool>` seam, `Directory.Exists` unless a test gives one, called on the thread pool: a share that does not answer can take minutes, 0.1); no git call | `Folder not found` | `{path} does not exist` |
| `NotARepository` | `NotARepositoryException` (no `.git`, bare, an orphaned worktree) | `Not a repository` | `{path} is not a git repository` |
| `NotARepository`, inside another working tree | `OpenAsync` succeeds, `OpenedAtTopAsync` false (0.8); the outer repository is dropped at once, never read further or acted on | `Not a repository` | `{path} is not a git repository of its own: it lies inside {outer}` |
| `NotOpened` | `GitException` (such as "dubious ownership") or `GitUnavailableException` | `Git could not open it` | `Git could not open {path}: {line}`, `{line}` git's first line of standard error or the exception's message |

Every state but `Ready` disables the row's Open, Fetch and Pull with its tooltip as the reason, and
its menu offers `Locate…` and `Remove from workspace`.

**`Locate…`**: the folder dialog titled `Locate {folder name}` (the saved path's last name); the
chosen folder's repository top replaces the saved path in place (`Replace`), also from a folder inside
it, the row's running read is cancelled (its token, 2.10) and the new path is read at once (2.10's
trigger 6). Refusals, in the error dialog `Locating the repository failed`, change
nothing: `{folder} is not a git repository.`; `{top} is already in {workspace}.`; `Git could not open
{folder}.` with git's output. **`Remove from workspace`** (also Delete; on macOS Cmd+Backspace first,
written `Cmd+Delete`, and Delete written `Forward Delete`, 2.7, 6.1): at once, no question (nothing on
disk changes, and the row comes back by 1.7's `Add repository…` on its top or by `Add open tabs`);
selection and focus as 2.7 says; it cancels the row's read (its token, 2.10), stops the row listening
to any view model, and a summary-mode view model the row held is released by 3.4's rules, so a removed
row is never fetched or pulled and holds nothing.
It is disabled while a run of its dashboard holds the row (in line, reading or running, 4.3, 4.7; 1.5): the
run has asked for the row's slot or waits for its read, and every one of the window's 4 slots must
come back. A row the run left `Skipped`, `Ended` or `CancelledInLine` can be removed: the run's counts
and result text stay as they were, since they describe the run as it started, and the row's kept
`DeferredDialog` (4.6) goes with it. `Locate…` on a row the run skipped leaves it skipped in that
run. **Moving rows**:
`Move up`, `Move down`, Alt+Up and Alt+Down, as phase 4's interactive list; no drag (phase 4's is
about 200 lines inside `InteractiveRebaseView.axaml.cs`, not reusable as it stands; backlog, 16.2).

### 1.11 Dashboard tabs in the session (D46, D189)

- `TabState(string? RepositoryPath, string? WorkspaceId = null)`: a dashboard is saved as `new
  TabState(null, workspaceId)` (a path beside an id is ignored); every `new TabState(path)` stays
  valid. `JsonSessionStore.Load` makes a blank path or id null, and a tab `new TabState(null)` only
  when both are blank.
- The restore drops a tab whose workspace id the store lacks (deleted, or `workspaces.json` set
  aside or unreadable, 1.3) and a second dashboard of one workspace; puts the dashboards first in their saved order, the
  other tabs after in theirs (a stable partition: a hand-edited file cannot put a dashboard in the
  middle); maps the saved `ActiveTab` to its tab (the first tab when it was dropped; one empty tab
  when all were). A restored dashboard is made by `ShowDashboard(workspace)` (2.1) and read by 2.10's
  rules; it has no `PendingPath`, so `InitializeAsync` does not open it as a repository.
- `SaveTabs` writes `new TabState(tab.SavedPath, tab.SavedWorkspaceId)` (2.1); the first save after a
  restore drops what it dropped. `UpdateCanClose` gains `|| tab.IsDashboard`. Nothing else of a
  dashboard is kept (the selected row and the scroll offset are the session's own).
- An older version ignores `workspaceId` and sees an empty tab, which its next save drops; a newer
  version reading an older file sees no dashboards.

### 1.12 Tests

Core (`VisualCommit.Git.Tests`): `WorkspaceEditsTests` (each edit; "nothing changes" by
`Assert.Same`; `Append`'s order; `Move` at the ends; `Replace` of a held path); `WorkspacePathsTests`
(`Normalise`; `Same` per platform); `RepositoryScannerTests` on a real tree (each rule of 1.8; a
junction by `cmd /c mklink /J` or a link by `Directory.CreateSymbolicLink` not followed while its
target is found once; a Hidden folder on Windows) and on a fake `IScanFileSystem` (an unlistable
folder, 12,000 folders stopping at 10,000, the token, progress counts).

Git (`GitRepositoryOpenTests`' additions): `OpenedAtTopAsync` from `WorkingDirectory` with **no git
call**, and with none either from an 8.3 short name (the one `cmd /c dir /x` reports, which the
opening's `Path.GetFullPath` expands) or, on Windows and macOS, from capitals (the comparer finds them
equal); through a junction or a symbolic link with exactly one `git rev-parse --show-prefix`; elsewhere
capitals name another folder, and the opening throws `NotARepositoryException` there; false from a
subfolder and from 0.8's two folders inside another repository (WP4).

App: `JsonWorkspaceStoreTests` (each case of 1.3; a newer version leaves the file's bytes alone; `Changed`
once per effective update; through a fake `IWorkspaceFiles`, not the operating system's locks: a read
and a set-aside that both fail give `Unreadable` with its notice and leave the file's bytes unchanged
after `Update`; a read that throws `IOException` twice and then succeeds reads the file; a failed save
sets `SaveFailed` and its notice, and the next save that succeeds clears both and brings back the
load's notice; a second set-aside keeps the first copy and names `workspaces.unreadable-2.json`); the lead's `AppSessionTests`
(`AppSession.Start`'s store is a `JsonWorkspaceStore` on `AppPaths.WorkspacesFile`, `fef0569`); `JsonSessionStoreTests`' additions (a dashboard's round trip, an older
file, blank ids); `DashboardSessionTests` (new; the restore partition and every session case of 1.11,
to which 2.11 refers: the restore of [repository, dashboard W1, empty, dashboard W2] gives [W1, W2,
repository, empty] with the active tab followed; 1.11's drops; `SaveTabs`' ids);
`WorkspaceNameErrorTests` (1.6's texts); `WorkspaceCommandsTests` with `TempRepo`s and a fake folder
dialog (every flow, text and reason of 1.5 to 1.10; a top through a junction counts as held);
`AddRepositoriesDialogViewModelTests` on a tree of `TempRepo`s (1.9's rows and texts; a linked
worktree listed; an empty `.git` and a broken `.git` file counted as refused; the confirm's count:
a tree whose repositories are all in the workspace already gives the disabled `Add repositories`, a
single addable row `Add 1 repository`, and the answer leaves out the rows already in; Cancel during
the search; at most 4 opens at once; the searching texts, which the scripted app cannot hold for a
picture); `WorkspaceFolderProbeTests` (1.10's states and tooltips; no call to the outer repository
after `rev-parse` and `--show-prefix`; the `directoryExists` seam called off the UI thread). Harness
helpers are 17.2's (`ShellDriver.WriteWorkspaces`, `StartWithTabs` for `TabState`,
`RealApp.WriteWorkspaces`). Visual checks: 17.4's W1 (in the real window through Windows' folder
dialog on the copy's root, the Add repositories checklist captured against its scripted twin), W2 (`Locate…` on the gone folder among its steps) and W9; a
saved folder that lies inside another repository is proven by App tests only
(`WorkspaceFolderProbeTests`).

## 2. The dashboard tab

A workspace's dashboard (D187) is a tab at the start of the strip listing the workspace's
repositories, one row each: the branch, ahead and behind, the uncommitted changes, what is in
progress and what the app's operations did. It runs Fetch all and Pull all (section 4), opens a
repository in a repo tab beside it (D188), and has no graph and no side panels; section 3's
repositories back its rows. Paths are relative to `src/VisualCommit.App/` unless they start with
`src/` or `tests/`.

### 2.1 Its place among the tabs

- **A `RepoTabViewModel` holding a `DashboardViewModel`** (`ViewModels/Workspaces/DashboardViewModel.cs`),
  not a second tab type, so every `ActiveTab.*` binding keeps one type. `RepoTabViewModel` gains `Dashboard` (set once
  by `ShowDashboard(Workspace)`), `IsDashboard`, `ShowsDashboard` (= `IsDashboard`), `ShowsLeftPanel`
  (= `!IsDashboard`), `GraphColumn` (0 on a dashboard, else 1), `SavedWorkspaceId` and
  `TabAutomationId` (13.4). On a dashboard (`ViewModels/RepoTabViewModel.cs`): `Title` (39) and
  `BranchText` (99) are the workspace's name, following a rename; `ShowsWelcome` (69) adds `&&
  !IsDashboard`; `ShowsRightPanel` (87) becomes `!IsDashboard && Repository?.IsResolverOpen != true`,
  so `ShowsDetailsPanel` and `ShowsStagePanel` are false; `GraphColumnSpan` (96) is 3; `Toolbar` (56)
  and `OperationStatus` (59) are section 4's (2.8). `Repository` stays null, so whatever acts on
  `ActiveTab.Repository` (Esc's `CloseDiff`, the undo keys, Search's key, auto-fetch's
  `ActiveRepository`, D85) does nothing there.
- **The strip**: the open dashboards occupy `Tabs[0 … d−1]` in the order opened, one per workspace;
  a new one goes at index `d`, and nothing puts another tab before a dashboard. The tab's look (icon,
  padding, id `DashboardTab`) is 13.4's; 13.3's `RepoTabStripPanel` lays it out as any tab.
- **Closing**: a dashboard tab always has its close button (`UpdateCanClose`, 1.11). Closing it
  closes the dashboard only: the repo tabs opened from it stay, its rows' repositories go by 3.7, and
  its bulk run is cancelled as by Cancel all (4.5). Closing the last tab leaves one new empty tab, as
  today (`CloseTab`, `MainWindowViewModel.cs:219-249`). "Delete workspace…" (1.6) closes the
  workspace's dashboard the same way: the window handles `DashboardViewModel.WorkspaceDeleted`,
  raised from `IWorkspaceStore.Changed`, with `CloseTab`.
- **"New tab" and "+"**: unchanged, but for "+"'s automation name, `Add repository` today
  (`Views/Shell/RepoTabsView.axaml:66-72`), which becomes `Open a new tab` (13.4, 15.5): beside the
  header's `Add repository…` (2.5), which adds to the workspace, a screen reader would announce two
  buttons of one name that do different things. Not `New tab`, the empty tab's own `TabButton` name.
  Its id stays `AddRepoButton`. The empty tab's welcome page lists the workspaces (13.1).

### 2.2 Opening a workspace

`MainWindowViewModel` implements `IWorkspaceHost.ShowWorkspace(string workspaceId, RepoTabViewModel?
from)` (1.3), the one way in: the welcome page's list (13.1, `from` its tab), `WorkspaceCommands`'
"New workspace…" once the workspace is made (1.6), the palette's "Open workspace {name}" (5.10,
`from` the active tab).

1. The workspace's dashboard is open: it is activated.
2. Else, when `from` is empty (no repository, not opening, no pending folder, not a dashboard, no
   clone running on its welcome page; a tab whose restore failed counts, its `FailedPath` cleared),
   it becomes the dashboard (`ShowDashboard`) and moves to index `d` (`Tabs.Move`): a welcome page
   that opened a workspace leaves no "New tab" behind.
3. Else a new dashboard tab is inserted at `d`.

Then it is activated (2.10's first read starts) and the tabs are saved. No repository of the
workspace opens in a tab (D188).

### 2.3 Opening a repository from the dashboard

A double-click on a row, Enter on the selected row and the row menu's `Open` raise
`DashboardViewModel.OpenRequested(row)`; the window:

1. activates the leftmost tab that shows that worktree, if any: its `Repository` is the row's view
   model or is registered for the row's worktree (3.4), or its `PendingPath` (a restored tab not
   opened yet) is the row's folder by `WorkspacePaths.Same` (1.2);
2. otherwise inserts a new tab at `1 + max(d − 1, the highest index of a still-open tab this
   dashboard opened in this session)` (`DashboardViewModel.OpenedTabs`, not saved), activates it and
   runs `tab.OpenAsync(row.Path)` with `remember: true` (the recent list's top). The tab adopts the
   row's summary-mode view model when it has one (3.3): the queue, a running operation and the undo
   history carry over.

Example: in `[Web, Tools, api]`, `api` opened from Tools, opening `web-app` from Tools gives `[Web,
Tools, api, web-app]`, then `docs` from Web gives `[Web, Tools, docs, api, web-app]`. A row without a
repository (`Folder not found`, `Not a repository`, `Git could not open it`) opens nothing: its
double-click and Enter do nothing, and `Open` is disabled with its reason (2.7).

### 2.4 Three columns become one

The dashboard spans the main area's three columns and neither side panel shows. In
`Views/MainWindow.axaml`, `LeftPanelView` (33) goes into a `Panel Grid.Column="0"
IsVisible="{Binding ActiveTab.ShowsLeftPanel}"`, as the right panels sit in panels (38-43), and
`LeftSplitter` (47-55) gets the same `IsVisible`; `CommitGraphView` (36) gets `Grid.Column="{Binding
GraphColumn}"`; the right panels and `RightSplitter` follow `ShowsDetailsPanel`, `ShowsStagePanel` and
`ShowsRightPanel`, false on a dashboard. The column definitions (260, `*`, 400), `PanelLayout.Fit`
(`Views/PanelLayout.cs:26-46`), its call in `MainWindow.FitPanels` (`MainWindow.axaml.cs:174`) and the
session's panel widths are untouched: the dashboard covers the columns as the resolver covers two
(D140), and a repo tab comes back with its panels as they were. Every other tab binds to today's
values, so no region of an earlier picture changes through the dashboard (compared by region, 17.7).

In `Views/Shell/CommitGraphView.axaml`, row 0 of `GraphAreaGrid` gets a third slot beside the welcome
page (20-22) and `GraphAreaContent`: `<Panel IsVisible="{Binding ShowsDashboard}"><workspaces:DashboardView
DataContext="{Binding Dashboard}" /></Panel>`. The activity log (rows 1 and 2) stays under it, fitted
by `FitActivityLog` as for a graph; `FocusGraphAreaContent` (`CommitGraphView.axaml.cs:191-213`),
used when the log closes with the focus inside it, focuses `DashboardList`.

| Window | Main area (height − 36 − 52 − 26) | Activity log closed | Open at its default 200 (+ 5 splitter) |
|---|---|---|---|
| 1100×700 | 1100×586 | header 36, column headers 28, list 522: 20 rows | 381: list 317, 12 rows |
| 1920×1080 | 1920×966 | header 36, column headers 28, list 902: 34 rows | 761: list 697, 26 rows |

Ten rows (260) are in view at both sizes, the log open or closed: "ten repos in mixed states" is one
picture at each size.

### 2.5 The header

`DashboardHeader`: 36 high, `VcChromeBackgroundBrush`, a 1-px `VcBorderBrush` bottom line as the
reflog page's (`Views/Pages/ReflogView.axaml`), a `DockPanel` with `Margin="8,0"`.

- **Left**: `IconWorkspace` 14×14 in `VcTextSecondaryBrush`, 6, the name (`DashboardTitle`, 13,
  SemiBold, primary, a `TrimmedTextBlock` at most 240 wide, whole in its tooltip), 10, the count
  (`DashboardCount`, secondary): `1 repository`, `{n} repositories`, `No repositories`.
- **Right**, from the right edge, 6 apart, each with `ToolTip.ShowOnDisabled="True"`: `DashboardMore`
  (`VcIconButton` 28×28, `Padding="7"`, `IconMore` 14×14, tooltip `More`, and the automation name
  `More`, its tooltip's text, as the page close buttons are named `Close`: an icon alone gives no
  name); then `VcActionButton`s with `Padding="10,3"` (as `ReflogSource`), an icon 13×13, 6, the
  label: `DashboardRefresh` (`IconRefresh`, `Refresh`, always enabled, tooltip `Read the repositories
  of {workspace} again ({key})`, Refresh's key, 6.1 row 4, as every keyed button's, D199),
  `DashboardPullAll` (`IconPull`, `Pull all`) and `DashboardFetchAll` (`IconFetch`, `Fetch all`) with
  the toolbar's states and tooltips (4.10), `DashboardAddRepository` (`IconPlus`, `Add repository…`,
  1.7, tooltip `Add a repository to {workspace}`, disabled while read-only, 1.5).
- **The run**, right-aligned 16 before `DashboardAddRepository`: `DashboardBulkText` (a
  `TrimmedTextBlock` in the run's tone, secondary, `VcWarningBrush` or `VcDangerBrush`, whole in its
  tooltip and in the status bar), 8, `DashboardBulkProgress` (80×4, the status bar's
  `OperationProgress` look, while a run runs), 8, `DashboardCancelAll` (`VcFlatButton` `Cancel all`,
  automation name `Cancel all`, tooltip `Cancel the rest of Fetch all` / `Cancel the rest of Pull
  all`, while a run runs until clicked, 4.5). Empty before the first run. Its texts are 4.9's
  (`Fetching 10 repositories… 4 done`, `Pulled 6 repositories; 1 failed; 2 skipped`).
- **A failed save** (1.3): while `IWorkspaceStore.SaveFailed`, a line below the header,
  `DashboardNotice` (a `TrimmedTextBlock` in `VcWarningBrush`, margin 8,4, whole in its tooltip), says
  `workspaces.json could not be saved, so changes to workspaces are lost when VisualCommit closes.`;
  the list moves down by its height; it hides when a save succeeds. Other notices of the store show on
  the welcome page only (13.1).
- **`DashboardMore`'s menu** (`DashboardMenus.ForHeader`, the menu layer, D86): header the
  workspace's name; `Add open tabs`; separator; `Rename workspace…`; `Delete workspace…`
  (destructive), as 1.5 to 1.7 define them. It keeps the header to four labelled buttons.

**Widths at 1100×700** (estimates at 12.5-px Inter, 0.8): inside 1084; the buttons about 460; the left
part about 300 with a 20-character name, 150 with the scenario's `Web`; the run part the rest less 16,
about 308 (458). While a run runs, the bar, `Cancel all` (about 70) and two gaps of 8 take 166, leaving
the text 142 (292): `Fetching 10 repositories… 4 done` (about 200) is cut with the long name, whole
with `Web`; after it, `Pulled 6 repositories; 1 failed; 2 skipped` (about 260) is whole. The text
keeps at least 120: in a narrower window the title gives way below its 240. The header never wraps.
At 1920×1080 the running text has about 960.

**An empty workspace**: below the header, a stack centred as the welcome page's (`DashboardEmpty`,
`MaxWidth="440"`, `Spacing="8"`): `{workspace} has no repositories yet.` (secondary, centred,
wrapping), then `Add repository…` (`DashboardEmptyAddRepository`) and `Add open tabs`
(`DashboardEmptyAddOpenTabs`), `VcActionButton`s side by side.

### 2.6 The rows

`DashboardColumnHeaders`: 28 high, `VcChromeBackgroundBrush`, a 1-px bottom line, headings in the
`heading` style (11, SemiBold, secondary) as the graph's (`Views/Graph/RepositoryGraphView.axaml:27-44`).
`DashboardList`: a `ListBox`, single selection, virtualised, `ScrollViewer.HorizontalScrollBarVisibility="Disabled"`,
styled by `ListBox.dashboardList`, a copy of phase 5's `pageList` in `DashboardView.axaml` (rows 26,
`VcRowHoverBrush` under the pointer, `VcSelectionBrush` selected, no focus adorner;
`ReflogView.axaml:19-50`), so the reflog and backups pages stay as they are. Ids: the root
`Dashboard` (named by the workspace); each item `DashboardRow` (named by the repository), its cells
`DashboardRowName`, `DashboardRowFolder`, `DashboardRowBranch`, `DashboardRowAheadBehind`,
`DashboardRowChanges`, `DashboardRowState`, `DashboardRowOperation`.

**Columns**: `DashboardColumns.Compute(width)` (`Views/Workspaces/DashboardColumns.cs`, pure), shared
by the headers and every row through `DashboardRowPanel` (`Views/Workspaces/DashboardRowPanel.cs`, an
attached `DashboardRowPanel.Column`), as `GraphColumns` is (D49). From the left: Repository 132,
Folder, Branch 150, Ahead / behind 130, Changes 220, State 180, Operation. With `rest = width − 812`:
when `rest ≥ 468`, Operation = `clamp(300 + (rest − 468) / 2, 300, 460)` and Folder = `rest −
Operation`; otherwise Folder is hidden and Operation = `max(rest, 180)`. A cell's text starts 8 into
its column and ends 8 before the next (`TextInset` 8).

| Column | Heading | 1100 | 1920 |
|---|---|---|---|
| Repository | `Repository` | 0–132 | 0–132 |
| Folder | `Folder` | hidden | 132–780 (648) |
| Branch | `Branch` | 132–282 | 780–930 |
| Ahead / behind | `Ahead / behind` | 282–412 | 930–1060 |
| Changes | `Changes` | 412–632 | 1060–1280 |
| State | `State` | 632–812 | 1280–1460 |
| Operation | `Operation` | 812–1100 (288) | 1460–1920 (460) |

Folder shows from 1280 wide (the default width): 300 and 168 there, 380 and 248 at 1440; at the
minimum of 1000 Operation has 188. Every cell is a `TrimmedTextBlock` (cut with "…", whole in its
tooltip) unless the table gives a tooltip. No "last fetched" column (0.1).

**What fits** (0.8's measure: Inter 12.5 without kerning, which moves these by 0.5 px or less; a
cell's room is its column less 16): every text of the scenario (17.1) is whole at 1100 and at 1920,
so no picture of part W shows a cut cell. The tightest: design-system's `1 staged, 2 unstaged, 1
untracked` 199.5 of 204 (any text of the three kinds with single digits is about 199 to 204);
infra's Pull all reason `HEAD is detached: check out a branch to pull` 267.0 of 272 at 1100 (4.2,
W7); `Detached at 09d320a` 129.6 of 134; `design-system` 87.9 of 116. Of 2.6's other texts,
`origin/main is gone` 111.8 of 114, `Rebase in progress (2 of 5)` 160.3 of 164 and the heading
`Ahead / behind` (11, SemiBold) 80.8 fit too.
Longer texts are cut with "…" and whole in the tooltip: four kinds of change, two-digit counts (`12
staged, 34 unstaged, 5 untracked` about 217), `Rebase in progress (12 of 15)` (171.9), long names,
branches and upstreams. `DashboardViewTests` pins this (2.12).

**Cells**, by the pure `DashboardTexts` (`ViewModels/Workspaces/DashboardTexts.cs`) from `RepoRefs`
and `WorkingTreeStatus`; `{b}` the current branch, `{up}` its upstream as the toolbar names it
(`ToolbarViewModel.UpstreamText`, `ViewModels/ToolbarViewModel.cs:237-250`), `{short}` 7 characters
of a SHA, `{path}` the saved folder:

| Cell | State | Text (colour) | Tooltip |
|---|---|---|---|
| Repository | always | the folder's name (`IGitRepository.Name`), primary | the folder (at 1100 the only place it shows) |
| Folder | always | the saved folder, secondary | when cut |
| Branch | a branch, unborn | `{b}` | when cut |
| | detached | `Detached at {short}` (the status bar's, `RepositoryViewModel.cs:1241-1246`) | |
| | a rebase naming its branch (`InProgressState.Branch`) | that `{b}` (git detaches HEAD while it rebases) | |
| Ahead / behind | ahead, behind or both | the left panel's marks (`LeftPanelView.axaml:143-154`): `IconArrowUp` and its count, 6, `IconArrowDown` and its count, each only above 0, secondary, 10×10, class `aheadBehind`; automation name `{a} ahead, {n} behind` / `{a} ahead` / `{n} behind` | `1 commit ahead of {up}` / `{a} commits ahead of {up}`; `1 commit behind {up}` / `{n} commits behind {up}`; `{a} commits ahead of and {n} behind {up}` (`1 commit ahead of and …`) |
| | level | `Up to date` (secondary) | `{b} is level with {up}` |
| | upstream gone | `{up} is gone` (`VcWarningBrush`) | `{up} no longer exists` (the toolbar's) |
| | no remote (tested before "no upstream", WD4) | `No remote` (secondary) | `This repository has no remotes` (`ToolbarViewModel.NoRemotesText`) |
| | no upstream | `No upstream` (secondary) | `{b} has no upstream` |
| | unborn | `No commits yet` (secondary) | |
| | detached, a rebase | empty | |
| Changes | nothing changed | `Clean` (secondary) | |
| | changes | the counts above 0, joined by `, `: `1 conflict` / `{c} conflicts`, `{s} staged`, `{u} unstaged` (without untracked and conflicted entries, WD5), `{t} untracked`; `VcDangerBrush` while a file conflicts, else primary | the whole text |
| State | first read: the row's own (2.10, also while its first folder check has not returned), or, for a row that follows a tab, until that tab's view model has its first read (`FirstRead`, 3.2) | `Reading…` (secondary) | |
| | in progress | `Merge in progress`, `Cherry-pick in progress`, `Revert in progress`, `Rebase in progress`, `Rebase in progress ({k} of {n})` when the rebase counts (`Done`, `Total`), `git am in progress` (`VcWarningBrush`) | the banner's (`ConflictBannerViewModel.ToolTipFor`) |
| | folder gone | `Folder not found` (`VcWarningBrush`) | `{path} does not exist` |
| | not a repository | `Not a repository` (`VcWarningBrush`) | `{path} is not a git repository`; inside another (1.10) `{path} is not a git repository of its own: it lies inside {outer}` |
| | git refused (dubious ownership, …), no usable git, a failed summary read (3.1), a followed tab's failed first read (its `ErrorText`) | `Git could not open it` (`VcWarningBrush`) | `Git could not open {path}: {git's first line}` (of standard error, or the exception's message) |
| Operation | `Ready` | empty | |
| | an operation of the user runs | `CompactText`: the running text and git's percentage, no stage (`Fetching from all remotes… 45%`, `Pulling origin/main into main…`), then `Cancel` | `CompactToolTip`: the status bar's whole text |
| | waits for auto-fetch | `Waiting for auto-fetch…` | |
| | the last result | the status bar's, in its tone (`Fetched from all remotes`; `Pull stopped: 1 file has conflicts`, warning; `Fetch failed`, danger), until the next operation starts (D95) | the whole text |
| | a bulk run holds it | in place of the above, 4.2's and 4.5's text in its tone (`Waiting to fetch…`, `Waiting to pull…`, the toolbar's reason for a row left out, `Fetch cancelled`) | the text |
| | a failure's dialog kept (4.6, D195) | the result, then `Details…` | the whole text |

The Ahead / behind cell tests "no remote" before "no upstream" (WD4): a repository without remotes
shows `No remote` (`This repository has no remotes`), so notes (17.1) reads as W2 expects; Pull's
reason keeps the toolbar's order (`main has no upstream: set one first`, 4.2), so the cell and the
reason name different things there. The unstaged count leaves out the untracked and conflicted
entries of `WorkingTreeStatus.Unstaged`, each counted by its own kind (WD5): design-system `1 staged,
2 unstaged, 1 untracked`, billing `1 conflict`, scratch with one new file `1 untracked`.
`DashboardTextsTests` pins both (2.12).

`Reading…` and the three folder states leave every cell but Repository, Folder and State empty.
`Cancel` (`DashboardRowCancel`, name `Cancel`, while `CanCancel`; its tooltip the status's
`CancelToolTip`, `OperationStatusViewModel.cs:114`, 219: `Stop git`, and `Stop git and the merge tool`
while the merge tool runs, as the status bar's button says for the same queue) and `Details…`
(`DashboardRowDetails`, name `Details`, opens 4.6's dialog) are `VcFlatButton`s, 11.5, 22 high, 6
after the text, never both.

`OperationStatusViewModel` (`ViewModels/Operations/OperationStatusViewModel.cs`) gains `CompactText`
(the running text and ` {p}%` without `progress.Stage`, the result, or empty for `Ready`),
`CompactTone` and `CompactToolTip` (the whole text `Update` builds before `Show`, 203-251), and, for
the row menu's `Cancel {title}` (2.7), `RunningTitle` (the `Title` of the operation of the user that
runs, else null) and `RunningIsCancellable` (it is a network operation or a local one with
`GitOperation.CanBeCancelled`, the merge tool: it could be cancelled, whether or not its cancel window
is still open; `CanCancel` says whether it can be now). None shows ssh's window-wide notice (`Show`,
253-264, puts it in place of `Text`), which would show in every row at once; the status bar keeps it.
With the stage and a bar the text would not fit 1100's 288 beside `Cancel` (`Fetching from all
remotes… 45%` is about 191), so a row has neither. The row's Fetch is the toolbar's Fetch of all
remotes (`OperationTexts.FetchAll`), hence its text.

A row keeps the values it last showed while its view model is replaced (3.6): it copies them on each
`PropertyChanged` of `Refs`, `Status` and `BranchText` instead of binding through the view model. It
stops listening to a view model when it leaves it (the tab closes, 3.6), when the row is removed or
its path replaced (1.10), and when its dashboard is disposed (2.10).

### 2.7 The row menu, selection and keys

**Selection**: single; none when a dashboard first shows; kept in the background, not saved. The
list takes the focus when the dashboard becomes active (`RequestFocus`, as phase 5's pages). After a
removal (1.10) no row is selected and the focus stays on the list (`DashboardList`), so a second
Delete, or a held one, removes nothing more, and Up or Down starts again from the first row; when the
last row goes, the focus moves to the empty state's `Add repository…` (`DashboardEmptyAddRepository`,
2.5).

**The row menu** (`DashboardMenus.ForRow`, `ViewModels/Menus/DashboardMenus.cs`, data as D86) opens at
once through `MenuLayerViewModel.Open` on a right-click (which selects the row), Shift+F10 or the
Menu key (D106). Header: the repository's name, its folder as tooltip. Only what can never apply is
left out (D77):

| Item | Does | Disabled when (reason) |
|---|---|---|
| `Open` | 2.3 | not read yet `Wait for {name} to be read` (never a row that follows a tab: Open activates that tab); a folder state: its State tooltip (2.6) |
| — | | |
| `Fetch` | the toolbar's Fetch (`RepositoryOperations.FetchAllAsync`: its title, dedupe key `fetch:*`, prompts asked); a failure's dialog opens at once (4.7) | `Open`'s reasons; the toolbar's (`ToolbarViewModel.Reasons`, 460-506: `This repository has no remotes`); in a running bulk run `Wait for Fetch all to finish` / `Wait for Pull all to finish` (4.7) |
| `Pull` | the toolbar's Pull (`RepositoryOperations.PullAsync`), recorded for undo (3.1); a failure's dialog at once (4.7) | `Open`'s; the toolbar's (`HEAD is detached: check out a branch to pull`, `{b} has no upstream: set one first`, `{up} no longer exists`, `Finish or abort the {kind} first`, …); in a running run as Fetch |
| `Cancel {title}` | the row's status's `CancelCommand` (`queue.CancelCurrent()`) | shown while an operation of the user runs that could be cancelled (`RunningIsCancellable`, 2.6: a network operation, or the merge tool), `{title}` its `RunningTitle`; disabled once it cannot be cancelled (`CanCancel` false: a pull past its fetch): `{title} can no longer be cancelled`. Left out while any other local operation runs (a checkout, a commit), which never could be, as the status bar then shows no Cancel |
| `Error details…` | 4.6's kept dialog | shown while one is kept |
| — | | |
| `Show in folder` | `IFileLauncher.RevealAsync(folder)` (`Services/IFileLauncher.cs:14`; `OperationTexts.MenuTexts.ShowInFolder`) | the folder is gone: `{path} does not exist` |
| `Copy path` | the folder as the platform writes it (`IClipboardService.SetTextAsync`; `OperationTexts.MenuTexts.CopyPath`) | never |
| — | | |
| `Move up`, `Move down` | 1.10 (Alt+Up, Alt+Down) | `It is the first repository` / `It is the last repository`; read-only |
| `Locate…` | 1.10 | shown only for `Folder not found`, `Not a repository`, `Git could not open it`; read-only |
| `Remove from workspace` | 1.10 (Delete, on macOS also Cmd+Backspace; no question); cancels the row's read (2.10) and releases the row's summary-mode view model (3.4) | read-only; while the row belongs to a running bulk run (in line, reading or running) `Wait for Fetch all to finish` / `Wait for Pull all to finish` (1.10, 4.7) |

"Read-only" is 1.5's `EditReason` (`workspaces.json was made by a newer version of VisualCommit`, or
`workspaces.json could not be read`, 1.3); a disabled item's key does nothing. The menu layer's log line (D106) names the dashboard: the window's
`repositoryName` callback (`MainWindowViewModel.cs:62-65`) becomes `ActiveTab?.Repository?.Name ??
ActiveTab?.Dashboard?.Name` (`Web: Context menu opened: web-app by pointer`).

**Keys**: Up, Down, Home, End, PageUp, PageDown move the selection (the `ListBox`'s); Enter opens;
Delete removes and Alt+Up, Alt+Down move the row (1.10); Shift+F10 and Menu open its menu; the
window's keys on a dashboard are 6.9's. On macOS Cmd+Backspace removes too, listed first (Finder's
Move to Trash; Cmd, because removal asks no question): the key a Mac keyboard labels "delete" sends
Backspace, and forward Delete there is Fn+delete. Its text is Apple's: `Cmd+Delete` for Cmd+Backspace
and `Forward Delete` for Delete (6.4). On a Mac keyboard Shift+F10 needs Fn unless the function keys
are set to act as standard keys, and Home, End, PageUp and PageDown are Fn with the arrows (6.1). Tab goes from the list to
the header's buttons and back; `Cancel` and `Details…` are reached by the pointer and by `Cancel
{title}` and `Error details…`.
**Pointer**: a press selects, a double-click opens; `Cancel` and `Details…` take their press without
selecting the row again or starting a double-click.

### 2.8 The toolbar and the status bar on a dashboard tab

Both keep their bindings (`ActiveTab.Toolbar.*`, `ActiveTab.OperationStatus.*`). Section 4's
`DashboardBulkViewModel` (`DashboardViewModel.Bulk`) gives a dashboard tab its values: the toolbar is
`ToolbarViewModel.ForDashboard(IDashboardToolbar)` (a static factory, 4.10), Fetch and Pull running Fetch all and Pull all with 4.10's
states, tooltips and reasons, the other buttons disabled as on an empty tab; the operation part shows
the run (4.9), `RepoTabViewModel.OperationStatus` typed `IStatusBarOperation`. The header's `Fetch
all` and `Pull all` are the same commands. **The branch part is this section's**: on a dashboard tab it
shows `BranchText`, the workspace's name (2.1), after `IconWorkspace` (id `StatusWorkspaceIcon`, in
`Views/Shell/StatusBarView.axaml`, shown only on a dashboard tab); a repo tab's status bar is
unchanged.

`IStatusBarOperation` carries `Waiting` beside the bound members: phase 3's check D reads
`ActiveTab.OperationStatus.Waiting` (`Phase3Checks.D.cs:190`, `:252`). `ShellDriver`'s
`StatusTextRecorder` (`tests/VisualCommit.Testing/Headless/ShellDriver.Checks.cs:404-410`) takes the
interface; the harness and checks reading `Text`, `Tone`, `IsRunning`, `Percent` and `Waiting`
through `ActiveTab.OperationStatus` compile unchanged.

### 2.9 The activity log on a dashboard

The panel follows the selected row's worktree: `ActivityLogViewModel.FollowTab`
(`ViewModels/Panels/ActivityLogViewModel.cs:497-511`), given a dashboard tab, listens to
`DashboardViewModel.SelectedRepository` (the selected row's view model, its own or a tab's; else null)
and shows `ActivityLogSource.Of(…)` (26-33) or nothing. Records are keyed by worktree
(`WorktreeKey.Of(gitDirectory)`), so it shows every operation on the worktree whichever queue ran it;
its header names the repository. With nothing to follow the panel hides and the toggle is disabled
(`CanToggle`, 231). While `Current` is null on a dashboard, `ToggleToolTip` (234) says the dashboard's
`ActivityLogReason` (new, raised with `SelectedRepository`), the first that holds:

1. the workspace has no rows: `{workspace} has no repositories` (Fetch all's reason, 4.10);
2. no row is selected: `Select a repository to see its activity` (`SelectRepositoryText`);
3. the selected row is not read yet, or follows a restored tab that has not opened: `Wait for {name}
   to be read` (`Open`'s, 2.7);
4. the selected row is in a folder state: its State tooltip, as `Open`'s reason (`{path} does not
   exist`, `{path} is not a git repository` or its "inside {outer}" form, `Git could not open {path}:
   {git's first line}`).

A row whose summary read failed has its view model, so the toggle shows that worktree's records.
Elsewhere, the empty tab included, the toggle says 6.8's texts with the key, `Show every git command
the app has run ({key})` and `Hide the activity log ({key})`. The open state stays the window's: the
panel shows again once a row with a view model is selected.

### 2.10 When rows are read, and a dashboard that is not active

No row has a file watcher. A row whose worktree is open in a tab follows that tab live (3.5) and is
never read by the dashboard; until that tab's view model has its first read (`FirstRead`, 3.2) the
row counts as not read yet (`Reading…`, 2.6; a bulk run waits for it, 4.2). The others are read (1)
when the dashboard becomes the active tab (`MainWindowViewModel.Activate`, 402-414, calls
`DashboardViewModel.OnBecameActive()`; not while the window restores its tabs); (2) when the window
is activated with the dashboard active: a new `Window.Activated` handler in `MainWindow.axaml.cs`
calls `MainWindowViewModel.OnWindowActivated()`, which calls `ActiveTab.Dashboard?.OnWindowActivated()`
(Q4: a change made in a terminal shows when the user comes back); (3) on F5 and the header's Refresh
(`DashboardViewModel.RefreshCommand`); (4) after the row's own operation, by the queue's refresh
(`RefreshScope`, D71), also in the background; (5) by a running bulk run, for the rows it waits for:
a first read (4.2), and the read of a row that had no view model, or a closed one, at its turn (4.3):
those reads belong to the run and go on, also in the background; (6) when
`IWorkspaceStore.Changed` brings new paths into an active dashboard's workspace (`Append`: Add
repository…, the scan's checklist and Add open tabs, 1.7; `Replace`: Locate…, 1.10): the new rows are
read at once, and through step 3 a row whose folder is open in a tab follows that tab without a git
call. The add commands are offered only on an active dashboard (1.5), so an added row rarely waits;
in the background it waits for trigger 1. "Active" is the window's `ActiveTab`, not the tab's
`IsActive`, which `CloseTab` leaves set on a closed tab. Triggers 1 and 2 read only rows never read or
last read 10 s ago or more (`TabServices.ReadClock`, a `TimeProvider` that is `TabServices.Clock`
unless the harness gives one, 17.2; a read counts when it ended, failed or not); trigger 3 reads every
row without a tab. A row whose view model went away (3.6) counts as never read. At most 4 reads run at
once per dashboard (`SemaphoreSlim(4)`, held only around a read's git calls); a call during a read
folds into one more run (`RefreshAsync`, 334-352).

**A row's read** (`DashboardRowViewModel.ReadAsync`, on the UI thread between its awaits; its folder
checks and its opening run on the thread pool, so a share that does not answer, 0.1, never holds the
window):

1. a row that follows a tab: nothing;
2. a row with its own view model: if its folder or its git folder (`IGitRepository.GitDirectory`) is
   gone (one `await Task.Run(() => directoryExists(folder) && directoryExists(gitDirectory))`, no
   process, before the read takes a slot), the view model is released (3.4) and the row is read as in
   step 3, so a folder whose `.git` was deleted inside another repository is never read as that one
   (0.1, 0.8); otherwise `RefreshAsync()` (3.2);
3. a row without one: a tab registered for the folder (`OpenRepositories.TabForFolder`) → it follows
   it. Else the folder is checked as in step 2 (gone: `Folder not found`, no slot, no process), then
   `WorkspaceFolderProbe.ProbeAsync(TabServices.Repositories, folder, token)` (1.10, its own check on
   the thread pool too): `FolderNotFound`, `NotARepository` (also inside another repository, whose
   outer repository the probe drops unread) and `NotOpened` give 2.6's states; an exception it does
   not sort gives `Git could not open it`, logged at Error. `Ready` gives the repository: a tab
   registered for its key → the row follows it; else `OpenRepositories.AcquireSummaryAsync(repository,
   dashboard, token)` (3.4) gives the summary-mode view model, started in summary mode when new,
   refreshed when another dashboard holds it.

`directoryExists` is the row's and the probe's seam (`Func<string, bool>`, `Directory.Exists` unless a
test gives one). `GitRepositoryProvider.OpenAsync` and `InitAsync` (`Services/GitRepositoryProvider.cs:28-38`)
run the whole open on the thread pool (`await Task.Run(() => GitRepository.OpenAsync(runner, path, ct,
_log), ct)`), so `GitRepository.OpenAsync`'s own `Directory.Exists` (`GitRepository.cs:80-83`) leaves
the UI thread too, for the restored tabs and the welcome page's Open as well (D59's promise; no
earlier check sees a difference).

**A check that has not returned holds nothing.** A row has at most one outstanding: a trigger that
finds one starts none. It holds no read slot; the row shows `Reading…` on its first read and its last
values otherwise; the pass ends without it and logs its line with the rows that finished; a call
folded into one more run, F5, `InitializeAsync`'s await and a bulk run's other rows do not wait for
it, and a run decides the row when the check returns (4.2). Then the row's read goes on alone.

**`token`** is the row's: a `CancellationTokenSource` linked to the dashboard's lifetime
(`DashboardViewModel._lifetime`, as `RepositoryViewModel` has one), cancelled when the row is removed
(1.10, 2.7) or its path replaced by Locate… (the new path is read with a new one, trigger 6). After
each await the read applies its result only while
the dashboard is not closed and the row is still in `Rows` with the same path (as 3.3's read generation
for a promoted view model); otherwise it releases what it acquired (`ReleaseSummary(vm, dashboard)`),
so a removed row or a closed dashboard keeps no summary-mode view model and starts no git call
afterwards.

**Disposing**: `DashboardViewModel.Dispose` (its tab closed, 2.1, or `CloseAll`, 3.7) cancels
`_lifetime`, sets `IsClosed`, cancels its run as Cancel all does (4.12), releases its rows'
summary-mode view models (3.4) and unsubscribes from `IWorkspaceStore.Changed`,
`OpenRepositories.Changed` and every followed tab view model's `PropertyChanged`. Removing a row or
replacing its path cancels that row's token, unsubscribes it the same way and releases its
summary-mode view model. A closed dashboard
reacts to none of those events. A row a running bulk run holds cannot be removed (2.7, 4.7), and
`Locate…` is offered only on a row in a folder state, which no run holds, so no run loses a row it
decided.

A row keeps its repository's `WorkingDirectory`, which `DashboardViewModel.OpenedWorkingDirectoryOf(savedPath)`
gives to 1.2's `WorkspaceMembership`. After a pass of triggers 1 to 3 or 6 the dashboard logs at Info
`{workspace}: read 1 repository in {ms} ms` / `{workspace}: read {n} repositories in {ms} ms`; a
summary read logs at Debug `{name}: summary read in {ms} ms`. A trigger that finds no row to read
starts no pass and logs no line (WD35): a row to read is one without a tab that, for triggers 1 and
2, was never read or was last read 10 s ago or more; a row that follows a tab is never read and never
counted in a pass's `{n}`. A request that folds into "one more pass" while a pass runs judges the 10 s
as of the moment it was made, not when it runs: a row the running pass read after the request is
fresh, so the window's activation at start, which folds into the start-up read, reads nothing twice
however long the first pass takes (the lead's ruling of 2026-10-23, after the helpers' smoke test saw
`read 10` in 16 s followed by `read 3` under load). So W1's `Add open tabs` (both added rows follow tabs) and W8's activations
before the read clock moves log no pass line, and `WaitForDashboardReadAsync` is ended only by a pass
that read; `DashboardRefreshTests` pins it (2.12). `ShellDriver.Phase6.Dashboard.cs`
(17.2) has `WaitForDashboardReadAsync()` (the pass's line, no row `Reading…`) and `ActivateWindow()`
(raises `Activated`, or calls `OnWindowActivated()` if headless `Window.Activate()` does not: the
builder checks which).

**At start** a restored dashboard reads nothing until it is active; a restored active one is read by
`InitializeAsync` (162-189), which awaits its first pass after git is found, beside the restored repo
tabs' openings, so `AppSession.Initialization` stays the harnesses' "ready" point. **In the
background** a dashboard starts no read of its own, except the reads a running bulk run waits for
(trigger 5, 4.2, 4.3), which go on: its rows keep their values, rows that follow tabs update as those tabs
do, its view models' operations go on with their refreshes (trigger 4), a bulk run goes on, and
workspace changes (section 1) apply at once, an added row waiting unread for trigger 1. So a
restored dashboard in the background never slows a restored repo tab's first graph (Q1, risk 10).

### 2.11 The session

A dashboard's place in `session.json`, its restore and its saving are 1.11's (tested by 1.12).

### 2.12 Tests

`DashboardColumnsTests` (App.Tests, pure: 2.6's table at 1100 and 1920; Folder hidden at 1279, shown
at 1280 with 300 and 168; 1440 with 380 and 248; Operation 188 at 1000; no gaps);
`DashboardTextsTests` (pure: every row of 2.6's table, singular and plural, a local upstream, a file
staged and changed again counted twice, `Rebase in progress (2 of 5)`, the header's three counts;
"no remote" before "no upstream" and the unstaged count without untracked and conflicted entries, WD4
and WD5);
`DashboardMenusTests` (pure: `ForRow` in every state of 2.7's table, read-only, at the top and the
bottom; `Cancel {title}` enabled for a network operation in its cancel window, disabled with `{title}
can no longer be cancelled` past it, enabled for the merge tool, left out for a checkout; `Remove from
workspace` disabled with `Wait for Fetch all to finish` / `Wait for Pull all to finish` for a row in
line, reading or running (4.7), enabled for a skipped or ended one; `ForHeader`);
`DashboardTabTests` (`MainWindowViewModel` with fakes: 2.1 to 2.3 with the three
callers of `ShowWorkspace`, a busy `from` tab left alone, 2.3's example, a pending tab matched by
`WorkspacePaths.Same`, a row without a repository opening nothing, closing a dashboard keeping its
tabs, `CanClose` on a lone dashboard, a deleted workspace's dashboard closed, `Title` and `BranchText`
after a rename; 2.8's bindings: on a dashboard tab `Toolbar` is the
`ToolbarViewModel.ForDashboard(IDashboardToolbar)` over its `DashboardBulkViewModel` and `OperationStatus` that
view model, on a repo tab the repository's); `DashboardRefreshTests` (`ManualTimeProvider`,
`tests/VisualCommit.Testing/ManualTimeProvider.cs`, as `TabServices.ReadClock`; a counting
`FakeRepositoryBase`: each trigger of 2.10 with its 10 s rule; no call in the background but a running
run's (trigger 5, which goes on there), and none for a row following a tab; a row following a tab
`Reading…` until that tab's `FirstRead`; trigger 6: a row appended while the dashboard is active read
at once with no other trigger and the pass line logged, one appended in the background not read, an
Add open tabs row following its tab with no git call, a Locate replacement read; a trigger that
finds no row to read starting no pass and logging no line (trigger 1 within 10 s, trigger 6 whose
rows all follow tabs, WD35); at most 4 reads at
once, by a gate in the fake; a deleted git folder released and probed again; the probe's states,
`Folder not found` without a process and no call on an outer repository; a `directoryExists` fake held
on a gate: never called on the UI thread (`Dispatcher.UIThread.CheckAccess()` false inside it), the
other rows read and the pass line logged, F5 and `InitializeAsync` completing while it is held, a
second activation starting no second call, the release giving `Folder not found`; with the probe held
by the fake's gate, closing the dashboard, removing the row or a Locate replacing its path leaving no
summary-mode view model and no git call afterwards; a closed dashboard reacting to none of
`IWorkspaceStore.Changed`, `OpenRepositories.Changed` and a followed tab's `PropertyChanged`; a
restored active dashboard read before `InitializeAsync` completes, a background one not);
`OperationStatusViewModelTests` additions (`CompactText`, `CompactTone`, `CompactToolTip`: no stage, no
notice, empty for `Ready`; `RunningTitle` and `RunningIsCancellable` for a fetch in and past its cancel
window, the merge tool and a checkout; `StatusBarTests` unchanged by them, its toggle's tooltip
changing only for 6.8's key, 16.1); `DashboardActivityLogTests` (2.9: the selected row followed, a
dashboard pull's record in the tab opened afterwards, hidden with no row; the toggle's tooltip, one
case per reason of 2.9 in their order, and reason 3 turning into an enabled toggle when the row's
summary-mode view model arrives); `DashboardViewTests` (headless: the header and the columns placed as
`DashboardColumns` says at 1100 and 1920; no cell of W2's ten rows cut (`TrimmedTextBlock.IsTrimmed`
false) at 1100 and at 1920, nor infra's Pull all reason in its Operation cell at 1100; at 1100 with a
long name during a run the run's text at least 120 and the header on one line; ten rows in view with
the log open; Enter, a double-click, Delete (the row gone, no row selected, the list keeping the focus,
a second Delete changing nothing; Delete on the only row leaving the focus on
`DashboardEmptyAddRepository`; Cmd+Backspace likewise with macOS's keys, 6.4), Shift+F10; a
row's Cancel's tooltip `Stop git`, and `Stop git and the merge tool` while the merge tool runs;
`DashboardMore`'s automation name `More`; the focus in the list on activation; the side panels and
splitters hidden and back with their widths on a repo tab; 2.8's branch part, `StatusWorkspaceIcon`
and the workspace's name, only on a dashboard; the empty state; `DashboardNotice` shown in the
warning tone while the store's `SaveFailed`, hidden otherwise). The session's cases are 1.12's (`DashboardSessionTests`,
`JsonSessionStoreTests`' additions). 4.10's states and reasons are 4.13's `DashboardToolbarTests`, and
the status bar's run part (4.9) 4.13's `DashboardBulkViewModelTests`. Part W of the walk-through
(17.4) pictures the ten-repository dashboard (W2 at 1100×700 dark with a row's menu, W3 at 1920×1080
light), a repository opened from a row, the log following a row, the empty workspace and a restart
restoring the dashboard (real window too); the bulk run's pictures are section 4's.

## 3. The repositories behind the rows

A row's Fetch and Pull run through a `RepositoryViewModel`'s queue and `RepositoryOperations`, with
the toolbar's titles, Cancel, error dialog and undo recording (the plan's phase 6 item, D81); anything
else would touch all 12 partial files of `RepositoryOperations`. So a row without a tab gets an
ordinary `RepositoryViewModel` in a light mode, summary mode; a row whose worktree is open in a tab
uses that tab's.

### 3.1 Summary mode

`RepositoryViewModel.ForSummary(IGitRepository repository, TabServices services)` calls the usual
constructor (`ViewModels/RepositoryViewModel.cs:76-128`, no stopwatch) and sets the new `IsSummary` (true until 3.3). The
constructor does no I/O, so the view model has its queue (97-108; `_watcher?.Pause()` pauses nothing)
refreshed by `RefreshScope`, `OperationStatus` (109), `Operations` (110), `Toolbar` (111, its states
and reasons from `Refs` and `Status`), `UndoHistory` (`TabServices.UndoHistories.For(GitDirectory,
CommonDirectory)`, 115, shared with any tab on the worktree) and `BackupsToKeep`
(`RepositoryViewModel.GraphArea.cs:138-150`): Fetch, Pull and their undo recording run unchanged.

**`StartAsync` in summary mode** (253-325 in full mode): (1) reads `ReadRefsAsync` and
`ReadStatusAsync` side by side with the view model's lifetime token; (2) applies the refs:
`_fingerprint`, `_headSha`, `BranchText`, `Refs` (1237-1251 without `LeftPanel.Update`,
`InteractiveRebase` and `Page`, which are null); (3) applies the status: `Status`, then
`OnStatusApplied` (`RepositoryViewModel.InProgress.cs:117-124`: the working-changes row's text,
`Operations.NoteStatus`, `Operations.NoteUndoState`, which drops a pending undo entry a terminal
ended and updates Undo's state, `Banner.Show`), nothing else of `ApplyStatus` (944-968), and
completes `FirstRead` (3.2); (4) logs at Debug `{name}: summary read in {ms} ms`; (5) on a failure
sets `ErrorText` and logs, as in full mode (319-324), completes `FirstRead`, and the row shows `Git
could not open it` (2.6). Summary mode never sets `_started` (60): only the full `StartAsync` sets it,
at its end (308), and its one reader, `OnRepositoryChanged`, is never called in summary mode (3.2), so
a promoted view model starts its full load with `_started` false (3.3).

| Skipped (with what it causes) | Why |
|---|---|
| The watcher (`CreateWatcher`, `Changed`, `Start` on the thread pool, 261-264) | no watchers on rows (2.10); on Linux a watcher walks every folder (risk 13) |
| Q1's lines `opened after` (269), `refs read after` (292), `first commits from git after` (1197), `loaded {n} commits` (1234), `first graph rows drawn after` (497), `working tree: {s} staged, {u} unstaged` (953) | the real-window pass counts them per repository name (`Phase1RealWindowPass`: `WaitForLog(…, "graph: loaded 13 commits", occurrences: 2)`, `graph: working tree:`) |
| `LoadSessionFilter` (`RepositoryViewModel.Filters.cs:76-84`), `CorrectFilters` (92-103) | they write the session (`Touch` counts an opening and keeps the filter from the bound, `Write` drops names); a read is no opening. `Filters` stays null |
| `LoadAsync`, `Graph`, `SwapGraph` (D47's history load) | no graph (D188); `Graph` stays `CommitGraphData.Empty` |
| `OnFirstLoadCompleted` (`.GraphArea.cs:291`), the backups' clean-up at opening | it belongs to an opening and runs at the promotion's first load (3.3); the clean-up after an operation that saved a backup still runs inside it (`RepositoryOperations.Run.cs:328-331`) |
| `LeftPanel.Update`, `Changes.Update`, the scroll offset, `FollowStatusInGraphArea` (the resolver, `ReadMergeToolAsync`: a git call while files conflict) | nothing shows them; promotion reads and applies everything again |
| The handler of `TabServices.GitSettingsChanged` (8.11: `ReadMergeToolAsync` while files conflict) | as `FollowStatusInGraphArea`: nothing shows the merge tool; promotion's full `StartAsync` reads it |

### 3.2 What each member does in summary mode

- `RefreshAsync` (334-352): `RunRefreshesAsync` (379-426) reads the refs and, side by side,
  `RefreshStatusAsync` (359-377), applies both as `StartAsync` does and logs `summary read in {ms}
  ms`, never calling `CorrectFilters`, `LoadAsync` or `SwapGraph`; calls during a run fold into one
  more, as now. A fetch's `RefreshScope.Refs` costs one refs and one status read, not a history.
  `ReloadDiffAsync` does nothing; `OnRepositoryChanged` (1163-1177) is never called.
- `Queue`, `Operations`, `OperationStatus`, `Toolbar`, `UndoHistory`, `BackupsToKeep`, `IsClosed`,
  `Name`, `Path` and `Repository` work as in full mode; `Refs`, `Status`, `BranchText`, `ErrorText`
  and `Banner` are set by the reads; `Graph`, `LeftPanel`, `Details`, `Changes`, `Diff`, `Page`,
  `Resolver`, `InteractiveRebase`, `Selection`, `Filters` and the menus stay empty or null, and the
  dashboard calls none.
- Fetch and pull read only `Name`, `Path` (lock files), `Repository`, `Refs.CurrentBranch`, `Status`
  (the undo recording reads `Status.InProgress.IsActive`, `RepositoryOperations.Undo.cs:653`; the
  first read ends before a row offers an operation), `IsClosed`, `UndoHistory`, `BackupsToKeep`,
  `Toolbar.UpdateUndoStates()`, and `Selection` only when a rebase's selection follows its branch
  (`FinishAsync`, `RepositoryOperations.Rebase.cs:905-921`), which a pull never sets; Pull's
  `FinishAsync(…, new SelectionAfter())` changes nothing without a graph.
- `FirstRead` (new, in both modes): a `Task` that completes once `StartAsync` has applied both the
  refs and the status (full mode: after `ShowStatusAsync`'s `ApplyStatus`, 302, since `ApplyRefs`, 299,
  comes first and `Status` is `WorkingTreeStatus.Clean` until then; summary mode: step 3 of 3.1), or
  has failed (`ErrorText` set, 319-324); `HasFirstRead` is raised in `PropertyChanged` when it does. A
  summary read that a promotion discards (3.3) does not complete it; the full start then does. A row
  that follows a tab counts as not read yet until it completes (2.6, 2.10), and a bulk run waits for it
  (4.2), so no run decides a row from a `Refs` still null or a `Status` not yet read.
- `GitSettingsChanged`'s handler (8.11) returns at once while `IsSummary`; the subscription is made in
  the constructor and removed in `Dispose`, as in full mode.
- `TimeSinceOpening` (`.InProgress.cs:38`) counts from creation and is not read (auto-fetch looks only
  at the active tab's repository, D85). `Dispose` (570-590) applies D81's close rules: what waits is
  dropped, a running network operation cancelled, a running local one finishes.

### 3.3 Promotion: a tab adopts the summary-mode view model

A tab that opens a worktree whose summary-mode view model the registry holds takes that object, so a
dashboard and the tab opened from it never have two queues on one worktree (D81 stays literal), and
a running operation (a dashboard's pull) goes on in the tab, its result in the tab's status bar.
`RepoTabViewModel.ShowAsync` (`ViewModels/RepoTabViewModel.cs:184-212`), once the provider opened the
repository, first awaits `TabServices.OpenRepositories.WhenIdleAsync(key)` (3.4), `key` being
`WorktreeKey.Of(repository.GitDirectory)`: while the queue of a closed tab or of a released summary
still runs on the worktree (a dashboard closed during Pull all, its pull past the fetch), the tab keeps
`IsOpening` true (no welcome page) and shows `Waiting for {title} to finish…` (`TabWaitingText`,
secondary, centred in the graph area as the welcome page's stack; `{title}` that queue's running
operation's, 14.13's text), and it returns without opening if it is disposed meanwhile. This closes
the same gap for a closed tab reopened while its merge runs, a deliberate change for earlier phases
that no earlier check covers (16.1). Then it asks `OpenRepositories.TryAdopt(key)`; with a view model
returned it uses it instead of `new RepositoryViewModel(…)` (195), drops the freshly opened
`IGitRepository` (the adopted one points at the same folders), calls `opened.Promote(sinceOpening)`,
registers it, and goes on as today (`PropertyChanged`, `Repository = opened`, `Title`, the recent list,
`RepositoryOpened`, `await opened.StartAsync()`).

`Promote(Stopwatch sinceOpening)` (no I/O) sets `IsSummary` false, sets `_promoted`
(`Debug.Assert(!_started)`: summary mode never sets `_started`, 3.1) and makes `_sinceOpening` (42, no
longer `readonly`) the tab's stopwatch, so Q1's lines and auto-fetch's first interval
(`AutoFetchScheduler.Track`, `ViewModels/Operations/AutoFetchScheduler.cs:241-253`, from
`TimeSinceOpening`) count from the user's open, not from the dashboard's read (which would make an
auto-fetch due at once). A summary read still running applies nothing when it ends (a read
generation counter) and leaves `FirstRead` to the full start (3.2). `StartAsync` then runs in full
mode: watcher, Q1 lines, refs, status, the session's filter, history, clean-up at opening.

**No second history during the first load.** `RefreshAsync`'s first check is `if (_promoted) {
_changedWhileStarting = true; return Task.CompletedTask; }`, so a refresh the queue asks for (the
adopted view model's operation ends) and a watcher's git-folder event are put off, where
`RunRefreshesAsync` (397-409) would load a second history beside the first; a working-tree event is
put off by `_started`, as today (`OnRepositoryChanged`, 1170-1173). `StartAsync` clears `_promoted` where it sets `_started`
(308) and refreshes once at its end (310-313). Only a promoted view model does this: a tab opened as
today behaves as today.

**The watcher's pause belongs to the view model** (D52, D71). The queue pauses the watcher once, as
an operation starts (`OperationQueue.cs:547`, `using (_pauseWatcher())`), and in summary mode
`_watcher?.Pause()` pauses nothing, so a watcher that a promotion creates while the adopted pull still
runs would refresh the tab on every burst of it: a detached HEAD and the rebase banner shown halfway,
a history reloaded mid-pull. So the queue's hook becomes `PauseWatcher()`: under a lock it counts the
operation's pause (`_operationPauses`), takes `_watcherPause = _watcher?.Pause()` on the first, and
returns a handle that disposes `_watcherPause` when the count is back at 0. When `StartAsync` sets
`_watcher` while the count is above 0, it takes `watcher.Pause()` under the same lock, before `Start`
runs on the thread pool. A watcher created in the middle of the adopted operation is then paused until
that operation's run ends, when the queue's own pause would have ended, before the queue's one refresh
(which `Finished` follows, `OperationQueue.cs:494-509`); later operations pause it as in a tab opened
directly.

### 3.4 The registry: `OpenRepositories`

`TabServices.OpenRepositories` (`ViewModels/Workspaces/OpenRepositories.cs`), keyed by `WorktreeKey`
(`src/VisualCommit.Core/Git/WorktreeKey.cs`: the worktree's own git folder, compared as the platform
compares paths), is used on the UI thread only; its calls are synchronous between awaits, so an
adoption and a row's acquisition cannot interleave.

| Member | What it does |
|---|---|
| `RegisterTab(RepositoryViewModel)`, `UnregisterTab(…)` | a tab's full view model, by key and working folder, in registration order: `ShowAsync` registers what it makes or adopts; replacing it (199-203) and the tab's `Dispose` (214-229) unregister it, keeping `Queue.WhenIdleAsync()` for the key until it completes |
| `TabFor(string key)`, `TabForFolder(string folder)` | the first registered tab view model still open for the worktree, or null; the folder lookup (`WorkspacePaths.Same`, 1.2) finds a tab before the row opens its folder |
| `WhenIdleAsync(string key)` | the key's kept idle task, whether `UnregisterTab` kept it (a closed tab) or `ReleaseSummary` did (a released summary), else a completed task: `ShowAsync` (3.3) and `AcquireSummaryAsync` await it, so no tab and no summary-mode view model is made on a worktree while a closed one's queue runs |
| `TryAdopt(string key)` | the key's summary-mode view model, removed with its holders (3.3), or null |
| `AcquireSummaryAsync(IGitRepository repository, object holder, CancellationToken holderLifetime)` | awaits `WhenIdleAsync(key)`, looks for a tab again (the caller then follows it), then returns the key's summary-mode view model, made by `ForSummary` when there is none, with `holder` (a `DashboardViewModel`) added: two dashboards share one view model and one queue. It returns null, making and keeping nothing, when after its wait `holderLifetime` is cancelled (the dashboard closed, the row removed or its path replaced, 2.10) or `DisposeAll` has run |
| `ReleaseSummary(RepositoryViewModel, object holder)` | removes the holder; with none left it removes the view model from `Summaries` and disposes it (3.2, D81: what waits is dropped, a running network operation cancelled, a running local one finishes); if `Queue.Current` is then an operation of the user, not a background one, it adds the view model to `Closing`, keeps `Queue.WhenIdleAsync()` for the key, and removes both once that completes, as `KeepUntilIdleAsync` does for a closed tab (`MainWindowViewModel.cs:328-339`) |
| `Summaries`, `Closing` | the view models held, and those released while running, already disposed (3.7) |
| `NetworkQueueOn(string commonKey, IOperationQueue? except)` | the first queue of a view model it holds (a tab's, a summary's, one in `Closing`) on that common git folder (`WorktreeKey.Of(CommonDirectory)`), other than `except`, whose `Current` is a network operation (`OperationSnapshot.Kind == OperationKind.Network`, background ones included), or null; read by `BulkSlots` and auto-fetch (4.3) |
| `Changed` (the key) | a tab registered or unregistered, a summary adopted: rows follow (3.5, 3.6); it starts no read |
| `DisposeAll()` | disposes every view model in `Summaries` (those in `Closing` are disposed already) and refuses every later acquisition (3.7) |

### 3.5 Rows that use a tab's view model

A row whose worktree is open in tabs uses the view model of the first to register (`TabFor`), not
the leftmost, so a tab inserted later does not take it over (2.3's leftmost tab decides only what is
shown). It does not own it (amends D45: a tab owns its view model; a dashboard owns its summary-mode
view models and uses tabs' without owning them): its cells follow the tab's live `Refs`, `Status` and
`BranchText`, kept by the tab's watcher (D52, D71), and the dashboard never reads it (2.10); its Fetch
and Pull, and section 4's for it, run in the tab's queue, so the tab's status bar shows them,
auto-fetch's tracking sees them (`AutoFetchScheduler`'s `Finished`), D123's dedupe merges them with
the tab's own and the row's Cancel cancels the tab's running operation; it never disposes it, and
its holder count leaves it out. An adopted view model's rows go on using the same object. Two tabs on
one worktree (possible from the welcome page) stay as they are, two view models and queues and one
undo history (phase 5 design 15.9): the rows use the first, then the other.

### 3.6 When the last tab on a row's worktree closes

The tab's close follows D81 (`CloseTab`, `MainWindowViewModel.cs:219-249`): its view model is disposed,
a running network operation cancelled (a dashboard's fetch through it ends `Fetch cancelled`), a
running local one finishes while the window waits (`KeepUntilIdleAsync`, 328-339). Another tab on
the worktree, if one remains, serves the row at once. If none does, the row keeps its values (2.6),
its Operation cell empties and it counts as never read. `OpenRepositories.Changed` starts no read by
itself: the row's next read is one of 2.10's triggers (closing a tab often activates the dashboard,
trigger 1), and it acquires a new summary-mode view model only once the closed one's queue is idle
(`WhenIdleAsync`, 3.4). So no tab and no summary-mode view model is made on a worktree while a closed
one's queue runs (3.3); two tabs opened side by side on one worktree keep their two queues, as phase 5
accepted (3.5).

### 3.7 Closing the window

`MainWindowViewModel.TryClose` (293-325), `FinishThenCloseAsync` (353-392) and `CloseAll` (256-277)
include the dashboards' view models:

- `TryClose` lists `Tabs`' repositories, `_closedButRunning`, `OpenRepositories.Summaries` and
  `.Closing`, each view model once, and waits while one runs an operation of the user (a background
  one does not hold the window). When it waits it first calls every bulk run's `StopStarting()`
  (4.12), then `DropWaiting` (316-319) on the queues of every view model listed.
- "Finishing operations" is unchanged, one line per running operation, `{Title} — {repository}`
  (`FinishingItem`, 358-360): a bulk pull of four rows shows four (`Pull — api` …); the window closes
  when their queues are idle or on "Quit now".
- `CloseAll` (`AppSession.Dispose`) disposes every tab, the dashboards releasing their summaries
  (D81's rules cancel their network operations), then calls `OpenRepositories.DisposeAll()`.
- Closing a dashboard tab releases its rows' summaries, which disposes them (3.4): a fetch from a
  row's menu, outside any run, is cancelled; a running pull past its fetch stays in `Closing`, so the
  window waits for a pull a closed dashboard started as for a closed tab's.

### 3.8 `NamesRepositoryInDialogs`

A dialog an operation opens names its repository ("In {repository}" under its title, phase 3's
design 4f) while a dashboard tab is open or more than one tab has a repository:
`MainWindowViewModel.NamesRepositoryInDialogs` (121) becomes `Tabs.Any(tab => tab.IsDashboard) ||
Tabs.Count(tab => tab.Repository is not null) > 1`. Its readers stay (`TabServices.NamesRepositoryInDialogs`,
`ViewModels/TabServices.cs:111`, set at `MainWindowViewModel.cs:58`; `RepositoryOperations.NamesRepositoryInDialogs`,
`RepositoryOperations.Run.cs:122`; the prompt dialogs, `AppSession.cs:177`), so a bulk fetch's
sign-in dialog says which repository asks, also with the dashboard as the only tab. The error dialog
always names its repository. Without a dashboard nothing changes, so no earlier check does.

### 3.9 Tests

`SummaryModeTests` (App.Tests; a counting `FakeRepositoryBase`, `TempRepo` where git must run: one
`ReadRefsAsync` and one `ReadStatusAsync`, no `CreateWatcher`, no `LoadCommitsAsync`; the graph
filters untouched, no `Touch`, no `Write`; one `summary read in` line and none of 3.1's skipped ones;
`FindBackupsToRemoveAsync` not called; the toolbar's states and reasons equal to a full view model's;
`RefreshAsync` loading no history; a fetch with the toolbar's title and result; a pull recorded in
`UndoHistories.For(…)` and offered as `Undo pull into main` by a tab opened later; a pull stopped with
conflicts leaving the pending entry and the banner; dispose cancelling a running fetch and letting a
local operation finish; `_started` still false after the summary read; `FirstRead` completed after
the refs and the status are applied, and after a failed read; `GitSettingsChanged` on a summary view
model whose status has conflicts making no `ReadMergeToolAsync` call); `PromotionTests` (the full start after
`Promote`: watcher, history, the Q1 lines from the promotion's stopwatch, the clean-up at opening; a
refresh the queue asks for during the first load, a watcher's git-folder event and a working-tree
event raised then, each run once after it, no second history; a summary read ending after the
promotion applying nothing, and an adoption while it runs leaving `FirstRead` incomplete until the
full read; an operation running at the promotion ending in the tab's status bar; with a counting fake
watcher and a pull held running across `Promote` and the first load: the new watcher paused from its
creation, before `Start`, an event raised during the pull starting no `ReadRefsAsync`,
`ReadStatusAsync` or second `LoadCommitsAsync`, the pause released when the run ends, exactly one
refresh after it, and a later operation pausing and resuming it as in a tab opened directly; a tab
opened from the recent list while a released summary's pull integrates showing `Waiting for Pull to
finish…` and starting once the queue is idle, with no second queue meanwhile; auto-fetch's first
fetch one interval after the promotion; after `Promote`, `GitSettingsChanged` reading `merge.tool`
once); `RepoTabsTests` addition (a tab reopened while a closed
tab's merge runs waits likewise, and one disposed while it waits opens nothing); `OpenRepositoriesTests`
(3.4: lookup by key and by folder through `WorkspacePaths.Same`; the first registered tab, and the
next when it closes; adoption removing the summary and its holders; two holders sharing one view
model, disposed at the last release; releasing the last holder during a row-menu Fetch disposing the
view model, the fetch ending `Fetch cancelled`; releasing it during a local operation letting it
finish, the view model in `Closing` until `WhenIdleAsync` completes, then removed; `WhenIdleAsync`
covering a closed tab and a released summary; `AcquireSummaryAsync` waiting for a closed queue and
returning a tab registered meanwhile; a holder whose token is cancelled during that wait getting null,
nothing left in `Summaries`; an acquisition after `DisposeAll` getting null; `DisposeAll`;
`NetworkQueueOn` finding a sibling worktree's running fetch and a second tab's on the same worktree,
never `except`, null once it ends);
`MainWindowCloseTests` (new; 3.7 and 3.8: `TryClose` waiting for a summary's running pull, `Pull —
api`, and for a closed dashboard's; `StopStarting` before `DropWaiting`; `CloseAll` disposing the
summaries; `CloseAll` while an active dashboard's read is held by the fake's gate making no git call
and no view model after `DisposeAll`; closing a dashboard tab during a row-menu Fetch, outside any
run, cancelling that fetch; `NamesRepositoryInDialogs` with a dashboard alone, with a repo tab beside
it, and unchanged without one).

The stale-index cost of every status read (0.1) is plan risk 29 (16.2). At the phase's end
`architecture.md`'s "Repository tabs (D45)" and "Operation queue" gain summary mode, the registry and
adoption.

## 4. Bulk fetch and pull

The plan's item (phase 6, from phase 3): "the dashboard's bulk fetch and pull run through each
repository's `OperationQueue`, as the toolbar's do (D81), with the same titles, Cancel and error
dialog." They are the toolbar's own `RepositoryOperations.FetchAllAsync`
(`RepositoryOperations.Remotes.cs:35-48`) and `PullAsync` (`:91-102`), whose titles, running texts,
dedupe keys (`fetch:*`, `pull:<ref>`), `PromptMode.Ask`, `UndoScope.Pull`, D124's checks and result
texts stay exactly as they are. New: which rows a run acts on (4.2), how many run at once (4.3), their
prompts (4.4), stopping (4.5), failures without a dialog (4.6), and the dashboard tab's header, status
bar and toolbar (4.9, 4.10). Auto-fetch still fetches only the active tab's repository (D79, D85),
now skipping a tick while another queue or a run fetches from its common git folder (4.3); a dashboard
tab has none, so nothing auto-fetches while it is active.

`{workspace}` is the workspace's name as its tab shows it; `{n}` a count, singular for one (D113);
`{b}`, `{up}` as in 2.6. "The run" is one click of Fetch all or Pull all on one dashboard.

### 4.1 The repository behind each row

A row's operations run through its `RepositoryViewModel`, the first registered tab's on that worktree
or its dashboard's summary-mode one (section 3: 3.1, 3.3 to 3.6), so they have the toolbar's titles,
Cancel, dedupe and undo recording by construction. A run starts a row's operation only after the row's
first read (4.2; for a row that follows a tab, its view model's `FirstRead`, 3.2), so `Refs` and
`Status` are set when the undo recording reads `Status.InProgress.IsActive`
(`RepositoryOperations.Undo.cs:653`). Their dialogs, a run's sign-in included, name the repository
("In api") while a dashboard tab is open (3.8).

### 4.2 Which rows each acts on

A run decides every row at its start, in row order, from the row's last read, by the toolbar's pure
rule `ToolbarViewModel.StatesFor(refs, status)` (`ToolbarViewModel.cs:368-376`; reasons `Reasons`,
`:460-506`), which reads reasons, not tooltips (6.8).

| Row | Fetch all | Pull all |
|---|---|---|
| Its first read has not ended (a row of its own in its first read or its first folder check, 2.10; a row that follows a tab until that tab's view model's `FirstRead` completes, 3.2) | waits for it ("Waiting to fetch…"), then decided as below | the same, "Waiting to pull…" |
| No repository behind it (2.6's `Folder not found`, `Not a repository`, `Git could not open it`) | left out; its State cell says why, its Operation cell shows nothing new | the same |
| `StatesFor(…).Fetch` disabled: no remotes | left out: "This repository has no remotes" | — |
| `StatesFor(…).Pull` disabled | — | left out with the first reason in `Reasons`' order: "HEAD is detached: check out a branch to pull", "{b} has no upstream: set one first", "{up} no longer exists", "Finish or abort the {kind} first", "This repository has no remotes" |
| Enabled | in line: "Waiting to fetch…" | in line: "Waiting to pull…" |

A row that follows a tab whose first read failed (its `ErrorText`) is left out as a row without a
repository (`Git could not open it`).

A read the run waits for belongs to the run and goes on while the dashboard is in the background, as
the run does (2.10). A row whose folder check has not returned (2.10: a share that does not answer)
counts as still being read: it shows `Waiting to fetch…` / `Waiting to pull…`, the run's other rows go
on without it, and it is decided when the check returns (left out with `Folder not found` when the
folder is gone); Cancel all ends its wait as `CancelledInLine` (4.5). A left-out row shows its reason in its Operation cell (2.6), secondary, the reason
as its tooltip, until the dashboard's next run or the row's next operation starts; it counts as skipped
(4.9). A row is decided **again when its turn comes** (4.3), by the whole table from its current state:
one whose state changed meanwhile (a terminal detached HEAD, a tab's merge stopped, a folder an F5 read
found gone) is left out with its reason then and gives its slot back at once; one that lost its view
model meanwhile is read again first (4.3). A started pull still makes D124's checks (`RefuseIfPullTargetMovedAsync`,
`RepositoryOperations.Remotes.cs:240-260`; `RefuseIfHeadMovedAsync(…, fresh: true)`, `:178`). Pull all
fetches inside each pull (D84), never before it.

### 4.3 The run: its object, its order, the cap and its slots

**`BulkRun`** (`src/VisualCommit.App/ViewModels/Workspaces/BulkRun.cs`): its kind (`BulkKind.Fetch`,
`.Pull`), a `BulkRunRow` per row in row order, counts, a cancellation source, a prompt group (4.4),
`IsRunning`, `Completion` (a task), `Changed` and `StopStarting()` (4.5). A `BulkRunRow`: its row (no
view model: the row's is looked up at its turn), its `BulkRowState` (`Reading`, `Skipped`, `InLine`,
`Running`, `Ended`, `CancelledInLine`), its reason (skipped), its `OperationResult` (ended), its kept
`DeferredDialog` (4.6).

`DashboardBulkViewModel` (4.9) creates a run on the click and keeps it as `Current` after it ends, for
its result and Details…, until the dashboard's next run starts or the dashboard closes; one run per
dashboard at a time (4.10). At the start it logs at Info `{workspace}: {Fetch all|Pull all}: started
for {n} repositories, {s} skipped`; at the end it disposes its prompt group and cancellation source,
raises `Changed` and logs `{workspace}: {Fetch all|Pull all}: {result}`.

**The cap.** `BulkSlots` (`ViewModels/Workspaces/BulkSlots.cs`, one per window, `TabServices.BulkSlots`)
grants **at most 4 slots at once** (`BulkSlots.Count = 4`) to all the window's runs together, and
**never two at once to one common git folder** (`WorktreeKey.Of(IGitRepository.CommonDirectory)`), nor
one to a common folder another queue is fetching from (below): two fetches of one common git folder
that overlap while the remote has something new lose one of them on its remote-tracking refs (0.2).
First come, first served; a request whose common folder is busy is passed over until it is free. A run
requests a slot per row in line, in row order, at its start (a row still reading, when its read ends;
a row read again at its turn, when that read ends); a second dashboard's run waits behind the first's
rows. A request withdrawn by `StopStarting` (4.5) is never granted. 4 bounds git's processes on the
processor-bound laptop (plan risks 12, 18) and the prompts that can wait at once (4.4).

- **The row's turn.** The run takes the slot, then the row's **current** view model (`row.Repository`:
  its own summary-mode one or the first registered tab's, 4.1), never one taken at the run's start, and
  decides the row again by 4.2's whole table from its current state: a folder state a read found
  meanwhile (2.10 step 2) leaves it out with its State reason, skipped, not cancelled. A row with no
  view model at its turn, or a closed one (`IsClosed`; its last tab closed, 3.6, or a read between the
  release and the acquisition, 2.10 step 2), gives its slot back at once and goes back to `Reading`:
  the run starts its read, or waits for the one running, also with the dashboard in the background
  (2.10's trigger 5), and the row requests a slot again when the read ends, as a row still reading at
  the start does. A row a run started counts as cancelled only when Cancel all, its own Cancel, a
  cancelled prompt or a closed tab cancelled it. A row cannot be removed from the workspace meanwhile
  (4.7).
- A slot is held **from the call to the end of the operation**: the call, `Operations.FetchAllAsync(part)`
  or `PullAsync(part)` (4.6), queues, waits, runs and refreshes, and the slot goes back when it returns.
  The call opens no result dialog (4.6); a prompt's dialog is part of the operation and holds its slot.
- An operation whose queue is busy (the tab's own push) waits behind it, holding its slot; the row
  shows the queue's running operation (2.6).
- Bulk fetch is the user's Fetch, not auto-fetch: prompts asked, no `AutoFetchScheduler.NetworkTimeout`
  (`AutoFetchScheduler.cs:165`), `--no-auto-maintenance` on every call of a repository but its last
  (D109).

**The common folder beside the tabs.** The slots keep the runs' rows apart; two more checks keep the
runs and auto-fetch off a fetch in another queue on the same common git folder (a sibling worktree's
tab or summary, or a second tab on the same worktree, 3.5):

- `OpenRepositories.NetworkQueueOn(string commonKey, IOperationQueue? except)` (new, 3.4): the first
  queue of a view model the registry holds (a tab's, a summary's, one in `Closing`) on that common git
  folder, other than `except`, whose `Current` is a network operation (`OperationSnapshot.Kind ==
  OperationKind.Network`, background ones included), else null.
- `BulkSlots` also passes over a request while `NetworkQueueOn` returns a queue for its common folder,
  the queue of the row's view model excepted (a queue runs one operation at a time), and looks again
  when that queue raises `Changed`; the row stays "Waiting to fetch…" / "Waiting to pull…".
- `AutoFetchScheduler.Tick` (`AutoFetchScheduler.cs:123-155`) also returns, to try again at a later
  tick, while `NetworkQueueOn` returns a queue for the active repository's common folder (its own queue
  excepted) or `BulkSlots.IsHeldFor(string commonKey)` says a slot is out on it, beside its "queue
  idle" check (`:141-142`). The scheduler gets the check as an init-only `Func<RepositoryViewModel,
  bool> CommonFolderBusy` (default false), which `MainWindowViewModel` (`:99`) sets. A skipped tick
  costs nothing: auto-fetch is a background operation.
- **Not covered**, as in phases 3 to 5: a Fetch, Pull or Push the user starts in a tab (or a row's own,
  4.7) while another queue fetches from the same common folder. One of the two can fail with git's
  `cannot lock ref` lines (0.2), in its error dialog, as "Auto-fetch failed" or in a row's Details…;
  nothing is left behind and the next fetch works. A window-wide gate taken by every queue's network
  operation would cover it, at the price of a new waiting state across D81's queues, Push included; a
  new plan risk records it (16.2).

### 4.4 Prompts of one run

A run's prompts form a **group**, so that ten repositories on one host do not open ten stacked "Sign in
to …" dialogs (D104):

- `PromptGroup` (`src/VisualCommit.Core/Operations/PromptGroup.cs`): `Id` (`bulk-<n>`), an `Ended`
  token, `Dispose()` (ends it); one per run, from its start to its end.
- `RunOptions.PromptGroup` (4.6) is copied by `RunAsync` into a new `GitOperation.PromptGroup` (`init`,
  beside `Prompts`), which the queue passes to `IGitPromptBroker.Begin(string title, string
  repositoryName, PromptMode mode, string? operationId = null, PromptGroup? group = null)`
  (`GitPrompts.cs:87`; `OperationQueue.cs:522`). `AskPassBroker`'s scope puts it in every `GitPrompt`
  (`AskPassBroker.cs:303-311`), a new last parameter `GitPrompt(…, PromptGroup? Group = null)`
  (`GitPrompts.cs:37-45`). `NullGitPromptBroker` ignores it.
- `PromptDialogs.AnswerAsync` (`PromptDialogs.cs:54-96`), for a prompt with a group: a notice
  (`GitPromptKind.Notice`) shows as today, never held back. Otherwise it waits for the group's turn,
  **one prompt of a group at a time** (a `SemaphoreSlim(1)` per group id; the scope's cancellation ends
  the wait with `PromptAnswer.Refused`), then looks for a kept answer by **(group id, prompt text)**:
  git's text names the URL and the user (`Username for 'https://github.com': `), so the same text is
  the same credential. A kept text answers at once; a kept Cancel answers `PromptAnswer.Cancelled` (that
  operation ends "Fetch cancelled", as a Cancel of its own dialog would, phase 3's design 2.16).
  Otherwise it shows the dialog as today (`_presenter.ShowAsync`) and keeps the answer under the
  group's key: the text, the password typed with a user name under the password prompt's text (`Keep`,
  `:124-137`), or the Cancel; a dialog closed because its scope ended keeps nothing. Then the group's
  next prompt has its turn. A group's answers are forgotten when it ends, and each after
  `KeepAnswersFor` (2 minutes, `:24`) as today. A prompt without a group is answered as today.
- **`AskPassBroker.MaxConnections` becomes 16** (`AskPassBroker.cs:26`, also the pipe's instance
  count): a run's 4 git processes can each hold a connection while waiting for their group's turn, and
  with 4 a tab's own prompt beside them would fail as refused within 5 s (0.2). 16 leaves room for every
  tab's own prompt and a clone's beside the runs' 4; the comment says so.
- ssh's notices (`PromptNotices`, `PromptDialogViewModels.cs:226-265`) stay window-wide, and every
  `OperationStatusViewModel` shows the newest in place of its text (`OperationStatusViewModel.cs:253-266`):
  rows show 2.6's `CompactText` and `CompactTone`, which leave it out; the dashboard tab's status bar
  shows it (4.9).

### 4.5 Cancel all, and a row's own Cancel

**`IOperationQueue.Cancel(GitOperation operation)`** (new, `IOperationQueue.cs`, `OperationQueue.cs`)
cancels one operation, whatever else the queue holds:

| `operation` is | Effect |
|---|---|
| waiting | removed and completed as `OperationOutcome.Cancelled` ("{Title} cancelled") without running, as `DropWaiting` does (`OperationQueue.cs:237-263`); `Changed` raised; the others stay |
| next in line, its start posted but not run yet | the same, as `Dispose` treats it; the one after it is next in line instead (WP4) |
| running, `CanCancel` (a network operation before `EndCancelWindow`) | cancelled as `CancelCurrent` does (`:218-235`): the token, then the stale-lock scan (D126) |
| running past its cancel window (a pull past its fetch, `RepositoryOperations.Remotes.cs:175`), or local | nothing: it runs to its end (D81) |
| merged into a waiting operation by its dedupe key (`RunAsync`, `:158-161`) | that operation, by the rows above: the queue records each operation merged into an entry and `Cancel` matches either |
| a background operation, or one the queue no longer holds | nothing |

**`RunOptions.Cancellation`** (a `CancellationToken`, default none): `RunAsync`
(`RepositoryOperations.Run.cs:283-353`) registers it to call `Queue.Cancel(operation)` for its own
operation until the call returns; a token already cancelled queues nothing and returns "{Title}
cancelled" (logged "not started, cancelled"). `CancelCurrent` and `DropWaiting` are not used: they
would also stop a tab's push or drop its waiting operations. So a run cancels only its own operations,
with one exception kept on purpose: a tab's waiting Fetch or Pull and a row's that D123's dedupe merged
into one entry, in either order, are one git call, which the table's merged row cancels for both; the
tab's status bar then shows "Fetch cancelled" / "Pull cancelled", as the row does, and the tab's user
can fetch again. Counting the callers of an entry instead would break D123's one dialog per task
(`_resultsShown`, `RepositoryOperations.Run.cs:89-90`) and give a row's slot back while git still
fetches.

**`BulkRun.StopStarting()`**: the run starts no more rows. Its rows in line and still reading end
`CancelledInLine`, showing "Fetch cancelled" / "Pull cancelled" (`OperationTexts.Cancelled`,
secondary) until the next run or their next operation, and their slot requests are withdrawn. Running
rows go on.

**Cancel all** (the header's `DashboardCancelAll`, 2.5, and on a dashboard tab the status bar's Cancel;
tooltip "Cancel the rest of Fetch all" / "Cancel the rest of Pull all"): `StopStarting()`; then the
run's cancellation source is cancelled, so every running row's registration cancels its operation by
the table (a pull past its fetch finishes and counts by its outcome); the run's text becomes
"Cancelling: waiting for {r} repositories…" ("… 1 repository…"), `{r}` the rows still running, and
Cancel all hides (the status bar's Cancel too) until the run ends. A cancelled fetch that left lock
files keeps the lock-file dialog (phase 3's design 1.8) for its row's Details… (4.6) instead of opening
it.

**A row's own Cancel** (`DashboardRowCancel`, 2.6) is its status's `CancelCommand`
(`OperationStatusViewModel.Cancel` → `queue.CancelCurrent()`, `OperationStatusViewModel.cs:147-148`),
while `CanCancel`, its tooltip the status's `CancelToolTip` ("Stop git"; "Stop git and the merge tool"
while the merge tool runs, 2.6): it cancels what runs in that queue (a tab's own push included,
as that tab's status bar would), and the run counts the row's result as it comes. A row in line has no
Cancel of its own. A Cancel at a shared sign-in also answers the run's other prompts that ask the same
(4.4).

**Closing a dashboard tab** while its run runs is Cancel all, then 3.7's release of its view models
(D81: a network operation cancelled, a local one finishing under `KeepUntilIdleAsync`); "Delete
workspace…" closes it the same way (2.1). What goes on (a pull past its fetch, a pull of a local
upstream, which is `OperationKind.Local`) belongs to an abandoned run: its failure opens its dialog
when a tab's view model runs it (4.6).

### 4.6 Failures in the rows, and the kept dialog (D195, amending D95 for bulk runs)

During a run **no dialog opens for a result** (D195): `ShowResultAsync`
(`RepositoryOperations.Run.cs:527-557`) awaits the dialog until it is closed, so a run would stop at
the first failure. The row keeps what the dialog would show:

- **`RunOptions.Defer`** (an `Action<DeferredDialog>?`, default null: dialogs open at once, D95). Set,
  `ShowResultAsync` does all it does today (the display by `options.DescribeFailure?.Invoke(failure) ??
  DefaultFailureDisplay(text, failure)`, `:533`; the summary to the operation log, `:534-537`) and,
  instead of `ShowFailureAsync`/`ShowStaleLockFilesAsync`, hands over a **`DeferredDialog`**
  (`ViewModels/Operations/DeferredDialog.cs`): `DeferredDialog(OperationText Text, FailureDisplay?
  Display, GitFailure? Failure, IReadOnlyList<string> StaleLockFiles, IReadOnlyDictionary<string,
  (DateTime Time, long Length)?>? Listed, string? OperationId)`: a failure, or a cancel's lock files
  with their stamps taken now (`LockFiles.Stamp`, `:831`), so "Remove lock files" later removes only
  files that are still those. The pull's "Rebase stopped" notice for a stop with nothing to look at
  (`FinishAsync`, `RepositoryOperations.Rebase.cs:917-920`) and `GuardAsync`'s exception dialog
  (`ShowExceptionAsync`, `RepositoryOperations.Run.cs:863-897`) are deferred likewise.
- **Deduplicated calls** (D123; only the first caller shows the result, `_resultsShown`, `:89-90`,
  `:345-350`): when the run's call merged into the tab's waiting fetch, it computes the display again
  by the same rule and defers it, while the toolbar's caller shows its dialog; when the toolbar's click
  merged into the run's waiting fetch, that click shows nothing, as a second click does today. Either
  way the row has Details….
- **`RepositoryOperations.ShowDeferredAsync(DeferredDialog dialog)`** opens it later: a failure through
  `ShowFailureAsync(display, failure)` (`:577-586`), the **identical** C11 error dialog of D95 (title,
  summary, git's output, the command, "Copy output", the extra button, whose action starts after the
  dialog closes), naming its repository; lock files through the "{Title} cancelled" dialog with
  "Remove lock files" (`:827-849`, split to take the stamps given). Nothing after the view model closed
  (`ShowDialogAsync`, `:598-614`).

**The bulk entry points**, in `RepositoryOperations.FetchPull.cs` (section 18 moves the toolbar's two
operations there): `public Task<OperationResult?> FetchAllAsync(BulkPart part)` and `PullAsync(BulkPart
part)`, the toolbar's operations with `RunOptions` carrying `part`'s `Cancellation`, `PromptGroup` and
`Defer`, all else unchanged; null when nothing started (Pull without a current branch with an upstream,
`:93-97`). The toolbar's `FetchAllAsync()` and `PullAsync()` keep their signatures and dialogs.
`BulkPart(CancellationToken Cancellation, PromptGroup PromptGroup, Action<DeferredDialog> Defer)` is a
record in `DeferredDialog.cs`.

**The row** (its Operation cell, 2.6) shows the result as the status bar does ("Fetch failed" / "Pull
failed" in the danger tone, "Pull stopped: 1 file has conflicts" in the warning tone) and, while its
run row keeps a `DeferredDialog`, **Details…** (`DashboardRowDetails`, name "Details"); the row menu's
`Error details…` (2.7) opens the same. The kept dialog goes when the row's queue starts its next
operation (D95), when the dashboard's next run starts, or when the row's view model changes (3.6); until
then it opens as often as asked. A plain "Fetch cancelled" without lock files keeps nothing. Cancel and
Details… never show together. Closing the dashboard drops every kept dialog with its run (4.3).

**An abandoned run.** A run is abandoned when its dashboard closes or is disposed (its tab's close,
"Delete workspace…", 2.1, `CloseAll`). A result that comes after that (a pull past its cancel window,
4.5) is kept nowhere: the part's `Defer` sees its run abandoned and posts `ShowDeferredAsync(dialog)`
of the view model that ran it on the UI thread without awaiting it, so the call returns and its slot
goes back while the dialog is open. A tab's view model (one the row followed, or one a tab adopted,
3.3) then opens D95's dialog naming its repository, as the toolbar's Pull would in that tab. A
summary-mode view model released by the close is closed, so `ShowResultAsync` only logs the failure
(`… failed after the tab closed`, `RepositoryOperations.Run.cs:539-547`), as for a closed tab, and
never calls `Defer`. D195's amendment of D95 ends with the run's dashboard.

### 4.7 A single row's Fetch and Pull

The row menu's Fetch and Pull (2.7) are **exactly the toolbar's** (`row.Repository.Operations.FetchAllAsync()`,
`PullAsync()`): no slot, no group, no `Defer`; a failure opens the error dialog at once (D95), naming
the repository (3.8); the result shows in the row's Operation cell. Each is disabled with the toolbar's
reason (4.2) and, while the row belongs to a run that runs (in line, reading or running), with "Wait for
Fetch all to finish" / "Wait for Pull all to finish". They take no slot, so 4.3's common-folder checks
do not hold them back ("Not covered"). The row menu's `Remove from workspace` and its Delete key (2.7,
1.10) are disabled with the same reason while the row belongs to a run that runs, so a run never loses
a row it decided and every slot it asked for comes back; a `Skipped`, `Ended` or `CancelledInLine` row
can be removed, and the run's counts and result text stay as they were, describing the run as it
started; the row's kept `DeferredDialog` goes with it (1.10).

### 4.8 Pull's outcomes and their texts

Every outcome but one reuses an existing text. The row's Operation cell and, on a repository's tab, the
status bar say the same.

| Case (0.2) | The app | Row and status bar (tone) | Dialog: title / summary (by Details… in a run, at once for a single row) |
|---|---|---|---|
| Left out by the toolbar's rule (H, G, an upstream gone in the last read, K, no remotes) | not started (4.2) | the reason (secondary) | none |
| A, E2, N | `PullOutcome.FastForward` | "Pulled 2 commits into main (fast-forward)" (`PulledFastForward`; "1 commit") | none |
| B, E3 | `UpToDate` | "main is already up to date" | none |
| a merge without conflicts | `Merged` | "Pulled and merged origin/main into main" | none |
| F1 | `Rebased` | "Pulled and rebased main onto origin/main"; with commits git's fork point left out "…; git left out 1 commit that origin/main once had" (warning); with `rebase.autoStash`'s changes conflicted "…; your changes conflicted when put back: they are also kept in the stash" (warning) | none |
| C, F2 | `Conflicts` | "Pull stopped: 1 file has conflicts" (warning); the State cell shows the merge or rebase in progress | none (D95) |
| a rebase stopped with no conflicted file | `Conflicts` | "Rebase stopped at 60005e9 "Local edit"" (warning) | for a stop with nothing to look at only: "Rebase stopped" / the banner's text (`RebaseStopReportAsync`, `RepositoryOperations.Rebase.cs:752-768`) |
| I (`feature` tracks `origin/feature`, deleted there) | `UpstreamGone` | "feature no longer exists on origin" (`PullUpstreamGone`, secondary) | none |
| D1, D2, D4 | failed, `MergeBlockedByLocalChanges` | "Pull failed" (danger) | "Pull failed" / "1 conflict prevents the merge. Commit, stash, or discard your changes first." ("{N} conflicts prevent the merge. …") |
| D3 | failed, `MergeBlockedByStagedChanges` | "Pull failed" | "Pull failed" / "Your staged changes prevent the merge. Commit, stash, or unstage them first." |
| F3; a rebase refused for untracked files | failed | "Pull failed" | "Pull failed" / "Git rebases only a clean working tree: commit or stash your changes first."; "Untracked files are in the way: move or remove them first." |
| **E, E4** | failed, `NotFastForward` | "Pull failed" | "Pull failed" / **new**: "main and origin/main have diverged, and pull.ff is set to only: merge origin/main into main or rebase main onto origin/main."; git's output `fatal: Not possible to fast-forward, aborting.` (2.55: after its nine hints), command `git merge --ff-only --no-overwrite-ignore FETCH_HEAD` (`git merge --ff-only FETCH_HEAD` when Keep ignored files is off, 9.10); also with an ignored file where the fast-forward would write: on diverged branches nothing counts as in the way (9.10) |
| E5, E6 (`merge.ff=only` or `branch.main.mergeOptions=--ff-only`, `pull.ff` unset) | failed, `NotFastForward` | "Pull failed" | "Pull failed" / today's "Git could not pull origin/main into main." with git's output (2.55: E's hints), command `git merge --no-edit --no-overwrite-ignore FETCH_HEAD`: `PullNotFastForward` would name the wrong key, and its "merge" advice would be refused by the same setting |
| an ignored file in the way (9.10; under `--ff-only` only when the branch can fast-forward, else E, E4) | failed | "Pull failed" | "Pull failed" / 9.15's text ("1 ignored file is in the way of the merge: secret.env. Move it away first.") |
| J; a refused or failed sign-in | failed | "Pull failed" | "Pull failed" / "Git could not pull origin/main into main." with git's output |
| HEAD or the upstream moved while it waited (D124) | failed | "Pull failed" | "Pull failed" / "HEAD moved while this waited: it was on main and is on topic now. Nothing was pulled." / "main's upstream changed while this waited: it is origin/next now, not origin/main. Nothing was pulled." |
| a pull setting git would refuse | failed | "Pull failed" | "Pull failed" / "pull.rebase is set to preserve, which this app does not understand." (before the fetch, 8.9); for "Pull from {up} (rebase)" only a refused `pull.ff` counts: "pull.ff is set to foo, which this app does not understand." (D231) |
| cancelled during its fetch (Cancel all, the row's Cancel, a cancelled prompt) | cancelled | "Pull cancelled" | none, or "Pull cancelled" with the lock files git left (phase 3's design 1.8) |

Fetch all's outcomes: "Fetched from all remotes" (secondary; also when nothing was new); "Fetch
cancelled"; "Fetch failed" with "Fetch failed" / "Git could not fetch from origin and mirror." (D109's
`FetchAllFailureDisplay`, `RepositoryOperations.Remotes.cs:83-87`; case L: "Git could not fetch from
broken.") or the default "Git could not fetch from all remotes."; left out: "This repository has no
remotes".

**`OperationTexts.PullNotFastForward(string branch, string upstream)`** (`OperationTexts.Remotes.cs`):
`"{b} and {up} have diverged, and pull.ff is set to only: merge {up} into {b} or rebase {b} onto
{up}."`, `{up}` as the menus name it (D114), echoing their "Merge {up} into {b}" and "Rebase {b} onto
{up}" (`OperationTexts.Menus.cs:91`, `:195`). `PullRefusalDisplay` (`:144-147`) takes the branch and
the upstream and checks first `failure.Kind == GitFailureKind.NotFastForward &&
failure.CommandText.StartsWith("git merge --ff-only ")` → `new FailureDisplay(text.ErrorTitle,
OperationTexts.PullNotFastForward(branch, upstream))`, for the toolbar's and the menus' Pull too (phase
3's check 18b changes, 16.1). The pull passes `--ff-only` under `pull.ff=only` (E, E4) and otherwise
only for a fast-forward under a rebase setting (N), after `PullCanFastForwardAsync` found one possible;
a plain `git merge --no-edit …` refused by `merge.ff=only` or `branch.<b>.mergeOptions` (E5, E6) goes
on to `BlockedByChangesDisplay` and keeps the default text (0.2). Pull (rebase) reads `pull.ff` only to
refuse, before its fetch, a value `git pull --rebase` refuses (8.9, D231): `only` and `false` give way
to the rebase.

### 4.9 The results: the header and the status bar

**What the run says**, the same in the header's `DashboardBulkText` (2.5) and the dashboard tab's
status bar:

| When | Text | Tone |
|---|---|---|
| Before the dashboard's first run | nothing in the header; "Ready" in the status bar | secondary |
| Running, nothing ended yet | "Fetching {n} repositories…" / "Pulling {n} repositories…" ("Fetching 1 repository…"), `{n}` the rows acted on | secondary |
| Running, `{k}` ended | "Fetching 10 repositories… 4 done" / "Pulling 6 repositories… 1 done" (up to four run at once, so a count of those done, after the running text as git's percentage is) | secondary |
| After Cancel all, until the run ends | "Cancelling: waiting for 2 repositories…" ("… 1 repository…") | secondary |
| Ended | the result | below |

The result: "Fetched {k} repositories" / "Pulled {k} repositories" for the rows that succeeded
("Fetched 1 repository"; "Fetched no repositories"), then, each only when not 0 and in this order, ";
{f} failed", "; {s} stopped" (Pull all's `Conflicts`), "; {c} cancelled", "; {x} skipped"; failures
first because a narrow header cuts the end. Examples: "Fetched 10 repositories", "Fetched 9
repositories; 1 failed", "Pulled 6 repositories; 1 failed; 2 skipped", "Pulled 4 repositories; 1
stopped; 3 skipped", "Fetched 3 repositories; 5 cancelled; 2 skipped". Danger when any failed, else
warning when any stopped, else secondary. "Pulled" counts an up-to-date branch and a gone upstream too;
the rows say which. The result stays until the dashboard's next run starts.

**Progress**: determinate, `Percent = 100 × {k} / {n}` rounded down, while the run runs, in the header's
`DashboardBulkProgress` (2.5) and the status bar; rows have no bar (2.6). **Waiting**: the status bar's
"{w} waiting" (`WaitingText`'s form) counts the rows in line. **Cancel**: Cancel all, while the run runs
and until it is clicked (4.5).

**The status bar's source.** `StatusBarView.axaml` binds `ActiveTab.OperationStatus.Text`, `Tone`,
`ToolTip`, `IsRunning`, `IsIndeterminate`, `Percent`, `CanCancel`, `CancelCommand`, `CancelToolTip`,
`WaitingText` and `HasWaiting` with compiled bindings (`x:DataType`) against
`RepoTabViewModel.OperationStatus` (`RepoTabViewModel.cs:59`), typed `OperationStatusViewModel`, a
sealed class. So:

- **`IStatusBarOperation : INotifyPropertyChanged`** (`ViewModels/Operations/IStatusBarOperation.cs`):
  those eleven members (`CancelCommand` as `ICommand`) and `Waiting`, which phase 3's checks read
  (`Phase3Checks.D.cs:190`, `:252`). `OperationStatusViewModel` implements it (`CancelCommand`
  explicitly: the generated one is an `IRelayCommand`).
- `RepoTabViewModel.OperationStatus` becomes `IStatusBarOperation`: `(IStatusBarOperation?)Dashboard?.Bulk
  ?? Repository?.OperationStatus ?? _emptyStatus`, raised when the tab's content changes. The XAML's
  paths stay and earlier pictures are unchanged. `StatusTextRecorder` (`ShellDriver.Checks.cs:404-410`)
  takes the interface.
- **`DashboardBulkViewModel`** (`ViewModels/Workspaces/DashboardBulkViewModel.cs`, one per dashboard,
  `DashboardViewModel.Bulk`) implements `IStatusBarOperation` from its `Current` run as above, ssh's
  newest notice in place of its text while one shows (as `OperationStatusViewModel.Show`), and
  `IDashboardToolbar` (4.10). Section 2.8 binds to it and owns the branch part (the workspace's name,
  `IconWorkspace`).

**Layout**: the header's run part (`DashboardBulkText`, `DashboardBulkProgress`, `DashboardCancelAll`,
name "Cancel all") is placed and sized by 2.5; the row's `DashboardRowCancel` (name "Cancel") and
`DashboardRowDetails` (name "Details") sit in 2.6's Operation cell. The status bar keeps its sizes (26
high, the operation text at most 520 wide), which hold every result of a ten-repository run whole.

### 4.10 The toolbar on a dashboard tab

The dashboard tab's `Toolbar` is a `ToolbarViewModel` (bindings stay `ActiveTab.Toolbar.*`) made by a
new static factory `ToolbarViewModel.ForDashboard(IDashboardToolbar dashboard)` over a private
constructor (`ToolbarViewModel.Dashboard.cs`); `DashboardBulkViewModel` implements `IDashboardToolbar`.
A second public constructor would make every `null` passed to the public
`ToolbarViewModel(RepositoryViewModel? repository)` (`ToolbarViewModel.cs:105`) ambiguous (CS0121);
with the factory `RepoTabViewModel.cs:22`'s `new(null)` and `ToolbarTests.cs:36`, `:287` compile
unchanged. The dashboard's toolbar:

- **Fetch** runs Fetch all, **Pull** runs Pull all, and so do their keys and the palette's toolbar
  entries (6.9, 5.4). Enabled tooltips: "Fetch every repository of {workspace}", "Pull every
  repository of {workspace}", with 6.8's key.
- Enabled while rows are still being read (the run waits for them, 4.2). Disabled with the first reason
  that holds: a run of this dashboard runs: "Wait for Fetch all to finish" / "Wait for Pull all to
  finish" (both buttons); no repository: "{workspace} has no repositories"; no row would be acted on
  and none is reading (a row that follows a tab counts as reading until its view model's `FirstRead`
  completes, 3.2): "No repository of {workspace} has a remote" (Fetch), "No repository of
  {workspace} can be pulled" (Pull).
- Push, Branch, Stash, Pop, Undo, Redo and Search are disabled without a tooltip, as on an empty tab
  (`ToolbarViewModel(null)`, `ToolbarViewModel.cs:445-458`); Settings (7.2) and Theme stay the
  window's, always enabled.

The header's `DashboardFetchAll` and `DashboardPullAll` (2.5) are the same commands, states and
tooltips.

### 4.11 Undo, the activity log and auto-fetch

- **Undo.** A row's pull runs with `UndoScope.Pull(branch.Name)` in the row's view model, whose
  `UndoHistory` is the worktree's session history (`UndoHistories.For`, D154, D167): the repository's
  tab, adopted (3.3) or opened later in the session, offers "Undo pull into main" (phase 5's design
  7.6). A stopped pull leaves a pending entry: the tab shows the banner and Undo says "Finish or abort
  the merge first", as after a toolbar pull. Fetches are not recorded (phase 5's design 7.2).
- **The activity log.** The queues record under `WorktreeKey.Of(gitDirectory)` (`OperationQueue.cs:76`,
  `:661-671`), so a row's operations show in its worktree's records, in its tab and for the selected
  row (2.9). The run itself has no record; its lines are the app log's (4.3).
- **Auto-fetch.** A row's fetch through a tab's view model counts for that tab as a toolbar fetch does
  (the queue's `Finished`, judged by `Fetched`, `AutoFetchScheduler.cs:221-226`). A tab that adopts a
  summary view model is tracked by `AutoFetchScheduler.Track` (`:241-253`) from `TimeSinceOpening`,
  which 3.3's `Promote(Stopwatch)` makes the tab's stopwatch: its first auto-fetch comes one interval
  after the tab opened, also right after a bulk fetch. `AutoFetchScheduler` changes only by 4.3's
  common-folder check (`CommonFolderBusy`).

### 4.12 Closing the window while a run runs

`TryClose`, "Finishing operations" and `CloseAll` are 3.7's. Before the queues' `DropWaiting`,
`TryClose` calls `StopStarting()` (4.5) on every dashboard's running run; its waiting rows end
`CancelledInLine` ("Fetch cancelled"). "Finishing operations" lists at most the run's 4 ("Fetch —
docs", "Pull — web-app") beside the tabs' own. On "Keep open" the run ends with the rows it had
started, counting the rest as cancelled, and a later Fetch all works. A dashboard tab's `Dispose` (from
`CloseAll`) cancels its run as Cancel all does. Background operations still never hold the window.

### 4.13 Tests

Git layer (`tests/VisualCommit.Git.Tests`):

- `OperationQueueTests`: `Cancel` of a waiting operation completes it cancelled without running and
  leaves the other waiting one and the running one; of the one next in line whose start is posted but
  has not run: the same, with no start record, prompt scope or lock scan, and the one after it next in
  line instead (WP4); of the running network operation before its cancel
  window ends: cancelled, stale locks scanned; after it: it ends succeeded; of an operation merged into
  a waiting one: the shared one is cancelled and both tasks get the cancelled result; of a background
  operation, and of one that has ended: nothing. `GitOperation.PromptGroup` reaches
  `IGitPromptBroker.Begin` (the file's `RecordingBroker`).
- `AskPassTests`: `Six_prompts_held_at_once_all_reach_the_handler` (six helpers whose handler answers
  only when all six have arrived all succeed; with 4 the fifth and sixth failed after 5 s); a scope with
  a group puts it in its `GitPrompt`.
- `FailureClassifierTests`: 0.2's texts: E and E5 on 2.36 and on 2.55 (with the hints) `NotFastForward`
  with no blocked paths; D2 and D3 on 2.55 (exit 2, "Merge with strategy ort failed.") as on 2.36.

App (`tests/VisualCommit.App.Tests`, where 17.3's measurement keeps the view tests):

- `BulkSlotsTests` (no git): at most 4 granted; the fifth gets the first released slot; first come,
  first served; a request whose common folder holds a slot is passed over and served when it is free; a
  request whose common folder another queue runs a network operation on (a fake `NetworkQueueOn`) is
  passed over, the row's own queue excepted, and served at that queue's `Changed` once it is idle;
  `IsHeldFor` true exactly while a slot is out on the folder; a withdrawn request is never granted and
  later ones are served.
- `PromptDialogsTests`: two prompts of one group with the same text: one dialog, the second answered
  from the first; a group's second prompt waits for the first's answer; a different text asks again; a
  Cancel answers the same text with `Cancelled`; answers forgotten when the group ends and after 2
  minutes (the test clock); prompts without a group as before.
- `RepositoryOperationsFetchTests`, `RepositoryOperationsPullTests` (`OperationsHarness`, real git):
  `FetchAllAsync(part)` of a failing remote opens no dialog, defers "Fetch failed" and returns the
  failed result; `ShowDeferredAsync` opens the identical error dialog (title, summary, output, command,
  buttons); a cancelled fetch's lock file is deferred with its stamps and "Remove lock files" removes
  it; a part cancelled before the call queues nothing; a part merged into the toolbar's waiting fetch
  gets a deferred display while the toolbar's caller shows its dialog; `PullAsync(part)` refused by
  `pull.ff=only` defers "Pull failed" / `PullNotFastForward`; the toolbar's Pull shows the new summary
  with the command `git merge --ff-only --no-overwrite-ignore FETCH_HEAD` (the changed
  `Pull_refused_by_pull_ff_only_shows_gits_message_after_the_fetch_ran`, 16.1); with `merge.ff=only` and
  `pull.ff` unset the summary stays "Git could not pull origin/main into main.", never the `pull.ff`
  text, and the command is `git merge --no-edit --no-overwrite-ignore FETCH_HEAD`.
- `BulkRunTests` (`TempRepo` repositories, summary-mode view models, a scripted prompt handler, 17.2's
  fetch hold, set per repository by `FetchHold.HoldIn` on the rows it holds, never in a global file):
  rows decided by `StatesFor` with 4.2's texts; "Waiting to fetch…" in line; never more
  than 4 running: six rows under the fetch hold, `WaitForHeldAsync(4)`, then four rows `Running` and two
  `InLine` ("Waiting to fetch…"), `Held` still 4 after a settle (no fifth git started), and after
  `Release()`, well within the hold's 120 rounds of 0.5 s (89 s on the laptop, 17.2), the two in line
  start and all six end "Fetched from all remotes"; where one test runs two held runs in one data
  folder, `FetchHold.Arm()` before the second run's `WaitForHeldAsync`; a repository without a remote,
  next to held ones, left out with `This repository has no remotes` (its cell `No remote`) under the
  hold, also after an F5 read; two worktrees of one repository never at once; a row waits "Waiting to fetch…"
  while a tab on another worktree of its repository fetches, and starts when that fetch ends; a row
  whose state changed while it waited is left out then; a row in line whose folder an F5 read finds gone
  is skipped with `Folder not found`, not counted as cancelled; a row in line whose last tab closes is
  read again and fetched, also with the dashboard in the background, and the run ends; Pull all
  started while the followed tab's refs read is held by the fake's gate: the row says `Waiting to
  pull…` and is decided once the read ends; the refs read ended but the status read held, with a merge
  in progress: the row left out with `Finish or abort the merge first`, not pulled; the followed tab's
  first read failing: the row left out and the run ending; a row whose `directoryExists` fake is held on
  a gate: the other rows run and end, the row is decided when the gate opens, and Cancel all ends it
  `CancelledInLine`; `Remove from workspace` and Delete disabled with 4.7's reason while a row is in
  line, reading or running, enabled for a skipped or ended row, whose removal leaves the run's result
  text and counts unchanged, and enabled after the run; Cancel all (in-line rows "Fetch cancelled", held fetches cancelled,
  "Cancelling: waiting for …"); a tab's waiting Fetch merged with a row's ends "Fetch cancelled" in the
  tab and the row after Cancel all; `StopStarting` alone (in-line rows `CancelledInLine`, requests
  withdrawn, running rows end, a later run works); closing the dashboard cancels its run; an abandoned
  run (4.6): a row that follows a tab, its pull held after its fetch (a repository wrapper whose
  `IntegratePullAsync` waits on a gate), the dashboard closed, the pull refused under `pull.ff=only`:
  the error dialog opens with `PullNotFastForward` and "In {repository}", and the run's slot is back
  before the dialog closes; the same with a summary-mode row: no dialog and the `failed after the tab
  closed` line; a row's own Cancel stops that row only; failures keep Details… and open no dialog;
  the result texts, singular and plural (1 and 2 of each part), and tones; a pull of Pull all undone
  from the adopting tab ("Undo pull into main", HEAD back); the log lines.
- `DashboardBulkViewModelTests`: `IStatusBarOperation` from a run: each text of 4.9, the percent, "{w}
  waiting", Cancel = Cancel all with its tooltip and hidden after it, the notice in place of the text.
- `DashboardToolbarTests`: through `ToolbarViewModel.ForDashboard(IDashboardToolbar)` only Fetch and Pull enabled,
  with 4.10's tooltips and reasons in order (enabled while a row is still being read); a click runs
  Fetch all or Pull all; the rest disabled without a tooltip, Settings and Theme enabled.
- `AutoFetchSchedulerTests`: with a dashboard tab active nothing is fetched; no auto-fetch while
  `CommonFolderBusy` holds (another worktree's queue running a network operation, a slot out on the
  common folder), and the fetch at the first tick after it no longer holds.

Visual checks: 17.4's W4 (Fetch all with progress, held at 25, 75 and 45), W5 (Cancel all), W6
(failures and one sign-in) and W7 (Pull all). W6's remote `../remotes/missing.git` is relative, so
git's `fatal: '../remotes/missing.git' does not appear to be a git repository` is the same in every run
(checked on 2.36 and 2.55, exit 128).

## 5. The command palette

### 5.1 What it is (D191)

Mod+Shift+P opens a box over a list near the window's top; typing filters it and Enter runs the
highlighted entry. **Commands** (5.4), the catalogue's (6.4) with their keys, show at once; **items**
(5.5), the objects as their menus offer them plus tabs, repositories and workspaces, only once the user
types (an empty box would list some 1,500 lines of "Copy SHA"). An entry shares its label, its state
and its reason with the menu item or button that does the same: the same code builds both.

### 5.2 A dialog of the dialog layer

`ViewModels/Commands/CommandPaletteViewModel : DialogViewModel<PaletteEntry?>`, shown by
`Dialogs.ShowAsync` (D69, D104); its view `Views/Commands/CommandPaletteView : DialogView`, found by
`DialogViewLocator`'s naming rule (`Views/Dialogs/DialogViewLocator.cs:63-68`). The layer gives it Esc
(the chain's `EscapeOrder.Dialog`, 300, calls `DialogHostViewModel.CancelCurrent`; result null); the
focus in the box on opening (`DialogView.FocusOnOpening`, caret at the end) and back where it was on
closing (`MainWindow.OnDialogRemoving`, `MainWindow.axaml.cs:414-438`); an open context menu closed
(`MenuLayer.OnDialogsChanged`, `MenuLayer.axaml.cs:257-263`); the window's keys declining, but its own
(6.3, 5.8); prompts stacking over it (D104); auto-fetch waiting (`AutoFetchScheduler` skips while a
dialog is open); a UI Automation pane named by the title. Unlike other dialogs it has no dimming (the
backdrop is a `Panel.paletteBackdrop`, `Transparent` so that a press outside the card cancels the
palette and is handled, as a menu's light dismiss), its card at the top centre (5.3), no buttons (the
box takes Enter, 5.8) and no title drawn (`Title` `Command palette` names the root only).

| Member | What |
|---|---|
| `CommandPaletteViewModel(IReadOnlyList<PaletteEntry> commands, IReadOnlyList<PaletteEntry> items, IReadOnlyList<string> recent, string query = "")` | The two lists in source order, the `Key`s run this session newest first (5.6), the box's starting text (6.5's `workspace`) |
| `Query` (observable, two-way with the box) | Every change ranks again and highlights the first (5.6) |
| `Shown` (`RowCollection<PaletteEntry>`, `ViewModels/Panels/RowCollection.cs`); `Highlighted` (observable), `HighlightedIndex`; `ShowsNothing` | The matches in rank order, refilled by `ReplaceAll(IEnumerable<T>)` with one reset per query, never one event per match (a one-letter query can match some 70,000 items, 5.5); null when none; `No matches` instead of the list |
| `MoveHighlight(int rows, bool wrap)`; `PageRows` | Up, Down ±1, wrapping; PageUp, PageDown ±11 (`PageRows`: 12 rows show, a page keeps one), clamped |
| `RunHighlighted()`, `Run(PaletteEntry)` | Complete with an enabled entry; a disabled one does nothing and the palette stays |
| `ReplaceItems(IReadOnlyList<PaletteEntry> items)` | The items again after the worktrees' read (5.5), only when the other worktrees differ from those the items were built with: the query kept, the highlight kept on its `Key` while shown, else the first |
| `SelectQueryRequested` (event); `CancelledResult` | Mod+Shift+P while the palette is the top dialog: the view selects the box's text; null |

`ViewModels/Commands/PaletteEntry`, a record without Avalonia types: `Label` (as the menu item or
command says it: "Check out feature/alpha", "Fetch all remotes"); `Detail` (an item's menu header,
"feature/alpha", "a1b2c3d Add core", "Working changes"; a repository's folder; or null); `KeyText`
(6.4's text, "Ctrl+Shift+F", or null); `IsEnabled`, `DisabledReason`, `IsDestructive` (the menu item's
or the command's); `Kind` (`Command` or `Item`); `Order` (its place in its list); `Run` (a menu item's
`Invoke`, the menus' `Start` wrapper that logs a failure, or the command's run); `Key` (`Label`, then
U+001F and `Detail` when there is one; the highlight and the session's runs go by it).
`MainWindowViewModel.OpenPaletteAsync(string query = "")` builds the lists, shows the palette and runs
its answer (5.9); it does nothing while a dialog is open.

### 5.3 The card

`Border.paletteCard` (`Theme/Dialogs.axaml`): `VcPanelBackgroundBrush`, a 1-px `VcBorderBrush` border,
corner radius 8, padding 8, the menus' `VcMenuShadow` (without dimming it tells the card from the
window), `Width` 600, `HorizontalAlignment="Center"`, `VerticalAlignment="Top"`,
`Margin="20,96,20,20"`, `KeyboardNavigation.TabNavigation="Cycle"`. Inside: the box
`CommandPaletteBox` (a `TextBox`, 32 high, 582 wide, placeholder `Search commands, branches, tags,
stashes, remotes, repositories and workspaces`, about 443 px at 12.5, so it fits; tabs stay
unnamed, as commits and working changes are); 6 of space; the list `CommandPaletteList` (a `ListBox` with its
own `VirtualizingStackPanel`, transparent, no border, rows 28 high, `MaxHeight` 336 = 12 rows, the
theme's thin vertical scroll bar, no horizontal one), replaced while nothing matches by
`CommandPaletteEmpty` (`No matches`, the search bar's `SearchViewModel.NoMatchesText`,
`VcTextSecondaryBrush`, 28 high, 10 from the left). The card is 2 + 16 + 32 + 6 + 28 × rows high, 84
to 392, and keeps its size at every window size, as VS Code's (12 rows are what an eye takes in; the
list scrolls). Its top lies 8 below the toolbar's bottom (88): card y 96 to 488 with 12 rows, box
y 105 to 137, list from y 143; card x 250 to 850 at 1100×700 (186 above the status bar's 674), 660 to
1260 at 1920×1080, 200 to 800 at the smallest size, 1000×560 (46 above 534); the box 9 inside it on
each side.

A row `CommandPaletteItem` (a `ListBoxItem`, `Padding="10,0"`, corner radius 4) has columns
`Auto,*,Auto`: `CommandPaletteItemLabel` (a `TrimmedTextBlock`, `MaxWidth` 380, whole in its tooltip;
12.5, `VcTextPrimaryBrush`, `VcDangerBrush` for an enabled destructive entry);
`CommandPaletteItemDetail` (a `TrimmedTextBlock` 8 after it, cut first; 11.5, `VcTextSecondaryBrush`;
a disabled entry's reason in its place, 5.7); `CommandPaletteItemKey` (a `TextBlock`, right-aligned, 16
after, never cut; 11.5, `VcTextSecondaryBrush`). An entry without a key shows no key column: the
key's `TextBlock` is hidden, its 16-px gap with it, and the detail runs to the row's padding (the
visual test report's ruling on K10's `k10e`). A disabled entry's parts are `VcTextDisabledBrush`.
The highlighted row lies on `VcSelectionBrush` (the graph's and the Pick dialog's selection) and the
pointer moves the highlight (5.8), so there is no hover look. The light theme uses the same tokens.

### 5.4 What it lists: the commands

For the active tab, in this order, each only where its row says (left out elsewhere, as menus leave out
what cannot apply, phase 3 design 4b). A state taken from a menu builder comes from that builder run on
the same `MenuContext` snapshot (5.5), so menu and palette never disagree. "Repository": the active tab
has a `Repository` (not opening, not empty, not a dashboard); while `NamesRepositoryInDialogs` is true
(3.8) its commands carry the repository's name as detail ("web-app"). "Key" is 6.1's row.

| # | Labels | Key | Listed | Enabled, or the reason, from | Runs |
|---|---|---|---|---|---|
| 1 | `Fetch all remotes`, `Pull`, `Push`, `Create branch…`, `Stash changes…` | 15 to 19 | a repository | `RepositoryMenus.ForEmptyGraphArea`'s items of the same labels | `ActiveTab.Toolbar.FetchCommand`, `PullCommand`, `PushCommand`, `BranchCommand`, `StashCommand` |
| 2 | `Pop the newest stash`, `Undo {text}`, `Redo {text}` (`Undo`, `Redo` while disabled), `Search commits` | 20 to 23 | a repository | `Toolbar.CanPop` and `PopDisabledReason`, and so on (6.8) | `Toolbar.PopCommand`, `UndoCommand`, `RedoCommand`, `SearchCommand` |
| 3 | `Next match`, `Previous match` | 24, 25 | the search bar is open (`Repository.Search` not null) | `Search.NextCommand.CanExecute` (`HasMatches`); `No matches` | `Search.NextCommand`, `PreviousCommand` |
| 4 | `Stage all`, `Unstage all` | 27, 28 | a repository with working changes (`HasWorkingChanges`) | `ForWorkingChanges`' items (`Nothing to stage`, `Nothing staged`) | `MenuActions.StageAllAsync`, `UnstageAllAsync` |
| 5 | `Go to the commit message` | 29 | a repository | `Close the conflict resolver first` (`ToolbarViewModel.CloseConflictResolverText`) while the resolver is open, then `There are no changes to commit` | `IWindowKeyActions.FocusCommitMessage` (6.3) |
| 6 | the commit button's text (`WorkingChangesViewModel.CommitButtonText`: `Commit 1 file`, `Commit {N} files`, `Commit merge`, `Amend previous commit`, `Continue rebase`; `Stage files to commit` while nothing is staged) | 30 | a repository with working changes or an operation the button continues (`HasWorkingChanges`, `Changes.ContinuesOperation`) | `Close the conflict resolver first` while the resolver is open, then `Changes.CanCommit` and the button's reason (`CommitDisabledReason`, 6.8), none when it has none | `Changes.CommitCommand` (`WorkingChangesViewModel.cs:439`), with the message in the panel's boxes (D199) |
| 7 | `Show reflog`, `Show backups` | 26; none | a repository | `ForEmptyGraphArea`'s items | their items |
| 8 | `Refresh` | 4 | a repository, a dashboard | always enabled | `Repository.RefreshAsync()`; `DashboardViewModel.RefreshCommand` (2.10) |
| 9 | `Create tag at HEAD…`, `Add remote…`, `Manage remotes…`, `Show all hidden branches`, `Stop soloing` | none | a repository | `ForSection`'s items for Tags, Remotes and LocalBranches (`No branch is hidden`, `No branch is soloed`) | their items |
| 10 | `Filter branches and tags` | 33 | a repository | always enabled | `IWindowKeyActions.FocusFilter` (6.3) |
| 11 | `Fetch every repository of {workspace}`, `Pull every repository of {workspace}` | 15, 16 | a dashboard | its toolbar's `CanFetch`, `CanPull` and reasons (4.10) | its `FetchCommand`, `PullCommand`: Fetch all, Pull all |
| 12 | `Add repository…`, `Add open tabs`, `Rename workspace…`, `Delete workspace…` | none | a dashboard | `WorkspaceCommands` (1.5) | the same |
| 13 | `New tab` | 5 | always | always enabled | `NewTabCommand` |
| 14 | `Close tab` (detail: the tab's title) | 6 | always | `ActiveTab.CanClose`; `This is the only tab, and it is empty` | `CloseTabCommand(ActiveTab)` |
| 15 | `Next tab`, `Previous tab` | 7, 8 | always | more than one tab; `There is only one tab` | 6.5 |
| 16 | `Go to the next dashboard` (detail: the workspace it goes to) | 11 | a dashboard tab is open | always enabled | 6.5 |
| 17 | `Open repository…`, `Clone repository…`, `Init repository…` | 12 to 14 | always | always enabled | 5.10 |
| 18 | `New workspace…` | none | always | `WorkspaceCommands`' Create (1.5) | the name dialog (1.6) |
| 19 | `Show the activity log` / `Hide the activity log` | 3 | a repository, a dashboard | `ActivityLog.CanToggle`; on a dashboard 2.9's reasons, the first that holds (`{workspace} has no repositories`, `Select a repository to see its activity`, `Wait for {name} to be read`, the selected row's State tooltip) | `ActivityLog.ToggleCommand` |
| 20 | `Open settings…`, `Show keyboard shortcuts…`, `Choose an editor…` | 2; none; none | always; `Choose an editor…` only while no editor is chosen (`Settings.Current.Editor` null, 12.1) | always enabled | `OpenSettingsCommand` on General, on Keyboard shortcuts, on Tools (7.2) |
| 21 | `Use the dark theme`, `Use the light theme`, `Follow the system's theme` | none | always, but the one chosen now (11.7) | always enabled | `MainWindowViewModel.UseTheme(AppTheme)` (11.6) |

Undo's and Redo's labels are the toolbar's words without the key (`Undo commit "Fix the parser"`,
phase 5 design 7.6). Pop's is not the stash menu's `Pop stash`: it pops whichever stash is newest.
Row 6's label is the stage panel's button's, so it changes with what the button does, and its key
works only from the message boxes (6.10) while the entry runs the commit from anywhere it is listed.
The commands without a key are 6.1's list (D199).

### 5.5 What it lists: the items

**The repository's menus.** On a repository the palette runs these `RepositoryMenus` builders with
`Repository.MenuActions` on one snapshot, `Repository.MenuContext` (`RepositoryViewModel.Menus.cs:52-65`,
the worktrees as last read):

| Builder | For | Detail (the menu's header) |
|---|---|---|
| `ForLocalBranch` | every local branch, the checked-out one first, then the left panel's order | its name |
| `ForRemoteBranch` | every remote branch (never `refs/remotes/<r>/HEAD`, which the refs leave out, `GitRepository.Refs.cs:149-152`) | `{r}/{b}` |
| `ForTag` | every tag | its name |
| `ForStash` | every stash, `stash@{0}` first | `stash@{n} {message}` |
| `ForRemote` | every remote | its name |
| `ForCommit`, `ForCommits` | the graph's selection when it has a commit (a stash row: `ForStash`) | `{short} {subject}`; `{N} commits` |
| `ForWorkingChanges` | the working changes, when there are any | `Working changes` |
| `ForDiff` | the open diff as `OpenDiffMenu` builds it with no hunk (`RepositoryViewModel.Menus.cs:269-294`): its selected lines' items when lines are selected | the file's name |
| the page's `MenuFor` | the open page's selected row: `FileHistoryViewModel.MenuFor(SelectedRow)`, `BlameViewModel.MenuFor(SelectedRun)`, `ReflogViewModel.MenuFor(SelectedRow)` (`Pages/*.cs:183`, `:202`, `:182`) | the row's header |

Each `MenuItem` becomes an item with its `Label`, the header as `Detail`, `KeyText` from its `Command`
(6.7), `IsEnabled`, `DisabledReason`, `IsDestructive`, and `Invoke` as `Run`; what a menu leaves out
(D77, D92, phase 3 design 4b) the palette lacks too. Left out on purpose: `ForDrop` (two objects),
`ForEmptyGraphArea` and `ForSection` (5.4's commands), `ForActivityLogRow`, `ConflictMenus` (a focused
editor's text commands), `ForReflogSource` (the reflog page's own control), the interactive list's row
menu and the resolver's commands (they act on the list's selected row and the focused result, and
have their own keys and buttons), and every file menu but the open diff's (it needs its row).

**The window's**, once the user types:

| Label | Detail | Listed | Runs |
|---|---|---|---|
| `Go to {title}` | `Tab {n}` (its place, from 1); key Mod+{n} for n ≤ 8, matching its `Tab {n}`, also on the last tab (with five tabs the fifth shows Mod+5, not Mod+9); Mod+9 only for a last tab at place 9 or later; none for a tab at place 9 or later that is not the last | every tab but the active one | activates it |
| `Open repository {name}` | its folder | every recent repository (`TabServices.Recent`), then every workspace's repository not listed yet (`WorkspacePaths.Same`), but the active tab's | 5.10 |
| `Open workspace {name}` | `1 repository` / `{N} repositories` / `No repositories` | every workspace, in the list's order | 5.10 |

**Duplicates.** Items of one `Key` are one (a stash selected in the graph and listed among the
stashes). An item whose `MenuItem.Command` is a listed command of the same label is left out (the
working-changes row's `Stage all` and `Stash changes…` show once, as commands); one of another label
stays (the checked-out branch's `Push main to origin/main`, found by "push main", which `Push` does not
contain).

**The worktrees.** A local branch's fast-forward item depends on the other worktrees (phase 3 design
2.7, D131), which `ReadMenuContextAsync` reads (`git worktree list --porcelain`). The palette opens at
once with the worktrees last read and starts that read; if it is still open when the read ends and the
other worktrees (`RepositoryMenuContext.OtherWorktrees`) differ from those the items were built with,
it builds the items again (`ReplaceItems`); otherwise the items stay. An operation checks what it acts
on when it runs (D124).

**Size.** A local branch gives about 15 items and a tag about 7, so 100 branches give about 1,500 items,
and 100 local branches, 200 remote branches and 10,000 tags about 70,000. The builders are pure and run
on the UI thread at opening. Each looks its ref up by `RepositoryMenuContext.FindRef`, today a linear
search over the refs (`RepositoryMenuContext.cs:181-182`), which makes the build quadratic in the refs
(in a micro-benchmark of the lookup and 7 items per tag: 275 ms at 10,000 tags against 35 ms with a
dictionary, paid twice when the worktree read built the items again). So `FindRef` becomes a lookup in
an index from full name to the first `GitRef` of that name (`TryAdd`, so duplicate names keep
`FirstOrDefault`'s answer), built at the first call and remembered with the `Refs.Refs` list it was
built from, so that a `with` copy that replaces `Refs` builds its own (`RepositoryViewModel.Menus.cs:319`
copies the context with `with`); the menus get the same answers faster. Ranking is one pass per
keystroke and `Shown` is refilled with one reset (5.2). 5.12 times the build and a one-letter query at
some 70,000 items and the matcher at 3,000; should either turn out slow, the lead caps how many matches
`Shown` holds.

### 5.6 Matching and ranking

`ViewModels/Commands/PaletteMatcher` (pure). The query is trimmed and split at white space; an empty
one lists the commands only, those run this session first (newest first), then the catalogue's order.
Otherwise an entry matches when **every word** occurs in its label or its detail
(`StringComparison.OrdinalIgnoreCase`); no fuzzy subsequence, which with 1,500 entries matches too much
and is hard to test. Matches rank by:

1. **tier**: 0, the label starts with the whole trimmed query; 1, every word starts a word of the
   label (at its start or after a space, `/`, `-`, `_`, `.`, `(` or `"`); 2, every word is in the
   label; 3, a word is only in the detail;
2. **enabled before disabled** within a tier, so that a disabled exact match is not buried;
3. **run this session**, the most recent first;
4. **source order**: commands before items, then each list's order.

The first match is highlighted after every change of the query. The session's runs are
`MainWindowViewModel`'s list of at most 20 `Key`s, newest first, in memory, never saved.

### 5.7 Disabled entries

Shown, not left out (D114): the label and the key in `VcTextDisabledBrush`, the reason in the detail's
place (cut with "…", whole in the row's tooltip); Enter or a click does nothing and the palette stays.
Its UI Automation `HelpText` is the reason and its `ItemStatus` stays the detail, so two entries of one
label still tell their objects apart. One disabled without a reason (refs not read yet:
`ToolbarMenuState.Disabled`) shows its detail, dimmed.

### 5.8 Keyboard and pointer

On `CommandPaletteBox`'s tunnelling pass, before the text box, handled: Up and Down move the highlight
one row, wrapping first to last and back, and scroll it into view; PageUp and PageDown move 11 rows,
stopping at the ends; Enter runs the highlighted entry when it is enabled (5.9); Mod+Shift+P selects
the box's text (the router raises `SelectQueryRequested`, 6.3). Esc is the window's chain's (5.2);
Home, End, Left, Right and text keys stay the box's; Tab stays in the box (the list and its rows are
`Focusable="False"`, the card cycles). The window's other keys decline while the palette is open
(6.3), so Ctrl+Z in the box is its text undo (D163). The pointer: moving over a row highlights it; a
click (press and release on one row) runs an enabled one; the wheel scrolls; a press outside the card
cancels.

### 5.9 Running an entry after the palette has closed

`OpenPaletteAsync` awaits `Dialogs.ShowAsync(palette)`. For an entry (enabled by construction):

1. The palette has closed and the focus is back: `OnDialogRemoving` runs while the dialog closes,
   before the awaiting code goes on (`TaskCreationOptions.RunContinuationsAsynchronously` posts the
   continuation to the UI context).
2. Its `Key` goes first in the session's runs.
3. Debug log `{repo}: Command palette ran: {label}`, with ` ({detail})` when there is a detail, the
   `{repo}: ` prefix only while the active tab has a repository, as `MenuLayerViewModel.Open` writes
   (`MenuLayerViewModel.cs:95-98`); the opening logs `{repo}: Command palette opened`.
4. `entry.Run()`. A dialog it opens ("Create branch…") records the element the focus came back to and
   returns the focus there.

A cancelled palette runs and logs nothing more.

### 5.10 Opening repositories and workspaces

`Open repository {name}`, `Open repository…` (after the folder dialog `Open a repository`),
`Clone repository…` and `Init repository…` (after `Choose a folder for the new repository`), from the
palette or a key, take:

1. **the tab that shows that worktree**: the first whose `RepositoryPath ?? PendingPath` is the same
   folder (`WorkspacePaths.Same`), activated; nothing opens again (Clone and Init skip this step);
2. **else the active tab when it is empty**: no repository, not opening, not a dashboard, no clone
   running (`ActiveTab.Welcome.RunningClone` null);
3. **else a new tab at the end** (`NewTab`, as "+").

There Open calls `RepoTabViewModel.OpenAsync(path)` (first in the recent list; a failure shows on that
tab's welcome page as today); Clone opens `WelcomeViewModel.OpenCloneFormCommand` with the focus in its
URL box; Init calls a new `WelcomeViewModel.InitAtAsync(path)`, the part of `InitAsync`
(`WelcomeViewModel.cs:94-114`) after its folder dialog, which `InitAsync` calls too. A cancelled folder
dialog changes nothing and makes no tab. `Open workspace {name}` calls
`IWorkspaceHost.ShowWorkspace(id, from: ActiveTab)`, which `MainWindowViewModel` implements (2.2, D188).

### 5.11 Automation ids and the harnesses

Ids: `CommandPalette` (the root: a Pane named `Command palette`, `DialogView`'s peer with
`AutomationProperties.Name` bound to `Title`); `CommandPaletteCard` (no control element);
`CommandPaletteBox` (Edit named `Search commands`); `CommandPaletteList` (List named `Commands`);
`CommandPaletteItem` (ListItem: `Name` the label, `ItemStatus` the detail, `HelpText` a disabled
entry's reason, `AcceleratorKey` the key text, selected while highlighted), its Text parts
`CommandPaletteItemLabel`, `CommandPaletteItemDetail`, `CommandPaletteItemKey`; `CommandPaletteEmpty`
(Text `No matches`).

The headless driver (`ShellDriver.Phase6.Palette.cs`, 17.2): `PressPalette()` (Mod+Shift+P with
`CommandModifier`); `PressShortcut(AppCommand command, int index = 0)` (the command's `index`-th key in
`AppShortcuts`, 6.4); `TypeInPalette(string text)` (replaces the box's text by typing);
`PaletteSnapshot()` → `PaletteSnapshot(string Query, Rect CardBounds, Rect BoxBounds,
IReadOnlyList<PaletteRow> Rows, bool ShowsNothing)`, the realised rows as `PaletteRow(string Label,
string? Detail, string? KeyText, bool IsEnabled, string? Reason, bool IsHighlighted, Rect Bounds,
Color LabelColour)`; `RunPaletteEntryAsync(string label, string? detail = null)` (Down to the entry,
then Enter: the keyboard only); `IsPaletteOpen`. The real window (`RealApp.Phase6.Palette.cs`):
`PressPalette()` (`PressKeys(CONTROL, SHIFT, KEY_P)`), find `CommandPalette`, type, read the rows'
`Name`, `ItemStatus`, `HelpText`, `AcceleratorKey`, Enter; count the log's `Command palette ran:`
lines.

### 5.12 Tests

Pure, in `VisualCommit.App.Tests`:

- `PaletteMatcherTests`: 5.6's rules one by one (each tier, a word start after each of its seven
  characters, every word required, case ignored, enabled first within a tier and not across tiers, the
  session's runs, source order, an empty or blank query, items only with a query); the time per query,
  measured as `DiffTextViewTests.Building_and_drawing_a_diff_grows_in_step_with_its_lines` measures
  (91d7f72): a warm-up round, then 300 and 3,000 entries timed interleaved, the best of three kept for
  each, both reported with `TestContext.Current.SendDiagnosticMessage`; it asserts only that the
  3,000-entry time is under 25 times max(the 300-entry time, 1 ms), which catches work that grows faster
  than the entries, and under a loose ceiling of 1 s. (As built: the ratio compares the UI thread's
  own work, `ThreadWork`, divided by max(the 300-entry work, 1 unit), as the diff view's growth test
  does since `b6d1d01`; the 1-s ceiling stays on the stopwatch.) Under 20 ms per query on the laptop alone is the
  indication and is not asserted: the default run's load makes a step take 5 to 8 times as long
  (`ShellDriver.cs:28-33`).
- `CommandPaletteViewModelTests`: 5.2's members (wrapping Up and Down, PageUp and PageDown by 11 to the
  ends, the highlight back on the first at each query, `ReplaceItems` keeping the query and the
  highlighted `Key`, a disabled entry's Enter leaving the result pending, `HandleEscape` answering
  null, `ShowsNothing`).
- `PaletteEntriesTests`, over phase 3's "ops" context (as `RepositoryMenusTests` builds it): every
  item equals its menu item (label, enabled, reason, destructive) for every local and remote branch,
  tag, stash, remote, the selection and the working changes, the header as detail; nothing of
  `ForDrop`, `ForEmptyGraphArea`, `ForSection`, `ForActivityLogRow`, `ConflictMenus`; 5.5's duplicate
  rules; the commands' states equal the empty graph area's and the sections' items (without remotes
  `Fetch all remotes` is disabled with `This repository has no remotes`); row 6's label, state and
  reason equal the commit button's (`Commit 1 file` enabled; `Continue rebase` disabled with `Resolve
  the conflicts first (1 file)`; `Close the conflict resolver first` with the resolver open; not listed
  without working changes or an operation); what is listed on an empty tab (no repository commands),
  on a dashboard (rows 8, 11, 12, 19) and with the search bar open (row 3); the theme in use left out;
  `Choose an editor…` listed and enabled with no editor, left out with one, and running it opens the
  settings screen on Tools;
  `Go to {title}` without the active tab, and its keys: with five tabs and tab 1 active, tab 5 shows
  `Ctrl+5`, not `Ctrl+9`; with ten, tab 8 `Ctrl+8`, tab 9 none and tab 10 `Ctrl+9`, each also with the
  lists built on a `new KeyTexts(CommandKey.Cmd)` (`Cmd+5`, `Cmd+8`, `Cmd+9`); `Open repository {name}`
  once per folder, the active one left out. The build at size: a context of 100 local branches, 200
  remote branches and 10,000 tags (some 70,000 items), the items built and the one-letter query `v`
  ranked, timed against the same context with 1,000 tags as `PaletteMatcherTests` times (best of three,
  interleaved, reported), asserting only that the larger time is under 20 times max(the smaller, 1 ms)
  (the refs grow about 8 times, so linear work about 8 times; today's linear `FindRef` made the build
  grow about 30 times in the micro-benchmark) and under a ceiling of 2 s.
- In `RepositoryMenusTests` (its owner's, 18): `FindRef` gives `FirstOrDefault`'s answer for every full
  name and for a duplicate one, and a `with` copy that replaces `Refs` finds its own refs, not the
  first context's.

Headless, `CommandPaletteViewTests` (in `VisualCommit.App.Tests`, as 17.3's measurement decided):
Mod+Shift+P opens it with the focus in its box; card and box where 5.3
puts them at 1100×700 and 1920×1080; a second Mod+Shift+P selects the box's text; it declines while a
menu or another dialog is open; typing filters; Up, Down, PageDown move the highlight into view; the
row colours of an enabled, a destructive and a disabled entry in both themes; Enter on "Create branch…"
opens the name dialog after the palette has closed, and Esc there gives the focus back to the graph;
Esc and a press on the backdrop cancel and give the focus back, the press not reaching the graph (its
selection unchanged); the rows' `Name`, `ItemStatus`, `HelpText`, `AcceleratorKey`; the two log
lines; `Open repository {name}` in each case of 5.10; a cancelled folder dialog makes no tab; a
worktree read that returns the worktrees the items were built with replaces nothing, one that differs
replaces the items (a local branch's fast-forward item changes, the query and the highlight kept).

Scripted: K10, and the palette in both themes in L31 (`l31c-palette`); real window: K10's launch and
L34's `l31c-palette-light` (17.4).

## 6. Keyboard shortcuts

### 6.1 The fixed set (D192, D199)

"Mod" is the platform's command modifier (Ctrl on Windows and Linux, Cmd on macOS), read from
Avalonia's `HotkeyConfiguration.CommandModifiers`, never from a test of the operating system (phase
5's rule). No screen changes the set. The Action is the command's name in the settings list (6.11).
The palette's labels are 5.4's and 5.5's: the same words but for row 3 (`Show the activity log` /
`Hide the activity log`), rows 9 and 10 (5.5's `Go to {title}` items), rows 15 and 16 on a dashboard
(`Fetch every repository of {workspace}`, `Pull every repository of {workspace}`), rows 21 and 22
(`Undo {text}`, `Redo {text}`), row 30 (the commit button's text, `Commit 1 file`, `Continue rebase`)
and rows 31 and 32 (the diff menu's `File history`, `Blame`); rows 1, 34 and 35 and the second table
have no palette entry. Of two keys the first listed is shown in tooltips, menus and the palette. The
router takes every key on the tunnelling pass (6.3) except 21 and 22, which keep phase 5's bubbling
rules. Where a key does not apply it is consumed and does nothing, unless its row says it declines.
Rows 15 to 33 apply on a repository tab only (15 and 16 on a dashboard too, 6.9).

| # | Action | Windows and Linux | macOS | Applies |
|---|---|---|---|---|
| 1 | Command palette (5) | Ctrl+Shift+P | Cmd+Shift+P | everywhere; with the palette open it selects its box's text |
| 2 | Open settings… (7) | Ctrl+, | Cmd+, | everywhere |
| 3 | Show or hide the activity log | Ctrl+J | Cmd+J | a repository tab; a dashboard with a row selected (2.9) |
| 4 | Refresh | F5, Ctrl+R | Cmd+R, F5 | a repository tab (refs, history, status); a dashboard (its rows, 2.10) |
| 5 | New tab | Ctrl+T | Cmd+T | everywhere |
| 6 | Close tab | Ctrl+W | Cmd+W | while the active tab can close (not the lone empty tab) |
| 7 | Next tab | Ctrl+Tab, Ctrl+PageDown | the same (Control: Cmd+Tab is the system's) | more than one tab; Ctrl+Tab declines in the resolver's result (6.5) |
| 8 | Previous tab | Ctrl+Shift+Tab, Ctrl+PageUp | the same | as 7 |
| 9 | Go to tab 1 to 8 | Ctrl+1 to Ctrl+8 | Cmd+1 to Cmd+8 | a tab at that place (the dashboards first, D187) |
| 10 | Go to the last tab | Ctrl+9 | Cmd+9 | everywhere |
| 11 | Go to the next dashboard (6.5) | Ctrl+Shift+D | Cmd+Shift+D | everywhere |
| 12 to 14 | Open repository… (5.10), Clone repository…, Init repository… | Ctrl+O, Ctrl+Shift+C, Ctrl+Shift+I | Cmd+O, Cmd+Shift+C, Cmd+Shift+I | everywhere |
| 15 | Fetch all remotes | Ctrl+Shift+F | Cmd+Shift+F | the toolbar's Fetch; on a dashboard Fetch all |
| 16 | Pull | Ctrl+Shift+L | Cmd+Shift+L | the toolbar's Pull; on a dashboard Pull all |
| 17 | Push | Ctrl+Shift+K | Cmd+Shift+K | the toolbar's Push |
| 18 | Create branch… | Ctrl+B | Cmd+B | the toolbar's Branch (at HEAD) |
| 19 | Stash changes… | Ctrl+Shift+S | Cmd+Shift+S | the toolbar's Stash |
| 20 | Pop the newest stash | Ctrl+Shift+O | Cmd+Shift+O | the toolbar's Pop |
| 21 | Undo | Ctrl+Z | Cmd+Z | the toolbar's Undo (D163); declines in a text box, the resolver, a dialog, a menu, a drag |
| 22 | Redo | Ctrl+Y, Ctrl+Shift+Z | Cmd+Shift+Z, Cmd+Y | as 21 |
| 23 | Search commits | Ctrl+F | Cmd+F | the toolbar's Search; in the search box it selects the text (phase 5) |
| 24 | Next match | F3, Ctrl+G | Cmd+G, F3 | the search bar is open; Enter in its box stays (phase 5) |
| 25 | Previous match | Shift+F3, Ctrl+Shift+G | Cmd+Shift+G, Shift+F3 | as 24; Shift+Enter in its box stays |
| 26 | Show reflog | Ctrl+Shift+R | Cmd+Shift+R | HEAD's reflog |
| 27 | Stage all | Ctrl+Shift+A | Cmd+Shift+A | unstaged changes |
| 28 | Unstage all | Ctrl+Shift+X | Cmd+Shift+X | staged changes |
| 29 | Go to the commit message | Ctrl+Shift+M | Cmd+Shift+M | working changes: their row selected, the focus in `CommitSummary` |
| 30 | Commit, in the message boxes (6.10) | Ctrl+Enter | Cmd+Enter | the focus in `CommitSummary` or `CommitDescription`; elsewhere it declines |
| 31 | File history of the open diff's file | Ctrl+Shift+H | Cmd+Shift+H | a diff is open: its menu's `File history` |
| 32 | Blame of the open diff's file | Ctrl+Shift+B | Cmd+Shift+B | a diff is open: its menu's `Blame` |
| 33 | Filter branches and tags | Ctrl+Shift+E | Cmd+Shift+E | the focus in `FilterBox`, its text selected |
| 34, 35 | Next region (6.6), Previous region | F6, Shift+F6 | F6, Shift+F6 | everywhere |

Kept from earlier phases and not taken by the router, under their settings rows (6.11):

| Action | Windows and Linux | macOS | Taken by |
|---|---|---|---|
| Context menu (of the focused object, D106) | Shift+F10, Menu | Shift+F10 | the platform on key-up; `MainWindow.OnShiftF10` where it lacks Shift+F10 |
| Close the diff, a page or the search bar (and the menu, the drag, the dialog, the activity log's focus, the resolver, the list) | Esc | Esc | `MainWindow.OnEscape`, the Esc chain (`EscapeOrder`) |
| Move in the graph; Extend the graph's selection | Up, Down, PageUp, PageDown, Home, End; each with Shift | the same | `CommitGraphControl.OnKeyDown` |
| The interactive list: Move the selected row; Pick, reword, squash, fixup, drop (Up, Down, Home, End move its selection) | Alt+Up, Alt+Down; P, R, S, F, D | Option+Up, Option+Down | `InteractiveRebaseView` |
| The resolver: Previous or next conflict; Previous or next file; Leave the result | Alt+Up, Alt+Down; Alt+Left, Alt+Right; Ctrl+Tab | Option+…; Ctrl+Tab | `ConflictResolverView`, `ResultTextArea` |
| The dashboard's list (2.7): Open the repository; Remove from workspace; Move the repository | Enter; Delete; Alt+Up, Alt+Down | Enter; Cmd+Backspace (written `Cmd+Delete`), Delete (written `Forward Delete`); Option+Up, Option+Down | the dashboard's view |
| Dialogs and menus: Confirm; Confirm from a box of several lines; Cancel or close | Enter; Ctrl+Enter (6.10); Esc | Cmd+Enter for the second | `DialogView`, the Esc chain |
| Not listed: the resolver's result's text keys (the platform's Undo, Redo, Cut, Copy, Paste, Select all); the search box's Enter, Shift+Enter, Esc; the activity log's Up, Down, Home, End, Right, Left; a dialog's Tab | | the same | the editor, `SearchBarView`, `ActivityLogView`, `DialogView` |

On a Mac keyboard F5 (Cmd+R is shown first for it), F6, Shift+F6 and Shift+F10 need Fn unless "Use
F1, F2, etc. keys as standard function keys" is on, and Home, End, PageUp and PageDown are Fn with the
arrows. No other key is added for them: a Mac has no standard key for moving between regions or
opening a context menu, and VS Code keeps F6 and Shift+F10 there. The key labelled "delete" sends
Backspace, so the dashboard list takes Cmd+Backspace (Finder's Move to Trash; with Cmd because removal
asks nothing).

Left free on purpose, checked by `AppShortcutsTests`: Mod+P (D191 left it free); Mod+N; Mod+Q, Mod+H,
Mod+M (macOS's); every Mod+Shift+digit (macOS's screenshots); every Ctrl+Alt combination (AltGr on
Windows: Ctrl+Alt+S types "ś" and Ctrl+Alt+L "ł" on a Polish layout, Ctrl+Alt+E "€" on a German one;
Linux's workspace switching); every Alt+letter (macOS's Option types characters: Option+P is "π");
Ctrl+Shift+U (IBus enters a character by its code with it; not checked with Avalonia); Ctrl+F1 to
Ctrl+F12 (KDE's and Xfce's desktops); F10 alone (Windows' menu mode), F11, F12. No key copies the
graph's SHAs (no Mod+C in the graph).

Left without a key, each two keys away in the palette (Mod+Shift+P and a word) and kept in its menu
or button (D199): `New workspace…`, `Add repository…`, `Add open tabs`, `Rename workspace…`,
`Delete workspace…` (rare set-up and edit actions; with no dashboard open Mod+Shift+D opens the
palette at `workspace`, 6.5); `Show backups`, `Create tag at HEAD…`, `Add remote…`, `Manage
remotes…`, `Show all hidden branches`, `Stop soloing` (rare, each in the empty graph area's menu or
its section's);
`Show keyboard shortcuts…` (Mod+, opens the settings screen, one click from its Keyboard shortcuts
section); the theme (one toolbar click, three palette entries); `Discard all changes…` (destructive:
it asks, and stays in its menu and the palette).

### 6.2 Which control takes a key first

1. **The window's tunnelling pass**: `KeyRouter` (6.3), added in `MainWindow`'s constructor, and phase
   3's `OnShiftF10` (no overlap). What the router takes never reaches the focused control.
2. **The focused control**: a text box, an AvaloniaEdit editor, the graph, a list, a dialog, the stock
   menu flyout (0.7).
3. **The window's bubbling pass**: undo and redo without `handledEventsToo`, so a text box or the
   resolver that took the key keeps it; the Esc chain (`OnEscape`, with `handledEventsToo`).
4. **The top level's `KeyboardNavigationHandler`** (Direct and Bubble, not `handledEventsToo`): Tab with
   any modifiers moves the focus (0.7).

The set holds no key a control owns (text editing, the arrows, Enter, Tab, Esc) but Mod+F (phase 5),
Mod+Enter in the two message boxes (6.10) and Ctrl+Tab (6.5).

### 6.3 The router

`Views/KeyRouter.cs` (no `WindowKeys` class exists). Phase 5's `MainWindow.OnSearchKey` and
`OnGitUndoOrRedo` (`MainWindow.axaml.cs:304-365`, in `MainWindow.Keys.cs` after WP0, 18) move into it;
`OnEscape` and `OnShiftF10` stay. `KeyRouter(MainWindow window)` adds `KeyDownEvent` handlers on the
tunnelling pass with `handledEventsToo: true` (as `OnSearchKey`) and on the bubbling pass without it
(as `OnGitUndoOrRedo`). On the tunnelling pass, per press:

1. `ShortcutKeys.FromEvent(e, commandModifier)` (6.4) gives a `Shortcut`, or null for a key outside
   `ShortcutKey` or a modifier the app has no word for (the Windows key, Super); the command modifier
   is read at each press from `this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers`
   (Control when it names none), as phase 5 does.
2. `AppShortcuts.Route(shortcut, commandKey)` gives the command and its argument (the place for Mod+1
   to Mod+8), or null: the key goes on.
3. **Declines** (goes on, not handled), in order: a dialog is open (`Dialogs.IsOpen`), except
   Mod+Shift+P with the palette the top dialog, which raises `SelectQueryRequested` and is handled; a
   menu is open (`Menu.IsOpen`) or a branch drag runs (`DragLayer.Service.IsDragging`); Ctrl+Tab or
   Ctrl+Shift+Tab with a `ConflictResultEditor` among the focused element's visual ancestors (6.5);
   Mod+Enter with the focus outside `CommitSummary` and `CommitDescription` (6.10).
4. Otherwise `MainWindowViewModel.Commands.Run(command, argument)` (6.4) runs it when it applies and is
   enabled, and **the key is handled either way**: a disabled command's key is consumed, as D163's
   disabled Undo. Mod+F too: phase 5 passed a disabled Search's key on (`MainWindow.axaml.cs:358-361`),
   which nothing used (the editors' search panel is removed) and no test asserts.

On the bubbling pass undo and redo behave as phase 5 built them (phase 5 design 1.3,
`MainWindow.axaml.cs:295-338`): every gesture of `HotkeyConfiguration.Undo` and `.Redo` not handled
before, declining while a dialog or a menu is open, a branch drag runs, or the focus is in a `TextBox`
or the conflict resolver; a disabled Undo or Redo takes its key.

View actions go through `IWindowKeyActions`, which `MainWindow` implements and sets on
`MainWindowViewModel.WindowActions` with its `DataContext` (no Avalonia types cross):
`FocusCommitMessage()` (`AppCommands` first sets `Repository.IsWorkingRowSelected = true`; the window
focuses `CommitSummary` once the stage panel is laid out, posted at `DispatcherPriority.Loaded`),
`FocusFilter()` (`FilterBox`, its text selected), `MoveFocusToRegion(bool backwards)` (6.6). The router
logs nothing: a key's effect logs itself.

### 6.4 The key types, the catalogue and the formatter

In `ViewModels/Commands/` (no Avalonia types):

- `AppCommand`, every command with a key or a palette entry: `CommandPalette`, `Settings`,
  `ShowKeyboardShortcuts`, `ToggleActivityLog`, `Refresh`, `NewTab`, `CloseTab`, `NextTab`,
  `PreviousTab`, `GoToTab`, `GoToLastTab`, `GoToNextDashboard`, `OpenRepository`, `CloneRepository`,
  `InitRepository`, `NewWorkspace`, `Fetch`, `Pull`, `Push`, `CreateBranch`, `Stash`, `Pop`, `Undo`,
  `Redo`, `Search`, `NextMatch`, `PreviousMatch`, `ShowReflog`, `ShowBackups`, `CreateTagAtHead`,
  `AddRemote`, `ManageRemotes`, `ShowAllHiddenBranches`, `StopSoloing`, `StageAll`, `UnstageAll`,
  `GoToCommitMessage`, `Commit`, `FileHistoryOfDiff`, `BlameOfDiff`, `FilterRefs`, `NextRegion`,
  `PreviousRegion`, `UseDarkTheme`, `UseLightTheme`, `FollowSystemTheme`; and the keys only drawn and
  listed, which their views handle: `ContextMenu`, `TextUndo`, `TextRedo`, `Cut`, `Copy`, `Paste`,
  `SelectAll`, `RebasePick`, `RebaseReword`, `RebaseSquash`, `RebaseFixup`, `RebaseDrop`, and for 6.8's
  tooltips `PreviousConflict`, `NextConflict`, `PreviousFile`, `NextFile` (the resolver's Alt+Up,
  Alt+Down, Alt+Left, Alt+Right) and `Close` (Esc, the Esc chain's).
- `readonly record struct Shortcut(ShortcutKey Key, ShortcutModifiers Modifiers)`;
  `[Flags] ShortcutModifiers { None = 0, Command = 1, Shift = 2, Alt = 4, Control = 8 }` (`Command` the
  command modifier, `Control` the Control key itself, for Ctrl+Tab and Ctrl+PageDown on macOS; one
  where the command modifier is Control); `ShortcutKey`: `A` to `Z`, `D0` to `D9`, `F1` to `F12`,
  `Enter`, `Escape`, `Tab`, `Comma`, `PageUp`, `PageDown`, `Up`, `Down`, `Left`, `Right`, `Home`, `End`,
  `Delete`, `Backspace`, `Space`, `Menu`; `CommandKey { Ctrl, Cmd }` names the platform.
- `AppShortcuts`, the catalogue as data: per command a `ShortcutBinding(AppCommand Command,
  IReadOnlyList<Shortcut> Keys, KeyPass Pass, int ShownOnCtrl = 0, int ShownOnCmd = 0)`, with
  `KeyPass { Tunnel, Bubble, View }` (`View`: drawn and listed, its view handles it). The shown key:
  Refresh F5, and Cmd+R on macOS (a Mac keyboard needs Fn for F5); Redo Ctrl+Y and Cmd+Shift+Z (the
  owner's words in D163); Next match F3 and Cmd+G; Previous match Shift+F3 and Cmd+Shift+G. `GoToTab`
  has the eight keys Mod+1 to Mod+8, its argument the digit. Members `Route(Shortcut, CommandKey)`,
  `Shown(AppCommand, CommandKey)`, `All(AppCommand)`, `Groups(CommandKey)` (6.11).
- `ShortcutText.Format(Shortcut shortcut, CommandKey platform)`, the one formatter: words, not
  symbols, on macOS too, joined by `+`: the command modifier (`Ctrl`, `Cmd`), the Control key where it
  is not the command modifier (`Ctrl`), `Shift`, `Alt` (`Option` on macOS), then the key by its
  `ShortcutKey` name, but the digit alone, `Esc` for `Escape` and `,` for `Comma`; on macOS
  `Backspace` is written `Delete` (so `Cmd+Delete`) and `Delete` `Forward Delete`, Apple's words, so
  the settings list names the key a Mac user presses. So `Ctrl+Shift+P`, `Cmd+Shift+P`, `Ctrl+,`,
  `Cmd+,`, `Ctrl+Tab` on both, `Alt+Up`, `Option+Up`, `Shift+F10`, `F6`.
  `FormatAll(IReadOnlyList<Shortcut>, CommandKey)` joins keys with `, `, and a run of `GoToTab`'s reads
  `Ctrl+1 to Ctrl+8`. This one text is the palette's, the menus', the tooltips', the settings list's
  and UI Automation's `AcceleratorKey`.
- `KeyTexts(CommandKey platform)`: `Of(AppCommand)` (the shown key's text or null),
  `WithKey(string text, AppCommand command)` (`{text} ({key})`, or the text alone). `KeyTexts.Current`
  is set once by `AppSession.Start`, before it makes the window (`AppSession.cs:178`), from
  `application.PlatformSettings?.HotkeyConfiguration.CommandModifiers` (Meta → `Cmd`, else `Ctrl`),
  `Ctrl` until then. The headless platform gives
  Control (0.7), so tests that start the app agree; a test of the Cmd texts makes its own
  `new KeyTexts(CommandKey.Cmd)` or calls `ShortcutText.Format`. `ToolbarViewModel.SearchGestureText`
  (`ToolbarViewModel.cs:287-290`) becomes `KeyTexts.Current.Of(AppCommand.Search)`, ending its read
  of `Application.Current`.
- `AppCommands` (`MainWindowViewModel.Commands`, made with a `KeyTexts`: `KeyTexts.Current` in the app,
  `new KeyTexts(CommandKey.Cmd)` in a test of the Cmd texts), for the active tab: `Describe(AppCommand)` →
  `CommandState(bool IsListed, bool IsEnabled, string Label, string? Detail, string? DisabledReason)`
  from 5.4's sources; `Run(AppCommand, int argument = 0)` runs it when listed and enabled and returns
  whether it ran; `PaletteCommands()` and `PaletteItems()` build 5.4's and 5.5's lists. The router, the
  palette and the tests share these states, so a key and its entry are enabled together.

In `Views/`: `ShortcutKeys.FromEvent(KeyEventArgs e, KeyModifiers command)` maps `Key` (both names of a
duplicate value; `NumPad1` to `NumPad9` as the digits; `OemComma` as `Comma`; `Apps` as `Menu`;
`Back` as `Backspace`) and the
modifiers (`command` → `Command`; Control on a Meta platform → `Control`; Shift; Alt; Meta on a Control
platform → null); `ShortcutKeys.ToGesture(Shortcut, KeyModifiers command)`, the reverse, serves the
harnesses.

### 6.5 Tab keys

- **Ctrl+Tab, Ctrl+Shift+Tab**: the next and previous tab in the strip's order (dashboards first,
  D187), wrapping, with Control on every platform (Cmd+Tab is macOS's application switcher); the
  strip's order, as a browser's and predictable in a picture, not the most recently used (VS Code's).
  Taken on the tunnelling pass, so `KeyboardNavigationHandler` never sees them (0.7): Avalonia's
  Ctrl+Tab focus move is given up; Tab and Shift+Tab still move the focus. They decline with the focus
  in the resolver's result, where `ResultTextArea` leaves Tab with Control to the navigation handler,
  which moves the focus out (phase 4 design 11.10): Tab types a tab there and Ctrl+Tab leaves.
- **Ctrl+PageDown, Ctrl+PageUp**: the same everywhere, the resolver's result included (AvaloniaEdit
  binds PageUp and PageDown with none and with Shift only, 0.7).
- **Mod+1 to Mod+8**: the tab at that place (with dashboards open Mod+1 is the first dashboard); no tab
  there: consumed. **Mod+9**: the last tab.
- **Mod+Shift+D**, "Go to the next dashboard": with dashboards open, the next one after the active tab
  among them (the first from a tab that is not a dashboard), wrapping; with none, the palette with
  `workspace` in its box, which lists `Open workspace {name}` and `New workspace…`.
- **Mod+W**: the active tab's close button (`CloseTabCommand`), so only that button shows the key
  (6.8); on the lone empty tab (`CanClose` false) consumed. **Mod+T**: "+" (`NewTabCommand`).

Activating a tab by a key saves the session as a click does (`Activate`).

### 6.6 F6 and Shift+F6: the regions

| Order | Region (its root) | Shown | Where the focus goes |
|---|---|---|---|
| 1 | Left panel (`LeftPanelView`) | not on a dashboard | `RefList`'s selected row, else its first row, else `FilterBox` |
| 2 | Graph area (row 0 of `CommitGraphView`) | always | what shows there: the resolver's `ConflictResult` (its card's first button on a binary or deleted conflict), `DiffText`, the page's `FileHistoryList`, `BlameText` or `ReflogList`, `RebaseRows`' selected row, `GraphRows`, the welcome page's `OpenRepoButton`, the dashboard's list |
| 3 | Right panel (`RightPanelView` or `WorkingChangesView`) | not with the resolver open, not on a dashboard | `FileList`'s or `CompareFileList`'s first row, `MultiSelectionList`, or the stage panel's `UnstagedFileList` first row, else `StagedFileList`'s, else `CommitSummary` |
| 4 | Activity log (`ActivityLogView`) | while shown | `ActivityLogList` |

F6 goes to the next region, Shift+F6 to the previous, wrapping, from the one holding the focus (none:
F6 starts at 1, Shift+F6 at the last shown); a region not shown or with nothing focusable is skipped.
Each region remembers its last focused element (`GotFocus` within its root), which wins while it is
still visible, enabled and inside, so F6 four times comes back to where the user was. Marks, in
`Views/KeyRegions.cs`: the attached `KeyRegion.Role` (`Left`, `GraphArea`, `Right`, `ActivityLog`) on
the four roots and `KeyRegion.IsMain` on the elements named above, tried in the table's order (an items
control gives its selected or first focusable item): `RefList`, `FilterBox`, `GraphRows`, `DiffText`
(set in code beside its automation id, on the left editor side by side, `DiffTextView.cs:551`),
`FileHistoryList`, `BlameText`, `ReflogList`, `RebaseRows`, `ConflictResult`, `OpenRepoButton`,
`FileList`, `CompareFileList`, `MultiSelectionList`, `UnstagedFileList`, `StagedFileList`,
`CommitSummary`, `ActivityLogList` and `DashboardList`. The roles and the marks in the existing views
are WP0's, data that nothing reads until WP-K's navigation, so no view behaves or draws differently
before it; `DashboardList`'s is WP-D's, in its new view (18). The resolver's binary or deleted conflict
has nothing to mark (its card's buttons are made per conflict in `CardButtons`, without an id): there
the target is the one `FocusGraphAreaContent` picks (`CommitGraphView.axaml.cs:191-213`, the card's
first button), which WP-D turns into a method returning the element, `GraphAreaFocusTarget()`, for the
navigation to call. Without a mark, the region's first focusable element in Tab order (in
`LeftPanelView` that would be `FilterBox` before `RefList`, in `WorkingChangesView` `StageFlatButton`
before `UnstagedFileList`, hence the marks). The focus moves with `NavigationMethod.Tab`, so the
keyboard outline shows.

### 6.7 Keys in menus

`MenuItem` (`ViewModels/Menus/MenuModel.cs:24-29`) gains an init property `AppCommand? Command`, set
only where the item **runs the keyed command on the same object**, through a new optional
`AppCommand? command` argument of `RepositoryMenus.Item(...)` (`RepositoryMenus.cs:828-834`); the
positional constructor and its other callers stay.

| Menu (builder) | Items and their commands |
|---|---|
| The empty graph area (`ForEmptyGraphArea`, `RepositoryMenus.cs:565`) | `Fetch all remotes` Fetch, `Pull` Pull, `Push` Push, `Create branch…` CreateBranch, `Stash changes…` Stash, `Show reflog` ShowReflog |
| The working changes (`ForWorkingChanges`, `:154`) | `Stage all` StageAll, `Unstage all` UnstageAll, `Stash changes…` Stash, `Create branch here…` CreateBranch (both call `CreateBranchAsync(null)`, `RepositoryOperations.cs:58`), `Show reflog` ShowReflog |
| The sections (`ForSection`, `:402`) | Local branches: `Create branch…` CreateBranch, `Show reflog` ShowReflog; Remotes: `Fetch all remotes` Fetch; Stashes: `Stash changes…` Stash |
| The checked-out branch (`ForLocalBranch`, `:184`, `isCurrent`) | `Pull from {up}` Pull (it calls the toolbar's `actions.PullAsync`), `Push {b} to {up}` and `Push {b}…` Push (the toolbar's Push pushes the current branch alike) |
| The newest stash (`ForStash`, `:129`, `stash@{0}` only) | `Pop stash` Pop |
| The open diff (`ForDiff`, `:514`, through `FileHistoryItems`, `RepositoryMenus.Pages.cs:180`) | `File history` FileHistoryOfDiff, `Blame` BlameOfDiff |
| The resolver's result (`ConflictMenus.ForConflictResult`, `ConflictMenus.cs:40`); a pane (`ForConflictPane`, `:61`) | `Undo` TextUndo, `Redo` TextRedo, `Cut`, `Copy`, `Paste`, `Select all` (the editor's text gestures); a pane's `Copy` |
| The interactive list's row (`InteractiveRebaseViewModel.MenuFor`, `:357`; it opens on the selected row, `InteractiveRebaseView.axaml.cs:356-379`) | `Pick` RebasePick (`P`), `Reword…` (`R`), `Squash` (`S`), `Fixup` (`F`), `Drop` (`D`) |

Not keyed: another branch's pull or push, `Pull from {up} (rebase)` (another command), a file row's
`File history` and `Blame` (another file), `Discard all changes…`.

**Drawing.** `MenuLayer.CreateItem` (`MenuLayer.axaml.cs:449-489`) gives a keyed item, in place of
`LabelTemplate`, a header template made per item (`FuncDataTemplate<string>`, the key text captured):
a `Grid` of columns `*,Auto`, the label's `TrimmedTextBlock` as now (still cut with "…" past the widest
menu) and, 24 to its right, right-aligned, a plain `TextBlock` `MenuItemKey` (class `vcMenuKey`,
automation id `ContextMenuItemKey`; not a `TrimmedTextBlock`, so tests counting a menu's labels count
the same). The `Header` stays the label (the harnesses find items by it); the item's
`AutomationProperties.AcceleratorKey` is the key text. `Theme/Menus.axaml` stretches
`ContentPresenter#PART_HeaderPresenter` (`HorizontalAlignment="Stretch"`: the key at the item's right
edge whatever Fluent's template gives) and styles `TextBlock.vcMenuKey` (`VcTextSecondaryBrush`, 12.5;
`VcTextDisabledBrush` under `MenuItem:disabled`); `PART_InputGestureText` stays hidden.

**Sizes.** Heights stay (26 per item, `WindowLayout.MenuHeight`). A menu is as wide as its widest item,
200 to 520 (`MenuLayer.MinMenuWidth`, `MaxMenuWidth`); a keyed item needs 10 + label + 24 + key + 10,
so a menu of the table grows where a keyed item is its widest (16.1); at 520 the label is cut and the
key stays whole (the `Auto` column is measured first).

### 6.8 Tooltips, help texts and AcceleratorKey

**An enabled button whose command has a key says `{what it does} ({key})`; a disabled one keeps its
reason alone** (phases 3 to 5's pictures of disabled tooltips stay), and one without a reason has none.
Its `AutomationProperties.HelpText` is its tooltip (architecture.md's convention), its
`AutomationProperties.AcceleratorKey` the key text, enabled or not (precedents: `Search commits
(Ctrl+F)`, phase 5's `Next match (Enter)`). Phase 3 design 4d's "an enabled toolbar button has no
tooltip" goes for every button below, as phase 5 broke it for Undo, Redo and Search. Every keyed button
follows the rule, the commit button and the resolver's included (D199); as in menus (6.7), a button
shows a key only when the key runs its command on the same object.

| Button | Enabled tooltip (disabled: its reason) |
|---|---|
| `FetchButton`, `PullButton` | `Fetch all remotes ({key})`, `Pull from {up} ({key})`; on a dashboard 4.10's texts and reasons, the key appended (`Fetch every repository of {workspace} ({key})`) |
| `PushButton` | `Push {b} to {up} ({key})` with a remote upstream (`UpstreamText`), else `Push {b} to a remote ({key})` |
| `BranchButton`, `StashButton`, `PopButton` | `Create a branch at HEAD ({key})`, `Stash your changes ({key})`, `Pop the newest stash ({key})` |
| `UndoButton`, `RedoButton`; `SearchButton` | `Undo {text} ({key})`, `Redo {text} ({key})` (reasons: phase 5 design 7.4); `Search commits ({key})`, unchanged |
| `SettingsButton` (7.2) | `Open settings ({key})`; never disabled |
| `CloseTabButton` | on the active tab `Close this tab ({key})`; on another tab `Close this tab`, without `AcceleratorKey`: Mod+W closes the active tab only (6.5), so on another tab's button the key would close a different tab |
| `AddRepoButton` ("+") | `Open, clone or init a repository ({key})` (New tab's key) |
| `ActivityLogToggle` | `Show every git command the app has run ({key})`, `Hide the activity log ({key})`; on a dashboard 2.9's reasons, the first that holds; on an empty tab (disabled) the first text with its key, as today's text shows there |
| `OpenRepoButton`, `CloneRepoButton`, `InitRepoButton` | `Open a repository on this computer ({key})`, `Clone a repository from a URL ({key})`, `Create a new repository in a folder ({key})` |
| The stage panel's `CommitButton` | `{its text} ({key})`: `Commit 1 file (Ctrl+Enter)`, `Continue rebase (Ctrl+Enter)` (the key works from the message boxes, 6.10; 5.4's row 6 runs it from the palette); disabled its reason as today (`Resolve the conflicts first (1 file)`), or none |
| The resolver's `ResolverPreviousConflict`, `ResolverNextConflict`, `ResolverPreviousFile`, `ResolverNextFile`, `ResolverCloseButton` | `Previous conflict ({key})`, `Next conflict ({key})`, `Previous file ({key})`, `Next file ({key})` (`Alt+Up`, `Alt+Down`, `Alt+Left`, `Alt+Right`; `Option+Up` and so on on macOS), `Close (Esc)`; disabled none, as today (they do not set `ToolTip.ShowOnDisabled`) |
| The interactive list's `RebaseCancelButton` | `Cancel (Esc)`. Its other buttons run no keyed command: `RebaseStartButton` keeps `StartToolTip`, the row's `RebaseActionButton` opens the row menu, whose items show P, R, S, F and D (6.7), and `RebaseEditMessageButton` has no key |
| The search bar's `SearchPrevious`, `SearchNext` | `Previous match (Shift+Enter, {key})`, `Next match (Enter, {key})`: `Previous match (Shift+Enter, Shift+F3)`, `Next match (Enter, F3)`; on macOS `Cmd+Shift+G` and `Cmd+G` (Enter in the box, the command's shown key everywhere, 6.1 rows 24 and 25); `AcceleratorKey` the shown key |

`ThemeSwitch` has no key (11.6's texts; 6.1's list, D199). Phase 2 to 5's expectations that pin a null
enabled tooltip on the commit button, the resolver's fixed texts or the search bar's `Next match (Enter)`
and `Previous match (Shift+Enter)` change with them (16.1).

**`XDisabledReason`.** `FetchToolTip` to `PopToolTip` hold the disabled reason and are null while
enabled (`ToolbarViewModel.cs:143-165`), and `ToolbarMenuState.Of` reads them as reasons
(`RepositoryMenuContext.cs:26-36`), so a tooltip that speaks while enabled would become a menu item's
reason. `ToolbarViewModel` gains `FetchDisabledReason`, `PullDisabledReason`, `PushDisabledReason`,
`BranchDisabledReason`, `StashDisabledReason`, `PopDisabledReason`, `UndoDisabledReason`,
`RedoDisabledReason`, `SearchDisabledReason` (observable; null while enabled or with no reason known),
set where `SetStates`, `UpdateUndoStates` and `UpdateSearchState` set the tooltips; each `XToolTip`
becomes `XDisabledReason ?? (CanX ? KeyTexts.Current.WithKey(enabledText, command) : null)`, and
`ToolbarMenuState.Of` reads `CanX` and `XDisabledReason`. (Not `StashReason` and so on: the static
`ToolbarViewModel.StashReason(HeadState, WorkingTreeStatus)` exists, `ToolbarViewModel.cs:388`.)
`UndoAvailability` keeps `Undo {text}`; the toolbar appends the key. The stage panel likewise:
`WorkingChangesViewModel` gains `CommitDisabledReason` (today's `CommitToolTip`,
`WorkingChangesViewModel.cs:212-215`), which 5.4's row 6 reads, and `CommitToolTip` becomes
`CommitDisabledReason ?? (CanCommit ? KeyTexts.Current.WithKey(CommitButtonText, AppCommand.Commit) :
null)`.

**Bindings.** `ToolbarView.axaml` binds every button's `AutomationProperties.HelpText` to its
`XToolTip` (today only Undo's, Redo's and Search's) and `AutomationProperties.AcceleratorKey` to
`Keys.Fetch` and so on, `MainWindowViewModel.Keys` being a `ShellKeys` record of the shown key texts
(`Palette`, `Settings`, `NewTab`, `CloseTab`, `Fetch`, `Pull`, `Push`, `Branch`, `Stash`, `Pop`, `Undo`,
`Redo`, `Search`, `ActivityLog`, `Open`, `Clone`, `Init`) from `KeyTexts.Current`. The other shell
texts with keys come from `ViewModels/Commands/ShellToolTips.cs` (`CloseTab`, `CloseOtherTab`, `NewTab`,
`ShowActivityLog`, `HideActivityLog`, `OpenRepository`, `CloneRepository`, `InitRepository`, from
`KeyTexts.Current`): `RepoTabsView.axaml` binds "+"'s tooltip and help text to it, `WelcomeView.axaml`
its three buttons', `ActivityLogViewModel.ToggleToolTip` takes its two texts from it, and
`StatusBarView.axaml` binds the toggle's key. The close button follows its tab: `RepoTabViewModel`
gains `CloseToolTip` and `CloseKeyText` (observable, raised when `IsActive` changes): `CloseTab` and
the key text on the active tab, `CloseOtherTab` (`Close this tab`) and null on the others;
`RepoTabsView.axaml` binds the button's tooltip and help text to `CloseToolTip` and its
`AcceleratorKey` to `CloseKeyText`.

The buttons outside the shell take their texts and keys from `ShellToolTips` too, read with `x:Static`
as `SearchBarView.axaml:64-78` reads `SearchViewModel`'s texts today (`KeyTexts.Current` is set before
the window is made, 6.4): `PreviousConflict`, `NextConflict`, `PreviousFile`, `NextFile` and
`CloseResolver` replace `ConflictResolverView.axaml`'s fixed tooltips (`:80-132`), and `CancelList`
gives `RebaseCancelButton` one, each with a `{name}Key` beside it for `AcceleratorKey` and the tooltip
as help text; `SearchViewModel.PreviousToolTip` and `NextToolTip` (constants today,
`SearchViewModel.cs:63-65`) become static properties built the same way, with `PreviousKey` and
`NextKey` beside them; `WorkingChangesView.axaml` binds `CommitButton`'s help text to `CommitToolTip`
and its `AcceleratorKey` to `ShellToolTips.CommitKey`, enabled or not.

### 6.9 The keys on a dashboard

The toolbar's Fetch and Pull are Fetch all and Pull all there (4.10), so Mod+Shift+F and Mod+Shift+L
run them: the router runs `ActiveTab.Toolbar.FetchCommand` and `PullCommand` on every kind of tab. F5
and Mod+R refresh the rows (2.10); Mod+J shows the selected row's activity (2.9); the tab keys, Mod+O,
Mod+Shift+C, Mod+Shift+I, Mod+Shift+P, Mod+, and F6 work as everywhere. Every other key of the set is
consumed, as on an empty tab: the other toolbar buttons are disabled without a reason (Mod+Z and Mod+Y
by D163: a disabled Undo takes its key). Enter, Delete (on macOS also Cmd+Backspace), Alt+Up, Alt+Down
and Shift+F10 are the list's (2.7).

### 6.10 Commit and dialogs: Mod+Enter

- **The stage panel**: Mod+Enter with the focus in `CommitSummary` or `CommitDescription` runs the
  panel's `CommitCommand` (`WorkingChangesViewModel.cs:439`) when it can run (its button says what:
  `Commit 1 file`, `Continue rebase`), handled either way. Taken on the way down: a `TextBox` with
  `AcceptsReturn` types a line break for Enter with any modifier (0.7) and the description accepts
  Return, so the way up comes too late. Plain Enter still types a line break in the description and
  does nothing in the summary. The button's tooltip names the key (6.8), and the palette's row 6 (5.4)
  runs the same command from anywhere it is listed.
- **A dialog**: `DialogView` takes Mod+Enter on its own tunnelling pass when the source is a `TextBox`
  with `AcceptsReturn` (the message dialog's `MessageDescription`, phase 4) and runs `ConfirmButton` as
  its Enter does (`DialogView.cs:75-89`), handled either way; plain Enter there types a line break. The
  router declines while a dialog is open, so the two never meet.

### 6.11 The settings screen's list

The settings screen's Keyboard shortcuts section (7.6 lays it out; read-only, D192) lists
`AppShortcuts.Groups(KeyTexts.Current.Platform)`: `ShortcutGroup(string Title,
IReadOnlyList<ShortcutRow> Rows, string? Note)` of `ShortcutRow(string Action, string Keys)`: each
row's text and keys are 6.1's (both tables), the keys joined by `, ` (`FormatAll`), the shown key first,
in the platform's words (`F5, Ctrl+R`, `Ctrl+1 to Ctrl+8`; on macOS `Cmd+R, F5`, `Cmd+Shift+Z, Cmd+Y`,
`Cmd+G, F3`, Ctrl kept for Ctrl+Tab, Ctrl+PageDown, Ctrl+PageUp, Option for Alt, the context menu
`Shift+F10` alone). A row of several commands joins their keys in its own order (`Pick, reword,
squash, fixup, drop`: `P, R, S, F, D`; `Previous or next conflict`: `Alt+Up, Alt+Down`). The action
is 6.1's name, not the palette's label where that differs (6.1). The groups:

| Group | Rows |
|---|---|
| App | Command palette; Open settings…; Show or hide the activity log; Refresh |
| Tabs | New tab; Close tab; Next tab; Previous tab; Go to tab 1 to 8; Go to the last tab; Go to the next dashboard |
| Repositories | Open repository…; Clone repository…; Init repository… |
| Repository | Fetch all remotes; Pull; Push; Create branch…; Stash changes…; Pop the newest stash; Undo; Redo; Search commits; Next match; Previous match; Show reflog. Note: `On a dashboard, Fetch and Pull fetch and pull every repository of its workspace.` |
| Changes | Stage all; Unstage all; Go to the commit message; Commit, in the message boxes |
| Files | File history of the open diff's file; Blame of the open diff's file; Close the diff, a page or the search bar |
| Moving around | Filter branches and tags; Next region; Previous region; Context menu; Move in the graph; Extend the graph's selection. Note, on macOS only: `On a Mac keyboard F6, Shift+F6 and Shift+F10 need Fn unless "Use F1, F2, etc. keys as standard function keys" is on; Home, End, PageUp and PageDown are Fn with the arrows.` |
| Interactive rebase | Move the selected row; Pick, reword, squash, fixup, drop |
| Conflict resolver | Previous or next conflict; Previous or next file; Leave the result |
| Dashboard | Open the repository; Remove from workspace (`Delete`; on macOS `Cmd+Delete, Forward Delete`); Move the repository |
| Dialogs and menus | Confirm; Confirm from a box of several lines; Cancel or close |

### 6.12 Tests

Pure, in `VisualCommit.App.Tests`:

- `ShortcutTextTests`: every binding formatted for `Ctrl` and `Cmd` against a table written out by hand
  (6.4's examples, `Ctrl+PageDown` on both, the shown Refresh `F5` and `Cmd+R`, the shown Redo `Ctrl+Y`
  and `Cmd+Shift+Z`, `Ctrl+9`, the dashboard's Remove `Cmd+Delete` and `Forward Delete` on macOS and
  `Delete` on Windows and Linux); `FormatAll`'s `, ` and `Ctrl+1 to Ctrl+8`; `KeyTexts.WithKey` with and
  without a key; never from a test of the operating system (D163's rule).
- `AppShortcutsTests`: no two of the router's commands (`Tunnel`, `Bubble`) share a key on either
  platform (`Command` and `Control` one for `Ctrl`), nor one of them a `View` key (`View` keys repeat
  across views: Alt+Up is the resolver's, the interactive list's and the dashboard's); no tunnelling key
  is a text gesture of 0.7 (written out for Win32 and macOS; Undo and Redo go by the bubbling pass) or
  AvaloniaEdit's Ctrl+D, Ctrl+I, Ctrl+H, nor one 6.1 leaves free; every key in exactly one settings
  group; every `Tunnel` command has an `AppCommands` case (a table over the enum, no reflection); the
  palette's commands without a key are exactly 6.1's list (D199), written out by hand.
- `ToolbarToolTipTests` (a new file, as 18 puts new tests): every `XDisabledReason` null while enabled
  and the old tooltip while disabled; 6.8's enabled tooltips with their keys; `ToolbarMenuState.Of`
  gives an enabled button no reason (so the empty graph area's enabled items have none); the stage
  panel's `CommitDisabledReason` equal to today's `CommitToolTip` in each state of
  `WorkingChangesInProgressTests`, and `CommitToolTip` `Commit 1 file (Ctrl+Enter)` and `Continue
  rebase (Ctrl+Enter)` while enabled.
- `RepositoryMenusKeyTests` (a new file): exactly 6.7's items carry their `Command`: the checked-out
  branch's `Pull from {up}` and `Push {b} to {up}`, not another branch's; only `stash@{0}`'s
  `Pop stash`; the diff's `File history` and `Blame`, not a file row's.

Headless (in `VisualCommit.App.Tests`, as 17.3's measurement decided), with 17.2's
`ShellDriver.Phase6.Keys.cs`:

- `WindowKeysTests` unchanged: its six tests (`WindowKeysTests.cs:27-192`) pass, now through the router.
- `KeyRouterTests`: each tunnelling key of 6.1 pressed by `ShellDriver.PressShortcut` in the "ops"
  scenario, with recording toolbar actions (`ToolbarViewModel.Actions`) and a recording
  `IWindowKeyActions`: the command ran once and the key was handled; each declines with a menu or a
  dialog open and during a branch drag; a disabled command's key is handled and runs nothing (Mod+F with
  the resolver open included); Mod+Shift+P with the palette open selects its text; a key with the
  Windows key held is not taken. Every tunnelling key is pressed within one "ops" app start per
  condition (plain, a menu open, a dialog open, a branch drag), as `WindowKeysTests` presses several
  keys per start, not one start per key (17.6).
- `TabKeysTests`: Ctrl+Tab and Ctrl+Shift+Tab through three tabs and round; Ctrl+Tab in the resolver's
  result leaves it and keeps the tab (phase 4 design 11.10), Ctrl+PageDown there switches; Mod+1 to
  Mod+8, a place without a tab, Mod+9; Mod+W, and nothing on the lone empty tab; Mod+Shift+D with two
  dashboards and with none (the palette with `workspace`); the session saved after a switch. Its keys
  share an app start per setup (17.6).
- `RegionKeysTests`: F6 and Shift+F6 round 6.6's regions on a repository tab, with the resolver open (no
  right panel), with the activity log open, on a dashboard; each region's first visit lands on 6.6's
  target (`RefList`'s selected row, not `FilterBox`; `UnstagedFileList`'s first row, not
  `StageFlatButton`; a binary conflict's first card button); a region's last focused element wins. Its
  keys share an app start per setup (17.6).
- `CommitKeyTests`: Mod+Enter in `CommitSummary` and `CommitDescription` commits (the recorded commit's
  message) and types no line break; outside them it is not handled; disabled (no summary) handled and
  nothing committed; in the message dialog's description it confirms; the palette's row 6 commits with
  the focus in the graph.
- `MenuKeyColumnTests`: a keyed item's key right-aligned, 24 from its label, `VcTextSecondaryBrush`
  (`VcTextDisabledBrush` disabled), both themes; its `AcceleratorKey`; the menu as wide as its widest
  item, key included; a label cut at 520 keeps its key whole; the `Header` still the label.
- `ShellToolTipTests`: 6.8's tooltips, help texts and `AcceleratorKey`s of the toolbar, the tab strip,
  the status bar's toggle and the welcome page, enabled and disabled; the active and an inactive tab's
  close button (`Close this tab (Ctrl+W)` with its key, `Close this tab` without), swapped after a click
  on the other tab and after Ctrl+Tab; the commit button (`Commit 1 file (Ctrl+Enter)`, a disabled one's
  reason alone, its `AcceleratorKey` either way); the resolver's five buttons (`Previous conflict
  (Alt+Up)` to `Close (Esc)`); the interactive list's `Cancel (Esc)`; the search bar's `Next match
  (Enter, F3)` and `Previous match (Shift+Enter, Shift+F3)`.

Scripted: K11 (keyed menus, tooltips), K12 (every main action from the keyboard alone, asserted by the
commands run: the Done when's "every main action is reachable from the keyboard"), K13 (the resolver's
result, a dashboard); real window: K10's launch also presses Ctrl+Tab and F6 and reads `FetchButton`'s
and a menu item's `AcceleratorKey` (17.4). Expected key texts are literals in the expectations
(`"Ctrl+Shift+F"`), never the app's formatter's output, so a formatter fault is caught.

## 7. The settings screen

Delivers: "Settings screen (identity, pull mode, auto-fetch interval, external tools)"; phase 3's
`AutoFetchMinutes`, `StashIncludeUntracked` and the extensions Open file asks about; the places of
D190's git settings (section 8), D192's key list (6.11), D193's switch (section 9) and D194's System
theme (section 11).

### 7.1 A large dialog

`SettingsDialogViewModel : DialogViewModel<bool>` (`ViewModels/Dialogs/`; true after a Save that
closed it, false for Cancel and Esc) with `GeneralSettingsViewModel`, `GitSettingsViewModel` and
`ToolSettingsViewModel` (`ViewModels/Settings/`); its view `Views/Dialogs/SettingsDialogView.axaml`,
found by name. A dialog of the dialog layer (D69), because both harnesses capture and drive that
layer, because a modal dialog keeps "This repository" fixed while it is open, and because it has
every dialog's Esc, Enter, Tab cycling, focus return and stacking (D104). A page in the graph area is
440 wide at 1100×700 (D61); a tab would leave "This repository" undefined. Auto-fetch waits while it
is open (`AutoFetchScheduler.Tick`, `ViewModels/Operations/AutoFetchScheduler.cs:123`).

No combo box (its drop-down is a window outside the headless capture, D141) and no menu (none opens
over a dialog, `MainWindowViewModel.cs:62-65`): radio buttons (`VcRadioButton`,
`Theme/Dialogs.axaml:22-80`), check boxes (`VcCheckBox`), text boxes, and a Pick dialog stacked on
top for a long list (7.7).

**The card.** Class `settings` on `Border.dialogCard` (`Theme/Dialogs.axaml`): `MaxWidth=860`,
`MaxHeight=680`, `Margin=40`, both alignments `Stretch`, `Padding=20`, border and corner as every
card, centred when its maximum holds it (the builder confirms that Avalonia 12.1.3 centres a
stretched element limited by its maximum; if not, the view sets `Width = min(860, W − 80)` and
`Height = min(680, H − 80)` from the dialog layer's bounds). Inside, a `Grid` with rows
`Auto, 14, *, Auto`: the title `SettingsTitle` `Settings` (`dialogTitle`, about 21 high); 14 of
space; the body, a `Grid` with columns `176, 20, *` holding the section list `SettingsNav` and the
shown section in a vertical `ScrollViewer` `SettingsSection`; the buttons (`dialogButtons`, 20 above,
30 high) `CancelButton` `Cancel` and `ConfirmButton` `Save` (`VcPrimaryButton`), with `SettingsSaving`
(secondary, 7.8) left of them while a save waits.

| Window (logical px) | Card (x, y, width × height) | Body height | Content column | Fits without scrolling |
|---|---|---|---|---|
| 1000×560 (the minimum) | 70, 40, 860 × 480 | about 353 | 622 | General (about 378) and Git (about 424) scroll a little; Tools fits |
| 1100×700 (the walk-through's size) | 120, 40, 860 × 620 | about 493 | 622 | General, Git and Tools; Keyboard shortcuts scrolls |
| 1280×800 (the default) | 210, 60, 860 × 680 | about 553 | 622 | as 1100×700 |
| 1920×1080 | 530, 200, 860 × 680 | about 553 | 622 | as 1100×700: the card does not grow, so lines stay short |

Heights are estimates; the scripted checks assert the card's bounds at 1100×700 and 1920×1080
exactly, and no control outside the card at 1000×560.

**The section list.** `SettingsNav`, a `StackPanel` (spacing 2) of four `VcFlatButton`s, 176×30, text
left-aligned with `Padding=10,0`, the shown one `selected` (phase 5's look): `SettingsNavGeneral`
`General`, `SettingsNavGit` `Git`, `SettingsNavTools` `Tools`, `SettingsNavShortcuts` `Keyboard
shortcuts`. A section with a validation error after Save gets a 6×6 `VcDangerBrush` dot at its
button's right (`SettingsNavError`; the button's help text `Has an error`). Up and Down move between
the buttons; a click or Enter shows the section without focusing into it.

**The form.** Each section is a `Grid` with columns `132, 12, *`: labels (`TextBlock.settingsLabel`:
`VcTextSecondaryBrush`, top-aligned with their control's first line), controls in the third column
(464 wide at 1100×700: 622 − 144 − 14 for the scroll bar). Text boxes at most 320 wide; an
explanation is a second line (`secondary`, 11.5, wrapping, 4 below); rows 18 apart. Errors
`dialogError`; warnings its layout in `VcWarningBrush` (`TextBlock.dialogWarning`). No new colour
token.

**The first focus.** `DialogView.FocusOnOpening` (`Views/Dialogs/DialogView.cs:50`) focuses the
first text box. `DialogView` gains `protected virtual bool FocusesTextBoxOnOpening => true`; the
settings view returns false and names `InitialFocus` (`:39`), the shown section's nav button. No
other dialog changes.

### 7.2 Where it opens from

| Entry | What | Opens on |
|---|---|---|
| The toolbar's `SettingsButton` | A `ToolbarButton`, label `Settings`, `IconSettings` (Lucide's `settings`, 15.4), at the start of the toolbar's right-hand group: `Settings`, `Search`, `Theme` (`Views/Shell/ToolbarView.axaml:20-31`) (D197, amending the toolbar row of requirements.md's main window layout, D20, D22). Always enabled, as Theme is. Tooltip and help text `Open settings ({key})` (6.8) | General |
| `Mod+,` | `Ctrl+,`, `Cmd+,` on macOS (6.1); declines while a dialog is open (6.3) | General |
| The palette | `Open settings…`; `Show keyboard shortcuts…`; `Choose an editor…`, listed while no editor is chosen (5.4, 12.1) | General; Keyboard shortcuts; Tools |
| The editor failure's error dialog (12.7) | its extra button `Open settings…` | Tools |

Left of Search the button takes toolbar background that is empty in every earlier picture (the group
is docked right), Search and Theme keep their places, and phase 0's real-window assertion that Theme
ends within 20 of the right edge (`tests/VisualCommit.RealWindowTests/Phase0RealWindowPass.cs:141`)
holds; at 1000 wide the left group ends at about 454 and the right group, now about 166 wide, starts
at about 828. The cost: **every window picture of phases 0 to 5 changes** in one box of about 56×52
left of Search, so the gate compares earlier pictures by region against references made on `master`
(17.7); 16.1 lists the expectations.

`MainWindowViewModel.OpenSettingsCommand`, generated from `[RelayCommand] OpenSettings(SettingsSection?
section)` (`MainWindowViewModel.Settings.cs`), null meaning General, so that `SettingsButton` binds it
without a `CommandParameter`, as the other toolbar buttons bind theirs (CommunityToolkit.Mvvm 8.4.2's
`RelayCommand<T>.CanExecute(null)` is false for a value type that is not nullable, whatever the method's
default, which would leave the button disabled); `Mod+,` (through `AppCommands.Run`), the palette and
12.7's button pass the section, typed. Run by every entry, it builds the dialog with the settings
store, the shell's `UseTheme` (11.6), the active tab's repository (`ActiveTab.Repository`: null on an
empty or a dashboard tab and while its refs are unread), the git settings service (8.1), the tool
catalogues (12.9), 6.11's shortcut groups and the git detection, and shows it with
`Dialogs.ShowAsync`.

### 7.3 General

When each takes effect is 7.9's.

| Control (automation id) | Text | Stored in `settings.json` | Default | Validation |
|---|---|---|---|---|
| Label `Theme`; radios `SettingsThemeDark`, `SettingsThemeLight`, `SettingsThemeSystem` in a row, 20 apart | `Dark`, `Light`, `System`; under the row `System follows your operating system's light or dark mode.` | `theme`: `Dark`, `Light`, `System` | Dark (D194) | none |
| Label `Fetching`; check box `AutoFetchCheckBox`, box `AutoFetchMinutesBox` (48 wide, centred), `minutes`, on one line | `Fetch in the background every`; second line `Only the active tab's repository; it never asks for a password.` | `autoFetchMinutes`: the number when checked, 0 when not | checked, 5 | while checked a whole number from 1 to 60, else `AutoFetchError` `Enter a whole number of minutes from 1 to 60.`, once the box has been edited (D114) or at Save; unchecked, the box is disabled and keeps its number |
| Label `Working tree`; check box `KeepIgnoredCheckBox` | `Keep ignored files`; second line `A checkout, merge, rebase, cherry-pick, revert, stash apply or stash pop that would overwrite or delete an ignored file does not start. Off: they run as git runs them, which can overwrite or delete such a file without a word; checking out an older commit of the current branch, Undo and Redo still keep it.` (what it covers is D196's; the exception is 9.1's, D179's option and D169's guards staying whatever the setting) | `keepIgnoredFiles` | checked (D193) | none |
| (same label) check box `StashUntrackedCheckBox`, 10 below | `Include untracked files when stashing`; second line `The Stash changes dialog starts with this; its own check box changes it too.` | `stashIncludeUntracked` | checked | none |
| Label `Opening files`; `RunnableIntro`, `RunnableList` (code font, wrapping, read-only), `RunnablePathExt` (Windows only), `Also ask for` with `RunnableExtraBox` (320 wide), `RunnableError`, `RunnableUnix` | `Open file asks first before it opens a file with one of these extensions:`; the fixed list, space-separated, in its order (12.8); `From PATHEXT: {extensions}` (lower case, only those the list lacks; hidden when none); placeholder `.py .pl`; `On Linux and macOS it also asks before it opens a file marked as a program.` | `extraRunnableExtensions`: lower-cased with their dot, one space apart, duplicates and the fixed list's left out | empty | each entry (split at spaces, commas, semicolons) matches `^\.?[A-Za-z0-9_+~-]{1,16}$`, else `RunnableError` `"{entry}" is not an extension such as .py: use letters and digits.` for the first bad one (once edited or at Save) |

Left out: `FileList`, `DiffMode` and `ShowAllFiles` stay toggles where they are used (D44, D61, D164),
since a second control would change a panel behind the user's back; `VISUALCOMMIT_AUTO_FETCH_MINUTES`
(D85), which both harnesses set to 0 (D112), so a note would be in every picture. When it holds a
whole number, `AppSession.Start` logs once, after loading the settings,
`VISUALCOMMIT_AUTO_FETCH_MINUTES={value} overrides the auto-fetch setting ({minutes} minutes).`
`AppSession.Start` reads the variable through a new optional `Func<string, string?>? readEnvironment =
null` (`Environment.GetEnvironmentVariable` when null) and hands the same function to the scheduler's
`ReadEnvironment` (`AutoFetchScheduler.cs:80`) through `MainWindowViewModel`, so the line and
`Interval` read one source and no test changes the process's environment, which every test of the
process shares (`AutoFetchSchedulerTests.The_test_processes_turn_it_off` asserts its 0 in parallel).

From the file: 0 or less shows the box unchecked at 5; 1 to 60 checked with that number; above 60
checked with that number and **no error until the box is edited**, and Save keeps it. Unchecking and
checking again is no edit of the box.

### 7.4 Git

The layout of D190's settings; their rules are section 8.

1. **The scope switch** `GitScope`: two `VcFlatButton`s, 28 high, the shown one `selected`:
   `GitScopeGlobal` `All repositories (global)`, `GitScopeRepository` `This repository`. `This
   repository` is always shown, so the section keeps one layout on every tab, and enabled only while a
   repository's tab is active (repository open, refs read); otherwise disabled with the tooltip `Open
   a repository's tab to change its own settings` (D90). Enabled, its tooltip is the repository's name
   and folder (`web-app — D:\src\web-app`). The section always opens on `All repositories (global)`,
   also on a repository's tab; `This repository` is one click away (D200: git's own advice sets an
   identity with `--global`, 0.3, and an identity typed into the first repository's scope by mistake
   would fail every other repository). Edits in both scopes are kept while switching and saved together.
2. **The caption** `GitScopeCaption` (secondary, 6 below): global `Saved in your global git
   configuration, which git in a terminal uses too.` (tooltip and help text the file's path, 8.2);
   repository `Saved in this repository's .git/config, which all its worktrees share. These values win
   over those for all repositories.`
3. 14 below: label `Name`, `UserNameBox`, under it `UserNameOrigin` (8.4), `UserNameError`; 8 below:
   label `Email`, `UserEmailBox`, under it `UserEmailOrigin`, `UserEmailError`, `UserEmailWarning`
   (8.6).
4. Repository scope only, 6 below: `IdentityInEffect`, then `IdentityNotices` (zero or more secondary
   lines) (8.6).
5. 18 below: label `Pull`; radios `PullNotSet`, `PullMerge`, `PullMergeCommit`, `PullRebase`,
   `PullRebaseMerges`, `PullFastForwardOnly`, one line each, 8 apart, and `PullOther` with its second
   line for an unusual value only (8.7); under them `PullNotices` (8.4), and in the repository scope
   `PullInEffect` (8.7) and `BranchRebaseNotice` with `BranchRebaseButton` (8.8).
6. 18 below: label `Merge tool`; `MergeToolValue` (the tool's name, `Not set`, or in the repository
   scope `Same as global ({tool})` / `Same as global (not set)`) and `MergeToolChooseButton` `Choose…`
   (7.7); under it `MergeToolNote` `Used by Merge tool… in the conflict resolver, and by git mergetool
   in a terminal.`, `MergeToolStatus` (8.10), `MergeToolNotices` (8.4).

While 8.1's reads run (about 0.1 s, side by side), `GitReading` `Reading git's configuration…` stands
in place of 3 to 6. When one fails, `GitReadError`: `Git's configuration could not be read:` and git's first
`fatal:` or `error:` line (`fatal: bad config line 1 in file C:/Users/me/.gitconfig`), nothing
editable. Without a usable git, `GitUnavailable`: `GitAccess`'s sentence (`Git was not found on this
computer.`, `Git 2.29.0 is too old; VisualCommit needs 2.30.0 or newer.`, `Git is not working on this
computer.`) and ` Git's own settings need it.`

Heights at 1100×700 (estimates): switch and caption about 64, identity about 84, in-effect line 21,
radios 142, pull lines about 36, merge tool about 47, gaps 50: about 424 in the repository scope and
367 in the global one, inside the body's 493. More than three lines of notices scroll, which is
accepted.

### 7.5 Tools

| Control (automation id) | Text | Stored | Default | Validation |
|---|---|---|---|---|
| Label `Editor`; `EditorValue`, `EditorProgram` (secondary `TrimmedTextBlock`, `LeadingCharacterEllipsis`, full path in its tooltip), `EditorChooseButton` `Choose…` | `None`, the editor's name (`Visual Studio Code`, ` (git's editor)` after it when 12.3's mark applies) or `Other`; `EditorProgram` the program's path (hidden for None) | `editor` (12.6) | None | 12.5 for Other |
| `EditorNote` (secondary) | `Open in editor appears in the file menus, beside Open file, once an editor is chosen.` | | | |
| For Other: label `Program`, `EditorProgramBox` (320 wide), `EditorBrowseButton` `Browse…`; label `Arguments`, `EditorArgumentsBox` (320 wide); `EditorArgumentsHint`; `EditorError` | hint `{path} is the file's full path, {line} its line (1 for now). Without {path}, the path comes last.` | `editor.program`, `editor.arguments` | arguments `{path}` | 12.5 |
| `EditorStatus`, while 12.3's search runs | `Looking for editors…` | | | |

`Choose…` opens the Pick dialog `Choose an editor` (7.7): `None` clears the editor, a found editor
sets it, `Other…` shows the Program and Arguments rows with the focus in `EditorProgramBox`.

### 7.6 Keyboard shortcuts

`ShortcutsIntro` `These keys are fixed.` (secondary), then `ShortcutList`, an `ItemsControl` of 6.11's
`AppShortcuts.Groups(KeyTexts.Current.Platform)`: per group a heading `ShortcutGroup` (12.5 SemiBold,
12 above) and its note, when it has one, as a secondary line under its rows; per row a `ShortcutRow`,
at least 22 high (`MinHeight`), a `Grid` with columns `*, 16, Auto`: the action (6.1's name,
one per row, as 6.11 lists them) at the left, wrapping in its column and never trimmed; its
keys in a `TextBlock` with `MaxWidth` 340, `TextWrapping` `Wrap` and `TextAlignment` `Right`, in the
code font and `VcTextSecondaryBrush`, several joined by `, ` (`F5, Ctrl+R`; 6.4). A key text holds no
space, so a line breaks only after a `, `: `Extend the graph's selection`'s six keys (`Shift+Up,
Shift+Down, Shift+PageUp, Shift+PageDown, Shift+Home, Shift+End`, about 504 wide in the code font at
11.5, beside a 168-wide label in a 608 column) take two lines and the row grows; every other row fits
one. `FormatAll` and the row's text stay as 6.4 makes them. Automation name `{label}: {key text}`.
Read-only (D192); scrolls with the section.

### 7.7 The Pick dialog for long lists

Phase 3's Pick dialog (`ViewModels/Dialogs/PickDialogViewModel.cs`, `Views/Dialogs/PickDialogView.axaml`)
gains items with a detail:

- `public sealed record PickOption(string Text, string? Detail = null)` and a second constructor
  `PickDialogViewModel(string title, IReadOnlyList<PickOption> options, string? selected, string
  confirmText)`. The filter matches text or detail, ignoring case; `Selected` and the answer stay the
  text (unique within a list).
- The item becomes a `Grid` with columns `*, Auto`: the text as today (`TextTrimming`, `ToolTip.Tip`,
  centred), then 12 apart the detail in `VcTextSecondaryBrush`, 11.5, cut at its start
  (`TrimmedTextBlock`, `LeadingCharacterEllipsis`), hidden when null. Rows stay 28 high. `PickItem`
  keeps its id and name (the text); its help text is the detail.
- Phase 3's use ("Set upstream for {b}") passes strings, which become options without a detail: its
  pictures and tests stay as they are apart from 7.2's toolbar box (compared by region, 17.7).

Both lists stack over the settings screen (D104), confirm `Choose`:

| Pick dialog | Options, in order (text — detail) |
|---|---|
| `Choose an editor` | `None` — `no Open in editor in the file menus`; each found editor (12.3), `{Name}` — `{program path}` (` (git's editor)` after the name when it matches); `Other…` — `a program of your choice` |
| `Choose a merge tool` | `Not set` — global `Merge tool… stays disabled`, repository `same as global ({tool})` / `same as global (not set)`; each tool git found, `{name}` — 2.55's description without ` (requires a graphical session)`, else `found on this computer`; each user-defined tool, `{name}` — `your own tool (mergetool.{name}.cmd)`; each tool git knows but did not find, `{name}` — `not found on this computer`; the current value when none of these, `{value}` — `git does not know this tool`, or `runs in a terminal; the app cannot start it` for 8.10's terminal tools |

### 7.8 Save and Cancel

Changes apply at Save only (no live preview: the toolbar's Theme switch is one). Cancel and Esc
close without asking and change nothing: nothing typed here is work (phase 4's design 7.2's
questions are for work). Enter saves, also from a text box. Save, in order:

1. **Validate** every section, 12.5's checks that read the disk off the UI thread (a program on a
   share that does not answer, 0.9). On any error: show every error (also of unedited fields), mark
   the sections (`SettingsNavError`), show the first such section, focus its first wrong field, stop.
2. **The app's settings**: one `ISettingsStore.Update` with every changed setting of General and Tools
   but the theme; none changed, no call.
3. **The theme**, when changed: `MainWindowViewModel.UseTheme(theme)` (11.6).
4. **Global git writes**: the changed global keys (8.3) through the app-level writer, one at a time,
   stopping at the first failure.
5. **This repository's writes**: its changed keys as one operation of its queue (8.3). While it waits
   behind another operation of that repository (an auto-fetch running when the dialog opened),
   `SettingsSaving` reads `Waiting for {title} to finish…` and Cancel cancels the token the screen
   passed to `SaveRepositorySettingsAsync` (8.3), which `RunAsync` turns into
   `IOperationQueue.Cancel(GitOperation)` for that operation (`RunOptions.Cancellation`, 4.5): it drops
   the operation while it waits and does nothing once the write runs. The dialog closes; what steps 2
   to 4 saved stays. While a write runs, `SettingsSaving` reads `Saving…` and both buttons are disabled
   (well under a second).
6. **Read back** (8.4): 8.1's outside read and, with a repository, its repository read, 8.6's
   identity read and, on a branch, Pull's reader (8.7, SD29), side by side; each scope's read becomes
   its fields' new baseline.
7. **Tell the tabs** (8.11) when any git key was written, also after a failure part-way.
8. **Close**, answering true, unless a changed key is still decided by another file of its scope (8.4) or a write
   failed: then the dialog stays on the Git section with the notices, nothing pending (Cancel closes
   it).

A failed git write opens the error dialog over the screen (D95, D104): for global writes title
`Saving the settings failed`, summary `Git could not save the settings for all repositories.`, git's
output and the command; the repository's operation has its own texts (8.3). Steps 2 and 3 stay
applied. After a failed global write the repository's writes are not made, the read back runs, the
tabs are told and the screen stays on Git; the read back shows what git holds, so the repository
scope's unsaved edits are gone without a word of their own (WP-SET's build, accepted by its review).

### 7.9 When each change takes effect

| Setting | Read by | Takes effect |
|---|---|---|
| Theme | `IThemeService` through `MainWindowViewModel.UseTheme` (11.6) | at Save; System then follows the system live (11.4) |
| Auto-fetch | `AutoFetchScheduler.Interval` (`ViewModels/Operations/AutoFetchScheduler.cs:92`), read at every 30-second tick | within 30 s, no restart. A shorter interval fetches at the next tick once the time since the last fetch passes it; 0 starts nothing new and lets a running fetch finish; turned on, the first fetch comes one interval after the repository was opened or last fetched (`Tracked.LastFetch`) |
| Keep ignored files | section 9's operations, read inside the operation when it runs (9.1) | the next such operation |
| Include untracked files when stashing | `RepositoryOperations.StashAsync` (`RepositoryOperations.StashTags.cs:53`) | the next Stash changes dialog |
| Extensions Open file asks about | `RepositoryOperations.OpenFileAsync` (12.8) | the next Open file |
| Editor | `RepositoryViewModel.MenuContext` (`RepositoryViewModel.Menus.cs:52`), built when a menu opens | the next file menu |
| Identity | git, at each commit (D68) | the next commit, also in a terminal |
| Pull mode | Pull, which reads git's configuration at each pull (8.9) | the next pull |
| Merge tool | a full-mode `RepositoryViewModel`'s `_mergeTool`, read again after Save (8.11) and after every status read with conflicts | at once in an open resolver; the next menu |

**No `Changed` event on `ISettingsStore`.** No consumer needs one: every reader reads
`ISettingsStore.Current` when it needs it, and the theme, the one setting that moves the screen at
once, goes through `UseTheme` (11.6), called by the toolbar's switch, the palette and Save; the
shell's `Theme`, `IsDarkTheme` and `ThemeSwitchToolTip` follow the theme service's own `Changed`.

### 7.10 The settings themselves

`src/VisualCommit.Core/Settings/AppSettings.cs` gains, each with its doc comment and a default so that
an older file loads (D107):

| Property (JSON) | Type | Default | Section |
|---|---|---|---|
| `Theme` (`theme`) | `AppTheme` with `System` appended | `Dark` | 11.2 |
| `KeepIgnoredFiles` (`keepIgnoredFiles`) | `bool` | `true` | 9.1 |
| `Editor` (`editor`) | `EditorSetting?` | null (none) | 12.6 |
| `ExtraRunnableExtensions` (`extraRunnableExtensions`) | `string`, space-separated | empty | 12.8 |

`AutoFetchMinutes`' comment ("No screen sets it in phase 3") names the screen. The new values are
value-equal (a string, a record), so `JsonSettingsStore.Update`'s `next == Current`
(`Services/JsonSettingsStore.cs:36`) still tells a change from none. An older build skips an unknown
property (`editor`) and sets a file with `"theme": "System"` aside (11.2).

### 7.11 Tests

- `SettingsDialogViewModelTests` (no window): a default `AppSettings`' fields; the auto-fetch box (0
  and −3: unchecked with 5; 90: checked, no error, saved as 90 when untouched; `90` typed, `abc`, `0`,
  `61`: the error; unchecked: saves 0); the extensions box (`.py, pl;.PY` saves `.py .pl`; `.exe`
  dropped; `a b/c` gives the error for `b/c`); Save makes one `Update` with only the changed settings
  and calls `UseTheme` only for a changed theme; Cancel and Esc write nothing; an error marks and
  shows its section; `This repository` disabled with its reason without a repository and on a
  dashboard tab; the Git section opening on `All repositories (global)` on a repository's tab too
  (D200), edits in both scopes kept while switching; Cancel drops a waiting repository write through
  the token passed to `SaveRepositorySettingsAsync` (a fake queue).
- `SettingsDialogViewTests` (headless): the card's bounds at 1000×560, 1100×700 and 1920×1080; no
  control outside the card at 1000×560; the first focus on `SettingsNavGeneral`; Up and Down in the
  list; Enter in a box saves; Esc cancels, the focus back on the toolbar button; the Pick dialog
  stacks over it and gives the focus back; at 1100×700 and 1920×1080, every `ShortcutRow`'s label and
  keys inside the row, apart and untrimmed, and the Extend row's keys exactly `Shift+Up, Shift+Down,
  Shift+PageUp, Shift+PageDown, Shift+Home, Shift+End` (a literal, 6.12) on two lines.
- `PickDialogViewModelTests` (new): options with details; the filter on a detail; phase 3's string
  constructor unchanged.
- `SettingsEntryTests` (new; the button and `OpenSettingsCommand`, 18): `SettingsButton` before
  `SearchButton`, enabled on empty, repository and dashboard tabs (`IsEffectivelyEnabled`, bound
  without a `CommandParameter`), a click opening the dialog on General, tooltip `Open settings
  (Ctrl+,)` (`Cmd+,` with the macOS configuration); `OpenSettingsCommand` passes the active tab's
  repository, none for an empty or a dashboard tab, and opens on the section it is given.
- `JsonSettingsStoreTests` (extended): a file without the new properties loads their defaults;
  `editor` round-trips, known and `custom`; `"theme": "System"` loads and round-trips, the default
  stays Dark and `{ "theme": "Purple" }` is still set aside (11.15).
- `AppSessionTests` (new, the lead's, 18), through `readEnvironment` only: a reader giving `15` logs
  the line with 15; `soon` and null log nothing; the default reader gives the process's `0` and logs
  the 0 line; the scheduler reads the same reader. No test changes the process's environment.

## 8. Git's own settings

D190: the identity, the pull mode and the merge tool are git's configuration, never the app's files:
the global configuration and, in "This repository", the repository's own, which wins as git's rules
say. Pull keeps following git (D78, D84); the merge tool stays `merge.tool` (D149).

### 8.1 Reading: one call

`IGitConfiguration` (Core `src/VisualCommit.Core/Git/GitConfiguration.cs`, built in
`src/VisualCommit.Git/GitConfiguration.cs` over the session's `IGitRunner`; one instance,
`TabServices.GitSettings`, created from `GitAccess`):

```csharp
public interface IGitConfiguration : IDisposable
{
    /// <summary>Every value git reads, with its scope and file: one git config --show-scope --show-origin -z --list in the working tree, or for null the outside read (8.1).</summary>
    Task<GitConfigRead> ReadAsync(string? workingTree, CancellationToken cancellationToken = default);

    /// <summary>git var GIT_AUTHOR_IDENT and GIT_COMMITTER_IDENT in the working tree (8.6).</summary>
    Task<GitIdentityRead> ReadIdentityAsync(string workingTree, CancellationToken cancellationToken = default);

    /// <summary>The file git config --global writes (8.2), worked out by the app, never asked of git.</summary>
    string GlobalFile { get; }

    /// <summary>The XDG file git reads before the global file when GIT_CONFIG_GLOBAL is unset, as a full path, for 8.2's naming; empty without a home folder.</summary>
    string XdgFile { get; }

    /// <summary>A variable of the environment git gets, the session's (8.12) over the process's; null when unset or removed (8.6's notices).</summary>
    string? Variable(string name);

    /// <summary>The global writes, in order, one git call each, one save at a time across the app (8.3).</summary>
    Task WriteGlobalAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellationToken = default);

    /// <summary>git mergetool --tool-help, run once per session in the background; the list kept in the data folder answers at once (8.10).</summary>
    Task<MergeToolHelp> ReadToolHelpAsync(CancellationToken cancellationToken = default);
}

public enum GitConfigScope { System, Global, Local, Worktree, Command }

/// <param name="Origin">The file's full path; null for Command.</param>
/// <param name="Value">Null for a key written without a value (git reads it as true).</param>
public sealed record GitConfigValue(GitConfigScope Scope, string? Origin, string Key, string? Value);

public sealed record GitConfigRead(IReadOnlyList<GitConfigValue> Values, bool InRepository);

/// <param name="Value">Null: the key is unset (--unset-all).</param>
public sealed record GitConfigWrite(string Key, string? Value);
```

The names avoid undo's `ConfigScope` and `ConfigEntry` (`Core/Undo/UndoScope.cs:510`,
`Core/Undo/UndoState.cs:63`). `XdgFile`, `Variable` and `IDisposable` joined the interface after wave 1
(the lead's, before WP-SET; the concrete class already had them): `AppSession.Dispose` disposes the
instance, which stops 8.10's lookup.

**The calls.** `git config --show-scope --show-origin -z --list`, a read (`GIT_OPTIONAL_LOCKS=0`),
in up to two places:

- **The outside read**, always (`ReadAsync(null)`): in the data folder with `GIT_CEILING_DIRECTORIES`
  set to the data folder's parent, so git looks for no repository above it (a home folder kept in git
  would add entries or fail with "dubious ownership"), and any `local` and `worktree` entries are
  dropped all the same. No `includeIf "gitdir:"`, `"gitdir/i:"` or `"onbranch:"` applies there;
  plain includes and the XDG file do.
- **The repository read**, with a repository's tab (`ReadAsync(workingTree)`): in the working tree, so
  `includeIf` applies as for its commits and pulls (0.3). It runs beside the outside read, about 0.1 s
  each (0.3).

The global scope's fields, baseline and read back come from the outside read only, the repository
scope's from the repository read (8.2): read inside a repository, a global value can be an
`includeIf`'s, and saving over it would replace the global file's own value, which the screen never
showed (0.3). Never `--global --get` or `--global --list` (0.3).

**Parsing.** Split at `\0`, three fields per entry: the scope; the origin (`file:` removed; `command
line:` a null origin and `Command`; a relative path resolved against the folder the call ran in); key
and value, split at the field's first `\n` (none: a bare key, value null). Never `RemoveEmptyEntries`
across fields: an empty value is an entry. Keys compare as git compares them: section and variable
ignoring case, the subsection (`branch.<b>`, `mergetool.<t>`) exactly. Every entry is kept (tens to a
few hundred); `GitConfigRead.Of(key)` gives a key's entries in git's order, `Winner(key)` the last,
which git uses for a single-valued key.

Both run when the screen opens (in the background, side by side, beside 8.6's identity read, 8.7's
Pull reader and 8.10's list) and after every Save (8.4). Nothing watches the global file: a terminal's change shows
at the next opening, and Pull reads its own settings at each pull (8.9).

### 8.2 The scopes the screen shows, and how it names files

The global scope shows each setting's last value of global scope in the outside read (8.1), wherever
it lies; the repository scope the last of local scope in the repository read. That value is the
field's baseline for 8.3, so a global save never replaces a value the screen did not show: in a
repository whose `includeIf` sets another `user.email`, the global field shows the global file's own
address, and a notice what git uses there (8.4). When the value lies in a file the app does not write
(an include, the XDG file), a notice names the file (8.4), as it names what else matters (another
scope that wins, a system value, an `includeIf` that applies in this repository). The repository
scope's `Same as global: {value}` placeholders and in-effect lines (8.6, 8.7) come from the repository
read: what this repository really gets. A file is named by what it is, never by its path, so texts
are the same on every machine and run; **the full path is always the tooltip and help text** of the
line naming it:

| File (from the read) | Named on screen |
|---|---|
| The file `--global` writes (`IGitConfiguration.GlobalFile`) | `your global configuration` |
| `$XDG_CONFIG_HOME/git/config`, or `~/.config/git/config` | `git/config under XDG_CONFIG_HOME`, or `.config/git/config` in the home folder |
| Any other file of global scope | `{file name} (included)` |
| A file of system scope | `git's system configuration` |
| The repository's `.git/config` (local scope) | `.git/config` |
| Another file of local scope | `{file name} (included by .git/config)` |
| A file of worktree scope | `config.worktree (this worktree only)` |
| Command scope | `the environment the app was started with` |

`GlobalFile` follows git's rule (0.3) over the environment git gets (the session's, 8.12, over the
process's): `GIT_CONFIG_GLOBAL` when set, also when set but empty, in which case git reads no global
file (`~/.gitconfig` included), so `GlobalFile` names none (empty; null from the pure rule below), a
global write fails with `error: could not write config file : Is a directory` (exit 4, 8.5) and an
`--unset-all` exits 5; otherwise, with `home` = `HOME` when set (on
Windows with `HOME` unset: `HOMEDRIVE`+`HOMEPATH` when both are set and that folder exists, else
`USERPROFILE`, as Git for Windows does, 0.3; elsewhere `HOME` alone) and `xdg` =
`$XDG_CONFIG_HOME/git/config` (or `<home>/.config/git/config`): `<home>/.gitconfig` when it exists,
else `xdg` when it exists, else `<home>/.gitconfig`. A pure `GitConfigLocations.GlobalFile(Func<string,
string?> environment, Func<string, bool> fileExists, Func<string, bool> directoryExists, bool
isWindows)`, tested without git and by a parity test against where git writes (`git var
GIT_CONFIG_GLOBAL` names it only from 2.55 on, 0.3). On a machine with a home share (a domain's `H:`)
`USERPROFILE` alone would name the wrong file, and every real global entry would read as
`.gitconfig (included)`.

### 8.3 Writing

Only changed keys are written, so an untouched value is never rewritten and its file keeps its
layout, comments and includes. A value: `git config --<global|local> --replace-all <key> <value>`
(absent, single and multi-valued keys alike; values that start with `-` kept); an emptied field or a
mode's key to unset: `git config --<global|local> --unset-all <key>`, **exit 5 counting as done** (the
key was absent, 0.3). Order: `user.name`, `user.email`, `pull.rebase`, `pull.ff`, `merge.tool`, then
(repository only) `branch.<b>.rebase`. Never `--worktree`, `--system`, an included file or `--file`:
git chooses, as in a terminal.

- **Global writes** belong to no repository: `IGitConfiguration.WriteGlobalAsync` runs one git call
  per key, off the UI thread (D59), under one app-wide `SemaphoreSlim` (two saves never interleave),
  in the data folder with 8.1's ceiling; the first failure stops the list and throws `GitException`
  with git's output.
- **This repository's writes** are one operation of its queue (D81),
  `RepositoryOperations.SaveRepositorySettingsAsync(IReadOnlyList<GitConfigWrite> writes,
  CancellationToken cancellation)`: `OperationKind.Local`, `RefreshScope.None`, run with `RunOptions {
  Cancellation = cancellation, Undo = UndoScope.None }` (4.5: the screen's Cancel drops it while it
  waits, 7.8 step 5; D168: no undo entry; a later undo whose check covers `branch.<b>` then refuses, as
  for any outside change). Texts `new("Save repository settings", "Saving the repository's
  settings…", "Saving the settings failed", "Git could not save the repository's settings.")`; result
  `Saved the repository's settings`. Inside,
  `IGitRepository.WriteConfigAsync(writes)` (new) runs `git config --local …` per key in the working
  tree, stopping at the first failure. The queue pauses the watcher meanwhile (D52); a write to
  `.git/config` changes nothing the watcher's refresh shows.

A global write's error dialog has no repository: `ErrorDialogViewModel`'s `repositoryName` becomes
nullable and null hides `ErrorRepository` (every caller today passes one).

### 8.4 Read back, and the notices

After every Save the screen reads again (8.1) and compares within the written scope (SD30): for each
changed key, the last entry of the scope written, in that scope's read (local scope in the repository
read, global scope in the outside read: 8.2's value of the scope), against what was written; a key
unset is done when that scope holds no entry for it. So a correct global save made on a repository's
tab whose `includeIf` overrides the key closes the dialog. `GitConfigRead.Winner`, the last entry over
every scope of the read, is not the comparison: with it a repository key that `Same as global` unsets
would stay decided by the global entry beneath it (S19 step 7: the global `pull.ff = only`), which is
what `Same as global` asks for and must not hold the screen open. The two "Still" cases below are
files of the written scope that the app does not write (an include read later, the XDG file): when
one still decides the key, the field's notice says so and the dialog stays open (7.8 step 8); another
scope's entry keeps its informational notice (the table) and holds nothing open. The same comparison
at opening gives the informational notices. Each is one secondary line under its field (`UserNameOrigin`,
`UserEmailOrigin`, `PullNotices`, `MergeToolNotices`), the file named as 8.2 says, the full path its
tooltip; the "Still" notices in `VcWarningBrush`.

| Case | Scope | Text |
|---|---|---|
| After a save, a later file of the same scope (an include after the written line) still decides the key | either | `Still {value} in {file}, which git reads later.` |
| After a save that unset a key, an included or XDG file still sets it | either | `Still set to {value} in {file}: the app cannot change that file.` |
| A file the app does not write decides the scope's value (an include, XDG) | either | `Git uses {value} from {file}.` |
| A repository is open and the repository read's global-scope winner differs from the outside read's (an `includeIf` that applies there) | global | `In {repository}, git uses {value} from {file}.` |
| The worktree scope sets the key | repository | `This worktree has its own value, {value}, in config.worktree; the app does not change it.` |
| The command scope sets the key | either | `{value} comes from the environment the app was started with and wins over every file.` |
| The scope has no value and a lower scope has one | repository | the box's placeholder (8.6) or the radio `Same as global` (8.7) |
| The global scope has no value and the system scope has one | global | placeholder `From git's system configuration: {value}`; for a pull key the line `{key} = {value} also applies, from git's system configuration.` |
| A repository is open and sets its own value | global | `{repository} sets its own: {value}.` |

"Later" is git's reading order: system, global (XDG before `~/.gitconfig`, includes where they
stand), local, worktree, command. A `pull.*` notice names the key: `Still pull.ff = only in
work.gitconfig (included), which git reads later.`

### 8.5 Locks, broken files and exit codes

| Git did | Exit | The app |
|---|---|---|
| `error: could not lock config file <file>: File exists` (a held lock: another program, a terminal, a git killed while writing) | 255 | the error dialog with git's line; the screen stays open. No offer to remove a configuration lock (D126 is for locks the app's own cancelled operations leave) |
| `error: could not lock config file <file>: No such file or directory` / `: Permission denied` | 255 | the same |
| `fatal: bad config line <n> in file <file>` | 128 | reading: `GitReadError` (7.4); writing: the error dialog. Such a file stops every git call, `git --version` included, so the app usually says "Git is not working" first; git's own line there is phase 8's first-run work (16.2) |
| `error: could not write config file : Is a directory` (`GIT_CONFIG_GLOBAL` set but empty, 8.2: no global file) | 4 | the error dialog with git's line |
| `--unset-all` of an absent key (also with `GIT_CONFIG_GLOBAL` set but empty) | 5 | done |
| exit 1 or 2 on a write (an invalid key) | 1, 2 | a bug: the error dialog with git's line, and a log line |

### 8.6 Identity

| Field | Writes | Placeholder | Rules |
|---|---|---|---|
| `UserNameBox` (`Name`) | `user.name` | global `Not set` (or 8.4's system text); repository `Same as global: {value}` or `Not set` | trimmed; `<`, `>`, carriage return or line feed refused with `UserNameError` `A name cannot contain <, > or a line break.` (git drops them silently, 0.3); a name of only `. , : ; " \ '` and spaces refused with `Git refuses a name made only of punctuation.` (2.36 refuses it, 2.55 not); empty unsets; an empty value is never written (git refuses every commit with it) |
| `UserEmailBox` (`Email`) | `user.email` | as Name | trimmed; `<`, `>` and line breaks refused with `UserEmailError` `An email address cannot contain <, > or a line break.`; empty unsets; without `@` the warning `UserEmailWarning` `This does not look like an email address.`, saved anyway (git accepts it) |

**In effect.** Repository scope only: `IdentityInEffect` reads `git var GIT_AUTHOR_IDENT` and `git var
GIT_COMMITTER_IDENT` in the working tree (in parallel, at opening and after a Save), drops the
trailing ` <seconds> <zone>`, and says `Commits here are made as Test Author <author@example.com>.`,
or with a different committer `Commits here are made as {author}, committed as {committer}.` A
refusal is said in the app's words, from git's last `fatal:` line:

| Git's last line | `IdentityInEffect` |
|---|---|
| `unable to auto-detect email address …`, `no email was given and auto-detection is disabled` | `Git cannot make commits here: no email is set.` |
| `no name was given and auto-detection is disabled` | `Git cannot make commits here: no name is set.` |
| `empty ident name (for <…>) not allowed` | `Git cannot make commits here: the name is empty.` |
| `name consists only of disallowed characters: …` | `Git cannot make commits here: it refuses the name.` |
| anything else | `Git cannot make commits here:` and git's line without `fatal: ` |

Repository scope only and in the app's words, because outside a repository git fills a missing name
or email from the machine (the account's name, `<user>@<computer>`) and git's refusals name the
machine's user and computer too (0.3): pictures would differ per machine and CI runner. A check
without any identity seeds `user.useConfigOnly = true` in the session's global file (17.2), so git
refuses with a fixed line.

`IdentityNotices`, repository scope, one line each when it applies: `GIT_AUTHOR_NAME is set in the
environment the app was started with, so git uses it for authors.` (likewise `GIT_AUTHOR_EMAIL`, and
`GIT_COMMITTER_NAME`, `GIT_COMMITTER_EMAIL` with `… for committers.`); `EMAIL is set in the
environment the app was started with, so git uses it when no email is set.`; `author.name = {value}
({file}) is used for authors instead.` (likewise `author.email`, and `committer.name`,
`committer.email` with `… for committers instead.`); `user.useConfigOnly is set, so git does not guess
a missing name or email.` The variables come from the session's environment over the process's
(8.12). Profiles (O16) stay in the backlog; the repository scope is D190's per-repository identity.

### 8.7 Pull modes

Each mode writes both keys **at the chosen scope** (`--global` or `--local`): precedence is per key
across scopes and Git for Windows writes a pull key into the system file, so one key could be
silently cancelled by a lower scope's other key (0.3).

| Radio (id) | Label | `pull.rebase` | `pull.ff` | Diverged branches | Can fast-forward |
|---|---|---|---|---|---|
| `PullNotSet` | global `Not set`; repository `Same as global` | unset | unset | the scope below decides; with nothing anywhere the app merges (D78) and git in a terminal stops | |
| `PullMerge` | `Merge (fast-forward when possible)` | `false` | `true` | merge | fast-forward (also with `merge.ff=false`) |
| `PullMergeCommit` | `Merge, always with a merge commit` | `false` | `false` | merge | merge commit |
| `PullRebase` | `Rebase` | `true` | `true` | rebase | fast-forward |
| `PullRebaseMerges` | `Rebase, keeping merge commits` | `merges` | `true` | rebase with `--rebase-merges` | fast-forward |
| `PullFastForwardOnly` | `Fast-forward only` | unset | `only` | refused: `Not possible to fast-forward, aborting.` | fast-forward |

No second lines (the six radios stay 142 high); `PullNotSet`'s tooltip says `Nothing set: the app
merges, and git in a terminal stops when the branches have diverged` (global) or `Uses the pull mode
for all repositories` (repository).

**Reading a scope's pair.** The scope's last value of each key (8.2), read as git reads it (0.3), by
one Core helper the screen and its tests share, `GitBoolean.TryParse(string? value, bool bare, out
bool result)` (`src/VisualCommit.Core/Git/GitBoolean.cs`):

- a bare key is true and the empty value false;
- `true`, `yes`, `on`, `false`, `no` and `off` compare ignoring case;
- anything else is a boolean when it is an integer as git parses it: an optional sign, then decimal,
  `0x` or `0X` hex or leading-`0` octal, then an optional unit `k`, `m` or `g` in either case, the
  result fitting in a 32-bit int; not zero is true, zero false (`2`, `0x10`, `-1`, `010`, `1K` true;
  `0x0`, `00` false; `99999999999` no boolean);
- a unit letter alone (`k`, `m`, `g` in either case) is no boolean here: 2.36 reads it as false and
  newer gits refuse it (0.3), which rule 3 shows (in `pull.rebase` even before rule 2, as the exception
  below the rules says).

`merges`, `m`, `interactive`, `i` and `only` compare exactly, so `MERGES` and `ONLY` are refused
values. Then, first that holds:

1. a value the app's Pull does not understand (`pull.rebase` other than a boolean, `merges`, `m`,
   `interactive`, `i`; `pull.ff` other than a boolean or `only`; a unit letter alone aside) →
   `PullOther`, refused;
2. `pull.ff` = `only` → Fast-forward only (whatever `pull.rebase` says, but for the exception below);
3. `pull.rebase` = `interactive`, `i` or `m`, or either key a unit letter alone → `PullOther`;
4. `pull.rebase` true → Rebase (`pull.ff=false` is ignored when rebasing);
5. `pull.rebase` = `merges` → Rebase, keeping merge commits;
6. `pull.ff` false → Merge, always with a merge commit;
7. `pull.rebase` false or `pull.ff` true → Merge (fast-forward when possible);
8. both unset → Not set / Same as global.

One exception to the order: a unit letter alone in `pull.rebase` (`k`, `M`, `G` and the rest; `m`
passes, being `merges` or false) reads as `PullOther` with the unit letter's second line
(`PullModes.UnitLetterNote`) before rule 2, because with `pull.ff=only` 2.36 fast-forwards only while
2.55 refuses every pull (`fatal: invalid value for 'pull.rebase'`, exit 128).

Only those values, and those the two gits read differently (`m`, a unit letter alone), are unusual,
so the single keys git's hint and Git for Windows' installer write (`pull.rebase false`, `pull.rebase
true`, `pull.ff only` alone), the commonest settings, show as their modes; a value is never rewritten unless the
user picks a mode. `PullOther`, a seventh radio, is shown checked and only then: `Other: {key} =
{value}` for the unusual key (both when both are: `Other: pull.rebase = preserve, pull.ff =
sometimes`), with a second line:

| Value | Second line |
|---|---|
| `interactive`, `i` | `Git opens an editor for the list; the app rebases without one.` |
| `m` | `Git 2.36 merges with this value and newer gits rebase keeping merge commits; the app does what its git does.` |
| a unit letter alone (`k`, `K`, `M`, `g`, `G`; `m` too for `pull.ff`) | `Git 2.36 reads this value as false and newer gits refuse it; the app does what its git does.` |
| a refused value | `Pulls fail while this value is set.` |

Picking any of the six replaces both keys at the scope. Core's `PullModes.Read(string? rebase, bool
rebaseIsBare, string? fastForward) → PullModeReading` and `PullModes.Writes(PullMode) →
IReadOnlyList<GitConfigWrite>`, `enum PullMode { NotSet, Merge, MergeCommit, Rebase, RebaseMerges,
FastForwardOnly }` (`src/VisualCommit.Core/Git/PullModes.cs`), with `GitBoolean`, hold the pure logic.
Pull keeps its own reader, which asks `git config --type=bool` (8.9); `PullConfigurationTests` checks
that the two agree on every value 8.13 names.

**In effect.** Repository scope: `PullInEffect`, from `IGitRepository.ReadPullConfigurationAsync`
(8.9), Pull's own reader, so screen and Pull never disagree (`m` included), with the deciding keys'
files from 8.1: `Pull into {b}: {mode} ({key} = {value} in {file}).` (`Pull into main: Rebase
(pull.rebase = true in .git/config).`, `Pull into main: Fast-forward only (pull.ff = only in your
global configuration).`); nothing set: `Pull into {b}: Merge (fast-forward when possible): nothing is
set, so the app merges; git in a terminal stops when the branches have diverged.`; a value Pull
refuses: `Pull into {b} fails: {problem}` (8.9's sentence). Detached HEAD or an unborn branch: no
line.

Pull's reader runs with the screen's other reads (SD29): on a repository's tab whose HEAD is on a
branch, `IGitRepository.ReadPullConfigurationAsync(<b>)` starts when the screen opens, beside 8.1's
two reads and 8.6's identity read, whatever section the screen opens on, and again in every read back
(7.8 step 6, beside the reads it names), so that `PullInEffect` always speaks of the reads it sits
beside, also when a Save leaves the screen open (7.8 step 8); a scope switch, a section change or a
radio's click reads nothing. Its calls are Pull's, in its order (8.9): three to five in the working
tree.

### 8.8 `branch.<b>.rebase`

The current branch's `branch.<b>.rebase` (often written by git through `branch.autoSetupRebase`, 0.3)
wins over `pull.rebase`. In the repository scope `BranchRebaseNotice` says `{b} has its own pull
setting, branch.{b}.rebase = {value}, which git uses instead of the mode above.` When the winning
entry is in `.git/config`, `BranchRebaseButton` `Use the repository's setting` marks the key for
removal; the notice then reads `Save removes branch.{b}.rebase, so {b} pulls as set above.` and the
button `Keep {b}'s setting` takes the mark back. Save runs `git config --local --unset-all
branch.<b>.rebase` last (8.3). In another file the notice names it (8.2), without a button. No other
branch key is edited in phase 6. The key is matched by section and variable ignoring case and the
branch name exactly (0.3).

### 8.9 Pull reads its configuration before the fetch (amends D84)

`git pull` refuses an invalid value before it fetches; the app's Pull fails after its fetch (0.3).
The repository ends the same, but a bulk pull (section 4) would spend a fetch per misconfigured
repository. So:

- **The reader.** `GitRepository.ReadPullFastForwardAsync`, `ReadPullRebaseAsync` and
  `ReadPullBoolAsync` (`src/VisualCommit.Git/GitRepository.Remotes.cs:550-625`, moved by WP0 into
  `GitRepository.PullConfig.cs`) become one public `IGitRepository.ReadPullConfigurationAsync(string
  branch, CancellationToken)` returning Core's `PullConfiguration(PullFastForwardSetting FastForward,
  PullRebaseSetting Rebase, string RebaseKey, string? RebaseValue, string? FastForwardValue, string?
  Problem, string? ProblemKey)` (`src/VisualCommit.Core/Git/PullConfiguration.cs`); the private enums
  (`:533`, `:542`) move to Core as `PullFastForwardSetting { Unset, Always, Never, Only }` and
  `PullRebaseSetting { None, Rebase, Merges }`. Calls and order stay (the branch key first, then
  `pull.rebase`; `--type=bool` for an uncommon boolean, so `m` follows each git). Instead of throwing,
  a value it does not understand fills `Problem` with today's sentence (`pull.rebase is set to
  preserve, which this app does not understand.`); `PullConfiguration.NotUnderstood()` builds the
  `GitException("git config --get <key>", 0, <sentence>)` that `RemoteArguments.NotUnderstood`
  (`:888`) builds today, which then calls it. `ReadPullFastForwardAsync` stays, private, beside it: it
  reads `pull.ff` first for `ReadPullConfigurationAsync` (a problem there stops the read, as `git pull`
  stops) and alone for `PullIntegration.Rebase` (D231).
- **`IntegratePullAsync`.** Phase 4's overload (`IGitRepository.cs:556`, `(GitRef branch,
  PullIntegration integration, Func<string, Task>? beforeRebase, NetworkOptions? options = null,
  CancellationToken cancellationToken = default)`) gains a last optional `PullIntegrationOptions?
  pullOptions = null` before the token (named so, since `options` is already the `NetworkOptions`;
  `FakeRepositoryBase`'s override alike): the record
  `PullIntegrationOptions(PullConfiguration? Configuration = null, bool KeepIgnored = false)`, which WP0
  declares (`KeepIgnored` is 9.10's). With a configuration, `AsConfigured` integrates by it (throwing
  its `NotUnderstood()` for a problem); without, it reads it itself, as today. `PullIntegration.Rebase`
  counts only a `pull.ff` problem of a given configuration and, given none, reads `pull.ff` alone,
  throwing for a value git refuses; `pull.ff=only` and `false` give way to the rebase (D231). So
  **`PullParityTests` and `PullRebaseParityTests` compile and stay unchanged** (they pass `beforeRebase:` and
  `cancellationToken:` by name, no options, after their own fetch) and keep
  comparing the app's integration with `git pull` on every git CI runs.
- **`PullStepsAsync`** (today `ViewModels/Operations/RepositoryOperations.Remotes.cs:159`, moved by WP0
  into `RepositoryOperations.FetchPull.cs`): after `RefuseIfPullTargetMovedAsync` and **before the
  fetch**, for `PullIntegration.AsConfigured`, `var configuration = await
  Git.ReadPullConfigurationAsync(branch.Name)`; with a problem it returns `Failed(text,
  FailureOf(configuration.NotUnderstood(), configuration.Problem))`, today's dialog ("Pull failed", the
  sentence, `git config --get pull.rebase`), and nothing is fetched. For "Pull from {up} (rebase)", the
  rebase chosen for one pull (`PullIntegration.Rebase`), it reads `pull.ff` alone and refuses the same
  way a value `git pull --rebase` refuses (`foo`, `ONLY`; `k` on 2.55; the command `git config --get
  pull.ff`), as git does (checked on 2.36 and 2.55); `pull.ff=only` and `false` still give way to the
  rebase, and no rebase setting is read (D231, amending D147's "does not read git's pull settings").
  Otherwise the fetch, the cancel window's end, D124's second HEAD check, then `IntegratePullAsync(branch, integration, oldTip =>
  SaveRebaseBackupAsync(branch.Name, oldTip), options, new PullIntegrationOptions(configuration,
  KeepIgnored), CancellationToken.None)`, `options` staying the `NetworkOptions` it passes today
  (`RepositoryOperations.Remotes.cs:186-191`) and the new argument going before the token. The `catch`
  at `:193` goes (it can no longer fire). A local upstream: read, then integrate.
- The choice of fast-forward, rebase or merge is still made after the fetch, inside
  `IntegratePullAsync`, from the configuration read before it: a terminal's change during the fetch is
  not seen, as with `git pull`, which reads its settings at its start. 9.10's ignored-files check also
  stays after the fetch, inside `IntegratePullAsync`.

D84 then reads: Pull reads git's pull settings and refuses one it does not understand before anything
else; then `git fetch <remote>`, which can be cancelled; then, without cancel, the integration git pull
would choose from the settings read first.

### 8.10 The merge tool

**The value** is `merge.tool` at the scope (`--replace-all`, or `--unset-all` for Not set). Neither
`mergetool.<tool>.path`, `.cmd` nor `.trustExitCode` is edited; `merge.guitool` is neither read nor
written (the app passes `--tool=<merge.tool>`, D149, 0.3). A tool of one's own is set with
`mergetool.<name>.cmd` in git's configuration and listed once it is there.

**The list** comes from `git mergetool --tool-help`, about 20 to 60 s on this laptop (0.3), so it is
looked up ahead and kept (D202):

- `IGitConfiguration.ReadToolHelpAsync` runs it once per app session (as `ToolCatalogues.MergeTools`,
  12.9), never on the UI thread, in the data folder with 8.1's ceiling, cancelled when the app closes;
  a later call gets the kept list while the run goes on (below), then the run's task. No time limit
  (no network). A failure (exit not 0, or git did not start) is logged and gives an empty list with
  `Failed` set.
- **When it starts**: once start-up has settled. `MainWindowViewModel` calls `Tools.MergeTools` and
  drops the task when the first tab's load ends (the first graph shown, or the first repository's
  load failed), or at once when the session restores no repository's tab; the settings screen's
  first opening starts it if nothing has yet. On Windows the git process runs at below-normal priority
  (`GitCommand.BelowNormalPriority`, new, which `GitRunner` applies right after the start to git and to
  what git has started by then, found by Toolhelp snapshots, at most 5; the shells git starts later
  inherit it), so it does not slow the first repository's reads (Q1, risk 12). `GitConfiguration`
  sets the flag from `ToolHelpBelowNormal` (internal, true for the app); the live test of `git
  mergetool --tool-help` sets it false and runs git at normal priority, because below normal, on a
  machine whose other test projects keep every processor busy, the lookup was starved past the hang
  watchdog's limit (three runs). The priority itself is proven by `GitRunnerPriorityTests` and the
  command's flag by the held runner's test (8.13, `2252a01`).
- **The kept list**: each run that ends without failing writes `merge-tools.json` in the data folder
  (`AppPaths.MergeToolsFile`, given to `GitConfiguration` by `AppSession.Start`), `{ "git": <git's
  path>, "version": <git --version's text>, "found": [...], "notFound": [...] }` with each tool's name
  and description, written whole and replaced as `settings.json` is (D46). While the run goes on, `ReadToolHelpAsync` answers at once with the kept
  list when its path and version are this session's git's; the run's result replaces it for later
  openings and sessions. A missing, unreadable or other git's file counts as none (logged once). Only
  the very first lookup on a machine, or after git changed, waits. A run that fails leaves the kept
  file as it is. `ToolCatalogues.Fixed` never reads or writes it (12.9).
- `GitMergeTools.ParseToolHelp` (pure, Git project): lines starting with two tabs after the header
  `may be set to one of the following:` are tools found, those after `The following tools are valid,
  but not currently available:` tools not found; the first whitespace-separated token is the name, the
  rest 2.55's description; the `user-defined:` block (one tab) and its lines are skipped (those tools
  come from the read); the closing note is ignored. `MergeToolHelp(IReadOnlyList<MergeTool> Found,
  IReadOnlyList<MergeTool> NotFound, bool Failed)`, `MergeTool(string Name, string? Description)`
  (Core).
- Left out: the tools that need a terminal, which the app cannot run (D149 starts git without one):
  `vimdiff`, `vimdiff1` to `vimdiff3`, `nvimdiff`, `nvimdiff1` to `nvimdiff3`, `emerge` (`gvimdiff` and
  its numbered forms are windowed and stay); 2.36 gives no descriptions, so the app keeps this list.
- Added: every `mergetool.<name>.cmd` of the read, as `your own tool`; a tool whose
  `mergetool.<tool>.path` names an existing file counts as found (`--tool-help` ignores the path,
  0.3); the current value when none of these (7.7).

Until a list is there (the kept one, or the run's when none is kept), `MergeToolChooseButton` is
disabled with the tooltip `Looking for the merge tools git knows…` and `MergeToolStatus` says the
same; the current value shows at once from the read. A Pick dialog opened from the kept list keeps
the options it opened with; the next opening shows the run's. After a failure with nothing kept
`MergeToolStatus` reads `Git could not list its merge tools.` and Choose… offers Not set, the
user-defined tools and the current value. A chosen tool git did not find: `git did not find {tool} on
this computer: Merge tool… fails until it is installed.`

The disabled text of the resolver's "Merge tool…" and the file menu's "Open in merge tool", `Set
merge.tool in git's configuration to use a merge tool` (`ConflictResolverTexts.cs:27`,
`OperationTexts.Conflicts.cs:63`), stays: it is still true.

### 8.11 Telling the open tabs

`TabServices` gains `event EventHandler? GitSettingsChanged` and `RaiseGitSettingsChanged()`, raised on
the UI thread after a Save that wrote any git key (7.8 step 7; the settings screen raises it, WP-SET).
Every `RepositoryViewModel` subscribes while it lives, in WP-D's files: its constructor keeps
`services` (today it only passes it on, `RepositoryViewModel.cs:104-115`) and adds
`OnGitSettingsChanged`; `Dispose` removes it before `_lifetime.Cancel()` and `_lifetime.Dispose()`
(`:570-590`), so a closed tab's or a released summary's view model is not kept alive by the
session-long `TabServices`, and no handler reads the token of a disposed `_lifetime`. The handler
(`RepositoryViewModel.GraphArea.cs`, beside `ReadMergeToolAsync`) returns at once when `_disposed`,
while `IsSummary` (3.1: a summary has no resolver or file menus, and promotion's full `StartAsync`
reads `merge.tool` through `FollowStatusInGraphArea`), or when `Status` shows no conflicts; otherwise
it calls `ReadMergeToolAsync` (`:256`), so an open resolver enables "Merge tool…" without being
reopened (today `merge.tool` is read again only after a status read with conflicts, `:247-254`, and a
global write fires no watcher). Identity and pull settings need no telling: git and Pull read them
when used.

### 8.12 The harnesses' own git configuration (amends D112)

The first scripted check that saves an identity or a pull mode would otherwise write the one global
file every test process and every later run shares (0.3). 17.2 builds the remedy and names its
parts: one git environment per app session (`AppSession.Start`'s `gitEnvironment`, which the session's
one `GitRunner` applies in `BuildStartInfo`, `GitRunner.cs:184-220`, after `DefaultEnvironment` and
before the command's own), with `GIT_CONFIG_GLOBAL` and `XDG_CONFIG_HOME` in the session's data folder;
the process's own `GIT_CONFIG_GLOBAL` in a folder never created, so a stray write fails with exit 255
(a read-only file would not do: git writes it on Windows, 0.3), with a tripwire; the identity variables
and `MSYSTEM` removed. `GIT_CONFIG_GLOBAL` needs Git 2.32, as the tests already require. This section
adds:

- `GitRunner.SessionEnvironment` exposes the session's variables, which 8.2's `GlobalFile` and 8.6's
  notices read over the process's.
- The process-wide `XDG_CONFIG_HOME` (`<ceiling>/isolation/xdg`, created empty) also covers the tests
  that build a `GitRunner` of their own (App.Tests' `RepositoryViewModelTests`,
  `CommitGraphControlTests`, …), which would otherwise read the owner's `~/.config/git/ignore`. On
  Linux and macOS .NET's `SpecialFolder.ApplicationData` follows it, so `AppPaths.DefaultDataDirectory()`
  lies in the isolation folder in test processes, which is only safer (`AppPathsTests` compare it with
  the same call); fontconfig finds no user configuration there, as on CI.
- The tripwire also fails when `<ceiling>/isolation/xdg` holds a `git` folder (git's `config`,
  `ignore` and `attributes` would lie there).
- `HOME` stays: git uses it for `~/.git-credentials` and ssh, and changing it could disturb the ssh
  tests (`FakeSsh`); with both variables set git reads no configuration from it.
- Phase 4's C16, whose fake merge tool runs through `sh`, proves `git mergetool` runs without
  `MSYSTEM`. Git-layer unit and integration tests pass a runner with a session environment of their
  own (a temporary global file), or `TempRepo.Environment`.

### 8.13 Tests

- **Git layer.** `GitConfigReadTests`: the `-z` parse of both gits' recorded outputs (a bare key, an
  empty value, a value with a line break, an included file's origin, `command line:`, a relative
  `.git/config`, a linked worktree's absolute path); live reads (global and local, an include, an
  `includeIf "gitdir:"` applying only inside its repository, the outside read with `local` entries
  dropped and the ceiling holding; 0.3's `includeIf` shape: the outside read leaves out the
  `includeIf`'s `user.email`, the repository read ends with it). `GitConfigWriteTests`:
  `--replace-all` on absent, single, multi-valued and bare keys; `--unset-all` of an absent key done; a
  held lock (255, git's line in the exception) for global and local; a global file in a missing folder
  (255, folder not created); a value starting with `-`; non-ASCII and quoted values round-tripping
  through `ArgumentList` (not checked yet, 0.3); a write that an include still beats; under 0.3's
  `includeIf` shape a global write of `user.email` read back in the outside read as written, while the
  work repository's winner stays the `includeIf`'s. `GitConfigLocationsTests`: the pure rule for each
  of 0.3's cases, Windows' home among them (`HOMEDRIVE`+`HOMEPATH` beating `USERPROFILE`, a missing
  `HOMEPATH` folder and an unset `HOMEPATH` falling back to `USERPROFILE`, `HOME` beating both; an empty
  `GIT_CONFIG_GLOBAL` naming no file, where git's write exits 4, 8.2), and a
  parity test that git writes where it predicts (each case with its own `HOME`, `XDG_CONFIG_HOME`,
  `GIT_CONFIG_GLOBAL`; on Windows two more cases that remove `HOME`, `GIT_CONFIG_GLOBAL` and
  `XDG_CONFIG_HOME` from the git call's environment and set `HOMEDRIVE`+`HOMEPATH` and `USERPROFILE` to
  two temporary folders, once with the first folder present and once missing). `GitIdentityTests`: `git var` parsed with and
  without a committer of its own; each refusal of 8.6 that every git gives produced live with
  `user.useConfigOnly` (no email, no name, an empty name) and mapped; `name consists only of disallowed
  characters`, which 2.55 no longer gives (0.3), mapped from its recorded 2.36 line.
  `PullConfigurationTests`: `ReadPullConfigurationAsync` for every row of 0.3's pull facts, `Problem`
  for refused values, `m` as each git reads it; Pull's reader and `GitBoolean.TryParse` agreeing on
  each value `PullModesTests` names (for a unit letter alone: Pull following its git, the screen
  showing `PullOther`); `IntegratePullAsync` with a configuration as without, and the chosen rebase
  refusing an invalid `pull.ff` as `git pull --rebase` does while counting only a `pull.ff` problem of
  a given configuration (`PullConfigurationIntegrationTests`, D231).
  `MergeToolHelpTests`: the parse of recorded 2.36 and 2.55 outputs (with a `user-defined:` block);
  the live run's exit code, at normal priority (`ToolHelpBelowNormal` false, 8.10); the command's
  `BelowNormalPriority` flag through the held runner; `GitRunnerPriorityTests`: the priority applied
  to git on Windows (`2252a01`); the kept list (a matching `merge-tools.json` answered at once while a held
  run goes on, then replaced by its result; another git's path or version, and an unreadable file,
  counted as none; a failed run leaving the file as it was). **`PullParityTests`, `PullRebaseParityTests` and `PullFailureParityTests`
  unchanged.**
- **Core.** `PullModesTests`: `Writes` per mode; `Read` for every rule of 8.7 (the hint forms,
  `pull.rebase=true pull.ff=only`, `true false`, a bare key, `1`, `10k`, `interactive`, `m`,
  `preserve`, `pull.ff=foo`); `GitBoolean.TryParse` and `Read` on `TRUE`, `Yes` and `ON` (true), `0x10`,
  `-1`, `010` and `1K` (true), `0x0` and `00` (false), `99999999999` (refused), `ONLY` and `MERGES`
  (refused), `M` and `k` (`PullOther` with the line that each git reads it differently), also beside
  `pull.ff=only` (8.7's exception to the order), while `m` beside it is Fast-forward only.
- **App.** `GitSettingsViewModelTests` (a fake `IGitConfiguration`): each scope's fields from its read
  (the global scope's from the outside read, the repository's from the repository read); a pair of
  reads with an `includeIf` value in the repository read only: the global field showing the global
  file's value with `In {repository}, git uses {value} from {file}.`, an edit of the repository scope
  alone writing no `--global`, and a global save judged by the outside read (the dialog closing);
  placeholders; dirty tracking (an untouched field writes nothing); each notice of 8.4, the "Still"
  notice after a save that an included file still beats among them (`Still pull.ff = only in
  work.gitconfig (included), which git reads later.`, the dialog staying open, 7.8 step 8); the identity
  rules and texts; `PullOther`; the branch notice and its mark; the merge tool list with terminal
  tools left out, user-defined tools, a path that counts as found, loading (Choose… enabled at once
  from a kept list) and failed.
  `RepositoryOperationsSettingsTests`: `SaveRepositorySettingsAsync` runs in the queue with
  `UndoScope.None`, is dropped while it waits when its token is cancelled (none of its writes run),
  stops at the first failure, and its failure, a held `.git/config.lock` among them
  (git's `could not lock config file`, 255, 8.5), opens the error dialog over the screen, which stays
  open. These two effects have no picture (17.5).
  `RepositoryOperationsPullTests.Pull_with_a_setting_the_app_does_not_understand_says_which_and_changes_nothing`
  (`tests/VisualCommit.App.Tests/RepositoryOperationsPullTests.cs:115`) gains: `origin/main` not
  fetched and no `fetch` among the operation's git calls; WP-B's case beside it: "Pull from {up}
  (rebase)" with `pull.ff = foo` fails the same way before its fetch (`git config --get pull.ff`),
  while with `pull.ff = only` it rebases (D231). A resolver open on a conflict enables "Merge
  tool…" after a Save that set `merge.tool` (`GitSettingsChanged`). WP-D's (8.11, 3.9):
  `GitSettingsChangedTests` (new; a counting `FakeRepositoryBase`): a full view model with conflicts
  reads `merge.tool` once per event, none without conflicts, and after `Dispose` reads nothing and
  logs no warning; `SummaryModeTests` gains: the event on a summary view model whose status has
  conflicts makes no `ReadMergeToolAsync` call; `PromotionTests` gains: after `Promote`, the same event
  reads `merge.tool` once. The lead's `AppSessionTests`: the merge-tool lookup started once, when the
  first tab's load ends, or at once with no repository's tab restored (a counting `ToolCatalogues`).
- **Harness**: 17.2's isolation tests (`ShellTests`, `GitIsolationTests`, the tripwire) with 8.12's
  XDG check, and a check that a real-window launch's global file is its data folder's.

## 9. Keeping ignored files (D193, D196)

D193 makes every checkout keep ignored files by default; D196, the owner's answer after the reviews,
extends it to every operation that can lose one but the hard reset (9.2, 9.13). Git's
`--no-overwrite-ignore` is exact for a checkout, works for a merge only when it fast-forwards, and does
not exist for rebase, cherry-pick, revert or stash, each of which overwrites an ignored file it writes;
a rebase can delete it (0.4). So checkouts and merges pass git's option, and
every other covered operation runs the app's own check before git.

### 9.1 The setting

`AppSettings.KeepIgnoredFiles` (7.10: `bool`, `"keepIgnoredFiles"`, default `true`, also for an older
file). The settings screen's General section (7.3) shows the check box `Keep ignored files`
(`KeepIgnoredCheckBox`) with the line "A checkout, merge, rebase, cherry-pick, revert, stash apply or
stash pop that would overwrite or delete an ignored file does not start. Off: they run as git runs
them, which can overwrite or delete such a file without a word; checking out an older commit of the
current branch, Undo and Redo still keep it." (7.3's text). Each operation reads it when it runs, through a private
`RepositoryOperations.KeepIgnored` (`Services?.Settings.Current.KeepIgnoredFiles ?? true`, as
`StashIncludeUntracked` is read), so a queued operation takes the value at its start (D124); Pull all
(section 4) runs the same pull. WP0 declares `KeepIgnored` in `.Protect.cs` as `false` and passes
`keepIgnored: KeepIgnored` at every app call site of the checkouts, `CreateBranchAsync` and `MergeAsync`,
both git calls of the moved `CheckoutRemoteBranchAsync` included (the automatic stash passes `true`);
WP-IA gives it the body above, which turns every checkout and merge on at once (section 18).

**Off turns off only D193's extension** (no option, no app check, no `check-ignore` after a refused
checkout): phase 5 exactly. Whatever the setting, the automatic stash's checkout (every checkout of an
older commit of the current branch, D179) keeps D179's `--no-overwrite-ignore` and its `check-ignore`
after a refusal, and undo and redo keep D169's guards (an undo must be exact): Off turns off D193's
extension of D179 and restores phase 5, and turning D179 off too would reopen a recorded decision.
The owner confirmed this reading of D193's "checkouts then follow git's own default" on
2026-10-18 (D221).

### 9.2 What it covers (D196)

| Operation | Git's option | The app's check |
|---|---|---|
| Every checkout: a local branch (double-click, menus, the drop's "Check out"); a remote branch (D93, and D201's creation without an upstream); a commit or tag that detaches (HEAD's own commit too, phase 5's PD7); "Create branch" with its box ticked; "Stash changes and check out" (D91); the checkouts of "Check out and merge", "Check out and rebase" and the interactive list; Abort's return after "Check out and rebase"; the backups list's and the reflog page's "Check out this commit" | `--no-overwrite-ignore` (exact: git refuses, nothing changes) | none; one `check-ignore` after a refusal for untracked files (9.11) |
| Merge (both kinds), "Fast-forward {cur} to {b}", the drop's merge, "Check out and merge"'s merge; the pull's fast-forward and merge (Pull, "Pull from {up}", "Pull from {up} (rebase)" when it fast-forwards, Pull all) | `--no-overwrite-ignore` (exact for a fast-forward only) | 9.4; the pull's inside `IntegratePullAsync` (9.10) |
| Rebase onto, "Check out and rebase", the pull's rebase (plain and `--rebase-merges`) | none exists | 9.6 |
| Interactive rebase (the list's Start, with or without its checkout), "Squash {N} commits" | none | 9.7 |
| Cherry-pick and revert, one or a list (graph menus, the reflog page's "Cherry-pick onto {cur}") | none | 9.5 |
| Stash apply and pop (menus, the toolbar's Pop, a double-click) | none | 9.8, after D179's count |

What is not covered: 9.13.

### 9.3 The shared helper and the five reads

Undo's guard (`UndoStepRun.IgnoredWhereAddedAsync`) moves into `GitRepository` as
`FindIgnoredAtAsync(IReadOnlyCollection<string> written, CancellationToken)`, in a new partial file
`GitRepository.KeepIgnored.cs`. For each path an operation may write (repository-relative, `/`, raw)
the candidates are every parent that is a file or symbolic link on disk (`ParentFolders`,
`IsFileOrLink`; S3), the path itself when a file or link, and every file under it when a folder
(`FilesUnder`, links not followed; S2, S4). Sorted by bytes they go to one `git check-ignore --stdin
-z` (`IgnoredAmongAsync`, without `GIT_LITERAL_PATHSPECS`, phase 5 design 15.8): exit 1 means none, no
candidate on disk means no process, a tracked file never comes back (0.4). It returns `(Written,
Ignored)` pairs, so 9.4 can keep or drop each by its written path; undo's Keep move and pop take the
distinct ignored files (`UndoStepRefusalTests` unchanged).

Five new reads on `IGitRepository` return the ignored files in the way, distinct, in byte order, with
`GIT_OPTIONAL_LOCKS=0` (D47), no pathspec, and every tree argument list ended by `--` (a file named
`HEAD` breaks `diff-tree HEAD …`, 0.4): `FindIgnoredInTheWayOfMergeAsync(string into, string source)`
(9.4), `FindIgnoredInTheWayOfCommitsAsync(IReadOnlyList<string> commits, bool revert, string?
mergeParent = null)` (9.5), `FindIgnoredInTheWayOfRebaseAsync(string tip, string onto)` (9.6) and
`(RebasePlan plan)` (9.7), `FindIgnoredInTheWayOfStashAsync(string stashCommit)` (9.8). A read that
fails (`GitException`: a bad revision, on which git fails too, or git not starting) is logged as a
warning and the operation goes on to git, as D179's `CountStashConflictsAsync` does. No read puts a
list of unbounded length on its command line: commits and paths go on standard input
(`GitCommand.StandardInput`: `check-ignore --stdin`, 9.5's `diff-tree --stdin`, 9.7's `rev-list
--stdin`), since Windows starts no process past 32,767 characters (0.4). `FakeRepositoryBase` returns
empty lists.

### 9.4 Merges and the fast-forward

`FindIgnoredInTheWayOfMergeAsync(into, source)`, `into` being `HEAD`, or for "Check out and merge"
`refs/heads/<A>`, checked before that checkout:

1. `git diff-tree -r -z --no-renames --name-only --diff-filter=A <into> <source> --`: what the source
   has and `into` lacks, which holds every path a merge can write over an untracked file (m16's too:
   `into` deleted it).
2. `FindIgnoredAtAsync` of those; nothing in the way ends it (one read, two with a candidate on disk).
3. Only when something is in the way: `git merge-base --all <into> <source>` and per base `git diff-tree
   -r -z --no-renames --name-only --diff-filter=AMT <base> <source> --`. A pair is kept when some base's
   list has its path (the merge writes it: an addition, or m16's conflict); otherwise `into` deleted it
   and the merge keeps the deletion (m15): dropped. A merge of an ancestor drops every pair (U4); no
   base (exit 1, unrelated histories, which git refuses anyway) and a fast-forward (`into` is the base)
   keep every pair; a criss-cross keeps a pair any base lists.

Not `merge-tree --write-tree`: exact, but it needs Git 2.38 (0.4) and the app supports 2.30.

`GitRepository.MergeAsync` gets a required `bool keepIgnored`, the option just before the positional
argument: `git merge --ff-only --no-overwrite-ignore <ref>`, `git merge --no-edit --no-log -m <msg>
--no-overwrite-ignore <ref>`, `git merge --no-edit --no-log -m <msg> --no-ff --no-overwrite-ignore
<ref>` (`Positional`'s `--end-of-options` follows). Git's refusal is exact for a fast-forward, also in
9.12's race, at no cost; the check runs for every merge because the app cannot know beforehand whether
a `MergeMode.Default` merge fast-forwards.

### 9.5 Cherry-pick and revert, one or a list

`FindIgnoredInTheWayOfCommitsAsync`: one `git diff-tree --stdin --no-commit-id --root -r -z
--no-renames --name-only --diff-filter=AMT` (`-R` for a revert) fed each commit's full SHA (`--stdin`
resolves no name, 0.4): each commit's change against its first parent, which a pick applies and a
revert applies reversed. A merge reverted with `-m 1` is fed `<sha> <first parent>`, `mergeParent`
coming from the graph row or `ReadCommitDetailsAsync` as `IsMergeAsync` has it. Then
`FindIgnoredAtAsync` of every path; no second stage. It is enough: an added (A) or modified (M, T) path
is written where HEAD lacks it (an addition, or the side git leaves of a modify/delete conflict, T9,
T4), a tracked path is never ignored, and in a list the union covers a path pick 1 adds and pick 2
deletes. The list is the one git gets (`OrderByRow`: picks oldest first, reverts newest first). Usual
cost: two reads.

### 9.6 Rebase onto, "Check out and rebase" and the pull's rebase

`FindIgnoredInTheWayOfRebaseAsync(tip, onto)`: `tip` is `HEAD`, or for "Check out and rebase" the
branch's full ref; `onto` is `RebaseTarget.Sha`, or the pull's `GitRef.UpstreamRef`. Four reads side by
side: (1) `git merge-base --is-ancestor <onto> <tip>`; (2) `git diff-tree -r -z --no-renames
--name-only --diff-filter=A <tip> <onto> --` (the first checkout of `<onto>`); (3) `git log
--no-renames --name-only -z --root --no-merges --no-show-signature --format=%x1e%H --diff-filter=AMT
<onto>..<tip> --` (the commits git may replay, 0.4; it can list no merge, which has no path without
`-m` and which `--diff-filter` leaves out anyway, U5); (4) `git rev-list --min-parents=2 --max-count=1
<onto>..<tip> --` (a merge in the range, which (3) cannot show). Whenever (4) finds a merge, whatever the
rebase's options, two more reads follow: (5) `git rev-list --topo-order --boundary <onto>..<tip> --`,
whose `-<sha>` lines are every parent outside the range, each a commit git may reset to; and (6) one `git
diff-tree --stdin --no-commit-id -r -z --no-renames --name-only --diff-filter=A` fed `<boundary> <tip>`
per boundary (what each boundary has and the tip lacks, as (2) for `<onto>`). `--rebase-merges` starts
each side branch with `reset` to its original branch point, which (1) to (4) do not see (0.4's side
branch: `reset <U0>` writes the ignored file and the next `reset onto` deletes it); and
`rebase.rebaseMerges` (Git 2.42 and later; 2.36 ignores the key) makes a plain `git rebase` keep merges,
and reset so, too. An ancestor `<onto>` and no merge in `<onto>..<tip>` mean git writes nothing
("Current branch {b} is up to date.", U2): an empty result, as for the pull's rebase of a linear branch
only ahead of its upstream (the menus never offer a rebase onto a commit the branch contains:
`AlreadyOn`, `UpToDateWith`). Otherwise `FindIgnoredAtAsync` of (2), (3) and, with a merge, (6): with a
merge in the range git checks out `<onto>` and replays even a branch only ahead of it, plain or with
`--rebase-merges`, and an ignored file a commit of the range stops tracking is deleted, also when
`--rebase-merges` gives back the same commit (U5). Cost: four reads side by side, then `check-ignore`,
5 calls; with a merge in the range (5) and (6) between them, 7. As (5) and (6) run whatever the
options, a plain rebase whose range holds a merge is refused where git without that setting would keep
the file, only when a boundary commit tracks a path that is an ignored file on disk.

The set is a superset of what git writes (git leaves out commits whose patch is in `<onto>`, the pull's
fork point maybe more, `CountLeftOutByForkPointAsync`): a refusal for a path only such a commit adds is
rare and the safe side. `--rebase-merges` remakes merges with `merge -C`, whose paths are in the set.
With a merge in the range git may check out a commit it skips rather than `<onto>`; (2), the first
checkout of `<onto>`, keeps the set a superset, and (6) covers the branch points git resets to. Known
gaps, recorded in the handoff: in fork-point mode git also rewrites a branch that contains its upstream
when the upstream was moved back since (its reflog names a later commit the branch has); (1) and (4)
then say up to date for a linear branch, and an ignored file the old upstream tracks can be
overwritten, as git would. And in fork-point mode git uses its own range, so the commits it resets to
can differ from (5)'s boundaries.

### 9.7 Interactive rebase and squash

`FindIgnoredInTheWayOfRebaseAsync(plan)`, tip `plan.ExpectedTip`, base `plan.Onto`. Git skips the
todo's leading picks whose only parent is the start and checks out the last of them instead of `<onto>`
(T5); the `label` line `RebaseTodo.Build` writes before every group that changes a message stops the
skipping (T6). The check starts where git does, or it would refuse a reword of the newest commits after
a commit that stopped tracking a now-ignored file:

1. Two pure Core functions: `RebaseTodo.LeadingPicks(RebasePlan)`, the targets of the first groups
   `Build` writes as a plain `pick` line, up to the first that is not (a `label`, or a group of drops
   only), a pick followed by drops included (the `drop` line ends the run); and
   `RebaseTodo.AppliedCommits(RebasePlan)`, every `pick` or `fixup` line's commit in todo order.
2. With leading picks, `git rev-list --stdin --no-walk=unsorted --parents` fed their SHAs on standard
   input, one per line (`GitCommand.StandardInput`, as 9.5's `diff-tree --stdin`): a list of 1,000
   commits whose newest is reworded has 999 leading picks, some 41,000 characters, which as arguments
   would not start on Windows (0.4). The start moves from `plan.Onto` to each leading pick in turn while
   its only parent is the start (git's `skip_unnecessary_picks` as observed in T5; its source was not
   read).
3. Side by side: `git diff-tree -r -z --no-renames --name-only --diff-filter=A <tip> <start> --`, and
   9.5's `diff-tree --stdin` fed every applied commit after the skipped ones (one after a `label` that
   git fast-forwards still writes its change, T6).
4. `FindIgnoredAtAsync` of the union.

A squash's first group changes the message, so nothing is skipped and the start is the oldest squashed
commit's parent (`RebaseTodo.SquashPlan`). Cost: two or three reads, then `check-ignore`.

### 9.8 Stash apply and pop

`FindIgnoredInTheWayOfStashAsync` uses `ReadStashPathsAsync` (cached per SHA, already read by D179's
count), so no process runs before `check-ignore`. Written: the changed paths against the base less the
deletions (A, M, T: ST1's staged new file; ST3's modify/delete, which 2.55 overwrites and 2.36 does
not) and the untracked part's paths (for a parent that is an ignored file). `StashPaths` gains
`Deleted`, an init-only list of the changed paths with status `D`, filled by
`ReadStashPathsUncachedAsync` from its `--name-status`; the index commit adds nothing.

In `ApplyOrPopAsync`: D179's count first, unchanged (it counts an untracked-part path that exists,
ignored or not: "1 conflict prevents applying the stash. …"); then, when it found nothing, this check;
then the pop's backup and git. So the untracked part keeps D179's sentence (git refuses to write it over
a file, ST2), and the check covers the tracked part, which git overwrites without a word; P27's stash
step keeps its call log. Not checked: D91's pop and the automatic stash's pop back (phase 5 design
9.2): right after the push and the checkout no ignored file can be at the stash's paths.

### 9.9 Where each check runs

Inside the operation's `Run` delegate, while `KeepIgnored` is on, after the operation's own refusals
before git and before its first write (backups, the undo capture, git). A refusal is `Refused(text,
summary)`: the operation's error title, 9.15's summary, no git output (phase 5's PD5, PD6); nothing
written, no backup ref, nothing recorded (phase 5 design 7.2).

| Operation | After | Before |
|---|---|---|
| `MergeAsync` | `RefuseWhileInProgressAsync`, `RefuseIfHeadMovedAsync` | `Git.MergeAsync` |
| `CheckoutAndMergeAsync` | `RefuseCheckoutAsync` | `GuardDetachedHeadAsync` (it may save a backup), the checkout; `into` = `refs/heads/<A>`; its display "Merge failed" (`mergeText.ErrorTitle`) |
| `CherryPickAsync`, `RevertAsync` | `RefuseIfCommitGoneAsync` (picks of kept commits), `RefuseWhileInProgressAsync`, `RefuseIfHeadMovedAsync` | git |
| `RunListAsync`, both lists | `RefuseWhileInProgressAsync`, `RefuseIfHeadMovedAsync` | `apply`; it gains a `Func<Task<IReadOnlyList<string>>>` for its check, or the commits and the revert flag |
| `RebaseAsync` | `RefuseRebaseAsync`, the tip read | `SaveRebaseBackupAsync` |
| `CheckoutAndRebaseAsync` | `RefuseBeforeCheckoutAsync`; `tip` = `refs/heads/<A>` | `GuardDetachedHeadAsync`, the checkout |
| `RunInteractiveRebaseAsync` | `RefuseWhileInProgressOrConflictedAsync`; with its checkout, the tip check and `RefuseDirtyTreeAsync`, otherwise `RefuseIfListMovedAsync` | `GuardDetachedHeadAsync` and the checkout, or `SaveRebaseBackupAsync` |
| `SquashAsync` | `RefuseWhileInProgressOrConflictedAsync`, `RefuseIfListMovedAsync` | `SaveRebaseBackupAsync` |
| `ApplyOrPopAsync` | D179's count | the pop's backup |
| The pull | the fetch, `EndCancelWindow`, D124's second HEAD check | inside `IntegratePullAsync` (9.10) |

### 9.10 The pull: inside `IntegratePullAsync`

The pull chooses fast-forward, merge or rebase inside `IntegratePullAsync`, so its check lives there and
the mode is decided once. `KeepIgnored` is a member of the optional `PullIntegrationOptions(
PullConfiguration? Configuration = null, bool KeepIgnored = false)` on phase 4's overload (WP0 declares
it; `Configuration` is 8.9's); phase 3's overload passes none. False is git's default, so
`PullParityTests` and `PullRebaseParityTests` compile unchanged and keep comparing with `git pull`.
With it, after `FetchMarkedNothingForMerge` and the choice of `arguments`, before `beforeRebase` (which
saves the rebase's backup) and the merge: a fast-forward or merge puts `--no-overwrite-ignore` before
`head` and runs `FindIgnoredInTheWayOfMergeAsync("HEAD", head)` (`head` is `FETCH_HEAD`, whose first
line the fetch marked for merge, or the local upstream); a `--ff-only` pull whose check finds an ignored
file also runs `git merge-base --is-ancestor <before> <head>`: on diverged branches nothing counts as in
the way, and git runs and refuses (4.8's E and E4, `NotFastForward`), so git's refusal wins over the
ignored-files text, as it writes nothing; with nothing in the way the call log is unchanged. A rebase runs
`FindIgnoredInTheWayOfRebaseAsync("HEAD", branch.UpstreamRef)`; an unborn branch (`before` null, a
fast-forward that writes every file) gives the paths of `git ls-tree -r -z --name-only <head>` to
`FindIgnoredAtAsync`.

Found: it returns `new PullResult(PullOutcome.IgnoredFilesInTheWay, 0) { IgnoredFiles = …, WouldRebase
= … }` and runs nothing more. Core's `MergeResult.cs` gains the outcome `IgnoredFilesInTheWay` ("Ignored
files are in the way of the integration: nothing was integrated.") and the init-only
`IReadOnlyList<string> IgnoredFiles` (default empty) and `bool WouldRebase` (the pull chose its rebase).
`PullStepsAsync` (in `RepositoryOperations.FetchPull.cs` after WP0, section 18) returns `Failed(text,
StoppedFailure(OperationTexts.IgnoredFilesInTheWayOf(files, pulled.WouldRebase ? "rebase" :
"merge")))` with a log line; `PullRefusalDisplay` returns null for it (no git command), so the dialog
is the default "Pull failed"; in Pull all the row shows "Pull failed" and keeps that dialog for its
`Details…` (4.6, 4.8; D195). The configuration is read before the fetch (8.9); this check stays after
it, where `FETCH_HEAD` and the upstream are what git will integrate.

### 9.11 Checkouts: git's option and the refusal's words

The git layer: a required `bool keepIgnored` on `CheckoutBranchAsync`, `CheckoutTrackingBranchAsync`,
`CheckoutDetachedAsync` (one overload in place of today's two) and `CreateBranchAsync` (used only with
`checkout`), so the compiler finds every caller (the automatic stash always passes `true`). The option
stands just before the positional arguments, as in D169's and D179's commands: `git switch --no-guess
--no-overwrite-ignore <b>`, `git switch --create <b> --track --no-overwrite-ignore refs/remotes/<r>/<x>`,
`git switch --create <b> --no-overwrite-ignore <start>`, `git switch --detach --no-overwrite-ignore
<x>`. `CreateBranchAsync` also gains a required `bool noTrack` for a start point fetched ambiguously
(10.4, D201), placed before the option: `git switch --create <b> --no-track --no-overwrite-ignore
<start>`, and without checkout `git branch --no-track <b> <start>`. `CheckoutRemoteBranchAsync` (in
`RepositoryOperations.RemoteBranches.cs` after WP0, WP-RA's) passes `keepIgnored: KeepIgnored` on each
of its git calls: the existing local branch's `git switch --no-guess`, the tracking checkout, and D201's
creation without an upstream (10.4 #10).

The app layer: git's refusal is its untracked-files block, which `GitFailureClassifier` cannot tell
from an untracked file's, and `DescribeFailure` is synchronous. So every checkout path catches the
`GitException` inside its operation and builds the display with one async helper,
`CheckoutRefusalDisplayAsync(GitFailure failure, string target, string name, bool offerStash)`, in
place of `AutoStashRefusalDisplayAsync`: phase 5's `CheckoutFailureDisplay` first; then, for an
untracked or folder block with paths while `KeepIgnored` is on (always for the automatic stash),
`FindIgnoredAsync(failure.BlockedPaths)` (a folder by its files); every path ignored gives D179's text
with git's count (a folder once, phase 5's PD9) and no "Stash changes and check out" (a stash keeps no
ignored file: D91, PD8), anything else phase 5's display. The operation returns `Failed(text, failure
with { Summary = display.Summary })`, as the automatic stash does.

Where each path catches, and `name` in D179's text: `CheckoutAsync` around `plan.Run`, `plan.Name` (the
branch `feature/alpha`, the remote branch `origin/feature/alpha`, the tag or the short SHA);
`StashAndCheckoutAsync` already, the read **before** `PopBackAsync`, `plan.Name`, this text alone
replacing today's "Checkout failed" (`text.FailureSummary`); `CheckoutAndMergeAsync` around the
checkout, `<A>`; `CreateBranchAsync` with checkout around `Git.CreateBranchAsync`, the start point as the
dialog names it (a short name, or a commit's short SHA); `CheckoutAndRebaseAsync`, the interactive
list's checkout and `AbortAsync`'s return already, the branch (after Abort only the way back failed);
the automatic stash already, unchanged. Phase 5 design 9.1's second row ("Git runs it as today (its
default for ignored files, Q5)") becomes "with `--no-overwrite-ignore` while Keep ignored files is on
(D193)"; HEAD's own commit gets the option too, with the same result (it writes nothing).

### 9.12 The race with git's write

A checkout checks and writes in one process. Every other covered operation leaves about one process
start (40 to 300 ms on the development laptop) between `check-ignore` and git's write. A file another
program creates at such a path meanwhile is overwritten as git would, except in a fast-forward, which
git's option refuses (worded as phase 5 design 9.3's merge block, "1 conflict prevents the merge. …",
git's output naming the file); a file removed meanwhile makes the app refuse for nothing, and the next
try runs. The checks run inside the operation (D124), so a wait in the queue does not widen the window.

### 9.13 What is not covered

Hard reset (D196): its `reset-<time>-changes` snapshot already saves every untracked and ignored file
it overwrites or deletes (`SnapshotForHardResetWithFilesAsync`, D127), its dialog is the confirmation,
and undo restores them. Soft and mixed reset, fetch, push, the fast-forward of a branch not checked out,
commit, stage: no working-tree write. Discard (a file, a hunk, lines, all): tracked paths and listed
untracked files, never an ignored one. The resolver's sides and "Delete it": a conflicted path is in the
index. The restore bar and the backups list's restores: the user's own saved files, with their own
checks (phase 5 design 7.9, 7.10). Undo and redo: D169's guards, whatever the setting. D91's pop and the
automatic stash's pop back: 9.8. Continue, Skip and Abort: 9.14.

### 9.14 Continue, Skip and Abort

No check runs again on them: the start's check covered every commit the todo or the list held.
Recorded in the handoff as not covered: an ignored file the user creates during a stop (a build) at a
path a later pick writes; commits added at an `edit` stop; a Skip, which resets to HEAD; an Abort, which
writes the original branch's files back (its `abort-<time>-changes` snapshot holds what the operation's
own work changed, D165). A merge's Continue is a commit and writes no file.

### 9.15 Texts and error titles

Checkouts keep D179's `OperationTexts.IgnoredFilesInTheWay` with git's count, the title "Checkout
blocked", git's output and command below: "1 ignored file is in the way: feature/alpha tracks a file of
the same name. Move it away first." / "2 ignored files are in the way: feature/alpha tracks files of the
same names. Move them away first."

The app's own refusals have no git output, so they name the files: up to three, then ", …" when there
are more (`FirstNames`, `NamesShown` of `OperationTexts.Undo.cs`, the same partial class). New in
`OperationTexts.Protect.cs`:

- `IgnoredFilesInTheWayOf(IReadOnlyList<string> files, string operation)`: "1 ignored file is in the
  way of the {operation}: {file}. Move it away first." / "{N} ignored files are in the way of the
  {operation}: {first names}. Move them away first." `{operation}` is "merge" (both kinds, a
  fast-forward, the drop's, "Check out and merge"'s, the pull's merge or fast-forward), "rebase" (onto,
  "Check out and rebase", interactive, the pull's), "squash" ("Squash {N} commits"), "cherry-pick" or
  "revert" (one or a list).
- `IgnoredFilesInTheWayOfStash(IReadOnlyList<string> files)`: "1 ignored file is in the way of applying
  the stash: {file}. Move it away first." / "{N} ignored files are in the way of applying the stash:
  {first names}. Move them away first."

For example "5 ignored files are in the way of the cherry-pick: build/a.bin, build/b.bin, build/c.bin,
…. Move them away first." The titles come through the default display (`DefaultFailureDisplay`; each
`DescribeFailure` returns null for a failure without git output): "Merge failed" ("Check out and merge"
sets it too), "Fast-forward failed", "Pull failed", "Rebase failed", "Interactive rebase failed",
"Squash failed", "Cherry-pick failed", "Revert failed", "Applying the stash failed"; the status bar
shows "{title} failed", as for every refusal.

### 9.16 Changes to earlier checks

16.1 lists the checks, pictures, expectations and tests this changes and the lines that pin them; each
goes into the report's "Changes to expected results" in the commit that changes the code. Every
checkout and merge command line of an earlier check gains `--no-overwrite-ignore` just before its
positional argument (9.4, 9.11: P27's merge becomes `git merge --no-edit --no-log -m "Merge branch
'other'" --no-overwrite-ignore refs/heads/other`, P24's `git switch --create topic
--no-overwrite-ignore 19a90447…`, check 18's `git merge --ff-only --no-overwrite-ignore FETCH_HEAD`; a
longer line can wrap in the error dialog and change its height), and every covered operation's call log
gains its check's reads (so does every activity-log picture with "Show reads" on). In particular: P24's
refusals for the untracked file and the folder `notes` and phase 3's 14b refusal for the untracked
`feature/addfile` gain `git check-ignore --stdin -z` (exit 1) with their summaries unchanged, while `14b1-checkout-failed`, refused before git, does not
change; P26 step 2 keeps its text and button (a tracked block, no `check-ignore`) and step 3, HEAD's own
commit, gets the option with the same result; P27 reads the `--diff-filter=A` list of `HEAD
refs/heads/other` before each merge (no `check-ignore`: `notes` is not on disk), the `diff-tree --stdin`
list and `check-ignore` (exit 1, tracked files) before each cherry-pick of Other edits, the same with
`-R` before each revert of Describe the app, and keeps its stash step, summaries, titles and step 8's
reflog entry; phase 3's "not called" assertion on `CheckoutCommand` must follow the new text to stay
meaningful. Unchanged: the automatic stash's and undo's commands (`UndoSwitch`, `UndoDetach`), phase
3's prefix match at `Phase3Checks.A.Pointer.cs:288`, `FailureClassifierTests` and `Phase5ScenarioTests`
(they run git themselves). Docs: architecture.md's "Protected changes and the automatic stash" ("Every
other checkout … runs as before, with git's default for ignored files") and "The ignored-files guard"
(the moved helper); requirements.md C12 and Q5 and plan.md as 16.2 says.

### 9.17 Tests

- **`KeepIgnoredCheckoutTests`** (Git.Tests, real git): every checkout form with `keepIgnored` refuses
  S1X, S2, S3 and S4 (exit 1, `TempRepo.ExactStateAsync` unchanged, no branch from `--create`), also with
  identical bytes and for a branch `-dash`; without it git's default; `MergeAsync` with it refuses S1F
  and overwrites in S1X's real merge; `CreateBranchAsync` with `noTrack`, with and without checkout,
  writes no `branch.<b>.*` key under P's lines (0.5's P3) and succeeds under H's, where without it git
  fails and leaves the branch behind (0.5).
- **`IgnoredInTheWayTests`** (Git.Tests, real git): found for S1X's merge, m16, a fast-forward,
  unrelated histories; S1X's pick, T9, pick 1 adding and pick 2 deleting, 0.4's two reverts, T4, a merge
  reverted with `mergeParent`, a root commit; S1X's rebase, the add-then-delete one, S2, S3, U5 (a
  branch only ahead of its upstream with a local merge) for `git rebase` and for `--rebase-merges`, 0.4's
  side branch from a commit outside the range (found by (5) and (6) of 9.6); T6,
  T7, a squash whose base tracks the file, a 1,000-commit list whose newest commit, reworded, is S1O's
  "Ignore secret" (999 leading picks; the call log's `rev-list` line holds no SHA, so the arguments form
  fails the case on every platform, not only on Windows); ST1 and ST3 on both gits. Not found for m15 (after the base
  read), U4, U2, T5, a stash that only deletes the path, a tracked file matching a rule. Also a
  criss-cross, a path with a space and `é`, a file named `HEAD`, every read leaving the index and state
  unchanged, the usual call count of each read (merge 1, pick, revert and a list 2, rebase 5, or 7 with
  a merge in the range, interactive rebase 3 or 4, squash 3, the stash 1 after D179's count).
- **`RebaseTodoTests`** (Core): `LeadingPicks` and `AppliedCommits` for plain picks, a reword first, a
  drop first, a pick with a drop after it, a squash group.
- **`IntegratePullTests`** (Git.Tests, new): with `KeepIgnored` the fast-forward, merge and rebase return
  `IgnoredFilesInTheWay` with the files and `WouldRebase`, run no `git merge` or `git rebase`, never call
  `beforeRebase`; without it git's default; U2's ahead-only linear rebase integrates; U5's ahead-only
  branch with a local merge, under `pull.rebase=true` and `merges`, returns `IgnoredFilesInTheWay`, runs
  no `git rebase`, never calls `beforeRebase` and keeps `secret.env`'s bytes, while without `KeepIgnored`
  git's rebase deletes the file; a branch that merged a side branch whose original branch point tracks
  the file is refused (9.6's (5) and (6)); a `--ff-only` pull of diverged branches with the file in the
  way runs 9.10's `merge-base --is-ancestor` and is left to git, which refuses it (`NotFastForward`)
  without writing.
- **`RepositoryOperationsKeepIgnoredTests`** (App): each operation of 9.2 refused with 9.15's text and
  title, without a git write (`git merge`, `git rebase`, `git cherry-pick`, `git revert`, `git stash
  apply|pop`), backup ref or undo entry; the two "Check out and …" refused before their checkout; off,
  phase 5's commands and no read, while a checkout of an older commit of the current branch still
  passes `--no-overwrite-ignore` and is refused with D179's text (9.1: "Check out this commit" on S1O's
  "Add secret" from `main`); every checkout path of 9.11 with
  D179's text, the remote branch's both ways (an existing local branch and a new one) with the option,
  "Stash changes and check out" reading before its pop back; the setting read when the operation runs;
  the pull's "Pull failed" with "merge" and "rebase"; the reads each operation of 9.2 runs before git,
  the pull's three modes included, counted in its call log as `IgnoredInTheWayTests` counts them.
- **`OperationTextsTests`**: the two texts with 1, 2, 3 and 4 files (D113, ", …").
- **`JsonSettingsStoreTests`** (7.11's): `keepIgnoredFiles: false` round-trips.
- **Visual**: G22 to G24 and S21 (17.4) on 17.1's ignored-files scenario; not in the real window (the
  error dialog is phase 5's, repeated in P24).

## 10. Fetch refspecs

No code reads `remote.<r>.fetch` today (plan.md, phase 6, "From phase 3"): a remote branch's name on
its remote is cut out of its tracking name, and its remote is the longest configured remote name that
starts it (`FindRemote`). Under a renaming fetch line the drop's push creates a wrong branch on the
remote, and beside GitHub's pull-request line it breaks every later fetch (0.5's B3, P); "Delete {b} on
{r}…" is refused by D100's lease with a wrong explanation; a namespace without the remote's name makes a
remote row that does not exist (B2). Git has no read of the reverse mapping, so the app reads the lines
and maps them itself, as git's tracking setup does.

### 10.1 `FetchRefspecs` in Core

A new file `src/VisualCommit.Core/Git/FetchRefspecs.cs`, pure, tested without git:

```csharp
// One value of remote.<r>.fetch, read as git reads a fetch refspec (0.5).
public sealed record FetchRefspec(string Remote, string Source, string? Destination, bool IsNegative, bool Force)
{
    // Null for an empty value, one without a source, or one git refuses ("fatal: invalid refspec").
    public static FetchRefspec? Parse(string remote, string value);
}

public enum RemoteSourceKind { Mapped, AmbiguousRemotes, AmbiguousRefs, NotFetched }

// Where a remote-tracking ref is fetched from. Remote: for Mapped and AmbiguousRefs, else null.
// RemoteRef (refs/heads/main): for Mapped, else null. Remotes: for AmbiguousRemotes, in configuration order.
public sealed record RemoteSource(RemoteSourceKind Kind, string? Remote, string? RemoteRef, IReadOnlyList<string> Remotes)
{
    public bool IsBranch { get; }                 // Mapped, and RemoteRef is refs/heads/<x>
    public string? Branch { get; }                // <x> (main), or null when not IsBranch
    public bool IsIdentity(string trackingRef);   // trackingRef is refs/remotes/<Remote>/<Branch>
}

// Every remote's fetch lines, in configuration order.
public sealed class FetchRefspecs
{
    public static FetchRefspecs Parse(IEnumerable<(string Remote, string Value)> lines);
    public static FetchRefspecs Default(IEnumerable<string> remotes);   // +refs/heads/*:refs/remotes/<r>/*, for tests
    public IReadOnlyList<FetchRefspec> Lines { get; }
    public RemoteSource SourceOf(string trackingRef);
    public IReadOnlyList<string> DestinationsOf(string remote, string remoteRef);   // the forward mapping, 10.4's pushes
}
```

`RemoteSource`'s record equality compares `Remotes` by reference, so two equal answers are not always
`==`: a caller (WP-RA) compares `Kind`, `Remote` and `RemoteRef`, or the lists by sequence (WP3).

**Parsing a line** (0.5's line forms): an empty value is no line; a leading `+` is force, and `^` may
not follow it (`+^…` is invalid); otherwise a leading `^` makes the line negative: no `:`, a full name,
at most one `*`, and not a 40-character commit id. The rest is split at its last `:`; for an exact line
no destination, or an empty one, gives no tracking ref (the line fetches into `FETCH_HEAD` only and maps
nothing), while a pattern line with no destination (`refs/heads/*`) or an empty one (`refs/heads/*:`)
is invalid, so "no destination maps nothing" holds for exact lines only; each side has at most one
`*`, and both sides or neither; `@` alone as a source means `HEAD`. These rules are copied from git
(WP3). `Parse` skips an invalid line, but no snapshot is made with one: git refuses the whole
configuration, and `git for-each-ref`, which reads the upstreams, fails with `fatal: invalid refspec
'…'`, so the whole refs read fails, as it did with `git remote` (0.5's line forms). An empty source
(`:refs/remotes/origin/x`) is skipped too: git accepts it, and its tracking setup takes it with an
empty name, which names no branch. So the read's warning `Skipped a fetch line of the remote {name}
that git refuses or that names no source: {value}` (14.14) fires only for a line git accepts that the
mapping leaves out (an empty source), once per repository object, not at every refs read.

**`SourceOf(T)`**, for a tracking ref `T` (`refs/remotes/…`), per remote that has lines:

1. Candidates, in configuration order, from each positive line with a destination: a pattern line whose
   destination's part before `*` starts `T` and part after `*` ends it (`T` at least as long as both)
   gives its source with `*` replaced by the text between (any text, slashes included, and also none:
   `refs/heads/f*:refs/remotes/origin/g*` maps `origin/g` to `refs/heads/f`); an exact line
   whose destination is `T` gives its source. The same source twice is one candidate.
2. When any candidate matches a negative line of that remote (its exact name, or its one-`*` pattern
   over the full name), the remote maps nothing (git's rule, N). This holds for an exact line's source
   too, which is stricter than git's tracking setup and matches its fetch (0.5's X): with
   `refs/heads/secret:refs/remotes/origin/s` and `^refs/heads/secret`, `git branch --track` tracks
   `refs/heads/secret`, but the fetch never writes `origin/s`, and the mapping says NotFetched (a
   deliberate difference, 10.9).
3. Otherwise one candidate maps `T`; **two or more different candidates make `T` AmbiguousRefs**,
   stricter than git, which takes the first in configuration order (C2): beside the default line, git's
   first candidate for GitHub's `origin/pr/1` is the wrong `refs/heads/pr/1`, and git's fetch cannot
   fill `T` from both. Such a ref still checks out, into a branch without an upstream (D201, 10.4 #10).

Then no remote mapping `T` gives NotFetched, two or more give AmbiguousRemotes (git's "not tracking:
ambiguous information") whatever each remote's own result, one gives its result. By 0.5's cases: the
default line is Mapped and the identity; B1 (`origin/up/main` ← `main`), B2 (`mirror/main` ← `main` of
origin), C1 and D (`origin/a` ← `team/a`) are Mapped renamed branches; P2 (`origin/pr/1` ←
`refs/pull/1/head`) and O2's short `main` (taken as it is) are Mapped, not a branch; H and T are
AmbiguousRemotes; C2 (`origin/trunk`) and P (`origin/pr/1`) are AmbiguousRefs; G, E, N, X (the
excluded exact line), a namespace no line fetches any more and a legacy `.git/remotes` file are
NotFetched. F's mirror line `+refs/*:refs/*` (WP3): on a remote beside origin's default line, git and
the mapping agree that origin's tracking refs are AmbiguousRemotes (R1; git says "ambiguous
information"), so their checkout and the items that need a mapped branch are disabled for all of them
(10.8 #7); on origin itself, beside its default line, it is a deliberate difference of C2's kind (the
mapping says AmbiguousRefs, git takes the first line); alone, `refs/remotes/origin/main` maps to
itself, Mapped and not a branch.

**`DestinationsOf(r, ref)`**, for a ref on the remote (`refs/heads/…`): git's forward mapping, which the
fetch uses. Each positive line of `r` with a destination whose source is `ref` (exact) or matches it
(the one `*` as in rule 1) gives its destination, the matched text put in; in configuration order, the
same destination once; none when a negative line of `r` matches `ref`. By 0.5's cases: the default
maps `refs/heads/main` to `refs/remotes/origin/main`; P maps `refs/heads/pr/1` to
`refs/remotes/origin/pr/1` only; C2 maps `refs/heads/main` to `refs/remotes/origin/main` and
`refs/remotes/origin/trunk`; E maps `refs/heads/secret` to nothing. It follows the fetch, not the
push (WP3): under C2 `git fetch` fills both `origin/main` and `origin/trunk`, while `git push origin
main` updates only the first line's tracking ref (git's `remote_find_tracking`, 0.5's C2); 10.4's guard
asks what the fetch will do, so it rightly follows the fetch. Under F's `+refs/*:refs/*` the list for
`refs/heads/<y>` includes `refs/heads/<y>` itself, which is no tracking ref (10.4's guard leaves it out).

### 10.2 The read: `ReadRefsAsync` and `RepoRefs.Refspecs`

`GitRepository.ReadRefsAsync` reads the remotes and their fetch lines in one call, **in place of `git
remote`** in its `Task.WhenAll`: `git config --null --get-regexp ^remote\.` (a read, `GIT_OPTIONAL_LOCKS=0`;
exit 1 is no remote and no line; any other failure throws as the other four do). Each `<key>\n<value>\0`
entry is split as `ReadRemotesAsync` splits its own: a key with a subsection (`remote.<name>.<var>`, the
name the text between `remote.` and the last `.`, its case and dots kept) names a remote, each name
once, sorted ordinally as today; a key without one (`remote.pushdefault`), a key with an empty remote
name (`remote..url`, WP3) and a name starting with `/` (which git skips with a warning; not run) are
skipped; the `.fetch` values (remote
`RemoteArguments.TrimSuffix(key["remote.".Length..], ".fetch")`) go to `FetchRefspecs.Parse`. The names
are exactly `git remote`'s (0.5: a remote with only `prune` counts, a dotted name stays whole, a legacy
`.git/remotes` file counts in neither). Not `--local`: git fetches with global lines too, and prints
them first. `ReadRemotesAsync` (the remotes dialog's read, 2.23) keeps its `git remote`. `RepoRefs`
gains, keeping its positional shape (48 places build it):

- `FetchRefspecs? Refspecs { get; init; }`: the lines as read; null when not read (the fakes, every
  snapshot a test builds by hand).
- `RemoteSource SourceOf(string trackingRef)` and `SourceOf(GitRef remoteBranch)`: `Refspecs.SourceOf`;
  **while `Refspecs` is null, exactly phase 5's assumption**: Mapped to the ref's `RemoteName` and
  `refs/heads/<Name less "<RemoteName>/">` (for a name, the longest of `Remotes` it starts with), so
  hand-built snapshots keep their behaviour without listing their remotes.
- `string? LocalNameOf(GitRef remoteBranch)`: 10.6's rule.

The menus stay pure functions of the snapshot (architecture.md, "Menus"). A change of `.git/config`
reaches the refs through the git folder's watcher (D52), and `ApplyRefs` applies them even when the
fingerprint is unchanged, so the menus follow a changed refspec. Cost: no process more; each refs read
stays at five (0.1), so Q1's path and a dashboard row's read (17.6) keep theirs. The call prints every
`remote.` key where `git remote` printed the names: a few hundred bytes more for a repository with a
few remotes.

### 10.3 `GitRef.RemoteName` from the mapping

`ParseRefsAsync` sets a remote branch's `RemoteName` to `SourceOf(fullName).Remote` for Mapped and
AmbiguousRefs (both name one remote), and to today's `FindRemote` (the longest configured name, else
the first part) for AmbiguousRemotes and NotFetched: `RefReadingTests`' `team/fork/main` (T) keeps
`team/fork`, B2's `refs/remotes/mirror/main` gets `origin`. `GitRef`'s shape does not change (tests
compare whole records). So the left panel groups B2's branches under `origin` with the leaf
`mirror/main` and has no `mirror` row whose Fetch, Rename and Remove would fail; hiding a remote
(`GraphFilterChanges.BranchesOf`), "Remove {r}…"'s count (`RemoteBranchesOf`) and its "remote" backup
take the refs the remote fetches, which `git remote remove` deletes (0.5, B2). WP3 built this: every
snapshot the app reads now has `Refspecs` and a `RemoteName` from the mapping, before WP-RA starts.

### 10.4 Every place, and what each does

`T` is the tracking ref's short name as shown (`GitRef.Name`, `origin/up/main`), `r` the mapped remote,
`y` the branch's name on the remote (`RemoteSource.Branch`); R1 to R4 are 10.5's reasons.

| # | Where | Identity | Renamed, a branch | Not a branch | Ambiguous (R1, R2) or not fetched (R3) |
|---|---|---|---|---|---|
| 1 | `RemoteName` (`ParseRefsAsync`) | as today | `r` | `r` | R2: its one remote; R1, R3: `FindRemote` |
| 2 | `LocalNameOf` (static in `RepositoryMenuContext` today; moves to `RepoRefs`, 10.6) | as today | `y` | today's: `T` less `<RemoteName>/` | R2: today's, `T` less `<r>/` (D201); R1, R3: null |
| 3 | `DropFacts.RemoteMapsBranchesDifferently` | removed (10.7) | | | |
| 4 | `ReadDropFactsAsync` | reads unchanged; its remark removed | | | |
| 5 | `ForDrop`'s "Push {A} to {T}" and "Push {A} to {T} and track it" | as today | enabled, labels as today | disabled R4 | disabled R1, R2, R3 |
| 6 | `ForRemoteBranch`'s "Check out {T}" | as today | enabled; `{y} is already checked out` while HEAD is on `y` | enabled, today's local name | R2: enabled, today's local name (D201); R1, R3: disabled |
| 6 | its "Delete {b} on {r}…" | as today | "Delete {y} on {r}…" | disabled R4, today's label | disabled R1, R2, R3, today's label |
| 7 | `SplitRemoteBranch` | replaced by `Refs.SourceOf`: `(r, y)` for Mapped and a branch, else null (the operation logs "not started": a menu built before the mapping changed) | | | |
| 8 | `PushBranchToAsync` | as today | `git push --progress [--set-upstream] <r> refs/heads/<A>:refs/heads/<y>`; title, running text and result name `{T}` ("Pushing topic to origin/up/main…", "Pushed topic to origin/up/main"), as `UpstreamText` names an upstream; dedupe key `push:refs/heads/<A>-><r>/refs/heads/<y>` | not started | not started |
| 9 | `DeleteRemoteBranchAsync` | as today | question, title, running text and result name `{y}` on `{r}`; lease `refs/heads/<y>:<T's commit>`, read inside the operation as today; backup `remote-branch-<t>/<r>/<y>`; undo `PushedRef(refs/heads/<y>, …, null)`; git deletes `T` too, through the fetch line (0.5) | not started | not started |
| 10 | Checkout of a remote branch (`CheckoutRemoteBranchAsync`, `CreatedBranchOf`, `LocalNameOfRemoteBranch`, moved to `RepositoryOperations.RemoteBranches.cs` by WP0, section 18; `CheckOutByDoubleClick`) | as today | the local branch `y` (10.6): checked out when it exists, else `git switch --create <y> --track [--no-overwrite-ignore] <T>` | `git switch --create <today's name> --track [--no-overwrite-ignore] <T>`; git tracks the ref on the remote (P2) | R2 (D201): the local branch of today's name checked out when it exists, else `git switch --create <n> --no-track [--no-overwrite-ignore] <T>` (0.5's P3), result `Created {n} at {T} without an upstream: {T} is fetched from more than one ref of {r}`; a double-click the same. R1, R3: refused before git with the reason in "Checkout failed" when a stale menu reaches it (git fails, and for R1 leaves a branch behind, 0.5); a double-click does nothing |
| 11 | "Set upstream…" / "Change upstream…" (`SetUpstreamAsync`; the menu's `hasRemoteBranches`) | listed | listed | left out | left out (git refuses R1 and R3; R2 would get git's first candidate); with none left, today's "No remote branches to track: fetch or push first" |
| 12 | A force push's backup restored when no local branch tracks it (`PushForcePushBackAsync`, `ForcePushRestore`) | as today | `Refs.SourceOf("refs/remotes/<name>")`: `r` and `refs/heads/<y>` (phase 5 design 0.5's and 7.10's promise, which the code lacked) | disabled `The remote this backup came from cannot be found` | the same |
| 13 | The conflict side's remote branch (`MergedRemoteBranch`) | as today | matched as `Refs.SourceOf(ref) is { IsBranch: true, Branch: <branch> }` instead of `Name == "{RemoteName}/{branch}"` | not matched | not matched |

Unchanged code: "Push {b} back to {r}…" for a remote branch's or tag's backup (`PushBackAsync`,
`SplitRemote`), because the deletion now records `<r>/<y>`, which the longest remote name splits back;
the undo history's remote refs (`PushOutcome`, `UndoEffect.RemoteRef`, `UndoSequence.Push`), full names
from git's output or the deletion's capture; the left panel's remotes, hiding a remote,
`RemoteBranchesOf` and "Remove {r}…", which follow `RemoteName` (10.3); undo of a remote's removal
(`UndoScope.RemoveRemote`), known issue 1 (10.8). Merge, rebase, compare, "Create branch here…",
"Create tag here…", hide, solo and "Copy branch name" stay enabled in every case (they act on the
tracking ref's commit); "Create branch here…" from an R1 or R2 ref passes `--no-track` (9.11's
`noTrack`; D201 for R2; WP0 wired it in the create-branch operation, and it takes effect now that
WP3 fills `Refspecs`, so WP-RA only tests it), since git's `branch.autoSetupMerge` would otherwise give the branch R2's first
candidate as its upstream (P's `refs/heads/pr/1`, whose push breaks every fetch) or, for R1, fail with
`not tracking: ambiguous information` and leave the branch behind (0.5's implicit upstream). Push, Pull
and Force push of a local branch use D101's upstream values, with the guard below. The operations use
the latest refs snapshot; a mapping changed after the menu opened is caught by the deletion's lease or
by row 7's null.

`[--no-overwrite-ignore]` in row 10 stands for git's option while `KeepIgnored` is on (9.11), on every
git call of the row in every column, the existing local branch's `git switch --no-guess <b>` included;
WP-RA's rewrite of `CheckoutRemoteBranchAsync` keeps `keepIgnored: KeepIgnored` on each (section 18).

**How an upstream is named** (GD15). An upstream is named by its remote-tracking ref when the fetch
lines map it to one, as `git status` names it: git's own `%(upstream)` (`GitRef.UpstreamRef`, git's
first matching fetch line, as a push updates it) shown without `refs/remotes/` when it lies there;
only when it does not, `{remote}/{remote ref}`, as today. (Built so by WP-RA, the review's lab
confirming that `%(upstream)` is what `git status` shortens for B1, C2 in either order, B2, E and an
excluded exact line, on 2.36 and 2.55; the first draft's "first of `FetchRefspecs.DestinationsOf`
under `refs/remotes/`" differs only for a mirror line before a renaming line, where git's
`%(upstream)` is `refs/heads/main`, git says `main` and the app `origin/main`: 10.8 #9.) So `pr/7`, checked out from `upstream/pr/7` (row 10's "not a
branch": git tracks `refs/pull/7/head` on `upstream`), shows `upstream/pr/7` in Pull's tooltip, the
push dialog and a dashboard row, not `upstream/refs/pull/7/head`; with the default lines every
upstream reads as today. D101 still holds (the three values are read; the text is only display).
WP-RA builds it in the place that makes `UpstreamText` (`ToolbarViewModel.UpstreamText`, which
`RepositoryOperations.UpstreamText` and `RepositoryMenuContext.UpstreamText` call), so every text
that names an upstream (14's `{up}`) follows; `UpstreamNameTests` (new, WP-RA's) proves the rule
with and without a mapping line, and G25 asserts it in Pull's tooltip (17.4).

**A push that would stop the fetch.** A local branch can still push into P's trap: one made by phase
5's checkout or by `--track` in a terminal tracks `refs/heads/pr/1` on `origin` (0.5's P), and the push
dialog offers the branch's own name. So `PushToAsync` (Push to the upstream, the push dialog, the
drop's two pushes) and `ForcePushAsync`, inside the operation before git and with `Refspecs` read,
refuse a push of `refs/heads/<A>` to `<r>`'s `refs/heads/<y>` when `Refs.Refspecs.DestinationsOf(r,
"refs/heads/<y>")` holds a tracking ref whose `SourceOf` is AmbiguousRefs and none whose `SourceOf` is
Mapped and that the snapshot holds (which would show that `<y>` already exists on `<r>`): the push would
create `<y>` beside the ref that fills that tracking ref, and every later fetch of `<r>` would fail
(0.5's P; C2's `trunk`). Only destinations under `refs/remotes/` count: under F's `+refs/*:refs/*` the
list also holds `refs/heads/<y>` itself, which is no tracking ref (10.1; looking each destination up
with `RepoRefs.SourceOf(GitRef)` does as well, since it gives NotFetched for a local branch). The
guard follows the fetch, not the push, rightly (10.1's `DestinationsOf`, WP3). The refusal is `Refused(text, summary)` with the push's error title ("Push failed",
"Force push failed") and 10.5's `PushWouldStopFetch`, no git output, nothing written. By 0.5's cases:
P's push of `pr/1` is refused; C2's push of `main` runs (`origin/main` is Mapped and fetched), a new
`trunk` is refused; the default line's pushes and a renamed branch's (row 8) run.

### 10.5 The reasons R1 to R4, and the texts that take the remote's name

New in `OperationTexts.MenuTexts`, in the partial file `OperationTexts.Menus.Refspecs.cs` (section 18),
with one helper `RemoteSourceReason(RemoteSource source, string trackingName, bool needsBranch)` giving
the reason that applies (null for Mapped, and for Mapped not a branch when `needsBranch` is false):

| Reason | Text | Example |
|---|---|---|
| R1 AmbiguousRemotes | `{T} is fetched from {remotes}` (`OperationTexts.List`: "a and b", "a, b and c") | "origin/main is fetched from origin and backup" |
| R2 AmbiguousRefs | `{T} is fetched from more than one ref of {r}` | "origin/pr/1 is fetched from more than one ref of origin" |
| R3 NotFetched | `{T} is not fetched from any remote` | "origin/secret is not fetched from any remote" |
| R4 Mapped, not a branch (push and delete only) | `{T} is {ref} on {r}, not a branch` | "origin/pr/1 is refs/pull/1/head on origin, not a branch" |

Two more take R2's words (D201, 10.4):

- `CreatedWithoutUpstream(string branch, string trackingName, string reason)`, the status-bar result of
  R2's checkout, no full stop: `Created {n} at {T} without an upstream: {R2}`, "Created pr/1 at
  origin/pr/1 without an upstream: origin/pr/1 is fetched from more than one ref of origin".
- `PushWouldStopFetch(string branch, string target, string trackingName, string remote)`, the summary of
  10.4's push refusal: `Pushing {A} to {target} would stop every fetch of {r}: {D} is fetched from more
  than one ref of {r}.`, "Pushing pr/1 to origin/pr/1 would stop every fetch of origin: origin/pr/1 is
  fetched from more than one ref of origin."; `{target}` as the push's title names it, `{D}` the
  ambiguous tracking ref's short name.

R1 to R4 replace `MapsBranchesDifferently` ("{remote} maps its branches differently: use Push {branch}…"),
which pointed to an item that exists only for a branch without an upstream. Like every disabled reason
they are the item's tooltip and automation help text, without a full stop. Under a renaming refspec
these keep their form and take `y`: "Delete {y} on {r}…", "Delete {y} on {r}?", "Delete the branch {y}
on {r}? Everyone who uses {r} loses it.", "Deleting {y} on {r}…", "Deleted {y} on {r}", "{y} on {r} has
changed since you last fetched. Fetch and look at it before deleting.", "Push {y} back to {r}…",
"Created {y} tracking {T} and checked it out"; for a renamed drop target "Push {A} to {T}", "Pushing {A}
to {T}…", "Pushed {A} to {T}".

### 10.6 Checkout's local name (amends D93, with D201)

D93: checking out a remote branch checks out the local branch of the same name when it exists, and
otherwise creates one tracking the remote branch. "The same name" is now the branch's name on the
remote, as `git switch <name>` and `git checkout <name>` find it (0.5: B1's `git checkout feature/x`,
D's `git checkout team/a`): `origin/up/main` checks out into `main`, `origin/a` (from `team/a`) into
`team/a`. `RepoRefs.LocalNameOf`: Mapped and a branch, `y`; Mapped and not a branch, today's rule (`T`
less `<RemoteName>/`: `origin/pr/1` → `pr/1`, which git then tracks as `refs/pull/1/head`, P2);
AmbiguousRefs, today's rule too (P's `origin/pr/1` → `pr/1`, D201), the branch then made without an
upstream (10.4 #10), since git would track the wrong first candidate and its Pull fail (0.5's P);
AmbiguousRemotes and NotFetched, null, which disables the item and makes a double-click do nothing. The
undo history's created branch (`CreatedBranchOf`) uses the same name; with the identity mapping nothing
changes.

### 10.7 `DropFacts.RemoteMapsBranchesDifferently` removed

`DropFacts` becomes `DropFacts(bool TargetIsAncestorOfSource)` with its `SourceContainsTarget`
property; the parameter, its doc comment, the known-issue comment and `ReadDropFactsAsync`'s remark go.
`ForDrop` reads `context.Refs.SourceOf(targetBranch)` from the snapshot, as `ForRemoteBranch` does for
its delete label. Plan.md's sentence ("Read the refspec and set `DropFacts.RemoteMapsBranchesDifferently`
…", line 386) becomes history (16.2); the handoff says what replaced it.

### 10.8 Known issues

Recorded in the handoff; 1 also goes to the backlog and 2 to plan.md's risks (16.2):

1. Undo of a remote's removal re-creates refs only under `refs/remotes/<r>/` (`UndoScope.RemoveRemote`'s
   `RefPrefixes`): under B2 the `mirror/*` refs do not come back; a fetch brings them back, and the
   "remote" backup keeps their commits now that `RemoteName` follows the mapping. Fixing it needs the
   destinations in the undo entry.
2. `remote.<r>.mirror = true` (`git clone --mirror`, `git remote add --mirror=push`) makes every push
   the app makes to that remote fail with `fatal: --mirror can't be combined with refspecs` (128, 2.36
   and 2.55): Push, the push dialog, tags, deletions, undo's `RemoteRef`; the error dialog shows git's
   words.
3. A namespace no remote fetches any more (a refspec changed, a remote renamed by hand) still makes a
   remote row (NotFetched: `FindRemote`'s first-part guess) whose Fetch, Rename and Remove fail with
   git's words; its branches' items say R3.
4. A short source (O2) makes git write `branch.<b>.merge = main`; that branch's Push stops in
   `RemoteArguments.RequireFullRef` ("Not a full ref name: main"), and R4 says "main on origin, not a
   branch". Rare.
5. The push dialog's "Set as upstream" to a branch the remote's lines do not fetch (D's `solo`): git
   writes the keys and the result says so, while the app shows no upstream.
6. A `remote-branch-…` backup phase 5 saved before a deletion refused under a renaming refspec carries
   the tracking name (`origin/up/feature/x`); its "Push up/feature/x back to origin…" would create that
   wrong name on the remote. The backups list offers it until the clean-up removes it: after 30 days,
   or sooner once more than 50 backups push it out (D139), longer only while an undo entry still needs
   it (D171).
7. A mirror fetch line `+refs/*:refs/*` (F, `git remote add --mirror=fetch`) on a remote beside
   origin's default line also maps every `refs/remotes/origin/*`, so git and the mapping agree that
   origin's tracking refs are fetched by two remotes (R1; git says `not tracking: ambiguous
   information`): their checkout, the drop's pushes and the deletion are disabled with R1 and "Set
   upstream…" leaves them out, for every one of them (10.1, WP3).
8. The push guard (above) refuses some pushes that would not stop the fetch: under P a new branch
   `pr/fix-typo` counts as filled by two refs (the PR line could fill `origin/pr/fix-typo` from
   `refs/pull/fix-typo/head`), and with two pattern lines of one remote that rename into two namespaces
   every new branch does, while git's next fetch exits 0 after either push (WP-RA's review, 2.36 and
   2.55). The refusal's text says it would stop every fetch. Kept on purpose: refusing only when another
   source fills the tracking ref now would leave a pull request never fetched unguarded, and that push
   does stop every fetch (`Cannot fetch both`, 128). A terminal push still works.
9. With a mirror line before a renaming line, git maps a branch's upstream to `refs/heads/main` and
   `git status` says `main`; the app names it `origin/main` (GD15's fallback).

### 10.9 Tests

- **`FetchRefspecsTests`** (Git.Tests beside `RefNameRulesTests`, no git): `Parse` for every form of
  0.5 (valid, invalid, negative, no destination, empty value, empty source, `+^`, `^…:…`, two `*`, `*`
  inside a component and in the middle; WP3's `@` as `HEAD`, a negative commit id, a pattern with no or
  an empty destination); `SourceOf` for the default with and without `+`, B1, B2, C1, C2
  and N in both orders, D, E, X, P, P2, G, H, T, O2, `f*`/`g*` (a `*` matching nothing too), `*/x`→`*/y`,
  F beside the default on another remote (either order) and on the same one, and alone; `DestinationsOf` for the
  default, B1, C2 (`main` to both), D, E (nothing), P and F (`refs/heads/main` among them); `IsIdentity`; `Default` against phase 5's split
  for remote names with slashes; `RepoRefs.SourceOf` with `Refspecs` null equal to phase 5's assumption;
  `RepoRefs.LocalNameOf` for each kind (R2 today's name, R1 and R3 null).
- **`FetchRefspecsParityTests`** (Git.Tests, real git; D96's model): each case's lines (`git config
  --add`, tracking refs by `update-ref`, no fetch), then `git branch --track probe <T>`: exit 0 with
  `branch.probe.remote` and `.merge` equal to the Mapped result, 128 with `not tracking: ambiguous
  information` for AmbiguousRemotes, with `is not a branch` for NotFetched; five deliberate differences
  asserted as such: C2, P, the empty source, the excluded exact line (X, where the test also shows the
  fetch leaving `origin/s` alone) and F on the same remote as the default; `git branch -D probe` after
  each, its failure ignored. It also checks the fetch against `DestinationsOf` (16 cases, a real fetch
  from a bare server: the tracking refs written are exactly those `DestinationsOf` gives) and C2's push
  (only the first line's tracking ref moves, 10.1). Symbolic refs are left out of the comparison: Git
  2.48 and later create `refs/remotes/origin/HEAD` from the server's HEAD, which no line maps (WP3).
- **`RefReadingTests`**: `Refspecs` read; a remote name with dots keeping its case; two sections of one
  remote in order; the test's global line before a local one; no line gives an empty `FetchRefspecs`, not
  null; `RemoteName` `origin` for B2, `team/fork` for T; `RepoRefs.Remotes` equal to `git remote`'s
  output (parity) for a dotted name, a remote with only `prune`, a `remote.pushDefault` and a legacy
  `.git/remotes` file, which both leave out; five processes per refs read, `git remote` not among them.
- **`RepositoryMenusTests`**: `ForDrop` and `ForRemoteBranch` for identity, renamed, not a branch, R1,
  R2 (its checkout enabled, D201), R3;
  `A_remote_that_maps_its_branches_differently_offers_no_push_by_drop` rewritten on purpose with
  refspecs in the snapshot; "Set upstream…" disabled when no remote branch can be tracked.
- **`RepositoryOperationsRemotesTests`, `RepositoryOperationsPushTests`** (App, a real bare remote): the
  drop's push onto `origin/u/main` moves the server's `main` and makes no `u/main`; deleting
  `origin/u/feature/x` deletes the server's `feature/x` with the backup
  `remote-branch-<t>/origin/feature/x`, which Undo and "Push feature/x back to origin…" bring back;
  checking out `origin/up/main` creates `main` tracking it; the force-push restore's fallback; no R1 ref
  in the upstream pick list; a double-click on an R1 ref runs nothing and leaves no branch;
  `MergedRemoteBranch` with a renamed branch. Under P's lines (D201): checking out `origin/pr/1`, and
  double-clicking it, creates `pr/1` at its commit with no `branch.pr/1.*` key and the result
  `Created pr/1 at origin/pr/1 without an upstream: …`, and with `pr/1` there checks it out; "Create
  branch here…" from `origin/pr/1` writes no `branch.<b>.merge`, and from an R1 ref makes the branch
  without an upstream where git alone would fail and leave it behind. The push guard: a branch made
  with `git switch --create pr/1 --track refs/remotes/origin/pr/1` (git's upstream `refs/heads/pr/1`)
  pushed by Push and by Force push, and the push dialog's push of `pr/1` to `origin`, run no `git push`,
  show `PushWouldStopFetch` and leave the server without `refs/heads/pr/1`; C2's push of `main` runs.
- **`LeftPanelViewModelTests`**: B2's branches under `origin`, no `mirror` row.
- **Visual**: G25 (17.4) on 17.1's refspecs scenario; not in the real window.

## 11. Theme (D194)

D194: Dark, Light or System, Dark the default. Also: light text at 4.5:1 and the lanes (11.10,
11.13; D198), a token for the danger button's text (11.11), tooltips in the app's colours (11.12),
and every main screen pictured in both themes (11.14).

### 11.1 What the user sees

- The toolbar's Theme switch (`ThemeSwitch`) still takes one click and always changes what the
  window shows: dark becomes Light, light becomes Dark, saved as such, which leaves System (11.6).
  System is chosen in the settings screen and the palette (11.7).
- With System, the window, its title bar on Windows 11 and every open view, menu, dialog and
  tooltip follow the operating system's mode without a restart; the first frame already has the
  right theme (11.5), except that on Linux the portal may answer a moment later (11.9).

### 11.2 The setting

`AppTheme` (`src/VisualCommit.Core/Settings/AppSettings.cs:3-8`, doc "The colour theme of the app
(D194). Dark is the default.") gains `System` at the end ("The operating system's light or dark
mode, followed while the app runs."), so `Dark` stays 0 and the default (`AppSettings.Theme`,
`:37`). `JsonSettingsStore` writes enums by name (`UseStringEnumConverter`,
`Services/JsonSettingsStore.cs:122-127`): `"theme": "System"`. A phase 5 build cannot parse it, sets
the file aside as `settings.unreadable.json` and uses the defaults (`:58-62`): accepted, as nothing
is released before phase 8. The theme stays a preference in `settings.json` (D46).

### 11.3 The seam: `ISystemTheme`

`Services/SystemTheme.cs` (new):

```csharp
/// <summary>The operating system's light or dark mode, as the platform reports it (D194).</summary>
public interface ISystemTheme
{
    bool IsDark { get; }           // the system asks for dark apps now
    event EventHandler? Changed;   // on the UI thread when IsDark changes; never for an accent colour alone
}

/// <summary>Over Avalonia's platform settings (0.6); no settings at all: Light, as Avalonia answers.</summary>
public sealed class PlatformSystemTheme : ISystemTheme, IDisposable
{
    public PlatformSystemTheme(IPlatformSettings? settings);   // IsDark = IsDarkIn(settings?.GetColorValues()); subscribes to ColorValuesChanged when not null
    public static bool IsDarkIn(PlatformColorValues? values);  // values?.ThemeVariant == PlatformThemeVariant.Dark
    public void Update(PlatformColorValues? values);           // the event's handler
    public void Dispose();                                     // unsubscribes
}
```

`Update` posts itself to the UI thread when called off it (`Dispatcher.UIThread.Post`, a guard only:
Avalonia raises there, 0.6); when `IsDarkIn(values) == IsDark` it does nothing (an accent change
raises nothing), else it sets `IsDark` and raises `Changed`. `IsDarkIn` and `Update` are public
because `App` has no `InternalsVisibleTo`; their doc comments say they exist for the platform's
event and the tests. High contrast: Avalonia maps the scheme to Light or Dark (0.6) and the app
follows that variant with its own tokens; a high-contrast palette is phase 8's accessibility pass.

`FakeSystemTheme(bool isDark = true) : ISystemTheme` (`tests/VisualCommit.Testing/Headless/`, new):
`Set(bool dark)` on the UI thread (`Dispatcher.UIThread.VerifyAccess()`), raising `Changed` only on a
change. It starts dark, so a System check looks like the default until it flips the fake; a restart
passes the same fake to the second start. `ThemeVariant.Default` is used nowhere: headless it always
means Light and no test could change it (0.6).

### 11.4 `ThemeService`'s rules

```csharp
/// <summary>The app's theme: the user's choice, and what the window shows (D194).</summary>
public interface IThemeService
{
    AppTheme Setting { get; }      // as chosen: Dark, Light or System (today's Current, renamed)
    bool IsDark { get; }           // the window shows dark: Dark, or System while the system is dark
    event EventHandler? Changed;   // on the UI thread after Setting or IsDark changed
    void Apply(AppTheme theme);
}

public sealed class ThemeService(Application application, ISystemTheme system, IAppLog log) : IThemeService, IDisposable
```

1. **Start**: `Setting = Dark`, `IsDark = true` (as `App.axaml:5`); subscribes to `system.Changed`;
   nothing else until `Apply`.
2. **`Apply(theme)`**: `Setting = theme`; `IsDark = theme switch { Light => false, System =>
   system.IsDark, _ => true }` (outside the enum: Dark); `application.RequestedThemeVariant = IsDark
   ? ThemeVariant.Dark : ThemeVariant.Light` every time (Avalonia ignores an equal value); `Changed`
   when `Setting` or `IsDark` changed. It neither saves nor logs: the caller does (11.6).
3. **The system changes**: while System, it resolves as in rule 2; when `IsDark` flips it raises
   `Changed` and logs `The system's theme changed to {light|dark}; the app follows it.` While Dark or
   Light nothing changes or is raised, and it logs `The system's theme changed to {light|dark}; the
   app keeps the {dark|light} theme.`
4. **Only the service writes `RequestedThemeVariant`**, so `IsDark` and
   `Application.ActualThemeVariant` always agree and the toolbar's icon and tooltip follow `IsDark`.
5. **`Dispose`** unsubscribes; a later change of the system's mode does nothing.
6. Every member is used on the UI thread.

Why the app resolves System itself: the fake then drives the platform's code path in both harnesses,
one service is the one source of the shown variant, Avalonia's own handler stays harmless (0.6), and
`ShellTests.cs:86` (`RequestedThemeVariant == Light` after a Light start) keeps its meaning; about
twenty lines.

### 11.5 `AppSession.Start` and the start-up order

`AppSession.Start` (`AppSession.cs:95-103`) gains `ISystemTheme? systemTheme = null`, documented as
the clocks are, beside 8.12's `gitEnvironment` and 12.9's `tools`. Null: the session makes, owns and
disposes `new PlatformSystemTheme(application.PlatformSettings)`; a fake passed in is the caller's.
The order, so that the first frame is right:

1. Before `App.Initialize`, `Application.RegisterServices` subscribes to the platform's values;
   `App.axaml` loads with `RequestedThemeVariant="Dark"` (`:5`; its comment stays true).
2. `AppSession.Start`, after `JsonSettingsStore` (`:116`), in place of `:118-120`: the system theme,
   `var themes = new ThemeService(application, systemTheme, log)`,
   `themes.Apply(settings.Current.Theme)`, then `Theme: Dark.`, `Theme: Light.` or `Theme: System
   (the system's theme is {dark|light}).` in the log.
3. `new MainWindowViewModel(settings, themes, …)` (`:174`) reads `Setting` and `IsDark`.
4. `new MainWindow` (`:178`): the `TopLevel` constructor takes the application's
   `ActualThemeVariant` and binds the frame to it (0.6), so the first frame and the title bar have
   the resolved variant; no window exists earlier, so nothing flashes.
5. The caller shows the window (`App.axaml.cs:35-36`, `ShellDriver.cs:104`).

New `public IThemeService Themes { get; }` (tests, harness). `Dispose` (`:189-216`) disposes `Themes`,
then the owned `PlatformSystemTheme`, after `Shell.CloseAll()` and before `Log.Info("VisualCommit
closed.")`, so nothing logs after "closed".

### 11.6 The view model and the toolbar's switch

In `ViewModels/MainWindowViewModel.Theme.cs` (18):

| Today | Becomes |
|---|---|
| `Theme` (`MainWindowViewModel.cs:132-136`, an `[ObservableProperty]` set at `:59`) | `public AppTheme Theme => _themes.Setting` ("The theme as chosen: Dark, Light or System (D194)"); `:59` goes |
| `IsDarkTheme` (`:138`, from the setting) | `_themes.IsDark`: what the window shows |
| `ThemeSwitchToolTip` (`:140`) | the table below |
| `ToggleTheme()` (`:191-199`) | `UseTheme(IsDarkTheme ? AppTheme.Light : AppTheme.Dark)` |
| new `public void UseTheme(AppTheme theme)` | nothing when `theme == _themes.Setting`; else `_themes.Apply(theme)`, `_settings.Update(s => s with { Theme = theme })` and today's log line `Theme changed to {theme}.` (`Theme changed to System.`). The toolbar, the palette (5.4, row 21) and the settings screen's Save (7.8) call it |
| new, in the constructor | `_themes.Changed` raises `PropertyChanged` for `Theme`, `IsDarkTheme`, `ThemeSwitchToolTip`; still no Avalonia type in the view model (architecture.md, "Conventions") |

| Chosen | Shows | Icon | Tooltip and help text | A click |
|---|---|---|---|---|
| Dark | dark | `IconSun` | `Switch to the light theme` (unchanged) | Light, saved `"Light"` |
| Light | light | `IconMoon` | `Switch to the dark theme` (unchanged) | Dark, saved `"Dark"` |
| System | dark | `IconSun` | `Switch to the light theme (stops following the system)` | Light, saved `"Light"` |
| System | light | `IconMoon` | `Switch to the dark theme (stops following the system)` | Dark, saved `"Dark"` |

`Views/Shell/ToolbarView.axaml:26-30` keeps `Classes.light="{Binding !IsDarkTheme}"` and the styles
at `:75-83` (the icon of the theme it switches to), and gains
`AutomationProperties.HelpText="{Binding ThemeSwitchToolTip}"`, as phase 5's Undo, Redo and Search
have, so the real-window pass reads the System wording (11.8). Theme has no key (6.1), so no
"({key})" (6.8). Phase 0's checks 3 to 6 and real-window steps A to C start Dark or with an explicit
Light and keep their clicks, tooltips, icons and saved values. Not taken: a cycle Dark → Light →
System (one click in three changes nothing on screen) and a menu (the only toolbar button opening
one); both would change phase 0's checks 5 and 6 and step B (r06 section 4).

### 11.7 The palette's and the settings screen's choices

The palette (5.4, row 21): `Use the dark theme`, `Use the light theme`, `Follow the system's theme`,
no detail and no key, always enabled, running `UseTheme(Dark | Light | System)`; the one chosen now
is left out, so with System chosen and a dark window `Use the dark theme` is listed: it keeps the
window dark and saves Dark.

The settings screen: 7.3 owns the theme's row (radios `SettingsThemeDark`, `SettingsThemeLight`,
`SettingsThemeSystem`; the line `System follows your operating system's light or dark mode.`). On
opening the radio of `Theme` is checked; Save calls `UseTheme(choice)` when it differs from `Theme`
(7.8); Cancel changes nothing; no preview: the toolbar's switch is one (7.1).

### 11.8 The real-window pass: S15 and L34

The pass never changes the operating system's theme: writing `AppsUseLightTheme` and broadcasting
`WM_SETTINGCHANGE "ImmersiveColorSet"` would flip every app on the owner's desktop, a killed pass
(the hang watchdog, a crash) would leave it flipped, and CLAUDE.md lets the pass take the mouse,
never the desktop's settings. No per-process way exists: Avalonia reads the system-wide UISettings,
and a `WM_SETTINGCHANGE` sent to the app's own window re-reads the same value (0.6). Following a
change is proved headless (S15); a fake in the real window would only repeat that.

S15 and L34 are two methods, each a launch of its own (17.4, 17.6), at 1100×700. The methods, the
scenarios and the picture names are 17.4's.

**S15** starts as its scripted twin does, with `"theme": "System"` in `settings.json`, on the "graph"
scenario (`Scenarios.GraphAsync`):

1. Before launching, read the mode: `RealApp.SystemUsesLightTheme()` (17.2) reads
   `AppsUseLightTheme` under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`
   (`1` or missing: light, Windows' default; `0`: dark), and high contrast through
   `SystemParametersInfo(SPI_GETHIGHCONTRAST = 0x0042)`, flag `HCF_HIGHCONTRASTON = 0x1` (new in
   `NativeMethods.cs`). High contrast on: `run.txt` gets `S15 (System at start): failed. High
   contrast is on, and Avalonia follows its scheme: turn it off and run the pass again.` and the
   method fails with the same text. The pass needs high contrast off as it needs the desktop
   unlocked (plan.md's gate step 4 and CLAUDE.md's note on the pass, 16.2, 17.3), and never turns
   it off itself: a gate run with it on is Blocked until the owner turns it off and runs the
   real-window pass again on the same commit. It is off on the owner's machine today (0.6). Not
   taken: a skip, which would leave S15's start unproven on a gate the plan does not make
   conditional (CLAUDE.md, closing step 6).
2. A new data folder with `"theme": "System"` and a session of one tab on the graph scenario;
   `RealApp.Launch(data, session)`; wait for the graph.
3. **The start**: the chrome, panel and window colours of the variant read
   (`Phase0RealWindowPass.cs:16-21`) at spots clear of text, lanes and rows, sampled as `AssertShell`
   does (`:144-149`); `ThemeSwitch`'s help text `Switch to the dark theme (stops following the
   system)` (light) or `Switch to the light theme (stops following the system)` (dark). The frame
   capture `s15-system-start-with-frame` (its title bar is inspected: it follows the variant on build
   22000 and later); the client capture `s15-system-start` against its scripted twin (D33's 3%):
   `s15e-restarted-light` when light (the owner's machine today, 0.6), `s15a-system-start-dark` when
   dark.
4. **The click**: `ThemeSwitch` with the real mouse; the other variant shows and `settings.json` says
   `"Dark"` (from light) or `"Light"` (from dark); the pointer to the neutral point; `s15-theme-click`
   against `s15c-theme-click-dark` (from light) or `s15b-system-light` (from dark: the same light
   window with the moon icon).

**L34** does not depend on the owner's mode: a new data folder with `"theme": "Light"` and a session of
one tab on the ops scenario (`Scenarios.OperationsAsync`); `RealApp.Launch(data, session)`; wait for
the graph. Four captures, each closed before the next, against twins that L31's "ops" method takes:
the activity log open (`l31a-activity-log-light`), "Delete feature/alpha…" from the branch's menu
and its question (`l31b-delete-question-light`) and the palette by a real Ctrl+Shift+P
(`l31c-palette-light`) against the `-light` pictures of their pairs, taken under System with the
fake light (System's light draws Light's pixels, 11.15's `System_and_Light_draw_the_same_pixels`);
the settings screen by its key on General (`l31d-settings-light`) against a single picture of its
own. The settings screen's General shows which theme is chosen, so L31 pictures it twice: the pair
`l31d-settings-system-dark` and `l31d-settings-system-light` (the System radio checked;
`ShotInBothThemesAsync(app, "l31d-settings-system")`), then, in the "ops" method's last step (the
pairs before it need System), with Light chosen and saved and the screen opened again by its key, the
single `l31d-settings-light` (the Light radio checked, as in L34's launch), L34's twin. No two steps
save one name: `Shot` refuses a name it has already saved in the run (17.2).

By hand at acceptance, one line of the report's checklist: "With System chosen in the settings
screen, switch Windows' app mode (Settings > Personalization > Colors > Choose your app mode) while
the app runs. The window, its title bar and the open views follow within a second. Switch back." The
macOS checklist gets the same step with System Settings > Appearance.

### 11.9 Linux without a portal, and macOS

Linux without a portal: System means Light, Avalonia's answer (0.6); the app has no D-Bus code of
its own, and minimal desktops and CI's containers have no portal. A portal answering after the first
frame is an ordinary `ColorValuesChanged`: `Changed`, the window turns dark, 11.4's log line. CI runs
headless with the fake, so it proves the rules, not the portal. macOS's native observer is not
checked (0.6): 11.8's by-hand macOS step.

### 11.10 Contrast: the light status and danger colours

**In the light theme every text token reaches 4.5:1 on every background its text lies on**, the
hovered (`VcHoverBackgroundBrush`) and selected (`VcSelectionBrush`) rows included, which r06 did
not measure. Marks that are not text (the banner's line, the lanes, the search mark) need 3:1;
disabled text is exempt (WCAG). The dark tokens do not change (D198). Ratios: WCAG 2 relative luminance
(`p6-lab/sec-11/contrast11.py`, `contrast11b.py`).

Where the changed tokens' text lies:

- `VcWarningBrush`: the "M" letter of every file list on panel, hover and selection
  (`Views/Shell/RightPanelView.axaml:58-60`, `Views/Shell/WorkingChangesView.axaml:49`, `:69-72`;
  `CompareView`, `FileHistoryView`, `DiffView`); the status bar's conflicts state on the chrome
  (`StatusBarView.axaml:100`); the banner's icon and line on `VcBannerBackgroundBrush`
  (`ConflictBanner.axaml:20`, `:31`); the marker lines' text on `VcConflictMarkerBrush`
  (`Controls/Conflicts/ConflictResultEditor.cs:480`); "Not committed yet" in the blame gutter
  (`Controls/Blame/BlameGutter.cs:259`); the activity log's, the message dialog's and the reflog's
  warnings.
- `VcSuccessBrush`: the "A" letter; the activity log's success glyph.
- `VcDangerBrush`: error texts (`TextBlock.error`, `Controls.axaml:331-333`), the "D" letter, the
  status bar's failures on the chrome, the menus' danger items, which keep the colour under the
  pointer (`Theme/Menus.axaml:39`), and the light danger button's background.

Hue and saturation kept, the lightness lowered to the first value whose smallest ratio reaches 4.55
(every rounded figure 4.57 or more):

| Light token | Colour | window `#FFFFFF` | panel `#F5F6F8` | chrome `#EBEDF1` | hover `#DEE2E9` | selection `#DDE1FA` | banner `#FFF4DE` | marker `#FCEFD2` |
|---|---|---|---|---|---|---|---|---|
| `VcWarningBrush`, today | `#B7791F` (H 35.5°, S 0.71, L 0.420) | 3.64 | 3.37 | 3.11 | **2.80** | **2.81** | 3.34 | 3.19 |
| phase 6 | **`#895A17`** (H 35.3°, S 0.71, L 0.314) | 5.93 | 5.49 | 5.06 | 4.57 | 4.59 | 5.44 | 5.20 |
| `VcSuccessBrush`, today | `#1E8E5A` (H 152.1°, S 0.65, L 0.337) | 4.14 | 3.83 | 3.53 | **3.19** | **3.20** | 3.79 | 3.63 |
| phase 6 | **`#187248`** (H 152.0°, S 0.65, L 0.271) | 5.93 | 5.49 | 5.06 | 4.57 | 4.58 | 5.44 | 5.20 |
| `VcDangerBrush`, today | `#C93A46` (H 355.0°, S 0.57, L 0.508) | 5.03 | 4.65 | **4.29** | **3.87** | **3.88** | 4.61 | 4.41 |
| phase 6 | **`#B7323D`** (H 355.0°, S 0.57, L 0.457) | 5.94 | 5.49 | 5.07 | 4.57 | 4.59 | 5.44 | 5.21 |

The light danger button's white text: 5.03:1 → 5.94:1. `VcLane3Brush` (`#C93A46`) is its own token
and stays. Passing already and kept: `VcTextSecondaryBrush` 4.54 on hover and 4.56 on selection,
`VcAccentBrush` 4.66 and 4.68, `VcSearchMatchBrush` (`#B98900`, a 3-px mark) 3.16 on the window.

**Found in the dark theme and left to phase 8's accessibility pass, with the owner's consent (D198;
16.2):** `VcDangerBrush` `#E5606B` 3.64:1 on a hovered row (`#2F3542`) and 3.76:1 on a selected one
(`#2A3150`), as a danger menu item under the pointer (`Theme/Menus.axaml:39`, `:62`) and a "D" letter
on a hovered or selected file row show it; `VcAccentBrush` `#7C8CFF` 4.13:1 on a hovered row and
4.26:1 on a selected one (the "R" letter, `TextBlock.status.renamed`, on the selected file row,
`Views/Shell/RightPanelView.axaml:64-69`; not in D198's list, found by the reviews, and left with the
others); the danger button's white label 3.38:1 (11.11). The danger colour and the label are one
question for phase 8: raised by this subsection's method, `VcDangerBrush` (about `#EA7D86`: 4.55 on
hover, 4.70 on selection) is also the button's background and would lower the white label to 2.70:1,
so it needs the dark label `#10121A` (6.92:1 on it); a raised accent (about `#8796FF`: 4.59 and 4.74)
recolours HEAD's branch name, the "R" letters and the primary buttons in nearly every dark picture.
Every other dark text token reaches 4.5:1 on every dark background.

### 11.11 `VcDangerTextBrush`

`VcDangerButton` (`Theme/Controls.axaml:269-273`) sets `Foreground="#FFFFFF"` (`:272`), the only
fixed colour outside the tokens (D29). `Theme/Tokens.axaml` gains `VcDangerTextBrush` after
`VcDangerBrush` in each dictionary, text on a danger background: **dark `#FFFFFF`, light `#FFFFFF`**
(D198). `:272` becomes `<Setter Property="Foreground" Value="{DynamicResource
VcDangerTextBrush}" />`, the comment at `:269` "the danger background, its text in
`VcDangerTextBrush`". Users: `ConfirmationView.axaml:64`, `ChoiceDialogView.axaml:65`,
`FinishingDialogView.axaml:54`, `ResetDialogView.axaml:90`; no picture changes (light: 5.94:1 on
`#B7323D`). The dark label stays at 3.38:1 on `#E5606B`, left to phase 8 with the dark theme's other
gaps (D198, 11.10): dark `#10121A` (the dark primary button's `VcAccentTextBrush`) would give 5.53:1
on `#E5606B` and change only the label in 35 earlier dark pictures (29 scripted, 6 real-window; 16.2
names them for phase 8).

### 11.12 Tooltips through Fluent's keys

Fluent's template stays (D105's overlay layer, D182's no-input style, `Controls.axaml:343-350`);
only its three brushes change, set in each dictionary of `Theme/Tokens.axaml` after the
`TextControl…` keys, under `<!-- Tooltips: these keys replace the Fluent theme's own colours for
them. -->`, written out as those keys are (architecture.md's token table lists them as copies):

| Fluent key | Dark today | Dark, phase 6 | Light today | Light, phase 6 |
|---|---|---|---|---|
| `ToolTipBackground` | `#2B2B2B` | `#1A1D24` (= `VcPanelBackgroundBrush`) | `#F2F2F2` | `#F5F6F8` (= `VcPanelBackgroundBrush`) |
| `ToolTipForeground` | `#FFFFFF` | `#E4E7EC` (= `VcTextPrimaryBrush`) | `#000000` | `#1B1F27` (= `VcTextPrimaryBrush`) |
| `ToolTipBorderBrush` | `#000000` at 0.36, about `#1C1C1C` | `#323845` (= `VcBorderBrush`) | `#000000` at 0.14, about `#D0D0D0` | `#D2D6DE` (= `VcBorderBrush`) |

Text contrast: dark 13.61:1 (14.16 today), light 15.27:1 (18.76 today). Size, padding (8,5,8,7),
border (1), font size (12) and corner radius stay: **only colours change, inside each tooltip's
rectangle**, in the 56 earlier pictures with a tooltip (phase 3 21 scripted and 3 real-window, phase
4 12 and 3, phase 5 15 and 2, phases 0 to 2 none; 16.1 names them). No test asserts Fluent's colours
(`tests/` has no `ToolTipBackground` or `2B2B2B`). Scroll bars stay Fluent's.

### 11.13 The light lane colours (D198)

White label text (`VcLabelTextBrush`, light `#FFFFFF`; 11-px SemiBold, not large text for WCAG)
stays below 4.5:1 on light lanes 1, 2, 4 and 7. They change by 11.10's method, until white text
reaches 4.55, amending D48's light palette (D198):

| Lane | Light today | White text today | Phase 6 | White text | As a line on the window (3:1 needed) |
|---|---|---|---|---|---|
| 1 | `#1E8E5A` | 4.14 | **`#1C8655`** | 4.57 | 4.57 |
| 2 | `#B7791F` | 3.64 | **`#A16A1B`** | 4.57 | 4.57 |
| 4 | `#1F86B0` | 4.12 | **`#1D7EA6`** | 4.58 | 4.58 |
| 7 | `#5F8A1E` | 4.09 | **`#59811C`** | 4.58 | 4.58 |

Darker by 0.02 to 0.05 of lightness: the same green, amber, blue and olive. Lanes 0, 3, 5 and 6
reach 6.06, 5.03, 5.72 and 4.86 and stay. Dark label text (`#1B1F27`) on today's lanes would reach
only 3.99, 4.54, 4.01 and 4.04. The dark lanes reach 5.53 to 8.37 with `#10121A` text and stay.
Lanes 1 and 2 equal today's success and warning colours and differ after 11.10: lanes are D48's
palette, their own tokens.

### 11.14 The sweep: every main screen in both themes

The methods, the scenario each uses, their numbers and the picture names 17.4 gives are 17.4's (S15
in part S, L26 to L31 in part L, in `VisualCommit.VisualTests.Phase6Look` (17.3), L31 two methods; L34 is the real
window's alone, 11.8); this subsection decides what each method pictures and what stays unpictured,
and names the pictures 17.4 leaves unnamed. A sweep method starts with `"theme": "System"` and the
fake dark (17.2's `ShellDriver.Start(…, bool systemDark = true)`) and takes each screen through 17.2's
`ShotInBothThemesAsync(app, name, keepPointer)`: `{name}-dark`; the fake flipped light
(`SetSystemThemeAsync(false)`), the variant and one sampled light token asserted; `{name}-light`;
flipped back, dark asserted, the screen still up. So each pair also proves that D194 follows a
change on that screen both ways, with its menu, dialog, drag or tooltip open. 1100×700; the pointer
at the neutral point except in tooltip and menu steps (`keepPointer`). For L27's flip mid-drag, and
any drag while the system changes, `DragLayer`'s constructor gains
`ActualThemeVariantChanged += (_, _) => _visual.InvalidateVisual();` (0.6's gap).

| Method | Pictures (each a `-dark` and `-light` pair unless said) |
|---|---|
| S15 (two methods, the "graph" scenario) | Five single pictures. A start with `"theme": "System"` in `settings.json` and the fake dark: `s15a-system-start-dark`, a clean twin of a System start in the real window (11.8). The fake flipped: `s15b-system-light`, the whole window light without a restart, the moon icon, tooltip `Switch to the dark theme (stops following the system)`, `settings.json` still `"System"`, the "follows it" log line. A click on Theme: `s15c-theme-click-dark`, `"Dark"` saved, tooltip `Switch to the light theme`; two flips then change nothing (captures byte-identical to `s15c`; the "keeps" lines). The palette's `Follow the system's theme`: light, `"System"` saved. A restart (the second method) with System and the fake light: `s15d-restarted-first-frame-light` (before git is found, as phase 0's `06c`), `s15e-restarted-light` |
| L26 Welcome page (the empty tab: a recent list and one workspace in the data folder) | `l26a-welcome` (its Workspaces section and the recent list; the recent folders cut with "…" as in phase 1's `10a`), `l26b-clone-form` |
| L27 Graph, menus, tooltips and dialogs (the "ops" scenario) | `l27a-drag` (a branch over another: the pill and the outline, flipped mid-drag, Esc ends it); `l27b-three-selected`; `l27c-danger-item` (a branch menu, "Delete feature/alpha…" under the pointer); `l27d-disabled-tooltip` (a disabled toolbar button's); `l27e-menu-item-tooltip` (a disabled menu item's); `l27f-name-dialog` (Create branch…); `l27g-push-dialog` (`feature/ff`, no upstream); `l27h-remotes-dialog` (Manage remotes…) |
| L28 Changes, pages and the graph's views (the "history" scenario) | `l28a-diff` (`src/Reader.cs`'s working change: its stage-panel "M" row and inline diff with C# syntax colours, Dark+ / Light+); `l28b-restore-bar` (after "Discard changes", then Restore); `l28c-blame` (its blame, "Not committed yet"); `l28d-compare` (two commits, Ctrl+click); `l28e-all-files`; `l28f-hidden` (`feature/compare` hidden and another branch soloed: the eye-off icon, the disabled name) |
| L29 Reflog and backups (the "reflog" scenario) | `l29a-reflog` (HEAD's); `l29b-backups` |
| L30 The dashboard (the workspace "Web", 17.1) | `l30a-dashboard` (every row's state), `l30b-row-menu`, `l30c-scan-checklist` ("Add repository…" on a folder holding repositories; its folder line shows the copy's temporary folder, so the pair varies by nature and its Notes say so, 17.4) |
| L31 (two methods) Overlays and phase 6's screens | On the "ops" scenario: `l31a-activity-log` (open); `l31b-delete-question` ("Delete feature/alpha?", the danger label `VcDangerTextBrush` in both themes, 11.11); `l31c-palette` (open, no text); `l31d-settings-system` (General, the Theme radios, System checked); `l31e-settings-git` (Git on "All repositories (global)", as it opens, D200; the merge tool's Pick dialog stacked on it once 8.10's lookup has ended, D202); `l31f-enabled-tooltip` (Fetch's, with its key, 6.8); last, Light chosen and saved and the screen opened again, the single `l31d-settings-light` (the Light radio checked; L34's twin, 11.8). On the "auth" scenario (phase 3's server with sign-in, on 47394 through `StartForVisualChecksAsync(port: AuthRemoteServer.Phase6LookVisualChecksPort)`, not phase 3's 47391, 17.2; its texts that show the port through `Phase6Expectations.WithPort`): `l31g-credentials` (the sign-in prompt); `l31h-operation-held` (the status bar during a fetch the server holds at its percentage: progress, Cancel, `1 waiting`; then Cancel) |

About 64 pictures: 29 pairs, the single `l31d-settings-light` and S15's five; the `-light` pictures
of `l31a` to `l31c` and the single `l31d-settings-light` are L34's twins (11.8).

**Every main screen and region**, with today's gate pictures (P*n* the phase; dark; light).
Covered by light gate pictures and not pictured again: the graph's search bar and marks (P5 `f10a`
to `f10f`; P5 `f10h`), commit details and the file list, flat and tree (P1 `03a`, `03b`; P1 `04a` to
`04c`), the conflict banner (P3 `09a`; P3 `27c`), the interactive rebase list (P4 `b6b`; P4
`d18a`), the resolver (P4 `c10e`; P4 `d18c`, `d18d`), file history (P5 `b5b`; P5 `b5k`), and the
reset, error and message dialogs (P3 `10b`, `14b1`, P4 `b7d`; P3 `27b`, `27d`, P4 `d18b`). Covered,
and shown following live in S15: the repo tabs, toolbar and status bar when idle (P0 `01`, `02`; P0
`03`, `04`, `05b`, `06b` to `06d`, P1 `02`), the left panel's sections and filter (P1 `05a`, `06a`;
P1 `02`) and the graph's rows, lanes, labels and columns (P1 `01`; P1 `02`). The rest:

| Region or screen | Dark today | Light today | Phase 6, both themes |
|---|---|---|---|
| Stage panel and commit box; diff view, inline and side by side | P2 `01b`, `03a`, `03b` | P2 `02a`, `02b`, `04a` to `04f` | covered; also `l28a` (syntax colours) |
| Context menu | P3 `01a` | P3 `27a` | covered; also `l27c` (a danger item under the pointer) |
| Welcome page | P0 `01` | P0 `03`, `04` | `l26a`, with its Workspaces section |
| Welcome page's error | P1 `14a` | P1 `11a` | covered (13.1 changes `11a`; L32 pictures the long folder in both themes) |
| Welcome page's recent list, clone form | P1 `10a`, `13a` | none | `l26a`, `l26b` |
| Clone in progress | P1 `13b` | none | not pictured: the tokens `l31h` shows |
| Status bar during an operation (progress, Cancel, waiting) | P3 `17b`, `17d`, `18b` | none | `l31h` |
| Left panel's hidden and soloed branches (O8) | P5 `f14d`, `f14h` | none | `l28f` |
| Graph's reload bar (`GraphReloading`) | none (shown for a moment, asserted hidden once loaded) | none | not pictured: a 2-px bar in `VcAccentBrush` |
| Branch drag (`DragLayer`) | P3 `07a` | none | `l27a` |
| Commit details, "All files" | P5 `b7a` | none | `l28e` |
| Compare (two commits; branches) | P5 `f12a`, `f13b` | none | `l28d` (two commits) |
| Multi-selection (3 or more) | P3 `11e`, `11f` | none | `l27b` |
| Restore bar after a discard | P2 `17b`; P5 `u16h` | none | `l28b` |
| Blame page | P5 `b6a` | none | `l28c` |
| Reflog and backups pages | P5 `b8b`, `b9a` | none | `l29a`, `l29b` |
| Activity log | P5 `a2a` | none | `l31a` (and L34) |
| C12's refusal and the automatic stash in the graph | P5 `p24a` to `p24f`, `p25b` | none | covered: the refusal is phase 3's error dialog, whose `ErrorExtraButton` ("Stash changes and check out") has the theme `VcActionButton` and the class `errorAction` of "Copy output" in P3 `27d`; the automatic stash's ring and dashed line are drawn as every stash's (the lane's `RingPens` and `DashedPen` on the row background), light in S15's `s15b` and `s15e` ("graph", row 0 `stash@{0}`) and, for the working-changes row's ring and dashed line, in P2 `02a`, `02b` |
| Dialogs: confirmation (danger button), name, push, remotes | P3 `13i`, `13b`, `29a`, `22a` | none (App tests only) | `l31b` (and L34), `l27f`, `l27g`, `l27h` |
| Dialogs: pick | P3 `28b` | none | `l31e` (stacked on the settings screen) |
| Dialogs: choice, finishing | P3 `29c`, `31a` | none | not pictured: the confirmation's tokens; `DialogTests.Pictures_of_the_dialogs_in_both_themes` (`:819`) draws both in both themes and asserts their layout (no clipped text, the buttons inside the card) |
| Prompt dialogs: credentials | P3 `21a` | none | `l31g` |
| Prompt dialogs: passphrase, host key, prompt | P3 `21b1`, `21b2`, `21b3` | none | not pictured: the credentials dialog's tokens; `PromptDialogTests` (`:287`) draws them in both themes |
| Tooltips (Fluent's `ToolTip`) | the 48 scripted pictures 16.1 names | none | `l27d`, `l27e` (disabled), `l31f` (enabled, with its key) |
| Dashboard tab (section 2) | new (part W) | new (W3, 1920×1080) | `l30a`, `l30b` |
| Folder scan's checklist (1.9) | new (part W) | – | `l30c` |
| Command palette (section 5) | new (part K) | – | `l31c` (and L34) |
| Settings screen (section 7) | new (part S) | – | `l31d`, `l31e` (and L34) |
| Tab strip that overflows (13.3) | new (L33) | – | not pictured: the tabs' tokens, light in S15 |

Each region not pictured uses only tokens another light picture shows, and its dialogs are drawn in
both themes by App tests; picturing them would add three app starts (a held clone, a close during an
operation, an ssh remote) for no new colour. The phase 5 handoff's open point on the sweep (its new
screens pictured only dark) is answered item by item: the activity log `l31a`, the pages `l28c`,
`l29a`, `l29b` (file history P5 `b5k`), the search bar P5 `f10h`, the comparison `l28d`, C12's
message and the automatic stash the row above.

### 11.15 Tests

App tests, WP-T's (18) unless a bullet names another owner, in `VisualCommit.App.Tests` or in phase
6's view test project if 17.3's measurement moves view tests out:

- `SystemThemeTests` (new): `PlatformSystemTheme(null)` and `IsDarkIn(null)` give not dark; `Update`
  with `ThemeVariant = Dark` raises `Changed` once, the same values again nothing; an accent-only
  change (another `AccentColor1`) nothing; `Update` from a pool thread raises on the UI thread
  (`[AvaloniaFact]`).
- `ThemeServiceTests` (new, `[AvaloniaFact]`; each test puts `Application.Current.RequestedThemeVariant`
  back to Dark, the headless application being shared): `Apply(Dark | Light)` sets the variant and
  `IsDark`; `Apply(System)` follows the fake both ways; while System a flip flips the variant, raises
  `Changed` once and logs "follows it"; while Dark or Light a flip changes neither the variant nor
  `IsDark`, raises nothing and logs "keeps"; `Apply` of the same theme raises nothing; `Apply(System)`
  while the system's mode equals what shows raises `Changed` and leaves the variant; `(AppTheme)7`
  resolves to Dark; after `Dispose` a flip changes nothing.
- `MainWindowViewModelTests` (changed): `FakeThemes` (`:143-154`) gains `Setting`, `IsDark`,
  `Changed`, loses `Current`; System with dark showing: tooltip `Switch to the light theme (stops
  following the system)`, a click saves `Light`; with light showing: `Switch to the dark theme (stops
  following the system)`, `Dark`; the service's `Changed` raises `Theme`, `IsDarkTheme`,
  `ThemeSwitchToolTip`; `UseTheme(System)` saves `System` and logs `Theme changed to System.`, of the
  chosen theme nothing; the toggle test keeps its assertions (`themes.Setting` for `themes.Current`).
- `JsonSettingsStoreTests` (changed; WP-SET's class, 7.11 and 18): `"theme": "System"` round-trips;
  the default stays Dark; `{ "theme": "Purple" }` is still set aside (`:76`).
- `SystemThemeStartTests` (new; 18 puts the starts here, not in `ShellTests`), through 17.2's
  `ShellDriver.Start(…, bool systemDark = true)` and `SetSystemThemeAsync(bool)`:
  `A_session_with_the_system_theme_starts_in_the_systems_mode` (`systemDark: false`:
  `RequestedThemeVariant == Light` right after `Start`, before the first wait; the window's
  `ActualThemeVariant` Light; `IsDarkTheme` false);
  `The_window_follows_the_system_while_System_is_chosen` (the variant, the switch's `light` class and
  tooltip change; `settings.json` keeps `"System"`); `Dark_and_Light_ignore_the_system`;
  `System_and_Light_draw_the_same_pixels` (two starts on one scenario, `"Light"` and `"System"` with
  the fake light: byte-identical captures, so earlier Light pictures stay valid for System, and L34's
  explicit Light matches the sweep's System pictures, 11.8).
- `AppSessionTests` (new; the lead's class, 18): 11.5's start-up log line, `Theme: System (the
  system's theme is light).` with the fake light.
- Live switches r06 found untested (switch `RequestedThemeVariant` while the control shows content,
  assert sampled colours of both themes): `BlameViewTests` (the text, the gutter's "Not committed
  yet"), `ConflictPanesTests` and `ConflictResultEditorTests` (a side's tint, the marker text),
  `DragLayerTests` (new: a switch mid-drag redraws the pill and the outline, 11.14's fix),
  `CommitGraphControlTests` (the search mark).
- `ThemeTokensTests` (new): both dictionaries of `Tokens.axaml` hold the same keys; light warning,
  success, danger, secondary, accent and primary text at 4.5:1 on window, panel, chrome, hover and
  selection, warning also on the banner and the marker; `VcAccentTextBrush` on `VcAccentBrush` in
  both themes, `VcDangerTextBrush` on `VcDangerBrush` in the light one; `VcLabelTextBrush` on every
  lane in both (11.13); the dark pairs at 4.5:1 today still are. 11.10's dark gaps (danger on a
  hovered and a selected row, the accent on both, white on the dark danger button) are named in the
  test as known exceptions left to phase 8 (D198), not asserted at 4.5:1. Tokens through `Application.Current.TryGetResource(key, variant, out _)`, ratios by
  `Contrast.Ratio(Color, Color)` (new, `tests/VisualCommit.Testing/Contrast.cs`).
- `ToolTipStyleTests` (new): a tooltip opened in each theme has 11.12's `Background`, `Foreground`
  and `BorderBrush`; switching the theme while it is open recolours it.

Harness: 17.2's `ShellDriver.Start(…, bool systemDark = true)`, `SetSystemThemeAsync(bool)`
(`ShellDriver.Phase6.Theme.cs`) and `ShotInBothThemesAsync(app, name, keepPointer)`
(`Phase6Checks.Support.cs`). Scripted, besides 11.14's pictures and assertions: S15's saved values
(`"System"` kept through the flip, then `"Dark"` from the click, then `"System"` from the palette),
tooltips, icons, both log lines, and the restart's first frame light before git is found; each
tooltip's background, text and border (11.12). Real window: 11.8's two launches, S15 and L34.

## 12. Open in editor and Open file

Delivers: the "external tools" of the settings screen; phase 3's "Open in editor" beside "Open
file"; the extensions "Open file" asks about (risk 20).

### 12.1 Open in editor in the file menus

`RepositoryMenus.FileItems` (`ViewModels/Menus/RepositoryMenus.cs:894-899`), which every file menu
uses (`RepositoryMenus.cs:456, 496, 505, 544, 553, 556`, `RepositoryMenus.Filters.cs:87`), takes the
menu context and adds `Open in editor` after `Open file` **only while an editor is chosen** (`Open
file`, `Open in editor`, `Show in folder`, `Copy path`). With none the menus are as today, so phases 3
to 5's asserted file menus (about 45) and their pictures' menus stay as they are. This is an
exception to 15.1's rule that what can be explained is disabled with its reason, named there: Open in
merge tool is disabled with its reason (`Set merge.tool in git's configuration to use a merge tool`,
phase 4 design 11.9), but it shows only on a conflicted file, while a disabled Open in editor would
add a row to every earlier file menu (D226). While no editor is
chosen the palette lists `Choose an editor…` (5.4), which opens the settings screen on Tools; with
the Tools note (7.5) it makes the item findable. The palette lists no Open in editor of its own
(5.5: what a menu leaves out the palette lacks too).

- `RepositoryMenuContext` (`ViewModels/Menus/RepositoryMenuContext.cs:106`) gains `EditorMenuState?
  Editor` (`record EditorMenuState(string Name)`), filled by `RepositoryViewModel.MenuContext`
  (`RepositoryViewModel.Menus.cs:52`) from `Services.Settings.Current.Editor`: null for none, else the
  editor's name. Nothing touches the disk for it: `MenuContext` is built on the UI thread for every
  menu, the palette and some page loads, and a check of a program on a share that does not answer
  blocks for minutes (0.9). A program that is gone is said when the item is clicked (12.7). The
  builders stay pure.
- Label `Open in editor` (`OperationTexts.MenuTexts.OpenInEditor`), fixed, so expectations do not
  depend on the machine. Disabled only with `The file is not in the working tree` (the existing
  `NotInWorkingTree`).
- It runs `IRepositoryActions.OpenInEditorAsync(string path)` (new, beside `OpenFileAsync`,
  `IRepositoryActions.cs:206`) on the working tree's file (`FullPathOf`, `RepositoryOperations.Files.cs:241`,
  always absolute), **never asking first**: an editor shows a file and runs nothing. No operation (no
  queue, no undo entry, D168), as Open file; through `LaunchAsync` (`:304`); log line `Open in editor:
  {command}`.
- Phase 6 opens a file at its top: no file menu knows a line (`FileMenuTarget`, `DiffMenuTarget`); the
  catalogue keeps each editor's line form for the blame page and the diff view later.

### 12.2 The editor catalogue

`KnownEditors` (App, `Services/KnownEditors.cs`), static data per platform: an id, a name, candidate
programs (absolute paths built from the folders below, never from the working directory, nor from
`PATH` on Windows), the arguments without and with a line (`{p}` the file's absolute path, `{n}` the
line). Of the Windows entries VS Code, Cursor, Notepad++ (under x86), IntelliJ IDEA 2020.3.2, Visual
Studio 2022 Community and Notepad exist here (0.9). `--goto` is seen only in `code --help` and
`cursor --help` (through their `.cmd`); `Code.exe` and `Cursor.exe` started directly are checked by
hand at the gate; the other line forms are the vendors' documentation, not run.

**Windows.** Only `.exe` programs, never a `.cmd` or `.bat` (0.9).

| Id | Name | Candidates, first that exists | Arguments | With a line |
|---|---|---|---|---|
| `vscode` | Visual Studio Code | `%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe`; `%ProgramFiles%\Microsoft VS Code\Code.exe` | `{p}` | `--goto {p}:{n}` |
| `vscode-insiders` | VS Code Insiders | `%LOCALAPPDATA%\Programs\Microsoft VS Code Insiders\Code - Insiders.exe`; the same under `%ProgramFiles%` | `{p}` | `--goto {p}:{n}` |
| `cursor` | Cursor | `%LOCALAPPDATA%\Programs\cursor\Cursor.exe` | `{p}` | `--goto {p}:{n}` |
| `vscodium` | VSCodium | `%LOCALAPPDATA%\Programs\VSCodium\VSCodium.exe`; `%ProgramFiles%\VSCodium\VSCodium.exe` | `{p}` | `--goto {p}:{n}` |
| `sublime` | Sublime Text | `%ProgramFiles%\Sublime Text\subl.exe`; `%ProgramFiles%\Sublime Text 3\subl.exe` | `{p}` | `{p}:{n}` |
| `notepadpp` | Notepad++ | `%ProgramFiles%\Notepad++\notepad++.exe`; `%ProgramFiles(x86)%\Notepad++\notepad++.exe` | `{p}` | `-n{n} {p}` (`-n12` one argument) |
| `idea`, `rider`, `pycharm`, `webstorm`, `clion`, `goland`, `phpstorm`, `rubymine`, `rustrover` | IntelliJ IDEA, Rider, PyCharm, WebStorm, CLion, GoLand, PhpStorm, RubyMine, RustRover | each folder of `%ProgramFiles%\JetBrains\` whose name starts with the product's (`IntelliJ IDEA`, `JetBrains Rider`, `PyCharm`, …), newest version first, then `bin\<idea64\|rider64\|pycharm64\|webstorm64\|clion64\|goland64\|phpstorm64\|rubymine64\|rustrover64>.exe` by its exact name; Toolbox installs are left to Other… | `{p}` | `--line {n} {p}` |
| `visualstudio` | Visual Studio | `%ProgramFiles%\Microsoft Visual Studio\<2022, then 18>\<Enterprise, Professional, Community>\Common7\IDE\devenv.exe` | `/Edit {p}` | none |
| `gvim` | gVim | each `%ProgramFiles%\Vim\vim*\gvim.exe`, newest first | `{p}` | `+{n} {p}` |
| `notepad` | Notepad | `%SystemRoot%\System32\notepad.exe` | `{p}` | none |

**macOS and Linux.** macOS searches `/Applications` and `~/Applications`; the programs inside the
bundles are scripts or executables that take their arguments as given (`"$@"`), safe without a shell.
Linux tries the fixed paths, then `FileLauncher.FindOnPath` for the command names (fully qualified
`PATH` folders only, D130); scripts are fine there; Flatpak exports and AppImages are left to Other….
Neither was checked (no such machine). An id that Windows has takes the same arguments as there.

| Id | macOS program | Linux program | Arguments; with a line |
|---|---|---|---|
| `vscode` | `Visual Studio Code.app/Contents/Resources/app/bin/code` | `/usr/bin/code`, `/usr/share/code/bin/code`, `/snap/bin/code`, `code` on `PATH` | as Windows |
| `vscode-insiders` | `Visual Studio Code - Insiders.app/Contents/Resources/app/bin/code-insiders` | `/usr/bin/code-insiders`, on `PATH` | as Windows |
| `cursor` | `Cursor.app/Contents/Resources/app/bin/cursor` | — | as Windows |
| `vscodium` | `VSCodium.app/Contents/Resources/app/bin/codium` | `/usr/bin/codium`, on `PATH` | as Windows |
| `sublime` | `Sublime Text.app/Contents/SharedSupport/bin/subl` | `/usr/bin/subl`, `/opt/sublime_text/sublime_text` | as Windows |
| JetBrains ids | `<IntelliJ IDEA.app, IntelliJ IDEA CE.app, Rider.app, PyCharm.app, …>/Contents/MacOS/<idea, rider, pycharm, …>` | `~/.local/share/JetBrains/Toolbox/scripts/<idea, rider, …>`, `/snap/bin/<product>` | as Windows |
| `gvim` | — | `/usr/bin/gvim` | as Windows |
| `bbedit` | `/usr/local/bin/bbedit` (when its tools are installed) | — | `{p}`; `+{n} {p}` |
| `xcode` | `/usr/bin/xed`, only when `/Applications/Xcode.app` exists (the stub without Xcode is not offered) | — | `{p}`; `--line {n} {p}` |
| `textedit` | `/usr/bin/open` | — | `-a TextEdit {p}`; none |
| `gedit` | — | `/usr/bin/gedit` | `{p}`; `+{n} {p}` |
| `gnome-text-editor` | — | `/usr/bin/gnome-text-editor` | `{p}`; none |
| `kate`, `kwrite` | — | `/usr/bin/kate`, `/usr/bin/kwrite` | `{p}`; `--line {n} {p}` |

WP-SET added unverified guesses as this section allows: `IntelliJ IDEA CE.app` and `PyCharm CE.app` on
macOS, and the script names under `/snap/bin/` on Linux, where the snaps' own commands are
`intellij-idea-community`, `intellij-idea-ultimate`, `pycharm-community` and `pycharm-professional`
(its review), so `/snap/bin/idea` and `/snap/bin/pycharm` find nothing; harmless, and the handoff
lists them for checking on those systems.

### 12.3 Finding the editors

`IEditorCatalog` (App, `Services/EditorCatalog.cs`): `Task<IReadOnlyList<FoundEditor>>
FindAsync(CancellationToken)`, `KnownEditor? Known(string id)`, with
`FoundEditor(string Id, string Name, string Program)` in Core beside `ToolCatalogues` (12.9). The real
`EditorCatalog` takes an `EditorSearchContext` (the folders from `Environment.GetFolderPath` and the
environment, a file-exists and a list-folders function), as `GitLocator.FindExecutable` takes
`GitSearchContext`, so tests give it a fake file system; it backs the machine's `ToolCatalogues`.

- About 40 to 60 file checks and three folder listings (`%ProgramFiles%\JetBrains`, `%ProgramFiles%\Vim`,
  `%ProgramFiles%\Microsoft Visual Studio`; on macOS the two Applications folders): milliseconds. No
  registry, and no `PATH` on Windows (0.9: it holds `.cmd` shims there, and a name can be another
  program).
- It runs off the UI thread when the settings screen opens; `EditorStatus` shows `Looking for
  editors…` meanwhile, and Choose… waits for it. A known editor whose stored program is gone (checked
  inside `FindAsync`, off the UI thread) and is found under its id elsewhere (a JetBrains update
  renames its folder) is updated in the setting, with the log line `{Name} moved to {program}; the
  editor setting follows it.`
- **git's editor is a mark only.** `core.editor` (from 8.1's read; `GIT_EDITOR`, `VISUAL` and `EDITOR`
  are not read) is a shell command line made to block until the file closes, often `vi` (0.9), and is
  never run. When its first token, unquoted, is a found editor's program (ignoring case on Windows),
  that editor shows ` (git's editor)` in the Pick dialog and in `EditorValue`.

### 12.4 Starting an editor

`EditorCommands.Build(EditorSetting editor, KnownEditor? known, string fullPath, int? line) →
LaunchCommand` (App, `Services/EditorCommands.cs`), pure, tested like `FileLauncher.RevealCommands`:

1. **The program by absolute path**, from the setting (found by the catalogue or chosen for Other).
   On Windows only `.exe` and `.com` start: `CreateProcess` hands a `.cmd` or `.bat` to `cmd.exe`,
   which parses the arguments (0.9).
2. **Arguments as a list** in `ProcessStartInfo.ArgumentList`, `UseShellExecute = false`
   (`LaunchCommand.ToStartInfo`, `Services/FileLauncher.cs:368`): no shell anywhere (D130's reasoning).
3. **The file as its absolute path**, so it never starts with `-`; a known editor's arguments come
   from the catalogue, never from the setting, so a later version can correct a template.
4. **The environment** is the app's without `ELECTRON_RUN_AS_NODE` (with it `Code.exe` and
   `Cursor.exe` would run the file as a Node script).
5. **The working directory** is the program's folder on Windows, the user's home elsewhere; never the
   repository, nor the app's own (the repository when started from a terminal there).
6. **Not waited for**, no output read; the process object disposed at once.

`IFileLauncher` (`Services/IFileLauncher.cs:8-15`) gains `Task OpenInEditorAsync(LaunchCommand command,
CancellationToken cancellationToken = default)`; `FileLauncher` starts it off the UI thread
(`Task.Run`, D59), as `RevealAsync`, first checking there that the program exists (a
`FileNotFoundException` when not), and throws when it cannot start (12.7). `LaunchCommand` gains
`WorkingDirectory` and `RemovedVariables` (applied in `ToStartInfo`). `FakeFileLauncher` records
`FileLaunchKind.Edit` with program, arguments and working directory, so checks assert the exact
command and nothing starts.

### 12.5 Other…

- **Program**: `EditorProgramBox` and `EditorBrowseButton` `Browse…`, which opens the platform's file
  dialog `Choose the editor's program` through `IFolderPicker.PickFileAsync(string title,
  CancellationToken cancellationToken = default)`, new on `Services/FolderPicker.cs:11`'s interface as
  a default member, `=> Task.FromResult<string?>(null)`: the interface's default answers null, a
  cancelled dialog, so the other implementers (`MainWindowViewModel`'s `NoFolderPicker` and the four
  test fakes `NoFolders`) need no change, as with 1.2's `OpenedAtTopAsync`. `StorageFolderPicker`
  implements it with `OpenFilePickerAsync`; `FakeFolderPicker` answers from a queue of its own,
  `AnswerFile`. Both are WP-SET's.
- **Arguments**: `EditorArgumentsBox`, `{path}` at first. Split at whitespace outside double quotes; a
  quoted part is one argument, its quotes removed; no other escaping. `{path}` and `{line}` are
  replaced inside each argument (`--goto {path}:{line}` gives `--goto` and
  `C:\src\web-app\Program.cs:1`); `{line}` is 1 in phase 6 (12.1). Without `{path}` the path comes
  last.
- **Validation** (`EditorError`, once a box has been edited or at Save, D114), first that holds:
  `Choose the editor's program.` (empty); `There is no file at this path.`; on Windows `Choose the
  program itself (an .exe file): a .cmd or .bat file would run through cmd.exe.` (any extension but
  `.exe` or `.com`); elsewhere `This file is not marked as a program.` (no execute bit; on macOS also a
  `.app` bundle: choose its `Contents/MacOS/` file or its command-line tool); `A quote in the arguments
  is not closed.` The two checks that read the disk (`There is no file at this path.`, the execute
  bit) run at Save only, off the UI thread (7.8 step 1), never at a keystroke: a `\\server\share\…`
  path blocks for seconds to minutes per check (0.9); the others run on each edit.
- Stored as `"editor": { "id": "custom", "program": "D:\\Tools\\Ed.exe", "arguments": "-l {line} {path}" }`.

### 12.6 The setting

```csharp
/// <summary>The editor "Open in editor" starts (docs/phase-6-design.md 12). Null in AppSettings: none chosen.</summary>
/// <param name="Id">A KnownEditors id, or "custom" for Other.</param>
/// <param name="Program">The program's absolute path.</param>
/// <param name="Arguments">Other's argument template; null for a known editor, whose arguments come from the catalogue.</param>
public sealed record EditorSetting(string Id, string Program, string? Arguments = null);
```

A missing, null or unreadable `editor` (an id the catalogue no longer knows, other than `custom`)
means none chosen; an unknown id is logged once.

### 12.7 When an editor cannot start

Open file only logs a failure. An editor the user chose must say why it did not open: when
`OpenInEditorAsync` throws (`Win32Exception`, `InvalidOperationException`, `IOException`), the error
dialog opens without git output (D114): title `Could not open the editor`, summary `{Name} could not
be started: {exception message}`, or for a program that is gone (`FileNotFoundException`, 12.4)
`{Name} is not at {program} any more: choose an editor in Settings.`, the repository's name, and the
extra button `Open settings…` (`ErrorExtraButton`; it opens a dialog, so its label ends in "…", as
the palette's), which opens the settings screen on Tools.

### 12.8 Open file's question

`RepositoryOperations.CanRunPrograms` (`RepositoryOperations.Files.cs:109`) asks today by a fixed list
(`:117-121`) and, on Unix, the execute bits. From phase 6 by the union of:

1. **the fixed list**, a floor the user cannot remove (D130: a cloned repository can ship a runnable
   file): `.exe .bat .cmd .com .ps1 .vbs .vbe .js .jse .wsf .hta .msi .scr .pif .cpl .lnk .url .reg
   .jar .app .command .sh .desktop`, public in that order as
   `RepositoryOperations.FixedRunnableExtensions` for the settings screen;
2. **the user's extensions**, `AppSettings.ExtraRunnableExtensions` (7.3);
3. **on Windows, `PATHEXT`'s entries**, read at each Open file through `ToolCatalogues.PathExtensions`
   (12.9): on this machine `.wsh`, `.msc`, `.py` and `.pyw`, which open without a question today (0.9,
   risk 20);
4. on Unix, the execute bits, as today.

Case is ignored, as today. A new overload `CanRunPrograms(string fileName, UnixFileMode? unixMode,
IReadOnlySet<string> extraExtensions)` takes 2 and 3; the old one stays for its tests. `OpenFileAsync`
(`:84`) builds the set at each call. The question's texts stay (`OperationTexts.ConfirmOpenRunnable`,
`OperationTexts.cs:551`). Risk 20's new wording is 16.2's.

### 12.9 Fixed tool catalogues for the harnesses

What the machine has installed must never show in a picture: the editors found, the merge tools git
finds, `PATHEXT`. As `AppClocks` (`src/VisualCommit.Core/AppClocks.cs`), one seam in Core,
`ToolCatalogues` (`src/VisualCommit.Core/ToolCatalogues.cs`, with `FoundEditor`, so expectations can
name its entries): `FindEditors` (`Func<CancellationToken, Task<IReadOnlyList<FoundEditor>>>`),
`MergeTools` (`Func<CancellationToken, Task<MergeToolHelp>>`), `PathExtensions` (`Func<string?>`, the
`PATHEXT` value, null off Windows); `ToolCatalogues.Fixed`; `Variable =
"VISUALCOMMIT_TOOL_CATALOGUES"`; `FromEnvironment()`, `Fixed` when the variable is `fixed`, else null.
No catalogue says whether a program exists: the menus never ask (12.1). `AppSession.Start` takes
`ToolCatalogues? tools = null`; null means `FromEnvironment()`, and null again the machine's: 12.3's
`EditorCatalog`, 8.10's `ReadToolHelpAsync` (once per session, with its kept list) and the process's
`PATHEXT`. `TabServices.Tools` holds the result for the settings screen, Open file (12.8) and 8.10's
early start of the merge-tool lookup. `ShellDriver.Start` passes `Fixed`; `RealApp.Launch` sets the
variable (17.2).

| Catalogue | `ToolCatalogues.Fixed` |
|---|---|
| Editors found | `vscode` Visual Studio Code, `sublime` Sublime Text and `idea` IntelliJ IDEA (ids every platform's table in 12.2 has, with the same argument forms), under a root that does not exist: `C:\Tools\Microsoft VS Code\Code.exe`, `C:\Tools\Sublime Text\subl.exe`, `C:\Tools\JetBrains\IntelliJ IDEA 2026.1\bin\idea64.exe` on Windows; `/opt/tools/vscode/code`, `/opt/tools/sublime/subl`, `/opt/tools/idea/bin/idea` elsewhere. Nothing is ever started (the headless launcher records and checks no program, the real-window pass never clicks Open in editor) |
| Merge tools | found `kdiff3`, `meld`, `vimdiff` (left out by 8.10's filter, which the list thereby proves); not found `araxis`, `bc`, `p4merge`, `winmerge`; no descriptions; there at once, without 8.10's kept list (`merge-tools.json` neither read nor written). The screen adds the read's user-defined tools (`slow` when the session's file defines `mergetool.slow.cmd`) and the current value, as for a real list. A check of the loading state passes catalogues whose list waits until the check releases it (`tests/VisualCommit.Testing/Headless/HeldMergeTools.cs`) |
| `PATHEXT` | on Windows its default, `.COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC`, so `RunnablePathExt` reads `From PATHEXT: .wsh .msc`; none elsewhere. The test process's own `PATHEXT` is fixed to the same value (17.2) |

### 12.10 Tests

- `KnownEditorsTests` (pure): each platform's candidates from a fake `EditorSearchContext`; no Windows
  candidate ends in `.cmd` or `.bat`; JetBrains folders newest first, by the exact program name
  (`fsnotifier64.exe` and `runnerw64.exe` beside `idea64.exe` not taken); Visual Studio's editions in
  order.
- `EditorCatalogTests`: `FindAsync` over a fake file system; `(git's editor)` from a `core.editor`
  with quotes and arguments (`"C:\Program Files (x86)\Notepad++\notepad++.exe" -multiInst …`); a moved
  JetBrains folder updates the setting.
- `EditorCommandsTests`: `Build` for each Windows entry and for Other (splitting, quotes, `{path}` and
  `{line}` inside an argument, the path appended without `{path}`, the unclosed quote), the working
  directory, `ELECTRON_RUN_AS_NODE` removed, a `.cmd` refused.
- `FileLauncherTests` (extended): `ToStartInfo` with a working directory and removed variables; no
  shell; `OpenInEditorAsync` with a program that does not exist throwing `FileNotFoundException` from
  its `Task.Run`, before any start.
- `FileMenuEditorTests` (new; 18 gives `RepositoryMenusTests` to WP-RA): every file menu unchanged
  with no editor; with one, `Open in editor` right after `Open file`; its disabled reason; with an
  editor whose program does not exist, the item enabled (no menu looks at the program).
- `RepositoryOperationsFilesTests` (extended): `OpenInEditorAsync` records `Edit` with the exact
  program, arguments and folder and asks nothing for an `.exe` file; a throwing launcher opens `Could
  not open the editor` with `Open settings…`, whose click opens the screen on Tools, and a
  `FileNotFoundException` the text `{Name} is not at {program} any more: choose an editor in
  Settings.`; `CanRunPrograms`
  with the floor, an extra extension and the fixed `PATHEXT`, case ignored: `tool.msc` asks on Windows
  through `PATHEXT`; `run.py` opens at once and, with `.py` added in the settings, asks on every
  platform.
- `SettingsDialogViewModelTests` (Tools): the Pick dialog's options from the fixed catalogue; Other's
  validation texts, the two that read the disk only at Save and off the UI thread (a file-exists
  function that throws on the UI thread); Browse… fills the box from `FakeFolderPicker.AnswerFile`.

The visual checks are 17.4's S14 to S21 (every section at 1100×700, General and Git at 1920×1080, a
Pick dialog over the screen, a validation state, 7.9's effects); a repository's Rebase over a seeded
global `pull.ff=only` (8.7's per-key precedence) is S19's. An included value's "Still" notice (8.4)
and a held `.git/config.lock` (8.5) are proven by 8.13's tests only. The real window repeats a picture
of the screen in each theme with the fixed catalogues (S14, L34) and never clicks Open file or Open in
editor; in S14's launch it goes through Tools, `Choose…`, `Other…` and `Browse…`, types the system
folder's `notepad.exe` into Windows' file dialog `Choose the editor's program` (`ChooseFileInDialog`,
17.2), asserts that `EditorProgramBox` holds that path and cancels the screen, so that
`StorageFolderPicker.PickFileAsync` through `TryGetLocalPath` is proven once; the native dialog is not
pictured, since it shows the machine's folders.

## 13. The welcome page and the tab strip

### 13.1 The welcome page's Workspaces section (D188)

The welcome page always shows `Workspaces` between its buttons and `Recent repositories`, also with
no workspace (it is where workspaces start), inside `WelcomeButtons`: the clone form, which hides
that panel, hides it too and keeps its pictures. After `Open`, `Clone`, `Init` and the error line, in
the stack's 8-px spacing:

1. A heading row, 20 high, 16 above: left `Workspaces` (`WorkspacesHeading`, `TextBlock.heading`: 11,
   SemiBold, secondary, centred in the row); right `NewWorkspaceButton` (`VcFlatButton`, padding 6,2: a 12×12 `IconPlus`,
   4, `New workspace…`, secondary, primary on hover; 133 × 20), tooltip `Create a workspace:
   repositories you check and update together`, or the read-only reason while disabled.
2. `WorkspacesNotice` (secondary, wraps; `VcWarningBrush` while `SaveFailed`) while the store has a
   notice (1.3: a file set aside, one that could not be read, a newer version's, a failed save).
3. `NoWorkspacesText` `No workspaces` (secondary), or `WorkspaceList` (an `ItemsControl` of
   `TabServices.WorkspaceItems`, by name: predictable, and workspaces need no order of their own):
   per workspace a `WorkspaceItem` (`VcFlatButton`, padding 8,5, as a recent item), 26 high, full
   width: the name (primary, cut with "…") and, 12 apart at the right, `{N} repositories` / `1
   repository` / `No repositories` (secondary); automation name the workspace's name; tooltip its
   repositories' folder names in order, joined by `, `, at most 8, then `, …`, or `No repositories
   yet`.

The section is 68 high with no workspace (8 + 16 + 20 + 8 + 16), 52 + 26 × N with N (WP-W's build:
a line of 12.5-px text is laid out 16 high, as every line of the page already was; the first
estimate was 66 and 51 + 25 × N). In the page's
room (the graph area: 440 × 586 at 1100×700, 1260 × 966 at 1920×1080) the stack (`MaxWidth` 440,
margin 24) is 392 and 440 wide; 10 recent repositories and 3 workspaces take about 567, so a fourth
workspace scrolls the page at 1100×700, as a long recent list does. The stack is centred vertically,
so the page moves up by half the section's height: every earlier picture of an empty tab changes
(16.1), each the same way (the section between `Init` and `Recent repositories`, the stack 34 px
higher, less where the page scrolls). The alternatives change the same pictures or hide the feature:
`New workspace…` beside `Recent repositories` adds a button in the same region and, about 133 × 20 in
a heading row about 15 high, still moves the list; a section shown only once a workspace exists
leaves the palette as the only place to create the first one (1.5).

A click, Enter or Space on an item opens the workspace (D188): `ShowWorkspace(id, from: the page's
tab)`, placed by 2.2. A right-click, Shift+F10 or the Menu key (D106) opens
`WorkspaceMenus.ForWelcomeItem` (data, D86): header the name, `Open`, separator, `Rename workspace…`,
`Delete workspace…` (destructive), the last two disabled with the read-only reason while read-only.
`WelcomeViewModel` gains `Workspaces`, `HasWorkspaces` (following `CollectionChanged`, left in
`Dispose`, as for `Recent`),
`WorkspacesNotice`, `HasWorkspacesNotice`, `NewWorkspaceCommand`, `OpenWorkspaceCommand(WorkspaceItem)`,
`CanEditWorkspaces`, `WorkspacesEditReason`. The page's texts in reading order (`VisibleTexts.In`):
`No repository open`, `Open, clone or init a repository to see its history.`, `Open`, `Clone`,
`Init`, `Workspaces`, `New workspace…`, `No workspaces`, `Recent repositories`, `No recent
repositories`.

### 13.2 A long folder in the welcome page's errors: `PathTextBlock` (from phase 1)

`PathTextBlock : TextBlock` (`App/Controls/`), built as `UrlTextBlock`: `CreateTextLayout` draws a
changed text while `Text`, the automation name and what tests read stay as given; `StyleKeyOverride`
is `TextBlock`, so `TextBlock.error` applies. Inside each rooted path it puts U+FEFF (ZERO WIDTH NO-BREAK SPACE, class WJ) between
every two characters, except after `\` or `/`, where it puts U+200B (ZERO WIDTH SPACE, class ZW: a
break after it), never inside a surrogate pair. A line then breaks before a path, after a separator
or after the path; only a name longer than the line breaks where it must. Both marks have no advance
in Inter (0.8).

`PathTextBlock.BreakOnlyAfterSeparators(text)`, public and pure, matches with one regular expression,
in this order: a drive path `(?<![\w])[A-Za-z]:[\\/][^\s"'<>|?*]*` (also the `C:/…` of a `file:///C:/…`
URL); a UNC path `\\\\[^\s"'<>|?*\\]+\\[^\s"'<>|?*]*`; a rooted Unix path at the start or after a
space, a quote or an opening bracket (so `and/or` is none) `(?<=^|[\s"'(\[<])/[^\s"'<>|?*]+`. URLs keep
today's breaks. `UrlTextBlock` and its dialogs (the error dialog's summary, the confirmation, the
credentials, passphrase and prompt dialogs) stay: their texts seldom hold a long path, and changing
them would move lines in earlier pictures with no defect recorded there.

Used by `WelcomeError` (`{path} is not a git repository.`, Init's git errors), `CloneError` and
`ScanFolderText` (1.9). Phase 1's `11a` at 392 px draws 0.8's two lines, centred; at 292 px the path
breaks after `Temp\`, not after `C:`; phase 1's `14a` may move a line.

### 13.3 The tab strip's overflow: `RepoTabStripPanel`

A dashboard and ten more tabs (D187's ten repositories; in the Web scenario its nine that can open and
one empty tab from "+", 17.4's L33) are 11 × 120 = 1,320 px, more than the strip's 1,062 at 1100 wide. `RepoTabStripPanel : Panel` (`App/Views/Shell/`) keeps today's layout while the tabs fit,
then shrinks them, then scrolls them. In `RepoTabsView.axaml` the outer horizontal `StackPanel`
becomes `DockPanel LastChildFill="False"`, `TabList` and `AddRepoButton` docked left (it measures
`TabList` with the strip's width, a `StackPanel` with infinity); `TabList`'s items panel is
`<shell:RepoTabStripPanel Reserve="38" ActiveIndex="{Binding ActiveTabIndex}" ClipToBounds="True" />`
(38: "+" 34 wide with 2 either side); the tab's `Border` keeps `MaxWidth="220"` and gets
`MinWidth="80"` (a `Border` cannot be arranged below its `MinWidth`; the panel keeps 120 as the
natural floor). `MainWindowViewModel.ActiveTabIndex` is `Tabs.IndexOf(ActiveTab)`.

**Measure**, `A` the strip's width less `Reserve`: (1) a tab measured with an infinite width has its
natural width `n = max(120, desired)`, today's; (2) when `Σ n ≤ A`, `t = n`: as today; (3) otherwise,
when `80 × count ≤ A`, the widest shrink first: the cap `c` is the largest width with `Σ min(n, c) =
A`, `t = min(n, ⌊c⌋)`, the pixels left over one each to the leftmost tabs at the cap (`Σ t = A`), and
"+" sits at the right end; (4) otherwise every tab is 80 and the panel scrolls; (5) each tab is
measured again with its `t`, which cuts its title with "…".

**Arrange**: each tab at `(x − Offset, 0, t, height)`, `x` the widths before it; `Offset` is 0 except
in step 4, `0 ≤ Offset ≤ Σ t − A`. When the active tab, the number of tabs or the width changes,
`Offset` moves as little as brings the active tab whole into view; tabs outside are clipped. A wheel
notch over the strip (either axis) moves `Offset` by 80, clamped (down or right shows tabs further
right), handled only while the tabs overflow; it may leave the active tab out of view until the next
change. A tab whose button takes the focus is brought into view (`RequestBringIntoView`). No arrow buttons: they take room from the tabs, and the wheel, 6.5's
tab keys (Ctrl+Tab, Ctrl+PageDown, Mod+1 to Mod+9) and the palette reach every tab.

| Window (room `A`) | Natural | Shrunk | Scrolled (80 each) |
|---|---|---|---|
| 1000 (962) | 8 tabs of 120 (960) | 12 of 120: `c` = 80.2, two 81 and ten 80 | 13 (1,040) |
| 1100 (1062) | 3 (phase 5's A1, phase 1's 15a) | a dashboard and ten tabs of natural width 120: six 97 and five 96 | 14 (1,120); with the last active `Offset` 58, the first tab shows its last 22 px |
| 1920 (1882) | 15 of 120 (1,800) | 23: 81 or 82 | 24 (1,920) |

Every natural width is at least 120, more than each cap in the table, so which tabs fill the strip
changes no width (L33 adds tabs from "+", `New tab`, natural width 120).

A title gets its tab's width less 51: 69 px at 120, 45 or 46 at 96 and 97, 29 at 80 (two to four
characters and "…"); the tooltip gives it whole. A dashboard's title gets its tab's width less 69
(13.4): 51 px at 120, 27 or 28 at 96 and 97 (about one character and "…"; `Web`, 26.9 px, fits the
28 of the first tab's 97 at 1100), 11 at 80 ("…" alone after the icon, 0.8). That is accepted: the
icon still marks the tab, and its tooltip, the palette and the tab keys still name and reach it. A
floor of its own (98) would change the shrinking and this table, and the panel would have to take
each tab's floor from its child, since a `Border` cannot be arranged below its `MinWidth`. Narrow tabs
keep their close buttons: hiding them would change the natural widths the shrinking is computed from.

**Earlier pictures**: while the natural widths and "+" fit (8 tabs of 120 at 1000 wide, at least 4 of
any title), every tab and "+" keep their place; no earlier picture has more than 3 tabs (0.8). In
17.7's comparison by region, a change in an earlier picture's tab-strip region is a defect.

### 13.4 A dashboard's tab in the strip

The same 120-to-220 tab with a 12×12 workspace icon before the title, so a workspace and a repository
of one name are told apart (the members `IsDashboard` and `TabAutomationId` are 2.1's):

- `IconWorkspace` in `Theme/Icons.axaml`, Lucide's `layout-grid`:
  `M4 3 h5 a1 1 0 0 1 1 1 v5 a1 1 0 0 1 -1 1 h-5 a1 1 0 0 1 -1 -1 v-5 a1 1 0 0 1 1 -1 z M15 3 h5 a1 1 0 0 1 1 1 v5 a1 1 0 0 1 -1 1 h-5 a1 1 0 0 1 -1 -1 v-5 a1 1 0 0 1 1 -1 z M15 14 h5 a1 1 0 0 1 1 1 v5 a1 1 0 0 1 -1 1 h-5 a1 1 0 0 1 -1 -1 v-5 a1 1 0 0 1 1 -1 z M4 14 h5 a1 1 0 0 1 1 1 v5 a1 1 0 0 1 -1 1 h-5 a1 1 0 0 1 -1 -1 v-5 a1 1 0 0 1 1 -1 z`;
  `THIRD-PARTY-NOTICES.md`'s Feather list gains `layout-grid (Feather's grid)`.
- `TabIcon`, a new child of the tab's `Grid` in the button's cell, left, margin 14,0,0,0, not hit
  test visible, `IsVisible="{Binding IsDashboard}"`; `Button.dashboard` (`Classes.dashboard="{Binding
  IsDashboard}"`) gives `TabButton` the padding 32,0,8,0, so the title starts 6 after the icon; other
  tabs draw exactly as before. The icon takes the title's colour: primary on the active tab, secondary
  on the others (`Border.repoTab:not(.active) c|Icon#TabIcon`).
- Natural width `clamp(69 + title, 120, 220)`: a title gets at most 151 px.
- `TabButton`'s automation id is bound: `DashboardTab` on a dashboard, `RepoTab` otherwise; its name
  and tooltip are the workspace's name. The real-window pass's `ShownElements("RepoTab")` still finds
  only repository and empty tabs. A dashboard tab's `AutomationProperties.ItemStatus` is `Workspace`
  (none on other tabs), so a screen reader tells a workspace from a repository of the same name; its
  name stays the workspace's name for the harnesses' lookups. "+" (`AddRepoButton`, id unchanged) is
  named `Open a new tab` instead of today's `Add repository` (`Views/Shell/RepoTabsView.axaml:66-72`),
  since the dashboard header's `Add repository…` adds to a workspace (2.1); not `New tab`, the empty
  tab's own `TabButton` name.

### 13.5 Tests

Headless, in the project 17.3's measurement chooses: `PathTextBlockTests` (in `UrlTextBlockTests`'
style: the three kinds of path, `and/or` and a URL untouched, a surrogate pair whole, the text back
without the marks; at 392 px `C:\Users\someone\AppData\Local\Temp\VisualCommit.Tests\plain-a373a2259cd0
is not a git repository.` draws `C:\Users\someone\AppData\Local\Temp\VisualCommit.Tests\` and
`plain-a373a2259cd0 is not a git repository.`; at 292 px every line but the last ends with a
separator or a space and none is `C:`; an over-long name stays within the block; a short path is as
wide as in a `TextBlock`, ±0.5 px); `WelcomeWorkspacesTests` (13.1's texts and order with none and
two workspaces, the tooltips, the notices (the warning tone while `SaveFailed`), the read-only button
for either cause, a click calling the host with the
page's tab, the menu, the clone form hiding the section; pictures `welcome-workspaces-dark`,
`welcome-workspaces-light`); `RepoTabStripPanelTests` (every row of 13.3's table: widths, places,
`Offset`; the active tab brought into view from either side; the wheel; `RequestBringIntoView`; in
`RepoTabsView`, three tabs at 1100 placed exactly as by the old `StackPanel` template, fourteen with
"+" whole at 1,064 to 1,098, the dashboard tab's icon, padding, id and width, its `ItemStatus`
`Workspace` and none on a repo tab, "+"'s name `Open a new tab`; a dashboard tab at 97
and at 80, its title arranged `t − 69` wide and drawn `Web` whole at 97, "…" alone at 80).

Phase 1's check 11 gains: every drawn line of `WelcomeError` but the last ends with a separator or a
space (`DrawnLines.Of(TextBlock)`, `tests/VisualCommit.Testing/Headless/DrawnLines.cs`, shared); the
welcome texts of phases 0, 1 and 5 gain the section's three (16.1). Visual checks: 17.4's L26, L32
(asserted on the drawn lines: the temporary folder differs per machine) and L33; the real window
repeats the Workspaces section in W1.

## 14. Texts

Every new or changed text of sections 1 to 13 once, grouped by where it shows, with the section that
defines it; that section wins if the two ever differ. Counts take the singular for one (D113); where a
count is shown in one form the other follows the same pattern. House style (phases 3 to 5): disabled
reasons, tooltips and status-bar results have no full stop; error summaries, notices and questions are
sentences with one. `{workspace}` is the workspace's name, `{b}` the current branch, `{up}` its
upstream as the toolbar names it (by its remote-tracking ref when the fetch lines map it to one, as
`git status` names it, else `{remote}/{remote ref}`: 10.4, GD15), `{path}` a saved folder, `{T}` a
remote-tracking ref's short name, `{r}` its remote, `{y}` the branch's name on that remote.

### 14.1 Operations: title, running text, error title, summary

| Operation | Title | Running text | Error title | Summary | Section |
|---|---|---|---|---|---|
| Save this repository's git settings (a queue operation, `OperationKind.Local`, `UndoScope.None`) | `Save repository settings` | `Saving the repository's settings…` | `Saving the settings failed` | `Git could not save the repository's settings.` | 8.3 |
| The global git settings (no operation: the app-level writer, one call per key) | — | `Saving…` in `SettingsSaving` | `Saving the settings failed` | `Git could not save the settings for all repositories.`, git's output and the command; no repository line | 7.8, 8.3 |
| A row's Fetch, and each row of Fetch all | the toolbar's Fetch of all remotes, unchanged | unchanged | `Fetch failed` | unchanged: D109's `Git could not fetch from origin and mirror.` (`Git could not fetch from broken.`), else `Git could not fetch from all remotes.` | 4.7, 4.8 |
| A row's Pull, and each row of Pull all | the toolbar's Pull, unchanged | unchanged | `Pull failed` | unchanged but for the three rows below | 4.7, 4.8 |
| Pull refused because the branches diverged under `pull.ff=only` (toolbar, menus, rows) | | | `Pull failed` | new `{b} and {up} have diverged, and pull.ff is set to only: merge {up} into {b} or rebase {b} onto {up}.` (`OperationTexts.PullNotFastForward`, `{up}` as the menus name it), git's output and `git merge --ff-only --no-overwrite-ignore FETCH_HEAD` below (`git merge --ff-only FETCH_HEAD` when Keep ignored files is off, 9.10); replaces `Git could not pull {up} into {b}.` only for a `git merge --ff-only …` refusal: a plain merge refused by `merge.ff=only` or `branch.<b>.mergeOptions` keeps `Git could not pull {up} into {b}.` (4.8) | 4.8 |
| Pull with an ignored file in the way (inside `IntegratePullAsync`) | | | `Pull failed` | 14.4's text with `merge` or `rebase`, no git output; under `--ff-only` on diverged branches git's refusal (the row above) instead | 9.10, 9.15 |
| Pull with a pull setting the app does not understand | | | `Pull failed` | unchanged `pull.rebase is set to {value}, which this app does not understand.`, command `git config --get pull.rebase`, now before anything is fetched; for "Pull from {up} (rebase)", which reads `pull.ff` alone, `pull.ff is set to {value}, which this app does not understand.`, command `git config --get pull.ff`, also before the fetch (D231) | 8.9 |
| The drop's push onto a renamed remote branch | `Push {A} to {T}` (label unchanged) | `Pushing {A} to {T}…` | unchanged | unchanged | 10.4 #8, 10.5 |
| `Delete {y} on {r}…` under a renaming refspec | `Delete {y} on {r}` | `Deleting {y} on {r}…` | unchanged | the lease's `{y} on {r} has changed since you last fetched. Fetch and look at it before deleting.` | 10.4 #9, 10.5 |
| Merge (both kinds), Fast-forward, Rebase, Interactive rebase, Squash, Cherry-pick, Revert, Applying the stash, refused by the app's ignored-files check | existing | existing | existing: `Merge failed` ("Check out and merge" too), `Fast-forward failed`, `Rebase failed`, `Interactive rebase failed`, `Squash failed`, `Cherry-pick failed`, `Revert failed`, `Applying the stash failed` | 14.4, no git output | 9.9, 9.15 |
| Every checkout refused for ignored files only | existing | existing | `Checkout blocked` | D179's sentence with git's count, now on every checkout path (14.4) | 9.11, 9.15 |
| A remote branch's checkout reached from a stale menu while two remotes or none fetch it | existing | — | `Checkout failed` | R1 or R3 (14.5), before git | 10.4 #10 |
| A push of a local branch that would give a tracking ref a second source (Push, the push dialog, the drop's two pushes, Force push) | existing | — | `Push failed` / `Force push failed` | `Pushing {A} to {target} would stop every fetch of {r}: {D} is fetched from more than one ref of {r}.` (`PushWouldStopFetch`), before git, no git output | 10.4, 10.5 |

The status bar shows `{Title} failed` for every refusal, as today. "Finishing operations" keeps its
line `{Title} — {repository}`, so a bulk pull of four rows shows four (`Pull — api`, `Fetch — docs`)
(3.7, 4.12).

### 14.2 Results in the status bar

| After, or where | Text (tone) | Section |
|---|---|---|
| Save repository settings | `Saved the repository's settings` | 8.3 |
| The drop's push onto a renamed remote branch | `Pushed {A} to {T}` | 10.4 #8 |
| `Delete {y} on {r}…` | `Deleted {y} on {r}` | 10.4 #9 |
| Checking out a renamed remote branch | `Created {y} tracking {T} and checked it out` | 10.5, 10.6 |
| Checking out a remote branch one remote fetches from two refs (R2) | `Created {n} at {T} without an upstream: {T} is fetched from more than one ref of {r}` (`CreatedWithoutUpstream`) | 10.4 #10, 10.5 (D201) |
| A dashboard tab, before its first run | `Ready` (secondary) | 4.9 |
| A dashboard tab, during and after a run | the header's run text, 14.3, the same in both places; ssh's newest notice in its place while one shows | 4.9 |
| A dashboard tab, the rows in line | `{w} waiting` (`WaitingText`'s form) | 4.9 |
| A dashboard tab, Cancel | Cancel all; tooltip `Cancel the rest of Fetch all` / `Cancel the rest of Pull all`; hidden once clicked until the run ends | 4.5, 4.9 |
| A dashboard tab, the branch part | `{workspace}` after `IconWorkspace` (`StatusWorkspaceIcon`); a repo tab's is unchanged | 2.8 |

### 14.3 The dashboard

| Where | Text (tone) | Section |
|---|---|---|
| Tab title and tooltip | `{workspace}`, following a rename | 2.1, 13.4 |
| Header, title | `{workspace}` (13, SemiBold, cut with "…" at 240, whole in its tooltip) | 2.5 |
| Header, count | `1 repository` / `{n} repositories` / `No repositories` | 2.5 |
| Header, buttons | `Add repository…` (tooltip `Add a repository to {workspace}`, or the read-only reason while disabled); `Fetch all`, `Pull all` (4.10's tooltips and reasons); `Refresh` (tooltip `Read the repositories of {workspace} again ({key})`, Refresh's key, always enabled); `DashboardMore` (tooltip and automation name `More`) | 2.5 |
| Header, `DashboardMore`'s menu | header `{workspace}`; `Add open tabs`; `Rename workspace…`; `Delete workspace…` (destructive) | 2.5 |
| A failed save (`DashboardNotice`, below the header, warning tone) | `workspaces.json could not be saved, so changes to workspaces are lost when VisualCommit closes.` | 1.3, 2.5 |
| An empty workspace | `{workspace} has no repositories yet.`; buttons `Add repository…`, `Add open tabs` | 2.5 |
| Column headings | `Repository`, `Folder`, `Branch`, `Ahead / behind`, `Changes`, `State`, `Operation` | 2.6 |
| Repository cell | the folder's name (primary); tooltip the folder (at 1100 the only place it shows) | 2.6 |
| Folder cell | the saved folder (secondary), whole in its tooltip when cut | 2.6 |
| Branch cell | `{b}` (also unborn); `Detached at {short}`; during a rebase that names its branch, that `{b}` | 2.6 |
| Ahead / behind cell | the left panel's arrows and counts, automation name `{a} ahead, {n} behind` / `{a} ahead` / `{n} behind`; `Up to date`; `{up} is gone` (warning); `No upstream`; `No remote`; `No commits yet`; empty when detached or rebasing | 2.6 |
| Ahead / behind tooltips | `1 commit ahead of {up}` / `{a} commits ahead of {up}`; `1 commit behind {up}` / `{n} commits behind {up}`; `{a} commits ahead of and {n} behind {up}` (`1 commit ahead of and …`); `{b} is level with {up}`; `{up} no longer exists` (the toolbar's); `{b} has no upstream`; `This repository has no remotes` (the toolbar's) | 2.6 |
| Changes cell | `Clean` (secondary); the counts above 0 joined by `, `: `1 conflict` / `{c} conflicts`, `{s} staged`, `{u} unstaged`, `{t} untracked`, danger while a file conflicts; tooltip the whole text | 2.6 |
| State cell | `Reading…` (secondary; also while the row's first folder check has not returned, and for a row that follows a tab until that tab's first read); `Merge in progress`, `Cherry-pick in progress`, `Revert in progress`, `Rebase in progress`, `Rebase in progress ({k} of {n})`, `git am in progress` (warning; tooltip the banner's); `Folder not found`, `Not a repository`, `Git could not open it` (warning) | 2.6, 1.10 |
| State tooltips (also the reason of the row's Open, Fetch and Pull) | `{path} does not exist`; `{path} is not a git repository`; `{path} is not a git repository of its own: it lies inside {outer}`; `Git could not open {path}: {line}`, `{line}` git's first line of standard error or the exception's message | 1.10, 2.6 |
| Operation cell, an operation of the user runs | `CompactText`: the running text and git's percentage, no stage, no ssh notice (`Fetching from all remotes… 45%`, `Pulling origin/main into main…`); tooltip `CompactToolTip`, the status bar's whole text; then `Cancel` (name `Cancel`, tooltip the status's `CancelToolTip`: `Stop git`, `Stop git and the merge tool` while the merge tool runs) | 2.6, 4.5 |
| Operation cell, other states | empty when ready; `Waiting for auto-fetch…`; the last result in its tone until the next operation starts (`Fetched from all remotes`, `Pull stopped: 1 file has conflicts`, `Fetch failed`, and 4.8's other outcomes, all existing texts) | 2.6, 4.8 |
| Operation cell, held by a bulk run | `Waiting to fetch…` / `Waiting to pull…`; a row left out: the toolbar's reason (secondary, tooltip the reason): `This repository has no remotes`, `HEAD is detached: check out a branch to pull`, `{b} has no upstream: set one first`, `{up} no longer exists`, `Finish or abort the {kind} first`; stopped in line `Fetch cancelled` / `Pull cancelled`; a kept failure, then `Details…` (name `Details`) | 4.2, 4.5, 4.6 |
| The run, running | `Fetching {n} repositories…` / `Pulling {n} repositories…` (`Fetching 1 repository…`); once rows ended `Fetching 10 repositories… 4 done` / `Pulling 6 repositories… 1 done` | 4.9 |
| The run, after Cancel all | `Cancelling: waiting for {r} repositories…` / `Cancelling: waiting for 1 repository…` | 4.5, 4.9 |
| The run, its result | `Fetched {k} repositories` / `Pulled {k} repositories` (`Fetched 1 repository`, `Fetched no repositories`), then each only when not 0, in this order, `; {f} failed`, `; {s} stopped`, `; {c} cancelled`, `; {x} skipped`; danger when any failed, else warning when any stopped, else secondary. `Fetched 9 repositories; 1 failed`, `Pulled 6 repositories; 1 failed; 2 skipped`, `Pulled 4 repositories; 1 stopped; 3 skipped`, `Fetched 3 repositories; 5 cancelled; 2 skipped` | 4.9 |
| The run's button | `Cancel all` (name `Cancel all`); tooltip `Cancel the rest of Fetch all` / `Cancel the rest of Pull all` | 2.5, 4.5 |

### 14.4 Refusals and other error dialogs

| Case | Title | Summary | Section |
|---|---|---|---|
| The app's ignored-files check: merge of either kind, fast-forward, the drop's merge, "Check out and merge"'s merge, the pull's merge or fast-forward; rebase onto, "Check out and rebase", interactive, the pull's rebase; squash; cherry-pick; revert, one or a list | the operation's (14.1) | `1 ignored file is in the way of the {operation}: {file}. Move it away first.` / `{N} ignored files are in the way of the {operation}: {first names}. Move them away first.`; `{first names}` at most three names, followed by `, …` only when there are more (`FirstNames`, `NamesShown`); `{operation}` one of `merge`, `rebase`, `squash`, `cherry-pick`, `revert`. `5 ignored files are in the way of the cherry-pick: build/a.bin, build/b.bin, build/c.bin, …. Move them away first.` | 9.15 |
| Stash apply and pop | `Applying the stash failed` | `1 ignored file is in the way of applying the stash: {file}. Move it away first.` / `{N} ignored files are in the way of applying the stash: {first names}. Move them away first.` | 9.15 |
| A checkout git refused for ignored files only (every checkout path) | `Checkout blocked` | D179's `1 ignored file is in the way: {name} tracks a file of the same name. Move it away first.` / `{N} ignored files are in the way: {name} tracks files of the same names. Move them away first.`, git's output and command below; no "Stash changes and check out" | 9.11, 9.15 |
| A stale menu's checkout of a remote branch two remotes or none fetch | `Checkout failed` | R1 or R3 (14.5) | 10.4 #10 |
| A push that would stop every fetch of its remote | `Push failed` / `Force push failed` | 14.1's `PushWouldStopFetch` | 10.4, 10.5 |
| Adding a repository | `Adding a repository failed` | `Git could not open {folder}.`, git's output below; with no usable git the exception's message | 1.7 |
| Locating a repository | `Locating the repository failed` | `{folder} is not a git repository.`; `{top} is already in {workspace}.`; `Git could not open {folder}.` with git's output | 1.10 |
| An editor that cannot start | `Could not open the editor` | `{Name} could not be started: {exception message}`; for a program that is gone (`FileNotFoundException`) `{Name} is not at {program} any more: choose an editor in Settings.`; the repository's name; extra button `Open settings…` (opens Tools); no git output | 12.7 |
| A global settings write | `Saving the settings failed` | `Git could not save the settings for all repositories.`, git's line and command; a held lock's `could not lock config file` (255) likewise | 7.8, 8.5 |
| A bulk run's kept result, opened by `Details…` or `Error details…` | the operation's | the identical C11 dialog it would have opened (summary, git's output, command, "Copy output", the extra button), naming its repository; a cancel's lock files in the "{Title} cancelled" dialog with "Remove lock files"; the pull's "Rebase stopped" notice | 4.6, 4.8 |

The workspace flows' errors use the C11 error dialog with the workspace's name as its "In {name}" line
(1.5). Removed: `{remote} maps its branches differently: use Push {branch}…`
(`OperationTexts.MapsBranchesDifferently`), replaced by R1 to R4 (10.5).

### 14.5 New disabled reasons

| Item | Reason | Section |
|---|---|---|
| Every workspace edit while the store is read-only (1.3) (`New workspace…`, `Rename workspace…`, `Delete workspace…`, `Add repository…`, `Add open tabs`, `Remove from workspace`, `Move up`, `Move down`, `Locate…`); a disabled item's key does nothing | `workspaces.json was made by a newer version of VisualCommit`; `workspaces.json could not be read` (a file that could be neither read nor set aside) | 1.3, 1.5, 2.7 |
| `Add open tabs` | `No tab has a repository open`; `Every open repository is in this workspace` | 1.5 |
| `Move up`, `Move down` | `It is the first repository` / `It is the last repository` | 1.5, 2.7 |
| A row's `Open` | `Wait for {name} to be read`; else the State tooltip (14.3) | 1.10, 2.7 |
| A row's `Fetch`, `Pull` | `Open`'s; the toolbar's own; while the row belongs to a running bulk run (in line, reading or running) `Wait for Fetch all to finish` / `Wait for Pull all to finish` | 2.7, 4.7 |
| A row's `Remove from workspace` (also Delete, and Cmd+Backspace on macOS) | while the row belongs to a running bulk run (in line, reading or running) `Wait for Fetch all to finish` / `Wait for Pull all to finish` | 1.5, 1.10, 2.7, 4.7 |
| A row's `Cancel {title}` (shown for a network operation and the merge tool; left out while any other local operation runs, which never could be cancelled) | `{title} can no longer be cancelled` (past its cancel window) | 2.7 |
| A row's `Show in folder` | `{path} does not exist` | 2.7 |
| A dashboard's toolbar Fetch and Pull, and the header's Fetch all and Pull all (enabled while rows are read; first that holds) | `Wait for Fetch all to finish` / `Wait for Pull all to finish` (both buttons); `{workspace} has no repositories`; `No repository of {workspace} has a remote` (Fetch) / `No repository of {workspace} can be pulled` (Pull). Push, Branch, Stash, Pop, Undo, Redo and Search disabled without a tooltip, as on an empty tab | 4.10 |
| The activity-log toggle on a dashboard (also the palette's entry), first that holds: its workspace without rows | `{workspace} has no repositories` | 2.9, 5.4 |
| … no row selected | `Select a repository to see its activity` | 2.9, 5.4 |
| … the selected row not read yet, or following a restored tab not opened yet | `Wait for {name} to be read` | 2.9, 5.4 |
| … the selected row in a folder state | its State tooltip (`{path} does not exist`; `{path} is not a git repository`; `{path} is not a git repository of its own: it lies inside {outer}`; `Git could not open {path}: {line}`) | 2.9, 5.4 |
| Palette `Close tab` | `This is the only tab, and it is empty` | 5.4 |
| Palette `Next tab`, `Previous tab` | `There is only one tab` | 5.4 |
| Palette `Go to the commit message` | `Close the conflict resolver first` (existing); `There are no changes to commit` | 5.4 |
| Palette's commit entry (the commit button's text, 5.4 row 6) | `Close the conflict resolver first` (existing); the button's reason, `CommitDisabledReason` (existing texts) | 5.4, 6.8 |
| Palette `Next match`, `Previous match` | `No matches` (existing) | 5.4 |
| Settings, `This repository` | `Open a repository's tab to change its own settings` | 7.4 |
| Settings, the merge tool's `Choose…` until a list is there (the first lookup on a machine, or after git changed; a kept list enables it at once) | `Looking for the merge tools git knows…` | 8.10 |
| `Open in editor` | `The file is not in the working tree` (existing) | 12.1 |
| R1 AmbiguousRemotes | `{T} is fetched from {remotes}` (`a and b`, `a, b and c`) | 10.5 |
| R2 AmbiguousRefs | `{T} is fetched from more than one ref of {r}` | 10.5 |
| R3 NotFetched | `{T} is not fetched from any remote` | 10.5 |
| R4 Mapped, not a branch (the drop's pushes and the deletion only) | `{T} is {ref} on {r}, not a branch` | 10.5 |

The force-push restore's fallback keeps the existing `The remote this backup came from cannot be found`
for a mapping that gives no branch, and "Set upstream…" its `No remote branches to track: fetch or push
first` when every ref is left out (10.4 #11, #12).

### 14.6 Dialogs and their buttons

| Dialog | Texts | Section |
|---|---|---|
| New workspace (the name dialog) | title `New workspace`; placeholder `Workspace name`; confirm `Create` | 1.6 |
| Rename workspace | title `Rename workspace`; context `Rename {name}`; placeholder `Workspace name`; confirm `Rename` | 1.6 |
| Name errors | `Enter a name.` (existing); `This is not a valid workspace name: it has more than 100 characters.`; `This is not a valid workspace name: it has a line break or another control character.`; `A workspace named {existing} exists.` | 1.6 |
| Delete a workspace | title `Delete workspace?`; `Delete the workspace {name}? Its {N} repositories stay where they are: nothing on disk changes.` / `Delete the workspace {name}? Its repository stays where it is: nothing on disk changes.` / `Delete the workspace {name}? Nothing on disk changes.`; detail while its dashboard is open `Its dashboard tab closes.`; confirm `Delete` (destructive) | 1.6 |
| Folder and file dialogs | `Add a repository to {workspace}`; `Locate {folder name}`; `Choose the editor's program`; the palette's Open and Init keep `Open a repository` and `Choose a folder for the new repository` | 1.7, 1.10, 12.5, 5.10 |
| Add repositories (the scan) | title `Add repositories to {workspace}`; searching `Looking for repositories in {folder}…` with `Searched {n} folders, found {k} repositories` (`1 folder`, `1 repository`; `N0`, invariant culture); done `Found {N} repositories in {folder}.` / `Found 1 repository in {folder}.` / `No repositories were found in {folder}.` / `No other repositories were found in {folder}.`; rows: the name, its path relative to the folder, or `Already in this workspace` (checked, disabled), or `Contains {folder name}` (unchecked); `Stopped after 10,000 folders: choose a folder further down to search the rest.`; `1 more folder looks like a repository, but git could not open it.` / `{N} more folders look like repositories, but git could not open them.` (tooltip their relative paths, at most 10, then `…`); buttons `Cancel`, `Add {N} repositories` / `Add 1 repository` (`{N}` the rows checked and enabled: `Already in this workspace` rows are left out), disabled `Add repositories` while that count is 0, also when every row found is already in the workspace | 1.9 |
| The palette | 14.7 | 5 |
| Settings | title `Settings`; buttons `Cancel`, `Save`; `SettingsSaving` `Saving…`, `Waiting for {title} to finish…`; the sections 14.8 | 7.1, 7.8 |
| Pick dialogs over the settings screen | titles `Choose an editor`, `Choose a merge tool`; confirm `Choose`; options (text — detail): `None` — `no Open in editor in the file menus`; `{Name}` — `{program path}` (` (git's editor)` after the name when it matches); `Other…` — `a program of your choice`; `Not set` — `Merge tool… stays disabled` (global) / `same as global ({tool})`, `same as global (not set)` (repository); `{tool}` — 2.55's description without ` (requires a graphical session)`, else `found on this computer`; `{name}` — `your own tool (mergetool.{name}.cmd)`; `{name}` — `not found on this computer`; `{value}` — `git does not know this tool`, or `runs in a terminal; the app cannot start it` | 7.7 |
| Sign-in and other prompts of a bulk run | unchanged texts; "In {repository}" under the title while a dashboard tab is open (`NamesRepositoryInDialogs`) | 3.8 |

### 14.7 The command palette

| Where | Texts | Section |
|---|---|---|
| Root (automation name only, no title drawn) | `Command palette` | 5.2 |
| Box | placeholder `Search commands, branches, tags, stashes, remotes, repositories and workspaces`; automation name `Search commands` | 5.3, 5.11 |
| List | automation name `Commands`; nothing matches: `No matches` (`SearchViewModel.NoMatchesText`) | 5.3 |
| Commands, new labels | `Pop the newest stash`; `Undo {text}` / `Undo`; `Redo {text}` / `Redo`; `Search commits`; `Next match`; `Previous match`; `Go to the commit message`; `Refresh`; `Filter branches and tags`; `Fetch every repository of {workspace}`, `Pull every repository of {workspace}` (on a dashboard); `New tab`; `Close tab` (detail the tab's title); `Next tab`; `Previous tab`; `Go to the next dashboard` (detail the workspace it goes to); `Open repository…`; `Clone repository…`; `Init repository…`; `Show the activity log` / `Hide the activity log`; `Open settings…`; `Show keyboard shortcuts…`; `Choose an editor…` (while no editor is chosen); `Use the dark theme`, `Use the light theme`, `Follow the system's theme` (the one chosen now left out) | 5.4, 11.7 |
| Commands, the menus' labels reused | `Fetch all remotes`, `Pull`, `Push`, `Create branch…`, `Stash changes…`, `Stage all`, `Unstage all`, `Show reflog`, `Show backups`, `Create tag at HEAD…`, `Add remote…`, `Manage remotes…`, `Show all hidden branches`, `Stop soloing`, `New workspace…`; the stage panel's commit button's text (`Commit 1 file`, `Commit merge`, `Continue rebase`, …; 5.4 row 6); on a dashboard `Add repository…`, `Add open tabs`, `Rename workspace…`, `Delete workspace…` | 5.4 |
| Detail of a repository's commands | the repository's name while `NamesRepositoryInDialogs` holds | 5.4 |
| Items | each object's menu items with the menu's header as detail; `Go to {title}` (detail `Tab {n}`); `Open repository {name}` (detail its folder); `Open workspace {name}` (detail `1 repository` / `{N} repositories` / `No repositories`) | 5.5 |
| Disabled entries | the menus' reason in the detail's place, whole in the row's tooltip | 5.7 |

### 14.8 The settings screen

| Part | Texts | Section |
|---|---|---|
| Section list | `General`, `Git`, `Tools`, `Keyboard shortcuts`; the error dot's help text `Has an error` | 7.1 |
| General, theme | label `Theme`; radios `Dark`, `Light`, `System`; under them `System follows your operating system's light or dark mode.` | 7.3, 11.7 |
| General, fetching | label `Fetching`; check box `Fetch in the background every`, the box, `minutes`; `Only the active tab's repository; it never asks for a password.`; error `Enter a whole number of minutes from 1 to 60.` | 7.3 |
| General, working tree | label `Working tree`; `Keep ignored files` with `A checkout, merge, rebase, cherry-pick, revert, stash apply or stash pop that would overwrite or delete an ignored file does not start. Off: they run as git runs them, which can overwrite or delete such a file without a word; checking out an older commit of the current branch, Undo and Redo still keep it.`; `Include untracked files when stashing` with `The Stash changes dialog starts with this; its own check box changes it too.` | 7.3, 9.1 |
| General, opening files | label `Opening files`; `Open file asks first before it opens a file with one of these extensions:`; the fixed list in its order; on Windows `From PATHEXT: {extensions}` (lower case, only those the list lacks; with the fixed catalogues `From PATHEXT: .wsh .msc`); `Also ask for`, placeholder `.py .pl`; error `"{entry}" is not an extension such as .py: use letters and digits.`; `On Linux and macOS it also asks before it opens a file marked as a program.` | 7.3, 12.8, 12.9 |
| Git, scope | `All repositories (global)`, `This repository`; enabled tooltip `{name} — {folder}` (`web-app — D:\src\web-app`); captions `Saved in your global git configuration, which git in a terminal uses too.` (tooltip and help text the file's path) / `Saved in this repository's .git/config, which all its worktrees share. These values win over those for all repositories.` | 7.4 |
| Git, states | `Reading git's configuration…`; `Git's configuration could not be read:` and git's first `fatal:` or `error:` line; `GitAccess`'s sentence and ` Git's own settings need it.` | 7.4 |
| Git, identity | labels `Name`, `Email`; placeholders `Not set`, `Same as global: {value}`, `From git's system configuration: {value}`; errors `A name cannot contain <, > or a line break.`, `Git refuses a name made only of punctuation.`, `An email address cannot contain <, > or a line break.`; warning `This does not look like an email address.` | 8.6 |
| Git, identity in effect (repository scope) | `Commits here are made as {author}.`; `Commits here are made as {author}, committed as {committer}.`; `Git cannot make commits here: no email is set.` / `… no name is set.` / `… the name is empty.` / `… it refuses the name.` / `Git cannot make commits here:` and git's line without `fatal: ` | 8.6 |
| Git, identity notices | `{VARIABLE} is set in the environment the app was started with, so git uses it for authors.` (`… for committers.`); `EMAIL is set in the environment the app was started with, so git uses it when no email is set.`; `{key} = {value} ({file}) is used for authors instead.` (`… for committers instead.`); `user.useConfigOnly is set, so git does not guess a missing name or email.` | 8.6 |
| Git, pull | label `Pull`; `Not set` (global) / `Same as global` (repository); `Merge (fast-forward when possible)`; `Merge, always with a merge commit`; `Rebase`; `Rebase, keeping merge commits`; `Fast-forward only`; `Other: {key} = {value}` (both keys: `Other: pull.rebase = preserve, pull.ff = sometimes`); `PullNotSet`'s tooltips `Nothing set: the app merges, and git in a terminal stops when the branches have diverged` / `Uses the pull mode for all repositories`; Other's second lines `Git opens an editor for the list; the app rebases without one.`, `Git 2.36 merges with this value and newer gits rebase keeping merge commits; the app does what its git does.`, `Git 2.36 reads this value as false and newer gits refuse it; the app does what its git does.` (a unit letter alone), `Pulls fail while this value is set.` | 8.7 |
| Git, pull in effect | `Pull into {b}: {mode} ({key} = {value} in {file}).`; `Pull into {b}: Merge (fast-forward when possible): nothing is set, so the app merges; git in a terminal stops when the branches have diverged.`; `Pull into {b} fails: {problem}` | 8.7 |
| Git, `branch.<b>.rebase` | `{b} has its own pull setting, branch.{b}.rebase = {value}, which git uses instead of the mode above.`; button `Use the repository's setting`; marked `Save removes branch.{b}.rebase, so {b} pulls as set above.`, button `Keep {b}'s setting` | 8.8 |
| Git, merge tool | label `Merge tool`; `Not set`; `Same as global ({tool})`, `Same as global (not set)`; `Choose…`; `Used by Merge tool… in the conflict resolver, and by git mergetool in a terminal.`; `Looking for the merge tools git knows…`; `Git could not list its merge tools.`; `git did not find {tool} on this computer: Merge tool… fails until it is installed.` | 7.4, 8.10 |
| Git, notices | `Still {value} in {file}, which git reads later.` (a pull key named: `Still pull.ff = only in work.gitconfig (included), which git reads later.`); `Still set to {value} in {file}: the app cannot change that file.`; `Git uses {value} from {file}.`; `In {repository}, git uses {value} from {file}.`; `This worktree has its own value, {value}, in config.worktree; the app does not change it.`; `{value} comes from the environment the app was started with and wins over every file.`; `{key} = {value} also applies, from git's system configuration.`; `{repository} sets its own: {value}.` | 8.4 |
| Git, file names in notices | `your global configuration`; `git/config under XDG_CONFIG_HOME`; `.config/git/config`; `{file name} (included)`; `git's system configuration`; `.git/config`; `{file name} (included by .git/config)`; `config.worktree (this worktree only)`; `the environment the app was started with` (the full path in each line's tooltip and help text) | 8.2 |
| Tools | label `Editor`; `None`, the editor's name (` (git's editor)` after it), `Other`; `Choose…`; `Open in editor appears in the file menus, beside Open file, once an editor is chosen.`; `Looking for editors…`; labels `Program`, `Arguments`; `Browse…`; hint `{path} is the file's full path, {line} its line (1 for now). Without {path}, the path comes last.`; errors `Choose the editor's program.`, `There is no file at this path.`, `Choose the program itself (an .exe file): a .cmd or .bat file would run through cmd.exe.`, `This file is not marked as a program.`, `A quote in the arguments is not closed.` | 7.5, 12.5 |
| Keyboard shortcuts | `These keys are fixed.`; group titles `App`, `Tabs`, `Repositories`, `Repository`, `Changes`, `Files`, `Moving around`, `Interactive rebase`, `Conflict resolver`, `Dashboard`, `Dialogs and menus`; each row's action is 6.1's name, from both tables (`Command palette`, `Open settings…`, `Show or hide the activity log`, `Refresh`, `New tab`, `Close tab`, `Go to the next dashboard`, …, `Remove from workspace`), which is not always the palette's label (6.1); on macOS the Moving around group's note `On a Mac keyboard F6, Shift+F6 and Shift+F10 need Fn unless "Use F1, F2, etc. keys as standard function keys" is on; Home, End, PageUp and PageDown are Fn with the arrows.`; the Repository group's note `On a dashboard, Fetch and Pull fetch and pull every repository of its workspace.`; each row's automation name `{label}: {key text}` | 6.11, 7.6 |

### 14.9 The welcome page and the tab strip

| Where | Texts | Section |
|---|---|---|
| Heading | `Workspaces` | 13.1 |
| Button | `New workspace…`; tooltip `Create a workspace: repositories you check and update together`, or the read-only reason while disabled | 13.1 |
| Empty list | `No workspaces` | 13.1 |
| Item | the workspace's name; `{N} repositories` / `1 repository` / `No repositories`; tooltip its folder names in order joined by `, `, at most 8, then `, …`, or `No repositories yet` | 13.1 |
| Notices | `workspaces.json could not be read, so it was set aside as {file}.` (`{file}` `workspaces.unreadable.json`, else the first free of `workspaces.unreadable-2.json`, `workspaces.unreadable-3.json`, …); `workspaces.json could not be read, so changes to workspaces are not saved this session.`; `workspaces.json was made by a newer version of VisualCommit, so workspaces cannot be changed in this version.`; `workspaces.json could not be saved, so changes to workspaces are lost when VisualCommit closes.` (in the warning tone, also on the dashboard, 2.5) | 1.3, 13.1, 2.5 |
| The page in reading order | `No repository open`, `Open, clone or init a repository to see its history.`, `Open`, `Clone`, `Init`, `Workspaces`, `New workspace…`, `No workspaces`, `Recent repositories`, `No recent repositories` | 13.1 |
| Error lines (`WelcomeError`, `CloneError`) and `ScanFolderText` | unchanged texts, drawn by `PathTextBlock`: a rooted path breaks only after a separator | 13.2 |
| A dashboard's tab | the workspace's name as title, tooltip and automation name; its `ItemStatus` `Workspace` | 13.4 |
| "+" (`AddRepoButton`) | automation name `Open a new tab` (was `Add repository`); its tooltip 14.11's | 2.1, 13.4 |
| A tab opening a worktree whose closed tab's or released summary's queue still runs | `Waiting for {title} to finish…` (`TabWaitingText`, secondary, centred in the graph area) | 3.3 |

### 14.10 Menus' new items

| Menu | Items | Section |
|---|---|---|
| A welcome page's workspace (`WorkspaceMenus.ForWelcomeItem`) | header `{name}`; `Open`; separator; `Rename workspace…`; `Delete workspace…` (destructive) | 13.1 |
| The dashboard header's `…` (`DashboardMenus.ForHeader`) | header `{workspace}`; `Add open tabs`; separator; `Rename workspace…`; `Delete workspace…` (destructive) | 2.5 |
| A dashboard row (`DashboardMenus.ForRow`) | header `{name}` (tooltip its folder); `Open`; separator; `Fetch`; `Pull`; `Cancel {title}` (while one runs); `Error details…` (while a result is kept); separator; `Show in folder`; `Copy path`; separator; `Move up`; `Move down`; `Locate…` (for `Folder not found`, `Not a repository` and `Git could not open it`); `Remove from workspace` | 2.7, 1.10 |
| Every file menu (`RepositoryMenus.FileItems`), only while an editor is chosen | `Open in editor` after `Open file` | 12.1 |
| `ForRemoteBranch` under a renaming refspec | `Delete {y} on {r}…`; for a ref that is not a branch the deletion keeps today's label, disabled R4, and the checkout stays enabled (10.6); R1 and R3 disable the checkout, R1 to R3 the deletion; under R2 the checkout makes the branch without an upstream (D201) | 10.4 #6 |
| `ForDrop` onto a renamed remote branch | `Push {A} to {T}`, `Push {A} to {T} and track it` (labels unchanged), disabled R1 to R4 where they apply | 10.4 #5 |
| Keyed items (6.7) | no new label; the key's text at the right (14.12) | 6.7 |

### 14.11 Tooltips and help texts

The rule (6.8): an enabled button whose command has a key says `{what it does} ({key})`; a disabled one
keeps its reason alone, and one without a reason has none; the help text is the tooltip.

| Control | Enabled | Disabled | Section |
|---|---|---|---|
| `FetchButton` | `Fetch all remotes ({key})`; on a dashboard `Fetch every repository of {workspace} ({key})` | its reason (14.5) | 6.8, 4.10 |
| `PullButton` | `Pull from {up} ({key})` (`Pull from upstream/pr/7 (Ctrl+Shift+L)` for a branch that tracks `refs/pull/7/head` on `upstream`, GD15); on a dashboard `Pull every repository of {workspace} ({key})` | its reason | 6.8, 4.10, 10.4 |
| `PushButton` | `Push {b} to {up} ({key})`; without a remote upstream `Push {b} to a remote ({key})` | its reason | 6.8 |
| `BranchButton`, `StashButton`, `PopButton` | `Create a branch at HEAD ({key})`, `Stash your changes ({key})`, `Pop the newest stash ({key})` | their reasons | 6.8 |
| `UndoButton`, `RedoButton` | `Undo {text} ({key})`, `Redo {text} ({key})` | phase 5 design 7.4's reasons | 6.8 |
| `SearchButton` | `Search commits ({key})` (unchanged) | its reason | 6.8 |
| `SettingsButton` | `Open settings ({key})` | never disabled | 6.8, 7.2 |
| `ThemeSwitch` (no key) | `Switch to the light theme` / `Switch to the dark theme` (unchanged, now also its help text); while System is chosen `Switch to the light theme (stops following the system)` / `Switch to the dark theme (stops following the system)` | — | 11.6 |
| A tab's `CloseTabButton` | on the active tab `Close this tab ({key})`; on another tab `Close this tab`, with no `AcceleratorKey` (Mod+W closes the active tab only) | — | 6.8, 6.5 |
| `AddRepoButton` ("+") | `Open, clone or init a repository ({key})` (New tab's key) | — | 6.8 |
| `ActivityLogToggle` | `Show every git command the app has run ({key})`, `Hide the activity log ({key})`; on an empty tab, where it is disabled, the first text with its key | on a dashboard 2.9's reasons (14.5) | 6.8, 2.9 |
| `OpenRepoButton`, `CloneRepoButton`, `InitRepoButton` | `Open a repository on this computer ({key})`, `Clone a repository from a URL ({key})`, `Create a new repository in a folder ({key})` | — | 6.8 |
| The dashboard header's buttons | 14.3; Fetch all and Pull all as the toolbar's, with the key; Refresh `Read the repositories of {workspace} again ({key})` | 14.5 | 2.5, 4.10 |
| A row's `Cancel` | `Stop git`; `Stop git and the merge tool` while the merge tool runs (the status's `CancelToolTip`) | — | 2.6, 4.5 |
| The stage panel's `CommitButton` | `{its text} ({key})`: `Commit 1 file (Ctrl+Enter)`, `Continue rebase (Ctrl+Enter)` | its reason as today, or none | 6.8 |
| The resolver's `ResolverPreviousConflict`, `ResolverNextConflict`, `ResolverPreviousFile`, `ResolverNextFile`, `ResolverCloseButton` | `Previous conflict ({key})`, `Next conflict ({key})`, `Previous file ({key})`, `Next file ({key})` (Alt+Up, Alt+Down, Alt+Left, Alt+Right; Option on macOS), `Close (Esc)` | none, as today | 6.8 |
| The interactive list's `RebaseCancelButton` | `Cancel (Esc)` | — | 6.8 |
| The search bar's `SearchPrevious`, `SearchNext` | `Previous match (Shift+Enter, {key})`, `Next match (Enter, {key})`: `Previous match (Shift+Enter, Shift+F3)`, `Next match (Enter, F3)`; on macOS `Cmd+Shift+G`, `Cmd+G` | as today | 6.8 |

### 14.12 Key texts

One formatter (6.4): words, not symbols, also on macOS, joined by `+`: the command modifier (`Ctrl`,
`Cmd`), the Control key where it is not the command modifier (`Ctrl`), `Shift`, `Alt` (`Option` on
macOS), then the key by its `ShortcutKey` name: a letter, the digit alone, `F1` to `F12`, `Enter`, `Esc`,
`Tab`, `,`, `PageUp`, `PageDown`, `Up`, `Down`, `Left`, `Right`, `Home`, `End`, `Delete`, `Backspace`,
`Space`, `Menu`; on macOS Backspace is written `Delete` and Delete `Forward Delete`, Apple's words
(`Cmd+Delete` removes a dashboard row, 2.7). Several keys are joined by `, `, the shown key first (`F5, Ctrl+R`; on macOS `Cmd+R, F5`,
`Cmd+Shift+Z, Cmd+Y`, `Cmd+G, F3`); `GoToTab`'s run reads `Ctrl+1 to Ctrl+8`. Examples: `Ctrl+Shift+P`,
`Cmd+Shift+P`, `Ctrl+,`, `Cmd+,`, `Ctrl+Tab` and `Ctrl+PageDown` on both, `Alt+Up` / `Option+Up`,
`Shift+F10`, `F6`. The same text in the palette, the menus, the tooltips, the settings list and UI
Automation's `AcceleratorKey`; expectations write it as literals (6.12).

### 14.13 Loading and progress states

`Reading…` (a row's first read, 2.6); `Looking for repositories in {folder}…` with `Searched {n}
folders, found {k} repositories`, no progress bar (1.9); `Reading git's configuration…` (7.4); `Looking
for editors…` (7.5, 12.3); `Looking for the merge tools git knows…` (8.10; only until a list is there,
which is the first lookup on a machine or after git changed); `Saving…` and `Waiting for {title} to
finish…` (7.8); `Waiting for {title} to finish…` also in a tab opening a worktree whose closed tab's or
released summary's queue still runs (`TabWaitingText`, secondary, centred in the graph area; `{title}`
that queue's running operation's, 3.3); the dashboard header's determinate 80×4 bar, `100 × {k} / {n}` rounded down,
while a run runs (2.5, 4.9). Rows have no bar (2.6).

### 14.14 Log lines (not on screen)

| Line | Level | Section |
|---|---|---|
| `Opened the repository at …` for each candidate the scan confirms, as a tab's opening logs it | Info | 1.8 |
| `{workspace}: read 1 repository in {ms} ms` / `{workspace}: read {n} repositories in {ms} ms` | Info | 2.10 |
| `{name}: summary read in {ms} ms` | Debug | 3.1, 2.10 |
| `{workspace}: Fetch all: started for {n} repositories, {s} skipped`; `{workspace}: Fetch all: {result}` (`Pull all` likewise) | Info | 4.3 |
| `{repo}: Pull: refused before the fetch, {problem}` (a pull setting git would refuse, read before the fetch; WP-B's) | Info | 8.9 |
| The menu layer's line, named by the dashboard on a dashboard tab (`Web: Context menu opened: web-app by pointer`) | Debug | 2.7 |
| `{repo}: Command palette opened`; `{repo}: Command palette ran: {label}`, with ` ({detail})` when there is a detail; the `{repo}: ` prefix only while the active tab has a repository | Debug | 5.9 |
| `VISUALCOMMIT_AUTO_FETCH_MINUTES={value} overrides the auto-fetch setting ({minutes} minutes).` | Info | 7.3 |
| `Theme: Dark.` / `Theme: Light.` / `Theme: System (the system's theme is {dark\|light}).` at start | Info | 11.5 |
| `Theme changed to {theme}.` (`Theme changed to System.`), written by `UseTheme` | Info | 11.6 |
| `The system's theme changed to {light\|dark}; the app follows it.` / `The system's theme changed to {light\|dark}; the app keeps the {dark\|light} theme.` | Info | 11.4 |
| `Open in editor: {command}`; `{Name} moved to {program}; the editor setting follows it.` | Info | 12.1, 12.3 |
| `Skipped a fetch line of the remote {name} that git refuses or that names no source: {value}`, only for a line git accepts that the mapping leaves out (an empty source), once per repository object | Warning | 10.1 |
| Real-window `run.txt`: `S15 (System at start): failed. High contrast is on, and Avalonia follows its scheme: turn it off and run the pass again.` (the method then fails) | — | 11.8 |

---

## 15. UI inventory: menus, dialogs, colours, icons, automation ids

### 15.1 The menus as phase 6 changes them

Phase 3's rule stands (D77, D92): what cannot apply is left out, what can be explained is disabled with
its reason. One exception: Open in editor is left out, not disabled, while no editor is chosen (12.1,
D226), since a disabled item would add a row to every earlier file menu, unlike Open in merge tool,
which is disabled with its reason (phase 4 design 11.9) and shows only on a conflicted file; the
palette's `Choose an editor…` (5.4) keeps it findable. `MenuItem` gains the init property `AppCommand? Command`, set only on an item that runs the
keyed command on the same object, through an optional `command` argument of `RepositoryMenus.Item(…)`
(6.7). `MenuLayer.CreateItem` draws a keyed item with a header template made per item: the label's
`TrimmedTextBlock` and, 24 to its right, the key (`TextBlock.vcMenuKey`, right-aligned); heights stay 26,
a menu is as wide as its widest item, 200 to 520, a label cut at 520 keeps its key whole.

| Builder | Change | Section |
|---|---|---|
| `ForEmptyGraphArea` | keys: `Fetch all remotes` Fetch, `Pull` Pull, `Push` Push, `Create branch…` CreateBranch, `Stash changes…` Stash, `Show reflog` ShowReflog | 6.7 |
| `ForWorkingChanges` | keys: `Stage all`, `Unstage all`, `Stash changes…`, `Create branch here…` (CreateBranch), `Show reflog` | 6.7 |
| `ForSection` | keys: Local branches `Create branch…`, `Show reflog`; Remotes `Fetch all remotes`; Stashes `Stash changes…` | 6.7 |
| `ForLocalBranch` | keys on the checked-out branch only: `Pull from {up}` Pull, `Push {b} to {up}` and `Push {b}…` Push; "Set upstream…" / "Change upstream…" leave out refs that map ambiguously or to no branch | 6.7, 10.4 #11 |
| `ForStash` | key on `stash@{0}` only: `Pop stash` Pop | 6.7 |
| `ForDiff` (through `FileHistoryItems`) | keys: `File history` FileHistoryOfDiff, `Blame` BlameOfDiff | 6.7 |
| `ConflictMenus.ForConflictResult`, `.ForConflictPane` | the editor's text gestures shown: `Undo`, `Redo`, `Cut`, `Copy`, `Paste`, `Select all`; a pane's `Copy` | 6.7 |
| `InteractiveRebaseViewModel.MenuFor` | single-letter keys `P`, `R`, `S`, `F`, `D` | 6.7 |
| `ForRemoteBranch` | "Check out {T}" and "Delete {y} on {r}…" by the mapping, R1 to R4; R2's checkout enabled, making the branch without an upstream (D201) | 10.4 #6 |
| `ForDrop` | the two pushes by the mapping, R1 to R4; `DropFacts` loses `RemoteMapsBranchesDifferently` | 10.4 #5, 10.7 |
| `RepositoryMenus.Pages.ForcePushRestore` | the fallback by `Refs.SourceOf` | 10.4 #12 |
| `FileItems` (every file menu) | `Open in editor` after `Open file`, only while `RepositoryMenuContext.Editor` is set | 12.1 |
| New `WorkspaceMenus.ForWelcomeItem`, `DashboardMenus.ForHeader`, `DashboardMenus.ForRow` | 14.10 | 13.1, 2.5, 2.7 |

Not keyed on purpose: another branch's pull or push, `Pull from {up} (rebase)`, a file row's File
history and Blame, `Discard all changes…` (6.7). The palette reads these same builders (5.5).
`ToolbarMenuState.Of` reads the toolbar's `XDisabledReason` properties instead of its tooltips (6.8).
No menu item but these changes; the push guard is the operations' own: `PushToAsync` and
`ForcePushAsync` refuse, before git, a push that would give a tracking ref a second source (10.4,
10.5's `PushWouldStopFetch`), whichever menu, button or dialog started it.

### 15.2 Dialogs

| Dialog | View model / view | Layer, size, stacking | Section |
|---|---|---|---|
| New and rename workspace | `WorkspaceNameDialogViewModel : NameDialogViewModel` (factories `Create`, `Rename`), drawn by `NameDialogView` | the dialog layer, the existing card | 1.6 |
| Delete a workspace | `IDialogService.ConfirmAsync`, destructive | the dialog layer | 1.6 |
| Add repositories (the scan's checklist) | `AddRepositoriesDialogViewModel : DialogViewModel<IReadOnlyList<string>?>`, `Views/Dialogs/AddRepositoriesDialogView.axaml` | the wide card, 460 wide (418 inside), about 400 high with eight rows, the list `MaxHeight` 232 | 1.9 |
| The command palette | `ViewModels/Commands/CommandPaletteViewModel : DialogViewModel<PaletteEntry?>`, `Views/Commands/CommandPaletteView : DialogView` | the dialog layer without dimming (`Panel.paletteBackdrop`), a 600-wide card at the top centre, `Margin="20,96,20,20"`, 84 to 392 high; prompts stack over it (D104) | 5.2, 5.3 |
| Settings | `SettingsDialogViewModel : DialogViewModel<bool>` with `GeneralSettingsViewModel`, `GitSettingsViewModel`, `ToolSettingsViewModel`; `Views/Dialogs/SettingsDialogView.axaml` | `Border.dialogCard.settings`: `MaxWidth` 860, `MaxHeight` 680, `Margin` 40, `Padding` 20; no combo box, no menu over it; the Pick and error dialogs stack over it; first focus the shown section's nav button (`DialogView.FocusesTextBoxOnOpening` false) | 7.1 |
| Pick (editor, merge tool) | `PickDialogViewModel` gains `PickOption(string Text, string? Detail = null)` and a second constructor; `PickDialogView`'s item gets a detail column | over the settings screen; phase 3's string constructor and its pictures unchanged but for the toolbar box | 7.7 |
| Errors of the workspace flows, the editor, the global settings writes | the C11 error dialog; `ErrorDialogViewModel`'s repository nullable (`ErrorRepository` hidden when null) | as every error dialog | 1.7, 1.10, 8.3, 12.7 |
| A bulk run's kept result | the identical C11 dialog (or the lock-file dialog), opened later by `RepositoryOperations.ShowDeferredAsync` | as every error dialog | 4.6 |
| Every dialog with a box of several lines | `DialogView` takes Mod+Enter on its tunnelling pass and runs `ConfirmButton` | — | 6.10 |

No other dialog changes; the workspace's deletion is the only new question.

### 15.3 Colours, tokens and styles

| Token (brush key) | Dark | Light | Used for | Section |
|---|---|---|---|---|
| `VcDangerTextBrush` (new) | `#FFFFFF` (D198; dark `#10121A` left to phase 8, 11.11) | `#FFFFFF` | text on a danger background: `VcDangerButton`'s label, replacing its fixed `#FFFFFF` | 11.11 |
| `VcWarningBrush` | unchanged | `#B7791F` → `#895A17` | text at 4.5:1 on window, panel, chrome, hover, selection, banner, marker | 11.10 |
| `VcSuccessBrush` | unchanged | `#1E8E5A` → `#187248` | as above | 11.10 |
| `VcDangerBrush` | unchanged | `#C93A46` → `#B7323D` | as above; the light danger button's background (its white label 5.94:1) | 11.10 |
| `VcLane1Brush`, `VcLane2Brush`, `VcLane4Brush`, `VcLane7Brush` (D198; amends D48) | unchanged | `#1E8E5A` → `#1C8655`, `#B7791F` → `#A16A1B`, `#1F86B0` → `#1D7EA6`, `#5F8A1E` → `#59811C` | white labels at 4.5:1 | 11.13 |
| Fluent's `ToolTipBackground`, `ToolTipForeground`, `ToolTipBorderBrush`, set per variant in `Tokens.axaml` | `#1A1D24`, `#E4E7EC`, `#323845` | `#F5F6F8`, `#1B1F27`, `#D2D6DE` | tooltips in the panel's colours (copies of `VcPanelBackgroundBrush`, `VcTextPrimaryBrush`, `VcBorderBrush`) | 11.12 |

| Style | Where | Section |
|---|---|---|
| `Panel.paletteBackdrop` (`Transparent`), `Border.paletteCard` (`VcPanelBackgroundBrush`, 1-px `VcBorderBrush`, corner 8, padding 8, `VcMenuShadow`, width 600, cycling Tab) | `Theme/Dialogs.axaml` | 5.3 |
| The palette's rows (`CommandPaletteItem`: 28 high, `Padding="10,0"`, corner 4, not focusable, the highlight on `VcSelectionBrush`) | `CommandPaletteView.axaml` | 5.3 |
| `TextBlock.vcMenuKey` (`VcTextSecondaryBrush`, 12.5; `VcTextDisabledBrush` under `MenuItem:disabled`); `ContentPresenter#PART_HeaderPresenter` stretched; `PART_InputGestureText` stays hidden | `Theme/Menus.axaml` | 6.7 |
| `Border.dialogCard.settings`; `TextBlock.dialogWarning` (`VcWarningBrush`, `dialogError`'s layout); `TextBlock.settingsLabel` | `Theme/Dialogs.axaml` | 7.1 |
| `VcFlatButton.selected` reused for the settings list and the scope switch | existing | 7.1, 7.4 |
| `Button.dashboard` (`TabButton` padding 32,0,8,0); `Border.repoTab:not(.active) c\|Icon#TabIcon` (secondary) | `RepoTabsView.axaml` | 13.4 |
| `ListBox.dashboardList`, a copy of phase 5's `pageList`, so the reflog and backups pages stay as they are | `DashboardView.axaml` | 2.6 |
| `VcDangerButton`'s `Foreground` → `{DynamicResource VcDangerTextBrush}` | `Theme/Controls.axaml:269-273` | 11.11 |
| Attached `KeyRegion.Role` (`Left`, `GraphArea`, `Right`, `ActivityLog`) and `KeyRegion.IsMain` | `Views/KeyRegions.cs` | 6.6 |
| `DragLayer` redraws on `ActualThemeVariantChanged` | `Views/Shell/DragLayer.cs` | 11.14 |

Scroll bars stay Fluent's (11.12). No other colour token is new.

### 15.4 Icons

Lucide only (ISC), added to `Theme/Icons.axaml` as path strings. `THIRD-PARTY-NOTICES.md` carries
Lucide's licence for every icon there and names those that derive from Feather, as phase 5 named
`eye-off` and `crosshair`; WP0 confirms each new icon's origin in Lucide's metadata and names it there
only if it has one.

| Key | Lucide | Used by | Feather list | Section |
|---|---|---|---|---|
| `IconWorkspace` | `layout-grid` (13.4 gives the path) | a dashboard's tab (12×12), the dashboard header (14×14), the status bar's branch part on a dashboard (`StatusWorkspaceIcon`) | `layout-grid (Feather's grid)` | 13.4, 2.5, 2.8 |
| `IconSettings` | `settings` | the toolbar's `SettingsButton` | `settings` | 7.2 |
| `IconRefresh` | `rotate-cw` (not `refresh-cw`, which is `IconFetch`) | the dashboard header's Refresh | `rotate-cw` | 2.5 |
| `IconMore` | `ellipsis` | `DashboardMore` | `ellipsis (Feather's more-horizontal)` | 2.5 |

Reused: `IconPlus`, `IconFetch`, `IconPull`, `IconArrowUp`, `IconArrowDown`, `IconBranch`, `IconSun`,
`IconMoon`.

### 15.5 Automation ids and names

Every view root added is a control element named by its title, as phase 4's and 5's are.

| Area | Ids (name) | Section |
|---|---|---|
| Welcome page | `WorkspacesHeading`; `NewWorkspaceButton` (`New workspace`); `WorkspacesNotice`; `NoWorkspacesText`; `WorkspaceList`; `WorkspaceItem` (the workspace) | 13.1 |
| Tab strip | `TabButton`'s id bound to `RepoTabViewModel.TabAutomationId`: `DashboardTab` on a dashboard, `RepoTab` otherwise; `TabIcon`; a dashboard tab's `ItemStatus` `Workspace`; `AddRepoButton`'s name `Open a new tab` (was `Add repository`, 2.1, 13.4); `TabWaitingText` (3.3) | 13.4, 2.1, 3.3 |
| Scan dialog | `AddRepositoriesDialog`, `AddRepositoriesTitle`, `ScanFolderText`, `ScanProgressText`, `FoundList`, `FoundRepository` (the repository), `FoundRepositoryCheck`, `ScanStoppedText`, `ScanFailedText`, `CancelButton`, `ConfirmButton` | 1.9 |
| Dashboard | `Dashboard` (root, the workspace), `DashboardHeader`, `DashboardTitle`, `DashboardCount`, `DashboardAddRepository`, `DashboardFetchAll`, `DashboardPullAll`, `DashboardRefresh`, `DashboardMore` (`More`), `DashboardNotice`, `DashboardBulkText`, `DashboardBulkProgress`, `DashboardCancelAll` (`Cancel all`), `DashboardColumnHeaders`, `DashboardList`, `DashboardRow` (the repository), `DashboardRowName`, `DashboardRowFolder`, `DashboardRowBranch`, `DashboardRowAheadBehind`, `DashboardRowChanges`, `DashboardRowState`, `DashboardRowOperation`, `DashboardRowCancel` (`Cancel`), `DashboardRowDetails` (`Details`), `DashboardEmpty`, `DashboardEmptyAddRepository`, `DashboardEmptyAddOpenTabs`; the status bar's `StatusWorkspaceIcon` | 2.5, 2.6, 2.8, 4.9 |
| Palette | `CommandPalette` (pane, `Command palette`), `CommandPaletteCard` (no control element), `CommandPaletteBox` (edit, `Search commands`), `CommandPaletteList` (list, `Commands`), `CommandPaletteItem` (list item: `Name` the label, `ItemStatus` the detail, `HelpText` a disabled entry's reason, `AcceleratorKey` the key, selected while highlighted), `CommandPaletteItemLabel`, `CommandPaletteItemDetail`, `CommandPaletteItemKey`, `CommandPaletteEmpty` (`No matches`) | 5.11 |
| Menus | a keyed item's key: id `ContextMenuItemKey`, `Name="MenuItemKey"`; the item's `AcceleratorKey` the key text; its `Header` stays the label | 6.7 |
| Toolbar | `SettingsButton` (`ToolbarButton`, label `Settings`, `IconSettings`, the first of the right-hand group, before `SearchButton`) | 7.2 |
| Settings, frame | `SettingsDialog` (root, named by its title), `SettingsTitle`, `SettingsNav`, `SettingsNavGeneral`, `SettingsNavGit`, `SettingsNavTools`, `SettingsNavShortcuts`, `SettingsNavError`, `SettingsSection`, `SettingsSaving`, `CancelButton`, `ConfirmButton` | 7.1 |
| Settings, General | `SettingsThemeDark`, `SettingsThemeLight`, `SettingsThemeSystem`, `AutoFetchCheckBox`, `AutoFetchMinutesBox`, `AutoFetchError`, `KeepIgnoredCheckBox`, `StashUntrackedCheckBox`, `RunnableIntro`, `RunnableList`, `RunnablePathExt`, `RunnableExtraBox`, `RunnableError`, `RunnableUnix` | 7.3 |
| Settings, Git | `GitScope`, `GitScopeGlobal`, `GitScopeRepository`, `GitScopeCaption`, `GitReading`, `GitReadError`, `GitUnavailable`, `UserNameBox`, `UserNameOrigin`, `UserNameError`, `UserEmailBox`, `UserEmailOrigin`, `UserEmailError`, `UserEmailWarning`, `IdentityInEffect`, `IdentityNotices`, `PullNotSet`, `PullMerge`, `PullMergeCommit`, `PullRebase`, `PullRebaseMerges`, `PullFastForwardOnly`, `PullOther`, `PullNotices`, `PullInEffect`, `BranchRebaseNotice`, `BranchRebaseButton`, `MergeToolValue`, `MergeToolChooseButton`, `MergeToolNote`, `MergeToolStatus`, `MergeToolNotices` | 7.4 |
| Settings, Tools | `EditorValue`, `EditorProgram`, `EditorChooseButton`, `EditorNote`, `EditorStatus`, `EditorProgramBox`, `EditorBrowseButton`, `EditorArgumentsBox`, `EditorArgumentsHint`, `EditorError` | 7.5 |
| Settings, Keyboard shortcuts | `ShortcutsIntro`, `ShortcutList`, `ShortcutGroup`, `ShortcutRow` (`{label}: {key text}`) | 7.6 |
| Pick dialog | `PickItem` keeps its id and name (the text); its help text is the option's detail | 7.7 |
| Help texts added (= the tooltip) | every toolbar button bound to its `XToolTip` (`FetchButton`, `PullButton`, `PushButton`, `BranchButton`, `StashButton`, `PopButton`; Undo, Redo and Search as today), `SettingsButton`, `ThemeSwitch`, `CloseTabButton` (`CloseToolTip`), `AddRepoButton`, `OpenRepoButton`, `CloneRepoButton`, `InitRepoButton`, `CommitButton`, `ResolverPreviousConflict`, `ResolverNextConflict`, `ResolverPreviousFile`, `ResolverNextFile`, `ResolverCloseButton`, `RebaseCancelButton`; `SearchPrevious` and `SearchNext` have help texts already, which become their new texts | 6.8, 11.6 |
| `AcceleratorKey` added | every button of 14.11 with a key, enabled or not (`MainWindowViewModel.Keys`, a `ShellKeys` record, and `ShellToolTips`' `{name}Key` texts), but another tab's `CloseTabButton` (only the active tab's shows Mod+W, 6.8): the toolbar's, `AddRepoButton`, the welcome page's three, `CommitButton`, the resolver's five, `RebaseCancelButton`, `SearchPrevious`, `SearchNext`; the status bar's `ActivityLogToggle`, every keyed menu item, every palette row with a key | 6.7, 6.8, 5.11 |

Left to phase 8's accessibility pass (16.2): the icon buttons that have no automation name today, of
which only the page close buttons set one (`ResolverPreviousFile`, `ResolverNextFile`,
`ResolverCloseButton`, `ResolverPreviousConflict`, `ResolverNextConflict`, `CloseDiffButton`,
`ActivityLogClose`, `SearchPrevious`, `SearchNext`, `SearchClose`, `CompareSwap`, `CompareClose`,
`RefHiddenToggle`, `RefSoloToggle`, `DismissRestoreButton`, `RowStageButton`, `RowDiscardButton`,
`RowUnstageButton`); phase 6's new icon button `DashboardMore` is named `More`.

---

## 16. What changes for earlier phases

### 16.1 Their checks, tests and pictures, changed on purpose

Each change goes into phase 6's report under "Changes to expected results", with the tests changed in
the commit that changes the behaviour (plan.md's gate). "RW" is the real-window twin.

**Earlier pictures are compared by region**, against reference pictures made on `master` before the gate
(17.7), as phase 5's gate did. The Settings button (7.2, D197) changes every window picture of
phases 0 to 5 in one toolbar box, so `phase-5-pictures.sha256` names them all as changed and says nothing
more; `compare-pictures.py` places each change in its region, and a change outside the regions this
table names for a picture is a defect. A change in an earlier picture's tab-strip region is a defect too
(13.3).

| Change | Where it shows (phase, check, picture) | Tests and expectations that pin today's state | Section |
|---|---|---|---|
| The toolbar's `Settings` button left of Search | every window picture of phases 0 to 5, scripted and RW, in one box of about 56×52 in the toolbar region left of Search; Search and Theme keep their places | phase 0 `ShellExpectations.cs`: `ToolbarLabels` (`:31`) gains `Settings` before `Search`, the enabled list (`:96`) becomes `Settings`, `Theme`, the messages `:97-98`; `Phase0RealWindowPass.cs:134-141` stays (Theme within 20 of the right edge, `:141`); phase 5's `PartA.ToolbarButtons` and `EmptyTabToolbar` stay; `ShellTests.cs:99` stays (Settings is enabled) | 7.2 |
| The welcome page's Workspaces section (the page moves up by half the section's 68 px) | phase 0: `01-shell-dark-1100x700`, `02-shell-dark-1920x1080`, `03-shell-light-1100x700`, `04-shell-light-1920x1080`, `05a` to `05c`, `06a` to `06d`, `07a` to `07c`; RW `A-first-start-dark`, `B-after-theme-click-light`, `C-restarted-light`. Phase 1: `10a-welcome-page`, `10b-second-tab`, `10c-second-tab-closed`, `11a-not-a-repository`, `11c-recent-in-new-tab`, `16a-before-closing`, `16b-restarted`, `17a-wide-panels`, `17b-narrow-window`, `17c-wide-again`; RW `11a` (by eye), `16b`. Phase 5: `a1a-empty-tab`, scripted and RW. Every other picture of an empty tab the comparison names. Not changed: phase 1's `13a`, `13b`, `14b` (the clone form hides the section); `14a` moves only through 13.2's line breaking, if at all | `ShellExpectations.WelcomeTexts` (`Phase0/ShellExpectations.cs:38-42`) gains `Workspaces`, `New workspace…`, `No workspaces` after `Init`; phase 1 check 10 (`Phase1Checks.Shell.cs:61`) `WelcomeTexts[..6]` → `[..9]`; check 11 (`:123`) `recent[6..8]` → `recent[9..11]`; `Phase5Expectations.A.WelcomeTexts` (`:210-214`) and A1 step 1 in the report; `Phase5RealWindowPass.A.AssertEmptyTab` (`:559`) leaves out `New workspace…`; perhaps `Phase0RealWindowPass.AssertShell` (`:119-125`) | 13.1 |
| A long folder breaks only after a separator (`PathTextBlock`) | phase 1 `11a-not-a-repository` (its lines become `C:\Users\…\Temp\VisualCommit.Tests\` and `plain-<12 hex> is not a git repository.`), perhaps `14a-clone-failed`; the phase 1 report's note on check 11's wrap is resolved | phase 1 check 11 gains an assertion on the drawn lines (`DrawnLines.Of`, shared with `PathTextBlockTests`) | 13.2 |
| Tooltips with keys; help texts on the shell's buttons | phase 3 part A's `AssertToolbar` (`Phase3Checks.A.Helpers.cs:293-323`): an enabled button's tooltip and help text are 6.8's (check 23's six methods and every caller); its disabled-tooltip pictures `23b`, `23h`, `23j`, `23k`, `23l`, `23m` stay. Phase 5: part A `OpenToolbar`, `CommittedToolbar` (Fetch to Stash), `UndoCommit` + ` (Ctrl+Z)`, `ShowActivityLog` / `HideActivityLog` + ` (Ctrl+J)` (`Phase5Expectations.A.cs:158-185`), check A1 scripted and RW (`Phase5RealWindowPass.cs:292-318`), A4's RW help texts of Undo and Redo; part U `UndoRedoButtons.CanUndo` / `CanRedo` (`ShellDriver.Phase5.Undo.cs:109-119`) + ` (Ctrl+Z)` / ` (Ctrl+Y)` in every U check; the longer tooltip in `u16a` (scripted and RW) and `a1f` (scripted; A1's tooltips are scripted only), while `u23a` and `u23c` keep their reasons (a disabled button keeps its reason, 6.8) and `u23b` shows no tooltip; `Phase5RealWindowPass.P.cs:125` | `ToolbarTests.cs:69` (`StashToolTip`) and `:231` (`PullToolTip`) become the enabled texts (the reasons at `:75`, `:135`, `:147`, `:157`, `:212`, `:222` stay); `ActivityLogViewModelTests.cs:377`, `:383`, `:387`; `StatusBarTests.cs:47`; `ToolbarViewModel.SearchGestureText` reads `KeyTexts` (`Search commits (Ctrl+F)` unchanged) | 6.8 |
| Keys in the tooltips of the commit button, the resolver's buttons, the interactive list's Cancel and the search bar's match buttons (D199) | no picture: phase 4's `c12c-stage-panel` and every earlier picture of the commit button's tooltip shows a disabled button's reason, which is unchanged; no earlier picture shows the resolver's, the list's or the search bar's match tooltips (phase 5's `a1d-search-tooltip` is the toolbar's Search, unchanged) | the enabled commit button's tooltip, null today, becomes `{its text} (Ctrl+Enter)`: `WorkingChangesInProgressTests.cs:63` (`Commit merge (Ctrl+Enter)`), `:108` (`Continue cherry-pick (Ctrl+Enter)`) and `:86` where the button is enabled; phase 3 `Phase3Checks.C.Banners.cs:245` (`AssertTip(commit, null)` on an enabled `Continue cherry-pick`); phase 4 `C14To17.AssertCommitButton` (`Phase4Checks.C.EditingSupport.cs:259-265`), whose enabled callers gain the text: `Phase4Checks.C.Editing.cs:214`, `Phase4Checks.C.Kinds.cs:147`, `Phase4Checks.C.MergeTool.cs:122`. The search bar's `Next match (Enter)` becomes `Next match (Enter, F3)` and `Previous match (Shift+Enter)` `Previous match (Shift+Enter, Shift+F3)`: `SearchBarTests.cs:57-58`, `Phase5Expectations.A.cs:582`, `:584` (read at `Phase5Checks.A.Keys.cs:251` and as help text by `Phase5RealWindowPass.A.cs:657`) and `Phase5Expectations.F.cs:185-186` (`Phase5Checks.F.Helpers.cs:161`). The real-window passes read only the commit button's name (`Phase5RealWindowPass.U.cs:90`) and stay as they are | 6.8 |
| "+"'s automation name `Open a new tab` (was `Add repository`); a dashboard tab's `ItemStatus` `Workspace` | no picture (UI Automation only) | none: no test reads "+"'s name | 2.1, 13.4 |
| A tab opened on a worktree whose closed tab's or released summary's queue still runs waits for it (`Waiting for {title} to finish…`, then opens; 3.3) | no earlier picture: a tab reopened while its merge runs, which no earlier check covers | none pins today's behaviour; `RepoTabsTests` gains the case (3.9) | 3.3 |
| Menus with keyed items get wider where a keyed item is their widest | phase 3: `03f-empty-area-menu`, `23i-unborn-empty-area-menu`, `04l-working-row-menu`, `04m-working-row-menu-all-staged`, `18b1-current-branch-menu` and every menu of the checked-out branch, `03b-stash-menu` (on `stash@{0}`), the sections' and the diff's menus (`04f`, `04g`, `04h`, `04k`, `26d`); phase 4: `c14b-result-menu`, `c14c-pane-menu`, the interactive list's row menus; phase 5: `b8a-section-menu`, `b5f-diff-menu` and the menus of parts B and F with keyed items; their RW twins | `RepositoryMenusTests.Window.AssertWholeLabels` (`:509-537`): a keyed item counts label + 24 + key; `Phase3Expectations.*`, `Phase4Expectations.*`, `Phase5Expectations.*` menu widths where asserted | 6.7 |
| `--no-overwrite-ignore` on every checkout and merge; the ignored-files check's reads | phase 3: check 06 (`Phase3Checks.A.Pointer.cs:37`, `:111`; `:288` matches by prefix and stays), 13 (`Phase3Checks.C.cs:83`), 14 (`Phase3Checks.C.Checkout.cs:65`, `:88`, `:110`, `:140`, `:158`, `:160`; `14b-untracked-in-the-way` gains `git check-ignore --stdin -z` (exit 1) with its summary unchanged, `14c-checkout-blocked`, `14f-clash-blocked`, `14g-reapplied-with-conflicts` in `ErrorCommand`, RW `14c`; `14b1-checkout-failed`, refused before git, unchanged), 18 (`18c`'s `git merge --no-edit --no-overwrite-ignore FETCH_HEAD`, `18b3-pull-refused`'s `git merge --ff-only --no-overwrite-ignore FETCH_HEAD`), 23 (`Phase3Checks.A.Toolbar.cs:46`); phase 4: the rebases' call logs gain 9.6's and 9.7's reads (9.6's (5) and (6) too where the range holds a merge, whatever the rebase's options); phase 5: P24 (`git switch --create topic --no-overwrite-ignore 19a90447…`, the refusals for the untracked file and the folder `notes` gain `check-ignore` with their summaries unchanged, every dialog picture's `ErrorCommand`; RW yes), P25 (the way back), P26 step 2 unchanged (a tracked block) and step 3, HEAD's own commit, `git switch --detach --no-overwrite-ignore 41f8398f…` with the same result, P27 (`git merge --no-edit --no-log -m "Merge branch 'other'" --no-overwrite-ignore refs/heads/other`, the `--diff-filter=A` read before each merge, `diff-tree --stdin` and `check-ignore` before each pick and, with `-R`, each revert; the stash step, summaries, titles and step 8's reflog entry kept), part A (`a2c-checkout-failed-opened`, A2's merge row and picture; RW `Phase5RealWindowPass.A.cs:144`, `:157`, `:259-277`), part U (the forward checkouts of U18 and U19, `MergeFfCall`, `MergeOtherCall`, `MergeFf`); every activity-log picture with "Show reads" that holds a covered operation. A longer command line can wrap in the error dialog and change its height | `Phase3Expectations.B.cs:772` (`CheckoutCommand`; the "not called" assertion at `Phase3Checks.B.cs:270` follows the new text), `Phase3Expectations.D.cs:170`, `:240`, `Phase3SupportTests.cs:75`; `Phase4Expectations.C.cs:823`, `:825`, `Phase4Expectations.A.cs:762`; `Phase5Expectations.P.cs:422`, `:424`, `:426`, `:429`, `:449`, `Phase5ExpectationTests.P.cs:201`, `:203`; `Phase5Expectations.A.cs:433`, `:460`; `Phase5Expectations.U.cs:567`, `:660`, `Phase5Expectations.U2.cs:399`, `Phase5ExpectationTests.U16-U19.cs:75`, `Phase5ExpectationTests.U2.cs:19`, `Phase5Checks.U18.cs:77`, `:230`, `:278`, `:381`, `:483`, `:493`, `Phase5Checks.U19.cs:311`; App tests `DialogTests` (347, 356, 685, 695, 865), `RepositoryOperationsCheckoutTests` (66, 167), `RepositoryOperationsProtectTests` (51, 86, 107, 243, 250, 392), `RepositoryOperationsRebaseTests` (251, 332, 362, 439), `ShellDriverCheckSupportTests` (242, 249), `UndoRecordingTests` (449), `ActivityLogViewTests` (114, 170), `RepositoryOperationsPullTests` (106), `UndoStepTests` (108); Git tests `BranchOperationTests.cs:217`, `DashBranchNameTests.cs:78`. Unchanged: the automatic stash's and undo's commands (`UndoSwitch`, `UndoDetach`), `FailureClassifierTests`, `Phase5ScenarioTests` | 9.11, 9.16 |
| Pull refused by `pull.ff=only` gets its own summary | phase 3 check 18b step 4's `ErrorSummary`, picture `18b3-pull-refused` (its summary's lines; not RW) | `Phase3Expectations.D.PullRefusedDialog` (`:236-243`); `RepositoryOperationsPullTests.Pull_refused_by_pull_ff_only_shows_gits_message_after_the_fetch_ran` (`:94-114`) | 4.8 |
| Pull reads its configuration before the fetch; "Pull from {up} (rebase)" reads `pull.ff` there and refuses a value `git pull --rebase` refuses (D231) | no picture | `RepositoryOperationsPullTests.Pull_with_a_setting_the_app_does_not_understand_says_which_and_changes_nothing` (`:115`) gains: `origin/main` not fetched, no `fetch` among the operation's calls; the chosen rebase's call logs gain `git config --get pull.ff` before the fetch (phase 4's part A pull checks assert their calls by name); `PullParityTests`, `PullRebaseParityTests`, `PullFailureParityTests` compile and stay unchanged (`PullIntegrationOptions` is optional) | 8.9 |
| Tooltips in the app's colours | the 56 pictures with a tooltip, only colours inside each tooltip's rectangle: phase 3 scripted `01c`, `02c`, `02e`, `03d`, `04m`, `09b`, `09c`, `09d`, `11a`, `11b2`, `11e`, `13l`, `23b`, `23h`, `23j`, `23k`, `23l`, `23m`, `26g`, `30f`, `30g`, RW `01c`, `09b`, `23b`; phase 4 scripted `a1a`, `a1b`, `a1h`, `b6f`, `b9b`, `b9e`, `c10b`, `c10c`, `c10d`, `c11b`, `c12c`, `c17b`, RW `a1a`, `b6f`, `c11b`; phase 5 scripted `a1c`, `a1d`, `a1f`, `b6b`, `b6f`, `b8c`, `b8d`, `b9b`, `b9d`, `b9p`, `f14b`, `f14e`, `u16a`, `u23a`, `u23c`, RW `b8d`, `u16a`. Phases 0 to 2 have none. `u16a` and `a1f` also grow by ` (Ctrl+Z)` (the keys row above) | none asserts Fluent's colours | 11.12 |
| Light warning, success and danger darker; light lanes 1, 2, 4 and 7 (D198) | warning and success, wherever an "A" or "M" letter, a warning text, the banner's line and icon or a marker line shows in light: phase 1 `04a`, `04b`, `04c`; phase 2 `02a`, `02b`, `04a` to `04f`; phase 3 `27a` to `27d`; phase 4 `d18a` to `d18d`; phase 5 `b5k`, `f10h`. Danger: phase 1 `11a` (scripted and RW), phase 2 `04c`, `04d`, phase 3 `27b`, `27d`. Lanes: phase 1 `02-graph-light-1920x1080`, `04a` to `04c`; phase 2 `02a`, `02b`; phase 3 `27a` to `27d`; phase 5 `f10h`. Phase 0's light pictures do not change from the colours | `Phase1Expectations.cs:52`, `:64-66`; `Phase3Expectations.Common.cs:111-113`, `:121`; `Phase3Checks.D.Light.cs:106`; `Phase4Expectations.Common.cs:128-130`, `:145`; `Phase4Expectations.D.cs:188-190`; `Phase5Expectations.Common.cs:132-134`, `:150`; `ConflictResultEditorTests.cs:51`; `CommitGraphControlTests.cs:998` (lanes) | 11.10, 11.13 |
| The theme service (`Setting`, `IsDark`, `Changed`, `Apply`); `UseTheme`; the Theme switch's help text | no picture; phase 0's checks 3 to 6 and RW steps A to C keep their meaning (they start Dark or with an explicit Light) | `MainWindowViewModelTests.cs:143-154` (`FakeThemes` gains `Setting`, `IsDark`, `Changed`, loses `Current`; the toggle test reads `themes.Setting`); `ShellTests.cs:86` stays true; `JsonSettingsStoreTests` (`"theme": "System"` loads) | 11.4, 11.6 |
| A disabled Search's key is consumed | no picture | `WindowKeysTests`' six tests keep passing, now through the router | 6.3 |
| Test isolation: a git environment per session, the safety net, the identity variables and `MSYSTEM` removed, `PATHEXT` fixed | no picture expected; the owner's `~/.config/git/ignore` no longer reaches the app under test (a scenario file it ignored would show untracked; none known) | `ShellTests.Git_calls_of_the_app_under_test_do_not_see_the_configuration_of_the_machine` (`:175-191`) extended; `GitIsolationTests` gains the variables (`GIT_AUTHOR_DATE` kept); `Git_calls_that_end_after_the_session_do_not_bring_its_deleted_data_folder_back` (`:247-268`) still passes | 8.12, 17.2 |
| `IOperationQueue.Cancel`, the prompt group, `MaxConnections` 16 | no picture | the fakes gain members: `RecordingQueue` (`RepositoryOperationsTests.cs:649`), `KeyRecordingQueue` (`RepositoryOperationsPushTests.cs:415`), `RecordingBroker` (`PromptDialogsTests.cs:237`, `OperationQueueTests.cs:857`); `AskPassTests.Several_prompts_at_once_in_different_scopes_are_all_answered` (`:309-334`): its comment's "(four)" becomes 16 | 4.4, 4.5 |
| The status bar's source typed `IStatusBarOperation` | no picture | `ShellDriver.Checks.cs:404-410` (`StatusTextRecorder`) takes the interface; `Phase3Checks.D.cs:190`, `:252` read `Waiting` and compile unchanged | 2.8, 4.9 |
| Refspecs: `DropFacts` loses its flag | no picture (every earlier scenario uses the identity mapping; phase 3's `07d-drop-onto-remote-branch` unchanged) | `RepositoryMenusTests.cs:798` `A_remote_that_maps_its_branches_differently_offers_no_push_by_drop` rewritten with refspecs in the snapshot; `RefReadingTests`' `team/fork/main` keeps `team/fork` | 10.3, 10.7 |
| A refs read runs `git config --null --get-regexp ^remote\.` in place of `git remote`, still five processes (10.2) | wherever an earlier picture shows a refs read's call: phase 5's `a2d-show-reads` (already varying by nature) and any activity-log row whose calls hold a refresh's refs read | no test asserts the app's `git remote` read (`tests/` runs `git remote` only as the tests' own git calls); `RefReadingTests` gains the parity with `git remote` (10.9) | 10.2 |
| A repository opens on the thread pool (`GitRepositoryProvider.OpenAsync` and `InitAsync` wrap the whole open in `Task.Run`, 2.10) | no picture | none pins the thread; the restored tabs' and the welcome page's openings keep their texts and order | 2.10 |
| A dashboard's session field | no picture | `JsonSessionStoreTests` (a dashboard's round trip); its tests compile (`TabState(path)` unchanged) | 1.11 |
| `TempRepo`'s blank git folders without `hooks/*.sample`; `CopyAsync` leaves relative URLs alone | no picture | `Phase5ScenarioTests` and every scenario test unchanged (the default run measured before and after) | 17.1 |
| The stage lists keep every row a status read did not change (`43751d0`, the phase branch's fix before WP0: `ChangedFileList.Show(…, keepFolders: true)` rebuilds with `keepRows`, `Rows.ReplaceAll(rows, ShowsSameAs)`, `ChangedFileRow.ShowsSameAs`, as the left panel does) | no picture expected: a kept row keeps its container, so only a row's hover or focus state could differ; phase 2's walk-through, run with the change just before it was committed, matched `phase-5-pictures.sha256` in 65 of its 66 pictures, `17c-restored` differing by 8 anti-aliased pixels | `StageListRefreshTests` (new with the fix) | none (a fix before WP0; 17.7) |

**By phase**, the checks those rows touch:

| Phase | Checks and pictures (16.1's rows) |
|---|---|
| 0 | every window picture (Settings button); `01` to `07c` and RW A to C (welcome section); the toolbar's labels and enabled list; checks 3 to 6 and RW A to C keep their meaning (theme) |
| 1 | every window picture; check 10 and check 11 (welcome texts, drawn lines), `10a` to `17c`, RW `11a`, `16b` (welcome section); `11a` (`PathTextBlock`, light danger); `04a` to `04c`, `02-graph-light-1920x1080` (light colours, lanes) |
| 2 | every window picture; `02a`, `02b`, `04a` to `04f` (light colours); the stage lists (`43751d0`, no picture expected) |
| 3 | every window picture; checks 06, 13, 14, 18 (and 18b's summary), 23 (command lines, tooltips); check C's enabled Continue tooltip; keyed menus `03b`, `03f`, `04f` to `04m`, `18b1`, `23i`, `26d`; 21 tooltip pictures and RW 3; `27a` to `27d` |
| 4 | every window picture; the rebases' call logs; `AssertCommitButton`'s enabled callers; keyed menus `c14b`, `c14c`, the interactive list's; 12 tooltip pictures and RW 3; `d18a` to `d18d` |
| 5 | every window picture; A1 (`a1a`; `a1f`, the longer tooltip; RW texts), A2 (`a2c`, the merge row; `a2d`'s refs read; RW), A4 (RW help texts); A's and F's match tooltips, RW A's help text of Next match; B and F keyed menus (`b5f`, `b8a`); P24 to P27; U16, U18, U19, U23 (`u16a`, the longer tooltip; Undo's and Redo's texts and command lines asserted); 15 tooltip pictures and RW 2; `b5k`, `f10h` |

**Stays as it is, region by region**, within the comparison's rule above:

- the tab-strip region of every earlier picture (13.3: each tab and "+" keep their place while the tabs fit
  at their natural widths; no earlier picture has more than 3 tabs);
- every region of a repository tab's picture that no row above names: the dashboard's bindings give every
  other tab today's values (2.4), the status bar's branch icon stays `IconBranch` (2.8), and
  `NamesRepositoryInDialogs` changes only while a dashboard tab is open (3.8);
- phase 3's Pick dialog pictures apart from the toolbar box (7.7);
- every file menu picture apart from a keyed diff menu's width (no editor is chosen in any earlier check,
  12.1);
- the reflog and backups pages (the dashboard's list style is a copy, 2.6);
- every dialog that uses `UrlTextBlock` (the error dialog's summary, the confirmation, the prompts: 13.2
  leaves them);
- the error dialog's layout for every earlier caller (its repository line stays, 8.3) and every dialog's
  first focus (`FocusesTextBoxOnOpening` is true for all of them, 7.1);
- phase 0's checks 3 to 6 and RW steps A to C as behaviour (11.6).

### 16.2 `plan.md`, `requirements.md`, `status.md` and the risks

**Plan text.** Phase 6's "From phase 3" item on `remote.<r>.fetch` ("Read the refspec and set
`DropFacts.RemoteMapsBranchesDifferently` …", line 386) and its "From phase 5" item (line 387) become
history: section 10 replaces the flag, section 9 settles what the setting covers (D196). Its "From
phase 4" item (line 384), "The settings screen's external tools are where the user chooses it",
becomes "The settings screen's Git section is where the user chooses it" (7.4; Tools holds the editor
only, 7.5). Phase 5's outcome sentence (lines 346-348) stays as history. "Visual test gate" step 3
names `VisualCommit.VisualTests.Phase6` and part L's `VisualCommit.VisualTests.Phase6Look`, which
the lead adds once that project exists (17.3; 17.3's measurement makes no view-test project); its
step 4's "It needs the desktop unlocked" becomes "It needs the desktop unlocked and Windows' high contrast
off (the pass never changes it, D224)" (11.8). Phase 8's item "Performance pass and
keyboard-accessibility pass." becomes "Performance pass and keyboard-accessibility pass; the dark
theme's text below 4.5:1 on hovered and selected rows and the dark danger button's label (D198; phase
6's design 11.10)."

**`requirements.md`**, the main window layout (D20, D22): the Toolbar row reads "Undo, redo, fetch,
pull, push, branch, stash, pop, settings, search, theme switch" (D197); the Repo tabs row "One tab per
open repo or workspace dashboard, the dashboards first, plus a button to open, clone or init another"
(D187); a note under the table says that on a dashboard tab the dashboard takes the place of the
panels and the graph, and the status bar's branch part shows the workspace's name (2.4, 2.8). C12's
last sentence becomes "From phase 6 every checkout, merge, rebase, cherry-pick, revert and stash apply
or pop also keeps ignored files by default, the hard reset aside (D193, D196)"; Q5's exception becomes
"operations that would overwrite or delete an ignored file keep it by default, which a setting turns
off (D193, D196)" (9.16).

**`status.md`**: step 11's "`remote.<r>.fetch` read: `DropFacts.RemoteMapsBranchesDifferently` set" is
reworded to "fetch refspecs read (design 10): the drop's push, `Delete <b> on <r>…` and checkout's
local name follow the mapping, reasons R1 to R4, R2's checkout without an upstream (D201);
`DropFacts.RemoteMapsBranchesDifferently` removed"; step 12's "as the owner decides" names D196.
Section 18's "Status steps" says which packages serve steps 5 and 7 to 21.

At the phase's end `architecture.md` gains summary mode, the registry and adoption ("Repository tabs
(D45)", "Operation queue", 3.9) and the moved ignored-files guard ("Protected changes and the automatic
stash", "The ignored-files guard", 9.16).

**Moved to phase 8** (first run, the accessibility pass, the by-hand platforms):

- a broken `~/.gitconfig`'s own message: today `git --version` fails and the app says "Git is not
  working"; showing git's `fatal: bad config line …` is first-run work (8.5);
- a palette made for high contrast (11.3), and the dark theme's text below 4.5:1, left with the
  owner's consent (D198): `VcDangerBrush` 3.64:1 on a hovered and 3.76:1 on a selected row,
  `VcAccentBrush` 4.13:1 on a hovered and 4.26:1 on a selected row (the renamed file's "R"), and the
  danger button's white label 3.38:1. A lighter dark danger colour also lowers that label (2.70:1 at
  about `#EA7D86`), so it comes with the dark label `#10121A`, which changes only the label in 35
  earlier dark pictures: scripted phase 2 `17a`, `18a`, `19a`, `19c`; phase 3 `04b`, `09f`, `10f`,
  `11b3`, `12g`, `13i`, `13j`, `16h`, `16k`, `20c`, `22e`, `25c`, `25g`, `29d`, `30d`, `31a`; phase 4
  `b9c`, `c13d`, `c13g`, `c13k`, `c14h`, `c16b`; phase 5 `b9i`, `b9m`, `u16i`; RW phase 2 `17a`; phase 3
  `09f`, `10f`, `22e`, `31a`; phase 4 `c16b` (11.10, 11.11);
- the icon buttons without an automation name (only the page close buttons set one, 15.5):
  `ResolverPreviousFile`, `ResolverNextFile`, `ResolverCloseButton`, `ResolverPreviousConflict`,
  `ResolverNextConflict`, `CloseDiffButton`, `ActivityLogClose`, `SearchPrevious`, `SearchNext`,
  `SearchClose`, `CompareSwap`, `CompareClose`, `RefHiddenToggle`, `RefSoloToggle`,
  `DismissRestoreButton`, `RowStageButton`, `RowDiscardButton`, `RowUnstageButton`, for the
  accessibility pass;
- the macOS and Linux editor install paths and the macOS native theme observer, by hand (12.2, 11.9);
  the macOS checklist gains 11.8's by-hand System step.

**To the backlog**: reordering workspace rows by a drag (1.10); "Open in editor" at a line from blame
and the diff (12.1); editing `mergetool.<tool>.path` or `.cmd` and `merge.guitool` (8.10); branch keys
other than `branch.<b>.rebase` (8.8); keys the user can change (D192); a write that refreshes a stale
index (risk 29, the owner's call); undo of a remote's removal under a namespace outside
`refs/remotes/<r>/` (10.8 #1). Profiles (O16) stay there.

**New risks** (numbered on from 28):

| # | Item | Needed by |
|---|---|---|
| 29 | Under `GIT_OPTIONAL_LOCKS=0` (D47) a stale index makes every status read re-hash the touched files: 3.1 s per read in a 20,000-file tree with `core.fscache=true` (6.1 s without) until something writes the index; the dashboard's reads and the tabs' pay it alike (0.1). Refreshing the index would be a write, the owner's call | When users report slow reads |
| 30 | `remote.<r>.mirror = true` makes every push the app makes to that remote fail with `fatal: --mirror can't be combined with refspecs` (Push, the push dialog, tags, deletions, undo's `RemoteRef`); the error dialog shows git's words (10.8 #2) | A report |
| 31 | The ignored-files check is the app's, not git's: a window of one process start between its `check-ignore` and git's write (9.12); nothing checked again on Continue, Skip or Abort (9.14); the fork-point gaps of a rebase onto a moved-back upstream and of git's own range (9.6); a plain rebase over a merge refused for a boundary commit's file, which git would keep (9.6); Git 2.30 to 2.33's unpack-trees rules for ignored files untested (0.4) | Phase 8 (the git versions the release supports) |
| 32 | The gate inspects nearly every earlier picture again: the Settings button in every toolbar, the welcome page, tooltips' colours, keyed menus, the light colours (16.1). Phase 5's method (references before the gate, the comparison by region, 17.7) carries it, with more batches | Phase 6's gate |
| 33 | Two fetches of one common git folder that overlap while the remote has something new lose one of them, with `cannot lock ref '<ref>': is at <new> but expected <old>` or a held `.lock` (0.2). 4.3 keeps the runs' rows apart, keeps a run off another queue's running network operation and keeps auto-fetch off both. A Fetch, Pull or Push the user starts in a tab, or a row's own (4.7), while another worktree of the repository (or a second tab on the same worktree, 3.5) fetches is not kept apart, as in phases 3 to 5. One ends "Fetch failed" (or "Pull failed", "Auto-fetch failed") with git's lines; nothing is left behind and the next fetch works. A cancelled fetch's stale-lock scan covers the common folder (`LockFiles.FindSince([GitDirectory, CommonDirectory], since)`, `RepositoryViewModel.cs:107`), so it can list another worktree's live lock; the stamps (4.6) and the "still exists" check keep its removal harmless. A window-wide fetch gate in every queue would close it | A report |

**Risks that change**:

- 8 (CI's limits): the Windows Test step may reach 28 to 31 minutes on a slow runner, near D186's 32
  (17.6).
- 10 (Q1): a restored dashboard in the background reads nothing, and an active one reads beside the
  restored repo tabs' openings (2.10); watch the gate's Q1 numbers; section 10's fetch lines come in the
  call that replaces `git remote` (10.2), so a refs read stays at five processes and Q1's path does not
  change.
- 12 (the run time): +5 to +10 minutes over WP-S's 43 min 33 s rested, about 49 to 54 (17.6); two more
  walk-through projects (`Phase6` and `Phase6Look`, 17.3); the view tests in App.Tests, where WP-S
  measured the processors, not the UI thread, setting the length (17.3).
- 18 (the real-window pass): +5 to +8 minutes (17.6).
- 20 ("Open file"): `PATHEXT` (through `ToolCatalogues.PathExtensions`), the user's extensions and the
  fixed floor are read; the file associations and macOS bundles beyond `.app` are not (12.8).
- 22 (Avalonia details): phase 6's keys rely on more of them, read from IL only (0.7):
  `KeyboardNavigationHandler` taking Tab with any modifier, `TextBox` typing a line break for Enter with
  a modifier when `AcceptsReturn`, `MenuItemAutomationPeer`'s accelerator key; not checked on a Mac or a
  Linux desktop (IBus and Fcitx with Ctrl+Shift+E, the macOS application menu).
- 23 (Pull's parity): Pull reads its configuration before the fetch (8.9), and the chosen rebase
  `pull.ff` alone, as `git pull --rebase` does (D231); the parity tests compile and compare unchanged,
  since `PullIntegrationOptions` is optional.
- 25 (git calls per recorded operation): the ignored-files check (D196) adds reads before each covered
  merge, pick, revert, rebase and stash apply or pop, and none to a checkout or to the option on a
  fast-forward: merge 1, pick, revert and a list 2, rebase 5, or 7 with a merge in the range,
  interactive rebase 3 or 4, squash 3, the stash 1 after D179's count (9.4 to 9.8); a `--ff-only` pull
  whose check finds a file adds `merge-base --is-ancestor` (9.10); pinned by the call counts in `IgnoredInTheWayTests` and
  `RepositoryOperationsKeepIgnoredTests` (9.17).
- 26 (CI's gits): phase 6's facts were taken on 2.36 and 2.55 only (0.3 to 0.5); CI's 2.43 and 2.56 run
  WP1's, WP2's and WP3's real-git tests for the first time, so every job of their first CI runs is
  read; tests that depend on the git version are written by parity with the git that runs (`m` read
  with `--type=bool`, `GlobalFile`, `FetchRefspecsParityTests`, which leaves out the symbolic
  `refs/remotes/origin/HEAD` that Git 2.48 and later create from the server's HEAD, 10.9) or from recorded outputs
  (`--tool-help`, 2.36's `name consists only of disallowed characters`, 8.13).
- 27 (disk): at most 3 built worktrees at once (18).
- 28 (Visual Studio's Git): `MSYSTEM` is removed by the tests' isolation (8.12, 17.2); that git also lacks
  `sleep`, so 17.2's fetch hold and phase 5's waiting merge tool do not hold with it first on `PATH`, and
  the folder server's tests fail with the server's message.

**Handoff only** (not risks): fsmonitor daemons started per dashboard repository under the owner's
`core.fsmonitor=true`, accepted (Q5, 0.1); a phase 5 build sets aside a `settings.json` with `"theme":
"System"` (7.10, 11.2); 10.8's known issues 3 to 7; the prompt group reusing an answer by prompt text
across one run's repositories (4.4); 9.6's fork-point gaps and 9.14's limits; 17.3's measured times and
the project chosen; on a Mac keyboard F6, Shift+F6 and Shift+F10 need Fn unless "Use F1, F2, etc. keys
as standard function keys" is on, and Home, End, PageUp and PageDown are Fn with the arrows (6.1), and
the dashboard list's Remove is Cmd+Backspace there (2.7); D221's reading of D193's "checkouts then
follow git's own default" (9.1), for the owner to confirm at acceptance.

---

## 17. Tests and harness

Phase 6's checks need what the harness lacks: ten repositories at once, each in its own state; a git
configuration a check may write without reaching any other test; and fixed answers where the app now
asks the machine (the system's theme, the installed editors and merge tools, `PATHEXT`).

### 17.1 Scenario repositories (`tests/VisualCommit.Testing/Scenarios.Phase6.cs`)

Each scenario is a template built once per test process through
`HangWatchdogAttribute.SharedPreparationAsync` (`HangWatchdogAttribute.cs:78`, so the first test's wait
does not count against the watchdog), deleted at exit; every call returns a fresh copy.
`Scenarios.TemplateAsync` and `KeepUntilExit` (`Scenarios.cs:323-332`) become generic
(`TemplateAsync<T>(string, Lazy<Task<T>>) where T : IDisposable`), their callers unchanged. Commits are
by `Test Author <author@example.com>` (`TempRepo.AuthorName`, `AuthorEmail`) from 2026-01-01 12:00 UTC,
a minute apart per repository; messages end with a line feed, files have `\n` endings. Every state was
derived on 2.36 and replayed on 2.55 with the same output (0.10).

**The workspace** (`Scenarios.WorkspaceAsync()`, "workspace") is a new `TempWorkspace`
(`tests/VisualCommit.Testing/TempWorkspace.cs`): `Root`, a `TempDirectory("workspace")` with a working
tree per repository, `remotes/<name>.git` for the eight with a remote and `home/`, `TempRepo`'s
isolation home for the builder's git calls, which is not a repository and which W1 and 1.8's folder
scan expect (a folder searched, no candidate); `Names` in the dashboard's
order (`api`, `web-app`, `docs`, `mobile`, `design-system`, `billing`, `infra`, `scratch`, `notes`,
`old-site`); `PathOf(name)`, `RemoteOf(name)`; `Repo(name)`, a `TempRepo` that does not own the folder
(a new `internal static TempRepo Over(TempDirectory root, string path, DateTimeOffset clock)` beside
`OpenSibling`, `TempRepo.cs:383-393`), its clock a minute after the last commit; `CopyAsync()` by
`TempRepo.CopyDirectory` (`TempRepo.cs:641-653`), **no git process**; `ToWorkspace(string id = "web",
string name = "Web")`, section 1's `Workspace` for `ShellDriver.WriteWorkspaces` (17.2).

`LeanRepositories` (`tests/VisualCommit.Testing/LeanRepositories.cs`, a stream builder as
`LargeHistory.BuildStream`, `LargeHistory.cs:185-224`) copies `TempRepo.CreateAsync`'s blank git folder
(`BlankGitFolderAsync`, its six `RepositorySettings` included) to `<name>/.git` and the blank bare one
to `remotes/<name>.git`; then (1) `.git/config` gains, as text, `[remote "origin"]` /
`\turl = ../remotes/<name>.git` / `\tfetch = +refs/heads/*:refs/remotes/origin/*` and per tracked branch
`[branch "<b>"]` / `\tremote = origin` / `\tmerge = refs/heads/<b>`; (2) `git fast-import --quiet
--export-marks=<file>` in the working repository with the commits it knows and its refs (`reset <ref>` /
`from :<mark>`), remote-tracking refs included; (3) `git fast-import --quiet` in the bare remote with
the commits it holds and its `main`; (4) `HEAD` written as text where the table says, a detached SHA
from the marks file; (5) `git reset --hard -q`; (6) the "Then" column. The builder makes 28 git calls
for the ten, the table's "git" column (`TempWorkspace.Calls`).

| # | Folder | Commits (time, message: files) | Working repository's refs | Remote's `main` | Then | git | `status --porcelain=v2 --branch` |
|---|---|---|---|---|---|---|---|
| 1 | `api` | 12:00 "Initial commit": `README.md` `# api`, `routes.txt` `GET /`; 12:01 "Add the health route": `routes.txt` `GET /` `GET /health` | `main` = `origin/main` = 12:01 | 12:01 | — | 3 | `# branch.ab +0 -0`, nothing else |
| 2 | `web-app` | 12:00 "Initial commit": `README.md` `# web-app`, `index.html` `<h1>Home</h1>`; 12:01 "Add the about page": `about.html` `<h1>About</h1>`; 12:02 "Style the pages": `style.css` `h1 { color: teal; }` | `main` = 12:02, `origin/main` = 12:00 | 12:00 | — | 3 | `+2 -0` |
| 3 | `docs` | 12:00 "Initial commit": `README.md` `# docs`, `guide.md` `Guide`; 12:01 "Write the install guide": `install.md` `Install it.`; 12:02 "Write the FAQ": `faq.md` `Questions.`; 12:03 "Fix the guide's typos": `guide.md` `Guide.`; 12:04 "Add the changelog": `changelog.md` `Changes.` | `main` = 12:00, `origin/main` = 12:03 (12:04 unfetched) | 12:04 | — | 3 | `+0 -3`; a fetch counts 4 objects: `[behind 4]` |
| 4 | `mobile` | 12:00 "Initial commit": `README.md` `# mobile`, `app.txt` `app`; 12:01 "Add the login screen": `login.txt` `Log in`; 12:02 "Add the settings screen": `settings.txt` `Settings`; 12:03 "Name the app", parent 12:00: `app.txt` `Mobile app`; 12:04 "Add the about screen", parent 12:02: `about.txt` `About` | 12:00 to 12:03; `main` = 12:03, `origin/main` = 12:02 | 12:04 (12:00 to 12:02, 12:04) | — | 3 | `+1 -2`; a fetch counts 4: `[ahead 1, behind 3]` |
| 5 | `design-system` | 12:00 "Initial commit": `README.md` `# design-system`, `colours.txt` `red`, `fonts.txt` `Inter`, `spacing.txt` `4`; 12:01 "Tighten the spacing": `spacing.txt` `2`; 12:02 "Loosen the spacing": `8`; 12:03 "Even out the spacing": `6` | `main` = `origin/main` = 12:00 | 12:03 | `colours.txt` `red` `blue`, `git add colours.txt`; `spacing.txt` `4` `16`; `fonts.txt` deleted; `icons.txt` `home` | 4 | `+0 -0`, `1 M. … colours.txt`, `1 .D … fonts.txt`, `1 .M … spacing.txt`, `? icons.txt`; a fetch counts 11 |
| 6 | `billing` | 12:00 "Initial commit": `README.md` `# billing`, `invoice.txt` `total: 10`; 12:01 "Add the tax" on `feature/tax`: `invoice.txt` `total: 12`; 12:02 "Round the totals" on `main`, parent 12:00: `invoice.txt` `total: 10.00` | `main` = `origin/main` = 12:02, `feature/tax` = 12:01 | 12:02 | `git merge --quiet feature/tax` (exit 1) | 4 | `+0 -0`, `u UU … invoice.txt`; `MERGE_HEAD` |
| 7 | `infra` | 12:00 "Initial commit": `README.md` `# infra`, `deploy.sh` `echo deploy`; 12:01 "Deploy to staging first": `deploy.sh` `echo staging` `echo deploy` | `main` = `origin/main` = 12:01, lightweight `v1.0` = 12:00; `HEAD` = 12:00's SHA | 12:01 | — | 3 | `# branch.head (detached)`, no upstream lines |
| 8 | `scratch` | 12:00 "Initial commit": `README.md` `# scratch`; 12:01 "Try an idea" on `experiment`: `idea.txt` `An idea` | `main` = `origin/main` = 12:00 (only `branch.main` configured), `experiment` = 12:01; `HEAD` = `ref: refs/heads/experiment` | 12:00 | — | 3 | `# branch.head experiment`, no upstream lines |
| 9 | `notes` | 12:00 "Initial commit": `README.md` `# notes`, `todo.txt` `Buy milk`; 12:01 "Add the reading list": `reading.txt` `A book` | `main` = 12:01; no remote | — | — | 2 | `# branch.head main`, no upstream lines |
| 10 | `old-site` | none: listed, never made | | | | 0 | `git -C old-site status`: `fatal: cannot change to 'old-site': No such file or directory`, 128 (the app never runs git there: `OpenAsync` tests the folder first, `GitRepository.cs:80-83`) |

The lab's SHAs, the same on both gits (`Phase6ScenarioTests` pins what the C# builder gives): `api`
`2248c9d`; `web-app` `cbd9037`, `origin/main` `147c6e3`; `docs` `ec855ba`, `origin/main` `23e2ad1`,
remote `245b729`; `mobile` `21b8aeb`, `origin/main` `f419c6e`, remote `67a2660`; `design-system`
`98ba313`, remote `948f7d1`; `billing` `0e943ed`, `feature/tax` `8236f53`; `infra` HEAD and `v1.0`
`09d320a`, `main` `229cea5`; `scratch` `experiment` `ba18976`, `main` `42a778b`; `notes` `937a43b`.
Fast-import writes reflogs on 2.36: a `fast-import` entry for every ref it sets, and one in HEAD's log,
then step (5)'s reset adds its own entry. Pull all's outcomes (2.36): docs fast-forwards 4 commits; mobile under
`pull.ff=only` `fatal: Not possible to fast-forward, aborting.`, 128; design-system `error: Your local
changes to the following files would be overwritten by merge:` / `\tspacing.txt`, exit 1; api and
web-app `Already up to date.`

**The renaming refspecs** (`Scenarios.RenamingRefspecAsync()`, "refspecs"), the TempRepo way, about 20
processes: `refspecs` with bare `origin.git`, `fork.git`, `upstream.git` beside it by relative URLs (a
new `TempRepo.AddBareRemoteAsync(name, relative: true)`); 12:00 "Initial commit" (`README.md`
`# refspecs`), 12:01 "Add the parser" (`parser.txt` `parse`) on `main`, 12:02 "Work on the topic"
(`topic.txt` `topic`) on `topic`. Fetch lines: `origin` the default and
`+refs/pull/*/head:refs/remotes/origin/pr/*`; `fork` only `+refs/heads/*:refs/remotes/fork/up/*`;
`upstream` only `+refs/pull/*/head:refs/remotes/upstream/pr/*`. The remotes hold `origin` `main`,
`feature/x` (12:01) and `refs/pull/1/head` (12:02); `fork` `main` (12:01); `upstream` `main` (12:01)
and `refs/pull/7/head` (12:02). After `fetch --all` the tracking refs are `fork/up/main`,
`origin/feature/x`, `origin/main`, `origin/pr/1`, `upstream/pr/7`; `main` tracks `origin/main`, clean:
a unique mapping, R2, R4 and the identity (0.10). R1 and R3 are 10.9's tests only.

**Ignored files** (`Scenarios.IgnoredAsync()`, "ignored"), the TempRepo way (25 processes), a bare
`origin.git` beside it by a relative URL:

| Time | Commit | On | Files |
|---|---|---|---|
| 12:00 | "Initial commit" | `main` | `README.md` `# Ignored`, `app.txt` `app`, `.gitignore` `secret.env` `build/` |
| 12:01 | "Track the secret" | `main` | `secret.env` `old` (`-f`) |
| 12:02 | "Stop tracking the secret" | `main` | `secret.env` out (`git rm --cached`); `vendor` and `tools` start here |
| 12:03 | "Add the vendor secret" | `vendor` | `secret.env` `vendor` (`-f`) |
| 12:04 | "Add the build file" | `tools` | a file `build`, `build` (`-f`) |
| 12:05 | "Main work" | `main` | `app.txt` `app` `main` |

Then a stash "Stash the secret" on `main` holding a staged new `secret.env` `stashed` (`git add -f`,
`git stash push --message`); `main` and `vendor` pushed and tracked; `vendor` pushed a second time as
`vendor-remote` by a push of its own without `-u` (`git push origin vendor:refs/heads/vendor-remote`;
listed last in the `-u` push it would make `vendor` track `origin/vendor-remote`, 0.10), so that
`origin/vendor-remote` has no local branch and `vendor` still tracks `origin/vendor`; and in the working
tree `secret.env` `LOCAL` and `build/out.bin` `binary`: `status --porcelain=v2 --branch --ignored` shows
`! build/` and `! secret.env` on a clean, level `main` (HEAD `c49cac4` on both gits). Each operation
G22 and G23 run overwrites `LOCAL` here without the app's check (0.10): checkouts of `vendor` (also
through `origin/vendor`, which checks out the local `vendor`, D93), of `origin/vendor-remote` (tracking;
0.10's `vendor2` form) and "Create branch… and check out" at `vendor`; `tools` (an ignored folder
where the target has a file); merging `vendor`; rebasing `main` onto `vendor`; cherry-picking "Add the
vendor secret"; reverting "Stop tracking the secret"; the stash's apply and pop. The rest of D196's
operations (the interactive rebase, the squash, "Check out and rebase", the pull's merge and rebase)
are 9.17's tests'. D179's case (an older commit tracking the file) is here too: `main` reaches "Track
the secret" (12:01), which S21 checks out with the setting off; phase 5's "protect" scenario gains no
branch.

**Git's settings** (`Scenarios.GitSettingsAsync()`, "settings"), the lean way, 4 processes: `settings`
and its bare `remotes/settings.git`, the blank folder's `[user]` removed (`git config --remove-section
user`); 12:00 "Initial commit" (`README.md` `# Settings`, `notes.txt` `one` `two`, `run.py`
`print("run")`), 12:01 "Remote work" (`remote.txt` `remote`) on the remote's `main` and `origin/main`,
12:02 "Local work", parent 12:00 (`local.txt` `local`) on `main`: `+1 -1`, clean, so the pull modes
differ (a merge commit, a rebase, a refusal). The checks seed the session's global file or write
`.git/config` (17.2, 17.4): an identity, `user.useConfigOnly = true` (else git's refusal names the
machine, 0.3), `mergetool.slow.cmd`, a global `pull.ff = only`, `branch.main.rebase`, `pull.rebase`.

**Reused**: phase 3's "ops" and "auth", phase 4's "conflicts", phase 1's "graph", phase 5's "history"
and "reflog".

**`Phase6ScenarioTests`** (`tests/VisualCommit.Git.Tests/`) pins what this subsection and 0.10 state:
each repository's `status --porcelain=v2 --branch`, `for-each-ref` (names, SHAs), remote refs and
`remote get-url origin`, `old-site` absent, `MERGE_HEAD`; a copy fetching from its own `remotes/` with
no git process of its own (`CopyAsync` takes no runner; the template's call log gains nothing), its
`Counting objects: 100% (4/4)` and `100% (11/11)` and the states after; the refspec lines, tracking refs
and `branch --track probe` results; the ignored scenario's commits, stash, ignored entries,
`refs/remotes/origin/vendor-remote` without `refs/heads/vendor-remote`, `vendor`'s upstream
`origin/vendor`, and three refusals (`vendor`, `vendor-remote`'s tracking form, `tools`); no `user.*`
in "settings"; no `*.sample` in any copy.

**Git's hook samples**: `MakeBlankGitFolderAsync` (`TempRepo.cs:200-224`) deletes
`hooks/*.sample` after `git init` and keeps the folder (`RebaseTestSupport.InstallHook`,
`SequenceTestSupport.WriteHook` and `RepositoryOperationsQueueTests`' `pre-receive` create it anyway):
544 files become 323 in the workspace's copy (0.10), 13 or 14 fewer per git folder everywhere. Its own
commit in WP-S, measured: 200 copies of a git folder took 7.8 to 8.4 s with the samples and 3.8 to 4.5 s
without; the default run, rested, 43 min 49 s for 3,236 tests at `3f62463` before WP-S and 43 min 33 s
for 3,299 at WP-S's head, this commit included (17.6).

**`TempRepo.CopyAsync`** (`TempRepo.cs:518-548`) rewrites a URL whose `Path.GetFullPath(url)` lies in the
old folder, leaving a relative one alone only by accident; it gains
`if (!System.IO.Path.IsPathRooted(url)) continue;` with a comment.

### 17.2 Harness additions

**One git environment per app session** (amends D112; 8.12 says why and what reads it).
`AppSession.Start` (`AppSession.cs:95-104`) takes `IReadOnlyDictionary<string, string?>? gitEnvironment
= null` and passes it through `GitLocator.DetectAsync` (`GitLocator.cs:15`, called at `AppSession.cs:135`)
to the app's one runner (`new GitRunner(path, callLog, log, recordClock, environment)`,
`GitLocator.cs:30`), whose `BuildStartInfo` (`GitRunner.cs:184-220`) applies `DefaultEnvironment`, then
the session's, then the command's (`:207`). The desktop app passes nothing. `AppSession.Start` also
takes `Func<string, string?>? readEnvironment = null` (7.3; `Environment.GetEnvironmentVariable` when
null). It reads `VISUALCOMMIT_AUTO_FETCH_MINUTES` through it for the start-up line and hands it to
`AutoFetchScheduler.ReadEnvironment` through `MainWindowViewModel`, so `AppSessionTests` never change
the process's environment, which `AutoFetchSchedulerTests.The_test_processes_turn_it_off` asserts in
parallel. The harnesses pass nothing.

- `GitIsolation.SessionGitEnvironment(string dataDirectory)` creates `<data>/harness/` and its `xdg/`
  and returns `GIT_CONFIG_GLOBAL = <data>/harness/global.gitconfig` (made by git's first write) and
  `XDG_CONFIG_HOME = <data>/harness/xdg`. `ShellDriver.Start` (`ShellDriver.cs:72-109`) passes it on
  every start, so a restart keeps what the app wrote; `RealApp.Launch` (`RealApp.cs:144-253`) sets both
  in place of the shared file (`:165-173`), before the check's own environment. The owner's
  `~/.config/git/ignore` stops reaching the app (0.10). `ShellDriver.GlobalGitConfigPath(string
  dataDirectory)` and `SeedGlobalGitConfig(string dataDirectory, string text)` write the file; `RealApp`
  uses the same helpers.
- **The safety net**: `GitIsolation.IsolateThisProcess` (`GitIsolation.cs:21-49`) points the process's
  `GIT_CONFIG_GLOBAL` at `<ceiling>/isolation/no-global/gitconfig`, in a folder nobody makes, and
  `XDG_CONFIG_HOME` at an empty `<ceiling>/isolation/xdg`: reads see no global configuration, and a
  write outside a session fails (255, 0.10) in the test that made it. Nothing writes global
  configuration today (a `grep` of `--global`); `TempRepo`'s calls keep their own
  (`TempRepo.IsolatedEnvironment`, `TempRepo.cs:66-104`).
- **The tripwire**: `GitIsolationTests.Nothing_wrote_a_global_configuration_outside_a_session` (and
  `Phase6SupportTests`) fails when `<ceiling>/isolation/no-global` exists, when `<ceiling>/isolation/xdg`
  holds a `git` folder (8.12), or when the old shared `<ceiling>/isolation/gitconfig`, which a stale
  build would use, is not empty.
- `GitIsolation.MachineIndependence` (`GitIsolation.cs:149-157`) removes `GIT_AUTHOR_NAME`,
  `GIT_AUTHOR_EMAIL`, `GIT_COMMITTER_NAME`, `GIT_COMMITTER_EMAIL`, `EMAIL` (not the `*_DATE` variables,
  set before it is applied, D72) and `MSYSTEM` (risk 28; WP-L's commit before WP0, 18), and sets
  `PATHEXT = .COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC`, 12.9's fixed value, for tests that
  build app pieces without a session. One list serves the test process, the real-window app and
  `TempRepo`'s calls; `TempRepo` sets its identity after it (`TempRepo.cs:42-48`).
- **Tests**: `ShellTests.Git_calls_of_the_app_under_test_do_not_see_the_configuration_of_the_machine`
  (`ShellTests.cs:175-191`) keeps its three assertions and adds that a session's `git config --global`
  lands in `<data>/harness/global.gitconfig` (per `git config --show-origin --show-scope --list`) and
  `isolation/no-global` stays absent; `Two_sessions_in_one_process_do_not_see_each_others_global_configuration`;
  `GitIsolationTests` for the variables (`GIT_AUTHOR_DATE` kept) and for the tripwire, its XDG check
  included (a `git` folder under `<ceiling>/isolation/xdg` fails it, 8.12); and in the real-window
  pass a check that each launch's global git file lies in its own data folder
  (`<data>/harness/global.gitconfig`, 8.13).
  `Git_calls_that_end_after_the_session_do_not_bring_its_deleted_data_folder_back` (`ShellTests.cs:247-268`)
  still passes: only `ShellDriver.Start` and, before it, `FetchHold.Install` make `<data>/harness`,
  never a git call.

**Fixed catalogues** (12.9 owns `ToolCatalogues` and its entries; nothing in them depends on the disk,
so no picture shows the machine's tools): `ShellDriver.Start` passes `ToolCatalogues.Fixed`,
`RealApp.Launch` sets `VISUALCOMMIT_TOOL_CATALOGUES=fixed`. As 12.9 gives them: the editors found are
`vscode` Visual Studio Code, `sublime` Sublime Text and `idea` IntelliJ IDEA under a root that does not
exist (`C:\Tools\Microsoft VS Code\Code.exe`, `C:\Tools\Sublime Text\subl.exe`, `C:\Tools\JetBrains\IntelliJ
IDEA 2026.1\bin\idea64.exe` on Windows; `/opt/tools/vscode/code`, `/opt/tools/sublime/subl`,
`/opt/tools/idea/bin/idea` elsewhere); the merge tools
found are `kdiff3`, `meld` and `vimdiff` (which 8.10's filter leaves out), those not found `araxis`,
`bc`, `p4merge` and `winmerge`, without descriptions and there at once, without 8.10's kept list
(`merge-tools.json` neither read nor written, D202); `PathExtensions` is Windows'
default `PATHEXT`, `.COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC`, and none elsewhere.
`Phase6Expectations.S` names the entries; `FakeFileLauncher` records each editor start (the program,
the arguments as a list); `HeldMergeTools` (12.9) holds the list back for a check of the loading
state, which D202 leaves to the very first lookup on a machine.

**The system's theme**: `FakeSystemTheme` (`Headless/FakeSystemTheme.cs`) implements 11.3's
`ISystemTheme`; `Set(bool dark)` raises `Changed` on the UI thread. `AppSession.Start` takes
`ISystemTheme? systemTheme = null`; `ShellDriver.Start` and `StartWithTabs` take `bool systemDark =
true` and expose `ShellDriver.SystemTheme`; `SetSystemThemeAsync(bool dark)` flips it and waits until
the window shows that variant (the theme service's `IsDark`) and the layout settles. The real window
has no fake (11.8).

**The dashboard's read clock.** `TabServices` gains `ReadClock` (a `TimeProvider`, `Clock` unless
given; only 2.10's 10 s rule reads it). `AppSession.Start(…, TimeProvider? readClock = null)` passes it
to `new TabServices(…)` (`AppSession.cs:159-173`); `ShellDriver.Start`, `StartWithTabs` and
`StartWithWorkspace` take `TimeProvider? readClock = null` and expose it as `ShellDriver.ReadClock`.
W8 alone passes the existing `ManualTimeProvider` (`tests/VisualCommit.Testing/ManualTimeProvider.cs`);
the search box's delay and batches, auto-fetch's timer and the recent list's and graph filters' times
keep `Clock`, so nothing else in that session stands still.

**`AuthRemoteServer` for a folder** (amends D97 as D150 did). It serves one repository
(`IsInRepository`, `AuthRemoteServer.cs:679-680`) and holds one fetch (`_pendingHold`, `:297-306`;
`Release()`, `:310-330`) today, and gains: `Phase6VisualChecksPort = 47393` and, for part L's project
in its own process, `Phase6LookVisualChecksPort = 47394` (the lead's ruling of 2026-10-22 on the
report), pinned beside 47391 and 47392 (phase 6's checks run beside phases 1 and 3's servers) in
`AuthRemoteServerTests.A_taken_port_gives_way_to_a_free_one_only_when_asked` (`AuthRemoteServerTests.cs:404-416`); `StartForFolderAsync(string folder, int port = 0,
bool fallBackToFreePort = false)`, as `StartAsync` (`AuthRemoteServer.cs:166`), so that a test gets a
system port unless it asks for one, serving each bare repository directly in `folder` (every folder in
it checked as `ExistingRepositoryAsync` does, `:243-275`, one `rev-parse --is-bare-repository` each,
and the start refused for one it cannot serve: no bare repository, or a name a URL would escape, so a
broken scenario fails there and not later with a 404) with `GIT_PROJECT_ROOT` = `folder`, changing and
deleting nothing; `UrlOf(string repository)` = `http://127.0.0.1:<port>/<name>.git`, the repository
named `docs` or `docs.git`; `ServesFolder` and `ServedRepositories` (the names without `.git`, in
ordinal order); on a folder server `Url`, `RepositoryPath`, `RepositoryFolderName` and the
parameterless `HoldNextFetchPack()` throw, while a server of one repository keeps them;
`IsInRepository` for any served name, still without `..`; `HoldNextFetchPack(string repository, int
atPercent = 0)`, a hold per repository taken by its next pack response (`TakePendingHold(repository)`,
`:591-603`) with D150's split; `Release(string repository)` beside `Release()`, which releases every
repository; `HoldNextRequest(string repository)`, a gate on that repository's next request of any kind
at the top of `HandleAsync` (`:481`), before `authorized` is decided (`:486`), whose task completes when
the request arrives, opened by `ReleaseRequest(string repository)` and by disposing (W6 orders two
sign-ins with it, 4.4); a request held at the gate gets a new read timer when the gate opens
(`RequestTimeout`, 60 s for the rest of the request). `RequireAuth`, `Credentials` stay server-wide; the "no http-backend" message is in (`43f52c4`).
Tests, the folder server's on a system port: `A_folder_server_serves_each_repository_at_its_own_url` (`ls-remote` of two);
`Holds_of_two_repositories_are_independent` (docs at 25, design-system at 45, exactly `Counting objects:
25% (1/4)` and `45% (5/11)`, released one at a time); `A_path_outside_the_served_repositories_is_refused`
(404); `A_held_request_gets_its_401_only_after_release` (no 401 recorded for the held repository until
`ReleaseRequest`, the other served meanwhile); and the port pin; with Visual Studio's 2.55 first on
`PATH` they fail with the server's message (risk 28). `Scenarios.ServeWorkspaceAsync(TempWorkspace
copy, bool requireAuth, params string[] names)`, the walk-through's and the real window's only way to a
folder server, serves the copy's `remotes/` on `Phase6VisualChecksPort` with the fallback and points each
named `origin` at `UrlOf(name)` (a process each). L31's "auth" step starts phase 3's server on its
own port, `StartForVisualChecksAsync(port: AuthRemoteServer.Phase6LookVisualChecksPort)`, not on 47391,
which phase 3's part D holds in its own process meanwhile; W4 and W6 never hold 47393 at once,
because `VisualCommit.VisualTests.Phase6`'s headless tests take turns on one UI thread
(`FreshApplication.cs:23-27`), as phase 3's part D's checks share 47391. L31 runs in
`Phase6Look`'s own process beside them (17.3), hence its own 47394, so neither takes the fallback to a
free port (`PortWasTaken`, noted in the report, D97) unless something else holds its port.
`Phase6Expectations.WithPort(text, port)`, as `Phase3Expectations.Common.cs:130`, gives every expected
text that shows the port (W6's sign-in dialog, `l31g`'s) the server's; a fallback (`PortWasTaken`) is
noted in the report, as D97 says.

**The fetch hold**: `FetchHold.Install(string dataDirectory)` (`tests/VisualCommit.Testing/FetchHold.cs`)
writes the hold script to `<data>/harness/hold-upload-pack.sh` (the folder made if missing, so a check
may install it before the start), its markers and `release` beside it, which the script finds through
`$(dirname "$0")`, so no path goes inside it. `HoldIn(params string[] repositoryFolders)` appends
`[remote "origin"]` / `\tuploadpack = sh '<script>'` as text to each named repository's own
`.git/config` (no git process; git merges the second header with the first, 0.10), the path in
`SequenceTestSupport.ShellQuote`'s form (`SequenceTestSupport.cs:119`: `/` for `\`, single quotes),
since a `\` in the value breaks every git call that reads the file (0.10); it throws for a repository
whose file has no `[remote "origin"]` with a `url`, so `notes` cannot be held by mistake. Never the
session's global file: there the section would give every repository a remote `origin`, `notes`
included, which Fetch all would then fetch (0.10). Checks call it before `ShellDriver.Start`, so that
the git-folder watcher causes no extra read; for the workspace, `HoldIn(copy.PathOf(name)…)`. Every
fetch of a held repository's file remote waits from its start, new commits or not (HTTP remotes ignore
the key), shown running without a percentage. The script: with `release` there it runs `exec git
upload-pack "$1"` at once and makes no marker; else it touches `held-<remote folder>.<pid>` (the
remote repository's folder name and the shell's `$$`), waits at most
120 rounds of `sleep 0.5` for `release`, removes its marker and runs `exec git upload-pack "$1"`. A
marker so means a fetch waiting now, or one a cancel killed while it waited, whose marker stays.
`Arm()` deletes `release` and takes the markers there as its baseline
(`Install` arms); `Held` and `WaitForHeldAsync(int count)` count the markers made since, so a second run
under one hold waits for its own; `Release()` writes `release`; `Dispose` releases (called in a
`finally`, so a cancelled fetch's shell ends, as `Scenarios.ReleaseWaitingMergeTool`). A held fetch
gives up after its 120 rounds and goes on, so a test that waits for `Held` releases well within about
60 s (the rounds' sleeps alone), or the hold no longer holds anything. `sleep` is a
process per round on Windows, about 36 ms of processor each (0.10): 120 rounds took 89 s on the laptop
(WP-S's measurement; 0.10's 600 rounds of 0.1 s had given an estimate of 60 to 70 s), below the hang
watchdog's 3 minutes, then the fetch goes on; a fifth of 0.10's processes, for a release up to 0.5 s
later, which every check's wait absorbs. Phase 5's waiting merge tool keeps `sleep 0.1`: its holds are
few and short. Visual Studio's Git does not hold (0.10; risk 28). `FetchHoldTests`, on Windows from a
data folder under `%TEMP%`: the line reads back through `git config --get remote.origin.uploadpack`;
two fetches held at once, one with nothing new; both exit 0 after `Release`; a later fetch not held and
no marker made; two runs under one hold, `Arm()` between them, the second `WaitForHeldAsync` not ended
by the first run's markers; beside held repositories, one without a remote prints nothing for `git
remote` and its `git fetch --progress` is not held (it exits 0 on 2.36, 0.2's M, so the test asserts
only that it is not held).

**Workspaces before the start** (D189): `ShellDriver.WriteWorkspaces(string dataDirectory, <the
workspace list>)` uses section 1's `JsonWorkspaceStore.Write(paths.WorkspacesFile, list)` (as
`WriteSession`, `ShellDriver.cs:135-136`; `AppPaths.WorkspacesFile` beside `SettingsFile`,
`AppPaths.cs:12-15`); `StartWithTabs(string dataDirectory, IReadOnlyList<TabState> tabs, …)` joins
today's overload (`:116-129`) for `new TabState(null, workspaceId)` (1.11); `RealApp.WriteWorkspaces`
likewise.

**`ShellDriver` additions** (`tests/VisualCommit.Testing/Headless/`), tested where the view tests go
(17.3): `ShellDriver.Phase6.Workspaces.cs` (`WriteWorkspaces`, `StartWithTabs(…TabState…)`,
`StartWithWorkspace(data, workspace list, openDashboardOf, extra tabs, width, height)`, the welcome
section and the add-repositories checklist as snapshots, `NewWorkspaceAsync(name)`);
`ShellDriver.Phase6.Dashboard.cs` (`DashboardSnapshot()` with the header and a `DashboardRowSnapshot`
per row: texts, tones, tooltips, Cancel and Details shown, selected; `WaitForDashboardReadAsync()`,
2.10's pass line and no `Reading…`; `WaitForRowAsync(name, predicate)`; `OpenRowAsync(name)`,
`RowMenuAsync(name)`; `ActivateWindow()`; `CallsSince(long sequence, string? worktree)` by
`GitCallRecord.Sequence` and `WorkingDirectory`); `.Bulk.cs` (`FetchAllAsync()`, `PullAllAsync()`,
`CancelAllAsync()`; `WaitForRowPercentAsync(name, percent)`; `WaitForBulkRunAsync()`, the result and
each row idle by `WaitForIdleAsync`, `ShellDriver.Phase3.cs:258`; `ClickRowDetailsAsync(name)`,
`ClickRowCancel(name)`);
`.Palette.cs` (5.11's); `.Keys.cs` (`PressCtrlTab(bool shift)`, `PressF6(bool shift)`, `FocusedRegion()`
by 6.6's `KeyRegion.Role`, `AcceleratorKeyOf(control)`, `HelpTextOf(control)`; `MenuSnapshot` entries'
`KeyText`); `.Settings.cs` (`OpenSettingsAsync(how)`, `SettingsSnapshot()`, `ChooseSection`,
`SetIdentity(scope, name, email)`, `ChoosePullMode(scope, mode)`, `ChooseMergeToolAsync(name)`,
`ChooseEditorAsync(name)`, `SetAutoFetch(on, minutes)`, `SetKeepIgnoredFiles(on)`,
`AddOpenFileExtension(text)`, `SaveSettingsAsync()`, `CancelSettings()`); `.Theme.cs` (`SystemTheme`,
`SetSystemThemeAsync(dark)`, `ShowsDarkTheme`); `.TabStrip.cs` (tabs' bounds and titles, "+"'s bounds,
the offset, the active tab).

**`RealApp` additions**: `RealApp.Phase6.cs` (the per-launch environment, `WriteWorkspaces`, 11.8's
read-only reads), `.Dashboard.cs` (rows and cells by id and name, the header's buttons, a real
double-click), `.Palette.cs` (5.11's), `.Settings.cs` (sections, fields, Save by id); the native folder
dialog by `ChooseFolderInDialog(title, folder, timeout)` (`RealApp.cs:347-372`), and beside it, new in
`RealApp.cs`, Windows' Open dialog by `ChooseFileInDialog(title, file, timeout)`, found by its title, the
file typed into its file name box (automation id `1148`) and confirmed by its button `1` (S14); a real
Ctrl+Shift+P by `PressKeys(CONTROL, SHIFT, KEY_P)` (`RealApp.cs:523-542`).

**`Phase6Checks.Support.cs`**: phase 5's `AssertStatus`, `AssertNoDialogAsync` and
`WaitForOperationAsync` wait only on a tab with a `Repository`, so it adds `WaitForAllRowsIdleAsync()`,
`AssertRow(name, expected)`, `AssertDashboardHeader(expected)` and `OpenWorkspaceAsync(data,
TempWorkspace copy, width, height, theme)` (writes "Web" and a session with its dashboard, starts,
waits for the first read); `OpenAsync` gains `AppTheme theme` and `bool systemDark = true`;
`ShotInBothThemesAsync(app, name, keepPointer)` runs 11.14's steps (Theme = System, the fake dark:
`Shot(name + "-dark")`; `SetSystemThemeAsync(false)`, Light and one token pixel asserted;
`Shot(name + "-light")`; `SetSystemThemeAsync(true)`, dark asserted, the screen still up), `keepPointer`
leaving the pointer where a tooltip or menu step put it. `Shot` refuses a picture name it has already
saved in the run, so two steps saving one name fail the run instead of overwriting (11.8). The five
helpers `WaitForAllRowsIdleAsync`, `AssertRow`, `AssertDashboardHeader`, `OpenWorkspaceAsync` and
`ShotInBothThemesAsync`, with their smoke tests in `Phase6SupportTests`, need WP-D's, WP-W's and WP-T's
drivers, so the lead adds them once those three are merged, before WP-V's parts W, S and L start
(section 18); WP-S's file has the rest.

### 17.3 The walk-through projects `VisualCommit.VisualTests.Phase6` and `.Phase6Look`

Two projects: `VisualCommit.VisualTests.Phase6` for parts W, K, S and G, copied file by file from
`tests/VisualCommit.VisualTests.Phase5`, and `VisualCommit.VisualTests.Phase6Look` for part L, beside
it as `Phase5Undo` is beside `Phase5` ("Two projects", below):

- `VisualCommit.VisualTests.Phase6.csproj`, phase 5's, its comment naming phase 6's parts and this section; `AssemblyInfo.cs` with
  `[AvaloniaTestApplication(typeof(HeadlessTestApp))]`, `[HangWatchdog]` and the module initializer
  (`ProcessIsolation.Isolate`), namespace `VisualCommit.VisualTests.Phase6`.
- `Phase6Checks.cs` (the map), `Phase6Checks.Support.cs` (`Phase = Phase6Expectations.Phase`, 17.2's
  additions, `PictureName()` matching `^[gklsw]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`); one
  class per part so xunit runs the parts side by side (`Phase6ChecksW`, `…K`, `…S`, `…G`, `…L`, in
  `Phase6Checks.<part>[.<topic>].cs`; `…L` and its files in `Phase6Look`).
- `Phase6Expectations.Common.cs` and `Phase6Expectations.<part>.cs` use only BCL, Avalonia, xunit,
  Core, Git and Testing (the real-window pass compiles them) and write key texts as literals
  (`"Ctrl+Shift+P"`, 6.12); their tests, `Phase6ExpectationTests.<part>.cs` and `Phase6SupportTests.cs`
  (the tripwire among them), are named so the real-window link leaves them out.
- Parts avoid the requirement prefixes C, O, Q, R, T: **W** workspaces and the dashboard (W1 to W9),
  **K** keys and the palette (K10 to K13), **S** settings and the theme's following (S14 to S21), **G**
  ignored files and refspecs (G22 to G25), **L** look: the sweep, the long folder, the tab strip (L26
  to L34); numbers run on across parts.
- Pictures of both projects under `artifacts/visual/phase-6/scripted/` through `VisualRun` unchanged
  (one folder, as phase 5's two projects share theirs), timings under
  `artifacts/visual/phase-6/timings/`.
- `VisualCommit.slnx` gains `<Project Path="tests/VisualCommit.VisualTests.Phase6/VisualCommit.VisualTests.Phase6.csproj" />`
  in `/tests/`, and `Phase6Look`'s line beside it once that project exists (the file is the lead's
  after WP-S, 18), so the default run and CI (`dotnet test --configuration Release --no-build`,
  `.github/workflows/ci.yml:64`, `:139`) run them.
- The real-window csproj, after phase 5's block: the comment "Phase 6's expectations, likewise
  (docs/phase-6-design.md 17.3); their own tests, Phase6ExpectationTests.<part>.cs and
  Phase6SupportTests.cs, are left out by their names, which the pattern does not match" and `<Compile
  Include="..\VisualCommit.VisualTests.Phase6*\Phase6Expectations*.cs"
  Link="Phase6\%(Filename)%(Extension)" />` (the `*` takes `Phase6Look`'s `Phase6Expectations.L.cs` too); its comment on
  the pass's length is updated at the gate. The pass: `Phase6RealWindowPass.cs` (fixture `Phase6Run`,
  `RealWindowRun(phase: Phase6Expectations.Phase)`), `.W.cs`, `.K.cs`, `.S.cs`, `.L.cs`.
- `scripts/time-scripted-check.sh` already sends phase 6 to `tests/VisualCommit.VisualTests.Phase${phase}`
  (`43f52c4`, `:33-34`); its default phase becomes 6 (`phase="${2:-6}"`, `:29`), the usage line and
  header comment too, with the example `bash scripts/time-scripted-check.sh "*Check_W4*"`. With part
  L's project the script sends a filter that names part L (`*Check_L*`, `*Phase6ChecksL*`) to
  `tests/VisualCommit.VisualTests.Phase6Look`, as it sends phase 5's part U to `Phase5Undo`.
- `CLAUDE.md`'s Commands: the two walk-through rows gain `dotnet test --project
  tests/VisualCommit.VisualTests.Phase6` (one part: `--filter-class "*Phase6ChecksW"`) and, once part
  L's project exists, `dotnet test --project tests/VisualCommit.VisualTests.Phase6Look` (part L); the
  timing row says 6 by default; "What to know" says nine test projects and gets
  the gate's times; the note on the real-window pass, "It needs an unlocked desktop and the scripted
  screenshots", becomes "It needs an unlocked desktop, Windows' high contrast off and the scripted
  screenshots" (11.8). `plan.md`'s "Visual test gate" step 3 names both projects. The lead adds
  `Phase6Look` to both files when it exists.

**Two projects** (the visual test report's ruling on its project size). The report's checks have 47
test methods (one per app start, a restart two; 49 app starts): W 11, K 8, S 13, G 6, L 9, where this
section first counted 38 (W 10, K 6, S 9, G 4, L 9). Past 40, by this section's rule, part L moves
whole to its own walk-through project `tests/VisualCommit.VisualTests.Phase6Look`, which links the
base (`Phase6Checks.cs`), the support (`Phase6Checks.Support.cs`) and the common expectations
(`Phase6Expectations.Common.cs`) of `VisualCommit.VisualTests.Phase6` as `Phase5Undo` links `Phase5`'s,
and saves its pictures in phase 6's one folder; the real-window project's link pattern above covers
both. `VisualCommit.VisualTests.Phase6` keeps parts W, K, S and G (38 methods), `Phase6Look` part L
(9), each project a test process of its own with its own UI thread. WP-V's part L creates the project
by WP-S's recipe (the csproj with its comment and the three links, `AssemblyInfo.cs` as above, the
timing script's route); the lead adds it to `VisualCommit.slnx`, `CLAUDE.md`'s Commands and
`plan.md`'s gate step 3 when it exists.

**Phase 6's headless App tests**: the sections' view tests (`DashboardViewTests`,
`CommandPaletteViewTests`, `KeyRouterTests`, `TabKeysTests`, `RegionKeysTests`, `CommitKeyTests`,
`MenuKeyColumnTests`, `ShellToolTipTests`, `SettingsDialogViewTests`, 11.15's theme tests,
`RepoTabStripPanelTests`, the drivers' tests) go into `VisualCommit.App.Tests` by subject; there is no
`VisualCommit.App.ViewTests` project. WP-S measured where they go before any was written (2026-10-19,
the laptop idle, `typeperf "\Processor(_Total)\% Processor Time" -si 5` beside each run): `dotnet
build`, then `dotnet test --project tests/VisualCommit.App.Tests --no-build
--output Detailed --report-xunit-trx` alone, `W_all` **21 min 16 s** for 1,319 tests, the processors at
94.3 % on average; and its headless tests alone, `W_ui` **8 min 26 s** for 449 tests, at 55.8 %: one
`--filter-class` for each of the 57 classes with `[AvaloniaFact]` or `[AvaloniaTheory]` (58 files:
`CommitGraphControlTests` is split over two) and one `--filter-not-method "*.<name>"` for each of the
214 `[Fact]` and `[Theory]` methods those classes also hold (0.10), both lists made by a script from the
attributes in every partial file of each class (`RepositoryMenusTests` has four, three without a
headless test), the filters in a response file. (`--report-trx` does not exist in this project;
xunit's `--report-xunit-trx` does.) Filtering by class alone would have counted those 214, many with
real-git scenarios, as UI-thread time. `W_ui` is 40 % of `W_all` with the processors near full, far
from the rule's 80 % with the processor well below 90 % (D230): the processors set the length (risk
12), not the UI thread. Pure tests stay in `VisualCommit.App.Tests` and `VisualCommit.Git.Tests`. The
times and the choice go into `status.md` and the handoff.

### 17.4 Proposed visual checks (the report's writers write them in full, step 6)

1100×700, dark, unless said; "Web" is the workspace copy with its ten folders. "RW" marks what the
real-window pass repeats: a picture of each new screen and what only a real window shows (the native
folder and file dialogs, a real restart, a real Ctrl+Shift+P and typing, the System theme from the
desktop).

| # | Check | RW |
|---|---|---|
| W1 | From the welcome page: its Workspaces section (`No workspaces`, `New workspace…`); a new "Web": the dashboard first and empty (`Web has no repositories yet.`, `Add repository…`, `Add open tabs`); `Add repository…` on `api` (added at once, selected), read with no F5 (`WaitForDashboardReadAsync`): `main`, `Up to date`, `Clean`; then on the copy's root: the checklist of nine, `api` `Already in this workspace`, the rest checked (no `remotes/*.git`, no `home`, no `old-site`), its folder line `Found 9 repositories in {copy.Root}.` with the folder as the fake folder dialog answered it and every drawn line but the last ending at a separator (13.2), so the checklist's picture varies by nature (the temporary folder) and the check's Notes say so, as phase 5's report does; the button `Add 8 repositories`; the eight new rows reach their states with no F5; `Add open tabs` with two repo tabs (the two rows follow their tabs: no summary-mode view model and no git call for them in `CallsSince`); `Rename workspace…`; `Delete workspace…` (`Delete workspace?`, `Delete`; nothing on disk changes; the dashboard closes); `workspaces.json` after each | yes: `Add repository…` with the copy's root chosen in the native folder dialog (`ChooseFolderInDialog("Add a repository to Web", root)`), the checklist captured against its scripted twin (the temporary folder differs, within D33's 3 %), then the section |
| W2 | The rows, read only: `Web`, `10 repositories`, no row selected; api `main`, `Up to date`, `Clean`; web-app 2 ahead; docs 3 behind; mobile 1 ahead, 2 behind; design-system `1 staged, 2 unstaged, 1 untracked`; billing `1 conflict` (danger), `Merge in progress`; infra `Detached at 09d320a`; scratch `experiment`, `No upstream`; notes `No remote`; old-site `Folder not found`; each `ExactStateAsync` unchanged, only reads called; web-app's row menu; old-site's `Locate…` (the fake folder dialog answers the folder's new place, a repository the check makes; `workspaces.json` holds the new path, and the row, read again, is `Ready`), then its `Remove from workspace` (no question); Alt+Up | yes: the rows, the row menu |
| W3 | W2 at 1920×1080, light, with the Folder column (temporary folders: asserted structurally) | no (risk 3) |
| W4 | Fetch all, the folder server (47393) holding docs, mobile and design-system at 25, 75 and 45; once held and the other five done: `Fetching from all remotes… 25%` (`75%`, `45%`) with Cancel; api, web-app, billing, infra, scratch `Fetched from all remotes`; notes `This repository has no remotes`; old-site its state; `Fetching 8 repositories… 5 done`, the bar at 62, `Cancel all`; the status bar the same, no waiting count; released: `Fetched 8 repositories; 2 skipped`, docs 4 behind, mobile 1 ahead and 3 behind | yes |
| W5 | Cancel all under the fetch hold (17.2's `HoldIn` on the eight with a remote): four running, four `Waiting to fetch…`, `4 waiting`; then the four in line `Fetch cancelled` at once, `Cancelling: waiting for 4 repositories…`, `Fetched no repositories; 8 cancelled; 2 skipped`, no dialog; after an F5 under the hold notes still `No remote`, left out with `This repository has no remotes`; a second run (`Arm()` first): a row's Cancel stops that row only | no |
| W6 | docs and mobile on the folder server with sign-in, scratch's remote `../remotes/missing.git`: mobile's first request held (`HoldNextRequest("mobile")`, 17.2) until the sign-in dialog shows `In docs`, then released and the dialog pictured; one dialog, answered once for both (4.4): the server's `Requests` show mobile's retried `info/refs` signed in as `alice`; the dialog's texts through `WithPort` (17.2); scratch `Fetch failed` with `Details…`, no dialog until clicked (D195), then the toolbar's (`Fetch failed`, `In scratch`, `fatal: '../remotes/missing.git' does not appear to be a git repository` …, the command); `Fetched 7 repositories; 1 failed; 2 skipped` (danger); then scratch's own Fetch from its row menu opens the dialog at once (D195) | no |
| W7 | Pull all, `pull.ff=only` set in mobile: api and web-app `main is already up to date`; docs `Pulled 4 commits into main (fast-forward)`; mobile and design-system `Pull failed` (D195), `Details…` giving 4.8's `PullNotFastForward` summary and `1 conflict prevents the merge. Commit, stash, or discard your changes first.`; the other five left out with 4.2's reasons; `Pulled 3 repositories; 2 failed; 5 skipped`; docs opened later: `Undo pull into main` undoes it | no |
| W8 | A double-click opens web-app after the dashboard, Enter docs after web-app; mobile's held Fetch (17.2's hold in mobile), then mobile opened: its status bar shows it, the release ends it there, one Fetch record (one queue); a commit in web-app's tab updates its row (3 ahead) without a read; under the session's manual read clock (17.2), after the read pass: a file written in scratch, `ActivateWindow()`: no call for scratch (`CallsSince(seq, scratch)`) and its row unchanged; `ReadClock.Advance(TimeSpan.FromSeconds(10))`, `ActivateWindow()`, the read awaited: the row shows `1 untracked` (2.10's 10 s rule, no real wait); the log follows the selected row; with none, `Select a repository to see its activity`; with a `Folder not found` row selected (old-site), `{path} does not exist` | no |
| W9 | A restart (two starts, phase 5's A3 pattern): `workspaces.json`, `session.json`; the dashboard first, a repo tab after it; a background dashboard calls no git until activated | yes: a real restart |
| K10 | The palette ("ops"): empty (commands with keys, the first highlighted); `delete main` (`Delete main…` dimmed with its reason); `Check out feature/alpha` by Enter (graph, status bar, `Command palette ran:` logged); `Create branch…`'s dialog, Esc back to the graph; on Web (a second method) `Open repository web-app` opens its tab (a picture that shows the item says in its Notes whether the cut detail, its folder, shows the temporary folder's random part), `Open workspace Web` activates the dashboard | yes: real Ctrl+Shift+P, typing, Enter; in that launch Ctrl+Tab, F6, `FetchButton`'s and a menu item's `AcceleratorKey` |
| K11 | The empty graph area's and the working-changes row's menus with keys (pictures); Fetch's tooltip with its key, a disabled button's without; with a file staged and a summary typed, the commit button's `Commit 1 file (Ctrl+Enter)` (D199); `Show every git command the app has run (Ctrl+J)`; an inactive tab's close button `Close this tab` without its key, the active one's `Close this tab (Ctrl+W)` | no (read in K10's launch) |
| K12 | No pointer event ("ops"): Mod+T, Mod+O (the fake folder dialog), Ctrl+Tab, Ctrl+Shift+Tab, Mod+1, Mod+9, F6 and Shift+F6 round the regions (focus outlines, pictures), Shift+F10, Down and Enter in a branch's menu, Mod+Shift+M, a summary, Mod+Enter, Mod+Z (D163), Mod+Shift+F, the Esc chain, each asserted by the command it ran; a disabled command's key does nothing | no |
| K13 | Ctrl+Tab leaves the resolver's result, Ctrl+PageDown switches from there; the resolver's `Next conflict (Alt+Down)` tooltip (D199); Ctrl+F; Ctrl+Z in a text box undoes typing only; on a dashboard (a second method, two workspaces open) Mod+Shift+F runs Fetch all, F5 reads every row, Enter, Delete, Alt+Up, Mod+Shift+D to the next dashboard | no |
| S14 | Settings: the four sections at 1100×700, General and Git at 1920×1080 (Git in the global scope it opens on, D200); opened by Mod+, `SettingsButton` and `Open settings…`; the Git section opens on `All repositories (global)`, also on a repository's tab, `This repository` one click away, and a switch keeps both scopes' edits (D200); Cancel changes nothing (`settings.json`, the global file byte-identical); Keyboard shortcuts is 6.11's list | yes: one picture (light in L34); in the same launch Tools, `Choose…` (`Choose an editor`, the Pick dialog's detail column), `Other…`, `Browse…`: Windows' file dialog `Choose the editor's program` found by its title, `notepad.exe` of the system folder (`Environment.SpecialFolder.System`) typed and confirmed (`ChooseFileInDialog`, 17.2), `EditorProgramBox` holding that path, then Cancel, nothing saved; no picture of the native dialog, which shows the machine's folders |
| S15 | Following the system (D194; "graph"; as 11.14 starts it, `"theme": "System"` in `settings.json` at the start and the fake dark, so that the first picture is a clean twin of a System start in the real window): `s15a-system-start-dark`; the fake flipped: `s15b-system-light` (no restart, the moon icon, `Switch to the dark theme (stops following the system)`, `"System"` kept, `The system's theme changed to light; the app follows it.`); a Theme click: `s15c-theme-click-dark` (`"Dark"`, `Switch to the light theme`), two flips then change nothing (byte-identical); the palette's `Follow the system's theme`: light, `"System"` saved; a restart (a second method) with the fake light: `s15d-restarted-first-frame-light` (before git is found, as phase 0's `06c`), `s15e-restarted-light` | yes, 11.8: started with System, the app mode and high contrast read only; `s15-system-start` (and `-with-frame`) against `s15e`/`s15a`, a real Theme click against `s15c`/`s15b`; with high contrast on, `run.txt` says `S15 (System at start): failed. High contrast is on, and Avalonia follows its scheme: turn it off and run the pass again.` and the method fails (the pass needs high contrast off, 11.8); the live following by hand at acceptance |
| S16 | Auto-fetch off saves 0, on with 15 saves 15, a file's 120 kept until edited (the tick's effect, 7.9, is not pictured: the harnesses force `VISUALCOMMIT_AUTO_FETCH_MINUTES=0`); `Include untracked files when stashing` on: the stash dialog's box checked | no |
| S17 | "settings": with 12.9's `PATHEXT` `run.py` opens at once, with `.py` added it asks; Visual Studio Code picked from 12.9's catalogue in the stacked Pick dialog: `Open in editor` after `Open file` in every file menu, a click recording 12.9's program and the file's absolute path | no |
| S18 | Identity ("settings", `user.useConfigOnly = true`); `IdentityInEffect` is asserted in the repository scope only, which the check chooses (the section opens on `All repositories (global)`, D200), in the app's words (8.6), the global scope having no such line: at first `Git cannot make commits here: no email is set.`; a global identity saved: `Commits here are made as {name} <{email}>.` in the repository scope, the identity in the global file and on a commit (its SHA fixed by D72); a "This repository" identity on the next commit; `A name cannot contain <, > or a line break.` | no |
| S19 | Pull mode ("settings", an identity, `pull.ff = only` seeded in the session's global file): global shows `Fast-forward only`; Pull refused with `PullNotFastForward`; under that global value, "This repository" (clicked, D200) `Rebase` saved: both keys at `--local` (8.7's per-key precedence: the repository's `pull.ff = true` beats the global `only`), `Pull into main: Rebase (pull.rebase = true in .git/config).`, and Pull rebases `main`; `Same as global` unsets both; then written by the check, `branch.main.rebase = false`: 8.8's notice, removed by `Use the repository's setting` and Save; `pull.rebase = preserve`: `Other: pull.rebase = preserve`, `Pulls fail while this value is set.`, Pull failing before any fetch (8.9) | no |
| S20 | "conflicts", the resolver open, `mergetool.slow.cmd`: `Merge tool…` disabled (`Set merge.tool in git's configuration to use a merge tool`); the Pick dialog lists 12.9's tools as 8.10 filters them (no `vimdiff`) and `slow` (`your own tool (mergetool.slow.cmd)`); `slow` saved enables `Merge tool…` without reopening (8.11); `Not set` disables it | no |
| S21 | "ignored": Keep ignored files off, `Check out vendor` runs as git does (`secret.env` becomes `vendor`); on, on a fresh copy, refused; and, with the setting off, checking out the commit "Track the secret" (12:01) from the graph is still refused (D179, 9.1): `Checkout blocked`, `1 ignored file is in the way: <short sha> tracks a file of the same name. Move it away first.`, command `git switch --detach --no-overwrite-ignore <sha>`, `secret.env` still `LOCAL` | no |
| G22 | "ignored": `Check out vendor` refused (`Checkout blocked`, `1 ignored file is in the way: vendor tracks a file of the same name. Move it away first.`, command `git switch --no-guess --no-overwrite-ignore vendor`); `origin/vendor`'s checkout goes into the local `vendor` (D93) and is refused with the same command, `origin/vendor` in the text (9.11's `plan.Name`); `origin/vendor-remote`'s tracking checkout refused (`Checkout blocked`, `1 ignored file is in the way: origin/vendor-remote tracks a file of the same name. Move it away first.`, command `git switch --create vendor-remote --track --no-overwrite-ignore refs/remotes/origin/vendor-remote`), no `refs/heads/vendor-remote` and no undo entry after it; `Create branch…` with checkout at `vendor` refused, no branch made; `tools` refused for `build/`; `secret.env` and `build/out.bin` intact | no |
| G23 | "ignored": merge `vendor`, rebase `main` onto `vendor`, cherry-pick "Add the vendor secret", revert "Stop tracking the secret", apply and pop the stash: refused with `Merge failed`, `Rebase failed`, `Cherry-pick failed`, `Revert failed`, `Applying the stash failed` and 14.4's texts naming `secret.env`, no git output; `ExactStateAsync` unchanged; only reads before each | no |
| G24 | Off in `settings.json`: the merge runs as git does (Q5), overwriting `secret.env`; the checkout takes git's default; no `--no-overwrite-ignore` | no |
| G25 | "refspecs": `topic` dropped onto `fork/up/main` pushes `fork`'s `main` (no `up/main` on `fork.git`); onto `origin/pr/1` the push is disabled with R2 (`origin/pr/1 is fetched from more than one ref of origin`), its deletion too, while its checkout creates `pr/1` without an upstream (D201: `git switch --create pr/1 --no-track --no-overwrite-ignore refs/remotes/origin/pr/1`, no `branch.pr/1.*` key, the status bar `Created pr/1 at origin/pr/1 without an upstream: origin/pr/1 is fetched from more than one ref of origin`), and, with `main` checked out again, its double-click checks out that `pr/1`; Push of `pr/1` through the push dialog to `origin` is then refused (`Push failed`, `Pushing pr/1 to origin/pr/1 would stop every fetch of origin: origin/pr/1 is fetched from more than one ref of origin.`, no git output, `origin.git` without `refs/heads/pr/1`); `upstream/pr/7`'s deletion disabled with R4 (`upstream/pr/7 is refs/pull/7/head on upstream, not a branch`), while its checkout stays enabled and creates `pr/7` tracking `refs/pull/7/head`, as git does, Pull's tooltip then naming the upstream `upstream/pr/7` (`Pull from upstream/pr/7 (Ctrl+Shift+L)`, 10.4's upstream name, GD15); `fork/up/main`'s `Delete main on fork…` (10.4 #6 and #10 and its push guard, 10.5, 10.6) | no |
| L26 | Sweep (11.14), the empty tab (a recent list, a workspace): the welcome page, the recent entries' folders cut with "…" as in phase 1's `10a` (the report lists `l26a` as varying by nature only if the Windows run shows the temporary folder's random part); the clone form | no |
| L27 | Sweep, "ops": a branch drag's pill and outline (flipped mid-drag); three rows selected; `Delete feature/alpha…` under the pointer in a menu; a disabled toolbar button's and a disabled menu item's tooltips; Create branch…; the push dialog without upstream; Manage remotes… | no |
| L28 | Sweep, "history": `src/Reader.cs`'s change (stage row `M`, inline diff in C#); the restore bar after a discard, then Restore; its blame (`Not committed yet`); two commits compared; `All files`; `feature/compare` hidden and another branch soloed (11.14) | no |
| L29 | Sweep, "reflog": HEAD's reflog; the backups page | no |
| L30 | Sweep, Web: the rows; a row's menu; the scan's checklist, its folder line asserted as W1's (`Found 9 repositories in {copy.Root}.`, 13.2's drawn lines), so `l30c-scan-checklist-dark` and `-light` vary by nature and their Notes say so | no (W2; the checklist W1's) |
| L31 | Sweep, two methods. "ops": `l31a-activity-log`; `l31b-delete-question` (the danger button's `VcDangerTextBrush` asserted, 11.11); `l31c-palette`; `l31d-settings-system` (General, System checked); `l31e-settings-git` (on `All repositories (global)`, as it opens, D200; the merge tool's Pick stacked); `l31f-enabled-tooltip` (Fetch's, with its key); last, Light saved and the screen opened again, the single `l31d-settings-light` (11.8). "auth", its server on 47394 (17.2): the credentials dialog, its texts through `WithPort`; the fetch held at its percentage (progress, Cancel, a waiting operation), then Cancel | yes: L34 |
| L32 | The long folder (13.2): each drawn line of `WelcomeError` but the last ends with a separator or a space, none is `C:`, the text unchanged; both themes; the clone error likewise | no (phase 1's `11a`) |
| L33 | The strip (13.3's numbers and rule: nothing moves while the tabs fit at their natural widths, 8 of 120 at 1000, at least 4 of any title): 8 tabs of 120 at 1000 in today's places; the Web dashboard, its nine repositories that can open (old-site cannot) and one tab from "+" at 1100: six of 97, five of 96, "+" at the end; a twelfth tab: 88 or 89 each, no scrolling; the strip scrolls from 14 tabs at 1100 (80 each), the active tab in view; Ctrl+Tab round to the first brings it into view. The twelfth tab and those up to 14 come from "+" too (`New tab`, natural width 120); every natural width is at least 120, more than each cap, so which tabs they are changes no width | no |
| L34 | Real window only (11.8): `"theme": "Light"` on "ops": `l31a-activity-log-light`, `l31b-delete-question-light`, `l31c-palette-light` (real Ctrl+Shift+P), `l31d-settings-light` (the settings key), the first three against their pairs' `-light` pictures, `l31d-settings-light` against L31's single picture of that name (System's light draws Light's pixels, 11.15) | yes |

L26 to L31 picture each step in pairs by `ShotInBothThemesAsync`, but for the single
`l31d-settings-light` (11.8); what 11.14 leaves unpictured (the
clone in progress, the reload bar, the choice and finishing dialogs, the passphrase, host-key and
prompt dialogs, the overflowing strip in both themes) stays so. Part L is 9 methods (L31 two), in
`VisualCommit.VisualTests.Phase6Look` (17.3); L34 has no scripted method. As the visual test report
writes the checks (its ruling on the project size): 47 methods, 49 app starts (W 11 with 12, K 8, S 13
with 14, G 6, L 9) and 198 scripted pictures (W 45, K 17, S 42, G 26, L 68), where this section first
estimated about 156 (W about 32, K 14, S 28, G 14, L 66); the real-window pass repeats 8 methods in 9
launches (W1, W2, W4, W9 with its restart, K10, S14, S15, L34) and takes 26 captures (W 14, K 4, S 4,
L 4).

### 17.5 Each "Delivers" item and what proves it

| Delivers (plan.md phase 6) | Proven by |
|---|---|
| Workspaces in `workspaces.json` and the dashboard tab with bulk fetch and pull (D187 to D189) | W1 to W9 (`Locate…` on a gone folder in W2), K13; 1.12's tests, by which alone a saved folder that lies inside another repository is proven (`WorkspaceFolderProbeTests`); 2.12's (2.8's branch part, the bindings on a dashboard tab, `OperationStatusViewModel.Compact*`); 3.9's; 4.13's, among them `DashboardToolbarTests` (4.10's states and reasons) and `DashboardBulkViewModelTests` (the status bar's run part); `Phase6ScenarioTests` |
| The command palette and the fixed keys, shown in the palette, menus and tooltips and listed in the settings screen (D191, D192) | K10 to K13, S14; 5.12's and 6.12's tests |
| The settings screen, git's identity, pull mode and merge tool at both scopes, theme polish and System (D190, D194) | S14 to S21, L26 to L31, L34; 7.11's, 8.13's and 11.15's tests, 8.13's alone for an included value's "Still" notice (8.4) and a held `.git/config.lock` (8.5); 17.2's isolation tests |
| From phase 1: the long folder | L32; 13.5's `PathTextBlockTests`, phase 1's check 11 on the drawn lines |
| From phase 3: `AutoFetchMinutes`, `StashIncludeUntracked`, Open file's extensions, the pull mode through `pull.rebase`, `pull.ff`, `branch.<name>.rebase` (D78, D84), Open in editor | S16, S17, S19; 7.11's and 12.10's tests, `AutoFetchSchedulerTests` (the interval from the setting); `PullParityTests` unchanged |
| From phase 4: `merge.tool` (D149) chosen on the screen | S20; 8.13's `MergeToolHelpTests`, `GitSettingsViewModelTests` |
| From phase 3: bulk runs through each `OperationQueue` with the toolbar's titles, Cancel and error dialog; auto-fetch only for the active tab | W4 to W8; 4.13's `BulkRunTests`, `RepositoryOperationsFetchTests`, `RepositoryOperationsPullTests`, `AutoFetchSchedulerTests`; 3.9's `PromotionTests` |
| From phase 3: `remote.<r>.fetch` for the drop's push and "Delete <b> on <r>…" | G25; 10.9's tests |
| From phase 5: ignored files kept by every checkout and the operations 9.2 adds, a setting to turn it off (D193, D196: every checkout; every merge, the pull's included; every rebase, that is onto, "Check out and rebase", interactive, the squash and the pull's; cherry-pick, revert, stash apply and pop; not the hard reset) | G22 to G24 (checkouts, merge, rebase onto, cherry-pick, revert, the stash), S21; 9.17's tests, by which alone the interactive rebase, the squash, "Check out and rebase" and the pull's merge and rebase are proven (`IgnoredInTheWayTests`, `IntegratePullTests`, `RepositoryOperationsKeepIgnoredTests`); P24 to P27 changed on purpose (16.1) |
| The visual checks: ten repositories and bulk fetch progress; palette search and run; settings taking effect; the sweep in both themes | W2 to W4; K10; S15 to S21; L26 to L31, L34 |
| Done when: ten repositories checked and updated from one screen; every main action from the keyboard; the gate | W2, W4, W7; K12; 17.7. Read as plan.md's "ten repos in mixed states" asks: W2 reads all ten rows on one screen; one Fetch all (W4) and one Pull all (W7) each decide every one of the ten rows; every row that can be updated is (all eight with a remote are fetched, none failing), and the rows that cannot be updated give 4.2's reason. The handoff states this reading for the owner to confirm at acceptance |

Harness items that are files are proven by the files and their tests (the handoff names them): the
two projects in `VisualCommit.slnx`, the real-window link, the timing script, `CLAUDE.md`'s rows; the
scenarios by `Phase6ScenarioTests`, port 47393 and the folder server by `AuthRemoteServerTests`, the
hold by `FetchHoldTests`, the isolation by `ShellTests` and `GitIsolationTests`, the drivers by theirs.

### 17.6 Run time

Estimates from 0.10 and WP-S's measurements (17.1, 17.3); none of phase 6's UI has run.

- **The walk-through**: first estimated at 38 methods and about 156 pictures; as the visual test
  report writes the checks, 47 methods (49 app starts) and 198 pictures in two projects (17.3: 38
  methods and 130 pictures in `VisualCommit.VisualTests.Phase6`, 9 and 68 in `Phase6Look`). At phase
  5's 4.8 s per picture, the dashboard's starts heavier (72 processes for the ten rows' first reads: 9
  rows × (six refs reads, the status read and `OpenAsync`'s), old-site none, billing's merge none, as
  `MERGE_HEAD` is read as a file; 81 on CI's Windows and macOS, where `OpenedAtTopAsync` adds a
  `--show-prefix` per row, 1.2) and the sweep's flips lighter, the 156 pictures came to about 700 to
  900 s of UI-thread time, and the report's 42 more add about 200 s, about 900 to 1,100 s in all, not
  yet measured. At phase 5's wall-to-UI ratios (1.67 idle, 2.6 at the gate) one project of 156
  pictures ended 20 to 39 minutes into the run; the two projects run side by side, each a test process
  with its own UI thread and fewer pictures than that, so each should end sooner, before
  `VisualCommit.App.Tests` (37 min 26 s, 43 min 8 s) unless that grows.
- **The default run**: 37 min 29 s idle at step 2, 43 min 22 s at phase 5's gate; rested, 43 min 49 s
  for 3,236 tests at `3f62463` before WP-S and 43 min 33 s for 3,299 at WP-S's head (17.1). So the
  laptop's state moves nearly the same tests by some 6 minutes, and WP-S's own tests (scenarios, folder
  server, hold, isolation) cost nothing measurable, the hook samples' removal included. The run gains
  the walk-through's processor load (about 1,250 git processes in part W: some 14 dashboard starts at
  72, four bulk runs, the reads after them; the hold's polling, under a tenth of a core per held fetch
  while it lasts, is under 1 % of the run's processor time), the Git-layer tests after WP-S
  (configuration reader and writer, ignored-files check, refspec parity) and the view tests, which go
  into App.Tests (17.3): **+5 to +10 minutes** (r10's +3 to +8 left out the sweep and the view tests),
  about 49 to 54 rested from WP-S's head. Past 50 minutes rested, which most of that range passes, the
  lead looks again at sharing app starts in the sweep before the gate. Phase 6 also adds reads to
  earlier checks and tests: section 9's reads before every covered operation of phases 3 to 5 (merge 1,
  pick 2, rebase 5, or 7 with a merge in the range, 9.17) and 8.9's pull configuration read before
  every pull's fetch, the chosen rebase's `pull.ff` read included (D231). They are not counted here:
  once WP-IA and WP-B are merged, the lead counts them from the call logs and runs the default tests on
  the commits before and after, alternately (risk 10's method), to measure their cost. A refs read keeps its five processes
  (10.2), so the dashboard's figures above stand.
- **The view tests**, about 18 to 20 new or extended headless classes (17.3's list, from 2.12, 4.13,
  5.12, 6.12, 7.11, 11.15 and 13.5), counted by method and app start as they are written. With WP-S's
  cost per headless test (`W_ui` / 449, about 1.1 s alone, 17.3), `n` tests add `n` × that cost × the
  load ratio to App.Tests' end, where they go (17.3), and the run grows with it.
  `KeyRouterTests`, `TabKeysTests` and `RegionKeysTests` press many keys per app start, as
  `WindowKeysTests` does: one "ops" start per condition (plain, a menu open, a dialog open, a drag), not
  one per key.
- **CI's Windows Test step**: 14 min 59 s on phase 5's merge (run 39078809291), 24 min 38 s on phase
  6's first run (39094444954), 25 min 15 s at phase 5's gate. With 15 to 25 % more, 17 to 19 minutes on
  a fast runner and 28 to 31 on a slow one, near D186's 32: past 32 on Windows or macOS the lead raises
  the limit and records it. macOS: 16 to 18 minutes today, 19 to 22 then.
- **The real-window pass** (24 min 57 s for phases 0 to 5): 8 methods (W1, W2, W4, W9, K10, S14, S15,
  L34) in 9 launches and 26 captures, the workspace template (28 processes) and the folder server; at phase 5's 41 s
  per method and more for the dashboard: **+5 to +8 minutes**, 30 to 33 in all; W1's checklist capture
  and S14's Browse… step add some seconds within that.

### 17.7 The gate's inspection of earlier pictures

As phase 5's (its design 15.7, handoff "Running the visual gate"): compared, not read afresh. The
Settings button (7.2, D197) changes every window picture of phases 0 to 5 in one toolbar box, so
`phase-5-pictures.sha256` names them all as changed: the comparison is by region, and outside the
regions 16.1 names for a picture nothing may change.

1. **References**, after the review (status step 22) and before the gate (status step 23), whose
   real-window pass is the first in the main checkout: status step 21 writes and builds the pass and
   does not run it, as phase 5's WP9 did, and a trial run stays in WP-R's worktree (section 18). Two
   parts: the phase 0 to 5 walk-throughs (five projects: `VisualCommit.VisualTests` for phases 0 to 2,
   `.Phase3`, `.Phase4`, `.Phase5`, `.Phase5Undo`) on `master` in a worktree of their own (phase 5's
   merge and its status commits), checked against `docs/test-reports/phase-5-pictures.sha256` (774
   scripted pictures; those phase 5's report and handoff call varying by nature, such as a temporary
   folder or a caret, inspected in full); and today's real-window files from the main checkout (306 in
   the list, untouched by step 2's baseline), checked against the same list. Both go to
   `D:/Projects/Personal/gate6-reference/`. The worktree (about 5 GB, risk 27) is removed at once. The
   real-window copy, about 12 MB and no worktree, may be made at any earlier time, so that it does not
   hang on the order of the steps.
2. **The comparison**, right after `docs/test-reports/phase-6-pictures.sha256` is written:
   `python scripts/compare-pictures.py D:/Projects/Personal/gate6-reference artifacts/visual <output>`
   (a difference picture per changed picture, `summary.md` by region; no change needed: `regions_of`,
   `compare-pictures.py:39-63`, places a change as for any picture).
3. **What may change** is 16.1's list, each recorded under "Changes to expected results" with its
   region. For the welcome page, 16.1's list is the pictures of an empty tab, while phase 1's `13a`,
   `13b` and `14b` keep their layout (the clone form hides the Workspaces section) and `14a` moves
   only through 13.2's line breaking, if at all. Every listed empty-tab picture changes the same way:
   the Workspaces section added between `Init` and `Recent repositories`, the stack 34 px higher (less
   where the page scrolls); the inspectors check that one layout against 13.1 once for each window size
   and theme, and any other difference in the welcome region is a defect. Nothing moves in the tab strip of an earlier picture: by 13.3's rule nothing
   moves while the tabs fit at their natural widths (8 of 120 at 1000 wide, at least 4 of any title),
   and none has more than 3 tabs. The references on `master` lack the phase branch's fix before WP0,
   `43751d0` (the stage lists keep the rows a status read did not change), which 16.1 lists too: no
   picture is expected to change from it (phase 2's walk-through, run with it, matched 65 of its 66
   pictures byte for byte, and `17c-restored` differed by 8 anti-aliased pixels), so it is compared like
   every change on the phase branch, not hidden by references made after it.
4. **The inspectors**, Opus agents side by side: phase 6's pictures in full against the report (W, K,
   S, G, and L in two batches); its real-window captures with their twins; the earlier scripted
   pictures in four batches (phases 0 to 2, 3 in two, 4, 5 in two) against reference, difference
   picture and 16.1, in full where a change lies outside 16.1's regions; earlier real-window captures
   against their new twins through the pass's difference pictures; CI's macOS and Linux light sweep
   pictures (other fonts), named in the report.
5. After a fix both passes run again on the new commit; only pictures whose bytes changed since the
   last list are inspected again.

## 18. Work breakdown

Each package is built in its own worktree on a branch made **from `phase/6-multi-repo`**
(`wip/p6-<name>`; a worktree may start from `master`, so its agent creates the branch from the phase
branch first), merged back with `--no-ff` after its tests pass and the solution builds, its worktree
removed at once and its fsmonitor daemon stopped (phase 5's first worktree needed `git worktree remove -f
-f`). **At most 3 built worktrees exist at once**: a built worktree is about 5 GB on D: (20 GB free) and
the tests' repositories land on C: (15.7 GB free); 17.7's reference run in a worktree counts as one. Work
that needs no build (writing and reviewing the report's checks, reading reviews) runs in the main
checkout or a plain clone and does not count.

**One owner per file and per test class.** A package edits only the files named for it; anything else
goes through the lead. A file that passes from one package to another passes at the merge the order
names. A test class has the owner of the class under test; a package whose new tests concern another
owner's class writes them in a new class of its own, named below. An existing earlier test or
expectation file that several packages must change (16.1) has one owner at a time, in merge order, each
changing only the lines its change moves.

| Package | Builds and owns | Test classes |
|---|---|---|
| WP-L (lead, first) | `MSYSTEM` removed by `GitIsolation.MachineIndependence`, a commit of its own in the main checkout before WP0 (the other leftovers are in `43f52c4`); `GitIsolation.cs` then passes to WP-S | `GitIsolationTests`' `MSYSTEM` case (the class then passes to WP-S) |
| WP0 (lead, main checkout) | Contracts, stubs and call points every package builds against, with no change of behaviour or pictures (below) | the stubs' compile fixes in the fakes (`MainWindowViewModelTests`' `FakeThemes` among them: `Setting`, `IsDark`, `Changed` for `Current`) |
| WP-S (harness) | 17.1's scenarios (`TempWorkspace.cs`, `LeanRepositories.cs`, `Scenarios.Phase6.cs`, `Scenarios.ServeWorkspaceAsync`); the hook samples left out (a commit of its own, measured before and after); `TempRepo.cs` (`CopyAsync`'s relative URLs, `AddBareRemoteAsync(…, relative)`, `Over`), `Scenarios.cs` (generic templates); `GitIsolation.cs` (`SessionGitEnvironment`, the safety net, the XDG folder, the identity variables, `PATHEXT`); `GitRunner.cs` (the session environment applied, `SessionEnvironment`; `GitCommand.BelowNormalPriority` applied on Windows right after the start to git and what it has started by then, 8.10), `GitLocator.cs` (the session environment); `ShellDriver.cs` (`Start`'s environment, `systemDark`, `readClock`, `ToolCatalogues.Fixed`, `GlobalGitConfigPath`, `SeedGlobalGitConfig`) and `ShellDriver.Checks.cs` after WP0; `Headless/FakeSystemTheme.cs`; `RealApp.cs` (the per-launch environment; `ChooseFileInDialog(title, file, timeout)`, 17.2); `AuthRemoteServer.cs` (folder mode, holds per repository, 47393); `FetchHold.cs`; the project `VisualCommit.VisualTests.Phase6` (csproj, `AssemblyInfo.cs`, `Phase6Checks.cs`, `Phase6Checks.Support.cs`, `Phase6Expectations.Common.cs`, `Phase6SupportTests.cs`) in `VisualCommit.slnx`; the real-window csproj's link; `scripts/time-scripted-check.sh`'s default phase; 17.3's measurement of `VisualCommit.App.Tests` (done: the view tests stay in App.Tests, no view-test project); the commands handed to the lead for CLAUDE.md | `Phase6ScenarioTests`, `GitIsolationTests` (the tripwire), `ShellTests`, `AuthRemoteServerTests`, `FetchHoldTests`, `Phase6SupportTests` |
| WP1 (Git: keeping ignored files) | `GitRepository.KeepIgnored.cs` (`FindIgnoredAtAsync`, the five reads, `IntegratePullAsync`'s check), `GitRepository.Branches.cs` (the option on the checkouts and the merges, one `CheckoutDetachedAsync`, `CreateBranchAsync`'s `noTrack`, 9.11), `GitRepository.Undo.cs` (the helper lifted out), `StashPaths.Deleted`'s reading; Core `RebaseTodo.cs`'s `LeadingPicks` and `AppliedCommits` | `KeepIgnoredCheckoutTests`, `IgnoredInTheWayTests`, `RebaseTodoTests`, `IntegratePullTests` (new); the Git tests 16.1 names (`BranchOperationTests.cs:217`, `DashBranchNameTests.cs:78`) |
| WP2 (Git: git's configuration and pull's settings) | `src/VisualCommit.Git/GitConfiguration.cs` (with 8.10's kept list, `merge-tools.json`), `GitMergeTools.cs`, `GitRepository.Config.cs` (`WriteConfigAsync`), `GitRepository.PullConfig.cs` (`ReadPullConfigurationAsync`), `GitRepository.Remotes.cs` (`IntegratePullAsync` by a given configuration, `RemoteArguments.NotUnderstood`); Core `PullModes.cs`, `GitBoolean.cs` (8.7), `GitConfigLocations` | `GitConfigReadTests`, `GitConfigWriteTests`, `GitConfigLocationsTests`, `GitIdentityTests`, `PullConfigurationTests`, `MergeToolHelpTests`, `PullModesTests` |
| WP3 (Git: fetch refspecs) | Core `FetchRefspecs.cs` (with `DestinationsOf`) and `RepoRefs.cs`' mapping; `GitRepository.Refs.cs` (one `git config --null --get-regexp ^remote\.` in place of `git remote`, 10.2; `RemoteName` from the mapping) | `FetchRefspecsTests`, `FetchRefspecsParityTests`, `RefReadingTests` |
| WP4 (queue, prompts, workspaces' data) | `OperationQueue.cs` (`Cancel`, the operations merged into an entry, the group passed to `Begin`), `AskPassBroker.cs` (`MaxConnections` 16, the scope's group), `Services/PromptDialogs.cs` (groups); Core `WorkspacePaths.cs`, `WorkspaceEdits.cs`, `RepositoryScanner.cs`; `Services/JsonWorkspaceStore.cs` (with `IWorkspaceFiles`, the set-aside names, `Unreadable` and `SaveFailed`, 1.3), `Services/JsonSessionStore.cs` (`WorkspaceId`); `GitRepository.cs` (`OpenedAtTopAsync`); `ShellDriver.Phase6.Workspaces.cs` with `WriteWorkspaces` and `StartWithTabs(…TabState…)` (the file passes to WP-W at WP4's merge) | `OperationQueueTests`, `AskPassTests`, `PromptDialogsTests`, `WorkspaceEditsTests`, `WorkspacePathsTests`, `RepositoryScannerTests`, `JsonWorkspaceStoreTests`, `JsonSessionStoreTests`, `GitRepositoryOpenTests` |
| WP-W (workspaces on screen, the welcome page, the tab strip) | `ViewModels/Workspaces/WorkspaceItems.cs`, `WorkspaceCommands.cs` (WP0's stub filled), `IWorkspaceHost.cs`, `WorkspaceMembership.cs`, `WorkspaceFolderProbe.cs` (after WP0), `WorkspaceTexts.cs`; `Dialogs/WorkspaceNameDialogViewModel.cs`, `AddRepositoriesDialogViewModel.cs`, `Views/Dialogs/AddRepositoriesDialogView.axaml(.cs)`; `Dialogs/DialogText.cs` (`WorkspaceNameError`); `Menus/WorkspaceMenus.cs`; `WelcomeViewModel.cs`, `Views/Shell/WelcomeView.axaml(.cs)`; `Controls/PathTextBlock.cs`; `Views/Shell/RepoTabStripPanel.cs`, `RepoTabsView.axaml` (the panel, the dashboard's icon, padding, id and `ItemStatus`, "+"'s name `Open a new tab`, 13.4); `MainWindowViewModel.Session.cs` (1.11's restore partition, the saved states, `ActiveTabIndex`); `ShellDriver.Phase6.Workspaces.cs` (after WP4), `.TabStrip.cs`, `Headless/DrawnLines.cs`; the scripted expectations of 16.1's welcome-page and long-folder rows | `DashboardSessionTests`, `WorkspaceNameErrorTests`, `WorkspaceCommandsTests`, `AddRepositoriesDialogViewModelTests`, `WorkspaceFolderProbeTests`, `PathTextBlockTests`, `WelcomeWorkspacesTests`, `RepoTabStripPanelTests` (all new) |
| WP-D (the dashboard, summary mode, the registry) | `ViewModels/Workspaces/DashboardViewModel.cs` (`ActivityLogReason`, 2.9), `DashboardRowViewModel.cs` (the read off the UI thread, the `directoryExists` seam, the row's token, 2.10), `DashboardTexts.cs`, `OpenRepositories.cs` (with `WhenIdleAsync` and `NetworkQueueOn`, 3.4, 4.3); `Menus/DashboardMenus.cs`; `Views/Workspaces/DashboardView.axaml(.cs)` (`DashboardNotice`; `KeyRegion.IsMain` on `DashboardList`, 6.6), `DashboardColumns.cs`, `DashboardRowPanel.cs`; `RepositoryViewModel.cs`, `.InProgress.cs`, `.GraphArea.cs` (summary mode, `Promote`, `FirstRead`, the deferred refresh, the read generation, `PauseWatcher()`, 8.11's `GitSettingsChanged` subscription in the constructor and `Dispose` and its handler in `.GraphArea.cs`); `RepoTabViewModel.cs` (2.1's members, `ShowDashboard` filled, the wait for a closed queue, adoption and registration); `MainWindowViewModel.Workspaces.cs` (`ShowWorkspace`, `OpenRequested`, 3.7's view models to wait for, 3.8's term); `OperationStatusViewModel.cs` (`CompactText`, `CompactTone`, `CompactToolTip`, `RunningTitle`, `RunningIsCancellable`); `Panels/ActivityLogViewModel.cs` (following a row); `Services/GitRepositoryProvider.cs` (the open on the thread pool, 2.10); `Views/MainWindow.axaml` (the left panel's and splitter's visibility, `Grid.Column`); `Views/Shell/CommitGraphView.axaml(.cs)` (the slot, the focus, `TabWaitingText` (3.3), `GraphAreaFocusTarget()`: `FocusGraphAreaContent`'s choice returned as an element for 6.6's navigation); `Views/Shell/StatusBarView.axaml` (`StatusWorkspaceIcon`); `ShellDriver.Phase6.Dashboard.cs` | `DashboardColumnsTests`, `DashboardTextsTests`, `DashboardMenusTests`, `DashboardTabTests`, `DashboardRefreshTests`, `DashboardActivityLogTests`, `DashboardViewTests`, `SummaryModeTests`, `PromotionTests`, `OpenRepositoriesTests`, `MainWindowCloseTests`, `GitSettingsChangedTests` (all new); `OperationStatusViewModelTests`, `RepoTabsTests` |
| WP-B (bulk fetch and pull, Pull's app path) | `ViewModels/Workspaces/BulkRun.cs`, `BulkSlots.cs` (`IsHeldFor`, the common-folder checks, 4.3), `DashboardBulkViewModel.cs` (after WP0's stub); `ToolbarViewModel.Dashboard.cs` (4.10's `ToolbarViewModel.ForDashboard(IDashboardToolbar)`); `ViewModels/Operations/DeferredDialog.cs`; `ViewModels/Operations/AutoFetchScheduler.cs` (4.3's `CommonFolderBusy`, which the lead sets at `MainWindowViewModel.cs:99` at WP-B's merge); `RepositoryOperations.Run.cs` (`ShowResultAsync`'s deferring, `ShowDeferredAsync`, `GuardAsync`'s variant, the lock-file dialog split); `RepositoryOperations.FetchPull.cs` (`FetchAllAsync(BulkPart)`, `PullAsync(BulkPart)`, `PullRefusalDisplay`'s `PullNotFastForward`, 8.9's read before the fetch, the chosen rebase's `pull.ff` included (D231), 9.10's `IgnoredFilesInTheWay`, `PullIntegrationOptions` passed); the rebase stop's deferral in `RepositoryOperations.Rebase.cs` (after WP-IA's merge); `OperationTexts.Bulk.cs`, `OperationTexts.Remotes.cs` (`PullNotFastForward`); the bulk parts of `DashboardViewModel.cs`, `DashboardRowViewModel.cs` and `DashboardView.axaml` (handed over at WP-D's merge); `ShellDriver.Phase6.Bulk.cs`; `Phase3Expectations.D.cs` (`PullRefusedDialog`, `:170`, `:240`) | `BulkSlotsTests`, `BulkRunTests`, `DashboardBulkViewModelTests`, `DashboardToolbarTests` (new); `RepositoryOperationsFetchTests`, `RepositoryOperationsPullTests`, `AutoFetchSchedulerTests`, `FailureClassifierTests` |
| WP-K (keys and the palette) | `ViewModels/Commands/` beyond WP0's stubs (`AppCommands.cs`, `PaletteEntry.cs`, `PaletteMatcher.cs`, `CommandPaletteViewModel.cs`, the formatter's and catalogue's bodies, `ShellToolTips.cs`' texts with keys, its resolver, list and commit members with their `{name}Key` texts, 6.8); `RepositoryViewModel.Palette.cs`; `Views/Commands/CommandPaletteView.axaml(.cs)`; `Views/KeyRouter.cs`, `Views/ShortcutKeys.cs`, `Views/KeyRegions.cs`' navigation (no view file: the marks are WP0's and WP-D's), `MainWindow.Keys.cs`; `MainWindowViewModel.Commands.cs`; `ToolbarViewModel.cs` (tooltips with keys); `ViewModels/Panels/WorkingChangesViewModel.cs` (`CommitDisabledReason`, `CommitToolTip` with its key), `Views/Shell/WorkingChangesView.axaml` (`CommitButton`'s help text and `AcceleratorKey`), `Views/Conflicts/ConflictResolverView.axaml` (its five buttons' tooltips, help texts and `AcceleratorKey`s from `ShellToolTips`), `Views/Rebase/InteractiveRebaseView.axaml` (`RebaseCancelButton`'s `Cancel (Esc)`), `ViewModels/Graph/SearchViewModel.cs` and `Views/Graph/SearchBarView.axaml` (`Previous match (Shift+Enter, {key})`, `Next match (Enter, {key})` and their keys); `Views/Shell/MenuLayer.axaml.cs`, `Theme/Menus.axaml`; `Theme/Dialogs.axaml`'s palette classes (the file passes from WP-SET at its merge); `Views/Dialogs/DialogView.cs` (Mod+Enter); `Headless/MenuSnapshot.cs` (`KeyText`), `ShellDriver.Phase6.Palette.cs`, `.Keys.cs`; the earlier expectations of 16.1's tooltip and keyed-menu rows | `PaletteMatcherTests`, `CommandPaletteViewModelTests`, `PaletteEntriesTests`, `CommandPaletteViewTests`, `ShortcutTextTests`, `AppShortcutsTests`, `ToolbarToolTipTests`, `RepositoryMenusKeyTests`, `KeyRouterTests`, `TabKeysTests`, `RegionKeysTests`, `CommitKeyTests`, `MenuKeyColumnTests`, `ShellToolTipTests` (all new); `ToolbarTests`, `ActivityLogViewModelTests`, `StatusBarTests`, `WindowKeysTests`, `SearchBarTests`, `WorkingChangesInProgressTests` (the lines 16.1 names) |
| WP-SET (the settings screen, git's settings on screen, the editor, Open file) | `Dialogs/SettingsDialogViewModel.cs`, `ViewModels/Settings/` (`GeneralSettingsViewModel`, `GitSettingsViewModel`, `ToolSettingsViewModel`), `Views/Dialogs/SettingsDialogView.axaml(.cs)`; `PickDialogViewModel.cs`, `PickDialogView.axaml`; `Theme/Dialogs.axaml` (then WP-K); `Views/Shell/ToolbarView.axaml` (`SettingsButton`); `MainWindowViewModel.Settings.cs` (`OpenSettingsCommand`; it sets WP0's `TabServices.OpenSettings`); `RepositoryViewModel.Settings.cs` (it only fills the menu context's editor name, 12.1, with no subscription); `RepositoryOperations.Settings.cs`, `RepositoryOperations.Files.cs` (`OpenInEditorAsync`'s body behind WP0's declaration, 12.7's dialog through `TabServices.OpenSettings`); `OperationTexts.Settings.cs`; `Services/KnownEditors.cs`, `EditorCatalog.cs`, `EditorCommands.cs`, `FolderPicker.cs` (`PickFileAsync`), `IFileLauncher.cs`, `FileLauncher.cs`; `Headless/FakeFileLauncher.cs`, `FakeFolderPicker.cs`, `HeldMergeTools.cs`, `ShellDriver.Phase6.Settings.cs`; the expectations of 16.1's Settings-button row | `SettingsDialogViewModelTests`, `SettingsDialogViewTests`, `PickDialogViewModelTests`, `SettingsEntryTests` (the button, `OpenSettingsCommand`), `GitSettingsViewModelTests`, `RepositoryOperationsSettingsTests`, `KnownEditorsTests`, `EditorCatalogTests`, `EditorCommandsTests`, `FileMenuEditorTests` (12.10's file menus) (all new); `JsonSettingsStoreTests`, `FileLauncherTests`, `RepositoryOperationsFilesTests` |
| WP-T (the theme) | `Services/SystemTheme.cs`, `Services/ThemeService.cs` (behind WP0's signature); `MainWindowViewModel.Theme.cs` (after WP0's move: `Theme` from the service, `UseTheme`'s body, the service's `Changed`); `Theme/Tokens.axaml`, `Theme/Controls.axaml` (`VcDangerButton`); `Views/Shell/DragLayer.cs`; `ShellDriver.Phase6.Theme.cs`, `VisualCommit.Testing/Contrast.cs`; the earlier expectations of 16.1's colour rows | `SystemThemeTests`, `ThemeServiceTests`, `SystemThemeStartTests` (11.15's starts), `DragLayerTests`, `ThemeTokensTests`, `ToolTipStyleTests` (all new); `MainWindowViewModelTests`, `BlameViewTests`, `ConflictPanesTests`, `ConflictResultEditorTests`, `CommitGraphControlTests` |
| WP-IA (keeping ignored files, the app half) | `RepositoryOperations.Branches.cs`, `.Protect.cs` (`CheckoutRefusalDisplayAsync`; `RepositoryOperations.KeepIgnored`'s body, 9.1, which turns every checkout and merge on at once), `.Rebase.cs` (then WP-B), `.StashTags.cs`; `OperationTexts.Protect.cs`; the earlier expectations of 16.1's `--no-overwrite-ignore` row but the pull's | `RepositoryOperationsKeepIgnoredTests` (new); `OperationTextsTests`, `DialogTests`, `RepositoryOperationsCheckoutTests`, `RepositoryOperationsProtectTests`, `RepositoryOperationsRebaseTests`, `ShellDriverCheckSupportTests`, `UndoRecordingTests`, `ActivityLogViewTests`, `UndoStepTests` |
| WP-RA (fetch refspecs, the app half) | `RepositoryOperations.RemoteBranches.cs` (10.6's name, R2's checkout without an upstream, D201; every git call keeping `keepIgnored: KeepIgnored`, 9.11), `RepositoryOperations.Remotes.cs` (`SplitRemoteBranch`, `PushBranchToAsync`, `DeleteRemoteBranchAsync`, `SetUpstreamAsync`, `PushToAsync` and `ForcePushAsync` with 10.4's push guard, which counts only destinations under `refs/remotes/`), `.Undo.cs` (`PushForcePushBackAsync`), `.Conflicts.cs` (`MergedRemoteBranch`); `Menus/RepositoryMenuContext.cs` (`DropFacts`, `LocalNameOf` moved, `FindRef`'s index by full name, 5.5), `RepositoryMenus.cs` (`ForDrop`, `ForRemoteBranch`, the set-upstream item), `RepositoryMenus.Pages.cs` (`ForcePushRestore`); `RepositoryViewModel.Menus.cs` (`ReadDropFactsAsync`, `CheckOutByDoubleClick`); `OperationTexts.Menus.Refspecs.cs` (R1 to R4, `CreatedWithoutUpstream`, `PushWouldStopFetch`, 10.5), `OperationTexts.Menus.cs` (`MapsBranchesDifferently` removed); 10.4's upstream name (GD15) in the place that makes `UpstreamText` (`ToolbarViewModel.UpstreamText`, `ViewModels/ToolbarViewModel.cs:277`, which every text of an upstream calls: that method alone of WP-K's file, before WP-K's merge). WP3's notes: `RemoteSource`'s equality compares `Remotes` by reference, so compare `Kind`, `Remote` and `RemoteRef`, or the lists by sequence (10.1); with `Refspecs` filled, WP0's `noTrack` in the create-branch operation already takes effect and `GitRef.RemoteName` already follows the mapping, a namespace such as `refs/remotes/mirror/*` that `origin` fetches belonging to `origin` (10.3, 10.4) | `RepositoryMenusTests` (with 5.12's `FindRef` cases: duplicate names keep `FirstOrDefault`'s answer, a `with` copy that replaces `Refs` finds its own), `RepositoryOperationsRemotesTests`, `RepositoryOperationsPushTests`, `LeftPanelViewModelTests`, `UpstreamNameTests` (new: 10.4's upstream named by its tracking ref, `upstream/pr/7`, and `{remote}/{remote ref}` when no line maps it; the lead's ruling of 2026-10-22) |
| WP-V (the walk-through, by part) | `Phase6Checks.<part>*.cs`, `Phase6Expectations.<part>.cs`, `Phase6ExpectationTests.<part>.cs` for W, K, S, G and L, one agent per part (the report's checks in full); W, K, S and G in `VisualCommit.VisualTests.Phase6`, L in `tests/VisualCommit.VisualTests.Phase6Look`, which part L creates by WP-S's recipe (17.3: the csproj with its links to `Phase6`'s base, support and common expectations, `AssemblyInfo.cs`, the timing script's route for part L; its line in `VisualCommit.slnx` the lead's) | `Phase6ExpectationTests.<part>` |
| WP-R (the real-window pass, Windows) | `Phase6RealWindowPass.cs`, `.W.cs`, `.K.cs`, `.S.cs`, `.L.cs`; `RealApp.Phase6.cs` (`WriteWorkspaces`, 11.8's read-only reads), `.Dashboard.cs`, `.Palette.cs`, `.Settings.cs`; `NativeMethods.cs` (`SystemParametersInfo`); phases 0 to 5's real-window files that 16.1 changes. Written and built (status step 21), run only at the gate; any trial run stays in WP-R's worktree | — |
| Lead | `MainWindowViewModel.cs` (tabs, `SaveTabs`, `UpdateCanClose`, `TryClose`, `FinishThenCloseAsync`, `CloseAll`, `CloseTab`, the calls into the packages' partial files, the menu layer's `repositoryName` callback, 8.10's start of the merge-tool lookup once the first tab's load ends, `readEnvironment` passed to `AutoFetchScheduler` (7.3), the scheduler's `CommonFolderBusy` (4.3, `:99`)), `MainWindow.axaml.cs`, `TabServices.cs` (`ReadClock`, `OpenSettings`), `AppSession.cs` (the store, the catalogues, 11.5's start-up order, `KeyTexts.Current`, 7.3's log line and `readEnvironment`, `readClock`, the kept list's path given to `GitConfiguration`, 8.10, and its disposal in `Dispose`, 8.1), `Theme/Icons.axaml`, `THIRD-PARTY-NOTICES.md`, `IGitRepository.cs`, `FakeRepositoryBase.cs`, `AppSettings.cs`, the Core records WP0 declares, `Phase6Checks.Support.cs` and `Phase6Expectations.Common.cs` after WP-S (17.2's five dashboard and theme helpers once WP-D, WP-W and WP-T are merged, before WP-V's parts W, S and L), `VisualCommit.slnx` after WP-S, CI, the docs, CLAUDE.md; before the gate, 17.7's references and comparison | `AppSessionTests` (new); `Phase6SupportTests`' smoke tests of those helpers |

**WP0's contracts**, each with its owner afterwards:

- **Core**: `Workspaces/` (`Workspace`, `WorkspaceList`, `WorkspaceRepository`, `IWorkspaceStore` with
  1.3's members, `IsReadOnly`, `ReadOnlyCause`, `Notice` and `SaveFailed` included, the signatures of
  `WorkspacePaths` and `WorkspaceEdits`); `AppPaths.WorkspacesFile`; `TabState.WorkspaceId`;
  `AppSettings` (`AppTheme.System`, `KeepIgnoredFiles`, `Editor` with `EditorSetting`,
  `ExtraRunnableExtensions`, `AutoFetchMinutes`' comment); `Operations/PromptGroup.cs`,
  `GitOperation.PromptGroup`, `GitPrompt.Group`, `IGitPromptBroker.Begin`'s group, `NullGitPromptBroker`;
  `IOperationQueue.Cancel(GitOperation)` (nothing until WP4); `Git/FetchRefspecs.cs`' types (with
  `DestinationsOf`), `RepoRefs.Refspecs`, `SourceOf` and `LocalNameOf` giving phase 5's assumption (WP3);
  `Git/GitConfiguration.cs` (`IGitConfiguration` and its records; its `XdgFile`, `Variable(name)` and
  `IDisposable`, which `AppSession.Dispose` calls, the lead adds after wave 1, before WP-SET, 8.1),
  `Git/PullConfiguration.cs`
  (`PullConfiguration`, `PullFastForwardSetting`, `PullRebaseSetting` moved from Git,
  `PullIntegrationOptions`), `PullModes` and `GitBoolean.TryParse` signatures (WP2);
  `GitCommand.BelowNormalPriority` (8.10; `GitRunner` applies it, WP-S); `AppPaths.MergeToolsFile`
  (8.10); `MergeResult.cs`
  (`PullOutcome.IgnoredFilesInTheWay`, `PullResult.IgnoredFiles`, `WouldRebase`), `StashPaths.Deleted`,
  `RebaseTodo.LeadingPicks` and `AppliedCommits` signatures (WP1); `ToolCatalogues.cs` whole, with
  `FoundEditor`, `Fixed`'s data (12.9), `Variable` and `FromEnvironment()`.
- **Git**: `IGitRepository`'s new members (`OpenedAtTopAsync` with its default true,
  `ReadPullConfigurationAsync`, `WriteConfigAsync`, the five `FindIgnoredInTheWayOf…Async`, the required
  `keepIgnored` of the checkouts, `CreateBranchAsync` and `MergeAsync`, `IntegratePullAsync`'s optional
  `PullIntegrationOptions? pullOptions = null` before the token on phase 4's overload: `options` is
  already its `NetworkOptions`, and `FakeRepositoryBase`'s override uses the same name), every call site
  passing them with git's behaviour unchanged: the app passes `keepIgnored: KeepIgnored` at every call
  site of the checkouts, `CreateBranchAsync` and `MergeAsync`, both git calls of the moved
  `CheckoutRemoteBranchAsync` included, and the automatic stash passes `true`; `CreateBranchAsync` also
  gains a required `bool noTrack` (9.11, D201), which the app's create-branch call passes as
  `Refs.SourceOf(start) is { Kind: RemoteSourceKind.AmbiguousRemotes or RemoteSourceKind.AmbiguousRefs }`
  (false while `Refspecs` is null); `FakeRepositoryBase` (empty lists); `GitRunner`'s and `GitLocator.DetectAsync`'s
  environment parameter and `GitRunner.SessionEnvironment` (signatures; WP-S); the pull readers moved
  unchanged into `GitRepository.PullConfig.cs` (WP2); in `IntegratePullAsync` the ignored-files check's
  call point, a no-op in `GitRepository.KeepIgnored.cs` (WP1).
- **App contracts**: `ViewModels/Operations/IStatusBarOperation.cs` (the eleven bound members and
  `Waiting`), `OperationStatusViewModel` implementing it, `RepoTabViewModel.OperationStatus` typed to it;
  `IDashboardToolbar`, a stub `ToolbarViewModel.ForDashboard(IDashboardToolbar)` in
  `ToolbarViewModel.Dashboard.cs` (a static factory over a private constructor, so the `null` calls at
  `RepoTabViewModel.cs:22` and `ToolbarTests.cs:36`, `:287` stay unambiguous, 4.10) and a stub
  `DashboardBulkViewModel` (`Ready`, everything disabled, `StopStarting()` doing nothing) (WP-B);
  `DashboardViewModel` as a stub with a constructor and the members others call (`Name`, `Bulk`,
  `OpenRequested`, `WorkspaceDeleted`, `SelectedRepository`, `RefreshCommand`, `OnBecameActive`,
  `OnWindowActivated`, `OpenedWorkingDirectoryOf(savedPath)`, which 1.2's `WorkspaceMembership` calls)
  (WP-D); `WorkspaceFolderProbe.ProbeAsync` (its `directoryExists` seam and its check on the thread
  pool) and `WorkspaceFolderState` written whole from 1.10's table, since WP-D's rows call them while
  WP-W builds beside it (WP-W); a stub `WorkspaceCommands` with every public command, return type and
  reason of 1.5 to 1.10 (Create, Open, Rename, Delete, AddRepository returning the added path,
  AddOpenTabs, Remove, MoveUp, MoveDown, Locate, `EditReason`), all disabled, since WP-D's header, menus
  and empty state run them while WP-W builds them (WP-W); `IWorkspaceHost` and `MainWindowViewModel`'s
  stub `ShowWorkspace` (WP-D's partial); `RepoTabViewModel`'s dashboard members as stubs (`Dashboard`,
  `IsDashboard`, `ShowsDashboard`, `ShowsLeftPanel`, `GraphColumn`, `SavedWorkspaceId`, `TabAutomationId`)
  and `ShowDashboard(Workspace)`, which sets `Dashboard`, `IsDashboard`, `Title` and `SavedWorkspaceId`,
  since WP-W's restore and WP-D's `ShowWorkspace` both call it (WP-D); `RepoTabViewModel.CloseToolTip`
  and `CloseKeyText`, observable and following `IsActive` (6.8), holding `Close this tab` and null until
  WP-K fills `ShellToolTips.CloseTab` and `CloseOtherTab`; `TabServices`' members (`Workspaces`,
  `WorkspaceItems`, `WorkspaceCommands`, `WorkspaceHost`, `OpenRepositories`, `BulkSlots`, `GitSettings`,
  `Tools`, `ReadClock` (2.10, 17.2), `OpenSettings` (a `Func<SettingsSection, Task>?` the window sets, for
  12.7's `Open settings…`; `SettingsSection` declared here), `GitSettingsChanged`,
  `RaiseGitSettingsChanged`); `AppSession.Start`'s `gitEnvironment`, `systemTheme` (the interface
  `ISystemTheme` in `Services/SystemTheme.cs`, WP-T's), `tools`, `readEnvironment` (7.3) and `readClock`
  (17.2), the store wired, `KeyTexts.Current` set; the theme's contract (WP-T's afterwards):
  `IThemeService` with `Setting`, `IsDark` and `Changed` (`Current` removed), the `ThemeService(Application,
  ISystemTheme, IAppLog)` signature keeping today's behaviour, wired in `AppSession` in 11.5's order,
  today's `Theme`, `IsDarkTheme`, `ThemeSwitchToolTip` and `ToggleTheme` moved unchanged into
  `MainWindowViewModel.Theme.cs`, and `UseTheme(AppTheme)` doing today's toggle body, which WP-SET's Save
  and WP-K's palette call; `RunOptions.Cancellation` (`RunAsync` registers it to call
  `Queue.Cancel(operation)` until it returns, and a token already cancelled queues nothing and returns
  "{Title} cancelled", 4.5; no behaviour changes: no caller passes a token before wave 2, and
  `IOperationQueue.Cancel` does nothing until WP4), `.PromptGroup`, `.Defer`, `BulkPart` and
  `DeferredDialog` in `DeferredDialog.cs`; `RepositoryOperations.KeepIgnored` (in `.Protect.cs`, `=>
  false` until WP-IA gives it 9.1's body); `MenuItem.Command`, `RepositoryMenus.Item(…, command)` and the
  `command:` arguments on 6.7's items; `FileItems(file, context, actions)` with `Open in editor` gated on
  `RepositoryMenuContext.Editor`, filled from a stub in `RepositoryViewModel.Settings.cs` (null until
  WP-SET), running `IRepositoryActions.OpenInEditorAsync` (declared here, a no-op in
  `RepositoryOperations.Files.cs` until WP-SET gives the body) with the label
  `OperationTexts.MenuTexts.OpenInEditor` (in `OperationTexts.Settings.cs`); `ToolbarViewModel`'s
  `XDisabledReason` properties (today's tooltips) and `ToolbarMenuState.Of` reading them;
  `ViewModels/Commands/` (`AppCommand`, 6.4's view-handled `PreviousConflict`, `NextConflict`,
  `PreviousFile`, `NextFile` and `Close` included; `Shortcut`, `ShortcutKey` with `Backspace`,
  `ShortcutModifiers`, `CommandKey`, `AppShortcuts` as data, `ShortcutText`, `KeyTexts`, `ShellKeys`,
  `IWindowKeyActions`) and `ShellToolTips.cs` (`CloseTab`, `CloseOtherTab`, `NewTab`, `ShowActivityLog`,
  `HideActivityLog`, `OpenRepository`, `CloneRepository`, `InitRepository`, today's texts):
  `RepoTabsView.axaml` binds "+"'s tooltip and help text to it, and the close button's tooltip and help
  text to `RepoTabViewModel.CloseToolTip` and its `AcceleratorKey` to `RepoTabViewModel.CloseKeyText`;
  `WelcomeView.axaml` its three buttons', and `ActivityLogViewModel.ToggleToolTip`, to which the toggle
  stays bound, takes its two texts from it; the `AcceleratorKey` and `HelpText` bindings of 15.5 in the
  toolbar, the tab strip, the status bar and the welcome page, `ThemeSwitch`'s help text included (UI
  Automation only; the stage panel's, the resolver's, the list's and the search bar's are WP-K's);
  `KeyRegions.cs` with `KeyRegion.Role` on the four region roots and `KeyRegion.IsMain` on 6.6's targets
  in the existing views (`RefList`, `FilterBox`, `GraphRows`, `DiffText` set in code in
  `DiffTextView.cs:551`, `FileHistoryList`, `BlameText`, `ReflogList`, `RebaseRows`, `ConflictResult`,
  `OpenRepoButton`, `FileList`, `CompareFileList`, `MultiSelectionList`, `UnstagedFileList`,
  `StagedFileList`, `CommitSummary`, `ActivityLogList`): data only, read by nothing until WP-K's
  navigation, so no view behaves or draws differently; `ErrorDialogViewModel`'s nullable repository; `DialogView.FocusesTextBoxOnOpening`; the `Activated`
  handler in `MainWindow.axaml.cs` calling `MainWindowViewModel.OnWindowActivated()`; `StatusTextRecorder`
  taking the interface.
- **Partial files**, so that each package owns its own: `MainWindowViewModel.Session.cs` (WP-W),
  `.Workspaces.cs` (WP-D), `.Commands.cs` (WP-K), `.Settings.cs` (WP-SET), `.Theme.cs` (WP-T), with their
  calls in `MainWindowViewModel.cs`; `MainWindow.Keys.cs` (WP-K: `OnSearchKey` and `OnGitUndoOrRedo` moved
  unchanged); `ToolbarViewModel.Dashboard.cs` (WP-B); `RepositoryOperations.FetchPull.cs` (WP-B: the
  toolbar's `FetchAllAsync` with `FetchAllFailureDisplay`, `PullAsync`, `PullStepsAsync`,
  `PullRefusalDisplay`, moved unchanged out of `.Remotes.cs`); `RepositoryOperations.RemoteBranches.cs`
  (WP-RA: `CheckoutRemoteBranchAsync`, `CreatedBranchOf`, `LocalNameOfRemoteBranch` moved out of
  `.Branches.cs`); `RepositoryViewModel.Settings.cs` (WP-SET), `.Palette.cs` (WP-K);
  `OperationTexts.Bulk.cs`, `.Settings.cs`, `.Menus.Refspecs.cs`, `Workspaces/WorkspaceTexts.cs`,
  `Workspaces/DashboardTexts.cs` (empty).
- **Theme**: `Icons.axaml` (`IconWorkspace`, `IconSettings`, `IconRefresh`, `IconMore`) and
  `THIRD-PARTY-NOTICES.md` (15.4).

**Shared lists, and how phase 5's merge conflicts are avoided.** Phase 5's conflicts came from several
packages editing one file at once (menus, texts, earlier expectations).

| Shared list | Resolution |
|---|---|
| `IGitRepository.cs`, `FakeRepositoryBase.cs`, `AppSettings.cs`, the Core records | WP0 declares every member once; no package edits them; a missing member goes through the lead |
| `MainWindowViewModel.cs`, `MainWindow.axaml.cs`, `TabServices.cs`, `AppSession.cs` | the lead's; each package works in its partial file and the lead joins the calls at its merge |
| `RepositoryViewModel` | `RepositoryViewModel.cs`, `.InProgress.cs`, `.GraphArea.cs` are WP-D's (summary mode); WP-SET, WP-K and WP-RA work in `.Settings.cs`, `.Palette.cs`, `.Menus.cs` |
| `ToolbarViewModel` | `ToolbarViewModel.cs` WP-K's (tooltips with keys) but for `UpstreamText`, which WP-RA changes first (10.4's upstream name, GD15; WP-RA merges before WP-K), `.Dashboard.cs` WP-B's (4.10) |
| `RepositoryOperations.Remotes.cs` (bulk, settings, ignored files and refspecs all touch it today) | WP0 moves Fetch all and Pull into `.FetchPull.cs` (WP-B, which builds 4.6, 4.8, 8.9's and 9.10's app parts on WP1's and WP2's merged git halves) and the remote-branch checkout into `.RemoteBranches.cs`; `.Remotes.cs` (push, deletion, set upstream, remotes) is WP-RA's |
| `RepositoryOperations.Rebase.cs` | WP-IA's, then WP-B's for the rebase stop's deferral |
| `GitRepository.Remotes.cs` | WP0 moves the pull readers out and places the ignored-files check's call point; then WP2 alone |
| `RepositoryMenus.cs`, `RepositoryMenus.Pages.cs`, `RepositoryMenuContext.cs`, `ConflictMenus.cs`, `InteractiveRebaseViewModel.MenuFor` | WP0 adds the keys' `command:` arguments and `FileItems`' editor gate (data only); then WP-RA alone for the first three, nobody for the last two |
| `OperationTexts` | one partial file per package (WP0 makes the new ones empty) |
| Tooltip texts with keys (toolbar, tabs, status bar, welcome page, stage panel, resolver, interactive list, search bar) | `ShellToolTips.cs` and `ToolbarViewModel.cs`, both WP-K's, with the stage panel's, the resolver's, the list's and the search bar's views and view models; the shell's views bind to them from WP0 on, so WP-W and WP-D never touch a tooltip text |
| `MainWindowViewModel`'s theme members | WP0 moves them unchanged into `.Theme.cs` with `UseTheme` and gives `IThemeService` its new members; WP-T's from then on; WP-SET and WP-K only call `UseTheme` |
| `RepoTabsView.axaml`, `StatusBarView.axaml`, `WelcomeView.axaml`, `ToolbarView.axaml` | WP-W, WP-D, WP-W, WP-SET; WP0 adds the UI Automation bindings beforehand |
| `Theme/Icons.axaml`, `THIRD-PARTY-NOTICES.md` | WP0 adds all four icons; then the lead's |
| `Tokens.axaml`, `Controls.axaml` | WP-T alone; the dashboard keeps its list style in its own view |
| `Dialogs.axaml` | WP-SET, then WP-K (the palette's two classes) after WP-SET's merge |
| `ShellDriver` | `ShellDriver.cs` and `.Checks.cs` WP0's, then WP-S's; each package adds its own `ShellDriver.Phase6.<subject>.cs`; `.Workspaces.cs` passes from WP4 to WP-W |
| Earlier test and expectation files several packages change | one at a time in merge order (the order below expects): `Phase5Expectations.A.cs` (WP-W `:210-214`, then WP-IA `:433`, `:460`, then WP-K `:158-185`, `:582`, `:584`); `ShellExpectations.cs` (WP-W `:38-42`, then WP-SET `:31`, `:96-98`); `RepositoryMenusTests` (WP-RA, then WP-K's `AssertWholeLabels`); each merges `phase/6-multi-repo` into its branch before its last test run, and the lead resolves what remains in the merge commit |
| Test classes two sections name | one owner each, as the table says: the session's restore in `DashboardSessionTests` (WP-W); the dashboard's toolbar in `DashboardToolbarTests` and its status bar in `DashboardBulkViewModelTests` (WP-B); `TryClose` and `NamesRepositoryInDialogs` in `MainWindowCloseTests` (WP-D); auto-fetch after adoption in `PromotionTests` (WP-D); the isolation in `ShellTests` and `GitIsolationTests` (WP-S); `"theme": "System"` loading in `JsonSettingsStoreTests` (WP-SET) |

**Order.**

1. WP-L's `MSYSTEM` commit, then WP0 (lead, main checkout).
2. Wave 1, the git halves and the harness, at most three built at once: WP-S, WP1 and WP2 first; WP3 and
   WP4 as worktrees free.
3. The report's checks (status step 6), written once WP-S's `Phase6ScenarioTests` is merged, reviewed,
   and committed before any UI they test is merged; the writers work without a build.
4. Wave 2, the UI packages, after wave 1, at most three built at once: WP-W, WP-D and WP-T first; then, as
   worktrees free, WP-IA (after WP1), WP-RA (after WP3), WP-B (after WP-D, WP-IA, WP2 and WP4) and WP-SET
   (after WP2, WP4 and WP-T: its repository save uses `IOperationQueue.Cancel` through WP0's
   `RunOptions.Cancellation` registration, and its Save calls `UseTheme`); WP-K last (after WP-W, WP-D,
   WP-B, WP-SET and WP-T: the palette lists their commands and calls `UseTheme`, the tooltip texts change
   once, and `Dialogs.axaml` passes from WP-SET).
5. Wave 3: WP-V's parts, at most three at once (W after WP-B, K after WP-K, S after WP-SET and WP-T, G
   after WP-IA and WP-RA, L after WP-T, WP-W, WP-K and WP-SET; W, S and L also after the lead's five
   helpers in `Phase6Checks.Support.cs`, added once WP-D, WP-W and WP-T are merged, 17.2); WP-R on the
   Windows machine.
6. The review (status step 22); 17.7's references in two parts: the phase 0 to 5 walk-throughs on
   `master` in a worktree of their own (no package worktree beside it), and the 306 real-window files
   copied from the main checkout to `D:/Projects/Personal/gate6-reference/` and checked against
   `phase-5-pictures.sha256` (a plain copy of about 12 MB, which may be made at any earlier time); then
   the gate (status step 23), whose real-window pass is the first in the main checkout.

**Status steps.** `status.md`'s Progress steps and the packages that serve them; a step is ticked when
every package that serves it is merged (and, for 19, every row of 16.1 is recorded):

| Step | Packages |
|---|---|
| 5 (harness) | WP-L, WP-S |
| 6 (the report's checks) | the writers of order step 3 (no package) |
| 7 (workspaces: model, file, edits, opening) | WP4 (Core, the store), WP-W (the commands and the screens) |
| 8 (each repository's status read cheaply) | WP4 (`OpenedAtTopAsync`), WP-D (summary mode, the dashboard's reads) |
| 9 (bulk fetch and pull through the queues) | WP4 (`Cancel`, the prompt groups), WP-B |
| 10 (git's own settings) | WP2 |
| 11 (fetch refspecs) | WP3, WP-RA |
| 12 (ignored files) | WP1, WP-IA |
| 13 (workspaces on screen, the dashboard) | WP-W, WP-D |
| 14 (bulk fetch and pull on screen) | WP-B |
| 15, 16 (the palette, the keys) | WP-K |
| 17 (the settings screen) | WP-SET |
| 18 (theme polish, the long folder) | WP-T, WP-W (`PathTextBlock`); the sweep's pictures are WP-V's part L |
| 19 (earlier checks changed on purpose) | every package that owns a row of 16.1 (WP-S, WP1, WP-IA, WP-RA, WP-B, WP-D, WP-W, WP-K, WP-SET, WP-T, WP-R) |
| 20 (the scripted walk-through) | WP-V |
| 21 (the real-window pass, written and built) | WP-R |
| 22 to 30 (closing) | the lead |

---

## 19. Decisions

Recorded in `decisions.md`: the owner's answers are D187 to D194 (at the phase's start) and D195 to
D202 (2026-10-18, after the design's reviews, first in the table below, as the owner gave them); the
design's own decisions follow from D203. Each row covers one subject, as phase 5's D167 to D181 do; a
detail that only follows from a decision stays in its section. A decision that changes an earlier one
names it.

| # | Date | Decision | Why |
|---|---|---|---|
| D195 | 2026-10-18 | The owner's answer after the design's reviews: during Fetch all and Pull all a failed repository's failure stays in its row, "Fetch failed" / "Pull failed" in the danger tone with "Details…", which opens the same error dialog (git's output, the command, Copy output, the extra button); no dialog opens during the run; a single row's own Fetch or Pull from its menu opens the dialog at once, as the toolbar does. Amends D95 for bulk runs (design 4.6, D211). | The owner chose this over a dialog per failure and over one summary dialog at the end. |
| D196 | 2026-10-18 | The owner's answer after the design's reviews: Keep ignored files (D193) protects every operation that can lose one: every checkout, every merge (the pull's included), every rebase (onto, Check out and rebase, interactive, squash, the pull's), cherry-pick and revert, stash apply and pop; not the hard reset, whose `reset-<time>-changes` snapshot already saves what it overwrites and undo restores. Settles D193's open part (design 9.2, D221). | The owner chose this over checkouts and fast-forwards only. |
| D197 | 2026-10-18 | The owner's answer after the design's reviews: the Settings button sits at the start of the toolbar's right group, left of Search (Settings, Search, Theme). Amends the toolbar row of requirements.md's main window layout (D20, D22) (design 7.2, D216). | The owner chose this over the far right and over no button. |
| D198 | 2026-10-18 | The owner's answer after the design's reviews: of the contrast fixes, the light lane colours 1, 2, 4 and 7 become `#1C8655`, `#A16A1B`, `#1D7EA6` and `#59811C` (white label text at 4.5:1 or more; amends D48's palette's light values); the dark danger button keeps white text, and the dark theme's gaps (danger text on hovered and selected rows 3.64 and 3.76:1, the accent on a hovered row 4.13:1, white on the dark danger button 3.38:1) are left to phase 8's accessibility pass; the light warning, success and danger text fixes of design 11.10 are made in any case (design 11.10 to 11.13, D225). | The owner's consent to leave the dark theme's gaps to phase 8. |
| D199 | 2026-10-18 | The owner's answer after the design's reviews: the fixed key table of design 6.1 is adopted as it is, with every command left without a key named (New workspace…, Add repository…, Add open tabs, Rename workspace…, Delete workspace…, Show backups, Create tag at HEAD…, Add remote…, Manage remotes…, Show all hidden branches, Stop soloing, Show keyboard shortcuts…, the theme, Discard all changes…: each reached through the palette). Every keyed button shows its key in its tooltip, the Commit button included ("Commit 1 file (Ctrl+Enter)") and the resolver's and the interactive list's buttons with their keys, so that D192 holds to the letter: the narrowing in the design's first version (its carve-out for those buttons, now D215) goes, and phase 2 to 5's expectations that pin a null enabled tooltip on those buttons change (design 16.1). The Commit command is also a palette entry (design 5.4, 6.1, 6.8; D213, D215). | So that D192 holds to the letter. |
| D200 | 2026-10-18 | The owner's answer after the design's reviews: the settings screen's Git section always opens on "All repositories (global)"; "This repository" is one click away; edits in both scopes are kept while switching (design 7.4, D216). | Git's own advice sets an identity with `--global` (0.3), and an identity typed into the first repository's scope by mistake would fail every other repository. |
| D201 | 2026-10-18 | The owner's answer after the design's reviews: under a fetch line that fetches two refs of one remote into one tracking ref (GitHub's `+refs/pull/*/head:refs/remotes/origin/pr/*` beside the default line), "Check out" of such a ref (and its double-click) creates the local branch without an upstream (`git switch --create <name> --no-track [--no-overwrite-ignore] <tracking ref>`), or checks out the local branch of that name when it exists, and says so in the status bar ("Created pr/7 at origin/pr/7 without an upstream: origin/pr/7 is fetched from more than one ref of origin"); push and delete onto it stay disabled with R2; "Create branch here…" on such a ref also passes `--no-track`; R1 (two remotes) and R3 (not fetched) stay disabled. Amends the design's 10.1 rule 3, 10.4, 10.6 and D93 for that case (D222). | Such a ref can be checked out, without the upstream git would give it: its first candidate, whose push would stop every fetch of the remote (0.5's P). |
| D202 | 2026-10-18 | The owner's answer after the design's reviews: the merge tools git knows are looked up in the background once start-up has settled (after the first tab's load, below-normal priority on Windows), and the last result is kept in the data folder, keyed by git's path and version, so "Choose…" is ready at once; only the very first lookup on a machine waits, with the disabled text of design 8.10 (D217). | `git mergetool --tool-help` takes about 20 to 60 s (0.3), and a lookup at start would slow the first repository's reads (Q1). |
| D203 | 2026-10-18 | Workspaces: `workspaces.json` (D189) holds `{ "version": 1, "workspaces": [ { "id", "name", "repositories": [ { "path" } ] } ] }`; an id is a GUID never shown, by which the session names an open dashboard; a name is trimmed, 1 to 100 characters, without control characters, unique ignoring case; a path is the working tree's top as git reports it; a repository may be in several workspaces, once in each, and a linked worktree is a repository of its own. `IWorkspaceStore` copies `ISettingsStore` with a `Changed` event; an unreadable file is set aside under a name that never overwrites an earlier copy (`workspaces.unreadable.json`, then `workspaces.unreadable-2.json`, …) and the welcome page says so; a file that can be neither read nor set aside makes the store read-only for the session and is never written over; a failed save is told on the welcome page and the dashboard until a save succeeds; a newer version's file is read, never saved, and every edit is disabled with its reason. Every match of a folder (the workspace code, the dashboard's rows and pending tabs, the palette) goes through one `WorkspacePaths.Same`. Workspace edits are not in the undo history (D154) (design 1.1 to 1.6). | The same safety as `settings.json` and `session.json` (D46, D107); a file another program holds, or one whose permissions shut the user out, can be neither read nor moved, and writing the first edit over it would lose the very list D189 keeps in a file of its own (0.8); an id keeps an open dashboard across a rename; one comparison, by `WorktreeKey.Comparer` on normalised paths, instead of a fourth copy of `SamePath`; nothing on disk changes with an edit, so there is nothing to undo. |
| D204 | 2026-10-18 | Folders in a workspace: a saved folder is a repository only at its top (`IGitRepository.OpenedAtTopAsync`: no git call when `WorktreeKey.Comparer` finds it equal to the working directory, else one `git rev-parse --show-prefix`); every row and add flow sorts a folder through `WorkspaceFolderProbe.ProbeAsync` into Ready, Folder not found, Not a repository (also a folder inside another repository, whose outer repository is never read) and Git could not open it, and a row that is not Ready stays in the list with `Locate…` and `Remove from workspace`, which asks nothing. `Add repository…` appends a working tree's top at once and scans any other folder by design 1.8's rules (depth 4, not into a repository, links not followed while other reparse points such as OneDrive folders are, on Windows no Hidden or System folders, at most 10,000 folders), confirmed by git and offered as a checklist. Rows move by Alt+Up, Alt+Down and the row menu's Move up and Move down; no drag (design 1.2, 1.7 to 1.10). | Comparing texts alone would make every row "Not a repository" on CI's Windows (`RUNNER~1`) and macOS (`/private/var`) runs (0.8); a folder inside another repository opens the outer one with exit 0, and acting on it would fetch or pull the wrong repository; a drive not mounted or a list from another machine must keep its entries. Without Hidden and System folders a home folder's scan reads 488 folders instead of 8,477 and loses only tools' own clones in `AppData` (0.8). Phase 4's drag is about 200 lines inside `InteractiveRebaseView` and not reusable as it stands: backlog. `Remove from workspace` asks nothing, unlike `Delete workspace…`: it takes one row, which comes back at once by `Add repository…` on its top or by `Add open tabs`, against a whole list and a closing tab. |
| D205 | 2026-10-18 | The dashboard tab (D187, D188): a `RepoTabViewModel` holding a `DashboardViewModel`, one per workspace, at the start of the strip in the order opened, with Lucide's `layout-grid` before its title, the automation id `DashboardTab` and the `ItemStatus` `Workspace`; "+" is named `Open a new tab`. `MainWindowViewModel` implements `IWorkspaceHost.ShowWorkspace(id, from)`, the one way a workspace opens (the welcome page, `WorkspaceCommands`, the palette): it activates the workspace's open dashboard, else turns an empty `from` tab into it, else inserts one. A repository opened from a row activates the leftmost tab that shows it, else opens after the dashboards and the tabs that dashboard opened this session; closing a dashboard closes only it. The session keeps a dashboard as `TabState(null, WorkspaceId)`: dashboards first on restore, one per workspace, one whose workspace is gone dropped (design 2.1 to 2.3, 1.11, 13.4). | One tab type keeps every `ActiveTab.*` binding; a welcome page that opened a workspace leaves no empty tab behind; the order keeps a dashboard's repositories beside it; a screen reader tells a workspace from a repository of one name, and "+" from the header's `Add repository…`; an older build reads the session as an empty tab, which its next save drops (D46). |
| D206 | 2026-10-18 | The dashboard's layout: it spans the main area's three columns while the side panels and splitters hide, their widths and `PanelLayout.Fit` untouched (as D140); a header, the headings Repository, Folder, Branch, Ahead / behind, Changes, State and Operation, and 26-high rows whose columns `DashboardColumns` computes (Folder only from 1280 wide), every text of the scenario whole in its cell at 1100 and at 1920; no row selected when it first shows; no "last fetched" column; a row shows its repository's operation as a compact text (git's percentage, without git's stage or ssh's notice) and no progress bar (design 2.4 to 2.7). | Ten rows are in view at both gate sizes with the log open or closed; `FETCH_HEAD`'s time is the last attempt by any program and a failed fetch empties it (0.1); ssh's notice is window-wide and would show in every row; with the stage or a bar the text does not fit 1100's 288 px beside Cancel, and an indeterminate bar would differ between two captures. |
| D207 | 2026-10-18 | When dashboard rows are read: no file watchers; a row whose worktree is open in a tab follows that tab, and counts as not read until that tab's view model has its first read; the others are read when the dashboard becomes active and when the window is activated (rows last read 10 s ago or more, by `TabServices.ReadClock`), on F5 and Refresh (all), after their own operations, and for a running bulk run that waits for their read (a first read, or that of a row without a view model at its turn); rows added to an active dashboard's workspace (Add repository…, the checklist, Add open tabs, Locate…) are read at once; a dashboard in the background starts no read of its own, while the reads a running run waits for go on; at most 4 reads at once; a row with its own view model checks its folder and git folder before each read, one without is opened through the probe (D204); a row's folder checks and its opening run on the thread pool, at most one outstanding per row, holding no read slot and no pass; the fsmonitor daemons the user's configuration starts are accepted (Q5). Amends D52 and D71: a repository a dashboard row holds in summary mode has no watcher, and Q4 is met by these reads (design 2.10, 4.2). | Q4 holds because a change made in a terminal means the user left the window and comes back; a restored dashboard in the background never slows a restored repo tab's first graph (Q1, risk 10); a watcher per row walks every folder on Linux (risk 13); a `.git` deleted inside another repository must never be read as that one; a folder on a share that does not answer blocks `Directory.Exists` for minutes (0.1); without the add trigger every added row would show `Reading…` until the user switched tabs. |
| D208 | 2026-10-18 | A dashboard tab's toolbar, status bar and activity log: one class, `DashboardBulkViewModel`, implements the status bar's source `IStatusBarOperation` (the eleven bound members and `Waiting`; the static type of `RepoTabViewModel.OperationStatus`) and `IDashboardToolbar` for the factory `ToolbarViewModel.ForDashboard(IDashboardToolbar)`; Fetch and Pull, their keys and their palette entries run Fetch all and Pull all, enabled while rows are being read and disabled while a run runs, for an empty workspace and when no row can; the other buttons are disabled as on an empty tab; the status bar's branch part shows the workspace after `IconWorkspace`; the activity log follows the selected row's worktree, and with nothing to follow its toggle is disabled with the first reason that holds: `{workspace} has no repositories`, `Select a repository to see its activity`, `Wait for {name} to be read`, or the selected row's State tooltip. Extends D90 to a dashboard tab: Fetch and Pull, their keys and their palette entries act on every row (design 2.8, 2.9, 4.9, 4.10). | The bindings keep their paths, so earlier pictures stay; compiled bindings need an interface where a sealed class stood; a run waits for rows still being read, so nothing needs waiting for before the click; records are keyed by worktree, so the log shows every operation on it whichever queue ran it; a second public constructor would make every `ToolbarViewModel(null)` ambiguous. |
| D209 | 2026-10-18 | The repositories behind the rows: a row without a tab has a `RepositoryViewModel` in summary mode (`ForSummary`: refs and status read as a tab reads them; no watcher, history, session filter, clean-up at opening or Q1 line) with its queue, operations, toolbar, status and undo history; a tab that opens its worktree adopts that object and promotes it with the tab's stopwatch (`Promote(Stopwatch)`, then a full `StartAsync`, deferring a refresh asked for during the first load); a watcher a promotion creates while the adopted operation runs starts paused until that operation's run ends (D52, D71); `TabServices.OpenRepositories` keys the view models by worktree: a row uses the first-registered tab's view model without owning it, two dashboards share one, no tab and no summary-mode view model is made on a worktree while a closed tab's or a released summary's queue still runs there (the tab waits, `Waiting for {title} to finish…`), and a released summary is disposed at its last release, kept in `Closing` while its operation runs. `TryClose`, "Finishing operations" and `CloseAll` include the dashboards' view models and those released while running, and stop every bulk run from starting rows before dropping waiting operations; a dialog names its repository while a dashboard tab is open or more than one tab has a repository. Amends D45: a dashboard owns view models without tabs and uses tabs' without owning them (design 3, 4.1, 4.12). | A row's Fetch and Pull then have the toolbar's titles, Cancel, error dialog, dedupe and undo recording by construction, and a dashboard and the tab opened from it share one queue (two tabs opened side by side keep two, as phase 5 accepted); a dashboard's running pull goes on in the tab opened from it; Q1's lines and auto-fetch's first interval count from the user's open; a closed dashboard's pull holds the window as a closed tab's does. |
| D210 | 2026-10-18 | Bulk runs: Fetch all and Pull all act on every row whose toolbar would fetch or pull (`ToolbarViewModel.StatesFor`, decided again at the row's turn from its current view model and state; a row without a view model then is read again first), after the row's first read; a left-out row shows the toolbar's reason and counts as skipped; each pull fetches first (D84). A window-wide pool of 4 slots serves every run, first come first served, never two at once to worktrees of one common git folder, nor one to a common folder another queue is fetching from, and auto-fetch skips a tick while another queue or a slot fetches from its common folder; a Fetch, Pull or Push the user starts beside them is not kept apart (risk 33); bulk fetch is the user's Fetch (prompts asked). A run's prompts form a group: one dialog of a group at a time, the same prompt text answered from the first answer (a Cancel included) until the run ends; `AskPassBroker.MaxConnections` 16. Cancel all stops starting rows (`BulkRun.StopStarting()`) and cancels only the run's own operations (a tab's Fetch or Pull merged with a row's by D123's dedupe included; `IOperationQueue.Cancel(GitOperation)`, `RunOptions.Cancellation`); a row's own Cancel is its status's; closing a dashboard tab is Cancel all. A row the run holds (in line, reading or running) cannot be removed from the workspace, and a removed skipped or ended row leaves the run's counts as they were (design 4.2 to 4.5, 4.7). | The toolbar's rule keeps a row and its tab in agreement; two fetches of one common git folder that overlap while the remote has something new lose one of them on its remote-tracking refs (0.2); a gate in every queue would add a waiting state across D81's queues; 4 bounds git's processes on the processor-bound laptop (risks 12, 18) and the prompts that can wait at once; ten repositories on one host must not stack ten sign-in dialogs (D104); with 4 connections a fifth waiting prompt failed as refused within 5 s (0.2); `CancelCurrent` and `DropWaiting` would also stop a tab's own push; an operation the run's call shares with a tab's by D123's dedupe is one git call, and Cancel all stops it for both; counting its callers would break D123's one dialog per task and give a slot back while git still fetches; a row removed while the run waited for its slot or its read could leak one of the window's 4 slots or leave the run unending. |
| D211 | 2026-10-18 | Failures of a bulk run stay in their rows (D195): during a run no dialog opens for a result; a failed row shows `<Title> failed` in the danger tone with `Details…`, which opens the identical C11 error dialog later (likewise a cancel's lock files, a pull's rebase stop with nothing to look at and an unexpected exception); a single row's Fetch or Pull from its menu opens its dialog at once. The amendment ends with the run's dashboard: a result that comes after it closed opens its dialog when a tab's view model ran it. A run says `Fetching {n} repositories… {k} done` and ends with its counts, failures first (`Pulled 6 repositories; 1 failed; 2 skipped`). A pull that `pull.ff=only` refused because the branches diverged (`git merge --ff-only`) says so, also from the toolbar and the menus: `{b} and {up} have diverged, and pull.ff is set to only: merge {up} into {b} or rebase {b} onto {up}.`; a plain merge refused by `merge.ff=only` or `branch.<b>.mergeOptions` keeps today's text. Follows D195, which amends D95 for bulk runs (design 4.6 to 4.9). | `ShowResultAsync` waits for its dialog, so a run would stop at its first failure; up to four rows run at once, so "4 of 10" would read as "the fourth is fetching"; a narrow header cuts the end of the text; git's `Not possible to fast-forward` had no summary of its own and the dialog said only "Git could not pull …" (0.2); after its dashboard closed a run has no row to keep a result, while the tab that ran it is still open; `merge.ff` and `branch.<b>.mergeOptions` refuse the same way under another key, and the menus' plain merge would be refused too (0.2). |
| D212 | 2026-10-18 | The command palette (D191): a dialog of the dialog layer without dimming, its 600-wide card 8 below the toolbar, the chosen entry run after the palette has closed; commands show at once, among them a `Commit` entry labelled as the commit button and run by its command (D199), and items (the objects' menus built by the same `RepositoryMenus` builders on one snapshot, tabs, repositories, workspaces) once the user types; an entry matches when every query word is a case-insensitive substring of its label or detail, ranked by tier, then enabled first, then the session's runs, then source order, without fuzzy matching; disabled entries are dimmed with the menus' reason, and what menus leave out the palette lacks too. A repository opened from the palette or a key goes to the tab that shows it, else the active tab when it is empty, else a new tab at the end (design 5). | The layer gives Esc, focus return, stacking and the UIA root for free; an empty box would list some 1,500 "Copy SHA" lines; a fuzzy subsequence over 1,500 entries matches too much and is hard to test; one builder keeps a menu and its entry from disagreeing. |
| D213 | 2026-10-18 | The fixed key table (D192, D199): design 6.1's set, leaving free Mod+P, Mod+N, Mod+Q, Mod+H, Mod+M, every Mod+Shift+digit, every Ctrl+Alt and Alt+letter, Ctrl+Shift+U, Ctrl+F1 to Ctrl+F12, F10, F11 and F12, and no key copying the graph's SHAs; left without a key, each reached through the palette and its menu or button: New workspace…, Add repository…, Add open tabs, Rename workspace…, Delete workspace…, Show backups, Create tag at HEAD…, Add remote…, Manage remotes…, Show all hidden branches, Stop soloing, Show keyboard shortcuts…, the theme and Discard all changes…; Ctrl+Tab and Ctrl+Shift+Tab switch tabs in the strip's order with Control on every platform (declining in the resolver's result), Ctrl+PageDown and Ctrl+PageUp everywhere, Mod+1 to Mod+8 and Mod+9; Mod+Shift+D goes to the next dashboard (with none open, the palette at `workspace`); F6 and Shift+F6 go round the left panel, the graph area, the right panel and the activity log, back to each one's last focused element; on a dashboard Fetch's and Pull's keys run Fetch all and Pull all, and on macOS the list's Remove also takes Cmd+Backspace; the function keys stay as they are on a Mac keyboard, which needs Fn for them (design 6.1, 6.5, 6.6, 6.9). | The free keys are the platforms' (macOS's application keys and screenshots, AltGr on Windows, IBus, the desktops' shortcuts) and D191's Mod+P; rare set-up and edit actions are two keys away in the palette (Mod+Shift+P and a word), and Discard all changes… asks first; Cmd+Tab is macOS's application switcher; the strip's order is predictable in a picture; F6 reaches every region without the pointer (R7); a Mac's "delete" key sends Backspace, and a Mac has no standard key for moving between regions or opening a context menu (VS Code keeps F6 and Shift+F10 there). |
| D214 | 2026-10-18 | Key texts and routing: one formatter writes every key text in words, also on macOS (`Ctrl+Shift+P`, `Cmd+Shift+P`, `Option+Up`; Backspace and Delete in Apple's words, `Cmd+Delete` and `Forward Delete`), for the palette, the menus, the tooltips, the settings list and UI Automation's `AcceleratorKey`; Avalonia's `InputGesture` and `HotKey` stay unused; `KeyRouter` takes the set on the window's tunnelling pass and keeps phase 5's undo and redo on the bubbling pass; every key declines while a dialog (but the palette's own) or a menu is open or a branch drag runs; a disabled command's key is consumed and does nothing, Mod+F included. Amends phase 5 design 1.3 (design 6.2 to 6.4). | Avalonia writes `⇧+⌘+P` on macOS, `OemComma` invariantly and UI Automation's text from `InputGesture` (0.7), none of them D191's words; the settings list must name the key a Mac user presses; Ctrl+Tab must be taken before `KeyboardNavigationHandler` moves the focus; a disabled Undo already takes its key (D163), and nothing used the passed-on Mod+F. |
| D215 | 2026-10-18 | Keys shown in menus and tooltips: a menu item shows a key only when it runs the keyed command on the same object, drawn by the app's own template, right-aligned in the secondary colour; an enabled button whose command has a key says `{what it does} ({key})`, a disabled one its reason alone, and its help text is its tooltip; the reasons move to `XDisabledReason` properties, which `ToolbarMenuState.Of` reads; every keyed button shows its key, also the commit button (`Commit 1 file (Ctrl+Enter)`), the resolver's five buttons (Alt+Up, Alt+Down, Alt+Left, Alt+Right, `Close (Esc)`), the interactive list's `Cancel (Esc)` and the search bar's `Next match (Enter, F3)` and `Previous match (Shift+Enter, Shift+F3)` (D199); a tab's close button shows Mod+W only on the active tab; the commit button's reason moves to `CommitDisabledReason`; Mod+Enter commits from the message boxes, taken on the way down, and confirms from a dialog's box of several lines. Amends phase 3 design 4d's "an enabled toolbar button has no tooltip" (design 6.7, 6.8, 6.10). | D192 shows every key where its command is; a key on an item acting on another object would mislead; Mod+W closes the active tab only, so another tab's button would name a key that closes a different tab; reading tooltips as reasons would turn an enabled button's tooltip into a menu item's reason; a `TextBox` with `AcceptsReturn` types a line break for Enter with any modifier (0.7). |
| D216 | 2026-10-18 | The settings screen: a large dialog on the dialog layer, at most 860×680, with sections General, Git, Tools and Keyboard shortcuts; no combo box and no menu over it, long lists in a stacked Pick dialog whose options carry a detail; changes apply at Save only, without a preview; "This repository" always shown, disabled off a repository tab; the Git section always opens on `All repositories (global)` (D200); auto-fetch a check box and 1 to 60 minutes, a file's value outside that range kept until edited, `VISUALCOMMIT_AUTO_FETCH_MINUTES` logged, not shown. It opens from a `Settings` button at the start of the toolbar's right group, left of Search (D197), from Mod+, from the palette, from the editor failure's `Open settings…` and, while no editor is chosen, from the palette's `Choose an editor…` (on Tools). `ISettingsStore` gets no change event: readers read `Current` when they need it and the theme goes through `MainWindowViewModel.UseTheme` (design 7). | Both harnesses capture and drive the dialog layer (D69), a combo box's drop-down is a window outside the capture (D141), and a modal dialog keeps "This repository" fixed while open; one layout on every tab; left of Search the button takes toolbar background that is empty in every earlier picture and keeps Search and Theme in place, and earlier pictures are compared by region against references made on `master` (design 17.7), as phase 5's gate did. |
| D217 | 2026-10-18 | Git's own settings (D190): `git config --show-scope --show-origin -z --list` read in the data folder under a ceiling, from which the global scope's fields, baseline and read back come, and beside it, on a repository's tab, one in its working tree for the repository scope, never `--global --get` or `--list`; only changed keys are written, by `--replace-all` and `--unset-all` (exit 5 counting as done) at `--global` or `--local`; global writes go one at a time through an app-level writer, the repository's as one operation of its queue with no undo entry; every save is read back and a value another file still decides is said in a notice; files are named by what they are, the full path in a tooltip; open tabs read `merge.tool` again after a save. The merge tools come from `git mergetool --tool-help`, run once per session in the background once start-up has settled, and the last list is kept in the data folder by git's path and version so that Choose… is ready at once (D202), without terminal tools and with the user-defined ones; no path or command editing, `merge.guitool` untouched. A broken `~/.gitconfig`'s own message is phase 8's first-run work (design 8.1 to 8.5, 8.10, 8.11). | `includeIf` applies only inside a repository and `--global --list` skips the XDG file and includes (0.3); read inside a repository, a global value can be an `includeIf`'s, and saving over it replaced the global file's own value unseen (0.3); an untouched value keeps its file's layout and comments; a write may land above an include that still wins (0.3); texts must be the same on every machine; `--tool-help` takes about 20 to 60 s; a broken file stops every git call, `git --version` included, so the app says "Git is not working" first. |
| D218 | 2026-10-18 | Identity and pull modes: a name or email is trimmed; `<`, `>`, line breaks and a name of punctuation only are refused; an empty field unsets, an empty value is never written; an email without `@` warns and is saved; the identity git will use is shown from `git var` in the repository scope only, its refusals in the app's words. The pull modes are one-line radios that write both keys at the chosen scope (Merge `false`/`true`, Merge commit `false`/`false`, Rebase `true`/`true`, Rebase keeping merges `merges`/`true`, Fast-forward only unset/`only`, Not set unset/unset); any value is read by its effect, values are read as git reads them by one Core parse, `GitBoolean` (the boolean words ignoring case, integers in decimal, hex or octal with a unit), and only `interactive`, `i`, `m`, a unit letter alone (2.36 reads it as false, newer gits refuse it) and values git refuses show as Other; a winning `branch.<b>.rebase` is noticed and removable in the repository scope (design 8.6 to 8.8). | Git drops `<`, `>` and line breaks silently and 2.36 refuses a punctuation name (0.3); outside a repository and in its refusals git names the machine's user and computer, which would differ per machine and runner; precedence is per key across scopes and Git for Windows writes a pull key into the system file, so one key alone could be cancelled silently; the commonest single keys show as their modes; a reader of lower-case words alone would call `TRUE` refused while Pull rebases (0.3). |
| D219 | 2026-10-18 | Pull reads its configuration before the fetch: a value it does not understand fails the pull with today's text before anything is fetched; the configuration read first decides fast-forward, merge or rebase after the fetch, passed to phase 4's `IntegratePullAsync` as its optional last parameter before the token, `PullIntegrationOptions? pullOptions`, the record `PullIntegrationOptions(PullConfiguration? Configuration = null, bool KeepIgnored = false)`, so the parity tests compile unchanged. Amends D84 (design 8.9). | `git pull` refuses an invalid value before it fetches (0.3); a bulk pull would otherwise spend a fetch per misconfigured repository; a terminal's change during the fetch is not seen, as with `git pull`; `options` already names that overload's `NetworkOptions`. |
| D220 | 2026-10-18 | The harnesses give each app session its own git configuration and fixed answers for what the machine has: `AppSession.Start` takes an optional git environment, which the session's one `GitRunner` applies after `DefaultEnvironment`; both harnesses put `GIT_CONFIG_GLOBAL` and `XDG_CONFIG_HOME` in the session's data folder; the test process's `GIT_CONFIG_GLOBAL` names a file in a folder never made and its `XDG_CONFIG_HOME` an empty folder, watched by a tripwire; `GIT_AUTHOR_*`, `GIT_COMMITTER_*`, `EMAIL` and `MSYSTEM` are removed and `PATHEXT` is fixed to Windows' default; one Core seam, `ToolCatalogues` (`AppSession.Start(…, ToolCatalogues? tools)`, `VISUALCOMMIT_TOOL_CATALOGUES=fixed`), gives fixed editors under a root that does not exist, a fixed merge-tool list and `PATHEXT`; a `FakeSystemTheme` stands for the system's theme headless; `AppSession.Start` also takes the environment reader the auto-fetch line and the scheduler share (`readEnvironment`) and a read clock for 2.10's 10 s rule (`readClock`), so no test changes the process's environment or waits 10 s. Amends D112 (design 7.3, 8.12, 12.9, 17.2). | The first check that saves an identity or a pull mode would otherwise write the one global file every test process shares (0.3), and a stray write must fail where it happens (exit 255) instead of leaking; the owner's `~/.config/git/ignore` and identity variables reached the app under test (0.10); Visual Studio's Git crashes running `sh` with `MSYSTEM` set (risk 28); pictures must never show the machine's editors, merge tools or `PATHEXT`; `AutoFetchSchedulerTests` assert the process's own value in parallel. |
| D221 | 2026-10-18 | Keeping ignored files (D193, D196): while the setting is on, every checkout passes `--no-overwrite-ignore`; every merge passes it and runs the app's check; every rebase (the pull's, interactive and squash included; the check sees a merge in the range with its own read, design 9.6), cherry-pick, revert and stash apply and pop runs the app's check inside the operation before any write (the paths its commits may write, looked at on disk, one `git check-ignore`) and refuses with the operation's own title and the files' names; the pull's check runs inside `IntegratePullAsync` after the fetch; nothing is checked again on Continue, Skip or Abort; resets, discards, the resolver's sides, the restores and undo are not covered. Off restores phase 5 exactly: the automatic stash's checkout keeps D179's option and undo D169's guards; this amends D193's "checkouts then follow git's own default", read as turning off D193's extension of D179 only, a reading the owner confirmed on 2026-10-18. Follows D196, which settles D193's open part, and extends Q5's exception (design 9). | Git's option is exact for a checkout, works for a merge only when it fast-forwards and does not exist for rebase, cherry-pick, revert or stash, each of which overwrites an ignored file, and a rebase can delete it (0.4); `merge-tree --write-tree` would be exact but needs Git 2.38; a hard reset's snapshot already saves what it overwrites (D127); an undo must be exact; Off turns off D193's extension of D179 and restores phase 5, and turning D179 off too would reopen a recorded decision; a rebase's `git log --diff-filter` lists no merge, and git replays a branch only ahead of its upstream when it holds one (0.4's U5). |
| D222 | 2026-10-18 | Fetch refspecs: every `remote.<r>.fetch` line is read with each refs read (`RepoRefs.Refspecs`), in the one `git config --null --get-regexp ^remote\.` call that replaces `git remote`, and mapped back as git's tracking setup does, stricter in one case (one remote fetching a ref from two sources is ambiguous); `GitRef.RemoteName` follows the mapping (the longest configured remote name only when two remotes or none fetch it); the drop's two pushes and `Delete {y} on {r}…` act on the branch's name on its remote when it is unique and a branch, else are disabled with R1 to R4 as design 10.5 words them; a remote branch checks out into its name on the remote; a mapped ref that is not a branch keeps today's name; a ref one remote fetches from two refs checks out (and double-clicks) into a branch of today's name without an upstream, saying so in the status bar, and "Create branch here…" from it or from a ref two remotes fetch passes `--no-track` (D201); a ref two remotes fetch, or none, cannot be checked out and its double-click does nothing; a push of a local branch that would give a tracking ref a second source on its remote is refused before git; "Set upstream…" leaves such refs out; the force-push restore's fallback goes by the mapping, else is disabled with `The remote this backup came from cannot be found`; `DropFacts.RemoteMapsBranchesDifferently` is removed. Amends D93, with D201 (design 10.1 to 10.7). | Under a renaming line today's drop push creates a wrong branch on the remote and, beside GitHub's pull-request line, breaks every later fetch; the deletion is refused by D100's lease with a wrong explanation; a namespace without the remote's name makes a remote row that does not exist (0.5); git has no read of the reverse mapping; `git switch <name>` finds a remote branch by its name on the remote; a push creating `refs/heads/pr/1` beside GitHub's line stops every fetch of that remote, and phase 5 and git already make branches that track it (0.5's P and implicit upstream); one call keeps a refs read at five processes. |
| D223 | 2026-10-18 | The theme (D194): `ThemeService` turns System into Dark or Light from the app's own seam `ISystemTheme` (`PlatformSystemTheme` over `Application.PlatformSettings`, a fake in tests through `AppSession.Start`), follows it while System is chosen and is the only writer of the application's theme variant; `ThemeVariant.Default` is unused; `MainWindowViewModel.UseTheme(AppTheme)` is the one way to change the theme (the toolbar, the palette, the settings screen's Save); the toolbar's Theme switch shows the other variant and saves it, which leaves System, its tooltip adding `(stops following the system)` while System is chosen; System is chosen in the settings screen and the palette; Linux without a portal means Light (design 11.1 to 11.7, 11.9). | Headless, `ThemeVariant.Default` always means Light and the platform's settings cannot be faked (0.6), so the fake drives the same code path in both harnesses; one click of the switch still changes what the window shows and phase 0's checks keep their meaning; a cycle through System would make one click in three change nothing. |
| D224 | 2026-10-18 | The real-window pass never changes the operating system's theme: S15 starts the app with System, reads the app mode and high contrast read-only and fails while high contrast is on, which the pass needs off as it needs an unlocked desktop (plan.md's gate step 4), and compares the start and a Theme click with the scripted twins of that variant; L34, a launch of its own with Theme explicitly Light whatever the owner's mode, repeats four pictures of the sweep against their light twins (the activity log, the delete question, the palette, the settings screen's General); following a change is proved headless with the fake; switching Windows' app mode while the app runs is a by-hand step at acceptance, one line of the report's checklist (design 11.8, 17.4). | Changing the mode flips every app on the owner's desktop and a killed pass would leave it flipped; no per-process way exists (0.6); CLAUDE.md lets the pass take the mouse, never the desktop's settings; with Theme explicitly Light, L34's pictures do not depend on the owner's mode, and System's light draws Light's pixels (design 11.15); a skip would leave S15's start unproven on a gate the plan does not make conditional (CLAUDE.md, closing step 6). |
| D225 | 2026-10-18 | Light contrast and colour tokens: every light text token reaches 4.5:1 on every background its text lies on, hovered and selected rows included (`VcWarningBrush` `#895A17`, `VcSuccessBrush` `#187248`, `VcDangerBrush` `#B7323D`); `VcDangerTextBrush` replaces the danger button's fixed `#FFFFFF`, white in both themes (D198); light lanes 1, 2, 4 and 7 become `#1C8655`, `#A16A1B`, `#1D7EA6` and `#59811C` so their white labels reach 4.5:1 (D198); tooltips take the app's colours through Fluent's `ToolTipBackground`, `ToolTipForeground` and `ToolTipBorderBrush` per variant; scroll bars stay Fluent's. Amends D48's light palette, with D198 (design 11.10 to 11.13). | Light warning, success and danger text fell to 2.80, 3.19 and 3.87:1 on a hovered row, and lanes 1, 2, 4 and 7 gave white labels 3.64 to 4.14:1; D29 keeps every colour in the tokens; Fluent's grey tooltips match the panels in neither theme. The dark gaps found (danger 3.64 and 3.76:1 on hovered and selected rows, the accent 4.13 and 4.26:1, white on the dark danger button 3.38:1) are left to phase 8's accessibility pass with the owner's consent (D198): a lighter dark danger colour lowers the white label further, so the two are one change, and a lighter accent recolours nearly every dark picture (design 11.10, 16.2). |
| D226 | 2026-10-18 | Open in editor and Open file: editors are found in their install folders by a fixed catalogue per platform, never by a `PATH` name on Windows and only as `.exe` or `.com` there, and started without a shell (arguments as a list, the file's absolute path, the program's folder or the home folder as working directory, `ELECTRON_RUN_AS_NODE` removed); Other… takes a program and arguments with `{path}` and `{line}`; git's `core.editor` only marks a matching editor; `Open in editor` shows after `Open file` in every file menu only while an editor is chosen, never asks first; no menu checks the program, and a program that is gone is said when the item is clicked; a failure to start opens an error dialog with `Open settings…`; while no editor is chosen the palette lists `Choose an editor…`. Open file asks by the fixed list, which cannot be removed, the user's extra extensions and, on Windows, `PATHEXT`'s entries. Extends D130: its fixed list stays as a floor (design 12). | On this machine `code` on `PATH` is Cursor's shim and VS Code's and Cursor's commands are batch files that `cmd.exe` would parse (0.9, D130); hiding the item keeps about 45 asserted file menus and their pictures unchanged, unlike Open in merge tool, which is disabled with its reason but shows only on a conflicted file; a file check of a program on a share that does not answer blocks the UI thread for minutes (0.9), and `MenuContext` is built for every menu; `core.editor` is a shell command line made to block; `.wsh`, `.msc`, `.py` and `.pyw` opened without a question (risk 20). |
| D227 | 2026-10-18 | The welcome page's Workspaces section and long paths: the welcome page always shows `Workspaces` between its buttons and the recent list, inside the buttons' panel (the clone form hides it), listing the workspaces by name with their counts and a menu (Open, Rename, Delete); a click opens the workspace in the page's own tab; a rooted path in the welcome page's errors and the scan dialog breaks only after a separator (`PathTextBlock`), while `UrlTextBlock` and its dialogs stay (design 13.1, 13.2). | It is where workspaces start, also with none; a `New workspace…` button beside `Recent repositories` would change the same earlier pictures, and a section shown only once a workspace exists would leave the palette as the only place to create the first one; by name is predictable and workspaces need no order of their own; Unicode's line breaking split phase 1's `11a` inside a folder name (0.8); changing `UrlTextBlock` would move lines in earlier pictures with no defect recorded there. |
| D228 | 2026-10-18 | The tab strip shrinks, then scrolls: tabs keep today's 120 to 220 while they and "+" fit, so nothing moves while the tabs fit at their natural widths (8 of 120 at 1000 wide, at least 4 of any title); then the widest shrink first, in whole pixels, down to 80; then they scroll with the active tab whole in view and "+" at the right end; a wheel notch scrolls 80; no arrow buttons (design 13.3). | A dashboard and ten more tabs (its repositories) are 1,320 px, more than the strip's 1,062 at 1100 wide, and today "+" leaves the view first; arrow buttons would take room from the tabs while the wheel, the tab keys and the palette reach every tab; no earlier picture has more than 3 tabs. |
| D229 | 2026-10-18 | Phase 6's scenarios and test servers: the workspace "Web" of ten repositories in mixed states is built once per test process the lean way (`git fast-import` and configuration written as text, 28 processes) with relative remote URLs, so a copy is files only, pinned by `Phase6ScenarioTests`; `TempRepo`'s blank git folders leave out git's `hooks/*.sample`; `AuthRemoteServer` serves a folder of bare repositories, each at its own URL with its own hold at a chosen percentage, on port 47393 with the fallback to a free one for the walk-through and the real window, and on a system port in its own tests (amends D97, as D150 did); a file remote's fetch is held by an upload-pack wrapper written into each held repository's own `.git/config` (design 17.1, 17.2). | The TempRepo way costs 80 processes, 1 to 2 minutes under the default run's load (0.10); copying is file-count bound and the samples are 221 of the template's 544 files; the progress picture needs each row's percentage, and git counts boundary objects too, so the holds are at 25, 75 and 45 (0.10); in the session's global file the wrapper's `[remote "origin"]` would give every repository a remote `origin`, so a repository without one would be fetched (0.10). |
| D230 | 2026-10-18 | Phase 6's walk-through and tests: one project, `VisualCommit.VisualTests.Phase6`, with parts W (workspaces and the dashboard), K (keys and the palette), S (settings and the theme's following), G (ignored files and refspecs) and L (every main screen in both themes, the long folder, the tab strip), one class per part, about 38 methods; part L moves to a project of its own past about 40 methods or when the project ends after the run's longest other project; phase 6's headless view tests go into `VisualCommit.App.ViewTests` when App.Tests' headless tests alone take at least 80% of its time with the processor well below 90%, else into App.Tests by subject; earlier phases' pictures are compared by region against references made on `master` (design 17.3, 17.7, 11.14). | The letters keep check names apart from the requirement prefixes; parts run side by side; `VisualCommit.App.Tests` already ends the default run and Avalonia's headless tests run one at a time on its one UI thread (0.10); the Settings button changes every earlier window picture in one toolbar box, so the sha256 list alone cannot tell intended changes from defects. |
| D231 | 2026-10-19 | "Pull from {up} (rebase)", the rebase chosen for one pull, reads `pull.ff` alone before its fetch and refuses a value `git pull --rebase` refuses (`foo`, `ONLY`; `k` on 2.55) with Pull's text for a setting it does not understand ("pull.ff is set to foo, which this app does not understand.", command `git config --get pull.ff`), nothing fetched; `pull.ff=only` and `false` still give way to the rebase, and no rebase setting is read (`branch.<b>.rebase`, `pull.rebase`, an invalid one included). The git layer does it (`ReadPullFastForwardAsync`, used by `ReadPullConfigurationAsync` and by `PullIntegration.Rebase`); `PullStepsAsync` reads it before the fetch. Amends D147's "does not read git's pull settings, as `git pull --rebase`" (design 8.9, 4.8). | `git pull --rebase` reads `pull.ff` whenever no `--ff` option is given and refuses an invalid value before it fetches (`fatal: invalid value for 'pull.ff'`), checked on 2.36 and 2.55, while it ignores `pull.ff=only` and an invalid `pull.rebase` as D147 found: D147's premise holds for those but not for an invalid `pull.ff`. Refusing before the fetch spends no fetch, as D219 does for the configured pull. |
