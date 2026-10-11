using System.Text;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="PatchBuilder"/> against real git: small random files, random edits (a missing line
/// feed at the end among them) and a random choice of changed lines. Every patch must apply, and
/// give the lines kept and chosen in order, each with a line feed except the file's last line,
/// which keeps the ending it had. All files of a run go through git at once, so a run takes a few
/// calls of git, not hundreds.
/// </summary>
public class PatchFuzzTests
{
    private const int FileCount = 100;

    private static readonly string[] Words = ["alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta"];

    [Theory]
    [InlineData(PatchDirection.Forward, 2026)]
    [InlineData(PatchDirection.Reverse, 1010)]
    public async Task Patches_of_random_choices_apply_and_give_the_chosen_lines(PatchDirection direction, int seed)
    {
        var random = new Random(seed);
        using var repo = await TempRepo.CreateAsync("fuzz");
        var files = new List<(string Path, FileText Old, FileText New)>();
        for (var i = 0; i < FileCount; i++)
        {
            var old = RandomText(random);
            files.Add(($"f{i:D3}.txt", old, Edit(old, random)));
        }

        // The old files are committed and put in the index without being written as files, and
        // the new ones written: quicker than committing hundreds of files the usual way.
        await ImportAsync(repo, files.Select(file => (file.Path, file.Old.ToString())));
        await repo.GitAsync("read-tree", "HEAD");
        foreach (var (path, _, changed) in files)
        {
            repo.WriteFile(path, changed.ToString());
        }

        var diffs = await ReadDiffsAsync(repo);
        var repository = await OpenAsync(repo);
        var patches = new List<(string Path, string Patch, string Expected, string Old, string New)>();
        foreach (var (path, old, changed) in files)
        {
            if (!diffs.TryGetValue(path, out var diff))
            {
                Assert.Equal(old.ToString(), changed.ToString());
                continue;
            }

            var chosen = RandomChoice(diff, random);
            var patch = PatchBuilder.Build(diff, chosen, direction);
            Assert.NotNull(patch);
            var applyTo = direction == PatchDirection.Forward ? old : changed;
            patches.Add((path, patch, Expected(diff, applyTo, chosen, direction), old.ToString(), changed.ToString()));
        }

        try
        {
            await repository.ApplyPatchAsync(string.Concat(patches.Select(patch => patch.Patch)), toIndex: direction == PatchDirection.Forward, direction, TestCancelled);
        }
        catch (GitException error)
        {
            Assert.Fail($"git apply refused the patches: {error.StandardError}\n{await DescribeRefusedAsync(repository, patches, direction)}");
        }

        var results = direction == PatchDirection.Forward ? await ReadIndexAsync(repo, patches.Select(patch => patch.Path)) : null;
        var wrong = new StringBuilder();
        foreach (var (path, patch, expected, old, changed) in patches)
        {
            var actual = results is not null
                ? results[path]
                : Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(repo.Path, path)));
            if (actual != expected)
            {
                wrong.Append(Describe(path, old, changed, patch)).Append("expected: ").Append(Show(expected)).Append("\nactual:   ").Append(Show(actual)).Append("\n\n");
            }
        }

        Assert.True(wrong.Length == 0, wrong.ToString());
    }

    [Theory]
    [InlineData(PatchDirection.Forward, "x\ny", "x\ny\nz", "+z", "x\ny\nz")]
    [InlineData(PatchDirection.Forward, "a", "a\nb\n", "+b", "a\nb\n")]
    [InlineData(PatchDirection.Reverse, "x\ny", "x\ny\nz", "-y", "x\ny\ny\nz")]
    [InlineData(PatchDirection.Reverse, "x\ny", "x\ny\nz", "+z", "x\ny\n")]
    public async Task A_line_without_a_line_feed_gets_one_when_a_chosen_line_follows_it(
        PatchDirection direction, string old, string changed, string choice, string expected)
    {
        using var repo = await TempRepo.CreateAsync("no-newline");
        await repo.CommitFileAsync("file.txt", old, "Old");
        repo.WriteFile("file.txt", changed);
        var repository = await OpenAsync(repo);
        var diff = (await ReadDiffsAsync(repo))["file.txt"];
        var kind = choice[0] == '+' ? DiffLineKind.Added : DiffLineKind.Removed;
        var chosen = diff.Hunks[0].Lines.Select((line, index) => (line, index)).Single(pair => pair.line.Kind == kind && pair.line.Raw == choice[1..]).index;

        var patch = PatchBuilder.Build(diff, new HashSet<DiffLineRef> { new(0, chosen) }, direction)!;
        await repository.ApplyPatchAsync(patch, toIndex: direction == PatchDirection.Forward, direction, TestCancelled);

        var actual = direction == PatchDirection.Forward
            ? (await ReadIndexAsync(repo, ["file.txt"]))["file.txt"]
            : File.ReadAllText(Path.Combine(repo.Path, "file.txt"));
        Assert.Equal(expected, actual);
    }

    /// <summary>A file as lines, and whether its last line ends with a line feed.</summary>
    private sealed record FileText(IReadOnlyList<string> Lines, bool FinalNewline)
    {
        public override string ToString() =>
            Lines.Count == 0 ? string.Empty : string.Join('\n', Lines) + (FinalNewline ? "\n" : string.Empty);
    }

    private static FileText RandomText(Random random) =>
        new([.. Enumerable.Range(0, random.Next(0, 16)).Select(_ => Words[random.Next(Words.Length)])], random.Next(2) == 0);

    /// <summary>One to three insertions, deletions or replacements, and sometimes the other ending.</summary>
    private static FileText Edit(FileText text, Random random)
    {
        var lines = text.Lines.ToList();
        for (var edits = random.Next(1, 4); edits > 0; edits--)
        {
            switch (random.Next(3))
            {
                case 0:
                    lines.Insert(random.Next(lines.Count + 1), Words[random.Next(Words.Length)]);
                    break;
                case 1 when lines.Count > 0:
                    lines.RemoveAt(random.Next(lines.Count));
                    break;
                case 2 when lines.Count > 0:
                    lines[random.Next(lines.Count)] = Words[random.Next(Words.Length)];
                    break;
            }
        }

        return new FileText(lines, random.Next(3) == 0 ? !text.FinalNewline : text.FinalNewline);
    }

    /// <summary>Each changed line with even odds, at least one.</summary>
    private static HashSet<DiffLineRef> RandomChoice(FileDiff diff, Random random)
    {
        var changes = diff.Hunks
            .SelectMany((hunk, h) => hunk.Lines.Select((line, l) => (line, Ref: new DiffLineRef(h, l))))
            .Where(pair => pair.line.IsChange)
            .Select(pair => pair.Ref)
            .ToList();
        var chosen = changes.Where(_ => random.Next(2) == 0).ToHashSet();
        if (chosen.Count == 0)
        {
            chosen.Add(changes[random.Next(changes.Count)]);
        }

        return chosen;
    }

    /// <summary>
    /// What the side applied to becomes: its lines outside the hunks, and in the hunks the context,
    /// the unchosen changes that are on it and the chosen ones that are not, in the diff's order.
    /// </summary>
    private static string Expected(FileDiff diff, FileText applyTo, IReadOnlySet<DiffLineRef> chosen, PatchDirection direction)
    {
        var forward = direction == PatchDirection.Forward;
        var onAppliedSide = forward ? DiffLineKind.Removed : DiffLineKind.Added;
        var result = new List<(string Text, bool NoNewline)>();
        var next = 0;
        void CopyUpTo(int count)
        {
            for (; next < count; next++)
            {
                result.Add((applyTo.Lines[next], next == applyTo.Lines.Count - 1 && !applyTo.FinalNewline));
            }
        }

        for (var h = 0; h < diff.Hunks.Count; h++)
        {
            var hunk = diff.Hunks[h];
            var (start, count) = forward ? (hunk.OldStart, hunk.OldCount) : (hunk.NewStart, hunk.NewCount);
            CopyUpTo(count == 0 ? start : start - 1);
            for (var l = 0; l < hunk.Lines.Count; l++)
            {
                var line = hunk.Lines[l];
                if (line.Kind == DiffLineKind.Context || line.Kind == onAppliedSide)
                {
                    next++;
                }

                var isChosen = chosen.Contains(new DiffLineRef(h, l));
                var kept = line.Kind == DiffLineKind.Context || (line.Kind == onAppliedSide) != isChosen;
                if (kept)
                {
                    result.Add((line.Raw, line.NoNewlineAtEnd));
                }
            }
        }

        CopyUpTo(applyTo.Lines.Count);
        var text = new StringBuilder();
        for (var i = 0; i < result.Count; i++)
        {
            text.Append(result[i].Text);
            if (i < result.Count - 1 || !result[i].NoNewline)
            {
                text.Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>Every changed file's diff against the index, as the app reads one, from one call of git.</summary>
    private static async Task<Dictionary<string, FileDiff>> ReadDiffsAsync(TempRepo repo)
    {
        var command = new GitCommand("diff", "--no-color", "--no-ext-diff", "--no-textconv", "--src-prefix=a/", "--dst-prefix=b/", "--unified=3")
        {
            WorkingDirectory = repo.Path,
            Encoding = GitCommand.Latin1,
        };
        var output = (await NewRunner().RunAsync(command, TestCancelled)).EnsureSuccess(command).StandardOutput;
        var diffs = new Dictionary<string, FileDiff>();
        foreach (var part in output.Split("diff --git ", StringSplitOptions.RemoveEmptyEntries))
        {
            var firstLine = part[..part.IndexOf('\n', StringComparison.Ordinal)];
            diffs[firstLine[(firstLine.IndexOf(" b/", StringComparison.Ordinal) + 3)..]] = DiffParser.Parse("diff --git " + part);
        }

        return diffs;
    }

    /// <summary>Commits the files on the repo's branch with one call of git fast-import.</summary>
    private static async Task ImportAsync(TempRepo repo, IEnumerable<(string Path, string Content)> files)
    {
        var stream = new StringBuilder("commit refs/heads/main\ncommitter Test Author <author@example.com> 1767268800 +0000\ndata 3\nOld\n");
        foreach (var (path, content) in files)
        {
            // The contents are ASCII: one byte per character.
            stream.Append("M 100644 inline ").Append(path).Append("\ndata ").Append(content.Length).Append('\n').Append(content).Append('\n');
        }

        await repo.GitWithInputAsync(stream.Append('\n').ToString(), "fast-import", "--quiet");
    }

    /// <summary>The files' contents in the index, byte for byte, from one call of git.</summary>
    private static async Task<Dictionary<string, string>> ReadIndexAsync(TempRepo repo, IEnumerable<string> paths)
    {
        var names = paths.ToList();
        var command = new GitCommand("cat-file", "--batch")
        {
            WorkingDirectory = repo.Path,
            StandardInput = string.Concat(names.Select(path => ":" + path + "\n")),
            Encoding = GitCommand.Latin1,
        };
        var output = (await NewRunner().RunAsync(command, TestCancelled)).EnsureSuccess(command).StandardOutput;

        // Each object: "<id> blob <size>\n", its bytes, "\n".
        var contents = new Dictionary<string, string>();
        var at = 0;
        foreach (var path in names)
        {
            var headerEnd = output.IndexOf('\n', at);
            var size = int.Parse(output[(output.LastIndexOf(' ', headerEnd) + 1)..headerEnd], System.Globalization.CultureInfo.InvariantCulture);
            contents[path] = output.Substring(headerEnd + 1, size);
            at = headerEnd + 1 + size + 1;
        }

        return contents;
    }

    /// <summary>The patches git refuses one by one, for the failure message.</summary>
    private static async Task<string> DescribeRefusedAsync(
        GitRepository repository,
        List<(string Path, string Patch, string Expected, string Old, string New)> patches,
        PatchDirection direction)
    {
        var refused = new StringBuilder();
        foreach (var (path, patch, _, old, changed) in patches)
        {
            try
            {
                await repository.ApplyPatchAsync(patch, toIndex: direction == PatchDirection.Forward, direction, TestCancelled);
            }
            catch (GitException error)
            {
                refused.Append(Describe(path, old, changed, patch)).Append("git: ").Append(error.StandardError.Trim()).Append("\n\n");
            }
        }

        return refused.ToString();
    }

    private static string Describe(string path, string old, string changed, string patch) =>
        $"{path}\nold: {Show(old)}\nnew: {Show(changed)}\npatch:\n{patch}";

    private static string Show(string text) => "\"" + text.Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
}
