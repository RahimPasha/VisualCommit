using System.Globalization;
using System.Reflection;
using System.Text;
using Xunit;

namespace VisualCommit.LiveHostingTests;

/// <summary>
/// The live command's record (docs/phase-7a-design.md 14.5, D323), the class fixture of
/// <see cref="GitHubLiveTests"/>: the notes the steps take, in order, and at the end
/// <c>artifacts/live/github/run.txt</c> (outside <c>artifacts/visual</c>, which the gate's pictures
/// list takes whole). Every note is searched for the token (<see cref="LiveTokenForms"/>) before it
/// is kept or written to the test output, and run.txt's text again before it is written: a note
/// that holds the token is kept as <see cref="WithheldNote"/>, a file that holds it is not written,
/// and either fails the run at its end. A run whose tests failed before their first note (no
/// variables) writes no run.txt and leaves an earlier one as it was. It also keeps what
/// <c>Check_1</c> read of the sandbox for <c>Check_2</c>.
/// </summary>
public sealed class LiveRun : IDisposable
{
    /// <summary>What stands in the notes for a note that held the token.</summary>
    public const string WithheldNote = "A note was withheld: it held the token.";

    /// <summary>What stands in an exception's message for a text that held the token.</summary>
    public const string WithheldText = "(withheld: it held the token)";

    private static readonly string RepoRoot =
        typeof(LiveRun).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == "RepoRoot").Value!;

    private readonly string? _token;
    private readonly Action<string> _output;
    private readonly Lock _gate = new();
    private readonly List<string> _notes = [];
    private readonly List<string> _failures = [];
    private bool _disposed;

    /// <summary>The run of the test process: the captured token (<see cref="LiveSettings.CapturedToken"/>), <see cref="DefaultFile"/>, the current test's output.</summary>
    public LiveRun()
        : this(LiveSettings.CapturedToken, DefaultFile, line => TestContext.Current.TestOutputHelper?.WriteLine(line))
    {
    }

    /// <summary>A run that searches for <paramref name="token"/>, writes <paramref name="file"/> and sends each kept note to <paramref name="output"/>: for <see cref="LiveSafetyTests"/>.</summary>
    internal LiveRun(string? token, string file, Action<string> output)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);
        ArgumentNullException.ThrowIfNull(output);
        _token = string.IsNullOrEmpty(token) ? null : token;
        File = file;
        _output = output;
    }

    /// <summary><c>artifacts/live/github/run.txt</c> under the repo root.</summary>
    public static string DefaultFile => Path.Combine(RepoRoot, "artifacts", "live", "github", "run.txt");

    /// <summary>Where run.txt goes.</summary>
    internal string File { get; }

    /// <summary>The notes as kept, in order: a withheld one as <see cref="WithheldNote"/>.</summary>
    internal IReadOnlyList<string> Notes
    {
        get
        {
            lock (_gate)
            {
                return [.. _notes];
            }
        }
    }

    /// <summary>Why the run fails at its end: a withheld note or file. Empty while nothing was found.</summary>
    internal IReadOnlyList<string> Failures
    {
        get
        {
            lock (_gate)
            {
                return [.. _failures];
            }
        }
    }

    /// <summary>What <c>Check_1</c> read of the sandbox (<c>GET /repos/{o}/{r}</c>), for <c>Check_2</c>; null until read.</summary>
    internal LiveRepository? Repository { get; set; }

    /// <summary>
    /// Keeps <paramref name="text"/> and writes it to the test output, after searching it for the
    /// token: a note that holds it is kept and written as <see cref="WithheldNote"/>, and the run
    /// fails at its end.
    /// </summary>
    public void Note(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var kept = text;
        if (LiveTokenForms.FoundIn(text, _token) is { } form)
        {
            kept = WithheldNote;
            lock (_gate)
            {
                _failures.Add($"A note held the token ({form}); run.txt shows where.");
            }
        }

        lock (_gate)
        {
            _notes.Add(kept);
        }

        _output(kept);
    }

    /// <summary><paramref name="text"/> as it is, or <see cref="WithheldText"/> when it holds the token: for an exception's or an assertion's message.</summary>
    public string Screen(string text) => LiveTokenForms.FoundIn(text, _token) is null ? text : WithheldText;

    /// <summary>run.txt's text: a heading with the time it was written, then the notes, one or more lines each.</summary>
    internal string Render()
    {
        var text = new StringBuilder()
            .Append("VisualCommit's live command against GitHub (docs/phase-7a-design.md 14.5), written ")
            .Append(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            .Append(" UTC.\n\n");
        foreach (var note in Notes)
        {
            text.Append(note.Replace("\r\n", "\n", StringComparison.Ordinal)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Writes <paramref name="text"/> to <see cref="File"/> (UTF-8, no byte order mark), unless it
    /// holds the token: then nothing is written, an earlier file there is deleted (so that it is not
    /// read as this run's), and the run fails at its end.
    /// </summary>
    internal void WriteRunFile(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (LiveTokenForms.FoundIn(text, _token) is { } form)
        {
            System.IO.File.Delete(File);
            lock (_gate)
            {
                _failures.Add($"run.txt was withheld: its text held the token ({form}).");
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Writes run.txt when a note was taken, then fails when a note or the file held the token.</summary>
    /// <exception cref="InvalidOperationException">A note or run.txt's text held the token.</exception>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Notes.Count > 0)
        {
            WriteRunFile(Render());
        }

        var failures = Failures;
        if (failures.Count > 0)
        {
            throw new InvalidOperationException("The live command's record failed: " + string.Join(' ', failures));
        }
    }
}

/// <summary>
/// The forms in which the token is searched for in the live command's notes, run.txt and messages
/// (14.5, D323): as written, URL-escaped, as the base64 of <c>x-access-token:&lt;token&gt;</c> (git's
/// Basic header, ruling 7), as the base64 of the token itself (the macOS store's form, as
/// <c>TokenTripwire</c> searches), and any <see cref="PartLength"/> characters of it in a row (a prefix
/// cut wrongly), each compared ordinally. A form's name never holds the token.
/// </summary>
internal static class LiveTokenForms
{
    /// <summary>How many characters of the token in a row count as the token: none of GitHub's prefixes is as long.</summary>
    internal const int PartLength = 16;

    /// <summary>The name of the first form of <paramref name="token"/> found in <paramref name="text"/>, or null (also without a token).</summary>
    internal static string? FoundIn(string? text, string? token)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
        {
            return null;
        }

        foreach (var (form, value) in WholeForms(token))
        {
            if (text.Contains(value, StringComparison.Ordinal))
            {
                return form;
            }
        }

        for (var start = 0; start + PartLength <= token.Length; start++)
        {
            if (text.Contains(token.Substring(start, PartLength), StringComparison.Ordinal))
            {
                return $"{PartLength} of its characters in a row";
            }
        }

        return null;
    }

    /// <summary>The whole forms, each with its name.</summary>
    internal static IEnumerable<(string Form, string Value)> WholeForms(string token)
    {
        yield return ("as written", token);
        yield return ("URL-escaped", Uri.EscapeDataString(token));
        yield return ("as the base64 of x-access-token:<token>", Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + token)));
        yield return ("as base64", Convert.ToBase64String(Encoding.UTF8.GetBytes(token)));
    }
}
