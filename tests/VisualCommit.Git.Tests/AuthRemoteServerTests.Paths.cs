using System.Net;
using System.Net.Sockets;
using System.Text;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// A host's repositories at a path of any depth (docs/phase-7b-design.md 2.2, g4 D3.4): Azure DevOps'
/// four levels with a space, GitLab's groups with or without <c>.git</c>, the longest registration,
/// the segments that put a path in no repository, the URLs, and 7b's three callbacks.
/// </summary>
public partial class AuthRemoteServerTests
{
    private const string MobileApp = "Fernhill-Labs/Mobile App/_git/mobile-app";

    [Fact]
    public async Task A_path_of_four_levels_with_a_space_is_cloned_through_its_escape_and_in_another_case()
    {
        using var host = await PathHostAsync(isPublic: false, MobileApp, "g/s/p.git");
        await using var server = await AuthRemoteServer.StartForHostAsync(host.Folder, host.Options);
        server.ServeAt(MobileApp, MobileApp, gitSuffix: false);
        Assert.Equal($"http://127.0.0.1:{server.Port}/Fernhill-Labs/Mobile%20App/_git/mobile-app", server.UrlOf(MobileApp));
        Assert.Equal(server.UrlOf(MobileApp), server.UrlOf("fernhill-labs/mobile app/_GIT/Mobile-App"));
        Assert.Contains(MobileApp, server.ServedRepositories);

        await GitOkAsync(host.Repo, "clone", "--quiet", WithCredentials(server.UrlOf(MobileApp), "x-access-token", OctoToken), host.Repo.SiblingPath("escaped"));
        var otherCase = $"http://127.0.0.1:{server.Port}/FERNHILL-LABS/mobile%20app/_Git/MOBILE-APP";
        await GitOkAsync(host.Repo, "clone", "--quiet", WithCredentials(otherCase, "Fernhill-Labs", OctoToken), host.Repo.SiblingPath("other-case"));
        Assert.Equal(["Second", "First"], Lines(await GitInAsync(host.Repo, host.Repo.SiblingPath("escaped"), "log", "--format=%s")));
        Assert.Equal(["Second", "First"], Lines(await GitInAsync(host.Repo, host.Repo.SiblingPath("other-case"), "log", "--format=%s")));

        // The records keep the raw path; the callbacks name the folder.
        var served = server.Requests.Where(request => request.Status == 200).ToList();
        Assert.Contains(served, request => request.Path == "/Fernhill-Labs/Mobile%20App/_git/mobile-app/git-upload-pack" && request.GivenUserName == "x-access-token");
        Assert.Contains(served, request => request.Path == "/FERNHILL-LABS/mobile%20app/_Git/MOBILE-APP/git-upload-pack" && request.GivenUserName == "Fernhill-Labs");
        Assert.All(served, request => Assert.Equal("octo", request.UserName));
        Assert.NotEmpty(host.Listed);
        Assert.All(host.Listed, name => Assert.Equal(MobileApp, name));

        // Without gitSuffix a final .git names no repository.
        Assert.Equal(404, await RawStatusAsync(server.Port, "/Fernhill-Labs/Mobile%20App/_git/mobile-app.git/info/refs?service=git-upload-pack"));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task With_gitSuffix_a_path_is_served_with_and_without_git_and_the_longest_registration_wins()
    {
        using var host = await PathHostAsync(isPublic: false, "g/s/p.git", "g/s/p/q.git");
        await GitOkAsync(host.Repo, "-C", Path.Combine(host.Folder, "g", "s", "p", "q.git"), "branch", "only-in-q", "main");
        await using var server = await AuthRemoteServer.StartForHostAsync(host.Folder, host.Options);
        server.ServeAt("g/s/p", "g/s/p.git", gitSuffix: true);
        server.ServeAt("g/s/p/q", "g/s/p/q.git", gitSuffix: true);
        Assert.Equal($"http://127.0.0.1:{server.Port}/g/s/p.git", server.UrlOf("g/s/p"));

        foreach (var url in new[] { $"http://127.0.0.1:{server.Port}/g/s/p.git", $"http://127.0.0.1:{server.Port}/g/s/p" })
        {
            var listed = await GitAsync(host.Repo, ["ls-remote", WithCredentials(url, "x-access-token", OctoToken)]);
            Assert.True(listed.Succeeded, listed.StandardError);
            Assert.DoesNotContain("only-in-q", listed.StandardOutput, StringComparison.Ordinal);
        }

        // g/s/p/q lies under g/s/p's path: the longer registration is its repository.
        var nested = await GitAsync(host.Repo, ["ls-remote", WithCredentials($"http://127.0.0.1:{server.Port}/g/s/p/q.git", "x-access-token", OctoToken)]);
        Assert.True(nested.Succeeded, nested.StandardError);
        Assert.Contains("\trefs/heads/only-in-q", nested.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(["g/s/p", "g/s/p/q"], host.Listed.Distinct());
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_segment_that_does_not_decode_to_a_plain_one_is_in_no_repository()
    {
        using var host = await PathHostAsync(isPublic: true, "g/s/p.git");
        await using var server = await AuthRemoteServer.StartForHostAsync(host.Folder, host.Options);
        server.ServeAt("g/s/p", "g/s/p.git", gitSuffix: true);

        // The public repository answers without a credential, so each refusal is the path's.
        Assert.Equal(200, await RawStatusAsync(server.Port, "/g/s/p/info/refs?service=git-upload-pack"));
        foreach (var path in new[]
        {
            "/g%2Fs/p/info/refs?service=git-upload-pack",
            "/g/s/p/info%5Crefs?service=git-upload-pack",
            "/g/s/p/%2E%2E/p/info/refs?service=git-upload-pack",
            "/g/s/p/./info/refs?service=git-upload-pack",
            "/g/s/p/%C3%28/info/refs?service=git-upload-pack",
            "/g/s/p/%zz/info/refs?service=git-upload-pack",
        })
        {
            Assert.Equal(404, await RawStatusAsync(server.Port, path));
        }

        Assert.Equal(6, server.Requests.Count(request => request.Status == 404));
        Assert.Equal(["g/s/p"], host.Listed);
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task Seven_bs_callbacks_refuse_a_push_a_fetch_and_a_token_in_its_own_words()
    {
        using var host = await PathHostAsync(isPublic: false, "g/s/p.git", "o/r.git");
        var options = host.Options with
        {
            PushRefusal = (repository, login) => $"You are not allowed to push to {repository} as {login}.",
            FetchRefusal = (repository, login) => login == "ada" ? "You are not allowed to download code." : null,
            LoginOfTokenIn = (repository, token) => repository == "g/s/p" && token == OctoToken ? "octo" : repository == "g/s/p" && token == AdaToken ? "ada" : null,
        };
        await using var server = await AuthRemoteServer.StartForHostAsync(host.Folder, options);
        server.ServeAt("g/s/p", "g/s/p.git", gitSuffix: true);
        server.Serve("o/r");
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });

        // ada may not fetch: a 403 text/plain before http-backend, which never ran for her.
        var refusedFetch = await GetAsync(client, server.UrlOf("g/s/p") + "/info/refs?service=git-upload-pack", AdaToken);
        Assert.Equal((HttpStatusCode.Forbidden, "You are not allowed to download code.\n"), (refusedFetch.Status, refusedFetch.Body));
        Assert.Equal(["text/plain; charset=utf-8"], refusedFetch.ContentType);
        Assert.False((await GitAsync(host.Repo, ["ls-remote", WithCredentials(server.UrlOf("g/s/p"), "x-access-token", AdaToken)])).Succeeded);

        // octo may fetch but not push: the host's words.
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, server.UrlOf("g/s/p") + "/info/refs?service=git-upload-pack", OctoToken)).Status);
        var refusedPush = await GetAsync(client, server.UrlOf("g/s/p") + "/info/refs?service=git-receive-pack", OctoToken);
        Assert.Equal((HttpStatusCode.Forbidden, "You are not allowed to push to g/s/p as octo.\n"), (refusedPush.Status, refusedPush.Body));

        // In o/r LoginOfTokenIn knows no token: 401, the user name given kept. A path in no
        // repository still asks LoginOfToken, which knows octo's token: its 404.
        var refusedToken = await GetAsync(client, server.UrlOf("o/r") + "/info/refs?service=git-upload-pack", OctoToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refusedToken.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(client, $"http://127.0.0.1:{server.Port}/nowhere/x/info/refs?service=git-upload-pack", OctoToken)).Status);

        var requests = server.Requests;
        Assert.Contains(requests, request => request is { Status: 403, UserName: "ada", Query: "service=git-upload-pack" });
        Assert.Contains(requests, request => request is { Status: 403, UserName: "octo", Query: "service=git-receive-pack" });
        Assert.Contains(requests, request => request is { Status: 401, UserName: null, GivenUserName: "x-access-token", Path: "/o/r.git/info/refs" });
        Assert.Equal(["g/s/p"], host.Listed);
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task One_folder_at_two_paths_is_named_alike_in_callbacks_and_holds()
    {
        using var host = await PathHostAsync(isPublic: false, "Org/Proj/_git/repo");
        await using var server = await AuthRemoteServer.StartForHostAsync(host.Folder, host.Options);
        server.ServeAt("Org/Proj/_git/repo", "Org/Proj/_git/repo", gitSuffix: false);
        server.ServeAt("Org/_git/repo", "Org/Proj/_git/repo", gitSuffix: false);

        foreach (var path in new[] { "Org/Proj/_git/repo", "Org/_git/repo" })
        {
            var listed = await GitAsync(host.Repo, ["ls-remote", WithCredentials(server.UrlOf(path), "x-access-token", OctoToken)]);
            Assert.True(listed.Succeeded, listed.StandardError);
        }

        Assert.NotEmpty(host.Listed);
        Assert.All(host.Listed, name => Assert.Equal("Org/Proj/_git/repo", name));
        Assert.Equal(["Org/Proj/_git/repo"], server.ServedRepositories);

        // A gate on the folder's name, without case, holds a request through either path.
        var arrived = server.HoldNextRequest("org/proj/_git/REPO");
        var held = GitAsync(host.Repo, ["ls-remote", WithCredentials(server.UrlOf("Org/_git/repo"), "x-access-token", OctoToken)]);
        await arrived.WaitAsync(StepTimeout, TestCancelled);
        Assert.False(held.IsCompleted, "The request was answered while it was held.");
        server.ReleaseRequest("Org/Proj/_git/repo");
        Assert.True((await held.WaitAsync(StepTimeout, TestCancelled)).Succeeded);
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task ServeAt_refuses_a_plain_server_a_path_or_folder_it_cannot_serve_and_a_second_folder()
    {
        using var host = await PathHostAsync(isPublic: false, "g/s/p.git", "g/s/q.git");
        await using var plain = await AuthRemoteServer.StartAsync();
        Assert.Throws<InvalidOperationException>(() => plain.ServeAt("g/s/p", "g/s/p.git", gitSuffix: true));

        await using var server = await AuthRemoteServer.StartForHostAsync(host.Folder, host.Options);
        foreach (var path in new[] { "g//p", "g/./p", "g/../p", "g\\s/p", "g/s\u0001/p", "/g/s/p", "g/s/p/" })
        {
            Assert.Throws<ArgumentException>(() => server.ServeAt(path, "g/s/p.git", gitSuffix: true));
            Assert.Throws<ArgumentException>(() => server.ServeAt("g/s/p", path, gitSuffix: true));
        }

        Assert.Throws<ArgumentException>(() => server.ServeAt("g/s/missing", "g/s/missing.git", gitSuffix: true));
        server.ServeAt("g/s/p", "g/s/p.git", gitSuffix: true);
        server.ServeAt("g/s/p", "g/s/p.git", gitSuffix: true);
        Assert.Throws<ArgumentException>(() => server.ServeAt("G/S/P", "g/s/q.git", gitSuffix: true));
        Assert.Throws<ArgumentException>(() => server.ServeAt("g/s/p", "g/s/p.git", gitSuffix: false));
        Assert.Equal(["g/s/p"], server.ServedRepositories);
        Assert.Throws<ArgumentException>(() => server.UrlOf("g/s/q"));
        Assert.Empty(server.Errors);
    }

    /// <summary>
    /// A host's folder for paths (2.2): each of <paramref name="folders"/> (relative, with <c>/</c>) a
    /// bare clone of a repository of two commits, and options that know octo's and ada's tokens,
    /// make every repository public or none (<paramref name="isPublic"/>), let nobody push, and record
    /// each name <c>BeforeRefsAsync</c> is called with.
    /// </summary>
    private static async Task<PathHost> PathHostAsync(bool isPublic, params string[] folders)
    {
        var repo = await TempRepo.CreateAsync("path-host");
        try
        {
            await repo.CommitFileAsync("README.md", "first\n", "First");
            await repo.CommitFileAsync("README.md", "second\n", "Second");
            var folder = repo.SiblingPath("host");
            foreach (var bare in folders)
            {
                await repo.GitAsync("clone", "--quiet", "--bare", "--no-hardlinks", repo.Path, Path.Combine([folder, .. bare.Split('/')]));
            }

            var listed = new List<string>();
            var options = new AuthRemoteHostOptions(
                new PingRoutes(),
                token => token switch { OctoToken => "octo", AdaToken => "ada", _ => null },
                _ => isPublic,
                (_, _) => false,
                BeforeRefsAsync: (name, _) =>
                {
                    lock (listed)
                    {
                        listed.Add(name);
                    }

                    return Task.CompletedTask;
                });
            return new PathHost(repo, folder, options, listed);
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private sealed record PathHost(TempRepo Repo, string Folder, AuthRemoteHostOptions Options, List<string> ListedNames) : IDisposable
    {
        /// <summary>The names <c>BeforeRefsAsync</c> was called with, in order.</summary>
        public IReadOnlyList<string> Listed
        {
            get
            {
                lock (ListedNames)
                {
                    return [.. ListedNames];
                }
            }
        }

        public void Dispose() => Repo.Dispose();
    }

    /// <summary>A GET with Basic <c>x-access-token:&lt;token&gt;</c>: its status, body and content type.</summary>
    private static async Task<(HttpStatusCode Status, string Body, IEnumerable<string> ContentType)> GetAsync(HttpClient client, string url, string token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        message.Headers.TryAddWithoutValidation("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + token)));
        using var response = await client.SendAsync(message, TestCancelled);
        var contentType = response.Content.Headers.TryGetValues("Content-Type", out var values) ? values.ToList() : [];
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestCancelled), contentType);
    }

    /// <summary>The status of a GET of <paramref name="target"/> sent byte for byte over a socket, so that no client normalises its path.</summary>
    private static async Task<int> RawStatusAsync(int port, string target)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, TestCancelled);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET {target} HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nConnection: close\r\n\r\n"), TestCancelled);
        using var reader = new StreamReader(stream, Encoding.ASCII);
        var statusLine = await reader.ReadLineAsync(TestCancelled) ?? throw new IOException("The server closed the connection without an answer.");
        return int.Parse(statusLine.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
    }
}
