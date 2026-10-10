using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// One run of a phase's real-window pass: its screenshot folder, the comparison with the scripted
/// walk-through, and the notes that end up in <c>run.txt</c>. A pass creates one at its start
/// and disposes it at its end, whatever happened in between.
/// </summary>
public sealed class RealWindowRun : IDisposable
{
    /// <summary>
    /// The most a real-window screenshot may differ from the scripted one, as a share of all
    /// pixels (decisions D33 and D58: a pixel differs when nothing within a pixel of it matches).
    /// Text is drawn at the display's scaling in the real window, so the edges of letters land a
    /// pixel off; a wrong theme, a missing region or a shifted layout differ far more.
    /// </summary>
    public const double MaxDifference = 0.03;

    private readonly StringBuilder _notes = new();
    private readonly List<string> _dataDirectories = [];

    /// <summary>Starts the run of a phase's pass and empties its folder, so that every file in it comes from this run.</summary>
    public RealWindowRun(int phase)
    {
        Phase = phase;
        Folder = Screens.RealWindowFolder(phase);
        Directory.CreateDirectory(Folder);
        foreach (var old in Directory.EnumerateFiles(Folder))
        {
            File.Delete(old);
        }

        Note($"Date: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}");
        Note($"Windows: {RuntimeInformation.OSDescription}");
    }

    public int Phase { get; }

    /// <summary><c>artifacts/visual/phase-N/real-window/</c>.</summary>
    public string Folder { get; }

    /// <summary>Adds a line to <c>run.txt</c>.</summary>
    public void Note(string line) => _notes.AppendLine(line);

    /// <summary>Records the display the app runs on. Call once, with the first app instance.</summary>
    public void NoteDisplay(RealApp app)
    {
        Note($"Display scaling: {app.Scaling:P0}");
        Note($"Work area: {app.WorkAreaSize.Width:F0}x{app.WorkAreaSize.Height:F0} logical pixels");
    }

    /// <summary>Creates an empty temporary data folder for the app. It is deleted when the run ends.</summary>
    public string NewDataDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VisualCommit.Tests", "real-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(directory);
        _dataDirectories.Add(directory);
        return directory;
    }

    /// <summary>Saves a capture as <c>&lt;name&gt;.png</c> in the run's folder.</summary>
    public string Save(Bitmap capture, string name) => capture.Save(Phase, name);

    /// <summary>
    /// Asserts that a capture shows the same as the scripted screenshot of the same step: at
    /// most <see cref="MaxDifference"/> of the pixels differ. Notes the difference and saves a
    /// picture of the differing pixels as <c>&lt;name&gt;-difference.png</c>.
    /// </summary>
    public void AssertMatchesScripted(Bitmap capture, string name, string scriptedName)
    {
        var scripted = Screens.ScriptedFile(Phase, scriptedName);
        Assert.True(
            File.Exists(scripted),
            $"The scripted screenshot {scripted} is missing. Run the scripted walk-through first: dotnet test (at the repo root).");

        var (difference, inPlace) = capture.DifferenceFrom(scripted, Path.Combine(Folder, name + "-difference.png"));
        Note(string.Create(CultureInfo.InvariantCulture, $"{name} against scripted {scriptedName}: {difference:P2} of pixels differ ({inPlace:P2} compared in place only)"));
        Assert.True(
            difference <= MaxDifference,
            string.Create(CultureInfo.InvariantCulture, $"{name} differs from the scripted screenshot {scriptedName} in {difference:P2} of its pixels; at most {MaxDifference:P0} is allowed. See {name}-difference.png."));
    }

    /// <summary>Asserts that a colour captured from the screen is a given theme colour, give or take a shade.</summary>
    public static void AssertColour(Color expected, Color actual, string what)
    {
        var close = Math.Abs(expected.R - actual.R) <= 3 && Math.Abs(expected.G - actual.G) <= 3 && Math.Abs(expected.B - actual.B) <= 3;
        Assert.True(
            close,
            $"The {what} is #{actual.R:X2}{actual.G:X2}{actual.B:X2} on screen, expected #{expected.R:X2}{expected.G:X2}{expected.B:X2}. " +
            "If the colour is unrelated to the app, the desktop may be locked or the window covered.");
    }

    /// <summary>Writes <c>run.txt</c>, including anything that went wrong while stopping the app, and deletes the data folders.</summary>
    public void Dispose()
    {
        foreach (var problem in RealApp.Problems)
        {
            Note("PROBLEM: " + problem);
        }

        File.WriteAllText(Path.Combine(Folder, "run.txt"), _notes.ToString());

        foreach (var directory in _dataDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Left behind in the temp folder.
            }
        }
    }
}
