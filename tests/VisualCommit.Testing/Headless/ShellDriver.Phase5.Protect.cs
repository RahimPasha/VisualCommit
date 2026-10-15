using System.Globalization;
using Avalonia.Controls;
using VisualCommit.Core.Git;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Part P's additions to the driver (docs/phase-5-design.md 9, 15.2; the report's part P,
/// package WP4): the status bar's text as it is laid out (part P asserts the automatic stash's
/// result whole, PD14, where a cut text's tooltip would hide the cut from
/// <c>AssertNoClippedText</c>), the stash calls made since a mark (the conventions' "no
/// <c>git stash</c> call"), and the wait for a status read with given counts after a change made
/// outside the app (PD16).
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>
    /// The status bar's operation text (<c>OperationStatus</c>) as it is laid out now: its text,
    /// whether it is drawn whole (no line shortened with "…" and as wide as its text needs), and
    /// its tooltip.
    /// </summary>
    public StatusTextLayout OperationStatusLayout()
    {
        Settle();
        var text = Find<TextBlock>("OperationStatus");
        var layout = text.TextLayout;
        var whole = !layout.TextLines.Any(line => line.HasCollapsed)
            && layout.WidthIncludingTrailingWhitespace <= text.Bounds.Width + 0.5;
        return new StatusTextLayout(text.Text ?? string.Empty, whole, ToolTip.GetTip(text) as string, text.Bounds.Width);
    }

    /// <summary>
    /// The git calls made since <paramref name="callMark"/> calls had been recorded
    /// (<see cref="GitCalls"/><c>().Count</c> before the step) that push, pop or apply a stash: those
    /// whose arguments start with <c>stash push</c>, <c>stash pop</c> or <c>stash apply</c> (part P's
    /// "no <c>git stash</c> call"; <c>git stash list</c>, which every refs read runs, is none).
    /// </summary>
    public IReadOnlyList<GitCallRecord> StashCallsSince(int callMark)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(callMark);
        return [.. GitCalls().Skip(callMark).Where(call => IsStashWrite(call.CommandText))];

        static bool IsStashWrite(string command) =>
            command.StartsWith("git stash push", StringComparison.Ordinal)
            || command.StartsWith("git stash pop", StringComparison.Ordinal)
            || command.StartsWith("git stash apply", StringComparison.Ordinal);
    }

    /// <summary>
    /// After a change made outside the app (the report's PD16): waits for a status read of the
    /// active tab's repository logged after <paramref name="mark"/> (<see cref="LogMark"/>) with
    /// <paramref name="staged"/> staged and <paramref name="unstaged"/> unstaged files
    /// (<see cref="AppLogFile.WorkingTreeRead"/>), then for its queue to be idle; and, when
    /// <paramref name="until"/> is given, until the repository's status satisfies it (P24 step 7,
    /// whose counts do not change). <paramref name="timeout"/> bounds each wait
    /// (<see cref="DefaultWaitTimeout"/> when null; the checks pass their <c>OutsideChangeTimeout</c>).
    /// </summary>
    /// <exception cref="TimeoutException">A wait timed out.</exception>
    public async Task WaitForStatusReadAsync(int staged, int unstaged, int mark, Func<WorkingTreeStatus, bool>? until = null, TimeSpan? timeout = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(staged);
        ArgumentOutOfRangeException.ThrowIfNegative(unstaged);
        var repository = Session.Shell.ActiveTab.Repository
            ?? throw new InvalidOperationException("The active tab has no repository open.");
        var pattern = AppLogFile.WorkingTreeRead(repository.Name);
        var expected = (staged.ToString(CultureInfo.InvariantCulture), unstaged.ToString(CultureInfo.InvariantCulture));
        await WaitForAsync(
            () => LogLinesSince(mark).Select(line => pattern.Match(line)).Any(match => match.Success && (match.Groups["staged"].Value, match.Groups["unstaged"].Value) == expected),
            string.Create(CultureInfo.InvariantCulture, $"a status read of {repository.Name} with {staged} staged and {unstaged} unstaged"),
            timeout);
        await WaitForIdleAsync(repository, timeout);
        if (until is not null)
        {
            await WaitForAsync(() => until(repository.Status), $"the status of {repository.Name} the step names", timeout);
        }
    }
}

/// <summary>The status bar's operation text as laid out (<see cref="ShellDriver.OperationStatusLayout"/>).</summary>
/// <param name="Text">The text it shows.</param>
/// <param name="IsWhole">Drawn whole: not shortened with "…", and as wide as the text needs.</param>
/// <param name="ToolTip">Its tooltip's text, or null.</param>
/// <param name="Width">The width it was given, in logical pixels.</param>
public sealed record StatusTextLayout(string Text, bool IsWhole, string? ToolTip, double Width);
