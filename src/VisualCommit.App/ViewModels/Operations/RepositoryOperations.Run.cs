using System.Runtime.CompilerServices;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

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
///         async (context, _) =&gt; { await Git.DeleteBranchAsync(branch); return OperationReport.Done(OperationTexts.DeletedBranch(branch)); });
/// });
/// </code>
/// </para>
/// </summary>
public sealed partial class RepositoryOperations
{
    private const string HeadsPrefix = "refs/heads/";
    private const string RemotesPrefix = "refs/remotes/";
    private const string TagsPrefix = "refs/tags/";

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
    /// Dialogs name the repository under their title ("In &lt;name&gt;", 4f): the window sets it while
    /// it has more than one tab with a repository. False (the default) names none.
    /// </summary>
    public bool NamesRepositoryInDialogs { get; set; }

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

        var operation = new GitOperation(text.Title, text.RunningText, kind, refresh, run)
        {
            DedupeKey = options.DedupeKey,
            HasSeveralSteps = options.HasSeveralSteps,
            Prompts = options.Prompts,
        };
        var result = await Queue.RunAsync(operation);
        await ShowResultAsync(text, result, options);
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
        return saved;
    }

    /// <summary>
    /// Shows what an ended operation left to show (D95): a failure in the error dialog, with
    /// <see cref="RunOptions.DescribeFailure"/>'s title, summary and extra button, or 2.33's
    /// error title with the operation's summary; the lock files of a cancelled network operation
    /// in the "&lt;Title&gt; cancelled" dialog (1.8). A success, a conflict (the banner shows it)
    /// and a plain cancel show nothing here. After the tab closed, a failure is only logged.
    /// </summary>
    public async Task ShowResultAsync(OperationText text, OperationResult result, RunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(result);
        options ??= RunOptions.Default;
        if (IsClosed)
        {
            if (result.Outcome == OperationOutcome.Failed)
            {
                Log.Info($"{Repository.Name}: {text.Title} failed after the tab closed: {result.Failure?.GitOutput}");
            }

            return;
        }

        if (result is { Outcome: OperationOutcome.Failed, Failure: { } failure } && options.ShowsErrorDialog)
        {
            await ShowFailureAsync(options.DescribeFailure?.Invoke(failure) ?? DefaultFailureDisplay(text, failure), failure);
        }
        else if (result is { Outcome: OperationOutcome.Cancelled, StaleLockFiles.Count: > 0 })
        {
            await ShowStaleLockFilesAsync(text, result.StaleLockFiles);
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

        dialog.RepositoryName = NamesRepositoryInDialogs ? Repository.Name : null;
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

    /// <summary>A failure the operation reports itself: the status bar says "&lt;Title&gt; failed" (4e) and the dialog shows <paramref name="failure"/>'s summary.</summary>
    public static OperationReport Failed(OperationText text, GitFailure failure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(failure);
        return OperationReport.Failed($"{text.Title} failed", failure);
    }

    // The error dialog's extra buttons (D95).

    /// <summary>"Stash changes and check out" (2.31): only for a checkout blocked by tracked changes.</summary>
    public ErrorAction StashAndCheckoutAction(string target) =>
        new(OperationTexts.StashAndCheckOutButton, () => StashAndCheckoutAsync(target));

    /// <summary>"Pull" (2.19): after a rejected push of the current branch to its upstream.</summary>
    public ErrorAction PullAction() => new(OperationTexts.PullButton, PullAsync);

    /// <summary>"Fetch &lt;r&gt;" (2.19): after a rejected push of another branch.</summary>
    public ErrorAction FetchAction(string remote) => new(OperationTexts.FetchButton(remote), () => FetchAsync(remote));

    /// <summary>
    /// The dialog of a checkout's failure (2.1 to 2.3): "Checkout blocked" for tracked changes, with
    /// "Stash changes and check out" when <paramref name="offerStash"/> (not after 4g's drop), or
    /// for untracked files in the way, without it; null for any other failure (2.33's default).
    /// </summary>
    /// <param name="failure">The failure.</param>
    /// <param name="target">What is checked out, as <see cref="CheckoutAsync"/> names it.</param>
    /// <param name="offerStash">Offer "Stash changes and check out".</param>
    public FailureDisplay? CheckoutFailureDisplay(GitFailure failure, string target, bool offerStash)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(target);
        var name = ShortName(target);
        return failure.Kind switch
        {
            GitFailureKind.CheckoutBlockedByLocalChanges => new FailureDisplay(
                OperationTexts.CheckoutBlockedTitle,
                OperationTexts.CheckoutBlockedByChanges(name),
                offerStash ? StashAndCheckoutAction(target) : null),
            GitFailureKind.CheckoutBlockedByUntrackedFiles => new FailureDisplay(
                OperationTexts.CheckoutBlockedTitle,
                OperationTexts.CheckoutBlockedByUntrackedFiles(name)),
            _ => null,
        };
    }

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
    /// "Remove lock files" (1.8): deletes those of <paramref name="files"/> that still exist; a file
    /// that cannot be deleted opens "Removing lock files failed" with the system's message.
    /// </summary>
    public async Task RemoveLockFilesAsync(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var problems = new List<string>();
        foreach (var file in files)
        {
            try
            {
                if (File.Exists(file))
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

    /// <summary>A stub left by WP7-core for a sub-package to replace: a task that fails with "WP7: &lt;package&gt;: &lt;method&gt;". The last sub-package deletes it.</summary>
    private static Task NotBuilt(string package, [CallerMemberName] string method = "") =>
        Task.FromException(new NotImplementedException($"WP7: {package}: {method}"));

    /// <summary>As <see cref="NotBuilt(string, string)"/> for a method with a result.</summary>
    private static Task<T> NotBuilt<T>(string package, [CallerMemberName] string method = "") =>
        Task.FromException<T>(new NotImplementedException($"WP7: {package}: {method}"));

    private IReadOnlyList<string> NamesOf(RefKind kind) =>
        Refs?.Refs.Where(gitRef => gitRef.Kind == kind).Select(gitRef => gitRef.Name).ToList() ?? [];

    private async Task ShowStaleLockFilesAsync(OperationText text, IReadOnlyList<string> files)
    {
        var dialog = new ErrorDialogViewModel(
            OperationTexts.Cancelled(text.Title),
            OperationTexts.StaleLockFilesSummary,
            Repository.Name,
            string.Join("\n", files.Select(ShowPath)),
            command: null,
            extraAction: OperationTexts.RemoveLockFilesButton,
            clipboard: Services?.Clipboard,
            log: Log);
        if (await ShowDialogAsync(dialog))
        {
            await RemoveLockFilesAsync(files);
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
