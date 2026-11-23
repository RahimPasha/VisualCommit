# Stashes and tags

## Stash your changes

A stash puts your uncommitted changes aside and leaves the working tree clean.

1. Press **Stash** in the toolbar, or Ctrl+Shift+S (Cmd+Shift+S on macOS), or choose
   **Stash changes…** in a menu.
2. In the dialog **Stash changes**, type a message if you like.
   **Include untracked files** also stashes the files git does not track yet. It starts as set in
   Settings, General, **Include untracked files when stashing**, and the choice you stash with is
   saved there for the next stash.
3. Press **Stash**.

The stash shows under **Stashes** in the left panel and in the graph, as a hollow circle joined by
a dotted line to the commit it was taken on, with a label such as `stash@{0}`. Select it to see
its message, its date and its changed files.

<img src="pictures/stash-in-graph-light.png" srcset="pictures/stash-in-graph-light.png 2x" alt="The graph's top rows with the stash stash@{0}, On main: Try a larger font, selected: a hollow circle joined by a dotted line to the commit on main below it, and its details on the right with its author, date, SHA and parent.">
<img src="pictures/stash-in-graph-dark.png" srcset="pictures/stash-in-graph-dark.png 2x" alt="The graph's top rows with the stash stash@{0}, On main: Try a larger font, selected: a hollow circle joined by a dotted line to the commit on main below it, and its details on the right with its author, date, SHA and parent.">

## Bring a stash back

Right-click a stash in the left panel or the graph:

- **Apply stash** puts its changes back and keeps the stash.
- **Pop stash** puts its changes back and drops the stash.
- **Drop stash…** deletes it, after a question; a backup is saved first, so this can be undone.

Double-clicking a stash in the left panel applies it. **Pop** in the toolbar, or Ctrl+Shift+O
(Cmd+Shift+O on macOS), pops the newest stash.

If the stash's changes conflict with yours, VisualCommit says so before it starts, and changes
nothing; commit, stash or discard your changes first. If applying it stops with conflicts, resolve
them as for a merge ([Resolve conflicts](conflicts.md)); a popped stash is then kept.

## Tags

A tag names a commit, such as a release. Tags are listed under **Tags** in the left panel and drawn
as labels with a tag sign in the graph.

### Create a tag

Choose **Create tag here…** in a commit's or a branch's menu, or **Create tag at HEAD…** in the
**Tags** section's menu. Type the tag's name. With a message, the tag is an annotated tag; without
one, a lightweight tag. Press **Create tag**.

<img src="pictures/create-tag-light.png" srcset="pictures/create-tag-light.png 2x" alt="The dialog Create tag, at c8a5135 Show the battery level, with the name v1.2 typed, an empty box Message (optional: makes an annotated tag), and the buttons Cancel and Create tag.">
<img src="pictures/create-tag-dark.png" srcset="pictures/create-tag-dark.png 2x" alt="The dialog Create tag, at c8a5135 Show the battery level, with the name v1.2 typed, an empty box Message (optional: makes an annotated tag), and the buttons Cancel and Create tag.">

### Push, delete and more

Right-click a tag:

- **Push** the tag **to** a remote, such as `Push v1.2 to origin`. With several remotes the item
  reads `Push v1.2…` and asks which remote.
- **Delete** the tag here; it stays on remotes it was pushed to. **Delete** it **on** a remote
  deletes it there for everyone who uses that remote.
- **Check out** the tag, which leaves HEAD detached at its commit; **Create branch here…** starts a
  branch there.
- **Copy tag name**.
