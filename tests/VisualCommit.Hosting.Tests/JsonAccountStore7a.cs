using System.Text.Json;
using System.Text.Json.Serialization;

// 7a's AccountsFile, AccountRecord and AccountsJsonContext word for word as at 5057892 (7a's code as
// merged), in a namespace of their own: JsonAccountStoreTests reads a file 7b wrote with them and
// reads their save with 7b's (docs/phase-7b-design.md 9.10; g4 D4 §6).
namespace VisualCommit.Hosting.Tests.Store7a;

/// <summary><c>accounts.json</c>: its version and the accounts' records (7.2).</summary>
public sealed record AccountsFile
{
    /// <summary>No file, or none that could be read.</summary>
    public static AccountsFile Empty { get; } = new();

    public int Version { get; init; } = JsonAccountStore.Version;

    public IReadOnlyList<AccountRecord> Accounts { get; init; } = [];

    /// <summary>The record the session uses (7.2): the first of provider <c>github</c> on <paramref name="host"/>; null without one.</summary>
    public AccountRecord? GitHubOn(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        return Accounts.FirstOrDefault(record => IsGitHubOn(record, host));
    }

    /// <summary>This file with <paramref name="record"/> in place of the first GitHub record on its host, or appended; the others kept (D292).</summary>
    public AccountsFile WithGitHub(AccountRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Host);
        var accounts = Accounts.ToList();
        var index = accounts.FindIndex(existing => IsGitHubOn(existing, record.Host));
        if (index >= 0)
        {
            accounts[index] = record;
        }
        else
        {
            accounts.Add(record);
        }

        return this with { Accounts = accounts };
    }

    /// <summary>This file without the first GitHub record on <paramref name="host"/>; the others kept (D292).</summary>
    public AccountsFile WithoutGitHub(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        var accounts = Accounts.ToList();
        var index = accounts.FindIndex(existing => IsGitHubOn(existing, host));
        if (index >= 0)
        {
            accounts.RemoveAt(index);
        }

        return this with { Accounts = accounts };
    }

    private static bool IsGitHubOn(AccountRecord record, string host) =>
        string.Equals(record.Provider, AccountRecord.GitHubProvider, StringComparison.OrdinalIgnoreCase)
        && string.Equals(record.Host, host, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// One account in <c>accounts.json</c> (7.2): never a token, nor a field that could hold one.
/// <see cref="TokenState"/> is read through <see cref="AccountTokenStates.Of"/>.
/// </summary>
public sealed record AccountRecord
{
    /// <summary>GitHub's provider id.</summary>
    public const string GitHubProvider = "github";

    public string? Provider { get; init; }

    public string? Host { get; init; }

    /// <summary>The login in its own case.</summary>
    public string? Login { get; init; }

    public long Id { get; init; }

    public string? Name { get; init; }

    public string? AvatarUrl { get; init; }

    /// <summary>
    /// "Use for git over HTTPS" (D255): on unless the file says otherwise. A setter, not
    /// <c>init</c>: the source-generated reader sets every <c>init</c> member in the object
    /// initializer, a missing one to its type's default, which would turn the switch off.
    /// </summary>
    public bool UseForGit { get; set; } = true;

    public string? TokenState { get; init; }
}

/// <summary>The file's names: camelCase, as the app's other files (D336).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(AccountsFile))]
internal sealed partial class AccountsJsonContext : JsonSerializerContext;
