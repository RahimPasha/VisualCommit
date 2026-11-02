using System.Text.RegularExpressions;
using VisualCommit.Core.Hosting;

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

    /// <summary>Azure DevOps' token for the sandbox's organization alone (docs/phase-7b-design.md 1.8, 15.2).</summary>
    public const string AzureDevOpsTokenVariable = "VISUALCOMMIT_LIVE_AZURE_DEVOPS_TOKEN";

    /// <summary>Azure DevOps' sandbox, <c>&lt;organization&gt;/&lt;project&gt;/&lt;repository&gt;</c> (15.2).</summary>
    public const string AzureDevOpsRepositoryVariable = "VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO";

    /// <summary>GitLab's fine-grained token (15.2).</summary>
    public const string GitLabTokenVariable = "VISUALCOMMIT_LIVE_GITLAB_TOKEN";

    /// <summary>GitLab's sandbox, <c>&lt;namespace path&gt;/&lt;project&gt;</c> (15.2).</summary>
    public const string GitLabRepositoryVariable = "VISUALCOMMIT_LIVE_GITLAB_REPO";

    /// <summary>An Azure DevOps organization's name (g3 G3.4): what the guard and the screen take as <c>{o}</c>.</summary>
    internal static readonly Regex Organization = new("^[A-Za-z0-9-]+$", RegexOptions.CultureInvariant);

    /// <summary>The four variables 1.8 adds, read and removed beside 7a's two.</summary>
    internal static readonly string[] NewVariables = [AzureDevOpsTokenVariable, AzureDevOpsRepositoryVariable, GitLabTokenVariable, GitLabRepositoryVariable];

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
    private static readonly Dictionary<string, string?> CapturedOthers = new(StringComparer.Ordinal);

    /// <summary>The captured token of <paramref name="provider"/>, or null: what that provider's <see cref="LiveRun"/> searches for.</summary>
    internal static string? CapturedTokenOf(HostingProvider provider)
    {
        if (provider == HostingProvider.GitHub)
        {
            return CapturedToken;
        }

        lock (Gate)
        {
            return CapturedOthers.GetValueOrDefault(TokenVariableOf(provider));
        }
    }

    /// <summary>
    /// The organization's name as <see cref="AzureDevOpsRepositoryVariable"/> gives it (its first
    /// segment, when that is one), or null: what Azure DevOps' screen hides from its first note on,
    /// even when the variable as a whole is not in 15.2's form.
    /// </summary>
    internal static string? CapturedOrganization
    {
        get
        {
            lock (Gate)
            {
                return CapturedOthers.GetValueOrDefault(AzureDevOpsRepositoryVariable)?.Split('/')[0] is { } first && Organization.IsMatch(first) ? first : null;
            }
        }
    }

    /// <summary>The token variable of <paramref name="provider"/>.</summary>
    internal static string TokenVariableOf(HostingProvider provider) => provider switch
    {
        HostingProvider.AzureDevOps => AzureDevOpsTokenVariable,
        HostingProvider.GitLab => GitLabTokenVariable,
        _ => TokenVariable,
    };

    /// <summary>The repository variable of <paramref name="provider"/>.</summary>
    internal static string RepositoryVariableOf(HostingProvider provider) => provider switch
    {
        HostingProvider.AzureDevOps => AzureDevOpsRepositoryVariable,
        HostingProvider.GitLab => GitLabRepositoryVariable,
        _ => RepositoryVariable,
    };

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
            foreach (var variable in NewVariables)
            {
                CapturedOthers[variable] = Read(variable);
                Environment.SetEnvironmentVariable(variable, null);
            }
        }
    }

    /// <summary>
    /// The sandbox of <paramref name="provider"/> and its token (15.2): a <see cref="LiveAzureSandbox"/>
    /// or a <see cref="LiveGitLabSandbox"/>; GitHub's stays 7a's <see cref="Require()"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">A variable is missing or not in its form; the message never names a value.</exception>
    public static LiveHostSandbox Require(HostingProvider provider)
    {
        if (provider == HostingProvider.GitHub)
        {
            throw new ArgumentException("GitHub's sandbox is 7a's Require().", nameof(provider));
        }

        string? token;
        string? repository;
        lock (Gate)
        {
            token = CapturedOthers.GetValueOrDefault(TokenVariableOf(provider));
            repository = CapturedOthers.GetValueOrDefault(RepositoryVariableOf(provider));
        }

        return provider == HostingProvider.AzureDevOps ? ResolveAzureDevOps(token, repository) : ResolveGitLab(token, repository);
    }

    /// <summary>15.2's rule for Azure DevOps: <c>&lt;organization&gt;/&lt;project&gt;/&lt;repository&gt;</c>, the organization <see cref="Organization"/>'s, the others 7a's <see cref="Segment"/>.</summary>
    /// <exception cref="InvalidOperationException">What is missing or wrong; the value is never named.</exception>
    internal static LiveAzureSandbox ResolveAzureDevOps(string? token, string? repository)
    {
        var (value, parts) = ResolveParts(HostingProvider.AzureDevOps, token, repository, "<organization>/<project>/<repository>",
            parts => parts.Length == 3 && Organization.IsMatch(parts[0]) && Segment.IsMatch(parts[1]) && Segment.IsMatch(parts[2]));
        return new LiveAzureSandbox(parts[0], parts[1], parts[2], value);
    }

    /// <summary>15.2's rule for GitLab: <c>&lt;namespace path&gt;/&lt;project&gt;</c>, 2 to 21 segments of 7a's <see cref="Segment"/>.</summary>
    /// <exception cref="InvalidOperationException">What is missing or wrong; the value is never named.</exception>
    internal static LiveGitLabSandbox ResolveGitLab(string? token, string? repository)
    {
        var (value, parts) = ResolveParts(HostingProvider.GitLab, token, repository, "<namespace path>/<project>",
            parts => parts.Length is >= 2 and <= 21 && parts.All(Segment.IsMatch));
        return new LiveGitLabSandbox(string.Join('/', parts[..^1]), parts[^1], value);
    }

    /// <summary>
    /// 15.2's "looks like a token": the value holds the token, starts like one of GitHub's tokens or
    /// with <c>glpat-</c>, or has 52 or 84 characters and no <c>/</c> (Azure DevOps' two lengths, r5 5).
    /// </summary>
    internal static bool LooksLikeAnyToken(string value, string? token) =>
        LooksLikeToken(value, token)
        || value.StartsWith("glpat-", StringComparison.OrdinalIgnoreCase)
        || (value.Length is 52 or 84 && !value.Contains('/', StringComparison.Ordinal));

    /// <summary>The token's kind for <paramref name="provider"/>, by a prefix or the length alone (r5 5, r6 2.4), never more of it.</summary>
    internal static string KindOf(HostingProvider provider, string token) => provider switch
    {
        HostingProvider.AzureDevOps => token.Length switch
        {
            84 => "of 84 characters (Azure DevOps' current length)",
            52 => "of 52 characters (Azure DevOps' legacy length)",
            _ => "of neither of Azure DevOps' two lengths",
        },
        HostingProvider.GitLab => token.StartsWith("glpat-", StringComparison.Ordinal) ? "GitLab's (glpat-)" : "of another kind (not glpat-)",
        _ => KindOf(token),
    };

    /// <summary>The token and the segments, or 15.2's message, which names the variables and the format and never a value.</summary>
    private static (string Token, string[] Parts) ResolveParts(HostingProvider provider, string? token, string? repository, string format, Func<string[], bool> inForm)
    {
        var tokenVariable = TokenVariableOf(provider);
        var repositoryVariable = RepositoryVariableOf(provider);
        token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        repository = string.IsNullOrWhiteSpace(repository) ? null : repository.Trim();
        var details = new List<string>();
        if (token is null && repository is null)
        {
            details.Add($"Missing: {tokenVariable} and {repositoryVariable}.");
        }
        else if (token is null)
        {
            details.Add($"Missing: {tokenVariable}.");
        }
        else if (repository is null)
        {
            details.Add($"Missing: {repositoryVariable}.");
        }

        string[]? parts = null;
        if (repository is not null)
        {
            parts = repository.Split('/');
            if (!inForm(parts))
            {
                details.Add(LooksLikeAnyToken(repository, token)
                    ? $"{repositoryVariable} holds what looks like a token, not {format}."
                    : $"{repositoryVariable} is not {format}.");
                parts = null;
            }
        }

        if (details.Count > 0 || token is null || parts is null)
        {
            throw new InvalidOperationException(
                $"The live command against {HostingProviders.DisplayName(provider)} needs two Windows user environment variables, set once on this machine (D346): {tokenVariable}, the sandbox's token, and {repositoryVariable}, the sandbox as {format}. "
                + string.Join(' ', details)
                + " Set them in Windows' Environment Variables dialog, then start a new terminal.");
        }

        return (token, parts);
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

/// <summary>A new provider's sandbox and its token (docs/phase-7b-design.md 15.2): <see cref="object.ToString"/> never shows the token, nor Azure DevOps' organization.</summary>
internal abstract class LiveHostSandbox(string token)
{
    public string Token { get; } = token;

    public abstract HostingProvider Provider { get; }

    /// <summary>The sandbox's full name as the app's <c>HostedRepository.FullName</c> gives it.</summary>
    public abstract string FullName { get; }
}

/// <summary>Azure DevOps' sandbox: <c>&lt;organization&gt;/&lt;project&gt;/&lt;repository&gt;</c>; <see cref="ToString"/> hides the organization.</summary>
internal sealed class LiveAzureSandbox(string organization, string project, string repository, string token) : LiveHostSandbox(token)
{
    public string Organization { get; } = organization;

    public string Project { get; } = project;

    public string Repository { get; } = repository;

    public override HostingProvider Provider => HostingProvider.AzureDevOps;

    public override string FullName => $"{Organization}/{Project}/{Repository}";

    public override string ToString() => $"<organization>/{Project}/{Repository}";
}

/// <summary>GitLab's sandbox: <c>&lt;namespace path&gt;/&lt;project&gt;</c>; the namespace's first segment is the account's login.</summary>
internal sealed class LiveGitLabSandbox(string @namespace, string project, string token) : LiveHostSandbox(token)
{
    public string Namespace { get; } = @namespace;

    public string Project { get; } = project;

    public override HostingProvider Provider => HostingProvider.GitLab;

    public override string FullName => $"{Namespace}/{Project}";

    /// <summary>The namespace's first segment: the login the sign-in must give (15.5 step 2).</summary>
    public string Login => Namespace.Split('/')[0];

    public override string ToString() => FullName;
}
