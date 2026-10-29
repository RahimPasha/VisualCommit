using System.Text.Json;
using System.Text.Json.Serialization;
using VisualCommit.Core;
using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting;

/// <summary>
/// <c>accounts.json</c> in the data folder (docs/phase-7a-design.md 7.2; D287, D292, D336): the
/// accounts' records, never a token. A missing file means no account. A file that cannot be read
/// is set aside as <c>accounts.unreadable.json</c> with a warning, and the session starts with no
/// account, so a damaged file never stops the app. Written through <see cref="DurableFile.Replace"/>;
/// the hosting session calls it off the UI thread, one call at a time.
/// </summary>
public sealed class JsonAccountStore
{
    /// <summary>The file's version.</summary>
    public const int Version = 1;

    private readonly string _filePath;
    private readonly IAppLog _log;

    public JsonAccountStore(string filePath, IAppLog log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(log);
        _filePath = filePath;
        _log = log;
    }

    /// <summary>The records in the file: none when it is missing or could not be read (then set aside).</summary>
    public AccountsFile Load()
    {
        if (!File.Exists(_filePath))
        {
            return AccountsFile.Empty;
        }

        try
        {
            var read = JsonSerializer.Deserialize(File.ReadAllText(_filePath), AccountsJsonContext.Default.AccountsFile);

            // A hand-edited file's null list or null record is read as none.
            return new AccountsFile
            {
                Version = read?.Version ?? Version,
                Accounts = read?.Accounts?.Where(record => record is not null).ToList() ?? [],
            };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _log.Warning($"The accounts file {_filePath} could not be read. The app starts with no account.", ex);
            SetAside();
            return AccountsFile.Empty;
        }
    }

    /// <summary>Writes <paramref name="file"/>, replacing the file whole; a failure is logged as an error, never thrown.</summary>
    public void Save(AccountsFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
            DurableFile.Replace(_filePath, JsonSerializer.Serialize(file with { Version = Version }, AccountsJsonContext.Default.AccountsFile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error($"The accounts could not be saved to {_filePath}.", ex);
        }
    }

    private void SetAside()
    {
        try
        {
            File.Move(_filePath, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_filePath))!, "accounts.unreadable.json"), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The next save overwrites the unreadable file instead.
        }
    }
}

/// <summary>Where an account's token is, as <c>accounts.json</c> says (D287).</summary>
public static class AccountTokenStates
{
    /// <summary>Saved in this computer's keychain: the next start reads it from there.</summary>
    public const string Keychain = "keychain";

    /// <summary>Kept in memory only (no keychain, or the save failed): gone at the next start.</summary>
    public const string Session = "session";

    /// <summary>GitHub refused it: the next start reads no store.</summary>
    public const string Refused = "refused";

    /// <summary><paramref name="value"/> as one of the three; missing or unknown is <see cref="Keychain"/>.</summary>
    public static string Of(string? value) => value switch
    {
        Session => Session,
        Refused => Refused,
        _ => Keychain,
    };
}

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
