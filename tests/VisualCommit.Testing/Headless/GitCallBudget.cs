using VisualCommit.Core.Git;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// One form of a refresh's git calls outside every operation (<c>RepositoryViewModel.RefreshAsync</c>):
/// its name as a failure prints it and the test of a call's command text. An optional form is a
/// call a refresh makes only sometimes (D281): counted 0 times or as often as the other forms.
/// </summary>
public sealed record RefreshForm(string Name, Func<string, bool> Matches, bool Optional = false);

/// <summary>
/// S8's counts of git calls (docs/phase-7a-design.md 5.3, D280, D281). With a live watcher a count
/// is marked and read with the watchers quiet (<see cref="ShellDriver.QuietCallMarkAsync"/>,
/// <see cref="ShellDriver.QuietCallsSinceAsync"/>); the refreshes the watcher started for a change
/// reported late are set aside only whole (<see cref="NotWholeRefreshes"/>), so that no other call
/// slips past with them; a count that is a cost is held to a budget (<see cref="AssertAtMost(IReadOnlyCollection{GitCallRecord}, int, string, bool)"/>),
/// one that is the test's meaning stays exact.
/// </summary>
public static class GitCallBudget
{
    /// <summary>
    /// The forms of a refresh's calls outside every operation: the refs read's five (docs/phase-6-design.md
    /// 10.2) and the status read, as the watcher's refresh after a write to the git folder makes them
    /// (part S's six, moved from <c>Phase6Checks.S.Helpers.cs</c>), and the URL rewrites' read a tab
    /// makes after each refs read while a GitHub account is signed in (docs/phase-7a-design.md 7.10),
    /// optional (D281): signed out, as in every check before phase 7a, it is never made.
    /// </summary>
    public static IReadOnlyList<RefreshForm> RefreshForms { get; } =
    [
        new("git for-each-ref --format=… refs/heads refs/remotes refs/tags", text => text.StartsWith("git for-each-ref --format=", StringComparison.Ordinal) && text.EndsWith(" refs/heads refs/remotes refs/tags", StringComparison.Ordinal)),
        new("git config --null --get-regexp ^remote\\.", text => text == "git config --null --get-regexp ^remote\\."),
        new("git stash list --no-show-signature --no-color --encoding=UTF-8 --format=…", text => text.StartsWith("git stash list --no-show-signature --no-color --encoding=UTF-8 --format=", StringComparison.Ordinal)),
        new("git symbolic-ref -q HEAD", text => text == "git symbolic-ref -q HEAD"),
        new("git rev-parse -q --verify HEAD", text => text == "git rev-parse -q --verify HEAD"),
        new("git status --porcelain=v2 -z --untracked-files=all --renames", text => text == "git status --porcelain=v2 -z --untracked-files=all --renames"),
        new("git config --null --get-regexp ^url\\.", text => text == "git config --null --get-regexp ^url\\.", Optional: true),
    ];

    /// <summary>
    /// What keeps <paramref name="calls"/> from being whole refreshes, or null when they are, none
    /// included: every call of a form of <paramref name="forms"/> (<see cref="RefreshForms"/> when
    /// null) with no operation id, every required form as often as each other one, and an optional
    /// form 0 times or as often. A refresh the watcher starts for a write the system reports late
    /// (macOS) can land in any step's calls; it is let through only whole.
    /// </summary>
    public static string? NotWholeRefreshes(IReadOnlyCollection<GitCallRecord> calls, IReadOnlyList<RefreshForm>? forms = null)
    {
        ArgumentNullException.ThrowIfNull(calls);
        forms ??= RefreshForms;
        var counts = forms.ToDictionary(form => form, _ => 0);
        var problems = new List<string>();
        foreach (var call in calls)
        {
            var form = forms.FirstOrDefault(candidate => candidate.Matches(call.CommandText));
            if (form is null || call.OperationId is not null)
            {
                problems.Add($"{call.CommandText}{(call.OperationId is null ? string.Empty : $" (in {call.OperationId})")} is no refresh's call");
                continue;
            }

            counts[form]++;
        }

        var required = counts.Where(entry => !entry.Key.Optional).Select(entry => entry.Value).Distinct().ToList();
        var refreshes = required.Count == 1 ? required[0] : -1;
        if (required.Count > 1 || counts.Any(entry => entry.Key.Optional && entry.Value != 0 && entry.Value != refreshes))
        {
            problems.Add("the refresh calls are not whole refreshes: " + string.Join(", ", counts.Select(entry => $"{entry.Key.Name} {entry.Value}")));
        }

        return problems.Count == 0 ? null : string.Join("; ", problems);
    }

    /// <summary>Fails unless <paramref name="calls"/> are whole refreshes (<see cref="NotWholeRefreshes"/>), none included, and prints them.</summary>
    /// <exception cref="GitCallBudgetException">A call is no whole refresh's.</exception>
    public static void AssertOnlyWholeRefreshes(IReadOnlyCollection<GitCallRecord> calls, string what)
    {
        ArgumentNullException.ThrowIfNull(calls);
        if (NotWholeRefreshes(calls) is { } reason)
        {
            throw new GitCallBudgetException($"{what} made git calls that are no whole refresh ({reason}):\n  " + string.Join("\n  ", calls.Select(Describe)));
        }
    }

    /// <summary>
    /// Fails when <paramref name="calls"/> are more than <paramref name="budget"/>: a count that is a
    /// cost, the count measured when the budget was set. With <paramref name="setAsideWholeRefreshes"/>
    /// the calls of a refresh form outside every operation are not counted, and must be whole
    /// refreshes (<see cref="NotWholeRefreshes"/>).
    /// </summary>
    /// <exception cref="GitCallBudgetException">The budget is exceeded, or a call set aside is no whole refresh's.</exception>
    public static void AssertAtMost(IReadOnlyCollection<GitCallRecord> calls, int budget, string what, bool setAsideWholeRefreshes = false)
    {
        ArgumentNullException.ThrowIfNull(calls);
        var counted = calls.ToList();
        if (setAsideWholeRefreshes)
        {
            var refreshes = counted.Where(IsRefreshCall).ToList();
            if (NotWholeRefreshes(refreshes) is { } reason)
            {
                throw new GitCallBudgetException($"{what} made git calls that are no whole refresh ({reason}):\n  " + string.Join("\n  ", calls.Select(Describe)));
            }

            counted = [.. counted.Where(call => !IsRefreshCall(call))];
        }

        if (counted.Count > budget)
        {
            throw new GitCallBudgetException($"{what}: budget {budget}, used {counted.Count}. The calls:\n  " + string.Join("\n  ", counted.Select(Describe)));
        }
    }

    /// <summary>Fails when <paramref name="commands"/> are more than <paramref name="budget"/>: as the overload for records, for a test that has only the commands.</summary>
    /// <exception cref="GitCallBudgetException">The budget is exceeded.</exception>
    public static void AssertAtMost(IReadOnlyCollection<string> commands, int budget, string what)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (commands.Count > budget)
        {
            throw new GitCallBudgetException($"{what}: budget {budget}, used {commands.Count}. The calls:\n  " + string.Join("\n  ", commands));
        }
    }

    /// <summary>A call of a refresh form outside every operation.</summary>
    private static bool IsRefreshCall(GitCallRecord call) =>
        call.OperationId is null && RefreshForms.Any(form => form.Matches(call.CommandText));

    /// <summary>A call as a failure prints it: its command, its operation id and its exit code.</summary>
    private static string Describe(GitCallRecord call) =>
        $"{call.CommandText}{(call.OperationId is null ? string.Empty : $" (in {call.OperationId})")} → {call.ExitCode}";
}

/// <summary>A count of git calls broke its rule (<see cref="GitCallBudget"/>): the message prints the calls.</summary>
public sealed class GitCallBudgetException(string message) : Exception(message);
