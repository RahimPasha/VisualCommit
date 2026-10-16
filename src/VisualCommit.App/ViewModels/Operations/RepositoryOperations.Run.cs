using System.Runtime.CompilerServices;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>An extra button of the error dialog (D95) and what it starts when it is clicked: "Stash changes and check out", "Pull", "Fetch origin", "Remove lock files".</summary>
/// <param name="Label">The button's label.</param>
/// <param name="Run">What it starts after the dialog has closed.</param>
public sealed record ErrorAction(string Label, Func<Task> Run);

/// <summary>How one failure is shown in the error dialog (4f): its title, its summary and its extra button.</summary>
/// <param name="Title">"Checkout blocked", "Push failed", … (2.33).</param>
/// <param name="Summary">The sentence that says what happened.</param>
/// <param name="Extra">The extra button, or null for none.</param>
public sealed record FailureDisplay(string Title, string Summary, ErrorAction? Extra = null);

/// <summary>What <see cref="RepositoryOperations.RunAsync"/> needs beyond the texts, the kind and the refresh.</summary>
public sealed record RunOptions
{
    /// <summary>No dedupe, one step, the queue's prompts, failures in the error dialog with 2.33's texts.</summary>
    public static RunOptions Default { get; } = new();

    /// <summary>The operation's <see cref="GitOperation.DedupeKey"/> (1.2): only Fetch, Pull and Push without choices set one.</summary>
    public string? DedupeKey { get; init; }

    /// <summary>A local operation of several git calls (<see cref="GitOperation.HasSeveralSteps"/>, 1.8).</summary>
    public bool HasSeveralSteps { get; init; }

    /// <summary>
    /// A local operation that only waits for another program and can be cancelled
    /// (<see cref="GitOperation.CanBeCancelled"/>: the merge tool, docs/phase-4-design.md 11.9, D149).
    /// </summary>
    public bool CanBeCancelled { get; init; }

    /// <summary>The operation's <see cref="GitOperation.Prompts"/>; null for the queue's default (Ask).</summary>
    public PromptMode? Prompts { get; init; }

    /// <summary>A failure opens the error dialog (true, the default); false where the caller shows it itself (the Remotes form, 2.23).</summary>
    public bool ShowsErrorDialog { get; init; } = true;

    /// <summary>
    /// Picks the error dialog for a failure: a dialog of its own for a recognised kind ("Checkout
    /// blocked", "Push rejected", …), or null for the default, 2.33's error title with its summary
    /// (or the failure's own summary when the operation reported the failure itself). Called after
    /// the operation has ended, so an operation can also hand over a display it chose while it ran.
    /// </summary>
    public Func<GitFailure, FailureDisplay?>? DescribeFailure { get; init; }

    /// <summary>
    /// What the undo history records of the operation (docs/phase-5-design.md 7.2, 7.7): its kind,
    /// its entry's text and what it may change, read inside the operation before and after its
    /// work; <see cref="UndoScope.None"/> (the default) for an operation that is not recorded.
    /// </summary>
    public UndoScope Undo { get; init; } = UndoScope.None;
}

/// <summary>
/// The helpers every operation shares (docs/phase-3-design.md 1.3 to 1.6, 1.8, 2.0, 2.33, 4f,
/// D95): running a <see cref="GitOperation"/> through the queue with its texts and refresh,
/// showing its failure in the error dialog with an extra button, confirming with a backup first,
/// showing dialogs, validating names, looking up refs, remotes, stashes and commits, and doing
/// nothing visible once the tab is closed. Built by WP7-core; the four sub-packages build on it.
/// <para>
/// A typical operation (a sketch of 2.6):
/// <code>
/// public Task DeleteBranchAsync(string branchRef) =&gt; GuardAsync(OperationTexts.DeleteBranch(ShortName(branchRef)), async () =&gt;
/// {
///     var branch = ShortName(branchRef);
///     var text = OperationTexts.DeleteBranch(branch);
///     var confirmation = OperationTexts.ConfirmDeleteBranch(branch, …);
///     await ConfirmAndRunAsync(confirmation, text, OperationKind.Local, RefreshScope.Refs,
///         _ =&gt; Task.FromResult&lt;BackupRequest?&gt;(new BackupRequest("branch", [new BackupEntry(branch, branchRef)])),
///         async (context, _) =&gt; { await Git.DeleteBranchAsync(branch); return OperationReport.Done(OperationTexts.DeletedBranch(branch)); },
///         new RunOptions { Undo = UndoScope.DeleteBranch(branch) });
/// });
/// </code>
/// </para>
/// </summary>
public sealed partial class RepositoryOperations
{
    private const string HeadsPrefix = "refs/heads/";
    private const string RemotesPrefix = "refs/remotes/";
    private const string TagsPrefix = "refs/tags/";

    /// <summary>The queue's tasks whose result a caller of <see cref="RunAsync"/> shows: a deduplicated second call does not show it again.</summary>
    private readonly ConditionalWeakTable<Task<OperationResult>, OperationText> _resultsShown = new();

    /// <summary>Set while an operation of <see cref="RunAsync"/> runs: <see cref="SaveBackupAsync"/> marks it, and the operation then ends with the backups' clean-up (D139).</summary>
    private readonly AsyncLocal<StrongBox<bool>?> _backupSaved = new();

    /// <summary>
    /// Set while a recorded operation of <see cref="RunAsync"/> runs: HEAD as the undo history's
    /// recording read it just before the operation's work (docs/phase-5-design.md 7.7), which the
    /// operation's own checks of HEAD share (<see cref="ReadHeadAtStartAsync"/>). Null when the
    /// operation is not recorded or completes a pending entry.
    /// </summary>
    private readonly AsyncLocal<HeadState?> _headAtStart = new();

    // The repository's state.

    /// <summary>The tab was closed: nothing starts any more, and nothing is shown (1.8).</summary>
    public bool IsClosed => Repository.IsClosed;

    /// <summary>The repository's git calls.</summary>
    public IGitRepository Git => Repository.Repository;

    /// <summary>The refs as last read, or null before the first read.</summary>
    public RepoRefs? Refs => Repository.Refs;

    /// <summary>The working tree's status as last read.</summary>
    public WorkingTreeStatus Status => Repository.Status;

    /// <summary>
    /// Dialogs name the repository under their title ("In &lt;name&gt;", 4f): the window's rule
    /// (<see cref="TabServices.NamesRepositoryInDialogs"/>, more than one tab has a repository),
    /// read when a dialog opens. False without services.
    /// </summary>
    public bool NamesRepositoryInDialogs => Services?.NamesRepositoryInDialogs() ?? false;

    /// <summary>Names compare ignoring case (D96); null (the default) follows <see cref="RefNameRules.NamesIgnoreCase"/>. Tests set it.</summary>
    public bool? IgnoreCaseInNames { get; set; }

    /// <summary>Whether names compare ignoring case now.</summary>
    public bool IgnoreCase => IgnoreCaseInNames ?? RefNameRules.NamesIgnoreCase;

    // Lookups (from RepositoryViewModel.Refs and the graph).

    /// <summary>The branch HEAD is on, or null while HEAD is detached, unborn without a ref, or before the refs are read.</summary>
    public GitRef? CurrentBranch => Refs?.Head.BranchName is { } branch ? FindRef(HeadsPrefix + branch) : null;

    /// <summary>How texts name what HEAD is on: the current branch's name, or "HEAD" while it is detached (4b).</summary>
    public string HeadName => Refs?.Head.BranchName ?? "HEAD";

    /// <summary>The configured remotes' names, sorted.</summary>
    public IReadOnlyList<string> RemoteNames => Refs?.Remotes ?? [];

    /// <summary>The local branches' short names, in git's order.</summary>
    public IReadOnlyList<string> LocalBranchNames => NamesOf(RefKind.LocalBranch);

    /// <summary>The tags' names, in git's order.</summary>
    public IReadOnlyList<string> TagNames => NamesOf(RefKind.Tag);

    /// <summary>The branch, remote branch or tag with this full name, or null.</summary>
    public GitRef? FindRef(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        return Refs?.Refs.FirstOrDefault(gitRef => string.Equals(gitRef.FullName, fullName, StringComparison.Ordinal));
    }

    /// <summary>The remote branches of <paramref name="remote"/>, in git's order.</summary>
    public IReadOnlyList<GitRef> RemoteBranchesOf(string remote)
    {
        ArgumentNullException.ThrowIfNull(remote);
        return Refs?.Refs.Where(gitRef => gitRef.Kind == RefKind.RemoteBranch && gitRef.RemoteName == remote).ToList() ?? [];
    }

    /// <summary>The stash whose commit is <paramref name="sha"/>, with its present name (<c>stash@{n}</c>), or null when it is gone.</summary>
    public StashEntry? FindStash(string sha)
    {
        ArgumentNullException.ThrowIfNull(sha);
        return Refs?.Stashes.FirstOrDefault(stash => string.Equals(stash.Sha, sha, StringComparison.Ordinal));
    }

    /// <summary>The commit <paramref name="sha"/> as the graph has loaded it, or null.</summary>
    public CommitInfo? FindCommit(string sha)
    {
        ArgumentNullException.ThrowIfNull(sha);
        var graph = Repository.Graph;
        var index = graph.IndexOf(sha);
        return index >= 0 ? graph.CommitAt(index) : null;
    }

    /// <summary>How texts name a branch's upstream: <c>origin/main</c>, or a local upstream's branch alone (D114); null without one.</summary>
    public static string? UpstreamText(GitRef branch) => ToolbarViewModel.UpstreamText(branch);

    /// <summary>
    /// The short name texts use for <paramref name="target"/> (design 2): <c>refs/heads/x</c> → <c>x</c>,
    /// <c>refs/remotes/origin/x</c> → <c>origin/x</c>, <c>refs/tags/v1</c> → <c>v1</c>, a full SHA
    /// → its first 7 characters; anything else as it is.
    /// </summary>
    public static string ShortName(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        foreach (var prefix in (ReadOnlySpan<string>)[HeadsPrefix, RemotesPrefix, TagsPrefix])
        {
            if (target.StartsWith(prefix, StringComparison.Ordinal))
            {
                return target[prefix.Length..];
            }
        }

        return IsFullSha(target) ? OperationTexts.Short(target) : target;
    }

    /// <summary>Whether <paramref name="text"/> is a full commit id: 40 (SHA-1) or 64 (SHA-256) lowercase hex digits.</summary>
    public static bool IsFullSha(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length is 40 or 64 && text.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    /// <summary>
    /// A commit as dialogs name it, <c>a1b2c3d "Add core"</c> (design 2): its subject from the
    /// graph, or read from git when the graph has not loaded it; the short SHA alone when it
    /// cannot be read.
    /// </summary>
    public async Task<string> DescribeCommitAsync(string sha)
    {
        ArgumentNullException.ThrowIfNull(sha);
        var subject = await ReadSubjectAsync(sha);
        return subject is null ? OperationTexts.Short(sha) : DialogText.Commit(sha, subject);
    }

    /// <summary>A commit's subject: from the graph, or from git; null when it cannot be read.</summary>
    public async Task<string?> ReadSubjectAsync(string sha)
    {
        ArgumentNullException.ThrowIfNull(sha);
        if (FindCommit(sha) is { } commit)
        {
            return commit.Subject;
        }

        try
        {
            return (await Git.ReadCommitDetailsAsync(sha)).Subject;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning($"{Repository.Name}: the subject of {sha} could not be read.", ex);
            return null;
        }
    }

    /// <summary>
    /// How many files are conflicted now: a fresh read of the status (<c>GIT_OPTIONAL_LOCKS=0</c>),
    /// for the conflict messages ("Merge stopped: 2 files have conflicts"), which are made inside
    /// the operation, before its refresh.
    /// </summary>
    public async Task<int> CountConflictedFilesAsync()
    {
        var status = await Git.ReadStatusAsync();
        return status.Unstaged.Count(file => file.Kind == FileChangeKind.Conflicted);
    }

    // Names (2.4, 2.5, 2.23, 2.27, D96): the repository's existing names, the platform's case rule.

    /// <summary>What the name box says about a branch name, or null when it is a valid new name; <paramref name="renaming"/> is left out of the existing names.</summary>
    public string? BranchNameError(string name, string? renaming = null) =>
        DialogText.BranchNameError(name, LocalBranchNames.Where(other => !string.Equals(other, renaming, StringComparison.Ordinal)), IgnoreCase);

    /// <summary>What the name box says about a tag name, or null.</summary>
    public string? TagNameError(string name) => DialogText.TagNameError(name, TagNames, IgnoreCase);

    /// <summary>What the Remotes form says about a remote's name, or null; <paramref name="renaming"/> is left out of the existing names.</summary>
    public string? RemoteNameError(string name, string? renaming = null) =>
        DialogText.RemoteNameError(name, RemoteNames.Where(other => !string.Equals(other, renaming, StringComparison.Ordinal)), IgnoreCase);

    /// <summary>The "Create branch" dialog (2.4) with this repository's branches and case rule.</summary>
    public BranchNameDialogViewModel NewBranchDialog(string context) => BranchNameDialogViewModel.Create(context, LocalBranchNames, IgnoreCase);

    /// <summary>The "Rename branch" dialog (2.5) with this repository's branches and case rule.</summary>
    public BranchNameDialogViewModel RenameBranchDialog(string branch) => BranchNameDialogViewModel.Rename(branch, LocalBranchNames, IgnoreCase);

    /// <summary>The "Create tag" dialog (2.27) with this repository's tags and case rule.</summary>
    public TagDialogViewModel NewTagDialog(string context) => new(context, TagNames, IgnoreCase);

    // Running an operation.

    /// <summary>
    /// Runs one operation through the queue (1.3) with 2.33's <paramref name="text"/>, then shows
    /// what it left to show: a failure in the error dialog (<see cref="ShowResultAsync"/>), the
    /// lock files a cancelled network operation left (1.8). The status bar shows the result by
    /// itself (<see cref="OperationStatusViewModel"/>: the report's message, "&lt;Title&gt;
    /// failed" or "&lt;Title&gt; cancelled"). Never throws for the operation's failure: the queue
    /// turns <see cref="GitException"/>, <see cref="IOException"/>, a cancel and any other
    /// exception of <paramref name="run"/> into the result. After the tab closed it starts nothing
    /// and returns <see cref="OperationOutcome.Cancelled"/>. Call it on the UI thread.
    /// </summary>
    public async Task<OperationResult> RunAsync(
        OperationText text,
        OperationKind kind,
        RefreshScope refresh,
        Func<OperationContext, Task<OperationReport>> run,
        RunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(run);
        options ??= RunOptions.Default;
        if (IsClosed)
        {
            Log.Info($"{Repository.Name}: {text.Title}: not started, the tab is closed.");
            return new OperationResult(OperationOutcome.Cancelled, OperationTexts.Cancelled(text.Title), null);
        }

        // The operation's id, for the git call a "<Title> cancelled" dialog of lock files names (1.8, 4f).
        string? operationId = null;
        var operation = new GitOperation(text.Title, text.RunningText, kind, refresh, async context =>
        {
            operationId = context.OperationId;

            // An operation that saved a backup (SaveBackupAsync) ends with the backups' clean-up,
            // inside it, after its work, whether the work succeeded or not (docs/phase-4-design.md
            // section 9, D139): a rebase git refused still leaves its backup.
            var saved = new StrongBox<bool>();
            _backupSaved.Value = saved;

            // The undo history (docs/phase-5-design.md 7.7): what the operation's scope names is
            // read before its work and after it, inside the operation, also when the work failed
            // half-way; what the reads cannot see is captured while it runs. The entry is added
            // before the clean-up, which then keeps the backups it needs.
            var recording = await BeginRecordingAsync(options.Undo, context, text.Title);
            _headAtStart.Value = recording?.Before?.Head;
            OperationReport? report = null;
            using (UndoCapture.Begin(out var capture))
            {
                try
                {
                    report = await run(context);
                    return report;
                }
                finally
                {
                    await EndRecordingAsync(recording, capture.Captured, report, text.Title);
                    if (saved.Value)
                    {
                        await CleanUpBackupsAsync();
                    }
                }
            }
        })
        {
            DedupeKey = options.DedupeKey,
            HasSeveralSteps = options.HasSeveralSteps,
            Prompts = options.Prompts,
            CanBeCancelled = options.CanBeCancelled,
        };
        // A second call with the dedupe key of a waiting or running operation gets that
        // operation's task (1.2): only the first caller shows its result, so a failure opens one
        // error dialog, not one per click.
        var running = Queue.RunAsync(operation);
        var first = _resultsShown.TryAdd(running, text);
        var result = await running;
        if (first)
        {
            await ShowResultAsync(text, result, options, operationId);
        }

        return result;
    }

    /// <summary>
    /// Asks <paramref name="confirmation"/>, then runs the operation (<see cref="RunAsync"/>), which
    /// first saves the backup <paramref name="backup"/> returns (2.0, D83) and then runs
    /// <paramref name="run"/> with what was saved. The backup is made inside the operation, so it
    /// holds what the refs are when it runs (2.22); when it fails, nothing else runs and the error
    /// dialog says so. Returns null when the user did not confirm.
    /// </summary>
    public async Task<OperationResult?> ConfirmAndRunAsync(
        ConfirmationRequest confirmation,
        OperationText text,
        OperationKind kind,
        RefreshScope refresh,
        Func<OperationContext, Task<BackupRequest?>> backup,
        Func<OperationContext, BackupResult?, Task<OperationReport>> run,
        RunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(run);
        if (!await ConfirmAsync(confirmation))
        {
            return null;
        }

        return await RunAsync(
            text,
            kind,
            refresh,
            async context =>
            {
                var saved = await SaveBackupAsync(await backup(context));
                return await run(context, saved);
            },
            options);
    }

    /// <summary>
    /// Writes a backup batch (2.0) and logs its refs; nothing for null or an empty request. Call it
    /// inside an operation, before what it protects; it is never cancelled half-way.
    /// </summary>
    public async Task<BackupResult?> SaveBackupAsync(BackupRequest? request)
    {
        if (request is null || (request.Entries.Count == 0 && request.ChangesCommit is null))
        {
            return null;
        }

        var saved = await Git.SaveBackupRefsAsync(request, CancellationToken.None);
        Log.Info($"{Repository.Name}: saved the backup {string.Join(", ", saved.Refs)}");
        MarkBackupSaved();

        // The undo entry needs it: the clean-up keeps it while the entry exists (7.8, D171).
        UndoCapture.Backups(saved.Refs);
        return saved;
    }

    /// <summary>
    /// The running operation saved a backup (a snapshot too): it ends with the backups' clean-up,
    /// after its undo entry is recorded, which keeps the backups the entry needs (D139, 7.8).
    /// </summary>
    private void MarkBackupSaved()
    {
        if (_backupSaved.Value is { } flag)
        {
            flag.Value = true;
        }
    }

    // The undo history's recording (docs/phase-5-design.md 7.7, D167), around the work of every
    // operation of RunAsync and of the stage panel's writes (RepositoryViewModel.WriteAsync).
    // Work package WP3 implements the two hooks in RepositoryOperations.Undo.cs.

    /// <summary>
    /// Hook (7.7 steps 1 and 2): inside the operation, before its work, reads what
    /// <paramref name="scope"/> names (HEAD always) and returns what
    /// <see cref="EndUndoRecordingAsync"/> needs, or null when nothing is recorded
    /// (<see cref="UndoScope.None"/>, and no pending entry to complete or remove).
    /// </summary>
    internal partial Task<UndoRecording?> BeginUndoRecordingAsync(UndoScope scope, OperationContext context);

    /// <summary>
    /// Hook (7.7 steps 4 and 5): after the work, still inside the operation, reads the same again
    /// and, when anything changed, adds the entry with <paramref name="captured"/> (Done, or Pending
    /// while the action stopped) and drops what lay after the cursor; or completes or removes the
    /// pending entry. <paramref name="report"/> is null when the work threw: what changed is still
    /// recorded (7.2). Runs on the operation's context, the UI's.
    /// </summary>
    internal partial Task EndUndoRecordingAsync(UndoRecording? recording, UndoCaptured captured, OperationReport? report);

    /// <summary>
    /// <see cref="BeginUndoRecordingAsync"/>, which never fails the operation: a failure is logged
    /// and nothing is recorded. The stage panel's writes call it too.
    /// </summary>
    internal async Task<UndoRecording?> BeginRecordingAsync(UndoScope scope, OperationContext context, string title)
    {
        try
        {
            return await BeginUndoRecordingAsync(scope, context);
        }
        catch (Exception ex)
        {
            Log.Warning($"{Repository.Name}: {title}: the undo history could not read the state before it; it is not recorded.", ex);
            return null;
        }
    }

    /// <summary>
    /// HEAD at the start of the running operation's work: the undo history's read of it when the
    /// operation is recorded (docs/phase-5-design.md 7.7: "Operations that already read HEAD for
    /// D124 share that read"), otherwise read now. Only for the checks an operation makes before
    /// it writes anything that can move HEAD (a backup ref moves nothing): D124's "HEAD moved
    /// while this waited", 2.1's detached commits, and the commit a reset or a rebase starts from.
    /// A check made after such a write, or after a wait such as a pull's fetch, reads HEAD itself.
    /// </summary>
    private Task<HeadState> ReadHeadAtStartAsync() =>
        _headAtStart.Value is { } head ? Task.FromResult(head) : Git.ReadHeadAsync();

    /// <summary><see cref="EndUndoRecordingAsync"/>, which never fails the operation: a failure is logged.</summary>
    internal async Task EndRecordingAsync(UndoRecording? recording, UndoCaptured captured, OperationReport? report, string title)
    {
        try
        {
            await EndUndoRecordingAsync(recording, captured, report);
        }
        catch (Exception ex)
        {
            Log.Warning($"{Repository.Name}: {title}: the undo history could not record it.", ex);
        }
    }

    /// <summary>
    /// The backups' clean-up after an operation that saved a backup (docs/phase-4-design.md
    /// section 9, D139): the backups older than 30 days, then the oldest beyond 50, never the
    /// restore bar's snapshot (<see cref="RepositoryViewModel.BackupsToKeep"/>). It never fails the
    /// operation and shows nothing: the git layer logs each backup it removed, or why it waits;
    /// a failure is logged at Warning. Each removal is also a note of the operation it runs in, for
    /// the activity log (docs/phase-5-design.md 2.3). Returns what it removed, empty after a failure.
    /// </summary>
    public async Task<IReadOnlyList<RemovedBackup>> CleanUpBackupsAsync()
    {
        try
        {
            var retention = BackupRetention.Default;
            var removed = await Git.CleanUpBackupsAsync(retention, Repository.BackupsToKeep, options: null, cancellationToken: CancellationToken.None);
            if (GitOperationScope.Current is { } operationId && Services?.Operations is { } operations)
            {
                foreach (var backup in removed)
                {
                    operations.Note(operationId, backup.Describe(retention));
                }
            }

            return removed;
        }
        catch (Exception ex)
        {
            Log.Warning($"{Repository.Name}: the backups' clean-up failed; the backups stay.", ex);
            return [];
        }
    }

    /// <summary>
    /// Shows what an ended operation left to show (D95): a failure in the error dialog, with
    /// <see cref="RunOptions.DescribeFailure"/>'s title, summary and extra button, or 2.33's
    /// error title with the operation's summary; the lock files of a cancelled network operation
    /// in the "&lt;Title&gt; cancelled" dialog (1.8). A success, a conflict (the banner shows it)
    /// and a plain cancel show nothing here. After the tab closed, a failure is only logged.
    /// <paramref name="operationId"/> names the operation, whose last git call the lock-file dialog
    /// shows as its command (4f: "the cancelled git call"), and whose record in the operation log
    /// gets the failure's summary as the dialog gives it, also when no dialog opens
    /// (docs/phase-5-design.md 2.3).
    /// </summary>
    public async Task ShowResultAsync(OperationText text, OperationResult result, RunOptions? options = null, string? operationId = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(result);
        options ??= RunOptions.Default;
        var failure = result.Outcome == OperationOutcome.Failed ? result.Failure : null;
        var display = failure is null ? null : options.DescribeFailure?.Invoke(failure) ?? DefaultFailureDisplay(text, failure);
        if (display is not null && operationId is not null)
        {
            Services?.Operations.SetFailureSummary(operationId, display.Summary);
        }

        if (IsClosed)
        {
            if (result.Outcome == OperationOutcome.Failed)
            {
                Log.Info($"{Repository.Name}: {text.Title} failed after the tab closed: {result.Failure?.GitOutput}");
            }

            return;
        }

        if (failure is not null && display is not null && options.ShowsErrorDialog)
        {
            await ShowFailureAsync(display, failure);
        }
        else if (result is { Outcome: OperationOutcome.Cancelled, StaleLockFiles.Count: > 0 })
        {
            await ShowStaleLockFilesAsync(text, result.StaleLockFiles, operationId);
        }
    }

    /// <summary>
    /// 2.33's dialog for a failure without one of its own: the error title, and the operation's
    /// failure summary in place of the queue's "&lt;Title&gt; failed."; a summary the operation
    /// reported itself (<see cref="OperationReport.Failed"/>) is kept.
    /// </summary>
    public static FailureDisplay DefaultFailureDisplay(OperationText text, GitFailure failure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(failure);
        var queueSummary = string.Equals(failure.Summary, $"{text.Title} failed.", StringComparison.Ordinal);
        return new FailureDisplay(text.ErrorTitle, queueSummary ? text.FailureSummary : failure.Summary);
    }

    /// <summary>
    /// The error dialog for <paramref name="failure"/> as <paramref name="display"/> says, with
    /// git's output, the command and "Copy output"; when its extra button is clicked, the dialog
    /// closes and the extra action starts (and this completes when it has ended).
    /// </summary>
    public async Task ShowFailureAsync(FailureDisplay display, GitFailure failure)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(failure);
        var dialog = ErrorDialogViewModel.ForFailure(display.Title, failure with { Summary = display.Summary }, Repository.Name, display.Extra?.Label, Services?.Clipboard, Log);
        if (await ShowDialogAsync(dialog) && display.Extra is { } extra)
        {
            await RunExtraAsync(extra);
        }
    }

    /// <summary>A dialog in the error dialog's layout without git's output, command or "Copy output" (2.31's "Changes re-applied with conflicts", D114).</summary>
    public Task ShowInformationAsync(string title, string summary) =>
        ShowDialogAsync(new ErrorDialogViewModel(title, summary, Repository.Name, clipboard: Services?.Clipboard, log: Log));

    /// <summary>
    /// Shows <paramref name="dialog"/> on the window's dialog layer (D104), naming the repository
    /// when <see cref="NamesRepositoryInDialogs"/>, and returns its answer. Without a dialog layer
    /// (view-model tests without services) or after the tab closed it shows nothing and answers
    /// the default (null, false): what was not confirmed does not happen.
    /// </summary>
    public async Task<T?> ShowDialogAsync<T>(DialogViewModel<T> dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        if (IsClosed || Dialogs is not { } layer)
        {
            Log.Info($"{Repository.Name}: the dialog \"{dialog.Title}\" was not shown: {(IsClosed ? "the tab is closed" : "there is no dialog layer")}.");
            return default;
        }

        if (NamesRepositoryInDialogs)
        {
            // An error dialog names its repository always, from its constructor (4f).
            dialog.RepositoryName = Repository.Name;
        }

        return await layer.ShowAsync(dialog);
    }

    /// <summary>
    /// Asks a confirmation (phase 2's <c>ConfirmationView</c>), naming the repository when
    /// <see cref="NamesRepositoryInDialogs"/>. False after the tab closed; true without any dialog
    /// service (view-model tests without services, as phase 2's discard).
    /// </summary>
    public async Task<bool> ConfirmAsync(ConfirmationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsClosed)
        {
            return false;
        }

        if (Services?.Dialogs is not { } dialogs)
        {
            return true;
        }

        return await dialogs.ConfirmAsync(request with { RepositoryName = NamesRepositoryInDialogs ? Repository.Name : null });
    }

    /// <summary>
    /// Runs the body of a public operation method so that it never throws: an exception outside the
    /// queue (a read before it, such as <c>CountDetachedCommitsAsync</c>, or a dialog) opens the
    /// error dialog with <paramref name="text"/>'s title and summary and git's output or the
    /// exception's message; an unexpected one is logged at Error too. A cancel shows nothing.
    /// </summary>
    public async Task GuardAsync(OperationText text, Func<Task> body)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            await body();
        }
        catch (OperationCanceledException)
        {
            // The tab closed, or the user cancelled: nothing to show.
        }
        catch (Exception ex)
        {
            await ShowExceptionAsync(text, ex);
        }
    }

    /// <summary>As <see cref="GuardAsync(OperationText, Func{Task})"/> for a body with a result; <paramref name="fallback"/> when it failed.</summary>
    public async Task<T> GuardAsync<T>(OperationText text, Func<Task<T>> body, T fallback)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            return await body();
        }
        catch (OperationCanceledException)
        {
            return fallback;
        }
        catch (Exception ex)
        {
            await ShowExceptionAsync(text, ex);
            return fallback;
        }
    }

    /// <summary>
    /// A failure built from a <see cref="GitException"/> as the queue builds it (1.6): git's output
    /// cleaned of progress and credentials (or the exception's message when git printed nothing),
    /// the kind read from git's text, the command, the exit code, with <paramref name="summary"/>.
    /// For operations that report a failure themselves (<see cref="OperationReport.Failed"/>).
    /// </summary>
    public static GitFailure FailureOf(GitException exception, string summary)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(summary);
        var output = GitOutputCleaner.Clean(exception.StandardError, exception.StandardOutput);
        if (output.Length == 0)
        {
            output = GitCommand.HideCredentials(exception.Message);
        }

        return new GitFailure(
            GitFailureClassifier.Classify(exception.StandardError, exception.StandardOutput, exception.ExitCode),
            summary,
            output,
            exception.CommandText,
            exception.ExitCode);
    }

    /// <summary>
    /// The failure of the operation's last failed git call that <paramref name="isCall"/> picks,
    /// as the session's call log recorded it (1.6, 1.7): git's output cleaned as the queue cleans
    /// it, the command, the exit code, with <paramref name="summary"/>. For a git method that
    /// reports a failed call only as an outcome, so that the error dialog still shows what git
    /// said (an empty cherry-pick's <see cref="ApplyResult.Empty"/>, 2.12: its output and command,
    /// check 25). Without a call log or such a call, the failure has no output and no command.
    /// </summary>
    /// <param name="context">The running operation: only its own calls are looked at.</param>
    /// <param name="isCall">Picks the call by its command text (<c>git cherry-pick &lt;sha&gt;</c>).</param>
    /// <param name="summary">The error dialog's summary.</param>
    public GitFailure FailureOfLastCall(OperationContext context, Func<string, bool> isCall, string summary)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(isCall);
        ArgumentNullException.ThrowIfNull(summary);
        var call = Services?.GitCalls?.Snapshot().LastOrDefault(record =>
            string.Equals(record.OperationId, context.OperationId, StringComparison.Ordinal) && !record.Succeeded && isCall(record.CommandText));
        if (call is null)
        {
            Log.Warning($"{Repository.Name}: the failed git call of {context.OperationId} is not in the call log; its output cannot be shown.");
            return new GitFailure(GitFailureKind.Other, summary, string.Empty, string.Empty, null);
        }

        return new GitFailure(
            GitFailureClassifier.Classify(call.StandardError, call.StandardOutput, call.ExitCode),
            summary,
            GitOutputCleaner.Clean(call.StandardError, call.StandardOutput),
            call.CommandText,
            call.ExitCode);
    }

    /// <summary>A failure the operation reports itself: the status bar says "&lt;Title&gt; failed" (4e) and the dialog shows <paramref name="failure"/>'s summary.</summary>
    public static OperationReport Failed(OperationText text, GitFailure failure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(failure);
        return OperationReport.Failed($"{text.Title} failed", failure);
    }

    // The error dialog's extra buttons (D95); the checkouts' are with their dialog in
    // RepositoryOperations.Branches.cs.

    /// <summary>"Pull" (2.19): after a rejected push of the current branch to its upstream.</summary>
    public ErrorAction PullAction() => new(OperationTexts.PullButton, PullAsync);

    /// <summary>"Fetch &lt;r&gt;" (2.19): after a rejected push of another branch.</summary>
    public ErrorAction FetchAction(string remote) => new(OperationTexts.FetchButton(remote), () => FetchAsync(remote));

    /// <summary>
    /// The dialog of a push's failure (2.19): "Push rejected" with "Pull" when the current branch
    /// was pushed to its upstream (<paramref name="currentToUpstream"/>), otherwise with
    /// "Fetch &lt;r&gt;"; null for any other failure.
    /// </summary>
    public FailureDisplay? PushFailureDisplay(GitFailure failure, string branch, string remote, bool currentToUpstream)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Kind == GitFailureKind.PushRejected
            ? new FailureDisplay(OperationTexts.PushRejectedTitle, OperationTexts.PushRejected(remote, branch), currentToUpstream ? PullAction() : FetchAction(remote))
            : null;
    }

    /// <summary>
    /// "Remove lock files" (1.8): deletes those of <paramref name="files"/> that still exist and,
    /// when <paramref name="listed"/> gives a file's last write time and length as it was listed,
    /// are still that same file: a lock removed meanwhile and taken again by a live git (an
    /// auto-fetch, a terminal) is another file, with another time, and is left alone. A file that
    /// cannot be deleted opens "Removing lock files failed" with the system's message.
    /// </summary>
    public async Task RemoveLockFilesAsync(IReadOnlyList<string> files, IReadOnlyDictionary<string, (DateTime Time, long Length)?>? listed = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        var problems = new List<string>();
        foreach (var file in files)
        {
            try
            {
                if (listed is not null && listed.TryGetValue(file, out var stamp) && LockFiles.Stamp(file) is { } now && now != stamp)
                {
                    Log.Info($"{Repository.Name}: the lock file {file} changed since it was listed; it is left alone.");
                }
                else if (File.Exists(file))
                {
                    File.Delete(file);
                    Log.Info($"{Repository.Name}: removed the lock file {file}");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warning($"{Repository.Name}: the lock file {file} could not be removed.", ex);
                problems.Add(ex.Message);
            }
        }

        if (problems.Count > 0)
        {
            await ShowDialogAsync(new ErrorDialogViewModel(
                OperationTexts.RemoveLockFilesFailedTitle,
                OperationTexts.RemoveLockFilesFailedSummary,
                Repository.Name,
                string.Join("\n", problems),
                clipboard: Services?.Clipboard,
                log: Log));
        }
    }

    /// <summary>
    /// How the lock-file dialog lists a file (4f): relative to the working tree's folder with
    /// <c>/</c> (<c>.git/refs/remotes/origin/main.lock</c>), or its full path when it is outside.
    /// </summary>
    public string ShowPath(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        var relative = Path.GetRelativePath(Repository.Path, fullPath);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? fullPath
            : relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private IReadOnlyList<string> NamesOf(RefKind kind) =>
        Refs?.Refs.Where(gitRef => gitRef.Kind == kind).Select(gitRef => gitRef.Name).ToList() ?? [];

    private async Task ShowStaleLockFilesAsync(OperationText text, IReadOnlyList<string> files, string? operationId)
    {
        // What each file is as it is listed, right after the queue found it: the button removes
        // only files that are still these (1.8).
        var listed = files.ToDictionary(file => file, LockFiles.Stamp, StringComparer.Ordinal);
        // 4f: ErrorCommand is the cancelled git call, the operation's last one in the session's call log.
        var cancelledCall = operationId is null
            ? null
            : Services?.GitCalls?.Snapshot().LastOrDefault(record => string.Equals(record.OperationId, operationId, StringComparison.Ordinal));
        var dialog = new ErrorDialogViewModel(
            OperationTexts.Cancelled(text.Title),
            OperationTexts.StaleLockFilesSummary,
            Repository.Name,
            string.Join("\n", files.Select(ShowPath)),
            command: cancelledCall?.CommandText,
            extraAction: OperationTexts.RemoveLockFilesButton,
            clipboard: Services?.Clipboard,
            log: Log);
        if (await ShowDialogAsync(dialog))
        {
            await RemoveLockFilesAsync(files, listed);
        }
    }

    private async Task RunExtraAsync(ErrorAction extra)
    {
        try
        {
            await extra.Run();
        }
        catch (Exception ex)
        {
            Log.Error($"{Repository.Name}: the error dialog's \"{extra.Label}\" failed.", ex);
        }
    }

    private async Task ShowExceptionAsync(OperationText text, Exception exception)
    {
        GitFailure failure;
        switch (exception)
        {
            case GitException git:
                failure = FailureOf(git, text.FailureSummary);
                Log.Warning($"{Repository.Name}: {text.Title} failed: {failure.GitOutput}");
                break;

            case IOException or UnauthorizedAccessException:
                failure = new GitFailure(GitFailureKind.Other, text.FailureSummary, exception.Message, string.Empty, null);
                Log.Warning($"{Repository.Name}: {text.Title} failed.", exception);
                break;

            default:
                failure = new GitFailure(GitFailureKind.Other, text.FailureSummary, "Unexpected error: " + exception.Message, string.Empty, null);
                Log.Error($"{Repository.Name}: {text.Title}: unexpected error.", exception);
                break;
        }

        if (IsClosed || text.ErrorTitle.Length == 0)
        {
            return;
        }

        try
        {
            await ShowFailureAsync(new FailureDisplay(text.ErrorTitle, text.FailureSummary), failure);
        }
        catch (Exception ex)
        {
            Log.Error($"{Repository.Name}: showing the failure of {text.Title} failed.", ex);
        }
    }
}
