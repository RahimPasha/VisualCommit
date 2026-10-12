using System.Text.RegularExpressions;
using VisualCommit.Core.Operations;

namespace VisualCommit.Git.Prompts;

/// <summary>What a prompt asks for, and the parts of it that the dialog shows.</summary>
/// <param name="Kind">The kind of prompt.</param>
/// <param name="Url">For <see cref="GitPromptKind.Username"/> and <see cref="GitPromptKind.Password"/>: the URL as the prompt names it (<c>https://alice@example.com</c>).</param>
/// <param name="User">For <see cref="GitPromptKind.Password"/>: the user name in that URL, when it has one.</param>
/// <param name="KeyPath">For <see cref="GitPromptKind.Passphrase"/>: the key's path, when ssh names it.</param>
public sealed record GitPromptClassification(GitPromptKind Kind, string? Url = null, string? User = null, string? KeyPath = null);

/// <summary>
/// Tells from a prompt's text what git or ssh asks for (docs/phase-3-design.md 3.2). Pure: the
/// rules are checked against texts recorded from git 2.30 to 2.55 and OpenSSH 8.x and 9.x
/// (<c>PromptClassifierTests</c>). The texts are English because every git call runs with
/// <c>LC_ALL=en_US.UTF-8</c> (D30); OpenSSH's prompts are not translated at all.
/// </summary>
public static partial class GitPromptClassifier
{
    /// <summary>OpenSSH's <c>SSH_ASKPASS_PROMPT</c> for a yes-or-no question (ssh-agent's confirmations).</summary>
    public const string SshConfirm = "confirm";

    /// <summary>OpenSSH's <c>SSH_ASKPASS_PROMPT</c> for a notice that needs no answer (touch a security key).</summary>
    public const string SshNotice = "none";

    /// <summary>
    /// Classifies <paramref name="prompt"/>, the askpass program's one argument, with
    /// <paramref name="sshPrompt"/>, the value of <c>SSH_ASKPASS_PROMPT</c> (null when unset).
    /// </summary>
    public static GitPromptClassification Classify(string prompt, string? sshPrompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        // A notice never gets a dialog, whatever its text says.
        if (sshPrompt == SshNotice)
        {
            return new GitPromptClassification(GitPromptKind.Notice);
        }

        if (UsernamePattern().Match(prompt) is { Success: true } username)
        {
            return new GitPromptClassification(GitPromptKind.Username, Url: username.Groups["url"].Value);
        }

        if (PasswordPattern().Match(prompt) is { Success: true } password)
        {
            var url = password.Groups["url"].Value;
            return new GitPromptClassification(GitPromptKind.Password, Url: url, User: UserOf(url));
        }

        if (PassphrasePattern().Match(prompt) is { Success: true } passphrase)
        {
            var path = passphrase.Groups["path"];
            return new GitPromptClassification(GitPromptKind.Passphrase, KeyPath: path.Success && path.Length > 0 ? path.Value : null);
        }

        if (prompt.Contains("Are you sure you want to continue connecting", StringComparison.Ordinal))
        {
            return new GitPromptClassification(GitPromptKind.HostKey);
        }

        if (sshPrompt == SshConfirm)
        {
            return new GitPromptClassification(GitPromptKind.Confirm);
        }

        return new GitPromptClassification(GitPromptKind.Other);
    }

    /// <summary>
    /// Whether the dialog hides what is typed: always for a password or a passphrase; for any
    /// other prompt that asks for text, when it mentions a password, passphrase, PIN or token
    /// (ignoring case: <c>git@host's password: </c>, <c>Enter PIN for key: </c>). A user name, a
    /// host-key question, a confirmation and a notice are not masked.
    /// </summary>
    public static bool IsMasked(GitPromptKind kind, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return kind switch
        {
            GitPromptKind.Password or GitPromptKind.Passphrase => true,
            GitPromptKind.Other => SecretWordPattern().IsMatch(text),
            _ => false,
        };
    }

    /// <summary>
    /// The host of <paramref name="url"/> as the credentials dialog names it ("Sign in to
    /// &lt;host&gt;", 4f): the authority without its user information, with the port when the
    /// URL has one (<c>https://alice@127.0.0.1:47391/x.git</c> gives <c>127.0.0.1:47391</c>).
    /// A text without <c>://</c> is returned as it is.
    /// </summary>
    public static string HostOf(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var authority = AuthorityOf(url);
        if (authority is null)
        {
            return url;
        }

        var at = authority.LastIndexOf('@');
        return at >= 0 ? authority[(at + 1)..] : authority;
    }

    /// <summary>The user name in <paramref name="url"/>'s user information (before a <c>:</c>), or null when it has none.</summary>
    public static string? UserOf(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var authority = AuthorityOf(url);
        var at = authority?.LastIndexOf('@') ?? -1;
        if (at <= 0)
        {
            return null;
        }

        var userInfo = authority![..at];
        var colon = userInfo.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0 ? userInfo[..colon] : userInfo;
    }

    /// <summary>
    /// The text of the password prompt that git asks after a user name prompt for
    /// <paramref name="url"/> has been answered with <paramref name="user"/>:
    /// <c>Password for 'https://alice@example.com': </c>. Git puts the user name into the URL as
    /// it was typed (<c>credential_describe</c>).
    /// </summary>
    public static string PasswordPromptAfter(string url, string user)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(user);
        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        var withUser = schemeEnd < 0 ? url : string.Concat(url.AsSpan(0, schemeEnd + 3), user, "@", url.AsSpan(schemeEnd + 3));
        return $"Password for '{withUser}': ";
    }

    private static string? AuthorityOf(string url)
    {
        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return null;
        }

        var start = schemeEnd + 3;
        var end = url.IndexOfAny(['/', '?', '#'], start);
        return end < 0 ? url[start..] : url[start..end];
    }

    /// <summary>git's <c>credential_ask_one</c>: <c>Username for 'https://example.com': </c>.</summary>
    [GeneratedRegex(@"^Username for '(?<url>.+)': ?$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();

    /// <summary>git's <c>credential_ask_one</c>: <c>Password for 'https://alice@example.com': </c>.</summary>
    [GeneratedRegex(@"^Password for '(?<url>.+)': ?$", RegexOptions.CultureInvariant)]
    private static partial Regex PasswordPattern();

    /// <summary>
    /// ssh's <c>Enter passphrase for key '/home/u/.ssh/id_ed25519': </c> (a long path is cut by
    /// ssh), ssh-keygen's <c>Enter passphrase: </c> and newer ssh-keygen's
    /// <c>Enter passphrase for "/path/key": </c>.
    /// </summary>
    [GeneratedRegex("""^Enter passphrase(?: for (?:key )?(?<q>['"]?)(?<path>.+?)\k<q>)?(?: \([^)]*\))?: ?$""", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex PassphrasePattern();

    [GeneratedRegex("password|passphrase|pin|token", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretWordPattern();
}
