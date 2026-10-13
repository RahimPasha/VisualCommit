using System.Text;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// A file's history, its blame and a commit's whole tree (docs/phase-5-design.md 4.1, 4.2, 4.5, 0.1,
/// D158, D164, D175).
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>The setting whose missing file stops a blame, as git names it in its message.</summary>
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
        // default order streams (0.1). -z prints paths as they are, whatever core.quotePath says.
        string[] arguments = ["log", "--follow", "--root", "-z", "-M", "--name-status", .. PlainLogOptions, "--format=" + LogFormat, startSha, "--", path];

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
        reader.Finish();
        if (reader.Skipped > 0)
        {
            _log.Warning($"git log --follow printed {reader.Skipped} part(s) that could not be read in {WorkingDirectory}; those commits are missing from the history of {path}.");
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
    /// Reads <c>git log --follow -z --name-status</c> as it streams (0.1): per commit the graph's
    /// header (<see cref="LogFormat"/>) ended by NUL, a line feed, then <c>status\0path\0</c>
    /// (<c>status\0old\0new\0</c> for a rename or copy, the status with its score, <c>R088</c>);
    /// the next header follows the last path directly. The runner hands the output over in lines,
    /// split at line feeds, so the lines are joined with line feeds again (a path may hold one)
    /// and the stream is read at NUL. A commit's first entry is its row; a path given for a
    /// folder lists several files in one commit, and only the first is kept.
    /// <para>
    /// The runner drops a carriage return that comes just before a line feed, so a path that holds
    /// "\r\n" loses its "\r". A commit's entries share their line with the next commit's header,
    /// whose line feed ends it, so a row arrives when git prints the next commit, the last one
    /// when git ends.
    /// </para>
    /// </summary>
    internal sealed class FileHistoryReader(Func<string, string> pooled, Action<FileHistoryRow> onRow)
    {
        private readonly StringBuilder _stream = new();
        private bool _started;

        /// <summary>The commit whose entries come now; null before the first and after a header that could not be read.</summary>
        private CommitInfo? _commit;

        /// <summary>The commit's row has been handed over: its other entries are left out.</summary>
        private bool _given;

        /// <summary>The status of the entry being read, while its paths are still to come.</summary>
        private string? _status;

        private readonly List<string> _paths = [];

        /// <summary>Headers that could not be read, and parts of the stream that fit nowhere.</summary>
        public int Skipped { get; private set; }

        /// <summary>Reads one line the runner handed over.</summary>
        public void Add(string line)
        {
            if (_started)
            {
                _stream.Append('\n');
            }

            _started = true;
            _stream.Append(line);
            var text = _stream.ToString();
            var start = 0;
            for (var end = text.IndexOf('\0', start); end >= 0; end = text.IndexOf('\0', start))
            {
                Read(text[start..end]);
                start = end + 1;
            }

            _stream.Clear().Append(text, start, text.Length - start);
        }

        /// <summary>Ends the stream: anything after its last NUL fits nowhere.</summary>
        public void Finish()
        {
            if (_stream.ToString().Trim('\n').Length > 0 || _status is not null)
            {
                Skipped++;
            }

            _stream.Clear();
            _status = null;
        }

        private void Read(string token)
        {
            if (_status is not null)
            {
                _paths.Add(token);
                if (_paths.Count == (_status[0] is 'R' or 'C' ? 2 : 1))
                {
                    GiveRow();
                }

                return;
            }

            // The line feed between a commit's header and its entries starts the first entry.
            var text = token.TrimStart('\n');
            if (IsHeader(text))
            {
                _given = false;
                _commit = TryParseLogLine(text, pooled, out var commit) ? commit : null;
                if (_commit is null)
                {
                    Skipped++;
                }

                return;
            }

            if (text.Length > 0)
            {
                _status = text;
                _paths.Clear();
            }
        }

        private void GiveRow()
        {
            var status = _status!;
            _status = null;
            if (_commit is null || _given)
            {
                return;
            }

            _given = true;
            var row = _paths.Count == 2
                ? new FileHistoryRow(_commit, KindOf(status), _paths[1], _paths[0])
                : new FileHistoryRow(_commit, KindOf(status), _paths[0]);
            onRow(row);
        }

        /// <summary>A commit's header: its id (40 or 64 hex digits), then the unit separator, which no status holds.</summary>
        private static bool IsHeader(string token)
        {
            var separator = token.IndexOf('\x1f', StringComparison.Ordinal);
            return separator is 40 or 64 && IsObjectId(token[..separator]);
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
