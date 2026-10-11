# Phase 3 design: branch, remote, stash and tag operations

Working design notes for phase 3, written on 2026-10-11 before any phase 3 code exists. The
builders implement from this file; the final facts are folded into `architecture.md` at the end
of the phase and this file is then deleted. Section 9 lists the decisions this design makes;
they are recorded in `decisions.md` as D77 to D104. Three independent reviews (git and data
safety, UI and harnesses, coverage and consistency) were folded in before recording; what they
changed is summarised in section 10.

The owner's four answers of 2026-10-10 are taken as given (D77 to D80):

1. Later phases' actions (rebase onto, squash, compare, file history, blame, create pull
   request) are left out of phase 3's menus, not shown disabled.
2. Pull follows git's own configuration (`pull.rebase`, `pull.ff`) and merges when nothing is
   set.
3. Auto-fetch is on by default, every 5 minutes, quietly, for the active tab; it never prompts;
   tests turn it off.
4. A checkout blocked by local changes offers "Stash changes and check out" in its error
   dialog: stash, check out, re-apply; on a conflict it stops and says so.

Conventions in this file: `<b>` is a local branch's short name (`feature/alpha`), `<r>` a
remote's name, `<t>` a tag's name, `<sha>` a full commit id. The app always passes git full ref
names (`refs/heads/<b>`, `refs/remotes/<r>/<b>`, `refs/tags/<t>`) or full SHAs where git accepts
them, so that a branch and a tag of the same name are never confused (where git would put a full
name into a message, as `git merge` does, the message is built separately: 2.8). "Short SHA" is
the first 7 characters. Every git call goes through `GitRunner` (D30, D59, D68, D74, D102).

Upstreams: `<r>` and `<u>` below are never cut out of `%(upstream:short)`, which is wrong for a
local upstream (`main` for `branch.<b>.remote = .`) and ambiguous when a local branch is named
like a remote branch (`remotes/origin/main`). `GitRef` gains `UpstreamRef` (`%(upstream)`, the
full remote-tracking ref, e.g. `refs/remotes/origin/main`), `UpstreamRemote` (`%(upstream:remotename)`,
`.` for a local upstream) and `UpstreamRemoteRef` (`%(upstream:remoteref)`, the branch's name on
the remote, e.g. `refs/heads/main`), all available in Git 2.30 (D101). `<r>` is
`UpstreamRemote`, `<u>` is `UpstreamRemoteRef` without `refs/heads/`, the lease, the backup and the
fast-forward use `UpstreamRef`, and labels show `<r>/<u>`. A **local upstream** (`UpstreamRemote`
is `.`) has no push, force push or remote deletion: those items are left out, and pull and
fast-forward work against `UpstreamRef` without a fetch (2.18).

## 0. Facts checked before deciding

Run in a scratch folder on the Windows machine (Git 2.36.0.windows.1, OpenSSH 8.9p1) and read
from the Avalonia 12.1.3 packages in the NuGet cache. Builders can rely on these; where CI's Git
2.55 might differ, the tests run on both.

| What | Result |
|---|---|
| `git switch <b>` and `git checkout <b>` blocked by a local change | exit 1, stderr `error: Your local changes to the following files would be overwritten by checkout:` then the files, then `Please commit your changes or stash them before you switch branches.` and `Aborting`. An untracked file in the way gives `error: The following untracked working tree files would be overwritten by checkout:`. When both apply, git prints both lists in one run |
| Stash, check out, pop with an untracked file in the way (review) | `stash push --include-untracked`, the switch and `stash pop` → `x already exists, no checkout`, `error: could not restore untracked files from stash`, exit 1, no conflicted file, the stash kept: the untracked case cannot be re-applied (2.31) |
| `git stash push` (no `-u`), switch, `git stash pop --index` | keeps the staged and unstaged split. When the index part does not apply: `error: conflicts in index. Try without --index.`, exit 1, nothing changed, the stash kept; a plain `pop` then applies it (with conflicts where they exist) |
| `git reset --hard <target>` with an untracked file whose path `<target>` tracks, and a file staged as new (review) | exit 0, no message: the untracked file is overwritten with the target's content and the new file is deleted |
| `git update-ref refs/heads/<b> <new> <old>` on a branch checked out in another worktree (review) | exit 0: the branch moves and that worktree then shows the fast-forward as staged reverse changes. `git fetch . <src>:refs/heads/<b>` refuses it (`refusing to fetch into branch ... checked out at ...`, exit 128) |
| Leaving a detached HEAD with commits on no branch (review) | `git switch <b>` exits 0 with only a warning on stderr (`you are leaving 1 commit behind`); the commits drop out of the graph (D47) |
| `git merge --no-edit --no-ff refs/heads/feature/alpha` | message `Merge branch 'refs/heads/feature/alpha'` (git puts the name as given). The short name gives `Merge branch 'feature/alpha'`, but a tag of the same name wins the lookup (`rev-parse --symbolic-full-name` then prints nothing and `refname ... is ambiguous`). `printf '<sha>\t\tbranch '"'"'feature/alpha'"'"' of .\n' \| git fmt-merge-msg` prints `Merge branch 'feature/alpha'` (with ` into side` on another branch), and `git merge --no-edit --no-log -m <that> refs/heads/feature/alpha` commits it |
| `git tag --annotate --file=-` with a message `#42 hotfix` | an empty message (cleanup `strip`); with `--cleanup=whitespace` the message is kept |
| `git push --force-with-lease=refs/heads/topic:<old> origin --delete refs/heads/topic` after someone else pushed | ` ! [rejected] (delete) -> topic (stale info)`, exit 1 |
| `git push --force-with-lease=refs/heads/main --force-if-includes origin refs/heads/main:refs/heads/main` after a fetch brought someone else's commit | ` ! [rejected] main -> main (remote ref updated since checkout)`, exit 1; after only an amend of the fetched tip: `(forced update)`. With an explicit `:<sha>` git ignores `--force-if-includes`. With no remote-tracking ref the push creates the branch |
| `git cherry-pick A C B` stopped at C | `sequencer/todo` lists C (the stopped pick) and B; committing concludes C only and leaves `sequencer/`; `--abort` undoes the whole sequence. A single `git cherry-pick C` that stops writes no `sequencer/` |
| `git pull` with `pull.rebase=true` and `pull.ff=only` on diverged branches | `fatal: Not possible to fast-forward, aborting.`: `pull.ff=only` wins |
| `git fetch origin`, then `git merge --no-edit FETCH_HEAD` | `Merge branch 'main' of <url>`, the message `git pull` writes; `git rebase` with no arguments after the fetch gives `git pull --rebase`'s history |
| `git fetch --no-auto-maintenance` | accepted (Git 2.29 and later) |
| `git pull` on diverged branches with no `pull.*` configuration | Git 2.36: exit 128, `fatal: Need to specify how to reconcile divergent branches.` (since 2.33.1; 2.27 to 2.33.0 only warn and merge). `git pull --no-rebase` merges |
| Where `git pull` writes | Fetch part (`remote: ...`, `From ...`, ref lines) on stderr; a merge's `Updating a..b`, `Fast-forward`, `Merge made by the 'ort' strategy.`, `Already up to date.` on stdout; a rebase's messages on stderr, nothing on stdout |
| `git fetch . refs/remotes/origin/main:refs/heads/side` | fast-forwards `side`; refuses a non-fast-forward (`! [rejected] ... (non-fast-forward)`, exit 1) and a branch checked out in any worktree (exit 128). The design uses it (2.7) |
| `git push --force-with-lease=refs/heads/main:<sha> origin main` | forced update when the remote is at `<sha>`; otherwise `! [rejected] main -> main (stale info)`, exit 1 |
| Push behind the remote | `! [rejected] main -> main (fetch first)` (or `(non-fast-forward)`), `error: failed to push some refs`, hints, exit 1 |
| `git tag --annotate --file=- <t> <sha>` | reads the message from standard input |
| `git stash push [--include-untracked] --message m` with nothing to stash | exit 0, `No local changes to save` |
| `git stash apply` / `pop` with a conflict | exit 1, `CONFLICT (content)`; the files are `UU` in `git status`; no in-progress file is written; `pop` keeps the entry (`The stash entry is kept in case you need it again.`) |
| `git stash drop <sha>` | refused (`is not a stash reference`); only `stash@{n}` works. `git stash apply <sha>` works |
| `git cherry-pick A B C` | applies in the order given (checked with commits given newest first) |
| Cherry-pick of several stopping at a conflict | `CHERRY_PICK_HEAD`, `sequencer/todo` (the remaining `pick` lines), `MERGE_MSG`; `--abort` returns to the start |
| `git revert <merge>` without `-m` | exit 128, `is a merge but no -m option was given`; `-m 1` works |
| Merge, revert and rebase stopped by a conflict | `MERGE_HEAD` (+ `MERGE_MSG`, `MERGE_MODE`); `REVERT_HEAD`; `rebase-merge/` with `head-name`, `onto`, `git-rebase-todo`. Each `--abort` exits 0 |
| `git check-ref-format --branch @{-1}` | resolves it (prints `main`): `--branch` is not a pure syntax check. `git check-ref-format refs/heads/<name>` is |
| `git switch -c x --track refs/remotes/origin/x`, `git branch --set-upstream-to=refs/remotes/origin/x main` | both accept the full remote ref and set the upstream to `origin/x` |
| `git push --progress origin --delete refs/heads/topic` | `- [deleted] topic`, exit 0 |
| `git fetch --all` without remotes | exit 0, no output |
| `GIT_ASKPASS` pointing at an `.exe` in a folder with spaces (Windows path or forward slashes) | works: git runs it without a shell, with the prompt as its one argument. Prompts seen: `Username for 'https://example.com': ` and `Password for 'https://alice@example.com': ` (`git credential fill`) |
| `SSH_ASKPASS` with a space in its path and `SSH_ASKPASS_REQUIRE=force`, Git for Windows' OpenSSH 8.9 | used for `Enter passphrase: ` (`ssh-keygen -y -f <encrypted key>`), standard input closed |
| `GIT_TERMINAL_PROMPT=0` together with `GIT_ASKPASS` | askpass is asked first; only when it fails does git fall back to the terminal, which the variable forbids (`could not read Username for '...': terminal prompts disabled`) |
| `System.Net.HttpListener` on `http://localhost:<port>/` | starts without administrator rights here; not used (7.3) |
| Avalonia popups | `AvaloniaHeadlessPlatformOptions.OverlayPopups` defaults to **true** (popups are drawn inside the window); `Win32PlatformOptions`, `X11PlatformOptions` and `AvaloniaNativePlatformOptions.OverlayPopups` default to **false** (a popup is a top-level window of its own) |
| Avalonia 12.1.3 APIs | `ToolTip.ShowOnDisabled`, `LauncherExtensions.LaunchFileInfoAsync`, `ClipboardExtensions.SetTextAsync` exist; `Control.ContextRequested` is raised by a right-click (phase 1's `A_right_click_opens_the_context_menu_of_the_control` proves it in the headless harness) and by the Menu (Apps) key, **not** by Shift+F10: `HotkeyConfiguration.OpenContextMenu` is `Apps` only, headless and on Win32 (UI review's spike). The app adds Shift+F10 itself (4a) |
| Avalonia 12.1.3, headless (UI review's spike) | a stock `ContextMenu` and `MenuFlyout` render in `CaptureRenderedFrame` and can be clicked; tooltips appear after the real delay (about 0.7 s, then 0.1 s), `ShowOnDisabled` included; per-popup overlay exists (`Popup.ShouldUseOverlayLayer`, public `MenuFlyout.Popup`, attached `ToolTip.ShouldUseOverlayLayer`); a `Panel` with an automation id is not a UI Automation control element (custom peers are needed, 4a); a double-click on a row `Button` raises `DoubleTapped` once and runs its `Command` twice; headless windows never lose activation; pointer capture moved to a hit-test-invisible `Canvas` keeps the drop target findable by `InputHitTest` and suppresses the source button's `Click` |
| The test projects' output folders | hold `VisualCommit.exe` and its runtimeconfig: an `Exe` project referenced by another project brings its app host along |

## 1. Operation queue

### 1.1 Where it lives

| Part | Where | Why |
|---|---|---|
| Contracts and the queue itself: `GitOperation`, `OperationContext`, `OperationReport`, `OperationResult`, `OperationProgress`, `GitFailure`, `IOperationQueue`, `OperationQueue`, `IOperationLog`, `IGitPromptBroker` | `src/VisualCommit.Core/Operations/` (new folder, namespace `VisualCommit.Core.Operations`) | The queue is scheduling only: it runs delegates one at a time, pauses and refreshes through delegates it is given, and never starts a process. Core holds no process calls (architecture.md), and the queue can be unit-tested without git or a UI |
| What runs inside an operation: the git calls | `IGitRepository` / `GitRepository` (new partial files, 2.x) | As today: the repository knows git |
| Progress parsing, failure classification | `src/VisualCommit.Git/GitProgressParser.cs`, `src/VisualCommit.Core/Operations/GitFailure.cs` (pure text rules) | |
| The prompt channel | `src/VisualCommit.Git/Prompts/` and the helper project `src/VisualCommit.AskPass` (section 3) | |
| One queue per open repository | `RepositoryViewModel` creates it in its constructor and disposes it in `Dispose` | D45: everything about one repository is owned by its `RepositoryViewModel` |
| What the status bar shows | `ViewModels/Operations/OperationStatusViewModel.cs`, one per `RepositoryViewModel` (`RepositoryViewModel.OperationStatus`) | |
| Building and starting each operation (confirmations, backups, dialogs, then `queue.RunAsync`) | `ViewModels/Operations/RepositoryOperations.*.cs`, one instance per `RepositoryViewModel` (`RepositoryViewModel.Operations`) | Keeps `RepositoryViewModel.cs` from growing by another thousand lines |

This replaces `architecture.md`'s "`VisualCommit.Git` ... Later: the operation queue" (D81).

### 1.2 API

```csharp
namespace VisualCommit.Core.Operations;

public enum OperationKind { Local, Network }

[Flags]
public enum RefreshScope { None = 0, Status = 1, Refs = 2 }   // Refs = RefreshAsync, which reads the status too

public enum ErrorDisplay { Dialog, StagePanel }               // where a failure is shown

public enum PromptMode { None, Ask, Deny }                    // section 3

public sealed record GitOperation(
    string Title,            // "Fetch", "Push main" — the log line and the error dialog's title prefix
    string RunningText,      // "Fetching from all remotes…" — the status bar while it runs
    OperationKind Kind,
    RefreshScope Refresh,
    Func<OperationContext, Task<OperationReport>> Run)
{
    public string? DedupeKey { get; init; }            // null (the default): never deduplicated. See below
    public ErrorDisplay Errors { get; init; } = ErrorDisplay.Dialog;
    public bool IsBackground { get; init; }            // auto-fetch only
    public PromptMode? Prompts { get; init; }          // null: Ask for Network and for phase 3's Local
                                                       // operations (1.5), None for phase 2's writes; auto-fetch: Deny
}

public sealed class OperationContext
{
    public string OperationId { get; }                       // "op-<n>", unique per app session
    public CancellationToken CancellationToken { get; }      // CancellationToken.None for Local operations
    public IReadOnlyDictionary<string, string?> Environment { get; }   // the prompt scope's variables; empty for PromptMode.None
    public IProgress<OperationProgress> Progress { get; }    // throttled to one update per 100 ms
    public void EndCancelWindow();                           // from now on Cancel is no longer offered and the token
                                                             // no longer reaches git (pull's integration step, 2.18)
}

public enum OperationOutcome { Succeeded, Conflicts, Failed, Cancelled }

public sealed record OperationReport(OperationOutcome Outcome, string Message)  // returned by Run
{
    public static OperationReport Done(string message);
    public static OperationReport Conflicts(string message);
}

public sealed record OperationResult(OperationOutcome Outcome, string Message, GitFailure? Failure);

public sealed record GitFailure(
    GitFailureKind Kind, string Summary, string GitOutput, string CommandText, int? ExitCode);

public interface IOperationQueue : IDisposable
{
    Task<OperationResult> RunAsync(GitOperation operation);
    bool TryStartInBackground(GitOperation operation);   // runs it only when the queue is idle; false otherwise.
                                                         // A background operation is never cancelled (1.8)
    Task WhenIdleAsync();                                // the running operation, all its steps, has ended (app exit, 1.8)
    OperationSnapshot? Current { get; }                  // Title, RunningText, Kind, IsBackground, Progress, CanCancel
    int Waiting { get; }
    OperationResult? LastResult { get; }                 // the last non-background operation's result
    void CancelCurrent();                                // no effect unless Current.CanCancel (never a background one)
    event EventHandler? Changed;                         // raised on any thread; listeners marshal themselves
}
```

`OperationQueue(string repositoryName, string gitDirectory, Func<IDisposable?> pauseWatcher, Func<RefreshScope, Task> refresh, IGitPromptBroker? prompts, IOperationLog? operations, IAppLog log)`.

**Dedupe (opt-in).** Only an operation with a non-null `DedupeKey` is deduplicated: a second
`RunAsync` with the key of a waiting or running operation returns that operation's task. The
key names the operation and every argument that changes what it does. Only the idempotent
toolbar and menu actions without choices set one: `fetch:*`, `fetch:<r>`, `pull:<full branch
ref>`, `push:<full branch ref>-><r>/<remote ref>`. Everything else leaves it null and always
runs: phase 2's writes (two stages of two files, or two line patches, are two operations, in
order, as D76 requires), force push (never merged with a normal push of the same branch),
every operation that takes files, lines, a message or a dialog's choices. `OperationQueueTests`
queue two stages of different files while a third runs and assert all three ran in order.

### 1.3 Lifecycle of one operation

1. `RunAsync` is called on the UI thread (every caller is a view model). It appends the
   operation to a FIFO list (`Waiting` + 1, `Changed`). If the queue is disposed, it returns
   `Cancelled` at once. A running background operation is **not** cancelled: the new operation
   waits for it (1.8), and the status bar shows "Waiting for auto-fetch…" meanwhile.
2. When it is first in line and nothing runs (a `SemaphoreSlim(1, 1)`, as D76's `_writeGate`):
   `Current` is set, `Changed`.
3. A prompt scope is opened for `PromptMode.Ask` or `Deny` (section 3); its variables become
   `OperationContext.Environment`.
4. `using (pauseWatcher())` (D71) around `Run(context)`, **on the caller's synchronization
   context** (the UI thread): the queue captures it in `RunAsync` and never uses `Task.Run`.
   Phase 2's delegates change bound view-model state (`Changes.ShowRestore`, `HideRestore`) and
   must stay on the UI thread. Q2 still holds: every git call already leaves the UI thread
   inside `GitRunner` (D59), and a repository method that does other slow work (file copies,
   hashing) awaits it on the pool itself. `refresh` (step 7) runs on the same context.
5. The outcome: the report `Run` returned; `OperationCanceledException` → `Cancelled`;
   `GitException` → `Failed` with a `GitFailure` (1.6); `IOException` or
   `UnauthorizedAccessException` → `Failed` with the exception's message as `GitOutput`; any
   other exception → `Failed` with "Unexpected error: <message>", logged at Error with the stack
   (the queue never dies with its operation). A failed prompt that the user cancelled (the
   scope's `UserCancelled`) turns `Failed` into `Cancelled`.
6. The prompt scope is disposed (an open prompt dialog of this operation closes).
7. After a `Cancelled` network operation: stale lock files (1.8) are looked for. Then
   `refresh(operation.Refresh)` runs, also after a failure or a cancel (the operation may have
   done part of its work), unless the queue was disposed meanwhile.
8. The operation is recorded (1.7), `Current` is cleared, `LastResult` set, `Changed`; the
   task completes with the `OperationResult`.

`OperationQueueTests` run a discard and a restore through the queue on the headless UI thread
(the restore bar changes without a cross-thread exception) and assert that `Run` is entered on
the captured context.

### 1.4 How it takes over `RepositoryViewModel.WriteAsync`

`WriteAsync(Func<CancellationToken, Task> write, bool refsChange)` stays as a private helper so
that phase 2's callers do not change, but its body becomes:

```csharp
var result = await _queue.RunAsync(new GitOperation(title, runningText, OperationKind.Local,
    refsChange ? RefreshScope.Refs : RefreshScope.Status,
    async context => { await write(context.CancellationToken); return OperationReport.Done(""); })
    { Errors = ErrorDisplay.StagePanel, Prompts = PromptMode.None });   // no DedupeKey: never merged
var error = result.Outcome == OperationOutcome.Failed ? result.Failure!.GitOutput : null;
Changes.ErrorText = error;
return error;
```

with these titles: Stage ("Staging…"), Unstage ("Unstaging…"), Discard ("Discarding…"), Restore
("Restoring…"), Commit ("Committing…"), Apply lines ("Applying…"). `_writeGate` and
`WriteNowAsync` are removed; one at a time (D76) and the watcher's pause (D71) are now the
queue's. Phase 2's behaviour stays exactly: git's message goes to the stage panel's
`CommitError`, not to a dialog, and the stage panel's operations leave no message in the status
bar (1.9). A write asked for after the tab closed returns `Cancelled` and does nothing, as
today. One change on purpose: today a write's token is the tab's lifetime, so closing the tab
can cancel a write half-way; from now on a write that has started runs to its end (1.8).

### 1.5 Network and local operations

| | Local | Network |
|---|---|---|
| Which | Everything that touches only the repository on disk: checkout, branch, merge, reset, cherry-pick, revert, abort, stash, tag, remote add/rename/set-url/remove, ignore, and phase 2's writes | Everything that talks to a remote: fetch, pull, push (all its forms: set upstream, force with lease, delete a remote branch, push or delete a tag), add remote with "Fetch now", clone (the welcome page, outside the queue) |
| Token passed to git | `CancellationToken.None`: once started the whole operation, every git call of it, runs to its end, even when the tab closes or the app exits (1.8; git is never killed half-way through writing the index or the working tree) | Cancellable by the status bar's Cancel and by closing the tab or the window (1.8). A background auto-fetch is never cancelled (1.8) |
| Prompts | Phase 3's local operations: `Ask`, not cancellable. A checkout, merge, reset, cherry-pick, revert or stash apply can run a filter or hook that asks for credentials (git-lfs's smudge filter downloads over HTTPS); an ask costs nothing when no prompt comes, and a cancelled prompt makes git fail rather than hang. Phase 2's writes and every read: `None` | `Ask` (user-started) or `Deny` (auto-fetch) |
| Progress | parsed from stderr when git prints it (a large checkout's `Updating files: 45%`) | parsed from stderr |

### 1.6 Failures keep git's own output (C11)

`GitException` gains an optional `StandardOutput` (constructor parameter with default `""`;
`GitResult.EnsureSuccess` passes it), additive and in Core. The queue builds a `GitFailure`:

- `GitOutput`: `GitOutputCleaner.Clean(stderr)` (stderr and stdout of the call that failed;
  `GitRunner` already hides credentials in what it records, D103) — git's standard error with progress lines
  removed (the rule of `GitRepository.CloneErrorText`, moved to Core and generalised: split on
  `\r` and `\n`, drop lines of git's progress shape and `Cloning into`), credentials hidden
  (`GitCommand.HideCredentials`); when that is empty, the cleaned standard output (a merge
  writes its messages there). Never shortened: the dialog scrolls.
- `CommandText`: the failing call's `GitCommand.DisplayText`.
- `Kind`: `GitFailureClassifier.Classify(stderr, stdout, exitCode)` on git's English text (D30):

| Kind | Recognised by |
|---|---|
| `CheckoutBlockedByLocalChanges` | `Your local changes to the following files would be overwritten by checkout` **and not** `untracked working tree files would be overwritten` (only this kind offers "Stash changes and check out", 2.31) |
| `CheckoutBlockedByUntrackedFiles` | `The following untracked working tree files would be overwritten by checkout` (alone or together with the tracked message) |
| `PushRejected` | ` ! [rejected]` with `(fetch first)` or `(non-fast-forward)` |
| `PushRejectedStale` | ` ! [rejected]` with `(stale info)` or `(remote ref updated since checkout)` (`--force-if-includes`, 2.20) |
| `AuthenticationFailed` | `Authentication failed`, `could not read Username`, `could not read Password`, `Permission denied (publickey`, `Host key verification failed` |
| `NotFastForward` | `Not possible to fast-forward` |
| `IndexLocked` | `index.lock': File exists` |
| `Other` | anything else |

- `Summary`: the operation's own one-line sentence (the "Failures" column of section 2), such as
  "Git refused to push main to origin."

### 1.7 Activity log (T4, IGitCallLog)

- `GitCallRecord` gains `string? OperationId` (last positional parameter with default null, so
  existing callers compile). `GitRunner.Record` fills it from `GitOperationScope.Current`, an
  `AsyncLocal<string?>` in Core that the queue sets around `Run`. `GitRunner.RunAsync`'s
  `Task.Run` carries the async-local value to the thread pool.
- `GitRunner.Record` passes the heads of standard output and standard error through
  `GitCommand.HideCredentials` before they reach `IGitCallLog`, as it already does for the
  command line (D103): `git remote -v` and some error texts print URLs with a token in them.
  A test adds a remote `https://alice:ghp_SECRET123@127.0.0.1:1/x.git`, runs `ReadRemotesAsync`
  and a failing fetch, and asserts that `ghp_SECRET123` is in no call record, no log line and no
  rendered text of the remotes dialog.
- `IOperationLog` (Core) with `Add(OperationRecord)` and `Snapshot()`; `OperationLog` (Core,
  in-memory, the last 500) is created in `AppSession` and handed to each queue through
  `TabServices`. `OperationRecord(string Id, string Repository, string Title, DateTimeOffset
  StartedAt, TimeSpan Duration, OperationOutcome Outcome, string Message)`. Phase 5's activity
  log groups the call records under these.
- The app log: `<repo>: <Title>: started` (Debug) and `<repo>: <Title>: <outcome> after <N> ms:
  <Message>` (Info; a failure at Warning with the `GitOutput`'s first line). The real-window pass
  waits for this line.

### 1.8 Tab close, app exit, auto-fetch

Killing git is not free: a fetch or push that is killed while it updates refs leaves
`refs/.../<name>.lock` or `packed-refs.lock` behind (TerminateProcess and SIGKILL skip git's
clean-up), and every later fetch then fails with `cannot lock ref ... File exists` until the
file is removed. A fetch also ends with `git maintenance run --auto`. So the app kills git only
when the user asks for it, and reports what a kill left behind.

- **Tab close** (`RepositoryViewModel.Dispose` → `queue.Dispose()`): the waiting operations
  complete as `Cancelled` without running; a running user network operation is cancelled (git
  is stopped, D31/D36/D37); a running local operation runs to its end, **all of its steps** (the
  queue keeps the operation's delegate alive; no refresh afterwards); a running background
  auto-fetch is left to finish on its own (it never prompts); every prompt scope of this queue
  is disposed, which closes its prompt dialog.
- **App exit:** `MainWindow.Closing` cancels the close while any tab's queue runs a
  non-background operation and opens the dialog "Finishing operations" (`FinishingDialog`): one
  line per running operation ("Push main — graph"), and the button "Quit now" (danger). Waiting
  operations are dropped at once. When the running ones end, the window closes by itself.
  "Quit now" cancels the network operations and closes; a local one still running then
  finishes its current git call after the process has gone (the Windows job object has no
  kill-on-close limit, D31; on macOS and Linux git is not a child that dies with the app), but
  its later steps do not run: the dialog says so under the line of such an operation ("Its
  remaining steps will not run"). A background auto-fetch is left running, as at tab close.
- **Stale locks after a cancel:** after a network operation ends `Cancelled`, the queue lists
  the `*.lock` files under the repository's git folder (and the common git folder of a
  worktree) whose time is after the operation's start. If there are any, the result is shown
  in the error dialog "<Title> cancelled": "Git was stopped while it was updating references.
  These lock files were left behind and will make later git commands fail: …", with the extra
  button "Remove lock files", which deletes exactly those files that still exist. After a tab
  close or "Quit now" the same list goes to the log at Warning.
- **Auto-fetch** (detail in 6.1) is a `GitOperation` with `IsBackground = true`, `Prompts =
  Deny`, started with `TryStartInBackground`, which does nothing unless the queue is idle. It
  runs `git fetch --all --progress --no-auto-maintenance` (the user's own fetches keep git's
  maintenance, Q5). It is **never pre-empted**: an operation the user starts while it runs
  waits for it (usually a second or two; the status bar says "Waiting for auto-fetch…"). A
  background operation shows no running text, progress or Cancel, and never opens a dialog.

### 1.9 Progress, cancel and the status bar

- `GitProgressParser.Parse(string line) → OperationProgress?` (Git project; `OperationProgress(string Stage, int? Percent, string Text)` in Core). It is today's
  `CloneProgressParser` rule (stage before the first colon that ends a word, first `NN%`, a
  leading `remote: ` removed) applied only to lines of git's progress shape: an optional
  `remote: `, then one of `Enumerating objects`, `Counting objects`, `Compressing objects`,
  `Receiving objects`, `Resolving deltas`, `Unpacking objects`, `Writing objects`,
  `Updating files`, `Checking out files`, `Delta compression`, or `Fetching <remote>`. Other
  lines return null and leave the shown progress as it is. `CloneProgressParser.Parse` keeps
  its contract and delegates. Network methods set `GitCommand.OnErrorLine` to report into
  `NetworkOptions.Progress`.
- The status bar (4e) shows `RunningText` and, when a progress line has come, `Stage Percent%`;
  the bar is determinate with a percentage, indeterminate without.
- Cancel is offered while `Current.CanCancel`: network operations, until
  `EndCancelWindow()`. Cancel calls `CancelCurrent()`, which cancels the operation's token; the
  runner stops git (on Windows with everything it started, through the job object; on macOS and
  Linux git only, D37) and the call returns within 10 seconds (D36). The result is `Cancelled`;
  the status bar says "<Title> cancelled".
- Pull (2.18) is two git calls in one operation: a fetch, cancellable, and then a local merge or
  rebase. `EndCancelWindow()` is called between them, so no output parsing decides when killing
  git is safe.

## 2. Operations

Every operation runs as one `GitOperation` through the queue, except where noted. "Refresh:
Refs" is `RefreshAsync` (refs, the history when the fingerprint changed, then the status);
"Status" is `RefreshStatusAsync`. "Error dialog" means the error dialog of 4f with the
operation's summary and git's cleaned output. A confirmation is phase 2's `ConfirmationView`
with the given title, question, detail and button; the detail of every confirmation of phase 3
that saves a backup is **"A backup is saved first, so this can be undone."** (phase 2's discard
keeps its own detail, "A snapshot is saved first, so the changes can be restored.", which its
restore bar makes true; its checks and `WorkingChangesTests` do not change). Every name shown in a
question or a message is the short name (`feature/alpha`, `origin/main`, `v1.0`); a commit is
shown as `a1b2c3d "Add core"` with the subject cut at 50 characters with "…".

### 2.0 Backup refs (Q3)

`GitRepository.SaveBackupRefsAsync(BackupRequest)` →
`refs/visualcommit/backup/<kind>-<UTC time>[-<n>][/<name>]`, `<UTC time>` as D67's
`yyyyMMdd'T'HHmmssfff'Z'`. One operation takes **one** timestamp and writes all its refs (a
`-changes` snapshot, several remote branches) in one `git update-ref --stdin` transaction with
`create <ref> <target>` lines (each must not exist). On a collision the whole batch is retried
with the suffix on the `kind-time` part (`branch-<time>-2/feature/alpha`, `reset-<time>-2` and
`reset-<time>-2-changes`), never on the whole ref: a name with slashes then cannot clash with a
folder of the same name, and a `-changes` ref always pairs with its main ref by name. D67's
`discard-<time>` refs keep their own scheme. `SaveSnapshotAsync` (D67) gets an overload that
returns the snapshot commit without making a ref, so the snapshot joins the operation's batch.
Kinds:

| Kind | Points at | Saved before |
|---|---|---|
| `discard-<time>` | D67's snapshot (unchanged) | a discard |
| `reset-<time>` | HEAD's commit before the reset | every reset (soft, mixed, hard) |
| `reset-<time>-changes` | a snapshot (2.11) of every staged or unstaged tracked file, every file staged as new, and every untracked file whose path the target commit tracks; the index's own tree as a second parent when it has no conflicts | a hard reset, when there are such files |
| `detached-<time>` | the detached HEAD's commit | checking out anything while HEAD is detached on commits that no branch, remote branch or tag reaches (2.1) |
| `branch-<time>/<b>` | the branch's commit | deleting a local branch |
| `remote-branch-<time>/<r>/<b>` | `refs/remotes/<r>/<b>`'s commit, which is also the lease's value (2.22) | deleting a branch on a remote |
| `remote-<time>/<r>/<b>` | each `refs/remotes/<r>/<b>`'s commit, one batch | removing a remote (2.23) |
| `force-push-<time>/<r>/<u>` | `UpstreamRef`'s commit read just before the push (2.20) | a force push |
| `tag-<time>/<t>` | the tag object itself (an annotated tag keeps its message) | deleting a tag |
| `remote-tag-<time>/<r>/<t>` | the local tag object, which is also the lease's value (2.29) | deleting a tag on a remote |
| `stash-<time>` | the stash commit (it keeps its index and untracked parents; `git stash store` brings it back) | dropping a stash |
| `abort-<time>` | HEAD's commit before the abort | aborting a merge, cherry-pick, revert, rebase or `git am` |
| `abort-<time>-changes` | a D67 snapshot of every changed tracked file | the same, when there are such files |

Lifetime: phase 3 never deletes a backup ref, as D67's snapshots. They are outside
`--branches --remotes --tags` (D47), so the graph does not show them. Phase 5's undo (O2) reads
them and decides their retention.

Operations that confirm and back up: discard (phase 2), every reset (the reset dialog is the
confirmation; its button is in the danger style while Hard is chosen), delete a local branch,
delete a remote branch, force push, delete a tag, delete a tag on a remote, drop a stash,
abort, remove a remote (its remote branches may hold commits that exist nowhere else, such as
branches already deleted on the server), and leaving a detached HEAD whose commits nothing else
reaches (2.1). Nothing else confirms: checkout, create, rename, merge, fast-forward,
cherry-pick, revert, fetch, pull, push, stash, apply, pop, create tag and push tag lose nothing
that the reflog or the stash list does not keep.

### 2.1 Check out a local branch

- Label: "Check out <b>". Double-click on the branch (left panel row or graph label).
- Git: `git switch --no-guess <b>`.
- Shown for every local branch except the one HEAD is on. Never disabled.
- Confirm and backup: none while HEAD is on a branch (uncommitted changes are carried along or
  the switch is refused). **While HEAD is detached** (this rule holds for every checkout: 2.1,
  2.2, 2.3, 2.31 and "Check out and merge"): before the operation is queued, the app runs
  `git rev-list --count HEAD --not --branches --remotes --tags` (a read). When it is above 0,
  it confirms: title "Leave these commits?", question "HEAD is detached on N commits that are on
  no branch. After this checkout they no longer show in the graph.", detail "A backup is saved
  first, so this can be undone.", button "Check out". The operation then saves `detached-<time>`
  → HEAD's commit before the checkout. git's own warning on stderr (`you are leaving N commits
  behind`) is not relied on.
- Refresh: Refs. Success: "Checked out <b>".
- Failure `CheckoutBlockedByLocalChanges`: error dialog "Checkout blocked" with summary "Your
  changes to these files would be overwritten by checking out <b>." and the extra button
  **"Stash changes and check out"** (2.31). Failure `CheckoutBlockedByUntrackedFiles`: error
  dialog "Checkout blocked" with summary "Untracked files are in the way: <b> has files with
  the same names. Move or delete them, then check out again." and no extra button (stashing
  could not bring them back, section 0). Other failures (a merge in progress: `you need to
  resolve your current index first`): error dialog "Checkout failed".

### 2.2 Check out a remote branch (into a tracking branch)

- Label: "Check out <r>/<b>". Double-click on the remote branch.
- Git: when a local branch `<b>` exists: `git switch --no-guess <b>` (it is checked out as is;
  when it tracks `<r>/<b>` and is behind, the success message says "Checked out <b>. It is N
  behind <r>/<b>: pull to update it."). Otherwise: `git switch --create <b> --track refs/remotes/<r>/<b>`.
  A remote branch `origin/feature/x` gives the local name `feature/x`: the part after
  `<r>/`, with `<r>` taken from `GitRef.RemoteName` (`GitRepository.FindRemote`, which already
  handles remote names with slashes).
- Shown for every remote branch. Confirm and backup: as 2.1 (only from a detached HEAD).
- Refresh: Refs. Success: "Created <b> tracking <r>/<b> and checked it out" or as 2.1.
- Failures: as 2.1, including the extra button. When the local name exists but tracks
  something else, it is checked out as is and the success message says what it tracks.

### 2.3 Check out a commit (detached HEAD)

- Label: "Check out this commit"; on a tag: "Check out <t>".
- Git: `git switch --detach <sha>`.
- Disabled when HEAD is already detached at this commit: "HEAD is already at this commit".
- Confirm and backup: as 2.1 (only from a detached HEAD). Refresh: Refs. Success: "HEAD is now
  detached at a1b2c3d". The status bar's branch shows "Detached at a1b2c3d" (phase 1).
- Failures: as 2.1, including the extra button.

### 2.4 Create a branch

- Labels: "Create branch here…" on a commit, a branch, a remote branch or a tag; "Create
  branch…" on the "Local branches" header and the empty graph area; "Create branch at HEAD…"
  on the toolbar's Branch button. Opens the name dialog (4f) with "Check out the new branch"
  ticked.
- Git: without checkout `git branch <name> <start>`; with checkout `git switch --create <name> <start>`.
  `<start>` is the full SHA for a commit, `refs/heads/<b>`, `refs/remotes/<r>/<b>` or
  `refs/tags/<t>^{commit}`. Starting from a remote branch sets it as upstream, as git does
  (`branch.autoSetupMerge`, Q5).
- Disabled when HEAD is unborn (no commit to start from): "Make the first commit first".
- Name validation (live, D96): not empty; `RefNameRules.IsValidBranchName` (git's
  check-ref-format rules, plus: not starting with `-`, not `HEAD`); no local branch of that
  name; no clash of folders in either direction (`feature` when `feature/alpha` exists, or
  `feature/alpha/x` when `feature/alpha` exists: git refuses both with `cannot lock ref`). On
  Windows and macOS, where refs may live in files, every path component is compared ignoring
  case with the existing local branches (`Feature/beta` next to `feature/alpha` would be stored
  under `feature/` while HEAD names `Feature/beta`). Messages under the box: "Enter a name.", "A
  branch named <name> exists.", "A branch named <existing> exists in another case.", "This
  conflicts with the branch <existing>.", "This is not a valid branch name: <reason>."
- Confirm: no. Backup: none. Refresh: Refs.
- Success: "Created <name>" or "Created <name> and checked it out".
- Failures: error dialog "Creating the branch failed".

### 2.5 Rename a branch

- Label: "Rename <b>…" on a local branch. Name dialog with the old name filled in and selected.
- Git: `git branch --move <b> <new>` (works for the current branch too; the upstream setting
  moves with it).
- Validation as 2.4; the unchanged name disables the button.
- Confirm: no. Backup: none. Refresh: Refs. Success: "Renamed <b> to <new>".
- Failures: error dialog "Renaming the branch failed".

### 2.6 Delete a local branch

- Label: "Delete <b>…" (danger colour).
- Disabled on the branch HEAD is on: "You cannot delete the branch you are on".
- Confirm: title "Delete branch?", question "Delete the branch <b>?" plus, when
  `git merge-base --is-ancestor refs/heads/<b> HEAD` exits 1: " It has commits that <current>
  does not have." (when HEAD is detached: "that HEAD does not have"); button "Delete".
- Backup: `branch-<time>/<b>`. Git: `git branch --delete --force <b>`.
- Refresh: Refs. Success: "Deleted <b>". Failures: error dialog "Deleting the branch failed".

### 2.7 Fast-forward a branch that is not checked out

- Label: "Fast-forward <b> to <r>/<u>" on a local branch with an upstream; from a drop (4g):
  "Fast-forward <B> to <A>".
- Enabled only when the branch is behind and not ahead (`Behind > 0 && Ahead == 0` for the
  upstream; for a drop, `git merge-base --is-ancestor <B> <A>`). Disabled reasons: "<b> is up
  to date with <r>/<u>", "<b> has commits that <r>/<u> does not have".
- On the branch HEAD is on, this item is replaced by "Pull" (2.18). Disabled, with the reason
  "<b> is checked out in another worktree (<path>)", when `git worktree list --porcelain`
  (read with the refs) shows the branch checked out in a linked worktree.
- Git (`FastForwardBranchAsync(branch, target)`): `git fetch . <target full ref>:refs/heads/<b>`
  (no network: `.` is the repository itself). git itself refuses a non-fast-forward
  (`! [rejected] ... (non-fast-forward)`) and a branch checked out in any worktree (`refusing to
  fetch into branch`), so a branch that moved meanwhile, or a worktree the menu did not know
  about, is never damaged. `update-ref` is not used: it moves a branch checked out in another
  worktree, whose files then show the commits as staged reverse changes (section 0). Prompts
  `None`; the call passes `--no-auto-maintenance` and `--no-write-fetch-head`.
- Confirm: no. Backup: none (the old commit stays an ancestor). Refresh: Refs.
- Success: "Fast-forwarded <b> to <target name>". Failure: error dialog "Fast-forward failed"
  (summary "<b> is checked out in another worktree." or "<b> is not behind <target name> any
  more." when git's line says so). `BranchOperationTests` include a scenario with a linked
  worktree.

### 2.8 Merge (fast-forward when possible)

- Label: "Merge <x> into <current>" on a local branch, remote branch (and from a drop). Not on
  the current branch itself.
- Disabled when HEAD is detached: "Check out a branch to merge into"; when a merge,
  cherry-pick, revert or rebase is in progress: "Finish or abort the <kind> first".
- Git, two calls in one operation:
  1. The message, as git would write it for the short name: `git fmt-merge-msg` with one line
     on standard input, `<sha>\t\tbranch '<b>' of .` for a local branch or
     `<sha>\t\tremote-tracking branch '<r>/<b>' of .` for a remote one. It prints "Merge branch
     'feature/alpha'" (plus " into <current>" where git adds it, `merge.suppressDest`, and the
     shortlog when `merge.log` is set), exactly what `git merge feature/alpha` writes.
  2. `git merge --no-edit --no-log -m <message> <full ref>`. The full ref keeps a tag of the same
     name from being merged instead; `-m` keeps the full ref out of the message (git writes the
     name as given: "Merge branch 'refs/heads/feature/alpha'", section 0); `--no-log` keeps the
     shortlog from being added twice. It follows `merge.ff` as configured (Q5): normally a
     fast-forward when possible (the message is then unused), otherwise a merge commit with that
     message; after a conflict `MERGE_MSG` starts with it. No editor opens (D68).
  `BranchOperationTests` pin the subject for a local and a remote branch, from `main` and from
  another branch, and with a tag of the same name present.
- Confirm: no. Backup: none (the reflog has the old position; nothing is discarded).
- Result (`MergeResult`): `UpToDate` → "main is already up to date with <x>"; `FastForward` →
  "Fast-forwarded main to <x>"; `Merged` → "Merged <x> into main"; `Conflicts` → outcome
  `Conflicts`, status bar "Merge stopped: N files have conflicts" (warning colour), no dialog;
  the banner (section 5) takes over.
- Conflict detection (all merging operations): git exited non-zero and the status then lists at
  least one conflicted file. Otherwise it is a failure: error dialog "Merge failed" (for
  example "Your local changes ... would be overwritten by merge", which needs no extra button
  in phase 3).
- Refresh: Refs.

### 2.9 Merge, always creating a merge commit

- Label: "Merge <x> into <current> (create a merge commit)". As 2.8 with `--no-ff` added to
  the merge call.
- Success: "Merged <x> into main".

### 2.10 Fast-forward only

- From a drop onto the current branch: "Fast-forward <current> to <A>". Git:
  `git merge --ff-only <full ref>` (no message is needed). Enabled when `git merge-base
  --is-ancestor refs/heads/<current> <A's full ref>` succeeds (read when the drop menu opens).
- Failure `NotFastForward` cannot normally happen (the item is disabled); if the branch moved
  meanwhile, error dialog "Fast-forward failed".

### 2.11 Reset (soft, mixed, hard)

- Label: "Reset <current> to this commit…" (detached: "Reset HEAD to this commit…") on a
  commit row. Opens the reset dialog (4f): Soft, Mixed (default), Hard.
- Disabled on a stash row (not offered), and when HEAD is unborn.
- Confirm: the reset dialog is the confirmation, and the only one (no second confirmation for
  Hard); its button reads "Reset" and takes the danger style while Hard is chosen.
- Backup: `reset-<time>` → HEAD's commit; for Hard also `reset-<time>-changes`, when there is
  anything to save. A hard reset loses more than the changed tracked files (section 0): it
  deletes files staged as new, and it overwrites, without a word, an untracked file whose path
  the target commit tracks. So the snapshot holds (D67's temporary index, read from HEAD, with
  these paths added as they are in the working tree): every staged or unstaged tracked file,
  every file staged as new, and every untracked file whose path is in
  `git ls-tree -r --name-only -z <target>` (one read). The snapshot commit gets a second parent
  when the real index has no conflicts: a commit of `git write-tree`'s tree of the real index,
  so a file whose staged content differs from its working-tree content keeps both. Untracked
  files that the target does not track are not touched by a reset and are not saved.
  `BackupRefTests` cover each case (an untracked `x` that the target tracks, a staged new file,
  a file with staged and unstaged edits).
- Git: `git reset --soft|--mixed|--hard <sha>` (`--mixed` passes `--quiet` too: otherwise it
  lists the unstaged files on stdout).
- Refresh: Refs. Success: "Reset main to a1b2c3d (soft)" / "(mixed)" / "(hard)".
- Failures: error dialog "Reset failed". Conflict: none.

### 2.12 Cherry-pick one commit

- Label: "Cherry-pick onto <current>" (detached: "Cherry-pick onto HEAD").
- Disabled: on a merge commit: "Merge commits cannot be cherry-picked here"; when the commit is
  already reachable from HEAD (the graph's ancestry, 4h): "This commit is already on <current>";
  when HEAD is unborn; during an operation in progress (as 2.8).
- Git: `git cherry-pick <sha>`.
- Confirm: no. Backup: none.
- Result: `Applied` → "Cherry-picked a1b2c3d onto main"; `Conflicts` → as 2.8 ("Cherry-pick
  stopped: N files have conflicts"). A pick that becomes empty (its changes are already there)
  stops with `CHERRY_PICK_HEAD` and no conflicted file: outcome `Failed`, error dialog
  "Cherry-pick stopped" with the summary "a1b2c3d changes nothing on main: its changes are
  already there. Abort the cherry-pick to go on." and git's output; the banner says the same
  (section 5) and offers Abort. Skip is phase 4's.
- Refresh: Refs.

### 2.13 Cherry-pick several commits

- Label: "Cherry-pick N commits onto <current>" on a multi-selection (4h).
- The commits are the SHAs captured when the menu opened (4h), never row numbers.
- Order: oldest first, by the graph's row order read from the bottom up (D47's date order puts
  every commit after its children, so this applies parents first).
- Git: **one `git cherry-pick <sha>` per commit**, in that order, inside one operation, stopping
  at the first that does not apply cleanly. The app never starts git's sequencer
  (`git cherry-pick A B C`): a sequence stopped by a conflict can only be finished with
  `git cherry-pick --continue` (phase 4), committing in the stage panel concludes only the
  current pick and leaves `sequencer/`, and its Abort would undo the picks already made
  (section 0). One at a time, a stop leaves a plain single pick, which committing in the stage
  panel concludes and Abort undoes alone.
- Disabled when any selected row is a merge commit or a stash ("Merge commits and stashes cannot
  be cherry-picked"), or when any is reachable from HEAD ("Some of these commits are already
  on <current>").
- Result: "Cherry-picked N commits onto main". A stop: outcome `Conflicts` (or `Failed` for an
  empty pick, as 2.12) with the message "Cherry-picked K of N commits. a1b2c3d stopped with
  conflicts in M files; not picked: b2c3d4e "Alpha two"[, …]." The banner shows the single
  pick (section 5); the commits not picked are named in that message, which stays in the status
  bar (its tooltip lists them all), so the user can pick them again after finishing.
- Refresh: Refs.

### 2.14 Revert

- Label: "Revert this commit"; on a merge commit "Revert this merge commit"; on a
  multi-selection "Revert N commits".
- Disabled on a stash; on a multi-selection that holds a merge commit ("Revert merge commits
  one at a time"); during an operation in progress.
- Git: `git revert --no-edit <sha>`; a merge commit `git revert --no-edit -m 1 <sha>` (undoes
  what the merge brought in from its second parent); several: one `git revert --no-edit <sha>`
  per commit, newest first (so each revert applies cleanly on the one before), stopping at the
  first that does not apply cleanly, as 2.13 (no sequencer).
- Confirm: no. Backup: none. Result: "Reverted a1b2c3d" / "Reverted N commits"; conflicts as
  2.8 ("Revert stopped: …") and, for several, as 2.13 ("Reverted K of N commits. … not
  reverted: …"). Refresh: Refs.

### 2.15 Abort an operation in progress

- The banner's button (section 5). Confirm: title "Abort the <kind>?", question per section 5,
  button "Abort <kind>" (danger).
- Backup: `abort-<time>` and, when there are changed tracked files, `abort-<time>-changes`
  (same batch, same timestamp, 2.0). The backup keeps the commits a sequence started in a
  terminal already made, which `git cherry-pick --abort` and `git revert --abort` drop
  (section 5's question says so).
- Git: `git merge --abort` | `git cherry-pick --abort` | `git revert --abort` |
  `git rebase --abort` | `git am --abort`.
- Refresh: Refs. Success: "Aborted the <kind>". Failures: error dialog "Abort failed".

### 2.16 Fetch

- Labels: toolbar Fetch (all remotes); "Fetch <r>" on a remote header; "Fetch all remotes" on
  the "Remotes" header and the empty graph area.
- Git: all: `git fetch --all --progress`; one: `git fetch --progress <r>`. No `--prune` and no
  `--tags`: `fetch.prune` and tag following work as the user configured them (Q5, D99).
- Disabled when the repository has no remotes: "This repository has no remotes".
- Network, prompts Ask, cancellable (1.8: a cancel reports stale locks). Dedupe keys `fetch:*`
  and `fetch:<r>`. Confirm: no. Backup: none.
- Refresh: Refs. Success: "Fetched from all remotes" / "Fetched from <r>".
- Failures: error dialog "Fetch failed" ("Git could not fetch from <r>."); `AuthenticationFailed`
  adds nothing more: git's output says what happened. A prompt cancelled by the user →
  `Cancelled`, no dialog, "Fetch cancelled".

### 2.17 Auto-fetch

- `git fetch --all --progress --no-auto-maintenance` with `PromptMode.Deny` (section 3),
  `IsBackground`, through `TryStartInBackground`; never pre-empted, never killed (1.8). Detail
  in 6.1.
- Refresh: Refs (the ahead and behind counts follow).
- Success: nothing shown. Failure: the status bar says "Auto-fetch failed" in the secondary
  colour, its tooltip git's first error line; logged at Warning. No dialog, ever.

### 2.18 Pull

- Labels: toolbar Pull; "Pull from <r>/<u>" on the current branch; "Pull" on the empty graph
  area.
- Disabled: detached ("HEAD is detached: check out a branch to pull"); no upstream ("<b> has no
  upstream: set one first"); an operation in progress (as 2.8); no remotes (unless the upstream
  is local).
- Git (D84): what `git pull` does, as two steps, so that only the network part can be cancelled
  (a merge prints its first line only after it has written the tree, and a rebase after it has
  checked out the new base, so no line of one `git pull` call can tell when killing it becomes
  unsafe):
  1. **Fetch** (network, prompts Ask, cancellable): `git fetch --progress <r>`, which is the
     fetch `git pull` itself runs (no refspec: `FETCH_HEAD` marks the upstream branch for
     merge). Skipped for a local upstream. Then `EndCancelWindow()`.
  2. **Integrate** (local, not cancellable), chosen as `git pull` 2.36 chooses, from three
     `git config --get` reads (exit 1 means unset):
     - `pull.ff` is `only` → `git merge --ff-only FETCH_HEAD` (it wins over a rebase setting,
       as in git: section 0).
     - else `branch.<b>.rebase`, or when unset `pull.rebase`, is `true`, `interactive` or `i` →
       `git rebase` (no arguments: the configured upstream, in fork-point mode, as pull's);
       `merges` or `m` → `git rebase --rebase-merges`. (`interactive` runs without stopping:
       `GIT_SEQUENCE_EDITOR=:`, D102.)
     - else (unset or false) → `git merge --no-edit [--no-ff when pull.ff=false] [--ff when
       pull.ff=true] FETCH_HEAD`: git's own pull message ("Merge branch 'main' of <url>",
       section 0). With nothing set this merges, as the owner chose (D78), also where Git 2.33.1
       and later would refuse diverged branches.
     - a value the app does not know (`preserve`, removed in Git 2.34, or a typo) → failure
       "Pull failed": "pull.rebase is set to <v>, which this app does not understand."
     For a local upstream, `UpstreamRef` stands in for `FETCH_HEAD` (the merge message is then
     git's for that branch, built as in 2.8).
  `PullParityTests` run the app's pull and `git pull` on copies of the same diverged and
  fast-forwardable repositories for each setting (none, `pull.rebase` true/merges,
  `branch.<b>.rebase`, `pull.ff` only/false/true, `pull.ff=only` with `pull.rebase=true`) and
  compare the resulting HEAD, its parents' trees and the subject; CI runs them on Git 2.55, so
  a change in git's rules shows up there. Not mirrored, and named in the risks: submodule
  recursion and `--autostash` given on pull's command line (git rebase and git merge read
  `rebase.autoStash` and `merge.autoStash` themselves).
- Dedupe key `pull:<full branch ref>`.
- Result, from `git rev-list --count <HEAD before>..HEAD` after the operation (not the behind
  count, which predates the fetch): HEAD unchanged → "main is already up to date" (whichever
  path ran); a fast-forward → "Pulled N commits into main (fast-forward)"; a merge commit →
  "Pulled and merged <r>/<u> into main"; a rebase → "Pulled and rebased main onto <r>/<u>";
  conflicts → as 2.8 ("Pull stopped: N files have conflicts"); a rebase that stopped leaves a
  rebase in progress: the banner offers Abort and says that continuing needs a terminal until
  phase 4 (section 5).
- Failures: error dialog "Pull failed" (for `pull.ff=only` on diverged branches git says
  `Not possible to fast-forward`); a cancelled fetch → "Pull cancelled", nothing integrated.
- Refresh: Refs.

### 2.19 Push

- Labels: toolbar Push (the current branch); "Push <b> to <r>/<u>" on a local branch with an
  upstream; "Push <b>…" on one without (push dialog, 4f); "Push" on the empty graph area.
- Disabled: detached (toolbar: "HEAD is detached: check out a branch to push"); no remotes
  ("Add a remote first"); HEAD unborn ("Make the first commit first": git would say `src
  refspec does not match any`). Left out for a branch with a local upstream (D101).
- Git, with an upstream: `git push --progress <UpstreamRemote> refs/heads/<b>:<UpstreamRemoteRef>`;
  from the push dialog: `git push --progress [--set-upstream] <r> refs/heads/<b>:refs/heads/<name>`.
  Hooks run (Q5); `push.followTags` works as configured.
- Network, prompts Ask, cancellable. Dedupe key `push:<full branch ref>-><r>/<remote ref>`.
- Refresh: Refs. Success: "Pushed <b> to <r>/<u>" (+ ", and set it as upstream").
- Failures: `PushRejected` → error dialog "Push rejected": summary "<r> has commits that your
  <b> does not have. Pull first, then push again." The extra button **"Pull"** (2.18; the user
  pushes again afterwards) is offered only when <b> is the current branch and the push went to
  its upstream; otherwise the extra button is **"Fetch <r>"** (2.16), and the summary names <b>.
  Others: error dialog "Push failed".

### 2.20 Force push with lease

- Label: "Force push <b>…" on a local branch with a remote upstream (danger colour). Disabled
  when the upstream is gone: "<r>/<u> no longer exists: push normally".
- Confirm: title "Force push <b>?", question "Replace <b> on <r> with your <b>? Commits on <r>
  that your branch does not have are dropped there. The push stops if <r> has commits you have
  not integrated into your <b>, even ones the last fetch brought.", button "Force push".
- Backup: `force-push-<time>/<r>/<u>` → `UpstreamRef`'s commit, read inside the operation just
  before the push.
- Git: `git push --progress --force-with-lease=<UpstreamRemoteRef> --force-if-includes <r>
  refs/heads/<b>:<UpstreamRemoteRef>`. The lease alone is not enough: its expected value is the
  remote-tracking ref, which auto-fetch (D79) moves quietly every 5 minutes, so a lease would
  match commits the user has never seen and drop them. `--force-if-includes` (Git 2.30) also
  requires that the remote-tracking ref's commit is in the local branch's history or reflog,
  so a fetched commit that was never integrated stops the push (section 0). Git ignores
  `--force-if-includes` when the lease names an explicit value, so none is given.
- Network, Ask, cancellable. Refresh: Refs. Success: "Force-pushed <b> to <r>/<u>".
- Failures: `PushRejectedStale` → error dialog "Force push stopped": "<r>/<u> has commits that
  your <b> does not include. Pull or look at them before forcing." Others: "Force push failed".

### 2.21 Set or remove the upstream

- Labels: "Set upstream…" (no upstream) or "Change upstream…" (has one): the pick dialog (4f)
  with the remote branches; "Stop tracking <r>/<u>".
- Git: `git branch --set-upstream-to=refs/remotes/<r>/<x> <b>`; `git branch --unset-upstream <b>`.
- Disabled "Set upstream…" when there are no remote branches: "No remote branches to track:
  fetch or push first".
- Confirm: no. Backup: none. Refresh: Refs (ahead/behind change). Success: "<b> now tracks
  <r>/<x>" / "<b> no longer tracks <r>/<u>".

### 2.22 Delete a branch on a remote

- Label: "Delete <b> on <r>…" on a remote branch `<r>/<b>` (danger).
- Confirm: title "Delete <b> on <r>?", question "Delete the branch <b> on <r>? Everyone who uses
  <r> loses it.", button "Delete on <r>".
- Backup: `remote-branch-<time>/<r>/<b>` → `refs/remotes/<r>/<b>`'s commit `<sha>`, read inside
  the operation (a fetch that brought newer commits has put them into `<sha>`, so the backup
  holds them).
- Git: `git push --progress --force-with-lease=refs/heads/<b>:<sha> <r> --delete refs/heads/<b>`.
  The lease makes the backup's promise true: if someone pushed to the branch since the last
  fetch, the deletion is refused (`(stale info)`, section 0) instead of dropping commits that
  the backup does not hold. Network, Ask, cancellable.
- Refresh: Refs. Success: "Deleted <b> on <r>". Failures: `PushRejectedStale` → error dialog
  "Deletion stopped": "<b> on <r> has changed since you last fetched. Fetch and look at it
  before deleting." Others: error dialog "Deleting the remote branch failed".

### 2.23 Manage remotes

All local, through the remotes dialog (4f) or the remote header's menu. Refresh: Refs.

| Action | Git | Validation | Confirm | Success |
|---|---|---|---|---|
| Add | `git remote add <name> <url>`, then, when "Fetch now" is ticked, a separate network operation `git fetch --progress <name>` | name: `RefNameRules.IsValidRemoteName` (check-ref-format of `refs/remotes/<name>/HEAD`), not an existing remote; URL not empty | no | "Added remote <name>" |
| Rename | `git remote rename <old> <new>` | as Add | no | "Renamed remote <old> to <new>" |
| Change URL | `git remote set-url <name> <url>` | URL not empty and changed | no | "Changed the URL of <name>" |
| Remove | first a backup batch `remote-<time>/<name>/<b>` of every `refs/remotes/<name>/*` (2.0), then `git remote remove <name>` | — | title "Remove remote <name>?", question "Remove the remote <name> and its N remote branches? Your local branches stay; those that track it lose their upstream. Its URL was <shown url>.", detail "A backup is saved first, so this can be undone.", button "Remove" (danger) | "Removed remote <name>" |

`ReadRemotesAsync` (new, a read with `GIT_OPTIONAL_LOCKS=0`): `git remote -v`, parsed into
`RemoteInfo(Name, FetchUrl, PushUrl?)` (a `(push)` URL equal to the fetch URL gives null).

URLs can carry a user name and a token (`https://alice:ghp_…@host/x.git`). Everything the app
shows or copies uses the **shown URL**: the remotes list, its tooltip, the Remove question and
the error dialog use `GitCommand.HideCredentials` (`https://***@host/x.git`); "Copy URL" copies
the URL with its user information removed (`https://host/x.git`, a new
`GitCommand.RemoveCredentials`), which can be shared and cloned. Only the Change URL form shows
the URL as configured, because saving it must keep what is there (D103).

### 2.24 Stash save

- Labels: toolbar Stash; "Stash changes…" on the working-changes row, the "Stashes" header and
  the empty graph area. Opens the stash dialog (4f): Message (optional), "Include untracked
  files" (ticked by default; the last choice is kept in settings, 6.2).
- Disabled when there is nothing to stash (no staged or unstaged changes, and no untracked
  files): "There are no changes to stash"; and when HEAD is unborn: "Make the first commit
  first" (git: `You do not have the initial commit yet`).
- Git: `git stash push [--include-untracked] [--message <m>]`. An empty message lets git name it
  ("WIP on main: a1b2c3d Add core"). Exit 0 with `No local changes to save` → message "There
  was nothing to stash".
- Confirm: no. Backup: none (the stash is the backup). Refresh: Refs.
- Success: "Stashed the changes". Failures: error dialog "Stash failed".

### 2.25 Stash apply and pop

- Labels: "Apply stash", "Pop stash" on a stash (left panel row or graph row); double-click a
  stash row in the left panel applies it (a stash row in the graph: as a single click, 4c);
  toolbar Pop pops `stash@{0}`.
- Disabled: toolbar Pop without stashes ("There are no stashes").
- Git: the stash is found again by its SHA at run time (`git stash list --format=%H`, its line
  number is `n`), because the list may have changed since it was drawn; a stash no longer listed
  fails with "This stash no longer exists". Apply: `git stash apply stash@{n}`; pop:
  `git stash pop stash@{n}`.
- Confirm: no. Backup: none (a pop that conflicts keeps the stash; one that applies cleanly
  had nothing more to lose).
- Result: "Applied stash@{n}" / "Popped stash@{n}"; conflicts → outcome `Conflicts`, "Applying
  the stash stopped: N files have conflicts" (pop adds " The stash is kept."). The banner shows
  the conflicts-only form (section 5).
- Refresh: Refs. Failures (`Your local changes ... would be overwritten`): error dialog
  "Applying the stash failed".

### 2.26 Stash drop

- Label: "Drop stash…" (danger).
- Confirm: title "Drop stash?", question "Drop stash@{n} \"<message>\"?", button "Drop".
- Backup: `stash-<time>` → the stash commit. Git: `git stash drop stash@{n}` (found by SHA as
  2.25).
- Refresh: Refs. Success: "Dropped stash@{n}".

### 2.27 Tag create

- Labels: "Create tag here…" on a commit, branch, remote branch; "Create tag at HEAD…" on the
  "Tags" header. Tag dialog (4f): Name, Message (optional; a message makes an annotated tag).
- Validation: `RefNameRules.IsValidTagName`; no tag of that name.
- Git: lightweight `git tag <t> <sha>`; annotated `git tag --annotate --cleanup=whitespace
  --file=- <t> <sha>` with the message on standard input (D68's way: a message such as "#42
  hotfix release" is kept, where git's default cleanup would empty it, section 0; it gets the
  repository's identity, D72 in tests).
- Confirm: no. Backup: none. Refresh: Refs. Success: "Created tag <t>".

### 2.28 Tag delete

- Label: "Delete <t>…" (danger). Confirm: title "Delete tag?", question "Delete the tag <t>
  here? It stays on remotes it was pushed to.", button "Delete".
- Backup: `tag-<time>/<t>`. Git: `git tag --delete <t>`.
- Refresh: Refs. Success: "Deleted tag <t>".

### 2.29 Tag push and delete on a remote

- Labels: "Push <t> to <r>" (one remote) or "Push <t>…" (several: the remote choice dialog);
  "Delete <t> on <r>…" or "Delete <t> on a remote…" (danger).
- Disabled without remotes ("Add a remote first").
- Push: `git push --progress <r> refs/tags/<t>:refs/tags/<t>`. Success: "Pushed tag <t> to <r>".
- Delete: confirm title "Delete <t> on <r>?", question "Delete the tag <t> on <r>? Everyone who
  uses <r> loses it.", button "Delete on <r>"; backup `remote-tag-<time>/<r>/<t>` → the local
  tag object; `git push --progress --force-with-lease=refs/tags/<t>:<local tag object's sha> <r>
  --delete refs/tags/<t>`: the remote's tag is deleted only when it is the tag the backup holds;
  one that differs, or a remote that has no such tag, is refused (`(stale info)` → "Deletion
  stopped": "<r> has no tag <t>, or its <t> is not the same as yours."; without the lease git
  would report `[deleted]` even for a tag the remote never had, checked on 2.36). Success:
  "Deleted tag <t> on <r>". The item is on local tags (the left panel and the graph show no
  others), so the local tag always exists; the app cannot see a remote's tags without asking
  it, so the item is offered for every tag.
- Network, Ask, cancellable. Refresh: Refs.

### 2.30 Ignore a file

- Labels (untracked files only, in the unstaged list and an untracked file's diff): "Ignore
  <file name>" and, when the name has an extension, "Ignore all *.<ext> files". On a tracked
  file both are disabled: "Only untracked files can be ignored".
- Implementation (`AppendToGitignoreAsync(pattern)`, a local operation, no git call): append to
  `<working tree>/.gitignore`, creating it; first a line feed if the file does not end with one.
  Patterns: `/<path>` with `\` put before `*`, `?`, `[`, `\`, a leading `#` or `!`, and
  trailing spaces; or `*.<ext>` with the same escaping.
- Confirm: no. Backup: none. Refresh: Status. Success: "Added /<path> to .gitignore".

### 2.31 Stash changes and check out (owner's answer 4)

- The extra button of the "Checkout blocked" error dialog (2.1, 2.2, 2.3), offered only for
  `CheckoutBlockedByLocalChanges`, that is when git names tracked files and no untracked file
  is in the way (1.6). Untracked files in the way cannot make the round trip: the target tracks
  files of the same names, so popping the stash cannot restore them (section 0). One local
  operation, "Check out <target> with your changes" (confirm and backup as 2.1 when HEAD is
  detached):
  1. `git stash push --message "VisualCommit: before checking out <target>"`, **without**
     `--include-untracked`: the untracked files blocked nothing, and the checkout carries them
     along as git always does. The new entry's SHA is read (`git rev-parse refs/stash`).
  2. The checkout of 2.1, 2.2 or 2.3. If it fails: the stash is popped back at once (step 3's
     way) and the error dialog "Checkout failed" shows git's output.
  3. `git stash pop --index stash@{n}` (the entry found by its SHA, as 2.25), which keeps what
     was staged staged. When it exits non-zero with `conflicts in index. Try without --index.`
     and has changed nothing (section 0), `git stash pop stash@{n}` follows.
- Results:
  - clean → "Checked out <target> and re-applied your changes";
  - the pop conflicts (non-zero, conflicted files in the status) → outcome `Conflicts`, an
    information dialog (the error dialog's layout, title "Changes re-applied with conflicts")
    "Checked out <target>. Re-applying your changes stopped with conflicts in N files. Your
    changes are kept in the stash "VisualCommit: before checking out <target>"." The banner
    shows the conflicts;
  - the pop fails without conflicts (non-zero, no conflicted file: an unexpected case) →
    outcome `Failed`, error dialog "Changes not re-applied": "Checked out <target>, but your
    changes could not be re-applied. They are kept in the stash "VisualCommit: before checking
    out <target>"." with git's output.
  In every case the stash entry is named, so the user knows where the changes are.
- Refresh: Refs. App exit while it runs: 1.8 (the window waits for all three steps).

### 2.32 Open a file, show it in the file manager, copy

- "Open file": `IFileLauncher.OpenAsync(fullPath)` → Avalonia's
  `TopLevel.Launcher.LaunchFileInfoAsync`. Disabled when the file is not in the working tree
  ("The file is not in the working tree"). A commit's file opens the working tree's version.
- "Show in folder" (one label on every platform, Q6): `IFileLauncher.RevealAsync(fullPath)`:
  Windows `explorer.exe /select,"<path>"`; macOS `open -R <path>`; Linux the
  `org.freedesktop.FileManager1.ShowItems` D-Bus call through `dbus-send`, falling back to
  `xdg-open <folder>`. A missing file reveals its folder.
- "Copy path" (the repository-relative path with `/`), "Copy SHA", "Copy SHAs" (one per line,
  in the selection's row order), "Copy branch name", "Copy tag name", "Copy URL":
  `IClipboardService.SetTextAsync` → `TopLevel.Clipboard.SetTextAsync`.
- None of these is an operation in the queue; none touches git. Both services are interfaces in
  `App/Services`, given through `TabServices`. `ShellDriver.Start` injects `FakeFileLauncher`
  and `FakeClipboard` by default, as it injects `FakeFolderPicker`, and exposes them for
  assertions, so the default test run never opens an application or the file manager and never
  touches the clipboard. The real-window pass never invokes these items (it would open the
  owner's file manager or overwrite the owner's clipboard): its menu checks invoke an item
  without side effects outside the window (7.5).

## 3. Credentials, passphrases and ssh prompts (C11)

### 3.1 Mechanism

- **Helper:** a new console project `src/VisualCommit.AskPass` (assembly and executable name
  `visualcommit-askpass`, `OutputType Exe`, references Core only). `VisualCommit.Git` references
  it, so the helper's app host lands next to the app and in every test project's output, as
  `VisualCommit.exe` already does (section 0). `AskPassLocator` finds it next to
  `typeof(AskPassBroker).Assembly.Location`; a missing helper is a startup error in the log and
  every prompt fails (git then reports `could not read Username`). A test in each test project
  asserts that the helper is in its output.
- **What git and ssh are given** for an operation with `PromptMode.Ask` (in addition to D30's
  variables, which stay, `GIT_TERMINAL_PROMPT=0` included):

| Variable | Value |
|---|---|
| `GIT_ASKPASS` | the helper's full path |
| `SSH_ASKPASS` | the helper's full path |
| `SSH_ASKPASS_REQUIRE` | `force` (OpenSSH 8.4 and later use askpass even when a terminal exists) |
| `VISUALCOMMIT_ASKPASS_PIPE` | the session's pipe name, `vc-<16 hex>` (short: on macOS and Linux .NET makes it a Unix socket under the temp folder, whose path is limited to 104 characters) |
| `VISUALCOMMIT_ASKPASS_TOKEN` | the session's secret: 32 random bytes as hex (`RandomNumberGenerator`) |
| `VISUALCOMMIT_OPERATION_ID` | the operation's id |

  These replace D30's last sentence ("Phase 3 replaces the prompt setting with in-app
  dialogs"): the setting stays as the fallback, and prompts reach the app through askpass (D82).
  With `PromptMode.Deny` (auto-fetch): `GIT_ASKPASS` and `SSH_ASKPASS` = the helper,
  `SSH_ASKPASS_REQUIRE=force`, `VISUALCOMMIT_ASKPASS_DENY=1`, `GCM_INTERACTIVE=never`; no pipe
  or token. With `PromptMode.None` (local operations and every read): nothing is added.
- **Paths with spaces:** git runs `GIT_ASKPASS` and OpenSSH runs `SSH_ASKPASS` directly, as a
  program with the prompt as its one argument, not through a shell; a path with spaces works
  (section 0, checked on Windows). No quoting is added.
- **The helper** (about 80 lines, no Avalonia):
  1. `VISUALCOMMIT_ASKPASS_DENY=1`, or no pipe or token → exit 1 at once, printing nothing.
  2. Connect `NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.CurrentUserOnly)`
     within 5 seconds, else exit 1.
  3. Send one UTF-8 line of JSON: `{"token":…,"operation":…,"prompt":<args[0]>,"sshPrompt":<SSH_ASKPASS_PROMPT or null>}`.
  4. Read one line `{"answer":<string or null>}`. Null, a closed pipe or a malformed line →
     exit 1. Otherwise write the answer and `\n` to standard output, exit 0 (for
     `SSH_ASKPASS_PROMPT=confirm` an answer means yes).
- **The server** (`src/VisualCommit.Git/Prompts/AskPassBroker.cs`, implements Core's
  `IGitPromptBroker`, one per app session, created in `AppSession.Start`): a
  `NamedPipeServerStream` loop (up to 4 instances, `PipeOptions.CurrentUserOnly |
  Asynchronous`), one request per connection, lines up to 64 KiB, the token compared with
  `CryptographicOperations.FixedTimeEquals`. `Begin(string title, string repositoryName,
  PromptMode mode)` returns a `PromptScope` (`Id`, `Environment`, `UserCancelled`,
  `Dispose`). A request for an id that has no open scope is answered null. The request is
  classified (3.2) and handed to Core's `IGitPromptHandler.AnswerAsync(GitPrompt, CancellationToken)`;
  disposing the scope cancels that token.
- **Git Credential Manager and other `credential.helper`s keep working unchanged (Q5):** git asks
  its credential helpers first and runs askpass only when none of them had an answer; it then
  tells the helpers to store what worked. GCM may show its own sign-in window for an `Ask`
  operation, as it does for the command line; for auto-fetch `GCM_INTERACTIVE=never` stops it.
  The app never writes `credential.helper`. A user's `core.askPass` is overridden by
  `GIT_ASKPASS` for the app's network operations (accepted: the app's dialog replaces it); a
  `GIT_ASKPASS` or `SSH_ASKPASS` inherited from the environment (VS Code sets one) is overridden
  the same way.
- **ssh:** host-key and passphrase questions go to the helper through `SSH_ASKPASS` with
  `SSH_ASKPASS_REQUIRE=force`; `Deny` makes them fail at once ("Host key verification failed",
  "Permission denied (publickey)"). A user's `core.sshCommand` (plink, TortoisePlink) keeps its
  own prompts. OpenSSH older than 8.4 ignores `SSH_ASKPASS_REQUIRE` and still reads a terminal
  when the app was started from one (7.7, risk 6).

### 3.2 From a prompt to an in-window dialog

`GitPromptClassifier.Classify(prompt, sshPrompt)` (Core, pure, tested with recorded texts):

| Kind | Recognised by | Dialog (4f) |
|---|---|---|
| `Username` | `^Username for '(.+)': $` | Credentials: Username and Password; the answer is the user name, and the password is kept (in memory, for at most 2 minutes, for this scope only) to answer the `Password for '<scheme>://<user>@<host>'` prompt that follows without another dialog |
| `Password` | `^Password for '(.+)': $` | when not answered from the kept value: Credentials with the user name shown and only Password editable |
| `Passphrase` | `^Enter passphrase for (key )?'?(.+?)'?: ?$` or `^Enter passphrase: ?$` | Passphrase |
| `HostKey` | contains `Are you sure you want to continue connecting` | Unknown host: the prompt's text (host, key type, fingerprint) in the code font; answer `yes` |
| `Confirm` | `sshPrompt == "confirm"` | Confirmation with ssh's text; answer `yes` |
| `Notice` | `sshPrompt == "none"` | No dialog: the status bar shows the text (such as "Confirm user presence for key …") until the scope ends; the helper waits for its pipe to close |
| `Other` | anything else | Prompt: the text and one box, masked when the text contains `password`, `passphrase`, `PIN` or `token` (ignoring case) |

`PromptDialogs` (App, `Services/PromptDialogs.cs`) implements `IGitPromptHandler`: it posts to
the UI thread and shows the dialog through the dialog host, on top of any dialog already open
(dialogs stack, 4f, D104: a prompt for a fetch started from the Remotes dialog appears over it).
The dialog's title names the repository and operation ("Fetch from origin — graph"). Within
one operation's scope an answer given for a prompt text is reused for the same text (for at
most 2 minutes), so a pull's or a multi-remote fetch's second contact does not ask twice. Cancel, Esc, or the scope ending → null, which makes the helper exit 1; the scope
records `UserCancelled` for Cancel and Esc, so the queue reports `Cancelled` instead of
an error (2.16). Answers are never logged, never put in a `GitCommand` and never kept beyond the
2-minute password case; the call log (`IGitCallLog`) records command lines, not environments.

### 3.3 Which calls get the helper

| Calls | Mode |
|---|---|
| User-started network operations: fetch, pull, push in every form, delete a remote branch or tag, push a tag, add a remote with "Fetch now" | `Ask` |
| Clone on the welcome page (outside the queue: `WelcomeViewModel` opens a scope through `TabServices.Prompts` and passes its environment to `IRepositoryProvider.CloneAsync`, which gains an optional `IReadOnlyDictionary<string, string?>? environment` parameter). This fixes phase 1's known issue: an ssh clone no longer waits on the terminal | `Ask` |
| Phase 3's local operations (checkout, merge, reset, cherry-pick, revert, stash, abort …: filters and hooks such as git-lfs may ask, 1.5) | `Ask`, not cancellable |
| Auto-fetch | `Deny` |
| Every read, phase 2's writes, the watcher's `check-ignore`, the fast-forward's `git fetch .` | `None` |

### 3.4 Security of the channel

The pipe is created for the current user only (`CurrentUserOnly`: an ACL on Windows, the
socket's owner check on macOS and Linux); the token, generated per app session and passed only
in the environment of the git processes the app starts, must match; the operation id must
belong to an open scope, so a stale or forged request gets nothing; one request per
connection, bounded in size. The trust boundary is the user account, as for GCM: another
process of the same user that reads git's environment could ask, and would only get an answer
by showing the user a dialog that names the operation.

### 3.5 How it is tested

| What | Test | Where |
|---|---|---|
| Classification | recorded prompt texts of git 2.30 to 2.55 and OpenSSH 8.x/9.x | `Git.Tests/PromptClassifierTests` |
| Helper and server end to end, as git runs them | `git credential fill` with `GIT_ASKPASS` = the real helper (and a copy of the helper's folder under a path with spaces); a scripted `IGitPromptHandler` answers alice/s3cret → git prints them; a wrong token, an unknown operation id, a null answer, a closed scope, `DENY=1` → git fails with `could not read Username` | `Git.Tests/AskPassTests` |
| HTTPS sign-in against a real server | `AuthRemoteServer` (7.3) demands Basic auth: fetch, pull and push with the scripted handler succeed; a wrong password fails as `AuthenticationFailed`; a cancelled prompt gives `Cancelled`; `Deny` fails without calling the handler | `Git.Tests/AuthenticatedRemoteTests` |
| ssh passphrase through `SSH_ASKPASS_REQUIRE` | `ssh-keygen -y -f <key encrypted with s3cret>` with the `Ask` environment and standard input closed: the handler receives a `Passphrase` prompt and the public key comes out. `ssh-keygen` is found next to git on Windows (`<git>/../usr/bin`) and on PATH elsewhere; `scripts/setup-linux.sh` installs `openssh-client` | `Git.Tests/AskPassTests` |
| The dialogs | headless: fetch from the auth server, the credentials dialog appears, typing and "Sign in" complete the fetch; Cancel ends it as cancelled | `App.Tests`, scripted check |
| Passphrase, unknown-host and generic prompt dialogs, as ssh would reach them | the real helper is run as OpenSSH runs it (the prompt as its argument, `SSH_ASKPASS_PROMPT` set as ssh sets it, with the `Ask` environment of an open scope) with recorded OpenSSH texts: `Enter passphrase for key '/home/u/.ssh/id_ed25519': `, the host-key question, `confirm` and an unknown text. The app's broker and `PromptDialogs` show the real dialog, the script types and confirms, and the helper prints the answer. No ssh server is needed | `App.Tests` (`PromptDialogTests`), scripted check 21b |
| The real app's own helper, real process environment | the real-window pass fetches from `AuthRemoteServer` and signs in through the dialog | real-window check |

Only by hand: an ssh host-key question from a real ssh server (the dialog itself is checked
with recorded texts above), and GCM's own window (neither can run without a network service).
The phase 3 report lists them in its manual checklist: clone
`git@github.com:<any public repo>.git` with an empty `known_hosts` (the "Unknown host" dialog
appears), and fetch an HTTPS remote with GCM installed.

## 4. UI inventory

### 4a. The in-window context menu

**What is fixed and what a spike decides (D86).** Fixed: menus are data (`MenuModel`, below),
built by pure functions; they are drawn **inside the main window**, never as a top-level window
of their own; they have a header row, disabled items with their reason as a tooltip, and open
at a given point (the pointer, a drop, a keyboard row). The reason for "inside the window": on
the desktop platforms Avalonia draws a popup as a window of its own by default
(`OverlayPopups` is false for Win32, X11 and macOS, true headless; section 0), and such a menu
would be outside `RealApp.Window`'s UI Automation tree and could reach beyond the client area
the real-window pass captures, so the two harnesses would see different things. Avalonia 12.1.3
can put a single popup on the window's overlay layer (`Popup.ShouldUseOverlayLayer`, and
`MenuFlyout.Popup` is public), so turning `OverlayPopups` on for the whole app is not needed
either way.

Open: which control draws the menu. **Before WP5 starts**, the lead runs a short spike on the
Windows machine (a small throwaway window, not the real-window pass; it does not take over the
mouse): a stock `MenuFlyout` whose `Popup.ShouldUseOverlayLayer` is true, opened with
`ShowAt(target, showAtPointer: true)` and at a point after a drop. It is adopted if (1) it is
drawn inside the client area, also near the right and bottom edges; (2) it and its items are in
the window's UI Automation tree, the items as MenuItem with name, enabled state and Invoke;
(3) a header entry and a disabled item with a `ShowOnDisabled` tooltip can be built from
`MenuModel`; (4) it opens at an arbitrary window point. Then WP5 is a `MenuModel` → `MenuItem`
adapter plus Shift+F10, and the stock keyboard handling and accessibility come with it.
Otherwise WP5 builds `InWindowMenu` as described below. The spike's result is written into this
section and recorded as a new row of `decisions.md` (as D86 requires) before WP5 starts. Either way the menu layer, the automation ids
and the behaviour listed below hold, and `InWindowMenuTests` test them. Tooltips stay
Avalonia's; the same spike checks that a tooltip inside the client area appears in the
real-window pass's capture (assumed so far from phase 1's report) and, if not, sets
`ToolTip.ShouldUseOverlayLayer` for the app.

**Model** (`ViewModels/Menus/MenuModel.cs`, no Avalonia types):

```csharp
public abstract record MenuEntry;
public sealed record MenuHeader(string Text, string? ToolTip = null) : MenuEntry;
public sealed record MenuSeparator : MenuEntry;
public sealed record MenuItem(string Label, Action Invoke, bool IsEnabled = true,
    string? DisabledReason = null, bool IsDestructive = false) : MenuEntry;
public sealed record MenuModel(IReadOnlyList<MenuEntry> Entries);
```

Builders (`ViewModels/Menus/RepositoryMenus.cs`): pure functions of the repository's state
(refs, status, in-progress state, selection, remotes) that return a `MenuModel`, unit-tested
without a UI. Items that open a dialog end in "…".

**Control (when the spike does not adopt the stock flyout):** `Controls/InWindowMenu.cs` (a
templated control showing a `MenuModel`) inside
`Views/Shell/MenuLayer` (a `Panel` in `MainWindow.axaml` over the main grid and under the
dialog layer; `MenuLayerViewModel` on `MainWindowViewModel.Menu`, with `Open(MenuModel,
Point windowPosition)` and `Close()`).

- Look: background `VcPanelBackgroundBrush`, 1 px `VcBorderBrush`, corner radius 6, padding 4,
  shadow `VcMenuShadow` (new token, 6.4). Width 200 to 360 (the longest label decides); an item
  is 26 high with 10 px side padding, a separator 9 high (a 1 px line), the header 24 high in
  `VcTextSecondaryBrush`, cut with "…" and a tooltip. Item text `VcTextPrimaryBrush`; a
  destructive item `VcDangerBrush`; a disabled item `VcTextDisabledBrush` with its reason as a
  tooltip (`ToolTip.ShowOnDisabled="True"`); the item under the pointer or the keyboard
  `VcHoverBackgroundBrush`.
- Position: top-left at the pointer + (2, 2). If it would cross the window's right edge it opens
  to the left of the pointer; past the bottom, above it; then it is moved to stay 4 px inside
  the window. Taller than the window less 8 px: it scrolls.
- Light dismiss: a press of any mouse button outside the menu closes it and is not passed on;
  so do Esc, a tab switch, the window losing activation (tested by unit test of the handler:
  headless windows never lose activation), a dialog opening, and a scroll of the graph. Only
  one menu is open at a time.
- Keyboard: on opening it takes the focus with nothing highlighted; Down/Up highlight the next
  or previous enabled item (wrapping, skipping separators, the header and disabled items),
  Home/End the first or last; Enter or Space invokes; Esc closes and gives the focus back to
  the element that opened it. `MainWindow.OnEscape` closes an open menu before a dialog or a
  diff.
- Invoking: the item's click (pointer released on it) closes the menu first, then posts
  `Invoke` to the UI thread.
- Opening: views handle Avalonia's `ContextRequested` routed event (a right-click or the Menu
  key). Avalonia does not raise it for Shift+F10 (section 0), so `MainWindow` handles Shift+F10
  in a tunnelling `KeyDown` (handled events too) and raises `ContextRequestedEventArgs` without
  a position on the focused element. With a pointer position the menu opens there; from the
  keyboard it opens at the bottom-left of the focused row (the graph: the selected row's message
  cell). `InWindowMenuTests` assert that Shift+F10 and the Menu key both open the graph's
  commit menu headless.
- Accessibility and harnesses: the menu has automation id `ContextMenu` and control type Menu;
  the header `ContextMenuHeader`; each item `ContextMenuItem` with its label as name (D49's
  rule: buttons carry their text as their name), control type MenuItem, `IsEnabled` as drawn.
  A `Panel` or templated control is not a UI Automation control element by default (section 0),
  so the menu container, each dialog root (4f), the conflict banner and `MultiSelectionView`
  get an automation peer with `IsControlElementCore() => true` and a control type (Menu, Pane,
  Pane, List), as `CommitGraphControl`, `DiffPane` and `DiffTextView` already have. One headless
  test walks `ControlAutomationPeer.CreatePeerForElement(window)` and asserts that each of these
  ids is a control element.

### 4b. Menus for every object

Order as listed; "—" is a separator. The header is the first entry of every menu. Items for
later phases are left out (owner's answer 1: D77, D92). "current" is the branch HEAD is on.
A menu acts on the objects captured when it opened (SHAs and full ref names), never on row
numbers. A right-click on a file row opens its menu only: it does not change the list's
selection and does not open the file's diff (D63's click does).

**Commit** (one selected commit, right-click on its row outside its labels). Header:
`a1b2c3d Add core`.
1. Check out this commit (2.3)
2. Create branch here… (2.4)
3. Create tag here… (2.27)
— 
4. Cherry-pick onto main (2.12)
5. Revert this commit / Revert this merge commit (2.14)
6. Reset main to this commit… (2.11)
— 
7. Copy SHA

A right-click on a row that is part of a multi-selection opens the menu for several commits; on
a row outside it, the row is selected alone first.

**Several commits** (two or more selected, 4h). Header: `3 commits`.
1. Cherry-pick 3 commits onto main (2.13)
2. Revert 3 commits (2.14)
— 
3. Copy SHAs

**Stash** (a stash row in the graph, a stash in the left panel, a `stash@{n}` label). Header:
`stash@{0} On main: Experiment`.
1. Apply stash (2.25)
2. Pop stash (2.25)
3. Drop stash… (2.26, danger)
— 
4. Copy SHA

**Working-changes row.** Header: `Working changes`.
1. Stage all (disabled without unstaged files: "Nothing to stage")
2. Unstage all (disabled without staged files: "Nothing staged")
3. Discard all changes… (phase 2's Discard all, danger; disabled without unstaged files)
— 
4. Stash changes… (2.24)
5. Create branch here… (2.4 from HEAD; the changes follow the new branch)
— 
6. Show in folder (the working tree's folder)

**Local branch** `<b>` (left panel row, or its label in the graph). Header: `<b>`.
1. Check out <b> (2.1; left out on the current branch)
2. Merge <b> into <current> (2.8; left out on the current branch)
3. Merge <b> into <current> (create a merge commit) (2.9; left out on the current branch)
4. On the current branch with an upstream: Pull from <r>/<u> (2.18). On another branch with an
   upstream: Fast-forward <b> to <r>/<u> (2.7). Without an upstream: left out.
— 
5. Push <b> to <r>/<u> (with a remote upstream) or Push <b>… (without an upstream) (2.19; left
   out with a local upstream)
6. Force push <b>… (2.20; only with a remote upstream; danger)
— 
7. Create branch here… (2.4)
8. Create tag here… (2.27)
9. Rename <b>… (2.5)
10. Delete <b>… (2.6, danger)
— 
11. Set upstream… / Change upstream… (2.21)
12. Stop tracking <r>/<u> (2.21; only with an upstream)
— 
13. Copy branch name

**Remote branch** `<r>/<b>`. Header: `<r>/<b>`.
1. Check out <r>/<b> (2.2)
2. Merge <r>/<b> into <current> (2.8)
3. Merge <r>/<b> into <current> (create a merge commit) (2.9)
— 
4. Create branch here… (2.4)
5. Create tag here… (2.27)
— 
6. Delete <b> on <r>… (2.22, danger)
— 
7. Copy branch name

**Remote** (the remote's row in the left panel). Header: `<r>`.
1. Fetch <r> (2.16)
— 
2. Change URL… (2.23)
3. Rename <r>… (2.23)
4. Remove <r>… (2.23, danger)
— 
5. Copy URL
6. Manage remotes… (the remotes dialog)

**Tag** `<t>` (left panel or graph label). Header: `<t>`.
1. Check out <t> (2.3)
2. Create branch here… (2.4)
— 
3. Push <t> to <r> / Push <t>… (2.29)
4. Delete <t>… (2.28, danger)
5. Delete <t> on <r>… / Delete <t> on a remote… (2.29, danger)
— 
6. Copy tag name

**Section headers** of the left panel: "Local branches": Create branch…; "Remotes": Add remote…,
Fetch all remotes, Manage remotes…; "Tags": Create tag at HEAD…; "Stashes": Stash changes…;
"Pull requests": no menu (phase 7). **Folder rows** (`feature/`) have no menu: they are not
objects of the repository, only groups of names. **Repository tabs** have no menu in phase 3:
a tab is a place in the window, not an object of the repository, and its close button and the
working-changes row's "Show in folder" cover what a tab menu would offer (D92).

**File in the unstaged list.** Header: the file name.
1. Stage (a conflicted file: "Mark as resolved", which is the same `git add`)
2. Discard changes… (phase 2's discard; disabled on a conflicted file: "Resolve the conflict
   first"; on a nested repository: "A repository of its own is not discarded")
— 
3. Ignore <file name> (2.30)
4. Ignore all *.<ext> files (2.30; left out when the name has no extension)
— 
5. Open file (2.32)
6. Show in folder
7. Copy path

**File in the staged list.** Header: the file name. 1. Unstage — 2. Open file 3. Show in folder
4. Copy path.

**In the diff view** (right-click on the text or the header). Header: the file name.
- Unstaged diff: 1. Stage selected lines 2. Discard selected lines… (both only while lines are
  selected) — 3. Stage hunk 4. Discard hunk… (only when the right-click was inside a hunk;
  they act on that hunk) — 5. Stage file 6. Discard file… — for an untracked file, the two
  Ignore items — 7. Open file 8. Show in folder 9. Copy path. A right-press does not change the
  text's selection (`DiffPane` already ignores every button but the left).
- Staged diff: Unstage selected lines, — Unstage hunk, — Unstage file, — Open file, Show in
  folder, Copy path.
- A commit's diff: Open file, Show in folder, Copy path.
- The menu's actions are the diff view's existing actions (phase 2, D76 and D70); hunk and line
  actions ignore a click while one runs, as now.

**File in a commit's changed files** (details panel). Header: the file name. 1. Open file
2. Show in folder 3. Copy path.

**Empty graph area** (below the last row, or a graph without commits). Header: the repository's
name. 1. Fetch all remotes 2. Pull 3. Push — 4. Create branch… 5. Stash changes… — 6. Manage
remotes…. Enabled as the toolbar's buttons (4d).

**Graph labels:** a right-click on a label opens the menu of what it stands for (local branch,
remote branch, tag, stash; `HEAD` → the commit menu); the "+N" label opens the commit's menu.
The row is selected too.

### 4c. Double-click map

| Where | Result |
|---|---|
| Local branch (left panel row or graph label) | Check out (2.1); on the current branch nothing more than the click's selection |
| Remote branch (row or label) | Check out into a tracking branch (2.2) |
| Stash (left panel row) | Apply (2.25) |
| Tag, commit row, stash row in the graph, working-changes row, remote row, section header, folder | As a single click (select, or open and close); no checkout of a detached HEAD by accident |
| File (stage lists, commit's files) | Open its diff (D63, unchanged) |

The first click of a double-click selects as a single click does; the second runs the action.
Left panel rows use the button's `DoubleTapped`; the graph control raises `LabelDoubleTapped`.
A double-click on a row `Button` also runs its `Command` twice (section 0): the row's command
is idempotent (selecting), so this is harmless, and the double-click action is started from
`DoubleTapped` only, never from the command.

### 4d. Toolbar

The buttons bind to `ActiveTab.Toolbar` (`ToolbarViewModel`, one per `RepositoryViewModel`; an
empty tab has one whose commands are all disabled, so phase 0's checks are unchanged: only Theme
enabled). No dropdowns. A disabled button's tooltip gives its reason
(`ToolTip.ShowOnDisabled`).

| Button | Action | Disabled when (tooltip) |
|---|---|---|
| Undo, Redo, Search | Placeholders until phase 5 | always (no tooltip change) |
| Fetch | 2.16, all remotes | no repository; no remotes ("This repository has no remotes") |
| Pull | 2.18, the current branch from its upstream | no repository; detached; no upstream; operation in progress; no remotes |
| Push | 2.19, the current branch: to its upstream, or the push dialog | no repository; detached; HEAD unborn; no remotes; a local upstream ("<b> tracks a local branch") |
| Branch | 2.4, the name dialog at HEAD, "Check out the new branch" ticked | no repository; HEAD unborn |
| Stash | 2.24, the stash dialog | no repository; HEAD unborn; nothing to stash |
| Pop | 2.25, pops `stash@{0}` | no repository; no stashes |

While an operation runs the buttons stay enabled and a click queues its operation; a second
click on Fetch, Pull or Push while that same operation waits or runs does nothing (its
`DedupeKey`, 1.2). Branch, Stash and Pop open a dialog or act on a changing list, so they are
never deduplicated.

### 4e. Status bar

Left to right (`StatusBarView`): the branch icon and `CurrentBranch` (unchanged);
`OperationStatus` (TextBlock); `OperationProgress` (ProgressBar 80×4, visible only while a
non-background operation runs; indeterminate without a percentage); `OperationCancelButton`
("Cancel", `VcFlatButton`, visible only while `CanCancel`, tooltip "Stop git");
`OperationWaiting` ("1 waiting", secondary, visible when operations wait). The right side is
unchanged. Hidden elements are not visible, so phase 0's text list ("No repository", "Ready",
"Git …", "Activity log") stays.

`OperationStatus` shows the active tab's `OperationStatusViewModel.Text` (an empty tab:
"Ready"):

| State | Text | Colour |
|---|---|---|
| Idle, nothing done yet, or after a stage-panel operation | Ready | secondary |
| Waiting behind an auto-fetch | Waiting for auto-fetch… | secondary |
| Running | `RunningText`, then the progress's `Stage Percent%` when there is one ("Fetching from origin… Receiving objects 45%"); pull and push show their progress the same way | secondary |
| Succeeded | the success message of section 2 | secondary |
| Conflicts | the conflict message | `VcWarningBrush` |
| Failed | "<Title> failed" | `VcDangerBrush` |
| Cancelled | "<Title> cancelled" | secondary |
| Auto-fetch failed | "Auto-fetch failed" with git's first error line as tooltip | secondary |

The message stays until the next operation of that repository starts (no timer, so screenshots
are stable). A background operation does not change the text unless it fails.

### 4f. Dialogs

All are drawn by the dialog layer (D69) on phase 2's card (max width 400, 460 for those with
lists; padding 20; backdrop `VcBackdropBrush`). **Dialogs stack (D104):** `DialogHostViewModel`
keeps a stack instead of phase 2's one-at-a-time semaphore. A dialog opened while another is
open goes on top, over a second backdrop; the one below stays drawn, takes no input and gets
the focus back when the top one closes. This is what lets the Remotes dialog ask its Remove
confirmation, a prompt appear for a fetch started from it, or a credential prompt appear over
an open error dialog; with one-at-a-time, the Remotes dialog's own confirmation would wait for
the Remotes dialog to close. `Current` is the top dialog; Esc cancels the top one only.
`DialogTests` open the Remotes dialog, click Remove and assert the confirmation is shown on top.
`DialogHostViewModel.Current` becomes a `DialogViewModel` (base class with `Title`,
`CancelCommand`, a completion task); the view picks a template by type through a view locator
(`DialogViewLocator`: view model type → view type, registered in code by each package, so no
package edits a shared XAML file for its dialogs); `ConfirmAsync` stays as it is. Tab cycles
inside the top card; Esc cancels; Enter
activates the confirm button unless the focus is in a multi-line box. Every dialog's buttons are
`CancelButton` (left, `VcActionButton`) and `ConfirmButton` (right; `VcPrimaryButton`, or
`VcDangerButton` for destructive ones), so both harnesses find them by the same ids. The first
text box takes the focus on opening (the confirmation keeps focusing Cancel).

| Dialog (automation id) | Title | Contents | Confirm button | Validation |
|---|---|---|---|---|
| Confirmation (`ConfirmationDialog`) | per operation | phase 2's: `ConfirmTitle`, `ConfirmQuestion`, `ConfirmDetail` | per operation | — |
| Branch name (`NameDialog`) | "Create branch" / "Rename branch" | `NameDialogContext` ("From a1b2c3d "Add core"", "From origin/feature/x", "Rename feature/alpha"); `NameBox` (placeholder "Branch name"); `NameError` (danger text, empty when valid); `NameDialogOption` check box "Check out the new branch" (create only, ticked) | "Create branch" / "Rename" | 2.4; the button is disabled while invalid |
| Tag (`NameDialog`) | "Create tag" | context "At a1b2c3d "Add core""; `NameBox` (placeholder "Tag name"); `MessageBox` (multi-line, 3 lines, placeholder "Message (optional: makes an annotated tag)") | "Create tag" | 2.27 |
| Stash (`NameDialog`) | "Stash changes" | `MessageBox` single line (placeholder "Message (optional)"); `NameDialogOption` "Include untracked files" | "Stash" | — |
| Reset (`ResetDialog`) | "Reset main to a1b2c3d?" | text "Move main to "Add core" (a1b2c3d)."; three radio buttons `ResetSoft`, `ResetMixed` (checked), `ResetHard`, each with a secondary line: Soft "Keep every change; the changes of the commits after it are staged.", Mixed "Keep every change; nothing is staged.", Hard "Discard every change to tracked files." (danger colour); `ConfirmDetail` "A backup is saved first, so this can be undone." | "Reset" (danger style while Hard is chosen); no second confirmation follows | — |
| Pick (`PickDialog`) | "Set upstream for <b>" | `PickFilter` box; `PickList` of remote branches (`PickItem`, name = the branch), the current upstream selected | "Set upstream" | an item must be selected |
| Remote choice (`ChoiceDialog`) | "Push <t> to…" / "Delete <t> on…" / push dialog's remote | radio buttons `ChoiceOption` (name = remote), the first checked | per operation | — |
| Push (`PushDialog`) | "Push <b>" | "To" — the remote as text when there is one, else `ChoiceOption` radios; `PushBranchBox` (the remote branch's name, default `<b>`); `PushSetUpstream` "Set as upstream" (ticked) | "Push" | branch name valid |
| Remotes (`RemotesDialog`) | "Remotes" | a list: per remote a `RemoteRow` (name = the remote) with its name, shown fetch URL (credentials hidden, 2.23; cut with "…", tooltip), `RemoteEditButton` ("Edit") and `RemoteRemoveButton` ("Remove", danger); below, `AddRemoteButton` ("Add remote"). Edit and Add switch the card to a form: `RemoteNameBox`, `RemoteUrlBox` (the URL as configured), `RemoteFetchOption` "Fetch now" (Add only, ticked), `RemoteError`, buttons Cancel and "Save" (`ConfirmButton`). Save runs the add, rename or set-url operation and waits for it (the form's buttons disabled, "Saving…"), then returns to the list, or shows git's message in `RemoteError`. "Fetch now" queues the fetch as its own operation and the dialog does not wait for it; a credential prompt for it stacks over the dialog. Remove opens the confirmation of 2.23 on top (stacking) and, after it, runs the removal and refreshes the list | list mode: "Close" (`CancelButton` only) | 2.23 |
| Error (`ErrorDialog`) | "<Title> failed", "Checkout blocked", "Push rejected", "Force push stopped", "Deletion stopped", "Changes re-applied with conflicts", "Changes not re-applied", "<Title> cancelled" (stale locks, 1.8) | `ErrorSummary` (the summary, wrapped); `ErrorRepository` ("In <repo name>", secondary); `ErrorOutput` (git's output in the code font, read-only, selectable, scrolls, max height 220); `ErrorCommand` (the command line, secondary, small, cut with "…" and a tooltip) | buttons: `ErrorCopyButton` ("Copy output"), optional `ErrorExtraButton` ("Stash changes and check out", "Pull", "Fetch <r>", "Remove lock files"), `ConfirmButton` "Close" (primary) | — |
| Finishing (`FinishingDialog`) | "Finishing operations" | text "VisualCommit closes when these operations end."; one line per running operation (`FinishingItem`: "<Title> — <repo>", and "Its remaining steps will not run if you quit now" under a local operation of several steps) | `CancelButton` "Keep open" (closes the dialog; the window stays and the operations go on) and `ConfirmButton` "Quit now" (danger) | — |
| Credentials (`CredentialsDialog`) | "Sign in to <host>" | text "Git needs a user name and a password or token for <url>." (`CredentialsText`); `CredentialUsername` (or the user as text for a `Password` prompt); `CredentialPassword` (masked) | "Sign in" | user name not empty |
| Passphrase (`PassphraseDialog`) | "Key passphrase" | "Enter the passphrase for <key path>." (or ssh's text); `PassphraseBox` (masked) | "Unlock" | — |
| Unknown host (`HostKeyDialog`) | "Unknown host" | ssh's text in the code font (`HostKeyText`); question "Trust this host and connect?" | "Trust and connect" | — |
| Prompt (`PromptDialog`) | "Git asks" | the prompt's text (`PromptText`); `PromptBox` (masked as 3.2) | "OK" | — |

Every dialog an operation of a repository opens names that repository when the window has more
than one tab with a repository (the error dialog always does).

Each dialog root has an automation peer that makes it a control element (4a), so the
real-window pass can wait for `RemotesDialog`, `ErrorDialog` and the others by id, not only for
their buttons.

### 4g. Drag a branch onto a branch

Pointer-driven inside the window, not the operating system's drag and drop (D87): both harnesses
drive and capture it with plain mouse input, and nothing leaves the window.

- **Service:** `Views/Shell/BranchDragService` (one per `MainWindow`) with a drag layer
  (`DragLayer`, a `Canvas` over everything but the dialog layer, `IsHitTestVisible=false`).
  Sources call `TryBegin(DragSource, PointerPressedEventArgs)` on press; targets register a
  `Func<Point, DropTarget?>` hit test (the graph control's label hit test; the left panel's
  row lookup through the visual tree and each row's `RefItemRow` data context).
- **Sources:** local and remote branch labels in the graph; local and remote branch rows in the
  left panel. Tags, stashes, HEAD and commits are not sources.
- **Threshold:** the drag starts when the pointer has moved more than 4 px from the press with
  the left button held. Before that the press is an ordinary click: the graph selects (and
  focuses) on the press, as it does today, so dragging a label from the graph first selects
  that label's row, and check 7's expected results say so. One exception: a press on a row that
  is part of a multi-selection keeps the multi-selection, and the row is selected alone only on
  a release without a drag (the usual list behaviour). When the drag starts, the service
  captures the pointer (`e.Pointer.Capture(dragLayer)`; the spike showed that later moves and
  the release then go to the layer and `InputHitTest` still finds the target under it), so the
  left panel's button gets no click and the graph changes its selection no further.
- **Adorner:** a pill with the branch's name in its label style (filled lane colour for a local
  branch, outlined for a remote one), drawn at 85% opacity in the drag layer 8 px right and below
  the pointer; the source label or row gets 50% opacity while dragging.
- **Targets:** local branch labels and rows other than the source; remote branch labels and rows
  when the source is a local branch. Under the pointer a valid target is outlined 2 px in
  `VcDragTargetBrush` (6.4); an invalid place shows nothing (the adorner stays). Remote onto remote, a
  branch onto itself and anything onto a tag or stash are not targets.
- **Drop:** releasing over a target opens the menu (4a) at the release point; elsewhere, or Esc
  during the drag, cancels. Header `<A> → <B>`. Items:

| Target B | Items |
|---|---|
| The current branch | Merge <A> into <B> (2.8) · Merge <A> into <B> (create a merge commit) (2.9) · Fast-forward <B> to <A> (2.10; disabled unless B is an ancestor of A: "<B> has commits that <A> does not have") |
| Another local branch | Fast-forward <B> to <A> (2.7; same rule, and disabled when B is checked out in another worktree) · Check out <B> and merge <A> into it (2.1 then 2.8 in one operation, "Check out and merge"; the detached-HEAD confirmation of 2.1 applies; a blocked checkout stops it with the plain "Checkout blocked" dialog, without the stash button, because the merge would then run on top of re-applied changes) |
| A remote branch `<r>/<x>` (A local) | Push <A> to <r>/<x> (`git push --progress <r> refs/heads/<A>:refs/heads/<x>`) · Push <A> to <r>/<x> and track it (`--set-upstream`). The remote-side name is `<x>` as `refs/remotes/<r>/<x>` maps under the remote's default fetch refspec; for a remote with another fetch refspec the drop offers nothing ("<r> maps its branches differently: use Push <A>…") |

  Rebase and "create pull request" are left out (owner's answer 1).
- **Keyboard:** no keyboard drag in phase 3; every drop action is also in a menu (merge,
  fast-forward and push are on the branch menus).

### 4h. Several commits selected

- **Model:** `RepositoryViewModel.Selection` (`GraphSelection`, immutable: an ordered list of
  commit **SHAs**, an anchor SHA and a focus SHA). Rows do not survive a graph swap (an
  auto-fetch, an outside commit, the operation itself all reload the graph, and pages load
  later), so a selection by row numbers could make "Cherry-pick 2 commits" act on commits the
  user did not choose. Every change makes a new `GraphSelection`, so the two-way styled property
  `CommitGraphControl.Selection` raises a change and `AffectsRender` redraws; the control maps
  SHAs to rows only to draw and hit-test. `SwapGraph` re-maps the selection: SHAs not in the new
  graph are dropped (a commit not loaded yet waits, like `_pendingSelection` today); when fewer
  than two remain, the multi-selection ends and the remaining one is the single selection.
  `SelectedIndex` stays the single selection that phases 1 and 2 bind to, and is set only when
  exactly one commit is selected.
- **Focus:** the focused row is the last row clicked that is still selected; Ctrl+click that
  removes the focused row moves the focus to the nearest selected row above it. The details
  panel follows the selection's count (one → that commit's details, two or more →
  `MultiSelectionView`), never `SelectedIndex` alone.
- **Gestures in the graph:** click selects one row and sets the anchor; Ctrl+click adds or
  removes a row (the anchor moves to it); Shift+click selects anchor..row;
  Shift+Up/Down/PageUp/PageDown/Home/End extend from the anchor; Ctrl+A is not handled in
  phase 3. The modifier is `TopLevel.PlatformSettings.HotkeyConfiguration.CommandModifiers`
  (Ctrl on Windows and Linux, Cmd on macOS, and whatever the headless platform reports), never a
  test of the operating system, so the scripted walk-through's Ctrl+click works in CI's macOS
  job; `GraphSelectionTests` assert that the gesture uses that value. The working-changes row
  and stash rows never join a multi-selection: Ctrl or Shift on them acts as a plain click.
- **Drawing:** every selected row gets `VcSelectionBrush`; nothing else changes.
- **Details panel:** with two or more rows selected `RightPanelView` shows `MultiSelectionView`:
  title `MultiSelectionTitle` "3 commits selected", then `MultiSelectionList` with one
  `MultiSelectionItem` per commit in row order: short SHA (code font, secondary) and subject
  (cut with "…"), then the hint "Right-click a selected commit for what can be done with all of
  them." (secondary). No changed-file list.
- **What works on several:** cherry-pick (2.13), revert (2.14), copy SHAs. Squash and compare are
  left out (phase 4, phase 5).
- **Ancestry for the menus:** `CommitGraphData.IsReachableFromHead(sha)`, computed lazily when
  a menu asks and extended as pages arrive (`data.Changed`): a walk over the parents from
  HEAD's commit over the rows loaded so far, cached per graph; 100,000 commits take a few
  milliseconds. While the history is still loading, a commit not reached yet counts as not
  reachable only if the walk has no unloaded parents left to follow; otherwise the menu item
  is enabled and git decides (an already-applied pick ends empty, 2.12). Used by 2.12 and 2.13.
- **Acting:** a menu captures the selection's SHAs when it opens; the operation runs on those
  (2.13, 2.14).

### 4i. The interaction table, row by row

| Gesture (requirements.md) | Phase 3 |
|---|---|
| Click a commit | As phase 1 and 2 (details, files, diff per file); Ctrl and Shift add multi-select (4h) |
| Double-click a branch | Checkout; a remote branch creates a local tracking branch first (4c, 2.2) |
| Double-click a file | Open its diff (D63, unchanged) |
| Right-click a commit | Cherry-pick, revert, reset to here, branch or tag here, copy SHA (4b). "Rebase onto" is left out (phase 4) |
| Right-click a branch | Merge, push, pull (or fast-forward), rename, delete, set upstream (4b). "Rebase" is left out (phase 4) |
| Right-click a file | Stage, discard, ignore, open (in the default app) and show in the file manager (4b). File history and blame are left out (phase 5) |
| Drag branch A onto branch B | Menu: merge, fast-forward, push (4g). Rebase (phase 4) and create pull request (phase 7) are left out |
| Ctrl/Shift-select commits | Cherry-pick several (and revert several). Squash (phase 4) and compare (phase 5) are left out |

"Every object has a context menu": commits, stashes, the working-changes row, local and remote
branches, remotes, tags, section headers, files in all four places, the diff view and the empty
graph area. Folder rows, the "Pull requests" header and repository tabs have none (4b, D92).
"Every common action takes
two clicks at most": a right-click and an item; checkout is a double-click; fetch, pull, push,
branch, stash and pop are one toolbar click.

## 5. Conflict and in-progress state (basic)

- **Detection** (`GitRepository.ReadStatusAsync`, no extra git call): files in the repository's
  own git folder (`GitDirectory`, which is per worktree), in this order of precedence:
  `rebase-merge/` → Rebase (`head-name`, `onto`, the count of non-comment lines of
  `git-rebase-todo`); `rebase-apply/` with `applying` → ApplyMailbox, without → Rebase;
  `MERGE_HEAD` → Merge (subject: `MERGE_MSG`'s first line); `CHERRY_PICK_HEAD` → CherryPick
  (the commit and its subject); `REVERT_HEAD` → Revert; `sequencer/todo` without those →
  CherryPick or Revert by its first line's verb (`pick` or `revert`), stopped between commits.
  `Remaining` for CherryPick and Revert: the non-comment lines of `sequencer/todo`, less one
  while `CHERRY_PICK_HEAD` or `REVERT_HEAD` exists (git keeps the stopped pick as the todo's
  first line, section 0); 0 without `sequencer/`. Only a sequence started outside the app has
  a sequencer: the app picks and reverts one commit at a time (2.13).
  `Empty`: a CherryPick or Revert whose index equals HEAD's tree and has no conflicts (git
  stopped on a pick that changes nothing, 2.12).
  `WorkingTreeStatus` gains `InProgressState InProgress { get; init; }` (default None:
  `InProgressState(InProgressKind Kind, string? Commit, string? Subject, string? Branch,
  string? Onto, int Remaining, bool Empty)`), and `SameAs` compares it. The conflicted files
  are the status's `Conflicted` entries (phase 2).
- **Watcher:** these files are in the git folder and neither under `objects/` nor `*.lock`, so a
  merge started in a terminal sets off `RefreshAsync`, which reads the status: the banner
  appears. The app's own operations pause the watcher and refresh afterwards (D71), so the banner
  appears once, after the operation.
- **Banner** (`Views/Shell/ConflictBanner`, in `CommitGraphView` above the graph or the diff,
  36 high, `ConflictBanner`): warning icon (`IconAlert`, new), `BannerText`, and `BannerAbortButton`
  at the right. Background `VcBannerBackgroundBrush`, a 1 px bottom line `VcWarningBrush`
  (6.4). Texts ("N files have conflicts" uses "1 file has" for one; "conflicts resolved" when
  none is conflicted):

| State | Text | Button |
|---|---|---|
| Merge | `Merging: "Merge branch 'feature/conflict'". 1 file has conflicts.` / `… Conflicts resolved: commit to finish the merge.` | "Abort merge" |
| CherryPick | `Cherry-picking a1b2c3d "Conflicting change". 1 file has conflicts.` / `… Conflicts resolved: commit to finish the cherry-pick.`; when `Remaining > 0` (a sequence from a terminal) ` 2 more commits wait: continue them in a terminal (git cherry-pick --continue).`; stopped between commits: `Cherry-pick stopped with 2 commits left: continue in a terminal, or abort.`; `Empty`: `Cherry-picking a1b2c3d "…" changes nothing: its changes are already here. Abort to go on.` | "Abort cherry-pick" |
| Revert | as CherryPick with "Reverting", "revert" and `git revert --continue` | "Abort revert" |
| Rebase | `Rebasing feature/conflict onto 1a2b3c4. 1 file has conflicts.` / `… Conflicts resolved: continue in a terminal (git rebase --continue), or abort.` | "Abort rebase" |
| ApplyMailbox | `Applying patches (git am). 1 file has conflicts.` | "Abort" |
| Conflicts without an operation (a stash that did not apply cleanly) | `1 file has conflicts. Resolve it, then stage it.` | none |

  The banner's tooltip says: "Continue and Skip, and the conflict resolver, come in a later
  version. Resolve the files in your editor and stage them, or abort." (phase 4).
- **Abort** (2.15), confirmation question per kind: "Abort merging "Merge branch
  'feature/conflict'"? Changes made to resolve conflicts are lost." / "Abort the cherry-pick?
  HEAD goes back to where it was before it started, and changes made to resolve conflicts are
  lost." plus, when `sequencer/` exists, " Commits this cherry-pick already made are dropped
  too." / "Abort the revert? …" (as cherry-pick) / "Abort the rebase of feature/conflict? The
  branch goes back to where it was before it started." / "Abort git am? …". The detail is
  always "A backup is saved first, so this can be undone." (`abort-<time>` holds HEAD before
  the abort, so dropped commits stay reachable).
- **Conflicted files flagged:** the unstaged list's conflicted rows keep phase 2's "U" in the
  danger colour and gain the tooltip "Conflict: resolve it in your editor, then stage it" and the
  menu item "Mark as resolved"; the list's title becomes "Unstaged files (N, M with conflicts)";
  the working-changes row in the graph reads "Working changes · M conflicts" (only while M > 0).
- **Finishing a merge, a cherry-pick or a revert:** committing in the stage panel (phase 2's
  `git commit`) concludes a merge or the current single pick or revert. While a Merge,
  CherryPick or Revert is in progress and the summary and description are empty, the stage
  panel fills them from `MERGE_MSG` with every line starting with `#` removed
  (`--cleanup=whitespace` would keep them, D68). Phase 2's `CanCommit` requires something
  staged; during a Merge with no conflicts left it is enabled even with nothing staged (a merge
  resolved to "ours" leaves the index equal to HEAD, and `git commit` still records the merge).
  During a CherryPick or Revert that is `Empty` it stays disabled with the tooltip "This
  cherry-pick changes nothing: abort it" (git would refuse an empty commit). During a Rebase, or
  a CherryPick or Revert with `Remaining > 0`, committing works as in a terminal but does not
  continue the operation; the banner says to continue in a terminal (phase 4 brings continue).
- **Left for phase 4:** continue, skip, the resolver, taking a side per file, an external merge
  tool, and the banner of a rebase the app itself starts from its menus. A pull that rebases
  (D84) and stops is shown by this phase's banner, with Abort.
- **Toolbar and menus during an operation in progress:** Pull, Merge, Cherry-pick and Revert are
  disabled with "Finish or abort the <kind> first"; checkout, reset, stash and others are left to
  git, whose refusal shows in the error dialog.

## 6. Settings, session, left panel, earlier checks

### 6.1 Auto-fetch (owner's answer 3)

- `AppSettings.AutoFetchMinutes` (int, default 5; 0 turns it off). No UI in phase 3 (the settings
  screen is phase 6); the value can be edited in `settings.json`.
- The environment variable `VISUALCOMMIT_AUTO_FETCH_MINUTES` overrides the setting when it is
  set to a whole number. `HeadlessTestApp.IsolateFromTheMachine` (through
  `GitIsolation.IsolateThisProcess`) and `RealApp.Launch` set it to `0`: the harnesses never
  auto-fetch.
- `AutoFetchScheduler` (`ViewModels/Operations/AutoFetchScheduler.cs`, one per
  `MainWindowViewModel`) runs `Tick(DateTimeOffset now)` every 30 seconds from a
  `TimeProvider` timer. On a tick, for the active tab's repository only: when the interval is
  above 0, the repository has a remote, its queue is idle, no dialog is open, and the last fetch
  (any fetch or pull that succeeded, or the moment the repository was opened) is at least the
  interval ago → `TryStartInBackground(autoFetch)`. So the first auto-fetch comes one interval
  after opening, not at start-up (Q1's measurement stays clean), and a tab that becomes active
  after a long time fetches within 30 seconds. The fetch is `git fetch --all --progress
  --no-auto-maintenance` (1.8): a background fetch does not start git's maintenance, and it is
  never killed (tab close and app exit leave it to finish; the user's operations wait for it).
- Tests call `Tick(now)` directly with chosen times and a fake repository view model state; no
  test waits for a real timer.

### 6.2 Other settings and the session

- `AppSettings.StashIncludeUntracked` (bool, default true): the stash dialog's check box starts
  with the last choice.
- Nothing new in `session.json`.

### 6.3 The left panel's selection follows the graph's (phase 1 follow-up, D98)

`LeftPanelViewModel` gets `SyncSelection(string? selectedSha)`, called from the
`OnGraphSelectionChanged` hook that WP0 adds to `RepositoryViewModel.cs` (section 8) with the
single selected SHA, or null when several rows or none are selected. The
row the user clicked keeps its selection background only while the graph's single selected commit
is that ref's target; any other selection (a click on another row, the keys, a parent link,
several rows) clears it. A selection made in the graph never highlights a ref row, because several
refs can point at one commit. Phase 1's checks that click a ref and look at it straight away are
unchanged.

### 6.4 New theme tokens and icons

Tokens (both themes, in `Tokens.axaml` and `architecture.md`'s table): `VcMenuShadow`
(`BoxShadows`, dark `0 6 16 0 #80000000`, light `0 6 16 0 #33000000`),
`VcBannerBackgroundBrush` (dark `#33291A`, light `#FFF4DE`), `VcDragTargetBrush` = the accent
(an alias, so the outline can change apart from it). Icons (`Icons.axaml`, Lucide outlines,
listed in `THIRD-PARTY-NOTICES.md`): `IconAlert` (triangle-alert). Menus have no icons.

### 6.5 Changes to phases 0 to 2's checks that phase 3 causes on purpose

To be recorded under "Changes to expected results" in the phase 3 report, each with its reason:

1. Every phase 1 and 2 screenshot with a repository open: the toolbar's Fetch, Pull, Push and
   Branch (and Stash with changes, Pop with stashes) are enabled, so their labels and icons are
   drawn in the primary text colour instead of the disabled one. Expected results that say "the
   toolbar as in phase 0" for a repository tab change accordingly. Phase 0's checks (no
   repository) are unchanged: only Theme is enabled.
2. Phase 1's known issue fixed: after a ref is clicked and the graph's selection moves, the ref's
   row loses its selection background. A phase 1 check whose screenshot was taken in that state
   changes.
3. Phase 2's checks with a conflicted file, if any picture shows one: the row's tooltip, the
   list's title "(N, M with conflicts)", the working-changes row's text and the banner above the
   graph. (Phase 2's visual checks use no conflicted repository today; `Scenarios.ConflictAsync`
   appears only in App and Git tests, `WorkingChangesTests` among them, whose assertions on the
   list's title change if they check it.) Phase 2's hover Stage, Unstage and Discard buttons
   stay as they are (D70 adds the menu, it does not remove the buttons), and phase 2's discard
   confirmation keeps its text: neither changes a phase 2 picture.
4. A phase 2 test that closes a tab during a write and expects the write to stop: from now on
   a started write runs to its end (1.4).
5. `docs/test-reports/phase-1-pictures.sha256` and `phase-2-pictures.sha256` then name pictures
   that differ; the phase 3 report says which and why.

### 6.6 Changes to `plan.md` for closing step 3

- Phase 4's "pull with rebase" is partly delivered by phase 3: pull already follows
  `pull.rebase` (D78, D84), and a pull that rebases and stops shows phase 3's banner with
  Abort. Phase 4 keeps rebase onto, continue and skip (also for a pull's rebase), and the
  resolver.
- Phase 4's "squash a multi-selection" builds on phase 3's SHA-based multi-selection (D88).
- Phase 5 (O2, undo) reads the backup refs of D83, including the new kinds `detached-` and
  `remote-`.

## 7. Tests and harness

### 7.1 Scenario repos (new, in `tests/VisualCommit.Testing/Scenarios.Phase3.cs`; `Scenarios` becomes `static partial`)

All pinned in `ScenarioTests` (refs, SHAs, the graph's row order and lanes), built once per test
process and copied per test (`TempRepo.CopyAsync`), unless said otherwise. TempRepo's clock
gives 2026-01-01 12:00 UTC plus one minute per commit.

**`OperationsAsync`** ("ops"): the base for branches, merges, resets, cherry-picks, reverts,
tags and stashes.

| Time | Commit | Branch | Files |
|---|---|---|---|
| 12:00 | Initial commit | main | README.md "# Operations\n" |
| 12:01 | Add core | main | src/core.txt "core\n"; tag `v1.0` (annotated, "Version 1.0") |
| 12:02 | Alpha one | feature/alpha (from Add core) | src/alpha.txt "alpha 1\n" |
| 12:03 | Alpha two | feature/alpha | src/alpha.txt "alpha 1\nalpha 2\n" |
| 12:04 | Main work | main | src/core.txt "core\nmain work\n"; tag `v1.1` (lightweight) |
| 12:05 | Fast-forward me | feature/ff (from Main work) | docs/ff.txt "ff\n" |
| 12:06 | Conflicting change | feature/conflict (from Add core) | src/core.txt "core\nconflict\n" |
| 12:07 | stash@{0} "On main: Experiment" | (main) | README.md "# Operations\n\nExperiment.\n" |

HEAD on a clean `main` at Main work. Remote `origin` (bare, next to it) has `main` and
`feature/alpha`, both pushed and tracked: ahead and behind 0. `feature/ff` and `feature/conflict`
are local only. Graph (derived from D48, D54, D55; `ScenarioTests` pins it): rows top to bottom
stash@{0} (lane 0, to Main work), Conflicting change (lane 1, to Add core), Fast-forward me
(lane 2, to Main work), Main work (lane 0; lane 2 ends here; to Add core), Alpha two (lane 2),
Alpha one (lane 2, to Add core), Add core (lane 0; lanes 1 and 2 end here), Initial commit
(lane 0); 3 lanes. Expected results of merges, resets, cherry-picks and reverts are derived from
this table.

**`StoppedAsync(InProgressKind kind)`**: an `OperationsAsync` copy stopped in the given state,
through `TempRepo` git calls (as a terminal would): Merge — `git merge feature/conflict`
(src/core.txt conflicted); CherryPick — `git cherry-pick <Alpha one> <Conflicting change> <Alpha
two>` (stops at the second; one more waits); Revert — a commit "Core again" changing
src/core.txt line 2 on main, then `git revert <Main work>` (conflict); Rebase — on
`feature/conflict`, `git rebase main` (conflict); None — `git stash apply` of a stash that
changes src/core.txt after main changed it (conflicts without an operation). Built each time.

**`RemoteSyncAsync`** ("sync"): a repository `sync` with a bare `origin`:
12:00 "Initial commit" (README.md), 12:01 "Shared work" (src/shared.txt) on main, pushed and
tracked; branch `published` (12:02 "Published work", pushed and tracked, then 12:03 "Unpushed
work": 1 ahead); branch `topic` (12:04 "Topic work", never pushed, no upstream); HEAD on main.
A helper `PushFromElsewhereAsync(TempRepo sync, string branch, string file, string content,
string message)` clones `origin` into a sibling folder `other` (once per test) and pushes a
commit from there, so a test makes `origin` ahead of `sync` when it needs to (fetch shows
behind 1; a local commit then makes it diverge; a push is rejected). Built once and copied;
`other` is made per test after the copy.

**`CheckoutBlockedAsync`**: main with notes.txt "one\ntwo\nthree\nfour\nfive\nsix\nseven\n"
(12:00 "Add notes"); `feature/other` changes line 7 to "seven (other)" (12:01); `feature/clash`
changes line 1 to "one (clash)" (12:02); HEAD on main with an uncommitted change of line 1 to
"one (local)". Checking out either branch is blocked; stash-and-checkout re-applies cleanly onto
`feature/other` and conflicts on `feature/clash`. A third branch `feature/addfile` (12:03) adds
`newfile.txt`, and the scenario has an untracked `newfile.txt` "mine\n": checking out
`feature/addfile` is blocked by the untracked file (and by the tracked change), so its dialog
has no stash button. Built once and copied.

**`EdgeCasesAsync`** ("edges"): small cases the reviews found, each pinned in `ScenarioTests`:
a branch `side` checked out in a linked worktree `edges-wt` next to the repository and behind
`main` (fast-forward disabled, and `git fetch .` refusing it); a branch `loc` whose upstream is
local (`branch.loc.remote = .`, `branch.loc.merge = refs/heads/main`); a local branch named
`origin/main` beside the remote branch of that name; HEAD detached on a commit "Detached work"
that no ref reaches; a tag `feature/alpha` beside the branch `feature/alpha`. Built once and
copied (the worktree's path is fixed up by `git worktree repair` after the copy).

**`AuthRemoteAsync(AuthRemoteServer server)`**: a clone of a bare repository that the server
serves, with `origin` = `http://127.0.0.1:<port>/remote.git`; the bare repository has main with
two commits "Initial commit", "Remote work" and the clone was made before "Remote work" (so a
fetch brings one commit). Built each time with its server.

Phase 2's `ConflictAsync` stays.

### 7.2 Fake remotes

Bare repositories on disk next to the working tree (`TempRepo.AddBareRemoteAsync`, as phase 1),
reached by their folder path. `TempRepo.CopyAsync` already points a copy's remotes at the
copy's bare repositories. New helpers on `TempRepo`: `CloneSiblingAsync(string name, string
from)`, `ConfigAsync(key, value)`, `RefTargetAsync(string fullRef)` (null when missing),
`BackupRefsAsync()` (`for-each-ref refs/visualcommit/backup`), `StashListAsync()`.

### 7.3 The credential test server

`tests/VisualCommit.Testing/AuthRemoteServer.cs`: a `TcpListener` on `127.0.0.1`, serving
`git http-backend` as CGI for one bare repository, and, when `RequireAuth` is true (the
default), demanding HTTP Basic auth (alice/s3cret by default) on every request (`401` with
`WWW-Authenticate: Basic realm="VisualCommit tests"`); with `RequireAuth = false` it serves
without asking (check 17's progress and cancel need no sign-in). Chosen over `HttpListener`
(http.sys on Windows) and Kestrel (an extra framework reference) so that it runs the same way
on the three platforms with nothing to register (D97).

Ports: the three test projects run side by side, so every server listens on port 0 (a port the
system picks) and tests read `Url`, except the servers of the visual checks whose pictures show
the URL (check 21's dialog shows "127.0.0.1:<port>"): `VisualCommit.VisualTests` and the
real-window pass (which runs alone) use port 47391, falling back to a free port only when it is
taken, which the report then notes (the picture differs in the port only). 47391 is below
Windows' ephemeral range (49152 and up), so the other projects' port-0 servers do not take it
there; on Linux it is inside the range, so a clash is possible but rare.

- Requests: HTTP/1.1, one per connection (`Connection: close` in every response), bodies by
  `Content-Length` or chunked (git sends chunked above `http.postBuffer`, 1 MiB; the tests stay
  far below but the decoder is there). `Content-Encoding: gzip` bodies (git compresses some
  POSTs) are passed to `http-backend` as they are with `HTTP_CONTENT_ENCODING` set.
- CGI through `GitRunner` with `GitCommand.Latin1` (D66: bytes in and out unchanged):
  `git http-backend` with `GIT_PROJECT_ROOT`, `GIT_HTTP_EXPORT_ALL=1`, `PATH_INFO`,
  `QUERY_STRING`, `REQUEST_METHOD`, `CONTENT_TYPE`, `CONTENT_LENGTH`, `REMOTE_USER` (allows
  `receive-pack`), `GIT_PROTOCOL` from the `Git-Protocol` header, and `TempRepo.Environment`'s
  isolation. `GitRunner` returns standard output only when git has exited, so the server runs
  `http-backend` to its end and then sends the response; it never streams. The CGI's `Status:`
  and other header lines become the HTTP response's.
- Test controls: `RequireAuth`, `Credentials` (change the password), `Requests` (a log), and
  `HoldNextFetchPack()` / `Release()`. The hold applies to the next `git-upload-pack` POST that
  **carries a pack request**: under protocol v2 (git's default since 2.26) one whose body
  contains `command=fetch`, not the `command=ls-refs` POST that comes first and carries no
  progress; under protocol v0 any upload-pack POST. The server takes http-backend's complete
  response, parses its pkt-lines, sends the bytes up to and including the first band-2
  (progress) pkt-line whose text contains `%`, and holds the rest until `Release()`. A visual
  check thereby shows a fetch in progress with a percentage (git 2.36 on a 4-object remote
  prints `Counting objects:  25% (1/4)`; the value depends on the pack, so the check accepts any
  from 1 to 100) and can click Cancel while git really waits.

### 7.4 Harness additions

| Where | Addition |
|---|---|
| `ShellDriver` (made `partial`; `ShellDriver.Phase3.cs`) | `Click(Point, KeyModifiers)` (Ctrl and Shift clicks through `Window.MouseDown(position, button, modifiers)`); `PressMouse(Point)`, `MoveMouseHeld(Point)`, `ReleaseMouse(Point)` (a drag with captures in the middle); `Menu()` → the open menu's header and items (label, enabled, destructive) in order; `ClickMenuItem(string label)`; `MenuItemBounds(string label)`; `Dialog<T>()` → the open dialog's view model; `TypeInto(string automationId, string text)`; `WaitForIdleAsync(RepositoryViewModel? repo = null)` (the queue idle and its refresh done); `Start` injects `FakeFileLauncher` and `FakeClipboard` by default (properties `Launcher`, `Clipboard`), as it injects `FakeFolderPicker`. The Ctrl modifier for multi-select is read from the headless platform's `CommandModifiers` (4h) |
| `RealApp` (made `partial`; `RealApp.Phase3.cs`) | `ClickAt(x, y, params VirtualKeyShort[] held)`; `MouseDownAt`, `MoveHeldTo`, `MouseUpAt` (a drag with a capture in the middle; the button is released in a `finally`); `MenuItemNames()` (`FindAll("ContextMenuItem")`); `ClickMenuItem(string name)`; `WaitForLogLine(Regex, TimeSpan)` (the queue's `<repo>: <Title>: <outcome>` lines) |
| `GitIsolation` | sets `VISUALCOMMIT_AUTO_FETCH_MINUTES=0` (and `RealApp.Launch` does the same); removes `GIT_ASKPASS`, `SSH_ASKPASS`, `SSH_ASKPASS_REQUIRE` and `GCM_INTERACTIVE` from the test process's environment (and from `RealApp.Launch`'s), so an askpass program of the machine (VS Code's) never answers a test |
| `FakeRepositoryBase` | every new member, throwing `NotSupportedException` (reads: empty results) |
| Fakes | `FakeFileLauncher`, `FakeClipboard`, `ScriptedPromptHandler` (answers from a queue, records prompts) in `tests/VisualCommit.Testing`; `AskPassRunner` (runs the real helper with a prompt text, `SSH_ASKPASS_PROMPT` and an open scope's environment, as OpenSSH would, for the prompt dialog checks, 3.5) |

The graph's rows are not in UI Automation (D49): the real-window pass right-clicks and drags them
at positions computed from the same constants as the scripted expectations, as phase 1's pass
clicks rows.

### 7.5 Proposed visual checks (the report's author writes them in full, step 5)

RW marks the checks the real-window pass repeats. Every check states the scenario, size and
theme. The plan's gate asks the real-window pass for at least one screenshot of every new
screen; the new screens and the RW check that pictures each are: the menu (1, 5), the drag
(7), the conflict banner (9), the reset dialog (10), the multi-selection view (11), the name
dialog (13), the error dialog and its stash flow (14), the stash dialog (15), the tag dialog
(16), the status bar's progress and Cancel (17), the push-rejected dialog (19), the credentials
dialog (21), the passphrase, unknown-host and prompt dialogs (21b), the remotes dialog (22), the
pick, push and choice dialogs (28, 29), the finishing dialog (31). RW steps in the real window
invoke no item that touches something outside the window (no "Copy …", "Open file", "Show in
folder").

1. Commit menu (RW: right-click with the real mouse, the menu captured, "Create branch here…"
   clicked and the name dialog cancelled; "Copy SHA" is clicked only in the scripted run,
   which asserts `FakeClipboard`).
2. Branch menus: local (left panel and graph label), remote, the current branch.
3. Tag, stash, remote, section-header and empty-area menus; a disabled item's tooltip.
4. File menus: unstaged (tracked and untracked), staged, the diff view (with selected lines),
   a commit's file; the working-changes row's menu ("Stage all" invoked: the stage lists
   change); "Show in folder" asserted on `FakeFileLauncher`.
5. Keyboard: Shift+F10 on the graph opens the commit menu, and so does the Menu key; Down,
   Down, Enter runs an item; Esc closes another (RW: real keyboard).
6. Double-click a local branch (checkout), a remote branch (tracking branch created), and a
   stash in the left panel (applied).
7. Drag feature/alpha onto main: the press selects Alpha two's row first (4g); the adorner and
   outline mid-drag; the drop menu; "Merge" → the graph (RW).
8. Merge feature/ff (fast-forward) and again with "create a merge commit": before and after
   graphs; the merge commit's subject is "Merge branch 'feature/ff'".
9. Merge feature/conflict: banner, conflicted file flagged, Abort's confirmation, the graph back;
   backup ref present (RW: the banner and the confirmation).
10. Reset to Add core: soft, mixed, hard (the dialog with Hard chosen and its danger button,
    graphs, stage panel, backup refs) (RW: the dialog).
11. Cherry-pick Alpha one; Ctrl/Shift-select Alpha one and Alpha two, details panel, menu,
    cherry-pick both (RW: real Ctrl and Shift).
12. Revert Main work.
13. Create a branch (validation messages including a folder clash, checkout ticked), rename,
    delete (confirmation) (RW: the name dialog with a validation message).
14. Checkout blocked → error dialog with git's message → "Stash changes and check out" → clean;
    the clashing branch → conflicts and the stash kept; `feature/addfile` → "Checkout blocked"
    without the stash button (RW: the first two).
15. Stash save (dialog), apply, pop, drop (confirmation) (RW: the stash dialog).
16. Tags: create annotated (message "#42 hotfix", kept) and lightweight, push, delete, delete on
    origin (RW: the tag dialog).
17. Fetch with the held server (`RequireAuth = false`): progress with a percentage and Cancel;
    then a full fetch: behind 1 in the left panel (RW).
18. Pull (merge) then push: the status bar's progress during each (held server), graphs and
    ahead/behind back to 0.
19. Push rejected: the error dialog with git's message and "Pull" (RW).
20. Force push: confirmation, result; a fetch that brought someone else's commit, then a force
    push stopped ("Force push stopped").
21. Credentials dialog: fetch from the auth server, sign in, success; a second run cancelled →
    "Fetch cancelled" (RW).
21b. Prompt dialogs through the real helper (3.5): the passphrase dialog (a recorded `Enter
    passphrase for key …` text), the unknown-host dialog (a recorded host-key question) and the
    generic prompt dialog; each answered, and the helper's output asserted (RW: each dialog
    once, through the real app's broker).
22. Remotes dialog: add (with fetch), change URL, rename, remove (the confirmation stacked over
    the dialog); a URL with a token shown hidden (RW: the dialog and the stacked confirmation).
23. Toolbar with a repository: enabled states and disabled tooltips (detached, unborn, no
    remotes); Branch, Stash and Pop.
24. The left panel's selection follows the graph.
25. Banners for a cherry-pick sequence and a revert started in a terminal, a rebase started in
    a terminal (outside change), an empty cherry-pick, and stash conflicts.
26. Ignore an untracked file.
27. Light theme at 1920×1080: a menu, the error dialog, the reset dialog and the banner.
28. Set upstream on `topic` (pick dialog, filter typed), then Change upstream, then Stop
    tracking: the left panel's ahead/behind marks follow (RW: the pick dialog).
29. Push `topic`, which has no upstream: the push dialog with "Set as upstream"; then "Push
    v1.0…" with two remotes: the choice dialog (RW: both dialogs).
30. Delete `published` on origin: confirmation, the remote branch gone, backup ref present;
    fast-forward `published` after a push from elsewhere (2.7), and the fast-forward disabled
    for `side` in `EdgeCasesAsync` (checked out in a linked worktree), with its tooltip.
31. Close the window while a held fetch runs: the finishing dialog; "Keep open"; then release
    the server and the window closes by itself; again with "Quit now" (RW: the dialog, with
    the real window's close button).
32. Detached HEAD with "Detached work": double-click main → the "Leave these commits?"
    confirmation → checked out, backup ref present.

### 7.6 Each "Delivers" item and what proves it

| Delivers (plan.md, phase 3) | Automated tests | Visual checks (7.5) |
|---|---|---|
| Checkout, create, rename, delete; merge and fast-forward; reset; cherry-pick; revert | `BranchOperationTests` (merge messages, `git fetch .` and the linked worktree, name clashes), `HistoryOperationTests` (one pick at a time), `RepositoryOperationsTests` | 6, 8, 9, 10, 11, 12, 13, 30, 32 |
| Fetch, pull, push, safe force-push, set upstream, manage remotes, auto-fetch | `RemoteOperationTests` (leases, `--force-if-includes`, local upstreams), `PullParityTests`, `AuthenticatedRemoteTests`, `AutoFetchSchedulerTests` | 17, 18, 19, 20, 22, 28, 29, 30 (auto-fetch: tests only; the harnesses turn it off) |
| Stash save, apply, pop, drop; tag create, delete, push | `StashTagTests` | 15, 16, 29 |
| Context menus on every object, double-click actions, drag a branch onto a branch, multi-select | `RepositoryMenusTests`, `InWindowMenuTests`, `BranchDragTests`, `GraphSelectionTests` | 1 to 7, 11 |
| Operation queue with progress, cancel and git's own output; takes over phase 2's writes | `OperationQueueTests` (UI context, opt-in dedupe, no pre-emption, stale locks), `ProgressParserTests`, `FailureClassifierTests`, `StatusBarTests`; phase 2's tests still green | 17, 18, 19, 31 |
| Stage, unstage and discard in a file's menu (from phase 2) | `RepositoryMenusTests` | 4 |
| Toolbar Fetch, Pull, Push, Branch, Stash, Pop and the status bar's operation status (from phase 1) | `ToolbarTests`, `StatusBarTests` | 17, 18, 23 |
| Credential and ssh passphrase prompts as in-app dialogs | `PromptClassifierTests`, `AskPassTests`, `AuthenticatedRemoteTests`, `PromptDialogTests` | 21, 21b (real window too) |
| ssh's own prompts answered in the app or turned off (from phase 1) | `AskPassTests` (passphrase through `SSH_ASKPASS_REQUIRE`; deny mode), `PromptDialogTests` (recorded host-key and passphrase texts through the real helper) | 21b; a real ssh server's host key: by hand (3.5) |
| The left panel's selection follows the graph's (from phase 1) | `LeftPanelViewModelTests` | 24 |
| Backup references before destructive operations | `BackupRefTests` (hard reset over untracked and staged-new files, batches, retry naming), `RepositoryOperationsTests` | 9, 10, 13, 15, 16, 20, 22, 30, 32 (backup refs asserted with `TempRepo.BackupRefsAsync`) |
| Basic conflict state: banner with abort, conflicted files flagged | `InProgressTests` (Remaining, Empty), `ConflictBannerTests` | 9, 14, 25 |

### 7.7 Time budget and risks

- The budget is set from measurements, not guessed. Phase 2's real-window pass took 5 min 56 s
  for 14 tests, every real input waits 400 ms (`RealApp.WaitUntilIdle`), each prompt starts the
  .NET helper (about 0.4 s per prompt measured), and phase 3 adds 20 RW checks, most with
  several steps. So: once WP-S is merged, the lead times one representative scripted check and
  one RW check, extrapolates, and writes the budget for both passes into `status.md`'s Notes
  and the report before WP10 starts. To keep it down: RW checks share app launches and servers
  where their scenarios allow (17, 21 and 21b on one server and one launch; 28 and 29 on one;
  9, 10 and 14 on one), shared scenario templates, the held server instead of waits, and no
  fixed sleeps. The report records the measured times.
- Risks:
  1. Test duration (plan risk 12): the scripted walk-through grows again; watch the hot laptop.
  2. Cancel on macOS (D37): only git is killed; ssh and the askpass helper end when their pipes
     break. The phase 3 manual checklist for the Mac includes cancelling a fetch over ssh.
  3. The askpass helper is framework-dependent: it needs the .NET runtime that the app uses
     (`DOTNET_ROOT` is inherited). Phase 8's self-contained packaging must include it.
  4. Pull is a fetch and a merge or rebase chosen by the app as `git pull` would (D84). Git may
     change its rules; `PullParityTests` on CI's Git 2.55 catch that. Not mirrored: submodule
     recursion after a pull, and options that exist only on pull's command line.
  5. If the spike keeps the custom `InWindowMenu` (4a), its keyboard and screen-reader
     behaviour is ours to get right (R7, phase 8's accessibility pass).
  6. OpenSSH older than 8.4 ignores `SSH_ASKPASS_REQUIRE`: when the app was started from a
     terminal, such an ssh still asks there (Ubuntu 20.04 ships 8.2). Git for Windows 2.36
     bundles 8.9; which OpenSSH the oldest supported Git for Windows (2.30) bundles was not
     checked.
  7. Port 47391 taken on a machine (on Linux it is inside the ephemeral range): the visual
     checks' server falls back, and check 21's screenshots then differ in the port number;
     the report notes it.
  8. The helper's app host must reach every test project's output through the project reference
     (section 0 shows it does for `VisualCommit.exe`); a test per project guards it.
  9. Unix socket paths are limited to 104 characters on macOS; the pipe name is kept short and a
     test asserts the socket path's length on macOS and Linux.
  10. Stale lock files: a user's Cancel of a fetch or push, closing a tab during one, or "Quit
      now" can leave `*.lock` files (1.8). After Cancel the app lists them and offers to
      remove them; after a tab close or "Quit now" it only logs them, and the next fetch then
      fails with git's `File exists` message until they are removed.
  11. git-lfs and other filters or hooks that download during a local operation: they can now
      prompt (1.5), but such an operation still cannot be cancelled, so a large LFS download in
      a checkout runs to its end. Not tested automatically (no LFS server); on the manual
      checklist.
  12. The detached-HEAD check (2.1) counts commits that no branch, remote branch or tag
      reaches; commits reachable only from a stash or a backup ref count as left behind
      (confirming more often than strictly needed, the safe direction).

## 8. Work breakdown

Each package is built on its own branch from `phase/3-operations` (worktrees under
`.claude/worktrees/`, as in phases 1 and 2: create the branch from the phase branch first) and
merged back with tests green. Shared files are edited only by the package named for them; a
later package that needs a change there asks the lead, who makes it on the phase branch.

| # | Package | Files it owns (new unless marked) | Needs |
|---|---|---|---|
| WP0 | Contracts and the queue (first, alone) | `Core/Operations/*` (queue, records, `GitFailure`, `GitOutputCleaner`, `IOperationLog`, `IGitPromptBroker`, `IGitPromptHandler`, `GitOperationScope`); `Core/Git/RefNameRules.cs`, `InProgressState.cs`, `RemoteInfo.cs`, `MergeResult.cs`, `PushRequest.cs`, `NetworkOptions.cs`; **shared**: `Core/Git/IGitRepository.cs` (every new member at once), `GitResult.cs` (`GitException.StandardOutput`), `GitCallRecord.cs` (`OperationId`), `WorkingTreeStatus.cs` (`InProgress`), `Settings/AppSettings.cs`; `Git/GitRunner.cs` (record the operation id; hide credentials in the recorded output, D103; `GIT_SEQUENCE_EDITOR=:`, D102); `GitRef` (the three upstream fields, D101) and `GitRepository.Refs.cs`'s format; `Git/GitRepository.Stubs.cs` (every new member throwing `NotImplementedException`, deleted piece by piece by WP1 to WP4); **shared** `tests/VisualCommit.Testing/FakeRepositoryBase.cs`; App: `ViewModels/Operations/OperationStatusViewModel.cs`, `RepositoryOperations.cs` (skeleton), `ViewModels/Menus/MenuModel.cs`, `ViewModels/Dialogs/DialogViewModel.cs` and the generalised `DialogHostViewModel.cs`; **shared** `RepositoryViewModel.cs` (the queue replaces `WriteAsync`'s gate; exposes `Queue`, `OperationStatus`, `Operations`, `Toolbar`, `Selection`), `TabServices.cs`, `AppSession.cs` (creates the broker, operation log, launcher and clipboard services, scheduler; no-op implementations where WP3 and WP9 fill in), `MainWindow.axaml` (the dialog layer becomes a `ContentControl` with templates; adds `MenuLayer` and `DragLayer` placeholders), `App.axaml` (includes the new `Theme/Menus.axaml` and `Theme/Dialogs.axaml`, created empty), `Theme/Tokens.axaml` and `Theme/Icons.axaml` (all of 6.4); `RefNameRulesTests` (against `git check-ref-format`), `OperationQueueTests`. **Hooks for the later packages, so that none of them edits a file another owns** (WP0 merges before they start; afterwards the file belongs to the package named): in `RepositoryViewModel.cs` the partial methods `OnGraphSelectionChanged(GraphSelection)`, `OnGraphSwapped(CommitGraphData)`, `OnStatusApplied(WorkingTreeStatus)` called from `SwapGraph`, the selection setter and `ApplyStatus`, and `GraphSelection` itself (immutable, SHAs, 4h); in `CommitGraphControl.cs` the events `ContextRequestedAt(GraphHit, Point)` and `LabelDoubleTapped(GraphHit)`, a `GraphHit HitTest(Point)` that phase 1's label hit test already answers, the styled properties `Selection` and `WorkingRowText` (replacing the constant in `CommitGraphControl.Render.cs`); in `RightPanelView.axaml` a `ContentControl` slot bound to `RepositoryViewModel.MultiSelection`; in `LeftPanelView.axaml` the attached property `RefDrag.Source` on branch rows and `LeftPanelView.RefRowAt(Point)`; in `WorkingChangesView.axaml` bindings to `RowToolTip` and `UnstagedTitle` (WP9 fills them); in `MainWindow.axaml.cs` an ordered Esc chain (`IEscapeHandler`s registered by the menu layer, drag service, dialog host and diff) and the Shift+F10 handler (4a) and the `Closing` hook (1.8); in `MainWindowViewModel.cs` the `Menu` layer, `AutoFetchScheduler` wiring (a no-op scheduler until WP9) and `FinishingDialog`'s call site; in `RepoTabViewModel.cs` `Toolbar`; `DialogViewLocator` (4f); `IRepositoryProvider.CloneAsync`'s environment parameter (WP3 implements it) | — |
| WP-S | Scenarios and harness | `Testing/Scenarios.Phase3.cs`, `Scenarios.cs` (only `partial`), `TempRepo.cs` helpers, `AuthRemoteServer.cs`, fakes, `ShellDriver.Phase3.cs`, `RealApp.Phase3.cs`, `GitIsolation.cs`, `ScenarioTests` additions, `scripts/setup-linux.sh` (`openssh-client`) | can start before WP0 (needs only `TempRepo`); the fakes need WP0's interfaces |
| WP1 | Git: branches, history, in-progress, backups | `Git/GitRepository.Branches.cs`, `.History.cs`, `.InProgress.cs`, `.Backups.cs`; `GitRepository.Writes.cs` (snapshot overload); `GitRepository.Changes.cs` (`InProgress` in `ReadStatusAsync`); tests `BranchOperationTests`, `HistoryOperationTests`, `InProgressTests`, `BackupRefTests` | WP0, WP-S |
| WP2 | Git: remotes and network | `Git/GitRepository.Remotes.cs`, `GitProgressParser.cs`, `CloneProgressParser.cs` (delegate), `GitRepository.Clone.cs` (environment, `CloneErrorText` → Core's cleaner); `Core/Operations/GitFailureClassifier.cs`; tests `RemoteOperationTests`, `ProgressParserTests`, `FailureClassifierTests` | WP0, WP-S |
| WP3 | Prompts | `src/VisualCommit.AskPass/*` (new project, in `VisualCommit.slnx`), `Git/Prompts/*`, `Git/VisualCommit.Git.csproj` (the reference), `App/Services/PromptDialogs.cs`, `ViewModels/Dialogs/PromptDialogViewModels.cs`, `Views/Dialogs/Credentials|Passphrase|HostKey|PromptView.axaml`; `WelcomeViewModel.cs` and `GitRepositoryProvider.cs` (clone environment); tests `PromptClassifierTests`, `AskPassTests`, `AuthenticatedRemoteTests` | WP0, WP-S (server), WP2 (fetch) for the end-to-end tests |
| WP4 | Git: stash, tags, ignore; file services | `Git/GitRepository.Stash.cs`, `.Tags.cs`, `.Ignore.cs`; `App/Services/FileLauncher.cs`, `ClipboardService.cs`; tests `StashTagTests`, `IgnoreTests`, `FileLauncherTests` (argument building only) | WP0 |
| WP5 | The menu (after the spike of 4a) | `Controls/InWindowMenu.cs` or the `MenuFlyout` adapter, `Views/Shell/MenuLayer.axaml(.cs)`, `ViewModels/Menus/MenuLayerViewModel.cs`, `Theme/Menus.axaml`, the menu's automation peer; registers its Esc handler (no edit of `MainWindow.axaml.cs`); tests `InWindowMenuTests` (headless: open, position, dismiss, keyboard incl. Shift+F10 and the Menu key, automation ids as control elements) | WP0; the spike's result |
| WP6 | Status bar, toolbar, dialogs | `Views/Shell/StatusBarView.axaml`, `ToolbarView.axaml`, `ViewModels/ToolbarViewModel.cs`, `Views/Dialogs/*` except WP3's, `ViewModels/Dialogs/*` except WP3's (`FinishingDialog` included), `Theme/Dialogs.axaml`, the dialog stack in `DialogHostViewModel.cs` (after WP0) and the dialog roots' automation peers; tests `StatusBarTests`, `ToolbarTests`, `DialogTests` (stacking) | WP0 |
| WP7 | Menus and actions | `ViewModels/Menus/RepositoryMenus.cs`, `ViewModels/Operations/RepositoryOperations.*.cs`, `RepositoryViewModel.Menus.cs` (partial), `LeftPanelViewModel.Actions.cs` (partial: events), `Views/Shell/LeftPanelView.axaml.cs` and `.axaml` (context and double-tap; the drag-source property WP0 put there stays), `WorkingChangesView` (menus), `DiffView` code-behind (context requests); handles the graph's `ContextRequestedAt` through WP0's `HitTest` stub; tests `RepositoryMenusTests`, `RepositoryOperationsTests` (real git through scenarios, scripted dialogs) | WP1, WP2, WP4, WP5, WP6 |
| WP8 | Graph input: multi-select, drag, hit tests | `Controls/CommitGraphControl.cs` (input only; after WP0's declarations), `CommitGraphControl.Selection.cs`, `.Drag.cs`, `.HitTest.cs`; `CommitGraphControl.Render.cs` (selected rows, drag outline); `CommitGraphData.cs` (`IsReachableFromHead`); `Views/Shell/BranchDragService.cs`, `DragLayer` (registers its Esc handler); `RepositoryViewModel.Selection.cs` (partial, implements `OnGraphSwapped`'s re-mapping); `Views/Shell/MultiSelectionView.axaml` (fills WP0's slot) and its automation peer, `CommitDetailsViewModel` (multi state); tests `GraphSelectionTests`, `BranchDragTests` | WP0, WP5 (the drop menu) |
| WP9 | Conflict banner, left panel selection, auto-fetch | `Views/Shell/ConflictBanner.axaml` and its automation peer, `CommitGraphView.axaml` (the banner's place), `RepositoryViewModel.InProgress.cs` (partial, implements `OnStatusApplied` and `OnGraphSelectionChanged`'s `SyncSelection` call), `WorkingChangesViewModel` (titles, `RowToolTip`, `CanCommit` during an operation, MERGE_MSG prefill), `LeftPanelViewModel.cs` (`SyncSelection`), `ViewModels/Operations/AutoFetchScheduler.cs`; sets `WorkingRowText` through the view model; tests `ConflictBannerTests`, `AutoFetchSchedulerTests`, `LeftPanelViewModelTests` additions | WP0, WP1 |
| WP10 | Visual checks and the real-window pass | `VisualTests/Phase3/*`, `RealWindowTests/Phase3RealWindowPass.cs`, `Phase3Expectations.cs`; the phase 0 to 2 tests changed by 6.5 | everything |

Order: WP0 (with WP-S beside it, and the lead's menu spike of 4a) → WP1, WP2, WP3, WP4, WP5,
WP6 in parallel → WP7, WP8, WP9 in parallel → WP10. Strictly sequential: WP0 before everything that compiles against it; WP7 after
WP5 and WP6 (it opens menus and dialogs) and after the Git packages whose operations it calls (it
can start against WP0's stubs and fakes, and finishes when they are merged); WP10 last. The
report's checks (step 5 of the Progress checklist) are written and committed before WP5 to WP9
build the UI they test (D15).

Shared files and their editors: `IGitRepository.cs`, `FakeRepositoryBase.cs`,
`RepositoryViewModel.cs`, `TabServices.cs`, `AppSession.cs`, `MainWindow.axaml`,
`MainWindow.axaml.cs`, `MainWindowViewModel.cs`, `RepoTabViewModel.cs`, `App.axaml`,
`Icons.axaml` and `Tokens.axaml` — WP0 only. `Controls.axaml` is not edited in phase 3 (menus
and dialogs get their own resource files; dialog views are found by `DialogViewLocator`, so WP3
and WP6 add no XAML to a shared file). `LeftPanelViewModel.cs` — WP9; its partial
`LeftPanelViewModel.Actions.cs` — WP7. `LeftPanelView.axaml(.cs)`, `WorkingChangesView`,
`DiffView` — WP7 (after WP0's hooks). `RightPanelView.axaml` — WP0 only (the slot);
`CommitGraphControl*.cs` — WP0 for the declarations, then WP8 only; WP7 uses its events.
`DialogHostViewModel.cs` — WP0, then WP6. `GitRepository.Clone.cs` — WP2 (cleaner); the clone
environment goes through WP0's `CloneAsync` signature and `GitRepositoryProvider.cs`, which WP3
owns. `RepositoryViewModel` grows only through partial files owned by WP7, WP8 and WP9, which
implement the partial methods WP0 declared. If a package still finds it needs a change in a
file it does not own, it asks the lead, who makes it on the phase branch.

## 9. Decisions

Recorded in `decisions.md` as D77 to D104 (dated 2026-10-10); the text there is binding, and
this index only says where each one shows in this design.

| # | In short | Sections |
|---|---|---|
| D77 | Owner: later phases' actions are left out of the menus, not disabled | 4b, 4g, 4h |
| D78 | Owner: pull follows git's configuration and merges when nothing is set | 2.18 |
| D79 | Owner: auto-fetch on, every 5 minutes, active tab, quiet, tests off | 2.17, 6.1 |
| D80 | Owner: "Stash changes and check out" in the checkout-blocked dialog | 2.1, 2.31 |
| D81 | One operation queue per repository; UI context; opt-in dedupe; only the user kills git; exit waits for running operations; stale locks reported | 1 |
| D82 | Prompts through the askpass helper and a named pipe; Ask, Deny and None modes; replaces D30's last sentence | 3 |
| D83 | Confirmations and backup refs, including the hard reset's untracked and staged-new files, a detached HEAD's commits and a removed remote's branches | 2.0 and each operation |
| D84 | Pull = cancellable fetch, then the merge or rebase `git pull` would choose; parity tests | 2.18, 1.9 |
| D85 | Auto-fetch's mechanics: background slot, never pre-empted or killed, no maintenance | 1.8, 2.17, 6.1 |
| D86 | Menus as data, drawn inside the window; stock overlay flyout or own control by a spike; Shift+F10 | 4a |
| D87 | Branch drag is a pointer drag inside the window; drop opens the menu | 4g |
| D88 | Multi-select by SHAs with the platform's command modifier; one pick or revert per commit, no sequencer | 4h, 2.13, 2.14 |
| D89 | Basic conflict state from git's files; banner with Abort; committing finishes a merge or single pick; continue is phase 4's | 5 |
| D90 | Toolbar buttons and when they are disabled | 4d |
| D91 | How "Stash changes and check out" works; tracked changes only; `pop --index`; every outcome names the stash | 2.31 |
| D92 | What has no menu: later phases' items, folders, "Pull requests", repository tabs | 4b, 4i |
| D93 | Checking out a remote branch; double-click map | 2.2, 4c |
| D94 | Merge with git's message for the short name (`fmt-merge-msg`); fast-forward of another branch with `git fetch .`; `-m 1` | 2.7 to 2.10, 2.14 |
| D95 | Results stay in the status bar; failures in the error dialog; conflicts are not failures (one exception) | 4e, 4f, 2.31 |
| D96 | Name validation in C#, including folder clashes and case on Windows and macOS | 2.4, 2.5 |
| D97 | Credential tests against `AuthRemoteServer`; ports | 3.5, 7.3 |
| D98 | The left panel's selection follows the graph's | 6.3 |
| D99 | Fetch passes neither `--prune` nor `--tags` | 2.16 |
| D100 | Force push with `--force-if-includes`; remote deletions with an explicit lease | 2.20, 2.22, 2.29 |
| D101 | Upstreams read as full ref, remote name and remote ref; local upstreams | Conventions, 2.7, 2.18 to 2.21 |
| D102 | `GIT_SEQUENCE_EDITOR=:` on every call | 2.18 |
| D103 | Credentials hidden in recorded output and on screen | 1.7, 2.23 |
| D104 | Dialogs stack | 4f, 3.2 |

## 10. What the reviews changed

Three reviews of the first draft (git and data safety with real git 2.36; UI and harnesses with
a headless spike on Avalonia 12.1.3; coverage and consistency) were checked against real git
and the code before they were accepted or rejected. Accepted and folded in above: the hard
reset's silent loss of untracked and staged-new files; `update-ref` moving a branch checked out
in another worktree; the detached HEAD's commits left behind; remote deletions without a lease;
the lease defeated by auto-fetch; `git merge` writing the full ref into its message; upstreams
cut from `%(upstream:short)`; the untracked case of "Stash changes and check out"; dedupe by an
undefined key; delegates moved off the UI thread; killing a background fetch (stale locks);
pull's unreliable cancel window; credentials in the call log; nested dialogs waiting for each
other; the remote removal without a backup; the annotated tag's cleanup; `sequence.editor`;
committing a merge with nothing staged and an empty pick; name clashes; prompts during local
operations (LFS); Push and Stash on an unborn HEAD; app exit in the middle of a multi-step
operation; backup ref retries; Shift+F10; multi-selection by row numbers; the work packages'
overlapping files; missing visual checks and real-window coverage; the reason given for the
in-window menu; automation peers; the held server's details; fixed ports; the press selecting
the dragged row; fakes in `ShellDriver` and the owner's clipboard in the real-window pass; the
command modifier on macOS; the time budget; the multi-commit cherry-pick's sequencer; D30's
replaced sentence; the conflict-dialog exception; the reset's single confirmation; the Pull
button after rejecting another branch's push; pull's success count; the auth switch for check
17; the open ambiguities; the plan's phase 4 entry and repository tabs.
