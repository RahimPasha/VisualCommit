# File history and blame

## A file's history

To see every commit that changed a file:

- right-click the file, in the working changes, in a commit's changed files or in its diff, and
  choose **File history**; or
- with the file's diff open, press Ctrl+Shift+H (Cmd+Shift+H on macOS).

The page **History of** *file* takes the graph's place. It lists the commits that changed the
file, newest first, each with its change's letter (`A` added, `M` modified, and so on), short SHA
and subject, and it follows the file across renames. Click a commit to see what it changed in the
file, on the right; **Inline** and **Side by side** work as in any diff.

<img src="pictures/file-history-light.png" srcset="pictures/file-history-light.png 2x" alt="The page History of units.py in the folder station, listing five commits from 25aded4 Round to the configured digits down to 29cf672 Add unit conversions, the first selected, with its diff of units.py on the right changing the function round_reading; the page's Blame button and close button at the top right.">
<img src="pictures/file-history-dark.png" srcset="pictures/file-history-dark.png 2x" alt="The page History of units.py in the folder station, listing five commits from 25aded4 Round to the configured digits down to 29cf672 Add unit conversions, the first selected, with its diff of units.py on the right changing the function round_reading; the page's Blame button and close button at the top right.">

Double-click a commit, or choose **Show in graph** in its menu, to close the page and select it in
the graph. Its menu also has **Blame at this commit**. **Blame** at the top right switches to the
file's blame.

## Blame

The blame shows, beside each line of the file, the commit that last changed it. Open it with
**Blame** in a file's menu, with Ctrl+Shift+B (Cmd+Shift+B on macOS) while the file's diff is open,
or from the file's history.

The page **Blame of** *file* shows the file as it is in the working tree, at HEAD or at a commit.
Lines that one commit changed together form a run, marked at its first line with the commit's
short SHA, its author and its date. Lines you have not committed yet read **Not committed yet**.

<img src="pictures/blame-light.png" srcset="pictures/blame-light.png 2x" alt="The page Blame of units.py in the working tree: on the left of the lines, runs marked with their commits' short SHA, author and date, such as 29cf672 of 2025-11-05, 411ad46 of 2025-11-18 and 25aded4 of 2025-12-12, and Not committed yet beside the lines changed since the last commit; the page's File history button and close button at the top right.">
<img src="pictures/blame-dark.png" srcset="pictures/blame-dark.png 2x" alt="The page Blame of units.py in the working tree: on the left of the lines, runs marked with their commits' short SHA, author and date, such as 29cf672 of 2025-11-05, 411ad46 of 2025-11-18 and 25aded4 of 2025-12-12, and Not committed yet beside the lines changed since the last commit; the page's File history button and close button at the top right.">

### Choose a run

- With the mouse, click a run, on its mark at the left or on its lines. A double-click does
  **Show in graph**.
- With the keyboard, move the caret in the text with the arrows, PageUp, PageDown, Home or End: the
  run of the line the caret is on is chosen. Shift+F10 opens its menu.

The chosen run is highlighted and the right panel shows its commit. A run's menu has
**Show in graph**, **Blame before this commit**, to step back through a line's history,
**File history** and **Copy SHA**.

**File history** at the top right switches to the file's history.

## Close the page

Press Esc or the **×** at the page's top right: the graph comes back.
