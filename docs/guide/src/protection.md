# How your work is protected

VisualCommit runs git's own commands, and adds a few guards so that an action does not quietly
throw away work that no commit holds.

## An action your changes are in the way of does not start

When a checkout would overwrite your uncommitted changes, it does not start, and nothing is
changed. The dialog **Checkout blocked** says why, such as
`1 conflict prevents checkout. Commit, stash, or discard your changes first.`, shows git's own
words and the command it ran, and offers **Stash changes and check out**: VisualCommit stashes your
changes, checks out, and puts your changes back on top.

<img src="pictures/refused-operation-light.png" srcset="pictures/refused-operation-light.png 2x" alt="The dialog Checkout blocked over the window: 1 conflict prevents checkout. Commit, stash, or discard your changes first. In weather-station; git's output naming station/units.py, the command git switch --no-guess --no-overwrite-ignore fix/rounding, and the buttons Copy output, Stash changes and check out and Close. The status bar reads Check out fix/rounding failed.">
<img src="pictures/refused-operation-dark.png" srcset="pictures/refused-operation-dark.png 2x" alt="The dialog Checkout blocked over the window: 1 conflict prevents checkout. Commit, stash, or discard your changes first. In weather-station; git's output naming station/units.py, the command git switch --no-guess --no-overwrite-ignore fix/rounding, and the buttons Copy output, Stash changes and check out and Close. The status bar reads Check out fix/rounding failed.">

A merge, cherry-pick or revert that your changes are in the way of is refused the same way, such as
`1 conflict prevents the merge. Commit, stash, or discard your changes first.`, and so is applying
a stash. A rebase needs a clean working tree: commit or stash your changes first.

## The automatic stash

Checking out an older commit of the branch you are on (or a tag on it) stashes your uncommitted
changes first, without asking, in a stash named `Auto stash before checking out` and the commit,
such as `Auto stash before checking out f39ea2d`. The status bar then reads
`Your changes are in the stash "Auto stash before checking out f39ea2d".` The stash is drawn in the
graph as a hollow circle joined to the commit it was taken on. Apply or pop it when you go back
([Stashes and tags](stash-and-tags.md)).

## Ignored files are kept

Files that git ignores, such as a `.env` file or local settings, are in no commit and in no backup.
Git itself can overwrite or delete one without a word when a checkout brings a tracked file of the
same name. VisualCommit keeps them: a checkout, merge, rebase, cherry-pick, revert, stash apply or
stash pop that would overwrite or delete an ignored file does not start, and says which files are
in the way, such as `1 ignored file is in the way of the merge: secret.env. Move it away first.`

The setting **Keep ignored files** in Settings, General turns this off; it is on to begin with.
Off, those actions run as git runs them. Checking out an older commit of the branch you are on,
Undo and Redo still keep ignored files.

## Backups and undo

Before anything that could lose work, VisualCommit saves a backup and asks first; and Undo takes
back its own actions. See [Undo, redo and the reflog](undo-and-reflog.md).
