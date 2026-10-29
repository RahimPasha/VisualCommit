using System.Net;
using System.Text;
using VisualCommit.Core;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The avatars kept in the data folder (docs/phase-7a-design.md 6.4, 6.5; D286, D336): PNG and
/// JPEG by their signatures and nothing else, never with the token, kept for seven days by the
/// clock, a failed refresh keeping the old image, one download per user and four at once,
/// redirects on the avatar's own address only, the index's camelCase round trip, and a cancelled
/// download writing nothing. Against the fake of GitHub in process, and a script for redirects.
/// </summary>
public sealed class AvatarCacheTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_png_is_kept_by_the_users_id_and_fetched_without_the_token()
    {
        using var setup = Setup.Start();

        var file = await setup.Cache.FileOfAsync(setup.Avatar(1001), Cancelled);

        Assert.Equal(setup.Folder.Combine("avatars", "1001.png"), file);
        Assert.Equal(setup.Fake.Users[1001].AvatarBytes, await File.ReadAllBytesAsync(file!, Cancelled));
        Assert.Equal(
            "{\"users\":{\"1001\":{\"file\":\"1001.png\",\"fetchedAt\":\"2026-01-02T09:00:00Z\"}}}",
            await File.ReadAllTextAsync(setup.Folder.Combine("avatars", "index.json"), Cancelled));
        var sent = Assert.Single(setup.Recorder.Sent);
        Assert.Equal("http://127.0.0.1:1/avatars/u/1001?v=4&s=64", sent.Url.AbsoluteUri);
        Assert.Equal(
            new[] { "Accept: image/png, image/jpeg", "User-Agent: VisualCommit/0.1.0" },
            sent.Headers.Select(header => $"{header.Key}: {header.Value}").Order(StringComparer.Ordinal));
        Assert.False(Assert.Single(setup.Fake.GitHub.Requests).TokenPresent);
        Assert.Equal(new[] { "1001.png", "index.json" }, Directory.GetFiles(setup.Folder.Combine("avatars")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Empty(setup.Log.Lines);
    }

    [Fact]
    public async Task A_jpeg_is_kept_as_jpg_and_anything_else_is_refused()
    {
        using var setup = Setup.Start();
        setup.Fake.Users[1003].AvatarBytes = Jpeg;
        setup.Fake.Users[1004].AvatarBytes = Encoding.UTF8.GetBytes("<html>not an image</html>");

        var jpeg = await setup.Cache.FileOfAsync(setup.Avatar(1003), Cancelled);
        var text = await setup.Cache.FileOfAsync(setup.Avatar(1004), Cancelled);
        var unknown = await setup.Cache.FileOfAsync(setup.Avatar(9999), Cancelled);

        Assert.Equal(setup.Folder.Combine("avatars", "1003.jpg"), jpeg);
        Assert.Equal(Jpeg, await File.ReadAllBytesAsync(jpeg!, Cancelled));
        Assert.Null(text);
        Assert.Null(unknown);
        Assert.False(File.Exists(setup.Folder.Combine("avatars", "1004.png")));
        Assert.Equal(
            new[] { "The avatar of user 1004 is not a PNG or JPEG image.", "The avatar of user 9999 could not be fetched: HTTP 404." },
            setup.Log.Lines.Select(line => line.Message));
    }

    [Fact]
    public async Task An_image_over_1_mb_is_refused()
    {
        using var setup = Setup.Start();
        setup.Fake.Users[1001].AvatarBytes = [.. Png, .. new byte[AvatarCache.MaxImageBytes]];

        Assert.Null(await setup.Cache.FileOfAsync(setup.Avatar(1001), Cancelled));
        Assert.Equal("The avatar of user 1001 is larger than 1 MB.", Assert.Single(setup.Log.Lines).Message);
    }

    [Fact]
    public async Task An_image_is_used_for_seven_days_then_fetched_again_and_a_failed_refresh_keeps_it()
    {
        using var setup = Setup.Start();
        var first = await setup.Cache.FileOfAsync(setup.Avatar(1001), Cancelled);
        var original = await File.ReadAllBytesAsync(first!, Cancelled);

        setup.Clock.Advance(AvatarCache.Lifetime);
        Assert.Equal(first, await setup.Cache.FileOfAsync(setup.Avatar(1001), Cancelled));
        Assert.Single(setup.Recorder.Sent);

        // Older than seven days: one request. It fails, and the old image stays.
        setup.Clock.Advance(TimeSpan.FromSeconds(1));
        setup.Fake.Users[1001].AvatarBytes = Encoding.UTF8.GetBytes("broken");
        Assert.Equal(first, await setup.Cache.FileOfAsync(setup.Avatar(1001), Cancelled));
        Assert.Equal(2, setup.Recorder.Sent.Count);
        Assert.Equal(original, await File.ReadAllBytesAsync(first!, Cancelled));

        // The next call asks again; a JPEG now replaces the PNG, with the clock's date.
        setup.Fake.Users[1001].AvatarBytes = Jpeg;
        var refreshed = await setup.Cache.FileOfAsync(setup.Avatar(1001), Cancelled);
        Assert.Equal(setup.Folder.Combine("avatars", "1001.jpg"), refreshed);
        Assert.False(File.Exists(first));
        Assert.Equal(3, setup.Recorder.Sent.Count);
        Assert.Equal(
            "{\"users\":{\"1001\":{\"file\":\"1001.jpg\",\"fetchedAt\":\"2026-01-09T09:00:01Z\"}}}",
            await File.ReadAllTextAsync(setup.Folder.Combine("avatars", "index.json"), Cancelled));
    }

    [Fact]
    public async Task The_index_is_read_back_by_another_cache_on_the_same_folder()
    {
        using var setup = Setup.Start();
        var octo = await setup.Cache.FileOfAsync(setup.Avatar(1001), Cancelled);
        setup.Fake.Users[1003].AvatarBytes = Jpeg;
        var ada = await setup.Cache.FileOfAsync(setup.Avatar(1003), Cancelled);

        var recorder = new RecordingHttpHandler(setup.Fake.GitHub.CreateHandler());
        using var again = new AvatarCache(setup.Folder.Combine("avatars"), () => recorder, setup.Clock, setup.Log);

        Assert.Equal(octo, await again.FileOfAsync(setup.Avatar(1001), Cancelled));
        Assert.Equal(ada, await again.FileOfAsync(setup.Avatar(1003), Cancelled));
        Assert.Empty(recorder.Sent);

        // An unreadable index costs a warning and a download, never an exception.
        await File.WriteAllTextAsync(setup.Folder.Combine("avatars", "index.json"), "{\"users\":", Cancelled);
        using var third = new AvatarCache(setup.Folder.Combine("avatars"), () => recorder, setup.Clock, setup.Log);
        Assert.Equal(octo, await third.FileOfAsync(setup.Avatar(1001), Cancelled));
        Assert.Single(recorder.Sent);
        Assert.Equal("The avatars' index could not be read: the avatars are fetched again.", Assert.Single(setup.Log.Lines).Message);

        // An index that parses but holds a null entry, a null file or a key that is not an id: those
        // are left out with the same warning, and the next save writes the index without them.
        await File.WriteAllTextAsync(
            setup.Folder.Combine("avatars", "index.json"),
            "{\"users\":{\"1001\":{\"file\":\"1001.png\",\"fetchedAt\":\"2026-01-02T09:00:00Z\"},\"1003\":null,\"1004\":{\"file\":null,\"fetchedAt\":\"2026-01-02T09:00:00Z\"},\"01\":{\"file\":\"1.png\",\"fetchedAt\":\"2026-01-02T09:00:00Z\"}}}",
            Cancelled);
        using var fourth = new AvatarCache(setup.Folder.Combine("avatars"), () => recorder, setup.Clock, setup.Log);
        Assert.Equal(octo, await fourth.FileOfAsync(setup.Avatar(1001), Cancelled));
        Assert.Equal(ada, await fourth.FileOfAsync(setup.Avatar(1003), Cancelled));
        Assert.Equal(2, recorder.Sent.Count);
        Assert.Equal(new[] { "The avatars' index could not be read: the avatars are fetched again.", "The avatars' index could not be read: the avatars are fetched again." }, setup.Log.Lines.Select(line => line.Message));
        Assert.Equal(
            "{\"users\":{\"1001\":{\"file\":\"1001.png\",\"fetchedAt\":\"2026-01-02T09:00:00Z\"},\"1003\":{\"file\":\"1003.jpg\",\"fetchedAt\":\"2026-01-02T09:00:00Z\"}}}",
            await File.ReadAllTextAsync(setup.Folder.Combine("avatars", "index.json"), Cancelled));
    }

    [Fact]
    public async Task Two_calls_for_one_user_make_one_request()
    {
        using var setup = Setup.Start();
        var hold = setup.Fake.GitHub.HoldNext("avatar:1001");

        var first = setup.Cache.FileOfAsync(setup.Avatar(1001), Cancelled);
        await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        var second = setup.Cache.FileOfAsync(setup.Avatar(1001), Cancelled);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Cancelled);
        Assert.False(second.IsCompleted);
        hold.Dispose();

        var files = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        Assert.Equal(files[0], files[1]);
        Assert.NotNull(files[0]);
        Assert.Single(setup.Recorder.Sent);
    }

    [Fact]
    public async Task At_most_four_downloads_run_at_once()
    {
        using var setup = Setup.Start();
        long[] ids = [1001, 1002, 1003, 1004, 1005];
        var holds = ids.Select(id => setup.Fake.GitHub.HoldNext(FormattableString.Invariant($"avatar:{id}"))).ToList();

        var calls = ids.Select(id => setup.Cache.FileOfAsync(setup.Avatar(id), Cancelled)).ToList();
        var arrived = holds.Select(hold => hold.Arrived).ToList();
        await WaitUntilAsync(() => arrived.Count(task => task.IsCompletedSuccessfully) == 4);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Cancelled);
        Assert.Equal(4, arrived.Count(task => task.IsCompletedSuccessfully));
        Assert.Equal(4, setup.Recorder.Sent.Count);

        holds[arrived.FindIndex(task => task.IsCompletedSuccessfully)].Dispose();
        await WaitUntilAsync(() => arrived.All(task => task.IsCompletedSuccessfully));
        holds.ForEach(hold => hold.Dispose());

        Assert.All(await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(60), Cancelled), Assert.NotNull);
        Assert.Equal(5, setup.Recorder.Sent.Count);
    }

    [Fact]
    public async Task A_cancelled_download_writes_neither_the_image_nor_the_index_after_its_release()
    {
        using var setup = Setup.Start();
        var hold = setup.Fake.GitHub.HoldNext("avatar:1001");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancelled);

        var call = setup.Cache.FileOfAsync(setup.Avatar(1001), cancel.Token);
        await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(TimeSpan.FromSeconds(60), Cancelled));
        hold.Dispose();
        await Task.Delay(TimeSpan.FromMilliseconds(200), Cancelled);

        Assert.False(File.Exists(setup.Folder.Combine("avatars", "1001.png")));
        Assert.False(File.Exists(setup.Folder.Combine("avatars", "index.json")));
        Assert.Empty(setup.Log.Lines);

        // A later call with its own token fetches it.
        Assert.NotNull(await setup.Cache.FileOfAsync(setup.Avatar(1001), Cancelled));
    }

    [Fact]
    public async Task A_download_cancelled_once_its_bytes_arrived_writes_neither_the_image_nor_the_index()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancelled);
        var recorder = new RecordingHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new CancellingAtItsEnd(Png, cancel)) });
        var log = new ListLog();
        using var folder = new TempDirectory("avatars");
        using var cache = new AvatarCache(folder.Combine("avatars"), () => recorder, new ManualTimeProvider(AppClocks.HarnessTime), log);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.FileOfAsync(new AuthorAvatar(1001, "octo", new Uri("http://127.0.0.1:1/avatars/u/1001?v=4")), cancel.Token));

        Assert.Single(recorder.Sent);
        Assert.False(Directory.Exists(folder.Combine("avatars")));
        Assert.Empty(log.Lines);
    }

    [Fact]
    public async Task A_cancellation_neither_the_callers_nor_the_time_limits_is_no_answer()
    {
        var recorder = new RecordingHttpHandler(_ => throw new TaskCanceledException("The operation was canceled.", new TimeoutException("A connection could not be made within 10 seconds.")));
        var log = new ListLog();
        using var folder = new TempDirectory("avatars");
        using var cache = new AvatarCache(folder.Combine("avatars"), () => recorder, new ManualTimeProvider(AppClocks.HarnessTime), log);

        Assert.Null(await cache.FileOfAsync(new AuthorAvatar(1001, "octo", new Uri("http://127.0.0.1:1/avatars/u/1001?v=4")), Cancelled));

        Assert.Equal("The avatar of user 1001 could not be fetched.", Assert.Single(log.Lines).Message);
    }

    [Theory]
    [InlineData("https://evil.example/u/1001", "https://evil.example:443")]
    [InlineData("http://127.0.0.1:2/avatars/u/1001", "http://127.0.0.1:2")]
    [InlineData("https://127.0.0.1:1/avatars/u/1001", "https://127.0.0.1:1")]
    public async Task A_redirect_off_the_avatars_address_gives_null_and_nothing_is_sent_there(string location, string origin)
    {
        var recorder = new RecordingHttpHandler(_ => Answer(HttpStatusCode.Found, [], ("Location", location)));
        var log = new ListLog();
        using var folder = new TempDirectory("avatars");
        using var cache = new AvatarCache(folder.Combine("avatars"), () => recorder, new ManualTimeProvider(AppClocks.HarnessTime), log);

        Assert.Null(await cache.FileOfAsync(new AuthorAvatar(1001, "octo", new Uri("http://127.0.0.1:1/avatars/u/1001?v=4")), Cancelled));

        Assert.Single(recorder.Sent);
        Assert.Equal($"The avatar of user 1001 was redirected to {origin}.", Assert.Single(log.Lines).Message);
        Assert.False(Directory.Exists(folder.Combine("avatars")));
    }

    [Fact]
    public async Task A_redirect_on_the_avatars_own_address_is_followed_without_the_token()
    {
        var recorder = new RecordingHttpHandler(request => request.RequestUri!.AbsolutePath == "/avatars/u/1001"
            ? Answer(HttpStatusCode.MovedPermanently, [], ("Location", "/avatars/u/1001/v4.png"))
            : Answer(HttpStatusCode.OK, Png));
        var log = new ListLog();
        using var folder = new TempDirectory("avatars");
        using var cache = new AvatarCache(folder.Combine("avatars"), () => recorder, new ManualTimeProvider(AppClocks.HarnessTime), log);

        var file = await cache.FileOfAsync(new AuthorAvatar(1001, "octo", new Uri("http://127.0.0.1:1/avatars/u/1001?s=460&v=4")), Cancelled);

        Assert.Equal(folder.Combine("avatars", "1001.png"), file);
        Assert.Equal(
            new[] { "http://127.0.0.1:1/avatars/u/1001?v=4&s=64", "http://127.0.0.1:1/avatars/u/1001/v4.png" },
            recorder.Sent.Select(sent => sent.Url.AbsoluteUri));
        Assert.All(recorder.Sent, sent => Assert.Null(sent.Header("Authorization")));
        Assert.Empty(log.Lines);
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, byte[] body, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
        foreach (var (name, value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }

    /// <summary>A body that cancels <paramref name="cancel"/> as its last read finds its end: the download's bytes arrive, then its call is cancelled.</summary>
    private sealed class CancellingAtItsEnd(byte[] bytes, CancellationTokenSource cancel) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count) => Ended(base.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Ended(base.Read(buffer));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromResult(Ended(base.Read(buffer.Span)));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromResult(Ended(base.Read(buffer, offset, count)));

        private int Ended(int read)
        {
            if (read == 0)
            {
                cancel.Cancel();
            }

            return read;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold within 60 s.");
            await Task.Delay(TimeSpan.FromMilliseconds(20), Cancelled);
        }
    }

    /// <summary>The fake in process with users 1001 to 1005, and a cache in a folder of its own over a recording handler, with a manual clock at the harness's time.</summary>
    private sealed class Setup : IDisposable
    {
        private Setup(TempDirectory folder, FakeHost fake, RecordingHttpHandler recorder, ManualTimeProvider clock, ListLog log, AvatarCache cache)
        {
            Folder = folder;
            Fake = fake;
            Recorder = recorder;
            Clock = clock;
            Log = log;
            Cache = cache;
        }

        public TempDirectory Folder { get; }

        public FakeHost Fake { get; }

        public RecordingHttpHandler Recorder { get; }

        public ManualTimeProvider Clock { get; }

        public ListLog Log { get; }

        public AvatarCache Cache { get; }

        public static Setup Start()
        {
            var folder = new TempDirectory("avatar-cache");
            var gitHub = FakeGitHub.InProcess(folder.Combine("github"));
            var users = new Dictionary<long, FakeUser>
            {
                [1001] = gitHub.AddUser("octo", 1001, "Octo Cat"),
                [1002] = gitHub.AddUser("acme", 1002, "Acme"),
                [1003] = gitHub.AddUser("ada", 1003, "Ada Lovelace"),
                [1004] = gitHub.AddUser("eve", 1004),
                [1005] = gitHub.AddUser("bob", 1005),
            };
            var recorder = new RecordingHttpHandler(gitHub.CreateHandler());
            var clock = new ManualTimeProvider(AppClocks.HarnessTime);
            var log = new ListLog();
            var cache = new AvatarCache(folder.Combine("avatars"), () => recorder, clock, log);
            return new Setup(folder, new FakeHost(gitHub, users), recorder, clock, log, cache);
        }

        /// <summary>The avatar the fake names for <paramref name="id"/>: <c>http://127.0.0.1:1/avatars/u/&lt;id&gt;?v=4</c>.</summary>
        public AuthorAvatar Avatar(long id) =>
            new(id, "user", new Uri(Fake.GitHub.Endpoints.WebBase, FormattableString.Invariant($"avatars/u/{id}?v=4")));

        public void Dispose()
        {
            Cache.Dispose();
            Assert.Empty(Fake.GitHub.Errors);
            Fake.GitHub.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Folder.Dispose();
        }
    }

    /// <summary>The fake and its users by id.</summary>
    private sealed record FakeHost(FakeGitHub GitHub, IReadOnlyDictionary<long, FakeUser> Users);
}
