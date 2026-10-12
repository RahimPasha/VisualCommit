using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// A dialog that answers one prompt of git or ssh (docs/phase-3-design.md 3.2, 4f): the
/// credentials, passphrase, unknown-host and generic prompt dialogs. Its answer is a
/// <see cref="PromptDialogAnswer"/>: what was typed when the user confirms,
/// <see cref="PromptDialogAnswer.CancelledByUser"/> for Cancel and Esc, and
/// <see cref="PromptDialogAnswer.None"/> when the prompt's scope ended first
/// (<see cref="CloseUnanswered"/>). Nothing typed is logged; what is kept for the operation is
/// <c>PromptDialogs</c>' to keep (3.2), and the dialog itself is dropped when it closes.
/// </summary>
public abstract partial class PromptDialogViewModel : DialogViewModel<PromptDialogAnswer>
{
    /// <summary>What a masked box shows for each character typed.</summary>
    public const char MaskCharacter = '•';

    /// <param name="prompt">The prompt it answers.</param>
    /// <param name="repositoryName">
    /// The repository to name under the title ("In &lt;repo&gt;", <c>DialogRepository</c>), when the
    /// window has more than one tab with a repository (4f); null otherwise. It is the dialog's
    /// <see cref="DialogViewModel.RepositoryName"/>, as for every other dialog.
    /// </param>
    protected PromptDialogViewModel(GitPrompt prompt, string? repositoryName)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        Prompt = prompt;
        RepositoryName = repositoryName;
    }

    /// <summary>The prompt as the broker classified it.</summary>
    public GitPrompt Prompt { get; }

    /// <summary>The label of the button that answers (<c>ConfirmButton</c>).</summary>
    public abstract string ConfirmText { get; }

    protected override PromptDialogAnswer CancelledResult => PromptDialogAnswer.CancelledByUser;

    /// <summary>Closes the dialog without an answer and without a Cancel of the user: the prompt's scope ended (git stopped, the operation ended).</summary>
    public void CloseUnanswered() => Complete(PromptDialogAnswer.None);

    /// <summary>Whether the answer can be given now (the credentials dialog needs a user name).</summary>
    protected virtual bool CanConfirm() => true;

    /// <summary>The answer the confirm button gives.</summary>
    protected abstract PromptDialogAnswer CreateAnswer();

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (CanConfirm())
        {
            Complete(CreateAnswer());
        }
    }

    /// <summary>Tells the confirm button that <see cref="CanConfirm"/> may have changed.</summary>
    protected void RefreshCanConfirm() => ConfirmCommand.NotifyCanExecuteChanged();

    /// <summary>ssh's and git's prompts end with ": " or "? ": shown without the trailing blanks of each line.</summary>
    internal static string Shown(string text) =>
        string.Join('\n', text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd().Split('\n').Select(line => line.TrimEnd()));
}

/// <summary>
/// "Sign in to &lt;host&gt;" (<c>CredentialsDialog</c>, 4f): git's <c>Username for '&lt;url&gt;': </c>
/// with a user name and a password box, whose password answers git's password prompt that follows
/// (3.2); or its <c>Password for '&lt;scheme&gt;://&lt;user&gt;@&lt;host&gt;': </c> alone, with the
/// user shown as text and only the password to type.
/// </summary>
public sealed partial class CredentialsViewModel : PromptDialogViewModel
{
    public CredentialsViewModel(GitPrompt prompt, string? repositoryName = null)
        : base(prompt, repositoryName)
    {
        var url = prompt.Url ?? string.Empty;
        IsUserEditable = prompt.Kind != GitPromptKind.Password;
        Host = GitPromptClassifier.HostOf(url);
        Url = WithoutUser(url);
        if (!IsUserEditable)
        {
            UserName = prompt.User ?? GitPromptClassifier.UserOf(url) ?? string.Empty;
        }
    }

    public override string Title => $"Sign in to {Host}";

    /// <summary>The host as the title names it, with its port (<c>127.0.0.1:47391</c>).</summary>
    public string Host { get; }

    /// <summary>The URL of git's prompt, without a user name.</summary>
    public string Url { get; }

    /// <summary><c>CredentialsText</c>.</summary>
    public string Text => $"Git needs a user name and a password or token for {Url}.";

    /// <summary>The user name is typed (a <see cref="GitPromptKind.Username"/> prompt); otherwise it is git's and shown as text.</summary>
    public bool IsUserEditable { get; }

    public bool IsUserShown => !IsUserEditable;

    /// <summary><c>CredentialUsername</c>.</summary>
    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    /// <summary><c>CredentialPassword</c>, masked.</summary>
    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    public override string ConfirmText => "Sign in";

    protected override bool CanConfirm() => !IsUserEditable || !string.IsNullOrWhiteSpace(UserName);

    protected override PromptDialogAnswer CreateAnswer() =>
        IsUserEditable ? new PromptDialogAnswer(UserName, Password) : new PromptDialogAnswer(Password);

    partial void OnUserNameChanged(string value) => RefreshCanConfirm();

    /// <summary><paramref name="url"/> without the user information between "://" and "@" (D103: nothing of a credential on screen).</summary>
    private static string WithoutUser(string url)
    {
        var start = url.IndexOf("://", StringComparison.Ordinal);
        if (start < 0)
        {
            return url;
        }

        start += 3;
        var end = url.IndexOfAny(['/', '?', '#'], start);
        var authority = end < 0 ? url[start..] : url[start..end];
        var at = authority.LastIndexOf('@');
        return at < 0 ? url : string.Concat(url.AsSpan(0, start), authority.AsSpan(at + 1), end < 0 ? string.Empty : url[end..]);
    }
}

/// <summary>"Key passphrase" (<c>PassphraseDialog</c>, 4f): ssh's passphrase prompt, with a masked box.</summary>
public sealed partial class PassphraseViewModel : PromptDialogViewModel
{
    public PassphraseViewModel(GitPrompt prompt, string? repositoryName = null)
        : base(prompt, repositoryName)
    {
        Text = prompt.KeyPath is { Length: > 0 } path ? $"Enter the passphrase for {path}." : Shown(prompt.Text);
    }

    public override string Title => "Key passphrase";

    /// <summary>"Enter the passphrase for &lt;key path&gt;.", or ssh's own text when it names no key.</summary>
    public string Text { get; }

    /// <summary><c>PassphraseBox</c>, masked.</summary>
    [ObservableProperty]
    public partial string Passphrase { get; set; } = string.Empty;

    public override string ConfirmText => "Unlock";

    protected override PromptDialogAnswer CreateAnswer() => new(Passphrase);
}

/// <summary>"Unknown host" (<c>HostKeyDialog</c>, 4f): ssh's question about a host key it does not know, answered <c>yes</c>.</summary>
public sealed partial class HostKeyViewModel : PromptDialogViewModel
{
    /// <summary>What ssh reads as trusting the key (3.2).</summary>
    public const string Yes = "yes";

    public HostKeyViewModel(GitPrompt prompt, string? repositoryName = null)
        : base(prompt, repositoryName)
    {
        Text = Shown(prompt.Text);
    }

    public override string Title => "Unknown host";

    /// <summary><c>HostKeyText</c>: ssh's lines (host, key type, fingerprint, question), in the code font.</summary>
    public string Text { get; }

    public string Question => "Trust this host and connect?";

    public override string ConfirmText => "Trust and connect";

    protected override PromptDialogAnswer CreateAnswer() => new(Yes);
}

/// <summary>
/// "Git asks" (<c>PromptDialog</c>, 4f): any other prompt, with one box, masked when the text asks
/// for a secret (<see cref="GitPromptClassifier.IsMasked"/>).
/// </summary>
public sealed partial class PromptViewModel : PromptDialogViewModel
{
    public PromptViewModel(GitPrompt prompt, string? repositoryName = null)
        : base(prompt, repositoryName)
    {
        Text = Shown(prompt.Text);
        IsMasked = GitPromptClassifier.IsMasked(prompt.Kind, prompt.Text);
    }

    public override string Title => "Git asks";

    /// <summary><c>PromptText</c>.</summary>
    public string Text { get; }

    /// <summary>The box hides what is typed.</summary>
    public bool IsMasked { get; }

    /// <summary>The box's mask character: <see cref="MaskCharacter"/> when masked, none otherwise.</summary>
    public char PasswordChar => IsMasked ? MaskCharacter : '\0';

    /// <summary><c>PromptBox</c>.</summary>
    [ObservableProperty]
    public partial string Answer { get; set; } = string.Empty;

    public override string ConfirmText => "OK";

    protected override PromptDialogAnswer CreateAnswer() => new(Answer);
}

/// <summary>
/// The texts ssh shows without asking (<see cref="GitPromptKind.Notice"/>, 3.2: "Confirm user
/// presence for key …"): no dialog; the status bar shows the newest one until its scope ends.
/// Changed on the UI thread only.
/// </summary>
public sealed partial class PromptNotices : ObservableObject
{
    private readonly List<(object Key, string Text)> _shown = [];

    /// <summary>The newest notice still shown, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    public partial string? Text { get; private set; }

    public bool IsShown => Text is not null;

    /// <summary>Shows <paramref name="text"/> until the returned object is disposed.</summary>
    public IDisposable Show(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var key = new object();
        _shown.Add((key, text.Trim()));
        Text = _shown[^1].Text;
        return new Hide(this, key);
    }

    private void Remove(object key)
    {
        _shown.RemoveAll(entry => entry.Key == key);
        Text = _shown.Count > 0 ? _shown[^1].Text : null;
    }

    private sealed class Hide(PromptNotices owner, object key) : IDisposable
    {
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
            {
                owner.Remove(key);
            }
        }
    }
}
