using System.Globalization;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Part U's expected values against the report and the app (docs/test-reports/phase-8a.md, part U's
/// conventions; docs/phase-8a-design.md 1.12, 9.3.5, 9.5): the names equal <c>AutomationNames</c>'
/// constants and formats; the rows' words equal what <c>GraphRowSpeech</c> builds from the
/// scenario's commits; the tooltips and keys the names keep are the app's, in Windows' words; every
/// picture name is of design 0.3's form and named once; the moves follow from the display rows;
/// launch 13 reads method 2's rows.
/// </summary>
public sealed class Phase8aExpectationTestsU
{
    private static readonly DateDisplay Utc = new(TimeZoneInfo.Utc);

    private static readonly KeyTexts Windows = new(CommandKey.Ctrl);

    [Fact]
    public void The_picture_names_are_of_the_reports_form_and_each_is_named_once()
    {
        Assert.Equal(6, U.Pictures.Count);
        Assert.All(U.Pictures, name => Assert.Matches(PictureName(), name));
        Assert.All(U.Pictures, name => Assert.StartsWith("u", name, StringComparison.Ordinal));
        Assert.Equal(U.Pictures.Count, U.Pictures.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_names_are_the_apps_constants()
    {
        Assert.Equal(
            [U.PreviousConflict, U.NextConflict, U.PreviousFile, U.NextFile, U.CloseResolver, U.CloseDiff, U.PreviousMatch, U.NextMatch, U.CloseSearch,
                U.CloseActivityLog, U.CloseComparison, U.SwapFromAndTo, U.DismissRestoreOffer, U.ActivityLog, U.ResizeLeftPanel, U.ResizeRightPanel, U.ResizeActivityLog],
            [AutomationNames.PreviousConflict, AutomationNames.NextConflict, AutomationNames.PreviousFile, AutomationNames.NextFile, AutomationNames.CloseResolver,
                AutomationNames.CloseDiff, AutomationNames.PreviousMatch, AutomationNames.NextMatch, AutomationNames.CloseSearch, AutomationNames.CloseActivityLog,
                AutomationNames.CloseComparison, AutomationNames.SwapFromAndTo, AutomationNames.DismissRestoreOffer, AutomationNames.ActivityLog,
                AutomationNames.ResizeLeftPanel, AutomationNames.ResizeRightPanel, AutomationNames.ResizeActivityLog]);
        Assert.Equal(
            [U.StageFileFormat, U.DiscardFileFormat, U.UnstageFileFormat, U.ShowRefFormat, U.StopSoloingRefFormat],
            [AutomationNames.StageFileFormat, AutomationNames.DiscardFileFormat, AutomationNames.UnstageFileFormat, AutomationNames.ShowRefFormat, AutomationNames.StopSoloingRefFormat]);

        // The rows' names: the formats over the path, or over the ref's full name.
        Assert.Equal(U.StageReadme, Format(AutomationNames.StageFileFormat, U.Readme));
        Assert.Equal(U.DiscardReadme, Format(AutomationNames.DiscardFileFormat, U.Readme));
        Assert.Equal(U.StageUntracked, Format(AutomationNames.StageFileFormat, U.Untracked));
        Assert.Equal(U.DiscardUntracked, Format(AutomationNames.DiscardFileFormat, U.Untracked));
        Assert.Equal(U.UnstageCore, Format(AutomationNames.UnstageFileFormat, U.Core));
        Assert.Equal(U.UnstageUntracked, Format(AutomationNames.UnstageFileFormat, U.Untracked));
        Assert.Equal(U.ShowFeatureAlpha, Format(AutomationNames.ShowRefFormat, "feature/alpha"));
        Assert.Equal(U.ShowOriginFeatureAlpha, Format(AutomationNames.ShowRefFormat, "origin/feature/alpha"));
        Assert.Equal(U.StopSoloingFeatureFf, Format(AutomationNames.StopSoloingRefFormat, "feature/ff"));
        Assert.All(U.RowNames, row => Assert.Equal(row.Name, Format(FormatOf(row.Id), row.Row)));

        // Each name of the table once, and every row of 1.12's table named.
        Assert.Equal(U.FixedNames.Count, U.FixedNames.Select(named => named.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(17, U.FixedNames.Count);
    }

    [Fact]
    public void The_tooltips_and_keys_the_names_keep_are_the_apps_in_windows_words()
    {
        var tips = U.FixedNames.ToDictionary(named => named.Id, named => (named.ToolTip, named.Key));

        Assert.Equal((ShellToolTips.PreviousConflictFor(Windows), Windows.Of(AppCommand.PreviousConflict)), tips["ResolverPreviousConflict"]);
        Assert.Equal((ShellToolTips.NextConflictFor(Windows), Windows.Of(AppCommand.NextConflict)), tips["ResolverNextConflict"]);
        Assert.Equal((ShellToolTips.PreviousFileFor(Windows), Windows.Of(AppCommand.PreviousFile)), tips["ResolverPreviousFile"]);
        Assert.Equal((ShellToolTips.NextFileFor(Windows), Windows.Of(AppCommand.NextFile)), tips["ResolverNextFile"]);
        Assert.Equal((ShellToolTips.CloseResolverFor(Windows), Windows.Of(AppCommand.Close)), tips["ResolverCloseButton"]);
        Assert.Equal((SearchViewModel.PreviousToolTipFor(Windows), Windows.Of(AppCommand.PreviousMatch)), tips["SearchPrevious"]);
        Assert.Equal((SearchViewModel.NextToolTipFor(Windows), Windows.Of(AppCommand.NextMatch)), tips["SearchNext"]);
        Assert.Equal((SearchViewModel.CloseToolTipFor(Windows), (string?)null), tips["SearchClose"]);
        Assert.Equal((CompareViewModel.SwapToolTip, (string?)null), tips["CompareSwap"]);
        Assert.Equal((CompareViewModel.CloseToolTip, (string?)null), tips["CompareClose"]);
        Assert.Equal((ShellToolTips.HideActivityLogFor(Windows), Windows.Of(AppCommand.ToggleActivityLog)), tips["ActivityLogToggle"]);
        Assert.All(
            U.FixedNames.Where(named => named.HelpText == U.HelpTextSource.None),
            named => Assert.Equal(((string?)null, (string?)null), (named.ToolTip, named.Key)));
        Assert.Equal(["LeftSplitter", "RightSplitter", "ActivityLogSplitter"], U.FixedNames.Where(named => named.HelpText == U.HelpTextSource.None).Select(named => named.Id));
    }

    [Fact]
    public void The_rows_words_are_what_GraphRowSpeech_builds_from_the_scenarios_commits()
    {
        Assert.Equal(U.MainWork.Name, Name("88d6b91", "Main work", 4, Label("main", RefLabelKind.CurrentBranch), Label("origin/main", RefLabelKind.RemoteBranch), Label("v1.1", RefLabelKind.Tag)));
        Assert.Equal(U.AlphaTwo.Name, Name("ce5ea6a", "Alpha two", 3, Label("feature/alpha", RefLabelKind.LocalBranch), Label("origin/feature/alpha", RefLabelKind.RemoteBranch)));
        Assert.Equal(U.AlphaOne.Name, Name("dff8fbf", "Alpha one", 2));
        Assert.Equal(U.FastForwardMe.Name, Name("d93814d", "Fast-forward me", 5, Label("feature/ff", RefLabelKind.LocalBranch)));
        Assert.Equal(U.ConflictingChange.Name, Name("078ef05", "Conflicting change", 6, Label("feature/conflict", RefLabelKind.LocalBranch)));
        Assert.Equal(U.Stash.Name, GraphRowSpeech.Name(Commit("0e95aca", "On main: Experiment", 7, CommitKind.Stash), [new("stash@{0}", RefLabelKind.Stash)], Utc.Format));
        Assert.Equal(U.OursEdits.Name, Name("da31c97", "Ours edits", 9, Label("main", RefLabelKind.CurrentBranch)));
        Assert.Equal(U.KindsEdits.Name, Name("3a9de3e", "Kinds edits", 8, Label("feature/kinds", RefLabelKind.LocalBranch)));
        Assert.Equal(U.WorkingRow.Name, GraphRowSpeech.WorkingRowName(U.WorkingRow.Subject));
        Assert.Equal(U.ConflictWorkingRow.Name, GraphRowSpeech.WorkingRowName(U.ConflictWorkingRow.Subject));

        // The hints: the short id, the count while two or more are selected.
        foreach (var row in new[] { U.MainWork, U.AlphaTwo, U.AlphaOne, U.FastForwardMe, U.ConflictingChange, U.OursEdits, U.KindsEdits })
        {
            Assert.Equal(row.Hint, GraphRowSpeech.Hint(Commit(row.Hint["Commit ".Length..], row.Subject, 0), 1, matchesSearch: false));
        }

        Assert.Equal(U.Stash.Hint, GraphRowSpeech.Hint(Commit("0e95aca", "On main: Experiment", 7, CommitKind.Stash), 1, matchesSearch: false));
        Assert.Equal(U.AlphaTwoOfTwo, GraphRowSpeech.Hint(Commit("ce5ea6a", "Alpha two", 3), 2, matchesSearch: false));
        Assert.Equal(U.AlphaOneOfThree, GraphRowSpeech.Hint(Commit("dff8fbf", "Alpha one", 2), 3, matchesSearch: false));
        Assert.Equal(string.Empty, U.WorkingRow.Hint);
        Assert.Equal(U.ThreeCommitsSelected, Format(GraphRowSpeech.SelectedCountFormat, "3"));
        Assert.Null(U.FirstOldName);
    }

    [Fact]
    public void The_moves_follow_from_the_display_rows()
    {
        // Method 1: from Main work (display row 4), each key one display row on; Shift extends.
        var at = U.MainWork.DisplayRow;
        foreach (var move in U.OpsMoves)
        {
            at += move.Key.EndsWith("Down", StringComparison.Ordinal) ? 1 : -1;
            Assert.Equal(at, move.Row.DisplayRow);
        }

        Assert.Equal(0, at);
        Assert.Equal([1, 1, 2, 3, 1, 1, 1, 1, 1, 0], U.OpsMoves.Select(move => move.Selected));
        Assert.Equal(6, U.OpsMoves.Skip(4).Count(move => move.Key == "Up"));

        // Method 2 and launch 13: from Ours edits (display row 1), Up, then Down twice.
        at = U.OursEdits.DisplayRow;
        foreach (var move in U.MergeMoves)
        {
            at += move.Key == "Down" ? 1 : -1;
            Assert.Equal(at, move.Row.DisplayRow);
        }

        Assert.Equal(["Up", "Down", "Down"], U.MergeMoves.Select(move => move.Key));
    }

    [Fact]
    public void Launch_13_reads_method_2s_rows_and_the_names_of_the_table()
    {
        Assert.Equal(("Check_U_graph_row_and_names", "Check_U1_U3_stopped_merge_rows_and_resolver", true, (1100, 700)), (U.GraphRowLaunch.Method, U.GraphRowLaunch.Counterpart, U.GraphRowLaunch.Dark, U.GraphRowLaunch.Size));
        Assert.Equal((U.Untracked, U.UntrackedContent), (U.GraphRowLaunch.File, U.GraphRowLaunch.FileContent));
        Assert.Equal(TimeSpan.FromSeconds(5), U.EventWait);
        Assert.All(U.LaunchNameIds, id => Assert.Contains(U.FixedNames, named => named.Id == id));
        Assert.Equal(U.ResolverIds.Concat(U.LogAndSplitterIds).Concat(U.SearchIds), U.LaunchNameIds);
        Assert.Equal([U.StageUntracked, U.DiscardUntracked, U.UnstageUntracked], U.LaunchRowNames.Select(row => row.Name));
    }

    private static string Format(string format, string value) => string.Format(CultureInfo.InvariantCulture, format, value);

    private static string FormatOf(string id) => id switch
    {
        "RowStageButton" => AutomationNames.StageFileFormat,
        "RowDiscardButton" => AutomationNames.DiscardFileFormat,
        "RowUnstageButton" => AutomationNames.UnstageFileFormat,
        "RefHiddenToggle" => AutomationNames.ShowRefFormat,
        _ => AutomationNames.StopSoloingRefFormat,
    };

    private static RefLabel Label(string name, RefLabelKind kind) => new(name, kind, kind switch
    {
        RefLabelKind.CurrentBranch or RefLabelKind.LocalBranch => "refs/heads/" + name,
        RefLabelKind.RemoteBranch => "refs/remotes/" + name,
        _ => "refs/tags/" + name,
    });

    private static string Name(string sha, string subject, int minute, params RefLabel[] labels) =>
        GraphRowSpeech.Name(Commit(sha, subject, minute), labels, Utc.Format);

    /// <summary>A commit of the scenarios: by Test Author on 2026-01-01 at 12:<paramref name="minute"/> UTC, its id padded to 40 digits.</summary>
    private static CommitInfo Commit(string sha, string subject, int minute, CommitKind kind = CommitKind.Commit)
    {
        var date = new DateTimeOffset(2026, 1, 1, 12, minute, 0, TimeSpan.Zero);
        return new(sha.PadRight(40, '0'), [], "Test Author", "test@example.com", date, date, subject, kind);
    }
}
