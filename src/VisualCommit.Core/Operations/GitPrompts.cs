namespace VisualCommit.Core.Operations;

/// <summary>What git or ssh asks for (phase 3 design 3.2).</summary>
public enum GitPromptKind
{
    /// <summary><c>Username for 'https://host': </c></summary>
    Username,

    /// <summary><c>Password for 'https://user@host': </c></summary>
    Password,

    /// <summary>An ssh key's passphrase.</summary>
    Passphrase,

    /// <summary>ssh's question about an unknown host key.</summary>
    HostKey,

    /// <summary>A yes-or-no question of ssh (<c>SSH_ASKPASS_PROMPT=confirm</c>).</summary>
    Confirm,

    /// <summary>A notice of ssh that needs no answer (<c>SSH_ASKPASS_PROMPT=none</c>): the status bar shows it.</summary>
    Notice,

    /// <summary>Anything else: a dialog with the text and one box.</summary>
    Other,
}

/// <summary>One prompt of git or ssh, classified, on its way to a dialog (3.1, 3.2).</summary>
/// <param name="Kind">What it asks for.</param>
/// <param name="Text">The prompt as git or ssh wrote it.</param>
/// <param name="ScopeId">The prompt scope (the operation) it belongs to.</param>
/// <param name="Title">The operation's title, for the dialog's title ("Fetch from origin — graph").</param>
/// <param name="RepositoryName">The repository's name.</param>
/// <param name="Url">For <see cref="GitPromptKind.Username"/> and <see cref="GitPromptKind.Password"/>: the URL in the prompt; otherwise null.</param>
/// <param name="User">For <see cref="GitPromptKind.Password"/>: the user name in the URL, when it has one.</param>
/// <param name="KeyPath">For <see cref="GitPromptKind.Passphrase"/>: the key's path, when ssh names it.</param>
public sealed record GitPrompt(
    GitPromptKind Kind,
    string Text,
    string ScopeId,
    string Title,
    string RepositoryName,
    string? Url = null,
    string? User = null,
    string? KeyPath = null);

/// <summary>The user's answer to a prompt.</summary>
/// <param name="Text">What git or ssh gets; null refuses the prompt (git then fails).</param>
/// <param name="UserCancelled">The user pressed Cancel or Esc: the queue then reports the operation as cancelled, not failed (2.16).</param>
public sealed record PromptAnswer(string? Text, bool UserCancelled)
{
    public static PromptAnswer Refused { get; } = new(null, false);

    public static PromptAnswer Cancelled { get; } = new(null, true);

    public static PromptAnswer Of(string text) => new(text, false);
}

/// <summary>Answers prompts, through dialogs in the window (App's <c>PromptDialogs</c>, 3.2).</summary>
public interface IGitPromptHandler
{
    /// <summary>
    /// Asks the user. Completes with <see cref="PromptAnswer.Cancelled"/> when they cancel, and
    /// with <see cref="PromptAnswer.Refused"/> when <paramref name="cancellationToken"/> is
    /// cancelled because the scope ended (its dialog closes then).
    /// </summary>
    Task<PromptAnswer> AnswerAsync(GitPrompt prompt, CancellationToken cancellationToken);
}

/// <summary>
/// The channel from git's and ssh's prompts to the app (3.1, D82): the server end of the askpass
/// helper, one per app session. An operation opens a scope; the scope's variables go into the
/// environment of its git calls, and a prompt that comes through them is answered within it.
/// </summary>
public interface IGitPromptBroker
{
    /// <summary>
    /// Opens a prompt scope for one operation (or a clone). <paramref name="mode"/>
    /// <see cref="PromptMode.Ask"/> gives the variables that lead prompts to the app,
    /// <see cref="PromptMode.Deny"/> those that refuse every prompt at once,
    /// <see cref="PromptMode.None"/> none. Disposing the scope closes its open prompt dialog.
    /// </summary>
    /// <param name="title">The operation's title, for the dialog.</param>
    /// <param name="repositoryName">The repository's name, for the dialog.</param>
    /// <param name="mode">How prompts are answered.</param>
    /// <param name="operationId">The operation's id, which becomes the scope's <see cref="PromptScope.Id"/>; null lets the broker make one (a clone).</param>
    PromptScope Begin(string title, string repositoryName, PromptMode mode, string? operationId = null);
}

/// <summary>One open prompt scope (3.1): its id, the variables for git's environment and whether the user cancelled a prompt.</summary>
public abstract class PromptScope : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string?> NoVariables = new Dictionary<string, string?>();
    private int _disposed;

    protected PromptScope(string id, IReadOnlyDictionary<string, string?>? environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        Environment = environment ?? NoVariables;
    }

    /// <summary>The scope's id, which the helper sends with each prompt (<c>VISUALCOMMIT_OPERATION_ID</c>).</summary>
    public string Id { get; }

    /// <summary>The variables to add to the environment of every git call of the operation.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; }

    /// <summary>The user cancelled one of this scope's prompts (Cancel or Esc).</summary>
    public bool UserCancelled { get; protected set; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            OnDisposed();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Closes the scope: a prompt still open is answered with null and its dialog closes.</summary>
    protected virtual void OnDisposed()
    {
    }
}

/// <summary>
/// A broker that leads no prompt to the app: every scope has no variables, so git's prompts
/// fail at once as in phases 0 to 2 (D30). The app uses it until the askpass broker (WP3) exists,
/// and tests that need no prompts use it.
/// </summary>
public sealed class NullGitPromptBroker : IGitPromptBroker
{
    private long _next;

    public static NullGitPromptBroker Instance { get; } = new();

    public PromptScope Begin(string title, string repositoryName, PromptMode mode, string? operationId = null) =>
        new EmptyScope(operationId ?? $"prompt-{Interlocked.Increment(ref _next)}");

    private sealed class EmptyScope(string id) : PromptScope(id, null);
}
