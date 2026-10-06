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
}
