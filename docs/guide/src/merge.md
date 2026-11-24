# Merge

A merge brings another branch's commits into the branch you are on.

## From a branch's menu

Right-click the branch you want to bring in, in the left panel or in the graph:

- **Merge** *branch* **into** *your branch*, such as `Merge feature/forecast into main`, lets git
  fast-forward your branch when it can, and makes a merge commit otherwise.
- The same item with **(create a merge commit)** always makes a merge commit, even when a
  fast-forward is possible.

A merge that stops with conflicts shows a banner over the graph; see
[Resolve conflicts](conflicts.md).

## Fast-forward

A fast-forward moves a branch ahead to a commit that already contains it, without a merge commit.

- **Fast-forward** in a branch's menu moves a branch that is behind its upstream up to it, such as
  `Fast-forward feature/forecast to origin/feature/forecast`, without checking it out. It is dimmed
  when the branch is up to date or has commits of its own.
- When you drag a branch onto another (below), **Fast-forward** moves the target to the dragged
  branch, when the target has no commits of its own.

## Drag a branch onto another

Drag a branch's label in the graph, or its row in the left panel, and drop it onto another
branch. A menu headed with both names, such as `fix/rounding → main`, offers what can be done with
the two:

<img src="pictures/drag-menu-light.png" srcset="pictures/drag-menu-light.png 2x" alt="The menu after dropping fix/rounding onto main, headed fix/rounding → main: Merge fix/rounding into main, Merge fix/rounding into main (create a merge commit), a dimmed Fast-forward main to fix/rounding, Check out fix/rounding and rebase it onto main, Check out fix/rounding and rebase it onto main interactively…, and Compare fix/rounding with main.">
<img src="pictures/drag-menu-dark.png" srcset="pictures/drag-menu-dark.png 2x" alt="The menu after dropping fix/rounding onto main, headed fix/rounding → main: Merge fix/rounding into main, Merge fix/rounding into main (create a merge commit), a dimmed Fast-forward main to fix/rounding, Check out fix/rounding and rebase it onto main, Check out fix/rounding and rebase it onto main interactively…, and Compare fix/rounding with main.">

- The two **Merge** items merge the dragged branch into the target when the target is the branch
  you are on. When it is not, **Check out** the target **and merge** the dragged branch **into it**
  does both.
- **Fast-forward** moves the target to the dragged branch.
- The rebase items rebase the dragged branch onto the target ([Rebase](rebase.md)).
- **Compare** shows what the two differ in ([Compare commits and branches](compare.md)).
- Dropped onto a remote branch, the menu offers to push the branch there.

Press Esc, or click elsewhere, to close the menu without doing anything.

## Undo a merge

**Undo** in the toolbar takes a merge back right after it; see
[Undo, redo and the reflog](undo-and-reflog.md). To undo a merge that was already pushed, revert
its merge commit instead ([Cherry-pick, revert and reset](pick-revert-reset.md)).
