# Undo, redo and the reflog

## Undo and redo

**Undo** in the toolbar, or Ctrl+Z (Cmd+Z on macOS), takes back the last action in this repository
that VisualCommit records (they are listed below; staging, unstaging and fetches are not among
them); **Redo**, or Ctrl+Y (Cmd+Shift+Z on macOS), makes it again. While the focus is in a text box,
such as the commit message, these keys undo and redo your typing instead. Point at **Undo** to see
what it would undo, such as `Undo commit "Fix the parser"`. Afterwards the status bar says what
happened, such as `Undid creation of try/larger-font`.

<img src="pictures/undo-done-light.png" srcset="pictures/undo-done-light.png 2x" alt="The window after an undo: the status bar reads Undid creation of try/larger-font, the toolbar's Undo is dimmed and its Redo is enabled, and the graph shows main without the branch that was undone.">
<img src="pictures/undo-done-dark.png" srcset="pictures/undo-done-dark.png 2x" alt="The window after an undo: the status bar reads Undid creation of try/larger-font, the toolbar's Undo is dimmed and its Redo is enabled, and the graph shows main without the branch that was undone.">

Undo reaches many steps back: up to the last 100 actions in each repository since VisualCommit
started. They include commits and amends, checkouts, branches created, renamed or deleted, merges,
rebases, squashes, cherry-picks, reverts, resets, pulls, stashes, tags, discarded changes, ignored
files, remotes and upstreams, and pushes. The list is not kept when VisualCommit closes.

- An action that changed a remote, such as a push, asks first before it is undone or redone,
  since others may already have fetched it.
- When the repository has changed since the action in a way the undo would overwrite, the undo is
  refused and says why, such as `Can't undo merge of fix/rounding into main: 1 conflict prevents
  it. Commit, stash, or discard your changes first.` Nothing is changed then.
- While an operation is in progress or conflicts are unresolved, Undo and Redo are dimmed: finish
  or abort it first. Point at them to see why.

## Backups

Before anything that could lose work (deleting a branch or a tag, a reset, a discard, dropping a
stash, a rebase, a force push, an abort), VisualCommit saves a backup in the repository, under
`refs/visualcommit/backup/`. A backup is deleted when it is older than 30 days, or when there are
more than 50 (the oldest go first).

To see them, choose **Show backups** in the menu of the **Working changes** row, of the graph's
empty area or of the **Local branches** section, or in the command palette. Each backup's menu offers what brings it back, such as **Restore branch** and its name,
**Restore stash**, **Restore the discarded files…**, **Restore the changes…** or
**Push** a branch **back to** its remote. Backups also cover actions from earlier sessions of
VisualCommit, which Undo does not reach.

## The reflog

Git keeps a log of where HEAD and each branch pointed, the reflog. **Show reflog**, in the menu of
the **Working changes** row, of the graph's empty area or of the **Local branches** section, in the
command palette, or Ctrl+Shift+R (Cmd+Shift+R on macOS), opens it in place of the graph; a local
branch's menu has **Show reflog of** the branch. The box at the top left switches between
**HEAD**, each local branch and **Backups**.

<img src="pictures/reflog-light.png" srcset="pictures/reflog-light.png 2x" alt="The reflog of HEAD: rows from HEAD@{0} down to HEAD@{20}, each with its date, what moved HEAD, such as reset: moving to HEAD, commit: Show the battery level or checkout: moving from fix/rounding to main, the commit's short SHA and its subject; the source box HEAD at the top left and a close button at the top right.">
<img src="pictures/reflog-dark.png" srcset="pictures/reflog-dark.png 2x" alt="The reflog of HEAD: rows from HEAD@{0} down to HEAD@{20}, each with its date, what moved HEAD, such as reset: moving to HEAD, commit: Show the battery level or checkout: moving from fix/rounding to main, the commit's short SHA and its subject; the source box HEAD at the top left and a close button at the top right.">

Each row says what moved HEAD or the branch, to which commit. Right-click a row to bring a commit
back: **Create branch here…**, **Check out this commit**, **Reset** your branch **to this
commit…** or **Cherry-pick onto** your branch. Click a row to see its commit in the right panel.
Double-click a row, or choose **Show in graph**, to see its commit in the graph; a commit that no
branch, tag or remote branch reaches any more is not in the graph, and its row has a warning sign.
Close the page with Esc or its **×**.
