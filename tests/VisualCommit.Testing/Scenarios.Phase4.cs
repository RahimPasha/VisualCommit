using System.Text;

namespace VisualCommit.Testing;

/// <summary>
/// The scenario repos of phase 4 (docs/phase-4-design.md 13.1): a rebase, an interactive rebase
/// and conflicts of every kind the resolver shows. Every file holds short lines: about 51
/// characters fit a pane of the resolver at 1100×700.
/// </summary>
public static partial class Scenarios
{
    /// <summary>
    /// The states <see cref="ConflictsStoppedAsync"/> leaves a copy of
    /// <see cref="ConflictsAsync"/> in, each made by git as a terminal would. Nested here, as
    /// <see cref="InProgressKind"/> is, so that it never clashes with a type of the app.
    /// </summary>
    public enum ConflictsStop
    {
        /// <summary>On main, <c>git merge feature/theirs</c>: notes.txt both modified, with two blocks.</summary>
        Merge,

        /// <summary>On feature/rebase, <c>git rebase main</c>: stopped at First change, notes.txt with one block.</summary>
        Rebase,

        /// <summary>On main, <c>git cherry-pick</c> of the three feature/picks commits: stopped at the second, one more waits.</summary>
        CherryPicks,

        /// <summary>On main, <c>git merge feature/kinds</c>: other.txt deleted by them, image.png binary, new.txt both added.</summary>
        MergeKinds,
    }

    /// <summary>
    /// The rebase scenario ("rebase"). Commits, by "Test Author" from 2026-01-01 12:00 UTC:
    /// <list type="number">
    /// <item>12:00 "Initial commit" on main (README.md "# Rebase\n")</item>
    /// <item>12:01 "Add core" on main (src/core.txt "core\n")</item>
    /// <item>12:02 "Topic one" on feature/topic, from Add core (topic/one.txt "topic one\n")</item>
    /// <item>12:03 "Topic two" on feature/topic (topic/two.txt "topic two\n")</item>
    /// <item>12:04 "Main work" on main (src/core.txt "core\nmain work\n")</item>
    /// <item>12:05 "Main more" on main (docs/main.txt "main more\n")</item>
    /// <item>12:06 "Other one" on feature/other, from Add core (other/one.txt "other one\n")</item>
    /// </list>
    /// The bare remote <c>origin</c> (<c>origin.git</c> next to the working tree) has main and
    /// feature/topic, both tracked, ahead and behind 0; feature/other is local only. HEAD is on a
    /// clean feature/topic. Rebasing feature/topic or feature/other onto main has no conflict.
    /// Built once per test process; every call returns a fresh copy.
    /// </summary>
    public static async Task<TempRepo> RebaseAsync()
    {
        var template = await TemplateAsync("rebase", RebaseTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> RebaseTemplate = new(() => KeepUntilExit(BuildRebaseAsync()));

    /// <summary>The rebase scenario's file contents, as committed.</summary>
    public static class RebaseFiles
    {
        public const string Readme = "# Rebase\n";

        public const string Core = "core\n";

        public const string CoreMainWork = "core\nmain work\n";

        public const string MainMore = "main more\n";

        public const string TopicOne = "topic one\n";

        public const string TopicTwo = "topic two\n";

        public const string OtherOne = "other one\n";
    }

    /// <summary>
    /// The interactive rebase scenario ("interactive"): one line of history on main. Commits, by
    /// "Test Author" from 2026-01-01 12:00 UTC:
    /// <list type="number">
    /// <item>12:00 "Initial commit" (README.md "# Interactive\n")</item>
    /// <item>12:01 "Base" (src/base.txt "base\n")</item>
    /// <item>12:02 "One" (src/one.txt "one\n")</item>
    /// <item>12:03 "Two" (src/two.txt "two\n")</item>
    /// <item>12:04 "Fix one" (src/one.txt "one\nfixed\n"): a fixup candidate for One</item>
    /// <item>12:05 "Three" (src/three.txt "three\n"), with the body line "#42 is the issue"
    /// (<see cref="InteractiveFiles.ThreeMessage"/>)</item>
    /// <item>12:06 "Four" (src/four.txt "four\n")</item>
    /// <item>12:07 "Five" (src/five.txt "five\n")</item>
    /// </list>
    /// HEAD is on a clean main; no remote. Every reorder of these commits applies without a
    /// conflict, except moving Fix one below One (src/one.txt does not exist before One). Built
    /// once per test process; every call returns a fresh copy.
    /// </summary>
    public static async Task<TempRepo> InteractiveAsync()
    {
        var template = await TemplateAsync("interactive", InteractiveTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> InteractiveTemplate = new(() => KeepUntilExit(BuildInteractiveAsync()));

    /// <summary>The interactive rebase scenario's file contents and messages, as committed.</summary>
    public static class InteractiveFiles
    {
        public const string Readme = "# Interactive\n";

        public const string Base = "base\n";

        public const string One = "one\n";

        public const string Two = "two\n";

        public const string OneFixed = "one\nfixed\n";

        public const string Three = "three\n";

        public const string Four = "four\n";

        public const string Five = "five\n";

        /// <summary>Three's message, as <c>git cat-file commit</c> shows it after the header: the body line starts with <c>#</c> and is kept (D68).</summary>
        public const string ThreeMessage = "Three\n\n#42 is the issue\n";
    }

    /// <summary>
    /// The conflicts scenario ("conflicts"). Commits, by "Test Author" from 2026-01-01 12:00 UTC:
    /// <list type="number">
    /// <item>12:00 "Initial commit" on main (README.md "# Conflicts\n")</item>
    /// <item>12:01 "Base" on main: notes.txt (<see cref="ConflictsFiles.Notes"/>, "line 1" to
    /// "line 12"), other.txt (<see cref="ConflictsFiles.Other"/>), image.png
    /// (<see cref="ConflictsFiles.ImageBase"/>)</item>
    /// <item>12:02 "Theirs edits" on feature/theirs, from Base: notes.txt lines 2, 6 and 10
    /// (<see cref="ConflictsFiles.NotesTheirs"/>)</item>
    /// <item>12:03 "First change" on feature/rebase, from Base: notes.txt line 2
    /// (<see cref="ConflictsFiles.NotesFirst"/>)</item>
    /// <item>12:04 "Second change" on feature/rebase: notes.txt line 10
    /// (<see cref="ConflictsFiles.NotesSecond"/>)</item>
    /// <item>12:05 "Pick clean" on feature/picks, from Base: pick.txt (<see cref="ConflictsFiles.Pick"/>)</item>
    /// <item>12:06 "Pick conflicting" on feature/picks: notes.txt line 2 (<see cref="ConflictsFiles.NotesPicked"/>)</item>
    /// <item>12:07 "Pick last" on feature/picks: last.txt (<see cref="ConflictsFiles.Last"/>)</item>
    /// <item>12:08 "Kinds edits" on feature/kinds, from Base: other.txt deleted, image.png
    /// changed to <see cref="ConflictsFiles.ImageKinds"/>, new.txt added
    /// (<see cref="ConflictsFiles.NewKinds"/>)</item>
    /// <item>12:09 "Ours edits" on main: notes.txt lines 2 and 10 (<see cref="ConflictsFiles.NotesOurs"/>),
    /// other.txt changed (<see cref="ConflictsFiles.OtherOurs"/>), image.png changed to
    /// <see cref="ConflictsFiles.ImageOurs"/>, new.txt added (<see cref="ConflictsFiles.NewOurs"/>)</item>
    /// </list>
    /// HEAD is on a clean main, the newest commit; no remote. feature/theirs, feature/rebase and
    /// feature/picks touch none of other.txt, image.png and new.txt. Merging feature/theirs
    /// conflicts in notes.txt in two blocks seven lines apart (line 6 merges cleanly); each of
    /// First change, Second change and Pick conflicting conflicts with main on its own; merging
    /// feature/kinds gives other.txt deleted by them, image.png both modified (binary) and new.txt
    /// both added. Built once per test process; every call returns a fresh copy.
    /// </summary>
    public static async Task<TempRepo> ConflictsAsync()
    {
        var template = await TemplateAsync("conflicts", ConflictsTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> ConflictsTemplate = new(() => KeepUntilExit(BuildConflictsAsync()));

    /// <summary>
    /// A copy of <see cref="ConflictsAsync"/> stopped in <paramref name="kind"/>, through git calls
    /// as a terminal would make them, on main unless said otherwise:
    /// <list type="bullet">
    /// <item>Merge: <c>git merge feature/theirs</c>; notes.txt is both modified (<c>UU</c>) with
    /// two blocks, at lines 2 and 10 (<see cref="ConflictsFiles.NotesMergeConflicted"/>).</item>
    /// <item>Rebase: on feature/rebase, <c>git rebase main</c>; stopped at First change (1 of 2)
    /// with notes.txt both modified, one block at line 2. Taking its theirs side, <c>git add</c>
    /// and <c>git rebase --continue</c> stop again at Second change, with one block at line 10.</item>
    /// <item>CherryPicks: <c>git cherry-pick &lt;Pick clean&gt; &lt;Pick conflicting&gt; &lt;Pick last&gt;</c>;
    /// Pick clean is picked (a new commit on main), Pick conflicting stops with notes.txt both
    /// modified, one block at line 2, and Pick last waits in <c>sequencer/todo</c>.</item>
    /// <item>MergeKinds: <c>git merge feature/kinds</c>; other.txt deleted by them (<c>UD</c>, ours
    /// in the working tree), image.png both modified (<c>UU</c>, binary: ours in the working tree,
    /// no markers), new.txt both added (<c>AA</c>, one block at line 2).</item>
    /// </list>
    /// With <paramref name="mergeTool"/>, the copy's own configuration also sets up the fake merge
    /// tool (<see cref="ConfigureFakeMergeToolAsync"/>). Built each time.
    /// </summary>
    public static async Task<TempRepo> ConflictsStoppedAsync(ConflictsStop kind, bool mergeTool = false)
    {
        var repo = await ConflictsAsync().ConfigureAwait(false);
        try
        {
            if (mergeTool)
            {
                await ConfigureFakeMergeToolAsync(repo).ConfigureAwait(false);
            }

            switch (kind)
            {
                case ConflictsStop.Merge:
                    await ExpectStopAsync(repo, "merge", "feature/theirs").ConfigureAwait(false);
                    break;

                case ConflictsStop.Rebase:
                    await repo.CheckoutAsync("feature/rebase").ConfigureAwait(false);
                    await ExpectStopAsync(repo, "rebase", "main").ConfigureAwait(false);
                    break;

                case ConflictsStop.CherryPicks:
                    var clean = await RevParseAsync(repo, "refs/heads/feature/picks~2").ConfigureAwait(false);
                    var conflicting = await RevParseAsync(repo, "refs/heads/feature/picks~1").ConfigureAwait(false);
                    var last = await RevParseAsync(repo, "refs/heads/feature/picks").ConfigureAwait(false);
                    await ExpectStopAsync(repo, "cherry-pick", clean, conflicting, last).ConfigureAwait(false);
                    break;

                case ConflictsStop.MergeKinds:
                    await ExpectStopAsync(repo, "merge", "feature/kinds").ConfigureAwait(false);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>The name of the fake merge tool (<c>merge.tool</c>) that <see cref="ConfigureFakeMergeToolAsync"/> sets up.</summary>
    public const string FakeMergeTool = "fake";

    /// <summary>The script of the fake merge tool, in the repository's (common) git folder.</summary>
    public const string FakeMergeToolScript = "vc-fake-merge-tool.sh";

    /// <summary>
    /// How long a test waits for <c>git mergetool</c> to start a tool, or to end a run of the fake
    /// one: 120 s. The command is a shell script that, before it runs the tool, starts about 30
    /// git processes and forks its shell (Git for Windows' MSYS <c>sh</c>) about a hundred times
    /// for its command substitutions: 2.8 s alone on the development laptop (Git 2.36), where
    /// <c>git status</c> takes 0.09 s. Under load every process start grows: with four of the
    /// default run's test projects side by side the script took 32 to 64 s (11 to 23 times as
    /// long) while <c>git status</c> took 0.9 to 2.6 s, and in the full default run (seven
    /// projects) the start passed the 60 s that phase 4's checks and tests gave it, and the 30 s of
    /// phase 5's A2. The limit is about twice the longest seen; a test that waits this long must
    /// still end within the hang watchdog's 3 minutes.
    /// </summary>
    public static readonly TimeSpan MergeToolTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Sets up a fake external merge tool in <paramref name="repo"/>'s own configuration, for check
    /// C16 (docs/phase-4-design.md 11.9, 13.1): <c>merge.tool=fake</c>;
    /// <c>mergetool.fake.cmd</c>, which runs the script <see cref="FakeMergeToolScript"/> in the
    /// repository's git folder on <c>$MERGED</c>: it writes
    /// <see cref="ConflictsFiles.FakeMergeToolResult"/> to the file and exits 0, at once and
    /// without a window; and <c>mergetool.fake.trustExitCode=true</c>, so that git takes the exit
    /// code for the result (and stages the file) rather than the file's time. The command finds
    /// the script through <c>git rev-parse --git-common-dir</c> in the worktree's top folder,
    /// where git runs it, so it holds for a copy of the repository too.
    /// </summary>
    public static async Task ConfigureFakeMergeToolAsync(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var script =
            "#!/bin/sh\n" +
            "# The fake merge tool of VisualCommit's tests: writes known content to the file it is given and succeeds.\n" +
            "printf '%s\\n' '" + ConflictsFiles.FakeMergeToolResult.TrimEnd('\n') + "' > \"$1\"\n" +
            "exit 0\n";
        await File.WriteAllBytesAsync(Path.Combine(repo.Path, ".git", FakeMergeToolScript), Encoding.UTF8.GetBytes(script)).ConfigureAwait(false);
        await repo.ConfigAsync("merge.tool", FakeMergeTool).ConfigureAwait(false);
        await repo.ConfigAsync($"mergetool.{FakeMergeTool}.cmd", $"sh \"$(git rev-parse --git-common-dir)/{FakeMergeToolScript}\" \"$MERGED\"").ConfigureAwait(false);
        await repo.ConfigAsync($"mergetool.{FakeMergeTool}.trustExitCode", "true").ConfigureAwait(false);
    }

    /// <summary>The conflicts scenario's files, as committed on each branch and as git leaves them at each stop.</summary>
    public static class ConflictsFiles
    {
        public const string Readme = "# Conflicts\n";

        /// <summary>notes.txt as Base commits it: the twelve lines "line 1" to "line 12".</summary>
        public static string Notes { get; } = NotesWith();

        /// <summary>notes.txt on main (Ours edits): lines 2 and 10 changed.</summary>
        public static string NotesOurs { get; } = NotesWith((2, "line 2 ours"), (10, "line 10 ours"));

        /// <summary>notes.txt on feature/theirs (Theirs edits): lines 2 and 10 changed otherwise, and line 6, which merges cleanly.</summary>
        public static string NotesTheirs { get; } = NotesWith((2, "line 2 theirs"), (6, "line 6 theirs"), (10, "line 10 theirs"));

        /// <summary>notes.txt after First change on feature/rebase: line 2 changed.</summary>
        public static string NotesFirst { get; } = NotesWith((2, "line 2 first"));

        /// <summary>notes.txt after Second change on feature/rebase: line 10 changed too.</summary>
        public static string NotesSecond { get; } = NotesWith((2, "line 2 first"), (10, "line 10 second"));

        /// <summary>notes.txt after Pick conflicting on feature/picks: line 2 changed.</summary>
        public static string NotesPicked { get; } = NotesWith((2, "line 2 picked"));

        public const string Other = "other file\n";

        /// <summary>other.txt on main (Ours edits); feature/kinds deletes the file.</summary>
        public const string OtherOurs = "other file\nchanged on main\n";

        /// <summary>new.txt as main adds it (Ours edits).</summary>
        public const string NewOurs = "new file\nadded on main\n";

        /// <summary>new.txt as feature/kinds adds it (Kinds edits): the first line the same, the second another.</summary>
        public const string NewKinds = "new file\nadded on feature/kinds\n";

        /// <summary>pick.txt as Pick clean adds it.</summary>
        public const string Pick = "pick clean\n";

        /// <summary>last.txt as Pick last adds it.</summary>
        public const string Last = "pick last\n";

        public static TestImages.Rgb White { get; } = new(255, 255, 255);

        /// <summary>image.png's colour at Base: indigo.</summary>
        public static TestImages.Rgb ImageBaseColour { get; } = TestImages.Rgb.Parse("#4353D8");

        /// <summary>image.png's colour on main (Ours edits): green.</summary>
        public static TestImages.Rgb ImageOursColour { get; } = TestImages.Rgb.Parse("#1E8E5A");

        /// <summary>image.png's colour on feature/kinds (Kinds edits): red.</summary>
        public static TestImages.Rgb ImageKindsColour { get; } = TestImages.Rgb.Parse("#C93A46");

        /// <summary>image.png at Base: 32×32 indigo with a white 8×8 square in the middle.</summary>
        public static byte[] ImageBase { get; } = TestImages.SquareOn(32, 32, ImageBaseColour, White, 8);

        /// <summary>image.png on main: 32×32 green with a white 8×8 square in the middle.</summary>
        public static byte[] ImageOurs { get; } = TestImages.SquareOn(32, 32, ImageOursColour, White, 8);

        /// <summary>image.png on feature/kinds: 32×32 red with a white 8×8 square in the middle.</summary>
        public static byte[] ImageKinds { get; } = TestImages.SquareOn(32, 32, ImageKindsColour, White, 8);

        /// <summary>
        /// notes.txt as <c>git merge feature/theirs</c> leaves it: two blocks, at lines 2 and 10,
        /// labelled <c>HEAD</c> and <c>feature/theirs</c>, with line 6 merged cleanly between them.
        /// </summary>
        public static string NotesMergeConflicted { get; } = string.Concat(
            Lines(1, 1),
            Block("line 2 ours", "line 2 theirs", "feature/theirs"),
            Lines(3, 5),
            "line 6 theirs\n",
            Lines(7, 9),
            Block("line 10 ours", "line 10 theirs", "feature/theirs"),
            Lines(11, 12));

        /// <summary>
        /// notes.txt as the rebase leaves it at First change: one block at line 2, ours (HEAD, main)
        /// against theirs (the user's commit), labelled <c>&lt;short SHA&gt; (First change)</c>.
        /// </summary>
        public static string NotesRebaseFirstConflicted(string firstShort) => string.Concat(
            Lines(1, 1),
            Block("line 2 ours", "line 2 first", $"{firstShort} (First change)"),
            Lines(3, 9),
            "line 10 ours\n",
            Lines(11, 12));

        /// <summary>notes.txt once the rebase's first block is resolved to its theirs side: what is committed as First change.</summary>
        public static string NotesRebaseFirstResolved { get; } = NotesWith((2, "line 2 first"), (10, "line 10 ours"));

        /// <summary>notes.txt as the rebase leaves it at Second change, after the first block was resolved to its theirs side: one block at line 10.</summary>
        public static string NotesRebaseSecondConflicted(string secondShort) => string.Concat(
            Lines(1, 1),
            "line 2 first\n",
            Lines(3, 9),
            Block("line 10 ours", "line 10 second", $"{secondShort} (Second change)"),
            Lines(11, 12));

        /// <summary>notes.txt as the cherry-pick list leaves it at Pick conflicting: one block at line 2.</summary>
        public static string NotesPicksConflicted(string pickShort) => string.Concat(
            Lines(1, 1),
            Block("line 2 ours", "line 2 picked", $"{pickShort} (Pick conflicting)"),
            Lines(3, 9),
            "line 10 ours\n",
            Lines(11, 12));

        /// <summary>new.txt as <c>git merge feature/kinds</c> leaves it: the common first line, then one block.</summary>
        public static string NewKindsConflicted { get; } = "new file\n" + Block("added on main", "added on feature/kinds", "feature/kinds");

        /// <summary>What the fake merge tool (<see cref="ConfigureFakeMergeToolAsync"/>) writes to the file it resolves.</summary>
        public const string FakeMergeToolResult = "Resolved by the fake merge tool.\n";

        /// <summary>The twelve lines of notes.txt with the given lines replaced.</summary>
        private static string NotesWith(params (int Line, string Text)[] changes) =>
            string.Concat(Enumerable.Range(1, 12).Select(line =>
                (changes.Where(change => change.Line == line).Select(change => change.Text).FirstOrDefault() ?? $"line {line}") + "\n"));

        /// <summary>The lines "line <paramref name="from"/>" to "line <paramref name="to"/>", each ended by a line feed.</summary>
        private static string Lines(int from, int to) =>
            string.Concat(Enumerable.Range(from, to - from + 1).Select(line => $"line {line}\n"));

        /// <summary>A conflict block in git's default style and marker size: ours labelled HEAD, then theirs with its label.</summary>
        private static string Block(string ours, string theirs, string theirsLabel) =>
            $"<<<<<<< HEAD\n{ours}\n=======\n{theirs}\n>>>>>>> {theirsLabel}\n";
    }

    private static async Task<TempRepo> BuildRebaseAsync()
    {
        var repo = await TempRepo.CreateAsync("rebase");
        try
        {
            await repo.CommitFileAsync("README.md", RebaseFiles.Readme, "Initial commit");
            await repo.CommitFileAsync("src/core.txt", RebaseFiles.Core, "Add core");
            await repo.CreateBranchAsync("feature/topic");
            await repo.CreateBranchAsync("feature/other");

            await repo.CheckoutAsync("feature/topic");
            await repo.CommitFileAsync("topic/one.txt", RebaseFiles.TopicOne, "Topic one");
            await repo.CommitFileAsync("topic/two.txt", RebaseFiles.TopicTwo, "Topic two");

            await repo.CheckoutAsync("main");
            await repo.CommitFileAsync("src/core.txt", RebaseFiles.CoreMainWork, "Main work");
            await repo.CommitFileAsync("docs/main.txt", RebaseFiles.MainMore, "Main more");

            await repo.CheckoutAsync("feature/other");
            await repo.CommitFileAsync("other/one.txt", RebaseFiles.OtherOne, "Other one");

            await repo.AddBareRemoteAsync("origin");
            await repo.PushAsync("origin", setUpstream: true, "main", "feature/topic");
            await repo.CheckoutAsync("feature/topic");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<TempRepo> BuildInteractiveAsync()
    {
        var repo = await TempRepo.CreateAsync("interactive");
        try
        {
            await repo.CommitFileAsync("README.md", InteractiveFiles.Readme, "Initial commit");
            await repo.CommitFileAsync("src/base.txt", InteractiveFiles.Base, "Base");
            await repo.CommitFileAsync("src/one.txt", InteractiveFiles.One, "One");
            await repo.CommitFileAsync("src/two.txt", InteractiveFiles.Two, "Two");
            await repo.CommitFileAsync("src/one.txt", InteractiveFiles.OneFixed, "Fix one");

            // git commit --message keeps a line that starts with "#" (its cleanup is "whitespace"
            // when no editor runs); the pinning test proves it.
            await repo.CommitFileAsync("src/three.txt", InteractiveFiles.Three, InteractiveFiles.ThreeMessage.TrimEnd('\n'));
            await repo.CommitFileAsync("src/four.txt", InteractiveFiles.Four, "Four");
            await repo.CommitFileAsync("src/five.txt", InteractiveFiles.Five, "Five");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<TempRepo> BuildConflictsAsync()
    {
        var repo = await TempRepo.CreateAsync("conflicts");
        try
        {
            await repo.CommitFileAsync("README.md", ConflictsFiles.Readme, "Initial commit");
            await repo
                .WriteFile("notes.txt", ConflictsFiles.Notes)
                .WriteFile("other.txt", ConflictsFiles.Other)
                .WriteBytes("image.png", ConflictsFiles.ImageBase)
                .CommitAsync("Base");
            foreach (var branch in new[] { "feature/theirs", "feature/rebase", "feature/picks", "feature/kinds" })
            {
                await repo.CreateBranchAsync(branch);
            }

            await repo.CheckoutAsync("feature/theirs");
            await repo.CommitFileAsync("notes.txt", ConflictsFiles.NotesTheirs, "Theirs edits");

            await repo.CheckoutAsync("feature/rebase");
            await repo.CommitFileAsync("notes.txt", ConflictsFiles.NotesFirst, "First change");
            await repo.CommitFileAsync("notes.txt", ConflictsFiles.NotesSecond, "Second change");

            await repo.CheckoutAsync("feature/picks");
            await repo.CommitFileAsync("pick.txt", ConflictsFiles.Pick, "Pick clean");
            await repo.CommitFileAsync("notes.txt", ConflictsFiles.NotesPicked, "Pick conflicting");
            await repo.CommitFileAsync("last.txt", ConflictsFiles.Last, "Pick last");

            await repo.CheckoutAsync("feature/kinds");
            await repo
                .DeleteFile("other.txt")
                .WriteBytes("image.png", ConflictsFiles.ImageKinds)
                .WriteFile("new.txt", ConflictsFiles.NewKinds)
                .CommitAsync("Kinds edits");

            await repo.CheckoutAsync("main");
            await repo
                .WriteFile("notes.txt", ConflictsFiles.NotesOurs)
                .WriteFile("other.txt", ConflictsFiles.OtherOurs)
                .WriteBytes("image.png", ConflictsFiles.ImageOurs)
                .WriteFile("new.txt", ConflictsFiles.NewOurs)
                .CommitAsync("Ours edits");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }
}
