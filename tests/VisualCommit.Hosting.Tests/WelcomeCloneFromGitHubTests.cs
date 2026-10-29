using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The clone form's "From GitHub…" and the clone's binding (docs/phase-7a-design.md 9.3, 9.6; D261,
/// D302, D331, D334): absent with GitHub off and signed out, the URL box then as wide as the name
/// box; shown after a sign-in, gone after a sign-out, disabled while cloning; "Choose" fills the URL
/// and the folder name and keeps a typed name; a clone of the private octo/hello through the app's
/// helper without a dialog and without a credential in the clone; another user's URL, and a global
/// rule that rewrites to one, open git's credentials dialog as before; signed out, no configuration
/// read. The fake of GitHub serves on a free port (design 16.3), so that git reaches it.
/// </summary>
public sealed class WelcomeCloneFromGitHubTests
{
    private const string ConfigRead = "git config --show-scope --show-origin -z --list";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [AvaloniaFact]
    public async Task The_button_is_absent_with_github_off_and_signed_out_and_the_url_box_is_then_as_wide_as_the_name_box()
    {
        using (var data = new TempDirectory("welcome clone off"))
        using (var off = ShellDriver.Start(data.Path))
        {
            await OpenFormAsync(off);
            AssertNoButton(off);
        }

        await using var served = await ServeAsync();
        using var signedOut = new TempDirectory("welcome clone signed out");
        var app = ShellDriver.Start(signedOut.Path, gitHub: served.GitHub);
        try
        {
            await OpenFormAsync(app);
            AssertNoButton(app);
            Assert.False(Welcome(app).ShowsGitHubButton);
        }
        finally
        {
            app.Dispose();
        }

        End(app, signedOut, served);
    }

    [AvaloniaFact]
    public async Task It_shows_after_a_sign_in_and_goes_after_a_sign_out()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone sign in");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenFormAsync(app);
            await app.SignInAsync(served.OctoToken, Cancelled);
            await app.WaitForAsync(() => Button(app).IsEffectivelyVisible, "From GitHub… to show");
            Assert.True(Button(app).IsEffectivelyEnabled);
            Assert.Equal("From GitHub…", Avalonia.Automation.AutomationProperties.GetName(Button(app)));
            Assert.Null(ToolTip.GetTip(Button(app)));
            var url = app.BoundsOf(Box(app, "CloneUrlBox"));
            var button = app.BoundsOf(Button(app));
            Assert.Equal(6, button.X - url.Right, 1.0);
            Assert.Equal(app.BoundsOf(Box(app, "CloneNameBox")).Right, button.Right, 1.0);

            Assert.Null(await app.Session.Services.Hosting.SignOutAsync(Cancelled));
            await app.WaitForAsync(() => !Button(app).IsEffectivelyVisible, "From GitHub… to go");
            AssertNoButton(app);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Choose_fills_the_url_and_the_folder_name_and_a_typed_name_is_kept()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone choose");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenSignedInFormAsync(app, served);
            var welcome = Welcome(app);

            await ChooseAsync(app, "octo/tool");
            Assert.Equal(Scenarios.HostingUrl("octo/tool", served.GitHub.Port), welcome.CloneUrl);
            Assert.Equal("tool", welcome.CloneName);
            Assert.Null(welcome.ErrorText);

            app.ReplaceText("CloneNameBox", "mine");
            await ChooseAsync(app, "octo/notes");
            Assert.Equal(Scenarios.HostingUrl("octo/notes", served.GitHub.Port), welcome.CloneUrl);
            Assert.Equal("mine", welcome.CloneName);

            // Cancel leaves the form as it was.
            app.Click(Button(app));
            var browser = await app.WaitForDialogAsync<CloneFromGitHubDialogViewModel>();
            browser.CancelCommand.Execute(null);
            app.Settle();
            Assert.Equal(Scenarios.HostingUrl("octo/notes", served.GitHub.Port), welcome.CloneUrl);
            Assert.True(welcome.IsCloneFormOpen);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_clone_of_the_private_hello_takes_the_account_s_token_without_a_dialog_and_keeps_none()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone hello");
        using var clones = new TempDirectory("clones");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenSignedInFormAsync(app, served);
            await ChooseAsync(app, "octo/hello");
            app.TypeInto("CloneParentBox", clones.Path);
            var mark = await app.QuietCallMarkAsync();
            var log = app.LogMark();
            var gitRequests = served.GitHub.GitRequests.Count;

            app.Click(app.FindByAutomationId("CloneStartButton"));
            var repository = await app.WaitForRepositoryAsync();
            await app.WaitForWatchersQuietAsync();

            Assert.Equal(Path.Combine(clones.Path, "hello"), repository.Path);
            Assert.DoesNotContain(app.Session.Services.DialogHost.Open, dialog => dialog is PromptDialogViewModel);
            var calls = app.GitCalls().Where(call => call.Sequence > mark).Select(call => call.CommandText).ToList();
            var read = calls.IndexOf(ConfigRead);
            var clone = calls.IndexOf($"git clone --progress -- {Scenarios.HostingUrl("octo/hello", served.GitHub.Port)} hello");
            Assert.True(read >= 0 && clone > read, "The clone did not read git's configuration first:\n" + string.Join('\n', calls));
            var lines = app.LogLinesSince(log);
            Assert.Contains(lines, line => line.Contains($"hello: Clone: git's credential get for 127.0.0.1:{served.GitHub.Port} answered from the account.", StringComparison.Ordinal));
            Assert.DoesNotContain(lines, line => line.Contains("hello: Clone: git asks (", StringComparison.Ordinal));
            Assert.DoesNotContain(lines, line => line.Contains("git's configuration could not be read", StringComparison.Ordinal));

            var requests = served.GitHub.GitRequests.Skip(gitRequests).Where(request => request.Path.StartsWith("/octo/hello.git/", StringComparison.Ordinal)).ToList();
            Assert.Contains(requests, request => request is { GivenUserName: "x-access-token", Status: 200 });
            Assert.DoesNotContain(requests, request => request.GivenUserName is not null && request.GivenUserName != "x-access-token");
            Assert.DoesNotContain(requests, request => request is { GivenUserName: not null, Status: 401 });
        }
        finally
        {
            app.Dispose();
        }

        var config = await File.ReadAllTextAsync(Path.Combine(clones.Path, "hello", ".git", "config"), Cancelled);
        Assert.Contains($"url = {Scenarios.HostingUrl("octo/hello", served.GitHub.Port)}", config, StringComparison.Ordinal);
        Assert.DoesNotContain("credential", config, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[http", config, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[url", config, StringComparison.OrdinalIgnoreCase);
        End(app, data, served, clones.Path);
    }

    [AvaloniaFact]
    public async Task A_url_naming_another_user_opens_git_s_credentials_dialog_and_the_form_is_disabled_meanwhile()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone alice");
        using var clones = new TempDirectory("clones");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenSignedInFormAsync(app, served);
            var url = Scenarios.HostingUrl("octo/hello", served.GitHub.Port).Replace("http://", "http://alice@", StringComparison.Ordinal);
            await AssertAsksAsync(app, url, clones.Path, readsConfiguration: false);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, clones.Path);
    }

    [AvaloniaFact]
    public async Task A_global_rule_that_rewrites_the_url_to_another_user_leaves_it_unbound()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone rule");
        using var clones = new TempDirectory("clones");
        var web = $"http://127.0.0.1:{served.GitHub.Port}/";
        ShellDriver.SeedGlobalGitConfig(data.Path, $"[url \"http://alice@127.0.0.1:{served.GitHub.Port}/\"]\n\tinsteadOf = {web}\n");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenSignedInFormAsync(app, served);
            await AssertAsksAsync(app, Scenarios.HostingUrl("octo/hello", served.GitHub.Port), clones.Path, readsConfiguration: true);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, clones.Path);
    }

    [AvaloniaFact]
    public async Task Signed_out_a_clone_reads_no_configuration()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone signed out");
        using var clones = new TempDirectory("clones");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenFormAsync(app);
            app.TypeInto("CloneUrlBox", Scenarios.HostingUrl("octo/tool", served.GitHub.Port));
            app.TypeInto("CloneParentBox", clones.Path);
            var mark = await app.QuietCallMarkAsync();
            app.Click(app.FindByAutomationId("CloneStartButton"));
            await app.WaitForRepositoryAsync();
            await app.WaitForWatchersQuietAsync();

            var calls = app.GitCalls().Where(call => call.Sequence > mark).Select(call => call.CommandText).ToList();
            Assert.Contains($"git clone --progress -- {Scenarios.HostingUrl("octo/tool", served.GitHub.Port)} tool", calls);
            Assert.DoesNotContain(ConfigRead, calls);
            Assert.Empty(served.GitHub.Requests);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, clones.Path);
    }

    /// <summary>Starts a clone of <paramref name="url"/> that git asks the user's credentials for, checks the form while the dialog is open, then cancels it.</summary>
    private static async Task AssertAsksAsync(ShellDriver app, string url, string parent, bool readsConfiguration)
    {
        var welcome = Welcome(app);
        app.ReplaceText("CloneUrlBox", url);
        app.TypeInto("CloneParentBox", parent);
        var mark = await app.QuietCallMarkAsync();
        var log = app.LogMark();
        app.Click(app.FindByAutomationId("CloneStartButton"));
        var dialog = await app.WaitForDialogAsync<CredentialsViewModel>();
        Assert.True(welcome.IsCloning);
        Assert.False(Button(app).IsEffectivelyEnabled);
        Assert.False(welcome.BrowseGitHubCommand.CanExecute(null));
        Assert.True(Button(app).IsEffectivelyVisible);

        dialog.CancelCommand.Execute(null);
        await app.WaitForAsync(() => !welcome.IsCloning, "the clone to end");
        app.Settle();
        Assert.Equal("Clone cancelled.", welcome.ErrorText);
        Assert.True(Button(app).IsEffectivelyEnabled);
        var calls = app.GitCalls().Where(call => call.Sequence > mark).Select(call => call.CommandText).ToList();
        Assert.Equal(readsConfiguration, calls.Contains(ConfigRead));
        var lines = app.LogLinesSince(log);
        Assert.DoesNotContain(lines, line => line.Contains("answered from the account", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Clone: git asks (", StringComparison.Ordinal));
    }

    private static async Task OpenSignedInFormAsync(ShellDriver app, ServedHosting served)
    {
        await OpenFormAsync(app);
        await app.SignInAsync(served.OctoToken, Cancelled);
        await app.WaitForAsync(() => Button(app).IsEffectivelyVisible, "From GitHub… to show");
    }

    private static async Task OpenFormAsync(ShellDriver app)
    {
        app.Click(app.FindByAutomationId("CloneRepoButton"));
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneUrlBox", "the focus in the clone form's URL box");
    }

    /// <summary>Opens the browser with "From GitHub…", waits for its rows, selects <paramref name="fullName"/> and clicks Choose.</summary>
    private static async Task ChooseAsync(ShellDriver app, string fullName)
    {
        app.Click(Button(app));
        var browser = await app.WaitForDialogAsync<CloneFromGitHubDialogViewModel>();
        await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's list");
        app.Settle();
        Assert.IsType<CloneFromGitHubDialogView>(app.TopDialogView);
        browser.Selected = browser.Rows.Single(row => row.FullName == fullName);
        app.Settle();
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
        app.Settle();
    }

    private static void AssertNoButton(ShellDriver app)
    {
        var button = app.Find<Button>("CloneFromGitHubButton");
        Assert.False(button.IsVisible);
        Assert.Equal(app.BoundsOf(Box(app, "CloneNameBox")).Width, app.BoundsOf(Box(app, "CloneUrlBox")).Width, 1.0);
        Assert.Equal(app.BoundsOf(Box(app, "CloneNameBox")).X, app.BoundsOf(Box(app, "CloneUrlBox")).X, 1.0);
    }

    private static WelcomeViewModel Welcome(ShellDriver app) => app.Session.Shell.ActiveTab.Welcome;

    private static Button Button(ShellDriver app) =>
        app.Find<Button>("CloneFromGitHubButton");

    private static TextBox Box(ShellDriver app, string id) => (TextBox)app.FindByAutomationId(id);

    /// <summary>The scenario "hosting" served on a free port (design 16.3: git and recognition need a port).</summary>
    private static async Task<ServedHosting> ServeAsync()
    {
        var copy = await Scenarios.HostingAsync();
        try
        {
            return await Scenarios.ServeHostingAsync(copy, port: 0, ct: Cancelled, ownsCopy: true);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }

    /// <summary>After the app closed: no token in the data folder, the copy or <paramref name="clones"/>, nor in any git call; nothing refused or wrong at the fake.</summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting served, string? clones = null)
    {
        string[] folders = clones is null ? [data.Path, served.Hello.Path] : [data.Path, served.Hello.Path, clones];
        foreach (var token in new[] { served.OctoToken, served.OctoClassicToken, served.AdaToken })
        {
            TokenTripwire.AssertAbsent(token, folders, app.Session.GitCalls);
        }

        Assert.Equal(0, app.RefusedRequests.Count);
        Assert.Empty(served.GitHub.Errors);
    }
}

