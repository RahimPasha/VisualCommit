using System.Globalization;
using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// What two commits or branches differ in (docs/phase-5-design.md 6.3, 0.3, D177), on the changed
/// files' parser of <c>GitRepository.Commits.cs</c> (<see cref="ParseNameStatus"/>).
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>What git says when <c>diff.renameLimit</c> stopped its rename detection (exhaustive or inexact).</summary>
    private const string RenameDetectionSkipped = "rename detection was skipped";

    public async Task<CompareResult> CompareAsync(string from, string to, bool threeDot, bool countCommits, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        if (from.StartsWith('-') || to.StartsWith('-'))
        {
            throw new ArgumentException("A ref name or commit id cannot start with '-'.", from.StartsWith('-') ? nameof(from) : nameof(to));
        }

        // Both names to commits in one call (a tag peeled), kept from here on, so that a ref that
        // moves meanwhile changes nothing; "--" keeps a file of either name from being taken for one.
        var names = SplitLines(await ReadOutputAsync(cancellationToken, "rev-parse", from + "^{commit}", to + "^{commit}", "--").ConfigureAwait(false));
        if (names.Count < 2 || !IsObjectId(names[0]) || !IsObjectId(names[1]))
        {
            throw new GitException($"git rev-parse {from} {to}", 0, $"Unexpected output: {string.Join(' ', names)}");
        }

        var (fromSha, toSha) = (names[0], names[1]);
        if (fromSha == toSha)
        {
            return new CompareResult(fromSha, toSha, threeDot ? CompareBase.MergeBase : CompareBase.Trees, fromSha, [], false, countCommits ? new CompareCounts(0, 0) : null);
        }

        // The counts do not depend on the list: they are read beside it.
        var counts = countCommits ? CountBothSidesAsync(fromSha, toSha, cancellationToken) : Task.FromResult<CompareCounts?>(null);

        var (compareBase, old) = (CompareBase.Trees, fromSha);
        if (threeDot)
        {
            // merge-base prints the first of --all's bases, which "git diff A...B" uses too; exit 1
            // without output is no common history. Neither A...B nor --merge-base is used: diff-tree
            // ignores a range, and --merge-base fails on several bases (0.3).
            var command = Read("merge-base", "--all", fromSha, toSha);
            var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
            List<string> bases = result.ExitCode == 1 ? [] : SplitLines(result.EnsureSuccess(command).StandardOutput);
            (compareBase, old) = bases.Count switch
            {
                0 => (CompareBase.NoCommonHistory, fromSha),
                1 => (CompareBase.MergeBase, bases[0]),
                _ => (CompareBase.SeveralMergeBases, bases[0]),
            };
        }

        // Two commit ids, never a range; -M finds renames whatever diff.renames says, and the user's
        // diff.renameLimit applies, with git's warning when it stopped the search.
        var diff = Read("diff-tree", "-r", "-z", "--name-status", "-M", "--no-ext-diff", "--no-color", "--no-commit-id", old, toSha);
        var listed = (await _runner.RunAsync(diff, cancellationToken).ConfigureAwait(false)).EnsureSuccess(diff);
        var files = ParseNameStatus(listed.StandardOutput);
        var renamesSkipped = listed.StandardError.Contains(RenameDetectionSkipped, StringComparison.Ordinal);

        return new CompareResult(fromSha, toSha, compareBase, old, files, renamesSkipped, await counts.ConfigureAwait(false));
    }

    /// <summary>
    /// The commits only on each side: <c>git rev-list --left-right --count &lt;from&gt;...&lt;to&gt;</c>
    /// prints the left side's count (<paramref name="fromSha"/>'s), a tab, and the right side's.
    /// </summary>
    private async Task<CompareCounts?> CountBothSidesAsync(string fromSha, string toSha, CancellationToken cancellationToken)
    {
        var output = await ReadOutputAsync(cancellationToken, "rev-list", "--left-right", "--count", $"{fromSha}...{toSha}", "--").ConfigureAwait(false);
        var fields = output.Trim().Split('\t');
        return fields.Length == 2
            && int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var onlyOnFrom)
            && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var onlyOnTo)
                ? new CompareCounts(onlyOnFrom, onlyOnTo)
                : throw new GitException($"git rev-list --left-right --count {fromSha}...{toSha}", 0, $"Unexpected output: {output.Trim()}");
    }
}
