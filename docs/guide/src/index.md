# VisualCommit user guide

VisualCommit is a desktop Git client for Windows, macOS and Linux. It shows a repository's
history as a graph of commits, branches, tags and stashes, and lets you stage, commit, branch,
merge, rebase, resolve conflicts, fetch, pull and push with clicks, right-clicks, double-clicks
and drag and drop. It drives the real `git` program installed on your computer, so everything it
does is what git itself does, and a terminal sees the same repository.

<img src="pictures/main-window-light.png" srcset="pictures/main-window-light.png 2x" alt="The main window with the weather-station repository open: the tab and toolbar at the top, the left panel with local branches, remotes, tags and the stash, the commit graph in the middle with the commit Show the battery level selected on main, and its commit details on the right.">
<img src="pictures/main-window-dark.png" srcset="pictures/main-window-dark.png 2x" alt="The main window with the weather-station repository open: the tab and toolbar at the top, the left panel with local branches, remotes, tags and the stash, the commit graph in the middle with the commit Show the battery level selected on main, and its commit details on the right.">

What it does:

- Opens, clones and creates repositories in tabs that come back after a restart.
- Draws the commit graph with every branch, tag and stash, and loads 100,000 commits in a few
  seconds.
- Stages and commits whole files, single hunks or chosen lines.
- Runs checkouts, merges, rebases (also interactive), cherry-picks, reverts, resets, stashes and
  tags from menus, from drag and drop and from the toolbar.
- Resolves conflicts in its own resolver or in your merge tool.
- Shows a file's history and blame, the reflog, and an activity log of every git command it ran.
- Searches commits by message, author, SHA, file or the text of their changes, and compares two
  commits or branches.
- Undoes and redoes its git actions, and refuses an action that would overwrite your uncommitted
  work or ignored files.
- Groups repositories into workspaces with a dashboard that fetches or pulls them all at once.
- With a GitHub, GitLab or Azure DevOps account, lists and creates pull requests (merge requests on
  GitLab) and clones your repositories.
- Has a command palette and a fixed set of keyboard shortcuts for every main action, and dark and
  light themes.

VisualCommit is free and open source, under the MIT License. It sends no telemetry and needs no
account: signing in to a hosting service only adds that service's features. Its source code, its
license and the notes on how it is shared are in the
[repository's README](https://github.com/RahimPasha/VisualCommit/blob/master/README.md).

## Where to start

- [Install VisualCommit](install.md), then read [the first start](first-run.md): VisualCommit
  needs Git 2.34.1 or newer.
- [Open, clone or create a repository](open-clone-create.md) and take
  [a tour of the window](tour.md).
- Press F1 in VisualCommit at any time to open this guide.
