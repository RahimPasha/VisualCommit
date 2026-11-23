# Read the graph

The graph shows the repository's commits, one row each, the newest at the top. Its columns are
**Branch / Tag**, **Graph**, **Message** (the commit's summary), **Author**, **Date** and **SHA**. When
the graph is narrow, the **SHA** column gives way first, then **Author**, then **Date**, so that the
message keeps its room.

<img src="pictures/graph-labels-light.png" srcset="pictures/graph-labels-light.png 2x" alt="The graph with its labels: stash@{0} beside the top stash row, main with a check mark on the selected commit Show the battery level, origin/main outlined on Add a French readme, fix/rounding, feature/forecast with a +1 label, v1.1 with a tag sign, and feature/units with a +1 label, each on its commit, and the colored lines of the branches joining at their merges.">
<img src="pictures/graph-labels-dark.png" srcset="pictures/graph-labels-dark.png 2x" alt="The graph with its labels: stash@{0} beside the top stash row, main with a check mark on the selected commit Show the battery level, origin/main outlined on Add a French readme, fix/rounding, feature/forecast with a +1 label, v1.1 with a tag sign, and feature/units with a +1 label, each on its commit, and the colored lines of the branches joining at their merges.">

## The labels

- The branch you are on has a check mark: `✓ main`.
- Other local branches are filled with their line's color, such as `fix/rounding`.
- Remote branches are outlined, such as `origin/main`.
- Tags have a tag sign, such as `v1.1`.
- A stash has its name, such as `stash@{0}`.
- A branch with an open pull request carries its number: `#12` on GitHub, `!12` on GitLab, where
  it is a merge request, and on Azure DevOps ([Pull requests](pull-requests.md)).
- When a commit has more labels than fit, a label such as `+1` stands for the rest: point at it to
  see them all.

Double-click a branch's label to check the branch out. Right-click a label for its menu; drag a
branch's label onto another branch for the menu of the two ([Merge](merge.md)).

## The lines

Each branch's history is a line of its own color, and a dot on it is a commit. Lines join where a
branch was merged and part where one was started. While you have uncommitted changes, the top row,
**Working changes**, is a hollow circle above the commit you are on, joined to it by a dotted line,
and stands for them. A stash is a hollow circle joined by a dotted line to the commit it was taken on.

## Select commits

- Click a row to select its commit: the right panel shows its details.
- Ctrl-click (Cmd-click on macOS) adds a commit to the selection or takes it out.
- Shift-click selects every commit from the selected one to the clicked one.
- With the keyboard, the arrows, PageUp, PageDown, Home and End move the selection; with Shift they
  extend it. Commits that are not next to each other can be selected only with the mouse.

With two commits selected, the right panel compares them
([Compare commits and branches](compare.md)). With more, it says
"Right-click a selected commit for what can be done with all of them.": cherry-pick, revert,
squash, or copy their SHAs.

## Hide and solo branches

A repository with many branches is easier to read with some of them out of the way:

- **Hide** and a branch's name, in its menu, takes the branch out of the graph, and for a local
  branch the remote branch it tracks too: their commits that no shown branch or tag reaches are no
  longer drawn. The branch you are on is always shown. A remote's menu has **Hide every branch of**
  the remote.
- **Solo** and a branch's name shows only that branch (for a local branch, with the remote branch
  it tracks), any others you solo, and the branch you are on.
- In the left panel, a hidden or soloed branch is marked; click the mark to show it again or to
  stop soloing it. The menus have **Show** or **Stop soloing** and the branch's name, and the
  **Local branches** and **Remotes** sections' menus have **Show all hidden branches** and
  **Stop soloing**.

What is hidden or soloed is kept for each repository and comes back when you open it again.
