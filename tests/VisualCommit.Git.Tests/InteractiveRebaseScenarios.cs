using VisualCommit.Core.Git;
using VisualCommit.Testing;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RebaseTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>The repositories and reads that the interactive rebase's test classes share.</summary>
internal static class InteractiveRebaseScenarios
{
    public const string Topic = "refs/heads/topic";
    public const string TwoMessage = "Add two\n\n#12 issue line\n";

    /// <summary>
    /// The line scenario: main has the initial commit (README.md, f.txt); topic, from it, adds
    /// one.txt ("Add one"), two.txt ("Add two" with a <c>#12 issue line</c>), three.txt and
    /// four.txt; <c>other</c> points at "Add two". HEAD on topic.
    /// </summary>
    public static async Task<TempRepo> LineAsync()
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            repo.WriteFile("README.md", "readme\n").WriteFile("f.txt", "a\nb\nc\n");
            await repo.CommitAsync("Initial commit");
            await repo.CreateBranchAsync("topic");
            await repo.CheckoutAsync("topic");
            await repo.CommitFileAsync("one.txt", "one\n", "Add one");
            await repo.CommitFileAsync("two.txt", "two\n", TwoMessage);
            await repo.CreateBranchAsync("other");
            await repo.CommitFileAsync("three.txt", "three\n", "Add three");
            await repo.CommitFileAsync("four.txt", "four\n", "Add four");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// main and topic diverged: main changes line 1 of f.txt to "M"; topic adds p.txt ("Add p"),
    /// then either changes line 1 to "T" and adds g.txt ("Topic changes a", "Add g"), so that the
    /// group's target conflicts, or (<paramref name="memberConflicts"/>) adds t.txt ("Add t" with a
    /// <c>#12 issue line</c>) and changes line 1 to "F" ("F changes a"), so that a member does.
    /// HEAD on topic.
    /// </summary>
    public static async Task<TempRepo> OntoMainAsync(bool memberConflicts)
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            repo.WriteFile("README.md", "readme\n").WriteFile("f.txt", "a\nb\nc\n");
            await repo.CommitAsync("Initial commit");
            await repo.CreateBranchAsync("topic");
            await repo.CommitFileAsync("f.txt", "M\nb\nc\n", "Main changes a");
            await repo.CheckoutAsync("topic");
            await repo.CommitFileAsync("p.txt", "p\n", "Add p");
            if (memberConflicts)
            {
                await repo.CommitFileAsync("t.txt", "t\n", "Add t\n\n#12 issue line\n");
                await repo.CommitFileAsync("f.txt", "F\nb\nc\n", "F changes a");
            }
            else
            {
                await repo.CommitFileAsync("f.txt", "T\nb\nc\n", "Topic changes a");
                await repo.CommitFileAsync("g.txt", "g\n", "Add g");
            }

            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>The repository and the list of topic onto <paramref name="onto"/> (a "from here" list onto main's initial commit when null).</summary>
    public static async Task<(GitRepository Repository, RebaseList List)> OpenListAsync(TempRepo repo, string? onto = null)
    {
        var repository = await OpenAsync(repo);
        var list = onto is null
            ? await repository.ReadRebaseListAsync(await RevAsync(repo, "main"), Topic, fromCommit: true, cancellationToken: TestCancelled)
            : await repository.ReadRebaseListAsync(await RevAsync(repo, onto), Topic, fromCommit: false, cancellationToken: TestCancelled);
        return (repository, list);
    }

    /// <summary>Every branch but topic, with its commit.</summary>
    public static async Task<string> OtherBranchesAsync(TempRepo repo) =>
        string.Join('\n', (await BranchesAsync(repo)).Split('\n').Where(line => !line.StartsWith(Topic + " ", StringComparison.Ordinal)));
}
