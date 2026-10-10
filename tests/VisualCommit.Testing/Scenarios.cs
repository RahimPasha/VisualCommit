namespace VisualCommit.Testing;

/// <summary>
/// The scenario repos: small repositories with known content that tests and visual checks start
/// from. Each method builds a fresh copy. Because <see cref="TempRepo"/> fixes authors and times,
/// a scenario has the same commit SHAs on every run and every platform.
/// <para>
/// To add one: write a method here that builds the repo step by step, and a test in
/// <c>VisualCommit.Git.Tests/ScenarioTests.cs</c> that pins down what it contains.
/// </para>
/// </summary>
public static class Scenarios
{
    /// <summary>
    /// Three commits in a line on <c>main</c>:
    /// "Add README" (README.md), "Add greeting" (src/greeting.txt), "Describe the project" (README.md changed).
    /// The working tree is clean.
    /// </summary>
    public static async Task<TempRepo> LinearAsync()
    {
        var repo = await TempRepo.CreateAsync("linear");
        try
        {
            await repo.CommitFileAsync("README.md", "# Sample\n", "Add README");
            await repo.CommitFileAsync("src/greeting.txt", "Hello\n", "Add greeting");
            await repo.CommitFileAsync("README.md", "# Sample\n\nA repository for tests.\n", "Describe the project");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The graph scenario of phase 1: branches in folders, a merge, an annotated and a
    /// lightweight tag, a local bare remote with ahead and behind counts, a remote branch the
    /// local branch lacks, and a stash. HEAD is on a clean <c>main</c>. Commits are made one
    /// minute apart from 2026-01-01 12:00 UTC, in this order:
    /// <list type="number">
    /// <item>12:00 "Initial commit" on main (README.md)</item>
    /// <item>12:01 "Add app skeleton" (src/app.txt, docs/notes.txt), tagged <c>v0.1</c> (annotated, "Version 0.1")</item>
    /// <item>12:02 "Add login form" on feature/login (src/login.txt)</item>
    /// <item>12:03 "Validate passwords" on feature/login (src/login.txt, src/validation/rules.txt)</item>
    /// <item>12:04 "Update README" on main</item>
    /// <item>12:05 "Merge branch 'feature/login'" on main, merging feature/login</item>
    /// <item>12:06 "Add search box" on feature/search (src/search.txt); pushed: origin/feature/search</item>
    /// <item>12:07 "Fix crash on start" on bugfix/crash-on-start (src/app.txt)</item>
    /// <item>12:08 "Bump version" on main (VERSION), tagged <c>v0.2</c> (lightweight); main pushed</item>
    /// <item>12:09 "Fix typo in docs" (docs/notes.txt), made on a detached HEAD and pushed as origin/main only</item>
    /// <item>12:10 "Add settings page" on main, with a body: adds src/settings/defaults.txt and
    /// src/settings/page.txt, changes README.md, deletes docs/notes.txt, renames src/app.txt to src/main-app.txt</item>
    /// <item>12:11 "Highlight matches" on feature/search (src/search.txt)</item>
    /// <item>12:12 stash@{0} "On main: Work in progress on README" (README.md changed)</item>
    /// </list>
    /// main tracks origin/main (1 ahead, 1 behind); feature/search tracks origin/feature/search
    /// (1 ahead). The bare remote is <c>origin.git</c> next to the working tree.
    /// <para>
    /// Building it runs git about 60 times, so it is built once per test process and every call
    /// returns a fresh copy (<see cref="TempRepo.CopyAsync"/>), which a test may change freely.
    /// </para>
    /// </summary>
    public static async Task<TempRepo> GraphAsync()
    {
        var template = await GraphTemplate.Value.ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> GraphTemplate = new(() => KeepUntilExit(BuildGraphAsync()));

    /// <summary>
    /// The changes scenario of phase 2: two commits on <c>main</c> and a working tree with a
    /// change of every kind the diff view shows. Commits, by "Test Author" from 2026-01-01 12:00
    /// UTC: 12:00 "Initial commit" (README.md), 12:01 "Add calculator" (the other committed
    /// files below). Then:
    /// <list type="bullet">
    /// <item>Staged: README.md (four lines added: a "## Usage" section), config/settings.json
    /// (added), src/util.py renamed to src/helpers.py (its first line changed).</item>
    /// <item>Unstaged: README.md (its third line changed), src/Calculator.cs (two hunks: a line
    /// changed in Add, and two methods added before the last line), docs/guide.md (untracked),
    /// docs/old-notes.txt (deleted), assets/logo.png (48×48 indigo becomes 64×48 green, each
    /// with a white 16×16 square in the middle), data/blob.bin (256 bytes become 320),
    /// data/large.txt (all 30,000 lines changed: a diff of 60,000 lines).</item>
    /// </list>
    /// Built once per test process; every call returns a fresh copy.
    /// </summary>
    public static async Task<TempRepo> ChangesAsync()
    {
        var template = await ChangesTemplate.Value.ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> ChangesTemplate = new(() => KeepUntilExit(BuildChangesAsync()));

    /// <summary>The changes scenario's files, as committed and as changed. Tests and the visual checks' expected results refer to them.</summary>
    public static class ChangesFiles
    {
        public const string ReadmeCommitted = "# Calculator\n\nA small calculator for tests.\n";

        public const string ReadmeStaged = "# Calculator\n\nA small calculator for tests.\n\n## Usage\n\nCreate a Calculator and call Add.\n";

        public const string ReadmeWorking = "# Calculator\n\nA small calculator for the visual checks.\n\n## Usage\n\nCreate a Calculator and call Add.\n";

        public const string CalculatorCommitted =
            "namespace Demo;\n" +
            "\n" +
            "/// <summary>A calculator for the tests.</summary>\n" +
            "public sealed class Calculator\n" +
            "{\n" +
            "    private readonly int _offset;\n" +
            "\n" +
            "    public Calculator(int offset)\n" +
            "    {\n" +
            "        _offset = offset;\n" +
            "    }\n" +
            "\n" +
            "    public int Add(int a, int b)\n" +
            "    {\n" +
            "        return a + b;\n" +
            "    }\n" +
            "\n" +
            "    public int Subtract(int a, int b)\n" +
            "    {\n" +
            "        return a - b;\n" +
            "    }\n" +
            "\n" +
            "    public int Multiply(int a, int b)\n" +
            "    {\n" +
            "        return a * b;\n" +
            "    }\n" +
            "\n" +
            "    public int Divide(int a, int b)\n" +
            "    {\n" +
            "        return a / b;\n" +
            "    }\n" +
            "}\n";

        public static string CalculatorWorking { get; } = CalculatorCommitted
            .Replace("        return a + b;\n", "        return a + b + _offset;\n", StringComparison.Ordinal)
            .Replace(
                "        return a / b;\n    }\n}\n",
                "        return a / b;\n    }\n\n    public int Negate(int a) => -a;\n\n    public int Square(int a) => a * a;\n}\n",
                StringComparison.Ordinal);

        public const string UtilCommitted =
            "\"\"\"Small helpers for the calculator.\"\"\"\n" +
            "\n" +
            "\n" +
            "def clamp(value, low, high):\n" +
            "    \"\"\"Keep value between low and high.\"\"\"\n" +
            "    return max(low, min(value, high))\n" +
            "\n" +
            "\n" +
            "def lerp(a, b, t):\n" +
            "    \"\"\"Blend a and b by t.\"\"\"\n" +
            "    return a + (b - a) * t\n";

        public static string HelpersStaged { get; } = UtilCommitted.Replace(
            "\"\"\"Small helpers for the calculator.\"\"\"",
            "\"\"\"Helpers shared by the calculator.\"\"\"",
            StringComparison.Ordinal);

        public const string SettingsStaged = "{\n  \"offset\": 2,\n  \"precision\": 4\n}\n";

        public const string GuideWorking = "# Guide\n\nAdd numbers with Add.\nSubtract them with Subtract.\n";

        public const string OldNotesCommitted = "Old notes.\nThey are out of date.\n";

        /// <summary>The number of lines of data/large.txt, in both versions.</summary>
        public const int LargeLines = 30_000;

        public static string LargeCommitted { get; } = Large("Line");

        public static string LargeWorking { get; } = Large("Row");

        public static TestImages.Rgb LogoCommittedColour { get; } = TestImages.Rgb.Parse("#4353D8");

        public static TestImages.Rgb LogoWorkingColour { get; } = TestImages.Rgb.Parse("#1E8E5A");

        public static TestImages.Rgb White { get; } = new(255, 255, 255);

        public static byte[] LogoCommitted { get; } = TestImages.SquareOn(48, 48, LogoCommittedColour, White, 16);

        public static byte[] LogoWorking { get; } = TestImages.SquareOn(64, 48, LogoWorkingColour, White, 16);

        /// <summary>The bytes 0 to 255.</summary>
        public static byte[] BlobCommitted { get; } = [.. Enumerable.Range(0, 256).Select(value => (byte)value)];

        /// <summary>The bytes 0 to 255, then 64 bytes of 0xFF.</summary>
        public static byte[] BlobWorking { get; } = [.. BlobCommitted, .. Enumerable.Repeat((byte)0xFF, 64)];

        private static string Large(string word)
        {
            var text = new System.Text.StringBuilder(LargeLines * 32);
            for (var line = 1; line <= LargeLines; line++)
            {
                text.Append(word).Append(' ').Append(line.ToString("D5", System.Globalization.CultureInfo.InvariantCulture)).Append(" of the large file.\n");
            }

            return text.ToString();
        }
    }

    private static async Task<TempRepo> BuildChangesAsync()
    {
        var repo = await TempRepo.CreateAsync("changes");
        try
        {
            await repo.CommitFileAsync("README.md", ChangesFiles.ReadmeCommitted, "Initial commit");
            await repo
                .WriteFile("src/Calculator.cs", ChangesFiles.CalculatorCommitted)
                .WriteFile("src/util.py", ChangesFiles.UtilCommitted)
                .WriteFile("docs/old-notes.txt", ChangesFiles.OldNotesCommitted)
                .WriteFile("data/large.txt", ChangesFiles.LargeCommitted)
                .WriteBytes("assets/logo.png", ChangesFiles.LogoCommitted)
                .WriteBytes("data/blob.bin", ChangesFiles.BlobCommitted)
                .CommitAsync("Add calculator");

            // Staged changes.
            repo.WriteFile("README.md", ChangesFiles.ReadmeStaged).WriteFile("config/settings.json", ChangesFiles.SettingsStaged);
            await repo.GitAsync("mv", "src/util.py", "src/helpers.py");
            repo.WriteFile("src/helpers.py", ChangesFiles.HelpersStaged);
            await repo.GitAsync("add", "README.md", "config/settings.json", "src/helpers.py");

            // Unstaged changes, on top of them.
            repo
                .WriteFile("README.md", ChangesFiles.ReadmeWorking)
                .WriteFile("src/Calculator.cs", ChangesFiles.CalculatorWorking)
                .WriteFile("docs/guide.md", ChangesFiles.GuideWorking)
                .DeleteFile("docs/old-notes.txt")
                .WriteFile("data/large.txt", ChangesFiles.LargeWorking)
                .WriteBytes("assets/logo.png", ChangesFiles.LogoWorking)
                .WriteBytes("data/blob.bin", ChangesFiles.BlobWorking);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// A repository whose own configuration sets <c>core.autocrlf=true</c>, as Git for Windows
    /// does for the whole machine: <c>notes.txt</c>, 20 lines "Note 1" to "Note 20", is stored
    /// with LF and checked out with CRLF, and is changed in the working tree in two places far
    /// enough apart to make two hunks: line 2 reads "Note 2, changed" and line 18 "Note 18,
    /// changed", still with CRLF. One commit, "Add notes", by "Test Author" at 2026-01-01 12:00 UTC.
    /// </summary>
    public static async Task<TempRepo> CrlfAsync()
    {
        var repo = await TempRepo.CreateAsync("crlf");
        try
        {
            await repo.CommitFileAsync("notes.txt", CrlfNotes(changed: false, lineEnding: "\n"), "Add notes");
            await repo.GitAsync("config", "core.autocrlf", "true");

            // Checked out again under the new setting: now with CRLF.
            repo.DeleteFile("notes.txt");
            await repo.GitAsync("checkout", "--", "notes.txt");
            repo.WriteFile("notes.txt", CrlfNotes(changed: true, lineEnding: "\r\n"));
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>The CRLF scenario's notes.txt: as committed, or with lines 2 and 18 changed; with the given line ending.</summary>
    public static string CrlfNotes(bool changed, string lineEnding)
    {
        var lines = Enumerable.Range(1, 20).Select(number =>
            changed && number is 2 or 18 ? $"Note {number}, changed" : $"Note {number}");
        return string.Concat(lines.Select(line => line + lineEnding));
    }

    /// <summary>Deletes a shared template when the test process ends.</summary>
    private static async Task<TempRepo> KeepUntilExit(Task<TempRepo> building)
    {
        var repo = await building.ConfigureAwait(false);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => repo.Dispose();
        return repo;
    }

    private static async Task<TempRepo> BuildGraphAsync()
    {
        var repo = await TempRepo.CreateAsync("graph");
        try
        {
            await repo.CommitFileAsync("README.md", "# Graph scenario\n", "Initial commit");
            await repo.WriteFile("src/app.txt", "app\n").WriteFile("docs/notes.txt", "notes\n").CommitAsync("Add app skeleton");
            await repo.AnnotatedTagAsync("v0.1", "Version 0.1");

            await repo.CreateBranchAsync("feature/login");
            await repo.CheckoutAsync("feature/login");
            await repo.CommitFileAsync("src/login.txt", "login form\n", "Add login form");
            await repo.WriteFile("src/login.txt", "login form\nvalidation\n").WriteFile("src/validation/rules.txt", "rules\n").CommitAsync("Validate passwords");

            await repo.CheckoutAsync("main");
            await repo.CommitFileAsync("README.md", "# Graph scenario\n\nUpdated.\n", "Update README");
            await repo.MergeAsync("feature/login");

            await repo.CreateBranchAsync("feature/search");
            await repo.CheckoutAsync("feature/search");
            await repo.CommitFileAsync("src/search.txt", "search box\n", "Add search box");

            await repo.CheckoutAsync("main");
            await repo.CreateBranchAsync("bugfix/crash-on-start");
            await repo.CheckoutAsync("bugfix/crash-on-start");
            await repo.CommitFileAsync("src/app.txt", "app\nfixed\n", "Fix crash on start");

            await repo.CheckoutAsync("main");
            await repo.CommitFileAsync("VERSION", "0.2\n", "Bump version");
            await repo.TagAsync("v0.2");

            await repo.AddBareRemoteAsync("origin");
            await repo.PushAsync("origin", setUpstream: true, "main", "feature/search");

            await repo.DetachAsync("main");
            await repo.CommitFileAsync("docs/notes.txt", "notes, fixed\n", "Fix typo in docs");
            await repo.PushAsync("origin", setUpstream: false, "HEAD:main");
            await repo.CheckoutAsync("main");

            await repo
                .WriteFile("src/settings/page.txt", "settings page\n")
                .WriteFile("src/settings/defaults.txt", "defaults\n")
                .WriteFile("README.md", "# Graph scenario\n\nUpdated.\n\nSee the settings page.\n")
                .DeleteFile("docs/notes.txt")
                .DeleteFile("src/app.txt")
                .WriteFile("src/main-app.txt", "app\n")
                .CommitAsync("Add settings page\n\nThe settings page lists the defaults.\nIt replaces the old notes.");

            await repo.CheckoutAsync("feature/search");
            await repo.CommitFileAsync("src/search.txt", "search box\nhighlight\n", "Highlight matches");

            await repo.CheckoutAsync("main");
            repo.WriteFile("README.md", "# Graph scenario\n\nWork in progress.\n");
            await repo.StashAsync("Work in progress on README");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }
}
