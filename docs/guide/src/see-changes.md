# See what changed

Click a file to see its diff in the middle of the window, in place of the graph:

- a file of the selected commit under **Changed files** in the right panel, or
- one of your changed files under **Unstaged files** or **Staged files** when the
  **Working changes** row is selected.

The diff's header names the file, its folder, and what it shows: `Unstaged`, `Staged`, or the
commit. Close it with the **×** at its right or with Esc, and the graph comes back.

## Inline or side by side

**Inline** shows the old and the new lines in one column, removed lines marked `-` and added lines
`+`, each with its old and new line number.

<img src="pictures/diff-inline-light.png" srcset="pictures/diff-inline-light.png 2x" alt="The inline diff of station/units.py, unstaged, with Inline selected: two hunks, the first replacing the module's first line with three lines, the second adding a function kilometers_per_hour_to_knots; each hunk's bar has the buttons Stage hunk and Discard hunk, and the toolbar has Stage file and Discard file.">
<img src="pictures/diff-inline-dark.png" srcset="pictures/diff-inline-dark.png 2x" alt="The inline diff of station/units.py, unstaged, with Inline selected: two hunks, the first replacing the module's first line with three lines, the second adding a function kilometers_per_hour_to_knots; each hunk's bar has the buttons Stage hunk and Discard hunk, and the toolbar has Stage file and Discard file.">

**Side by side** shows the old file on the left and the new one on the right, with the lines lined
up.

<img src="pictures/diff-side-by-side-light.png" srcset="pictures/diff-side-by-side-light.png 2x" alt="The same diff of station/units.py with Side by side selected: the old lines on the left, the new lines on the right, removed lines red and added lines green, and the hunks' Stage hunk and Discard hunk buttons on the right side.">
<img src="pictures/diff-side-by-side-dark.png" srcset="pictures/diff-side-by-side-dark.png 2x" alt="The same diff of station/units.py with Side by side selected: the old lines on the left, the new lines on the right, removed lines red and added lines green, and the hunks' Stage hunk and Discard hunk buttons on the right side.">

Switch between them with the two buttons under the header; VisualCommit keeps your choice for the
next diff. Words that changed within a line are marked more strongly.

A diff of your working changes has buttons to stage or discard a whole file, a hunk or chosen
lines: see [Stage and commit](stage-and-commit.md).

## Images

A changed image shows the image as it was, under **Before**, beside the image as it is now, under
**After**.

## Other files

- A binary file that is not an image shows **Binary file** and its sizes before and after.
- A very large diff first shows **Very large diff** with how many lines were removed and added;
  **Show diff** shows it anyway.
- A file whose content did not change, such as one that was only renamed or whose mode changed,
  shows "No changes to the file's content."

## From a diff to the file's history

With a diff open, Ctrl+Shift+H (Cmd+Shift+H on macOS) shows the file's history, and Ctrl+Shift+B
(Cmd+Shift+B on macOS) its blame. See [File history and blame](history-and-blame.md).
