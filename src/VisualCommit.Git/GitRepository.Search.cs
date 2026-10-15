using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// The search over the graph's rows (docs/phase-5-design.md 3.2, 3.4, 0.3, D174): git is given the
/// rows' commit ids, never the graph's refs, so that it searches exactly what the graph shows and
/// prints its matches in the rows' order.
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>What a commit's line starts with in the Files mode (<c>%x1e</c>): the record separator, which git C-quotes in a path.</summary>
    private const char SearchHeaderMark = '\x1e';

    /// <summary>
    /// Git reads all of standard input before it prints, so the top of the graph goes first in a
    /// small call: the first match comes at once, for little more in total (0.3).
    /// </summary>
    internal int FirstSearchChunk { get; set; } = 2_000;

    /// <summary>The rows each later call is given.</summary>
    internal int SearchChunk { get; set; } = 20_000;

    /// <summary>
    /// Set when git refused <c>--diff-merges</c>, which Git 2.31 brought: from then on the Changes
    /// mode leaves it out, and <c>--first-parent</c> alone gives merges their first-parent change
    /// (Git 2.29 and later).
    /// </summary>
    private volatile bool _noDiffMergesOption;

    public async Task SearchAsync(SearchMode mode, string text, IReadOnlyList<string> shas, Action<string> onMatch, IProgress<int>? progress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(shas);
        ArgumentNullException.ThrowIfNull(onMatch);
        foreach (var sha in shas)
        {
            // Standard input takes revisions, and newer git options too: only commit ids go there.
            if (!IsObjectId(sha))
            {
                throw new ArgumentException($"'{sha}' is not a full commit id.", nameof(shas));
            }
        }

        // An empty text matches everything (and an empty -S is an error, 0.3); a NUL no argument can carry.
        if (text.Length == 0 || text.Contains('\0', StringComparison.Ordinal))
        {
            return;
        }

        var searched = 0;
        while (searched < shas.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var size = Math.Min(searched == 0 ? FirstSearchChunk : SearchChunk, shas.Count - searched);
            var input = new StringBuilder(size * 41);
            for (var i = searched; i < searched + size; i++)
            {
                input.Append(shas[i]).Append('\n');
            }

            if (!await SearchChunkAsync(mode, text, input.ToString(), onMatch, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            searched += size;
            progress?.Report(searched);
        }
    }

    /// <summary>
    /// Searches one chunk of rows and hands over each match as git prints it. False when git no
    /// longer knows one of the commits (the graph is older than a <c>gc</c> that pruned it): the
    /// search ends quietly, and the next refresh runs it again.
    /// </summary>
    private async Task<bool> SearchChunkAsync(SearchMode mode, string text, string input, Action<string> onMatch, CancellationToken cancellationToken)
    {
        while (true)
        {
            var command = SearchCommand(mode, text, input, onMatch, cancellationToken);
            var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                return true;
            }

            if (mode == SearchMode.Changes && !_noDiffMergesOption && result.StandardError.Contains("--diff-merges", StringComparison.Ordinal))
            {
                // Git before 2.31: the same search without the option.
                _log.Info($"{Name}: git does not know --diff-merges; searching changes with --first-parent alone.");
                _noDiffMergesOption = true;
                continue;
            }

            if (result.ExitCode == 128 && result.StandardError.Contains("bad object", StringComparison.Ordinal))
            {
                _log.Debug($"{Name}: The search stopped: a commit of the graph no longer exists ({result.StandardError.Trim()}).");
                return false;
            }

            result.EnsureSuccess(command);
        }
    }

    /// <summary>
    /// The call for one chunk (3.2): <c>git log --no-walk=unsorted --stdin</c> prints only the
    /// commits it is given, in their order, and flushes after each; each mode's options decide
    /// which of them it prints.
    /// </summary>
    private GitCommand SearchCommand(SearchMode mode, string text, string input, Action<string> onMatch, CancellationToken cancellationToken)
    {
        // --encoding=UTF-8 matters to the Commits mode: git matches --grep against the message
        // re-encoded to the output encoding, which the user's i18n.logOutputEncoding would set.
        List<string> arguments = ["log", "--no-walk=unsorted", "--stdin", .. PlainLogOptions];
        var environment = ReadEnvironment;
        Action<string> onLine = line =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsObjectId(line))
            {
                onMatch(line);
            }
        };

        switch (mode)
        {
            case SearchMode.Commits:
                // -F keeps the user's grep.patternType from reading the text as a pattern; the whole
                // message is searched, its case folded. Authors and ids the app matches itself.
                arguments.AddRange(["--format=%H", "-F", "--regexp-ignore-case", "--grep=" + text, "--"]);
                break;

            case SearchMode.Files:
                // A match is a commit whose own change (against its first parent; --root for a
                // root commit whatever log.showRoot says) lists a path the globs match. Git 2.55.0
                // prints every commit given to --no-walk whatever the path (a regression, fixed
                // upstream by 32c4ed70), so a commit counts only when a path follows its line;
                // listing the paths works on both. Glob magic needs GIT_LITERAL_PATHSPECS removed,
                // as AllIgnoredAsync removes it; a glob that matches a folder does not match the
                // files under it, hence the second pathspec.
                var glob = EscapeGlob(text.Replace('\\', '/'));
                arguments.AddRange([
                    "--format=%x1e%H", "--first-parent", "--root", "--no-renames", "--name-only",
                    "--", $":(glob,icase)**/*{glob}*", $":(glob,icase)**/*{glob}*/**"]);
                environment = new Dictionary<string, string?>(ReadEnvironment) { ["GIT_LITERAL_PATHSPECS"] = null };
                string? pending = null;
                onLine = line =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (line.Length > 0 && line[0] == SearchHeaderMark)
                    {
                        pending = IsObjectId(line[1..]) ? line[1..] : null;
                    }
                    else if (line.Length > 0 && pending is not null)
                    {
                        onMatch(pending);
                        pending = null;
                    }
                };
                break;

            case SearchMode.Changes:
                // -S: the number of the text's occurrences changed (O7), a fixed string, its case
                // folded for A to Z. The commit's change against its first parent, a merge's too;
                // the root's whatever log.showRoot says, renames whatever diff.renames says, and the
                // bytes the diff shows, without textconv. -s prints no diff.
                arguments.AddRange(["--format=%H", "--first-parent"]);
                if (!_noDiffMergesOption)
                {
                    arguments.Add("--diff-merges=first-parent");
                }

                arguments.AddRange(["--root", "--find-renames", "--no-textconv", "-s", "--regexp-ignore-case", "-S" + text, "--"]);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        return new GitCommand([.. arguments])
        {
            WorkingDirectory = WorkingDirectory,
            Environment = environment,
            StandardInput = input,
            OnOutputLine = onLine,
        };
    }

    /// <summary>Text for a glob that matches it literally: <c>*</c>, <c>?</c>, <c>[</c> and <c>\</c> each escaped by a backslash.</summary>
    internal static string EscapeGlob(string text)
    {
        var escaped = new StringBuilder(text.Length + 4);
        foreach (var c in text)
        {
            if (c is '*' or '?' or '[' or '\\')
            {
                escaped.Append('\\');
            }

            escaped.Append(c);
        }

        return escaped.ToString();
    }
}
