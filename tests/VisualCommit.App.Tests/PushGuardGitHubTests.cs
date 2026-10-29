using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The push guard on a remote recognised as GitHub (ruling 8, D279, docs/phase-7a-design.md 4.7):
/// beside the default line, <c>+refs/pull/*/head:refs/remotes/origin/pr/*</c> makes
/// <c>origin/pr/fix-typo</c> look fetched from two refs, but on GitHub
/// <c>refs/pull/fix-typo/head</c> cannot exist, so <c>pr/fix-typo</c> is pushed; <c>pr/1</c> keeps
/// phase 6's refusal, and so does <c>pr/fix-typo</c> on a remote not recognised; the two-namespace
/// refusal, whose candidates are branches, stays on GitHub too.
/// </summary>
public class PushGuardGitHubTests
{
    private const string FixTypoRefused = "Pushing pr/fix-typo to origin/pr/fix-typo would stop every fetch of origin: origin/pr/fix-typo is fetched from more than one ref of origin.";

    [Fact]
    public async Task On_github_a_branch_named_pr_slash_a_word_is_pushed()
    {
        using var repo = await FixTypoAsync();
        var stub = await RecognisingOriginAsync(repo);
        using var app = await OperationsHarness.OpenAsync(repo, prompts: null, hosting: stub);
        var head = await repo.HeadAsync();

        await app.Operations.PushBranchAsync("refs/heads/pr/fix-typo");
        await app.WaitForIdleAsync();

        Assert.Empty(app.Dialogs.Errors);
        Assert.Equal(head, await RemoteHeadAsync(repo, "refs/heads/pr/fix-typo"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task On_a_remote_not_recognised_the_same_push_is_refused_as_in_phase_6()
    {
        using var repo = await FixTypoAsync();
        var stub = new HostingSessionStub(new Uri("https://github.com/"), "octocat", null);
        using var app = await OperationsHarness.OpenAsync(repo, prompts: null, hosting: stub);
        var before = await RemoteHeadAsync(repo, "refs/heads/pr/fix-typo");
        app.Dialogs.CloseError();

        await app.Operations.PushBranchAsync("refs/heads/pr/fix-typo");
        await app.WaitForIdleAsync();

        Assert.Equal(("Push failed", FixTypoRefused), (app.Dialogs.Errors[^1].Title, app.Dialogs.Errors[^1].Summary));
        Assert.Equal(before, await RemoteHeadAsync(repo, "refs/heads/pr/fix-typo"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task On_github_a_branch_named_pr_slash_a_number_is_refused_with_todays_text()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();
        await repo.GitAsync("branch", "--quiet", "--track", "pr/1", "refs/remotes/origin/pr/1");
        var stub = await RecognisingOriginAsync(repo);
        using var app = await OperationsHarness.OpenAsync(repo, prompts: null, hosting: stub);
        app.Dialogs.CloseError();

        await app.Operations.PushBranchAsync("refs/heads/pr/1");
        await app.WaitForIdleAsync();

        Assert.Equal(
            ("Push failed", "Pushing pr/1 to origin/pr/1 would stop every fetch of origin: origin/pr/1 is fetched from more than one ref of origin."),
            (app.Dialogs.Errors[^1].Title, app.Dialogs.Errors[^1].Summary));
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/pr/1"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task On_github_a_new_branch_that_two_lines_would_fetch_into_one_ref_is_still_refused()
    {
        // The two-namespace refusal (0.2): origin/trunk is fetched from refs/heads/main and, through
        // the default line, from refs/heads/trunk once that exists. Both candidates are branches,
        // which ruling 8 never leaves out, so recognising origin changes nothing.
        using var repo = await Scenarios.RenamingRefspecAsync();
        await repo.GitAsync("config", "--add", "remote.origin.fetch", "refs/heads/main:refs/remotes/origin/trunk");
        await repo.GitAsync("fetch", "--quiet", "origin");
        await repo.GitAsync("branch", "--quiet", "trunk");
        var stub = await RecognisingOriginAsync(repo);
        using var app = await OperationsHarness.OpenAsync(repo, prompts: null, hosting: stub);
        app.Dialogs.Push(remote: "origin").CloseError();

        await app.Operations.PushBranchAsync("refs/heads/trunk");
        await app.WaitForIdleAsync();

        Assert.Equal(
            ("Push failed", "Pushing trunk to origin/trunk would stop every fetch of origin: origin/trunk is fetched from more than one ref of origin."),
            (app.Dialogs.Errors[^1].Title, app.Dialogs.Errors[^1].Summary));
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git push", StringComparison.Ordinal));
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/trunk"));
        app.AssertScriptFollowed();
    }

    /// <summary>
    /// Phase 6's renaming-refspec scenario (origin with GitHub's pull-request line beside the
    /// default one) with <c>pr/fix-typo</c> published on origin, its tracking ref fetched, and one
    /// more local commit to push.
    /// </summary>
    private static async Task<TempRepo> FixTypoAsync()
    {
        var repo = await Scenarios.RenamingRefspecAsync();
        await repo.GitAsync("switch", "--quiet", "--create", "pr/fix-typo");
        await repo.GitAsync("push", "--quiet", "--set-upstream", "origin", "refs/heads/pr/fix-typo:refs/heads/pr/fix-typo");
        await repo.CommitFileAsync("typo.txt", "fixed\n", "Fix a typo");
        return repo;
    }

    private static async Task<HostingSessionStub> RecognisingOriginAsync(TempRepo repo)
    {
        var stub = new HostingSessionStub(new Uri("https://github.com/"), "octocat", null);
        stub.GitHubUrls.Add((await repo.GitAsync("config", "--get", "remote.origin.url")).StandardOutput.Trim());
        return stub;
    }

    private static async Task<string?> RemoteHeadAsync(TempRepo repo, string remoteRef)
    {
        var listed = (await repo.GitAsync("ls-remote", "origin", remoteRef)).StandardOutput.Trim();
        return listed.Length == 0 ? null : listed.Split('\t')[0];
    }
}
