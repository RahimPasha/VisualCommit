using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The real diffs of the changes and CRLF scenarios (phase 2's test report, "Scenario repos"),
/// read with real git once per test process: the diff view tests only read them.
/// </summary>
internal static class DiffScenario
{
    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

    private static readonly Lazy<Task<ScenarioDiffs>> Loaded = new(LoadAsync);

    public static Task<ScenarioDiffs> DiffsAsync() => Loaded.Value;

    /// <summary>Parses a diff written out in a test, as git would print it.</summary>
    public static FileDiff Parse(params string[] lines) => DiffParser.Parse(string.Join("\n", lines) + "\n");

    private static async Task<ScenarioDiffs> LoadAsync()
    {
        using var repo = await Scenarios.ChangesAsync().ConfigureAwait(false);
        var repository = await GitRepository.OpenAsync(Runner, repo.Path).ConfigureAwait(false);

        Task<FileDiff> Read(DiffSide side, string path, FileChangeKind kind, string? oldPath = null, bool untracked = false) =>
            repository.ReadDiffAsync(new DiffTarget(side, path, kind, oldPath, untracked));

        var calculator = await Read(DiffSide.Unstaged, "src/Calculator.cs", FileChangeKind.Modified).ConfigureAwait(false);
        var guide = await Read(DiffSide.Unstaged, "docs/guide.md", FileChangeKind.Added, untracked: true).ConfigureAwait(false);
        var oldNotes = await Read(DiffSide.Unstaged, "docs/old-notes.txt", FileChangeKind.Deleted).ConfigureAwait(false);
        var helpers = await Read(DiffSide.Staged, "src/helpers.py", FileChangeKind.Renamed, "src/util.py").ConfigureAwait(false);
        var readmeUnstaged = await Read(DiffSide.Unstaged, "README.md", FileChangeKind.Modified).ConfigureAwait(false);
        var readmeStaged = await Read(DiffSide.Staged, "README.md", FileChangeKind.Modified).ConfigureAwait(false);
        var large = await Read(DiffSide.Unstaged, "data/large.txt", FileChangeKind.Modified).ConfigureAwait(false);

        using var crlf = await Scenarios.CrlfAsync().ConfigureAwait(false);
        var crlfRepository = await GitRepository.OpenAsync(Runner, crlf.Path).ConfigureAwait(false);
        var notes = await crlfRepository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "notes.txt", FileChangeKind.Modified)).ConfigureAwait(false);

        return new ScenarioDiffs(calculator, guide, oldNotes, helpers, readmeUnstaged, readmeStaged, large, notes);
    }
}

/// <summary>The scenario diffs the diff view tests use.</summary>
internal sealed record ScenarioDiffs(
    FileDiff Calculator,
    FileDiff Guide,
    FileDiff OldNotes,
    FileDiff Helpers,
    FileDiff ReadmeUnstaged,
    FileDiff ReadmeStaged,
    FileDiff Large,
    FileDiff CrlfNotes);

/// <summary>The colours of phase 2's report and the token table, per theme.</summary>
internal sealed record DiffColors(
    ThemeVariant Variant,
    Color Background,
    Color Added,
    Color AddedWord,
    Color Removed,
    Color RemovedWord,
    Color Hunk,
    Color Filler,
    Color TextPrimary,
    Color TextSecondary,
    Color Selection,
    Color Border)
{
    public static readonly DiffColors Dark = new(
        ThemeVariant.Dark,
        Color.Parse("#14161B"),
        Color.Parse("#1A2E24"),
        Color.Parse("#2B5A3F"),
        Color.Parse("#331D23"),
        Color.Parse("#6A2D37"),
        Color.Parse("#1C2130"),
        Color.Parse("#181A20"),
        Color.Parse("#E4E7EC"),
        Color.Parse("#9BA3B0"),
        Color.Parse("#2A3150"),
        Color.Parse("#323845"));

    public static readonly DiffColors Light = new(
        ThemeVariant.Light,
        Color.Parse("#FFFFFF"),
        Color.Parse("#E6F6EC"),
        Color.Parse("#B4E5C6"),
        Color.Parse("#FBE9EB"),
        Color.Parse("#F3BAC1"),
        Color.Parse("#EEF0FB"),
        Color.Parse("#F3F4F6"),
        Color.Parse("#1B1F27"),
        Color.Parse("#5C6572"),
        Color.Parse("#DDE1FA"),
        Color.Parse("#D2D6DE"));

    public static DiffColors For(string name) => name == "Light" ? Light : Dark;

    public static void AssertColor(Color expected, Color actual, string where, int tolerance = 3)
    {
        var close = Math.Abs(expected.R - actual.R) <= tolerance
            && Math.Abs(expected.G - actual.G) <= tolerance
            && Math.Abs(expected.B - actual.B) <= tolerance;
        Assert.True(close, $"{where}: expected {expected}, found {actual}.");
    }

    /// <summary>Asserts that a brush is a solid colour brush of the expected colour.</summary>
    public static void AssertBrush(Color expected, IBrush? brush, string where)
    {
        var solid = Assert.IsAssignableFrom<ISolidColorBrush>(brush);
        Assert.True(solid.Color == expected, $"{where}: expected {expected}, found {solid.Color}.");
    }
}

/// <summary>A window that shows one control, at a size and in a theme of its own, and drives it with simulated input.</summary>
internal sealed class DiffHost : IDisposable
{
    private DiffHost(Window window)
    {
        Window = window;
    }

    public Window Window { get; }

    public static DiffHost Show(Control content, double width, double height, ThemeVariant theme)
    {
        // The theme is set on the window, not the application: other tests on the same UI
        // thread may change the application's theme while this one waits.
        var window = new Window { Width = width, Height = height, Content = content, RequestedThemeVariant = theme };
        window.Show();
        Settle();
        return new DiffHost(window);
    }

    /// <summary>Runs the UI thread's jobs and the layout until nothing is left to do.</summary>
    public static void Settle()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    public Screenshot Capture()
    {
        Settle();
        var frame = Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The window has not rendered a frame.");
        return new Screenshot(frame);
    }

    public Point ToWindow(Visual visual, Point point) =>
        visual.TranslatePoint(point, Window) ?? throw new InvalidOperationException("The control is not in the window.");

    public void Click(Visual visual, Point point, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var position = ToWindow(visual, point);
        Window.MouseMove(position, modifiers);
        Window.MouseDown(position, MouseButton.Left, modifiers);
        Window.MouseUp(position, MouseButton.Left, modifiers);
        Settle();
    }

    public void Drag(Visual visual, Point from, Point to)
    {
        var start = ToWindow(visual, from);
        var end = ToWindow(visual, to);
        Window.MouseMove(start);
        Window.MouseDown(start, MouseButton.Left);
        Window.MouseMove(new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2), RawInputModifiers.LeftMouseButton);
        Window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        Window.MouseUp(end, MouseButton.Left);
        Settle();
    }

    public void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Settle();
    }

    public void Dispose() => Window.Close();
}
