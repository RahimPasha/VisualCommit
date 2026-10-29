using VisualCommit.App.Tests;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core;
using VisualCommit.Hosting;
using VisualCommit.Hosting.Tokens;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// "Add GitHub account" as a view model (docs/phase-7a-design.md 8.4, 8.8): Sign in's enablement,
/// the local refusal without a request (D298), each failure's text, a sign-in, a failed save and its
/// one "Close" (ruling 19), Cancel during the check (the held request ends, nothing changes), the
/// link's address (D295) and the no-keychain line (D253). The session is the real one over the fake
/// of GitHub in process, with a <see cref="FakeTokenStore"/>.
/// </summary>
public sealed class SignInDialogViewModelTests
{
    private const string TokenPage = "http://127.0.0.1:1/settings/personal-access-tokens/new?name=VisualCommit&contents=write&pull_requests=write";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sign_in_is_enabled_while_the_trimmed_token_is_not_empty_and_no_check_runs()
    {
        await using var setup = await Setup.StartAsync();
        var dialog = setup.Dialog();
        await dialog.Opened;

        Assert.False(dialog.ShowsNoKeychain);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
        dialog.Token = "   ";
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
        dialog.Token = " x ";
        Assert.True(dialog.ConfirmCommand.CanExecute(null));
        Assert.Equal("Sign in", dialog.ConfirmText);
        Assert.True(dialog.ShowsCancel);
        Assert.True(dialog.IsBoxEnabled);
        Assert.Equal("Add GitHub account", dialog.Title);
        Assert.Equal(["probe"], setup.Tokens.Calls);
    }

    [Theory]
    [InlineData("github_pat_FAKE octo-1")]
    [InlineData("github_pat_FAKE-octo-1\nx")]
    [InlineData("github_pat_​FAKE-octo-1")]
    [InlineData("﻿github_pat_FAKE-octo-1")]
    [InlineData("github_pat_FAKE-öcto-1")]
    public async Task A_token_outside_printable_ascii_is_refused_without_a_request(string token)
    {
        await using var setup = await Setup.StartAsync();
        var dialog = setup.Dialog();
        await dialog.Opened;

        dialog.Token = token;
        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal("The token can contain only letters, digits and punctuation, without spaces or line breaks.", dialog.ErrorText);
        Assert.Empty(setup.Fake.Requests);
        Assert.False(dialog.IsClosed);
        Assert.True(dialog.IsBoxEnabled);
        Assert.Equal(token, dialog.Token);
        Assert.Null(setup.Session.Account);
    }

    [Theory]
    [InlineData(HostingFailureKind.BadCredentials, "Bad credentials", "GitHub refused the token: Bad credentials.")]
    [InlineData(HostingFailureKind.BadCredentials, "Bad credentials.", "GitHub refused the token: Bad credentials.")]
    [InlineData(HostingFailureKind.Network, "Could not reach GitHub.", "Could not reach GitHub.")]
    [InlineData(HostingFailureKind.Network, "GitHub did not answer within 30 seconds.", "GitHub did not answer within 30 seconds.")]
    [InlineData(HostingFailureKind.Forbidden, "Resource not accessible by personal access token", "GitHub answered: Resource not accessible by personal access token.")]
    [InlineData(HostingFailureKind.ServerError, "Server Error.", "GitHub answered: Server Error.")]
    [InlineData(HostingFailureKind.RateLimited, "API rate limit exceeded", "GitHub answered: API rate limit exceeded.")]
    public void Each_failure_has_its_text(HostingFailureKind kind, string message, string expected) =>
        Assert.Equal(expected, Texts.SignInFailed(new HostingFailure(kind, message)));

    [Fact]
    public async Task A_refused_token_shows_GitHub_s_words_with_the_box_enabled_and_its_text_kept_for_selecting()
    {
        await using var setup = await Setup.StartAsync();
        var dialog = setup.Dialog();
        var focus = new List<string>();
        dialog.FocusRequested += (_, id) => focus.Add(id);
        await dialog.Opened;

        dialog.Token = "github_pat_FAKE-nobody-9";
        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal("GitHub refused the token: Bad credentials.", dialog.ErrorText);
        Assert.False(dialog.IsChecking);
        Assert.True(dialog.IsBoxEnabled);
        Assert.Equal("github_pat_FAKE-nobody-9", dialog.Token);
        Assert.Equal(["SignInToken"], focus);
        Assert.False(dialog.IsClosed);
        Assert.Null(setup.Session.Account);
        Assert.Single(setup.Fake.Requests);
        Assert.Equal(["probe"], setup.Tokens.Calls);
    }

    [Fact]
    public async Task A_sign_in_closes_the_dialog_with_the_account_and_clears_the_box()
    {
        await using var setup = await Setup.StartAsync();
        var dialog = setup.Dialog();
        await dialog.Opened;

        dialog.Token = "  " + setup.Served.OctoToken + " ";
        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.True(dialog.IsClosed);
        var status = await dialog.Result;
        Assert.NotNull(status);
        Assert.Equal(AccountState.SignedIn, status.State);
        Assert.Equal(TokenPlace.Keychain, status.Place);
        Assert.Equal("octo", status.Account.Login);
        Assert.Equal(string.Empty, dialog.Token);
        Assert.Null(dialog.ErrorText);
        Assert.Equal(["probe", "save github:127.0.0.1:1:octo"], setup.Tokens.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failed_save_signs_in_and_leaves_one_close_that_closes_it_signed_in(bool byEscape)
    {
        await using var setup = await Setup.StartAsync();
        var dialog = setup.Dialog();
        var focus = new List<string>();
        dialog.FocusRequested += (_, id) => focus.Add(id);
        await dialog.Opened;
        setup.Tokens.FailNextSave(TokenStoreReasons.Locked);

        dialog.Token = setup.Served.OctoToken;
        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.False(dialog.IsClosed);
        Assert.Equal(
            "Signed in as octo, but the token could not be saved in this computer's keychain: the keychain stayed locked. VisualCommit keeps it until it closes and asks for it again at the next start.",
            dialog.SaveFailedText);
        Assert.True(dialog.IsSaveFailed);
        Assert.False(dialog.IsBoxEnabled);
        Assert.Equal(string.Empty, dialog.Token);
        Assert.False(dialog.ShowsCancel);
        Assert.Equal("Close", dialog.ConfirmText);
        Assert.True(dialog.ConfirmCommand.CanExecute(null));
        Assert.Null(dialog.ErrorText);
        Assert.Equal(["ConfirmButton"], focus);
        Assert.Equal(AccountState.SignedIn, setup.Session.Account?.State);
        Assert.Equal(AccountNoteKind.SaveFailed, setup.Session.Account?.NoteKind);

        if (byEscape)
        {
            dialog.HandleEscape();
        }
        else
        {
            await dialog.ConfirmCommand.ExecuteAsync(null);
        }

        Assert.True(dialog.IsClosed);
        var status = await dialog.Result;
        Assert.Equal(AccountState.SignedIn, status?.State);
        Assert.Equal(TokenPlace.Session, status?.Place);
    }

    [Fact]
    public async Task Cancel_during_the_check_cancels_its_request_and_nothing_changes()
    {
        await using var setup = await Setup.StartAsync();
        var dialog = setup.Dialog();
        await dialog.Opened;
        using var hold = setup.Fake.HoldNext("user");

        dialog.Token = setup.Served.OctoToken;
        var checking = dialog.ConfirmCommand.ExecuteAsync(null);
        await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        Assert.True(dialog.IsChecking);
        Assert.False(dialog.IsBoxEnabled);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
        Assert.True(dialog.ShowsCancel);

        dialog.CancelCommand.Execute(null);

        Assert.True(dialog.IsClosed);
        Assert.Null(await dialog.Result);

        // The held request ends with the dialog's cancellation, before the hold is released.
        await checking.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        Assert.Null(setup.Session.Account);
        Assert.False(File.Exists(Path.Combine(setup.Data.Path, "accounts.json")));
        Assert.Equal(["probe"], setup.Tokens.Calls);
    }

    [Fact]
    public async Task The_link_opens_GitHub_s_page_for_a_new_token()
    {
        await using var setup = await Setup.StartAsync();
        var dialog = setup.Dialog();
        await dialog.Opened;

        await dialog.OpenTokenPageCommand.ExecuteAsync(null);

        var launch = Assert.Single(setup.Launcher.Requests);
        Assert.Equal(FileLaunchKind.OpenUrl, launch.Kind);
        Assert.Equal(TokenPage, launch.FullPath);
        Assert.Empty(setup.Fake.Requests);
    }

    [Fact]
    public async Task Without_a_keychain_the_dialog_says_so_from_its_opening()
    {
        await using var setup = await Setup.StartAsync(new FakeTokenStore { Available = false });
        var dialog = setup.Dialog();

        await dialog.Opened;

        Assert.True(dialog.ShowsNoKeychain);
        Assert.Equal(
            "No keychain was found on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again at the next start.",
            Texts.NoKeychain);
        Assert.Equal(["probe"], setup.Tokens.Calls);
    }

    /// <summary>A hosting session over the scenario "hosting" served in process, its data folder, keychain, log and launcher.</summary>
    private sealed class Setup : IAsyncDisposable
    {
        private Setup(ServedHosting served, FakeTokenStore tokens)
        {
            Served = served;
            Tokens = tokens;
            Session = new HostingSession(new AppPaths(Data.Path), new HostingOptions(served.GitHub.Endpoints, served.GitHub.CreateHandler), tokens, Log, action => action(), TimeProvider.System);
        }

        public ServedHosting Served { get; }

        public FakeGitHub Fake => Served.GitHub;

        public FakeTokenStore Tokens { get; }

        public TempDirectory Data { get; } = new("sign-in dialog");

        public RecordingLog Log { get; } = new();

        public FakeFileLauncher Launcher { get; } = new();

        public HostingSession Session { get; }

        public static async Task<Setup> StartAsync(FakeTokenStore? tokens = null)
        {
            var copy = await Scenarios.HostingAsync();
            try
            {
                var served = await Scenarios.ServeHostingAsync(copy, inProcess: true, ct: Cancelled, ownsCopy: true);
                var setup = new Setup(served, tokens ?? new FakeTokenStore());
                await setup.Session.StartAsync(Cancelled);
                return setup;
            }
            catch
            {
                copy.Dispose();
                throw;
            }
        }

        public SignInDialogViewModel Dialog() => new(Session, Launcher, Log);

        public async ValueTask DisposeAsync()
        {
            Session.Dispose();
            try
            {
                foreach (var token in new[] { Served.OctoToken, Served.OctoClassicToken, Served.AdaToken })
                {
                    TokenTripwire.AssertAbsent(token, [Data.Path], null);
                    Assert.DoesNotContain(Log.Messages, message => message.Contains(token, StringComparison.Ordinal));
                }

                Assert.Empty(Fake.Errors);
            }
            finally
            {
                await Served.DisposeAsync();
                Data.Dispose();
            }
        }
    }
}
