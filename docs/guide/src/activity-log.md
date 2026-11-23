# The activity log

The activity log lists what VisualCommit did in the repository, and every git command it ran for
it, with git's output and how long it took. Open or close it with the **Activity log** button at the
right of the status bar, or with Ctrl+J (Cmd+J on macOS). It opens below the graph.

<img src="pictures/activity-log-light.png" srcset="pictures/activity-log-light.png 2x" alt="The activity log below the graph, headed Activity log weather-station, with the check box Show reads and a close button; one row with a check mark: 09:00:00, Create branch try/larger…, Created try/larger-font, 0 ms.">
<img src="pictures/activity-log-dark.png" srcset="pictures/activity-log-dark.png 2x" alt="The activity log below the graph, headed Activity log weather-station, with the check box Show reads and a close button; one row with a check mark: 09:00:00, Create branch try/larger…, Created try/larger-font, 0 ms.">

## What a row shows

Each operation you started is a row: the time it started, its title (such as `Create branch` and
the branch's name), its result (such as `Created try/larger-font`) and how long it took. A check
mark means it succeeded; a failed one is marked as failed. Click the arrow at a row's left to open
it: underneath are the git commands it ran, each of which opens in turn to show its output.

**Show reads** also lists the commands VisualCommit runs only to read the repository, such as those
that fill the graph and the panels.

## Copy from the log

Right-click a row for **Copy title** (an operation), **Copy command** (a git command) or
**Copy output** (a git command's output). Credentials in a command are hidden.

## When something goes wrong

The activity log is the first place to look when an operation fails: it shows the exact command
and git's own words. VisualCommit also writes a log file of its own; see
[Data and logs](troubleshooting/data-and-logs.md).

Drag the line above the log to make it taller or shorter. Close it with its **×**, the status bar's
button or Ctrl+J.
