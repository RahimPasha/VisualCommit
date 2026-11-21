using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// Runs the series' facts in the order of the measures, M2 first, so that M3's trigger reads M2's
/// median of the same run (docs/phase-8a-design.md 10.3.4). A fact the list does not name runs after
/// them, by name.
/// </summary>
public sealed class Phase8aPerformanceOrder : ITestCaseOrderer
{
    /// <summary>The facts in their order.</summary>
    public static IReadOnlyList<string> Methods { get; } =
    [
        nameof(Phase8aPerformance.M2_start_up_with_one_tab),
        nameof(Phase8aPerformance.M3_start_up_with_ten_tabs),
        nameof(Phase8aPerformance.M4_M5_memory_and_the_reload_after_an_outside_commit),
        nameof(Phase8aPerformance.M6_the_reload_after_a_fetch),
        nameof(Phase8aPerformance.M7_pulls_settings_reads),
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
/// What the series' facts share in one run of the class: its file, the folder of its clones and
/// data folders, and M2's median for M3's trigger. Its constructor does nothing, so that the facts,
/// which skip without <see cref="PerformanceSeries.RunsVariable"/>, do no work and write no file
/// (docs/phase-8a-design.md 10.3.5): everything is made by the first fact that needs it, and the
/// folder is deleted when the class ends.
/// </summary>
public sealed class Phase8aPerformanceRun : IDisposable
{
    private static readonly Lazy<string> GitPath = new(() =>
        GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The series needs git."));

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _clones = new(StringComparer.Ordinal);
    private TempDirectory? _folder;
    private Task<PerformanceSeries>? _series;
    private int _dataFolders;

    /// <summary>M2's median of this run, once M2 has run, for M3's trigger.</summary>
    public double? StartUpMedian { get; set; }

    /// <summary>The folder of the clones and the data folders, made at the first call.</summary>
    private TempDirectory Folder => _folder ??= new TempDirectory("perf");

    /// <summary>This run's series file, started by the first fact that asks.</summary>
    public Task<PerformanceSeries> SeriesAsync(int runs) =>
        _series ??= PerformanceSeries.StartAsync(runs, TestContext.Current.CancellationToken);

    /// <summary>A new, empty data folder for one launch.</summary>
    public string NewDataDirectory()
    {
        var directory = Folder.Combine("data-" + (++_dataFolders).ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// The clone <paramref name="name"/> of <paramref name="source"/> in the class's folder, made at
    /// the first call: <c>git clone --local</c> with <c>gc.auto 0</c> and <c>maintenance.auto false</c>,
    /// as every scenario repository has them, so that git writes no commit-graph between the "before"
    /// and the "after" series (D47); bare with <paramref name="bare"/>. Its objects are copies, not
    /// links: deleting the folder clears each file's read-only attribute first, which on a link
    /// would clear the shared 100,000-commit repository's own, and that one is only read.
    /// </summary>
    public async Task<string> CloneAsync(string source, string name, bool bare = false)
    {
        await _gate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            var target = Folder.Combine(name);
            if (_clones.Contains(name))
            {
                return target;
            }

            var clone = new List<string> { "clone", "--quiet", "--local", "--no-hardlinks", "-c", "gc.auto=0", "-c", "maintenance.auto=false" };
            if (bare)
            {
                clone.Add("--bare");
            }

            await GitAsync(Folder.Path, [.. clone, source, target]);
            _clones.Add(name);
            return target;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Runs the harness's own git in <paramref name="workingDirectory"/> and returns its output,
    /// trimmed: cut off from the machine, and committing as <c>Perf Author &lt;perf@example.com&gt;</c>.
    /// </summary>
    public async Task<string> GitAsync(string workingDirectory, params string[] arguments)
    {
        var environment = new Dictionary<string, string?>(TempRepo.IsolatedEnvironment(Folder.Combine("home")))
        {
            ["GIT_AUTHOR_NAME"] = "Perf Author",
            ["GIT_AUTHOR_EMAIL"] = "perf@example.com",
            ["GIT_COMMITTER_NAME"] = "Perf Author",
            ["GIT_COMMITTER_EMAIL"] = "perf@example.com",
        };
        var command = new GitCommand(arguments) { WorkingDirectory = workingDirectory, Environment = environment };
        var result = await new GitRunner(GitPath.Value).RunAsync(command, TestContext.Current.CancellationToken);
        return result.EnsureSuccess(command).StandardOutput.Trim();
    }

    /// <summary>A runner of the tests' git with <paramref name="environment"/> on every call (M7's).</summary>
    public static GitRunner Runner(IReadOnlyDictionary<string, string?> environment) => new(GitPath.Value, environment: environment);

    public void Dispose()
    {
        _folder?.Dispose();
        _gate.Dispose();
    }
}

/// <summary>
/// The performance series (docs/phase-8a-design.md 10.3.4 and 10.3.5, D436 to D438): one fact per
/// measure, M2 to M7, each taken <see cref="PerformanceSeries.Runs"/> times in the Release build on
/// the real desktop, with the app's own log lines as the measure (D50). The facts skip unless
/// <see cref="PerformanceSeries.RunsVariable"/> is set, which only the lead does, on the rested
/// laptop with the owner told: they start the app many times and M6 clicks Fetch. Each writes its
/// lines into the series file (<see cref="PerformanceSeries"/>). M1, Q1, stays in phase 1's pass.
/// <para>
/// Every launch has a data folder of its own, the environment of <see cref="RealApp.Launch(string, SessionState, IReadOnlyDictionary{string, string?})"/>
/// without the watcher's lag (so that a shell left with it does not slow the series), and its window
/// as the app opens it. Before and after each run, each repository a fact measures must have no
/// commit-graph (D47). Each wait has its own limit, so that a run that hangs fails with its number.
/// </para>
/// </summary>
[TestCaseOrderer(typeof(Phase8aPerformanceOrder))]
public sealed class Phase8aPerformance(Phase8aPerformanceRun fixture) : IClassFixture<Phase8aPerformanceRun>
{
    private const string SkipReason = "VISUALCOMMIT_PERF_RUNS turns on the performance series; only the lead sets it, on the rested laptop.";

    /// <summary>The 100,000 commits of <see cref="LargeHistory"/>, which every measured repository opens with first.</summary>
    private const int Commits = LargeHistory.CommitCount;

    // The bounds (D436): null until the lead writes them after the "before" series, in a commit of
    // their own: the slowest counted run times 1.25, rounded up (PerformanceSeries.Bound): times to
    // 100 ms, M7's to 10 ms, memory to 10 MB. While null, a fact asserts only its guards.
    private static readonly long? M2BoundMilliseconds = null;
    private static readonly long? M3BoundMilliseconds = null;
    private static readonly long? M4LoadBoundMegabytes = null;
    private static readonly long? M4PeakBoundMegabytes = null;
    private static readonly long? M5BoundMilliseconds = null;
    private static readonly long? M6BoundMilliseconds = null;
    private static readonly long? M7BoundMilliseconds = null;

    /// <summary>The triggers of 10.3.4, read against the medians: M5's and M6's reload line above this.</summary>
    private const double ReloadTrigger = 2_000;

    /// <summary>M7's trigger: a median at this or more makes S8's batch of Pull's reads a candidate.</summary>
    private const double PullReadsTrigger = 250;

    /// <summary>M3's trigger: its median above this many times M2's.</summary>
    private const double TenTabsTrigger = 1.25;

    /// <summary>M7's calls of <see cref="IGitRepository.ReadPullConfigurationAsync"/> per run.</summary>
    private const int PullReadsPerRun = 20;

    private static readonly TimeSpan OneLoad = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan TenLoads = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan Reload = TimeSpan.FromSeconds(60);

    /// <summary>How long after a load or a reload M4 reads the memory, so that the app has settled.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    /// <summary>What every launch adds to <see cref="RealApp.Launch(string, SessionState, IReadOnlyDictionary{string, string?})"/>'s environment: no watcher's lag.</summary>
    private static readonly IReadOnlyDictionary<string, string?> LaunchEnvironment = new Dictionary<string, string?>
    {
        [GitRepository.WatcherLagVariable] = null,
    };

    [Fact]
    public async Task M2_start_up_with_one_tab()
    {
        var runs = RunsOrSkip();
        var series = await fixture.SeriesAsync(runs);
        await RecordFailureAsync(series, "M2", async () =>
        {
            var large = await LargeHistory.GetAsync();
            var name = Path.GetFileName(large);
            var values = new List<double>();
            for (var run = 0; run <= runs; run++)
            {
                // Run 0 is the uncounted warm-up: a first start after a build is the coldest.
                var label = run == 0 ? "M2 warm-up" : $"M2 run {run}";
                AssertNoCommitGraph(label, (name, GitFolder(large)));
                var data = fixture.NewDataDirectory();
                using (var app = RealApp.Launch(data, Session(large), LaunchEnvironment))
                {
                    WaitForLine(app, data, FirstRows(name, 1), OneLoad, $"{label}: the start-up's first-rows line");
                    WaitForLine(app, data, Loaded(name, Commits), OneLoad, $"{label}: the history's {Commits} commits loaded");
                    Assert.Equal(0, app.Close());
                }

                AssertNoCommitGraph(label, (name, GitFolder(large)));
                var firstRows = Number(data, FirstRows(name, 1), label);
                if (run == 0)
                {
                    series.Write(Invariant($"M2 warm-up: {firstRows} ms"));
                    continue;
                }

                values.Add(firstRows);
                series.Write(Invariant($"M2 run {run}: {firstRows} ms ({StartLines(data)})"));
            }

            series.Write(PerformanceSeries.Summary("M2", "ms", values, M2BoundMilliseconds));
            fixture.StartUpMedian = PerformanceSeries.Median(values);
            AssertHeld("M2", values, M2BoundMilliseconds, "ms");
        });
    }

    [Fact]
    public async Task M3_start_up_with_ten_tabs()
    {
        var runs = RunsOrSkip();
        var series = await fixture.SeriesAsync(runs);
        await RecordFailureAsync(series, "M3", async () =>
        {
            var large = await LargeHistory.GetAsync();
            var repositories = new List<(string Name, string Path)> { (Path.GetFileName(large), large) };
            for (var clone = 1; clone <= 9; clone++)
            {
                var name = Invariant($"large-clone-{clone}");
                repositories.Add((name, await fixture.CloneAsync(large, name)));
            }

            var gitFolders = repositories.Select(repository => (repository.Name, GitFolder(repository.Path))).ToArray();
            var active = repositories[0].Name;
            var values = new List<double>();
            for (var run = 0; run <= runs; run++)
            {
                var label = run == 0 ? "M3 warm-up" : $"M3 run {run}";
                AssertNoCommitGraph(label, gitFolders);
                var data = fixture.NewDataDirectory();
                double workingSet;
                var launched = Stopwatch.StartNew();
                using (var app = RealApp.Launch(data, Session([.. repositories.Select(repository => repository.Path)]), LaunchEnvironment))
                {
                    // The ten loads share one limit, from the launch.
                    TimeSpan Left() => TenLoads > launched.Elapsed ? TenLoads - launched.Elapsed : TimeSpan.Zero;
                    WaitForLine(app, data, FirstRows(active, 10), Left(), $"{label}: the start-up's first-rows line");
                    foreach (var (name, _) in repositories)
                    {
                        WaitForLine(app, data, Loaded(name, Commits), Left(), $"{label}: {name}'s {Commits} commits loaded");
                    }

                    workingSet = PerformanceSeries.Megabytes(app.Memory().WorkingSet);
                    Assert.Equal(0, app.Close());
                }

                AssertNoCommitGraph(label, gitFolders);
                var firstRows = Number(data, FirstRows(active, 10), label);
                if (run == 0)
                {
                    series.Write(Invariant($"M3 warm-up: {firstRows} ms"));
                    continue;
                }

                values.Add(firstRows);
                series.Write(Invariant($"M3 run {run}: {firstRows} ms ({StartLines(data)}; working set {PerformanceSeries.Format(workingSet)} MB after the ten loads)"));
            }

            series.Write(PerformanceSeries.Summary("M3", "ms", values, M3BoundMilliseconds));
            var median = PerformanceSeries.Median(values);
            series.Write(fixture.StartUpMedian is { } startUp
                ? Trigger("M3", Invariant($"median {PerformanceSeries.Format(median)} ms against {TenTabsTrigger} times M2's median, {PerformanceSeries.Format(TenTabsTrigger * startUp)} ms"), median > TenTabsTrigger * startUp)
                : "M3 trigger: not read here, since M2 did not run in this run of the class.");
            AssertHeld("M3", values, M3BoundMilliseconds, "ms");
        });
    }

    [Fact]
    public async Task M4_M5_memory_and_the_reload_after_an_outside_commit()
    {
        var runs = RunsOrSkip();
        var series = await fixture.SeriesAsync(runs);
        await RecordFailureAsync(series, "M4 and M5", async () =>
        {
            const string name = "large-reload";
            var clone = await fixture.CloneAsync(await LargeHistory.GetAsync(), name);
            var (afterLoads, peaks, reloads, reloadLines) = (new List<double>(), new List<double>(), new List<double>(), new List<double>());
            for (var run = 1; run <= runs; run++)
            {
                // Each run's commit stays, so run i opens with one commit more than the run before.
                var (before, after) = (Commits + run - 1, Commits + run);
                var label = $"M4 and M5 run {run}";
                AssertNoCommitGraph(label, (name, GitFolder(clone)));
                var data = fixture.NewDataDirectory();
                (long WorkingSet, long Private, long PeakWorkingSet) loaded, reloaded;
                DateTimeOffset committed;
                using (var app = RealApp.Launch(data, Session(clone), LaunchEnvironment))
                {
                    WaitForLine(app, data, Loaded(name, before), OneLoad, $"{label}: the history's {before} commits loaded");
                    Thread.Sleep(Settle);
                    loaded = app.Memory();

                    await fixture.GitAsync(clone, "commit", "--quiet", "--allow-empty", "-m", Invariant($"Perf run {run}"));
                    committed = DateTimeOffset.Now;
                    WaitForLine(app, data, Reloaded(name, after), Reload, $"{label}: the reload line with {after} commits");
                    Thread.Sleep(Settle);
                    reloaded = app.Memory();
                    Assert.Equal(0, app.Close());
                }

                AssertNoCommitGraph(label, (name, GitFolder(clone)));
                var lineTime = RealApp.LogTime(data, Reloaded(name, after))
                    ?? throw new InvalidOperationException($"{label}: the reload line is no longer in the log.");
                var reload = (lineTime - committed).TotalMilliseconds;
                var reloadLine = Number(data, Reloaded(name, after), label);
                var (afterLoad, peak) = (PerformanceSeries.Megabytes(loaded.WorkingSet), PerformanceSeries.Megabytes(reloaded.PeakWorkingSet));
                afterLoads.Add(afterLoad);
                peaks.Add(peak);
                reloads.Add(reload);
                reloadLines.Add(reloadLine);
                series.Write(Invariant(
                    $"M4 run {run}: {PerformanceSeries.Format(afterLoad)} MB working set after the load, {PerformanceSeries.Format(peak)} MB peak after the reload (private {PerformanceSeries.Format(PerformanceSeries.Megabytes(loaded.Private))} MB after the load and {PerformanceSeries.Format(PerformanceSeries.Megabytes(reloaded.Private))} MB after the reload; working set {PerformanceSeries.Format(PerformanceSeries.Megabytes(reloaded.WorkingSet))} MB after the reload; managed heap {Noted(data, Heap(name, before))} MB after loading {before} commits, {Noted(data, Heap(name, after))} MB after loading {after})"));
                series.Write(Invariant($"M5 run {run}: {PerformanceSeries.Format(reload)} ms (the reload line's {reloadLine} ms)"));
            }

            series.Write(PerformanceSeries.Summary("M4 after the load", "MB", afterLoads, M4LoadBoundMegabytes));
            series.Write(PerformanceSeries.Summary("M4 peak after the reload", "MB", peaks, M4PeakBoundMegabytes));
            var (peakMedian, loadMedian) = (PerformanceSeries.Median(peaks), PerformanceSeries.Median(afterLoads));
            series.Write(Trigger("M4", Invariant($"median peak after the reload {PerformanceSeries.Format(peakMedian)} MB against twice the median working set after the load, {PerformanceSeries.Format(2 * loadMedian)} MB"), peakMedian > 2 * loadMedian));
            series.Write(PerformanceSeries.Summary("M5", "ms", reloads, M5BoundMilliseconds));
            WriteReloadTrigger(series, "M5", reloadLines);
            AssertHeld("M4 after the load", afterLoads, M4LoadBoundMegabytes, "MB");
            AssertHeld("M4 peak after the reload", peaks, M4PeakBoundMegabytes, "MB");
            AssertHeld("M5", reloads, M5BoundMilliseconds, "ms");
        });
    }

    [Fact]
    public async Task M6_the_reload_after_a_fetch()
    {
        var runs = RunsOrSkip();
        var series = await fixture.SeriesAsync(runs);
        await RecordFailureAsync(series, "M6", async () =>
        {
            const string name = "large-fetch";
            var remote = await fixture.CloneAsync(await LargeHistory.GetAsync(), "large-remote.git", bare: true);
            var clone = await fixture.CloneAsync(remote, name);
            var measured = new[] { (name, GitFolder(clone)), ("large-remote.git", remote) };
            var (reloads, reloadLines) = (new List<double>(), new List<double>());
            for (var run = 1; run <= runs; run++)
            {
                var (before, after) = (Commits + run - 1, Commits + run);
                var label = $"M6 run {run}";

                // The remote gains a commit on main before the app opens the clone, which still has
                // the commits of the runs before; the click on Fetch brings it.
                var parent = await fixture.GitAsync(remote, "rev-parse", "refs/heads/main");
                var tree = await fixture.GitAsync(remote, "rev-parse", "refs/heads/main^{tree}");
                var commit = await fixture.GitAsync(remote, "commit-tree", tree, "-p", parent, "-m", Invariant($"Perf fetch {run}"));
                await fixture.GitAsync(remote, "update-ref", "refs/heads/main", commit, parent);

                AssertNoCommitGraph(label, measured);
                var data = fixture.NewDataDirectory();
                DateTimeOffset clicked;
                using (var app = RealApp.Launch(data, Session(clone), LaunchEnvironment))
                {
                    WaitForLine(app, data, Loaded(name, before), OneLoad, $"{label}: the history's {before} commits loaded");
                    Thread.Sleep(Settle);
                    var fetch = app.Find("FetchButton");
                    clicked = DateTimeOffset.Now;
                    app.ClickWithMouse(fetch);
                    WaitForLine(app, data, Reloaded(name, after), Reload, $"{label}: the reload line with {after} commits after the fetch");

                    // The fetch's span is only noted: a line that does not come is written "no line".
                    try
                    {
                        WaitForLine(app, data, FetchEnded(name), Reload, $"{label}: the fetch's end");
                    }
                    catch (TimeoutException)
                    {
                    }

                    Assert.Equal(0, app.Close());
                }

                AssertNoCommitGraph(label, measured);
                var lineTime = RealApp.LogTime(data, Reloaded(name, after))
                    ?? throw new InvalidOperationException($"{label}: the reload line is no longer in the log.");
                var reload = (lineTime - clicked).TotalMilliseconds;
                var reloadLine = Number(data, Reloaded(name, after), label);
                reloads.Add(reload);
                reloadLines.Add(reloadLine);
                series.Write(Invariant($"M6 run {run}: {PerformanceSeries.Format(reload)} ms (the reload line's {reloadLine} ms; Fetch succeeded after {Noted(data, FetchEnded(name))} ms)"));
            }

            series.Write(PerformanceSeries.Summary("M6", "ms", reloads, M6BoundMilliseconds));
            WriteReloadTrigger(series, "M6", reloadLines);
            AssertHeld("M6", reloads, M6BoundMilliseconds, "ms");
        });
    }

    [Fact]
    public async Task M7_pulls_settings_reads()
    {
        var runs = RunsOrSkip();
        var series = await fixture.SeriesAsync(runs);
        await RecordFailureAsync(series, "M7", async () =>
        {
            // No window: the scenario's main with no pull key, read as the Pull reads it before its fetch.
            using var repo = await Scenarios.RemoteSyncAsync();
            var git = await GitRepository.OpenAsync(Phase8aPerformanceRun.Runner(repo.Environment), repo.Path, TestContext.Current.CancellationToken);
            var values = new List<double>();
            for (var run = 1; run <= runs; run++)
            {
                var calls = new List<double>(PullReadsPerRun);
                for (var call = 0; call < PullReadsPerRun; call++)
                {
                    var watch = Stopwatch.StartNew();
                    await git.ReadPullConfigurationAsync("main", TestContext.Current.CancellationToken);
                    calls.Add(watch.Elapsed.TotalMilliseconds);
                }

                var median = PerformanceSeries.Median(calls);
                var (lowest, highest) = PerformanceSeries.Range(calls);
                values.Add(median);
                series.Write(Invariant($"M7 run {run}: {PerformanceSeries.Format(median)} ms (the median of {PullReadsPerRun} reads, from {PerformanceSeries.Format(lowest)} to {PerformanceSeries.Format(highest)} ms)"));
            }

            series.Write(PerformanceSeries.Summary("M7", "ms", values, M7BoundMilliseconds));
            var medianOfRuns = PerformanceSeries.Median(values);
            series.Write(Trigger("M7", Invariant($"median {PerformanceSeries.Format(medianOfRuns)} ms against {PullReadsTrigger} ms or more"), medianOfRuns >= PullReadsTrigger));
            AssertHeld("M7", values, M7BoundMilliseconds, "ms");
        });
    }

    /// <summary>The runs per measure, or the fact skips: without the variable it does no work and writes no file.</summary>
    private static int RunsOrSkip()
    {
        var runs = PerformanceSeries.Runs;
        Assert.SkipUnless(runs is not null, SkipReason);
        return runs.Value;
    }

    /// <summary>Runs a fact's body, and when it fails, writes the failure into the file first, so that the file says where the series stopped.</summary>
    private static async Task RecordFailureAsync(PerformanceSeries series, string measure, Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (Exception ex)
        {
            series.Write($"{measure} failed: {ex.GetType().Name}: {ex.Message.Split('\n')[0].TrimEnd('\r')}");
            throw;
        }
    }

    /// <summary>A session that restores a tab on each of <paramref name="repositories"/>, the first active.</summary>
    private static SessionState Session(params string[] repositories) =>
        new() { Tabs = [.. repositories.Select(repository => new TabState(repository))], ActiveTab = 0 };

    /// <summary>The git folder of a repository with a working tree.</summary>
    private static string GitFolder(string workingTree) => Path.Combine(workingTree, ".git");

    /// <summary>
    /// Fails when a repository has a commit-graph, <c>objects/info/commit-graph</c> or the folder
    /// <c>objects/info/commit-graphs/</c> (D47), naming the run, the repository and the file.
    /// </summary>
    private static void AssertNoCommitGraph(string label, params (string Name, string GitFolder)[] repositories)
    {
        foreach (var (name, gitFolder) in repositories)
        {
            var file = Path.Combine(gitFolder, "objects", "info", "commit-graph");
            var folder = Path.Combine(gitFolder, "objects", "info", "commit-graphs");
            Assert.False(File.Exists(file), $"{label}: {name} has a commit-graph, {file}; the series measures histories without one (D47).");
            Assert.False(Directory.Exists(folder), $"{label}: {name} has commit-graphs, {folder}; the series measures histories without one (D47).");
        }
    }

    /// <summary>The window's and Git's start-up lines of a launch, noted beside its measure.</summary>
    private static string StartLines(string data) =>
        $"window opened after {Noted(data, WindowOpened)} ms, Git found after {Noted(data, GitFound)} ms";

    /// <summary>Waits until a line of the app's log matches <paramref name="line"/>.</summary>
    private static void WaitForLine(RealApp app, string data, Regex line, TimeSpan timeout, string what) =>
        app.WaitFor(() => FindLine(data, line) is not null, what, timeout);

    /// <summary>The first line of the app's log that <paramref name="line"/> matches, or null.</summary>
    private static Match? FindLine(string data, Regex line) =>
        AppLogFile.ReadLines(data).Select(text => line.Match(text)).FirstOrDefault(match => match.Success);

    /// <summary>The number in the first group of the first log line that <paramref name="line"/> matches.</summary>
    private static long Number(string data, Regex line, string label)
    {
        var match = FindLine(data, line) ?? throw new InvalidOperationException($"{label}: the log has no line matching {line}.");
        return long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A value only noted beside a measure: the number in the first group of the first log line that
    /// <paramref name="line"/> matches, or "no line", so that a missing note is seen in the file and
    /// does not stop the series.
    /// </summary>
    private static string Noted(string data, Regex line) =>
        FindLine(data, line) is { } match ? match.Groups[1].Value : "no line";

    /// <summary>Fails when a bound is set and a counted run's value is above it.</summary>
    private static void AssertHeld(string measure, IReadOnlyList<double> values, long? bound, string unit)
    {
        if (bound is not { } limit)
        {
            return;
        }

        for (var run = 0; run < values.Count; run++)
        {
            Assert.True(values[run] <= limit, Invariant($"{measure} run {run + 1}: {PerformanceSeries.Format(values[run])} {unit}, above its bound of {limit} {unit}."));
        }
    }

    /// <summary>M5's and M6's trigger: the reload line's own time, its median above 2,000 ms (D437).</summary>
    private static void WriteReloadTrigger(PerformanceSeries series, string measure, IReadOnlyCollection<double> reloadLines)
    {
        var median = PerformanceSeries.Median(reloadLines);
        series.Write(Trigger(measure, Invariant($"the reload line's median {PerformanceSeries.Format(median)} ms against {ReloadTrigger} ms"), median > ReloadTrigger));
    }

    /// <summary>A trigger's line: what it compared, and whether it fired.</summary>
    private static string Trigger(string measure, string comparison, bool fired) =>
        $"{measure} trigger: {comparison}: {(fired ? "fired" : "not fired")}";

    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);

    // ---- The log lines the series reads (D438) ----

    private static readonly Regex WindowOpened = Line(@"Start-up: window opened after (\d+) ms since the process started\.");

    private static readonly Regex GitFound = Line(@"Start-up: Git found after (\d+) ms since the process started\.");

    /// <summary>The start-up's first-rows line of <paramref name="repository"/> after <paramref name="restored"/> restored tabs.</summary>
    private static Regex FirstRows(string repository, int restored) =>
        Line(Invariant($@"Start-up: first graph rows of {Regex.Escape(repository)} after (\d+) ms since the process started \({restored} restored {(restored == 1 ? "tab" : "tabs")}\)\."));

    /// <summary>The end of a load of <paramref name="commits"/> commits (the history's line, D50).</summary>
    private static Regex Loaded(string repository, int commits) =>
        Line(Invariant($@"{Regex.Escape(repository)}: loaded {commits} commits in (\d+) ms"));

    /// <summary>A swap after a change of the refs, to a graph of <paramref name="commits"/> commits; the group its own time.</summary>
    private static Regex Reloaded(string repository, int commits) =>
        Line(Invariant($@"{Regex.Escape(repository)}: graph reloaded after a change of the refs, {commits} commits in (\d+) ms\."));

    /// <summary>The managed heap after a load of <paramref name="commits"/> commits; the group its megabytes.</summary>
    private static Regex Heap(string repository, int commits) =>
        Line(Invariant($@"{Regex.Escape(repository)}: managed heap (\d+) MB after loading {commits} commits\."));

    /// <summary>The toolbar's Fetch ended with success (<see cref="AppLogFile.OperationEnded"/>'s line, its message after it); the group its milliseconds.</summary>
    private static Regex FetchEnded(string repository) =>
        Line(Invariant($@"{Regex.Escape(repository)}: Fetch: {OperationOutcome.Succeeded} after (\d+) ms(?:: .*)?"));

    /// <summary>A whole log entry's message after its time and level.</summary>
    private static Regex Line(string message) =>
        new(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{3} [+-]\d\d:\d\d \[[A-Z]+\] " + message + "$", RegexOptions.CultureInvariant);
}
