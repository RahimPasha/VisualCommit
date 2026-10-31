using System.Text.RegularExpressions;

namespace VisualCommit.LiveHostingTests;

/// <summary>
/// The live command's two variables (docs/phase-7a-design.md 14.3; D260): the owner's fine-grained
/// token and the sandbox repository, Windows user variables set once on the development machine.
/// They are read once, before anything else in the process (<see cref="ProcessIsolation"/>), and
/// removed from the process's environment at once, so that no git or app process inherits them;
/// every test then asks <see cref="Require"/> for them first, and fails without them (none is
/// skipped: a skip could pass for green at a gate).
/// </summary>
internal static class LiveSettings
{
    public const string TokenVariable = "VISUALCOMMIT_LIVE_GITHUB_TOKEN";

    public const string RepositoryVariable = "VISUALCOMMIT_LIVE_GITHUB_REPO";

    /// <summary>The prefixes of GitHub's tokens: a value of the repository's variable that starts with one is never shown.</summary>
    private static readonly string[] TokenPrefixes = ["github_pat_", "ghp_", "gho_", "ghu_", "ghs_", "ghr_"];

    /// <summary>
    /// One segment of owner/name: letters, digits, <c>.</c>, <c>-</c> and <c>_</c>, not dots alone:
    /// <c>.</c> and <c>..</c> would make the API's paths name other ones (<c>repos/x/..</c>), and the
    /// test's calls must never reach another repository than the sandbox.
    /// </summary>
    private static readonly Regex Segment = new(@"^(?!\.+$)[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant);

    private static readonly Lock Gate = new();
    private static bool captured;
    private static string? capturedToken;
    private static string? capturedRepository;

    /// <summary>
    /// The token read by <see cref="CaptureAndRemove"/>, or null without one: what
    /// <see cref="LiveRun"/> searches its notes and run.txt for.
    /// </summary>
    internal static string? CapturedToken
    {
        get
        {
            lock (Gate)
            {
                return capturedToken;
            }
        }
    }

    /// <summary>
    /// Reads both variables (trimmed; an empty one counts as missing) into this class, once, then
    /// removes them from the process's environment.
    /// </summary>
    internal static void CaptureAndRemove()
    {
        lock (Gate)
        {
            if (captured)
            {
                return;
            }

            captured = true;
            capturedToken = Read(TokenVariable);
            capturedRepository = Read(RepositoryVariable);
            Environment.SetEnvironmentVariable(TokenVariable, null);
            Environment.SetEnvironmentVariable(RepositoryVariable, null);
        }
    }

    /// <summary>The sandbox and the token, or the message that says what is missing.</summary>
    /// <exception cref="InvalidOperationException">A variable is missing, or the repository is not owner/name.</exception>
    public static LiveSandbox Require()
    {
        string? token;
        string? repository;
        lock (Gate)
        {
            token = capturedToken;
            repository = capturedRepository;
        }

        return Resolve(token, repository);
    }

    /// <summary>
    /// <see cref="Require"/>'s rule on the two values as read (14.3): both present, the repository two
    /// non-empty segments of letters, digits, <c>.</c>, <c>-</c> and <c>_</c>, neither of dots alone
    /// (<see cref="Segment"/>). The message names the
    /// repository's value when it is not owner/name, unless the value holds the token or starts like
    /// one of GitHub's tokens (the two variables swapped): then it says so without the value.
    /// </summary>
    /// <exception cref="InvalidOperationException">What is missing or wrong, in 14.3's message.</exception>
    internal static LiveSandbox Resolve(string? token, string? repository)
    {
        token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        repository = string.IsNullOrWhiteSpace(repository) ? null : repository.Trim();
        var details = new List<string>();
        if (token is null && repository is null)
        {
            details.Add($"Missing: {TokenVariable} and {RepositoryVariable}.");
        }
        else if (token is null)
        {
            details.Add($"Missing: {TokenVariable}.");
        }
        else if (repository is null)
        {
            details.Add($"Missing: {RepositoryVariable}.");
        }

        string[]? parts = null;
        if (repository is not null)
        {
            parts = repository.Split('/');
            if (parts.Length != 2 || !Segment.IsMatch(parts[0]) || !Segment.IsMatch(parts[1]))
            {
                details.Add(LooksLikeToken(repository, token)
                    ? $"{RepositoryVariable} holds what looks like a token, not owner/name."
                    : $"{RepositoryVariable} is \"{repository}\", not owner/name.");
                parts = null;
            }
        }

        if (details.Count > 0 || token is null || parts is null)
        {
            throw new InvalidOperationException(
                $"The live command needs two Windows user environment variables, set once on this machine (D260): {TokenVariable}, a fine-grained token for the sandbox repository only, and {RepositoryVariable}, that repository as owner/name. "
                + string.Join(' ', details)
                + " Set them in Windows' Environment Variables dialog, then start a new terminal.");
        }

        return new LiveSandbox(parts[0], parts[1], token);
    }

    /// <summary>The token's kind by its prefix (14.5), never more of it.</summary>
    internal static string KindOf(string token) =>
        token.StartsWith("github_pat_", StringComparison.Ordinal) ? "fine-grained (github_pat_)"
        : token.StartsWith("ghp_", StringComparison.Ordinal) ? "classic (ghp_)"
        : "of another kind (neither github_pat_ nor ghp_)";

    private static string? Read(string variable) =>
        Environment.GetEnvironmentVariable(variable)?.Trim() is { Length: > 0 } value ? value : null;

    private static bool LooksLikeToken(string value, string? token) =>
        (token is not null && value.Contains(token, StringComparison.Ordinal))
        || TokenPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The sandbox repository and the token for it (14.3). <see cref="ToString"/> is the repository's
/// full name, never the token; the token is read only where it is sent (the sign-in dialog, the
/// test's own API calls, Check_3's <c>GitHubProvider</c>) or searched for.
/// </summary>
internal sealed class LiveSandbox(string owner, string name, string token)
{
    public string Owner { get; } = owner;

    public string Name { get; } = name;

    public string Token { get; } = token;

    /// <summary><c>owner/name</c> as the variable gives it.</summary>
    public string FullName => $"{Owner}/{Name}";

    public override string ToString() => FullName;
}
