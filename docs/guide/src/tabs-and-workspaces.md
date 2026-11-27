# Tabs, workspaces and the dashboard

## Tabs

Each tab holds one repository, a workspace's dashboard, or the welcome page. The tabs you leave
open, and the one that was active, come back when VisualCommit starts again. When several repository
tabs come back and the tab in front is one of them, it opens first; the others open once its
history shows its first rows, or after 5 seconds at the latest, at most four at a time. A tab you
switch to meanwhile opens at once, and a tab you close before its turn does not open.

| To | Press |
|---|---|
| Open a new tab | Ctrl+T (Cmd+T on macOS), or **+** after the last tab |
| Close the tab | Ctrl+W (Cmd+W on macOS), or the tab's **×** |
| Go to the next or the previous tab | Ctrl+Tab and Ctrl+Shift+Tab, or Ctrl+PageDown and Ctrl+PageUp (the same on macOS) |
| Go to tab 1 to 8 | Ctrl+1 to Ctrl+8 (Cmd+1 to Cmd+8 on macOS) |
| Go to the last tab | Ctrl+9 (Cmd+9 on macOS) |

Opening a repository that a tab already shows, with **Open** on the welcome page, Ctrl+O (Cmd+O on
macOS), the command palette or a dashboard's row, switches to that tab. A repository picked under
**Recent repositories** on the welcome page opens in that page's tab, even when another tab shows
it.

## Workspaces

A workspace is a named group of repositories that you check and update together, such as your home
projects or the services of one product.

- **Create one**: press **New workspace…** on the welcome page, give it a name, and press
  **Create**. Its dashboard opens in that tab.
- **Open one**: click it in the welcome page's **Workspaces** list.
- **Rename or delete one**: in the dashboard's **…** menu, **Rename workspace…** and
  **Delete workspace…**. Deleting a workspace closes its dashboard tab and leaves its repositories
  as they are.

## The dashboard

A workspace's dashboard lists its repositories, one row each, with their **Branch**, how far ahead
and behind their upstream they are (**Ahead / behind**), their uncommitted **Changes**, their
**State** (such as a merge in progress) and the **Operation** that runs on them.

<img src="pictures/dashboard-light.png" srcset="pictures/dashboard-light.png 2x" width="1100" alt="The dashboard tab of the workspace Home projects, 2 repositories, with the buttons Add repository…, Fetch all, Pull all and Refresh and a … menu; the rows weather-station on main, ↑1 ↓1, 3 unstaged, and recipe-box on main, Up to date, 1 unstaged.">
<img src="pictures/dashboard-dark.png" srcset="pictures/dashboard-dark.png 2x" width="1100" alt="The dashboard tab of the workspace Home projects, 2 repositories, with the buttons Add repository…, Fetch all, Pull all and Refresh and a … menu; the rows weather-station on main, ↑1 ↓1, 3 unstaged, and recipe-box on main, Up to date, 1 unstaged.">

- **Add repository…** adds repositories to the workspace; **Add open tabs**, in the **…** menu, adds
  the repositories open in tabs.
- **Fetch all** fetches every repository of the workspace, and **Pull all** pulls them. On a
  dashboard tab, the toolbar's **Fetch** and **Pull**, and their keys, do the same.
- **Refresh** reads every repository again.
- Double-click a row, or select it and press Enter, to open the repository in a tab.
- Right-click a row to **Fetch**, **Pull**, **Move up**, **Move down**, **Locate…** a repository
  that moved, or **Remove from workspace**. With a row selected, Alt+Up and Alt+Down (Option+Up and
  Option+Down on macOS) move it, and Delete removes it from the workspace (Cmd+Delete on macOS).

Ctrl+Shift+D (Cmd+Shift+D on macOS) goes to the next dashboard tab; with none open, it opens the
command palette at the workspaces.
