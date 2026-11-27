# Resolve conflicts

When a merge, rebase, cherry-pick, revert, pull or stash cannot put two changes to the same lines
together, git stops and leaves the files with conflicts. VisualCommit then shows a banner over the
graph that says what stopped and how many files have conflicts, such as
`Merging: "Merge branch 'fix/rounding'". 1 file has conflicts.` When the operation ran in
VisualCommit, the status bar says so too, such as `Merge stopped: 1 file has conflicts`. The
conflicted files are listed under **Unstaged files**, marked `U`.

## The conflict resolver

Click a conflicted file to open it in VisualCommit's conflict resolver, in place of the graph.

<img src="pictures/resolver-light.png" srcset="pictures/resolver-light.png 2x" width="1100" alt="The conflict resolver during a merge of fix/rounding into main: the banner Merging: Merge branch 'fix/rounding'. 1 file has conflicts, with a dimmed Continue and the button Abort merge; the file units.py in station, Both modified, File 1 of 1; Conflict 1 of 1 with the buttons Take ours, Take theirs, a dimmed Merge tool… and Mark resolved; the panes Ours: main and Theirs: fix/rounding, each with its version of the function round_reading, with the buttons Use ours and Use both in the Ours pane and Use theirs and Use both in the Theirs pane; and below them the Result with git's conflict markers and 1 conflict left.">
<img src="pictures/resolver-dark.png" srcset="pictures/resolver-dark.png 2x" width="1100" alt="The conflict resolver during a merge of fix/rounding into main: the banner Merging: Merge branch 'fix/rounding'. 1 file has conflicts, with a dimmed Continue and the button Abort merge; the file units.py in station, Both modified, File 1 of 1; Conflict 1 of 1 with the buttons Take ours, Take theirs, a dimmed Merge tool… and Mark resolved; the panes Ours: main and Theirs: fix/rounding, each with its version of the function round_reading, with the buttons Use ours and Use both in the Ours pane and Use theirs and Use both in the Theirs pane; and below them the Result with git's conflict markers and 1 conflict left.">

- The left pane, **Ours**, shows the version already in place; the right pane, **Theirs**, the
  change being brought in. During a merge, a cherry-pick, a revert or a stash, **Ours** is the
  branch you are on. During a rebase, including a pull with rebase, the sides are the other way
  around: **Ours** is what your commits are being replayed onto, marked `(the new base)`, and
  **Theirs** is your own commit, marked `(your commit)`. Each conflict has buttons above it:
  **Use ours** or **Use theirs** puts that side's lines in the result; **Use both** puts both: the
  one in the **Ours** pane puts ours first, the one in the **Theirs** pane puts theirs first.
- **Result** below is the file as it will be saved. You can type in it too. Its header counts the
  conflicts left.
- **Take ours** and **Take theirs** replace the whole result with one side's version of the file;
  the file is resolved only when you press **Mark resolved**.
- **Merge tool…** opens the file in the merge tool set in Settings, Git; it is dimmed while none is
  set.
- **Conflict 1 of 2** with its arrows, or Alt+Up and Alt+Down (Option+Up and Option+Down on
  macOS), moves between the conflicts; **File 1 of 3** with its arrows, or Alt+Left and Alt+Right
  (Option+Left and Option+Right on macOS), between the conflicted files.
- **Mark resolved** saves the result and stages the file. A file's menu also has
  **Mark as resolved**.

A conflict that is not about lines of text, such as a file one side changed and the other deleted,
or a binary file, shows a card in place of the panes. The card says what happened and has buttons
that resolve the file at once, such as **Keep the file** and **Delete it**, or **Use ours** and
**Use theirs**. For a submodule, the card says to resolve it in a terminal.

Close the resolver with the **×** at its right, or with Esc while the focus is not in the result;
it asks first when the result has changes you have not saved.

## Continue

When every conflicted file is resolved, press **Continue** on the banner, or the commit button in
the **Working changes** panel. For a merge, that button commits the merge, with the merge's message
filled in, which you can change. During a rebase or a list of picks or reverts, it reads
**Continue rebase**, **Continue cherry-pick** or **Continue revert**.

## Abort

The banner's **Abort** button, such as **Abort merge** or **Abort rebase**, puts everything back
as it was before the operation started. It asks first; changes you made to resolve conflicts are
lost, but a backup is saved first. **Undo** does not take an abort back: to get those changes back,
choose **Restore the changes…** on the abort's backup in the backups list
([Undo, redo and the reflog](undo-and-reflog.md)). The stopped operation itself does not come back.

## A stash that did not apply cleanly

When applying a stash stops with conflicts, there is no operation to continue or abort: resolve
the files as above, and the stash is kept until you drop it.
