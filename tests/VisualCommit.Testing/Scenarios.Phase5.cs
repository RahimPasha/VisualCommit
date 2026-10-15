using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VisualCommit.Core;
using VisualCommit.Core.Git;

namespace VisualCommit.Testing;

/// <summary>
/// The scenario repos of phase 5 (docs/phase-5-design.md 15.1): a history for file history,
/// blame, search and compare; branches to hide and solo; reflogs and backups of every kind; and
/// the checkouts C12 protects. Every state is made by git, as a terminal or the app would make it;
/// the backups are written as the app writes them (phase 3 design 2.0, D67), each after the git
/// state it keeps, by an <c>update-ref --stdin</c> transaction and snapshot commits of the app's
/// own form.
/// </summary>
public static partial class Scenarios
{
    /// <summary>An author of the history scenario's commits other than <see cref="TempRepo.AuthorName"/>.</summary>
    /// <param name="Name">As <c>%aN</c> shows it.</param>
    /// <param name="Email">As <c>%aE</c> shows it.</param>
    public sealed record ScenarioAuthor(string Name, string Email);

    /// <summary>
    /// The history scenario ("history", design 15.1): three authors (<see cref="HistoryFiles.Mira"/>,
    /// <see cref="HistoryFiles.Jonas"/> and "Test Author"), a file renamed with an edit, a file
    /// deleted and added again, a file no commit changes after the first, an image, a merge, words
    /// only in commit bodies, a pickaxe string added and removed, and two branches that diverged.
    /// Commits, from 2026-01-01 12:00 UTC, each by its author (author and committer alike):
    /// <list type="number">
    /// <item>12:00 "Initial commit" on main, Test Author: README.md, LICENSE, docs/notes.md (<see cref="HistoryFiles.Readme"/>, <see cref="HistoryFiles.License"/>, <see cref="HistoryFiles.NotesFirst"/>)</item>
    /// <item>12:01 "Add the parser", Mira: src/Parser.cs (<see cref="HistoryFiles.ParserAdded"/>)</item>
    /// <item>12:02 "Add the logo", Test Author: assets/logo.png (<see cref="HistoryFiles.Logo"/>)</item>
    /// <item>12:03 "Handle empty input", Jonas, its body naming the tokenizer: src/Parser.cs (<see cref="HistoryFiles.ParserEmptyInput"/>)</item>
    /// <item>12:04 "Add tracing", Test Author: src/Config.cs with the pickaxe string
    /// <see cref="HistoryFiles.PickaxeText"/> (<see cref="HistoryFiles.ConfigTraced"/>); feature/compare starts here</item>
    /// <item>12:05 "Add the comparer" on feature/compare, Jonas: src/Comparer.cs (<see cref="HistoryFiles.ComparerAdded"/>), README.md (<see cref="HistoryFiles.ReadmeCompare"/>)</item>
    /// <item>12:06 "Rename the parser to reader" on main, Mira: src/Parser.cs renamed to
    /// src/Reader.cs with two lines changed (<see cref="HistoryFiles.ReaderRenamed"/>; git sees a
    /// rename of 84 % similarity, <c>R084</c>)</item>
    /// <item>12:07 "Compare whole lines" on feature/compare, Jonas, its body naming the tokenizer: src/Comparer.cs (<see cref="HistoryFiles.ComparerLines"/>)</item>
    /// <item>12:08 "Remove the notes" on main, Test Author: docs/notes.md deleted</item>
    /// <item>12:09 "Explain the reader" on feature/readme, from Remove the notes, Jonas: README.md (<see cref="HistoryFiles.ReadmeReader"/>)</item>
    /// <item>12:10 "Merge branch 'feature/readme'" on main, Test Author (feature/readme then deleted)</item>
    /// <item>12:11 "Write new notes" on main, Mira: docs/notes.md added again (<see cref="HistoryFiles.NotesSecond"/>)</item>
    /// <item>12:12 "Stop tracing" on main, Test Author: the pickaxe string removed from src/Config.cs (<see cref="HistoryFiles.ConfigUntraced"/>)</item>
    /// <item>12:13 "Read whole lines" on main, Jonas: src/Reader.cs (<see cref="HistoryFiles.ReaderLines"/>)</item>
    /// </list>
    /// So blame of src/Reader.cs at main has lines of four commits (Add the parser, Handle empty
    /// input, the rename, Read whole lines), by two authors; LICENSE is in every commit unchanged;
    /// <see cref="HistoryFiles.BodyWord"/> is in the bodies of Handle empty input and Compare whole
    /// lines and in no subject. The bare remote <c>origin</c> (<c>origin.git</c> next to the
    /// working tree) has main and feature/compare, both tracked, ahead and behind 0. HEAD is on
    /// main, with src/Reader.cs changed in the working tree, not staged
    /// (<see cref="HistoryFiles.ReaderWorking"/>: its summary line changed and a property added),
    /// so that its blame in the working tree has lines not committed yet. Built once per test
    /// process; every call returns a fresh copy.
    /// </summary>
    public static async Task<TempRepo> HistoryAsync()
    {
        var template = await TemplateAsync("history", HistoryTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> HistoryTemplate = new(() => KeepUntilExit(BuildHistoryAsync()));

    /// <summary>The history scenario's authors, file contents and search texts.</summary>
    public static class HistoryFiles
    {
        /// <summary>The author of Add the parser, the rename and Write new notes.</summary>
        public static ScenarioAuthor Mira { get; } = new("Mira Kovac", "mira@example.com");

        /// <summary>The author of Handle empty input, Add the comparer, Compare whole lines, Explain the reader and Read whole lines.</summary>
        public static ScenarioAuthor Jonas { get; } = new("Jonas Berg", "jonas@example.com");

        /// <summary>A word in the bodies of Handle empty input and Compare whole lines, and in no subject (check F10's message search).</summary>
        public const string BodyWord = "tokenizer";

        /// <summary>The string Add tracing adds and Stop tracing removes, and no other commit changes (check F11's Changes search).</summary>
        public const string PickaxeText = "TraceEverything";

        /// <summary>Handle empty input's message.</summary>
        public const string EmptyInputMessage = "Handle empty input\n\nThe tokenizer stops at an empty text now.";

        /// <summary>Compare whole lines' message.</summary>
        public const string CompareLinesMessage = "Compare whole lines\n\nIt splits the texts with the tokenizer first.";

        public const string Readme = "# History\n\nA repository for the history checks.\n";

        /// <summary>README.md on feature/compare (Add the comparer).</summary>
        public const string ReadmeCompare = Readme + "\nCompare two texts with Comparer.\n";

        /// <summary>README.md as Explain the reader leaves it, and main after the merge.</summary>
        public const string ReadmeReader = Readme + "\nThe reader splits a text into lines.\n";

        public const string License =
            "MIT License\n" +
            "\n" +
            "Copyright (c) 2026 The History Authors\n" +
            "\n" +
            "Permission is hereby granted, free of charge, to any person obtaining a copy\n" +
            "of this software, to deal in the software without restriction.\n";

        /// <summary>docs/notes.md in its first life (Initial commit to Remove the notes).</summary>
        public const string NotesFirst = "# Notes\n\nThe first notes.\n";

        /// <summary>docs/notes.md in its second life (Write new notes).</summary>
        public const string NotesSecond = "# Notes\n\nNotes written again.\n";

        public const string ParserAdded =
            "namespace History;\n" +
            "\n" +
            "/// <summary>Reads the words of a text.</summary>\n" +
            "public sealed class Parser\n" +
            "{\n" +
            "    private readonly string _text;\n" +
            "\n" +
            "    public Parser(string text)\n" +
            "    {\n" +
            "        _text = text;\n" +
            "    }\n" +
            "\n" +
            "    public string[] Words()\n" +
            "    {\n" +
            "        return _text.Split(' ');\n" +
            "    }\n" +
            "}\n";

        /// <summary>src/Parser.cs after Handle empty input: a null text read as empty, and an empty text has no words.</summary>
        public static string ParserEmptyInput { get; } = ParserAdded
            .Replace("        _text = text;\n", "        _text = text ?? string.Empty;\n", StringComparison.Ordinal)
            .Replace(
                "    {\n        return _text.Split(' ');\n",
                "    {\n        if (_text.Length == 0)\n        {\n            return [];\n        }\n\n        return _text.Split(' ');\n",
                StringComparison.Ordinal);

        /// <summary>src/Reader.cs as the rename leaves it: the class and its constructor named Reader.</summary>
        public static string ReaderRenamed { get; } = ParserEmptyInput
            .Replace("public sealed class Parser\n", "public sealed class Reader\n", StringComparison.Ordinal)
            .Replace("    public Parser(string text)\n", "    public Reader(string text)\n", StringComparison.Ordinal);

        /// <summary>src/Reader.cs after Read whole lines: Lines in place of Words, split at line feeds.</summary>
        public static string ReaderLines { get; } = ReaderRenamed
            .Replace("    public string[] Words()\n", "    public string[] Lines()\n", StringComparison.Ordinal)
            .Replace("        return _text.Split(' ');\n", "        return _text.Split('\\n');\n", StringComparison.Ordinal);

        /// <summary>src/Reader.cs in the working tree: its summary line changed and a Length property added at the end, not staged.</summary>
        public static string ReaderWorking { get; } = ReaderLines
            .Replace("/// <summary>Reads the words of a text.</summary>\n", "/// <summary>Reads the lines of a text.</summary>\n", StringComparison.Ordinal)
            .Replace("    }\n}\n", "    }\n\n    public int Length => _text.Length;\n}\n", StringComparison.Ordinal);

        /// <summary>src/Config.cs as Add tracing adds it, with <see cref="PickaxeText"/>.</summary>
        public const string ConfigTraced =
            "namespace History;\n" +
            "\n" +
            "public static class Config\n" +
            "{\n" +
            "    public const bool TraceEverything = true;\n" +
            "\n" +
            "    public const int Width = 80;\n" +
            "}\n";

        /// <summary>src/Config.cs after Stop tracing: without <see cref="PickaxeText"/>.</summary>
        public const string ConfigUntraced =
            "namespace History;\n" +
            "\n" +
            "public static class Config\n" +
            "{\n" +
            "    public const int Width = 80;\n" +
            "}\n";

        public const string ComparerAdded =
            "namespace History;\n" +
            "\n" +
            "public static class Comparer\n" +
            "{\n" +
            "    public static bool Same(string one, string two) => one == two;\n" +
            "}\n";

        /// <summary>src/Comparer.cs after Compare whole lines.</summary>
        public const string ComparerLines =
            "namespace History;\n" +
            "\n" +
            "public static class Comparer\n" +
            "{\n" +
            "    public static bool Same(string one, string two) =>\n" +
            "        one.Split('\\n').SequenceEqual(two.Split('\\n'));\n" +
            "}\n";

        public static TestImages.Rgb LogoColour { get; } = TestImages.Rgb.Parse("#1F86B0");

        /// <summary>assets/logo.png: 32×32 blue with a white 8×8 square in the middle.</summary>
        public static byte[] Logo { get; } = TestImages.SquareOn(32, 32, LogoColour, new TestImages.Rgb(255, 255, 255), 8);
    }

    /// <summary>
    /// The filters scenario ("filters", design 15.1, 8): branches to hide and solo. Commits, by
    /// "Test Author" from 2026-01-01 12:00 UTC:
    /// <list type="number">
    /// <item>12:00 "Initial commit" on main (README.md "# Filters\n")</item>
    /// <item>12:01 "Add the core" on main (src/core.txt "core\n"), tagged <c>v1</c> (lightweight); feature/alpha, feature/beta and feature/merged start here</item>
    /// <item>12:02 "Beta one" on feature/beta (beta/one.txt "beta one\n")</item>
    /// <item>12:03 "Alpha one" on feature/alpha (src/alpha.txt "alpha one\n")</item>
    /// <item>12:04 "Beta two" on feature/beta (beta/two.txt "beta two\n")</item>
    /// <item>12:05 "Merged work" on feature/merged (src/merged.txt "merged\n")</item>
    /// <item>12:06 "Beta three" on feature/beta (beta/three.txt "beta three\n")</item>
    /// <item>12:07 "Main work" on main (src/core.txt "core\nmain work\n")</item>
    /// <item>12:08 "Merge branch 'feature/merged'" on main</item>
    /// <item>12:09 "Beta four" on feature/beta (beta/four.txt "beta four\n")</item>
    /// <item>12:10 "Alpha two" on feature/alpha (src/alpha.txt "alpha one\nalpha two\n"): pushed,
    /// then feature/alpha put back on Alpha one, so origin/feature/alpha is one commit ahead</item>
    /// <item>12:11 "Beta five" on feature/beta (beta/five.txt "beta five\n")</item>
    /// <item>12:12 "Beta six" on feature/beta (beta/six.txt "beta six\n")</item>
    /// <item>12:13 stash "On feature/beta: Beta experiment" on Beta six (beta/one.txt "beta one\nexperiment\n")</item>
    /// <item>12:14 stash "On feature/alpha: Alpha experiment" on Alpha one (src/alpha.txt "alpha one\nexperiment\n"):
    /// a commit only feature/alpha and its upstream reach, so soloing feature/beta leaves it out</item>
    /// </list>
    /// The bare remote <c>origin</c> (<c>origin.git</c> next to the working tree) has main,
    /// feature/alpha (at Alpha two) and feature/beta, each the upstream of the local branch of its
    /// name (main and feature/beta ahead and behind 0, feature/alpha behind 1); its HEAD,
    /// <c>refs/remotes/origin/HEAD</c>, points at origin/feature/beta. feature/merged is local
    /// only. HEAD is on a clean main. Built once per test process; every call returns a fresh copy.
    /// </summary>
    public static async Task<TempRepo> FiltersAsync()
    {
        var template = await TemplateAsync("filters", FiltersTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> FiltersTemplate = new(() => KeepUntilExit(BuildFiltersAsync()));

    /// <summary>
    /// The reflog scenario ("reflog", design 15.1, 5): reflogs with lost commits and a gap, and a
    /// backup of every kind the backups list shows. Commits and steps, from 2026-01-01 12:00 UTC,
    /// each backup named, and its snapshot made, just before the step it keeps, as the app makes
    /// them, the name one second after the minute of that step (<see cref="BackupClock.Fixed"/> at
    /// the scenario's clock; the clock moves on a minute after it). The backup refs themselves are
    /// written by one <c>update-ref --stdin</c> transaction before doomed is deleted, which also
    /// deletes the tags v0.9 and v1.0-rc: neither backup refs nor tags have a reflog, so the
    /// repository is the same as if each had been written at its step, with fewer git processes:
    /// <list type="number">
    /// <item>12:00 "Initial commit" on main (README.md <see cref="ReflogFiles.Readme"/>), tagged <c>v0.8</c> (lightweight)</item>
    /// <item>12:01 "Add the app" (src/app.txt <see cref="ReflogFiles.App"/>), tagged <c>v0.9</c> (annotated, "Version 0.9"); topic and clash start here</item>
    /// <item>12:02 "Topic work" on topic (src/topic.txt "topic\n")</item>
    /// <item>12:03 "Main work" on main (src/app.txt <see cref="ReflogFiles.AppMainWork"/>)</item>
    /// <item>12:04 "Clashing work" on clash (src/app.txt <see cref="ReflogFiles.AppClash"/>), 12:05 "Clash notes" on clash (docs/clash.txt "clash notes\n")</item>
    /// <item>main and topic pushed to the bare <c>origin</c> and tracked, the tag v0.8 too, and the
    /// commit Add the app as origin's feature/gone, in one push</item>
    /// <item>abort-…: <c>git merge clash</c> stops with src/app.txt conflicted; its content written as
    /// <see cref="ReflogFiles.AppAbortResolution"/>; <c>abort-&lt;t&gt;</c> → HEAD and
    /// <c>-changes</c> → a snapshot of docs/clash.txt and src/app.txt; <c>git merge --abort</c></item>
    /// <item>skip-…: <c>git cherry-pick &lt;Clashing work&gt; &lt;Clash notes&gt;</c> stops at the
    /// first; src/app.txt written as <see cref="ReflogFiles.AppSkipResolution"/>;
    /// <c>skip-&lt;t&gt;</c> → HEAD and <c>-changes</c> → a snapshot of src/app.txt;
    /// <c>git cherry-pick --skip</c>, which picks Clash notes onto main</item>
    /// <item>rebase-…/topic → topic's tip; topic checked out and rebased onto main</item>
    /// <item>force-push-…/origin/topic → origin/topic's old commit; topic force-pushed (with a lease); main checked out</item>
    /// <item>detached-…: HEAD detached at main, a commit "Detached experiment" (docs/idea.txt
    /// "idea\n"), <c>detached-&lt;t&gt;</c> → it; main checked out, which leaves it behind</item>
    /// <item>stash-…: README.md changed and stashed as "Try a README"; <c>stash-&lt;t&gt;</c> → the stash; the stash dropped</item>
    /// <item>tag-…/v0.9 → the tag object; the tag v0.9 deleted</item>
    /// <item>tag-undone-…/v1.0-rc: an annotated tag v1.0-rc ("Release candidate") made at main,
    /// then its creation undone as the undo does it: the backup → the tag object, the tag deleted</item>
    /// <item>remote-tag-…/origin/v0.8 → v0.8's commit; v0.8 deleted on origin (with a lease)</item>
    /// <item>remote-branch-…/origin/feature/gone → Add the app; feature/gone deleted on origin
    /// (with a lease, by the same push as v0.8), so origin no longer has the branch this backup names</item>
    /// <item>remote-…/mirror/main and /mirror/topic: a second bare remote <c>mirror</c>
    /// (<c>mirror.git</c>) that main and topic were pushed to, then removed</item>
    /// <item>reset-… with <c>-changes</c> of a mixed reset: src/app.txt changed and staged
    /// (<see cref="ReflogFiles.AppStaged"/>); <c>reset-&lt;t&gt;</c> → HEAD, <c>-changes</c> → the
    /// index ("VisualCommit: index before a mixed reset"); <c>git reset --mixed</c> to HEAD's own commit</item>
    /// <item>discard-…: notes/todo.txt (untracked, "todo\n") and src/app.txt (still changed) saved
    /// in a discard snapshot and discarded</item>
    /// <item>topic checked out, and main again</item>
    /// <item>"Draft the notes" (docs/notes.txt <see cref="ReflogFiles.NotesDraft"/>), amended as
    /// "Write the notes" (<see cref="ReflogFiles.Notes"/>): the old commit is lost</item>
    /// <item>reset-… with <c>-changes</c> of a hard reset: "Try a shortcut" (src/shortcut.txt
    /// "shortcut\n"), README.md changed (<see cref="ReflogFiles.ReadmeEdited"/>, not staged);
    /// <c>reset-&lt;t&gt;</c> → Try a shortcut, <c>-changes</c> → a snapshot of README.md;
    /// <c>git reset --hard</c> to Write the notes: Try a shortcut is lost</item>
    /// <item>draft made and checked out, "Draft work" (docs/draft.txt "draft\n"), draft renamed to
    /// feature/renamed while checked out (HEAD's reflog gets an entry <c>git log -g</c> skips: a
    /// gap), main checked out</item>
    /// <item>branch-…/doomed: doomed made and checked out, "Doomed work" (docs/doomed.txt
    /// "doomed\n"), main checked out; <c>branch-&lt;t&gt;/doomed</c> → its tip; doomed deleted (its
    /// reflog goes with it, its commit is lost)</item>
    /// </list>
    /// Then two backups dated 2025-11-20 (<see cref="ReflogFiles.OldBackups"/>), older than 30
    /// days by the harnesses' backup clock (<see cref="AppClocks.HarnessTime"/>, design 2.3), which
    /// the clean-up removes when the repository opens; every other backup is younger. The app's
    /// ref updates are dated 2026-01-02 09:00, later than all of these. HEAD is on a clean main.
    /// Built once per test process; every call returns a fresh copy.
    /// </summary>
    public static async Task<TempRepo> ReflogAsync()
    {
        var template = await TemplateAsync("reflog", ReflogTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> ReflogTemplate = new(() => KeepUntilExit(BuildReflogAsync()));

    /// <summary>
    /// A copy of <see cref="ReflogAsync"/> with backups added until 51 are younger than 30 days
    /// (<see cref="AddOldAndManyBackupsAsync"/>), so that the clean-up at opening removes the
    /// two old ones (older than 30 days) and then the oldest young one (more than 50 backups):
    /// the activity log's clean-up notes (design 2.2, 15.1). Built each time.
    /// </summary>
    public static async Task<TempRepo> ReflogManyBackupsAsync()
    {
        var repo = await ReflogAsync().ConfigureAwait(false);
        try
        {
            await AddOldAndManyBackupsAsync(repo).ConfigureAwait(false);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Adds to <paramref name="repo"/> the backups the clean-up removes when the repository opens
    /// (design 2.2, 2.3, D139): <see cref="ReflogFiles.OldBackups"/> unless it has them, two backups
    /// dated 2025-11-20 and so older than 30 days by <see cref="AppClocks.HarnessTime"/>; and
    /// <c>reset-&lt;t&gt;</c> backups of HEAD's commit dated from 2025-12-20 09:00:01 UTC on, a
    /// second apart, until exactly 51 backups are younger than 30 days (<see cref="ReflogFiles.YoungBackupsForCount"/>).
    /// The young ones it adds are older than any the repository had, so the clean-up removes the
    /// first of them, <see cref="ReflogFiles.FirstFillerBackup"/>, as one too many. For the
    /// reflog scenario (<see cref="ReflogManyBackupsAsync"/>) and any other scenario a check of
    /// the clean-up's notes starts from.
    /// </summary>
    public static async Task AddOldAndManyBackupsAsync(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var existing = await BackupNamesAsync(repo).ConfigureAwait(false);
        if (!existing.Contains(ReflogFiles.OldBackups[0]))
        {
            await SaveOldBackupsAsync(repo).ConfigureAwait(false);
        }

        var cutoff = AppClocks.HarnessTime.AddDays(-BackupRetention.Default.Days);
        var young = existing.Count(name => BackupTimeOf(name) >= cutoff);
        if (young > ReflogFiles.YoungBackupsForCount)
        {
            throw new InvalidOperationException($"The repository already has {young} young backups, more than {ReflogFiles.YoungBackupsForCount}.");
        }

        var head = (await repo.GitAsync("rev-parse", "--verify", "HEAD").ConfigureAwait(false)).StandardOutput.Trim();
        var clock = BackupClock.Fixed(ReflogFiles.FillerStart);
        var lines = new StringBuilder();
        for (var count = young; count < ReflogFiles.YoungBackupsForCount; count++)
        {
            lines.Append("create ").Append(BackupPrefix).Append("reset-").Append(BackupTime(clock.NextName())).Append(' ').Append(head).Append('\n');
        }

        await repo.GitWithInputAsync(lines.ToString(), "update-ref", "--stdin").ConfigureAwait(false);
    }

    /// <summary>The reflog scenario's files, its backups' names and the clean-up's numbers.</summary>
    public static class ReflogFiles
    {
        public const string Readme = "# Reflog\n";

        /// <summary>README.md as changed before the hard reset, which its -changes snapshot keeps.</summary>
        public const string ReadmeEdited = "# Reflog\n\nAn edit the hard reset took away.\n";

        /// <summary>README.md as the dropped stash "Try a README" holds it.</summary>
        public const string ReadmeStashed = "# Reflog\n\nTried in a stash.\n";

        public const string App = "app\n";

        public const string AppMainWork = "app\nmain work\n";

        /// <summary>src/app.txt on clash: it conflicts with <see cref="AppMainWork"/>.</summary>
        public const string AppClash = "app\nclash\n";

        /// <summary>src/app.txt as written to resolve the merge before its abort, kept by the abort's -changes snapshot.</summary>
        public const string AppAbortResolution = "app\nmain work and clash\n";

        /// <summary>src/app.txt as written to resolve the pick before its skip, kept by the skip's -changes snapshot.</summary>
        public const string AppSkipResolution = "app\nresolved, then skipped\n";

        /// <summary>src/app.txt as staged before the mixed reset, kept by its -changes snapshot, and discarded after it.</summary>
        public const string AppStaged = "app\nmain work\nstaged\n";

        /// <summary>notes/todo.txt, the untracked file the discard snapshot keeps.</summary>
        public const string Todo = "todo\n";

        /// <summary>docs/notes.txt in "Draft the notes", the commit the amend leaves behind.</summary>
        public const string NotesDraft = "notes\n";

        /// <summary>docs/notes.txt in "Write the notes", the amended commit.</summary>
        public const string Notes = "notes\nwritten\n";

        /// <summary>The names of the two backups dated 2025-11-20, older than 30 days: a reset's and a deleted branch old-idea's, both of HEAD's commit when they were made.</summary>
        public static IReadOnlyList<string> OldBackups { get; } = ["reset-20251120T090001000Z", "branch-20251120T090002000Z"];

        /// <summary>How many backups younger than 30 days <see cref="AddOldAndManyBackupsAsync"/> leaves: one more than the clean-up keeps (D139's 50).</summary>
        public const int YoungBackupsForCount = 51;

        /// <summary>The time the young backups <see cref="AddOldAndManyBackupsAsync"/> adds count from (the first is a second later): younger than 30 days, older than the scenarios' own.</summary>
        public static DateTimeOffset FillerStart { get; } = new(2025, 12, 20, 9, 0, 0, TimeSpan.Zero);

        /// <summary>The first and oldest young backup <see cref="AddOldAndManyBackupsAsync"/> adds: the one the clean-up removes as more than 50.</summary>
        public const string FirstFillerBackup = "reset-20251220T090001000Z";
    }

    /// <summary>
    /// The protected changes scenario ("protect", design 15.1, 9): checkouts C12 refuses or
    /// stashes for. Commits, by "Test Author" from 2026-01-01 12:00 UTC:
    /// <list type="number">
    /// <item>12:00 "Initial commit" on main: README.md, src/app.txt, src/lib.txt and
    /// settings.local.json (<see cref="ProtectFiles.SettingsCommitted"/>)</item>
    /// <item>12:01 "Add the guide" on main: docs/guide.txt, docs/old.txt</item>
    /// <item>12:02 "Stop tracking the local settings" on main: settings.local.json taken out of the
    /// index and ignored by .gitignore (<see cref="ProtectFiles.Gitignore"/>); other starts here</item>
    /// <item>12:03 "Describe the app" on main: README.md (<see cref="ProtectFiles.ReadmeDescribed"/>), tagged <c>v1.1</c> (annotated, "Version 1.1")</item>
    /// <item>12:04 "Other edits" on other: src/app.txt, src/lib.txt and docs/guide.txt changed</item>
    /// <item>12:05 "Other files" on other: the file <c>notes</c> added, docs/old.txt deleted</item>
    /// <item>12:06 "Main work" on main: README.md (<see cref="ProtectFiles.ReadmeMainWork"/>)</item>
    /// </list>
    /// HEAD is on a clean main. settings.local.json is in the working tree, ignored, holding
    /// <see cref="ProtectFiles.SettingsLocal"/>: the two oldest commits track it, so checking one of
    /// them out would overwrite it (refused with <c>--no-overwrite-ignore</c>); Stop tracking, Describe
    /// the app and other do not. Checking out other is refused for a change to any of README.md,
    /// src/app.txt, src/lib.txt, docs/guide.txt and docs/old.txt, an untracked file <c>notes</c>,
    /// or a folder <c>notes</c> with an untracked file in it. README.md is the only file that
    /// differs between main and Stop tracking, and other's commits do not touch it. The checks
    /// make their own working-tree changes. Built once per test process; every call returns a
    /// fresh copy.
    /// </summary>
    public static async Task<TempRepo> ProtectAsync()
    {
        var template = await TemplateAsync("protect", ProtectTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> ProtectTemplate = new(() => KeepUntilExit(BuildProtectAsync()));

    /// <summary>The protected changes scenario's files on each branch.</summary>
    public static class ProtectFiles
    {
        public const string Readme = "# Protect\n";

        public const string ReadmeDescribed = "# Protect\n\nAn app to protect.\n";

        public const string ReadmeMainWork = "# Protect\n\nAn app to protect.\n\nMain work.\n";

        public const string App = "app\n";

        public const string AppOther = "app\nother\n";

        public const string Lib = "lib\n";

        public const string LibOther = "lib\nother\n";

        public const string Guide = "guide\n";

        public const string GuideOther = "guide\nother\n";

        public const string Old = "old notes\n";

        /// <summary>The file <c>notes</c> as other adds it.</summary>
        public const string NotesOther = "Notes of the other branch.\n";

        /// <summary>settings.local.json as the first two commits track it.</summary>
        public const string SettingsCommitted = "{\n  \"theme\": \"light\"\n}\n";

        /// <summary>settings.local.json in the working tree, ignored: the user's own settings.</summary>
        public const string SettingsLocal = "{\n  \"theme\": \"dark\"\n}\n";

        public const string Gitignore = "settings.local.json\n";
    }

    private static async Task<TempRepo> BuildHistoryAsync()
    {
        var repo = await TempRepo.CreateAsync("history");
        try
        {
            await repo
                .WriteFile("README.md", HistoryFiles.Readme)
                .WriteFile("LICENSE", HistoryFiles.License)
                .WriteFile("docs/notes.md", HistoryFiles.NotesFirst)
                .CommitAsync("Initial commit");
            await CommitAsAsync(repo.WriteFile("src/Parser.cs", HistoryFiles.ParserAdded), HistoryFiles.Mira, "Add the parser");
            await repo.WriteBytes("assets/logo.png", HistoryFiles.Logo).CommitAsync("Add the logo");
            await CommitAsAsync(repo.WriteFile("src/Parser.cs", HistoryFiles.ParserEmptyInput), HistoryFiles.Jonas, HistoryFiles.EmptyInputMessage);
            await repo.CommitFileAsync("src/Config.cs", HistoryFiles.ConfigTraced, "Add tracing");
            await repo.CreateBranchAsync("feature/compare");

            await repo.CheckoutAsync("feature/compare");
            await CommitAsAsync(
                repo.WriteFile("src/Comparer.cs", HistoryFiles.ComparerAdded).WriteFile("README.md", HistoryFiles.ReadmeCompare),
                HistoryFiles.Jonas,
                "Add the comparer");

            await repo.CheckoutAsync("main");
            await repo.GitAsync("mv", "src/Parser.cs", "src/Reader.cs");
            await CommitAsAsync(repo.WriteFile("src/Reader.cs", HistoryFiles.ReaderRenamed), HistoryFiles.Mira, "Rename the parser to reader");

            await repo.CheckoutAsync("feature/compare");
            await CommitAsAsync(repo.WriteFile("src/Comparer.cs", HistoryFiles.ComparerLines), HistoryFiles.Jonas, HistoryFiles.CompareLinesMessage);

            await repo.CheckoutAsync("main");
            await repo.DeleteFile("docs/notes.md").CommitAsync("Remove the notes");

            await repo.CreateBranchAsync("feature/readme");
            await repo.CheckoutAsync("feature/readme");
            await CommitAsAsync(repo.WriteFile("README.md", HistoryFiles.ReadmeReader), HistoryFiles.Jonas, "Explain the reader");
            await repo.CheckoutAsync("main");
            await repo.MergeAsync("feature/readme");
            await repo.GitAsync("branch", "--delete", "feature/readme");

            await CommitAsAsync(repo.WriteFile("docs/notes.md", HistoryFiles.NotesSecond), HistoryFiles.Mira, "Write new notes");
            await repo.CommitFileAsync("src/Config.cs", HistoryFiles.ConfigUntraced, "Stop tracing");
            await CommitAsAsync(repo.WriteFile("src/Reader.cs", HistoryFiles.ReaderLines), HistoryFiles.Jonas, "Read whole lines");

            await repo.AddBareRemoteAsync("origin");
            await repo.PushAsync("origin", setUpstream: true, "main", "feature/compare");

            repo.WriteFile("src/Reader.cs", HistoryFiles.ReaderWorking);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<TempRepo> BuildFiltersAsync()
    {
        var repo = await TempRepo.CreateAsync("filters");
        try
        {
            await repo.CommitFileAsync("README.md", "# Filters\n", "Initial commit");
            await repo.CommitFileAsync("src/core.txt", "core\n", "Add the core");
            await repo.TagAsync("v1");
            foreach (var branch in new[] { "feature/alpha", "feature/beta", "feature/merged" })
            {
                await repo.CreateBranchAsync(branch);
            }

            await OnBranchAsync(repo, "feature/beta", "beta/one.txt", "beta one\n", "Beta one");
            await OnBranchAsync(repo, "feature/alpha", "src/alpha.txt", "alpha one\n", "Alpha one");
            await OnBranchAsync(repo, "feature/beta", "beta/two.txt", "beta two\n", "Beta two");
            await OnBranchAsync(repo, "feature/merged", "src/merged.txt", "merged\n", "Merged work");
            await OnBranchAsync(repo, "feature/beta", "beta/three.txt", "beta three\n", "Beta three");
            await OnBranchAsync(repo, "main", "src/core.txt", "core\nmain work\n", "Main work");
            await repo.MergeAsync("feature/merged");
            await OnBranchAsync(repo, "feature/beta", "beta/four.txt", "beta four\n", "Beta four");

            // origin/feature/alpha one commit ahead: Alpha two pushed, then the local branch put back.
            var alphaTwo = await OnBranchAsync(repo, "feature/alpha", "src/alpha.txt", "alpha one\nalpha two\n", "Alpha two");
            await repo.AddBareRemoteAsync("origin");
            await repo.PushAsync("origin", setUpstream: true, "feature/alpha");

            await OnBranchAsync(repo, "feature/beta", "beta/five.txt", "beta five\n", "Beta five");
            await OnBranchAsync(repo, "feature/beta", "beta/six.txt", "beta six\n", "Beta six");
            await repo.GitAsync("branch", "--force", "feature/alpha", alphaTwo + "~1");
            await repo.PushAsync("origin", setUpstream: true, "main", "feature/beta");
            await repo.GitAsync("remote", "set-head", "origin", "feature/beta");

            repo.WriteFile("beta/one.txt", "beta one\nexperiment\n");
            await repo.StashAsync("Beta experiment");
            await repo.CheckoutAsync("feature/alpha");
            repo.WriteFile("src/alpha.txt", "alpha one\nexperiment\n");
            await repo.StashAsync("Alpha experiment");
            await repo.CheckoutAsync("main");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<TempRepo> BuildReflogAsync()
    {
        var repo = await TempRepo.CreateAsync("reflog");
        try
        {
            // The update-ref --stdin lines of the backups (and of the two tags' deletions), written at the end.
            var backups = new StringBuilder();

            var initial = await repo.CommitFileAsync("README.md", ReflogFiles.Readme, "Initial commit");
            await repo.TagAsync("v0.8");
            var addTheApp = await repo.CommitFileAsync("src/app.txt", ReflogFiles.App, "Add the app");
            await repo.AnnotatedTagAsync("v0.9", "Version 0.9");
            await repo.CreateBranchAsync("topic");
            await repo.CreateBranchAsync("clash");
            var topicWork = await OnBranchAsync(repo, "topic", "src/topic.txt", "topic\n", "Topic work");
            var mainWork = await OnBranchAsync(repo, "main", "src/app.txt", ReflogFiles.AppMainWork, "Main work");
            var clashingWork = await OnBranchAsync(repo, "clash", "src/app.txt", ReflogFiles.AppClash, "Clashing work");
            var clashNotes = await repo.CommitFileAsync("docs/clash.txt", "clash notes\n", "Clash notes");
            await repo.CheckoutAsync("main");

            // One push: the branches with their upstreams (a commit given by its SHA gets none), and the tag.
            await repo.AddBareRemoteAsync("origin");
            await repo.PushAsync("origin", setUpstream: true, "main", "topic", "refs/tags/v0.8", addTheApp + ":refs/heads/feature/gone");

            // A merge stopped by a conflict, partly resolved, then aborted (phase 4 design 5.1).
            await ExpectStopAsync(repo, "merge", "clash");
            repo.WriteFile("src/app.txt", ReflogFiles.AppAbortResolution);
            AddBackup(repo, backups, "abort", [(null, mainWork)], await TrackedChangesSnapshotAsync(repo));
            await repo.GitAsync("merge", "--abort");

            // A list of picks stopped at its first, partly resolved, then skipped: the second is
            // picked, and git's summary of that commit names it.
            await ExpectStopAsync(repo, "cherry-pick", clashingWork, clashNotes);
            repo.WriteFile("src/app.txt", ReflogFiles.AppSkipResolution);
            AddBackup(repo, backups, "skip", [(null, mainWork)], await TrackedChangesSnapshotAsync(repo));
            var main = TempRepo.CommittedSha(await repo.GitAsync("-c", "core.abbrev=no", "cherry-pick", "--skip"));

            // topic rebased onto main, then force-pushed.
            AddBackup(repo, backups, "rebase", [("topic", topicWork)]);
            await repo.CheckoutAsync("topic");
            await repo.GitAsync("rebase", main);
            AddBackup(repo, backups, "force-push", [("origin/topic", topicWork)]);
            await repo.GitAsync("push", "--quiet", "--force-with-lease=refs/heads/topic", "origin", "refs/heads/topic:refs/heads/topic");
            await repo.CheckoutAsync("main");

            // A commit on a detached HEAD, left behind by a checkout.
            await repo.DetachAsync(main);
            var detached = await repo.CommitFileAsync("docs/idea.txt", "idea\n", "Detached experiment");
            AddBackup(repo, backups, "detached", [(null, detached)]);
            await repo.CheckoutAsync("main");

            // A stash dropped.
            var stash = await repo.WriteFile("README.md", ReflogFiles.ReadmeStashed).StashAsync("Try a README");
            AddBackup(repo, backups, "stash", [(null, stash)]);
            await repo.GitAsync("stash", "drop", "--quiet", "stash@{0}");

            // Tags: one deleted, one whose creation was undone (both deleted with the backups), one
            // deleted on the remote.
            var tagBackup = BackupStem("tag", repo);
            await repo.AnnotatedTagAsync("v1.0-rc", "Release candidate");
            var objects = Lines(await repo.GitAsync("rev-parse", "refs/tags/v0.9", "refs/tags/v1.0-rc", "refs/heads/topic"));
            var (v09, candidate, topic) = (objects[0], objects[1], objects[2]);
            backups.Append(CreateLine(tagBackup + "/v0.9", v09));
            AddBackup(repo, backups, "tag-undone", [("v1.0-rc", candidate)]);
            AddBackup(repo, backups, "remote-tag", [("origin/v0.8", initial)]);

            // A branch deleted on the remote, by the same push as the tag.
            AddBackup(repo, backups, "remote-branch", [("origin/feature/gone", addTheApp)]);
            await repo.GitAsync(
                "push",
                "--quiet",
                $"--force-with-lease=refs/tags/v0.8:{initial}",
                $"--force-with-lease=refs/heads/feature/gone:{addTheApp}",
                "origin",
                "--delete",
                "refs/tags/v0.8",
                "refs/heads/feature/gone");

            // A remote removed with its branches.
            await repo.AddBareRemoteAsync("mirror");
            await repo.PushAsync("mirror", setUpstream: false, "main", "topic");
            AddBackup(repo, backups, "remote", [("mirror/main", main), ("mirror/topic", topic)]);
            await repo.GitAsync("remote", "remove", "mirror");

            // A mixed reset of staged changes, then the discard of what it left and a new file.
            await repo.WriteFile("src/app.txt", ReflogFiles.AppStaged).GitAsync("add", "--", "src/app.txt");
            AddBackup(repo, backups, "reset", [(null, main)], await IndexCommitAsync(repo, "HEAD", "VisualCommit: index before a mixed reset"));
            await repo.GitAsync("reset", "--mixed", "--quiet", main, "--");
            repo.WriteFile("notes/todo.txt", ReflogFiles.Todo);
            await AddDiscardSnapshotAsync(repo, backups, ["notes/todo.txt", "src/app.txt"]);
            await repo.GitAsync("restore", "--worktree", "--", "src/app.txt");
            repo.DeleteFile("notes/todo.txt");
            Directory.Delete(Path.Combine(repo.Path, "notes"));

            // The reflog's own story, newest: a checkout and back, an amend, a hard reset, a rename
            // of the checked-out branch, a branch deleted.
            await repo.CheckoutAsync("topic");
            await repo.CheckoutAsync("main");
            await repo.CommitFileAsync("docs/notes.txt", ReflogFiles.NotesDraft, "Draft the notes");
            await repo.WriteFile("docs/notes.txt", ReflogFiles.Notes).GitAsync("add", "--all");
            var notes = TempRepo.CommittedSha(await repo.GitAsync(TempRepo.CommitArguments("--amend", "--message", "Write the notes")));
            repo.MoveClockTo(repo.ClockTime.AddMinutes(1));
            var shortcut = await repo.CommitFileAsync("src/shortcut.txt", "shortcut\n", "Try a shortcut");
            repo.WriteFile("README.md", ReflogFiles.ReadmeEdited);
            AddBackup(repo, backups, "reset", [(null, shortcut)], await HardResetSnapshotAsync(repo, shortcut, notes, ["README.md"]));
            await repo.GitAsync("reset", "--hard", "--quiet", notes, "--");

            // A branch made and checked out in one step, as git checkout -b does: its reflog's
            // first entry and HEAD's are those of git branch and git checkout.
            await repo.GitAsync("checkout", "--quiet", "-b", "draft");
            await repo.CommitFileAsync("docs/draft.txt", "draft\n", "Draft work");
            await repo.GitAsync("branch", "--move", "draft", "feature/renamed");
            await repo.CheckoutAsync("main");

            await repo.GitAsync("checkout", "--quiet", "-b", "doomed");
            var doomed = await repo.CommitFileAsync("docs/doomed.txt", "doomed\n", "Doomed work");
            await repo.CheckoutAsync("main");
            AddBackup(repo, backups, "branch", [("doomed", doomed)]);
            backups.Append(OldBackupLines(notes));
            backups.Append("delete refs/tags/v0.9 ").Append(v09).Append('\n');
            backups.Append("delete refs/tags/v1.0-rc ").Append(candidate).Append('\n');
            await repo.GitWithInputAsync(backups.ToString(), "update-ref", "--stdin");
            await repo.GitAsync("branch", "--delete", "--force", "doomed");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<TempRepo> BuildProtectAsync()
    {
        var repo = await TempRepo.CreateAsync("protect");
        try
        {
            await repo
                .WriteFile("README.md", ProtectFiles.Readme)
                .WriteFile("src/app.txt", ProtectFiles.App)
                .WriteFile("src/lib.txt", ProtectFiles.Lib)
                .WriteFile("settings.local.json", ProtectFiles.SettingsCommitted)
                .CommitAsync("Initial commit");
            await repo.WriteFile("docs/guide.txt", ProtectFiles.Guide).WriteFile("docs/old.txt", ProtectFiles.Old).CommitAsync("Add the guide");

            // The settings stay in the working tree, now ignored, and become the user's own.
            await repo.GitAsync("rm", "--cached", "--quiet", "settings.local.json");
            await repo.WriteFile(".gitignore", ProtectFiles.Gitignore).CommitAsync("Stop tracking the local settings");
            repo.WriteFile("settings.local.json", ProtectFiles.SettingsLocal);
            await repo.CreateBranchAsync("other");

            await repo.CommitFileAsync("README.md", ProtectFiles.ReadmeDescribed, "Describe the app");
            await repo.AnnotatedTagAsync("v1.1", "Version 1.1");

            await repo.CheckoutAsync("other");
            await repo
                .WriteFile("src/app.txt", ProtectFiles.AppOther)
                .WriteFile("src/lib.txt", ProtectFiles.LibOther)
                .WriteFile("docs/guide.txt", ProtectFiles.GuideOther)
                .CommitAsync("Other edits");
            await repo.WriteFile("notes", ProtectFiles.NotesOther).DeleteFile("docs/old.txt").CommitAsync("Other files");

            await repo.CheckoutAsync("main");
            await repo.CommitFileAsync("README.md", ProtectFiles.ReadmeMainWork, "Main work");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    // ---- Steps the phase 5 builders share ----------------------------------------------------------

    /// <summary>The prefix of the app's backup refs (D67, phase 3 design 2.0).</summary>
    private const string BackupPrefix = "refs/visualcommit/backup/";

    /// <summary>
    /// Stages every change and commits it as <paramref name="author"/>, author and committer
    /// alike, at the scenario's clock, which then moves on a minute as for every commit. Returns
    /// the new commit's SHA.
    /// </summary>
    private static async Task<string> CommitAsAsync(TempRepo repo, ScenarioAuthor author, string message)
    {
        await repo.GitAsync("add", "--all").ConfigureAwait(false);
        var committed = await repo.GitWithEnvironmentAsync(
            new Dictionary<string, string?>
            {
                ["GIT_AUTHOR_NAME"] = author.Name,
                ["GIT_AUTHOR_EMAIL"] = author.Email,
                ["GIT_COMMITTER_NAME"] = author.Name,
                ["GIT_COMMITTER_EMAIL"] = author.Email,
            },
            TempRepo.CommitArguments("--message", message)).ConfigureAwait(false);
        repo.MoveClockTo(repo.ClockTime.AddMinutes(1));
        return TempRepo.CommittedSha(committed);
    }

    /// <summary>Checks out <paramref name="branch"/> and commits one file on it. Returns the new commit's SHA.</summary>
    private static async Task<string> OnBranchAsync(TempRepo repo, string branch, string path, string content, string message)
    {
        await repo.CheckoutAsync(branch).ConfigureAwait(false);
        return await repo.CommitFileAsync(path, content, message).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a backup to <paramref name="lines"/>, the <c>update-ref --stdin</c> transaction of
    /// <c>create</c> lines that writes a builder's backups, as the app names them (phase 3 design
    /// 2.0): one name for the operation, <c>&lt;kind&gt;-&lt;time&gt;</c> (<see cref="BackupStem"/>),
    /// with each entry's name after a slash, and the changes snapshot as
    /// <c>&lt;kind&gt;-&lt;time&gt;-changes</c>. The scenario's clock moves on a minute, as for
    /// every backup.
    /// </summary>
    private static void AddBackup(TempRepo repo, StringBuilder lines, string kind, IReadOnlyList<(string? Name, string Target)> entries, string? changes = null)
    {
        var stem = BackupStem(kind, repo);
        foreach (var (name, target) in entries)
        {
            lines.Append(CreateLine(name is null ? stem : stem + "/" + name, target));
        }

        if (changes is not null)
        {
            lines.Append(CreateLine(stem + "-changes", changes));
        }
    }

    /// <summary>
    /// A backup's ref name without its entries, <c>refs/visualcommit/backup/&lt;kind&gt;-&lt;time&gt;</c>:
    /// the time is the backup clock's next name (<see cref="BackupClock.Fixed"/>) at the scenario's
    /// clock, one second after its minute. The clock then moves on a minute, so that the step the
    /// backup keeps is dated after it and the next backup gets a name of its own.
    /// </summary>
    private static string BackupStem(string kind, TempRepo repo)
    {
        var time = BackupTime(BackupClock.Fixed(repo.ClockTime).NextName());
        repo.MoveClockTo(repo.ClockTime.AddMinutes(1));
        return BackupPrefix + kind + "-" + time;
    }

    /// <summary>An <c>update-ref --stdin</c> line that creates <paramref name="name"/> (a ref that must not exist yet) at <paramref name="target"/>.</summary>
    private static string CreateLine(string name, string target) => $"create {name} {target}\n";

    /// <summary>The lines of a git call's output, empty ones left out.</summary>
    private static string[] Lines(GitResult result) => result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>A backup's time as its name writes it: UTC to the millisecond (D67).</summary>
    private static string BackupTime(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);

    /// <summary>The time in a backup's name (<c>&lt;kind&gt;-&lt;time&gt;…</c>).</summary>
    private static DateTimeOffset BackupTimeOf(string name)
    {
        var time = Regex.Match(name, @"\d{8}T\d{9}Z").Value;
        return DateTimeOffset.ParseExact(time, "yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }

    /// <summary>The names of the backups a repository holds, <c>&lt;kind&gt;-&lt;time&gt;[-&lt;n&gt;]</c>, each once.</summary>
    private static async Task<HashSet<string>> BackupNamesAsync(TempRepo repo)
    {
        var refs = await repo.BackupRefsAsync().ConfigureAwait(false);
        return [.. refs.Select(entry => Regex.Match(entry.Name[BackupPrefix.Length..], @"^[a-z-]+?-\d{8}T\d{9}Z(-\d+)?").Value)];
    }

    /// <summary>
    /// <see cref="ReflogFiles.OldBackups"/>: a reset's backup and a deleted branch old-idea's, both
    /// of HEAD's commit, named by <see cref="BackupClock.Fixed"/> at 2025-11-20 09:00 UTC and so
    /// older than 30 days by <see cref="AppClocks.HarnessTime"/>: aged by their names alone, for
    /// the clean-up to remove.
    /// </summary>
    private static async Task SaveOldBackupsAsync(TempRepo repo)
    {
        var head = (await repo.GitAsync("rev-parse", "--verify", "HEAD").ConfigureAwait(false)).StandardOutput.Trim();
        await repo.GitWithInputAsync(OldBackupLines(head), "update-ref", "--stdin").ConfigureAwait(false);
    }

    /// <summary>The <c>update-ref --stdin</c> lines of <see cref="SaveOldBackupsAsync"/>, with <paramref name="head"/> as HEAD's commit.</summary>
    private static string OldBackupLines(string head)
    {
        var clock = BackupClock.Fixed(new DateTimeOffset(2025, 11, 20, 9, 0, 0, TimeSpan.Zero));
        return CreateLine($"{BackupPrefix}reset-{BackupTime(clock.NextName())}", head) +
            CreateLine($"{BackupPrefix}branch-{BackupTime(clock.NextName())}/old-idea", head);
    }

    /// <summary>The identity of the app's snapshot commits (D67): the app's record, not the user's.</summary>
    private static readonly IReadOnlyDictionary<string, string?> SnapshotIdentity = new Dictionary<string, string?>
    {
        ["GIT_AUTHOR_NAME"] = "VisualCommit",
        ["GIT_AUTHOR_EMAIL"] = "snapshot@visualcommit.invalid",
        ["GIT_COMMITTER_NAME"] = "VisualCommit",
        ["GIT_COMMITTER_EMAIL"] = "snapshot@visualcommit.invalid",
    };

    /// <summary>
    /// A snapshot commit of the app's form (D67, <c>GitRepository.CommitSnapshotAsync</c>): an index
    /// of its own read from HEAD with <paramref name="paths"/> taken from the working tree (a path
    /// gone from it removed), committed with the parents HEAD and <paramref name="secondParent"/>
    /// and the message <paramref name="subject"/>, an empty line and the paths, one per line.
    /// </summary>
    private static async Task<string> SnapshotAsync(TempRepo repo, IReadOnlyList<string> paths, string head, string? secondParent, string subject)
    {
        using var folder = new TempDirectory("vc snapshot");
        var environment = new Dictionary<string, string?>(SnapshotIdentity) { ["GIT_INDEX_FILE"] = folder.Combine("index") };
        await repo.GitWithEnvironmentAsync(environment, "read-tree", head).ConfigureAwait(false);
        var present = paths.Where(path => File.Exists(Path.Combine(repo.Path, path))).ToList();
        var gone = paths.Where(path => !File.Exists(Path.Combine(repo.Path, path))).ToList();
        if (present.Count > 0)
        {
            await repo.GitWithEnvironmentAsync(environment, ["add", "--all", "--force", "--", .. present]).ConfigureAwait(false);
        }

        if (gone.Count > 0)
        {
            await repo.GitWithEnvironmentAsync(environment, ["update-index", "--force-remove", "--", .. gone]).ConfigureAwait(false);
        }

        var tree = (await repo.GitWithEnvironmentAsync(environment, "write-tree").ConfigureAwait(false)).StandardOutput.Trim();
        string[] parents = secondParent is null ? ["-p", head] : ["-p", head, "-p", secondParent];
        var message = $"{subject}\n\n{string.Join('\n', paths)}";
        return (await repo.GitWithEnvironmentAsync(SnapshotIdentity, ["commit-tree", tree, .. parents, "-m", message]).ConfigureAwait(false)).StandardOutput.Trim();
    }

    /// <summary>
    /// A commit of the real index's tree with <paramref name="head"/> as its parent and
    /// <paramref name="subject"/> as its message, as the app keeps an index
    /// (<c>GitRepository.CommitIndexAsync</c>): written from a copy, so the real index is not touched.
    /// </summary>
    private static async Task<string> IndexCommitAsync(TempRepo repo, string head, string subject)
    {
        using var folder = new TempDirectory("vc index");
        var copy = folder.Combine("index");
        File.Copy(Path.Combine(repo.Path, ".git", "index"), copy);
        var environment = new Dictionary<string, string?>(SnapshotIdentity) { ["GIT_INDEX_FILE"] = copy };
        var tree = (await repo.GitWithEnvironmentAsync(environment, "write-tree").ConfigureAwait(false)).StandardOutput.Trim();
        return (await repo.GitWithEnvironmentAsync(SnapshotIdentity, "commit-tree", tree, "-p", head, "-m", subject).ConfigureAwait(false)).StandardOutput.Trim();
    }

    /// <summary>
    /// The snapshot an abort or a skip keeps (<c>SnapshotTrackedChangesAsync</c>): every staged
    /// path and every changed tracked one, conflicted ones included, in byte order, with HEAD as
    /// its only parent and the app's subject "VisualCommit: snapshot before an abort". The paths
    /// come from one <c>git diff --name-only HEAD</c>, the working tree against HEAD: in this
    /// scenario's stops every staged path is also changed in the working tree, so they are those
    /// the app's two calls (staged, then unstaged) give.
    /// </summary>
    private static async Task<string> TrackedChangesSnapshotAsync(TempRepo repo)
    {
        var changed = (await repo.GitAsync("diff", "--name-only", "-z", "HEAD", "--").ConfigureAwait(false)).StandardOutput;
        var paths = changed.Split('\0').Where(path => path.Length > 0).Distinct().Order(StringComparer.Ordinal).ToList();
        return await SnapshotAsync(repo, paths, "HEAD", null, "VisualCommit: snapshot before an abort").ConfigureAwait(false);
    }

    /// <summary>
    /// The snapshot a hard reset to <paramref name="target"/> keeps (<c>SnapshotForHardResetAsync</c>)
    /// of <paramref name="paths"/>: the parents HEAD and the index ("VisualCommit: index before a
    /// hard reset"), the subject naming the target's short SHA.
    /// </summary>
    private static async Task<string> HardResetSnapshotAsync(TempRepo repo, string head, string target, IReadOnlyList<string> paths)
    {
        var index = await IndexCommitAsync(repo, head, "VisualCommit: index before a hard reset").ConfigureAwait(false);
        return await SnapshotAsync(repo, paths, head, index, $"VisualCommit: snapshot before a hard reset to {target[..7]}").ConfigureAwait(false);
    }

    /// <summary>
    /// A discard snapshot of the app's form (D67, <c>GitRepository.SaveSnapshotAsync</c>): the
    /// files as they are, on HEAD, added to <paramref name="lines"/> as <c>discard-&lt;time&gt;</c>
    /// (a ref that must not exist yet), the time from the backup clock as every backup's
    /// (<see cref="BackupStem"/>).
    /// </summary>
    private static async Task AddDiscardSnapshotAsync(TempRepo repo, StringBuilder lines, IReadOnlyList<string> paths)
    {
        var files = paths.Count == 1 ? "1 file" : string.Create(CultureInfo.InvariantCulture, $"{paths.Count} files");
        var commit = await SnapshotAsync(repo, paths, "HEAD", null, $"VisualCommit: snapshot before discarding {files}").ConfigureAwait(false);
        lines.Append(CreateLine(BackupStem("discard", repo), commit));
    }
}
