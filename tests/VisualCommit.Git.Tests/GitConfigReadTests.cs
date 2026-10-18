using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.GitConfigTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitConfiguration.ReadAsync"/> (docs/phase-6-design.md 8.1, facts 0.3): the <c>-z</c>
/// parse of git's recorded output, and live reads: global and local, an include, an
/// <c>includeIf "gitdir:"</c> applying only inside its repository, the outside read with its
/// <c>local</c> entries dropped and its ceiling holding, and 0.3's <c>includeIf</c> shape.
/// </summary>
public class GitConfigReadTests
{
    /// <summary>
    /// <c>git config --show-scope --show-origin -z --list</c> as Git 2.36 printed it in a repository
    /// with a global file, an include, an <c>includeIf</c> and <c>-c core.x=1</c> (2026-10-18; Git
    /// 2.55 printed the same bytes, 0.3), the lab's folder written <c>{LAB}</c>: a bare key, an empty
    /// value, a value with a line break, an included file's origin, a relative <c>.git/config</c>
    /// and <c>command line:</c>.
    /// </summary>
    private const string RecordedInRepository =
        "global\0file:{LAB}/global.gitconfig\0user.email\npersonal@example.com\0" +
        "global\0file:{LAB}/global.gitconfig\0include.path\ninc.gitconfig\0" +
        "global\0file:{LAB}/inc.gitconfig\0merge.tool\nmeld\0" +
        "global\0file:{LAB}/global.gitconfig\0includeif.gitdir:{LAB}/repo1/.path\nwork.gitconfig\0" +
        "global\0file:{LAB}/work.gitconfig\0user.email\nwork@example.com\0" +
        "global\0file:{LAB}/global.gitconfig\0pull.rebase\0" +
        "global\0file:{LAB}/global.gitconfig\0x.empty\n\0" +
        "global\0file:{LAB}/global.gitconfig\0x.multi\nline one\nline two\0" +
        "local\0file:.git/config\0core.repositoryformatversion\n0\0" +
        "local\0file:.git/config\0user.name\nLocal Name\0" +
        "command\0command line:\0core.x\n1\0";

    /// <summary>The same read in a linked worktree with its own <c>config.worktree</c>: the repository's file is absolute there (0.3).</summary>
    private const string RecordedInLinkedWorktree =
        "local\0file:{LAB}/repo/.git/config\0user.name\nLocal Name\0" +
        "worktree\0file:{LAB}/repo/.git/worktrees/wt/config.worktree\0user.name\nWT Name\0";

    private static readonly string Lab = GitPath(Path.Combine(Path.GetTempPath(), "lab"));

    [Fact]
    public void The_recorded_read_in_a_repository_parses_entry_by_entry()
    {
        var folder = Path.GetFullPath(Path.Combine(Lab, "repo1"));

        var values = GitConfiguration.ParseList(RecordedInRepository.Replace("{LAB}", Lab, StringComparison.Ordinal), folder);

        var global = Path.GetFullPath(Lab + "/global.gitconfig");
        var local = Path.Combine(folder, ".git", "config");
        Assert.Equal(
            [
                new(GitConfigScope.Global, global, "user.email", "personal@example.com"),
                new(GitConfigScope.Global, global, "include.path", "inc.gitconfig"),
                new(GitConfigScope.Global, Path.GetFullPath(Lab + "/inc.gitconfig"), "merge.tool", "meld"),
                new(GitConfigScope.Global, global, $"includeif.gitdir:{Lab}/repo1/.path", "work.gitconfig"),
                new(GitConfigScope.Global, Path.GetFullPath(Lab + "/work.gitconfig"), "user.email", "work@example.com"),
                new(GitConfigScope.Global, global, "pull.rebase", null),
                new(GitConfigScope.Global, global, "x.empty", string.Empty),
                new(GitConfigScope.Global, global, "x.multi", "line one\nline two"),
                new(GitConfigScope.Local, local, "core.repositoryformatversion", "0"),
                new(GitConfigScope.Local, local, "user.name", "Local Name"),
                new GitConfigValue(GitConfigScope.Command, null, "core.x", "1"),
            ],
            values);
    }

    [Fact]
    public void The_recorded_read_in_a_linked_worktree_keeps_its_absolute_files()
    {
        var values = GitConfiguration.ParseList(RecordedInLinkedWorktree.Replace("{LAB}", Lab, StringComparison.Ordinal), Path.GetFullPath(Lab + "/wt"));

        Assert.Equal(
            [
                new(GitConfigScope.Local, Path.GetFullPath(Lab + "/repo/.git/config"), "user.name", "Local Name"),
                new GitConfigValue(GitConfigScope.Worktree, Path.GetFullPath(Lab + "/repo/.git/worktrees/wt/config.worktree"), "user.name", "WT Name"),
            ],
            values);
        var read = new GitConfigRead(values, InRepository: true);
        Assert.Equal(GitConfigScope.Worktree, read.Winner("USER.Name")!.Scope);
    }

    [Fact]
    public void Nothing_set_is_no_entry_and_an_unknown_scope_is_skipped()
    {
        Assert.Empty(GitConfiguration.ParseList(string.Empty, Lab));
        Assert.Equal(
            [new GitConfigValue(GitConfigScope.System, Path.GetFullPath(Lab + "/etc/gitconfig"), "a.b", "c")],
            GitConfiguration.ParseList($"unknown\0file:{Lab}/x\0x.y\nz\0system\0file:{Lab}/etc/gitconfig\0a.b\nc\0", Lab));
    }

    [Fact]
    public void Output_that_is_not_three_fields_per_entry_is_refused()
    {
        Assert.Throws<FormatException>(() => GitConfiguration.ParseList("global\0file:x\0", Lab));
    }

    [Fact]
    public async Task The_repository_read_has_the_global_and_local_scopes_and_the_local_value_wins()
    {
        using var lab = new ConfigLab();
        lab.WriteGlobal("[user]\n\tname = Global Name\n");
        using var repo = await TempRepo.CreateAsync();
        await repo.ConfigAsync("user.name", "Local Name");

        var read = await lab.Configuration.ReadAsync(repo.Path, TestCancelled);

        Assert.True(read.InRepository);
        Assert.Equal(
            [(GitConfigScope.Global, "Global Name"), (GitConfigScope.Local, "Local Name")],
            read.Of("user.name").Select(value => (value.Scope, value.Value!)));
        Assert.Equal(lab.GlobalFile, read.Of("user.name")[0].Origin);
        Assert.Equal(Path.Combine(Path.GetFullPath(repo.Path), ".git", "config"), read.Of("user.name")[1].Origin);
        Assert.Equal("Local Name", read.Winner("user.name")!.Value);
    }

    [Fact]
    public async Task The_outside_read_has_no_repository_and_an_included_file_keeps_the_global_scope()
    {
        using var lab = new ConfigLab();
        var included = lab.Combine("inc.gitconfig");
        File.WriteAllText(included, "[merge]\n\ttool = meld\n");
        lab.WriteGlobal($"[user]\n\tname = Global Name\n[include]\n\tpath = {GitPath(included)}\n");

        var read = await lab.Configuration.ReadAsync(null, TestCancelled);

        Assert.False(read.InRepository);
        Assert.Equal(
            [
                new(GitConfigScope.Global, lab.GlobalFile, "user.name", "Global Name"),
                new(GitConfigScope.Global, lab.GlobalFile, "include.path", GitPath(included)),
                new GitConfigValue(GitConfigScope.Global, included, "merge.tool", "meld"),
            ],
            read.Values);
        Assert.True(Directory.Exists(lab.DataDirectory));
    }

    [Fact]
    public async Task An_includeIf_applies_only_inside_its_repository()
    {
        using var lab = new ConfigLab();
        using var work = await TempRepo.CreateAsync("work");
        using var other = await TempRepo.CreateAsync("other");
        var workFile = lab.Combine("work.gitconfig");
        File.WriteAllText(workFile, "[x]\n\twork = yes\n");
        lab.WriteGlobal($"[includeIf \"gitdir:{await GitDirectoryAsync(work)}\"]\n\tpath = {GitPath(workFile)}\n");

        Assert.Equal((GitConfigScope.Global, workFile), ScopeAndOrigin((await lab.Configuration.ReadAsync(work.Path, TestCancelled)).Winner("x.work")));
        Assert.Null((await lab.Configuration.ReadAsync(other.Path, TestCancelled)).Winner("x.work"));
        Assert.Null((await lab.Configuration.ReadAsync(null, TestCancelled)).Winner("x.work"));
    }

    [Fact]
    public async Task Under_the_includeIf_shape_the_outside_read_leaves_out_its_email_and_the_repository_read_ends_with_it()
    {
        using var lab = new ConfigLab();
        using var work = await TempRepo.CreateAsync("work");
        await work.GitAsync("config", "--unset", "user.email");
        var workFile = lab.Combine("work.gitconfig");
        File.WriteAllText(workFile, "[user]\n\temail = work@example.com\n");
        lab.WriteGlobal($"[user]\n\temail = personal@example.com\n[includeIf \"gitdir:{await GitDirectoryAsync(work)}\"]\n\tpath = {GitPath(workFile)}\n");

        var outside = await lab.Configuration.ReadAsync(null, TestCancelled);
        var inWork = await lab.Configuration.ReadAsync(work.Path, TestCancelled);

        // 8.2: the global field's value comes from the outside read; what git uses in the
        // repository from the repository read.
        Assert.Equal(["personal@example.com"], outside.Of("user.email").Select(value => value.Value));
        Assert.Equal(["personal@example.com", "work@example.com"], inWork.Of("user.email").Select(value => value.Value));
        Assert.Equal((GitConfigScope.Global, workFile), ScopeAndOrigin(inWork.Winner("user.email")));
    }

    [Fact]
    public async Task The_outside_read_drops_the_local_entries_of_a_data_folder_that_is_a_repository()
    {
        using var lab = new ConfigLab();
        Directory.CreateDirectory(lab.DataDirectory);
        await RunAsync(lab, lab.DataDirectory, "init", "--quiet");
        await RunAsync(lab, lab.DataDirectory, "config", "x.local", "here");
        var raw = await lab.Runner.RunAsync(new GitCommand("config", "--show-scope", "--list") { WorkingDirectory = lab.DataDirectory }, TestCancelled);
        Assert.Contains("local\tx.local=here", raw.StandardOutput.Replace("\r", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);

        var read = await lab.Configuration.ReadAsync(null, TestCancelled);

        Assert.DoesNotContain(read.Values, value => value.Scope is GitConfigScope.Local or GitConfigScope.Worktree);
    }

    [Fact]
    public async Task The_outside_read_looks_for_no_repository_above_the_data_folder()
    {
        // The lab's folder is a repository and the data folder lies in it, as a home folder kept in
        // git would: under the ceiling git finds no repository, so its includeIf does not apply.
        using var lab = new ConfigLab();
        Directory.CreateDirectory(lab.DataDirectory);
        await RunAsync(lab, lab.Folder, "init", "--quiet");
        var gitDirectory = (await RunAsync(lab, lab.Folder, "rev-parse", "--absolute-git-dir")).StandardOutput.Trim();
        var insideFile = lab.Combine("inside.gitconfig");
        File.WriteAllText(insideFile, "[x]\n\tinside = yes\n");
        lab.WriteGlobal($"[includeIf \"gitdir:{gitDirectory}\"]\n\tpath = {GitPath(insideFile)}\n");
        var found = await lab.Runner.RunAsync(new GitCommand("config", "--get", "x.inside") { WorkingDirectory = lab.DataDirectory, Environment = new Dictionary<string, string?> { [GitIsolation.CeilingVariable] = GitIsolation.CeilingDirectory } }, TestCancelled);
        Assert.Equal("yes", found.StandardOutput.Trim());

        var read = await lab.Configuration.ReadAsync(null, TestCancelled);

        Assert.Null(read.Winner("x.inside"));
        Assert.DoesNotContain(read.Values, value => value.Scope == GitConfigScope.Local);
    }

    [Fact]
    public async Task A_linked_worktrees_own_value_has_the_worktree_scope_and_wins()
    {
        using var lab = new ConfigLab();
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        var linked = Sibling(repo, "linked");
        await repo.GitAsync("worktree", "add", "--quiet", "-b", "side", linked);
        await repo.ConfigAsync("core.repositoryformatversion", "1");
        await repo.ConfigAsync("extensions.worktreeConfig", "true");
        await RunAsync(lab, linked, "config", "--worktree", "user.name", "WT Name");

        var read = await lab.Configuration.ReadAsync(linked, TestCancelled);

        var winner = read.Winner("user.name")!;
        Assert.Equal((GitConfigScope.Worktree, "WT Name", "config.worktree"), (winner.Scope, winner.Value, Path.GetFileName(winner.Origin)));
        Assert.Contains(read.Of("user.name"), value => value is { Scope: GitConfigScope.Local, Value: TempRepo.AuthorName });
    }

    [Fact]
    public async Task The_environments_values_have_the_command_scope_and_win()
    {
        using var lab = new ConfigLab(extraEnvironment: new Dictionary<string, string?>
        {
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "user.name",
            ["GIT_CONFIG_VALUE_0"] = "From The Environment",
        });
        lab.WriteGlobal("[user]\n\tname = Global Name\n");

        var winner = (await lab.Configuration.ReadAsync(null, TestCancelled)).Winner("user.name");

        Assert.Equal(new GitConfigValue(GitConfigScope.Command, null, "user.name", "From The Environment"), winner);
    }

    [Fact]
    public async Task A_broken_file_fails_the_read_with_gits_exit_code()
    {
        using var lab = new ConfigLab();
        lab.WriteGlobal("[user\n\tname = Broken\n");

        var failure = await Assert.ThrowsAsync<GitException>(() => lab.Configuration.ReadAsync(null, TestCancelled));

        Assert.Equal(128, failure.ExitCode);
    }

    [Fact]
    public void The_global_file_and_the_variables_follow_the_session_over_the_process()
    {
        using var lab = new ConfigLab();

        Assert.Equal(lab.GlobalFile, lab.Configuration.GlobalFile);
        Assert.Equal(Path.Combine(lab.XdgHome, "git", "config"), lab.Configuration.XdgFile);
        Assert.Equal(lab.XdgHome, lab.Configuration.Variable("XDG_CONFIG_HOME"));
        Assert.Null(lab.Configuration.Variable("GIT_AUTHOR_NAME"));
        Assert.Equal(Environment.GetEnvironmentVariable("PATH"), lab.Configuration.Variable("PATH"));
    }

    [Fact]
    public async Task Each_read_is_one_git_call_that_takes_no_lock_and_the_outside_one_has_its_ceiling()
    {
        using var lab = new ConfigLab();
        using var repo = await TempRepo.CreateAsync();
        var commands = new List<GitCommand>();
        using var configuration = new GitConfiguration(new CapturingRunner(lab.Runner, commands), lab.DataDirectory, lab.MergeToolsFile, lab.Environment);

        await configuration.ReadAsync(null, TestCancelled);
        await configuration.ReadAsync(repo.Path, TestCancelled);

        Assert.Equal(["config --show-scope --show-origin -z --list", "config --show-scope --show-origin -z --list"], commands.Select(command => string.Join(' ', command.Arguments)));
        Assert.All(commands, command => Assert.Equal("0", command.Environment["GIT_OPTIONAL_LOCKS"]));
        Assert.Equal((Path.GetFullPath(lab.DataDirectory), lab.Folder), (commands[0].WorkingDirectory, commands[0].Environment[GitIsolation.CeilingVariable]));
        Assert.Equal(Path.GetFullPath(repo.Path), commands[1].WorkingDirectory);
        Assert.False(commands[1].Environment.ContainsKey(GitIsolation.CeilingVariable));
    }

    private static async Task<GitResult> RunAsync(ConfigLab lab, string folder, params string[] arguments)
    {
        var result = await lab.Runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = folder }, TestCancelled);
        Assert.True(result.Succeeded, $"git {string.Join(' ', arguments)}: {result.StandardError}");
        return result;
    }

    private static (GitConfigScope, string?) ScopeAndOrigin(GitConfigValue? value)
    {
        Assert.NotNull(value);
        return (value.Scope, value.Origin);
    }
}
