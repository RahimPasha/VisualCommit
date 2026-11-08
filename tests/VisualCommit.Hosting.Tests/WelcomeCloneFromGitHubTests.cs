using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core;
using VisualCommit.Core.Git;
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
/// read; the tab not empty while that read is held. A conditional include (fixed at phase 7b's review
/// before the gate, docs/phase-7b-design.md 16.3) whose file rewrites the URL to another user, through
/// <c>gitdir:</c>, <c>hasconfig:remote.*.url:</c> or a relative path, that git cannot read, or that
/// holds a conditional include of its own, binds nothing and git asks the user's own helper first;
/// one that holds only an email, or names no file, still binds. The fake of GitHub serves on a free
/// port (design 16.3), so that git reaches it; the held read's test serves it in process and runs no
/// git.
/// </summary>
public sealed class WelcomeCloneFromGitHubTests
{
    private const string ConfigRead = "git config --show-scope --show-origin -z --list";

    /// <summary>The start of a read of a conditional include's file (D331, fixed at phase 7b's review before the gate).</summary>
    private const string IncludeRead = "git config --file ";

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
            Assert.False(Welcome(app).ShowsBrowseButton);
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

            Assert.Equal(await TopLevelAsync(served, Path.Combine(clones.Path, "hello")), repository.Path);
            Assert.DoesNotContain(app.Session.Services.DialogHost.Open, dialog => dialog is PromptDialogViewModel);
            var calls = app.GitCalls().Where(call => call.Sequence > mark).Select(call => call.CommandText).ToList();
            var read = calls.IndexOf(ConfigRead);
            var clone = calls.IndexOf($"git clone --progress -- {Scenarios.HostingUrl("octo/hello", served.GitHub.Port)} hello");
            Assert.True(read >= 0 && clone > read, "The clone did not read git's configuration first:\n" + string.Join('\n', calls));
            Assert.DoesNotContain(calls, call => call.StartsWith(IncludeRead, StringComparison.Ordinal));
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
            Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.StartsWith(IncludeRead, StringComparison.Ordinal));
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, clones.Path);
    }

    /// <summary>
    /// A global <c>includeIf "gitdir:"</c> over the clone's parent whose file rewrites the URL to another
    /// user: the outside read sees no rule of it, <c>git clone</c> applies it (D331, fixed at phase 7b's
    /// review before the gate). The clone binds nothing and git asks the user's own helper first, for
    /// that user, then the app's dialog (D82's order).
    /// </summary>
    [AvaloniaFact]
    public async Task A_gitdir_include_whose_rule_rewrites_the_url_to_another_user_binds_nothing_and_asks_the_user_s_helper()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone gitdir");
        using var clones = new TempDirectory("clones");
        using var files = new TempDirectory("includes");
        var rules = Path.Combine(files.Path, "rules.gitconfig");
        File.WriteAllText(rules, AnotherUserRule(served));
        var helper = SeedWithUserHelper(data, files, $"[includeIf \"gitdir:{Path.GetFileName(clones.Path)}/\"]\n\tpath = {GitPath(rules)}\n");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenSignedInFormAsync(app, served);
            await AssertIncludeUnbindsAsync(app, served, clones.Path, helper, "rewrites the clone's URL", includeReads: 1);

            // Git applied the include: it asked the user's helper for alice.
            Assert.Contains("username=alice", File.ReadAllText(helper), StringComparison.Ordinal);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, clones.Path);
    }

    /// <summary>The same with <c>includeIf "hasconfig:remote.*.url:"</c> naming the clone's URL, which <c>git clone</c> applies since Git 2.36.</summary>
    [AvaloniaFact]
    public async Task A_hasconfig_include_whose_rule_rewrites_the_url_to_another_user_binds_nothing_and_asks_the_user_s_helper()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone hasconfig");
        using var clones = new TempDirectory("clones");
        using var files = new TempDirectory("includes");
        var rules = Path.Combine(files.Path, "rules.gitconfig");
        File.WriteAllText(rules, AnotherUserRule(served));
        var helper = SeedWithUserHelper(data, files, $"[includeIf \"hasconfig:remote.*.url:http://127.0.0.1:{served.GitHub.Port}/**\"]\n\tpath = {GitPath(rules)}\n");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenSignedInFormAsync(app, served);
            await AssertIncludeUnbindsAsync(app, served, clones.Path, helper, "rewrites the clone's URL", includeReads: 1);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, clones.Path);
    }

    /// <summary>
    /// A relative include path is read beside the file that holds the directive, as git reads it: the
    /// rule in <c>rules.gitconfig</c> next to the global file binds nothing.
    /// </summary>
    [AvaloniaFact]
    public async Task A_relative_include_path_is_read_beside_the_global_file_and_its_rule_binds_nothing()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone relative");
        using var clones = new TempDirectory("clones");
        using var files = new TempDirectory("includes");
        var helper = SeedWithUserHelper(data, files, $"[includeIf \"gitdir:{Path.GetFileName(clones.Path)}/\"]\n\tpath = rules.gitconfig\n");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(ShellDriver.GlobalGitConfigPath(data.Path))!, "rules.gitconfig"), AnotherUserRule(served));
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenSignedInFormAsync(app, served);
            await AssertIncludeUnbindsAsync(app, served, clones.Path, helper, "rewrites the clone's URL", includeReads: 1);
            Assert.Contains("username=alice", File.ReadAllText(helper), StringComparison.Ordinal);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, clones.Path);
    }

    /// <summary>An included file git cannot read (a broken line) binds nothing, whatever its condition: what it holds is not known.</summary>
    [AvaloniaFact]
    public async Task An_included_file_git_cannot_read_binds_nothing()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone unreadable");
        using var clones = new TempDirectory("clones");
        using var files = new TempDirectory("includes");
        var broken = Path.Combine(files.Path, "broken.gitconfig");
        File.WriteAllText(broken, "[url \"http://alice@127.0.0.1/\"\n\tinsteadOf = x\n");
        var helper = SeedWithUserHelper(data, files, $"[includeIf \"gitdir:{Path.GetFileName(files.Path)}-elsewhere/\"]\n\tpath = {GitPath(broken)}\n");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenSignedInFormAsync(app, served);
            await AssertIncludeUnbindsAsync(app, served, clones.Path, helper, "could not be read", includeReads: 1);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, clones.Path);
    }

    /// <summary>An included file that holds a conditional include of its own binds nothing: only the new repository can judge its condition.</summary>
    [AvaloniaFact]
    public async Task An_included_file_with_a_conditional_include_of_its_own_binds_nothing()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone nested");
        using var clones = new TempDirectory("clones");
        using var files = new TempDirectory("includes");
        var outer = Path.Combine(files.Path, "outer.gitconfig");
        File.WriteAllText(outer, $"[includeIf \"onbranch:main\"]\n\tpath = {GitPath(Path.Combine(files.Path, "inner.gitconfig"))}\n");
        var helper = SeedWithUserHelper(data, files, $"[includeIf \"gitdir:{Path.GetFileName(clones.Path)}/\"]\n\tpath = {GitPath(outer)}\n");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenSignedInFormAsync(app, served);
            await AssertIncludeUnbindsAsync(app, served, clones.Path, helper, "holds a conditional include of its own", includeReads: 1);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, clones.Path);
    }

    /// <summary>
    /// A conditional include whose file holds only <c>user.email</c>, and one whose file is not there
    /// (git ignores it), change no URL: the clone is bound and takes the account's token without a
    /// dialog, after one read of each directive's file.
    /// </summary>
    [AvaloniaFact]
    public async Task An_include_of_only_an_email_and_a_missing_include_still_bind()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("welcome clone email");
        using var clones = new TempDirectory("clones");
        using var files = new TempDirectory("includes");
        var email = Path.Combine(files.Path, "email.gitconfig");
        File.WriteAllText(email, "[user]\n\temail = someone@example.invalid\n");
        ShellDriver.SeedGlobalGitConfig(
            data.Path,
            $"[includeIf \"gitdir:{Path.GetFileName(clones.Path)}/\"]\n\tpath = {GitPath(email)}\n" +
            $"[includeIf \"hasconfig:remote.*.url:http://127.0.0.1:{served.GitHub.Port}/**\"]\n\tpath = {GitPath(Path.Combine(files.Path, "missing.gitconfig"))}\n");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        try
        {
            await OpenSignedInFormAsync(app, served);
            app.ReplaceText("CloneUrlBox", Scenarios.HostingUrl("octo/hello", served.GitHub.Port));
            app.TypeInto("CloneParentBox", clones.Path);
            var mark = await app.QuietCallMarkAsync();
            var log = app.LogMark();

            app.Click(app.FindByAutomationId("CloneStartButton"));
            await app.WaitForRepositoryAsync();
            await app.WaitForWatchersQuietAsync();

            Assert.DoesNotContain(app.Session.Services.DialogHost.Open, dialog => dialog is PromptDialogViewModel);
            var calls = app.GitCalls().Where(call => call.Sequence > mark).Select(call => call.CommandText).ToList();
            var read = calls.IndexOf(ConfigRead);
            var clone = calls.IndexOf($"git clone --progress -- {Scenarios.HostingUrl("octo/hello", served.GitHub.Port)} hello");
            var includes = calls.Select((call, index) => (call, index)).Where(pair => pair.call.StartsWith(IncludeRead, StringComparison.Ordinal)).Select(pair => pair.index).ToList();
            Assert.True(read >= 0 && clone > read && includes.Count == 2 && includes.All(index => index > read && index < clone), "The clone did not read the configuration and each include once, in that order:\n" + string.Join('\n', calls));
            var lines = app.LogLinesSince(log);
            Assert.Contains(lines, line => line.Contains($"hello: Clone: git's credential get for 127.0.0.1:{served.GitHub.Port} answered from the account.", StringComparison.Ordinal));
            Assert.DoesNotContain(lines, line => line.Contains("Cloning: a conditional include", StringComparison.Ordinal));
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

    /// <summary>
    /// While a bound clone reads git's configuration, before git starts, the tab is not empty, so that
    /// nothing else opens in it (the palette's Open, Clone and Init, a workspace from its welcome
    /// page), and a close has nothing to wait for; cancelled, the tab is empty again.
    /// </summary>
    [Fact]
    public async Task A_tab_whose_clone_reads_git_s_configuration_is_not_empty_meanwhile()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        using var data = new TempDirectory("welcome clone held read");
        var configuration = new HeldConfiguration();
        var services = new TabServices(
            new NoClones(),
            new FakeFolderPicker(),
            new JsonSettingsStore(Path.Combine(data.Path, "settings.json")),
            new JsonSessionStore(Path.Combine(data.Path, "session.json")),
            new DateDisplay(TimeZoneInfo.Utc),
            gitSettings: configuration,
            hosting: setup.Session);
        using var tab = new RepoTabViewModel(services);
        var welcome = tab.Welcome;
        welcome.CloneUrl = Scenarios.HostingUrl("octo/hello", setup.Fake.Port);
        welcome.CloneParent = data.Path;
        Assert.NotEmpty(setup.Session.BindingsForClone(welcome.CloneUrl));
        Assert.True(tab.IsEmpty);

        var cloning = welcome.StartCloneCommand.ExecuteAsync(null);
        await configuration.Reading.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        Assert.True(welcome.IsCloning);
        Assert.False(tab.IsEmpty);
        Assert.True(welcome.RunningClone is { IsCompleted: true }, "No completed task stands in for the clone during the read.");

        welcome.CancelCloneCommand.Execute(null);
        await cloning.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        Assert.Equal("Clone cancelled.", welcome.ErrorText);
        Assert.False(welcome.IsCloning);
        Assert.Null(welcome.RunningClone);
        Assert.True(tab.IsEmpty);
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
        Assert.False(welcome.BrowseCommand.CanExecute(null));
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

    /// <summary>
    /// Starts a clone of octo/hello that a conditional include keeps unbound (<see cref="AssertAsksAsync"/>),
    /// then checks that the scope bound nothing, that <paramref name="includeReads"/> include files were
    /// read, that the Debug line gives <paramref name="reason"/> and that git asked the user's own helper
    /// (<paramref name="helper"/>, its record) first, as D82's order has it.
    /// </summary>
    private static async Task AssertIncludeUnbindsAsync(ShellDriver app, ServedHosting served, string parent, string helper, string reason, int includeReads)
    {
        var mark = await app.QuietCallMarkAsync();
        var log = app.LogMark();
        await AssertAsksAsync(app, Scenarios.HostingUrl("octo/hello", served.GitHub.Port), parent, readsConfiguration: true);

        var lines = app.LogLinesSince(log);
        Assert.True(File.Exists(helper), "Git did not ask the user's own helper.");
        Assert.Contains($"host=127.0.0.1:{served.GitHub.Port}", File.ReadAllText(helper), StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Contains("Clone: ", StringComparison.Ordinal) && line.Contains("URLs bound to the account", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains($"Cloning: a conditional include of git's configuration {reason}; the clone goes without the account's token (D82's order).", StringComparison.Ordinal));
        var calls = app.GitCalls().Where(call => call.Sequence > mark).Select(call => call.CommandText).ToList();
        Assert.Equal(includeReads, calls.Count(call => call.StartsWith(IncludeRead, StringComparison.Ordinal)));
    }

    /// <summary>A rule that rewrites the fake's URLs to the user alice, as a multi-account set-up keeps it in an included file.</summary>
    private static string AnotherUserRule(ServedHosting served) =>
        $"[url \"http://alice@127.0.0.1:{served.GitHub.Port}/\"]\n\tinsteadOf = http://127.0.0.1:{served.GitHub.Port}/\n";

    /// <summary>
    /// Seeds the global configuration of <paramref name="data"/> with the user's own credential helper,
    /// which records what git asks it in <c>user-helper.txt</c> of <paramref name="files"/> and answers
    /// nothing, then <paramref name="more"/>; returns the record's path.
    /// </summary>
    private static string SeedWithUserHelper(TempDirectory data, TempDirectory files, string more)
    {
        var record = Path.Combine(files.Path, "user-helper.txt");
        ShellDriver.SeedGlobalGitConfig(data.Path, $"[credential]\n\thelper = \"!f() {{ cat >> '{GitPath(record)}'; }}; f\"\n{more}");
        return record;
    }

    /// <summary>A path as a configuration file writes it: forward slashes.</summary>
    private static string GitPath(string path) => path.Replace('\\', '/');

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

    /// <summary>
    /// The working tree <paramref name="folder"/> as git names it (<c>rev-parse --show-toplevel</c>),
    /// as the tab keeps it: the physical folder (macOS: <c>/private/var/…</c> for a temporary folder
    /// made as <c>/var/…</c>), full and without a trailing separator.
    /// </summary>
    private static async Task<string> TopLevelAsync(ServedHosting served, string folder)
    {
        var topLevel = (await served.Hello.GitAsync("-C", folder, "rev-parse", "--show-toplevel")).StandardOutput.TrimEnd('\r', '\n');
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(topLevel));
    }

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

    /// <summary>Git's configuration whose read waits until it is cancelled: a bound clone's read of the user's rules held (9.3).</summary>
    private sealed class HeldConfiguration : IGitConfiguration
    {
        private readonly TaskCompletionSource _reading = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes when the read begins.</summary>
        public Task Reading => _reading.Task;

        public string GlobalFile => string.Empty;

        public string XdgFile => string.Empty;

        public async Task<GitConfigRead> ReadAsync(string? workingTree, CancellationToken cancellationToken = default)
        {
            _reading.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new UnreachableException();
        }

        public Task<IReadOnlyList<GitConfigValue>> ReadIncludedAsync(GitConfigValue directive, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<GitIdentityRead> ReadIdentityAsync(string workingTree, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public string? Variable(string name) => null;

        public Task WriteGlobalAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MergeToolHelp> ReadToolHelpAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    /// <summary>No repository opens or clones: the held read is cancelled before git would start.</summary>
    private sealed class NoClones : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null) => throw new NotSupportedException();
    }
}

