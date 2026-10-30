using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using VisualCommit.Core;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.GitHub;

namespace VisualCommit.Hosting;

/// <summary>
/// Commit authors' avatars, kept in the data folder's <c>avatars/</c> (docs/phase-7a-design.md
/// 6.4; D252, D286): <c>&lt;user id&gt;.png</c> or <c>.jpg</c> and an <c>index.json</c> of when
/// each was fetched, by <see cref="TimeProvider"/>, so a frozen clock never expires one. An image
/// is fetched when missing or older than <see cref="Lifetime"/>, with <c>s=64</c> and never the
/// token, at most 1 MB, four downloads at once and one per user; a redirect is followed only on
/// the avatar address's own scheme, host and port; only PNG and JPEG are kept, by their
/// signatures. A failed refresh keeps the old image. Every file operation runs on the thread pool;
/// a failure is a warning and null, never an exception, and a cancelled call writes nothing.
/// </summary>
public sealed class AvatarCache : IDisposable
{
    /// <summary>How long an image is used before it is fetched again.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>The largest image kept.</summary>
    public const int MaxImageBytes = 1024 * 1024;

    /// <summary>The downloads that run at once, at most.</summary>
    public const int MaxDownloads = 4;

    /// <summary>The index of fetch dates, in the folder beside the images.</summary>
    public const string IndexFileName = "index.json";

    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    private readonly string _directory;
    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly IAppLog _log;
    private readonly SemaphoreSlim _downloads = new(MaxDownloads, MaxDownloads);
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _users = new();
    private readonly Lock _indexGate = new();
    private Dictionary<string, AvatarIndexEntryJson>? _index;

    /// <param name="directory">The folder of the images, <c>AppPaths.AvatarsDirectory</c>; made at the first image.</param>
    /// <param name="createHandler">Makes the handler of the images' client.</param>
    /// <param name="clock">The fetch dates' clock.</param>
    /// <param name="log">Where failures are written, as warnings that name the user's id, never an address.</param>
    public AvatarCache(string directory, Func<HttpMessageHandler> createHandler, TimeProvider clock, IAppLog log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(createHandler);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(log);
        _directory = directory;
        _clock = clock;
        _log = log;
        _http = new HttpClient(createHandler(), disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>
    /// The local image of <paramref name="avatar"/>'s user: the kept one while it is younger than
    /// <see cref="Lifetime"/>, else one fetched now (the kept one when that fails); null when there
    /// is none. Cancelling <paramref name="ct"/> throws <see cref="OperationCanceledException"/>,
    /// and a download it cancels writes neither the image nor the index.
    /// </summary>
    public Task<string?> FileOfAsync(AuthorAvatar avatar, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(avatar);
        return Task.Run(() => FileOfCoreAsync(avatar, ct));
    }

    public void Dispose() => _http.Dispose();

    private static bool IsPng(byte[] bytes) => bytes.AsSpan().StartsWith(PngSignature);

    private static bool IsJpeg(byte[] bytes) => bytes.AsSpan().StartsWith(JpegSignature);

    /// <summary><paramref name="url"/> with <c>s=64</c> in its query in place of any <c>s</c> it had (r5 I1; harmless where it is ignored).</summary>
    private static Uri WithSize(Uri url)
    {
        var query = url.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part != "s" && !part.StartsWith("s=", StringComparison.Ordinal));
        return new UriBuilder(url) { Query = string.Join('&', [.. query, "s=64"]) }.Uri;
    }

    private async Task<string?> FileOfCoreAsync(AuthorAvatar avatar, CancellationToken ct)
    {
        var user = _users.GetOrAdd(avatar.UserId, _ => new SemaphoreSlim(1, 1));
        await user.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var kept = Kept(avatar.UserId);
            if (kept is { } fresh && _clock.GetUtcNow() - fresh.FetchedAt <= Lifetime)
            {
                return fresh.Path;
            }

            var bytes = await DownloadAsync(avatar, ct).ConfigureAwait(false);
            return bytes is null ? kept?.Path : Save(avatar.UserId, bytes, ct) ?? kept?.Path;
        }
        finally
        {
            user.Release();
        }
    }

    /// <summary>The kept image of <paramref name="userId"/> and when it was fetched, when the index names one that exists.</summary>
    private (string Path, DateTimeOffset FetchedAt)? Kept(long userId)
    {
        lock (_indexGate)
        {
            if (!IndexLocked().TryGetValue(Key(userId), out var entry) || !IsFileOf(userId, entry.File))
            {
                return null;
            }

            var path = Path.Combine(_directory, entry.File);
            return File.Exists(path) ? (path, new DateTimeOffset(DateTime.SpecifyKind(entry.FetchedAt, DateTimeKind.Utc))) : null;
        }
    }

    /// <summary>
    /// The image's bytes, or null with a warning: a failed or refused answer, a redirect off the
    /// avatar address's scheme, host and port (nothing is sent there), more than three redirects,
    /// more than 1 MB, neither PNG nor JPEG, no answer or the 30 s run out.
    /// </summary>
    private async Task<byte[]?> DownloadAsync(AuthorAvatar avatar, CancellationToken ct)
    {
        var id = avatar.UserId;
        var origin = avatar.AvatarUrl;
        if (!origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps && origin.Scheme != Uri.UriSchemeHttp)
        {
            _log.Warning($"The avatar of user {id} is not on a web address.");
            return null;
        }

        await _downloads.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(DownloadTimeout);
            var current = WithSize(origin);
            for (var hop = 0; ; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.TryAddWithoutValidation("User-Agent", GitHubClient.UserAgent);
                request.Headers.TryAddWithoutValidation("Accept", "image/png, image/jpeg");
                try
                {
                    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false);
                    var status = (int)response.StatusCode;
                    if (GitHubClient.IsRedirect(status))
                    {
                        var location = GitHubClient.Resolve(current, response.Headers.Location);
                        if (location is not null && !GitHubUrls.SameOrigin(location, origin))
                        {
                            _log.Warning($"The avatar of user {id} was redirected to {GitHubUrls.OriginOf(location)}.");
                            return null;
                        }

                        if (location is null || hop >= GitHubClient.MaxRedirects)
                        {
                            _log.Warning($"The avatar of user {id} could not be fetched: HTTP {status}.");
                            return null;
                        }

                        current = location;
                        continue;
                    }

                    if (status is < 200 or >= 300)
                    {
                        _log.Warning($"The avatar of user {id} could not be fetched: HTTP {status}.");
                        return null;
                    }

                    var bytes = await GitHubClient.ReadBoundedAsync(response.Content, MaxImageBytes, limit.Token).ConfigureAwait(false);
                    if (bytes is null)
                    {
                        _log.Warning($"The avatar of user {id} is larger than 1 MB.");
                        return null;
                    }

                    if (!IsPng(bytes) && !IsJpeg(bytes))
                    {
                        _log.Warning($"The avatar of user {id} is not a PNG or JPEG image.");
                        return null;
                    }

                    return bytes;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (limit.IsCancellationRequested)
                {
                    _log.Warning($"The avatar of user {id} did not arrive within 30 seconds.");
                    return null;
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
                {
                    // A cancellation that is neither the caller's nor the 30 s (the handler's connect
                    // time-out, a cache disposed mid-download) is no answer.
                    _log.Warning($"The avatar of user {id} could not be fetched.", ex);
                    return null;
                }
            }
        }
        finally
        {
            _downloads.Release();
        }
    }

    /// <summary>
    /// Writes the image as <c>&lt;id&gt;.png</c> or <c>.jpg</c> beside its temporary file, flushed,
    /// then moved over it (as <see cref="DurableFile.Replace"/>), removes the other type's image,
    /// and records the fetch in the index. Its path, or null with a warning when the disk refused.
    /// Nothing is written once <paramref name="ct"/> is cancelled: the last check comes after the
    /// temporary file is flushed (which is then deleted), and past it the image, the other type's
    /// removal and the index are written together.
    /// </summary>
    private string? Save(long userId, byte[] bytes, CancellationToken ct)
    {
        var (name, other) = IsPng(bytes)
            ? (Name(userId, ".png"), Name(userId, ".jpg"))
            : (Name(userId, ".jpg"), Name(userId, ".png"));
        var path = Path.Combine(_directory, name);
        var temporary = DurableFile.TemporaryOf(path);
        try
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_directory);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            lock (_indexGate)
            {
                ct.ThrowIfCancellationRequested();
                File.Move(temporary, path, overwrite: true);
                File.Delete(Path.Combine(_directory, other));
                var index = IndexLocked();
                index[Key(userId)] = new AvatarIndexEntryJson { File = name, FetchedAt = _clock.GetUtcNow().UtcDateTime };
                var json = JsonSerializer.Serialize(new AvatarIndexJson { Users = new SortedDictionary<string, AvatarIndexEntryJson>(index, StringComparer.Ordinal) }, AvatarIndexJsonContext.Default.AvatarIndexJson);
                DurableFile.Replace(Path.Combine(_directory, IndexFileName), json);
            }

            return path;
        }
        catch (OperationCanceledException)
        {
            DeleteTemporary(temporary);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning($"The avatar of user {userId} could not be saved.", ex);
            DeleteTemporary(temporary);
            return null;
        }
    }

    /// <summary>Removes the temporary image of a cancelled or failed save; when that fails too, the next save writes over it.</summary>
    private static void DeleteTemporary(string temporary)
    {
        try
        {
            File.Delete(temporary);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The next save of this image creates it anew.
        }
    }

    /// <summary>
    /// The index, read from the disk once; empty when it is missing or unreadable (each image is then
    /// fetched again). An entry that is null, or whose key is not a user's id or whose file is not
    /// that user's image, is left out with the same warning. An entry whose image is gone is left out
    /// without one, from a single listing of the folder, so the next save writes the index without
    /// it. An image that goes later is caught by <see cref="Kept"/>, which fetches it again; its
    /// entry stays until that user's image is saved again or the index is next read.
    /// </summary>
    private Dictionary<string, AvatarIndexEntryJson> IndexLocked()
    {
        if (_index is not null)
        {
            return _index;
        }

        var path = Path.Combine(_directory, IndexFileName);
        try
        {
            var read = File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllBytes(path), AvatarIndexJsonContext.Default.AvatarIndexJson) : null;
            var users = read?.Users ?? new Dictionary<string, AvatarIndexEntryJson>();
            var entries = users.Where(pair => IsEntry(pair.Key, pair.Value)).ToList();
            if (entries.Count < users.Count)
            {
                _log.Warning("The avatars' index could not be read: the avatars are fetched again.");
            }

            var images = entries.Count > 0 ? Directory.EnumerateFiles(_directory).Select(file => Path.GetFileName(file)).ToHashSet(StringComparer.Ordinal) : [];
            _index = new Dictionary<string, AvatarIndexEntryJson>(entries.Where(pair => images.Contains(pair.Value.File)), StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warning("The avatars' index could not be read: the avatars are fetched again.", ex);
            _index = new(StringComparer.Ordinal);
        }

        return _index;
    }

    private static string Key(long userId) => userId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Name(long userId, string extension) => Key(userId) + extension;

    /// <summary>Whether an index entry read from the disk is one this cache writes: a user's id, and that user's image.</summary>
    private static bool IsEntry(string key, AvatarIndexEntryJson? entry) =>
        entry is not null
        && long.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var userId)
        && Key(userId) == key
        && IsFileOf(userId, entry.File);

    /// <summary>Whether the index's file name is this user's image, so that no name it holds reaches outside the folder.</summary>
    private static bool IsFileOf(long userId, string? file) => file == Name(userId, ".png") || file == Name(userId, ".jpg");
}

/// <summary><c>avatars/index.json</c>: <c>{"users":{"1003":{"file":"1003.png","fetchedAt":"2026-01-02T09:00:00Z"}}}</c> (D336).</summary>
internal sealed class AvatarIndexJson
{
    public IDictionary<string, AvatarIndexEntryJson>? Users { get; set; }
}

/// <summary>One user's image in the index and when it was fetched, in UTC.</summary>
internal sealed class AvatarIndexEntryJson
{
    public required string File { get; set; }

    public DateTime FetchedAt { get; set; }
}

/// <summary>The index's names: camelCase, as the app's other files (D336), never GitHub's snake_case.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AvatarIndexJson))]
internal sealed partial class AvatarIndexJsonContext : JsonSerializerContext;
