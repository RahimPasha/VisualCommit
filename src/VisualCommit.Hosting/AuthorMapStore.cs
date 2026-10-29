using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using VisualCommit.Core;
using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting;

/// <summary>
/// The commit authors' e-mail maps (docs/phase-7a-design.md 7.9, D291): one per repository, shared
/// by its tabs, in <c>avatars/authors/github_&lt;host&gt;_&lt;owner&gt;_&lt;name&gt;.json</c> (lower
/// case, <c>:</c> as <c>_</c>), with the date of the commits page and each e-mail's own; and the
/// lookups by e-mail, at most <see cref="LookupsPerRepository"/> per repository per app session.
/// Read from the disk at a repository's first use, written whole through <see cref="DurableFile"/>,
/// one writer at a time. A sign-out drops the maps from memory (<see cref="Forget"/>); the lookup
/// counts stay.
/// </summary>
internal sealed class AuthorMapStore
{
    /// <summary>The lookups by e-mail a repository may make in one app session.</summary>
    public const int LookupsPerRepository = 20;

    /// <summary>How long the commits page and an e-mail's answer are used before they are asked again.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly string _directory;
    private readonly TimeProvider _clock;
    private readonly IAppLog _log;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, AuthorMapJson> _maps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _lookups = new(StringComparer.Ordinal);

    /// <param name="directory">The maps' folder, <c>avatars/authors</c> in the data folder; made at the first write.</param>
    public AuthorMapStore(string directory, TimeProvider clock, IAppLog log)
    {
        _directory = directory;
        _clock = clock;
        _log = log;
    }

    /// <summary>The map's file name: <c>github_127.0.0.1_47395_octo_hello.json</c>.</summary>
    public static string FileNameOf(HostedRepository repository) =>
        $"github_{repository.Host}_{repository.Owner}_{repository.Name}.json".Replace(':', '_').ToLowerInvariant();

    /// <summary>
    /// What to ask GitHub for <paramref name="emails"/> (lower case) of <paramref name="repository"/>
    /// (7.9 step 3): the commits page when the map is missing or its page is 7 days old or more; the
    /// e-mails the map lacks or whose answer is that old, in order, as many as the repository's
    /// lookups left, which they use up (<see cref="Merge"/> gives back those not reached).
    /// </summary>
    public (bool CommitsDue, IReadOnlyList<string> Emails) Plan(HostedRepository repository, IReadOnlyList<string> emails)
    {
        var key = FileNameOf(repository);
        var now = _clock.GetUtcNow();
        lock (_lock)
        {
            var map = MapLocked(key);
            var commitsDue = map?.FetchedAt is not { } fetched || now - fetched >= Lifetime;
            var left = LookupsPerRepository - _lookups.GetValueOrDefault(key);
            var due = emails
                .Where(email => map?.Authors?.FirstOrDefault(author => author.Email == email) is not { } known || now - known.CheckedAt >= Lifetime)
                .Take(Math.Max(0, left))
                .ToList();
            _lookups[key] = _lookups.GetValueOrDefault(key) + due.Count;
            return (commitsDue, due);
        }
    }

    /// <summary>
    /// Takes A6's answer into <paramref name="repository"/>'s map and writes it, unless
    /// <paramref name="ct"/> was cancelled (a sign-out): every e-mail answered, checked now, and the
    /// page's date when <paramref name="commitsRead"/>. The e-mails of <paramref name="asked"/> the
    /// answer lacks were not reached: their lookups are given back. Without an answer
    /// (<paramref name="map"/> null), every lookup is given back.
    /// </summary>
    public void Merge(HostedRepository repository, AuthorMap? map, bool commitsRead, IReadOnlyCollection<string> asked, CancellationToken ct)
    {
        var key = FileNameOf(repository);
        var now = _clock.GetUtcNow();
        lock (_lock)
        {
            var notReached = asked.Count(email => map?.ByEmail.ContainsKey(email) != true);
            _lookups[key] = Math.Max(0, _lookups.GetValueOrDefault(key) - notReached);
            if (map is null || ct.IsCancellationRequested)
            {
                return;
            }

            var current = MapLocked(key);
            var authors = (current?.Authors ?? []).ToDictionary(author => author.Email, StringComparer.Ordinal);
            foreach (var (email, avatar) in map.ByEmail)
            {
                authors[email] = new AuthorEntryJson
                {
                    Email = email,
                    UserId = avatar?.UserId,
                    Login = avatar?.Login,
                    AvatarUrl = avatar?.AvatarUrl.AbsoluteUri,
                    CheckedAt = now,
                };
            }

            var next = new AuthorMapJson
            {
                FetchedAt = commitsRead ? now : current?.FetchedAt,
                Authors = [.. authors.Values.OrderBy(author => author.Email, StringComparer.Ordinal)],
            };
            _maps[key] = next;
            try
            {
                Directory.CreateDirectory(_directory);
                DurableFile.Replace(Path.Combine(_directory, key), JsonSerializer.Serialize(next, AuthorMapJsonContext.Default.AuthorMapJson));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warning($"The authors of {repository.FullName} could not be saved.", ex);
            }
        }
    }

    /// <summary><paramref name="repository"/>'s map: each e-mail to its account, or null for one GitHub does not know.</summary>
    public IReadOnlyDictionary<string, AuthorAvatar?> Known(HostedRepository repository)
    {
        lock (_lock)
        {
            var known = new Dictionary<string, AuthorAvatar?>(StringComparer.Ordinal);
            foreach (var author in MapLocked(FileNameOf(repository))?.Authors ?? [])
            {
                known[author.Email] = author is { UserId: { } id, Login: { } login } && Uri.TryCreate(author.AvatarUrl, UriKind.Absolute, out var avatar)
                    ? new AuthorAvatar(id, login, avatar)
                    : null;
            }

            return known;
        }
    }

    /// <summary>A sign-out (7.5): the maps leave memory; the lookups used this session stay used.</summary>
    public void Forget()
    {
        lock (_lock)
        {
            _maps.Clear();
        }
    }

    // Under _lock: the map in memory, else the one on the disk (an unreadable one counts as none).
    private AuthorMapJson? MapLocked(string key)
    {
        if (_maps.TryGetValue(key, out var map))
        {
            return map;
        }

        var path = Path.Combine(_directory, key);
        try
        {
            map = File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllBytes(path), AuthorMapJsonContext.Default.AuthorMapJson) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warning($"The authors' map {key} could not be read: it is made again.", ex);
            map = null;
        }

        if (map is not null)
        {
            map.Authors = [.. (map.Authors ?? []).Where(author => author is { Email.Length: > 0 }).Select(author =>
            {
                author.Email = author.Email.ToLowerInvariant();
                return author;
            })];
            _maps[key] = map;
        }

        return map;
    }
}

/// <summary>One repository's map (D336): <c>{"fetchedAt":"…","authors":[{"email":"…","userId":1003,"login":"ada","avatarUrl":"…","checkedAt":"…"}]}</c>.</summary>
internal sealed class AuthorMapJson
{
    /// <summary>When the commits page was read; null before it was.</summary>
    public DateTimeOffset? FetchedAt { get; set; }

    public List<AuthorEntryJson>? Authors { get; set; }
}

/// <summary>One e-mail's answer: its user, or nulls for one GitHub does not know, and when it was checked.</summary>
internal sealed class AuthorEntryJson
{
    public string Email { get; set; } = string.Empty;

    public long? UserId { get; set; }

    public string? Login { get; set; }

    public string? AvatarUrl { get; set; }

    public DateTimeOffset CheckedAt { get; set; }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Email} → {Login ?? "unknown"}");
}

/// <summary>The maps' names: camelCase, as the app's other files (D336), never GitHub's snake_case.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AuthorMapJson))]
internal sealed partial class AuthorMapJsonContext : JsonSerializerContext;
