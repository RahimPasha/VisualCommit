using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The palette's "Clone from GitHub…" (docs/phase-7a-design.md 9.5, 9.6; D299, D300, D334): not
/// listed with GitHub off; right after "Clone repository…" with the fake, without a key; signed
/// out it runs the window's sign-in entry first (a stub on <c>TabServices.SignInToGitHub</c>):
/// false leaves the tabs and the welcome page as they were, true opens the form on the empty active
/// tab with the browser over it; signed in it does not ask. The fake of GitHub is in process.
/// </summary>
public sealed class PaletteCloneFromGitHubTests
{
    private const string Command = "Clone from GitHub…";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [AvaloniaFact]
    public void Not_listed_with_github_off()
    {
        using var data = new TempDirectory("palette clone off");
        using var app = ShellDriver.Start(data.Path);

        app.PressPalette();
        var labels = app.Palette.Shown.Select(entry => entry.Label).ToList();
        Assert.Contains("Clone repository…", labels);
        Assert.DoesNotContain(Command, labels);
        app.TypeInPalette("Clone from GitHub");
        Assert.DoesNotContain(app.Palette.Shown, entry => entry.Label == Command);
    }

    [AvaloniaFact]
    public async Task Listed_right_after_clone_repository_without_a_key()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("palette clone listed");
        var app = Start(data, served);
        try
        {
            app.PressPalette();
            var labels = app.Palette.Shown.Select(entry => entry.Label).ToList();
            var at = labels.IndexOf("Clone repository…");
            Assert.True(at >= 0, string.Join(" | ", labels));
            Assert.Equal(["Clone repository…", Command, "Init repository…"], labels.Skip(at).Take(3));
            var entry = app.Palette.Shown[at + 1];
            Assert.True(entry.IsEnabled);
            Assert.Null(entry.Detail);
            Assert.Null(entry.KeyText);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Signed_out_a_refused_sign_in_leaves_the_tabs_and_the_welcome_page_as_they_were()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("palette clone refused");
        var app = Start(data, served);
        try
        {
            var asked = 0;
            app.Session.Services.SignInToGitHub = () =>
            {
                asked++;
                return Task.FromResult(false);
            };
            var tab = app.Session.Shell.ActiveTab;

            await RunAsync(app);

            Assert.Equal(1, asked);
            Assert.Same(tab, Assert.Single(app.Session.Shell.Tabs));
            Assert.False(tab.Welcome.IsCloneFormOpen);
            Assert.Null(app.Session.Services.DialogHost.Current);
            Assert.Empty(served.GitHub.Requests);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Signed_out_a_sign_in_opens_the_form_on_the_empty_tab_with_the_browser_over_it()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("palette clone signed in after");
        var app = Start(data, served);
        try
        {
            var asked = 0;
            app.Session.Services.SignInToGitHub = async () =>
            {
                asked++;
                await app.Session.Services.Hosting.SignInAsync(served.OctoToken, Cancelled);
                return true;
            };
            var tab = app.Session.Shell.ActiveTab;

            await RunAsync(app);
            var browser = await app.WaitForDialogAsync<CloneFromGitHubDialogViewModel>();
            await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's list");
            app.Settle();

            Assert.Equal(1, asked);
            Assert.Same(tab, Assert.Single(app.Session.Shell.Tabs));
            Assert.True(tab.Welcome.IsCloneFormOpen);
            Assert.Equal(string.Empty, tab.Welcome.CloneUrl);
            Assert.Equal(["octo/hello", "octo/tool", "octo/notes", "octo/website"], browser.Rows.Select(row => row.FullName));
            await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneFromGitHubFilter", "the focus in the browser's filter");

            // Cancelling the browser leaves the form open (D300).
            browser.CancelCommand.Execute(null);
            app.Settle();
            Assert.True(tab.Welcome.IsCloneFormOpen);
            Assert.Null(app.Session.Services.DialogHost.Current);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Signed_in_it_does_not_ask()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("palette clone signed in");
        var app = Start(data, served);
        try
        {
            await app.SignInAsync(served.OctoToken, Cancelled);
            var asked = 0;
            app.Session.Services.SignInToGitHub = () =>
            {
                asked++;
                return Task.FromResult(true);
            };

            await RunAsync(app);
            var browser = await app.WaitForDialogAsync<CloneFromGitHubDialogViewModel>();
            await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's list");

            Assert.Equal(0, asked);
            Assert.True(app.Session.Shell.ActiveTab.Welcome.IsCloneFormOpen);
            Assert.Equal(4, browser.Rows.Count);
            browser.CancelCommand.Execute(null);
            app.Settle();
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served);
    }

    /// <summary>The palette's command by the keyboard: the palette, the typed query, Enter.</summary>
    private static async Task RunAsync(ShellDriver app)
    {
        app.PressPalette();
        app.TypeInPalette("Clone from GitHub");
        await app.RunPaletteEntryAsync(Command);
        Assert.False(app.IsPaletteOpen);
    }

    /// <summary>The scenario "hosting" served in process (D265: no port in this project).</summary>
    private static async Task<ServedHosting> ServeAsync()
    {
        var copy = await Scenarios.HostingAsync();
        try
        {
            return await Scenarios.ServeHostingAsync(copy, inProcess: true, ct: Cancelled, ownsCopy: true);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }

    /// <summary>One empty tab with the in-process fake as GitHub (design 16.3: through <c>hosting:</c>).</summary>
    private static ShellDriver Start(TempDirectory data, ServedHosting served) =>
        ShellDriver.Start(data.Path, hosting: new HostingOptions(served.GitHub.Endpoints, served.GitHub.CreateHandler));

    /// <summary>After the app closed: no token in the data folder or any git call; nothing refused or wrong at the fake.</summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting served)
    {
        foreach (var token in new[] { served.OctoToken, served.OctoClassicToken, served.AdaToken })
        {
            TokenTripwire.AssertAbsent(token, [data.Path], app.Session.GitCalls);
        }

        Assert.Equal(0, app.RefusedRequests.Count);
        Assert.Empty(served.GitHub.Errors);
    }
}
