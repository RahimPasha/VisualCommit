using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitConfiguration.ReadIdentityAsync"/> (docs/phase-6-design.md 8.6, facts 0.3):
/// <c>git var</c> read with and without a committer of its own, and the refusals the settings
/// screen says in its own words, produced live with <c>user.useConfigOnly</c> (so that no git
/// fills a name or address from the machine), and one only Git 2.36 gives from its recorded line.
/// </summary>
public class GitIdentityTests
{
    /// <summary>Git 2.36's refusal of a name of punctuation only (2026-10-18; 2.55 accepts the name).</summary>
    private const string RecordedPunctuationRefusal = "fatal: name consists only of disallowed characters: .,:;\n";

    /// <summary>Git's refusal without <c>user.useConfigOnly</c> and nothing set, as Git 2.36 printed it (the machine's names in its last line).</summary>
    private const string RecordedUnknownIdentity =
        "Author identity unknown\n\n*** Please tell me who you are.\n\nRun\n\n  git config --global user.email \"you@example.com\"\n" +
        "  git config --global user.name \"Your Name\"\n\nto set your account's default identity.\n" +
        "Omit --global to set the identity only in this repository.\n\nfatal: unable to auto-detect email address (got 'ann@LAPTOP.(none)')\n";

    [Fact]
    public async Task The_author_and_committer_are_read_without_gits_time()
    {
        using var lab = new ConfigLab();
        using var repo = await TempRepo.CreateAsync();
        await repo.ConfigAsync("user.name", "Ann O'Neil");
        await repo.ConfigAsync("user.email", "ann@example.com");

        Assert.Equal(
            new GitIdentityRead("Ann O'Neil <ann@example.com>", "Ann O'Neil <ann@example.com>", null),
            await lab.Configuration.ReadIdentityAsync(repo.Path, TestCancelled));
    }

    [Fact]
    public async Task A_committer_of_its_own_is_read_apart()
    {
        using var lab = new ConfigLab();
        using var repo = await TempRepo.CreateAsync();
        await repo.ConfigAsync("committer.name", "Cee");
        await repo.ConfigAsync("committer.email", "cee@example.com");

        Assert.Equal(
            new GitIdentityRead($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", "Cee <cee@example.com>", null),
            await lab.Configuration.ReadIdentityAsync(repo.Path, TestCancelled));
    }

    /// <summary>The identities to refuse (the repository's user.name and user.email, null: unset) and the part of git's last line that says why (8.6).</summary>
    public static TheoryData<string?, string?, string> Refusals => new()
    {
        { null, null, "no email was given and auto-detection is disabled" },
        { null, "ann@example.com", "no name was given and auto-detection is disabled" },
        { string.Empty, "ann@example.com", "empty ident name (for <ann@example.com>) not allowed" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_refusal_is_gits_last_fatal_line(string? name, string? email, string reason)
    {
        using var lab = new ConfigLab();
        using var repo = await TempRepo.CreateAsync();
        await repo.ConfigAsync("user.useConfigOnly", "true");
        await SetOrUnsetAsync(repo, "user.name", name);
        await SetOrUnsetAsync(repo, "user.email", email);

        var identity = await lab.Configuration.ReadIdentityAsync(repo.Path, TestCancelled);

        Assert.Equal(new GitIdentityRead(null, null, "fatal: " + reason), identity);
    }

    [Fact]
    public async Task An_author_refused_beside_a_committer_of_its_own_is_the_refusal()
    {
        using var lab = new ConfigLab();
        using var repo = await TempRepo.CreateAsync();
        await repo.ConfigAsync("user.useConfigOnly", "true");
        await SetOrUnsetAsync(repo, "user.name", null);
        await SetOrUnsetAsync(repo, "user.email", null);
        await repo.ConfigAsync("committer.name", "Cee");
        await repo.ConfigAsync("committer.email", "cee@example.com");
        var author = await lab.Runner.RunAsync(new GitCommand("var", "GIT_AUTHOR_IDENT") { WorkingDirectory = repo.Path }, TestCancelled);

        var identity = await lab.Configuration.ReadIdentityAsync(repo.Path, TestCancelled);

        // Which line git gives here is its own ("empty ident name (for <>)" on 2.36): the author's.
        Assert.False(author.Succeeded);
        var authorsLine = author.StandardError.Split('\n').Select(line => line.TrimEnd('\r')).Last(line => line.StartsWith("fatal: ", StringComparison.Ordinal));
        Assert.Equal(new GitIdentityRead(null, "Cee <cee@example.com>", authorsLine), identity);
    }

    [Fact]
    public void Git_236s_refusal_of_a_punctuation_name_is_its_line()
    {
        var refused = new GitResult(128, string.Empty, RecordedPunctuationRefusal, TimeSpan.Zero);

        Assert.Equal(
            new GitIdentityRead(null, null, "fatal: name consists only of disallowed characters: .,:;"),
            GitConfiguration.ParseIdentity(refused, refused));
    }

    [Fact]
    public void The_refusal_after_gits_advice_is_its_last_fatal_line()
    {
        var refused = new GitResult(128, string.Empty, RecordedUnknownIdentity.Replace("\n", "\r\n", StringComparison.Ordinal), TimeSpan.Zero);

        Assert.Equal("fatal: unable to auto-detect email address (got 'ann@LAPTOP.(none)')", GitConfiguration.ParseIdentity(refused, refused).Refusal);
    }

    [Theory]
    [InlineData("Ann O'Neil <ann@example.com> 1792295653 -0700\n", "Ann O'Neil <ann@example.com>")]
    [InlineData("Test Author <author@example.com> 1767344400 +0000\r\n", "Test Author <author@example.com>")]
    [InlineData(". <ann@example.com> 1792352644 +0530\n", ". <ann@example.com>")]
    public void Gits_time_and_zone_are_dropped(string output, string identity)
    {
        var read = new GitResult(0, output, string.Empty, TimeSpan.Zero);

        Assert.Equal(new GitIdentityRead(identity, identity, null), GitConfiguration.ParseIdentity(read, read));
    }

    private static Task SetOrUnsetAsync(TempRepo repo, string key, string? value) =>
        value is null ? repo.GitAllowingFailureAsync("config", "--unset-all", key) : repo.ConfigAsync(key, value);
}
