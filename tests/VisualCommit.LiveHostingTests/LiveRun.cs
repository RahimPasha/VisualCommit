using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Xunit;
using Xunit.Sdk;

namespace VisualCommit.LiveHostingTests;

/// <summary>
/// The live command's record (docs/phase-7a-design.md 14.5, D323), the class fixture of
/// <see cref="GitHubLiveTests"/>: the notes the steps take, in order, and at the end
/// <c>artifacts/live/github/run.txt</c> (outside <c>artifacts/visual</c>, which the gate's pictures
/// list takes whole). Every note is searched for the token (<see cref="LiveTokenForms"/>) before it
/// is kept or written to the test output, and run.txt's text again before it is written: a note
/// that holds the token is kept as <see cref="WithheldNote"/>, a file that holds it is not written,
/// and either fails the run at its end. So do a message (<see cref="Screen"/>) and a failed step's
/// exception (<see cref="FailureOf"/>) that hold it: they are replaced before they reach the test
/// output. A run whose tests failed before their first note (no variables) writes a run.txt that
/// says so (<see cref="NoNotes"/>), so that an earlier run's file is never read as this run's. It
/// also keeps what <c>Check_1</c> read of the sandbox for <c>Check_2</c>.
/// </summary>
public sealed class LiveRun : IDisposable
{
    /// <summary>What stands in the notes for a note that held the token.</summary>
    public const string WithheldNote = "A note was withheld: it held the token.";

    /// <summary>What stands in an exception's message for a text that held the token.</summary>
    public const string WithheldText = "(withheld: it held the token)";

    /// <summary>run.txt's only line after its heading when no note was taken.</summary>
    public const string NoNotes = "No note was taken: the tests failed before their first step.";

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

    /// <summary>
    /// <paramref name="text"/> as it is, or <see cref="WithheldText"/> when it holds the token: for an
    /// exception's or an assertion's message. A find fails the run at its end, as a note's does.
    /// </summary>
    public string Screen(string text)
    {
        if (LiveTokenForms.FoundIn(text, _token) is not { } form)
        {
            return text;
        }

        Withheld($"A message held the token ({form}); it was withheld.");
        return WithheldText;
    }

    /// <summary>Records that a text held the token and was withheld (<see cref="GitHubLiveApi"/>'s answers too): the run fails at its end.</summary>
    internal void Withheld(string why)
    {
        ArgumentException.ThrowIfNullOrEmpty(why);
        lock (_gate)
        {
            _failures.Add(why);
        }
    }

    /// <summary>
    /// What a step that failed with <paramref name="exception"/> throws at the test's end: the
    /// exception itself, unless its text (<see cref="Exception.ToString"/>, with its inner exceptions)
    /// holds the token, as an assertion's message that shows a URL or a git value would. Then an
    /// <see cref="XunitException"/> that names the step and the exception's type, without an inner
    /// exception, and the run fails at its end; the test output never sees the original's text.
    /// </summary>
    internal ExceptionDispatchInfo FailureOf(Exception exception, string step)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (LiveTokenForms.FoundIn(exception.ToString(), _token) is not { } form)
        {
            return ExceptionDispatchInfo.Capture(exception);
        }

        Withheld($"The failure of step {step} held the token ({form}); its exception was withheld.");
        return ExceptionDispatchInfo.Capture(new XunitException($"Step {step} failed with {exception.GetType().Name}, whose text held the token, so it is withheld (D323)."));
    }

    /// <summary>run.txt's text: a heading with the time it was written, then the notes, one or more lines each, or <see cref="NoNotes"/>.</summary>
    internal string Render()
    {
        var text = new StringBuilder()
            .Append("VisualCommit's live command against GitHub (docs/phase-7a-design.md 14.5), written ")
            .Append(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            .Append(" UTC.\n\n");
        var notes = Notes;
        foreach (var note in notes.Count > 0 ? notes : [NoNotes])
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

    /// <summary>Writes run.txt, <see cref="NoNotes"/> when no note was taken, then fails when a note, a message, an exception or the file held the token.</summary>
    /// <exception cref="InvalidOperationException">A note, a message, an exception or run.txt's text held the token.</exception>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        WriteRunFile(Render());

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
