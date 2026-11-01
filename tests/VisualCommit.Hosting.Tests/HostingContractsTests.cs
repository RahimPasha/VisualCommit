using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.GitHub;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// WP0's contracts with 7a's behaviour (docs/phase-7b-design.md 1.4, 1.5, 1.10): GitHub's provider
/// members that moved out of the App, the session's provider members while it holds GitHub alone,
/// the null session, and the records' new members. No request leaves the process.
/// </summary>
public sealed class HostingContractsTests
{
    private static readonly HostedRepository Hello = new(HostingProvider.GitHub, "127.0.0.1:1", "octo", "hello");

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public void GitHubs_provider_answers_the_moved_members_as_7a_did()
    {
        using var provider = new GitHubProvider(GitHubEndpoints.Local(1), HostingOptions.Off.CreateHandler, new ListLog(), TimeProvider.System);
        var account = new AccountKey(HostingProvider.GitHub, "127.0.0.1:1", "octo");

        Assert.Equal(("127.0.0.1:1", "http://127.0.0.1:1/"), (provider.Host, provider.WebBase.AbsoluteUri));
        Assert.Equal(new Uri(new Uri("http://127.0.0.1:1/"), OperationTexts.AccountTexts.TokenPage), provider.TokenPage(null));
        Assert.Equal(provider.TokenPage(null), provider.TokenPage("Fernhill-Labs"));
        Assert.Equal(OperationTexts.AccountTexts.TokenPage, GitHubProvider.TokenPagePath);
        Assert.Equal("http://127.0.0.1:1/octo/hello/pulls", provider.PullRequestsPage(Hello).AbsoluteUri);
        Assert.Equal(new GitCoverage(account, new Uri("http://127.0.0.1:1/")), provider.CoverageOf(account));
        Assert.True(provider.Covers(Hello, account));
        Assert.True(provider.Covers(Hello with { Host = "127.0.0.1:1", Owner = "Other" }, account));
        Assert.False(provider.Covers(Hello with { Host = "127.0.0.1:2" }, account));
        Assert.False(provider.Covers(Hello with { Provider = HostingProvider.GitLab }, account));
        Assert.Equal("refs/pull/7/head", provider.PullRequestHeadRef(PullRequestOf(7)));
        Assert.False(provider.SourceCanExist("refs/pull/fix/head"));
    }

    [Fact]
    public void The_7a_form_of_Select_is_the_coverage_on_the_web_base()
    {
        var account = new AccountKey(HostingProvider.GitHub, "127.0.0.1:1", "octo");
        var webBase = new Uri("http://127.0.0.1:1/");
        IReadOnlyList<VisualCommit.Core.Git.RemoteEffectiveUrls> remotes =
        [
            new("origin", ["http://127.0.0.1:1/octo/hello.git", "http://octo@127.0.0.1:1/octo/two.git"], ["http://127.0.0.1:2/octo/hello.git"]),
        ];

        var seven = CredentialBinding.Select(remotes, webBase, account);
        var coverage = CredentialBinding.Select(remotes, new GitCoverage(account, webBase));

        Assert.Equal(seven, coverage);
        Assert.Equal(["http://127.0.0.1:1/octo/hello.git", "http://127.0.0.1:1/octo/two.git"], coverage.Select(binding => binding.Url));
        Assert.All(coverage, binding => Assert.Null(binding.SharedOriginOwner));
    }

    [Fact]
    public async Task The_session_holds_GitHub_alone_until_WP_S()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        var session = setup.Session;
        var providers = new List<HostingProvider>();
        await setup.Thread.RunAsync(() => session.AccountChanged += (_, e) => providers.Add(e.Provider));

        Assert.False(session.IsOff);
        Assert.Equal([HostingProvider.GitHub], session.Providers);
        Assert.Equal((true, false, false), (session.IsOn(HostingProvider.GitHub), session.IsOn(HostingProvider.GitLab), session.IsOn(HostingProvider.AzureDevOps)));
        Assert.Same(session.GitHub, session.ProviderOf(HostingProvider.GitHub));
        Assert.Null(session.ProviderOf(HostingProvider.GitLab));
        Assert.Null(session.AccountOf(HostingProvider.GitHub));

        var status = await setup.SignInAsync();
        await setup.Thread.IdleAsync();

        Assert.Same(status, session.AccountOf(HostingProvider.GitHub));
        Assert.Null(session.AccountOf(HostingProvider.AzureDevOps));
        Assert.Equal([HostingProvider.GitHub], providers);
        var sentBefore = setup.Sent.Count;

        var gitLab = await session.SignInAsync(new SignInRequest(HostingProvider.GitLab, "glpat-FAKE-ada-1"), Cancelled);
        var azure = await session.ListRepositoriesAsync(HostingProvider.AzureDevOps, Cancelled);
        var signedOut = await session.SignOutAsync(HostingProvider.GitLab, Cancelled);
        session.SetUseForGit(HostingProvider.AzureDevOps, false);

        Assert.Equal((HostingFailureKind.Validation, HostingProvider.GitLab), (gitLab.Failure!.Kind, gitLab.Failure.Provider));
        Assert.Null(gitLab.Status);
        Assert.Equal((HostingFailureKind.Validation, HostingProvider.AzureDevOps), (azure.Failure!.Kind, azure.Failure.Provider));
        Assert.Null(signedOut);
        Assert.Equal(sentBefore, setup.Sent.Count);
        Assert.Same(status, session.AccountOf(HostingProvider.GitHub));
        Assert.True(session.BindsGit);
    }

    [Fact]
    public async Task The_off_session_has_no_provider_and_its_shims_go_through_the_provider_members()
    {
        IHostingSession session = NullHostingSession.Instance;

        Assert.True(session.IsOff);
        Assert.Empty(session.Providers);
        Assert.All(HostingProviders.InAccountsOrder, provider =>
        {
            Assert.False(session.IsOn(provider));
            Assert.Null(session.ProviderOf(provider));
            Assert.Null(session.AccountOf(provider));
        });
        Assert.Null(session.GitHub);
        Assert.Null(session.Account);
        var signIn = await session.SignInAsync("ghp_FAKE-octo-1", Cancelled);
        Assert.Equal(("GitHub is off.", HostingProvider.GitHub), (signIn.Failure!.Message, signIn.Failure.Provider));
        var list = await session.ListRepositoriesAsync(HostingProvider.GitLab, Cancelled);
        Assert.Equal(("GitLab is off.", HostingProvider.GitLab), (list.Failure!.Message, list.Failure.Provider));
        Assert.Equal(PullRequestsState.NoHostedRemote, NullRepositoryHosting.Instance.State.PullRequests);
    }

    [Fact]
    public void A_sign_in_request_never_shows_its_token()
    {
        var request = new SignInRequest(HostingProvider.AzureDevOps, "azdo_FAKE-Fernhill-Labs-1", "Fernhill-Labs");

        Assert.Equal("SignInRequest(AzureDevOps, ***, Fernhill-Labs)", request.ToString());
        Assert.Equal("SignInRequest(GitHub, ***)", new SignInRequest(HostingProvider.GitHub, "ghp_FAKE-octo-1").ToString());
    }

    [Fact]
    public void The_records_new_members_default_to_7as_values()
    {
        var failure = new HostingFailure(HostingFailureKind.Validation, "Validation Failed");
        var nested = new HostedRepository(HostingProvider.GitLab, "gitlab.com", "group/sub", "project");

        Assert.Equal((HostingProvider.GitHub, PullRequestRefusal.None), (failure.Provider, failure.Refusal));
        Assert.Equal(("group/sub/project", "group"), (nested.FullName, nested.OwnerRoot));
        Assert.Equal(("octo/hello", "octo"), (Hello.FullName, Hello.OwnerRoot));
        Assert.Null(new HostingAccount(new AccountKey(HostingProvider.GitHub, "github.com", "octo"), "Octo", 1, null, null, true).UserId);
        Assert.Null(new NewPullRequest("t", "b", "h", null, "main", false).HeadRepository);
    }

    [Fact]
    public void The_request_remotes_are_the_listed_providers_and_hosts()
    {
        var azure = new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "Fernhill-Labs/Mobile App", "mobile-app");
        var remotes = new Dictionary<string, HostedRepository>(StringComparer.Ordinal)
        {
            ["origin"] = azure,
            ["same-organization"] = azure with { Owner = "fernhill-labs/Other", Name = "other" },
            ["other-organization"] = azure with { Owner = "Other-Org/Shared", Name = "shared" },
            ["github"] = Hello,
        };
        var state = RepositoryHostingState.NoHostedRemote with { PullRequests = PullRequestsState.Loaded, Remotes = remotes, Repository = azure };

        Assert.Equal(["origin", "same-organization"], state.RequestRemotes.Keys.Order(StringComparer.Ordinal));
        Assert.Empty((state with { PullRequests = PullRequestsState.OtherOrganization }).RequestRemotes);
        Assert.Empty((state with { Repository = null }).RequestRemotes);
        Assert.Equal(["github"], (state with { Repository = Hello }).RequestRemotes.Keys);
        Assert.Empty(RepositoryHostingState.NoHostedRemote.RequestRemotes);
    }

    private static PullRequest PullRequestOf(int number) =>
        new(number, "Fix", "octo", false, new Uri("http://127.0.0.1:1/octo/hello/pull/" + number), "fix", new string('a', 40), Hello, "main", Hello, DateTimeOffset.UnixEpoch);
}
