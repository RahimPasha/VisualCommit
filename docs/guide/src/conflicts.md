# Resolve conflicts

When a merge, rebase, cherry-pick, revert, pull or stash cannot put two changes to the same lines
together, git stops and leaves the files with conflicts. VisualCommit then shows a banner over the
graph that says what stopped and how many files have conflicts, such as
`Merging: "Merge branch 'fix/rounding'". 1 file has conflicts.` The status bar says so too, and the
conflicted files are listed under **Unstaged files**, marked `U`.

## The conflict resolver

Click a conflicted file to open it in VisualCommit's conflict resolver, in place of the graph.

<img src="pictures/resolver-light.png" srcset="pictures/resolver-light.png 2x" alt="The conflict resolver during a merge of fix/rounding into main: the banner Merging: Merge branch 'fix/rounding'. 1 file has conflicts, with Continue and Abort merge; the file station/units.py, Both modified, File 1 of 1; Conflict 1 of 1 with the buttons Take ours, Take theirs, a dimmed Merge tool… and Mark resolved; the panes Ours: main and Theirs: fix/rounding, each with its version of the function round_reading and the buttons Use ours, Use theirs and Use both; and below them the Result with git's conflict markers and 1 conflict left.">
<img src="pictures/resolver-dark.png" srcset="pictures/resolver-dark.png 2x" alt="The conflict resolver during a merge of fix/rounding into main: the banner Merging: Merge branch 'fix/rounding'. 1 file has conflicts, with Continue and Abort merge; the file station/units.py, Both modified, File 1 of 1; Conflict 1 of 1 with the buttons Take ours, Take theirs, a dimmed Merge tool… and Mark resolved; the panes Ours: main and Theirs: fix/rounding, each with its version of the function round_reading and the buttons Use ours, Use theirs and Use both; and below them the Result with git's conflict markers and 1 conflict left.">

- The left pane, **Ours**, shows your side, the branch you are on; the right pane, **Theirs**, the
  side being brought in. Each conflict has buttons above it: **Use ours** or **Use theirs** puts
  that side's lines in the result, **Use both** puts both.
- **Result** below is the file as it will be saved. You can type in it too. Its header counts the
  conflicts left.
- **Take ours** and **Take theirs** resolve the whole file with one side.
- **Merge tool…** opens the file in the merge tool set in Settings, Git; it is dimmed while none is
  set.
- **Conflict 1 of 2** with its arrows, or Alt+Up and Alt+Down (Option+Up and Option+Down on
  macOS), moves between the conflicts; **File 1 of 3** with its arrows, or Alt+Left and Alt+Right,
  between the conflicted files.
- **Mark resolved** saves the result and stages the file. A file's menu also has
  **Mark as resolved**.

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
lost, but a backup is saved first, so the abort can be undone.

## A stash that did not apply cleanly

When applying a stash stops with conflicts, there is no operation to continue or abort: resolve
the files as above, and the stash is kept until you drop it.
