using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The credential test server serving an existing bare repository in place (design 7.3): the
/// sync scenario's <c>origin.git</c>, so that checks 18 to 20 and 22 can point <c>origin</c> at
/// the server while <see cref="Scenarios.PushFromElsewhereAsync"/> keeps pushing into the same
/// folder.
/// </summary>
public class AuthRemoteServerInPlaceTests
{
    [Fact]
    public async Task The_sync_scenarios_origin_is_served_in_place_and_sees_pushes_made_around_the_server()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        var origin = sync.SiblingPath("origin.git");
        var configBefore = await File.ReadAllTextAsync(Path.Combine(origin, "config"), TestCancelled);
        await using var server = await AuthRemoteServer.StartAsync(existingRepository: origin);
        server.RequireAuth = false;

        Assert.True(server.ServesInPlace);
        Assert.Equal("origin.git", server.RepositoryFolderName);
        Assert.Equal($"http://127.0.0.1:{server.Port}/origin.git", server.Url);
        AssertSameFolder(origin, server.RepositoryPath);
        await sync.GitAsync("remote", "set-url", "origin", server.Url);

        // Someone else pushes into the folder; a fetch through the server brings it.
        var elsewhere = await Scenarios.PushFromElsewhereAsync(sync, "main", "src/elsewhere.txt", "elsewhere\n", "Work from elsewhere");
        await sync.GitAsync("fetch", "--quiet", "origin");
        Assert.Equal(elsewhere, await sync.RefTargetAsync("refs/remotes/origin/main"));

        // A push through the server lands in the folder.
        await sync.GitAsync("merge", "--quiet", "--ff-only", "refs/remotes/origin/main");
        var local = await sync.CommitFileAsync("src/local.txt", "local\n", "Local work");
        await sync.GitAsync("push", "--quiet", "origin", "refs/heads/main:refs/heads/main");
        Assert.Equal(local, await GitInAsync(sync, origin, "rev-parse", "refs/heads/main"));

        Assert.Contains(server.Requests, request => request.Path == "/origin.git/git-upload-pack" && request.Status == 200);
        Assert.Contains(server.Requests, request => request.Path == "/origin.git/git-receive-pack" && request.Status == 200);
        Assert.Empty(server.Errors);

        // The repository's configuration is its own: the server sets nothing in it.
        Assert.Equal(configBefore, await File.ReadAllTextAsync(Path.Combine(origin, "config"), TestCancelled));

        // Disposing the server leaves the repository where it was.
        await server.DisposeAsync();
        Assert.Equal(local, await GitInAsync(sync, origin, "rev-parse", "refs/heads/main"));
    }

    [Fact]
    public async Task A_repository_served_in_place_asks_for_credentials_like_the_servers_own()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        await using var server = await AuthRemoteServer.StartAsync(existingRepository: sync.SiblingPath("origin.git"));
        await sync.GitAsync("remote", "set-url", "origin", server.Url);

        var refused = await sync.GitAllowingFailureAsync("fetch", "origin");
        Assert.False(refused.Succeeded);
        Assert.Contains("could not read Username", refused.StandardError, StringComparison.Ordinal);

        var signedIn = await sync.GitAllowingFailureAsync("-c", "http.extraHeader=Authorization: Basic " + Convert.ToBase64String("alice:s3cret"u8), "ls-remote", "origin", "refs/heads/published");
        Assert.True(signedIn.Succeeded, signedIn.StandardError);
        Assert.Equal(await sync.RefTargetAsync("refs/remotes/origin/published"), signedIn.StandardOutput.Split('\t')[0]);

        // Only the served repository is reachable, not the folders beside it.
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String("alice:s3cret"u8));
        using var beside = await client.GetAsync(new Uri($"http://127.0.0.1:{server.Port}/sync/.git/info/refs?service=git-upload-pack"), TestCancelled);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, beside.StatusCode);
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task Only_an_existing_bare_repository_with_a_plain_name_is_served()
    {
        using var sync = await Scenarios.RemoteSyncAsync();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => AuthRemoteServer.StartAsync(existingRepository: sync.SiblingPath("missing.git")));
        await Assert.ThrowsAsync<ArgumentException>(() => AuthRemoteServer.StartAsync(existingRepository: sync.Path));

        // A name that would need escaping in the URL is refused before anything else.
        var spaced = sync.SiblingPath("with space.git");
        Directory.CreateDirectory(spaced);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AuthRemoteServer.StartAsync(existingRepository: spaced));
        Assert.Contains("with space.git", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_servers_own_repository_is_unchanged_by_the_new_parameter()
    {
        await using var server = await AuthRemoteServer.StartAsync();

        Assert.False(server.ServesInPlace);
        Assert.Equal(AuthRemoteServer.RepositoryName, server.RepositoryFolderName);
        Assert.EndsWith("/remote.git", server.Url, StringComparison.Ordinal);
        Assert.True(Directory.Exists(server.RepositoryPath));
    }

    private static async Task<string> GitInAsync(TempRepo repo, string folder, params string[] arguments)
    {
        var command = new GitCommand(arguments) { WorkingDirectory = folder, Environment = repo.Environment };
        return (await repo.Runner.RunAsync(command, TestCancelled)).EnsureSuccess(command).StandardOutput.Trim();
    }
}
