using System.Buffers;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

public sealed partial class GitRepository
{
    /// <summary>
    /// What <c>git log</c> prints for each commit of the graph, separated by the unit separator
    /// (<c>%x1f</c>). The subject comes last, so a separator inside it cannot shift the other
    /// fields. <c>%aN</c> and <c>%aE</c> apply <c>.mailmap</c>, as <c>git log</c> itself does.
    /// </summary>
    private const string LogFormat = "%H%x1f%P%x1f%aN%x1f%aE%x1f%aI%x1f%cI%x1f%s";

    private const int LogFieldCount = 7;

    /// <summary>What <c>git log -1</c> prints for the details of one commit, separated by NUL, which no commit message can contain.</summary>
    private const string DetailsFormat = "%H%x00%P%x00%aN%x00%aE%x00%aI%x00%cN%x00%cE%x00%cI%x00%s%x00%b";

    private const int DetailsFieldCount = 10;

    /// <summary>
    /// Options of every <c>git log</c> call that keep the user's configuration out of the output:
    /// no signature check (<c>log.showSignature</c>), no colours (<c>color.ui=always</c>) and
    /// UTF-8 text whatever <c>i18n.logOutputEncoding</c> says.
    /// </summary>
    private static readonly string[] PlainLogOptions = ["--no-show-signature", "--no-color", "--encoding=UTF-8"];

    /// <summary>The characters of a commit id as git prints it.</summary>
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdef");

    public Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refs);
        return LoadCommitsAsync(Task.FromResult(refs), onPage, cancellationToken);
    }

    public async Task LoadCommitsAsync(Task<RepoRefs> refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentNullException.ThrowIfNull(onPage);
        cancellationToken.ThrowIfCancellationRequested();

        // Refs already known, none of them, and an unborn HEAD: no commit to show, nothing to ask git.
        if (refs.IsCompletedSuccessfully && refs.Result.Refs.Count == 0 && refs.Result.Head.IsUnborn)
        {
            // Stashes alone are possible in theory (every branch deleted after stashing). They
            // are handed over on a background thread, as the contract promises.
            await Task.Run(new CommitPager(refs.Result.Stashes, onPage).Finish, cancellationToken).ConfigureAwait(false);
            return;
        }

        // git log does not depend on the refs, so it starts before they are known. HEAD is always
        // named, with --ignore-missing, which skips it when it is unborn: a detached HEAD's
        // commits are reachable from nothing else.
        var arguments = new List<string> { "log", "--date-order", "--format=" + LogFormat };
        arguments.AddRange(PlainLogOptions);
        arguments.AddRange(["--branches", "--remotes", "--tags", "--ignore-missing", "HEAD", "--"]);

        // The pager needs the stashes, so it is made when the first commit arrives, waiting for the
        // refs if they are still being read; git keeps writing into the pipe meanwhile.
        CommitPager? pager = null;
        CommitPager Pager() => pager ??= new CommitPager(refs.GetAwaiter().GetResult().Stashes, onPage);

        // Names and addresses repeat across commits; one string each keeps a large history small.
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        Func<string, string> pooled = text =>
        {
            if (!strings.TryGetValue(text, out var kept))
            {
                strings.Add(text, text);
                kept = text;
            }

            return kept;
        };

        var skipped = 0;
        var command = new GitCommand([.. arguments])
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironment,
            OnOutputLine = line =>
            {
                // A cancelled load must not hand over another page, even from output git
                // printed before it was stopped.
                cancellationToken.ThrowIfCancellationRequested();
                if (TryParseLogLine(line, pooled, out var commit))
                {
                    Pager().Add(commit);
                }
                else if (line.Length > 0)
                {
                    skipped++;
                }
            },
        };

        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        result.EnsureSuccess(command);
        if (skipped > 0)
        {
            _log.Warning($"git log printed {skipped} line(s) that could not be read in {WorkingDirectory}; those commits are missing from the graph.");
        }

        // A history without commits never made the pager; a failure to read the refs surfaces here.
        var known = await refs.ConfigureAwait(false);
        pager ??= new CommitPager(known.Stashes, onPage);
        pager.Finish();
    }

    public async Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        if (sha.StartsWith('-'))
        {
            throw new ArgumentException("A commit id cannot start with '-'.", nameof(sha));
        }

        var output = await ReadOutputAsync(cancellationToken, ["log", "-1", "--format=" + DetailsFormat, .. PlainLogOptions, sha, "--"]).ConfigureAwait(false);
        var fields = output.Split('\0', DetailsFieldCount);
        if (fields.Length != DetailsFieldCount || !TryParseIds(fields[0], fields[1], out var fullSha, out var parents))
        {
            throw new GitException($"git log -1 {sha}", 0, $"Unexpected output: {output.Trim()}");
        }

        var files = await ReadChangedFilesAsync(fullSha, parents, cancellationToken).ConfigureAwait(false);
        return new CommitDetails(
            fullSha,
            parents,
            fields[2],
            fields[3],
            GitDates.ParseOrEpoch(fields[4]),
            fields[5],
            fields[6],
            GitDates.ParseOrEpoch(fields[7]),
            fields[8],
            fields[9].TrimEnd(),
            files);
    }

    /// <summary>
    /// Reads the files a commit changed, compared with its first parent, or with an empty tree for
    /// a root commit. A merge and a stash are compared with their first parent too: plain
    /// <c>diff-tree</c> on a merge shows nothing. <c>-M</c> asks for renames whatever
    /// <c>diff.renames</c> says, and <c>-z</c> prints paths as they are.
    /// </summary>
    private async Task<IReadOnlyList<ChangedFile>> ReadChangedFilesAsync(string sha, IReadOnlyList<string> parents, CancellationToken cancellationToken)
    {
        List<string> arguments = ["diff-tree", "-r", "-z", "--name-status", "-M", "--no-ext-diff", "--no-color", "--no-commit-id"];
        switch (parents.Count)
        {
            case 0:
                arguments.AddRange(["--root", sha]);
                break;
            case 1:
                arguments.Add(sha);
                break;
            default:
                arguments.AddRange([parents[0], sha]);
                break;
        }

        return ParseNameStatus(await ReadOutputAsync(cancellationToken, [.. arguments]).ConfigureAwait(false));
    }

    /// <summary>
    /// Reads <c>--name-status -z</c> output: a status (<c>M</c>, or <c>R100</c> with a
    /// similarity score), then the path, or the old and the new path for a rename or a copy,
    /// each ended by NUL.
    /// </summary>
    internal static List<ChangedFile> ParseNameStatus(string output)
    {
        var files = new List<ChangedFile>();
        var tokens = output.Split('\0');
        var i = 0;
        while (i < tokens.Length)
        {
            var status = tokens[i++].Trim('\n', '\r');
            if (status.Length == 0)
            {
                continue;
            }

            var kind = status[0] switch
            {
                'A' => FileChangeKind.Added,
                'M' => FileChangeKind.Modified,
                'D' => FileChangeKind.Deleted,
                'R' => FileChangeKind.Renamed,
                'C' => FileChangeKind.Copied,
                'T' => FileChangeKind.TypeChanged,
                _ => FileChangeKind.Unknown,
            };

            if (kind is FileChangeKind.Renamed or FileChangeKind.Copied)
            {
                if (i + 1 >= tokens.Length)
                {
                    break;
                }

                var oldPath = tokens[i++];
                files.Add(new ChangedFile(tokens[i++], kind, oldPath));
            }
            else
            {
                if (i >= tokens.Length)
                {
                    break;
                }

                files.Add(new ChangedFile(tokens[i++], kind));
            }
        }

        return files;
    }

    /// <summary>
    /// Reads one line of <see cref="LogFormat"/>. Only a line whose commit id or parents cannot be
    /// read is refused; a date git could not print is read as the epoch (<see cref="GitDates.ParseOrEpoch"/>).
    /// </summary>
    private static bool TryParseLogLine(string line, Func<string, string> pooled, out CommitInfo commit)
    {
        commit = null!;
        var fields = line.Split('\x1f', LogFieldCount);
        if (fields.Length != LogFieldCount || !TryParseIds(fields[0], fields[1], out var sha, out var parents))
        {
            return false;
        }

        commit = new CommitInfo(
            sha,
            parents,
            pooled(fields[2]),
            pooled(fields[3]),
            GitDates.ParseOrEpoch(fields[4]),
            GitDates.ParseOrEpoch(fields[5]),
            fields[6]);
        return true;
    }

    /// <summary>
    /// Reads a commit id (<c>%H</c>) and its parents (<c>%P</c>, separated by spaces). Each must be
    /// a full object id of the same length as the commit's: 40 hex digits, or 64 in a SHA-256 repository.
    /// </summary>
    private static bool TryParseIds(string shaField, string parentsField, out string sha, out IReadOnlyList<string> parents)
    {
        sha = shaField;
        parents = parentsField.Length == 0 ? [] : parentsField.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!IsObjectId(sha))
        {
            return false;
        }

        foreach (var parent in parents)
        {
            if (parent.Length != sha.Length || !IsObjectId(parent))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsObjectId(string text) =>
        text.Length is 40 or 64 && !text.AsSpan().ContainsAnyExcept(HexDigits);
}
