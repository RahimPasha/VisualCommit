# Phase 5 design: history, traceability, undo, and protected changes

Working design notes for phase 5, written on 2026-10-12 before any phase 5 code exists. The
builders implement from this file; its final facts are folded into `architecture.md` at the end of
the phase, after which this file is kept as history, as `phase-3-design.md` and `phase-4-design.md`
are. The last section lists the decisions this design makes; they are recorded in `decisions.md`
from D164 on. Where this file and merged code differ later, the code's doc comments say what is
built; tell the lead.

Sources: `plan.md` "Phase 5", the phase 4 handoff's "For the next phase", decisions D47, D52, D54,
D60, D62, D63, D67, D80, D81, D83, D88, D91, D95, D124, D137 to D139, D148 and D153 to D163, a code
map and seven research notes made by sub-agents on 2026-10-12 (the notes derived every git fact
with Git 2.36 in throw-away repositories). Those notes lived in the session's scratch folder;
everything this design relies on is copied into section 0.

The owner's answers, taken as given (2026-10-12, at the start of phase 5):

1. (D153) **C12 is built in this phase.** A checkout that git would refuse because uncommitted
   changes would be overwritten does not start; the app says why ("1 conflict prevents checkout.
   Commit, stash, or discard your changes first.") and keeps "Stash changes and check out" as a
   button of that message. Checking out a commit that the current branch reaches first stashes
   every uncommitted change, always when there are any, without asking ("Auto stash before
   checking out …"). Such a stash is kept until the user pops or drops it.
2. (D154) **Undo and redo reach many steps back** within the app's session, per repository; a step
   is refused, with its reason, when the repository has changed since in a way that undoing it
   would overwrite; older actions are restored from the reflog view and the backups.
3. (D155) **Undoing a change to a remote** (a force push, a deleted remote branch or tag) asks first,
   naming the remote, then pushes the backed-up commit or tag back with a lease.
4. (D156) **The activity log is a panel under the graph area**, opened by the status bar's
   "Activity log": one line per operation, newest last, each opening to its git commands with their
   output, exit code and time; the backups' clean-up lines too.
5. (D157) **Search marks its matches in the graph**: the toolbar's Search and Ctrl+F open a box
   above the graph; matches are marked, the rest dimmed, a count shows, Enter and Shift+Enter go to
   the next and previous match; it searches message, author, SHA, a file, or the changes' text.
6. (D158) **File history and blame open in place of the graph.** History lists the commits that
   touched the file, following renames, with the diff of the selected one beside it; blame shows
   each line's commit in a gutter, and a click selects that commit in the graph. Esc returns.
7. (D159) **Hidden and soloed branches are remembered per repository across restarts**, in the
   session; the left panel marks a hidden branch and shows it again on request.
8. (D160) **Phase 4's leftovers first**: the details panel's wrapping SHA, the resolver's disabled
   arrows that look enabled, the Cancel tooltip while the merge tool runs, two stale comments, the
   watcher test on the wall clock.
9. (D161) **Every stash's line to its commit is dashed**, as the working changes' line to HEAD is.
10. (D162) **Two selected commits are compared at once** in the right panel; branches are compared
    from their menus.
11. (D163) **Ctrl+Z and Ctrl+Y** (Cmd+Z and Cmd+Shift+Z on macOS) undo and redo git actions when
    no text box, editor, dialog or menu takes them.

Conventions, as in phase 4's design: `<b>` a local branch's short name, `<r>` a remote, `<sha>` a
full commit id, "short SHA" its first 7 characters, `{cur}` what HEAD is on as menus name it
(`RepositoryMenuContext.CurrentName`). The app passes git full ref names or full SHAs wherever git
accepts them. Every git call goes through `GitRunner` with D30, D68, D74, D102 and D128's
environment; reads add `GIT_OPTIONAL_LOCKS=0` (D47). Counts in texts use the singular for one
(D113). Phase 5 adds no package unless section 0 says so.

## Contents

0. Facts checked before deciding
1. The shell: toolbar, status bar, keys
2. The activity log
3. Search
4. File history and blame
5. The reflog and backups view
6. Compare
7. Undo and redo
8. Solo and hide
9. C12: protected changes and the automatic stash
10. Stashes drawn dashed
11. What the graph area holds now
12. Texts
13. UI inventory: menus, questions, colours, icons, automation ids
14. What changes for earlier phases
15. Tests and harness
16. Work breakdown
17. Decisions

## 0. Facts checked before deciding

Run with Git 2.36.0.windows.1 in throw-away repositories, isolated as the tests are
(`GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`, `LANGUAGE=en`, `LC_ALL=en_US.UTF-8`,
`GIT_EDITOR=:`, `GIT_SEQUENCE_EDITOR=:`, `GIT_TERMINAL_PROMPT=0`, `GIT_LITERAL_PATHSPECS=1` unless a
row says otherwise), and read in the app's code. **Git 2.55.0.windows.5, CI's version, is on the
development machine** inside Visual Studio
(`C:/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/CommonExtensions/Microsoft/TeamFoundation/Team Explorer/Git/mingw64/bin/git.exe`;
put its `mingw64/bin` first on `PATH`, or its child processes fall back to 2.36). The reflog and
solo-and-hide facts were also run on it; it opened reftable repositories, which 2.36 cannot
(`fatal: unknown repository extension found: refstorage`). Where a fact was not run on 2.30 (the
app's minimum) or 2.55 it says UNVERIFIED. Timings were taken on the shared laptop and are
indications.

### 0.1 File history and blame

| What | Result |
|---|---|
| The history command | `git log --follow --root -z -M --name-status --no-show-signature --no-color --encoding=UTF-8 --format=%H%x1f%P%x1f%aN%x1f%aE%x1f%aI%x1f%cI%x1f%s <start sha> -- <path>`: per commit the graph's own header (parsed by `TryParseLogLine`) ended by `\0`, then `\n` before the first entry, then `status\0path\0` (`R` and `C`: `status\0old\0new\0`, with a score such as `R088`). The next header follows the last path directly. The runner splits stdout at `\n`; joining the chunks with `\n` gives the stream back, which is then split at `\0`. Paths are raw UTF-8 bytes (`-z`: `core.quotePath` does not apply) |
| What `--follow` lists | A rename at 50 % similarity or more is followed (`R100`, `R088`); below 50 % it is an `A` and the older history is not reached (as `ReadDiffAsync`'s `--find-renames`, also 50 %). A copy shows as `C100 src copy` (follow turns on copy detection) and goes on with the source's history. A file deleted and re-added, or a new file at an old renamed name, lists both lives (`A`, `D`, …). A deleted file from HEAD lists the deletion first |
| What `--follow` leaves out | **Merges**: no merge is listed, also one whose conflict resolution wrote into the file (blame can still name it). `-m` lists merges twice, `--cc` every merge, `--diff-merges=first-parent` is 2.31+ and duplicates branch changes. A rename on a branch while another branch kept editing the old name loses the other branch's edits (git's own limit; the walk order decides which) |
| Empty, errors | An untracked file, a file only in the index, a path in no commit: no output, exit 0. A staged, uncommitted rename: nothing under the new name. Two pathspecs: `fatal: --follow requires exactly one pathspec` (128). Unborn HEAD: `fatal: your current branch 'main' does not have any commits yet` (128). A directory path lists every file under it |
| Settings | `log.showRoot=false` drops the root commit's row: pass `--root`. `diff.renames`, `log.follow`, `core.quotePath` do nothing here. `GIT_LITERAL_PATHSPECS=1` keeps `pages/[id].txt` literal (without it the brackets match `pages/d.txt`) |
| Timings | Default order streams: first row 147 ms, all 2.06 s on a 100k clone without a commit-graph file; `--date-order` and `--topo-order` print nothing for 1.3 s. 300 commits: 65 to 140 ms. With a commit-graph file 0.79 s (`--follow` gets no Bloom filters) |
| Each row's diff | `DiffTarget(Commit, <path at C>, <kind>, <old path for R and C>, Commit: C, Parent: C's first parent)` runs `git diff --find-renames P C -- new old`; a root commit `Parent: null` runs `diff-tree -p --root`. `R100` gives header lines and no hunk; `C` shows the copy as a new file (the diff does not detect copies). No git-layer change |
| The blame command | `git -c i18n.logOutputEncoding=UTF-8 blame --porcelain --no-textconv [<sha>] -- <path>`, read as Latin-1 (D66). Per group a header `<sha> <orig line> <final line> [<count>]`; the first time a commit appears its metadata lines (`author`, `author-mail`, `author-time` (Unix seconds), `author-tz`, the committer's four, `summary`, `boundary` for a root, `previous <sha> <path>` when the line came from an earlier commit, `filename <path at that commit>`, C-quoted when needed); then `\t<the line's bytes>`. `filename` and `previous` repeat at a group of a commit met under more than one path. Lines of one commit in a row may come in several porcelain groups: the view groups consecutive final lines by SHA itself |
| Blame cases | Working tree (no revision): lines that differ from HEAD carry the 40-zero SHA, author `Not Committed Yet`. A whole-file rename is followed without `-M`. A CRLF file keeps `\r` in each line. A binary file is blamed (NUL bytes in the lines). An empty file prints nothing (exit 0). A conflicted file during a merge is blamed merge-aware (each side's lines to their commits, markers uncommitted). A staged rename blames under the new name. An index-only file: every line uncommitted |
| Blame errors (128) | Untracked or unknown path, a directory: `fatal: no such path '<p>' in HEAD`. Deleted in the working tree: `fatal: Cannot lstat '<p>': No such file or directory` (blame `HEAD` instead). A path deleted at the revision: `fatal: no such path <p> in <rev>` (blame its parent). Unborn HEAD: `fatal: no such ref: HEAD`. `blame.ignoreRevsFile` naming a missing file: `fatal: could not open object name list: <file>`, which no option on the command line avoids |
| Blame settings | `blame.ignoreRevsFile` changes the attribution (the user's choice: honoured). textconv applies by default (`--no-textconv` gives the diff view's bytes). `i18n.logOutputEncoding` changes author and summary bytes (fixed by the `-c`). `.mailmap` applies, as to the graph's `%aN`. `blame.showRoot`, colouring, dates, `blame.blankBoundary` and the like do not change the porcelain |
| Blame timings | 3,017 lines and 300 commits: about 0.4 s (`--incremental` gives the first entry at 45 ms). One old line in the 100k repository without a commit-graph: 1.9 s |

### 0.2 The reflog

| What | Result (2.36 and 2.55, files and reftable alike unless said) |
|---|---|
| Reading | `git log --walk-reflogs` (the same as `git reflog show`) goes through the ref backend, so it also works under reftable (no `.git/logs` there). `%gD` the selector (`HEAD@{3}`, `refs/heads/main@{0}`), `%gd` its short form, `%gs` the entry's message, `%gn` and `%ge` who wrote it. **No placeholder gives the entry's own time**: with `--date=raw` the selector becomes `HEAD@{<seconds> <zone>}`; one call gives either the index or the time, so the view reads twice (5.2). **No placeholder gives the old value** |
| Gaps | An entry whose new value is not a readable commit (a missing object, or the null value that renaming the checked-out branch writes into HEAD's reflog) is skipped silently and its number is missing: `HEAD@{0}`, then `HEAD@{2}` |
| Missing and empty | A deleted branch's reflog goes with it; `refs/stash` goes with the last stash. `git log -g <ref> --` then fails (`fatal: bad revision`, 128) unless `--ignore-missing` comes before the ref: no output, exit 0. Tags and `refs/visualcommit/backup/*` have no reflog. A clone writes no reflog for `refs/remotes/<r>/<b>` until the first fetch or push |
| Order | Newest first, by index. Several refs in one call are merged by time, and with the tests' fixed clock all of the first ref's come first: never merge reflogs in one call |
| "Lost" | `git rev-list --ignore-missing --stdin --not --branches --tags --remotes HEAD` with the entries' SHAs on stdin: one process; 0.14 s when the entries lie near the tips, about 1 s for entries 50,000 commits deep on the 100k repository without a commit-graph (0.25 s with one). One call per SHA took 30 s or more for 124 SHAs |
| What the app's operations write (2.36) | checkout: HEAD `checkout: moving from A to B` (also `main to main`); tracking branch: `branch: Created from refs/remotes/origin/x`; create branch: `branch: Created from <start as given>`; rename: the reflog moves and gains `Branch: renamed …`; delete: the reflog is deleted; fast-forward of another branch: `fetch --no-auto-maintenance --no-write-fetch-head . <src>:refs/heads/<b>: fast-forward`; merge: `merge <ref>: Fast-forward` / `Merge made by the 'ort' strategy.`; **pull's merge: `merge <full sha>: …`** (not `FETCH_HEAD`); a conflicted merge then the app's commit: `commit (merge): <subject>`; reset (any mode): `reset: moving to <sha>`; commit and amend: `commit: …`, `commit (amend): …`; cherry-pick: `cherry-pick: <subject>` per commit, and after a conflict the app's commit `commit (cherry-pick): …`; revert: `revert: Revert "<subject>"`, after a conflict `commit: …`; rebase: `rebase (start): checkout <onto>`, `rebase (pick): …`, `rebase (finish): returning to refs/heads/<b>` (2.36 adds `(continue)` and `(skip)` after a Continue or Skip, 2.55 does not); the app's guarded amend in an interactive rebase: `rebase: Squashed …` on 2.36, `commit (amend): …` on 2.55; **stash push: HEAD `reset: moving to HEAD`**; pop with `--index` of a stash with an index part: HEAD `reset: moving to HEAD`; merge abort `reset: moving to HEAD`; cherry-pick abort or skip `reset: moving to <sha>`; rebase abort `rebase (abort): updating HEAD` (2.55: `returning to refs/heads/<b>`); fetch: `fetch --progress <r>: fast-forward | forced-update | storing head` (2.55 also writes `refs/remotes/<r>/HEAD` with `fetch`); push: `update by push`; tags, upstreams, backups: nothing |
| `GIT_REFLOG_ACTION` | On `switch` it replaces `moving from A to B` and so breaks `@{-1}` and `git switch -`; on `reset` it hides the target. The app never sets it; `update-ref -m <text>` writes `<text>` exactly |
| Expiry | `gc`, `gc --auto` and `maintenance run --auto` expire entries older than 90 days, and entries 30 to 90 days old whose commit no ref reaches; `refs/stash` entries never expire. An entry keeps its commit alive until it expires. The tests' fixed committer date (2026-01-02) makes every entry look months old to a `gc` |

### 0.3 Search and compare

| What | Result |
|---|---|
| Search exactly the graph's rows | `git log --no-walk=unsorted --stdin --no-show-signature --no-color --format=%H <mode options>` with the rows' SHAs on stdin (commits and stashes, row order) prints only those, in that order, and **flushes after every hit**. It reads all of stdin before printing: 2,000 SHAs 67 ms to the first hit, 100,000 SHAs 0.8 to 1.6 s. Chunks (the first 2,000 rows, then 20,000 per process) give the first hit at about 70 ms on 100k for 0.25 s more in total. An unknown SHA: `fatal: bad object <sha>` (128) |
| Message | `-F --regexp-ignore-case --grep=<text>`: the whole message (subject and body, not headers), Unicode case folded. `-F` also guards against the user's `grep.patternType` (with `extended`, `--grep='(issue'` fails: `fatal: … Unmatched ( or \(`) |
| Author | Git matches `Name <email>` with the mailmap applied, as the graph's `%aN`/`%aE` already are: the same result in memory, no process |
| SHA | In memory: the graph holds every row's SHA (stashes included); a prefix may match several rows |
| File | Needs `GIT_LITERAL_PATHSPECS` removed for the call (as `AllIgnoredAsync` does): `":(glob,icase)**/*<t>*"` and `":(glob,icase)**/*<t>*/**"` (a glob matching a folder does not match files under it, so two pathspecs), with `\` turned into `/` and `*?[\` escaped by `\`. With the variable set the magic is read as a file name and finds nothing, silently; `git --glob-pathspecs` and `GIT_GLOB_PATHSPECS` refuse to combine with it (`fatal: global 'literal' pathspec setting is incompatible…`). `icase` folds ASCII only. Finds deleted files and both names of a rename commit |
| Merges in path and pickaxe searches | Default simplification prunes side-branch commits whose effect was undone; `--full-history` adds merges that differ only from their second parent. **`--first-parent` with `--no-walk --stdin` gives exactly "the commit's own change as the right panel shows it"** (a merge against its first parent, the right panel's rule) |
| Changes | `-S<text>`: a commit whose number of occurrences of the text changes (added or removed: O7's wording); fixed string; searches binary files; `-i` folds ASCII only. `-G<regex>` matches any changed line containing it (extended regex, skips binary). Merges never match without a merge option; `--diff-merges=first-parent -s` (2.31+) or `--first-parent` (2.29+ implies first-parent diffs, UNVERIFIED on 2.30) matches a merge's first-parent change once. `log.showRoot=false` misses the root (pass `--root`); `diff.renames=false` makes a rename look like delete-and-add (pass `--find-renames`); textconv applies (pass `--no-textconv`). An empty `-S` is exit 129 or a `BUG` (exit 3): never pass one |
| Search timings | Message on 100k: 1.6 s first hit, 1.8 s all (chunked: 70 ms first). File glob on 100k: 1.9 s. `-S` costs about 0.4 to 1 ms per commit with real changes (`-G` 2 to 3 times that): tens of seconds on a large real history |
| Stash rows | A stash commit is a merge (base, index commit, untracked commit); with its SHA on stdin and first-parent options it matches by its working-tree changes; its untracked files are only in `^3` and are not searched (the details panel does not show them either) |
| Compare list | `diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id <old> <new>` with two SHAs, parsed by `ParseNameStatus`. **`diff-tree A...B` prints nothing and exits 0** (a range is ignored). `diff.renameLimit` applies (a warning `exhaustive rename detection was skipped…` turns renames into delete and add) |
| Three-dot | `git merge-base A B` once, then the two-SHA list from the base. Exit 1 with no output: unrelated histories. Several bases (criss-cross): `merge-base` prints one; `git diff A...B` uses the same with a warning; `--merge-base` fails (`fatal: multiple merge bases found`, 128). Two-dot shows A's own later changes reversed (A's rename appears renamed back) |
| Counts | `rev-list --left-right --count A...B` prints `L\tR`; meaningless for unrelated histories and for a stash (it counts the stash's own commits). 0.4 s on 100k |
| Per-file diff | `DiffTarget(Commit, path, kind, oldPath, Commit: new, Parent: old)`: git pairs any two paths given, so the old path comes only from the list's own `R`/`C` entry |

### 0.4 Undo: moving HEAD, branches and commits back

| What | Result |
|---|---|
| Moving the checked-out branch back with uncommitted work | `reset --merge` loses staged changes without a word; `reset --keep` keeps contents but replaces the index (a partly staged file loses its staged version); `switch -C` keeps everything but writes `checkout: moving from main to main`, which breaks `@{-1}`. **`git read-tree -m -u <now> <target>` then `git update-ref -m <msg> refs/heads/<b> <target> <now>`** keeps staged, partly staged and unstaged work, refuses (changing nothing) when a file the move changes has local changes or an untracked file is in the way, and is the only one with a compare-and-swap. If the update-ref then fails (a race), `read-tree -m -u <target> <now>` puts the files back exactly. `read-tree -n -m -u` is a dry run that writes nothing |
| Refusals of read-tree | `error: Entry 'f.txt' not uptodate. Cannot merge.`, `error: Entry 'f.txt' would be overwritten by merge. Cannot merge.`, `error: Untracked working tree file 'd.txt' would be overwritten by merge.`, `… would be removed by merge.` (128); it stops at the first |
| Compare-and-swap | `update-ref -m <msg> <ref> <new> <old>`: `fatal: update_ref failed for ref '<ref>': cannot lock ref '<ref>': is at <sha> but expected <sha>` (128). `<old>` empty = must not exist (`reference already exists`). `--stdin` with `update`/`create`/`delete`/`verify` lines is all or none. **Always name `refs/heads/<b>`**, never `HEAD`: after a terminal checkout of another branch at the same commit, `update-ref HEAD` moved that other branch. `update-ref` moves a branch checked out in another worktree without a word (`%(worktreepath)` of `for-each-ref` tells) |
| Soft moves | Undo of a commit, an amend or a soft reset: `update-ref refs/heads/<b> <before> <after>`: the commit's changes come back staged, the working tree untouched; exact. The initial commit: `update-ref -d refs/heads/<b> <after>` (the branch becomes unborn, the index kept); redo `update-ref refs/heads/<b> <after> ""` |
| In-progress states | `update-ref` refuses nothing during a merge, rebase or pick; `reset --soft` (`fatal: Cannot do a soft reset in the middle of a merge.`), `switch` (`fatal: cannot switch branch while merging`) and read-tree with conflicts do. The app's own in-progress check must come first |
| Mixed and hard reset | Undo mixed: the move back, then `git restore --staged --source=<reset-…-changes> -- .` (exact, partly staged files included); precondition: the index unchanged since (`diff-index --cached --quiet <after>`). Undo hard: the move back (read-tree), then the index from the snapshot's second parent, then the files (`RestoreSnapshotAsync`), in that order; precondition: none of the snapshot's paths changed since (`git restore --worktree` overwrites without a word). `read-tree -m <tree>` refuses an index with a partly staged file; plain `read-tree <tree>` drops stat data |
| Checkouts | Undo: `git switch --no-guess <before>` (or `--detach <sha>`): it carries work or refuses with the checkout texts. A checkout that created a tracking branch: switch back, `delete refs/heads/<b> <tip>` through `update-ref --stdin`, `config --remove-section branch.<b>` (`update-ref -d` leaves the config). Redo of that: `switch --no-track --create <b> <tip>` and the two config keys (`--track` with a SHA fails: `fatal: cannot set up tracking information; starting point … is not a branch`) |
| Branches | Create: `delete … <tip>`, remove its config section; redo `create`. Rename: `branch --move <new> <old>` (refused when `<old>` exists again: `fatal: a branch named '<old>' already exists`); a case-only rename on Windows cannot be undone by a rename. Fast-forward of a branch not checked out: compare-and-swap, refused when it is checked out in a worktree |
| Keeping undone commits | Branch and HEAD reflogs keep an undone commit for 30 days from the undo (`gc.reflogExpireUnreachable`), so a redo within the session finds it; backup refs keep snapshots and have no reflog, so only the clean-up can lose them (D139's `keep`) |
| Cost | Every git call costs about one process start on this laptop (160 to 290 ms busy, about 40 to 70 ms quiet): an undo of 3 to 5 calls takes 0.5 to 1 s here |

### 0.5 Undo: stashes, tags, remotes, pushes, files

| What | Result |
|---|---|
| Stash and pop back | `git stash push --include-untracked` (without `GIT_LITERAL_PATHSPECS`, D108: with it the untracked files stay behind) then `git stash pop --index stash@{n}` gives back `status --porcelain=v2` and `ls-files --stage` byte-identical (a partly staged file, staged deletions and additions included). A plain `stash apply` loses what was staged. `stash push` writes HEAD's reflog `reset: moving to HEAD` |
| Partial refusals | `stash pop --index` with an overlapping local change is refused, keeps the stash, **but restores the stash's untracked files anyway**; a staged overlap is not refused but conflicts (the staged version becomes a side). So a stash's undo checks everything before git runs |
| Stash store | `git stash store -m "<message>" <sha>` puts an entry back at `stash@{0}` with that exact message (without `-m`: `Created via "git stash store".`); it accepts any commit and the same stash twice, so the app checks the SHA is not listed first |
| Restoring index entries | `git update-index -z --index-info` fed `ls-files --stage -z` lines (a `0 <zeros> 0\t<path>` line first) restores a path's index entries exactly, unmerged stages included; it loses the intent-to-add and skip-worktree flags. `write-tree` fails on a conflicted index |
| Tags | Undo of a create: keep the object in a `tag-<time>/<t>` backup, then `update-ref -d refs/tags/<t> <object>`; redo `update-ref refs/tags/<t> <object> ""` brings back the same tag object. Name taken (also by case on Windows): `reference already exists` |
| Pushes | Undo of a new branch on a remote: `git push --force-with-lease=<RR>:<P> <r> --delete <RR>`; of an update: `--force-with-lease=<RR>:<P> <r> <O>:<RR>`; redo: `--force-with-lease=<RR>: <r> <P>:<RR>` (empty expect: must not exist) or `--force-with-lease=<RR>:<O> …`. Force push undo `--force-with-lease=<RR>:<P> <r> <B>:<RR>` and back. `<O>`, the remote's old value, comes from the push's own output line (`   <o>..<p>  …`, ` * [new branch]`), never from the remote-tracking ref, which can be stale. Every lease failure is ` ! [rejected] … (stale info)` (exit 1), already `PushRejectedStale`. `Everything up-to-date` on a "must not exist" push means it is already there: done. Tag push undo `--force-with-lease=refs/tags/<t>:<T> <r> --delete refs/tags/<t>` and back |
| Remote names | A force push's backup is named by the remote-tracking short name, which may not contain the remote's name at all (`fetch = +refs/heads/*:refs/remotes/mirror/*`); remote names may contain `/`. In the session the undo entry records the remote and its ref; out of it, the upstream config or the fetch refspecs map the name back, and an ambiguous one is refused |
| Remotes and upstreams | `git remote remove` removes the remote's section, `remote.pushDefault` and the `branch.*.remote`, `.merge`, `.pushRemote` keys that name it, every `refs/remotes/<r>/*` and their reflogs. Replaying the recorded `config --local --null --list` entries with `config --add` and re-creating the refs with `update-ref --stdin` brings it back (keys lower-cased, sections at the end: meaning unchanged). `remote rename` back and forth leaves `.git/config` byte-identical. `set-url` refuses a remote with two `url` values. Upstream keys are put back with `config --local`, not `--set-upstream-to` (refused for a gone upstream) |
| Branch delete | `branch --delete --force` removes every `branch.<b>.*` key and the reflog; undo: `update-ref --create-reflog -m <msg> refs/heads/<b> <tip> ""` and the keys replayed |
| Discard | `RestoreSnapshotAsync` from the `discard-<time>` snapshot; precondition: the files unchanged since (SHA-256 of the raw bytes, as the restore bar does). Snapshots go through git's filters: under `core.autocrlf=true` an LF file comes back CRLF (git's own `stash` does the same) |
| .gitignore | The app's append is undone by truncating to the old length (or deleting a file it created), when the file's hash is still the one it left |
| Resolutions | Undoable with the index entries and raw bytes recorded; `update-index --unresolve` and `checkout -m` are not exact |

### 0.6 Solo and hide

| What | Result (2.36 and 2.55 alike) |
|---|---|
| Hide | `--exclude=<pattern>` applies to the next `--branches`, `--remotes` or `--tags` only, and its pattern is the name relative to that namespace: a local branch's short name, `<remote>/<b>`, a tag's name (`GitRef.Name`); a full name does nothing there. It is a case-sensitive glob in which `*` crosses `/`; ref names cannot hold `*?[\`, so an exact name needs no escaping. A stale name does nothing |
| `<remote>/HEAD` | `--remotes` takes the symref `refs/remotes/<r>/HEAD`, which keeps its target's history: hiding `origin/main` does nothing until `--exclude=origin/HEAD` is added too. A branch can be named `feature/HEAD`, so never `--exclude='*/HEAD'` |
| HEAD | `HEAD` on the command line keeps the checked-out branch's history even when it is excluded |
| Upstreams | Hiding `long-a` alone left 2,500 of its 3,000 commits on screen through `origin/long-a`; hiding both left none |
| Solo | `--ignore-missing <full name> … HEAD --` (`--ignore-missing` must come before the names; a short name is ambiguous: tag `x` wins over branch `x`) |
| `--stdin` | On 2.36 (and so 2.30) only plain revisions and `^rev` lines; `--branches`, `--exclude`, `--not` on stdin need 2.42 (UNVERIFIED version; 2.55 accepts them). `^ref` removes shared history too, so it cannot hide. An empty stdin means HEAD |
| Command-line length | A .NET `Process.Start` command line over about 32,760 characters throws `Win32Exception` 206 and git never starts: about 800 hidden refs of 25-character names |
| Effect | On a 13,000-commit history, hiding 9,000 commits roughly halved git's walk and stream; on the 100k repository (one merged branch) nothing changes |
| Stashes | A stash whose base is hidden would be placed at the top with a lane that never ends |
| Outside renames | A renamed hidden branch shows again (the stored name is stale); a deleted and re-created one is hidden again |
| GitKraken | Hidden refs get a grey icon, soloed an orange one; several refs can be soloed; branches, tags, remotes and stashes can be hidden from the left panel; whether a local branch's upstream follows is not documented |

### 0.7 C12

| What | Result |
|---|---|
| What a checkout refuses | A tracked change (staged or not) to a path the target changes; an untracked file or folder where the target has a file. A change to a path the target does not change is carried along. An unstaged deletion of a file the target changes is silently undone. A staged change equal to the target is allowed. Conflicted files: `error: you need to resolve your current index first` (stdout `<p>: needs merge`). An operation in progress: `fatal: cannot switch branch while merging` (128). The four checkout commands give the same result and text |
| Ignored files | A checkout **silently overwrites an ignored file** the target tracks and **deletes an ignored folder** where the target has a file; `--no-overwrite-ignore` (a `git switch` option since 2.23) turns both into ordinary refusals |
| A refusal changes nothing | HEAD, the index's bytes, every ref, HEAD's reflog, the files and the status are unchanged; `--create` creates no branch |
| The texts | `error: Your local changes to the following files would be overwritten by checkout:` / `\t<path>` … / `Please commit your changes or stash them before you switch branches.`; `error: The following untracked working tree files would be overwritten by checkout:` / … / `Please move or remove them before you switch branches.`; `error: Updating the following directories would lose untracked files in them:` / … / an empty line; then one `Aborting`. Several blocks can come together (index changes first). Every path is listed, raw. **The count**: the distinct tab lines under those headers |
| Pre-check or run | `read-tree -n -m -u` predicts the same only after `update-index --refresh` (which writes the index), stops at the first path and costs more than the refused checkout itself; a diff of the paths is wrong both ways. Running the checkout is exact and costs one process |
| Other refusals | Merge, pick, revert with an overlapping unstaged change: the same blocks with `by merge:` (counted the same way). **Anything staged** refuses a merge (`Your local changes to the following files would be overwritten by merge:` and the names on one line after two spaces, no `error:` with ort), a cherry-pick (`error: your local changes would be overwritten by cherry-pick.`, no names) and a revert likewise. Rebase: `error: cannot rebase: You have unstaged changes.` / `Your index contains uncommitted changes.` (any change). Stash apply/pop: partial (0.5) |
| Is it on the branch | `git merge-base --is-ancestor <target> HEAD` (0 yes, 1 no, 128 error), second parents included, tags peeled; 0.15 to 1 s on 100k. The graph's `IsReachableFromHead` answers the same for loaded rows, for free |
| The automatic stash | `git stash push --include-untracked --message "Auto stash before checking out <name>"` (without `GIT_LITERAL_PATHSPECS`), then `git switch --detach <target>`: the stash list shows `On main: Auto stash before checking out <name>` with **first parent the branch's tip**; going back to the branch and `stash pop --index` gives the status back identical. Nothing to stash: `No local changes to save` (also with only ignored files, or only a staged submodule change). Conflicted files: the stash refuses (`<p>: needs merge`, exit 1). **During a merge without conflicts the stash succeeds and deletes `MERGE_HEAD`**, and the checkout then succeeds: the merge is lost without a word. Detached HEAD: `On (no branch): …`. Unborn HEAD: `You do not have the initial commit yet` |
| Ignored files and the automatic stash | An ignored file that an old commit tracks is overwritten by the detached checkout and then deleted when the branch is checked out again: **lost**. With `--no-overwrite-ignore` the checkout is refused, and `stash pop --index` then restores everything |
| Windows file locks | An untracked file opened exclusively by another program: the stash fails before saving anything. One open for reading only: the stash is saved, git says `warning: failed to remove <file>` and exits 1, and the working tree is **not** reset: the changes are in both places |

## 1. The shell: toolbar, status bar, keys

### 1.1 The toolbar's Undo, Redo and Search

Undo and Redo become real (section 7) and keep their places at the toolbar's left; Search becomes
real (section 3) and keeps its place at the right, left of Theme. They bind to the active tab's
repository, as Fetch to Pop do (`ActiveTab.Toolbar`): `UndoCommand`, `UndoToolTip`, `RedoCommand`,
`RedoToolTip`, `SearchCommand`, `SearchToolTip` on `ToolbarViewModel`, which reads them from the
repository's undo history (7) and graph area (11). An empty tab's toolbar keeps every button
disabled, so phase 0's empty shell stays as it is (only Theme enabled).

| Button | Enabled when | Tooltip, enabled | Tooltip, disabled (first that holds) |
|---|---|---|---|
| Undo | the repository's undo history has a step before its cursor that can be undone now (7.4) | `Undo {step}` (`{step}` is the step's own text, 7.6: `Undo commit "Fix the parser"`) | 7.4's reasons in their order: `Wait for {title} to finish`; `Finish or abort the {kind} first`; `Nothing to undo`; the step's check, e.g. `main has moved since (it is at 9f8e7d6)` |
| Redo | a step after the cursor that can be redone now | `Redo {step}` | the same, with `Nothing to redo` |
| Search | a repository is open and the graph area may show the graph (11.2) | `Search commits ({gesture})`, `{gesture}` being `Ctrl+F` or `Cmd+F` from the platform (1.3) | `Close the interactive rebase first`, `Close the conflict resolver first` |

Undo and Redo are the one exception to phase 3's rule that an enabled toolbar button has no
tooltip (D90): the step a click acts on is not otherwise visible, and the first click must not
surprise. Their tooltip is also their automation help text (`AutomationProperties.HelpText`), so
the real-window pass can read it.

A click on Undo or Redo runs the step as one local operation of the repository's queue (7.5). A
click on Search opens the search box (3.1).

### 1.2 The status bar's Activity log

`ActivityLogToggle` becomes real: enabled while the active tab has a repository (an empty tab keeps
it disabled, so phase 0's pictures stay as they are), tooltip `Show every git command the app has
run` while the panel is closed and `Hide the activity log` while it is open. It toggles the panel
(section 2) for the window: the panel's being open is the window's state, not a tab's, and shows
the active tab's repository. The button draws its "open" state with the `selected` class of the
flat button (the accent underline phase 1's tabs use is not reused: the button keeps its look, and
`VcTextPrimaryBrush` replaces the secondary colour while the panel is open).

### 1.3 Keys

There are no window-wide shortcuts today (the code map, menus.md section 6). Phase 5 adds three, in
`MainWindow`'s key handling, each built from the platform's `HotkeyConfiguration` (never from a test
of the operating system), as `CommitGraphControl.CommandModifiers` is:

| Keys | Does | Declines (the key goes on as before) when |
|---|---|---|
| the platform's `HotkeyConfiguration.Undo` (Ctrl+Z; Cmd+Z) | the toolbar's Undo (D163) | a dialog is open; a menu is open; a branch drag runs; the focus is in a `TextBox` (Avalonia's text undo) or anywhere in the conflict resolver (its own undo and redo, D148, whether it shows text or a card); the interactive rebase's list is open; Undo is disabled (nothing happens, the key is handled) |
| every gesture in the platform's `HotkeyConfiguration.Redo` (the builders record the lists Avalonia 12.1.3 gives on Win32, macOS, X11 and headless; the owner's words were Ctrl+Y and Cmd+Shift+Z) | the toolbar's Redo | as Undo |
| the command modifier with F (Ctrl+F; Cmd+F) | the toolbar's Search | a dialog or menu is open; Search is disabled. In the search box it selects the box's text |

Undo and Redo are handled on the window's bubbling `KeyDown` without `handledEventsToo`, so a text
box or the resolver that took the key first keeps it; the declines above stop the keys that such a
control does not mark handled (a text box without a change to undo). Search's key is handled in the
tunnelling phase, as `OnShiftF10` is (D106), because the graph control and the diff's editor would
not mark it handled anyway and nothing else uses it.

`ShellDriver` presses them through new helpers `PressGitUndo()`, `PressGitRedo()` and
`PressSearch()` (phase 4's `Undo()` and `Redo()` stay the text-undo gestures of C10 and C14).

## 2. The activity log

### 2.1 Where it is

A panel under the graph area (D156): `CommitGraphView` becomes a grid of the graph area's present
content (the banner and the graph, list, diff, resolver or a page of section 11) above a 5-high
splitter (`ActivityLogSplitter`, a 1-px `VcBorderBrush` line in a 5-px grab area) and the panel
(`ActivityLog`, `ActivityLogView`). It spans what the graph area spans: column 1, or columns 1 and 2
while the resolver is open. The side panels keep their full height.

- **Height**: 200 by default, at least 120, at most the graph area's height less 160 (the banner and
  the graph keep at least that). Dragging the splitter sets it; letting go saves it in the session
  (`SessionState.ActivityLogHeight`, null for the default). When the window is too low for both,
  the panel gives way first, down to 120, and then hides its rows below its header; it takes its
  height back when the window grows (as `PanelLayout.Fit` does for the side panels).
- **Open or closed**: closed when the app starts; the status bar's button (1.2) opens and closes
  it, and so do the panel's close button and Esc while the focus is inside it (`EscapeOrder.ActivityLog`
  = 380: after the resolver and the list, before the diff). A closed panel takes no room, so every
  picture of phases 0 to 4 keeps its layout.
- **Which repository**: the active tab's. Switching tabs shows the other repository's log at once;
  each repository keeps which rows are open and where it was scrolled.

### 2.2 What it shows

A header 30 high (`ActivityLogHeader`): `Activity log`, then the repository's name in the secondary
colour, then on the right a check box `Show reads` (`ActivityLogShowReads`, unchecked) and the close
button (`ActivityLogClose`, `IconClose`). Below it a virtualised list (`ActivityLogList`) of rows 24
high, oldest at the top and newest last (owner's answer 4); the list starts scrolled to the end and
stays at the end while new rows come, unless the user scrolled up.

**An operation's row** (`ActivityLogOperation`), left to right:

| Part | Content |
|---|---|
| Chevron | `IconChevronRight`, `IconChevronDown` while open; the row opens and closes on a click anywhere on it or with Right and Left |
| Outcome | a 10-px glyph in its tone: `IconCheck` in `VcSuccessBrush` (succeeded), `IconAlert` in `VcWarningBrush` (a warning or a stop with conflicts), `IconClose` in `VcDangerBrush` (failed), `IconMinus` in `VcTextSecondaryBrush` (cancelled); a running operation has none and shows `Running…` in its message |
| Time | when it started, `HH:mm:ss`, in the zone `DateDisplay` uses (D43) |
| Title | the operation's title (D113), e.g. `Check out feature/alpha`, `Undo commit "Fix the parser"` |
| Message | the status bar's text for it, in its tone; empty for the stage panel's writes (their message goes to the stage panel) |
| Duration | `{N} ms` below a second, `{N.N} s` above, right-aligned, secondary |

Title and message are cut with "…" (`TrimmedTextBlock`), the whole text in the tooltip.

**Opened**, an operation shows under it, indented 24, in this order:

1. Its notes (2.3), one line each, secondary: `Removed backup reset-20260102T090000000Z (older than 30 days)`.
2. Its git calls, one row each (`ActivityLogCall`), 22 high: a chevron, the exit code (`exit 0` in
   the secondary colour, another code in `VcDangerBrush`, `cancelled`, `failed to start`), the
   command (`GitCallRecord.CommandText`, credentials hidden, in the code font, cut with "…"), the
   duration. Opened, a call shows its standard output and standard error (as recorded: the first 16
   KiB of each, credentials hidden, D103) in a read-only box in the code font, 12 lines at most
   before it scrolls, each stream titled `Output` and `Error` when not empty, and `No output` when
   both are empty.
3. When the call log no longer holds the operation's calls (it keeps the last 2,000 of the session,
   reads included): `Its git commands are no longer kept.`

A failed operation also shows, before its calls, the error dialog's summary in the danger colour.

**Reads**: with `Show reads` checked, the git calls that belong to no operation (the app's reads:
refreshes, the details panel, diffs) show as rows of their own between the operations, by their
start time, as calls: `ActivityLogCall` rows at the top level. Unchecked, they are hidden; a read
made inside an operation (the operation's own `status`, `for-each-ref`) is one of its calls and
always shows under it.

**Context menu** on a row (`RepositoryMenus.ForActivityLogRow`): `Copy command` (a call), `Copy
output` (a call with output), `Copy title` (an operation). No other action: the log only shows.

**What it never shows**: an operation dropped before it started (a closed tab, D81's "Quit now"),
which has no record; the credentials of a prompt (the call log never has them, D103, and
`ShellDriver.WhereWritten` keeps proving it).

### 2.3 What the records must carry

Today `OperationRecord` is `(Id, Repository, Title, StartedAt, Duration, Outcome, Message)` in a
session-wide `OperationLog` of 500, with no event; `GitCallRecord` carries `OperationId`; the
backups' clean-up's removals reach only the log file (operations.md sections 3 and 4). Phase 5
changes:

- `OperationRecord` gains `RepositoryKey` (the repository's common git folder, full path: unique
  across tabs, unlike the folder name), `IsWarning`, `Kind` (Local or Network), `IsBackground`,
  `FailureSummary` (the summary the error dialog shows, or null) and `Notes` (a list of strings).
  `OperationQueue.Record` fills them from the entry and the result.
- `IOperationLog` gains an event `Added(OperationRecord)` (raised on the thread that adds; the
  panel posts to the UI thread), `Note(operationId, text)` (kept until that operation's record is
  added, then put in its `Notes`; a note for an id with no record after 10 minutes is dropped), and
  a `Running` entry: `OperationQueue` tells the log when an operation starts (`Started(id, key,
  title, startedAt)`) so the panel can show it as running.
- The backups' clean-up returns what it removed (`IReadOnlyList<RemovedBackup>`, already returned by
  `IGitRepository.CleanUpBackupsAsync`); `RepositoryOperations.CleanUpBackupsAsync` and the
  background "Clean up backups" pass each removal to `IOperationLog.Note(GitOperationScope.Current,
  "Removed backup {name} ({reason})")`, with the same words as the log file's line. The background
  operation's message becomes `Removed {N} backups` (still never shown in the status bar, D139).
- `GitCallLog` keeps its 2,000 calls and its `Added` event; the panel indexes calls by
  `OperationId` as they come.
- **Times for pictures.** `OperationRecord.StartedAt` and `Duration`, and `GitCallRecord.StartedAt`
  and `Duration`, come from a `RecordClock` (`TimeProvider`) that `AppSession.Start` passes to
  every queue and to the runner, `TimeProvider.System` by default. Both harnesses give a frozen
  clock at 2026-01-02 09:00:00 UTC (the app's commit time, D72): the headless one through
  `AppSession.Start`'s new `recordClock` parameter, the real-window pass through the environment
  variable `VISUALCOMMIT_RECORD_CLOCK=frozen` that `RealApp.Launch` sets. Every time then reads
  `09:00:00` and every duration `0 ms`, so the panel's pictures are the same in every run and in
  both harnesses. The real durations still go to the log file and Q1's measurements, which use
  their own stopwatches.

### 2.4 Tests

`ActivityLogViewModelTests` (records and calls as they come, the grouping by id, reads shown and
hidden, notes, an evicted operation's calls, the running row, per-repository state), `OperationLogTests`
(the event, notes before and after the record, the 500 limit), `ActivityLogViewTests` (the header, a
row opened by click and by Right, the splitter's limits, Esc, the session's height), and the
scripted checks of section 15.

## 3. Search

### 3.1 The box

The toolbar's Search, or its key (1.3), shows the graph (closing a diff or a page of section 11,
which hold no work; disabled while the interactive list or the resolver is open) and opens the
search bar (`SearchBar`, `SearchBarView`) at the top of `RepositoryGraphView`, under the banner and
above the column headers. It is 36 high, `VcChromeBackgroundBrush` with a 1-px `VcBorderBrush` line
along its bottom, and pushes the column headers and rows down by 36, as the banner does (D122: every
harness takes row positions from the control's bounds; the real-window pass's `GraphRowsTop` learns
the bar). Left to right, 8 from the edges, 6 apart:

| Part | Automation id | What |
|---|---|---|
| Mode button | `SearchModeButton` | `VcActionButton` labelled with the mode and `IconChevronDown`: `Commits`, `Files` or `Changes`. A click opens the mode menu through the menu layer (D141's reason: a combo box's dropdown is a window outside the capture): `Commits: message, author or SHA`, `Files: a path or file name`, `Changes: text added or removed`. The mode is kept per repository for the session |
| Text box | `SearchBox` | Takes the rest of the width (at least 120). Placeholder by mode: `Message, author or SHA`, `File name or path`, `Text added or removed` |
| Count | `SearchCount` | Secondary text: empty while the box is empty; `Searching…` while a search runs and found nothing yet; `{n} so far…` while it runs with matches; then `No matches`, `{n} matches`, or `{k} of {n}` while the selected commit is the k-th match in row order |
| Previous, next | `SearchPrevious`, `SearchNext` | `VcIconButton`s with `IconChevronUp` and `IconChevronDown`, tooltips `Previous match (Shift+Enter)` and `Next match (Enter)`; disabled without matches |
| Close | `SearchClose` | `VcIconButton`, `IconClose`, tooltip `Close the search (Esc)` |

Opening the bar puts the keyboard focus in the box with its text selected. The box searches as the
user types, 300 ms after the last key (Commits) or 600 ms (Files, Changes), and at once on Enter
when nothing ran for the text yet. A new search cancels the one running. An empty box clears the
marks. Enter in the box goes to the next match, Shift+Enter to the previous one: "after" and
"before" are in row order from the selected row (or from the top when nothing is selected),
wrapping round; the match is selected as a click selects it (the right panel shows it) and
scrolled into view (`ScrollIntoView`, not centred, so the eye can follow). Esc in the bar closes it
(`EscapeOrder.Search` = 390, before the diff's 400; the diff's handler declines in a text box
anyway). Closing clears the marks, keeps the selection, and gives the focus to the graph.

### 3.2 What each mode matches

The search covers exactly the rows the graph shows (with section 8's filter applied, so hidden
branches are not searched), commits and stashes: git is given the rows' SHAs, not the graph's refs
(0.3), through `IGitRepository.SearchAsync(mode, text, shas, onMatch, token)`, which runs
`git log --no-walk=unsorted --stdin --no-show-signature --no-color --format=%H <mode options>` with
`GIT_OPTIONAL_LOCKS=0`, the first 2,000 rows' SHAs on stdin, then 20,000 per call, one call after
another, each match handed over as git prints it. While the graph still loads, the rows loaded so
far are searched and the later ones as they come. Cancelling stops the running call and starts no
other.

| Mode | Matches | How |
|---|---|---|
| Commits | the whole message contains the text; or the author's name or e-mail contains it; or the SHA starts with it (4 to 40 hex digits). Case is ignored | message: git with `-F --regexp-ignore-case --grep=<text>`; author and SHA: in memory over the rows (`%aN`/`%aE` already apply the mailmap, as git's `--author` does); the union |
| Files | the commit's own change (against its first parent, as the right panel lists it) touches a path containing the text; `\` in the text is read as `/`; case is ignored (ASCII letters only, as git folds them: the tooltip of the mode's menu item says so) | `--first-parent -- ":(glob,icase)**/*<t>*" ":(glob,icase)**/*<t>*/**"`, `<t>` with `*?[\` escaped, with `GIT_LITERAL_PATHSPECS` removed for this call (as `AllIgnoredAsync` removes it) |
| Changes | the commit's own change adds or removes the text (the number of its occurrences changes, O7); case is ignored (ASCII letters) | `--first-parent --diff-merges=first-parent --root --find-renames --no-textconv -s --regexp-ignore-case -S<text>`; on a git older than 2.31 without `--diff-merges=first-parent` (`--first-parent` implies it from 2.29). Never with an empty text |

A match whose SHA is not a row (none should come) is dropped and logged at Debug. A call that fails
with `fatal: bad object` (the graph is older than a `gc` that pruned) stops the search quietly; the
next refresh runs it again. The Changes mode can take tens of seconds on a large history: its count
shows `Searched {k} of {n} commits…` while it runs, and the graph stays usable.

### 3.3 How the graph shows it

`CommitGraphControl` gains a styled property `Search` (`GraphSearchMarks`: an immutable set of
matching SHAs and whether a search is active), bound to the repository's search view model and
registered in `AffectsRender`; a change clears the row cache. While a search is active and its text
is not empty:

- a matching row gets a 3-px bar of `VcSearchMatchBrush` at its left edge (over the Branch / Tag
  column's first 3 px) and its message in `FontWeight.SemiBold`;
- every other commit row is dimmed: its node, labels and texts are drawn at 35 % opacity
  (`PushOpacity` around the node and around the row's text and label cells; the lanes passing
  through stay at full strength, so the history's shape stays readable);
- the selection and hover backgrounds are unchanged, so the match Enter went to is the selected
  row.

With no search active the control draws exactly as today: every picture of phases 1 to 4 that has
no search keeps its pixels (apart from section 10's stash lines and section 14's toolbar).
Lookups are a hash set per visible row, within D50's frame budget.

### 3.4 Results and reloads

Matches arrive as SHAs while git prints them (3.2) and are applied in batches at most every 100 ms;
the count follows. A SHA the graph does not have is ignored (none should come: the search names the
graph's refs). A reload of the graph (a refresh after an operation or an outside change) runs the
search again on the new graph, keeping the selection; closing the tab cancels it.

## 4. File history and blame

Both open in place of the graph as pages of the graph area (section 11, D158), from the file menus
(4.4), and from each other.

`IGitRepository.ReadFileHistoryAsync(startSha, path, onRow, token)` runs 0.1's history command and
hands over a `FileHistoryRow` (the commit as the graph's `CommitInfo`, the entry's status, the path
at that commit, the old path of an `R` or `C`) for each commit as git prints it. No merge is listed,
as `git log --follow` lists none (0.1); a file renamed on a branch while another branch edited the
old name misses that branch's edits (git's own limit; the handoff says so). The start is resolved
to a full SHA when the page opens and stays fixed.

`IGitRepository.ReadBlameAsync(sha or null, path, token)` runs 0.1's blame command (no revision for
the working tree) and returns a `BlameResult`: the lines' bytes (Latin-1 strings, as diff lines are)
and, per line, its commit (`BlameCommit`: SHA, author, e-mail, author time and zone, summary, the
path at that commit, `previous`); the zero SHA is "not committed yet". The parse is a three-state
machine (a header, metadata lines, a tab line), checks that the final line numbers run 1 to N,
C-unquotes `filename` and `previous`, decodes author and summary as UTF-8, and reads
`summary (<sha>)` as an empty message. `blame.ignoreRevsFile` is honoured; when it names a missing
file the page says `Blame stopped: the git setting blame.ignoreRevsFile names {file}, which does not
exist.`

### 4.1 File history

`FileHistoryViewModel` / `FileHistoryView` (`FileHistory`). It lists the commits that touched the
file, newest first, following renames, starting from HEAD for a file of the working tree or the
index, and from the commit for a commit's file.

- **Header** 36 (`FileHistoryHeader`): `IconHistory`, `History of {name}` (`FileHistoryTitle`), the
  folder in the secondary colour, `from {short}` when it starts at a commit other than HEAD, then
  on the right `Blame` (`FileHistoryBlame`, a `VcActionButton`: the blame of the selected row's
  version) and the close button (`FileHistoryClose`).
- **The list** (`FileHistoryList`), rows 26 (`FileHistoryRow`): the status letter in the changed-file
  list's colours (`A`, `M`, `D`, `R`, `C`, `T`), the short SHA in the code font, the subject, then
  in the secondary colour `renamed from {old path}` (an `R` row), `copied from {path}` (a `C` row),
  `added` or `deleted` (so a file deleted and added again reads as two lives), or `as {path}` on a
  row whose path differs from the one the history opened for; the author; the date
  (`DateDisplay`). Columns are hidden right to left as the graph's are (D49) when the width runs
  short. Selecting a row shows that commit in the right panel
  (11.3) and its change to the file in the diff part.
- **The diff part**: the file's diff at the selected commit against its first parent (a merge: the
  first parent), with the paths at that commit, through the same `DiffView` the graph area uses,
  hosted by the page (its own `IDiffHost` that reads through the repository and has no write
  action; Inline and Side by side as the setting says). The first row is selected when the list
  loads.
- **Layout**: list and diff side by side when the page is at least 760 wide (list 45 %, diff 55 %),
  otherwise the list above (40 % of the height, at least 3 rows) and the diff below: at 1100×700
  the graph area is 440 wide, at 1920×1080 it is 1260.
- **Rows' menu** (`ForFileHistoryRow`): `Show in graph` (closes the page, selects and reveals the
  commit; disabled `This commit is not in the graph` when no shown ref reaches it), `Blame at this
  commit`, `Copy SHA`. A double-click on a row does `Show in graph`.
- **Loading**: rows come as git prints them; `Loading…` (secondary) until the first one; `No commit
  has touched {name} yet` when git lists none (4.4 disables the item for a file in no commit); a
  failure shows git's message in the list's place. Closing the page cancels the read.

### 4.2 Blame

`BlameViewModel` / `BlameView` (`Blame`). It shows the file with, for each line, the commit that last
changed it: at a commit for a commit's file, in the working tree for a file of the working tree, in
the index for a staged file.

- **Header** 36 (`BlameHeader`): `IconBlame`, `Blame of {name}` (`BlameTitle`), `at {short}
  {subject}`, `in the working tree` or `in the index` in the secondary colour, then `History`
  (`BlameHistory`, the file's history from the same commit) and the close button (`BlameClose`).
- **The text** (`BlameText`): a read-only `CodeTextEditor` (phase 2's base) in the code font, with
  the diff's syntax colours for the file's type (`DiffSyntax`), line numbers, and no caret.
- **The gutter** (`BlameGutter`), left of the line numbers, 200 wide: lines are grouped in runs of
  consecutive lines from one commit; the first line of a run shows the short SHA (code font), the
  author (cut with "…") and the date (`yyyy-MM-dd`), the run's other lines nothing; runs alternate
  between `VcPanelBackgroundBrush` and the window's background so a run's extent is visible; a line
  not committed yet shows `Not committed yet` in `VcWarningBrush`. Hovering a run shows its whole
  commit in a tooltip: the SHA, the author and e-mail, the date and the subject.
- **Click-through** (D158): a click on a run, in the gutter or on its text, selects that commit in
  the graph behind the page and the right panel shows it (11.3); the run's lines get
  `VcSelectionBrush` in the gutter. A double-click does `Show in graph`. The gutter's menu
  (`ForBlameRun`): `Show in graph`, `Blame before this commit` (the blame at the commit's first
  parent, of the path it had there: how one goes further back), `History of {name}`, `Copy SHA`.
  This makes D158's two clicks: right-click a file, `Blame` shows every line's commit.
- **Large**: a file over phase 2's very-large limit (D65: 10,000 lines or 1 MiB) is shown without
  syntax colours.

- **Binary, empty, failed**: a line with a NUL byte makes the page say `{name} is a binary file: it
  has no lines to blame`; no output says `{name} is empty`; git's failure is shown in the text's
  place with git's message.

### 4.3 Commits the graph does not have

A history row or a blame run may name a commit that no shown ref reaches: one on a hidden branch
(section 8), or on a branch deleted or reset while the page was open. Selecting it still shows its
details in the right panel (11.3); `Show in graph` is disabled with `This commit is not in the
graph`. A blame run that is not committed yet selects the working-changes row instead (the stage
panel then shows the changes).

### 4.4 Where they open

`File history` and `Blame` are one group, before `Open file`, in the file menus (`ForUnstagedFile`,
`ForStagedFile`, `ForCommitFile`, `ForDiff`, and the resolver's header menu, which is
`ForUnstagedFile`), as items of D92's kind: left out where they cannot apply, disabled where the
reason helps.

| Place | File history from | Blame of |
|---|---|---|
| Unstaged or staged list, a modified, renamed or type-changed file | HEAD, the path in HEAD (a staged rename: its old path) | the working tree (lines staged or not are "not committed yet") |
| A file deleted in the working tree | HEAD | HEAD's version, titled `at HEAD` |
| An untracked file, a staged new file | disabled: `This file is in no commit yet` | disabled, the same |
| A nested repository | left out | left out |
| A conflicted file | HEAD (empty for `AA`: `No commit has touched {name} yet`) | the working tree when the file exists (merge-aware, 0.1), else HEAD's version, else disabled |
| A commit's file (`FileMenuTarget.CommitSha`), and a commit's diff | that commit, the path at it | that commit; a file the commit deleted: its first parent, titled `before {short}` |
| A stash's file | the stash's base (`^1`); the stash row's details show the stash itself | the stash commit |
| A row of the history page | — | `Blame at this commit`: that commit and the path at it (a `D` row: its parent) |
| An unborn repository | disabled: `Make the first commit first` | disabled, the same |

The two clicks of D158 ("any line of any file traced to its commit"): a right-click on the file and
`Blame`.

## 5. The reflog and backups view

### 5.1 Where it opens

A page of the graph area (section 11), `ReflogViewModel` / `ReflogView` (`Reflog`), opened from:
the empty graph area's menu and the working-changes row's menu, `Show reflog` (HEAD's); a local
branch's menu, `Show reflog of {b}`; and the toolbar's Undo tooltip says, when nothing is left to
undo in the session, `Nothing to undo. Older actions: Show reflog` (the words only; the menu item is
the way in). The page's header has a selector (`ReflogSource`, a `VcActionButton` with the source's
name and `IconChevronDown`, opening a menu through the menu layer) to switch between `HEAD`, each
local branch, and `Backups`.

### 5.2 A reflog

`IGitRepository.ReadReflogAsync(fullRef, limit, token)` reads one reflog through git (never
`.git/logs`, which reftable lacks) with two calls whose rows are zipped by position, because no
placeholder gives an entry's time together with its index (0.2):

1. `git log --walk-reflogs --no-show-signature --no-color --encoding=UTF-8 --ignore-missing -z -n 500
   --format=%gD%x1f%H%x1f%P%x1f%gs%x1f%s <full ref> --`: the selector, the commit, its parents, the
   entry's message and the commit's subject.
2. The same walk with `--date=raw --format=%gD%x1f%H`: the selector becomes `<ref>@{<seconds>
   <zone>}`, the entry's own time.

The result is used only when both list the same SHAs in the same order; otherwise (a ref moved
between the calls) both are read once more. A missing reflog (a deleted branch, an unborn HEAD) is
no rows. Then `git rev-list --ignore-missing --stdin --not --branches --tags --remotes HEAD` with the
rows' distinct SHAs marks which are lost; it runs after the list shows and fills the column in. The
page reads again on every refresh while it is open (some entries change no ref target: `checkout:
moving from main to main`), and never while it is closed.

Rows 26 (`ReflogRow`), newest first, in git's order (the tests' app commits all share 2026-01-02
09:00, so time cannot order them):

| Part | Content |
|---|---|
| Selector | `HEAD@{n}` or `{b}@{n}`, secondary, code font |
| Time | the entry's own time, `yyyy-MM-dd HH:mm` (`DateDisplay`) |
| Action | git's reflog message, e.g. `checkout: moving from main to feature/alpha`, `commit (amend): Fix the parser` |
| Commit | the entry's commit: short SHA and subject |
| Lost | `IconAlert` in `VcWarningBrush` with the tooltip `No branch, tag or remote branch reaches this commit` when that holds |

Selecting a row shows its commit in the right panel (11.3). The rows' menu (`ForReflogEntry`):
`Create branch here…`, `Check out this commit`, `Reset {cur} to this commit…`, `Cherry-pick onto
{cur}`, `Show in graph` (disabled when the graph does not have it), `Copy SHA`; each is phase 3's
operation with its own question and backup, so nothing new restores; their disabled reasons come
from the row (its parents, lost or not, HEAD at it), not from the graph. A double-click does `Show
in graph`. A reflog that git keeps none of says `{name} has no reflog`. When git skipped entries
(their numbers are missing: a commit that no longer exists, or the empty entry a rename of the
checked-out branch writes), a last line says `{N} entries are not shown: they name no commit that
exists`. Each restore action checks inside its operation that the commit still exists (`cat-file -e
<sha>^{commit}`), since a `gc` may have run since the read. The page's empty state explains git's
rules: `Git keeps a reflog entry for 90 days, and for 30 days when no branch holds its commit.`

The source menu lists `HEAD`, the local branches (by name) and `Backups`; remote-tracking reflogs
are left out (they start only at the first fetch and show what fetches did, which the activity log
shows better), and so is `refs/stash` (its entries are the stash list the graph already shows).

### 5.3 Backups

The `Backups` source lists the backups under `refs/visualcommit/backup/` that the clean-up has left
(D139), newest first, one row per backup (one operation's refs, phase 4's grouping), read with a new
`IGitRepository.ReadBackupsAsync()` built on phase 4's parser (`BackupNamePattern`):

| Part | Content |
|---|---|
| Time | from its name, `yyyy-MM-dd HH:mm:ss` |
| What | the backup in words, by kind (12.3): `Before discarding 3 files`, `Before a hard reset of main`, `Deleted branch feature/old`, `Dropped stash "WIP on main"`, `Before a force push of main to origin`, … |
| Commit | the commit it keeps (the first ref's), short SHA and subject |

Its menu restores by kind (`ForBackup`, 7.10 lists each): `Restore branch {b}`, `Restore tag
{t}`, `Restore stash`, `Restore the discarded files…`, `Restore the changes…`, `Push {b} back to
{r}…`, and for every backup that holds a commit `Create branch here…`, `Check out this commit`,
`Copy SHA`. These share the effects of the undo steps that do the same (7.3).

## 6. Compare

### 6.1 Two selected commits (D162)

When exactly two commits are selected (D88's multi-selection, never a stash or the working row), the
right panel shows their comparison (`CompareViewModel`, `CompareView`, `Compare`) in place of phase
3's list of selected commits; with three or more it shows that list as before. Selecting anything
else ends the comparison. The two are ordered by the graph: the lower row is `From`, the upper `To`
(for commits of one line of history: the older first). The swap button exchanges them.

### 6.2 Two branches

The branch menus offer `Compare {b} with {cur}` (local and remote branches other than the current
one, and tags: `Compare {t} with {cur}`) and the drop menu `Compare {A} with {B}`. `From` is the
current branch (or B), `To` the branch the menu is on (or A). It opens the same view in the right
panel and stays until the user closes it (`CompareClose`), selects a single commit or the working
row, or opens another comparison.

### 6.3 What is compared

- **Two commits**: their trees, `From` against `To` (two-dot): the user picked two snapshots. For
  commits on one line of history it is the same as "what happened between them".
- **Two branches** (or a tag): by default the changes on `To` since it left `From` (three-dot: the
  merge base against `To`, as a pull request shows them), with a switch `All differences` for the
  two trees. Two-dot would show `From`'s own later changes reversed (0.3).
- Commands (`IGitRepository.CompareAsync(from, to, threeDot, token)`): the names resolved to SHAs
  first and kept, so that refs moving meanwhile change nothing; for three-dot `git merge-base
  <from> <to>` (exit 1: no common history: three-dot is not offered, `No common history: showing
  all differences`; several bases: the one git prints, and `Several merge bases: changes since
  {short}`); then `diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id <old>
  <new>` with two SHAs, parsed by `ParseNameStatus`; and for branches `rev-list --left-right --count
  <from>...<to>` beside it. Git's `rename detection was skipped` warning adds `Renames are shown as
  removed and added files.`
- The same commit on both sides: `Both are {short}: nothing to compare`, without git.

### 6.4 The view

Header 36 (`CompareTitle` `Compare`, `CompareSwap` with `IconArrowDown` and `IconArrowUp`,
`CompareClose` for a branch comparison), then:

- `From {short} {subject}` and `To {short} {subject}` (`CompareFrom`, `CompareTo`), each a link that
  selects that commit in the graph (as a parent link does); for branches the names: `From main`,
  `To feature/alpha`, the counts `{a} commits only on {To}, {b} only on {From}`, and the switch
  (`CompareAllDifferences`, a check box `All differences`);
- `Changed files ({N})` with Flat and Tree (the setting of D44), and the file list
  (`CompareFileList`, phase 1's `ChangedFileList` and rows);
- a click on a file opens its diff between the two in the graph area (phase 2's diff view,
  `DiffTarget(Commit, path, kind, oldPath, Commit: To, Parent: From)`), with the side label
  `{from short}..{to short}`; the row keeps its selection while the diff is open; Esc closes the diff;
- the files' menu: `Open file`, `Show in folder`, `Copy path`, and `File history`, `Blame` (at To).

`No differences` when the two trees are the same.

## 7. Undo and redo (D154, D155, D163)

### 7.1 The history

Each repository has one `UndoHistory` for the app's session (`VisualCommit.Core.Undo`, pure, tested
without git): a list of entries and a cursor between them. Undo applies the entry before the cursor
backwards and moves the cursor back; Redo applies the entry after it forwards and moves it on; a new
recorded action drops every entry after the cursor, then is added. `AppSession` keeps the histories
in a dictionary keyed by the repository's common git folder (its full path), so closing a tab and
opening the repository again in the same session keeps its history; nothing is written to disk or
into the repository. At most 100 entries are kept per repository; the oldest goes first.

An **entry** (`UndoEntry`): the action's text (7.6), the operation id it was recorded in, its
effects in the order they happened (7.3), the backup refs its undo needs (7.8), and its state:
`Done`, or `Pending` while the action it records is stopped (a merge, a pick, a revert or a rebase
with the banner showing).

### 7.2 What is recorded

| Recorded (an entry each) | Not recorded |
|---|---|
| Commit and amend (the stage panel's Commit), checkout of every kind (a local branch, a remote branch into a tracking branch, a commit or tag, the automatic stash's checkout of 9.2), create, rename and delete a branch, fast-forward a branch, merge (both kinds, the drop's, "Check out and merge"), reset (soft, mixed, hard), cherry-pick and revert (one or a list), rebase onto, "Check out and rebase", interactive rebase, squash, pull (fast-forward, merge, rebase), stash, apply, pop, drop, create and delete a tag, discard (a file, a hunk, lines, all), the restore bar's Restore, ignore a file or an extension, set and stop tracking an upstream, add, rename and remove a remote, change a remote's URL, push a branch, force push, delete a branch on a remote, push a tag, delete a tag on a remote | Stage, unstage and applying lines to the index (the preparation of a commit, undone with one click in the stage panel; recording each would bury the actions people undo; GitKraken leaves them out too); fetch and auto-fetch (they change only remote-tracking refs, which no entry relies on except through a lease); the backups' clean-up; the conflict resolver's resolutions and the merge tool (they happen inside a stopped operation, during which undo is off, 7.4); open, show, copy; undo and redo themselves |

- An action that **changed nothing** (refused, up to date, `Everything up-to-date`, nothing to stash)
  records nothing.
- An action that **stopped** (conflicts, an `edit` stop) records a `Pending` entry with its
  before-state. Continue, Skip and the stage panel's Commit that end the operation complete it with
  the after-state (one entry from before the action to the final result: a rebase with three stops
  is one Undo); Abort removes it (the abort's own backups stay in the backups list, 5.3); an
  operation that ends outside the app (a terminal) removes it at the next refresh.
- **Failed half-way** (a multi-step operation whose second step failed: "Check out and merge" whose
  merge failed): what changed is recorded (the checkout), as the effects are read, not assumed
  (7.7).

### 7.3 Effects

An entry is a list of **effects**, each with an exact way forward (redo) and back (undo) and its own
check (`UndoEffect`, Core, a closed set of records; `GitRepository.Undo.cs` runs them). Undo runs the
entry's effects backwards in reverse order, redo forwards in order. Every recorded action is made of
these:

| Effect | Forward (redo) | Back (undo) | Check before (undo; redo is the mirror) |
|---|---|---|---|
| `MoveBranch(branch, before, after, Mode)`, Mode `Soft` (commit, amend, soft reset) or `Keep` (everything else that moves the checked-out branch) | Soft: `update-ref -m <msg> refs/heads/<b> <after> <before>`; Keep: `read-tree -m -u <before> <after>`, then that update-ref; when the update-ref fails, `read-tree -m -u <after> <before>` puts the files back and the step is refused | the same, `<after>` → `<before>`; the initial commit: `update-ref -d refs/heads/<b> <after>` | HEAD is on `<b>` (`symbolic-ref -q HEAD`) and `<b>` is at `<after>`; Keep: git's two-way merge refuses a move over local changes (0.4) |
| `MoveDetachedHead(before, after, Mode)` | as above on `--no-deref HEAD` | | HEAD detached at `<after>` |
| `MoveRef(ref, before, after)` (either may be absent: created, deleted) | `update-ref --stdin` with `update`, `create` or `delete` and the expected value | the reverse | the ref is at the expected value; a branch is checked out in no worktree (`%(worktreepath)`) |
| `SwitchHead(before, after)` (a branch's full name or a SHA) | `switch --no-guess <b>` / `switch --detach <sha>` | the same to `<before>` | HEAD is exactly `<after>`; `<before>` exists; a refusal over local changes shows 9.3's message |
| `RenameBranch(old, new)` | `branch --move <old> <new>` | `branch --move <new> <old>` | `<new>` exists, `<old>` does not (a name differing only in case on Windows and macOS: refused, as D131) |
| `BranchConfig(b, before, after)` (every `branch.<b>.*` key) | the keys of `after` written with `config --local`, the others unset | `before` likewise | the keys equal the side being left |
| `RemoteConfig(r, before, after)` (`remote.<r>.*`, `remote.pushDefault`, the branches' `remote`/`merge`/`pushRemote` keys naming it) | replayed with `config --local --add` in order, or removed | likewise | the remote's keys equal the side being left |
| `RemoteRename(old, new)`, `RemoteUrl(r, before, after)` | `remote rename`, `remote set-url --` | back | the side being left exists and the other name does not; one `url` value |
| `IndexFromSnapshot(snapshot, after)` (a mixed or hard reset) | `reset -q --mixed <after>` | `restore --staged --source=<snapshot> -- .` (a hard reset's: the snapshot's second parent) | the index is unchanged since (`diff-index --cached --quiet <after>`) |
| `FilesFromSnapshot(snapshot, paths, hashes)` (a hard reset's, a discard's) | the discard again (`restore --worktree`, deletion), or the hard reset with a new snapshot | `RestoreSnapshotAsync` | none of the paths changed since: SHA-256 of their raw bytes equal the recorded ones (as the restore bar checks, D75) |
| `StashPushed(sha, options, statusAfter)` | `stash push [--include-untracked] [--message <m>]` without `GIT_LITERAL_PATHSPECS` | `stash pop --index stash@{n}` (found by SHA) | the SHA is listed; HEAD unchanged; the whole status equals the recorded one (a stash's pop mixes with anything changed since, 0.5); no untracked file of the stash exists |
| `StashEntry(sha, message, present before, present after)` (a pop's or a drop's list change) | `stash drop stash@{k}` by SHA | `stash store -m "<message>" <sha>` (it comes back as `stash@{0}`) | listed or not as the side being left says |
| `WorkingFiles(paths, before, after)` (apply and pop: each path's index entries from `ls-files --stage -z` and raw bytes from `hash-object -w --no-filters`) | the after entries through `update-index -z --index-info` and the after bytes written | the before ones | each path's index entries and bytes equal the side being left; no operation in progress; no path intent-to-add or skip-worktree (refused: git cannot restore those flags) |
| `TagRef(name, object, before present)` | `update-ref refs/tags/<t> <object> ""` | `update-ref -d refs/tags/<t> <object>` (an undone create first saves `tag-<time>/<t>`, so the object stays for redo) | the tag is at the object, or absent |
| `GitignoreBytes(existed, lengthBefore, appended, hashAfter)` | append the bytes | truncate to the old length, or delete a file it created | the file's hash equals the side being left; not a symbolic link |
| `RemoteRef(remote, ref, before, after)` (a push, force push, remote deletion, tag push, remote tag deletion; the local remote-tracking ref follows) | `push --progress --force-with-lease=<ref>:<before or empty> <r> <after>:<ref>` or `--delete <ref>` | the reverse, with the lease the value the action left | the lease; `Everything up-to-date` on a push that must create counts as done. **Asks first** (7.5) |

Messages for `update-ref -m`: `VisualCommit: undo <text>` and `VisualCommit: redo <text>`, so the
reflog view tells them apart (0.2). `switch` keeps git's own message (`@{-1}` keeps working).

### 7.4 When Undo and Redo are enabled

Undo is enabled when the entry before the cursor is `Done` and its checks pass against what the app
last read, Redo likewise for the entry after it. After every refresh the repository evaluates those
two entries in memory against `Refs` (HEAD, branch tips, tags, stashes), the status (staged
changes, the paths of a snapshot), the remotes, and whether an operation runs or waits; the git
reads that only the operation can do (file hashes, index entries, worktree paths, leases) are left
to the operation. The first reason that holds, in this order:

1. An operation of the repository runs or waits: `Wait for {title} to finish`.
2. An operation is in progress (the banner shows): `Finish or abort the {kind} first` (D119's
   texts; conflicts without one: `Resolve the conflicts first`).
3. No entry: `Nothing to undo` / `Nothing to redo`.
4. The entry's check fails (7.6's texts), e.g. `main has moved since the commit (it is at 9f8e7d6)`.

The checks are strict in order: an entry that cannot be undone blocks the older ones (undoing them
around it would apply effects to a state they were not recorded on). The backups and reflog view
(section 5) is the way past it. Outside changes never remove entries: an unrelated change (a
fetch, a commit on another branch) leaves the entry undoable; a conflicting one makes it refuse
until the state is back.

### 7.5 Running a step

Undo and Redo each run as one operation of the repository's queue (titles `Undo {text}` and `Redo
{text}`, refresh `Refs`, prompts `Ask`), **Local**, or **Network** when an effect pushes (it can then
be cancelled and prompts for credentials). Inside it, in this order (D124): refuse while an operation
is in progress (the app's own check: `update-ref` refuses nothing, 0.4); read HEAD and the entry's
refs (`rev-parse HEAD --symbolic-full-name HEAD`, one `for-each-ref` with `%(worktreepath)`), the
status, and what each effect checks; refuse with the first failed check, having written nothing;
then apply the effects. Several ref writes of one step go into one `update-ref --stdin` with
`verify` lines, all or none; the steps that can refuse (switch, read-tree) come before the
compare-and-swaps.

A step with a `RemoteRef` effect asks first, as D155 says, for every remote change in either
direction (the owner named force pushes and deletions; an ordinary push's undo rewinds or deletes a
remote branch in the same way): `Undo the push to {remote}?` / `{remote}'s {branch} goes back to
{short}, which it was before your push. Anyone who fetched {short2} keeps it.`, or for a deletion
`{remote} gets {branch} back at {short}.`; buttons `Undo` (or `Redo`) and `Cancel`; not destructive
in style (it restores). A lease failure shows the push's error dialog with git's output and the
summary `{branch} on {remote} has changed since: nothing was changed.`

Undoing a commit or an amend leaves its changes staged (a soft move), so the stage panel's summary
and description are filled with the undone commit's message when both are empty, as GitKraken does;
redoing it clears them again if they still hold that message (phase 4's rule for filled messages).

A refusal found inside the operation opens the error dialog titled `Undo failed` or `Redo failed`
with the reason as its summary and no git output (D95's failures); a git failure shows git's output
as every operation does. The step's result in the status bar: `Undid {text}` / `Redid {text}`.

### 7.6 Texts

An entry's text is the operation's title (D113), except where the title does not say enough:

| Action | Entry text |
|---|---|
| Commit | `commit "{subject}"` (the title is "Commit") |
| Amend | `amend "{subject}"` |
| A discard from the stage panel | `discard of {N} files` / `discard of {name}`; lines: `discard of lines in {name}` |
| Restore | `restore of the discarded files` |
| Reset | `reset of {b} to {short} ({soft|mixed|hard})` |
| Everything else | the operation's title with its first letter in lower case: `check out feature/alpha`, `merge feature into main`, `rebase topic onto main`, `cherry-pick of 3 commits`, `force push of main`, … |

The toolbar shows `Undo {text}` and `Redo {text}`. Check failures, each prefixed by `Can't undo
{text}: ` or `Can't redo {text}: ` in the error dialog and shown without the prefix in the tooltip:

| Check | Text |
|---|---|
| HEAD not on the branch | `HEAD is no longer on {b}` |
| The branch moved | `{b} has moved since (it is at {short})` |
| A ref moved or exists again | `{ref} has changed since` / `a branch named {b} exists again` / `a tag named {t} exists again` |
| Checked out elsewhere | `{b} is checked out in {folder}` |
| Local changes in the way (Keep move, switch) | 9.3's message: `{N} conflicts prevent it. Commit, stash, or discard your changes first.` |
| Index changed | `you have staged changes since` |
| Files changed | `{N} files have changed since: {first names}` |
| Stash | `the stash is no longer in the list` / `the stash is in the list again` / `your changes have changed since the stash` |
| Commits gone | `the commits no longer exist` (`cat-file -e` failed: an outside `gc`) |
| A remote | `{b} on {r} has changed since` (from the lease) |
| Intent-to-add, skip-worktree | `{name} has a flag git cannot restore` |

### 7.7 Recording, inside the action's own operation

A `Run` delegate must not queue another operation (the queue would deadlock), and recording needs
none: `RepositoryOperations.RunAsync`'s wrapper (where `_backupSaved` is set) and phase 2's
`RepositoryViewModel.WriteAsync` (the stage panel's Commit, discard, Restore) do it around `run`:

1. Each operation names what it may change through `RunOptions.Undo` (an `UndoScope`: HEAD always;
   the branches, tags, remotes, stash, files or config keys it touches; `None` for the unrecorded
   kinds of 7.2).
2. Before `run`, inside the operation: HEAD (`rev-parse HEAD --symbolic-full-name HEAD`, or
   `symbolic-ref -q HEAD` when unborn) and one `for-each-ref --format=%(refname)%1f%(objectname)%1f%(symref)`
   of the scope's refs (exact names filtered in code: a pattern matches by path component), the
   stash list when the scope has the stash, and the config keys when it has config.
3. What the reads cannot see goes through an `UndoCapture` (`AsyncLocal`, thread-safe, because
   `SaveRebaseBackupAsync` runs on a pool thread): a reset's mode and snapshot refs, the stash SHA
   `StashAsync` returns, a push's old remote value from git's output line, the files' hashes after a
   discard, the bytes appended to `.gitignore`.
4. After `run`, still inside the operation: the same reads again. The differences and the capture
   become the entry's effects; no difference, no entry. The reads write nothing
   (`GIT_OPTIONAL_LOCKS=0`), so a refused action still leaves HEAD's reflog as it was (phase 4's
   check A5 counts it).
5. Still inside the operation, on the UI context, the entry is added (`Done`, or `Pending` when an
   operation is now in progress) and the redo part is dropped. It is not carried through
   `OperationReport` (the queue drops fields it does not know).

The reads cost two git calls before and two after an operation: about 0.1 to 0.3 s on the
development laptop. Operations that already read HEAD for D124 share that read.

### 7.8 What the history keeps alive

- Commits left behind by an undo stay in the branch and HEAD reflogs (30 days, 0.4); a redo checks
  `cat-file -e` first.
- Backup refs an entry needs (a reset's `reset-<time>` and `-changes`, `detached-<time>`,
  `rebase-<time>/<b>`, a drop's `stash-<time>`, a deleted tag's or an undone tag creation's
  `tag-<time>/<t>`, a deleted branch's `branch-<time>/<b>`, a removed remote's `remote-<time>/<r>/…`)
  are passed to the backups' clean-up as `keep` through `RepositoryViewModel.BackupsToKeep` (today the
  restore bar's snapshot only), for every entry of the repository's history. They still count toward
  D139's 50: a long session keeps more than 50, on purpose.
- An entry dropped from the history (the 100 limit, or a new action after undos) no longer keeps
  them.

### 7.9 The restore bar

The restore bar (D62, D75) stays. Its Restore is recorded as the undo of that discard when the
discard is the entry before the cursor (the cursor moves; Redo discards again); otherwise it runs as
today and is recorded as an entry of its own (`restore of the discarded files`), which Undo can
discard again.

### 7.10 Restoring from the backups list

The backups list (5.3) restores a backup outside the session's history with the same effects:

| Backup | Restore item | Effects |
|---|---|---|
| `branch-<t>/<b>` | `Restore branch {b}` | `MoveRef(refs/heads/<b>, absent, <tip>)` (the config is not in the backup: no upstream) |
| `tag-<t>/<t>` | `Restore tag {t}` | `TagRef` create |
| `stash-<t>` | `Restore stash` | `StashEntry` store, with the stash commit's subject as message |
| `discard-<t>` | `Restore the discarded files…` | asks which files will be overwritten when any exist (`Restore discarded files?`, D75's question), then `FilesFromSnapshot` |
| `reset-<t>-changes`, `abort-<t>-changes`, `skip-<t>-changes` | `Restore the changes…` | as a discard snapshot: the snapshot's files written into the working tree, after the same question |
| `remote-branch-<t>/<r>/<b>` | `Push {b} back to {r}…` | `RemoteRef` create, with D155's question; the remote and branch split by the remote list's longest matching name, as the deletion did |
| `remote-tag-<t>/<r>/<t>` | `Push {t} back to {r}…` | likewise for the tag |
| `force-push-<t>/<name>` | `Push {b} back to {r}…` | the remote and its ref from the upstream that maps to `refs/remotes/<name>`, or from the fetch refspecs; when none or several map: disabled, `The remote this backup came from cannot be found` |
| every backup that holds a commit | `Create branch here…`, `Check out this commit`, `Copy SHA` | phase 3's operations |

Each is one operation titled as its item (without the "…"), recorded in the history like any action.

### 7.11 Tests

`UndoHistoryTests` (Core: cursor, truncation, the 100 limit, pending entries completed and removed);
`UndoEffectTests` (Git: every effect forward and back on real repositories, each check's refusal
changing nothing, the race of a Keep move, the initial commit, the lease texts on a local bare
remote); `UndoRecordingTests` (App: the entry each recorded action of 7.2 leaves, with its effects
and text; no entry for a refused or empty action; the pending flow through Continue, Skip and
Abort); `UndoToolbarTests` (enabled states and tooltips of 7.4 after refreshes and outside changes,
the keys of 1.3 and their declines); and section 15's scripted checks, which undo and redo each
destructive action of phases 2 to 4.

## 8. Solo and hide (O8, D159)

### 8.1 What can be hidden or soloed

Local and remote branches (O8 says branches; tags stay as they are). The state per repository is two
sets of **full ref names**, `Hidden` and `Soloed` (short names collide across kinds: a tag and a
branch can both be `x`, a local branch can be named `origin/x`), plus, for a hidden remote branch,
its remote's name. A ref is never in both sets: soloing a hidden ref shows it, hiding a soloed one
ends its solo.

- **Hiding a local branch hides its upstream too** (both names go into `Hidden`, each shown and
  undone separately in the left panel): otherwise the upstream keeps most of the branch's commits on
  screen (0.6). Hiding a remote branch hides only it. Soloing a local branch solos its upstream too,
  so the commits a pull would bring stay visible.
- **The checked-out branch cannot be hidden** (`Hide` disabled: `The checked-out branch is always
  shown`). A hidden branch that becomes the checked-out one (a checkout in the app or a terminal) is
  shown while it is, and keeps its flag; it hides again when HEAD leaves it.
- **Solo mode**: while `Soloed` is not empty, the graph shows the soloed refs' histories and HEAD's;
  several refs can be soloed. `Hidden` is kept and applies again when solo mode ends. A branch the
  app creates while solo mode is on is soloed too, so it does not vanish.

### 8.2 What the graph loads (`LoadCommitsAsync` takes a `GraphFilter`)

With no filter the command is byte-identical to today's, so every picture, the Q1 measurement and
the real-window pass's `loaded N commits` waits are unchanged. With one (0.6):

- **Hide**: `--exclude=<b>` … `--branches`, `--exclude=<r>/<b>` … and `--exclude=<r>/HEAD` for every
  remote with a hidden branch, then `--remotes`, `--tags`, `--ignore-missing HEAD --`. Never `^ref`
  (it removes shared history), never `*/HEAD`.
- **Solo**: `--ignore-missing <full name> … HEAD --`.
- **Over 30,000 characters** of arguments (about 800 hidden refs): `--ignore-missing --stdin --` with
  plain full names on stdin (2.30 to 2.41 accept no `--exclude` there): for solo the soloed names and
  `HEAD`; for hide the full names of every visible ref from the refs read, and `HEAD`, so this case
  waits for the refs (about 0.2 s).
- The filter comes from the session, so git starts before the refs are read, as today (Q1).
- **Stashes**: with a filter, the pager keeps the set of SHAs it has seen as visible tips or parents;
  a stash whose base is in it is placed by D54's rule as today, otherwise it waits and is placed
  right above its base when the base comes, and is left out if the base never comes. Without a
  filter the pager is unchanged. (Amends D54 while a filter is on.)
- **Labels**: `CommitGraphData` takes the filter; `BuildLabels` leaves out hidden refs' labels, and in
  solo mode every branch label but the soloed refs' and HEAD's branch's; tags keep their labels on
  the rows shown. `Refs` itself stays whole (the details panel and the HEAD walks use it).
- **A change of filter** reloads through the existing refresh: the filter's key joins the
  fingerprint's comparison (`RunRefreshesAsync`), and `RefreshAsync()` loads the new graph off screen
  and swaps it when complete, as an outside change does. The left panel shows the new state at once.

### 8.3 The left panel

- Every ref stays listed. A hidden branch row draws its name in `VcTextDisabledBrush` and an
  `IconEyeOff` button (`RefHiddenToggle`, tooltip `Hidden in the graph. Click to show it`) at its
  right, before the ahead and behind counts; a click shows it again. A soloed row draws an
  `IconSolo` button (`RefSoloToggle`, `VcAccentBrush`, tooltip `Soloed. Click to stop soloing it`).
  In solo mode the branch rows that are neither soloed nor HEAD's are drawn in `VcTextDisabledBrush`.
- The toggles are `VcIconButton`s 18 by 18 that stop the press from starting a branch drag and a
  double-click from checking out (the code map's pitfalls: `RefDrag.Source` on the row, the
  double-click's `RowOf`), and are left out of the row's automation name.
- A click on a hidden branch's row whose commit is a row of the graph (another ref reaches it)
  selects it as today; otherwise nothing is selected and the row's tooltip says `{b} is hidden in
  the graph`.
- Menus: a local or remote branch's menu gains, in a group before `Copy branch name`: `Hide {b}`
  (or `Show {b}`), `Solo {b}` (or `Stop soloing {b}`); the Local branches and Remotes sections'
  menus gain `Show all hidden branches` (enabled when any is hidden) and `Stop soloing` (enabled in
  solo mode). A remote's menu gains `Hide every branch of {r}` / `Show every branch of {r}`, which
  adds or removes each of its branches by name.

### 8.4 The session (D159)

`SessionState.GraphFilters`: a list of `RepositoryGraphFilter(RepositoryPath, Hidden, Soloed,
HiddenRemotes)` with full names, keyed by the same normalised working-tree path as `TabState` and
`Recent`, separate from the tabs so that closing a tab keeps the filter. It is written when a filter
changes. The app's own rename of a branch moves its entry, its delete removes it; after a
repository's first refs read, names that no longer exist are dropped, and solo mode ends when no
soloed name exists. Entries are kept only for repositories in open tabs or in the recent list. An
outside rename shows the branch again under its new name.

### 8.5 Tests

`GraphFilterTests` (Git: the arguments for each filter, the shown SHAs equal to git's own for hide,
solo, upstream, `<r>/HEAD`, a tag and branch of one name; the stdin fallback; the stashes' rule),
`CommitGraphDataTests` (labels under a filter), `LeftPanelViewModelTests` (marks, toggles, menus),
`SessionStoreTests` (the new field with an older file), and section 15's checks.

## 9. C12: protected changes and the automatic stash (D153)

### 9.1 Which checkouts do what

| Checkout | When | What happens |
|---|---|---|
| Of a commit or a tag (it detaches: "Check out this commit", a tag's "Check out {t}", the reflog's and backups' "Check out this commit") | HEAD is on a branch, the target is not HEAD's own commit, the current branch reaches the target (any parent: `IsReachableFromHead` for a loaded row, else `merge-base --is-ancestor <sha> HEAD` inside the operation), and the status has changes (staged, unstaged, untracked or conflicted; ignored files are not changes) | **The automatic stash** (9.2), without a question |
| Every other checkout (a branch, a remote branch, a commit the branch does not reach, a detached HEAD's checkout, "Create branch" with its box ticked, "Check out and merge", "Check out and rebase", the list's checkout, Abort's return) | always | Git runs it; when git refuses because changes would be overwritten, 9.3's message |

The target being HEAD's own commit changes no file and is never refused, so no stash is taken there;
a detached HEAD has no current branch, so nothing is stashed from one.

### 9.2 The automatic stash

One local operation of several steps, titled `Check out {name}` as phase 3's, `HasSeveralSteps`:

1. A fresh status. Refuse before any git write: an operation in progress → `Finish or abort the
   {kind} first` (a stash during a merge without conflicts would delete `MERGE_HEAD`, 0.7);
   conflicted files → `Resolve the conflicts first`. No changes any more → a plain checkout.
2. Note `refs/stash` (`rev-parse --verify --quiet refs/stash`).
3. `git stash push --include-untracked --message "Auto stash before checking out {name}"` without
   `GIT_LITERAL_PATHSPECS` (D108). `{name}` is the short SHA or the tag's name. "No local changes to
   save" → a plain checkout. A failure with `refs/stash` moved (Windows could not delete a locked
   file): stop, check nothing out, keep the stash, and say so: `Your changes are also in the stash
   "Auto stash before checking out {name}", but git could not clear them from the working tree:
   nothing was checked out.` (error dialog "Checkout failed", git's output). A failure with
   `refs/stash` unchanged: the failure, nothing changed.
4. `git switch --detach --no-overwrite-ignore <sha>`: `--no-overwrite-ignore` on this checkout only,
   because an old commit that tracks a file now ignored would otherwise overwrite it and the way
   back would delete it (0.7). Other checkouts keep git's default (Q5).
5. A refusal: `git stash pop --index stash@{n}` (found by SHA; D91's plain-pop fallback), which
   cannot overlap right after the push; then 9.3's message without the button; when the pop-back
   fails, phase 3's `CheckoutFailedChangesKept` naming the stash.
6. Done: the status bar says `HEAD is now detached at {short}. Your changes are in the stash "Auto
   stash before checking out {name}".` The stash is drawn as every stash: a ring on the commit HEAD
   was on, with a dashed line to it (section 10). Nothing pops it later (D153 (3)); checking the
   branch out again and popping it gives the changes back without a conflict.

The undo entry (7.3) is `StashPushed` then `SwitchHead`: Undo switches back and pops the stash, which
is what the user had before (the undo asks nothing: D153 (3)'s "nothing applies it by itself" is
about the app acting unasked).

### 9.3 The refusal message (D153 (1))

Every checkout path classifies git's refusal the same way (`GitFailureClassifier`, stderr and stdout
together), with the count of distinct paths under git's headers (0.7):

| Git said | Error dialog (title `Checkout blocked`) | Button |
|---|---|---|
| only `Your local changes to the following files would be overwritten by checkout:` blocks | `{N} conflict prevents checkout. Commit, stash, or discard your changes first.` / `{N} conflicts prevent checkout. …` | `Stash changes and check out` (D80, D91), where phase 3 offers it (`offerStash`); never after an automatic stash's refusal |
| any untracked block (`untracked working tree files would be overwritten by checkout:`, `would be removed by checkout:`, `Updating the following directories would lose untracked files in them:`) | the same sentence | none (D91) |
| `you need to resolve your current index first` | refused before git: `Resolve the conflicts first` | none |
| `cannot switch branch while …` | refused before git: `Finish or abort the {kind} first` | none |

Git's output stays below the summary, so the names are one glance away. This replaces phase 3's
`CheckoutBlockedByChanges` and `CheckoutBlockedByUntrackedFiles` summaries, gives "Create branch"
with checkout the same display (today "Creating the branch failed"), and brings the folder block
(today "Checkout failed") under C12. The classifier gains `CheckoutBlockedByUntrackedFolder` and a
`BlockedPaths` count on `GitFailure`.

The same sentence covers the operations that refuse for the same reason (D153's "an operation that
could lose uncommitted changes does not start and says why"):

| Operation | Git said | Summary |
|---|---|---|
| Merge, the drop's merge, pull's merge and fast-forward, "Check out and merge"'s merge, cherry-pick, revert (one or a list) | the `by merge:` blocks | `{N} conflicts prevent the {merge|cherry-pick|revert}. Commit, stash, or discard your changes first.` |
| Merge, cherry-pick, revert with anything staged | ort's one-line form, or `your local changes would be overwritten by cherry-pick.` / `by revert.` (no names) | `Your staged changes prevent the {merge|cherry-pick|revert}. Commit, stash, or unstage them first.` |
| Rebase of any kind | `cannot rebase: …` | phase 4's `Git rebases only a clean working tree: commit or stash your changes first.` (any change refuses; there is nothing to count) |
| Apply or pop a stash (menus, the toolbar's Pop, a double-click) | checked by the app before git: the stash's changed paths (`diff --name-only <s>^1 <s>`) that have a local change, plus its untracked paths (`ls-tree -r --name-only <s>^3`) that exist | `{N} conflicts prevent applying the stash. Commit, stash, or discard your changes first.`; git is not run. Stricter than git on purpose: git's refusal restores the untracked files anyway and a staged overlap becomes a conflict (0.5). D91's own pop after "Stash changes and check out" keeps going to git |

### 9.4 Tests

Git layer: a refused checkout of each of the four forms changes nothing (index bytes, HEAD's
reflog, refs, files, status); the count over recorded texts (several blocks, the folder block's
empty line, 43 names, names with a space and `é`); the automatic stash's round trip (the stash's
`%gs` and first parent, ignored files untouched, the status identical after the way back); its
refusals (in progress, conflicts, nothing to stash, a staged submodule change only); ignored files
with `--no-overwrite-ignore`; `merge-base --is-ancestor` cases. App layer: the dialog for each
refusal kind and every checkout path; the automatic stash's git calls, status text, stash row and
its rules not taken; the half-made stash; the apply pre-check; nothing pops the automatic stash.
Section 15's checks.

## 10. Stashes drawn dashed (D161)

A stash row keeps its ring (D54's place, its lane and colour), and the line from it down to the
commit it was made on is dashed: dashes and gaps of 3 along the line, as the working-changes row's
line to HEAD (D60). `GraphLayout` marks the lane a stash starts as a stash lane until the base's row:
the stash row's `OutOfNode` line, the `PassThrough` lines below it, and the `IntoNode` line at the
base row carry `GraphLine.IsDashed` (a new init property, false by default); from the base on, the
lane is the base's own and solid. `CommitGraphControl` draws a dashed line with a dash style whose
phase is taken from the line's absolute y (as `DrawWorkingRowLine` does), so a line cut by the
viewport or drawn row by row keeps one pattern, curves included. Nothing else changes: lanes,
colours, nodes and the stash's place are as D54 and D55 make them, so phase 1's
`GraphLayoutTests` keep their lanes and only their `IsDashed` facts are added. Every picture with a
stash changes (14.2).

## 11. What the graph area holds now

### 11.1 Pages

Phase 4 left four things in the graph area: the graph, the interactive list, the diff (over the
graph or the list) and the resolver (over everything, across two columns). Phase 5 adds three
**pages**: the file history, the blame and the reflog (sections 4 and 5). They hold no work, so
leaving one never asks.

`RepositoryViewModel.Page` (`IGraphAreaPage?`, implemented by `FileHistoryViewModel`,
`BlameViewModel` and `ReflogViewModel`: `Title`, `Close()`, `RequestFocus()` with `FocusRequested`,
`Dispose()`). At most one page is open, and a page and the interactive list exclude each other:

| Shows | When |
|---|---|
| `ShowsGraph` | no diff, no list, no resolver, no page |
| `ShowsInteractiveRebase` | the list, no diff, no resolver |
| `ShowsPage` | a page, no diff, no resolver |
| `ShowsDiff` | a diff, no resolver (over the graph, the list or a page) |
| `IsResolverOpen` | the resolver |

- Opening a page: `TryLeaveGraphAreaAsync()` (the list asks if changed), then `LeaveGraphArea()`
  (which now also closes the page), then `Page = page`. Opening a page from a page (Blame from the
  history) replaces it. `OpenResolverAsync`, which closes the diff and the list itself, closes the
  page too (the code map's pitfall: it does not call `LeaveGraphArea`).
- A diff from the right panel (a commit's file) opens over a page as over the list; closing it
  returns to the page (`CloseDiff(returnFocus)` asks the page for the focus). A diff from the stage
  panel leaves the page first, as it leaves the list today.
- Esc: a page registers `EscapeOrder.Page` = 370 and declines while a diff is open over it or the
  focus is in a text box; it closes the page and gives the focus to the graph
  (`GraphFocusRequested(Graph)`).
- The search bar belongs to the graph view: Search closes a page and the diff first (they hold no
  work).
- `RepoTabViewModel` forwards nothing new: pages stay in column 1, so the right panel is unchanged.
- The activity log lies under all of these (2.1).

### 11.2 When Search can show the graph

Search is enabled while a repository is open and neither the list nor the resolver is open (they
hold work, and leaving them asks); it closes a diff or a page first.

### 11.3 Showing a commit from a page or a comparison

`RepositoryViewModel.ShowCommit(sha)`: when the graph has the commit, it selects it behind the page
as `IInteractiveRebaseHost.ShowDetails` does (without closing a diff); otherwise it clears the
graph's selection and shows the commit's details directly (`Details.ShowAsync(sha)`), and records
the SHA as the shown one, so that selecting that row later (or another) reloads the panel (the code
map's `_selectedSha` pitfall). `ShowInGraph(sha)` closes the page, selects the row and reveals it
(`Select(sha, reveal: true)`); for a commit the graph does not have it does nothing and its menu
item is disabled. The right panel's file menu for a commit shown this way takes the shown commit's
SHA, not `Selection.Single` (the pitfall of `FileMenuTarget.CommitSha`).

## 12. Texts

Texts given in the sections above are not repeated here. Counts use the singular for one (D113);
`{N} files` means `1 file` for one.

### 12.1 Operations: title, running text, error dialog title, failure summary

| Operation | Title | Running text | Error title | Summary |
|---|---|---|---|---|
| Undo | `Undo {text}` | `Undoing {text}…` | `Undo failed` | `Git could not undo {text}.`, or the check's reason (7.6) |
| Redo | `Redo {text}` | `Redoing {text}…` | `Redo failed` | `Git could not redo {text}.`, or the reason |
| Restore branch | `Restore branch {b}` | `Restoring {b}…` | `Restoring the branch failed` | `Git could not restore {b}.` |
| Restore tag | `Restore tag {t}` | `Restoring {t}…` | `Restoring the tag failed` | `Git could not restore {t}.` |
| Restore stash | `Restore stash` | `Restoring the stash…` | `Restoring the stash failed` | `Git could not restore the stash.` |
| Restore files | `Restore files` | `Restoring files…` | `Restoring the files failed` | `Git could not restore the files.` |
| Push back | `Push {b} back to {r}` | `Pushing {b} back to {r}…` | `Push failed` | `Git could not push {b} back to {r}.` |
| The automatic stash's checkout | `Check out {name}` (phase 3's) | `Stashing your changes…`, then `Checking out {name}…` | `Checkout failed` | phase 3's, or 9.2's texts |

### 12.2 Results in the status bar

| After | Text |
|---|---|
| Undo, Redo | `Undid {text}`, `Redid {text}` |
| A restore | `Restored branch {b}`, `Restored tag {t}`, `Restored the stash as stash@{0}`, `Restored {N} files`, `Pushed {b} back to {r}` |
| The automatic stash | 9.2's text |
| A stash restored by undo below the top | `Restored the stash as stash@{0}` |

### 12.3 Backups in words (the backups list's "What")

| Kind | Text |
|---|---|
| `discard` | `Before discarding {N} files` (the paths in the snapshot's message) |
| `reset` with `-changes` | `Before a hard reset to {short}` or `Before a mixed reset` (from the snapshot's subject); without: `Before a reset` |
| `detached` | `Commits a detached HEAD left behind` |
| `branch` | `Deleted branch {b}` |
| `remote-branch` | `Deleted {b} on {r}` |
| `remote` | `Removed remote {r}` |
| `force-push` | `Before a force push of {name}` |
| `tag` | `Deleted tag {t}` (also the backup an undone tag creation saves: `Tag {t} before undo`) |
| `remote-tag` | `Deleted tag {t} on {r}` |
| `stash` | `Dropped stash "{subject}"` |
| `abort` | `Before aborting` |
| `rebase` | `Before rebasing {b}` |
| `skip` | `Before skipping a commit` |
| any other kind | the kind as written |

### 12.4 New disabled reasons

| Item | Reason |
|---|---|
| `File history`, `Blame` | `This file is in no commit yet`; `Make the first commit first` |
| `Show in graph` | `This commit is not in the graph` |
| `Hide {b}` | `The checked-out branch is always shown` |
| `Show all hidden branches` | `No branch is hidden` |
| `Stop soloing` | `No branch is soloed` |
| `Compare {b} with {cur}` | `HEAD is unborn` (no commit to compare) |
| `Push {b} back to {r}…` (backups) | `The remote this backup came from cannot be found`; `{r} no longer exists` |
| Search | `Close the interactive rebase first`; `Close the conflict resolver first` |

## 13. UI inventory: menus, questions, colours, icons, automation ids

### 13.1 The menus as phase 5 changes them

Phase 3's rule stands (D77, D92): items that cannot apply are left out, items whose reason helps are
disabled with it.

| Builder | Change |
|---|---|
| `ForCommit`, `ForCommits`, `ForStash`, `ForWorkingChanges` | none (two selected commits compare without a menu, D162) |
| `ForLocalBranch` | a new group before `Copy branch name`: `Compare {b} with {cur}` (not on the current branch), `Show reflog of {b}`, `Hide {b}` or `Show {b}`, `Solo {b}` or `Stop soloing {b}` |
| `ForRemoteBranch` | a new group before `Copy branch name`: `Compare {r}/{b} with {cur}`, `Hide {r}/{b}` or `Show {r}/{b}`, `Solo {r}/{b}` or `Stop soloing {r}/{b}` |
| `ForTag` | `Compare {t} with {cur}` in a group before `Copy tag name` |
| `ForRemote` | `Hide every branch of {r}` or `Show every branch of {r}`, in a group before `Copy URL` |
| `ForSection` | Local branches and Remotes: `Show all hidden branches`, `Stop soloing` after their items |
| `ForUnstagedFile`, `ForStagedFile`, `ForCommitFile`, `ForDiff` | a group `File history`, `Blame` before `Open file` (4.4) |
| `ForEmptyGraphArea` | `Show reflog` in the last group, before `Manage remotes…` |
| `ForDrop` | `Compare {A} with {B}` in a group of its own at the end |
| New | `ForActivityLogRow` (2.2), `ForFileHistoryRow` (4.1), `ForBlameRun` (4.2), `ForReflogEntry` (5.2), `ForBackup` (5.3, 7.10), `ForCompareFile` (6.4: `File history`, `Blame`, `Open file`, `Show in folder`, `Copy path`), the search mode menu (3.1), the reflog source menu (5.1) |

Every changed menu's height and picture changes in phases 3 and 4 (14.1).

### 13.2 Questions

- Every undo or redo with a `RemoteRef` effect, and `Push {b} back to {r}…`: 7.5's question.
- `Restore the discarded files…` and `Restore the changes…` when a file would be overwritten: D75's
  `Restore discarded changes?` question, as the restore bar asks it.
- Nothing else asks: Undo, Redo and the automatic stash change only what can be put back.

### 13.3 New colours and icons

| Token (brush key) | Dark | Light | Used for |
|---|---|---|---|
| `VcSearchMatchBrush` | `#F2C94C` | `#B98900` | The 3-px mark of a search match (3.3) |

The dimmed rows use opacity, the blame gutter's runs and the activity log existing brushes. New
icons, Lucide's (ISC, in `THIRD-PARTY-NOTICES.md`): `IconHistory` ("history"), `IconBlame`
("text-search"), `IconEyeOff` ("eye-off"), `IconSolo` ("crosshair").

### 13.4 Automation ids

Toolbar and status bar: `UndoButton`, `RedoButton`, `SearchButton`, `ActivityLogToggle` (kept).
Activity log: `ActivityLog`, `ActivityLogHeader`, `ActivityLogShowReads`, `ActivityLogClose`,
`ActivityLogList`, `ActivityLogOperation`, `ActivityLogCall`, `ActivityLogOutput`,
`ActivityLogSplitter`. Search: `SearchBar`, `SearchModeButton`, `SearchBox`, `SearchCount`,
`SearchPrevious`, `SearchNext`, `SearchClose`. History: `FileHistory`, `FileHistoryHeader`,
`FileHistoryTitle`, `FileHistoryBlame`, `FileHistoryClose`, `FileHistoryList`, `FileHistoryRow`,
`FileHistoryDiff`. Blame: `Blame`, `BlameHeader`, `BlameTitle`, `BlameHistory`, `BlameClose`,
`BlameText`, `BlameGutter`. Reflog: `Reflog`, `ReflogSource`, `ReflogClose`, `ReflogList`,
`ReflogRow`, `BackupRow`, `ReflogFooter`. Compare: `Compare`, `CompareTitle`, `CompareSwap`,
`CompareClose`, `CompareFrom`, `CompareTo`, `CompareAllDifferences`, `CompareCounts`,
`CompareFileList`. Left panel: `RefHiddenToggle`, `RefSoloToggle`. Every page's root has an
automation peer that makes it a control element (a pane named by its title), as phase 4's views do.
Names stay unique: `ShellDriver.Find<T>(name)` returns the first match in the tree whatever its
visibility (the code map's pitfall), so no new view reuses `DiffSideLabel` or another phase 2 name;
the history page's diff part hosts phase 2's `DiffView`, whose names it shares, so the driver's
history helpers look inside `FileHistory` only.

## 14. What changes for earlier phases

### 14.1 Their checks and tests, changed on purpose

Every change below is listed in phase 5's report under "Changes to expected results", with the
tests changed in the commit that changes the behaviour (plan.md's gate, step 6).

| Change | Where it shows | Tests that pin today's state |
|---|---|---|
| Search enabled whenever a repository is open; Undo and Redo enabled after recorded actions, with tooltips; the Activity log toggle enabled with a repository | the toolbar and status bar of **every** window picture of phases 1 to 4 with a repository open; phase 0's empty shell is unchanged | `ToolbarTests` (lines 289 and 311), `Phase3Checks.A.Helpers.cs` (313), `ShellExpectations.cs` (the empty tab's states stay; the status bar's texts stay), `StatusBarTests` (38), `Phase0RealWindowPass` (125, 134) |
| Every stash's line dashed (section 10) | phase 1's graph-scenario pictures (its `stash@{0}`), phase 2's check 23, phase 3's pictures of the ops scenario with its stash row | `GraphLayoutTests` (the `IsDashed` facts), `CommitGraphControlTests` (line 115's colour sample in the stash's lane, which a gap can now hit: sample a dash) |
| New menu items (13.1) | the menu pictures and heights of phases 3 and 4 that show a branch, remote, tag, section, file, diff, empty-area or drop menu (the code map's list: about 60 pictures) | `RepositoryMenusTests`, `Phase3Expectations.*` and `Phase4Expectations.*` (lists and heights; the real-window pass links them) |
| Two selected commits show their comparison (D162) | phase 3's and phase 4's pictures with exactly two selected commits (the right panel) | `GraphSelectionTests`, the checks that assert `MultiSelectionView` with two commits |
| C12's refusal message (9.3) | phase 3's check 14 (`14a` to `14h`, `14b1`) and its real-window `14c`; phase 3's check 15 when a stash's apply is refused by the app first | `OperationTextsTests`, `RepositoryOperationsCheckoutTests`, `RepositoryOperationsTests` (106 to 147), `DialogTests`, `FailureClassifierTests`, `RepositoryOperationsBranchesTests` (318), `RepositoryOperationsStashTagsTests`, `Phase3Expectations.C.cs` (286 to 311) |
| Phase 4's leftovers (D160) | `a3d`, `b8c`, `c11j` (the SHA on one line); the resolver's pictures with a disabled arrow | as the leftovers' commits say |

Nearly every picture of phases 1 to 4 therefore changes, most of them only in the toolbar and status
bar. The gate makes that inspection quick and exact (15.7).

### 14.2 `plan.md` and the risks

Risk 12 (the run time) grows: a sixth test process, and two git calls before and after every
operation for the undo history (7.7). Risk 18 (the real-window pass) grows by phase 5's launches. A
new risk: the inspection of nearly every earlier picture at this gate (15.7). Phase 6's settings
screen may expose `--no-overwrite-ignore` for every checkout (9.2 uses it for the automatic stash
only).

## 15. Tests and harness

### 15.1 Scenario repositories (`tests/VisualCommit.Testing/Scenarios.Phase5.cs`)

Built once per process as templates, copied per test, every SHA pinned by
`tests/VisualCommit.Git.Tests/Phase5ScenarioTests.cs`, every git state derived with Git 2.36 (and,
where words may differ, also with the 2.55 on the development machine, 0):

| Scenario | Holds |
|---|---|
| `HistoryAsync` ("history") | three authors; `src/Parser.cs` created, edited, renamed to `src/Reader.cs` with an edit (over 50 % similar), edited again by another author, so blame has lines from four commits; `docs/notes.md` deleted and added again; an image; a merge of a side branch that edited `README.md`; commit messages with words only in their bodies, a pickaxe string added in one commit and removed in a later one; `main` and `feature/compare` diverged from a base, with a rename on `main`; a local bare `origin` |
| `FiltersAsync` ("filters") | `main`; `feature/alpha` with an upstream `origin/feature/alpha` one commit ahead; `feature/beta` with a long history of its own; `feature/merged`, merged into `main`; `origin/HEAD` pointing at `origin/feature/beta`; a stash made on `feature/beta`'s tip; a tag `v1` |
| `ReflogAsync` ("reflog") | a branch's reflog with a commit reset away (lost), an amended commit (the old one lost), a checkout of another branch and back, the checked-out branch renamed (a gap in HEAD's reflog), a branch deleted; backup refs of several kinds with fixed old names for the backups list (`branch-…`, `tag-…`, `stash-…`, `discard-…`, `remote-branch-…`), and backups older than 30 days for the activity log's clean-up notes |
| `ProtectAsync` ("protect") | `main` with older commits and a tag on one; `other` changing files `main` has, adding a file, deleting one; a file `main` ignores that an older commit of `main` tracks; working-tree changes are made by each check |

The undo checks use phase 3's and phase 4's scenarios (ops, sync with its bare remote, rebase,
conflicts) and these.

### 15.2 Harness additions

`ShellDriver.Phase5*.cs`: the activity log (open, rows as a snapshot, open a row, the splitter);
search (open by button and key, type, Enter, Shift+Enter, the marks as a snapshot read from the
control's `Search` and per-row state, not from pixels); the pages (history rows, the hosted diff,
blame lines and gutter runs, reflog rows, backup rows, the source menu); compare (rows, swap, the
switch); the left panel's toggles; the toolbar's Undo and Redo and their tooltips; `PressGitUndo`,
`PressGitRedo`, `PressSearch` (1.3). `RealApp.Phase5*.cs` for the same by automation id. The record
clock (2.3): `AppSession.Start(..., recordClock)` in `ShellDriver.Start`, and
`VISUALCOMMIT_RECORD_CLOCK=frozen` in `RealApp.Launch`. Each driver addition is tested in
App.Tests, as phase 4's are.

### 15.3 The walk-through project

`tests/VisualCommit.VisualTests.Phase5`, as phase 4's (D133, D151, the code map's recipe): csproj,
`AssemblyInfo.cs`, `Phase5Checks.cs`, `Phase5Checks.Support.cs` (phase 4's, with the part letters
`a` to `e` in `PictureName()`), `Phase5Expectations.Common.cs` and one file per part, their tests
named `Phase5ExpectationTests.<part>.cs` and `Phase5SupportTests.cs` (so the real-window link
pattern takes only the expectations); the project in `VisualCommit.slnx`; the real-window csproj's
link `..\VisualCommit.VisualTests.Phase5\Phase5Expectations*.cs`; `scripts/time-scripted-check.sh`
taking phase 5 by default and `-ge 5` for the new project; CLAUDE.md's Commands rows.

### 15.4 Proposed visual checks (the report's writers write them in full, step 6)

"RW" marks what the real-window pass repeats: a picture of every new screen, and what only a real
window shows (real keys, real typing, a real drag of the splitter, a real double-click, a real
restart of the process). Window 1100×700, dark, unless said.

| # | Check | RW |
|---|---|---|
| A1 | The toolbar and status bar: an empty tab (only Theme enabled); a repository open (Search and Activity log enabled, Undo and Redo disabled, `Nothing to undo`); after a commit, Undo enabled with `Undo commit "…"` | yes |
| A2 | The activity log: opened from the status bar under the graph; operations with each outcome (succeeded, warning, failed with its summary, cancelled); a row opened to its git calls; a call opened to its output; `Show reads`; the clean-up's `Removed backup` notes at opening (old backups of the scenario); Esc closes it | yes |
| A3 | The panel's height: the splitter dragged, the window restarted: the height kept | yes (a real drag and a real process restart) |
| A4 | The keys: Ctrl+F opens the search; Ctrl+Z in the graph undoes a commit, Ctrl+Y redoes it; Ctrl+Z in the commit summary undoes typing only; in the resolver's result it is the resolver's undo | yes (real keys) |
| B5 | File history across a rename, from the stage panel's file and from a commit's file: rows, the `renamed from` row, the diff of a step, the right panel showing the row's commit; the side-by-side layout at 1920×1080 (light theme); `Show in graph` | the page at 1100×700 |
| B6 | Blame: the gutter's runs and dates, `Not committed yet` lines, a click on a run (details), a double-click (the graph, the commit revealed), `Blame before this commit`, `History` | yes (a real double-click) |
| B7 | The reflog: HEAD's with the lost marks and the gap line, a branch's, `Create branch here…` from a lost commit (the branch in the graph) | yes |
| B8 | The backups list and restores: a deleted branch, discarded files (with the question when a file changed), a deleted remote branch pushed back (the question) | the list |
| C9 | Search, Commits mode: a word only in a commit body, an author, a SHA prefix: marks, dimmed rows, the count, Enter and Shift+Enter (the selection and the right panel), Esc | yes (real typing and Enter) |
| C10 | Search, Files and Changes modes: a part of a file name (the renamed file's both names), a string added then removed (two matches) | n/a |
| C11 | Compare two selected commits (two-dot): the right panel, a file's diff, Swap | yes |
| C12 | Compare branches from a branch menu (three-dot) and with `All differences`, the counts; the drop menu's compare | n/a |
| C13 | Hide `feature/alpha` (its upstream too): the graph without them, the left panel's marks, `Show`; solo `feature/beta`: the graph, labels, the stash whose base is hidden left out; `Stop soloing`; a restart keeps the hidden branch | yes (the restart) |
| C14 | Stashes dashed: the graph scenario's stash and the filters scenario's (the dash pattern sampled) | n/a |
| D15 | Undo and redo of a commit and an amend (the message back in the box), a discard of a file and of a hunk, Restore after them | yes (the toolbar's Undo) |
| D16 | Undo and redo of resets: soft, mixed, hard (the files back from the snapshot) | n/a |
| D17 | Undo and redo of branch operations: create, rename, delete (its upstream back), a checkout that created a tracking branch, a fast-forward | n/a |
| D18 | Undo and redo of history operations: merge (both kinds), a cherry-pick of a list, a revert, a rebase, a squash, a pull | n/a |
| D19 | Undo and redo of stash, pop, drop and tags (create annotated, delete) | n/a |
| D20 | Undo of remote actions against a local bare remote: force push, delete a remote branch, delete a remote tag, a push (each with the question); a lease that fails | n/a |
| D21 | Undo of local configuration: set upstream, remove remote (its branches and URL back), ignore | n/a |
| D22 | Refusals: Undo after a terminal commit on the same branch (the tooltip, the button disabled), during a merge in progress, a Keep move over a local change in the way (9.3's text) | n/a |
| E23 | Refused checkouts: a tracked change (`1 conflict prevents checkout…`, the button), several (`3 conflicts…`), an untracked file (no button), a folder in the way; "Create branch" with checkout | yes (the dialog) |
| E24 | The automatic stash: `Check out this commit` on an older commit of `main` with staged, unstaged and untracked changes: no question, the stash row (ring, dashed line to `main`'s tip), the status text, the detached HEAD; `main` checked out again; Pop | yes |
| E25 | When it is not taken: HEAD's own commit, a commit only `other` reaches (refused with 9.3's text); a file ignored now that the older commit tracks (refused after the stash, the stash popped back, the file intact) | n/a |
| E26 | Other refusals: a merge and a cherry-pick over a changed file, over a staged file; a stash's apply refused by the app | n/a |

### 15.5 Each "Delivers" item and what proves it

| Delivers | Proven by |
|---|---|
| File history following renames, and blame, with click-through | B5, B6; `FileHistoryTests`, `BlameParserTests`, `BlameTests` (Git), `FileHistoryViewModelTests`, `BlameViewModelTests` |
| Reflog viewer with restore | B7, B8; `ReflogTests` (Git: both calls, gaps, missing reflogs, lost), `ReflogViewModelTests`, `BackupReaderTests` |
| Activity log panel | A2, A3; `ActivityLogViewModelTests`, `OperationLogTests`, `ActivityLogViewTests` |
| Commit search, search inside changes | C9, C10, A4; `SearchTests` (Git: each mode, chunks, merges, stashes, the empty text), `SearchViewModelTests`, `CommitGraphControlTests` (marks) |
| Compare two commits or branches | C11, C12; `CompareTests` (Git: two-dot, three-dot, unrelated, criss-cross, renames), `CompareViewModelTests` |
| Undo and redo, many steps; remote undo with a lease; the keys | A1, A4, D15 to D22; 7.11's tests |
| Solo and hide, remembered | C13; 8.5's tests |
| C12 | E23 to E26, C14; 9.4's tests |
| Phase 4's leftovers | the leftovers' tests; `a3d`, `b8c`, `c11j` and the resolver's pictures |

### 15.6 Run time

The default run took 21 min 52 s at phase 4's gate, rested and alone. Phase 5 adds a sixth test
process (about 26 checks: estimated 12 to 18 minutes alone, run beside the others), Git-layer and App
tests of several git calls each, and two git calls before and after every operation of every
earlier check (7.7). Expect the run to grow by several minutes; the CI rule of D152 stands (past
20 minutes of the Windows Test step's 24, the lead raises the limit and records it). If the phase 5
project alone passes 20 minutes, it is split in two as D133 and D151 split phases. The real-window
pass gains about 14 launches (estimated 6 to 9 minutes on top of phase 4's 14).

### 15.7 The gate's inspection of earlier pictures

Nearly every earlier picture changes (14.1), mostly in the toolbar and status bar. So that the
inspection compares rather than re-reads: before the gate, the phase 0 to 4 walk-throughs run on
`master` in a worktree to make the reference pictures again (they must match
`docs/test-reports/phase-4-pictures.sha256`, which proves them the inspected ones); a script makes,
for each earlier picture, a difference picture and the bounding boxes of the changed pixels against
that reference; the inspectors get the reference, the new picture and the difference, and say for
each whether every change is one that 14.1 lists. A picture whose changes lie outside the regions
14.1 names is opened and judged in full. Phase 5's own pictures are inspected in full, as every
phase's are.

## 16. Work breakdown

Each package is built in its own worktree on a branch made **from `phase/5-history`**
(`wip/p5-<name>`; a worktree may start from `master`, so its agent creates the branch from the phase
branch first), merged back with `--no-ff` after its tests pass and the solution builds, its worktree
removed at once and its fsmonitor daemon stopped (phase 3 and 4 handoffs). A package edits only the
files named for it; anything else goes through the lead. A package whose code changes an earlier
phase's expected result edits that phase's tests and expectation files in the same commit.

| Package | Builds and owns |
|---|---|
| WP-L (started first, running) | Phase 4's leftovers (D160) and the two waits of the baseline run |
| WP0 (lead) | **Core**: `Undo/` (`UndoEntry`, `UndoEffect` and its records, `UndoHistory`, `UndoScope`), `Graph/GraphFilter`, the result records of history, blame, reflog, backups, compare and search, `OperationRecord`'s fields and `IOperationLog`'s events and notes, `GraphLine.IsDashed`, `SessionState.GraphFilters` and `ActivityLogHeight`. **Git**: `IGitRepository`'s new members (`ReadFileHistoryAsync`, `ReadBlameAsync`, `ReadReflogAsync`, `FindLostCommitsAsync`, `ReadBackupsAsync`, `CompareAsync`, `SearchAsync`, `ReadRefStatesAsync`, `ApplyUndoAsync`, `CheckUndoAsync`, the filter parameter of `LoadCommitsAsync`), `FakeRepositoryBase`, one stub file per package. **Queue**: the record clock, `Started`, the record's fields. **App contracts**: `IGraphAreaPage`, the pages' host interfaces, `IRepositoryActions` additions, `RunOptions.Undo`, `UndoCapture`, `RepositoryViewModel`'s hooks stubbed (`Page`, `ShowCommit`, `ShowInGraph`, `Compare`, `Search`, `UndoHistory`), `EscapeOrder` 370, 380, 390, `ToolbarViewModel`'s new commands as stubs. **Theme**: `Icons.axaml`, `THIRD-PARTY-NOTICES.md`, `Tokens.axaml`. **Tests**: `GitIsolation`/`RealApp` (the record clock) |
| WP-S | 15.1's scenarios and `Phase5ScenarioTests`; the Phase 5 project (15.3); the driver files of 15.2 as their views arrive (shared with WP5 to WP7); `scripts/time-scripted-check.sh`; CLAUDE.md's rows |
| WP1 (Git: reading) | `GitRepository.FileHistory.cs` (file history and blame), `.Reflog.cs` (reflog, lost commits, backups' reader on phase 4's parser), `.Compare.cs`, `.Search.cs`; their tests |
| WP2 (graph) | `GraphFilter` in `LoadCommitsAsync` and `CommitPager` (8.2), `GraphLayout`'s dashed stash lanes (10), `CommitGraphData`'s label filter, `CommitGraphControl`'s dashes and search marks (3.3, 10); their tests and phase 1 to 3's graph tests and expectations that change |
| WP3 (undo engine) | `GitRepository.Undo.cs` (every effect of 7.3 forward and back, the checks, the reads of 7.7), `RepositoryOperations.Undo.cs` (recording in `RunAsync` and `WriteAsync`, undo and redo operations, the history per repository, the toolbar's state and tooltips, the keys' handlers' targets, `BackupsToKeep`, the restore bar's tie, 7.10's restores), `OperationTexts.Undo.cs`; 7.11's tests; phase 0 to 4's toolbar tests that change |
| WP4 (C12) | `GitFailureClassifier` (the count, the folder kind), `RepositoryOperations.Branches.cs`'s checkouts (9.1 to 9.3 on every path), the stash apply pre-check, `OperationTexts`; 9.4's tests; phase 3's check 14 and 15 expectations and tests that change |
| WP5 (activity log and shell) | `OperationQueue`/`OperationLog` changes, `ActivityLogViewModel`/`ActivityLogView`, the grid under the graph area and its splitter and session height, the status bar's toggle, `MainWindow`'s keys (1.3), the toolbar's Search and Undo/Redo bindings; their tests |
| WP6 (pages) | `FileHistoryViewModel`/`View`, `BlameViewModel`/`View` with the gutter, `ReflogViewModel`/`View` with the backups list, their menus and the file menus' new group (13.1), the hosted diff; their tests |
| WP7 (search, compare, solo and hide) | `SearchViewModel`/`SearchBarView`, `CompareViewModel`/`CompareView` in the right panel's slot, the left panel's marks and toggles, the branch, remote, section, tag and drop menus' new items, the session's filters; their tests |
| WP8 | The scripted checks of the report, part by part: `Phase5Checks.{A..E}*.cs`, `Phase5Expectations*.cs`, `Phase5ExpectationTests*.cs`; phase 0 to 4's expectation files for 14.1's changes |
| WP9 | The real-window pass for the RW checks: `Phase5RealWindowPass*.cs`, `RealApp.Phase5*.cs`, the Phase 5 link in `RealWindowTests.csproj`, and phase 0 to 4's real-window expectations that change |
| Lead | `RepositoryViewModel*.cs` (the hooks: pages, `ShowCommit`, `ShowInGraph`, compare's place, search's place, the filter's reload, `OnStatusApplied`/`ApplyRefs` feeding the pages and the undo state), `RepoTabViewModel.cs`, `MainWindow.axaml(.cs)`, `MainWindowViewModel.cs`, `CommitGraphView.axaml`, `AppSession.cs` (the histories, the record clock), the `.slnx`, CI, the docs, CLAUDE.md |

**Order**: WP-L runs from the start. WP0 (contracts and stubs, so that packages build against each
other: phase 4's lesson); then WP-S, WP1, WP2 and WP3's git half side by side; the report's checks
(step 6) are written once WP-S has pinned the scenarios, and committed before WP5 to WP7 build UI;
then WP3's app half, WP4, WP5, WP6 and WP7 side by side, the lead joining their hooks; then WP8 and
WP9; then the review (step 25) and the gate.

## 17. Decisions

This design records these in `decisions.md`:

| # | Decision |
|---|---|
| D164 | The undo history (7): per repository and session in `AppSession`, at most 100 entries; an entry is a list of effects from a closed set, recorded by reading the declared refs and state before and after the action inside its own operation; undo and redo are one queued operation each that checks everything before writing; strict order; outside changes never remove entries |
| D165 | What the undo history records and leaves out (7.2): stage, unstage, applying lines to the index, fetches, the clean-up and the resolver's resolutions are not recorded; a stopped action is one pending entry completed by Continue, Skip or Commit and removed by Abort; undo is off while an operation is in progress |
| D166 | Moving the checked-out branch back keeps the user's later work: `read-tree -m -u` and a compare-and-swap `update-ref` on `refs/heads/<b>`, never `reset --merge`, `reset --keep` or `switch -C`, and never through `HEAD`'s link; `switch` keeps git's own reflog message; `GIT_REFLOG_ACTION` is never set (7.3) |
| D167 | Every undo or redo that changes a remote asks first, not only a force push's and a deletion's (extends D155); the lease's expected value is given explicitly in both directions, and a push's old value comes from git's output line (7.3, 7.5) |
| D168 | The backups an entry of the undo history needs are kept by the clean-up (`keep`) while the entry exists, and still count toward D139's 50 (7.8) |
| D169 | The backups list in the reflog page restores backups outside the session with the undo history's effects (5.3, 7.10) |
| D170 | The activity log reads structured records, never the log file: `OperationRecord` gains the repository's key, the warning, the kind, the failure's summary and notes; the backups' clean-up reports its removals as notes of the operation it ran in; the records' times come from a clock that both harnesses freeze (2.3) |
| D171 | Search gives git the graph's rows' SHAs (`--no-walk=unsorted --stdin`, in chunks), so it searches exactly what is shown, hidden branches excepted; Files and Changes match a commit's change against its first parent; case is ignored (3.2) |
| D172 | File history is git's `log --follow` from a fixed start, without merges; blame is `blame --porcelain --no-textconv` with the user's `blame.ignoreRevsFile` honoured; both are pages of the graph area with the history's diff beside or below the list by width (4) |
| D173 | The graph area's pages (history, blame, reflog) hold no work, exclude the interactive list, and lie under a diff; `ShowCommit` shows any commit in the right panel, in the graph or not (11) |
| D174 | Compare: two commits two-dot, branches three-dot by default with an "All differences" switch; names resolved to SHAs once (6.3) |
| D175 | Solo and hide act on branches only; hiding or soloing a local branch includes its upstream; the checked-out branch is always shown; the filter is git's `--exclude` and named refs, with a stdin fallback past 30,000 characters; a stash whose base is hidden is left out (amends D54 while a filter is on) (8) |
| D176 | C12's rules (9): the automatic stash only for a detaching checkout of a commit the current branch reaches, not HEAD's own commit, never from a detached HEAD, with `--no-overwrite-ignore` on its checkout only; every checkout path shows D153's message with git's count; merge, cherry-pick and revert refusals say the same; a stash's apply is checked by the app before git |
| D177 | The reflog is read through git with two calls zipped by position; one reflog at a time (HEAD, a local branch); lost commits from one `rev-list --stdin` call (5.2) |
