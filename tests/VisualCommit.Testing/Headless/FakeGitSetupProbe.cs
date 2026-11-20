using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Git;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Stands in for the machine's search for git and for the first-run screen's guidance facts
/// (docs/phase-8a-design.md 1.15, 5.3): a test reaches each state of the screen without touching the
/// machine's <c>PATH</c>, git or os-release. Every lookup is recorded in <see cref="Lookups"/>, in
/// order, a probe of a chosen file (<c>GitAccess.ProbeAsync</c>, <c>new GitLookup(path, null)</c>)
/// as well as the session's searches. A lookup whose <see cref="GitLookup.ChosenPath"/> has an
/// <see cref="AnswerFor"/> entry gets that answer, every time; any other takes the next answer of
/// the queue, the last repeated. The guidance is fixed for the session. <see cref="HoldNext"/> holds
/// the next lookup until the test releases it, so that a check can see what shows while a search runs.
/// The made-up values are the report's (ruling 63).
/// </summary>
public sealed class FakeGitSetupProbe : IGitSetupProbe
{
    /// <summary>Git for Windows' usual git, as the fake reports it.</summary>
    public const string WindowsGit = @"C:\Program Files\Git\cmd\git.exe";

    /// <summary>The git of the macOS and Linux states.</summary>
    public const string UnixGit = "/usr/bin/git";

    /// <summary>The global configuration a broken git names.</summary>
    public const string DemoGitConfig = "C:/Users/demo/.gitconfig";

    /// <summary>winget's path in the Windows guidance.</summary>
    public const string WingetPath = @"C:\Users\demo\AppData\Local\Microsoft\WindowsApps\winget.exe";

    /// <summary>Every too-old git's version.</summary>
    public const string TooOldVersion = "2.29.0";

    /// <summary>Why a git could not be started (F7's message), made up.</summary>
    public const string NotStartedMessage = "The system cannot find the file specified.";

    /// <summary>What a git of damaged Command Line Tools says (F9's words), made up after xcrun's.</summary>
    public const string ToolsBrokenWords = "xcrun: error: invalid active developer path (/Library/Developer/CommandLineTools), missing xcrun at: /Library/Developer/CommandLineTools/usr/bin/xcrun";

    private readonly Lock _gate = new();
    private readonly List<FakeGitAnswer> _queue;
    private readonly Dictionary<string, FakeGitAnswer> _answersFor = new(StringComparer.Ordinal);
    private readonly List<GitLookup> _lookups = [];
    private ProbeHold? _hold;
    private int _next;
    private int _guidanceReads;

    /// <param name="guidance">The guidance's facts, fixed for the session.</param>
    /// <param name="answers">The answers to the lookups that no <see cref="AnswerFor"/> entry answers, in order, the last repeated.</param>
    public FakeGitSetupProbe(GitGuidance guidance, params FakeGitAnswer[] answers)
    {
        ArgumentNullException.ThrowIfNull(guidance);
        ArgumentNullException.ThrowIfNull(answers);
        if (answers.Length == 0)
        {
            throw new ArgumentException("Give the probe at least one answer.", nameof(answers));
        }

        Guidance = guidance;
        _queue = [.. answers];
    }

    /// <summary>Ubuntu 22.04's os-release, made up from its fields.</summary>
    public static LinuxDistribution Ubuntu { get; } = new("ubuntu", ["debian"], "Ubuntu 22.04.5 LTS");

    /// <summary>Alpine's os-release: <c>ID</c> <c>alpine</c>, no <c>ID_LIKE</c>.</summary>
    public static LinuxDistribution Alpine { get; } = new("alpine", [], "Alpine Linux v3.20");

    /// <summary>The Windows guidance, with winget at <paramref name="wingetPath"/> (null: no winget).</summary>
    public static GitGuidance Windows(string? wingetPath = WingetPath) => new(GuidancePlatform.Windows, wingetPath, null);

    /// <summary>The macOS guidance.</summary>
    public static GitGuidance MacOS { get; } = new(GuidancePlatform.MacOS, null, null);

    /// <summary>The Linux guidance of <paramref name="distribution"/> (null: unknown).</summary>
    public static GitGuidance Linux(LinuxDistribution? distribution) => new(GuidancePlatform.Linux, null, distribution);

    /// <summary>The guidance's facts.</summary>
    public GitGuidance Guidance { get; }

    /// <summary>How often the guidance was read: once per session at most.</summary>
    public int GuidanceReads => Volatile.Read(ref _guidanceReads);

    /// <summary>Every lookup received, probes included, in order.</summary>
    public IReadOnlyList<GitLookup> Lookups
    {
        get
        {
            lock (_gate)
            {
                return [.. _lookups];
            }
        }
    }

    /// <summary>The answer to every lookup whose chosen path is <paramref name="path"/>, probe or search alike.</summary>
    public FakeGitSetupProbe AnswerFor(string path, FakeGitAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(answer);
        lock (_gate)
        {
            _answersFor[path] = answer;
        }

        return this;
    }

    /// <summary>Holds the next lookup, after it is recorded, until <see cref="ProbeHold.Release"/>.</summary>
    public ProbeHold HoldNext()
    {
        var hold = new ProbeHold();
        lock (_gate)
        {
            _hold = hold;
        }

        return hold;
    }

    /// <inheritdoc/>
    public async Task<GitDetection> DetectAsync(
        GitLookup lookup,
        IGitCallLog? callLog,
        IAppLog log,
        TimeProvider? recordClock,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        FakeGitAnswer answer;
        ProbeHold? hold;
        lock (_gate)
        {
            _lookups.Add(lookup);
            if (lookup.ChosenPath is { } chosen && _answersFor.TryGetValue(chosen, out var forPath))
            {
                answer = forPath;
            }
            else
            {
                answer = _queue[Math.Min(_next, _queue.Count - 1)];
                _next++;
            }

            hold = _hold;
            _hold = null;
        }

        if (hold is not null)
        {
            await hold.WaitAsync(cancellationToken);
        }

        return await answer.AnswerAsync(lookup, callLog, log, recordClock, environment, cancellationToken);
    }

    /// <inheritdoc/>
    public GitGuidance ReadGuidance()
    {
        Interlocked.Increment(ref _guidanceReads);
        return Guidance;
    }
}

/// <summary>A lookup the fake holds (<see cref="FakeGitSetupProbe.HoldNext"/>) until the test releases it.</summary>
public sealed class ProbeHold
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes once the held lookup has come.</summary>
    public Task Started => _started.Task;

    /// <summary>Lets the held lookup answer.</summary>
    public void Release() => _released.TrySetResult();

    internal Task WaitAsync(CancellationToken cancellationToken)
    {
        _started.TrySetResult();
        return _released.Task.WaitAsync(cancellationToken);
    }
}

/// <summary>
/// One answer of <see cref="FakeGitSetupProbe"/>: a detection made up for a state of the first-run
/// screen, or the machine's git reported at a made-up path (<see cref="RealAt"/>).
/// </summary>
public abstract record FakeGitAnswer
{
    /// <summary>No git was found.</summary>
    public static FakeGitAnswer Missing { get; } = new Made(_ => new GitDetection(GitAvailability.NotFound, null, null, null));

    /// <summary>No git was found, and macOS's <c>/usr/bin/git</c> was left out: the Command Line Tools are missing.</summary>
    public static FakeGitAnswer CommandLineToolsMissing { get; } =
        new Made(_ => new GitDetection(GitAvailability.NotFound, null, null, null) { CommandLineToolsMissing = true });

    /// <summary>The chosen git is gone (its path the lookup's), and the machine's search found <paramref name="candidate"/> (null: none).</summary>
    public static FakeGitAnswer ChosenMissing(string? candidate) =>
        new Made(lookup => new GitDetection(GitAvailability.NotFound, null, null, null)
        {
            Source = GitSource.Setting,
            ChosenMissing = lookup.ChosenPath ?? throw new InvalidOperationException("ChosenMissing answers a lookup with a chosen path only."),
            PathCandidate = candidate,
        });

    /// <summary>A git older than the minimum at <paramref name="path"/>.</summary>
    public static FakeGitAnswer TooOld(string path, GitSource source = GitSource.Path, string version = FakeGitSetupProbe.TooOldVersion) =>
        new Made(_ => new GitDetection(GitAvailability.TooOld, path, Version(version), null) { Source = source });

    /// <summary>A git at <paramref name="path"/> that did not report its version: <paramref name="words"/> on standard error, exit <paramref name="exitCode"/>.</summary>
    public static FakeGitAnswer NotWorking(string path, string words, int exitCode = 128, GitSource source = GitSource.Path) =>
        new Made(_ => new GitDetection(GitAvailability.Broken, path, null, null)
        {
            Source = source,
            ErrorText = words,
            ExitCode = exitCode,
            ConfigFault = null,
        });

    /// <summary>A git at <paramref name="path"/> that could not read line <paramref name="line"/> of <paramref name="file"/>, in git's words, exit 128.</summary>
    public static FakeGitAnswer ConfigFault(string path, string file, int line, GitSource source = GitSource.Path) =>
        new Made(_ => new GitDetection(GitAvailability.Broken, path, null, null)
        {
            Source = source,
            ErrorText = ConfigFaultWords(file, line),
            ExitCode = 128,
            ConfigFault = new GitConfigFault(file, line),
        });

    /// <summary>A git at <paramref name="path"/> that could not be started, for <paramref name="message"/>.</summary>
    public static FakeGitAnswer NotStarted(string path, string message = FakeGitSetupProbe.NotStartedMessage, GitSource source = GitSource.Path) =>
        new Made(_ => new GitDetection(GitAvailability.Broken, path, null, null) { Source = source, ErrorText = message });

    /// <summary>
    /// The machine's git, found the harness's usual way and run for every git call, reported at the
    /// made-up <paramref name="path"/> and <paramref name="source"/> and, if given, at
    /// <paramref name="version"/> (else the machine git's own).
    /// </summary>
    public static FakeGitAnswer RealAt(string path, GitSource source = GitSource.Path, string? version = null) => new Real(path, source, version);

    /// <summary>Git's words for a configuration line it cannot read.</summary>
    public static string ConfigFaultWords(string file, int line) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"fatal: bad config line {line} in file {file}");

    /// <summary>The detection this answer gives to <paramref name="lookup"/>.</summary>
    public abstract Task<GitDetection> AnswerAsync(
        GitLookup lookup,
        IGitCallLog? callLog,
        IAppLog log,
        TimeProvider? recordClock,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken);

    private static GitVersion Version(string text) =>
        GitVersion.TryParse(text, out var version) ? version : throw new ArgumentException($"\"{text}\" is no version.", nameof(text));

    /// <summary>A detection made up from the lookup alone.</summary>
    private sealed record Made(Func<GitLookup, GitDetection> Make) : FakeGitAnswer
    {
        public override Task<GitDetection> AnswerAsync(GitLookup lookup, IGitCallLog? callLog, IAppLog log, TimeProvider? recordClock, IReadOnlyDictionary<string, string?>? environment, CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested ? Task.FromCanceled<GitDetection>(cancellationToken) : Task.FromResult(Make(lookup));
    }

    /// <summary>The machine's git at a made-up path, source and version.</summary>
    private sealed record Real(string Path, GitSource Source, string? ReportedVersion) : FakeGitAnswer
    {
        public override async Task<GitDetection> AnswerAsync(GitLookup lookup, IGitCallLog? callLog, IAppLog log, TimeProvider? recordClock, IReadOnlyDictionary<string, string?>? environment, CancellationToken cancellationToken)
        {
            var real = await MachineGitSetupProbe.Instance.DetectAsync(new GitLookup(null, null), callLog, log, recordClock, environment, cancellationToken);
            if (real is not { Availability: GitAvailability.Available })
            {
                throw new InvalidOperationException($"The tests need a working git: the machine's search found {real.Availability}.");
            }

            return real with
            {
                ExecutablePath = Path,
                Source = Source,
                Version = ReportedVersion is null ? real.Version : Version(ReportedVersion),
            };
        }
    }
}
