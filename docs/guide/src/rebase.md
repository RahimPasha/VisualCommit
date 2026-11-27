# Rebase

A rebase replays a branch's commits on top of another commit, so that its history reads as if it
had started there. It rewrites those commits: if they were pushed, the branch then needs a force
push ([Fetch, pull and push](sync.md)). VisualCommit saves a backup before every rebase, so
**Undo** can take it back.

## Rebase onto a branch or a commit

- Right-click a branch: **Rebase** *your branch* **onto** *that branch*, such as
  `Rebase main onto feature/forecast`.
- Right-click a commit: **Rebase** *your branch* **onto this commit**.
- Drag a branch onto another: **Check out** the dragged branch **and rebase it onto** the target,
  or, when the dragged branch is the one you are on, **Rebase** it **onto** the target
  ([Merge](merge.md) shows the drag).

## Pull with rebase

The branch you are on has **Pull from** its upstream **(rebase)** in its menu, such as
`Pull from origin/main (rebase)`: it fetches and rebases your commits onto the upstream for this
one pull. To pull with a rebase every time, set Settings, Git, **Pull** to **Rebase**
([Settings](settings.md)).

## The interactive list

An interactive rebase lets you reorder, reword, squash, fix up and drop commits before they are
replayed. Open it with:

- **Interactive rebase of** *your branch* **from here…** in the menu of a commit of your branch:
  the list holds the commits after it;
- **Interactive rebase of** *your branch* **onto** *a branch…* in another branch's menu;
- **Check out** a branch **and rebase it onto** another **interactively…** after a drag.

The list takes the graph's place. It is headed with the branch and its base, such as
`Interactive rebase of main from 9ca23e8`, newest commit first, with the base at the bottom.

<img src="pictures/interactive-list-light.png" srcset="pictures/interactive-list-light.png 2x" width="440" alt="The interactive list headed Interactive rebase of main from 9ca23e8, 4 commits: four rows each with a drag handle, the action Pick, its short SHA and its subject, from Show the battery level, which is selected, down to Tidy the display code, then the Base row 9ca23e8 Cache sensor readings, and the buttons Cancel and a dimmed Start rebase.">
<img src="pictures/interactive-list-dark.png" srcset="pictures/interactive-list-dark.png 2x" width="440" alt="The interactive list headed Interactive rebase of main from 9ca23e8, 4 commits: four rows each with a drag handle, the action Pick, its short SHA and its subject, from Show the battery level, which is selected, down to Tidy the display code, then the Base row 9ca23e8 Cache sensor readings, and the buttons Cancel and a dimmed Start rebase.">

For each commit:

- Choose its action in its box: **Pick** keeps it, **Reword** keeps it with a new message, which
  VisualCommit asks for at once (**Edit…** on its row changes it later; **Cancel** in that dialog
  keeps the action as it was), **Squash** joins it to the commit below and joins their
  messages, **Fixup** joins it to the commit below and keeps that commit's message, **Drop** leaves
  it out.
- Drag it by its handle to another place, or select it and press Alt+Up or Alt+Down (Option+Up or
  Option+Down on macOS).
- With a row selected, the keys P, R, S, F and D set its action.

**Start rebase** stays dimmed until you change an action or the order. **Cancel** closes the list;
if you changed it, VisualCommit asks **Discard the changes to this list?** first.

To squash commits that follow one another without opening the list, select them in the graph
(Shift-click) and choose **Squash** with their count, such as `Squash 3 commits…`.

## When a rebase stops

A rebase stops when a commit conflicts with what came before it. A banner over the graph says so,
with:

- **Continue** once the conflicts are resolved and staged; the commit button reads
  **Continue rebase** too;
- **Skip** to leave out the commit it stopped on;
- **Abort rebase** to put the branch back where it was before the rebase. It asks first, and saves
  a backup.

See [Resolve conflicts](conflicts.md).
