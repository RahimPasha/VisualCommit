using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase0;
using Xunit;

namespace VisualCommit.VisualTests.Phase2;

/// <summary>
/// What phase 2's checks compare against, copied from docs/test-reports/phase-2.md ("What phase 2
/// must show" and the checks), not read from the app, so the two are checked against each other.
/// </summary>
public static class Phase2Expectations
{
    /// <summary>The changes scenario's unstaged list, flat: status letter, name, folder, origin.</summary>
    public static readonly string[] UnstagedFlat =
    [
        "M README.md",
        "M logo.png assets",
        "M blob.bin data",
        "M large.txt data",
        "A guide.md docs",
        "D old-notes.txt docs",
        "M Calculator.cs src",
    ];

    /// <summary>The changes scenario's staged list, flat.</summary>
    public static readonly string[] StagedFlat =
    [
        "M README.md",
        "A settings.json config",
        "R helpers.py src renamed from src/util.py",
    ];

    /// <summary>Check 14's commit: what plain git makes from the same index, message, identity and date ("Scenario repos").</summary>
    public const string FirstCommit = "f976a24c1cb298e1c293542624fb96fe0b2d81c5";

    /// <summary>Check 15's amend of <see cref="FirstCommit"/>.</summary>
    public const string AmendedCommit = "f9add9d8b5d8ae985965e5a4215dd46b0d0a0517";

    public const double PanelHeaderHeight = 36;
    public const double SectionHeaderHeight = 34;
    public const double CommitAreaHeight = 212;
    public const double FileRowHeight = 24;

    /// <summary>The colours phase 2 adds, and the hover background it uses.</summary>
    public sealed record DiffPalette(
        Color Hover,
        Color Added,
        Color AddedWord,
        Color Removed,
        Color RemovedWord,
        Color Hunk,
        Color Filler,
        Color Disabled);

    public static DiffPalette Dark { get; } = new(
        Hover: Color.Parse("#2F3542"),
        Added: Color.Parse("#1A2E24"),
        AddedWord: Color.Parse("#2B5A3F"),
        Removed: Color.Parse("#331D23"),
        RemovedWord: Color.Parse("#6A2D37"),
        Hunk: Color.Parse("#1C2130"),
        Filler: Color.Parse("#181A20"),
        Disabled: Color.Parse("#5F6775"));

    public static DiffPalette Light { get; } = new(
        Hover: Color.Parse("#DEE2E9"),
        Added: Color.Parse("#E6F6EC"),
        AddedWord: Color.Parse("#B4E5C6"),
        Removed: Color.Parse("#FBE9EB"),
        RemovedWord: Color.Parse("#F3BAC1"),
        Hunk: Color.Parse("#EEF0FB"),
        Filler: Color.Parse("#F3F4F6"),
        Disabled: Color.Parse("#A1A8B3"));

    /// <summary>
    /// The stage panel's parts as "The stage panel" places them, without a restore bar: the
    /// header, the unstaged header and list, the staged header and list, the commit area, the
    /// lists sharing what is left half each (2 pixels of tolerance).
    /// </summary>
    public static void AssertStagePanelLayout(ShellDriver app, double height)
    {
        ArgumentNullException.ThrowIfNull(app);
        var panel = app.BoundsOf(app.FindByAutomationId("StagePanel"));
        var content = height - ShellExpectations.TabStripHeight - ShellExpectations.ToolbarHeight - ShellExpectations.StatusBarHeight;
        Assert.Equal(content, panel.Height, ShellExpectations.SizeTolerance);
        Assert.Equal(ShellExpectations.RightPanelWidth, panel.Width, ShellExpectations.SizeTolerance);

        var list = (content - PanelHeaderHeight - CommitAreaHeight - (2 * SectionHeaderHeight)) / 2;
        var unstaged = app.BoundsOf(app.Find<ItemsControl>("UnstagedFileList"));
        var staged = app.BoundsOf(app.Find<ItemsControl>("StagedFileList"));
        var summary = app.BoundsOf(app.Find<TextBox>("CommitSummary"));
        var commit = app.BoundsOf(app.Find<Button>("CommitButton"));

        Assert.Equal(panel.Top + PanelHeaderHeight + SectionHeaderHeight, unstaged.Top, ShellExpectations.SizeTolerance);
        Assert.Equal(list, unstaged.Height, ShellExpectations.SizeTolerance);
        Assert.Equal(unstaged.Bottom + SectionHeaderHeight, staged.Top, ShellExpectations.SizeTolerance);
        Assert.Equal(list, staged.Height, ShellExpectations.SizeTolerance);
        Assert.Equal(panel.Bottom - CommitAreaHeight + 12 + 1, summary.Top, ShellExpectations.SizeTolerance);
        Assert.Equal(32, summary.Height, ShellExpectations.SizeTolerance);
        Assert.Equal(32, commit.Height, ShellExpectations.SizeTolerance);
        Assert.Equal(panel.Bottom - 12, commit.Bottom, ShellExpectations.SizeTolerance);
        Assert.Equal(panel.Width - 1 - 24, commit.Width, ShellExpectations.SizeTolerance);
    }
}
