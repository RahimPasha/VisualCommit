# Cherry-pick, revert and reset

All three start from a commit's menu: right-click a commit's row in the graph.

<img src="pictures/commit-menu-light.png" srcset="pictures/commit-menu-light.png 2x" alt="The menu of the commit 25aded4 Round to the configured digits: Check out this commit, Create branch here…, Create tag here…, a dimmed Cherry-pick onto main, Revert this commit, a dimmed Rebase main onto this commit, Interactive rebase of main from here…, Reset main to this commit… and Copy SHA.">
<img src="pictures/commit-menu-dark.png" srcset="pictures/commit-menu-dark.png 2x" alt="The menu of the commit 25aded4 Round to the configured digits: Check out this commit, Create branch here…, Create tag here…, a dimmed Cherry-pick onto main, Revert this commit, a dimmed Rebase main onto this commit, Interactive rebase of main from here…, Reset main to this commit… and Copy SHA.">

An item that cannot run is dimmed; point at it to see why. In the picture, the commit is already on
`main`, so it cannot be cherry-picked onto it.

## Cherry-pick

**Cherry-pick onto** *your branch* copies the commit's change onto the branch you are on as a new
commit. To pick several, select them in the graph (Ctrl-click or Shift-click; Cmd-click on macOS)
and choose **Cherry-pick** with their count, such as `Cherry-pick 3 commits onto main`. Merge
commits and stashes cannot be cherry-picked here.

## Revert

**Revert this commit** makes a new commit that undoes the commit's change, and keeps the history as
it is, which is the safe way to undo a commit that was pushed. A merge commit's menu has
**Revert this merge commit**. Several selected commits can be reverted together, but merge commits
one at a time.

A cherry-pick or a revert that stops with conflicts shows a banner over the graph, with
**Continue**, **Skip** to leave out the commit it stopped on, and **Abort cherry-pick** or
**Abort revert**; see [Resolve conflicts](conflicts.md).

## Reset

**Reset** *your branch* **to this commit…** moves the branch you are on to the commit. The question
**Reset** *branch* **to** *commit*? asks how:

<img src="pictures/reset-question-light.png" srcset="pictures/reset-question-light.png 2x" alt="The question Reset main to 25aded4?, Move main to Round to the configured digits (25aded4), with three choices: Soft, Keep every change; the changes of the commits after it are staged. Mixed, selected, Keep every change; nothing is staged. Hard, in red, Discard every change to tracked files. Below: A backup is saved first, so this can be undone, and the buttons Cancel and Reset.">
<img src="pictures/reset-question-dark.png" srcset="pictures/reset-question-dark.png 2x" alt="The question Reset main to 25aded4?, Move main to Round to the configured digits (25aded4), with three choices: Soft, Keep every change; the changes of the commits after it are staged. Mixed, selected, Keep every change; nothing is staged. Hard, in red, Discard every change to tracked files. Below: A backup is saved first, so this can be undone, and the buttons Cancel and Reset.">

- **Soft**: keep every change; the changes of the commits after it are staged.
- **Mixed**: keep every change; nothing is staged.
- **Hard**: discard every change to tracked files.

A backup is saved first, so even a hard reset can be undone with **Undo**, or restored from the
backups ([Undo, redo and the reflog](undo-and-reflog.md)).

## Undo

Each of these is undone by **Undo** in the toolbar, Ctrl+Z (Cmd+Z on macOS), right after it.
