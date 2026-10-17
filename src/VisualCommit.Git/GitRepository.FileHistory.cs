using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// A file's history, its blame and a commit's whole tree (docs/phase-5-design.md 4.1, 4.2, 4.5, 0.1,
/// D158, D164, D175).
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>What git says when the file a <c>blame.ignoreRevsFile</c> setting names cannot be opened; the file follows.</summary>
    private const string IgnoreRevsFileMissing = "could not open object name list: ";

    public async Task ReadFileHistoryAsync(string startSha, string path, Action<FileHistoryRow> onRow, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startSha);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(onRow);
        if (startSha.StartsWith('-'))
        {
            throw new ArgumentException("A commit id cannot start with '-'.", nameof(startSha));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // --follow takes exactly one path and follows renames of 50 % or more (-M, as the diff's
        // --find-renames); --root keeps the file's first commit when log.showRoot is off. Only the
        // default order streams (0.1). Not -z: a commit's entries would then share a line with the
        // next commit's header and wait for it. core.quotePath quotes every byte above 127 too, so
        // an entry is one line of ASCII whatever its path holds.
        string[] arguments = ["-c", "core.quotePath=true", "log", "--follow", "--root", "-M", "--name-status", .. PlainLogOptions, "--format=" + LogFormat, startSha, "--", path];

        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        var reader = new FileHistoryReader(
            text =>
            {
                if (!strings.TryGetValue(text, out var kept))
                {
                    strings.Add(text, text);
                    kept = text;
                }

                return kept;
            },
            row =>
            {
                // A cancelled read must not hand over another row, even from output git printed
                // before it was stopped.
                cancellationToken.ThrowIfCancellationRequested();
                onRow(row);
            });
        var command = new GitCommand(arguments)
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironment,
            OnOutputLine = reader.Add,
        };

        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        result.EnsureSuccess(command);
        if (reader.Skipped > 0)
        {
            _log.Warning($"git log --follow printed {reader.Skipped} line(s) that could not be read in {WorkingDirectory}; those commits are missing from the history of {path}.");
        }
    }

    public async Task<BlameResult> ReadBlameAsync(string? sha, string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (sha is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sha);
            if (sha.StartsWith('-'))
            {
                throw new ArgumentException("A commit id cannot start with '-'.", nameof(sha));
            }
        }

        // The setting makes author and summary UTF-8 (blame has no --encoding); --no-textconv gives
        // the bytes the diff view shows. The user's blame.ignoreRevsFile is honoured (0.1).
        List<string> arguments = ["-c", "i18n.logOutputEncoding=UTF-8", "blame", "--porcelain", "--no-textconv"];
        if (sha is not null)
        {
            arguments.Add(sha);
        }

        arguments.AddRange(["--", path]);
        var command = new GitCommand([.. arguments]) { WorkingDirectory = WorkingDirectory, Environment = ReadEnvironment, Encoding = GitCommand.Latin1 };
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded && MissingIgnoreRevsFile(result.StandardError) is { } file)
        {
            // No option on the command line avoids it: git opens the configured file first.
            throw new BlameIgnoreRevsFileMissingException(file, result.StandardError);
        }

        if (!result.Succeeded && await IsGitlinkAsync(sha, path, cancellationToken).ConfigureAwait(false))
        {
            // A submodule has no lines: git refuses it with a fatal error of its own (15.9).
            return new BlameResult([]) { IsSubmodule = true };
        }

        result.EnsureSuccess(command);
        try
        {
            return BlameParser.Parse(result.StandardOutput);
        }
        catch (FormatException ex)
        {
            throw new GitException(command.DisplayText, result.ExitCode, $"Unexpected output: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Whether <paramref name="path"/> is a submodule in <paramref name="sha"/>'s tree
    /// (<c>ls-tree</c>), or in the index for the working tree's blame (<c>ls-files --stage</c>):
    /// its entry's mode is 160000. False when that cannot be read.
    /// </summary>
    private async Task<bool> IsGitlinkAsync(string? sha, string path, CancellationToken cancellationToken)
    {
        try
        {
            var output = sha is null
                ? await ReadOutputAsync(cancellationToken, "ls-files", "--stage", "-z", "--", path).ConfigureAwait(false)
                : await ReadOutputAsync(cancellationToken, "ls-tree", "-z", sha, "--", path).ConfigureAwait(false);
            return SplitNul(output).Any(entry => entry.StartsWith("160000 ", StringComparison.Ordinal));
        }
        catch (GitException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> ReadTreeFilesAsync(string sha, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        if (sha.StartsWith('-'))
        {
            throw new ArgumentException("A commit id cannot start with '-'.", nameof(sha));
        }

        // Run in the working tree's top folder, ls-tree lists the whole tree; -z prints paths as they are.
        var output = await ReadOutputAsync(cancellationToken, "ls-tree", "-r", "-z", "--name-only", sha).ConfigureAwait(false);
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>The file a <c>blame.ignoreRevsFile</c> setting names that git could not open, from git's message; null for any other failure.</summary>
    internal static string? MissingIgnoreRevsFile(string standardError)
    {
        var start = standardError.IndexOf(IgnoreRevsFileMissing, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += IgnoreRevsFileMissing.Length;
        var end = standardError.IndexOfAny(['\r', '\n'], start);
        return (end < 0 ? standardError[start..] : standardError[start..end]).Trim();
    }

    /// <summary>
    /// Reads <c>git log --follow --name-status</c> line by line as it streams (0.1): per commit the
    /// graph's header (<see cref="LogFormat"/>), an empty line, then one line per entry,
    /// <c>status\tpath</c> (<c>status\told\tnew</c> for a rename or copy, the status with its score,
    /// <c>R088</c>). Under <c>core.quotePath</c> git C-quotes a path that holds a tab, a line feed,
    /// any other control character, a quote, a backslash or a byte above 127, so an entry is one
    /// line of ASCII, split at its tabs, and never holds the header's unit separator. A commit's
    /// first entry is its row, handed over as soon as its line comes; a path given for a folder
    /// lists several files in one commit, and only the first is kept.
    /// </summary>
    internal sealed class FileHistoryReader(Func<string, string> pooled, Action<FileHistoryRow> onRow)
    {
        /// <summary>The commit whose entries come now; null before the first and after a header that could not be read.</summary>
        private CommitInfo? _commit;

        /// <summary>The commit's row has been handed over: its other entries are left out.</summary>
        private bool _given;

        /// <summary>Headers that could not be read, and lines that are neither a header nor an entry.</summary>
        public int Skipped { get; private set; }

        /// <summary>Reads one line the runner handed over.</summary>
        public void Add(string line)
        {
            // The empty line between a commit's header and its entries.
            if (line.Length == 0)
            {
                return;
            }

            if (IsHeader(line))
            {
                _given = false;
                _commit = TryParseLogLine(line, pooled, out var commit) ? commit : null;
                if (_commit is null)
                {
                    Skipped++;
                }

                return;
            }

            var fields = line.Split('\t');
            if (fields[0].Length == 0 || fields.Length != (fields[0][0] is 'R' or 'C' ? 3 : 2))
            {
                Skipped++;
                return;
            }

            if (_commit is null || _given)
            {
                return;
            }

            _given = true;
            var status = KindOf(fields[0]);
            var row = fields.Length == 3
                ? new FileHistoryRow(_commit, status, GitQuotedPaths.Unquote(fields[2]), GitQuotedPaths.Unquote(fields[1]))
                : new FileHistoryRow(_commit, status, GitQuotedPaths.Unquote(fields[1]));
            onRow(row);
        }

        /// <summary>A commit's header: its id (40 or 64 hex digits), then the unit separator, which no entry holds.</summary>
        private static bool IsHeader(string line)
        {
            var separator = line.IndexOf('\x1f', StringComparison.Ordinal);
            return separator is 40 or 64 && IsObjectId(line[..separator]);
        }

        /// <summary>The status letter as the changed-file list reads it (<see cref="ParseNameStatus"/>).</summary>
        private static FileChangeKind KindOf(string status) => status[0] switch
        {
            'A' => FileChangeKind.Added,
            'M' => FileChangeKind.Modified,
            'D' => FileChangeKind.Deleted,
            'R' => FileChangeKind.Renamed,
            'C' => FileChangeKind.Copied,
            'T' => FileChangeKind.TypeChanged,
            _ => FileChangeKind.Unknown,
        };
    }
}
