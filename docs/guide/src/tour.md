# A tour of the window

<img src="pictures/main-window-light.png" srcset="pictures/main-window-light.png 2x" alt="The main window with the weather-station repository open: the tab and toolbar at the top, the left panel with local branches, remotes, tags and the stash, the commit graph in the middle with the commit Show the battery level selected on main, and its commit details on the right.">
<img src="pictures/main-window-dark.png" srcset="pictures/main-window-dark.png 2x" alt="The main window with the weather-station repository open: the tab and toolbar at the top, the left panel with local branches, remotes, tags and the stash, the commit graph in the middle with the commit Show the battery level selected on main, and its commit details on the right.">

From top to bottom and left to right, the window has these parts.

## Tabs

Each tab shows one repository, a workspace's dashboard, or the welcome page. **+** opens a new
tab. The tabs you leave open come back when VisualCommit starts again. See
[Tabs, workspaces and the dashboard](tabs-and-workspaces.md).

## The toolbar

- **Undo** and **Redo** step back and forward through your git actions in this repository
  ([Undo, redo and the reflog](undo-and-reflog.md)). Point at them to see what they would undo or
  redo.
- **Fetch**, **Pull** and **Push** talk to your remotes ([Fetch, pull and push](sync.md)).
- **Branch** creates a branch at HEAD; **Stash** stashes your changes and **Pop** applies the
  newest stash and drops it ([Branches](branches.md), [Stashes and tags](stash-and-tags.md)).
- **Settings** opens the settings screen, **Search** the search bar above the graph, and **Theme**
  switches between the dark and the light theme.

A toolbar button that cannot run now is dimmed; point at it to see why.

## The left panel

The **Filter** box at the top narrows the lists to names that contain what you type. Below it:

- **Local branches**, grouped in folders by the part of their names before a `/`, such as
  `feature` for `feature/forecast`. The branch you are on is highlighted, and a branch that has an
  upstream shows how many commits it is ahead (↑) and behind (↓).
- **Remotes**, each with its branches.
- **Pull requests**, the open pull requests of the repository on GitHub, GitLab (merge requests) or
  Azure DevOps when you are signed in ([Pull requests](pull-requests.md)).
- **Tags** and **Stashes**.

Click a row to select its commit in the graph; double-click a branch to check it out, or a stash
to apply it. Right-click any row for what you can do with it.

## The graph

The middle shows the history, newest first, in the columns **Branch / Tag**, **Graph**, **Message**,
**Author**, **Date** and **SHA**. When the graph is too narrow for them all, as in the picture, the
**SHA** column gives way first, then **Author**, then **Date**. While you have uncommitted changes,
the top row, **Working changes**, stands for them. See [Read the graph](graph.md).

## The right panel

**Commit details** shows the selected commit: its message, **Author**, **Date**, **SHA**,
**Parents** (click one to go to it) and its **Changed files**, as a **Flat** list or a **Tree** of
folders; **All files** shows every file of the commit. Click a file to see its diff in the middle.
When the **Working changes** row is selected, this panel shows your changes and the commit message
boxes instead ([Stage and commit](stage-and-commit.md)).

## The status bar

From the left: the branch you are on, what VisualCommit is doing or did last (such as `Ready`), the
Git in use ([The first start](first-run.md)) and the **Activity log** button
([The activity log](activity-log.md)). While an operation that talks to a remote runs, a
**Cancel** button stops it.

## Resizing

Drag the dividers between the panels to change their widths. The widths are kept.
