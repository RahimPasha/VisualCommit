// What is where in phase 4's real-window pass (docs/test-reports/phase-4.md: the "Real window"
// column of "Checks", every part's conventions; docs/phase-4-design.md 13.2 to 13.4):
// - Phase4RealWindowPass.cs (this file): the run (Phase4Run: one folder of pictures and one run.txt
//   for the whole pass), launching the real app on a scenario, waiting for an operation's log line,
//   comparing a capture with the scripted picture of the same step, and what the parts share (menus,
//   tooltips, the banner, the details panel, git's state read back).
// - Phase4RealWindowPass.A.cs (A1, A2, A3), .B.cs (B6, B7), .C.cs (C10, C11), .C2.cs (C14, C15,
//   C16): the parts' real-window steps, in the same partial class. A4, A5, B8, B9, C12, C13, C17 and
//   D18 are "n/a" in the report (their reasons are in its Real window column).
// - RealApp.Phase4.cs (the banner), RealApp.Phase4.Rebase.cs (the list), RealApp.Phase4.Conflicts.cs
//   (the resolver), RealApp.Phase4.Checks.cs (this pass's own waits), with phase 3's RealApp.Phase3.cs
//   and RealApp.Checks.cs (menus, drags, tooltips, rows below the banner, left-panel rows).
// - The expectations of the scripted walk-through (VisualTests.Phase4/Phase4Expectations*.cs) are
//   compiled into this project too (see the .csproj), so both harnesses use the same constants and
//   texts.
//
// Conventions for the part files (phase 3's, with phase 4's report):
// - One test method per launch, named after the check whose real-window steps it runs. Every
//   launch is a fresh copy of the check's scenario at 1100×700 in the dark theme (every RW row of
//   the report is at that size; D18, the only 1920×1080 check, is scripted only).
// - Declare the scenario first, then the app: `using var app = Launch(...)`. When a step fails, the
//   app is disposed first, which stops it and releases a mouse button a drag left held
//   (RealApp.Dispose), and then the repository goes. No step may leave a window on the desktop.
// - Pictures: Compare(app, "<name>") with the scripted picture's name (the report's Screenshots
//   column, "real window"); it rests the pointer at the neutral point first unless a menu is open, a
//   drag is under way or the capture is about a tooltip (keepPointer: true), and asserts at most 3%
//   differing pixels (D33, D58). The scripted pictures must exist: run `dotnet test` at the repo
//   root before the pass.
// - After an operation, wait for its log line (ExpectOperation before the input, then Wait), never
//   for a fixed time. Never click "Copy …", "Open file" or "Show in folder".
// - Graph rows: RowPoint (the row constants, below the banner when one is shown, D122); left-panel
//   rows: RealApp.RefRow; a label's centre: the constant the part's expectations hold for it,
//   measured by the scripted run; list rows: RealApp.RebaseRowPoint; the result: RealApp.ResultPoint.
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.VisualTests.Phase4;
using Xunit;

namespace VisualCommit.RealWindowTests;

/// <summary>One run of phase 4's pass, shared by its tests: one folder of screenshots and one run.txt.</summary>
public sealed class Phase4Run : IDisposable
{
    public RealWindowRun Run { get; } = new(phase: Phase4Expectations.Phase);

    /// <summary>Whether the display has been noted in run.txt yet (once, with the first launch).</summary>
    internal bool DisplayNoted { get; set; }

    public void Dispose() => Run.Dispose();
}

/// <summary>
/// The real-window pass of phase 4: the steps of docs/test-reports/phase-4.md marked "RW", repeated
/// in the real app on the Windows desktop with the real mouse and keyboard. Screenshots go to
/// artifacts/visual/phase-4/real-window/ and are compared with the scripted walk-through's of the
/// same step (D33, D58), which must have run first (<c>dotnet test</c> at the repo root). The parts'
/// checks are in Phase4RealWindowPass.A.cs to .C2.cs.
/// </summary>
public partial class Phase4RealWindowPass(Phase4Run fixture) : IClassFixture<Phase4Run>
{
    /// <summary>How long the app may take to open a scenario: its history read and drawn, its status shown.</summary>
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long an operation may take to end (its log line): a rebase, Continue and the merge tool run chains of git calls (the report's OperationChainTimeout).</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The app's commits' date (D72): 2026-01-02 09:00 UTC, as the scripted run's AppCommitTime.</summary>
    private static readonly DateTimeOffset AppCommitTime = GitIsolation.AppCommitDate;

    private RealWindowRun Run => fixture.Run;

    /// <summary>The scenario's clock time of a commit made at 12:<paramref name="minute"/> on 2026-01-01 UTC.</summary>
    private static DateTimeOffset ScenarioTime(int minute) => new(2026, 1, 1, 12, minute, 0, TimeSpan.Zero);

    /// <summary>The working tree's folder name: the tab's name and the repository's in the log.</summary>
    private static string NameOf(TempRepo repo) => Path.GetFileName(repo.Path);

    /// <summary>
    /// Starts the real app with one tab on <paramref name="repo"/> in a 1100×700 client area (every
    /// RW row of the report's), dark (the default theme), and waits until the repository has opened:
    /// its history loaded and its status shown (the log lines the app writes for both). The first
    /// launch of the pass notes the display in run.txt.
    /// </summary>
    private RealApp Launch(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var data = Run.NewDataDirectory();
        var app = RealApp.Launch(data, new SessionState { Tabs = [new TabState(repo.Path)] });
        try
        {
            app.SetClientSize(Phase4Expectations.DefaultSize.Width, Phase4Expectations.DefaultSize.Height);
            if (!fixture.DisplayNoted)
            {
                Run.NoteDisplay(app);
                fixture.DisplayNoted = true;
            }

            var name = NameOf(repo);
            app.WaitForLogLine(AppLogFile.Containing($"] {name}: loaded "), LoadTimeout);
            app.WaitForLogLine(AppLogFile.WorkingTreeRead(name), LoadTimeout);
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Captures the client area, saves it as <c>&lt;name&gt;.png</c> and asserts that it matches the
    /// scripted picture of the same name within <see cref="RealWindowRun.MaxDifference"/> (D33,
    /// D58); run.txt gets a line with the difference and <c>&lt;name&gt;-difference.png</c> shows it.
    /// The pointer moves to the neutral point first, as both harnesses do before a capture that is
    /// not about a hover, unless <paramref name="keepPointer"/> is true: a capture with a menu open
    /// (the pointer stays where it opened the menu), in the middle of a drag, or of a tooltip.
    /// </summary>
    private void Compare(RealApp app, string name, bool keepPointer = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!keepPointer)
        {
            app.MoveToNeutral();
        }

        using var capture = app.CaptureClient();
        Run.Save(capture, name);
        Run.AssertMatchesScripted(capture, name, name);
    }

    /// <summary>
    /// Notes how many times the operation <paramref name="title"/> of <paramref name="repository"/>
    /// has ended so far, before the input that starts it; <see cref="PendingOperation.Wait"/> then
    /// waits for its next end line (<c>&lt;repo&gt;: &lt;Title&gt;: &lt;outcome&gt; after &lt;N&gt;
    /// ms: &lt;message&gt;</c>), which the queue writes after the operation's refresh: the real
    /// window's "wait until idle" (the report's conventions of C14 to C17).
    /// </summary>
    private static PendingOperation ExpectOperation(RealApp app, string repository, string title)
    {
        ArgumentNullException.ThrowIfNull(app);
        var pattern = AppLogFile.OperationEnded(repository, title);
        return new PendingOperation(app, pattern, app.CountLogLines(pattern));
    }

    /// <summary>The point of a graph row to click "in its Message column", from the row constants (D49), below the banner when one shows (D122).</summary>
    private static PointF RowPoint(RealApp app, int displayRow, int lanes) => app.GraphRowPoint(displayRow, lanes);

    // ---- Menus (phase 3's conventions, phase 4's "Menus") ----

    /// <summary>The labels of a menu as the report writes it: its items without the header, the separators and the marks after a label.</summary>
    private static List<string> ItemLabels(IEnumerable<string> lines) =>
        [.. lines.Skip(1).Where(line => line != "—").Select(line => MenuMark().Replace(line, string.Empty))];

    /// <summary>The disabled items of a menu as the report writes it, with their reasons (each item's tooltip).</summary>
    private static List<(string Label, string Reason)> DisabledItems(IEnumerable<string> lines) =>
        [.. lines.Skip(1)
            .Select(line => DisabledMark().Match(line))
            .Where(match => match.Success)
            .Select(match => (match.Groups["label"].Value, match.Groups["reason"].Value))];

    /// <summary>
    /// Asserts the open menu as the report writes it (<paramref name="lines"/>, header first): its
    /// items' labels in order, each disabled item with its reason as its help text (its tooltip),
    /// every other item enabled; the height the report gives (±2, 4a); and that the menu lies at
    /// least 4 px inside the window. Returns where the menu is.
    /// </summary>
    private static RectangleF AssertMenu(RealApp app, IReadOnlyList<string> lines, double height)
    {
        var menu = app.BoundsOf(app.WaitForMenu());
        Assert.Equal(ItemLabels(lines), app.MenuItemNames());
        var disabled = DisabledItems(lines);
        foreach (var item in app.FindAll("ContextMenuItem"))
        {
            var reason = disabled.Where(entry => entry.Label == item.Name).Select(entry => entry.Reason).FirstOrDefault();
            if (reason is null)
            {
                Assert.True(item.IsEnabled, $"The menu item \"{item.Name}\" is disabled.");
            }
            else
            {
                Assert.False(item.IsEnabled, $"The menu item \"{item.Name}\" is enabled; the report says it is disabled with \"{reason}\".");
                Assert.Equal(reason, item.HelpText);
            }
        }

        Assert.True(
            Math.Abs(menu.Height - height) <= WindowLayout.MenuHeightTolerance,
            string.Create(CultureInfo.InvariantCulture, $"The menu is {menu.Height} high; the report says {height} (±{WindowLayout.MenuHeightTolerance})."));
        var client = app.ClientSize;
        var inset = (float)WindowLayout.MenuEdgeInset - 0.5f;
        Assert.True(
            menu.Left >= inset && menu.Top >= inset && menu.Right <= client.Width - inset && menu.Bottom <= client.Height - inset,
            $"The menu at {menu} is not 4 px inside the {client.Width}x{client.Height} window.");
        return menu;
    }

    /// <summary>The open menu's item <paramref name="label"/> alone has the keyboard focus: its highlight (D105).</summary>
    private static void AssertFocusedItem(RealApp app, string label) =>
        app.WaitFor(
            () => app.FindAll("ContextMenuItem").Where(RealApp.HasKeyboardFocus).Select(item => item.Name).SequenceEqual([label]),
            $"\"{label}\" alone to have the keyboard focus",
            RealApp.DefaultWaitTimeout);

    /// <summary>Esc closes the open menu; waits until no menu is in the window.</summary>
    private static void CloseMenuWithEscape(RealApp app)
    {
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitFor(() => app.FindAll("ContextMenu").Count == 0, "the menu to close", RealApp.DefaultWaitTimeout);
    }

    /// <summary>A position within 2 logical pixels of the expected one (4a's ±2).</summary>
    private static void AssertNear(PointF expected, PointF actual, string what) =>
        Assert.True(
            Math.Abs(expected.X - actual.X) <= 2 && Math.Abs(expected.Y - actual.Y) <= 2,
            $"{what} is at {actual}, expected {expected} (±2).");

    // ---- Tooltips ----

    /// <summary>
    /// Rests the real pointer on <paramref name="element"/> until its tooltip
    /// <paramref name="text"/> shows, and asserts that the tooltip is drawn inside the window (D105)
    /// and, with <paramref name="below"/>, below the pointer (part C's "a tooltip opens below the
    /// pointer").
    /// </summary>
    private static void RestUntilToolTip(RealApp app, AutomationElement element, string text, bool below = false)
    {
        var bounds = app.BoundsOf(element);
        var pointer = new PointF(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
        var tip = app.RestPointerOn(element, text);
        AssertToolTipInside(app, tip, text);
        if (below)
        {
            var place = app.ToolTipPlace(tip).Bounds;
            Assert.True(place.Top >= pointer.Y - 0.5, $"The tooltip \"{text}\" at {place} is not below the pointer at {pointer}.");
        }
    }

    /// <summary>The tooltip <paramref name="tip"/> (its text <paramref name="text"/>) lies inside the client area (D105: tooltips are drawn on the window's overlay layer).</summary>
    private static void AssertToolTipInside(RealApp app, AutomationElement tip, string text)
    {
        var (bounds, inside) = app.ToolTipPlace(tip);
        var client = app.ClientSize;
        Assert.True(inside, $"The tooltip \"{text}\" at {bounds} is not inside the {client.Width}x{client.Height} client area.");
    }

    // ---- The details panel and the status bar ----

    /// <summary>Waits until the details panel shows the commit <paramref name="subject"/>.</summary>
    private static void WaitForDetails(RealApp app, string subject) =>
        app.WaitFor(() => app.FindAll("DetailsSubject").Any(element => element.Name == subject), $"the details of \"{subject}\"", RealApp.DefaultWaitTimeout);

    /// <summary>Waits until the details panel shows the commit <paramref name="subject"/> whose SHA is <paramref name="sha"/> (<c>DetailsSha</c>): for a subject two rows share.</summary>
    private static void WaitForDetails(RealApp app, string subject, string sha) =>
        app.WaitFor(
            () => app.FindAll("DetailsSubject").Any(element => element.Name == subject) && app.FindAll("DetailsSha").Any(element => element.Name == sha),
            $"the details of \"{subject}\" ({sha})",
            RealApp.DefaultWaitTimeout);

    /// <summary>Waits until the status bar's operation text is <paramref name="text"/> (it follows the queue's log line on the UI thread).</summary>
    private static void WaitForStatus(RealApp app, string text)
    {
        try
        {
            app.WaitFor(() => app.OperationStatusText() == text, $"the status bar to say \"{text}\"", RealApp.DefaultWaitTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} It says \"{app.OperationStatusText()}\".", ex);
        }
    }

    /// <summary>No dialog is open: no element of phase 3's or phase 4's dialogs is on screen (phase 3's "Absence").</summary>
    private static void AssertNoDialog(RealApp app)
    {
        foreach (var dialog in new[] { "ConfirmationDialog", "ErrorDialog", "MessageDialog", "NameDialog", "ResetDialog" })
        {
            Assert.True(app.ShownElements(dialog).Count == 0, $"A dialog ({dialog}) is open.");
        }
    }

    // ---- The banner (design 5.2) ----

    /// <summary>
    /// Waits for the in-progress banner with <paramref name="text"/> and asserts its buttons left to
    /// right as the report gives them: Continue (enabled, or disabled with
    /// <paramref name="continueReason"/> as its tooltip), Skip (shown or not), Abort with its label;
    /// and where they stand: on a row of their own under the text while the graph shows
    /// (<paramref name="besideText"/> false, the banner then more than one line), beside the text
    /// while the resolver is open (design 5.2, the report's part C conventions).
    /// </summary>
    private static void AssertBanner(RealApp app, string text, string? continueReason, bool skip, string abort, bool besideText)
    {
        app.WaitForText("BannerText", text, OperationTimeout);
        var banner = app.BannerBounds() ?? throw new InvalidOperationException("No banner is shown.");
        var resume = app.BannerContinueButton();
        Assert.Equal(Phase4Expectations.ContinueText, resume.Name);
        if (continueReason is null)
        {
            Assert.True(resume.IsEnabled, "The banner's Continue is disabled.");
        }
        else
        {
            Assert.False(resume.IsEnabled, "The banner's Continue is enabled.");
            Assert.Equal(continueReason, resume.HelpText);
        }

        var skips = app.ShownElements(RealApp.BannerSkipId);
        Assert.Equal(skip ? 1 : 0, skips.Count);
        if (skip)
        {
            Assert.Equal(Phase4Expectations.SkipText, skips[0].Name);
            Assert.True(skips[0].IsEnabled, "The banner's Skip is disabled.");
        }

        var abortButton = app.Find("BannerAbortButton");
        Assert.Equal(abort, abortButton.Name);
        Assert.True(abortButton.IsEnabled, $"The banner's {abort} is disabled.");

        // Left to right: Continue, Skip, Abort.
        var buttons = new[] { resume }.Concat(skips).Append(abortButton).Select(app.BoundsOf).ToList();
        for (var index = 1; index < buttons.Count; index++)
        {
            Assert.True(buttons[index].Left >= buttons[index - 1].Right - 0.5, $"The banner's buttons are not left to right: {string.Join(", ", buttons)}.");
        }

        var textBounds = app.BoundsOf(app.Find("BannerText"));
        if (besideText)
        {
            Assert.True(buttons[0].Left >= textBounds.Right - 0.5, $"The banner's buttons at {buttons[0]} are not beside its text at {textBounds}.");
        }
        else
        {
            Assert.True(buttons[0].Top >= textBounds.Bottom - 0.5, $"The banner's buttons at {buttons[0]} are not under its text at {textBounds}.");
            Assert.True(banner.Height > WindowLayout.BannerMinHeight + 0.5, $"The banner is {banner.Height} high: one line, with its buttons under the text.");
        }
    }

    // ---- Git, read back ----

    /// <summary>Plain git's output as lines, without empty ones and line-end carriage returns.</summary>
    private static async Task<List<string>> GitLinesAsync(TempRepo repo, params string[] arguments) =>
        [.. (await repo.GitAsync(arguments)).StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];

    /// <summary>A file of the working tree as git wrote it, line ends as line feeds.</summary>
    private static string WorkingFile(TempRepo repo, string path) =>
        File.ReadAllText(Path.Combine(repo.Path, path)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>Whether a file or folder is in the git folder.</summary>
    private static bool InGitDir(TempRepo repo, string name)
    {
        var path = Path.Combine(repo.Path, ".git", name);
        return File.Exists(path) || Directory.Exists(path);
    }

    /// <summary>No rebase is in progress: no <c>rebase-merge</c> or <c>rebase-apply</c> folder.</summary>
    private static void AssertNoRebaseFolder(TempRepo repo)
    {
        Assert.False(InGitDir(repo, "rebase-merge"), "The git folder holds rebase-merge.");
        Assert.False(InGitDir(repo, "rebase-apply"), "The git folder holds rebase-apply.");
    }

    /// <summary>The backup refs (<c>refs/visualcommit/backup/</c>), parsed (<see cref="Phase4Expectations.BackupRef"/>), with what each points at.</summary>
    private static async Task<List<(Phase4Expectations.BackupRef Ref, string Name, string Sha)>> BackupsAsync(TempRepo repo) =>
        [.. (await repo.BackupRefsAsync()).Select(entry => (Phase4Expectations.BackupRef.Parse(entry.Name), entry.Name, entry.Sha))];

    /// <summary>Every ref but the backups, with what it points at, HEAD's target, and the status: "git's state unchanged".</summary>
    private static async Task<string> StateAsync(TempRepo repo) =>
        string.Join(
            "\n---\n",
            string.Join('\n', (await GitLinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)")).Where(line => !line.StartsWith(Phase4Expectations.BackupRef.Prefix, StringComparison.Ordinal))),
            (await repo.GitAllowingFailureAsync("symbolic-ref", "HEAD")).StandardOutput.Trim(),
            await repo.HeadAsync(),
            (await repo.GitAsync("status", "--porcelain", "--untracked-files=all")).StandardOutput);

    /// <summary>
    /// Asserts a commit as git stores it: its parents (in order), author and committer (Test Author),
    /// author and commit dates as instants (git 2.36 writes UTC as +00:00, later gits as Z:
    /// <see cref="GitIsoDates"/>).
    /// </summary>
    private static async Task AssertCommitAsync(TempRepo repo, string sha, DateTimeOffset authorDate, DateTimeOffset commitDate, params string[] parents)
    {
        var fields = (await GitLinesAsync(repo, "log", "-1", "--format=%P%x1f%an <%ae>%x1f%cn <%ce>%x1f%aI%x1f%cI", sha)).Single().Split('\x1f');
        Assert.Equal(parents, fields[0].Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", fields[1]);
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", fields[2]);
        Assert.Equal(authorDate, GitIsoDates.Parse(fields[3]));
        Assert.Equal(commitDate, GitIsoDates.Parse(fields[4]));
    }

    /// <summary>A commit's message exactly as git keeps it: <c>git cat-file commit</c> after the header's first empty line.</summary>
    private static async Task<string> RawMessageAsync(TempRepo repo, string sha)
    {
        var raw = (await repo.GitAsync("cat-file", "commit", sha)).StandardOutput;
        var end = raw.IndexOf("\n\n", StringComparison.Ordinal);
        Assert.True(end >= 0, $"git cat-file commit {sha} has no message.");
        return raw[(end + 2)..];
    }

    [GeneratedRegex(@" \((danger|disabled: "".*""|disabled)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex MenuMark();

    [GeneratedRegex(@"^(?<label>.*) \(disabled: ""(?<reason>.*)""\)$", RegexOptions.CultureInvariant)]
    private static partial Regex DisabledMark();

    /// <summary>An operation a step is about to start, and how many times it had ended before (<see cref="ExpectOperation"/>).</summary>
    private sealed record PendingOperation(RealApp App, Regex Pattern, int EndedBefore)
    {
        /// <summary>
        /// Waits for the operation's next end line and asserts its outcome and, when given, its
        /// message (the status bar's text, design 1.7). Returns the line.
        /// </summary>
        public string Wait(OperationOutcome outcome, string? message = null, TimeSpan? timeout = null)
        {
            var line = App.WaitForLogLine(Pattern, timeout ?? OperationTimeout, EndedBefore + 1);
            Assert.Contains($": {outcome} after ", line, StringComparison.Ordinal);
            if (message is not null)
            {
                Assert.EndsWith(": " + message, line, StringComparison.Ordinal);
            }

            return line;
        }
    }
}
