using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;

namespace VisualCommit.GuideShots;

/// <summary>
/// The demo data every guide picture shows (docs/phase-8a-design.md 11.3.5; ruling 63, D451):
/// repositories under the demo's root (<see cref="GuideSettings.Root"/>), built once per process
/// into <c>&lt;root&gt;/.template</c> and copied to the same paths before each start
/// (<see cref="FreshAsync"/>), so that every shot starts from the same files and a shot that
/// changes them changes no other.
/// <list type="bullet">
/// <item><c>Projects/weather-station</c>: 30 commits by <see cref="Alex"/>, <see cref="Sam"/> and
/// <see cref="Kim"/> at <c>example.com</c>, dated 2025-11-03 to 2025-12-19 UTC (before the app's own
/// commits, 2026-01-02); the branches <c>main</c> (checked out), <c>feature/forecast</c>,
/// <c>feature/units</c> (merged) and <c>fix/rounding</c>; the annotated tags <c>v1.0</c> and
/// <c>v1.1</c>; one stash; <c>origin</c> at <see cref="OriginUrl"/>, never fetched, its branches
/// written from the bare repository <c>remotes/weather-station.git</c>, its <c>main</c> one commit
/// behind <c>main</c> and one ahead of it; in the working tree two changed files
/// (<c>station/units.py</c> in two hunks, <c>README.md</c>) and a changed image
/// (<c>assets/icon.png</c>); its own <c>user.name</c> and <c>user.email</c>, Alex's.</item>
/// <item><c>Projects/recipe-box</c>: a small repository for the dashboard, in step with its
/// <c>origin</c> (<c>remotes/recipe-box.git</c>, shown as <c>github.com/demo--alex/recipe-box</c>),
/// with one changed file.</item>
/// <item>The app's data folder, <c>.config/VisualCommit</c>, empty at each start.</item>
/// </list>
/// A merge stopped on a conflict, and the other states a shot needs, are made on demand in the
/// copy (<see cref="StopMergeOnConflictAsync"/>). No shot fetches, pulls, pushes or clones.
/// </summary>
public sealed class GuideScenario
{
    /// <summary>The demo's GitHub owner and login: two hyphens in a row, a name GitHub cannot give out (D451).</summary>
    public const string Owner = "demo--alex";

    /// <summary>Another login the demo's fake of GitHub serves, of the same form.</summary>
    public const string SecondLogin = "demo--sam";

    public const string WeatherStationName = "weather-station";
    public const string RecipeBoxName = "recipe-box";

    /// <summary>The weather station's <c>origin</c>, which no shot reaches.</summary>
    public const string OriginUrl = "https://github.com/" + Owner + "/" + WeatherStationName + ".git";

    /// <summary>The recipe box's <c>origin</c>.</summary>
    public const string RecipeBoxOriginUrl = "https://github.com/" + Owner + "/" + RecipeBoxName + ".git";

    public static GuideAuthor Alex { get; } = new("Alex Demo", "alex@example.com");

    public static GuideAuthor Sam { get; } = new("Sam Example", "sam@example.com");

    public static GuideAuthor Kim { get; } = new("Kim Sample", "kim@example.com");

    /// <summary>The weather station's stash.</summary>
    public const string StashMessage = "Try a larger font";

    /// <summary>The working tree's changed text file with two hunks, which the diff shots open.</summary>
    public const string UnitsFile = "station/units.py";

    public const string ReadmeFile = "README.md";

    public const string IconFile = "assets/icon.png";

    private static readonly Lazy<Task<string>> Template = new(BuildTemplateAsync);

    private GuideScenario(string root)
    {
        Root = root;
    }

    /// <summary>The demo's root.</summary>
    public string Root { get; }

    /// <summary>The folder of the demo's working trees.</summary>
    public string Projects => Path.Combine(Root, "Projects");

    public string WeatherStation => Path.Combine(Projects, WeatherStationName);

    public string RecipeBox => Path.Combine(Projects, RecipeBoxName);

    /// <summary>The folder of the demo's bare repositories.</summary>
    public string Remotes => Path.Combine(Root, "remotes");

    /// <summary>The bare repository the weather station's remote branches were written from.</summary>
    public string WeatherStationBare => Path.Combine(Remotes, WeatherStationName + ".git");

    /// <summary>The app's data folder, empty at each start.</summary>
    public string DataDirectory => Path.Combine(Root, ".config", "VisualCommit");

    /// <summary>
    /// The demo's repositories, fresh: the template built once per process (its wait not counted
    /// against the shot, <see cref="HangWatchdogAttribute.SharedPreparationAsync{T}"/>), the shot's
    /// folders deleted and the template's copied to their places, and an empty data folder.
    /// </summary>
    /// <exception cref="InvalidOperationException">The demo's root is not set as it must be (<see cref="GuideSettings.RootRefusal"/>).</exception>
    public static async Task<GuideScenario> FreshAsync()
    {
        var root = GuideSettings.Root();
        var template = await HangWatchdogAttribute.SharedPreparationAsync("the guide's demo repositories", Template.Value).ConfigureAwait(false);
        var scenario = new GuideScenario(root);
        foreach (var folder in new[] { scenario.Projects, scenario.Remotes, Path.Combine(root, ".config") })
        {
            DeleteTree(folder);
        }

        CopyTree(Path.Combine(template, "Projects"), scenario.Projects);
        CopyTree(Path.Combine(template, "remotes"), scenario.Remotes);
        Directory.CreateDirectory(scenario.DataDirectory);
        return scenario;
    }

    /// <summary>
    /// Runs git in <paramref name="folder"/> of the copy, cut off from the machine, as
    /// <see cref="Alex"/> on 2025-12-19 at 12:00 UTC: for a state a shot makes before the app starts.
    /// </summary>
    public Task<GitResult> GitAsync(string folder, params string[] arguments) =>
        Git.RunAsync(folder, Alex, new DateTimeOffset(2025, 12, 19, 12, 0, 0, TimeSpan.Zero), arguments);

    /// <summary>
    /// The weather station with its working tree clean and a merge of <c>fix/rounding</c> into
    /// <c>main</c> stopped on a conflict in <see cref="UnitsFile"/>.
    /// </summary>
    public async Task StopMergeOnConflictAsync()
    {
        await GitAsync(WeatherStation, "reset", "--quiet", "--hard").ConfigureAwait(false);
        var merge = await Git.RunAllowingFailureAsync(WeatherStation, Alex, new DateTimeOffset(2025, 12, 19, 12, 0, 0, TimeSpan.Zero), "merge", "--no-edit", "fix/rounding").ConfigureAwait(false);
        if (merge.ExitCode == 0 || !File.Exists(Path.Combine(WeatherStation, ".git", "MERGE_HEAD")))
        {
            throw new InvalidOperationException("The merge of fix/rounding into main did not stop on a conflict:\n" + merge.StandardOutput + merge.StandardError);
        }
    }

    /// <summary>Deletes a folder and everything in it, read-only files (git's objects on Windows) included; nothing when it is missing.</summary>
    internal static void DeleteTree(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(folder, recursive: true);
    }

    private static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var directory in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)));
        }
    }

    /// <summary>Builds the template in <c>&lt;root&gt;/.template</c>, afresh, and returns its folder.</summary>
    private static async Task<string> BuildTemplateAsync()
    {
        var template = Path.Combine(GuideSettings.Root(), ".template");
        DeleteTree(template);
        Directory.CreateDirectory(template);
        await BuildWeatherStationAsync(template).ConfigureAwait(false);
        await BuildRecipeBoxAsync(template).ConfigureAwait(false);
        return template;
    }

    private static DateTimeOffset At(int month, int day, int hour, int minute) => new(2025, month, day, hour, minute, 0, TimeSpan.Zero);

    private static async Task BuildWeatherStationAsync(string template)
    {
        var bare = Path.Combine(template, "remotes", WeatherStationName + ".git");
        Directory.CreateDirectory(Path.GetDirectoryName(bare)!);
        var repo = await Builder.InitAsync(Path.Combine(template, "Projects", WeatherStationName)).ConfigureAwait(false);
        var files = WeatherStationFiles.Initial();

        await repo.CommitAsync(Alex, At(11, 3, 9, 12), "Start the weather station", files, "README.md", ".gitignore").ConfigureAwait(false);
        await repo.CommitAsync(Alex, At(11, 4, 10, 5), "Read the temperature sensor", files, "station/__init__.py", "station/sensors.py").ConfigureAwait(false);
        await repo.CommitAsync(Sam, At(11, 5, 14, 20), "Add unit conversions", files, UnitsFile).ConfigureAwait(false);
        await repo.CommitAsync(Sam, At(11, 6, 11, 2), "Test the unit conversions", files, "tests/test_units.py").ConfigureAwait(false);
        await repo.CommitAsync(Kim, At(11, 7, 16, 45), "Show readings on the display", files, "station/display.py").ConfigureAwait(false);
        repo.WriteBytes(IconFile, WeatherStationFiles.Icon(cloud: false));
        await repo.CommitAsync(Alex, At(11, 10, 9, 30), "Add the station's icon", files, IconFile).ConfigureAwait(false);
        await repo.CommitAsync(Kim, At(11, 11, 13, 15), "Document the set-up", files, "docs/setup.md").ConfigureAwait(false);
        files.ReadHumidity();
        await repo.CommitAsync(Alex, At(11, 12, 10, 40), "Read the humidity as well", files, "station/sensors.py").ConfigureAwait(false);
        files.RoundReadings();
        await repo.CommitAsync(Sam, At(11, 13, 15, 55), "Round readings to one decimal", files, UnitsFile).ConfigureAwait(false);
        files.Version("1.0");
        await repo.CommitAsync(Alex, At(11, 14, 9, 0), "Prepare release 1.0", files, "station/__init__.py").ConfigureAwait(false);
        await repo.TagAsync(Alex, At(11, 14, 9, 5), "v1.0", "Version 1.0").ConfigureAwait(false);
        await repo.CommitAsync(Kim, At(11, 17, 11, 30), "Add an example configuration", files, "config.example.toml").ConfigureAwait(false);

        // feature/units, merged into main.
        await repo.GitAsync(Alex, At(11, 17, 12, 0), "checkout", "--quiet", "-b", "feature/units").ConfigureAwait(false);
        files.SupportKelvin();
        await repo.CommitAsync(Sam, At(11, 18, 10, 10), "Support Kelvin", files, UnitsFile).ConfigureAwait(false);
        await repo.GitAsync(Alex, At(11, 18, 12, 0), "checkout", "--quiet", "main").ConfigureAwait(false);
        files.LogSensorErrors();
        await repo.CommitAsync(Alex, At(11, 19, 16, 20), "Log sensor errors", files, "station/sensors.py").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(11, 19, 17, 0), "checkout", "--quiet", "feature/units").ConfigureAwait(false);
        files.ConvertWindSpeeds();
        await repo.CommitAsync(Sam, At(11, 20, 14, 35), "Convert wind speeds", files, UnitsFile, "tests/test_units.py").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(11, 20, 15, 0), "checkout", "--quiet", "main").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(11, 21, 9, 45), "merge", "--quiet", "--no-ff", "-m", "Merge branch 'feature/units'", "feature/units").ConfigureAwait(false);

        files.RefreshEveryMinute();
        await repo.CommitAsync(Kim, At(11, 24, 13, 0), "Refresh the display every minute", files, "station/display.py").ConfigureAwait(false);
        files.FixHumidityRange();
        await repo.CommitAsync(Sam, At(11, 25, 10, 30), "Fix the humidity range", files, "station/sensors.py").ConfigureAwait(false);
        files.Version("1.1");
        await repo.CommitAsync(Alex, At(11, 26, 9, 0), "Prepare release 1.1", files, "station/__init__.py").ConfigureAwait(false);
        await repo.TagAsync(Alex, At(11, 26, 9, 5), "v1.1", "Version 1.1").ConfigureAwait(false);

        // feature/forecast, not merged; main goes on beside it. Each branch's commits write only
        // files the other does not change, so one set of files serves both.
        await repo.GitAsync(Alex, At(11, 26, 10, 0), "checkout", "--quiet", "-b", "feature/forecast").ConfigureAwait(false);
        files.SketchForecast();
        await repo.CommitAsync(Alex, At(11, 27, 11, 20), "Sketch the forecast module", files, "station/forecast.py").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(11, 27, 12, 0), "checkout", "--quiet", "main").ConfigureAwait(false);
        files.ShowPressureTrend();
        await repo.CommitAsync(Sam, At(12, 1, 14, 0), "Show the pressure trend", files, "station/display.py").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 1, 15, 0), "checkout", "--quiet", "feature/forecast").ConfigureAwait(false);
        files.PredictRain();
        await repo.CommitAsync(Kim, At(12, 2, 15, 10), "Predict rain from the pressure", files, "station/forecast.py").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 2, 16, 0), "checkout", "--quiet", "main").ConfigureAwait(false);
        files.UpdateReadme();
        await repo.CommitAsync(Kim, At(12, 3, 9, 30), "Update the readme", files, "README.md").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 3, 10, 0), "checkout", "--quiet", "feature/forecast").ConfigureAwait(false);
        await repo.CommitAsync(Alex, At(12, 4, 10, 0), "Test the forecast", files, "tests/test_forecast.py").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 4, 11, 0), "checkout", "--quiet", "main").ConfigureAwait(false);

        // fix/rounding changes the line that main changes later: merging it stops on a conflict.
        await repo.GitAsync(Alex, At(12, 4, 12, 0), "checkout", "--quiet", "-b", "fix/rounding").ConfigureAwait(false);
        var roundingFiles = files.Copy();
        roundingFiles.RoundHalfUp();
        await repo.CommitAsync(Sam, At(12, 5, 16, 0), "Round half up", roundingFiles, UnitsFile).ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 5, 17, 0), "checkout", "--quiet", "main").ConfigureAwait(false);

        files.CacheReadings();
        await repo.CommitAsync(Alex, At(12, 8, 10, 15), "Cache sensor readings", files, "station/sensors.py").ConfigureAwait(false);
        files.TidyDisplay();
        await repo.CommitAsync(Kim, At(12, 10, 11, 40), "Tidy the display code", files, "station/display.py").ConfigureAwait(false);
        files.RoundToConfiguredDigits();
        await repo.CommitAsync(Sam, At(12, 12, 15, 25), "Round to the configured digits", files, UnitsFile).ConfigureAwait(false);
        files.CheckWiring();
        await repo.CommitAsync(Alex, At(12, 15, 9, 50), "Check the sensor wiring at start", files, "station/sensors.py").ConfigureAwait(false);

        // origin/main: one commit main lacks.
        await repo.GitAsync(Alex, At(12, 17, 14, 0), "checkout", "--quiet", "-b", "origin-main").ConfigureAwait(false);
        var originFiles = files.Copy();
        await repo.CommitAsync(Sam, At(12, 17, 14, 10), "Add a French readme", originFiles.WithFrenchReadme(), "README.fr.md").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 17, 15, 0), "checkout", "--quiet", "main").ConfigureAwait(false);

        // main: one commit origin lacks.
        files.ShowBatteryLevel();
        await repo.CommitAsync(Alex, At(12, 19, 10, 30), "Show the battery level", files, "station/display.py").ConfigureAwait(false);

        // The remote's branches, written from the bare repository, then the remote renamed to GitHub's address.
        await Git.RunAsync(Path.Combine(template, "remotes"), Alex, At(12, 19, 10, 40), "init", "--quiet", "--bare", "--initial-branch", "main", bare).ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 19, 10, 41), "remote", "add", "origin", bare).ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 19, 10, 42), "push", "--quiet", "origin", "origin-main:refs/heads/main", "feature/units:refs/heads/feature/units", "feature/forecast:refs/heads/feature/forecast").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 19, 10, 43), "fetch", "--quiet", "origin").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 19, 10, 44), "remote", "set-url", "origin", OriginUrl).ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 19, 10, 45), "symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 19, 10, 46), "branch", "--quiet", "--set-upstream-to", "origin/main", "main").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 19, 10, 47), "branch", "--quiet", "--set-upstream-to", "origin/feature/forecast", "feature/forecast").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 19, 10, 48), "branch", "--quiet", "--set-upstream-to", "origin/feature/units", "feature/units").ConfigureAwait(false);
        await repo.GitAsync(Alex, At(12, 19, 10, 49), "branch", "--quiet", "-D", "origin-main").ConfigureAwait(false);

        // The stash, then the working tree's changes.
        repo.WriteText("station/display.py", files.Text("station/display.py").Replace("FONT_SIZE = 14", "FONT_SIZE = 18", StringComparison.Ordinal));
        await repo.GitAsync(Alex, At(12, 19, 11, 0), "stash", "push", "--quiet", "-m", StashMessage).ConfigureAwait(false);
        repo.WriteText(UnitsFile, WeatherStationFiles.UnitsInProgress(files.Text(UnitsFile)));
        repo.WriteText(ReadmeFile, files.Text(ReadmeFile) + WeatherStationFiles.ReadmeAddition);
        repo.WriteBytes(IconFile, WeatherStationFiles.Icon(cloud: true));
    }

    private static async Task BuildRecipeBoxAsync(string template)
    {
        var bare = Path.Combine(template, "remotes", RecipeBoxName + ".git");
        Directory.CreateDirectory(Path.GetDirectoryName(bare)!);
        var repo = await Builder.InitAsync(Path.Combine(template, "Projects", RecipeBoxName)).ConfigureAwait(false);
        repo.WriteText("README.md", "# Recipe box\n\nFamily recipes, one Markdown file each.\n");
        await repo.CommitStagedAsync(Kim, At(11, 6, 18, 0), "Start the recipe box", "README.md").ConfigureAwait(false);
        repo.WriteText("recipes/pancakes.md", "# Pancakes\n\n- 200 g flour\n- 2 eggs\n- 300 ml milk\n");
        await repo.CommitStagedAsync(Kim, At(11, 9, 10, 0), "Add pancakes", "recipes/pancakes.md").ConfigureAwait(false);
        repo.WriteText("recipes/lentil-soup.md", "# Lentil soup\n\n- 250 g red lentils\n- 1 onion\n- 1 l stock\n");
        await repo.CommitStagedAsync(Sam, At(11, 23, 19, 30), "Add lentil soup", "recipes/lentil-soup.md").ConfigureAwait(false);
        repo.WriteText("recipes/pancakes.md", "# Pancakes\n\n- 200 g flour\n- 2 eggs\n- 300 ml milk\n- A pinch of salt\n");
        await repo.CommitStagedAsync(Kim, At(12, 7, 9, 15), "Salt the pancakes", "recipes/pancakes.md").ConfigureAwait(false);
        await Git.RunAsync(Path.Combine(template, "remotes"), Kim, At(12, 7, 9, 20), "init", "--quiet", "--bare", "--initial-branch", "main", bare).ConfigureAwait(false);
        await repo.GitAsync(Kim, At(12, 7, 9, 21), "remote", "add", "origin", bare).ConfigureAwait(false);
        await repo.GitAsync(Kim, At(12, 7, 9, 22), "push", "--quiet", "--set-upstream", "origin", "main").ConfigureAwait(false);
        await repo.GitAsync(Kim, At(12, 7, 9, 23), "remote", "set-url", "origin", RecipeBoxOriginUrl).ConfigureAwait(false);
        await repo.GitAsync(Kim, At(12, 7, 9, 24), "symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main").ConfigureAwait(false);
        repo.WriteText("recipes/lentil-soup.md", "# Lentil soup\n\n- 250 g red lentils\n- 1 onion\n- 1 l stock\n- A squeeze of lemon\n");
    }

    /// <summary>Runs git cut off from the machine (<see cref="TempRepo.IsolatedEnvironment"/>), with an author and a date of the demo's.</summary>
    private static class Git
    {
        private static readonly Lazy<string> Executable = new(() =>
            GitLocator.FindExecutable(GitSearchContext.FromSystem())
            ?? throw new InvalidOperationException("The guide's pictures need git, and no git executable was found."));

        private static readonly Lazy<IReadOnlyDictionary<string, string?>> Isolated = new(() =>
        {
            var home = new TempDirectory("guide-git-home");
            AppDomain.CurrentDomain.ProcessExit += (_, _) => home.Dispose();
            return TempRepo.IsolatedEnvironment(home.Path);
        });

        public static async Task<GitResult> RunAsync(string folder, GuideAuthor author, DateTimeOffset date, params string[] arguments)
        {
            var command = Command(folder, author, date, arguments);
            var result = await new GitRunner(Executable.Value).RunAsync(command).ConfigureAwait(false);
            return result.EnsureSuccess(command);
        }

        public static Task<GitResult> RunAllowingFailureAsync(string folder, GuideAuthor author, DateTimeOffset date, params string[] arguments) =>
            new GitRunner(Executable.Value).RunAsync(Command(folder, author, date, arguments));

        private static GitCommand Command(string folder, GuideAuthor author, DateTimeOffset date, string[] arguments)
        {
            var time = date.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
            var environment = new Dictionary<string, string?>(Isolated.Value)
            {
                ["GIT_AUTHOR_NAME"] = author.Name,
                ["GIT_AUTHOR_EMAIL"] = author.Email,
                ["GIT_COMMITTER_NAME"] = author.Name,
                ["GIT_COMMITTER_EMAIL"] = author.Email,
                ["GIT_AUTHOR_DATE"] = time,
                ["GIT_COMMITTER_DATE"] = time,
            };
            return new GitCommand(arguments) { WorkingDirectory = folder, Environment = environment };
        }
    }

    /// <summary>One demo repository being built.</summary>
    private sealed class Builder
    {
        private Builder(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static async Task<Builder> InitAsync(string path)
        {
            Directory.CreateDirectory(path);
            var builder = new Builder(path);
            var date = At(11, 1, 9, 0);
            await builder.GitAsync(Alex, date, "init", "--quiet", "--initial-branch", "main").ConfigureAwait(false);
            foreach (var (key, value) in new[]
            {
                ("core.autocrlf", "false"),
                ("commit.gpgsign", "false"),
                ("tag.gpgsign", "false"),
                ("gc.auto", "0"),
                ("maintenance.auto", "false"),
                ("user.name", Alex.Name),
                ("user.email", Alex.Email),
            })
            {
                await builder.GitAsync(Alex, date, "config", key, value).ConfigureAwait(false);
            }

            return builder;
        }

        public Task<GitResult> GitAsync(GuideAuthor author, DateTimeOffset date, params string[] arguments) =>
            Git.RunAsync(Path, author, date, arguments);

        public void WriteText(string relative, string text) => WriteBytes(relative, System.Text.Encoding.UTF8.GetBytes(text));

        public void WriteBytes(string relative, byte[] bytes)
        {
            var file = System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, bytes);
        }

        /// <summary>Writes the named files from <paramref name="files"/> and commits them.</summary>
        public async Task CommitAsync(GuideAuthor author, DateTimeOffset date, string message, WeatherStationFiles files, params string[] paths)
        {
            foreach (var path in paths)
            {
                if (files.Has(path))
                {
                    WriteText(path, files.Text(path));
                }
            }

            await CommitStagedAsync(author, date, message, paths).ConfigureAwait(false);
        }

        public async Task CommitStagedAsync(GuideAuthor author, DateTimeOffset date, string message, params string[] paths)
        {
            await GitAsync(author, date, ["add", "--", .. paths]).ConfigureAwait(false);
            await GitAsync(author, date, "commit", "--quiet", "-m", message).ConfigureAwait(false);
        }

        public Task TagAsync(GuideAuthor author, DateTimeOffset date, string name, string message) =>
            GitAsync(author, date, "tag", "--annotate", "-m", message, name);
    }
}

/// <summary>An author of the demo's commits, at <c>example.com</c>.</summary>
public sealed record GuideAuthor(string Name, string Email);
