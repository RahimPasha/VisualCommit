using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;

namespace VisualCommit.App.Services;

/// <summary>
/// Answers git's and ssh's prompts with dialogs in the window (docs/phase-3-design.md 3.2, D82).
/// It keeps what one operation's dialogs were told, so that the operation does not ask twice:
/// the password typed with a user name answers git's password prompt that follows, and an answer
/// to a prompt text answers the same text again (a pull's second contact, a fetch of several
/// remotes). Both are kept in memory for this scope only and for at most
/// <see cref="KeepAnswersFor"/>, and are never logged.
/// <para>
/// The dialogs themselves are shown by an <see cref="IPromptDialogPresenter"/>. Until work package
/// WP3b builds the credentials, passphrase, unknown-host and prompt dialogs (they wait until the
/// visual checks that test them are committed, D15), the presenter is
/// <see cref="PromptDialogsPendingPresenter"/>, which shows none: every prompt is refused, as in
/// phases 0 to 2, and the log says why.
/// </para>
/// </summary>
public sealed class PromptDialogs : IGitPromptHandler
{
    /// <summary>How long an answer is kept for its scope (3.2).</summary>
    public static readonly TimeSpan KeepAnswersFor = TimeSpan.FromMinutes(2);

    private readonly IPromptDialogPresenter _presenter;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private readonly Dictionary<(string ScopeId, string Text), Kept> _kept = [];

    /// <summary>The app's prompt dialogs, shown through <paramref name="dialogs"/>.</summary>
    public PromptDialogs(DialogHostViewModel dialogs, IAppLog? log = null)
        : this(new PromptDialogsPendingPresenter(dialogs), log)
    {
    }

    /// <summary>Prompt dialogs shown by <paramref name="presenter"/>; <paramref name="clock"/> times the kept answers.</summary>
    public PromptDialogs(IPromptDialogPresenter presenter, IAppLog? log = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        _presenter = presenter;
        Log = log ?? NullAppLog.Instance;
        _clock = clock ?? TimeProvider.System;
    }

    private IAppLog Log { get; }

    public async Task<PromptAnswer> AnswerAsync(GitPrompt prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        // A notice needs no answer: it shows until its scope ends or ssh stops the helper.
        if (prompt.Kind == GitPromptKind.Notice)
        {
            await _presenter.ShowAsync(prompt, cancellationToken).ConfigureAwait(false);
            return PromptAnswer.Refused;
        }

        if (Recall(prompt) is { } kept)
        {
            Log.Debug($"{prompt.RepositoryName}: {prompt.Title}: a {prompt.Kind} prompt was answered with what this operation was told before.");
            return PromptAnswer.Of(kept);
        }

        // What a scope kept goes when the scope ends.
        cancellationToken.Register(() => Forget(prompt.ScopeId));

        PromptDialogAnswer answer;
        try
        {
            answer = await _presenter.ShowAsync(prompt, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PromptAnswer.Refused;
        }

        if (answer.Cancelled)
        {
            return PromptAnswer.Cancelled;
        }

        if (answer.Text is null || cancellationToken.IsCancellationRequested)
        {
            return PromptAnswer.Refused;
        }

        Keep(prompt, answer);
        return PromptAnswer.Of(answer.Text);
    }

    /// <summary>
    /// The app session's prompt broker, answering through <paramref name="handler"/>: the askpass
    /// broker, whose helper is found next to <c>VisualCommit.Git</c>. Called once by
    /// <c>AppSession.Start</c>, which disposes the broker at the end of the session.
    /// </summary>
    public static IGitPromptBroker CreateBroker(IGitPromptHandler handler, IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(log);
        return new AskPassBroker(handler, log);
    }

    private string? Recall(GitPrompt prompt)
    {
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            foreach (var key in _kept.Where(entry => entry.Value.Until <= now).Select(entry => entry.Key).ToList())
            {
                _kept.Remove(key);
            }

            return _kept.TryGetValue((prompt.ScopeId, prompt.Text), out var kept) ? kept.Text : null;
        }
    }

    private void Keep(GitPrompt prompt, PromptDialogAnswer answer)
    {
        var until = _clock.GetUtcNow() + KeepAnswersFor;
        lock (_gate)
        {
            _kept[(prompt.ScopeId, prompt.Text)] = new Kept(answer.Text!, until);

            // The credentials dialog asks for both; git asks for the password next, naming the user.
            if (prompt.Kind == GitPromptKind.Username && prompt.Url is { } url && answer.Password is { } password)
            {
                _kept[(prompt.ScopeId, GitPromptClassifier.PasswordPromptAfter(url, answer.Text!))] = new Kept(password, until);
            }
        }
    }

    private void Forget(string scopeId)
    {
        lock (_gate)
        {
            foreach (var key in _kept.Keys.Where(key => key.ScopeId == scopeId).ToList())
            {
                _kept.Remove(key);
            }
        }
    }

    private sealed record Kept(string Text, DateTimeOffset Until);
}

/// <summary>What a prompt dialog returns.</summary>
/// <param name="Text">The answer: the user name for a <see cref="GitPromptKind.Username"/> prompt, the password, passphrase or text otherwise, <c>yes</c> for a host key or a confirmation; null when there is none.</param>
/// <param name="Password">For a <see cref="GitPromptKind.Username"/> prompt: the password typed with the user name, which answers git's password prompt that follows.</param>
/// <param name="Cancelled">The user pressed Cancel or Esc.</param>
public sealed record PromptDialogAnswer(string? Text, string? Password = null, bool Cancelled = false)
{
    public static PromptDialogAnswer CancelledByUser { get; } = new(null, null, Cancelled: true);

    public static PromptDialogAnswer None { get; } = new(Text: null);
}

/// <summary>
/// Shows the dialog of one prompt (4f): the credentials dialog for <see cref="GitPromptKind.Username"/>
/// (user name and password) and <see cref="GitPromptKind.Password"/> (the user as text), the
/// passphrase, unknown-host and generic prompt dialogs, the "ssh asks" confirmation, and a
/// <see cref="GitPromptKind.Notice"/> in the status bar. Called on any thread; an implementation
/// posts to the UI thread itself. The dialog closes, and the call completes, when
/// <c>cancellationToken</c> is cancelled.
/// </summary>
public interface IPromptDialogPresenter
{
    Task<PromptDialogAnswer> ShowAsync(GitPrompt prompt, CancellationToken cancellationToken);
}

/// <summary>
/// TODO(WP3b): replace with the presenter that shows the prompt dialogs through the dialog host.
/// Until then no dialog exists: every prompt fails with <see cref="NotSupportedException"/>,
/// which the broker logs and answers as refused, so git fails as it did in phases 0 to 2.
/// </summary>
public sealed class PromptDialogsPendingPresenter : IPromptDialogPresenter
{
    public PromptDialogsPendingPresenter(DialogHostViewModel dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        Dialogs = dialogs;
    }

    /// <summary>The dialog host the dialogs of WP3b will be shown in.</summary>
    public DialogHostViewModel Dialogs { get; }

    public Task<PromptDialogAnswer> ShowAsync(GitPrompt prompt, CancellationToken cancellationToken) =>
        throw new NotSupportedException("dialogs: WP3b");
}
