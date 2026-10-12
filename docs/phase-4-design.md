# Phase 4 design: rebase and conflict resolution

Working design notes for phase 4, written on 2026-10-12 before any phase 4 code exists, and
revised the same day after four independent reviews (section 16). The builders implement from this
file; its final facts are folded into `architecture.md` at the end of the phase, after which this
file is kept as history, as `phase-3-design.md` is. Section 15 lists the decisions this design
makes; they are recorded in `decisions.md` as D139 to D151. Where this file and merged code differ
later, the code's doc comments say what is built; tell the lead.

Sources: `plan.md` "Phase 4", the phase 3 handoff's "For the next phase", decisions D63, D81, D83,
D88, D89, D95, D97, D102, D110, D119, D122, D124, D128 and D134 to D138, the owner's answers below,
six research notes and four review notes made by sub-agents on 2026-10-12 with Git 2.36. Those notes
lived in the session's scratch folder; everything this design relies on is copied into section 0.

The owner's answers, taken as given:

1. (2026-10-11, D135) A rebase asks for no confirmation. When a rebase or a merge stops on a
   conflict, the user resolves it or aborts and is back where everything was. A rebase saves a
   backup ref of the branch's old tip first.
2. (2026-10-12, D136) Several commits are picked or reverted with one git command that takes the
   whole list, so that Continue, Skip and Abort act on the list.
3. (2026-10-12, D137) Backups are cleaned up automatically: after 30 days, or when more than a set
   number exist (the oldest first).
4. (2026-10-12, asked at the start of phase 4) **50** backups are kept besides the 30 days. The
   clean-up says **nothing on screen**; the log records each removal.
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
Every git call goes through `GitRunner` with D30, D68, D74, D102 and D128's environment. Counts in
texts use the singular for one (D113); a text written here with `{N} files` means `1 file` for one.

## 0. Facts checked before deciding

Run with Git 2.36.0.windows.1 in throw-away repositories, isolated as the tests are
(`GIT_CONFIG_NOSYSTEM=1`, `GIT_CONFIG_GLOBAL=/dev/null`, `LANGUAGE=en`, `GIT_EDITOR=:`,
`GIT_SEQUENCE_EDITOR=:`, `GIT_LITERAL_PATHSPECS=1`), and read in the app's code and the Avalonia and
AvaloniaEdit packages. Where CI's Git 2.55 may differ, the Git-layer tests run on both (phase 3's
"two gits" rule).

### 0.1 Rebase

| What | Result |
|---|---|
| `git rebase main` on `topic` (diverged) | exit 0, stdout empty; stderr `Rebasing (1/2)\rRebasing (2/2)\r\r\x1b[KSuccessfully rebased and updated refs/heads/topic.` (the runner splits at `\r`; the last part starts with `ESC[K`, which is removed before it is shown). `ORIG_HEAD` = the old tip. HEAD reflog `rebase (start): checkout main` … `rebase (finish): returning to refs/heads/topic` |
| Already up to date | stdout `Current branch topic is up to date.` (detached: `HEAD is up to date.`), exit 0, nothing written. Onto a descendant: a fast-forward, `Successfully rebased …` |
| `git rebase main topic` from another branch | checks `topic` out and **stays on it** after success, after `--abort` and when up to date. With `topic` checked out in another worktree: `fatal: 'topic' is already checked out at '<path>'`, exit 128 |
| The `<branch>` argument | git prefixes `refs/heads/` itself: `git rebase main refs/heads/topic` rebases a **detached** HEAD (`Successfully rebased and updated detached HEAD.`) and leaves `topic` where it was. The app never passes a branch argument (1.3) |
| A conflict stop (two conflicting commits) | exit 1; stdout `Auto-merging f.txt` / `CONFLICT (content): Merge conflict in f.txt`; stderr `Rebasing (1/3)error: could not apply dd76563... T1 topic edits one`, four `hint:` lines, `Could not apply dd76563... T1 topic edits one` |
| Files at a stop | `.git/rebase-merge/`: `head-name` (`refs/heads/topic`), `onto` and `orig-head` (full SHAs), `done` (the commands done, full SHAs), `git-rebase-todo` (left), `msgnum`/`end` (count every command, `label` and `exec` included), `message` (the message Continue uses), `author-script` (`GIT_AUTHOR_NAME='…'` / `GIT_AUTHOR_EMAIL='…'` / `GIT_AUTHOR_DATE='@<seconds> <zone>'`, shell-quoted), `stopped-sha` (full), `patch`, `interactive` (also for a plain rebase), `rewritten-list` after the first commit; `amend` at an `edit` stop. Also `.git/REBASE_HEAD` (= `stopped-sha`), `ORIG_HEAD`, `AUTO_MERGE`, `MERGE_MSG`. HEAD is detached |
| Git 2.55 (found on CI) | at a conflict stop `rebase-merge/message` ends with git's `# Conflicts:` block (`# Conflicts:` and a `#	<path>` line per conflicted file), which 2.36 does not write: the app leaves that block out of the message it fills (5.3), except after a fixup or squash, where it leaves out every comment line as git does. An autostash that conflicts when put back is told in other words (`Your local changes are stashed, however applying them` / `resulted in conflicts.`): the app tells that state by the conflicted files left after an exit 0 with no rebase folder (1.4), never by git's words. In `rebase-merge/done` and `git-rebase-todo` each line's subject is written as a comment (`pick <sha> # First change`); the app reads only the command and the SHA |
| Stale files | `REBASE_HEAD` and `AUTO_MERGE` stay after a rebase or sequence finishes: neither means "in progress". Only `rebase-merge/` or `rebase-apply/` does, **also after an exit 0** (an `edit` stop exits 0 with the folder present) |
| Stages in a rebase | stage 2 ("ours") = HEAD = the new base plus the commits already replayed; stage 3 ("theirs") = the user's commit being replayed. Markers `<<<<<<< HEAD` / `>>>>>>> <short> (<subject>)` |
| `git rebase --continue` after `git add` (`GIT_EDITOR=:`) | commits with `rebase-merge/message` (author and author date kept) and goes on; the next stop exits 1 with the same lines. It starts the editor on `.git/COMMIT_EDITMSG`, so `GIT_EDITOR=:` strips every line of the message that starts with `#` (`#12 issue line` lost). `-c commit.cleanup=whitespace` writes git's whole comment block into the message: unusable. It runs **no** `pre-commit` and **no** `commit-msg` hook |
| Committing at a stop before Continue | `git commit --no-verify --cleanup=whitespace --file=-` with `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL` and `GIT_AUTHOR_DATE` taken from `author-script` (a name with a quote and a non-ASCII letter, a date `@<seconds> +0230` included) keeps the message exactly, the author and the date; `git rebase --continue` then accepts the commit, makes no other, and `rewritten-list` maps the stopped commit to it. Without the author variables the author date is the current time |
| Continue variants | unstaged or unresolved: stdout `f.txt: needs merge` / `You must edit all merge conflicts and then` / `mark them as resolved using git add`, exit 1, nothing changes. Resolved to "no change" (nothing staged): Continue **drops the commit silently** and goes on |
| `git rebase --skip` | drops the stopped commit, cleans the index and working tree itself (a staged resolution is lost), exit 0 at the end (`Successfully rebased …`) or 1 at the next stop |
| `git rebase --abort` | silent, exit 0; the branch back at `orig-head`, HEAD on that branch (also when the rebase began by checking it out); HEAD reflog `rebase (abort): updating HEAD`, the branch's reflog untouched |
| Nothing in progress | `--continue`, `--skip`, `--abort`: `fatal: No rebase in progress?`, exit 128. Another rebase in progress when one starts: `fatal: It seems that there is already a rebase-merge directory, and …`, exit 128 |
| Dirty working tree | unstaged: `error: cannot rebase: You have unstaged changes.` / `error: Please commit or stash them.`, exit 1; staged: `error: cannot rebase: Your index contains uncommitted changes.` and the same second line; nothing starts. Untracked files only: rebases. With `rebase.autoStash=true` git stashes and re-applies (`Created autostash: …`, `Applied autostash.`); when putting the changes back conflicts: exit 0, stderr `Applying autostash resulted in conflicts. Your changes are safe in the stash.`, `stash@{0}: autostash` left, the files `UU`, nothing in progress |
| Untracked file in the way | at the start: `error: The following untracked working tree files would be overwritten by checkout:` … `error: could not detach HEAD`, exit 1, nothing starts. In the middle: the pick is **rescheduled** (`hint: Could not execute the todo command` … `It has been rescheduled`), exit 1, stopped with no conflicted file; `done` ends with the **previous** command, the rescheduled pick is the first line of `git-rebase-todo` and `stopped-sha` names it; both `--continue` and `--skip` try it again (neither leaves it out) |
| A failed `exec` | stops with `Execution failed: <cmd>`; `done` ends with the `exec` line; no `stopped-sha`, `message` or `author-script`; `--continue` goes on without running it again (`no-reschedule-failed-exec`) |
| An `edit` stop (a todo from elsewhere) | exit 0 with `rebase-merge/` present and an `amend` file; `--continue` with something staged amends HEAD itself |
| Commits in the range | a merge commit is dropped (the history is linearised; `--rebase-merges` keeps it); a commit whose patch is upstream is skipped (`warning: skipped previously applied commit …`); a commit that becomes empty is dropped by a plain rebase (`dropping <sha> <subject> -- patch contents already upstream`) and **stops** an interactive one (`The previous cherry-pick is now empty, possibly due to conflict resolution.` and a `CHERRY_PICK_HEAD` file next to `rebase-merge/`) |
| rerere with `autoUpdate` | `Staged 'f.txt' using previous resolution.`, exit 1, stopped with no unmerged path |
| Exit codes | 0 done (unless the folder is still there); 1 with `rebase-merge/` (or `rebase-apply/`) present: **stopped**; 1 without it: refused, nothing changed; 128: fatal refusal (hook, another rebase, worktree, no rebase). A stop does not imply unmerged paths |
| Fork point | `git rebase` with no arguments (and `git pull --rebase`) use the upstream's reflog: a commit the upstream once held is left out. Made on `main`, `topic` branched from it, `main` reset back: pulling `topic` with rebase drops that commit; `git rebase main` keeps it |
| A user's `rebase.backend=apply` | uses `.git/rebase-apply/` (no `applying` file) with other texts; `-i` always uses the merge backend |
| Options on 2.36 | `--no-update-refs` unknown (2.38+); `-c rebase.updateRefs=false` accepted; `--no-autostash`, `--merge` exist. `git config --type=bool --get rebase.autoStash` reads git's booleans |

### 0.2 Interactive rebase

| What | Result |
|---|---|
| How git runs the sequence editor | as `sh -c '<GIT_SEQUENCE_EDITOR> "$@"' '<GIT_SEQUENCE_EDITOR>' <todo>`: the value is followed by `"$@"`, the todo's absolute path (forward slashes, spaces kept); working folder the worktree's top. A `GitCommand`'s own environment overrides `GitRunner`'s defaults (`DefaultEnvironment.Concat(command.Environment)`), so one call can set its own editor while D102's `:` stays the default |
| A chain of commands as the editor | `cp '<m1>' "$(dirname "$1")/vc-message-1" && cp '<todo>' "$1"` **fails**: the appended `"$@"` gives the last `cp` three operands (`cp: target … is not a directory`, `error: There was a problem with the editor …`, exit 1, nothing started). A shell function works: `vc() { cp '<m1>' "$(dirname "$1")/vc-message-1" && cp '<todo>' "$1"; }; vc` (the appended `"$@"` becomes the function's arguments); checked with two message files and paths with a space and an apostrophe (written `'\''`), backslash and forward-slash Windows paths |
| The todo file | CRLF lines are accepted; a UTF-8 **BOM** breaks it (`error: invalid line 1: ﻿pick …`). Full SHAs without subjects, `pick`/`p`, `drop`, `squash`, `fixup`, `reword`, `exec`, `label` (2.18+). `fixup -C` exists in 2.36 but not in 2.30, the app's minimum |
| Unchanged leading commits | keep their SHAs (git fast-forwards over them); an identity todo leaves the tip as it was |
| `squash` with `GIT_EDITOR=:` | messages joined by a blank line, git's `#` lines removed, **and the commits' own `#` lines lost** |
| `reword` with `GIT_EDITOR=:` | message "unchanged" but `#` lines stripped, and the commit rewritten. Git's own `reword` runs the `pre-commit` and `commit-msg` hooks (a commit-msg hook's line was added); `git cherry-pick --continue` runs both too; `git rebase --continue` after a conflict runs neither |
| A new message through `exec git commit --amend --only --allow-empty --cleanup=whitespace --quiet --file=<path>` | works: message exactly the file (`#` lines kept), author and date kept. Hazard: if the pick before it is skipped or emptied, the `exec` amends whatever HEAD is. A guard holds: `label vc-1` before the pick, and the exec only amends when `HEAD^` equals `refs/rewritten/vc-1`: correct when clean, when the pick is skipped, when it conflicts and is resolved then continued, and when a `fixup` member conflicts; also from a linked worktree whose path has spaces (`refs/rewritten/` is per worktree). `"$(git rev-parse --git-path rebase-merge/vc-message-1)"` finds the message from any worktree. `refs/rewritten/vc-1` exists during the rebase and is deleted at its end and at `--abort` |
| A group whose first pick is skipped or emptied | the `fixup` lines after it are applied to HEAD, i.e. joined to the commit **before** the group (for the first group, the new base's own tip is rewritten and the branch no longer contains it) |
| A fixup member that conflicts, continued | the target's own `#` lines are stripped (git's message handling with `GIT_EDITOR=:`); the guarded amend restores the group's message |
| `exec` output | every exec prints `Executing: <command>` on stderr; `exec` and `label` lines count in `Rebasing (n/m)`, `msgnum` and `end` |
| `rebase.missingCommitsCheck=error` | a todo that leaves a commit out stops with `Warning: some commits may have been dropped accidentally.` …; an explicit `drop <sha>` line passes |
| Invalid todo | `error: invalid line 4: …` + `You can fix this with 'git rebase --edit-todo' …`, exit 1, **left in progress** (HEAD detached at `onto`, `rebase-merge/` without `done`) |
| The editor command fails | `error: There was a problem with the editor '<cmd>'.`, exit 1, **nothing started** |
| Only `drop` lines | the branch moves to `onto`; empty todo: `error: nothing to do`, exit 1, nothing changed |
| `rebase.updateRefs` (Git 2.38+, from git's source, not run here) | git writes `update-ref` lines into the todo it makes; a todo the app writes has none, so other branches do not move |

### 0.3 Cherry-pick and revert lists (D136)

| What | Result |
|---|---|
| `git cherry-pick A B C`, B conflicts | exit 1; stdout `[main b68aa9c] A adds a.txt` … `CONFLICT (content): Merge conflict in f.txt`; stderr `error: could not apply 4f7880e... B changes line2` and hints. Files: `CHERRY_PICK_HEAD` (B), `MERGE_MSG` (`B changes line2\n\n# Conflicts:\n#\tf.txt\n`), `AUTO_MERGE`, `sequencer/head` (HEAD before the command), `sequencer/abort-safety` (HEAD after the last pick), `sequencer/todo` (`pick 4f7880e B changes line2` / `pick cfce4e5 C adds c.txt`: the stopped pick stays first, abbreviated SHAs) |
| A single `git cherry-pick B` that stops | `CHERRY_PICK_HEAD`, `MERGE_MSG`, `AUTO_MERGE`, **no `sequencer/`** |
| Resolve, `git add`, `--continue` | exit 0; commits B with `MERGE_MSG` less every `#` line (B's author and date; the commit's own `#` lines are lost too), then picks C; `sequencer/` removed |
| Resolve, `git commit --file=-` with a message, then `--continue` | exit 0, picks only C; the message kept as given (D110) |
| `--skip` at B | exit 0, drops B and picks C; **a staged resolution is lost without a word** |
| `--abort` at B | exit 0, `reset --merge` to `sequencer/head` (before A): keeps untracked files and unstaged changes to other files, **drops staged ones**. After HEAD moved (B committed by hand): `warning: You seem to have moved HEAD. Not rewinding, check your HEAD!`, exit 0, HEAD kept |
| `git revert --no-edit C B A` | the same shape: `REVERT_HEAD`, `MERGE_MSG` `Revert "B changes line2"\n\nThis reverts commit …\n\n# Conflicts:…`, `sequencer/opts` (`edit = false`), todo `revert …` lines; `git revert --continue` and `--abort` as for picks |
| An empty pick in a list (already on HEAD) | exit 1, `CHERRY_PICK_HEAD`, `MERGE_MSG`, sequencer kept; stderr `The previous cherry-pick is now empty, possibly due to conflict resolution.` / `If you wish to commit it anyway, use:` / `    git commit --allow-empty` / `Otherwise, please use 'git cherry-pick --skip'`. `--continue` repeats it (exit 1); `--skip` goes on |
| A pick resolved to "ours" (nothing staged) | `--continue` exits 1 with the same "now empty" text; `--skip` goes on |
| An empty revert in a list (also the first of the list) | exit 1, **no `REVERT_HEAD`**, `MERGE_MSG` and `sequencer/` kept, only the status text on stdout ending `nothing to commit, working tree clean`; when it is the first, HEAD = `sequencer/head` = `abort-safety`. `--continue` exits 0, leaves it out and reverts the rest |
| A later pick refused without a conflict (an untracked file in the way) | exit 128, `error: The following untracked working tree files would be overwritten by merge:` … `fatal: cherry-pick failed`; `sequencer/` kept with the refused pick first, **no `CHERRY_PICK_HEAD`, no `MERGE_MSG`**, HEAD = `abort-safety` ≠ `sequencer/head`. `--continue` then **silently drops the refused commit**; `--skip` drops it knowingly |
| A list refused at its start (a staged change anywhere, or a local change the first pick touches) | exit 128 (`error: your local changes would be overwritten by cherry-pick.` / `fatal: cherry-pick failed`, or the overwritten-by-merge text), no `MERGE_MSG`, HEAD unchanged, **`sequencer/` written anyway** (head = abort-safety = HEAD). `--abort` there **drops the staged change**; `--quit` removes `sequencer/` and keeps it |
| Wrong verb | during a revert sequence: `git cherry-pick --continue` → `error: cannot cherry-pick during a revert.` (128); `git cherry-pick --abort` aborts the revert |
| Nothing in progress | `git cherry-pick --continue` / `--abort`: `error: no cherry-pick or revert in progress` / `fatal: cherry-pick failed` (128); `--skip`: `error: no cherry-pick in progress`; after the stopped pick was committed by hand: `error: there is nothing to skip` / `hint: have you committed already?` (128) |
| Unmerged paths left | `--continue` (pick, revert, merge) and `git commit`: stdout `U\tf.txt`, stderr `error: Committing is not possible because you have unmerged files.` … `fatal: Exiting because of an unresolved conflict.` (128). A rebase says `f.txt: needs merge` (exit 1, 0.1) |
| A single pick plus `--skip` | exit 0, `CHERRY_PICK_HEAD` gone, HEAD unchanged |
| `git merge --continue` | exists in 2.36; commits `MERGE_MSG` less `#` lines; with nothing staged after a resolution to "ours" it still makes the merge commit |

### 0.4 Conflicts

| What | Result |
|---|---|
| Stage ids | `git status --porcelain=v2 -z`: `u <XY> <sub> <m1> <m2> <m3> <mW> <h1> <h2> <h3> <path>`, zeros for a missing stage; `sub` is `S...` for a submodule. Phase 3's parser keeps only the path (`GitRepository.Changes.cs:80-88`) |
| Reading a stage | `git cat-file blob <oid>` is byte-exact in the repository's form; `git cat-file --filters --path=<p> <oid>` gives the working-tree form (CRLF under `core.autocrlf=true`) and writes nothing. `git checkout-index --stage=all --temp` writes temp files **into the working tree**: not used |
| Kinds (git's words, XY) | both modified `UU` (text: markers; **binary or symbolic link: no markers, ours in the working tree**; submodule: `S...`, working tree untouched); both added `AA` (markers, no stage 1); deleted by them `UD` (ours in the tree); deleted by us `DU` (theirs in the tree); added by us `AU` / by them `UA` (a rename/rename); both deleted `DD` (no file). A file/folder clash: `CONFLICT (file/directory): directory in the way of p from HEAD; moving it to p~HEAD instead.`, the status line `u AU p~HEAD` |
| Taking a side | `git checkout --ours|--theirs -- <p>` writes that stage in working-tree form, **overwriting any edit in the working-tree file**, and does not mark the path resolved; a missing side: `error: path '<p>' does not have our version` (or `their`), exit 1. **`git restore --ours` on a path without stage 2 deletes the file silently**: never used |
| Marking resolved | `git add -- <p>` (accepts markers without a word: `git diff --cached --check` finds them, `leftover conflict marker`, exit 2); `git rm -- <p>` for a deleted side and for `DD` |
| Undoing a resolution before the commit | `git update-index --unresolve <p>` (no `--`) or `git checkout -m -- <p>`; `git checkout -m` on a path that is not unmerged **discards unstaged edits**: never run on such a path |
| Marker labels (merge, cherry-pick, revert, rebase, stash) | `HEAD` / the merged name as typed; `HEAD` / `<short> (<subject>)`; `HEAD` / `parent of <short> (<subject>)`; `HEAD` / `<short> (<subject>)`; `Updated upstream` / `Stashed changes`. A side whose path differs gets `:path` (`HEAD:rm2.txt`) |
| Hunks | the working-tree file uses the user's `merge.conflictStyle` (`merge`, `diff3`, `zdiff3`) and `conflict-marker-size`; the default style joins conflicts three or fewer lines apart (or separated only by lines without letters or digits) into one block, with the common lines on both sides |
| `git mergetool` with stdin closed | `-c <name>=<value>` must come **before** the subcommand: `git mergetool … -c mergetool.keepBackup=false -- f` prints its usage and exits 1; `git -c mergetool.keepBackup=false mergetool --tool=<t> --no-prompt -- <p>` works. Without `merge.tool` it guesses a tool and **prompts anyway** (`Hit return to start merge resolution tool (vimdiff): `), exit 1, leaving `<name>_BACKUP_<pid>.<ext>`, `_BASE_`, `_LOCAL_`, `_REMOTE_` in the working tree. With a tool it runs `mergetool.<t>.cmd` (or git's own command line for `<t>`) in the worktree's top folder with `$BASE`, `$LOCAL`, `$REMOTE` (temp files in the working tree) and `$MERGED`; it waits for the tool to exit; on success it runs `git add` itself. Without `trustExitCode` success is judged by the file's modification time (wrong in 1 of 6 quick runs of a scripted tool); with `trustExitCode=true` exit 0 stages the file even with markers. Deletion, symbolic-link and submodule conflicts always prompt and never run the tool. An unavailable tool: `The merge tool <t> is not available as '<t>.exe'`, exit 1. `git config --get merge.tool` takes 0.06 s; `--tool-help` 18 s |
| rerere | `Resolved 'f.txt' using previous resolution.`: the path stays `UU` with no markers left |

### 0.5 Reftable, `TempRepo`, the clone of check 13

| What | Result |
|---|---|
| Git 2.36 and reftable | `git init --ref-format=reftable` exits 129 (unknown option); `git rev-parse --show-ref-format` **prints `--show-ref-format` and exits 0**. Under reftable (Git 2.45+; from git's documentation) `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `REBASE_HEAD`, `ORIG_HEAD` and `AUTO_MERGE` are refs, `MERGE_HEAD` and `FETCH_HEAD` stay files, and the repository's config has `extensions.refStorage = reftable` |
| `git rev-parse --verify --quiet CHERRY_PICK_HEAD` | the SHA while a pick is stopped, exit 1 otherwise; **a branch named `CHERRY_PICK_HEAD` answers too**. `--symbolic-full-name` tells them apart (`CHERRY_PICK_HEAD` against `refs/heads/CHERRY_PICK_HEAD`; with both, an `ambiguous` error and exit 0) |
| `TempRepo` and editors | `TempRepo`'s environment sets no `GIT_EDITOR` or `GIT_SEQUENCE_EDITOR`, and `GitIsolation` does not remove the machine's `GIT_EDITOR`, `GIT_SEQUENCE_EDITOR`, `EDITOR` and `VISUAL`: a scenario that runs `git rebase --continue` started the editor (`vi` when nothing is set) |
| Dates of rewritten commits | with `GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` set, a rebase keeps each commit's author date and gives every rewritten commit the environment's committer date: the app's rebases in the tests are reproducible (D72) |
| The 100k-commit clone over HTTP | `AuthRemoteServer` serves a clone; a protocol-v2 clone's pack request is the `command=fetch` POST that `HoldNextFetchPack` holds; phase 3's hold stops after the first side-band-2 packet with a `%` (`Counting objects:   0% … 3%` for 100,002 objects, several lines per packet). The shared 100k repository is not bare, so the server cannot serve it in place |

### 0.6 The app and its libraries

| What | Result |
|---|---|
| Menus and popups | context menus are Avalonia's `MenuFlyout` on the window's overlay layer (D105), drivable and capturable in both harnesses. A `ComboBox`'s dropdown is a `Popup` that is a window of its own unless set otherwise: not in a headless capture, a separate window in the real one. The app has no `ComboBox` today |
| Undo keys | AvaloniaEdit 12.0.0 binds undo, redo, copy and paste to the operating system's modifier (`IsOSPlatform(OSX)`: Cmd on macOS), while the headless driver uses Avalonia's `HotkeyConfiguration`. The app has no window-wide undo binding |
| The caret | AvaloniaEdit has no public way to stop its caret blinking (`CaretLayer`'s timer is private); the diff panes hide its caret with a transparent brush |
| Focus | a focused button that leaves the visual tree gives the focus to an element of Avalonia's choosing (`MainWindow.axaml.cs:290-293`); bar buttons in `HunkHeaderLayer` are rebuilt on every redraw |
| Esc | `MainWindow` sees Esc with `handledEventsToo` and asks its handlers by `EscapeOrder` (Menu 100, Drag 200, Dialog 300, Diff 400); the diff's handler declines only while a `TextBox` has the focus |
| Phase 2's diff panes | `DiffPane` is `internal sealed`, built for a `DiffTextView` owner (`Words`, `Syntax`, `HunkActions`, `RaiseHunkAction`); `HunkHeaderBar` puts buttons on the right pane only, from a closed enum, with fixed automation ids; row colours come from `DiffLineKind` (context, added, removed) |
| Background operations | the queue's background slot is used by auto-fetch only: the status bar shows `Auto-fetch failed` for any failed background operation and `Waiting for auto-fetch…` while any runs and a user operation waits (`OperationStatusViewModel.cs:155-164, 224-227`) |
| Cancelling | only network operations have a cancellation source; a tab's disposal cancels a running one; on Windows git's job object ends everything git started, a program it launched included (`GitRunner.cs:323-341`) |
| The confirmation dialog | `ConfirmationRequest(Title, Question, Detail, ConfirmText, IsDestructive)`; its cancel button always says `Cancel` (`ConfirmationView.axaml:43`) |
| Pull | `GitRepository.IntegratePullAsync` chooses between a fast-forward and a rebase inside one call; backups are saved by the app (`RepositoryOperations.SaveBackupAsync`) |
| Discard snapshots | named from `DateTimeOffset.UtcNow` (`GitRepository.Writes.cs:136`), the other backups from `BackupClock` |

## 1. Rebase onto a commit or a branch

### 1.1 Where it is offered

| Menu (builder) | Item | Runs |
|---|---|---|
| A commit (`ForCommit`), group 2 after "Revert this commit" | `Rebase {cur} onto this commit` | `git rebase <sha>` |
| A local branch other than the current one (`ForLocalBranch`), group 1 after the two merges | `Rebase {cur} onto {b}` | `git rebase <b's tip>` |
| A remote branch (`ForRemoteBranch`), group 1 after the two merges | `Rebase {cur} onto {r}/{b}` | `git rebase <its tip>` |
| Branch A dropped onto branch B (`ForDrop`), A local and checked out | `Rebase {A} onto {B}` | `git rebase <B's tip>` |
| A dropped onto B, A local and not checked out | `Check out {A} and rebase it onto {B}` | a checkout of A (phase 3's), then `git rebase <B's tip>` |

The app passes the target's SHA as it was read when the menu opened (D124's rule: an operation acts
on what the user saw; auto-fetch can move a remote branch meanwhile); texts name the target by the
name the user chose. A drop's merge items change the target ("Merge A into B"); its rebase items
change the source ("Rebase A onto B"): that is what the gesture means for a rebase (GitKraken's
reading too). A remote branch is never rebased: a drop whose source is a remote branch offers no
rebase item, as phase 3 offers no push for one. A drop onto a remote branch offers the rebase items
after the two push items.

Disabled reasons, the object's own first (phase 3's order rule):

1. The rebased branch already contains the target: a commit reachable from HEAD →
   `This commit is already on {cur}` (phase 3's `AlreadyOn`); a branch whose tip the rebased branch
   reaches → `{b} is up to date with {target}` (`UpToDateWith`; for a drop `{A} is up to date with
   {B}`). Git would only say "Current branch … is up to date". For the commit and branch menus this
   comes from the loaded graph (`IsReachableFromHead`, and `RepositoryMenuContext.TipsOnHead`: the
   SHAs of the local and remote branch tips that HEAD reaches, computed when the context is built);
   a commit the graph has not reached yet counts as not on HEAD, and git decides (phase 3's rule).
   For a drop it is read before the menu opens: `DropFacts.SourceContainsTarget` (`bool?`, `git
   merge-base --is-ancestor <B> <A>` for a local or remote B); null (not read, or the read failed)
   leaves the items enabled. The fast-forward keeps its own field and fallback.
2. For "Check out {A} and rebase it onto {B}": A checked out in another worktree →
   `{A} is checked out in another worktree ({folder})` (D120's text).
3. HEAD detached → `Check out a branch to rebase` (as merge disables itself).
4. Unborn → `Make the first commit first`.
5. An operation in progress → `Finish or abort the {kind} first`; conflicts without an operation (a
   stash that did not apply cleanly) → `Resolve the conflicts first` (D119's texts).

Uncommitted changes do not disable the items: git refuses a dirty tree unless the user set
`rebase.autoStash`, and the app follows git's configuration (Q5). Git's refusal opens the error
dialog (1.4). The exception is "Check out {A} and rebase it", which checks first (1.3), because
the checkout would carry the changes along and leave the user on A when git then refuses.

A plain rebase drops the merge commits of the range and linearises the history, as `git rebase`
does (0.1); the interactive rebase refuses such a range (2.1), because its list cannot show them.
The plain rebase follows git here (Q5) because its most common use is exactly that: a branch that
merged its upstream in and is now rebased onto it. A merge's own changes (a resolution, an "evil
merge") are not lost for good: the rebase's backup keeps the old tip (1.2).

### 1.2 Backup and no question

No confirmation (D135). Inside the operation, just before the rebase starts, the app saves
`refs/visualcommit/backup/rebase-<time>/<b>` pointing at the branch's old tip (one
`BackupRequest("rebase", [BackupEntry(<b>, <old tip>)])`, phase 3's batch). Every rebase the app
starts saves one: rebase onto, an interactive rebase, a squash, and a pull that rebases (configured
or chosen, section 6). The backup is saved also when the rebase then says "up to date"; the
clean-up (section 9) takes care of the extra.

### 1.3 What runs

`RepositoryOperations.RebaseAsync(onto)`: one local operation "Rebase {b} onto {target}"
(`RefreshScope.Refs`, prompts `Ask` as phase 3's local operations). Inside it: read HEAD and refuse
when it moved since the click (D124: "HEAD moved while this waited: …"); refuse when an operation is
in progress or files conflict (a fresh status, `RefuseWhileInProgressAsync`); save the backup; run
`git rebase <onto sha>`.

"Check out {A} and rebase it onto {B}" is one operation of several steps (`HasSeveralSteps`):

1. Unless `git config --type=bool --get rebase.autoStash` says true, refuse when the status has
   staged or unstaged changes to tracked files, with 10.1's text for a dirty tree; nothing changes.
2. Phase 3's checkout of A.
3. The backup of A and `git rebase <B's tip>`.

When that rebase stops, the app writes the branch the user was on before into
`rebase-merge/visualcommit-return` (it goes away with the folder). The app's Abort reads it before
`git rebase --abort` and checks that branch out again afterwards, so the user is back where
everything was (owner's answer 1); the Abort question says so (`… and {branch} is checked out
again`). An abort from a terminal ignores the file.

The app never passes `git rebase` a branch argument (0.1). It passes no option that changes git's
configured behaviour: not `--merge`, not `--no-autostash`, not `--rebase-merges`, not
`-c rebase.updateRefs=false` (Q5). A user's `rebase.backend=apply` gives `rebase-apply/`, which
phase 3 already reads as a rebase.

### 1.4 How it ended

Classification after `git rebase` (`GitRepository.ClassifyRebaseAsync`, shared by every rebase
command of sections 2, 3, 5 and 6; `RebaseResult(RebaseOutcome Outcome, string? NewTip, int
ConflictedFiles)`). Whether `rebase-merge/` or `rebase-apply/` existed is read **before** the call;
when one did, the app's rebase is refused before it runs ("in progress", 1.3), so a folder seen
after the call is this call's own:

| Exit | After | Result | Status bar |
|---|---|---|---|
| 0 | no rebase folder, stdout has `is up to date` | `UpToDate` | `{b} is already up to date with {target}` |
| 0 | no rebase folder | `Rebased` (the new tip) | `Rebased {b} onto {target}` |
| 0 | no rebase folder, conflicted files in the status (git 2.36 says `Applying autostash resulted in conflicts`, 2.55 other words: 0.1) | `Rebased`, with the conflicts of the put-back changes | warning: `Rebased {b} onto {target}; your changes conflicted when put back: they are also kept in the stash` (the banner shows the conflicted files, as phase 3 shows a stash's) |
| any | a rebase folder present | `Stopped` (conflicted file count from the status) | `Rebase stopped: {N} files have conflicts` or, with none, `Rebase stopped at {short} "{subject}"` (warning tone, no dialog unless 5.1 says so) |
| non-zero | no rebase folder | a `GitException`: nothing changed | `Rebase {b} onto {target} failed` and the error dialog "Rebase failed" with git's output |

### 1.5 After the rebase

The graph reloads (refs changed). When a rebase the app runs ends rebased (started from a menu, the
list or a squash, or finished by Continue or Skip), the rewritten branch's new tip is selected if
the selected commit was one of the branch's commits before the rebase, whether or not that old
commit is still drawn (another ref may still reach it), or was dropped; otherwise the selection
stays as it was. A squash of selected commits is the exception: it selects the squashed commit
(3). The working-changes row is unaffected; when it was selected and the working tree becomes clean,
phase 2's rule applies (nothing is selected, 14.3).

## 2. Interactive rebase

### 2.1 Where it is offered

| Menu | Item | The list holds |
|---|---|---|
| A commit (`ForCommit`), right after "Rebase {cur} onto this commit" | `Interactive rebase of {cur} from here…` | the commits of `<sha>..HEAD`; the base is `<sha>`, which stays as it is |
| A local branch other than the current one | `Interactive rebase of {cur} onto {b}…` | the commits `git rebase -i <b>` would list (2.3) |
| A remote branch | `Interactive rebase of {cur} onto {r}/{b}…` | the same with the remote branch |
| A drop, A checked out | `Interactive rebase of {A} onto {B}…` | the same |
| A drop, A not checked out | `Check out {A} and rebase it onto {B} interactively…` | the same; the checkout happens when Start is clicked, as the first step (1.3's dirty-tree check first) |

"From here" is enabled only for a commit on HEAD's **linear first-parent line**: HEAD, its first
parent, and so on until the first merge commit (which may be the base itself) or the root. Reasons,
in order: `{cur} has no commits after this one` (the commit is HEAD's); `This commit is not on
{cur}` (not reachable); `A merge lies between this commit and {cur}` (reachable but not on the
linear line); then 1.1's reasons 3 to 5. The line is computed by a new
`CommitGraphData.LinearLineFromHead()` (cached like `IsReachableFromHead`) and given to the menu
as `CommitMenuInfo.HeadLineDepth` (an init property: the distance from HEAD, null when not on the
line).

The onto items share 1.1's reasons 2 to 5, not reason 1: when the target is already in the branch
(the branch is simply ahead of it), the list holds the branch's own commits after it, which is the
most common use. What can only be known from the list (2.3) is shown in the list instead of rows,
with only `Cancel`: the range has merge commits (`A merge lies between {target} and {b}:
interactive rebase would flatten it.`), the branch has nothing of its own after the target (`{b}
has no commits of its own after {target}.`), or more than 1,000 commits (`{N} commits are too many
for one interactive rebase.`). No `--root`: the first commit of a history can be neither edited nor
squashed in phase 4 (D143).

### 2.2 The list (owner's answer 6)

`InteractiveRebaseViewModel` (`RepositoryViewModel.InteractiveRebase`; `IsInteractiveRebaseOpen`),
drawn by `Views/Rebase/InteractiveRebaseView` in the graph area in place of the graph, as the diff
view is; the banner, the left panel and the right panel stay. Opening it closes an open diff (after
2.2's leaving rule below).

```
┌ Interactive rebase of feature onto main ─────────────── 3 commits ┐  header, 36
│ ⠿ [Pick   ˅] c3d4e5f Add tests                      Test Author   │  rows, 30
│   ⠿ [Squash ˅] b2c3d4e Fix typo                     Test Author   │  (indented 16, joined by a line)
│ ⠿ [Pick   ˅] a1b2c3d Add login                      Test Author   │
│               Message: Add login                     [Edit…]      │  a second line, 22
│   Base  9f8e7d6 Release 1.0                                      │  the base row, 30
│                                      [Cancel]  [Start rebase]     │  footer, 44
└───────────────────────────────────────────────────────────────────┘
```

**Layout** (logical pixels):

- **Header** (36, `RebaseListHeader`): `Interactive rebase of {b} onto {target}` (from here:
  `Interactive rebase of {cur} from {short}`), cut with "…" with its whole text as tooltip, and on
  the right `{N} commits` (`RebaseListCount`).
- **Rows** (30 each, `RebaseRow`, named `{short} {subject}`), newest on top (the graph's order),
  scrolling between header and footer, a virtualising list: a drag handle column 24 wide
  (`IconGrip` 14, `VcTextSecondaryBrush`, centred); the action button (`RebaseActionButton`,
  `VcActionButton`, 84 × 22, at x 28: the action's name and `IconChevronDown` 10 × 10 on its
  right; not focusable); the short SHA at x 120, 64 wide, code font 12, secondary colour; the
  subject from x 192, cut with "…" and its whole text as tooltip; the author 140 wide at the right,
  shown only when the list is at least 520 wide. A squash or fixup row's whole content (its grip,
  button, SHA and subject) moves 16 right; a 2-px line in `VcLane0Brush` at x 12 runs from its
  vertical centre to the top edge of its target row. The selected row has `VcSelectionBrush`, a
  hovered one `VcRowHoverBrush`.
- **Second line** (22, `RebaseRowSecondLine`) under a row whose message will change: `New message:
  {subject}` for a reworded row, `Message: {subject}` for the target of a group with a squash; an
  `Edit…` button (`RebaseEditMessageButton`) at its right opens the message dialog (7.2).
- **The base row** (30, `RebaseBaseRow`): `Base` in the secondary colour, the short SHA and the
  subject; not selectable, never moved.
- **Footer** (44, docked at the bottom): `Cancel` (`RebaseCancelButton`) and `Start rebase`
  (`RebaseStartButton`, `VcPrimaryButton`), right-aligned, 8 apart, 12 from the right edge.
- **A list that cannot be made** (2.1): `RebaseListMessage` in the rows' place, and only `Cancel`.

**The actions.** The action button opens a context menu through the menu layer (D105): header
`{short} {subject}`, then Pick, Reword…, Squash, Fixup, Drop, leaving out the row's current action;
on the bottom row Squash and Fixup are disabled with `The oldest commit has nothing below it to
join`. The same menu is the row's context menu (right-click, Shift+F10, the Menu key). On the
selected row the keys `p`, `r`, `s`, `f` and `d` (git's letters) set the action; a key that cannot
apply does nothing.

- **Pick**: as it is. **Drop**: the row's SHA and subject struck through in the disabled colour.
- **Reword**: opens the message dialog titled `Reword {short}`, filled with the commit's message
  (`Save`); Cancel there leaves the action as it was. Then the second line.
- **Squash and fixup** join the **target**: the nearest row below that is Pick or Reword; Drop,
  Squash and Fixup rows between are passed over (a dropped row in between is drawn outside the
  group's line). A squash or fixup row with no target below it turns into Pick, as the bottom row
  does. A group with at least one squash has a message: it starts as git's own, the target's
  message (its new one when reworded) followed by each squash member's message in history order,
  joined by a blank line; fixup members add nothing; `Edit…` changes it (`Message of the squashed
  commit`, `Save`). An edited message is kept while the group keeps its target. A group of fixups
  only keeps the target's message (its new one when reworded).
- **Moving rows**: a drag starts anywhere on a row except its buttons (4 px threshold, as D87);
  the dragged row stays in place at opacity 0.5 and a 2-px `VcDragTargetBrush` line marks the gap
  nearest the pointer, never below the bottom commit row (a drop below the base row lands just above
  it). Alt+Up and Alt+Down move the selected row; Up, Down, Home and End move the selection.
- **Start rebase** is disabled while every row is still Pick in its first order (`Change an action
  or the order first`), and while HEAD is no longer on {b} at the tip the list was read from
  (`HEAD moved: open the list again`).

**Selection, the right panel and leaving.**

- The list opens with its top row selected and the keyboard focus on it; opening it leaves the
  working-changes row (`IsWorkingRowSelected = false`). Selecting a row selects that commit in the
  graph by SHA (D88) without closing the list, so the right panel shows its details.
- A file clicked in the right panel opens its diff over the list; closing the diff returns to the
  list. A ref clicked in the left panel moves the graph's selection behind the list and leaves it
  open.
- Esc (`EscapeOrder.InteractiveRebase` = 360): a row drag in progress is cancelled first (the row
  stays where it was); otherwise the list closes, asking first when anything was changed (7.2). Every
  other path that would show something else in the graph area (another diff from the stage panel,
  the resolver, another list) first asks the same question
  (`RepositoryViewModel.TryLeaveGraphAreaAsync`, 11.1).
- When it closes, the focus goes to the graph and the commit last selected in it stays selected
  (after Start, 1.5's rule).
- While Start's operation waits or runs, the rows and buttons are disabled.

### 2.3 Which commits are listed

The list and its target are read once when it opens (`ReadRebaseListAsync`), and Start rebases
onto the target's SHA read then (D124's rule). For "from here": `git rev-list --reverse
--topo-order <sha>..HEAD` (the line is linear, 2.1). For an onto target: the commits git itself
would put in the todo, `git rev-list --reverse --topo-order --no-merges --right-only --cherry-pick
<target>...<branch>` (commits already upstream by patch are left out), plus `git rev-list --merges
--count <target>..<branch>` for the merge check. A test (`InteractiveRebaseListParityTests`)
compares the app's list with the todo git writes for the same rebase (captured by a sequence editor
that copies the todo out and fails, which leaves nothing started, 0.2), on 2.36 and CI's git; where
they differ, git's list wins and this section is corrected.

### 2.4 What Start runs

One local operation "Interactive rebase of {b}" (`HasSeveralSteps` when it checks out first).
Inside it: refuse unless HEAD is `refs/heads/<b>` at the tip the list was read from (for the drop's
variant: unless `<A>`'s tip is unchanged), with D124's text; refuse when an operation is in
progress or files conflict; for the drop's variant, 1.3's dirty-tree check and the checkout; the
backup of the branch being rebased (1.2); then `git rebase -i <target sha>` with
`GIT_SEQUENCE_EDITOR` set for this call only (2.5). The list closes when the operation ends with
the rebase done or stopped; when git refused before anything started (a dirty tree, a hook), the
list stays open as it was and the error dialog shows git's output (`Interactive rebase failed`).

### 2.5 The todo the app writes (`RebaseTodo`, Core, pure)

Built from the rows in git's order (oldest first). Full SHAs, no subjects, no comments, LF line
ends, UTF-8 without BOM, an explicit `drop` line for every dropped commit (so that
`rebase.missingCommitsCheck` passes).

- Pick: `pick <sha>`. Drop: `drop <sha>`.
- **Every row whose message changes, and every group, has the same shape**: `label vc-<n>` /
  `pick <T>` / `fixup <sha>` for each member in history order (squash members too: the message
  comes from the file) / the guarded amend with the message file `vc-message-<n>`. This holds for a
  reword (no members; the new message), a group with a squash (the group's message) and a group of
  fixups only (the target's own message, its exact bytes from `git cat-file commit <T>` after the
  header, or its new one when reworded: a conflicting fixup would otherwise lose the target's `#`
  lines, 0.2).

The guarded amend, one line (`<n>` the group's number):

```
exec if l=$(git rev-parse -q --verify refs/rewritten/vc-<n>) && [ -n "$l" ] && [ "$(git rev-parse -q --verify HEAD^)" = "$l" ]; then git commit --amend --only --allow-empty --cleanup=whitespace --quiet --file="$(git rev-parse --git-path rebase-merge/vc-message-<n>)"; fi
```

It keeps the message exactly as written (`#` lines included, D68), the author and author date; it
amends only when the group made exactly one commit on top of its label, so a skipped or emptied pick
never rewrites another commit, and a missing label never matches (0.2). The amend runs the
`pre-commit` and `commit-msg` hooks, as git's own reword does (Q5, D68). A hook that refuses fails
the exec, and the rebase stops there (5.1: "the new message was not applied").

**Delivery.** The app writes the todo as `todo` and each message as `vc-message-<n>` (UTF-8 without
BOM, LF) into a new folder under the system temp folder (`visualcommit-rebase-<guid>`), and sets
`GIT_SEQUENCE_EDITOR` to a shell function, so that the `"$@"` git appends becomes its argument
(0.2):

```
vc() { cp '<msg 1>' "$(dirname "$1")/vc-message-1" && … && cp '<todo>' "$1"; }; vc
```

with paths in forward slashes, single-quoted for `sh`, `'` written `'\''`. The messages then live in
`.git/rebase-merge/` and go with it at the finish or abort, so a Continue from a terminal works the
same. The temp folder is deleted when git returns.

When git exits 1 with `rebase-merge/` present (it was not there before, 1.4) and no `done` file,
and stderr has `error: invalid line` or the `missingCommitsCheck` warning, the app's own todo was
refused: the app runs `git rebase --abort` and reports the failure with git's output (a fault of the
app, logged at Error).

A todo the app writes has no `update-ref` lines, so an interactive rebase or a squash does not move
other branches even when the user set `rebase.updateRefs` (Git 2.38+); the backup covers only the
rebased branch. This is the one place where the app does not follow that setting.

### 2.6 Stops during an interactive rebase

They are rebase stops (5): Continue, Skip, Abort, with the rules of 5.1 for a stop inside a group
the app wrote (its message file, the target of a group, the app's own amend failing). A commit that
becomes empty stops an interactive rebase (0.1, with a `CHERRY_PICK_HEAD` next to `rebase-merge/`):
the banner says so and Continue leaves it out.

## 3. Squash several selected commits

- **Menu**: `ForCommits`, first item of group 1 (the requirements' order: "Squash, compare,
  cherry-pick several"): `Squash {N} commits…`.
- **Enabled when** the selected commits are a contiguous first-parent chain on HEAD's linear line
  (every commit's first parent is the next older selected one, 2.1's line), none is a merge, the
  oldest is not the root, HEAD is on a branch, nothing is in progress and no file conflicts.
  Reasons, in order: any merge → `Merge commits cannot be squashed`; not all on HEAD → `Only
  commits of {cur} can be squashed`; not a chain → `Select commits that follow one another`; not on
  the linear line → `A merge lies between these commits and {cur}`; the root among them → `The
  first commit cannot be squashed`; detached → `Check out a branch to squash`; in progress →
  `Finish or abort the {kind} first`; conflicts → `Resolve the conflicts first`.
- **The dialog**: the message dialog (7.2) titled `Squash {N} commits`, filled with git's squash
  message (the commits' messages oldest first, joined by a blank line), button `Squash`.
- **What runs**: one local operation "Squash {N} commits": the HEAD check (D124), the in-progress
  refusal, the backup (1.2), then an interactive rebase from the oldest selected commit's parent
  with the todo of 2.5: the selected commits as one group with the dialog's message, every later
  commit `pick`.
- **Result**: `Squashed {N} commits into {short}` (the new commit), which is selected.

## 4. Several picks and reverts with git's list (D136)

`GitRepository.CherryPickAsync(IReadOnlyList<string> commitsOldestFirst, …)` runs `git cherry-pick
<A> <B> <C>` (oldest first by row, as phase 3 orders them) and `RevertAsync(IReadOnlyList<string>
commitsNewestFirst, …)` runs `git revert --no-edit <C> <B> <A>`. One commit keeps phase 3's single
call. A multi-revert with a merge stays disabled (`Revert merge commits one at a time`). Both return
`SequenceResult(SequenceStop Stop, int Applied, int Total)`, `SequenceStop` being `None` (all
applied), `Conflicts`, `Empty` or `Refused` (stopped on a commit git could not apply without a
conflict).

How a list ended (read right after the call; "pick head" is `CHERRY_PICK_HEAD` or `REVERT_HEAD`,
read as in 8.1):

| After the call | Result |
|---|---|
| exit 0 | `None` |
| a pick head, conflicted files | `Conflicts` (stopped): the banner |
| a pick head, no conflict, the index equal to HEAD | `Empty` (stopped): the banner offers Skip |
| a pick head, no conflict, the index not equal to HEAD (rerere's `autoUpdate`, a refusing hook: D128) | `Conflicts` with 0 files (stopped) |
| no pick head, `sequencer/` present, `MERGE_MSG` present (an empty revert, also the first of the list) | `Empty` (stopped): Skip and Continue both go on |
| no pick head, `sequencer/` present, no `MERGE_MSG`, exit 128, HEAD = `sequencer/head` | refused at the start: the app runs `git cherry-pick --quit` (`git revert --quit`), which removes `sequencer/` and keeps the user's changes (0.3), and fails with git's output and 10.1's text |
| no pick head, `sequencer/` present, no `MERGE_MSG`, HEAD ≠ `sequencer/head` | `Refused` (stopped): a later commit could not be applied (an untracked file in the way); the banner offers Skip and Abort, not Continue (5.1), and the error dialog shows git's output (5.1) |
| none of these, non-zero | a `GitException` |

`Applied` is the list's length less the commands left in `sequencer/todo` (the stopped command is
still its first line, 0.3); `Total` the list's length. The status bar says `Cherry-picked {K} of
{N} commits; stopped at {short} with conflicts in {M} files` (10.2), and the rest waits in git's
list (phase 3's "not picked" wording goes: the banner names what waits).

## 5. Continue, Skip and Abort

### 5.1 What each state offers

The state is `InProgressState` as phase 3 reads it, with section 8's additions. "Conflicts" is the
number of conflicted files of the status; "staged" means the index differs from HEAD.

| State | Continue | Skip | Abort |
|---|---|---|---|
| Merge | enabled with 0 conflicts: commits the merge (5.4) | none (git has no skip for a merge) | phase 3's |
| A single cherry-pick or revert (no `sequencer/`), not empty | enabled with 0 conflicts: commits it (5.4) | `git cherry-pick --skip` (`revert`): ends it, HEAD unchanged | phase 3's |
| A pick or revert in a list (a pick head and `sequencer/`), not empty | enabled with 0 conflicts: commits it and goes on with the list (5.4) | `--skip` | phase 3's: the whole list is undone |
| `Empty`: a pick or revert (single or in a list) that changes nothing, or an empty revert in a list (no pick head, `MERGE_MSG` present) | disabled: `This cherry-pick changes nothing: skip it` (`This revert changes nothing: skip it`) | enabled | phase 3's |
| A list paused between commits, HEAD moved since git's last pick (`AbortKeepsHead`, D110: the stopped pick was committed in a terminal) | enabled: `--continue` picks the rest | disabled: `There is nothing to skip: the stopped commit was committed` | phase 3's (keeps HEAD) |
| A list stopped on a commit git could not apply without a conflict (`sequencer/`, no pick head, no `MERGE_MSG`, HEAD where git left it) | disabled: `Git would leave {short} out without a word: skip it or abort` | enabled | phase 3's |
| Rebase, stopped on a pick or reword (`StopKind` Pick) | enabled with 0 conflicts (5.4) | enabled: `git rebase --skip` | phase 3's (and 1.3's return to the branch the user was on) |
| Rebase, the stopped commit is the target of a group (the first line of `git-rebase-todo` is `fixup` or `squash`) | as a pick, but disabled while nothing is staged and nothing conflicts: `{short} changes nothing now, and git would join the commits squashed into it to the commit before it: abort, or change it` | disabled: `Skipping {short} would join the commits squashed into it to the commit before it: abort, or resolve it and continue` | phase 3's |
| Rebase, `Empty` (an interactive rebase's commit that became empty: `CHERRY_PICK_HEAD` beside the folder) | enabled: leaves it out | enabled | phase 3's |
| Rebase, `Rescheduled` (an untracked file was in the way) | enabled: `git rebase --continue` tries it again | disabled: `Skip would try {short} again: move or remove what is in the way, or abort` | phase 3's |
| Rebase, `OwnAmendFailed` (the app's guarded amend failed: a hook refused the new message) | enabled: the app runs the amend again, then continues (5.4) | enabled: goes on without the new message | phase 3's |
| Rebase, `ExecFailed` (another `exec` line failed) | enabled: `git rebase --continue` | disabled: `There is nothing to skip: continue to go on` | phase 3's |
| Rebase, `Edit` (an `edit` line of a todo from elsewhere) | enabled: `git rebase --continue` (git amends what is staged) | disabled: `Skip does not leave out a commit that is being edited: continue or abort` | phase 3's |
| `git am` | none: the banner keeps phase 3's "continue in a terminal" | none | phase 3's |
| Conflicts without an operation (a stash that did not apply cleanly) | none | none | none (phase 3) |

- **Skip** loses the resolution of the stopped commit (0.1, 0.3), so it asks first (7.2) and saves a
  backup like Abort's: `skip-<time>` (HEAD) and `skip-<time>-changes`
  (`SnapshotTrackedChangesAsync`). This amends D83's "Nothing else confirms": Skip joins Abort
  (D144). Continue asks nothing: it only adds history.
- **Markers.** Before it runs, Continue (from the banner or the stage panel) runs `git diff
  --cached --check -- <the paths that were conflicted at the stop>`; when it reports `leftover
  conflict marker`, it asks first (7.2).
- **A stop with nothing conflicted that is not "empty"** (`Rescheduled`, `ExecFailed`,
  `OwnAmendFailed`, a list `Refused`) gives the user no file to look at, so when the app's own
  operation ends in such a stop, the error dialog opens with git's output (titled `Rebase stopped`,
  `Cherry-pick stopped` or `Revert stopped`; summary the banner's text). This amends D95 (a stop is
  not a failure and opens no dialog) for these stops only.

### 5.2 The banner

`ConflictBanner` gains `BannerContinueButton` and `BannerSkipButton` left of `BannerAbortButton`, in
the same style (`VcActionButton`, 26 high), shown as 5.1 says, a disabled one with its reason as its
tooltip (`ToolTip.ShowOnDisabled`). `ConflictBannerViewModel(abort, continue, skip)` gains
`HasContinue`, `CanContinue`, `ContinueToolTip`, `ContinueCommand` and the same for Skip; `Show(state,
conflicts, ontoName)` takes the name of a branch that points at a rebase's `onto` (from the refs: a
local branch if one does, else a remote branch; null when none does, 14.3).

**Layout** (amends D122): the buttons stand right of the text, 6 apart and 8 from the right edge,
while the text column keeps at least 240 px; otherwise they go on a row of their own under the text,
right-aligned, 26 high, with 8 above and below it. The banner is at least 36 high, and exactly 36
when its text and buttons fit on one line. Checks state "one line" or "more", and whether the
buttons are inline or below, never a measured height.

**Texts.** Phase 3's texts stay where they are still true; the changes are marked "new":

| State | Text |
|---|---|
| Merge | phase 3's: `Merging: "{subject}".` (or `Merging {short}.`, `Merging.`) and ` {N} files have conflicts.` / ` Conflicts resolved: commit to finish the merge.` |
| Single pick (revert) | phase 3's: `Cherry-picking {short} "{subject}". {N} files have conflicts.` / `… Conflicts resolved: commit to finish the cherry-pick.` (`Reverting …`, `… the revert.`) |
| A pick in a list (new) | `Cherry-picking {short} "{subject}". {N} files have conflicts.` / `… Conflicts resolved: continue to commit it.`, then ` 1 more commit waits: Continue picks it.` or ` {M} more commits wait: Continue picks them.` (nothing when none waits); revert: `Reverting …`, `Continue reverts it` / `them` |
| Empty (new wording) | `Cherry-picking {short} "{subject}" changes nothing: its changes are already here. Skip it to go on.` / `Reverting {short} "{subject}" changes nothing: it is already undone. Skip it to go on.`, then the list's sentence |
| A list paused (new) | `Cherry-pick paused: {M} more commits wait. Continue picks them.` (`1 more commit waits. Continue picks it.`; with none: `Cherry-pick paused with no commits left: continue to end it.`); revert likewise with `Revert`, `reverts` |
| A list stopped on a refused commit (new) | `Cherry-pick stopped at {short} "{subject}": git could not pick it. Skip it, or abort.` / `Revert stopped at {short} "{subject}": git could not revert it. Skip it, or abort.` |
| Rebase (new) | `Rebasing {b} onto {onto}: stopped at {short} "{subject}" ({k} of {n}).` then ` {N} files have conflicts.` / ` Conflicts resolved: continue to commit it and go on.` / with nothing staged and nothing conflicted: ` It changes nothing now: continue to leave it out.` (for a group's target: ` It changes nothing now: abort, or change it.`); without a branch (a detached rebase from a terminal): `Rebasing onto {onto}: …` |
| Rebase, rescheduled (new) | `Rebasing {b} onto {onto}: git could not apply {short} "{subject}" ({k} of {n}): an untracked file is in the way. Move or remove it, then continue.` |
| Rebase, the app's amend failed (new) | `Rebasing {b} onto {onto}: the new message of {short} was not applied ({k} of {n}). Continue tries again; Skip goes on without it.` |
| Rebase, another exec failed (new) | `Rebasing {b} onto {onto}: a command failed after {short} ({k} of {n}). Continue goes on.` |
| Rebase, edit (new) | `Rebasing {b} onto {onto}: stopped to edit {short} "{subject}" ({k} of {n}). Continue when ready.` |
| `git am`, conflicts without an operation | phase 3's |

`{onto}` is a local branch that points at `onto` when one does, else a remote branch that does,
else its short SHA (14.3); `{k} of {n}`
counts the commands that apply a commit (8). The icon's tooltip is in 10.3.

### 5.3 The stage panel during a stop

- **Merge, single pick or revert**: unchanged from phase 3 ("Commit", "Commit merge"); committing
  concludes it, exactly as Continue does.
- **A list or a rebase**: the commit button reads `Continue cherry-pick`, `Continue revert` or
  `Continue rebase` and runs the banner's Continue with the panel's message; enabled as Continue
  is, with the same reason as its tooltip. The summary may be empty: the operation's own message is
  used then. This is how D136's "the Commit button does not finish a pick that belongs to a list;
  Continue does" is built. Amend is disabled during a list or a rebase stop (`Finish or abort the
  {kind} first`).
- **The message box is filled** once per stopped commit (`IWorkingChangesHost.ReadOperationMessageAsync`):
  - a merge, pick or revert: `MERGE_MSG` with git's trailing comment block removed (from a line
    `{c} Conflicts:` to the end, when every later line starts with git's comment character `{c}`,
    `core.commentChar`, `#` by default). The commit's own `#` lines stay (D68). Phase 3 removed every
    `#` line; this changes that fill.
  - a rebase: `rebase-merge/message`, with no line removed; inside a group the app wrote (the group's
    `exec … vc-message-<n>` line still waits in `git-rebase-todo` before the next `label`), the
    group's message `rebase-merge/vc-message-<n>` instead.
  - a `fixup` or `squash` stop of a rebase started elsewhere: `rebase-merge/message` less git's
    comment lines (Continue then runs `git rebase --continue`, which keeps git's message; no
    tooltip says so: the state does not tell that stop apart, 14.2).
  - The first line is the summary, the rest the description (phase 3's `SplitMessage` without its
    `#` filter). Continue passes no message when the boxes still hold what was filled, so that the
    git layer reads the source itself.
- Conflicted rows keep the phase 3 look; their tooltip becomes `Conflict: click to resolve it`.
  The empty pick's commit tooltip becomes `This cherry-pick changes nothing: skip it` (`revert`).

### 5.4 What Continue and Skip run (`GitRepository.ContinueAsync`, `SkipAsync`)

`ContinueAsync(InProgressState state, string? message, NetworkOptions?, CancellationToken)` returns
`ContinueResult(ContinueOutcome Outcome, string? NewTip)` (`Done` or `Stopped`); a refusal throws.
`message` is the stage panel's (null: use the source of 5.3).

| State | Runs |
|---|---|
| Merge | `git commit --file=- --cleanup=whitespace` with the message (phase 3's commit) |
| Single pick or revert | the same commit (git takes the author from the pick head) |
| Pick or revert in a list, pick head present | the same commit, then `git cherry-pick --continue` (`revert`): git then leaves the committed line out (D110, 0.3) |
| List paused, `AbortKeepsHead` | `git cherry-pick --continue` (`revert`) |
| Rebase stopped on a `pick` or `reword` with something staged | inside a group the app wrote: the message (when given) is first written to `rebase-merge/vc-message-<n>` (UTF-8 without BOM), so that the group's amend applies the user's edit; then `git commit --no-verify --cleanup=whitespace --file=-` with the message (or the source of 5.3) and `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL`, `GIT_AUTHOR_DATE` from `rebase-merge/author-script` (0.1: git's own Continue runs no hook there either), then `git rebase --continue` |
| Rebase, `OwnAmendFailed` | when `HEAD^` equals `refs/rewritten/vc-<n>`: `git commit --amend --only --allow-empty --cleanup=whitespace --file=<rebase-merge/vc-message-n>` (hooks run; a refusal fails Continue with the hook's output and the rebase stays stopped), then `git rebase --continue` |
| Rebase otherwise (`Rescheduled`, `ExecFailed`, `Edit`, `Empty`, nothing staged, a `fixup`/`squash` stop) | `git rebase --continue` |

`SkipAsync(InProgressState state, …)`: `git cherry-pick --skip`, `git revert --skip` or `git rebase
--skip`; for `OwnAmendFailed`: `git rebase --continue` (on without the new message). The question
and the `skip` backup come before, in the app (the banner's Skip, like Abort's question, is asked by
`RepositoryViewModel`).

In the app, `IInProgressActions.ContinueAsync(state, message)` and `SkipAsync(state)` run them as
one local operation each, "Continue the {kind}" and "Skip {short}" (`RefreshScope.Refs`, `Ask`).
Before it runs, each refuses when the state changed since the click (a fresh status read: another
kind, another stopped commit, conflicts back). It ends `Done` (10.2), `Conflicts` at the next stop
(the banner), or failed with git's output ("Continue failed", "Skip failed").

## 6. Pull with rebase, chosen for one pull

- **Menu**: the current branch's menu (`ForLocalBranch` on HEAD's branch, in the left panel and on
  its graph label), right after `Pull from {up}`: `Pull from {up} (rebase)`, enabled exactly as the
  toolbar's Pull (`AsToolbar`, D90). Not on the toolbar (D90: no dropdowns).
- **What runs**: phase 3's pull steps (fetch, `EndCancelWindow`, the HEAD checks) with the
  integration forced to rebase: `IntegratePullAsync(branch, PullIntegration.Rebase, beforeRebase,
  …)`. When the branch has nothing of its own: `git merge --ff-only <head>` (as now); otherwise
  `beforeRebase(oldTip)` (the app saves the backup of 1.2) and `git rebase` **without arguments**,
  which rebases onto the upstream from its fork point, as `git pull --rebase` does (0.1: `git rebase
  FETCH_HEAD` would replay commits the upstream dropped by a force push). `pull.ff`, `pull.rebase`
  and `branch.<b>.rebase` are not read (git's command line `--rebase` ignores them too, also an
  invalid value). Title `Pull (rebase)`; dedupe key `pull:<full branch ref>:rebase` (never merged
  with a plain Pull's `pull:<ref>`; auto-fetch counts it as a fetch, as it counts a Pull); a local
  upstream skips the fetch, as Pull does.
- **The configured pull** (phase 3, D84) passes `PullIntegration.AsConfigured` and the same
  `beforeRebase`, so it saves the same backup before a rebase.
- **The fork point**: before the rebase the app counts `git rev-list --count --no-merges
  --right-only --cherry-pick <upstream>...HEAD`, after it `git rev-list --count --no-merges
  <upstream>..HEAD`; when the second is lower, git left out commits the upstream once had (0.1), and
  the status bar says so (warning, 10.2). The behaviour stays git's (Q5).
- **Stops**: phase 3 took a pull's rebase as stopped only when unmerged paths appeared; a rebase can
  stop with none (0.1). Both pulls now classify with `ClassifyRebaseAsync` (1.4), and the outcome
  `PullOutcome.Conflicts` stays for every stop. Continue and Skip after a pull's rebase are those of
  any rebase (5).
- **Parity**: `PullRebaseParityTests` (a tenth class beside phase 3's nine) compares the chosen
  rebase pull with `git pull --rebase` on copies of the same template, for no settings,
  `pull.ff=only`, `pull.rebase=false`, `pull.rebase=merges` with a local merge, an upstream
  rewritten by a force push, and the fork-point case, on Git 2.36 and CI's git.

## 7. UI inventory: menus, questions, dialogs, colours, automation ids

### 7.1 The menus as phase 4 changes them

Notation as in phase 3's report: header first, "—" a separator, "…" an item that opens a dialog or
a list (an item that asks only sometimes has none, as phase 3's "Check out {b}"). Only the groups
that change are shown; the rest is phase 3's.

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
| A conflicted file in the unstaged list | [**Resolve conflict**, **Open in merge tool** (text and binary conflicts only; disabled without `merge.tool`, 11.9), Mark as resolved, Discard changes…] — phase 3's other groups |
| An interactive list's row | header `{short} {subject}`, then the actions of 2.2 |
| The resolver (11.12) | its header: the conflicted file's menu; the result: [Undo, Redo] — [Cut, Copy, Paste, Select all]; the ours and theirs panes: [Copy] |

Staging a conflicted file whose working-tree file still has conflict markers asks first (7.2),
wherever it is staged from: "Mark as resolved", the row's stage button, "Stage all", the resolver's
Mark resolved. The new disabled reasons are in 10.4.

### 7.2 Questions and dialogs

`ConfirmationRequest` gains `CancelText` (default `Cancel`), bound in `ConfirmationView`.

| Dialog | Title / question / detail / buttons (the confirm button's style) |
|---|---|
| Skip | `Skip {short}?` / `"{subject}" is left out of the {rebase, cherry-pick, revert}, and changes made to resolve its conflicts are lost.` / `A backup is saved first, so this can be undone.` / `Skip` (danger), `Cancel` |
| Leaving a changed interactive list | `Discard the changes to this list?` / — / — / `Discard` (danger), `Keep editing` |
| Staging or marking resolved with markers left | `{file} still has conflict markers.` (several: `{N} files still have conflict markers.`) / `Mark it resolved anyway? The markers are committed with it.` / — / `Mark resolved` (primary), `Cancel` |
| Continue (or the stage panel's commit that concludes an operation) with markers staged | `{N} files still have conflict markers.` / `Continue anyway? The markers are committed with them.` (one file: `1 file still has conflict markers.` / `Continue anyway? The markers are committed with it.`) / — / `Continue` (primary), `Cancel` |
| Leaving the resolver with a modified result (11.1) | `Discard your changes to {file}?` / `The result has not been marked resolved.` / — / `Discard` (danger), `Keep editing` |
| The merge tool with a modified result | `Discard your changes to {file}?` / `The merge tool starts from the file as it is on disk.` / — / `Discard` (danger), `Keep editing` (the tool does not start) |
| Mark resolved over a file changed on disk | `{file} changed on disk.` / `Overwrite it with the result?` / — / `Overwrite` (danger), `Cancel` |
| The message dialog (`MessageDialogViewModel` : `DialogViewModel<string?>` / `MessageDialogView`, `MessageDialog`) | the `wide` card (460); title (`MessageDialogTitle`) `Reword {short}`, `Message of the squashed commit` or `Squash {N} commits`; `MessageSummary` 32 high with `MessageSummaryCounter` (what is left of 72, as the stage panel's); `MessageDescription` 120 high, wrapping, accepting Return; the focus in the summary with the caret at its end; Enter in the summary confirms (`DialogView`'s rule); `Save` or `Squash` (`ConfirmButton`, primary; disabled while the summary is blank: `Write a summary`), `Cancel`. The message is split as 5.3 says and joined as `{summary}\n\n{description}` (the summary alone when the description is empty) |

Every question is the app's own dialog on the window's dialog layer (D69, D104); Esc cancels.

### 7.3 New colours and icons

| Token | Dark | Light | Used for |
|---|---|---|---|
| `VcConflictOursBrush` | `#15323A` | `#DCF1F5` | The ours side of a conflict block: its rows in the ours pane and its section of the result |
| `VcConflictTheirsBrush` | `#2C2140` | `#F0E6FA` | The theirs side, likewise |
| `VcConflictBaseBrush` | `#24272F` | `#EEF0F3` | A `diff3` base section in the result |
| `VcConflictMarkerBrush` | `#3A2E16` | `#FCEFD2` | The marker lines of an unresolved block in the result (their text in `VcWarningBrush`) |
| `VcConflictUsedBrush` | `#1A2E24` | `#E6F6EC` | In the ours and theirs panes, a block's side once it is used in the result (the colours of `VcDiffAddedBrush`); the result tints no chosen line |

Icons (Lucide, added to `Icons.axaml` and `THIRD-PARTY-NOTICES.md`): `IconGrip` (grip-vertical),
`IconChevronLeft`, `IconChevronUp` (Right and Down exist).

### 7.4 Automation ids

| Where | Ids |
|---|---|
| Banner | `BannerContinueButton`, `BannerSkipButton` (beside phase 3's) |
| Interactive list | `InteractiveRebaseView`, `RebaseListHeader`, `RebaseListCount`, `RebaseRow` (named `{short} {subject}`, top to bottom), `RebaseActionButton`, `RebaseRowSecondLine`, `RebaseEditMessageButton`, `RebaseBaseRow`, `RebaseListMessage`, `RebaseCancelButton`, `RebaseStartButton` |
| Resolver header and toolbar | `ConflictResolver` (its root, a control element), `ResolverFileStatus`, `ResolverFileName`, `ResolverFolder`, `ResolverKind`, `ResolverFilePosition`, `ResolverPreviousFile`, `ResolverNextFile`, `ResolverCloseButton`, `ResolverConflictPosition`, `ResolverPreviousConflict`, `ResolverNextConflict`, `ResolverTakeOurs`, `ResolverTakeTheirs`, `ResolverMergeTool`, `ResolverMarkResolved` |
| Panes and result | `ConflictOurs`, `ConflictTheirs`, `ConflictResult`, `ConflictOursTitle`, `ConflictTheirsTitle`, `ConflictResultTitle`, `ConflictResultCount` |
| Block bars (only for bars in view) | ours pane: `ConflictUseOurs`, `ConflictUseBothOursFirst`; theirs pane: `ConflictUseTheirs`, `ConflictUseBothTheirsFirst`; `ConflictBarState` (the `Used in the result` / `Not used` text) |
| The card (11.8) | `ConflictCard`, `ConflictCardText`, `ConflictKeepFile`, `ConflictDeleteFile`, `ConflictUseOursFile`, `ConflictUseTheirsFile`, `ConflictCardMarkResolved` |
| Message dialog | `MessageDialog`, `MessageDialogTitle`, `MessageSummary`, `MessageSummaryCounter`, `MessageDescription` |

A pane's and the result's automation ids are set by their owner (the diff's `DiffText*` ids stay
the diff's). The dialogs keep the shared `CancelButton` and `ConfirmButton` ids; a harness finds them
in the top dialog first (phase 3's rule).

## 8. The in-progress state and reftable (D128)

`InProgressState` gains init properties with defaults, so that phase 3's constructions keep
compiling (its record equality changes what `InProgressTests` compare: those tests are updated):

- `Sequenced` (a `sequencer/` folder; replaces the two `HasSequencer()` copies in the app) and
  `HasPickHead`. A list is paused between commits when `Sequenced && !HasPickHead`; the stage panel
  fills no message then.
- `Commit` and `Subject`: for a rebase from `rebase-merge/stopped-sha` and its subject; for a paused
  list from `sequencer/todo`'s first line (its abbreviated SHA resolved with `rev-parse --verify`).
- For a rebase: `StopKind` (`Pick`, `Rescheduled`, `ExecFailed`, `OwnAmendFailed`, `Edit`, `Empty`),
  `NextIsSquash` (the first line of `git-rebase-todo` is `fixup`, `f`, `squash` or `s`), `Group`
  (the number `<n>` when the stop is inside a group the app wrote, 5.3), and `Done` and `Total`:
  the commands that apply a commit (`pick`, `reword`, `edit`, `squash`, `fixup`, `merge` and their
  letters) in `done` and in `git-rebase-todo`, a rescheduled pick counted as the current one.
  `Remaining` counts the same commands, so `label` and `exec` lines do not count.
- The rebase's stop is read in this order, `rebase-merge/` (or `rebase-apply/`) being present
  (also after an exit 0): `Rescheduled` when the first line of `git-rebase-todo` is pick-like and
  names `stopped-sha`; `ExecFailed` when `done` ends with an `exec` line and there is no
  `stopped-sha` (`OwnAmendFailed` when that line names `vc-message-`); `Edit` when `amend` exists
  and `done` ends with `edit`; `Empty` when `CHERRY_PICK_HEAD` sits beside the folder, or `done`
  ends with a pick-like line for `stopped-sha` with nothing staged and nothing conflicted; `Pick`
  otherwise.
- A rebase's `onto` stays a SHA in the state; the banner gets a branch name for it from the refs.

### 8.1 Pick heads under both ref formats

`GitRepository.ReadPseudoRefAsync(name)` is the one place that reads `CHERRY_PICK_HEAD` and
`REVERT_HEAD`: first the file `<git folder>/<name>`, as phase 3 does; when it is missing and the
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

- **A backup** is one operation's refs. The part of a ref's name after `refs/visualcommit/backup/`
  is read with `^(?<kind>[a-z]+(?:-[a-z]+)*)-(?<time>\d{8}T\d{9}Z)(?:-(?<n>\d+))?(?<rest>-changes|/.+)?$`;
  the refs that share `kind`, `time` and `n` are one backup (D83's scheme: the ref itself, its
  `-changes` ref, its `/<name>` refs; D67's `discard-<time>[-<n>]`; phase 4's `rebase` and `skip`).
  Its time is the one in its name (UTC). A ref under the prefix that fits no scheme is never
  deleted and never counted. A time later than now counts as now.
- **The rule** (`BackupRetention(Days: 30, Count: 50)`, Core): with `now` from the repository's
  backup clock, every backup older than 30 days goes; then, while more than 50 remain, the oldest
  goes. Nothing goes while an operation is in progress in any worktree of the repository (the main
  git folder and every `<common git folder>/worktrees/*/`: `rebase-merge/`, `rebase-apply/`,
  `MERGE_HEAD`, `sequencer/`, a pick head): the whole clean-up waits for another time, so the backup
  of an operation still in progress is never deleted. The restore bar's snapshot (D62), passed in as
  `keep`, never goes.
- **How**: one `git for-each-ref --format=%(refname) %(objectname) refs/visualcommit/backup/`, then
  one `git update-ref --stdin` transaction of `delete <ref> <old value>` lines: a ref changed since
  the read fails the whole transaction, and nothing is deleted (logged).
- **When**: (a) at the end of every operation that saved a backup, inside it, after its work: a
  failure there is logged at Warning and never fails the operation; (b) when a repository has opened
  and its first load is done (`RepositoryViewModel.OnFirstLoadCompleted`), as a background operation
  of its queue (`Clean up backups`, `IsBackground`, prompts `None`, no refresh), started with
  `TryStartInBackground`: if the queue is busy at that moment it is not run; the next backup or
  opening does it. Its `Run` catches every exception, logs it at Warning and returns
  `OperationReport.Done("")`: it never ends failed or cancelled. The status bar's auto-fetch texts
  (`Auto-fetch failed`, `Waiting for auto-fetch…`) are shown only for auto-fetch's own operation
  (by its title); while another background operation runs the status bar keeps its text.
- **Discard snapshots** are named from the backup clock too (they used the system clock), so that a
  fixed test clock orders them with the other backups.
- **What it says**: nothing on screen. The log gets one Info line per backup removed,
  `<repo>: Removed backup <kind>-<time>[-<n>] (older than 30 days)` or `(more than 50 backups)`, and
  a Debug line when an operation in progress made it wait. Phase 5's activity log is to show these
  lines (12.2).
- `GitRepository.CleanUpBackupsAsync(BackupRetention, IReadOnlySet<string> keep, CancellationToken)`
  returns what it removed (`RemovedBackup(Name, Refs, Reason)`). `FakeRepositoryBase` returns an
  empty list. The Git tests use a fixed clock (`BackupClock`); the App tests name refs as old with
  `TempRepo` and use the real clock. Phase 5's undo offers only backups that exist (D137).

## 10. Texts

`OperationTexts` gets the parts `OperationTexts.Rebase.cs` and `.Conflicts.cs`. `{target}` is a
branch's short name, `{r}/{b}`, or a commit's short SHA; `{b}` is the branch being rebased (the
drop's A); `{kind}` is phase 3's `InProgressName` (merge, cherry-pick, revert, rebase).

### 10.1 Operations: title, running text, error dialog title, failure summary

| Operation | Title | Running text | Error title | Failure summary |
|---|---|---|---|---|
| Rebase onto | `Rebase {b} onto {target}` | `Rebasing {b} onto {target}…` | `Rebase failed` | `Rebase {b} onto {target} failed.` |
| Check out and rebase | `Check out {A} and rebase it onto {B}` | `Checking out {A}…`, then `Rebasing {A} onto {B}…` | `Checkout failed` or `Rebase failed` (the step that failed) | phase 3's checkout summary, or the rebase's |
| Interactive rebase | `Interactive rebase of {b}` | `Rebasing {b}…` (with the checkout: `Checking out {A}…` first) | `Interactive rebase failed` (`Checkout failed`) | `Interactive rebase of {b} failed.` |
| Squash | `Squash {N} commits` | `Squashing {N} commits…` | `Squash failed` | `Squash {N} commits failed.` |
| Pull with rebase | `Pull (rebase)` | phase 3's pull texts | `Pull failed` | phase 3's |
| Continue | `Continue the {kind}` | `Continuing the {kind}…` | `Continue failed` | `Git could not continue the {kind}.` |
| Skip | `Skip {short}` | `Skipping {short}…` | `Skip failed` | `Git could not skip {short}.` |
| Mark resolved, take a side | `Resolve {file}` | `Resolving {file}…` | `Resolving failed` | `{file} could not be marked resolved.` |
| Merge tool | `Merge tool for {file}` | `Waiting for {tool} to close…` | `Merge tool failed` | `{tool} did not resolve {file}.` |
| Backups' clean-up | `Clean up backups` | (background: never shown) | (never shown) | (logged only) |

Special summaries (in place of the default, git's output below, as phase 3's): a rebase refused for
local changes → `Git rebases only a clean working tree: commit or stash your changes first.`; for an
untracked file in the way → `Untracked files are in the way: move or remove them first.`; a list
refused at its start → `Git would overwrite your changes: commit or stash them first. Nothing was
cherry-picked.` (`… Nothing was reverted.`); the refusals D124 adds, as their own summaries:
`HEAD moved while this waited: it was on {a} and is on {b} now. Nothing was rebased.`, `{b} changed
while the list was open: open it again. Nothing was rebased.`

### 10.2 Results in the status bar

| Outcome | Text (tone) |
|---|---|
| Rebased | `Rebased {b} onto {target}` |
| Up to date | `{b} is already up to date with {target}` |
| Rebased, the put-back changes conflicted | `Rebased {b} onto {target}; your changes conflicted when put back: they are also kept in the stash` (warning) |
| Interactive rebase done | `Rebased {b}: {N} commits` (the commits on the branch after its base) |
| Squash done | `Squashed {N} commits into {short}` |
| A rebase stopped | `Rebase stopped: {N} files have conflicts` (warning); with none `Rebase stopped at {short} "{subject}"` (warning) |
| A list stopped | `Cherry-picked {K} of {N} commits; stopped at {short} with conflicts in {M} files` (warning); on an empty pick `Cherry-picked {K} of {N} commits; {short} changes nothing` (warning); on a refused commit `Cherry-picked {K} of {N} commits; git could not pick {short}` (warning); `Reverted …` likewise |
| Continue done | `Merge committed`, `Cherry-pick finished`, `Revert finished`, `Rebased {b} onto {onto}`; at a new stop as the stops above |
| Skip done | at a new stop, that stop's text; when the operation ends, Continue's done text (`Rebased {b} onto {onto}`, `Cherry-pick finished`, `Revert finished`); `Skipped {short}` only when a single pick or revert was skipped (nothing follows) |
| A pull with rebase | phase 3's `Pulled and rebased {b} onto {up}`; when the fork point left commits out: `Pulled and rebased {b} onto {up}; git left out {k} commits that {up} once had` (warning) |
| Resolved | `Resolved {file}` |
| Merge tool | `Resolved {file} in {tool}`; `{tool} closed without resolving {file}` (secondary) |

### 10.3 The banner's icon tooltip

| State | Tooltip |
|---|---|
| Merge | `Continue commits the merge with the message in the stage panel. Abort puts {cur} back where it was before the merge.` |
| A single pick or revert | `Continue commits the resolved files with the message in the stage panel. Skip leaves this commit out. Abort puts {cur} back where it was before the {kind}.` |
| A pick or revert in a list | `Continue commits the resolved files and goes on with the commits that wait. Skip leaves this commit out and goes on. Abort puts {cur} back where it was before the {kind}: the commits it already made are dropped too.` |
| A list paused (`AbortKeepsHead`) | `Continue picks the commits that wait. Abort ends the {kind} and keeps {cur} and the commits made so far.` |
| Rebase | `Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts {b} back where it was before the rebase.` |
| `git am` | `Resolve the files and stage them, then continue in a terminal (git am --continue), or abort.` |
| Conflicts without an operation | `Click a conflicted file to resolve it, then mark it resolved.` |

### 10.4 New disabled reasons

| Reason | Where |
|---|---|
| `Check out a branch to rebase` | rebase and interactive items while detached |
| `Check out a branch to squash` | squash while detached |
| `Resolve the conflicts first` | rebase, interactive and squash items while files conflict without an operation (D119's text) |
| `{cur} has no commits after this one` | "from here" on HEAD's own commit |
| `This commit is not on {cur}` | "from here" on a commit HEAD does not reach |
| `A merge lies between this commit and {cur}` | "from here" off HEAD's linear line |
| `Merge commits cannot be squashed`, `Only commits of {cur} can be squashed`, `Select commits that follow one another`, `A merge lies between these commits and {cur}`, `The first commit cannot be squashed` | squash |
| `Resolve the conflicts first ({N} files)` | Continue, the stage panel's Continue |
| `This cherry-pick changes nothing: skip it` / `This revert changes nothing: skip it` | Continue |
| `There is nothing to skip: the stopped commit was committed` | Skip (a paused list) |
| `Git would leave {short} out without a word: skip it or abort` | Continue (a list stopped on a refused commit) |
| `{short} changes nothing now, and git would join the commits squashed into it to the commit before it: abort, or change it` | Continue (a group's target) |
| `Skipping {short} would join the commits squashed into it to the commit before it: abort, or resolve it and continue` | Skip (a group's target) |
| `Skip would try {short} again: move or remove what is in the way, or abort` | Skip (rescheduled) |
| `There is nothing to skip: continue to go on` | Skip (another exec failed) |
| `Skip does not leave out a commit that is being edited: continue or abort` | Skip (edit) |
| `The oldest commit has nothing below it to join` | squash and fixup in the list's bottom row |
| `Change an action or the order first` | Start rebase |
| `HEAD moved: open the list again` | Start rebase |
| `Write a summary` | the message dialog's confirm button |
| `Set merge.tool in git's configuration to use a merge tool` | the resolver's Merge tool button, the file menu's Open in merge tool (11.9) |
| `This side deleted the file` | the resolver's Take ours or Take theirs for a side the conflict lacks (11.6) |

The list's own messages (2.1: merges in the range, nothing of its own, too many commits) are shown
in the list, not as reasons.

## 11. The conflict resolver

### 11.1 Opening, leaving and closing

- A click (or double-click) on a conflicted file in the stage panel's unstaged list opens the
  resolver for that file, where phase 2 opened the "This file has a merge conflict" body (D63's
  gesture, amended: D148); so does the file menu's `Resolve conflict`. `OpenDiff` of a conflicted
  file opens the resolver. The diff view's `Conflict` body goes. For 500 ms after it opens, the
  resolver ignores a pointer press whose click count is 2 or more (handled in the tunnel phase at
  its root), so that a double-click's second press does not land on the resolver's buttons.
- `ConflictResolverViewModel` belongs to the `RepositoryViewModel` (`Resolver`,
  `IsResolverOpen`), like the diff; its document, undo stack, caret and scroll live in the view
  model, so a tab switch keeps them (one view serves every tab, as for the diff).
- **Modified** means: the result differs from the file as the resolver last read or wrote it.
- **Leaving.** While the resolver or the interactive list is open, every path that would replace
  it in the graph area (`OpenDiff` of a file of the stage panel, `OpenResolver`,
  `OpenInteractiveRebaseAsync`) first awaits `RepositoryViewModel.TryLeaveGraphAreaAsync()`, which
  asks 7.2's question when the result is modified (or the list changed) and returns false on "Keep
  editing"; it closes nothing itself. A diff of a commit's file (`OpenCommitDiff`, from the right
  panel beside the list) opens over the list without asking, and closing it returns to the list. A
  `Select` (from the left panel, a parent link, or a refresh) never closes the resolver or the
  list: it moves the graph's selection behind them.
- **Closing**: its close button (`ResolverCloseButton`) or Esc (11.10), asking first when the
  result is modified; when the conflicted files are all resolved (11.7); when the operation ends
  (Abort, or a refresh that leaves no conflict), without asking; when the tab closes, without asking
  (the work is in memory only, as an unsent stage-panel message is). When it closes the focus goes
  to the banner's Continue button when it is shown and enabled (the next step after the last file
  is resolved), otherwise to the graph.

### 11.2 Layout (owner's answer 5)

While the resolver is open, `CommitGraphView` spans the graph and right-panel columns
(`Grid.ColumnSpan` bound to `IsResolverOpen`), and the view shows the resolver where the graph or
the diff was. Column 2 keeps its width, so `PanelLayout.Fit`, the splitters' logic and the widths
saved in the session are unchanged; the right panel's two views and `RightSplitter` are hidden by
a new `RepoTabViewModel.ShowsRightPanel`. The left panel and `LeftSplitter` stay. The banner stays on
top, across the resolver's width.

```
┌ Merging: "Merge branch 'feature/theirs'". 1 file has conflicts.       [Continue][Abort merge] ┐ banner
│ U  notes.txt   Both modified               File 1 of 1  [‹][›]                          [✕] │ header, 36
│ Conflict 1 of 2  [˄][˅]   [Take ours] [Take theirs] [Merge tool…]          [Mark resolved]  │ toolbar, 36
├───────────────────────────────────────────┬─────────────────────────────────────────────────┤
│ Ours: main                                │ Theirs: feature/theirs                          │ titles, 26
│  1 line 1                                 │  1 line 1                                       │
│  [Use ours] [Use both]                    │  [Use theirs] [Use both]                        │ block bar, 26
│  2 line 2 ours                            │  2 line 2 theirs                                │
│  3 line 3                                 │  3 line 3                                       │
├───────────────────────────────────────────┴─────────────────────────────────────────────────┤
│ Result                                                                 2 conflicts left     │ title, 26
│  1 line 1                                                                                   │
│  2 <<<<<<< HEAD                                                                             │
│  3 line 2 ours                                                                              │
│  4 =======                                                                                  │
│  5 line 2 theirs                                                                            │
│  6 >>>>>>> feature/theirs                                                                   │
│  7 line 3                                                                                   │
└─────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Header** (36): the status letter `U` (`ResolverFileStatus`) in `VcDangerBrush`, the file name
  (`ResolverFileName`), its folder (`ResolverFolder`), the kind in git's words (`ResolverKind`:
  `Both modified`, `Both added`, `Deleted by them`, `Deleted by us`, `Added by us`, `Added by them`,
  `Both deleted`), `File {i} of {n}` (`ResolverFilePosition`) with previous and next (the conflicted
  files in the stage list's order, wrapping), and the close button.
- **Toolbar** (36): `Conflict {k} of {m}` (`ResolverConflictPosition`; with none left `No conflicts
  left`) with previous and next, `Take ours` and `Take theirs` (11.6), `Merge tool…` (11.9), and on
  the right `Mark resolved` (`VcPrimaryButton`, 11.7).
- **Top half**: the ours pane (`ConflictOurs`) and the theirs pane (`ConflictTheirs`) side by side,
  each half the width, a 1-px `VcBorderBrush` line between; each with a title strip (26) naming its
  side (11.4). **Bottom half**: the result (`ConflictResult`), with its title strip `Result` and on
  the right `{m} conflicts left` (`1 conflict left`, `No conflicts left`; `ConflictResultCount`). The
  halves split the height equally, with a 1-px line between; no splitter in phase 4.
- **Sizes at 1100×700** with a one-line banner (36): the resolver 840 wide (1100 − 260), each top
  pane 419.5 less its half of the divider; header 36, toolbar 36, the titles 26, about 212 of text in
  each half (13 rows of 15.84). The text is the code font at 12 (D64). Panes and result have one
  number column (8 + 7.2 × digits, at least 2, + 8) and no sign column. The scenario files of the
  checks keep their lines short (about 51 characters fit a top pane).

### 11.3 What the panes hold

The working-tree file is the source (it is what git wrote, in the user's conflict style and marker
size, and it holds a rerere resolution or the user's own edits). It is read as bytes, decoded as
the diff view decodes a file (UTF-8, or Latin-1 when it is not valid UTF-8, D66), with its line
ends kept; the bytes and the time it was read are kept for 11.7.

`ConflictText.Parse(text, markerSize)` (Core, pure, tested) splits it into common segments and
blocks. A block runs from a line that starts with `markerSize` `<` followed by a space or the line's
end to the next such `>` line, with an optional `|` section (diff3, zdiff3) and one `=` line; the
text after a marker is its label. The marker size is git's attribute `conflict-marker-size` for the
path (`git check-attr conflict-marker-size -- <p>`, default 7). Lines that do not form a whole
block are common text.

- **The ours pane** shows the file as it would be with every block's ours side: the common
  segments, and for each block its ours lines. **The theirs pane** likewise with the theirs sides.
  Their rows line up: a block's shorter side gets filler rows (as the side-by-side diff, D61), and
  before each block both panes have a block bar (26 high) with its buttons. The two panes scroll
  together. Line numbers count the pane's own lines.
- **Syntax**: the panes are coloured as the diff is (D64): `ConflictText` builds a synthetic
  `FileDiff` of one hunk (the common lines as context, a block's ours lines as removed and its
  theirs lines as added), so that `DiffHighlighter` tokenizes the ours file as the old side and
  the theirs file as the new side. The result has no syntax colours in phase 4.
- **The result** is the file itself, editable. An unresolved block shows git's markers as text: its
  marker lines on `VcConflictMarkerBrush` in `VcWarningBrush`, its ours section on
  `VcConflictOursBrush`, its theirs section on `VcConflictTheirsBrush`, a base section on
  `VcConflictBaseBrush`.
- **How it is built.** The panes are a `ConflictPanes` control made of two `DiffPane`s: phase 2's
  `DiffPane` is generalised over an internal `IDiffPaneHost` (palette, syntax, word highlights,
  scrolling and selection callbacks, a header's content per row with its buttons and automation
  ids, a tint per row, whether rows can be selected, whether the sign column shows), which
  `DiffTextView` and `ConflictPanes` both implement. Phase 2's pictures must stay byte-identical
  (`tr -d '\r' < docs/test-reports/phase-2-pictures.sha256 | sha256sum -c` for the phase 2 files)
  and its diff tests pass unchanged. WP0's spike proves this first; if it cannot, the panes are
  built on two read-only `TextEditor`s of their own, reusing only `DiffSyntax` and `DiffPalette`.
  The result is a `ConflictResultEditor`: an AvaloniaEdit `TextEditor` (`DiffTextEditor`'s base:
  the search panel removed, the working `ScrollTo`) with `LineHeightFactor` 1, no word wrap, no
  current-line highlight, no text drag and drop, no rectangular selection, no scrolling below the
  document, no hyperlinks, IME on, the selection in `VcSelectionBrush` with no border, undo on; its
  own background renderer for the tints; AvaloniaEdit's caret hidden (transparent) and drawn by the
  editor itself (a 1-px `VcTextPrimaryBrush` line one row high, only while the result has the
  keyboard focus, blinking every 530 ms only while `ConflictResultEditor.CaretBlinks` is true:
  false when the environment variable `VISUALCOMMIT_CARET_BLINK` is `0` at start-up, which
  `RealApp.Launch` sets, and set false by `ShellDriver.Start`). Its automation peer is a Document
  with a read-only Value pattern holding the text, so that the real-window pass can read it. Reading
  the file again replaces the document and clears its undo stack.

### 11.4 The sides' names

`{ours name}` and `{theirs name}` are the titles without `Ours: ` and `Theirs: `; tooltips and cards
use them (`Use the lines of {ours name}`).

| In progress | Ours title | Theirs title |
|---|---|---|
| Merge | `Ours: {cur}` | `Theirs: {merged}`: the branch name from `MERGE_MSG`'s `Merge branch '<x>'` (or `remote-tracking branch '<x>'`), else the theirs marker's label, else `MERGE_HEAD`'s short SHA |
| Cherry-pick | `Ours: {cur}` | `Theirs: {short} {subject}` (the picked commit) |
| Revert | `Ours: {cur}` | `Theirs: the revert of {short} {subject}` |
| Rebase | `Ours: {onto} (the new base)` | `Theirs: {short} {subject} (your commit)` |
| `git am` | `Ours: {cur}` | `Theirs: the patch` |
| Conflicts without an operation | `Ours: {cur}` | `Theirs: the stash` when the theirs marker's label is `Stashed changes`, else `Theirs: the other side` |

In a rebase the sides are the other way round from what users expect (0.1): the titles say which
is the user's own commit.

### 11.5 Choosing per block

- In the ours pane's bar: `Use ours` (`ConflictUseOurs`: the block becomes its ours lines) and `Use
  both` (`ConflictUseBothOursFirst`: ours, then theirs). In the theirs pane's bar: `Use theirs`
  (`ConflictUseTheirs`) and `Use both` (`ConflictUseBothTheirsFirst`: theirs, then ours).
- A choice replaces the block's text in the result (from its `<` line to its `>` line) with the
  chosen lines, as one undo step. Afterwards the keyboard focus goes to the result (no focus
  outline), the caret at the start of the replaced text.
- **The bars' states**, read from the result after every change: while a block's markers are in
  the result, both bars show their buttons and the sides keep their ours and theirs tints. After
  `Use ours`: the ours bar shows `Used in the result` (secondary text, `ConflictBarState`) and the
  block's ours rows take `VcConflictUsedBrush`; the theirs bar shows `Not used` and the theirs rows
  lose their tint. `Use theirs`: the mirror image. `Use both`: both bars say `Used in the result`,
  both sides take the used tint. An undo or redo that brings a block's markers back, or removes
  them, updates the bars, tints and counts at once. A result block is matched to the panes' block
  whose ours and theirs lines it holds (the first one not yet matched), by order only when none
  matches; a block whose markers the user edited away shows `Not used` on both bars.
- **Undo and redo** are the resolver's own bindings, from Avalonia's `HotkeyConfiguration.Undo`
  and `.Redo` (Ctrl+Z and Ctrl+Y on Windows and Linux; the platform's own on macOS), taken at the
  resolver's root in the tunnel phase while the focus is anywhere in it, and applied to the result's
  document; they replace AvaloniaEdit's own (0.6). The checks say "the platform's undo".
- **Reaching a block** (opening, the conflict arrows, Alt+Up and Alt+Down; a choice does not move
  on: the caret stays at the start of the replaced text) moves the caret to the start of the
  block's `<` line and scrolls each top pane so that the block's bar is the third
  visible row (fewer rows above it near the start), and the result so that the block's `<` line is
  the third visible row; a pane stops at its end. The first block is reached when the resolver
  opens, with the keyboard focus in the result.
- **Editing**: any text can be typed in the result. Choices, undo and redo update at once; typing is
  parsed again 300 ms after the last key. The count in the result's title is the truth.

### 11.6 A whole file from one side

`Take ours` and `Take theirs` (`ResolverTakeOurs`, `ResolverTakeTheirs`; text conflicts) replace the
whole result with that side's file, read with `git cat-file --filters --path=<p> <stage oid>` (the
working-tree form, 0.4), as one undo step; nothing is written until Mark resolved. A side the
conflict lacks disables the button with `This side deleted the file`. Afterwards every block's bars
and tints are as if that side had been chosen in it (11.5): after `Take ours` each ours bar says
`Used in the result` and its rows take the used tint, each theirs bar says `Not used` and its rows
lose their tint; `Take theirs` the mirror image. The result tints no line. Undoing it brings back the
bars and tints as they were before it, and so does undoing or redoing a choice (14.3).

### 11.7 Mark resolved

`Mark resolved` takes the result's text at the click. When the result still has blocks, it asks
first (7.2). Before writing, it compares the file on disk with the bytes it read; when they differ
(a terminal or a tool changed it), it asks (`{file} changed on disk.`, 7.2). It then writes the
result to the working-tree file (in the encoding it was read in, with the line ends the document
has; typed lines take the file's existing line end) and runs `git add -- <p>`, in one queued
operation `Resolve {file}` (10.1). Until that operation ends, the result is read-only and the
resolver's buttons are disabled. Then the resolver moves to the next conflicted file in the list, or
closes when none is left (the graph and the stage panel come back; the banner offers Continue).

### 11.8 Conflicts without text: the card

A conflict whose kind is not "both modified" or "both added", or whose content is binary (a NUL in
the first 8000 bytes of a side, as git decides), a symbolic link (mode `120000`) or a submodule
(`S...`), shows no panes. The resolver's body is a card (`ConflictCard`) in its middle with what
happened (`ConflictCardText`) and buttons that resolve the file at once (one operation `Resolve
{file}`, then 11.7's moving on). The toolbar then shows only `Merge tool…` (for a binary file); the
conflict navigation, `Take ours`, `Take theirs` and `Mark resolved` are hidden.

| Kind | Card text | Buttons and what they run |
|---|---|---|
| Deleted by them (`UD`) | `{ours name} changed {file}; {theirs name} deleted it.` | `Keep the file` (`ConflictKeepFile`): `git add -- <p>` (the file git left is ours; `git checkout --ours` only when it is missing), `Delete it` (`ConflictDeleteFile`): `git rm -- <p>` |
| Deleted by us (`DU`) | `{ours name} deleted {file}; {theirs name} changed it.` | `Keep the file` (`git add`; `git checkout --theirs` only when missing), `Delete it` (`rm`) |
| Added by us (`AU`) | `{file} exists only in {ours name}.` | `Keep it` (`git add`), `Delete it` (`rm`) |
| Added by them (`UA`) | `{file} exists only in {theirs name}.` | `Keep it` (`git add`; `--theirs` only when missing), `Delete it` (`rm`) |
| A file moved aside by a file/folder clash (`AU` or `UA` on `<p>~<label>`, `<p>` a folder in the index) | `Git moved {file} aside because {other name} has a folder named {p}: rename or remove it, then mark it resolved.` | `Mark resolved` (`ConflictCardMarkResolved`: `git add`) |
| Both deleted (`DD`) | `Both sides deleted {file}.` | `Delete it` (`rm`) |
| Binary (`UU` or `AA`) | `{file} is a binary file: its two versions cannot be merged line by line.` | `Use ours` (`ConflictUseOursFile`), `Use theirs` (`ConflictUseTheirsFile`): `git checkout --ours|--theirs -- <p>`, then `git add`; when the working-tree file differs from the stage about to be written (`git hash-object -- <p>`), a snapshot is saved first (D67's scheme, `discard-<time>`), so an edit is never lost |
| Symbolic link (`UU`) | `{file} is a symbolic link changed on both sides.` | `Use ours`, `Use theirs` (as for a binary file) |
| Submodule | `{file} is a submodule: resolve it in a terminal.` | none |

`git restore --ours` is never used (0.4).

### 11.9 The external merge tool

- **Offered** for text conflicts and binary files (git never runs the tool for the others, 0.4):
  `Merge tool…` in the toolbar (`ResolverMergeTool`) and `Open in merge tool` in the file menu.
  Enabled when git's configuration names a tool (`merge.tool`, read with `git config --get
  merge.tool` whenever the status shows a conflict, kept with the repository's state as
  `RepositoryMenuContext.MergeTool`); otherwise disabled with `Set merge.tool in git's
  configuration to use a merge tool` (a settings screen is phase 6's).
- **Before it**, a modified result asks first (7.2); "Keep editing" does not start the tool.
- **Runs** `git -c mergetool.keepBackup=false mergetool --tool=<tool> --no-prompt -- <p>` (the `-c`
  before the subcommand, 0.4) as one operation `Merge tool for {file}` that can be cancelled: a new
  `GitOperation.CanBeCancelled` for a local operation that only waits for another program (D149
  amends D81). The queue gives it a cancellation source as it gives a network operation; the status
  bar's Cancel (tooltip `Stop git and the merge tool`) stops git, its shell and, on Windows, the tool
  itself (git's job object, 0.6); a tab that closes leaves it running, as a local operation; "Quit
  now" ends it; "Finishing operations" lists it; stale locks are looked for after a cancel, as after
  a network operation. While it runs the queue and the watcher wait (D81), and the resolver is
  read-only with `Waiting for {tool} to close…` in the result's title.
- **After** it (in a `finally`, so after a cancel too): the files git leaves in the working tree on
  its failure paths (`<name>_BACKUP_<n>.<ext>`, `_BASE_`, `_LOCAL_`, `_REMOTE_` beside the file) are
  deleted when they did not exist before the run; the status is read again. When git staged the
  file the resolver moves on (11.7); otherwise the result is read again from the file (the tool may
  have written it) and the status bar says the tool closed without resolving.
- **Tests** use a fake tool: a repository-local `mergetool.fake.cmd` script that writes known
  content to `$MERGED` and exits 0, with `mergetool.fake.trustExitCode=true` (0.4: then git trusts
  the exit code instead of the file's time).

### 11.10 Keyboard and focus

- **Esc** (`EscapeOrder.Resolver` = 350, before the diff's 400): when the focused element has the
  `ConflictResultEditor` among its visual ancestors, the resolver takes Esc and does nothing, so a
  slip never loses work; otherwise it closes the resolver (asking first when the result is
  modified).
- **Alt+Up and Alt+Down** go to the previous and next conflict, **Alt+Left and Alt+Right** to the
  previous and next file; handled in the bubble phase at the resolver's root without
  `handledEventsToo`, so the editor's own uses of these keys win inside the result (word moves on
  macOS).
- **Tab order**: previous file, next file, close; previous conflict, next conflict, Take ours, Take
  theirs, Merge tool…, Mark resolved; the ours pane's bar buttons in view, then the theirs pane's;
  the result. Inside the result Tab types a tab; Ctrl+Tab leaves it.

### 11.11 Refreshes while it is open

A refresh that leaves the file conflicted keeps a modified result as it is and reads an unmodified
one again (a terminal or a tool may have changed it). A refresh after which the file is no longer
conflicted moves on (11.7) when the result is not modified; when it is, the resolver stays on the
file and its result title says `Marked resolved elsewhere`, and leaving asks first. A refresh that
ends the operation closes the resolver.

### 11.12 Its context menus

Built as data in `RepositoryMenus` like every menu (D86): a right-click on the header opens the
conflicted file's menu (7.1); the result's menu is [Undo, Redo] — [Cut, Copy, Paste, Select all]
(`ForConflictResult`); a pane's is [Copy] (`ForConflictPane`). Cut, copy and paste use the
platform's clipboard; no check copies or pastes in the real window.

## 12. What changes for earlier phases

### 12.1 Their checks and tests, changed on purpose

Recorded under "Changes to expected results" in phase 4's report, each with its reason; the tests
change in the same commit as the code that changes them (plan's "Visual test gate", item 6), by the
package that makes that code (14):

- **Phase 1, checks 13 and 14** (D150): the clone is of a bare copy of the 100k-commit repository
  served over HTTP by `AuthRemoteServer` on 127.0.0.1:47392 (a second fixed port of the visual
  checks, D97 amended), held at a fixed percentage instead of raced; picture `13b` shows the
  server's URL and that percentage. Check 13 failed once on CI's `ubuntu-latest` (run
  38213858149): a fast runner finished the clone between two pictures.
- **Phase 3's menus that gain items**: checks 1, 2, 5 (the keyboard walk of the commit menu), 6, 7,
  8 to 12 (check 11/12's two-commit menu gains a Squash that is disabled there), 9b (the conflicted
  file's menu gains `Resolve conflict` and `Open in merge tool`), 13, 13b, 16, 18b, 20, 27, 28 and
  30 (the drop menu's height too); the real-window pass's parts B and C; `RepositoryMenusTests`,
  `RepositoryMenusTests.Window` and `ContextMenuAdapterTests`: item lists, heights, and the first
  enabled item where it moves. Check 3's menus do not change.
- **Phase 3's banner**: the Continue and Skip buttons and the layout of 5.2 (checks 9, 9b, 11b
  steps 2 and 4, 12 steps 8 and 9, and 25; check 27 keeps one line at 1920); the icon's tooltip
  (checks 9, 14, 15b and 25, the real-window pass's part B); the texts of 5.2 marked "new" (check 12
  step 11's alternative, the empty pick; check 25's list, rebase and empty texts); the merge and
  single-pick texts do not change.
- **Phase 3's stage panel**: the conflicted row's tooltip (checks 9 step 4, 14 step 8, 15b step 6,
  and 25 steps 2, 5 and 8); the empty pick's commit tooltip; the message filled from `MERGE_MSG` now
  keeps a commit's own `#` lines.
- **Phase 3's check 11b** (a pick of two commits that stops): the app now makes one git call with
  the list, so `sequencer/` is there at the stop; the status says `Cherry-picked 1 of 2 commits;
  stopped at 078ef05 with conflicts in 1 file`; the Abort question says the commits already made
  are dropped too; after Abort `main` is where it was before the first pick.
- **Phase 3's check 25 step 4** (a terminal's pick sequence): the stopped pick is committed in a
  terminal (the check runs `git commit --no-edit` through `TempRepo`), and the check shows the paused
  banner, Continue enabled, Skip disabled with its reason and the Abort that keeps HEAD; a new step
  on a fresh copy shows the stage panel's `Continue cherry-pick` (step 2's button text changes from
  "Stage files to commit").
- **Phase 3's tests**: `InProgressTests` (lines 68, 89, 115, 125, 156 and 202: the state's new
  properties); `HistoryOperationTests` (171, 329: one call per commit) and
  `RepositoryOperationsHistoryTests` (89, 159, 178, 207, 290: lists, the empty pick in a list);
  `RepositoryOperationsMergeTests` (417: "Abort to go on"); `ConflictBannerTests`,
  `WorkingChangesInProgressTests`, `Phase3SupportTests`.
- **Phase 2's conflicted-file diff body** ("This file has a merge conflict"): replaced by the
  resolver; no phase 2 check shows it (phase 2's handoff recorded it as expected until the
  resolver). Phase 2's pictures do not change (11.3).

### 12.2 `plan.md` and the risks

At the end of the phase (closing step 3): risk 2 (the resolver) is settled or rewritten with what
was built; risk 17 (reftable) settled; risk 24 (backups never deleted) settled by section 9; risks
12 and 18 (run times) updated with phase 4's measures. Phase 5's "From phase 3" item on backups gains
the kinds `rebase` (`rebase-<time>/<b>`) and `skip` (with `-changes`), reads "phase 4 cleans them
up (D139): undo offers what is left", and its activity log is to show the app log's `Removed backup`
lines.

## 13. Tests and harness

### 13.1 Scenario repositories (`tests/VisualCommit.Testing/Scenarios.Phase4.cs`)

Built as templates (copied for each test) and pinned in
`tests/VisualCommit.Git.Tests/Phase4ScenarioTests.cs`; stopped states are built per call, as
phase 3's `StoppedAsync`. Times by `TempRepo`'s clock (2026-01-01 12:00 plus a minute per commit).
Every file holds short lines (about 51 characters fit a resolver pane at 1100×700).

1. **"rebase"** (`RebaseAsync`): `Initial commit` (README.md), `Add core` (src/core.txt); from it
   `feature/topic`: `Topic one` (topic/one.txt), `Topic two` (topic/two.txt); then on `main`:
   `Main work` (src/core.txt changed), `Main more` (docs/main.txt); from `Add core`
   `feature/other`: `Other one` (other/one.txt). A bare `origin` with `main` and `feature/topic`
   pushed and tracked. HEAD on `feature/topic`. Rebasing the topic onto `main` has no conflict.
2. **"interactive"** (`InteractiveAsync`): one line of history on `main`: `Initial commit`, `Base`
   (src/base.txt), `One` (src/one.txt), `Two` (src/two.txt), `Fix one` (src/one.txt changed: a
   fixup candidate), `Three` (src/three.txt, with a body line `#42 is the issue`), `Four`
   (src/four.txt), `Five` (src/five.txt). HEAD on `main`. Every reorder of these commits applies
   without a conflict, except moving `Fix one` below `One` (it would not apply before `One`).
3. **"conflicts"** (`ConflictsAsync`): `Initial commit`; `Base` with notes.txt (twelve short lines,
   `line 1` to `line 12`), other.txt and image.png (a `TestImages` picture); on `main`: `Ours
   edits` (notes.txt lines 2 and 10 changed, other.txt changed, image.png changed to another
   picture, new.txt added); from `Base`: `feature/theirs`: `Theirs edits` (notes.txt lines 2 and
   10 changed otherwise, line 6 changed: merged cleanly); `feature/rebase`: `First change`
   (notes.txt line 2), `Second change` (notes.txt line 10): each conflicts with `main` on its own;
   `feature/picks`: `Pick clean` (pick.txt), `Pick conflicting` (notes.txt line 2), `Pick last`
   (last.txt); `feature/kinds`: other.txt deleted, image.png changed to a third picture, new.txt
   added with other lines. `feature/theirs`, `feature/rebase` and `feature/picks` do not touch
   other.txt, image.png or new.txt. HEAD on `main`. Stopped copies (`ConflictsStoppedAsync(kind)`),
   each made by git as a terminal would: a merge of `feature/theirs` (notes.txt `UU` with two blocks
   at least four lines with letters apart, so git keeps them apart); a rebase of `feature/rebase`
   onto `main` (stopped at `First change`; resolved per block it then stops again at `Second
   change`); a pick of the three `feature/picks` commits (stopped at the second); a merge of
   `feature/kinds` (other.txt `UD`, image.png binary `UU`, new.txt `AA`); optionally with the fake
   merge tool configured.
4. Pull with rebase uses phase 3's "sync" scenario (`RemoteSyncAsync` with
   `PushFromElsewhereAsync`): a branch and its upstream that have diverged, through a file remote
   (no server, so no port).
5. Phase 1's checks 13 and 14: `LargeHistory.GetBareAsync()`, a bare copy of the 100k-commit
   repository made once per machine beside the working copy (its own marker file), served in place.

The Git-layer packages (WP1 to WP3) build their tests' repositories with `TempRepo` directly, as
phase 3's Git tests do, so that they do not wait for these scenarios.

As built by WP-S (`d64e2b7`; `Phase4ScenarioTests` pins every SHA and stopped state): feature/kinds'
commit is `Kinds edits`; main's `Ours edits` is the conflicts scenario's newest commit (its top
row); the interactive and conflicts scenarios have no remote; image.png is a 32×32 picture with a
white centre, indigo at Base, green on main, red on feature/kinds; the fake merge tool is
`mergetool.fake.cmd` = `sh "$(git rev-parse --git-common-dir)/vc-fake-merge-tool.sh" "$MERGED"`,
writing `Resolved by the fake merge tool.` (`ConfigureFakeMergeToolAsync`). The stopped states are
`ConflictsStoppedAsync(ConflictsStop.Merge | Rebase | CherryPicks | MergeKinds, mergeTool)`.

### 13.2 Harness additions

- `TempRepo`'s environment sets `GIT_EDITOR=:` and `GIT_SEQUENCE_EDITOR=:`; `GitIsolation` and
  `RealApp.Launch` remove the machine's `EDITOR`, `VISUAL`, `GIT_EDITOR` and `GIT_SEQUENCE_EDITOR`
  (0.5; the handoff's isolation rule). `TempRepo.GitWithEnvironmentAsync(extra, args)` and its
  "allowing failure" twin keep the clock's dates for a call with its own variables.
- `AuthRemoteServer.HoldNextFetchPack(int atPercent = 0)`: 0 keeps phase 3's rule; a percentage
  holds right after the first whole progress line at or above it, re-framing the packet so that
  git's last line shows exactly that percentage (D150). A second fixed visual-checks port, 47392,
  with the same fallback as 47391. Tests in `AuthRemoteServerTests`.
- `ShellDriver.Phase4.cs` (WP-S: `Undo()` and `Redo()` pressing the first gesture of
  `HotkeyConfiguration.Undo` and `.Redo`, the banner's Continue and Skip, `ConflictResultEditor
  .CaretBlinks = false` in `Start`), `ShellDriver.Phase4.Rebase.cs` (WP5: the list as a snapshot,
  a row's point, a drag of a row in steps, a row's action through its menu) and
  `ShellDriver.Phase4.Conflicts.cs` (WP6: the resolver as a snapshot, a bar's button, the result's
  text, caret and modified state, typing into the result, a click at a line and column), each
  package writing the driver of the controls it builds, on their public test surface
  (`InteractiveRebaseView.Rows`, `ConflictPanes.RowsOf(side)`, `ConflictPanes.BarButton(block,
  side, choice)`, `ConflictResultEditor.PointAt(line, column)`, `TintAt(line)`, `CaretPosition`,
  `IsModified`). `RealApp.Phase4*.cs` likewise, through the automation ids of 7.4, and
  `VISUALCOMMIT_CARET_BLINK=0` in `RealApp.Launch`.
- `Phase4Checks.Support.cs` copies what phase 3's support file offers (phase 3's stays as it is),
  and has once what phase 3 repeated in each part (a graph assertion, `BackupsAsync`,
  `AssertCalled`, `WaitForOperationAsync`); `AssertBanner` knows Continue, Skip and 5.2's layout.

### 13.3 The walk-through project (D151)

`tests/VisualCommit.VisualTests.Phase4`, a project of its own as phase 3's (D133): its own headless
UI thread, in `VisualCommit.slnx`, picked up by CI with no change to the workflow;
`Phase4Expectations*.cs` linked into the real-window project, the tests' own files named so that the
link's pattern does not take them (`Phase4ExpectationTests.cs`, `Phase4SupportTests.cs`, excluded by
name). `scripts/time-scripted-check.sh` takes phase 4 (and makes it the default); CLAUDE.md's
Commands name the project.

### 13.4 Proposed visual checks (the report's writers write them in full, step 7)

"RW" marks what the real-window pass repeats (a picture of every new screen, and what only a real
window shows: a real drag, real typing into the editor, a real double-click, a real process for the
merge tool). Window 1100×700, dark, unless said.

| # | Check | RW |
|---|---|---|
| A1 | The new menu items: a commit not on HEAD, a commit on HEAD's line, HEAD's own commit (each item's state and reason, one disabled reason's tooltip), a local and a remote branch, the current branch with `Pull … (rebase)`, several commits with `Squash` enabled and, for a selection that is no chain, disabled | the commit menu |
| A2 | A plain rebase from the branch menu (`Rebase feature/topic onto main`): graphs before and after, the status, the backup ref and its target | yes |
| A3 | Rebase by drag: the drop menu with its rebase items (the current branch onto `main`; `feature/other`, not current, onto `main`), then "Check out feature/other and rebase it onto main": graph, HEAD | the drop menu |
| A4 | Pull with rebase against a file remote: before and after, backup, status | n/a |
| A5 | Rebases git refuses: a changed tracked file (the error dialog's title, summary and git's lines; nothing changed), and "Check out … and rebase it" with a changed file (refused before the checkout: still on the branch the user was on) | n/a |
| B6 | The interactive list from the commit menu on `Base`: header, rows newest on top, the base row, the right panel on a selected row, a row's action menu, the bottom row's disabled Squash with its tooltip | yes |
| B7 | Editing the list: drag `Fix one` below `Two` (right above `One`), Fixup on `Fix one`, Squash on `Four` (joins `Three`) and `Edit…` its message keeping `#42 is the issue`, Reword `Two` (the dialog), Drop `Five`, `Start rebase`: the history and messages after it, the backup | yes (the drag, the reword dialog and Start) |
| B8 | Squash two selected commits: the menu, the dialog filled with both messages, the result | n/a |
| B9 | Esc on a changed list: the question; Discard; nothing changed | n/a |
| C10 | A merge conflict end to end: the banner (Continue disabled, its reason), the resolver's layout, titles and both panes, `Use ours` on block 1, `Conflict 2 of 2`, `Use theirs` on block 2, the platform's undo, `Use both`, the result, Mark resolved, the resolver closes, Continue: the merge commit in the graph | yes (with a real double-click on the file: the result unchanged, `File 1 of 1`) |
| C11 | A rebase with two conflicting commits: the first stop's banner (`1 of 2`, Continue and Skip), the panes with the rebase's titles, `Use theirs`, Mark resolved, Continue to the second stop, `Use both`, Mark resolved, Continue to the end: the graph and the backup | yes (the banner) |
| C12 | A cherry-pick of three commits that stops at the second: banner (`1 more commit waits`), the stage panel's `Continue cherry-pick`, the panes with the pick's titles, `Use ours` then a typed edit, Mark resolved, a message edited in the stage panel, Continue: the history and the edited message | n/a |
| C13 | Skip and Abort: a rebase stopped at `First change`: Skip with its question, the `skip` backup, the stop at `Second change`, Abort: the branch and HEAD as before; a pick list stopped: Abort undoes the whole list; "Check out … and rebase it" stopped: Abort checks out again the branch the user was on | n/a |
| C14 | Editing freely: typing, the platform's undo and redo, Esc inside the result does nothing, Mark resolved with a block left (the question), Stage all on a file with markers (the question), leaving with a modified result (the question) | yes (real typing) |
| C15 | Conflicts without text: the merge of `feature/kinds`: the "deleted by them" card, `Keep the file`; the binary card, `Use theirs`; the add/add file in the panes | yes (the "deleted by them" card and `Keep the file`) |
| C16 | The external merge tool (the fake tool): `Merge tool…`, the file resolved, the resolver moves on, the status | yes (a real process) |
| C17 | A rebase started in a terminal: the banner offers Continue and Skip (no "terminal" text); Continue from the app finishes it | n/a |
| D18 | The light theme at 1920×1080: the interactive list and the resolver (each tint's colour sampled) | n/a (scripted only: the size does not fit the development screen) |

### 13.5 Each "Delivers" item and what proves it

| Delivers | Proven by |
|---|---|
| Rebase onto, from menus and by drag | A1 to A3, A5, C13; `RebaseOperationTests` (Git), `RepositoryOperationsRebaseTests`, `RepositoryMenusTests` |
| Pull with rebase, also chosen for one pull; Continue and Skip after a pull's rebase | A4; `PullRebaseParityTests`, `RepositoryOperationsRebaseTests` (a configured and a chosen pull whose rebase stops, with and without a conflicted file, finished by Continue and by Skip) |
| Interactive rebase: reorder, squash, fixup, reword, drop; squash a multi-selection | B6 to B9; `RebaseTodoTests` (Core, pure), `InteractiveRebaseTests` (Git: the delivery with paths with spaces and apostrophes, two message files, the guarded amend in every case of 0.2, hooks), `InteractiveRebaseListParityTests`, `InteractiveRebaseViewModelTests` |
| The banner's Continue, Skip, Abort for merge, rebase, cherry-pick and revert | C10 to C13, C17; `ContinueSkipTests` (Git, every row of 5.1), `ConflictBannerTests`, `WorkingChangesInProgressTests` |
| The resolver: three panes, per block, editing, a whole file, mark resolved, the merge tool | C10, C11, C12, C14 to C16, D18; `ConflictTextTests` (Core), `ConflictResolverViewModelTests`, `ConflictResultEditorTests`, `ConflictOperationTests` (Git: every kind of 0.4) |
| Several picks and reverts with git's list (D136) | C12, C13; `HistoryOperationTests` (every row of 4), `RepositoryOperationsHistoryTests` |
| Reftable (D128) | `PseudoRefTests` (the probe's rules; a real reftable repository on git 2.45+) |
| The sequence editor of the app's own | B7; `InteractiveRebaseTests` |
| Backups: the rebase's and Skip's, the automatic clean-up | A2, C13; `BackupCleanupTests` (Git, with a fixed clock), `RepositoryOperationsBackupCleanupTests` (App: at opening, after a backup, a failing clean-up shows nothing) |
| Phase 1's check 13 made deterministic | phase 1's checks 13 and 14; `AuthRemoteServerTests` (the hold at a percentage) |

### 13.6 Run time

The default run took 19 to 26 minutes on the laptop with phase 3 (23 min 21 s for phase 4's
baseline, with other work beside it). Phase 4 adds a fifth test process of about 18 checks
(estimated 8 to 12 minutes alone) and Git-layer tests that each start several git processes; the
run is bound by the processors, so expect it to grow by a few minutes. On CI the Windows job's test
step took about 9 minutes of its 14-minute limit at phase 3's gate: every package reads its CI runs,
and when the Windows test step passes 12 minutes the lead raises the step's limit, taking the owner's
answer recorded in D134 ("let it run as long as it needs") as allowing it, and records that as an
amendment of D134 (done on 2026-10-12: D152, at 12 min 8 s). The real-window pass gains about seven launches (estimated 5 to 7 minutes on top
of phase 3's 11).

## 14. Work breakdown

Each package is built in its own worktree on a branch made **from `phase/4-rebase-conflicts`**
(`wip/p4-<name>`; a worktree starts from `master`, so its agent creates the branch from the phase
branch first), merged back with `--no-ff` after its tests pass and the solution builds, its worktree
removed at once and its fsmonitor daemon stopped (phase 3 handoff, "Things that cost time"). A
package edits only the files named for it; anything else goes through the lead. A package whose code
changes an earlier phase's expected result edits that phase's test and expectation files in the same
commit.

| Package | Builds and owns |
|---|---|
| WP-C13 (started first, independent) | Phase 1's checks 13 and 14 (D150): `LargeHistory.cs` (`GetBareAsync`), `AuthRemoteServer.cs` (`HoldNextFetchPack(atPercent)`, port 47392) and `AuthRemoteServerTests.cs`, phase 1's check file; the real-window project's comment (handoff step 3) |
| WP0 (lead) | **Core and Git**: the new types (the checklist below); `InProgressState.cs`, `WorkingTreeStatus.cs` (`Conflicts`, `SameAs`); `IGitRepository.cs`; `FakeRepositoryBase.cs` (reads empty, `CleanUpBackupsAsync` returns an empty list); one stub file per package (`GitRepository.Stubs.WP1.cs`, `.WP2.cs`, `.WP3.cs`); `GitRepository.Changes.cs` (the `u` lines); `GitRepository.Branches.cs` (shared helpers: `WriteCommand` with extra environment, `ClassifyRebaseAsync`, `ReadPseudoRefAsync`). **Queue**: `GitOperation.cs`, `OperationQueue.cs` (`CanBeCancelled`), `OperationStatusViewModel.cs` (auto-fetch texts by title). **App contracts**: `IRepositoryActions.cs`, `IInProgressActions.cs`, the `IWorkingChangesHost` interface, `IInteractiveRebaseHost`, `IConflictActions`, and their test fakes; `ConflictBannerViewModel`'s constructor and `Show`; `RepositoryOperations.Stubs.cs` (bodies that fail); `RepositoryMenuContext.cs` (the facts), `CommitGraphData.cs` (`LinearLineFromHead`); `IEscapeHandler.cs` (`EscapeOrder`); `DialogHostViewModel.cs` and `ConfirmationView.axaml` (`CancelText`); `MessageDialogViewModel`'s shape and a plain view. **Theme**: `Icons.axaml`, `THIRD-PARTY-NOTICES.md`, `Tokens.axaml` (the new icons and tokens). **Tests**: `TempRepo.cs`, `GitIsolation.cs`, `RealApp.cs` (the editor variables, the caret switch). **The spike**: the editable result editor and the generalised `DiffPane` (11.3), headless |
| WP-S | The scenarios of 13.1 and `Phase4ScenarioTests`; the Phase 4 project (csproj, AssemblyInfo, `Phase4Checks.Support.cs`, `Phase4SupportTests.cs`), `ShellDriver.Phase4.cs`, `RealApp.Phase4.cs`, `TempRepo.GitWithEnvironmentAsync`, `scripts/time-scripted-check.sh` |
| WP1 | Git: `GitRepository.Rebase.cs` (rebase onto, interactive rebase, squash, the list's commits), `Core/Git/RebaseTodo.cs`, `GitRepository.Remotes.cs` (`IntegratePullAsync` and its helpers); `RebaseOperationTests`, `InteractiveRebaseTests`, `InteractiveRebaseListParityTests`, `RebaseTodoTests`, `PullRebaseParityTests`, `PullParityTests` |
| WP2 | Git: `GitRepository.History.cs` (lists), `GitRepository.InProgress.cs` (section 8), `GitRepository.Sequence.cs` (Continue, Skip); `HistoryOperationTests`, `InProgressTests`, `ContinueSkipTests`, `PseudoRefTests` |
| WP3 | Git: `GitRepository.Conflicts.cs` (versions, sides, resolutions, the merge tool), `GitRepository.Backups.cs` (the clean-up), `GitRepository.Writes.cs` (discard snapshots named by the backup clock), `Core/Conflicts/*` (`ConflictText`); `ConflictOperationTests`, `ConflictTextTests`, `BackupCleanupTests` |
| WP4 | App operations, menus and texts: `RepositoryOperations.Rebase.cs`, `.Conflicts.cs`, `.Branches.cs` (lists, Abort's return, `HasSequencer` removed), `.Remotes.cs` (pull), `.Run.cs` (`RunOptions.CanBeCancelled`, the clean-up after a backup), `.Files.cs` (marking resolved, staging with markers); `RepositoryMenus.cs`; `OperationTexts*.cs`; the banner (`ConflictBannerViewModel.cs`, `ConflictBanner.axaml(.cs)`); the stage panel (`WorkingChangesViewModel.cs` apart from the interface, `WorkingChangesView.axaml`, `CommitDetailsRows.cs`); `AutoFetchScheduler.cs` (counting a pull with rebase); their tests, and phase 3's test, expectation and real-window files that change with them (12.1) |
| WP5 | The interactive list: `ViewModels/Rebase/*`, `Views/Rebase/*`, the message dialog's view model and view; `ShellDriver.Phase4.Rebase.cs`, `RealApp.Phase4.Rebase.cs`; `InteractiveRebaseViewModelTests`, `MessageDialogTests` |
| WP6 | The resolver: `ViewModels/Conflicts/*`, `Views/Conflicts/*`, `Controls/Conflicts/*`, `Controls/Diff/*` (the generalisation of 11.3: phase 2's pictures and tests unchanged), `Views/Diff/DiffView.axaml` and `ViewModels/Diff/DiffViewModel.cs` (the Conflict body removed); `ShellDriver.Phase4.Conflicts.cs`, `RealApp.Phase4.Conflicts.cs`; `ConflictResolverViewModelTests`, `ConflictResultEditorTests`, `ConflictPanesTests`, and a headless test of 11.1's 500-ms guard (a press with click count 2 on the open resolver's `ConflictUseTheirs` within 500 ms of its opening changes nothing; after 500 ms the same press chooses) |
| WP7 | The scripted checks of the report, part by part: `Phase4Checks.{A,B,C,D}*.cs`, `Phase4Expectations*.cs`, `Phase4ExpectationTests.cs` |
| WP8 | The real-window pass for the RW checks: `Phase4RealWindowPass*.cs`, the Phase 4 link in `RealWindowTests.csproj` |
| Lead | `RepositoryViewModel*.cs` (the hooks: `Resolver`, `IsResolverOpen`, `OpenResolver`; `InteractiveRebase`, `IsInteractiveRebaseOpen`, `OpenInteractiveRebaseAsync`; `TryLeaveGraphAreaAsync`, and `OpenDiff` sending a conflicted file to the resolver; the resolver fed from `OnStatusApplied`; the banner's Continue and Skip with the Skip question; `ReadOperationMessageAsync`; `OnFirstLoadCompleted` and the restore bar's snapshot as `keep`; the details for the list; `MergeTool` kept with the state), `RepoTabViewModel.cs` (`ShowsRightPanel`), `MainWindow.axaml(.cs)` (the column span, the hidden right panel and splitter), `MainWindowViewModel.cs`, `CommitGraphView.axaml`, `GitRunner.cs`, `Program.cs` and `App` (the caret switch), the `.slnx`, CI, the docs, CLAUDE.md |

**Order**: WP-C13 runs from the start. WP0 (with the spike); then WP-S, WP1, WP2 and WP3 side by
side (WP1 to WP3 make their test repositories with `TempRepo`); the report (step 7) is written once
WP-S has pinned the scenarios, and committed before WP4 to WP6 build UI; then WP4, WP5 and WP6 side
by side; then WP7 and WP8; then the review (step 21) and the gate.

**WP0's contract checklist** (Core types; `VisualCommit.Core.Git` unless said):

```csharp
public enum RebaseOutcome { UpToDate, Rebased, Stopped }
public sealed record RebaseResult(RebaseOutcome Outcome, string? NewTip, int ConflictedFiles) { public bool AutostashConflicted { get; init; } }
public enum RebaseAction { Pick, Reword, Squash, Fixup, Drop }
public sealed record RebaseStep(string Sha, RebaseAction Action, string? Message); // Message: a reword's, or a group's on its target
public sealed record RebasePlan(string Onto, string ExpectedTip, string BranchRef, IReadOnlyList<RebaseStep> StepsOldestFirst);
public sealed record RebaseListEntry(string Sha, string Subject, string AuthorName, string Message);
public sealed record RebaseList(string Onto, string BranchTip, IReadOnlyList<RebaseListEntry> CommitsOldestFirst, int MergeCount);
public enum PullIntegration { AsConfigured, Rebase }
public enum SequenceStop { None, Conflicts, Empty, Refused }
public sealed record SequenceResult(SequenceStop Stop, int Applied, int Total);
public enum ContinueOutcome { Done, Stopped }
public sealed record ContinueResult(ContinueOutcome Outcome, string? NewTip);
public enum RebaseStopKind { Pick, Rescheduled, ExecFailed, OwnAmendFailed, Edit, Empty }
public enum ConflictKind { BothModified, BothAdded, DeletedByThem, DeletedByUs, AddedByUs, AddedByThem, BothDeleted }
public sealed record ConflictEntry(string Path, ConflictKind Kind, bool IsSubmodule, string BaseMode, string OursMode, string TheirsMode, string BaseId, string OursId, string TheirsId);
public enum ConflictSide { Ours, Theirs }
public sealed record ConflictContent(byte[] WorkingTree, int MarkerSize, bool OursBinary, bool TheirsBinary, bool IsSymbolicLink, bool WorkingTreeExists);
public sealed record MergeToolResult(bool Staged);
public sealed record BackupRetention(int Days, int Count) { public static BackupRetention Default { get; } = new(30, 50); }
public enum BackupRemovalReason { OlderThanDays, BeyondCount }
public sealed record RemovedBackup(string Name, IReadOnlyList<string> Refs, BackupRemovalReason Reason);
```

`InProgressState` (init): `Sequenced`, `HasPickHead`, `StopKind` (`RebaseStopKind?`), `NextIsSquash`,
`Group` (`int?`), `Done`, `Total`. `WorkingTreeStatus` (init): `Conflicts`
(`IReadOnlyList<ConflictEntry>`), compared by `SameAs`. `IGitRepository`: `RebaseAsync(onto)`,
`ReadRebaseListAsync(onto, fromCommit)`, `InteractiveRebaseAsync(plan)`,
`IntegratePullAsync(branch, integration, beforeRebase)` (the old overload kept as `AsConfigured`
without a callback), `CherryPickAsync(IReadOnlyList<string>)`, `RevertAsync(IReadOnlyList<string>)`,
`QuitSequenceAsync(kind)`, `ContinueAsync(state, message)`, `SkipAsync(state)`,
`ReadOperationMessageAsync(state)`, `ReadConflictAsync(entry)`, `ReadConflictSideAsync(entry,
side)`, `WriteResolutionAsync(path, content)`, `TakeSideAsync(path, side)`,
`RemoveConflictedAsync(path)`, `ReadMergeToolAsync()`, `RunMergeToolAsync(path, tool)`,
`CleanUpBackupsAsync(retention, keep)`, each with `NetworkOptions?` and a `CancellationToken` as
phase 3's writes. The queue: `GitOperation.CanBeCancelled`, a cancellation source for it, no cancel
at the tab's disposal, stale locks after its cancel; `RunOptions.CanBeCancelled`. The app:
`IRepositoryActions` (`RebaseAsync(onto)`, `CheckoutAndRebaseAsync(branchRef, onto)`,
`OpenInteractiveRebaseAsync(request)`, `SquashAsync(commits)`, `PullRebaseAsync()`,
`ResolveConflict(file)`, `OpenMergeToolAsync(path)`), `IInProgressActions` (`ContinueAsync(state,
message)`, `SkipAsync(state)`), `IWorkingChangesHost` (`ContinueAsync(message)`,
`ReadOperationMessageAsync(state)`, with default bodies), `ConflictBannerViewModel(abort,
continue, skip)` and `Show(state, conflicts, ontoName)`, `MessageDialogViewModel(title, message,
confirmText)` (`DialogViewModel<string?>`), `ConfirmationRequest.CancelText`,
`EscapeOrder.Resolver` = 350 and `EscapeOrder.InteractiveRebase` = 360,
`RepositoryMenuContext.TipsOnHead` and `.MergeTool`, `CommitMenuInfo.HeadLineDepth`,
`DropFacts.SourceContainsTarget` (`bool?`).

### 14.1 What WP0 settled (merged in `a7ce01f` and `6090f8f`)

The code's doc comments are the reference; in short, where WP0 differs from or adds to the text
above:

- `ReadRebaseListAsync(onto, branchRef, fromCommit, …)` takes the branch (2.1's drop variant lists
  a branch that is not checked out). Every new `IGitRepository` member takes `NetworkOptions?` and a
  `CancellationToken`, reads included (call `CleanUpBackupsAsync` with a named token). `SkipAsync`
  returns a `ContinueResult`. The list overloads of `CherryPickAsync` and `RevertAsync` sit beside
  phase 3's single-commit ones.
- `ClassifyRebaseAsync` for a call made while a rebase folder already existed (Continue, Skip): a
  fatal exit (128 or above) or git's `You must edit all merge conflicts` throws; any other result
  with the folder still there is `Stopped`; the conflicted count comes from `ls-files --unmerged`.
- The app's actions take a `RebaseTarget(Sha, Name)` (the SHA read when the menu opened, the name
  for the texts) and an `InteractiveRebaseRequest(Onto, BranchRef, FromHere, CheckoutFirst)`;
  `ResolveConflict(ChangedFile)` returns nothing, as `OpenDiff` does. `IConflictActions.TakeSideAsync`
  is "Keep the file" and "Keep it" too: the file git left is staged as it is and checked out from
  that side only when it is missing (11.8). `RepositoryViewModel.ReadDropFactsAsync(source,
  target)` reads the drop's facts, one more git call for a drop onto a remote branch.
  `CommitGraphData.DepthOnHeadLine(sha)` fills `HeadLineDepth`. `RepositoryMenuContext.MergeTool` is
  filled by the lead's hook (11.9).
- The spike: `ConflictPanes`, `ConflictResultEditor` (with their test surface), the generalised
  `DiffPane` (`IDiffPaneHost`, `CodeTextEditor`), and the five tokens. Phase 2's 66 pictures stayed
  byte-identical. The panes' enums are the app's own (`ConflictChoice`, `ConflictPaneSide`).
  AvaloniaEdit's `Undo()`, `Redo()`, `CanUndo` and `CanRedo` run through the routed bindings the
  editor removes: the result's undo and redo, its menu included, call `ConflictResultEditor.Undo()`
  and `Redo()` (or the document's `UndoStack`), never the base methods. **Typed text is undone one
  character per step** (AvaloniaEdit 12.0.0); a choice, a whole side and Take ours or theirs are one
  step each, and the checks undo a choice, not typing. `IsModified` compares the text with the
  document's original (`MarkOriginal()`); a file read again is shown as a new document. Ctrl+Tab
  and Ctrl+Shift+Tab leave the result; Shift+Tab stays AvaloniaEdit's unindent. A headless test
  sees a `DispatcherTimer` (the caret's blink) tick only while it awaits.

### 14.2 What WP1 to WP3 settled (merged in `81769a6`, `0a4fdc6`, `bd18b84`)

- **WP1** (rebase): the fork point's count is, before the rebase, `rev-list --count --no-merges
  --right-only --cherry-pick <upstream>...HEAD` less the same count with `^$(git merge-base
  --fork-point <upstream> HEAD)` (0 without a fork point): section 6's before-and-after count
  would also count a commit git drops as empty. The sequence editor copies every message file
  with one pattern (`vc() { cp '<folder>/'vc-message-* "$(dirname "$1")/" && cp '<todo>' "$1";
  }; vc`), so that its value stays under Windows' 32,767 characters for any number of groups.
  `PullResult.LeftOutByForkPoint`. `RebaseTodo.SquashMessage` and `.SquashPlan` build a squash's
  message and plan; the squashed commit is `NewTip~(list length − N)`. A moved HEAD at Start throws
  with `GitRepository.RebaseHeadMovedMessage`. Phase 3's `PullRebaseParityTests` (for
  `pull.rebase=true`) is now `PullRebaseTrueParityTests`. Left: `RebaseResult` carries no git
  output (the app takes it from the call log, `FailureOfLastCall`); a pull's rebase has no
  autostash flag; a commit message in another encoding than UTF-8 is not kept byte for byte by an
  interactive rebase's own-message copy (known issue).
- **WP2** (lists, Continue, Skip): a paused list's state names the commit that waits
  (`Commit`, `Subject`), so "paused" is `Sequenced && !HasPickHead` (phase 3's banner and the stage
  panel were changed to read it so, `4177a58`); `ContinueAsync` throws `InvalidOperationException`
  for a list stopped on a refused commit; a list refused because conflicts were already there is
  quit too; `NextIsSquash` is false when the stopped command is itself a fixup or squash (a group's
  member: emptying it joins nothing elsewhere); `Group` is set for `OwnAmendFailed`; a rebase's
  `Empty` stays false and `StopKind` carries emptiness; a rescheduled pick on a newer git (no
  `stopped-sha`) is recognised by `done` ending with the same command; with `rebase.backend=apply`
  Continue with nothing staged stops again ("No changes"). No state tells a fixup or squash stop of
  a rebase from elsewhere apart, so 5.3's tooltip for it is dropped.
- **WP3** (conflicts, the clean-up): `ConflictText` (`Parse`, `Segments`, `Blocks`, `Choose`,
  `BlockChoice` `Ours`, `Theirs`, `OursThenTheirs`, `TheirsThenOurs`, `Decode` and `Encode` byte
  for byte); its parsing is strict as git's rerere is (one `=` marker, `|` only before it), so a
  block whose theirs side holds a 7-character `=======` line is read as common text, and `git diff
  --cached --check` reports any marker-like line. `git rm` on a conflicted path deletes the file
  whatever it holds, so "Delete it" (`RemoveConflictedAsync`) saves a D67 snapshot first when the
  file matches none of the path's stages. A file/folder clash is any kind on `<p>~<label>` with
  `<p>` a folder in the index (git also reports it as modify/delete). The merge tool: an unknown
  tool or one that is not installed throws `GitException` (it did not run); a tool that fails
  leaves the file as it was. New member `ReadStagedConflictMarkersAsync`.

### 14.3 Decided while the checks were written (step 8)

The report's writers derived every check with Git 2.36 on replicas of the scenarios and listed what
the design left open; the lead decided (the sections above are corrected where they said otherwise):

- **Rebase (part A).** `{onto}` in the banner is a local branch at `onto` if there is one, else a
  remote branch, else the short SHA. "Check out {A} and rebase it…" items are not disabled while
  HEAD is detached (they check a branch out first, with phase 3's question when leaving commits
  behind); the other rebase items keep 1.1's order, the object's own facts first. A chosen rebase
  pull's success is phase 3's `Pulled and rebased {b} onto {up}`. The app's own dirty-tree refusal of
  "Check out {A} and rebase it" opens "Rebase failed" with 10.1's dirty-tree summary and no git
  output; the status says `Check out {A} and rebase it onto {B} failed`. The backup is saved before
  git runs, so a rebase git refuses leaves one (the clean-up takes it in time). After Continue or Skip
  with the working-changes row selected and the tree clean, nothing is selected (phase 2's rule;
  1.5).
- **The list (part B).** A click or a right-click on a row selects it (a right-click then opens the
  row's menu); a click on a row's action button selects the row and opens its menu, with the menu's
  top-left at the button's bottom-left corner; Shift+F10 and the Menu key open it at the same place.
  While a diff is open over the list, the list's Esc handler declines (the diff's closes it). `{N}
  commits` counts the listed rows, whatever their actions. The action button of a reworded row says
  `Reword`. `Cancel` asks as Esc does. "Changed" means the rows' actions or order differ from the
  list's first state; a list moved back to it is not changed. A click on the base row changes
  nothing. When a diff over the list closes, the focus goes back to the list's selected row.
- **The resolver and the stops (part C).** The banner's buttons say `Continue` and `Skip`. The
  Abort question of a rebase that began with a checkout ends `…before it started, and {branch} is
  checked out again.`; `rebase-merge/visualcommit-return` holds the full ref name and a line feed.
  `Conflict {k} of {m}`: `m` is the number of blocks as the file was read, `k` the block last
  reached; with no block left the toolbar says `No conflicts left`. A choice does not move to the
  next block. A block's bars keep showing its choice after the user types inside the chosen text,
  until an undo brings its markers back; a block whose markers were removed by typing alone shows
  `Not used` on both bars. After `Take ours` or `Take theirs` the bars and tints are as if that side
  had been chosen in every block (11.6). The result tints no chosen line. The squash of selected
  commits selects the squashed commit (3), the one exception to 1.5. The stage panel's message is
  joined as phase 3's commit joins it: the summary alone when the description is blank, else the
  summary, an empty line and the description. The ours pane's common text is the working-tree file's, so it can
  hold lines git merged in cleanly from theirs (11.3). The view model exposes `IsParsePending`, true
  from a typed change until its parse is applied, for the checks to wait on. A merge's or a single
  pick's stage-panel Commit asks the marker question too (it concludes the operation). The result's
  menu has the header `{file} (result)`, `Undo` and `Redo` disabled with `Nothing to undo` and
  `Nothing to redo`, `Cut` and `Copy` disabled with `Select some text first`, `Paste` and `Select
  all` always enabled; a pane's menu has the header of its title and `Copy` likewise. A snapshot a
  card saves is not offered by the stage panel's restore bar (that bar is for discards). A card
  that offers nothing in the toolbar keeps the toolbar row, empty. `ResolverFolder` is empty for a
  file at the root. `{file}` in the questions is the file's name. `Resolved {file}` and `Resolved
  {file} in {tool}` are secondary. The toolbar keeps `Merge tool…`, the file menu `Open in merge
  tool`.
- **Earlier checks.** Phase 3's checks whose menus change also include 5b, 19, 25, 29 and 32; in
  check 12 step 7 the two selected commits form a chain on main's line, so `Squash 2 commits…` is
  enabled there and highlighted first.

### 14.4 What WP4 to WP6 and the lead's hooks settled (merged in `9242ed8`, `2e9b722`, `60cf77a`)

The code's doc comments are the reference; where the build differs from or adds to the text above:

- **WP5** (the list, `2e9b722`): `InteractiveRebaseViewModel(request, list, baseSubject, host)`
  takes the base commit's subject, which `RebaseList` lacks. Squash and Fixup are also disabled on
  a row whose rows below are all dropped (`Every commit below it is dropped: there is nothing to
  join`). A click on a row's action button selects the row and focuses it (the button itself takes
  no focus), and its menu is logged as opened by pointer; `Edit…` takes no focus and leaves the
  selection; `r` on a reworded row does nothing (Edit… changes its message). An edited group
  message is dropped once no squash joins its target. Unfixed by the design and settled so: the
  header and footer on the chrome background with a border; the second line's text at x 120 and
  `Edit…` 12 from the right; `Base` at x 36; the selection colour on the 30-px row only; the count
  hidden while the list shows a message; rows without the theme's focus frame (the selection marks
  the focused row); a drag held within 20 px of the top or bottom scrolls the rows; End brings the
  base row into view. `MessageDialogViewModel` joins the description with line feeds.
- **WP6** (the resolver, `60cf77a`): the result shows the empty line after the file's last line
  feed (AvaloniaEdit's model), so its line count is one more than the file's lines. 11.12's menus
  are built in `ConflictMenus` (`RepositoryMenus` has no partial parts); the header's menu is
  `RepositoryMenus.ForUnstagedFile` through `ConflictResolverViewModel.FileMenu`. A file moved aside
  by a file/folder clash is recognised from the status's paths under `<p>/`. The bars' buttons keep
  the focus (the panes' text areas are no tab stops). With one conflicted file the file arrows are
  disabled; `Delete it` asks nothing; a card's first button takes the focus without an outline.
  While a write runs, the bars, the close button and Esc are disabled, and Undo, Redo and Cut too.
  A refresh closes the resolver for "the operation ended" only when an operation was in progress
  before; with conflicts left by a stash and a modified result it stays (`Marked resolved
  elsewhere`). `Take ours` and `Take theirs` decode the side as UTF-8, else Latin-1; Mark resolved
  writes in the encoding the file was read in. The diff view's `Conflict` body is gone.
- **WP4** (operations, menus, banner, stage panel, `9242ed8`): `ConflictBannerViewModel.Show(state,
  conflicts, ontoName, currentName)`; the banner's layout is `BannerLayoutPanel` (buttons below when
  the text would keep less than 240 px). Several picks and reverts are one call as section 4 says;
  `HasSequencer` is gone. `Operations.CleanUpBackupsAsync()` runs after every operation that saved a
  backup (phase 3's aborts and resets included); the resolver's own snapshots (`Delete it`, a binary
  side) are saved inside the git layer and trigger no clean-up, which the next backup or opening
  makes up for. After its review (`f68ece9`): a list's `Done` and `Total` are read for a sequenced
  list (`git rev-list --count <sequencer/head>..HEAD` and the lines left in `sequencer/todo`), so a
  list's status after Continue or Skip says 10.2's `{K} of {N}`; git keeps no record of a skipped
  commit, so a Skip makes N smaller. The banner's Continue commits the stage panel's message when
  the user changed it, as the panel's own Continue does (`WorkingChangesViewModel.ContinueMessage`;
  an empty summary means the operation's own message). `IGitRepository.ReadRebaseAutoStashAsync`
  is read before "Check out and rebase"'s status read. `OperationContext.SetRunningText` lets an
  operation of several steps name the step it is at ("Checking out {A}…", then "Rebasing {A} onto
  {B}…"). `OperationReport.Warning` is a success in the warning colour, logged `Succeeded` (the
  fork point's text; an autostash that conflicted stays `Conflicts`). The empty pick's dialog ends
  `Skip it to go on.`. "Open in merge tool" on the file the resolver shows runs the resolver's own
  `Merge tool…`, which asks first for a modified result. A menu without an enabled item keeps the
  focus on its presenter and gives it back on Esc (found by check C14).
- **The lead's hooks** (`d7b3c20`, `f184e95`, `51fb51f`): `RepositoryViewModel` implements
  `IInteractiveRebaseHost`; `ShowsGraph`, `ShowsInteractiveRebase` and `ShowsDiff` say what the
  graph area shows; `GraphFocusRequested` (`Graph` or `ContinueFirst`) gives the focus back when the
  list or the resolver closes, and a diff closed over the list gives it to the list. `OpenDiff` of a
  conflicted unstaged file opens the resolver; an open diff whose file became conflicted closes.
  `RepoTabViewModel.ShowsRightPanel`, `ShowsDetailsPanel`, `ShowsStagePanel` and `GraphColumnSpan`
  carry 11.2. `merge.tool` is read after a status read that shows conflicts and kept in
  `RepositoryMenuContext.MergeTool` and the resolver. The background clean-up after the first load
  is the operation `Clean up backups` (section 9 (b)). A confirmation with no question hides its
  question line (the list's `Discard the changes to this list?`).

## 15. Decisions

Recorded in `decisions.md` with their reasons; in short:

| # | Decision | Section |
|---|---|---|
| D139 | The backups' clean-up: a backup is one operation's refs (read with 9's pattern); gone after 30 days or beyond 50 (oldest first); never while any worktree has an operation in progress, never the restore bar's snapshot, never a ref that fits no scheme; at the end of every operation that saved one and in the background when a repository has opened, never failing and never showing auto-fetch's texts; nothing on screen, a log line per backup; discard snapshots named by the backup clock | 9 |
| D140 | The resolver spans the graph and right-panel columns while open (column 2 keeps its width), ours and theirs above the result, the banner across it, files and conflicts navigated from its header and toolbar | 11.1, 11.2 |
| D141 | The interactive rebase's list replaces the graph, newest on top; an action button with a menu per row (and git's letters), drag or Alt+arrows to move; squash and fixup join the nearest Pick or Reword below; new messages through a message dialog; leaving a changed list asks | 2.2 |
| D142 | Rebase: from the commit, branch and drop menus, onto the target's SHA read at the click; a drop's rebase changes the source; a branch that is not checked out is checked out first as a step of its own (after a dirty-tree check), and the app's Abort returns to the branch the user was on; never git's `<branch>` argument; not while detached; uncommitted changes and options left to git's configuration; a plain rebase flattens merges as git does; every rebase saves `rebase-<time>/<b>` and asks nothing | 1 |
| D143 | Interactive rebase: the list holds what git's todo would (a parity test), refused with merges in the range; no `--root`; the todo comes from the app's own sequence editor (a shell function copying prepared files); every group, a reword and a fixup-only group included, ends with a guarded `git commit --amend` that reads a message file next to the todo and runs the user's hooks; other branches are not moved | 2.3 to 2.6, 3 |
| D144 | Continue and Skip per state (5.1); Continue commits with the stage panel's message (in a rebase with the stopped commit's author) before git continues, and asks when markers are staged; the stage panel's button becomes Continue for a list or a rebase; Skip asks and saves `skip-<time>` (amends D83); no Continue where git would silently drop a commit; a stop with nothing to look at opens the error dialog (amends D95); the banner's buttons and layout (amends D122) | 5 |
| D145 | Several picks and reverts: one git call (D136), the outcome read from git's files (4); a list git refused at its start is cleared with `--quit`, which keeps the user's changes | 4 |
| D146 | Pick heads read as files, and through git (`--symbolic-full-name`) only in a reftable repository | 8.1 |
| D147 | Pull with rebase for one pull: the current branch's menu item, `git rebase` without arguments after the fetch, git's configuration not read, dedupe key `pull:<ref>:rebase` (extends D81's list); every pull that rebases saves the backup through a callback and is told stopped by the rebase folder; commits left out by the fork point are reported | 6 |
| D148 | The resolver's content: the working-tree file is the source; the top panes show the file with every block's ours or theirs side; the result is the file, editable, without syntax colours; a whole side is the stage's working-tree form; non-text conflicts resolve at once on a card, keeping the user's edits (`git add` of the file git left, a snapshot before overwriting a binary file); Mark resolved writes and stages, asking when blocks remain or the file changed on disk; staging a file with markers asks wherever it is done; a click on a conflicted file opens the resolver (amends D63); Esc never closes it from inside the result | 11 |
| D149 | The merge tool: `git -c mergetool.keepBackup=false mergetool --tool=<merge.tool> --no-prompt`, for text and binary conflicts only, as a local operation that can be cancelled (amends D81), the files it leaves removed afterwards; disabled without `merge.tool` | 11.9 |
| D150 | Phase 1's checks 13 and 14 clone a bare copy of the 100k-commit repository through `AuthRemoteServer`, held at a fixed percentage, on a second fixed port 47392 (amends D97) | 12.1, 13.2 |
| D151 | Phase 4's walk-through is the project `VisualCommit.VisualTests.Phase4`; `TempRepo` and the tests' isolation set and remove the editor variables; the result's caret does not blink in either harness | 13.2, 13.3 |
| D152 | CI's Test step 24 minutes and the OS jobs 32 (amends D134) | 13.6 |

## 16. What the reviews changed

Four independent reviews of the first draft (`6cf3b64`) were made on 2026-10-12: git and data
safety (with experiments on Git 2.36), the UI and the two harnesses, coverage and consistency, and
the fit with the code. They found 9 blockers, 39 major and 40 minor problems; this revision takes
them in. The main changes:

- **Git**: the sequence editor as a shell function (the chain of `cp`s failed: git appends `"$@"`);
  Start checks that HEAD is still on the listed branch (another branch could have lost its
  commits); the guarded amend runs the user's hooks (git's own reword does) and cannot match on two
  empty strings; every group has the same shape (a fixup-only group lost `#` lines); a group's
  target can be neither skipped nor emptied (its fixups would join the commit before); the stops a
  rebase can make (rescheduled, a failed exec, the app's own amend failing, edit) are told apart;
  the list classification of an empty first revert and of D128's stop; `-c` before `mergetool`;
  "Keep the file" no longer overwrites the user's edits, and a file/folder clash has its own card;
  "Check out and rebase" checks the working tree first and its Abort returns to the user's branch;
  Continue checks for staged markers; the fork point's dropped commits are reported; an autostash
  that conflicts is reported; the clean-up's pattern parses kinds with hyphens.
- **UI and harnesses**: the action box became a button with a menu (a combo box's dropdown is
  outside the headless capture and fought the list's keys); the banner's layout rule; undo, focus,
  caret and Esc made concrete; the double-click guard; the list's geometry, selection and leaving
  rules; the bars' states; automation ids; the generalisation of phase 2's `DiffPane` stated, with
  phase 2's pictures kept byte-identical.
- **Coverage**: the checks now cover the plan's bullets (a squash in the list, picks per block in
  the rebase and the cherry-pick); the scenarios were fixed where a check could not happen (the
  second rebase stop, the binary and add/add conflicts); the amended decisions (D63, D81, D83, D95,
  D97, D122, D134) are named; the list of earlier checks and tests that change is completed; phase
  3's merge texts are kept.
- **Code fit**: the background clean-up never shows auto-fetch's texts; a pull's backup through a
  callback; the result types of lists, Continue and Skip; the interface and constructor changes and
  their test fakes; the work breakdown's ownership, its order and one stub file per package.

One suggestion was not taken: to disable a plain "Rebase onto" whose range holds a merge commit.
Git flattens such a range, and the most common reason to rebase a branch is exactly a branch that
merged its upstream in; refusing it would block that. The backup keeps the old history (1.1).
