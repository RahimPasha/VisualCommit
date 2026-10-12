// Check B8 of part B (docs/test-reports/phase-4.md): two selected commits squashed from their
// several-commits menu, the dialog filled with both messages (the "#" line kept), the result with
// the later commit replayed. Part of Phase4ChecksB; see Phase4Checks.B.cs for the class.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartB;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksB
{
    /// <summary>Check B8 (steps 1 to 4): Four and Three selected, their menu, the squash dialog, G-B8 and the squashed commit selected.</summary>
    [AvaloniaFact]
    public async Task Check_B8_squash_two_commits()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, GI, Lanes, showsDate: true);

        // Step 1: Four clicked, Three added with the platform's command modifier.
        var four = GraphRowPoint(1);
        var three = GraphRowPoint(2);
        Assert.Equal((four, three), (app.GraphRowPoint(1), app.GraphRowPoint(2)));
        app.Click(four);
        app.Click(three, app.CommandModifier);
        await app.WaitForAsync(() => Repo(app).Selection.Count == 2, "Four and Three to be selected");
        var selected = app.Capture();
        AssertSelectedRows(app, selected, 1, 2);
        await AssertMultiSelectionAsync(app);

        // Step 2: the several-commits menu of the right-clicked row, part of the selection.
        app.RightClick(three);
        var menu = app.Menu();
        var menuPicture = Shot(app, "b8a-commits-menu");
        AssertSelectedRows(app, menuPicture, 1, 2);
        AssertMenu(menu, [.. SeveralCommitsMenu]);
        AssertHighlighted(menu, SquashItem);
        AssertMenuShape(app, menu);
        Assert.Equal(SeveralCommitsMenuHeight, menu.Bounds.Height, 2);
        AssertMenuAtPointer(menu, three);
        AssertMenuColours(app, menu);
        await AssertMultiSelectionAsync(app);

        // Step 3: the dialog, filled with git's squash message of Three and Four.
        app.ClickMenuItem(SquashItem);
        await AssertMessageDialogAsync(app, SquashTitle, SquashSummary, SquashDescription, SquashText);
        var dialogPicture = Shot(app, "b8b-squash-dialog");
        Assert.False(app.IsMenuOpen, "A menu is open over the dialog.");
        AssertOverBackdrop(app);
        AssertPrimaryConfirm(app, dialogPicture);
        AssertGraph(app, GI, Lanes, showsDate: true);
        AssertSelectedRows(app, null, 1, 2);
        await AssertGitUnchangedAsync(repo);

        // Step 4: Squash.
        var logMark = app.LogMark();
        var callMark = app.GitCalls().Count;
        using var statuses = app.RecordStatusTexts();
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await WaitForOperationAsync(app, repo, B8Title, logMark);
        Assert.Equal(1, app.CountLogLines(AppLogFile.OperationEnded(Repository, B8Title, OperationOutcome.Succeeded, B8Status), logMark));
        await app.WaitForAsync(
            () => Graph(app).Data is { IsComplete: true } graph && graph.Count == GB8.Count && graph.CommitAt(0).Sha == B8FiveSha,
            "the graph G-B8");
        await AssertDetailsAsync(app, B8SquashedDetails);
        var squashed = Shot(app, "b8c-squashed");
        await AssertNoDialogAsync(app);
        Assert.Null(app.BannerBounds);
        AssertGraph(app, GB8, Lanes, showsDate: true, picture: squashed);
        AssertSelectedRows(app, squashed, 1);
        AssertGraphSelection(app, B8ThreeSha);
        AssertStatus(app, B8Status);
        Assert.Contains(B8Running, statuses.Texts);
        AssertNoClippedText(app);

        // Git: one git rebase -i from Three's parent; Five replayed on the squashed commit.
        var call = Assert.Single(app.GitCalls().Skip(callMark), record => record.CommandText.StartsWith("git rebase", StringComparison.Ordinal));
        Assert.Equal((RebaseCall(FixOneSha), 0), (call.CommandText, call.ExitCode ?? -1));
        Assert.Equal(B8FiveSha, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal([B8FiveSha, B8ThreeSha, FixOneSha, TwoSha, OneSha, BaseSha, InitialSha], await GitLinesAsync(repo, "log", "--format=%H", "main"));
        Assert.Equal(B8FiveMessage, await RawMessageAsync(repo, B8FiveSha));
        Assert.Equal((ScenarioTime(7), AppCommitTime), await DatesAsync(repo, B8FiveSha));
        Assert.Equal([B8ThreeSha], await ParentsAsync(repo, B8FiveSha));
        Assert.Equal(B8ThreeMessage, await RawMessageAsync(repo, B8ThreeSha));
        Assert.Equal((ScenarioTime(5), AppCommitTime), await DatesAsync(repo, B8ThreeSha));
        Assert.Equal([FixOneSha], await ParentsAsync(repo, B8ThreeSha));
        Assert.Equal(["A\tsrc/four.txt", "A\tsrc/three.txt"], await ChangesAsync(repo, B8ThreeSha));
        Assert.Equal(["A\tsrc/five.txt"], await ChangesAsync(repo, B8FiveSha));
        foreach (var sha in new[] { B8FiveSha, B8ThreeSha })
        {
            Assert.Equal(Author + " / " + Author, await PeopleAsync(repo, sha));
        }

        Assert.Equal([CleanStatus], await StatusAsync(repo));
        AssertNoRebaseFolders(repo);
        Assert.Empty(await GitLinesAsync(repo, "for-each-ref", "refs/rewritten/"));
        await AssertRebaseBackupAsync(repo, FiveSha);
    }

    /// <summary>
    /// The right panel's multi-selection view (phase 3's 4h) of Four and Three: "2 commits
    /// selected", the commits in row order (short SHA in the code font, secondary colour), the hint.
    /// </summary>
    private static async Task AssertMultiSelectionAsync(ShellDriver app)
    {
        await app.WaitForAsync(() => Shown(app, "MultiSelectionView") is not null, "the multi-selection view");
        var view = app.FindByAutomationId("MultiSelectionView");
        Assert.Equal(MultiSelectionTitle, ((TextBlock)app.FindByAutomationId(view, "MultiSelectionTitle")).Text);
        var rows = view.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == "MultiSelectionItem")
            .OrderBy(control => app.BoundsOf(control).Y)
            .ToList();
        Assert.Equal(MultiSelectionItems, rows.Select(row => string.Join(' ', TextsIn(row))));
        var palette = PaletteOf(app);
        foreach (var row in rows)
        {
            var sha = row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "MultiSelectionSha");
            Assert.Equal(palette.TextSecondary, (sha.Foreground as ISolidColorBrush)?.Color);
        }

        Assert.Equal(MultiSelectionHint, ((TextBlock)app.FindByAutomationId(view, "MultiSelectionHint")).Text);
    }
}
