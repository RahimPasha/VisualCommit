using System.Text;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The user guide's page of keys (docs/guide/src/reference/shortcuts.md) holds, between its two
/// markers, the tables made from the app's own key table (<see cref="AppShortcuts.Groups"/> on
/// Windows and Linux and on macOS), so the guide and the settings screen's list cannot drift: per
/// group a heading <c>### {Title}</c> and a table <c>| Action | Windows and Linux | macOS |</c>,
/// then the group's note once when both platforms give the same, else each platform's note on a
/// line of its own. A failure prints the region the page should hold. Reads one file; the guide's
/// workflow runs this class (D451).
/// </summary>
public class GuideShortcutsPageTests
{
    /// <summary>The line before the made region.</summary>
    public const string StartMarker = "<!-- shortcuts:start -->";

    /// <summary>The line after the made region.</summary>
    public const string EndMarker = "<!-- shortcuts:end -->";

    private const string CtrlColumn = "Windows and Linux";
    private const string CmdColumn = "macOS";

    [Fact]
    public void The_pages_tables_are_the_apps_key_table()
    {
        var page = Path.GetFullPath(Path.Combine(VisualRun.RepoRoot, "docs", "guide", "src", "reference", "shortcuts.md"));
        var expected = Region();
        Assert.True(File.Exists(page), $"The guide has no page of keys at {page}. Its region, between {StartMarker} and {EndMarker}:\n\n{expected}");

        var text = File.ReadAllText(page).ReplaceLineEndings("\n");
        var start = text.IndexOf(StartMarker, StringComparison.Ordinal);
        var end = text.IndexOf(EndMarker, StringComparison.Ordinal);
        Assert.True(
            start >= 0 && end > start && text.IndexOf(StartMarker, start + 1, StringComparison.Ordinal) < 0 && text.IndexOf(EndMarker, end + 1, StringComparison.Ordinal) < 0,
            $"The page of keys does not hold {StartMarker} once and, after it, {EndMarker} once. Its region:\n\n{expected}");

        var actual = text[(start + StartMarker.Length)..end].Trim('\n');
        Assert.True(actual == expected, $"The page of keys differs from the app's key table. Its region, between the markers, should be:\n\n{expected}");
    }

    [Fact]
    public void The_region_has_a_table_per_group_and_a_row_per_action_on_both_platforms()
    {
        var ctrl = AppShortcuts.Groups(CommandKey.Ctrl);
        var region = Region();

        Assert.Equal(ctrl.Count, region.Split('\n').Count(line => line.StartsWith("### ", StringComparison.Ordinal)));
        Assert.Equal(ctrl.Count, region.Split('\n').Count(line => line == $"| Action | {CtrlColumn} | {CmdColumn} |"));
        Assert.Contains("| Open the user guide | F1 | F1 |", region.Split('\n'));
        Assert.Contains("| Command palette | Ctrl+Shift+P | Cmd+Shift+P |", region.Split('\n'));

        // Repository's note is the same on both platforms: once, with no platform named.
        Assert.Contains("On a dashboard, Fetch and Pull fetch and pull every repository of its workspace.", region.Split('\n'));

        // Help's notes differ (none on Windows and Linux): macOS's alone, on its own line.
        Assert.Contains($"{CmdColumn}: {AppShortcuts.HelpNoteOnMac}", region.Split('\n'));
    }

    /// <summary>The region between the markers, made from the app's key table, without the blank lines next to the markers.</summary>
    public static string Region()
    {
        var ctrl = AppShortcuts.Groups(CommandKey.Ctrl);
        var cmd = AppShortcuts.Groups(CommandKey.Cmd);
        Assert.Equal(ctrl.Select(group => group.Title), cmd.Select(group => group.Title));

        var text = new StringBuilder();
        for (var g = 0; g < ctrl.Count; g++)
        {
            var (onCtrl, onCmd) = (ctrl[g], cmd[g]);
            Assert.Equal(onCtrl.Rows.Select(row => row.Action), onCmd.Rows.Select(row => row.Action));
            if (g > 0)
            {
                text.Append('\n');
            }

            text.Append("### ").Append(onCtrl.Title).Append("\n\n");
            text.Append("| Action | ").Append(CtrlColumn).Append(" | ").Append(CmdColumn).Append(" |\n");
            text.Append("|---|---|---|\n");
            for (var r = 0; r < onCtrl.Rows.Count; r++)
            {
                text.Append("| ").Append(onCtrl.Rows[r].Action)
                    .Append(" | ").Append(onCtrl.Rows[r].Keys)
                    .Append(" | ").Append(onCmd.Rows[r].Keys).Append(" |\n");
            }

            if (onCtrl.Note == onCmd.Note)
            {
                if (onCtrl.Note is not null)
                {
                    text.Append('\n').Append(onCtrl.Note).Append('\n');
                }
            }
            else
            {
                foreach (var (platform, note) in new[] { (CtrlColumn, onCtrl.Note), (CmdColumn, onCmd.Note) })
                {
                    if (note is not null)
                    {
                        text.Append('\n').Append(platform).Append(": ").Append(note).Append('\n');
                    }
                }
            }
        }

        return text.ToString().TrimEnd('\n');
    }
}
