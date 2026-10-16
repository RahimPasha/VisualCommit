# Phase 5 design: history, traceability, undo, and protected changes

Working design notes for phase 5, written on 2026-10-12 before any phase 5 code exists. The
builders implement from this file; its final facts are folded into `architecture.md` at the end of
the phase, after which this file is kept as history, as `phase-3-design.md` and `phase-4-design.md`
are. The last section lists the decisions this design makes; they are recorded in `decisions.md`
from D167 on. Where this file and merged code differ later, the code's doc comments say what is
built; tell the lead.

Sources: `plan.md` "Phase 5", the phase 4 handoff's "For the next phase", decisions D47, D52, D54,
D60, D62, D63, D67, D80, D81, D83, D88, D91, D95, D124, D137 to D139, D148 and D153 to D166, a code
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

Asked after the design's five reviews (2026-10-12):

12. (D164) **The details panel gains an "All files" switch** beside Flat and Tree: every file of the
    shown commit's tree, each with File history and Blame in its menu.
13. (D165) **An Abort is undone from the backups list**, not by Undo: its files and commits come back,
    the stopped operation does not. An Abort records no undo entry.
14. (D166) **Undoing the automatic stash's checkout switches back and pops that stash.**

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
4. File history and blame, and "All files"
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
| The history command | `git log --follow --root -z -M --name-status --no-show-signature --no-color --encoding=UTF-8 --format=%H%x1f%P%x1f%aN%x1f%aE%x1f%aI%x1f%cI%x1f%s <start sha> -- <path>`: per commit the graph's own header (parsed by `TryParseLogLine`) ended by `\0`, then `\n` before the first entry, then `status\0path\0` (`R` and `C`: `status\0old\0new\0`, with a score such as `R088`). The next header follows the last path directly. The runner splits stdout at `\n`; joining the chunks with `\n` gives the stream back, which is then split at `\0`. Paths are raw UTF-8 bytes (`-z`: `core.quotePath` does not apply). **Built without `-z`** (WP1): with it a commit's entries share a line with the next header, so each row reached the app one commit late. The app runs `git -c core.quotePath=true log --follow --root -M --name-status … <start> -- <path>`: per commit the header line, an empty line, then one `status\tpath[\tpath]` line per entry, every path holding a tab, line feed, control character, quote, backslash or byte above 127 C-quoted (unquoted by `GitQuotedPaths`), and each row is handed over on its entry line |
| What `--follow` lists | A rename at 50 % similarity or more is followed (`R100`, `R088`); below 50 % it is an `A` and the older history is not reached (as `ReadDiffAsync`'s `--find-renames`, also 50 %). A copy shows as `C100 src copy` (follow turns on copy detection) and goes on with the source's history. A file deleted and re-added, or a new file at an old renamed name, lists both lives (`A`, `D`, …). A deleted file from HEAD lists the deletion first |
| What `--follow` leaves out | **Merges**: no merge is listed, also one whose conflict resolution wrote into the file (blame can still name it). `-m` lists merges twice, `--cc` every merge, `--diff-merges=first-parent` is 2.31+ and duplicates branch changes. A rename on a branch while another branch kept editing the old name loses the other branch's edits (git's own limit; the walk order decides which) |
| Empty, errors | An untracked file, a file only in the index, a path in no commit: no output, exit 0. A staged, uncommitted rename: nothing under the new name. Two pathspecs: `fatal: --follow requires exactly one pathspec` (128). Unborn HEAD: `fatal: your current branch 'main' does not have any commits yet` (128). A directory path lists every file under it |
| Settings | `log.showRoot=false` drops the root commit's row: pass `--root`. `diff.renames` and `log.follow` do nothing here; `core.quotePath` does without `-z`, so the app fixes it with `-c core.quotePath=true`. `GIT_LITERAL_PATHSPECS=1` keeps `pages/[id].txt` literal (without it the brackets match `pages/d.txt`) |
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
| "Lost" | `git rev-list --ignore-missing --stdin --not --branches --tags --remotes HEAD --` (the `--` because a file named `HEAD` in the working tree makes `HEAD` ambiguous: `fatal: ambiguous argument 'HEAD': both revision and filename`, 128, on 2.36 and 2.55) with the entries' SHAs on stdin: one process; 0.14 s when the entries lie near the tips, about 1 s for entries 50,000 commits deep on the 100k repository without a commit-graph (0.25 s with one). One call per SHA took 30 s or more for 124 SHAs |
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
| File | Needs `GIT_LITERAL_PATHSPECS` removed for the call (as `AllIgnoredAsync` does): `":(glob,icase)**/*<t>*"` and `":(glob,icase)**/*<t>*/**"` (a glob matching a folder does not match files under it, so two pathspecs), with `\` turned into `/` and `*?[\` escaped by `\`. With the variable set the magic is read as a file name and finds nothing, silently; `git --glob-pathspecs` and `GIT_GLOB_PATHSPECS` refuse to combine with it (`fatal: global 'literal' pathspec setting is incompatible…`). `icase` folds ASCII only. Finds deleted files and both names of a rename commit. **Git 2.55.0 ignores the path under `--no-walk`** and prints every commit it is given (a regression, fixed upstream by 32c4ed70; 2.36 filters, and so does CI's macOS git 2.56.0, while Git for Windows 2.56.0.windows.2 does not), so the app lists the paths instead and counts a commit only when a path line follows it: `--format=%x1e%H --first-parent --root --no-renames --name-only -- <the two globs>` (WP1, `SearchTests`) |
| Merges in path and pickaxe searches | Default simplification prunes side-branch commits whose effect was undone; `--full-history` adds merges that differ only from their second parent. **`--first-parent` with `--no-walk --stdin` gives exactly "the commit's own change as the right panel shows it"** (a merge against its first parent, the right panel's rule) |
| Changes | `-S<text>`: a commit whose number of occurrences of the text changes (added or removed: O7's wording); fixed string; searches binary files; `-i` folds ASCII only. `-G<regex>` matches any changed line containing it (extended regex, skips binary). Merges never match without a merge option; `--diff-merges=first-parent -s` (2.31+) or `--first-parent` (2.29+ implies first-parent diffs, UNVERIFIED on 2.30) matches a merge's first-parent change once. `log.showRoot=false` misses the root (pass `--root`); `diff.renames=false` makes a rename look like delete-and-add (pass `--find-renames`); textconv applies (pass `--no-textconv`). An empty `-S` is exit 129 or a `BUG` (exit 3): never pass one |
| Search timings | Message on 100k: 1.6 s first hit, 1.8 s all (chunked: 70 ms first). File glob on 100k: 1.9 s. `-S` costs about 0.4 to 1 ms per commit with real changes (`-G` 2 to 3 times that): tens of seconds on a large real history |
| Stash rows | A stash commit is a merge (base, index commit, untracked commit); with its SHA on stdin and first-parent options it matches by its working-tree changes; its untracked files are only in `^3` and are not searched (the details panel does not show them either) |
| Compare list | `diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id <old> <new>` with two SHAs, parsed by `ParseNameStatus`. **`diff-tree A...B` prints nothing and exits 0** (a range is ignored). `diff.renameLimit` applies (a warning `exhaustive rename detection was skipped…` turns renames into delete and add) |
| Three-dot | `git merge-base A B` once (built: `merge-base --all A B` once, the first base printed being the one `git diff A...B` uses and more than one marking a criss-cross; both names resolved first in one `rev-parse <a>^{commit} <b>^{commit} --`), then the two-SHA list from the base. Exit 1 with no output: unrelated histories. Several bases (criss-cross): `merge-base` prints one; `git diff A...B` uses the same with a warning; `--merge-base` fails (`fatal: multiple merge bases found`, 128). Two-dot shows A's own later changes reversed (A's rename appears renamed back) |
| Counts | `rev-list --left-right --count A...B` prints `L\tR`; meaningless for unrelated histories and for a stash (it counts the stash's own commits). 0.4 s on 100k |
| Per-file diff | `DiffTarget(Commit, path, kind, oldPath, Commit: new, Parent: old)`: git pairs any two paths given, so the old path comes only from the list's own `R`/`C` entry |

### 0.4 Undo: moving HEAD, branches and commits back

| What | Result |
|---|---|
| Moving the checked-out branch back with uncommitted work | `reset --merge` loses staged changes without a word; `reset --keep` keeps contents but replaces the index (a partly staged file loses its staged version); `switch -C` keeps everything but writes `checkout: moving from main to main`, which breaks `@{-1}`. **`git read-tree -m -u <now> <target>` then `git update-ref -m <msg> refs/heads/<b> <target> <now>`** keeps staged, partly staged and unstaged work, refuses (changing nothing) when a file the move changes has local changes or an untracked file is in the way, and is the only one with a compare-and-swap. **It must follow `git update-index -q --refresh`**: `switch` refreshes the index before its two-way merge, `read-tree` does not, and an unchanged file with stale stat data (touched, or edited and edited back; the app's reads never write a refreshed index, D47) refuses with `not uptodate`. **It overwrites an ignored file where the target has one and deletes an ignored folder where the target has a file, without a word** (2.36 and 2.55; Git 2.34 made read-tree treat ignored files as expendable, so 2.30 refuses instead, inferred); no option keeps them (`--exclude-per-directory` only allows overwriting), so the app checks for ignored paths in the way itself (7.3). `switch --no-overwrite-ignore` refuses in the same case. If the update-ref then fails (a race), `read-tree -m -u <target> <now>` puts the files back exactly. `read-tree -n -m -u` is a dry run that writes nothing |
| Refusals of read-tree | `error: Entry 'f.txt' not uptodate. Cannot merge.`, `error: Entry 'f.txt' would be overwritten by merge. Cannot merge.`, `error: Untracked working tree file 'd.txt' would be overwritten by merge.`, `… would be removed by merge.` (128); it stops at the first, so a count of the paths in the way must come from the app's own reckoning (7.6) |
| Compare-and-swap | `update-ref -m <msg> <ref> <new> <old>`: `fatal: update_ref failed for ref '<ref>': cannot lock ref '<ref>': is at <sha> but expected <sha>` (128). `<old>` empty = must not exist (`reference already exists`). `--stdin` with `update`/`create`/`delete`/`verify` lines is all or none. **Always name `refs/heads/<b>`**, never `HEAD`: after a terminal checkout of another branch at the same commit, `update-ref HEAD` moved that other branch. `update-ref` moves a branch checked out in another worktree without a word (`%(worktreepath)` of `for-each-ref` tells) |
| Soft moves | Undo of a commit, an amend or a soft reset: `update-ref refs/heads/<b> <before> <after>`: the commit's changes come back staged, the working tree untouched; exact. The initial commit: `update-ref -d refs/heads/<b> <after>` (the branch becomes unborn, the index kept); redo `update-ref refs/heads/<b> <after> ""` |
| In-progress states | `update-ref` refuses nothing during a merge, rebase or pick; `reset --soft` (`fatal: Cannot do a soft reset in the middle of a merge.`), `switch` (`fatal: cannot switch branch while merging`) and read-tree with conflicts do. The app's own in-progress check must come first |
| Mixed and hard reset | A mixed reset leaves the old commit's files in the working tree, so its undo is a soft move (a Keep move refuses on every file the reset crossed), then `git restore --staged --source=<source> -- .` where `<source>` is the `reset-…-changes` snapshot or, when the app made none (the index equalled HEAD, `SnapshotIndexAsync` returns null), the commit moved back to: exact, partly staged files and files the reset made untracked included; precondition: the index unchanged since (`diff-index --cached --quiet <after>`). Undo hard: the move back (read-tree), then the index from the snapshot's second parent, then the files (`RestoreSnapshotAsync`), in that order; precondition: none of the snapshot's paths changed since (`git restore --worktree` overwrites without a word). `read-tree -m <tree>` refuses an index with a partly staged file; plain `read-tree <tree>` drops stat data |
| Checkouts | Undo: `git switch --no-guess --no-overwrite-ignore <before>` (or `--detach`): it carries work or refuses with the checkout texts, and the option keeps it from overwriting ignored files as read-tree would. A checkout that created a tracking branch: switch back, `delete refs/heads/<b> <tip>` through `update-ref --stdin`, `config --remove-section branch.<b>` (`update-ref -d` leaves the config). Redo of that: `switch --no-track --create <b> <tip>` and the two config keys (`--track` with a SHA fails: `fatal: cannot set up tracking information; starting point … is not a branch`) |
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
| Windows file locks | An untracked file opened exclusively by another program: the stash fails before saving anything. One open for reading only: the stash is saved, git says `warning: failed to remove <file>` and exits 1 (2.36 and 2.55). Git's clean step **has already deleted every other untracked file** before failing on the locked one, and then skips its reset: the tracked changes and the locked file are in both places, the other untracked files only in the stash. `git restore --source=<stash>^3 --worktree -- <missing paths>` puts them back (9.2) |

## 1. The shell: toolbar, status bar, keys

### 1.1 The toolbar's Undo, Redo and Search

Undo and Redo become real (section 7) and keep their places at the toolbar's left; Search becomes
real (section 3) and keeps its place at the right, left of Theme. They bind to the active tab's
repository, as Fetch to Pop do (`ActiveTab.Toolbar`): `UndoCommand`, `UndoToolTip`, `RedoCommand`,
`RedoToolTip`, `SearchCommand`, `SearchToolTip` on `ToolbarViewModel`, which reads them from the
repository's undo history (7.4) and graph area (11.2). An empty tab's toolbar keeps every button
disabled, so phase 0's empty shell stays as it is (only Theme enabled).

| Button | Enabled when | Tooltip, enabled | Tooltip, disabled |
|---|---|---|---|
| Undo | the entry before the history's cursor can be undone now (7.4) | `Undo {text}` (7.6: `Undo commit "Fix the parser"`) | 7.4's first reason: `Wait for {title} to finish`, `Finish or abort the {kind} first`, `Resolve the conflicts first`, `Close the interactive rebase first`, `Nothing to undo`, or the entry's check (`main has moved since (it is at 9f8e7d6)`) |
| Redo | the entry after the cursor can be redone now | `Redo {text}` | the same reasons, with `Nothing to redo` |
| Search | a repository is open and neither the interactive list nor the resolver is open (11.2) | `Search commits ({gesture})`, `{gesture}` being `Ctrl+F` or `Cmd+F` from the platform (1.3) | `Close the interactive rebase first`, `Close the conflict resolver first` |

Phase 3's design (4d) gave the toolbar the rule that an enabled button has no tooltip; Theme already
breaks it. Undo, Redo and Search break it too, on purpose: what Undo and Redo would act on is not
visible anywhere else and the first click must not surprise, and Search's key is worth showing. Their
tooltips are also their automation help text (`AutomationProperties.HelpText`), which the real-window
pass reads; the checks rest the pointer at the neutral point before a capture, as before, so no
tooltip shows in a picture unless the step is about it.

A click on Undo or Redo runs the step as one operation of the repository's queue (7.5). A click on
Search opens the search bar (3.1).

### 1.2 The status bar's Activity log

`ActivityLogToggle` becomes real: enabled while the active tab has a repository (an empty tab keeps
it disabled, so phase 0's pictures stay as they are), tooltip `Show every git command the app has
run` while the panel is closed and `Hide the activity log` while it is open. It toggles the panel
(section 2) for the window: the panel's being open is the window's state, not a tab's, and shows the
active tab's repository; on a tab without a repository the panel is hidden and the window keeps its
state for the next repository tab. While the panel is open the button has the new `selected` class
of `VcFlatButton`: text in `VcTextPrimaryBrush` on `VcControlBackgroundBrush`, so that it differs
from the hover look (primary text only).

### 1.3 Keys

There are no window-wide shortcuts today (the code map, menus.md section 6). Phase 5 adds three, in
`MainWindow`'s key handling, each built from the platform's `HotkeyConfiguration` (never from a test
of the operating system), as `CommitGraphControl.CommandModifiers` is:

| Keys | Does | Declines (the key goes on as before) when |
|---|---|---|
| the platform's `HotkeyConfiguration.Undo` (Ctrl+Z; Cmd+Z) | the toolbar's Undo (D163), when it is enabled | a dialog is open; a menu is open; a branch drag runs; the focus is in a `TextBox` (Avalonia's text undo) or anywhere in the conflict resolver (its own undo and redo, D148, whether it shows text or a card) |
| every gesture in the platform's `HotkeyConfiguration.Redo` (the builders record the lists Avalonia 12.1.3 gives on Win32, macOS, X11 and headless; the owner's words were Ctrl+Y and Cmd+Shift+Z) | the toolbar's Redo, when it is enabled | as Undo |
| the command modifier with F (Ctrl+F; Cmd+F) | the toolbar's Search, when it is enabled | a dialog or menu is open. In the search box it selects the box's text |

A disabled Undo or Redo makes its key do nothing (the key is handled, so it does not reach the
graph). Undo and Redo are handled on the window's bubbling `KeyDown` without `handledEventsToo`, so a
text box or the resolver that took the key first keeps it; the declines above stop the keys that such
a control does not mark handled (a text box with nothing to undo). Search's key is handled in the
tunnelling phase, as `OnShiftF10` is (D106): nothing else uses it.

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
  it, and so do the panel's close button and Esc while the focus is inside it
  (`EscapeOrder.ActivityLog` = 340: before the resolver, the list and a page, which would otherwise
  take Esc wherever the focus is; the handler declines unless the focus is inside the panel).
  Closing the panel while the focus is inside it gives the focus to the graph
  (`GraphFocusRequested(Graph)`). A closed panel takes no room, so every picture of phases 0 to 4
  keeps its layout.
- **Which repository**: the active tab's, as one worktree (the key of 2.3). Switching tabs shows the
  other repository's log at once; each keeps which rows are open and where it was scrolled.

### 2.2 What it shows

A header 30 high (`ActivityLogHeader`): `Activity log`, then the repository's name in the secondary
colour, then on the right a check box `Show reads` (`ActivityLogShowReads`, unchecked) and the close
button (`ActivityLogClose`, `IconClose`). Below it a virtualised list (`ActivityLogList`) of rows 24
high, **in the order the operations and calls started** (the sequence numbers of 2.3), oldest at the
top and newest last (owner's answer 4); the list starts scrolled to the end and stays at the end while
new rows come, unless the user scrolled up.

**An operation's row** (`ActivityLogOperation`, automation name its title), left to right:

| Part | Content |
|---|---|
| Chevron | `IconChevronRight`, `IconChevronDown` while open; the row opens and closes on a click anywhere on it or with Right and Left |
| Outcome | a 10-px glyph in its tone: `IconCheck` in `VcSuccessBrush` (succeeded), `IconAlert` in `VcWarningBrush` (a warning or a stop with conflicts), `IconClose` in `VcDangerBrush` (failed), `IconMinus` in `VcTextSecondaryBrush` (cancelled); a running operation has none and shows `Running…` as its message |
| Time | when it started, `HH:mm:ss`, in the zone `DateDisplay` uses (D43) |
| Title | the operation's title (D113), e.g. `Check out feature/alpha`, `Undo commit "Fix the parser"` |
| Message | the status bar's text for it, in its tone; empty for the stage panel's writes (their message goes to the stage panel) |
| Duration | `{N} ms` below a second, `{N.N} s` above, right-aligned, secondary |

Title and message are cut with "…" (`TrimmedTextBlock`), the whole text in the tooltip.

**Opened**, an operation shows under it, indented 24, in this order:

1. A failure's summary, as the error dialog showed it, in the danger colour.
2. Its notes (2.3), one line each, secondary: `Removed backup reset-20260102T090000000Z (older than
   30 days)`.
3. Its git calls, one row each (`ActivityLogCall`, automation name its command), 22 high: a chevron,
   the exit code (`exit 0` in the secondary colour, another code in `VcDangerBrush`, `cancelled`,
   `failed to start`), the command (`GitCallRecord.CommandText`, credentials hidden, in the code
   font, cut with "…"; a call made on a temporary index is followed by `(on a temporary index)`),
   the duration. Opened, a call shows what it was given on standard input when the app records it
   (only `update-ref --stdin`'s lines: ref names and SHAs, so that a backup's or an undo's
   transaction can be read), then its standard output and standard error (as recorded: the first 16
   KiB of each, credentials hidden, D103) in a read-only box in the code font, 12 lines at most
   before it scrolls, each part titled `Input`, `Output` and `Error` when not empty, and `No output`
   when all are empty.
4. When the operation's calls are no longer kept (2.3): `Its git commands are no longer kept.`

**Reads**: with `Show reads` checked, the git calls of this worktree that belong to no operation (the
app's reads: refreshes, the details panel, diffs, searches) show as rows of their own between the
operations, in their start order: `ActivityLogCall` rows at the top level. Unchecked, they are
hidden; a read made inside an operation (the operation's own `status`, the undo's reads) is one of
its calls and always shows under it.

**Context menu** on a row (`RepositoryMenus.ForActivityLogRow`): `Copy command` (a call), `Copy
output` (a call with output), `Copy title` (an operation). No other action: the log only shows.

**What it never shows**: an operation dropped before it started (a closed tab, D81's "Quit now"),
which has no record; the welcome page's open, init and clone, which run before a repository's tab
has one (their calls are in the log file); the credentials of a prompt (the call log never has them,
D103, and `ShellDriver.WhereWritten` keeps proving it).

### 2.3 What the records must carry

Today `OperationRecord` is `(Id, Repository, Title, StartedAt, Duration, Outcome, Message)` in a
session-wide `OperationLog` of 500, with no event; `GitCallRecord` carries `OperationId`; the
call log is one ring of 2,000 calls, reads included; the backups' clean-up's removals reach only the
log file (operations.md sections 3 and 4). Phase 5 changes:

- **Keys and order.** `OperationRecord` gains `RepositoryKey` (the worktree's own git folder, as
  7.1's histories: unique per worktree, unlike the folder name) and `Sequence`; `GitCallRecord`
  gains `Sequence`. Both sequences come from one session-wide counter (`Interlocked`), taken when the
  operation or call **starts**, so the panel orders rows by start whatever the times say (the
  harnesses freeze them) and whenever the panel was opened. A read belongs to the worktree whose
  working tree, or the folder a tab opened it from, holds the call's `WorkingDirectory` (the first
  `rev-parse` of `OpenAsync` runs in the folder as opened: a subfolder, or a path through a link,
  which git's `--show-toplevel` resolves; `IGitRepository.OpenedFolder` keeps it, found by CI's
  macOS run 38847538648, `15a252f`).
- **More of each operation.** `OperationRecord` gains `IsWarning`, `Kind` (Local or Network),
  `IsBackground` and `Notes` (a list of strings), filled by `OperationQueue.Record`, and
  `FailureSummary`, which the queue cannot know (the error dialog's summary is chosen after the
  operation, by `RunOptions.DescribeFailure` or the text's own summary): `IOperationLog.SetFailureSummary(id,
  summary)` is called by `RepositoryOperations.ShowResultAsync` once it has chosen the summary (also
  when the tab has closed or no dialog is shown), and the panel updates the row.
- **Events.** `IOperationLog` gains `Started(id, key, title, sequence)` (raised by the queue when an
  operation starts, so the panel shows it running), `Added(record)`, `Note(operationId, text)` (kept
  until that operation's record is added, then put in its `Notes`; a note for an id with no record
  after 10 minutes is dropped), `Updated(id)` and `Evicted(id)`; all raised on the thread that acts,
  the panel posting to the UI thread.
- **Calls kept with their operation.** `GitCallLog` keeps the calls that carry an `OperationId` until
  that operation's record leaves the 500 (`Evicted`), and the reads in a ring of 2,000 of their own,
  so a burst of reads never takes an operation's commands away. A memory budget of 64 MB for the
  kept operation calls (each output is already cut to 16 KiB) drops the oldest operation's calls
  first, which then shows `Its git commands are no longer kept.`
- **Input and temporary index.** `GitCommand` gains `RecordsStandardInput` (set only by
  `update-ref --stdin`, whose lines name refs and SHAs and carry no secret) and the record keeps that
  input; a command run with `GIT_INDEX_FILE` set is marked `OnTemporaryIndex`.
- **The backups' clean-up** returns what it removed (`IReadOnlyList<RemovedBackup>`, already returned
  by `IGitRepository.CleanUpBackupsAsync`); `RepositoryOperations.CleanUpBackupsAsync` and the
  background "Clean up backups" pass each removal to `IOperationLog.Note(GitOperationScope.Current,
  "Removed backup {name} ({reason})")`, with the same words as the log file's line. The background
  operation's message becomes `Removed {N} backups` (`Removed 1 backup`; still never shown in the
  status bar, D139).
- **Times for pictures.** The times the panel shows (`OperationRecord.DisplayedStart` and
  `DisplayedDuration`, `GitCallRecord.DisplayedStart` and `DisplayedDuration`) come from a
  `RecordClock` (`TimeProvider`) that `AppSession.Start` passes to every queue and to the runner,
  `TimeProvider.System` by default. The queue keeps its own system-clock start (`entry.StartedAt`)
  for the stale-lock scan after a cancel (D126) and its stopwatch for the log file's `after {N} ms`;
  the runner likewise. Both harnesses give a frozen record clock at 2026-01-02 09:00:00 UTC (the
  app's commit time, D72): the headless one through `AppSession.Start`'s new `recordClock`
  parameter, the real-window pass through `VISUALCOMMIT_RECORD_CLOCK=frozen`, which `RealApp.Launch`
  sets. Every time then reads `09:00:00` and every duration `0 ms`.
- **Backup names and the clean-up's "now" in tests.** `GitRepository.BackupClock` (today the system
  clock) is given by `AppSession.Start` to every repository the app opens (`backupClock`), and both
  harnesses give one that says it is 2026-01-02 09:00:00.000 UTC for the clean-up and names each
  backup one second after the last (its "now" moves forward with each name, so no backup is in the
  clock's future; `BackupClock` is a class of its own, since one `TimeProvider` cannot give both) (`VISUALCOMMIT_BACKUP_CLOCK=fixed` for the real window): backups
  the checks make get the same names in every run and both passes, still in order, and a scenario's
  backups are judged against a fixed day (15.1). Phase 3's and 4's checks and App tests that make
  backups or age them by the wall clock are adjusted in the same commit (`RepositoryOperationsBackupCleanupTests`
  dates its old backups against the clock it is given).

### 2.4 Tests

`ActivityLogViewModelTests` (records and calls as they come, the grouping by id, the order by
sequence with equal times and with the panel opened late, reads by worktree, shown and hidden, two
tabs' reads, notes, the failure summary set after the record, an evicted operation's calls, a burst
of reads that evicts no operation's call, the running row, per-worktree state), `OperationLogTests`
(the events, notes before and after the record, the 500 limit, `Evicted`), `GitCallLogTests` (the
two stores and the budget), `ActivityLogViewTests` (the header, a row opened by click and by Right,
the splitter's limits, Esc with the resolver, the list or a page open, the session's height), and the
scripted checks of section 15.

## 3. Search

### 3.1 The bar

The toolbar's Search, or its key (1.3), shows the graph (closing a diff or a page of section 11,
which hold no work) and opens the search bar (`SearchBar`, `SearchBarView`) at the top of
`RepositoryGraphView`, under the banner and above the column headers. It is 36 high,
`VcChromeBackgroundBrush` with a 1-px `VcBorderBrush` line along its bottom, and pushes the column
headers and rows down by 36, as the banner does (D122: every harness takes row positions from the
control's bounds; the real-window pass's `GraphRowsTop` learns the bar). Left to right, 8 from the
edges, 6 apart:

| Part | Automation id | Width | What |
|---|---|---|---|
| Mode button | `SearchModeButton` | its text (about 90) | `VcActionButton` labelled with the mode and `IconChevronDown`: `Commits`, `Files` or `Changes`. A click opens the mode menu through the menu layer (D141's reason: a combo box's dropdown is a window outside the capture): `Commits: message, author or SHA`, `Files: a path or file name`, `Changes: text added or removed`. The mode is kept per repository for the session |
| Text box | `SearchBox` | the rest, at least 120 | Placeholder by mode: `Message, author or SHA`, `File name or path (case ignored for A–Z)`, `Text added or removed (case ignored for A–Z)` |
| Count | `SearchCount` | as its text needs, at most 110 | Secondary text, the first that holds: empty while the box is empty; `Searching…` while a search runs and found nothing yet; `{n} so far…` (`1 so far…`) while it runs with matches; `No matches`; `{k} of {n}` while the selected commit is the k-th match in row order; `{n} matches` (`1 match`) |
| Previous, next | `SearchPrevious`, `SearchNext` | 22 each | `VcIconButton`s with `IconChevronUp` and `IconChevronDown`, tooltips `Previous match (Shift+Enter)` and `Next match (Enter)`; disabled without matches |
| Close | `SearchClose` | 22 | `VcIconButton`, `IconClose`, tooltip `Close the search (Esc)` |

At 1100×700 (440 wide) the parts take about 90 + 6 + box + 6 + count + 6 + 22 + 6 + 22 + 6 + 22 + 16;
when the area is too narrow for the box's 120 the count is cut with "…" (its whole text in the
tooltip), then hidden; the close button never moves out. While a Changes search runs, a 2-px
determinate progress bar (`SearchProgress`, `VcAccentBrush`) lies along the bar's bottom edge, its
help text and tooltip `Searched {k} of {n} commits`.

Opening the bar puts the keyboard focus in the box with its text selected. The box searches as the
user types, 300 ms after the last key (Commits) or 600 ms (Files, Changes), and at once on Enter when
nothing ran for the text yet. A new text or mode cancels the search that runs. An empty box clears
the marks. Enter in the box goes to the next match, Shift+Enter to the previous one: "after" and
"before" are in row order from the selected row (or from the top when nothing is selected), wrapping
round; the match is selected as a click selects it (the right panel shows it) and scrolled into view
(`ScrollIntoView`, not centred, so the eye can follow). Esc in the bar closes it
(`EscapeOrder.Search` = 390, before the diff's 400; the diff's handler declines in a text box
anyway). Closing clears the marks, keeps the selection, and gives the focus to the graph.

### 3.2 What each mode matches

The search covers exactly the rows the graph shows (with section 8's filter applied, so hidden
branches are not searched), commits and stashes: git is given the rows' SHAs, not the graph's refs
(0.3), through `IGitRepository.SearchAsync(mode, text, shas, onMatch, token)`, which runs
`git log --no-walk=unsorted --stdin --no-show-signature --no-color --encoding=UTF-8 --format=%H <mode options>` with
`GIT_OPTIONAL_LOCKS=0` (`--encoding=UTF-8` because git matches `--grep` against the message converted to the
user's `i18n.logOutputEncoding`), the first 2,000 rows' SHAs on stdin, then 20,000 per call, one call after
another, each match handed over as git prints it. While the graph still loads, the rows loaded so
far are searched and the later ones as they come. Cancelling stops the running call and starts no
other.

| Mode | Matches | How |
|---|---|---|
| Commits | the whole message contains the text; or the author's name or e-mail contains it; or the SHA starts with it (4 to 40 hex digits). Case is ignored | message: git with `-F --regexp-ignore-case --grep=<text>`; author and SHA: in memory over the rows (`%aN`/`%aE` already apply the mailmap, as git's `--author` does); the union |
| Files | the commit's own change (against its first parent, as the right panel lists it) touches a path containing the text; `\` in the text is read as `/`; case is ignored for the letters A to Z (git's `icase` folds ASCII only: the placeholder says so) | `--format=%x1e%H --first-parent --root --no-renames --name-only -- ":(glob,icase)**/*<t>*" ":(glob,icase)**/*<t>*/**"`, `<t>` with `*?[\` escaped, with `GIT_LITERAL_PATHSPECS` removed for this call (as `AllIgnoredAsync` removes it); a commit matches when a path line follows its own line (Git 2.55.0 ignores the path under `--no-walk`, 0.3) |
| Changes | the commit's own change adds or removes the text (the number of its occurrences changes, O7); case is ignored for A to Z | `--first-parent --diff-merges=first-parent --root --find-renames --no-textconv -s --regexp-ignore-case -S<text>`; when git refuses `--diff-merges` (older than 2.31), the call runs again without it and the repository leaves it out from then on, judged by git's error, not its version (`--first-parent` implies it from 2.29). Never with an empty text |

A match whose SHA is not a row (none should come) is dropped and logged at Debug. A call that fails
with `fatal: bad object` (the graph is older than a `gc` that pruned) stops the search quietly; the
next refresh runs it again. The Changes mode can take tens of seconds on a large history (0.3); the
graph stays usable meanwhile.

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

With no search active the control draws exactly as today. Lookups are a hash set per visible row,
within D50's frame budget.

### 3.4 Results and reloads

Matches arrive as SHAs while git prints them and are applied in batches at most every 100 ms; the
count follows. The results of the git-run part (message, files, changes) are kept by SHA, matched or
not, for the current mode and text: a commit's message, its first-parent change and its pickaxe
result never change. When the graph is swapped after a refresh (an operation, an outside change, a
fetch that brought commits), the marks and the count stay; the author and SHA matches are made again
in memory; and git is given only the new graph's rows not searched yet, in the same chunks, the
count showing `{n} so far…` meanwhile. Only a new text or mode starts over. Closing the tab cancels
the search.

## 4. File history and blame

Both open in place of the graph as pages of the graph area (section 11, D158), from the file menus
and the details panel's "All files" (4.4, 4.5), and from each other.

`IGitRepository.ReadFileHistoryAsync(startSha, path, onRow, token)` runs 0.1's history command and
hands over a `FileHistoryRow` (the commit as the graph's `CommitInfo`, the entry's status, the path
at that commit, the old path of an `R` or `C`) for each commit as git prints it. No merge is listed,
as `git log --follow` lists none (0.1); a file renamed on a branch while another branch edited the
old name misses that branch's edits (git's own limit; the handoff says so). The start is resolved to
a full SHA when the page opens and stays fixed.

`IGitRepository.ReadBlameAsync(sha or null, path, token)` runs 0.1's blame command (no revision for
the working tree) and returns a `BlameResult`: the lines' bytes (Latin-1 strings, as diff lines are)
and, per line, its commit (`BlameCommit`: SHA, author, e-mail, author time and zone, summary, the path
at that commit, `previous`, `boundary`); the zero SHA is "not committed yet". The parse is a
three-state machine (a header, metadata lines, a tab line), checks that the final line numbers run 1
to N, C-unquotes `filename` and `previous`, decodes author and summary as UTF-8, and reads `summary
(<sha>)` as an empty message. `blame.ignoreRevsFile` is honoured; when it names a missing file the
page says `Blame stopped: the git setting blame.ignoreRevsFile names {file}, which does not exist.`

### 4.1 File history

`FileHistoryViewModel` / `FileHistoryView` (`FileHistory`). It lists the commits that touched the
file, newest first, following renames, starting from HEAD for a file of the working tree or the
index, and from the commit for a commit's file.

- **Header** 36 (`FileHistoryHeader`): `IconHistory`, `History of {name}` (`FileHistoryTitle`), the
  folder in the secondary colour, `from {short}` when it starts at a commit other than HEAD, then
  on the right `Blame` (`FileHistoryBlame`, a `VcActionButton`: the blame of the selected row's
  version) and the close button (`FileHistoryClose`).
- **The list** (`FileHistoryList`), rows 26 (`FileHistoryRow`, automation name `{short} {subject}`),
  left to right: the status letter in the changed-file list's colours (`A`, `M`, `D`, `R`, `C`,
  `T`), the short SHA in the code font, the subject, then in the secondary colour `renamed from {old
  path}` (an `R` row), `copied from {path}` (a `C` row), `added` or `deleted` (so a file deleted and
  added again reads as two lives), or `as {path}` on a row whose path differs from the one the history
  opened for; the author; the date (`DateDisplay`). When the width runs short the date goes first,
  then the author, then the secondary text; the subject keeps at least 120 and is cut with "…", its
  whole text in the tooltip.
- **Selecting a row** loads its change into the diff part. Only the user's click or key on a row
  also shows that commit in the right panel and selects it behind the page (11.3); the first row,
  selected when the list loads, fills only the diff part, so the right panel keeps what it showed
  (the stage panel, when the page was opened from it).
- **The diff part** (`FileHistoryDiff`): the file's diff at the selected commit against its first
  parent, with the paths at that commit (0.1), through phase 2's `DiffView` with its own
  `DiffViewModel`, reused for every row (`LoadAsync` per row) and hosted by the page through its own
  `IDiffHost`, which reads through the repository and has no write action. Two new properties keep it
  from acting as the main diff: `DiffViewModel.ShowsClose` false (no `CloseDiffButton`: the page has
  its own close) and `DiffView.TakesFocusOnOpen` false (the focus stays in the list, so Up and Down
  walk it). It has no context menu, on purpose: the row's menu holds the commit's actions. Inline
  and Side by side follow the setting.
- **Layout**: list and diff side by side when the page is at least 760 wide (list 45 %, diff 55 %),
  otherwise the list above (40 % of the height, at least 3 rows) and the diff below: at 1100×700
  the graph area is 440 wide, at 1920×1080 it is 1260.
- **Rows' menu** (`ForFileHistoryRow`): `Show in graph` (closes the page, selects and reveals the
  commit; disabled `This commit is not in the graph` when no shown ref reaches it), `Blame at this
  commit`, `Copy SHA`. A double-click on a row does `Show in graph`.
- **Loading**: rows come as git prints them; `Loading…` (secondary) until the first one; `No commit
  has touched {name} yet` when git lists none; a failure shows git's message in the list's place.
  Closing the page cancels the read.

### 4.2 Blame

`BlameViewModel` / `BlameView` (`Blame`). It shows the file with, for each line, the commit that last
changed it: at a commit for a commit's file, in the working tree for a file of either stage list
(lines staged or not are "not committed yet").

- **Header** 36 (`BlameHeader`): `IconBlame`, `Blame of {name}` (`BlameTitle`), `at {short}
  {subject}` or `in the working tree` in the secondary colour, then `File history` (`BlameHistory`,
  the file's history from the same commit) and the close button (`BlameClose`).
- **The text** (`BlameText`): a read-only `CodeTextEditor` (phase 2's base) in the code font at 12
  (a line 15.84 high, as the diff's), with the diff's syntax colours for the file's type
  (`DiffSyntax`), line numbers, and no caret; its automation peer is a Document whose value is the
  text, as the resolver's result's is.
- **The gutter** (`BlameGutter`, a control element of its own), left of the line numbers, 200 wide
  while the page is at least 700 wide and 130 below (then the author moves into the tooltip): lines
  are grouped in runs of consecutive lines from one commit; the first line of a run shows the short
  SHA (code font), the author (cut with "…") and the date (`yyyy-MM-dd`), the run's other lines
  nothing; runs alternate between `VcPanelBackgroundBrush` and the window's background so a run's
  extent is visible; a line not committed yet shows `Not committed yet` in `VcWarningBrush`.
  Hovering a run shows its whole commit in a tooltip: the SHA, the author and e-mail, the date, the
  subject, and `as {path}` when the line came from another path.
- **Click-through** (D158): a click on a run, in the gutter or on its text, selects that commit in the
  graph behind the page and the right panel shows it (11.3); the run's lines get `VcSelectionBrush`
  in the gutter. A double-click does `Show in graph`. The gutter's menu (`ForBlameRun`): `Show in
  graph`, `Blame before this commit` (the blame at the commit's first parent, of the path it had
  there, from `previous`: how one goes further back; disabled `This is the file's first commit` on a
  run without `previous`), `File history`, `Copy SHA`. On a run not committed yet the menu has only
  `Show in graph`, which selects the working-changes row.
- **Loading**: `Loading…` (secondary) in the text's place until git ends (blame prints only at its
  end: 0.4 s for 3,000 lines, 1.9 s for an old line of the 100k repository); closing the page cancels
  it.
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
`ForStagedFile`, `ForCommitFile`, `ForDiff`), as items of D92's kind: left out where they cannot
apply, disabled where the reason helps. The conflict resolver's header menu (which is
`ForUnstagedFile`) leaves them out: opening a page would close the resolver.

| Place | File history from | Blame of |
|---|---|---|
| Unstaged or staged list, a modified, renamed or type-changed file | HEAD, the path in HEAD (a staged rename: its old path) | the working tree (lines staged or not are "not committed yet") |
| A file deleted in the working tree | HEAD | HEAD's version, titled `at HEAD` |
| An untracked file, a staged new file | disabled: `This file is in no commit yet` | disabled, the same |
| A nested repository | left out | left out |
| A conflicted file (the stage list) | HEAD (empty for `AA`: `No commit has touched {name} yet`) | the working tree when the file exists (merge-aware, 0.1), else HEAD's version, else disabled |
| A commit's file (`FileMenuTarget.CommitSha`), a commit's diff, a file of "All files" (4.5) | that commit, the path at it | that commit; a file the commit deleted: its first parent, titled `before {short}` |
| A stash's file | the stash's base (`^1`); the stash row's details show the stash itself | the stash commit |
| A row of the history page | — | `Blame at this commit`: that commit and the path at it (a `D` row: its parent) |
| An unborn repository | disabled: `Make the first commit first` | disabled, the same |

Plan.md's "Done when" (any line of any file traced to its commit in two clicks): with "All files"
on, a right-click on any file of the shown commit and `Blame`, or a click on an unchanged file (4.5).

### 4.5 All files (D164)

The details panel's file list (`RightPanelView`) gains a third button, `All files`
(`AllFilesButton`, a `FileListToggle` beside Flat and Tree, class `selected` while on). While it is
on, the list shows every file of the shown commit's tree (`IGitRepository.ReadTreeFilesAsync(sha)`:
`ls-tree -r -z --name-only <sha>`, read with the details): the commit's changed files with their
status letters as today, and the unchanged ones without a letter, in the Flat or Tree arrangement the
setting chooses; the title reads `All files ({N})`. It is a setting, `AppSettings.ShowAllFiles`
(default false), so it stays as the user left it. A click on a changed file opens its diff as today
(D63); a click on an unchanged file opens its blame at that commit (a page, 4.2). Every row's menu is
`ForCommitFile`, with File history and Blame. A stash's details and the comparison's list (6.4) have
no `All files`. With the button off, the panel is as in phase 1, apart from the button itself.

## 5. The reflog and backups view

### 5.1 Where it opens

A page of the graph area (section 11), `ReflogViewModel` / `ReflogView` (`Reflog`), opened on HEAD's
reflog by `Show reflog`, on the backups by `Show backups`, both in menus that always exist: the Local
branches section's menu (`ForSection`), the empty graph area's menu and the working-changes row's
menu; and on a branch's reflog by `Show reflog of {b}` in a local branch's menu. The page's header
has a selector (`ReflogSource`, a `VcActionButton` with the source's name and `IconChevronDown`,
opening a menu through the menu layer) to switch between `HEAD`, each local branch, and `Backups`,
and the close button (`ReflogClose`).

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
between the calls) both are read once more, and if they disagree again the read fails with "the reflog
changed while it was being read" and the page's next refresh reads again. A missing reflog (a deleted branch, an unborn HEAD) is no
rows. Then `git rev-list --ignore-missing --stdin --not --branches --tags --remotes HEAD --` with the
rows' distinct SHAs marks which are lost; it runs after the list shows and fills the column in. The
page reads again on every refresh while it is open (some entries change no ref target: `checkout:
moving from main to main`), and never while it is closed.

Rows 26 (`ReflogRow`, automation name its selector), newest first, in git's order (the tests' app
commits all share 2026-01-02 09:00, so time cannot order them):

| Part | Content |
|---|---|
| Selector | `HEAD@{n}` or `{b}@{n}`, secondary, code font |
| Time | the entry's own time, `yyyy-MM-dd HH:mm` (`DateDisplay`) |
| Action | git's reflog message, e.g. `checkout: moving from main to feature/alpha`, `commit (amend): Fix the parser` |
| Commit | the entry's commit: short SHA and subject |
| Lost | `IconAlert` in `VcWarningBrush` with the tooltip `No branch, tag or remote branch reaches this commit` when that holds |

When the width runs short the time goes first, then the selector; the action and the commit are
`TrimmedTextBlock`s (cut with "…", the whole text in the tooltip).

Selecting a row shows its commit in the right panel (11.3). The rows' menu (`ForReflogEntry`):
`Create branch here…`, `Check out this commit`, `Reset {cur} to this commit…`, `Cherry-pick onto
{cur}`, `Show in graph` (disabled when the graph does not have it), `Copy SHA`; each is phase 3's
operation with its own question and backup, so nothing new restores; their disabled reasons come
from the row (its parents, lost or not, HEAD at it), not from the graph. A double-click does `Show
in graph`. A reflog that git keeps none of says `{name} has no reflog`. When git skipped entries (their
numbers are missing: a commit that no longer exists, or the empty entry a rename of the checked-out
branch writes), a last line (`ReflogFooter`) says `1 entry is not shown: it names no commit that
exists` / `{N} entries are not shown: they name no commit that exists`. Each restore action checks
inside its operation that the commit still exists (`cat-file -e <sha>^{commit}`), since a `gc` may
have run since the read. The page's empty state explains git's rules: `Git keeps a reflog entry for 90
days, and for 30 days when no branch holds its commit.`

The source menu lists `HEAD`, the local branches (by name) and `Backups`; remote-tracking reflogs are
left out (they start only at the first fetch and show what fetches did, which the activity log shows
better), and so is `refs/stash` (its entries are the stash list the graph already shows).

### 5.3 Backups

The `Backups` source lists the backups under `refs/visualcommit/backup/` that the clean-up has left
(D139), newest first, one row per backup (one operation's refs, phase 4's grouping), read with a new
`IGitRepository.ReadBackupsAsync()` built on phase 4's parser (`BackupNamePattern`):

| Part | Content |
|---|---|
| Time | from its name (UTC), shown as `yyyy-MM-dd HH:mm:ss` in `DateDisplay`'s zone |
| What | the backup in words, by kind (12.3) |
| Commit | the commit it keeps (the first ref's), short SHA and subject |

Rows 26 (`BackupRow`, automation name the backup's name); the words and the subject are cut with
"…" when the width runs short, their whole text in the tooltip. Its menu restores by kind
(`ForBackup`, 7.10 lists each) and, for every backup that holds a commit, offers `Create branch
here…`, `Check out this commit`, `Copy SHA`. These share the effects of the undo steps that do the
same (7.3).

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
current branch (or B), `To` the branch the menu is on (or A). Opening one leaves the working-changes
row when it was selected (`IsWorkingRowSelected = false`; the stage panel's draft stays in memory),
so that the details panel, which holds the comparison, shows. It stays until the user closes it
(`CompareClose`), selects a single commit or the working row, or opens another comparison. Closing it
shows the details of the graph's single selection, or the placeholder when there is none, and gives
the focus to the graph.

### 6.3 What is compared

- **Two commits**: their trees, `From` against `To` (two-dot): the user picked two snapshots. For
  commits on one line of history it is the same as "what happened between them".
- **Two branches** (or a tag): by default the changes on `To` since it left `From` (three-dot: the
  merge base against `To`, as a pull request shows them), with a switch `All differences` for the two
  trees. Two-dot would show `From`'s own later changes reversed (0.3).
- Commands (`IGitRepository.CompareAsync(from, to, threeDot, token)`): the names resolved to SHAs
  first and kept, so that refs moving meanwhile change nothing; for three-dot `git merge-base <from>
  <to>` (exit 1: no common history: three-dot is not offered, `No common history: showing all
  differences`; several bases: the one git prints, and `Several merge bases: changes since
  {short}`); then `diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id <old>
  <new>` with two SHAs (`<old>` the merge base in three-dot, `From` otherwise), parsed by
  `ParseNameStatus`; and for branches `rev-list --left-right --count <from>...<to>` beside it. Git's
  `rename detection was skipped` warning adds `Renames are shown as removed and added files.`
- The same commit on both sides: `Both are {short}: nothing to compare`, without git.

### 6.4 The view

Header 36 (`CompareTitle` `Compare`, `CompareSwap` with `IconArrowDown` and `IconArrowUp`,
`CompareClose` for a branch comparison), then:

- `From {short} {subject}` and `To {short} {subject}` (`CompareFrom`, `CompareTo`), each a link that
  **reveals** that commit's row in the graph without selecting it (a new `Reveal(sha)` that raises
  `RevealRequested` and leaves the selection and the comparison alone; disabled, with the tooltip
  `This commit is not in the graph`, when the graph lacks it); for branches the names: `From main`,
  `To feature/alpha`, the counts (`CompareCounts`) `{a} commits only on {To}, {b} only on {From}` (`1
  commit only on …`), and the switch (`CompareAllDifferences`, a check box `All differences`);
- `Changed files ({N})` with Flat and Tree (the setting of D44), and the file list
  (`CompareFileList`, phase 1's `ChangedFileList` and rows);
- a click on a file opens its diff in the graph area (phase 2's diff view, `DiffTarget(Commit, path,
  kind, oldPath, Commit: To, Parent: <old>)`, `<old>` being the side the list was computed from: the
  merge base in three-dot, `From` otherwise; the old path only from the list's own `R` or `C`
  entry), with the side label `{old short}..{to short}`; the row keeps its selection while the diff
  is open; Esc closes the diff;
- the files' menu (`ForCompareFile`): `File history`, `Blame` (at To), then `Open file`, `Show in
  folder`, `Copy path`.

`Loading…` (secondary) shows under the header until the list is read; the counts fill in when
ready. `No differences` when the two trees are the same.

## 7. Undo and redo (D154, D155, D163, D165, D166)

### 7.1 The history

Each worktree of a repository has one `UndoHistory` for the app's session (`VisualCommit.Core.Undo`,
pure, tested without git): a list of entries and a cursor between them. Undo applies the entry before
the cursor backwards and moves the cursor back; Redo applies the entry after it forwards and moves
it on; a new recorded action drops every entry after the cursor, then is added. `AppSession` keeps
the histories in a dictionary keyed by the worktree's own git folder (`IGitRepository.GitDirectory`,
`.git/worktrees/<n>` for a linked worktree; full and normalised as `TabState`'s path): HEAD, the
index and the files belong to a worktree, so two tabs on two worktrees of one repository never share
a history, while closing a tab and opening the same worktree again in the session keeps its history.
Two tabs on the same worktree share one. Nothing is written to disk or into the repository. At most
100 entries are kept per worktree, the oldest dropped first: the history is for recent mistakes, and
the bound keeps the backups the history holds back from the clean-up (7.8) bounded.

An **entry** (`UndoEntry`): the action's kind and text (7.6), the operation id it was recorded in,
its recorded values (the before and after states 7.3's sequences need), the backup refs it needs
(7.8), and its state: `Done`, or `Pending` while the action it records is stopped (a merge, a pick, a
revert or a rebase with the banner showing).

### 7.2 What is recorded, and how each destructive action is undone

| Recorded (an entry each) | Not recorded |
|---|---|
| Commit and amend (the stage panel's Commit), checkout of every kind (a local branch, a remote branch into a tracking branch, a commit or tag, the automatic stash's checkout of 9.2, D91's "Stash changes and check out"), create (with or without checkout), rename and delete a branch, fast-forward a branch, merge (both kinds, the drop's), "Check out and merge", reset (soft, mixed, hard), cherry-pick and revert (one or a list), rebase onto, "Check out and rebase", interactive rebase, squash, pull (fast-forward, merge, rebase), stash, apply, pop, drop, create and delete a tag, discard (a file, a hunk, lines, all), the restore bar's Restore, ignore a file or an extension, set and stop tracking an upstream, add, rename and remove a remote, change a remote's URL, push a branch, force push, delete a branch on a remote, push a tag, delete a tag on a remote | Stage, unstage and applying lines to the index (the preparation of a commit, undone with one click in the stage panel; recording each would bury the actions people undo; GitKraken leaves them out too); fetch and auto-fetch (they change only remote-tracking refs, which no entry relies on except through a lease); the backups' clean-up; the conflict resolver's resolutions and the merge tool (they happen inside a stopped operation, during which undo is off, 7.4); Abort (D165: undone from the backups list); open, show, copy; undo and redo themselves |

- An action that **changed nothing** (refused, up to date, `Everything up-to-date`, nothing to stash)
  records nothing.
- An action that **stopped** (conflicts, an `edit` stop) records a `Pending` entry with its
  before-state. Continue, Skip and the stage panel's Commit that end the operation complete it with
  the after-state (one entry from before the action to the final result: a rebase with three stops
  is one Undo); Abort removes it (D165); an operation that ends outside the app (a terminal) removes
  it at the next refresh. A Commit that ends an operation the app has no pending entry for (one
  started in a terminal) records nothing.
- **Failed half-way** (a "Check out and merge" whose merge failed): what changed is recorded (the
  checkout), because the effects come from what was read, not from what was meant (7.7).

Every destructive action of phases 2 to 4 (D62, D83, D127, D142, D144, D148) and how it is undone:

| Destructive action | Undone by |
|---|---|
| Discard (file, hunk, lines, all) | Undo (7.3, discard), or the restore bar, or the backups list's `Restore the discarded files…` |
| Reset (each mode) | Undo, or the backups list (`reset-…`: the commit; `-changes`: 7.10's restores) |
| Delete a branch; delete a branch on a remote; remove a remote | Undo, or the backups list (`Restore branch {b}`; `Push {b} back to {r}…`; each remote branch's commit) |
| Force push | Undo (asks), or the backups list's `Push {b} back to {r}…` |
| Delete a tag; delete a tag on a remote | Undo, or the backups list (`Restore tag {t}`; `Push {t} back to {r}…`) |
| Drop a stash | Undo, or the backups list's `Restore stash` |
| Leaving a detached HEAD's commits | Undo of that checkout, or the backups list (`detached-…`: create a branch or check it out) |
| Rebase of every kind (onto, interactive, squash, a pull's) | Undo, or the backups list (`rebase-…/<b>`: the old tip) |
| Abort (merge, cherry-pick, revert, rebase, `git am`) | The backups list (D165): `Restore the changes…` from `abort-…-changes` and the commit of `abort-…`; the stopped operation does not come back |
| Skip | Undo of the action it belongs to (the pending entry it completes), or the backups list (`skip-…`, `-changes`) |
| The resolver's "Use ours/theirs" on a binary file, "Delete it" | The backups list's `Restore the discarded files…` (their `discard-…` snapshots) |

### 7.3 The sequences

An entry is undone and redone by a **sequence of effects** fixed per action kind, not by a generic
reverse of what happened: the order of a step matters (a branch cannot be deleted while it is checked
out; a hard reset's files must come back after its move), and one git call can change several
things at once. `UndoSequence` (Core) names each kind's undo and redo sequences; `GitRepository.Undo.cs`
runs the effects. The effects:

| Effect | What it runs | Its check (against the state the earlier effects of the step leave, 7.5) |
|---|---|---|
| `MoveHead(branch, from, to, Soft)` | `update-ref -m <msg> refs/heads/<b> <to> <from>` (the initial commit's undo: `update-ref -d refs/heads/<b> <from>`; its redo: `update-ref refs/heads/<b> <to> ""`) | HEAD is on `<b>` (`symbolic-ref -q HEAD`) and `<b>` is at `<from>` |
| `MoveHead(branch, from, to, Keep)` | `update-index -q --refresh` (git's own checkout refreshes first; without it an unchanged file with stale stat data refuses), then `read-tree -m -u <from> <to>`, then that update-ref; when the update-ref fails, `read-tree -m -u <to> <from>` puts the files back and the step is refused | as Soft, and the **ignored-files guard**: no ignored file or folder lies where `<to>` adds a file (the paths of `diff-tree -r -z --no-renames --name-only --diff-filter=A <from> <to>` and their parent folders, checked on disk with `check-ignore`): `read-tree` would overwrite or delete them without a word, on every git from 2.34 |
| `MoveDetachedHead(from, to, mode)` | as `MoveHead` on `--no-deref HEAD` | HEAD detached at `<from>` |
| `SetRef(ref, from, to)` (either may be absent) | `update-ref --stdin` with `update`, `create` or `delete` and the expected value | the ref is at `<from>`; a branch is checked out in no worktree (`%(worktreepath)`) |
| `Switch(from, to)` (a branch's short name, or a SHA) | `switch --no-guess --no-overwrite-ignore <b>` / `switch --detach --no-overwrite-ignore <sha>` (never with an automatic stash, 9.1) | HEAD is exactly `<from>`; `<to>` exists |
| `RenameBranch(from, to)` | `branch --move <from> <to>` | `<from>` exists, `<to>` does not (a name differing only in case on Windows and macOS: refused, as D131) |
| `BranchConfig(b, from, to)`, `RemoteConfig(r, from, to)` | the keys of `<to>` written with `config --local` (`--add` in order for a remote's), the others unset | the keys equal `<from>` |
| `RemoteRename(from, to)`, `RemoteUrl(r, from, to)` | `remote rename`, `remote set-url --` | `<from>` exists and `<to>` does not; one `url` value |
| `IndexTo(source, from)` | `restore --staged --source=<source> -- .` | the index equals `<from>` (`diff-index --cached --quiet <from>`) |
| `FilesTo(paths, bytes)` | each path's recorded raw bytes written back (`cat-file blob` of a blob saved with `hash-object -w --no-filters`), or the file deleted when it was absent, folders left empty removed | each path's SHA-256 equals the recorded state being left |
| `RestoreSnapshot(snapshot)` | `RestoreSnapshotAsync` | the snapshot's paths' SHA-256 equal the recorded state being left |
| `StashPush(options, message)` | `stash push [--include-untracked] [--message <m>]` without `GIT_LITERAL_PATHSPECS`; **the entry takes the new stash's SHA** | the whole status equals the recorded one |
| `StashPop(sha)` | `stash pop --index stash@{n}` (found by SHA) | the SHA is listed and exists (`cat-file -e`); the whole status equals the recorded one; no untracked path of the stash (`ls-tree -r -z --name-only <sha>^3`) exists |
| `StashStore(sha, message)` / `StashDrop(sha)` | `stash store -m "<message>" <sha>` (it comes back as `stash@{0}`) / `stash drop stash@{k}` by SHA | not listed / listed; the commit exists |
| `IndexEntries(paths, entries)` | `update-index -z --index-info` with a `0 <zeros> 0\t<path>` line, then the path's recorded entries (unmerged stages included) | the paths' current entries (`ls-files --stage -z`) equal the state being left; no path intent-to-add or skip-worktree |
| `TagRef(name, object, present)` | `update-ref refs/tags/<t> <object> ""` / `update-ref -d refs/tags/<t> <object>` | the tag is absent / at the object |
| `GitignoreBytes(existed, lengthBefore, appended)` | append the bytes / truncate to the old length, or delete a file it created | the file's SHA-256 equals the state being left; not a symbolic link |
| `RemoteRef(remote, ref, from, to)` | `push --progress --force-with-lease=<ref>:<from, or empty> <r> <to>:<ref>` or `--delete <ref>`; the local remote-tracking ref follows | the lease (git's own check); `Everything up-to-date` on a push that must create counts as done |

The sequences (undo first; redo is the mirror unless the table says otherwise; `B` the branch, `O`
its tip before, `N` after):

| Action | Undo | Redo |
|---|---|---|
| Commit, amend, soft reset | `MoveHead(B, N, O, Soft)` (the initial commit: delete) | `MoveHead(B, O, N, Soft)` |
| Mixed reset | `MoveHead(B, N, O, Soft)`, then `IndexTo(<snapshot> or O, N)`: the index snapshot the reset saved, or `O` itself when the index equalled HEAD and none was saved | `IndexTo(N, <the source>)` (the index only), then `MoveHead(B, O, N, Soft)` |
| Hard reset | `MoveHead(B, N, O, Keep)`, then `IndexTo(<snapshot>^2, O)` (after the move the index is at `O`), then `RestoreSnapshot(<snapshot>)` (in this order: restored files would make the move refuse) | the app's own hard reset to `N` with a **new** snapshot (the files may have changed since the undo), checked by HEAD on `B` and `B` at `O`; **the entry takes the new snapshot** |
| Merge, fast-forward, cherry-pick, revert (one or a list), rebase onto, interactive rebase, squash, pull, a finished stopped action | `MoveHead(B, N, O, Keep)`; for a plain rebase also `SetRef` back for every other branch git moved (a user's `rebase.updateRefs`, 7.7) | the mirror |
| Checkout of a branch, or of a commit or tag (no automatic stash) | `Switch(now, before)` | the mirror |
| Checkout that created a tracking branch; create a branch and check it out | `Switch(B, before)`, then `SetRef(refs/heads/B, tip, absent)`, then `BranchConfig(B, written, none)` | `SetRef(refs/heads/B, absent, tip)` and `BranchConfig(B, none, written)` (`switch --create … --track` would fail with a SHA), then `Switch(before, B)` |
| Create a branch, no checkout | `SetRef` delete, `BranchConfig` removed | the mirror |
| Rename | `RenameBranch(new, old)` | the mirror |
| Delete a branch | `SetRef(refs/heads/B, absent, tip)` with `--create-reflog`, `BranchConfig(B, none, before)` | the mirror |
| Fast-forward a branch not checked out | `SetRef(refs/heads/B, N, O)` | the mirror |
| "Check out and merge", "Check out and rebase" (and the list's checkout first) | `MoveHead(B, N, O, Keep)` while HEAD is still on `B`, then `Switch(B, before)` | `Switch(before, B)`, then `MoveHead(B, O, N, Keep)`; when that move refuses after the switch, `Switch(B, before)` and the step is refused (9.3's text) |
| D91's "Stash changes and check out" | `StashPush` (tracked files only, as D91), `Switch(target, before)`, `StashPop` | the mirror; a pop that conflicted when the action ran records no entry (its stash is kept and named, D91) |
| The automatic stash's checkout (9.2, D166) | `Switch(target, branch)`, then `StashPop(sha)` | `StashPush(--include-untracked, its message)`, then `Switch(branch, target)`; when the switch refuses, `StashPop` back and the step is refused |
| Stash | `StashPop(sha)` | `StashPush(options, message)`; the entry takes the new SHA |
| Apply | `IndexEntries` and `FilesTo` back to the state before | forward to the state after |
| Pop | `StashStore(sha, message)` first, then `IndexEntries` and `FilesTo` back (so a failure leaves the changes in the working tree) | `IndexEntries`/`FilesTo` forward, then `StashDrop(sha)` |
| Drop | `StashStore(sha, message)` | `StashDrop(sha)` |
| Create a tag | the object kept in a `tag-undone-<time>/<t>` backup, then `TagRef` delete | `TagRef` create (the same tag object) |
| Delete a tag | `TagRef` create | `TagRef` delete |
| Discard of a file, a hunk, lines, all | `RestoreSnapshot(snapshot)` | `FilesTo(paths, the bytes after the discard)`: a hunk or line discard keeps the file's other changes, which `restore --worktree` would throw away |
| Restore (the restore bar) | `FilesTo(paths, the bytes before the restore)` (or the bar's own snapshot when it saved one) | `RestoreSnapshot` again |
| Ignore | `GitignoreBytes` back | forward |
| Set or stop tracking an upstream | `BranchConfig(B, after, before)` | the mirror |
| Add, rename, remove a remote; change its URL | `RemoteConfig` and the remote branches' `SetRef`s from the `remote-…` backup; `RemoteRename`; `RemoteUrl` | the mirror |
| Push of a branch (new or updated, with or without set-upstream), force push, delete a branch on a remote, push a tag, delete a tag on a remote | `RemoteRef(r, ref, after, before)` **first** (the only effect that can be refused by another machine), then `BranchConfig` | the mirror |

Messages for `update-ref -m`: `VisualCommit: undo <text>` and `VisualCommit: redo <text>`, so the
reflog page tells them apart (0.2). `switch` keeps git's own message, so `@{-1}` keeps working.

### 7.4 When Undo and Redo are enabled

Undo is enabled when the entry before the cursor is `Done` and its checks pass against what the app
last read; Redo likewise for the entry after it. The checks that need no git (HEAD, branch tips,
tags, stashes, the staged files, the worktrees' branches, the remotes) are made in memory after
every refresh and kept; the rest (file hashes, index entries, the ignored-files guard, the lease) only
inside the operation. The first reason that holds, in this order:

1. A user operation of the repository runs or waits: `Wait for {title} to finish`. Evaluated again
   on every `Changed` event of the queue (raised when an operation starts, waits, and after `Finish`
   clears its slot), so that the state after an action shows at once; `RepositoryViewModel`
   subscribes, posts to the UI context and raises the property the toolbar reads. A background
   operation (auto-fetch, the clean-up) gives no reason: a click queues behind it.
2. An operation is in progress (the banner shows): `Finish or abort the {kind} first` (D119's
   texts); conflicts without one: `Resolve the conflicts first`, except when the entry before the
   cursor is an apply or a pop whose recorded paths hold every conflicted path (its undo restores
   them as they were).
3. The interactive rebase's list is open: `Close the interactive rebase first` (it holds work, and
   moving its branch would disable its Start).
4. No entry: `Nothing to undo` / `Nothing to redo`.
5. The entry's in-memory check fails (7.6's texts), e.g. `main has moved since (it is at 9f8e7d6)`.

The checks are strict in order: an entry that cannot be undone blocks the older ones (undoing them
around it would apply effects to a state they were not recorded on); the backups list (5.3) is the
way past it. Outside changes never remove entries: an unrelated change (a fetch, a commit on another
branch) leaves the entry undoable; a conflicting one makes it refuse until the state is back.

### 7.5 Running a step

Undo and Redo each run as one operation of the repository's queue (titles `Undo {text}` and `Redo
{text}`, refresh `Refs`, prompts `Ask`), **Local**, or **Network** when the sequence has a
`RemoteRef` (it can then be cancelled and prompts for credentials). Inside it, in this order:

1. Refuse while an operation is in progress (the app's own check: `update-ref` refuses nothing,
   0.4) and when HEAD or the entry's refs moved since the click (D124).
2. Read HEAD and the entry's refs (`rev-parse HEAD --symbolic-full-name HEAD`, one `for-each-ref`
   with `%(worktreepath)`), the status, and what the effects check.
3. **Simulate the sequence**: each effect's check is evaluated against the state the effects before
   it in the sequence will leave (HEAD after a `Switch`, a branch no longer checked out after
   switching away, the status after a `StashPush` or `StashPop`), predicted from the recorded values.
   A check that only git can make and that depends on an earlier effect (the two-way merge of a
   `Keep` move after a `Switch`) runs at its place in step 4.
4. Apply the effects in the sequence's order, grouped so that what another machine can refuse comes
   first (`RemoteRef`), then what git can refuse locally (`Switch`, `read-tree`), then one
   `update-ref --stdin` transaction with `verify` lines for every ref write, then config and file
   writes. When an effect refuses after earlier ones were applied, those are reversed in reverse
   order and the step is refused. The only write a refused step may leave is `update-index
   --refresh`'s stat data.
5. When an effect made a new object (a redo's stash, a redo's hard-reset snapshot, an undone tag
   creation's backup), the entry is rewritten with it before the cursor moves.

**Questions** (D155, extended by D170 to every remote change in either direction), one per step,
naming the remote; confirm button `Undo` or `Redo` (`Push` for the backups list), not destructive in
style (they restore):

| Case | Title | Text |
|---|---|---|
| Undo a push that updated a branch (force push included) | `Undo the push to {r}?` | `{b} on {r} goes back to {short}, which it was before the push. Anyone who fetched {short2} keeps it.` |
| Redo it | `Redo the push to {r}?` | `{b} on {r} moves to {short2} again.` |
| Undo a push that created a branch on the remote | `Delete {b} on {r}?` | `Undoing the push deletes {b} on {r}. Anyone who fetched it keeps it.` |
| Redo it | `Push {b} to {r} again?` | `{r} gets {b} back at {short}.` |
| Undo a deletion of a branch on a remote | `Undo the deletion of {b} on {r}?` | `{r} gets {b} back at {short}.` |
| Redo it | `Delete {b} on {r} again?` | `{b} is deleted on {r} again.` |
| The same four for a tag | `… tag {t} …` | `… tag {t} …` |
| The backups list's push back | `Push {b} back to {r}?` | `{r} gets {b} back at {short}.` |

A lease failure shows the push's error dialog with git's output and the summary `{b} on {r} has
changed since: nothing was changed.` (`{t}` for a tag).

Undoing a commit or an amend leaves its changes staged (a soft move), so the stage panel's summary
and description are filled with the undone commit's message when both are empty, as GitKraken does;
redoing it clears them again if they still hold that message (phase 4's rule for filled messages).

A refusal found inside the operation opens the error dialog titled `Undo failed` or `Redo failed`
with the reason as its summary and no git output (D95's failures); a git failure shows git's output
as every operation does. The step's result in the status bar: `Undid {text}` / `Redid {text}`.

### 7.6 Texts

An entry's text names the action as the user knows it:

| Action | Entry text |
|---|---|
| Commit, amend | `commit "{subject}"`, `amend "{subject}"` |
| Checkout (every kind) | `checkout of {name}` |
| Create, rename, delete a branch | `creation of {b}`, `rename of {old} to {new}`, `deletion of {b}` |
| Fast-forward | `fast-forward of {b}` |
| Merge, "Check out and merge" | `merge of {source} into {b}` |
| Reset | `reset of {b} to {short} ({soft|mixed|hard})` |
| Cherry-pick, revert | `cherry-pick of {short}` / `of {N} commits`; `revert of {short}` / `of {N} commits` |
| Rebase onto, "Check out and rebase", interactive, squash | `rebase of {b} onto {target}`, `interactive rebase of {b}`, `squash of {N} commits` |
| Pull | `pull into {b}` |
| Stash, apply, pop, drop | `stash "{message}"`, `apply of stash "{message}"`, `pop of stash "{message}"`, `drop of stash "{message}"` (never `stash@{n}`, which renumbering reuses) |
| Tags | `creation of tag {t}`, `deletion of tag {t}` |
| Discard, Restore | `discard of {N} files` / `of {name}` / `of lines in {name}`; `restore of the discarded files` |
| The backups list's restores (7.10) | `restore of branch {b}`, `restore of tag {t}`, `restore of stash "{message}"`, `restore of the changes`, `restore of the staged changes`, `push of {b} back to {r}`, `push of tag {t} back to {r}` |
| Ignore | `ignore of {pattern}` |
| Upstream | `upstream change of {b}` |
| Remotes | `addition of remote {r}`, `rename of remote {old} to {new}`, `removal of remote {r}`, `URL change of {r}` |
| Pushes | `push of {b} to {r}`, `force push of {b} to {r}`, `deletion of {b} on {r}`, `push of tag {t} to {r}`, `deletion of tag {t} on {r}` |

The toolbar shows `Undo {text}` and `Redo {text}`. Check failures, prefixed by `Can't undo {text}: `
or `Can't redo {text}: ` in the error dialog and shown without the prefix in the tooltip:

| Check | Text |
|---|---|
| HEAD not where it was | `HEAD is no longer on {b}` / `HEAD is no longer at {short}` |
| A branch moved | `{b} has moved since (it is at {short})` |
| A ref moved or exists again | `{ref} has changed since` / `a branch named {b} exists again` / `a tag named {t} exists again` |
| Checked out elsewhere | `{b} is checked out in {folder}` |
| Local changes in the way of a move or a switch | `1 conflict prevents it. Commit, stash, or discard your changes first.` / `{N} conflicts prevent it. …`, the count made by the app: the paths `diff-tree -r -z --no-renames --name-only <from> <to>` lists that the status shows changed or untracked (git's `read-tree` names only the first); git's line goes below the summary |
| Ignored files in the way | `1 ignored file is in the way: {name}. Move it away first.` / `{N} ignored files are in the way: {first names}. …` |
| Index changed | `you have staged changes since` |
| Files changed | `1 file has changed since: {name}` / `{N} files have changed since: {first names}` |
| Stash | `the stash is no longer in the list` / `the stash is in the list again` / `your changes have changed since the stash` |
| Commits gone | `the commits no longer exist` (`cat-file -e` failed: an outside `gc`) |
| A remote | `{b} on {r} has changed since` (the lease) |
| Intent-to-add, skip-worktree | `{name} has a flag git cannot restore` |

### 7.7 Recording, inside the action's own operation

A `Run` delegate must not queue another operation (the queue would deadlock), and recording needs
none: `RepositoryOperations.RunAsync`'s wrapper (where `_backupSaved` is set) and phase 2's
`RepositoryViewModel.WriteAsync` (the stage panel's Commit, discard, Restore) do it around `run`:

1. Each operation names its kind and what it may change through `RunOptions.Undo` (an `UndoScope`:
   HEAD always; the branches, tags, remotes, stash, files or config keys it touches; `None` for the
   unrecorded kinds of 7.2). A plain rebase's scope (rebase onto, "Check out and rebase", a pull
   that rebases) holds every `refs/heads/` ref, so branches a user's `rebase.updateRefs` moves (Git
   2.38 and later) become part of its entry; the app passes no option that changes it (D142).
2. Before `run`, inside the operation: HEAD (`rev-parse HEAD --symbolic-full-name HEAD`, or
   `symbolic-ref -q HEAD` when unborn) and one `for-each-ref --format=%(refname)%1f%(objectname)`
   of the scope's refs (exact names filtered in code: a pattern matches by path component), the stash
   list when the scope has the stash, the config keys when it has config, and the files' raw bytes
   (`hash-object -w --no-filters`) and index entries when it has files (apply, pop, Restore).
3. What the reads cannot see goes through an `UndoCapture` (`AsyncLocal`, thread-safe, because
   `SaveRebaseBackupAsync` runs on a pool thread): a reset's mode and snapshot refs, the stash SHA
   `StashAsync` returns, a push's old remote value (`IGitRepository.PushAsync` and `PushTagAsync`
   return a `PushOutcome` with each updated remote ref's old value, null when new, and new value,
   read from git's summary line; never the remote-tracking ref, which can be stale), and the files'
   bytes after a discard or a hard reset. The `.gitignore` is read by the recorder itself (whether it
   existed, its length and SHA-256) before and after `AppendToGitignoreAsync`.
4. After `run`, still inside the operation: the same reads again. The differences and the capture
   fill the kind's recorded values; no difference, no entry. The reads write nothing
   (`GIT_OPTIONAL_LOCKS=0`), so a refused action still leaves HEAD's reflog as it was (phase 4's check
   A5 counts it).
5. Still inside the operation, on the UI context, the entry is added (`Done`, or `Pending` when an
   operation is now in progress) and the redo part is dropped. It is not carried through
   `OperationReport` (the queue drops fields it does not know).

The reads cost two git calls before and two after an operation: about 0.1 to 0.3 s on the
development laptop. Operations that already read HEAD for D124 share that read.

### 7.8 What the history keeps alive

- Commits left behind by an undo stay in the branch and HEAD reflogs (30 days, 0.4); a redo checks
  `cat-file -e` first.
- Backup refs an entry needs (a reset's `reset-<time>` and `-changes`, `detached-<time>`,
  `rebase-<time>/<b>`, a drop's or a pop's `stash-<time>` (a pop saves one now, so the popped stash
  outlives git's pruning), `tag-<time>/<t>` and `tag-undone-<time>/<t>`, `branch-<time>/<b>`,
  `remote-<time>/<r>/…`, the discards' snapshots) are passed to the backups' clean-up as `keep`
  through `RepositoryViewModel.BackupsToKeep` (today the restore bar's snapshot only): the union over
  every history of the repository's worktrees (they share the backup refs). They still count toward
  D139's 50: a long session keeps more than 50, on purpose.
- An entry dropped from the history (the 100 limit, or a new action after undos) no longer keeps
  them.

### 7.9 The restore bar

The restore bar (D62, D75) stays and is tied to the discard it names, both ways: Undo of that discard
closes the bar (the files are back); Redo of it shows the bar again with the same snapshot. Its
Restore runs as that entry's Undo when the discard is the entry before the cursor (Redo discards
again); otherwise it runs as today and is recorded as an entry of its own (`restore of the discarded
files`). This amends D62's list of when the bar goes.

### 7.10 Restoring from the backups list

The backups list (5.3) restores a backup outside the session's history with the same effects:

| Backup | Restore item | What it runs |
|---|---|---|
| `branch-<t>/<b>` | `Restore branch {b}` (disabled `A branch named {b} exists`) | `SetRef` create (the config is not in the backup: no upstream) |
| `tag-<t>/<t>`, `tag-undone-<t>/<t>` | `Restore tag {t}` (disabled `A tag named {t} exists`) | `TagRef` create |
| `stash-<t>` | `Restore stash` | `StashStore`, with the stash commit's subject as message |
| `discard-<t>` | `Restore the discarded files…` | asks D75's `Restore discarded changes?` when a file would be overwritten, then `RestoreSnapshot` |
| `reset-<t>-changes` of a hard reset, `abort-<t>-changes`, `skip-<t>-changes` (paths in the message) | `Restore the changes…` | the same question, then the files (`RestoreSnapshot`); for a hard reset's, when nothing is staged now, the index from its second parent first |
| `reset-<t>-changes` of a mixed reset (`VisualCommit: index before a mixed reset`, no paths) | `Restore the staged changes` (disabled `Unstage your changes first` while the index differs from HEAD) | `IndexTo(<snapshot>, HEAD)` |
| `remote-branch-<t>/<r>/<b>` | `Push {b} back to {r}…` (disabled `{r} no longer exists`) | `RemoteRef` create (lease: must not exist), after 7.5's question; the remote and branch split by the remote list's longest matching name, as the deletion did |
| `remote-tag-<t>/<r>/<t>` | `Push {t} back to {r}…` | likewise for the tag |
| `force-push-<t>/<name>` | `Push {b} back to {r}…` | the remote and its ref from the upstream that maps to `refs/remotes/<name>`, or from the fetch refspecs; the lease is the commit the force push left, read from the remote-tracking reflog (the `update by push` entry whose older neighbour is the backup's commit); when none of these can be found: disabled, `The remote this backup came from cannot be found` or `The commit your force push left can no longer be found` |
| `remote-<t>/<r>/…` | none of its own | the URL and configuration are not in the backup: each remote branch's commit has the generic items below |
| every backup that holds a commit | `Create branch here…`, `Check out this commit`, `Copy SHA` | phase 3's operations |

Each is one operation titled as its item (without the "…"), recorded in the history like any action.

### 7.11 Tests

`UndoHistoryTests` (Core: cursor, truncation, the 100 limit, pending entries completed and removed);
`UndoSequenceTests` (Core: each kind's sequences and the simulation of 7.5 step 3); `UndoEffectTests`
(Git: every effect forward and back on real repositories; each check's refusal changing nothing but
stat data, compared with `ls-files --stage -z`, the refs and HEAD's reflog; the race of a Keep move;
an unchanged file with stale stat data; an ignored file in the way of an undo and of a redo; the
initial commit; the lease texts on a local bare remote; undo, redo, undo of a stash and of a hard
reset with an edit between, `GIT_COMMITTER_DATE` advanced between steps so the stash's SHA changes);
`UndoRecordingTests` (App: the entry each recorded action of 7.2 leaves; no entry for a refused or
empty action; the pending flow through Continue, Skip and Abort; two worktrees of one repository);
`UndoToolbarTests` (enabled states and tooltips of 7.4 after refreshes, after the queue's `Changed`
without a refresh, and after outside changes; the keys of 1.3 and their declines); and section 15's
checks, which undo and redo each destructive action of phases 2 to 4.

### 7.12 What the git half settled (WP3, merged in `b0e8710`)

Building and reviewing the git half against Git 2.36 and 2.55 corrected or added these; the code's
doc comments and `UndoEffectTests`, `UndoStep*Tests` and `UndoSequenceTests` pin them.

- **A stash is popped back only onto the commit it was made on.** `StashPop` carries `Onto` (the
  head the stash was made at; for a step's own stash, the commit its switch goes to). Its check adds
  "HEAD is at that commit": a commit made since in a terminal leaves the status clean, and the pop
  would conflict (`UU`, exit 1). Refused as `{b} has moved since (it is at {short})` when HEAD is on
  the stash's branch, `HEAD is no longer at {short}` otherwise; another branch at the same commit is
  allowed. The Stash, automatic stash and D91 rows of 7.3 all use it.
- **Folders in the way.** read-tree also refuses with `error: Updating '<d>' would lose untracked
  files in it` (128, both gits). 7.6's count names such a folder once; an untracked file where the
  move puts a folder counts once. A switch blocked by a folder (`Updating the following directories
  would lose untracked files in them`) is checked by the files inside it: when every one is
  ignored, the refusal is 7.6's ignored-files text naming those files; otherwise the folder is local
  work. The Keep move's ignored-files guard lists the files inside a folder at a path the move adds,
  as well as the parent folders. A symbolic link counts as a file, as git counts it.
- **A remote's symbolic `HEAD`.** `git remote remove` deletes `refs/remotes/<r>/HEAD` too, and 2.36's
  `update-ref --stdin` has no symref commands. The reads of 7.7 keep symbolic refs apart
  (`UndoState.SymbolicRefs`, from `%(symref)`), and a new effect `SymbolicRef(ref, from, to)` (the
  closed set of 7.3 gains it) recreates one with `symbolic-ref -m` after the transaction has written
  its target, or deletes one with `update-ref --no-deref -d`; its check is the ref it points at.
  "Remove a remote" uses it both ways.
- **"Add a remote" after "Fetch now".** The undo's `RemoteConfig` that removes the remote also
  deletes, in the step's transaction, every ref under `refs/remotes/<r>/` that no earlier effect of
  the step writes (symbolic ones at once); refs of a remote whose name goes on from this one
  (`mirror/eu` after `mirror`) stay, as git keeps them. The redo leaves the state right after the
  add, without the fetched refs. Git 2.55's `remote add` refuses a name that is a prefix of an
  existing remote's or has one as its prefix; 2.36 allows it.
- **More refusal kinds.** `UndoRefusalKind` gains `Conflicts` (7.4's `Resolve the conflicts first`),
  `ConfigChanged` (`the settings of {b|r} have changed since`), `RemoteExists` (`a remote named {r}
  exists again`) and `GitRefused` (git's own line as the reason); `StatusChanged` may name the
  stash's untracked files that are in the way (`{N} files are in the way: {first names}`). The
  app half (`RepositoryOperations.Undo.cs`) words them; 7.6's table is extended with these texts.
- **Where checks are made.** A hard reset's undo checks the index before anything is written,
  against the commit the move leaves (the move carries the index), not at its place in step 4. The
  exception that an apply's or a pop's own conflicts do not refuse its undo (7.4) is made inside the
  step as well as in memory.
- **A push's old value** comes from git's summary line, whose abbreviation is expanded with
  `rev-parse --disambiguate`; a value this repository does not have leaves that ref out of the
  record, so its undo is not offered.

## 8. Solo and hide (O8, D159)

### 8.1 What can be hidden or soloed

Local and remote branches (O8 says branches; tags stay as they are). The state per repository is two
sets of **full ref names**, `Hidden` and `Soloed` (short names collide across kinds: a tag and a
branch can both be `x`, a local branch can be named `origin/x`), a set of **hidden remotes** (every
branch of the remote, now and later), and for each local branch hidden or soloed with its upstream,
which upstream came with it. A ref is never in both sets: soloing a hidden ref shows it, hiding a
soloed one ends its solo.

- **A local branch's remote-tracking upstream follows it**: `Hide {b}` hides the branch and its
  upstream (`refs/remotes/…` only; a local upstream, D101, never follows), `Solo {b}` solos both;
  otherwise the upstream keeps most of the branch's commits on screen (0.6), and the commits a pull
  would bring stay visible under solo. `Show {b}` and `Stop soloing {b}` undo both, unless the
  upstream was shown or hidden on its own since. The upstream's own row toggles it alone.
- **The checked-out branch cannot be hidden** (`Hide` disabled: `The checked-out branch is always
  shown`). A hidden branch that becomes the checked-out one (a checkout in the app or a terminal) is
  shown while it is, through `HEAD` on git's command line, with its label; its flag stays, and its
  upstream stays hidden by its own entry. It hides again when HEAD leaves it. The load's arguments
  never depend on HEAD's branch, so the first load and later ones agree.
- **Solo mode**: while `Soloed` is not empty, the graph shows the soloed refs' histories and HEAD's;
  several refs can be soloed. `Hidden` is kept and applies again when solo mode ends. A branch the app
  creates while solo mode is on is soloed too, so it does not vanish.
- **A hidden remote** (`Hide every branch of {r}`) hides every branch of that remote, also those a
  later fetch brings: it is expanded to the remote's current branches at each load. `Show every
  branch of {r}` removes the remote and its branches' names.

### 8.2 What the graph loads (`LoadCommitsAsync` takes a `GraphFilter`)

With no filter the command is byte-identical to today's, so every picture, the Q1 measurement and
the real-window pass's `loaded N commits` waits are unchanged. With one (0.6), which amends D47's
single command while a filter is on:

- **Hide**: `--exclude=<b>` … `--branches`, `--exclude=<r>/<b>` … and `--exclude=<r>/HEAD` for every
  remote with a hidden branch, then `--remotes`, `--tags`, `--ignore-missing HEAD --`. Never `^ref`
  (it removes shared history), never `*/HEAD`.
- **Solo**: `--ignore-missing <full name> … HEAD --`.
- **Over 30,000 characters** of arguments (about 800 hidden refs): `--ignore-missing --stdin --` with
  plain full names on stdin (2.30 to 2.41 accept no `--exclude` there): for solo the soloed names and
  `HEAD`; for hide the full names of every visible ref from the refs read, and `HEAD`, so this case
  waits for the refs (about 0.2 s).
- The filter comes from the session (a hidden remote expanded from the session's last known branches
  of that remote, the refs read correcting it at the next refresh), so git starts before the refs are
  read, as today (Q1).
- **Stashes**: with a filter, the pager keeps the set of SHAs it has seen as visible tips or parents;
  a stash whose base is in it is placed by D54's rule as today, otherwise it waits and is placed
  right above its base when the base comes, and is left out if the base never comes. Without a
  filter the pager is unchanged. (Amends D54 while a filter is on.) What counts as seen: the shown
  refs' tips and HEAD's commit, taken from the refs read, plus the parents of every row added while
  a stash still waits. A filter that hides and solos nothing loads exactly as no filter, stashes
  included; solo's standard input does not wait for the refs (WP2a).
- **Labels**: `CommitGraphData` takes the filter; `BuildLabels` leaves out hidden refs' labels, except
  the checked-out branch's, and in solo mode every branch label but the soloed refs' and HEAD's
  branch's; tags keep their labels on the rows shown. `Refs` itself stays whole (the details panel
  and the HEAD walks use it).
- **A change of filter** reloads through the existing refresh: the filter's key joins the
  fingerprint's comparison (`RunRefreshesAsync`), and `RefreshAsync()` loads the new graph off screen
  and swaps it when complete, as an outside change does. The left panel shows the new state at once;
  while the graph reloads for a filter, a 2-px indeterminate bar (`GraphReloading`, `VcAccentBrush`)
  runs under the column headers, so the click is seen to take effect on a large history.

### 8.3 The left panel

- Every ref stays listed. A hidden branch row draws its name in `VcTextDisabledBrush` and an
  `IconEyeOff` button (`RefHiddenToggle`, tooltip `Hidden in the graph. Click to show it`) at its
  right, before the ahead and behind counts; a click shows it again. A soloed row draws an `IconSolo`
  button (`RefSoloToggle`, `VcAccentBrush`, tooltip `Soloed. Click to stop soloing it`). In solo mode
  the branch rows that are neither soloed nor HEAD's are drawn in `VcTextDisabledBrush`. A remote
  hidden as a whole draws `IconEyeOff` on its row too.
- The toggles are `VcIconButton`s 18 by 18, marked with a new attached property
  `RefDrag.IsExcluded`: `BranchDragService`'s walk from the pressed element (a tunnelling handler that
  sees the press before the button) stops at such a control, so a press never arms a branch drag, and
  `LeftPanelView`'s double-click `RowOf` skips it, so a double-click never checks out. They are left
  out of the row's automation name.
- A click on a hidden branch's row whose commit is a row of the graph (another ref reaches it)
  selects it as today; otherwise nothing is selected and the row's tooltip says `{b} is hidden in
  the graph`.
- Menus: a local or remote branch's menu gains, in a group before `Copy branch name`: `Compare {b}
  with {cur}` (not on the current branch), `Show reflog of {b}` (local only), `Hide {b}` (or `Show
  {b}`), `Solo {b}` (or `Stop soloing {b}`); the Local branches and Remotes sections' menus gain `Show
  all hidden branches` (disabled `No branch is hidden`) and `Stop soloing` (disabled `No branch is
  soloed`). A remote's menu gains `Hide every branch of {r}` / `Show every branch of {r}`.

### 8.4 The session (D159)

`SessionState.GraphFilters`: a list of `RepositoryGraphFilter(RepositoryPath, Hidden, Soloed,
HiddenRemotes, Upstreams, LastUsed)` with full names, keyed by the same normalised working-tree path
as `TabState` and `Recent`, separate from the tabs so that closing a tab keeps the filter, and bounded
on its own: the 100 most recently used repositories' filters are kept (whatever the recent list
holds), and an entry whose folder no longer exists is dropped at start-up. It is written when a filter
changes. The app's own rename of a branch moves its entry, its delete removes it; after a
repository's first refs read, names that no longer exist are dropped, and solo mode ends when no
soloed name exists. An outside rename shows the branch again under its new name.

### 8.5 Tests

`GraphFilterTests` (Git: the arguments for each filter, the shown SHAs equal to git's own for hide,
solo, the upstream, a local upstream (not followed), `<r>/HEAD`, a hidden remote with a branch fetched
later, a tag and a branch of one name, the checked-out hidden branch; the stdin fallback; the stashes'
rule), `CommitGraphDataTests` (labels under a filter, the checked-out branch's label),
`LeftPanelViewModelTests` (marks, toggles, menus), a view test that a press-and-move and a
double-click on a toggle start no drag and no checkout, `SessionStoreTests` (the new field with an
older file, the bound), and section 15's checks.

## 9. C12: protected changes and the automatic stash (D153)

### 9.1 Which checkouts do what

| Checkout | When | What happens |
|---|---|---|
| Of a commit or a tag that the current branch reaches (it detaches: "Check out this commit", a tag's "Check out {t}", the reflog's and backups' "Check out this commit") | HEAD is on a branch, the target is not HEAD's own commit, and the current branch reaches the target (any parent: `IsReachableFromHead` for a loaded row, else `merge-base --is-ancestor <sha> HEAD` inside the operation) | **Always** `git switch --detach --no-overwrite-ignore`: an old commit that tracks a file now ignored would otherwise overwrite it, and the way back would delete it (0.7), with or without the user's changes. When the status has changes (staged, unstaged, untracked or conflicted; ignored files are not changes): **the automatic stash** first (9.2), without a question |
| Every other checkout (a branch, a remote branch, a commit the branch does not reach, a detached HEAD's checkout, "Create branch" with its box ticked, "Check out and merge", "Check out and rebase", the list's checkout, Abort's return) | always | Git runs it as today (its default for ignored files, Q5); when git refuses because changes would be overwritten, 9.3's message |
| An undo or redo that switches HEAD (7.3's `Switch`) | always | never the automatic stash: `--no-overwrite-ignore`, and a refusal shows 7.6's text |

The target being HEAD's own commit changes no file and is never refused, so no stash is taken there;
a detached HEAD has no current branch, so nothing is stashed from one. A checkout that silently undoes
an unstaged deletion of a file the target changes (git's own behaviour, 0.7) is accepted: the content
is in HEAD and in the target; only the deletion is lost, which `git checkout` itself does.

### 9.2 The automatic stash

One local operation of several steps, titled `Check out {name}` as phase 3's, `HasSeveralSteps`:

1. Read HEAD (`rev-parse HEAD --symbolic-full-name HEAD`) and a fresh status. When HEAD is not the
   branch and commit the click saw (D124), decide again: still on a branch, not at the target, and
   `merge-base --is-ancestor <target> HEAD`; otherwise refuse with `HEAD moved while this waited: it
   was on {a} and is on {b} now. Nothing was checked out.` Refuse before any git write: an operation in
   progress → `Finish or abort the {kind} first` (a stash during a merge without conflicts would
   delete `MERGE_HEAD`, 0.7); conflicted files → `Resolve the conflicts first`. No changes any more →
   step 4 alone.
2. Note `refs/stash` (`rev-parse --verify --quiet refs/stash`).
3. `git stash push --include-untracked --message "Auto stash before checking out {name}"` without
   `GIT_LITERAL_PATHSPECS` (D108). `{name}` is the short SHA or the tag's name. "No local changes to
   save" → step 4 alone. A failure with `refs/stash` moved (Windows could not delete a file another
   program held open; git then deleted the other untracked files and skipped resetting the tracked
   ones): put back every untracked file of `<stash>^3` that is now missing (`git restore
   --source=<stash>^3 --worktree -- <missing paths>`), check nothing out, keep the stash, and say so
   in the error dialog "Checkout failed", with git's output: `Git could not clear your changes from
   the working tree, so nothing was checked out. They are also saved in the stash "Auto stash before
   checking out {name}".` (when every file was put back) or `Your changes are saved in the stash "Auto
   stash before checking out {name}". Git could not finish clearing the working tree, so some of them
   are still there too. Nothing was checked out. Keep the stash until you have checked your files.`
   (when one could not be). A failure with `refs/stash` unchanged: the failure, nothing changed.
4. `git switch --detach --no-overwrite-ignore <sha>`.
5. A refusal: `git stash pop --index stash@{n}` (found by SHA; D91's plain-pop fallback), which
   cannot overlap right after the push; then the refusal's message without the button (9.3: after the
   automatic stash only ignored files or a submodule can be in the way, so the ignored-files text
   applies); when the pop-back fails, phase 3's `CheckoutFailedChangesKept` naming the stash.
6. Done: the status bar says `Your changes are in the stash "Auto stash before checking out {name}".`
   (it fits the status bar; the detached HEAD shows in its branch text; the tooltip has the same
   sentence). The stash is drawn as every stash: a ring on the commit HEAD was on, with a dashed line
   to it (section 10). Nothing pops it later (D153 (3)); checking the branch out again and popping it
   gives the changes back without a conflict. Undo switches back and pops it (D166, 7.3).

### 9.3 The refusal message (D153 (1))

Every checkout path classifies git's refusal the same way (`GitFailureClassifier`, stderr and stdout
together), with the count of distinct paths under git's headers (0.7):

| Git said | Error dialog (title `Checkout blocked`) | Button |
|---|---|---|
| only `Your local changes to the following files would be overwritten by checkout:` blocks | `1 conflict prevents checkout. Commit, stash, or discard your changes first.` / `{N} conflicts prevent checkout. …` | `Stash changes and check out` (D80, D91), where phase 3 offers it (`offerStash`); never after an automatic stash's refusal |
| any untracked block (`untracked working tree files would be overwritten by checkout:`, `would be removed by checkout:`, `Updating the following directories would lose untracked files in them:`) whose paths are not all ignored | the same sentence; git's output below names the files | none (D91) |
| untracked blocks whose paths are all ignored (`check-ignore --stdin -z`; after an automatic stash or with `--no-overwrite-ignore`) | `1 ignored file is in the way: {short} tracks a file of the same name. Move it away first.` / `{N} ignored files are in the way: …` | none |
| `you need to resolve your current index first` | refused before git: `Resolve the conflicts first` | none |
| `cannot switch branch while …` | refused before git: `Finish or abort the {kind} first` | none |

Git's output stays below the summary, so the names are one glance away. This replaces phase 3's
`CheckoutBlockedByChanges` and `CheckoutBlockedByUntrackedFiles` summaries (D153's sentence replaces
D91's own explanation, which git's output now carries), gives "Create branch" with checkout the same
display (today "Creating the branch failed"), and brings the folder block (today "Checkout failed")
under C12. The classifier gains `CheckoutBlockedByUntrackedFolder` and a `BlockedPaths` list on
`GitFailure`.

The same words cover the operations that refuse for the same reason (D153's "an operation that could
lose uncommitted changes does not start and says why"):

| Operation | Git said | Summary |
|---|---|---|
| Merge, the drop's merge, pull's merge and fast-forward, "Check out and merge"'s merge, cherry-pick, revert (one or a list) | the `by merge:` blocks | `1 conflict prevents the {merge|cherry-pick|revert}. Commit, stash, or discard your changes first.` / `{N} conflicts prevent the …` |
| Merge, cherry-pick, revert with anything staged | ort's one-line form, or `your local changes would be overwritten by cherry-pick.` / `by revert.` (no names) | `Your staged changes prevent the {merge|cherry-pick|revert}. Commit, stash, or unstage them first.` |
| Rebase of any kind | `cannot rebase: …` | phase 4's `Git rebases only a clean working tree: commit or stash your changes first.` (any change refuses; there is nothing to count) |
| Apply or pop a stash (menus, the toolbar's Pop, a double-click) | checked by the app before git: the stash's changed paths (`diff-tree -r -z --no-renames --name-only <s>^1 <s>`, both sides of a rename) that have a local change, plus its untracked paths (`ls-tree -r -z --name-only <s>^3`) that exist, compared with the status's raw (`-z`) paths | `1 conflict prevents applying the stash. Commit, stash, or discard your changes first.` / `{N} conflicts prevent applying the stash. …`; git is not run. Stricter than git on purpose: git's refusal restores the untracked files anyway and a staged overlap becomes a conflict (0.5). D91's own pop after "Stash changes and check out" keeps going to git |

### 9.4 Tests

Git layer: a refused checkout of each of the four forms changes nothing (index entries, HEAD's
reflog, refs, files, status); the count over recorded texts (several blocks, the folder block's empty
line, 43 names, names with a space and `é`); the automatic stash's round trip (the stash's `%gs` and
first parent, ignored files untouched, the status identical after the way back); its refusals (in
progress, conflicts, nothing to stash, a staged submodule change only); ignored files with
`--no-overwrite-ignore`, also on a clean tree; the half-made stash with two untracked files, one held
open; `merge-base --is-ancestor` cases. App layer: the dialog for each refusal kind and every checkout
path; the automatic stash's git calls, status text, stash row and its rules not taken; HEAD moved
while it waited; the apply pre-check with a renamed path and an `é` path; nothing pops the automatic
stash. Section 15's checks.

## 10. Stashes drawn dashed (D161)

A stash row keeps its ring (D54's place, its lane and colour), and the line from it down to the
commit it was made on is dashed: dashes and gaps of 3 along the line, as the working-changes row's
line to HEAD (D60). `GraphLayout` marks the lane a stash starts as a stash lane until the base's row:
the stash row's `OutOfNode` line, the `PassThrough` lines below it, and the `IntoNode` line at the
base row carry `GraphLine.IsDashed` (a new init property, false by default); from the base on, the
lane is the base's own and solid. A merge's line to a further parent that joins a stash lane makes
the lane solid from there, because it then leads to a real parent (found while building WP2a). `CommitGraphControl` draws a dashed line with a dash style whose
phase is taken from the line's absolute y (as `DrawWorkingRowLine` does), so a line cut by the
viewport or drawn row by row keeps one pattern, curves included. Nothing else changes: lanes,
colours, nodes and the stash's place are as D54 and D55 make them, so phase 1's
`GraphLayoutTests` keep their lanes and only their `IsDashed` facts are added. Every picture with a
stash changes (14.1).

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

- **In `CommitGraphView.axaml` the pages' panel comes after the diff's and the resolver's**: hidden
  views stay in the visual tree, `ShellDriver.Find<T>(name)` returns the first match whatever its
  visibility, and the history page hosts a `DiffView` with phase 2's names, so phase 2's lookups must
  find the main diff first (a `GraphAreaHooksTests` assertion pins it).
- Opening a page: `TryLeaveGraphAreaAsync()` (the list asks if changed), then `LeaveGraphArea()`
  (which now also closes the page), then `Page = page`. Opening a page from a page (Blame from the
  history) replaces it. `OpenResolverAsync`, which closes the diff and the list itself, closes the
  page too (the code map's pitfall: it does not call `LeaveGraphArea`). Opening a page does not
  change the right panel; the user's first click or key on a row of it does (4.1, 11.3).
- A diff from the right panel (a commit's file) opens over a page as over the list; closing it
  returns to the page (`CloseDiff(returnFocus)` asks the page for the focus). A diff from the stage
  panel leaves the page first, as it leaves the list today.
- Esc: a page registers `EscapeOrder.Page` = 370 and declines while a diff is open over it or the
  focus is in a text box; it closes the page and gives the focus to the graph
  (`GraphFocusRequested(Graph)`).
- **Revealing on the way back**: when a page closes (Esc, its close button) after the graph's
  selection was set from it, the selected row is revealed once the graph shows again (`Reveal`
  posted at `Loaded` priority after `ShowsGraph` turns true, not while the control is hidden and has
  no viewport), so the commit the user clicked in a blame, a history or a reflog is in view.
- The search bar belongs to the graph view: Search closes a page and the diff first (they hold no
  work).
- `RepoTabViewModel` forwards nothing new: pages stay in column 1.
- The activity log lies under all of these (2.1).

### 11.2 When Search can show the graph

Search is enabled while a repository is open and neither the list nor the resolver is open (they
hold work, and leaving them asks); it closes a diff or a page first.

### 11.3 Showing a commit from a page or a comparison

`RepositoryViewModel.ShowCommit(sha)`: it first leaves the working-changes row
(`IsWorkingRowSelected = false`, the stage panel's draft staying in memory), so that the details
panel shows; then, when the graph has the commit, it selects it behind the page as
`IInteractiveRebaseHost.ShowDetails` does (without closing a diff); otherwise it clears the graph's
selection and shows the commit's details directly (`Details.ShowAsync(sha)`), and records the SHA as
the shown one, so that selecting that row later (or another) reloads the panel (the code map's
`_selectedSha` pitfall). The zero SHA of an uncommitted blame line selects the working-changes row
instead (4.3). `ShowInGraph(sha)` closes the page, selects the row and reveals it (`Select(sha,
reveal: true)`); for a commit the graph does not have it does nothing and its menu item is disabled.
The right panel's file menu for a commit shown this way takes the shown commit's SHA, not
`Selection.Single` (the pitfall of `FileMenuTarget.CommitSha`). `Reveal(sha)` scrolls a row into view
without selecting it (6.4's links).

## 12. Texts

Texts given in the sections above are not repeated here. Counts use the singular for one (D113); every
text with a count is written out in both forms where it is defined (`1 match` / `{n} matches`,
`Removed 1 backup` / `Removed {N} backups`, `1 file has changed since` / `{N} files have changed
since`, `1 conflict prevents …` / `{N} conflicts prevent …`, `1 entry is not shown: it names …` / `{N}
entries are not shown: they name …`, `1 commit only on …` / `{a} commits only on …`).

### 12.1 Operations: title, running text, error dialog title, failure summary

| Operation | Title | Running text | Error title | Summary |
|---|---|---|---|---|
| Undo | `Undo {text}` | `Undoing {text}…` | `Undo failed` | `Git could not undo {text}.`, or the check's reason (7.6) |
| Redo | `Redo {text}` | `Redoing {text}…` | `Redo failed` | `Git could not redo {text}.`, or the reason |
| Restore branch | `Restore branch {b}` | `Restoring {b}…` | `Restoring the branch failed` | `Git could not restore {b}.` |
| Restore tag | `Restore tag {t}` | `Restoring {t}…` | `Restoring the tag failed` | `Git could not restore {t}.` |
| Restore stash | `Restore stash` | `Restoring the stash…` | `Restoring the stash failed` | `Git could not restore the stash.` |
| Restore the discarded files | `Restore the discarded files` | `Restoring the discarded files…` | `Restoring the files failed` | `Git could not restore the files.` |
| Restore the changes | `Restore the changes` | `Restoring the changes…` | `Restoring the changes failed` | `Git could not restore the changes.` |
| Restore the staged changes | `Restore the staged changes` | `Restoring the staged changes…` | `Restoring the staged changes failed` | `Git could not restore the staged changes.` |
| Push back | `Push {b} back to {r}`, `Push {t} back to {r}` | `Pushing {b} back to {r}…` | `Push failed` | `Git could not push {b} back to {r}.` |
| The automatic stash's checkout | `Check out {name}` (phase 3's) | `Stashing your changes…`, then `Checking out {name}…` | `Checkout failed` | phase 3's, or 9.2's texts |

### 12.2 Results in the status bar

| After | Text |
|---|---|
| Undo, Redo | `Undid {text}`, `Redid {text}` |
| A restore | `Restored branch {b}`, `Restored tag {t}`, `Restored the stash as stash@{0}`, `Restored 1 file` / `Restored {N} files`, `Restored the staged changes`, `Pushed {b} back to {r}`, `Pushed {t} back to {r}` |
| The automatic stash | 9.2's text |
| An undo of a pop or a drop | `Undid {text}`; the stash comes back as `stash@{0}` (the left panel shows it first) |

### 12.3 Backups in words (the backups list's "What")

| Kind | Text |
|---|---|
| `discard` | `Before discarding 1 file` / `Before discarding {N} files` (the paths in the snapshot's message) |
| `reset` | `Before a hard reset to {short}` or `Before a mixed reset` (from the `-changes` snapshot's subject); without one: `Before a reset` |
| `detached` | `Commits a detached HEAD left behind` |
| `branch` | `Deleted branch {b}` |
| `remote-branch` | `Deleted {b} on {r}` |
| `remote` | `Removed remote {r}` |
| `force-push` | `Before a force push of {name}` |
| `tag` | `Deleted tag {t}` |
| `tag-undone` | `Tag {t}, its creation undone` |
| `remote-tag` | `Deleted tag {t} on {r}` |
| `stash` | `Dropped or popped stash "{subject}"` |
| `abort` | `Before aborting` |
| `rebase` | `Before rebasing {b}` |
| `skip` | `Before skipping a commit` |
| any other kind | the kind as written |

### 12.4 New disabled reasons

| Item | Reason |
|---|---|
| Undo, Redo | 7.4's reasons |
| `File history`, `Blame` | `This file is in no commit yet`; `Make the first commit first` |
| `Blame before this commit` | `This is the file's first commit` |
| `Show in graph`, a compare link | `This commit is not in the graph` |
| `Hide {b}` | `The checked-out branch is always shown` |
| `Show all hidden branches` | `No branch is hidden` |
| `Stop soloing` | `No branch is soloed` |
| `Compare {b} with {cur}` | `HEAD is unborn` |
| `Restore branch {b}`, `Restore tag {t}` | `A branch named {b} exists`, `A tag named {t} exists` |
| `Restore the staged changes` | `Unstage your changes first` |
| `Push {b} back to {r}…` | `{r} no longer exists`; `The remote this backup came from cannot be found`; `The commit your force push left can no longer be found` |
| Search | `Close the interactive rebase first`; `Close the conflict resolver first` |

### 12.5 Loading states

`Loading…` (secondary) in the place of the content: a history until its first row (4.1), a blame
until git ends (4.2), a comparison's list (6.4). `Searching…` and the progress bar for a search
(3.1). The 2-px bar under the column headers while a filter reloads the graph (8.2).

## 13. UI inventory: menus, questions, colours, icons, automation ids

### 13.1 The menus as phase 5 changes them

Phase 3's rule stands (D77, D92): items that cannot apply are left out, items whose reason helps are
disabled with it.

| Builder | Change |
|---|---|
| `ForCommit`, `ForCommits`, `ForStash` | none (two selected commits compare without a menu, D162) |
| `ForWorkingChanges` | `Show reflog` and `Show backups` in the last group, before `Show in folder` |
| `ForLocalBranch` | a new group before `Copy branch name`: `Compare {b} with {cur}` (not on the current branch), `Show reflog of {b}`, `Hide {b}` or `Show {b}`, `Solo {b}` or `Stop soloing {b}` |
| `ForRemoteBranch` | a new group before `Copy branch name`: `Compare {r}/{b} with {cur}`, `Hide {r}/{b}` or `Show {r}/{b}`, `Solo {r}/{b}` or `Stop soloing {r}/{b}` |
| `ForTag` | `Compare {t} with {cur}` in a group before `Copy tag name` |
| `ForRemote` | `Hide every branch of {r}` or `Show every branch of {r}`, in a group before `Copy URL` |
| `ForSection` | Local branches: `Show reflog`, `Show backups`, `Show all hidden branches`, `Stop soloing` after its item; Remotes: `Show all hidden branches`, `Stop soloing` after its items |
| `ForUnstagedFile`, `ForStagedFile`, `ForCommitFile`, `ForDiff` | a group `File history`, `Blame` before `Open file` (4.4); the resolver's header menu leaves them out (a flag) |
| `ForEmptyGraphArea` | `Show reflog` and `Show backups` in the last group, before `Manage remotes…` |
| `ForDrop` | `Compare {A} with {B}` in a group of its own at the end |
| New | `ForActivityLogRow` (2.2), `ForFileHistoryRow` (4.1), `ForBlameRun` (4.2), `ForReflogEntry` (5.2), `ForBackup` (5.3, 7.10), `ForCompareFile` (6.4), the search mode menu (3.1), the reflog source menu (5.1) |

The menu model needs nothing new: the Files mode's case note is in the box's placeholder, not in a
menu item's tooltip (`MenuItem` has a tooltip only for a disabled reason). WP0 splits
`RepositoryMenus.cs` and `OperationTexts.Menus.cs` into partial files per package (16).

### 13.2 Questions

- Every undo or redo with a `RemoteRef` effect, and `Push {b} back to {r}…`: 7.5's table.
- `Restore the discarded files…` and `Restore the changes…` when a file would be overwritten: D75's
  `Restore discarded changes?` question, as the restore bar asks it.
- Nothing else asks: Undo, Redo and the automatic stash change only what can be put back, and refuse
  instead of overwriting (7.3's checks, 9.2).

### 13.3 New colours, styles and icons

| Token (brush key) | Dark | Light | Used for |
|---|---|---|---|
| `VcSearchMatchBrush` | `#F2C94C` | `#B98900` | The 3-px mark of a search match (3.3) |

The dimmed rows use opacity; the blame gutter's runs, the activity log, the progress bars and the
toggles use existing brushes. `VcFlatButton` gains a `selected` class (1.2). New icons, Lucide's
(ISC, in `THIRD-PARTY-NOTICES.md`): `IconHistory` ("history"), `IconBlame` ("text-search"),
`IconEyeOff` ("eye-off"), `IconSolo` ("crosshair").

### 13.4 Automation ids and names

Toolbar and status bar: `UndoButton`, `RedoButton`, `SearchButton`, `ActivityLogToggle` (kept), with
`HelpText` their tooltips. Activity log: `ActivityLog`, `ActivityLogHeader`, `ActivityLogShowReads`,
`ActivityLogClose`, `ActivityLogList`, `ActivityLogOperation` (named by its title),
`ActivityLogCall` (named by its command), `ActivityLogOutput`, `ActivityLogSplitter`. Search:
`SearchBar`, `SearchModeButton`, `SearchBox`, `SearchCount`, `SearchProgress`, `SearchPrevious`,
`SearchNext`, `SearchClose`; `GraphReloading`. History: `FileHistory`, `FileHistoryHeader`,
`FileHistoryTitle`, `FileHistoryBlame`, `FileHistoryClose`, `FileHistoryList`, `FileHistoryRow`
(named `{short} {subject}`), `FileHistoryDiff`. Blame: `Blame`, `BlameHeader`, `BlameTitle`,
`BlameHistory`, `BlameClose`, `BlameText` (a Document; line height 15.84, the text's left edge after
the gutter and the line numbers, so the report can state a line's point as phase 4's `ResultPoint`
does), `BlameGutter` (a control element). Reflog: `Reflog`, `ReflogSource`, `ReflogClose`,
`ReflogList`, `ReflogRow` (named by its selector), `BackupRow` (named by the backup's name),
`ReflogFooter`. Compare: `Compare`, `CompareTitle`, `CompareSwap`, `CompareClose`, `CompareFrom`,
`CompareTo`, `CompareAllDifferences`, `CompareCounts`, `CompareFileList`. Details panel:
`AllFilesButton`. Left panel: `RefHiddenToggle`, `RefSoloToggle`. Every page's root has an automation
peer that makes it a control element (a pane named by its title), as phase 4's views do. No new view
reuses a phase 2 name except the history page's hosted `DiffView`, which comes later in the tree than
the main diff (11.1); the driver's history helpers look inside `FileHistory` only.

## 14. What changes for earlier phases

### 14.1 Their checks and tests, changed on purpose

Every change below is listed in phase 5's report under "Changes to expected results", with the
tests changed in the commit that changes the behaviour (plan.md's gate, step 6).

| Change | Where it shows | Tests that pin today's state |
|---|---|---|
| Search enabled whenever a repository is open; Undo and Redo enabled after recorded actions, with tooltips; the Activity log toggle enabled with a repository | the toolbar and status bar of **every** window picture of phases 1 to 4 with a repository open; phase 0's empty shell is unchanged | `ToolbarTests` (lines 289 and 311), `Phase3Checks.A.Helpers.cs` (313), `ShellExpectations.cs` (the empty tab's states stay; the status bar's texts stay), `StatusBarTests` (38), `Phase0RealWindowPass` (125, 134) |
| The details panel's `All files` button (4.5) | every picture of phases 1 to 4 showing a commit's details | `PanelViewTests`, `CommitDetailsViewModelTests`, the checks that assert the panel's buttons |
| Every stash's line dashed (section 10) | phase 1's graph-scenario pictures (its `stash@{0}`), phase 2's check 23, phase 3's pictures of the ops scenario with its stash row | `GraphLayoutTests` (the `IsDashed` facts), `CommitGraphControlTests` (line 115's colour sample in the stash's lane, which a gap can now hit: sample a dash), `Phase3Checks.B.Support.cs` (`ExpectedLines`: a line leaving a stash is dashed down to its base; changed with WP2a's merge, `7316ad0`) |
| New menu items (13.1) | the menu pictures and heights of phases 3 and 4 of the changed builders: `ForLocalBranch` (02a, 02b, 02c, 02f, 02g, 05e, 08b, 09d, 13f, 13l, 18b1, 20b, 28a, 28c, 30a, 30f, 32c; a1d, a1f), `ForRemoteBranch` (02d, 02e, 02h, 06d, 08f, 08g, 13b1, 30c; a1e), `ForRemote` (03c), `ForTag` (03a, 16f), `ForSection` (03d), `ForEmptyGraphArea` (03f, 05b1, 23i), `ForWorkingChanges` (04l, 04m), the file menus (04a, 04c, 04d, 04f, 04g, 04h, 04j, 04k, 05b2, 09b2, 26b, 26d, 26f, 26g; c16a only if the stage list's menu is shown, the resolver's header has no new items), `ForDrop` (07b, 07c, 07d, 30g; a3a, a3b, a3c, c13i), and their real-window twins | `RepositoryMenusTests`, `Phase3Expectations.*` and `Phase4Expectations.*` (lists and heights; the real-window pass links them) |
| Two selected commits show their comparison (D162) | phase 3's and phase 4's pictures with exactly two selected commits (the right panel) | `GraphSelectionTests`, the checks that assert `MultiSelectionView` with two commits |
| C12's refusal message (9.3) | phase 3's check 14 (`14a` to `14h`, `14b1`) and its real-window `14c`; phase 3's check 15 when a stash's apply is refused by the app first | `OperationTextsTests`, `RepositoryOperationsCheckoutTests`, `RepositoryOperationsTests` (106 to 147), `DialogTests`, `FailureClassifierTests`, `RepositoryOperationsBranchesTests` (318), `RepositoryOperationsStashTagsTests`, `Phase3Expectations.C.cs` (286 to 311) |
| Backup names from the fixed backup clock in the harnesses (2.3) | no picture (earlier phases never show a backup's name) | phase 3's and 4's checks and App tests that read backup times (`Phase4Checks.C.SkipAbort.cs` compares their order, which the clock keeps), `RepositoryOperationsBackupCleanupTests` |
| Phase 4's leftovers (D160, merged) | `a3d`, `b8c`, `c11j` (the SHA on one line); the resolver's pictures with one conflicted file (the file arrows dimmer) | changed with them in `279dd8c` |

Nearly every picture of phases 1 to 4 therefore changes, most of them only in the toolbar, the status
bar and the details panel's buttons. The gate makes that inspection quick and exact (15.7).

### 14.2 `plan.md` and the risks

Risk 12 (the run time) grows: two more test processes (15.3), and two git calls before and after every
operation for the undo history (7.7). Risk 18 (the real-window pass) grows by about 18 launches. New
risks: the inspection of nearly every earlier picture at this gate (15.7); `read-tree`'s treatment of
ignored files changed in Git 2.34 (the ignored-files guard of 7.3 makes undo the same on every git).
Phase 6's settings screen may expose `--no-overwrite-ignore` for every checkout (9.1 uses it for the
checkouts of a commit the branch reaches only).

## 15. Tests and harness

### 15.1 Scenario repositories (`tests/VisualCommit.Testing/Scenarios.Phase5.cs`)

Built once per process as templates, copied per test, every SHA pinned by
`tests/VisualCommit.Git.Tests/Phase5ScenarioTests.cs`, every git state derived with Git 2.36 and, where
words may differ, also with the 2.55 on the development machine (0):

| Scenario | Holds |
|---|---|
| `HistoryAsync` ("history") | three authors; `src/Parser.cs` created, edited, renamed to `src/Reader.cs` with an edit (over 50 % similar), edited again by another author, so blame has lines from four commits; `docs/notes.md` deleted and added again; an unchanged `LICENSE` for "All files"; an image; a merge of a side branch that edited `README.md`; commit messages with words only in their bodies; a pickaxe string added in one commit and removed in a later one; `main` and `feature/compare` diverged from a base, with a rename on `main`; a local bare `origin` |
| `FiltersAsync` ("filters") | `main`; `feature/alpha` with an upstream `origin/feature/alpha` one commit ahead; `feature/beta` with a long history of its own; `feature/merged`, merged into `main`; `origin/HEAD` pointing at `origin/feature/beta`; a stash made on `feature/beta`'s tip and a second stash on a commit only `feature/alpha` reaches (so soloing `feature/beta` leaves it out); a tag `v1`; a local bare `origin` |
| `ReflogAsync` ("reflog") | a branch's reflog with a commit reset away (lost), an amended commit (the old one lost), a checkout of another branch and back, the checked-out branch renamed (a gap in HEAD's reflog), a branch deleted; a local bare `origin` without the branch its `remote-branch-…` backup names; backup refs of every kind the list shows, with fixed names dated 2026-01-01 (younger than 30 days by the harnesses' backup clock, 2.3), among them a hard reset's and a mixed reset's `-changes`, an abort's and a skip's; and backups dated 2025-11 for the activity log's clean-up notes, plus a copy with 51 young backups for its `(more than 50 backups)` line |
| `ProtectAsync` ("protect") | `main` with older commits and a tag on one; `other` changing files `main` has, adding a file, deleting one; a file `main` ignores now that an older commit of `main` tracks; working-tree changes are made by each check |

The undo checks also use phase 3's and phase 4's scenarios (ops, sync with its bare remote, rebase,
interactive, conflicts with the fake merge tool), and these.

### 15.2 Harness additions

`ShellDriver.Phase5*.cs`: the activity log (open, rows as a snapshot, open a row, the splitter);
search (open by button and key, type, Enter, Shift+Enter, the marks as a snapshot read from the
control's `Search` and per-row state, not from pixels); the pages (history rows, the hosted diff,
blame lines and gutter runs, reflog rows, backup rows, the source menu); "All files"; compare (rows,
swap, the switch, the links); the left panel's toggles; the toolbar's Undo and Redo and their
tooltips; `PressGitUndo`, `PressGitRedo`, `PressSearch` (1.3). `RealApp.Phase5*.cs` for the same by
automation id and name. The clocks (2.3): `AppSession.Start(..., recordClock, backupClock)` in
`ShellDriver.Start`, and `VISUALCOMMIT_RECORD_CLOCK=frozen` and `VISUALCOMMIT_BACKUP_CLOCK=fixed` in
`RealApp.Launch`. When a scenario holds backups due for the clean-up, the driver and `RealApp` wait for
the `Clean up backups` record before a check's first input (the clean-up at opening is dropped when
the queue is busy). Each driver addition is tested in App.Tests, as phase 4's are.

### 15.3 The walk-through projects

Two projects, decided now (D133 and D151's rule: a project's single UI thread sets a floor, and the
undo checks alone are about 100 operations): `tests/VisualCommit.VisualTests.Phase5` (parts A, B, F
and P) and `tests/VisualCommit.VisualTests.Phase5Undo` (part U), each as phase 4's (the code map's
recipe): csproj, `AssemblyInfo.cs`, `Phase5Checks.cs`, `Phase5Checks.Support.cs` (phase 4's, with the
part letters `a`, `b`, `f`, `p` and `u` in `PictureName()`; the undo project links the support and the
common expectations), `Phase5Expectations.Common.cs` and one file per part, their tests named
`Phase5ExpectationTests.<part>.cs` and `Phase5SupportTests.cs` (so the real-window link pattern takes
only the expectations); both projects in `VisualCommit.slnx`; the real-window csproj's link
`..\VisualCommit.VisualTests.Phase5*\Phase5Expectations*.cs`; `scripts/time-scripted-check.sh` taking
phase 5 by default and the right project for each part; CLAUDE.md's Commands rows. Both projects save
their pictures under `artifacts/visual/phase-5/scripted/`: the first picture of a run empties that
folder only once per run of both projects (`VisualRun` keys the emptying by phase and run, not by
process).

### 15.4 Proposed visual checks (the report's writers write them in full, step 6)

Parts: **A** the shell and the activity log, **B** the pages and "All files", **F** finding (search,
compare, solo and hide, dashed stashes), **U** undo and redo, **P** protected changes (C12). The
letters avoid C, so that a check's name never reads as the requirement C12. "RW" marks what the
real-window pass repeats: a picture of every new screen, and what only a real window shows (real
keys, real typing, a real drag of the splitter, a real double-click, a real restart of the process).
Window 1100×700, dark, unless said. Every U check is several test methods, one action with its undo
and redo each, on a fresh copy, so that none comes near the hang watchdog's 3 minutes.

| # | Check | RW |
|---|---|---|
| A1 | The toolbar and status bar: an empty tab (only Theme enabled); a repository open (Search and Activity log enabled, Undo and Redo disabled, `Nothing to undo`); after a commit, Undo enabled at once with `Undo commit "…"` | yes |
| A2 | The activity log on the conflicts scenario with old and many backups: opened from the status bar under the graph; the clean-up's `Removed backup … (older than 30 days)` and `(more than 50 backups)` notes; operations with each outcome (succeeded, a stop with conflicts, failed with the dialog's summary, the merge tool cancelled); a row opened to its git calls; a call opened to its input and output; `Show reads` (asserted by structure: top-level call rows of no operation, in order); Esc in the log with a page open closes only the log | yes |
| A3 | The panel's height: the splitter dragged, the app restarted: the height kept | yes (a real drag and a real process restart) |
| A4 | The keys: Ctrl+F opens the search; Ctrl+Z in the graph undoes a commit and Ctrl+Y redoes it; Ctrl+Z in the commit summary undoes typing only; in the resolver's result it is the resolver's undo | yes (real keys) |
| B5 | File history across a rename, from the stage panel's file, from a commit's file and from a diff's menu: rows, the `renamed from` row, the diff of a step, the right panel unchanged until a row is clicked, then showing it; the list walked with Down; the side-by-side layout at 1920×1080 (light theme); `Show in graph`; Esc: the clicked commit in view | the page at 1100×700 |
| B6 | Blame: the gutter's runs and dates, `Not committed yet` lines, a click on a run (details), a double-click (the graph, the commit revealed), `Blame before this commit`, `File history` | yes (a real double-click on a run) |
| B7 | "All files": the details panel's button, an unchanged file's blame by a click and by its menu (two clicks), the setting kept | yes |
| B8 | The reflog from the Local branches section's menu: HEAD's with the lost marks and the gap line, a branch's, `Create branch here…` from a lost commit (the branch in the graph); Esc: the commit in view | yes |
| B9 | The backups list from `Show backups` and its restores: a deleted branch, discarded files (with the question when a file changed), an abort's and a skip's changes, a mixed reset's staged changes, a deleted remote branch pushed back (the question) | the list |
| F10 | Search, Commits mode: a word only in a commit body, an author, a SHA prefix: marks, dimmed rows, the count, Enter and Shift+Enter (the selection and the right panel), Esc | yes (real typing and Enter) |
| F11 | Search, Files and Changes modes: a part of a file name (the renamed file's both names), a string added then removed (two matches); a refresh while the search is open keeps the marks | n/a |
| F12 | Compare two selected commits (two-dot): the right panel, a file's diff, Swap, a link click (the comparison stays) | yes |
| F13 | Compare branches from a branch menu (three-dot, the merge base's file diff) and with `All differences`, the counts; from a drop; with the working-changes row selected | n/a |
| F14 | Hide `feature/alpha` (its upstream too): the graph without them, the left panel's marks, `Show`; solo `feature/beta`: the graph, labels, the stash on `feature/alpha`'s commit left out; `Stop soloing`; a restart keeps the hidden branch | yes (the restart) |
| F15 | Stashes dashed: the graph scenario's stash and the filters scenario's (the dash pattern sampled) | n/a |
| U16 | Undo and redo of a commit and an amend (the message back in the box), a discard of a file, of a hunk (the other hunk kept on redo), of lines and of all, the restore bar going and coming back | yes (the toolbar's Undo) |
| U17 | Undo and redo of resets: soft, mixed (with and without staged changes), hard (the files back from the snapshot) | n/a |
| U18 | Undo and redo of branch operations: create, create and check out, rename, delete (its upstream back), a checkout that created a tracking branch, a checkout from a detached HEAD that left a commit, a fast-forward, D91's "Stash changes and check out" | n/a |
| U19 | Undo and redo of history operations: merge (both kinds), a cherry-pick of a list, a revert, a rebase, an interactive rebase with a drop and a reword, a squash, a pull | n/a |
| U20 | Undo and redo of stash, apply, pop, drop and tags (create annotated, delete) | n/a |
| U21 | Undo of remote actions against a local bare remote: force push, delete a remote branch, delete a remote tag, a push (each with its question); a lease that fails | n/a |
| U22 | Undo of local configuration: set upstream, remove remote (its branches and URL back), ignore | n/a |
| U23 | Refusals: Undo after a terminal commit on the same branch (the tooltip, the button disabled), during a merge in progress, a Keep move over two changed files in the way (the app's count), an ignored file in the way; a Keep undo on a fresh copy with stale stat data succeeds | n/a |
| P24 | Refused checkouts: a tracked change (`1 conflict prevents checkout…`, the button), several (`3 conflicts…`), an untracked file (no button), a folder in the way; "Create branch" with checkout | yes (the dialog) |
| P25 | The automatic stash: `Check out this commit` on an older commit of `main` with staged, unstaged and untracked changes: no question, the stash row (ring, dashed line to `main`'s tip), the status text, the detached HEAD; `main` checked out again; Pop; and Undo of such a checkout (switched back, changes back) | yes |
| P26 | When it is not taken: HEAD's own commit, a commit only `other` reaches (refused with 9.3's text); a file ignored now that the older commit tracks (refused after the stash with the ignored-files text, the stash popped back, the file intact; and on a clean tree) | n/a |
| P27 | Other refusals: a merge and a cherry-pick over a changed file, over a staged file; a stash's apply refused by the app | n/a |

### 15.5 Each "Delivers" item and what proves it

| Delivers (plan.md phase 5) | Proven by |
|---|---|
| File history following renames, and blame, with click-through | B5, B6, B7; `FileHistoryTests`, `BlameParserTests`, `BlameTests` (Git), `FileHistoryViewModelTests`, `BlameViewModelTests` |
| Reflog viewer with restore; activity log panel | B8, B9, A2, A3; `ReflogTests` (Git: both calls, gaps, missing reflogs, lost), `ReflogViewModelTests`, `BackupReaderTests`, `ActivityLogViewModelTests`, `OperationLogTests`, `GitCallLogTests`, `ActivityLogViewTests` |
| Commit search, search inside changes | F10, F11, A4; `SearchTests` (Git: each mode, chunks, merges, stashes, the empty text), `SearchViewModelTests`, `CommitGraphControlTests` (marks) |
| Compare two commits or branches | F12, F13; `CompareTests` (Git: two-dot, three-dot, unrelated, criss-cross, renames), `CompareViewModelTests` |
| Undo and redo, many steps; remote undo with a lease; the keys | A1, A4, U16 to U23; 7.11's tests |
| Solo and hide, remembered | F14; 8.5's tests |
| C12 | P24 to P27, F15; 9.4's tests |
| Phase 4's leftovers | the leftovers' tests (`279dd8c`); `a3d`, `b8c`, `c11j` and the resolver's pictures |
| Undo reads the backups, which the clean-up keeps; the clean-up's lines in the activity log | B9, A2; `BackupReaderTests`, `UndoRecordingTests` (the keep set), `RepositoryOperationsBackupCleanupTests` |
| The activity log reads `OperationLog` and `IGitCallLog`, with the background clean-up and warnings | A2; `OperationLogTests`, `ActivityLogViewModelTests` |
| Compare on a multi-selection (D162 makes it automatic), file history and blame in the file menus, solo and hide in the branch menus | F12, B5 (from the stage list, a commit's file, a diff), F14; `RepositoryMenusTests` (every changed builder, the resolver's header without the items) |
| The toolbar's Undo, Redo and Search become real | A1, A4, F10; `UndoToolbarTests`, `ToolbarTests` |

Every destructive action of phases 2 to 4 and the check that undoes or restores it (7.2's table):
discard of a file, a hunk, lines, all (U16; B9 from the list); reset of each mode (U17); delete a
branch (U18, B9), delete a branch on a remote (U21, B9), remove a remote (U22); force push (U21);
delete a tag, on a remote (U20, U21); drop a stash (U20); leaving a detached HEAD's commits (U18);
rebase of every kind (U19); abort (B9, D165); skip (B9); the resolver's binary side and "Delete it"
(B9's discard restore, the same `discard-` path).

### 15.6 Run time

The default run took 21 min 52 s at phase 4's gate, rested and alone; the leftovers' run took 26 min
56 s beside other agents. Phase 5 adds two test processes: about 14 test methods in the Phase 5 project
(estimated 8 to 12 minutes alone) and about 30 in the undo project (about 100 operations at 8 to 15
seconds each under the full run's load: 15 to 25 minutes), Git-layer and App tests of several git calls
each, and two git calls before and after every operation of every earlier check (7.7). Expect the
default run to reach 30 minutes or more on the laptop; D152's rule stands for CI (past 20 minutes of
the Windows Test step's 24, the lead raises the limit and records it), and the owner's decision of
D134 on CI's length comes at the project's end. The real-window pass gains about 18 launches (A1 to
A4 with A3's restart, B5 to B9, F10, F12, F14 with its restart, U16, P24, P25): estimated 8 to 11
minutes on top of phase 4's 14.

### 15.7 The gate's inspection of earlier pictures

Nearly every earlier picture changes (14.1), mostly in the toolbar, the status bar and the details
panel's buttons. So that the inspection compares rather than re-reads, the lead, before the gate,
runs the phase 0 to 4 walk-throughs on `master` in a worktree to make the reference pictures again
(they must match `docs/test-reports/phase-4-pictures.sha256`, except the pictures phase 4's report and
step 4 of `status.md` name as varying by nature, which are inspected in full) and keeps a copy of
today's real-window pictures, checked against the same list, before phase 5's first real-window
pass. A script the lead writes makes, for each earlier picture, a difference picture and the bounding
boxes of the changed pixels against its reference; the inspectors get the reference, the new picture
and the difference, and say for each whether every change is one that 14.1 lists. A picture whose
changes lie outside the regions 14.1 names is opened and judged in full. Earlier real-window captures
are judged against their new scripted twins through the pass's own difference pictures. Phase 5's own
pictures are inspected in full, as every phase's are.

### 15.8 What the report settled (D183)

`docs/test-reports/phase-5.md`, written from this design before any phase 5 UI (step 6), fixes
the values this design left open: its section "Decisions this report makes" (AD1 to AD15, BD1 to
BD24, FD1 to FD21, UD1 to UD20, PD1 to PD17) is part of this design for the builders, and where a
decision there and a sentence here differ, the decision holds. The lead ruled on its eight questions
(D183): AD11, BD16, UD17, UD18, UD20 and PD7 as the report proposes; BD21 `Restore the staged
changes` without "…" (changed in 7.10 and 12.4); UD10 the remote questions name, for an annotated
tag, the commit it points to. Of these, these change sentences above:

- **The opening's reads** (2.2, 2.3, AD11): a tab's opening reads, from `git rev-parse
  --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir` to the first refresh,
  are top-level rows under `Show reads`; 2.2's "the welcome page's open, init and clone" means only
  that a welcome page has no log of its own.
- **Index restores from the backups list** (7.10, BD16): `Restore the staged changes` runs `git
  restore --staged --source=<snapshot> -- <the paths diff-tree -r -z --no-renames --name-only
  <snapshot>^1 <snapshot> lists>`, and a hard reset's `Restore the changes…` restores the index
  for the paths `<snapshot>^1` and `<snapshot>^2` differ in: a whole-tree restore after HEAD has
  moved would stage the commits' differences as well. The undo's `IndexTo` inside a session stays
  `-- .`, which is exact there.
- **The restore bar against D75** (7.9, UD18): the bar's Restore runs as the discard entry's Undo
  only while the files are as the discard left them (the bar's own check comes first); otherwise
  D75's question, and the Restore is recorded as an entry of its own (`restore of the discarded
  files`). Phase 2's D75 test stands.
- **A step's calls** (7.3, 7.5, UD20), as the merged git half runs them: soft moves and tag writes
  are lines of the step's one `update-ref -m <msg> --stdin` transaction, written at the step's end
  unless a later effect needs one of its refs first (a switch to a branch the step creates);
  `IndexTo` runs at its place in the sequence (a mixed reset's undo runs `restore --staged` before
  the transaction); configuration and `.gitignore` writes come after the transaction and any
  switch; a Keep move keeps its positional `update-ref` (D169). The end states are the same on 2.36
  and 2.55.
- **HEAD's own commit** (9.1, D179, PD7) is checked out without `--no-overwrite-ignore` and without
  a stash, as 9.1 says; D179's "every checkout of a commit the current branch reaches" excludes it.
- **`git check-ignore` refuses to run with `GIT_LITERAL_PATHSPECS=1`** (exit 128, 2.36 and 2.55),
  so every call of it (9.3's ignored-files test, 7.3's guard) runs without that variable, as
  `AllIgnoredAsync` already does.
- **A merge refused for local changes** differs on 2.55 (exit 2 instead of 128, a last line `Merge
  with strategy ort failed.`, and `error:` before the staged-file message); the classifier keys on
  lines both gits print (PD10).
- **Run time** (15.6, UD17): the Phase5 project has 34 test methods and Phase5Undo 55 (about 170
  operations), more than 15.6 estimates; D152's rule for CI's limit applies.

### 15.9 What the second wave settled

The second wave's packages (WP2b, WP4, WP5, WP6, WP7) built the UI to the report and found these;
each is pinned by the tests named in its merge (`17d8f97`, `a7eb3c9`, `43e8586`, `2905f55`,
`e11ea7d`).

- **Dashes** (10, FD15): the dash pattern of every dashed line starts at the bottom of the row-0
  ring in content coordinates; in a sampled range every run is 2 to 4 pixels, the first and the
  last cut by the range may be shorter but never longer than 4. The pattern is global, so a stash
  whose ring is not on a multiple of 3 starts with a partial dash (WP2b). The reload bar (8.2) lies
  over the scroll bar too and takes no input.
- **Closing the activity log** (2.1, AD6) gives the focus to whatever content is shown (the graph,
  a page, the interactive list, a diff or the resolver), never to a banner.
- **C12's decision is made again inside the operation** (9.1): a checkout the click gave no
  automatic stash (a detached HEAD, HEAD's own commit, a commit the branch did not reach) is
  refused with D124's text (`HEAD moved while this waited: …`) when HEAD has moved meanwhile onto
  a branch that reaches the target, as the automatic stash's own path is. 9.3's rebase row covers
  only `cannot rebase: …`: a pull's rebase refused at its own checkout keeps the pull's summary; a
  pull's merge or fast-forward gets C12's words for every block git prints, the folder's included.
- **The reflog page** (5.2): "Cherry-pick onto {cur}" is disabled when HEAD's history (the graph's
  walk from HEAD, complete by D178) reaches the entry's commit: 5.2's "reasons from the row, not
  from the graph" means not from the graph's selection. A read again keeps the rows it did not
  change, with their containers and focus. A right-click on a list's empty part opens no menu.
- **"All files"** (4.5, BD14) lists the commit's deletions with D at their path's place, and its
  title counts the rows listed (the tree and the deletions).
- **The backups list** (5.3, 7.10) reads a force-push backup's lease when it loads (the
  remote-tracking ref's reflog, two calls per such backup) and disables `Push {b} back to {r}…` when
  it is gone.
- **BD16's paths** (7.10, WP3): the paths for which a reset's snapshot puts the index back (a
  hard reset's index, a mixed reset's staged changes) are the `diff-tree` list less the paths whose
  version in the snapshot equals HEAD's, since git refuses a path that neither side has (`did not
  match any file(s) known to git`); the restore's undo and redo use the same paths
  (`UndoRestoreTests`).
- **Search** (3.1): Enter, Shift+Enter, previous and next during a running search select once git
  has searched every row between the selection and the target match (no nearer match can come) or
  when the search ends; afterwards at once.
- **Compare** (6.2): a reload that maps or drops a multi-selection is not the user selecting, so a
  branch comparison stays.
- **Hide** (8.3, FD19): clicking a hidden branch's row clears the selection (no commit, no working
  row, a diff closed, the details placeholder shown). An upstream two branches track stays hidden
  until both are shown.
- **A kept commit is checked** (5.2; the lead's follow-ups after the wave, `88ac924`): the reflog
  page's and the backups list's "Create branch here…", "Check out this commit", "Reset {cur} to
  this commit…" and "Cherry-pick onto {cur}" run phase 3's operations through actions of their own
  (`CreateBranchAtKeptCommitAsync`, `CheckoutKeptCommitAsync`, `ResetToKeptCommitAsync`,
  `CherryPickKeptCommitAsync`), which check `git cat-file -e <sha>^{commit}`
  (`IGitRepository.CommitExistsAsync`) inside the operation, and before the reset's dialog and the
  checkout's automatic-stash choice, which would read the commit: `The commit {short} no longer
  exists.`, nothing changed. The graph's menus make no such call. The backups list's restores
  refuse with 7.6's `Can't restore …: the commits no longer exist`: those that write the commit by
  their step's own object check, the files' and the staged changes' before their snapshot is read,
  a push back before its question (`KeptCommitGoneTests`).
- **Shared helpers** (the follow-ups, `847d796`): the undo's switch reads git's refusal with
  `GitFailureClassifier.BlockedPaths` (its own version missed the `would be removed by checkout:`
  block and failed with git's raw output: `UndoStepRefusalTests`), the undo walks a folder with
  `GitRepository.FilesUnder`, and phase 1's `AllIgnoredAsync` asks through `IgnoredAmongAsync`.
- **A merge git refuses for something staged writes a reflog entry** (0.7, 7.7; found by CI run
  38853641791 in P27): Git 2.43, 2.55 and 2.56 (exit 2, `Merge with strategy ort failed.`) write a
  no-op entry `merge <the ref>: updating HEAD` into HEAD's reflog, its old and new value both HEAD's
  commit; Git 2.36 writes none, and no git writes one for a merge refused for an unstaged change.
  0.7's "A refusal changes nothing: … HEAD's reflog" holds for the checkouts its row is about, and
  7.7's "a refused action still leaves HEAD's reflog as it was" for what the app's own reads write
  (nothing); a refused merge on 2.43 and later adds this one entry. The app does not mind it: the
  undo recording compares HEAD and the scope's refs before and after (7.7) and never reads HEAD's
  reflog (only a remote-tracking ref's, for a force push's lease, 7.10), so the refused merge records
  nothing (P27 asserts `Nothing to undo` after it on every git); the reflog page reads again after
  the failed operation's refresh and shows the entry as `git reflog` does, git's message at HEAD's
  commit, its menu that of any entry at HEAD's commit. P27's step 8 and
  `Phase5ScenarioTests.Protect_merge_and_cherry_pick_of_others_work_are_refused_over_a_changed_or_a_staged_file`
  pin it.
- **Under the default run's load** (the full run on `4bec368`, 50 min: five of 3,196 failed on
  waits): `git mergetool` is a shell script that starts about 30 git processes and forks its
  shell (MSYS `sh`) about a hundred times before it runs the tool, 2.8 s alone on the development
  laptop, where `git status` takes 0.09 s; under load every process start grows: with four of the
  seven test projects side by side the script took 32 to 64 s while `git status` took 0.9 to
  2.6 s, past the 60 s phase 4's C16 and the git tests waited for the tool and A2's 30 s. They
  wait `Scenarios.MergeToolTimeout`, 120 s, now. A2's second method then took 146 s with the four
  projects (the tool's start 55 s): a start of more than about 90 s would reach the hang
  watchdog's 3 minutes before the wait's own limit, and the steps after the start (the cancel, the
  abort) need the same app and its activity log, so a split does not shorten it. And a scripted
  double-click is two presses that Avalonia counts as one only within its double-tap time, 500 ms,
  by the window's input clock, which is real time: the jobs and frames the headless helpers run
  between the presses took longer under load, and P25's double-click on `main` came as two single
  clicks (its 60 s wait was for a "Check out main" that never started; that checkout makes 8 git
  calls, 0.7 s alone). `ShellDriver.DoubleClick` stops the window's input clock for its presses
  (`HeadlessInputClock`), as a user's double-click carries the times of its presses however busy
  the app is (`ShellDriverTests.A_double_click_stays_one_when_its_first_click_takes_longer_than_the_double_tap_time`).

The review before the gate (status step 25) found these; each is pinned by the tests named.

- **What stands in a file's way** (7.3, 7.9, 7.10; `WorkingTreeFiles.IsInTheWayOfFile`): git's
  `restore --source=<snapshot> --worktree` writes with force, so a folder holding files where the
  snapshot has a file, or a file where it has a folder, goes without a word (2.36 and 2.55 alike),
  and `File.Exists` had taken either for absence, which a discard's undo expects. The recorded
  files (`RecordedFile.InTheWay`, neither a hash nor absence), the restore bar's hash of them and
  its phase-2 fingerprint tell such a path apart, so the simulation refuses with `1 file has
  changed since: notes`; the step's own check of a `RestoreSnapshot` refuses the same before it
  writes (also at its place, after a hard reset's Keep move, where the state left was itself a
  folder, and for the backups list's restores, which record none); and `RestoreSnapshotAsync`
  itself throws (`Nothing was restored: …`) for phase 2's restore bar. An empty folder is replaced
  and loses nothing, so it is no reason (`UndoStepRefusalTests`).
- **A stash's pop and ignored files** (7.3, D169): `stash pop` overwrites an ignored file where the
  stash adds a staged one (`git add -f`, or a path `info/exclude` ignores), which neither the status
  nor the stash's untracked part shows. The undo's `StashPop` check adds the Keep move's ignored-files
  guard against the stash's base (`diff-tree --diff-filter=A <s>^1 <s>`), refusing with `1 ignored
  file is in the way: …` (the undo of a stash and of the automatic stash's checkout). The menus'
  Apply and Pop keep git's behaviour, as 9.3 says (Q5).
- **A stash git could not finish** (0.7, 9.2): a tracked file held open on Windows stops not git's
  clean but its reset, after it rewrote every other tracked file, with no index written
  (`unable to unlink old 'a.txt'`, `Could not reset index file`). `PutBackUnclearedStashAsync` puts
  back from the stash both the untracked files and the tracked files that no longer match it
  (`git diff --name-only <stash>`, then `git restore --source=<stash> --worktree`); "They are also
  saved in the stash" is said only when every path matches again. The undo's own `StashPush` puts
  back the same before its step fails (`ProtectedChangesTests`, `RepositoryOperationsProtectTests`).
- **A Keep move git could not finish** (7.3, 7.5): `read-tree -m -u` that fails at a file held open
  goes on past it and writes no index; the move's other files are put back from the index
  (`checkout-index -f`, which skips the held file) and the files only the target has are deleted
  where they hold its version, then the step fails with git's line (`UndoStepRefusalTests`).
- **Empty untracked folders** (9.2): `stash push --include-untracked` removes them (its `clean -d`)
  and no stash holds a folder; `StashAsync` makes again those that were empty before it
  (`ls-files --others --directory`), for the automatic stash, its redo and the toolbar's stash
  alike (`ProtectedChangesTests`).

## 16. Work breakdown

Each package is built in its own worktree on a branch made **from `phase/5-history`**
(`wip/p5-<name>`; a worktree may start from `master`, so its agent creates the branch from the phase
branch first), merged back with `--no-ff` after its tests pass and the solution builds, its worktree
removed at once and its fsmonitor daemon stopped (phase 3 and 4 handoffs; this phase's first worktree
had to be removed with `git worktree remove -f -f`, as its agent's lock outlived it). A package edits
only the files named for it; anything else goes through the lead. Every file has one owner.

| Package | Builds and owns |
|---|---|
| WP-L (done, `279dd8c`) | Phase 4's leftovers (D160) and the two waits of the baseline run |
| WP0 (lead) | **Core**: `Undo/` (`UndoEntry`, `UndoSequence`, the effect records, `UndoHistory`, `UndoScope`), `Graph/GraphFilter`, the result records of history, blame, reflog, backups, the tree's files, compare and search, `OperationRecord`'s fields and `IOperationLog`'s events, `GraphLine.IsDashed`, `SessionState.GraphFilters` and `ActivityLogHeight`, `AppSettings.ShowAllFiles`. **Git**: `IGitRepository`'s new members (`ReadFileHistoryAsync`, `ReadBlameAsync`, `ReadTreeFilesAsync`, `ReadReflogAsync`, `FindLostCommitsAsync`, `ReadBackupsAsync`, `CompareAsync`, `SearchAsync`, `ReadRefStatesAsync`, `RunUndoStepAsync`, the filter parameter of `LoadCommitsAsync`, `PushAsync`'s and `PushTagAsync`'s `PushOutcome`), `FakeRepositoryBase`, one stub file per package. **Queue and runner**: the record clock, the sequence numbers, `Started`, the record's fields, `GitCommand.RecordsStandardInput`. **App contracts**: `IGraphAreaPage`, the pages' host interfaces, `IRepositoryActions` additions, `RunOptions.Undo` with **every operation's scope and its `UndoCapture` call points as no-ops at every `RunAsync` and `WriteAsync` call site**, the recording hook in `RepositoryOperations.Run.cs` and `RepositoryViewModel.WriteAsync`, `RepositoryViewModel`'s hooks stubbed (`Page`, `ShowCommit`, `ShowInGraph`, `Reveal`, `Compare`, `Search`, `UndoHistory`), `EscapeOrder` 340, 370, 390, `ToolbarViewModel`'s new commands as stubs, `RepositoryMenus` and `OperationTexts.Menus` split into partial files (`.Pages.cs` for WP6, `.Filters.cs` for WP7). **Theme**: `Icons.axaml`, `THIRD-PARTY-NOTICES.md`, `Tokens.axaml`, `VcFlatButton`'s `selected`. **Tests**: `GitIsolation`/`RealApp` (the two clocks) |
| WP0's later additions | `RepositoryMenus.ActivityLog.cs` and `OperationTexts.Menus.ActivityLog.cs` (WP5); `Core/Undo/UndoChecks.cs` and `RepositoryOperations.Undo.cs` (WP3); the C12 stubs in `GitRepository.Branches.cs` and the checkouts' error display moved into `RepositoryOperations.Branches.cs` (WP4); `GraphSearchMarks` (WP7 fills it, WP2b draws it); `LeftPanelViewModel`'s hidden and soloed marks (WP7) |
| WP-S | 15.1's scenarios and `Phase5ScenarioTests`; the two projects (15.3); the driver files of 15.2 as their views arrive; `scripts/time-scripted-check.sh`; CLAUDE.md's rows |
| WP1 (Git: reading) | `GitRepository.FileHistory.cs` (file history, blame, the tree's files), `.Reflog.cs` (reflog, lost commits, the backups' reader on phase 4's parser), `.Compare.cs`, `.Search.cs`; their tests |
| WP2a (graph, data; first wave) | `GraphFilter` in `LoadCommitsAsync` and `CommitPager` (8.2), `GraphLayout`'s dashed stash lanes (10), `CommitGraphData`'s label filter; their tests and phase 1's graph tests that change |
| WP2b (graph, drawing; after the report) | `CommitGraphControl`'s dashes and search marks (3.3, 10), the reload bar (8.2); their tests |
| WP3 (undo) | Git half (first wave): `GitRepository.Undo.cs` (every effect and sequence of 7.3, the checks, the reads of 7.7), `GitRepository.Remotes.cs`'s `PushOutcome`. App half (after the report): `RepositoryOperations.Undo.cs` (recording, undo and redo, the histories, the toolbar's state and tooltips, `BackupsToKeep`, 7.10's restores), filling every operation's `UndoCapture` call points (in the `RepositoryOperations.*.cs` files, after WP4's changes to `Branches.cs` are merged), `WorkingChangesViewModel.cs` (the message put back), the restore bar's tie, `OperationTexts.Undo.cs`; 7.11's tests |
| WP4 (C12) | `GitFailureClassifier` (the count, the folder and ignored kinds), `GitRepository.Branches.cs` (`--no-overwrite-ignore`, the automatic stash's steps), `RepositoryOperations.Branches.cs`'s checkouts (9.1 to 9.3 on every path), the refusal texts of merge, pick, revert and pull (`RepositoryOperations.Remotes.cs`'s pull part), the stash apply pre-check (`RepositoryOperations.StashTags.cs`), `OperationTexts`; 9.4's tests |
| WP5 (activity log and shell) | `OperationQueue`/`OperationLog`/`GitCallLog`/`GitRunner` changes, `ActivityLogViewModel`/`ActivityLogView`, `CommitGraphView.axaml`'s grid under the graph area with its splitter and the session's height, the status bar's toggle, `MainWindow`'s key handlers (1.3), the toolbar's Search and Undo/Redo bindings; their tests |
| WP6 (pages and All files) | `FileHistoryViewModel`/`View` (with `DiffView.TakesFocusOnOpen` and `DiffViewModel.ShowsClose`), `BlameViewModel`/`View` with the gutter, `ReflogViewModel`/`View` with the backups list, "All files" in `CommitDetailsViewModel` and `RightPanelView`, `RepositoryMenus.Pages.cs`; their tests |
| WP7 (search, compare, solo and hide) | `SearchViewModel`/`SearchBarView`, `CompareViewModel`/`CompareView` in the right panel's slot, the left panel's marks and toggles (`LeftPanelView.axaml(.cs)`, `BranchDragService.cs`), `RepositoryMenus.Filters.cs`, the session's filters; their tests |
| WP8 | The scripted checks of the report, part by part (`Phase5Checks.*`, `Phase5Expectations*.cs`, `Phase5ExpectationTests*.cs`), and **every phase 0 to 4 test and expectation file that 14.1 changes** (the other packages send their changes to those files through the lead) |
| WP9 | The real-window pass for the RW checks: `Phase5RealWindowPass*.cs`, `RealApp.Phase5*.cs`, the Phase 5 links in `RealWindowTests.csproj`, and phase 0 to 4's real-window files that change |
| Lead | `RepositoryViewModel*.cs` (the hooks: pages, `ShowCommit`, `ShowInGraph`, `Reveal`, compare's place, search's place, the filter's reload, the undo state on the queue's `Changed`, `OnStatusApplied`/`ApplyRefs` feeding the pages), `RepoTabViewModel.cs`, `MainWindow.axaml`, `MainWindowViewModel.cs`, `AppSession.cs` and `TabServices.cs` (the histories, the clocks), the `.slnx`, CI, the docs, CLAUDE.md; before the gate, the reference run and the comparison script of 15.7 |

**Order**: WP0 first (contracts, stubs and every call point, so that packages build against each other:
phase 4's lesson). Then, side by side, the first wave: WP-S, WP1, WP2a and WP3's git half (no UI).
The report's checks (step 6) are written once WP-S has pinned the scenarios, reviewed, and committed
before any UI the checks test is built. Then the second wave: WP2b, WP3's app half (after WP4's
`Branches.cs`), WP4, WP5, WP6 and WP7, the lead joining their hooks. Then WP8 and WP9. Then the review
(step 25) and the gate.

## 17. Decisions

This design records these in `decisions.md` (the owner's answers are D153 to D166):

| # | Decision | Reason |
|---|---|---|
| D167 | The undo history (7): per worktree and session in `AppSession`, at most 100 entries; each action kind has fixed undo and redo sequences of effects from a closed set; values recorded by reading the declared refs and state before and after the action inside its own operation; undo and redo one queued operation each that simulates its checks in order and writes in a fixed order; strict order; outside changes never remove entries | A generic reverse of what changed cannot order a step (a branch is deleted only after switching away; a hard reset's files come back after its move) and one git call changes several things; per-worktree keys because HEAD, the index and files belong to a worktree; 100 bounds memory and the backups held back from the clean-up |
| D168 | What the history records and leaves out (7.2): stage, unstage, applying lines to the index, fetches, the clean-up, the resolver's resolutions and Abort (D165) are not recorded; a stopped action is one pending entry; undo is off while an operation is in progress; every destructive action of phases 2 to 4 has a stated way back (7.2's table) | Recording preparations would bury the actions people undo (GitKraken leaves them out); a stopped state cannot be recreated (D165) |
| D169 | Moving the checked-out branch back keeps later work and never loses ignored files: `update-index --refresh`, the ignored-files guard, `read-tree -m -u` and a compare-and-swap `update-ref` on `refs/heads/<b>`; never `reset --merge`, `reset --keep` or `switch -C`, never through `HEAD`'s link; undo's switches pass `--no-overwrite-ignore`; `GIT_REFLOG_ACTION` is never set (7.3) | 0.4's experiments: the others lose staged or partly staged work or break `@{-1}`; read-tree refuses falsely on stale stat data and overwrites ignored files without a word from Git 2.34 |
| D170 | Every undo or redo that changes a remote asks first (extends D155 beyond force pushes and deletions); the lease's expected value is explicit in both directions; a push's old value comes from git's output (`PushOutcome`), never from the remote-tracking ref (7.3, 7.5) | An ordinary push's undo rewinds or deletes a remote branch as a force push's does; the remote-tracking ref can be stale (0.5) |
| D171 | The backups an entry needs are kept by the clean-up while the entry exists (the union over the repository's worktrees), and still count toward D139's 50; a pop saves a `stash-…` backup as a drop does; an undone tag creation keeps its object in a `tag-undone-…` backup (7.8) | Snapshots and backups have no reflog, so only the clean-up could lose what an undo needs; a popped stash is otherwise unreachable |
| D172 | The backups list in the reflog page restores backups outside the session with the undo's effects (7.10); the restore bar is tied to its discard's entry both ways, which amends D62's list of when the bar goes (7.9) | D154's "older actions are restored from the reflog view and the backups"; a bar offering files Undo already put back would restore them twice and drop the redo |
| D173 | The activity log reads structured records, never the log file: per-worktree keys, session-wide sequence numbers taken at start, the dialog's failure summary set after the fact, notes for the clean-up's removals, operation calls kept as long as their record (64 MB budget) apart from a ring of 2,000 reads, `update-ref --stdin`'s input recorded, calls on a temporary index marked; displayed times from a record clock that both harnesses freeze, and backup names and the clean-up's "now" from a backup clock they fix; the welcome page's commands are not shown (2.3) | T4 asks for every command an operation ran; the log file is shared by instances and kept 14 days; the queue cannot know the dialog's summary; frozen clocks make pictures repeatable but leave no order, hence the sequences |
| D174 | Search gives git the graph's rows' SHAs (`--no-walk=unsorted --stdin`, in chunks): it searches exactly what is shown, hidden branches excepted; Files and Changes match a commit's change against its first parent; case is ignored (ASCII for paths and pickaxe); results are kept by SHA across graph reloads (3) | Hits arrive in graph order and never outside it; the first-parent rule matches what the right panel shows; a reload must not restart a long Changes search |
| D175 | File history is git's `log --follow` from a fixed start, without merges; blame is `blame --porcelain --no-textconv` of a commit or the working tree, honouring `blame.ignoreRevsFile`; both are pages with the history's diff beside or below the list by width; the details panel's "All files" carries D164 (4) | git's own rules (Q5); a fixed start keeps the list from following another branch's file |
| D176 | The graph area's pages (history, blame, reflog) hold no work, exclude the interactive list, lie under a diff and after it in the visual tree; opening one does not change the right panel until the user picks a row; closing one reveals the commit picked; `ShowCommit` shows any commit in the right panel, leaving the working row (11) | A page must not take the stage panel away unasked; a picked commit must be in view when the graph returns |
| D177 | Compare: two commits two-dot, branches three-dot by default with an "All differences" switch; names resolved to SHAs once; a file's diff from the side the list was computed from; links reveal without selecting (6) | Two-dot shows the other side's later changes reversed; a selection would end the comparison |
| D178 | Solo and hide act on branches only (and on whole remotes); hiding or soloing a local branch includes its remote-tracking upstream, recorded so that showing it again undoes both; the checked-out branch is always shown, with its label; the filter is git's `--exclude` and named refs, with a stdin fallback past 30,000 characters (amends D47 while a filter is on); a stash whose base is hidden is left out (amends D54 while a filter is on); filters of the 100 most recently used repositories are kept (8) | 0.6's measurements (the upstream keeps a hidden branch's commits on screen; `<r>/HEAD` too); D159's "remembered" must not end when a repository leaves the ten recent ones |
| D179 | C12's rules (9): every checkout of a commit the current branch reaches uses `--no-overwrite-ignore`, and stashes first when there are changes (never from a detached HEAD, never at HEAD's own commit, never for an undo); D153's sentence with git's count on every checkout path and on merge, pick, revert and pull refusals, an ignored-files text of its own, and D153's sentence replacing D91's explanation (git's output carries it); a stash's apply is checked by the app before git; a half-made automatic stash puts back what git deleted; an unstaged deletion a checkout undoes is accepted as git does | 0.7's experiments: an old commit overwrites and then deletes a now-ignored file; git's stash apply refuses only partly; git deletes untracked files before failing on a locked one |
| D180 | The reflog is read through git with two calls zipped by position; one reflog at a time (HEAD, a local branch); lost commits from one `rev-list --stdin` call; reached from menus that always exist (5) | No placeholder gives an entry's time with its index; reftable has no `.git/logs`; one call per SHA took 30 s or more |
| D181 | Phase 5's walk-through is two projects, `VisualCommit.VisualTests.Phase5` and `.Phase5Undo` (as D133 and D151), with check parts A, B, F, U and P; every undo check is one action per test method (15.3, 15.4) | The undo checks alone are about 100 operations, which one project's UI thread would run for 15 to 25 minutes; the letters keep check names apart from requirement numbers |
