using System.Text;
using VisualCommit.Core.Conflicts;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The resolver's git members (docs/phase-4-design.md 0.4, 11.3, 11.6 to 11.8, 5.1 "Markers") on
/// every kind of conflict git makes: what is read of a conflicted file and its sides, how a file is
/// marked resolved with the result, with one side or by deleting it, and that no edit the user
/// made to the working-tree file is lost on the way (a snapshot under
/// <c>refs/visualcommit/backup/discard-…</c> when git would overwrite or delete it). Conflicts are
/// made by real merges where every git makes the same kind, and by unmerged index entries written
/// with <c>git update-index --index-info</c> for a rename/rename's kinds and a submodule, as
/// <see cref="ConflictStatusTests"/> does. The merge tool is <see cref="MergeToolTests"/>'.
/// </summary>
public class ConflictOperationTests
{
    private const string BaseText = "line 1\nline 2\nline 3\n";
    private const string OursText = "line 1\nline 2 ours\nline 3\n";
    private const string TheirsText = "line 1\nline 2 theirs\nline 3\n";

    [Fact]
    public async Task A_text_conflict_is_read_as_the_working_tree_file_with_gits_markers()
    {
        using var repo = await MergeAsync(r => r.WriteFile("f.txt", BaseText), r => r.WriteFile("f.txt", OursText), r => r.WriteFile("f.txt", TheirsText));
        var repository = await OpenAsync(repo);
        var entry = await ConflictAsync(repository, "f.txt");

        var content = await repository.ReadConflictAsync(entry, cancellationToken: TestCancelled);

        Assert.Equal(ConflictKind.BothModified, entry.Kind);
        Assert.True(content.WorkingTreeExists);
        Assert.Equal(File.ReadAllBytes(Path.Combine(repo.Path, "f.txt")), content.WorkingTree);
        Assert.Equal(7, content.MarkerSize);
        Assert.False(content.OursBinary);
        Assert.False(content.TheirsBinary);
        Assert.False(content.IsSymbolicLink);

        var text = ConflictText.Parse(ConflictText.Decode(content.WorkingTree).Text, content.MarkerSize);
        var block = Assert.Single(text.Blocks);
        Assert.Equal("HEAD", block.OursLabel);
        Assert.Equal("theirs", block.TheirsLabel);
        Assert.Equal(OursText, text.OursText);
        Assert.Equal(TheirsText, text.TheirsText);
    }

    [Fact]
    public async Task The_sides_are_read_whole_and_a_side_without_the_file_is_null()
    {
        using var repo = await MergeAsync(
            r => r.WriteFile("f.txt", BaseText).WriteFile("ud.txt", "base\n"),
            r => r.WriteFile("f.txt", OursText).WriteFile("ud.txt", "ours\n"),
            r => r.WriteFile("f.txt", TheirsText).DeleteFile("ud.txt"));
        var repository = await OpenAsync(repo);
        var both = await ConflictAsync(repository, "f.txt");
        var deletedByThem = await ConflictAsync(repository, "ud.txt");

        Assert.Equal(OursText, Encoding.UTF8.GetString((await repository.ReadConflictSideAsync(both, ConflictSide.Ours, cancellationToken: TestCancelled))!));
        Assert.Equal(TheirsText, Encoding.UTF8.GetString((await repository.ReadConflictSideAsync(both, ConflictSide.Theirs, cancellationToken: TestCancelled))!));
        Assert.Equal(ConflictKind.DeletedByThem, deletedByThem.Kind);
        Assert.Equal("ours\n", Encoding.UTF8.GetString((await repository.ReadConflictSideAsync(deletedByThem, ConflictSide.Ours, cancellationToken: TestCancelled))!));
        Assert.Null(await repository.ReadConflictSideAsync(deletedByThem, ConflictSide.Theirs, cancellationToken: TestCancelled));

        // Reading writes nothing: the conflict is as git left it.
        Assert.Equal(["UU f.txt", "UD ud.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Theory]
    [InlineData("merge", false)]
    [InlineData("diff3", true)]
    [InlineData("zdiff3", true)]
    public async Task Each_conflict_style_git_writes_parses_into_its_block(string style, bool hasBase)
    {
        using var repo = await MergeAsync(
            r => r.WriteFile("f.txt", BaseText),
            r => r.WriteFile("f.txt", OursText),
            r => r.WriteFile("f.txt", TheirsText),
            "-c", $"merge.conflictStyle={style}");
        var repository = await OpenAsync(repo);

        var content = await repository.ReadConflictAsync(await ConflictAsync(repository, "f.txt"), cancellationToken: TestCancelled);

        var block = Assert.Single(ConflictText.Parse(ConflictText.Decode(content.WorkingTree).Text, content.MarkerSize).Blocks);
        Assert.Equal(["line 2 ours"], block.Ours.Select(line => line.Text));
        Assert.Equal(["line 2 theirs"], block.Theirs.Select(line => line.Text));
        Assert.Equal(hasBase, block.Base is not null);
        if (hasBase)
        {
            Assert.Equal(["line 2"], block.Base!.Select(line => line.Text));
        }
    }

    [Fact]
    public async Task The_conflict_marker_size_attribute_is_read_for_the_path()
    {
        using var repo = await MergeAsync(
            r => r.WriteFile(".gitattributes", "*.md conflict-marker-size=9\n").WriteFile("f.md", BaseText).WriteFile("f.txt", BaseText),
            r => r.WriteFile("f.md", OursText).WriteFile("f.txt", OursText),
            r => r.WriteFile("f.md", TheirsText).WriteFile("f.txt", TheirsText));
        var repository = await OpenAsync(repo);

        var markdown = await repository.ReadConflictAsync(await ConflictAsync(repository, "f.md"), cancellationToken: TestCancelled);
        var text = await repository.ReadConflictAsync(await ConflictAsync(repository, "f.txt"), cancellationToken: TestCancelled);

        Assert.Equal(9, markdown.MarkerSize);
        Assert.Equal(7, text.MarkerSize);
        var nine = ConflictText.Decode(markdown.WorkingTree).Text;
        Assert.Contains("<<<<<<<<< HEAD\n", nine, StringComparison.Ordinal);
        Assert.Single(ConflictText.Parse(nine, markdown.MarkerSize).Blocks);
        Assert.False(ConflictText.Parse(nine, 7).HasMarkers);
    }

    [Fact]
    public async Task Under_core_autocrlf_the_file_and_its_sides_have_crlf_and_a_crlf_result_is_staged_with_lf()
    {
        using var repo = await MergeAsync(
            r => r.WriteFile("f.txt", BaseText),
            r => r.WriteFile("f.txt", OursText),
            r => r.WriteFile("f.txt", TheirsText),
            "-c", "core.autocrlf=true");
        var repository = await OpenAsync(repo);
        var entry = await ConflictAsync(repository, "f.txt");

        var content = await repository.ReadConflictAsync(entry, cancellationToken: TestCancelled);
        var ours = await repository.ReadConflictSideAsync(entry, ConflictSide.Ours, cancellationToken: TestCancelled);

        var text = ConflictText.Parse(ConflictText.Decode(content.WorkingTree).Text, content.MarkerSize);
        var block = Assert.Single(text.Blocks);
        Assert.All(text.Lines, line => Assert.Equal("\r\n", line.Ending));
        Assert.Equal("theirs", block.TheirsLabel);
        Assert.Equal(OursText.Replace("\n", "\r\n", StringComparison.Ordinal), Encoding.UTF8.GetString(ours!));

        var result = Encoding.UTF8.GetBytes(text.Choose(0, BlockChoice.TheirsThenOurs));
        await repository.WriteResolutionAsync("f.txt", result, cancellationToken: TestCancelled);

        Assert.Equal(result, File.ReadAllBytes(Path.Combine(repo.Path, "f.txt")));
        Assert.Equal("line 1\nline 2 theirs\nline 2 ours\nline 3\n", await ShowFileAsync(repo, string.Empty, "f.txt"));
        Assert.Equal(["M  f.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task A_result_is_written_byte_for_byte_and_staged_as_it_is()
    {
        using var repo = await MergeAsync(r => r.WriteFile("f.txt", BaseText), r => r.WriteFile("f.txt", OursText), r => r.WriteFile("f.txt", TheirsText));
        var repository = await OpenAsync(repo);

        // Latin-1 (not valid UTF-8), CRLF and LF lines, a conflict marker kept on purpose, no final line end.
        byte[] result = [0x63, 0x61, 0x66, 0xE9, 0x0D, 0x0A, .. "<<<<<<< kept\n"u8, 0xFF, 0x00, 0x41];
        await repository.WriteResolutionAsync("f.txt", result, cancellationToken: TestCancelled);

        Assert.Equal(result, File.ReadAllBytes(Path.Combine(repo.Path, "f.txt")));
        Assert.Equal(await HashAsync(repo, result), await RevAsync(repo, ":f.txt"));
        Assert.Empty(await GitLinesAsync(repo, "ls-files", "--unmerged"));
    }

    [Fact]
    public async Task A_result_is_not_written_through_a_symbolic_link()
    {
        using var repo = await MergeAsync(r => r.WriteFile("f.txt", BaseText), r => r.WriteFile("f.txt", OursText), r => r.WriteFile("f.txt", TheirsText));
        var outside = Sibling(repo, "outside.txt");
        File.WriteAllText(outside, "outside\n");
        var link = Path.Combine(repo.Path, "f.txt");
        File.Delete(link);
        try
        {
            File.CreateSymbolicLink(link, outside);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // This machine cannot make links (Windows without the privilege): there is nothing to
            // refuse here; CI's runners make them.
            return;
        }

        var repository = await OpenAsync(repo);

        await Assert.ThrowsAsync<IOException>(() => repository.WriteResolutionAsync("f.txt", "result\n"u8.ToArray(), cancellationToken: TestCancelled));

        Assert.Equal("outside\n", File.ReadAllText(outside));
        Assert.Single(await GitLinesAsync(repo, "status", "--porcelain"), "UU f.txt");
    }

    [Fact]
    public async Task A_binary_conflict_is_read_as_binary_and_git_left_ours_without_markers()
    {
        using var repo = await MergeAsync(
            r => r.WriteBytes("bin.dat", Binary("base")),
            r => r.WriteBytes("bin.dat", Binary("ours")),
            r => r.WriteBytes("bin.dat", Binary("theirs")));
        var repository = await OpenAsync(repo);

        var content = await repository.ReadConflictAsync(await ConflictAsync(repository, "bin.dat"), cancellationToken: TestCancelled);

        Assert.True(content.OursBinary);
        Assert.True(content.TheirsBinary);
        Assert.False(content.IsSymbolicLink);
        Assert.Equal(Binary("ours"), content.WorkingTree);
    }

    [Fact]
    public async Task A_side_is_binary_with_a_nul_in_its_first_8000_bytes_only()
    {
        // Ours has a NUL at byte 7999, theirs at byte 8000: git calls ours binary and theirs text.
        var lines = string.Concat(Enumerable.Repeat("xxxxxxx\n", 999));
        using var repo = await MergeAsync(
            _ => { },
            r => r.WriteFile("f.dat", lines + "xxxxxxx\0\n"),
            r => r.WriteFile("f.dat", lines + "xxxxxxx\n\0\n"));
        var repository = await OpenAsync(repo);
        var entry = await ConflictAsync(repository, "f.dat");

        var content = await repository.ReadConflictAsync(entry, cancellationToken: TestCancelled);

        Assert.Equal(ConflictKind.BothAdded, entry.Kind);
        Assert.True(content.OursBinary);
        Assert.False(content.TheirsBinary);
    }

    [Fact]
    public async Task Taking_a_binary_side_over_an_edited_file_saves_the_edit_in_a_snapshot_first()
    {
        using var repo = await MergeAsync(
            r => r.WriteBytes("bin.dat", Binary("base")),
            r => r.WriteBytes("bin.dat", Binary("ours")),
            r => r.WriteBytes("bin.dat", Binary("theirs")));
        repo.WriteBytes("bin.dat", Binary("edited by hand"));
        var repository = await OpenAsync(repo);

        await repository.TakeSideAsync("bin.dat", ConflictSide.Theirs, cancellationToken: TestCancelled);

        Assert.Equal(Binary("theirs"), File.ReadAllBytes(Path.Combine(repo.Path, "bin.dat")));
        Assert.Equal(await RevAsync(repo, "theirs:bin.dat"), await RevAsync(repo, ":bin.dat"));
        Assert.Empty(await GitLinesAsync(repo, "ls-files", "--unmerged"));
        var snapshot = Assert.Single(await repo.BackupRefsAsync());
        Assert.StartsWith("refs/visualcommit/backup/discard-", snapshot.Name, StringComparison.Ordinal);
        Assert.Equal(await HashAsync(repo, Binary("edited by hand")), await RevAsync(repo, $"{snapshot.Name}:bin.dat"));
    }

    [Fact]
    public async Task Taking_the_side_the_working_tree_already_holds_saves_no_snapshot()
    {
        using var repo = await MergeAsync(
            r => r.WriteBytes("bin.dat", Binary("base")),
            r => r.WriteBytes("bin.dat", Binary("ours")),
            r => r.WriteBytes("bin.dat", Binary("theirs")));
        var repository = await OpenAsync(repo);

        await repository.TakeSideAsync("bin.dat", ConflictSide.Ours, cancellationToken: TestCancelled);

        Assert.Equal(Binary("ours"), File.ReadAllBytes(Path.Combine(repo.Path, "bin.dat")));
        Assert.Equal(await RevAsync(repo, "HEAD:bin.dat"), await RevAsync(repo, ":bin.dat"));
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Empty(await GitLinesAsync(repo, "ls-files", "--unmerged"));
    }

    [Fact]
    public async Task A_symbolic_link_conflict_is_read_as_a_link_and_takes_a_side_as_a_link()
    {
        // core.symlinks as git init found it on this machine: real links, or files holding the target.
        using var repo = await TempRepo.CreateAsync();
        repo.WriteFile("target-base", "base\n").WriteFile("target-ours", "ours\n").WriteFile("target-theirs", "theirs\n");
        await repo.GitAsync("add", "--", "target-base", "target-ours", "target-theirs");
        await CommitLinkAsync(repo, "target-base", "Base");
        await repo.CreateBranchAsync("theirs");
        await CommitLinkAsync(repo, "target-ours", "Ours");
        await repo.CheckoutAsync("theirs");
        await CommitLinkAsync(repo, "target-theirs", "Theirs");
        await repo.CheckoutAsync("main");
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("merge", "--no-edit", "theirs")).ExitCode);
        var repository = await OpenAsync(repo);
        var entry = await ConflictAsync(repository, "link");

        var content = await repository.ReadConflictAsync(entry, cancellationToken: TestCancelled);
        var theirs = await repository.ReadConflictSideAsync(entry, ConflictSide.Theirs, cancellationToken: TestCancelled);

        Assert.Equal(ConflictKind.BothModified, entry.Kind);
        Assert.Equal(("120000", "120000"), (entry.OursMode, entry.TheirsMode));
        Assert.True(content.IsSymbolicLink);
        Assert.False(content.OursBinary || content.TheirsBinary);
        Assert.True(content.WorkingTreeExists);
        Assert.Equal("target-ours", Encoding.UTF8.GetString(content.WorkingTree));
        Assert.Equal("target-theirs", Encoding.UTF8.GetString(theirs!));

        await repository.TakeSideAsync("link", ConflictSide.Theirs, cancellationToken: TestCancelled);

        Assert.Equal([$"120000 {await RevAsync(repo, "theirs:link")} 0\tlink"], await GitLinesAsync(repo, "ls-files", "--stage", "--", "link"));
        var full = Path.Combine(repo.Path, "link");
        Assert.Equal("target-theirs", await SymlinksWorkAsync(repo) ? new FileInfo(full).LinkTarget : File.ReadAllText(full));
        Assert.Equal(["M  link"], await GitLinesAsync(repo, "status", "--porcelain"));

        // The working tree held ours, which differs from the side written: a snapshot keeps it (11.8).
        var snapshot = Assert.Single(await repo.BackupRefsAsync());
        Assert.Equal(await RevAsync(repo, "HEAD:link"), await RevAsync(repo, $"{snapshot.Name}:link"));
    }

    [Fact]
    public async Task Both_added_text_has_markers_and_no_base()
    {
        using var repo = await MergeAsync(_ => { }, r => r.WriteFile("new.txt", "ours\n"), r => r.WriteFile("new.txt", "theirs\n"));
        var repository = await OpenAsync(repo);
        var entry = await ConflictAsync(repository, "new.txt");

        var content = await repository.ReadConflictAsync(entry, cancellationToken: TestCancelled);

        Assert.Equal(ConflictKind.BothAdded, entry.Kind);
        Assert.False(entry.HasBase);
        var block = Assert.Single(ConflictText.Parse(ConflictText.Decode(content.WorkingTree).Text).Blocks);
        Assert.Equal(["ours"], block.Ours.Select(line => line.Text));
        Assert.Equal("theirs\n", Encoding.UTF8.GetString((await repository.ReadConflictSideAsync(entry, ConflictSide.Theirs, cancellationToken: TestCancelled))!));
    }

    [Fact]
    public async Task Keep_the_file_deleted_by_them_stages_it_as_git_left_it_with_a_hand_edit()
    {
        using var repo = await MergeAsync(r => r.WriteFile("ud.txt", "base\n"), r => r.WriteFile("ud.txt", "ours\n"), r => r.DeleteFile("ud.txt"));
        repo.WriteFile("ud.txt", "ours, edited by hand\n");
        var repository = await OpenAsync(repo);
        Assert.Equal(ConflictKind.DeletedByThem, (await ConflictAsync(repository, "ud.txt")).Kind);

        await repository.TakeSideAsync("ud.txt", ConflictSide.Ours, cancellationToken: TestCancelled);

        Assert.Equal("ours, edited by hand\n", ReadText(repo, "ud.txt"));
        Assert.Equal("ours, edited by hand\n", await ShowFileAsync(repo, string.Empty, "ud.txt"));
        Assert.Equal(["M  ud.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task Keep_the_file_writes_the_side_back_only_when_it_is_missing()
    {
        using var repo = await MergeAsync(
            r => r.WriteFile("ud.txt", "base\n").WriteFile("du.txt", "base\n"),
            r => r.WriteFile("ud.txt", "ours\n").DeleteFile("du.txt"),
            r => r.DeleteFile("ud.txt").WriteFile("du.txt", "theirs\n"));
        repo.DeleteFile("ud.txt").DeleteFile("du.txt");
        var repository = await OpenAsync(repo);
        Assert.Equal(ConflictKind.DeletedByUs, (await ConflictAsync(repository, "du.txt")).Kind);

        // A side the conflict lacks: git's own refusal, and nothing changes.
        var refused = await Assert.ThrowsAsync<GitException>(() => repository.TakeSideAsync("du.txt", ConflictSide.Ours, cancellationToken: TestCancelled));
        Assert.Contains("does not have our version", refused.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(repo.Path, "du.txt")));

        await repository.TakeSideAsync("ud.txt", ConflictSide.Ours, cancellationToken: TestCancelled);
        await repository.TakeSideAsync("du.txt", ConflictSide.Theirs, cancellationToken: TestCancelled);

        Assert.Equal("ours\n", ReadText(repo, "ud.txt"));
        Assert.Equal("theirs\n", ReadText(repo, "du.txt"));
        Assert.Equal(["A  du.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task Added_by_one_side_is_kept_and_both_deleted_is_removed()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("kept.txt", "kept\n", "Base");
        var ours = await HashAsync(repo, "ours\n"u8.ToArray());
        var theirs = await HashAsync(repo, "theirs\n"u8.ToArray());
        var gone = await HashAsync(repo, "base\n"u8.ToArray());

        // The unmerged entries a rename/rename leaves (0.4): the old path at stage 1 only, each new
        // path at one side's stage only. Theirs is missing from the working tree.
        await repo.GitWithInputAsync(
            $"100644 {gone} 1\tgone.txt\n100644 {ours} 2\tmoved-ours.txt\n100644 {theirs} 3\tmoved-theirs.txt\n",
            "update-index", "--index-info");
        repo.WriteFile("moved-ours.txt", "ours\n");
        var repository = await OpenAsync(repo);
        Assert.Equal(
            [ConflictKind.BothDeleted, ConflictKind.AddedByUs, ConflictKind.AddedByThem],
            (await repository.ReadStatusAsync(TestCancelled)).Conflicts.Select(conflict => conflict.Kind));

        await repository.TakeSideAsync("moved-ours.txt", ConflictSide.Ours, cancellationToken: TestCancelled);
        await repository.TakeSideAsync("moved-theirs.txt", ConflictSide.Theirs, cancellationToken: TestCancelled);
        await repository.RemoveConflictedAsync("gone.txt", cancellationToken: TestCancelled);

        Assert.Equal("theirs\n", ReadText(repo, "moved-theirs.txt"));
        Assert.Equal(["A  moved-ours.txt", "A  moved-theirs.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task A_file_moved_aside_by_a_file_folder_clash_is_kept_as_git_left_it()
    {
        using var repo = await MergeAsync(_ => { }, r => r.WriteFile("p", "ours file\n"), r => r.WriteFile("p/inner.txt", "theirs folder\n"));
        var repository = await OpenAsync(repo);
        var status = await repository.ReadStatusAsync(TestCancelled);

        // Git moved the file aside to p~<label>, whatever kind the clash gives it.
        var entry = Assert.Single(status.Conflicts);
        Assert.StartsWith("p~", entry.Path, StringComparison.Ordinal);
        Assert.Equal("ours file\n", ReadText(repo, entry.Path));

        await repository.TakeSideAsync(entry.Path, entry.HasOurs ? ConflictSide.Ours : ConflictSide.Theirs, cancellationToken: TestCancelled);

        Assert.Equal("ours file\n", ReadText(repo, entry.Path));
        Assert.Equal("theirs folder\n", ReadText(repo, "p/inner.txt"));
        Assert.Empty(await GitLinesAsync(repo, "ls-files", "--unmerged"));
        Assert.Equal(["keep.txt", "p/inner.txt", entry.Path], await GitLinesAsync(repo, "ls-files"));
    }

    [Fact]
    public async Task Delete_it_removes_the_file_and_keeps_a_hand_edit_in_a_snapshot()
    {
        using var repo = await MergeAsync(
            r => r.WriteFile("edited.txt", "base\n").WriteFile("plain.txt", "base\n"),
            r => r.WriteFile("edited.txt", "ours\n").WriteFile("plain.txt", "ours\n"),
            r => r.DeleteFile("edited.txt").DeleteFile("plain.txt"));
        repo.WriteFile("edited.txt", "ours, edited by hand\n");
        var repository = await OpenAsync(repo);

        await repository.RemoveConflictedAsync("plain.txt", cancellationToken: TestCancelled);
        Assert.Empty(await repo.BackupRefsAsync());
        await repository.RemoveConflictedAsync("edited.txt", cancellationToken: TestCancelled);

        Assert.False(File.Exists(Path.Combine(repo.Path, "edited.txt")));
        Assert.False(File.Exists(Path.Combine(repo.Path, "plain.txt")));
        Assert.Equal(["D  edited.txt", "D  plain.txt"], await GitLinesAsync(repo, "status", "--porcelain"));

        // git rm deletes the edit without a word; the snapshot keeps it.
        var snapshot = Assert.Single(await repo.BackupRefsAsync());
        Assert.StartsWith("refs/visualcommit/backup/discard-", snapshot.Name, StringComparison.Ordinal);
        Assert.Equal("ours, edited by hand\n", await ShowFileAsync(repo, snapshot.Name, "edited.txt"));
    }

    [Fact]
    public async Task A_submodule_conflict_is_left_alone()
    {
        using var repo = await TempRepo.CreateAsync();
        var first = await repo.CommitFileAsync("a.txt", "a\n", "First");
        var second = await repo.CommitFileAsync("a.txt", "b\n", "Second");
        var third = await repo.CommitFileAsync("a.txt", "c\n", "Third");
        var entries = $"160000 {first} 1\tmodule\n160000 {second} 2\tmodule\n160000 {third} 3\tmodule\n";
        await repo.GitWithInputAsync(entries, "update-index", "--index-info");
        var repository = await OpenAsync(repo);
        var entry = await ConflictAsync(repository, "module");

        var content = await repository.ReadConflictAsync(entry, cancellationToken: TestCancelled);

        Assert.True(entry.IsSubmodule);
        Assert.False(content.OursBinary || content.TheirsBinary || content.IsSymbolicLink || content.WorkingTreeExists);
        Assert.Null(await repository.ReadConflictSideAsync(entry, ConflictSide.Ours, cancellationToken: TestCancelled));
        var before = await GitLinesAsync(repo, "ls-files", "--unmerged");
        await Assert.ThrowsAsync<IOException>(() => repository.TakeSideAsync("module", ConflictSide.Ours, cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<IOException>(() => repository.RemoveConflictedAsync("module", cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<IOException>(() => repository.RunMergeToolAsync("module", "fake", cancellationToken: TestCancelled));
        Assert.Equal(before, await GitLinesAsync(repo, "ls-files", "--unmerged"));
        Assert.Equal(3, before.Count);
    }

    [Fact]
    public async Task A_path_that_is_not_conflicted_is_refused_and_its_edit_kept()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("f.txt", "committed\n", "Base");
        repo.WriteFile("f.txt", "edited\n");
        var repository = await OpenAsync(repo);

        // git checkout --ours on a path that is not unmerged would write the index's version over the edit.
        await Assert.ThrowsAsync<IOException>(() => repository.TakeSideAsync("f.txt", ConflictSide.Ours, cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<IOException>(() => repository.RemoveConflictedAsync("f.txt", cancellationToken: TestCancelled));

        Assert.Equal("edited\n", ReadText(repo, "f.txt"));
        Assert.Equal([" M f.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task Staged_conflict_markers_are_found_in_the_paths_asked_about_only()
    {
        using var repo = await MergeAsync(
            r => r.WriteFile("f.txt", BaseText).WriteFile("g.txt", BaseText).WriteFile("h.txt", BaseText),
            r => r.WriteFile("f.txt", OursText).WriteFile("g.txt", OursText).WriteFile("h.txt", OursText),
            r => r.WriteFile("f.txt", TheirsText).WriteFile("g.txt", TheirsText).WriteFile("h.txt", TheirsText));
        repo.WriteFile("g.txt", "line 1\nline 2 both  \nline 3\n");
        await repo.GitAsync("add", "--", "f.txt", "g.txt", "h.txt");
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);

        // f.txt and h.txt keep their markers; g.txt has only a whitespace error, which is not asked about.
        var marked = await repository.ReadStagedConflictMarkersAsync(["g.txt", "h.txt", "f.txt", "h.txt"], cancellationToken: TestCancelled);
        var one = await repository.ReadStagedConflictMarkersAsync(["g.txt", "f.txt"], cancellationToken: TestCancelled);
        var count = calls.Snapshot().Count;
        var none = await repository.ReadStagedConflictMarkersAsync([], cancellationToken: TestCancelled);

        Assert.Equal(["h.txt", "f.txt"], marked);
        Assert.Equal(["f.txt"], one);
        Assert.Empty(none);
        Assert.Equal(count, calls.Snapshot().Count);

        // Git's way of saying it found a problem, which the read takes as a result.
        Assert.Equal(2, (await repo.GitAllowingFailureAsync("diff", "--cached", "--check", "--", "f.txt")).ExitCode);
    }

    /// <summary>
    /// A repository whose base commit <paramref name="base"/> writes (with a <c>keep.txt</c> so that
    /// it has a file), whose <c>main</c> then <paramref name="ours"/> and branch <c>theirs</c>
    /// <paramref name="theirs"/> change, each committed whole, with <c>main</c> checked out and
    /// merging <c>theirs</c> stopped on its conflicts (<paramref name="mergeOptions"/> before
    /// <c>merge</c>, such as <c>-c merge.conflictStyle=diff3</c>).
    /// </summary>
    internal static async Task<TempRepo> MergeAsync(Action<TempRepo> @base, Action<TempRepo> ours, Action<TempRepo> theirs, params string[] mergeOptions)
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            @base(repo.WriteFile("keep.txt", "keep\n"));
            await repo.CommitAsync("Base");
            await repo.CreateBranchAsync("theirs");
            ours(repo);
            await repo.CommitAsync("Ours");
            await repo.CheckoutAsync("theirs");
            theirs(repo);
            await repo.CommitAsync("Theirs");
            await repo.CheckoutAsync("main");
            if (mergeOptions.Contains("core.autocrlf=true"))
            {
                // The working tree as core.autocrlf=true checks it out, before git merges into it.
                await repo.ConfigAsync("core.autocrlf", "true");
                foreach (var file in await GitLinesAsync(repo, "ls-files"))
                {
                    File.Delete(Path.Combine(repo.Path, file));
                }

                await repo.GitAsync("checkout", "--quiet", "--", ".");
            }

            var merge = await repo.GitAllowingFailureAsync([.. mergeOptions, "merge", "--no-edit", "theirs"]);
            Assert.Equal(1, merge.ExitCode);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    internal static async Task<ConflictEntry> ConflictAsync(GitRepository repository, string path) =>
        (await repository.ReadStatusAsync(TestCancelled)).FindConflict(path) ?? throw new InvalidOperationException($"{path} is not conflicted.");

    /// <summary>Binary content: a NUL and a byte that is not UTF-8 before <paramref name="text"/>.</summary>
    private static byte[] Binary(string text) => [0x00, 0xFF, .. Encoding.ASCII.GetBytes(text)];

    /// <summary>The blob id of <paramref name="content"/>, written into the repository.</summary>
    private static async Task<string> HashAsync(TempRepo repo, byte[] content)
    {
        var command = new GitCommand("hash-object", "-w", "--stdin", "--no-filters")
        {
            WorkingDirectory = repo.Path,
            Environment = repo.Environment,
            StandardInput = Encoding.Latin1.GetString(content),
            Encoding = GitCommand.Latin1,
        };
        return (await repo.Runner.RunAsync(command)).EnsureSuccess(command).StandardOutput.Trim();
    }

    /// <summary>Commits <c>link</c> as a symbolic link to <paramref name="target"/>, written into the index only, then checks it out as the machine does.</summary>
    private static async Task CommitLinkAsync(TempRepo repo, string target, string message)
    {
        var blob = await HashAsync(repo, Encoding.UTF8.GetBytes(target));
        await repo.GitAsync("update-index", "--add", "--cacheinfo", $"120000,{blob},link");
        await repo.GitAsync("commit", "--quiet", "--message", message);
        await repo.GitAsync("reset", "--quiet", "--hard");
    }

    /// <summary>Whether the repository makes real symbolic links (<c>core.symlinks</c>, which git init sets false where it cannot).</summary>
    private static async Task<bool> SymlinksWorkAsync(TempRepo repo) =>
        (await repo.GitAllowingFailureAsync("config", "--type=bool", "--get", "core.symlinks")).StandardOutput.Trim() != "false";
}
