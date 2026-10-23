// Check S20 (docs/test-reports/phase-6.md, part S): the merge tool chosen on the screen (D149, D202,
// design 7.7, 8.10, 8.11): with no merge.tool the resolver's "Merge tool…" disabled with its reason;
// the Pick dialog "Choose a merge tool" listing the fixed catalogue's tools as 8.10 filters them (no
// vimdiff) and the user's own "slow" (SD7); "slow" saved globally enabling "Merge tool…" in the open
// resolver without reopening it; "araxis", which git did not find, chosen and its notice shown
// before Save; "Not set" disabling it again (SD22).
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksS
{
    /// <summary>Check S20, one app start (steps 1 to 4), on "conflicts" stopped in its merge with the user's own tool "slow" in the global file (SD3).</summary>
    [AvaloniaFact]
    public async Task Check_S20_merge_tool_chosen_in_settings()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var data = new TempDirectory("data");
        var layout = new Layout { Step = "step 1" };

        // Step 1: the resolver on notes.txt, "Merge tool…" disabled with its reason.
        using var app = await StartAsync(data, repo, PartS.SeedSlowTool);
        Assert.Equal(PartS.MergingBanner, ((Avalonia.Controls.TextBlock)app.FindByAutomationId("BannerText")).Text);
        app.Click(app.WorkingRowPoint());
        await app.WaitForAsync(() => Repo(app).IsWorkingRowSelected, "the working-changes row to be selected");
        await app.WaitForIdleAsync();
        Rest(app);
        app.DoubleClick(await app.FileRowAsync("UnstagedFileList", PartS.ConflictsFile));
        await app.WaitForResolverAsync(PartS.ConflictsFile);
        await app.WaitForIdleAsync();
        Shot(app, "s20a-merge-tool-disabled");
        var resolver = AssertResolver(app, mergeToolEnabled: false);
        Assert.True(app.BannerBounds is { } banner && banner.Width >= app.BoundsOf(app.ResolverView).Width - 0.5, "The banner is not across the resolver.");
        await AssertMergeStoppedAsync(repo);
        await AssertMergeToolKeyAsync(data, repo, null);

        // Step 2: Git on the global scope; Choose… at once (D202); the Pick dialog in SD7's order, no vimdiff.
        layout.Step = "step 2";
        var screen = await app.OpenSettingsAsync();
        app.ChooseSection(SettingsSection.Git);
        var snapshot = app.SettingsSnapshot();
        AssertText(snapshot, "MergeToolValue", PartS.NotSet);
        AssertMergeToolChoose(snapshot);
        AssertHidden(snapshot, "MergeToolStatus");
        app.Click(app.FindByAutomationId("MergeToolChooseButton"));
        await app.WaitForDialogAsync<PickDialogViewModel>();
        ShotAtRest(app, "s20b-merge-tool-pick");
        await AssertPickAsync(app, layout, PartS.ChooseMergeToolTitle, PartS.MergeToolOptions, PartS.NotSet);
        AssertNoMergetool(app);

        // Step 3: slow chosen and saved: one global write; the open resolver's "Merge tool…" enabled (8.11).
        layout.Step = "step 3";
        await app.PickAsync(PartS.SlowTool);
        snapshot = app.SettingsSnapshot();
        AssertText(snapshot, "MergeToolValue", PartS.SlowTool);
        AssertHidden(snapshot, "MergeToolStatus");
        AssertLines(snapshot, "MergeToolNotices");
        var operations = OperationCount(app);
        var sequence = app.CallMark();
        Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
        await WaitForScreenClosedAsync(app, screen);
        await app.WaitForAsync(() => app.Resolver().MergeTool is { IsEnabled: true }, "Merge tool… to be enabled in the open resolver");
        Shot(app, "s20c-merge-tool-enabled");
        await AssertWritesAsync(app, sequence, data, repo, (PartS.WriteGlobalSlow, "data"));
        await AssertScreenReadsAsync(app, sequence, data, repo, varExit: null, others: call => IsWrite(call) || call.CommandText == PartS.GetMergeToolCommand, allowWrites: true);
        AssertReadsAfterWrites(app, sequence);
        await AssertMergeToolReadAsync(app, repo, sequence, 0, PartS.SlowTool);
        Assert.Equal(PartS.SlowToolChosen, GlobalText(data));
        Assert.Equal(operations, OperationCount(app));
        var now = AssertResolver(app, mergeToolEnabled: true);
        Assert.Equal((resolver.Result, false), (now.Result, now.IsModified));
        await AssertMergeStoppedAsync(repo);

        // Step 4: araxis chosen, its notice shown, then Not set; Save unsets merge.tool; "Merge tool…" disabled again.
        layout.Step = "step 4";
        screen = await app.OpenSettingsAsync();
        app.ChooseSection(SettingsSection.Git);
        app.Click(app.FindByAutomationId("MergeToolChooseButton"));
        var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        Assert.Equal(PartS.SlowTool, pick.Selected);
        await app.PickAsync(PartS.AraxisTool);
        snapshot = app.SettingsSnapshot();
        AssertText(snapshot, "MergeToolValue", PartS.AraxisTool);
        AssertText(snapshot, "MergeToolStatus", PartS.MergeToolNotFound(PartS.AraxisTool));
        Assert.Equal(PartS.SlowToolChosen, GlobalText(data));
        app.Click(app.FindByAutomationId("MergeToolChooseButton"));
        pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        Assert.Equal(PartS.AraxisTool, pick.Selected);
        await app.PickAsync(PartS.NotSet);
        snapshot = app.SettingsSnapshot();
        AssertText(snapshot, "MergeToolValue", PartS.NotSet);
        AssertHidden(snapshot, "MergeToolStatus");
        sequence = app.CallMark();
        Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
        await WaitForScreenClosedAsync(app, screen);
        await app.WaitForAsync(() => app.Resolver().MergeTool is { IsEnabled: false }, "Merge tool… to be disabled in the open resolver");
        await AssertWritesAsync(app, sequence, data, repo, (PartS.UnsetGlobalMergeTool, "data"));
        Assert.DoesNotContain(app.CallsSince(sequence), call => call.CommandText.Contains("merge.tool araxis", StringComparison.Ordinal));
        await AssertScreenReadsAsync(app, sequence, data, repo, varExit: null, others: call => IsWrite(call) || call.CommandText == PartS.GetMergeToolCommand, allowWrites: true);
        AssertReadsAfterWrites(app, sequence);
        await AssertMergeToolReadAsync(app, repo, sequence, 1, null);
        Assert.Equal(PartS.SeedSlowTool, GlobalText(data));
        AssertResolver(app, mergeToolEnabled: false);
        await AssertMergeStoppedAsync(repo);
        await AssertMergeToolKeyAsync(data, repo, null);
        AssertNoMergetool(app);
        layout.AssertNone();
    }

    /// <summary>
    /// Phase 4's C10 step 5 resolver (S20 step 1): <c>U</c>, <c>notes.txt</c>, <c>Both modified</c>,
    /// <c>File 1 of 1</c>; <c>Conflict 1 of 2</c>, Take ours, Take theirs, <c>Merge tool…</c> enabled
    /// with no tooltip or disabled with its reason (8.10, SD22), Mark resolved.
    /// </summary>
    private static ResolverSnapshot AssertResolver(ShellDriver app, bool mergeToolEnabled)
    {
        var resolver = app.Resolver();
        Assert.Equal(("U", PartS.ConflictsFile, "Both modified", "File 1 of 1", "Conflict 1 of 2"), (resolver.Status, resolver.FileName, resolver.Kind, resolver.FilePosition, resolver.ConflictPosition));
        Assert.Equal("Take ours", resolver.TakeOurs?.Label);
        Assert.Equal("Take theirs", resolver.TakeTheirs?.Label);
        Assert.Equal("Mark resolved", resolver.MarkResolved?.Label);
        Assert.Equal(new ResolverButton(PartS.MergeToolButton, mergeToolEnabled, mergeToolEnabled ? null : PartS.SetMergeToolReason), resolver.MergeTool);
        return resolver;
    }

    /// <summary>"conflicts" still stopped in its merge: <c>UU notes.txt</c> and its <c>MERGE_HEAD</c>.</summary>
    private static async Task AssertMergeStoppedAsync(TempRepo repo)
    {
        Assert.Equal(["UU notes.txt"], await GitLinesAsync(repo, "status", "--porcelain=v1"));
        Assert.Equal(PartS.TheirsEditsSha, File.ReadAllText(Path.Combine(repo.Path, ".git", "MERGE_HEAD")).Trim());
    }

    /// <summary><c>git config --get merge.tool</c> in the working tree with the session's global file: <paramref name="tool"/>, or exit 1 for none.</summary>
    private static async Task AssertMergeToolKeyAsync(TempDirectory data, TempRepo repo, string? tool)
    {
        var environment = new Dictionary<string, string?>(GitIsolation.SessionGitEnvironment(data.Path));
        var result = await repo.GitWithEnvironmentAllowingFailureAsync(environment, "config", "--get", "merge.tool");
        Assert.Equal((tool is null ? 1 : 0, tool), (result.ExitCode, tool is null ? null : result.StandardOutput.Trim()));
    }

    /// <summary>After a save (8.11): exactly one <c>git config --get merge.tool</c> in the working tree since <paramref name="sequence"/>, with its exit code and output.</summary>
    private static async Task AssertMergeToolReadAsync(ShellDriver app, TempRepo repo, long sequence, int exitCode, string? tool)
    {
        var folder = await WorkingTreeAsync(repo);
        var reads = app.CallsSince(sequence).Where(call => call.CommandText == PartS.GetMergeToolCommand).ToList();
        var read = Assert.Single(reads);
        Assert.True(InFolder(read, repo.Path, folder), $"{read.CommandText} ran in {read.WorkingDirectory}.");
        Assert.Equal(exitCode, read.ExitCode);
        Assert.Equal(tool is null ? string.Empty : tool + "\n", read.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
