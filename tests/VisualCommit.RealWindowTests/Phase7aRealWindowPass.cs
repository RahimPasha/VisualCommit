// What is where in phase 7a's real-window pass (docs/test-reports/phase-7a.md: "Conventions of every
// part", "The real window" and the R steps in the Notes of A1, A2, A5, B3, B6, B7, P6, P11, P15, G1 and
// N1; docs/phase-7a-design.md 13, 16 and 17; D319 to D322):
// - Phase7aRealWindowPass.cs (this file): the run (Phase7aRun: one folder of pictures and one run.txt
//   for the whole pass), the order of its methods (Phase7aOrder, D319's: A, B, N, G, P), launching the
//   real app on the scenario "hosting" served by the fake of GitHub (one empty tab, or one tab on
//   hello), the waits on the fake's records and on D322's two log lines, comparing a capture with the
//   scripted picture of the same name, the menus, accounts.json, and the clean end every launch makes.
// - Phase7aRealWindowPass.A.cs, .B.cs, .N.cs, .G.cs and .P.cs: one method and one launch per part,
//   in the same partial class; .P.cs holds OpensTheBrowser (13.5, D321).
// - RealApp.Phase7a.cs (the hosting environment, the askpass helper's path, the Accounts section and
//   the sign-in dialog, the repository browser's and the left panel's rows) and RealApp.Phase6.Settings.cs
//   (the settings screen, its section list with Accounts), with the earlier phases' RealApp files.
// - The expectations of the scripted walk-through (VisualTests.Phase7a/Phase7aExpectations*.cs) are
//   compiled into this project too (see the .csproj), so both harnesses use the same constants, texts,
//   picture names and PrepareAsync calls.
//
// Conventions for the part files (phase 6's, with design 13.2's):
// - One method per part, one launch each, at 1100×700, dark (D319). Declare the scenario and the fake
//   first, then the app, so the app is stopped first when a step fails: `using var hello = await
//   Scenarios.HostingAsync();`, `await using var served = await Scenarios.ServeHostingAsync(hello);`,
//   the part's PrepareAsync where its twin calls one (G and N), then `using var app = LaunchServed(…)`.
// - Every launch signs in by typing the fake's OctoToken into the real "Add GitHub account" dialog
//   (D320): B through the palette's "Clone from GitHub…", the others through Settings, Accounts. The
//   app's keychain is the memory store (VISUALCOMMIT_KEYCHAIN=memory); nothing is seeded.
// - After an input, wait for a named condition (a log line, a UI Automation element or text, a request
//   in the fake's log), each with one of the pass's limits; never for a fixed time. A picture of a
//   signed-in hello waits for D322's two lines since its mark (WaitForHostingSettled).
// - Pictures: Compare(app, PartX's constant) with the scripted picture of the same name, after checking
//   that no name on screen holds a token; the pointer at the neutral point unless a menu is open
//   (keepPointer). At most 3 % differ (D33, D58). The 7a walk-through's pictures must exist: run
//   `dotnet test --project tests/VisualCommit.VisualTests.Phase7a` first.
// - No token is ever passed to an Assert method or put in a message: texts that could hold one are
//   tested with string.Contains and a failure names the place. Never click "Copy link", "Copy …",
//   "Open file", "Open in editor", "Show in folder" or the status bar's "Open in browser"; never change
//   a desktop setting (D224). The one browser opened is part P's (13.5, D321).
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using VisualCommit.VisualTests.Phase7a;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.RealWindowTests;

/// <summary>One run of phase 7a's pass, shared by its tests: one folder of screenshots and one run.txt.</summary>
public sealed class Phase7aRun : IDisposable
{
    public RealWindowRun Run { get; } = new(phase: Phase7aExpectations.Phase);

    /// <summary>Whether the display has been noted in run.txt yet (once, with the first launch).</summary>
    internal bool DisplayNoted { get; set; }

    public void Dispose() => Run.Dispose();
}

/// <summary>
/// Runs phase 7a's real-window methods in D319's order: A, B, N, G, P (five methods, five launches);
/// P last, so that the browser step is the pass's last input (design 13.2). A method the list does not
/// name runs after them, by name.
/// </summary>
public sealed class Phase7aOrder : ITestCaseOrderer
{
    /// <summary>The methods in their order.</summary>
    public static IReadOnlyList<string> Methods { get; } =
    [
        nameof(Phase7aRealWindowPass.Check_A_accounts),
        nameof(Phase7aRealWindowPass.Check_B_browse_and_clone),
        nameof(Phase7aRealWindowPass.Check_N_new_pull_request),
        nameof(Phase7aRealWindowPass.Check_G_badge_and_avatars),
        nameof(Phase7aRealWindowPass.Check_P_pull_requests_and_browser),
    ];

    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
        where TTestCase : notnull, ITestCase
    {
        ArgumentNullException.ThrowIfNull(testCases);
        return [.. testCases.OrderBy(test => Rank(test.TestMethodName)).ThenBy(test => test.TestMethodName, StringComparer.Ordinal)];
    }

    private static int Rank(string? method) => method is not null && Methods.Contains(method) ? Methods.ToList().IndexOf(method) : int.MaxValue;
}

/// <summary>
/// The real-window pass of phase 7a: the steps of docs/test-reports/phase-7a.md marked "R", repeated
/// in the real app on the Windows desktop with the real mouse and keyboard, against the fake of GitHub
/// on 127.0.0.1 (design 13). Screenshots go to artifacts/visual/phase-7a/real-window/ and are compared
/// with the scripted walk-through's of the same name (D33, D58), which must have run first. The parts'
/// checks are in Phase7aRealWindowPass.A.cs, .B.cs, .N.cs, .G.cs and .P.cs.
/// </summary>
[TestCaseOrderer(typeof(Phase7aOrder))]
public partial class Phase7aRealWindowPass(Phase7aRun fixture) : IClassFixture<Phase7aRun>
{
    /// <summary>How long the app may take to open a scenario: its history read and drawn, its status shown (phase 6's).</summary>
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long an operation may take to end (its log line), phase 6's.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long a change of the window that follows an input may take to show: the scripted run's default wait.</summary>
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long a network step may take: a sign-in, a list, a clone, the browser's request (13.5).</summary>
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How often <see cref="WaitForFake"/> reads the fake's records.</summary>
    private static readonly TimeSpan FakePollInterval = TimeSpan.FromMilliseconds(100);

    private RealWindowRun Run => fixture.Run;

    // ---- Launches ----

    /// <summary>
    /// Starts the real app on a new data folder with one tab, empty (<paramref name="emptyTab"/>) or on
    /// <paramref name="served"/>'s <c>hello</c>, with the hosting environment of the fake's port
    /// (<see cref="RealApp.HostingEnvironment"/>) at 1100×700, and waits for WP-S's start line
    /// <c>GitHub: 127.0.0.1:&lt;port&gt;.</c>, then for the welcome page (git found, <c>CloneRepoButton</c>
    /// shown, as the scripted <c>StartEmptyAsync</c> waits for the start-up work, design 17 item 2) or
    /// for <c>hello</c> to have opened (<see cref="Opened"/>). Notes the port in run.txt when 47395 was
    /// taken (<see cref="NotePort"/>). Disposes the app when that fails.
    /// </summary>
    private RealApp LaunchServed(ServedHosting served, bool emptyTab)
    {
        ArgumentNullException.ThrowIfNull(served);
        NotePort(served.GitHub);
        var port = served.GitHub.Port;
        var data = Run.NewDataDirectory();
        var tab = emptyTab ? new TabState(null) : new TabState(served.Hello.Path);
        var app = Sized(RealApp.Launch(data, new SessionState { Tabs = [tab], ActiveTab = 0 }, RealApp.HostingEnvironment(port)));
        try
        {
            app.WaitForLogLine(AppLogFile.Containing(string.Create(CultureInfo.InvariantCulture, $"GitHub: 127.0.0.1:{port}.")), LoadTimeout);
            if (!emptyTab)
            {
                return Opened(app, Hello, loadsBefore: 0, readsBefore: 0);
            }

            app.WaitForGitFound(LoadTimeout);
            app.WaitFor(() => app.ShownElements("CloneRepoButton").Count == 1, "the welcome page's Clone repository button", LoadTimeout);
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>Sizes the window to 1100×700 and notes the display once. Disposes the app when that fails. As phase 6's (Phase6RealWindowPass.cs).</summary>
    private RealApp Sized(RealApp app)
    {
        try
        {
            app.SetClientSize(DefaultSize.Width, DefaultSize.Height);
            if (!fixture.DisplayNoted)
            {
                Run.NoteDisplay(app);
                fixture.DisplayNoted = true;
            }

            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Waits until <paramref name="repository"/> has opened: its history loaded and its status shown,
    /// the log lines the app writes for both, after the <paramref name="loadsBefore"/> and
    /// <paramref name="readsBefore"/> the log held before. Disposes the app when that fails. As phase 6's
    /// (Phase6RealWindowPass.cs).
    /// </summary>
    private static RealApp Opened(RealApp app, string repository, int loadsBefore, int readsBefore)
    {
        try
        {
            app.WaitForLogLine(Loaded(repository), LoadTimeout, loadsBefore + 1);
            app.WaitForLogLine(AppLogFile.WorkingTreeRead(repository), LoadTimeout, readsBefore + 1);
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>The line a repository logs when its graph has loaded: <c>&lt;repo&gt;: loaded N commits in M ms</c>.</summary>
    private static Regex Loaded(string repository) => AppLogFile.Containing($"] {repository}: loaded ");

    /// <summary>Notes in run.txt when the fake of GitHub could not have port 47395: its URLs then show another port (D265), which <see cref="WithPort"/> gives the texts.</summary>
    private void NotePort(FakeGitHub gitHub)
    {
        if (gitHub.PortWasTaken)
        {
            Run.Note(string.Create(CultureInfo.InvariantCulture, $"Port {ServerPort} was taken: the fake of GitHub ran on {gitHub.Port} (D265)."));
        }
    }

    // ---- The fake's records and the log ----

    /// <summary>
    /// Waits until the fake has recorded, after its first <paramref name="mark"/> requests
    /// (<c>Requests.Count</c> read before the step), a request that <paramref name="match"/> accepts,
    /// and returns it. The fake records a request after its answer, so a step that the app has
    /// already shown may still lack its record: this read ends that race (design 13.3). Reads the
    /// records every 100 ms, at most <paramref name="timeout"/>.
    /// </summary>
    private static FakeGitHubRequest WaitForFake(FakeGitHub gitHub, int mark, Func<FakeGitHubRequest, bool> match, string what, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(gitHub);
        ArgumentNullException.ThrowIfNull(match);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            if (gitHub.Requests.Skip(mark).FirstOrDefault(match) is { } found)
            {
                return found;
            }

            if (clock.Elapsed >= timeout)
            {
                throw new TimeoutException(
                    $"Timed out after {timeout.TotalSeconds:0} s waiting for the fake's record of {what}. Since the mark it recorded: "
                    + string.Join(" | ", gitHub.Requests.Skip(mark).Select(request => $"{request.Method} {request.Path} {request.Route} {request.Status}")));
            }

            Thread.Sleep(FakePollInterval);
        }
    }

    /// <summary>
    /// D322: waits for <paramref name="repository"/>'s <c>listed {n} open pull requests of {owner/name}</c>
    /// (<see cref="AppLogFile.PullRequestsListed"/>) and <c>showing GitHub avatars for {n} authors</c>
    /// (<see cref="AppLogFile.AvatarsShown"/>) since <paramref name="logMark"/>, both written on the UI
    /// thread once their state is applied: the state of a picture of a signed-in GitHub repository
    /// (design 13.3). Only the shown tab writes the avatars' line (design 17 item 11).
    /// </summary>
    private static void WaitForHostingSettled(RealApp app, int logMark, string repository = Hello)
    {
        var listed = AppLogFile.PullRequestsListed(repository);
        var shown = AppLogFile.AvatarsShown(repository);
        WaitTelling(
            app,
            () => app.CountLogLinesSince(listed, logMark) > 0 && app.CountLogLinesSince(shown, logMark) > 0,
            $"{repository}'s pull requests listed and its avatars shown since the mark (D322)",
            () => $"Since the mark the log has {app.CountLogLinesSince(listed, logMark)} \"listed\" and {app.CountLogLinesSince(shown, logMark)} \"showing GitHub avatars\" lines of {repository}.",
            NetworkTimeout);
    }

    /// <summary>Waits for <paramref name="condition"/> as <see cref="RealApp.WaitFor"/> does, at most <paramref name="timeout"/> (<see cref="StepTimeout"/> when null); on a timeout the message adds what <paramref name="state"/> reads then. As phase 6's.</summary>
    private static void WaitTelling(RealApp app, Func<bool> condition, string what, Func<string> state, TimeSpan? timeout = null)
    {
        try
        {
            app.WaitFor(condition, what, timeout ?? StepTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} {state()}", ex);
        }
    }

    /// <summary>
    /// The fake's API and page requests since <paramref name="mark"/> (<c>Requests.Count</c> read before
    /// the step), the avatars' left out: a step that "asks GitHub nothing" is read on these, since an
    /// avatar run of the sign-in may be recorded after D322's line (the fake records after its answer).
    /// </summary>
    private static List<FakeGitHubRequest> ApiRequestsSince(ServedHosting served, int mark) =>
        [.. served.GitHub.Requests.Skip(mark).Where(request => !(request.Route?.StartsWith("avatar:", StringComparison.Ordinal) ?? false))];

    // ---- Tokens ----

    /// <summary>The scenario's three tokens with their constants' names, for a failure's message (design 2.6).</summary>
    private static IReadOnlyList<(string Name, string Token)> TokensOf(ServedHosting served) =>
        [(nameof(OctoToken), served.OctoToken), (nameof(OctoClassicToken), served.OctoClassicToken), (nameof(AdaToken), served.AdaToken)];

    /// <summary>Fails, naming <paramref name="place"/> and the token's constant but never the token, when <paramref name="text"/> holds one of the scenario's tokens.</summary>
    private static void AssertNoToken(ServedHosting served, string? text, string place)
    {
        foreach (var (name, token) in TokensOf(served))
        {
            Assert.False(text?.Contains(token, StringComparison.Ordinal) ?? false, $"{place} holds {name}.");
        }
    }

    /// <summary>
    /// No element of the window is named with a token (the names UI Automation gives every element,
    /// the text elements' texts among them): the twins' "no token on screen" (design 13.2). The value
    /// of the masked box is <see cref="RealApp.SecretBoxValue"/>'s, read where a token was typed.
    /// </summary>
    private static void AssertNoTokenOnScreen(RealApp app, ServedHosting served)
    {
        foreach (var element in app.Window.FindAllDescendants())
        {
            string? name;
            string? id;
            try
            {
                name = element.Properties.Name.ValueOrDefault;
                id = element.Properties.AutomationId.ValueOrDefault;
            }
            catch (Exception ex) when (ex is FlaUI.Core.Exceptions.ElementNotAvailableException or System.Runtime.InteropServices.COMException)
            {
                // Gone since the walk listed it (a tooltip, a closing popup): it shows nothing.
                continue;
            }

            AssertNoToken(served, name, $"The name of the element {id}");
        }
    }

    // ---- Pictures ----

    /// <summary>
    /// Asserts that no element of the window is named with a token (<see cref="AssertNoTokenOnScreen"/>),
    /// then captures the client area, saves it as <c>&lt;name&gt;.png</c> and asserts that it matches the
    /// scripted picture of the same name within <see cref="RealWindowRun.MaxDifference"/> (D33, D58);
    /// run.txt gets a line with the difference and <c>&lt;name&gt;-difference.png</c> shows it. The
    /// pointer moves to the neutral point first unless <paramref name="keepPointer"/> is true: a capture
    /// with a menu open. As phase 6's (Phase6RealWindowPass.cs), with the token check.
    /// </summary>
    private void Compare(RealApp app, ServedHosting served, string name, bool keepPointer = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        AssertNoTokenOnScreen(app, served);
        if (!keepPointer)
        {
            app.MoveToNeutral();
        }

        using var capture = app.CaptureClient();
        Run.Save(capture, name);
        Run.AssertMatchesScripted(capture, name, name);
    }

    // ---- Menus (phase 3's conventions; as phase 6's, Phase6RealWindowPass.cs) ----

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
    /// items' labels in order, each disabled item with its reason as its help text, every other item
    /// enabled; the height the report gives (±2); and that the menu lies at least 4 px inside the
    /// window. As phase 6's.
    /// </summary>
    private static void AssertMenu(RealApp app, IReadOnlyList<string> lines, double height)
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
                Assert.Equal(reason, RealApp.HelpTextOf(item));
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
    }

    /// <summary>Esc closes the open menu; waits until no menu is in the window. As phase 6's.</summary>
    private static void CloseMenuWithEscape(RealApp app)
    {
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitFor(() => app.FindAll("ContextMenu").Count == 0, "the menu to close", RealApp.DefaultWaitTimeout);
    }

    // ---- accounts.json (design 7.2) ----

    /// <summary>The one record of <c>accounts.json</c> as the file holds it, and whether the file's text holds one of the scenario's tokens.</summary>
    private sealed record SavedAccount(int Version, string? Provider, string? Host, string? Login, bool UseForGit, string? TokenState, bool HoldsAToken);

    /// <summary>
    /// <c>accounts.json</c> in <paramref name="data"/> with its one record, or null while it is missing,
    /// being written or holds no single record. The file is written off the UI thread (7.2), so a check
    /// polls it.
    /// </summary>
    private static SavedAccount? ReadSavedAccount(string data, ServedHosting served)
    {
        try
        {
            string text;
            using (var stream = new FileStream(new AppPaths(data).AccountsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            using var json = JsonDocument.Parse(text);
            var root = json.RootElement;
            if (root.GetProperty("accounts") is not { ValueKind: JsonValueKind.Array } accounts || accounts.GetArrayLength() != 1)
            {
                return null;
            }

            var record = accounts[0];
            return new SavedAccount(
                root.GetProperty("version").GetInt32(),
                record.GetProperty("provider").GetString(),
                record.GetProperty("host").GetString(),
                record.GetProperty("login").GetString(),
                record.GetProperty("useForGit").GetBoolean(),
                record.GetProperty("tokenState").GetString(),
                TokensOf(served).Any(entry => text.Contains(entry.Token, StringComparison.Ordinal)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    // ---- The end of every launch ----

    /// <summary>
    /// What every launch ends with (design 13.3, the report's "The real window"): the app closed with
    /// exit code 0 (closed here unless the method closed it already); then no token of the scenario in
    /// any file of the data folder (the app's log among them), of the copy's <c>hello/</c> and
    /// <c>tool/</c> and of <paramref name="folders"/> (<see cref="TokenTripwire"/>; the real process's git
    /// calls are not recorded, so no call log); the fake's <c>Errors</c> empty; no API request answered
    /// 400 or 415; no <c>avatar:</c> request with a token.
    /// </summary>
    private static void AssertCleanEnd(RealApp app, ServedHosting served, params string[] folders)
    {
        Assert.Equal(0, app.ExitCode ?? app.Close());
        string[] searched = [app.DataDirectory, served.Hello.Path, served.Tool.Path, .. folders];
        foreach (var (_, token) in TokensOf(served))
        {
            TokenTripwire.AssertAbsent(token, searched, calls: null);
        }

        Assert.Empty(served.GitHub.Errors);
        Assert.DoesNotContain(served.GitHub.Requests, request => request.Status is 400 or 415);
        Assert.DoesNotContain(served.GitHub.Requests, request => (request.Route?.StartsWith("avatar:", StringComparison.Ordinal) ?? false) && request.TokenPresent);
    }

    [GeneratedRegex(@" \((danger|disabled: "".*""|disabled)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex MenuMark();

    [GeneratedRegex(@"^(?<label>.*) \(disabled: ""(?<reason>.*)""\)$", RegexOptions.CultureInvariant)]
    private static partial Regex DisabledMark();
}
