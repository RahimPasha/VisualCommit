# Fetch, pull and push

## Ahead and behind

A local branch that tracks a remote branch, its upstream, shows in the left panel how many commits
it has that the upstream lacks (↑, ahead) and how many the upstream has that it lacks (↓, behind),
as of the last fetch.

<img src="pictures/ahead-behind-light.png" srcset="pictures/ahead-behind-light.png 2x" alt="The left panel's Local branches with the folders feature and fix and the branch main marked ↑1 ↓1, and Remotes with origin, its folder feature and its branch main.">
<img src="pictures/ahead-behind-dark.png" srcset="pictures/ahead-behind-dark.png 2x" alt="The left panel's Local branches with the folders feature and fix and the branch main marked ↑1 ↓1, and Remotes with origin, its folder feature and its branch main.">

## Fetch

**Fetch** in the toolbar, or Ctrl+Shift+F (Cmd+Shift+F on macOS), fetches from every remote. A
remote's menu in the left panel has **Fetch** with its name, for that remote alone. Fetching
changes none of your branches: it brings the remotes' new commits and updates the remote branches.

### Fetch in the background

VisualCommit fetches the active tab's repository in the background every 5 minutes. Change the
interval, from 1 to 60 minutes, or turn it off, in Settings, General, **Fetching**. A background
fetch never asks for a password.

## Pull

**Pull** in the toolbar, or Ctrl+Shift+L (Cmd+Shift+L on macOS), fetches the branch you are on
from its upstream and brings its new commits into your branch. How it brings them in follows git's
own pull setting, which you can set in Settings, Git, **Pull**:
**Merge (fast-forward when possible)**, **Merge, always with a merge commit**, **Rebase**,
**Rebase, keeping merge commits** or **Fast-forward only**. With nothing set, VisualCommit merges.
The current branch's menu has **Pull from** its upstream, and **Pull from** its upstream
**(rebase)**, such as `Pull from origin/main (rebase)`, which rebases for this one pull; see
[Rebase](rebase.md).

Another branch that is behind its upstream can be brought up to date without checking it out:
**Fast-forward** in its menu moves it to its upstream when that is possible.

## Push

**Push** in the toolbar, or Ctrl+Shift+K (Cmd+Shift+K on macOS), pushes the branch you are on to
its upstream. A branch with no upstream opens the dialog **Push** and the branch's name: choose the
remote under **To**, the **Branch on the remote**, and whether to **Set as upstream**.

If the remote has commits your branch does not have, the push is refused with
**Push rejected**: pull first, then push again. The dialog's **Pull** button does that.

You can also drag a local branch onto a remote branch: the menu offers to push it there, and to
push and track it.

### Force push, safely

After you rewrite commits that were pushed (an amend, a rebase, a reset), a plain push is refused.
**Force push** in the branch's menu asks first, saves a backup, and replaces the remote branch with
yours. It stops, with **Force push stopped**, if the remote has commits that you have not brought
into your branch, even ones the last fetch brought, so it never drops someone else's work
unseen.

## Upstreams

- **Set upstream…** in a branch's menu chooses the remote branch it tracks; **Change upstream…**
  changes it.
- **Stop tracking** and the upstream's name removes it.
- A push with **Set as upstream** ticked sets it too.

## Remotes

**Manage remotes…**, in a remote's menu, in the **Remotes** section's menu and in the menu of the
graph's empty area, opens the dialog **Remotes**: **Edit** a remote's name or URL, **Remove** it,
or **Add remote** with a name and a URL. When adding, **Fetch now** fetches it at once.

<img src="pictures/manage-remotes-light.png" srcset="pictures/manage-remotes-light.png 2x" alt="The dialog Remotes with the remote origin at https://github.com/demo--alex/weather-station…, its buttons Edit and Remove, the button Add remote and the button Close.">
<img src="pictures/manage-remotes-dark.png" srcset="pictures/manage-remotes-dark.png 2x" alt="The dialog Remotes with the remote origin at https://github.com/demo--alex/weather-station…, its buttons Edit and Remove, the button Add remote and the button Close.">

Removing a remote asks first, keeps your local branches, and saves a backup.

## Cancel

While a fetch, pull or push runs, the status bar shows a **Cancel** button that stops it.
The command palette also lists **Cancel** and the operation's name, such as
`Cancel Fetch origin`, while the operation can be canceled. Afterwards the status bar reads, for
example, `Fetch origin cancelled`.
