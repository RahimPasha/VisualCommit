// Phase 7b's support (docs/phase-7b-design.md 2.7): copied from Phase7aChecks.Support.cs with 7b's
// names: Shot and its picture and timing helpers (artifacts/visual/phase-7b/timings/), the menu,
// dialog, status, clipped-text, tooltip and error-dialog assertions, the call assertions (a long
// mark, presence and order only), and the picture pattern of design 0.3; the start of a check on
// the scenario "hosting7b" served by the three fakes (ServeAsync, OpenAsync, StartEmptyAsync).
// Shared helpers live here only; a part's own go in its part files.
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
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.VisualTests.Phase7b;

public abstract partial class Phase7bChecks
{
    private static readonly Lock TimingGate = new();
    private static readonly HashSet<string> TimingsStarted = [];

    /// <summary>The picture names saved in this run (this test process), so that two steps saving one name fail instead of overwriting (7a design 11.8).</summary>
    private static readonly HashSet<string> SavedPictures = new(StringComparer.Ordinal);

    /// <summary>The phase, as the docs write it (design 0.3).</summary>
    protected const string Phase = Phase7bExpectations.Phase;

    /// <summary>The active tab's requests section ("Pull requests" or "Merge requests") as lines: "v"/"&gt;" title count; a row's name; a message row's "[Kind] text" (part P).</summary>
    protected static List<string> Section(ShellDriver app)
    {
        app.Settle();
        var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
        var start = rows.FindIndex(row => row is RefSectionRow { Section: RefSection.PullRequests });
        var lines = new List<string>();
        for (var i = start; i < rows.Count && (i == start || rows[i] is not RefSectionRow); i++)
        {
            lines.Add(rows[i] switch
            {
                RefSectionRow section => $"{(section.IsOpen ? "v" : ">")} {section.Text} {section.CountText}",
                PullRequestRow pull => pull.Text,
                PullRequestMessageRow message => $"[{message.Kind}] {message.Text}",
                _ => rows[i].Text,
            });
        }

        return lines;
    }

    /// <summary>
    /// One empty tab with the three fakes of <paramref name="served"/> (<c>gitHub:</c>,
    /// <c>azureDevOps:</c>, <c>gitLab:</c>) and <paramref name="tokens"/> or a new store, the pointer
    /// at rest, once the start-up work has ended (finding git), as 7a's support does. Every part's
    /// empty-tab start goes through it.
    /// </summary>
    protected static async Task<ShellDriver> StartEmptyAsync(TempDirectory data, ServedHosting7b served, FakeTokenStore? tokens = null, int width = 1100, int height = 700)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(served);
        var app = ShellDriver.Start(data.Path, width, height, gitHub: served.GitHub, tokens: tokens, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        try
        {
            await app.WaitUntilReadyAsync();
        }
        catch
        {
            app.Dispose();
            throw;
        }

        app.MoveToNeutral();
        return app;
    }

    /// <summary>
    /// A copy of the scenario "hosting7b" served by the three fakes on their ports (47397 to 47399,
    /// <see cref="Phase7bExpectations.AzureDevOpsPort"/>, <see cref="Phase7bExpectations.GitLabPort"/>,
    /// <see cref="Phase7bExpectations.GitHubPort"/>), or a free port for one whose port is taken (D265;
    /// its clones' URLs then rewritten). Disposing it stops the fakes and deletes the copy.
    /// </summary>
    protected static async Task<ServedHosting7b> ServeAsync()
    {
        var copy = await Scenarios.Hosting7bAsync();
        try
        {
            return await Scenarios.ServeHosting7bAsync(copy, Phase7bExpectations.AzureDevOpsPort, Phase7bExpectations.GitLabPort, Phase7bExpectations.GitHubPort, ownsCopy: true);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The checks' default start (design 2.7): one tab on <paramref name="repository"/> of the served
    /// copy (<c>mobile-app</c>, <c>manifest</c> or <c>web-app</c>), the three fakes, the keychain
    /// <paramref name="tokens"/> or a new one, at the given size in the given theme; waits for the
    /// repository (<see cref="ShellDriver.WaitForRepositoryAsync"/>); signs in to each provider of
    /// <paramref name="signIn"/> with the scenario's token (Azure DevOps with its organization,
    /// Fernhill-Labs), by default the repository's provider (<see cref="ProviderOf"/>), none for
    /// <c>[]</c>; then waits until the watchers are quiet, a sign-in's URL-rewrites read included; and
    /// rests the pointer. Dispose the driver at the end of the check.
    /// </summary>
    protected static async Task<ShellDriver> OpenAsync(
        TempDirectory data,
        ServedHosting7b served,
        string repository = Phase7bExpectations.MobileApp,
        HostingProvider[]? signIn = null,
        FakeTokenStore? tokens = null,
        int width = 1100,
        int height = 700,
        AppTheme theme = AppTheme.Dark,
        bool systemDark = true)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(served);
        if (theme != AppTheme.Dark)
        {
            new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = theme });
        }

        var folder = repository == Phase7bExpectations.MobileApp ? served.MobileApp.Path : served.MobileApp.SiblingPath(repository);
        var app = ShellDriver.StartWithTabs(
            data.Path,
            [folder],
            width: width,
            height: height,
            systemDark: systemDark,
            gitHub: served.GitHub,
            tokens: tokens,
            azureDevOps: served.AzureDevOps,
            gitLab: served.GitLab);
        try
        {
            await app.WaitForRepositoryAsync();
            foreach (var provider in signIn ?? [ProviderOf(repository)])
            {
                var (token, organization) = provider switch
                {
                    HostingProvider.AzureDevOps => (served.AzureDevOpsToken, (string?)served.Organization),
                    HostingProvider.GitLab => (served.GitLabToken, null),
                    _ => (served.GitHubToken, null),
                };
                await app.SignInAsync(provider, token, organization, TestContext.Current.CancellationToken);
            }

            await app.WaitForWatchersQuietAsync();
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>The provider of a clone of the scenario's: Azure DevOps for <c>mobile-app</c> and <c>manifest</c>, GitLab for <c>web-app</c> (whose upstream is GitHub's).</summary>
    /// <exception cref="ArgumentException">The scenario has no such clone.</exception>
    protected static HostingProvider ProviderOf(string repository) => repository switch
    {
        Phase7bExpectations.MobileApp or Phase7bExpectations.Manifest => HostingProvider.AzureDevOps,
        Phase7bExpectations.WebApp => HostingProvider.GitLab,
        _ => throw new ArgumentException($"The scenario hosting7b has no clone {repository}.", nameof(repository)),
    };

    /// <summary>
    /// Saves a picture of the window as artifacts/visual/phase-7b/scripted/<paramref name="name"/>.png
    /// (the first picture of a run empties the folder, <see cref="VisualRun.Save"/>) and returns it
    /// for pixel assertions. The name is the report's, "&lt;check&gt;&lt;step&gt;-&lt;what&gt;", and is
    /// refused when a step of this run saved it already (<see cref="ClaimPictureName"/>, 7a design 11.8).
    /// Before the capture the pointer moves to the neutral point, as both harnesses do before a
    /// capture that is not about a hover, unless <paramref name="keepPointer"/> is true (a hover,
    /// a tooltip or a drag of a list's row), a menu is open or a branch drag runs. Before that it
    /// waits until the details panel is not reading a commit's details
    /// (<see cref="ShellDriver.WaitForCommitDetails"/>). The step's time is noted for design 0.6's
    /// budget (artifacts/visual/phase-7b/timings/).
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
        WaitForAuthorAvatar(app);
        if (!keepPointer && !app.IsMenuOpen && !IsDragging(app))
        {
            app.MoveToNeutral();
        }

        return app.Capture();
    }

    /// <summary>
    /// 7a's report's open point 44: while the active tab's details name an author's image
    /// (<c>Details.AuthorAvatarFile</c>) that <c>DetailsAuthorAvatar</c> does not show yet (it is
    /// decoded off the UI thread, design 11.3), waits for it. Nothing to wait for without a file, as
    /// always with GitHub off, or without the panel in the window.
    /// </summary>
    private static void WaitForAuthorAvatar(ShellDriver app)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            app.Settle();
            if (app.Session.Shell.ActiveTab.Repository?.Details is not { AuthorAvatarFile: not null }
                || app.Window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().FirstOrDefault(ellipse => ellipse.Name == "DetailsAuthorAvatar") is not { } avatar
                || avatar.IsVisible)
            {
                return;
            }

            if (clock.Elapsed >= ShellDriver.DefaultWaitTimeout)
            {
                throw new TimeoutException("Timed out waiting for the details panel to show the author's avatar.");
            }

            Thread.Sleep(10);
        }
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
    /// The git calls the app made after <paramref name="mark"/> (a <see cref="GitCallRecord.Sequence"/>
    /// read before the input, such as WP1's <c>QuietCallMarkAsync</c>'s), as their command lines.
    /// Presence and order only: a count goes through <c>GitCallBudget</c> (7a design 5.3, ruling 28).
    /// </summary>
    protected static List<string> CallsSince(ShellDriver app, long mark)
    {
        ArgumentNullException.ThrowIfNull(app);
        return [.. app.GitCalls().Where(call => call.Sequence > mark).Select(call => call.CommandText)];
    }

    /// <summary>Asserts that the app made the call <paramref name="command"/> after <paramref name="mark"/>, and returns its (last) record. Presence only.</summary>
    protected static GitCallRecord AssertCalled(ShellDriver app, long mark, string command)
    {
        ArgumentNullException.ThrowIfNull(app);
        var call = app.GitCalls().Where(record => record.Sequence > mark).LastOrDefault(record => record.CommandText == command);
        Assert.True(call is not null, $"The app did not run \"{command}\". It ran:\n" + string.Join('\n', CallsSince(app, mark)));
        return call!;
    }

    /// <summary>Asserts the order of calls the app made after <paramref name="mark"/>: these commands, each after the one before. Order only.</summary>
    protected static void AssertCalledInOrder(ShellDriver app, long mark, params string[] commands)
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

    /// <summary>The active theme's colours (<see cref="Phase7bExpectations.Dark"/>, <see cref="Phase7bExpectations.Light"/>).</summary>
    protected static Phase7bExpectations.Palette PaletteOf(ShellDriver app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Window.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light ? Phase7bExpectations.Light : Phase7bExpectations.Dark;
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
    /// steps saving one name fail the run instead of one overwriting the other (7a design 11.8).
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
                throw new InvalidOperationException($"The picture \"{name}\" was saved already in this run: two steps must not save one name (7a design 11.8).");
            }
        }
    }

    /// <summary>Notes a step's time in artifacts/visual/phase-7b/timings/&lt;test&gt;.txt; the first note of a test in a run starts its file afresh.</summary>
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
    /// The error dialog on top (7a's first error-dialog assertion, 7a design 16.3; 7a's A9 step 5): its title,
    /// summary and "In …" line; git's cleaned output holding <paramref name="keyLine"/> and ending with
    /// <paramref name="lastLine"/>; the command; the buttons left to right (<see cref="ButtonsOf"/>).
    /// </summary>
    protected static void AssertErrorDialog(
        ShellDriver app,
        ErrorDialogViewModel dialog,
        string title,
        string summary,
        string repository,
        string keyLine,
        string lastLine,
        string command,
        string[] buttons)
    {
        var view = app.TopDialogView!;
        Assert.Equal(title, ((TextBlock)app.FindByAutomationId(view, "ErrorTitle")).Text);
        Assert.Equal(summary, ((TextBlock)app.FindByAutomationId(view, "ErrorSummary")).Text);
        Assert.Equal("In " + repository, ((TextBlock)app.FindByAutomationId(view, "ErrorRepository")).Text);
        var output = dialog.Output ?? string.Empty;
        Assert.Contains(keyLine, output, StringComparison.Ordinal);
        Assert.EndsWith(lastLine, output.TrimEnd(), StringComparison.Ordinal);
        Assert.Equal(command, dialog.Command);
        Assert.Equal(buttons, ButtonsOf(app, view));
    }

    /// <summary>The visible buttons of a dialog's view, left to right, as their texts.</summary>
    protected static List<string> ButtonsOf(ShellDriver app, Visual view) =>
        [.. view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && button.Bounds.Height > 0
                && AutomationProperties.GetAutomationId(button) is "CancelButton" or "ConfirmButton" or "ErrorCopyButton" or "ErrorExtraButton")
            .OrderBy(button => app.BoundsOf(button).X)
            .Select(button => AutomationProperties.GetName(button) ?? button.Content as string ?? string.Empty)];

    /// <summary>
    /// A picture's name (design 0.3): the check (its part's letter, as the report numbers phase 7b's
    /// checks: a for accounts and sign-in, b for browse and clone, p for requests in the left panel
    /// and the graph, n for a new request; and its number; or the number alone), the step (up to
    /// three letters or digits), then words joined by hyphens that name the provider:
    /// <c>a03a-azure-dialog</c>, <c>p02b-gitlab-rows</c>. The letters C, O, Q, R and T are no part's,
    /// so that a name never reads as a requirement's number.
    /// </summary>
    [GeneratedRegex(@"^[abpn]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    protected static partial Regex PictureName();
}
