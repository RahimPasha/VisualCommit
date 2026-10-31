using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The window's sign-in entry, <c>TabServices.SignInToGitHub</c> (docs/phase-7a-design.md 8.5,
/// 8.8; D104, D334): GitHub off answers false without a dialog; signed in answers true without one;
/// signed out stacks "Add GitHub account" over a dialog already open; two calls share one dialog.
/// The app starts through <see cref="ShellDriver"/> with the fake of GitHub in process.
/// </summary>
public sealed class SignInEntryTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [AvaloniaFact]
    public async Task Off_answers_false_without_a_dialog()
    {
        using var data = new TempDirectory("sign-in entry");
        using var app = ShellDriver.Start(data.Path);

        Assert.False(await EntryOf(app)());

        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Empty(app.Tokens.Calls);
    }

    [AvaloniaFact]
    public async Task Signed_in_answers_true_without_a_dialog()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("sign-in entry");
        using var app = Start(data, served);
        await app.SignInAsync(served.OctoToken, Cancelled);
        var requests = served.GitHub.Requests.Count;

        Assert.True(await EntryOf(app)());

        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Equal(requests, served.GitHub.Requests.Count);
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task Signed_out_stacks_the_dialog_over_an_open_one_and_answers_whether_it_signed_in()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("sign-in entry");
        using var app = Start(data, served);
        var settings = await app.OpenSettingsAsync();

        var entry = EntryOf(app)();
        var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();

        var open = app.Session.Services.DialogHost.Open;
        Assert.Equal(2, open.Count);
        Assert.Same(settings, open[0]);
        Assert.Same(dialog, open[1]);
        Assert.Same(dialog, app.Session.Services.DialogHost.Current);

        dialog.Token = served.OctoToken;
        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.True(await entry.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));
        Assert.True(dialog.IsClosed);
        Assert.False(settings.IsClosed);
        Assert.Same(settings, app.Session.Services.DialogHost.Current);
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task Two_calls_share_one_dialog()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("sign-in entry");
        using var app = Start(data, served);

        var first = EntryOf(app)();
        var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
        var second = EntryOf(app)();
        app.Settle();

        Assert.Single(app.Session.Services.DialogHost.Open);
        Assert.False(second.IsCompleted);

        dialog.CancelCommand.Execute(null);

        Assert.False(await first.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));
        Assert.False(await second.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));
        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Null(app.Session.Services.Hosting.Account);
        await EndAsync(app, data, served);
    }

    private static Func<Task<bool>> EntryOf(ShellDriver app) =>
        app.Session.Services.SignInToGitHub ?? throw new InvalidOperationException("The window set no sign-in entry.");

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

    /// <summary>One empty tab with the in-process fake as GitHub (design 16.3: through <c>hosting:</c>, never <c>gitHub:</c>).</summary>
    private static ShellDriver Start(TempDirectory data, ServedHosting served) =>
        ShellDriver.Start(data.Path, hosting: new HostingOptions(served.GitHub.Endpoints, served.GitHub.CreateHandler));

    /// <summary>Closes the app, then checks that no token reached the data folder and that the fake saw nothing wrong.</summary>
    private static Task EndAsync(ShellDriver app, TempDirectory data, ServedHosting served)
    {
        app.Dispose();
        foreach (var token in new[] { served.OctoToken, served.OctoClassicToken, served.AdaToken })
        {
            TokenTripwire.AssertAbsent(token, [data.Path], null);
        }

        Assert.Empty(served.GitHub.Errors);
        return Task.CompletedTask;
    }
}
