# Branches

The left panel lists your branches under **Local branches** and each remote's under **Remotes**.
In the graph, a branch's label stands beside the commit it points to. Right-click a branch's row or
label for what you can do with it, or drag it onto another branch for what the two allow
([Merge](merge.md)).

<img src="pictures/branch-menu-light.png" srcset="pictures/branch-menu-light.png 2x" width="548" alt="The menu of the branch feature/forecast in the left panel: Check out feature/forecast, two Merge feature/forecast into main items, Rebase main onto feature/forecast, Interactive rebase of main onto feature/forecast…, a dimmed Fast-forward item, Push feature/forecast to origin/feature/forecast, Force push feature/forecast…, Create branch here…, Create tag here…, Rename feature/forecast…, Delete feature/forecast…, Change upstream…, Stop tracking origin/feature/forecast, Compare feature/forecast with main, Show reflog of feature/forecast, Hide feature/forecast, Solo feature/forecast and Copy branch name.">
<img src="pictures/branch-menu-dark.png" srcset="pictures/branch-menu-dark.png 2x" width="548" alt="The menu of the branch feature/forecast in the left panel: Check out feature/forecast, two Merge feature/forecast into main items, Rebase main onto feature/forecast, Interactive rebase of main onto feature/forecast…, a dimmed Fast-forward item, Push feature/forecast to origin/feature/forecast, Force push feature/forecast…, Create branch here…, Create tag here…, Rename feature/forecast…, Delete feature/forecast…, Change upstream…, Stop tracking origin/feature/forecast, Compare feature/forecast with main, Show reflog of feature/forecast, Hide feature/forecast, Solo feature/forecast and Copy branch name.">

An item that cannot run now is dimmed; point at it to see why.

## Create a branch

- **Branch** in the toolbar, or Ctrl+B (Cmd+B on macOS), creates a branch at HEAD.
- **Create branch here…** in the menu of a commit, a branch or a tag creates one there.

Type the name in the dialog **Create branch**. **Check out the new branch** is ticked to begin
with; untick it to stay on the branch you are on. VisualCommit says at once when a name is not
allowed or is taken.

<img src="pictures/create-branch-light.png" srcset="pictures/create-branch-light.png 2x" width="408" alt="The dialog Create branch, from c8a5135 Show the battery level, with the name feature/alerts typed, Check out the new branch ticked, and the buttons Cancel and Create branch.">
<img src="pictures/create-branch-dark.png" srcset="pictures/create-branch-dark.png 2x" width="408" alt="The dialog Create branch, from c8a5135 Show the battery level, with the name feature/alerts typed, Check out the new branch ticked, and the buttons Cancel and Create branch.">

## Check out a branch

Double-click a branch in the left panel or its label in the graph, or choose **Check out** and its
name in its menu. A remote branch with no local branch of its name gets one that tracks it.

When your uncommitted changes are in the way, the checkout does not start. When they are files you
changed, not untracked files, VisualCommit offers to stash them first: see
[How your work is protected](protection.md).

## Rename or delete a branch

The menu items name the branch, such as **Rename feature/forecast…**.

- **Rename** opens the dialog **Rename branch** with the old name filled in.
- **Delete** asks **Delete branch?** first, and says so when the branch has commits that the branch
  you are on does not have. A backup is saved first, so the deletion can be undone. You cannot
  delete the branch you are on.
- A remote branch's menu has **Delete … on** the remote, such as
  **Delete feature/forecast on origin…**, which deletes it on the remote for everyone who uses it.

## Folders

Branches whose names have a `/` are grouped in folders by the part before it: `feature/forecast`
and `feature/units` sit in the folder `feature`. Click a folder's arrow to open or close it.

## Find a branch

Type in the **Filter** box at the top of the left panel, or press Ctrl+Shift+E (Cmd+Shift+E on
macOS) to go there: the lists keep only the branches, tags and other rows whose names contain the
text. Clear the box to see everything again.

## More in a branch's menu

- **Set upstream…** or **Change upstream…**, and **Stop tracking**: see
  [Fetch, pull and push](sync.md).
- Merge, rebase and fast-forward: see [Merge](merge.md) and [Rebase](rebase.md).
- **Compare** with the branch you are on: see [Compare commits and branches](compare.md).
- **Show reflog of** the branch: see [Undo, redo and the reflog](undo-and-reflog.md).
- **Hide** and **Solo**: see [Read the graph](graph.md).
- **Copy branch name**.
