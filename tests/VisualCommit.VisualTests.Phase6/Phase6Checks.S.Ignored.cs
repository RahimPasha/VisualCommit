// Check S21 (docs/test-reports/phase-6.md, part S): Keep ignored files on, then off (D193, D196,
// D221, design 9.1, 9.11, 7.3), one start (SD13): on, "Check out vendor" refused with D179's text;
// turned off on the screen, a checkout of the older commit "Track the secret" of the current branch
// still refused (D179's automatic stash path keeps its option, 9.1); off, "Check out vendor" runs as
// git runs it and overwrites the ignored secret.env.
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksS
{
    /// <summary>Check S21, one app start (steps 1 to 4), on "ignored" (IG0).</summary>
    [AvaloniaFact]
    public async Task Check_S21_keep_ignored_files_on_and_off()
    {
        using var repo = await Scenarios.IgnoredAsync();
        using var data = new TempDirectory("data");
        var layout = new Layout { Step = "step 1" };

        // Step 1: on (the default): Check out vendor from the left panel's menu, refused (G22's refusal, not pictured here).
        using var app = await StartAsync(data, repo);
        var exact = await repo.ExactStateAsync();
        Assert.Equal(PartS.MainWorkSha, await repo.HeadAsync());
        Assert.Equal(PartS.SecretLocal, WorkingFile(repo, "secret.env"));
        Assert.Equal(PartS.BuildOutput, WorkingFile(repo, "build/out.bin"));
        Assert.Equal(PartS.Ig0Ignored, await GitLinesAsync(repo, "status", "--porcelain=v1", "--ignored"));
        AssertGraph(app, PartS.Ig0, PartS.IgLanes, working: null);
        var start = await QuietMarkAsync(app);
        await RefusedCheckoutAsync(app, repo, () => CheckOutVendorAsync(app), PartS.CheckOutVendor, PartS.SwitchVendorKeptCommand, PartS.IgnoredFileInTheWay("vendor"), picture: null);
        Assert.Equal(exact, await repo.ExactStateAsync());
        AssertUndo(app, PartS.NothingToUndo);
        await CloseDialogAsync(app);

        // Step 2: Keep ignored files turned off on the screen and saved: settings.json only, no git write.
        layout.Step = "step 2";
        var sequence = await QuietMarkAsync(app);
        var screen = await app.OpenSettingsAsync();
        var opened = await QuietMarkAsync(app);
        await AssertScreenReadsAsync(app, sequence, data, repo, varExit: null, until: opened);
        AssertChecked(app.SettingsSnapshot(), "KeepIgnoredCheckBox", true);
        ClickField(app, "KeepIgnoredCheckBox");
        AssertChecked(app.SettingsSnapshot(), "KeepIgnoredCheckBox", false);
        Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
        await WaitForScreenClosedAsync(app, screen);
        AssertSettingsJson(data, ("keepIgnoredFiles", "false"));
        await AssertScreenReadsAsync(app, opened, data, repo, varExit: null);
        AssertNoWrite(app, start);

        // Step 3: off, the older commit Track the secret (row 5) still refused: D179's path keeps its option (9.1, D221).
        layout.Step = "step 3";
        var point = app.GraphRowPoint(PartS.TrackSecretRow);
        layout.Near(PartS.TrackSecretPoint.X, point.X, "Track the secret's click point's x");
        layout.Near(PartS.TrackSecretPoint.Y, point.Y, "Track the secret's click point's y");
        await RefusedCheckoutAsync(
            app,
            repo,
            async () =>
            {
                app.RightClick(point);
                var menu = app.Menu();
                Assert.Equal(PartS.TrackSecretRow, Repo(app).SelectedIndex);
                Assert.Equal("Track the secret", Repo(app).Graph.CommitAt(PartS.TrackSecretRow).Subject);
                var item = menu.Item(PartS.CheckOutThisCommit);
                Assert.True(item.IsEnabled && item.ToolTip is null, $"\"{PartS.CheckOutThisCommit}\" is disabled: \"{item.ToolTip}\".");
                app.ClickMenuItem(PartS.CheckOutThisCommit);
                await Task.CompletedTask;
            },
            PartS.CheckOutTrackSecret,
            PartS.DetachTrackSecretCommand,
            PartS.IgnoredFileInTheWay(PartS.TrackSecretSha[..7]),
            picture: "s21a-off-old-commit-blocked");
        Assert.Equal(exact, await repo.ExactStateAsync());
        await CloseDialogAsync(app);

        // Step 4: off, Check out vendor runs as git runs it: secret.env overwritten without a word (Q5).
        layout.Step = "step 4";
        var mark = app.LogMark();
        sequence = await QuietMarkAsync(app);
        await CheckOutVendorAsync(app);
        await WaitForOutcomeAsync(app, repo, PartS.CheckOutVendor, mark, OperationOutcome.Succeeded);
        Shot(app, "s21b-off-checkout-ran");
        await AssertNoDialogAsync(app);
        var switched = app.CallsSince(sequence).Where(call => call.CommandText.StartsWith("git switch ", StringComparison.Ordinal)).ToList();
        var call = Assert.Single(switched);
        Assert.Equal((PartS.SwitchVendorCommand, (int?)0), (call.CommandText, call.ExitCode));
        if (await IsReportGitAsync())
        {
            Assert.Equal(PartS.UpToDateWithVendor, call.StandardOutput.Trim());
            Assert.Equal(PartS.SwitchedToVendor, call.StandardError.Trim());
        }
        Assert.DoesNotContain(app.CallsSince(sequence), record => record.CommandText.StartsWith(PartS.CheckIgnoreCommand, StringComparison.Ordinal));
        AssertStatus(app, PartS.CheckedOutVendor);
        Assert.Equal("vendor", await repo.CurrentBranchAsync());
        Assert.Equal(PartS.VendorSecretSha, await repo.HeadAsync());
        Assert.Equal(PartS.SecretVendor, WorkingFile(repo, "secret.env"));
        Assert.Equal(PartS.BuildOutput, WorkingFile(repo, "build/out.bin"));
        Assert.Equal(PartS.Ig0vIgnored, await GitLinesAsync(repo, "status", "--porcelain=v1", "--ignored"));
        Assert.Equal(PartS.IgnoredHeads, (await GitLinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)", "refs/heads", "refs/stash")).ToList());
        AssertGraph(app, PartS.Ig0v, PartS.IgLanes, working: null);
        AssertBranch(app, "vendor");
        Assert.True(app.RefRowModel("Local branches", "vendor") is VisualCommit.App.ViewModels.Panels.RefItemRow { IsHead: true }, "Local branches › vendor is not HEAD's branch.");
        var name = (await app.RefRowAsync("Local branches", "vendor")).GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().Single(block => block.Name == "RefName");
        Assert.Equal(PaletteOf(app).Accent, (name.Foreground as Avalonia.Media.ISolidColorBrush)?.Color);
        AssertUndo(app, PartS.UndoCheckoutOfVendor);
        layout.AssertNone();
    }

    /// <summary>Local branches › vendor's menu, then its "Check out vendor" clicked.</summary>
    private static async Task CheckOutVendorAsync(ShellDriver app)
    {
        app.RightClick(await app.RefRowAsync("Local branches", "vendor"));
        var menu = app.Menu();
        Assert.Equal("vendor", menu.Header);
        var item = menu.Item(PartS.CheckOutVendor);
        Assert.True(item.IsEnabled && item.ToolTip is null, $"\"{PartS.CheckOutVendor}\" is disabled: \"{item.ToolTip}\".");
        app.ClickMenuItem(PartS.CheckOutVendor);
    }

    /// <summary>
    /// A checkout refused for the ignored <c>secret.env</c> (scenario facts 4.2, 9.11): started by
    /// <paramref name="act"/>, it ends Failed; the calls in this order, <paramref name="command"/> (1,
    /// git's block naming secret.env) then <c>git check-ignore --stdin -z</c> (0, <c>secret.env\0</c>),
    /// no stash pushed; the error dialog <c>Checkout blocked</c> with D179's
    /// <paramref name="summary"/>, git's block, the command and Copy output, Close (no extra button);
    /// the status bar's <c>{title} failed</c> in the danger colour. <paramref name="picture"/> is taken
    /// with the dialog open.
    /// </summary>
    private static async Task RefusedCheckoutAsync(ShellDriver app, TempRepo repo, Func<Task> act, string title, string command, string summary, string? picture)
    {
        var mark = app.LogMark();
        var sequence = await QuietMarkAsync(app);
        await act();
        await WaitForOutcomeAsync(app, repo, title, mark, OperationOutcome.Failed);
        if (picture is not null)
        {
            Shot(app, picture);
        }

        AssertOperationCalls(app, sequence, [(command, 1), (PartS.CheckIgnoreCommand, 0)]);
        Assert.Equal(PartS.SecretIgnored, app.CallsSince(sequence).Last(call => call.CommandText == PartS.CheckIgnoreCommand).StandardOutput);
        Assert.DoesNotContain(app.CallsSince(sequence), call => call.CommandText.StartsWith("git stash push", StringComparison.Ordinal));
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartS.CheckoutBlocked,
            summary,
            PartS.Ignored,
            command,
            [PartS.CopyOutput, PartS.Close],
            PartS.UntrackedOverwritten,
            PartS.UntrackedOverwritten[0]));
        AssertStatus(app, PartS.Failed(title), OperationStatusTone.Danger);
    }
}
