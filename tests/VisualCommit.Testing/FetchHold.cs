using System.Text;

namespace VisualCommit.Testing;

/// <summary>
/// Holds the fetches of chosen repositories from their file remotes until a test lets them go
/// (docs/phase-6-design.md 17.2): a shell script that git runs as the remote's upload-pack
/// (<c>remote.origin.uploadpack</c>, appended to each held repository's own configuration), which
/// waits for a file <c>release</c> before it hands over to <c>git upload-pack</c>. Every fetch of a
/// held repository waits from its start, new commits or not, and shows running without a
/// percentage; an HTTP remote ignores the key. Nothing of it is ever in the session's global
/// configuration, where it would give every repository a remote <c>origin</c> (0.10).
/// <para>
/// The script and its files lie in <c>&lt;data&gt;/harness/</c>: <c>hold-upload-pack.sh</c>, a
/// marker <c>held-&lt;remote folder&gt;.&lt;pid&gt;</c> per waiting fetch, and <c>release</c>, which
/// the script finds beside itself, so that no path goes inside it. With <c>release</c> there it
/// runs <c>git upload-pack</c> at once and makes no marker; else it makes its marker, waits at most
/// <see cref="Rounds"/> rounds of <c>sleep 0.5</c> for <c>release</c>, removes its marker and goes
/// on. A marker so means a fetch waiting now, or one a cancel killed while it waited, whose marker
/// stays: <see cref="Arm"/> takes the markers there as its baseline. A held fetch gives up after
/// its rounds (each <c>sleep</c> a process on Windows: 89 s measured in Git 2.36.0's <c>sh</c> on
/// the development machine with two other builds at work, against the design's estimate of 60 to
/// 70 s), below the hang watchdog's limit, so a test that waits for <see cref="Held"/> releases
/// well within about 60 s or the hold no longer holds: a fetch that gave up leaves a marker
/// <c>gave-up-&lt;remote folder&gt;.&lt;pid&gt;</c>, on which <see cref="WaitForHeldAsync"/>,
/// <see cref="Release"/> and <see cref="Dispose"/> fail, so that a test never passes on a hold that
/// held nothing. Disposing releases, so that a cancelled fetch's shell ends (call it in a
/// <c>finally</c>, as <see cref="Scenarios.ReleaseWaitingMergeTool"/>).
/// </para>
/// </summary>
public sealed class FetchHold : IDisposable
{
    /// <summary>The rounds of <c>sleep 0.5</c> a held fetch waits at most for its release.</summary>
    public const int Rounds = 120;

    /// <summary>The script's file name in the harness folder.</summary>
    public const string ScriptName = "hold-upload-pack.sh";

    /// <summary>The file whose presence lets every held fetch go.</summary>
    public const string ReleaseName = "release";

    /// <summary>The start of a waiting fetch's marker's name.</summary>
    public const string MarkerPrefix = "held-";

    /// <summary>
    /// The start of the marker a held fetch leaves when it gives up (its rounds over without a
    /// release): <c>gave-up-&lt;remote folder&gt;.&lt;pid&gt;</c>. <see cref="WaitForHeldAsync"/>,
    /// <see cref="Release"/> and <see cref="Dispose"/> fail on one.
    /// </summary>
    public const string GaveUpPrefix = "gave-up-";

    /// <summary>The script with <see cref="Rounds"/> rounds, as <see cref="Install"/> writes it by default.</summary>
    public static string Script { get; } = ScriptFor(Rounds);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock _gate = new();
    private HashSet<string> _baseline = [];

    private FetchHold(string folder, int rounds)
    {
        Folder = folder;
        ScriptPath = Path.Combine(folder, ScriptName);
        HeldFor = TimeSpan.FromSeconds(rounds * 0.5);
    }

    /// <summary>
    /// The script for <paramref name="rounds"/> rounds of <c>sleep 0.5</c>. <c>$0</c> is its own
    /// path and <c>$1</c> the remote's folder, as git hands an upload-pack command; its files are
    /// found beside it by parameter expansion, which starts no process. A fetch whose rounds run
    /// out before the release leaves a <see cref="GaveUpPrefix"/> marker and goes on.
    /// </summary>
    public static string ScriptFor(int rounds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rounds);
        return
            "#!/bin/sh\n" +
            "# docs/phase-6-design.md 17.2: holds this fetch's upload-pack until the file release is beside the script.\n" +
            "here=\"${0%/*}\"\n" +
            "if [ ! -e \"$here/release\" ]; then\n" +
            "  remote=\"${1%/}\"\n" +
            "  marker=\"$here/held-${remote##*/}.$$\"\n" +
            "  : > \"$marker\"\n" +
            "  rounds=0\n" +
            "  while [ ! -e \"$here/release\" ] && [ \"$rounds\" -lt " + rounds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ]; do\n" +
            "    sleep 0.5\n" +
            "    rounds=$((rounds + 1))\n" +
            "  done\n" +
            "  rm -f \"$marker\"\n" +
            "  if [ ! -e \"$here/release\" ]; then\n" +
            "    : > \"$here/gave-up-${remote##*/}.$$\"\n" +
            "  fi\n" +
            "fi\n" +
            "exec git upload-pack \"$1\"\n";
    }

    /// <summary>How long a held fetch waits at most for its release: its rounds' sleeps.</summary>
    public TimeSpan HeldFor { get; }

    /// <summary>The harness folder the script and its files lie in: <c>&lt;data&gt;/harness</c>.</summary>
    public string Folder { get; }

    /// <summary>The script's path.</summary>
    public string ScriptPath { get; }

    /// <summary>
    /// The value <see cref="HoldIn"/> gives <c>remote.origin.uploadpack</c>: <c>sh '&lt;script&gt;'</c>,
    /// the path with forward slashes in single quotes, as <c>SequenceTestSupport.ShellQuote</c>
    /// writes a path for <c>sh</c>: a backslash in the value would break every git call that reads
    /// the file (0.10).
    /// </summary>
    public string UploadPackCommand => "sh '" + ScriptPath.Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    /// <summary>
    /// The markers made since <see cref="Arm"/>: one per fetch that waits now, and one per fetch a
    /// cancel killed while it waited.
    /// </summary>
    public int Held
    {
        get
        {
            var markers = Markers();
            lock (_gate)
            {
                return markers.Count(marker => !_baseline.Contains(marker));
            }
        }
    }

    /// <summary>
    /// Writes the script into <c>&lt;data&gt;/harness/</c> (made if missing, so that a check may
    /// install it before the app starts on the data folder) and arms it (<see cref="Arm"/>). Hold a
    /// repository with <see cref="HoldIn"/>.
    /// </summary>
    public static FetchHold Install(string dataDirectory, int rounds = Rounds)
    {
        var folder = GitIsolation.SessionHarnessFolder(dataDirectory);
        Directory.CreateDirectory(folder);
        var hold = new FetchHold(folder, rounds);
        File.WriteAllText(hold.ScriptPath, ScriptFor(rounds), Utf8);
        hold.Arm();
        return hold;
    }

    /// <summary>
    /// Holds every fetch of each of <paramref name="repositoryFolders"/> (working trees) from its
    /// remote <c>origin</c>, by appending <c>[remote "origin"]</c> with <c>uploadpack =</c>
    /// <see cref="UploadPackCommand"/> as text to the repository's own <c>.git/config</c>: no git
    /// process, and git merges the second header with the first (0.10). Call it before the app
    /// starts, so that its git-folder watcher causes no extra read.
    /// </summary>
    /// <exception cref="InvalidOperationException">A repository's configuration has no <c>[remote "origin"]</c> with a <c>url</c>, so it cannot be held (<c>notes</c>).</exception>
    public void HoldIn(params string[] repositoryFolders)
    {
        ArgumentNullException.ThrowIfNull(repositoryFolders);
        var configs = repositoryFolders.Select(folder => Path.Combine(folder, ".git", "config")).ToList();
        foreach (var config in configs)
        {
            if (!File.Exists(config) || !HasOriginUrl(File.ReadAllText(config)))
            {
                throw new InvalidOperationException($"{config} has no [remote \"origin\"] with a url: there is no fetch from origin to hold.");
            }
        }

        foreach (var config in configs)
        {
            File.AppendAllText(config, "[remote \"origin\"]\n\tuploadpack = " + UploadPackCommand + "\n", Utf8);
        }
    }

    /// <summary>
    /// Holds again after a release: deletes <c>release</c> and takes the markers there now as the
    /// baseline, so that <see cref="Held"/> and <see cref="WaitForHeldAsync"/> count only the fetches
    /// that wait from now on. <see cref="Install"/> arms.
    /// </summary>
    public void Arm()
    {
        File.Delete(Path.Combine(Folder, ReleaseName));
        var markers = Markers();
        lock (_gate)
        {
            _baseline = [.. markers];
        }
    }

    /// <summary>Waits until at least <paramref name="count"/> fetches wait (<see cref="Held"/>), looking every 50 ms; at most <paramref name="timeout"/> (60 s when null, about a held fetch's patience).</summary>
    /// <exception cref="TimeoutException">Fewer fetches wait when the time is up.</exception>
    /// <exception cref="InvalidOperationException">A held fetch gave up (<see cref="GaveUpPrefix"/>).</exception>
    public async Task WaitForHeldAsync(int count, TimeSpan? timeout = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var limit = timeout ?? TimeSpan.FromSeconds(60);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (Held < count)
        {
            ThrowIfGaveUp();
            if (clock.Elapsed >= limit)
            {
                throw new TimeoutException($"Timed out after {limit.TotalSeconds:0.###} s waiting for {count} held fetches; {Held} wait in {Folder}.");
            }

            // No ConfigureAwait(false): a headless check goes on on its UI thread.
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        ThrowIfGaveUp();
    }

    /// <summary>
    /// Lets every held fetch go, and every later one through at once until <see cref="Arm"/>: writes
    /// <c>release</c>; then fails if a held fetch had given up before (the hold held nothing any more).
    /// </summary>
    /// <exception cref="InvalidOperationException">A held fetch gave up (<see cref="GaveUpPrefix"/>).</exception>
    public void Release()
    {
        File.WriteAllText(Path.Combine(Folder, ReleaseName), string.Empty);
        ThrowIfGaveUp();
    }

    /// <summary>Releases (<see cref="Release"/>), if the harness folder is still there, and so fails as it does when a held fetch gave up.</summary>
    public void Dispose()
    {
        if (Directory.Exists(Folder))
        {
            Release();
        }
    }

    /// <summary>The markers of held fetches that gave up, in the harness folder now.</summary>
    public IReadOnlyList<string> GaveUp() =>
        Directory.Exists(Folder)
            ? [.. Directory.EnumerateFiles(Folder, GaveUpPrefix + "*").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)]
            : [];

    private void ThrowIfGaveUp()
    {
        if (GaveUp() is { Count: > 0 } markers)
        {
            throw new InvalidOperationException(
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"The hold gave up: the steps between hold and release took over {HeldFor.TotalSeconds:0.###} s ({string.Join(", ", markers)} in {Folder})."));
        }
    }

    /// <summary>The names of the markers in the harness folder now.</summary>
    private List<string> Markers() =>
        Directory.Exists(Folder)
            ? [.. Directory.EnumerateFiles(Folder, MarkerPrefix + "*").Select(Path.GetFileName).OfType<string>()]
            : [];

    /// <summary>Whether a configuration file's text has a <c>[remote "origin"]</c> section with a <c>url</c> key.</summary>
    private static bool HasOriginUrl(string config)
    {
        var inOrigin = false;
        foreach (var raw in config.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inOrigin = line.Replace(" ", string.Empty, StringComparison.Ordinal).StartsWith("[remote\"origin\"]", StringComparison.Ordinal);
                continue;
            }

            if (inOrigin && line.StartsWith("url", StringComparison.OrdinalIgnoreCase) && line[3..].TrimStart().StartsWith('='))
            {
                return true;
            }
        }

        return false;
    }
}
