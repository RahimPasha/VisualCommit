using System.Globalization;
using VisualCommit.App.Tests;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;
using Xunit;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.CloneTexts;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The repository browser "Clone from GitHub" as a view model (docs/phase-7a-design.md 9.4, 9.6;
/// D301, D338), with a delegate in place of the session's list: loading, the rows and each detail's
/// form, the filter, "Choose", the cap's and single sign-on's lines, failures in
/// <c>HostingTexts.Describe</c>'s words, the empty list, and Cancel and Esc.
/// </summary>
public sealed class CloneFromGitHubDialogViewModelTests
{
    private static readonly DateTimeOffset Updated = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Loading_shows_its_line_and_no_rows_and_choose_is_disabled()
    {
        var list = new HeldList();
        var dialog = new CloneFromGitHubDialogViewModel(list.Run, new RecordingLog());
        var load = dialog.Start();

        Assert.Same(load, dialog.Start());
        Assert.Same(load, dialog.Load);
        Assert.False(load.IsCompleted);
        Assert.Equal(1, list.Calls);
        Assert.Equal("Clone from GitHub", dialog.Title);
        Assert.Equal("Loading repositories…", dialog.StatusText);
        Assert.True(dialog.ShowsStatus);
        Assert.False(dialog.ShowsError || dialog.ShowsMore || dialog.ShowsSso);
        Assert.Empty(dialog.Rows);
        Assert.Null(dialog.Selected);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_rows_keep_github_s_order_with_their_flags_then_the_description()
    {
        var dialog = new CloneFromGitHubDialogViewModel(_ => Task.FromResult(HostingResult<RepositoryList>.Ok(new RepositoryList(
        [
            Repository("octo", "hello", isPrivate: true),
            Repository("octo", "tool", isFork: true, description: "Small command-line tools."),
            Repository("octo", "website", isArchived: true),
            Repository("octo", "all", isPrivate: true, isFork: true, isArchived: true),
            Repository("octo", "demo", isPrivate: true, isFork: true, description: "A tiny demo"),
            Repository("octo", "described", description: "Two\r\nlines  and   spaces "),
            Repository("octo", "plain", description: "   "),
            Repository("acme", "secret", cloneUrl: "http://alice:pass@127.0.0.1:47395/acme/secret.git"),
        ], false, false))), new RecordingLog());
        await dialog.Start();

        Assert.Equal(
            [
                ("octo/hello", "Private"),
                ("octo/tool", "Fork · Small command-line tools."),
                ("octo/website", "Archived"),
                ("octo/all", "Private · Fork · Archived"),
                ("octo/demo", "Private · Fork · A tiny demo"),
                ("octo/described", "Two lines and spaces"),
                ("octo/plain", null),
                ("acme/secret", null),
            ],
            dialog.Rows.Select(row => (row.FullName, row.Detail)));
        Assert.Equal("http://127.0.0.1:47395/octo/hello.git", dialog.Rows[0].CloneUrl);
        Assert.Equal("http://127.0.0.1:47395/acme/secret.git", dialog.Rows[^1].CloneUrl);
        Assert.Equal("octo/tool\nFork · Small command-line tools.", dialog.Rows[1].ToolTip);
        Assert.Equal("octo/plain", dialog.Rows[6].ToolTip);
        Assert.Equal(dialog.Rows, dialog.AllRows);
        Assert.False(dialog.ShowsStatus || dialog.ShowsError || dialog.ShowsMore || dialog.ShowsSso);
        Assert.Null(dialog.Selected);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_filter_matches_the_name_and_the_detail_clears_a_hidden_selection_and_restores_the_order()
    {
        var dialog = await ListedAsync();
        dialog.Selected = dialog.Rows.Single(row => row.FullName == "octo/notes");
        Assert.True(dialog.ConfirmCommand.CanExecute(null));

        dialog.Filter = "PRIVATE";
        Assert.Equal(["octo/hello", "octo/notes"], Names(dialog));
        Assert.Equal("octo/notes", dialog.Selected?.FullName);

        dialog.Filter = "command-line";
        Assert.Equal(["octo/tool"], Names(dialog));
        Assert.Null(dialog.Selected);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));

        dialog.Filter = "web";
        Assert.Equal(["octo/website"], Names(dialog));

        dialog.Filter = "nothing like it";
        Assert.Empty(dialog.Rows);
        Assert.False(dialog.ShowsStatus);

        dialog.Filter = string.Empty;
        Assert.Equal(["octo/hello", "octo/tool", "octo/notes", "octo/website"], Names(dialog));
    }

    [Fact]
    public async Task A_filter_typed_while_loading_filters_the_rows_when_they_come()
    {
        var list = new HeldList();
        var dialog = new CloneFromGitHubDialogViewModel(list.Run, new RecordingLog());
        var load = dialog.Start();
        dialog.Filter = "private";
        Assert.Empty(dialog.Rows);
        Assert.Equal("Loading repositories…", dialog.StatusText);

        list.Answer(HostingResult<RepositoryList>.Ok(Scenario()));
        await load;
        Assert.Equal(["octo/hello", "octo/notes"], Names(dialog));
        Assert.Null(dialog.StatusText);
    }

    [Fact]
    public async Task Choose_answers_the_selected_row_s_plain_clone_url()
    {
        var dialog = new CloneFromGitHubDialogViewModel(
            _ => Task.FromResult(HostingResult<RepositoryList>.Ok(new RepositoryList(
                [Repository("octo", "hello", isPrivate: true, cloneUrl: "http://x-access-token:secret@127.0.0.1:47395/octo/hello.git")], false, false))),
            new RecordingLog());
        await dialog.Start();
        dialog.Selected = dialog.Rows[0];
        dialog.ConfirmCommand.Execute(null);

        Assert.True(dialog.IsClosed);
        Assert.Equal("http://127.0.0.1:47395/octo/hello.git", await dialog.Result);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task The_cap_s_and_single_sign_on_s_lines_show_under_the_list(bool moreExist, bool someHiddenBySso)
    {
        var dialog = new CloneFromGitHubDialogViewModel(
            _ => Task.FromResult(HostingResult<RepositoryList>.Ok(new RepositoryList(Scenario().Repositories, moreExist, someHiddenBySso))),
            new RecordingLog());
        await dialog.Start();

        Assert.Equal(4, dialog.Rows.Count);
        Assert.Equal(moreExist ? "Only the 1,000 most recently updated repositories are listed." : null, dialog.MoreText);
        Assert.Equal(someHiddenBySso ? "Some organisations' repositories are hidden until the token is authorised for single sign-on." : null, dialog.SsoText);
        Assert.Equal((moreExist, someHiddenBySso), (dialog.ShowsMore, dialog.ShowsSso));
        Assert.False(dialog.ShowsStatus || dialog.ShowsError);
    }

    [Fact]
    public async Task An_empty_list_says_so()
    {
        var dialog = new CloneFromGitHubDialogViewModel(_ => Task.FromResult(HostingResult<RepositoryList>.Ok(new RepositoryList([], false, false))), new RecordingLog());
        await dialog.Start();

        Assert.Equal("No repositories to list", dialog.StatusText);
        Assert.Empty(dialog.Rows);
        Assert.False(dialog.ShowsError || dialog.ShowsMore || dialog.ShowsSso);
        dialog.Filter = "x";
        Assert.Equal("No repositories to list", dialog.StatusText);
    }

    [Fact]
    public async Task Failures_are_worded_by_describe_with_the_signed_in_login_and_logged_once()
    {
        var retryAt = new DateTimeOffset(2026, 10, 29, 21, 5, 0, TimeSpan.Zero);
        (HostingFailure Failure, string Expected)[] cases =
        [
            (new(HostingFailureKind.BadCredentials, "Bad credentials", RequestId: "FAKE-0001"), OperationTexts.TokenRefused("octo")),
            (new(HostingFailureKind.Network, "Could not reach GitHub."), "Could not reach GitHub."),
            (new(HostingFailureKind.RateLimited, "API rate limit exceeded", RetryAt: retryAt, RequestId: "FAKE-0002"),
                $"GitHub's rate limit was reached. Try again after {retryAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)}."),
            (new(HostingFailureKind.ServerError, "Server Error", RequestId: "FAKE-0003"), "GitHub had a problem. Try again later."),
        ];
        foreach (var (failure, expected) in cases)
        {
            var log = new RecordingLog();
            var dialog = new CloneFromGitHubDialogViewModel(_ => Task.FromResult(HostingResult<RepositoryList>.Fail(failure)), log, () => "octo");
            await dialog.Start();

            Assert.Equal(expected, dialog.ErrorText);
            Assert.True(dialog.ShowsError);
            Assert.False(dialog.ShowsStatus || dialog.ShowsMore || dialog.ShowsSso);
            Assert.Empty(dialog.Rows);
            Assert.False(dialog.ConfirmCommand.CanExecute(null));
            var entry = Assert.Single(log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            var request = failure.RequestId is null ? string.Empty : $" (request {failure.RequestId})";
            Assert.Equal($"Listing GitHub repositories failed: {failure.Kind}: {failure.Message}{request}", entry.Message);
        }

        var signedOut = new CloneFromGitHubDialogViewModel(
            _ => Task.FromResult(HostingResult<RepositoryList>.Fail(new HostingFailure(HostingFailureKind.BadCredentials, "Bad credentials"))),
            new RecordingLog());
        await signedOut.Start();
        Assert.Equal("Bad credentials. Sign in to GitHub in Settings, Accounts.", signedOut.ErrorText);
    }

    [Fact]
    public async Task An_unexpected_exception_is_the_app_s_sentence_and_an_error_in_the_log()
    {
        var log = new RecordingLog();
        var dialog = new CloneFromGitHubDialogViewModel(_ => throw new InvalidOperationException("broken"), log);
        await dialog.Start();

        Assert.Equal("The repositories could not be loaded.", dialog.ErrorText);
        Assert.Null(dialog.StatusText);
        Assert.Empty(dialog.Rows);
        var entry = Assert.Single(log.Entries);
        Assert.Equal((LogLevel.Error, "Listing GitHub repositories failed."), (entry.Level, entry.Message));
        Assert.IsType<InvalidOperationException>(entry.Exception);

        var thrownLater = new CloneFromGitHubDialogViewModel(_ => Task.FromException<HostingResult<RepositoryList>>(new HttpRequestException("gone")), new RecordingLog());
        await thrownLater.Start();
        Assert.Equal("The repositories could not be loaded.", thrownLater.ErrorText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_and_esc_cancel_the_list_s_token_and_a_late_answer_changes_nothing(bool escape)
    {
        var list = new HeldList();
        var log = new RecordingLog();
        var dialog = new CloneFromGitHubDialogViewModel(list.Run, log, () => "octo");
        var load = dialog.Start();
        dialog.Filter = "o";
        if (escape)
        {
            dialog.HandleEscape();
        }
        else
        {
            dialog.CancelCommand.Execute(null);
        }

        Assert.True(dialog.IsClosed);
        Assert.True(list.Token.IsCancellationRequested);
        Assert.Null(await dialog.Result);

        list.Answer(HostingResult<RepositoryList>.Ok(Scenario()));
        await load;
        Assert.Empty(dialog.Rows);
        Assert.Equal("Loading repositories…", dialog.StatusText);
        Assert.Null(dialog.ErrorText);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task A_list_cancelled_by_the_closing_dialog_shows_and_logs_nothing()
    {
        var log = new RecordingLog();
        var dialog = new CloneFromGitHubDialogViewModel(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return HostingResult<RepositoryList>.Ok(Scenario());
        }, log);
        var load = dialog.Start();
        dialog.CancelCommand.Execute(null);
        await load;

        Assert.Empty(log.Entries);
        Assert.Null(dialog.ErrorText);
        Assert.Null(await dialog.Result);
    }

    private static async Task<CloneFromGitHubDialogViewModel> ListedAsync()
    {
        var dialog = new CloneFromGitHubDialogViewModel(_ => Task.FromResult(HostingResult<RepositoryList>.Ok(Scenario())), new RecordingLog());
        await dialog.Start();
        return dialog;
    }

    private static List<string> Names(CloneFromGitHubDialogViewModel dialog) => [.. dialog.Rows.Select(row => row.FullName)];

    /// <summary>The scenario's four repositories of octo as GitHub lists them (part B's facts, with ruling 11's description).</summary>
    private static RepositoryList Scenario() => new(
    [
        Repository("octo", "hello", isPrivate: true),
        Repository("octo", "tool", isFork: true, description: "Small command-line tools."),
        Repository("octo", "notes", isPrivate: true),
        Repository("octo", "website", isArchived: true),
    ], false, false);

    private static RepositoryInfo Repository(string owner, string name, bool isPrivate = false, bool isFork = false, bool isArchived = false, string? description = null, string? cloneUrl = null) =>
        new(
            new HostedRepository(HostingProvider.GitHub, "127.0.0.1:47395", owner, name),
            "main",
            isPrivate,
            isFork,
            isArchived,
            description,
            new Uri(cloneUrl ?? $"http://127.0.0.1:47395/{owner}/{name}.git"),
            new Uri($"http://127.0.0.1:47395/{owner}/{name}"),
            Updated,
            true,
            null,
            null);

    /// <summary>A list that answers when the test says, keeping the token it was given.</summary>
    private sealed class HeldList
    {
        private readonly TaskCompletionSource<HostingResult<RepositoryList>> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public CancellationToken Token { get; private set; }

        public Task<HostingResult<RepositoryList>> Run(CancellationToken token)
        {
            Calls++;
            Token = token;
            return _answer.Task;
        }

        public void Answer(HostingResult<RepositoryList> result) => _answer.SetResult(result);
    }
}
