# Stage and commit

Select the **Working changes** row at the top of the graph. The right panel then shows your
uncommitted changes in two lists, **Unstaged files** and **Staged files**, and below them the boxes
for the commit message.

<img src="pictures/working-changes-light.png" srcset="pictures/working-changes-light.png 2x" alt="The Working changes panel: Unstaged files (3) with README.md, assets/icon.png and station/units.py, the buttons Stage all and Discard all, Staged files (0) with No staged changes, the summary Explain the units in the readme with 41 characters left, an empty Description box, the check box Amend previous commit, and the dimmed button Stage files to commit.">
<img src="pictures/working-changes-dark.png" srcset="pictures/working-changes-dark.png 2x" alt="The Working changes panel: Unstaged files (3) with README.md, assets/icon.png and station/units.py, the buttons Stage all and Discard all, Staged files (0) with No staged changes, the summary Explain the units in the readme with 41 characters left, an empty Description box, the check box Amend previous commit, and the dimmed button Stage files to commit.">

**Flat** lists the files with their folders; **Tree** shows them in a tree of folders. Click a file
to see its diff.

## Stage whole files

- Point at a file: its **Stage** button stages it, its **Discard** button discards its changes. In
  **Staged files**, a file's **Unstage** button unstages it.
- Right-click a file for **Stage**, **Discard changes…**, **Unstage**, and more.
- **Stage all**, Ctrl+Shift+A (Cmd+Shift+A on macOS), stages every change.
- **Unstage all**, Ctrl+Shift+X (Cmd+Shift+X on macOS), unstages everything.

## Stage a hunk

Open a file's diff. Each hunk's bar has **Stage hunk** and **Discard hunk**; in a diff of a staged
file, **Unstage hunk**.

<img src="pictures/stage-hunk-light.png" srcset="pictures/stage-hunk-light.png 2x" alt="The inline diff of station/units.py with the pointer on the first hunk's Stage hunk button; the second hunk has its own Stage hunk and Discard hunk buttons.">
<img src="pictures/stage-hunk-dark.png" srcset="pictures/stage-hunk-dark.png 2x" alt="The inline diff of station/units.py with the pointer on the first hunk's Stage hunk button; the second hunk has its own Stage hunk and Discard hunk buttons.">

## Stage chosen lines

In a file's diff, select lines by clicking their numbers or by dragging over the text. The diff's
toolbar then shows **Stage lines** and **Discard lines** (**Unstage lines** in a staged diff), and a
right-click on the selection offers **Stage selected lines** and **Discard selected lines…**. Only
the added and removed lines you selected are staged; the rest of the hunk stays as it is.

With the keyboard: press F6 until the diff has the focus, select lines with Shift and the arrows,
then press Shift+F10 for the menu with **Stage selected lines**. See
[Keyboard and screen readers](keyboard-and-screen-readers.md).

## Write the message and commit

1. Type a summary in the **Summary** box. The number at its right is how many of 72 characters are
   left; it goes below zero when the summary is longer.
2. Add a **Description** if you like. VisualCommit puts a blank line between the summary and it.
3. Press the commit button, which reads **Commit 1 file** or **Commit 3 files**, or press
   Ctrl+Enter (Cmd+Enter on macOS) in either box.

Ctrl+Shift+M (Cmd+Shift+M on macOS) selects the **Working changes** row and goes to the commit
message from anywhere in the repository, while there are changes to commit.
The button reads **Stage files to commit** while nothing is staged, and stays disabled until the
summary has text.

## Amend the last commit

Tick **Amend previous commit**: the boxes fill with the last commit's message, and the button reads
**Amend previous commit**. Your staged changes and the message replace the last commit. If the
commit was already pushed, pushing it again needs a force push (see [Fetch, pull and push](sync.md)).

## Discard changes

Discarding throws away uncommitted changes, so VisualCommit asks first:

- **Discard changes…** in a file's menu, or its **Discard** button.
- **Discard hunk…** in a diff's menu, or the hunk's **Discard hunk** button.
- **Discard selected lines…** for chosen lines.
- **Discard all**, or **Discard all changes…** in the **Working changes** row's menu, for every
  unstaged change.

Undo brings discarded changes back ([Undo, redo and the reflog](undo-and-reflog.md)).

## Ignore a file

Right-click an untracked file for **Ignore** and its name, such as `Ignore run.log`, which adds it
to `.gitignore`, or for its extension, such as `Ignore all *.log files`.
