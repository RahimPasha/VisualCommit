# Phase 4 design: rebase and conflict resolution

Working design notes for phase 4, written on 2026-10-12 before any phase 4 code exists. The
builders implement from this file; its final facts are folded into `architecture.md` at the end of
the phase, after which this file is kept as history, as `phase-3-design.md` is. Section 15 lists the
decisions this design makes; they are recorded in `decisions.md` as D139 to D151. Where this file
and merged code differ later, the code's doc comments say what is built; tell the lead.

Sources: `plan.md` "Phase 4", the phase 3 handoff's "For the next phase", decisions D83, D88, D89,
D102, D110, D128 and D135 to D138, the owner's answers below, and six research notes made by
sub-agents on 2026-10-12 (four maps of the code, two sets of experiments with Git 2.36). The
research notes lived in the session's scratch folder; everything this design relies on is copied
into section 0.

The owner's answers, taken as given:

1. (2026-10-11, D135) A rebase asks for no confirmation. When a rebase or a merge stops on a
   conflict, the user resolves it or aborts and is back where everything was. A rebase saves a
   backup ref of the branch's old tip first.
2. (2026-10-12, D136) Several commits are picked or reverted with one git command that takes the
   whole list, so that Continue, Skip and Abort act on the list.
3. (2026-10-12, D137) Backups are cleaned up automatically: after 30 days, or when more than a set
   number exist (the oldest first).
4. (2026-10-12, asked at the start of phase 4) **50** backups are kept besides the 30 days. The
   clean-up says **nothing on screen**; the log records each removal (phase 5's activity log shows
   the log).
5. (2026-10-12) The conflict resolver shows **ours and theirs side by side above the editable
   result**, across the graph area **and** the right panel's area; moving between conflicted
   files is done from its header.
6. (2026-10-12) The interactive rebase's list **replaces the graph**, in the graph's own order
   (**newest on top**); a row is dragged to move it; squash and fixup join the commit below
   (older); the right panel shows the selected commit.

Conventions, as in phase 3's design: `<b>` a local branch's short name, `<r>` a remote, `<sha>` a
full commit id, "short SHA" its first 7 characters, `{cur}` what HEAD is on as menus name it
(`RepositoryMenuContext.CurrentName`). The app passes git full ref names or full SHAs wherever git
accepts them; section 0 names the one place where it must not (`git rebase <upstream> <branch>`).
Every git call goes through `GitRunner` with D30, D68, D74, D102 and D128's environment.

## 0. Facts checked before deciding

Run with Git 2.36.0.windows.1 in throw-away repositories, isolated as the tests are
(`GIT_CONFIG_NOSYSTEM=1`, `GIT_CONFIG_GLOBAL=/dev/null`, `LANGUAGE=en`, `GIT_EDITOR=:`,
`GIT_SEQUENCE_EDITOR=:`, `GIT_LITERAL_PATHSPECS=1`). Where CI's Git 2.55 may differ, the Git-layer
tests run on both (phase 3's "two gits" rule).

### 0.1 Rebase

| What | Result |
|---|---|
| `git rebase main` on `topic` (diverged) | exit 0, stdout empty; stderr `Rebasing (1/2)\rRebasing (2/2)\r\r\x1b[KSuccessfully rebased and updated refs/heads/topic.` (the runner splits at `\r`; the last part starts with `ESC[K`, which must be removed before it is shown). `ORIG_HEAD` = the old tip. HEAD reflog `rebase (start): checkout main` … `rebase (finish): returning to refs/heads/topic` |
| Already up to date | stdout `Current branch topic is up to date.` (detached: `HEAD is up to date.`), exit 0, nothing written. Onto a descendant: a fast-forward, `Successfully rebased …` |
| `git rebase main topic` from another branch | checks `topic` out and **stays on it** after success, after `--abort` and when up to date. With `topic` checked out in another worktree: `fatal: 'topic' is already checked out at '<path>'`, exit 128 |
| The `<branch>` argument | git prefixes `refs/heads/` to it itself, so a full name `refs/heads/topic` would be looked up as `refs/heads/refs/heads/topic`, fail, and be taken as a commit (a detached rebase). Inference from git's `builtin/rebase.c`; WP1 pins it with a test. The design never passes it: another branch is checked out first (1.3) |
| A conflict stop (two conflicting commits) | exit 1; stdout `Auto-merging f.txt` / `CONFLICT (content): Merge conflict in f.txt`; stderr `Rebasing (1/3)error: could not apply dd76563... T1 topic edits one`, four `hint:` lines, `Could not apply dd76563... T1 topic edits one` |
| Files at a stop | `.git/rebase-merge/`: `head-name` (`refs/heads/topic`), `onto` and `orig-head` (full SHAs), `done` (the commands done, full SHAs), `git-rebase-todo` (left), `msgnum`/`end` (count every command, `label` and `exec` included), `message` (the message Continue uses), `author-script` (`GIT_AUTHOR_NAME='…'` / `GIT_AUTHOR_EMAIL='…'` / `GIT_AUTHOR_DATE='@<seconds> <zone>'`), `stopped-sha`, `patch`, `interactive` (also for a plain rebase), `rewritten-list` after the first commit. Also `.git/REBASE_HEAD` (= `stopped-sha`), `ORIG_HEAD`, `AUTO_MERGE`, `MERGE_MSG`. HEAD is detached |
| Stale files | `REBASE_HEAD` and `AUTO_MERGE` stay after a rebase or sequence finishes: neither means "in progress". Only `rebase-merge/` or `rebase-apply/` does |
| Stages in a rebase | stage 2 ("ours") = HEAD = the new base plus the commits already replayed; stage 3 ("theirs") = the user's commit being replayed. Markers `<<<<<<< HEAD` / `>>>>>>> <short> (<subject>)` |
| `git rebase --continue` after `git add` (`GIT_EDITOR=:`) | commits with `rebase-merge/message` (author and author date kept) and goes on; the next stop exits 1 with the same lines. It **starts the editor** on `.git/COMMIT_EDITMSG`, so `GIT_EDITOR=:` strips every line of the message that starts with `#` (`#12 issue line` lost). `-c commit.cleanup=whitespace` writes git's whole comment block into the message: unusable |
| Continue variants | unstaged or unresolved: stdout `f.txt: needs merge` / `You must edit all merge conflicts and then` / `mark them as resolved using git add`, exit 1, nothing changes. Resolved to "no change" (nothing staged): Continue **drops the commit silently** and goes on. The user committing first (`git commit --file=<new message>`) and then Continue: accepted, the user's commit and message kept, no extra commit; that commit's author date is the current time unless `GIT_AUTHOR_*` are given |
| `git rebase --skip` | drops the stopped commit, cleans the index and working tree itself (a staged resolution is lost), exit 0 at the end (`Successfully rebased …`) or 1 at the next stop |
| `git rebase --abort` | silent, exit 0; the branch back at `orig-head`, HEAD on that branch; HEAD reflog `rebase (abort): updating HEAD`, the branch's reflog untouched |
| Nothing in progress | `--continue`, `--skip`, `--abort`: `fatal: No rebase in progress?`, exit 128 |
| Dirty working tree | unstaged: `error: cannot rebase: You have unstaged changes.` / `error: Please commit or stash them.`, exit 1; staged: `error: cannot rebase: Your index contains uncommitted changes.` and the same second line; nothing starts. Untracked files only: rebases. With `rebase.autoStash=true` git stashes and re-applies (`Created autostash: …`, `Applied autostash.`) |
| Untracked file in the way | at the start: `error: The following untracked working tree files would be overwritten by checkout:` … `error: could not detach HEAD`, exit 1, nothing starts. In the middle: the pick is rescheduled (`hint: Could not execute the todo command` … `It has been rescheduled`), exit 1, **stopped with no conflicted file** |
| Commits in the range | a merge commit is dropped (the history is linearised; `--rebase-merges` keeps it); a commit whose patch is upstream is skipped (`warning: skipped previously applied commit …`); a commit that becomes empty is dropped by a plain rebase (`dropping <sha> <subject> -- patch contents already upstream`) and **stops** an interactive one (`The previous cherry-pick is now empty, possibly due to conflict resolution.` and a `CHERRY_PICK_HEAD` file next to `rebase-merge/`) |
| rerere with `autoUpdate` | `Staged 'f.txt' using previous resolution.`, exit 1, stopped with no unmerged path |
| Exit codes | 0 done; 1 with `rebase-merge/` (or `rebase-apply/`) present: **stopped**; 1 without it: refused, nothing changed; 128: fatal refusal (hook, another rebase, worktree, no rebase). A stop does not imply unmerged paths |
| A user's `rebase.backend=apply` | uses `.git/rebase-apply/` (no `applying` file) with other texts; `-i` always uses the merge backend |
| Options on 2.36 | `--no-update-refs` unknown (2.38+); `-c rebase.updateRefs=false` accepted; `--no-autostash`, `--merge` exist |

### 0.2 Interactive rebase

| What | Result |
|---|---|
| How git runs the sequence editor | one shell command: `sh -c '<GIT_SEQUENCE_EDITOR> "$@"'` with the todo's absolute path (forward slashes, spaces kept) as its argument; working folder the worktree's top. A `GitCommand`'s own environment overrides `GitRunner`'s defaults (`DefaultEnvironment.Concat(command.Environment)`), so one call can set its own editor while D102's `:` stays the default |
| `GIT_SEQUENCE_EDITOR="cp '<prepared todo>'"` | works with forward-slash and backslash Windows paths, spaces, an apostrophe written `'\''`, CRLF lines. A UTF-8 BOM breaks it (`error: invalid line 1: ﻿pick …`) |
| Todo syntax | full SHAs without subjects, `pick`/`p`, `drop`, `squash`, `fixup`, `reword`, `exec`, `label` (2.18+). `fixup -C` exists in 2.36 but not in 2.30, the app's minimum |
| Unchanged leading commits | keep their SHAs (git fast-forwards over them); an identity todo leaves the tip as it was |
| `squash` with `GIT_EDITOR=:` | messages joined by a blank line, git's `#` lines removed, **and the commits' own `#` lines lost** |
| `reword` with `GIT_EDITOR=:` | message "unchanged" but `#` lines stripped, and the commit rewritten |
| A new message through `exec git commit --amend --only --no-verify --allow-empty --cleanup=whitespace --quiet --file='<msg>'` | works: message exactly the file, `#` lines kept, author and date kept. Hazard: if the pick before it is skipped (or dropped as empty), the `exec` amends whatever HEAD is (another commit's message replaced). A guard works: `label vc-1` before the pick, and `exec if [ "$(git rev-parse -q --verify HEAD^)" = "$(git rev-parse -q --verify refs/rewritten/vc-1)" ]; then git commit --amend …; fi`: clean, skipped and resolved-then-continued cases all right; `refs/rewritten/vc-1` exists during the rebase and is deleted at its end and at `--abort` |
| Message files next to the todo | the sequence editor `cp '<msg>' "$(dirname "$1")/vc-message-1" && cp '<todo>' "$1"` copies a message into `.git/rebase-merge/`; the exec reads it with `--file="$(git rev-parse --git-path rebase-merge/vc-message-1)"`; git deletes them with the folder at the finish and at `--abort` |
| `exec` output | every exec prints `Executing: <command>` on stderr; `exec` and `label` lines count in `Rebasing (n/m)`, `msgnum` and `end` |
| `rebase.missingCommitsCheck=error` | a todo that leaves a commit out stops with `Warning: some commits may have been dropped accidentally.` …; an explicit `drop <sha>` line passes |
| Invalid todo | `error: invalid line 4: …` + `You can fix this with 'git rebase --edit-todo' …`, exit 1, **left in progress** (HEAD detached at `onto`, `rebase-merge/` without `done`) |
| The editor command fails | `error: There was a problem with the editor '<cmd>'.`, exit 1, **nothing started** |
| Only `drop` lines | the branch moves to `onto`; empty todo: `error: nothing to do`, exit 1, nothing changed |

### 0.3 Cherry-pick and revert lists (D136)

| What | Result |
|---|---|
| `git cherry-pick A B C`, B conflicts | exit 1; stdout `[main b68aa9c] A adds a.txt` … `CONFLICT (content): Merge conflict in f.txt`; stderr `error: could not apply 4f7880e... B changes line2` and hints. Files: `CHERRY_PICK_HEAD` (B), `MERGE_MSG` (`B changes line2\n\n# Conflicts:\n#\tf.txt\n`), `AUTO_MERGE`, `sequencer/head` (HEAD before the command), `sequencer/abort-safety` (HEAD after the last pick), `sequencer/todo` (`pick 4f7880e B changes line2` / `pick cfce4e5 C adds c.txt`: the stopped pick stays first) |
| A single `git cherry-pick B` that stops | `CHERRY_PICK_HEAD`, `MERGE_MSG`, `AUTO_MERGE`, **no `sequencer/`** |
| Resolve, `git add`, `--continue` | exit 0; commits B with `MERGE_MSG` less its `#` lines (B's author and date), then picks C; `sequencer/` removed |
| Resolve, `git commit --file=-` with a new message, then `--continue` | exit 0, picks only C; the new message kept (D110) |
| `--skip` at B | exit 0, drops B and picks C; **a staged resolution is lost without a word** |
| `--abort` at B | exit 0, `reset --merge` to `sequencer/head` (before A): keeps untracked files and unstaged changes to other files, **drops staged ones**. After HEAD moved (B committed by hand): `warning: You seem to have moved HEAD. Not rewinding, check your HEAD!`, exit 0, HEAD kept |
| `git revert --no-edit C B A` | the same shape: `REVERT_HEAD`, `MERGE_MSG` `Revert "B changes line2"\n\nThis reverts commit …\n\n# Conflicts:…`, `sequencer/opts` (`edit = false`), todo `revert …` lines; `git revert --continue` and `--abort` as for picks |
| An empty pick in a list (already on HEAD) | exit 1, `CHERRY_PICK_HEAD`, `MERGE_MSG`, sequencer kept; stderr `The previous cherry-pick is now empty, possibly due to conflict resolution.` / `If you wish to commit it anyway, use:` / `    git commit --allow-empty` / `Otherwise, please use 'git cherry-pick --skip'`. `--continue` repeats it (exit 1); `--skip` goes on |
| A pick resolved to "ours" (nothing staged) | `--continue` exits 1 with the same "now empty" text; `--skip` goes on |
| An empty revert in a list | exit 1, **no `REVERT_HEAD`**, `MERGE_MSG` and `sequencer/` kept, HEAD = `abort-safety`, only the status text on stdout ending `nothing to commit, working tree clean`; `--continue` exits 0, leaves the empty revert out and reverts the rest |
| A later pick refused without a conflict (an untracked file in the way) | exit 128, `error: The following untracked working tree files would be overwritten by merge:` … `fatal: cherry-pick failed`; `sequencer/` kept with the refused pick first, **no `CHERRY_PICK_HEAD`, no `MERGE_MSG`**, HEAD = `abort-safety`. `--continue` then **silently drops the refused commit** |
| A list refused at its start (a staged change anywhere, or a local change the first pick touches) | exit 128 (`error: your local changes would be overwritten by cherry-pick.` / `fatal: cherry-pick failed`, or the overwritten-by-merge text), HEAD unchanged, **`sequencer/` written anyway** (head = abort-safety = HEAD). `--abort` there **drops the staged change**; `--quit` removes `sequencer/` and keeps it |
| Wrong verb | during a revert sequence: `git cherry-pick --continue` → `error: cannot cherry-pick during a revert.` (128); `git cherry-pick --abort` aborts the revert |
| Nothing in progress | `git cherry-pick --continue` / `--abort`: `error: no cherry-pick or revert in progress` / `fatal: cherry-pick failed` (128); `--skip`: `error: no cherry-pick in progress`; `git revert --continue`: `… fatal: revert failed` |
| Unmerged paths left | `--continue` (pick, revert, merge) and `git commit`: stdout `U\tf.txt`, stderr `error: Committing is not possible because you have unmerged files.` … `fatal: Exiting because of an unresolved conflict.` (128). A rebase says `f.txt: needs merge` (exit 1, 0.1) |
| A single pick plus `--skip` | exit 0, `CHERRY_PICK_HEAD` gone, HEAD unchanged |
| `git merge --continue` | exists in 2.36; commits `MERGE_MSG` less `#` lines; with nothing staged after a resolution to "ours" it still makes the merge commit |

### 0.4 Conflicts

| What | Result |
|---|---|
| Stage ids | `git status --porcelain=v2 -z`: `u <XY> <sub> <m1> <m2> <m3> <mW> <h1> <h2> <h3> <path>`, zeros for a missing stage; `sub` is `S...` for a submodule. Phase 3's parser keeps only the path (`GitRepository.Changes.cs:80-88`) |
| Reading a stage | `git cat-file blob <oid>` is byte-exact in the repository's form; `git cat-file --filters --path=<p> <oid>` gives the working-tree form (CRLF under `core.autocrlf=true`) and writes nothing. `git checkout-index --stage=all --temp` writes temp files **into the working tree**: not used |
| Kinds (git's words, XY) | both modified `UU` (text: markers; **binary or symbolic link: no markers, ours in the working tree**; submodule: `S...`, working tree untouched); both added `AA` (markers, no stage 1); deleted by them `UD` (ours in the tree); deleted by us `DU` (theirs in the tree); added by us `AU` / by them `UA` (a rename/rename, or a file/folder clash where git moved ours aside as `<p>~HEAD`); both deleted `DD` (no file) |
| Taking a side | `git checkout --ours|--theirs -- <p>` writes that stage in working-tree form and **does not** mark the path resolved; a missing side: `error: path '<p>' does not have our version` (or `their`), exit 1. **`git restore --ours` on a path without stage 2 deletes the file silently**: never used |
| Marking resolved | `git add -- <p>` (accepts markers without a word: `git diff --cached --check` finds them, `leftover conflict marker`); `git rm -- <p>` for a deleted side and for `DD` |
| Undoing a resolution before the commit | `git update-index --unresolve <p>` (no `--`) or `git checkout -m -- <p>`; `git checkout -m` on a path that is not unmerged **discards unstaged edits**: never run on such a path |
| Marker labels (merge, cherry-pick, revert, rebase, stash) | `HEAD` / the merged name as typed; `HEAD` / `<short> (<subject>)`; `HEAD` / `parent of <short> (<subject>)`; `HEAD` / `<short> (<subject>)`; `Updated upstream` / `Stashed changes`. A side whose path differs gets `:path` (`HEAD:rm2.txt`) |
| Hunks | the working-tree file uses the user's `merge.conflictStyle` (`merge`, `diff3`, `zdiff3`) and `conflict-marker-size`; the default style joins conflicts three or fewer lines apart into one block with the common lines on both sides. `git merge-file` cuts hunks differently from the merge in some cases: not used to recompute git's hunks |
| `git mergetool` with stdin closed | without `merge.tool`: it guesses a tool and **prompts anyway** (`Hit return to start merge resolution tool (vimdiff): `), exit 1, and leaves `<name>_BACKUP_<pid>.<ext>`, `_BASE_`, `_LOCAL_`, `_REMOTE_` files in the working tree. With `--tool=<t> --no-prompt` and a configured tool it runs `mergetool.<t>.cmd` (or git's known command line for `<t>`) in the worktree's top folder with `$BASE`, `$LOCAL`, `$REMOTE` (temp files in the working tree) and `$MERGED`; it waits until the tool exits; on success it runs `git add` itself and leaves `<file>.orig` (`mergetool.keepBackup`, default true). Without `trustExitCode` success is judged by the file's modification time, which was wrong in 1 of 6 quick runs of a scripted tool; with `trustExitCode=true` exit 0 stages the file even with its markers. Deletion, symbolic-link and submodule conflicts always prompt and never run the tool. An unavailable tool: `The merge tool <t> is not available as '<t>.exe'`, exit 1, temp files left. `git config --get merge.tool` takes 0.06 s; `--tool-help` 18 s |
| rerere | `Resolved 'f.txt' using previous resolution.`: the path stays `UU` with no markers left |

### 0.5 Reftable, `TempRepo`, the clone of check 13

| What | Result |
|---|---|
| Git 2.36 and reftable | `git init --ref-format=reftable` exits 129 (unknown option); `git rev-parse --show-ref-format` **prints `--show-ref-format` and exits 0**. Under reftable (Git 2.45+; from git's documentation, not run here) `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `REBASE_HEAD`, `ORIG_HEAD` and `AUTO_MERGE` are refs, `MERGE_HEAD` and `FETCH_HEAD` stay files, and the repository's config has `extensions.refStorage = reftable` |
| `git rev-parse --verify --quiet CHERRY_PICK_HEAD` | the SHA while a pick is stopped, exit 1 otherwise; **a branch named `CHERRY_PICK_HEAD` answers too**. `--symbolic-full-name` tells them apart (`CHERRY_PICK_HEAD` against `refs/heads/CHERRY_PICK_HEAD`; with both, an `ambiguous` error and exit 0) |
| `TempRepo` and editors | `TempRepo`'s environment sets no `GIT_EDITOR` or `GIT_SEQUENCE_EDITOR`, and `GitIsolation` does not remove the machine's `GIT_EDITOR`, `GIT_SEQUENCE_EDITOR`, `EDITOR` and `VISUAL`: a scenario that runs `git rebase --continue` started the editor (`vi` when nothing is set) |
| Dates of rewritten commits | with `GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` set, a rebase keeps each commit's author date and gives every rewritten commit the environment's committer date: the app's rebases in the tests are reproducible (D72) |
| The 100k-commit clone over HTTP | `AuthRemoteServer` serves a clone (its tests clone through it); a protocol-v2 clone's pack request is the `command=fetch` POST that `HoldNextFetchPack` holds. The server holds after the first side-band-2 packet with a `%`, which for 100,002 objects is `Counting objects:   0% … 3%` (one line per percent, several per packet). The shared 100k repository is not bare, so the server cannot serve it in place |

## 1. Rebase onto a commit or a branch

### 1.1 Where it is offered

| Menu (builder) | Item | Runs |
|---|---|---|
| A commit (`ForCommit`), group 2 after "Revert this commit" | `Rebase {cur} onto this commit` | `git rebase <sha>` |
| A local branch other than the current one (`ForLocalBranch`), group 1 after the two merges | `Rebase {cur} onto {b}` | `git rebase refs/heads/<b>` |
| A remote branch (`ForRemoteBranch`), group 1 after the two merges | `Rebase {cur} onto {r}/{b}` | `git rebase refs/remotes/<r>/<b>` |
| Branch A dropped onto branch B (`ForDrop`), A local and checked out | `Rebase {A} onto {B}` | `git rebase <B's full ref>` |
| A dropped onto B, A local and not checked out | `Check out {A} and rebase it onto {B}` | a checkout of A (phase 3's, with its detached-HEAD question and backup), then `git rebase <B>` |

A drop's merge items change the target ("Merge A into B"); its rebase items change the source
("Rebase A onto B"): that is what the gesture means for a rebase (GitKraken's reading too). A
remote branch is never rebased: a drop whose source is a remote branch offers no rebase item, as
phase 3 offers no push for one. A drop onto a remote branch offers the rebase items after the two
push items.

Disabled reasons, the object's own first (phase 3's order rule, menus-map 1):

1. The target is already in the branch's history: a commit reachable from HEAD →
   `This commit is already on {cur}` (phase 3's `AlreadyOn`); a branch whose tip is reachable from
   the rebased branch → `{cur} is up to date with {b}` (`UpToDateWith`). Git would say "Current
   branch … is up to date", or, worse, flatten a merge above the target (0.1). For a drop the
   reachability is read before the menu opens (`DropFacts`, which today reads
   `merge-base --is-ancestor` only for a local target: phase 4 reads it for a remote target too).
   For the commit and branch menus it comes from the loaded graph (`IsReachableFromHead`, and a new
   `RepositoryMenuContext.TipsOnHead`: the SHAs of the branch tips that HEAD reaches, computed when
   the context is built); a commit the graph has not reached yet counts as not on HEAD, and git
   decides (phase 3's rule).
2. For "Check out {A} and rebase it onto {B}": A checked out in another worktree →
   `{A} is checked out in another worktree ({folder})` (D120's text).
3. HEAD detached → `Check out a branch to rebase` (new; as merge, D142).
4. Unborn → `Make the first commit first`.
5. An operation in progress → `Finish or abort the {kind} first`.

Uncommitted changes do not disable the items: git refuses a dirty tree unless the user set
`rebase.autoStash`, and the app follows git's configuration (Q5). Git's refusal opens the error
dialog (1.5).

### 1.2 Backup and no question

No confirmation (D135). Inside the operation, just before the rebase starts, the app saves
`refs/visualcommit/backup/rebase-<time>/<b>` pointing at the branch's old tip (one
`BackupRequest("rebase", [BackupEntry(<b>, <old tip>)])`, phase 3's batch). Every rebase the app
starts saves one: rebase onto, an interactive rebase, a squash, a pull that rebases (configured or
chosen, section 6). A rebase of a detached HEAD cannot happen (1.1, 3). The backup is saved also
when the rebase then says "up to date"; the clean-up (section 9) takes care of the extra.

### 1.3 What runs

`RepositoryOperations.RebaseAsync(onto)`: one local operation "Rebase {cur} onto {target}"
(`RefreshScope.Refs`, prompts `Ask` as phase 3's local operations). Inside it: read HEAD and refuse
when it moved since the click (D124's check: "HEAD moved while this waited: …"); refuse when an
operation is in progress (fresh status, `RefuseWhileInProgressAsync`); save the backup; run
`git rebase <onto>`.

"Check out {A} and rebase it onto {B}" is one operation of several steps (`HasSeveralSteps`):
phase 3's checkout of A (its detached-HEAD confirmation comes before the operation, as
`CheckoutAndMergeAsync` does), then the rebase. The app never passes `git rebase` a branch
argument (0.1).

The app passes no option that changes git's configured behaviour: not `--merge`, not
`--no-autostash`, not `--rebase-merges`, not `-c rebase.updateRefs=false` (Q5). A user's
`rebase.backend=apply` gives `rebase-apply/`, which phase 3 already reads as a rebase.

### 1.4 How it ended

Classification after `git rebase` (also used by every rebase command of sections 2, 3, 5 and 6,
in `GitRepository`; `RebaseResult`):

| Exit | After | Result | Status bar |
|---|---|---|---|
| 0 | stdout has `is up to date` | `UpToDate` | `{b} is already up to date with {target}` |
| 0 | otherwise | `Rebased` (the new tip) | `Rebased {b} onto {target}` |
| non-zero | `rebase-merge/` or `rebase-apply/` exists | `Stopped` (conflicted file count from the status) | `Rebase stopped: {N} files have conflicts` or, with none, `Rebase stopped at {short} "{subject}"` (warning tone, phase 3's conflict report: no dialog) |
| non-zero | neither | a `GitException`: nothing changed | `Rebase {cur} onto {target} failed` and the error dialog "Rebase failed" with git's output |

The error dialog's summary for the refusals git words in 0.1: unstaged or staged changes →
`Git rebases only a clean working tree: commit or stash your changes first.`; an untracked file in
the way → `Untracked files are in the way: move or remove them first.`; others → the default
`Rebase {cur} onto {target} failed.` (the text table, 10.1, is binding).

### 1.5 After the rebase

The graph reloads (refs changed). The rewritten branch's new tip is selected when the selection
was on a commit the rebase replaced (the old SHAs are gone; phase 3's `OnGraphSwapped` would drop
the selection). The working-changes row is unaffected.

## 2. Interactive rebase

### 2.1 Where it is offered

| Menu | Item | The list holds |
|---|---|---|
| A commit (`ForCommit`), right after "Rebase {cur} onto this commit" | `Interactive rebase of {cur} from here…` | the commits of `<sha>..HEAD`; the base is `<sha>`, which stays as it is |
| A local branch other than the current one | `Interactive rebase of {cur} onto {b}…` | the commits `git rebase -i <b>` would list (2.3) |
| A remote branch | `Interactive rebase of {cur} onto {r}/{b}…` | the same with the remote branch |
| A drop, A checked out | `Interactive rebase of {A} onto {B}…` | the same |
| A drop, A not checked out | `Check out {A} and rebase it onto {B} interactively…` | the same; the checkout happens when Start is clicked, as the first step |

"From here" is enabled only for a commit on HEAD's **linear first-parent line**: HEAD, its first
parent, and so on until the first merge commit (which may be the base itself) or the root. Reasons,
in order: `{cur} has no commits after this one` (the commit is HEAD's); `This commit is not on
{cur}` (not reachable); `A merge lies between this commit and {cur}` (reachable but not on the
linear line); detached, unborn, in progress as in 1.1. The line is computed by a new
`CommitGraphData.LinearLineFromHead()` (cached like `IsReachableFromHead`) and given to the menu
as `CommitMenuInfo.HeadLineDepth` (an init property: the distance from HEAD, null when not on the
line).

The onto items share 1.1's reasons, plus: the range has merge commits → `A merge lies between {b}
and {cur}: interactive rebase would flatten it` (read with the list, 2.3; the item opens, and the
list shows the reason instead of rows: see 2.2). No `--root`: the first commit of a history can be
neither edited nor squashed in phase 4 (D143).

### 2.2 The list (owner's answer 6)

`InteractiveRebaseViewModel` (`RepositoryViewModel.InteractiveRebase`; `IsInteractiveRebaseOpen`),
drawn by `Views/Rebase/InteractiveRebaseView` in the graph area in place of the graph, as the diff
view is; the banner, the left panel and the right panel stay. Opening it closes an open diff.

```
┌ Interactive rebase of feature onto main ─────────────── 3 commits ┐  header, 36
│ ⠿ [Pick   ▾] c3d4e5f Add tests                      Test Author   │  rows, 30
│ ⠿ [Squash ▾] b2c3d4e Fix typo                       Test Author   │
│ ⠿ [Reword ▾] a1b2c3d Add login                      Test Author   │
│               New message: Add sign-in               [Edit…]      │  a second line, 22
│   Base  9f8e7d6 Release 1.0                                      │  the base row, 30
│                                      [Cancel]  [Start rebase]     │  footer, 44
└───────────────────────────────────────────────────────────────────┘
```

- **Header**: `Interactive rebase of {b} onto {target}` (from here: `Interactive rebase of {cur}
  from {short}`), and on the right `{N} commits` (singular for one).
- **Rows**, newest on top (the graph's order), 30 high: a drag handle (the `IconGrip` outline), the
  action box, the short SHA (secondary), the subject, the author (hidden below 520 wide, as the
  graph's columns give way). The action box is a combo box (`ActionBox`) with Pick, Reword,
  Squash, Fixup, Drop; the keys `p`, `r`, `s`, `f`, `d` on a selected row set them too (git's own
  letters). Clicking a row selects it; the right panel shows that commit's details
  (`CommitDetailsViewModel`, as for a graph row).
- **Pick**: as it is. **Drop**: the row's SHA and subject struck through in the disabled colour.
- **Reword**: choosing it opens the message dialog (`MessageDialogViewModel`, 7.2) titled
  `Reword {short}`, filled with the commit's message, button `Save`; Cancel there puts the action
  back to what it was. The row gets a second line `New message: {new subject}` with an `Edit…`
  button that opens the dialog again.
- **Squash and fixup** join the nearest row below that is neither (the older commit they meld into,
  "the target"). Their rows are indented 16 and joined to the target by a line in the lane colour 0
  (`VcLane0Brush`) at the handle's place. The target gets a second line: for a group with at least
  one squash, `Message: {subject of the group's message}` with `Edit…`; the group's message starts
  as git's own: the messages of the target and every squash row in history order (oldest first),
  joined by a blank line; fixup rows add nothing. A group of fixups only keeps the target's message
  and has no second line.
- The bottom row (the oldest commit) cannot be squash or fixup: those entries of its box are
  disabled with the tooltip `The oldest commit has nothing below it to join`. A row that becomes
  the bottom row by a move while it is squash or fixup turns into Pick.
- **Moving rows**: a drag on a row (4 px threshold, as D87), with a 2 px line in
  `VcDragTargetBrush` where it will go; Alt+Up and Alt+Down move the selected row. The base row
  never moves.
- **The base row**: `Base` in the secondary colour, the short SHA and subject; not selectable.
- **Footer**: `Cancel` and `Start rebase` (`VcPrimaryButton`). `Start rebase` is disabled while
  every row is still Pick in its first order (`Change an action or the order first`).
- **A range with merges** (2.1): the list area shows `A merge lies between {target} and {b}:
  interactive rebase would flatten it.` and only `Cancel`.
- **Esc or Cancel** closes the list; when anything was changed it asks first (`Discard the changes
  to this list?`, buttons `Keep editing` and `Discard`).

### 2.3 Which commits are listed

For "from here": `git rev-list --reverse --topo-order <sha>..HEAD` (the line is linear, 2.1). For
an onto target: the commits git itself would put in the todo, `git rev-list --reverse
--topo-order --no-merges --right-only --cherry-pick <target>...<branch>` (git's own list for
`rebase -i`: commits already upstream by patch are left out), plus `git rev-list --merges
--count <merge-base>..<branch>` for the merge check. A test (`InteractiveRebaseListParityTests`)
compares the app's list with the todo git writes for the same rebase (captured by a sequence editor
that copies the todo out and fails, which leaves nothing started, 0.2), on 2.36 and CI's git.

### 2.4 What Start runs

One local operation "Interactive rebase of {b}" (`HasSeveralSteps` when it checks out first).
Inside it: refuse when the branch's tip moved since the list was read (`{b} changed while the list
was open: open it again.`); for the drop's variant, the checkout; the backup (1.2); then
`git rebase -i <base or target>` with `GIT_SEQUENCE_EDITOR` set for this call only (2.5). The list
closes when the operation ends with the rebase done or stopped; when git refused before anything
started (a dirty tree, a hook), the list stays open as it was and the error dialog shows git's
output (`Interactive rebase failed`).

### 2.5 The todo the app writes (`RebaseTodo`, Core, pure)

Built from the rows in git's order (oldest first). Full SHAs, no subjects, no comments, LF line
ends, UTF-8 without BOM, an explicit `drop` line for every dropped commit (so that
`rebase.missingCommitsCheck` passes). For each row group:

- Pick: `pick <sha>`. Drop: `drop <sha>`.
- Reword: `label vc-<n>` / `pick <sha>` / the guarded amend (below) with message file `vc-message-<n>`.
- A squash group (target T, then its squash and fixup rows in history order): `label vc-<n>` /
  `pick <T>` / `fixup <sha>` for every member (squash rows too: the message comes from the file) /
  the guarded amend with the group's message.
- A fixup-only group: `pick <T>` / `fixup <sha>` …

The guarded amend (one line; `<n>` the group's number):

```
exec if [ "$(git rev-parse -q --verify HEAD^)" = "$(git rev-parse -q --verify refs/rewritten/vc-<n>)" ]; then git commit --amend --only --no-verify --allow-empty --cleanup=whitespace --quiet --file="$(git rev-parse --git-path rebase-merge/vc-message-<n>)"; fi
```

It keeps the message exactly as typed (`#` lines included, D68), the author and author date; it
amends only when the group made exactly one commit on top of its label, so a skipped or emptied
pick never rewrites another commit (0.2). `--no-verify`: git's own reword and continue run neither
`pre-commit` nor `commit-msg` (0.1).

Delivery: the app writes the todo as `todo` and each message as `vc-message-<n>` into a new folder
under the system temp folder (`visualcommit-rebase-<guid>`), and sets
`GIT_SEQUENCE_EDITOR` to `cp '<msg 1>' "$(dirname "$1")/vc-message-1" && … && cp '<todo>' "$1"`
(paths with forward slashes, single-quoted for `sh`, `'` written `'\''`). The messages then live in
`.git/rebase-merge/` and go with it at the finish or abort, so a Continue from a terminal works the
same. The temp folder is deleted when git returns. When git exits non-zero with `rebase-merge/`
present but no `done` file, the app's own todo was refused (an invalid line): the app runs
`git rebase --abort` and reports the failure with git's output (a fault of the app, logged at
Error).

### 2.6 Stops during an interactive rebase

They are rebase stops (5): Continue, Skip, Abort. A commit that becomes empty stops an interactive
rebase (0.1, with a `CHERRY_PICK_HEAD` next to `rebase-merge/`): the banner says so (5.2) and
Continue leaves it out. A failed `exec` stops with git's `Execution failed: …` text; the banner's
generic stop text applies.

## 3. Squash several selected commits

- **Menu**: `ForCommits`, first item of group 1 (the requirements' order: "Squash, compare,
  cherry-pick several"): `Squash {N} commits…`.
- **Enabled when** the selected commits are a contiguous first-parent chain on HEAD's linear line
  (every commit's first parent is the next older selected one, 2.1's line), none is a merge, the
  oldest is not the root, HEAD is on a branch, nothing is in progress. Reasons, in order: any merge
  → `Merge commits cannot be squashed`; not all on HEAD → `Only commits of {cur} can be squashed`;
  not a chain → `Select commits that follow one another`; not on the linear line → `A merge lies
  between these commits and {cur}`; the root among them → `The first commit cannot be squashed`;
  detached → `Check out a branch to squash`; in progress → `Finish or abort the {kind} first`.
- **The dialog**: the message dialog (7.2) titled `Squash {N} commits`, filled with git's squash
  message (the commits' messages oldest first, joined by a blank line), button `Squash`.
- **What runs**: one local operation "Squash {N} commits": the HEAD check (D124), the backup (1.2),
  then an interactive rebase from the oldest selected commit's parent with the todo of 2.5: the
  selected commits as one squash group with the dialog's message, every later commit `pick`.
- **Result**: `Squashed {N} commits into {short}` (the new commit), which is selected.

## 4. Several picks and reverts with git's list (D136)

`GitRepository.CherryPickAsync(IReadOnlyList<string> commits, …)` runs `git cherry-pick <A> <B>
<C>` (oldest first by row, as phase 3 orders them) and `RevertAsync(IReadOnlyList<string>, …)` runs
`git revert --no-edit <C> <B> <A>` (newest first). One commit keeps phase 3's single call and
results. A multi-revert with a merge stays disabled (`Revert merge commits one at a time`).

How a list ended (`SequenceResult(ApplyResult Outcome, int Applied)`; the app's own call, so it
knows the list):

| After the call | Outcome |
|---|---|
| exit 0 | `Applied` (all) |
| `CHERRY_PICK_HEAD` / `REVERT_HEAD` present (read as in 8.1), conflicts | `Conflicts` (stopped): the banner |
| the same, no conflicts and the index equals HEAD | `Empty` (stopped): the banner offers Skip (5) |
| `sequencer/` present, no pick head, HEAD = `sequencer/head` (nothing applied) | refused at the start: the app runs `git cherry-pick --quit` (`git revert --quit`), which removes `sequencer/` and keeps the user's changes (0.3), and fails with git's output |
| `sequencer/` present, no pick head, HEAD moved | a later commit was refused without a conflict (or an empty revert): stopped, `Applied` = the commits made; the banner offers Skip and Abort, not Continue (5.1) |
| none of these, non-zero | a `GitException` |

`Applied` is the number of todo commands git has left behind: the list's length less the lines of
`sequencer/todo` (the stopped command is still its first line, 0.3). The status bar says
`Cherry-picked {K} of {N} commits; stopped at {short} with conflicts in {M} files` and the rest
waits in git's list (phase 3's "not picked" wording goes: the banner names what waits).

## 5. Continue, Skip and Abort

### 5.1 What each state offers

The state is `InProgressState` as phase 3 reads it, with what section 8 adds: `Sequenced` (a
`sequencer/` folder), the rebase's stopped commit, its subject and its place (`Done` of `Total`,
counting only the commands that apply a commit: `pick`, `reword`, `edit`, `squash`, `fixup`,
`merge` and their letters, in `done` and in `git-rebase-todo`), and `Empty`. "Conflicts" is the
number of conflicted files of the status.

| State | Continue | Skip | Abort |
|---|---|---|---|
| Merge | enabled with 0 conflicts: commits the merge (5.4) | none (git has no skip for a merge) | phase 3's |
| A single cherry-pick or revert (no `sequencer/`), not empty | enabled with 0 conflicts: commits it (5.4) | enabled: `git cherry-pick --skip` (`git revert --skip`), which ends it with HEAD unchanged | phase 3's |
| A pick or revert in a list (`Sequenced`, pick head present), not empty | enabled with 0 conflicts: commits it and goes on with the list (5.4) | enabled: `--skip` | phase 3's: the whole list is undone |
| A pick or revert (single or in a list) that is `Empty` | disabled: `This cherry-pick changes nothing: skip it` (`revert`) | enabled | phase 3's |
| A list stopped between commits, HEAD moved since git's last pick (`AbortKeepsHead`, D110: the stopped pick was committed in a terminal) | enabled: `--continue` picks the rest | disabled: `There is nothing to skip: the stopped commit was committed` (git: `error: there is nothing to skip`) | phase 3's (keeps HEAD) |
| A list stopped between commits, HEAD where git left it (a commit refused without a conflict, or an empty revert, 0.3) | disabled: `Git would leave {short} out without a word: skip it or abort` | enabled | phase 3's |
| Rebase | enabled with 0 conflicts (5.4) | enabled: `git rebase --skip` | phase 3's |
| `git am` | none: the banner keeps phase 3's "continue in a terminal" | none | phase 3's |
| Conflicts without an operation (a stash that did not apply cleanly) | none | none | none (phase 3) |

Skip loses the resolution of the stopped commit (0.1, 0.3), so it asks first (7.2) and saves a
backup like Abort's: `skip-<time>` (HEAD) and `skip-<time>-changes` (`SnapshotTrackedChangesAsync`),
D83's scheme. Continue asks nothing: it only adds history.

### 5.2 The banner

`ConflictBanner` gains `BannerContinueButton` and `BannerSkipButton` left of `BannerAbortButton`,
in the same style (`VcActionButton`, 26 high), visible as 5.1 says, a disabled one with its reason
as its tooltip (`ToolTip.ShowOnDisabled`). `ConflictBannerViewModel` gains `ContinueText`
(`Continue`), `HasContinue`, `CanContinue`, `ContinueToolTip`, `ContinueCommand`, and the same for
Skip (`Skip`). Its texts (10.3 adds the tooltips):

- Merge: `Merging "{subject}". {N} files have conflicts.` / with none: `Merging "{subject}".
  Conflicts resolved: continue to commit the merge.`
- A single pick: `Cherry-picking {short} "{subject}". {N} files have conflicts.` / `… Conflicts
  resolved: continue to commit it.`; a revert likewise with `Reverting`.
- A pick in a list: the same, then ` {M} more commits wait.` (` 1 more commit waits.`).
- Empty: `Cherry-picking {short} "{subject}" changes nothing: its changes are already here. Skip it
  to go on.` (revert: `… it is already undone. Skip it to go on.`), then the list's sentence.
- A list stopped between commits: with `AbortKeepsHead`: `Cherry-pick paused: {M} more commits
  wait. Continue picks them.`; otherwise: `Cherry-pick stopped at {short} "{subject}": git could
  not pick it. Skip it, or abort.` (the commit and subject from `sequencer/todo`'s first line).
- Rebase: `Rebasing {b} onto {onto}: stopped at {short} "{subject}" ({k} of {n}). {N} files have
  conflicts.` / `… Conflicts resolved: continue to commit it and go on.` / with nothing to commit
  (`Empty`, or nothing staged and nothing conflicted): `… {short} "{subject}" changes nothing now:
  continue to leave it out.`; `{onto}` is a branch name that points at `onto` when one does
  (the rebase's target as the user chose it), else the short SHA.
- `git am` and conflicts without an operation: phase 3's texts, without the sentence about a later
  version.

The tooltip of the banner's icon (`BannerInfo`) says what the buttons do for the state, for
example for a rebase: `Continue commits the resolved files and replays the rest. Skip leaves this
commit out. Abort puts {b} back where it was before the rebase.` Phase 3's "come in a later version"
tooltips go.

The banner keeps D122's rule: at least 36 high, as high as its text needs at the width of the
graph area (or of the resolver, 11.2, which is wider). With three buttons the text has less room:
at 1100×700 the merge banner wraps to three lines (WP7 measures; the checks state the height).

### 5.3 The stage panel during a stop

- Merge, single pick or revert: unchanged from phase 3 ("Commit", "Commit merge"); committing
  concludes it, exactly as Continue does.
- A list or a rebase: the commit button reads `Continue cherry-pick`, `Continue revert` or
  `Continue rebase` and runs the banner's Continue with the panel's message; enabled as Continue
  is, with the same reason as its tooltip. The summary may be empty: git's own message is used
  then (5.4). This is how D136's "the Commit button does not finish a pick that belongs to a list;
  Continue does" is built: the button becomes Continue. Amend is disabled during a list or a
  rebase stop (`Finish or abort the {kind} first`).
- The message box is filled as phase 3 fills it from `MERGE_MSG` (its `#` lines removed), once per
  stopped commit; for a rebase from `rebase-merge/message`, as it is (it has no comment lines).
- Conflicted rows keep the phase 3 look; their tooltip becomes `Conflict: click to resolve it`.

### 5.4 What Continue runs (`GitRepository.ContinueAsync`)

`ContinueAsync(InProgressState state, string? message, NetworkOptions?, CancellationToken)`, the
message being the stage panel's (null when its summary is blank):

| State | Runs |
|---|---|
| Merge | with a message: `git commit --file=- --cleanup=whitespace` (phase 3's commit); without: `git merge --continue` |
| Single pick or revert | with a message: `git commit --file=- --cleanup=whitespace` (git takes the author from `CHERRY_PICK_HEAD`); without: `git cherry-pick --continue` (`revert`) |
| Pick or revert in a list, pick head present | `git commit --file=- --cleanup=whitespace` with the message (or `MERGE_MSG` less its `#` lines), then `git cherry-pick --continue` (`revert`): git then leaves the committed line out (D110, 0.3) |
| List stopped between commits, `AbortKeepsHead` | `git cherry-pick --continue` (`revert`) |
| Rebase, something staged, the stopped command a `pick`, `reword` or `edit` | `git commit --no-verify --cleanup=whitespace --file=-` with the message (or `rebase-merge/message`) and `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL`, `GIT_AUTHOR_DATE` from `rebase-merge/author-script`, then `git rebase --continue`: the message is kept exactly, `#` lines included (D68), with the commit's author and date (0.1) |
| Rebase otherwise (nothing staged, or a `squash`/`fixup` stop of a rebase started elsewhere) | `git rebase --continue` |

Each runs as one local operation "Continue the {kind}" (`RefreshScope.Refs`, `Ask`). It ends
`Done` (`Merge committed`, `Cherry-pick finished`, `Revert finished`, `Rebased {b} onto {onto}`),
`Conflicts` at the next stop (the banner), or failed with git's output ("Continue failed"). Before
it runs it refuses when the state changed since the click (a fresh status read: another kind, or
conflicts back).

`SkipAsync(InProgressState)`: the question (7.2), the `skip` backup, then `git cherry-pick --skip`,
`git revert --skip` or `git rebase --skip`; the same outcomes as Continue ("Skip the {kind}").

## 6. Pull with rebase, chosen for one pull

- **Menu**: the current branch's menu (`ForLocalBranch` on HEAD's branch, in the left panel and on
  its graph label), right after `Pull from {up}`: `Pull from {up} (rebase)`, enabled exactly as the
  toolbar's Pull (`AsToolbar`, D90). Not on the toolbar (D90: no dropdowns).
- **What runs**: phase 3's pull steps (fetch, `EndCancelWindow`, the HEAD checks) with the
  integration forced to rebase: when the branch has nothing of its own, `git merge --ff-only
  <head>` (as now); otherwise the backup (1.2) and `git rebase` **without arguments**, which rebases
  onto the upstream with its fork point, as `git pull --rebase` does (0.1: `git rebase FETCH_HEAD`
  would replay commits the upstream dropped by a force push). `pull.ff`, `pull.rebase` and
  `branch.<b>.rebase` are not read (git's command line `--rebase` ignores them too, also an invalid
  value). Title `Pull (rebase)`; dedupe key `pull-rebase:<full branch ref>`; a local upstream skips
  the fetch, as Pull does. Its success texts are phase 3's (`Pulled …`, `PulledRebased`).
- **The configured pull** (phase 3, D84) that rebases saves the same backup before its rebase.
- **Stops**: phase 3 took a pull's rebase as stopped only when unmerged paths appeared; a rebase can
  stop with none (0.1). Both pulls now tell a stop by the rebase folder (`RebaseResult`, 1.4), and
  the outcome `PullOutcome.Conflicts` stays for both kinds of stop.
- **Parity**: `PullRebaseParityTests` (a tenth class beside phase 3's nine) compares the chosen
  rebase pull with `git pull --rebase` on copies of the same template, for no settings,
  `pull.ff=only`, `pull.rebase=false`, `pull.rebase=merges` with a local merge, and an upstream
  rewritten by a force push, on Git 2.36 and CI's git.

## 7. UI inventory: menus, questions, dialogs, colours

### 7.1 The menus as phase 4 changes them

Notation as in phase 3's report: header first, "—" a separator, "…" an item that opens a dialog, a
list or asks first. Only the groups that change are shown; the rest is phase 3's.

| Menu | Phase 4's groups |
|---|---|
| A commit | [Check out this commit, Create branch here…, Create tag here…] — [Cherry-pick onto {cur}, Revert this commit, **Rebase {cur} onto this commit**, **Interactive rebase of {cur} from here…**, Reset {cur} to this commit…] — [Copy SHA] |
| Several commits | [**Squash {N} commits…**, Cherry-pick {N} commits onto {cur}, Revert {N} commits] — [Copy SHAs] |
| A local branch, not checked out | [Check out {b}, Merge {b} into {cur}, Merge {b} into {cur} (create a merge commit), **Rebase {cur} onto {b}**, **Interactive rebase of {cur} onto {b}…**, Fast-forward {b} to {up}] — phase 3's other groups |
| The current branch with an upstream | [Pull from {up}, **Pull from {up} (rebase)**] — phase 3's other groups |
| A remote branch | [Check out {r}/{b}, Merge {r}/{b} into {cur}, Merge {r}/{b} into {cur} (create a merge commit), **Rebase {cur} onto {r}/{b}**, **Interactive rebase of {cur} onto {r}/{b}…**] — phase 3's other groups |
| Drop: local A onto the current branch B | [Merge {A} into {B}, Merge {A} into {B} (create a merge commit), Fast-forward {B} to {A}] — [**Check out {A} and rebase it onto {B}**, **Check out {A} and rebase it onto {B} interactively…**] |
| Drop: the current branch A onto local B | [Fast-forward {B} to {A}, Check out {B} and merge {A} into it] — [**Rebase {A} onto {B}**, **Interactive rebase of {A} onto {B}…**] |
| Drop: local A onto local B, neither current | [Fast-forward {B} to {A}, Check out {B} and merge {A} into it] — [**Check out {A} and rebase it onto {B}**, **Check out {A} and rebase it onto {B} interactively…**] |
| Drop: local A onto a remote branch {r}/{x} | [Push {A} to {r}/{x}, Push {A} to {r}/{x} and track it] — [**Rebase {A} onto {r}/{x}**, **Interactive rebase of {A} onto {r}/{x}…**] (or the "Check out {A} and …" pair when A is not current) |
| Drop: a remote branch onto a local branch | phase 3's items only |
| A conflicted file (the unstaged list and its diff view) | [**Resolve conflict**, Mark as resolved…, Discard changes] — phase 3's other groups; **Open in merge tool** follows Resolve conflict while `merge.tool` is set (11.9) |

"Mark as resolved" now asks first when the file still has conflict markers (11.7), hence its "…";
it is otherwise phase 3's (it stages the file). Every new disabled reason is in 10.4.

### 7.2 Questions and dialogs

| Dialog | Title, text, buttons |
|---|---|
| Skip (`ConfirmationRequest`) | `Skip {short}?` / `"{subject}" is left out of the {rebase / cherry-pick / revert}, and changes made to resolve its conflicts are lost.` / detail `A backup is saved first, so this can be undone.` / `Skip` (danger), `Cancel` |
| Leaving the interactive list with changes | `Discard the changes to this list?` / no detail / `Discard`, `Keep editing` |
| Mark resolved with markers left (resolver and file menus) | `{file} still has {N} conflicts.` / `Mark it resolved anyway? The conflict markers stay in the file.` / `Mark resolved`, `Cancel` |
| Closing or leaving the resolver with an unsaved result | `Discard your changes to {file}?` / `The result has not been saved.` / `Discard`, `Keep editing` |
| The message dialog (`MessageDialogViewModel` / `MessageDialogView`, automation id `MessageDialog`) | title `Reword {short}`, `Message of the squashed commit` or `Squash {N} commits`; a summary box (`MessageSummary`, with the stage panel's counter of 72) and a description box (`MessageDescription`); `Save` or `Squash` as `ConfirmButton`, `Cancel`. `Save` is disabled while the summary is blank (`Write a summary`) |

Every question is the app's own dialog on the window's dialog layer (D69, D104); Esc cancels.

### 7.3 New colours and icons

| Token | Dark | Light | Used for |
|---|---|---|---|
| `VcConflictOursBrush` | `#1C2A45` | `#E3EAFB` | The ours side of a conflict block: its rows in the ours pane and its section of the result |
| `VcConflictTheirsBrush` | `#2C2140` | `#F0E6FA` | The theirs side, likewise |
| `VcConflictBaseBrush` | `#24272F` | `#EEF0F3` | A `diff3` base section in the result |
| `VcConflictMarkerBrush` | `#3A2E16` | `#FCEFD2` | The marker lines of an unresolved block in the result (their text in `VcWarningBrush`) |
| `VcConflictUsedBrush` | `#1A2E24` | `#E6F6EC` | A block's side once it is used in the result (the same colours as `VcDiffAddedBrush`) |

Icons (Lucide, added to `Icons.axaml` and `THIRD-PARTY-NOTICES.md`): `IconGrip` (grip-vertical,
the list's drag handle), `IconChevronLeft`, `IconChevronUp` (the resolver's navigation; Right
and Down exist).

## 8. The in-progress state and reftable (D128)

- `InProgressState` gains init properties with defaults, so that phase 3's constructions keep
  compiling: `Sequenced` (a `sequencer/` folder: replaces the two `HasSequencer()` copies in the
  app), `StopCommand` (the verb of `rebase-merge/done`'s last line), `Done` and `Total` (5.1's
  counting; `Remaining` stays, now counting commit commands only, so `label` and `exec` lines of
  the app's own todo do not count), and, for a rebase, `Commit` and `Subject` set from
  `rebase-merge/stopped-sha` and its subject. A list stopped between commits gets `Commit` and
  `Subject` from `sequencer/todo`'s first line (its abbreviated SHA resolved with
  `rev-parse --verify`). A rebase is `Empty` when `CHERRY_PICK_HEAD` sits next to `rebase-merge/`
  (0.1) or nothing is staged and nothing conflicts.
- A rebase's `onto` stays a SHA in the state; the banner finds a branch name for it among the refs.

### 8.1 Pick heads under both ref formats

`GitRepository.ReadPseudoRefAsync(name)` reads `CHERRY_PICK_HEAD`, `REVERT_HEAD` (and is the one
place that does): first the file `<git folder>/<name>`, as phase 3 does; when it is missing and the
repository uses the reftable format, `git rev-parse --verify --quiet --symbolic-full-name <name>`
must print exactly `<name>` (or nothing, with git's `ambiguous` error), then `git rev-parse
--verify --quiet <name>` gives the SHA (0.5: a branch named `CHERRY_PICK_HEAD` would otherwise
answer). Whether the repository uses reftable is read once per `GitRepository`, lazily, with
`git config --get extensions.refStorage`, and only when `MERGE_MSG` or `sequencer/` exists (every
stopped pick or revert writes one of them, 0.3): a files repository with nothing in progress makes
no extra git call. `MERGE_HEAD` and `FETCH_HEAD` stay files under both formats. The in-progress
read, `ApplyCommitAsync` and the list's classification (4) use it.

Tests: the probe's rules with a recorded runner (no git); on a git of 2.45 or later (CI's) a real
reftable repository with a stopped pick shows the banner's state; on 2.36 that test asserts that
git refuses `--ref-format` (the "two gits" rule: a version decides what is asserted, nothing is
skipped).

## 9. The backups' clean-up (D137, owner's answer 4)

- **A backup** is one operation's refs: every ref under `refs/visualcommit/backup/` whose name
  starts with the same `<kind>-<time>[-<n>]` (D83's scheme: the ref itself, its `-changes` ref, its
  `/<name>` refs), and D67's `discard-<time>[-<n>]`. Its time is the one in its name (UTC,
  `yyyyMMdd'T'HHmmssfff'Z'`). A ref under the prefix that fits no scheme is left alone.
- **The rule** (`BackupRetention(Days: 30, Count: 50)`, Core): with `now` from the repository's
  backup clock, every backup older than 30 days goes; then, while more than 50 remain, the oldest
  goes. Nothing goes while an operation is in progress in any worktree of the repository (the main
  git folder and every `<common git folder>/worktrees/*/`: `rebase-merge/`, `rebase-apply/`,
  `MERGE_HEAD`, `sequencer/`, a pick head): the whole clean-up waits for another time, so the
  backup of an operation still in progress is never deleted. The restore bar's snapshot (D62),
  passed in as `keep`, never goes.
- **How**: one `git for-each-ref --format=%(refname) %(objectname) refs/visualcommit/backup/`, then
  one `git update-ref --stdin` transaction of `delete <ref> <old value>` lines: a ref changed since
  the read fails the whole transaction, and nothing is deleted (logged).
- **When**: (a) at the end of every operation that saved a backup, inside it, after its work: a
  failure there is logged at Warning and never fails the operation; (b) when a repository has
  opened and its first load is done, as a background operation of its queue (`Clean up backups`,
  `IsBackground`, prompts `None`, no refresh), started with `TryStartInBackground`: if the queue is
  busy at that moment it is not run; the next backup or opening does it.
- **What it says**: nothing on screen. The log gets one Info line per backup removed,
  `<repo>: Removed backup <kind>-<time> (older than 30 days)` or `(more than 50 backups)`, and a
  Debug line when an operation in progress made it wait.
- `GitRepository.CleanUpBackupsAsync(BackupRetention, IReadOnlySet<string> keep, CancellationToken)`
  returns what it removed. Phase 5's undo offers only backups that exist (D137).

## 10. Texts

`OperationTexts` gets a part `OperationTexts.Rebase.cs` (and `.Conflicts.cs`); counts use the
singular for one (D113). `{target}` is a branch's short name, `{r}/{b}`, or a commit's short SHA;
`{kind}` is phase 3's `InProgressName` (merge, cherry-pick, revert, rebase).

### 10.1 Operations: title, running text, error dialog title, failure summary

| Operation | Title | Running text | Error title | Failure summary |
|---|---|---|---|---|
| Rebase onto | `Rebase {b} onto {target}` | `Rebasing {b} onto {target}…` | `Rebase failed` | `Rebase {b} onto {target} failed.` |
| Check out and rebase | `Check out {A} and rebase it onto {B}` | `Checking out {A}…`, then `Rebasing {A} onto {B}…` | `Checkout failed` or `Rebase failed` (the step that failed) | phase 3's checkout summary, or the rebase's |
| Interactive rebase | `Interactive rebase of {b}` | `Rebasing {b}…` | `Interactive rebase failed` | `Interactive rebase of {b} failed.` |
| Squash | `Squash {N} commits` | `Squashing {N} commits…` | `Squash failed` | `Squash {N} commits failed.` |
| Pull with rebase | `Pull (rebase)` | phase 3's pull texts | `Pull failed` | phase 3's |
| Continue | `Continue the {kind}` | `Continuing the {kind}…` | `Continue failed` | `Git could not continue the {kind}.` |
| Skip | `Skip {short}` | `Skipping {short}…` | `Skip failed` | `Git could not skip {short}.` |
| Mark resolved, take a side | `Resolve {file}` | `Resolving {file}…` | `Resolving failed` | `{file} could not be marked resolved.` |
| Merge tool | `Merge tool for {file}` | `Waiting for {tool} to close…` | `Merge tool failed` | `{tool} did not resolve {file}.` |
| Backups' clean-up | `Clean up backups` | (background: never shown) | (never shown) | (logged only) |

Special summaries (the error dialog's summary in place of the default, git's output below it, as
phase 3's): a rebase refused for local changes → `Git rebases only a clean working tree: commit or
stash your changes first.`; for an untracked file in the way → `Untracked files are in the way:
move or remove them first.`; a list refused at its start → `Git would overwrite your changes:
commit or stash them first. Nothing was cherry-picked.` (`… Nothing was reverted.`).

### 10.2 Results in the status bar

| Outcome | Text (tone) |
|---|---|
| Rebased | `Rebased {b} onto {target}` |
| Up to date | `{b} is already up to date with {target}` |
| Interactive rebase done | `Rebased {b}: {N} commits` (the commits on the branch after its base) |
| Squash done | `Squashed {N} commits into {short}` |
| A rebase stopped | `Rebase stopped: {N} files have conflicts` (warning); with none `Rebase stopped at {short} "{subject}"` (warning) |
| A list stopped | `Cherry-picked {K} of {N} commits; stopped at {short} with conflicts in {M} files` (warning); `Reverted …` likewise; a list stopped on an empty pick: `Cherry-picked {K} of {N} commits; {short} changes nothing` (warning) |
| Continue done | `Merge committed`, `Cherry-pick finished`, `Revert finished`, `Rebased {b} onto {onto}`; at a new stop as the stops above |
| Skip done | `Skipped {short}`; at the end of the operation as Continue's |
| Resolved | `Resolved {file}` |
| Merge tool | `Resolved {file} in {tool}`; `{tool} closed without resolving {file}` (secondary) |

### 10.3 The banner

5.2 is binding for the banner's texts. Its buttons: `Continue`, `Skip`, phase 3's `Abort …`. The
icon's tooltip per kind:

| Kind | Tooltip |
|---|---|
| Merge | `Continue commits the merge with the message in the stage panel. Abort puts {cur} back where it was before the merge.` |
| Cherry-pick, revert | `Continue commits the resolved files and goes on with the commits that wait. Skip leaves this commit out. Abort puts {cur} back where it was before the {kind}.` |
| Rebase | `Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts {b} back where it was before the rebase.` |
| Conflicts without an operation | `Click a conflicted file to resolve it, then mark it resolved.` |

### 10.4 New disabled reasons

| Reason | Where |
|---|---|
| `Check out a branch to rebase` | rebase and interactive items while detached |
| `Check out a branch to squash` | squash while detached |
| `{cur} has no commits after this one` | "from here" on HEAD's own commit |
| `This commit is not on {cur}` | "from here" on a commit HEAD does not reach |
| `A merge lies between this commit and {cur}` | "from here" off HEAD's linear line |
| `A merge lies between {b} and {cur}: interactive rebase would flatten it` | the interactive list of an onto target with merges in its range (shown in the list, 2.2) |
| `Merge commits cannot be squashed` | squash |
| `Only commits of {cur} can be squashed` | squash |
| `Select commits that follow one another` | squash |
| `A merge lies between these commits and {cur}` | squash |
| `The first commit cannot be squashed` | squash |
| `Resolve the conflicts first ({N} files)` | Continue, the stage panel's Continue |
| `This cherry-pick changes nothing: skip it` / `This revert changes nothing: skip it` | Continue |
| `There is nothing to skip: the stopped commit was committed` | Skip |
| `Git would leave {short} out without a word: skip it or abort` | Continue |
| `The oldest commit has nothing below it to join` | squash and fixup in the list's bottom row |
| `Change an action or the order first` | Start rebase |
| `Write a summary` | the message dialog's Save |
| `Set merge.tool in git's configuration to use a merge tool` | the resolver's Merge tool button (11.9) |
| `This conflict has no text to merge` | the resolver's per-block buttons for a non-text conflict (11.8) |

## 11. The conflict resolver

### 11.1 Opening and closing

- A click (or double-click) on a conflicted file in the stage panel's unstaged list opens the
  resolver for that file, where phase 2 opened the "This file has a merge conflict" body (D63's
  gesture); so does the file menu's `Resolve conflict`. The diff view's `Conflict` body goes.
- `ConflictResolverViewModel` belongs to the `RepositoryViewModel` (`Resolver`,
  `IsResolverOpen`), like the diff; opening the resolver closes an open diff or interactive list,
  and the other way round. Its document, undo stack, caret and scroll live in the view model, so a
  tab switch keeps them (one view serves every tab, as for the diff).
- It closes with its close button (`ResolverCloseButton`), Esc (11.10), when the conflicted files
  are all resolved (11.7), when the operation ends (Abort, or anything that leaves no conflict), or
  when the tab closes. A close with an unsaved result asks first (7.2); a tab closing does not ask
  (the work is in memory only, as an unsent stage-panel message is).

### 11.2 Layout (owner's answer 5)

While the resolver is open, `CommitGraphView` spans the graph and right-panel columns
(`Grid.ColumnSpan` 2), the right panel and `RightSplitter` are hidden, and the view shows the
resolver where the graph or the diff was. The left panel stays. The banner stays on top, across
the resolver's width. At 1100×700 the resolver is 840 wide (1100 − 260) and 586 high less the
banner.

```
┌ banner: Merging "Merge branch 'feature'". 1 file has conflicts.   [Continue][Abort merge] ┐
│ U  src/app.txt   Both modified        File 1 of 2  [‹][›]                          [✕] │ header, 36
│ Conflict 1 of 2  [˄][˅]   [Take ours] [Take theirs] [Merge tool…]   [Mark resolved]     │ toolbar, 36
├───────────────────────────────────────┬────────────────────────────────────────────────┤
│ Ours: main                            │ Theirs: feature                                 │ titles, 26
│  1 a                                  │  1 a                                            │
│ ┌ [Use ours] [Use both] ────────────┐ │ ┌ [Use theirs] [Use both] ─────────────────┐   │ block bar, 26
│  2 b-ours                             │  2 b-theirs                                     │
│  3 c                                  │  3 c                                            │
├───────────────────────────────────────┴────────────────────────────────────────────────┤
│ Result                                      1 conflict left                             │ title, 26
│  1 a                                                                                    │
│  2 <<<<<<< HEAD                                                                         │
│  3 b-ours                                                                               │
│  4 =======                                                                              │
│  5 b-theirs                                                                             │
│  6 >>>>>>> feature                                                                      │
│  7 c                                                                                    │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Header** (36): the status letter `U` in `VcDangerBrush`, the file name, its folder (as the diff
  header), the kind in git's words (`Both modified`, `Both added`, `Deleted by them`, `Deleted by
  us`, `Added by us`, `Added by them`, `Both deleted`), `File {i} of {n}` with previous and next
  (`ResolverPreviousFile`, `ResolverNextFile`: the conflicted files in the stage list's order,
  wrapping), and the close button.
- **Toolbar** (36): `Conflict {k} of {m}` with previous and next (`ResolverPreviousConflict`,
  `ResolverNextConflict`; with none left: `No conflicts left`), `Take ours` and `Take theirs` (the
  whole file, 11.6), `Merge tool…` (11.9), and on the right `Mark resolved` (`VcPrimaryButton`,
  11.7).
- **Top half**: the ours pane (`ConflictOurs`) and the theirs pane (`ConflictTheirs`) side by side,
  each half the width, a 1 px `VcBorderBrush` between; each with a title strip (26) naming its side
  (11.4). **Bottom half**: the result (`ConflictResult`), with its title strip `Result` and on the
  right `{m} conflicts left` (`1 conflict left`, `No conflicts left`). The halves split the height
  equally; no splitter in phase 4.
- Text is the code font at 12, rows 15.84 high, as the diff (D64). At 1100×700 each top pane shows
  about 13 rows and 51 characters; the scenario files of the checks keep their lines short.

### 11.3 What the panes hold

The working-tree file is the source (it is what git wrote, in the user's conflict style and marker
size, and it holds a rerere resolution or the user's own edits). It is read as bytes, decoded as
the diff view decodes a file (UTF-8, or Latin-1 when it is not valid UTF-8, D66), with its line
ends kept.

`ConflictText.Parse(text, markerSize)` (Core, pure, tested) splits it into common segments and
blocks. A block runs from a line that starts with `markerSize` `<` and a space (or ends there) to
the next such `>` line, with an optional `|` section (diff3, zdiff3) and one `=` line; the text
after the markers is the block's label. The marker size is git's attribute
`conflict-marker-size` for the path (`git check-attr conflict-marker-size -- <p>`, default 7).
Lines that do not form a whole block are common text.

- **The ours pane** shows the file as it would be with every block's ours side: the common
  segments, and for each block its ours lines. **The theirs pane** likewise with the theirs sides.
  Their rows line up: a block's shorter side gets filler rows (as the side-by-side diff, D61), and
  before each block both panes have a block bar (26 high) with its buttons. The block's rows are
  tinted `VcConflictOursBrush` and `VcConflictTheirsBrush`; once the block is used in the result
  the side used is tinted `VcConflictUsedBrush` and its bar says `Used in the result` instead of
  the buttons. The two panes scroll together; syntax highlighting as the diff's (D64), each pane a
  side of its own.
- **The result** is the file itself, editable. An unresolved block shows git's markers as text:
  its marker lines on `VcConflictMarkerBrush` in `VcWarningBrush`, its ours section on
  `VcConflictOursBrush`, its theirs section on `VcConflictTheirsBrush`, a base section on
  `VcConflictBaseBrush`. The result has no syntax highlighting in phase 4 (D148).
- Built on phase 2's diff text: the top panes are a new `ConflictPanes` control made of two
  `DiffPane`s fed with rows of its own (`DiffPane.Show(side, rows)` takes any rows), the block bars
  from `HunkHeaderLayer` with a button list per side, settable automation ids; the result is a
  `ConflictResultEditor` (an editable `DiffTextEditor`-like AvaloniaEdit `TextEditor`: caret and
  selection drawn, undo on, IME on, the search panel removed, its own background renderer for the
  tints, the document owned by the view model). WP0's spike (14) proves the editable editor in the
  headless harness before the checks are written.

### 11.4 The sides' names

| In progress | Ours title | Theirs title |
|---|---|---|
| Merge | `Ours: {cur}` | `Theirs: {merged}`: the branch name from `MERGE_MSG`'s `Merge branch '<x>'` (or `remote-tracking branch '<x>'`), else the theirs marker's label, else `MERGE_HEAD`'s short SHA |
| Cherry-pick | `Ours: {cur}` | `Theirs: {short} {subject}` (the picked commit) |
| Revert | `Ours: {cur}` | `Theirs: the revert of {short} {subject}` |
| Rebase | `Ours: {onto} (the new base)` | `Theirs: {short} {subject} (your commit)` |
| Conflicts without an operation (a stash) | `Ours: {cur}` | `Theirs: the stash` |

In a rebase the sides are the other way round from what users expect (0.1): the titles say which
is the user's own commit. The buttons say "ours" and "theirs" as git does; their tooltips name the
side (`Use the lines of {ours title}`).

### 11.5 Choosing per block

- In the ours pane's bar: `Use ours` (the block becomes its ours lines) and `Use both` (ours, then
  theirs). In the theirs pane's bar: `Use theirs` and `Use both` (theirs, then ours).
- A choice replaces the block's text in the result (from its `<` line to its `>` line) with the
  chosen lines, as one undo step (Ctrl+Z undoes it, Ctrl+Y does it again). The block is then used:
  its bars change (11.3), and the count of conflicts left goes down.
- `Conflict {k} of {m}` and its arrows move the caret to the result's k-th remaining block and
  scroll the top panes to the same block (blocks are matched by their order among those the
  result still has). The first block is shown when the resolver opens.
- **Editing**: any text can be typed in the result. A block whose markers are edited away is no
  longer counted; the top panes keep its bars until a choice is made or the file is saved (the
  count in the result's title is the truth). The result is parsed again after an edit (debounced,
  as the diff's refresh).

### 11.6 A whole file from one side

`Take ours` and `Take theirs` (text conflicts) replace the whole result with that side's file,
read with `git cat-file --filters --path=<p> <stage oid>` (the working-tree form, 0.4), as one undo
step; nothing is written until Mark resolved. A side the conflict lacks (a deleted side) disables
the button with `This side deleted the file`.

### 11.7 Mark resolved

`Mark resolved` writes the result to the working-tree file (in the encoding it was read in, with
the line ends the document has; typed lines take the file's existing line end) and runs
`git add -- <p>`, in one queued operation `Resolve {file}` (11.1's texts). When the result still
has blocks, it asks first (7.2). Then the resolver moves to the next conflicted file in the list,
or closes when none is left (the graph and the stage panel come back; the banner offers Continue).
The file menu's `Mark as resolved…` asks the same question when the file has markers, and stages
it as phase 3 did.

### 11.8 Conflicts without text

A conflict whose kind is not "both modified" or "both added", or whose content is binary (a NUL in
the first 8000 bytes of a side, as git decides), a symbolic link (mode `120000`) or a submodule
(`S...`), shows no panes. The resolver's body is a card in its middle with what happened and two
buttons that resolve the file at once (one operation `Resolve {file}`, then 11.7's moving on):

| Kind | Card text | Buttons and what they run |
|---|---|---|
| Deleted by them (`UD`) | `{ours title} changed {file}; {theirs title} deleted it.` | `Keep the file` (`git checkout --ours -- <p>`, `git add -- <p>`), `Delete it` (`git rm -- <p>`) |
| Deleted by us (`DU`) | `{ours title} deleted {file}; {theirs title} changed it.` | `Keep the file` (`--theirs`, `add`), `Delete it` (`rm`) |
| Added by us (`AU`) | `{file} exists only in {ours title}.` | `Keep it` (`add`), `Delete it` (`rm`) |
| Added by them (`UA`) | `{file} exists only in {theirs title}.` | `Keep it` (`--theirs`, `add`), `Delete it` (`rm`) |
| Both deleted (`DD`) | `Both sides deleted {file}.` | `Delete it` (`rm`) |
| Binary or symbolic link (`UU`) | `{file} is a binary file (a symbolic link): its versions cannot be merged line by line.` | `Use ours` (`--ours`, `add`), `Use theirs` (`--theirs`, `add`); `Merge tool…` for a binary file |
| Submodule | `{file} is a submodule: resolve it in a terminal.` | none |

`git restore --ours` is never used (0.4). The toolbar's per-block buttons do not apply; its `Take
ours` and `Take theirs` are the card's own buttons.

### 11.9 The external merge tool

- **Enabled** when git's configuration names a tool (`merge.tool`, read with
  `git config --get merge.tool` whenever the status shows a conflict, kept with the repository's
  state); otherwise disabled with `Set merge.tool in git's configuration to use a merge tool` (a
  settings screen is phase 6's). Offered for text conflicts and binary files only (git never runs
  the tool for the others, 0.4).
- **Runs** `git mergetool --tool=<tool> --no-prompt -c mergetool.keepBackup=false -- <p>` as one
  operation `Merge tool for {file}`, which can be cancelled (a new `GitOperation.CanBeCancelled` for
  a local operation that only waits for another program; the status bar's Cancel stops git and its
  shell; the tool's own window may stay open). Before it, an unsaved result is offered to be saved
  or discarded (7.2's question, with `Save first` added).
- **After** it: the files git leaves in the working tree on its failure paths
  (`<name>_BACKUP_<n>.<ext>`, `_BASE_`, `_LOCAL_`, `_REMOTE_` next to the file) are deleted when
  they did not exist before the run; the status is read again. When git staged the file the
  resolver moves on (11.7); otherwise the result is read again from the file (the tool may have
  written it) and the status bar says the tool closed without resolving.
- The tests use a fake tool: a repository-local `mergetool.fake.cmd` script that writes known
  content to `$MERGED` and exits 0, with `mergetool.fake.trustExitCode=true` (0.4: then git trusts
  the exit code instead of the file's time).

### 11.10 Keyboard and focus

- Tab moves between the toolbar's buttons, the panes' bars and the result; inside the result Tab
  types a tab (an editor's convention), and Ctrl+Tab leaves it.
- Esc inside the result does nothing (the editor keeps it, so a slip never loses work: the
  window's Esc chain gets an entry for the resolver before the diff's, which declines while the
  result has the focus); elsewhere it closes the resolver (asking first when the result is
  unsaved).
- Alt+Up and Alt+Down go to the previous and next conflict, Alt+Left and Alt+Right to the previous
  and next file.
- The result's caret does not blink in the test harnesses (`ConflictResultEditor.CaretBlinks`, set
  false by both harnesses), so pictures do not vary.

### 11.11 Refreshes while it is open

A refresh that leaves the file conflicted keeps the result as it is when it has unsaved changes,
and otherwise reads the file again (a terminal or a tool may have changed it). A refresh after
which the file is no longer conflicted moves on (11.7). A refresh that ends the operation closes
the resolver.

## 12. What changes for earlier phases

### 12.1 Their checks and tests, changed on purpose

Recorded under "Changes to expected results" in phase 4's report, each with its reason, and the
tests changed in the same commit as the code (plan's "Visual test gate", item 6):

- **Phase 1, checks 13 and 14** (D150): the clone is of a bare copy of the 100k-commit repository
  served over HTTP by `AuthRemoteServer` on a free port, held at a fixed percentage instead of
  raced; check 13's picture `13b` shows that percentage and the server's URL. Check 13 failed once
  on CI's `ubuntu-latest` (run 38213858149) because a fast runner finished the clone between two
  pictures.
- **Phase 3's menus** that gain items (checks 1, 2, 3, 7, 11, 12 and the menus of the real-window
  pass): their item lists and heights; a menu near the window's bottom may open upward now.
- **Phase 3's banner** (checks 9, 25, 27): the Continue and Skip buttons, the texts without "in a
  terminal" for rebases and lists, the new tooltips, the heights that follow.
- **Phase 3's check 11b** (a pick of two commits that stops): the app now makes one git call with
  the list, so a `sequencer/` folder is there at the stop; the Abort question says the commits
  already made are dropped too; after Abort `main` is where it was before the first pick.
- **Phase 3's check 25 step 4** (a terminal's pick sequence): the stage panel's button reads
  `Continue cherry-pick` and continues the sequence instead of committing the stopped pick alone.
- **Phase 3's tests** that pin one git call per commit (`HistoryOperationTests`,
  `RepositoryOperationsHistoryTests`: several picks and reverts, the empty pick in a list) and the
  banner's texts (`ConflictBannerTests`, `WorkingChangesInProgressTests`).
- **Phase 2's conflicted-file diff body** ("This file has a merge conflict"): replaced by the
  resolver; no phase 2 check shows it (phase 2's handoff recorded it as expected until the
  resolver).

### 12.2 `plan.md` and the risks

At the end of the phase (closing step 3): risk 2 (the resolver) is settled or rewritten with what
was built; risk 17 (reftable) settled; risk 24 (backups never deleted) settled by section 9; risk
12 and 18 (run times) updated with phase 4's measures; phase 5's "From phase 3" item on backups
reads "phase 4 cleans them up (D139): undo offers what is left".

## 13. Tests and harness

### 13.1 Scenario repositories (`tests/VisualCommit.Testing/Scenarios.Phase4.cs`)

Built by WP-S as templates (copied for each test) and pinned in
`tests/VisualCommit.Git.Tests/Phase4ScenarioTests.cs`; stopped states are built per call, as
phase 3's `StoppedAsync`. Times by `TempRepo`'s clock (2026-01-01 12:00 plus a minute per commit).
Every file holds short lines (the resolver's panes show about 51 characters at 1100×700).

1. **"rebase"** (`RebaseAsync`): `Initial commit` (README.md), `Add core` (src/core.txt); from it
   `feature/topic`: `Topic one` (topic/one.txt), `Topic two` (topic/two.txt); then on `main`:
   `Main work` (src/core.txt changed), `Main more` (docs/main.txt); from `Add core`
   `feature/other`: `Other one` (other/one.txt). A bare `origin` with `main` and `feature/topic`
   pushed and tracked. HEAD on `feature/topic`. Rebasing the topic onto `main` has no conflict.
2. **"interactive"** (`InteractiveAsync`): one line of history on `main`: `Initial commit`, `Base`
   (src/base.txt), `One` (src/one.txt), `Two` (src/two.txt), `Fix one` (src/one.txt changed: a
   fixup candidate), `Three` (src/three.txt, with a body line `#42 is the issue`), `Four`
   (src/four.txt). HEAD on `main`. Every reorder of these commits applies without a conflict, except
   moving `Fix one` below `One` (it would not apply before `One`): checks keep it above `One`.
3. **"conflicts"** (`ConflictsAsync`): `Initial commit`; `Base` with notes.txt (twelve short lines,
   `line 1` to `line 12`) and other.txt; on `main`: `Ours edits` (notes.txt lines 2 and 10 changed,
   other.txt changed); from `Base`: `feature/theirs`: `Theirs edits` (notes.txt lines 2 and 10
   changed otherwise, line 6 changed: merged cleanly); `feature/rebase`: `First change` (notes.txt
   line 2), `Second change` (notes.txt line 10): each conflicts with `main` on its own;
   `feature/picks`: `Pick clean` (pick.txt), `Pick conflicting` (notes.txt line 2), `Pick last`
   (last.txt); `feature/kinds`: other.txt deleted, image.png changed on both sides (a binary file,
   `TestImages`), new.txt added on both sides with different content. HEAD on `main`.
   Stopped copies (`ConflictsStoppedAsync(kind)`): a merge of `feature/theirs` (notes.txt `UU` with
   two blocks four or more letter lines apart, so git keeps them apart); a rebase of
   `feature/rebase` onto `main` (stopped at `First change`); a pick of the three `feature/picks`
   commits (stopped at the second); a merge of `feature/kinds` (`UD`, binary `UU`, `AA`); each
   built as a terminal would, with git, and optionally with the fake merge tool configured.
4. Pull with rebase uses phase 3's "sync" scenario (`RemoteSyncAsync` with
   `PushFromElsewhereAsync`): a branch and its upstream that have diverged, through a file remote
   (no server, so no port).
5. Phase 1's check 13: `LargeHistory.GetBareAsync()`, a bare copy of the 100k-commit repository
   made once per machine beside the working copy (its own marker file), served in place.

### 13.2 Harness additions

- `TempRepo`'s environment sets `GIT_EDITOR=:` and `GIT_SEQUENCE_EDITOR=:`; `GitIsolation` and
  `RealApp.Launch` remove the machine's `EDITOR`, `VISUAL`, `GIT_EDITOR` and `GIT_SEQUENCE_EDITOR`
  (0.5; the handoff's isolation rule). `TempRepo.GitWithEnvironmentAsync(extra, args)` and its
  "allowing failure" twin keep the clock's dates for a call with its own variables.
- `AuthRemoteServer.HoldNextFetchPack(int atPercent = 0)`: 0 keeps phase 3's rule; a percentage
  holds right after the first whole progress line at or above it, re-framing the packet so that
  git's last line shows exactly that percentage (D150). Tests in `AuthRemoteServerTests`.
- `ShellDriver.Phase4.cs`: the interactive list as a snapshot (rows with action, SHA, subject,
  second line, indentation), a row's point, a drag of a row in steps, setting a row's action; the
  resolver as a snapshot (header texts, counts, titles, the panes' rows with their tints, the
  bars' buttons), a block bar's button, the result's text, caret and modified state, typing into
  the result, a click at a line and column; the banner's Continue and Skip.
- `RealApp.Phase4.cs`: the same through UI Automation (automation ids in 11.2 and 2.2); the result
  editor's automation peer offers a read-only Value pattern with its text, so the pass can read it
  (a masked box it is not).
- `Phase4Checks.Support.cs` copies what phase 3's support file offers (phase 3's stays as it is),
  and has once what phase 3 repeated in each part (a graph assertion, `BackupsAsync`,
  `AssertCalled`, `WaitForOperationAsync`); `AssertBanner` knows Continue and Skip.

### 13.3 The walk-through project (D151)

`tests/VisualCommit.VisualTests.Phase4`, a project of its own as phase 3's (D133): its own
headless UI thread, in `VisualCommit.slnx`, picked up by CI with no change to the workflow;
`Phase4Expectations*.cs` linked into the real-window project (its tests file is named so that the
link's pattern does not take it: `Phase4SupportTests.cs`, `Phase4ExpectationTests.cs` excluded by
name). `scripts/time-scripted-check.sh` takes phase 4 (and makes it the default); CLAUDE.md's
Commands name the project.

### 13.4 Proposed visual checks (the report's writers write them in full, step 7)

"RW" marks what the real-window pass repeats (a picture of every new screen, and what only a real
window shows: a real drag, real typing into the editor, a real process for the merge tool). Window
1100×700, dark, unless said.

| # | Check | RW |
|---|---|---|
| A1 | The new menu items: a commit not on HEAD, a commit on HEAD's line, HEAD's own commit (each item's state and reason, one disabled reason's tooltip), a local and a remote branch, the current branch with `Pull … (rebase)`, several commits with `Squash` enabled and, for a selection that is no chain, disabled | the commit menu |
| A2 | A plain rebase from the branch menu (`Rebase feature/topic onto main`): graphs before and after, the status, the backup ref and its target | yes |
| A3 | Rebase by drag: the drop menu with its rebase items (the current branch onto `main`; `feature/other`, not current, onto `main`), then "Check out feature/other and rebase it onto main": graph, HEAD | the drop menu |
| A4 | Pull with rebase against a file remote: before and after, backup, status | n/a |
| A5 | A rebase git refuses (a changed tracked file): the error dialog's title, summary and git's lines; nothing changed | n/a |
| B6 | The interactive list from the commit menu on `Base`: header, rows newest on top, the base row, the right panel on a selected row, the action box open, the bottom row's disabled squash with its tooltip | yes |
| B7 | Editing the list: drag `Four` below `Two`, `Fix one` set to fixup, `Three` reworded (the dialog), `Two` dropped, `Start rebase`: the history and messages after it (the `#42` line kept), the backup | yes (the drag and Start) |
| B8 | Squash two selected commits: the menu, the dialog filled with both messages, the result | n/a |
| B9 | Esc on a changed list: the question; Discard; nothing changed | n/a |
| C10 | A merge conflict end to end: the banner (Continue disabled, its reason), the resolver's layout, titles and both panes, `Use ours` on block 1, `Use theirs` on block 2, Ctrl+Z, `Use both`, the result, Mark resolved, the resolver closes, Continue: the merge commit in the graph | yes |
| C11 | A rebase with two conflicting commits: the first stop's banner (`1 of 2`, Continue and Skip), the titles (the new base, your commit), `Take theirs`, Mark resolved, Continue to the second stop, Skip with its question: the graph and the backups (`rebase`, `skip`) | yes (banner and Skip) |
| C12 | A cherry-pick of three commits that stops at the second: banner (`1 more commit waits`), the stage panel's `Continue cherry-pick`, a resolution typed into the result, a message edited in the stage panel, Continue: the history and the edited message | n/a |
| C13 | Abort returns to the start: a rebase stopped at its second commit, and a pick list stopped: refs and working tree as before each | n/a |
| C14 | Editing freely: typing, Ctrl+Z and Ctrl+Y, Esc inside the result does nothing, Mark resolved with a block left (the question), closing with an unsaved result (the question) | yes (real typing) |
| C15 | Conflicts without text: the merge of `feature/kinds`: the cards for "deleted by them" and the binary file, `Keep the file`, `Use theirs`; the add/add file in the panes | n/a |
| C16 | The external merge tool (the fake tool): `Merge tool…`, the file resolved, the resolver moves on, the status | yes (a real process) |
| C17 | A rebase started in a terminal: the banner offers Continue and Skip (no "terminal" text); Continue from the app finishes it | n/a |
| D18 | The light theme at 1920×1080: the interactive list and the resolver (each tint's colour sampled) | n/a (scripted only: the size does not fit the development screen) |

### 13.5 Each "Delivers" item and what proves it

| Delivers | Proven by |
|---|---|
| Rebase onto, from menus and by drag; pull with rebase | A1 to A5; `RebaseOperationTests` (Git), `RepositoryOperationsRebaseTests`, `RepositoryMenusTests`, `PullRebaseParityTests` |
| Interactive rebase: reorder, squash, fixup, reword, drop; squash a multi-selection | B6 to B9; `RebaseTodoTests` (Core, pure), `InteractiveRebaseTests` (Git), `InteractiveRebaseListParityTests`, `InteractiveRebaseViewModelTests` |
| The banner's Continue, Skip, Abort for merge, rebase, cherry-pick and revert | C10 to C13, C17; `ContinueSkipTests` (Git, every state of 5.1), `ConflictBannerTests`, `WorkingChangesInProgressTests` |
| The resolver: three panes, per block, editing, a whole file, mark resolved, the merge tool | C10, C11, C14 to C16, D18; `ConflictTextTests` (Core), `ConflictResolverViewModelTests`, `ConflictResultEditorTests`, `ConflictOperationTests` (Git: every kind of 0.4) |
| Several picks and reverts with git's list (D136) | C12, C13; `HistoryOperationTests` (lists), `RepositoryOperationsHistoryTests` |
| Reftable (D128) | `PseudoRefTests` (the probe's rules; a real reftable repository on git 2.45+) |
| The sequence editor of the app's own | B7; `InteractiveRebaseTests` (todo delivery, paths with spaces and apostrophes) |
| Backups: the rebase's backup, the automatic clean-up | A2, C11; `BackupCleanupTests` (Git, with a fixed clock), `RepositoryOperationsBackupCleanupTests` (App: at opening, after a backup) |
| Phase 1's check 13 made deterministic | phase 1's checks 13 and 14; `AuthRemoteServerTests` (the hold at a percentage) |

### 13.6 Run time

The default run took 19 to 26 minutes on the laptop with phase 3 (23 min 21 s for phase 4's
baseline, with other work beside it). Phase 4 adds a fifth test process of about 17 checks
(estimated 8 to 12 minutes alone) and Git-layer tests that each start several git processes; the
run is bound by the processors, so expect it to grow by a few minutes. On CI the Windows job's test
step took about 9 minutes of its 14-minute limit at phase 3's gate: WP-S and every later package
read each CI run, and when the Windows test step passes 12 minutes the lead raises the step's limit
(D134: the owner said to let CI run as long as the tests need) and records it. The real-window pass
gains about six launches (estimated 4 to 6 minutes on top of phase 3's 11).

## 14. Work breakdown

Each package is built in its own worktree on a branch made **from `phase/4-rebase-conflicts`**
(`wip/p4-<name>`; a worktree starts from `master`, so its agent creates the branch from the phase
branch first), merged back with `--no-ff` after its tests pass and the solution builds, its
worktree removed at once and its fsmonitor daemon stopped (phase 3 handoff, "Things that cost
time"). A package edits only the files named for it; anything else goes through the lead.

| Package | Builds | Owns |
|---|---|---|
| WP0 (lead) | The contracts: every new `IGitRepository` member declared with its git command in its doc comment and stubbed in `GitRepository.Stubs.cs` and `FakeRepositoryBase`; `InProgressState`'s additions; Core's `RebasePlan`, `RebaseStep`, `RebaseAction`, `RebaseResult`, `SequenceResult`, `ConflictEntry`, `ConflictKind`, `BackupRetention`; the `IRepositoryActions` and `IInProgressActions` members (bodies that fail until built); the menu context's new facts; `GitOperation.CanBeCancelled`; `WorkingTreeStatus`'s conflict entries parsed from the `u` lines; the `TempRepo` and `GitIsolation` editor fix; the correction of the real-window project's comment (handoff step 3). The editable-editor spike (11.3) | `Core/Git/*` new types, `IGitRepository.cs`, `FakeRepositoryBase.cs`, `GitRepository.Stubs.cs`, `GitRepository.Changes.cs` (status parse), the interfaces |
| WP-S | Scenarios 13.1, their pins, the Phase 4 project and its support, `ShellDriver.Phase4.cs`, `RealApp.Phase4.cs` (written, run at the gate), `LargeHistory.GetBareAsync`, `HoldNextFetchPack(atPercent)`, phase 1's checks 13 and 14, the script and CLAUDE.md's commands | `tests/VisualCommit.Testing/*` (new files), `tests/VisualCommit.VisualTests.Phase4/*Support*`, phase 1's check file, `scripts/time-scripted-check.sh` |
| WP1 | Git: rebase onto, the classification (1.4), interactive rebase (`RebaseTodo`, the delivery, the list's commits and its parity test), squash, pull with rebase (and the configured pull's backup and stop detection), with tests | `GitRepository.Rebase.cs`, `Core/Git/RebaseTodo.cs`, `GitRepository.Remotes.cs` (pull part) |
| WP2 | Git: picks and reverts as lists, Continue and Skip for every state, the in-progress read's additions, reftable (8.1), with tests | `GitRepository.History.cs`, `GitRepository.InProgress.cs`, `GitRepository.Sequence.cs` |
| WP3 | Git: the conflicts' versions, taking a side, writing a result and marking it resolved, the merge tool, the backups' clean-up, with tests; Core's `ConflictText` | `GitRepository.Conflicts.cs`, `GitRepository.Backups.cs` (clean-up part), `Core/Conflicts/*` |
| WP4 | App: the operations (`RepositoryOperations.Rebase.cs`, `.Conflicts.cs`), the menus' items and facts, the banner's Continue and Skip, the stage panel's Continue, the texts, the clean-up's scheduling, with tests | `RepositoryOperations.*` new parts, `RepositoryMenus.cs`, `RepositoryMenuContext.cs`, `OperationTexts.*` new parts, `ConflictBanner*`, `WorkingChangesViewModel` (in-progress parts) |
| WP5 | App: the interactive list (view model, view, drag, keys), the message dialog, with tests | `ViewModels/Rebase/*`, `Views/Rebase/*`, `MessageDialog*` |
| WP6 | App: the resolver (view model, `ConflictPanes`, `ConflictResultEditor`, the view, the layout spanning two columns, Esc, the tokens and icons), with tests | `ViewModels/Conflicts/*`, `Views/Conflicts/*`, `Controls/Conflicts/*`, `Theme/Tokens.axaml` (new tokens), `Icons.axaml` |
| WP7 | The scripted checks of the report, part by part | `tests/VisualCommit.VisualTests.Phase4/Phase4Checks.*.cs`, `Phase4Expectations*.cs` |
| WP8 | The real-window pass for the RW checks | `tests/VisualCommit.RealWindowTests/Phase4RealWindowPass*.cs` |

Order: WP0 first (with the spike); then WP-S, WP1, WP2 and WP3 side by side; the report (step 7)
is written once WP-S has pinned the scenarios, and committed before WP4 to WP6 build UI; then WP4,
WP5 and WP6 side by side; then WP7 and WP8; then the review (step 21) and the gate. The lead keeps
`RepositoryViewModel*.cs`, `MainWindow.axaml(.cs)`, `CommitGraphView.axaml`, `GitRunner.cs`, the
`.slnx`, CI and the docs, and adds the hooks the packages need.

## 15. Decisions

Recorded in `decisions.md` with their reasons; in short:

| # | Decision | Section |
|---|---|---|
| D139 | The backups' clean-up: a backup is one operation's refs; gone after 30 days or beyond 50 (oldest first); never while any worktree has an operation in progress, never the restore bar's snapshot; at the end of every operation that saved one and in the background when a repository has opened; nothing on screen, a log line per backup | 9 |
| D140 | The resolver spans the graph and right-panel columns while open, ours and theirs above the result, the banner across it, files and conflicts navigated from its header and toolbar | 11.1, 11.2 |
| D141 | The interactive rebase's list replaces the graph, newest on top; an action box per row (and git's letters), drag or Alt+arrows to move; squash and fixup join the older row below; new messages through a message dialog | 2.2 |
| D142 | Rebase: from the commit, branch and drop menus; a drop's rebase changes the source; a branch that is not checked out is checked out first as a step of its own (git's `<branch>` argument is never passed); not while detached; uncommitted changes and every option are left to git's configuration; every rebase saves `rebase-<time>/<b>` and asks nothing | 1 |
| D143 | Interactive rebase: the list holds what git's todo would; a range with merges is refused; no `--root`; the todo comes from the app's own sequence editor (`cp` of a prepared file), with explicit `drop` lines; new messages by a guarded `exec git commit --amend --no-verify` reading a file next to the todo | 2.3 to 2.5, 3 |
| D144 | Continue and Skip per state (5.1); Continue commits with the stage panel's message before git continues (in a rebase with the stopped commit's author); the stage panel's button becomes Continue for a list or a rebase; Skip asks and saves `skip-<time>`; no Continue where git would silently drop a commit | 5 |
| D145 | Several picks and reverts: one git call (D136); a list git refused at its start is cleared with `--quit`, which keeps the user's changes; the outcome is read from git's files | 4 |
| D146 | Pick heads read as files, and through git (`--symbolic-full-name`) only in a reftable repository | 8.1 |
| D147 | Pull with rebase for one pull: the current branch's menu item, `git rebase` without arguments after the fetch, git's configuration not read; a pull's rebase saves the backup and is told stopped by the rebase folder | 6 |
| D148 | The resolver's content: the working-tree file is the source; the top panes show the file with every block's ours or theirs side; the result is the file, editable, without syntax highlighting; a whole side is the stage's working-tree form; non-text conflicts resolve at once through `checkout --ours/--theirs`, `add` and `rm`; Mark resolved writes and stages, asking when blocks remain; Esc never closes it from inside the result | 11 |
| D149 | The merge tool: `git mergetool --tool=<merge.tool> --no-prompt -c mergetool.keepBackup=false`, for text and binary conflicts only, as a cancellable operation, the files it leaves removed afterwards; disabled without `merge.tool` | 11.9 |
| D150 | Phase 1's checks 13 and 14 clone a bare copy of the 100k-commit repository through `AuthRemoteServer`, held at a fixed percentage, on a free port | 12.1, 13.2 |
| D151 | Phase 4's walk-through is the project `VisualCommit.VisualTests.Phase4`; `TempRepo` and the tests' isolation set and remove the editor variables | 13.2, 13.3 |

## 16. What the reviews changed

Filled in when the independent reviews of this design (git and data safety, UI and harnesses,
coverage and consistency) have been folded in.

