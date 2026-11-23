# What VisualCommit does not do

VisualCommit covers everyday Git work. Some things are left out on purpose, and some are planned
for later. For these, use git in a terminal: VisualCommit sees whatever git does to a repository.

## Planned for a later version

- **Submodules**: VisualCommit has no commands for submodules.
- **Worktrees**: VisualCommit does not create or switch worktrees. It does notice a branch that is
  checked out in another worktree, and does not move it from there.
- **Git LFS**: VisualCommit has no Git LFS commands.
- **A built-in terminal**.
- **A built-in file editor**: Open file opens a file in your system's program for it, and Open in
  editor in the editor you choose in [Settings](settings.md).
- **AI commit messages and commit explanations**.
- **Several profiles**, such as a work identity and a personal one. Your name and email are git's
  own settings, for all repositories or for one.
- **Commit signing** with GPG or SSH: VisualCommit has no settings or commands for signing commits
  or tags.
- **Creating and applying patches**.

## Not planned

- **A bisect view**.
- **Git Flow**.
- **Issue trackers, cloud workspaces and team features.**

## Two limits worth knowing

- **High contrast.** VisualCommit has no palette made for high contrast. When Windows' high
  contrast is on, VisualCommit follows it as its light or its dark theme.
- **Commits that are not next to each other** can be selected only with the mouse:
  Ctrl-click them (Cmd-click on macOS). The keyboard extends a selection only to the commits next
  to it, with Shift and the arrows. See [Keyboard and screen readers](keyboard-and-screen-readers.md).
