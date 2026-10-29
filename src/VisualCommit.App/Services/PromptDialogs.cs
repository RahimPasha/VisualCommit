using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
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
/// The prompts of a bulk run form a group (<see cref="GitPrompt.Group"/>, docs/phase-6-design.md
/// 4.4, D210): one of them is asked at a time, and what the group was told, a Cancel included,
/// answers the same prompt text in any of the run's repositories until the run ends, so that ten
/// repositories on one host ask once.
/// </para>
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
    private readonly Dictionary<string, GroupState> _groups = new(StringComparer.Ordinal);

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

        // A notice needs no answer: it shows until its scope ends or ssh stops the helper. One of a
        // prompt group is never held back (docs/phase-6-design.md 4.4).
        if (prompt.Kind == GitPromptKind.Notice)
        {
            await _presenter.ShowAsync(prompt, cancellationToken).ConfigureAwait(false);
            return PromptAnswer.Refused;
        }

        if (prompt.Group is { } group && await AnswerInGroupAsync(prompt, group, cancellationToken).ConfigureAwait(false) is { } answered)
        {
            return answered;
        }

        return await AnswerAloneAsync(prompt, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The app session's prompt broker, answering through <paramref name="handler"/>: the askpass
    /// broker, whose helper is found next to <c>VisualCommit.Git</c>. Called once by
    /// <c>AppSession.Start</c>, which disposes the broker at the end of the session.
    /// <paramref name="credentials"/>, <paramref name="sessionEnvironment"/> and
    /// <paramref name="gitVersion"/> go to the broker for the hosting account's bound URLs (D255,
    /// docs/phase-7a-design.md 1.3, 1.7).
    /// </summary>
    public static IGitPromptBroker CreateBroker(
        IGitPromptHandler handler,
        IAppLog log,
        IGitCredentialSource? credentials = null,
        IReadOnlyDictionary<string, string?>? sessionEnvironment = null,
        Func<GitVersion?>? gitVersion = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(log);
        return new AskPassBroker(handler, log, credentials: credentials, sessionEnvironment: sessionEnvironment, gitVersion: gitVersion);
    }

    /// <summary>
    /// A prompt of a bulk run (docs/phase-6-design.md 4.4, D210): it waits for its group's turn,
    /// one prompt of a group at a time; then a text the group was answered before (git's text names
    /// the URL and the user, so the same text is the same credential) answers at once, a Cancel
    /// included; otherwise its dialog shows, and what it was told is kept under the group, the
    /// password typed with a user name under the password prompt's text, until the group ends or
    /// for <see cref="KeepAnswersFor"/>. A dialog closed because its scope ended keeps nothing.
    /// Null when the group ended before the prompt's turn came (it was over already, or ended while
    /// the prompt waited): the prompt is then answered as one without a group, since its git still
    /// waits for it.
    /// </summary>
    private async Task<PromptAnswer?> AnswerInGroupAsync(GitPrompt prompt, PromptGroup group, CancellationToken cancellationToken)
    {
        if (GroupOf(group) is not { } state)
        {
            return null;
        }

        using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, group.Ended))
        {
            try
            {
                await state.Turn.WaitAsync(waiting.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The scope ended (git was stopped, the operation ended) while it waited.
                return PromptAnswer.Refused;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PromptAnswer.Refused;
            }

            if (Recall(state, group, prompt) is { } kept)
            {
                Log.Debug($"{prompt.RepositoryName}: {prompt.Title}: a {prompt.Kind} prompt was answered with what its run was told before.");
                return kept.Cancelled ? PromptAnswer.Cancelled : PromptAnswer.Of(kept.Text!);
            }

            PromptDialogAnswer answer;
            try
            {
                answer = await _presenter.ShowAsync(prompt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return PromptAnswer.Refused;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return PromptAnswer.Refused;
            }

            if (answer.Cancelled)
            {
                KeepInGroup(state, group, prompt, answer);
                return PromptAnswer.Cancelled;
            }

            if (answer.Text is null)
            {
                return PromptAnswer.Refused;
            }

            KeepInGroup(state, group, prompt, answer);
            return PromptAnswer.Of(answer.Text);
        }
        finally
        {
            state.Turn.Release();
        }
    }

    /// <summary>A prompt without a group, or of a group that has ended: as phase 3 answered it (3.2).</summary>
    private async Task<PromptAnswer> AnswerAloneAsync(GitPrompt prompt, CancellationToken cancellationToken)
    {
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
    /// The group's turn and kept answers, made at its first prompt and forgotten when it ends; null
    /// when it has ended.
    /// </summary>
    private GroupState? GroupOf(PromptGroup group)
    {
        GroupState state;
        lock (_gate)
        {
            if (group.IsEnded)
            {
                return null;
            }

            if (_groups.TryGetValue(group.Id, out var known))
            {
                return known;
            }

            state = new GroupState();
            _groups[group.Id] = state;
        }

        // Outside the gate: the callback runs at once when the group has just ended.
        group.Ended.Register(() => ForgetGroup(group.Id, state));
        return state;
    }

    private void ForgetGroup(string groupId, GroupState state)
    {
        lock (_gate)
        {
            if (_groups.TryGetValue(groupId, out var known) && ReferenceEquals(known, state))
            {
                _groups.Remove(groupId);
            }

            state.Kept.Clear();
        }
    }

    /// <summary>
    /// A kept answer of the group for <paramref name="prompt"/>'s account and text (ruling 9,
    /// docs/phase-7a-design.md 4.7): one text under two accounts is two answers. Answers kept before
    /// the group's last <see cref="PromptGroup.ForgetAnswers"/> are dropped, as are expired ones.
    /// </summary>
    private Kept? Recall(GroupState state, PromptGroup group, GitPrompt prompt)
    {
        var now = _clock.GetUtcNow();
        var version = group.AnswersVersion;
        lock (_gate)
        {
            foreach (var key in state.Kept.Where(entry => entry.Value.Until <= now || entry.Value.Version < version).Select(entry => entry.Key).ToList())
            {
                state.Kept.Remove(key);
            }

            return state.Kept.TryGetValue(KeyOf(prompt, prompt.Text), out var kept) ? kept : null;
        }
    }

    private void KeepInGroup(GroupState state, PromptGroup group, GitPrompt prompt, PromptDialogAnswer answer)
    {
        var until = _clock.GetUtcNow() + KeepAnswersFor;
        var version = group.AnswersVersion;
        lock (_gate)
        {
            if (answer.Cancelled)
            {
                state.Kept[KeyOf(prompt, prompt.Text)] = new Kept(null, until, Cancelled: true, version);
                return;
            }

            state.Kept[KeyOf(prompt, prompt.Text)] = new Kept(answer.Text!, until, Version: version);
            if (prompt.Kind == GitPromptKind.Username && prompt.Url is { } url && answer.Password is { } password)
            {
                // Under the account git's password prompt will carry, which the broker gives only
                // when the user typed is the account's (AskPassBroker.IsAccountUser).
                var account = prompt.Account is { } named && AskPassBroker.IsAccountUser(answer.Text, named) ? named : null;
                state.Kept[KeyOf(account, GitPromptClassifier.PasswordPromptAfter(url, answer.Text!))] = new Kept(password, until, Version: version);
            }
        }
    }

    /// <summary>A group's key: the prompt's account (<see cref="GitPrompt.Account"/>, empty for none) and a prompt text.</summary>
    private static (string Account, string Text) KeyOf(GitPrompt prompt, string text) => KeyOf(prompt.Account, text);

    private static (string Account, string Text) KeyOf(AccountKey? account, string text) => (account?.Id ?? string.Empty, text);

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

    /// <summary>An answer kept for <see cref="KeepAnswersFor"/>: a text, or for a prompt group also the user's Cancel (null text).</summary>
    /// <param name="Version">The group's <see cref="PromptGroup.AnswersVersion"/> when it was kept; 0 outside a group.</param>
    private sealed record Kept(string? Text, DateTimeOffset Until, bool Cancelled = false, int Version = 0);

    /// <summary>A prompt group's turn (one prompt of a group at a time) and its kept answers by account and prompt text, under <see cref="_gate"/>.</summary>
    private sealed class GroupState
    {
        public SemaphoreSlim Turn { get; } = new(1, 1);

        public Dictionary<(string Account, string Text), Kept> Kept { get; } = [];
    }
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

        var repositoryName = ShowsRepositoryName?.Invoke() == true ? prompt.RepositoryName : null;
        if (prompt.Kind == GitPromptKind.Confirm)
        {
            var confirmation = new ConfirmationViewModel(ConfirmationFor(prompt) with { RepositoryName = repositoryName });
            bool yes;
            using (cancellationToken.Register(() => OnUiThread(confirmation.Cancel)))
            {
                yes = await Dialogs.ShowAsync(confirmation);
            }

            return cancellationToken.IsCancellationRequested ? PromptDialogAnswer.None
                : yes ? new PromptDialogAnswer(HostKeyViewModel.Yes)
                : PromptDialogAnswer.CancelledByUser;
        }

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
