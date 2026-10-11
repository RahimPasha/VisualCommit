// What is where in phase 3's real-window pass (docs/test-reports/phase-3.md: each part's "Real
// window" column and "The real-window pass for these checks"; design 7.7 for the launches):
// - Phase3RealWindowPass.cs (this file): the run (Phase3Run: one folder of pictures and one run.txt
//   for the whole pass), launching the real app on a scenario, the server scenarios, waiting for an
//   operation's log line, and comparing a capture with the scripted picture of the same step.
// - Phase3RealWindowPass.A.cs to .D.cs: each part's real-window checks, in the same partial class.
// - RealApp.Phase3.cs (design 7.4: modifier clicks, drags, menus, log lines) and RealApp.Checks.cs
//   (the neutral point, resting the pointer until a tooltip shows, replacing a box's text, rows
//   below the banner, left-panel rows by path, the status bar's progress, the close button).
// - The expectations of the scripted walk-through (VisualTests/Phase3/Phase3Expectations*.cs) are
//   compiled into this project too (see the .csproj), so both harnesses use the same constants and
//   texts; WindowLayout and AppLogFile (VisualCommit.Testing) are shared the same way.
//
// Conventions for the part files:
// - One test method per launch. Checks that share a launch (design 7.7 and the report: 1, 5, 7 and
//   23 on one ops copy, check 7's merge last; 13 and 16; 17 and 21 on one server; 28 and 29 step 1)
//   are one method, named after them (Checks_23_1_5_and_7_on_one_launch), with their steps in the
//   report's order; every step before the last leaves git's state as it found it and ends with no
//   menu or dialog open. Every other check has a launch, and a method, of its own (31: two).
// - Declare the scenario (and its server) first, then the app: `using var app = Launch(...)`. When
//   a step fails, the app is disposed first, which stops it if it is still running and releases a
//   mouse button a drag left held (RealApp.Dispose), and then the server and the repository go.
//   No step may leave a window on the desktop.
// - Pictures: Compare(app, "<name>") with the scripted picture's name; it rests the pointer at the
//   neutral point first unless a menu is open, a drag is under way or the capture is about a hover
//   (keepPointer: true), and asserts at most 3% differing pixels (D33, D58). The scripted pictures
//   must exist: run `dotnet test` at the repo root before the pass.
// - After an operation, wait for its log line (ExpectOperation before the input, then Wait), never
//   for a fixed time. Never click "Copy …", "Open file" or "Show in folder" (design 7.5).
// - Graph rows: RowPoint / RealApp.GraphRowPoint (the row constants, below the banner when one is
//   shown); left-panel rows: RealApp.RefRow(section, …, name); a label's centre, which only the
//   scripted run can measure: the constant the part's expectations hold for it.
using System.Drawing;
using System.Text.RegularExpressions;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.RealWindowTests;

/// <summary>One run of phase 3's pass, shared by its tests: one folder of screenshots and one run.txt.</summary>
public sealed class Phase3Run : IDisposable
{
    public RealWindowRun Run { get; } = new(phase: 3);

    /// <summary>Whether the display has been noted in run.txt yet (once, with the first launch).</summary>
    internal bool DisplayNoted { get; set; }

    public void Dispose() => Run.Dispose();
}

/// <summary>
/// The real-window pass of phase 3: the checks of docs/test-reports/phase-3.md marked for it,
/// repeated in the real app on the Windows desktop with the real mouse and keyboard. Screenshots
/// go to artifacts/visual/phase-3/real-window/ and are compared with the scripted walk-through's
/// of the same step (D33, D58), which must have run first (<c>dotnet test</c> at the repo root).
/// The parts' checks are in Phase3RealWindowPass.A.cs to .D.cs.
/// </summary>
public partial class Phase3RealWindowPass(Phase3Run fixture) : IClassFixture<Phase3Run>
{
    /// <summary>How long the app may take to open a scenario: its history read and drawn, its status shown.</summary>
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long an operation may take to end (its log line), network ones included.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

    private RealWindowRun Run => fixture.Run;

    /// <summary>
    /// Starts the real app with one tab on <paramref name="repo"/> in a window whose client area is
    /// <paramref name="width"/> × <paramref name="height"/>, and waits until the repository has
    /// opened: its history loaded and its status shown (the log lines the app writes for both).
    /// <paramref name="environment"/> adds variables to the app's environment
    /// (<see cref="RealApp.Launch(string, SessionState, IReadOnlyDictionary{string, string?}?)"/>).
    /// The first launch of the pass notes the display in run.txt.
    /// </summary>
    private RealApp Launch(TempRepo repo, int width = 1100, int height = 700, IReadOnlyDictionary<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var data = Run.NewDataDirectory();
        var app = RealApp.Launch(data, new SessionState { Tabs = [new TabState(repo.Path)] }, environment);
        try
        {
            app.SetClientSize(width, height);
            if (!fixture.DisplayNoted)
            {
                Run.NoteDisplay(app);
                fixture.DisplayNoted = true;
            }

            var name = Path.GetFileName(repo.Path);
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
    /// scripted picture of the same step (the same name unless <paramref name="scriptedName"/> says
    /// otherwise) within <see cref="RealWindowRun.MaxDifference"/> (D33, D58). The pointer moves to
    /// the neutral point first, as both harnesses do before a capture that is not about a hover,
    /// unless <paramref name="keepPointer"/> is true: a capture with a menu open (the pointer stays
    /// where it opened the menu), in the middle of a drag, or of a tooltip.
    /// </summary>
    private void Compare(RealApp app, string name, bool keepPointer = false, string? scriptedName = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!keepPointer)
        {
            app.MoveToNeutral();
        }

        using var capture = app.CaptureClient();
        Run.Save(capture, name);
        Run.AssertMatchesScripted(capture, name, scriptedName ?? name);
    }

    /// <summary>
    /// Notes how many times the operation <paramref name="title"/> of <paramref name="repository"/>
    /// has ended so far, before the input that starts it; <see cref="PendingOperation.Wait"/> then
    /// waits for its next end line (design 1.7, <c>&lt;repo&gt;: &lt;Title&gt;: &lt;outcome&gt; after
    /// &lt;N&gt; ms: …</c>): the real window's "wait until idle".
    /// </summary>
    private static PendingOperation ExpectOperation(RealApp app, string repository, string title)
    {
        ArgumentNullException.ThrowIfNull(app);
        var pattern = AppLogFile.OperationEnded(repository, title);
        return new PendingOperation(app, pattern, app.CountLogLines(pattern));
    }

    /// <summary>
    /// The auth scenario at the credential test server, as the scripted run's: the server on port
    /// 47391 (another when it is taken; run.txt then says so), asking for a sign-in unless
    /// <paramref name="requireAuth"/> is false. Declare it before the app, so that the app goes first.
    /// </summary>
    private async Task<RealServerScenario> AuthAtServerAsync(bool requireAuth = true)
    {
        var server = await AuthRemoteServer.StartForVisualChecksAsync();
        try
        {
            server.RequireAuth = requireAuth;
            NotePort(server);
            return new RealServerScenario(server, await Scenarios.AuthRemoteAsync(server));
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
    }

    /// <summary>The sync scenario with <c>origin</c> at the server, serving its own <c>origin.git</c> in place without a sign-in, as the scripted run's.</summary>
    private async Task<RealServerScenario> SyncAtServerAsync()
    {
        var repo = await Scenarios.RemoteSyncAsync();
        AuthRemoteServer? server = null;
        try
        {
            server = await AuthRemoteServer.StartForVisualChecksAsync(existingRepository: repo.SiblingPath("origin.git"));
            server.RequireAuth = false;
            NotePort(server);
            await repo.GitAsync("remote", "set-url", "origin", server.Url);
            return new RealServerScenario(server, repo);
        }
        catch
        {
            repo.Dispose();
            if (server is not null)
            {
                await server.DisposeAsync();
            }

            throw;
        }
    }

    /// <summary>Notes in run.txt when the server could not have port 47391: its pictures then differ from the scripted ones in the port only (design 7.3).</summary>
    private void NotePort(AuthRemoteServer server)
    {
        if (server.PortWasTaken)
        {
            Run.Note($"Port {AuthRemoteServer.VisualChecksPort} was taken: the server ran on {server.Port}, so its pictures show that port.");
        }
    }

    /// <summary>The point of a graph row to click "in its Message column", from the row constants (D49), below the banner when one shows (D122).</summary>
    private static PointF RowPoint(RealApp app, int displayRow, int lanes) => app.GraphRowPoint(displayRow, lanes);

    /// <summary>An operation a step is about to start, and how many times it had ended before (<see cref="ExpectOperation"/>).</summary>
    private sealed record PendingOperation(RealApp App, Regex Pattern, int EndedBefore)
    {
        /// <summary>Waits for the operation's next end line and asserts its outcome. Returns the line.</summary>
        public string Wait(OperationOutcome outcome, TimeSpan? timeout = null)
        {
            var line = App.WaitForLogLine(Pattern, timeout ?? OperationTimeout, EndedBefore + 1);
            Assert.Contains($": {outcome} after ", line, StringComparison.Ordinal);
            return line;
        }
    }

    /// <summary>A scenario repository with its server (part D). Dispose it after the app: the repository, then the server.</summary>
    private sealed class RealServerScenario(AuthRemoteServer server, TempRepo repo) : IAsyncDisposable
    {
        public AuthRemoteServer Server { get; } = server;

        public TempRepo Repo { get; } = repo;

        public async ValueTask DisposeAsync()
        {
            Repo.Dispose();
            await Server.DisposeAsync();
        }
    }
}
