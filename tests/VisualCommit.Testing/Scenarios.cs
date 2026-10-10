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
