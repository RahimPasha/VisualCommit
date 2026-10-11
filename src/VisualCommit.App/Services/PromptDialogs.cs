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
/// The dialogs themselves are shown by an <see cref="IPromptDialogPresenter"/>: in the app the
/// <see cref="PromptDialogPresenter"/>, which draws them in the window's dialog layer; in tests a
/// script.
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

    /// <summary>
    /// The app's prompt dialogs, shown in <paramref name="dialogs"/> by a <see cref="PromptDialogPresenter"/>
    /// (<see cref="Presenter"/>), with notices in <see cref="DialogHostViewModel.Notices"/>. Create it on the UI thread.
    /// </summary>
    public PromptDialogs(DialogHostViewModel dialogs, IAppLog? log = null)
        : this(new PromptDialogPresenter(dialogs, dialogs?.Notices), log)
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

    /// <summary>What shows the dialogs: a <see cref="PromptDialogPresenter"/> in the app.</summary>
    public IPromptDialogPresenter Presenter => _presenter;

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
/// Shows the prompt dialogs in the window's dialog layer (4f, D104), on top of any dialog already
/// open: "Sign in to &lt;host&gt;" for git's user name and password prompts, "Key passphrase",
/// "Unknown host", "Git asks" for any other prompt, and for ssh's <c>confirm</c> prompt the
/// confirmation titled "ssh asks" with "Cancel" and "Yes" (D115). A notice gets no dialog: it is
/// put in <see cref="Notices"/>, which the status bar shows, until its scope ends.
/// <para>
/// Called on any thread; it moves to the UI thread through the synchronization context it was
/// created on (the app creates it on the UI thread; a test without one runs it where it is
/// called). When the prompt's token is cancelled (its scope ended, or git stopped the helper)
/// the dialog closes and the answer is <see cref="PromptDialogAnswer.None"/>, never a Cancel of
/// the user.
/// </para>
/// </summary>
public sealed class PromptDialogPresenter : IPromptDialogPresenter
{
    /// <summary>The title of ssh's yes-or-no question (D115).</summary>
    public const string ConfirmTitle = "ssh asks";

    /// <summary>The label of the button that says yes to it.</summary>
    public const string ConfirmYes = "Yes";

    private readonly SynchronizationContext? _ui;

    /// <param name="dialogs">The dialog layer the dialogs are shown in.</param>
    /// <param name="notices">Where notices go; a list of its own when null.</param>
    public PromptDialogPresenter(DialogHostViewModel dialogs, PromptNotices? notices = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        Dialogs = dialogs;
        Notices = notices ?? new PromptNotices();
        _ui = SynchronizationContext.Current;
    }

    /// <summary>The dialog layer the dialogs are shown in.</summary>
    public DialogHostViewModel Dialogs { get; }

    /// <summary>ssh's notices that are shown now (3.2): the status bar shows the newest.</summary>
    public PromptNotices Notices { get; }

    /// <summary>
    /// Whether a dialog names its repository under the title ("In &lt;repo&gt;"): when the window
    /// has more than one tab with a repository (4f). Read on the UI thread when a dialog opens;
    /// null names none.
    /// </summary>
    public Func<bool>? ShowsRepositoryName { get; set; }

    public Task<PromptDialogAnswer> ShowAsync(GitPrompt prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (_ui is null || SynchronizationContext.Current == _ui)
        {
            return ShowHereAsync(prompt, cancellationToken);
        }

        var shown = new TaskCompletionSource<PromptDialogAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ui.Post(
            _ => ShowHereAsync(prompt, cancellationToken).ContinueWith(task => shown.TrySetFromTask(task), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default),
            null);
        return shown.Task;
    }

    /// <summary>The dialog of <paramref name="prompt"/>; null for a confirmation or a notice, which have none of their own (<see cref="ShowAsync"/>).</summary>
    public static PromptDialogViewModel? CreateDialog(GitPrompt prompt, string? repositoryName = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return prompt.Kind switch
        {
            GitPromptKind.Username or GitPromptKind.Password => new CredentialsViewModel(prompt, repositoryName),
            GitPromptKind.Passphrase => new PassphraseViewModel(prompt, repositoryName),
            GitPromptKind.HostKey => new HostKeyViewModel(prompt, repositoryName),
            GitPromptKind.Other => new PromptViewModel(prompt, repositoryName),
            _ => null,
        };
    }

    /// <summary>What ssh's <c>confirm</c> prompt asks: the confirmation titled "ssh asks", ssh's text as its question, no detail, "Yes" (D115).</summary>
    public static ConfirmationRequest ConfirmationFor(GitPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return new ConfirmationRequest(ConfirmTitle, PromptDialogViewModel.Shown(prompt.Text), string.Empty, ConfirmYes, IsDestructive: false);
    }

    /// <summary>On the UI thread (or without one): shows the dialog and waits for its answer or the scope's end.</summary>
    private async Task<PromptDialogAnswer> ShowHereAsync(GitPrompt prompt, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return PromptDialogAnswer.None;
        }

        if (prompt.Kind == GitPromptKind.Notice)
        {
            using (Notices.Show(prompt.Text))
            {
                await WhenCancelledAsync(cancellationToken);
            }

            return PromptDialogAnswer.None;
        }

        if (prompt.Kind == GitPromptKind.Confirm)
        {
            var confirmation = new ConfirmationViewModel(ConfirmationFor(prompt));
            bool yes;
            using (cancellationToken.Register(() => OnUiThread(confirmation.Cancel)))
            {
                yes = await Dialogs.ShowAsync(confirmation);
            }

            return cancellationToken.IsCancellationRequested ? PromptDialogAnswer.None
                : yes ? new PromptDialogAnswer(HostKeyViewModel.Yes)
                : PromptDialogAnswer.CancelledByUser;
        }

        var repositoryName = ShowsRepositoryName?.Invoke() == true ? prompt.RepositoryName : null;
        var dialog = CreateDialog(prompt, repositoryName) ?? new PromptViewModel(prompt, repositoryName);
        PromptDialogAnswer answer;
        using (cancellationToken.Register(() => OnUiThread(dialog.CloseUnanswered)))
        {
            answer = await Dialogs.ShowAsync(dialog);
        }

        return cancellationToken.IsCancellationRequested ? PromptDialogAnswer.None : answer;
    }

    private void OnUiThread(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui)
        {
            action();
        }
        else
        {
            _ui.Post(_ => action(), null);
        }
    }

    private static async Task WhenCancelledAsync(CancellationToken cancellationToken)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(() => cancelled.TrySetResult()))
        {
            await cancelled.Task;
        }
    }
}
