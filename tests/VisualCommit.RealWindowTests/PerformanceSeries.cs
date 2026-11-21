using System.Globalization;
using System.Reflection;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The performance series (docs/phase-8a-design.md 10.3.5, D436, D437): the variable that turns it
/// on, the arithmetic of its lines and bounds, and its file. One run of <see cref="Phase8aPerformance"/>
/// writes one file, <c>artifacts/perf/phase-8a/series-&lt;yyyyMMdd-HHmmss&gt;-&lt;commit&gt;.txt</c> under
/// the repository's root, never through <see cref="RealWindowRun"/>, which empties the real-window
/// folder that holds the gate's captures. Each line is appended as it is measured, so that a run
/// that fails keeps what it measured before.
/// </summary>
public sealed class PerformanceSeries
{
    /// <summary>The variable that turns the series on: the runs per measure, 1 to <see cref="MostRuns"/>. Only the lead sets it, on the rested laptop.</summary>
    public const string RunsVariable = "VISUALCOMMIT_PERF_RUNS";

    /// <summary>The most runs per measure <see cref="RunsVariable"/> may ask for.</summary>
    public const int MostRuns = 20;

    /// <summary>A bound is the slowest counted run of the "before" series times this (D436).</summary>
    public const decimal BoundFactor = 1.25m;

    /// <summary>A time's bound rounds up to this many milliseconds (D437).</summary>
    public const int TimeStep = 100;

    /// <summary>M7's bound rounds up to this many milliseconds (D437).</summary>
    public const int PullReadsStep = 10;

    /// <summary>A memory bound rounds up to this many megabytes (D437).</summary>
    public const int MemoryStep = 10;

    /// <summary>The bytes of a megabyte in every memory line.</summary>
    public const double BytesPerMegabyte = 1_048_576;

#if DEBUG
    /// <summary>The configuration this project and the app it starts were built in, for the head line.</summary>
    private const string BuildConfiguration = "Debug";
#else
    /// <summary>The configuration this project and the app it starts were built in, for the head line.</summary>
    private const string BuildConfiguration = "Release";
#endif

    private static readonly string RepoRoot =
        typeof(PerformanceSeries).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == "RepoRoot").Value!;

    private readonly Lock _gate = new();

    private PerformanceSeries(string file) => FilePath = file;

    /// <summary>
    /// The runs per measure that <see cref="RunsVariable"/> asks for, read now; null when it is
    /// unset, and the series' facts skip. Any value but a whole number from 1 to <see cref="MostRuns"/>
    /// throws, which fails the fact that reads it.
    /// </summary>
    public static int? Runs => ParseRuns(Environment.GetEnvironmentVariable(RunsVariable));

    /// <summary>The file this run of the series writes.</summary>
    public string FilePath { get; }

    /// <summary>
    /// <paramref name="value"/> as runs per measure: null for null (the variable unset), a whole
    /// number from 1 to <see cref="MostRuns"/> as itself.
    /// </summary>
    /// <exception cref="ArgumentException">Any other value.</exception>
    public static int? ParseRuns(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var runs) && runs is >= 1 and <= MostRuns)
        {
            return runs;
        }

        throw new ArgumentException(
            $"{RunsVariable} is \"{value}\": it takes a whole number of runs per measure from 1 to {MostRuns}, or is left unset.",
            nameof(value));
    }

    /// <summary>The median: the middle value of an odd count, the mean of the two middle values of an even one.</summary>
    /// <exception cref="ArgumentException">No value.</exception>
    public static double Median(IReadOnlyCollection<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("A median needs one value or more.", nameof(values));
        }

        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    /// <summary>The lowest and the highest value.</summary>
    /// <exception cref="ArgumentException">No value.</exception>
    public static (double Lowest, double Highest) Range(IReadOnlyCollection<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("A range needs one value or more.", nameof(values));
        }

        return (values.Min(), values.Max());
    }

    /// <summary>
    /// The bound a measure's "before" series gives: its slowest counted run times
    /// <see cref="BoundFactor"/>, rounded up to a whole <paramref name="step"/> (D437):
    /// <see cref="TimeStep"/> for a time, <see cref="PullReadsStep"/> for M7, <see cref="MemoryStep"/> for memory.
    /// </summary>
    public static long Bound(double slowest, int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slowest);
        ArgumentOutOfRangeException.ThrowIfLessThan(step, 1);

        // In decimal, so that a product that is a whole step (400 × 1.25) is not rounded up a step by a binary fraction.
        var scaled = (decimal)slowest * BoundFactor;
        return (long)(Math.Ceiling(scaled / step) * step);
    }

    /// <summary>Bytes in megabytes of 1,048,576 bytes.</summary>
    public static double Megabytes(long bytes) => bytes / BytesPerMegabyte;

    /// <summary>A measured value as the file writes it: invariant, at most one decimal, no group separator.</summary>
    public static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>
    /// A measure's summary line: <c>{measure}: median {m} {unit}, range {a} to {b} {unit}, {n} runs;</c>
    /// then <c>bound {B} {unit}, held</c> when every value is at or below <paramref name="bound"/>,
    /// <c>bound {B} {unit}, not held</c> when one is above, or <c>bound not set</c> without one.
    /// </summary>
    public static string Summary(string measure, string unit, IReadOnlyCollection<double> values, long? bound)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(measure);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        var (lowest, highest) = Range(values);
        var runs = values.Count == 1 ? "1 run" : $"{values.Count.ToString(CultureInfo.InvariantCulture)} runs";
        var held = bound is { } limit
            ? $"bound {limit.ToString(CultureInfo.InvariantCulture)} {unit}, {(highest <= limit ? "held" : "not held")}"
            : "bound not set";
        return $"{measure}: median {Format(Median(values))} {unit}, range {Format(lowest)} to {Format(highest)} {unit}, {runs}; {held}";
    }

    /// <summary>
    /// Starts this run's file: reads the repository's commit and whether it has changes, and the
    /// tests' git, and writes the head line. The reads run with the isolation of a scenario's git
    /// and without optional locks, as the app's reads do (D47): the real-window project's process is
    /// not isolated, and a plain <c>git status</c> would read the machine's configuration and
    /// refresh the index of the checkout under its lock.
    /// </summary>
    public static async Task<PerformanceSeries> StartAsync(int runs, CancellationToken cancellationToken)
    {
        using var home = new TempDirectory("perf-home");
        var runner = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The series needs git."));
        var environment = new Dictionary<string, string?>(TempRepo.IsolatedEnvironment(home.Path))
        {
            ["GIT_OPTIONAL_LOCKS"] = "0",
        };

        async Task<string> GitAsync(params string[] arguments)
        {
            var command = new GitCommand(arguments) { WorkingDirectory = RepoRoot, Environment = environment };
            return (await runner.RunAsync(command, cancellationToken)).EnsureSuccess(command).StandardOutput.Trim();
        }

        var commit = await GitAsync("rev-parse", "--short=7", "HEAD");
        var changes = (await GitAsync("status", "--porcelain")).Length > 0;
        var version = await GitAsync("--version");

        var started = DateTimeOffset.Now;
        var folder = Path.Combine(RepoRoot, "artifacts", "perf", "phase-8a");
        Directory.CreateDirectory(folder);
        var name = string.Create(CultureInfo.InvariantCulture, $"series-{started:yyyyMMdd-HHmmss}-{commit}{(changes ? "-changes" : string.Empty)}.txt");
        var series = new PerformanceSeries(Path.Combine(folder, name));
        series.Write(string.Create(
            CultureInfo.InvariantCulture,
            $"Performance series of {started:yyyy-MM-dd HH:mm:ss zzz}: commit {commit}{(changes ? " with changes" : string.Empty)}, {version}, {runs} {(runs == 1 ? "run" : "runs")} per measure, the {BuildConfiguration} build."));
        return series;
    }

    /// <summary>Appends one line to the file, at once.</summary>
    public void Write(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        lock (_gate)
        {
            File.AppendAllText(FilePath, line + Environment.NewLine);
        }
    }
}
