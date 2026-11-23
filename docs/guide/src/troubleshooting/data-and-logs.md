# Troubleshooting: data and logs

## The data folder

VisualCommit keeps its own data in one folder per user:

| System | Data folder |
|---|---|
| Windows | `%APPDATA%\VisualCommit` |
| macOS | `~/Library/Application Support/VisualCommit` |
| Linux | `~/.config/VisualCommit`, or `VisualCommit` in `$XDG_CONFIG_HOME` when that is set |

It holds:

- `settings.json`: the settings of the General and Tools sections, and the Git program you chose;
- `session.json`: the tabs to restore, the recent repositories, the panels' sizes, and what each
  repository's graph hides or solos;
- `workspaces.json`: your workspaces;
- `accounts.json`: which hosting accounts you signed in to, without their tokens, which are in the
  keychain;
- `avatars`: the authors' pictures from GitHub;
- `merge-tools.json`: the merge tools git listed;
- `logs`: VisualCommit's log files;
- `instance.lock` and `instance.json`, while VisualCommit runs.

Git's own settings (your name and email, the pull mode, the merge tool) are not here: they are in
git's configuration ([Settings](../settings.md)).

One VisualCommit runs per data folder. Starting it again brings the running window to the front.

## The log files

VisualCommit writes a log file of its own in the data folder's `logs` folder, one file per day,
named `visualcommit-` and the date, such as `visualcommit-20261123.log`. Files older than 14 days
are deleted. The log says what VisualCommit did and what went wrong, with the errors' details: which
Git it found, each operation's failure, and warnings such as a Git it has not been tested with.

## The activity log

For what happened in one repository, the activity log in the window shows every git command
VisualCommit ran and git's own output: see [The activity log](../activity-log.md).

## Reporting a problem

How to report a problem is set up with the public beta, and this page will then say where. When you
report one, say what you did, what you expected and what happened, and add the activity log's
lines for the operation (**Copy command** and **Copy output**) and the part of the log file from
that time. Check them first: they hold your repositories' paths and names, and your branch and file
names.
