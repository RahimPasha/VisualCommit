using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using VisualCommit.Core.Hosting;
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
/// also keeps what <c>Check_1</c> read of the sandbox for <c>Check_2</c>. For Azure DevOps and GitLab
/// (docs/phase-7b-design.md 15.3) the fixtures <see cref="AzureDevOpsLiveRun"/> and
/// <see cref="GitLabLiveRun"/> add a <see cref="LiveScreen"/>: after the token's search, every note,
/// message, failure and run.txt is screened (<see cref="ScreenedBy"/>), and one that holds a learned
/// value in a form the screen does not replace, or still holds one once screened, is withheld and
/// fails the run (D323, D372).
/// </summary>
public class LiveRun : IDisposable
{
    /// <summary>What stands in the notes for a note that held the token.</summary>
    public const string WithheldNote = "A note was withheld: it held the token.";

    /// <summary>What stands in an exception's message for a text that held the token.</summary>
    public const string WithheldText = "(withheld: it held the token)";

    /// <summary>What begins the stand-in for a note that the screen left holding a value (15.3): the kind of value follows, never the value.</summary>
    public const string ScreenedNotePrefix = "A note was withheld: it still held ";

    /// <summary>run.txt's only line after its heading when no note was taken.</summary>
    public const string NoNotes = "No note was taken: the tests failed before their first step.";

    private static readonly string RepoRoot =
        typeof(LiveRun).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == "RepoRoot").Value!;

    private readonly string? _token;
    private readonly HostingProvider _provider;
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
        : this(token, file, output, null, HostingProvider.GitHub)
    {
    }

    /// <summary>A new provider's run (15.3): its captured token, <c>artifacts/live/&lt;id&gt;/run.txt</c>, and a screen that knows Azure DevOps' organization or GitLab's namespace from the variable (D384).</summary>
    protected LiveRun(HostingProvider provider)
        : this(LiveSettings.CapturedTokenOf(provider), FileOf(provider), line => TestContext.Current.TestOutputHelper?.WriteLine(line), ScreenOf(provider), provider)
    {
    }

    /// <summary>A run with <paramref name="screen"/> for <paramref name="provider"/>: for <see cref="LiveScreenTests"/>.</summary>
    internal LiveRun(string? token, string file, Action<string> output, LiveScreen? screen, HostingProvider provider)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);
        ArgumentNullException.ThrowIfNull(output);
        _token = string.IsNullOrEmpty(token) ? null : token;
        File = file;
        _output = output;
        Screener = screen;
        _provider = provider;
    }

    /// <summary><c>artifacts/live/github/run.txt</c> under the repo root.</summary>
    public static string DefaultFile => Path.Combine(RepoRoot, "artifacts", "live", "github", "run.txt");

    /// <summary><c>artifacts/live/&lt;provider id&gt;/run.txt</c> (0.3: <c>azure-devops</c>, <c>gitlab</c>, beside 7a's <c>github</c>).</summary>
    public static string FileOf(HostingProvider provider) => Path.Combine(RepoRoot, "artifacts", "live", HostingProviders.Id(provider), "run.txt");

    /// <summary>The screen of the run's notes (15.3), or null for GitHub's, which keeps 7a's.</summary>
    internal LiveScreen? Screener { get; }

    /// <summary>The token this run searches for, for the run's own API: null without one.</summary>
    internal string? Token => _token;

    /// <summary>The provider's screen: Azure DevOps' with the organization its variable names, GitLab's with the namespace's first segment its variable names (D384).</summary>
    private static LiveScreen ScreenOf(HostingProvider provider) =>
        ScreenOf(provider, LiveSettings.CapturedOrganization, LiveSettings.CapturedGitLabNamespace);

    /// <summary>The screen of <paramref name="provider"/>'s run, made before its first request: Azure DevOps' with <paramref name="organization"/>, GitLab's with <paramref name="namespace"/>, each when given.</summary>
    internal static LiveScreen ScreenOf(HostingProvider provider, string? organization, string? @namespace)
    {
        var screen = new LiveScreen();
        if (provider == HostingProvider.AzureDevOps && organization is not null)
        {
            screen.AddOrganization(organization);
        }
        else if (provider == HostingProvider.GitLab && @namespace is not null)
        {
            screen.AddNamespace(@namespace);
        }

        return screen;
    }

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

    /// <summary>Whether this run's identity read (connectionData, <c>GET user</c>) has answered: each method's first call, once per run (docs/phase-7b-design.md 15.3).</summary>
    internal bool IdentityRead { get; set; }

    /// <summary>What <c>Check_2</c> made, for <c>Check_2b_probes</c>, which probes it and cleans it up (15.5 steps 8 and 9); null until <c>Check_2</c> knows its branches, and again once taken.</summary>
    internal LiveMade? Made { get; set; }

    /// <summary>
    /// Keeps <paramref name="text"/> and writes it to the test output, after searching it for the
    /// token: a note that holds it is kept and written as <see cref="WithheldNote"/>, and the run
    /// fails at its end. With a screen, the note is screened (<see cref="ScreenedBy"/>), and one the
    /// screen withholds is kept and written as <see cref="ScreenedNotePrefix"/> and the kind of value,
    /// and the run fails at its end too.
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
        else if (Screener is { } screen)
        {
            if (ScreenedBy(screen, text, out var kind) is { } screened)
            {
                kept = screened;
            }
            else
            {
                kept = ScreenedNotePrefix + kind + ".";
                Withheld($"A note still held {kind} once screened (D372); run.txt shows where.");
            }
        }

        lock (_gate)
        {
            _notes.Add(kept);
        }

        _output(kept);
    }

    /// <summary>
    /// <paramref name="text"/> as it is (screened, with a screen: <see cref="ScreenedBy"/>), or
    /// <see cref="WithheldText"/> when it holds the token: for an exception's or an assertion's message.
    /// A text the screen withholds is replaced by a text that names the kind of value alone. A find
    /// fails the run at its end, as a note's does.
    /// </summary>
    public string Screen(string text)
    {
        if (LiveTokenForms.FoundIn(text, _token) is not { } form)
        {
            if (Screener is not { } screen)
            {
                return text;
            }

            if (ScreenedBy(screen, text, out var kind) is { } screened)
            {
                return screened;
            }

            Withheld($"A message still held {kind} once screened (D372); it was withheld.");
            return $"(withheld: it still held {kind})";
        }

        Withheld($"A message held the token ({form}); it was withheld.");
        return WithheldText;
    }

    /// <summary>
    /// <paramref name="text"/> screened by <paramref name="screen"/>, or null with the kind of value in
    /// <paramref name="kind"/> when the text is withheld: when a reading of the text as it stands shows a
    /// learned value in a form <see cref="LiveScreen.Apply"/> does not replace
    /// (<see cref="LiveScreen.EscapedValueIn"/>, judged before Apply, which then only meets values written
    /// as themselves), or when the screened text still holds a value the screen knows
    /// (<see cref="LiveScreen.SurvivorIn"/>). A text with nothing to hide comes back as it was.
    /// </summary>
    internal static string? ScreenedBy(LiveScreen screen, string text, out string? kind)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(text);
        kind = screen.EscapedValueIn(text);
        if (kind is not null)
        {
            return null;
        }

        var screened = screen.Apply(text);
        kind = screen.SurvivorIn(screened);
        return kind is null ? screened : null;
    }

    /// <summary>The one assertion given values (15.3, g4 D1.4): fails with <paramref name="message"/>'s text, screened, unless <paramref name="condition"/> holds.</summary>
    /// <exception cref="XunitException"><paramref name="condition"/> is false.</exception>
    public void Check(bool condition, Func<string> message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!condition)
        {
            throw new XunitException(Screen(message()));
        }
    }

    /// <summary>
    /// The data folder's <c>accounts.json</c> and logs searched for the learned addresses (15.3): one
    /// problem per file that holds one, naming the file and never the value; none without a screen.
    /// </summary>
    internal IReadOnlyList<string> DataFolderProblems(string dataFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataFolder);
        if (Screener is null || !Directory.Exists(dataFolder))
        {
            return [];
        }

        var problems = new List<string>();
        foreach (var path in Directory.EnumerateFiles(dataFolder, "*", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetFileName(path), "accounts.json", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(path), ".log", StringComparison.OrdinalIgnoreCase)))
        {
            string text;
            try
            {
                text = System.IO.File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                problems.Add($"The data folder's {Path.GetFileName(path)} could not be read to prove it holds no address: {ex.GetType().Name}.");
                continue;
            }

            if (Screener.SurvivorInDataFile(text) is { } kind)
            {
                problems.Add($"The data folder's {Path.GetFileName(path)} held {kind} (D372).");
            }
        }

        return problems;
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
    /// exception, and the run fails at its end; the test output never sees the original's text. With
    /// a screen, a text the screen changes is rethrown screened, and one the screen withholds
    /// (<see cref="ScreenedBy"/>), its text or its message, is withheld as a token's is.
    /// </summary>
    internal ExceptionDispatchInfo FailureOf(Exception exception, string step)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (LiveTokenForms.FoundIn(exception.ToString(), _token) is not { } form)
        {
            var whole = exception.ToString();
            if (Screener is not { } screen)
            {
                return ExceptionDispatchInfo.Capture(exception);
            }

            string? kind = null;
            var screenedWhole = ScreenedBy(screen, whole, out var wholeKind);
            var message = screenedWhole is null ? null : ScreenedBy(screen, exception.Message, out kind);
            if (screenedWhole is null || message is null)
            {
                kind = wholeKind ?? kind;
                Withheld($"The failure of step {step} still held {kind} once screened (D372); its exception was withheld.");
                return ExceptionDispatchInfo.Capture(new XunitException($"Step {step} failed with {exception.GetType().Name}, whose text still held {kind} once screened, so it is withheld (D372)."));
            }

            return !string.Equals(screenedWhole, whole, StringComparison.Ordinal)
                ? ExceptionDispatchInfo.Capture(new XunitException($"Step {step} failed with {exception.GetType().Name}: {message}"))
                : ExceptionDispatchInfo.Capture(exception);
        }

        Withheld($"The failure of step {step} held the token ({form}); its exception was withheld.");
        return ExceptionDispatchInfo.Capture(new XunitException($"Step {step} failed with {exception.GetType().Name}, whose text held the token, so it is withheld (D323)."));
    }

    /// <summary>run.txt's text: a heading with the time it was written, then the notes, one or more lines each, or <see cref="NoNotes"/>.</summary>
    internal string Render()
    {
        var text = new StringBuilder()
            .Append(_provider == HostingProvider.GitHub
                ? "VisualCommit's live command against GitHub (docs/phase-7a-design.md 14.5), written "
                : $"VisualCommit's live command against {HostingProviders.DisplayName(_provider)} (docs/phase-7b-design.md 15.7), written ")
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
            DeleteEarlierFile();
            lock (_gate)
            {
                _failures.Add($"run.txt was withheld: its text held the token ({form}).");
            }

            return;
        }

        if (Screener is { } screen)
        {
            if (ScreenedBy(screen, text, out var kind) is not { } screened)
            {
                DeleteEarlierFile();
                lock (_gate)
                {
                    _failures.Add($"run.txt was withheld: its screened text still held {kind} (D323, D372).");
                }

                return;
            }

            text = screened;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Deletes an earlier run's file, so that it is not read as this run's; a provider's first run has no folder yet, and nothing to delete.</summary>
    private void DeleteEarlierFile()
    {
        if (System.IO.File.Exists(File))
        {
            System.IO.File.Delete(File);
        }
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
/// What a new provider's <c>Check_2</c> hands to <c>Check_2b_probes</c> (15.5 steps 8 and 9): its
/// branch, the branch with nothing ahead, the target and the title, known before the push; the
/// request's number, the account's user id (Azure DevOps) and the pushed commit, once the create answered.
/// </summary>
internal sealed record LiveMade(string Branch, string Even, string Target, string Title)
{
    public int? Number { get; init; }

    public string? UserId { get; init; }

    public string? Head { get; init; }
}

/// <summary>Azure DevOps' run (15.3): <c>artifacts/live/azure-devops/run.txt</c>, its token and its screen.</summary>
public sealed class AzureDevOpsLiveRun : LiveRun
{
    public AzureDevOpsLiveRun()
        : base(HostingProvider.AzureDevOps)
    {
    }

    /// <summary>A run that writes <paramref name="file"/>, for <see cref="LiveSafetyTests"/> driving <see cref="AzureDevOpsLiveTests"/>' steps over a stub.</summary>
    internal AzureDevOpsLiveRun(string? token, string file, Action<string> output, LiveScreen screen)
        : base(token, file, output, screen, HostingProvider.AzureDevOps)
    {
    }
}

/// <summary>GitLab's run (15.3): <c>artifacts/live/gitlab/run.txt</c>, its token and its screen of the namespace (D384) and addresses.</summary>
public sealed class GitLabLiveRun : LiveRun
{
    public GitLabLiveRun()
        : base(HostingProvider.GitLab)
    {
    }

    /// <summary>A run that writes <paramref name="file"/>, for <see cref="LiveSafetyTests"/> driving <see cref="GitLabLiveTests"/>' steps over a stub.</summary>
    internal GitLabLiveRun(string? token, string file, Action<string> output, LiveScreen screen)
        : base(token, file, output, screen, HostingProvider.GitLab)
    {
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
        for (var offset = 0; offset < 3; offset++)
        {
            if (Base64AtOffset(token, offset) is { } aligned)
            {
                yield return ($"as base64 at offset {offset.ToString(CultureInfo.InvariantCulture)}", aligned);
            }
        }
    }

    /// <summary>
    /// The part of the base64 of any text that holds <paramref name="token"/> after a prefix leaving
    /// <paramref name="offset"/> bytes over a multiple of 3 (g4 D1.6; as <c>TokenTripwire</c>'s): it
    /// covers Azure DevOps' <c>:&lt;token&gt;</c>, <c>x-access-token:</c> and any user before the token.
    /// Null when under 8 characters are left.
    /// </summary>
    internal static string? Base64AtOffset(string token, int offset)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var aligned = new byte[offset + bytes.Length];
        bytes.CopyTo(aligned, offset);
        var full = aligned.Length / 3 * 3;
        var start = offset > 0 ? 3 : 0;
        if (full - start <= 0)
        {
            return null;
        }

        var text = Convert.ToBase64String(aligned, start, full - start);
        return text.Length >= 8 ? text : null;
    }
}
