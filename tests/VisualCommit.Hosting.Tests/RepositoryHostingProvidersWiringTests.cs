using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The hosting of three providers in a repository's view model with real git (docs/phase-7b-design.md
/// 9.7, 9.9, 9.10; D290, D322, D356, D277): the app started through <see cref="ShellDriver"/> with
/// "hosting7b"'s fakes on port 0 (<see cref="Scenarios.ServeHosting7bAsync"/>). An Azure DevOps
/// <c>origin</c> and a GitLab <c>upstream</c> both bound, one <c>^url\.</c> read per refs read and one
/// Debug line per list; GitLab alone signed in, the Azure DevOps fake asked nothing; a succeeded fetch
/// refreshing the listed provider's list alone. Another organization's repository sends nothing after
/// the sign-in's requests and stays unbound, and its fetch sends git no account's token: no
/// <c>x-access-token</c> user and no "answered from the account." line until git's dialog is
/// cancelled. On 0.4's lagged list.
/// </summary>
public sealed class RepositoryHostingProvidersWiringTests
{
    private static readonly Regex RulesRead = new(@"^git config --null --get-regexp \^url\\\.", RegexOptions.CultureInvariant);

    private static readonly Regex MobileAppListed = new("mobile-app: listed \\d+ open pull requests of Fernhill-Labs/Mobile App/mobile-app$", RegexOptions.CultureInvariant);

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [AvaloniaFact]
    public async Task An_azure_devops_origin_and_a_gitlab_upstream_are_both_bound_and_a_fetch_refreshes_the_listed_providers_list_alone()
    {
        await using var served = await ServeAsync();
        await served.MobileApp.GitAsync("remote", "add", "upstream", Scenarios.GitLabUrl(Scenarios.Hosting7bWebApp, served.GitLab.Port));
        using var data = new TempDirectory("providers wiring");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.MobileApp.Path], gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        var repository = await app.WaitForRepositoryAsync();
        var folder = repository.Repository.WorkingDirectory;
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.SignedOut, "the section signed out");
        Assert.Equal(0, RulesReads(app, folder));

        // GitLab alone: the listed repository is Azure DevOps', whose fake is asked nothing.
        await app.SignInAsync(HostingProvider.GitLab, served.GitLabToken, cancellationToken: Cancelled);
        await app.WaitForAsync(() => repository.Hosting.Bindings.Count > 0 && repository.IsWatcherQuiet, "GitLab's binding");
        await app.WaitForWatchersQuietAsync();
        Assert.Equal(PullRequestsState.SignedOut, repository.HostingState.PullRequests);
        Assert.Empty(served.AzureDevOps.Requests);
        var gitLabKey = repository.Hosting.Bindings[0].Account;
        Assert.Equal(HostingProvider.GitLab, gitLabKey.Provider);

        await app.SignInAsync(HostingProvider.AzureDevOps, served.AzureDevOpsToken, served.Organization, Cancelled);
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.Loaded && repository.IsWatcherQuiet, "Azure DevOps' list");
        await app.WaitForWatchersQuietAsync();
        await app.WaitForLogLineAsync(MobileAppListed);
        var bindings = repository.Hosting.Bindings;
        Assert.True(
            bindings.Any(binding => binding.Account.Provider == HostingProvider.AzureDevOps && string.Equals(binding.SharedOriginOwner, served.Organization, StringComparison.OrdinalIgnoreCase)),
            string.Join("; ", bindings.Select(binding => $"{binding.Url} {binding.Account} {binding.SharedOriginOwner}")));
        Assert.Contains(new CredentialBinding(Scenarios.GitLabUrl(Scenarios.Hosting7bWebApp, served.GitLab.Port), gitLabKey), bindings);
        Assert.Equal(1, app.CountLogLines(MobileAppListed));

        // One read of the rules per read of the refs: a refresh, and any the watcher adds.
        var (rules, refs) = (RulesReads(app, folder), RefsReads(app, folder));
        await Dispatcher.UIThread.InvokeAsync(repository.RefreshAsync);
        await app.WaitForAsync(() => RulesReads(app, folder) > rules && repository.IsWatcherQuiet, "the refresh's rules read");
        await app.WaitForWatchersQuietAsync();
        await app.WaitForIdleAsync(repository);
        Assert.InRange(RulesReads(app, folder) - rules, 1, 3);
        Assert.Equal(RefsReads(app, folder) - refs, RulesReads(app, folder) - rules);

        // A succeeded fetch refreshes Azure DevOps' list alone; GitLab's fake is asked nothing.
        var (lists, gitLabRequests, logMark) = (Lists(served), served.GitLab.Requests.Count, app.LogMark());
        await Dispatcher.UIThread.InvokeAsync(() => repository.Operations.FetchAsync("origin"));
        await app.WaitForIdleAsync(repository);
        await app.WaitForAsync(() => Lists(served) > lists, "the list's refresh");
        await app.WaitForLogLineAsync(MobileAppListed, mark: logMark);
        await app.WaitForWatchersQuietAsync();
        Assert.Equal(gitLabRequests, served.GitLab.Requests.Count);
        Assert.Equal(lists + 1, Lists(served));
        Assert.Equal(2, app.CountLogLines(MobileAppListed));
        Assert.Equal(1, app.CountLogLines(MobileAppListed, logMark));
        Assert.Empty(served.GitHub.Requests);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Another_organizations_repository_sends_nothing_and_its_fetch_gives_git_no_accounts_token()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("providers wiring");
        var prompts = new ScriptedPromptHandler().Cancel().Cancel().Cancel();
        using var app = ShellDriver.StartWithTabs(data.Path, [served.Manifest.Path], promptHandler: prompts, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.SignedOut, "the section signed out");

        await app.SignInAsync(HostingProvider.AzureDevOps, served.AzureDevOpsToken, served.Organization, Cancelled);
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.OtherOrganization && repository.IsWatcherQuiet, "another organization's state");
        await app.WaitForWatchersQuietAsync();
        var signIn = served.AzureDevOps.Requests.Count;
        Assert.All(served.AzureDevOps.Requests, request => Assert.StartsWith("/Fernhill-Labs/", request.Path, StringComparison.Ordinal));
        Assert.Equal(("harbor-co/Docks/manifest", (PullRequestList?)null), (repository.HostingState.Repository!.FullName, repository.HostingState.List));
        Assert.Empty(repository.HostingState.RequestRemotes);
        Assert.Empty(repository.Hosting.Bindings);
        Assert.True(repository.Hosting.WantsUrlRewrites);

        await Dispatcher.UIThread.InvokeAsync(repository.RefreshAsync);
        await Dispatcher.UIThread.InvokeAsync(() => repository.Hosting.RefreshPullRequestsAsync(Cancelled));
        var mark = app.LogMark();
        await Dispatcher.UIThread.InvokeAsync(() => repository.Operations.FetchAsync("origin"));
        await app.WaitForIdleAsync(repository);
        await app.WaitForWatchersQuietAsync();

        Assert.Equal(signIn, served.AzureDevOps.Requests.Count);
        Assert.NotEmpty(prompts.Prompts);
        var gitRequests = served.AzureDevOps.GitRequests.Where(request => request.Path.Contains("harbor-co/Docks/_git/manifest", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(gitRequests);
        Assert.All(gitRequests, request =>
        {
            Assert.Null(request.UserName);
            Assert.True(request.GivenUserName is null or Scenarios.Hosting7bOtherOrganization, $"git gave the user {request.GivenUserName}.");
        });
        Assert.DoesNotContain(gitRequests, request => request.GivenUserName == GitCredential.UserName);
        Assert.DoesNotContain(app.LogLinesSince(mark), line => line.Contains("answered from the account.", StringComparison.Ordinal));
        Assert.Equal(PullRequestsState.OtherOrganization, repository.HostingState.PullRequests);
        End(app, data, served);
    }

    /// <summary>"hosting7b" served on port 0 (D265): the clones' remotes name the fakes' ports, so the app recognises them.</summary>
    private static async Task<ServedHosting7b> ServeAsync()
    {
        var copy = await Scenarios.Hosting7bAsync();
        try
        {
            return await Scenarios.ServeHosting7bAsync(copy, azureDevOpsPort: 0, gitLabPort: 0, gitHubPort: 0, ownsCopy: true, ct: Cancelled);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }

    // A call's folder is the one git ran in as git spells it (IGitRepository.WorkingDirectory):
    // /private/var/... on macOS where TMPDIR says /var/..., the long name on Windows where TEMP
    // may be a short 8.3 one; so the calls are matched with the tab's own repository's spelling.
    private static int RulesReads(ShellDriver app, string folder) =>
        app.GitCalls().Count(call => RulesRead.IsMatch(call.CommandText) && InFolder(call, folder));

    private static int RefsReads(ShellDriver app, string folder) =>
        app.GitCalls().Count(call => call.CommandText == "git symbolic-ref -q HEAD" && InFolder(call, folder));

    private static bool InFolder(GitCallRecord call, string folder) =>
        call.WorkingDirectory is { } directory
        && WorktreeKey.Comparer.Equals(Path.TrimEndingDirectorySeparator(directory), Path.TrimEndingDirectorySeparator(folder));

    private static int Lists(ServedHosting7b served) =>
        served.AzureDevOps.Requests.Count(request => request.Path.EndsWith("/_apis/git/repositories/mobile-app/pullrequests", StringComparison.OrdinalIgnoreCase));

    /// <summary>Closes the app, then checks that no token reached the data folder, the clones or a git call's command, input, output or error (the session's call log, taken before the app closes) and that no fake saw an error.</summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting7b served)
    {
        var calls = app.Session.GitCalls;
        app.Dispose();
        foreach (var token in served.Tokens)
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.MobileApp.Path, served.Manifest.Path, served.WebApp.Path], calls);
        }

        Assert.Empty(served.AzureDevOps.Errors);
        Assert.Empty(served.GitLab.Errors);
        Assert.Empty(served.GitHub.Errors);
    }
}
