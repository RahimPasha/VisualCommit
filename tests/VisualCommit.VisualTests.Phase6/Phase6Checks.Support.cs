// What every part of phase 6's walk-through shares (docs/phase-6-design.md 17.2, 17.3); the map of
// the project is in Phase6Checks.cs. Copied from phase 5's Phase5Checks.Support.cs, which stays as
// it is, with phase 6's part letters (w, k, s, g and l) in the picture names, OpenAsync's system
// theme, and Shot refusing a picture name it has already saved in the run (design 11.8). Then
// 17.2's helpers for the dashboard and the sweep, over the driver's ShellDriver.Phase6.*.cs:
// OpenWorkspaceAsync (part W's Defaults, with its read clock), WaitForAllRowsIdleAsync,
// AssertRow and AssertDashboardHeader (part W's "A row is written", with the expected rows and
// header, ExpectedCell, ExpectedRow, ExpectedDashboardHeader and ExpectedHeaderButton, at the end
// of this file), and ShotInBothThemesAsync (11.14, part L's "The pairs", LD1, LD19).
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public abstract partial class Phase6Checks
{
    /// <summary>The phase whose report these checks are and whose picture folder they fill.</summary>
    protected const int Phase = Phase6Expectations.Phase;

    /// <summary>
    /// How long a wait for a change made outside the app (through <see cref="TempRepo"/>, seen by
    /// the app's file watcher) may take: phase 3's, 30 seconds at first (a hot laptop has missed 5
    /// before), since D341 the driver's default, 60 s, as the default run's load needs (D237).
    /// The expected result is the state after the wait, not how long it took.
    /// </summary>
    protected static readonly TimeSpan OutsideChangeTimeout = ShellDriver.DefaultWaitTimeout;

    /// <summary>How long a network operation (a push or a pull through a file remote, the undo of a push) may take before a check gives up.</summary>
    protected static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How long an operation that runs a chain of git commands (a rebase, Continue to the next
    /// stop, an interactive rebase with its execs) may take: under a full default run such a
    /// chain needed 60 seconds where it takes 18 alone (phase 3's handoff, "Rules worth keeping").
    /// </summary>
    protected static readonly TimeSpan OperationChainTimeout = TimeSpan.FromSeconds(60);

    private static readonly Lock TimingGate = new();
    private static readonly HashSet<string> TimingsStarted = [];

    /// <summary>The picture names saved in this run (this test process), so that two steps saving one name fail instead of overwriting (design 11.8).</summary>
    private static readonly HashSet<string> SavedPictures = new(StringComparer.Ordinal);

    /// <summary>
    /// The checks' default start: writes a session with one tab on <paramref name="repo"/> (a fresh
    /// copy of the named scenario), starts the app at the given size in the given theme (Light and
    /// System written into <c>settings.json</c> first) with the system's theme dark unless
    /// <paramref name="systemDark"/> is false (<see cref="ShellDriver.SystemTheme"/>, design 17.2),
    /// waits until the graph has drawn its rows, the working tree's status is shown and the
    /// operation queue is idle (<see cref="ShellDriver.WaitForRepositoryAsync"/>), and rests the
    /// pointer at the neutral point. Dispose the driver at the end of the check
    /// (<c>using var app = …</c>).
    /// </summary>
    protected static async Task<ShellDriver> OpenAsync(
        TempDirectory data,
        TempRepo repo,
        int width = 1100,
        int height = 700,
        AppTheme theme = AppTheme.Dark,
        bool systemDark = true)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(repo);
        WriteTheme(data, theme);
        var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: width, height: height, systemDark: systemDark);
        try
        {
            await app.WaitForRepositoryAsync();
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The checks' start on the workspace "Web" (design 17.2; part W's Defaults): writes
    /// <c>workspaces.json</c> with <paramref name="copy"/>'s <see cref="TempWorkspace.ToWorkspace"/>
    /// ("Web", id <c>web</c>, its ten folders <c>copy.PathOf(name)</c> in <see cref="TempWorkspace.Names"/>'
    /// order) and <c>session.json</c> with one tab, that workspace's dashboard
    /// (<c>TabState(null, "web")</c>), active; writes the theme into <c>settings.json</c> for Light and
    /// System as <see cref="OpenAsync"/> does; starts the app at the given size with the system's
    /// theme dark, <paramref name="readClock"/> being the dashboard's read clock (W8's
    /// <see cref="ManualTimeProvider"/>; the session's own clock when null, part W's "Harness
    /// additions"); waits for the dashboard's first read pass
    /// (<see cref="ShellDriver.WaitForDashboardReadAsync"/>: its line and no row <c>Reading…</c>) and
    /// until every row is idle (<see cref="WaitForAllRowsIdleAsync"/>); and rests the pointer at the
    /// neutral point. Each wait may take <see cref="OperationChainTimeout"/>: the first pass alone is
    /// some 72 git processes, four at a time (design 17.6). <paramref name="tools"/> replaces the
    /// fixed tool catalogues (<see cref="ShellDriver.Start"/>'s, such as
    /// <see cref="HeldMergeTools.Catalogues"/>). Dispose the driver at the end of the check
    /// (<c>using var app = …</c>).
    /// </summary>
    protected static async Task<ShellDriver> OpenWorkspaceAsync(
        TempDirectory data,
        TempWorkspace copy,
        int width = 1100,
        int height = 700,
        AppTheme theme = AppTheme.Dark,
        TimeProvider? readClock = null,
        ToolCatalogues? tools = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(copy);
        WriteTheme(data, theme);
        var workspace = copy.ToWorkspace();
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList { Workspaces = [workspace] });
        var app = ShellDriver.StartWithTabs(data.Path, [new TabState(null, workspace.Id)], 0, width, height, readClock: readClock, tools: tools);
        try
        {
            await app.WaitUntilReadyAsync();
            await app.WaitForDashboardReadAsync(timeout: OperationChainTimeout);
            await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>Git's spelling of each folder <see cref="PrepareSameFolderAsync"/> prepared, by the copy's folder and the folder as given (normalised); null where git has none.</summary>
    private static readonly ConcurrentDictionary<(string Root, string Folder), string?> GitSpellings = new();

    /// <summary>
    /// Works out git's own spelling of folders of <paramref name="copy"/> for <see cref="SameFolder"/>
    /// (part W's "Harness additions"): <c>git rev-parse --show-toplevel</c> in each, run by the
    /// check's own <see cref="TempRepo"/> with <c>GIT_OPTIONAL_LOCKS=0</c> (so that it refreshes no
    /// index the app reads), normalised as <c>GitRepository</c> normalises it (the platform's
    /// separators, full, no trailing one); for a folder that does not exist (old-site), or where git
    /// finds no repository, none, so that only its text matches. With no <paramref name="folders"/>,
    /// the copy's ten folders (<see cref="TempWorkspace.Names"/>); otherwise those folders, such as
    /// one the check made inside the copy (W2's <c>archive/old-site</c>, once it is made). Await it
    /// once before the first <see cref="SameFolder"/> on those folders; a folder prepared again keeps
    /// its first spelling.
    /// </summary>
    protected static async Task PrepareSameFolderAsync(TempWorkspace copy, params string[] folders)
    {
        ArgumentNullException.ThrowIfNull(copy);
        ArgumentNullException.ThrowIfNull(folders);
        IEnumerable<string> targets = folders.Length > 0 ? folders : copy.Names.Select(copy.PathOf);
        var spelt = await Task.WhenAll(targets.Select(async folder => (Folder: folder, Git: await GitSpellingAsync(copy, folder))));
        foreach (var (folder, git) in spelt)
        {
            GitSpellings.TryAdd((copy.Root, WorkspacePaths.Normalise(folder)), git);
        }
    }

    /// <summary>
    /// Whether <paramref name="path"/> (a call's <c>WorkingDirectory</c>, a saved path, a row's
    /// folder) names the folder <paramref name="folder"/> of <paramref name="copy"/> (part W's
    /// <c>SameFolder</c>): <see cref="WorkspacePaths.Same"/> finds it equal to <paramref name="folder"/>
    /// as given or to git's own spelling of it, which <see cref="PrepareSameFolderAsync"/> worked out:
    /// on CI's macOS the copy's <c>/var/…</c> is git's <c>/private/var/…</c>, which no text compare of
    /// the two sees. For every folder and call-log match of part W.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="folder"/> was not prepared: await <see cref="PrepareSameFolderAsync"/> first.</exception>
    protected static bool SameFolder(TempWorkspace copy, string folder, string path)
    {
        ArgumentNullException.ThrowIfNull(copy);
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentNullException.ThrowIfNull(path);
        if (!GitSpellings.TryGetValue((copy.Root, WorkspacePaths.Normalise(folder)), out var git))
        {
            throw new InvalidOperationException($"Git's spelling of {folder} was not prepared: await PrepareSameFolderAsync(copy) (or with this folder) before the first SameFolder on it.");
        }

        return WorkspacePaths.Same(path, folder) || (git is not null && WorkspacePaths.Same(path, git));
    }

    /// <summary>Git's <c>--show-toplevel</c> of <paramref name="folder"/>, normalised; null without the folder or a repository there.</summary>
    private static async Task<string?> GitSpellingAsync(TempWorkspace copy, string folder)
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        using var repo = copy.RepoAt(folder);
        var result = await repo.GitWithEnvironmentAllowingFailureAsync(new Dictionary<string, string?> { ["GIT_OPTIONAL_LOCKS"] = "0" }, "rev-parse", "--show-toplevel");
        return result.ExitCode == 0
            ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(result.StandardOutput.TrimEnd('\r', '\n')))
            : null;
    }

    /// <summary>Writes <paramref name="theme"/> into the data folder's <c>settings.json</c> before the start, unless it is Dark, the default (D194): no file then.</summary>
    private static void WriteTheme(TempDirectory data, AppTheme theme)
    {
        if (theme != AppTheme.Dark)
        {
            new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = theme });
        }
    }

    /// <summary>
    /// Waits until the active dashboard is idle (design 17.2; part W's WD37, "until no row of the
    /// dashboard reads"): no read pass under way (<c>IsPassRunning</c>), no row with a read running or
    /// waiting for a slot (<c>IsReading</c>), and the queue of every row's repository, a summary-mode
    /// view model or the view model of a tab the row follows, with nothing running or waiting, its
    /// refresh included, as <see cref="ShellDriver.WaitForIdleAsync"/> means it. It holds at once when
    /// nothing reads or runs. At most <paramref name="timeout"/> (<see cref="ShellDriver.DefaultWaitTimeout"/> when null).
    /// </summary>
    /// <exception cref="InvalidOperationException">The active tab is no dashboard.</exception>
    protected static async Task WaitForAllRowsIdleAsync(ShellDriver app, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        var dashboard = app.ActiveDashboard ?? throw new InvalidOperationException("The active tab is no dashboard.");
        List<RepositoryViewModel> Repositories() => [.. dashboard.Rows.Select(row => row.Repository).OfType<RepositoryViewModel>().Distinct()];
        await app.WaitForAsync(
            () => !dashboard.IsPassRunning
                && dashboard.Rows.All(row => !row.IsReading)
                && Repositories().All(repository => repository.Queue.Current is null && repository.Queue.Waiting == 0 && repository.Queue.WhenIdleAsync().IsCompleted),
            $"no row of {dashboard.Name} reading, no read pass under way and every row's queue idle",
            timeout);
        foreach (var repository in Repositories())
        {
            await app.WaitForIdleAsync(repository, timeout);
        }
    }

    /// <summary>
    /// Asserts the dashboard's row <paramref name="name"/> as the report writes it (part W's "A row is
    /// written"; <see cref="ExpectedRow"/>): each cell's text, tone, tooltip and automation name (the
    /// Repository cell's its name, and its tooltip the folder when <see cref="ExpectedRow.Folder"/> is
    /// given; the Branch cell's text, primary, WD34; the Ahead / behind cell's arrows as <c>↑ a ↓ b</c>);
    /// whether <c>Cancel</c> or <c>Details…</c> shows; whether the row is selected and has a read
    /// outstanding (<c>IsReading</c>, WD37); and, for a row drawn in view, every non-empty cell drawn
    /// in its tone's colour of the theme shown and whole, not cut with "…" (part W's "Every text a row
    /// of part W shows is drawn whole"), but the Folder cell (WD29). The row comes from
    /// <paramref name="snapshot"/> when given (one read right after an input, or
    /// <see cref="ShellDriver.DashboardSnapshot(string)"/> of a dashboard in the background, whose
    /// rows are read from their view models: no colours), else from the active dashboard's
    /// <see cref="ShellDriver.DashboardSnapshot()"/>. A failure names every cell that differs, with
    /// the expected and the actual row in the report's form.
    /// </summary>
    protected static void AssertRow(ShellDriver app, string name, ExpectedRow expected, DashboardSnapshot? snapshot = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(expected);
        var dashboard = snapshot ?? app.DashboardSnapshot();
        var row = dashboard.Rows.FirstOrDefault(candidate => candidate.Name == name)
            ?? throw new Xunit.Sdk.XunitException($"The dashboard {dashboard.Title} has no row {name}. Its rows:\n  " + string.Join("\n  ", dashboard.Rows.Select(Described)));
        var problems = RowProblems(row, expected, PaletteOf(app));
        Assert.True(
            problems.Count == 0,
            $"The row {name} is not the expected one.\nExpected: {name} · {expected}\nActual:   {Described(row)}\n" + string.Join('\n', problems.Select(problem => "- " + problem)));
    }

    /// <summary>
    /// Asserts the active dashboard's header as drawn (part W's conventions, W1 step 3, TD and TR;
    /// <see cref="ExpectedDashboardHeader"/>): <c>DashboardTitle</c> (primary, whole) and
    /// <c>DashboardCount</c> (secondary); the run's text in its tone's colour and whole, or none;
    /// the bar at its value, or none; <c>Cancel all</c> with its tooltip, or none; <c>Add
    /// repository…</c>, <c>Fetch all</c>, <c>Pull all</c> and <c>Refresh</c> shown, enabled or not,
    /// with their tooltips (a disabled one's reason), Fetch all's, Pull all's and Refresh's help text
    /// the same; <c>DashboardMore</c> enabled, named and tooltipped <c>More</c>; and
    /// <c>DashboardNotice</c> (warning) or none. From <paramref name="snapshot"/> when given, else
    /// the active dashboard's <see cref="ShellDriver.DashboardSnapshot()"/>; the dashboard must be drawn.
    /// </summary>
    protected static void AssertDashboardHeader(ShellDriver app, ExpectedDashboardHeader expected, DashboardSnapshot? snapshot = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(expected);
        var dashboard = snapshot ?? app.DashboardSnapshot();
        var header = dashboard.Header
            ?? throw new Xunit.Sdk.XunitException($"The dashboard {dashboard.Title} is not drawn: its header cannot be read.");
        var problems = HeaderProblems(dashboard, header, expected, PaletteOf(app));
        Assert.True(
            problems.Count == 0,
            $"The header of {dashboard.Title} is not the expected one.\nExpected: {expected}\nActual:   {Described(dashboard, header)}\n" + string.Join('\n', problems.Select(problem => "- " + problem)));
    }

    /// <summary>What differs between a row as drawn and as expected (<see cref="AssertRow"/>), each in words; empty when nothing does.</summary>
    internal static List<string> RowProblems(DashboardRowSnapshot row, ExpectedRow expected, Palette palette)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(palette);
        var problems = new List<string>();
        void Drawn(string what, DashboardCellSnapshot actual, DashboardTone tone, bool whole)
        {
            if (!actual.IsShown || actual.Text.Length == 0)
            {
                return;
            }

            if (actual.Color is { } colour && colour != Hex(ToneColour(palette, tone)))
            {
                problems.Add($"{what} is drawn in {colour}, not in its {Words(tone)} colour {Hex(ToneColour(palette, tone))}.");
            }

            if (whole && actual.IsTrimmed)
            {
                problems.Add($"{what} is cut with \"…\": \"{actual.Text}\" is not drawn whole.");
            }
        }

        void Cell(string what, DashboardCellSnapshot actual, ExpectedCell want, bool named = true)
        {
            if (actual.Text != want.Text)
            {
                problems.Add($"{what}: the text is {Quoted(actual.Text)}, expected {Quoted(want.Text)}.");
            }

            if (!want.IsEmpty && actual.Tone != want.Tone)
            {
                problems.Add($"{what}: the tone is {Words(actual.Tone)}, expected {Words(want.Tone)}.");
            }

            if (actual.ToolTip != want.ToolTip)
            {
                problems.Add($"{what}: the tooltip is {Quoted(actual.ToolTip)}, expected {Quoted(want.ToolTip)}.");
            }

            if (named && !want.IsEmpty && actual.AutomationName != want.Name)
            {
                problems.Add($"{what}: the automation name is {Quoted(actual.AutomationName)}, expected {Quoted(want.Name)}.");
            }

            Drawn(what, actual, want.IsEmpty ? actual.Tone : want.Tone, whole: true);
        }

        Cell("Repository", row.Repository, ExpectedCell.Primary(row.Name, expected.Folder ?? row.Repository.ToolTip));
        if (expected.Folder is { } folder && row.Folder.IsShown)
        {
            if (row.Folder.Text != folder)
            {
                problems.Add($"Folder: the text is {Quoted(row.Folder.Text)}, expected {Quoted(folder)}.");
            }

            // Cut with "…" off the development machine's short temporary folder (WD29): its colour only.
            Drawn("Folder", row.Folder, DashboardTone.Secondary, whole: false);
        }

        if (row.Branch.Text != expected.Branch)
        {
            problems.Add($"Branch: the text is {Quoted(row.Branch.Text)}, expected {Quoted(expected.Branch)}.");
        }

        Drawn("Branch", row.Branch, DashboardTone.Primary, whole: true);
        Cell("Ahead / behind", row.AheadBehind, expected.AheadBehind);
        Cell("Changes", row.Changes, expected.Changes);
        Cell("State", row.State, expected.State);
        Cell("Operation", row.Operation, expected.Operation);
        void Flag(string what, bool actual, bool want)
        {
            if (actual != want)
            {
                problems.Add($"{what}: {(actual ? "yes" : "no")}, expected {(want ? "yes" : "no")}.");
            }
        }

        Flag("Cancel shows", row.ShowsCancel, expected.ShowsCancel);
        Flag("Details… shows", row.ShowsDetails, expected.ShowsDetails);
        Flag("Selected", row.IsSelected, expected.Selected);
        Flag("A read outstanding (IsReading)", row.IsReading, expected.IsReading);
        return problems;
    }

    /// <summary>What differs between a header as drawn and as expected (<see cref="AssertDashboardHeader"/>), each in words; empty when nothing does.</summary>
    internal static List<string> HeaderProblems(DashboardSnapshot dashboard, DashboardHeaderSnapshot header, ExpectedDashboardHeader expected, Palette palette)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(palette);
        var problems = new List<string>();
        void Text(string what, DashboardTextSnapshot? actual, string? want, Color colour, bool whole = true)
        {
            if (actual?.Text != want)
            {
                problems.Add(want is null ? $"{what} shows {Quoted(actual?.Text)}; expected none." : $"{what} is {Quoted(actual?.Text)}, expected {Quoted(want)}.");
                return;
            }

            if (actual is null)
            {
                return;
            }

            if (actual.Color is { } drawn && drawn != Hex(colour))
            {
                problems.Add($"{what} is drawn in {drawn}, not {Hex(colour)}.");
            }

            if (whole && actual.IsTrimmed)
            {
                problems.Add($"{what} is cut with \"…\".");
            }
        }

        void Button(string what, DashboardButtonSnapshot actual, ExpectedHeaderButton want, bool helpText)
        {
            if (!actual.IsShown)
            {
                problems.Add($"{what} does not show.");
                return;
            }

            if (actual.IsEnabled != want.IsEnabled)
            {
                problems.Add($"{what} is {(actual.IsEnabled ? "enabled" : "disabled")}, expected {(want.IsEnabled ? "enabled" : "disabled")}.");
            }

            if (actual.ToolTip != want.ToolTip)
            {
                problems.Add($"{what}'s tooltip is {Quoted(actual.ToolTip)}, expected {Quoted(want.ToolTip)}.");
            }

            if (helpText && actual.HelpText != want.ToolTip)
            {
                problems.Add($"{what}'s help text is {Quoted(actual.HelpText)}, expected its tooltip {Quoted(want.ToolTip)}.");
            }
        }

        if (dashboard.Title != expected.Title)
        {
            problems.Add($"The workspace is {Quoted(dashboard.Title)}, expected {Quoted(expected.Title)}.");
        }

        Text("DashboardTitle", header.Title, expected.Title, palette.TextPrimary);
        Text("DashboardCount", header.Count, expected.Count, palette.TextSecondary);
        Text("The run's text", header.Run, expected.RunText, StatusColour(palette, expected.RunTone));
        if (expected.RunText is not null && header.RunTone != expected.RunTone)
        {
            problems.Add($"The run's tone is {header.RunTone}, expected {expected.RunTone}.");
        }

        if (header.Progress != expected.Progress)
        {
            problems.Add($"The bar is {Shown(header.Progress)}, expected {Shown(expected.Progress)}.");
        }

        if (expected.CancelAll is null)
        {
            if (header.CancelAll.IsShown)
            {
                problems.Add("Cancel all shows; expected none.");
            }
        }
        else
        {
            Button("Cancel all", header.CancelAll, ExpectedHeaderButton.Enabled(expected.CancelAll), helpText: false);
        }

        Button("Add repository…", header.AddRepository, expected.AddRepository, helpText: false);
        Button("Fetch all", header.FetchAll, expected.FetchAll, helpText: true);
        Button("Pull all", header.PullAll, expected.PullAll, helpText: true);
        Button("Refresh", header.Refresh, expected.Refresh, helpText: true);
        Button("DashboardMore", header.More, ExpectedHeaderButton.Enabled(MoreText), helpText: false);
        if (header.More.AutomationName != MoreText)
        {
            problems.Add($"DashboardMore's automation name is {Quoted(header.More.AutomationName)}, expected {Quoted(MoreText)}.");
        }

        Text("DashboardNotice", header.Notice, expected.Notice, palette.Warning, whole: false);
        return problems;
    }

    /// <summary>The name and tooltip of the header's <c>DashboardMore</c> (2.5).</summary>
    private const string MoreText = "More";

    /// <summary>A row as the report writes it: <c>name · branch · ahead / behind · changes · state · operation</c>, each cell with its tone and tooltip, then what the row shows besides.</summary>
    internal static string Described(DashboardRowSnapshot row)
    {
        ArgumentNullException.ThrowIfNull(row);
        string Cell(DashboardCellSnapshot cell) =>
            new ExpectedCell(cell.Text, cell.Tone, cell.ToolTip, cell.AutomationName == cell.Text ? null : cell.AutomationName).ToString();
        var flags = RowFlags(row.IsSelected, row.ShowsCancel, row.ShowsDetails, row.IsReading);
        return $"{row.Name} · {(row.Branch.Text.Length == 0 ? "empty" : "`" + row.Branch.Text + "`")} · {Cell(row.AheadBehind)} · {Cell(row.Changes)} · {Cell(row.State)} · {Cell(row.Operation)}{flags}";
    }

    /// <summary>A header as the report writes it, drawn.</summary>
    private static string Described(DashboardSnapshot dashboard, DashboardHeaderSnapshot header)
    {
        static string Button(DashboardButtonSnapshot button) => !button.IsShown
            ? "hidden"
            : $"{(button.IsEnabled ? "enabled" : "disabled")}, {Quoted(button.ToolTip)}";
        return $"{dashboard.Title}, {Quoted(header.Count?.Text)}; run {Quoted(header.Run?.Text)} ({header.RunTone}), bar {Shown(header.Progress)}, Cancel all {Button(header.CancelAll)}; "
            + $"Add repository… {Button(header.AddRepository)}; Fetch all {Button(header.FetchAll)}; Pull all {Button(header.PullAll)}; Refresh {Button(header.Refresh)}; notice {Quoted(header.Notice?.Text)}";
    }

    /// <summary>What a row shows besides its cells, as <see cref="ExpectedRow"/>'s and <see cref="Described(DashboardRowSnapshot)"/>'s texts end.</summary>
    internal static string RowFlags(bool selected, bool cancel, bool details, bool reading) =>
        string.Concat(
            selected ? " (selected)" : string.Empty,
            cancel ? " [Cancel]" : string.Empty,
            details ? " [Details…]" : string.Empty,
            reading ? " (reading)" : string.Empty);

    private static Color ToneColour(Palette palette, DashboardTone tone) => tone switch
    {
        DashboardTone.Primary => palette.TextPrimary,
        DashboardTone.Warning => palette.Warning,
        DashboardTone.Danger => palette.Danger,
        _ => palette.TextSecondary,
    };

    private static Color StatusColour(Palette palette, OperationStatusTone tone) => tone switch
    {
        OperationStatusTone.Warning => palette.Warning,
        OperationStatusTone.Danger => palette.Danger,
        _ => palette.TextSecondary,
    };

    /// <summary>A tone as the report writes it: primary, secondary, warning, danger.</summary>
    internal static string Words(DashboardTone tone) => tone.ToString().ToLowerInvariant();

    private static string Hex(Color colour) => string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    private static string Quoted(string? text) => text is null ? "none" : "\"" + text + "\"";

    private static string Shown(int? value) => value is { } percent ? string.Create(CultureInfo.InvariantCulture, $"at {percent}") : "hidden";

    /// <summary>
    /// One pair of the sweep (design 11.14, 17.2; part L's "The pairs", LD1, LD19), with
    /// <c>"theme": "System"</c> chosen and the fake system dark: (1) the window shows dark and LD1's
    /// token pixel has its dark value; <c>Shot(name + "-dark")</c>. (2)
    /// <see cref="ShellDriver.SetSystemThemeAsync"/>(false): the window shows light, the log has one
    /// more line <c>The system's theme changed to light; the app follows it.</c>,
    /// <c>settings.json</c> still says <c>"System"</c>, <c>ThemeSwitch</c> shows <c>IconMoon</c> with
    /// the tooltip and help text <c>Switch to the dark theme (stops following the system)</c>, and the
    /// screen is still up; <c>Shot(name + "-light")</c>, the token pixel light. (3)
    /// <c>SetSystemThemeAsync(true)</c>: dark again, the token pixel dark, the line <c>The system's
    /// theme changed to dark; the app follows it.</c>, <c>ThemeSwitch</c> with <c>IconSun</c> and
    /// <c>Switch to the light theme (stops following the system)</c>, and the screen still up: the
    /// same tab active, the same dialog on top (<c>DialogHostViewModel.Current</c>), the same menu
    /// open (<c>Menu.IsOpen</c>, its header unchanged), the same tooltips open
    /// (<c>ToolTip.GetIsOpen</c>), the same page shown, the drag still running with its pill, as the
    /// dark picture had them. LD1's token pixel is the status bar's background at the neutral point,
    /// <c>VcChromeBackgroundBrush</c>, or, while the dialog on top has a backdrop, its card's
    /// background 10 px inside its top-left corner, <c>VcPanelBackgroundBrush</c>. While a tooltip is
    /// open (LD19) each picture also has it in its variant's colours: its background and border the
    /// tokens', its text's ink the primary text's (<see cref="TextInk"/>), none of Fluent's old
    /// colours inside it, and its bounds the same (±0.5) in both. <paramref name="keepPointer"/>
    /// leaves the pointer where the step put it (a tooltip's control, a menu item; a drag or an open
    /// menu keeps it anyway, <see cref="Shot"/>); otherwise it moves to the neutral point first.
    /// <paramref name="assertEach"/> runs after each of the two pictures, while the window shows its
    /// variant, with the picture and that variant's colours: the step's own expectations, asserted
    /// once per variant. A name already saved in the run is refused before anything flips
    /// (<see cref="ClaimPictureName"/>). Returns the two pictures.
    /// </summary>
    protected static Task<(Screenshot Dark, Screenshot Light)> ShotInBothThemesAsync(
        ShellDriver app,
        string name,
        bool keepPointer = false,
        Action<Screenshot, Palette>? assertEach = null) =>
        ShotInBothThemesAsync(app, name, keepPointer, assertEach, pictures: null);

    /// <summary>
    /// <see cref="ShotInBothThemesAsync(ShellDriver, string, bool, Action{Screenshot, Palette}?)"/>, its
    /// two pictures saved as <c>&lt;name&gt;-dark.png</c> and <c>&lt;name&gt;-light.png</c> in
    /// <paramref name="pictures"/> (with no note of the steps' times) when it is given, else under
    /// artifacts/visual/phase-6/scripted/ as <see cref="Shot"/> saves them; the names are claimed in
    /// the run either way. For the support's own tests, whose pictures the gate must not list.
    /// </summary>
    internal static async Task<(Screenshot Dark, Screenshot Light)> ShotInBothThemesAsync(
        ShellDriver app,
        string name,
        bool keepPointer,
        Action<Screenshot, Palette>? assertEach,
        string? pictures)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrEmpty(name);
        Screenshot Take(string picture)
        {
            if (pictures is null)
            {
                return TakePicture(app, picture, keepPointer);
            }

            var shot = CaptureForPicture(app, keepPointer);
            shot.Save(Path.Combine(pictures, picture + ".png"));
            return shot;
        }

        var setting = app.Session.Themes.Setting;
        Assert.True(setting == AppTheme.System, $"A pair needs the theme System chosen (11.14); it is {setting}.");
        Assert.True(app.SystemTheme.IsDark, "A pair starts with the fake system dark (11.14).");
        ClaimPictureName(name + "-dark");
        ClaimPictureName(name + "-light");

        // (1) Dark.
        Assert.True(app.ShowsDarkTheme, $"The window shows {app.ShownThemeVariant} at the start of the pair {name}, not Dark.");
        var dark = Take(name + "-dark");
        var screen = ScreenState.Of(app);
        AssertTokenPixel(app, dark, Dark);
        var tips = AssertOpenToolTips(app, dark, Dark);
        assertEach?.Invoke(dark, Dark);

        // (2) Light, following the system without a restart; the setting stays System.
        await FlipAsync(app, dark: false, screen);
        Assert.Equal("System", SavedTheme(app));
        var light = Take(name + "-light");
        AssertTokenPixel(app, light, Light);
        var lightTips = AssertOpenToolTips(app, light, Light);
        Assert.True(
            tips.Count == lightTips.Count && tips.Zip(lightTips).All(pair => SameBounds(pair.First, pair.Second)),
            $"The tooltips' bounds changed with the theme (LD19: only colours change): dark {string.Join(", ", tips)}, light {string.Join(", ", lightTips)}.");
        assertEach?.Invoke(light, Light);

        // (3) Dark again, the screen still up.
        await FlipAsync(app, dark: true, screen);
        AssertTokenPixel(app, app.Capture(), Dark);
        return (dark, light);
    }

    /// <summary>
    /// Turns the fake system dark or light under System (<see cref="ShellDriver.SetSystemThemeAsync"/>,
    /// which waits until the window shows it), then asserts the follow's log line (exactly one since
    /// the flip), the variant, <c>ThemeSwitch</c> and the screen still up (<see cref="ScreenState"/>).
    /// </summary>
    private static async Task FlipAsync(ShellDriver app, bool dark, ScreenState screen)
    {
        var mark = app.LogMark();
        await app.SetSystemThemeAsync(dark);
        var line = AppLogFile.Containing($"The system's theme changed to {(dark ? "dark" : "light")}; the app follows it.");
        await app.WaitForLogLineAsync(line, mark: mark);
        Assert.True(app.CountLogLines(line, mark) == 1, "The theme's follow was logged more than once:\n" + string.Join('\n', app.LogLinesSince(mark)));
        Assert.True(app.ShowsDarkTheme == dark, $"The window shows {app.ShownThemeVariant} after the system turned {(dark ? "dark" : "light")}.");
        AssertThemeSwitch(app, dark);
        await screen.AssertStillUpAsync(app);
    }

    /// <summary>
    /// <c>ThemeSwitch</c> with System chosen (11.6): in the dark theme <c>IconSun</c> and <c>Switch to
    /// the light theme (stops following the system)</c>, in the light one <c>IconMoon</c> and <c>Switch
    /// to the dark theme (stops following the system)</c>, as tooltip and help text.
    /// </summary>
    private static void AssertThemeSwitch(ShellDriver app, bool dark)
    {
        var button = app.Find<ToolbarButton>("ThemeSwitch");
        var icon = dark ? "IconSun" : "IconMoon";
        var text = dark ? "Switch to the light theme (stops following the system)" : "Switch to the dark theme (stops following the system)";
        Assert.True(ReferenceEquals(Application.Current!.FindResource(icon), button.IconData), $"ThemeSwitch does not show {icon} in the {(dark ? "dark" : "light")} theme.");
        Assert.Equal(text, ToolTip.GetTip(button) as string);
        Assert.Equal(text, AutomationProperties.GetHelpText(button));
    }

    /// <summary>LD1's token pixel in <paramref name="picture"/>, against <paramref name="palette"/>'s value.</summary>
    private static void AssertTokenPixel(ShellDriver app, Screenshot picture, Palette palette)
    {
        if (app.TopDialogView is { } view
            && view.GetVisualDescendants().OfType<Panel>().Any(panel => panel.Classes.Contains("dialogBackdrop") && panel.IsEffectivelyVisible))
        {
            var card = view.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Classes.Contains("dialogCard") && border.IsEffectivelyVisible)
                ?? throw new Xunit.Sdk.XunitException($"The dialog on top ({view.GetType().Name}) has a backdrop but no card (Border.dialogCard): LD1 names no token pixel for it.");
            AssertColour(palette.Panel, picture, app.BoundsOf(card).TopLeft + new Point(10, 10), "top dialog card's background (VcPanelBackgroundBrush, LD1)");
            return;
        }

        AssertColour(palette.Chrome, picture, ShellDriver.NeutralPoint, "status bar's background at the neutral point (VcChromeBackgroundBrush, LD1)");
    }

    /// <summary>
    /// Every open tooltip in <paramref name="picture"/> in <paramref name="palette"/>'s colours (11.12,
    /// LD19): its brushes the tokens' (<c>VcPanelBackgroundBrush</c>, <c>VcTextPrimaryBrush</c>,
    /// <c>VcBorderBrush</c>), its background 3 inside its left edge, its border on that edge, its text's
    /// ink the primary text's and not Fluent's (<see cref="TextInk.Problems"/>), and none of Fluent's
    /// old background and text colours inside it (dark <c>#2B2B2B</c>, <c>#FFFFFF</c>; light
    /// <c>#F2F2F2</c>, <c>#000000</c>). Returns their bounds, top to bottom.
    /// </summary>
    private static List<Rect> AssertOpenToolTips(ShellDriver app, Screenshot picture, Palette palette)
    {
        var dark = ReferenceEquals(palette, Dark);
        var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible).ToList();
        var bounds = new List<Rect>();
        foreach (var tip in tips)
        {
            Assert.Equal(palette.Panel, (tip.Background as ISolidColorBrush)?.Color);
            Assert.Equal(palette.TextPrimary, (tip.Foreground as ISolidColorBrush)?.Color);
            Assert.Equal(palette.Border, (tip.BorderBrush as ISolidColorBrush)?.Color);
            var rect = app.BoundsOf(tip);
            var left = (int)Math.Ceiling(rect.X);
            var top = (int)Math.Ceiling(rect.Y);
            var right = (int)Math.Floor(rect.Right) - 1;
            var bottom = (int)Math.Floor(rect.Bottom) - 1;
            var middle = (int)rect.Center.Y;
            AssertColour(palette.Panel, picture, new Point(left + 3, middle), "tooltip's background (VcPanelBackgroundBrush, 11.12)");
            Assert.True(
                picture.PixelAt(left, middle) == palette.Border || picture.PixelAt(left + 1, middle) == palette.Border,
                $"The tooltip at {rect} has no border of {palette.Border} on its left edge: {picture.PixelAt(left, middle)}, {picture.PixelAt(left + 1, middle)}.");
            var area = new Rect(left, top, right - left + 1, bottom - top + 1);
            var fluentText = dark ? Colors.White : Colors.Black;
            var problems = TextInk.Problems(picture, area, palette.TextPrimary, palette.Panel, fluentText, "the tooltip's text");
            Assert.True(problems.Count == 0, string.Join('\n', problems));
            var fluent = dark ? new[] { Color.Parse("#2B2B2B"), Colors.White } : new[] { Color.Parse("#F2F2F2"), Colors.Black };
            for (var y = top; y <= bottom; y++)
            {
                for (var x = left; x <= right; x++)
                {
                    var pixel = picture.PixelAt(x, y);
                    Assert.False(fluent.Contains(pixel), $"The tooltip at {rect} has Fluent's old colour {pixel} at ({x}, {y}) (LD19).");
                }
            }

            bounds.Add(rect);
        }

        return [.. bounds.OrderBy(rect => rect.Y).ThenBy(rect => rect.X)];
    }

    private static bool SameBounds(Rect a, Rect b) =>
        Math.Abs(a.X - b.X) <= 0.5 && Math.Abs(a.Y - b.Y) <= 0.5 && Math.Abs(a.Width - b.Width) <= 0.5 && Math.Abs(a.Height - b.Height) <= 0.5;

    /// <summary>The theme <c>settings.json</c> holds (<c>"theme"</c>), read without the store, which would set a file it cannot read aside; null without the file or the key.</summary>
    private static string? SavedTheme(ShellDriver app)
    {
        var file = app.Session.Paths.SettingsFile;
        if (!File.Exists(file))
        {
            return null;
        }

        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.TryGetProperty("theme", out var theme) ? theme.GetString() : null;
    }

    /// <summary>
    /// What a pair keeps on the screen while the theme flips (part L's "the screen still up"): the
    /// active tab, the dialog on top, the open menu and its header, the controls whose tooltips are
    /// open, the page shown, and whether a branch drag runs with its pill.
    /// </summary>
    private sealed record ScreenState(
        RepoTabViewModel Tab,
        DialogViewModel? Dialog,
        bool MenuOpen,
        string? MenuHeader,
        IReadOnlyList<Control> ToolTips,
        object? Page,
        bool Dragging,
        bool HasPill)
    {
        public static ScreenState Of(ShellDriver app)
        {
            app.Settle();
            var shell = app.Session.Shell;
            return new ScreenState(
                shell.ActiveTab,
                app.Session.Services.DialogHost.Current,
                shell.Menu.IsOpen,
                shell.Menu.Current?.HeaderText,
                [.. app.Window.GetVisualDescendants().OfType<Control>().Where(ToolTip.GetIsOpen)],
                shell.ActiveTab.Repository?.Page,
                IsDragging(app),
                PillOf(app) is not null);
        }

        /// <summary>Asserts that the screen is as it was (each difference named), then waits for each tooltip to have faded in again.</summary>
        public async Task AssertStillUpAsync(ShellDriver app)
        {
            var now = Of(app);
            var problems = new List<string>();
            if (!ReferenceEquals(now.Tab, Tab))
            {
                problems.Add($"another tab is active: {now.Tab.Title}, not {Tab.Title}");
            }

            if (!ReferenceEquals(now.Dialog, Dialog))
            {
                problems.Add($"the dialog on top is {Describe(now.Dialog)}, not {Describe(Dialog)}");
            }

            if (now.MenuOpen != MenuOpen || now.MenuHeader != MenuHeader)
            {
                problems.Add($"the menu is {(now.MenuOpen ? "open: " + now.MenuHeader : "closed")}, not {(MenuOpen ? "open: " + MenuHeader : "closed")}");
            }

            var closed = ToolTips.Where(owner => !now.ToolTips.Contains(owner)).ToList();
            var opened = now.ToolTips.Where(owner => !ToolTips.Contains(owner)).ToList();
            if (closed.Count > 0 || opened.Count > 0)
            {
                problems.Add($"the tooltips changed: closed {string.Join(", ", closed.Select(Describe))}; opened {string.Join(", ", opened.Select(Describe))}");
            }

            if (!ReferenceEquals(now.Page, Page))
            {
                problems.Add($"the page shown is {now.Page?.GetType().Name ?? "none"}, not {Page?.GetType().Name ?? "none"}");
            }

            if (now.Dragging != Dragging || now.HasPill != HasPill)
            {
                problems.Add($"the drag {(now.Dragging ? "runs" : "does not run")} {(now.HasPill ? "with" : "without")} its pill, not {(Dragging ? "running" : "stopped")} {(HasPill ? "with" : "without")} it");
            }

            Assert.True(problems.Count == 0, "The screen is not still up after the theme changed (part L's \"The pairs\"): " + string.Join("; ", problems) + ".");
            foreach (var owner in ToolTips)
            {
                await app.WaitForToolTipAsync(owner);
            }
        }

        private static string Describe(DialogViewModel? dialog) => dialog is null ? "none" : $"{dialog.GetType().Name} \"{dialog.Title}\"";

        private static string Describe(Control owner) => $"{owner.GetType().Name} \"{owner.Name ?? AutomationProperties.GetAutomationId(owner)}\"";
    }

    /// <summary>The drag layer's pill while a drag draws one, else null (no drag layer: none).</summary>
    private static Rect? PillOf(ShellDriver app)
    {
        try
        {
            return app.Find<DragLayer>("DragLayer").PillBounds;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves a picture of the window as artifacts/visual/phase-6/scripted/<paramref name="name"/>.png
    /// (the first picture of a run empties the folder, <see cref="VisualRun.Save"/>) and returns it
    /// for pixel assertions. The name is the report's, "&lt;check&gt;&lt;step&gt;-&lt;what&gt;", and is
    /// refused when a step of this run saved it already (<see cref="ClaimPictureName"/>, design 11.8).
    /// Before the capture the pointer moves to the neutral point, as both harnesses do before a
    /// capture that is not about a hover, unless <paramref name="keepPointer"/> is true (a hover,
    /// a tooltip or a drag of a list's row), a menu is open or a branch drag runs. Before that it
    /// waits until the details panel is not reading a commit's details
    /// (<see cref="ShellDriver.WaitForCommitDetails"/>). The step's time is noted for design 17.6's
    /// budget (artifacts/visual/phase-6/timings/).
    /// </summary>
    protected static Screenshot Shot(ShellDriver app, string name, bool keepPointer = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        ClaimPictureName(name);
        return TakePicture(app, name, keepPointer);
    }

    /// <summary><see cref="Shot"/> for a name already claimed (<see cref="ClaimPictureName"/>).</summary>
    private static Screenshot TakePicture(ShellDriver app, string name, bool keepPointer)
    {
        var screenshot = CaptureForPicture(app, keepPointer);
        screenshot.Save(Phase, name);
        NoteTime(app.MarkStep(name));
        return screenshot;
    }

    /// <summary><see cref="Shot"/>'s capture: the details panel's read waited for, the pointer at the neutral point unless kept, a menu is open or a drag runs.</summary>
    private static Screenshot CaptureForPicture(ShellDriver app, bool keepPointer)
    {
        app.WaitForCommitDetails();
        if (!keepPointer && !app.IsMenuOpen && !IsDragging(app))
        {
            app.MoveToNeutral();
        }

        return app.Capture();
    }

    /// <summary>
    /// Asserts a menu as the report writes it: the header, then each item's label in order, "—"
    /// for a separator, " (danger)" after an enabled destructive item and
    /// <c> (disabled: "reason")</c> after a disabled one (<see cref="MenuSnapshot.Lines"/>).
    /// </summary>
    protected static void AssertMenu(MenuSnapshot menu, params string[] lines)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var actual = menu.Lines();
        Assert.True(
            actual.SequenceEqual(lines),
            "The menu is not the expected one.\nExpected:\n  " + string.Join("\n  ", lines) + "\nActual:\n  " + string.Join("\n  ", actual));
    }

    /// <summary>Asserts which item has the highlight (D105: the first enabled one when the menu opens), or none when <paramref name="label"/> is null.</summary>
    protected static void AssertHighlighted(MenuSnapshot menu, string? label)
    {
        ArgumentNullException.ThrowIfNull(menu);
        Assert.Equal(label, menu.Highlighted?.Label);
    }

    /// <summary>
    /// Asserts a menu's height by phase 3's 4a formula, 8 + 24 + 26 × items + 9 × separators (±2),
    /// and that it lies at least 4 px inside the window.
    /// </summary>
    protected static void AssertMenuShape(ShellDriver app, MenuSnapshot menu)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(menu);
        var items = menu.Items.Count;
        var separators = menu.Entries.Count - items;
        var expected = MenuHeight(items, separators);
        Assert.True(
            Math.Abs(menu.Bounds.Height - expected) <= WindowLayout.MenuHeightTolerance,
            string.Create(CultureInfo.InvariantCulture, $"The menu is {menu.Bounds.Height} high; 4a gives {expected} for {items} items and {separators} separators."));
        var window = app.Window.ClientSize;
        var inset = WindowLayout.MenuEdgeInset - 0.5;
        Assert.True(
            menu.Bounds.Left >= inset && menu.Bounds.Top >= inset && menu.Bounds.Right <= window.Width - inset && menu.Bounds.Bottom <= window.Height - inset,
            $"The menu at {menu.Bounds} is not 4 px inside the {window.Width}x{window.Height} window.");
    }

    /// <summary>Asserts that a menu opened by the pointer at <paramref name="pointer"/> has its top-left 2 right of and 2 below it (±2, 4a).</summary>
    protected static void AssertMenuAtPointer(MenuSnapshot menu, Point pointer)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var expected = pointer + new Point(WindowLayout.MenuPointerGap, WindowLayout.MenuPointerGap);
        Assert.True(
            Math.Abs(menu.Bounds.X - expected.X) <= 2 && Math.Abs(menu.Bounds.Y - expected.Y) <= 2,
            $"The menu's top-left is {menu.Bounds.TopLeft}, expected {expected} (the pointer + (2, 2)).");
    }

    /// <summary>
    /// "No menu opens" / "no menu is open", as phase 3's conventions assert it: once the input is
    /// settled and the tab is idle, the menu layer has no menu, no <c>Context menu opened</c> line
    /// was logged since <paramref name="logMark"/> (<see cref="ShellDriver.LogMark"/>, read before
    /// the input; null to skip), and no menu is in the window.
    /// </summary>
    protected static async Task AssertNoMenuAsync(ShellDriver app, int? logMark)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Settle();
        if (app.Session.Shell.ActiveTab.Repository is not null)
        {
            await app.WaitForIdleAsync();
        }

        Assert.False(app.Session.Shell.Menu.IsOpen, $"A menu is open: {app.Session.Shell.Menu.Current?.HeaderText}.");
        Assert.Null(app.Session.Shell.Menu.Current);
        if (logMark is { } mark)
        {
            var opened = app.LogLinesSince(mark).Where(line => line.Contains(": Context menu opened: ", StringComparison.Ordinal)).ToList();
            Assert.True(opened.Count == 0, "A menu was opened since the input:\n" + string.Join('\n', opened));
        }

        Assert.Equal(0, app.MenuCount);
    }

    /// <summary>"No dialog": once the app is idle after the input (the operation's refresh included), the dialog layer has nothing open.</summary>
    protected static async Task AssertNoDialogAsync(ShellDriver app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.Session.Shell.ActiveTab.Repository is not null)
        {
            await app.WaitForIdleAsync();
        }

        var current = app.Session.Services.DialogHost.Current;
        Assert.True(current is null, $"A dialog is open: {current?.GetType().Name} \"{current?.Title}\".");
        Assert.Null(app.TopDialogView);
    }

    /// <summary>
    /// Asserts the status bar's operation text (<c>OperationStatus</c>) and its tone (secondary,
    /// the warning colour for a stop with conflicts, the danger colour for "&lt;Title&gt; failed"),
    /// and that the text is drawn in the tone's colour of the active theme.
    /// </summary>
    protected static void AssertStatus(ShellDriver app, string text, OperationStatusTone tone = OperationStatusTone.Secondary)
    {
        ArgumentNullException.ThrowIfNull(app);
        var status = app.Session.Shell.ActiveTab.OperationStatus;
        Assert.Equal(text, status.Text);
        Assert.Equal(tone, status.Tone);
        var block = (TextBlock)app.FindByAutomationId("OperationStatus");
        Assert.Equal(text, block.Text);
        var palette = PaletteOf(app);
        var colour = tone switch
        {
            OperationStatusTone.Warning => palette.Warning,
            OperationStatusTone.Danger => palette.Danger,
            _ => palette.TextSecondary,
        };
        Assert.Equal(colour, (block.Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary>Asserts the status bar's branch text (<c>CurrentBranch</c>): a branch's name or "Detached at &lt;sha&gt;".</summary>
    protected static void AssertBranch(ShellDriver app, string text)
    {
        ArgumentNullException.ThrowIfNull(app);
        Assert.Equal(text, ((TextBlock)app.FindByAutomationId("CurrentBranch")).Text);
    }

    /// <summary>
    /// Asserts the error dialog on top as phase 3's "Git's own texts" say: always the app's own
    /// parts (title, summary, "In &lt;repo&gt;", the command, the buttons); for a dialog with git's
    /// output, that <c>ErrorOutput</c> is exactly <c>GitOutputCleaner.Clean</c> of the failed
    /// call's own standard error and output as the call log recorded them; on every git, without
    /// the app's code, that it is what git printed (<see cref="AssertGitOutputShown"/>, with the
    /// classifying key line); then, on Git 2.36, the report's quoted lines word for word. Returns
    /// what the dialog shows.
    /// </summary>
    protected static async Task<ErrorDialogSnapshot> AssertErrorDialogAsync(ShellDriver app, ExpectedErrorDialog expected)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(expected);
        var dialog = app.ErrorDialog();
        Assert.Equal(expected.Title, dialog.Title);
        Assert.Equal(expected.Summary, dialog.Summary);
        Assert.Equal("In " + expected.Repository, dialog.Repository);
        Assert.Equal(expected.Command, dialog.Command);
        Assert.Equal(expected.Buttons, dialog.Buttons);
        if (expected.Command is null)
        {
            Assert.Null(dialog.Output);
            return dialog;
        }

        Assert.NotNull(dialog.Output);
        if (!expected.OutputFromGit)
        {
            Assert.Equal(expected.Lines, dialog.OutputLines);
            return dialog;
        }

        var key = expected.KeyLine
            ?? throw new Xunit.Sdk.XunitException("A dialog of git's output needs the line that classifies the failure (ExpectedErrorDialog.KeyLine): on another git than 2.36 it is what proves the output.");
        var call = app.LastCall(expected.Command);
        Assert.False(call.Succeeded, $"The call \"{call.CommandText}\" succeeded; the dialog shows a failure.");
        Assert.Equal(ShellDriver.CleanedOutputOf(call), dialog.Output);
        AssertGitOutputShown(call, key, dialog.Output);
        if (await IsReportGitAsync())
        {
            Assert.Equal(expected.Lines, dialog.OutputLines);
        }

        return dialog;
    }

    /// <summary>
    /// What the error dialog shows of a failed call (<paramref name="shown"/>), held against what
    /// git printed as the call log recorded it, by rules of the test's own and not the app's
    /// cleaner: <paramref name="keyLine"/> is a line git printed and a line of the dialog; every
    /// line of the dialog is a line git printed; every line of standard error that starts with
    /// <c>fatal:</c>, <c>error:</c> or <c> ! [</c> is shown; and no line shows git's progress count
    /// or a carriage return. <paramref name="appLines"/> are lines the app adds after git's own
    /// (part W: <c>error: Could not fetch origin</c>, which <c>FetchAsync(null)</c> appends, D109):
    /// they must be the dialog's last lines, in this order, and the rules above hold for the lines
    /// before them.
    /// </summary>
    internal static void AssertGitOutputShown(GitCallRecord call, string keyLine, string shown, IReadOnlyList<string>? appLines = null)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentException.ThrowIfNullOrEmpty(keyLine);
        ArgumentNullException.ThrowIfNull(shown);
        var error = PrintedLines(call.StandardError);
        var printed = error.Concat(PrintedLines(call.StandardOutput)).ToHashSet(StringComparer.Ordinal);
        var lines = shown.Split('\n');
        if (appLines is { Count: > 0 })
        {
            var last = lines.TakeLast(appLines.Count).ToList();
            Assert.True(
                lines.Length > appLines.Count && last.SequenceEqual(appLines, StringComparer.Ordinal),
                "The dialog does not end with the app's own lines\n  " + string.Join("\n  ", appLines) + "\nafter git's. It shows:\n" + shown);
            lines = lines[..^appLines.Count];
        }

        Assert.True(printed.Contains(keyLine), $"git did not print the key line \"{keyLine}\". It printed:\n{call.StandardError}\n{call.StandardOutput}");
        Assert.True(lines.Contains(keyLine, StringComparer.Ordinal), $"The dialog does not show the key line \"{keyLine}\". It shows:\n{shown}");
        foreach (var line in lines)
        {
            Assert.True(printed.Contains(line), $"The dialog shows a line git did not print: \"{line}\".");
        }

        foreach (var line in error.Where(line => line.StartsWith("fatal:", StringComparison.Ordinal) || line.StartsWith("error:", StringComparison.Ordinal) || line.StartsWith(" ! [", StringComparison.Ordinal)))
        {
            Assert.True(lines.Contains(line, StringComparer.Ordinal), $"The dialog leaves out git's line \"{line}\". It shows:\n{shown}");
        }

        Assert.DoesNotContain('\r', shown);
        Assert.DoesNotMatch(GitProgressCount(), shown);
    }

    /// <summary>The lines of git's raw output: split at every line feed and carriage return, without trailing spaces and empty lines.</summary>
    private static List<string> PrintedLines(string output) =>
        [.. output.Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0)];

    /// <summary>The count of a progress line of git, as in <c>Receiving objects:  45% (9/20)</c>.</summary>
    [GeneratedRegex(@"\d{1,3}% \(\d+/\d+\)")]
    private static partial Regex GitProgressCount();

    /// <summary>
    /// Asserts the confirmation on top (D69, with phase 4 design 7.2's texts): its title
    /// (<c>ConfirmTitle</c>), question (<c>ConfirmQuestion</c>), detail (<c>ConfirmDetail</c>,
    /// hidden when <paramref name="detail"/> is empty), the cancel button's label
    /// (<paramref name="cancelText"/>: "Cancel", or "Keep editing" for a question about leaving
    /// work, phase 4 design 7.2) with the keyboard focus, and the confirm button's label, in the danger
    /// style when <paramref name="destructive"/> (else the primary one). Waits for the dialog first.
    /// </summary>
    protected static async Task AssertConfirmationAsync(
        ShellDriver app,
        string title,
        string question,
        string detail,
        string confirmText,
        bool destructive = true,
        string cancelText = "Cancel")
    {
        ArgumentNullException.ThrowIfNull(app);
        var dialog = await app.WaitForDialogAsync<ConfirmationViewModel>();
        var view = app.TopDialogView ?? throw new InvalidOperationException("The confirmation has no view in the window.");
        Assert.Equal(title, ((TextBlock)app.FindByAutomationId(view, "ConfirmTitle")).Text);
        Assert.Equal(question, ((TextBlock)app.FindByAutomationId(view, "ConfirmQuestion")).Text);
        var detailBlock = (TextBlock)app.FindByAutomationId(view, "ConfirmDetail");
        Assert.Equal(detail.Length > 0, detailBlock.IsEffectivelyVisible);
        if (detail.Length > 0)
        {
            Assert.Equal(detail, detailBlock.Text);
        }

        var cancel = (Button)app.FindByAutomationId(view, "CancelButton");
        Assert.Equal(cancelText, cancel.Content);
        await app.WaitForAsync(() => cancel.IsFocused, "the confirmation's cancel button to have the keyboard focus");
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal(confirmText, confirm.Content);
        Assert.Equal(destructive, confirm.Classes.Contains("danger"));
        Assert.Equal(destructive, dialog.IsDestructive);
    }

    /// <summary>
    /// Asserts the in-progress banner (<c>ConflictBanner</c>, phase 4 design 5.2, D122 as amended): its
    /// text (<c>BannerText</c>); each of its buttons, Continue (<c>BannerContinueButton</c>), Skip
    /// (<c>BannerSkipButton</c>) and Abort (<c>BannerAbortButton</c>), shown or not as expected
    /// (null: not shown), with its label, whether it is enabled and its tooltip (a disabled one's
    /// reason, shown on the disabled button too); the buttons in that order left to right, 26
    /// high, 6 apart, the last 8 from the banner's right edge; whether the text and the buttons
    /// fit on <paramref name="oneLine"/> (one line of text, the buttons beside it, the banner
    /// exactly 36 high; "more": higher than 36), and where the buttons stand,
    /// <paramref name="placement"/> (beside the text, which keeps at least 240 px, or on a row of
    /// their own under it). The banner is at least 36 high. Null for <paramref name="oneLine"/> or
    /// <paramref name="placement"/>: the check states nothing about it. Returns where the banner is.
    /// </summary>
    protected static Rect AssertBanner(
        ShellDriver app,
        string text,
        string? abortText,
        BannerButton? continueButton = null,
        BannerButton? skipButton = null,
        bool? oneLine = null,
        BannerButtonPlacement? placement = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Settle();
        var bounds = app.BannerBounds ?? throw new Xunit.Sdk.XunitException("No banner is shown.");
        var textBlock = (TextBlock)app.FindByAutomationId("BannerText");
        Assert.Equal(text, textBlock.Text);

        var continueControl = AssertBannerButton(app, ShellDriver.BannerContinueId, "Continue", continueButton);
        var skipControl = AssertBannerButton(app, ShellDriver.BannerSkipId, "Skip", skipButton);
        var abortControl = AssertBannerButton(app, "BannerAbortButton", "Abort", abortText is null ? null : new BannerButton(abortText));
        var buttons = new[] { continueControl, skipControl, abortControl }.OfType<Button>().Select(app.BoundsOf).ToList();

        var problems = BannerLayoutProblems(bounds, app.BoundsOf(textBlock), TextLineCount(textBlock), buttons, oneLine, placement);
        Assert.True(problems.Count == 0, "The banner's layout is not the expected one:\n" + string.Join('\n', problems));
        return bounds;
    }

    /// <summary>
    /// What is wrong with a banner's layout (phase 4 design 5.2), from where its parts are: the banner
    /// <paramref name="banner"/>, its text <paramref name="text"/> of <paramref name="textLines"/>
    /// lines, and its shown buttons <paramref name="buttons"/> in the order Continue, Skip, Abort.
    /// Empty when nothing is. Kept apart from the window so that its rules are tested on their own.
    /// </summary>
    internal static List<string> BannerLayoutProblems(Rect banner, Rect text, int textLines, IReadOnlyList<Rect> buttons, bool? oneLine, BannerButtonPlacement? placement)
    {
        var problems = new List<string>();
        var tolerance = 1.0;
        if (banner.Height < WindowLayout.BannerMinHeight - 0.5)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"The banner is {banner.Height} high, less than {WindowLayout.BannerMinHeight}."));
        }

        for (var index = 0; index < buttons.Count; index++)
        {
            var button = buttons[index];
            if (Math.Abs(button.Height - BannerButtonHeight) > 0.5)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"Button {index + 1} is {button.Height} high, not {BannerButtonHeight}."));
            }

            if (index > 0 && Math.Abs(button.Left - buttons[index - 1].Right - BannerButtonGap) > tolerance)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"Buttons {index} and {index + 1} are {button.Left - buttons[index - 1].Right} apart, not {BannerButtonGap}."));
            }
        }

        if (buttons.Count > 0 && Math.Abs(banner.Right - buttons[^1].Right - BannerButtonRightInset) > tolerance)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"The last button is {banner.Right - buttons[^1].Right} from the banner's right edge, not {BannerButtonRightInset}."));
        }

        var besideText = buttons.All(button => button.Left >= text.Right - 0.5 && button.Top < text.Bottom && button.Bottom > text.Top);
        var ownRow = buttons.Count > 0
            && buttons.All(button => button.Top >= text.Bottom - 0.5 && Math.Abs(button.Top - buttons[0].Top) < 0.5);
        if (placement is { } where && buttons.Count > 0)
        {
            if (where == BannerButtonPlacement.BesideText && !besideText)
            {
                problems.Add("The buttons do not stand beside the text: " + Describe(text, buttons));
            }
            else if (where == BannerButtonPlacement.OwnRow && !ownRow)
            {
                problems.Add("The buttons are not on a row of their own under the text: " + Describe(text, buttons));
            }
        }

        if (buttons.Count > 0 && besideText && text.Width < BannerTextMinWidth - 0.5)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"The buttons stand beside a text {text.Width} wide, less than {BannerTextMinWidth}: they belong on a row of their own."));
        }

        if (oneLine is { } single)
        {
            var isOneLine = textLines == 1 && Math.Abs(banner.Height - WindowLayout.BannerMinHeight) < 0.5 && (buttons.Count == 0 || besideText);
            if (single && !isOneLine)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"The banner is not on one line: {textLines} lines of text, {banner.Height} high, buttons {(besideText ? "beside the text" : "under it")}."));
            }
            else if (!single && (banner.Height < WindowLayout.BannerMinHeight + 0.5 || (textLines == 1 && besideText)))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"The banner takes one line: {textLines} lines of text, {banner.Height} high."));
            }
        }

        return problems;
    }

    /// <summary>
    /// Asserts one of the banner's buttons by its automation id: hidden (or not in the window)
    /// when <paramref name="expected"/> is null; else shown with its label, enabled or not, its
    /// tooltip when the check states one (a disabled button's reason always), shown on a disabled
    /// button too. Returns the shown button, or null.
    /// </summary>
    private static Button? AssertBannerButton(ShellDriver app, string automationId, string what, BannerButton? expected)
    {
        var button = app.Window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == automationId && control.IsEffectivelyVisible);
        if (expected is null)
        {
            Assert.True(button is null, $"The banner shows {what} (\"{button?.Content}\"); the check expects none.");
            return null;
        }

        Assert.True(button is not null, $"The banner shows no {what}; the check expects \"{expected.Text}\".");
        Assert.Equal(expected.Text, button!.Content as string);
        Assert.True(expected.Enabled == button.IsEffectivelyEnabled, $"The banner's {what} is {(button.IsEffectivelyEnabled ? "enabled" : "disabled")}.");
        if (!expected.Enabled || expected.ToolTip is not null)
        {
            Assert.Equal(expected.ToolTip, ToolTip.GetTip(button) as string);
        }

        if (!expected.Enabled)
        {
            Assert.True(ToolTip.GetShowOnDisabled(button), $"The disabled {what} does not show its reason (ToolTip.ShowOnDisabled).");
        }

        return button;
    }

    /// <summary>How many lines a text block's text takes as laid out.</summary>
    private static int TextLineCount(TextBlock block) => block.TextLayout.TextLines.Count;

    private static string Describe(Rect text, IReadOnlyList<Rect> buttons) =>
        string.Create(CultureInfo.InvariantCulture, $"text at {text}, buttons at {string.Join(", ", buttons)}");

    /// <summary>
    /// Asserts the graph as the report's tables write it, row by row from the top: each row's
    /// subject, node lane and colour, and its labels in drawing order (<see cref="CommitRow"/>:
    /// " ✓" after the branch HEAD is on, "HEAD" for a detached HEAD), with what the row states
    /// besides (short SHA, merge, stash, date, the lanes that end in its node); then the number of
    /// rows, the lane count with the Graph column's width (16 per lane plus 16, at least 48), the
    /// working-changes row (its text and ring) or its absence, and whether the Date column shows.
    /// With <paramref name="picture"/>, each node's colour (a stash's ring) and the working-changes
    /// ring are also sampled in it. The history must be complete.
    /// </summary>
    protected static void AssertGraph(
        ShellDriver app,
        IReadOnlyList<CommitRow> rows,
        int lanes,
        WorkingChangesRow? working = null,
        bool? showsDate = null,
        Screenshot? picture = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(rows);
        var graph = Graph(app);
        var data = graph.Data ?? throw new Xunit.Sdk.XunitException("The graph has no data.");
        Assert.True(data.IsComplete, "The graph's history is not complete.");
        var actual = Enumerable.Range(0, data.Count).Select(row => DescribeRow(graph, row)).ToList();
        Assert.True(
            data.Count == rows.Count && Enumerable.Range(0, rows.Count).All(row => RowMatches(graph, rows[row], row)),
            "The graph is not the expected one.\nExpected:\n  " + string.Join("\n  ", rows) + "\nActual:\n  " + string.Join("\n  ", actual));

        Assert.Equal(lanes, data.MaxLaneCount);
        Assert.Equal(WindowLayout.GraphColumnWidth(lanes), graph.Columns.Graph.Width);
        if (showsDate is { } date)
        {
            Assert.Equal(date, graph.Columns.ShowsDate);
        }

        Assert.True((working is not null) == graph.ShowsWorkingRow, $"The working-changes row is {(graph.ShowsWorkingRow ? "shown" : "hidden")}.");
        if (working is not null)
        {
            Assert.Equal(working.Text, graph.WorkingRowText ?? CommitGraphControl.DefaultWorkingRowText);
        }

        if (picture is null)
        {
            return;
        }

        var palette = PaletteOf(app);
        var origin = app.BoundsOf(graph).TopLeft;
        for (var index = 0; index < rows.Count; index++)
        {
            var expected = rows[index];
            var centre = new Point(origin.X + graph.LaneCenterX(expected.Lane), origin.Y + graph.RowBounds(index).Center.Y);
            if (expected.Stash)
            {
                AssertRing(picture, centre, palette.Lane(expected.Colour), $"row {index}'s stash ring");
            }
            else
            {
                AssertColour(palette.Lane(expected.Colour), picture, centre, $"node of row {index} (\"{expected.Message}\")");
            }
        }

        if (working is not null)
        {
            var ring = new Point(origin.X + graph.LaneCenterX(working.Lane), origin.Y + graph.WorkingRowBounds.Center.Y);
            AssertRing(picture, ring, palette.Lane(working.Colour), "the working-changes ring");
        }
    }

    private static bool RowMatches(CommitGraphControl graph, CommitRow want, int row)
    {
        var data = graph.Data!;
        var commit = data.CommitAt(row);
        var layout = data.RowAt(row);
        return commit.Subject == want.Message
            && layout.NodeLane == want.Lane
            && layout.NodeColor == want.Colour
            && GraphLabels(data, row).SequenceEqual(want.Labels)
            && (want.ShortSha is null || commit.Sha.StartsWith(want.ShortSha, StringComparison.Ordinal))
            && (want.Merge is not { } merge || merge == commit.Parents.Count > 1)
            && want.Stash == (commit.Kind == CommitKind.Stash)
            && (want.Date is null || graph.Dates?.Format(commit.AuthorDate) == want.Date)
            && (want.Ends is null || want.Ends.Order().SequenceEqual(EndingLanes(layout)));
    }

    private static string DescribeRow(CommitGraphControl graph, int row)
    {
        var data = graph.Data!;
        var commit = data.CommitAt(row);
        var layout = data.RowAt(row);
        return new CommitRow(
            commit.Subject,
            layout.NodeLane,
            layout.NodeColor,
            [.. GraphLabels(data, row)],
            commit.Sha[..7],
            commit.Parents.Count > 1,
            commit.Kind == CommitKind.Stash,
            graph.Dates?.Format(commit.AuthorDate),
            EndingLanes(layout)).ToString();
    }

    /// <summary>A row's labels as the report writes them: their texts in drawing order, " ✓" after the branch HEAD is on.</summary>
    private static IEnumerable<string> GraphLabels(CommitGraphData data, int row) =>
        data.LabelsAt(row).Select(label => label.Kind == RefLabelKind.CurrentBranch ? Current(label.Text) : label.Text);

    /// <summary>The lanes other than a row's own that end in its node, in order.</summary>
    private static List<int> EndingLanes(GraphRow layout) =>
        [.. layout.Lines.Where(line => line.Kind == GraphLineKind.IntoNode && line.FromLane != layout.NodeLane).Select(line => line.FromLane).Distinct().Order()];

    /// <summary>A ring 10 across and 2 wide: between 3 and 5 pixels right of its centre some pixel has the colour, and its centre does not.</summary>
    private static void AssertRing(Screenshot screenshot, Point centre, Color colour, string what)
    {
        var samples = Enumerable.Range(3, 3).Select(offset => screenshot.PixelAt(centre + new Point(offset, 0))).ToList();
        Assert.True(samples.Contains(colour), $"The {what} at {centre} is not {colour} anywhere from 3 to 5 pixels right of its centre: {string.Join(", ", samples)}.");
        Assert.NotEqual(colour, screenshot.PixelAt(centre));
    }

    /// <summary>
    /// Asserts which lines of the graph are drawn dashed (D161, design 10): exactly
    /// <paramref name="lines"/>, each a commit row, the line's kind and the lane it starts in, of
    /// all the lines of every row (<c>CommitGraphData.RowAt(row).Lines</c>) that carry
    /// <see cref="GraphLine.IsDashed"/>. Every other line is solid.
    /// </summary>
    protected static void AssertDashedLines(ShellDriver app, params (int Row, GraphLineKind Kind, int Lane)[] lines)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(lines);
        var data = Graph(app).Data ?? throw new Xunit.Sdk.XunitException("The graph has no data.");
        var dashed = Enumerable.Range(0, data.Count)
            .SelectMany(row => data.RowAt(row).Lines.Where(line => line.IsDashed).Select(line => (Row: row, line.Kind, Lane: line.FromLane)))
            .ToList();
        var expected = lines.OrderBy(line => line.Row).ThenBy(line => line.Kind).ThenBy(line => line.Lane).ToList();
        var actual = dashed.OrderBy(line => line.Row).ThenBy(line => line.Kind).ThenBy(line => line.Lane).ToList();
        Assert.True(
            expected.SequenceEqual(actual),
            "The dashed lines are not the expected ones.\nExpected:\n  " + string.Join("\n  ", expected) + "\nActual:\n  " + string.Join("\n  ", actual));
    }

    /// <summary>The backup refs (<c>refs/visualcommit/backup/</c>), parsed (<see cref="BackupRef"/>), with what each points at, in git's order of their names.</summary>
    protected static async Task<List<(BackupRef Ref, string Name, string Sha)>> BackupsAsync(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return [.. (await repo.BackupRefsAsync()).Select(entry => (BackupRef.Parse(entry.Name), entry.Name, entry.Sha))];
    }

    /// <summary>The git calls the app made since <paramref name="mark"/> (a count of <see cref="ShellDriver.GitCalls"/> read before the input), as their command lines.</summary>
    protected static List<string> CallsSince(ShellDriver app, int mark)
    {
        ArgumentNullException.ThrowIfNull(app);
        return [.. app.GitCalls().Skip(mark).Select(call => call.CommandText)];
    }

    /// <summary>Asserts that the app made the call <paramref name="command"/> since <paramref name="mark"/>, and returns its (last) record.</summary>
    protected static GitCallRecord AssertCalled(ShellDriver app, int mark, string command)
    {
        ArgumentNullException.ThrowIfNull(app);
        var call = app.GitCalls().Skip(mark).LastOrDefault(record => record.CommandText == command);
        Assert.True(call is not null, $"The app did not run \"{command}\". It ran:\n" + string.Join('\n', CallsSince(app, mark)));
        return call!;
    }

    /// <summary>Asserts the order of calls the app made since <paramref name="mark"/>: these commands, each after the one before.</summary>
    protected static void AssertCalledInOrder(ShellDriver app, int mark, params string[] commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var calls = CallsSince(app, mark);
        var at = -1;
        foreach (var command in commands)
        {
            var next = calls.FindIndex(at + 1, call => call == command);
            Assert.True(next > at, $"The app did not run \"{command}\" after the calls before it. It ran:\n" + string.Join('\n', calls));
            at = next;
        }
    }

    /// <summary>
    /// Waits until the queue logged the end of the operation <paramref name="title"/> since
    /// <paramref name="mark"/> (<see cref="ShellDriver.LogMark"/>, read before the input that
    /// started it), whatever its outcome, and the tab is idle, its refresh included. At most
    /// <paramref name="timeout"/> (<see cref="OperationChainTimeout"/> when null).
    /// </summary>
    protected static async Task WaitForOperationAsync(ShellDriver app, TempRepo repo, string title, int mark, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(repo);
        var limit = timeout ?? OperationChainTimeout;
        await app.WaitForLogLineAsync(AppLogFile.OperationEnded(NameOf(repo), title), mark: mark, timeout: limit);
        await app.WaitForIdleAsync(timeout: limit);
    }

    /// <summary>
    /// After a change made outside the app (part P's conventions, PD16; phase 4's inline wait of
    /// A4): waits, at most <see cref="OutsideChangeTimeout"/> each, for a status read logged after
    /// <paramref name="mark"/> (<see cref="ShellDriver.LogMark"/>, read before the change) with
    /// <paramref name="staged"/> staged and <paramref name="unstaged"/> unstaged files, then until
    /// the tab is idle, and, where the counts do not change, until the repository's status
    /// satisfies <paramref name="until"/> (<see cref="ShellDriver.WaitForStatusReadAsync"/>).
    /// </summary>
    protected static Task WaitForStatusReadAsync(ShellDriver app, int staged, int unstaged, int mark, Func<WorkingTreeStatus, bool>? until = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.WaitForStatusReadAsync(staged, unstaged, mark, until, OutsideChangeTimeout);
    }

    /// <summary>
    /// What the clone form shows for <paramref name="url"/>'s failed clone (part L's L32 step 2):
    /// <c>git clone --progress -- {url} cloned</c> run in <paramref name="parent"/> by the tests' git,
    /// before the app starts, in the session's git environment the app's calls get
    /// (<see cref="GitIsolation.SessionGitEnvironment"/> of <paramref name="dataDirectory"/>); it must
    /// fail and leave no folder <c>cloned</c>; then <c>GitOutputCleaner.Clean(standardError,
    /// standardOutput)</c>, the app's own cleaning (<c>GitRepository.Clone.cs</c>), trimmed as
    /// <c>WelcomeViewModel</c> trims it. Phase 1's <c>GitCloneErrorAsync</c> gives only the first
    /// <c>fatal:</c> line; this gives the whole text, so it holds on every git.
    /// </summary>
    protected static async Task<string> CloneErrorTextAsync(string url, string parent, string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        ArgumentException.ThrowIfNullOrEmpty(parent);
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        var git = VisualCommit.Git.GitLocator.FindExecutable(VisualCommit.Git.GitSearchContext.FromSystem())
            ?? throw new InvalidOperationException("Git was not found on this machine.");
        var result = await new VisualCommit.Git.GitRunner(git).RunAsync(
            new GitCommand("clone", "--progress", "--", url, "cloned") { WorkingDirectory = parent, Environment = GitIsolation.SessionGitEnvironment(dataDirectory) },
            TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded, $"The clone of {url} succeeded.");
        Assert.False(Directory.Exists(Path.Combine(parent, "cloned")), "The failed clone left a folder cloned.");
        return VisualCommit.Core.Operations.GitOutputCleaner.Clean(result.StandardError, result.StandardOutput).Trim();
    }

    /// <summary>
    /// Phase 1's <c>TypeCloneFormAsync</c> (part L's L32 step 2): once <c>CloneUrlBox</c> has the
    /// keyboard focus, <paramref name="url"/> typed into it, <paramref name="parent"/> into
    /// <c>CloneParentBox</c>, and <paramref name="name"/> over <c>CloneNameBox</c>'s text (selected
    /// first with the platform's command modifier and A).
    /// </summary>
    protected static async Task TypeCloneFormAsync(ShellDriver app, string url, string parent, string name)
    {
        ArgumentNullException.ThrowIfNull(app);
        await app.WaitForAsync(() => app.Find<TextBox>("CloneUrlBox").IsFocused, "the URL field to have the focus");
        app.Type(url);
        app.Click(app.Find<TextBox>("CloneParentBox"));
        app.Type(parent);
        app.Click(app.Find<TextBox>("CloneNameBox"));
        app.PressKey(
            Avalonia.Input.Key.A,
            app.CommandModifier == Avalonia.Input.KeyModifiers.Meta ? Avalonia.Input.RawInputModifiers.Meta : Avalonia.Input.RawInputModifiers.Control);
        app.Type(name);
    }

    /// <summary>The working tree's folder name: the tab's and the repository's name.</summary>
    protected static string NameOf(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return Path.GetFileName(repo.Path);
    }

    /// <summary>Asserts that no text in the window is cut off, except shortened text that shows the whole in its tooltip (phase 2's rule).</summary>
    protected static void AssertNoClippedText(ShellDriver app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var problems = LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true);
        Assert.True(problems.Count == 0, "Clipped text:\n" + string.Join("\n", problems));
    }

    /// <summary>Asserts a control's tooltip text (<c>ToolTip.GetTip</c>) without picturing it, for reasons the steps do not rest the pointer on.</summary>
    protected static void AssertTip(Control control, string? expected)
    {
        ArgumentNullException.ThrowIfNull(control);
        Assert.Equal(expected, ToolTip.GetTip(control) as string);
    }

    /// <summary>Asserts that <paramref name="text"/> is in no log line or call record.</summary>
    protected static void AssertNotWritten(ShellDriver app, string text)
    {
        ArgumentNullException.ThrowIfNull(app);
        var found = app.WhereWritten(text);
        Assert.True(found.Count == 0, $"\"{text}\" was written down:\n" + string.Join('\n', found));
    }

    /// <summary>The colour at a window position of a picture, against the expected one.</summary>
    protected static void AssertColour(Color expected, Screenshot screenshot, Point position, string what)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        var actual = screenshot.PixelAt(position);
        Assert.True(actual == expected, $"The {what} at {position} is {actual}, expected {expected}.");
    }

    /// <summary>The date the harness gives the app's commits and ref updates (D72): 2026-01-02 09:00 UTC.</summary>
    protected static readonly DateTimeOffset AppCommitTime = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The scenario's clock time of a commit made at 12:<paramref name="minute"/> on 2026-01-01 UTC.</summary>
    protected static DateTimeOffset ScenarioTime(int minute) => new(2026, 1, 1, 12, minute, 0, TimeSpan.Zero);

    /// <summary>
    /// Asserts that a date git printed in its strict ISO form (<c>%aI</c>, <c>%cI</c>) is the
    /// instant <paramref name="expected"/> in the zone it carries. Git 2.36 writes UTC as
    /// <c>+00:00</c> and git 2.55 as <c>Z</c>, so the text is never compared (<see cref="GitIsoDates"/>).
    /// </summary>
    protected static void AssertGitDate(DateTimeOffset expected, string gitText)
    {
        ArgumentNullException.ThrowIfNull(gitText);
        var actual = GitIsoDates.Parse(gitText);
        Assert.Equal((expected, expected.Offset), (actual, actual.Offset));
    }

    /// <summary>
    /// The working tree's folder as git names it (<c>rev-parse --show-toplevel</c>), which is the
    /// folder the app shows and passes on, in the platform's form, full and without a trailing
    /// separator. Compare a path the app reports with a path built from this, never from
    /// <c>repo.Path</c>.
    /// </summary>
    protected static async Task<string> WorkingTreeAsync(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var topLevel = (await repo.GitAsync("rev-parse", "--show-toplevel")).StandardOutput.TrimEnd('\r', '\n');
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(topLevel));
    }

    /// <summary>The graph's rows (the control <c>GraphRows</c>).</summary>
    protected static CommitGraphControl Graph(ShellDriver app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Find<CommitGraphControl>("GraphRows");
    }

    /// <summary>A toolbar button by its automation id (<c>FetchButton</c>, <c>StashButton</c>, …).</summary>
    protected static ToolbarButton ToolbarButton(ShellDriver app, string automationId)
    {
        ArgumentNullException.ThrowIfNull(app);
        return (ToolbarButton)app.FindByAutomationId(automationId);
    }

    /// <summary>The palette of the theme the window shows.</summary>
    protected static Palette PaletteOf(ShellDriver app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Window.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light ? Light : Dark;
    }

    /// <summary>The visible texts inside a control, in reading order (top to bottom, then left to right).</summary>
    protected static List<string> TextsIn(Visual region) => VisibleTexts.In(region);

    /// <summary>Whether a branch drag runs in the window: its pointer must stay where the drag is.</summary>
    private static bool IsDragging(ShellDriver app)
    {
        try
        {
            return app.BranchDrag.IsDragging;
        }
        catch (InvalidOperationException)
        {
            // No drag layer in the window (no repository open): no drag either.
            return false;
        }
    }

    /// <summary>
    /// Takes <paramref name="name"/> for a picture of this run: refused when it is no picture name of
    /// the report (<see cref="PictureName"/>) or a step of this run has saved it already, so that two
    /// steps saving one name fail the run instead of one overwriting the other (design 11.8).
    /// </summary>
    /// <exception cref="ArgumentException">The name is not of the report's form.</exception>
    /// <exception cref="InvalidOperationException">The name was saved already in this run.</exception>
    protected static void ClaimPictureName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!PictureName().IsMatch(name))
        {
            throw new ArgumentException($"\"{name}\" is not a picture name of the report: <check><step>-<what>, such as w4a-fetch-held.", nameof(name));
        }

        lock (TimingGate)
        {
            if (!SavedPictures.Add(name))
            {
                throw new InvalidOperationException($"The picture \"{name}\" was saved already in this run: two steps must not save one name (design 11.8).");
            }
        }
    }

    /// <summary>Notes a step's time in artifacts/visual/phase-6/timings/&lt;test&gt;.txt; the first note of a test in a run starts its file afresh.</summary>
    private static void NoteTime(StepTime step)
    {
        var test = TestContext.Current.Test?.TestDisplayName ?? "unknown";
        var file = Path.Combine(Path.GetDirectoryName(VisualRun.FolderFor(Phase))!, "timings", FileNameOf(test) + ".txt");
        lock (TimingGate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            if (TimingsStarted.Add(test))
            {
                File.WriteAllText(file, test + "\n");
            }

            File.AppendAllText(file, step + "\n");
        }
    }

    private static string FileNameOf(string text) =>
        string.Concat(text.Select(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '_'));

    /// <summary>
    /// A picture's name: the check (its part's letter, as the report numbers phase 6's checks: w
    /// for workspaces and the dashboard, k for keys and the palette, s for settings and the theme's
    /// following, g for ignored files and refspecs, l for the look, design 17.3; and its number; or
    /// the number alone), the step (up to three letters or digits), then words joined by hyphens:
    /// <c>w4a-fetch-held</c>, <c>s15b-system-light</c>, <c>l31d-settings-light</c>. The letters C, O,
    /// Q, R and T are no part's, so that a name never reads as a requirement's number.
    /// </summary>
    [GeneratedRegex(@"^[gklsw]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    protected static partial Regex PictureName();
}

/// <summary>
/// The error dialog a check expects (<see cref="Phase6Checks.AssertErrorDialogAsync"/>).
/// </summary>
/// <param name="Title">"Rebase failed", "Undo failed", … (phase 4 design 10.1, design 12.1).</param>
/// <param name="Summary">The summary, word for word.</param>
/// <param name="Repository">The repository's name: the dialog shows "In &lt;name&gt;".</param>
/// <param name="Command">The failed call as <c>ErrorCommand</c> shows it, or null for a dialog without git's output (then there is no <c>ErrorOutput</c> either).</param>
/// <param name="Buttons">The visible buttons, left to right: "Copy output", the extra action if any, "Close".</param>
/// <param name="Lines">The output's lines as the report quotes them (Git 2.36's, or the app's own when <paramref name="OutputFromGit"/> is false).</param>
/// <param name="KeyLine">The line that classifies the failure, asserted on any git; null when the report names none.</param>
/// <param name="OutputFromGit">False for an output the app writes itself, asserted word for word on every git.</param>
public sealed record ExpectedErrorDialog(
    string Title,
    string Summary,
    string Repository,
    string? Command,
    IReadOnlyList<string> Buttons,
    IReadOnlyList<string> Lines,
    string? KeyLine = null,
    bool OutputFromGit = true);

/// <summary>
/// One cell of a dashboard row as the report writes it (scenario facts 0, part W's "A row is
/// written"): <c>`text` (tone; tooltip)</c>, "empty" for a cell with no text; the Ahead / behind
/// cell's arrows as <c>↑ a ↓ b</c> with their automation name (<see cref="Arrows"/>). For
/// <c>Phase6Checks.AssertRow</c>.
/// </summary>
/// <param name="Text">What the cell shows; the arrows as <c>↑ 2</c>, <c>↓ 3</c>, <c>↑ 1 ↓ 2</c>; empty for nothing.</param>
/// <param name="Tone">Its colour: primary, secondary, warning or danger; not asserted for an empty cell.</param>
/// <param name="ToolTip">Its tooltip, or null for none: asserted as given.</param>
/// <param name="AutomationName">What UI Automation reads where that is not the text: the arrows' <c>1 ahead, 2 behind</c>; null for the text.</param>
public sealed record ExpectedCell(string Text, DashboardTone Tone, string? ToolTip = null, string? AutomationName = null)
{
    /// <summary>A cell with no text and no tooltip ("empty").</summary>
    public static ExpectedCell Empty { get; } = new(string.Empty, DashboardTone.Secondary);

    /// <summary>The cell shows nothing.</summary>
    public bool IsEmpty => Text.Length == 0;

    /// <summary>Its automation name: <see cref="AutomationName"/>, or the text.</summary>
    public string Name => AutomationName ?? Text;

    /// <summary><paramref name="text"/> in <c>VcTextPrimaryBrush</c>.</summary>
    public static ExpectedCell Primary(string text, string? toolTip = null) => new(text, DashboardTone.Primary, toolTip);

    /// <summary><paramref name="text"/> in <c>VcTextSecondaryBrush</c>.</summary>
    public static ExpectedCell Secondary(string text, string? toolTip = null) => new(text, DashboardTone.Secondary, toolTip);

    /// <summary><paramref name="text"/> in <c>VcWarningBrush</c>.</summary>
    public static ExpectedCell Warning(string text, string? toolTip = null) => new(text, DashboardTone.Warning, toolTip);

    /// <summary><paramref name="text"/> in <c>VcDangerBrush</c>.</summary>
    public static ExpectedCell Danger(string text, string? toolTip = null) => new(text, DashboardTone.Danger, toolTip);

    /// <summary>
    /// The Ahead / behind cell's arrows (2.6): <c>IconArrowUp</c> with <paramref name="ahead"/> and
    /// <c>IconArrowDown</c> with <paramref name="behind"/>, each only above 0, in the secondary colour,
    /// written <c>↑ 1 ↓ 2</c>; its automation name (<c>1 ahead, 2 behind</c>) and tooltip as the report gives them.
    /// </summary>
    public static ExpectedCell Arrows(int ahead, int behind, string automationName, string toolTip)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ahead);
        ArgumentOutOfRangeException.ThrowIfNegative(behind);
        if (ahead == 0 && behind == 0)
        {
            throw new ArgumentException("The arrows show only with a count above 0: write the cell's text instead (Up to date).");
        }

        var text = string.Join(' ', new[]
        {
            ahead > 0 ? string.Create(CultureInfo.InvariantCulture, $"↑ {ahead}") : null,
            behind > 0 ? string.Create(CultureInfo.InvariantCulture, $"↓ {behind}") : null,
        }.OfType<string>());
        return new ExpectedCell(text, DashboardTone.Secondary, toolTip, automationName);
    }

    /// <summary>The cell as the report writes it: <c>`text` (tone; tooltip)</c>, the automation name after it where it is not the text; "empty".</summary>
    public override string ToString()
    {
        if (IsEmpty && ToolTip is null)
        {
            return "empty";
        }

        var name = AutomationName is null || AutomationName == Text ? string.Empty : $"; name `{AutomationName}`";
        return $"`{Text}` ({Phase6Checks.Words(Tone)}; {(ToolTip is null ? "no tooltip" : "`" + ToolTip + "`")}{name})";
    }
}

/// <summary>
/// A dashboard row as the report writes it (part W's "A row is written"): <c>name · branch ·
/// ahead / behind · changes · state · operation</c>, its name the one given to
/// <c>Phase6Checks.AssertRow</c>; with what the row shows besides, each false or unasserted
/// unless set: its folder (<see cref="Folder"/>), selected, <c>Cancel</c>, <c>Details…</c>, a read
/// outstanding. For example
/// <c>new ExpectedRow("main", ExpectedCell.Arrows(2, 0, "2 ahead", "2 commits ahead of origin/main"), ExpectedCell.Secondary("Clean"), ExpectedCell.Empty, ExpectedCell.Empty)</c>
/// for web-app in W0.
/// </summary>
/// <param name="Branch">The Branch cell's text (primary, WD34), empty for none.</param>
/// <param name="AheadBehind">The Ahead / behind cell.</param>
/// <param name="Changes">The Changes cell.</param>
/// <param name="State">The State cell.</param>
/// <param name="Operation">The Operation cell.</param>
public sealed record ExpectedRow(string Branch, ExpectedCell AheadBehind, ExpectedCell Changes, ExpectedCell State, ExpectedCell Operation)
{
    /// <summary>
    /// The folder as the workspace holds it: the Repository cell's tooltip (2.6) and, where the
    /// Folder column shows (1920 wide), the Folder cell's text, whose cut is not asserted (WD29).
    /// Null: neither is asserted.
    /// </summary>
    public string? Folder { get; init; }

    /// <summary>The row is selected (<c>VcSelectionBrush</c>).</summary>
    public bool Selected { get; init; }

    /// <summary>Its <c>DashboardRowCancel</c> shows.</summary>
    public bool ShowsCancel { get; init; }

    /// <summary>Its <c>DashboardRowDetails</c> (<c>Details…</c>) shows.</summary>
    public bool ShowsDetails { get; init; }

    /// <summary>It has a read running or waiting for a slot (<c>IsReading</c>, WD37).</summary>
    public bool IsReading { get; init; }

    /// <summary>The row after its name, as the report writes it.</summary>
    public override string ToString() =>
        $"{(Branch.Length == 0 ? "empty" : "`" + Branch + "`")} · {AheadBehind} · {Changes} · {State} · {Operation}{Phase6Checks.RowFlags(Selected, ShowsCancel, ShowsDetails, IsReading)}";
}

/// <summary>
/// A button of the dashboard's header as a check expects it: enabled or not, and its tooltip (a
/// disabled one's reason), which it shows when disabled too.
/// </summary>
public sealed record ExpectedHeaderButton(bool IsEnabled, string? ToolTip)
{
    /// <summary>Enabled, with <paramref name="toolTip"/>.</summary>
    public static ExpectedHeaderButton Enabled(string toolTip) => new(true, toolTip);

    /// <summary>Disabled, its tooltip <paramref name="reason"/>.</summary>
    public static ExpectedHeaderButton Disabled(string reason) => new(false, reason);

    public override string ToString() => $"{(IsEnabled ? "enabled" : "disabled")}, \"{ToolTip}\"";
}

/// <summary>
/// The dashboard's header as the report writes it (part W's conventions, W1 step 3, TD and TR;
/// design 2.5, 4.9, 4.10): the workspace's name and count, the four buttons left of
/// <c>DashboardMore</c>, the run's text and tone, the bar, <c>Cancel all</c> and the notice. For
/// <c>Phase6Checks.AssertDashboardHeader</c>. <see cref="Idle"/> gives TD's header and
/// <see cref="Running"/> TR's; a check changes the rest with <c>with { … }</c>.
/// </summary>
/// <param name="Title"><c>DashboardTitle</c>: the workspace's name.</param>
/// <param name="Count"><c>DashboardCount</c>: <c>10 repositories</c>, <c>1 repository</c>, <c>No repositories</c>.</param>
/// <param name="AddRepository"><c>DashboardAddRepository</c>.</param>
/// <param name="FetchAll"><c>DashboardFetchAll</c>: the toolbar's Fetch.</param>
/// <param name="PullAll"><c>DashboardPullAll</c>: the toolbar's Pull.</param>
/// <param name="Refresh"><c>DashboardRefresh</c>.</param>
public sealed record ExpectedDashboardHeader(
    string Title,
    string Count,
    ExpectedHeaderButton AddRepository,
    ExpectedHeaderButton FetchAll,
    ExpectedHeaderButton PullAll,
    ExpectedHeaderButton Refresh)
{
    /// <summary><c>DashboardBulkText</c>, the run's text (4.9), or null while none shows.</summary>
    public string? RunText { get; init; }

    /// <summary>The run text's tone: secondary, the warning colour, or the danger colour for a run with a failure (4.9).</summary>
    public OperationStatusTone RunTone { get; init; } = OperationStatusTone.Secondary;

    /// <summary><c>DashboardBulkProgress</c>'s value while the bar shows (a run runs), or null while it is hidden.</summary>
    public int? Progress { get; init; }

    /// <summary><c>DashboardCancelAll</c>'s tooltip while it shows (<c>Cancel the rest of Fetch all</c>), or null while it is hidden (WD31).</summary>
    public string? CancelAll { get; init; }

    /// <summary><c>DashboardNotice</c> (a failed save, 1.3), or null while hidden.</summary>
    public string? Notice { get; init; }

    /// <summary>
    /// TD's header (part W's conventions): every button enabled, <c>Add a repository to {workspace}</c>,
    /// <c>Fetch every repository of {workspace} (Ctrl+Shift+F)</c>, <c>Pull every repository of
    /// {workspace} (Ctrl+Shift+L)</c> and <c>Read the repositories of {workspace} again (F5)</c>;
    /// no run text, no bar, no <c>Cancel all</c>, no notice.
    /// </summary>
    public static ExpectedDashboardHeader Idle(string workspace, string count) => new(
        workspace,
        count,
        ExpectedHeaderButton.Enabled($"Add a repository to {workspace}"),
        ExpectedHeaderButton.Enabled($"Fetch every repository of {workspace} (Ctrl+Shift+F)"),
        ExpectedHeaderButton.Enabled($"Pull every repository of {workspace} (Ctrl+Shift+L)"),
        ExpectedHeaderButton.Enabled($"Read the repositories of {workspace} again (F5)"));

    /// <summary>
    /// TR's header (part W's conventions, 4.9): TD's, but Fetch all and Pull all disabled with
    /// <c>Wait for Fetch all to finish</c> (or <c>Pull all</c>), the run's <paramref name="runText"/>
    /// (secondary), the bar at <paramref name="progress"/> and <c>Cancel all</c> with <c>Cancel the
    /// rest of Fetch all</c> (or <c>Pull all</c>).
    /// </summary>
    public static ExpectedDashboardHeader Running(string workspace, string count, BulkKind kind, string runText, int progress)
    {
        var run = kind == BulkKind.Fetch ? "Fetch all" : "Pull all";
        return Idle(workspace, count) with
        {
            FetchAll = ExpectedHeaderButton.Disabled($"Wait for {run} to finish"),
            PullAll = ExpectedHeaderButton.Disabled($"Wait for {run} to finish"),
            RunText = runText,
            Progress = progress,
            CancelAll = $"Cancel the rest of {run}",
        };
    }

    /// <summary>The header as the report writes it.</summary>
    public override string ToString() =>
        $"{Title}, \"{Count}\"; run {(RunText is null ? "none" : "\"" + RunText + "\"")} ({RunTone}), bar {(Progress is { } at ? string.Create(CultureInfo.InvariantCulture, $"at {at}") : "hidden")}, Cancel all {(CancelAll is null ? "hidden" : "enabled, \"" + CancelAll + "\"")}; "
        + $"Add repository… {AddRepository}; Fetch all {FetchAll}; Pull all {PullAll}; Refresh {Refresh}; notice {(Notice is null ? "none" : "\"" + Notice + "\"")}";
}
