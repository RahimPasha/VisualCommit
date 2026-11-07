using VisualCommit.App.Tests;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;
using Xunit;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// The repository browser for every signed-in provider as a view model (docs/phase-7b-design.md 11.4,
/// 11.6; D354), with a delegate per provider in place of the session's list: one provider without a
/// switch, two and three in D353's order with the asked one shown, one list per provider however often
/// shown, the filter kept and the selection cleared by a switch, a late answer for a provider not
/// shown, each provider's lines, Azure DevOps' <c>&lt;organization&gt;@</c> kept (ruling 15), Cancel and
/// the log line per provider.
/// </summary>
public sealed class CloneFromAccountDialogViewModelTests
{
    private static readonly HostingProvider[] Three = [HostingProvider.GitHub, HostingProvider.GitLab, HostingProvider.AzureDevOps];

    [Theory]
    [InlineData(HostingProvider.GitLab, "Clone from GitLab")]
    [InlineData(HostingProvider.AzureDevOps, "Clone from Azure DevOps")]
    [InlineData(HostingProvider.GitHub, "Clone from GitHub")]
    public async Task One_provider_has_no_switch_and_its_title(HostingProvider provider, string title)
    {
        var lists = new Lists();
        var dialog = new CloneFromGitHubDialogViewModel([provider], provider, lists.Run, new RecordingLog());
        Assert.Equal([provider], dialog.Providers);
        Assert.False(dialog.ShowsSwitch);
        Assert.Equal(title, dialog.Title);
        Assert.Equal([true], dialog.ProviderChoices.Select(choice => choice.IsSelected));

        var load = dialog.Start();
        Assert.Same(load, dialog.Load);
        Assert.Equal("Loading repositories…", dialog.StatusText);
        lists.Answer(provider, Ok(provider, "a"));
        await load;
        Assert.Equal([FullName(provider, "a")], dialog.Rows.Select(row => row.FullName));
        Assert.Equal([provider], lists.Asked);
    }

    [Fact]
    public void Two_and_three_are_in_d353_s_order_with_the_asked_one_shown()
    {
        var lists = new Lists();
        var two = new CloneFromGitHubDialogViewModel([HostingProvider.AzureDevOps, HostingProvider.GitLab], HostingProvider.AzureDevOps, lists.Run, new RecordingLog());
        Assert.Equal([HostingProvider.GitLab, HostingProvider.AzureDevOps], two.Providers);
        Assert.True(two.ShowsSwitch);
        Assert.Equal(HostingProvider.AzureDevOps, two.Provider);
        Assert.Equal("Clone from Azure DevOps", two.Title);
        Assert.Equal(
            [("GitLab", "CloneFromGitHubProviderGitLab", false), ("Azure DevOps", "CloneFromGitHubProviderAzureDevOps", true)],
            two.ProviderChoices.Select(choice => (choice.Name, choice.AutomationId, choice.IsSelected)));

        var three = new CloneFromGitHubDialogViewModel([HostingProvider.AzureDevOps, HostingProvider.GitHub, HostingProvider.GitLab], HostingProvider.GitLab, lists.Run, new RecordingLog());
        Assert.Equal(Three, three.Providers);
        Assert.Equal(["GitHub", "GitLab", "Azure DevOps"], three.ProviderChoices.Select(choice => choice.Name));
        Assert.Equal("CloneFromGitHubProviderGitHub", three.ProviderChoices[0].AutomationId);
        Assert.Equal("Clone from GitLab", three.Title);
        Assert.Empty(lists.Asked);

        Assert.Throws<ArgumentException>(() => new CloneFromGitHubDialogViewModel([HostingProvider.GitLab], HostingProvider.AzureDevOps, lists.Run, new RecordingLog()));
    }

    [Fact]
    public async Task Each_list_is_asked_once_however_often_it_shows()
    {
        var lists = new Lists();
        var dialog = new CloneFromGitHubDialogViewModel(Three, HostingProvider.GitHub, lists.Run, new RecordingLog());
        var changed = new List<string?>();
        dialog.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var first = dialog.Start();
        Assert.Same(first, dialog.Start());
        Assert.Equal([HostingProvider.GitHub], lists.Asked);

        dialog.ProviderChoices[1].ShowCommand.Execute(null);
        Assert.Equal(HostingProvider.GitLab, dialog.Provider);
        Assert.Contains(nameof(dialog.Title), changed);
        Assert.Equal("Clone from GitLab", dialog.Title);
        Assert.Equal([false, true, false], dialog.ProviderChoices.Select(choice => choice.IsSelected));
        dialog.Provider = HostingProvider.AzureDevOps;
        dialog.Provider = HostingProvider.GitHub;
        dialog.Provider = HostingProvider.GitLab;
        dialog.Provider = HostingProvider.AzureDevOps;
        Assert.Equal(Three, lists.Asked);

        foreach (var provider in Three)
        {
            lists.Answer(provider, Ok(provider, "x"));
        }

        await Task.WhenAll(Three.Select(dialog.LoadOf));
        dialog.Provider = HostingProvider.GitHub;
        dialog.Provider = HostingProvider.GitLab;
        Assert.Equal(Three, lists.Asked);
        Assert.Equal([FullName(HostingProvider.GitLab, "x")], dialog.Rows.Select(row => row.FullName));
    }

    [Fact]
    public async Task A_switch_keeps_the_filter_and_clears_the_selection()
    {
        var lists = new Lists();
        var dialog = new CloneFromGitHubDialogViewModel([HostingProvider.GitLab, HostingProvider.AzureDevOps], HostingProvider.GitLab, lists.Run, new RecordingLog());
        _ = dialog.Start();
        lists.Answer(HostingProvider.GitLab, Ok(HostingProvider.GitLab, "web-app", "web-app-fork", "tool"));
        await dialog.Load;
        dialog.Filter = "web";
        dialog.Selected = dialog.Rows[1];
        Assert.True(dialog.ConfirmCommand.CanExecute(null));

        dialog.Provider = HostingProvider.AzureDevOps;
        Assert.Equal("web", dialog.Filter);
        Assert.Null(dialog.Selected);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
        Assert.Empty(dialog.Rows);
        Assert.Equal("Loading repositories…", dialog.StatusText);
        lists.Answer(HostingProvider.AzureDevOps, Ok(HostingProvider.AzureDevOps, "mobile-app", "web-site"));
        await dialog.Load;
        Assert.Equal([FullName(HostingProvider.AzureDevOps, "web-site")], dialog.Rows.Select(row => row.FullName));

        dialog.Provider = HostingProvider.GitLab;
        Assert.Equal("web", dialog.Filter);
        Assert.Null(dialog.Selected);
        Assert.Equal([FullName(HostingProvider.GitLab, "web-app"), FullName(HostingProvider.GitLab, "web-app-fork")], dialog.Rows.Select(row => row.FullName));
        Assert.Null(dialog.StatusText);
    }

    [Fact]
    public async Task A_late_answer_for_a_provider_not_shown_changes_nothing_on_screen()
    {
        var lists = new Lists();
        var dialog = new CloneFromGitHubDialogViewModel([HostingProvider.GitLab, HostingProvider.AzureDevOps], HostingProvider.GitLab, lists.Run, new RecordingLog());
        _ = dialog.Start();
        lists.Answer(HostingProvider.GitLab, Ok(HostingProvider.GitLab, "web-app"));
        await dialog.Load;
        dialog.Provider = HostingProvider.AzureDevOps;
        dialog.Provider = HostingProvider.GitLab;
        var changed = new List<string?>();
        dialog.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var rows = new List<string>();
        dialog.Rows.CollectionChanged += (_, _) => rows.Add("changed");

        lists.Answer(HostingProvider.AzureDevOps, Failed(HostingProvider.AzureDevOps, HostingFailureKind.ServerError, "boom"));
        await dialog.LoadOf(HostingProvider.AzureDevOps);
        Assert.Empty(changed);
        Assert.Empty(rows);
        Assert.Equal("Clone from GitLab", dialog.Title);
        Assert.Null(dialog.ErrorText);
        Assert.Equal([FullName(HostingProvider.GitLab, "web-app")], dialog.Rows.Select(row => row.FullName));

        dialog.Provider = HostingProvider.AzureDevOps;
        Assert.Equal("Azure DevOps had a problem. Try again later.", dialog.ErrorText);
        Assert.Null(dialog.StatusText);
        Assert.Empty(dialog.Rows);
        Assert.Equal(2, lists.Asked.Count);
    }

    [Fact]
    public async Task Each_provider_s_lines_gitlab_s_cap_none_for_azure_devops_and_sso_never()
    {
        var lists = new Lists();
        var dialog = new CloneFromGitHubDialogViewModel(Three, HostingProvider.GitLab, lists.Run, new RecordingLog());
        _ = dialog.Start();
        lists.Answer(HostingProvider.GitLab, Ok(HostingProvider.GitLab, moreExist: true, sso: true, names: "a"));
        await dialog.Load;
        Assert.Equal("Only the 1,000 most recently active repositories are listed.", dialog.MoreText);
        Assert.Null(dialog.SsoText);
        Assert.False(dialog.ShowsSso);

        dialog.Provider = HostingProvider.AzureDevOps;
        lists.Answer(HostingProvider.AzureDevOps, Ok(HostingProvider.AzureDevOps, moreExist: true, sso: true, names: "a"));
        await dialog.Load;
        Assert.Null(dialog.MoreText);
        Assert.Null(dialog.SsoText);

        dialog.Provider = HostingProvider.GitHub;
        lists.Answer(HostingProvider.GitHub, Ok(HostingProvider.GitHub, moreExist: true, sso: true, names: "a"));
        await dialog.Load;
        Assert.Equal("Only the 1,000 most recently updated repositories are listed.", dialog.MoreText);
        Assert.Equal("Some organisations' repositories are hidden until the token is authorised for single sign-on.", dialog.SsoText);

        dialog.Provider = HostingProvider.GitLab;
        Assert.Equal("Only the 1,000 most recently active repositories are listed.", dialog.MoreText);
        Assert.Null(dialog.SsoText);

        var empty = new CloneFromGitHubDialogViewModel([HostingProvider.AzureDevOps], HostingProvider.AzureDevOps, (_, _) => Task.FromResult(HostingResult<RepositoryList>.Ok(new RepositoryList([], false, false))), new RecordingLog());
        await empty.Start();
        Assert.Equal("No repositories to list", empty.StatusText);
    }

    [Theory]
    [InlineData("http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app", "http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app")]
    [InlineData("http://fernhill-labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app", "http://fernhill-labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app")]
    [InlineData("http://Fernhill-Labs:x@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app", "http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app")]
    [InlineData("http://alice@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app", "http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app")]
    [InlineData("http://Fernhill-Labs-x@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app", "http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app")]
    [InlineData("http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app", "http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app")]
    public void Azure_devops_keeps_the_organization_as_the_user_and_removes_any_other(string cloneUrl, string expected)
    {
        var row = GitHubRepositoryRow.Of(Info(new HostedRepository(HostingProvider.AzureDevOps, "127.0.0.1:47397", "Fernhill-Labs/Mobile App", "mobile-app"), cloneUrl));
        Assert.Equal(expected, row.CloneUrl);
        Assert.Equal("Fernhill-Labs/Mobile App/mobile-app", row.FullName);
    }

    [Theory]
    [InlineData(HostingProvider.GitLab, "http://northfield@127.0.0.1:47398/northfield/platform/web-app.git")]
    [InlineData(HostingProvider.GitHub, "http://octo@127.0.0.1:47395/octo/web-app.git")]
    public void Another_provider_s_user_part_is_removed_as_7a_s(HostingProvider provider, string cloneUrl)
    {
        var owner = provider == HostingProvider.GitLab ? "northfield/platform" : "octo";
        var row = GitHubRepositoryRow.Of(Info(new HostedRepository(provider, "127.0.0.1:1", owner, "web-app"), cloneUrl));
        Assert.DoesNotContain("@", row.CloneUrl, StringComparison.Ordinal);
        Assert.Equal($"{owner}/web-app", row.FullName);
    }

    [Fact]
    public void Cancel_cancels_every_load()
    {
        var lists = new Lists();
        var log = new RecordingLog();
        var dialog = new CloneFromGitHubDialogViewModel(Three, HostingProvider.GitHub, lists.Run, log);
        _ = dialog.Start();
        dialog.Provider = HostingProvider.GitLab;
        dialog.Provider = HostingProvider.AzureDevOps;
        Assert.All(Three, provider => Assert.False(lists.TokenOf(provider).IsCancellationRequested));

        dialog.CancelCommand.Execute(null);
        Assert.True(dialog.IsClosed);
        Assert.All(Three, provider => Assert.True(lists.TokenOf(provider).IsCancellationRequested));
        Assert.DoesNotContain(log.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Choose_cancels_another_provider_s_load_still_running()
    {
        // GitLab listed and shown; Azure DevOps' list held, observing its token as the session's does.
        var log = new RecordingLog();
        var asked = new List<HostingProvider>();
        var gitLab = new TaskCompletionSource<HostingResult<RepositoryList>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = new TaskCompletionSource<HostingResult<RepositoryList>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken heldToken = default;
        Task<HostingResult<RepositoryList>> Run(HostingProvider provider, CancellationToken token)
        {
            asked.Add(provider);
            if (provider == HostingProvider.GitLab)
            {
                return gitLab.Task;
            }

            heldToken = token;
            token.Register(() => held.TrySetCanceled(token));
            return held.Task;
        }

        var dialog = new CloneFromGitHubDialogViewModel([HostingProvider.GitLab, HostingProvider.AzureDevOps], HostingProvider.GitLab, Run, log);
        _ = dialog.Start();
        gitLab.SetResult(Ok(HostingProvider.GitLab, "web-app", "tool"));
        await dialog.Load;
        dialog.Provider = HostingProvider.AzureDevOps;
        dialog.Provider = HostingProvider.GitLab;
        Assert.False(dialog.LoadOf(HostingProvider.AzureDevOps).IsCompleted);
        Assert.False(heldToken.IsCancellationRequested);

        var row = dialog.Rows[0];
        dialog.Selected = row;
        dialog.ConfirmCommand.Execute(null);

        Assert.True(dialog.IsClosed);
        Assert.Equal(row.CloneUrl, await dialog.Result);
        Assert.True(heldToken.IsCancellationRequested, "Choose left Azure DevOps' load running.");
        await dialog.LoadOf(HostingProvider.AzureDevOps).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(dialog.LoadOf(HostingProvider.AzureDevOps).IsCompletedSuccessfully);
        Assert.Equal([HostingProvider.GitLab, HostingProvider.AzureDevOps], asked);
        Assert.Empty(log.Entries);
        Assert.Equal([FullName(HostingProvider.GitLab, "web-app"), FullName(HostingProvider.GitLab, "tool")], dialog.Rows.Select(each => each.FullName));
    }

    [Theory]
    [InlineData(HostingProvider.GitLab, HostingFailureKind.ServerError, "500 Internal Server Error", "FAKE-GL-000001", null, "Listing GitLab repositories failed: ServerError: 500 Internal Server Error (request FAKE-GL-000001)", "GitLab had a problem. Try again later.")]
    [InlineData(HostingProvider.AzureDevOps, HostingFailureKind.BadCredentials, "Unauthorized", "a1b2", "Fernhill-Labs", "Listing Azure DevOps repositories failed: BadCredentials: Unauthorized (request a1b2)", "Azure DevOps refused the token of Fernhill-Labs. Sign in to Azure DevOps in Settings, Accounts.")]
    [InlineData(HostingProvider.GitHub, HostingFailureKind.ServerError, "HTTP 502", null, null, "Listing GitHub repositories failed: ServerError: HTTP 502", "GitHub had a problem. Try again later.")]
    public async Task A_failure_is_logged_once_with_its_provider_and_worded_in_its_provider_s_words(HostingProvider provider, HostingFailureKind kind, string message, string? requestId, string? login, string line, string shown)
    {
        var log = new RecordingLog();
        var asked = new List<HostingProvider>();
        var dialog = new CloneFromGitHubDialogViewModel(
            [provider],
            provider,
            (each, _) => Task.FromResult(HostingResult<RepositoryList>.Fail(new HostingFailure(kind, message, RequestId: requestId) { Provider = each })),
            log,
            each =>
            {
                asked.Add(each);
                return login;
            });
        await dialog.Start();

        Assert.Equal(shown, dialog.ErrorText);
        Assert.Equal([provider], asked);
        var entry = Assert.Single(log.Entries);
        Assert.Equal((LogLevel.Warning, line), (entry.Level, entry.Message));
        Assert.Empty(dialog.Rows);
        Assert.Null(dialog.StatusText);
    }

    [Fact]
    public async Task An_exception_is_logged_with_the_provider_s_name()
    {
        var log = new RecordingLog();
        var dialog = new CloneFromGitHubDialogViewModel([HostingProvider.GitLab], HostingProvider.GitLab, (_, _) => Task.FromException<HostingResult<RepositoryList>>(new InvalidOperationException("boom")), log);
        await dialog.Start();
        Assert.Equal("The repositories could not be loaded.", dialog.ErrorText);
        var entry = Assert.Single(log.Entries);
        Assert.Equal((LogLevel.Error, "Listing GitLab repositories failed."), (entry.Level, entry.Message));
    }

    private static string FullName(HostingProvider provider, string name) => provider switch
    {
        HostingProvider.GitLab => $"group/sub/{name}",
        HostingProvider.AzureDevOps => $"Fernhill-Labs/Mobile App/{name}",
        _ => $"octo/{name}",
    };

    private static HostingResult<RepositoryList> Ok(HostingProvider provider, params string[] names) => Ok(provider, false, false, names);

    private static HostingResult<RepositoryList> Ok(HostingProvider provider, bool moreExist, bool sso, params string[] names) =>
        HostingResult<RepositoryList>.Ok(new RepositoryList([.. names.Select(name => Info(Repository(provider, name), null))], moreExist, sso));

    private static HostingResult<RepositoryList> Failed(HostingProvider provider, HostingFailureKind kind, string message) =>
        HostingResult<RepositoryList>.Fail(new HostingFailure(kind, message) { Provider = provider });

    private static HostedRepository Repository(HostingProvider provider, string name) => provider switch
    {
        HostingProvider.GitLab => new(provider, "127.0.0.1:3", "group/sub", name),
        HostingProvider.AzureDevOps => new(provider, "127.0.0.1:2", "Fernhill-Labs/Mobile App", name),
        _ => new(provider, "127.0.0.1:1", "octo", name),
    };

    private static RepositoryInfo Info(HostedRepository repository, string? cloneUrl) =>
        new(
            repository,
            "main",
            true,
            false,
            false,
            null,
            new Uri(cloneUrl ?? $"http://{repository.Host}/{Uri.EscapeDataString(repository.Owner).Replace("%2F", "/", StringComparison.Ordinal)}/{repository.Name}.git"),
            new Uri($"http://{repository.Host}/{repository.Name}"),
            DateTimeOffset.MinValue,
            true,
            null,
            null);

    /// <summary>One held answer per provider; records each provider asked and its cancellation.</summary>
    private sealed class Lists
    {
        private readonly Dictionary<HostingProvider, TaskCompletionSource<HostingResult<RepositoryList>>> _answers = [];
        private readonly Dictionary<HostingProvider, CancellationToken> _tokens = [];

        public List<HostingProvider> Asked { get; } = [];

        public CancellationToken TokenOf(HostingProvider provider) => _tokens[provider];

        public Task<HostingResult<RepositoryList>> Run(HostingProvider provider, CancellationToken token)
        {
            Asked.Add(provider);
            _tokens[provider] = token;
            return Source(provider).Task;
        }

        public void Answer(HostingProvider provider, HostingResult<RepositoryList> answer) => Source(provider).SetResult(answer);

        private TaskCompletionSource<HostingResult<RepositoryList>> Source(HostingProvider provider)
        {
            if (!_answers.TryGetValue(provider, out var source))
            {
                source = new TaskCompletionSource<HostingResult<RepositoryList>>(TaskCreationOptions.RunContinuationsAsynchronously);
                _answers[provider] = source;
            }

            return source;
        }
    }
}
