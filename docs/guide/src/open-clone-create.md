# Open, clone or create a repository

A tab with no repository shows the welcome page: **No repository open**, with the buttons
**Open**, **Clone** and **Init**, your workspaces, and your recent repositories. Press
Ctrl+T (Cmd+T on macOS) for a new tab, which starts on the welcome page.

<img src="pictures/welcome-page-light.png" srcset="pictures/welcome-page-light.png 2x" alt="The welcome page: No repository open, the buttons Open, Clone and Init, the Workspaces heading with New workspace… and No workspaces, and Recent repositories listing weather-station and recipe-box with their folders.">
<img src="pictures/welcome-page-dark.png" srcset="pictures/welcome-page-dark.png 2x" alt="The welcome page: No repository open, the buttons Open, Clone and Init, the Workspaces heading with New workspace… and No workspaces, and Recent repositories listing weather-station and recipe-box with their folders.">

## Open a repository

Press **Open** on the welcome page, or Ctrl+O (Cmd+O on macOS) on any tab, and choose the
repository's folder. When a tab already shows that repository, VisualCommit switches to it.
Otherwise the repository opens in the active tab if it is empty, else in a new tab. The key does
nothing while the settings screen, the command palette, another dialog or a menu is open, or
while you drag a branch; the same holds for the keys of **Clone** and **Init** below.

## Open a recent repository

Under **Recent repositories**, click a repository to open it. The list holds the last ten
repositories you opened, newest first, with their folders.

## Clone a repository

1. Press **Clone** on the welcome page, or Ctrl+Shift+C (Cmd+Shift+C on macOS) on any tab. The form
   **Clone a repository** opens.
2. Type or paste the **Repository URL**.
3. Choose the **Parent folder**, the folder the clone goes into, with **Browse** or by typing it.
4. Check the **Folder name**, which VisualCommit fills in from the URL.
5. Press **Clone**. The clone opens in the tab when it has finished.

<img src="pictures/clone-form-light.png" srcset="pictures/clone-form-light.png 2x" alt="The form Clone a repository on the welcome page, with the Repository URL https://github.com/demo--alex/garden-lights.git, a Parent folder with its Browse button, the Folder name garden-lights, and the buttons Clone and Cancel.">
<img src="pictures/clone-form-dark.png" srcset="pictures/clone-form-dark.png 2x" alt="The form Clone a repository on the welcome page, with the Repository URL https://github.com/demo--alex/garden-lights.git, a Parent folder with its Browse button, the Folder name garden-lights, and the buttons Clone and Cancel.">

When you are signed in to GitHub, GitLab or Azure DevOps, the form also has a button such as
**From GitHub…** (**From an account…** when you are signed in to more than one service), which
lists your account's repositories to choose from. See
[Sign in to GitHub, GitLab or Azure DevOps](accounts.md).

## Create a new repository

Press **Init** on the welcome page, or Ctrl+Shift+I (Cmd+Shift+I on macOS) on any tab, and choose a
folder for the new repository. VisualCommit runs `git init` there and opens the repository.

## From the command palette

The command palette, Ctrl+Shift+P (Cmd+Shift+P on macOS), has **Open repository…**,
**Clone repository…** and **Init repository…**, and lists your recent repositories by name.
