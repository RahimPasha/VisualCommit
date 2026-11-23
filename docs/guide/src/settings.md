# Settings

Open the settings screen with **Settings** in the toolbar, or Ctrl+, (Cmd+, on macOS). It has six
sections: **General**, **Git**, **Tools**, **Keyboard shortcuts**, **Accounts** and **About**.
**Save** keeps your changes; **Cancel** drops them.

## General

<img src="pictures/settings-general-light.png" srcset="pictures/settings-general-light.png 2x" alt="The settings screen's General section: Theme with Dark, Light (chosen) and System; Fetching with Fetch in the background every 5 minutes ticked; Working tree with Keep ignored files and Include untracked files when stashing ticked; Opening files with the extensions Open file asks about first and the box Also ask for.">
<img src="pictures/settings-general-dark.png" srcset="pictures/settings-general-dark.png 2x" alt="The settings screen's General section: Theme with Dark, Light (chosen) and System; Fetching with Fetch in the background every 5 minutes ticked; Working tree with Keep ignored files and Include untracked files when stashing ticked; Opening files with the extensions Open file asks about first and the box Also ask for.">

- **Theme**: **Dark**, **Light**, or **System**, which follows your operating system's light or
  dark mode. The toolbar's **Theme** button switches between dark and light at once.
- **Fetching**: **Fetch in the background every** 1 to 60 **minutes**, the active tab's repository
  only; it never asks for a password. See [Fetch, pull and push](sync.md).
- **Working tree**: **Keep ignored files** ([How your work is protected](protection.md)) and
  **Include untracked files when stashing**, which the dialog **Stash changes** starts with.
- **Opening files**: **Open file** asks first before it opens a file that can run programs, such as
  an `.exe` or a script. The section lists those extensions, and **Also ask for** adds your own,
  such as `.py .pl`. On Linux and macOS it also asks before it opens a file marked as a program.

## Git

These are git's own settings, saved in git's configuration, so git in a terminal uses them too.
**All repositories (global)** saves them in your global git configuration; **This repository**, on
a repository's tab, in that repository's `.git/config`, and they win over the global ones.

<img src="pictures/settings-git-light.png" srcset="pictures/settings-git-light.png 2x" alt="The settings screen's Git section on All repositories (global): Name and Email Not set, each with a note that weather-station sets its own; Pull with Not set chosen among six choices; Merge tool Not set with Choose…; and Git program C:\Program Files\Git\cmd\git.exe, Found on the PATH, with Browse….">
<img src="pictures/settings-git-dark.png" srcset="pictures/settings-git-dark.png 2x" alt="The settings screen's Git section on All repositories (global): Name and Email Not set, each with a note that weather-station sets its own; Pull with Not set chosen among six choices; Merge tool Not set with Choose…; and Git program C:\Program Files\Git\cmd\git.exe, Found on the PATH, with Browse….">

- **Name** and **Email**: who your commits are made by. A note says when the open repository sets
  its own, or when git could not make commits as things stand.
- **Pull**: how Pull brings in new commits; see [Fetch, pull and push](sync.md).
- **Merge tool**: **Choose…** picks one of the merge tools git knows, found on this computer or
  not. **Merge tool…** in the conflict resolver uses it, and so does `git mergetool` in a terminal.
- **Git program**: the Git VisualCommit uses, and how it was found, such as `Found on the PATH.`
  **Browse…** chooses another; VisualCommit checks it first. While Git works, a Git you choose here
  is used from VisualCommit's next start, and the row says "VisualCommit uses it from its next
  start." When you have chosen one, a button such as **Use the Git on the PATH** removes your
  choice. See [The first start](first-run.md).

## Tools

**Editor**: choose an editor, and the file menus gain **Open in editor** beside **Open file**. Pick
one VisualCommit found, or **Other** with its **Program** and **Arguments**, where `{path}` stands
for the file's full path.

## Keyboard shortcuts

Every key VisualCommit knows, grouped as in [Keyboard shortcuts](reference/shortcuts.md). The keys
are fixed.

## Accounts and About

**Accounts**: see [Sign in to GitHub, GitLab or Azure DevOps](accounts.md). **About**: see
[About VisualCommit](reference/about.md).

## The command palette

The command palette reaches every command, and every branch, tag, stash, remote, repository and
workspace, by typing. Open it with Ctrl+Shift+P (Cmd+Shift+P on macOS), type part of a name, move
with the arrows and press Enter. A command's key, if it has one, stands at its right, and its
detail, such as the branch it acts on, beside its name.

<img src="pictures/palette-light.png" srcset="pictures/palette-light.png 2x" alt="The command palette with branch typed: Create branch… with Ctrl+B, Filter branches and tags with Ctrl+Shift+E, then Create branch here… and Copy branch name for main, feature/forecast, feature/units, fix/rounding and origin/feature/forecast.">
<img src="pictures/palette-dark.png" srcset="pictures/palette-dark.png 2x" alt="The command palette with branch typed: Create branch… with Ctrl+B, Filter branches and tags with Ctrl+Shift+E, then Create branch here… and Copy branch name for main, feature/forecast, feature/units, fix/rounding and origin/feature/forecast.">

It also has commands with no key, such as **Use the dark theme**, **Use the light theme**,
**Follow the system's theme**, **Show backups**, **Manage remotes…** and **About VisualCommit…**,
and **Open the user guide**, whose key is F1. Esc closes it.
