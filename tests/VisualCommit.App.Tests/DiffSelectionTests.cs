using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.Views.Diff;
using VisualCommit.Core;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Which lines the diff view's actions act on: the lines selected in the text, never lines left
/// over from another diff. Real git on the changes scenario.
/// </summary>
public class DiffSelectionTests
{
    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

    private static readonly ChangedFile Calculator = new("src/Calculator.cs", FileChangeKind.Modified);

    [Fact]
    public async Task Selected_lines_are_kept_when_the_same_lines_are_read_again_and_dropped_when_they_change()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var viewModel = await OpenAsync(repo);
        var diff = await OpenCalculatorAsync(viewModel);
        var selected = new HashSet<DiffLineRef> { new(0, 3) };
        diff.SelectedChanges = selected;

        await diff.LoadAsync(diff.Target);
        Assert.Same(selected, diff.SelectedChanges);

        File.AppendAllText(Path.Combine(repo.Path, "src", "Calculator.cs"), "// More\n");
        await diff.LoadAsync(diff.Target);
        Assert.Empty(diff.SelectedChanges);
    }

    [AvaloniaFact]
    public async Task A_diff_that_leaves_the_view_or_comes_back_to_it_has_no_lines_selected()
    {
        // Two tabs on the same repository, each with Calculator.cs's diff open.
        using var repo = await Scenarios.ChangesAsync();
        using var first = await OpenAsync(repo);
        using var second = await OpenAsync(repo);
        var a = await OpenCalculatorAsync(first);
        var b = await OpenCalculatorAsync(second);
        var view = new DiffView { DataContext = a, Width = 900, Height = 600 };
        using var host = DiffHost.Show(view, 900, 600, ThemeVariant.Dark);
        host.Capture();
        var text = view.FindControl<DiffTextView>("DiffText")!;

        host.Click(text, text.LineNumberPoint(new DiffLineRef(0, 3))!.Value);
        Assert.Equal([new DiffLineRef(0, 3)], a.SelectedChanges);
        Assert.True(a.HasSelectedLines);

        view.DataContext = b;
        host.Capture();
        Assert.Empty(a.SelectedChanges);
        Assert.Empty(b.SelectedChanges);
        Assert.Empty(text.SelectedChanges);

        view.DataContext = a;
        host.Capture();
        Assert.Empty(a.SelectedChanges);
        Assert.False(a.HasSelectedLines);
    }

    [Fact]
    public async Task A_second_click_while_a_hunk_is_being_staged_does_nothing()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var viewModel = await OpenAsync(repo);
        var diff = await OpenCalculatorAsync(viewModel);

        var first = diff.RunHunkActionAsync(0, LineAction.Stage);
        var second = diff.RunHunkActionAsync(0, LineAction.Stage);
        await Task.WhenAll(first, second);

        Assert.Null(viewModel.Changes.ErrorText);
        Assert.Equal(
            ["@@ -12,7 +12,7 @@ public sealed class Calculator"],
            (await repo.GitAsync("diff", "--cached", "--", "src/Calculator.cs")).StandardOutput.Split('\n').Where(line => line.StartsWith("@@", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_type_change_has_its_files_actions_but_none_on_hunks_or_lines()
    {
        using var repo = await Scenarios.LinearAsync();
        var link = (await repo.GitWithInputAsync("README.md", "hash-object", "-w", "--stdin")).StandardOutput.Trim();
        await repo.GitAsync("update-index", "--cacheinfo", $"120000,{link},src/greeting.txt");
        using var viewModel = await OpenAsync(repo);

        viewModel.OpenDiff(viewModel.Status.Staged.Single(), staged: true);
        var diff = viewModel.Diff!;
        await diff.LoadAsync(diff.Target);
        diff.SelectedChanges = new HashSet<DiffLineRef> { new(0, 0) };

        Assert.Equal(DiffBody.Text, diff.Body);
        Assert.Equal(HunkActions.None, diff.HunkActions);
        Assert.False(diff.HasSelectedLines);
        Assert.False(diff.ShowsLineUnstage);
        Assert.True(diff.ShowsFileUnstage);
    }

    private static async Task<DiffViewModel> OpenCalculatorAsync(RepositoryViewModel viewModel)
    {
        viewModel.OpenDiff(Calculator, staged: false);
        var diff = viewModel.Diff!;
        await diff.LoadAsync(diff.Target);
        Assert.Equal(DiffBody.Text, diff.Body);
        return diff;
    }

    private static async Task<RepositoryViewModel> OpenAsync(TempRepo repo)
    {
        var repository = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        await viewModel.StartAsync();
        return viewModel;
    }
}
