# Tracery — Requirements

The phased delivery plan is in [plan.md](plan.md).

## Goal

A desktop Git client in the spirit of GitKraken: visual, fast, reliable, and usable with simple
clicks, right-clicks, double-clicks and drag-and-drop. It handles several repositories at once.
It does not need to copy GitKraken exactly; where it differs, it should be better.

## Decisions

| Topic | Decision |
|---|---|
| Name | Tracery (the branching line-work of Gothic windows; also "trace") |
| Licence | MIT, open source |
| Platforms | Windows, macOS, Linux. Windows is developed and tested first; macOS is tested by hand later |
| Audience | Public release in the future |
| UI | Avalonia (all C#), MVVM |
| Runtime | .NET 10 (LTS) |
| Git engine | The real `git` executable, driven by the app (minimum Git 2.30) |
| Hosting providers | GitHub, Azure DevOps, GitLab |
| Accounts / telemetry | None. Everything stays on the user's machine |

## Core features

| # | Area | What it covers |
|---|---|---|
| C1 | Repos | Open, clone, init; recent list; several repos open in tabs; repo groups (workspaces) |
| C2 | Commit graph | Coloured branch lanes; branch, tag, remote, stash and HEAD labels; author, date and SHA columns; smooth scrolling on very large histories |
| C3 | Left panel | Local branches, remotes, tags, stashes; ahead/behind counts; filter box; folders from `feature/...` names |
| C4 | Working changes | Staged and unstaged lists (tree or flat); stage, unstage or discard by file, hunk or line; commit and amend |
| C5 | Diff viewer | Side-by-side and inline; syntax highlighting; image diff |
| C6 | Branch actions | Create, checkout, rename, delete, merge, fast-forward, reset (soft/mixed/hard), cherry-pick, revert |
| C7 | Rebase | Rebase a branch onto another; pull with rebase; continue, skip, abort |
| C8 | Remote actions | Fetch, pull, push, safe force-push, set upstream, manage remotes, auto-fetch |
| C9 | Stash and tags | Stash, apply, pop, drop; create, delete and push tags |
| C10 | Conflict resolution | Conflicted-file list; built-in three-pane resolver (pick ours, theirs or both per hunk, edit the result); take a whole file from one side; continue or abort |
| C11 | Remote sign-in | HTTPS through Git Credential Manager; SSH keys; prompts shown as in-app dialogs |

## Interaction model

| Gesture | Result |
|---|---|
| Click a commit | Details panel: message, files changed, diff per file |
| Double-click a branch | Checkout (a remote branch creates a local tracking branch first) |
| Double-click a file | Open its diff |
| Right-click a commit | Cherry-pick, revert, reset to here, branch or tag here, rebase onto, copy SHA |
| Right-click a branch | Merge, rebase, push, pull, rename, delete, set upstream |
| Right-click a file | Stage, discard, ignore, file history, blame, open in editor or file manager |
| Drag branch A onto branch B | Menu: merge, rebase, or create a pull request |
| Ctrl/Shift-select commits | Squash, compare, cherry-pick several |

Every object has a context menu. Every common action takes two clicks at most.

## Trace and records of changes

| # | Feature |
|---|---|
| T1 | File history: every commit that touched a file, following renames, with the diff at each step |
| T2 | Blame: who last changed each line and in which commit, with a click through to it |
| T3 | Reflog viewer: where HEAD and branches have been; restore "lost" commits |
| T4 | Activity log: every git command the app ran, with output and timing |
| T5 | Commit search: by message, author, SHA or file |

## Optional features

| # | Feature | Status |
|---|---|---|
| O1 | Interactive rebase (drag to reorder, squash, fixup, reword, drop) | Included |
| O2 | Undo / redo of the last git action | Included |
| O3 | Multi-repo dashboard: status of all repos at a glance, bulk fetch/pull | Included |
| O4 | Hosting integration (GitHub, Azure DevOps, GitLab): clone from account, view and create pull requests | Included |
| O5 | Command palette and keyboard shortcuts | Included |
| O6 | Compare any two commits or branches | Included |
| O7 | Search inside changes (find the commit that added or removed a piece of text) | Included |
| O8 | Solo / hide branches in the graph | Included |
| O9 | Dark and light themes | Included |
| O19 | Installer and auto-update | Included (required for public release) |
| O10 | Submodules | Later |
| O11 | Worktrees | Later |
| O12 | Git LFS | Later |
| O13 | Built-in terminal | Later |
| O14 | Built-in file editor | Later |
| O15 | AI commit messages and commit explanations | Later |
| O16 | Multiple profiles (work and personal identity) | Later |
| O17 | Commit signing (GPG/SSH) | Later |
| O18 | Create and apply patches | Later |
| O20 | Bisect UI | Skipped |
| O21 | Git Flow | Skipped |
| O22 | Issue trackers, cloud workspaces, team features | Skipped |

"Later" features have no UI yet, but repos that use them (submodules, worktrees, LFS, signed
commits) must still open and work normally.

## Quality requirements

| # | Requirement |
|---|---|
| Q1 | Fast: first graph paint within about 2 seconds on a 100k-commit repo; smooth scrolling |
| Q2 | Responsive: the UI never freezes during a git operation; long operations show progress and can be cancelled |
| Q3 | Safe: destructive actions (hard reset, discard, force-push, branch delete) ask for confirmation and save a backup first, so they can be undone |
| Q4 | In sync: a file watcher refreshes the UI when the repo changes from a terminal or editor |
| Q5 | Compatible: behaviour matches command-line git; existing config, hooks and credential helpers work unchanged |
| Q6 | Consistent: same features and look on Windows, macOS and Linux |

## Public-release requirements

| # | Requirement |
|---|---|
| R1 | Installers and auto-update on all three platforms |
| R2 | Code signing (Windows) and notarisation (macOS) |
| R3 | Hosting tokens stored in the OS keychain, never in plain files |
| R4 | First-run check for Git, with guidance when it is missing or too old |
| R5 | Original name, icons and visual design; no GitKraken branding or assets, and no "Git" in the product name (Git trademark policy) |
| R6 | MIT licence file and third-party notices |
| R7 | Keyboard access to all main actions |

## Better than GitKraken

- No account, login, paywall or telemetry.
- Native UI instead of a bundled browser: quicker to start, lighter on memory.
- Multi-repo dashboard with bulk actions.
- Backups before every destructive action, and broader undo.
- A visible log of every git command the app runs.
