# Keyboard and screen readers

Every main action can be reached from the keyboard: by its own key, through the command palette
(Ctrl+Shift+P, Cmd+Shift+P on macOS), or with Tab and the arrows. The keys are fixed; the full list
is in [Keyboard shortcuts](reference/shortcuts.md) and in Settings, **Keyboard shortcuts**.

## General keys

| Key | What it does |
|---|---|
| F1 | Opens this guide in your browser |
| F6, Shift+F6 | Moves the focus to the next or the previous region: the left panel, the graph area, the right panel, and the activity log when it is open |
| Shift+F10, or the Menu key | Opens the menu of what has the focus, as a right-click does |
| Esc | Closes the diff, a page (file history, blame, reflog) or the search bar; in a dialog or a menu, cancels or closes it |
| Enter | In a dialog, confirms it; Ctrl+Enter (Cmd+Enter on macOS) confirms from a box of several lines |
| Tab, Shift+Tab | Moves between the controls of a region or a dialog |

F1, F6 and Shift+F6 do nothing while the settings screen, the command palette, another dialog or a
menu is open, or while you drag a branch.

> [!NOTE]
> On a Mac keyboard, F1, F6, Shift+F6 and Shift+F10 need Fn unless "Use F1, F2, etc. keys as
> standard function keys" is on; Home, End, PageUp and PageDown are Fn with the arrows. A Mac
> keyboard has no Menu key: use Shift+F10.

## What the settings screen's list cannot show

- **The graph**: the arrows, PageUp, PageDown, Home and End move the selection; with Shift they
  extend it to the commits next to it. Commits apart from each other can be selected only with the
  mouse: Ctrl-click (Cmd-click on macOS).
- **A diff**: Tab in a diff reaches the hunks' buttons, **Stage hunk** and **Discard hunk**
  (**Unstage hunk** in a staged diff), hunk by hunk; Shift+Tab reaches its toolbar, such as
  **Inline** and **Side by side**. Enter or Space presses the focused button.
- **Lines of a diff**: the diff's caret is not drawn. Press F6 until the diff has the focus, select
  lines with Shift and the arrows, then press Shift+F10 for **Stage selected lines** (or
  **Discard selected lines…**).
- **A blame**: the arrows, PageUp, PageDown, Home and End pick the run of the line the caret moves
  to, with any modifier; Shift+F10 opens that run's menu.
- **A file's buttons in the working changes**: Tab from a file's row reaches its **Stage** and
  **Discard** buttons, icons whose tooltips say so (**Unstage** on a staged file), then the next
  file's row.
- **Cancel**: while an operation that can be canceled runs, the command palette lists **Cancel**
  and its name, such as `Cancel Fetch origin`. It has no key.
- **Panels' widths**: Tab to a divider between panels; each arrow moves it by 10 pixels, and the
  panel keeps its new width, as after a drag.
- **Lists in dialogs**: in a dialog's list, such as the list of your account's repositories, the
  arrows choose a row and Enter confirms it; on a dialog's options, Enter confirms the one that is
  checked.
- **The interactive rebase list**: Alt+Up and Alt+Down (Option+Up and Option+Down on macOS) move the
  selected row; P, R, S, F and D set its action.
- **The conflict resolver**: Alt+Up and Alt+Down go to the previous or next conflict, Alt+Left and
  Alt+Right to the previous or next file (Option on macOS). Ctrl+Tab leaves the result's text.

## Screen readers

Every control has a name that a screen reader speaks, the icon buttons too. In the graph, the
selected row is read: its commit's summary, author, date and labels, such as
`current branch main`, and as its description the commit's short SHA. When you move the selection,
the new row is read. The status bar's messages, such as an operation's result, are read as they
change.

Narrator on Windows is the one screen reader VisualCommit is tested with. Others may work, but have
not been tried.

## High contrast

VisualCommit has no palette of its own for high contrast. With **Settings**, **General**,
**Theme** set to **System**, VisualCommit follows Windows' high contrast as its light or its dark
theme; with **Dark** (the default) or **Light** chosen, it keeps that theme. See
[Settings](settings.md).
