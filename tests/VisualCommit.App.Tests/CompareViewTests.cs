using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Shell;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The comparison in the right panel in the whole app on the history scenario
/// (docs/phase-5-design.md 6, D162, D177, report part F's F12 and F13, FD9 to FD12, FD20, FD21):
/// two selected commits at once, the file's diff from the side compared from, Swap, the list's
/// menu, the links, a third commit and a single one; and the driver's helpers for them
/// (ShellDriver.Phase5.Find.cs).
/// </summary>
public class CompareViewTests
{
    private const string ReadWholeLines = "c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9";
    private const string RenameTheParser = "0575c7f6631b3ba6c9d623f53b28489405230cfd";

    [AvaloniaFact]
    public async Task Two_selected_commits_compare_at_once_and_a_third_or_a_single_one_ends_it()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.HistoryAsync();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();

        // F12 step 1: row 7, then the command modifier on row 0.
        app.Click(app.GraphRowPoint(7));
        app.Click(app.GraphRowPoint(0), app.CommandModifier);
        await app.WaitForCompareAsync();
        var compare = app.Compare()!;
        Assert.Equal("Compare", compare.Title);
        Assert.Equal("From 0575c7f Rename the parser to reader", compare.From);
        Assert.Equal("To c0bc2dd Read whole lines", compare.To);
        Assert.True(compare.FromEnabled);
        Assert.True(compare.ToEnabled);
        Assert.Null(compare.Counts);
        Assert.False(compare.AllDifferencesShown);
        Assert.True(compare.SwapShown);
        Assert.False(compare.CloseShown);
        Assert.Equal("Changed files (4)", compare.FilesTitle);
        Assert.Equal(["M README.md", "M notes.md docs", "M Config.cs src", "M Reader.cs src"], Files(compare));
        Assert.True(repository.Details.IsMultiSelection);
        Assert.False(app.FindByAutomationId("DetailsPlaceholder").IsEffectivelyVisible);
        Assert.Contains(app.GitCalls(), call => call.CommandText == $"git rev-parse {RenameTheParser}^{{commit}} {ReadWholeLines}^{{commit}} --");
        Assert.Contains(app.GitCalls(), call => call.CommandText == $"git diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id {RenameTheParser} {ReadWholeLines}");
        Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.StartsWith("git merge-base", StringComparison.Ordinal) || call.CommandText.StartsWith("git rev-list --left-right", StringComparison.Ordinal));

        // Its root is a control element, a pane named "Compare", in the right panel inside its 1-px left border.
        var view = app.Window.GetVisualDescendants().OfType<CompareView>().Single(candidate => candidate.IsEffectivelyVisible);
        Assert.Equal("Compare", ControlAutomationPeer.CreatePeerForElement(view).GetName());
        Assert.Equal(701, app.BoundsOf(view).X, 1);

        // Step 2: a file's diff in the graph area, from From to To; its row keeps the selection.
        app.ClickCompareFile("src/Reader.cs");
        Assert.True(repository.IsDiffOpen);
        Assert.Equal("0575c7f..c0bc2dd", repository.Diff!.SideLabel);
        Assert.Equal(new DiffTarget(DiffSide.Commit, "src/Reader.cs", FileChangeKind.Modified, null, Commit: ReadWholeLines, Parent: RenameTheParser), repository.Diff.Target);
        Assert.True(Assert.Single(app.Compare()!.Files, row => row.Key == "src/Reader.cs").IsSelected);
        Assert.Equal(2, repository.Selection.Count);

        // Step 3: Search closes the diff; the comparison stays and its row loses the selection.
        repository.OpenSearch();
        Assert.False(repository.IsDiffOpen);
        Assert.DoesNotContain(app.Compare()!.Files, row => row.IsSelected);
        app.PressKey(Key.Escape);

        // Step 4: Swap reads it the other way round.
        app.Click(app.FindByAutomationId("CompareSwap"));
        await app.WaitForCompareAsync();
        compare = app.Compare()!;
        Assert.Equal("From c0bc2dd Read whole lines", compare.From);
        Assert.Equal("To 0575c7f Rename the parser to reader", compare.To);
        Assert.Equal(["M README.md", "M notes.md docs", "M Config.cs src", "M Reader.cs src"], Files(compare));
        Assert.Contains(app.GitCalls(), call => call.CommandText == $"git diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id {ReadWholeLines} {RenameTheParser}");

        // The list's file menu, a right-click opening it only (FD21).
        app.RightClick(app.CompareFileRow("src/Reader.cs"));
        var menu = app.Menu();
        Assert.Equal("Reader.cs", menu.Header);
        Assert.Contains("Open file", menu.Lines());
        Assert.False(repository.IsDiffOpen);
        Assert.DoesNotContain(app.Compare()!.Files, row => row.IsSelected);
        app.PressKey(Key.Escape);

        // Step 5: the To link reveals row 7 without selecting it alone.
        app.Click(app.FindByAutomationId("CompareTo"));
        Assert.Equal(2, repository.Selection.Count);
        Assert.NotNull(app.Compare());

        // Step 6: a third commit shows phase 3's list; taken out again, a new comparison in the graph's order (FD9).
        app.Click(app.GraphRowPoint(3), app.CommandModifier);
        Assert.Null(app.Compare());
        Assert.Equal("3 commits selected", ((TextBlock)app.FindByAutomationId("MultiSelectionTitle")).Text);
        app.Click(app.GraphRowPoint(3), app.CommandModifier);
        await app.WaitForCompareAsync();
        Assert.Equal("From 0575c7f Rename the parser to reader", app.Compare()!.From);

        // Step 7: a single row ends it.
        app.Click(app.GraphRowPoint(3));
        Assert.Null(app.Compare());
        Assert.Null(repository.MultiSelection);
        Assert.False(repository.Details.IsMultiSelection);
        await app.WaitForAsync(() => repository.Details.Subject == "Merge branch 'feature/readme'", "the details of the merge");
    }

    private static List<string> Files(CompareSnapshot compare) =>
        [.. compare.Files.Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => part.Length > 0)))];
}
