using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// Browse and clone for every provider in the app (docs/phase-7b-design.md 11.3, 11.5, 11.6; D354,
/// D350, D331, D277, ruling 15): the clone form's button hidden, "From GitHub…" at 7a's width,
/// "From GitLab…", "From an account…" with two, following sign-outs; clones of GitLab's
/// <c>northfield/platform/web-app</c> and of Azure DevOps' <c>mobile-app</c> (<c>Mobile%20App</c>)
/// through the app's helper with no credentials dialog, the remote as chosen and no <c>credential</c>
/// key; a typed <c>.git</c>-less GitLab URL bound with its twin (through the fake's 301); harbor-co's
/// typed URL, with Fernhill-Labs signed in, asks git's credentials dialog and gets no token; each
/// palette command listed with its fake alone, signing in first, its browser on its provider. The app
/// starts through <see cref="ShellDriver"/> with real git and "hosting7b" served by the three fakes
/// on free ports (port 0, D265). On the lagged list (design 0.4).
/// </summary>
public sealed class CloneFromAccountTests
{
    private const string Organization = "Fernhill-Labs";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [AvaloniaFact]
    public async Task The_button_follows_the_signed_in_providers()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("clone from account button");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        double gitHubWidth;
        try
        {
            await app.WaitUntilReadyAsync();
            var welcome = OpenForm(app);
            AssertButton(app, null);

            await app.SignInAsync(HostingProvider.GitHub, served.GitHubToken, null, Cancelled);
            gitHubWidth = AssertButton(app, "From GitHub…");
            await app.SignInAsync(HostingProvider.GitLab, served.GitLabToken, null, Cancelled);
            AssertButton(app, "From an account…");
            await app.SignInAsync(HostingProvider.AzureDevOps, served.AzureDevOpsToken, Organization, Cancelled);
            AssertButton(app, "From an account…");
            await app.Session.Services.Hosting.SignOutAsync(HostingProvider.GitHub, Cancelled);
            await app.WaitForAsync(() => app.Session.Services.Hosting.AccountOf(HostingProvider.GitHub) is null, "GitHub's sign-out");
            AssertButton(app, "From an account…");
            await app.Session.Services.Hosting.SignOutAsync(HostingProvider.AzureDevOps, Cancelled);
            await app.WaitForAsync(() => app.Session.Services.Hosting.AccountOf(HostingProvider.AzureDevOps) is null, "Azure DevOps' sign-out");
            AssertButton(app, "From GitLab…");
            await app.Session.Services.Hosting.SignOutAsync(HostingProvider.GitLab, Cancelled);
            await app.WaitForAsync(() => app.Session.Services.Hosting.AccountOf(HostingProvider.GitLab) is null, "GitLab's sign-out");
            AssertButton(app, null);
            Assert.True(welcome.IsCloneFormOpen);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served);

        // 7a's harness: GitHub alone on; its button is as wide.
        using var alone = new TempDirectory("clone from account github alone");
        var seven = ShellDriver.Start(alone.Path, gitHub: served.GitHub);
        try
        {
            await seven.WaitUntilReadyAsync();
            await seven.SignInAsync(served.GitHubToken, Cancelled);
            OpenForm(seven);
            Assert.Equal(gitHubWidth, AssertButton(seven, "From GitHub…"), 3);
        }
        finally
        {
            seven.Dispose();
        }
    }

    [AvaloniaFact]
    public Task A_clone_of_gitlab_s_nested_web_app_takes_the_token_through_the_helper() =>
        CloneThroughTheHelperAsync(HostingProvider.GitLab, served => $"http://127.0.0.1:{served.GitLab.Port}/northfield/platform/web-app.git", "web-app");

    [AvaloniaFact]
    public Task A_clone_of_azure_devops_mobile_app_keeps_the_organization_as_its_user() =>
        CloneThroughTheHelperAsync(HostingProvider.AzureDevOps, served => $"http://{Organization}@127.0.0.1:{served.AzureDevOps.Port}/{Organization}/Mobile%20App/_git/mobile-app", "mobile-app");

    [AvaloniaFact]
    public Task A_typed_git_less_gitlab_url_is_bound_with_its_twin() =>
        CloneThroughTheHelperAsync(HostingProvider.GitLab, served => $"http://127.0.0.1:{served.GitLab.Port}/northfield/platform/web-app", "web-app", twin: true);

    private static async Task CloneThroughTheHelperAsync(HostingProvider provider, Func<ServedHosting7b, string> urlOf, string folder, bool twin = false)
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("clone from account");
        using var clones = new TempDirectory("clones");
        var prompts = new ScriptedPromptHandler();
        var app = ShellDriver.Start(data.Path, promptHandler: prompts, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        var url = urlOf(served);
        try
        {
            await app.WaitUntilReadyAsync();
            await app.SignInAsync(provider, provider == HostingProvider.GitLab ? served.GitLabToken : served.AzureDevOpsToken, provider == HostingProvider.AzureDevOps ? Organization : null, Cancelled);
            var session = app.Session.Services.Hosting;
            var bindings = session.BindingsForClone(url);
            var plain = GitCommand.RemoveCredentials(url);
            Assert.Equal(twin ? [plain, plain + ".git"] : [plain], bindings.Select(binding => binding.Url));
            Assert.All(bindings, binding => Assert.Equal(session.AccountOf(provider)!.Account.Key, binding.Account));

            var fake = provider == HostingProvider.GitLab ? (FakeHostingService)served.GitLab : served.AzureDevOps;
            var gitMark = fake.GitRequests.Count;
            var apiMark = fake.Requests.Count;
            var otherMark = (provider == HostingProvider.GitLab ? (FakeHostingService)served.AzureDevOps : served.GitLab).GitRequests.Count;
            var log = app.LogMark();
            var welcome = OpenForm(app);
            welcome.CloneUrl = url;
            welcome.CloneParent = clones.Path;
            Assert.Equal(folder, welcome.CloneName);
            await welcome.StartCloneCommand.ExecuteAsync(null);
            Assert.Null(welcome.ErrorText);
            await app.WaitForRepositoryAsync();
            await app.WaitForWatchersQuietAsync();

            Assert.Empty(prompts.Prompts);
            var lines = app.LogLinesSince(log);
            Assert.Contains(lines, line => line.EndsWith($"{folder}: Clone: git's credential get for {fake.Endpoint().Authority} answered from the account.", StringComparison.Ordinal));
            Assert.DoesNotContain(lines, line => line.Contains("git's configuration could not be read", StringComparison.Ordinal));
            var git = fake.GitRequests.Skip(gitMark).ToList();
            Assert.Contains(git, request => request is { GivenUserName: GitCredential.UserName, Status: 200 });
            Assert.DoesNotContain(git, request => request.Status == 200 && request.GivenUserName is not null && request.GivenUserName != GitCredential.UserName);
            Assert.DoesNotContain(git, request => request is { GivenUserName: GitCredential.UserName, Status: 401 });
            Assert.Equal(otherMark, (provider == HostingProvider.GitLab ? (FakeHostingService)served.AzureDevOps : served.GitLab).GitRequests.Count);
            if (twin)
            {
                // The fake's 301 to <path>.git/info/refs (4.5), which git follows with the twin's token.
                Assert.Contains(fake.Requests.Skip(apiMark), request => request.Status == 301);
                Assert.Contains(git, request => request.Path.StartsWith("/northfield/platform/web-app.git/", StringComparison.Ordinal) && request is { GivenUserName: GitCredential.UserName, Status: 200 });
            }
        }
        finally
        {
            app.Dispose();
        }

        var clone = Path.Combine(clones.Path, folder);
        using var home = new TempDirectory("clone from account home");
        Assert.Equal(url, (await GitAsync(home, clone, "config", "--get", "remote.origin.url")).StandardOutput.Trim());
        var credential = await GitAsync(home, clone, "config", "--get-regexp", "^credential");
        Assert.Equal((1, string.Empty), (credential.ExitCode, credential.StandardOutput.Trim()));
        Assert.DoesNotContain((await GitAsync(home, clone, "config", "--local", "--list")).StandardOutput.Split('\n'), line => line.StartsWith("http.", StringComparison.Ordinal) || line.StartsWith("url.", StringComparison.Ordinal));
        End(app, data, served, clones.Path);
    }

    [AvaloniaFact]
    public async Task Harbor_co_s_typed_url_asks_git_s_credentials_dialog_and_gets_no_token()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("clone from account harbor-co");
        using var clones = new TempDirectory("clones");
        var prompts = new ScriptedPromptHandler().Cancel().Cancel().Cancel();
        var app = ShellDriver.Start(data.Path, promptHandler: prompts, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        try
        {
            await app.WaitUntilReadyAsync();
            await app.SignInAsync(HostingProvider.AzureDevOps, served.AzureDevOpsToken, Organization, Cancelled);
            var url = $"http://harbor-co@127.0.0.1:{served.AzureDevOps.Port}/harbor-co/Docks/_git/manifest";
            Assert.Empty(app.Session.Services.Hosting.BindingsForClone(url));
            var gitMark = served.AzureDevOps.GitRequests.Count;
            var log = app.LogMark();
            var welcome = OpenForm(app);
            welcome.CloneUrl = url;
            welcome.CloneParent = clones.Path;
            await welcome.StartCloneCommand.ExecuteAsync(null);

            Assert.NotEmpty(prompts.Prompts);
            Assert.Equal("Clone cancelled.", welcome.ErrorText);
            var git = served.AzureDevOps.GitRequests.Skip(gitMark).Where(request => request.Path.Contains("harbor-co/Docks/_git/manifest", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.NotEmpty(git);
            Assert.All(git, request =>
            {
                Assert.Null(request.UserName);
                Assert.True(request.GivenUserName is null or "harbor-co", $"git gave the user {request.GivenUserName}.");
            });
            Assert.DoesNotContain(git, request => request.GivenUserName == GitCredential.UserName);
            Assert.DoesNotContain(app.LogLinesSince(log), line => line.Contains("answered from the account.", StringComparison.Ordinal));
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, clones.Path);
    }

    [AvaloniaTheory]
    [InlineData(HostingProvider.GitHub, "Clone from GitHub…", "Clone from GitHub")]
    [InlineData(HostingProvider.GitLab, "Clone from GitLab…", "Clone from GitLab")]
    [InlineData(HostingProvider.AzureDevOps, "Clone from Azure DevOps…", "Clone from Azure DevOps")]
    public async Task Each_palette_command_is_listed_with_its_fake_alone_and_signs_in_first(HostingProvider provider, string command, string title)
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("clone from account palette");
        using var opened = await TempRepo.CreateAsync("local");
        await opened.CommitFileAsync("README.md", "local\n", "First commit");
        var app = ShellDriver.Start(
            data.Path,
            gitHub: provider == HostingProvider.GitHub ? served.GitHub : null,
            azureDevOps: provider == HostingProvider.AzureDevOps ? served.AzureDevOps : null,
            gitLab: provider == HostingProvider.GitLab ? served.GitLab : null);
        try
        {
            await app.WaitUntilReadyAsync();
            app.PressPalette();
            var labels = app.Palette.Shown.Select(entry => entry.Label).ToList();
            Assert.Equal([command], labels.Where(label => label.StartsWith("Clone from ", StringComparison.Ordinal)));
            Assert.Equal(labels.IndexOf("Clone repository…") + 1, labels.IndexOf(command));
            app.PressKey(Key.Escape);
            app.Settle();

            // Signed out: the provider's dialog first; cancelled, it leaves the tabs (D300: no tab). The
            // active tab holds a repository, so that a tab taken before the sign-in would be a new one.
            Assert.True(await app.Session.Shell.ActiveTab.OpenAsync(opened.Path), "The repository did not open.");
            await app.WaitForRepositoryAsync();
            await app.WaitForWatchersQuietAsync();
            var tabs = app.Session.Shell.Tabs.ToList();
            var active = app.Session.Shell.ActiveTab;
            await RunAsync(app, command);
            var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
            Assert.Equal(provider, dialog.Provider);
            dialog.CancelCommand.Execute(null);
            await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is null, "the dialog to close");
            Assert.Equal(tabs, app.Session.Shell.Tabs);
            Assert.Same(active, app.Session.Shell.ActiveTab);
            Assert.All(app.Session.Shell.Tabs, tab => Assert.False(tab.Welcome.IsCloneFormOpen));

            // Again, signing in through the dialog: then its browser on its provider.
            await RunAsync(app, command);
            dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
            await app.WaitForAsync(() => dialog.Opened.IsCompleted, "the dialog's keychain question");
            if (provider == HostingProvider.AzureDevOps)
            {
                app.TypeOrganization(Organization);
                app.Click(app.FindByAutomationId("SignInToken"));
            }

            app.TypeToken(provider switch
            {
                HostingProvider.GitHub => served.GitHubToken,
                HostingProvider.GitLab => served.GitLabToken,
                _ => served.AzureDevOpsToken,
            });
            await app.ConfirmSignInAsync();
            var browser = await app.WaitForDialogAsync<CloneFromGitHubDialogViewModel>();
            await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's load");
            Assert.Equal((provider, title, false), (browser.Provider, browser.Title, browser.ShowsSwitch));
            Assert.Null(browser.ErrorText);
            Assert.NotEmpty(browser.Rows);

            // The form opens where a palette command opens (5.10): a new tab at the end, the repository's kept.
            Assert.Equal(tabs.Count + 1, app.Session.Shell.Tabs.Count);
            Assert.Equal(tabs, app.Session.Shell.Tabs.Take(tabs.Count));
            Assert.Same(app.Session.Shell.Tabs[^1], app.Session.Shell.ActiveTab);
            Assert.True(active.HasRepository);
            Assert.False(active.Welcome.IsCloneFormOpen);
            Assert.True(app.Session.Shell.ActiveTab.Welcome.IsCloneFormOpen);
            Assert.False(app.Session.Shell.ActiveTab.Welcome.BrowseCommand.CanExecute(null));
            browser.CancelCommand.Execute(null);
            await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is null, "the browser to close");
            Assert.True(app.Session.Shell.ActiveTab.Welcome.BrowseCommand.CanExecute(null));
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served);
    }

    /// <summary>
    /// The session ends while a palette command waits for its sign-in (11.5): the dialog shows its
    /// failed save, the account signed in for the session, when the window closes. <c>CloseAll</c>
    /// cancels the dialog, the window's sign-in then answers true, and the command must still open
    /// nothing: no form, no browser left on the dialog layer that nothing would close, no listing
    /// request, and its task ends.
    /// </summary>
    [AvaloniaFact]
    public async Task A_palette_command_whose_sign_in_outlives_the_session_opens_nothing()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("clone from account closed");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        var disposed = false;
        try
        {
            await app.WaitUntilReadyAsync();
            var tabs = app.Session.Shell.Tabs.ToList();
            var command = app.Session.Shell.CloneFromAccountAsync(HostingProvider.GitLab);
            var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
            Assert.Equal(HostingProvider.GitLab, dialog.Provider);
            await app.WaitForAsync(() => dialog.Opened.IsCompleted, "the dialog's keychain question");
            dialog.Token = served.GitLabToken;
            app.Tokens.FailNextSave("the fake keychain is full");
            await dialog.ConfirmCommand.ExecuteAsync(null);
            await app.WaitForAsync(() => dialog.IsSaveFailed, "the failed save");
            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.AccountOf(HostingProvider.GitLab)?.State);
            Assert.False(command.IsCompleted);
            var requests = served.GitLab.Requests.Count;

            // The window closes: the session's end.
            app.Dispose();
            disposed = true;
            for (var waited = 0; waited < 50 && !command.IsCompleted; waited++)
            {
                app.Settle();
                await Task.Delay(100, Cancelled);
            }

            // A little longer, so that a browser shown late would be on the layer by now.
            for (var settled = 0; settled < 5; settled++)
            {
                app.Settle();
                await Task.Delay(100, Cancelled);
            }

            Assert.True(command.IsCompletedSuccessfully, "The palette's command did not end after the session's end.");
            Assert.Empty(app.Session.Services.DialogHost.Open);
            Assert.Equal(tabs, app.Session.Shell.Tabs);
            Assert.All(app.Session.Shell.Tabs, tab => Assert.False(tab.Welcome.IsCloneFormOpen));
            Assert.Empty(served.GitLab.Requests.Skip(requests));
        }
        finally
        {
            if (!disposed)
            {
                app.Dispose();
            }
        }

        End(app, data, served);
    }

    /// <summary>Runs a palette command by its whole label.</summary>
    private static async Task RunAsync(ShellDriver app, string command)
    {
        app.PressPalette();
        app.TypeInPalette(command);
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.Session.Shell.OpenPalette is null, "the palette to close");
    }

    /// <summary>Opens the active tab's clone form.</summary>
    private static WelcomeViewModel OpenForm(ShellDriver app)
    {
        var welcome = app.Session.Shell.ActiveTab.Welcome;
        welcome.OpenCloneFormCommand.Execute(null);
        app.Settle();
        return welcome;
    }

    /// <summary>The form's button collapsed (<paramref name="text"/> null) or shown with that text and automation name; returns its width.</summary>
    private static double AssertButton(ShellDriver app, string? text)
    {
        app.Settle();
        var welcome = app.Session.Shell.ActiveTab.Welcome;
        var button = app.Find<Button>("CloneFromGitHubButton");
        Assert.Equal(text is not null, welcome.ShowsBrowseButton);
        Assert.Equal(text is not null, button.IsEffectivelyVisible);
        if (text is null)
        {
            return 0;
        }

        Assert.Equal(text, welcome.BrowseButtonText);
        Assert.Equal(text, AutomationProperties.GetName(button));
        Assert.Equal(text, button.FindDescendantText());
        var url = app.BoundsOf(app.Find<TextBox>("CloneUrlBox"));
        var bounds = app.BoundsOf(button);
        Assert.Equal(6, bounds.X - url.Right, 1);
        Assert.Equal(app.BoundsOf(app.Find<TextBox>("CloneNameBox")).Right, bounds.Right, 1);
        return bounds.Width;
    }

    private static async Task<GitResult> GitAsync(TempDirectory home, string folder, params string[] arguments)
    {
        var runner = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The tests need git."));
        return await runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = folder, Environment = TempRepo.IsolatedEnvironment(home.Path) }, Cancelled);
    }

    /// <summary>The scenario "hosting7b" served by its three fakes on free ports (D265: no fixed port in this project).</summary>
    private static async Task<ServedHosting7b> ServeAsync()
    {
        var copy = await Scenarios.Hosting7bAsync();
        try
        {
            return await Scenarios.ServeHosting7bAsync(copy, 0, 0, 0, ownsCopy: true, ct: Cancelled);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }

    /// <summary>After the app has closed: no token in the data folder, the clones or a git call; the fakes saw nothing wrong.</summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting7b served, string? clones = null)
    {
        List<string> folders = [data.Path, served.MobileApp.Path, served.Manifest.Path, served.WebApp.Path];
        if (clones is not null)
        {
            folders.Add(clones);
        }

        foreach (var token in served.Tokens)
        {
            TokenTripwire.AssertAbsent(token, folders, app.Session.GitCalls);
        }

        Assert.Equal(0, app.RefusedRequests.Count);
        Assert.Empty(served.AzureDevOps.Errors);
        Assert.Empty(served.GitLab.Errors);
        Assert.Empty(served.GitHub.Errors);
    }
}

/// <summary>Small readings these tests share.</summary>
internal static class CloneFromAccountReadings
{
    /// <summary>The one text a control draws.</summary>
    public static string? FindDescendantText(this Control control) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(control).OfType<TextBlock>().Single().Text;

    /// <summary>The fake's origin (scheme, host and port) on which git reaches it.</summary>
    public static Uri Endpoint(this FakeHostingService fake) => new($"http://127.0.0.1:{fake.Port}/");
}
